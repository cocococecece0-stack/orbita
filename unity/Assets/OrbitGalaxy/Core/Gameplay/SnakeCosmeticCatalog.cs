using System;

namespace Orbit.Galaxy
{
    /// <summary>Readable body patterns used by the arena renderer for unlockable energy tails.</summary>
    public enum SnakeTailStyle
    {
        Glow,
        Pulse,
        Spark,
        Ribbon,
        Spectrum
    }

    /// <summary>Cosmetic-only snake look. It never changes stats, collision size or drop value.</summary>
    public sealed class SnakeCosmeticDef
    {
        public string Id;
        public string Name;
        public string Category; // head or tail
        public string Hint;
        public string GoalId;
        public HullShape Hull;
        public string PrimaryHex;
        public string AccentHex;
        public SnakeTailStyle Style;
        public bool Rare;

        public SnakeCosmeticDef(string id, string name, string category, string hint, string goalId,
            HullShape hull, string primary, string accent, SnakeTailStyle style, bool rare)
        {
            Id = id;
            Name = name;
            Category = category;
            Hint = hint;
            GoalId = goalId;
            Hull = hull;
            PrimaryHex = primary;
            AccentHex = accent;
            Style = style;
            Rare = rare;
        }
    }

    /// <summary>
    /// Unlockable snake looks: color variants, two-tone gradients, unusual heads, movement effects,
    /// energy tails and a small set of rare variants. All rewards come from in-match goals.
    /// </summary>
    public static class SnakeCosmeticCatalog
    {
        public static readonly SnakeCosmeticDef[] All =
        {
            new SnakeCosmeticDef("snake_head_aurora", "АВРОРА", "head",
                "Достигните длины 30 сегментов.", "snake_goal_length_30",
                HullShape.Ring, "#6CF6D2", "#B6FF3D", SnakeTailStyle.Glow, false),
            new SnakeCosmeticDef("snake_head_magma", "МАГМА", "head",
                "Продержитесь 180 секунд.", "snake_goal_survive_180",
                HullShape.Bulwark, "#FF8166", "#FFD54A", SnakeTailStyle.Pulse, false),
            new SnakeCosmeticDef("snake_head_prism", "ПРИЗМА", "head",
                "Выполните 25 змеиных способностей.", "snake_goal_ability_25",
                HullShape.Spike, "#9D7BFF", "#3DE6FF", SnakeTailStyle.Spark, false),
            new SnakeCosmeticDef("snake_head_voidglass", "СТЕКЛО ПУСТОТЫ", "head",
                "Достигните 3 уровня роста змейки.", "snake_goal_growth_3",
                HullShape.Core, "#FF4D7E", "#261044", SnakeTailStyle.Ribbon, true),
            new SnakeCosmeticDef("snake_head_neon", "НЕОНОВЫЙ КОД", "head",
                "Достигните длины 80 сегментов.", "snake_goal_length_80",
                HullShape.Hex, "#3DE6FF", "#FFFFFF", SnakeTailStyle.Spectrum, true),

            new SnakeCosmeticDef("snake_tail_comet", "КОМЕТА", "tail",
                "Достигните 3 уровня роста змейки.", "snake_goal_growth_3",
                HullShape.Dart, "#FFD54A", "#FF963D", SnakeTailStyle.Spark, false),
            new SnakeCosmeticDef("snake_tail_pulse", "ПУЛЬС", "tail",
                "Продержитесь 180 секунд.", "snake_goal_survive_180",
                HullShape.Dart, "#3DE6FF", "#6CF6D2", SnakeTailStyle.Pulse, false),
            new SnakeCosmeticDef("snake_tail_ribbon", "ЛЕНТА", "tail",
                "Достигните длины 30 сегментов.", "snake_goal_length_30",
                HullShape.Dart, "#B6FF3D", "#9D7BFF", SnakeTailStyle.Ribbon, false),
            new SnakeCosmeticDef("snake_tail_spectrum", "СПЕКТР", "tail",
                "Выполните 25 змеиных способностей.", "snake_goal_ability_25",
                HullShape.Dart, "#9D7BFF", "#3DE6FF", SnakeTailStyle.Spectrum, true),
            new SnakeCosmeticDef("snake_tail_eclipse", "ЗАТМЕНИЕ", "tail",
                "Достигните длины 80 сегментов.", "snake_goal_length_80",
                HullShape.Dart, "#FF4D7E", "#261044", SnakeTailStyle.Pulse, true)
        };

        public static SnakeCosmeticDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase)) return All[i];
            }
            return null;
        }

        public static SnakeCosmeticDef FindHead(string id)
        {
            SnakeCosmeticDef def = Find(id);
            return def != null && def.Category == "head" ? def : null;
        }

        public static SnakeCosmeticDef FindTail(string id)
        {
            SnakeCosmeticDef def = Find(id);
            return def != null && def.Category == "tail" ? def : null;
        }
    }
}
