using System;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// Portrait-first interface built in code: HUD at the top, thumb controls at the bottom,
    /// modal panels in the middle. It only reads game state and forwards taps to GameManager.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        GameManager game;
        Canvas canvas;
        RectTransform canvasRect;
        RectTransform safe;

        // HUD
        Text coinsText, levelText, orderTitle, orderStep, infoText;
        Image levelFill;
        readonly List<Image> stepPips = new List<Image>();
        RectTransform silkeBubble;
        Text silkeText;
        float silkeHideAt;

        // Controls
        GameObject characterControls, factoryControls, placementBar, selectionPanel;
        VirtualJoystick joystick;
        Button interactButton;
        Text interactLabel;
        Image interactImage;
        Text selectionText;
        Text placementText;
        Button placementConfirm;
        FactoryTouchSurface touchSurface;

        // Overlays
        RectTransform modal;
        Action onModalClosed;
        RectTransform toastRoot;
        RectTransform labelRoot;
        readonly Dictionary<object, Text> worldLabels = new Dictionary<object, Text>();
        HeatPressMinigameView minigame;

        int shownCoins = -1, shownLevel = -1;
        string shownStep;
        bool resetArmed;

        public VirtualJoystick Joystick => joystick;
        public FactoryTouchSurface TouchSurface => touchSurface;
        public bool ModalOpen => modal != null;
        public bool MinigameOpen => minigame != null;

        public void Build(GameManager gameManager)
        {
            game = gameManager;
            EnsureEventSystem();

            canvas = new GameObject("UI", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.gameObject.layer = 5;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)canvas.transform;

            // Touch layer for factory mode sits behind everything else.
            var surfaceRect = UIFactory.Rect(canvas.transform, "FactoryTouchSurface").Stretch();
            surfaceRect.gameObject.AddComponent<EmptyGraphic>();
            touchSurface = surfaceRect.gameObject.AddComponent<FactoryTouchSurface>();
            surfaceRect.gameObject.SetActive(false);

            labelRoot = UIFactory.Rect(canvas.transform, "WorldLabels").Stretch();

            safe = UIFactory.Rect(canvas.transform, "SafeArea").Stretch();
            safe.gameObject.AddComponent<SafeArea>().Apply();

            BuildHud();
            BuildCharacterControls();
            BuildFactoryControls();
            BuildPlacementBar();
            toastRoot = UIFactory.Rect(safe, "Toasts").Stretch();
        }

        static void EnsureEventSystem()
        {
            var es = EventSystem.current != null ? EventSystem.current : UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                go.AddComponent<StandaloneInputModule>();
#endif
            }
            es.pixelDragThreshold = 6;
        }

        // ------------------------------------------------------------------ HUD

        void BuildHud()
        {
            // Coins
            var coins = UIFactory.Panel(safe, "Coins", Palette.UiDark);
            coins.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(340f, 110f));
            var coinIcon = UIFactory.CircleImage(coins.transform, "Icon", Palette.UiCoin);
            coinIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(78f, 78f));
            UIFactory.Label(coinIcon.transform, "C", "¢", 48, Palette.UiDark).rectTransform.Stretch();
            coinsText = UIFactory.Label(coins.transform, "Value", "0", 56, Color.white, TextAnchor.MiddleLeft);
            coinsText.rectTransform.Stretch(110, 0, 16, 0);

            // Level
            var level = UIFactory.Panel(safe, "Level", Palette.UiDark);
            level.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -24f), new Vector2(300f, 110f));
            levelText = UIFactory.Label(level.transform, "Value", "", 42, Color.white);
            levelText.rectTransform.Stretch(12, 8, 12, 34);
            var barBg = UIFactory.Panel(level.transform, "Bar", new Color(1f, 1f, 1f, 0.2f), true, false);
            barBg.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(240f, 18f));
            levelFill = UIFactory.Panel(barBg.transform, "Fill", Palette.UiGreen, true, false);
            levelFill.rectTransform.anchorMin = Vector2.zero;
            levelFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            levelFill.rectTransform.offsetMin = levelFill.rectTransform.offsetMax = Vector2.zero;

            // Pause
            var pause = UIFactory.Button(safe, "Pause", "II", Palette.UiDark, () => ShowPause(), 48);
            ((RectTransform)pause.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));

            // Order card
            var card = UIFactory.Panel(safe, "OrderCard", new Color(1f, 0.99f, 0.97f, 0.96f));
            card.rectTransform.TopStrip(150f, 200f, 24f);
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => ShowOrderPanel());
            orderTitle = UIFactory.Label(card.transform, "Title", "", 44, Palette.UiDark, TextAnchor.UpperLeft);
            orderTitle.rectTransform.Stretch(28, 18, 28, 120);
            orderStep = UIFactory.Label(card.transform, "Step", "", 40, Palette.UiAccent, TextAnchor.MiddleLeft);
            orderStep.rectTransform.Stretch(28, 74, 28, 48);
            var pips = UIFactory.Rect(card.transform, "Pips");
            pips.anchorMin = new Vector2(0f, 0f); pips.anchorMax = new Vector2(1f, 0f);
            pips.pivot = new Vector2(0.5f, 0f);
            pips.offsetMin = new Vector2(28f, 18f); pips.offsetMax = new Vector2(-28f, 40f);
            var h = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            for (int i = 0; i < PlayerOrderFlow.StepCount; i++)
                stepPips.Add(UIFactory.Panel(pips, "Pip" + i, Palette.UiGrey, true, false));

            infoText = UIFactory.Label(safe, "Info", "", 32, Palette.UiDark, TextAnchor.MiddleRight, FontStyle.Normal);
            infoText.rectTransform.TopStrip(354f, 44f, 40f);

            // Silke speech bubble (tap to dismiss)
            var bubble = UIFactory.Panel(safe, "SilkeBubble", Color.white);
            silkeBubble = bubble.rectTransform;
            silkeBubble.TopStrip(404f, 190f, 24f);
            bubble.gameObject.AddComponent<Button>().onClick.AddListener(() => silkeBubble.gameObject.SetActive(false));
            var portrait = UIFactory.CircleImage(bubble.transform, "Portrait", Palette.UiPurple);
            portrait.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(140f, 140f));
            UIFactory.Label(portrait.transform, "Initial", "S", 72, Color.white).rectTransform.Stretch();
            var name = UIFactory.Label(bubble.transform, "Name", Loc.T("silke.name"), 34, Palette.UiPurple, TextAnchor.UpperLeft);
            name.rectTransform.Stretch(180, 14, 24, 130);
            silkeText = UIFactory.Label(bubble.transform, "Text", "", 38, Palette.UiDark, TextAnchor.UpperLeft, FontStyle.Normal);
            silkeText.rectTransform.Stretch(180, 56, 24, 14);
            silkeBubble.gameObject.SetActive(false);
        }

        void BuildCharacterControls()
        {
            var root = UIFactory.Rect(safe, "CharacterControls").Stretch();
            characterControls = root.gameObject;

            // Joystick zone: lower-left area, floating stick.
            var zone = UIFactory.Rect(root, "JoystickZone");
            zone.anchorMin = new Vector2(0f, 0f); zone.anchorMax = new Vector2(0.55f, 0f);
            zone.pivot = new Vector2(0f, 0f);
            zone.offsetMin = Vector2.zero; zone.offsetMax = new Vector2(0f, 640f);
            zone.gameObject.AddComponent<EmptyGraphic>();
            var baseImg = UIFactory.CircleImage(zone, "Base", new Color(0.17f, 0.18f, 0.26f, 0.45f));
            baseImg.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(210f, 230f), new Vector2(280f, 280f));
            var knob = UIFactory.CircleImage(baseImg.transform, "Knob", new Color(1f, 1f, 1f, 0.9f));
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            // Base is anchored at the zone's bottom-left, so local coordinates match.
            joystick.Init(zone, baseImg.rectTransform, knob.rectTransform, 120f);

            // Interact button: large, lower-right.
            interactImage = UIFactory.CircleImage(root, "Interact", Palette.UiAccent, true);
            interactImage.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 90f), new Vector2(290f, 290f));
            interactButton = interactImage.gameObject.AddComponent<Button>();
            interactButton.onClick.AddListener(() => game.OnInteractPressed());
            interactLabel = UIFactory.Label(interactImage.transform, "Label", "", 40, Color.white);
            interactLabel.rectTransform.Stretch(30, 50, 30, 50);

            // Factory button: above the interact button, one-thumb reach.
            var factory = UIFactory.CircleImage(root, "FactoryButton", Palette.UiBlue, true);
            factory.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 420f), new Vector2(180f, 180f));
            factory.gameObject.AddComponent<Button>().onClick.AddListener(() => game.SetMode(CameraMode.Factory));
            UIFactory.Label(factory.transform, "Label", Loc.T("hud.factory"), 34, Color.white).rectTransform.Stretch(14, 14, 14, 14);
        }

        void BuildFactoryControls()
        {
            var root = UIFactory.Rect(safe, "FactoryControls").Stretch();
            factoryControls = root.gameObject;

            var bar = UIFactory.Panel(root, "Bar", new Color(0.17f, 0.18f, 0.26f, 0.92f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = new Vector2(24f, 30f); bar.rectTransform.offsetMax = new Vector2(-24f, 210f);
            var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 16, 16); h.spacing = 14;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            UIFactory.Button(bar.transform, "Build", Loc.T("hud.build"), Palette.UiGreen, () => ShowShop(), 40);
            UIFactory.Button(bar.transform, "Staff", Loc.T("hud.staff"), Palette.UiPurple, () => ShowStaff(), 40);
            UIFactory.Button(bar.transform, "Orders", Loc.T("hud.orders"), Palette.UiAccent, () => ShowOrderPanel(), 40);
            UIFactory.Button(bar.transform, "Back", Loc.T("hud.back_to_hue"), Palette.UiBlue, () => game.SetMode(CameraMode.Character), 40);

            var sel = UIFactory.Panel(root, "Selection", new Color(1f, 0.99f, 0.97f, 0.96f));
            sel.rectTransform.anchorMin = new Vector2(0f, 0f); sel.rectTransform.anchorMax = new Vector2(1f, 0f);
            sel.rectTransform.pivot = new Vector2(0.5f, 0f);
            sel.rectTransform.offsetMin = new Vector2(24f, 230f); sel.rectTransform.offsetMax = new Vector2(-24f, 420f);
            sel.gameObject.AddComponent<Button>().onClick.AddListener(() => selectionPanel.SetActive(false));
            selectionText = UIFactory.Label(sel.transform, "Text", "", 40, Palette.UiDark, TextAnchor.MiddleLeft, FontStyle.Normal);
            selectionText.rectTransform.Stretch(30, 16, 30, 16);
            selectionText.supportRichText = true;
            selectionPanel = sel.gameObject;
            selectionPanel.SetActive(false);
            factoryControls.SetActive(false);
        }

        void BuildPlacementBar()
        {
            var bar = UIFactory.Panel(safe, "PlacementBar", new Color(0.17f, 0.18f, 0.26f, 0.95f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = new Vector2(24f, 30f); bar.rectTransform.offsetMax = new Vector2(-24f, 330f);
            placementText = UIFactory.Label(bar.transform, "Hint", "", 40, Color.white);
            placementText.rectTransform.TopStrip(16f, 90f, 24f);
            placementConfirm = UIFactory.Button(bar.transform, "Confirm", "", Palette.UiGreen, () => game.ConfirmPlacement(), 42);
            ((RectTransform)placementConfirm.transform).Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(560f, 150f));
            var cancel = UIFactory.Button(bar.transform, "Cancel", Loc.T("place.cancel"), Palette.UiRed, () => game.CancelPlacement(), 42);
            ((RectTransform)cancel.transform).Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(400f, 150f));
            placementBar = bar.gameObject;
            placementBar.SetActive(false);
        }

        // ------------------------------------------------------------------ Per frame

        void Update()
        {
            if (game == null || game.Session == null) return;
            var s = game.Session;

            if (s.Wallet.Coins != shownCoins)
            {
                if (shownCoins >= 0 && s.Wallet.Coins > shownCoins) StartCoroutine(PunchRoutine(coinsText.transform.parent));
                shownCoins = s.Wallet.Coins;
                coinsText.text = FormatCoins(shownCoins);
            }
            if (s.Progression.Level != shownLevel)
            {
                shownLevel = s.Progression.Level;
                levelText.text = Loc.T("hud.level", shownLevel);
            }
            levelFill.rectTransform.anchorMax = new Vector2(s.Progression.LevelProgress, 1f);

            var o = s.Orders;
            orderTitle.text = Loc.T("order.title", Loc.T(o.Order.CustomerKey), o.Order.Quantity) + "   " + Loc.T("order.reward", o.Order.Reward);
            string step = Loc.T("step." + o.StepIndex, o.Order.Quantity);
            if (step != shownStep) { shownStep = step; orderStep.text = "► " + step; }
            for (int i = 0; i < stepPips.Count; i++)
                stepPips[i].color = i < o.StepIndex ? Palette.UiGreen : (i == o.StepIndex ? Palette.UiAccent : Palette.UiGrey);

            string info = Loc.T("hud.stock", s.Inventory.BlankStock);
            if (s.Workers.Count > 0) info += "   ·   " + Loc.T("hud.autojobs", s.AutoJobs.Queued + s.AutoJobs.InProgress);
            infoText.text = info;

            if (silkeBubble.gameObject.activeSelf && Time.unscaledTime > silkeHideAt) silkeBubble.gameObject.SetActive(false);

            // Interact button reflects the focused station.
            var interaction = game.Interaction;
            bool available = interaction.FocusedAvailable;
            interactButton.interactable = available;
            interactImage.color = available ? Palette.UiAccent : new Color(0.55f, 0.57f, 0.63f, 0.75f);
            interactLabel.text = interaction.Focused != null ? interaction.FocusedLabel : "";
            if (available) interactImage.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.03f);
            else interactImage.rectTransform.localScale = Vector3.one;

            UpdateWorldLabels();
            UpdatePlacementBar();
        }

        /// <summary>Danish thousands separator (1.250) without depending on device culture data.</summary>
        public static string FormatCoins(int value)
        {
            string digits = Math.Abs(value).ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return (value < 0 ? "-" : "") + sb;
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
        }

        System.Collections.IEnumerator PunchRoutine(Transform t)
        {
            for (float a = 0f; a < 0.2f; a += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * (1f + Mathf.Sin(a / 0.2f * Mathf.PI) * 0.12f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        public void SetMode(CameraMode mode)
        {
            bool factory = mode == CameraMode.Factory;
            characterControls.SetActive(!factory);
            factoryControls.SetActive(factory && !game.Build.IsPlacing);
            touchSurface.gameObject.SetActive(factory);
            if (!factory) selectionPanel.SetActive(false);
            joystick.Release();
        }

        void UpdatePlacementBar()
        {
            bool placing = game.Build.IsPlacing;
            if (placementBar.activeSelf != placing)
            {
                placementBar.SetActive(placing);
                factoryControls.SetActive(!placing && game.Cameras.Mode == CameraMode.Factory);
            }
            if (!placing) return;
            var def = game.Build.Placing;
            bool valid = game.Build.CurrentValid;
            bool afford = game.Session.Wallet.CanAfford(def.price);
            placementText.text = valid ? Loc.T("place.hint") : Loc.T("place.invalid");
            placementText.color = valid ? Color.white : Palette.Invalid;
            UIFactory.LabelOf(placementConfirm).text = Loc.T("place.confirm", def.price);
            placementConfirm.interactable = valid && afford;
        }

        public void ShowSelection(string text)
        {
            if (string.IsNullOrEmpty(text)) { selectionPanel.SetActive(false); return; }
            selectionText.text = text;
            selectionPanel.SetActive(true);
        }

        // ------------------------------------------------------------------ World labels

        void UpdateWorldLabels()
        {
            var cam = game.Cameras.Camera;
            var seen = HashSetPool;
            seen.Clear();
            foreach (var w in game.WorkerViews)
            {
                seen.Add(w);
                PlaceLabel(w, w.LabelPosition, Loc.T("staff.worker", GameManager.WorkerNumber(w.Machine.Id)) + "\n" + w.StatusText, Palette.UiPurple, cam);
            }
            if (game.Cameras.Mode == CameraMode.Factory && !game.Cameras.IsTransitioning)
            {
                foreach (var press in game.Presses)
                {
                    seen.Add(press);
                    PlaceLabel(press, press.TopPosition + Vector3.up * 0.6f, press.DisplayName, Palette.UiDark, cam);
                }
            }
            var remove = new List<object>();
            foreach (var kv in worldLabels)
                if (!seen.Contains(kv.Key) || kv.Key == null || (kv.Key is UnityEngine.Object uo && uo == null))
                    remove.Add(kv.Key);
            foreach (var k in remove)
            {
                if (worldLabels[k] != null) Destroy(worldLabels[k].transform.parent.gameObject);
                worldLabels.Remove(k);
            }
        }

        static readonly HashSet<object> HashSetPool = new HashSet<object>();

        void PlaceLabel(object key, Vector3 world, string text, Color color, Camera cam)
        {
            if (!worldLabels.TryGetValue(key, out var label))
            {
                var bg = UIFactory.Panel(labelRoot, "Label", new Color(color.r, color.g, color.b, 0.85f), true, false);
                bg.rectTransform.sizeDelta = new Vector2(300f, 96f);
                label = UIFactory.Label(bg.transform, "Text", "", 30, Color.white);
                label.rectTransform.Stretch(10, 6, 10, 6);
                worldLabels[key] = label;
            }
            var rt = (RectTransform)label.transform.parent;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool visible = sp.z > 0f && !ModalOpen;
            rt.gameObject.SetActive(visible);
            if (!visible) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local))
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = local;
            }
            if (label.text != text) label.text = text;
        }

        // ------------------------------------------------------------------ Feedback

        public void Toast(string message, bool error = false)
        {
            var bg = UIFactory.Panel(toastRoot, "Toast", error ? new Color(0.91f, 0.31f, 0.36f, 0.95f) : new Color(0.17f, 0.18f, 0.26f, 0.92f), true, false);
            bg.rectTransform.Place(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 110f));
            var t = UIFactory.Label(bg.transform, "Text", message, 42, Color.white);
            t.rectTransform.Stretch(20, 10, 20, 10);
            // Push older toasts up.
            for (int i = 0; i < toastRoot.childCount - 1; i++)
            {
                var older = (RectTransform)toastRoot.GetChild(i);
                older.anchoredPosition += new Vector2(0f, 125f);
            }
            StartCoroutine(FadeAndDestroy(bg.gameObject, 2.2f));
        }

        public void FloatingCoins(Vector3 world, int amount)
        {
            var cam = game.Cameras.Camera;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
            var t = UIFactory.Label(canvasRect, "FloatingCoins", Loc.T("toast.paid", amount), 72, Palette.UiCoin);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(700f, 120f);
            t.rectTransform.anchoredPosition = local;
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.UiDark;
            outline.effectDistance = new Vector2(4f, -4f);
            StartCoroutine(FloatUp(t.rectTransform, 1.4f));
        }

        System.Collections.IEnumerator FadeAndDestroy(GameObject go, float life)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            for (float a = 0f; a < life && go != null; a += Time.unscaledDeltaTime)
            {
                group.alpha = a < life - 0.4f ? 1f : (life - a) / 0.4f;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        System.Collections.IEnumerator FloatUp(RectTransform rt, float life)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Vector2 start = rt.anchoredPosition;
            for (float a = 0f; a < life && rt != null; a += Time.unscaledDeltaTime)
            {
                float k = a / life;
                rt.anchoredPosition = start + new Vector2(0f, 260f * (1f - (1f - k) * (1f - k)));
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : 1f);
                group.alpha = k < 0.7f ? 1f : (1f - k) / 0.3f;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        public void ShowSilke(string text, float seconds = 7f)
        {
            silkeText.text = text;
            silkeBubble.gameObject.SetActive(true);
            silkeHideAt = Time.unscaledTime + seconds;
            StartCoroutine(PunchRoutine(silkeBubble));
        }

        // ------------------------------------------------------------------ Modals

        RectTransform OpenModal(string title, float height, Action onClosed = null, bool pauseGame = false)
        {
            onModalClosed = null;
            DestroyModal();
            onModalClosed = onClosed;
            game.SetPaused(pauseGame);
            var dim = UIFactory.Panel(canvasRect, "Modal", new Color(0.05f, 0.05f, 0.1f, 0.6f), false, true);
            dim.rectTransform.Stretch();
            modal = dim.rectTransform;

            var card = UIFactory.Panel(dim.transform, "Card", Palette.UiPanel);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(980f, height));
            var titleText = UIFactory.Label(card.transform, "Title", title, 56, Palette.UiDark);
            titleText.rectTransform.TopStrip(30f, 90f, 120f);
            var close = UIFactory.Button(card.transform, "Close", "X", Palette.UiRed, () => CloseModal(), 46);
            ((RectTransform)close.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(100f, 100f));
            var content = UIFactory.Rect(card.transform, "Content").Stretch(40, 140, 40, 40);
            var list = UIFactory.VerticalList(content, 22, 0);
            list.childAlignment = TextAnchor.UpperCenter;
            game.Audio.Play(Sfx.Click, 0.6f);
            game.OnModalChanged(true);
            return content;
        }

        void DestroyModal()
        {
            if (modal == null) return;
            if (minigame != null) { minigame.Abort(); minigame = null; }
            Destroy(modal.gameObject);
            modal = null;
        }

        public void CloseModal()
        {
            if (modal == null) return;
            DestroyModal();
            var cb = onModalClosed;
            onModalClosed = null;
            game.SetPaused(false);
            game.OnModalChanged(false);
            cb?.Invoke();
        }

        Text Paragraph(RectTransform content, string text, int size = 40, Color? color = null, float height = 70f, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var t = UIFactory.Label(content, "Text", text, size, color ?? Palette.UiDark, anchor, style);
            UIFactory.Height(t, height);
            return t;
        }

        Button ListButton(RectTransform content, string text, Color color, Action onClick, float height = 140f)
        {
            var b = UIFactory.Button(content, "Button", text, color, onClick, 46);
            UIFactory.Height(b, height);
            return b;
        }

        public void ShowOrderPanel()
        {
            var s = game.Session;
            var o = s.Orders;
            var content = OpenModal(Loc.T("order.panel_title"), 1120f);
            Paragraph(content, Loc.T(o.Order.CustomerKey), 54, Palette.UiAccent, 80f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Paragraph(content, o.Order.Quantity + " T-shirts", 42);

            var logo = s.Balance.FindLogo(o.Order.LogoId);
            var swatchRow = UIFactory.Rect(content, "Logo");
            UIFactory.Height(swatchRow, 170f);
            var swatch = UIFactory.CircleImage(swatchRow, "Swatch", logo != null ? MaterialLibrary.Hex(logo.colorHex) : Color.white);
            swatch.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180f, 0f), new Vector2(150f, 150f));
            var logoName = UIFactory.Label(swatchRow, "Name", Loc.T("order.logo", logo != null ? Loc.T(logo.nameKey) : "?"), 42, Palette.UiDark, TextAnchor.MiddleLeft);
            logoName.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-80f, 0f), new Vector2(460f, 120f));

            Paragraph(content, Loc.T("order.reward", o.Order.Reward), 46, Palette.UiGreen, 80f, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (o.Step == OrderStep.Offered)
            {
                ListButton(content, Loc.T("order.accept"), Palette.UiGreen, () =>
                {
                    if (o.Accept() == ActionResult.Ok) { game.Audio.Play(Sfx.Good); CloseModal(); }
                }, 160f);
            }
            else
            {
                Paragraph(content, Loc.T("order.in_progress", o.StepIndex, PlayerOrderFlow.StepCount - 1), 40, Palette.UiDark, 70f);
                Paragraph(content, "► " + Loc.T("step." + o.StepIndex, o.Order.Quantity), 40, Palette.UiAccent, 110f);
            }
            if (s.Workers.Count > 0)
                Paragraph(content, Loc.T("hud.autojobs", s.AutoJobs.Queued + s.AutoJobs.InProgress), 36, Palette.UiPurple, 60f);
        }

        public void ShowLogoSelect(Action<string> onPicked)
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("logo.select_title"), 1000f);
            foreach (var logo in s.Balance.logos)
            {
                var id = logo.id;
                var b = ListButton(content, Loc.T(logo.nameKey), MaterialLibrary.Hex(logo.colorHex), () =>
                {
                    CloseModal();
                    onPicked(id);
                }, 190f);
                UIFactory.LabelOf(b).fontSize = 52;
            }
            var o = s.Orders.Order;
            Paragraph(content, Loc.T("order.title", Loc.T(o.CustomerKey), o.Quantity), 36, Palette.UiDark, 70f);
        }

        public void ShowHeatPressMinigame(HeatPressStation station)
        {
            var content = OpenModal(Loc.T("machine.heat_press"), 900f, null);
            minigame = content.gameObject.AddComponent<HeatPressMinigameView>();
            minigame.Build(game, station, content, () => { minigame = null; CloseModal(); });
        }

        public void ShowShop()
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("shop.title"), 860f);
            foreach (var def in s.Balance.machines)
            {
                var d = def;
                Paragraph(content, Loc.T(def.nameKey), 48, Palette.UiDark, 80f, TextAnchor.MiddleCenter, FontStyle.Bold);
                var b = ListButton(content, Loc.T("shop.buy", def.price), Palette.UiGreen, () =>
                {
                    CloseModal();
                    game.BeginPlacement(d);
                }, 150f);
                b.interactable = s.Wallet.CanAfford(def.price);
            }
            Paragraph(content, Loc.T("shop.soon"), 34, Palette.UiGrey, 70f);
            Paragraph(content, Loc.T("act.restock", s.Balance.restockAmount, s.Balance.restockCost) + "  –  " + Loc.T("station.shelf"), 34, Palette.UiDark, 60f);
            var restock = ListButton(content, Loc.T("act.restock", s.Balance.restockAmount, s.Balance.restockCost), Palette.UiBlue, () =>
            {
                var r = s.TryRestock();
                if (r == ActionResult.Ok) { game.Audio.Play(Sfx.Purchase); Toast(Loc.T("toast.restocked", s.Balance.restockAmount)); }
                else { game.Audio.Play(Sfx.Error); Toast(Loc.T("toast.no_coins"), true); }
            }, 120f);
            restock.interactable = s.Wallet.CanAfford(s.Balance.restockCost);
        }

        public void ShowStaff()
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("staff.title"), 1240f);
            Paragraph(content, Loc.T("staff.explain"), 36, Palette.UiDark, 110f);
            var hire = ListButton(content, Loc.T("staff.hire", s.Balance.workerHireCost), Palette.UiPurple, () =>
            {
                game.HireWorker();
                ShowStaff(); // refresh
            }, 150f);
            hire.interactable = s.CanHireWorker && s.Wallet.CanAfford(s.Balance.workerHireCost);

            if (s.Workers.Count == 0) Paragraph(content, Loc.T("staff.none"), 40, Palette.UiGrey, 90f);
            foreach (var w in s.Workers)
            {
                var worker = w;
                var press = game.FindPress(w.AssignedMachineId);
                string where = press != null ? Loc.T("staff.at", press.DisplayName + " " + GameManager.PressNumber(press.MachineId)) : Loc.T("staff.unassigned");
                var view = game.FindWorkerView(w);
                Paragraph(content, Loc.T("staff.worker", GameManager.WorkerNumber(w.Id)) + "  –  " + where + (view != null ? "\n" + view.StatusText : ""), 36, Palette.UiDark, 100f);
                ListButton(content, Loc.T("staff.assign"), Palette.UiBlue, () =>
                {
                    var next = s.NextFreePressFor(worker);
                    if (next != null && s.AssignWorker(worker, next) == ActionResult.Ok) game.Audio.Play(Sfx.Click);
                    else { game.Audio.Play(Sfx.Error); Toast(Loc.T("toast.station_busy"), true); }
                    ShowStaff();
                }, 110f);
            }
        }

        public void ShowSettings(Action onClosed = null)
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("menu.settings"), 900f, onClosed, true);
            ListButton(content, Loc.T("settings.sound", Loc.T(s.SoundOn ? "settings.on" : "settings.off")), Palette.UiBlue, () =>
            {
                game.SetSound(!s.SoundOn);
                ShowSettings(onClosed);
            });
            ListButton(content, Loc.T("settings.language"), Palette.UiPurple, () =>
            {
                game.SetLanguage(Loc.Language == "da" ? "en" : "da");
            });
            var reset = ListButton(content, Loc.T("menu.reset"), Palette.UiRed, null);
            reset.onClick.AddListener(() =>
            {
                if (!resetArmed)
                {
                    resetArmed = true;
                    UIFactory.LabelOf(reset).text = Loc.T("menu.reset_confirm");
                    return;
                }
                resetArmed = false;
                game.ResetGame();
            });
            resetArmed = false;
            Paragraph(content, "Prototype v0.1", 30, Palette.UiGrey, 60f);
        }

        public void ShowPause()
        {
            if (ModalOpen && minigame != null) return;
            var content = OpenModal(Loc.T("menu.pause"), 900f, null, true);
            ListButton(content, Loc.T("menu.resume"), Palette.UiGreen, () => CloseModal());
            ListButton(content, Loc.T("menu.save"), Palette.UiBlue, () =>
            {
                Toast(Loc.T(game.SaveNow() ? "toast.saved" : "toast.save_failed"), false);
            });
            ListButton(content, Loc.T("menu.settings"), Palette.UiPurple, () => ShowSettings(() => ShowPause()));
            ListButton(content, Loc.T("menu.main"), Palette.UiAccent, () => { game.SaveNow(); ShowMainMenu(); });
        }

        public void ShowMainMenu()
        {
            onModalClosed = null;
            DestroyModal();
            var bg = UIFactory.Panel(canvasRect, "MainMenu", Palette.UiAccent, false, true);
            bg.rectTransform.Stretch();
            modal = bg.rectTransform;
            game.SetPaused(true);
            game.OnModalChanged(true);

            var inner = UIFactory.Rect(bg.transform, "Safe").Stretch();
            inner.gameObject.AddComponent<SafeArea>().Apply();
            var title = UIFactory.Label(inner, "Title", Loc.T("game.title"), 110, Color.white);
            title.rectTransform.TopStrip(260f, 260f, 40f);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.UiDark; outline.effectDistance = new Vector2(6f, -6f);
            var sub = UIFactory.Label(inner, "Subtitle", Loc.T("menu.subtitle"), 48, Palette.UiDark);
            sub.rectTransform.TopStrip(540f, 90f, 60f);

            // Placeholder mascot badges (real art comes from the brand owner).
            var hue = UIFactory.CircleImage(inner, "Hue", MaterialLibrary.Hex("#FFC93C"));
            hue.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 170f), new Vector2(240f, 240f));
            UIFactory.Label(hue.transform, "Name", "Hue", 56, Palette.UiDark).rectTransform.Stretch();
            var silke = UIFactory.CircleImage(inner, "Silke", Palette.UiPurple);
            silke.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 170f), new Vector2(240f, 240f));
            UIFactory.Label(silke.transform, "Name", "Silke", 56, Color.white).rectTransform.Stretch();

            var buttons = UIFactory.Rect(inner, "Buttons");
            buttons.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(760f, 560f));
            UIFactory.VerticalList(buttons, 30, 0);
            bool hasProgress = game.Session.Progression.CompletedOrders > 0 || game.Session.Orders.Step != OrderStep.Offered;
            UIFactory.Height(UIFactory.Button(buttons, "Play", Loc.T(hasProgress ? "menu.continue" : "menu.play"), Palette.UiGreen, () => CloseModal(), 64), 180f);
            UIFactory.Height(UIFactory.Button(buttons, "Settings", Loc.T("menu.settings"), Palette.UiBlue, () => ShowSettings(() => ShowMainMenu()), 52), 150f);
            var footer = UIFactory.Label(inner, "Footer", "Prototype v0.1  ·  Hue & Silke: placeholder", 30, new Color(1f, 1f, 1f, 0.8f), TextAnchor.MiddleCenter, FontStyle.Normal);
            footer.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 60f));
        }
    }
}
