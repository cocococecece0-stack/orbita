using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>Орфографическое следование за игроком с динамическим масштабом и удержанием внутри арены.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private const float MinZoom = 240f;
        private const float MaxZoom = 820f;
        private const float CameraDepth = -10f;

        private GalaxyApp _app;
        private Camera _camera;
        private Vector3 _position;
        private float _zoom = 340f;

        public Camera Camera { get { return _camera; } }
        public float Zoom { get { return _zoom; } }

        public void Initialize(GalaxyApp app)
        {
            _app = app;
            _camera = GetComponentInChildren<Camera>(true);
            if (_camera == null)
            {
                GameObject go = new GameObject("GalaxyCamera");
                go.transform.SetParent(transform, false);
                _camera = go.AddComponent<Camera>();
                if (FindObjectOfType<AudioListener>() == null) go.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 400f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = TextureFactory.Ink;
            _camera.cullingMask = -1;
            _camera.depth = -50f;
            _position = _camera.transform.localPosition;
            _position.z = CameraDepth;
            SnapTo(Vec2.Zero);
        }

        public void SnapTo(Vec2 position)
        {
            _position = new Vector3(position.X, position.Y, CameraDepth);
            if (_camera != null) _camera.transform.localPosition = _position;
        }

        public void UpdateCamera(MatchSimulation match, float dt)
        {
            if (_camera == null) return;
            if (dt < 0f) dt = 0f;

            Ship player = match == null ? null : match.Player;
            Vec2 focus = player == null ? Vec2.Zero : player.Position;

            float targetZoom = 320f;
            if (player != null)
            {
                targetZoom = 232f
                    + player.Radius * 2.6f
                    + Mathf.Sqrt(Mathf.Max(1f, player.Mass)) * 7.5f
                    + player.Speed * 0.12f
                    + Mathf.Clamp01(player.Velocity.Magnitude / 620f) * 42f;
                if (player.IsBoss) targetZoom += 120f;
            }
            targetZoom = Mathf.Clamp(targetZoom, MinZoom, MaxZoom);
            if (match != null && match.Arena != null)
                targetZoom = Mathf.Min(targetZoom, Mathf.Max(MinZoom, match.Arena.Radius * 0.44f));

            bool reduced = _app != null && _app.Profile != null && _app.Profile.Settings != null
                && _app.Profile.Settings.ReducedMotion;
            float followRate = reduced ? 16f : 5.6f;
            float zoomRate = reduced ? 12f : 2.8f;
            float follow = 1f - Mathf.Exp(-followRate * dt);
            float scale = 1f - Mathf.Exp(-zoomRate * dt);

            Vector3 desired = new Vector3(focus.X, focus.Y, CameraDepth);
            _position = Vector3.Lerp(_position, desired, follow);
            _zoom = Mathf.Lerp(_zoom, targetZoom, scale);

            float arenaRadius = match != null && match.Arena != null ? match.Arena.Radius : GameConfig.ArenaRadius;
            float halfHeight = _zoom;
            float halfWidth = _zoom * Mathf.Max(0.5f, _camera.aspect);
            float maxX = Mathf.Max(0f, arenaRadius - halfWidth * 1.04f);
            float maxY = Mathf.Max(0f, arenaRadius - halfHeight * 1.04f);
            _position.x = Mathf.Clamp(_position.x, -maxX, maxX);
            _position.y = Mathf.Clamp(_position.y, -maxY, maxY);
            _position.z = CameraDepth;

            _camera.transform.localPosition = _position;
            _camera.orthographicSize = _zoom;
        }
    }
}
