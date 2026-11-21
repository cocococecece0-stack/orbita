using System.Collections.Generic;
using Orbit.Core;
using UnityEngine;

namespace Orbit.Unity
{
    public enum ScreenKind { Hub, Game, Result, Settings, Shop, Stats }

    /// <summary>Общий базовый класс экранов: экраны сами строят UI в Awake.</summary>
    public abstract class OrbitScreen : MonoBehaviour
    {
        protected OrbitApp App;

        public virtual void Bind(OrbitApp app) { App = app; }
        public virtual void Show() { gameObject.SetActive(true); }
        public virtual void Hide() { gameObject.SetActive(false); }
        public virtual void Refresh() { }
        public virtual void Tick() { }
    }

    /// <summary>Дирижёр: ядро, сохранение, звук и навигация по экранам.</summary>
    public sealed class OrbitApp : MonoBehaviour
    {
        private Transform _canvasRoot;
        private Camera _camera;
        private GameEngineCore _engine;
        private SaveData _save;
        private AudioRouter _audio;

        private HubScreen _hub;
        private GameScreen _game;
        private ResultScreen _result;
        private SettingsScreen _settings;
        private ShopScreen _shop;
        private StatsScreen _stats;

        private ScreenKind _current = ScreenKind.Hub;
        private RunRecord _lastRecord;
        private RewardSummary _lastReward;
        private bool _rewardApplied;
        private float _hapticCooldown;
        private bool _initialized;

        public GameEngineCore Engine { get { return _engine; } }
        public SaveData Save { get { return _save; } }
        public AudioRouter Audio { get { return _audio; } }
        public ScreenKind CurrentScreen { get { return _current; } }
        public RunRecord LastRecord { get { return _lastRecord; } }
        public RewardSummary LastReward { get { return _lastReward; } }
        public Camera UiCamera { get { return _camera; } }

        public void Configure(Transform canvasRoot, Camera camera)
        {
            _canvasRoot = canvasRoot;
            _camera = camera;
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (_canvasRoot == null)
            {
                Canvas found = Object.FindObjectOfType<Canvas>();
                if (found != null) _canvasRoot = found.transform;
            }
            if (_canvasRoot == null)
            {
                GameObject go = new GameObject("OrbitCanvasFallback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                UiBox.Full.Apply(go.GetComponent<RectTransform>());
                Canvas c = go.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler s = go.GetComponent<CanvasScaler>();
                s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                s.referenceResolution = new Vector2(1080f, 1920f);
                _canvasRoot = go.transform;
            }

            _save = UnitySaveStore.Load();
            _engine = new GameEngineCore();
            _audio = gameObject.GetComponent<AudioRouter>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioRouter>();
            _audio.Configure(_save.settings);

            _hub = CreateScreen<HubScreen>("Экран Hub");
            _game = CreateScreen<GameScreen>("Экран Game");
            _result = CreateScreen<ResultScreen>("Экран Result");
            _settings = CreateScreen<SettingsScreen>("Экран Settings");
            _shop = CreateScreen<ShopScreen>("Экран Shop");
            _stats = CreateScreen<StatsScreen>("Экран Stats");

            Open(ScreenKind.Hub);
        }

        private T CreateScreen<T>(string name) where T : OrbitScreen
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRoot, false);
            UiBox.Full.Apply(rt);
            T screen = go.AddComponent<T>();
            screen.Bind(this);
            screen.Hide();
            return screen;
        }

        private void Update()
        {
            if (!_initialized || _engine == null) return;

            if (_engine.Status == GameStatus.Playing)
                _engine.Update(Time.deltaTime);

            ProcessEvents();

            if (_engine.Status == GameStatus.GameOver && !_rewardApplied)
                FinalizeRun();

            if (_current == ScreenKind.Game && _game != null)
            {
                _game.Tick();
                if (Input.GetKeyDown(KeyCode.Escape) && _engine.Draft == null) TogglePause();
            }
        }

        private void ProcessEvents()
        {
            List<GameEvent> events = _engine.Events;
            for (int i = 0; i < events.Count; i++)
            {
                GameEvent ev = events[i];
                if (_audio != null) _audio.PlayFor(ev);
                if (_game != null) _game.OnEvent(ev);
                if (_save != null && _save.settings != null && _save.settings.haptics && Time.unscaledTime >= _hapticCooldown && NeedHaptic(ev.Kind))
                {
                    Handheld.Vibrate();
                    _hapticCooldown = Time.unscaledTime + 0.09f;
                }
            }
            events.Clear();
        }

        private static bool NeedHaptic(GameEventKind kind)
        {
            return kind == GameEventKind.Crash || kind == GameEventKind.ShieldBreak ||
                   kind == GameEventKind.BlockClear || kind == GameEventKind.Orb ||
                   kind == GameEventKind.GoldenOrb || kind == GameEventKind.PowerUp;
        }

        private void FinalizeRun()
        {
            _rewardApplied = true;
            _lastRecord = _engine.ToRecord();
            _lastReward = Progression.Apply(_save, _lastRecord, _engine);
            Persist();
            if (_result != null) _result.ShowResult(_engine, _lastRecord, _lastReward, _save);
            Open(ScreenKind.Result);
        }

        public void StartRun(ModeKind mode)
        {
            if (_engine == null || _save == null) return;
            ModeDef def = Content.Mode(mode);
            _save.selectedMode = def.Id;
            int best;
            if (!_save.bestByMode.TryGetValue(def.Id, out best)) best = 0;
            int seed;
            if (mode == ModeKind.Daily) seed = Content.SeedFromString(Content.Today());
            else seed = System.Environment.TickCount ^ (int)System.DateTime.UtcNow.Ticks;

            _engine.StartRun(mode, _save.selectedSector, seed, best, _save.settings);
            _lastRecord = null;
            _lastReward = null;
            _rewardApplied = false;
            if (_audio != null) _audio.Configure(_save.settings);
            Persist();
            Open(ScreenKind.Game);
        }

        public void Finish()
        {
            if (_engine != null && _engine.Status == GameStatus.Playing)
                _engine.FinishRun();
        }

        public void GoHub()
        {
            Open(ScreenKind.Hub);
        }

        public void Open(ScreenKind kind)
        {
            if (_hub == null || _game == null || _result == null || _settings == null || _shop == null || _stats == null) return;
            _hub.Hide();
            _game.Hide();
            _result.Hide();
            _settings.Hide();
            _shop.Hide();
            _stats.Hide();

            _current = kind;
            OrbitScreen target = ScreenFor(kind);
            if (target == null) return;
            target.Show();
            target.Refresh();
        }

        public void Open(string screenName)
        {
            if (string.IsNullOrEmpty(screenName)) { Open(ScreenKind.Hub); return; }
            string name = screenName.ToLowerInvariant();
            if (name == "game" || name == "игра") Open(ScreenKind.Game);
            else if (name == "result" || name == "результат") Open(ScreenKind.Result);
            else if (name == "settings" || name == "настройки") Open(ScreenKind.Settings);
            else if (name == "shop" || name == "магазин") Open(ScreenKind.Shop);
            else if (name == "stats" || name == "статистика") Open(ScreenKind.Stats);
            else Open(ScreenKind.Hub);
        }

        private OrbitScreen ScreenFor(ScreenKind kind)
        {
            switch (kind)
            {
                case ScreenKind.Game: return _game;
                case ScreenKind.Result: return _result;
                case ScreenKind.Settings: return _settings;
                case ScreenKind.Shop: return _shop;
                case ScreenKind.Stats: return _stats;
                default: return _hub;
            }
        }

        public void SelectMode(ModeKind mode)
        {
            if (_save == null) return;
            _save.selectedMode = Content.Mode(mode).Id;
            Persist();
            if (_hub != null) _hub.Refresh();
        }

        public void SelectSector(int sector)
        {
            if (_save == null) return;
            _save.selectedSector = Mathf.Clamp(sector, 0, Content.Sectors.Length - 1);
            Persist();
            if (_hub != null) _hub.Refresh();
        }

        public void TogglePause()
        {
            if (_engine == null || _engine.Status != GameStatus.Playing || _engine.Draft != null) return;
            _engine.PauseToggle();
            if (_game != null) _game.SetPaused(_engine.Paused);
        }

        public void ToggleMute()
        {
            if (_save == null || _save.settings == null) return;
            _save.settings.muted = !_save.settings.muted;
            if (_audio != null) _audio.ApplySettings();
            Persist();
            if (_game != null) _game.RefreshMute();
        }

        public void ChoosePerk(int index)
        {
            if (_engine != null) _engine.ChoosePerk(index);
        }

        public void Persist()
        {
            UnitySaveStore.Save(_save);
            if (_audio != null && _save != null) _audio.Configure(_save.settings);
        }
    }
}
