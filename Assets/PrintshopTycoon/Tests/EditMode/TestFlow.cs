using NUnit.Framework;
using PrintshopTycoon.Core;

namespace PrintshopTycoon.Tests
{
    /// <summary>Shortcuts through the timed production steps.</summary>
    static class TestFlow
    {
        /// <summary>Correct logo, wait for the DTF print, take the transfers.</summary>
        public static void Print(GameSession s)
        {
            Assert.AreEqual(ActionResult.Ok, s.Orders.SelectLogo(s.Orders.Order.LogoId));
            s.Tick(s.Orders.PrintRemaining + 0.05f);
            Assert.AreEqual(ActionResult.Ok, s.TryCollectTransfers());
        }

        /// <summary>Let the closed press finish.</summary>
        public static void FinishPress(GameSession s) => s.Tick(s.Orders.PressRemaining + 0.05f);

        /// <summary>Runs one whole order (regular or special) with the given press result.</summary>
        public static OrderPayout CompleteOrder(GameSession s, PressResult press = PressResult.Good, bool acceptSpecial = false)
        {
            if (acceptSpecial) Assert.AreEqual(ActionResult.Ok, s.Orders.AcceptSpecial());
            else Assert.AreEqual(ActionResult.Ok, s.Orders.Accept());
            if (s.Inventory.Stock(s.Orders.Order.ProductId) < s.Orders.Order.Quantity)
                s.Inventory.Add(s.Orders.Order.ProductId, s.Orders.Order.Quantity);
            Assert.AreEqual(ActionResult.Ok, s.Orders.CollectBlanks(s.Inventory));
            Print(s);
            Assert.AreEqual(ActionResult.Ok, s.TryBeginPlayerPress(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, s.ResolvePlayerPress(WorkshopLayout.StarterPressId, press));
            FinishPress(s);
            Assert.AreEqual(ActionResult.Ok, s.TryCollectFinished(WorkshopLayout.StarterPressId));
            Assert.AreEqual(ActionResult.Ok, s.TryPack(out OrderPayout payout));
            return payout;
        }
    }
}
