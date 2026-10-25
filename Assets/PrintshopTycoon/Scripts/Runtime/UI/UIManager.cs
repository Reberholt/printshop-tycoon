using System;
using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// Portrait-first interface built in code. Permanent HUD is kept small: a status row
    /// (coins, level, stock, pause), one compact order card that expands on tap, and the
    /// thumb controls. Everything else lives in modal panels (UIManager.Panels.cs) and in
    /// feedback (UIManager.Feedback.cs). It only reads game state and forwards taps.
    /// </summary>
    [DefaultExecutionOrder(100)] // world labels are placed after the camera moved
    public partial class UIManager : MonoBehaviour
    {
        const float Margin = 20f;

        GameManager game;
        Canvas canvas;
        RectTransform canvasRect;
        RectTransform safe;

        // Status row
        Text coinsText, levelText, stockText;
        Image levelFill;

        // Compact order card
        RectTransform orderCard;
        Image orderIcon, orderIconInner;
        Text orderTitle, orderSub, orderBadge;
        Image orderBadgeBg;
        readonly List<Image> stepPips = new List<Image>();
        RectTransform orderDetails;
        Text orderDetailsText;
        float detailsHideAt;

        // Controls
        GameObject characterControls, factoryControls, placementBar, selectionPanel;
        VirtualJoystick joystick;
        Button interactButton;
        Text interactLabel;
        Image interactImage, interactProgress;
        Text selectionText;
        Text placementText;
        Button placementConfirm;
        FactoryTouchSurface touchSurface;

        int shownCoins = -1, shownLevel = -1;
        int shownOrderKey = int.MinValue, shownBadgeSeconds = -1;
        string shownOrderLanguage;
        bool resetArmed;
        int shownStockKey = -1;

        public VirtualJoystick Joystick => joystick;
        public FactoryTouchSurface TouchSurface => touchSurface;

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
            scaler.matchWidthOrHeight = 0f; // portrait only: width drives the layout, so nothing overflows on tall phones
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

            BuildStatusRow();
            BuildOrderCard();
            BuildCharacterControls();
            BuildFactoryControls();
            BuildPlacementBar();

            overlay = UIFactory.Rect(canvasRect, "Overlay").Stretch();
            overlay.gameObject.AddComponent<SafeArea>().Apply();
            BuildSilkeBubble(overlay);
            toastRoot = UIFactory.Rect(overlay, "Toasts").Stretch();
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
            // No keyboard navigation/submit: Space and WASD belong to Hue, not to UI buttons.
            es.sendNavigationEvents = false;
        }

        // ------------------------------------------------------------------ HUD

        Image Pill(string name, float x, float width, Action onClick)
        {
            var pill = UIFactory.Panel(safe, name, new Color(0.13f, 0.13f, 0.15f, 0.88f));
            pill.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -Margin), new Vector2(width, 96f));
            if (onClick != null) pill.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            return pill;
        }

        void BuildStatusRow()
        {
            var coins = Pill("Coins", Margin, 300f, null);
            var coinIcon = UIFactory.CircleImage(coins.transform, "Icon", Palette.UiCoin);
            coinIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(68f, 68f));
            UIFactory.Label(coinIcon.transform, "C", "¢", 44, Palette.UiDark).rectTransform.Stretch();
            coinsText = UIFactory.Label(coins.transform, "Value", "0", 50, Color.white, TextAnchor.MiddleLeft);
            coinsText.rectTransform.Stretch(96, 0, 12, 0);

            var level = Pill("Level", Margin * 2 + 300f, 250f, () => ShowProgress());
            levelText = UIFactory.Label(level.transform, "Value", "", 38, Color.white);
            levelText.rectTransform.Stretch(12, 6, 12, 32);
            var barBg = UIFactory.Panel(level.transform, "Bar", new Color(1f, 1f, 1f, 0.2f), true, false);
            barBg.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(200f, 16f));
            levelFill = UIFactory.Panel(barBg.transform, "Fill", Palette.UiGreen, true, false);
            levelFill.rectTransform.anchorMin = Vector2.zero;
            levelFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            levelFill.rectTransform.offsetMin = levelFill.rectTransform.offsetMax = Vector2.zero;

            var stock = Pill("Stock", Margin * 3 + 550f, 230f, () => ShowShop());
            var boxIcon = UIFactory.Panel(stock.transform, "Icon", Palette.Cardboard, true, false);
            boxIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(56f, 50f));
            stockText = UIFactory.Label(stock.transform, "Value", "", 40, Color.white, TextAnchor.MiddleLeft);
            stockText.rectTransform.Stretch(84, 0, 10, 0);

            var pause = UIFactory.Button(safe, "Pause", "II", new Color(0.13f, 0.13f, 0.15f, 0.88f), () => ShowPause(), 44);
            ((RectTransform)pause.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(96f, 96f));
        }

        void BuildOrderCard()
        {
            var card = UIFactory.Panel(safe, "OrderCard", new Color(1f, 0.99f, 0.97f, 0.95f));
            orderCard = card.rectTransform;
            orderCard.TopStrip(Margin * 2 + 96f, 128f, Margin);
            card.gameObject.AddComponent<Button>().onClick.AddListener(ToggleOrderDetails);

            orderIcon = UIFactory.CircleImage(card.transform, "Logo", Color.white);
            orderIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 4f), new Vector2(92f, 92f));
            orderIconInner = UIFactory.Panel(orderIcon.transform, "Product", Color.white, true, false);
            orderIconInner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 40f));

            orderTitle = UIFactory.Label(card.transform, "Step", "", 38, Palette.UiDark, TextAnchor.MiddleLeft);
            orderTitle.rectTransform.Stretch(126, 10, 150, 62);
            orderSub = UIFactory.Label(card.transform, "Sub", "", 30, new Color(0.35f, 0.36f, 0.42f), TextAnchor.MiddleLeft, FontStyle.Normal);
            orderSub.rectTransform.Stretch(126, 62, 150, 22);

            orderBadgeBg = UIFactory.Panel(card.transform, "Badge", Branding.Primary, true, false);
            orderBadgeBg.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 6f), new Vector2(124f, 70f));
            orderBadge = UIFactory.Label(orderBadgeBg.transform, "Text", "", 36, Color.white);
            orderBadge.rectTransform.Stretch(4, 4, 4, 4);

            var pips = UIFactory.Rect(card.transform, "Pips");
            pips.anchorMin = new Vector2(0f, 0f); pips.anchorMax = new Vector2(1f, 0f);
            pips.pivot = new Vector2(0.5f, 0f);
            pips.offsetMin = new Vector2(126f, 10f); pips.offsetMax = new Vector2(-150f, 20f);
            var h = pips.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            for (int i = 0; i < PlayerOrderFlow.StepCount; i++)
                stepPips.Add(UIFactory.Panel(pips, "Pip" + i, Palette.UiGrey, true, false));

            // Expandable details under the card (tap the card to toggle).
            var details = UIFactory.Panel(safe, "OrderDetails", new Color(1f, 0.99f, 0.97f, 0.97f));
            orderDetails = details.rectTransform;
            orderDetails.TopStrip(Margin * 2 + 96f + 136f, 250f, Margin);
            details.gameObject.AddComponent<Button>().onClick.AddListener(ToggleOrderDetails);
            orderDetailsText = UIFactory.Label(details.transform, "Text", "", 32, Palette.UiDark, TextAnchor.UpperLeft, FontStyle.Normal);
            orderDetailsText.rectTransform.Stretch(28, 18, 28, 18);
            orderDetailsText.supportRichText = true;
            orderDetails.gameObject.SetActive(false);
        }

        void ToggleOrderDetails()
        {
            bool show = !orderDetails.gameObject.activeSelf;
            orderDetails.gameObject.SetActive(show);
            detailsHideAt = Time.unscaledTime + 8f;
            game.Audio.Play(Sfx.Click, 0.4f);
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
            var baseImg = UIFactory.CircleImage(zone, "Base", new Color(0.13f, 0.13f, 0.15f, 0.4f));
            baseImg.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(210f, 230f), new Vector2(280f, 280f));
            var knob = UIFactory.CircleImage(baseImg.transform, "Knob", new Color(1f, 1f, 1f, 0.9f));
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f));
            joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            joystick.Init(zone, baseImg.rectTransform, knob.rectTransform, 120f);

            // Interact button: the largest, lower-right; a ring fills while a machine works.
            interactImage = UIFactory.CircleImage(root, "Interact", Palette.UiAccent, true);
            interactImage.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 80f), new Vector2(300f, 300f));
            interactButton = interactImage.gameObject.AddComponent<Button>();
            interactButton.onClick.AddListener(() => game.OnInteractPressed());
            interactProgress = UIFactory.CircleImage(interactImage.transform, "Progress", new Color(1f, 1f, 1f, 0.35f));
            interactProgress.rectTransform.Stretch(14, 14, 14, 14);
            interactProgress.type = Image.Type.Filled;
            interactProgress.fillMethod = Image.FillMethod.Radial360;
            interactProgress.fillOrigin = 2; // top
            interactProgress.fillAmount = 0f;
            interactLabel = UIFactory.Label(interactImage.transform, "Label", "", 40, Color.white);
            interactLabel.rectTransform.Stretch(30, 54, 30, 54);

            // Factory button: smaller and secondary, above the interact button.
            var factory = UIFactory.CircleImage(root, "FactoryButton", new Color(0.13f, 0.13f, 0.15f, 0.9f), true);
            factory.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-70f, 410f), new Vector2(170f, 170f));
            factory.gameObject.AddComponent<Button>().onClick.AddListener(() => game.SetMode(CameraMode.Factory));
            UIFactory.Label(factory.transform, "Label", Loc.T("hud.factory"), 32, Color.white).rectTransform.Stretch(14, 14, 14, 14);
        }

        void BuildFactoryControls()
        {
            var root = UIFactory.Rect(safe, "FactoryControls").Stretch();
            factoryControls = root.gameObject;

            var bar = UIFactory.Panel(root, "Bar", new Color(0.13f, 0.13f, 0.15f, 0.92f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = new Vector2(Margin, 30f); bar.rectTransform.offsetMax = new Vector2(-Margin, 200f);
            var h = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 12, 14, 14); h.spacing = 10;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = true;
            UIFactory.Button(bar.transform, "Build", Loc.T("hud.build"), Palette.UiGreen, () => ShowShop(), 34);
            UIFactory.Button(bar.transform, "Upgrade", Loc.T("hud.upgrades"), Palette.UiAccent, () => ShowUpgrades(), 34);
            UIFactory.Button(bar.transform, "Staff", Loc.T("hud.staff"), Palette.UiPurple, () => ShowStaff(), 34);
            UIFactory.Button(bar.transform, "Orders", Loc.T("hud.orders"), Palette.UiBlue, () => ShowOrderPanel(), 34);
            UIFactory.Button(bar.transform, "Back", Loc.T("hud.back_to_hue"), Branding.Primary, () => game.SetMode(CameraMode.Character), 34);

            var sel = UIFactory.Panel(root, "Selection", new Color(1f, 0.99f, 0.97f, 0.96f));
            sel.rectTransform.anchorMin = new Vector2(0f, 0f); sel.rectTransform.anchorMax = new Vector2(1f, 0f);
            sel.rectTransform.pivot = new Vector2(0.5f, 0f);
            sel.rectTransform.offsetMin = new Vector2(Margin, 220f); sel.rectTransform.offsetMax = new Vector2(-Margin, 430f);
            sel.gameObject.AddComponent<Button>().onClick.AddListener(() => selectionPanel.SetActive(false));
            selectionText = UIFactory.Label(sel.transform, "Text", "", 38, Palette.UiDark, TextAnchor.MiddleLeft, FontStyle.Normal);
            selectionText.rectTransform.Stretch(30, 16, 30, 16);
            selectionText.supportRichText = true;
            selectionPanel = sel.gameObject;
            selectionPanel.SetActive(false);
            factoryControls.SetActive(false);
        }

        void BuildPlacementBar()
        {
            var bar = UIFactory.Panel(safe, "PlacementBar", new Color(0.13f, 0.13f, 0.15f, 0.95f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.offsetMin = new Vector2(Margin, 30f); bar.rectTransform.offsetMax = new Vector2(-Margin, 330f);
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
                if (shownLevel > 0) StartCoroutine(PunchRoutine(levelText.transform.parent));
                shownLevel = s.Progression.Level;
                levelText.text = Loc.T("hud.level", shownLevel);
            }
            levelFill.rectTransform.anchorMax = new Vector2(s.Progression.LevelProgress, 1f);
            int stockKey = s.Inventory.Total * 10000 + s.Inventory.Capacity;
            if (stockKey != shownStockKey) { shownStockKey = stockKey; stockText.text = s.Inventory.Total + "/" + s.Inventory.Capacity; }
            stockText.color = s.Inventory.FreeSpace == 0 ? Palette.UiCoin : Color.white;

            UpdateOrderCard(s);
            if (silkeBubble.gameObject.activeSelf && Time.unscaledTime > silkeHideAt) silkeBubble.gameObject.SetActive(false);
            if (orderDetails.gameObject.activeSelf && Time.unscaledTime > detailsHideAt) orderDetails.gameObject.SetActive(false);

            // Interact button reflects the focused station (and fills while a machine works).
            var interaction = game.Interaction;
            bool available = interaction.FocusedAvailable;
            interactButton.interactable = available;
            interactImage.color = available ? Palette.UiAccent : new Color(0.45f, 0.47f, 0.52f, 0.8f);
            interactLabel.text = interaction.Focused != null ? interaction.FocusedLabel : "";
            float progress = 0f;
            if (interaction.Focused != null && interaction.Focused.TryGetProgress(out float p, out bool ready) && !ready) progress = p;
            interactProgress.fillAmount = progress;
            interactImage.rectTransform.localScale = available ? Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.03f) : Vector3.one;

            UpdatePlacementBar();
        }

        void UpdateOrderCard(GameSession s)
        {
            var o = s.Orders;
            var order = o.Order;
            var logo = s.Balance.FindLogo(order.LogoId);
            var product = s.Balance.FindProduct(order.ProductId);
            orderIcon.color = logo != null ? MaterialLibrary.Hex(logo.colorHex) : Color.white;
            orderIconInner.color = product != null ? MaterialLibrary.Hex(product.colorHex) : Color.white;

            // Rebuild strings only when something visible changed (no per-frame garbage on mobile).
            int percent = Mathf.RoundToInt((o.IsPrinting ? o.PrintProgress : (o.Step == OrderStep.CollectFinished ? o.PressProgress : 0f)) * 100f);
            int key = (((order.Sequence * 7 + o.StepIndex) * 101 + percent) * 3 + (o.TransfersReady ? 1 : 0) + (o.PressDone ? 2 : 0)) * 31 + (int)o.Quality + (order.IsSpecial ? 17 : 0) + (o.SpecialExpired ? 5 : 0);
            if (key != shownOrderKey || Loc.Language != shownOrderLanguage)
            {
                shownOrderKey = key;
                shownOrderLanguage = Loc.Language;
                orderTitle.text = "► " + StepText(o, product);
                string productName = product != null ? Loc.T(product.nameKey) : "";
                orderSub.text = Loc.T(order.CustomerKey) + " · " + order.Quantity + " " + productName + " · " + Loc.T("order.net", o.CalculatePayout().Net);
            }

            for (int i = 0; i < stepPips.Count; i++)
                stepPips[i].color = i < o.StepIndex ? Palette.UiGreen : (i == o.StepIndex ? Palette.UiAccent : Palette.UiGrey);

            if (order.IsSpecial && o.Step != OrderStep.Offered)
            {
                orderBadgeBg.gameObject.SetActive(true);
                bool late = o.SpecialExpired;
                orderBadgeBg.color = late ? Palette.UiGrey : (o.SpecialRemaining < 30f && Mathf.Repeat(Time.time * 2f, 1f) > 0.5f ? Palette.UiCoin : Branding.Primary);
                int secondsLeft = late ? 0 : Mathf.CeilToInt(o.SpecialRemaining);
                if (secondsLeft != shownBadgeSeconds) { shownBadgeSeconds = secondsLeft; orderBadge.text = FormatTime(secondsLeft); }
            }
            else if (o.OfferedSpecial != null)
            {
                orderBadgeBg.gameObject.SetActive(true);
                orderBadgeBg.color = Branding.Primary;
                orderBadge.text = "♦";
                shownBadgeSeconds = -1;
            }
            else orderBadgeBg.gameObject.SetActive(false);

            if (orderDetails.gameObject.activeSelf) orderDetailsText.text = OrderDetailsText(s);
        }

        static string StepText(PlayerOrderFlow o, ProductDefinition product)
        {
            string productName = product != null ? Loc.T(product.nameKey) : "";
            switch (o.Step)
            {
                case OrderStep.PrintTransfers when o.IsPrinting: return Loc.T("step.2_printing") + " " + Mathf.RoundToInt(o.PrintProgress * 100f) + "%";
                case OrderStep.PrintTransfers when o.TransfersReady: return Loc.T("step.2_ready");
                case OrderStep.CollectFinished when !o.PressDone: return Loc.T("step.4_pressing") + " " + Mathf.RoundToInt(o.PressProgress * 100f) + "%";
                default: return Loc.T("step." + o.StepIndex, o.Order.Quantity, productName);
            }
        }

        string OrderDetailsText(GameSession s)
        {
            var o = s.Orders;
            var pay = o.CalculatePayout();
            var logo = s.Balance.FindLogo(o.Order.LogoId);
            string text = "<b>" + Loc.T(o.Order.CustomerKey) + "</b>  ·  " + Loc.T("order.logo", logo != null ? Loc.T(logo.nameKey) : "?") + "\n";
            text += Loc.T("complete.gross", o.Order.Quantity) + ": " + pay.Gross + "   " + Loc.T("complete.material") + ": -" + pay.MaterialCost + "\n";
            if (o.Order.IsSpecial)
                text += Loc.T("special.bonus", o.Order.SpecialBonus) + "  ·  " + Loc.T("special.deadline", o.SpecialExpired ? Loc.T("special.late") : FormatTime(o.SpecialRemaining)) + "\n";
            text += "<b>" + Loc.T("order.net", pay.Net) + "</b>  ·  " + Loc.T("complete.xp", pay.Xp);
            return text;
        }

        public static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
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
            orderDetails.gameObject.SetActive(false);
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
    }
}
