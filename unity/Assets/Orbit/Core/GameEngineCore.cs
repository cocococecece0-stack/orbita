using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit.Core
{
    public sealed class PerkDraft
    {
        public int[] Options = new int[3];
        public int Round;
    }

    /// <summary>
    /// Generic entity used by the orbit simulation. The first six fields are the original
    /// Unity surface; the rest carry the native entity state (Asteroid/Orb/Boss/Wormhole/...).
    /// </summary>
    public struct OrbitThing
    {
        public float Angle;
        public float Radius;
        public float Speed;
        public float Size;
        public int Kind;
        public bool Active;

        // Native-style aliases and extra state.
        public float Distance
        {
            get { return Radius; }
            set { Radius = value; }
        }
        public float Rotation;
        public float Spin;
        public float Pulse;
        public float Life;
        public bool Dodged;
        public bool NearMissed;
        public bool Dead;
        public int Sprite;

        // Wormholes / gravity wells.
        public float Partner;
        public float Cooldown;
        public float Strength;

        // Bosses.
        public int Hp;
        public float Duration;
        public float Charge;
        public float HitFlash;
        public float Time;

        // Particles.
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float MaxLife;
        public float Power;
    }

    /// <summary>
    /// Faithful C# port of the shipped native GameEngine.kt simulation.
    /// No Unity APIs; deterministic for a given seed and input sequence.
    /// </summary>
    public sealed class GameEngineCore
    {
        private const float Tau = 6.2831855f;
        private const float PlayerHitRadius = 0.055f; // native minDim*0.017 / orbitRadius(minDim*0.31)
        private const int MaxEvents = 512;

        public ModeKind Mode { get; private set; }
        public int SectorId { get; private set; }
        public GameStatus Status { get; private set; } = GameStatus.Ready;
        public bool Paused;
        public bool Completed;
        public bool IsNewRecord;
        public int Score;
        public int Best;
        public int Level = 1;
        public int Combo;
        public int Multiplier = 1;
        public int OrbsCollected;
        public int MaxCombo;
        public int TimeLeft
        {
            get
            {
                if (_timeLeft <= 0f) return _timeLeft < 0f ? -1 : 0;
                return (int)Math.Ceiling(_timeLeft - 0.000001f);
            }
            set { _timeLeft = value; }
        }
        public float Elapsed { get { return _time; } set { _time = value; } }
        public float Resonance;
        public bool Shield;
        public float ShieldTime;
        public float StormWarning;
        public float FlareWarning;
        public float ResonanceFlash;
        public float LevelUpFlash;
        public int ResonanceCount;
        public int BossKills;
        public int Direction = 1;
        public float PlayerAngle;
        public float PlayerRadius = 1f;
        public PerkDraft Draft;
        public readonly List<int> PerkIds = new List<int>();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly List<OrbitThing> Asteroids = new List<OrbitThing>();
        public readonly List<OrbitThing> Orbs = new List<OrbitThing>();
        public readonly List<OrbitThing> PowerUps = new List<OrbitThing>();
        public readonly BlockArena Blocks = new BlockArena();
        public readonly RunStats Stats = new RunStats();
        public GameSettings Settings { get; private set; } = new GameSettings();

        // Extra surfaces mirroring the native engine's entity lists.
        public readonly List<OrbitThing> Wormholes = new List<OrbitThing>();
        public readonly List<OrbitThing> Wells = new List<OrbitThing>();
        public readonly List<OrbitThing> Bosses = new List<OrbitThing>();
        public readonly List<OrbitThing> Particles = new List<OrbitThing>();

        public bool Fever;
        public float SpeedLevel { get { return _speedLevel; } }
        public float TimeLeftSeconds { get { return _timeLeft; } }
        public float ShieldFlash { get { return _shieldFlash; } }
        public float Flash { get { return _flash; } }
        public float Shake { get { return _shake; } }
        public float OrbitRadius { get { return 1f; } }
        public float SpawnDistance { get { return _spawnDistance; } }
        public float ViewportWidth { get { return _viewportWidth; } }
        public float ViewportHeight { get { return _viewportHeight; } }

        // Native callbacks, surfaced for Unity audio/UI without changing the Events list API.
        public Action<GameEvent> OnEvent;
        public Action<RunStats> OnRunEnd;
        public Action<int> OnNewRecord;

        private long _rngState = 1L;
        private float _time;
        private float _timeLeft = -1f;
        private float _spawnTimer;
        private float _orbTimer;
        private float _powerTimer;
        private float _stormTimer;
        private float _flareTimer;
        private float _mineTimer;
        private float _portalTimer;
        private float _wellTimer;
        private int _nextBossLevel;
        private float _eventCooldown;
        private float _seasonDensity = 1f;
        private float _ghostTimer;
        private bool _reviveAvailable;
        private bool _upgradeShield;
        private bool _upgradeTime;
        private bool _upgradeMagnet;
        private bool _upgradeSlow;
        private bool _upgradeDouble;
        private bool _reducedMotion;
        private float _scoreBoost = 1f;
        private int _orbBoost = 1;
        private float _hazardSpeed = 1f;
        private float _hazardDensity = 1f;
        private float _nextDraftAt = 16f;
        private int _draftRound;
        private bool _objectiveRewarded;

        private float _hitStop;
        private float _invulnerable;
        private float _slowTimer;
        private float _doubleTimer;
        private float _magnetTimer;
        private float _phaseTimer;
        private float _freezeTimer;
        private float _comboTime;
        private float _flash;
        private float _levelUpFlashValue;
        private float _shieldFlash;
        private float _reverseFlash;
        private float _shake;
        private float _speedLevel;
        private float _hazardSpawnCursor;
        private float _orbSpawnCursor;
        private float _viewportWidth = 1f;
        private float _viewportHeight = 1f;
        // Native: sqrt(width^2 + height^2) * 0.5 / (minDim * 0.31).
        private float _spawnDistance = 2.28f;

        public bool CanInput
        {
            get { return Status == GameStatus.Playing && !Paused && Draft == null; }
        }

        public void SetSettings(GameSettings settings)
        {
            Settings = settings ?? new GameSettings();
            _reducedMotion = Settings.reducedMotion;
        }

        public void SetReducedMotion(bool value)
        {
            _reducedMotion = value;
        }

        public void SetBest(int value)
        {
            Best = value;
        }

        public void SetSeasonModifier(float value)
        {
            _seasonDensity = Clamp(value, 0.5f, 2f);
        }

        /// <summary>
        /// Optional native parity hook: pass the gameplay viewport size so hazard spawn
        /// distance matches GameEngine.resize(). The default is a square viewport.
        /// </summary>
        public void Resize(float width, float height)
        {
            if (width <= 0f || height <= 0f) return;
            _viewportWidth = width;
            _viewportHeight = height;
            float minDim = Math.Min(width, height);
            if (minDim <= 0f) return;
            _spawnDistance = (float)Math.Sqrt(width * width + height * height) * 0.5f / (minDim * 0.31f);
        }

        public void SetUpgrades(Dictionary<string, bool> flags)
        {
            _upgradeShield = flags != null && flags.ContainsKey("upg_shield") && flags["upg_shield"];
            _upgradeTime = flags != null && flags.ContainsKey("upg_time") && flags["upg_time"];
            _upgradeMagnet = flags != null && flags.ContainsKey("upg_magnet") && flags["upg_magnet"];
            _upgradeSlow = flags != null && flags.ContainsKey("upg_slow") && flags["upg_slow"];
            _upgradeDouble = flags != null && flags.ContainsKey("upg_double") && flags["upg_double"];
        }

        public void SetUpgrades(SaveData save)
        {
            if (save == null) return;
            var flags = new Dictionary<string, bool>();
            if (save.upgradeLevels != null)
                foreach (KeyValuePair<string, int> pair in save.upgradeLevels)
                    flags[pair.Key] = pair.Value > 0;
            SetUpgrades(flags);
        }

        public void StartRun(ModeKind mode, int sector, int seed, int best, GameSettings settings)
        {
            unchecked
            {
                _rngState = ((long)seed ^ 0x5DEECE66DL) & long.MaxValue;
            }
            Mode = mode;
            Blocks.Reset(new Func<float>(Rnd));
            SectorId = Clamp(sector, 0, Content.Sectors.Length - 1);
            Best = best;
            Settings = settings ?? new GameSettings();
            _reducedMotion = Settings.reducedMotion;

            Asteroids.Clear();
            Orbs.Clear();
            PowerUps.Clear();
            Wormholes.Clear();
            Wells.Clear();
            Bosses.Clear();
            Particles.Clear();

            Score = 0;
            IsNewRecord = false;
            Paused = false;
            PerkIds.Clear();
            Combo = 0;
            Multiplier = 1;
            Level = Mode == ModeKind.Survival ? 3 : 1;
            Shield = _upgradeShield;
            ShieldTime = Shield ? 14f : 0f;
            OrbsCollected = 0;
            MaxCombo = 0;
            BossKills = 0;
            ResonanceCount = 0;
            Fever = false;

            _eventCooldown = 0f;
            _time = 0f;
            _comboTime = 0f;
            _slowTimer = 0f;
            _doubleTimer = 0f;
            _magnetTimer = 0f;
            _phaseTimer = 0f;
            _freezeTimer = 0f;
            _invulnerable = 0f;
            _hitStop = 0f;
            _flash = 0f;
            _levelUpFlashValue = 0f;
            LevelUpFlash = 0f;
            _shieldFlash = 0f;
            ResonanceFlash = 0f;
            Resonance = 0f;
            StormWarning = 0f;
            FlareWarning = 0f;
            PlayerAngle = -(float)Math.PI / 2f;
            Direction = 1;

            _spawnTimer = 0.7f;
            _orbTimer = 2.2f;
            _powerTimer = 5f;
            _stormTimer = 20f;
            _flareTimer = 16f;
            _mineTimer = 9f;
            _portalTimer = 12f;
            _wellTimer = 18f;
            _orbSpawnCursor = 0f;
            _hazardSpawnCursor = Rnd() * Tau;
            _speedLevel = 0f;
            _reverseFlash = 0f;
            _shake = 0f;

            Draft = null;
            _scoreBoost = 1f;
            _orbBoost = 1;
            _hazardSpeed = 1f;
            _hazardDensity = 1f;
            _nextDraftAt = 16f;
            _draftRound = 0;
            _objectiveRewarded = false;
            Completed = false;
            _reviveAvailable = false;
            _nextBossLevel = 5;
            _ghostTimer = 0f;

            int seconds = ModeInfo(Mode).Seconds;
            _timeLeft = seconds > 0 ? seconds + (_upgradeTime ? 8f : 0f) : -1f;

            Stats.score = 0;
            Stats.level = Level;
            Stats.orbs = 0;
            Stats.goldenOrbs = 0;
            Stats.rareOrbs = 0;
            Stats.maxCombo = 0;
            Stats.nearMisses = 0;
            Stats.dodges = 0;
            Stats.bossKills = 0;
            Stats.wormholes = 0;
            Stats.seconds = 0;
            Stats.powerups = 0;
            Stats.hits = 0;
            Status = GameStatus.Playing;
            Events.Clear();
        }

        public void StartRun(ModeKind mode, int sector, int seed, GameSettings settings)
        {
            StartRun(mode, sector, seed, Best, settings);
        }

        public void Update(float dtRaw)
        {
            float step = dtRaw;
            if (step < 0f) step = 0f;
            if (step > 0.05f) step = 0.05f;
            _time += step;
            bool running = Status == GameStatus.Playing && !Paused && Draft == null;

            if (running && _timeLeft > 0f)
            {
                _timeLeft = Math.Max(0f, _timeLeft - step);
                Stats.seconds = (int)_time;
                if (_timeLeft <= 0f) { FinishRun(); return; }
            }
            else if (running)
            {
                Stats.seconds = (int)_time;
            }

            DecayVisuals(step);
            UpdateParticles(step);
            if (!running) return;

            if (Mode == ModeKind.Stack)
            {
                ApplyBlockStep(Blocks.Tick(step));
                CheckObjectiveInRun();
                AddEvent(GameEventKind.Tick);
                return;
            }

            if (_draftRound < 6 && _time >= _nextDraftAt)
            {
                OpenDraft();
                return;
            }
            if (_hitStop > 0f)
            {
                _hitStop = Math.Max(0f, _hitStop - step);
                return;
            }
            _eventCooldown = Math.Max(0f, _eventCooldown - step);

            if (ShieldTime > 0f)
            {
                ShieldTime = Math.Max(0f, ShieldTime - step);
                if (ShieldTime <= 0f) { Shield = false; AddEvent(GameEventKind.ShieldBreak); }
            }
            if (Combo > 0)
            {
                _comboTime -= step;
                if (_comboTime <= 0f) { Combo = 0; Multiplier = 1; Fever = false; }
            }
            _slowTimer = Math.Max(0f, _slowTimer - step);
            _doubleTimer = Math.Max(0f, _doubleTimer - step);
            _magnetTimer = Math.Max(0f, _magnetTimer - step);
            _phaseTimer = Math.Max(0f, _phaseTimer - step);
            _freezeTimer = Math.Max(0f, _freezeTimer - step);
            _invulnerable = Math.Max(0f, _invulnerable - step);

            _speedLevel = Math.Min(1f, Score / 400f + (Mode == ModeKind.Survival ? _time / 180f : 0f));
            // Native accumulates the angle without wrapping; AngDiff normalizes only deltas.
            PlayerAngle += Direction * (1.45f + _speedLevel * 0.65f) * step;
            if (_ghostTimer <= 0f) _ghostTimer = 0.08f;

            UpdateSpawns(step);
            UpdateHazards(step);
            UpdateCollisions(step);
            CheckObjectiveInRun();
            AddEvent(GameEventKind.Tick);
        }

        private void CheckObjectiveInRun()
        {
            if (Status != GameStatus.Playing || _objectiveRewarded || !ObjectiveDone()) return;
            _objectiveRewarded = true;
            ResonanceFlash = 1f;
            if (Mode == ModeKind.Stack) AddBlockPoints(200);
            else AddPoints(200);
        }

        public void Tap()
        {
            AddEvent(GameEventKind.Tap);
            if (Mode == ModeKind.Stack) return;
            if (Paused) { Paused = false; return; }
            if (Status == GameStatus.Ready)
            {
                int seed = (int)(DateTime.UtcNow.Ticks & 0x7fffffffL);
                StartRun(Mode, SectorId, seed, Best, Settings);
                return;
            }
            if (Status == GameStatus.Playing)
            {
                Direction = -Direction;
                _reverseFlash = 1f;
            }
        }

        public void MoveBlock(int direction)
        {
            if (!CanInput || Mode != ModeKind.Stack) return;
            if (Blocks.Move(direction)) AddEvent(GameEventKind.BlockMove, direction);
        }

        public void RotateBlock()
        {
            if (!CanInput || Mode != ModeKind.Stack) return;
            if (Blocks.Rotate()) AddEvent(GameEventKind.BlockRotate);
        }

        public void HoldBlock()
        {
            if (!CanInput || Mode != ModeKind.Stack) return;
            Blocks.Hold();
            AddEvent(GameEventKind.BlockHold);
            if (Blocks.TopOut) BlockOver();
        }

        public void SoftDropBlock()
        {
            if (!CanInput || Mode != ModeKind.Stack) return;
            ApplyBlockStep(Blocks.SoftDrop());
        }

        public void HardDropBlock()
        {
            if (!CanInput || Mode != ModeKind.Stack) return;
            BlockStep step = Blocks.HardDrop();
            if (step.Dropped > 0) AddEvent(GameEventKind.BlockDrop, step.Dropped);
            ApplyBlockStep(step);
        }

        public void ChoosePerk(int option)
        {
            if (Draft == null || option < 0 || option >= Draft.Options.Length) return;
            int index = Draft.Options[option];
            if (index < 0 || index >= Content.Perks.Length) return;
            PerkIds.Add(index);
            string id = Content.Perks[index].Key;
            switch (id)
            {
                case "overclock":
                    _scoreBoost += 0.25f;
                    _hazardSpeed += 0.10f;
                    break;
                case "barrier":
                    _scoreBoost = Math.Max(0.5f, _scoreBoost - 0.10f);
                    Shield = true;
                    ShieldTime = Math.Max(ShieldTime, 14f);
                    break;
                case "magnet":
                    _magnetTimer = Math.Max(_magnetTimer, 10f);
                    _hazardDensity += 0.10f;
                    break;
                case "slowmo":
                    _scoreBoost = Math.Max(0.5f, _scoreBoost - 0.10f);
                    _slowTimer = Math.Max(_slowTimer, 10f);
                    break;
                case "pulse":
                    TriggerResonance();
                    Combo = 0;
                    Multiplier = 1;
                    _comboTime = 0f;
                    break;
                case "ghost":
                    _phaseTimer = Math.Max(_phaseTimer, 8f);
                    _invulnerable = Math.Max(_invulnerable, 8f);
                    _orbTimer += 4f;
                    break;
                case "harvest":
                    _scoreBoost = Math.Max(0.5f, _scoreBoost - 0.10f);
                    _orbBoost = 2;
                    break;
                case "chronos":
                    _hazardDensity += 0.15f;
                    if (_timeLeft > 0f) _timeLeft += 12f;
                    else AddPoints(150);
                    break;
            }
            _nextDraftAt = _time + 18f;
            Paused = false;
            Draft = null;
        }

        public ObjectiveDef Objective()
        {
            return Content.Objective(Mode, SectorId);
        }

        public int ObjectiveValue()
        {
            ObjectiveDef objective = Objective();
            switch (objective.Field)
            {
                case ObjectiveField.Orbs: return OrbsCollected;
                case ObjectiveField.Lines: return Stats.orbs;
                case ObjectiveField.Seconds: return (int)_time;
                default: return Score;
            }
        }

        public bool ObjectiveDone()
        {
            return ObjectiveValue() >= Objective().Target;
        }

        public RunRecord FinishRun()
        {
            if (Status == GameStatus.GameOver) return ToRecord();
            // The native engine grants the 200-point objective bonus as soon as the goal is met.
            // Granting it here as well keeps the previous Unity API correct when the UI ends a run
            // before the next Update tick.
            CheckObjectiveInRun();
            Status = GameStatus.GameOver;
            // Native finishRun sets completed = true; the Unity UI uses Completed for objective
            // success, so keep that meaning while still emitting a proper Finish event.
            Completed = ObjectiveDone();
            Paused = false;
            Draft = null;
            Stats.seconds = Math.Max(1, (int)_time);
            Stats.maxCombo = Math.Max(Stats.maxCombo, MaxCombo);
            Stats.level = Level;
            Stats.score = Score;
            IsNewRecord = Score > Best;
            if (IsNewRecord) Best = Score;
            if (OnRunEnd != null) OnRunEnd(Stats);
            AddEvent(GameEventKind.Finish, Completed ? 1 : 0);
            return ToRecord();
        }

        public void PauseToggle()
        {
            if (Status != GameStatus.Playing) return;
            Paused = !Paused;
        }

        public void Pause()
        {
            if (Status == GameStatus.Playing) Paused = true;
        }

        public void Resume()
        {
            Paused = false;
        }

        public RunRecord ToRecord()
        {
            return new RunRecord
            {
                mode = Content.Mode(Mode).Id,
                sector = SectorId,
                score = Math.Max(0, Score),
                orbs = Math.Max(0, Stats.orbs),
                maxCombo = Math.Max(0, Stats.maxCombo),
                nearMisses = Math.Max(0, Stats.nearMisses),
                dodges = Math.Max(0, Stats.dodges),
                bossKills = Math.Max(0, BossKills),
                wormholes = Math.Max(0, Stats.wormholes),
                rareOrbs = Math.Max(0, Stats.rareOrbs),
                seconds = Math.Max(0, Stats.seconds),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                level = Math.Max(1, Level),
                completed = Completed
            };
        }

        // ---------------- Native achievement / secret conditions ----------------

        public List<string> AchievedIds()
        {
            var result = new List<string>();
            AddIf(result, "first_run", true);
            AddIf(result, "score_500", Stats.score >= 500);
            AddIf(result, "score_1500", Stats.score >= 1500);
            AddIf(result, "score_5000", Stats.score >= 5000);
            AddIf(result, "combo_10", Stats.maxCombo >= 10);
            AddIf(result, "combo_25", Stats.maxCombo >= 25);
            AddIf(result, "orbs_50", Stats.orbs >= 50);
            AddIf(result, "near_10", Stats.nearMisses >= 10);
            AddIf(result, "near_30", Stats.nearMisses >= 30);
            AddIf(result, "level_10", Stats.level >= 10);
            AddIf(result, "level_20", Stats.level >= 20);
            AddIf(result, "boss_1", Stats.bossKills >= 1);
            AddIf(result, "boss_5", Stats.bossKills >= 5);
            AddIf(result, "worm_5", Stats.wormholes >= 5);
            AddIf(result, "stack_40", Mode == ModeKind.Stack && Stats.orbs >= 40);
            AddIf(result, "objective_done", ObjectiveDone());
            return result;
        }

        public List<string> SecretIds()
        {
            var result = new List<string>();
            AddIf(result, "eye", Stats.rareOrbs >= 3);
            AddIf(result, "dancer", Stats.nearMisses >= 25 && Stats.seconds <= 60);
            AddIf(result, "survivor", Stats.level >= 20 && Stats.hits == 0);
            return result;
        }

        private static void AddIf(List<string> list, string id, bool ok)
        {
            if (ok) list.Add(id);
        }

        // ---------------- Simulation internals ----------------

        private void OpenDraft()
        {
            _draftRound++;
            var pool = new List<int>();
            for (int i = 0; i < Content.Perks.Length; i++) pool.Add(i);
            var options = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int at = (int)(Rnd() * pool.Count);
                if (at < 0) at = 0;
                if (at >= pool.Count) at = pool.Count - 1;
                options.Add(pool[at]);
                pool.RemoveAt(at);
            }
            Draft = new PerkDraft { Options = options.ToArray(), Round = _draftRound };
            AddEvent(GameEventKind.PerkDraft, _draftRound);
        }

        private void UpdateSpawns(float step)
        {
            SectorDef sector = Content.Sector(SectorId);
            if (StormWarning > 0f)
            {
                StormWarning -= step;
                if (StormWarning <= 0f) SpawnStorm();
            }
            if (FlareWarning > 0f)
            {
                FlareWarning -= step;
                if (FlareWarning <= 0f) SpawnFlare();
            }

            if (Mode == ModeKind.Zen)
            {
                _orbTimer -= step;
                if (_orbTimer <= 0f) { SpawnOrb(); _orbTimer = 1.6f + Rnd() * 1.8f; }
                _powerTimer -= step;
                if (_powerTimer <= 0f) { SpawnPowerUp(); _powerTimer = 5f + Rnd() * 4f; }
                return;
            }

            if (Level >= 2)
            {
                _stormTimer -= step;
                if (_stormTimer <= 0f && StormWarning <= 0f)
                {
                    StormWarning = 1.6f;
                    _stormTimer = 22f + Rnd() * 12f;
                    AddEvent(GameEventKind.Storm);
                }
            }
            if (sector.Has(HazardKind.Flare) && Level >= 6)
            {
                _flareTimer -= step;
                if (_flareTimer <= 0f && FlareWarning <= 0f)
                {
                    FlareWarning = 1.2f;
                    _flareTimer = 14f + Rnd() * 10f;
                    AddEvent(GameEventKind.Flare);
                }
            }

            _spawnTimer -= step;
            if (_spawnTimer <= 0f && StormWarning <= 0f)
            {
                SpawnAsteroid();
                float baseInterval = 1.0f - Math.Min(0.35f, Score * 0.0004f);
                float floor = Mode == ModeKind.Survival ? 0.40f : 0.55f;
                float divisor = sector.Density * _seasonDensity * _hazardDensity;
                _spawnTimer = Clamp(baseInterval / divisor, floor, 1.1f) * (0.85f + Rnd() * 0.3f);
            }
            _orbTimer -= step;
            if (_orbTimer <= 0f) { SpawnOrb(); _orbTimer = 2.8f + Rnd() * 2.5f; }
            _powerTimer -= step;
            if (_powerTimer <= 0f) { SpawnPowerUp(); _powerTimer = 6f + Rnd() * 5f; }

            if (sector.Has(HazardKind.Mine) && Level >= 3)
            {
                _mineTimer -= step;
                if (_mineTimer <= 0f) { SpawnMine(); _mineTimer = 7f + Rnd() * 6f; }
            }
            if (sector.Has(HazardKind.Wormhole) && Level >= 4)
            {
                _portalTimer -= step;
                if (_portalTimer <= 0f) { SpawnWormholes(); _portalTimer = 16f + Rnd() * 10f; }
            }
            if (sector.Has(HazardKind.Gravity) && Level >= 5)
            {
                _wellTimer -= step;
                if (_wellTimer <= 0f)
                {
                    var well = new OrbitThing { Angle = Rnd() * Tau, Strength = 0.5f + Rnd() * 0.7f, Active = true };
                    Wells.Add(well);
                    _wellTimer = 20f + Rnd() * 12f;
                }
            }
            while (Level >= _nextBossLevel && Status == GameStatus.Playing)
            {
                SpawnBoss();
                _nextBossLevel += 5;
            }
        }

        private void UpdateHazards(float step)
        {
            if (_freezeTimer <= 0f)
            {
                for (int i = 0; i < Wells.Count; i++)
                {
                    OrbitThing well = Wells[i];
                    well.Pulse += step * 2f;
                    Wells[i] = well;
                    for (int j = 0; j < Asteroids.Count; j++)
                    {
                        OrbitThing a = Asteroids[j];
                        if (a.Kind == (int)AsteroidKind.Bolt) continue;
                        a.Speed *= 1f + well.Strength * step * 0.10f;
                        Asteroids[j] = a;
                    }
                }
            }
            for (int i = 0; i < Wormholes.Count; i++)
            {
                OrbitThing w = Wormholes[i];
                w.Pulse += step * 3f;
                w.Cooldown = Math.Max(0f, w.Cooldown - step);
                Wormholes[i] = w;
            }
            for (int i = Bosses.Count - 1; i >= 0; i--)
            {
                OrbitThing b = Bosses[i];
                b.Time += step;
                b.HitFlash = Math.Max(0f, b.HitFlash - step * 4f);
                b.Charge += step;
                float interval = 2.4f / (1f + Math.Min(2, Math.Max(0, b.Kind)) * 0.22f);
                if (b.Charge >= interval)
                {
                    b.Charge = 0f;
                    int shots = 1 + Clamp(b.Kind, 0, 2);
                    for (int k = -shots; k <= shots; k++)
                        Asteroids.Add(MakeBolt(Norm(b.Angle + k * (0.16f - shots * 0.02f))));
                    AddEvent(GameEventKind.BossHit);
                }
                if (b.Time >= b.Duration) b.Dead = true;
                Bosses[i] = b;
                if (b.Dead) Bosses.RemoveAt(i);
            }
        }

        private void UpdateCollisions(float step)
        {
            float px = (float)Math.Cos(PlayerAngle);
            float py = (float)Math.Sin(PlayerAngle);
            float speedScale = (_slowTimer > 0f ? 0.45f : (_freezeTimer > 0f ? 0f : 1f)) * _hazardSpeed;
            OrbitThing hit = default;
            int hitIndex = -1;
            bool hasHit = false;

            for (int i = 0; i < Asteroids.Count; i++)
            {
                OrbitThing a = Asteroids[i];
                if (a.Dead) continue;
                a.Pulse += step * 2f;
                float prevDistance = a.Radius;
                if (_freezeTimer <= 0f) a.Radius -= a.Speed * speedScale * step;
                a.Rotation += a.Spin * step;
                a.Life -= step;
                if (a.Life <= 0f) { a.Dead = true; Asteroids[i] = a; continue; }

                float ax = (float)Math.Cos(a.Angle) * a.Radius;
                float ay = (float)Math.Sin(a.Angle) * a.Radius;
                float d = Distance(ax, ay, px, py);
                float cd = a.Size + PlayerHitRadius;
                float angleToPlayer = Math.Abs(AngDiff(a.Angle, PlayerAngle));

                if (d < cd && _invulnerable <= 0f && _phaseTimer <= 0f)
                {
                    hit = a;
                    hitIndex = i;
                    hasHit = true;
                    Asteroids[i] = a;
                    break;
                }
                if (!a.NearMissed && d > cd && d < cd + PlayerHitRadius * 1.2f &&
                    angleToPlayer < 0.16f && _invulnerable <= 0f && a.Kind != (int)AsteroidKind.Bolt)
                {
                    a.NearMissed = true;
                    Stats.nearMisses++;
                    RewardSkill(1);
                    if (a.Dead) { Asteroids[i] = a; continue; }
                    _flash = 1f;
                    _hitStop = _reducedMotion ? 0f : 0.02f;
                    AddEvent(GameEventKind.NearMiss);
                }
                if (!a.Dodged && prevDistance >= 1f && a.Radius < 1f &&
                    angleToPlayer < 0.24f && a.Kind != (int)AsteroidKind.Bolt)
                {
                    a.Dodged = true;
                    Stats.dodges++;
                    RewardSkill(1);
                    if (a.Dead) { Asteroids[i] = a; continue; }
                    AddEvent(GameEventKind.Dodge, Combo);
                }
                if (a.Radius <= 0.95f)
                {
                    Burst(ax, ay, 6, 3, 0.45f);
                    a.Dead = true;
                }
                Asteroids[i] = a;
            }
            RemoveDead(Asteroids);
            if (hasHit)
            {
                hit = HandleHit(hit);
                if (hitIndex >= 0 && hitIndex < Asteroids.Count) Asteroids[hitIndex] = hit;
                RemoveDead(Asteroids);
                return;
            }

            for (int i = Orbs.Count - 1; i >= 0; i--)
            {
                OrbitThing o = Orbs[i];
                o.Pulse += step * 3f;
                if (_magnetTimer > 0f || _upgradeMagnet)
                {
                    float diff = AngDiff(PlayerAngle, o.Angle);
                    o.Angle = Norm(o.Angle + diff * step * (_upgradeMagnet ? 0.5f : 1.6f));
                }
                float ox = (float)Math.Cos(o.Angle);
                float oy = (float)Math.Sin(o.Angle);
                if (Distance(ox, oy, px, py) < PlayerHitRadius * 2.4f)
                {
                    CollectOrb(o);
                    Orbs.RemoveAt(i);
                }
                else Orbs[i] = o;
            }

            for (int i = PowerUps.Count - 1; i >= 0; i--)
            {
                OrbitThing p = PowerUps[i];
                p.Pulse += step * 4f;
                float ox = (float)Math.Cos(p.Angle);
                float oy = (float)Math.Sin(p.Angle);
                if (Distance(ox, oy, px, py) < PlayerHitRadius * 2.8f)
                {
                    ApplyPowerUp(PowerUpFromNativeIndex(p.Kind));
                    Burst(ox, oy, 18, p.Kind + 4, 1f);
                    Stats.powerups++;
                    AddEvent(GameEventKind.PowerUp, p.Kind);
                    PowerUps.RemoveAt(i);
                }
                else PowerUps[i] = p;
            }

            for (int i = 0; i < Wormholes.Count; i++)
            {
                OrbitThing w = Wormholes[i];
                if (w.Cooldown > 0f) continue;
                if (Math.Abs(AngDiff(w.Angle, PlayerAngle)) < 0.10f)
                {
                    PlayerAngle = Norm(w.Partner + 0.35f * Direction);
                    w.Cooldown = 2f;
                    Stats.wormholes++;
                    Burst(px, py, 20, 6, 1f);
                    AddEvent(GameEventKind.Wormhole);
                    Wormholes[i] = w;
                }
            }
            RemoveDead(Asteroids);
        }

        private void CollectOrb(OrbitThing orb)
        {
            ChargeResonance(0.04f);
            switch (orb.Kind)
            {
                case 0: // Normal
                    Stats.orbs++;
                    OrbsCollected++;
                    RewardSkill(2 * _orbBoost);
                    AddEvent(GameEventKind.Orb);
                    break;
                case 1: // Golden
                    Stats.orbs++;
                    Stats.goldenOrbs++;
                    OrbsCollected++;
                    RewardSkill(8 * _orbBoost);
                    AddEvent(GameEventKind.GoldenOrb, Combo);
                    break;
                default: // Artifact
                    Stats.rareOrbs++;
                    RewardSkill(25 * _orbBoost);
                    AddEvent(GameEventKind.Artifact);
                    break;
            }
            float ox = (float)Math.Cos(orb.Angle);
            float oy = (float)Math.Sin(orb.Angle);
            Burst(ox, oy, 14, orb.Kind == 0 ? 1 : 7, 0.9f);
            DamageBoss(orb.Angle);
        }

        private void DamageBoss(float angle)
        {
            for (int i = 0; i < Bosses.Count; i++)
            {
                OrbitThing b = Bosses[i];
                if (Math.Abs(AngDiff(b.Angle, angle)) > 0.75f) continue;
                b.HitFlash = 1f;
                AddPoints(40);
                AddEvent(GameEventKind.BossHit);
                if (b.Hp <= 1)
                {
                    b.Dead = true;
                    Stats.bossKills++;
                    BossKills = Stats.bossKills;
                    AddPoints(150 + 10 * Level);
                    Burst((float)Math.Cos(b.Angle), (float)Math.Sin(b.Angle), 60, 8, 2f);
                    AddEvent(GameEventKind.BossDown);
                }
                else b.Hp--;
                Bosses[i] = b;
                break;
            }
            RemoveDead(Bosses);
        }

        private int ActiveCount()
        {
            return (Shield ? 1 : 0) + (_slowTimer > 0f ? 1 : 0) + (_doubleTimer > 0f ? 1 : 0) +
                   (_magnetTimer > 0f ? 1 : 0) + (_phaseTimer > 0f ? 1 : 0) + (_freezeTimer > 0f ? 1 : 0);
        }

        private void ApplyPowerUp(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield:
                    if (ActiveCount() < 3) { Shield = true; ShieldTime = _upgradeShield ? 14f : 10f; }
                    break;
                case PowerUpKind.Slow:
                    if (ActiveCount() < 3) _slowTimer = _upgradeSlow ? 8f : 5f;
                    break;
                case PowerUpKind.Double:
                    if (ActiveCount() < 3) _doubleTimer = _upgradeDouble ? 12f : 8f;
                    break;
                case PowerUpKind.Magnet:
                    if (ActiveCount() < 3) _magnetTimer = 8f;
                    break;
                case PowerUpKind.Phase:
                    if (ActiveCount() < 3) _phaseTimer = 6f;
                    break;
                case PowerUpKind.Freeze:
                    if (ActiveCount() < 3) _freezeTimer = 4f;
                    break;
                case PowerUpKind.Revive:
                    _reviveAvailable = true;
                    break;
            }
        }

        private void RewardSkill(int basePoints)
        {
            ChargeResonance(0.08f);
            if (_eventCooldown > 0f) return;
            _eventCooldown = 0.45f;
            BumpCombo();
            AddPoints(basePoints);
        }

        private void ChargeResonance(float amount)
        {
            Resonance = Math.Min(1f, Resonance + amount);
            if (Resonance >= 1f) TriggerResonance();
        }

        private void TriggerResonance()
        {
            Resonance = 0f;
            ResonanceFlash = 1f;
            ResonanceCount++;
            AddPoints(80);
            int cleared = 0;
            for (int i = 0; i < Asteroids.Count; i++)
            {
                OrbitThing a = Asteroids[i];
                if (a.Dead || a.Radius > 1.45f || a.Kind == (int)AsteroidKind.Bolt) continue;
                Burst((float)Math.Cos(a.Angle) * a.Radius, (float)Math.Sin(a.Angle) * a.Radius, 12, 1, 0.9f);
                a.Dead = true;
                Asteroids[i] = a;
                cleared++;
            }
            if (cleared > 0) AddPoints(cleared * 12);
            AddEvent(GameEventKind.Resonance);
        }

        private void BumpCombo()
        {
            Combo++;
            _comboTime = 2.0f;
            Multiplier = Math.Min(5, 1 + Combo / 5);
            Fever = Multiplier >= 4;
            if (Combo > MaxCombo) { MaxCombo = Combo; Stats.maxCombo = Combo; }
        }

        private void AddPoints(int basePoints)
        {
            int factor = _doubleTimer > 0f ? 2 : 1;
            int boosted = Math.Max(1, (int)(basePoints * _scoreBoost + 0.5f));
            int gained = boosted * Multiplier * factor;
            Score = Math.Min(int.MaxValue / 4, Score + gained);
            Stats.score = Score;
            int next = 1 + Score / 150;
            if (next > Level)
            {
                Level = Math.Min(Level + 1, next);
                Stats.level = Level;
                _levelUpFlashValue = 1f;
                LevelUpFlash = 1f;
                AddEvent(GameEventKind.LevelUp, Level);
            }
            if (Score > Best)
            {
                Best = Score;
                if (!IsNewRecord)
                {
                    IsNewRecord = true;
                    if (OnNewRecord != null) OnNewRecord(Score);
                }
            }
        }

        private void AddBlockPoints(int basePoints)
        {
            int boosted = Math.Max(1, (int)(basePoints * _scoreBoost + 0.5f));
            int gained = boosted * Multiplier * (_doubleTimer > 0f ? 2 : 1);
            Score = Math.Min(int.MaxValue / 4, Score + gained);
            Stats.score = Score;
            if (Score > Best)
            {
                Best = Score;
                if (!IsNewRecord)
                {
                    IsNewRecord = true;
                    if (OnNewRecord != null) OnNewRecord(Score);
                }
            }
        }

        private void ApplyBlockStep(BlockStep step)
        {
            if (step.Dropped > 0) AddBlockPoints(step.Dropped * 2);
            if (step.Cleared > 0)
            {
                int count = Clamp(step.Cleared, 1, 4);
                Stats.orbs += step.Cleared;
                OrbsCollected += step.Cleared;
                Combo++;
                if (Combo > MaxCombo) { MaxCombo = Combo; Stats.maxCombo = Combo; }
                Multiplier = Math.Min(5, 1 + Combo / 3);
                Fever = Multiplier >= 4;
                int[] table = { 0, 100, 300, 500, 800 };
                AddBlockPoints(table[count] * Blocks.Level + (count == 4 ? 200 : 0));
                _flash = Math.Max(_flash, 0.55f);
                if (!_reducedMotion) _shake = 0.35f;
                AddEvent(GameEventKind.BlockClear, step.Cleared);
            }
            else if (step.Locked)
            {
                Combo = 0;
                Multiplier = 1;
                Fever = false;
            }
            Level = Blocks.Level;
            Stats.level = Level;
            if (step.ToppedOut) BlockOver();
        }

        private void BlockOver()
        {
            if (Status == GameStatus.GameOver) return;
            Status = GameStatus.GameOver;
            Completed = false;
            Paused = false;
            Draft = null;
            Stats.seconds = (int)_time;
            Stats.score = Score;
            Stats.level = Level;
            _flash = 1f;
            if (!_reducedMotion) _shake = 0.7f;
            Burst(0f, 0f, 30, 9, 1.2f);
            if (OnRunEnd != null) OnRunEnd(Stats);
            AddEvent(GameEventKind.BlockTopOut);
        }

        private OrbitThing HandleHit(OrbitThing a)
        {
            Stats.hits++;
            Combo = 0;
            Multiplier = 1;
            _comboTime = 0f;
            Fever = false;
            ShieldTime = 0f;
            if (_phaseTimer > 0f)
            {
                a.Dead = true;
                Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 12, 6, 0.8f);
            }
            else if (Shield)
            {
                Shield = false;
                _invulnerable = 1.6f;
                _shieldFlash = 1f;
                if (!_reducedMotion) _shake = 0.9f;
                _hitStop = 0.06f;
                Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 24, 4, 1.1f);
                AddEvent(GameEventKind.ShieldBreak);
            }
            else if (_reviveAvailable)
            {
                _reviveAvailable = false;
                _invulnerable = 2.4f;
                _shieldFlash = 1f;
                Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 40, 10, 1.6f);
                AddEvent(GameEventKind.PowerUp, NativePowerUpIndex(PowerUpKind.Revive));
            }
            else if (Mode == ModeKind.Sprint)
            {
                _timeLeft = Math.Max(0f, _timeLeft - 3f);
                _flash = 1f;
                _invulnerable = 1.2f;
                if (!_reducedMotion) _shake = 0.7f;
                Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 20, 2, 1f);
                AddEvent(GameEventKind.Crash);
                if (_timeLeft <= 0f) FinishRun();
            }
            else if (Mode == ModeKind.Zen)
            {
                // Zen hits are harmless apart from the combo/shield reset above.
            }
            else
            {
                Crash();
            }
            return a;
        }

        private void Crash()
        {
            Status = GameStatus.GameOver;
            Paused = false;
            if (!_reducedMotion) _shake = 1f;
            _hitStop = 0.09f;
            Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 34, 0, 1.4f);
            Burst((float)Math.Cos(PlayerAngle), (float)Math.Sin(PlayerAngle), 14, 2, 0.9f);
            if (OnRunEnd != null) OnRunEnd(Stats);
            AddEvent(GameEventKind.Crash);
        }

        // ---------------- Spawning ----------------

        private void SpawnAsteroid()
        {
            SectorDef sector = Content.Sector(SectorId);
            bool comet = Level >= 2 && sector.Has(HazardKind.Comet) && Rnd() < 0.18f;
            Asteroids.Add(MakeAsteroid(NextHazardAngle(), comet ? (int)AsteroidKind.Comet : (int)AsteroidKind.Rock));
        }

        private void SpawnStorm()
        {
            float safe = Rnd() * Tau;
            int count = (Mode == ModeKind.Survival ? 6 : 5) + (_seasonDensity > 1.1f ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                float angle = NextHazardAngle();
                int tries = 0;
                while (Math.Abs(AngDiff(angle, safe)) < 0.8f && tries < 24)
                {
                    angle = NextHazardAngle();
                    tries++;
                }
                Asteroids.Add(MakeAsteroid(angle, (int)AsteroidKind.Rock));
            }
        }

        private void SpawnFlare()
        {
            for (int i = 0; i < 14; i++) Asteroids.Add(MakeBolt(NextHazardAngle()));
        }

        private void SpawnBoss()
        {
            int kind = (int)(((long)(Level / 5)) % Content.BossNames.Length);
            var boss = new OrbitThing
            {
                Angle = NextHazardAngle(),
                Hp = 2 + Level / 20 + kind,
                Duration = 26f,
                Kind = kind,
                Active = true
            };
            Bosses.Add(boss);
            for (int i = 0; i < 2; i++)
                Orbs.Add(MakeOrb(Norm(boss.Angle + (Rnd() - 0.5f) * 0.55f), 0));
            AddEvent(GameEventKind.BossSpawn);
            Burst((float)Math.Cos(boss.Angle), (float)Math.Sin(boss.Angle), 40, 8, 1.5f);
        }

        private OrbitThing MakeOrb(float angle, int kind)
        {
            return new OrbitThing { Angle = angle, Radius = 1f, Kind = kind, Sprite = kind, Active = true };
        }

        private void SpawnOrb()
        {
            float v = Rnd();
            int kind = v < 0.04f ? 2 : (v < 0.20f ? 1 : 0);
            float angle = Norm(_orbSpawnCursor + (Rnd() - 0.5f) * 0.35f);
            _orbSpawnCursor = Norm(_orbSpawnCursor + 2.3999632f);
            Orbs.Add(MakeOrb(angle, kind));
        }

        private void SpawnPowerUp()
        {
            PowerUpKind[] all =
            {
                PowerUpKind.Shield, PowerUpKind.Slow, PowerUpKind.Double, PowerUpKind.Magnet,
                PowerUpKind.Phase, PowerUpKind.Freeze, PowerUpKind.Revive
            };
            PowerUpKind[] kinds = all;
            if (Mode == ModeKind.Zen)
            {
                kinds = new[] { PowerUpKind.Slow, PowerUpKind.Double, PowerUpKind.Magnet,
                                PowerUpKind.Phase, PowerUpKind.Freeze };
            }
            // Native evaluates PowerUp(angle = rnd() * TAU, kind = kinds[rnd() ...]).
            float angle = Rnd() * Tau;
            int at = (int)(Rnd() * kinds.Length);
            if (at < 0) at = 0;
            if (at >= kinds.Length) at = kinds.Length - 1;
            // PowerUp.Kind carries the native ordinal (Shield..Revive = 0..6), not the
            // legacy Unity enum ordinal.
            PowerUps.Add(new OrbitThing { Angle = angle, Kind = NativePowerUpIndex(kinds[at]), Active = true });
        }

        private void SpawnMine()
        {
            OrbitThing a = MakeAsteroid(NextHazardAngle(), (int)AsteroidKind.Mine);
            a.Speed *= 0.35f;
            a.Life = 16f;
            Asteroids.Add(a);
        }

        private void SpawnWormholes()
        {
            float a = Rnd() * Tau;
            Wormholes.Clear();
            Wormholes.Add(new OrbitThing { Angle = a, Partner = Norm(a + (float)Math.PI), Active = true });
            Wormholes.Add(new OrbitThing { Angle = Norm(a + (float)Math.PI), Partner = a, Active = true });
        }

        private OrbitThing MakeAsteroid(float angle, int kind)
        {
            float baseRadius = 0.026f / 0.31f + Rnd() * (0.018f / 0.31f);
            float baseSpeed = (0.30f + _speedLevel * 0.25f) / 0.31f * (0.85f + Rnd() * 0.3f);
            bool comet = kind == (int)AsteroidKind.Comet;
            float size = comet ? baseRadius * 0.7f : (kind == (int)AsteroidKind.Mine ? baseRadius * 1.15f : baseRadius);
            var a = new OrbitThing
            {
                Angle = angle,
                Radius = _spawnDistance + baseRadius,
                Speed = comet ? baseSpeed * 1.9f : baseSpeed,
                Size = size,
                Kind = kind,
                Sprite = (int)(Rnd() * 8f),
                Rotation = Rnd() * Tau,
                Spin = (Rnd() - 0.5f) * 1.6f,
                Life = 6f,
                Active = true
            };
            if (a.Sprite < 0) a.Sprite = 0;
            if (a.Sprite > 7) a.Sprite = 7;
            return a;
        }

        private OrbitThing MakeBolt(float angle)
        {
            return new OrbitThing
            {
                Angle = angle,
                Radius = 2.6f,
                Speed = 0.55f / 0.31f,
                Size = 0.010f / 0.31f,
                Kind = (int)AsteroidKind.Bolt,
                Life = 5f,
                Active = true
            };
        }

        // ---------------- Visual helpers ----------------

        private void DecayVisuals(float step)
        {
            _shake = Math.Max(0f, _shake - step * 2.4f);
            _reverseFlash = Math.Max(0f, _reverseFlash - step * 3.5f);
            _flash = Math.Max(0f, _flash - step * 3.2f);
            _levelUpFlashValue = Math.Max(0f, _levelUpFlashValue - step * 0.9f);
            LevelUpFlash = _levelUpFlashValue;
            _shieldFlash = Math.Max(0f, _shieldFlash - step * 2.5f);
            ResonanceFlash = Math.Max(0f, ResonanceFlash - step * 1.8f);
        }

        private void Burst(float x, float y, int count, int paletteIndex, float power)
        {
            int n = _reducedMotion ? Math.Max(4, count / 3) : count;
            for (int i = 0; i < n; i++)
            {
                float angle = Rnd() * Tau;
                float velocity = (0.05f + Rnd() * 0.18f) * power;
                float life = 0.35f + Rnd() * 0.55f;
                Particles.Add(new OrbitThing
                {
                    X = x, Y = y,
                    Vx = (float)Math.Cos(angle) * velocity,
                    Vy = (float)Math.Sin(angle) * velocity,
                    Life = life, MaxLife = life,
                    Size = 0.002f + Rnd() * 0.004f,
                    Spin = (Rnd() - 0.5f) * 6f,
                    Kind = paletteIndex,
                    Power = power,
                    Active = true
                });
            }
            while (Particles.Count > 420) Particles.RemoveAt(0);
        }

        private void UpdateParticles(float dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                OrbitThing p = Particles[i];
                p.Life -= dt;
                if (p.Life <= 0f) { Particles.RemoveAt(i); continue; }
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                p.Vx *= 0.96f;
                p.Vy *= 0.96f;
                Particles[i] = p;
            }
        }

        // ---------------- Math / RNG / events ----------------

        private float Rnd()
        {
            unchecked
            {
                _rngState = _rngState * 6364136223846793005L + 1442695040888963407L;
                // Kotlin uses Long.ushr(33); C# >> would sign-extend a negative state.
                ulong bits = ((ulong)_rngState >> 33) & 0xFFFFFFUL;
                return (float)(bits / 16777216.0);
            }
        }

        /// <summary>Native PowerUpKind ordinal: Shield, Slow, Double, Magnet, Phase, Freeze, Revive.</summary>
        private static int NativePowerUpIndex(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Shield: return 0;
                case PowerUpKind.Slow: return 1;
                case PowerUpKind.Double: return 2;
                case PowerUpKind.Magnet: return 3;
                case PowerUpKind.Phase: return 4;
                case PowerUpKind.Freeze: return 5;
                default: return 6;
            }
        }

        private static PowerUpKind PowerUpFromNativeIndex(int index)
        {
            switch (index)
            {
                case 0: return PowerUpKind.Shield;
                case 1: return PowerUpKind.Slow;
                case 2: return PowerUpKind.Double;
                case 3: return PowerUpKind.Magnet;
                case 4: return PowerUpKind.Phase;
                case 5: return PowerUpKind.Freeze;
                default: return PowerUpKind.Revive;
            }
        }

        private static float AngDiff(float a, float b)
        {
            float d = (a - b) % Tau;
            if (d > (float)Math.PI) d -= Tau;
            if (d < -(float)Math.PI) d += Tau;
            return d;
        }

        private static float Norm(float a)
        {
            float v = a % Tau;
            if (v < 0f) v += Tau;
            return v;
        }

        private float NextHazardAngle()
        {
            float a = Norm(_hazardSpawnCursor + (Rnd() - 0.5f) * 0.3f);
            _hazardSpawnCursor = Norm(_hazardSpawnCursor + 2.3999632f);
            return a;
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2;
            float dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static void RemoveDead(List<OrbitThing> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Dead) list.RemoveAt(i);
        }

        private void AddEvent(GameEventKind kind, int value = 0)
        {
            var ev = new GameEvent(kind, value);
            Events.Add(ev);
            if (Events.Count > MaxEvents) Events.RemoveAt(0);
            if (OnEvent != null) OnEvent(ev);
        }

        private static ModeDef ModeInfo(ModeKind kind)
        {
            return Content.Mode(kind);
        }
    }

    internal enum AsteroidKind
    {
        Rock = 0,
        Comet = 1,
        Mine = 2,
        Bolt = 3
    }

    internal enum OrbKind
    {
        Normal = 0,
        Golden = 1,
        Artifact = 2
    }
}
