using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>Order board: new orders wiggle on the board; a special (timed) offer gets a red star note.</summary>
    public class OrderBoardStation : Interactable
    {
        GameObject highlightNote;
        GameObject specialNote;
        Vector3 specialBaseScale;

        public override string DisplayName => Loc.T("station.board");

        public static OrderBoardStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("OrderBoard").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            Shapes.Box(root, "Frame", new Vector3(0f, 1.9f, 0f), new Vector3(f.width + 0.2f, 1.5f, 0.12f), Palette.WoodDark);
            Shapes.Box(root, "Cork", new Vector3(0f, 1.9f, -0.05f), new Vector3(f.width, 1.3f, 0.06f), Palette.BoardYellow);
            Shapes.Box(root, "NoteA", new Vector3(-0.6f, 2.2f, -0.1f), new Vector3(0.34f, 0.38f, 0.02f), Color.white);
            Shapes.Box(root, "NoteB", new Vector3(-0.62f, 1.62f, -0.1f), new Vector3(0.34f, 0.38f, 0.02f), MaterialLibrary.Hex("#BDE0FE"));
            var note = Shapes.Box(root, "NewOrder", new Vector3(0.0f, 1.95f, -0.12f), new Vector3(0.5f, 0.6f, 0.02f), MaterialLibrary.Hex("#FFADAD"));
            Shapes.Sphere(note.transform, "Pin", new Vector3(0f, 0.4f, -1f), 0.25f, Palette.PressRed);
            var special = Shapes.Box(root, "SpecialOrder", new Vector3(0.62f, 1.95f, -0.12f), new Vector3(0.42f, 0.55f, 0.02f), CharacterFactory.BrandRed);
            Shapes.Box(special.transform, "Star", new Vector3(0f, 0.15f, -1f), new Vector3(0.45f, 0.35f, 0.5f), Palette.UiCoin).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Shapes.Box(special.transform, "Line", new Vector3(0f, -0.25f, -1f), new Vector3(0.7f, 0.06f, 0.5f), Color.white);
            // Counter in front so the board reads as a "station" from above, with a phone and a mug.
            Shapes.Box(root, "Desk", new Vector3(0f, 0.45f, -0.05f), new Vector3(f.width, 0.9f, 0.35f), Palette.Wood);
            Shapes.Box(root, "Phone", new Vector3(-0.6f, 0.93f, -0.08f), new Vector3(0.22f, 0.06f, 0.16f), Palette.UiDark);
            Shapes.Cylinder(root, "Mug", new Vector3(0.65f, 0.97f, -0.08f), 0.11f, 0.14f, CharacterFactory.BrandRed);

            var station = root.gameObject.AddComponent<OrderBoardStation>();
            station.highlightNote = note;
            station.specialNote = special;
            station.specialBaseScale = special.transform.localScale;
            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 1.2f, 0f), new Vector3(f.width, 2.4f, f.depth));
            return station;
        }

        void Update()
        {
            if (Game == null) return;
            bool offered = Session.Orders.Step == OrderStep.Offered;
            highlightNote.SetActive(offered);
            if (offered) highlightNote.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f) * 4f);
            bool special = Session.Orders.OfferedSpecial != null;
            specialNote.SetActive(special);
            if (special) specialNote.transform.localScale = specialBaseScale * (1f + Mathf.Sin(Time.time * 5f) * 0.06f);
        }

        public override bool TryGetAction(out string label)
        {
            label = Session.Orders.OfferedSpecial != null ? Loc.T("act.accept_special") : Loc.T("act.accept");
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
