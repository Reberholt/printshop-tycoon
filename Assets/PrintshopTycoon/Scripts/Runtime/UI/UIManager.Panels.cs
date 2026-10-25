using System;
using System.Linq;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>Modal panels: orders, completion, level-up, progression, upgrades, shop, staff, menus.</summary>
    public partial class UIManager
    {
        RectTransform modal;
        Action onModalClosed;
        HeatPressMinigameView minigame;

        public bool ModalOpen => modal != null;
        public bool MainMenuOpen { get; private set; }
        public bool MinigameOpen => minigame != null;
        public HeatPressMinigameView Minigame => minigame;

        // ------------------------------------------------------------------ Modal infrastructure

        /// <summary>
        /// Opens a card with a title, close button and a vertically scrolling content list.
        /// Height is capped so the card always fits on screen; long content scrolls.
        /// </summary>
        RectTransform OpenModal(string title, float height, Action onClosed = null, bool pauseGame = false, bool closeButton = true)
        {
            onModalClosed = null;
            DestroyModal();
            onModalClosed = onClosed;
            game.SetPaused(pauseGame);
            var dim = UIFactory.Panel(canvasRect, "Modal", new Color(0.05f, 0.05f, 0.08f, 0.62f), false, true);
            dim.rectTransform.Stretch();
            modal = dim.rectTransform;

            float maxHeight = Mathf.Max(800f, canvasRect.rect.height - 260f);
            height = Mathf.Min(height, maxHeight);
            var card = UIFactory.Panel(dim.transform, "Card", Palette.UiPanel);
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1000f, height));
            var titleText = UIFactory.Label(card.transform, "Title", title, 52, Palette.UiDark);
            titleText.rectTransform.TopStrip(28f, 86f, 120f);
            if (closeButton)
            {
                var close = UIFactory.Button(card.transform, "Close", "X", Palette.UiRed, () => CloseModal(), 44);
                ((RectTransform)close.transform).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(96f, 96f));
            }

            var viewport = UIFactory.Rect(card.transform, "Viewport").Stretch(30, 130, 30, 30);
            viewport.gameObject.AddComponent<EmptyGraphic>();
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIFactory.Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var list = UIFactory.VerticalList(content, 18, 6);
            list.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            overlay.SetAsLastSibling();
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
            MainMenuOpen = false;
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

        Text Paragraph(RectTransform content, string text, int size = 38, Color? color = null, float height = 64f, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var t = UIFactory.Label(content, "Text", text, size, color ?? Palette.UiDark, anchor, style);
            t.supportRichText = true;
            UIFactory.Height(t, height);
            return t;
        }

        Button ListButton(RectTransform content, string text, Color color, Action onClick, float height = 130f, int fontSize = 44)
        {
            var b = UIFactory.Button(content, "Button", text, color, onClick, fontSize);
            UIFactory.Height(b, height);
            return b;
        }

        /// <summary>A fixed-height row inside a list, for manual left/right layouts.</summary>
        RectTransform Row(RectTransform content, float height, Color? background = null)
        {
            RectTransform row;
            if (background.HasValue)
            {
                var bg = UIFactory.Panel(content, "Row", background.Value, true, false);
                row = bg.rectTransform;
            }
            else row = UIFactory.Rect(content, "Row");
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            return row;
        }

        Image Art(RectTransform parent, string pose, float height)
        {
            var img = UIFactory.Rect(parent, "Art").gameObject.AddComponent<Image>();
            img.sprite = CharacterArt.Get(pose);
            img.enabled = img.sprite != null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (parent.GetComponent<VerticalLayoutGroup>() != null) UIFactory.Height(img, height);
            return img;
        }

        // ------------------------------------------------------------------ Orders

        public void ShowOrderPanel()
        {
            var s = game.Session;
            var o = s.Orders;
            var content = OpenModal(Loc.T("order.panel_title"), 1500f);

            // Regular order card.
            OrderCard(content, o.Order, o.Step == OrderStep.Offered && !o.Order.IsSpecial ? (Action)(() =>
            {
                if (o.Accept() == ActionResult.Ok) { game.Audio.Play(Sfx.Good); CloseModal(); }
            }) : null, false);

            if (o.Step != OrderStep.Offered)
            {
                Paragraph(content, Loc.T("order.in_progress", o.StepIndex, PlayerOrderFlow.StepCount - 1), 34, Palette.UiDark, 56f);
                Paragraph(content, "► " + StepText(o, s.Balance.FindProduct(o.Order.ProductId)), 36, Palette.UiAccent, 70f);
            }

            // Optional special order offer.
            var special = o.OfferedSpecial;
            if (special != null)
            {
                var preview = OrderInstance.CreateSpecial(s.Balance, special, o.Order.Sequence, o.SpecialsCompleted);
                OrderCard(content, preview, () => game.AcceptSpecialOrder(), true);
                Paragraph(content, Loc.T("special.optional"), 30, new Color(0.4f, 0.4f, 0.45f), 80f);
            }

            if (s.Workers.Count > 0)
                Paragraph(content, Loc.T("hud.autojobs", s.AutoJobs.Queued + s.AutoJobs.InProgress), 32, Palette.UiPurple, 54f);
        }

        void OrderCard(RectTransform content, OrderInstance order, Action accept, bool special)
        {
            var s = game.Session;
            var product = s.Balance.FindProduct(order.ProductId);
            var logo = s.Balance.FindLogo(order.LogoId);
            float h = accept != null ? 420f : 280f;
            var row = Row(content, h, special ? new Color(1f, 0.93f, 0.92f) : new Color(0.96f, 0.95f, 0.92f));

            var head = UIFactory.Label(row, "Head", special ? "♦ " + Loc.T("special.title") : Loc.T("order.regular"), 30, special ? CharacterFactory.BrandRed : Palette.UiGrey, TextAnchor.UpperLeft);
            head.rectTransform.TopStrip(14f, 40f, 24f);
            var customer = UIFactory.Label(row, "Customer", Loc.T(order.CustomerKey), 46, Palette.UiDark, TextAnchor.UpperLeft);
            customer.rectTransform.TopStrip(54f, 60f, 24f);

            var swatch = UIFactory.CircleImage(row, "Logo", logo != null ? MaterialLibrary.Hex(logo.colorHex) : Color.white);
            swatch.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(110f, 110f));
            var inner = UIFactory.Panel(swatch.transform, "Product", product != null ? MaterialLibrary.Hex(product.colorHex) : Color.white, true, false);
            inner.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 46f));

            string productName = product != null ? Loc.T(product.nameKey) : "";
            string line = Loc.T("order.product", order.Quantity, productName) + "  ·  " + Loc.T("order.logo", logo != null ? Loc.T(logo.nameKey) : "?");
            var details = UIFactory.Label(row, "Details", line, 34, Palette.UiDark, TextAnchor.UpperLeft, FontStyle.Normal);
            details.rectTransform.TopStrip(120f, 50f, 24f);

            int net = Mathf.Max(0, order.Gross + order.SpecialBonus - order.MaterialCost);
            string money = Loc.T("order.net", net);
            if (special) money += "  ·  " + Loc.T("special.deadline", FormatTime(order.TimeLimitSeconds)) + "  ·  " + Loc.T("special.bonus", order.SpecialBonus);
            var pay = UIFactory.Label(row, "Pay", money, 34, Palette.UiGreen, TextAnchor.UpperLeft);
            pay.rectTransform.TopStrip(176f, 80f, 24f);

            if (accept != null)
            {
                var b = UIFactory.Button(row, "Accept", special ? Loc.T("special.accept") : Loc.T("order.accept"), special ? CharacterFactory.BrandRed : Palette.UiGreen, accept, 44);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.offsetMin = new Vector2(24f, 20f); rt.offsetMax = new Vector2(-24f, 150f);
            }
        }

        // ------------------------------------------------------------------ Order complete

        /// <summary>Shows exactly what was paid. Called after the wallet was credited with payout.Net.</summary>
        public void ShowOrderComplete(OrderPayout payout, Action onClosed = null)
        {
            var content = OpenModal(Loc.T(payout.Special ? "complete.special" : "complete.title"), 1400f, onClosed, false, false);
            var hero = Row(content, 300f);
            var art = Art(hero, payout.Special ? CharacterArt.DuoWelcome : CharacterArt.HuePoint, 300f);
            art.rectTransform.Stretch();

            Paragraph(content, Loc.T("complete.net"), 38, Palette.UiGrey, 54f);
            var big = Paragraph(content, "+" + FormatCoins(payout.Net), 110, Palette.UiGreen, 140f, TextAnchor.MiddleCenter, FontStyle.Bold);
            var outline = big.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.15f);
            outline.effectDistance = new Vector2(3f, -3f);
            StartCoroutine(PopIn(big.transform));

            BreakdownLine(content, Loc.T("complete.gross", payout.Quantity), payout.Gross, false);
            if (payout.QualityAdjustment != 0)
                BreakdownLine(content, Loc.T(payout.Quality == PressQuality.Perfect ? "complete.quality_perfect" : "complete.quality_low"), payout.QualityAdjustment, false);
            if (payout.SpecialBonus != 0) BreakdownLine(content, Loc.T("complete.special_bonus"), payout.SpecialBonus, false);
            BreakdownLine(content, Loc.T("complete.material"), -payout.MaterialCost, false);
            BreakdownLine(content, Loc.T("complete.net"), payout.Net, true);
            if (payout.Late) Paragraph(content, Loc.T("complete.late"), 30, Palette.UiRed, 50f);
            Paragraph(content, Loc.T("complete.xp", payout.Xp), 40, Palette.UiPurple, 64f, TextAnchor.MiddleCenter, FontStyle.Bold);
            ListButton(content, Loc.T("complete.continue"), Palette.UiGreen, () => CloseModal(), 150f, 52);
        }

        void BreakdownLine(RectTransform content, string label, int amount, bool total)
        {
            var row = Row(content, total ? 64f : 52f, total ? new Color(0.93f, 0.97f, 0.93f) : (Color?)null);
            var l = UIFactory.Label(row, "Label", label, total ? 36 : 32, Palette.UiDark, TextAnchor.MiddleLeft, total ? FontStyle.Bold : FontStyle.Normal);
            l.rectTransform.Stretch(30, 0, 260, 0);
            string sign = amount > 0 && !total ? "+" : (amount < 0 ? "-" : "");
            var v = UIFactory.Label(row, "Value", sign + FormatCoins(Mathf.Abs(amount)), total ? 36 : 32,
                amount < 0 ? Palette.UiRed : Palette.UiDark, TextAnchor.MiddleRight, total ? FontStyle.Bold : FontStyle.Normal);
            v.rectTransform.Stretch(600, 0, 30, 0);
        }

        System.Collections.IEnumerator PopIn(Transform t)
        {
            for (float a = 0f; a < 0.35f; a += Time.unscaledDeltaTime)
            {
                float k = a / 0.35f;
                t.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0.3f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ Progression

        public void ShowLevelUp(int level)
        {
            var b = game.Session.Balance;
            var content = OpenModal(Loc.T("level.title", level), 1300f, () => game.UI.ShowSilke(Loc.T("silke.levelup"), CharacterArt.SilkeMonitor), false, false);
            var hero = Row(content, 320f);
            Art(hero, CharacterArt.DuoWelcome, 320f).rectTransform.Stretch();
            var unlocks = Unlocks.At(b, level);
            if (unlocks.Count == 0) Paragraph(content, Loc.T("level.nothing"), 36, Palette.UiDark, 90f);
            else
            {
                Paragraph(content, Loc.T("level.unlocked"), 40, Palette.UiDark, 64f, TextAnchor.MiddleCenter, FontStyle.Bold);
                foreach (var u in unlocks) UnlockRow(content, u, true);
            }
            ListButton(content, Loc.T("level.ok"), Palette.UiGreen, () => CloseModal(), 150f, 52);
        }

        void UnlockRow(RectTransform content, UnlockInfo u, bool unlocked)
        {
            string kind = Loc.T(u.Kind == UnlockKind.Product ? "kind.product" : (u.Kind == UnlockKind.Upgrade ? "kind.upgrade" : "kind.special"));
            string name = Loc.T(u.NameKey) + (u.ComingSoon ? " " + Loc.T("level.coming_soon") : "");
            var row = Row(content, 76f, unlocked ? new Color(0.93f, 0.97f, 0.93f) : new Color(0.95f, 0.95f, 0.95f));
            var left = UIFactory.Label(row, "Kind", kind, 28, Palette.UiGrey, TextAnchor.MiddleLeft, FontStyle.Normal);
            left.rectTransform.Stretch(24, 0, 700, 0);
            var right = UIFactory.Label(row, "Name", name, 34, unlocked ? Palette.UiDark : Palette.UiGrey, TextAnchor.MiddleLeft);
            right.rectTransform.Stretch(240, 0, 140, 0);
            var lvl = UIFactory.Label(row, "Level", unlocked ? "●" : Loc.T("hud.level", u.Level), 30, unlocked ? Palette.UiGreen : Palette.UiGrey, TextAnchor.MiddleRight);
            lvl.rectTransform.Stretch(700, 0, 24, 0);
        }

        public void ShowProgress()
        {
            var s = game.Session;
            var b = s.Balance;
            int level = s.Progression.Level;
            var content = OpenModal(Loc.T("progress.title"), 1500f);
            Paragraph(content, Loc.T("hud.level", level), 64, Palette.UiDark, 90f, TextAnchor.MiddleCenter, FontStyle.Bold);
            var barRow = Row(content, 46f);
            var bg = UIFactory.Panel(barRow, "Bar", new Color(0f, 0f, 0f, 0.1f), true, false);
            bg.rectTransform.Stretch(60, 6, 60, 6);
            var fill = UIFactory.Panel(bg.transform, "Fill", Palette.UiGreen, true, false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(s.Progression.LevelProgress, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Paragraph(content, Loc.T("progress.xp", s.Progression.XpIntoLevel, s.Progression.XpForNextLevel), 32, Palette.UiGrey, 52f);

            int next = Unlocks.NextUnlockLevel(b, level);
            if (next == 0) Paragraph(content, Loc.T("progress.all"), 38, Palette.UiGreen, 70f);
            else
            {
                Paragraph(content, Loc.T("progress.next", next), 40, Palette.UiAccent, 64f, TextAnchor.MiddleCenter, FontStyle.Bold);
                foreach (var u in Unlocks.At(b, next)) UnlockRow(content, u, false);
                Paragraph(content, Loc.T("progress.goal", next), 30, Palette.UiGrey, 60f);
            }
            Paragraph(content, Loc.T("level.unlocked"), 34, Palette.UiDark, 60f, TextAnchor.MiddleCenter, FontStyle.Bold);
            foreach (var u in Unlocks.All(b).Where(x => x.Level <= level).OrderBy(x => x.Level)) UnlockRow(content, u, true);
            foreach (var u in Unlocks.All(b).Where(x => x.Level > next && next != 0).OrderBy(x => x.Level)) UnlockRow(content, u, false);
        }

        // ------------------------------------------------------------------ Upgrades

        public void ShowUpgrades()
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("upgrades.title"), 1600f);
            foreach (var group in new[] { "press", "printer", "storage", "staff" })
            {
                var defs = s.Balance.upgrades.Where(u => u.target == group).ToList();
                if (defs.Count == 0) continue;
                Paragraph(content, Loc.T("upgrades.group." + group), 34, Palette.UiGrey, 52f, TextAnchor.MiddleLeft, FontStyle.Bold);
                foreach (var def in defs) UpgradeRow(content, def);
            }
        }

        void UpgradeRow(RectTransform content, UpgradeDefinition def)
        {
            var s = game.Session;
            var up = s.Upgrades;
            int level = up.Level(def.id);
            bool unlocked = up.IsUnlocked(def, s.Progression.Level);
            bool maxed = up.IsMaxed(def);
            var row = Row(content, 196f, unlocked ? new Color(0.96f, 0.95f, 0.92f) : new Color(0.93f, 0.93f, 0.93f));

            var name = UIFactory.Label(row, "Name", Loc.T(def.nameKey), 40, unlocked ? Palette.UiDark : Palette.UiGrey, TextAnchor.UpperLeft);
            name.rectTransform.Stretch(24, 18, 330, 120);

            // Level pips.
            for (int i = 0; i < def.maxLevel; i++)
            {
                var pip = UIFactory.CircleImage(row, "Pip", i < level ? Palette.UiCoin : new Color(0f, 0f, 0f, 0.15f));
                pip.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f + i * 38f, -86f), new Vector2(30f, 30f));
            }

            string effect;
            float now = up.ValueAt(def.id, level), next = up.ValueAt(def.id, Mathf.Min(def.maxLevel, level + 1));
            effect = Loc.T("upg.effect." + def.id, FormatValue(now), FormatValue(next));
            var eff = UIFactory.Label(row, "Effect", effect, 30, new Color(0.3f, 0.3f, 0.35f), TextAnchor.UpperLeft, FontStyle.Normal);
            eff.rectTransform.Stretch(24, 128, 330, 14);

            string label;
            Color color;
            bool canBuy = false;
            if (!unlocked) { label = Loc.T("upgrades.locked", def.unlockLevel); color = Palette.UiGrey; }
            else if (maxed) { label = Loc.T("upgrades.max"); color = Palette.UiGreen; }
            else
            {
                int cost = up.NextCost(def);
                label = Loc.T("upgrades.buy", FormatCoins(cost));
                canBuy = s.Wallet.CanAfford(cost);
                color = Palette.UiAccent;
            }
            var id = def.id;
            var b = UIFactory.Button(row, "Buy", label, color, () => { game.BuyUpgrade(id); ShowUpgrades(); }, 36);
            ((RectTransform)b.transform).Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(290f, 130f));
            b.interactable = canBuy;
        }

        static string FormatValue(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");

        // ------------------------------------------------------------------ Logo / minigame

        public void ShowLogoSelect(Action<string> onPicked)
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("logo.select_title"), 1500f);
            var heroRow = Row(content, 230f);
            Art(heroRow, CharacterArt.HueTransfer, 230f).rectTransform.Stretch();
            var o = s.Orders.Order;
            Paragraph(content, Loc.T("order.title", Loc.T(o.CustomerKey), o.Quantity), 34, Palette.UiDark, 60f);
            foreach (var logo in s.Balance.logos)
            {
                var id = logo.id;
                var b = ListButton(content, Loc.T(logo.nameKey), MaterialLibrary.Hex(logo.colorHex), () =>
                {
                    CloseModal();
                    onPicked(id);
                }, 150f, 48);
            }
        }

        public void ShowHeatPressMinigame(HeatPressStation station)
        {
            var content = OpenModal(Loc.T("machine.heat_press"), 1000f, null);
            minigame = content.gameObject.AddComponent<HeatPressMinigameView>();
            minigame.Build(game, station, content, () => { minigame = null; CloseModal(); });
        }

        // ------------------------------------------------------------------ Shop / staff

        public void ShowShop()
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("shop.title"), 1500f);
            foreach (var def in s.Balance.machines)
            {
                var d = def;
                Paragraph(content, Loc.T(def.nameKey), 44, Palette.UiDark, 70f, TextAnchor.MiddleCenter, FontStyle.Bold);
                var b = ListButton(content, Loc.T("shop.buy", def.price), Palette.UiGreen, () =>
                {
                    CloseModal();
                    game.BeginPlacement(d);
                }, 140f);
                b.interactable = s.Wallet.CanAfford(def.price);
            }
            Paragraph(content, Loc.T("shop.soon"), 30, Palette.UiGrey, 56f);

            Paragraph(content, Loc.T("shop.restock_title") + "  ·  " + Loc.T("hud.stock_capacity", s.Inventory.Total, s.Inventory.Capacity), 36, Palette.UiDark, 70f, TextAnchor.MiddleCenter, FontStyle.Bold);
            foreach (var p in s.Balance.products)
            {
                string name = Loc.T(p.nameKey);
                if (!p.playable) { Paragraph(content, Loc.T("shop.coming_soon", name), 32, Palette.UiGrey, 60f); continue; }
                if (!s.IsProductUnlocked(p.id)) { Paragraph(content, Loc.T("shop.locked", name, p.unlockLevel), 32, Palette.UiGrey, 60f); continue; }
                int amount = s.RestockAmountFor(p.id);
                int cost = s.RestockCostFor(p.id);
                var id = p.id;
                string label = amount > 0
                    ? Loc.T("act.restock_product", amount, name, cost) + "   (" + s.Inventory.Stock(p.id) + ")"
                    : Loc.T("act.storage_full");
                var b = ListButton(content, label, Palette.UiBlue, () => { game.RestockProduct(id); ShowShop(); }, 120f, 38);
                b.interactable = amount > 0 && s.Wallet.CanAfford(cost);
            }
        }

        public void ShowStaff()
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("staff.title"), 1400f);
            Paragraph(content, Loc.T("staff.explain"), 34, Palette.UiDark, 110f);
            var hire = ListButton(content, Loc.T("staff.hire", s.Balance.workerHireCost), Palette.UiPurple, () =>
            {
                game.HireWorker();
                ShowStaff();
            }, 140f);
            hire.interactable = s.CanHireWorker && s.Wallet.CanAfford(s.Balance.workerHireCost);
            Paragraph(content, Loc.T("hud.autojobs", s.AutoJobs.Queued + s.AutoJobs.InProgress), 32, Palette.UiPurple, 54f);

            if (s.Workers.Count == 0) Paragraph(content, Loc.T("staff.none"), 38, Palette.UiGrey, 80f);
            foreach (var w in s.Workers)
            {
                var worker = w;
                var press = game.FindPress(w.AssignedMachineId);
                string where = press != null ? Loc.T("staff.at", press.DisplayName + " " + GameManager.PressNumber(press.MachineId)) : Loc.T("staff.unassigned");
                var view = game.FindWorkerView(w);
                Paragraph(content, "<b>" + Loc.T("staff.worker", GameManager.WorkerNumber(w.Id)) + "</b>  –  " + where + (view != null ? "\n" + view.StatusText : ""), 34, Palette.UiDark, 96f);
                ListButton(content, Loc.T("staff.assign"), Palette.UiBlue, () =>
                {
                    var next = s.NextFreePressFor(worker);
                    if (next != null && s.AssignWorker(worker, next) == ActionResult.Ok) game.Audio.Play(Sfx.Click);
                    else { game.Audio.Play(Sfx.Error); Toast(Loc.T("toast.station_busy"), true); }
                    ShowStaff();
                }, 104f, 38);
            }
        }

        // ------------------------------------------------------------------ Menus

        public void ShowSettings(Action onClosed = null)
        {
            var s = game.Session;
            var content = OpenModal(Loc.T("menu.settings"), 1000f, onClosed, true);
            ListButton(content, Loc.T("settings.sound", Loc.T(s.SoundOn ? "settings.on" : "settings.off")), Palette.UiBlue, () =>
            {
                game.SetSound(!s.SoundOn);
                ShowSettings(onClosed);
            });
            ListButton(content, Loc.T("settings.language"), Palette.UiPurple, () =>
            {
                game.SetLanguage(Loc.Language == "da" ? "en" : "da", onClosed);
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
            Paragraph(content, Loc.T("menu.version"), 28, Palette.UiGrey, 56f);
        }

        public void ShowPause()
        {
            if (ModalOpen && minigame != null) return;
            var content = OpenModal(Loc.T("menu.pause"), 1000f, null, true);
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
            var bg = UIFactory.Panel(canvasRect, "MainMenu", CharacterFactory.BrandRed, false, true);
            bg.rectTransform.Stretch();
            modal = bg.rectTransform;
            MainMenuOpen = true;
            game.SetPaused(true);
            game.OnModalChanged(true);

            var inner = UIFactory.Rect(bg.transform, "Safe").Stretch();
            inner.gameObject.AddComponent<SafeArea>().Apply();
            var title = UIFactory.Label(inner, "Title", Loc.T("game.title"), 110, Color.white);
            title.rectTransform.TopStrip(220f, 240f, 40f);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.UiDark; outline.effectDistance = new Vector2(6f, -6f);
            var sub = UIFactory.Label(inner, "Subtitle", Loc.T("menu.subtitle"), 46, Color.white);
            sub.rectTransform.TopStrip(470f, 80f, 60f);

            // Hue & Silke welcome the player (official art).
            var duo = UIFactory.Rect(inner, "HueAndSilke").gameObject.AddComponent<Image>();
            duo.sprite = CharacterArt.Get(CharacterArt.DuoWelcome);
            duo.enabled = duo.sprite != null;
            duo.preserveAspect = true;
            duo.raycastTarget = false;
            duo.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 100f), new Vector2(620f, 520f));

            var buttons = UIFactory.Rect(inner, "Buttons");
            buttons.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(760f, 380f));
            UIFactory.VerticalList(buttons, 28, 0);
            bool hasProgress = game.Session.Progression.CompletedOrders > 0 || game.Session.Orders.Step != OrderStep.Offered;
            UIFactory.Height(UIFactory.Button(buttons, "Play", Loc.T(hasProgress ? "menu.continue" : "menu.play"), Palette.UiGreen, () => CloseModal(), 64), 180f);
            UIFactory.Height(UIFactory.Button(buttons, "Settings", Loc.T("menu.settings"), Palette.UiDark, () => ShowSettings(() => ShowMainMenu()), 50), 140f);
            var footer = UIFactory.Label(inner, "Footer", Loc.T("menu.version") + "  ·  Tex-Tryk, Nibe", 30, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Normal);
            footer.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1000f, 60f));
            overlay.SetAsLastSibling();
        }
    }
}
