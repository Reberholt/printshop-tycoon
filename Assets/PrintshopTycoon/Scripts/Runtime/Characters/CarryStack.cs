using PrintshopTycoon.Core;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>How a product category looks when folded (colour, thickness, reflective stripes).</summary>
    public struct ProductLook
    {
        public Color Body;
        public float Thickness;
        public bool Stripes;

        public static ProductLook For(ProductDefinition p)
        {
            if (p == null) return new ProductLook { Body = Color.white, Thickness = 0.06f };
            return new ProductLook
            {
                Body = MaterialLibrary.Hex(p.colorHex),
                Thickness = p.shape == "sweatshirt" ? 0.1f : (p.shape == "vest" ? 0.05f : 0.06f),
                Stripes = p.shape == "vest",
            };
        }

        public bool Same(ProductLook o) => Body == o.Body && Mathf.Approximately(Thickness, o.Thickness) && Stripes == o.Stripes;
    }

    /// <summary>Folded garments built from boxes: body, collar, optional logo print and reflective stripes.</summary>
    public static class GarmentFactory
    {
        public static readonly Color Reflective = MaterialLibrary.Hex("#D9DEE3");
        public static readonly Color TransferFilm = MaterialLibrary.Hex("#E8F4FF");

        public static GameObject Folded(Transform parent, string name, Vector3 localPos, ProductLook look, Color? logo, float width = 0.45f, float depth = 0.36f)
        {
            var g = Shapes.Box(parent, name, localPos, new Vector3(width, look.Thickness, depth), look.Body);
            // Children are in the body's scaled space: express sizes relative to the body box.
            var collar = Shapes.Box(g.transform, "Collar", new Vector3(0f, 0.55f, 0.36f), new Vector3(0.36f, 0.25f, 0.16f), Darker(look.Body));
            collar.name = "Collar";
            if (look.Stripes)
            {
                Shapes.Box(g.transform, "Stripe", new Vector3(0f, 0.55f, -0.05f), new Vector3(1.02f, 0.2f, 0.12f), Reflective);
                Shapes.Box(g.transform, "Stripe", new Vector3(0f, 0.55f, -0.32f), new Vector3(1.02f, 0.2f, 0.12f), Reflective);
            }
            if (logo.HasValue)
                Shapes.Box(g.transform, "Print", new Vector3(0f, 0.6f, -0.05f), new Vector3(0.45f, 0.25f, 0.42f), logo.Value);
            return g;
        }

        static Color Darker(Color c) => Color.Lerp(c, Color.black, 0.15f);
    }

    /// <summary>Stack of garments carried in front of a character.</summary>
    public class CarryStack : MonoBehaviour
    {
        const int MaxVisible = 6;
        readonly GameObject[] items = new GameObject[MaxVisible];
        int shownCount = -1;
        ProductLook shownLook;
        Color? shownPrint;
        Color? shownFilm;
        Transform film;
        Renderer filmLogoRenderer;

        public int Count => Mathf.Max(0, shownCount);

        public static CarryStack Create(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("CarryStack").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            var stack = root.gameObject.AddComponent<CarryStack>();
            stack.film = Shapes.Box(root, "TransferSheets", Vector3.zero, new Vector3(0.4f, 0.02f, 0.32f), GarmentFactory.TransferFilm).transform;
            stack.filmLogoRenderer = Shapes.Box(stack.film, "Logo", new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Color.red).GetComponent<Renderer>();
            stack.film.gameObject.SetActive(false);
            return stack;
        }

        /// <summary>
        /// count = garments carried, look = product, printColor = logo colour on the garments
        /// (null = blanks), filmLogo = DTF transfer sheets with this logo lying on top.
        /// </summary>
        public void Show(int count, ProductLook look, Color? printColor, Color? filmLogo = null)
        {
            int visible = Mathf.Clamp(count, 0, MaxVisible);
            if (visible == shownCount && look.Same(shownLook) && printColor == shownPrint && filmLogo == shownFilm) return;
            bool rebuild = !look.Same(shownLook) || printColor != shownPrint;
            shownCount = visible; shownLook = look; shownPrint = printColor; shownFilm = filmLogo;
            float y = 0f;
            for (int i = 0; i < MaxVisible; i++)
            {
                if (rebuild && items[i] != null) { Destroy(items[i]); items[i] = null; }
                if (items[i] == null)
                    items[i] = GarmentFactory.Folded(transform, "Garment", new Vector3(0f, y, 0f), look, printColor);
                items[i].transform.localPosition = new Vector3((i % 2) * 0.015f, y, 0f);
                items[i].SetActive(i < visible);
                if (i < visible) y += look.Thickness + 0.008f;
            }
            film.gameObject.SetActive(filmLogo.HasValue && visible > 0);
            film.localPosition = new Vector3(0f, y + 0.01f, 0f);
            if (filmLogo.HasValue) filmLogoRenderer.sharedMaterial = MaterialLibrary.Get(filmLogo.Value);
        }

        /// <summary>Old signature: T-shirt look.</summary>
        public void Show(int count, Color shirtColor, Color? printColor)
        {
            Show(count, new ProductLook { Body = shirtColor, Thickness = 0.06f }, printColor);
        }
    }
}
