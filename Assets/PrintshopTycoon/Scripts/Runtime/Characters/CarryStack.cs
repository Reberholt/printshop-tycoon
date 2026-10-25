using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace PrintshopTycoon
{
    /// <summary>Stack of shirts carried in front of a character.</summary>
    public class CarryStack : MonoBehaviour
    {
        const int MaxVisible = 5;
        readonly GameObject[] shirts = new GameObject[MaxVisible];
        readonly GameObject[] prints = new GameObject[MaxVisible];
        int shownCount = -1;
        Color shownColor;
        Color? shownPrint;

        public static CarryStack Create(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("CarryStack").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            var stack = root.gameObject.AddComponent<CarryStack>();
            for (int i = 0; i < MaxVisible; i++)
            {
                stack.shirts[i] = Shapes.Box(root, "Shirt", new Vector3(0f, i * 0.07f, 0f), new Vector3(0.45f, 0.06f, 0.36f), Palette.Shirt);
                stack.prints[i] = Shapes.Box(stack.shirts[i].transform, "Print", new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.3f, 0.5f), Palette.UiAccent);
                stack.shirts[i].SetActive(false);
            }
            return stack;
        }

        /// <summary>count = shirts carried, printColor = logo colour (or null for blanks).</summary>
        public void Show(int count, Color shirtColor, Color? printColor)
        {
            int visible = Mathf.Clamp(count, 0, MaxVisible);
            bool hasPrint = printColor.HasValue;
            if (visible == shownCount && shirtColor == shownColor && printColor == shownPrint) return;
            shownCount = visible; shownColor = shirtColor; shownPrint = printColor;
            for (int i = 0; i < MaxVisible; i++)
            {
                shirts[i].SetActive(i < visible);
                shirts[i].GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(shirtColor);
                prints[i].SetActive(hasPrint);
                if (hasPrint) prints[i].GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(printColor.Value);
            }
        }
    }
}
