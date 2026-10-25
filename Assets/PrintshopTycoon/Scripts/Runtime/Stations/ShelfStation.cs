using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    public class ShelfStation : Interactable
    {
        readonly List<GameObject> shirtStacks = new List<GameObject>();

        public override string DisplayName => Loc.T("station.shelf");


        public static ShelfStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("StorageShelf").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var station = root.gameObject.AddComponent<ShelfStation>();

            float w = f.width, d = f.depth;
            foreach (var sx in new[] { -1f, 1f })
                Shapes.Box(root, "Side", new Vector3(sx * (w * 0.5f - 0.05f), 1.1f, 0f), new Vector3(0.1f, 2.2f, d), Palette.WoodDark);
            Shapes.Box(root, "Back", new Vector3(0f, 1.1f, d * 0.5f - 0.03f), new Vector3(w, 2.2f, 0.06f), Palette.Wood);
            float[] levels = { 0.15f, 0.85f, 1.55f };
            foreach (var y in levels)
                Shapes.Box(root, "Board", new Vector3(0f, y, 0f), new Vector3(w - 0.1f, 0.08f, d - 0.05f), Palette.Wood);
            // Folded shirt stacks: 4 per level, each stack shows up to 2 shirts.
            foreach (var y in levels)
                for (int i = 0; i < 4; i++)
                    for (int k = 0; k < 2; k++)
                    {
                        float x = -w * 0.5f + 0.35f + i * ((w - 0.7f) / 3f);
                        var s = Shapes.Box(root, "Shirt", new Vector3(x, y + 0.1f + k * 0.12f, -0.05f), new Vector3(0.38f, 0.1f, 0.5f), Palette.Shirt);
                        station.shirtStacks.Add(s);
                    }
            Shapes.Box(root, "Label", new Vector3(0f, 2.05f, -d * 0.5f), new Vector3(0.9f, 0.22f, 0.02f), Palette.WallTrim);
            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 1.1f, 0f), new Vector3(w, 2.2f, d));
            return station;
        }

        protected override void OnSetup()
        {
            Session.Inventory.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (Game != null) Session.Inventory.Changed -= Refresh;
        }

        void Refresh()
        {
            int stock = Session.Inventory.BlankStock;
            for (int i = 0; i < shirtStacks.Count; i++) shirtStacks[i].SetActive(i < stock);
        }

        bool NeedsShirtsForOrder => Session.Orders.Step == OrderStep.CollectBlanks;

        public override bool TryGetAction(out string label)
        {
            if (NeedsShirtsForOrder && Session.Inventory.BlankStock >= Session.Orders.Order.Quantity)
                label = Loc.T("act.take_blanks");
            else
                label = Loc.T("act.restock", Session.Balance.restockAmount, Session.Balance.restockCost);
            return true;
        }

        public override void Interact()
        {
            if (NeedsShirtsForOrder && Session.Inventory.BlankStock >= Session.Orders.Order.Quantity)
            {
                var r = Session.Orders.CollectBlanks(Session.Inventory);
                if (r == ActionResult.Ok) { Punch(); Game.Audio.Play(Sfx.Pickup); }
                else Report(r);
                return;
            }
            var restock = Session.TryRestock();
            if (restock == ActionResult.Ok)
            {
                Punch();
                Game.Audio.Play(Sfx.Purchase);
                Game.UI.Toast(Loc.T("toast.restocked", Session.Balance.restockAmount));
            }
            else Report(restock);
        }

        public override string StatusText => Loc.T("hud.stock", Session.Inventory.BlankStock);
    }
}
