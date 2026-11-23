using System;

namespace Orbit.Galaxy
{
    /// <summary>One snake-specific goal. Rewards are cosmetic or currency only, never power.</summary>
    public sealed class SnakeGoalDef
    {
        public string Id;
        public string Name;
        public string Description;
        public string StatKey;
        public int Target;
        public int Credits;
        public int Crystals;
        public string UnlockCosmetic;

        public SnakeGoalDef(string id, string name, string description, string statKey, int target,
            int credits, int crystals, string unlockCosmetic)
        {
            Id = id;
            Name = name;
            Description = description;
            StatKey = statKey;
            Target = target;
            Credits = credits;
            Crystals = crystals;
            UnlockCosmetic = unlockCosmetic;
        }
    }

    /// <summary>
    /// Long-term snake goals layered on the existing progression system. They use the same profile,
    /// stats, achievements and reward flow; no new save schema and no permanent stat bonus is added.
    /// </summary>
    public static class SnakeProgression
    {
        public const string StatBestLength = "snakeBestLength";
        public const string StatBestGrowth = "snakeBestGrowth";
        public const string StatBestSurvival = "snakeBestSurvival";
        public const string StatAbilityUses = "snakeAbilityUses";

        public static readonly SnakeGoalDef[] Goals =
        {
            new SnakeGoalDef("snake_goal_length_30", "ЗМЕЯ ИЗ 30 СЕГМЕНТОВ",
                "Достигните длины 30 в одном матче.", StatBestLength, 30, 350, 1, "snake_head_aurora"),
            new SnakeGoalDef("snake_goal_survive_180", "ДОЛГИЙ ПОЛЁТ",
                "Продержитесь 180 секунд.", StatBestSurvival, 180, 450, 1, "snake_tail_pulse"),
            new SnakeGoalDef("snake_goal_growth_3", "ПОЛНЫЙ РОСТ",
                "Достигните 3 уровня роста змейки.", StatBestGrowth, 3, 600, 2, "snake_head_voidglass"),
            new SnakeGoalDef("snake_goal_ability_25", "МАСТЕР СПОСОБНОСТЕЙ",
                "Используйте змеиные способности 25 раз.", StatAbilityUses, 25, 550, 2, "snake_tail_spectrum"),
            new SnakeGoalDef("snake_goal_length_40", "ЛЕВИАФАН",
                "Достигните длины 40 сегментов.", StatBestLength, 40, 1500, 5, "snake_head_neon")
        };

        public static SnakeGoalDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Goals.Length; i++)
            {
                if (string.Equals(Goals[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Goals[i];
            }
            return null;
        }

        /// <summary>Updates lifetime snake stats, then grants each newly completed cosmetic goal once.</summary>
        public static void Apply(AccountProfile profile, MatchResult result, ProgressionRewards rewards)
        {
            if (profile == null || result == null) return;

            SetMax(profile, StatBestLength, Math.Max(0, result.Length));
            SetMax(profile, StatBestGrowth, Math.Max(0, result.SnakeEvolutionTier));
            SetMax(profile, StatBestSurvival, (int)Math.Max(0f, result.SurvivalTime));
            profile.AddStat(StatAbilityUses, Math.Max(0, result.SnakeAbilityUses));
            SetRecord(profile, StatBestLength, profile.Stat(StatBestLength));
            SetRecord(profile, StatBestGrowth, profile.Stat(StatBestGrowth));
            SetRecord(profile, StatBestSurvival, profile.Stat(StatBestSurvival));
            SetRecord(profile, StatAbilityUses, profile.Stat(StatAbilityUses));

            if (rewards == null) return;
            for (int i = 0; i < Goals.Length; i++)
            {
                SnakeGoalDef def = Goals[i];
                if (profile.Achievements != null && profile.Achievements.Contains(def.Id)) continue;
                if (profile.Stat(def.StatKey) < def.Target) continue;

                profile.Achievements.Add(def.Id);
                profile.AddCredits(def.Credits);
                profile.AddCrystals(def.Crystals);
                profile.Unlock(def.UnlockCosmetic);
                // A goal can highlight one reward while also unlocking its matching head and tail.
                for (int j = 0; j < SnakeCosmeticCatalog.All.Length; j++)
                {
                    SnakeCosmeticDef cosmetic = SnakeCosmeticCatalog.All[j];
                    if (cosmetic != null &&
                        string.Equals(cosmetic.GoalId, def.Id, StringComparison.OrdinalIgnoreCase))
                        profile.Unlock(cosmetic.Id);
                }
                rewards.Credits += def.Credits;
                rewards.Crystals += def.Crystals;
                rewards.SnakeGoals.Add(def.Name);
                rewards.Messages.Add("ЦЕЛЬ ЗМЕЙКИ: " + def.Name);
            }
        }

        private static void SetMax(AccountProfile profile, string key, int value)
        {
            if (value <= profile.Stat(key)) return;
            profile.Stats[key] = value;
        }

        private static void SetRecord(AccountProfile profile, string key, int value)
        {
            if (profile.Records == null) return;
            int current;
            if (profile.Records.TryGetValue(key, out current) && current >= value) return;
            profile.Records[key] = value;
        }
    }
}
