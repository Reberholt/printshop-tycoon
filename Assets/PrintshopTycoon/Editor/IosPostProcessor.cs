#if UNITY_IOS
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace PrintshopTycoon.EditorTools
{
    /// <summary>
    /// Runs after every iOS build and makes the generated Xcode project TestFlight-ready:
    /// portrait only, export-compliance answered (no custom encryption), automatic signing
    /// with the team id from the PT_APPLE_TEAM_ID environment variable, no bitcode.
    /// </summary>
    public static class IosPostProcessor
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            PatchInfoPlist(Path.Combine(path, "Info.plist"));
            PatchXcodeProject(path);
            Debug.Log("[PrintshopTycoon] iOS post-processing done: " + path);
        }

        static void PatchInfoPlist(string plistPath)
        {
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetString("CFBundleDisplayName", "Printshop Tycoon");
            // App Store Connect asks about encryption on every upload; the game uses none beyond HTTPS.
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            var orientations = root.CreateArray("UISupportedInterfaceOrientations");
            orientations.AddString("UIInterfaceOrientationPortrait");
            // Unity's default template may add this legacy key; it is harmless but not needed.
            root.values.Remove("UIApplicationExitsOnSuspend");
            plist.WriteToFile(plistPath);
        }

        static void PatchXcodeProject(string buildPath)
        {
            string projPath = PBXProject.GetPBXProjectPath(buildPath);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            string mainTarget = proj.GetUnityMainTargetGuid();
            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();

            foreach (var guid in new[] { mainTarget, frameworkTarget })
            {
                proj.SetBuildProperty(guid, "ENABLE_BITCODE", "NO");
                proj.SetBuildProperty(guid, "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
            }
            string team = Environment.GetEnvironmentVariable("PT_APPLE_TEAM_ID");
            if (!string.IsNullOrEmpty(team))
            {
                proj.SetTeamId(mainTarget, team);
                proj.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Automatic");
            }
            proj.WriteToFile(projPath);
        }
    }
}
#endif
