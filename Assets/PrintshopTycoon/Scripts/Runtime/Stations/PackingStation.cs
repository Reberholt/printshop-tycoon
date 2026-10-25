using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    public class PackingStation : Interactable
    {
        readonly List<GameObject> packedBoxes = new List<GameObject>();
        Transform openBox;

        public override string DisplayName => Loc.T("station.packing");

        public static PackingStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("PackingTable").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var station = root.gameObject.AddComponent<PackingStation>();

            Shapes.RoundedBox(root, "Top", new Vector3(0f, 0.9f, 0f), new Vector3(f.width, 0.12f, f.depth), 0.12f, Palette.PackGreen);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Shapes.Cylinder(root, "Leg", new Vector3(sx * (f.width * 0.5f - 0.15f), 0.42f, sz * (f.depth * 0.5f - 0.15f)), 0.12f, 0.84f, Palette.MetalDark);
            station.openBox = Shapes.Box(root, "OpenBox", new Vector3(0.1f, 1.15f, 0.2f), new Vector3(0.6f, 0.4f, 0.5f), Palette.Cardboard).transform;
            var tape = Shapes.Cylinder(root, "Tape", new Vector3(0.2f, 1.0f, -0.7f), 0.22f, 0.08f, MaterialLibrary.Hex("#C9A227"));
            tape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 5; i++)
            {
                var b = Shapes.Box(root, "Packed", new Vector3(-0.15f, 1.12f + i * 0.28f, -0.75f + (i % 2) * 0.06f), new Vector3(0.5f, 0.26f, 0.4f), Palette.Cardboard);
                b.SetActive(false);
                station.packedBoxes.Add(b);
            }
            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 0.5f, 0f), new Vector3(f.width, 1.0f, f.depth));
            return station;
        }

        protected override void OnSetup()
        {
            int shown = Mathf.Min(packedBoxes.Count, Session.Progression.CompletedOrders);
            for (int i = 0; i < packedBoxes.Count; i++) packedBoxes[i].SetActive(i < shown);
        }

        public override bool TryGetAction(out string label)
        {
            label = Loc.T("act.pack");
            return Session.Orders.Step == OrderStep.Pack;
        }

        public override void Interact()
        {
            var r = Session.TryPack(out int paid);
            if (r != ActionResult.Ok) { Report(r); return; }
            Punch();
            StartCoroutine(PackAnimation());
            Game.OnOrderPaid(paid, TopPosition);
        }

        IEnumerator PackAnimation()
        {
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                openBox.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.35f * Mathf.PI) * 0.25f);
                yield return null;
            }
            openBox.localScale = Vector3.one;
            int index = (Session.Progression.CompletedOrders - 1) % packedBoxes.Count;
            if (index >= 0) packedBoxes[index].SetActive(true);
        }
    }
}
