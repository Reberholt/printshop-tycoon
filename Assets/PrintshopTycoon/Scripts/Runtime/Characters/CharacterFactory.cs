using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Low-poly stand-ins for Hue and Silke, styled after the official reference art
    /// (Art/Reference): black beanie with red T, grey hoodie, black Tex-Tryk overalls,
    /// chunky black boots with beige soles, big round nose, big chibi head.
    /// Built as a tiny rig (Hips/LegL/LegR/ArmL/ArmR/Head pivots) so CharacterAnimator can
    /// walk, idle and work. Real 3D models can replace "Visual" later without touching gameplay.
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
            public float scale;
        }

        public static Transform CreateHue(Transform parent)
        {
            return Create(parent, "Hue", new Style { hoodie = Hoodie, hair = HueHair, ponytail = false, scale = 1f });
        }

        public static Transform CreateSilke(Transform parent)
        {
            var t = Create(parent, "Silke", new Style { hoodie = Hoodie, hair = SilkeHair, ponytail = true, scale = 0.97f });
            // Clipboard in her right hand: Silke is the organised one.
            var hand = t.Find("Visual/ArmR");
            Shapes.Box(hand, "Clipboard", new Vector3(0f, -0.42f, -0.12f), new Vector3(0.26f, 0.34f, 0.04f), Palette.Wood);
            Shapes.Box(hand, "Paper", new Vector3(0f, -0.42f, -0.145f), new Vector3(0.2f, 0.26f, 0.01f), Color.white);
            return t;
        }

        /// <summary>Staff wear the same uniform with a coloured hoodie so you can tell them apart.</summary>
        public static Transform CreateWorker(Transform parent, string name, Color hoodie)
        {
            return Create(parent, name, new Style { hoodie = hoodie, hair = MaterialLibrary.Hex("#3B2A20"), ponytail = false, scale = 0.92f });
        }

        static Transform Pivot(Transform parent, string name, Vector3 localPos)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = localPos;
            return p;
        }

        static Transform Create(Transform parent, string name, Style s)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var v = new GameObject("Visual").transform;
            v.SetParent(root, false);
            v.localScale = Vector3.one * s.scale;

            // Legs (pivot at the hip) with chunky boots and beige soles.
            foreach (var side in new[] { -1f, 1f })
            {
                var leg = Pivot(v, side < 0 ? "LegL" : "LegR", new Vector3(side * 0.14f, 0.46f, 0f));
                Shapes.Capsule(leg, "Leg", new Vector3(0f, -0.2f, 0f), 0.24f, 0.42f, Overalls);
                Shapes.Box(leg, "Sole", new Vector3(0f, -0.42f, -0.05f), new Vector3(0.26f, 0.08f, 0.38f), Sole);
                Shapes.Sphere(leg, "Boot", new Vector3(0f, -0.33f, -0.06f), 0.3f, Boot).transform.localScale = new Vector3(0.28f, 0.2f, 0.38f);
            }

            // Torso: hoodie, overalls, bib with the Tex-Tryk T, tool pocket with red pen.
            var hips = Pivot(v, "Hips", new Vector3(0f, 0.46f, 0f));
            Shapes.Capsule(hips, "Body", new Vector3(0f, 0.32f, 0f), 0.62f, 0.72f, s.hoodie);
            Shapes.Box(hips, "Overalls", new Vector3(0f, 0.14f, 0f), new Vector3(0.58f, 0.36f, 0.5f), Overalls);
            Shapes.Box(hips, "Bib", new Vector3(0f, 0.42f, -0.24f), new Vector3(0.36f, 0.3f, 0.06f), Overalls);
            foreach (var x in new[] { -0.15f, 0.15f })
                Shapes.Box(hips, "Strap", new Vector3(x, 0.55f, -0.2f), new Vector3(0.06f, 0.2f, 0.06f), Overalls);
            BrandT(hips, new Vector3(0f, 0.45f, -0.275f), 0.13f);
            Shapes.Box(hips, "Pocket", new Vector3(-0.27f, 0.08f, -0.12f), new Vector3(0.1f, 0.16f, 0.2f), Overalls);
            Shapes.Box(hips, "Pen", new Vector3(-0.27f, 0.19f, -0.18f), new Vector3(0.035f, 0.1f, 0.035f), BrandRed);
            Shapes.Sphere(hips, "Hood", new Vector3(0f, 0.66f, 0.1f), 0.5f, s.hoodie).transform.localScale = new Vector3(0.55f, 0.25f, 0.45f);

            // Arms (pivot at the shoulder) in the hoodie with round hands.
            foreach (var side in new[] { -1f, 1f })
            {
                var arm = Pivot(v, side < 0 ? "ArmL" : "ArmR", new Vector3(side * 0.37f, 1.04f, 0f));
                Shapes.Capsule(arm, "Sleeve", new Vector3(0f, -0.22f, 0f), 0.2f, 0.48f, s.hoodie);
                Shapes.Sphere(arm, "Hand", new Vector3(0f, -0.48f, -0.02f), 0.17f, Skin);
            }

            // Big head on a neck pivot: ears, big round nose, eyes with highlights, smile.
            var head = Pivot(v, "Head", new Vector3(0f, 1.12f, 0f));
            Shapes.Sphere(head, "Face", new Vector3(0f, 0.32f, 0f), 0.66f, Skin);
            foreach (var x in new[] { -0.33f, 0.33f })
                Shapes.Sphere(head, "Ear", new Vector3(x, 0.3f, 0.02f), 0.15f, Skin);
            Shapes.Sphere(head, "Nose", new Vector3(0f, 0.27f, -0.32f), 0.19f, Nose);
            foreach (var x in new[] { -0.13f, 0.13f })
            {
                Shapes.Sphere(head, "Eye", new Vector3(x, 0.38f, -0.29f), 0.1f, Palette.UiDark).transform.localScale = new Vector3(0.09f, 0.13f, 0.06f);
                Shapes.Sphere(head, "Shine", new Vector3(x + 0.02f, 0.41f, -0.32f), 0.03f, Color.white);
                Shapes.Sphere(head, "Cheek", new Vector3(x * 1.7f, 0.22f, -0.24f), 0.1f, MaterialLibrary.Hex("#F4A99A")).transform.localScale = new Vector3(0.1f, 0.06f, 0.04f);
            }
            Shapes.Box(head, "Smile", new Vector3(0f, 0.15f, -0.3f), new Vector3(0.16f, 0.045f, 0.03f), MaterialLibrary.Hex("#9E3B32"));

            // Hair peeking out under the beanie (+ Silke's big wavy ponytail).
            foreach (var x in new[] { -0.29f, 0.29f })
                Shapes.Sphere(head, "Hair", new Vector3(x, 0.42f, -0.05f), 0.2f, s.hair).transform.localScale = new Vector3(0.14f, s.ponytail ? 0.34f : 0.16f, 0.2f);
            if (s.ponytail)
            {
                Shapes.Sphere(head, "Ponytail", new Vector3(0.08f, 0.76f, 0.24f), 0.34f, s.hair);
                Shapes.Sphere(head, "Ponytail", new Vector3(0.22f, 0.54f, 0.36f), 0.3f, s.hair);
                Shapes.Sphere(head, "Ponytail", new Vector3(0.27f, 0.32f, 0.38f), 0.24f, s.hair);
                Shapes.Sphere(head, "Curl", new Vector3(0.22f, 0.14f, 0.34f), 0.16f, s.hair);
            }

            // Black beanie with turned-up rim and the red T.
            Shapes.Sphere(head, "Beanie", new Vector3(0f, 0.53f, 0.02f), 0.68f, Beanie).transform.localScale = new Vector3(0.7f, 0.5f, 0.68f);
            var rim = Shapes.Cylinder(head, "BeanieRim", new Vector3(0f, 0.5f, 0f), 0.72f, 0.16f, Beanie);
            rim.transform.localScale = new Vector3(0.72f, 0.085f, 0.7f);
            if (!s.ponytail)
                Shapes.Sphere(head, "BeanieTop", new Vector3(-0.05f, 0.78f, 0.06f), 0.36f, Beanie).transform.localScale = new Vector3(0.42f, 0.3f, 0.4f);
            BrandT(head, new Vector3(0f, 0.53f, -0.355f), 0.12f);

            // Built facing -z; rotate so the face points along the root's forward.
            v.localRotation = Quaternion.Euler(0f, 180f, 0f);
            root.gameObject.AddComponent<CharacterAnimator>();
            return root;
        }

        /// <summary>The red Tex-Tryk "T" made from two boxes.</summary>
        public static void BrandT(Transform parent, Vector3 center, float size) => BrandTOnWall(parent, center, size, BrandRed);

        /// <summary>The Tex-Tryk "T" in any colour (white on red banners).</summary>
        public static void BrandTOnWall(Transform parent, Vector3 center, float size, Color color)
        {
            Shapes.Box(parent, "T_Top", center + new Vector3(0f, size * 0.4f, 0f), new Vector3(size, size * 0.22f, 0.02f), color);
            Shapes.Box(parent, "T_Stem", center + new Vector3(0f, -size * 0.08f, 0f), new Vector3(size * 0.26f, size * 0.8f, 0.02f), color);
        }
    }
}
