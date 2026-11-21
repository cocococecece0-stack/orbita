namespace Orbit.Galaxy
{
    /// <summary>
    /// Poolable projectile. CombatSystem reuses instances with Active == false instead of
    /// constantly allocating, so the list stays bounded by the peak concurrent volley.
    /// </summary>
    public sealed class Projectile
    {
        public int Id;
        public int OwnerId;
        public Vec2 Position;
        public Vec2 Velocity;
        public float Damage;
        public float Radius;
        public float Lifetime;
        public WeaponKind Weapon;
        public bool Friendly;
        public Vec2 PreviousPosition;

        public bool Active;

        internal float HomingStrength;
        internal int TargetShipId;

        public Projectile()
        {
            Radius = 6f;
            Active = false;
            Lifetime = 0f;
            PreviousPosition = Vec2.Zero;
        }

        public Projectile(int id, int ownerId, Vec2 position, Vec2 velocity, float damage,
            float radius, float lifetime, WeaponKind weapon, bool friendly) : this()
        {
            Id = id;
            OwnerId = ownerId;
            Position = position;
            PreviousPosition = position;
            Velocity = velocity;
            Damage = damage;
            Radius = radius;
            Lifetime = lifetime;
            Weapon = weapon;
            Friendly = friendly;
            Active = lifetime > 0f;
        }

        public Projectile(int id, int ownerId, Vec2 position, Vec2 velocity, float damage,
            float radius, WeaponKind weapon, bool friendly, float lifetime)
            : this(id, ownerId, position, velocity, damage, radius, lifetime, weapon, friendly)
        {
        }

        internal void Reset(int id, int ownerId, Vec2 position, Vec2 velocity, float damage,
            float radius, float lifetime, WeaponKind weapon, bool friendly, float homing, int targetShipId)
        {
            Id = id;
            OwnerId = ownerId;
            Position = position;
            PreviousPosition = position;
            Velocity = velocity;
            Damage = damage;
            Radius = radius;
            Lifetime = lifetime;
            Weapon = weapon;
            Friendly = friendly;
            Active = lifetime > 0f;
            HomingStrength = homing;
            TargetShipId = targetShipId;
        }

        internal void Deactivate()
        {
            Active = false;
            Lifetime = 0f;
            HomingStrength = 0f;
            TargetShipId = 0;
        }
    }
}
