using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Seven active abilities with cooldowns, energy costs, timed buffs and area effects.
    /// Nova and BlackHole also interact with the live projectile list.
    /// </summary>
    public static class AbilitySystem
    {
        private static float EnergyCost(AbilityKind kind)
        {
            switch (kind)
            {
                case AbilityKind.Dash: return 12f;
                case AbilityKind.Shield: return 18f;
                case AbilityKind.Nova: return 26f;
                case AbilityKind.Magnet: return 10f;
                case AbilityKind.Overdrive: return 22f;
                case AbilityKind.BlackHole: return 32f;
                case AbilityKind.Phase: return 16f;
                default: return 0f;
            }
        }

        private static int Index(AbilityKind kind)
        {
            return (int)kind;
        }

        public static bool IsActive(Ship ship, AbilityKind kind)
        {
            if (ship == null || ship.EffectTimers == null) return false;
            int index = Index(kind);
            return index >= 0 && index < ship.EffectTimers.Length && ship.EffectTimers[index] > 0f;
        }

        public static bool CanActivate(Ship ship, AbilityKind kind)
        {
            if (ship == null || ship.State != ShipState.Alive || ship.Hp <= 0f) return false;
            if (ship.Cooldowns == null || ship.EffectTimers == null) return false;
            int index = Index(kind);
            if (index < 0 || index >= ship.Cooldowns.Length || index >= ship.EffectTimers.Length) return false;
            if (ship.Cooldowns[index] > 0f) return false;
            if (kind == AbilityKind.Shield && ship.ShieldBonusActive) return false;
            return ship.Energy >= EnergyCost(kind);
        }

        public static bool Activate(Ship ship, AbilityKind kind, SimLog log, float time)
        {
            if (!CanActivate(ship, kind)) return false;
            AbilityDef def = ContentDatabase.FindAbility(kind);
            int index = Index(kind);

            ship.Energy = MathGalaxy.Clamp(ship.Energy - EnergyCost(kind), 0f, GameConfig.MaxEnergy);
            ship.Cooldowns[index] = Math.Max(0.5f, def.Cooldown);
            ship.EffectTimers[index] = Math.Max(0f, def.Duration);
            if (log != null)
            {
                log.Add(SimEventKind.Ability, ship.Id, index, def.Name, ship.Position, time, def.Duration);
            }
            ship.Remember("Способность: " + def.Name);

            switch (kind)
            {
                case AbilityKind.Dash:
                {
                    Vec2 dir = ship.Facing.SqrMagnitude > 0.0001f ? ship.Facing.Normalized : Vec2.Up;
                    ship.Position += dir * def.Power;
                    float limit = GameConfig.ArenaRadius * 0.985f;
                    if (ship.Position.SqrMagnitude > limit * limit)
                        ship.Position = ship.Position.Normalized * limit;
                    ship.Velocity += dir * def.Power * 0.4f;
                    ship.Invulnerability = Math.Max(ship.Invulnerability, def.Duration + 0.2f);
                    break;
                }
                case AbilityKind.Shield:
                    ship.AbilityShieldBonus = def.Power;
                    ship.ShieldBonusActive = true;
                    ship.RecalculateStats(null);
                    ship.Shield = ship.MaxShield;
                    break;
                case AbilityKind.Nova:
                    ship.NovaPending = true;
                    break;
                case AbilityKind.Magnet:
                    // Поле притяжения работает каждый тик в Update, а не в момент активации.
                    break;
                case AbilityKind.Overdrive:
                    ship.RecalculateStats(null);
                    break;
                case AbilityKind.BlackHole:
                    ship.BlackHolePosition = ship.Position;
                    break;
                case AbilityKind.Phase:
                    ship.Invulnerability = Math.Max(ship.Invulnerability, def.Duration);
                    ship.RecalculateStats(null);
                    break;
            }
            return true;
        }

        private static void NovaBurst(Ship ship, List<Ship> ships, List<Projectile> projectiles, SimLog log, float time)
        {
            ship.NovaPending = false;
            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.Nova);
            float radius = Math.Max(1f, def.Radius);

            if (ships != null)
            {
                for (int i = 0; i < ships.Count; i++)
                {
                    Ship target = ships[i];
                    if (target == null || target == ship || target.State != ShipState.Alive || target.Hp <= 0f) continue;
                    float distance = Vec2.Distance(ship.Position, target.Position);
                    if (distance > radius) continue;
                    float falloff = 1f - distance / radius;
                    float massFactor = MathGalaxy.Clamp(ship.Mass / MathGalaxy.Clamp(target.Mass, 1f, 100000f), 0.5f, 2f);
                    CombatSystem.ApplyDamage(target, def.Power * (0.6f + falloff) * massFactor, ship, log, time, false);
                    Vec2 away = distance > 0.0001f ? (target.Position - ship.Position) / distance : Vec2.Right;
                    target.Position += away * 70f * falloff;
                    target.Velocity += away * 220f * falloff;
                }
            }

            if (projectiles != null)
            {
                for (int i = 0; i < projectiles.Count; i++)
                {
                    Projectile p = projectiles[i];
                    if (p == null || !p.Active || p.OwnerId == ship.Id) continue;
                    float distance = Vec2.Distance(ship.Position, p.Position);
                    if (distance > radius) continue;
                    Vec2 away = distance > 0.0001f ? (p.Position - ship.Position) / distance : Vec2.Right;
                    p.Velocity = away * Math.Max(60f, p.Velocity.Magnitude * 1.25f);
                    if (distance < radius * 0.25f) p.Deactivate();
                }
            }
            if (log != null) log.Add(SimEventKind.Ability, ship.Id, (int)AbilityKind.Nova,
                "ВЗРЫВ НОВЫ", ship.Position, time, radius);
        }

        private static void MagnetPull(Ship ship, Arena arena, float dt)
        {
            if (arena == null || arena.Objects == null) return;
            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.Magnet);
            float radius = Math.Max(1f, def.Radius);
            float speed = 520f * Math.Max(0.2f, def.Power);
            for (int i = 0; i < arena.Objects.Count; i++)
            {
                WorldObject obj = arena.Objects[i];
                if (obj == null || !obj.Alive) continue;
                bool resource = obj.Kind == ObjectKind.Energy || obj.Kind == ObjectKind.Crystal
                    || obj.Kind == ObjectKind.DarkMatter || obj.Kind == ObjectKind.Artifact
                    || obj.Kind == ObjectKind.Drop;
                if (!resource) continue;
                float distanceSqr = Vec2.DistanceSqr(obj.Position, ship.Position);
                if (distanceSqr > radius * radius) continue;
                float distance = (float)Math.Sqrt(distanceSqr);
                Vec2 dir = distance > 0.0001f ? (ship.Position - obj.Position) / distance : Vec2.Zero;
                float step = Math.Min(distance, speed * dt);
                obj.Position += dir * step;
                if (arena.Index != null) arena.Index.Update(obj);
            }
        }

        private static void BlackHolePull(Ship ship, List<Ship> ships, List<Projectile> projectiles, float dt)
        {
            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.BlackHole);
            float radius = Math.Max(1f, def.Radius);
            Vec2 anchor = ship.BlackHolePosition;
            float power = Math.Max(0.2f, def.Power);

            if (ships != null)
            {
                for (int i = 0; i < ships.Count; i++)
                {
                    Ship target = ships[i];
                    if (target == null || target == ship || target.State != ShipState.Alive || target.Hp <= 0f) continue;
                    float distance = Vec2.Distance(anchor, target.Position);
                    if (distance > radius || distance <= 0.0001f) continue;
                    Vec2 toward = (anchor - target.Position) / distance;
                    target.Position += toward * 170f * power * dt;
                    target.Velocity *= MathGalaxy.Clamp(1f - 0.7f * dt, 0f, 1f);
                    target.Shield = MathGalaxy.Clamp(target.Shield - 10f * power * dt, 0f, Math.Max(0f, target.MaxShield));
                }
            }

            if (projectiles != null)
            {
                for (int i = 0; i < projectiles.Count; i++)
                {
                    Projectile p = projectiles[i];
                    if (p == null || !p.Active) continue;
                    float distance = Vec2.Distance(anchor, p.Position);
                    if (distance > radius || distance <= 0.0001f) continue;
                    Vec2 toward = (anchor - p.Position) / distance;
                    Vec2 current = p.Velocity.SqrMagnitude > 0.0001f ? p.Velocity.Normalized : toward;
                    p.Velocity = Vec2.Lerp(current, toward, MathGalaxy.Clamp(power * dt * 2f, 0f, 1f)).Normalized
                        * p.Velocity.Magnitude;
                    p.Lifetime -= 0.35f * dt;
                    if (p.Lifetime <= 0f) p.Deactivate();
                }
            }
        }

        public static void Update(float dt, List<Ship> ships, Arena arena, List<Projectile> projectiles, SimLog log, float time)
        {
            if (dt <= 0f || ships == null) return;
            int max = (int)AbilityKind.Phase + 1;

            for (int i = 0; i < ships.Count; i++)
            {
                Ship ship = ships[i];
                if (ship == null) continue;
                if (ship.Cooldowns == null || ship.Cooldowns.Length < max) ship.Cooldowns = new float[max];
                if (ship.EffectTimers == null || ship.EffectTimers.Length < max) ship.EffectTimers = new float[max];

                bool shieldWasActive = ship.EffectTimers[(int)AbilityKind.Shield] > 0f;
                bool overdriveWasActive = ship.EffectTimers[(int)AbilityKind.Overdrive] > 0f;
                for (int k = 0; k < max; k++)
                {
                    if (ship.Cooldowns[k] > 0f) ship.Cooldowns[k] = Math.Max(0f, ship.Cooldowns[k] - dt);
                    if (ship.EffectTimers[k] > 0f) ship.EffectTimers[k] = Math.Max(0f, ship.EffectTimers[k] - dt);
                }

                if (shieldWasActive && ship.EffectTimers[(int)AbilityKind.Shield] <= 0f && ship.ShieldBonusActive)
                {
                    ship.ShieldBonusActive = false;
                    ship.AbilityShieldBonus = 0f;
                    ship.RecalculateStats(null);
                }
                if (overdriveWasActive && ship.EffectTimers[(int)AbilityKind.Overdrive] <= 0f)
                    ship.RecalculateStats(null);

                if (ship.State != ShipState.Alive || ship.Hp <= 0f) continue;

                if (ship.NovaPending) NovaBurst(ship, ships, projectiles, log, time);
                if (IsActive(ship, AbilityKind.Magnet)) MagnetPull(ship, arena, dt);
                if (IsActive(ship, AbilityKind.BlackHole)) BlackHolePull(ship, ships, projectiles, dt);
                if (IsActive(ship, AbilityKind.Phase))
                {
                    ship.Invulnerability = Math.Max(ship.Invulnerability, Math.Min(0.2f, dt * 1.5f));
                    ship.RecalculateStats(null);
                }
            }
        }
    }
}
