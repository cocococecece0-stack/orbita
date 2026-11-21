using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Создаёт сцену UI без единого ассета. Повторный вызов безопасен.</summary>
    public static class OrbitBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Boot()
        {
            DestroyNamed("OrbitRoot");
            DestroyNamed("OrbitCanvas");
            DestroyNamed("OrbitCamera");
            DestroyNamed("OrbitEventSystem");

            EventSystem oldSystem = Object.FindObjectOfType<EventSystem>();
            if (oldSystem != null)
            {
                oldSystem.gameObject.SetActive(false);
                Object.Destroy(oldSystem.gameObject);
            }

            GameObject cameraGo = new GameObject("OrbitCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiFactory.Ink;
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.depth = -100f;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            GameObject canvasGo = new GameObject("OrbitCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 1f;
            UiBox.Full.Apply(canvasGo.GetComponent<RectTransform>());

            GameObject systemGo = new GameObject("OrbitEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            systemGo.SetActive(false);
            systemGo.SetActive(true);

            GameObject root = new GameObject("OrbitRoot");
            OrbitApp app = root.AddComponent<OrbitApp>();
            app.Configure(canvasGo.transform, camera);
        }

        private static void DestroyNamed(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || all[i].name != name || !all[i].scene.IsValid()) continue;
                    go = all[i];
                    break;
                }
            }
            if (go == null) return;
            go.SetActive(false);
            Object.Destroy(go);
        }
    }
}
