using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Cheap, dependency-free juice: coin bursts, steam, sparkles and confetti from a small
    /// object pool (no per-effect allocations, no physics), plus the bouncing "go here" marker.
    /// Built from primitives so it renders with the same URP material as everything else.
    /// </summary>
    public class FxManager : MonoBehaviour
    {
        enum Kind { Coin, Puff, Confetti }

        readonly Dictionary<Kind, Stack<Transform>> pool = new Dictionary<Kind, Stack<Transform>>();
        Transform marker;
        Transform poolRoot;

        static readonly Color[] ConfettiColors =
        {
            MaterialLibrary.Hex("#E3191B"), MaterialLibrary.Hex("#FFC93C"), MaterialLibrary.Hex("#3D7DD8"),
            MaterialLibrary.Hex("#3DBE6B"), Color.white, MaterialLibrary.Hex("#8E6CD8"),
        };

        public void Init()
        {
            poolRoot = new GameObject("FxPool").transform;
            poolRoot.SetParent(transform, false);
            marker = new GameObject("NextActionMarker").transform;
            marker.SetParent(transform, false);
            var diamond = Shapes.Box(marker, "Diamond", Vector3.zero, Vector3.one * 0.38f, Palette.Marker);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Shapes.Sphere(marker, "Dot", new Vector3(0f, -0.45f, 0f), 0.16f, Palette.Marker);
            marker.gameObject.SetActive(false);
        }

        /// <summary>Hovers the marker over the next station (null hides it).</summary>
        public void SetMarker(Vector3? worldTop)
        {
            if (marker == null) return;
            marker.gameObject.SetActive(worldTop.HasValue);
            if (!worldTop.HasValue) return;
            float bob = Mathf.Sin(Time.time * 4f) * 0.15f;
            marker.position = worldTop.Value + Vector3.up * (0.9f + bob);
            marker.rotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
        }

        // ------------------------------------------------------------------ Effects

        public void CoinBurst(Vector3 position, int coins = 10)
        {
            for (int i = 0; i < coins; i++)
            {
                var c = Get(Kind.Coin, Palette.UiCoin);
                c.position = position;
                c.rotation = Quaternion.Euler(90f, 0f, 0f);
                c.localScale = new Vector3(0.22f, 0.025f, 0.22f);
                Vector3 v = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(3.5f, 5.5f), Random.Range(-1.6f, 1.6f));
                StartCoroutine(Fly(Kind.Coin, c, v, 0.9f, 12f, true, 0.6f));
            }
        }

        public void Steam(Vector3 position, int puffs = 7)
        {
            for (int i = 0; i < puffs; i++)
            {
                var s = Get(Kind.Puff, new Color(0.95f, 0.97f, 1f));
                s.position = position + Random.insideUnitSphere * 0.3f;
                s.localScale = Vector3.one * Random.Range(0.18f, 0.32f);
                StartCoroutine(Fly(Kind.Puff, s, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2f), Random.Range(-0.3f, 0.3f)), 1.1f, 0f, false, 1f));
            }
        }

        /// <summary>A few small coloured sparks - "the machine just did something".</summary>
        public void Sparkle(Vector3 position, Color color, int count = 6)
        {
            for (int i = 0; i < count; i++)
            {
                var s = Get(Kind.Puff, color);
                s.position = position;
                s.localScale = Vector3.one * 0.1f;
                var v = Random.onUnitSphere * 1.6f; v.y = Mathf.Abs(v.y) + 1f;
                StartCoroutine(Fly(Kind.Puff, s, v, 0.5f, 4f, true, 0.4f));
            }
        }

        public void Confetti(Vector3 position, int count = 40)
        {
            for (int i = 0; i < count; i++)
            {
                var c = Get(Kind.Confetti, ConfettiColors[i % ConfettiColors.Length]);
                c.position = position + Random.insideUnitSphere * 0.4f;
                c.rotation = Random.rotation;
                c.localScale = new Vector3(0.12f, 0.02f, 0.08f);
                var v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(4f, 7f), Random.Range(-2.5f, 2.5f));
                StartCoroutine(Fly(Kind.Confetti, c, v, 1.8f, 6f, true, 1f));
            }
        }

        // ------------------------------------------------------------------ Pool

        Transform Get(Kind kind, Color color)
        {
            if (!pool.TryGetValue(kind, out var stack)) pool[kind] = stack = new Stack<Transform>();
            Transform t = stack.Count > 0 ? stack.Pop() : Create(kind);
            t.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(color);
            t.gameObject.SetActive(true);
            return t;
        }

        Transform Create(Kind kind)
        {
            var type = kind == Kind.Coin ? PrimitiveType.Cylinder : (kind == Kind.Confetti ? PrimitiveType.Cube : PrimitiveType.Sphere);
            var go = Shapes.Prim(type, poolRoot, kind.ToString(), Vector3.zero, Vector3.one, Color.white);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void Release(Kind kind, Transform t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            pool[kind].Push(t);
        }

        IEnumerator Fly(Kind kind, Transform t, Vector3 velocity, float life, float gravity, bool shrink, float spin)
        {
            Vector3 startScale = t.localScale;
            for (float a = 0f; a < life && t != null; a += Time.deltaTime)
            {
                velocity += Vector3.down * gravity * Time.deltaTime;
                if (kind == Kind.Confetti) velocity *= 1f - 1.2f * Time.deltaTime; // flutter
                t.position += velocity * Time.deltaTime;
                t.Rotate(180f * spin * Time.deltaTime, 360f * spin * Time.deltaTime, 0f, Space.World);
                float k = 1f - a / life;
                t.localScale = startScale * (shrink ? Mathf.Min(1f, k * 3f) : 0.6f + (1f - k));
                yield return null;
            }
            Release(kind, t);
        }
    }
}
