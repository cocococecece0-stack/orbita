using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Drives the seven arena events. Guarantees a warning phase before every event, at most one
    /// active event, at most one pending event, and a real world mutation for every mechanic.
    /// </summary>
    public sealed class WorldEventSystem
    {
        private const float FullCircle = 6.2831853f;

        private static readonly WorldEventKind[] AllKinds =
        {
            WorldEventKind.MeteorShower,
            WorldEventKind.BlackHole,
            WorldEventKind.EnergyStorm,
            WorldEventKind.VoidZone,
            WorldEventKind.EliteInvasion,
            WorldEventKind.AncientRelic,
            WorldEventKind.SpaceCollapse
        };

        private static readonly string[] ArtifactEffectIds =
        {
            "gravity_well", "shield_pulse", "damage_boost", "energy_surge",
            "phase_step", "magnet_field", "xp_amplifier"
        };

        private readonly Arena _arena;
        private readonly bool _chaos;
        private readonly List<WorldEventKind> _kindPool;
        private readonly List<WorldObject> _eventObjects;
        private readonly List<WorldObject> _scratch;
        private readonly List<Ship> _bosses;
        private Rng _rng;
        private WorldEventKind? _lastKind;
        private Vec2 _center;
        private float _spawnTimer;
        private float _pulseTimer;
        private int _eventIndex;

        public WorldEventKind? ActiveKind;
        public WorldEventKind? PendingKind;
        public float WarningRemaining;
        public float ActiveRemaining;
        public float NextEventIn;
        public string ActiveTitle;
        public string ActiveDescription;

        public WorldEventSystem(Arena arena, Rng rng, bool chaos)
        {
            _arena = arena;
            _rng = rng;
            _chaos = chaos;
            _kindPool = new List<WorldEventKind>(24);
            for (int i = 0; i < AllKinds.Length; i++) _kindPool.Add(AllKinds[i]);
            if (chaos)
            {
                _kindPool.Add(WorldEventKind.MeteorShower);
                _kindPool.Add(WorldEventKind.VoidZone);
                _kindPool.Add(WorldEventKind.SpaceCollapse);
            }
            _eventObjects = new List<WorldObject>(160);
            _scratch = new List<WorldObject>(160);
            _bosses = new List<Ship>(8);
            _lastKind = null;
            _center = Vec2.Zero;
            _spawnTimer = 0f;
            _pulseTimer = 0f;
            _eventIndex = 0;

            ActiveKind = null;
            PendingKind = null;
            WarningRemaining = 0f;
            ActiveRemaining = 0f;
            NextEventIn = chaos ? GameConfig.FirstEventDelay * 0.6f : GameConfig.FirstEventDelay;
            ActiveTitle = "СТАБИЛЬНАЯ ФАЗА";
            ActiveDescription = "Арена спокойна. Следите за предупреждениями о событиях.";
        }

        private static float TimeOf(MatchSimulation match)
        {
            return match == null ? 0f : match.Time;
        }

        // ───────────────────────── State machine ─────────────────────────

        public void Update(float dt, MatchSimulation match, SimLog log)
        {
            if (dt <= 0f) return;

            if (PendingKind == null)
            {
                NextEventIn -= dt;
                if (NextEventIn <= 0f)
                {
                    WorldEventKind next = ChooseKind();
                    SetPending(next, match, log);
                }
            }
            else
            {
                WarningRemaining -= dt;
                if (WarningRemaining <= 0f)
                {
                    WarningRemaining = 0f;
                    // Hold at zero while another event is active: no overlapping events.
                    if (ActiveKind == null)
                    {
                        WorldEventKind started = PendingKind.Value;
                        PendingKind = null;
                        StartEvent(started, match, log);
                    }
                }
            }

            if (ActiveKind == null) return;

            ActiveRemaining -= dt;
            if (ActiveRemaining > 0f)
            {
                UpdateActive(dt, match, log);
                return;
            }

            EndEvent(match, log);

            if (PendingKind != null && WarningRemaining <= 0f)
            {
                WorldEventKind started = PendingKind.Value;
                PendingKind = null;
                StartEvent(started, match, log);
            }
        }

        public void Trigger(WorldEventKind kind, MatchSimulation match, SimLog log)
        {
            if (ActiveKind != null)
            {
                PendingKind = kind;
                WarningRemaining = GameConfig.EventWarningTime;
                PrepareCenter(kind);
                ActiveTitle = "ВНИМАНИЕ: " + TitleOf(kind);
                ActiveDescription = WarningText(kind);
                if (log != null) log.Add(SimEventKind.EventWarning, -1, (int)kind,
                    WarningText(kind), Vec2.Zero, TimeOf(match));
                return;
            }

            PendingKind = null;
            WarningRemaining = 0f;
            NextEventIn = GameConfig.EventIntervalMin;
            PrepareCenter(kind);
            StartEvent(kind, match, log);
        }

        private WorldEventKind ChooseKind()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                WorldEventKind kind = _rng.Pick(_kindPool);
                if (!_lastKind.HasValue || kind != _lastKind.Value) return kind;
            }
            return _rng.Pick(_kindPool);
        }

        private void SetPending(WorldEventKind kind, MatchSimulation match, SimLog log)
        {
            PendingKind = kind;
            WarningRemaining = GameConfig.EventWarningTime;
            PrepareCenter(kind);
            ActiveTitle = "ВНИМАНИЕ: " + TitleOf(kind);
            ActiveDescription = WarningText(kind);
            if (log != null) log.Add(SimEventKind.EventWarning, -1, (int)kind,
                WarningText(kind), _center, TimeOf(match));
        }

        private void PrepareCenter(WorldEventKind kind)
        {
            if (_arena == null)
            {
                _center = Vec2.Zero;
                return;
            }
            if (kind == WorldEventKind.SpaceCollapse)
            {
                _center = Vec2.Zero;
                return;
            }
            if (kind == WorldEventKind.AncientRelic && _arena.SecretAreas.Count > 0)
            {
                _center = _rng.Pick(_arena.SecretAreas);
                return;
            }
            float fraction = kind == WorldEventKind.BlackHole ? 0.52f : 0.8f;
            _center = Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(0f, _arena.SafeRadius * fraction);
        }

        private void StartEvent(WorldEventKind kind, MatchSimulation match, SimLog log)
        {
            ActiveKind = kind;
            _lastKind = kind;
            _eventIndex++;
            float duration = _rng.Range(GameConfig.EventDurationMin, GameConfig.EventDurationMax);
            if (_chaos) duration *= 0.85f;
            ActiveRemaining = duration;
            ActiveTitle = TitleOf(kind);
            ActiveDescription = ActiveTextOf(kind);
            _spawnTimer = 0f;
            _pulseTimer = 4f;
            _eventObjects.Clear();
            _bosses.Clear();

            float time = TimeOf(match);
            if (log != null) log.Add(SimEventKind.EventStart, -1, (int)kind, ActiveDescription, _center, time);
            ApplyStart(kind, match, log, time);
        }

        private void EndEvent(MatchSimulation match, SimLog log)
        {
            WorldEventKind kind = ActiveKind.Value;
            float time = TimeOf(match);

            for (int i = 0; i < _eventObjects.Count; i++)
            {
                WorldObject o = _eventObjects[i];
                if (o == null || !o.Alive || _arena == null) continue;
                _arena.Remove(o);
            }
            _eventObjects.Clear();

            SpawnRewards(kind, match, log, time);
            if (log != null) log.Add(SimEventKind.EventEnd, -1, (int)kind, RewardText(kind), _center, time);

            ActiveKind = null;
            ActiveRemaining = 0f;
            ActiveTitle = "СОБЫТИЕ ЗАВЕРШЕНО";
            ActiveDescription = RewardText(kind);
            float interval = _rng.Range(GameConfig.EventIntervalMin, GameConfig.EventIntervalMax);
            if (_chaos) interval *= 0.62f;
            NextEventIn = interval;
        }

        // ───────────────────────── Active mechanics ─────────────────────────

        private void ApplyStart(WorldEventKind kind, MatchSimulation match, SimLog log, float time)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    _spawnTimer = 0f;
                    break;
                case WorldEventKind.BlackHole:
                    SpawnBlackHoleCore();
                    break;
                case WorldEventKind.EnergyStorm:
                    SpawnEnergyBurst();
                    break;
                case WorldEventKind.VoidZone:
                    SpawnVoidZones();
                    break;
                case WorldEventKind.EliteInvasion:
                    SpawnEliteWave(match, log, time, 2);
                    _spawnTimer = 9f;
                    break;
                case WorldEventKind.AncientRelic:
                    SpawnRelicCluster();
                    _spawnTimer = 6f;
                    break;
                case WorldEventKind.SpaceCollapse:
                    _spawnTimer = 0f;
                    break;
            }
        }

        private void UpdateActive(float dt, MatchSimulation match, SimLog log)
        {
            _pulseTimer -= dt;
            bool pulse = false;
            if (_pulseTimer <= 0f)
            {
                _pulseTimer = 4.5f;
                pulse = true;
            }
            float time = TimeOf(match);

            switch (ActiveKind.Value)
            {
                case WorldEventKind.MeteorShower:
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = _rng.Range(0.38f, 0.72f) / (_chaos ? 1.4f : 1f);
                        SpawnMeteor();
                    }
                    break;
                case WorldEventKind.BlackHole:
                    ApplyBlackHolePull(dt, match);
                    break;
                case WorldEventKind.EnergyStorm:
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = _rng.Range(0.6f, 1f);
                        SpawnEnergyBurst();
                    }
                    break;
                case WorldEventKind.VoidZone:
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = _rng.Range(4.5f, 7f);
                        SpawnVoidDarkMatter();
                    }
                    break;
                case WorldEventKind.EliteInvasion:
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = 9f;
                        if (CountAliveBosses() < 2) SpawnEliteWave(match, log, time, 1);
                    }
                    break;
                case WorldEventKind.AncientRelic:
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = 6f;
                        SpawnRelicSupply();
                    }
                    break;
                case WorldEventKind.SpaceCollapse:
                    if (_arena != null) _arena.Shrink(1.25f * _arena.DangerScale * dt);
                    _spawnTimer -= dt;
                    if (_spawnTimer <= 0f)
                    {
                        _spawnTimer = 0.95f;
                        SpawnCollapseHazards();
                    }
                    break;
            }

            if (pulse && log != null) log.Add(SimEventKind.Warning, -1, (int)ActiveKind.Value,
                PulseText(ActiveKind.Value), _center, time);
        }

        private Vec2 RandomPoint(float maxRadius)
        {
            return Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(0f, maxRadius);
        }

        private static string ArtifactEffectAt(int index)
        {
            int wrapped = ((index % ArtifactEffectIds.Length) + ArtifactEffectIds.Length) % ArtifactEffectIds.Length;
            return ArtifactEffectIds[wrapped];
        }

        private void SpawnMeteor()
        {
            if (_arena == null) return;

            Vec2 origin = Vec2.FromAngle(_rng.Range(0f, FullCircle)) * (_arena.Radius * 0.99f);
            Vec2 target = RandomPoint(_arena.SafeRadius * 0.72f);
            Vec2 velocity = (target - origin).Normalized * _rng.Range(680f, 1020f);

            HazardZone zone = new HazardZone(_arena.NextId(), origin, _rng.Range(58f, 108f),
                24f * _arena.DangerScale, 6f, false);
            zone.Velocity = velocity;
            zone.SourceEvent = "MeteorShower";
            _arena.Add(zone);
            _eventObjects.Add(zone);

            Asteroid rock = new Asteroid(_arena.NextId(), origin, _rng.Range(22f, 46f), 60f, 40f);
            rock.Velocity = velocity;
            rock.IsMeteor = true;
            rock.Spin = _rng.Range(-2.5f, 2.5f);
            _arena.Add(rock);
            _eventObjects.Add(rock);
        }

        private void SpawnBlackHoleCore()
        {
            if (_arena == null) return;
            float life = ActiveRemaining + 3f;

            Anomaly core = new Anomaly(_arena.NextId(), _center, 280f,
                240f * _arena.DangerScale, life, false);
            core.SourceEvent = "BlackHole";
            _arena.Add(core);
            _eventObjects.Add(core);

            HazardZone well = new HazardZone(_arena.NextId(), _center, 215f,
                15f * _arena.DangerScale, life, true);
            well.SourceEvent = "BlackHole";
            _arena.Add(well);
            _eventObjects.Add(well);

            for (int i = 0; i < 4; i++)
            {
                Vec2 p = _center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(280f, 520f);
                _arena.Add(new DarkMatterNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(12, 22)));
            }
        }

        private void ApplyBlackHolePull(float dt, MatchSimulation match)
        {
            if (_arena == null) return;
            float pull = 240f * _arena.DangerScale;

            _scratch.Clear();
            _arena.QueryInto(_center, 680f, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                WorldObject o = _scratch[i];
                if (o == null || o.Kind == ObjectKind.Hazard) continue;
                Vec2 delta = _center - o.Position;
                float distance = delta.Magnitude;
                if (distance < 24f || distance > 680f) continue;
                o.Position = o.Position + (delta / distance) * (pull * dt * 0.22f);
                _arena.Index.Update(o);
            }

            if (match == null || match.Ships == null) return;
            List<Ship> ships = match.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship ship = ships[i];
                if (ship == null || ship.State != ShipState.Alive) continue;
                Vec2 delta = _center - ship.Position;
                float distance = delta.Magnitude;
                if (distance < 30f || distance > 720f) continue;
                ship.Position = ship.Position + (delta / distance) * (pull * dt * 0.11f);
            }
        }

        private void SpawnEnergyBurst()
        {
            if (_arena == null) return;
            Vec2 center = RandomPoint(_arena.SafeRadius * 0.88f);
            for (int i = 0; i < 7; i++)
            {
                Vec2 p = center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(0f, 150f);
                _arena.Add(new EnergyNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(6, 15)));
            }
            if (_rng.Chance(0.5f))
            {
                Vec2 p = center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * 90f;
                _arena.Add(new CrystalNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(8, 15)));
            }
        }

        private void SpawnVoidZones()
        {
            if (_arena == null) return;
            float life = ActiveRemaining + 3f;
            for (int i = 0; i < 3; i++)
            {
                Vec2 p = RandomPoint(_arena.SafeRadius * 0.78f);
                HazardZone zone = new HazardZone(_arena.NextId(), p, _rng.Range(300f, 440f),
                    _rng.Range(22f, 34f) * _arena.DangerScale, life, true);
                zone.SourceEvent = "VoidZone";
                _arena.Add(zone);
                _eventObjects.Add(zone);

                for (int j = 0; j < 2; j++)
                {
                    Vec2 edge = p + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * (zone.Radius + 34f);
                    _arena.Add(new DarkMatterNode(_arena.NextId(), _arena.ClampToSafe(edge), _rng.NextInt(15, 26)));
                }
            }
        }

        private void SpawnVoidDarkMatter()
        {
            if (_arena == null) return;
            HazardZone zone = null;
            for (int i = 0; i < _eventObjects.Count; i++)
            {
                HazardZone candidate = _eventObjects[i] as HazardZone;
                if (candidate != null && candidate.Alive && candidate.SourceEvent == "VoidZone")
                {
                    zone = candidate;
                    break;
                }
            }
            Vec2 basePoint = zone != null ? zone.Position : _center;
            float baseRadius = zone != null ? zone.Radius : 200f;
            Vec2 p = basePoint + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * (baseRadius + 45f);
            _arena.Add(new DarkMatterNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(14, 24)));
        }

        private void SpawnEliteWave(MatchSimulation match, SimLog log, float time, int count)
        {
            if (_arena == null || match == null || match.Bosses == null) return;
            for (int i = 0; i < count; i++)
            {
                BossDef def = _rng.Pick(ContentDatabase.Bosses);
                Vec2 p = RandomPoint(_arena.SafeRadius * 0.55f);
                Ship boss = match.Bosses.Spawn(def, p, match, log, time);
                if (boss == null) continue;

                _bosses.Add(boss);
                if (_bosses.Count > 12) _bosses.RemoveAt(0);
                if (match.Ships != null && !match.Ships.Contains(boss)) match.Ships.Add(boss);

                for (int j = 0; j < 3; j++)
                {
                    Vec2 ap = boss.Position + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(90f, 220f);
                    _arena.Add(new ArtifactNode(_arena.NextId(), _arena.ClampToSafe(ap),
                        ArtifactEffectAt(_eventIndex + j), _rng.Range(0.6f, 1.4f)));
                }
            }
        }

        private int CountAliveBosses()
        {
            int count = 0;
            for (int i = 0; i < _bosses.Count; i++)
            {
                Ship boss = _bosses[i];
                if (boss != null && boss.State == ShipState.Alive) count++;
            }
            return count;
        }

        private void SpawnRelicCluster()
        {
            if (_arena == null) return;
            for (int i = 0; i < 4; i++)
            {
                Vec2 p = _center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(60f, 260f);
                _arena.Add(new ArtifactNode(_arena.NextId(), _arena.ClampToSafe(p),
                    ArtifactEffectAt(_eventIndex + i), _rng.Range(0.7f, 1.6f)));
            }
            SpawnRelicSupply();
        }

        private void SpawnRelicSupply()
        {
            if (_arena == null) return;
            for (int i = 0; i < 8; i++)
            {
                Vec2 p = _center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(180f, 420f);
                _arena.Add(new CrystalNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(10, 20)));
            }
            for (int i = 0; i < 3; i++)
            {
                Vec2 p = _center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(220f, 460f);
                _arena.Add(new DarkMatterNode(_arena.NextId(), _arena.ClampToSafe(p), _rng.NextInt(16, 28)));
            }
        }

        private void SpawnCollapseHazards()
        {
            if (_arena == null) return;
            float radius = _arena.SafeRadius;
            for (int i = 0; i < 5; i++)
            {
                Vec2 p = Vec2.FromAngle(_rng.Range(0f, FullCircle)) * radius;
                HazardZone zone = new HazardZone(_arena.NextId(), p, _rng.Range(150f, 230f),
                    30f * _arena.DangerScale, 2.8f, true);
                zone.SourceEvent = "SpaceCollapse";
                _arena.Add(zone);
                _eventObjects.Add(zone);
            }
        }

        private void SpawnRewards(WorldEventKind kind, MatchSimulation match, SimLog log, float time)
        {
            if (_arena == null) return;

            ResourceKind resource = RewardResource(kind);
            int count = 3;
            if (kind == WorldEventKind.EnergyStorm) count = 6;
            if (kind == WorldEventKind.AncientRelic || kind == WorldEventKind.EliteInvasion) count = 4;

            for (int i = 0; i < count; i++)
            {
                Vec2 p = _center + Vec2.FromAngle(_rng.Range(0f, FullCircle)) * _rng.Range(30f, 200f);
                Drop drop = new Drop(_arena.NextId(), _arena.ClampToSafe(p), resource,
                    RewardValue(kind), GameConfig.ResourceRespawnSeconds);
                _arena.Add(drop);
            }

            if (match == null || match.Ships == null) return;
            float bonus = 80f + 40f * _arena.DangerScale;
            float radius = 820f;
            for (int i = 0; i < match.Ships.Count; i++)
            {
                Ship ship = match.Ships[i];
                if (ship == null || ship.IsBoss || ship.State != ShipState.Alive) continue;
                if (Vec2.DistanceSqr(ship.Position, _center) > radius * radius) continue;
                ship.Score += bonus;
                if (log != null) log.Add(SimEventKind.EventEnd, ship.Id, (int)kind,
                    "Награда за событие: +" + bonus.ToString("F0") + " очков.",
                    ship.Position, time, bonus);
            }
        }

        // ───────────────────────── Russian presentation text ─────────────────────────

        private static string TitleOf(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower: return "МЕТЕОРНЫЙ ДОЖДЬ";
                case WorldEventKind.BlackHole: return "ЧЁРНАЯ ДЫРА";
                case WorldEventKind.EnergyStorm: return "ЭНЕРГЕТИЧЕСКАЯ БУРЯ";
                case WorldEventKind.VoidZone: return "ЗОНЫ ПУСТОТЫ";
                case WorldEventKind.EliteInvasion: return "ЭЛИТНЫЙ ДЕСАНТ";
                case WorldEventKind.AncientRelic: return "ДРЕВНИЙ РЕЛИКТ";
                case WorldEventKind.SpaceCollapse: return "СЖАТИЕ ПРОСТРАНСТВА";
                default: return "НЕИЗВЕСТНОЕ СОБЫТИЕ";
            }
        }

        private static string WarningText(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    return "Внимание: метеорный дождь! Через несколько секунд арену засыплют метеоры — уходите от открытых зон.";
                case WorldEventKind.BlackHole:
                    return "Внимание: в арене возникает чёрная дыра. Гравитация притянет корабли и ресурсы к её ядру.";
                case WorldEventKind.EnergyStorm:
                    return "Внимание: энергетическая буря! На арену выпадут новые источники энергии.";
                case WorldEventKind.VoidZone:
                    return "Внимание: зоны пустоты! Опасные области с замедлением появятся в случайных местах арены.";
                case WorldEventKind.EliteInvasion:
                    return "Внимание: элитный десант! На арену прибывают мощные вражеские корабли.";
                case WorldEventKind.AncientRelic:
                    return "Внимание: обнаружен древний реликт. В секретной зоне появятся артефакты и редкие ресурсы.";
                case WorldEventKind.SpaceCollapse:
                    return "Внимание: сжатие пространства! Безопасный радиус арены начнёт резко уменьшаться.";
                default:
                    return "Внимание: в арене возникнет неизвестное событие.";
            }
        }

        private static string ActiveTextOf(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    return "Метеоры бомбардируют арену. Опасные зоны отмечены на радаре.";
                case WorldEventKind.BlackHole:
                    return "Чёрная дыра притягивает корабли и ресурсы. Держитесь на расстоянии.";
                case WorldEventKind.EnergyStorm:
                    return "Буря насыщает арену энергией. Собирайте выпавшие узлы.";
                case WorldEventKind.VoidZone:
                    return "Зоны пустоты наносят урон и замедляют. На их границе появилась тёмная материя.";
                case WorldEventKind.EliteInvasion:
                    return "Элитные корабли охотятся на арене. За их уничтожение полагаются артефакты.";
                case WorldEventKind.AncientRelic:
                    return "Древний кластер активирован. Соберите артефакты и редкие ресурсы.";
                case WorldEventKind.SpaceCollapse:
                    return "Пространство сжимается, граница наносит урон. Спешите к центру.";
                default:
                    return "Событие изменило арену.";
            }
        }

        private static string PulseText(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    return "Очередная волна метеоров вошла в арену.";
                case WorldEventKind.BlackHole:
                    return "Гравитационное поле чёрной дыры искажает траектории.";
                case WorldEventKind.EnergyStorm:
                    return "Энергетическая буря продолжается.";
                case WorldEventKind.VoidZone:
                    return "Пустотные зоны активны: тёмная материя доступна на их границе.";
                case WorldEventKind.EliteInvasion:
                    return "Элитные корабли продолжают патрулировать арену.";
                case WorldEventKind.AncientRelic:
                    return "Реликт излучает артефакты и редкие ресурсы.";
                case WorldEventKind.SpaceCollapse:
                    return "Арена сжимается! Граница остаётся опасной.";
                default:
                    return "Событие продолжается.";
            }
        }

        private static string RewardText(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    return "Метеорный дождь завершён: награда распределена.";
                case WorldEventKind.BlackHole:
                    return "Чёрная дыра исчезла: тёмная материя и очки начислены.";
                case WorldEventKind.EnergyStorm:
                    return "Буря утихла: энергия и очки начислены.";
                case WorldEventKind.VoidZone:
                    return "Зоны пустоты рассеялись: кристаллы и очки начислены.";
                case WorldEventKind.EliteInvasion:
                    return "Десант отражён: артефакты и очки начислены.";
                case WorldEventKind.AncientRelic:
                    return "Реликт исчез: артефакты и очки начислены.";
                case WorldEventKind.SpaceCollapse:
                    return "Сжатие завершено: тёмная материя и очки начислены.";
                default:
                    return "Событие завершено: награда распределена.";
            }
        }

        private static ResourceKind RewardResource(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower: return ResourceKind.Energy;
                case WorldEventKind.BlackHole: return ResourceKind.DarkMatter;
                case WorldEventKind.EnergyStorm: return ResourceKind.Energy;
                case WorldEventKind.VoidZone: return ResourceKind.Crystal;
                case WorldEventKind.EliteInvasion: return ResourceKind.Artifact;
                case WorldEventKind.AncientRelic: return ResourceKind.Artifact;
                case WorldEventKind.SpaceCollapse: return ResourceKind.DarkMatter;
                default: return ResourceKind.Energy;
            }
        }

        private static int RewardValue(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MeteorShower: return 24;
                case WorldEventKind.BlackHole: return 20;
                case WorldEventKind.EnergyStorm: return 18;
                case WorldEventKind.VoidZone: return 16;
                case WorldEventKind.EliteInvasion: return 28;
                case WorldEventKind.AncientRelic: return 30;
                case WorldEventKind.SpaceCollapse: return 22;
                default: return 12;
            }
        }
    }
}
