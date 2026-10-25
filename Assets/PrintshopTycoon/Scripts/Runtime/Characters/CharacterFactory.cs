using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Low-poly stand-ins for Hue and Silke, styled after the official reference art
    /// (Art/Reference): black beanie with red T, grey hoodie, black Tex-Tryk overalls,
    /// chunky black boots with beige soles, big round nose. Proper 3D models built from the
    /// same references can replace these later; gameplay only uses the root and its "Visual" child.
    /// </summary>
    public static class CharacterFactory
    {
        public static readonly Color Skin = MaterialLibrary.Hex("#F2C29B");
        public static readonly Color Nose = MaterialLibrary.Hex("#EFA27C");
        public static readonly Color Beanie = MaterialLibrary.Hex("#1F1F22");
        public static readonly Color BrandRed = MaterialLibrary.Hex("#E3191B");
        public static readonly Color Hoodie = MaterialLibrary.Hex("#A3A3A6");
        public static readonly Color Overalls = MaterialLibrary.Hex("#242427");
        public static readonly Color Boot = MaterialLibrary.Hex("#1B1B1D");
        public static readonly Color Sole = MaterialLibrary.Hex("#D9C5A8");
        public static readonly Color HueHair = MaterialLibrary.Hex("#6B3E1F");
        public static readonly Color SilkeHair = MaterialLibrary.Hex("#E2B46A");

        struct Style
        {
            public Color hoodie;
            public Color hair;
            public bool ponytail;
            public bool brandPatch;
            public float scale;
        }

        public static Transform CreateHue(Transform parent)
        {
            return Create(parent, "Hue", new Style { hoodie = Hoodie, hair = HueHair, ponytail = false, brandPatch = true, scale = 1f });
        }

        public static Transform CreateSilke(Transform parent)
        {
            var t = Create(parent, "Silke", new Style { hoodie = Hoodie, hair = SilkeHair, ponytail = true, brandPatch = true, scale = 0.97f });
            // Clipboard: Silke is the organised one.
            var visual = t.Find("Visual");
            Shapes.Box(visual, "Clipboard", new Vector3(0.36f, 0.82f, -0.2f), new Vector3(0.26f, 0.34f, 0.04f), Palette.Wood);
            Shapes.Box(visual, "Paper", new Vector3(0.36f, 0.82f, -0.225f), new Vector3(0.2f, 0.26f, 0.01f), Color.white);
            return t;
        }

        /// <summary>Staff wear the same uniform with a coloured hoodie so you can tell them apart.</summary>
        public static Transform CreateWorker(Transform parent, string name, Color hoodie)
        {
            return Create(parent, name, new Style { hoodie = hoodie, hair = MaterialLibrary.Hex("#3B2A20"), ponytail = false, brandPatch = true, scale = 0.92f });
        }

        static Transform Create(Transform parent, string name, Style s)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var v = new GameObject("Visual").transform;
            v.SetParent(root, false);
            v.localScale = Vector3.one * s.scale;

            // Boots with beige soles (chunky, like the art).
            foreach (var x in new[] { -0.15f, 0.15f })
            {
                Shapes.Box(v, "Sole", new Vector3(x, 0.04f, -0.04f), new Vector3(0.26f, 0.08f, 0.38f), Sole);
                Shapes.Sphere(v, "Boot", new Vector3(x, 0.14f, -0.05f), 0.3f, Boot).transform.localScale = new Vector3(0.28f, 0.2f, 0.38f);
            }
            // Overalls: legs + body + bib with the Tex-Tryk T.
            Shapes.Capsule(v, "Legs", new Vector3(0f, 0.38f, 0f), 0.46f, 0.55f, Overalls);
            Shapes.Capsule(v, "Body", new Vector3(0f, 0.78f, 0f), 0.62f, 0.75f, s.hoodie);
            Shapes.Box(v, "Overalls", new Vector3(0f, 0.62f, 0f), new Vector3(0.58f, 0.42f, 0.5f), Overalls);
            Shapes.Box(v, "Bib", new Vector3(0f, 0.9f, -0.24f), new Vector3(0.36f, 0.3f, 0.06f), Overalls);
            foreach (var x in new[] { -0.15f, 0.15f })
                Shapes.Box(v, "Strap", new Vector3(x, 1.03f, -0.2f), new Vector3(0.06f, 0.2f, 0.06f), Overalls);
            if (s.brandPatch) BrandT(v, new Vector3(0f, 0.93f, -0.275f), 0.13f);
            Shapes.Box(v, "Pocket", new Vector3(-0.27f, 0.55f, -0.12f), new Vector3(0.1f, 0.16f, 0.2f), Overalls);
            Shapes.Box(v, "Pen", new Vector3(-0.27f, 0.66f, -0.18f), new Vector3(0.035f, 0.1f, 0.035f), BrandRed);

            // Arms in the hoodie, round hands.
            foreach (var x in new[] { -0.38f, 0.38f })
            {
                Shapes.Capsule(v, "Arm", new Vector3(x, 0.8f, 0f), 0.2f, 0.5f, s.hoodie);
                Shapes.Sphere(v, "Hand", new Vector3(x, 0.52f, -0.02f), 0.17f, Skin);
            }
            Shapes.Sphere(v, "Hood", new Vector3(0f, 1.12f, 0.1f), 0.5f, s.hoodie).transform.localScale = new Vector3(0.55f, 0.25f, 0.45f);

            // Big head, ears, big round nose, eyes with highlights.
            Shapes.Sphere(v, "Head", new Vector3(0f, 1.42f, 0f), 0.62f, Skin);
            foreach (var x in new[] { -0.31f, 0.31f })
                Shapes.Sphere(v, "Ear", new Vector3(x, 1.4f, 0.02f), 0.14f, Skin);
            Shapes.Sphere(v, "Nose", new Vector3(0f, 1.38f, -0.3f), 0.17f, Nose);
            foreach (var x in new[] { -0.12f, 0.12f })
            {
                Shapes.Sphere(v, "Eye", new Vector3(x, 1.48f, -0.27f), 0.1f, Palette.UiDark).transform.localScale = new Vector3(0.09f, 0.12f, 0.06f);
                Shapes.Sphere(v, "Shine", new Vector3(x + 0.02f, 1.51f, -0.3f), 0.03f, Color.white);
            }
            Shapes.Box(v, "Smile", new Vector3(0f, 1.27f, -0.28f), new Vector3(0.16f, 0.04f, 0.03f), MaterialLibrary.Hex("#9E3B32"));

            // Hair peeking out under the beanie.
            foreach (var x in new[] { -0.27f, 0.27f })
                Shapes.Sphere(v, "Hair", new Vector3(x, 1.52f, -0.05f), 0.2f, s.hair).transform.localScale = new Vector3(0.14f, s.ponytail ? 0.3f : 0.16f, 0.2f);
            if (s.ponytail)
            {
                Shapes.Sphere(v, "Ponytail", new Vector3(0.08f, 1.82f, 0.22f), 0.32f, s.hair);
                Shapes.Sphere(v, "Ponytail", new Vector3(0.2f, 1.62f, 0.34f), 0.28f, s.hair);
                Shapes.Sphere(v, "Ponytail", new Vector3(0.24f, 1.42f, 0.36f), 0.22f, s.hair);
            }

            // Black beanie with turned-up rim and the red T.
            Shapes.Sphere(v, "Beanie", new Vector3(0f, 1.62f, 0.02f), 0.64f, Beanie).transform.localScale = new Vector3(0.66f, 0.48f, 0.64f);
            var rim = Shapes.Cylinder(v, "BeanieRim", new Vector3(0f, 1.6f, 0f), 0.68f, 0.16f, Beanie);
            rim.transform.localScale = new Vector3(0.68f, 0.08f, 0.66f);
            if (!s.ponytail)
                Shapes.Sphere(v, "BeanieTop", new Vector3(-0.05f, 1.86f, 0.06f), 0.36f, Beanie).transform.localScale = new Vector3(0.4f, 0.3f, 0.38f);
            BrandT(v, new Vector3(0f, 1.63f, -0.335f), 0.11f);

            // Characters face -z while building; rotate so the face points along the root's forward.
            v.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return root;
        }

        /// <summary>The red Tex-Tryk "T" made from two boxes.</summary>
        static void BrandT(Transform parent, Vector3 center, float size)
        {
            Shapes.Box(parent, "T_Top", center + new Vector3(0f, size * 0.4f, 0f), new Vector3(size, size * 0.22f, 0.02f), BrandRed);
            Shapes.Box(parent, "T_Stem", center + new Vector3(0f, -size * 0.08f, 0f), new Vector3(size * 0.26f, size * 0.8f, 0.02f), BrandRed);
        }
    }
}
