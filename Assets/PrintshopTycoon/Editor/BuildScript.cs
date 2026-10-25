using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PrintshopTycoon.EditorTools
{
    /// <summary>
    /// Menu + command-line builds, e.g.:
    /// Unity -batchmode -quit -projectPath . -executeMethod PrintshopTycoon.EditorTools.BuildScript.BuildAndroid
    /// </summary>
    public static class BuildScript
    {
        const string Scene = "Assets/PrintshopTycoon/Scenes/Main.unity";

        [MenuItem("Printshop Tycoon/Build Android APK", priority = 20)]
        public static void BuildAndroid()
        {
            ProjectSetup.Run();
            Build(BuildTarget.Android, "Builds/Android/PrintshopTycoon.apk");
        }

        [MenuItem("Printshop Tycoon/Build iOS Xcode Project", priority = 21)]
        public static void BuildIOS()
        {
            ProjectSetup.Run();
            Build(BuildTarget.iOS, "Builds/iOS");
        }

        [MenuItem("Printshop Tycoon/Build WebGL", priority = 22)]
        public static void BuildWebGL()
        {
            ProjectSetup.Run();
            // Portrait canvas in the browser (the default WebGL template is landscape).
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        [MenuItem("Printshop Tycoon/Capture Screenshot Tour (Play Mode)", priority = 40)]
        public static void CaptureScreenshots()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Printshop Tycoon", "Start Play Mode first, and set the Game view to 1080x1920.", "OK");
                return;
            }
            ScreenshotTour.Begin();
        }

        static void Build(BuildTarget target, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[PrintshopTycoon] " + target + " build " + report.summary.result + " -> " + path +
                      " (" + report.summary.totalSize / (1024 * 1024) + " MB, " + report.summary.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
