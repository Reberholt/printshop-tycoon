using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>Builds the placeholder geometry from Unity primitives.</summary>
    public static class Shapes
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = MaterialLibrary.Get(color);
            if (!keepCollider)
            {
                var c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Color color, bool collider = false)
            => Prim(PrimitiveType.Cube, parent, name, pos, size, color, collider);

        public static GameObject Cylinder(Transform parent, string name, Vector3 pos, float diameter, float height, Color color)
            => Prim(PrimitiveType.Cylinder, parent, name, pos, new Vector3(diameter, height * 0.5f, diameter), color);

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, float diameter, Color color)
            => Prim(PrimitiveType.Sphere, parent, name, pos, Vector3.one * diameter, color);

        public static GameObject Capsule(Transform parent, string name, Vector3 pos, float diameter, float height, Color color)
            => Prim(PrimitiveType.Capsule, parent, name, pos, new Vector3(diameter, height * 0.5f, diameter), color);

        /// <summary>A box with soft rounded edges (box core + cylinder corners), cheap but friendly.</summary>
        public static GameObject RoundedBox(Transform parent, string name, Vector3 pos, Vector3 size, float radius, Color color)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            radius = Mathf.Min(radius, size.x * 0.5f, size.z * 0.5f);
            Box(root, "CoreX", Vector3.zero, new Vector3(size.x - radius * 2f, size.y, size.z), color);
            Box(root, "CoreZ", Vector3.zero, new Vector3(size.x, size.y, size.z - radius * 2f), color);
            float hx = size.x * 0.5f - radius, hz = size.z * 0.5f - radius;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Cylinder(root, "Corner", new Vector3(sx * hx, 0f, sz * hz), radius * 2f, size.y, color);
            return root.gameObject;
        }

        public static BoxCollider AddCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            return c;
        }
    }
}
