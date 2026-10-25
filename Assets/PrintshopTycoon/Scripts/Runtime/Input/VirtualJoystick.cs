using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// Floating virtual joystick: touch anywhere inside its zone, the base appears under the
    /// thumb and the knob follows. Uses the EventSystem, so it works with touch and mouse and
    /// never fires through other UI.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform zone;
        RectTransform baseRect;
        RectTransform knob;
        CanvasGroup group;
        int pointerId = int.MinValue;
        float radius = 110f;
        Vector2 restPosition;

        public Vector2 Value { get; private set; }
        public bool Active => pointerId != int.MinValue;

        public void Init(RectTransform zoneRect, RectTransform baseRectangle, RectTransform knobRect, float radiusPixels)
        {
            zone = zoneRect;
            baseRect = baseRectangle;
            knob = knobRect;
            radius = radiusPixels;
            restPosition = baseRect.anchoredPosition;
            group = baseRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0.55f;
            group.blocksRaycasts = false;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Active) return;
            pointerId = e.pointerId;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out var local))
                baseRect.anchoredPosition = local;
            group.alpha = 1f;
            UpdateKnob(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointerId) UpdateKnob(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            Release();
        }

        public void Release()
        {
            pointerId = int.MinValue;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (baseRect != null) baseRect.anchoredPosition = restPosition;
            if (group != null) group.alpha = 0.55f;
        }

        void OnDisable() => Release();

        void UpdateKnob(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, e.position, e.pressEventCamera, out var local)) return;
            var clamped = Vector2.ClampMagnitude(local, radius);
            knob.anchoredPosition = clamped;
            var v = clamped / radius;
            // Small dead zone so a resting thumb doesn't drift.
            Value = v.magnitude < 0.12f ? Vector2.zero : v;
        }
    }
}
