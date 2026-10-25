using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Storage shelf with visible folded blanks per product. Hue takes blanks for the current
    /// order here, or buys a new batch (limited by shelf capacity). A side rack appears with
    /// the capacity upgrade.
    /// </summary>
    public class ShelfStation : Interactable
    {
        class Row
        {
            public string productId;
            public readonly List<GameObject> items = new List<GameObject>();
        }

        readonly List<Row> rows = new List<Row>();
        GameObject sideRack;
        Transform root;

        public override string DisplayName => Loc.T("station.shelf");

        public static ShelfStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("StorageShelf").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var station = root.gameObject.AddComponent<ShelfStation>();
            station.root = root;

            float w = f.width, d = f.depth;
            foreach (var sx in new[] { -1f, 1f })
                Shapes.Box(root, "Side", new Vector3(sx * (w * 0.5f - 0.05f), 1.1f, 0f), new Vector3(0.1f, 2.2f, d), Palette.WoodDark);
            Shapes.Box(root, "Back", new Vector3(0f, 1.1f, d * 0.5f - 0.03f), new Vector3(w, 2.2f, 0.06f), Palette.Wood);
            float[] levels = { 0.15f, 0.85f, 1.55f };
            foreach (var y in levels)
                Shapes.Box(root, "Board", new Vector3(0f, y, 0f), new Vector3(w - 0.1f, 0.08f, d - 0.05f), Palette.Wood);
            // Little label plates per row.
            Shapes.Box(root, "Label", new Vector3(0f, 2.05f, -d * 0.5f), new Vector3(0.9f, 0.22f, 0.02f), Palette.WallTrim);

            // Top + middle rows: T-shirts, bottom row: sweatshirts.
            station.rows.Add(station.BuildRow(ProductDefinition.TShirt, levels[2], w, 8));
            station.rows.Add(station.BuildRow(ProductDefinition.TShirt, levels[1], w, 8));
            station.rows.Add(station.BuildRow(ProductDefinition.Sweatshirt, levels[0], w, 6));

            // Capacity upgrade: extra rack with boxes beside the shelf.
            station.sideRack = new GameObject("SideRack");
            station.sideRack.transform.SetParent(root, false);
            station.sideRack.transform.localPosition = new Vector3(w * 0.5f + 0.45f, 0f, 0f);
            Shapes.Box(station.sideRack.transform, "Frame", new Vector3(0f, 0.7f, 0f), new Vector3(0.7f, 1.4f, d), Palette.MetalDark);
            for (int i = 0; i < 3; i++)
                Shapes.Box(station.sideRack.transform, "Box", new Vector3(0f, 0.25f + i * 0.42f, -0.05f), new Vector3(0.55f, 0.36f, d - 0.2f), Palette.Cardboard);
            station.sideRack.SetActive(false);

            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 1.1f, 0f), new Vector3(w, 2.2f, d));
            return station;
        }

        Row BuildRow(string productId, float y, float width, int count)
        {
            var row = new Row { productId = productId };
            for (int i = 0; i < count; i++)
            {
                float x = -width * 0.5f + 0.3f + (i % 4) * ((width - 0.6f) / 3f);
                float h = 0.1f + (i / 4) * 0.11f;
                row.items.Add(Shapes.Box(root, "Stack", new Vector3(x, y + h, -0.05f), new Vector3(0.38f, 0.1f, 0.5f), Color.white));
            }
            return row;
        }

        protected override void OnSetup()
        {
            Session.Inventory.Changed += Refresh;
            Session.Upgrades.Upgraded += OnUpgraded;
            for (int r = 0; r < rows.Count; r++)
            {
                var look = ProductLook.For(Session.Balance.FindProduct(rows[r].productId));
                foreach (var item in rows[r].items) item.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(look.Body);
            }
            Refresh();
            OnUpgraded(null, 0);
        }

        void OnDestroy()
        {
            if (Game == null) return;
            Session.Inventory.Changed -= Refresh;
            Session.Upgrades.Upgraded -= OnUpgraded;
        }

        void OnUpgraded(string id, int level)
        {
            sideRack.SetActive(Session.Upgrades.Level(UpgradeDefinition.StorageCapacity) > 0);
        }

        void Refresh()
        {
            // Each visible folded stack stands for ~2 blanks; rows of the same product share the stock.
            var shown = new Dictionary<string, int>();
            foreach (var row in rows)
            {
                shown.TryGetValue(row.productId, out int used);
                int remaining = Mathf.CeilToInt((Session.Inventory.Stock(row.productId) - used * 2) / 2f);
                int visible = Mathf.Clamp(remaining, 0, row.items.Count);
                for (int i = 0; i < row.items.Count; i++) row.items[i].SetActive(i < visible);
                shown[row.productId] = used + visible;
            }
        }

        bool CanTakeForOrder =>
            Session.Orders.Step == OrderStep.CollectBlanks &&
            Session.Inventory.Stock(Session.Orders.Order.ProductId) >= Session.Orders.Order.Quantity;

        string ProductName(string productId)
        {
            var p = Session.Balance.FindProduct(productId);
            return p != null ? Loc.T(p.nameKey) : productId;
        }

        public override bool TryGetAction(out string label)
        {
            if (CanTakeForOrder)
            {
                label = Loc.T("act.take_items", Session.Orders.Order.Quantity, ProductName(Session.Orders.Order.ProductId));
                return true;
            }
            string product = Session.ShelfProduct;
            int amount = Session.RestockAmountFor(product);
            if (amount <= 0) { label = Loc.T("act.storage_full"); return false; }
            label = Loc.T("act.restock_product", amount, ProductName(product), Session.RestockCostFor(product));
            return true;
        }

        public override void Interact()
        {
            if (CanTakeForOrder)
            {
                var r = Session.Orders.CollectBlanks(Session.Inventory);
                if (r == ActionResult.Ok) { Punch(); Game.Audio.Play(Sfx.Pickup); Game.Fx.Sparkle(TopPosition, Color.white, 4); }
                else Report(r);
                return;
            }
            string product = Session.ShelfProduct;
            var restock = Session.TryRestock(product, out int added);
            if (restock == ActionResult.Ok)
            {
                Punch();
                Game.Audio.Play(Sfx.Purchase);
                Game.UI.Toast(Loc.T("toast.restocked_product", added, ProductName(product)));
            }
            else Report(restock);
        }

        public override string StatusText =>
            Loc.T("hud.stock_capacity", Session.Inventory.Total, Session.Inventory.Capacity);
    }
}
