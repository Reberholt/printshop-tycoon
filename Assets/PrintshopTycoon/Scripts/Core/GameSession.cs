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
            Inventory = new Inventory(Balance.startingBlankStock);
            Orders = new PlayerOrderFlow(Balance, 0);
            AutoJobs = new AutoJobQueue(Balance);
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
            if (workers.Count > 0) AutoJobs.Tick(deltaTime);
            for (int i = 0; i < workers.Count; i++) workers[i].Tick(deltaTime, this);
        }

        // ---------- Shelf ----------

        public ActionResult TryRestock()
        {
            if (!Wallet.TrySpend(Balance.restockCost)) return ActionResult.NotEnoughCoins;
            Inventory.Add(Balance.restockAmount);
            return ActionResult.Ok;
        }

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

        public ActionResult TryPack(out int paid) => Orders.Pack(Wallet, Progression, out paid);

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
            var w = new WorkerTaskMachine(id, Balance);
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

        public SaveData ToSaveData()
        {
            var d = new SaveData
            {
                coins = Wallet.Coins,
                totalXp = Progression.TotalXp,
                completedOrders = Progression.CompletedOrders,
                claimedRewards = Wallet.ClaimedRewards.ToList(),
                orderSequence = Orders.Order.Sequence,
                orderStep = (int)Orders.Step,
                pressQuality = (int)Orders.Quality,
                missCount = Orders.MissCount,
                pressStationId = Orders.PressStationId,
                blankStock = Inventory.BlankStock,
                nextMachineSequence = nextMachineSequence,
                nextWorkerSequence = nextWorkerSequence,
                autoJobsQueued = AutoJobs.Queued,
                autoJobSequence = AutoJobs.NextSequence,
                tutorialStep = TutorialStep,
                soundOn = SoundOn,
                language = Language,
            };
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

        /// <summary>Builds a session from a save. Invalid values are repaired instead of crashing.</summary>
        public static GameSession FromSaveData(BalanceData balance, SaveData d)
        {
            var s = new GameSession(balance);
            if (d == null) return s;

            s.Wallet.Restore(d.coins, d.claimedRewards);
            s.Progression.Restore(d.totalXp, d.completedOrders);
            s.Inventory.Restore(d.blankStock);
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
                    if (s.Balance.FindMachine(m.typeId) == null) continue;
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
                    if (ws.blanksHeld > 0) s.Inventory.Add(ws.blanksHeld);
                }
            }
            s.AutoJobs.Restore(d.autoJobsQueued + returnedJobs, d.autoJobSequence);

            var step = (OrderStep)d.orderStep;
            string pressId = d.pressStationId;
            if (pressId != null && s.FindMachine(pressId) == null) pressId = null;
            s.Orders.Restore(d.orderSequence, step, (PressQuality)d.pressQuality, d.missCount, pressId);
            // If the order was already paid (e.g. crash right after payment), move on to the next one.
            if (s.Wallet.IsClaimed(s.Orders.Order.RewardId))
                s.Orders.Restore(d.orderSequence + 1, OrderStep.Offered, PressQuality.None, 0, null);
            if (s.Orders.Step == OrderStep.CollectFinished)
                s.TryReserveStation(s.Orders.PressStationId, PlayerOccupant);
            return s;
        }
    }
}
