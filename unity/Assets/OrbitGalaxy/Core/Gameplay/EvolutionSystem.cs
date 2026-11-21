using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Four evolution branches with three tiers each. Offers always contain three distinct,
    /// not-yet-taken choices from the next tier.
    /// </summary>
    public static class EvolutionSystem
    {
        private static int NextTier(Ship ship)
        {
            return ship == null ? 1 : MathGalaxy.Clamp(ship.EvolutionTier + 1, 1, 3);
        }

        public static int NextEvolutionLevel(Ship ship)
        {
            if (ship == null || ship.EvolutionTier >= 3) return int.MaxValue;
            if (ship.EvolutionTier <= 0) return 3;
            return ship.EvolutionTier == 1 ? 7 : 12;
        }

        private static bool AlreadyTaken(Ship ship, string id)
        {
            if (ship == null || ship.EvolutionsTaken == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < ship.EvolutionsTaken.Length; i++)
            {
                if (ship.EvolutionsTaken[i] != null
                    && string.Equals(ship.EvolutionsTaken[i].Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool IsAvailable(Ship ship, EvolutionDef def)
        {
            if (ship == null || def == null) return false;
            if (ship.EvolutionTier >= 3) return false;
            if (def.Tier != NextTier(ship)) return false;
            if (AlreadyTaken(ship, def.Id)) return false;
            if (ship.EvolutionsTaken != null && def.Tier >= 1 && def.Tier <= ship.EvolutionsTaken.Length
                && ship.EvolutionsTaken[def.Tier - 1] != null) return false;
            return true;
        }

        public static List<EvolutionDef> Offer(Ship ship, Rng rng)
        {
            List<EvolutionDef> result = new List<EvolutionDef>(3);
            if (ship == null || ship.EvolutionTier >= 3) return result;

            List<EvolutionDef> pool = new List<EvolutionDef>();
            for (int i = 0; i < ContentDatabase.Evolutions.Length; i++)
            {
                EvolutionDef def = ContentDatabase.Evolutions[i];
                if (IsAvailable(ship, def)) pool.Add(def);
            }

            Rng local = rng;
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = local.NextInt(0, i + 1);
                EvolutionDef tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            for (int i = 0; i < pool.Count && result.Count < 3; i++)
            {
                bool duplicate = false;
                for (int j = 0; j < result.Count; j++)
                {
                    if (string.Equals(result[j].Id, pool[i].Id, StringComparison.OrdinalIgnoreCase))
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) result.Add(pool[i]);
            }
            return result;
        }

        public static void Choose(Ship ship, EvolutionDef def, SimLog log, float time, AccountProfile profile)
        {
            if (!IsAvailable(ship, def)) return;
            Apply(ship, def, profile);
            if (log != null)
            {
                log.Add(SimEventKind.Evolution, ship.Id, def.Tier, def.Name, ship.Position, time, ship.EvolutionTier);
            }
        }

        public static void Apply(Ship ship, EvolutionDef def, AccountProfile profile)
        {
            if (ship == null || def == null || AlreadyTaken(ship, def.Id)) return;

            int tier = MathGalaxy.Clamp(def.Tier, 1, 3);
            float oldMaxHp = ship.MaxHp;
            float oldMaxShield = ship.MaxShield;

            ship.EvolutionMassMul *= Math.Max(0.1f, def.MassMul);
            ship.EvolutionSpeedMul *= Math.Max(0.1f, def.SpeedMul);
            ship.EvolutionDamageMul *= Math.Max(0.1f, def.DamageMul);
            ship.EvolutionHpMul *= Math.Max(0.1f, def.HpMul);
            ship.EvolutionFireMul *= Math.Max(0.1f, def.FireRateMul);
            ship.EvolutionPickupMul *= Math.Max(0.1f, def.PickupMul);
            ship.EvolutionShieldMul *= Math.Max(0.1f, def.ShieldMul);

            ship.Drones += Math.Max(0, def.Drones);
            ship.ExtraProjectiles += Math.Max(0, def.ExtraProjectiles);
            ship.GravityPower += Math.Max(0f, def.GravityPower);
            ship.Lifesteal += Math.Max(0f, def.Lifesteal);
            ship.Weapon = def.Weapon;
            ship.Hull = def.Hull;
            ship.Branch = def.Branch;
            ship.EvolutionTier = tier;
            ship.EvolutionId = def.Id;

            if (ship.EvolutionsTaken == null || ship.EvolutionsTaken.Length < 3)
            {
                EvolutionDef[] resized = new EvolutionDef[3];
                if (ship.EvolutionsTaken != null)
                {
                    for (int i = 0; i < ship.EvolutionsTaken.Length && i < resized.Length; i++)
                        resized[i] = ship.EvolutionsTaken[i];
                }
                ship.EvolutionsTaken = resized;
            }
            ship.EvolutionsTaken[tier - 1] = def;

            ship.AddMass(ship.Mass * (Math.Max(0.1f, def.MassMul) - 1f), null, 0f);
            ship.RecalculateStats(profile);
            ship.Heal(Math.Max(0f, ship.MaxHp - oldMaxHp));
            ship.Shield = Math.Min(ship.MaxShield, ship.Shield + Math.Max(0f, ship.MaxShield - oldMaxShield));
            ship.Remember("Эволюция: " + def.Name);
        }
    }
}
