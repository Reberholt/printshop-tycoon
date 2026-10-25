using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// TEMPORARY placeholder characters. Hue and Silke are existing brand mascots - their real
    /// look must come from the owner's reference art. Replace these with proper models later;
    /// gameplay only needs the root transform and the "Visual" child.
    /// </summary>
    public static class CharacterFactory
    {
        public static Transform CreateHuePlaceholder(Transform parent)
        {
            return CreateCharacter(parent, "Hue_PLACEHOLDER",
                body: MaterialLibrary.Hex("#FF8C42"), pants: MaterialLibrary.Hex("#2B2D42"),
                skin: MaterialLibrary.Hex("#F5CBA7"), hat: MaterialLibrary.Hex("#3D7DD8"), height: 1.0f, hasCap: true);
        }

        public static Transform CreateSilkePlaceholder(Transform parent)
        {
            var t = CreateCharacter(parent, "Silke_PLACEHOLDER",
                body: MaterialLibrary.Hex("#8E6CD8"), pants: MaterialLibrary.Hex("#2B2D42"),
                skin: MaterialLibrary.Hex("#F5CBA7"), hat: MaterialLibrary.Hex("#6B3E26"), height: 0.97f, hasCap: false);
            // Ponytail + clipboard so she reads as "the organised one".
            var visual = t.Find("Visual");
            Shapes.Sphere(visual, "Ponytail", new Vector3(0f, 1.55f, 0.25f), 0.24f, MaterialLibrary.Hex("#6B3E26"));
            Shapes.Box(visual, "Clipboard", new Vector3(0.32f, 0.85f, -0.18f), new Vector3(0.28f, 0.36f, 0.04f), Palette.Wood);
            return t;
        }

        public static Transform CreateWorker(Transform parent, string name, Color shirt)
        {
            return CreateCharacter(parent, name, shirt, MaterialLibrary.Hex("#4A4E69"),
                MaterialLibrary.Hex("#E0AC69"), MaterialLibrary.Hex("#FFC93C"), 0.92f, true);
        }

        static Transform CreateCharacter(Transform parent, string name, Color body, Color pants, Color skin, Color hat, float height, bool hasCap)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            visual.localScale = Vector3.one * height;

            Shapes.Capsule(visual, "Legs", new Vector3(0f, 0.35f, 0f), 0.42f, 0.7f, pants);
            Shapes.Capsule(visual, "Body", new Vector3(0f, 0.8f, 0f), 0.6f, 0.8f, body);
            Shapes.Sphere(visual, "Head", new Vector3(0f, 1.42f, 0f), 0.56f, skin);
            Shapes.Sphere(visual, "EyeL", new Vector3(-0.11f, 1.47f, -0.25f), 0.09f, Palette.UiDark);
            Shapes.Sphere(visual, "EyeR", new Vector3(0.11f, 1.47f, -0.25f), 0.09f, Palette.UiDark);
            if (hasCap)
            {
                Shapes.Sphere(visual, "Cap", new Vector3(0f, 1.6f, 0.02f), 0.54f, hat).transform.localScale = new Vector3(0.56f, 0.3f, 0.56f);
                Shapes.Box(visual, "Visor", new Vector3(0f, 1.58f, -0.3f), new Vector3(0.4f, 0.05f, 0.24f), hat);
            }
            else
            {
                Shapes.Sphere(visual, "Hair", new Vector3(0f, 1.55f, 0.06f), 0.58f, hat).transform.localScale = new Vector3(0.6f, 0.42f, 0.6f);
            }
            Shapes.Capsule(visual, "ArmL", new Vector3(-0.38f, 0.82f, 0f), 0.16f, 0.55f, body);
            Shapes.Capsule(visual, "ArmR", new Vector3(0.38f, 0.82f, 0f), 0.16f, 0.55f, body);
            // Characters face -z (towards the camera) by default; rotate the root to turn.
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return root;
        }
    }
}
