using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Orbit.Galaxy.Unity
{
    /// <summary>
    /// Владелец всего Galaxy-рантайма: симуляция, профиль, камера, рендер, ввод, UI и звук.
    /// Фиксированный шаг симуляции отделён от кадра; пауза останавливает только симуляцию.
    /// </summary>
    public sealed class GalaxyApp : MonoBehaviour
    {
        private static GalaxyApp _instance;

        private MatchSimulation _match;
        private AccountProfile _profile;
        private ProgressionRewards _lastRewards;
        private SaveManager _saves;
        private GalaxyAudio _audio;
        private GalaxyInput _input;
        private CameraRig _cameraRig;
        private ArenaRenderer _renderer;
        private GalaxyUi _ui;

        private bool _paused;
        private float _accumulator;
        private GameModeKind _lastMode = GameModeKind.Classic;
        private bool _resultApplied;
        private bool _evolutionShown;
        private bool _abilityEdge;
        private int _abilitySlotEdge = -1;
        private bool _dashEdge;
        private bool _fireEdge;
        private bool _showFps;
        private int _stepsThisFrame;
        private float _hudRefreshTimer;

        public static GalaxyApp Instance { get { return _instance; } }

        public MatchSimulation Match { get { return _match; } }
        public AccountProfile Profile { get { return _profile; } }
        public ProgressionRewards LastRewards { get { return _lastRewards; } }
        public SaveManager Saves { get { return _saves; } }
        public GalaxyAudio Audio { get { return _audio; } }
        public GalaxyInput Input { get { return _input; } }
        public CameraRig CameraRig { get { return _cameraRig; } }
        public ArenaRenderer Renderer { get { return _renderer; } }
        public GalaxyUi Ui { get { return _ui; } }
        public bool Paused { get { return _paused; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            _saves = new SaveManager();
            _saves.SetPath(Path.Combine(Application.persistentDataPath, GameConfig.SaveFileName));
            _profile = LoadProfile();
            if (_profile.Settings == null) _profile.Settings = new GameSettings();

            _audio = GetOrAdd<GalaxyAudio>();
            _input = GetOrAdd<GalaxyInput>();
            _cameraRig = GetOrAdd<CameraRig>();
            _renderer = GetOrAdd<ArenaRenderer>();
            _ui = GetOrAdd<GalaxyUi>();
        }

        private void Start()
        {
            if (_cameraRig != null) _cameraRig.Initialize(this);
            if (_renderer != null) _renderer.Initialize(this);
            if (_ui != null) _ui.Initialize(this);
            ApplySettings();
            if (_ui != null) _ui.ShowMenu();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, GameConfig.MaxFrameDelta);
            if (dt < 0f) dt = 0f;

            bool active = _match != null && !_match.Finished && !_paused;
            if (_input != null) _input.SetVisible(active);
            _stepsThisFrame = 0;
            _abilityEdge = _input != null && _input.AbilityPressed;
            _abilitySlotEdge = _input == null ? -1 : _input.AbilitySlot;
            _dashEdge = _input != null && _input.DashPressed;
            _fireEdge = _input != null && _input.FirePressed;

            if (_input != null && _input.PausePressed)
            {
                _input.Consume();
                if (_match != null && !_match.Finished) TogglePause(!_paused);
            }

            if (active)
            {
                _accumulator += dt;
                int maxSteps = Mathf.CeilToInt(GameConfig.MaxFrameDelta / GameConfig.SimulationStep) + 1;
                while (_accumulator >= GameConfig.SimulationStep && _stepsThisFrame < maxSteps)
                {
                    ApplyInput();
                    _match.Update(GameConfig.SimulationStep);
                    _accumulator -= GameConfig.SimulationStep;
                    _stepsThisFrame++;
                    _abilityEdge = false;
                    _abilitySlotEdge = -1;
                    _dashEdge = false;
                    _fireEdge = false;
                    if (_match.Finished)
                    {
                        _accumulator = 0f;
                        break;
                    }
                }
            }
            else
            {
                _accumulator = 0f;
            }

            if (_input != null && (_stepsThisFrame > 0 || !active)) _input.Consume();

            DrainEvents();
            CheckEvolution();
            CheckFinish();

            if (_cameraRig != null) _cameraRig.UpdateCamera(_match, dt);
            if (_renderer != null) _renderer.Render(_match);

            // HUD text is rebuilt from strings; 10 Hz is enough for gameplay and avoids
            // allocating new text every rendered frame on mobile devices.
            _hudRefreshTimer -= dt;
            if (_ui != null && _match != null && _hudRefreshTimer <= 0f)
            {
                _ui.RefreshHud(_match);
                _hudRefreshTimer = 0.1f;
            }
            UpdateMusic();
        }

        private void ApplyInput()
        {
            GalaxyInput input = _input;
            if (input == null || _match == null) return;

            Vec2 aim = input.Aim;
            if (aim.SqrMagnitude > 0.00001f) _match.PlayerAim(aim);

            if (_abilitySlotEdge == GalaxyInput.CycleAbilitySlot && _match.Player != null)
            {
                SnakeAbilitySlot current = SnakeAbilities.SlotFor(_match.Player.ActiveAbility);
                SetAbilitySlot((SnakeAbilitySlot)(((int)current + 1) % SnakeAbilities.Count));
            }
            else if (_abilitySlotEdge >= 0 && _abilitySlotEdge < SnakeAbilities.Count)
            {
                SetAbilitySlot((SnakeAbilitySlot)_abilitySlotEdge);
            }
            _match.PlayerMove(input.Move);
            _match.PlayerBoost(input.BoostHeld);
            if (_abilityEdge) _match.PlayerActivateAbility();
            if (_dashEdge) _match.PlayerDash();
            if (_fireEdge) _match.PlayerFire();
        }

        private void DrainEvents()
        {
            if (_match == null || _match.Log == null) return;
            List<SimEvent> events = _match.Log.Drain();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent ev = events[i];
                if (ev == null) continue;
                if (_renderer != null) _renderer.OnEvent(ev);
                if (_audio != null) _audio.Play(ev);
                if (_ui == null || ev.Kind == SimEventKind.Tick) continue;
                _ui.PushFeed(ev);
                if (IsToastWorthy(ev)) _ui.ShowToast(EventText(ev));
            }
        }

        private void CheckEvolution()
        {
            if (_match == null || _match.Finished) return;
            List<EvolutionDef> pending = _match.PendingEvolutionChoices;
            if (pending != null && pending.Count > 0)
            {
                if (_evolutionShown) return;
                _evolutionShown = true;
                if (_ui != null) _ui.ShowEvolution(new List<EvolutionDef>(pending));
            }
            else
            {
                _evolutionShown = false;
            }
        }

        private void CheckFinish()
        {
            if (_match == null || !_match.Finished) return;
            FinalizeResult(true);
        }

        private void FinalizeResult(bool showResultUi)
        {
            if (_resultApplied || _match == null) return;
            MatchResult result = _match.Result;
            if (result == null) return;
            _resultApplied = true;

            if (_profile != null)
            {
                _lastRewards = _profile.ApplyResultWithRewards(result, DateTime.UtcNow);
                if (_saves != null) _saves.Save(_profile);
            }
            if (_audio != null) _audio.SetMusicIntensity(0f, 0f);
            if (showResultUi && _ui != null) _ui.ShowResult(result);
        }

        private void UpdateMusic()
        {
            if (_audio == null) return;
            float intensity = 0.12f;
            float danger = 0f;
            if (_match != null && _match.Player != null)
            {
                Ship player = _match.Player;
                intensity = 0.16f + Mathf.Clamp01(player.Level / 16f) * 0.42f
                    + Mathf.Clamp01(_match.Time / 420f) * 0.22f
                    + (player.Streak >= 3 ? 0.12f : 0f);
                danger = (1f - Mathf.Clamp01(player.Hp / Mathf.Max(1f, player.MaxHp))) * 0.85f;
                if (player.Shield <= 0f) danger += 0.08f;
                if (_match.Arena != null)
                {
                    float safe = _match.Arena.SafeRadius;
                    float full = Mathf.Max(1f, _match.Arena.SafeRadiusStart);
                    danger += (1f - Mathf.Clamp01(safe / full)) * 0.35f;
                }
                if (_match.Events != null && _match.Events.PendingKind.HasValue) danger += 0.16f;
                if (_match.Finished) { intensity = 0f; danger = 0f; }
            }
            _audio.SetMusicIntensity(Mathf.Clamp01(intensity), Mathf.Clamp01(danger));
        }

        public void StartMatch(GameModeKind mode)
        {
            _lastMode = mode;
            _paused = false;
            _accumulator = 0f;
            _hudRefreshTimer = 0f;
            _resultApplied = false;
            _lastRewards = null;
            _evolutionShown = false;
            _abilityEdge = false;
            _dashEdge = false;
            _fireEdge = false;
            if (_input != null) _input.Reset();

            if (_match != null && _match.Log != null) _match.Log.Clear();

            ModeDef def = ContentDatabase.FindMode(mode);
            Difficulty difficulty = def != null ? def.SuggestedDifficulty : Difficulty.Normal;
            int seed = unchecked(Environment.TickCount ^ (int)DateTime.UtcNow.Ticks ^ (GetInstanceID() << 7));
            _match = new MatchSimulation(mode, seed, difficulty, _profile);

            if (_renderer != null) _renderer.Initialize(this);
            if (_cameraRig != null && _match.Player != null) _cameraRig.SnapTo(_match.Player.Position);
            if (_ui != null)
            {
                _ui.ShowHud();
                _ui.ShowToast("МАТЧ: " + (def != null ? def.Name : mode.ToString()));
            }
        }

        public void RestartMatch()
        {
            StartMatch(_lastMode);
        }

        public void FinishMatch(bool voluntary)
        {
            if (_match == null)
            {
                ReturnToMenu();
                return;
            }
            if (!_match.Finished) _match.Finish(voluntary);
            FinalizeResult(true);
        }

        public void ReturnToMenu()
        {
            if (_match != null && !_match.Finished)
            {
                try { _match.Finish(true); }
                catch (Exception ex) { Debug.LogWarning("ORBITA GALAXY: завершение матча: " + ex.Message); }
            }
            FinalizeResult(false);
            _match = null;
            _paused = false;
            _accumulator = 0f;
            _evolutionShown = false;
            if (_input != null) _input.Reset();
            if (_renderer != null) _renderer.Render(null);
            if (_ui != null) _ui.ShowMenu();
        }

        public void TogglePause(bool value)
        {
            bool next = value;
            if (next == _paused) return;
            _paused = next;
            _accumulator = 0f;
            // Releasing latched controls on both transitions prevents a stuck boost or ability
            // when the touch layer is hidden and shown again.
            if (_input != null) _input.Reset();
            if (_ui != null)
            {
                if (_paused)
                {
                    // The overlay already carries the title; a second toast duplicated it.
                    _ui.ShowPause();
                }
                else
                {
                    _ui.HidePause();
                    _ui.ShowToast("ПРОДОЛЖЕНИЕ");
                }
            }
        }

        public void ChooseEvolution(EvolutionDef def)
        {
            if (def == null || _match == null || _match.Player == null) return;
            _match.PlayerChooseEvolution(def);
            _evolutionShown = false;
            if (_ui != null)
            {
                _ui.ShowHud();
                _ui.ShowToast("ЭВОЛЮЦИЯ: " + def.Name);
            }
        }

        public void SetAbility(AbilityKind kind)
        {
            SetAbilitySlot(SnakeAbilities.SlotFor(kind));
        }

        public void SetAbilitySlot(SnakeAbilitySlot slot)
        {
            if (_match == null || _match.Player == null) return;
            SnakeAbilityDef def = SnakeAbilities.Get(slot);
            if (def == null) return;
            _match.Player.ActiveAbility = def.Kind;
            if (_ui != null) _ui.ShowToast(def.Name + ": " + def.Purpose);
        }

        public void ApplySettings()
        {
            if (_profile != null && _profile.Settings == null) _profile.Settings = new GameSettings();
            GameSettings settings = _profile == null ? null : _profile.Settings;
            _showFps = settings != null && settings.ShowFps;
            if (_input != null) _input.Configure(settings);
            if (_audio != null)
            {
                _audio.Configure(settings);
                _audio.ApplySettings();
            }
            if (settings != null)
            {
                string quality = settings.Quality == null ? GameSettings.QualityAuto : settings.Quality.ToUpperInvariant();
                if (_renderer != null) _renderer.SetQuality(quality);
                if (quality == GameSettings.QualityLow) Application.targetFrameRate = 30;
                else if (quality == GameSettings.QualityHigh) Application.targetFrameRate = 120;
                else if (quality == GameSettings.QualityMedium) Application.targetFrameRate = 60;
                else Application.targetFrameRate = -1;
            }
        }

        private AccountProfile LoadProfile()
        {
            AccountProfile profile = null;
            try { profile = _saves.Load(); }
            catch (Exception ex) { Debug.LogWarning("ORBITA GALAXY: чтение сохранения: " + ex.Message); }
            if (profile == null)
            {
                try { profile = _saves.Migrate("{}"); }
                catch (Exception) { profile = null; }
            }
            if (profile == null) profile = new AccountProfile();
            if (profile.Settings == null) profile.Settings = new GameSettings();
            return profile;
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component == null) component = gameObject.AddComponent<T>();
            return component;
        }

        private bool IsToastWorthy(SimEvent ev)
        {
            switch (ev.Kind)
            {
                case SimEventKind.LevelUp:
                case SimEventKind.Evolution:
                case SimEventKind.RankUp:
                case SimEventKind.Streak:
                case SimEventKind.BossSpawn:
                case SimEventKind.EventWarning:
                case SimEventKind.EventStart:
                case SimEventKind.EventEnd:
                case SimEventKind.Victory:
                case SimEventKind.Defeat:
                case SimEventKind.Warning:
                case SimEventKind.MatchEnd:
                case SimEventKind.LeaderKilled:
                    return true;
                case SimEventKind.Kill:
                case SimEventKind.Absorb:
                    return _match != null && _match.Player != null && ev.ActorId == _match.Player.Id;
                default:
                    return false;
            }
        }

        private static string EventText(SimEvent ev)
        {
            if (!string.IsNullOrEmpty(ev.Text)) return ev.Text;
            switch (ev.Kind)
            {
                case SimEventKind.LevelUp: return "НОВЫЙ УРОВЕНЬ!";
                case SimEventKind.Evolution: return "ЭВОЛЮЦИЯ ДОСТУПНА";
                case SimEventKind.RankUp: return "МЕСТО В РЕЙТИНГЕ ВЫРОСЛО";
                case SimEventKind.Streak: return "СЕРИЯ УНИЧТОЖЕНИЙ!";
                case SimEventKind.BossSpawn: return "ПОЯВИЛСЯ БОСС";
                case SimEventKind.EventWarning: return "ВНИМАНИЕ: СОБЫТИЕ";
                case SimEventKind.EventStart: return "СОБЫТИЕ НАЧАЛОСЬ";
                case SimEventKind.EventEnd: return "СОБЫТИЕ ЗАВЕРШЕНО";
                case SimEventKind.Victory: return "ПОБЕДА!";
                case SimEventKind.Defeat: return "ПОРАЖЕНИЕ";
                case SimEventKind.Warning: return "ОПАСНОСТЬ";
                case SimEventKind.MatchEnd: return "МАТЧ ЗАВЕРШЁН";
                case SimEventKind.Kill: return "УНИЧТОЖЕНИЕ";
                case SimEventKind.Absorb: return "ПОГЛОЩЕНИЕ";
                case SimEventKind.LeaderKilled: return "ЛИДЕР УНИЧТОЖЕН";
                default: return ev.Kind.ToString();
            }
        }

        private void OnGUI()
        {
            if (!_showFps) return;
            float fps = Time.unscaledDeltaTime > 0.0001f ? 1f / Time.unscaledDeltaTime : 0f;
            GUI.Label(new Rect(Screen.width - 124f, 8f, 116f, 26f),
                Mathf.RoundToInt(fps) + " FPS");
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            if (_profile != null && _saves != null) _saves.Save(_profile);
            // Backgrounding must not leave a live match ticking or resume with a stale input state.
            if (_match != null && !_match.Finished) TogglePause(true);
        }

        private void OnApplicationQuit()
        {
            if (_profile != null && _saves != null) _saves.Save(_profile);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
