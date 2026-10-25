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
