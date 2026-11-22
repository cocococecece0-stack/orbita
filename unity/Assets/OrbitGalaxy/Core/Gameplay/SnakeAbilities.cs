using System;

namespace Orbit.Galaxy
{
    /// <summary>Five readable snake abilities. Each reuses an existing, tested effect.</summary>
    public enum SnakeAbilitySlot
    {
        Impulse = 0,
        Shield = 1,
        Magnet = 2,
        Trail = 3,
        PhaseDash = 4
    }

    public sealed class SnakeAbilityDef
    {
        public SnakeAbilitySlot Slot;
        public AbilityKind Kind;
        public string Name;
        public string Purpose;
        public string Shortcut;
        public float Cost;
        public float Cooldown;
        public float Duration;

        public SnakeAbilityDef(SnakeAbilitySlot slot, AbilityKind kind, string name, string purpose,
            string shortcut, float cost, float cooldown, float duration)
        {
            Slot = slot;
            Kind = kind;
            Name = name;
            Purpose = purpose;
            Shortcut = shortcut;
            Cost = cost;
            Cooldown = cooldown;
            Duration = duration;
        }
    }

    /// <summary>
    /// Player-facing ability layer for the cosmic snake. Abilities are optional tools: energy still
    /// regenerates normally, and no ability is required to collect resources or finish a match.
    /// Costs and cooldowns are bounded here so an early lead cannot become near-invulnerability.
    /// </summary>
    public static class SnakeAbilities
    {
        public const int Count = 5;

        public static readonly SnakeAbilityDef[] All =
        {
            new SnakeAbilityDef(SnakeAbilitySlot.Impulse, AbilityKind.Dash, "ИМПУЛЬС",
                "Короткий рывок вперёд с кратким иммунитетом.", "1 / Q", 12f, 6.5f, 0.24f),
            new SnakeAbilityDef(SnakeAbilitySlot.Shield, AbilityKind.Shield, "ЩИТ",
                "Временный барьер, поглощающий урон.", "2 / E", 18f, 13f, 4.2f),
            new SnakeAbilityDef(SnakeAbilitySlot.Magnet, AbilityKind.Magnet, "МАГНИТ",
                "Притягивает ресурсы рядом со змейкой.", "3 / E", 10f, 18f, 6f),
            new SnakeAbilityDef(SnakeAbilitySlot.Trail, AbilityKind.Overdrive, "ЭНЕРГЕТИЧЕСКИЙ СЛЕД",
                "Короткий разгон и яркий светящийся хвост.", "4 / E", 22f, 20f, 4.5f),
            new SnakeAbilityDef(SnakeAbilitySlot.PhaseDash, AbilityKind.Phase, "ФАЗОВЫЙ РЫВОК",
                "Короткий проход сквозь опасности и тела.", "5 / E", 16f, 22f, 1.4f)
        };

        public static SnakeAbilityDef Get(SnakeAbilitySlot slot)
        {
            int index = (int)slot;
            return index >= 0 && index < All.Length ? All[index] : null;
        }

        /// <summary>Resolves the active ability selected in the HUD to a snake slot.</summary>
        public static SnakeAbilitySlot SlotFor(AbilityKind kind)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind) return All[i].Slot;
            }
            return SnakeAbilitySlot.Impulse;
        }

        public static bool CanActivate(Ship ship, SnakeAbilitySlot slot)
        {
            SnakeAbilityDef def = Get(slot);
            if (def == null || !AbilitySystem.CanActivate(ship, def.Kind)) return false;
            return ship.Energy >= def.Cost;
        }

        /// <summary>
        /// Activates a snake ability with snake-specific balance. The shared effect code remains the
        /// single source of truth for damage, pull, shielding and invulnerability.
        /// </summary>
        public static bool Activate(Ship ship, SnakeAbilitySlot slot, SimLog log, float time)
        {
            SnakeAbilityDef def = Get(slot);
            if (!CanActivate(ship, slot)) return false;
            if (!AbilitySystem.Activate(ship, def.Kind, log, time, def.Name)) return false;

            int index = (int)def.Kind;
            float extraCost = def.Cost - AbilitySystem.BaseEnergyCost(def.Kind);
            if (extraCost > 0f)
                ship.Energy = MathGalaxy.Clamp(ship.Energy - extraCost, 0f, GameConfig.MaxEnergy);
            ship.Cooldowns[index] = def.Cooldown;
            ship.EffectTimers[index] = def.Duration;

            if (slot == SnakeAbilitySlot.PhaseDash)
            {
                // The shared Phase definition is longer than the snake dash: keep the effect short.
                ship.Invulnerability = Math.Max(ship.Invulnerability, def.Duration);
                ship.RecalculateStats(null);
            }
            else if (slot == SnakeAbilitySlot.Trail)
            {
                ship.RecalculateStats(null);
            }
            return true;
        }

        public static bool IsTrailActive(Ship ship)
        {
            return ship != null && AbilitySystem.IsActive(ship, AbilityKind.Overdrive);
        }

        public static string Status(Ship ship, SnakeAbilitySlot slot)
        {
            SnakeAbilityDef def = Get(slot);
            if (def == null || ship == null || ship.Cooldowns == null) return string.Empty;
            int index = (int)def.Kind;
            if (index < 0 || index >= ship.Cooldowns.Length) return "НЕТ";
            float cd = ship.Cooldowns[index];
            if (cd > 0.05f) return cd.ToString("F1") + "с";
            return ship.Energy >= def.Cost ? "ГОТОВ" : "ЭНЕРГИЯ";
        }
    }
}
