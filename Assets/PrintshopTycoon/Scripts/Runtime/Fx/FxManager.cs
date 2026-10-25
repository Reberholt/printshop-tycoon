using System.Collections;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>
    /// Cheap, dependency-free juice: coin bursts, steam puffs and a bouncing "go here" marker.
    /// Built from primitives so it renders with the same URP material as everything else.
    /// </summary>
    public class FxManager : MonoBehaviour
    {
        Transform marker;

        public void Init()
        {
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

        public void CoinBurst(Vector3 position, int coins = 10)
        {
            for (int i = 0; i < coins; i++)
            {
                var c = Shapes.Cylinder(transform, "Coin", position, 0.22f, 0.05f, Palette.UiCoin);
                c.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Vector3 v = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(3.5f, 5.5f), Random.Range(-1.6f, 1.6f));
                StartCoroutine(Fly(c.transform, v, 0.9f, true));
            }
        }

        public void Steam(Vector3 position)
        {
            for (int i = 0; i < 7; i++)
            {
                var s = Shapes.Sphere(transform, "Steam", position + Random.insideUnitSphere * 0.3f, Random.Range(0.18f, 0.32f), new Color(0.95f, 0.97f, 1f));
                StartCoroutine(Fly(s.transform, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2f), Random.Range(-0.3f, 0.3f)), 1.1f, false));
            }
        }

        IEnumerator Fly(Transform t, Vector3 velocity, float life, bool gravity)
        {
            Vector3 startScale = t.localScale;
            for (float a = 0f; a < life && t != null; a += Time.deltaTime)
            {
                if (gravity) velocity += Vector3.down * 12f * Time.deltaTime;
                t.position += velocity * Time.deltaTime;
                t.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);
                float k = 1f - a / life;
                t.localScale = startScale * (gravity ? Mathf.Min(1f, k * 3f) : 0.6f + (1f - k));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
