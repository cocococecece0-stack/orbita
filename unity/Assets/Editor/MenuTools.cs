using System.IO;
using UnityEditor;
using UnityEngine;

namespace Orbit.EditorBuild
{
    /// <summary>Convenience menu items for local development.</summary>
    public static class MenuTools
    {
        [MenuItem("Orbit/Scenes/Regenerate Main Scene")]
        public static void RegenerateMainScene()
        {
            string path = BuildScript.EnsureMainScene();
            Debug.Log("Regenerated " + path);
        }

        [MenuItem("Orbit/Build/Build Android APK")]
        public static void BuildAndroid()
        {
            BuildScript.BuildAndroid();
        }

        [MenuItem("Orbit/Build/Build iOS Xcode Project")]
        public static void BuildIOS()
        {
            BuildScript.BuildIOS();
        }

        [MenuItem("Orbit/Build/Open Build Folder")]
        public static void OpenBuildFolder()
        {
            string buildRoot = Path.Combine(Path.GetDirectoryName(Application.dataPath), "build");
            Directory.CreateDirectory(buildRoot);
            EditorUtility.RevealInFinder(buildRoot);
        }
    }
}
