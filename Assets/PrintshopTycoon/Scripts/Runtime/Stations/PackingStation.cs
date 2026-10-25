using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Packing table: the finished garments drop into the box, the flaps fold shut, tape goes
    /// on and the parcel hops onto the pallet - then the completion screen shows the payout.
    /// </summary>
    public class PackingStation : Interactable
    {
        readonly List<GameObject> palletBoxes = new List<GameObject>();
        Transform box;
        Transform flapL, flapR;
        GameObject tape;
        Transform contents;
        bool animating;

        public override string DisplayName => Loc.T("station.packing");

        public static PackingStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("PackingTable").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var s = root.gameObject.AddComponent<PackingStation>();

            Shapes.RoundedBox(root, "Top", new Vector3(0f, 0.9f, 0f), new Vector3(f.width, 0.12f, f.depth), 0.12f, Palette.PackGreen);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Shapes.Cylinder(root, "Leg", new Vector3(sx * (f.width * 0.5f - 0.15f), 0.42f, sz * (f.depth * 0.5f - 0.15f)), 0.12f, 0.84f, Palette.MetalDark);
            // Packaging materials: flat cardboard sheets underneath, tape dispenser, paper roll.
            Shapes.Box(root, "FlatBoxes", new Vector3(0f, 0.25f, 0f), new Vector3(f.width - 0.3f, 0.2f, f.depth - 0.5f), Palette.Cardboard);
            var tapeRoll = Shapes.Cylinder(root, "TapeRoll", new Vector3(0.35f, 1.0f, -0.85f), 0.22f, 0.08f, MaterialLibrary.Hex("#C9A227"));
            tapeRoll.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var paper = Shapes.Cylinder(root, "PaperRoll", new Vector3(-0.35f, 1.04f, 0.9f), 0.2f, 0.9f, Color.white);
            paper.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // The box being packed (with two flaps).
            s.box = new GameObject("Box").transform;
            s.box.SetParent(root, false);
            s.box.localPosition = new Vector3(0.05f, 0.96f, 0.15f);
            Shapes.Box(s.box, "Bottom", new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.36f, 0.5f), Palette.Cardboard);
            s.flapL = Pivot(s.box, "FlapL", new Vector3(-0.31f, 0.36f, 0f));
            Shapes.Box(s.flapL, "Flap", new Vector3(0.155f, 0f, 0f), new Vector3(0.31f, 0.02f, 0.5f), MaterialLibrary.Hex("#C98F55"));
            s.flapR = Pivot(s.box, "FlapR", new Vector3(0.31f, 0.36f, 0f));
            Shapes.Box(s.flapR, "Flap", new Vector3(-0.155f, 0f, 0f), new Vector3(0.31f, 0.02f, 0.5f), MaterialLibrary.Hex("#C98F55"));
            s.tape = Shapes.Box(s.box, "Tape", new Vector3(0f, 0.375f, 0f), new Vector3(0.08f, 0.01f, 0.52f), MaterialLibrary.Hex("#C9A227"));
            CharacterFactory.BrandT(s.box, new Vector3(0f, 0.2f, -0.255f), 0.16f);
            s.contents = new GameObject("Contents").transform;
            s.contents.SetParent(s.box, false);
            s.ResetBox();

            // Pallet with packed parcels next to the table.
            var pallet = Shapes.Box(root, "Pallet", new Vector3(0f, 0.07f, -1.75f), new Vector3(0.9f, 0.14f, 0.9f), Palette.Wood);
            for (int i = 0; i < 6; i++)
            {
                var b = Shapes.Box(root, "Parcel", new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.33f + (i / 2) * 0.38f, -1.75f), new Vector3(0.38f, 0.36f, 0.6f), Palette.Cardboard);
                b.SetActive(false);
                s.palletBoxes.Add(b);
            }
            s.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 0.5f, 0f), new Vector3(f.width, 1.0f, f.depth));
            return s;
        }

        static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            return p;
        }

        void ResetBox()
        {
            flapL.localRotation = Quaternion.Euler(0f, 0f, 110f);   // open: flaps stand up and lean out
            flapR.localRotation = Quaternion.Euler(0f, 0f, -110f);
            tape.SetActive(false);
            box.localScale = Vector3.one;
            box.localPosition = new Vector3(0.05f, 0.96f, 0.15f);
            for (int i = contents.childCount - 1; i >= 0; i--) Destroy(contents.GetChild(i).gameObject);
        }

        protected override void OnSetup() => RefreshPallet(Session.Progression.CompletedOrders);

        void RefreshPallet(int completed)
        {
            int shown = completed <= 0 ? 0 : ((completed - 1) % palletBoxes.Count) + 1;
            for (int i = 0; i < palletBoxes.Count; i++) palletBoxes[i].SetActive(i < shown);
        }

        public override bool TryGetAction(out string label)
        {
            label = Loc.T("act.pack");
            return Session.Orders.Step == OrderStep.Pack && !animating;
        }

        public override void Interact()
        {
            if (animating) return;
            var product = Session.Balance.FindProduct(Session.Orders.Order.ProductId);
            var logo = Session.Balance.FindLogo(Session.Orders.Order.LogoId);
            int quantity = Session.Orders.Order.Quantity;
            var r = Session.TryPack(out OrderPayout payout);
            if (r != ActionResult.Ok) { Report(r); return; }
            Punch();
            StartCoroutine(PackAnimation(payout, ProductLook.For(product), logo != null ? MaterialLibrary.Hex(logo.colorHex) : Palette.UiAccent, quantity));
        }

        IEnumerator PackAnimation(OrderPayout payout, ProductLook look, Color logo, int quantity)
        {
            animating = true;
            ResetBox();
            Game.Audio.Play(Sfx.Pickup);

            // 1. Garments drop into the box.
            int shown = Mathf.Min(quantity, 4);
            var items = new List<Transform>();
            for (int i = 0; i < shown; i++)
                items.Add(GarmentFactory.Folded(contents, "Packed", new Vector3(0f, 0.9f + i * 0.15f, 0f), look, logo, 0.5f, 0.4f).transform);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                for (int i = 0; i < items.Count; i++)
                    items[i].localPosition = new Vector3(0f, Mathf.Lerp(0.9f + i * 0.15f, 0.08f + i * look.Thickness, k * k), 0f);
                yield return null;
            }

            // 2. Flaps fold shut, 3. tape.
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                flapL.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(110f, 0f, k));
                flapR.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-110f, 0f, k));
                yield return null;
            }
            tape.SetActive(true);
            Game.Audio.Play(Sfx.Tick, 0.8f);
            yield return new WaitForSeconds(0.15f);

            // 4. Parcel hops onto the pallet.
            Vector3 from = box.localPosition;
            Vector3 to = new Vector3(0f, 0.4f, -1.75f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                box.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.6f;
                box.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
                yield return null;
            }
            RefreshPallet(Session.Progression.CompletedOrders);
            ResetBox();

            // 5. Celebrate + completion screen.
            Game.OnOrderPaid(payout, TopPosition);
            animating = false;
        }
    }
}
