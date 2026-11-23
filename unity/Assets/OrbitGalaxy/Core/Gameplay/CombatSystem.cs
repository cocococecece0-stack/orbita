using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Projectile simulation, contact combat, shields, kill bookkeeping and boss weak spots.
    /// Projectiles are recycled inside <see cref="Projectiles"/>.
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>Active projectiles only; spent instances are recycled in the internal pool.</summary>
        public List<Projectile> Projectiles;

        private readonly List<Projectile> _pool = new List<Projectile>(128);
        private Rng _rng;
        private int _nextProjectileId = 1;

        public CombatSystem(Rng rng)
        {
            _rng = rng;
            Projectiles = new List<Projectile>(128);
        }

        private Projectile Acquire()
        {
            Projectile p;
            if (_pool.Count > 0)
            {
                int last = _pool.Count - 1;
                p = _pool[last];
                _pool.RemoveAt(last);
            }
            else
            {
                p = new Projectile();
            }
            Projectiles.Add(p);
            return p;
        }

        private void ReleaseProjectile(int index)
        {
            Projectile p = Projectiles[index];
            p.Deactivate();
            Projectiles.RemoveAt(index);
            if (_pool.Count < 512) _pool.Add(p);
        }

        private static Vec2 Rotate(Vec2 v, float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        private void Spawn(Ship shooter, Vec2 origin, Vec2 direction, float damage, float speed,
            float radius, float homing, WeaponKind weapon, float lifetime)
        {
            Projectile p = Acquire();
            p.Reset(_nextProjectileId++, shooter.Id, origin, direction * speed, damage, radius,
                lifetime, weapon, shooter.IsPlayer, homing, 0);
        }

        public void Fire(Ship shooter, Vec2 direction, SimLog log, float time)
        {
            if (shooter == null || shooter.State != ShipState.Alive || shooter.Hp <= 0f) return;
            if (shooter.FireTimer > 0f) return;

            Vec2 dir = direction.SqrMagnitude > 0.0001f
                ? direction.Normalized
                : (shooter.Facing.SqrMagnitude > 0.0001f ? shooter.Facing.Normalized : Vec2.Up);
            shooter.Facing = dir;

            WeaponDef weapon = ContentDatabase.FindWeapon(shooter.Weapon);
            int count = MathGalaxy.Clamp(weapon.Projectiles + shooter.ExtraProjectiles, 1, 10);
            float damage = shooter.Damage * weapon.DamageMul / (1f + 0.22f * (count - 1));
            float speed = Math.Max(80f, GameConfig.BaseProjectileSpeed * weapon.SpeedMul);
            float radius = shooter.Weapon == WeaponKind.Missile ? 10f : 6f;
            float homing = shooter.Weapon == WeaponKind.Missile ? 2.8f : 0f;
            float lifetime = MathGalaxy.Clamp(GameConfig.ArenaRadius * 1.4f / speed, 1.2f, 5f);

            bool ring = shooter.Weapon == WeaponKind.Ring;
            float spread = weapon.Spread + 0.06f * (count - 1);
            for (int i = 0; i < count; i++)
            {
                float offset = ring
                    ? i * (float)(Math.PI * 2.0) / count
                    : (count > 1 ? (i / (float)(count - 1) - 0.5f) * spread : 0f);
                Vec2 shotDir = Rotate(dir, offset);
                Spawn(shooter, shooter.Position + shotDir * (shooter.Radius + radius), shotDir,
                    damage, speed, radius, homing, shooter.Weapon, lifetime);
            }

            for (int i = 0; i < shooter.Drones; i++)
            {
                float side = (i % 2 == 0) ? 1f : -1f;
                Vec2 perpendicular = dir.Perpendicular * side;
                Vec2 origin = shooter.Position + perpendicular * (shooter.Radius * 1.2f)
                    + dir * (shooter.Radius * 0.45f);
                Spawn(shooter, origin, dir, damage * 0.85f, speed * 1.08f, 5f, 4.5f,
                    WeaponKind.Drone, lifetime);
            }

            shooter.FireTimer = Math.Max(0f, shooter.FireInterval);
        }

        private static Ship FindShipById(List<Ship> ships, int id)
        {
            if (ships == null || id <= 0) return null;
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i] != null && ships[i].Id == id) return ships[i];
            }
            return null;
        }

        private static Ship FindHomingTarget(Projectile p, List<Ship> ships)
        {
            if (ships == null) return null;
            Ship current = p.TargetShipId > 0 ? FindShipById(ships, p.TargetShipId) : null;
            if (current != null && current.Id != p.OwnerId && current.State == ShipState.Alive) return current;

            Ship best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship s = ships[i];
                if (s == null || s.Id == p.OwnerId || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                float d = Vec2.DistanceSqr(p.Position, s.Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = s;
                }
            }
            if (best != null) p.TargetShipId = best.Id;
            return best;
        }

        private static float SegmentDistanceSqr(Vec2 a, Vec2 b, Vec2 point)
        {
            Vec2 ab = b - a;
            float lengthSqr = ab.SqrMagnitude;
            float t = lengthSqr > 0.000001f
                ? MathGalaxy.Clamp(Vec2.Dot(point - a, ab) / lengthSqr, 0f, 1f)
                : 0f;
            return Vec2.DistanceSqr(a + ab * t, point);
        }

        public void Update(float dt, List<Ship> ships, Arena arena, SimLog log, float time, AccountProfile profile)
        {
            if (dt <= 0f) return;

            if (ships != null)
            {
                for (int i = 0; i < ships.Count; i++)
                {
                    Ship s = ships[i];
                    if (s == null) continue;
                    if (s.FireTimer > 0f) s.FireTimer = Math.Max(0f, s.FireTimer - dt);
                    if (s.Invulnerability > 0f) s.Invulnerability = Math.Max(0f, s.Invulnerability - dt);
                }
            }

            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectile p = Projectiles[i];
                if (!p.Active || p.Lifetime <= 0f)
                {
                    ReleaseProjectile(i);
                    i--;
                    continue;
                }

                if (p.HomingStrength > 0f)
                {
                    Ship target = FindHomingTarget(p, ships);
                    if (target != null)
                    {
                        Vec2 desired = (target.Position - p.Position).Normalized;
                        Vec2 current = p.Velocity.SqrMagnitude > 0.0001f ? p.Velocity.Normalized : desired;
                        float blend = MathGalaxy.Clamp(p.HomingStrength * dt, 0f, 1f);
                        Vec2 steered = Vec2.Lerp(current, desired, blend).Normalized;
                        if (steered.SqrMagnitude > 0.0001f) p.Velocity = steered * p.Velocity.Magnitude;
                    }
                }

                p.PreviousPosition = p.Position;
                p.Position += p.Velocity * dt;
                p.Lifetime -= dt;
                if (p.Lifetime <= 0f)
                {
                    ReleaseProjectile(i);
                    i--;
                    continue;
                }
                if (arena != null && !arena.IsInside(p.Position))
                {
                    ReleaseProjectile(i);
                    i--;
                    continue;
                }

                bool consumed = false;
                if (ships != null)
                {
                    for (int j = 0; j < ships.Count; j++)
                    {
                        Ship target = ships[j];
                        // A shielded target must not silently eat projectiles: the shot passes
                        // through instead of producing a bogus "block" and losing its damage.
                        if (target == null || target.Id == p.OwnerId || target.State != ShipState.Alive ||
                            target.Hp <= 0f || target.Invulnerability > 0f) continue;
                        float reach = target.Radius + p.Radius;
                        if (SegmentDistanceSqr(p.PreviousPosition, p.Position, target.Position) > reach * reach) continue;
                        Ship source = FindShipById(ships, p.OwnerId);
                        ApplyDamage(target, p.Damage, source, log, time, true);
                        consumed = true;
                        break;
                    }
                }
                if (consumed)
                {
                    ReleaseProjectile(i);
                    i--;
                    continue;
                }
            }

            ResolveContacts(ships, log, time);
            ApplyHazards(ships, arena, log, time, dt);
        }

        private static void ResolveContacts(List<Ship> ships, SimLog log, float time)
        {
            if (ships == null) return;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship a = ships[i];
                if (a == null || a.State != ShipState.Alive || a.Hp <= 0f) continue;
                for (int j = i + 1; j < ships.Count; j++)
                {
                    Ship b = ships[j];
                    if (b == null || b.State != ShipState.Alive || b.Hp <= 0f) continue;

                    float reach = a.Radius + b.Radius;
                    float distanceSqr = Vec2.DistanceSqr(a.Position, b.Position);
                    if (distanceSqr > reach * reach) continue;

                    if (AbsorptionSystem.CanAbsorb(a, b)) AbsorptionSystem.TryAbsorb(a, b, log, time);
                    else if (AbsorptionSystem.CanAbsorb(b, a)) AbsorptionSystem.TryAbsorb(b, a, log, time);
                    else
                    {
                        if (b.Invulnerability <= 0f)
                            ApplyDamage(b, AbsorptionSystem.ContactDamage(a, b), a, log, time, false);
                        if (a.Invulnerability <= 0f)
                            ApplyDamage(a, AbsorptionSystem.ContactDamage(b, a), b, log, time, false);
                    }

                    float distance = (float)Math.Sqrt(distanceSqr);
                    Vec2 normal = distance > 0.0001f ? (b.Position - a.Position) / distance : Vec2.Right;
                    float push = (reach - distance) * 0.3f;
                    if (push > 0f)
                    {
                        a.Position -= normal * push;
                        b.Position += normal * push;
                    }
                }
            }
        }

        private static void ApplyHazards(List<Ship> ships, Arena arena, SimLog log, float time, float dt)
        {
            if (ships == null || arena == null) return;

            if (arena.Hazards != null)
            {
                for (int h = 0; h < arena.Hazards.Count; h++)
                {
                    HazardZone zone = arena.Hazards[h];
                    if (zone == null || !zone.Alive) continue;
                    for (int i = 0; i < ships.Count; i++)
                    {
                        Ship s = ships[i];
                        if (s == null || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                        if (AbilitySystem.IsActive(s, AbilityKind.Phase)) continue;
                        float reach = zone.Radius + s.Radius * 0.4f;
                        if (Vec2.DistanceSqr(zone.Position, s.Position) > reach * reach) continue;
                        ApplyDamage(s, zone.DamagePerSecond * dt, null, log, time, false);
                        if (zone.Slows) s.Velocity *= MathGalaxy.Clamp(1f - 0.5f * dt, 0f, 1f);
                    }
                }
            }

            if (arena.Anomalies != null)
            {
                for (int a = 0; a < arena.Anomalies.Count; a++)
                {
                    Anomaly anomaly = arena.Anomalies[a];
                    if (anomaly == null || !anomaly.Alive) continue;
                    for (int i = 0; i < ships.Count; i++)
                    {
                        Ship s = ships[i];
                        if (s == null || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                        float reach = anomaly.Radius + s.Radius;
                        if (Vec2.DistanceSqr(anomaly.Position, s.Position) > reach * reach) continue;
                        Vec2 pull = (anomaly.Position - s.Position).Normalized;
                        s.Position += pull * Math.Max(0f, anomaly.Pull) * dt * 0.18f;
                        if (anomaly.GrantsShield) s.Shield = Math.Min(s.MaxShield, s.Shield + 16f * dt);
                    }
                }
            }
        }

        private static bool ContainsText(string source, string fragment)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float BossWeakFactor(Ship boss, Ship source)
        {
            if (boss.BossWeakWindow > 0f) return 2.2f;
            BossDef def = boss.BossDef;
            if (def == null) return 1f;
            string weakness = def.Weakness ?? string.Empty;

            if (ContainsText(weakness, "покоя")) return boss.BossPhase >= 4 ? 2.4f : 0.55f;
            if (ContainsText(weakness, "щит")) return boss.BossPhase >= 3 || boss.Shield <= 0f ? 2.1f : 0.5f;
            if (ContainsText(weakness, "атаки")) return boss.BossAttackTimer <= 0.45f ? 2.2f : 0.45f;
            if (ContainsText(weakness, "импульса")) return boss.BossPhase >= 2 ? 1.9f : 0.6f;
            if (ContainsText(weakness, "зад"))
            {
                if (source == null) return 0.8f;
                Vec2 toSource = source.Position - boss.Position;
                Vec2 facing = boss.Facing.SqrMagnitude > 0.0001f ? boss.Facing.Normalized : Vec2.Right;
                return Vec2.Dot(toSource.Normalized, facing) < -0.2f ? 2f : 0.7f;
            }
            return 1f;
        }

        public static void ApplyDamage(Ship target, float amount, Ship source, SimLog log, float time, bool fromProjectile)
        {
            if (target == null || target.State != ShipState.Alive || target.Hp <= 0f) return;
            if (amount <= 0f || target.Invulnerability > 0f) return;

            float damage = amount;
            if (target.BossDef != null) damage *= BossWeakFactor(target, source) * (fromProjectile ? 1f : 0.85f);
            damage *= 1f - MathGalaxy.Clamp(target.DamageReduction, 0f, 0.9f);

            if (target.Shield > 0f)
            {
                float absorbed = Math.Min(target.Shield, damage * GameConfig.ShieldAbsorption);
                target.Shield -= absorbed;
                damage -= absorbed;
            }
            if (damage <= 0f)
            {
                if (log != null)
                {
                    log.Add(SimEventKind.Damage, target.Id, source == null ? -1 : source.Id,
                        "БЛОК ЩИТА", target.Position, time, 0f);
                }
                return;
            }

            target.Hp -= damage;
            target.LastAttackerId = source == null ? -1 : source.Id;
            if (source != null && source.Lifesteal > 0f) source.Heal(damage * source.Lifesteal);

            if (log != null)
            {
                log.Add(SimEventKind.Damage, target.Id, source == null ? -1 : source.Id,
                    "-" + damage.ToString("F0"), target.Position, time, damage);
            }

            if (target.Hp <= 0f) HandleKill(target, source, log, time);
        }

        /// <summary>
        /// Single death path for every source of lethal damage (projectiles, contacts, hazards,
        /// the arena border and snake bodies). The match layer also calls it for zero-HP ships so
        /// no damage source can leave an "alive" ship with no health. "KillerMayBeDying" is used by
        /// simultaneous snake hits: both kills are real even though the first call changes one
        /// ship's state before the second is registered.
        /// </summary>
        public static void HandleKill(Ship victim, Ship killer, SimLog log, float time,
            bool killerMayBeDying = false)
        {
            if (victim == null || victim.State != ShipState.Alive) return;

            Ship credited = killer;
            if (credited == null || credited == victim) credited = null;
            else if (credited.State != ShipState.Alive && !killerMayBeDying) credited = null;

            victim.Hp = 0f;
            victim.Shield = 0f;
            victim.State = ShipState.Respawning;
            victim.RespawnTimer = GameConfig.RespawnDelay;
            victim.Invulnerability = 0f;
            victim.FireTimer = 0f;
            victim.BossWeakWindow = 0f;
            // Death cancels running abilities. RespawnDelay (3.2s) is shorter than several effect
            // durations, so without this a ship could respawn still overdriven/magnetised/phased,
            // and GrowthSystem.Update would refill Shield = MaxShield from a lingering shield bonus.
            // Cooldowns are kept so abilities still need to recover after returning.
            if (victim.EffectTimers != null)
                for (int k = 0; k < victim.EffectTimers.Length; k++) victim.EffectTimers[k] = 0f;
            victim.AbilityShieldBonus = 0f;
            victim.ShieldBonusActive = false;
            victim.NovaPending = false;
            victim.BlackHolePosition = Vec2.Zero;
            victim.RecalculateStats(null);
            victim.Deaths++;
            victim.DeathAttackerId = credited == null ? -1 : credited.Id;
            // A death always breaks the victim's streak, even when nobody is credited with the kill.
            victim.Streak = 0;
            victim.MultiKill = 0;
            victim.LastKillTime = -1000f;
            victim.Remember("Уничтожен");

            // BossController emits its own dedicated "БОСС ПОВЕРЖЕН" line without an attacker;
            // suppressing this duplicate keeps the feed readable.
            if (log != null && (!victim.IsBoss || credited != null))
            {
                string text = credited == null ? "ПОГИБ: " + victim.Name : "УНИЧТОЖЕН: " + victim.Name;
                log.Add(SimEventKind.Kill, credited == null ? -1 : credited.Id, victim.Id, text,
                    victim.Position, time, credited == null ? 0f : credited.Score);
            }

            if (credited == null) return;

            credited.Kills++;
            credited.Score += 40f + victim.Mass * 1.5f + (victim.IsBoss ? 600f : 0f);
            if (time - credited.LastKillTime <= GameConfig.MultiKillTime && credited.LastKillTime > 0f) credited.MultiKill++;
            else credited.MultiKill = 1;
            credited.LastKillTime = time;
            credited.Streak++;
            if (credited.Streak > credited.BestStreak) credited.BestStreak = credited.Streak;
            if (credited.State == ShipState.Alive) credited.Heal(Math.Max(4f, victim.Mass * 0.35f));

            if (log != null)
            {
                if (victim.Rank == 1) log.Add(SimEventKind.LeaderKilled, credited.Id, victim.Id,
                    "ПОВЕРЖЕН ЛИДЕР", victim.Position, time);
                if (credited.Streak > 0 && credited.Streak % 3 == 0)
                {
                    log.Add(SimEventKind.Streak, credited.Id, credited.Streak,
                        "СЕРИЯ " + credited.Streak.ToString(), credited.Position, time, credited.Streak);
                }
            }
            credited.Remember("Уничтожен " + victim.Name);
        }
    }
}
