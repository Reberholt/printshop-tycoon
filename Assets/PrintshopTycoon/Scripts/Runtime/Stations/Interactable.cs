using System.Collections;
using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Anything Hue can walk up to and use. The InteractionSystem asks for the current action
    /// label; the big interact button calls Interact().
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        protected GameManager Game;
        protected GameSession Session => Game.Session;

        Vector3 approachLocal;
        Coroutine punch;

        /// <summary>Solid body: blocks Hue, is hit by factory-mode taps, defines interaction range.</summary>
        public BoxCollider Body { get; internal set; }

        public Vector3 ApproachPosition => transform.TransformPoint(approachLocal);
        public abstract string DisplayName { get; }

        /// <summary>Returns false when Hue can't do anything here right now.</summary>
        public abstract bool TryGetAction(out string label);
        public abstract void Interact();

        /// <summary>Short status for the factory-mode info panel.</summary>
        public virtual string StatusText => null;

        /// <summary>A running job on this station (progress bar above it); ready = waiting to be collected.</summary>
        public virtual bool TryGetProgress(out float progress01, out bool ready)
        {
            progress01 = 0f; ready = false;
            return false;
        }

        public void Setup(GameManager game, Vector3 approachLocalPosition)
        {
            Game = game;
            approachLocal = approachLocalPosition;
            OnSetup();
        }

        protected virtual void OnSetup() { }

        /// <summary>Distance from a point to the station's body (0 when touching).</summary>
        public float DistanceTo(Vector3 point)
        {
            if (Body == null) return Vector3.Distance(point, transform.position);
            var b = Body.bounds;
            point.y = b.center.y;
            return Mathf.Sqrt(b.SqrDistance(point));
        }

        public Vector3 TopPosition
        {
            get
            {
                if (Body == null) return transform.position + Vector3.up * 2f;
                var b = Body.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        /// <summary>Small squash-and-stretch so every interaction feels responsive.</summary>
        public void Punch()
        {
            if (!isActiveAndEnabled) return;
            if (punch != null) StopCoroutine(punch);
            punch = StartCoroutine(PunchRoutine());
        }

        IEnumerator PunchRoutine()
        {
            const float duration = 0.25f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / duration * Mathf.PI);
                transform.localScale = new Vector3(1f + 0.06f * k, 1f - 0.08f * k, 1f + 0.06f * k);
                yield return null;
            }
            transform.localScale = Vector3.one;
            punch = null;
        }

        protected void Report(ActionResult result)
        {
            switch (result)
            {
                case ActionResult.NotEnoughCoins: Game.UI.Toast(Loc.T("toast.no_coins"), true); break;
                case ActionResult.NotEnoughStock: Game.UI.Toast(Loc.T("toast.no_stock"), true); break;
                case ActionResult.WrongLogo: Game.UI.Toast(Loc.T("toast.wrong_logo"), true); break;
                case ActionResult.StationBusy: Game.UI.Toast(Loc.T("toast.station_busy"), true); break;
                case ActionResult.WrongStation: Game.UI.Toast(Loc.T("toast.wrong_press"), true); break;
                case ActionResult.InvalidPlacement: Game.UI.Toast(Loc.T("toast.invalid_place"), true); break;
                case ActionResult.LimitReached: Game.UI.Toast(Loc.T("toast.limit"), true); break;
                case ActionResult.StorageFull: Game.UI.Toast(Loc.T("toast.storage_full"), true); break;
                case ActionResult.Locked: Game.UI.Toast(Loc.T("toast.locked"), true); break;
                case ActionResult.NotReady: Game.UI.Toast(Loc.T("toast.not_ready"), true); break;
            }
            if (result != ActionResult.Ok) Game.Audio.Play(Sfx.Error);
        }
    }
}
