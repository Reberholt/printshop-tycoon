using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Printshop Tycoon's own identity. Design rule: no real-world company branding anywhere in
    /// the game (no Tex-Tryk logos, signs, prints or packaging). Machines carry fictional brands
    /// until licensed models from real manufacturers replace them (see Docs: swappable models).
    /// </summary>
    public static class Branding
    {
        public const string GameName = "Printshop Tycoon";

        /// <summary>Warm coral - buttons, highlights, the shop's own colour.</summary>
        public static readonly Color Primary = MaterialLibrary.Hex("#FF6B3D");
        /// <summary>Teal - the ink-drop mark and secondary accents.</summary>
        public static readonly Color Secondary = MaterialLibrary.Hex("#2EC4B6");
        public static readonly Color Dark = MaterialLibrary.Hex("#23252B");

        // Fictional machine brands (placeholders for licensed real machines later).
        public const string HeatPressBrand = "PRESSO";
        public const string PrinterBrand = "INKRA";

        /// <summary>
        /// The game's logo mark: a round badge with an ink drop. Built from primitives so it
        /// works on characters, machines, boxes and walls. Faces -z (towards the viewer).
        /// </summary>
        public static void Mark(Transform parent, Vector3 center, float size, Color badge, Color drop)
        {
            var ring = Shapes.Cylinder(parent, "MarkBadge", center, size, 0.02f, badge);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var d = Shapes.Sphere(parent, "MarkDrop", center + new Vector3(0f, -size * 0.08f, -0.012f), size * 0.5f, drop);
            d.transform.localScale = new Vector3(size * 0.45f, size * 0.55f, 0.02f);
            var tip = Shapes.Box(parent, "MarkTip", center + new Vector3(0f, size * 0.26f, -0.012f), new Vector3(size * 0.16f, size * 0.22f, 0.02f), drop);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        public static void Mark(Transform parent, Vector3 center, float size) => Mark(parent, center, size, Secondary, Color.white);
    }
}
