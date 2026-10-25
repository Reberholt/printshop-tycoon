using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// Integer coin balance. Never goes negative, and every reward carries a unique id
    /// so the same order can never be paid twice (also not after a save/load).
    /// </summary>
    public class Wallet
    {
        readonly HashSet<string> claimedRewards = new HashSet<string>();

        public int Coins { get; private set; }
        public event Action<int> CoinsChanged;

        public Wallet(int startingCoins)
        {
            Coins = Math.Max(0, startingCoins);
        }

        public bool CanAfford(int amount) => amount >= 0 && Coins >= amount;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Coins < amount) return false;
            Coins -= amount;
            CoinsChanged?.Invoke(Coins);
            return true;
        }

        /// <summary>Pays a reward once. Returns false if the id was already claimed.</summary>
        public bool TryClaimReward(string rewardId, int amount)
        {
            if (string.IsNullOrEmpty(rewardId) || amount < 0) return false;
            if (!claimedRewards.Add(rewardId)) return false;
            Coins = checked(Coins + amount);
            CoinsChanged?.Invoke(Coins);
            return true;
        }

        public bool IsClaimed(string rewardId) => claimedRewards.Contains(rewardId);

        public IEnumerable<string> ClaimedRewards => claimedRewards;

        internal void Restore(int coins, IEnumerable<string> claimed)
        {
            Coins = Math.Max(0, coins);
            claimedRewards.Clear();
            if (claimed != null)
                foreach (var id in claimed)
                    if (!string.IsNullOrEmpty(id)) claimedRewards.Add(id);
            CoinsChanged?.Invoke(Coins);
        }
    }

    public class Progression
    {
        readonly int xpPerLevel;

        public int TotalXp { get; private set; }
        public int CompletedOrders { get; private set; }
        public event Action Changed;

        public Progression(int xpPerLevel)
        {
            this.xpPerLevel = Math.Max(1, xpPerLevel);
        }

        public int Level
        {
            get
            {
                int level = 1, remaining = TotalXp;
                while (remaining >= xpPerLevel * level) { remaining -= xpPerLevel * level; level++; }
                return level;
            }
        }

        /// <summary>0..1 progress towards next level.</summary>
        public float LevelProgress
        {
            get
            {
                int level = 1, remaining = TotalXp;
                while (remaining >= xpPerLevel * level) { remaining -= xpPerLevel * level; level++; }
                return remaining / (float)(xpPerLevel * level);
            }
        }

        public void AddXp(int xp)
        {
            if (xp <= 0) return;
            TotalXp += xp;
            Changed?.Invoke();
        }

        public void RegisterCompletedOrder()
        {
            CompletedOrders++;
            Changed?.Invoke();
        }

        internal void Restore(int xp, int completedOrders)
        {
            TotalXp = Math.Max(0, xp);
            CompletedOrders = Math.Max(0, completedOrders);
            Changed?.Invoke();
        }
    }
}
