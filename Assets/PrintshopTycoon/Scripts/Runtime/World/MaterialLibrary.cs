using System.Collections.Generic;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Shares one material per colour. The base material is created by the editor setup in
    /// Resources so the URP shader is included in mobile builds; Shader.Find is a fallback.
    /// </summary>
    public static class MaterialLibrary
    {
        public const string BaseMaterialPath = "PrintshopTycoon/PT_Base";

        static Material baseMaterial;
        static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();

        public static Material Get(Color color)
        {
            Color32 key = color;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Base()) { name = "PT_" + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        }

        static Material Base()
        {
            if (baseMaterial != null) return baseMaterial;
            baseMaterial = Resources.Load<Material>(BaseMaterialPath);
            if (baseMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                baseMaterial = new Material(shader);
                Debug.LogWarning("[PrintshopTycoon] Base material missing - run 'Printshop Tycoon > Setup Project' in the editor.");
            }
            if (baseMaterial.HasProperty("_Smoothness")) baseMaterial.SetFloat("_Smoothness", 0.25f);
            if (baseMaterial.HasProperty("_Glossiness")) baseMaterial.SetFloat("_Glossiness", 0.25f);
            return baseMaterial;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }
    }

    /// <summary>Colour palette for the cute, bright workshop look.</summary>
    public static class Palette
    {
        public static readonly Color Floor = MaterialLibrary.Hex("#F3E3C7");
        public static readonly Color FloorTile = MaterialLibrary.Hex("#EAD5B2");
        public static readonly Color Wall = MaterialLibrary.Hex("#FFF6EA");
        public static readonly Color WallTrim = MaterialLibrary.Hex("#7FC8C2");
        public static readonly Color Wood = MaterialLibrary.Hex("#C98B5A");
        public static readonly Color WoodDark = MaterialLibrary.Hex("#9C6640");
        public static readonly Color Metal = MaterialLibrary.Hex("#9AA7B5");
        public static readonly Color MetalDark = MaterialLibrary.Hex("#5E6B7A");
        public static readonly Color PressRed = MaterialLibrary.Hex("#E8505B");
        public static readonly Color PrinterBlue = MaterialLibrary.Hex("#3D7DD8");
        public static readonly Color PackGreen = MaterialLibrary.Hex("#5CC27A");
        public static readonly Color BoardYellow = MaterialLibrary.Hex("#FFC93C");
        public static readonly Color Shirt = MaterialLibrary.Hex("#FFFFFF");
        public static readonly Color Cardboard = MaterialLibrary.Hex("#D9A066");
        public static readonly Color Plant = MaterialLibrary.Hex("#4CAF6A");
        public static readonly Color Marker = MaterialLibrary.Hex("#FFD23F");
        public static readonly Color BuildZone = MaterialLibrary.Hex("#BFE6E2");
        public static readonly Color Valid = MaterialLibrary.Hex("#6BE08A");
        public static readonly Color Invalid = MaterialLibrary.Hex("#FF6B6B");
        public static readonly Color Sky = MaterialLibrary.Hex("#BFE3F2");

        // UI
        public static readonly Color UiDark = MaterialLibrary.Hex("#2B2D42");
        public static readonly Color UiPanel = MaterialLibrary.Hex("#FFFDF7");
        public static readonly Color UiAccent = MaterialLibrary.Hex("#FF8C42");
        public static readonly Color UiBlue = MaterialLibrary.Hex("#3D7DD8");
        public static readonly Color UiGreen = MaterialLibrary.Hex("#3DBE6B");
        public static readonly Color UiRed = MaterialLibrary.Hex("#E8505B");
        public static readonly Color UiGrey = MaterialLibrary.Hex("#B8BCC6");
        public static readonly Color UiCoin = MaterialLibrary.Hex("#FFC93C");
        public static readonly Color UiPurple = MaterialLibrary.Hex("#8E6CD8");
    }
}
