using System;

namespace PrintshopTycoon.Core
{
    /// <summary>Steps of a hands-on customer order, in the order the player performs them.</summary>
    public enum OrderStep
    {
        Offered = 0,         // waiting on the order board
        CollectBlanks = 1,   // fetch blank T-shirts from the shelf
        PrintTransfers = 2,  // pick the right logo at the DTF printer
        PressShirts = 3,     // heat press minigame
        CollectFinished = 4, // take the pressed shirts off the press
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
    }

    public class OrderInstance
    {
        public int Sequence;
        public string TemplateId;
        public string CustomerKey;
        public string LogoId;
        public int Quantity;
        public int Reward;

        public string RewardId => "order#" + Sequence;

        public static OrderInstance Create(BalanceData balance, int sequence)
        {
            if (balance.orders.Count == 0) throw new InvalidOperationException("No order templates configured.");
            var t = balance.orders[sequence % balance.orders.Count];
            int cycle = sequence / balance.orders.Count;
            return new OrderInstance
            {
                Sequence = sequence,
                TemplateId = t.id,
                CustomerKey = t.customerKey,
                LogoId = t.logoId,
                Quantity = t.quantity,
                Reward = t.reward + cycle * 20,
            };
        }
    }

    public class Inventory
    {
        public int BlankStock { get; private set; }
        public event Action Changed;

        public Inventory(int blanks) { BlankStock = Math.Max(0, blanks); }

        public bool TryTake(int amount)
        {
            if (amount <= 0 || BlankStock < amount) return false;
            BlankStock -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            BlankStock += amount;
            Changed?.Invoke();
        }

        internal void Restore(int blanks)
        {
            BlankStock = Math.Max(0, blanks);
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// State machine for the order Hue works on by hand. Pure logic - Unity stations call into it.
    /// </summary>
    public class PlayerOrderFlow
    {
        readonly BalanceData balance;

        public OrderInstance Order { get; private set; }
        public OrderStep Step { get; private set; }
        public PressQuality Quality { get; private set; }
        public int MissCount { get; private set; }
        public string PressStationId { get; private set; }

        public event Action Changed;
        /// <summary>Raised after payment: (order, coins paid).</summary>
        public event Action<OrderInstance, int> Completed;

        public PlayerOrderFlow(BalanceData balance, int sequence)
        {
            this.balance = balance;
            StartOrder(sequence);
        }

        public int StepIndex => (int)Step;
        public static int StepCount => 6;

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

        /// <summary>Shirts lying on the press waiting to be collected.</summary>
        public bool ShirtsOnPress => Step == OrderStep.CollectFinished;

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
            if (!inventory.TryTake(Order.Quantity)) return ActionResult.NotEnoughStock;
            Step = OrderStep.PrintTransfers;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult SelectLogo(string logoId)
        {
            if (Step != OrderStep.PrintTransfers) return ActionResult.WrongStep;
            if (logoId != Order.LogoId) return ActionResult.WrongLogo;
            Step = OrderStep.PressShirts;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CanBeginPress()
        {
            return Step == OrderStep.PressShirts ? ActionResult.Ok : ActionResult.WrongStep;
        }

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
            else
            {
                Quality = result == PressResult.Perfect ? PressQuality.Perfect : PressQuality.Good;
            }
            PressStationId = stationId;
            Step = OrderStep.CollectFinished;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public ActionResult CollectFinished(string stationId)
        {
            if (Step != OrderStep.CollectFinished) return ActionResult.WrongStep;
            if (stationId != PressStationId) return ActionResult.WrongStation;
            Step = OrderStep.Pack;
            Changed?.Invoke();
            return ActionResult.Ok;
        }

        public int CalculatePayout()
        {
            int reward = Order.Reward;
            switch (Quality)
            {
                case PressQuality.Perfect: return reward + reward * balance.perfectBonusPercent / 100;
                case PressQuality.Low: return Math.Max(0, reward - reward * balance.lowQualityPenaltyPercent / 100);
                default: return reward;
            }
        }

        /// <summary>Packs and pays. The reward id makes double payment impossible.</summary>
        public ActionResult Pack(Wallet wallet, Progression progression, out int paid)
        {
            paid = 0;
            if (Step != OrderStep.Pack) return ActionResult.WrongStep;
            var finished = Order;
            int payout = CalculatePayout();
            if (!wallet.TryClaimReward(finished.RewardId, payout))
            {
                // Already paid (should never happen) - still advance so the player is not stuck.
                StartOrder(finished.Sequence + 1);
                return ActionResult.AlreadyClaimed;
            }
            paid = payout;
            progression.AddXp(balance.xpPerOrder);
            progression.RegisterCompletedOrder();
            StartOrder(finished.Sequence + 1);
            Completed?.Invoke(finished, payout);
            return ActionResult.Ok;
        }

        void StartOrder(int sequence)
        {
            Order = OrderInstance.Create(balance, sequence);
            Step = OrderStep.Offered;
            Quality = PressQuality.None;
            MissCount = 0;
            PressStationId = null;
            Changed?.Invoke();
        }

        internal void Restore(int sequence, OrderStep step, PressQuality quality, int missCount, string pressStationId)
        {
            Order = OrderInstance.Create(balance, Math.Max(0, sequence));
            if (!Enum.IsDefined(typeof(OrderStep), step)) step = OrderStep.Offered;
            Step = step;
            Quality = quality;
            MissCount = Math.Max(0, missCount);
            PressStationId = pressStationId;
            if (Step == OrderStep.CollectFinished && string.IsNullOrEmpty(PressStationId))
                Step = OrderStep.PressShirts; // press unknown -> redo the press rather than lose the shirts
            Changed?.Invoke();
        }
    }
}
