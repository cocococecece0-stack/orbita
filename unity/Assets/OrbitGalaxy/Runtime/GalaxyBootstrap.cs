using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Точка входа Galaxy-слоя. Старый OrbitBootstrap не трогаем: он создаёт свои объекты в любой сборке,
    /// поэтому в Galaxy-сборке ждём один кадр, удаляем их и только потом поднимаем своё ядро.
    /// </summary>
    public static class GalaxyBootstrap
    {
        private const string GalaxyMarker = "GALAXY";
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string product = Application.productName;
            if (string.IsNullOrEmpty(product) || product.IndexOf(GalaxyMarker, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            if (_started) return;
            _started = true;

            if (UnityEngine.Object.FindObjectOfType<GalaxyApp>() != null) return;
            if (GameObject.Find("GalaxyRoot") != null) return;

            GameObject host = new GameObject("GalaxyBoot");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<GalaxyBootRunner>();
        }

        internal static void DestroyLegacyObjects()
        {
            string[] names = { "OrbitRoot", "OrbitCanvas", "OrbitCamera", "OrbitEventSystem" };
            for (int i = 0; i < names.Length; i++) DestroyNamed(names[i]);

            EventSystem[] systems = UnityEngine.Object.FindObjectsOfType<EventSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(systems[i].gameObject);
            }
        }

        private static void DestroyNamed(string name)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                found.SetActive(false);
                UnityEngine.Object.Destroy(found);
            }

            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate == null || candidate.name != name || !candidate.scene.IsValid()) continue;
                candidate.SetActive(false);
                UnityEngine.Object.Destroy(candidate);
            }
        }

        internal static void CreateGalaxyScene()
        {
            GameObject root = new GameObject("GalaxyRoot");
            UnityEngine.Object.DontDestroyOnLoad(root);

            GameObject cameraGo = new GameObject("GalaxyCamera");
            cameraGo.transform.SetParent(root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 340f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = TextureFactory.Ink;
            camera.cullingMask = -1;
            camera.depth = -50f;
            cameraGo.tag = "MainCamera";
            if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                cameraGo.AddComponent<AudioListener>();

            GameObject eventGo = new GameObject("GalaxyEventSystem");
            eventGo.transform.SetParent(root.transform, false);
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<StandaloneInputModule>();

            root.AddComponent<GalaxyApp>();
        }
    }

    /// <summary>Хост для однокадровой задержки: Unity запускает IEnumerator Start как корутину.</summary>
    public sealed class GalaxyBootRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            GalaxyBootstrap.DestroyLegacyObjects();
            if (UnityEngine.Object.FindObjectOfType<GalaxyApp>() == null)
                GalaxyBootstrap.CreateGalaxyScene();
            Destroy(gameObject);
        }
    }
}
