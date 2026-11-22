using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Procedural uGUI front-end for ORBITA GALAXY. Every screen is built from primitives at runtime:
    /// the game has no external fonts, prefabs or TextMeshPro assets.
    /// </summary>
    public sealed partial class GalaxyUi : MonoBehaviour
    {
        private sealed class FeedLine
        {
            public string Text;
            public Color Color;
            public float Until;
            public Text Label;
        }

        private GalaxyApp _app;
        private RectTransform _safeRoot;
        private RectTransform _screenLayer;
        private RectTransform _hudLayer;
        private RectTransform _toastLayer;
        private RectTransform _feedRoot;
        private RectTransform _evoPanel;
        private RectTransform _pauseOverlay;
        private RectTransform _deathOverlay;
        private Text _deathDetail;
        private string _deathReason = string.Empty;
        private Image _toastPanel;
        private Text _toastLabel;
        private float _toastUntil;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private string _screen = string.Empty;
        private bool _hudBuilt;
        private GameModeKind _selectedMode = GameModeKind.Classic;
        private GameModeKind _lastMode = GameModeKind.Classic;
        private MatchSimulation _hudMatch;
        private string _lastWorldWarningText = string.Empty;

        private Text _hudRank;
        private Text _hudTop;
        private Text _hudMass;
        private Text _hudLevel;
        private Text _hudKills;
        private Text _hudResources;
        private Text _hudTimer;
        private Text _hudRecord;
        private Text _hudWarning;
        private Text _hudBoss;
        private Text _hudEvolution;
        private GBar _barHp;
        private GBar _barShield;
        private GBar _barEnergy;
        private GBar _barBoss;
        private RectTransform _bossPanel;
        private readonly Text[] _abilityLabels = new Text[5];
        private readonly AbilityKind[] _abilityKinds =
        {
            AbilityKind.Dash, AbilityKind.Shield, AbilityKind.Nova, AbilityKind.Magnet, AbilityKind.Overdrive
        };

        private readonly List<FeedLine> _feed = new List<FeedLine>();
        private const int MaxFeedLines = 6;

        // ───────────────────────────── Public API ─────────────────────────────

        public void Initialize(GalaxyApp app)
        {
            if (app == null) return;
            _app = app;
            if (_canvasReady)
            {
                ShowMenu();
                return;
            }
            BuildCanvas();
            GalaxyUiTheme.SetLargeText(app.Profile != null && app.Profile.Settings != null && app.Profile.Settings.LargeText);
            GalaxyUiTheme.SetColorblind(app.Profile != null && app.Profile.Settings != null && app.Profile.Settings.Colorblind);
            GalaxyUiTheme.ReducedMotion = app.Profile != null && app.Profile.Settings != null && app.Profile.Settings.ReducedMotion;
            ShowMenu();
        }

        public void ShowMenu()
        {
            RectTransform root = BeginScreen("menu", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.AccentA);

            GalaxyUiTheme.Panel(root, "Верхняя панель", new Color32(8, 13, 31, 226), new GBox(0f, 1f, 1f, 1f, 0f, -92f, 0f, 0f));
            GalaxyUiTheme.Label(root, GameConfig.GameTitle, 44, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.At(-560f, 404f, 620f, 64f), FontStyle.Bold);
            GalaxyUiTheme.Label(root, "АРЕНА · ЭВОЛЮЦИЯ · ДОМИНИРОВАНИЕ", 20, GalaxyUiTheme.AccentC, TextAnchor.MiddleLeft,
                GBox.At(-560f, 362f, 620f, 34f), FontStyle.Bold);

            AccountProfile profile = _app == null ? null : _app.Profile;
            string account = profile == null
                ? "АККАУНТ —"
                : "УРОВЕНЬ " + profile.Level + "    ◈ " + FormatNumber(profile.Credits) + " КР    ✦ " + profile.Crystals +
                  "    СЕРИЯ " + profile.WinStreak;
            GalaxyUiTheme.Panel(root, "Плашка аккаунта", GalaxyUiTheme.WithAlpha(GalaxyUiTheme.PanelColor, 0.94f),
                GBox.At(430f, 384f, 690f, 74f), GalaxyUiTheme.Round);
            GalaxyUiTheme.Label(root, account, 22, GalaxyUiTheme.AccentA, TextAnchor.MiddleCenter,
                GBox.At(430f, 384f, 660f, 60f), FontStyle.Bold);

            GalaxyUiTheme.Label(root, "ОРБИТА", 106, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.At(-470f, 244f, 780f, 120f), FontStyle.Bold);
            GalaxyUiTheme.Label(root, "ГАЛАКТИКА", 106, GalaxyUiTheme.AccentA, TextAnchor.MiddleLeft,
                GBox.At(-470f, 130f, 780f, 120f), FontStyle.Bold);
            GalaxyUiTheme.Panel(root, "Акцент", GalaxyUiTheme.AccentB, GBox.At(-578f, 62f, 250f, 6f));

            string continueLabel = profile != null && profile.Matches > 0
                ? "ПРОДОЛЖИТЬ · " + ContentDatabase.FindMode(_lastMode).Name
                : "НАЧАТЬ ПЕРВЫЙ МАТЧ";
            GalaxyUiTheme.MakeButton(root, "Продолжить", continueLabel, GalaxyUiTheme.AccentA, GalaxyUiTheme.Ink,
                GBox.At(-470f, -6f, 640f, 88f), 30, delegate { StartSelectedMode(_lastMode); });
            GalaxyUiTheme.MakeButton(root, "Новая игра", "НОВАЯ ИГРА", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(-470f, -108f, 640f, 76f), 26, ShowModeSelect);
            GalaxyUiTheme.MakeButton(root, "Режимы", "ВЫБОР РЕЖИМА", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(-470f, -196f, 640f, 76f), 26, ShowModeSelect);

            GalaxyUiTheme.Label(root, "АНГАР И ПРОГРЕСС", 20, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                GBox.At(455f, 268f, 600f, 36f), FontStyle.Bold);
            GalaxyUiTheme.MakeButton(root, "Профиль", "ПРОФИЛЬ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(300f, 190f, 270f, 76f), 23, ShowProfile);
            GalaxyUiTheme.MakeButton(root, "Магазин", "МАГАЗИН", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(600f, 190f, 270f, 76f), 23, ShowShop);
            GalaxyUiTheme.MakeButton(root, "Коллекция", "КОЛЛЕКЦИЯ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(300f, 100f, 270f, 76f), 23, ShowCollection);
            GalaxyUiTheme.MakeButton(root, "Статистика", "СТАТИСТИКА", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(600f, 100f, 270f, 76f), 23, ShowStats);
            GalaxyUiTheme.MakeButton(root, "Настройки", "НАСТРОЙКИ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(300f, 10f, 270f, 76f), 23, ShowSettings);
            GalaxyUiTheme.MakeButton(root, "Выход", "ВЫХОД", new Color32(58, 24, 48, 255), GalaxyUiTheme.AccentB,
                GBox.At(600f, 10f, 270f, 76f), 23, delegate { Application.Quit(); });

            GalaxyUiTheme.Panel(root, "Нижняя панель", new Color32(8, 13, 31, 210), new GBox(0f, 0f, 1f, 0f, 0f, 0f, 0f, 58f));
            GalaxyUiTheme.Label(root, "WASD — движение · SPACE/SHIFT — ускорение · ЛКМ/E — способность · ПКМ/Q — рывок · F — огонь · ESC — пауза",
                18, GalaxyUiTheme.Muted, TextAnchor.MiddleCenter, new GBox(0f, 0f, 1f, 0f, 40f, 10f, -40f, 50f));
            if (profile != null && profile.Records != null)
                GalaxyUiTheme.Label(root, "Лучший счёт: " + FormatNumber(BestRecord(profile)), 17, GalaxyUiTheme.AccentC,
                    TextAnchor.MiddleRight, GBox.At(600f, -300f, 560f, 30f));
        }

        public void ShowModeSelect()
        {
            RectTransform root = BeginScreen("modes", false);
            if (root == null) return;
            DrawBackdrop(root, GalaxyUiTheme.AccentC);
            GalaxyUiTheme.Label(root, "ВЫБЕРИ РЕЖИМ", 54, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.At(-540f, 388f, 520f, 76f), FontStyle.Bold);
            GalaxyUiTheme.Label(root, "Шесть арен — шесть способов стать первым", 22, GalaxyUiTheme.AccentC,
                TextAnchor.MiddleLeft, GBox.At(-540f, 344f, 620f, 36f));

            float[] xs = { -400f, 400f };
            float[] ys = { 232f, 4f, -224f };
            for (int i = 0; i < ContentDatabase.Modes.Length && i < 6; i++)
            {
                ModeDef mode = ContentDatabase.Modes[i];
                int col = i % 2;
                int row = i / 2;
                bool selected = mode.Kind == _selectedMode;
                float cx = xs[col];
                float cy = ys[row];
                Color accent = ModeColor(mode.Kind);
                Color fill = selected ? GalaxyUiTheme.WithAlpha(accent, 0.24f) : new Color32(16, 23, 48, 244);
                Image card = GalaxyUiTheme.Panel(root, "Режим " + mode.Name, fill, GBox.At(cx, cy, 740f, 202f), GalaxyUiTheme.Round, true);
                GalaxyUiTheme.Panel(card.transform, "Полоса", accent, new GBox(0f, 0f, 0f, 1f, 0f, 0f, 8f, 0f));
                GalaxyUiTheme.Label(card.transform, mode.Name, 30, selected ? accent : GalaxyUiTheme.Paper,
                    TextAnchor.MiddleLeft, GBox.Inset(26f, 142f, 24f, 12f), FontStyle.Bold);
                GalaxyUiTheme.Label(card.transform, mode.Description, 19, GalaxyUiTheme.Muted, TextAnchor.UpperLeft,
                    GBox.Inset(26f, 88f, 24f, 52f));

                string difficulty = DifficultyName(mode.SuggestedDifficulty) + " · опасность x" + mode.DangerScale.ToString("F2");
                string rewards = "Награда x" + mode.RewardScale.ToString("F2") + " · " + mode.Participants + " игроков";
                string extras = (mode.ShrinkingArena ? "СЖАТИЕ " : "") + (mode.Bosses ? "БОССЫ " : "") +
                                (mode.ChaosEvents ? "ХАОС " : "") + "Респавн: " + mode.Respawns;
                GalaxyUiTheme.Label(card.transform, difficulty + "\n" + rewards + "\n" + extras, 17, GalaxyUiTheme.Paper,
                    TextAnchor.LowerLeft, GBox.Inset(26f, 10f, 24f, 108f));

                int record = GetRecord(mode.Kind);
                GalaxyUiTheme.Label(card.transform, record > 0 ? "РЕКОРД\n" + FormatNumber(record) : "РЕКОРД\n—",
                    20, GalaxyUiTheme.AccentA, TextAnchor.MiddleRight, GBox.Inset(520f, 100f, 22f, 10f), FontStyle.Bold);

                GameModeKind captured = mode.Kind;
                Button pick = card.gameObject.AddComponent<Button>();
                pick.targetGraphic = card;
                pick.transition = Selectable.Transition.None;
                pick.onClick.AddListener(delegate { _selectedMode = captured; ShowModeSelect(); });
            }

            GalaxyUiTheme.MakeButton(root, "Назад", "НАЗАД", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(-400f, -398f, 400f, 76f), 25, ShowMenu);
            GalaxyUiTheme.MakeButton(root, "Старт", "СТАРТ · " + ContentDatabase.FindMode(_selectedMode).Name,
                GalaxyUiTheme.AccentA, GalaxyUiTheme.Ink, GBox.At(400f, -398f, 400f, 76f), 27,
                delegate { StartSelectedMode(_selectedMode); });
        }

        public void ShowHud()
        {
            if (_screen == "hud" && _hudBuilt)
            {
                RefreshHud(_app == null ? null : _app.Match);
                return;
            }
            BeginScreen("hud", true);
        }

        public void RefreshHud(MatchSimulation match)
        {
            if (match == null || !_hudBuilt) return;
            _hudMatch = match;

            Ship player = match.Player;
            if (player != null)
            {
                int rank = player.Rank > 0 ? player.Rank : (match.PlayerRank > 0 ? match.PlayerRank : 0);
                SetText(_hudRank, "МЕСТО " + rank + " / " + match.Ships.Count, rank == 1 ? GalaxyUiTheme.AccentA : GalaxyUiTheme.Paper);
                SetText(_hudMass, "МАССА " + player.Mass.ToString("F1"), GalaxyUiTheme.AccentA);
                SetText(_hudLevel, "УРОВЕНЬ " + Mathf.FloorToInt(player.Level), GalaxyUiTheme.Paper);
                string length = player.Body == null ? string.Empty : "   ДЛИНА " + player.Body.Count;
                SetText(_hudKills, "УБИЙСТВА " + player.Kills + "   СЕРИЯ " + player.Streak + length, GalaxyUiTheme.Paper);
                SetText(_hudResources, "ЭНЕРГИЯ " + player.EnergyCollected.ToString("F0") +
                    "   КРИСТАЛЛЫ " + player.Crystals + "   ТМ " + player.DarkMatter + "   АРТЕФАКТЫ " + player.Artifacts,
                    GalaxyUiTheme.Muted);
                _barHp.Set(player.MaxHp > 0f ? player.Hp / player.MaxHp : 0f);
                _barShield.Set(player.MaxShield > 0f ? player.Shield / player.MaxShield : 0f);
                _barEnergy.Set(GameConfig.MaxEnergy > 0f ? player.Energy / GameConfig.MaxEnergy : 0f);
                for (int i = 0; i < _abilityLabels.Length && i < _abilityKinds.Length; i++)
                {
                    AbilityKind kind = _abilityKinds[i];
                    float cd = player.Cooldowns != null && (int)kind < player.Cooldowns.Length ? player.Cooldowns[(int)kind] : 0f;
                    AbilityDef def = ContentDatabase.FindAbility(kind);
                    string cdText = cd > 0.05f ? cd.ToString("F1") : "ГОТОВ";
                    SetText(_abilityLabels[i], def.Name + "\n" + cdText, cd > 0.05f ? GalaxyUiTheme.Muted : GalaxyUiTheme.AccentA);
                }
            }

            SetText(_hudTimer, FormatTime(match.Time), GalaxyUiTheme.Paper);
            SetText(_hudRecord, "РЕКОРД " + FormatNumber(GetRecord(match.Mode)), GalaxyUiTheme.AccentC);
            SetText(_hudTop, BuildTopList(match), GalaxyUiTheme.Paper);

            Ship boss = FindBoss(match);
            if (_bossPanel != null) _bossPanel.gameObject.SetActive(boss != null);
            if (boss != null)
            {
                string bossName = boss.BossDef != null ? boss.BossDef.Name : boss.Name;
                SetText(_hudBoss, bossName + " · ФАЗА " + boss.BossPhase, GalaxyUiTheme.AccentB);
                _barBoss.Set(boss.MaxHp > 0f ? boss.Hp / boss.MaxHp : 0f);
            }

            string worldWarning = string.Empty;
            WorldEventSystem events = match.Events;
            if (events != null)
            {
                if (events.WarningRemaining > 0f)
                    worldWarning = "ВНИМАНИЕ · " + (string.IsNullOrEmpty(events.ActiveTitle) ? "СОБЫТИЕ" : events.ActiveTitle) +
                                   " · " + events.WarningRemaining.ToString("F0") + " с";
                else if (events.ActiveKind.HasValue && !string.IsNullOrEmpty(events.ActiveTitle))
                    worldWarning = events.ActiveTitle + (string.IsNullOrEmpty(events.ActiveDescription) ? string.Empty : " · " + events.ActiveDescription);
                if (!string.IsNullOrEmpty(worldWarning) && worldWarning != _lastWorldWarningText)
                {
                    _lastWorldWarningText = worldWarning;
                    ShowToast(worldWarning);
                }
                if (string.IsNullOrEmpty(worldWarning)) _lastWorldWarningText = string.Empty;
            }

            // Player death outranks disaster banners: the countdown must never be overwritten by
            // a world event while the ship is waiting to return.
            bool respawning = player != null && player.State == ShipState.Respawning;
            if (respawning)
            {
                string countdown = "РЕСПАВН " + Mathf.Max(0f, player.RespawnTimer).ToString("F1") + " с";
                SetWarning(countdown);
                if (_deathOverlay != null)
                {
                    _deathOverlay.gameObject.SetActive(true);
                    string cause = string.IsNullOrEmpty(_deathReason) ? "Причина не установлена" : _deathReason;
                    SetText(_deathDetail, cause + "\n" + countdown, GalaxyUiTheme.Paper);
                }
            }
            else
            {
                SetWarning(worldWarning);
                if (_deathOverlay != null) _deathOverlay.gameObject.SetActive(false);
                _deathReason = string.Empty;
            }

            if (_evoPanel != null)
            {
                bool pending = HasPendingEvolution(match);
                _evoPanel.gameObject.SetActive(pending);
                if (pending) SetText(_hudEvolution, "ЭВОЛЮЦИЯ ДОСТУПНА · ВЫБЕРИ ВЕТВЬ", GalaxyUiTheme.AccentA);
            }
        }

        public void PushFeed(SimEvent ev)
        {
            if (ev == null) return;
            if (_feed.Count >= MaxFeedLines) _feed.RemoveAt(0);
            FeedLine line = new FeedLine();
            line.Text = FormatEvent(ev);
            line.Color = FeedColor(ev.Kind);
            line.Until = Time.unscaledTime + 5.2f;
            _feed.Add(line);
            if (_feedRoot != null) CreateFeedLabel(line);

            MatchSimulation current = _hudMatch != null ? _hudMatch : (_app == null ? null : _app.Match);
            if (ev.Kind == SimEventKind.Kill && current != null && current.Player != null &&
                ev.Value == current.Player.Id)
            {
                _deathReason = ev.Text;
            }
            // Toasts are owned by GalaxyApp.DrainEvents; showing them here as well produced the
            // same warning twice on screen.
        }

        /// <summary>Shows a resume/menu overlay without rebuilding the HUD or clearing its feed.</summary>
        public void ShowPause()
        {
            if (!_canvasReady || _screen != "hud" || _pauseOverlay != null) return;
            RectTransform overlay = GalaxyUiTheme.Panel(_screenLayer, "Пауза",
                GalaxyUiTheme.WithAlpha(GalaxyUiTheme.Ink, 0.88f), GBox.Full).rectTransform;
            _pauseOverlay = overlay;
            GalaxyUiTheme.Label(overlay, "ПАУЗА", 64, GalaxyUiTheme.Paper, TextAnchor.MiddleCenter,
                GBox.At(0f, 210f, 620f, 90f), FontStyle.Bold);
            GalaxyUiTheme.MakeButton(overlay, "Продолжить", "ПРОДОЛЖИТЬ", GalaxyUiTheme.AccentA, GalaxyUiTheme.Ink,
                GBox.At(0f, 40f, 440f, 76f), 26,
                delegate { if (_app != null) _app.TogglePause(false); });
            GalaxyUiTheme.MakeButton(overlay, "В меню", "В МЕНЮ", GalaxyUiTheme.PanelSoft, GalaxyUiTheme.Paper,
                GBox.At(0f, -70f, 440f, 76f), 26,
                delegate { if (_app != null) _app.ReturnToMenu(); });
        }

        public void HidePause()
        {
            if (_pauseOverlay == null) return;
            Destroy(_pauseOverlay.gameObject);
            _pauseOverlay = null;
        }

        public void ShowToast(string text)
        {
            if (string.IsNullOrEmpty(text) || _toastPanel == null || _toastLabel == null) return;
            _toastLabel.text = text;
            _toastPanel.color = GalaxyUiTheme.WithAlpha(GalaxyUiTheme.Ink, 0.92f);
            _toastPanel.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 2.8f;
        }

        // ───────────────────────────── Unity messages ─────────────────────────────

        private void Update()
        {
            if (!_canvasReady) return;
            ApplySafeArea();
            UpdateFeed();
            if (_toastPanel != null && _toastPanel.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toastPanel.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_canvasRoot != null) Destroy(_canvasRoot.gameObject);
        }

        // ───────────────────────────── Canvas ─────────────────────────────

        private bool _canvasReady;
        private RectTransform _canvasRoot;

        private void BuildCanvas()
        {
            GameObject go = new GameObject("GalaxyUI_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            _canvasRoot = go.GetComponent<RectTransform>();

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _safeRoot = GalaxyUiTheme.Place(go.transform, "SafeArea", GBox.Full);
            _screenLayer = GalaxyUiTheme.Place(_safeRoot, "Screens", GBox.Full);
            _hudLayer = GalaxyUiTheme.Place(_safeRoot, "Hud", GBox.Full);
            _toastLayer = GalaxyUiTheme.Place(_safeRoot, "Toasts", GBox.Full);
            _hudLayer.gameObject.SetActive(false);

            _toastPanel = GalaxyUiTheme.Panel(_toastLayer, "Всплывающее уведомление", GalaxyUiTheme.WithAlpha(GalaxyUiTheme.Ink, 0.92f),
                new GBox(0.5f, 1f, 0.5f, 1f, -420f, -150f, 420f, -98f), GalaxyUiTheme.Round);
            _toastLabel = GalaxyUiTheme.Label(_toastPanel.transform, string.Empty, 24, GalaxyUiTheme.Paper,
                TextAnchor.MiddleCenter, GBox.Inset(18f, 4f, 18f, 4f), FontStyle.Bold);
            _toastPanel.gameObject.SetActive(false);

            _canvasReady = true;
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (!_canvasReady || _safeRoot == null) return;
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight) return;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            Rect safe = Screen.safeArea;
            float w = Screen.width > 0 ? (float)Screen.width : 1f;
            float h = Screen.height > 0 ? (float)Screen.height : 1f;
            _safeRoot.anchorMin = new Vector2(Mathf.Clamp01(safe.xMin / w), Mathf.Clamp01(safe.yMin / h));
            _safeRoot.anchorMax = new Vector2(Mathf.Clamp01(safe.xMax / w), Mathf.Clamp01(safe.yMax / h));
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private RectTransform BeginScreen(string id, bool hudVisible)
        {
            if (!_canvasReady) return null;
            _screen = id;
            _hudBuilt = false;
            ClearChildren(_screenLayer);
            _pauseOverlay = null;
            _deathOverlay = null;
            _deathDetail = null;
            _deathReason = string.Empty;
            _lastWorldWarningText = string.Empty;
            _feedRoot = null;
            _feed.Clear();
            if (_hudLayer != null) _hudLayer.gameObject.SetActive(hudVisible);
            if (hudVisible) BuildHud();
            return _screenLayer;
        }

        private static void ClearChildren(RectTransform layer)
        {
            if (layer == null) return;
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                GameObject child = layer.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void DrawBackdrop(RectTransform root, Color accent)
        {
            GalaxyUiTheme.Panel(root, "Фон", GalaxyUiTheme.Deep, GBox.Full);
            GalaxyUiTheme.CircleImage(root, "Орбита 1", GalaxyUiTheme.WithAlpha(accent, 0.055f), GBox.At(560f, 250f, 940f, 940f));
            GalaxyUiTheme.CircleImage(root, "Орбита 2", GalaxyUiTheme.WithAlpha(GalaxyUiTheme.Violet, 0.045f), GBox.At(-660f, -300f, 720f, 720f));
            GalaxyUiTheme.CircleImage(root, "Ядро", GalaxyUiTheme.WithAlpha(accent, 0.035f), GBox.At(390f, -40f, 420f, 420f));
            GalaxyUiTheme.Panel(root, "Верхняя линия", GalaxyUiTheme.WithAlpha(accent, 0.72f),
                new GBox(0f, 1f, 1f, 1f, 0f, -5f, 0f, 0f));
        }

        // ───────────────────────────── HUD ─────────────────────────────

        private void BuildHud()
        {
            ClearChildren(_hudLayer);
            _hudBuilt = true;
            RectTransform hud = _hudLayer;

            Image topLeft = GalaxyUiTheme.Panel(hud, "Место и рейтинг", new Color32(8, 13, 31, 210),
                new GBox(0f, 1f, 0f, 1f, 20f, -196f, 398f, -18f), GalaxyUiTheme.Round);
            _hudRank = GalaxyUiTheme.Label(topLeft.transform, "МЕСТО 1", 26, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 138f, 14f, 8f), FontStyle.Bold);
            _hudTop = GalaxyUiTheme.Label(topLeft.transform, string.Empty, 16, GalaxyUiTheme.Paper, TextAnchor.UpperLeft,
                GBox.Inset(18f, 12f, 14f, 44f));

            Image topRight = GalaxyUiTheme.Panel(hud, "Ресурсы", new Color32(8, 13, 31, 210),
                new GBox(1f, 1f, 1f, 1f, -430f, -160f, -20f, -18f), GalaxyUiTheme.Round);
            _hudMass = GalaxyUiTheme.Label(topRight.transform, "МАССА", 28, GalaxyUiTheme.AccentA, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 104f, 12f, 8f), FontStyle.Bold);
            _hudLevel = GalaxyUiTheme.Label(topRight.transform, "УРОВЕНЬ", 19, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 78f, 12f, 40f));
            _hudKills = GalaxyUiTheme.Label(topRight.transform, "УБИЙСТВА", 17, GalaxyUiTheme.Paper, TextAnchor.MiddleLeft,
                GBox.Inset(18f, 50f, 12f, 68f));
            _hudResources = GalaxyUiTheme.Label(topRight.transform, "", 14, GalaxyUiTheme.Muted, TextAnchor.UpperLeft,
                GBox.Inset(18f, 6f, 12f, 98f));

            Image timerPanel = GalaxyUiTheme.Panel(hud, "Таймер", new Color32(8, 13, 31, 205),
                new GBox(0.5f, 1f, 0.5f, 1f, -130f, -84f, 130f, -20f), GalaxyUiTheme.Round);
            _hudTimer = GalaxyUiTheme.Label(timerPanel.transform, "00:00", 28, GalaxyUiTheme.Paper,
                TextAnchor.MiddleCenter, GBox.Inset(8f, 28f, 8f, 4f), FontStyle.Bold);
            _hudRecord = GalaxyUiTheme.Label(timerPanel.transform, "РЕКОРД", 14, GalaxyUiTheme.AccentC,
                TextAnchor.MiddleCenter, GBox.Inset(8f, 4f, 8f, 34f));
            GalaxyUiTheme.MakeButton(hud, "Пауза", "II", new Color32(18, 25, 51, 220), GalaxyUiTheme.Paper,
                new GBox(0.5f, 1f, 0.5f, 1f, 146f, -84f, 206f, -28f), 20,
                delegate { if (_app != null) _app.TogglePause(true); });

            _bossPanel = GalaxyUiTheme.Panel(hud, "Босс", new Color32(40, 12, 32, 226),
                new GBox(0.5f, 1f, 0.5f, 1f, -270f, -150f, 270f, -94f), GalaxyUiTheme.Round).rectTransform;
            _hudBoss = GalaxyUiTheme.Label(_bossPanel, "БОСС", 18, GalaxyUiTheme.AccentB,
                TextAnchor.MiddleLeft, GBox.Inset(14f, 26f, 14f, 4f), FontStyle.Bold);
            _barBoss = GalaxyUiTheme.Bar(_bossPanel, "Полоса босса", new Color32(16, 16, 34, 255), GalaxyUiTheme.AccentB,
                GBox.Inset(14f, 8f, 14f, 30f));
            _bossPanel.gameObject.SetActive(false);

            Image bars = GalaxyUiTheme.Panel(hud, "Показатели", new Color32(8, 13, 31, 205),
                new GBox(0f, 1f, 0f, 1f, 20f, -354f, 398f, -210f), GalaxyUiTheme.Round);
            AddBar(bars.transform, "HP", 0, GalaxyUiTheme.AccentB, out _barHp);
            AddBar(bars.transform, "ЩИТ", 46, GalaxyUiTheme.AccentC, out _barShield);
            AddBar(bars.transform, "ЭНЕРГИЯ", 92, GalaxyUiTheme.AccentA, out _barEnergy);

            for (int i = 0; i < _abilityKinds.Length; i++)
            {
                float x = 20f + i * 76f;
                AbilityKind kind = _abilityKinds[i];
                AbilityKind captured = kind;
                int slot = i;
                Button btn = GalaxyUiTheme.MakeButton(hud, "Способность " + i, ContentDatabase.FindAbility(kind).Name,
                    new Color32(18, 25, 51, 220), GalaxyUiTheme.Paper,
                    new GBox(0f, 1f, 0f, 1f, x, -412f, x + 70f, -360f), 15,
                    delegate { if (_app != null) _app.SetAbility(captured); });
                _abilityLabels[slot] = btn.GetComponentInChildren<Text>();
            }

            RectTransform feed = GalaxyUiTheme.Place(hud, "Лента событий",
                new GBox(0.5f, 0f, 0.5f, 0f, -250f, 540f, 250f, 700f));
            _feedRoot = feed;
            for (int i = 0; i < _feed.Count; i++) CreateFeedLabel(_feed[i]);
            GalaxyUiTheme.Label(hud, "", 1, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft, GBox.At(0f, 0f, 1f, 1f)).gameObject.SetActive(false);

            Image warning = GalaxyUiTheme.Panel(hud, "Предупреждение", new Color32(56, 22, 40, 226),
                new GBox(0.5f, 1f, 0.5f, 1f, -330f, -196f, 330f, -158f), GalaxyUiTheme.Round);
            _hudWarning = GalaxyUiTheme.Label(warning.transform, string.Empty, 19, GalaxyUiTheme.Sun,
                TextAnchor.MiddleCenter, GBox.Inset(12f, 4f, 12f, 4f), FontStyle.Bold);
            warning.gameObject.SetActive(false);

            Image death = GalaxyUiTheme.Panel(hud, "Смерть", GalaxyUiTheme.WithAlpha(GalaxyUiTheme.Ink, 0.88f),
                GBox.At(0f, 52f, 760f, 210f), GalaxyUiTheme.Round);
            death.raycastTarget = false;
            _deathOverlay = death.rectTransform;
            GalaxyUiTheme.Label(death.transform, "ВЫ УНИЧТОЖЕНЫ", 46, GalaxyUiTheme.Coral,
                TextAnchor.MiddleCenter, GBox.Inset(20f, 150f, 20f, 62f), FontStyle.Bold);
            _deathDetail = GalaxyUiTheme.Label(death.transform, string.Empty, 22, GalaxyUiTheme.Paper,
                TextAnchor.MiddleCenter, GBox.Inset(20f, 72f, 20f, 14f), FontStyle.Bold);
            death.gameObject.SetActive(false);

            _evoPanel = GalaxyUiTheme.Panel(hud, "Подсказка эволюции", GalaxyUiTheme.WithAlpha(GalaxyUiTheme.AccentA, 0.92f),
                new GBox(0.5f, 0f, 0.5f, 0f, -290f, 238f, 290f, 294f), GalaxyUiTheme.Round).rectTransform;
            _hudEvolution = GalaxyUiTheme.Label(_evoPanel, string.Empty, 18, GalaxyUiTheme.Ink,
                TextAnchor.MiddleCenter, GBox.Inset(10f, 4f, 10f, 4f), FontStyle.Bold);
            _evoPanel.gameObject.SetActive(false);
        }

        private static void AddBar(Transform parent, string caption, float y, Color color, out GBar bar)
        {
            GalaxyUiTheme.Label(parent, caption, 14, GalaxyUiTheme.Muted, TextAnchor.MiddleLeft,
                new GBox(0f, 1f, 1f, 1f, 16f, -(y + 32f), -16f, -y));
            bar = GalaxyUiTheme.Bar(parent, "Полоса " + caption, new Color32(30, 38, 66, 255), color,
                new GBox(0f, 1f, 1f, 1f, 16f, -(y + 44f), -16f, -(y + 34f)));
        }

        private void CreateFeedLabel(FeedLine line)
        {
            if (_feedRoot == null || line.Label != null) return;
            int index = _feed.IndexOf(line);
            if (index < 0) index = 0;
            float top = index * 25f;
            Text label = GalaxyUiTheme.Label(_feedRoot, line.Text, 16, line.Color, TextAnchor.MiddleRight,
                new GBox(0f, 1f, 1f, 1f, 0f, -(top + 24f), 0f, -top), FontStyle.Bold);
            line.Label = label;
        }

        private void UpdateFeed()
        {
            if (_feed.Count == 0) return;
            bool changed = false;
            for (int i = _feed.Count - 1; i >= 0; i--)
            {
                FeedLine line = _feed[i];
                float remain = line.Until - Time.unscaledTime;
                if (remain <= 0f)
                {
                    if (line.Label != null) Destroy(line.Label.gameObject);
                    _feed.RemoveAt(i);
                    changed = true;
                    continue;
                }
                if (line.Label != null)
                {
                    Color c = line.Color;
                    c.a = Mathf.Clamp01(remain / 1.2f);
                    line.Label.color = c;
                }
            }
            if (!changed || _feedRoot == null) return;
            for (int i = 0; i < _feed.Count; i++)
            {
                FeedLine line = _feed[i];
                if (line.Label == null) continue;
                float top = i * 25f;
                RectTransform rt = line.Label.rectTransform;
                rt.offsetMin = new Vector2(rt.offsetMin.x, -(top + 24f));
                rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
            }
        }

        private void SetWarning(string text)
        {
            if (_hudWarning == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            if (_hudWarning.transform.parent != null)
            {
                Image panel = _hudWarning.transform.parent.GetComponent<Image>();
                if (panel != null) panel.gameObject.SetActive(visible);
            }
            if (visible) _hudWarning.text = text;
        }

        private static void SetText(Text text, string value, Color color)
        {
            if (text == null) return;
            text.text = value ?? string.Empty;
            text.color = color;
        }

        private string BuildTopList(MatchSimulation match)
        {
            StringBuilder sb = new StringBuilder();
            List<LeaderboardEntry> lb = match.Leaderboard;
            if (lb != null && lb.Count > 0)
            {
                int shown = 0;
                for (int i = 0; i < lb.Count && shown < 10; i++)
                {
                    LeaderboardEntry e = lb[i];
                    if (e == null) continue;
                    int rank = e.Rank > 0 ? e.Rank : shown + 1;
                    string row = rank.ToString("00") + "  " + Clip(e.Name, 12) + "  " + Mathf.RoundToInt(e.Score);
                    if (e.IsPlayer) sb.Append("<color=#B6FF3D>").Append(row).Append("</color>\n");
                    else sb.Append(row).Append('\n');
                    shown++;
                }
            }
            else if (match.Ships != null)
            {
                List<Ship> ships = new List<Ship>(match.Ships);
                ships.Sort(delegate(Ship a, Ship b) { return b.Score.CompareTo(a.Score); });
                for (int i = 0; i < ships.Count && i < 10; i++)
                {
                    Ship s = ships[i];
                    if (s == null) continue;
                    string row = (i + 1).ToString("00") + "  " + Clip(s.Name, 12) + "  " + Mathf.RoundToInt(s.Score);
                    if (s.IsPlayer) sb.Append("<color=#B6FF3D>").Append(row).Append("</color>\n");
                    else sb.Append(row).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static Ship FindBoss(MatchSimulation match)
        {
            if (match == null || match.Ships == null) return null;
            for (int i = 0; i < match.Ships.Count; i++)
            {
                Ship s = match.Ships[i];
                if (s != null && s.IsBoss && s.State == ShipState.Alive && s.Hp > 0f) return s;
            }
            return null;
        }

        private static bool HasPendingEvolution(MatchSimulation match)
        {
            if (match == null) return false;
            FieldInfo field = match.GetType().GetField("PendingEvolutionChoices", BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return false;
            object value = field.GetValue(match);
            ICollection collection = value as ICollection;
            if (collection != null) return collection.Count > 0;
            if (value is int) return (int)value > 0;
            return value != null;
        }

        // ───────────────────────────── Shared helpers ─────────────────────────────

        private void StartSelectedMode(GameModeKind kind)
        {
            _lastMode = kind;
            _selectedMode = kind;
            if (_app != null) _app.StartMatch(kind);
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int m = total / 60;
            int s = total % 60;
            return m.ToString("00") + ":" + s.ToString("00");
        }

        private static string FormatNumber(int value)
        {
            if (Mathf.Abs(value) < 1000) return value.ToString();
            if (Mathf.Abs(value) < 1000000) return (value / 1000f).ToString("F1") + "K";
            return (value / 1000000f).ToString("F2") + "M";
        }

        private static string Clip(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) return "—";
            return value.Length <= max ? value : value.Substring(0, max);
        }

        private static string DifficultyName(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Casual: return "ЛЕГКО";
                case Difficulty.Hard: return "СЛОЖНО";
                case Difficulty.Expert: return "ЭКСПЕРТ";
                default: return "НОРМАЛЬНО";
            }
        }

        private static Color ModeColor(GameModeKind kind)
        {
            switch (kind)
            {
                case GameModeKind.Survival: return GalaxyUiTheme.AccentC;
                case GameModeKind.BossHunt: return GalaxyUiTheme.AccentB;
                case GameModeKind.Chaos: return GalaxyUiTheme.Violet;
                case GameModeKind.Hardcore: return GalaxyUiTheme.Orange;
                case GameModeKind.Infinite: return GalaxyUiTheme.Sun;
                default: return GalaxyUiTheme.AccentA;
            }
        }

        private int GetRecord(GameModeKind kind)
        {
            AccountProfile profile = _app == null ? null : _app.Profile;
            if (profile == null || profile.Records == null) return 0;
            string[] keys =
            {
                kind.ToString(), kind.ToString().ToLowerInvariant(), "mode_" + kind,
                "record_" + kind, "score_" + kind, ContentDatabase.FindMode(kind).Name
            };
            for (int i = 0; i < keys.Length; i++)
            {
                int value;
                if (profile.Records.TryGetValue(keys[i], out value) && value > 0) return value;
            }
            return 0;
        }

        private static int BestRecord(AccountProfile profile)
        {
            int best = 0;
            if (profile == null || profile.Records == null) return 0;
            foreach (KeyValuePair<string, int> pair in profile.Records)
                if (pair.Value > best) best = pair.Value;
            return best;
        }

        private static string FormatEvent(SimEvent ev)
        {
            if (!string.IsNullOrEmpty(ev.Text)) return ev.Text;
            switch (ev.Kind)
            {
                case SimEventKind.Kill: return "УНИЧТОЖЕНИЕ · " + ev.Value;
                case SimEventKind.Absorb: return "ПОГЛОЩЕНИЕ · +" + ev.Value;
                case SimEventKind.LevelUp: return "УРОВЕНЬ " + ev.Value;
                case SimEventKind.Evolution: return "ЭВОЛЮЦИЯ · " + ev.Value;
                case SimEventKind.BossSpawn: return "БОСС ПОЯВИЛСЯ";
                case SimEventKind.LeaderKilled: return "ЛИДЕР УНИЧТОЖЕН";
                case SimEventKind.RankUp: return "МЕСТО " + ev.Value;
                case SimEventKind.Streak: return "СЕРИЯ " + ev.Value;
                case SimEventKind.EventWarning: return "ПРЕДУПРЕЖДЕНИЕ · СОБЫТИЕ";
                case SimEventKind.EventStart: return "СОБЫТИЕ НАЧАЛОСЬ";
                case SimEventKind.EventEnd: return "СОБЫТИЕ ЗАВЕРШЕНО";
                case SimEventKind.Victory: return "ПОБЕДА!";
                case SimEventKind.Defeat: return "ПОРАЖЕНИЕ";
                case SimEventKind.Warning: return "ОПАСНОСТЬ";
                default: return string.Empty;
            }
        }

        private static Color FeedColor(SimEventKind kind)
        {
            switch (kind)
            {
                case SimEventKind.Kill:
                case SimEventKind.LeaderKilled: return GalaxyUiTheme.AccentB;
                case SimEventKind.Absorb: return GalaxyUiTheme.Violet;
                case SimEventKind.LevelUp:
                case SimEventKind.Evolution:
                case SimEventKind.Victory: return GalaxyUiTheme.AccentA;
                case SimEventKind.BossSpawn: return GalaxyUiTheme.Orange;
                case SimEventKind.Warning:
                case SimEventKind.EventWarning: return GalaxyUiTheme.Sun;
                case SimEventKind.Defeat: return GalaxyUiTheme.Coral;
                default: return GalaxyUiTheme.Muted;
            }
        }
    }
}
