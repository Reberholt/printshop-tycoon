using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    public class OrderBoardStation : Interactable
    {
        GameObject highlightNote;

        public override string DisplayName => Loc.T("station.board");

        public static OrderBoardStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("OrderBoard").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            Shapes.Box(root, "Frame", new Vector3(0f, 1.9f, 0f), new Vector3(f.width + 0.2f, 1.5f, 0.12f), Palette.WoodDark);
            Shapes.Box(root, "Cork", new Vector3(0f, 1.9f, -0.05f), new Vector3(f.width, 1.3f, 0.06f), Palette.BoardYellow);
            Shapes.Box(root, "NoteA", new Vector3(-0.55f, 2.15f, -0.1f), new Vector3(0.38f, 0.42f, 0.02f), Color.white);
            Shapes.Box(root, "NoteB", new Vector3(0.55f, 1.65f, -0.1f), new Vector3(0.38f, 0.42f, 0.02f), MaterialLibrary.Hex("#BDE0FE"));
            var note = Shapes.Box(root, "NewOrder", new Vector3(0.05f, 1.95f, -0.12f), new Vector3(0.5f, 0.6f, 0.02f), MaterialLibrary.Hex("#FFADAD"));
            Shapes.Sphere(note.transform, "Pin", new Vector3(0f, 0.4f, -1f), 0.25f, Palette.PressRed);
            // Little counter in front so the board reads as a "station" from above.
            Shapes.Box(root, "Desk", new Vector3(0f, 0.45f, -0.05f), new Vector3(f.width, 0.9f, 0.35f), Palette.Wood);

            var station = root.gameObject.AddComponent<OrderBoardStation>();
            station.highlightNote = note;
            var col = Shapes.AddCollider(root.gameObject, new Vector3(0f, 1.2f, 0f), new Vector3(f.width, 2.4f, f.depth));
            station.Body = col;
            return station;
        }


        void Update()
        {
            if (Game == null) return;
            bool offered = Session.Orders.Step == OrderStep.Offered;
            highlightNote.SetActive(offered);
            if (offered) highlightNote.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 4f);
        }

        public override bool TryGetAction(out string label)
        {
            label = Loc.T("act.accept");
            return true;
        }

        public override void Interact()
        {
            Punch();
            Game.Audio.Play(Sfx.Click);
            Game.UI.ShowOrderPanel();
        }

        public override string StatusText => Loc.T("order.title", Loc.T(Session.Orders.Order.CustomerKey), Session.Orders.Order.Quantity);
    }
}
