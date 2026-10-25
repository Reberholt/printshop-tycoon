using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
#if PT_RECORDER
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
#endif

namespace PrintshopTycoon.EditorTools
{
    /// <summary>
    /// One click: enters Play Mode, plays the scripted GameplayDemo (~40 s) and records it with
    /// Unity Recorder to "gameplay-preview.mp4" in the project folder (portrait 1080x1920, 30 fps).
    /// If Unity Recorder is not installed yet, the first click installs it.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayRecorder
    {
        const string PendingKey = "PT_RecordGameplayPending";
        public const string FileBaseName = "gameplay-preview";

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string VideoPath => Path.Combine(ProjectRoot, FileBaseName + ".mp4");

        static GameplayRecorder()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Printshop Tycoon/Record Gameplay Preview (gameplay-preview.mp4)", priority = 41)]
        public static void Record()
        {
#if PT_RECORDER
            if (EditorApplication.isPlaying) { StartRecording(); return; }
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
#else
            if (EditorUtility.DisplayDialog("Printshop Tycoon",
                    "Unity Recorder er ikke installeret endnu.\n\nInstallér den nu? Når Unity er færdig, så klik på menupunktet igen.",
                    "Installér", "Annullér"))
            {
                Client.Add("com.unity.recorder");
                Debug.Log("[PrintshopTycoon] Installing Unity Recorder - click 'Record Gameplay Preview' again when it is done.");
            }
#endif
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            SessionState.SetBool(PendingKey, false);
#if PT_RECORDER
            StartRecording();
#endif
        }

#if PT_RECORDER
        static RecorderController controller;

        static void StartRecording()
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "Gameplay Preview";
            movie.Enabled = true;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1080, OutputHeight = 1920 };
            movie.AudioInputSettings.PreserveAudio = true;
            movie.OutputFile = Path.Combine(ProjectRoot, FileBaseName); // Recorder adds ".mp4" (default format)
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate = 30f;

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            controller.StartRecording();
            Debug.Log("[PrintshopTycoon] Recording gameplay preview ...");

            GameplayDemo.Finished -= OnDemoFinished;
            GameplayDemo.Finished += OnDemoFinished;
            GameplayDemo.Begin();
        }

        static void OnDemoFinished()
        {
            GameplayDemo.Finished -= OnDemoFinished;
            if (controller != null && controller.IsRecording()) controller.StopRecording();
            controller = null;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExitPlaymode();
                Debug.Log("[PrintshopTycoon] Gameplay preview saved: " + VideoPath);
                if (File.Exists(VideoPath)) EditorUtility.RevealInFinder(VideoPath);
            };
        }
#endif

        [MenuItem("Printshop Tycoon/Play Gameplay Demo (no recording)", priority = 42)]
        public static void PlayDemo()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Printshop Tycoon", "Start Play Mode first.", "OK");
                return;
            }
            GameplayDemo.Begin();
        }
    }
}
