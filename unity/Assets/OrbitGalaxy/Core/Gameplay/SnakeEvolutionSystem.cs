using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>Five bounded growth directions for the cosmic snake.</summary>
    public enum SnakeGrowthPath
    {
        Collection,
        Acceleration,
        Size,
        Protection,
        Magnet
    }

    /// <summary>One step of a snake growth path. Values are deliberately small and capped at tier 3.</summary>
    public sealed class SnakeUpgradeDef
    {
        public string Id;
        public string Name;
        public string Description;
        public SnakeGrowthPath Path;
        public int Tier;
        public float Value;
        public EvolutionBranch Branch;
        public HullShape Hull;

        public SnakeUpgradeDef(string id, string name, string description, SnakeGrowthPath path,
            int tier, float value, EvolutionBranch branch, HullShape hull)
        {
            Id = id;
            Name = name;
            Description = description;
            Path = path;
            Tier = tier;
            Value = value;
            Branch = branch;
            Hull = hull;
        }
    }

    /// <summary>
    /// Gradual, optional snake development. Every tier offers three different paths and the player
    /// picks one, so a match never turns into an automatic stat climb. Upgrades live only for the
    /// current match: account progression unlocks looks and goals, never permanent power.
    /// </summary>
    public static class SnakeEvolutionSystem
    {
        public const string OfferPrefix = "snake_";
        public const int MaxTier = 3;

        private static readonly int[] LevelThresholds = { 2, 6, 10 };

        public static readonly SnakeUpgradeDef[] All =
        {
            // Collection: more energy from the same motes, never more mass (no snowball).
            new SnakeUpgradeDef("snake_collect_1", "СБОРЩИК", "Энергия с собранных частиц +8%.", SnakeGrowthPath.Collection, 1, 0.08f, EvolutionBranch.Swarm, HullShape.Ring),
            new SnakeUpgradeDef("snake_collect_2", "ПОГЛОТИТЕЛЬ", "Энергия с собранных частиц +9%.", SnakeGrowthPath.Collection, 2, 0.09f, EvolutionBranch.Swarm, HullShape.Ring),
            new SnakeUpgradeDef("snake_collect_3", "АККУМУЛЯТОР", "Энергия с собранных частиц +10%.", SnakeGrowthPath.Collection, 3, 0.10f, EvolutionBranch.Swarm, HullShape.Hex),

            // Acceleration: steering and velocity response, not raw top speed or damage.
            new SnakeUpgradeDef("snake_accel_1", "МАНЁВР", "Поворот и разгон змейки +10%.", SnakeGrowthPath.Acceleration, 1, 0.10f, EvolutionBranch.Phantom, HullShape.Blade),
            new SnakeUpgradeDef("snake_accel_2", "ИНЕРЦИЯ", "Поворот и разгон змейки +11%.", SnakeGrowthPath.Acceleration, 2, 0.11f, EvolutionBranch.Phantom, HullShape.Blade),
            new SnakeUpgradeDef("snake_accel_3", "ВИХРЬ", "Поворот и разгон змейки +12%.", SnakeGrowthPath.Acceleration, 3, 0.12f, EvolutionBranch.Phantom, HullShape.Spike),

            // Size: longer body is a real trade-off — more reach, more tail to protect.
            new SnakeUpgradeDef("snake_size_1", "РОСТ", "Максимальная длина +2 сегмента.", SnakeGrowthPath.Size, 1, 2f, EvolutionBranch.Titan, HullShape.Bulwark),
            new SnakeUpgradeDef("snake_size_2", "РАЗМАХ", "Максимальная длина +2 сегмента.", SnakeGrowthPath.Size, 2, 2f, EvolutionBranch.Titan, HullShape.Bulwark),
            new SnakeUpgradeDef("snake_size_3", "ЛЕВИАФАН", "Максимальная длина +3 сегмента.", SnakeGrowthPath.Size, 3, 3f, EvolutionBranch.Titan, HullShape.Hex),

            // Protection: a small regenerating shield, never damage immunity.
            new SnakeUpgradeDef("snake_guard_1", "ЗАЩИТА", "Бонус щита +6.", SnakeGrowthPath.Protection, 1, 6f, EvolutionBranch.Titan, HullShape.Bulwark),
            new SnakeUpgradeDef("snake_guard_2", "ПАНЦИРЬ", "Бонус щита +6.", SnakeGrowthPath.Protection, 2, 6f, EvolutionBranch.Titan, HullShape.Hex),
            new SnakeUpgradeDef("snake_guard_3", "БАСТИОН", "Бонус щита +8.", SnakeGrowthPath.Protection, 3, 8f, EvolutionBranch.Titan, HullShape.Hex),

            // Magnet: wider pickup reach so a crowded arena stays readable.
            new SnakeUpgradeDef("snake_magnet_1", "ПРИТЯЖЕНИЕ", "Радиус сбора +12%.", SnakeGrowthPath.Magnet, 1, 0.12f, EvolutionBranch.Singularity, HullShape.Core),
            new SnakeUpgradeDef("snake_magnet_2", "ПОЛЕ", "Радиус сбора +14%.", SnakeGrowthPath.Magnet, 2, 0.14f, EvolutionBranch.Singularity, HullShape.Core),
            new SnakeUpgradeDef("snake_magnet_3", "ГОРИЗОНТ", "Радиус сбора +16%.", SnakeGrowthPath.Magnet, 3, 0.16f, EvolutionBranch.Singularity, HullShape.Ring)
        };

        public static int NextLevel(Ship ship)
        {
            if (ship == null || ship.SnakeEvolutionTier >= MaxTier) return int.MaxValue;
            int tier = MathGalaxy.Clamp(ship.SnakeEvolutionTier, 0, MaxTier - 1);
            return LevelThresholds[tier];
        }

        public static bool IsSnakeOffer(EvolutionDef def)
        {
            return def != null && def.Id != null &&
                   def.Id.StartsWith(OfferPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static SnakeUpgradeDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return null;
        }

        private static int TakenPath(Ship ship, int tier)
        {
            if (ship == null || ship.SnakePathsTaken == null || tier < 1 || tier > MaxTier) return -1;
            return ship.SnakePathsTaken[tier - 1];
        }

        public static bool IsAvailable(Ship ship, SnakeUpgradeDef def)
        {
            if (ship == null || def == null || ship.SnakeEvolutionTier >= MaxTier) return false;
            if (def.Tier != ship.SnakeEvolutionTier + 1) return false;
            // Zero means the tier is still free; chosen paths are stored as 1..5.
            return TakenPath(ship, def.Tier) <= 0;
        }

        /// <summary>Three distinct paths from the next tier, in a deterministic random order.</summary>
        public static List<EvolutionDef> Offer(Ship ship, Rng rng)
        {
            List<EvolutionDef> result = new List<EvolutionDef>(3);
            if (ship == null || ship.SnakeEvolutionTier >= MaxTier) return result;

            List<SnakeUpgradeDef> pool = new List<SnakeUpgradeDef>();
            for (int i = 0; i < All.Length; i++)
            {
                if (IsAvailable(ship, All[i])) pool.Add(All[i]);
            }
            Rng local = rng;
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = local.NextInt(0, i + 1);
                SnakeUpgradeDef tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            for (int i = 0; i < pool.Count && result.Count < 3; i++) result.Add(ToEvolutionDef(pool[i]));
            return result;
        }

        /// <summary>Reuses the existing evolution screen without adding a protected event or enum.</summary>
        public static EvolutionDef ToEvolutionDef(SnakeUpgradeDef def)
        {
            if (def == null) return null;
            return new EvolutionDef(def.Id, def.Branch, def.Tier, def.Name, def.Description, def.Hull,
                1f, 1f, 1f, 1f, 1f, 1f, 1f, 0, 0, 0f, 0f, WeaponKind.Laser);
        }

        public static bool Choose(Ship ship, EvolutionDef def, SimLog log, float time)
        {
            if (!IsSnakeOffer(def)) return false;
            SnakeUpgradeDef upgrade = Find(def.Id);
            if (!IsAvailable(ship, upgrade)) return false;
            Apply(ship, upgrade);
            if (log != null)
            {
                log.Add(SimEventKind.Evolution, ship.Id, upgrade.Tier, upgrade.Name, ship.Position, time,
                    ship.SnakeEvolutionTier);
            }
            return true;
        }

        public static void Apply(Ship ship, SnakeUpgradeDef def)
        {
            if (!IsAvailable(ship, def)) return;
            if (ship.SnakePathsTaken == null || ship.SnakePathsTaken.Length < MaxTier)
            {
                int[] resized = new int[MaxTier];
                if (ship.SnakePathsTaken != null)
                {
                    for (int i = 0; i < ship.SnakePathsTaken.Length && i < resized.Length; i++)
                        resized[i] = ship.SnakePathsTaken[i];
                }
                ship.SnakePathsTaken = resized;
            }

            switch (def.Path)
            {
                case SnakeGrowthPath.Collection: ship.SnakePickupGainMul += def.Value; break;
                case SnakeGrowthPath.Acceleration: ship.SnakeAgilityMul += def.Value; break;
                case SnakeGrowthPath.Size: ship.SnakeLengthBonus += (int)def.Value; break;
                case SnakeGrowthPath.Protection: ship.SnakeShieldBonus += def.Value; break;
                case SnakeGrowthPath.Magnet: ship.SnakeMagnetMul += def.Value; break;
            }
            ship.SnakePathsTaken[def.Tier - 1] = (int)def.Path + 1;
            ship.SnakeEvolutionTier = def.Tier;
            ship.RecalculateStats(null);
            ship.Remember("Рост змейки: " + def.Name);
        }
    }
}
