using System;
using System.Collections.Generic;

namespace Orbit.Core
{
    public enum GameStatus { Ready, Playing, GameOver }

    public enum ModeKind { Classic, Zen, Sprint, Survival, Daily, Stack }

    public enum ObjectiveField { Score, Orbs, Seconds, Lines }

    public enum HazardKind { Rock, Comet, Mine, Wormhole, Gravity, Flare, Boss }

    public enum GameEventKind
    {
        Tap,
        BlockMove,
        BlockRotate,
        BlockHold,
        BlockDrop,
        BlockClear,
        BlockTopOut,
        Dodge,
        Orb,
        GoldenOrb,
        Artifact,
        PowerUp,
        ShieldBreak,
        Crash,
        LevelUp,
        NearMiss,
        Storm,
        Wormhole,
        Flare,
        BossSpawn,
        BossHit,
        BossDown,
        Resonance,
        Finish,
        Tick,
        PerkDraft
    }

    // Native order: Shield, Slow, Double, Magnet, Phase, Freeze, Revive.
    // The numeric order below is kept for compatibility with the existing Unity UI.
    public enum PowerUpKind { Slow, Double, Magnet, Phase, Freeze, Revive, Shield }

    public readonly struct GameEvent
    {
        public readonly GameEventKind Kind;
        public readonly int Value;

        public GameEvent(GameEventKind kind, int value = 0)
        {
            Kind = kind;
            Value = value;
        }
    }

    public sealed class ObjectiveDef
    {
        public string Title;
        public ObjectiveField Field;
        public int Target;

        /// <summary>Native string field: "score", "orbs" or "lines".</summary>
        public string NativeField;

        public ObjectiveDef(string title, ObjectiveField field, int target)
        {
            Title = title;
            Field = field;
            Target = target;
            NativeField = ToNativeField(field);
        }

        public ObjectiveDef(string title, ObjectiveField field, int target, string nativeField)
        {
            Title = title;
            Field = field;
            Target = target;
            NativeField = nativeField;
        }

        private static string ToNativeField(ObjectiveField field)
        {
            switch (field)
            {
                case ObjectiveField.Orbs: return "orbs";
                case ObjectiveField.Lines: return "lines";
                case ObjectiveField.Seconds: return "seconds";
                default: return "score";
            }
        }
    }

    public sealed class ModeDef
    {
        public string Id;
        public string Short;
        public string Title;
        public string Detail;
        public ObjectiveField Field;

        /// <summary>Native Mode.seconds: 90 for Zen, 60 for Sprint, 0 otherwise.</summary>
        public int Seconds;

        public ModeDef(string id, string shortName, string title, string detail, ObjectiveField field)
            : this(id, shortName, title, detail, field, 0)
        {
        }

        public ModeDef(string id, string shortName, string title, string detail, ObjectiveField field, int seconds)
        {
            Id = id;
            Short = shortName;
            Title = title;
            Detail = detail;
            Field = field;
            Seconds = seconds;
        }
    }

    public sealed class SectorDef
    {
        public int Id;
        public string Name;
        public string Subtitle;

        /// <summary>Native unlockLevel; 99 is the secret sector.</summary>
        public int UnlockLevel;
        public HazardKind[] Hazards;
        public float Density;

        public SectorDef(int id, string name, string subtitle)
            : this(id, name, subtitle, 1, new HazardKind[] { HazardKind.Rock }, 1f)
        {
        }

        public SectorDef(int id, string name, string subtitle, int unlockLevel, HazardKind[] hazards, float density)
        {
            Id = id;
            Name = name;
            Subtitle = subtitle;
            UnlockLevel = unlockLevel;
            Hazards = hazards ?? new HazardKind[] { HazardKind.Rock };
            Density = density;
        }

        public bool Has(HazardKind hazard)
        {
            for (int i = 0; i < Hazards.Length; i++) if (Hazards[i] == hazard) return true;
            return false;
        }
    }

    public sealed class PerkDef
    {
        /// <summary>Index into Content.Perks; used by PerkDraft.Options.</summary>
        public int Id;
        public string Glyph;
        public string Title;
        public string Detail;
        public string Risk;

        /// <summary>Native string id ("overclock", "barrier", ...).</summary>
        public string Key;

        public string NativeId
        {
            get { return Key; }
            set { Key = value; }
        }

        public PerkDef(int id, string glyph, string title, string detail, string risk)
            : this(id, glyph, title, detail, risk, "")
        {
        }

        public PerkDef(int id, string glyph, string title, string detail, string risk, string key)
        {
            Id = id;
            Glyph = glyph;
            Title = title;
            Detail = detail;
            Risk = risk;
            Key = key;
        }
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Title;
        public string Detail;
        public int Reward;
        public int Xp;

        public AchievementDef(string id, string title, string detail, int reward, int xp)
        {
            Id = id;
            Title = title;
            Detail = detail;
            Reward = reward;
            Xp = xp;
        }
    }

    public sealed class SeasonModifier
    {
        public string Title;
        public float Density;

        public SeasonModifier(string title, float density)
        {
            Title = title;
            Density = density;
        }
    }

    public sealed class ShopItem
    {
        public string Id;
        public string Title;
        public string Detail;
        public int Price;
        public string Category;
        public int Value;

        public ShopItem(string id, string title, string detail, int price, string category, int value)
        {
            Id = id;
            Title = title;
            Detail = detail;
            Price = price;
            Category = category;
            Value = value;
        }
    }

    public sealed class Secret
    {
        public string Id;
        public string Title;
        public string Hint;

        public Secret(string id, string title, string hint)
        {
            Id = id;
            Title = title;
            Hint = hint;
        }
    }

    public sealed class MissionTemplate
    {
        public string Id;
        public string Title;
        public string Field;
        public int Base;
        public int Reward;

        public MissionTemplate(string id, string title, string field, int baseValue, int reward)
        {
            Id = id;
            Title = title;
            Field = field;
            Base = baseValue;
            Reward = reward;
        }
    }

    public sealed class Mission
    {
        public string Id;
        public string Title;
        public string Field;
        public int Goal;
        public int Reward;

        public Mission(string id, string title, string field, int goal, int reward)
        {
            Id = id;
            Title = title;
            Field = field;
            Goal = goal;
            Reward = reward;
        }
    }

    public sealed class SeasonTier
    {
        public int Xp;
        public int Reward;

        public SeasonTier(int xp, int reward)
        {
            Xp = xp;
            Reward = reward;
        }
    }
}
