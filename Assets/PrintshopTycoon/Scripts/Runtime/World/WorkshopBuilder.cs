using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrintshopTycoon
{
    /// <summary>
    /// Builds the first workshop from WorkshopLayout with placeholder geometry: room, light,
    /// decoration and the fixed stations. Machines are added separately (they can be bought).
    /// </summary>
    public static class WorkshopBuilder
    {
        public class Result
        {
            public Transform Root;
            public OrderBoardStation Board;
            public ShelfStation Shelf;
            public DtfPrinterStation Printer;
            public PackingStation Packing;
            public Transform Silke;
            public Camera Camera;
        }

        public static Result Build()
        {
            var r = new Result();
            r.Root = new GameObject("Workshop").transform;

            BuildLighting(r.Root);
            r.Camera = BuildCamera();
            BuildRoom(r.Root);
            BuildDecor(r.Root);

            r.Board = OrderBoardStation.Build(r.Root, WorkshopLayout.OrderBoard);
            r.Shelf = ShelfStation.Build(r.Root, WorkshopLayout.Shelf);
            r.Printer = DtfPrinterStation.Build(r.Root, WorkshopLayout.DtfPrinter);
            r.Packing = PackingStation.Build(r.Root, WorkshopLayout.PackingTable);

            r.Silke = CharacterFactory.CreateSilkePlaceholder(r.Root);
            r.Silke.position = new Vector3(0.0f, 0f, 6.7f);
            r.Silke.rotation = Quaternion.Euler(0f, 180f, 0f);
            return r;
        }

        static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(55f, 35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.85f, 0.88f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.74f, 0.70f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.50f, 0.46f);
            RenderSettings.fog = false;
        }

        static Camera BuildCamera()
        {
            var existing = Camera.main;
            Camera cam = existing != null ? existing : new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Sky;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            return cam;
        }

        static void BuildRoom(Transform root)
        {
            float hw = WorkshopLayout.HalfWidth, hd = WorkshopLayout.HalfDepth;
            var floor = Shapes.Box(root, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(hw * 2f, 0.1f, hd * 2f), Palette.Floor, true);
            // Checker tiles for a sense of scale and movement.
            for (int x = -3; x < 3; x++)
                for (int z = -4; z < 4; z++)
                    if (((x + z) & 1) == 0)
                        Shapes.Box(floor.transform.parent, "Tile", new Vector3(x * 2f + 1f, 0.002f, z * 2f + 1f), new Vector3(1.98f, 0.004f, 1.98f), Palette.FloorTile);

            // Outside ground so the camera never sees the void.
            Shapes.Box(root, "Ground", new Vector3(0f, -0.12f, 0f), new Vector3(60f, 0.1f, 60f), MaterialLibrary.Hex("#A8D5A2"), true);
            // Invisible blocker in the doorway keeps Hue inside the workshop.
            var blocker = new GameObject("EntranceBlocker");
            blocker.transform.SetParent(root, false);
            Shapes.AddCollider(blocker, new Vector3(0f, 1f, -hd - 0.15f), new Vector3(2.4f, 2f, 0.3f));

            const float wallH = 3.0f, lowWallH = 0.7f, t = 0.3f;
            Wall(root, "WallNorth", new Vector3(0f, wallH * 0.5f, hd + t * 0.5f), new Vector3(hw * 2f + t * 2f, wallH, t));
            Wall(root, "WallWest", new Vector3(-hw - t * 0.5f, wallH * 0.5f, 0f), new Vector3(t, wallH, hd * 2f));
            Wall(root, "WallEast", new Vector3(hw + t * 0.5f, wallH * 0.5f, 0f), new Vector3(t, wallH, hd * 2f));
            // Front wall is low (cut-away) so the camera can look in; gap in the middle = entrance.
            Wall(root, "WallSouthL", new Vector3(-hw * 0.5f - 0.6f, lowWallH * 0.5f, -hd - t * 0.5f), new Vector3(hw - 1.2f + t, lowWallH, t));
            Wall(root, "WallSouthR", new Vector3(hw * 0.5f + 0.6f, lowWallH * 0.5f, -hd - t * 0.5f), new Vector3(hw - 1.2f + t, lowWallH, t));

            // Trim stripes.
            Shapes.Box(root, "TrimNorth", new Vector3(0f, 1.0f, hd - 0.01f), new Vector3(hw * 2f, 0.15f, 0.04f), Palette.WallTrim);
            Shapes.Box(root, "TrimWest", new Vector3(-hw + 0.01f, 1.0f, 0f), new Vector3(0.04f, 0.15f, hd * 2f), Palette.WallTrim);
            Shapes.Box(root, "TrimEast", new Vector3(hw - 0.01f, 1.0f, 0f), new Vector3(0.04f, 0.15f, hd * 2f), Palette.WallTrim);

            // Entrance: door frame and mat.
            Shapes.Box(root, "DoorPostL", new Vector3(-1.2f, 1.1f, -hd - 0.15f), new Vector3(0.2f, 2.2f, 0.3f), Palette.WoodDark);
            Shapes.Box(root, "DoorPostR", new Vector3(1.2f, 1.1f, -hd - 0.15f), new Vector3(0.2f, 2.2f, 0.3f), Palette.WoodDark);
            Shapes.Box(root, "DoorTop", new Vector3(0f, 2.2f, -hd - 0.15f), new Vector3(2.6f, 0.25f, 0.3f), Palette.WoodDark);
            Shapes.Box(root, "Sign", new Vector3(0f, 2.55f, -hd - 0.2f), new Vector3(2.2f, 0.45f, 0.08f), Palette.UiAccent);
            Shapes.Box(root, "DoorMat", new Vector3(0f, 0.01f, -hd + 0.6f), new Vector3(2.0f, 0.02f, 1.0f), MaterialLibrary.Hex("#8D6E63"));

            // Build zone marking (dashed border) so players know where machines can go.
            var a = WorkshopLayout.BuildArea;
            var zone = Shapes.Box(root, "BuildZone", new Vector3(a.centerX, 0.004f, a.centerZ), new Vector3(a.width, 0.006f, a.depth), Palette.BuildZone);
            zone.name = "BuildZone";
            for (float x = a.MinX; x < a.MaxX - 0.1f; x += 0.6f)
            {
                Shapes.Box(root, "Dash", new Vector3(x + 0.2f, 0.01f, a.MinZ), new Vector3(0.35f, 0.01f, 0.08f), Palette.WallTrim);
                Shapes.Box(root, "Dash", new Vector3(x + 0.2f, 0.01f, a.MaxZ), new Vector3(0.35f, 0.01f, 0.08f), Palette.WallTrim);
            }
            for (float z = a.MinZ; z < a.MaxZ - 0.1f; z += 0.6f)
            {
                Shapes.Box(root, "Dash", new Vector3(a.MinX, 0.01f, z + 0.2f), new Vector3(0.08f, 0.01f, 0.35f), Palette.WallTrim);
                Shapes.Box(root, "Dash", new Vector3(a.MaxX, 0.01f, z + 0.2f), new Vector3(0.08f, 0.01f, 0.35f), Palette.WallTrim);
            }
        }

        static void Wall(Transform root, string name, Vector3 pos, Vector3 size)
        {
            var w = Shapes.Box(root, name, pos, size, Palette.Wall, true);
            w.AddComponent<CameraOccluder>();
        }

        static void BuildDecor(Transform root)
        {
            float hd = WorkshopLayout.HalfDepth, hw = WorkshopLayout.HalfWidth;
            // Windows on the back wall.
            foreach (var x in new[] { 3.6f, -3.8f })
            {
                Shapes.Box(root, "WindowFrame", new Vector3(x, 2.0f, hd - 0.02f), new Vector3(1.5f, 1.0f, 0.06f), Color.white);
                Shapes.Box(root, "WindowGlass", new Vector3(x, 2.0f, hd - 0.05f), new Vector3(1.3f, 0.8f, 0.04f), MaterialLibrary.Hex("#9ED8F0"));
            }
            // Posters with "print samples".
            Shapes.Box(root, "PosterA", new Vector3(-hw + 0.03f, 1.9f, 2.5f), new Vector3(0.04f, 0.9f, 0.7f), MaterialLibrary.Hex("#FFADAD"));
            Shapes.Box(root, "PosterB", new Vector3(-hw + 0.03f, 1.9f, 0.8f), new Vector3(0.04f, 0.9f, 0.7f), MaterialLibrary.Hex("#A0C4FF"));
            Shapes.Box(root, "PosterC", new Vector3(hw - 0.03f, 1.9f, 6.2f), new Vector3(0.04f, 0.9f, 0.7f), MaterialLibrary.Hex("#CAFFBF"));
            // Plants.
            foreach (var p in new[] { new Vector3(-5.4f, 0f, -7.2f), new Vector3(5.4f, 0f, -7.2f), new Vector3(-5.4f, 0f, 1.0f) })
            {
                Shapes.Cylinder(root, "Pot", p + Vector3.up * 0.25f, 0.5f, 0.5f, MaterialLibrary.Hex("#E07A5F"));
                Shapes.Sphere(root, "Leaves", p + Vector3.up * 0.8f, 0.8f, Palette.Plant);
                Shapes.Sphere(root, "Leaves", p + new Vector3(0.15f, 1.15f, 0.1f), 0.5f, Palette.Plant);
            }
            // Rug in the walkway.
            Shapes.Box(root, "Rug", new Vector3(-0.6f, 0.008f, 1.5f), new Vector3(2.0f, 0.01f, 5.0f), MaterialLibrary.Hex("#F4A261"));
            // A stack of boxes near the shelf for flavour.
            Shapes.Box(root, "Boxes", new Vector3(-5.3f, 0.3f, 4.0f), new Vector3(0.7f, 0.6f, 0.7f), Palette.Cardboard, true);
            Shapes.Box(root, "Boxes", new Vector3(-5.3f, 0.85f, 4.05f), new Vector3(0.55f, 0.5f, 0.55f), Palette.Cardboard);
        }
    }
}
