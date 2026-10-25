using System.IO;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrintshopTycoon
{
    /// <summary>
    /// Guarantees a GameManager exists, so the game runs even from an empty scene.
    /// The editor setup also places one in Scenes/Main.unity.
    /// </summary>
    static class Bootstrap
    {
        static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Ensure();
            if (subscribed) return;
            subscribed = true;
            SceneManager.sceneLoaded += (scene, mode) => Ensure();
        }

        static void Ensure()
        {
            if (Object.FindFirstObjectByType<GameManager>() == null)
                new GameObject("PrintshopTycoon").AddComponent<GameManager>();
        }
    }
}
