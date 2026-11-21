using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    // ───────────────────────────── Enums ─────────────────────────────

    public enum GameModeKind
    {
        Classic,
        Survival,
        BossHunt,
        Chaos,
        Hardcore,
        Infinite
    }

    public enum BotPersonality
    {
        Aggressive,
        Farmer,
        Survivor,
        Hunter,
        Scavenger,
        Elite,
        Chaotic
    }

    public enum EvolutionBranch
    {
        None,
        Titan,
        Phantom,
        Swarm,
        Singularity
    }

    public enum AbilityKind
    {
        Dash,
        Shield,
        Nova,
        Magnet,
        Overdrive,
        BlackHole,
        Phase
    }

    public enum WeaponKind
    {
        Laser,
        Plasma,
        Missile,
        Drone,
        Ring,
        Pulse,
        Lightning,
        Orbital
    }

    public enum ResourceKind
    {
        Energy,
        Crystal,
        DarkMatter,
        Artifact
    }

    public enum ObjectKind
    {
        Energy,
        Crystal,
        DarkMatter,
        Artifact,
        Asteroid,
        Drop,
        Hazard,
        Anomaly,
        Projectile
    }

    public enum WorldEventKind
    {
        MeteorShower,
        BlackHole,
        EnergyStorm,
        VoidZone,
        EliteInvasion,
        AncientRelic,
        SpaceCollapse
    }

    public enum SimEventKind
    {
        Pickup,
        LevelUp,
        Evolution,
        Kill,
        Absorb,
        Damage,
        Ability,
        BossSpawn,
        EventWarning,
        EventStart,
        EventEnd,
        MatchEnd,
        RankUp,
        LeaderKilled,
        Streak,
        Victory,
        Defeat,
        Warning,
        Tick
    }

    public enum ShipState
    {
        Alive,
        Respawning,
        Dead
    }

    public enum Difficulty
    {
        Casual,
        Normal,
        Hard,
        Expert
    }

    public enum BotGoal
    {
        Idle,
        Farm,
        Hunt,
        Flee,
        CollectRare,
        AvoidHazard,
        ContestEvent,
        AttackBoss,
        Patrol,
        Regroup
    }

    public enum HullShape
    {
        Dart,
        Wedge,
        Hex,
        Bulwark,
        Blade,
        Spike,
        Ring,
        Core
    }

    // ───────────────────────────── Vec2 ─────────────────────────────

    /// <summary>Small allocation-free 2D vector used by the deterministic simulation.</summary>
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero { get { return new Vec2(0f, 0f); } }
        public static Vec2 Up { get { return new Vec2(0f, 1f); } }
        public static Vec2 Right { get { return new Vec2(1f, 0f); } }

        public float SqrMagnitude { get { return X * X + Y * Y; } }
        public float Magnitude { get { return (float)Math.Sqrt(SqrMagnitude); } }

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 0.00001f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        public Vec2 Perpendicular { get { return new Vec2(-Y, X); } }

        public static Vec2 FromAngle(float radians)
        {
            return new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians));
        }

        public float Angle()
        {
            return (float)Math.Atan2(Y, X);
        }

        public static float Dot(Vec2 a, Vec2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        public static float Cross(Vec2 a, Vec2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float DistanceSqr(Vec2 a, Vec2 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            return new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.X + b.X, a.Y + b.Y); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.X - b.X, a.Y - b.Y); }
        public static Vec2 operator -(Vec2 a) { return new Vec2(-a.X, -a.Y); }
        public static Vec2 operator *(Vec2 a, float s) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator *(float s, Vec2 a) { return new Vec2(a.X * s, a.Y * s); }
        public static Vec2 operator /(Vec2 a, float s) { return new Vec2(a.X / s, a.Y / s); }

        public bool Equals(Vec2 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Vec2 && Equals((Vec2)obj);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() * 397);
        }

        public override string ToString()
        {
            return "(" + X.ToString("F1") + ", " + Y.ToString("F1") + ")";
        }
    }

    // ───────────────────────────── Rng ─────────────────────────────

    /// <summary>Deterministic xorshift-based RNG. Struct semantics keep simulations reproducible.</summary>
    public struct Rng
    {
        private uint _state;

        public Rng(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint State { get { return _state; } }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public float NextFloat()
        {
            // 2^24 divisor matches the mobile builds and keeps the value in [0,1).
            return (NextUInt() & 0xFFFFFFu) / 16777216f;
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        public T Pick<T>(IList<T> items)
        {
            if (items == null || items.Count == 0) return default(T);
            return items[NextInt(0, items.Count)];
        }

        public Rng Fork()
        {
            return new Rng(NextUInt() ^ 0x85EBCA6Bu);
        }

        public static uint Hash(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0x811C9DC5u;
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }

    // ─────────────────────────── Events ─────────────────────────────

    /// <summary>One simulation-to-presentation message. Value carries the second semantic (e.g. finish result).</summary>
    public sealed class SimEvent
    {
        public SimEventKind Kind;
        public int ActorId;
        public int Value;
        public float FloatValue;
        public string Text;
        public Vec2 Position;
        public float Time;

        public SimEvent(SimEventKind kind, int actorId, int value, string text, Vec2 position, float time, float floatValue = 0f)
        {
            Kind = kind;
            ActorId = actorId;
            Value = value;
            Text = text;
            Position = position;
            Time = time;
            FloatValue = floatValue;
        }
    }

    /// <summary>Per-tick event buffer. The runtime drains it; the simulation never depends on Unity.</summary>
    public sealed class SimLog
    {
        private readonly List<SimEvent> _events = new List<SimEvent>(64);
        private readonly List<SimEvent> _drained = new List<SimEvent>(64);

        public int Count { get { return _events.Count; } }

        public void Add(SimEventKind kind, int actorId, int value, string text, Vec2 position, float time, float floatValue = 0f)
        {
            _events.Add(new SimEvent(kind, actorId, value, text, position, time, floatValue));
        }

        public void Add(SimEventKind kind, Ship ship, string text, float time)
        {
            Add(kind, ship == null ? -1 : ship.Id, 0, text, ship == null ? Vec2.Zero : ship.Position, time);
        }

        public List<SimEvent> Drain()
        {
            _drained.Clear();
            if (_events.Count == 0) return _drained;
            _drained.AddRange(_events);
            _events.Clear();
            return _drained;
        }

        public void Clear()
        {
            _events.Clear();
        }
    }

    // ─────────────────────────── Helpers ─────────────────────────────

    public static class MathGalaxy
    {
        public static float Clamp(float value, float min, float max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Clamp(t, 0f, 1f);
        }

        public static float SmoothStep(float t)
        {
            t = Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public static float MoveTowards(float current, float target, float delta)
        {
            if (current < target) return Math.Min(current + delta, target);
            if (current > target) return Math.Max(current - delta, target);
            return target;
        }
    }
}
