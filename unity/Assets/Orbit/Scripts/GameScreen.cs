using System.Globalization;
using Orbit.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>Игровой HUD, модалки перков/паузы и жестовое управление.</summary>
    public sealed class GameScreen : OrbitScreen
    {
        private Text _scoreText;
        private Text _recordText;
        private Text _objectiveText;
        private Text _timerText;
        private Text _comboText;
        private Text _linesText;
        private Text _levelText;
        private Text _warningText;
        private Text _muteLabel;
        private Image _flash;

        private GameObject _field;
        private GameObject _stackBar;
        private GameObject _hint;
        private GameObject _draftPanel;
        private GameObject _pausePanel;
        private Button[] _perkButtons;
        private Text[] _perkTitles;
        private Text[] _perkDetails;
        private Text[] _perkRisks;

        private StackBoardView _stack;
        private OrbitArenaView _orbit;

        private float _impactFlash;
        private bool _pointerActive;
        private Vector2 _pointerStart;
        private Vector2 _pointerPrev;

        private void Awake()
        {
            Build();
        }

        public override void Bind(OrbitApp app)
        {
            base.Bind(app);
            if (_orbit != null && _field != null) _orbit.Initialize(app, _field.transform);
            if (_stack != null && _field != null) _stack.Initialize(app, _field.transform);
            RefreshMute();
        }

        private void Build()
        {
            UiFactory.Panel(transform, "Фон", UiFactory.Ink, UiBox.Full);
            UiFactory.Panel(transform, "Верх", UiFactory.WithAlpha(UiFactory.Deep, 0.94f),
                new UiBox(0f, 0.875f, 1f, 1f, 0f, 0f, 0f, 0f));

            _scoreText = UiFactory.TextLabel(transform, "0", 74, UiFactory.Lime, TextAnchor.MiddleLeft,
                new UiBox(0.03f, 0.935f, 0.56f, 0.995f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.FitText(_scoreText, 36, 80);
            _recordText = UiFactory.Chip(transform, "Рекорд", "РЕКОРД 0", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.57f, 0.95f, 0.73f, 0.99f, 0f, 0f, 0f, 0f), 20).Label;

            UiFactory.MakeButton(transform, "Пауза", "II", UiFactory.Violet, UiFactory.Ink,
                new UiBox(0.745f, 0.94f, 0.825f, 0.995f, 0f, 0f, 0f, 0f), 30,
                delegate { if (App != null) App.TogglePause(); });
            Button mute = UiFactory.MakeButton(transform, "Звук", "♪", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.83f, 0.94f, 0.91f, 0.995f, 0f, 0f, 0f, 0f), 30,
                delegate { if (App != null) App.ToggleMute(); });
            _muteLabel = mute.GetComponentInChildren<Text>();
            UiFactory.MakeButton(transform, "Домой", "⌂", UiFactory.Coral, UiFactory.Ink,
                new UiBox(0.915f, 0.94f, 0.99f, 0.995f, 0f, 0f, 0f, 0f), 30,
                delegate { if (App != null) App.Finish(); });

            _objectiveText = UiFactory.Chip(transform, "Цель", "ЦЕЛЬ", UiFactory.Sun, UiFactory.Ink,
                new UiBox(0.03f, 0.883f, 0.35f, 0.935f, 0f, 0f, 0f, 0f), 21).Label;
            _timerText = UiFactory.Chip(transform, "Таймер", "0 С", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.36f, 0.883f, 0.51f, 0.935f, 0f, 0f, 0f, 0f), 21).Label;
            _comboText = UiFactory.Chip(transform, "Комбо", "КОМБО", UiFactory.Coral, UiFactory.Ink,
                new UiBox(0.52f, 0.883f, 0.67f, 0.935f, 0f, 0f, 0f, 0f), 21).Label;
            _linesText = UiFactory.Chip(transform, "Линии", "ЛИНИИ 0", UiFactory.Slate, UiFactory.Paper,
                new UiBox(0.68f, 0.883f, 0.83f, 0.935f, 0f, 0f, 0f, 0f), 19).Label;
            _levelText = UiFactory.Chip(transform, "Уровень", "УР 1", UiFactory.Violet, UiFactory.Ink,
                new UiBox(0.84f, 0.883f, 0.99f, 0.935f, 0f, 0f, 0f, 0f), 19).Label;

            RectTransform fieldRect = UiFactory.Place(transform, "Поле", new UiBox(0.02f, 0.16f, 0.98f, 0.872f, 0f, 0f, 0f, 0f));
            _field = fieldRect.gameObject;
            _orbit = fieldRect.gameObject.AddComponent<OrbitArenaView>();
            _stack = fieldRect.gameObject.AddComponent<StackBoardView>();
            _orbit.Initialize(App, fieldRect);
            _stack.Initialize(App, fieldRect);
            _flash = UiFactory.Panel(fieldRect, "Вспышка", new Color(1f, 1f, 1f, 0f), UiBox.Full);

            _warningText = UiFactory.TextLabel(transform, "", 30, UiFactory.Coral, TextAnchor.MiddleCenter,
                new UiBox(0.15f, 0.825f, 0.85f, 0.87f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            _warningText.gameObject.SetActive(false);

            BuildStackBar();
            _hint = UiFactory.TextLabel(transform, "НАЖМИ ИЛИ ТЯНИ, ЧТОБЫ СМЕНИТЬ НАПРАВЛЕНИЕ", 22,
                UiFactory.WithAlpha(UiFactory.Paper, 0.7f), TextAnchor.MiddleCenter,
                new UiBox(0.04f, 0.06f, 0.96f, 0.12f, 0f, 0f, 0f, 0f)).gameObject;

            BuildDraftPanel();
            BuildPausePanel();
        }

        private void BuildStackBar()
        {
            RectTransform bar = UiFactory.Place(transform, "Панель управления",
                new UiBox(0.02f, 0.035f, 0.98f, 0.15f, 0f, 0f, 0f, 0f));
            _stackBar = bar.gameObject;
            UiFactory.Panel(bar, "Фон", UiFactory.WithAlpha(UiFactory.Slate, 0.92f), UiBox.Full, UiFactory.RoundedSprite).type = Image.Type.Sliced;

            string[] labels = { "◀", "⟳", "▶", "▼", "⇣", "HOLD" };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                float x0 = 0.012f + i * 0.164f;
                Color fill = i == 5 ? UiFactory.Violet : UiFactory.Paper;
                Color ink = i == 5 ? UiFactory.Ink : UiFactory.Deep;
                UiFactory.MakeButton(bar, "Кнопка " + labels[i], labels[i], fill, ink,
                    new UiBox(x0, 0.08f, x0 + 0.152f, 0.92f, 0f, 0f, 0f, 0f), 42,
                    delegate
                    {
                        if (App == null || App.Engine == null) return;
                        switch (index)
                        {
                            case 0: App.Engine.MoveBlock(-1); break;
                            case 1: App.Engine.RotateBlock(); break;
                            case 2: App.Engine.MoveBlock(1); break;
                            case 3: App.Engine.SoftDropBlock(); break;
                            case 4: App.Engine.HardDropBlock(); break;
                            case 5: App.Engine.HoldBlock(); break;
                        }
                    });
            }
        }

        private void BuildDraftPanel()
        {
            RectTransform dim = UiFactory.Place(transform, "Выбор перка", UiBox.Full);
            _draftPanel = dim.gameObject;
            UiFactory.Panel(dim, "Затемнение", new Color(0f, 0f, 0f, 0.82f), UiBox.Full, null, true);
            UiFactory.Panel(dim, "Карточка", UiFactory.WithAlpha(UiFactory.Slate, 0.98f),
                new UiBox(0.06f, 0.24f, 0.94f, 0.77f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(dim, "ВЫБЕРИ ПЕРК", 46, UiFactory.Lime, TextAnchor.MiddleCenter,
                new UiBox(0.08f, 0.695f, 0.92f, 0.762f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.TextLabel(dim, "ОДИН РАЗ ЗА КРУГ · ВЫБОР ВЛИЯЕТ НА ЗАБЕГ", 19, UiFactory.WithAlpha(UiFactory.Paper, 0.7f),
                TextAnchor.MiddleCenter, new UiBox(0.08f, 0.655f, 0.92f, 0.7f, 0f, 0f, 0f, 0f));

            _perkButtons = new Button[3];
            _perkTitles = new Text[3];
            _perkDetails = new Text[3];
            _perkRisks = new Text[3];
            float[] tops = { 0.635f, 0.515f, 0.395f };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                float top = tops[i];
                UiBox box = new UiBox(0.1f, top - 0.105f, 0.9f, top, 0f, 0f, 0f, 0f);
                Button button = UiFactory.MakeButton(dim, "Перк " + i, string.Empty, UiFactory.Deep, UiFactory.Paper,
                    box, 20, delegate { if (App != null) App.ChoosePerk(index); });
                _perkButtons[i] = button;
                _perkTitles[i] = UiFactory.TextLabel(button.transform, string.Empty, 30, UiFactory.Lime, TextAnchor.MiddleLeft,
                    new UiBox(0.05f, 0.56f, 0.95f, 0.96f, 0f, 0f, 0f, 0f), FontStyle.Bold);
                _perkDetails[i] = UiFactory.TextLabel(button.transform, string.Empty, 22, UiFactory.Paper, TextAnchor.MiddleLeft,
                    new UiBox(0.05f, 0.2f, 0.95f, 0.58f, 0f, 0f, 0f, 0f));
                _perkRisks[i] = UiFactory.TextLabel(button.transform, string.Empty, 17, UiFactory.Coral, TextAnchor.MiddleLeft,
                    new UiBox(0.05f, 0.02f, 0.95f, 0.22f, 0f, 0f, 0f, 0f));
            }
            _draftPanel.SetActive(false);
        }

        private void BuildPausePanel()
        {
            RectTransform dim = UiFactory.Place(transform, "Пауза", UiBox.Full);
            _pausePanel = dim.gameObject;
            UiFactory.Panel(dim, "Затемнение", new Color(0f, 0f, 0f, 0.78f), UiBox.Full, null, true);
            UiFactory.Panel(dim, "Карточка", UiFactory.WithAlpha(UiFactory.Slate, 0.98f),
                new UiBox(0.14f, 0.34f, 0.86f, 0.68f, 0f, 0f, 0f, 0f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(dim, "ПАУЗА", 62, UiFactory.Lime, TextAnchor.MiddleCenter,
                new UiBox(0.16f, 0.55f, 0.84f, 0.66f, 0f, 0f, 0f, 0f), FontStyle.Bold);
            UiFactory.MakeButton(dim, "Продолжить", "ПРОДОЛЖИТЬ", UiFactory.Lime, UiFactory.Ink,
                new UiBox(0.2f, 0.45f, 0.8f, 0.54f, 0f, 0f, 0f, 0f), 30,
                delegate { if (App != null) App.TogglePause(); });
            UiFactory.MakeButton(dim, "Завершить", "ЗАВЕРШИТЬ ЗАБЕГ", UiFactory.Coral, UiFactory.Ink,
                new UiBox(0.2f, 0.37f, 0.8f, 0.44f, 0f, 0f, 0f, 0f), 26,
                delegate { if (App != null) { App.TogglePause(); App.Finish(); } });
            _pausePanel.SetActive(false);
        }

        public override void Show()
        {
            base.Show();
            Tick();
        }

        public override void Refresh()
        {
            Tick();
        }

        public void SetPaused(bool paused)
        {
            if (_pausePanel != null) _pausePanel.SetActive(paused);
        }

        public void RefreshMute()
        {
            if (_muteLabel == null || App == null || App.Save == null || App.Save.settings == null) return;
            _muteLabel.text = App.Save.settings.muted ? "×" : "♪";
            _muteLabel.color = App.Save.settings.muted ? UiFactory.Coral : UiFactory.Paper;
        }

        public void OnEvent(GameEvent ev)
        {
            switch (ev.Kind)
            {
                case GameEventKind.Crash:
                case GameEventKind.BlockTopOut:
                case GameEventKind.ShieldBreak:
                    _impactFlash = 1f;
                    break;
                case GameEventKind.BlockClear:
                case GameEventKind.Resonance:
                case GameEventKind.LevelUp:
                    _impactFlash = Mathf.Max(_impactFlash, 0.65f);
                    break;
                default:
                    break;
            }
        }

        public override void Tick()
        {
            if (App == null || App.Engine == null) return;
            GameEngineCore e = App.Engine;

            _scoreText.text = e.Score.ToString("N0", CultureInfo.InvariantCulture);
            _recordText.text = "РЕКОРД " + e.Best.ToString("N0", CultureInfo.InvariantCulture);
            ObjectiveDef objective = e.Objective();
            _objectiveText.text = objective.Title + " " + e.ObjectiveValue() + "/" + objective.Target;
            _timerText.text = e.TimeLeft > 0 ? "⏱ " + e.TimeLeft + " С" : "⏱ " + Mathf.FloorToInt(e.Elapsed) + " С";
            _comboText.text = "КОМБО x" + e.Combo + (e.Multiplier > 1 ? " · x" + e.Multiplier : string.Empty);
            _linesText.text = "ЛИНИИ " + e.Blocks.Lines;
            _levelText.text = "УР " + e.Level;

            bool stack = e.Mode == ModeKind.Stack;
            if (_stackBar != null) _stackBar.SetActive(stack);
            if (_hint != null) _hint.SetActive(!stack);
            if (_stack != null && _stack.gameObject.activeSelf != stack) _stack.gameObject.SetActive(stack);
            if (_orbit != null && _orbit.gameObject.activeSelf == stack) _orbit.gameObject.SetActive(!stack);

            float alpha = Mathf.Clamp01(_impactFlash * 0.55f + e.ResonanceFlash * 0.16f +
                                        e.LevelUpFlash * 0.16f + e.Blocks.ClearFlash * 0.2f);
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, alpha);
            _impactFlash = Mathf.Max(0f, _impactFlash - Time.deltaTime * 2.4f);

            if (_warningText != null)
            {
                string warning = string.Empty;
                if (e.StormWarning > 0f) warning = "⚠ ШТОРМ!";
                else if (e.FlareWarning > 0f) warning = "☀ СОЛНЕЧНАЯ ВСПЫШКА!";
                else if (e.Shield) warning = "◈ ЩИТ АКТИВЕН";
                _warningText.text = warning;
                if (_warningText.gameObject.activeSelf != !string.IsNullOrEmpty(warning))
                    _warningText.gameObject.SetActive(!string.IsNullOrEmpty(warning));
            }

            bool draft = e.Draft != null && _perkButtons != null;
            if (_draftPanel != null && _draftPanel.activeSelf != draft) _draftPanel.SetActive(draft);
            if (draft)
            {
                for (int i = 0; i < 3; i++)
                {
                    int option = e.Draft.Options[i];
                    PerkDef def = option >= 0 && option < Content.Perks.Length ? Content.Perks[option] : null;
                    if (def == null) continue;
                    _perkTitles[i].text = def.Glyph + "  " + def.Title;
                    _perkDetails[i].text = def.Detail;
                    _perkRisks[i].text = "РИСК: " + def.Risk;
                }
            }

            if (_pausePanel != null && _pausePanel.activeSelf != e.Paused) _pausePanel.SetActive(e.Paused);
            HandlePointer();
        }

        private static bool PointerOverUi()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.touches[0].fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }

        private static bool PointerDown(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) { position = touch.position; return true; }
            }
            if (Input.GetMouseButtonDown(0)) { position = Input.mousePosition; return true; }
            position = Vector2.zero;
            return false;
        }

        private static bool PointerHeld(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    position = touch.position;
                    return true;
                }
            }
            if (Input.GetMouseButton(0)) { position = Input.mousePosition; return true; }
            position = Vector2.zero;
            return false;
        }

        private static bool PointerUp(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    position = touch.position;
                    return true;
                }
            }
            if (Input.GetMouseButtonUp(0)) { position = Input.mousePosition; return true; }
            position = Vector2.zero;
            return false;
        }

        private bool InsideField(Vector2 position)
        {
            if (_field == null) return false;
            RectTransform fieldRect = _field.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            fieldRect.GetWorldCorners(corners);
            return position.x >= corners[0].x && position.x <= corners[2].x &&
                   position.y >= corners[0].y && position.y <= corners[1].y;
        }

        private bool BottomTap(Vector2 position)
        {
            if (_field == null) return false;
            RectTransform fieldRect = _field.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            fieldRect.GetWorldCorners(corners);
            float bottom = corners[0].y;
            float top = corners[1].y;
            return position.y < bottom + (top - bottom) * 0.26f;
        }

        private void HandlePointer()
        {
            GameEngineCore e = App.Engine;
            if (e == null || !e.CanInput) { _pointerActive = false; return; }
            if (PointerOverUi()) return;

            Vector2 p;
            if (PointerDown(out p))
            {
                if (!InsideField(p)) return;
                _pointerActive = true;
                _pointerStart = p;
                _pointerPrev = p;
                if (e.Mode != ModeKind.Stack) e.Tap();
                return;
            }

            if (e.Mode == ModeKind.Stack)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow)) e.MoveBlock(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow)) e.MoveBlock(1);
                if (Input.GetKeyDown(KeyCode.UpArrow)) e.RotateBlock();
                if (Input.GetKeyDown(KeyCode.DownArrow)) e.SoftDropBlock();
                if (Input.GetKeyDown(KeyCode.C)) e.HoldBlock();
                if (Input.GetKeyDown(KeyCode.Space)) e.HardDropBlock();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                e.Tap();
            }

            if (!_pointerActive) return;

            if (e.Mode == ModeKind.Stack && PointerHeld(out p))
            {
                Vector2 delta = p - _pointerPrev;
                if (Mathf.Abs(delta.x) >= 20f)
                {
                    e.MoveBlock(delta.x > 0f ? 1 : -1);
                    _pointerPrev = p;
                }
                else if (delta.y <= -26f)
                {
                    e.SoftDropBlock();
                    _pointerPrev = p;
                }
                else if (delta.y >= 44f)
                {
                    e.RotateBlock();
                    _pointerPrev = p;
                }
            }

            if (PointerUp(out p))
            {
                if (e.Mode == ModeKind.Stack && Mathf.Abs(p.x - _pointerStart.x) < 20f && Mathf.Abs(p.y - _pointerStart.y) < 26f)
                {
                    if (BottomTap(p)) e.HardDropBlock();
                    else e.RotateBlock();
                }
                _pointerActive = false;
            }
        }
    }
}
