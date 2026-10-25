using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrintshopTycoon.Core;

namespace PrintshopTycoon.Tests
{
    public class CoreGameplayTests
    {
        static GameSession NewSession() => new GameSession(BalanceData.CreateDefault());

        static void CompleteCurrentOrder(GameSession s, PressResult press, out int paid)
        {
            var o = s.Orders;
            Assert.AreEqual(ActionResult.Ok, o.Accept());
            Assert.AreEqual(ActionResult.Ok, o.CollectBlanks(s.Inventory));
            Assert.AreEqual(ActionResult.Ok, o.SelectLogo(o.Order.LogoId));
            Assert.AreEqual(ActionResult.Ok, s.TryBeginPlayerPress(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, s.ResolvePlayerPress(WorkshopLayout.StarterPressId, press));
            Assert.AreEqual(ActionResult.Ok, s.TryCollectFinished(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, s.TryPack(out paid));
        }

        // ---------- Economy ----------

        [Test]
        public void NewGame_StartsWith500Coins()
        {
            Assert.AreEqual(500, NewSession().Wallet.Coins);
        }

        [Test]
        public void Wallet_NeverGoesNegative()
        {
            var w = new Wallet(100);
            Assert.IsFalse(w.TrySpend(101));
            Assert.IsFalse(w.TrySpend(-5));
            Assert.AreEqual(100, w.Coins);
            Assert.IsTrue(w.TrySpend(100));
            Assert.AreEqual(0, w.Coins);
        }

        [Test]
        public void Wallet_RewardIsPaidOnlyOnce()
        {
            var w = new Wallet(0);
            Assert.IsTrue(w.TryClaimReward("order#0", 150));
            Assert.IsFalse(w.TryClaimReward("order#0", 150));
            Assert.AreEqual(150, w.Coins);
        }

        // ---------- First order ----------

        [Test]
        public void FirstOrder_IsFiveShirtsFor150Coins()
        {
            var s = NewSession();
            Assert.AreEqual(5, s.Orders.Order.Quantity);
            Assert.AreEqual(150, s.Orders.Order.Reward);
            Assert.AreEqual(OrderStep.Offered, s.Orders.Step);
        }

        [Test]
        public void FirstOrder_FullFlow_GoodPress_Pays150()
        {
            var s = NewSession();
            CompleteCurrentOrder(s, PressResult.Good, out int paid);
            Assert.AreEqual(150, paid);
            Assert.AreEqual(650, s.Wallet.Coins);
            Assert.AreEqual(15, s.Inventory.BlankStock); // 20 - 5 blanks used
            Assert.AreEqual(1, s.Progression.CompletedOrders);
            Assert.AreEqual(1, s.Orders.Order.Sequence, "next order is offered");
            Assert.AreEqual(OrderStep.Offered, s.Orders.Step);
            Assert.IsNull(s.GetOccupant(WorkshopLayout.StarterPressId), "press released");
        }

        [Test]
        public void FirstOrder_PerfectPress_GivesBonus()
        {
            var s = NewSession();
            CompleteCurrentOrder(s, PressResult.Perfect, out int paid);
            Assert.AreEqual(180, paid); // 150 + 20 %
        }

        [Test]
        public void Order_StepsCannotBeSkipped()
        {
            var s = NewSession();
            Assert.AreEqual(ActionResult.WrongStep, s.Orders.CollectBlanks(s.Inventory));
            s.Orders.Accept();
            Assert.AreEqual(ActionResult.WrongStep, s.Orders.SelectLogo(s.Orders.Order.LogoId));
            Assert.AreEqual(ActionResult.WrongStep, s.TryPack(out _));
            Assert.AreEqual(500, s.Wallet.Coins);
        }

        [Test]
        public void Order_WrongLogoIsRejectedWithoutPenalty()
        {
            var s = NewSession();
            s.Orders.Accept();
            s.Orders.CollectBlanks(s.Inventory);
            Assert.AreEqual(ActionResult.WrongLogo, s.Orders.SelectLogo("logo_fishing"));
            Assert.AreEqual(OrderStep.PrintTransfers, s.Orders.Step);
        }

        [Test]
        public void Order_MissMeansRetry_ThenForcedLowQuality()
        {
            var s = NewSession();
            var o = s.Orders;
            o.Accept(); o.CollectBlanks(s.Inventory); o.SelectLogo(o.Order.LogoId);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            Assert.AreEqual(ActionResult.Retry, s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Miss));
            Assert.AreEqual(ActionResult.Retry, s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Miss));
            Assert.AreEqual(ActionResult.Ok, s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Miss));
            Assert.AreEqual(PressQuality.Low, o.Quality);
            s.TryCollectFinished(WorkshopLayout.StarterPressId);
            s.TryPack(out int paid);
            Assert.AreEqual(120, paid); // 150 - 20 %
        }

        [Test]
        public void Order_NotEnoughBlanks_BlocksAndRestockFixesIt()
        {
            var b = BalanceData.CreateDefault();
            b.startingBlankStock = 2;
            var s = new GameSession(b);
            s.Orders.Accept();
            Assert.AreEqual(ActionResult.NotEnoughStock, s.Orders.CollectBlanks(s.Inventory));
            Assert.AreEqual(ActionResult.Ok, s.TryRestock());
            Assert.AreEqual(460, s.Wallet.Coins);
            Assert.AreEqual(ActionResult.Ok, s.Orders.CollectBlanks(s.Inventory));
        }

        [Test]
        public void Order_CollectFromWrongPressIsRejected()
        {
            var s = NewSession();
            var o = s.Orders;
            o.Accept(); o.CollectBlanks(s.Inventory); o.SelectLogo(o.Order.LogoId);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Good);
            Assert.AreEqual(ActionResult.WrongStation, s.TryCollectFinished("machine#1"));
        }

        // ---------- Minigame ----------

        [Test]
        public void Minigame_JudgesZones()
        {
            Assert.AreEqual(PressResult.Perfect, HeatPressMinigame.Judge(0.50f, 0.5f, 0.13f, 0.04f));
            Assert.AreEqual(PressResult.Good, HeatPressMinigame.Judge(0.60f, 0.5f, 0.13f, 0.04f));
            Assert.AreEqual(PressResult.Miss, HeatPressMinigame.Judge(0.70f, 0.5f, 0.13f, 0.04f));
        }

        [Test]
        public void Minigame_IndicatorPingPongsInsideBar()
        {
            var g = new HeatPressMinigame(BalanceData.CreateDefault(), 42);
            g.Start();
            for (int i = 0; i < 500; i++)
            {
                g.Tick(0.037f);
                Assert.That(g.Position, Is.InRange(0f, 1f));
            }
            g.Tick(5f); // huge frame spike
            Assert.That(g.Position, Is.InRange(0f, 1f));
            Assert.That(g.TargetCenter - g.GoodHalfWidth, Is.GreaterThanOrEqualTo(0f));
            Assert.That(g.TargetCenter + g.GoodHalfWidth, Is.LessThanOrEqualTo(1f));
        }

        // ---------- Building ----------

        [Test]
        public void Purchase_SecondHeatPress_ValidSpot()
        {
            var s = NewSession();
            var area = WorkshopLayout.BuildArea;
            Assert.AreEqual(ActionResult.Ok, s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX, area.centerZ, out var m));
            Assert.AreEqual(200, s.Wallet.Coins);
            Assert.AreEqual(2, s.Machines.Count);
            Assert.AreEqual("machine#1", m.id);
        }

        [Test]
        public void Purchase_RejectsOverlapAndOutsideArea_WithoutCharging()
        {
            var s = NewSession();
            var area = WorkshopLayout.BuildArea;
            s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX, area.centerZ, out _);
            Assert.AreEqual(ActionResult.InvalidPlacement, s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX + 0.5f, area.centerZ, out _));
            Assert.AreEqual(ActionResult.InvalidPlacement, s.TryPurchaseMachine(MachineDefinition.HeatPress, -5f, 0f, out _));
            Assert.AreEqual(200, s.Wallet.Coins);
        }

        [Test]
        public void Purchase_NotEnoughCoins()
        {
            var b = BalanceData.CreateDefault();
            b.startingCoins = 100;
            var s = new GameSession(b);
            var area = WorkshopLayout.BuildArea;
            Assert.AreEqual(ActionResult.NotEnoughCoins, s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX, area.centerZ, out _));
            Assert.AreEqual(1, s.Machines.Count);
            Assert.AreEqual(100, s.Wallet.Coins);
        }

        [Test]
        public void Placement_StarterPressDoesNotBlockBuildAreaCenter()
        {
            var s = NewSession();
            var area = WorkshopLayout.BuildArea;
            Assert.IsTrue(s.IsPlacementValid(MachineDefinition.HeatPress, area.centerX, area.centerZ));
        }

        // ---------- Automation ----------

        static void RunWorker(GameSession s, WorkerTaskMachine w, int maxTicks, Func<bool> until)
        {
            for (int i = 0; i < maxTicks && !until(); i++)
            {
                s.Tick(0.1f);
                if (w.State == WorkerState.MovingToShelf || w.State == WorkerState.MovingToStation || w.State == WorkerState.MovingToPacking)
                    w.NotifyArrived(); // simulate instant walking
            }
        }

        [Test]
        public void Worker_HireAssignAndAutomateOneJob_PaidExactlyOnce()
        {
            var s = NewSession();
            Assert.AreEqual(ActionResult.Ok, s.TryHireWorker(out var w));
            Assert.AreEqual(250, s.Wallet.Coins);
            Assert.AreEqual(ActionResult.Ok, s.AssignWorker(w, WorkshopLayout.StarterPressId));

            int paidEvents = 0;
            s.AutoJobCompleted += (id, coins) => paidEvents++;
            RunWorker(s, w, 2000, () => paidEvents >= 1);

            Assert.AreEqual(1, paidEvents);
            Assert.AreEqual(290, s.Wallet.Coins); // 250 + 40
            Assert.AreEqual(18, s.Inventory.BlankStock); // 2 blanks used
            Assert.AreEqual(WorkerState.Idle, w.State);
            Assert.IsNull(s.GetOccupant(WorkshopLayout.StarterPressId));
        }

        [Test]
        public void Worker_WaitsWhenPlayerUsesPress()
        {
            var s = NewSession();
            s.TryHireWorker(out var w);
            s.AssignWorker(w, WorkshopLayout.StarterPressId);
            Assert.IsTrue(s.TryReserveStation(WorkshopLayout.StarterPressId, GameSession.PlayerOccupant));
            RunWorker(s, w, 400, () => w.State == WorkerState.MovingToStation && w.Block == WorkerBlock.StationBusy);
            Assert.AreEqual(WorkerBlock.StationBusy, w.Block);
            s.ReleaseStation(WorkshopLayout.StarterPressId, GameSession.PlayerOccupant);
            s.Tick(0.1f);
            Assert.AreEqual(WorkerState.Working, w.State);
        }

        [Test]
        public void Worker_NoStock_ReturnsJobToQueue()
        {
            var b = BalanceData.CreateDefault();
            b.startingBlankStock = 0;
            var s = new GameSession(b);
            s.TryHireWorker(out var w);
            s.AssignWorker(w, WorkshopLayout.StarterPressId);
            RunWorker(s, w, 400, () => w.Block == WorkerBlock.NoStock);
            Assert.AreEqual(WorkerBlock.NoStock, w.Block);
            Assert.AreEqual(WorkerState.Idle, w.State);
            Assert.AreEqual(1, s.AutoJobs.Queued);
            Assert.AreEqual(250, s.Wallet.Coins);
        }

        [Test]
        public void Worker_OnlyOnePerPress()
        {
            var s = NewSession();
            s.Wallet.TryClaimReward("test", 1000);
            s.TryHireWorker(out var a);
            s.TryHireWorker(out var b);
            Assert.AreEqual(ActionResult.Ok, s.AssignWorker(a, WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.StationBusy, s.AssignWorker(b, WorkshopLayout.StarterPressId));
        }

        // ---------- Save / load ----------

        [Test]
        public void SaveLoad_RestoresProgressMachinesAndWorkers()
        {
            var s = NewSession();
            CompleteCurrentOrder(s, PressResult.Good, out _);
            var area = WorkshopLayout.BuildArea;
            s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX, area.centerZ, out var press);
            s.Wallet.TryClaimReward("test", 500);
            s.TryHireWorker(out var w);
            s.AssignWorker(w, press.id);
            s.TutorialStep = 3;

            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), s.ToSaveData());

            Assert.AreEqual(s.Wallet.Coins, loaded.Wallet.Coins);
            Assert.AreEqual(1, loaded.Progression.CompletedOrders);
            Assert.AreEqual(s.Progression.TotalXp, loaded.Progression.TotalXp);
            Assert.AreEqual(2, loaded.Machines.Count);
            Assert.AreEqual(area.centerX, loaded.FindMachine(press.id).x);
            Assert.AreEqual(1, loaded.Workers.Count);
            Assert.AreEqual(press.id, loaded.Workers[0].AssignedMachineId);
            Assert.AreEqual(1, loaded.Orders.Order.Sequence);
            Assert.AreEqual(3, loaded.TutorialStep);
            Assert.AreEqual(s.Inventory.BlankStock, loaded.Inventory.BlankStock);
        }

        [Test]
        public void SaveLoad_MidOrderStateIsKept()
        {
            var s = NewSession();
            var o = s.Orders;
            o.Accept(); o.CollectBlanks(s.Inventory); o.SelectLogo(o.Order.LogoId);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Perfect);

            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), s.ToSaveData());
            Assert.AreEqual(OrderStep.CollectFinished, loaded.Orders.Step);
            Assert.AreEqual(GameSession.PlayerOccupant, loaded.GetOccupant(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, loaded.TryCollectFinished(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, loaded.TryPack(out int paid));
            Assert.AreEqual(180, paid);
        }

        [Test]
        public void SaveLoad_PaidOrderCannotBePaidAgain()
        {
            var s = NewSession();
            var o = s.Orders;
            o.Accept(); o.CollectBlanks(s.Inventory); o.SelectLogo(o.Order.LogoId);
            s.TryBeginPlayerPress(WorkshopLayout.StarterPressId);
            s.ResolvePlayerPress(WorkshopLayout.StarterPressId, PressResult.Good);
            s.TryCollectFinished(WorkshopLayout.StarterPressId);
            var beforePack = s.ToSaveData();
            s.TryPack(out _);

            // Simulate a stale save that still says "Pack" but already contains the claimed reward.
            var stale = s.ToSaveData();
            stale.orderSequence = beforePack.orderSequence;
            stale.orderStep = beforePack.orderStep;
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), stale);
            Assert.AreEqual(OrderStep.Offered, loaded.Orders.Step);
            Assert.AreEqual(1, loaded.Orders.Order.Sequence);
            Assert.AreEqual(650, loaded.Wallet.Coins);
        }

        [Test]
        public void SaveLoad_WorkerMidJob_JobAndShirtsAreReturned()
        {
            var s = NewSession();
            s.TryHireWorker(out var w);
            s.AssignWorker(w, WorkshopLayout.StarterPressId);
            RunWorker(s, w, 2000, () => w.State == WorkerState.Working);
            Assert.AreEqual(18, s.Inventory.BlankStock);

            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), s.ToSaveData());
            Assert.AreEqual(20, loaded.Inventory.BlankStock);
            Assert.AreEqual(1, loaded.AutoJobs.Queued);
            Assert.AreEqual(WorkerState.Idle, loaded.Workers[0].State);
            Assert.IsNull(loaded.GetOccupant(WorkshopLayout.StarterPressId));
        }

        [Test]
        public void SaveLoad_RepairsBadValues()
        {
            var d = NewSession().ToSaveData();
            d.coins = -50;
            d.orderStep = 99;
            d.machines.Add(null);
            d.machines.Add(new MachineInstance { id = "machine#7", typeId = "unknown_machine" });
            d.workers.Add(new WorkerSave { id = "worker#1", assignedMachineId = "does_not_exist" });
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), d);
            Assert.AreEqual(0, loaded.Wallet.Coins);
            Assert.AreEqual(OrderStep.Offered, loaded.Orders.Step);
            Assert.AreEqual(1, loaded.Machines.Count);
            Assert.IsNull(loaded.Workers[0].AssignedMachineId);
        }

        // ---------- Save files ----------

        static SaveStore NewStore(string dir) => new SaveStore(Path.Combine(dir, "save.json"),
            d => "coins=" + d.coins + ";version=" + d.version,
            text =>
            {
                var parts = text.Split(';').Select(p => p.Split('=')).ToDictionary(p => p[0], p => int.Parse(p[1]));
                return new SaveData { coins = parts["coins"], version = parts["version"] };
            });

        static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "pt_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public void SaveStore_MissingFile_ReturnsNull()
        {
            Assert.IsNull(NewStore(TempDir()).Load());
        }

        [Test]
        public void SaveStore_CorruptMain_FallsBackToBackup()
        {
            string dir = TempDir();
            var store = NewStore(dir);
            Assert.IsTrue(store.Save(new SaveData { coins = 111 }));
            Assert.IsTrue(store.Save(new SaveData { coins = 222 }));
            File.WriteAllText(store.MainPath, "garbage{{{");
            var loaded = store.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(111, loaded.coins);
            Directory.Delete(dir, true);
        }

        [Test]
        public void SaveStore_RejectsFutureVersion()
        {
            string dir = TempDir();
            var store = NewStore(dir);
            File.WriteAllText(store.MainPath, "coins=5;version=999");
            Assert.IsNull(store.Load());
            Directory.Delete(dir, true);
        }

        // ---------- Localization ----------

        [Test]
        public void Loc_EveryKeyHasDanishAndEnglish()
        {
            Loc.Language = "da";
            Assert.AreEqual("Lagerhylde", Loc.T("station.shelf"));
            Loc.Language = "en";
            Assert.AreEqual("Storage shelf", Loc.T("station.shelf"));
            Loc.Language = "da";
            Assert.AreEqual("+150 mønter!", Loc.T("toast.paid", 150));
        }
    }
}
