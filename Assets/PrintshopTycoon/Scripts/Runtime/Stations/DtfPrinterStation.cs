using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// DTF printer: choose the logo, watch the carriage print the transfer film (it slides out
    /// with the logo on it), then take the transfers. The printer-speed upgrade shortens the
    /// print and adds turbo stripes and an extra status LED per level.
    /// </summary>
    public class DtfPrinterStation : Interactable
    {
        Transform carriage;
        Transform filmOut;
        Renderer filmLogo;
        Renderer screen;
        Renderer statusLed;
        Transform filmRoll;
        GameObject[] turbo;
        float carriagePhase;
        bool wasReady;

        public override string DisplayName => Loc.T("station.printer");

        public static DtfPrinterStation Build(Transform parent, Footprint f)
        {
            var root = new GameObject("DtfPrinter").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(f.centerX, 0f, f.centerZ);
            var s = root.gameObject.AddComponent<DtfPrinterStation>();
            float w = f.width;

            // Stand with a shelf of film boxes underneath.
            Shapes.RoundedBox(root, "Stand", new Vector3(0f, 0.4f, 0.1f), new Vector3(w - 0.2f, 0.8f, 0.8f), 0.12f, Palette.MetalDark);
            Shapes.Box(root, "FilmBox", new Vector3(-0.45f, 0.2f, -0.05f), new Vector3(0.5f, 0.25f, 0.5f), Palette.Cardboard);
            Shapes.Box(root, "FilmBox", new Vector3(0.25f, 0.2f, -0.05f), new Vector3(0.5f, 0.25f, 0.5f), Palette.Cardboard);

            // Body, transparent-looking lid window, rail.
            Shapes.RoundedBox(root, "Body", new Vector3(0f, 1.05f, 0.1f), new Vector3(w, 0.5f, 0.9f), 0.2f, Palette.PrinterBlue);
            Shapes.Box(root, "Window", new Vector3(0f, 1.31f, -0.02f), new Vector3(w - 0.5f, 0.03f, 0.42f), MaterialLibrary.Hex("#BFE3F2"));
            Shapes.Box(root, "Rail", new Vector3(0f, 1.36f, 0.24f), new Vector3(w - 0.3f, 0.05f, 0.06f), Palette.Metal);
            s.carriage = Shapes.Box(root, "Carriage", new Vector3(-0.6f, 1.42f, 0.18f), new Vector3(0.28f, 0.16f, 0.3f), Palette.UiAccent).transform;

            // Film roll at the back (spins while printing).
            var roll = Shapes.Cylinder(root, "FilmRoll", new Vector3(0f, 1.38f, 0.5f), 0.28f, w - 0.3f, Color.white);
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            s.filmRoll = roll.transform;

            // Output tray at the front with the printed film sliding out.
            Shapes.Box(root, "Tray", new Vector3(0f, 0.86f, -0.5f), new Vector3(w - 0.4f, 0.04f, 0.5f), Palette.MetalDark);
            var outPivot = new GameObject("FilmOut").transform;
            outPivot.SetParent(root, false);
            outPivot.localPosition = new Vector3(0f, 0.9f, -0.3f);
            var sheet = Shapes.Box(outPivot, "Sheet", new Vector3(0f, 0f, -0.2f), new Vector3(w - 0.55f, 0.015f, 0.4f), GarmentFactory.TransferFilm);
            s.filmLogo = Shapes.Box(sheet.transform, "Logo", new Vector3(0f, 0.8f, 0f), new Vector3(0.35f, 0.6f, 0.5f), Color.red).GetComponent<Renderer>();
            s.filmOut = outPivot;
            outPivot.localScale = new Vector3(1f, 1f, 0.01f);

            // Ink tanks: DTF prints C, M, Y, K + white.
            Color[] inks = { Color.cyan, Color.magenta, Color.yellow, MaterialLibrary.Hex("#222222"), Color.white };
            for (int i = 0; i < inks.Length; i++)
                Shapes.Box(root, "Ink", new Vector3(-0.55f + i * 0.2f, 1.42f, 0.42f), new Vector3(0.13f, 0.2f, 0.13f), inks[i]);

            // Control panel: screen shows the logo colour, LED shows the state.
            s.screen = Shapes.Box(root, "Screen", new Vector3(0.75f, 1.33f, -0.3f), new Vector3(0.32f, 0.2f, 0.04f), MaterialLibrary.Hex("#1B263B")).GetComponent<Renderer>();
            s.statusLed = Shapes.Sphere(root, "Led", new Vector3(0.95f, 1.33f, -0.33f), 0.08f, Palette.Valid).GetComponent<Renderer>();
            CharacterFactory.BrandT(root, new Vector3(-0.75f, 1.08f, -0.36f), 0.16f);

            // Upgrade visuals: gold turbo stripes on the body, one per level.
            s.turbo = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                s.turbo[i] = Shapes.Box(root, "Turbo", new Vector3(-0.2f + i * 0.2f, 0.95f, -0.36f), new Vector3(0.12f, 0.06f, 0.02f), Palette.UiCoin);
                s.turbo[i].SetActive(false);
            }

            s.Body = Shapes.AddCollider(root.gameObject, new Vector3(0f, 0.8f, 0f), new Vector3(w, 1.6f, f.depth));
            return s;
        }

        protected override void OnSetup()
        {
            Session.Upgrades.Upgraded += OnUpgraded;
            OnUpgraded(null, 0);
        }

        void OnDestroy()
        {
            if (Game != null) Session.Upgrades.Upgraded -= OnUpgraded;
        }

        void OnUpgraded(string id, int level)
        {
            int lvl = Session.Upgrades.Level(UpgradeDefinition.PrinterSpeed);
            for (int i = 0; i < turbo.Length; i++) turbo[i].SetActive(i < lvl);
            if (id == UpgradeDefinition.PrinterSpeed) { Punch(); Game.Fx.Sparkle(TopPosition, Palette.UiCoin, 10); }
        }

        void Update()
        {
            if (Game == null) return;
            var o = Session.Orders;
            var logo = Session.Balance.FindLogo(o.Order.LogoId);
            Color logoColor = logo != null ? MaterialLibrary.Hex(logo.colorHex) : Color.white;
            bool printing = o.IsPrinting;
            bool ready = o.TransfersReady;

            // Carriage sweeps while printing; film roll turns; film slides out with progress.
            if (printing)
            {
                carriagePhase += Time.deltaTime * 6f;
                carriage.localPosition = new Vector3(Mathf.Sin(carriagePhase) * 0.6f, 1.42f, 0.18f);
                filmRoll.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
            }
            else carriage.localPosition = Vector3.Lerp(carriage.localPosition, new Vector3(-0.6f, 1.42f, 0.18f), Time.deltaTime * 6f);

            float outAmount = printing ? o.PrintProgress : (ready ? 1f : 0f);
            filmOut.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, outAmount));
            filmOut.gameObject.SetActive(outAmount > 0.02f);
            filmLogo.sharedMaterial = MaterialLibrary.Get(logoColor);

            screen.sharedMaterial = MaterialLibrary.Get(printing || ready ? logoColor : MaterialLibrary.Hex("#1B263B"));
            bool blink = Mathf.Repeat(Time.time * 4f, 1f) > 0.5f;
            statusLed.sharedMaterial = MaterialLibrary.Get(printing ? (blink ? Palette.BoardYellow : Palette.UiAccent) : Palette.Valid);

            if (ready && !wasReady)
            {
                Game.Audio.Play(Sfx.Good, 0.6f);
                Game.Fx.Sparkle(filmOut.position + Vector3.up * 0.2f, logoColor, 8);
                Punch();
            }
            wasReady = ready;
        }

        public override bool TryGetProgress(out float progress01, out bool ready)
        {
            var o = Session.Orders;
            progress01 = o.PrintProgress;
            ready = o.TransfersReady;
            return o.Printing;
        }

        public override bool TryGetAction(out string label)
        {
            var o = Session.Orders;
            if (o.Step != OrderStep.PrintTransfers) { label = Loc.T("act.print"); return false; }
            if (o.TransfersReady) { label = Loc.T("act.take_transfers"); return true; }
            if (o.IsPrinting) { label = Loc.T("act.printing", Mathf.RoundToInt(o.PrintProgress * 100f)); return false; }
            label = Loc.T("act.print");
            return true;
        }

        public override void Interact()
        {
            var o = Session.Orders;
            if (o.TransfersReady)
            {
                var r = Session.TryCollectTransfers();
                if (r == ActionResult.Ok) { Punch(); Game.Audio.Play(Sfx.Pickup); }
                else Report(r);
                return;
            }
            if (o.Step != OrderStep.PrintTransfers || o.Printing) { Report(ActionResult.WrongStep); return; }
            Game.Audio.Play(Sfx.Click);
            Game.UI.ShowLogoSelect(OnLogoChosen);
        }

        void OnLogoChosen(string logoId)
        {
            var r = Session.Orders.SelectLogo(logoId);
            if (r != ActionResult.Ok) { Report(r); return; }
            Punch();
            Game.Audio.Play(Sfx.Machine);
        }

        public override string StatusText
        {
            get
            {
                var o = Session.Orders;
                if (o.IsPrinting) return Loc.T("act.printing", Mathf.RoundToInt(o.PrintProgress * 100f));
                if (o.TransfersReady) return Loc.T("info.ready");
                return Loc.T("info.free") + "\n" + Loc.T("info.print_time", Session.Upgrades.PrintSeconds.ToString("0.0"));
            }
        }
    }
}
