using System;
using System.Collections;
using UnityEngine;

namespace PrintshopTycoon
{
    /// <summary>Starts a fresh game on a throwaway save, for screenshot tours and video recordings.</summary>
    public static class DemoSetup
    {
        public static IEnumerator StartFreshGame(string saveFileName, Action<GameManager> started)
        {
            SaveManager.FileNameOverride = saveFileName;
            var old = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            foreach (var name in new[] { "Workshop", "Dynamic" })
            {
                var go = GameObject.Find(name);
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            yield return null;
            new SaveManager().Delete();
            started(new GameObject("PrintshopTycoon").AddComponent<GameManager>());
        }
    }
}
