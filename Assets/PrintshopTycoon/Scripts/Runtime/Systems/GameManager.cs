using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Composition root: owns the GameSession (rules + state), builds the world and wires the
    /// systems together. Gameplay rules live in Core; this class only connects them to Unity.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        const float AutosaveSeconds = 30f;

        SaveManager saveManager;
        WorkshopBuilder.Result world;
        Transform dynamicRoot;
        readonly List<HeatPressStation> presses = new List<HeatPressStation>();
        readonly List<WorkerView> workerViews = new List<WorkerView>();
        float autosaveTimer;
        bool resetting;
        bool introShown;
        bool paused;
        OrderStep lastStep;

        public GameSession Session { get; private set; }
        public UIManager UI { get; private set; }
        public AudioManager Audio { get; private set; }
        public FxManager Fx { get; private set; }
        public CameraModeController Cameras { get; private set; }
        public InteractionSystem Interaction { get; private set; }
        public FactoryBuildManager Build { get; private set; }
        public PlayerController Player { get; private set; }

        public OrderBoardStation Board => world.Board;
        public DtfPrinterStation Printer => world.Printer;
        public ShelfStation Shelf => world.Shelf;
        public PackingStation Packing => world.Packing;
        public IReadOnlyList<HeatPressStation> Presses => presses;
        public IReadOnlyList<WorkerView> WorkerViews => workerViews;
        public Interactable NextTarget { get; private set; }

        // ------------------------------------------------------------------ Startup

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var balance = GameBalanceConfig.LoadOrDefault();
            saveManager = new SaveManager();
            var data = saveManager.Load();
            Session = GameSession.FromSaveData(balance, data);
            Loc.Language = Session.Language;
            lastStep = Session.Orders.Step;

            BuildWorld();
        }

        void BuildWorld()
        {
            DisableForeignDirectionalLights();

            Audio = new GameObject("Audio").AddComponent<AudioManager>();
            Audio.transform.SetParent(transform, false);
            Audio.Muted = !Session.SoundOn;
            Fx = gameObject.AddComponent<FxManager>();
            Fx.Init();

            world = WorkshopBuilder.Build();
            dynamicRoot = new GameObject("Dynamic").transform;

            world.Board.Setup(this, new Vector3(0f, 0f, -1.1f));
            world.Shelf.Setup(this, new Vector3(0f, 0f, -1.1f));
            world.Printer.Setup(this, new Vector3(0f, 0f, -1.2f));
            world.Packing.Setup(this, new Vector3(1.2f, 0f, 0f));

            // Hue
            var hue = CharacterFactory.CreateHuePlaceholder(dynamicRoot);
            hue.gameObject.AddComponent<CharacterController>();
            Player = hue.gameObject.AddComponent<PlayerController>();
            Player.Teleport(new Vector3(0f, 0f, -5.5f), 0f);

            Cameras = gameObject.AddComponent<CameraModeController>();
            Cameras.Init(world.Camera, hue);

            Interaction = gameObject.AddComponent<InteractionSystem>();
            Interaction.Init(this, hue);
            Interaction.Register(world.Board);
            Interaction.Register(world.Shelf);
            Interaction.Register(world.Printer);
            Interaction.Register(world.Packing);

            Build = gameObject.AddComponent<FactoryBuildManager>();
            Build.Init(this);

            foreach (var m in Session.Machines) SpawnMachine(m);

            CreateUI();

            foreach (var w in Session.Workers) SpawnWorker(w, IdleSpawnPosition(w));

            Session.MachineAdded += OnMachineAdded;
            Session.WorkerHired += w => SpawnWorker(w, new Vector3(0f, 0f, -7.2f));
            Session.AutoJobCompleted += OnAutoJobCompleted;
            Session.Orders.Changed += OnOrderChanged;

            Cameras.SnapToCurrent();
            UI.ShowMainMenu();
        }

        void CreateUI()
        {
            UI = new GameObject("UIManager").AddComponent<UIManager>();
            UI.transform.SetParent(transform, false);
            UI.Build(this);
            Player.Init(world.Camera.transform, UI.Joystick, Audio);
            var surface = UI.TouchSurface;
            surface.Panned += d => Cameras.Pan(d);
            surface.Zoomed += f => Cameras.Zoom(f);
            surface.Tapped += OnFactoryTap;
            surface.PlacementDragged += p => Build.DragTo(p);
            surface.IsPlacing = () => Build.IsPlacing;
            UI.SetMode(Cameras.Mode);
        }

        static void DisableForeignDirectionalLights()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ Frame

        void Update()
        {
            Session.Tick(Time.deltaTime);
            UpdatePlayerCarry();

            NextTarget = ComputeNextTarget();
            bool showMarker = Cameras.Mode == CameraMode.Character && !UI.ModalOpen && NextTarget != null;
            Fx.SetMarker(showMarker ? NextTarget.TopPosition : (Vector3?)null);

            if (Cameras.Mode == CameraMode.Character && !UI.ModalOpen && PlayerController.InteractKeyPressed())
                OnInteractPressed();
            if (BackPressed()) OnBack();

            autosaveTimer += Time.unscaledDeltaTime;
            if (autosaveTimer >= AutosaveSeconds) SaveNow();
        }

        void UpdatePlayerCarry()
        {
            var o = Session.Orders;
            var logo = Session.Balance.FindLogo(o.Order.LogoId);
            Color logoColor = logo != null ? MaterialLibrary.Hex(logo.colorHex) : Palette.UiAccent;
            switch (o.Carry)
            {
                case CarryKind.Blanks: Player.Carry.Show(o.CarriedShirts, Palette.Shirt, null); break;
                case CarryKind.BlanksWithTransfers: Player.Carry.Show(o.CarriedShirts, MaterialLibrary.Hex("#E8F4FF"), logoColor); break;
                case CarryKind.Finished: Player.Carry.Show(o.CarriedShirts, Palette.Shirt, logoColor); break;
                default: Player.Carry.Show(0, Palette.Shirt, null); break;
            }
        }

        Interactable ComputeNextTarget()
        {
            var o = Session.Orders;
            switch (o.Step)
            {
                case OrderStep.Offered: return world.Board;
                case OrderStep.CollectBlanks: return world.Shelf;
                case OrderStep.PrintTransfers: return world.Printer;
                case OrderStep.CollectFinished: return FindPress(o.PressStationId);
                case OrderStep.Pack: return world.Packing;
                case OrderStep.PressShirts:
                    HeatPressStation best = null;
                    float bestD = float.MaxValue;
                    foreach (var p in presses)
                    {
                        var occ = Session.GetOccupant(p.MachineId);
                        if (occ != null && occ != GameSession.PlayerOccupant) continue;
                        // Prefer presses no worker is assigned to, so Hue and staff don't collide.
                        float d = Vector3.Distance(Player.transform.position, p.transform.position)
                                  + (Session.FindWorkerAssignedTo(p.MachineId) != null ? 8f : 0f);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                    return best;
            }
            return null;
        }

        static bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        void OnBack()
        {
            if (UI.MainMenuOpen) return; // back on the main menu must not start the game
            if (Build.IsPlacing) { CancelPlacement(); return; }
            if (UI.ModalOpen) { UI.CloseModal(); return; }
            if (Cameras.Mode == CameraMode.Factory) { SetMode(CameraMode.Character); return; }
            UI.ShowPause();
        }

        // ------------------------------------------------------------------ Player actions

        public void OnInteractPressed()
        {
            if (UI.ModalOpen || Cameras.Mode != CameraMode.Character) return;
            if (!Interaction.TryInteract()) Audio.Play(Sfx.Error, 0.5f);
        }

        public void SetMode(CameraMode mode)
        {
            if (mode == CameraMode.Character && Build.IsPlacing) Build.CancelPlacement();
            Cameras.SetMode(mode);
            UI.SetMode(mode);
            Player.InputEnabled = mode == CameraMode.Character && !UI.ModalOpen;
            Audio.Play(Sfx.Click, 0.6f);
        }

        public void OnModalChanged(bool open)
        {
            if (Player != null) Player.InputEnabled = !open && Cameras.Mode == CameraMode.Character;
            if (UI != null && UI.Joystick != null) UI.Joystick.Release();
            if (!open && !introShown)
            {
                introShown = true;
                if (Session.TutorialStep == 0) UI.ShowSilke(Loc.T("silke." + (int)Session.Orders.Step));
            }
        }

        public void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = paused ? 0f : 1f;
        }

        void OnFactoryTap(Vector2 screen)
        {
            if (Build.IsPlacing) { Build.DragTo(screen); return; }
            var ray = Cameras.Camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 100f, ~0, QueryTriggerInteraction.Collide))
            {
                var worker = hit.collider.GetComponentInParent<WorkerView>();
                if (worker != null)
                {
                    UI.ShowSelection("<b>" + Loc.T("staff.worker", WorkerNumber(worker.Machine.Id)) + "</b>\n" + worker.StatusText);
                    Audio.Play(Sfx.Click, 0.6f);
                    return;
                }
                var station = hit.collider.GetComponentInParent<Interactable>();
                if (station != null)
                {
                    string name = station.DisplayName;
                    if (station is HeatPressStation hp) name += " " + PressNumber(hp.MachineId);
                    UI.ShowSelection("<b>" + name + "</b>\n" + (station.StatusText ?? ""));
                    station.Punch();
                    Audio.Play(Sfx.Click, 0.6f);
                    return;
                }
            }
            UI.ShowSelection(null);
        }

        // ------------------------------------------------------------------ Building

        public void BeginPlacement(MachineDefinition def)
        {
            if (Cameras.Mode != CameraMode.Factory) SetMode(CameraMode.Factory);
            UI.ShowSelection(null);
            Build.BeginPlacement(def);
        }

        public void ConfirmPlacement()
        {
            var r = Build.ConfirmPlacement();
            if (r == ActionResult.NotEnoughCoins) { UI.Toast(Loc.T("toast.no_coins"), true); Audio.Play(Sfx.Error); }
            else if (r == ActionResult.InvalidPlacement) { UI.Toast(Loc.T("toast.invalid_place"), true); Audio.Play(Sfx.Error); }
        }

        public void CancelPlacement()
        {
            Build.CancelPlacement();
            UI.SetMode(Cameras.Mode);
            Audio.Play(Sfx.Click, 0.6f);
        }

        void OnMachineAdded(MachineInstance m)
        {
            var station = SpawnMachine(m);
            UI.SetMode(Cameras.Mode);
            var def = Session.Balance.FindMachine(m.typeId);
            UI.Toast(Loc.T("toast.bought", def != null ? Loc.T(def.nameKey) : m.typeId));
            Audio.Play(Sfx.Purchase);
            Fx.CoinBurst(station.TopPosition, 6);
            station.Punch();
            if (Session.TutorialStep <= 1)
            {
                Session.TutorialStep = 2;
                UI.ShowSilke(Loc.T("silke.hire"));
            }
            SaveNow();
        }

        HeatPressStation SpawnMachine(MachineInstance m)
        {
            var def = Session.Balance.FindMachine(m.typeId);
            var station = HeatPressStation.Build(world.Root, m, def);
            station.Setup(this, HeatPressStation.ApproachLocal);
            presses.Add(station);
            Interaction.Register(station);
            return station;
        }

        public HeatPressStation FindPress(string machineId)
        {
            if (machineId == null) return null;
            foreach (var p in presses) if (p.MachineId == machineId) return p;
            return null;
        }

        // ------------------------------------------------------------------ Workers

        public void HireWorker()
        {
            var r = Session.TryHireWorker(out var w);
            if (r != ActionResult.Ok)
            {
                UI.Toast(Loc.T(r == ActionResult.LimitReached ? "toast.limit" : "toast.no_coins"), true);
                Audio.Play(Sfx.Error);
                return;
            }
            // Auto-assign to the newest press without staff, so Hue keeps the starter press.
            for (int i = presses.Count - 1; i >= 0; i--)
            {
                if (Session.FindWorkerAssignedTo(presses[i].MachineId) != null) continue;
                Session.AssignWorker(w, presses[i].MachineId);
                break;
            }
            UI.Toast(Loc.T("toast.hired"));
            Audio.Play(Sfx.Purchase);
            if (Session.TutorialStep <= 2)
            {
                Session.TutorialStep = 3;
                UI.ShowSilke(Loc.T("silke.auto"));
            }
            SaveNow();
        }

        void SpawnWorker(WorkerTaskMachine w, Vector3 position)
        {
            var view = WorkerView.Create(this, dynamicRoot, w, position);
            var col = view.gameObject.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.4f;
            workerViews.Add(view);
        }

        Vector3 IdleSpawnPosition(WorkerTaskMachine w)
        {
            var press = FindPress(w.AssignedMachineId);
            return press != null ? press.ApproachPosition + new Vector3(-0.9f, 0f, -0.3f) : new Vector3(-2f, 0f, -6f);
        }

        public WorkerView FindWorkerView(WorkerTaskMachine w)
        {
            foreach (var v in workerViews) if (v.Machine == w) return v;
            return null;
        }

        void OnAutoJobCompleted(string jobId, int coins)
        {
            Audio.Play(Sfx.Coin, 0.5f);
            Vector3 pos = world.Packing.TopPosition;
            Fx.CoinBurst(pos, 4);
            UI.FloatingCoins(pos + Vector3.up * 0.5f, coins);
        }

        // ------------------------------------------------------------------ Orders

        void OnOrderChanged()
        {
            var step = Session.Orders.Step;
            if (step == lastStep) return;
            lastStep = step;
            if (Session.TutorialStep == 0 && step != OrderStep.Offered)
                UI.ShowSilke(Loc.T("silke." + (int)step));
        }

        /// <summary>Called by the packing table after a successful (single) payment.</summary>
        public void OnOrderPaid(int paid, Vector3 where)
        {
            Audio.Play(Sfx.OrderComplete);
            Audio.Play(Sfx.Coin, 0.8f);
            Fx.CoinBurst(where, 14);
            UI.FloatingCoins(where + Vector3.up * 0.6f, paid);
            if (Session.TutorialStep == 0)
            {
                Session.TutorialStep = 1;
                UI.ShowSilke(Loc.T("silke.done"), 9f);
            }
            SaveNow();
        }

        // ------------------------------------------------------------------ Settings / save

        public void SetSound(bool on)
        {
            Session.SoundOn = on;
            Audio.Muted = !on;
        }

        public void SetLanguage(string language, System.Action reopenAfterSettings)
        {
            Session.Language = language;
            Loc.Language = language;
            // Rebuild the UI so every static label picks up the new language.
            Destroy(UI.gameObject); // also destroys its canvas
            CreateUI();
            UI.ShowSettings(reopenAfterSettings);
            SaveNow();
        }

        public bool SaveNow()
        {
            autosaveTimer = 0f;
            if (resetting || Session == null) return false;
            bool ok = saveManager.Save(Session.ToSaveData());
            if (!ok) Debug.LogWarning("[PrintshopTycoon] Save failed: " + saveManager.LastError);
            return ok;
        }

        /// <summary>Deletes the save and rebuilds a fresh game in place (works in any scene).</summary>
        public void ResetGame()
        {
            resetting = true;
            saveManager.Delete();
            SetPaused(false);
            Build.CancelPlacement();
            Destroy(world.Root.gameObject);
            Destroy(dynamicRoot.gameObject);
            Destroy(gameObject); // UI, audio, systems and marker are children / owned
            new GameObject("PrintshopTycoon").AddComponent<GameManager>();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        void OnDestroy()
        {
            if (paused) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ Helpers

        public static int WorkerNumber(string workerId)
        {
            int i = workerId != null ? workerId.IndexOf('#') : -1;
            return i >= 0 && int.TryParse(workerId.Substring(i + 1), out var n) ? n : 1;
        }

        public static int PressNumber(string machineId)
        {
            if (machineId == WorkshopLayout.StarterPressId) return 1;
            int i = machineId != null ? machineId.IndexOf('#') : -1;
            return i >= 0 && int.TryParse(machineId.Substring(i + 1), out var n) ? n + 1 : 1;
        }
    }
}
