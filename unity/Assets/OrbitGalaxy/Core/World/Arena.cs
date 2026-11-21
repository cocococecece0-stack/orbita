using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Deterministic procedural arena. Owns all world objects, their broadphase index, respawns and
    /// the shrinking safe zone. Pure C#: no Unity references and no dependence on other core modules
    /// besides the foundation types.
    /// </summary>
    public sealed class Arena
    {
        private const float MinSafeRadius = 260f;
        private const float FullCircle = 6.2831853f;

        private struct RespawnEntry
        {
            public float Time;
            public ObjectKind Kind;
            public Vec2 Position;
            public int Value;
            public float Radius;
            public float Health;
            public float Mass;
            public string EffectId;
            public float RotationSpeed;
        }

        private static readonly string[] ArtifactEffects =
        {
            "gravity_well", "shield_pulse", "damage_boost", "energy_surge",
            "phase_step", "magnet_field", "xp_amplifier"
        };

        private readonly GameModeKind _mode;
        private readonly float _dangerScale;
        private readonly bool _shrinks;
        private readonly float _shrinkBase;
        private readonly uint _seed;
        private readonly List<RespawnEntry> _respawns;
        private readonly List<WorldObject> _scratch;
        private readonly List<WorldObject> _expired;
        private readonly Dictionary<int, float> _borderWarnings;
        private int _nextId;
        private float _shrinkWarningTimer;

        public float Radius;
        public float SafeRadius;
        public Rng Rng;
        public SpatialHash Index;
        public List<WorldObject> Objects;
        public List<HazardZone> Hazards;
        public List<Anomaly> Anomalies;
        public float SafeRadiusStart;

        /// <summary>Hidden clusters where rare resources and relics are generated.</summary>
        public List<Vec2> SecretAreas;

        public Arena(GameModeKind mode, int seed, float dangerScale)
        {
            _mode = mode;
            _dangerScale = dangerScale > 0f ? dangerScale : 1f;
            ModeDef modeDef = ContentDatabase.FindMode(mode);
            _shrinks = modeDef != null && modeDef.ShrinkingArena;
            _shrinkBase = _shrinks ? 0.26f + 0.18f * _dangerScale : 0f;

            Radius = GameConfig.ArenaRadius;
            SafeRadiusStart = GameConfig.SafeRadiusStart;
            SafeRadius = SafeRadiusStart;

            _seed = unchecked((uint)seed * 2654435761u + (uint)((int)mode + 1) * 0x9E3779B9u);
            Rng = new Orbit.Galaxy.Rng(_seed);

            Index = new SpatialHash(GameConfig.CellSize);
            Objects = new List<WorldObject>(1024);
            Hazards = new List<HazardZone>(32);
            Anomalies = new List<Anomaly>(16);
            SecretAreas = new List<Vec2>(8);
            _respawns = new List<RespawnEntry>(160);
            _scratch = new List<WorldObject>(160);
            _expired = new List<WorldObject>(64);
            _borderWarnings = new Dictionary<int, float>(48);
            _nextId = 1;
            _shrinkWarningTimer = 0f;
        }

        public float DangerScale { get { return _dangerScale; } }
        public GameModeKind Mode { get { return _mode; } }
        public bool Shrinks { get { return _shrinks; } }

        /// <summary>Resets the arena and regenerates it from the constructor seed.</summary>
        public void Initialize()
        {
            Rng = new Orbit.Galaxy.Rng(_seed);
            Index.Clear();
            Objects.Clear();
            Hazards.Clear();
            Anomalies.Clear();
            SecretAreas.Clear();
            _respawns.Clear();
            _expired.Clear();
            _borderWarnings.Clear();
            _nextId = 1;
            SafeRadius = SafeRadiusStart;
            _shrinkWarningTimer = 0f;

            GenerateSecretAreas();
            GenerateAsteroidFields();
            GenerateEnergyClusters();
            GenerateCrystals();
            GenerateDarkMatter();
            GenerateArtifacts();
            GenerateHazards();
            GenerateAnomalies();
        }

        // ───────────────────────── Procedural generation ─────────────────────────

        private Vec2 RandomPoint(float maxRadius)
        {
            float r = Rng.Range(0f, maxRadius);
            return Vec2.FromAngle(Rng.Range(0f, FullCircle)) * r;
        }

        private bool HasRoom(Vec2 p, float minDistance)
        {
            _scratch.Clear();
            Index.Query(p, minDistance, _scratch);
            return _scratch.Count == 0;
        }

        private bool TryFindSpot(Vec2 anchor, float jitter, float minDistance, float maxRadius, out Vec2 result)
        {
            result = Vec2.Zero;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vec2 p = anchor + Vec2.FromAngle(Rng.Range(0f, FullCircle)) * Rng.Range(0f, jitter);
                if (p.Magnitude > maxRadius) continue;
                if (!HasRoom(p, minDistance)) continue;
                result = p;
                return true;
            }
            return false;
        }

        private void GenerateSecretAreas()
        {
            int count = 5;
            for (int i = 0; i < count; i++)
            {
                float angle = FullCircle * ((i + 0.5f) / count) + Rng.Range(-0.3f, 0.3f);
                float r = SafeRadiusStart * Rng.Range(0.72f, 0.9f);
                SecretAreas.Add(Vec2.FromAngle(angle) * r);
            }
        }

        private void GenerateAsteroidFields()
        {
            int placed = 0;
            const int FieldCount = 12;
            for (int f = 0; f < FieldCount && placed < GameConfig.Asteroids; f++)
            {
                Vec2 center = RandomPoint(SafeRadiusStart * 0.82f);
                int inField = Rng.NextInt(8, 18);
                for (int i = 0; i < inField && placed < GameConfig.Asteroids; i++)
                {
                    Vec2 p;
                    if (!TryFindSpot(center, Rng.Range(120f, 310f), 52f, SafeRadiusStart * 0.93f, out p)) continue;
                    float radius = Rng.Range(28f, 74f);
                    Add(new Asteroid(NextId(), p, radius, radius * Rng.Range(7f, 11f), radius * Rng.Range(3f, 5f)));
                    placed++;
                }
            }
            while (placed < GameConfig.Asteroids)
            {
                Vec2 p;
                if (!TryFindSpot(Vec2.Zero, SafeRadiusStart * 0.9f, 52f, SafeRadiusStart * 0.93f, out p)) break;
                float radius = Rng.Range(28f, 74f);
                Add(new Asteroid(NextId(), p, radius, radius * Rng.Range(7f, 11f), radius * Rng.Range(3f, 5f)));
                placed++;
            }
        }

        private void GenerateEnergyClusters()
        {
            int placed = 0;
            const int ClusterCount = 27;
            for (int c = 0; c < ClusterCount && placed < GameConfig.EnergyNodes; c++)
            {
                Vec2 center = RandomPoint(SafeRadiusStart * 0.88f);
                int inCluster = Rng.NextInt(15, 29);
                for (int i = 0; i < inCluster && placed < GameConfig.EnergyNodes; i++)
                {
                    Vec2 p;
                    if (!TryFindSpot(center, Rng.Range(55f, 215f), 21f, SafeRadiusStart * 0.96f, out p)) continue;
                    Add(new EnergyNode(NextId(), p, Rng.NextInt(5, 13)));
                    placed++;
                }
            }
            while (placed < GameConfig.EnergyNodes)
            {
                Vec2 p;
                if (!TryFindSpot(Vec2.Zero, SafeRadiusStart * 0.94f, 21f, SafeRadiusStart * 0.96f, out p)) break;
                Add(new EnergyNode(NextId(), p, Rng.NextInt(5, 13)));
                placed++;
            }
        }

        private void GenerateCrystals()
        {
            int placed = 0;
            while (placed < GameConfig.CrystalNodes)
            {
                Vec2 p;
                if (!TryFindSpot(Vec2.Zero, SafeRadiusStart * 0.94f, 60f, SafeRadiusStart * 0.96f, out p)) break;
                Add(new CrystalNode(NextId(), p, Rng.NextInt(8, 17)));
                placed++;
            }
        }

        private void GenerateDarkMatter()
        {
            int placed = 0;
            while (placed < GameConfig.DarkMatterNodes)
            {
                Vec2 anchor = SecretAreas.Count > 0 && Rng.Chance(0.68f)
                    ? Rng.Pick(SecretAreas)
                    : Vec2.Zero;
                float jitter = anchor.SqrMagnitude > 1f ? 260f : SafeRadiusStart * 0.95f;
                Vec2 p;
                if (!TryFindSpot(anchor, jitter, 90f, SafeRadiusStart * 0.96f, out p)) break;
                Add(new DarkMatterNode(NextId(), p, Rng.NextInt(14, 25)));
                placed++;
            }
        }

        private void GenerateArtifacts()
        {
            int placed = 0;
            while (placed < GameConfig.ArtifactNodes)
            {
                Vec2 anchor = SecretAreas.Count > 0
                    ? Rng.Pick(SecretAreas)
                    : RandomPoint(SafeRadiusStart * 0.8f);
                Vec2 p;
                if (!TryFindSpot(anchor, 230f, 130f, SafeRadiusStart * 0.96f, out p)) break;
                string effect = ArtifactEffects[Rng.NextInt(0, ArtifactEffects.Length)];
                Add(new ArtifactNode(NextId(), p, effect, Rng.Range(0.5f, 1.5f)));
                placed++;
            }
        }

        private void GenerateHazards()
        {
            int placed = 0;
            while (placed < GameConfig.Hazards)
            {
                Vec2 p;
                if (!TryFindSpot(Vec2.Zero, SafeRadiusStart * 0.9f, 230f, SafeRadiusStart * 0.94f, out p)) break;
                float radius = Rng.Range(120f, 225f);
                float dps = Rng.Range(10f, 21f) * _dangerScale;
                Add(new HazardZone(NextId(), p, radius, dps, 0f, Rng.Chance(0.42f)));
                placed++;
            }
        }

        private void GenerateAnomalies()
        {
            int placed = 0;
            while (placed < GameConfig.Anomalies)
            {
                Vec2 p;
                if (!TryFindSpot(Vec2.Zero, SafeRadiusStart * 0.88f, 310f, SafeRadiusStart * 0.93f, out p)) break;
                float radius = Rng.Range(120f, 215f);
                float pull = Rng.Range(38f, 88f);
                Add(new Anomaly(NextId(), p, radius, pull, 0f, Rng.Chance(0.35f)));
                placed++;
            }
        }

        // ───────────────────────── Object management ─────────────────────────

        public int NextId()
        {
            return _nextId++;
        }

        public void Add(WorldObject obj)
        {
            if (obj == null) return;
            obj.Alive = true;
            Objects.Add(obj);
            Index.Insert(obj);

            HazardZone hazard = obj as HazardZone;
            if (hazard != null && !Hazards.Contains(hazard)) Hazards.Add(hazard);

            Anomaly anomaly = obj as Anomaly;
            if (anomaly != null && !Anomalies.Contains(anomaly)) Anomalies.Add(anomaly);
        }

        public void Remove(WorldObject obj)
        {
            if (obj == null) return;
            obj.Alive = false;
            Index.Remove(obj);

            HazardZone hazard = obj as HazardZone;
            if (hazard != null) Hazards.Remove(hazard);

            Anomaly anomaly = obj as Anomaly;
            if (anomaly != null) Anomalies.Remove(anomaly);

            Objects.Remove(obj);
            ScheduleRespawn(obj);
        }

        private void ScheduleRespawn(WorldObject obj)
        {
            if (obj == null) return;

            Asteroid asteroid = obj as Asteroid;
            if (asteroid != null && asteroid.IsMeteor) return;

            RespawnEntry entry = new RespawnEntry();
            entry.Kind = obj.Kind;
            entry.Position = obj.Position;
            entry.Value = obj.Value;
            entry.Radius = obj.Radius;
            entry.Health = 1f;
            entry.Mass = 1f;
            entry.EffectId = null;
            entry.RotationSpeed = 0f;

            switch (obj.Kind)
            {
                case ObjectKind.Energy:
                case ObjectKind.Crystal:
                case ObjectKind.DarkMatter:
                case ObjectKind.Artifact:
                    entry.Time = GameConfig.ResourceRespawnSeconds * Rng.Range(0.75f, 1.3f);
                    break;
                case ObjectKind.Asteroid:
                    if (asteroid == null) return;
                    entry.Time = GameConfig.AsteroidRespawnSeconds * Rng.Range(0.8f, 1.25f);
                    entry.Radius = asteroid.Radius;
                    entry.Health = asteroid.Health;
                    entry.Mass = asteroid.Mass;
                    break;
                default:
                    return;
            }

            ArtifactNode artifact = obj as ArtifactNode;
            if (artifact != null)
            {
                entry.EffectId = artifact.EffectId;
                entry.RotationSpeed = artifact.RotationSpeed;
            }

            if (_respawns.Count > 400) _respawns.RemoveAt(0);
            _respawns.Add(entry);
        }

        private void SpawnRespawn(RespawnEntry entry)
        {
            Vec2 p = entry.Position;
            float limit = SafeRadius - 30f;
            if (limit < 40f) limit = 40f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vec2 candidate = p + Vec2.FromAngle(Rng.Range(0f, FullCircle)) * Rng.Range(0f, 75f);
                if (candidate.Magnitude <= limit)
                {
                    p = candidate;
                    break;
                }
            }
            if (p.Magnitude > limit) p = Vec2.FromAngle(Rng.Range(0f, FullCircle)) * limit;

            switch (entry.Kind)
            {
                case ObjectKind.Energy:
                    Add(new EnergyNode(NextId(), p, entry.Value));
                    break;
                case ObjectKind.Crystal:
                    Add(new CrystalNode(NextId(), p, entry.Value));
                    break;
                case ObjectKind.DarkMatter:
                    Add(new DarkMatterNode(NextId(), p, entry.Value));
                    break;
                case ObjectKind.Artifact:
                    Add(new ArtifactNode(NextId(), p, entry.EffectId, entry.RotationSpeed));
                    break;
                case ObjectKind.Asteroid:
                    Add(new Asteroid(NextId(), p, entry.Radius, entry.Health, entry.Mass));
                    break;
            }
        }

        // ───────────────────────── Simulation ─────────────────────────

        public void Update(float dt, SimLog log, float time)
        {
            if (dt <= 0f) return;

            if (_shrinks && SafeRadius > MinSafeRadius)
            {
                float rate = _shrinkBase * (1f + time / 540f);
                SafeRadius = MathGalaxy.Clamp(SafeRadius - rate * dt, MinSafeRadius, SafeRadiusStart);
                _shrinkWarningTimer += dt;
                if (_shrinkWarningTimer >= 45f)
                {
                    _shrinkWarningTimer = 0f;
                    if (log != null) log.Add(SimEventKind.Warning, -1, 0,
                        "Безопасная зона сжимается. Возвращайтесь к центру!", Vec2.Zero, time);
                }
            }

            UpdateBodies(dt);
            UpdateRespawns(dt);
        }

        private void UpdateBodies(float dt)
        {
            _expired.Clear();
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                WorldObject o = Objects[i];
                if (o == null || !o.Alive) continue;

                Drop drop = o as Drop;
                HazardZone hazard = o as HazardZone;
                Anomaly anomaly = o as Anomaly;
                Asteroid asteroid = o as Asteroid;
                ArtifactNode artifact = o as ArtifactNode;

                bool expire = false;

                float lifetime = 0f;
                bool hasLifetime = false;
                if (drop != null) { lifetime = drop.Lifetime; hasLifetime = true; }
                else if (hazard != null) { lifetime = hazard.Lifetime; hasLifetime = true; }
                else if (anomaly != null) { lifetime = anomaly.Lifetime; hasLifetime = true; }

                if (hasLifetime && lifetime > 0f)
                {
                    lifetime -= dt;
                    if (drop != null) drop.Lifetime = lifetime;
                    else if (hazard != null) hazard.Lifetime = lifetime;
                    else if (anomaly != null) anomaly.Lifetime = lifetime;
                    if (lifetime <= 0f) expire = true;
                }

                if (artifact != null) artifact.Rotation += artifact.RotationSpeed * dt;

                Vec2 velocity = Vec2.Zero;
                if (asteroid != null)
                {
                    velocity = asteroid.Velocity;
                    if (asteroid.IsMeteor) asteroid.Rotation += asteroid.Spin * dt;
                }
                else if (drop != null) velocity = drop.Velocity;
                else if (hazard != null) velocity = hazard.Velocity;
                else if (anomaly != null) velocity = anomaly.Velocity;

                if (velocity.X != 0f || velocity.Y != 0f)
                {
                    o.Position = o.Position + velocity * dt;
                    Index.Update(o);
                    if (o.Position.Magnitude > Radius + 160f) expire = true;
                }

                if (expire) _expired.Add(o);
            }

            for (int i = 0; i < _expired.Count; i++) Remove(_expired[i]);
            _expired.Clear();
        }

        private void UpdateRespawns(float dt)
        {
            for (int i = _respawns.Count - 1; i >= 0; i--)
            {
                RespawnEntry entry = _respawns[i];
                entry.Time -= dt;
                if (entry.Time > 0f)
                {
                    _respawns[i] = entry;
                    continue;
                }
                _respawns.RemoveAt(i);
                SpawnRespawn(entry);
            }
        }

        // ───────────────────────── Queries and danger ─────────────────────────

        public bool IsInside(Vec2 p)
        {
            return p.SqrMagnitude <= Radius * Radius;
        }

        public Vec2 ClampToSafe(Vec2 p)
        {
            float limit = SafeRadius - 12f;
            if (limit < 40f) limit = 40f;
            float length = p.Magnitude;
            if (length <= limit) return p;
            return p * (limit / length);
        }

        /// <summary>Combined damage per second from hazards and the unsafe border at p.</summary>
        public float DangerAt(Vec2 p)
        {
            float danger = 0f;
            _scratch.Clear();
            Index.Query(p, 640f, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                HazardZone hazard = _scratch[i] as HazardZone;
                if (hazard == null) continue;
                if (Vec2.DistanceSqr(p, hazard.Position) <= hazard.Radius * hazard.Radius)
                    danger += hazard.DamagePerSecond;
            }

            float distance = p.Magnitude;
            if (distance > SafeRadius)
            {
                float overflow = MathGalaxy.Clamp((distance - SafeRadius) / 280f, 0f, 6f);
                danger += GameConfig.BorderDamagePerSecond * _dangerScale * (1f + overflow);
            }
            return danger;
        }

        public void DamageBorder(Ship ship, float dt, SimLog log, float time)
        {
            if (ship == null || dt <= 0f) return;
            if (ship.State != ShipState.Alive) return;
            if (ship.Invulnerability > 0f) return;

            float distance = ship.Position.Magnitude;
            if (distance <= SafeRadius) return;

            float overflow = MathGalaxy.Clamp((distance - SafeRadius) / 280f, 0f, 6f);
            float dps = GameConfig.BorderDamagePerSecond * _dangerScale * (1f + overflow);
            float amount = dps * dt;

            float remaining = amount;
            if (ship.Shield > 0f)
            {
                float absorbed = Math.Min(ship.Shield, amount * GameConfig.ShieldAbsorption);
                ship.Shield -= absorbed;
                remaining -= absorbed;
            }
            if (remaining > 0f)
            {
                ship.Hp -= remaining;
                if (ship.Hp < 0f) ship.Hp = 0f;
            }

            float last;
            bool warn = !_borderWarnings.TryGetValue(ship.Id, out last) || time - last >= 3.5f;
            if (!warn) return;
            _borderWarnings[ship.Id] = time;
            if (log != null)
            {
                log.Add(SimEventKind.Warning, ship.Id, 0,
                    "Вне безопасной зоны: корпус повреждается!", ship.Position, time);
                log.Add(SimEventKind.Damage, ship.Id, 0,
                    "Урон от границы арены", ship.Position, time, amount);
            }
        }

        public List<WorldObject> Query(Vec2 center, float radius)
        {
            List<WorldObject> result = new List<WorldObject>(64);
            QueryInto(center, radius, result);
            return result;
        }

        public void QueryInto(Vec2 center, float radius, List<WorldObject> output)
        {
            if (output == null) return;
            Index.Query(center, radius, output);
        }

        private static bool IsResourceKind(ObjectKind kind)
        {
            switch (kind)
            {
                case ObjectKind.Energy:
                case ObjectKind.Crystal:
                case ObjectKind.DarkMatter:
                case ObjectKind.Artifact:
                case ObjectKind.Drop:
                    return true;
                default:
                    return false;
            }
        }

        private static ResourceKind ResourceOf(WorldObject o)
        {
            Drop drop = o as Drop;
            if (drop != null) return drop.Resource;
            switch (o.Kind)
            {
                case ObjectKind.Energy: return ResourceKind.Energy;
                case ObjectKind.Crystal: return ResourceKind.Crystal;
                case ObjectKind.DarkMatter: return ResourceKind.DarkMatter;
                case ObjectKind.Artifact: return ResourceKind.Artifact;
                default: return ResourceKind.Energy;
            }
        }

        public WorldObject FindNearestResource(Vec2 pos, float radius, ResourceKind? filter = null)
        {
            _scratch.Clear();
            Index.Query(pos, radius + 96f, _scratch);

            WorldObject best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _scratch.Count; i++)
            {
                WorldObject o = _scratch[i];
                if (o == null || !IsResourceKind(o.Kind)) continue;
                if (filter.HasValue && ResourceOf(o) != filter.Value) continue;

                float reach = radius + o.Radius;
                float sqr = Vec2.DistanceSqr(pos, o.Position);
                if (sqr > reach * reach) continue;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = o;
                }
            }
            return best;
        }

        public void Shrink(float amount)
        {
            if (amount <= 0f) return;
            SafeRadius = MathGalaxy.Clamp(SafeRadius - amount, MinSafeRadius, SafeRadiusStart);
        }
    }
}
