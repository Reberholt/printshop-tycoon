using System.Collections;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    public class DtfPrinterStation : Interactable
    {
        Transform head;
        Transform film;
        Coroutine printing;

        public override string DisplayName => Loc.T("station.printer");

        public static DtfPrinterStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("DtfPrinter").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var station = root.gameObject.AddComponent<DtfPrinterStation>();

            Shapes.RoundedBox(root, "Stand", new Vector3(0f, 0.4f, 0.1f), new Vector3(f.width - 0.2f, 0.8f, 0.8f), 0.12f, Palette.MetalDark);
            Shapes.RoundedBox(root, "Body", new Vector3(0f, 1.05f, 0.1f), new Vector3(f.width, 0.5f, 0.9f), 0.2f, Palette.PrinterBlue);
            var roll = Shapes.Cylinder(root, "FilmRoll", new Vector3(0f, 1.42f, 0.35f), 0.28f, f.width - 0.3f, Color.white);
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            station.film = Shapes.Box(root, "Film", new Vector3(0f, 1.12f, -0.45f), new Vector3(f.width - 0.5f, 0.02f, 0.4f), MaterialLibrary.Hex("#E8F4FF")).transform;
            station.head = Shapes.Box(root, "PrintHead", new Vector3(-0.6f, 1.36f, 0f), new Vector3(0.28f, 0.2f, 0.3f), Palette.UiAccent).transform;
            Shapes.Box(root, "Screen", new Vector3(0.75f, 1.32f, -0.3f), new Vector3(0.3f, 0.18f, 0.04f), MaterialLibrary.Hex("#1B263B"));
            foreach (var x in new[] { -0.3f, 0f, 0.3f })
                Shapes.Sphere(root, "Ink", new Vector3(x, 1.36f, 0.25f), 0.12f, x < 0f ? Color.cyan : (x > 0f ? Color.magenta : Color.yellow));
            station.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 0.8f, 0f), new Vector3(f.width, 1.6f, f.depth));
            return station;
        }

        public override bool TryGetAction(out string label)
        {
            label = Loc.T("act.print");
            return Session.Orders.Step == OrderStep.PrintTransfers;
        }

        public override void Interact()
        {
            if (Session.Orders.Step != OrderStep.PrintTransfers) { Report(ActionResult.WrongStep); return; }
            Game.Audio.Play(Sfx.Click);
            Game.UI.ShowLogoSelect(OnLogoChosen);
        }

        void OnLogoChosen(string logoId)
        {
            var r = Session.Orders.SelectLogo(logoId);
            if (r != ActionResult.Ok) { Report(r); return; }
            Punch();
            Game.Audio.Play(Sfx.Machine);
            if (printing != null) StopCoroutine(printing);
            printing = StartCoroutine(PrintAnimation(Session.Balance.FindLogo(logoId)));
        }

        IEnumerator PrintAnimation(LogoDefinition logo)
        {
            var filmRenderer = film.GetComponent<Renderer>();
            filmRenderer.sharedMaterial = MaterialLibrary.Get(logo != null ? MaterialLibrary.Hex(logo.colorHex) : Color.white);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                head.localPosition = new Vector3(Mathf.PingPong(t * 3f, 1.2f) - 0.6f, 1.36f, 0f);
                yield return null;
            }
            head.localPosition = new Vector3(-0.6f, 1.36f, 0f);
            filmRenderer.sharedMaterial = MaterialLibrary.Get(MaterialLibrary.Hex("#E8F4FF"));
            printing = null;
        }
    }
}
