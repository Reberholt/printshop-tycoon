using System;
using UnityEngine;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>
    /// Code-built uGUI helpers. Reference resolution is 1080x1920 portrait; sizes below are in
    /// those reference pixels. Rounded sprites are generated, so no art assets are required.
    /// </summary>
    public static class UIFactory
    {
        static Font font;
        static Sprite rounded;
        static Sprite circle;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                // Unity 2022.2+ / Unity 6 ship LegacyRuntime.ttf; older versions ship Arial.ttf.
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { font = null; }
                if (font == null)
                {
                    try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { font = null; }
                }
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 32);
                return font;
            }
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 24));
        public static Sprite Circle => circle != null ? circle : (circle = MakeRounded(128, 64));

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor at a normalized point with a fixed size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch horizontally between anchors, fixed height.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float top, float height, float sideMargin)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideMargin, -top - height);
            rt.offsetMax = new Vector2(-sideMargin, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, bool roundedSprite = true, bool raycast = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            if (roundedSprite)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Image CircleImage(Transform parent, string name, Color color, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(18, size / 2);
            t.resizeTextMaxSize = size;
            return t;
        }

        public static Button Button(Transform parent, string name, string text, Color color, Action onClick, int fontSize = 46, Color? textColor = null)
        {
            var img = Panel(parent, name, color);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var label = Label(img.transform, "Label", text, fontSize, textColor ?? Color.white);
            label.rectTransform.Stretch(16, 8, 16, 8);
            return btn;
        }

        public static Text LabelOf(Button b) => b.GetComponentInChildren<Text>();

        public static VerticalLayoutGroup VerticalList(RectTransform rt, float spacing, int padding)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        public static LayoutElement Height(Component c, float height)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return le;
        }
    }
}
