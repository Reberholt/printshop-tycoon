using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// All tunable gameplay numbers in one place. Unity wraps this in a ScriptableObject
    /// (GameBalanceConfig) so designers can edit values without touching code.
    /// </summary>
    [Serializable]
    public class BalanceData
    {
        // Economy
        public int startingCoins = 500;
        public int startingBlankStock = 20;
        public int restockAmount = 10;
        public int restockCost = 40;

        // Heat press minigame
        public float indicatorSpeed = 0.9f;        // full sweeps (0->1) per second
        public float goodZoneWidth = 0.26f;        // fraction of the bar
        public float perfectZoneWidth = 0.08f;
        public int perfectBonusPercent = 20;
        public int lowQualityPenaltyPercent = 20;
        public int missesBeforeForcedAccept = 3;

        // Progression
        public int xpPerOrder = 50;
        public int xpPerAutoJob = 10;
        public int xpPerLevel = 100;               // level n -> n+1 costs xpPerLevel * n

        // Workers / automation
        public int workerHireCost = 250;
        public int maxWorkers = 3;
        public float workerMoveSpeed = 2.6f;
        public float workerPressSeconds = 6f;
        public float workerPackSeconds = 1.2f;
        public int autoJobShirts = 2;
        public int autoJobReward = 40;
        public float autoJobIntervalSeconds = 20f;
        public int maxQueuedAutoJobs = 3;

        public List<LogoDefinition> logos = new List<LogoDefinition>();
        public List<OrderTemplate> orders = new List<OrderTemplate>();
        public List<MachineDefinition> machines = new List<MachineDefinition>();

        public static BalanceData CreateDefault()
        {
            var b = new BalanceData();
            b.logos.Add(new LogoDefinition { id = "logo_bakery", nameKey = "logo.bakery", colorHex = "#F28C28" });
            b.logos.Add(new LogoDefinition { id = "logo_handball", nameKey = "logo.handball", colorHex = "#2E86DE" });
            b.logos.Add(new LogoDefinition { id = "logo_fishing", nameKey = "logo.fishing", colorHex = "#27AE60" });

            b.orders.Add(new OrderTemplate { id = "first_order", customerKey = "customer.bakery", logoId = "logo_bakery", quantity = 5, reward = 150 });
            b.orders.Add(new OrderTemplate { id = "handball", customerKey = "customer.handball", logoId = "logo_handball", quantity = 5, reward = 170 });
            b.orders.Add(new OrderTemplate { id = "fishing", customerKey = "customer.fishing", logoId = "logo_fishing", quantity = 5, reward = 190 });

            b.machines.Add(new MachineDefinition { typeId = MachineDefinition.HeatPress, nameKey = "machine.heat_press", price = 300, width = 1.5f, depth = 1.5f });
            return b;
        }

        public LogoDefinition FindLogo(string id) => logos.Find(l => l.id == id);
        public MachineDefinition FindMachine(string typeId) => machines.Find(m => m.typeId == typeId);
    }

    [Serializable]
    public class LogoDefinition
    {
        public string id;
        public string nameKey;
        public string colorHex;
    }

    [Serializable]
    public class OrderTemplate
    {
        public string id;
        public string customerKey;
        public string logoId;
        public int quantity;
        public int reward;
    }

    [Serializable]
    public class MachineDefinition
    {
        public const string HeatPress = "heat_press";

        public string typeId;
        public string nameKey;
        public int price;
        public float width;
        public float depth;
    }
}
