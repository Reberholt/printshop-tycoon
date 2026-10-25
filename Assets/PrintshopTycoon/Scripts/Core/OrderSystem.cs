using System;
using System.Collections.Generic;
using System.Linq;

namespace PrintshopTycoon.Core
{
    /// <summary>Steps of a hands-on customer order, in the order the player performs them.</summary>
    public enum OrderStep
    {
        Offered = 0,         // waiting on the order board
        CollectBlanks = 1,   // fetch blanks from the shelf
        PrintTransfers = 2,  // pick the logo at the DTF printer, wait for the print, take the transfers
        PressShirts = 3,     // heat press minigame
        CollectFinished = 4, // press finishes, take the shirts off
        Pack = 5,            // pack at the packing table -> payment
    }

    public enum PressQuality { None = 0, Low = 1, Good = 2, Perfect = 3 }

    public enum CarryKind { None, Blanks, BlanksWithTransfers, Finished }

    public enum ActionResult
    {
        Ok,
        WrongStep,
        NotEnoughStock,
        NotEnoughCoins,
        WrongLogo,
        WrongStation,
        StationBusy,
        InvalidPlacement,
        LimitReached,
        Retry,
        AlreadyClaimed,
        Unknown,
        NotReady,
        Locked,
        StorageFull,
    }

    public class OrderInstance
    {
        public int Sequence;          // regular order counter (specials keep the current one)
        public string TemplateId;     // regular template id, or special definition id
        public string CustomerKey;
        public string LogoId;
        public string ProductId;
        public int Quantity;
        public int Gross;             // customer payment before bonuses and material
        public int MaterialCost;      // DTF film + ink, deducted at payout
        public bool IsSpecial;
        public int SpecialBonus;
        public int SpecialBonusXp;
        public float TimeLimitSeconds;
        public int SpecialNumber;     // how many specials were finished before this one

        public string RewardId => IsSpecial ? "special#" + TemplateId + "#" + SpecialNumber : "order#" + Sequence;

        public static OrderInstance FromTemplate(BalanceData b, OrderTemplate t, int sequence)
        {
            var p = b.FindProduct(t.productId) ?? b.FindProduct(ProductDefinition.TShirt);
            return new OrderInstance
            {
                Sequence = sequence,
                TemplateId = t.id,
                CustomerKey = t.customerKey,
                LogoId = t.logoId,
                ProductId = p != null ? p.id : ProductDefinition.TShirt,
                Quantity = t.quantity,
                Gross = t.quantity * (p != null ? p.pricePerItem : 30),
                MaterialCost = t.quantity * (p != null ? p.materialCostPerItem : 0),
            };
        }

        /// <summary>Regular order for a sequence number, only from products the player has unlocked.</summary>
        public static OrderInstance CreateRegular(BalanceData b, int sequence, int playerLevel)
        {
            if (b.orders.Count == 0) throw new InvalidOperationException("No order templates configured.");
            var available = b.orders.Where(t =>
            {
                var p = b.FindProduct(t.productId);
                return p != null && p.playable && p.unlockLevel <= playerLevel;
            }).ToList();
            if (available.Count == 0) available = b.orders.ToList();
            return FromTemplate(b, available[sequence % available.Count], sequence);
        }

        public static OrderInstance CreateSpecial(BalanceData b, SpecialOrderDefinition s, int regularSequence, int specialNumber)
        {
            var p = b.FindProduct(s.productId) ?? b.FindProduct(ProductDefinition.TShirt);
            return new OrderInstance
            {
                Sequence = regularSequence,
                TemplateId = s.id,
                CustomerKey = s.customerKey,
                LogoId = s.logoId,
                ProductId = p != null ? p.id : ProductDefinition.TShirt,
                Quantity = s.quantity,
                Gross = s.quantity * (p != null ? p.pricePerItem : 30),
                MaterialCost = s.quantity * (p != null ? p.materialCostPerItem : 0),
                IsSpecial = true,
                SpecialBonus = s.bonus,
                SpecialBonusXp = s.bonusXp,
                TimeLimitSeconds = EffectiveTimeLimit(b, s),
                SpecialNumber = specialNumber,
            };
        }

        /// <summary>Never shorter than what is realistically doable, so no deadline is impossible.</summary>
        public static float EffectiveTimeLimit(BalanceData b, SpecialOrderDefinition s)
        {
            float minimum = b.minSecondsOverhead + b.minSecondsPerItem * Math.Max(1, s.quantity);
            return Math.Max(s.timeLimitSeconds, minimum);
        }
    }

    /// <summary>Everything that makes up a payout, so the UI can show exactly what was paid.</summary>
    public struct OrderPayout
    {
        public int Gross;
        public int QualityAdjustment;   // + perfect bonus / - low quality
        public int SpecialBonus;
        public int MaterialCost;
        public int Net;                 // exactly what is added to the wallet
        public int Xp;
        public PressQuality Quality;
        public bool Special;
        public bool Late;
        public string CustomerKey;
        public string ProductId;
        public int Quantity;
    }

    /// <summary>Blank stock per product, sharing one shelf capacity.</summary>
    public class Inventory
    {
        readonly Dictionary<string, int> stock = new Dictionary<string, int>();
        readonly Func<int> capacity;

        public event Action Changed;

        public Inventory(int tshirtBlanks, Func<int> capacityProvider = null)
        {
            capacity = capacityProvider ?? (() => int.MaxValue);
            if (tshirtBlanks > 0) stock[ProductDefinition.TShirt] = tshirtBlanks;
        }

        public int Capacity => capacity();
        public int Total => stock.Values.Sum();
        public int FreeSpace => Math.Max(0, Capacity - Total);
        public int Stock(string productId) => productId != null && stock.TryGetValue(productId, out var n) ? n : 0;

        /// <summary>T-shirt blanks (kept for older callers).</summary>
        public int BlankStock => Stock(ProductDefinition.TShirt);

        public bool TryTake(string productId, int amount)
        {
            if (amount <= 0 || Stock(productId) < amount) return false;
            stock[productId] -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TryTake(int amount) => TryTake(ProductDefinition.TShirt, amount);

        public void Add(string productId, int amount)
        {
            if (amount <= 0 || productId == null) return;
            stock[productId] = Stock(productId) + amount;
            Changed?.Invoke();
        }

        public void Add(int amount) => Add(ProductDefinition.TShirt, amount);

        public IEnumerable<KeyValuePair<string, int>> All => stock;

        internal void Restore(IEnumerable<KeyValuePair<string, int>> values)
        {
            stock.Clear();
            if (values != null)
                foreach (var kv in values)
                    if (kv.Key != null && kv.Value > 0) stock[kv.Key] = Stock(kv.Key) + kv.Value;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// State machine for the order Hue works on by hand, including printer/press timers and
    /// optional timed special orders. Pure logic - Unity stations call into it.
    /// </summary>
    public class PlayerOrderFlow
    {
        readonly BalanceData balance;
        readonly UpgradeState upgrades;
        readonly Func<int> playerLevel;

        public OrderInstance Order { get; private set; }
        public OrderStep Step { get; private set; }
        public PressQuality Quality { get; private set; }
        public int MissCount { get; private set; }
        public string PressStationId { get; private set; }

        public bool Printing { get; private set; }
        public float PrintRemaining { get; private set; }
        public float PrintDuration { get; private set; }
        public float PressRemaining { get; private set; }
        public float PressDuration { get; private set; }

        public float SpecialRemaining { get; private set; }
        public bool SpecialExpired { get; private set; }
        public int SpecialsCompleted { get; private set; }
        public int SpecialCooldown { get; private set; }

        public event Action Changed;
        /// <summary>Raised after payment with the exact payout.</summary>
        public event Action<OrderInstance, OrderPayout> Completed;

        public PlayerOrderFlow(BalanceData balance, int sequence, UpgradeState upgrades = null, Func<int> playerLevel = null)
        {
            this.balance = balance;
            this.upgrades = upgrades ?? new UpgradeState(balance);
            this.playerLevel = playerLevel ?? (() => 1);
            StartRegular(sequence);
        }

        public int StepIndex => (int)Step;
        public static int StepCount => 6;

        public bool TransfersReady => Printing && PrintRemaining <= 0f;
        public bool IsPrinting => Printing && PrintRemaining > 0f;
        public bool PressDone => Step == OrderStep.CollectFinished && PressRemaining <= 0f;
        public float PrintProgress => PrintDuration <= 0f ? 1f : 1f - Clamp01(PrintRemaining / PrintDuration);
        public float PressProgress => PressDuration <= 0f ? 1f : 1f - Clamp01(PressRemaining / PressDuration);

        public CarryKind Carry
        {
            get
            {
                switch (Step)
                {
                    case OrderStep.PrintTransfers: return CarryKind.Blanks;
                    case OrderStep.PressShirts: return CarryKind.BlanksWithTransfers;
                    case OrderStep.Pack: return CarryKind.Finished;
                    default: return CarryKind.None;
                }
            }
        }

        public int CarriedShirts => Carry == CarryKind.None ? 0 : Order.Quantity;
        public bool ShirtsOnPress => Step == OrderStep.CollectFinished;

        // ---------------- Special orders ----------------

        /// <summary>The special order the board offers right now (null = none).</summary>
        public SpecialOrderDefinition OfferedSpecial
        {
            get
            {
                if (Step != OrderStep.Offered || Order.IsSpecial || SpecialCooldown > 0) return null;
                int level = playerLevel();
                foreach (var s in balance.specialOrders)
                {
                    var p = balance.FindProduct(s.productId);
                    if (s.unlockLevel <= level && p != null && p.playable && p.unlockLevel <= level) return s;
                }
                return null;
            }
        }

        public ActionResult AcceptSpecial()
        {
            var s = OfferedSpecial;
            if (s == null) return Step == OrderStep.Offered ? ActionResult.Locked : ActionResult.WrongStep;
            Order = OrderInstance.CreateSpecial(balance, s, Order.Sequence, SpecialsCompleted);
            SpecialRemaining = Order.TimeLimitSeconds;
            SpecialExpired = false;
            Step = OrderStep.CollectBlanks;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        // ---------------- Steps ----------------

        public ActionResult Accept()
        {
            if (Step != OrderStep.Offered) return ActionResult.WrongStep;
            Step = OrderStep.CollectBlanks;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CollectBlanks(Inventory inventory)
        {
            if (Step != OrderStep.CollectBlanks) return ActionResult.WrongStep;
            if (!inventory.TryTake(Order.ProductId, Order.Quantity)) return ActionResult.NotEnoughStock;
            Step = OrderStep.PrintTransfers;
            Printing = false;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        /// <summary>Choosing the right logo starts the DTF print (takes PrintSeconds).</summary>
        public ActionResult SelectLogo(string logoId)
        {
            if (Step != OrderStep.PrintTransfers || Printing) return ActionResult.WrongStep;
            if (logoId != Order.LogoId) return ActionResult.WrongLogo;
            Printing = true;
            PrintDuration = upgrades.PrintSeconds;
            PrintRemaining = PrintDuration;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CollectTransfers()
        {
            if (Step != OrderStep.PrintTransfers || !Printing) return ActionResult.WrongStep;
            if (PrintRemaining > 0f) return ActionResult.NotReady;
            Printing = false;
            Step = OrderStep.PressShirts;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CanBeginPress() => Step == OrderStep.PressShirts ? ActionResult.Ok : ActionResult.WrongStep;

        /// <summary>Applies a minigame result. A miss means retry until the forced-accept limit.</summary>
        public ActionResult ResolvePress(string stationId, PressResult result)
        {
            if (Step != OrderStep.PressShirts) return ActionResult.WrongStep;
            if (result == PressResult.Miss)
            {
                MissCount++;
                if (MissCount < balance.missesBeforeForcedAccept)
                {
                    Changed?.Invoke();
                    return ActionResult.Retry;
                }
                Quality = PressQuality.Low;
            }
            else Quality = result == PressResult.Perfect ? PressQuality.Perfect : PressQuality.Good;

            var p = balance.FindProduct(Order.ProductId);
            PressDuration = upgrades.PlayerPressSeconds * (p != null ? p.pressTimeMultiplier : 1f);
            PressRemaining = PressDuration;
            PressStationId = stationId;
            Step = OrderStep.CollectFinished;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CollectFinished(string stationId)
        {
            if (Step != OrderStep.CollectFinished) return ActionResult.WrongStep;
            if (stationId != PressStationId) return ActionResult.WrongStation;
            if (PressRemaining > 0f) return ActionResult.NotReady;
            Step = OrderStep.Pack;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            bool changed = false;
            if (Printing && PrintRemaining > 0f)
            {
                PrintRemaining = Math.Max(0f, PrintRemaining - deltaTime);
                changed |= PrintRemaining <= 0f;
            }
            if (Step == OrderStep.CollectFinished && PressRemaining > 0f)
            {
                PressRemaining = Math.Max(0f, PressRemaining - deltaTime);
                changed |= PressRemaining <= 0f;
            }
            if (Order.IsSpecial && Step != OrderStep.Offered && !SpecialExpired)
            {
                SpecialRemaining = Math.Max(0f, SpecialRemaining - deltaTime);
                if (SpecialRemaining <= 0f) { SpecialExpired = true; changed = true; }
            }
            if (changed) Changed?.Invoke();
        }

        public OrderPayout CalculatePayout()
        {
            var o = Order;
            int quality = 0;
            if (Quality == PressQuality.Perfect) quality = o.Gross * balance.perfectBonusPercent / 100;
            else if (Quality == PressQuality.Low) quality = -(o.Gross * balance.lowQualityPenaltyPercent / 100);
            int special = o.IsSpecial && !SpecialExpired ? o.SpecialBonus : 0;
            int net = Math.Max(0, o.Gross + quality + special - o.MaterialCost);
            int xp = balance.xpPerOrder + (o.IsSpecial && !SpecialExpired ? o.SpecialBonusXp : 0);
            return new OrderPayout
            {
                Gross = o.Gross, QualityAdjustment = quality, SpecialBonus = special, MaterialCost = o.MaterialCost,
                Net = net, Xp = xp, Quality = Quality, Special = o.IsSpecial, Late = o.IsSpecial && SpecialExpired,
                CustomerKey = o.CustomerKey, ProductId = o.ProductId, Quantity = o.Quantity,
            };
        }

        /// <summary>Packs and pays exactly payout.Net. The reward id makes double payment impossible.</summary>
        public ActionResult Pack(Wallet wallet, Progression progression, out OrderPayout payout)
        {
            payout = default;
            if (Step != OrderStep.Pack) return ActionResult.WrongStep;
            var finished = Order;
            var calc = CalculatePayout();
            if (!wallet.TryClaimReward(finished.RewardId, calc.Net))
            {
                AdvanceAfter(finished); // already paid (should never happen) - don't leave the player stuck
                return ActionResult.AlreadyClaimed;
            }
            payout = calc;
            progression.AddXp(calc.Xp);
            progression.RegisterCompletedOrder();
            AdvanceAfter(finished);
            Completed?.Invoke(finished, calc);
            return ActionResult.Ok;
        }

        void AdvanceAfter(OrderInstance finished)
        {
            if (finished.IsSpecial)
            {
                SpecialsCompleted++;
                SpecialCooldown = balance.specialCooldownOrders;
                StartRegular(finished.Sequence);     // the regular order that was waiting
            }
            else
            {
                if (SpecialCooldown > 0) SpecialCooldown--;
                StartRegular(finished.Sequence + 1);
            }
        }

        void StartRegular(int sequence)
        {
            Order = OrderInstance.CreateRegular(balance, sequence, playerLevel());
            ResetProgress();
            Changed?.Invoke();
        }

        void ResetProgress()
        {
            Step = OrderStep.Offered;
            Quality = PressQuality.None;
            MissCount = 0;
            PressStationId = null;
            Printing = false;
            PrintRemaining = PrintDuration = 0f;
            PressRemaining = PressDuration = 0f;
            SpecialRemaining = 0f;
            SpecialExpired = false;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ---------------- Save / load ----------------

        internal void RestoreCounters(int specialsCompleted, int specialCooldown)
        {
            SpecialsCompleted = Math.Max(0, specialsCompleted);
            SpecialCooldown = Math.Max(0, specialCooldown);
        }

        internal void Restore(OrderInstance order, OrderStep step, PressQuality quality, int missCount, string pressStationId,
            bool printing, float printRemaining, float pressRemaining, float specialRemaining, bool specialExpired)
        {
            Order = order;
            ResetProgress();
            if (!Enum.IsDefined(typeof(OrderStep), step)) step = OrderStep.Offered;
            Step = step;
            Quality = quality;
            MissCount = Math.Max(0, missCount);
            PressStationId = pressStationId;
            Printing = printing && step == OrderStep.PrintTransfers;
            PrintDuration = Math.Max(0.01f, upgrades.PrintSeconds);
            PrintRemaining = Printing ? Math.Max(0f, Math.Min(printRemaining, PrintDuration)) : 0f;
            var p = balance.FindProduct(order.ProductId);
            PressDuration = Math.Max(0.01f, upgrades.PlayerPressSeconds * (p != null ? p.pressTimeMultiplier : 1f));
            PressRemaining = step == OrderStep.CollectFinished ? Math.Max(0f, Math.Min(pressRemaining, PressDuration)) : 0f;
            if (order.IsSpecial)
            {
                SpecialRemaining = Math.Max(0f, Math.Min(specialRemaining, order.TimeLimitSeconds));
                SpecialExpired = specialExpired || (step != OrderStep.Offered && SpecialRemaining <= 0f);
            }
            if (Step == OrderStep.CollectFinished && string.IsNullOrEmpty(PressStationId))
                Step = OrderStep.PressShirts; // press unknown -> redo the press rather than lose the shirts
            Changed?.Invoke();
        }

        internal void RestartRegular(int sequence) => StartRegular(sequence);
    }
}
