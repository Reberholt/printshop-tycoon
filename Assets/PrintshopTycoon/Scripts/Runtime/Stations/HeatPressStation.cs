using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// A heat press (starter or purchased). Hue uses it through the timing minigame, then the
    /// lid stays closed (glowing, steaming) for the press time; workers use it automatically.
    /// Occupancy lives in GameSession so both can never press at once. Press upgrades shorten
    /// the press time and add chrome rails / a pressure gauge.
    /// </summary>
    public class HeatPressStation : Interactable
    {
        public const float BodyOffsetZ = 0.4f;   // machine sits at the back of its footprint
        public static readonly Vector3 ApproachLocal = new Vector3(0f, 0f, -0.75f);

        Transform lidPivot;
        Transform gaugeNeedle;
        GameObject garmentsOnPlaten;
        Renderer statusLamp;
        Renderer heatGlow;
        Material heatGlowCool;      // the plate's normal material (imported model or primitive)
        Quaternion lidRest = Quaternion.identity;
        float lidOpenAngle = LidOpenAngle;
        GameObject[] rails;
        GameObject gauge;
        Color shownLampColor = Color.clear;
        string shownGarmentKey;
        const float LidOpenAngle = 32f;   // positive X lifts the front of the lid
        const float ModelLidOpenAngle = 50f;
        public const string ModelResource = "PrintshopTycoon/Models/HeatPress";
        float lidAngle = LidOpenAngle;
        float steamTimer;
        bool forcedClosed;
        bool wasDone;

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

            // Rounded 3D model made in Blender (Tools/Blender/heat_press.py); primitives are the fallback.
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab != null && BuildFromModel(m, prefab, station)) return;

            Shapes.RoundedBox(m, "Cabinet", new Vector3(0f, 0.42f, 0f), new Vector3(1.2f, 0.84f, 1.1f), 0.18f, Palette.MetalDark);
            Shapes.Box(m, "Drawer", new Vector3(0f, 0.45f, -0.56f), new Vector3(0.8f, 0.3f, 0.03f), Palette.Metal);
            Shapes.RoundedBox(m, "Platen", new Vector3(0f, 0.92f, -0.05f), new Vector3(1.0f, 0.12f, 0.8f), 0.12f, Palette.Metal);
            Shapes.Box(m, "Pad", new Vector3(0f, 0.985f, -0.05f), new Vector3(0.9f, 0.02f, 0.7f), MaterialLibrary.Hex("#4A4E57"));
            Shapes.Box(m, "Column", new Vector3(0f, 1.25f, 0.45f), new Vector3(0.24f, 0.8f, 0.24f), Palette.MetalDark);

            var pivot = new GameObject("LidPivot").transform;
            pivot.SetParent(m, false);
            pivot.localPosition = new Vector3(0f, 1.2f, 0.45f);
            Shapes.RoundedBox(pivot, "Lid", new Vector3(0f, -0.08f, -0.48f), new Vector3(1.05f, 0.18f, 0.9f), 0.14f, Palette.PressRed);
            var glow = Shapes.Box(pivot, "HeatPlate", new Vector3(0f, -0.18f, -0.48f), new Vector3(0.92f, 0.03f, 0.78f), MaterialLibrary.Hex("#5E6B7A"));
            var handle = Shapes.Cylinder(pivot, "Handle", new Vector3(0f, 0.02f, -0.98f), 0.1f, 0.7f, Palette.UiDark);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Branding.Mark(pivot, new Vector3(0f, 0.03f, -0.48f), 0.1f);

            // Temperature display + status lamp on the cabinet front.
            Shapes.Box(m, "Display", new Vector3(-0.32f, 0.72f, -0.56f), new Vector3(0.28f, 0.14f, 0.03f), MaterialLibrary.Hex("#1B263B"));
            var lamp = Shapes.Sphere(m, "Lamp", new Vector3(0.38f, 0.72f, -0.57f), 0.14f, Palette.Valid);

            var garments = new GameObject("GarmentsOnPlaten");
            garments.transform.SetParent(m, false);
            garments.transform.localPosition = new Vector3(0f, 1.0f, -0.05f);
            garments.SetActive(false);

            // Upgrade visuals: chrome rails (press speed) and a pressure gauge (press timing).
            var rails = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                rails[i] = Shapes.Box(m, "Rail", new Vector3(-0.62f, 0.25f + i * 0.2f, 0f), new Vector3(0.03f, 0.06f, 1.0f), MaterialLibrary.Hex("#E6EBF0"));
                rails[i].SetActive(false);
            }
            var gauge = new GameObject("Gauge");
            gauge.transform.SetParent(m, false);
            gauge.transform.localPosition = new Vector3(0.24f, 1.45f, 0.3f);
            Shapes.Cylinder(gauge.transform, "Dial", Vector3.zero, 0.26f, 0.05f, Color.white).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var needle = Shapes.Box(gauge.transform, "Needle", new Vector3(0f, 0.05f, -0.035f), new Vector3(0.02f, 0.11f, 0.01f), Palette.UiRed).transform;
            gauge.SetActive(false);

            if (station != null)
            {
                station.lidPivot = pivot;
                station.garmentsOnPlaten = garments;
                station.statusLamp = lamp.GetComponent<Renderer>();
                station.heatGlow = glow.GetComponent<Renderer>();
                station.rails = rails;
                station.gauge = gauge;
                station.gaugeNeedle = needle;
                pivot.localRotation = Quaternion.Euler(station.lidAngle, 0f, 0f);
            }
            else pivot.localRotation = Quaternion.Euler(LidOpenAngle, 0f, 0f);
            if (station != null) station.heatGlowCool = glow.GetComponent<Renderer>().sharedMaterial;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Uses the imported FBX; returns false (and cleans up) if expected parts are missing.</summary>
        static bool BuildFromModel(Transform m, GameObject prefab, HeatPressStation station)
        {
            var model = Object.Instantiate(prefab, m, false);
            model.name = "Model";
            var pivot = FindDeep(model.transform, "LidPivot");
            var pad = FindDeep(model.transform, "HeatPad");
            var lamp = FindDeep(model.transform, "StatusLamp");
            if (pivot == null || pad == null || lamp == null)
            {
                Debug.LogWarning("[PrintshopTycoon] HeatPress model is missing LidPivot/HeatPad/StatusLamp - using primitives.");
                Object.Destroy(model);
                return false;
            }

            // Guard against unit mistakes in the export (model should be ~1.5 m tall).
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y > 20f) model.transform.localScale *= 0.01f;

            foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }

            var garments = new GameObject("GarmentsOnPlaten");
            garments.transform.SetParent(m, false);
            garments.transform.localPosition = new Vector3(0f, 1.075f, -0.08f);
            garments.SetActive(false);

            // Upgrade visuals: chrome rails on the table side, gauge on the control box.
            var rails = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                rails[i] = Shapes.Box(m, "Rail", new Vector3(-0.68f, 0.3f + i * 0.18f, 0f), new Vector3(0.03f, 0.05f, 0.9f), MaterialLibrary.Hex("#E6EBF0"));
                rails[i].SetActive(false);
            }
            var gauge = new GameObject("Gauge");
            gauge.transform.SetParent(m, false);
            gauge.transform.localPosition = new Vector3(0.36f, 1.33f, 0.3f);
            Shapes.Cylinder(gauge.transform, "Dial", Vector3.zero, 0.16f, 0.03f, Color.white).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var needle = Shapes.Box(gauge.transform, "Needle", new Vector3(0f, 0.03f, -0.02f), new Vector3(0.015f, 0.07f, 0.008f), Palette.UiRed).transform;
            gauge.SetActive(false);

            var rest = pivot.localRotation;
            pivot.localRotation = Quaternion.AngleAxis(ModelLidOpenAngle, Vector3.right) * rest;
            if (station != null)
            {
                station.lidPivot = pivot;
                station.lidRest = rest;
                station.lidOpenAngle = ModelLidOpenAngle;
                station.lidAngle = ModelLidOpenAngle;
                station.garmentsOnPlaten = garments;
                station.statusLamp = lamp.GetComponent<Renderer>();
                station.heatGlow = pad.GetComponent<Renderer>();
                station.heatGlowCool = station.heatGlow.sharedMaterial;
                station.rails = rails;
                station.gauge = gauge;
                station.gaugeNeedle = needle;
            }
            return true;
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
            int speed = Session.Upgrades.Level(UpgradeDefinition.PressSpeed);
            for (int i = 0; i < rails.Length; i++) rails[i].SetActive(i < speed);
            gauge.SetActive(Session.Upgrades.Level(UpgradeDefinition.PressTiming) > 0);
            if (id == UpgradeDefinition.PressSpeed || id == UpgradeDefinition.PressTiming)
            {
                Punch();
                Game.Fx.Sparkle(TopPosition, Palette.UiCoin, 10);
            }
        }

        /// <summary>Closes the lid while Hue plays the minigame.</summary>
        public void SetForcedClosed(bool closed) => forcedClosed = closed;

        string Occupant => Session.GetOccupant(MachineId);
        bool OrderGarmentsHere => Session.Orders.Step == OrderStep.CollectFinished && Session.Orders.PressStationId == MachineId;
        bool PlayerPressing => OrderGarmentsHere && !Session.Orders.PressDone;

        WorkerTaskMachine WorkerHere
        {
            get
            {
                var occ = Occupant;
                return occ != null && occ != GameSession.PlayerOccupant ? Session.Workers.FirstLike(occ) : null;
            }
        }

        void Update()
        {
            if (Game == null) return;
            var worker = WorkerHere;
            bool workerPressing = worker != null && worker.State == WorkerState.Working;
            bool hot = PlayerPressing || workerPressing;
            bool closed = forcedClosed || hot;

            float target = closed ? 0f : lidOpenAngle;
            lidAngle = Mathf.MoveTowards(lidAngle, target, 160f * Time.deltaTime);
            // Rotate around the parent's X axis on top of the rest pose (works for primitives and the FBX).
            lidPivot.localRotation = Quaternion.AngleAxis(lidAngle, Vector3.right) * lidRest;

            // Heat glow + steam while pressing.
            // Two discrete colours only: MaterialLibrary caches one material per colour.
            heatGlow.sharedMaterial = hot ? MaterialLibrary.Get(Mathf.Repeat(Time.time * 2f, 1f) > 0.5f ? Palette.UiAccent : Palette.PressRed) : heatGlowCool;
            if (hot)
            {
                steamTimer -= Time.deltaTime;
                if (steamTimer <= 0f)
                {
                    steamTimer = 0.45f;
                    Game.Fx.Steam(transform.position + new Vector3(0f, 1.25f, BodyOffsetZ - 0.3f), 2);
                }
            }
            if (gauge.activeSelf)
                gaugeNeedle.parent.localRotation = Quaternion.Euler(0f, 0f, hot ? -40f + Mathf.Sin(Time.time * 8f) * 6f : 35f);

            // Garments on the platen: the order's product with its logo, or the worker's batch.
            string key = null;
            if (OrderGarmentsHere) key = "order:" + Session.Orders.Order.ProductId + ":" + Session.Orders.Order.LogoId;
            else if (workerPressing) key = "worker";
            if (key != shownGarmentKey) RebuildGarments(key);

            bool done = OrderGarmentsHere && Session.Orders.PressDone;
            if (done && !wasDone)
            {
                Game.Audio.Play(Sfx.Good, 0.7f);
                Game.Fx.Steam(transform.position + Vector3.up * 1.3f, 6);
                Punch();
            }
            wasDone = done;

            Color lampColor = Occupant == null ? Palette.Valid : (done ? Palette.Valid : (hot ? Palette.UiAccent : Palette.BoardYellow));
            if (lampColor != shownLampColor)
            {
                shownLampColor = lampColor;
                statusLamp.sharedMaterial = MaterialLibrary.Get(lampColor);
            }
        }

        void RebuildGarments(string key)
        {
            shownGarmentKey = key;
            for (int i = garmentsOnPlaten.transform.childCount - 1; i >= 0; i--) Destroy(garmentsOnPlaten.transform.GetChild(i).gameObject);
            garmentsOnPlaten.SetActive(key != null);
            if (key == null) return;
            ProductLook look;
            Color logoColor;
            if (key == "worker")
            {
                look = ProductLook.For(Session.Balance.FindProduct(ProductDefinition.TShirt));
                logoColor = Palette.UiAccent;
            }
            else
            {
                look = ProductLook.For(Session.Balance.FindProduct(Session.Orders.Order.ProductId));
                var logo = Session.Balance.FindLogo(Session.Orders.Order.LogoId);
                logoColor = logo != null ? MaterialLibrary.Hex(logo.colorHex) : Palette.UiAccent;
            }
            GarmentFactory.Folded(garmentsOnPlaten.transform, "Garment", Vector3.zero, look, logoColor, 0.75f, 0.6f);
        }

        public override bool TryGetProgress(out float progress01, out bool ready)
        {
            progress01 = 0f; ready = false;
            if (OrderGarmentsHere)
            {
                progress01 = Session.Orders.PressProgress;
                ready = Session.Orders.PressDone;
                return true;
            }
            var w = WorkerHere;
            if (w != null && w.State == WorkerState.Working) { progress01 = w.TaskProgress; return true; }
            return false;
        }

        public override bool TryGetAction(out string label)
        {
            if (OrderGarmentsHere)
            {
                if (!Session.Orders.PressDone)
                {
                    label = Loc.T("act.pressing", Mathf.RoundToInt(Session.Orders.PressProgress * 100f));
                    return false;
                }
                label = Loc.T("act.collect_product");
                return true;
            }
            label = Loc.T("act.press");
            if (Session.Orders.Step != OrderStep.PressShirts) return false;
            var occ = Occupant;
            return occ == null || occ == GameSession.PlayerOccupant;
        }

        public override void Interact()
        {
            if (OrderGarmentsHere)
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
                    : Loc.T("worker." + (WorkerHere?.State == WorkerState.Working ? "working" : "moving"));
                var assigned = Session.FindWorkerAssignedTo(MachineId);
                if (assigned != null) s += "\n" + Loc.T("info.worker", GameManager.WorkerNumber(assigned.Id));
                s += "\n" + Loc.T("info.press_time", Session.Upgrades.PlayerPressSeconds.ToString("0.0"));
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
