using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Sole owner of a Galaxy match. Every step runs in a fixed order (input, AI, abilities,
    /// combat, growth, world, events, leaderboard, result) so a seed always replays identically.
    /// Pure C#: no Unity and no Newtonsoft references.
    /// </summary>
    public sealed class MatchSimulation
    {
        private const int MaxHighlights = 64;
        private const int MaxSpawnAttempts = 48;
        private const float FullCircle = 6.2831853f;

        private static readonly Comparison<LeaderboardEntry> EntryComparator = CompareEntries;

        public GameModeKind Mode;
        public ModeSettings Settings;
        public Rng Rng;
        public int Seed;
        public Arena Arena;
        public WorldEventSystem Events;
        public CombatSystem Combat;
        public BossController Bosses;
        public SnakeSystem Snakes;
        public SimLog Log;
        public List<Ship> Ships;
        public List<BotBrain> Bots;
        public Ship Player;
        public float Time;
        public bool Finished;
        public MatchResult Result;
        public List<LeaderboardEntry> Leaderboard;
        public int PlayerRank;

        // Extra surfaces used by the runtime, AI and WorldEventSystem.
        public AccountProfile Profile;
        public List<EvolutionDef> PendingEvolutionChoices;
        public bool IsEvolutionPaused { get { return PendingEvolutionChoices.Count > 0; } }
        public float RemainingTime { get { return Math.Max(0f, Settings.DurationSeconds - Time); } }

        private readonly List<Ship> _participants = new List<Ship>(GameConfig.MaxParticipants + 8);
        private readonly List<WorldObject> _spawnScratch = new List<WorldObject>(64);
        private readonly List<SnakeSegmentRef> _spawnSegments = new List<SnakeSegmentRef>(32);
        private readonly List<SimEvent> _highlights = new List<SimEvent>(MaxHighlights);
        private readonly List<Ship> _newBosses = new List<Ship>(8);
        private readonly Dictionary<int, int> _previousKills = new Dictionary<int, int>(64);
        private readonly Dictionary<int, int> _previousDeaths = new Dictionary<int, int>(64);
        private readonly HashSet<int> _countedBossDeaths = new HashSet<int>();
        private readonly HashSet<string> _usedNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _namePool = new List<string>(48);

        private Rng _nameRng;
        private Rng _spawnRng;
        private Rng _aiRng;
        private Rng _dropRng;
        private Vec2 _playerMove;
        private Vec2 _playerAim = Vec2.Up;
        private bool _playerBoost;
        private bool _dashQueued;
        private bool _abilityQueued;
        private bool _fireQueued;
        private bool _evolutionOfferActive;
        private bool _initialBossSpawned;
        private WorldEventKind? _lastActiveEvent;
        private WorldEventKind? _lastPendingEvent;
        private int _nextShipId = 1;
        private int _bossKills;
        private int _snakeAbilityUses;
        private int _archetypeOffset;
        private float _playerSurvivalTime = -1f;

        public MatchSimulation(GameModeKind mode, int seed, Difficulty difficulty, AccountProfile profile)
            : this(mode, seed, difficulty, profile, -1)
        {
        }

        /// <summary>Optional stress-test overload: forces an exact participant count in 20..35.</summary>
        public MatchSimulation(GameModeKind mode, int seed, Difficulty difficulty, AccountProfile profile, int participants)
        {
            Mode = mode;
            Seed = seed;
            Profile = profile;
            Settings = new ModeSettings(mode, difficulty);
            if (participants > 0)
                Settings.Participants = MathGalaxy.Clamp(participants, GameConfig.MinParticipants, GameConfig.MaxParticipants);
            Rng = new Rng(MixSeed(seed));
            Log = new SimLog();
            Arena = new Arena(mode, seed, Settings.DangerScale);
            Arena.Initialize();
            Combat = new CombatSystem(Rng.Fork());
            Bosses = new BossController(Rng.Fork());
            Snakes = new SnakeSystem(Rng.Fork());
            Events = new WorldEventSystem(Arena, Rng.Fork(), Settings.ChaosEvents);
            Ships = new List<Ship>(GameConfig.MaxParticipants + 16);
            Bots = new List<BotBrain>(GameConfig.MaxParticipants);
            Leaderboard = new List<LeaderboardEntry>(GameConfig.MaxParticipants + 16);
            PendingEvolutionChoices = new List<EvolutionDef>(3);

            _nameRng = Rng.Fork();
            _spawnRng = Rng.Fork();
            _aiRng = Rng.Fork();
            _dropRng = Rng.Fork();
            _archetypeOffset = _aiRng.NextInt(0, BotArchetypeCatalog.Count);
            BuildNamePool();
            SpawnParticipants();
            Snakes.Initialize(Ships);
            UpdateLeaderboard();
        }

        // Deterministic seed mixing, identical on every runtime.
        private static uint SeedSalt()
        {
            return 0x51ED270Bu;
        }

        private static uint SeedSaltOverflow()
        {
            return 0x85EBCA6Bu;
        }

        private static uint HexSalt()
        {
            return 0x9E3779B9u;
        }

        private static uint MixSeed(int seed)
        {
            unchecked
            {
                uint value = (uint)seed ^ SeedSalt();
                value ^= value >> 15;
                value *= SeedSaltOverflow();
                return value ^ HexSalt();
            }
        }

        public void Update(float dt)
        {
            if (Finished || dt <= 0f) return;
            // While the player chooses an evolution the whole arena is frozen: no bot moves,
            // no damage is applied and the match timer does not advance.
            if (PendingEvolutionChoices != null && PendingEvolutionChoices.Count > 0) return;
            dt = MathGalaxy.Clamp(dt, 0f, GameConfig.MaxFrameDelta);
            Time += dt;

            EnsureParticipants();

            // 1. Input.
            ApplyPlayerInput(dt);

            // 2. AI (bots think on LOD but steer every step).
            for (int i = 0; i < Bots.Count; i++) Bots[i].Update(dt, this, Log);

            // 3. Abilities and boss behaviour.
            AbilitySystem.Update(dt, Ships, Arena, Combat.Projectiles, Log, Time);
            if (Bosses != null) Bosses.Update(dt, Ships, Arena, Log, Time, Combat);
            SpawnModeBosses();

            // 3b. Snake heads turn before combat/fire so the hull points where it is going.
            if (Snakes != null) Snakes.Steer(dt);

            // 4. Combat, contacts and hazards.
            Combat.Update(dt, Ships, Arena, Log, Time, Profile);

            // 5. Growth, pickups and movement.
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship == null) continue;
                HandleRespawn(ship, dt);
                if (ship.State == ShipState.Alive)
                {
                    CollectPickups(ship);
                    IntegrateMovement(ship, dt);
                    if (Arena != null) Arena.DamageBorder(ship, dt, Log, Time);
                }
                else
                {
                    ship.MoveInput = Vec2.Zero;
                    ship.Velocity = Vec2.Zero;
                }
            }

            // 5b. Tail samples the final head path; this must happen after movement, never before.
            if (Snakes != null)
            {
                Snakes.Follow(dt);
                Snakes.ResolveCollisions(Arena, Log, Time);
            }

            // 5c. Border damage bypasses CombatSystem.ApplyDamage. Resolve every zero-HP ship here
            // so no damage source can leave a ship Alive with no health.
            ResolveDeaths();
            ResolveDeathDrops();

            // 6. World and 7. events.
            if (Arena != null) Arena.Update(dt, Log, Time);
            if (Events != null) Events.Update(dt, this, Log);

            // 8. Leaderboard and kill rewards.
            ReconcileKills();
            UpdateLeaderboard();
            OfferPlayerEvolution();
            TrackHighlights();

            // 9. Result.
            CheckFinish();
        }

        public void PlayerMove(Vec2 direction)
        {
            _playerMove = direction.SqrMagnitude > 1f ? direction.Normalized : direction;
        }

        public void PlayerAim(Vec2 direction)
        {
            if (direction.SqrMagnitude > 0.0001f) _playerAim = direction.Normalized;
        }

        public void PlayerBoost(bool enabled)
        {
            _playerBoost = enabled;
        }

        public void PlayerActivateAbility()
        {
            _abilityQueued = true;
        }

        public void PlayerDash()
        {
            _dashQueued = true;
        }

        /// <summary>Ручной выстрел. При AutoFire вызывать не обязательно.</summary>
        public void PlayerFire()
        {
            _fireQueued = true;
        }

        public void PlayerChooseEvolution(EvolutionDef def)
        {
            if (Finished) return;
            if (def == null && PendingEvolutionChoices.Count > 0) def = PendingEvolutionChoices[0];
            if (def == null)
            {
                _evolutionOfferActive = false;
                return;
            }
            bool valid = false;
            for (int i = 0; i < PendingEvolutionChoices.Count; i++)
            {
                if (PendingEvolutionChoices[i] == def) { valid = true; break; }
            }
            if (!valid && PendingEvolutionChoices.Count > 0) return;

            if (SnakeEvolutionSystem.IsSnakeOffer(def))
            {
                if (SnakeEvolutionSystem.Choose(Player, def, Log, Time))
                {
                    AddHighlight(new SimEvent(SimEventKind.Evolution, Player.Id, def.Tier,
                        "РОСТ ЗМЕЙКИ: " + def.Name, Player.Position, Time, Player.Level));
                }
            }
            else if (EvolutionSystem.IsAvailable(Player, def))
            {
                EvolutionSystem.Choose(Player, def, Log, Time, Profile);
                Player.Remember("Эволюция: " + def.Name);
                AddHighlight(new SimEvent(SimEventKind.Evolution, Player.Id, def.Tier,
                    "ЭВОЛЮЦИЯ: " + def.Name, Player.Position, Time, Player.Level));
            }
            PendingEvolutionChoices.Clear();
            _evolutionOfferActive = false;
        }

        public void Finish(bool voluntary)
        {
            if (Finished) return;
            Finished = true;
            UpdateLeaderboard();
            PlayerRank = Player == null ? Leaderboard.Count : Player.Rank;

            bool survived = Player != null && Player.State != ShipState.Dead &&
                            (Player.State == ShipState.Alive || CanRespawn(Player));
            bool victory = !voluntary && survived &&
                           (PlayerRank == 1 ||
                            (Settings.EndsOnPlayerDeath && Time >= Settings.DurationSeconds && PlayerRank <= 3));
            if (victory && Player != null)
            {
                Player.Score += 750f;
                UpdateLeaderboard();
                PlayerRank = Player.Rank;
            }

            Result = new MatchResult();
            Result.Mode = Mode;
            Result.Duration = Time;
            Result.Rank = PlayerRank;
            Result.Participants = _participants.Count;
            Result.Victory = victory;
            if (Player != null)
            {
                Result.Score = Player.Score;
                Result.Mass = Player.Mass;
                Result.Length = Player.Body == null ? 0 : Player.Body.Count;
                Result.SurvivalTime = _playerSurvivalTime >= 0f ? _playerSurvivalTime : Time;
                Result.SnakeEvolutionTier = Player.SnakeEvolutionTier;
                Result.SnakeAbilityUses = _snakeAbilityUses;
                Result.Level = (int)Player.Level;
                Result.Kills = Player.Kills;
                Result.Deaths = Player.Deaths;
                Result.EnergyCollected = Player.EnergyCollected;
                Result.Crystals = Player.Crystals;
                Result.DarkMatter = Player.DarkMatter;
                Result.Artifacts = Player.Artifacts;
                Result.BestStreak = Player.BestStreak;
            }
            Result.BossKills = _bossKills;
            Result.Leaderboard = new List<LeaderboardEntry>(Leaderboard);

            float economy = 1f;
            if (Profile != null)
            {
                float bonus = Profile.ResearchBonus("res_economy");
                if (bonus > 0f) economy += bonus;
            }
            float rankBonus = Math.Max(0f, (Result.Participants - PlayerRank + 1)) * 22f;
            float rewardScale = Math.Max(0.1f, Settings.RewardScale);
            Result.Credits = (int)Math.Max(0f, Math.Round((85f + Result.Score * 0.11f +
                Result.Kills * 26f + Result.BestStreak * 14f + _bossKills * 190f + rankBonus)
                * rewardScale * economy));
            Result.CrystalsReward = (int)Math.Max(0f, Math.Round((Result.Crystals * 0.55f +
                Result.Artifacts * 1.5f + _bossKills * 2.5f + rankBonus * 0.06f) * rewardScale));
            Result.Xp = (int)Math.Max(0f, Math.Round((Result.Score * 0.28f +
                Result.EnergyCollected * 0.09f + Result.Kills * 20f + _bossKills * 90f) * rewardScale));
            Result.AccountXp = (int)Math.Max(0f, Math.Round((Result.Xp * 0.55f +
                Time * 0.6f + (victory ? 140f : 0f)) * rewardScale));

            SimEvent end = new SimEvent(SimEventKind.MatchEnd, Player == null ? -1 : Player.Id,
                PlayerRank, "МАТЧ ЗАВЕРШЁН", Player == null ? Vec2.Zero : Player.Position, Time, Result.Score);
            if (Log != null) Log.Add(SimEventKind.MatchEnd, end.ActorId, end.Value, end.Text, end.Position, Time, end.FloatValue);
            AddHighlight(end);
            AddHighlight(new SimEvent(victory ? SimEventKind.Victory : SimEventKind.Defeat,
                end.ActorId, PlayerRank, victory ? "ПОБЕДА!" : "ПОРАЖЕНИЕ", end.Position, Time));
            Result.Highlights = new List<SimEvent>(_highlights);
        }

        public Ship FindShipById(int id)
        {
            if (id <= 0) return null;
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship != null && ship.Id == id) return ship;
            }
            return null;
        }

        public bool IsPlayerNear(Vec2 point, float radius)
        {
            if (Player == null || Player.State != ShipState.Alive) return false;
            return Vec2.DistanceSqr(Player.Position, point) <= radius * radius;
        }

        // ───────────────────────── Spawning ─────────────────────────

        private void BuildNamePool()
        {
            _namePool.Clear();
            string[] source = ContentDatabase.BotNames;
            for (int i = 0; i < source.Length; i++) _namePool.Add(source[i]);
            for (int i = _namePool.Count - 1; i > 0; i--)
            {
                int j = _nameRng.NextInt(0, i + 1);
                string tmp = _namePool[i];
                _namePool[i] = _namePool[j];
                _namePool[j] = tmp;
            }
        }

        private void SpawnParticipants()
        {
            Vec2 playerPosition = FindSpawnPoint(GameConfig.BaseRadius);
            Player = CreateShip("ИГРОК", true, false, BotPersonality.Aggressive, playerPosition, Profile);
            Player.ActiveAbility = AbilityKind.Dash;
            Player.Remember("Выход в арену");
            Ships.Add(Player);
            _participants.Add(Player);
            _previousKills[Player.Id] = 0;
            _previousDeaths[Player.Id] = 0;

            int botCount = MathGalaxy.Clamp(Settings.Participants, GameConfig.MinParticipants,
                GameConfig.MaxParticipants) - 1;
            for (int i = 0; i < botCount; i++) SpawnBot(i);
        }

        private void SpawnBot(int index)
        {
            BotArchetype archetype = BotArchetypeCatalog.Pick(index + _archetypeOffset, Settings.Difficulty);
            BotPersonality personality = archetype.Personality;
            BotProfile profile = archetype.Profile;
            Vec2 position = FindSpawnPoint(GameConfig.BaseRadius);
            Ship ship = CreateShip(UniqueBotName(index), false, true, personality, position, null);
            // Shared profiles are read-only during a match, so the archetype profile must be
            // installed on the ship as well: utility scoring reads Ship.Profile.
            ship.Profile = profile;
            // Visible size variety from the first second. This is size, not difficulty: bigger
            // snakes are stronger but slower through the existing mass curve.
            ship.AddMass(GameConfig.BaseMass * _aiRng.Range(-0.16f, 0.42f), null, 0f);
            Ships.Add(ship);
            _participants.Add(ship);
            _previousKills[ship.Id] = 0;
            _previousDeaths[ship.Id] = 0;
            if (Snakes != null) Snakes.Add(ship);

            Rng brainRng = _aiRng.Fork();
            BotBrain brain = new BotBrain(ship, profile, brainRng, Settings.Difficulty, archetype);
            Bots.Add(brain);
            ship.Remember("Выход в арену: " + archetype.Label);
        }

        private Ship CreateShip(string name, bool isPlayer, bool isBot, BotPersonality personality,
            Vec2 position, AccountProfile account)
        {
            Ship ship = isPlayer
                ? new Ship(_nextShipId++, name, true, false, personality, position, account)
                : new Ship(_nextShipId++, name, false, true, personality, position);
            if (isBot) ship.Profile = ContentDatabase.FindBotProfile(personality);

            SnakeCosmeticDef snakeHead = isPlayer && account != null
                ? SnakeCosmeticCatalog.FindHead(account.SelectedShip)
                : null;
            ShipVisualDef visual = null;
            if (isPlayer && account != null && !string.IsNullOrEmpty(account.SelectedShip))
                visual = ContentDatabase.FindShipVisual(account.SelectedShip);
            if (visual == null && snakeHead == null)
            {
                ShipVisualDef[] visuals = ContentDatabase.ShipVisuals;
                visual = visuals[_aiRng.NextInt(0, visuals.Length)];
            }
            if (snakeHead != null)
            {
                ship.Hull = snakeHead.Hull;
                ship.PrimaryHex = snakeHead.PrimaryHex;
                ship.AccentHex = snakeHead.AccentHex;
            }
            else if (visual != null)
            {
                ship.Hull = visual.Hull;
                ship.PrimaryHex = visual.PrimaryHex;
                ship.AccentHex = visual.AccentHex;
            }
            SnakeCosmeticDef snakeTail = isPlayer && account != null
                ? SnakeCosmeticCatalog.FindTail(account.SelectedTrail)
                : null;
            ship.TailStyle = snakeTail == null ? SnakeTailStyle.Glow : snakeTail.Style;
            ship.Facing = Vec2.FromAngle(_spawnRng.Range(0f, FullCircle));
            ship.AimInput = ship.Facing;
            ship.MoveInput = Vec2.Zero;
            ship.LastKillTime = -1000f;
            ship.RecalculateStats(account);
            return ship;
        }

        private string UniqueBotName(int index)
        {
            if (_namePool.Count > 0)
            {
                for (int attempt = 0; attempt < _namePool.Count; attempt++)
                {
                    string candidate = _namePool[(index + attempt) % _namePool.Count];
                    if (_usedNames.Add(candidate)) return candidate;
                }
            }
            string fallback = "ПИЛОТ-" + (index + 1).ToString();
            while (!_usedNames.Add(fallback)) fallback = "ПИЛОТ-" + (++index + 1).ToString();
            return fallback;
        }

        private Vec2 FindSpawnPoint(float radius)
        {
            float limit = Arena == null ? GameConfig.SafeRadiusStart * 0.85f : Arena.SafeRadius * 0.82f;
            if (limit < 1f) limit = 1f;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                Vec2 candidate;
                if (attempt < MaxSpawnAttempts - 8)
                {
                    candidate = Vec2.FromAngle(_spawnRng.Range(0f, FullCircle)) *
                                _spawnRng.Range(0f, limit);
                }
                else
                {
                    // Deterministic spiral before the exhaustive grid fallback.
                    float angle = attempt * 2.399963f;
                    float ring = radius * 3f + (attempt - MaxSpawnAttempts + 8) * 95f;
                    candidate = Vec2.FromAngle(angle) * ring;
                    if (candidate.Magnitude > limit) candidate = candidate.Normalized * limit;
                }
                if (IsFreeSpawn(candidate, radius)) return candidate;
            }

            // Exhaustive deterministic scan: never fall back to a blindly chosen occupied point.
            Vec2 best = Vec2.Zero;
            float bestClearance = float.NegativeInfinity;
            for (int ring = 0; ring < 10; ring++)
            {
                float ringRadius = limit * (ring + 1f) / 10f;
                int slices = 12 + ring * 6;
                for (int slice = 0; slice < slices; slice++)
                {
                    Vec2 candidate = Vec2.FromAngle(slice * FullCircle / slices) * ringRadius;
                    float clearance = SpawnClearance(candidate, radius);
                    if (clearance > bestClearance)
                    {
                        bestClearance = clearance;
                        best = candidate;
                    }
                    if (clearance >= 0f) return candidate;
                }
            }
            return best;
        }

        private bool IsFreeSpawn(Vec2 point, float radius)
        {
            return SpawnClearance(point, radius) >= 0f;
        }

        /// <summary>
        /// Minimum free gap around a spawn point. A direct object scan is used because hazard and
        /// anomaly centres can lie far outside a broadphase query while their area still overlaps.
        /// </summary>
        private float SpawnClearance(Vec2 point, float radius)
        {
            float clearance = float.MaxValue;
            if (Arena != null)
            {
                float borderLimit = Arena.SafeRadius - radius - 140f;
                clearance = Math.Min(clearance, borderLimit - point.Magnitude);
                List<WorldObject> objects = Arena.Objects;
                if (objects != null)
                {
                    for (int i = 0; i < objects.Count; i++)
                    {
                        WorldObject obj = objects[i];
                        if (obj == null || !obj.Alive) continue;
                        float reach = radius + obj.Radius + 42f;
                        clearance = Math.Min(clearance, Vec2.Distance(point, obj.Position) - reach);
                    }
                }
            }
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship other = Ships[i];
                if (other == null || other.State != ShipState.Alive) continue;
                float reach = radius + other.Radius + 110f;
                clearance = Math.Min(clearance, Vec2.Distance(point, other.Position) - reach);
            }
            if (Snakes != null)
            {
                // A long tail spans far more than the head check above. The segment index is
                // already built, so this is a few cell lookups and prevents respawning inside a
                // snake body just as its shield ends.
                _spawnSegments.Clear();
                Snakes.QuerySegments(point, point, radius + 130f, _spawnSegments);
                for (int i = 0; i < _spawnSegments.Count; i++)
                {
                    SnakeSegmentRef segment = _spawnSegments[i];
                    if (segment.Owner == null || segment.Owner.State != ShipState.Alive) continue;
                    float reach = radius + segment.Radius + 90f;
                    clearance = Math.Min(clearance, Vec2.Distance(point, segment.Point) - reach);
                }
            }
            return clearance;
        }

        private void EnsureParticipants()
        {
            int target = MathGalaxy.Clamp(Settings.Participants, GameConfig.MinParticipants,
                GameConfig.MaxParticipants);
            while (_participants.Count < target)
            {
                SpawnBot(_participants.Count - 1);
            }
            while (_participants.Count > target)
            {
                int last = _participants.Count - 1;
                Ship removed = _participants[last];
                _participants.RemoveAt(last);
                if (last - 1 >= 0 && last - 1 < Bots.Count)
                {
                    Bots.RemoveAt(last - 1);
                }
                bool inShips = false;
                for (int i = 0; i < Ships.Count; i++)
                {
                    if (Ships[i] == removed) { inShips = true; break; }
                }
                if (inShips) Ships.Remove(removed);
                if (Snakes != null) Snakes.Remove(removed);
                removed.State = ShipState.Dead;
            }
        }

        private void SpawnModeBosses()
        {
            if (!Settings.BossesEnabled || _initialBossSpawned || Time < 12f) return;
            _initialBossSpawned = true;
            int count = Mode == GameModeKind.BossHunt ? 3 : (Settings.BossesEnabled ? 1 : 0);
            BossDef[] defs = ContentDatabase.Bosses;
            if (defs == null || defs.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                BossDef def = defs[_aiRng.NextInt(0, defs.Length)];
                Vec2 point = FindSpawnPoint(90f);
                Ship boss = Bosses.Spawn(def, point, this, Log, Time);
                if (boss != null)
                {
                    if (!Ships.Contains(boss)) Ships.Add(boss);
                    _newBosses.Add(boss);
                    _previousKills[boss.Id] = 0;
                    _previousDeaths[boss.Id] = 0;
                }
            }
        }

        // ───────────────────────── Per-step systems ─────────────────────────

        private static float TurnTowards(float current, float target, float maxStep)
        {
            float delta = target - current;
            while (delta > Math.PI) delta -= 2f * (float)Math.PI;
            while (delta < -Math.PI) delta += 2f * (float)Math.PI;
            if (delta > maxStep) delta = maxStep;
            if (delta < -maxStep) delta = -maxStep;
            return current + delta;
        }

        private void ApplyPlayerInput(float dt)
        {
            if (Player == null) return;
            if (Player.State != ShipState.Alive)
            {
                Player.MoveInput = Vec2.Zero;
                _dashQueued = false;
                _abilityQueued = false;
                _fireQueued = false;
                _playerBoost = false;
                return;
            }

            if (Player.Body != null)
            {
                // Snake control: stick/keyboard steers, aim pad is a fallback when no move input
                // is present. SnakeSystem.Steer performs the actual rate-limited rotation for all
                // snakes, including bots, so the head always points along the travelled path.
                Vec2 desired = _playerMove.SqrMagnitude > 0.0001f
                    ? _playerMove
                    : (_playerAim.SqrMagnitude > 0.0001f ? _playerAim : Player.Facing);
                Player.MoveInput = desired;
                if (_playerAim.SqrMagnitude > 0.0001f) Player.AimInput = _playerAim;
            }
            else
            {
                Player.MoveInput = _playerMove;
                if (_playerAim.SqrMagnitude > 0.0001f)
                {
                    Player.AimInput = _playerAim;
                    float maxTurn = Math.Max(0.01f, Player.TurnRate) * dt;
                    Player.Facing = Vec2.FromAngle(TurnTowards(Player.Facing.Angle(), _playerAim.Angle(), maxTurn));
                }
            }

            bool boost = _playerBoost && Player.Energy > 1f && Player.MoveInput.SqrMagnitude > 0.01f;
            if (boost)
            {
                Player.Energy = MathGalaxy.Clamp(Player.Energy - GameConfig.BoostEnergyDrain * dt,
                    0f, GameConfig.MaxEnergy);
            }
            _playerBoost = boost;

            if (_dashQueued && SnakeAbilities.CanActivate(Player, SnakeAbilitySlot.Impulse))
            {
                if (SnakeAbilities.Activate(Player, SnakeAbilitySlot.Impulse, Log, Time)) _snakeAbilityUses++;
            }
            if (_abilityQueued && SnakeAbilities.CanActivate(Player, SnakeAbilities.SlotFor(Player.ActiveAbility)))
            {
                if (SnakeAbilities.Activate(Player, SnakeAbilities.SlotFor(Player.ActiveAbility), Log, Time))
                    _snakeAbilityUses++;
            }

            bool autoFire = Profile != null && Profile.Settings != null && Profile.Settings.AutoFire;
            if (autoFire || _fireQueued) Combat.Fire(Player, Player.AimInput, Log, Time);
            _dashQueued = false;
            _abilityQueued = false;
            _fireQueued = false;
        }

        /// <summary>
        /// Central death resolution. Combat damage already calls CombatSystem.HandleKill directly;
        /// this pass covers the remaining sources (the shrinking border) and keeps a single
        /// authoritative death path for the whole simulation.
        /// </summary>
        private void ResolveDeaths()
        {
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship == null || ship.State != ShipState.Alive || ship.Hp > 0f) continue;
                CombatSystem.HandleKill(ship, null, Log, Time);
                if (ship.IsPlayer && _playerSurvivalTime < 0f) _playerSurvivalTime = Time;
                if (Arena != null) Arena.ForgetShip(ship.Id);
            }
        }

        /// <summary>
        /// Snake collisions already spawn a pile through DeathDropSystem; this sweep covers every
        /// other source (projectiles, contacts, hazards, border). The per-victim flag makes it a
        /// no-op for deaths that were already handled, so one death can never drop twice.
        /// </summary>
        private void ResolveDeathDrops()
        {
            if (Arena == null) return;
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship == null) continue;
                int previous;
                _previousDeaths.TryGetValue(ship.Id, out previous);
                if (ship.Deaths <= previous) continue;
                // Record the death step itself: waiting for the respawn would add the delay to
                // the survival stat even though the ship was already gone.
                if (ship.IsPlayer && _playerSurvivalTime < 0f) _playerSurvivalTime = Time;
                if (ship.IsBoss || ship.DeathDropsSpawned) continue;
                DeathDropSystem.Spawn(Arena, ship, Log, Time, _dropRng);
            }
        }

        private void HandleRespawn(Ship ship, float dt)
        {
            if (ship == null) return;
            if (ship.State == ShipState.Respawning &&
                ship.RespawnTimer - dt <= 0f && !CanRespawn(ship))
            {
                ship.State = ShipState.Dead;
                ship.RespawnTimer = 0f;
                ship.Hp = 0f;
                ship.Shield = 0f;
                ship.Velocity = Vec2.Zero;
                ship.MoveInput = Vec2.Zero;
                ship.LastAttackerId = -1;
                ship.DeathAttackerId = -1;
                if (ship.IsPlayer && _playerSurvivalTime < 0f) _playerSurvivalTime = Time;
                if (ship.IsPlayer && Log != null)
                {
                    Log.Add(SimEventKind.Warning, ship.Id, 0,
                        "Корабль уничтожен окончательно.", ship.Position, Time);
                }
                return;
            }

            ShipState before = ship.State;
            if (ship.State != ShipState.Dead) GrowthSystem.Update(ship, dt, Log, Time);
            if (before == ShipState.Respawning && ship.State == ShipState.Alive)
            {
                ship.Position = FindSpawnPoint(Math.Max(GameConfig.BaseRadius, ship.Radius));
                ship.Velocity = Vec2.Zero;
                ship.LastAttackerId = -1;
                ship.DeathAttackerId = -1;
                ship.DeathDropsSpawned = false;
                if (ship.Body != null && Snakes != null) Snakes.Reset(ship);
                BotBrain bot = FindBot(ship);
                if (bot != null) bot.OnRespawn();
                ship.Remember("Возвращение в арену");
            }
        }

        private BotBrain FindBot(Ship ship)
        {
            if (ship == null) return null;
            for (int i = 0; i < Bots.Count; i++)
                if (Bots[i] != null && Bots[i].Ship == ship) return Bots[i];
            return null;
        }

        /// <summary>
        /// Runtime bot-count control. The next Update adds or removes bots through the existing
        /// participant bookkeeping, so ranks, leaderboard and snake registry stay consistent.
        /// </summary>
        public void SetParticipantCount(int count)
        {
            Settings.Participants = MathGalaxy.Clamp(count, GameConfig.MinParticipants, GameConfig.MaxParticipants);
        }

        private bool CanRespawn(Ship ship)
        {
            if (ship == null || ship.IsBoss) return false;
            int allowed = Settings.Respawns;
            if (!ship.IsPlayer) allowed += 4; // bots keep interacting far from the player
            return ship.Deaths <= allowed;
        }

        private void CollectPickups(Ship ship)
        {
            if (Arena == null || ship == null) return;
            float reach = ship.PickupRadius + 46f;
            _spawnScratch.Clear();
            Arena.QueryInto(ship.Position, reach, _spawnScratch);
            for (int i = 0; i < _spawnScratch.Count; i++)
            {
                WorldObject obj = _spawnScratch[i];
                if (obj == null || !obj.Alive || !BotPerception.IsPickup(obj.Kind)) continue;
                float distance = Vec2.Distance(ship.Position, obj.Position);
                if (distance > ship.PickupRadius + obj.Radius) continue;
                GrowthSystem.ApplyPickup(ship, obj, Log, Time, ship.IsPlayer ? Profile : null);
                if (obj.Alive) Arena.Remove(obj);
                if (ship.State != ShipState.Alive) break;
            }
        }

        private void IntegrateMovement(Ship ship, float dt)
        {
            if (ship == null || dt <= 0f) return;
            Vec2 input = ship.MoveInput;
            if (input.SqrMagnitude > 1f) input = input.Normalized;
            float speed = ship.Speed;
            if (ship.IsPlayer && _playerBoost) speed *= GameConfig.BoostMultiplier;
            Vec2 targetVelocity = input * speed;
            // Exponential smoothing is frame-rate independent; a plain dt*9 blend was not.
            float blend = 1f - (float)Math.Exp(-9f * dt);
            ship.Velocity = Vec2.Lerp(ship.Velocity, targetVelocity, blend);
            ship.Position = ship.Position + ship.Velocity * dt;

            if (Arena != null)
            {
                _spawnScratch.Clear();
                Arena.QueryInto(ship.Position, ship.Radius + 130f, _spawnScratch);
                for (int i = 0; i < _spawnScratch.Count; i++)
                {
                    Asteroid rock = _spawnScratch[i] as Asteroid;
                    if (rock == null || !rock.Alive || AbilitySystem.IsActive(ship, AbilityKind.Phase)) continue;
                    Vec2 away = ship.Position - rock.Position;
                    float minDist = ship.Radius + rock.Radius;
                    float distSqr = away.SqrMagnitude;
                    if (distSqr >= minDist * minDist) continue;
                    if (distSqr < 0.01f) away = Vec2.FromAngle(_spawnRng.Range(0f, FullCircle));
                    float dist = Math.Max(0.01f, away.Magnitude);
                    ship.Position = rock.Position + (away / dist) * minDist;
                    ship.Velocity = ship.Velocity * 0.55f;
                }

                float hardLimit = Arena.Radius - ship.Radius - 4f;
                if (hardLimit > 0f && ship.Position.Magnitude > hardLimit)
                {
                    ship.Position = ship.Position.Normalized * hardLimit;
                    ship.Velocity = ship.Velocity * 0.35f;
                }
            }
        }

        private void ReconcileKills()
        {
            if (_previousKills == null) return;
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship == null) continue;
                int previousKillCount;
                _previousKills.TryGetValue(ship.Id, out previousKillCount);
                int previousDeathCount;
                _previousDeaths.TryGetValue(ship.Id, out previousDeathCount);
                int killDelta = ship.Kills - previousKillCount;
                int deathDelta = ship.Deaths - previousDeathCount;
                _previousKills[ship.Id] = ship.Kills;
                _previousDeaths[ship.Id] = ship.Deaths;

                if (killDelta > 0)
                {
                    // Utility score on top of the base score granted by CombatSystem.
                    ship.Score += killDelta * (18f + Math.Max(0, ship.MultiKill) * 12f);
                    AddHighlight(new SimEvent(SimEventKind.Kill, ship.Id, -1,
                        "УНИЧТОЖЕНИЕ: " + ship.Name, ship.Position, Time, ship.Score));
                    if (ship.IsPlayer && ship.MultiKill >= 2)
                    {
                        AddHighlight(new SimEvent(SimEventKind.Streak, ship.Id, ship.MultiKill,
                            "МУЛЬТИКИЛЛ x" + ship.MultiKill.ToString(), ship.Position, Time));
                    }
                }

                // DeathAttackerId is captured by HandleKill and consumed here. LastAttackerId is
                // combat scratch data and may belong to an earlier life, so it must not decide who
                // gets the leader/boss bonus.
                int deathAttacker = ship.DeathAttackerId;
                ship.DeathAttackerId = -1;
                if (deathDelta <= 0 || deathAttacker <= 0) continue;
                Ship killer = FindShipById(deathAttacker);
                if (killer == null || killer == ship) continue;
                if (ship.Rank == 1)
                {
                    killer.Score += 320f * deathDelta;
                    AddHighlight(new SimEvent(SimEventKind.LeaderKilled, killer.Id, ship.Id,
                        "ПОВЕРЖЕН ЛИДЕР: " + ship.Name, ship.Position, Time, 320f));
                }
                if (ship.IsBoss && killer.IsPlayer && _countedBossDeaths.Add(ship.Id))
                {
                    _bossKills += deathDelta;
                    killer.Score += 240f * deathDelta;
                    AddHighlight(new SimEvent(SimEventKind.Kill, killer.Id, ship.Id,
                        "БОСС УНИЧТОЖЕН: " + ship.Name, ship.Position, Time, 240f));
                }
            }

            // Bosses that die without a direct player kill still count when the player is nearby.
            if (_newBosses.Count > 0)
            {
                for (int i = 0; i < _newBosses.Count; i++)
                {
                    Ship boss = _newBosses[i];
                    if (boss == null || boss.State == ShipState.Alive || _countedBossDeaths.Contains(boss.Id)) continue;
                    _countedBossDeaths.Add(boss.Id);
                    if (Player != null && IsPlayerNear(boss.Position, 900f))
                    {
                        _bossKills++;
                        Player.Score += 180f;
                    }
                }
                _newBosses.Clear();
            }
        }

        private void UpdateLeaderboard()
        {
            // Rows are recycled so the leaderboard step does not allocate every simulation step.
            int count = 0;
            for (int i = 0; i < Ships.Count; i++)
            {
                Ship ship = Ships[i];
                if (ship == null) continue;
                // A defeated boss stays in Ships until its reward is paid, but it must not keep
                // occupying a permanent TOP row (bosses never respawn).
                if (ship.IsBoss && ship.State != ShipState.Alive)
                {
                    ship.Rank = 0;
                    continue;
                }
                LeaderboardEntry entry;
                if (count < Leaderboard.Count) entry = Leaderboard[count];
                else
                {
                    entry = new LeaderboardEntry();
                    Leaderboard.Add(entry);
                }
                WriteEntry(entry, ship, count + 1);
                count++;
            }
            while (Leaderboard.Count > count) Leaderboard.RemoveAt(Leaderboard.Count - 1);
            Leaderboard.Sort(EntryComparator);
            for (int i = 0; i < Leaderboard.Count; i++)
            {
                Leaderboard[i].Rank = i + 1;
                Ship ship = FindShipById(Leaderboard[i].ShipId);
                if (ship != null) ship.Rank = i + 1;
            }
            PlayerRank = Player == null ? Leaderboard.Count : Player.Rank;
        }

        private static void WriteEntry(LeaderboardEntry entry, Ship ship, int rank)
        {
            entry.ShipId = ship.Id;
            entry.Name = ship.Name;
            entry.Score = ship.Score;
            entry.Mass = ship.Mass;
            entry.Level = (int)ship.Level;
            entry.Kills = ship.Kills;
            entry.Rank = rank;
            entry.IsPlayer = ship.IsPlayer;
            entry.Personality = ship.Personality;
            entry.IsBoss = ship.IsBoss;
        }

        private static int CompareEntries(LeaderboardEntry a, LeaderboardEntry b)
        {
            if (a == null || b == null) return 0;
            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0) return byScore;
            int byMass = b.Mass.CompareTo(a.Mass);
            if (byMass != 0) return byMass;
            int byKills = b.Kills.CompareTo(a.Kills);
            if (byKills != 0) return byKills;
            return a.ShipId.CompareTo(b.ShipId);
        }

        private void OfferPlayerEvolution()
        {
            if (Finished || Player == null || Player.State != ShipState.Alive || _evolutionOfferActive) return;
            bool snakeDone = Player.SnakeEvolutionTier >= SnakeEvolutionSystem.MaxTier;
            bool classicDone = Player.EvolutionTier >= 3;
            if (snakeDone && classicDone) return;

            int snakeLevel = SnakeEvolutionSystem.NextLevel(Player);
            int classicLevel = EvolutionSystem.NextEvolutionLevel(Player);
            bool snakeDue = !snakeDone && snakeLevel <= SnakeEvolutionSystem.MaxTier && Player.Level >= snakeLevel;
            bool classicDue = !classicDone && classicLevel < int.MaxValue && Player.Level >= classicLevel;
            if (!snakeDue && !classicDue) return;

            List<EvolutionDef> offers = snakeDue
                ? SnakeEvolutionSystem.Offer(Player, Rng)
                : EvolutionSystem.Offer(Player, Rng);
            PendingEvolutionChoices.Clear();
            if (offers != null)
            {
                for (int i = 0; i < offers.Count; i++)
                {
                    EvolutionDef def = offers[i];
                    if (def == null) continue;
                    bool available = snakeDue
                        ? SnakeEvolutionSystem.IsSnakeOffer(def)
                        : EvolutionSystem.IsAvailable(Player, def);
                    if (available) PendingEvolutionChoices.Add(def);
                }
            }
            if (PendingEvolutionChoices.Count == 0) return;
            _evolutionOfferActive = true;
            int tier = snakeDue ? Player.SnakeEvolutionTier + 1 : Player.EvolutionTier + 1;
            string prompt = snakeDue ? "ВЫБЕРИТЕ РОСТ ЗМЕЙКИ" : "ВЫБЕРИТЕ ЭВОЛЮЦИЮ";
            AddHighlight(new SimEvent(SimEventKind.Evolution, Player.Id, tier, prompt, Player.Position, Time, Player.Level));
            if (Log != null)
                Log.Add(SimEventKind.Evolution, Player.Id, tier, prompt, Player.Position, Time, Player.Level);
        }

        private void TrackHighlights()
        {
            if (Events == null) return;
            if (_lastPendingEvent.HasValue != Events.PendingKind.HasValue ||
                (_lastPendingEvent.HasValue && Events.PendingKind.HasValue &&
                 _lastPendingEvent.Value != Events.PendingKind.Value))
            {
                _lastPendingEvent = Events.PendingKind;
                if (Events.PendingKind.HasValue)
                {
                    AddHighlight(new SimEvent(SimEventKind.EventWarning, -1, (int)Events.PendingKind.Value,
                        Events.ActiveTitle ?? "ВНИМАНИЕ: СОБЫТИЕ", Vec2.Zero, Time));
                }
            }
            if (_lastActiveEvent.HasValue != Events.ActiveKind.HasValue ||
                (_lastActiveEvent.HasValue && Events.ActiveKind.HasValue &&
                 _lastActiveEvent.Value != Events.ActiveKind.Value))
            {
                _lastActiveEvent = Events.ActiveKind;
                if (Events.ActiveKind.HasValue)
                {
                    AddHighlight(new SimEvent(SimEventKind.EventStart, -1, (int)Events.ActiveKind.Value,
                        Events.ActiveTitle ?? "СОБЫТИЕ НАЧАЛОСЬ", Vec2.Zero, Time));
                }
                else
                {
                    AddHighlight(new SimEvent(SimEventKind.EventEnd, -1, 0,
                        "СОБЫТИЕ ЗАВЕРШЕНО", Vec2.Zero, Time));
                }
            }
        }

        private void AddHighlight(SimEvent ev)
        {
            if (ev == null) return;
            if (_highlights.Count >= MaxHighlights) _highlights.RemoveAt(0);
            _highlights.Add(ev);
        }

        private void CheckFinish()
        {
            if (Finished) return;
            bool timeUp = Time >= Settings.DurationSeconds;
            bool playerDead = Player == null || Player.State == ShipState.Dead ||
                              (Player.State == ShipState.Respawning && !CanRespawn(Player));
            if (timeUp)
            {
                Finish(false);
                return;
            }
            if (Settings.EndsOnPlayerDeath && playerDead)
            {
                Finish(false);
                return;
            }
            if (Mode == GameModeKind.Classic || Mode == GameModeKind.BossHunt || Mode == GameModeKind.Chaos)
            {
                if (playerDead && Player != null && Player.Deaths > Settings.Respawns) Finish(false);
            }
        }
    }
}
