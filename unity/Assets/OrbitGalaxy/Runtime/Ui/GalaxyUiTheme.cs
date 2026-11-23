using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Orbit.Galaxy.Unity
{
    /// <summary>Normalized rectangle: anchors plus raw offsets. Mirrors the legacy layout helper without depending on it.</summary>
    internal struct GBox
    {
        public float Ax0;
        public float Ay0;
        public float Ax1;
        public float Ay1;
        public float L;
        public float B;
        public float R;
        public float T;

        public GBox(float ax0, float ay0, float ax1, float ay1, float l, float b, float r, float t)
        {
            Ax0 = ax0; Ay0 = ay0; Ax1 = ax1; Ay1 = ay1;
            L = l; B = b; R = r; T = t;
        }

        public static GBox Full { get { return new GBox(0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f); } }

        public static GBox Inset(float left, float bottom, float right, float top)
        {
            return new GBox(0f, 0f, 1f, 1f, left, bottom, -right, -top);
        }

        public static GBox At(float cx, float cy, float w, float h)
        {
            return new GBox(0.5f, 0.5f, 0.5f, 0.5f, cx - w * 0.5f, cy - h * 0.5f, cx + w * 0.5f, cy + h * 0.5f);
        }

        public static GBox TopRow(float y, float h, float left, float right)
        {
            return new GBox(0f, 1f, 1f, 1f, left, -(y + h), -right, -y);
        }

        public static GBox BottomRow(float y, float h, float left, float right)
        {
            return new GBox(0f, 0f, 1f, 0f, left, y, -right, y + h);
        }

        /// <summary>Centered horizontally, anchored to the bottom of the safe area.</summary>
        public static GBox BottomAt(float cx, float bottom, float w, float h)
        {
            return new GBox(0.5f, 0f, 0.5f, 0f, cx - w * 0.5f, bottom, cx + w * 0.5f, bottom + h);
        }

        public void Apply(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(Ax0, Ay0);
            rt.anchorMax = new Vector2(Ax1, Ay1);
            rt.offsetMin = new Vector2(L, B);
            rt.offsetMax = new Vector2(R, T);
        }
    }

    internal sealed class GBar
    {
        public RectTransform Root;
        public Image Background;
        public Image Fill;
        private float _target;
        private float _current;

        /// <summary>Immediate value for static bars (quests, research, goals).</summary>
        public void Set(float value01)
        {
            _target = Mathf.Clamp01(value01);
            _current = _target;
            Apply();
        }

        /// <summary>Eased value for live combat bars; call Tick once per frame to animate.</summary>
        public void SetAnimated(float value01)
        {
            _target = Mathf.Clamp01(value01);
            if (GalaxyUiTheme.ReducedMotion)
            {
                _current = _target;
                Apply();
            }
        }

        /// <summary>Smooths HP/shield/energy without allocating or adding per-frame components.</summary>
        public void Tick(float dt)
        {
            if (Mathf.Abs(_current - _target) < 0.0005f) return;
            _current = Mathf.MoveTowards(_current, _target, Mathf.Max(0.001f, dt) * 2.8f);
            Apply();
        }

        public void SetColor(Color color)
        {
            if (Fill != null) Fill.color = color;
        }

        private void Apply()
        {
            if (Fill == null) return;
            RectTransform rt = Fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(_current, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Small unscaled-time CanvasGroup tween for screens, overlays and toasts.</summary>
    internal sealed class GFade : MonoBehaviour
    {
        private CanvasGroup _group;
        private float _until;
        private float _duration;
        private bool _running;

        public void PlayIn(float duration, bool blocksRaycasts = true)
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            // Keep Selectables interactive: CanvasGroup.interactable=false would disable every
            // button in a screen, which is not what a visual fade means here.
            _group.interactable = true;
            _group.blocksRaycasts = blocksRaycasts;
            if (GalaxyUiTheme.ReducedMotion || duration <= 0f)
            {
                _group.alpha = 1f;
                _running = false;
                enabled = false;
                return;
            }
            _duration = duration;
            _until = Time.unscaledTime + duration;
            _group.alpha = 0f;
            _running = true;
            enabled = true;
        }

        private void Update()
        {
            if (!_running || _group == null) return;
            float remain = _until - Time.unscaledTime;
            if (remain <= 0f)
            {
                _group.alpha = 1f;
                _running = false;
                enabled = false;
                return;
            }
            _group.alpha = 1f - remain / _duration;
        }
    }

    /// <summary>Lightweight integer count-up used for match rewards. No allocations per frame.</summary>
    internal sealed class GCountUp : MonoBehaviour
    {
        private Text _label;
        private string _prefix;
        private string _suffix;
        private int _target;
        private float _until;
        private float _duration;
        private bool _running;

        public void Play(Text label, int target, string prefix, string suffix, float duration)
        {
            _label = label;
            _target = target;
            _prefix = prefix ?? string.Empty;
            _suffix = suffix ?? string.Empty;
            if (_label == null)
            {
                _running = false;
                enabled = false;
                return;
            }
            if (GalaxyUiTheme.ReducedMotion || duration <= 0f)
            {
                _label.text = _prefix + target + _suffix;
                _running = false;
                enabled = false;
                return;
            }
            _duration = duration;
            _until = Time.unscaledTime + duration;
            _label.text = _prefix + "0" + _suffix;
            _running = true;
            enabled = true;
        }

        private void Update()
        {
            if (!_running || _label == null) return;
            float remain = _until - Time.unscaledTime;
            if (remain <= 0f)
            {
                _label.text = _prefix + _target + _suffix;
                _running = false;
                enabled = false;
                return;
            }
            int value = Mathf.RoundToInt(_target * (1f - remain / _duration));
            _label.text = _prefix + value + _suffix;
        }
    }

    internal sealed class GToggle
    {
        public RectTransform Root;
        public Toggle Toggle;
        public Image Background;
        public Image Knob;

        public void Apply(bool on, Color accent)
        {
            if (Background != null) Background.color = on ? GalaxyUiTheme.WithAlpha(accent, 0.42f) : new Color32(30, 38, 66, 255);
            if (Knob != null) Knob.color = on ? accent : new Color32(150, 162, 196, 255);
            if (Knob == null) return;
            RectTransform rt = Knob.rectTransform;
            if (on)
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(-26f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(26f, -2f);
            }
        }
    }

    internal sealed class GSlider
    {
        public RectTransform Root;
        public Slider Slider;
        public Image Fill;
        public Image Handle;
    }

    /// <summary>Procedural uGUI primitives: no TextMeshPro, no external fonts, no editor assets.</summary>
    internal static class GalaxyUiTheme
    {
        internal static readonly Color Ink = new Color32(8, 12, 30, 255);
        internal static readonly Color Deep = new Color32(6, 9, 22, 255);
        internal static readonly Color PanelColor = new Color32(18, 25, 51, 244);
        internal static readonly Color PanelSoft = new Color32(24, 33, 65, 224);
        internal static readonly Color Paper = new Color32(240, 246, 255, 255);
        internal static readonly Color Muted = new Color32(148, 164, 202, 255);
        internal static readonly Color Lime = new Color32(182, 255, 61, 255);
        internal static readonly Color Coral = new Color32(255, 77, 126, 255);
        internal static readonly Color Sun = new Color32(255, 213, 74, 255);
        internal static readonly Color Violet = new Color32(157, 123, 255, 255);
        internal static readonly Color Cyan = new Color32(61, 230, 255, 255);
        internal static readonly Color Orange = new Color32(255, 150, 61, 255);

        internal static Color AccentA = Lime;
        internal static Color AccentB = Coral;
        internal static Color AccentC = Cyan;

        internal static bool LargeText;
        internal static bool ReducedMotion;

        /// <summary>Wired by GalaxyUi to the audio layer so every button/toggle gives a soft tick.</summary>
        internal static Action UiClick;

        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _round;
        private static Font _font;
        private static bool _fontTried;
        private static readonly Dictionary<Text, int> BaseFontSizes = new Dictionary<Text, int>();
        private static readonly Dictionary<HullShape, Sprite> HullSprites = new Dictionary<HullShape, Sprite>();

        internal static void SetColorblind(bool enabled)
        {
            if (enabled)
            {
                AccentA = Cyan;
                AccentB = Sun;
                AccentC = Violet;
            }
            else
            {
                AccentA = Lime;
                AccentB = Coral;
                AccentC = Cyan;
            }
        }

        internal static void SetLargeText(bool enabled)
        {
            LargeText = enabled;
            List<Text> dead = null;
            foreach (KeyValuePair<Text, int> pair in BaseFontSizes)
            {
                if (pair.Key == null)
                {
                    if (dead == null) dead = new List<Text>();
                    dead.Add(pair.Key);
                    continue;
                }
                pair.Key.fontSize = enabled ? Mathf.RoundToInt(pair.Value * 1.16f) : pair.Value;
            }
            if (dead != null)
                for (int i = 0; i < dead.Count; i++) BaseFontSizes.Remove(dead[i]);
        }

        internal static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// Keeps the 1600x900 design inside any landscape aspect: wide screens fit height and gain
        /// horizontal room, narrow screens fit width and gain vertical room. Without this, phones
        /// with 19.5:9 displays clip bottom buttons while tablets clip header controls.
        /// </summary>
        internal static void ApplyCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null) return;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            scaler.matchWidthOrHeight = aspect >= 16f / 9f ? 1f : 0f;
        }

        internal static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.name = "GalaxyWhite";
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    tex.wrapMode = TextureWrapMode.Clamp;
                    _white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
                }
                return _white;
            }
        }

        internal static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle();
                return _circle;
            }
        }

        internal static Sprite Round
        {
            get
            {
                if (_round == null) _round = BuildRounded();
                return _round;
            }
        }

        internal static Font UiFont
        {
            get
            {
                if (_fontTried) return _font;
                _fontTried = true;
                string[] names = { "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans", "Tahoma", "Segoe UI" };
                try { _font = Font.CreateDynamicFontFromOSFont(names, 32); }
                catch (Exception ex) { Debug.LogWarning("Galaxy UI: системный шрифт недоступен: " + ex.Message); _font = null; }
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
                if (_font == null) Debug.LogWarning("Galaxy UI: шрифт не найден, подписи отключены.");
                return _font;
            }
        }

        private static Sprite BuildCircle()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "GalaxyCircle";
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 28f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRounded()
        {
            const int size = 64;
            const float radius = 15f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "GalaxyRounded";
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Abs(px - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(py - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector4 border = new Vector4(20f, 20f, 20f, 20f);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        internal static Sprite Hull(HullShape shape)
        {
            Sprite cached;
            if (HullSprites.TryGetValue(shape, out cached) && cached != null) return cached;
            const int size = 64;
            float[][] poly = HullPolygon(shape);
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "GalaxyHull" + shape;
            Color32[] pixels = new Color32[size * size];
            bool ring = shape == HullShape.Ring || shape == HullShape.Core;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float px = ((x + (sx + 0.5f) * 0.5f) / size) * 2f - 1f;
                            float py = ((y + (sy + 0.5f) * 0.5f) / size) * 2f - 1f;
                            bool inside = InsidePolygon(px, py, poly);
                            if (ring)
                            {
                                float d = Mathf.Sqrt(px * px + py * py);
                                inside = d < 0.92f && d > (shape == HullShape.Ring ? 0.42f : 0.30f);
                            }
                            if (inside) coverage += 0.25f;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            cached = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            HullSprites[shape] = cached;
            return cached;
        }

        private static float[][] HullPolygon(HullShape shape)
        {
            switch (shape)
            {
                case HullShape.Dart:
                    return new[] { P(0f, 1f), P(-0.62f, -0.62f), P(0f, -0.32f), P(0.62f, -0.62f) };
                case HullShape.Wedge:
                    return new[] { P(0f, 1f), P(-0.82f, -0.72f), P(0.82f, -0.72f) };
                case HullShape.Bulwark:
                    return new[] { P(-0.70f, 0.92f), P(0.70f, 0.92f), P(0.92f, -0.68f), P(0f, -0.96f), P(-0.92f, -0.68f) };
                case HullShape.Blade:
                    return new[] { P(0f, 1f), P(-0.44f, 0.12f), P(-0.18f, -1f), P(0.18f, -1f), P(0.44f, 0.12f) };
                case HullShape.Spike:
                    return new[] { P(0f, 1f), P(-0.36f, -0.18f), P(-0.78f, -0.92f), P(0f, -0.48f), P(0.78f, -0.92f), P(0.36f, -0.18f) };
                case HullShape.Ring:
                case HullShape.Core:
                    return new[]
                    {
                        P(0f, 1f), P(-0.71f, 0.71f), P(-1f, 0f), P(-0.71f, -0.71f),
                        P(0f, -1f), P(0.71f, -0.71f), P(1f, 0f), P(0.71f, 0.71f)
                    };
                default:
                    return new[]
                    {
                        P(0f, 1f), P(-0.87f, 0.5f), P(-0.87f, -0.5f),
                        P(0f, -1f), P(0.87f, -0.5f), P(0.87f, 0.5f)
                    };
            }
        }

        private static float[] P(float x, float y) { return new[] { x, y }; }

        private static bool InsidePolygon(float x, float y, float[][] poly)
        {
            bool inside = false;
            int j = poly.Length - 1;
            for (int i = 0; i < poly.Length; j = i++)
            {
                float xi = poly[i][0], yi = poly[i][1];
                float xj = poly[j][0], yj = poly[j][1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        internal static RectTransform Place(Transform parent, string name, GBox box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            box.Apply(rt);
            return rt;
        }

        internal static Image Panel(Transform parent, string name, Color color, GBox box, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Place(parent, name, box);
            Image img = rt.gameObject.AddComponent<Image>();
            Sprite sp = sprite != null ? sprite : White;
            img.sprite = sp;
            img.type = sp == Round ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        internal static Image CircleImage(Transform parent, string name, Color color, GBox box, bool raycast = false)
        {
            return Panel(parent, name, color, box, Circle, raycast);
        }

        internal static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, GBox box,
            FontStyle style = FontStyle.Normal, bool rich = true)
        {
            RectTransform rt = Place(parent, "Текст", box);
            Text label = rt.gameObject.AddComponent<Text>();
            Font f = UiFont;
            if (f != null) label.font = f;
            else label.enabled = false;
            label.text = text ?? string.Empty;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = align;
            label.supportRichText = rich;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            BaseFontSizes[label] = size;
            if (LargeText) label.fontSize = Mathf.RoundToInt(size * 1.16f);
            return label;
        }

        internal static Button MakeButton(Transform parent, string name, string label, Color fill, Color textColor, GBox box,
            int fontSize, UnityAction onClick, Sprite sprite = null, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = Place(parent, name, box);
            Image img = rt.gameObject.AddComponent<Image>();
            Sprite sp = sprite != null ? sprite : Round;
            img.sprite = sp;
            img.type = sp == Round ? Image.Type.Sliced : Image.Type.Simple;
            img.color = fill;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
            cb.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.45f, 0.48f, 0.58f, 0.7f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = ReducedMotion ? 0f : 0.06f;
            btn.colors = cb;

            // Soft depth instead of a bright 3px outline around every button.
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.34f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            Image gloss = Panel(rt, "Блик", new Color(1f, 1f, 1f, 0.075f),
                new GBox(0f, 1f, 1f, 1f, 10f, -6f, -10f, -3f), Round);
            gloss.raycastTarget = false;

            Text text = Label(rt, label, fontSize, textColor, TextAnchor.MiddleCenter, GBox.Inset(8f, 3f, 8f, 3f), style);
            text.name = "Подпись";
            btn.onClick.AddListener(delegate { if (UiClick != null) UiClick(); });
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        internal static GBar Bar(Transform parent, string name, Color back, Color fill, GBox box)
        {
            RectTransform rt = Place(parent, name, box);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Round;
            bg.type = Image.Type.Sliced;
            bg.color = back;
            bg.raycastTarget = false;
            RectTransform fillRt = Place(rt, "Заполнение", new GBox(0f, 0f, 1f, 1f, 3f, 3f, -3f, -3f));
            Image fg = fillRt.gameObject.AddComponent<Image>();
            fg.sprite = White;
            fg.color = fill;
            fg.raycastTarget = false;
            GBar view = new GBar { Root = rt, Background = bg, Fill = fg };
            view.Set(0f);
            return view;
        }

        internal static RectTransform Scroll(Transform parent, string name, GBox box, out RectTransform content)
        {
            RectTransform root = Place(parent, name, box);
            Image img = root.gameObject.AddComponent<Image>();
            img.sprite = Round;
            img.type = Image.Type.Sliced;
            img.color = new Color32(10, 15, 34, 210);
            img.raycastTarget = true;

            RectTransform viewport = Place(root, "Viewport", GBox.Inset(6f, 6f, 6f, 6f));
            Image vpImage = viewport.gameObject.AddComponent<Image>();
            vpImage.color = new Color(1f, 1f, 1f, 0.01f);
            vpImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Place(viewport, "Content", new GBox(0f, 1f, 1f, 1f, 0f, -100f, 0f, 0f));
            content.pivot = new Vector2(0.5f, 1f);

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26f;
            return root;
        }

        internal static void SetScrollHeight(RectTransform content, float height)
        {
            if (content == null) return;
            content.offsetMin = new Vector2(content.offsetMin.x, -Mathf.Max(40f, height));
            content.offsetMax = new Vector2(content.offsetMax.x, 0f);
        }

        internal static GToggle Toggle(Transform parent, string name, bool value, Color accent, GBox box, UnityAction<bool> onChanged)
        {
            RectTransform root = Place(parent, name, box);
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = Round;
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            RectTransform knob = Place(root, "Ползунок", new GBox(0f, 0f, 0f, 1f, 2f, 2f, 26f, -2f));
            Image knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = Circle;
            knobImage.raycastTarget = false;

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = knobImage;
            toggle.graphic = bg;
            toggle.isOn = value;
            toggle.transition = Selectable.Transition.None;

            GToggle view = new GToggle { Root = root, Toggle = toggle, Background = bg, Knob = knobImage };
            view.Apply(value, accent);
            toggle.onValueChanged.AddListener(delegate(bool on)
            {
                view.Apply(on, accent);
                if (UiClick != null) UiClick();
                if (onChanged != null) onChanged(on);
            });
            return view;
        }

        internal static GSlider Slider(Transform parent, string name, float value, Color accent, GBox box, UnityAction<float> onChanged)
        {
            RectTransform root = Place(parent, name, box);
            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = Round;
            bg.type = Image.Type.Sliced;
            bg.color = new Color32(34, 44, 76, 255);
            bg.raycastTarget = true;

            RectTransform fillArea = Place(root, "Область заполнения", new GBox(0f, 0f, 1f, 1f, 8f, 0f, -8f, 0f));
            RectTransform fillRt = Place(fillArea, "Заполнение", new GBox(0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f));
            Image fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = White;
            fill.color = accent;
            fill.raycastTarget = false;

            RectTransform handleArea = Place(root, "Область ползунка", new GBox(0f, 0f, 1f, 1f, 8f, 0f, -8f, 0f));
            RectTransform handleRt = Place(handleArea, "Ползунок", new GBox(0f, 0f, 0f, 1f, -10f, 0f, 10f, 0f));
            Image handle = handleRt.gameObject.AddComponent<Image>();
            handle.sprite = Circle;
            handle.color = Paper;
            handle.raycastTarget = false;

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.value = Mathf.Clamp01(value);
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);

            return new GSlider { Root = root, Slider = slider, Fill = fill, Handle = handle };
        }
    }
}
