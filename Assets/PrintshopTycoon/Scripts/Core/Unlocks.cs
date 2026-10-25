using System.Collections.Generic;
using System.Linq;

namespace PrintshopTycoon.Core
{
    public enum UnlockKind { Product, Upgrade, SpecialOrder }

    public struct UnlockInfo
    {
        public int Level;
        public UnlockKind Kind;
        public string Id;
        public string NameKey;
        public bool ComingSoon;   // product shown but not playable yet
    }

    /// <summary>
    /// Progression milestones derived from data (unlockLevel fields on products, upgrades and
    /// special orders) - nothing about the level curve is hardcoded in gameplay code.
    /// </summary>
    public static class Unlocks
    {
        public static IEnumerable<UnlockInfo> All(BalanceData b)
        {
            foreach (var p in b.products)
                if (p.unlockLevel > 1)
                    yield return new UnlockInfo { Level = p.unlockLevel, Kind = UnlockKind.Product, Id = p.id, NameKey = p.nameKey, ComingSoon = !p.playable };
            foreach (var u in b.upgrades)
                if (u.unlockLevel > 1)
                    yield return new UnlockInfo { Level = u.unlockLevel, Kind = UnlockKind.Upgrade, Id = u.id, NameKey = u.nameKey };
            foreach (var s in b.specialOrders)
                if (s.unlockLevel > 1)
                    yield return new UnlockInfo { Level = s.unlockLevel, Kind = UnlockKind.SpecialOrder, Id = s.id, NameKey = s.customerKey };
        }

        /// <summary>Everything that unlocks exactly at this level.</summary>
        public static List<UnlockInfo> At(BalanceData b, int level) =>
            All(b).Where(u => u.Level == level).OrderBy(u => u.Kind).ToList();

        /// <summary>The next level that unlocks something, or 0 when nothing is left.</summary>
        public static int NextUnlockLevel(BalanceData b, int currentLevel)
        {
            var later = All(b).Where(u => u.Level > currentLevel).ToList();
            return later.Count == 0 ? 0 : later.Min(u => u.Level);
        }

        public static bool IsProductUnlocked(BalanceData b, string productId, int level)
        {
            var p = b.FindProduct(productId);
            return p != null && p.playable && p.unlockLevel <= level;
        }
    }
}
