using System.IO;
using PrintshopTycoon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrintshopTycoon
{
    /// <summary>JSON save file in Application.persistentDataPath, via the crash-safe core SaveStore.</summary>
    public class SaveManager
    {
        public const string FileName = "printshop_tycoon_save.json";

        /// <summary>Tests point this at a throwaway file so they never touch the player's save.</summary>
        public static string FileNameOverride;

        readonly SaveStore store;

        public SaveManager()
        {
            store = new SaveStore(Path.Combine(Application.persistentDataPath, FileNameOverride ?? FileName),
                data => JsonUtility.ToJson(data, true),
                json => JsonUtility.FromJson<SaveData>(json));
        }

        public string LastError => store.LastError;

        public SaveData Load()
        {
            var data = store.Load();
            if (data == null && store.LastError != null)
                Debug.LogWarning("[PrintshopTycoon] No usable save, starting a new game: " + store.LastError);
            return data;
        }

        public bool Save(SaveData data) => store.Save(data);
        public void Delete() => store.Delete();
    }
}
