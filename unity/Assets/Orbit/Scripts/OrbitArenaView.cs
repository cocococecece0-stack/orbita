using System.Collections.Generic;
using Orbit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Орбита: планеты, кольцо, маркер игрока и пулы астероидов/сфер/бонусов.</summary>
    public sealed class OrbitArenaView : MonoBehaviour
    {
        private const float RingRadius = 300f;
        private const int RingDots = 72;

        private OrbitApp _app;
        private RectTransform _root;
        private Image _planet;
        private Image _planetCore;
        private Image _planetGlint;
        private Image _player;
        private Image _trail;
        private Image _shieldRing;
        private Image _resonanceRing;
        private Image[] _ringDots;
        private Image[] _asteroids;
        private Image[] _orbs;
        private Image[] _powerups;

        private static readonly Color[] PlanetColors = { UiFactory.Violet, UiFactory.Coral, UiFactory.Sun, UiFactory.Cyan };
        private static readonly Color[] ShipColors = { UiFactory.Lime, UiFactory.Paper, UiFactory.Cyan, UiFactory.Coral };
        private static readonly Color[] TrailColors = { UiFactory.Lime, UiFactory.Violet, UiFactory.Cyan, UiFactory.Sun };
        private static readonly Color[] AsteroidColors = { UiFactory.Coral, UiFactory.Sun, UiFactory.Violet, UiFactory.Pink };

        public void Initialize(OrbitApp app, Transform parent)
        {
            _app = app;
            if (_root != null) return;
            _root = UiFactory.Place(parent, "Орбита", UiBox.Full);
            Build();
        }

        private void Build()
        {
            UiFactory.Circle(_root, "Ореол", UiFactory.WithAlpha(UiFactory.Violet, 0.08f), UiBox.At(0f, 0f, 900f, 900f));

            _ringDots = new Image[RingDots];
            for (int i = 0; i < RingDots; i++)
            {
                float angle = (float)i / RingDots * Mathf.PI * 2f;
                Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RingRadius;
                _ringDots[i] = UiFactory.Circle(_root, "Точка орбиты " + i, UiFactory.WithAlpha(UiFactory.Paper, 0.34f),
                    UiBox.At(pos.x, pos.y, 11f, 11f));
            }

            for (int i = 0; i < 48; i++)
            {
                float angle = (float)i / 48f * Mathf.PI * 2f;
                Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 420f;
                UiFactory.Circle(_root, "Внешняя точка " + i, UiFactory.WithAlpha(UiFactory.Paper, 0.11f),
                    UiBox.At(pos.x, pos.y, 7f, 7f));
            }

            _planet = UiFactory.Circle(_root, "Планета", UiFactory.Violet, UiBox.At(0f, 0f, 280f, 280f));
            _planetCore = UiFactory.Circle(_root, "Ядро планеты", UiFactory.WithAlpha(UiFactory.Deep, 0.72f), UiBox.At(0f, 0f, 190f, 190f));
            _planetGlint = UiFactory.Circle(_root, "Блик", UiFactory.WithAlpha(UiFactory.Paper, 0.55f), UiBox.At(-58f, 62f, 54f, 54f));

            _resonanceRing = UiFactory.Circle(_root, "Резонанс", new Color(1f, 1f, 1f, 0f), UiBox.At(0f, 0f, 130f, 130f));
            _shieldRing = UiFactory.Circle(_root, "Щит", UiFactory.WithAlpha(UiFactory.Cyan, 0.34f), UiBox.At(0f, 0f, 86f, 86f));
            _trail = UiFactory.Circle(_root, "След", UiFactory.WithAlpha(UiFactory.Lime, 0.25f), UiBox.At(0f, 0f, 30f, 30f));
            _player = UiFactory.Circle(_root, "Игрок", UiFactory.Lime, UiBox.At(0f, 0f, 46f, 46f));

            _asteroids = MakePool("Астероид", 72);
            _orbs = MakePool("Сфера", 48);
            _powerups = MakePool("Бонус", 16);
        }

        private Image[] MakePool(string name, int count)
        {
            Image[] pool = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Image image = UiFactory.Circle(_root, name + " " + i, Color.white, UiBox.At(0f, 0f, 24f, 24f));
                image.gameObject.SetActive(false);
                pool[i] = image;
            }
            return pool;
        }

        private static Vector2 Polar(float angle, float radius)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Color Pick(Color[] colors, int index)
        {
            if (colors == null || colors.Length == 0) return UiFactory.Paper;
            int i = Mathf.Abs(index) % colors.Length;
            return colors[i];
        }

        private void Update()
        {
            if (_app == null || _app.Engine == null || _root == null) return;
            GameEngineCore engine = _app.Engine;
            SaveData save = _app.Save;
            bool reduced = save != null && save.settings != null && save.settings.reducedMotion;

            if (save != null)
            {
                if (_planet != null) _planet.color = Pick(PlanetColors, save.equippedPlanet);
                if (_player != null) _player.color = Pick(ShipColors, save.equippedShip);
                if (_trail != null) _trail.color = UiFactory.WithAlpha(Pick(TrailColors, save.equippedTrail), 0.32f);
            }

            float playerRadius = Mathf.Max(0.2f, engine.PlayerRadius) * RingRadius;
            Vector2 playerPos = Polar(engine.PlayerAngle, playerRadius);
            Vector2 trailPos = Polar(engine.PlayerAngle - 0.09f * engine.Direction, playerRadius);

            if (_player != null) _player.rectTransform.anchoredPosition = playerPos;
            if (_trail != null) _trail.rectTransform.anchoredPosition = trailPos;
            if (_resonanceRing != null)
            {
                _resonanceRing.rectTransform.anchoredPosition = playerPos;
                _resonanceRing.color = UiFactory.WithAlpha(UiFactory.Sun, Mathf.Clamp01(engine.Resonance) * 0.4f);
            }
            if (_shieldRing != null)
            {
                _shieldRing.rectTransform.anchoredPosition = playerPos;
                _shieldRing.enabled = engine.Shield;
                if (engine.Shield && !reduced)
                {
                    float scale = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.07f;
                    _shieldRing.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
                else _shieldRing.rectTransform.localScale = Vector3.one;
            }

            float pulse = reduced ? 0f : (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
            Color dotColor = UiFactory.Paper;
            float dotAlpha = 0.30f + pulse * 0.12f;
            if (engine.StormWarning > 0f) { dotColor = UiFactory.Coral; dotAlpha = reduced ? 0.72f : 0.45f + pulse * 0.5f; }
            else if (engine.FlareWarning > 0f) { dotColor = UiFactory.Sun; dotAlpha = reduced ? 0.72f : 0.45f + pulse * 0.5f; }
            for (int i = 0; i < _ringDots.Length; i++)
            {
                if (_ringDots[i] != null) _ringDots[i].color = UiFactory.WithAlpha(dotColor, dotAlpha);
            }

            UpdatePool(_asteroids, engine.Asteroids, RingRadius, delegate (OrbitThing thing) { return Pick(AsteroidColors, thing.Kind); });
            UpdatePool(_orbs, engine.Orbs, RingRadius, delegate (OrbitThing thing) { return thing.Kind == 1 ? UiFactory.Sun : UiFactory.Lime; });
            UpdatePool(_powerups, engine.PowerUps, RingRadius, delegate (OrbitThing thing) { return UiFactory.Violet; });
        }

        private delegate Color ThingColor(OrbitThing thing);

        private static void UpdatePool(Image[] pool, List<OrbitThing> items, float ring, ThingColor colorFor)
        {
            if (pool == null || items == null) return;
            int count = Mathf.Min(pool.Length, items.Count);
            for (int i = 0; i < pool.Length; i++)
            {
                Image image = pool[i];
                if (image == null) continue;
                if (i >= count)
                {
                    if (image.gameObject.activeSelf) image.gameObject.SetActive(false);
                    continue;
                }
                OrbitThing thing = items[i];
                if (!thing.Active)
                {
                    if (image.gameObject.activeSelf) image.gameObject.SetActive(false);
                    continue;
                }
                if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
                float radius = Mathf.Max(0.1f, thing.Radius) * ring;
                image.rectTransform.anchoredPosition = Polar(thing.Angle, radius);
                float size = Mathf.Clamp(thing.Size * ring * 2f, 14f, 92f);
                image.rectTransform.sizeDelta = new Vector2(size, size);
                image.color = colorFor == null ? UiFactory.Paper : colorFor(thing);
            }
        }
    }
}
