using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Управление без внешних ассетов: виртуальный джойстик слева, кнопки способности/рывка/ускорения
    /// справа, правый тач-пад для прицеливания и полноценная клавиатурно-мышиная альтернатива.
    /// Все элементы живут в углах безопасной области и не перекрывают центр арены.
    /// </summary>
    public sealed class GalaxyInput : MonoBehaviour
    {
        private const float StickSize = 210f;
        private const float PadSize = 300f;
        private const float ButtonSize = 128f;

        private GameSettings _settings;
        private string _layout = "LEFT";
        private string _layoutToken = GameSettings.ControlTouch;
        private bool _touchEnabled = true;
        private bool _built;
        private Canvas _canvas;
        private RectTransform _safeRoot;
        private GameObject _controls;
        private VirtualStick _stick;
        private VirtualStick _aimPad;
        private HoldButton _boost;
        private TapButton _ability;
        private TapButton _dash;
        private TapButton _fire;
        private Vec2 _move;
        private Vec2 _aim;
        private bool _boostHeld;
        private bool _abilityPressed;
        private bool _dashPressed;
        private bool _firePressed;
        private bool _visible;
        private Vector2Int _screenSize = new Vector2Int(-1, -1);
        private Vector2 _lastMouse;

        public Vec2 Move { get { return _move; } }
        public Vec2 Aim { get { return _aim; } }
        public bool BoostHeld { get { return _boostHeld; } }
        public bool AbilityPressed { get { return _abilityPressed; } }
        public bool DashPressed { get { return _dashPressed; } }
        public bool FirePressed { get { return _firePressed; } }

        public void Configure(GameSettings settings)
        {
            _settings = settings;
            string raw = settings == null || string.IsNullOrEmpty(settings.ControlLayout)
                ? GameSettings.ControlTouch : settings.ControlLayout.ToUpperInvariant();

            // Все шесть токенов ядра: TOUCH — левый джойстик, LEFT/RIGHT — зеркальные раскладки,
            // ALT — приподнятый джойстик и вертикальные кнопки, GAMEPAD — раскладка ALT плюс геймпад,
            // KEYBOARD — только клавиатура и мышь, сенсорный слой скрыт.
            if (raw != GameSettings.ControlLeft && raw != GameSettings.ControlRight
                && raw != GameSettings.ControlAlt && raw != GameSettings.ControlGamepad
                && raw != GameSettings.ControlKeyboard && raw != GameSettings.ControlTouch)
                raw = GameSettings.ControlTouch;

            string layout = raw;
            if (layout == GameSettings.ControlTouch) layout = GameSettings.ControlLeft;
            if (layout == GameSettings.ControlGamepad) layout = GameSettings.ControlAlt;
            _touchEnabled = raw != GameSettings.ControlKeyboard;
            _layoutToken = raw;
            if (_built && layout == _layout)
            {
                SetVisible(_visible);
                return;
            }
            _layout = layout;
            Build();
        }

        public void Consume()
        {
            _abilityPressed = false;
            _dashPressed = false;
            _firePressed = false;
            if (_ability != null) _ability.Consume();
            if (_dash != null) _dash.Consume();
            if (_fire != null) _fire.Consume();
        }

        /// <summary>Сенсорные элементы показываются только во время активного матча.</summary>
        public void SetVisible(bool value)
        {
            _visible = value && _touchEnabled;
            if (_canvas != null) _canvas.gameObject.SetActive(_visible);
        }

        private void Update()
        {
            if (!_built)
            {
                Configure(_settings);
                if (!_built) return;
            }
            if (_screenSize.x != Screen.width || _screenSize.y != Screen.height)
            {
                _screenSize = new Vector2Int(Screen.width, Screen.height);
                ApplySafeArea();
            }

            Vec2 keyboardMove = ReadKeyboardMove();
            _move = keyboardMove.SqrMagnitude > 0.0001f
                ? keyboardMove
                : (_stick == null ? Vec2.Zero : _stick.Value);

            Vec2 touchAim = _aimPad == null ? Vec2.Zero : _aimPad.Value;
            if (touchAim.SqrMagnitude > 0.0016f)
            {
                _aim = touchAim.Normalized;
            }
            else
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                Vector3 mouse = UnityEngine.Input.mousePosition;
                if (!overUi && (mouse.x != _lastMouse.x || mouse.y != _lastMouse.y))
                {
                    _lastMouse = new Vector2(mouse.x, mouse.y);
                    Vector2 delta = new Vector2(mouse.x - Screen.width * 0.5f, mouse.y - Screen.height * 0.5f);
                    if (delta.sqrMagnitude > 900f) _aim = new Vec2(delta.x, delta.y).Normalized;
                }
            }

            _boostHeld = (_boost != null && _boost.IsHeld)
                || Key(UnityEngine.KeyCode.Space)
                || Key(UnityEngine.KeyCode.LeftShift)
                || Key(UnityEngine.KeyCode.RightShift);

            bool overInterface = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overInterface)
            {
                if (MouseDown(0) || KeyDown(UnityEngine.KeyCode.E) || KeyDown(UnityEngine.KeyCode.LeftControl))
                    _abilityPressed = true;
                if (MouseDown(1) || KeyDown(UnityEngine.KeyCode.Q) || KeyDown(UnityEngine.KeyCode.RightControl))
                    _dashPressed = true;
                if (KeyDown(UnityEngine.KeyCode.F)) _firePressed = true;
            }
            if (_ability != null && _ability.WasPressed) _abilityPressed = true;
            if (_dash != null && _dash.WasPressed) _dashPressed = true;
            if (_fire != null && _fire.WasPressed) _firePressed = true;

            if (_layoutToken == GameSettings.ControlGamepad) ApplyGamepad();
        }

        /// <summary>Дополнительный опрос геймпада через стандартные оси Unity без внешних ассетов.</summary>
        private void ApplyGamepad()
        {
            float x = UnityEngine.Input.GetAxisRaw("Horizontal");
            float y = UnityEngine.Input.GetAxisRaw("Vertical");
            if (x * x + y * y > 0.0225f) _move = new Vec2(x, y).Normalized;

            if (KeyDown(UnityEngine.KeyCode.JoystickButton0)) _abilityPressed = true;
            if (KeyDown(UnityEngine.KeyCode.JoystickButton1)) _dashPressed = true;
            if (KeyDown(UnityEngine.KeyCode.JoystickButton2)) _firePressed = true;
            _boostHeld = _boostHeld
                || Key(UnityEngine.KeyCode.JoystickButton5)
                || Key(UnityEngine.KeyCode.JoystickButton6);
        }

        private static bool Key(UnityEngine.KeyCode code)
        {
            return UnityEngine.Input.GetKey(code);
        }

        private static bool KeyDown(UnityEngine.KeyCode code)
        {
            return UnityEngine.Input.GetKeyDown(code);
        }

        private static bool MouseDown(int button)
        {
            return UnityEngine.Input.GetMouseButtonDown(button);
        }

        private static Vec2 ReadKeyboardMove()
        {
            float x = 0f;
            float y = 0f;
            if (Key(UnityEngine.KeyCode.A) || Key(UnityEngine.KeyCode.LeftArrow)) x -= 1f;
            if (Key(UnityEngine.KeyCode.D) || Key(UnityEngine.KeyCode.RightArrow)) x += 1f;
            if (Key(UnityEngine.KeyCode.S) || Key(UnityEngine.KeyCode.DownArrow)) y -= 1f;
            if (Key(UnityEngine.KeyCode.W) || Key(UnityEngine.KeyCode.UpArrow)) y += 1f;
            if (x == 0f && y == 0f) return Vec2.Zero;
            return new Vec2(x, y).Normalized;
        }

        private void Build()
        {
            EnsureCanvas();
            if (_controls != null) Destroy(_controls);
            _controls = new GameObject("TouchControls", typeof(RectTransform));
            _controls.transform.SetParent(_safeRoot, false);
            RectTransform root = _controls.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            bool mirrored = _layout == "RIGHT";
            Vector2 stickAnchor = mirrored ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            Vector2 stickOffset = mirrored ? new Vector2(-52f, 46f) : new Vector2(52f, 46f);
            if (_layout == "ALT")
            {
                stickAnchor = new Vector2(0f, 0f);
                stickOffset = new Vector2(52f, 92f);
            }

            _stick = MakeStick("Джойстик", StickSize, true, 0.34f);
            Place(_stick.GetComponent<RectTransform>(), stickAnchor, stickOffset, new Vector2(StickSize, StickSize));

            _aimPad = MakeStick("Прицел", PadSize, false, 0.045f);
            Place(_aimPad.GetComponent<RectTransform>(), new Vector2(mirrored ? 0f : 1f, 0.52f),
                new Vector2(mirrored ? 28f : -28f, 0f), new Vector2(PadSize, PadSize));

            Vector2 buttonAnchor = mirrored ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            float side = mirrored ? 1f : -1f;
            _boost = MakeHold("Ускорение", "БУСТ", TextureFactory.Lime);
            _dash = MakeTap("Рывок", "РЫВОК", TextureFactory.Cyan);
            _ability = MakeTap("Способность", "СПОСОБНОСТЬ", TextureFactory.Violet);
            _fire = MakeTap("Огонь", "ОГОНЬ", TextureFactory.Coral);
            Place(_fire.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 48f), Vector2.one * ButtonSize);

            if (_layout == "ALT")
            {
                Place(_boost.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 54f, 54f), Vector2.one * ButtonSize);
                Place(_dash.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 54f, 204f), Vector2.one * ButtonSize);
                Place(_ability.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 54f, 354f), Vector2.one * ButtonSize);
            }
            else
            {
                Place(_boost.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 50f, 48f), Vector2.one * ButtonSize);
                Place(_dash.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 50f, 196f), Vector2.one * ButtonSize);
                Place(_ability.GetComponent<RectTransform>(), buttonAnchor, new Vector2(side * 186f, 122f), Vector2.one * ButtonSize);
            }

            _built = true;
            ApplySafeArea();
            if (_canvas != null) _canvas.gameObject.SetActive(_visible);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            GameObject go = new GameObject("GalaxyControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 1f;

            RectTransform canvasRect = go.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(canvasRect, false);
            _safeRoot = safe.GetComponent<RectTransform>();
        }

        private void ApplySafeArea()
        {
            if (_safeRoot == null) return;
            Rect area = Screen.safeArea;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            _safeRoot.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / width), Mathf.Clamp01(area.yMin / height));
            _safeRoot.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / width), Mathf.Clamp01(area.yMax / height));
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private VirtualStick MakeStick(string name, float size, bool showKnob, float alpha)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_controls.transform, false);
            Image image = go.AddComponent<Image>();
            image.sprite = TextureFactory.UiCircle();
            image.color = new Color(0.45f, 0.85f, 1f, alpha);
            image.raycastTarget = true;
            VirtualStick stick = go.AddComponent<VirtualStick>();
            stick.Init(showKnob);
            return stick;
        }

        private HoldButton MakeHold(string name, string label, Color color)
        {
            GameObject go = MakeButtonObject(name, label, color);
            return go.AddComponent<HoldButton>();
        }

        private TapButton MakeTap(string name, string label, Color color)
        {
            GameObject go = MakeButtonObject(name, label, color);
            return go.AddComponent<TapButton>();
        }

        private GameObject MakeButtonObject(string name, string label, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_controls.transform, false);
            Image image = go.AddComponent<Image>();
            image.sprite = TextureFactory.UiCircle();
            image.color = new Color(color.r, color.g, color.b, 0.62f);
            image.raycastTarget = true;

            GameObject textGo = new GameObject("Подпись", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 4f);
            textRect.offsetMax = new Vector2(-6f, -4f);
            Text text = textGo.AddComponent<Text>();
            Font font = TextureFactory.UiFont;
            if (font != null) text.font = font;
            else text.enabled = false;
            text.text = label;
            text.fontSize = 19;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = TextureFactory.Ink;
            text.raycastTarget = false;
            return go;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            float dx = anchor.x == 0f ? size.x * 0.5f : (anchor.x == 1f ? -size.x * 0.5f : 0f);
            float dy = anchor.y == 0f ? size.y * 0.5f : (anchor.y == 1f ? -size.y * 0.5f : 0f);
            rect.anchoredPosition = offset + new Vector2(dx, dy);
        }

        public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            private RectTransform _rect;
            private RectTransform _knob;
            private bool _showKnob;

            public Vec2 Value { get; private set; }

            public void Init(bool showKnob)
            {
                _rect = GetComponent<RectTransform>();
                _showKnob = showKnob;
                if (!_showKnob) return;
                GameObject knobGo = new GameObject("Ручка", typeof(RectTransform));
                knobGo.transform.SetParent(_rect, false);
                _knob = knobGo.GetComponent<RectTransform>();
                _knob.anchorMin = new Vector2(0.5f, 0.5f);
                _knob.anchorMax = new Vector2(0.5f, 0.5f);
                _knob.sizeDelta = Vector2.one * 88f;
                Image image = knobGo.AddComponent<Image>();
                image.sprite = TextureFactory.UiCircle();
                image.color = new Color(1f, 1f, 1f, 0.55f);
                image.raycastTarget = false;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                Track(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                Track(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                Value = Vec2.Zero;
                if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            }

            private void Track(PointerEventData eventData)
            {
                if (_rect == null) _rect = GetComponent<RectTransform>();
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out local))
                    return;
                float radius = Mathf.Max(1f, _rect.rect.width * 0.5f);
                Vector2 direction = local / radius;
                if (direction.sqrMagnitude > 1f) direction = direction.normalized;
                Value = new Vec2(direction.x, direction.y);
                if (_knob != null) _knob.anchoredPosition = direction * radius * 0.55f;
            }
        }

        public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public bool IsHeld { get; private set; }
            public void OnPointerDown(PointerEventData eventData) { IsHeld = true; }
            public void OnPointerUp(PointerEventData eventData) { IsHeld = false; }
            public void OnPointerExit(PointerEventData eventData) { IsHeld = false; }
        }

        public sealed class TapButton : MonoBehaviour, IPointerDownHandler
        {
            public bool WasPressed { get; private set; }
            public void OnPointerDown(PointerEventData eventData) { WasPressed = true; }
            public void Consume() { WasPressed = false; }
        }
    }
}
