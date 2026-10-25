using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PrintshopTycoon.EditorTools
{
    /// <summary>
    /// One-click project configuration: portrait mobile player settings, a mobile URP asset,
    /// the shared base material, the balance asset and the Main scene. Runs automatically the
    /// first time the project is opened, and can be re-run from the menu at any time.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Root = "Assets/PrintshopTycoon";
        const string ResourcesDir = Root + "/Resources/PrintshopTycoon";
        const string SettingsDir = Root + "/Settings";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string MaterialPath = ResourcesDir + "/PT_Base.mat";
        const string BalancePath = ResourcesDir + "/GameBalance.asset";
        const string UrpPath = SettingsDir + "/PT_URP_Mobile.asset";
        const string UrpRendererPath = SettingsDir + "/PT_URP_Mobile_Renderer.asset";

        public const string BundleId = "dk.reberholt.printshoptycoon";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath) || !File.Exists(MaterialPath) || !File.Exists(UrpPath))
                {
                    Debug.Log("[PrintshopTycoon] First open detected - running project setup.");
                    Run();
                }
            };
        }

        [MenuItem("Printshop Tycoon/Setup Project", priority = 0)]
        public static void Run()
        {
            Directory.CreateDirectory(ResourcesDir);
            Directory.CreateDirectory(SettingsDir);
            Directory.CreateDirectory(Root + "/Scenes");

            ConfigurePlayer();
            var urp = ConfigureUrp();
            CreateBaseMaterial();
            CreateBalanceAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[PrintshopTycoon] Project setup complete" + (urp != null ? " (URP mobile asset assigned)." : " (URP asset could NOT be created - check that the URP package is installed)."));
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Reberholt";
            PlayerSettings.productName = "Printshop Tycoon";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

            // Portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Mobile rendering defaults.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, true);

            // Android
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // iOS
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

            // Enable both input backends: game code uses the EventSystem and supports either.
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2)
                {
                    prop.intValue = 2;
                    so.ApplyModifiedProperties();
                    Debug.Log("[PrintshopTycoon] Active Input Handling set to 'Both'. Restart the editor if prompted.");
                }
            }
        }

        static UniversalRenderPipelineAsset ConfigureUrp()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpPath);
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, UrpPath);
            }
            if (urp == null) return null;

            // Lean mobile settings: one shadow cascade, no HDR, light MSAA.
            urp.supportsHDR = false;
            urp.msaaSampleCount = 2;
            urp.renderScale = 1f;
            urp.shadowDistance = 30f;
            urp.shadowCascadeCount = 1;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            return urp;
        }

        static void CreateBaseMaterial()
        {
            if (File.Exists(MaterialPath)) return;
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = "PT_Base", enableInstancing = true };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        static void CreateBalanceAsset()
        {
            if (File.Exists(BalancePath)) return;
            var asset = ScriptableObject.CreateInstance<GameBalanceConfig>();
            AssetDatabase.CreateAsset(asset, BalancePath);
        }

        static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("PrintshopTycoon").AddComponent<GameManager>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (!Application.isBatchMode && SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
