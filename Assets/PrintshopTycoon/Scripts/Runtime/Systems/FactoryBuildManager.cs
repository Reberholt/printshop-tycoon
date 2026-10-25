using System;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Placement flow for new machines: preview ghost -> drag -> validate -> confirm (pay + spawn).
    /// Validation and payment are done by GameSession, the ghost is pure presentation.
    /// </summary>
    public class FactoryBuildManager : MonoBehaviour
    {
        GameManager game;
        Transform ghost;
        readonly List<Renderer> ghostRenderers = new List<Renderer>();
        MachineDefinition placing;
        Vector3 ghostPosition;
        bool lastValid;

        public bool IsPlacing => placing != null;
        public bool CurrentValid => lastValid;
        public MachineDefinition Placing => placing;
        public event Action PlacementChanged;

        public void Init(GameManager gameManager) => game = gameManager;

        public void BeginPlacement(MachineDefinition def)
        {
            CancelPlacement();
            placing = def;
            ghost = new GameObject("PlacementGhost").transform;
            if (def.typeId == MachineDefinition.HeatPress)
                HeatPressStation.BuildVisual(ghost, null);
            Shapes.Box(ghost, "Footprint", new Vector3(0f, 0.03f, 0f), new Vector3(def.width, 0.04f, def.depth), Palette.Valid);
            ghostRenderers.Clear();
            ghostRenderers.AddRange(ghost.GetComponentsInChildren<Renderer>());

            // Start at the first free spot in the build area so the player sees a valid preview.
            var area = WorkshopLayout.BuildArea;
            Vector3 start = new Vector3(area.centerX, 0f, area.centerZ);
            for (float z = area.MaxZ - def.depth * 0.5f; z >= area.MinZ + def.depth * 0.5f; z -= 0.5f)
            {
                bool found = false;
                for (float x = area.MinX + def.width * 0.5f; x <= area.MaxX - def.width * 0.5f; x += 0.5f)
                {
                    if (game.Session.IsPlacementValid(def.typeId, Snap(x), Snap(z))) { start = new Vector3(Snap(x), 0f, Snap(z)); found = true; break; }
                }
                if (found) break;
            }
            MoveGhostTo(start);
            PlacementChanged?.Invoke();
        }

        float Snap(float v) => game.Session.Placement.Snap(v);

        public void DragTo(Vector2 screenPosition)
        {
            if (!IsPlacing) return;
            if (game.Cameras.ScreenToFloor(screenPosition, out var p))
                MoveGhostTo(p);
        }

        void MoveGhostTo(Vector3 world)
        {
            ghostPosition = new Vector3(Snap(world.x), 0f, Snap(world.z));
            ghost.position = ghostPosition + Vector3.up * 0.05f;
            bool valid = game.Session.IsPlacementValid(placing.typeId, ghostPosition.x, ghostPosition.z);
            var tint = MaterialLibrary.Get(valid ? Palette.Valid : Palette.Invalid);
            foreach (var r in ghostRenderers) if (r != null) r.sharedMaterial = tint;
            if (valid != lastValid) game.Audio.Play(Sfx.Tick, 0.4f);
            lastValid = valid;
            PlacementChanged?.Invoke();
        }

        public ActionResult ConfirmPlacement()
        {
            if (!IsPlacing) return ActionResult.WrongStep;
            var def = placing;
            var r = game.Session.TryPurchaseMachine(def.typeId, ghostPosition.x, ghostPosition.z, out _);
            if (r == ActionResult.Ok)
            {
                CancelPlacement(); // MachineAdded event spawns the real machine
            }
            return r;
        }

        public void CancelPlacement()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            placing = null;
            ghostRenderers.Clear();
            lastValid = false;
            PlacementChanged?.Invoke();
        }
    }
}
