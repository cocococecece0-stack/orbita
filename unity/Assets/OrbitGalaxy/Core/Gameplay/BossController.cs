using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Spawns and drives the five authored bosses. Patterns are selected from BossDef,
    /// phases follow lost health, and weak spots open real damage windows.
    /// </summary>
    public sealed class BossController
    {
        private Rng _rng;
        private int _nextId = 900000;

        public BossController(Rng rng)
        {
            _rng = rng;
        }

        private static bool Has(string source, string fragment)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsShipId(List<Ship> ships, int id)
        {
            if (ships == null) return false;
            for (int i = 0; i < ships.Count; i++) if (ships[i] != null && ships[i].Id == id) return true;
            return false;
        }

        private static Ship FindShipById(List<Ship> ships, int id)
        {
            if (ships == null || id <= 0) return null;
            for (int i = 0; i < ships.Count; i++) if (ships[i] != null && ships[i].Id == id) return ships[i];
            return null;
        }

        private static WeaponKind WeaponFor(BossDef def)
        {
            if (def == null) return WeaponKind.Pulse;
            switch (def.Id)
            {
                case "boss_devourer": return WeaponKind.Pulse;
                case "boss_leviathan": return WeaponKind.Missile;
                case "boss_bastion": return WeaponKind.Orbital;
                case "boss_wraith": return WeaponKind.Lightning;
                case "boss_singularity": return WeaponKind.Plasma;
                default:
                    if (Has(def.AttackPattern, "турел")) return WeaponKind.Orbital;
                    if (Has(def.AttackPattern, "залп")) return WeaponKind.Missile;
                    if (Has(def.AttackPattern, "импульс")) return WeaponKind.Pulse;
                    return WeaponKind.Plasma;
            }
        }

        public Ship Spawn(BossDef def, Vec2 position, MatchSimulation match, SimLog log, float time)
        {
            if (def == null) return null;

            int id = _nextId++;
            while (ContainsShipId(match == null ? null : match.Ships, id)) id++;

            Ship boss = new Ship(id, def.Name, false, false, BotPersonality.Elite, position);
            boss.IsBoss = true;
            boss.BossDef = def;
            boss.Hull = def.Hull;
            boss.PrimaryHex = def.PrimaryHex;
            boss.AccentHex = "#FFFFFF";
            boss.Mass = def.Mass;
            boss.Radius = Ship.ComputeRadius(def.Mass);
            boss.Hp = def.Hp;
            boss.MaxHp = def.Hp;
            boss.Damage = def.Damage;
            boss.Speed = def.Speed;
            boss.Level = 4f + def.Phases * 2f;
            boss.XpToNext = GameConfig.XpPerLevelBase * def.Phases * 2f;
            boss.Weapon = WeaponFor(def);
            boss.FireInterval = Math.Max(0.12f, GameConfig.BaseFireInterval
                * ContentDatabase.FindWeapon(boss.Weapon).IntervalMul * 0.85f);
            boss.PickupRadius = 0f;
            boss.Shield = def.Hp * (Has(def.AttackPattern, "щит") ? 0.2f : 0.08f);
            boss.MaxShield = boss.Shield;
            boss.Facing = position.SqrMagnitude > 0.0001f ? -position.Normalized : Vec2.Right;
            boss.State = ShipState.Alive;
            boss.BossPhase = 1;
            boss.BossAttackTimer = 2.4f;
            boss.Drones = def.Phases >= 4 ? 2 : 0;
            boss.ExtraProjectiles = def.Phases >= 3 ? 1 : 0;
            boss.ActiveAbility = Has(def.Weakness, "покоя") || Has(def.AttackPattern, "притяжение")
                ? AbilityKind.BlackHole : AbilityKind.Nova;
            boss.Score = 250f * def.Phases;
            boss.Rank = 1;

            if (match != null && match.Ships != null && !match.Ships.Contains(boss)) match.Ships.Add(boss);
            if (log != null)
            {
                log.Add(SimEventKind.BossSpawn, boss.Id, def.Phases, "БОСС: " + def.Name + " — " + def.Title,
                    boss.Position, time, def.Hp);
            }
            boss.Remember("Появление босса " + def.Name);
            return boss;
        }

        private static Ship FindTarget(Ship boss, List<Ship> ships)
        {
            Ship best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship s = ships[i];
                if (s == null || s == boss || s.IsBoss || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                float d = Vec2.DistanceSqr(boss.Position, s.Position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = s;
                }
            }
            return best;
        }

        private void PullNearby(Ship boss, List<Ship> ships, float phase)
        {
            float radius = 760f;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship s = ships[i];
                if (s == null || s == boss || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                float distance = Vec2.Distance(boss.Position, s.Position);
                if (distance > radius || distance <= 0.0001f) continue;
                Vec2 toward = (boss.Position - s.Position) / distance;
                s.Position += toward * (90f + 32f * phase) * 0.45f;
                s.Velocity *= MathGalaxy.Clamp(1f - 0.35f, 0f, 1f);
            }
        }

        private void Volley(Ship boss, Ship target, SimLog log, float time, int extra)
        {
            int shots = 3 + extra;
            float baseAngle = target != null ? (target.Position - boss.Position).Angle() : boss.Facing.Angle();
            for (int i = 0; i < shots; i++)
            {
                float angle = baseAngle + (i - (shots - 1) * 0.5f) * 0.2f + _rng.Range(-0.035f, 0.035f);
                boss.FireTimer = 0f;
                CombatSystem combat = _activeCombat;
                if (combat != null) combat.Fire(boss, Vec2.FromAngle(angle), log, time);
            }
        }

        private CombatSystem _activeCombat;

        private void PerformAttack(Ship boss, BossDef def, List<Ship> ships, Arena arena, SimLog log,
            float time, CombatSystem combat, Ship target)
        {
            _activeCombat = combat;
            string pattern = def.AttackPattern ?? string.Empty;
            int phase = boss.BossPhase;

            if (Has(pattern, "притяжение") || Has(pattern, "всасывание")) PullNearby(boss, ships, phase);
            if (Has(pattern, "импульс")) boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 1.6f);
            if (Has(pattern, "фаза") || Has(pattern, "невидимости"))
            {
                boss.Invulnerability = Math.Max(boss.Invulnerability, 0.55f);
                if (target != null)
                {
                    Vec2 away = (target.Position - boss.Position).Normalized;
                    boss.Position = target.Position - away * 320f;
                    boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 1.1f);
                }
            }
            if (Has(pattern, "щит"))
            {
                boss.MaxShield = Math.Max(boss.MaxShield, def.Hp * 0.18f);
                boss.Shield = boss.MaxShield;
                if (phase >= 3) boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 2f);
            }
            if (Has(pattern, "таран") && target != null)
            {
                Vec2 charge = (target.Position - boss.Position).Normalized;
                boss.Position += charge * (140f + 24f * phase);
                CombatSystem.ApplyDamage(target, boss.Damage * (1.2f + 0.2f * phase), boss, log, time, false);
            }
            if (Has(pattern, "коллапс") || Has(pattern, "выброс"))
            {
                float radius = 430f;
                for (int i = 0; i < ships.Count; i++)
                {
                    Ship s = ships[i];
                    if (s == null || s == boss || s.State != ShipState.Alive || s.Hp <= 0f) continue;
                    if (Vec2.Distance(boss.Position, s.Position) > radius) continue;
                    CombatSystem.ApplyDamage(s, boss.Damage * (0.55f + 0.16f * phase), boss, log, time, false);
                }
            }
            if (Has(pattern, "покой"))
            {
                boss.Shield = 0f;
                boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 6f);
            }
            if (Has(pattern, "атаки") || Has(pattern, "залп") || Has(pattern, "турели")
                || Has(pattern, "импульс") || Has(pattern, "рыво") || Has(pattern, "выброс"))
            {
                Volley(boss, target, log, time, phase);
            }
            if (Has(pattern, "импульс") && combat != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    boss.FireTimer = 0f;
                    combat.Fire(boss, Vec2.FromAngle(i * (float)(Math.PI / 4.0)), log, time);
                }
            }
            if (Has(pattern, "атаки")) boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 0.9f);

            if (arena != null && !arena.IsInside(boss.Position)) boss.Position = arena.ClampToSafe(boss.Position);
            if (log != null)
            {
                log.Add(SimEventKind.Ability, boss.Id, phase, def.Name + ": " + pattern,
                    boss.Position, time, boss.BossAttackTimer);
            }
            boss.Remember("Атака: " + pattern);
            _activeCombat = null;
        }

        private static void RewardDeadBoss(Ship boss, List<Ship> ships, SimLog log, float time)
        {
            if (boss.BossRewarded) return;
            boss.BossRewarded = true;
            BossDef def = boss.BossDef;
            if (def == null) return;

            Ship killer = FindShipById(ships, boss.LastAttackerId);
            if (killer != null)
            {
                killer.Crystals += def.CrystalReward;
                killer.DarkMatter += def.DarkMatterReward;
                killer.Artifacts += def.ArtifactReward;
                killer.Score += 500f * def.Phases;
                killer.Remember("Повержен босс " + def.Name);
            }
            if (log != null)
            {
                log.Add(SimEventKind.Kill, killer == null ? boss.Id : killer.Id, boss.Id,
                    "БОСС ПОВЕРЖЕН: " + def.Name, boss.Position, time, 500f * def.Phases);
            }
        }

        public void Update(float dt, List<Ship> ships, Arena arena, SimLog log, float time, CombatSystem combat)
        {
            if (dt <= 0f || ships == null) return;

            for (int i = 0; i < ships.Count; i++)
            {
                Ship boss = ships[i];
                if (boss == null || !boss.IsBoss || boss.BossDef == null) continue;
                BossDef def = boss.BossDef;

                if (boss.State != ShipState.Alive || boss.Hp <= 0f)
                {
                    RewardDeadBoss(boss, ships, log, time);
                    continue;
                }

                if (boss.BossWeakWindow > 0f) boss.BossWeakWindow = Math.Max(0f, boss.BossWeakWindow - dt);

                float healthFraction = MathGalaxy.Clamp(boss.Hp / Math.Max(1f, boss.MaxHp), 0f, 1f);
                int desiredPhase = MathGalaxy.Clamp(1 + (int)((1f - healthFraction) * def.Phases), 1, def.Phases);
                if (desiredPhase > boss.BossPhase)
                {
                    boss.BossPhase = desiredPhase;
                    boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, 2.4f);
                    boss.Damage = def.Damage * (1f + 0.2f * (desiredPhase - 1));
                    boss.Invulnerability = Math.Max(boss.Invulnerability, 0.35f);
                    if (log != null)
                    {
                        log.Add(SimEventKind.Tick, boss.Id, desiredPhase, "ФАЗА " + desiredPhase,
                            boss.Position, time, healthFraction);
                    }
                    boss.Remember("Фаза " + desiredPhase);
                }

                if ((Has(def.Weakness, "покоя") && boss.BossPhase >= def.Phases)
                    || (Has(def.Weakness, "щит") && boss.BossPhase >= 3 && boss.Shield <= 0f))
                {
                    boss.BossWeakWindow = Math.Max(boss.BossWeakWindow, dt * 2f);
                }

                boss.Damage = def.Damage * (1f + 0.2f * (boss.BossPhase - 1));
                Ship target = FindTarget(boss, ships);
                if (target != null)
                {
                    Vec2 desired = (target.Position - boss.Position).Normalized;
                    boss.Facing = desired;
                    boss.AimInput = desired;
                    boss.MoveInput = desired;
                    boss.Velocity = desired * boss.Speed * (0.6f + 0.12f * boss.BossPhase);
                    boss.Position += desired * boss.Speed * dt * (0.22f + 0.04f * boss.BossPhase);
                }

                boss.BossAttackTimer -= dt;
                if (boss.BossAttackTimer <= 0f)
                {
                    PerformAttack(boss, def, ships, arena, log, time, combat, target);
                    boss.BossAttackTimer = Math.Max(1.15f, 3.6f - 0.42f * boss.BossPhase);
                }
                if (arena != null && !arena.IsInside(boss.Position)) boss.Position = arena.ClampToSafe(boss.Position);
            }
        }
    }
}
