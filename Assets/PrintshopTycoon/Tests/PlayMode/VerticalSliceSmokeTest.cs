using System.Collections;
using System.IO;
using NUnit.Framework;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace PrintshopTycoon.Tests
{
    /// <summary>
    /// Boots the real game in Play Mode and drives the vertical slice through the actual
    /// stations (no UI taps): order -> press -> pack -> buy press -> hire -> automation -> save.
    /// Run via Window > General > Test Runner > PlayMode.
    /// </summary>
    public class VerticalSliceSmokeTest
    {
        const string TestSave = "printshop_tycoon_playmode_test.json";
        GameManager game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SaveManager.FileNameOverride = TestSave;
            DeleteTestSave();
            // The bootstrapper may already have started a game in the test scene - remove it.
            var existing = Object.FindFirstObjectByType<GameManager>();
            if (existing != null) Object.Destroy(existing.gameObject);
            DestroyWorld();
            yield return null;
            game = new GameObject("PrintshopTycoon").AddComponent<GameManager>();
            yield return null;
            game.UI.CloseModal(); // leave the main menu
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (game != null) Object.Destroy(game.gameObject);
            DestroyWorld();
            Time.timeScale = 1f;
            yield return null;
            DeleteTestSave();
            SaveManager.FileNameOverride = null;
        }

        static void DestroyWorld()
        {
            foreach (var name in new[] { "Workshop", "Dynamic" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.Destroy(go);
            }
        }

        static void DeleteTestSave()
        {
            string p = Path.Combine(Application.persistentDataPath, TestSave);
            foreach (var f in new[] { p, p + ".bak", p + ".tmp" })
                if (File.Exists(f)) File.Delete(f);
        }

        [UnityTest]
        public IEnumerator Boots_InCharacterMode_WithStarterContent()
        {
            Assert.AreEqual(500, game.Session.Wallet.Coins);
            Assert.AreEqual(CameraMode.Character, game.Cameras.Mode);
            Assert.AreEqual(1, game.Presses.Count);
            Assert.IsNotNull(game.UI.Joystick);
            Assert.AreSame(game.Board, game.NextTarget, "the order board is the first goal");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraModes_SwitchWithoutTouchingState()
        {
            game.Session.Orders.Accept();
            game.SetMode(CameraMode.Factory);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(CameraMode.Factory, game.Cameras.Mode);
            game.SetMode(CameraMode.Character);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(OrderStep.CollectBlanks, game.Session.Orders.Step);
            Assert.AreEqual(500, game.Session.Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator FullVerticalSlice()
        {
            var s = game.Session;

            // 1. First order through the real stations (with the timed printer and press).
            Assert.AreEqual(ActionResult.Ok, s.Orders.Accept());
            game.Shelf.Interact();
            Assert.AreEqual(OrderStep.PrintTransfers, s.Orders.Step);
            Assert.AreEqual(ActionResult.Ok, s.Orders.SelectLogo(s.Orders.Order.LogoId));
            yield return new WaitForSeconds(s.Upgrades.PrintSeconds + 0.3f);
            Assert.IsTrue(s.Orders.TransfersReady);
            game.Printer.Interact(); // take the transfers
            Assert.AreEqual(OrderStep.PressShirts, s.Orders.Step);

            var press = game.Presses[0];
            press.Interact(); // opens the minigame
            yield return null;
            Assert.IsTrue(game.UI.MinigameOpen);
            Assert.AreEqual(ActionResult.Ok, press.ApplyPressResult(PressResult.Good));
            game.UI.CloseModal();
            Assert.AreEqual(ActionResult.NotReady, s.TryCollectFinished(press.MachineId));
            yield return new WaitForSeconds(s.Orders.PressRemaining + 0.3f);
            press.Interact(); // collect
            Assert.AreEqual(OrderStep.Pack, s.Orders.Step);

            int before = s.Wallet.Coins;
            game.Packing.Interact();
            Assert.AreEqual(before + 150, s.Wallet.Coins, "net payout credited at once");
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(game.UI.ModalOpen, "completion screen after the packing animation");
            game.UI.CloseModal();
            yield return null;

            // 2. Buy and place a second heat press.
            game.BeginPlacement(s.Balance.FindMachine(MachineDefinition.HeatPress));
            yield return null;
            Assert.IsTrue(game.Build.IsPlacing);
            Assert.IsTrue(game.Build.CurrentValid);
            game.ConfirmPlacement();
            yield return null;
            Assert.AreEqual(2, game.Presses.Count);
            Assert.AreEqual(350, s.Wallet.Coins);

            // 3. Hire a worker - auto-assigned to the new press - and let them work.
            game.HireWorker();
            Assert.AreEqual(1, s.Workers.Count);
            Assert.AreEqual(game.Presses[1].MachineId, s.Workers[0].AssignedMachineId);
            int coinsBefore = s.Wallet.Coins;
            float timeout = Time.realtimeSinceStartup + 90f;
            Time.timeScale = 4f;
            while (s.Wallet.Coins == coinsBefore && Time.realtimeSinceStartup < timeout) yield return null;
            Time.timeScale = 1f;
            Assert.AreEqual(coinsBefore + s.Balance.autoJobReward, s.Wallet.Coins, "worker completed one automated job");

            // 4. Save and load.
            Assert.IsTrue(game.SaveNow());
            var loaded = GameSession.FromSaveData(BalanceData.CreateDefault(), new SaveManager().Load());
            Assert.AreEqual(s.Wallet.Coins, loaded.Wallet.Coins);
            Assert.AreEqual(2, loaded.Machines.Count);
            Assert.AreEqual(1, loaded.Workers.Count);
        }

        [UnityTest]
        public IEnumerator CameraSwitch_DuringPrinting_KeepsProduction()
        {
            var s = game.Session;
            s.Orders.Accept();
            game.Shelf.Interact();
            s.Orders.SelectLogo(s.Orders.Order.LogoId);
            game.SetMode(CameraMode.Factory);
            yield return new WaitForSeconds(s.Upgrades.PrintSeconds * 0.5f);
            game.SetMode(CameraMode.Character);
            yield return new WaitForSeconds(s.Upgrades.PrintSeconds * 0.5f + 0.5f);
            Assert.IsTrue(s.Orders.TransfersReady, "printing continued while the camera switched");
            Assert.AreEqual(OrderStep.PrintTransfers, s.Orders.Step);
        }

        [UnityTest]
        public IEnumerator Upgrade_BoughtInGame_ShortensPrinting()
        {
            var s = game.Session;
            while (s.Progression.Level < 3) s.Progression.AddXp(50);
            yield return null;
            game.UI.CloseModal(); // level-up screen
            s.Wallet.TryClaimReward("playmode-test", 2000);
            float before = s.Upgrades.PrintSeconds;
            game.BuyUpgrade(UpgradeDefinition.PrinterSpeed);
            Assert.Less(s.Upgrades.PrintSeconds, before);
            Assert.AreEqual(1, s.Upgrades.Level(UpgradeDefinition.PrinterSpeed));
        }

        [UnityTest]
        public IEnumerator SpecialOrder_AcceptedInGame_TimerRuns()
        {
            var s = game.Session;
            while (s.Progression.Level < 2) s.Progression.AddXp(50);
            yield return null;
            game.UI.CloseModal();
            Assert.IsNotNull(s.Orders.OfferedSpecial);
            game.AcceptSpecialOrder();
            Assert.IsTrue(s.Orders.Order.IsSpecial);
            float t0 = s.Orders.SpecialRemaining;
            yield return new WaitForSeconds(1f);
            Assert.Less(s.Orders.SpecialRemaining, t0);
        }
    }
}
