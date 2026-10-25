using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// A heat press (starter or purchased). Used by Hue through the minigame, or by an
    /// assigned worker. Occupancy lives in GameSession so both can never press at once.
    /// </summary>
    public class HeatPressStation : Interactable
    {
        public const float BodyOffsetZ = 0.4f;   // machine sits at the back of its footprint
        public static readonly Vector3 ApproachLocal = new Vector3(0f, 0f, -0.75f);

        Transform lidPivot;
        GameObject shirtsOnPlaten;
        Renderer statusLamp;
        Renderer shirtsRenderer;
        Color shownShirtColor = Color.clear;
        Color shownLampColor = Color.clear;
        float lidAngle = -32f;
        bool forcedClosed;

        public string MachineId { get; private set; }

        public override string DisplayName => Loc.T("machine.heat_press");

        public static HeatPressStation Build(Transform parent, MachineInstance machine, MachineDefinition def)
        {
            var root = new GameObject("HeatPress_" + machine.id).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(machine.x, 0f, machine.z);
            var station = root.gameObject.AddComponent<HeatPressStation>();
            station.MachineId = machine.id;
            BuildVisual(root, station);
            float w = def != null ? def.width : 1.6f;
            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 0.75f, BodyOffsetZ), new Vector3(w - 0.2f, 1.5f, 1.3f));
            return station;
        }

        /// <summary>Visual only - also used for the placement ghost.</summary>
        public static void BuildVisual(Transform root, HeatPressStation station)
        {
            var m = new GameObject("Machine").transform;
            m.SetParent(root, false);
            m.localPosition = new Vector3(0f, 0f, BodyOffsetZ);

            Shapes.RoundedBox(m, "Cabinet", new Vector3(0f, 0.42f, 0f), new Vector3(1.2f, 0.84f, 1.1f), 0.18f, Palette.MetalDark);
            Shapes.RoundedBox(m, "Platen", new Vector3(0f, 0.92f, -0.05f), new Vector3(1.0f, 0.12f, 0.8f), 0.12f, Palette.Metal);
            Shapes.Box(m, "Column", new Vector3(0f, 1.25f, 0.45f), new Vector3(0.24f, 0.8f, 0.24f), Palette.MetalDark);

            var pivot = new GameObject("LidPivot").transform;
            pivot.SetParent(m, false);
            pivot.localPosition = new Vector3(0f, 1.2f, 0.45f);
            Shapes.RoundedBox(pivot, "Lid", new Vector3(0f, -0.08f, -0.48f), new Vector3(1.05f, 0.18f, 0.9f), 0.14f, Palette.PressRed);
            var handle = Shapes.Cylinder(pivot, "Handle", new Vector3(0f, 0.02f, -0.98f), 0.1f, 0.7f, Palette.UiDark);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var shirts = Shapes.Box(m, "ShirtsOnPlaten", new Vector3(0f, 1.0f, -0.05f), new Vector3(0.7f, 0.06f, 0.55f), Palette.Shirt);
            shirts.SetActive(false);
            var lamp = Shapes.Sphere(m, "Lamp", new Vector3(0.45f, 0.7f, -0.56f), 0.16f, Palette.Valid);

            if (station != null)
            {
                station.lidPivot = pivot;
                station.shirtsOnPlaten = shirts;
                station.shirtsRenderer = shirts.GetComponent<Renderer>();
                station.statusLamp = lamp.GetComponent<Renderer>();
                pivot.localRotation = Quaternion.Euler(station.lidAngle, 0f, 0f);
            }
            else pivot.localRotation = Quaternion.Euler(-32f, 0f, 0f);
        }

        /// <summary>Closes the lid while Hue plays the minigame.</summary>
        public void SetForcedClosed(bool closed) => forcedClosed = closed;

        string Occupant => Session.GetOccupant(MachineId);
        bool OrderShirtsHere => Session.Orders.Step == OrderStep.CollectFinished && Session.Orders.PressStationId == MachineId;

        void Update()
        {
            if (Game == null) return;
            var worker = Occupant != null && Occupant != GameSession.PlayerOccupant ? Session.Workers.FirstLike(Occupant) : null;
            bool workerPressing = worker != null && worker.State == WorkerState.Working;
            bool closed = forcedClosed || workerPressing;

            float target = closed ? 0f : -32f;
            lidAngle = Mathf.MoveTowards(lidAngle, target, 160f * Time.deltaTime);
            lidPivot.localRotation = Quaternion.Euler(lidAngle, 0f, 0f);

            bool shirtsHere = OrderShirtsHere;
            shirtsOnPlaten.SetActive(shirtsHere || workerPressing);
            Color shirtColor = Palette.Shirt;
            if (shirtsHere)
            {
                var logo = Session.Balance.FindLogo(Session.Orders.Order.LogoId);
                if (logo != null) shirtColor = MaterialLibrary.Hex(logo.colorHex);
            }
            Color lampColor = Occupant == null ? Palette.Valid : (closed ? Palette.UiAccent : Palette.BoardYellow);
            // Only touch materials when something changed (no per-frame lookups on mobile).
            if (shirtColor != shownShirtColor)
            {
                shownShirtColor = shirtColor;
                shirtsRenderer.sharedMaterial = MaterialLibrary.Get(shirtColor);
            }
            if (lampColor != shownLampColor)
            {
                shownLampColor = lampColor;
                statusLamp.sharedMaterial = MaterialLibrary.Get(lampColor);
            }
        }

        public override bool TryGetAction(out string label)
        {
            if (OrderShirtsHere)
            {
                label = Loc.T("act.collect");
                return true;
            }
            label = Loc.T("act.press");
            if (Session.Orders.Step != OrderStep.PressShirts) return false;
            var occ = Occupant;
            return occ == null || occ == GameSession.PlayerOccupant;
        }

        public override void Interact()
        {
            if (OrderShirtsHere)
            {
                var r = Session.TryCollectFinished(MachineId);
                if (r == ActionResult.Ok) { Punch(); Game.Audio.Play(Sfx.Pickup); }
                else Report(r);
                return;
            }
            var begin = Session.TryBeginPlayerPress(MachineId);
            if (begin != ActionResult.Ok) { Report(begin); return; }
            Game.Audio.Play(Sfx.Click);
            Game.UI.ShowHeatPressMinigame(this);
        }

        /// <summary>Called by the minigame view with the result.</summary>
        public ActionResult ApplyPressResult(PressResult result)
        {
            var r = Session.ResolvePlayerPress(MachineId, result);
            if (r == ActionResult.Ok)
            {
                Punch();
                Game.Fx.Steam(transform.position + Vector3.up * 1.4f);
            }
            return r;
        }

        public void CancelPress()
        {
            SetForcedClosed(false);
            Session.CancelPlayerPress(MachineId);
        }

        public override string StatusText
        {
            get
            {
                var occ = Occupant;
                string s = occ == null ? Loc.T("info.free")
                    : occ == GameSession.PlayerOccupant ? Loc.T("info.occupied_by_player")
                    : Loc.T("worker." + (Session.Workers.FirstLike(occ)?.State == WorkerState.Working ? "working" : "moving"));
                var assigned = Session.FindWorkerAssignedTo(MachineId);
                if (assigned != null) s += "\n" + Loc.T("info.worker", GameManager.WorkerNumber(assigned.Id));
                return s;
            }
        }
    }

    static class WorkerListExtensions
    {
        public static WorkerTaskMachine FirstLike(this System.Collections.Generic.IReadOnlyList<WorkerTaskMachine> list, string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }
    }
}
