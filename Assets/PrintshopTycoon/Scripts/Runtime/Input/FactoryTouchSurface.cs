using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// Full-screen touch layer for factory mode: one finger pans (or drags a machine while
    /// placing), two fingers pinch-zoom, a short tap selects. Mouse: drag + scroll wheel.
    /// Sits behind all buttons, so touches on UI never reach the world.
    /// </summary>
    public class FactoryTouchSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler
    {
        const float TapMaxMove = 18f;
        const float TapMaxSeconds = 0.35f;

        readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        Vector2 downPosition;
        float downTime;
        bool movedTooFar;
        float lastPinchDistance;

        public event Action<Vector2> Panned;              // screen pixels
        public event Action<float> Zoomed;                // >1 zoom in
        public event Action<Vector2> Tapped;              // screen position
        public event Action<Vector2> PlacementDragged;    // screen position
        public Func<bool> IsPlacing;

        public void OnPointerDown(PointerEventData e)
        {
            pointers[e.pointerId] = e.position;
            if (pointers.Count == 1)
            {
                downPosition = e.position;
                downTime = Time.unscaledTime;
                movedTooFar = false;
                if (IsPlacing != null && IsPlacing()) PlacementDragged?.Invoke(e.position);
            }
            else
            {
                movedTooFar = true; // multi-touch is never a tap
                lastPinchDistance = PinchDistance();
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!pointers.ContainsKey(e.pointerId)) return;
            Vector2 previous = pointers[e.pointerId];
            pointers[e.pointerId] = e.position;
            if ((e.position - downPosition).magnitude > TapMaxMove) movedTooFar = true;

            if (pointers.Count >= 2)
            {
                float d = PinchDistance();
                if (lastPinchDistance > 1f && d > 1f) Zoomed?.Invoke(d / lastPinchDistance);
                lastPinchDistance = d;
                Panned?.Invoke((e.position - previous) / pointers.Count);
            }
            else if (IsPlacing != null && IsPlacing())
            {
                PlacementDragged?.Invoke(e.position);
            }
            else
            {
                Panned?.Invoke(e.position - previous);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            bool wasSingle = pointers.Count == 1;
            pointers.Remove(e.pointerId);
            if (wasSingle && !movedTooFar && Time.unscaledTime - downTime <= TapMaxSeconds)
                Tapped?.Invoke(e.position);
            if (pointers.Count >= 2) lastPinchDistance = PinchDistance();
        }

        public void OnScroll(PointerEventData e)
        {
            if (Mathf.Abs(e.scrollDelta.y) > 0.01f) Zoomed?.Invoke(e.scrollDelta.y > 0 ? 1.1f : 1f / 1.1f);
        }

        void OnDisable() => pointers.Clear();

        float PinchDistance()
        {
            Vector2 a = default, b = default;
            int i = 0;
            foreach (var p in pointers.Values)
            {
                if (i == 0) a = p; else if (i == 1) b = p; else break;
                i++;
            }
            return i >= 2 ? Vector2.Distance(a, b) : 0f;
        }
    }
}
