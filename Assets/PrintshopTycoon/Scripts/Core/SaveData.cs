using System;
using System.Collections.Generic;
using System.IO;

namespace PrintshopTycoon.Core
{
    /// <summary>
    /// Flat, JSON-friendly snapshot of the game. Public fields only, so Unity's JsonUtility
    /// can serialize it. Bump CurrentVersion and add a migration step when the format changes.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public long savedAtUtcTicks;

        public int coins;
        public int totalXp;
        public int completedOrders;
        public List<string> claimedRewards = new List<string>();

        public int orderSequence;
        public int orderStep;
        public int pressQuality;
        public int missCount;
        public string pressStationId;

        public int blankStock;

        public int nextMachineSequence;
        public List<MachineInstance> machines = new List<MachineInstance>();

        public int nextWorkerSequence;
        public List<WorkerSave> workers = new List<WorkerSave>();

        public int autoJobsQueued;
        public int autoJobSequence;

        public int tutorialStep;
        public bool soundOn = true;
        public string language = "da";
    }

    [Serializable]
    public class WorkerSave
    {
        public string id;
        public string assignedMachineId;
        public bool hadJobInProgress;
        public int blanksHeld;
    }

    /// <summary>
    /// Crash-safe file storage: write to a temp file, keep the previous save as .bak,
    /// and fall back to the backup if the main file is missing or corrupt.
    /// </summary>
    public class SaveStore
    {
        readonly string path;
        readonly Func<SaveData, string> serialize;
        readonly Func<string, SaveData> deserialize;

        public string LastError { get; private set; }

        public SaveStore(string path, Func<SaveData, string> serialize, Func<string, SaveData> deserialize)
        {
            this.path = path;
            this.serialize = serialize;
            this.deserialize = deserialize;
        }

        public string MainPath => path;
        public string BackupPath => path + ".bak";
        string TempPath => path + ".tmp";

        public bool Save(SaveData data)
        {
            try
            {
                data.version = SaveData.CurrentVersion;
                data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
                string json = serialize(data);
                if (string.IsNullOrEmpty(json)) throw new InvalidDataException("Serializer returned empty data.");

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(TempPath, json);
                if (File.Exists(path))
                {
                    File.Copy(path, BackupPath, true);
                    File.Delete(path);
                }
                File.Move(TempPath, path);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return false;
            }
        }

        /// <summary>Returns null when there is no usable save (new game).</summary>
        public SaveData Load()
        {
            var data = TryRead(path) ?? TryRead(BackupPath);
            return data;
        }

        public void Delete()
        {
            foreach (var p in new[] { path, BackupPath, TempPath })
            {
                try { if (File.Exists(p)) File.Delete(p); } catch (Exception e) { LastError = e.Message; }
            }
        }

        SaveData TryRead(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                string json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = deserialize(json);
                if (data == null) return null;
                if (data.version <= 0 || data.version > SaveData.CurrentVersion)
                {
                    LastError = "Unsupported save version " + data.version;
                    return null;
                }
                return data;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                return null;
            }
        }
    }
}
