using System.Collections.Generic;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Finds the station Hue is standing at and drives the contextual interact button.
    /// The station the current order needs next wins ties, so the button "just works".
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        public float range = 1.25f;

        readonly List<Interactable> stations = new List<Interactable>();
        Transform player;
        GameManager game;

        public Interactable Focused { get; private set; }
        public string FocusedLabel { get; private set; }
        public bool FocusedAvailable { get; private set; }

        public void Init(GameManager gameManager, Transform playerTransform)
        {
            game = gameManager;
            player = playerTransform;
        }

        public void Register(Interactable i) { if (!stations.Contains(i)) stations.Add(i); }
        public void Unregister(Interactable i) => stations.Remove(i);
        public IReadOnlyList<Interactable> Stations => stations;

        void Update()
        {
            if (player == null) return;
            Interactable best = null;
            float bestScore = float.MaxValue;
            var next = game.NextTarget;
            Vector3 p = player.position;
            Vector3 fwd = player.forward;
            foreach (var s in stations)
            {
                if (s == null) continue;
                float d = s.DistanceTo(p);
                if (d > range) continue;
                // Prefer what Hue faces, and strongly prefer the next required station.
                Vector3 to = s.transform.position - p; to.y = 0f;
                float facing = to.sqrMagnitude > 0.001f ? Vector3.Dot(fwd, to.normalized) : 1f;
                float score = d - facing * 0.3f - (s == next ? 1f : 0f);
                if (score < bestScore) { bestScore = score; best = s; }
            }
            Focused = best;
            if (best != null)
            {
                FocusedAvailable = best.TryGetAction(out var label);
                FocusedLabel = label;
            }
            else
            {
                FocusedAvailable = false;
                FocusedLabel = null;
            }
        }

        public bool TryInteract()
        {
            if (Focused == null || !FocusedAvailable) return false;
            Focused.Interact();
            return true;
        }
    }
}
