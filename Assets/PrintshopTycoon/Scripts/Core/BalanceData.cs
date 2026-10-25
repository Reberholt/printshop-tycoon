using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// All tunable gameplay numbers in one place. Unity wraps this in a ScriptableObject
    /// (GameBalanceConfig) so designers can edit values without touching code.
    /// Unlock levels below are provisional balancing targets, not rules baked into code.
    /// </summary>
    [Serializable]
    public class BalanceData
    {
        /// <summary>Bump when the shape of the defaults changes, so outdated balance assets get replaced.</summary>
        public const int CurrentDataVersion = 3;
        public int dataVersion;   // 0 in assets saved before versioning existed



        // Economy
        public int startingCoins = 500;
        public int startingBlankStock = 20;      // T-shirt blanks on the shelf at game start
        public int restockAmount = 10;
        public int storageCapacity = 40;         // total blanks the shelf can hold

        // Production timing (seconds, before upgrades)
        public float printSeconds = 3f;          // DTF printer prints the transfers
        public float playerPressSeconds = 2.5f;  // press stays closed after the minigame

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

        // Special (timed) orders
        public int specialCooldownOrders = 2;      // regular orders between two special offers
        public float minSecondsPerItem = 12f;      // used to reject impossible deadlines
        public float minSecondsOverhead = 60f;

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

        public List<ProductDefinition> products = new List<ProductDefinition>();
        public List<LogoDefinition> logos = new List<LogoDefinition>();
        public List<OrderTemplate> orders = new List<OrderTemplate>();
        public List<SpecialOrderDefinition> specialOrders = new List<SpecialOrderDefinition>();
        public List<MachineDefinition> machines = new List<MachineDefinition>();
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        public static BalanceData CreateDefault()
        {
            var b = new BalanceData { dataVersion = CurrentDataVersion };

            b.products.Add(new ProductDefinition { id = ProductDefinition.TShirt, nameKey = "product.tshirt", unlockLevel = 1, playable = true,
                blankCost = 4, materialCostPerItem = 3, pricePerItem = 33, pressTimeMultiplier = 1f, colorHex = "#FFFFFF", shape = "tshirt" });
            b.products.Add(new ProductDefinition { id = ProductDefinition.Sweatshirt, nameKey = "product.sweatshirt", unlockLevel = 3, playable = true,
                blankCost = 9, materialCostPerItem = 5, pricePerItem = 62, pressTimeMultiplier = 1.4f, colorHex = "#9EA3AB", shape = "sweatshirt" });
            b.products.Add(new ProductDefinition { id = ProductDefinition.SafetyVest, nameKey = "product.vest", unlockLevel = 5, playable = false,
                blankCost = 7, materialCostPerItem = 4, pricePerItem = 48, pressTimeMultiplier = 0.8f, colorHex = "#D7F21F", shape = "vest" });

            b.logos.Add(new LogoDefinition { id = "logo_bakery", nameKey = "logo.bakery", colorHex = "#F28C28" });
            b.logos.Add(new LogoDefinition { id = "logo_handball", nameKey = "logo.handball", colorHex = "#2E86DE" });
            b.logos.Add(new LogoDefinition { id = "logo_fishing", nameKey = "logo.fishing", colorHex = "#27AE60" });
            b.logos.Add(new LogoDefinition { id = "logo_school", nameKey = "logo.school", colorHex = "#8E44AD" });
            b.logos.Add(new LogoDefinition { id = "logo_festival", nameKey = "logo.festival", colorHex = "#E3191B" });

            // First order: 5 x 33 = 165 gross - 15 material = 150 net (the classic first payout).
            b.orders.Add(new OrderTemplate { id = "first_order", customerKey = "customer.bakery", logoId = "logo_bakery", productId = ProductDefinition.TShirt, quantity = 5 });
            b.orders.Add(new OrderTemplate { id = "handball", customerKey = "customer.handball", logoId = "logo_handball", productId = ProductDefinition.TShirt, quantity = 5 });
            b.orders.Add(new OrderTemplate { id = "fishing", customerKey = "customer.fishing", logoId = "logo_fishing", productId = ProductDefinition.TShirt, quantity = 6 });
            b.orders.Add(new OrderTemplate { id = "school_sweat", customerKey = "customer.school", logoId = "logo_school", productId = ProductDefinition.Sweatshirt, quantity = 4 });
            b.orders.Add(new OrderTemplate { id = "handball_sweat", customerKey = "customer.handball", logoId = "logo_handball", productId = ProductDefinition.Sweatshirt, quantity = 5 });

            b.specialOrders.Add(new SpecialOrderDefinition { id = "festival", customerKey = "customer.festival", logoId = "logo_festival",
                productId = ProductDefinition.TShirt, quantity = 8, bonus = 120, bonusXp = 40, timeLimitSeconds = 240f, unlockLevel = 2 });

            b.machines.Add(new MachineDefinition { typeId = MachineDefinition.HeatPress, nameKey = "machine.heat_press", price = 300, width = 1.6f, depth = 2.4f });

            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.PressTiming, nameKey = "upg.press_timing", target = "press", unlockLevel = 2, maxLevel = 3, baseCost = 120, costGrowthPercent = 60, effectPerLevel = 0.04f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.StorageCapacity, nameKey = "upg.storage_capacity", target = "storage", unlockLevel = 2, maxLevel = 3, baseCost = 100, costGrowthPercent = 60, effectPerLevel = 20f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.PressSpeed, nameKey = "upg.press_speed", target = "press", unlockLevel = 3, maxLevel = 3, baseCost = 180, costGrowthPercent = 60, effectPerLevel = 0.15f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.PrinterSpeed, nameKey = "upg.printer_speed", target = "printer", unlockLevel = 3, maxLevel = 3, baseCost = 160, costGrowthPercent = 60, effectPerLevel = 0.2f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.StorageRestock, nameKey = "upg.storage_restock", target = "storage", unlockLevel = 4, maxLevel = 3, baseCost = 120, costGrowthPercent = 60, effectPerLevel = 5f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.StaffSpeed, nameKey = "upg.staff_speed", target = "staff", unlockLevel = 4, maxLevel = 3, baseCost = 200, costGrowthPercent = 60, effectPerLevel = 0.15f });
            b.upgrades.Add(new UpgradeDefinition { id = UpgradeDefinition.StaffEfficiency, nameKey = "upg.staff_efficiency", target = "staff", unlockLevel = 4, maxLevel = 3, baseCost = 220, costGrowthPercent = 60, effectPerLevel = 0.2f });
            return b;
        }

        public LogoDefinition FindLogo(string id) => logos.Find(l => l.id == id);
        public MachineDefinition FindMachine(string typeId) => machines.Find(m => m.typeId == typeId);
        public ProductDefinition FindProduct(string id) => products.Find(p => p.id == id);
        public OrderTemplate FindOrderTemplate(string id) => orders.Find(o => o.id == id);
        public SpecialOrderDefinition FindSpecial(string id) => specialOrders.Find(s => s.id == id);
        public UpgradeDefinition FindUpgrade(string id) => upgrades.Find(u => u.id == id);

        /// <summary>Restock price for one batch of a product's blanks.</summary>
        public int RestockCost(ProductDefinition p, int amount) => Math.Max(0, p.blankCost * amount);
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
        public string productId = ProductDefinition.TShirt;
        public int quantity;
    }

    [Serializable]
    public class MachineDefinition
    {
        public const string HeatPress = "heat_press";

        public string typeId;
        public string nameKey;
        public int price;
        public float width;   // footprint includes the operator space in front of the machine
        public float depth;
    }

    /// <summary>A sellable product category (T-shirts, sweatshirts, safety vests ...).</summary>
    [Serializable]
    public class ProductDefinition
    {
        public const string TShirt = "tshirt";
        public const string Sweatshirt = "sweatshirt";
        public const string SafetyVest = "vest";

        public string id;
        public string nameKey;
        public int unlockLevel = 1;
        public bool playable = true;         // false = shown as "coming soon"
        public int blankCost;                // per blank, paid when restocking
        public int materialCostPerItem;      // DTF film + ink, deducted from the order payout
        public int pricePerItem;             // what the customer pays per item
        public float pressTimeMultiplier = 1f;
        public string colorHex = "#FFFFFF";
        public string shape = "tshirt";      // visual representation key
        public List<string> stations = new List<string> { "dtf_printer", "heat_press" };
    }

    /// <summary>Optional timed order with a bonus for finishing before the deadline.</summary>
    [Serializable]
    public class SpecialOrderDefinition
    {
        public string id;
        public string customerKey;
        public string logoId;
        public string productId = ProductDefinition.TShirt;
        public int quantity;
        public int bonus;
        public int bonusXp;
        public float timeLimitSeconds;
        public int unlockLevel = 1;
    }
}
