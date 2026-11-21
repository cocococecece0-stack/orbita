using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orbit.EditorBuild
{
    /// <summary>
    /// Headless build entry points for game-ci/unity-builder and the Orbit editor menu.
    /// The scene and player settings are always regenerated from code so CI builds are reproducible.
    /// </summary>
    public static class BuildScript
    {
        public const string MainScenePath = "Assets/Orbit/Scenes/Main.unity";

        private const string ProductName = "Orbita";
        private const string CompanyName = "Orbit";
        private const string BundleVersion = "4.0.0";
        private const int AndroidVersionCode = 8;
        private const string ApplicationIdentifier = "com.example.orbit.unity";

        /// <summary>Entry point invoked by the Android CI job (and Orbit &gt; Build menu).</summary>
        public static void BuildAndroid()
        {
            string apkPath = Path.Combine(ProjectRoot(), "build", "Android", "Orbit.apk");
            Build(BuildTarget.Android, apkPath);
        }

        /// <summary>Entry point invoked by the iOS CI job (and Orbit &gt; Build menu).</summary>
        public static void BuildIOS()
        {
            string xcodePath = Path.Combine(ProjectRoot(), "build", "iOS", "Orbit.xcodeproj");
            Build(BuildTarget.iOS, xcodePath);
        }

        /// <summary>
        /// Creates a fresh empty scene, saves it as Main.unity, and makes it the only enabled build scene.
        /// Presentation code under Orbit.Unity is expected to populate this scene at runtime/editor time.
        /// </summary>
        public static string EnsureMainScene()
        {
            string absolutePath = Path.Combine(ProjectRoot(), "Assets", "Orbit", "Scenes", "Main.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, MainScenePath))
                throw new InvalidOperationException("Could not save scene to " + MainScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            return MainScenePath;
        }

        private static void Build(BuildTarget target, string locationPathName)
        {
            string scenePath = EnsureMainScene();
            ConfigurePlayerSettings();

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
                throw new InvalidOperationException("Unity build failed for " + target + ": " + result);
            }

            Debug.Log("Orbita " + target + " build written to " + locationPathName);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.bundleVersion = BundleVersion;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);

            PlayerSettings.Android.bundleVersionCode = AndroidVersionCode;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.Mono2x);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        }

        private static string ProjectRoot()
        {
            // Application.dataPath is <project>/Assets regardless of the runner's working directory.
            return Path.GetDirectoryName(Application.dataPath);
        }
    }
}
