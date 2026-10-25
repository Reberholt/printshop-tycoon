using System.Collections.Generic;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PrintshopTycoon
{
    /// <summary>Toasts, floating coins, Silke's speech bubble and world-space labels.</summary>
    public partial class UIManager
    {
        RectTransform toastRoot;
        RectTransform overlay;   // toasts + Silke, always drawn above modals
        RectTransform labelRoot;
        RectTransform silkeBubble;
        Text silkeText;
        Image silkePortrait;
        float silkeHideAt;

        // ------------------------------------------------------------------ Silke

        void BuildSilkeBubble(RectTransform parent)
        {
            var bubble = UIFactory.Panel(parent, "SilkeBubble", Color.white);
            silkeBubble = bubble.rectTransform;
            silkeBubble.TopStrip(Margin * 2 + 96f + 150f, 180f, Margin);
            bubble.gameObject.AddComponent<Button>().onClick.AddListener(() => silkeBubble.gameObject.SetActive(false));
            // Official Silke art; it pokes out above the bubble for a friendlier look.
            silkePortrait = UIFactory.Rect(bubble.transform, "Portrait").gameObject.AddComponent<Image>();
            silkePortrait.raycastTarget = false;
            silkePortrait.preserveAspect = true;
            silkePortrait.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(6f, 4f), new Vector2(180f, 236f));
            var name = UIFactory.Label(bubble.transform, "Name", Loc.T("silke.name"), 32, CharacterFactory.BrandRed, TextAnchor.UpperLeft);
            name.rectTransform.Stretch(200, 12, 24, 126);
            silkeText = UIFactory.Label(bubble.transform, "Text", "", 36, Palette.UiDark, TextAnchor.UpperLeft, FontStyle.Normal);
            silkeText.rectTransform.Stretch(200, 52, 24, 12);
            silkeBubble.gameObject.SetActive(false);
        }

        /// <summary>Silke speaks; pose is one of the CharacterArt Silke images.</summary>
        public void ShowSilke(string text, string pose = CharacterArt.SilkeWave, float seconds = 7f)
        {
            silkeText.text = text;
            var sprite = CharacterArt.Get(pose) ?? CharacterArt.Get(CharacterArt.SilkeNeutral);
            silkePortrait.sprite = sprite;
            silkePortrait.enabled = sprite != null;
            silkeBubble.gameObject.SetActive(true);
            silkeHideAt = Time.unscaledTime + seconds;
            orderDetails.gameObject.SetActive(false);
            StartCoroutine(PunchRoutine(silkeBubble));
        }

        // ------------------------------------------------------------------ Toasts / floating coins

        public void Toast(string message, bool error = false)
        {
            var bg = UIFactory.Panel(toastRoot, "Toast", error ? new Color(0.89f, 0.1f, 0.11f, 0.95f) : new Color(0.13f, 0.13f, 0.15f, 0.92f), true, false);
            bg.rectTransform.Place(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 104f));
            var t = UIFactory.Label(bg.transform, "Text", message, 40, Color.white);
            t.rectTransform.Stretch(20, 10, 20, 10);
            // Push older toasts up; keep at most three.
            for (int i = 0; i < toastRoot.childCount - 1; i++)
            {
                var older = (RectTransform)toastRoot.GetChild(i);
                older.anchoredPosition += new Vector2(0f, 116f);
                if (toastRoot.childCount - 1 - i > 2) older.gameObject.SetActive(false);
            }
            StartCoroutine(FadeAndDestroy(bg.gameObject, 2.2f));
        }

        public void FloatingCoins(Vector3 world, int amount)
        {
            var cam = game.Cameras.Camera;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
            var t = UIFactory.Label(canvasRect, "FloatingCoins", Loc.T("toast.paid", amount), 64, Palette.UiCoin);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(600f, 110f);
            t.rectTransform.anchoredPosition = local;
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.UiDark;
            outline.effectDistance = new Vector2(4f, -4f);
            StartCoroutine(FloatUp(t.rectTransform, 1.4f));
        }

        System.Collections.IEnumerator FadeAndDestroy(GameObject go, float life)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            for (float a = 0f; a < life && go != null; a += Time.unscaledDeltaTime)
            {
                group.alpha = a < life - 0.4f ? 1f : (life - a) / 0.4f;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        System.Collections.IEnumerator FloatUp(RectTransform rt, float life)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Vector2 start = rt.anchoredPosition;
            for (float a = 0f; a < life && rt != null; a += Time.unscaledDeltaTime)
            {
                float k = a / life;
                rt.anchoredPosition = start + new Vector2(0f, 240f * (1f - (1f - k) * (1f - k)));
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : 1f);
                group.alpha = k < 0.7f ? 1f : (1f - k) / 0.3f;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // ------------------------------------------------------------------ World labels

        class WorldTag
        {
            public RectTransform root;
            public Image bg;
            public Text text;
            public Image fill;
            public RectTransform fillTrack;
            public bool used;
        }

        readonly Dictionary<object, WorldTag> worldTags = new Dictionary<object, WorldTag>();
        readonly List<WorldTag> visibleTags = new List<WorldTag>();
        readonly List<object> tagRemoval = new List<object>();

        void LateUpdate()
        {
            if (game == null || game.Session == null) return;
            UpdateWorldLabels();
        }

        void UpdateWorldLabels()
        {
            var cam = game.Cameras.Camera;
            foreach (var t in worldTags.Values) t.used = false;
            visibleTags.Clear();
            bool factory = game.Cameras.Mode == CameraMode.Factory && !game.Cameras.IsTransitioning;

            // Workers: small pill with number + status (+ progress while pressing).
            foreach (var w in game.WorkerViews)
            {
                float progress = w.Machine.State == WorkerState.Working || w.Machine.State == WorkerState.Packing ? w.Machine.TaskProgress : -1f;
                PlaceTag(w, w.LabelPosition, Loc.T("staff.worker", GameManager.WorkerNumber(w.Machine.Id)) + " · " + w.StatusText, Palette.UiPurple, progress, cam, 340f);
            }

            // Machines: progress bar while working, "Klar!" when ready. Names only in factory mode.
            foreach (var station in game.Interaction.Stations)
            {
                if (station == null) continue;
                bool hasJob = station.TryGetProgress(out float p, out bool ready);
                if (hasJob)
                    PlaceTag(station, station.TopPosition + Vector3.up * 0.5f, ready ? Loc.T("info.ready") : station.DisplayName,
                        ready ? Palette.UiGreen : new Color(0.13f, 0.13f, 0.15f), ready ? -1f : p, cam, 260f, ready);
                else if (factory && station is HeatPressStation press)
                    PlaceTag(station, station.TopPosition + Vector3.up * 0.5f, press.DisplayName + " " + GameManager.PressNumber(press.MachineId),
                        new Color(0.13f, 0.13f, 0.15f), -1f, cam, 260f);
            }

            ResolveOverlaps();

            tagRemoval.Clear();
            foreach (var kv in worldTags)
                if (!kv.Value.used || (kv.Key is Object uo && uo == null)) tagRemoval.Add(kv.Key);
            foreach (var k in tagRemoval)
            {
                if (worldTags[k].root != null) Destroy(worldTags[k].root.gameObject);
                worldTags.Remove(k);
            }
        }

        void PlaceTag(object key, Vector3 world, string text, Color color, float progress, Camera cam, float width, bool pulse = false)
        {
            if (!worldTags.TryGetValue(key, out var tag))
            {
                tag = new WorldTag();
                tag.bg = UIFactory.Panel(labelRoot, "Tag", color, true, false);
                tag.root = tag.bg.rectTransform;
                tag.root.anchorMin = tag.root.anchorMax = new Vector2(0.5f, 0.5f);
                tag.text = UIFactory.Label(tag.root, "Text", "", 28, Color.white);
                tag.text.rectTransform.Stretch(12, 4, 12, 22);
                var track = UIFactory.Panel(tag.root, "Track", new Color(1f, 1f, 1f, 0.25f), true, false);
                tag.fillTrack = track.rectTransform;
                tag.fillTrack.anchorMin = new Vector2(0f, 0f); tag.fillTrack.anchorMax = new Vector2(1f, 0f);
                tag.fillTrack.offsetMin = new Vector2(12f, 8f); tag.fillTrack.offsetMax = new Vector2(-12f, 20f);
                tag.fill = UIFactory.Panel(track.transform, "Fill", Palette.UiCoin, true, false);
                tag.fill.rectTransform.anchorMin = Vector2.zero;
                tag.fill.rectTransform.offsetMin = tag.fill.rectTransform.offsetMax = Vector2.zero;
                worldTags[key] = tag;
            }
            tag.used = true;
            Vector3 sp = cam.WorldToScreenPoint(world);
            bool visible = sp.z > 0f && !ModalOpen;
            tag.root.gameObject.SetActive(visible);
            if (!visible) return;

            bool hasBar = progress >= 0f;
            tag.root.sizeDelta = new Vector2(width, hasBar ? 74f : 56f);
            tag.text.rectTransform.offsetMin = new Vector2(12f, hasBar ? 22f : 4f);
            tag.fillTrack.gameObject.SetActive(hasBar);
            if (hasBar) tag.fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            tag.bg.color = new Color(color.r, color.g, color.b, 0.9f);
            if (tag.text.text != text) tag.text.text = text;
            tag.root.localScale = pulse ? Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f) : Vector3.one;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local))
                tag.root.anchoredPosition = local;
            visibleTags.Add(tag);
        }

        /// <summary>Pushes labels upward until none overlap (few labels, so O(n²) is fine).</summary>
        void ResolveOverlaps()
        {
            visibleTags.Sort((a, b) => a.root.anchoredPosition.y.CompareTo(b.root.anchoredPosition.y));
            for (int i = 0; i < visibleTags.Count; i++)
            {
                var a = visibleTags[i].root;
                for (int j = 0; j < i; j++)
                {
                    var b = visibleTags[j].root;
                    Vector2 pa = a.anchoredPosition, pb = b.anchoredPosition;
                    float overlapX = (a.sizeDelta.x + b.sizeDelta.x) * 0.5f - Mathf.Abs(pa.x - pb.x);
                    float overlapY = (a.sizeDelta.y + b.sizeDelta.y) * 0.5f - Mathf.Abs(pa.y - pb.y);
                    if (overlapX > 0f && overlapY > 0f)
                        a.anchoredPosition = new Vector2(pa.x, pb.y + (a.sizeDelta.y + b.sizeDelta.y) * 0.5f + 6f);
                }
            }
        }
    }
}
