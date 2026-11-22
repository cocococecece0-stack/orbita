using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Orbit.Galaxy.Editor
{
    /// <summary>
    /// Headless build entry points for ORBITA GALAXY. Kept separate from the frozen legacy build script.
    /// The scene, URP 2D assets and player settings are regenerated from code so CI builds are reproducible.
    /// </summary>
    public static class GalaxyBuildScript
    {
        public const string ArenaScenePath = "Assets/OrbitGalaxy/Scenes/Arena.unity";
        public const string UrpAssetPath = "Assets/OrbitGalaxy/Settings/GalaxyURP.asset";
        public const string RendererAssetPath = "Assets/OrbitGalaxy/Settings/GalaxyRenderer2D.asset";

        private const string ProductName = "ORBITA GALAXY";
        private const string CompanyName = "Orbit";
        private const string BundleVersion = "4.1.0";
        private const int AndroidVersionCode = 4100;
        private const string ApplicationIdentifier = "com.orbit.galaxy";
        private const string IosMinimumVersion = "13.0";

        [MenuItem("ORBITA GALAXY/Activate Galaxy profile")]
        public static void ActivateGalaxy()
        {
            EnsureArenaScene();
            ConfigurePlayerSettings();
            ConfigureUrp2D();
            AssetDatabase.SaveAssets();
            Debug.Log("ORBITA GALAXY profile activated: " + ArenaScenePath);
        }

        [MenuItem("ORBITA GALAXY/Restore legacy Orbita profile")]
        public static void RestoreLegacy()
        {
            PlayerSettings.productName = "Orbita";
            PlayerSettings.bundleVersion = "4.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.example.orbit.unity");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.example.orbit.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("Legacy Orbita profile restored. Galaxy runtime stays dormant unless product name contains GALAXY.");
        }

        [MenuItem("ORBITA GALAXY/Build/Android")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, Path.Combine(ProjectRoot(), "build", "GalaxyAndroid", "OrbitaGalaxy-Android.apk"));
        }

        [MenuItem("ORBITA GALAXY/Build/iOS Xcode project")]
        public static void BuildIOS()
        {
            // Для iOS Unity ожидает папку назначения: внутри появится Unity-iPhone.xcodeproj.
            Build(BuildTarget.iOS, Path.Combine(ProjectRoot(), "build", "GalaxyiOS"));
        }

        /// <summary>Creates the dedicated scene and makes it the only enabled build scene.</summary>
        public static string EnsureArenaScene()
        {
            EnsureFolder("Assets/OrbitGalaxy/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, ArenaScenePath))
                throw new InvalidOperationException("Could not save Galaxy arena scene to " + ArenaScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ArenaScenePath, true) };
            return ArenaScenePath;
        }

        private static void Build(BuildTarget target, string locationPathName)
        {
            string scenePath = EnsureArenaScene();
            ConfigurePlayerSettings();
            ConfigureUrp2D();
            AssetDatabase.SaveAssets();

            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
                    throw new InvalidOperationException("Failed to switch active build target to " + target);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(locationPathName));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = locationPathName,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report == null || report.summary.result != BuildResult.Succeeded)
            {
                string result = report == null ? "no build report" : report.summary.result.ToString();
                throw new InvalidOperationException("ORBITA GALAXY build failed for " + target + ": " + result);
            }

            Debug.Log("ORBITA GALAXY " + target + " build written to " + locationPathName);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = BundleVersion;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);
            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
            PlayerSettings.iOS.buildNumber = AndroidVersionCode.ToString();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard_2_0);
            PlayerSettings.runInBackground = false;
            SetAndroidTargetSettings();
            SetIosMinimumVersion();
        }

        /// <summary>
        /// ARM64 and API 23 are required for the published APK. Reflection keeps this editor
        /// script independent of the Android editor assembly reference while still failing the
        /// build loudly if the expected PlayerSettings properties are unavailable.
        /// </summary>
        private static void SetAndroidTargetSettings()
        {
            try
            {
                PropertyInfo architecture = typeof(PlayerSettings.Android).GetProperty(
                    "targetArchitectures", BindingFlags.Public | BindingFlags.Static);
                if (architecture == null || !architecture.CanWrite)
                    throw new InvalidOperationException("PlayerSettings.Android.targetArchitectures is unavailable.");
                architecture.SetValue(null, Enum.Parse(architecture.PropertyType, "ARM64"), null);

                PropertyInfo minimumSdk = typeof(PlayerSettings.Android).GetProperty(
                    "minSdkVersion", BindingFlags.Public | BindingFlags.Static);
                if (minimumSdk == null || !minimumSdk.CanWrite)
                    throw new InvalidOperationException("PlayerSettings.Android.minSdkVersion is unavailable.");
                minimumSdk.SetValue(null, Enum.Parse(minimumSdk.PropertyType, "AndroidApiLevel23"), null);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not apply required Android target settings: " + exception.Message);
                throw;
            }
        }

        private static void SetIosMinimumVersion()
        {
            try
            {
                PropertyInfo minimum = typeof(PlayerSettings.iOS).GetProperty(
                    "targetOSVersionString", BindingFlags.Public | BindingFlags.Static);
                if (minimum == null || !minimum.CanWrite)
                    throw new InvalidOperationException("PlayerSettings.iOS.targetOSVersionString is unavailable.");
                minimum.SetValue(null, IosMinimumVersion, null);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not apply required iOS minimum version: " + exception.Message);
                throw;
            }
        }

        /// <summary>
        /// Creates a 2D URP asset through reflection so the editor script compiles even if the URP package
        /// version changes its strongly typed API. Falls back to the built-in renderer with a warning.
        /// </summary>
        private static void ConfigureUrp2D()
        {
            EnsureFolder("Assets/OrbitGalaxy/Settings");

            Type assetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            Type rendererType = Type.GetType("UnityEngine.Rendering.Universal.Renderer2DData, Unity.RenderPipelines.Universal.Runtime");
            if (assetType == null || rendererType == null)
            {
                Debug.LogWarning("URP 2D types were not found. Check that com.unity.render-pipelines.universal is installed.");
                return;
            }

            try
            {
                ScriptableObject rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererAssetPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    rendererData.name = "GalaxyRenderer2D";
                    AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
                }

                ScriptableObject pipeline = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpAssetPath);
                if (pipeline == null)
                {
                    pipeline = ScriptableObject.CreateInstance(assetType);
                    pipeline.name = "GalaxyURP";
                    AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
                }

                var serialized = new SerializedObject(pipeline);
                SerializedProperty renderers = serialized.FindProperty("m_RendererDataList");
                if (renderers == null)
                    throw new InvalidOperationException("URP asset has no m_RendererDataList property.");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;

                SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
                if (defaultIndex != null) defaultIndex.intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GraphicsSettings.defaultRenderPipeline = pipeline as RenderPipelineAsset;
                QualitySettings.renderPipeline = pipeline as RenderPipelineAsset;
                AssetDatabase.SaveAssets();
                Debug.Log("URP 2D configured: " + UrpAssetPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not configure URP 2D automatically: " + exception.Message);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            string absolute = Path.Combine(ProjectRoot(), assetPath);
            Directory.CreateDirectory(absolute);
        }

        private static string ProjectRoot()
        {
            return Path.GetDirectoryName(Application.dataPath);
        }
    }
}
