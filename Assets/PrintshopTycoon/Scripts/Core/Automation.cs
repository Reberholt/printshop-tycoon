using System;
using System.Collections.Generic;

namespace PrintshopTycoon.Core
{
    /// <summary>Small walk-in orders that hired workers produce automatically.</summary>
    public class AutoJobQueue
    {
        readonly BalanceData balance;
        readonly HashSet<string> reserved = new HashSet<string>();
        float spawnTimer;

        public int Queued { get; private set; }
        public int NextSequence { get; private set; }
        public int InProgress => reserved.Count;

        public AutoJobQueue(BalanceData balance) { this.balance = balance; }

        public void Tick(float deltaTime)
        {
            if (Queued >= balance.maxQueuedAutoJobs) { spawnTimer = 0f; return; }
            spawnTimer += deltaTime;
            if (spawnTimer >= balance.autoJobIntervalSeconds)
            {
                spawnTimer = 0f;
                Queued++;
            }
        }

        public bool TryReserve(out string jobId)
        {
            jobId = null;
            if (Queued <= 0) return false;
            Queued--;
            jobId = "auto#" + NextSequence;
            NextSequence++;
            reserved.Add(jobId);
            return true;
        }

        public void Release(string jobId)
        {
            if (jobId != null && reserved.Remove(jobId)) Queued++;
        }

        /// <summary>Removes the job from the in-progress set. False if it was not in progress.</summary>
        public bool Finish(string jobId) => jobId != null && reserved.Remove(jobId);

        internal void Restore(int queued, int nextSequence)
        {
            Queued = Math.Max(0, Math.Min(queued, balance.maxQueuedAutoJobs));
            NextSequence = Math.Max(0, nextSequence);
            reserved.Clear();
        }
    }

    public interface IWorkerWorld
    {
        bool TryReserveJob(out string jobId);
        void ReleaseJob(string jobId);
        bool TryTakeBlanks(int amount);
        void ReturnBlanks(int amount);
        bool TryReserveStation(string machineId, string occupantId);
        void ReleaseStation(string machineId, string occupantId);
        bool CompleteJob(string jobId);
    }

    public enum WorkerState { Idle, MovingToShelf, MovingToStation, Working, MovingToPacking, Packing }
    public enum WorkerTarget { None, Shelf, Station, Packing }
    public enum WorkerBlock { None, Unassigned, NoJobs, NoStock, StationBusy }

    /// <summary>
    /// Finite state machine for one worker. Unity moves the character and calls NotifyArrived();
    /// everything that affects the economy happens here, exactly once per job.
    /// </summary>
    public class WorkerTaskMachine
    {
        readonly BalanceData balance;
        bool arrived;
        float timer;

        public string Id { get; }
        public string AssignedMachineId { get; private set; }
        public WorkerState State { get; private set; } = WorkerState.Idle;
        public WorkerBlock Block { get; private set; } = WorkerBlock.Unassigned;
        public string JobId { get; private set; }
        public int BlanksHeld { get; private set; }

        public event Action<WorkerTaskMachine> StateChanged;

        public WorkerTaskMachine(string id, BalanceData balance)
        {
            Id = id;
            this.balance = balance;
        }

        public WorkerTarget Target
        {
            get
            {
                switch (State)
                {
                    case WorkerState.MovingToShelf: return WorkerTarget.Shelf;
                    case WorkerState.MovingToStation:
                    case WorkerState.Working: return WorkerTarget.Station;
                    case WorkerState.MovingToPacking:
                    case WorkerState.Packing: return WorkerTarget.Packing;
                    default: return WorkerTarget.None;
                }
            }
        }

        /// <summary>0..1 progress of the current timed task.</summary>
        public float TaskProgress
        {
            get
            {
                if (State == WorkerState.Working) return 1f - Clamp01(timer / Math.Max(0.01f, balance.workerPressSeconds));
                if (State == WorkerState.Packing) return 1f - Clamp01(timer / Math.Max(0.01f, balance.workerPackSeconds));
                return 0f;
            }
        }

        public void NotifyArrived() => arrived = true;

        public void Assign(string machineId, IWorkerWorld world)
        {
            if (AssignedMachineId == machineId) return;
            Abort(world);
            AssignedMachineId = machineId;
            Block = machineId == null ? WorkerBlock.Unassigned : WorkerBlock.None;
            StateChanged?.Invoke(this);
        }

        /// <summary>Cancels the current job and gives back everything it reserved.</summary>
        public void Abort(IWorkerWorld world)
        {
            if (AssignedMachineId != null) world.ReleaseStation(AssignedMachineId, Id);
            if (JobId != null) world.ReleaseJob(JobId);
            if (BlanksHeld > 0) world.ReturnBlanks(BlanksHeld);
            JobId = null;
            BlanksHeld = 0;
            arrived = false;
            SetState(WorkerState.Idle);
        }

        public void Tick(float deltaTime, IWorkerWorld world)
        {
            switch (State)
            {
                case WorkerState.Idle:
                    if (AssignedMachineId == null) { Block = WorkerBlock.Unassigned; return; }
                    if (world.TryReserveJob(out var jobId))
                    {
                        JobId = jobId;
                        Block = WorkerBlock.None;
                        GoTo(WorkerState.MovingToShelf);
                    }
                    else Block = WorkerBlock.NoJobs;
                    break;

                case WorkerState.MovingToShelf:
                    if (!Consume()) return;
                    if (world.TryTakeBlanks(balance.autoJobShirts))
                    {
                        BlanksHeld = balance.autoJobShirts;
                        Block = WorkerBlock.None;
                        GoTo(WorkerState.MovingToStation);
                    }
                    else
                    {
                        world.ReleaseJob(JobId);
                        JobId = null;
                        Block = WorkerBlock.NoStock;
                        SetState(WorkerState.Idle);
                    }
                    break;

                case WorkerState.MovingToStation:
                    if (!arrived) return;
                    if (world.TryReserveStation(AssignedMachineId, Id))
                    {
                        arrived = false;
                        Block = WorkerBlock.None;
                        timer = balance.workerPressSeconds;
                        SetState(WorkerState.Working);
                    }
                    else Block = WorkerBlock.StationBusy; // keep waiting at the station
                    break;

                case WorkerState.Working:
                    timer -= deltaTime;
                    if (timer <= 0f)
                    {
                        world.ReleaseStation(AssignedMachineId, Id);
                        GoTo(WorkerState.MovingToPacking);
                    }
                    break;

                case WorkerState.MovingToPacking:
                    if (!Consume()) return;
                    timer = balance.workerPackSeconds;
                    SetState(WorkerState.Packing);
                    break;

                case WorkerState.Packing:
                    timer -= deltaTime;
                    if (timer <= 0f)
                    {
                        world.CompleteJob(JobId);
                        JobId = null;
                        BlanksHeld = 0;
                        SetState(WorkerState.Idle);
                    }
                    break;
            }
        }

        bool Consume()
        {
            if (!arrived) return false;
            arrived = false;
            return true;
        }

        void GoTo(WorkerState movingState)
        {
            arrived = false;
            SetState(movingState);
        }

        void SetState(WorkerState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(this);
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        internal void RestoreAssignment(string machineId)
        {
            AssignedMachineId = machineId;
            Block = machineId == null ? WorkerBlock.Unassigned : WorkerBlock.None;
        }
    }

    [Serializable]
    public class MachineInstance
    {
        public string id;
        public string typeId;
        public float x;
        public float z;
    }
}
