using NUnit.Framework;
using PrintshopTycoon.Core;

namespace PrintshopTycoon.Tests
{
    /// <summary>
    /// The whole prototype journey on pure game logic, including a JSON round trip of the save:
    /// first order -> buy second press -> hire worker -> automated job -> save -> load.
    /// </summary>
    public class VerticalSliceJourneyTests
    {
        static string ToJson(SaveData d)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.ToJson(d);
#else
            return System.Text.Json.JsonSerializer.Serialize(d, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
#endif
        }

        static SaveData FromJson(string json)
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.JsonUtility.FromJson<SaveData>(json);
#else
            return System.Text.Json.JsonSerializer.Deserialize<SaveData>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
#endif
        }

        [Test]
        public void FullJourney_WithJsonSaveRoundTrip()
        {
            var s = new GameSession(BalanceData.CreateDefault());
            var press1 = WorkshopLayout.StarterPressId;

            // First order: 5 shirts, 150 coins.
            Assert.AreEqual(ActionResult.Ok, s.Orders.Accept());
            Assert.AreEqual(ActionResult.Ok, s.Orders.CollectBlanks(s.Inventory));
            Assert.AreEqual(ActionResult.Ok, s.Orders.SelectLogo("logo_bakery"));
            Assert.AreEqual(ActionResult.Ok, s.TryBeginPlayerPress(press1));
            Assert.AreEqual(ActionResult.Ok, s.ResolvePlayerPress(press1, PressResult.Good));
            Assert.AreEqual(ActionResult.Ok, s.TryCollectFinished(press1));
            Assert.AreEqual(ActionResult.Ok, s.TryPack(out int paid));
            Assert.AreEqual(150, paid);
            Assert.AreEqual(650, s.Wallet.Coins);
            Assert.AreEqual(1, s.Progression.CompletedOrders);

            // Second heat press in the build area.
            var area = WorkshopLayout.BuildArea;
            Assert.AreEqual(ActionResult.Ok, s.TryPurchaseMachine(MachineDefinition.HeatPress, area.centerX, area.MaxZ - 1.2f, out var press2));
            Assert.AreEqual(350, s.Wallet.Coins);

            // Worker on the new press runs one automated job.
            Assert.AreEqual(ActionResult.Ok, s.TryHireWorker(out var worker));
            Assert.AreEqual(ActionResult.Ok, s.AssignWorker(worker, press2.id));
            Assert.AreEqual(100, s.Wallet.Coins);
            int jobs = 0;
            s.AutoJobCompleted += (id, coins) => jobs++;
            for (int i = 0; i < 3000 && jobs == 0; i++)
            {
                s.Tick(0.05f);
                if (worker.Target != WorkerTarget.None) worker.NotifyArrived();
            }
            Assert.AreEqual(1, jobs);
            Assert.AreEqual(140, s.Wallet.Coins);

            // Save -> JSON -> load.
            string json = ToJson(s.ToSaveData());
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), FromJson(json));
            Assert.AreEqual(140, loaded.Wallet.Coins);
            Assert.AreEqual(1, loaded.Progression.CompletedOrders);
            Assert.AreEqual(2, loaded.Machines.Count);
            Assert.AreEqual(press2.id, loaded.Workers[0].AssignedMachineId);
            Assert.IsTrue(loaded.Wallet.IsClaimed("order#0"));
            Assert.IsTrue(loaded.Wallet.IsClaimed("auto#0"));
            Assert.IsFalse(loaded.Wallet.TryClaimReward("order#0", 150), "no double payment after load");
            Assert.AreEqual(s.Inventory.BlankStock, loaded.Inventory.BlankStock);
        }
    }
}
