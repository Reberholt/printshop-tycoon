using System;
using UnityEngine;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>Keeps its RectTransform inside Screen.safeArea (notches, rounded corners, home bar).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect applied;
        Vector2Int appliedScreen;

        void Update() => Apply();

        public void Apply()
        {
            var safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == applied && screen == appliedScreen) return;
            applied = safe;
            appliedScreen = screen;
            if (screen.x <= 0 || screen.y <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rt.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
