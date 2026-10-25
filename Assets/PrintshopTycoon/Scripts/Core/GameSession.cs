using System;
using System.Collections.Generic;
using System.Linq;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// The whole game state and its rules, free of Unity. Views subscribe to events;
    /// stations and menus call the Try* methods and react to the ActionResult.
    /// </summary>
    public class GameSession : IWorkerWorld
    {
        public const string PlayerOccupant = "player";

        readonly Dictionary<string, string> stationOccupants = new Dictionary<string, string>();
        readonly List<WorkerTaskMachine> workers = new List<WorkerTaskMachine>();
        readonly List<MachineInstance> machines = new List<MachineInstance>();
        int nextMachineSequence = 1;
        int nextWorkerSequence = 1;

        public BalanceData Balance { get; }
        public Wallet Wallet { get; }
        public Progression Progression { get; }
        public Inventory Inventory { get; }
        public UpgradeState Upgrades { get; }
        public PlayerOrderFlow Orders { get; }
        public AutoJobQueue AutoJobs { get; }
        public PlacementValidator Placement { get; }

        public int TutorialStep { get; set; }
        public bool SoundOn { get; set; } = true;
        public string Language { get; set; } = "da";

        public IReadOnlyList<MachineInstance> Machines => machines;
        public IReadOnlyList<WorkerTaskMachine> Workers => workers;

        public event Action<MachineInstance> MachineAdded;
        public event Action<WorkerTaskMachine> WorkerHired;
        public event Action<WorkerTaskMachine> WorkerAssignmentChanged;
        /// <summary>(jobId, coins) when a worker finishes an automated job.</summary>
        public event Action<string, int> AutoJobCompleted;
        public event Action StationsChanged;

        public GameSession(BalanceData balance)
        {
            Balance = balance ?? BalanceData.CreateDefault();
            Wallet = new Wallet(Balance.startingCoins);
            Progression = new Progression(Balance.xpPerLevel);
            Upgrades = new UpgradeState(Balance);
            Inventory = new Inventory(Balance.startingBlankStock, () => Upgrades.StorageCapacity);
            Orders = new PlayerOrderFlow(Balance, 0, Upgrades, () => Progression.Level);
            AutoJobs = new AutoJobQueue(Balance, Upgrades);
            Placement = new PlacementValidator(WorkshopLayout.BuildArea);

            machines.Add(new MachineInstance
            {
                id = WorkshopLayout.StarterPressId,
                typeId = MachineDefinition.HeatPress,
                x = WorkshopLayout.StarterPress.centerX,
                z = WorkshopLayout.StarterPress.centerZ,
            });
        }

        // ---------- Simulation ----------

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            Orders.Tick(deltaTime);
            if (workers.Count > 0) AutoJobs.Tick(deltaTime);
            for (int i = 0; i < workers.Count; i++) workers[i].Tick(deltaTime, this);
        }

        // ---------- Shelf ----------

        /// <summary>Blanks one restock tap would add (limited by free shelf space).</summary>
        public int RestockAmountFor(string productId) => Math.Min(Upgrades.RestockAmount, Inventory.FreeSpace);

        public int RestockCostFor(string productId)
        {
            var p = Balance.FindProduct(productId);
            return p == null ? 0 : Balance.RestockCost(p, RestockAmountFor(productId));
        }

        public bool IsProductUnlocked(string productId) => Unlocks.IsProductUnlocked(Balance, productId, Progression.Level);

        /// <summary>Buys a batch of blanks. Never overfills the shelf or charges for space that isn't there.</summary>
        public ActionResult TryRestock(string productId, out int added)
        {
            added = 0;
            if (!IsProductUnlocked(productId)) return ActionResult.Locked;
            int amount = RestockAmountFor(productId);
            if (amount <= 0) return ActionResult.StorageFull;
            if (!Wallet.TrySpend(RestockCostFor(productId))) return ActionResult.NotEnoughCoins;
            Inventory.Add(productId, amount);
            added = amount;
            return ActionResult.Ok;
        }

        public ActionResult TryRestock() => TryRestock(ProductDefinition.TShirt, out _);

        /// <summary>The product the shelf should restock right now (current order first).</summary>
        public string ShelfProduct => Orders.Step == OrderStep.CollectBlanks || Orders.Step == OrderStep.Offered
            ? Orders.Order.ProductId : ProductDefinition.TShirt;

        // ---------- Upgrades ----------

        public ActionResult TryBuyUpgrade(string id) => Upgrades.TryBuy(id, Progression.Level, Wallet);

        public HeatPressMinigame CreateMinigame(int seed) =>
            new HeatPressMinigame(Upgrades.IndicatorSpeed, Upgrades.GoodZoneWidth, Upgrades.PerfectZoneWidth, seed);

        // ---------- Player production ----------

        public ActionResult TryBeginPlayerPress(string machineId)
        {
            var r = Orders.CanBeginPress();
            if (r != ActionResult.Ok) return r;
            if (FindMachine(machineId) == null) return ActionResult.WrongStation;
            return TryReserveStation(machineId, PlayerOccupant) ? ActionResult.Ok : ActionResult.StationBusy;
        }

        /// <summary>Call when the minigame ends. On retry the press stays reserved.</summary>
        public ActionResult ResolvePlayerPress(string machineId, PressResult result)
        {
            var r = Orders.ResolvePress(machineId, result);
            return r;
        }

        /// <summary>Player walked away from the minigame without finishing.</summary>
        public void CancelPlayerPress(string machineId)
        {
            if (Orders.Step == OrderStep.PressShirts) ReleaseStation(machineId, PlayerOccupant);
        }

        public ActionResult TryCollectFinished(string machineId)
        {
            var r = Orders.CollectFinished(machineId);
            if (r == ActionResult.Ok) ReleaseStation(machineId, PlayerOccupant);
            return r;
        }

        public ActionResult TryPack(out OrderPayout payout) => Orders.Pack(Wallet, Progression, out payout);

        public ActionResult TryCollectTransfers() => Orders.CollectTransfers();

        // ---------- Station occupancy ----------

        public string GetOccupant(string machineId) =>
            machineId != null && stationOccupants.TryGetValue(machineId, out var o) ? o : null;

        public bool TryReserveStation(string machineId, string occupantId)
        {
            if (machineId == null || occupantId == null) return false;
            if (stationOccupants.TryGetValue(machineId, out var current))
                return current == occupantId;
            stationOccupants[machineId] = occupantId;
            StationsChanged?.Invoke();
            return true;
        }

        public void ReleaseStation(string machineId, string occupantId)
        {
            if (machineId != null && stationOccupants.TryGetValue(machineId, out var current) && current == occupantId)
            {
                stationOccupants.Remove(machineId);
                StationsChanged?.Invoke();
            }
        }

        // ---------- Machines ----------

        public MachineInstance FindMachine(string id) => machines.FirstOrDefault(m => m.id == id);

        public Footprint FootprintOf(MachineInstance m)
        {
            var def = Balance.FindMachine(m.typeId);
            float w = def != null ? def.width : 1.5f, d = def != null ? def.depth : 1.5f;
            return new Footprint(m.x, m.z, w, d);
        }

        public IEnumerable<Footprint> OccupiedFootprints(string ignoreMachineId = null)
        {
            foreach (var f in WorkshopLayout.FixedObstacles) yield return f;
            foreach (var m in machines)
                if (m.id != ignoreMachineId) yield return FootprintOf(m);
        }

        public bool IsPlacementValid(string typeId, float x, float z)
        {
            var def = Balance.FindMachine(typeId);
            if (def == null) return false;
            return Placement.IsValid(new Footprint(x, z, def.width, def.depth), OccupiedFootprints());
        }

        /// <summary>Validates funds and placement, then charges and adds the machine.</summary>
        public ActionResult TryPurchaseMachine(string typeId, float x, float z, out MachineInstance machine)
        {
            machine = null;
            var def = Balance.FindMachine(typeId);
            if (def == null) return ActionResult.Unknown;
            if (!IsPlacementValid(typeId, x, z)) return ActionResult.InvalidPlacement;
            if (!Wallet.CanAfford(def.price)) return ActionResult.NotEnoughCoins;
            if (!Wallet.TrySpend(def.price)) return ActionResult.NotEnoughCoins;

            machine = new MachineInstance { id = "machine#" + nextMachineSequence++, typeId = typeId, x = x, z = z };
            machines.Add(machine);
            MachineAdded?.Invoke(machine);
            return ActionResult.Ok;
        }

        // ---------- Workers ----------

        public bool CanHireWorker => workers.Count < Balance.maxWorkers;

        public ActionResult TryHireWorker(out WorkerTaskMachine worker)
        {
            worker = null;
            if (!CanHireWorker) return ActionResult.LimitReached;
            if (!Wallet.TrySpend(Balance.workerHireCost)) return ActionResult.NotEnoughCoins;
            worker = CreateWorker("worker#" + nextWorkerSequence++);
            WorkerHired?.Invoke(worker);
            return ActionResult.Ok;
        }

        public WorkerTaskMachine FindWorkerAssignedTo(string machineId) =>
            workers.FirstOrDefault(w => w.AssignedMachineId == machineId);

        /// <summary>Assigns a worker to a heat press (null = unassign). One worker per press.</summary>
        public ActionResult AssignWorker(WorkerTaskMachine worker, string machineId)
        {
            if (worker == null) return ActionResult.Unknown;
            if (machineId != null)
            {
                var m = FindMachine(machineId);
                if (m == null || m.typeId != MachineDefinition.HeatPress) return ActionResult.WrongStation;
                var other = FindWorkerAssignedTo(machineId);
                if (other != null && other != worker) return ActionResult.StationBusy;
            }
            worker.Assign(machineId, this);
            WorkerAssignmentChanged?.Invoke(worker);
            return ActionResult.Ok;
        }

        /// <summary>The next heat press without a worker, cycling from the worker's current one.</summary>
        public string NextFreePressFor(WorkerTaskMachine worker)
        {
            var presses = machines.Where(m => m.typeId == MachineDefinition.HeatPress).ToList();
            if (presses.Count == 0) return null;
            int start = presses.FindIndex(m => m.id == worker.AssignedMachineId);
            for (int i = 1; i <= presses.Count; i++)
            {
                var candidate = presses[(start + i + presses.Count) % presses.Count];
                var owner = FindWorkerAssignedTo(candidate.id);
                if (owner == null || owner == worker) return candidate.id;
            }
            return null;
        }

        WorkerTaskMachine CreateWorker(string id)
        {
            var w = new WorkerTaskMachine(id, Balance, Upgrades);
            workers.Add(w);
            return w;
        }

        // ---------- IWorkerWorld ----------

        public bool TryReserveJob(out string jobId) => AutoJobs.TryReserve(out jobId);
        public void ReleaseJob(string jobId) => AutoJobs.Release(jobId);
        public bool TryTakeBlanks(int amount) => Inventory.TryTake(amount);
        public void ReturnBlanks(int amount) => Inventory.Add(amount);

        public bool CompleteJob(string jobId)
        {
            if (!AutoJobs.Finish(jobId)) return false;
            if (!Wallet.TryClaimReward(jobId, Balance.autoJobReward)) return false;
            Progression.AddXp(Balance.xpPerAutoJob);
            AutoJobCompleted?.Invoke(jobId, Balance.autoJobReward);
            return true;
        }

        // ---------- Save / load ----------

        /// <summary>Order templates of save version 1, in their v1 order (used to migrate old saves).</summary>
        static readonly string[] LegacyV1Templates = { "first_order", "handball", "fishing" };

        public SaveData ToSaveData()
        {
            var o = Orders;
            var d = new SaveData
            {
                coins = Wallet.Coins,
                totalXp = Progression.TotalXp,
                completedOrders = Progression.CompletedOrders,
                claimedRewards = Wallet.ClaimedRewards.ToList(),
                orderSequence = o.Order.Sequence,
                orderStep = (int)o.Step,
                pressQuality = (int)o.Quality,
                missCount = o.MissCount,
                pressStationId = o.PressStationId,
                blankStock = Inventory.BlankStock,
                orderTemplateId = o.Order.TemplateId,
                orderIsSpecial = o.Order.IsSpecial,
                printing = o.Printing,
                printRemaining = o.PrintRemaining,
                pressRemaining = o.PressRemaining,
                specialRemaining = o.SpecialRemaining,
                specialExpired = o.SpecialExpired,
                specialsCompleted = o.SpecialsCompleted,
                specialCooldown = o.SpecialCooldown,
                nextMachineSequence = nextMachineSequence,
                nextWorkerSequence = nextWorkerSequence,
                autoJobsQueued = AutoJobs.Queued,
                autoJobSequence = AutoJobs.NextSequence,
                tutorialStep = TutorialStep,
                soundOn = SoundOn,
                language = Language,
            };
            foreach (var kv in Inventory.All)
                if (kv.Value > 0) d.stocks.Add(new ProductStockSave { productId = kv.Key, amount = kv.Value });
            foreach (var kv in Upgrades.All)
                if (kv.Value > 0) d.upgrades.Add(new UpgradeLevelSave { id = kv.Key, level = kv.Value });
            foreach (var m in machines)
                if (m.id != WorkshopLayout.StarterPressId)
                    d.machines.Add(new MachineInstance { id = m.id, typeId = m.typeId, x = m.x, z = m.z });
            foreach (var w in workers)
                d.workers.Add(new WorkerSave
                {
                    id = w.Id,
                    assignedMachineId = w.AssignedMachineId,
                    hadJobInProgress = w.JobId != null,
                    blanksHeld = w.BlanksHeld,
                });
            return d;
        }

        /// <summary>
        /// Builds a session from a save (version 1 or 2). Invalid values are repaired instead of
        /// crashing, and nothing that was paid can be paid again.
        /// </summary>
        public static GameSession FromSaveData(BalanceData balance, SaveData d)
        {
            var s = new GameSession(balance);
            if (d == null) return s;
            var b = s.Balance;

            s.Wallet.Restore(d.coins, d.claimedRewards);
            s.Progression.Restore(d.totalXp, d.completedOrders);
            s.Upgrades.Restore(d.upgrades);

            // Stock: v2 per product, v1 only had T-shirt blanks.
            var stock = new List<KeyValuePair<string, int>>();
            if (d.version >= 2 && d.stocks != null)
            {
                foreach (var st in d.stocks)
                    if (st != null && b.FindProduct(st.productId) != null)
                        stock.Add(new KeyValuePair<string, int>(st.productId, st.amount));
            }
            else stock.Add(new KeyValuePair<string, int>(ProductDefinition.TShirt, d.blankStock));
            s.Inventory.Restore(stock);

            s.nextMachineSequence = Math.Max(1, d.nextMachineSequence);
            s.nextWorkerSequence = Math.Max(1, d.nextWorkerSequence);
            s.TutorialStep = Math.Max(0, d.tutorialStep);
            s.SoundOn = d.soundOn;
            s.Language = string.IsNullOrEmpty(d.language) ? "da" : d.language;

            if (d.machines != null)
            {
                foreach (var m in d.machines)
                {
                    if (m == null || string.IsNullOrEmpty(m.id) || s.FindMachine(m.id) != null) continue;
                    if (b.FindMachine(m.typeId) == null) continue;
                    s.machines.Add(new MachineInstance { id = m.id, typeId = m.typeId, x = m.x, z = m.z });
                    int seq;
                    if (m.id.StartsWith("machine#") && int.TryParse(m.id.Substring(8), out seq))
                        s.nextMachineSequence = Math.Max(s.nextMachineSequence, seq + 1);
                }
            }

            int returnedJobs = 0;
            if (d.workers != null)
            {
                foreach (var ws in d.workers)
                {
                    if (ws == null || string.IsNullOrEmpty(ws.id) || s.workers.Any(w => w.Id == ws.id)) continue;
                    var w = s.CreateWorker(ws.id);
                    string machineId = ws.assignedMachineId;
                    if (machineId != null && (s.FindMachine(machineId) == null || s.FindWorkerAssignedTo(machineId) != null))
                        machineId = null;
                    w.RestoreAssignment(machineId);
                    // Interrupted jobs go back to the queue and their shirts back on the shelf (never paid twice).
                    if (ws.hadJobInProgress) returnedJobs++;
                    if (ws.blanksHeld > 0) s.Inventory.Add(ProductDefinition.TShirt, ws.blanksHeld);
                }
            }
            s.AutoJobs.Restore(d.autoJobsQueued + returnedJobs, d.autoJobSequence);

            RestoreOrder(s, d);
            return s;
        }

        static void RestoreOrder(GameSession s, SaveData d)
        {
            var b = s.Balance;
            var flow = s.Orders;
            int seq = Math.Max(0, d.orderSequence);
            flow.RestoreCounters(d.specialsCompleted, d.specialCooldown);

            OrderInstance order = null;
            if (d.orderIsSpecial)
            {
                var def = b.FindSpecial(d.orderTemplateId);
                if (def != null) order = OrderInstance.CreateSpecial(b, def, seq, flow.SpecialsCompleted);
            }
            else
            {
                string templateId = d.orderTemplateId;
                if (string.IsNullOrEmpty(templateId) && d.version < 2) templateId = LegacyV1Templates[seq % LegacyV1Templates.Length];
                var t = b.FindOrderTemplate(templateId);
                if (t != null) order = OrderInstance.FromTemplate(b, t, seq);
            }

            if (order == null) { flow.RestartRegular(seq); return; } // unknown order: offer a fresh one

            string pressId = d.pressStationId;
            if (pressId != null && s.FindMachine(pressId) == null) pressId = null;
            flow.Restore(order, (OrderStep)d.orderStep, (PressQuality)d.pressQuality, d.missCount, pressId,
                d.printing, d.printRemaining, d.pressRemaining, d.specialRemaining, d.specialExpired);

            // Already paid (e.g. crash right after payment): move on instead of paying again.
            if (s.Wallet.IsClaimed(flow.Order.RewardId))
            {
                if (flow.Order.IsSpecial)
                {
                    flow.RestoreCounters(flow.SpecialsCompleted + 1, b.specialCooldownOrders);
                    flow.RestartRegular(seq);
                }
                else flow.RestartRegular(seq + 1);
            }
            if (flow.Step == OrderStep.CollectFinished)
                s.TryReserveStation(flow.PressStationId, PlayerOccupant);
        }
    }
}
