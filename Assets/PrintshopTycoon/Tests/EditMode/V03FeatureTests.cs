using System.Linq;
using NUnit.Framework;
using PrintshopTycoon.Core;

namespace PrintshopTycoon.Tests
{
    /// <summary>v0.3: payouts, upgrades, products, special orders, progression and save migration.</summary>
    public class V03FeatureTests
    {
        static GameSession NewSession() => new GameSession(BalanceData.CreateDefault());

        static GameSession AtLevel(int level, int coins = 5000)
        {
            var s = NewSession();
            while (s.Progression.Level < level) s.Progression.AddXp(10);
            s.Wallet.TryClaimReward("test-coins", coins);
            return s;
        }

        // ---------- Reward calculation ----------

        [TestCase(PressResult.Good, 150)]
        [TestCase(PressResult.Perfect, 183)]
        public void Payout_NetIsExactlyWhatTheWalletReceives(PressResult press, int expectedNet)
        {
            var s = NewSession();
            int before = s.Wallet.Coins;
            var payout = TestFlow.CompleteOrder(s, press);
            Assert.AreEqual(expectedNet, payout.Net);
            Assert.AreEqual(before + payout.Net, s.Wallet.Coins);
            Assert.AreEqual(payout.Gross + payout.QualityAdjustment + payout.SpecialBonus - payout.MaterialCost, payout.Net);
            Assert.AreEqual(s.Balance.xpPerOrder, payout.Xp);
        }

        [Test]
        public void Payout_NeverNegative()
        {
            var b = BalanceData.CreateDefault();
            b.FindProduct(ProductDefinition.TShirt).materialCostPerItem = 1000;
            var s = new GameSession(b);
            int before = s.Wallet.Coins;
            var payout = TestFlow.CompleteOrder(s);
            Assert.AreEqual(0, payout.Net);
            Assert.AreEqual(before, s.Wallet.Coins);
        }

        // ---------- Production state transitions ----------

        [Test]
        public void Printer_TakesTimeAndBlocksUntilReady()
        {
            var s = NewSession();
            s.Orders.Accept();
            s.Orders.CollectBlanks(s.Inventory);
            Assert.AreEqual(ActionResult.Ok, s.Orders.SelectLogo(s.Orders.Order.LogoId));
            Assert.IsTrue(s.Orders.IsPrinting);
            Assert.AreEqual(ActionResult.WrongStep, s.Orders.SelectLogo(s.Orders.Order.LogoId), "can't print twice");
            Assert.AreEqual(ActionResult.NotReady, s.TryCollectTransfers());
            s.Tick(s.Balance.printSeconds * 0.5f);
            Assert.That(s.Orders.PrintProgress, Is.InRange(0.4f, 0.6f));
            s.Tick(s.Balance.printSeconds);
            Assert.IsTrue(s.Orders.TransfersReady);
            Assert.AreEqual(ActionResult.Ok, s.TryCollectTransfers());
            Assert.AreEqual(OrderStep.PressShirts, s.Orders.Step);
        }

        [Test]
        public void Press_TimerMustFinishBeforeCollecting()
        {
            var s = NewSession();
            s.Orders.Accept(); s.Orders.CollectBlanks(s.Inventory); TestFlow.Print(s);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Good);
            Assert.AreEqual(ActionResult.NotReady, s.TryCollectFinished(WorkshopLayout.StarterPressId));
            TestFlow.FinishPress(s);
            Assert.AreEqual(ActionResult.Ok, s.TryCollectFinished(WorkshopLayout.StarterPressId));
        }

        // ---------- Upgrades ----------

        [Test]
        public void Upgrades_LockedBelowUnlockLevel()
        {
            var s = AtLevel(1);
            Assert.AreEqual(ActionResult.Locked, s.TryBuyUpgrade(UpgradeDefinition.PressSpeed));
            Assert.AreEqual(0, s.Upgrades.Level(UpgradeDefinition.PressSpeed));
        }

        [Test]
        public void Upgrades_ChargeExactPrice_StopAtMax_NeverNegative()
        {
            var s = AtLevel(3, 100000);
            var def = s.Balance.FindUpgrade(UpgradeDefinition.PrinterSpeed);
            int before = s.Wallet.Coins;
            int cost = s.Upgrades.NextCost(def);
            Assert.AreEqual(ActionResult.Ok, s.TryBuyUpgrade(def.id));
            Assert.AreEqual(before - cost, s.Wallet.Coins);
            for (int i = 1; i < def.maxLevel; i++) Assert.AreEqual(ActionResult.Ok, s.TryBuyUpgrade(def.id));
            Assert.AreEqual(ActionResult.LimitReached, s.TryBuyUpgrade(def.id));
            Assert.AreEqual(def.maxLevel, s.Upgrades.Level(def.id));

            var poor = AtLevel(3, 0);
            while (poor.Wallet.Coins > 0) poor.Wallet.TrySpend(1);
            Assert.AreEqual(ActionResult.NotEnoughCoins, poor.TryBuyUpgrade(UpgradeDefinition.PressSpeed));
            Assert.AreEqual(0, poor.Wallet.Coins);
        }

        [Test]
        public void Upgrades_PreviewValueMatchesRealEffect()
        {
            var s = AtLevel(4, 100000);
            foreach (var def in s.Balance.upgrades)
            {
                float next = s.Upgrades.ValueAt(def.id, 1);
                Assert.AreNotEqual(s.Upgrades.ValueAt(def.id, 0), next, def.id + " has a measurable effect");
            }
            s.TryBuyUpgrade(UpgradeDefinition.PrinterSpeed);
            Assert.AreEqual(s.Upgrades.ValueAt(UpgradeDefinition.PrinterSpeed, 1), s.Upgrades.PrintSeconds, 0.001f);
            s.TryBuyUpgrade(UpgradeDefinition.StorageCapacity);
            Assert.AreEqual(s.Upgrades.ValueAt(UpgradeDefinition.StorageCapacity, 1), s.Inventory.Capacity, 0.001f);
        }

        [Test]
        public void Upgrades_CostGrowsPerLevel()
        {
            var def = BalanceData.CreateDefault().FindUpgrade(UpgradeDefinition.PressSpeed);
            Assert.Less(def.CostForLevel(0), def.CostForLevel(1));
            Assert.Less(def.CostForLevel(1), def.CostForLevel(2));
        }

        [Test]
        public void Upgrade_PrinterSpeed_ShortensPrinting()
        {
            var s = AtLevel(3);
            float before = s.Upgrades.PrintSeconds;
            s.TryBuyUpgrade(UpgradeDefinition.PrinterSpeed);
            Assert.Less(s.Upgrades.PrintSeconds, before);
            s.Orders.Accept(); s.Orders.CollectBlanks(s.Inventory);
            s.Orders.SelectLogo(s.Orders.Order.LogoId);
            Assert.AreEqual(s.Upgrades.PrintSeconds, s.Orders.PrintRemaining, 0.001f);
        }

        [Test]
        public void Upgrade_PressSpeed_ShortensPlayerAndWorkerPressing()
        {
            var s = AtLevel(3);
            float player = s.Upgrades.PlayerPressSeconds, worker = s.Upgrades.WorkerPressSeconds;
            s.TryBuyUpgrade(UpgradeDefinition.PressSpeed);
            Assert.Less(s.Upgrades.PlayerPressSeconds, player);
            Assert.Less(s.Upgrades.WorkerPressSeconds, worker);
        }

        [Test]
        public void Upgrade_PressTiming_WidensZonesAndSlowsIndicator()
        {
            var s = AtLevel(2);
            var before = s.CreateMinigame(1);
            float speed = s.Upgrades.IndicatorSpeed;
            s.TryBuyUpgrade(UpgradeDefinition.PressTiming);
            var after = s.CreateMinigame(1);
            Assert.Greater(after.GoodHalfWidth, before.GoodHalfWidth);
            Assert.Greater(after.PerfectHalfWidth, before.PerfectHalfWidth);
            Assert.Less(s.Upgrades.IndicatorSpeed, speed);
        }

        [Test]
        public void Upgrade_Storage_RaisesCapacityAndRestockAmount()
        {
            var s = AtLevel(4);
            int cap = s.Inventory.Capacity, amount = s.Upgrades.RestockAmount;
            s.TryBuyUpgrade(UpgradeDefinition.StorageCapacity);
            s.TryBuyUpgrade(UpgradeDefinition.StorageRestock);
            Assert.AreEqual(cap + 20, s.Inventory.Capacity);
            Assert.AreEqual(amount + 5, s.Upgrades.RestockAmount);
            Assert.AreEqual(ActionResult.Ok, s.TryRestock(ProductDefinition.TShirt, out int added));
            Assert.AreEqual(amount + 5, added);
        }

        [Test]
        public void Storage_RestockNeverOverfillsOrChargesForMissingSpace()
        {
            var s = NewSession();
            s.Inventory.Add(ProductDefinition.TShirt, s.Inventory.FreeSpace - 3);
            int before = s.Wallet.Coins;
            Assert.AreEqual(ActionResult.Ok, s.TryRestock(ProductDefinition.TShirt, out int added));
            Assert.AreEqual(3, added);
            Assert.AreEqual(before - 3 * 4, s.Wallet.Coins);
            Assert.AreEqual(ActionResult.StorageFull, s.TryRestock(ProductDefinition.TShirt, out _));
            Assert.AreEqual(s.Inventory.Capacity, s.Inventory.Total);
        }

        [Test]
        public void Upgrades_SurviveSaveLoad_AndUnknownOnesAreIgnored()
        {
            var s = AtLevel(3);
            s.TryBuyUpgrade(UpgradeDefinition.PressSpeed);
            s.TryBuyUpgrade(UpgradeDefinition.PressSpeed);
            var data = s.ToSaveData();
            data.upgrades.Add(new UpgradeLevelSave { id = "removed_upgrade", level = 3 });
            data.upgrades.Add(new UpgradeLevelSave { id = UpgradeDefinition.PrinterSpeed, level = 99 });
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), data);
            Assert.AreEqual(2, loaded.Upgrades.Level(UpgradeDefinition.PressSpeed));
            Assert.AreEqual(3, loaded.Upgrades.Level(UpgradeDefinition.PrinterSpeed), "clamped to max level");
            Assert.AreEqual(0, loaded.Upgrades.Level("removed_upgrade"));
        }

        // ---------- Automation after upgrades ----------

        static float TimeForOneAutoJob(GameSession s)
        {
            s.TryHireWorker(out var w);
            s.AssignWorker(w, WorkshopLayout.StarterPressId);
            int jobs = 0; float t = 0f;
            s.AutoJobCompleted += (id, c) => jobs++;
            while (jobs == 0 && t < 200f)
            {
                s.Tick(0.05f); t += 0.05f;
                if (w.Target != WorkerTarget.None) w.NotifyArrived();
            }
            Assert.AreEqual(1, jobs);
            return t;
        }

        [Test]
        public void Automation_StaffUpgradesMakeJobsFaster_StillPaidOnce()
        {
            var plain = AtLevel(4);
            float slow = TimeForOneAutoJob(plain);
            var upgraded = AtLevel(4);
            upgraded.TryBuyUpgrade(UpgradeDefinition.StaffSpeed);
            upgraded.TryBuyUpgrade(UpgradeDefinition.StaffEfficiency);
            int coins = upgraded.Wallet.Coins;
            float fast = TimeForOneAutoJob(upgraded);
            Assert.Less(fast, slow);
            Assert.AreEqual(coins - upgraded.Balance.workerHireCost + upgraded.Balance.autoJobReward, upgraded.Wallet.Coins);
            Assert.AreEqual(upgraded.Balance.maxQueuedAutoJobs + 1, upgraded.AutoJobs.MaxQueued);
        }

        // ---------- Products ----------

        [Test]
        public void Products_SweatshirtLockedAtStart_UnlocksWithLevel()
        {
            var s = NewSession();
            Assert.IsFalse(s.IsProductUnlocked(ProductDefinition.Sweatshirt));
            Assert.AreEqual(ActionResult.Locked, s.TryRestock(ProductDefinition.Sweatshirt, out _));
            var l3 = AtLevel(3);
            Assert.IsTrue(l3.IsProductUnlocked(ProductDefinition.Sweatshirt));
            Assert.AreEqual(ActionResult.Ok, l3.TryRestock(ProductDefinition.Sweatshirt, out int added));
            Assert.AreEqual(added, l3.Inventory.Stock(ProductDefinition.Sweatshirt));
        }

        [Test]
        public void Products_VestIsDefinedButNotPlayableYet()
        {
            var s = AtLevel(10);
            var vest = s.Balance.FindProduct(ProductDefinition.SafetyVest);
            Assert.IsNotNull(vest);
            Assert.IsFalse(s.IsProductUnlocked(vest.id));
        }

        [Test]
        public void Products_RegularOrdersOnlyUseUnlockedProducts()
        {
            var b = BalanceData.CreateDefault();
            for (int seq = 0; seq < 20; seq++)
                Assert.AreEqual(ProductDefinition.TShirt, OrderInstance.CreateRegular(b, seq, 1).ProductId);
            Assert.IsTrue(Enumerable.Range(0, 20).Any(seq => OrderInstance.CreateRegular(b, seq, 3).ProductId == ProductDefinition.Sweatshirt));
        }

        [Test]
        public void Products_SweatshirtOrder_FullFlow_UsesSweatshirtStockAndPrice()
        {
            var b = BalanceData.CreateDefault();
            var s = AtLevel(3);
            int seq = Enumerable.Range(0, 20).First(i => OrderInstance.CreateRegular(b, i, 3).ProductId == ProductDefinition.Sweatshirt);
            // Jump the order book to a sweatshirt order via save/load.
            var data = s.ToSaveData();
            data.orderSequence = seq;
            data.orderTemplateId = OrderInstance.CreateRegular(b, seq, 3).TemplateId;
            s = GameSession.FromSaveData(b, data);
            var order = s.Orders.Order;
            Assert.AreEqual(ProductDefinition.Sweatshirt, order.ProductId);
            s.TryRestock(ProductDefinition.Sweatshirt, out _);
            int tees = s.Inventory.BlankStock;
            int coins = s.Wallet.Coins;
            var payout = TestFlow.CompleteOrder(s);
            var p = b.FindProduct(ProductDefinition.Sweatshirt);
            Assert.AreEqual(order.Quantity * p.pricePerItem, payout.Gross);
            Assert.AreEqual(order.Quantity * p.materialCostPerItem, payout.MaterialCost);
            Assert.AreEqual(coins + payout.Net, s.Wallet.Coins);
            Assert.AreEqual(tees, s.Inventory.BlankStock, "T-shirt stock untouched");
        }

        [Test]
        public void Products_SweatshirtPressesLongerThanTShirt()
        {
            var b = BalanceData.CreateDefault();
            Assert.Greater(b.FindProduct(ProductDefinition.Sweatshirt).pressTimeMultiplier, b.FindProduct(ProductDefinition.TShirt).pressTimeMultiplier);
        }

        // ---------- Special orders ----------

        [Test]
        public void Special_NotOfferedBeforeUnlock_OfferedAfter()
        {
            Assert.IsNull(NewSession().Orders.OfferedSpecial);
            Assert.IsNotNull(AtLevel(2).Orders.OfferedSpecial);
        }

        [Test]
        public void Special_IsOptional_RegularOrderStaysAvailable()
        {
            var s = AtLevel(2);
            var regular = s.Orders.Order;
            Assert.AreEqual(ActionResult.Ok, s.Orders.Accept());
            Assert.AreSame(regular, s.Orders.Order);
            Assert.IsNull(s.Orders.OfferedSpecial, "no special while busy");
        }

        [Test]
        public void Special_OnTime_PaysBonusOnce_ThenReturnsToSameRegularOrder()
        {
            var s = AtLevel(2);
            int regularSeq = s.Orders.Order.Sequence;
            int before = s.Wallet.Coins;
            var payout = TestFlow.CompleteOrder(s, PressResult.Good, acceptSpecial: true);
            var def = s.Balance.specialOrders[0];
            Assert.IsTrue(payout.Special);
            Assert.IsFalse(payout.Late);
            Assert.AreEqual(def.bonus, payout.SpecialBonus);
            Assert.AreEqual(before + payout.Net, s.Wallet.Coins);
            Assert.AreEqual(s.Balance.xpPerOrder + def.bonusXp, payout.Xp);
            Assert.AreEqual(regularSeq, s.Orders.Order.Sequence, "regular order was kept waiting");
            Assert.IsFalse(s.Orders.Order.IsSpecial);
            Assert.IsTrue(s.Wallet.IsClaimed("special#festival#0"));
        }

        [Test]
        public void Special_Late_StillPaysBase_NoBonus()
        {
            var s = AtLevel(2);
            s.Orders.AcceptSpecial();
            s.Tick(s.Orders.Order.TimeLimitSeconds + 1f);
            Assert.IsTrue(s.Orders.SpecialExpired);
            s.Inventory.Add(ProductDefinition.TShirt, 10);
            s.Orders.CollectBlanks(s.Inventory); TestFlow.Print(s);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Good);
            TestFlow.FinishPress(s);
            s.TryCollectFinished(WorkshopLayout.StarterPressId);
            s.TryPack(out OrderPayout payout);
            Assert.IsTrue(payout.Late);
            Assert.AreEqual(0, payout.SpecialBonus);
            Assert.AreEqual(payout.Gross - payout.MaterialCost, payout.Net);
            Assert.Greater(payout.Net, 0, "failure is not punished harshly");
        }

        [Test]
        public void Special_ImpossibleDeadlineIsExtended()
        {
            var b = BalanceData.CreateDefault();
            var def = b.specialOrders[0];
            def.timeLimitSeconds = 5f;
            float limit = OrderInstance.EffectiveTimeLimit(b, def);
            Assert.AreEqual(b.minSecondsOverhead + b.minSecondsPerItem * def.quantity, limit, 0.001f);
            Assert.Greater(limit, 100f);
        }

        [Test]
        public void Special_CooldownBetweenOffers_AndSecondSpecialHasNewRewardId()
        {
            var s = AtLevel(2);
            TestFlow.CompleteOrder(s, PressResult.Good, acceptSpecial: true);
            Assert.IsNull(s.Orders.OfferedSpecial, "cooldown after a special");
            for (int i = 0; i < s.Balance.specialCooldownOrders; i++) TestFlow.CompleteOrder(s);
            Assert.IsNotNull(s.Orders.OfferedSpecial);
            int coins = s.Wallet.Coins;
            var second = TestFlow.CompleteOrder(s, PressResult.Good, acceptSpecial: true);
            Assert.AreEqual(coins + second.Net, s.Wallet.Coins);
            Assert.IsTrue(s.Wallet.IsClaimed("special#festival#1"));
        }

        [Test]
        public void Special_DeadlineAndProgressSurviveSaveLoad()
        {
            var s = AtLevel(2);
            s.Orders.AcceptSpecial();
            s.Tick(30f);
            float remaining = s.Orders.SpecialRemaining;
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), s.ToSaveData());
            Assert.IsTrue(loaded.Orders.Order.IsSpecial);
            Assert.AreEqual(remaining, loaded.Orders.SpecialRemaining, 0.01f);
            Assert.AreEqual(OrderStep.CollectBlanks, loaded.Orders.Step);
        }

        [Test]
        public void Special_PaidThenStaleSave_IsNotPaidAgain()
        {
            var s = AtLevel(2);
            s.Orders.AcceptSpecial();
            s.Inventory.Add(ProductDefinition.TShirt, 10);
            s.Orders.CollectBlanks(s.Inventory); TestFlow.Print(s);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Good);
            TestFlow.FinishPress(s);
            s.TryCollectFinished(WorkshopLayout.StarterPressId);
            var beforePack = s.ToSaveData();
            s.TryPack(out OrderPayout _);
            var stale = s.ToSaveData();
            stale.orderIsSpecial = true; stale.orderTemplateId = beforePack.orderTemplateId;
            stale.orderStep = beforePack.orderStep; stale.specialsCompleted = beforePack.specialsCompleted;
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), stale);
            Assert.IsFalse(loaded.Orders.Order.IsSpecial);
            Assert.AreEqual(OrderStep.Offered, loaded.Orders.Step);
            Assert.AreEqual(s.Wallet.Coins, loaded.Wallet.Coins);
        }

        // ---------- Progression ----------

        [Test]
        public void Progression_LevelUpEventFiresOncePerLevel()
        {
            var p = new Progression(100);
            var levels = new System.Collections.Generic.List<int>();
            p.LeveledUp += l => levels.Add(l);
            p.AddXp(50);
            p.AddXp(350); // 400 xp total -> level 3 (100 + 200)
            CollectionAssert.AreEqual(new[] { 2, 3 }, levels);
            Assert.AreEqual(100, p.XpIntoLevel);
            Assert.AreEqual(300, p.XpForNextLevel);
        }

        [Test]
        public void Unlocks_AreDataDriven_WithNextPreview()
        {
            var b = BalanceData.CreateDefault();
            Assert.AreEqual(2, Unlocks.NextUnlockLevel(b, 1));
            Assert.IsTrue(Unlocks.At(b, 3).Any(u => u.Kind == UnlockKind.Product && u.Id == ProductDefinition.Sweatshirt));
            Assert.IsTrue(Unlocks.At(b, 2).Any(u => u.Kind == UnlockKind.SpecialOrder));
            b.FindProduct(ProductDefinition.Sweatshirt).unlockLevel = 9;
            Assert.IsTrue(Unlocks.At(b, 9).Any(u => u.Id == ProductDefinition.Sweatshirt));
            Assert.AreEqual(0, Unlocks.NextUnlockLevel(b, 99));
        }

        // ---------- Save migration ----------

        [Test]
        public void SaveMigration_V1SaveLoadsIntoV2()
        {
            var v1 = new SaveData
            {
                version = 1, coins = 777, totalXp = 120, completedOrders = 2, blankStock = 13,
                orderSequence = 2, orderStep = (int)OrderStep.CollectBlanks,
                claimedRewards = new System.Collections.Generic.List<string> { "order#0", "order#1" },
                stocks = null, upgrades = null,
            };
            var s = GameSession.FromSaveData(BalanceData.CreateDefault(), v1);
            Assert.AreEqual(777, s.Wallet.Coins);
            Assert.AreEqual(13, s.Inventory.BlankStock);
            Assert.AreEqual("fishing", s.Orders.Order.TemplateId, "v1 order 2 was the fishing order");
            Assert.AreEqual(OrderStep.CollectBlanks, s.Orders.Step);
            Assert.AreEqual(0, s.Upgrades.Level(UpgradeDefinition.PressSpeed));
            var v2 = s.ToSaveData();
            Assert.AreEqual(SaveData.CurrentVersion, v2.version);
            Assert.AreEqual(13, v2.stocks.Single(x => x.productId == ProductDefinition.TShirt).amount);
        }

        [Test]
        public void SaveMigration_V1MidPrintStep_ResumesCleanly()
        {
            var v1 = new SaveData { version = 1, coins = 500, blankStock = 10, orderSequence = 0, orderStep = (int)OrderStep.PrintTransfers };
            var s = GameSession.FromSaveData(BalanceData.CreateDefault(), v1);
            Assert.AreEqual(OrderStep.PrintTransfers, s.Orders.Step);
            Assert.IsFalse(s.Orders.Printing, "player simply picks the logo again");
            TestFlow.Print(s);
            Assert.AreEqual(OrderStep.PressShirts, s.Orders.Step);
        }

        [Test]
        public void SaveMigration_UnknownTemplate_FallsBackToFreshOrder()
        {
            var d = NewSession().ToSaveData();
            d.orderTemplateId = "deleted_template";
            d.orderStep = (int)OrderStep.Pack;
            var s = GameSession.FromSaveData(BalanceData.CreateDefault(), d);
            Assert.AreEqual(OrderStep.Offered, s.Orders.Step);
        }
    }
}
