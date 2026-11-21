using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>Base class for everything that lives in the arena and can be indexed by SpatialHash.</summary>
    public abstract class WorldObject
    {
        public int Id;
        public ObjectKind Kind;
        public Vec2 Position;
        public float Radius;
        public bool Alive;
        public int Value;
        public float Rotation;
        public string OwnerId;

        protected WorldObject(int id, ObjectKind kind, Vec2 position, float radius, int value)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Radius = radius > 0f ? radius : 1f;
            Alive = true;
            Value = value;
            Rotation = 0f;
            OwnerId = null;
        }

        /// <summary>Russian label used by UI and logs.</summary>
        public virtual string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case ObjectKind.Energy: return "Энергия";
                    case ObjectKind.Crystal: return "Кристалл";
                    case ObjectKind.DarkMatter: return "Тёмная материя";
                    case ObjectKind.Artifact: return "Артефакт";
                    case ObjectKind.Asteroid: return "Астероид";
                    case ObjectKind.Drop: return "Добыча";
                    case ObjectKind.Hazard: return "Опасная зона";
                    case ObjectKind.Anomaly: return "Аномалия";
                    case ObjectKind.Projectile: return "Снаряд";
                    default: return "Объект";
                }
            }
        }
    }

    public sealed class EnergyNode : WorldObject
    {
        public EnergyNode(int id, Vec2 pos, int value)
            : base(id, ObjectKind.Energy, pos, 12f, value)
        {
        }
    }

    public sealed class CrystalNode : WorldObject
    {
        public CrystalNode(int id, Vec2 pos, int value)
            : base(id, ObjectKind.Crystal, pos, 15f, value)
        {
        }
    }

    public sealed class DarkMatterNode : WorldObject
    {
        public DarkMatterNode(int id, Vec2 pos, int value)
            : base(id, ObjectKind.DarkMatter, pos, 18f, value)
        {
        }
    }

    public sealed class ArtifactNode : WorldObject
    {
        public string EffectId;
        public float RotationSpeed;

        public ArtifactNode(int id, Vec2 pos, string effectId, float rotationSpeed)
            : base(id, ObjectKind.Artifact, pos, 20f, 40)
        {
            EffectId = string.IsNullOrEmpty(effectId) ? "unknown" : effectId;
            RotationSpeed = rotationSpeed;
        }

        public override string DisplayName { get { return "Древний артефакт"; } }
    }

    /// <summary>Solid obstacle. Meteors are asteroids with IsMeteor set and a non-zero Velocity.</summary>
    public sealed class Asteroid : WorldObject
    {
        public float Health;
        public float Mass;
        public Vec2 Velocity;
        public bool IsMeteor;
        public float Spin;

        public Asteroid(int id, Vec2 pos, float radius, float health, float mass)
            : base(id, ObjectKind.Asteroid, pos, radius, 0)
        {
            Health = health > 0f ? health : 1f;
            Mass = mass > 0f ? mass : 1f;
            Velocity = Vec2.Zero;
            IsMeteor = false;
            Spin = 0f;
        }
    }

    /// <summary>Short-lived resource pickup dropped by events and destroyed ships.</summary>
    public sealed class Drop : WorldObject
    {
        public ResourceKind Resource;
        public float Lifetime;
        public Vec2 Velocity;

        public Drop(int id, Vec2 pos, ResourceKind resource, int value, float lifetime)
            : base(id, ObjectKind.Drop, pos, 11f, value)
        {
            Resource = resource;
            Lifetime = lifetime > 0f ? lifetime : GameConfig.ResourceRespawnSeconds;
            Velocity = Vec2.Zero;
        }

        public override string DisplayName
        {
            get
            {
                switch (Resource)
                {
                    case ResourceKind.Energy: return "Энергия";
                    case ResourceKind.Crystal: return "Кристалл";
                    case ResourceKind.DarkMatter: return "Тёмная материя";
                    case ResourceKind.Artifact: return "Артефакт";
                    default: return "Добыча";
                }
            }
        }
    }

    /// <summary>Area of increased danger. Lifetime &lt;= 0 means a permanent arena feature.</summary>
    public sealed class HazardZone : WorldObject
    {
        public float DamagePerSecond;
        public float Lifetime;
        public bool Slows;
        public Vec2 Velocity;
        public string SourceEvent;

        public HazardZone(int id, Vec2 pos, float radius, float damagePerSecond, float lifetime, bool slows)
            : base(id, ObjectKind.Hazard, pos, radius, 0)
        {
            DamagePerSecond = damagePerSecond > 0f ? damagePerSecond : 0f;
            Lifetime = lifetime;
            Slows = slows;
            Velocity = Vec2.Zero;
            SourceEvent = null;
        }
    }

    /// <summary>Gravity or shield anomaly. Pull is consumed by navigation and gravity systems.</summary>
    public sealed class Anomaly : WorldObject
    {
        public float Pull;
        public float Lifetime;
        public bool GrantsShield;
        public Vec2 Velocity;
        public string SourceEvent;

        public Anomaly(int id, Vec2 pos, float radius, float pull, float lifetime, bool grantsShield)
            : base(id, ObjectKind.Anomaly, pos, radius, 0)
        {
            Pull = pull > 0f ? pull : 0f;
            Lifetime = lifetime;
            GrantsShield = grantsShield;
            Velocity = Vec2.Zero;
            SourceEvent = null;
        }
    }
}
