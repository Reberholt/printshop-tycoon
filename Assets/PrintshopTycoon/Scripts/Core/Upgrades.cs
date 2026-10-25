using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// A buyable upgrade. The id selects which game value it changes (see UpgradeState);
    /// how much it changes per level, its price and its unlock level come from data.
    /// </summary>
    [Serializable]
    public class UpgradeDefinition
    {
        public const string PressSpeed = "press_speed";           // press time -x% per level
        public const string PressTiming = "press_timing";         // wider minigame zones (+x per level)
        public const string PrinterSpeed = "printer_speed";       // print time -x% per level
        public const string StorageCapacity = "storage_capacity"; // +x blanks shelf capacity
        public const string StorageRestock = "storage_restock";   // +x blanks per restock tap
        public const string StaffSpeed = "staff_speed";           // workers walk/work x% faster
        public const string StaffEfficiency = "staff_efficiency"; // small orders arrive x% sooner

        public string id;
        public string nameKey;
        public string target;          // press / printer / storage / staff (UI grouping)
        public int unlockLevel = 1;
        public int maxLevel = 3;
        public int baseCost = 100;
        public int costGrowthPercent = 60;
        public float effectPerLevel = 0.1f;

        public int CostForLevel(int currentLevel)
        {
            double cost = baseCost * Math.Pow(1.0 + costGrowthPercent / 100.0, currentLevel);
            return (int)Math.Round(cost / 10.0) * 10;
        }
    }

    /// <summary>
    /// Purchased upgrade levels and the resulting gameplay numbers. Every getter here is used
    /// by the simulation, so each upgrade has a measurable effect.
    /// </summary>
    public class UpgradeState
    {
        readonly BalanceData balance;
        readonly Dictionary<string, int> levels = new Dictionary<string, int>();

        public event Action<string, int> Upgraded;

        public UpgradeState(BalanceData balance) { this.balance = balance; }

        public int Level(string id) => id != null && levels.TryGetValue(id, out var l) ? l : 0;

        float Effect(string id)
        {
            var def = balance.FindUpgrade(id);
            return def == null ? 0f : def.effectPerLevel * Level(id);
        }

        static float Mult(float reduction) => Math.Max(0.35f, 1f - reduction);

        // ---- Effects ----
        public float PrintSeconds => balance.printSeconds * Mult(Effect(UpgradeDefinition.PrinterSpeed));
        public float PressTimeMultiplier => Mult(Effect(UpgradeDefinition.PressSpeed));
        public float PlayerPressSeconds => balance.playerPressSeconds * PressTimeMultiplier;
        public float GoodZoneWidth => Math.Min(0.6f, balance.goodZoneWidth + Effect(UpgradeDefinition.PressTiming));
        public float PerfectZoneWidth => Math.Min(0.25f, balance.perfectZoneWidth + Effect(UpgradeDefinition.PressTiming) * 0.35f);
        public float IndicatorSpeed => balance.indicatorSpeed * Mult(Effect(UpgradeDefinition.PressTiming) * 1.5f);
        public int StorageCapacity => balance.storageCapacity + (int)Math.Round(Effect(UpgradeDefinition.StorageCapacity));
        public int RestockAmount => balance.restockAmount + (int)Math.Round(Effect(UpgradeDefinition.StorageRestock));
        public float WorkerMoveSpeed => balance.workerMoveSpeed * (1f + Effect(UpgradeDefinition.StaffSpeed));
        public float WorkerWorkMultiplier => Mult(Effect(UpgradeDefinition.StaffSpeed)) * PressTimeMultiplier;
        public float WorkerPressSeconds => balance.workerPressSeconds * WorkerWorkMultiplier + PrintSeconds * 0.5f;
        public float WorkerPackSeconds => balance.workerPackSeconds * Mult(Effect(UpgradeDefinition.StaffSpeed));
        public float AutoJobIntervalSeconds => balance.autoJobIntervalSeconds * Mult(Effect(UpgradeDefinition.StaffEfficiency));
        public int MaxQueuedAutoJobs => balance.maxQueuedAutoJobs + Level(UpgradeDefinition.StaffEfficiency);

        /// <summary>
        /// The headline number an upgrade changes, at a given upgrade level (for "now -> next"
        /// previews in the UI). Unit: seconds, blanks, % or zone width in %.
        /// </summary>
        public float ValueAt(string id, int level)
        {
            var def = balance.FindUpgrade(id);
            float e = def == null ? 0f : def.effectPerLevel * level;
            switch (id)
            {
                case UpgradeDefinition.PressSpeed: return balance.playerPressSeconds * Mult(e);
                case UpgradeDefinition.PressTiming: return Math.Min(0.6f, balance.goodZoneWidth + e) * 100f;
                case UpgradeDefinition.PrinterSpeed: return balance.printSeconds * Mult(e);
                case UpgradeDefinition.StorageCapacity: return balance.storageCapacity + (float)Math.Round(e);
                case UpgradeDefinition.StorageRestock: return balance.restockAmount + (float)Math.Round(e);
                case UpgradeDefinition.StaffSpeed: return (1f + e) * 100f;
                case UpgradeDefinition.StaffEfficiency: return balance.autoJobIntervalSeconds * Mult(e);
                default: return 0f;
            }
        }

        // ---- Buying ----

        public bool IsUnlocked(UpgradeDefinition def, int playerLevel) => def != null && playerLevel >= def.unlockLevel;
        public bool IsMaxed(UpgradeDefinition def) => def != null && Level(def.id) >= def.maxLevel;
        public int NextCost(UpgradeDefinition def) => def == null ? 0 : def.CostForLevel(Level(def.id));

        /// <summary>Buys exactly one level. Validates unlock, max level and funds before charging.</summary>
        public ActionResult TryBuy(string id, int playerLevel, Wallet wallet)
        {
            var def = balance.FindUpgrade(id);
            if (def == null) return ActionResult.Unknown;
            if (!IsUnlocked(def, playerLevel)) return ActionResult.Locked;
            if (IsMaxed(def)) return ActionResult.LimitReached;
            if (!wallet.TrySpend(NextCost(def))) return ActionResult.NotEnoughCoins;
            levels[id] = Level(id) + 1;
            Upgraded?.Invoke(id, levels[id]);
            return ActionResult.Ok;
        }

        public IEnumerable<KeyValuePair<string, int>> All => levels;

        internal void Restore(IEnumerable<UpgradeLevelSave> saved)
        {
            levels.Clear();
            if (saved == null) return;
            foreach (var s in saved)
            {
                if (s == null) continue;
                var def = balance.FindUpgrade(s.id);
                if (def == null) continue; // removed upgrade: ignore safely
                levels[s.id] = Math.Max(0, Math.Min(def.maxLevel, s.level));
            }
        }
    }
}
