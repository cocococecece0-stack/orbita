using System;
using System.Collections.Generic;
using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Процедурные спрайты и шрифт для Galaxy-слоя. Никаких внешних ассетов и TextMeshPro.
    /// Мировые спрайты создаются с PPU, равным размеру текстуры, поэтому одна текстура = 1 мировая единица.
    /// </summary>
    public sealed class TextureFactory : MonoBehaviour
    {
        private const int WorldSize = 64;
        private const int IconSize = 64;
        private const int GridSize = 256;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Font _font;
        private static bool _fontTried;

        public static readonly Color Ink = new Color32(7, 10, 24, 255);
        public static readonly Color Deep = new Color32(11, 16, 34, 255);
        public static readonly Color Slate = new Color32(24, 30, 55, 255);
        public static readonly Color Paper = new Color32(238, 246, 255, 255);
        public static readonly Color Lime = new Color32(182, 255, 61, 255);
        public static readonly Color Coral = new Color32(255, 77, 126, 255);
        public static readonly Color Sun = new Color32(255, 213, 74, 255);
        public static readonly Color Violet = new Color32(157, 123, 255, 255);
        public static readonly Color Cyan = new Color32(61, 230, 255, 255);
        public static readonly Color Orange = new Color32(255, 150, 61, 255);
        public static readonly Color Pink = new Color32(255, 110, 200, 255);

        /// <summary>Разбор HEX из ContentDatabase с безопасным запасным цветом.</summary>
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            string value = hex[0] == '#' ? hex : "#" + hex;
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : fallback;
        }

        public static Color BranchColor(EvolutionBranch branch)
        {
            switch (branch)
            {
                case EvolutionBranch.Titan: return Orange;
                case EvolutionBranch.Phantom: return Cyan;
                case EvolutionBranch.Swarm: return Lime;
                case EvolutionBranch.Singularity: return Violet;
                default: return Paper;
            }
        }

        public static Color PersonalityColor(BotPersonality personality)
        {
            switch (personality)
            {
                case BotPersonality.Aggressive: return Coral;
                case BotPersonality.Farmer: return Lime;
                case BotPersonality.Survivor: return Cyan;
                case BotPersonality.Hunter: return Orange;
                case BotPersonality.Scavenger: return Sun;
                case BotPersonality.Elite: return Violet;
                case BotPersonality.Chaotic: return Pink;
                default: return Paper;
            }
        }

        public static Color WeaponColor(WeaponKind weapon)
        {
            switch (weapon)
            {
                case WeaponKind.Plasma: return Violet;
                case WeaponKind.Missile: return Orange;
                case WeaponKind.Drone: return Lime;
                case WeaponKind.Ring: return Sun;
                case WeaponKind.Pulse: return Pink;
                case WeaponKind.Lightning: return Paper;
                case WeaponKind.Orbital: return Violet;
                default: return Cyan;
            }
        }

        public static Sprite Circle()
        {
            return Circle(Color.white);
        }

        public static Sprite Circle(Color color)
        {
            int size = WorldSize;
            return Make("circle_" + ColorKey(color), size, size, delegate(Color32[] px, int n)
            {
                FillDisc(px, n, 0.5f, 0.5f, 0.5f, color, 0.035f);
            });
        }

        public static Sprite Circle(string hex)
        {
            return Circle(Parse(hex, Paper));
        }

        /// <summary>Круг для uGUI: 100 пикселей на единицу, как у стандартных спрайтов интерфейса.</summary>
        public static Sprite UiCircle()
        {
            return Make("ui_circle", IconSize, 100f, delegate(Color32[] px, int n)
            {
                FillDisc(px, n, 0.5f, 0.5f, 0.5f, Color.white, 0.035f);
            });
        }

        public static Sprite UiSquare()
        {
            return Make("ui_square", 8, 100f, delegate(Color32[] px, int n)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++) px[y * n + x] = new Color32(255, 255, 255, 255);
            });
        }

        public static Sprite Square()
        {
            return Make("square", 8, 8f, delegate(Color32[] px, int n)
            {
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++) px[y * n + x] = new Color32(255, 255, 255, 255);
            });
        }

        public static Sprite Glow()
        {
            return Make("glow", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                FillDisc(px, n, 0.5f, 0.5f, 0.5f, Color.white, 0.5f);
            });
        }

        public static Sprite Ring()
        {
            return Make("ring", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                DrawRing(px, n, 0.5f, 0.5f, 0.37f, 0.5f, Color.white, 0.02f);
            });
        }

        /// <summary>Тонкое кольцо для границы арены: не закрашивает игровое поле.</summary>
        public static Sprite ThinRing()
        {
            return Make("thin_ring", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                DrawRing(px, n, 0.5f, 0.5f, 0.462f, 0.5f, Color.white, 0.012f);
            });
        }

        public static Sprite Diamond()
        {
            return Make("diamond", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                float[] xs = { 0.5f, 0.94f, 0.5f, 0.06f };
                float[] ys = { 0.96f, 0.5f, 0.04f, 0.5f };
                FillPoly(px, n, xs, ys, Color.white);
            });
        }

        public static Sprite Star()
        {
            return Make("star", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                float[] xs = new float[10];
                float[] ys = new float[10];
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * 0.5f - i * Mathf.PI / 5f;
                    float radius = (i % 2 == 0) ? 0.49f : 0.21f;
                    xs[i] = 0.5f + Mathf.Cos(angle) * radius;
                    ys[i] = 0.5f + Mathf.Sin(angle) * radius;
                }
                FillPoly(px, n, xs, ys, Color.white);
            });
        }

        public static Sprite Rock()
        {
            return Make("rock", WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                float[] xs = { 0.08f, 0.46f, 0.92f, 0.78f, 0.34f, 0.02f };
                float[] ys = { 0.34f, 0.06f, 0.28f, 0.82f, 0.96f, 0.68f };
                FillPoly(px, n, xs, ys, Color.white);
                FillDisc(px, n, 0.38f, 0.62f, 0.10f, new Color(1f, 1f, 1f, 0.35f), 0.06f);
            });
        }

        public static Sprite Grid()
        {
            return Make("grid", GridSize, (float)GridSize, delegate(Color32[] px, int n)
            {
                Color line = new Color(1f, 1f, 1f, 0.16f);
                Color major = new Color(1f, 1f, 1f, 0.30f);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        bool small = (x % 32) < 2 || (y % 32) < 2;
                        bool big = (x % 128) < 2 || (y % 128) < 2;
                        if (big) Blend(px, n, x, y, major);
                        else if (small) Blend(px, n, x, y, line);
                    }
                }
            });
        }

        public static Sprite Hull(HullShape shape)
        {
            return Hull(shape, "#6CF6D2", "#B6FF3D");
        }

        public static Sprite Hull(HullShape shape, string primaryHex, string accentHex)
        {
            Color primary = Parse(primaryHex, Cyan);
            Color accent = Parse(accentHex, Lime);
            string key = "hull_" + (int)shape + "_" + ColorKey(primary) + "_" + ColorKey(accent);
            return Make(key, WorldSize, WorldSize, delegate(Color32[] px, int n)
            {
                DrawHull(px, n, shape, primary, accent);
            });
        }

        /// <summary>OS-шрифт с кириллицей, запасной — встроенный шрифт Unity.</summary>
        public static Font UiFont
        {
            get
            {
                if (_fontTried) return _font;
                _fontTried = true;
                string[] names = { "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans", "Tahoma", "Segoe UI" };
                try { _font = Font.CreateDynamicFontFromOSFont(names, 32); }
                catch (Exception ex)
                {
                    Debug.LogWarning("ORBITA GALAXY: системный шрифт недоступен: " + ex.Message);
                    _font = null;
                }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (Exception) { _font = null; }
                }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch (Exception) { _font = null; }
                }
                if (_font == null) Debug.LogWarning("ORBITA GALAXY: шрифт не найден, подписи будут скрыты.");
                return _font;
            }
        }

        private static string ColorKey(Color c)
        {
            return ((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f)).ToString("X2")
                + ((byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f)).ToString("X2")
                + ((byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f)).ToString("X2")
                + ((byte)Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f)).ToString("X2");
        }

        private static Sprite Make(string key, int size, float ppu, Action<Color32[], int> paint)
        {
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;
            Color32[] pixels = new Color32[size * size];
            paint(pixels, size);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Galaxy_" + key;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            sprite.name = "Galaxy_" + key;
            Cache[key] = sprite;
            return sprite;
        }

        private static void DrawHull(Color32[] px, int n, HullShape shape, Color primary, Color accent)
        {
            float[] xs;
            float[] ys;
            switch (shape)
            {
                case HullShape.Dart:
                    xs = new[] { 0.97f, 0.30f, 0.42f, 0.30f };
                    ys = new[] { 0.50f, 0.88f, 0.50f, 0.12f };
                    break;
                case HullShape.Wedge:
                    xs = new[] { 0.96f, 0.14f, 0.40f, 0.14f };
                    ys = new[] { 0.50f, 0.92f, 0.50f, 0.08f };
                    break;
                case HullShape.Hex:
                    xs = new[] { 0.96f, 0.62f, 0.20f, 0.04f, 0.20f, 0.62f };
                    ys = new[] { 0.50f, 0.88f, 0.88f, 0.50f, 0.12f, 0.12f };
                    break;
                case HullShape.Bulwark:
                    xs = new[] { 0.92f, 0.70f, 0.24f, 0.06f, 0.24f, 0.70f };
                    ys = new[] { 0.50f, 0.84f, 0.94f, 0.50f, 0.06f, 0.16f };
                    break;
                case HullShape.Blade:
                    xs = new[] { 0.98f, 0.56f, 0.10f, 0.48f, 0.10f, 0.56f };
                    ys = new[] { 0.50f, 0.70f, 0.92f, 0.50f, 0.08f, 0.30f };
                    break;
                case HullShape.Spike:
                    xs = new[] { 0.99f, 0.62f, 0.20f, 0.52f, 0.20f, 0.62f };
                    ys = new[] { 0.50f, 0.68f, 0.94f, 0.50f, 0.06f, 0.32f };
                    break;
                case HullShape.Ring:
                    DrawRing(px, n, 0.50f, 0.50f, 0.27f, 0.49f, primary, 0.02f);
                    FillDisc(px, n, 0.50f, 0.50f, 0.12f, accent, 0.04f);
                    FillDisc(px, n, 0.76f, 0.50f, 0.08f, accent, 0.04f);
                    return;
                case HullShape.Core:
                default:
                    xs = new[] { 0.90f, 0.64f, 0.32f, 0.10f, 0.32f, 0.64f, 0.78f, 0.78f };
                    ys = new[] { 0.50f, 0.78f, 0.90f, 0.50f, 0.10f, 0.22f, 0.28f, 0.72f };
                    break;
            }
            FillPoly(px, n, xs, ys, primary);
            Color cockpit = new Color(accent.r, accent.g, accent.b, 1f);
            FillDisc(px, n, 0.62f, 0.50f, 0.10f, cockpit, 0.03f);
            Color engine = new Color(accent.r, accent.g, accent.b, 0.85f);
            FillDisc(px, n, 0.20f, 0.40f, 0.07f, engine, 0.03f);
            FillDisc(px, n, 0.20f, 0.60f, 0.07f, engine, 0.03f);
        }

        private static void FillDisc(Color32[] px, int n, float cx, float cy, float radius, Color color, float soft)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((cx - radius) * n) - 1);
            int maxX = Mathf.Min(n - 1, Mathf.CeilToInt((cx + radius) * n) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt((cy - radius) * n) - 1);
            int maxY = Mathf.Min(n - 1, Mathf.CeilToInt((cy + radius) * n) + 1);
            float edge = Mathf.Max(0.001f, soft);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f) / n - cx;
                    float dy = (y + 0.5f) / n - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((radius - d) / edge);
                    if (a <= 0f) continue;
                    Blend(px, n, x, y, new Color(color.r, color.g, color.b, color.a * a));
                }
            }
        }

        private static void DrawRing(Color32[] px, int n, float cx, float cy, float inner, float outer, Color color, float soft)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((cx - outer) * n) - 1);
            int maxX = Mathf.Min(n - 1, Mathf.CeilToInt((cx + outer) * n) + 1);
            int minY = Mathf.Max(0, Mathf.FloorToInt((cy - outer) * n) - 1);
            int maxY = Mathf.Min(n - 1, Mathf.CeilToInt((cy + outer) * n) + 1);
            float edge = Mathf.Max(0.001f, soft);
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = (x + 0.5f) / n - cx;
                    float dy = (y + 0.5f) / n - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - inner) / edge) * Mathf.Clamp01((outer - d) / edge);
                    if (a <= 0f) continue;
                    Blend(px, n, x, y, new Color(color.r, color.g, color.b, color.a * a));
                }
            }
        }

        private static void FillPoly(Color32[] px, int n, float[] xs, float[] ys, Color color)
        {
            int count = xs.Length;
            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            for (int i = 0; i < count; i++)
            {
                minX = Mathf.Min(minX, xs[i]); maxX = Mathf.Max(maxX, xs[i]);
                minY = Mathf.Min(minY, ys[i]); maxY = Mathf.Max(maxY, ys[i]);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * n) - 1);
            int x1 = Mathf.Min(n - 1, Mathf.CeilToInt(maxX * n) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * n) - 1);
            int y1 = Mathf.Min(n - 1, Mathf.CeilToInt(maxY * n) + 1);
            for (int y = y0; y <= y1; y++)
            {
                float py = (y + 0.5f) / n;
                for (int x = x0; x <= x1; x++)
                {
                    float pxx = (x + 0.5f) / n;
                    if (!Inside(pxx, py, xs, ys, count)) continue;
                    Blend(px, n, x, y, color);
                }
            }
        }

        private static bool Inside(float x, float y, float[] xs, float[] ys, int count)
        {
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if ((ys[i] > y) != (ys[j] > y))
                {
                    float cross = xs[i] + (y - ys[i]) * (xs[j] - xs[i]) / (ys[j] - ys[i]);
                    if (x < cross) inside = !inside;
                }
            }
            return inside;
        }

        private static void Blend(Color32[] px, int n, int x, int y, Color src)
        {
            if (x < 0 || y < 0 || x >= n || y >= n) return;
            int index = y * n + x;
            Color32 dst = px[index];
            float sa = Mathf.Clamp01(src.a);
            if (sa <= 0f) return;
            float da = dst.a / 255f;
            float oa = sa + da * (1f - sa);
            if (oa <= 0f) return;
            float r = (src.r * sa + dst.r / 255f * da * (1f - sa)) / oa;
            float g = (src.g * sa + dst.g / 255f * da * (1f - sa)) / oa;
            float b = (src.b * sa + dst.b / 255f * da * (1f - sa)) / oa;
            px[index] = new Color32((byte)Mathf.RoundToInt(r * 255f), (byte)Mathf.RoundToInt(g * 255f),
                (byte)Mathf.RoundToInt(b * 255f), (byte)Mathf.RoundToInt(oa * 255f));
        }
    }
}
