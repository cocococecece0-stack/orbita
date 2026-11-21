using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Прямоугольник в нормализованных якорях + сырые offsetMin/offsetMax.</summary>
    public struct UiBox
    {
        public float Ax0;
        public float Ay0;
        public float Ax1;
        public float Ay1;
        public float L;
        public float B;
        public float R;
        public float T;

        public UiBox(float ax0, float ay0, float ax1, float ay1, float l, float b, float r, float t)
        {
            Ax0 = ax0; Ay0 = ay0; Ax1 = ax1; Ay1 = ay1;
            L = l; B = b; R = r; T = t;
        }

        public static UiBox Full
        {
            get { return new UiBox(0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f); }
        }

        /// <summary>Отступы от краёв родителя (положительные числа — внутрь).</summary>
        public static UiBox Inset(float left, float bottom, float right, float top)
        {
            return new UiBox(0f, 0f, 1f, 1f, left, bottom, -right, -top);
        }

        public static UiBox Inset(float ax0, float ay0, float ax1, float ay1, float left, float bottom, float right, float top)
        {
            return new UiBox(ax0, ay0, ax1, ay1, left, bottom, -right, -top);
        }

        /// <summary>Прямоугольник по центру с размером в опорных пикселях.</summary>
        public static UiBox At(float cx, float cy, float w, float h)
        {
            return new UiBox(0.5f, 0.5f, 0.5f, 0.5f, cx - w * 0.5f, cy - h * 0.5f, cx + w * 0.5f, cy + h * 0.5f);
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

    public sealed class ChipView
    {
        public RectTransform Root;
        public Image Background;
        public Text Label;

        public void SetText(string value)
        {
            if (Label != null) Label.text = value ?? string.Empty;
        }

        public void SetColor(Color color)
        {
            if (Background != null) Background.color = color;
        }

        public void SetLabelColor(Color color)
        {
            if (Label != null) Label.color = color;
        }
    }

    public sealed class BarView
    {
        public RectTransform Root;
        public Image Background;
        public Image Fill;

        public void Set(float value01)
        {
            if (Fill == null) return;
            float v = Mathf.Clamp01(value01);
            RectTransform rt = Fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(v, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Фабрика uGUI без внешних ассетов: спрайты и шрифт создаются в рантайме.</summary>
    public static class UiFactory
    {
        public static readonly Color Ink = new Color32(11, 16, 34, 255);
        public static readonly Color Paper = new Color32(255, 247, 232, 255);
        public static readonly Color Lime = new Color32(182, 255, 61, 255);
        public static readonly Color Coral = new Color32(255, 77, 126, 255);
        public static readonly Color Sun = new Color32(255, 213, 74, 255);
        public static readonly Color Violet = new Color32(157, 123, 255, 255);

        public static readonly Color Deep = new Color32(7, 10, 24, 255);
        public static readonly Color Slate = new Color32(24, 30, 55, 255);
        public static readonly Color Cyan = new Color32(61, 230, 255, 255);
        public static readonly Color Orange = new Color32(255, 150, 61, 255);
        public static readonly Color Pink = new Color32(255, 110, 200, 255);

        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _rounded;
        private static Font _font;
        private static bool _fontTried;

        public static Sprite WhiteSprite
        {
            get
            {
                if (_white == null) _white = MakeWhiteSprite();
                return _white;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (_circle == null) _circle = MakeCircleSprite();
                return _circle;
            }
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = MakeRoundedSprite();
                return _rounded;
            }
        }

        /// <summary>OS-шрифт с кириллицей; запасной — встроенный LegacyRuntime.ttf.</summary>
        public static Font UiFont
        {
            get
            {
                if (_fontTried) return _font;
                _fontTried = true;
                string[] names = { "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans", "Tahoma", "Segoe UI" };
                try { _font = Font.CreateDynamicFontFromOSFont(names, 32); }
                catch (Exception ex) { Debug.LogWarning("Orbit UI: OS-шрифт недоступен: " + ex.Message); _font = null; }
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
                if (_font == null) Debug.LogWarning("Orbit UI: шрифт не найден — подписи будут скрыты.");
                return _font;
            }
        }

        private static Sprite MakeWhiteSprite()
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.name = "OrbitWhite";
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeCircleSprite()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "OrbitCircle";
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * 28f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRoundedSprite()
        {
            const int size = 64;
            const float radius = 15f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "OrbitRounded";
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float dx = Mathf.Max(Mathf.Abs(px - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(py - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
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

        public static RectTransform Place(Transform parent, string name, UiBox box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            box.Apply(rt);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, UiBox box, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Place(parent, name, box);
            Image img = rt.gameObject.AddComponent<Image>();
            Sprite sp = sprite != null ? sprite : WhiteSprite;
            img.sprite = sp;
            img.type = sp == RoundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Circle(Transform parent, string name, Color color, UiBox box, bool raycast = false)
        {
            return Panel(parent, name, color, box, CircleSprite, raycast);
        }

        public static Text TextLabel(Transform parent, string text, int size, Color color, TextAnchor align, UiBox box,
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
            return label;
        }

        public static void FitText(Text text, int minSize, int maxSize)
        {
            if (text == null) return;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color fill, Color textColor, UiBox box,
            int fontSize, UnityAction onClick, Sprite sprite = null, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = Place(parent, name, box);
            Image img = rt.gameObject.AddComponent<Image>();
            Sprite sp = sprite != null ? sprite : RoundedSprite;
            img.sprite = sp;
            img.type = sp == RoundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = fill;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = ColorBlock.defaultColorBlock;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.06f;
            btn.colors = cb;

            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = Luma(fill) > 0.52f ? Ink : Lime;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;

            Text text = TextLabel(rt, label, fontSize, textColor, TextAnchor.MiddleCenter, UiBox.Inset(8f, 4f, 8f, 4f), style);
            text.name = "Подпись";
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label ?? string.Empty;
        }

        public static ChipView Chip(Transform parent, string name, string label, Color fill, Color textColor, UiBox box, int fontSize = 26, Sprite sprite = null)
        {
            RectTransform rt = Place(parent, name, box);
            Image img = rt.gameObject.AddComponent<Image>();
            Sprite sp = sprite != null ? sprite : RoundedSprite;
            img.sprite = sp;
            img.type = sp == RoundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = fill;
            img.raycastTarget = false;
            Text text = TextLabel(rt, label, fontSize, textColor, TextAnchor.MiddleCenter, UiBox.Inset(6f, 2f, 6f, 2f), FontStyle.Bold);
            return new ChipView { Root = rt, Background = img, Label = text };
        }

        public static BarView Bar(Transform parent, string name, Color back, Color fill, UiBox box)
        {
            RectTransform rt = Place(parent, name, box);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = back;
            bg.raycastTarget = false;

            RectTransform fillRt = Place(rt, "Заполнение", new UiBox(0f, 0f, 1f, 1f, 4f, 4f, -4f, -4f));
            Image fg = fillRt.gameObject.AddComponent<Image>();
            fg.sprite = WhiteSprite;
            fg.color = fill;
            fg.raycastTarget = false;
            BarView view = new BarView { Root = rt, Background = bg, Fill = fg };
            view.Set(0f);
            return view;
        }

        public static RectTransform Spacer(Transform parent, float height)
        {
            RectTransform rt = Place(parent, "Отступ", new UiBox(0f, 1f, 1f, 1f, 0f, -height, 0f, 0f));
            LayoutElement el = rt.gameObject.AddComponent<LayoutElement>();
            el.minHeight = height;
            el.preferredHeight = height;
            return rt;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        public static float Luma(Color color)
        {
            return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        }
    }
}
