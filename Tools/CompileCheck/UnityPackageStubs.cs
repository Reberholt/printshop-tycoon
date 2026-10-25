// Compile-check only: public surface of uGUI / URP / Input System / Test Framework used by the game.
// Signatures mirror Unity's packages; nothing here ships in the game.
using System;
using UnityEngine.Events;
namespace UnityEngine.EventSystems {
  public abstract class UIBehaviour : MonoBehaviour { protected virtual void OnEnable(){} protected virtual void OnDisable(){} }
  public class EventSystem : UIBehaviour { public static EventSystem current { get; set; } public int pixelDragThreshold { get; set; } public bool sendNavigationEvents { get; set; } }
  public abstract class BaseInputModule : UIBehaviour {}
  public abstract class PointerInputModule : BaseInputModule {}
  public class StandaloneInputModule : PointerInputModule {}
  public abstract class BaseRaycaster : UIBehaviour {}
  public class AbstractEventData {}
  public class BaseEventData : AbstractEventData {}
  public class PointerEventData : BaseEventData { public int pointerId { get; set; } public Vector2 position { get; set; } public Vector2 delta { get; set; } public Vector2 scrollDelta { get; set; } public Camera pressEventCamera => null; }
  public interface IEventSystemHandler {}
  public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
  public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
  public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
  public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
}
namespace UnityEngine.UI {
  using UnityEngine.EventSystems;
  public class VertexHelper { public void Clear(){} }
  public abstract class Graphic : UIBehaviour { public virtual Color color { get; set; } public virtual bool raycastTarget { get; set; } public RectTransform rectTransform => null; protected virtual void OnPopulateMesh(VertexHelper vh){} }
  public abstract class MaskableGraphic : Graphic {}
  public class Image : MaskableGraphic { public enum Type { Simple, Sliced, Tiled, Filled } public Sprite sprite { get; set; } public Type type { get; set; } }
  public class Text : MaskableGraphic { public Font font { get; set; } public virtual string text { get; set; } public int fontSize { get; set; } public FontStyle fontStyle { get; set; } public TextAnchor alignment { get; set; } public HorizontalWrapMode horizontalOverflow { get; set; } public VerticalWrapMode verticalOverflow { get; set; } public bool resizeTextForBestFit { get; set; } public int resizeTextMinSize { get; set; } public int resizeTextMaxSize { get; set; } public bool supportRichText { get; set; } }
  public struct ColorBlock { public Color normalColor { get; set; } public Color highlightedColor { get; set; } public Color pressedColor { get; set; } public Color selectedColor { get; set; } public Color disabledColor { get; set; } public float colorMultiplier { get; set; } public float fadeDuration { get; set; } }
  public class Selectable : UIBehaviour { public bool interactable { get; set; } public ColorBlock colors { get; set; } }
  public class Button : Selectable { public class ButtonClickedEvent : UnityEvent {} public ButtonClickedEvent onClick { get; set; } }
  public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink } public ScaleMode uiScaleMode { get; set; } public Vector2 referenceResolution { get; set; } public ScreenMatchMode screenMatchMode { get; set; } public float matchWidthOrHeight { get; set; } }
  public class GraphicRaycaster : BaseRaycaster {}
  public abstract class LayoutGroup : UIBehaviour { public RectOffset padding { get; set; } public TextAnchor childAlignment { get; set; } }
  public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing { get; set; } public bool childForceExpandWidth { get; set; } public bool childForceExpandHeight { get; set; } public bool childControlWidth { get; set; } public bool childControlHeight { get; set; } }
  public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup {}
  public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup {}
  public class LayoutElement : UIBehaviour { public virtual float minHeight { get; set; } public virtual float preferredHeight { get; set; } }
  public abstract class BaseMeshEffect : UIBehaviour {}
  public class Shadow : BaseMeshEffect { public Color effectColor { get; set; } public Vector2 effectDistance { get; set; } }
  public class Outline : Shadow {}
}
namespace UnityEngine.Rendering.Universal {
  public abstract class ScriptableRendererData : ScriptableObject {}
  public class UniversalRendererData : ScriptableRendererData {}
  public class UniversalRenderPipelineAsset : UnityEngine.Rendering.RenderPipelineAsset {
    public override UnityEngine.Rendering.RenderPipeline CreatePipeline() => null;
    public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
    public bool supportsHDR { get; set; } public int msaaSampleCount { get; set; } public float renderScale { get; set; } public float shadowDistance { get; set; } public int shadowCascadeCount { get; set; } public bool supportsCameraDepthTexture { get; set; } public bool supportsCameraOpaqueTexture { get; set; }
  }
}
namespace UnityEngine.InputSystem {
  namespace Controls { public class ButtonControl { public bool isPressed => false; public bool wasPressedThisFrame => false; } }
  public class Keyboard { public static Keyboard current => null; public Controls.ButtonControl aKey, dKey, wKey, sKey, eKey, spaceKey, escapeKey, leftArrowKey, rightArrowKey, upArrowKey, downArrowKey; }
  namespace UI { public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule {} }
}
namespace UnityEngine.TestTools {
  public class UnityTestAttribute : System.Attribute {}
  public class UnitySetUpAttribute : System.Attribute {}
  public class UnityTearDownAttribute : System.Attribute {}
}
