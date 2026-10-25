using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Presentation of a worker: walks to where the WorkerTaskMachine wants to be and reports
    /// arrival. All economy effects stay in the core state machine, so camera switches,
    /// frame drops or a destroyed view can never pay twice.
    /// </summary>
    public class WorkerView : MonoBehaviour
    {
        const float ArriveDistance = 0.12f;
        const float AisleX = -0.6f;   // free walkway through the middle of the workshop

        GameManager game;
        Transform visual;
        CarryStack carry;
        readonly List<Vector3> path = new List<Vector3>();
        WorkerTarget pathTarget = (WorkerTarget)(-1);
        string pathMachine;
        float bobPhase;

        public WorkerTaskMachine Machine { get; private set; }

        public static WorkerView Create(GameManager game, Transform parent, WorkerTaskMachine machine, Vector3 position)
        {
            int number = GameManager.WorkerNumber(machine.Id);
            Color[] shirts = { MaterialLibrary.Hex("#2EC4B6"), MaterialLibrary.Hex("#E71D36"), MaterialLibrary.Hex("#7B2CBF") };
            var t = CharacterFactory.CreateWorker(parent, "Worker_" + machine.Id, shirts[(number - 1 + shirts.Length) % shirts.Length]);
            t.position = position;
            var view = t.gameObject.AddComponent<WorkerView>();
            view.game = game;
            view.Machine = machine;
            view.visual = t.Find("Visual");
            view.carry = CarryStack.Create(t, new Vector3(0f, 0.9f, 0.42f));
            return view;
        }

        void Update()
        {
            if (Machine == null) return;
            Vector3 goal = GoalPosition(out bool reportArrival);
            if (Machine.Target != pathTarget || Machine.AssignedMachineId != pathMachine)
            {
                pathTarget = Machine.Target;
                pathMachine = Machine.AssignedMachineId;
                BuildPath(transform.position, goal);
            }

            bool moving = FollowPath(goal);
            if (!moving && reportArrival &&
                (Machine.State == WorkerState.MovingToShelf || Machine.State == WorkerState.MovingToStation || Machine.State == WorkerState.MovingToPacking))
                Machine.NotifyArrived();

            if (!moving)
            {
                // Face the station while working.
                var face = FaceDirection();
                if (face.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 540f * Time.deltaTime);
            }
            Animate(moving);
            carry.Show(Machine.BlanksHeld, Machine.State == WorkerState.MovingToPacking || Machine.State == WorkerState.Packing ? MaterialLibrary.Hex("#BDE0FE") : Palette.Shirt, null);
        }

        Vector3 GoalPosition(out bool reportArrival)
        {
            reportArrival = true;
            switch (Machine.Target)
            {
                case WorkerTarget.Shelf: return game.Shelf.ApproachPosition;
                case WorkerTarget.Packing: return game.Packing.ApproachPosition + new Vector3(0f, 0f, 0.6f);
                case WorkerTarget.Station:
                    var press = game.FindPress(Machine.AssignedMachineId);
                    if (press != null) return press.ApproachPosition;
                    break;
            }
            reportArrival = false;
            // Idle: wait next to the assigned press, or near the entrance.
            var idlePress = game.FindPress(Machine.AssignedMachineId);
            if (idlePress != null) return idlePress.ApproachPosition + new Vector3(-0.9f, 0f, -0.3f);
            return new Vector3(-1.8f - GameManager.WorkerNumber(Machine.Id) * 0.8f, 0f, -6.2f);
        }

        Vector3 FaceDirection()
        {
            switch (Machine.Target)
            {
                case WorkerTarget.Shelf: return game.Shelf.transform.position - transform.position;
                case WorkerTarget.Packing: return game.Packing.transform.position - transform.position;
                case WorkerTarget.Station:
                    var press = game.FindPress(Machine.AssignedMachineId);
                    return press != null ? press.transform.position - transform.position : Vector3.zero;
                default: return Vector3.back;
            }
        }

        void BuildPath(Vector3 from, Vector3 to)
        {
            path.Clear();
            // Simple aisle routing: step into the central walkway, walk along it, step out.
            if (Mathf.Abs(from.z - to.z) > 1.5f)
            {
                path.Add(new Vector3(AisleX, 0f, from.z));
                path.Add(new Vector3(AisleX, 0f, to.z));
            }
            path.Add(new Vector3(to.x, 0f, to.z));
        }

        bool FollowPath(Vector3 goal)
        {
            if (path.Count == 0) path.Add(goal);
            Vector3 next = path[0];
            Vector3 pos = transform.position;
            Vector3 delta = next - pos; delta.y = 0f;
            if (delta.magnitude <= ArriveDistance)
            {
                if (path.Count > 1) { path.RemoveAt(0); return true; }
                // Keep the final point in sync with a goal that might move (e.g. machine placed).
                path[0] = goal;
                return (goal - pos).sqrMagnitude > ArriveDistance * ArriveDistance;
            }
            float step = game.Session.Balance.workerMoveSpeed * Time.deltaTime;
            transform.position = pos + Vector3.ClampMagnitude(delta, step);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 720f * Time.deltaTime);
            return true;
        }

        void Animate(bool moving)
        {
            if (visual == null) return;
            if (moving)
            {
                bobPhase += Time.deltaTime * 13f;
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.07f, 0f);
            }
            else if (Machine.State == WorkerState.Working || Machine.State == WorkerState.Packing)
            {
                bobPhase += Time.deltaTime * 8f;
                visual.localPosition = new Vector3(0f, Mathf.Max(0f, Mathf.Sin(bobPhase)) * 0.05f, 0f);
            }
            else visual.localPosition = Vector3.zero;
        }

        public string StatusText
        {
            get
            {
                switch (Machine.Block)
                {
                    case WorkerBlock.Unassigned: return Loc.T("worker.unassigned");
                    case WorkerBlock.NoStock: return Loc.T("worker.no_stock");
                    case WorkerBlock.StationBusy: return Loc.T("worker.busy");
                    case WorkerBlock.NoJobs when Machine.State == WorkerState.Idle: return Loc.T("worker.no_jobs");
                }
                switch (Machine.State)
                {
                    case WorkerState.Working: return Loc.T("worker.working") + " " + Mathf.RoundToInt(Machine.TaskProgress * 100f) + "%";
                    case WorkerState.Packing: return Loc.T("worker.packing");
                    case WorkerState.Idle: return Loc.T("worker.idle");
                    default: return Loc.T("worker.moving");
                }
            }
        }

        public Vector3 LabelPosition => transform.position + Vector3.up * 2.1f;
    }
}
