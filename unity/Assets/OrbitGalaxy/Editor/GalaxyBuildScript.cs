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
            SetMobileUiSettings();
            SetManagedStripping();
            ConfigureBranding();
        }

        /// <summary>
        /// Full-screen landscape mobile UI: hide the iOS status bar/home indicator and require full screen
        /// so the Safe Area math in the runtime matches what the player sees. Android stays offline.
        /// </summary>
        private static void SetMobileUiSettings()
        {
            try
            {
                PlayerSettings.iOS.requiresFullScreen = true;
                PlayerSettings.iOS.statusBarHidden = true;
                PlayerSettings.iOS.hideHomeIndicator = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not apply iOS UI settings: " + exception.Message);
            }

            try { PlayerSettings.Android.forceInternetPermission = false; }
            catch (Exception exception) { Debug.LogWarning("Could not clear Android internet permission: " + exception.Message); }

            // Target a modern Android API while keeping minSdk 23. Reflection tolerates editor
            // builds whose enum does not yet name the newest level.
            try
            {
                PropertyInfo targetSdk = typeof(PlayerSettings.Android).GetProperty(
                    "targetSdkVersion", BindingFlags.Public | BindingFlags.Static);
                if (targetSdk != null && targetSdk.CanWrite)
                {
                    string[] candidates = { "AndroidApiLevel34", "AndroidApiLevel33", "AndroidApiLevelAuto" };
                    foreach (string name in candidates)
                    {
                        try
                        {
                            targetSdk.SetValue(null, Enum.Parse(targetSdk.PropertyType, name), null);
                            break;
                        }
                        catch (Exception) { /* try the next candidate */ }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not apply Android target SDK: " + exception.Message);
            }
        }

        /// <summary>
        /// Low stripping keeps the reflection-based SaveManager facade and core types alive under IL2CPP.
        /// Higher levels can remove members the save layer resolves by name at runtime.
        /// </summary>
        private static void SetManagedStripping()
        {
            try
            {
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not set managed stripping level: " + exception.Message);
            }
        }

        /// <summary>
        /// Own ORBITA GALAXY identity: a procedurally drawn app icon and a dark engine splash background.
        /// The Unity Personal splash/logo is left intact (license requirement); the branded boot screen
        /// is drawn by the runtime GalaxyUi.ShowSplash after the engine splash.
        /// </summary>
        private static void ConfigureBranding()
        {
            try
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.backgroundColor = new Color32(7, 10, 24, 255);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not set splash background: " + exception.Message);
            }

            try
            {
                Texture2D master = EnsureIconMaster();
                if (master != null)
                {
                    AssignIcons(NamedBuildTarget.Android, master);
                    AssignIcons(NamedBuildTarget.iOS, master);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not assign Galaxy app icon: " + exception.Message);
            }
        }

        private const string IconFolder = "Assets/OrbitGalaxy/Branding";
        private const string IconMasterPath = IconFolder + "/GalaxyIcon.png";

        /// <summary>
        /// Unity only accepts an icon list whose length matches GetIconSizes, so we export the master
        /// emblem at every required size and assign the whole set. Textures are persisted as assets so
        /// they survive into the player build.
        /// </summary>
        private static void AssignIcons(NamedBuildTarget target, Texture2D master)
        {
            int[] sizes = PlayerSettings.GetIconSizes(target);
            if (sizes == null || sizes.Length == 0) return;
            var icons = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = Mathf.Max(16, sizes[i]);
                string path = IconFolder + "/icon_" + target + "_" + size + ".png";
                Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon == null)
                {
                    icon = Resample(master, size);
                    System.IO.File.WriteAllBytes(path, icon.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(icon);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    ConfigureIconImporter(path);
                    icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                icons[i] = icon;
            }
            PlayerSettings.SetIcons(target, icons, IconKind.Application);
        }

        private static void ConfigureIconImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Texture2D Resample(Texture2D source, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = source.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>Draws and imports a 1024px orbital emblem once; reused on every build.</summary>
        private static Texture2D EnsureIconMaster()
        {
            EnsureFolder(IconFolder);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(IconMasterPath);
            if (existing != null) return existing;

            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            Color32 top = new Color32(20, 26, 62, 255);
            Color32 bottom = new Color32(8, 10, 26, 255);
            Color32 ring = new Color32(61, 230, 255, 255);
            Color32 core = new Color32(182, 255, 61, 255);
            for (int y = 0; y < n; y++)
            {
                float v = y / (float)(n - 1);
                Color32 bg = Color32.Lerp(bottom, top, v);
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f;
                    float dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = bg;
                    float ex = dx / 0.40f;
                    float ey = (dy + dx * 0.32f) / 0.16f;
                    float er = Mathf.Sqrt(ex * ex + ey * ey);
                    if (er > 0.86f && er < 1.06f) c = ring;
                    if (r < 0.13f) c = core;
                    else if (r < 0.17f) c = Color32.Lerp(core, bg, (r - 0.13f) / 0.04f);
                    if ((x * 73856093 ^ y * 19349663) % 997 == 0 && r > 0.2f) c = new Color32(220, 232, 255, 255);
                    px[y * n + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            System.IO.File.WriteAllBytes(IconMasterPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconMasterPath, ImportAssetOptions.ForceUpdate);
            ConfigureIconImporter(IconMasterPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconMasterPath);
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
