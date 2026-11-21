using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    // ─────────────────────────── Definition types ───────────────────────────

    public sealed class ShipVisualDef
    {
        public string Id;
        public string Name;
        public HullShape Hull;
        public string PrimaryHex;
        public string AccentHex;
        public int EngineCount;

        public ShipVisualDef(string id, string name, HullShape hull, string primary, string accent, int engines)
        {
            Id = id; Name = name; Hull = hull; PrimaryHex = primary; AccentHex = accent; EngineCount = engines;
        }
    }

    public sealed class EvolutionDef
    {
        public string Id;
        public EvolutionBranch Branch;
        public int Tier;
        public string Name;
        public string Description;
        public HullShape Hull;
        public float MassMul;
        public float SpeedMul;
        public float DamageMul;
        public float HpMul;
        public float FireRateMul;
        public float PickupMul;
        public float ShieldMul;
        public int Drones;
        public int ExtraProjectiles;
        public float GravityPower;
        public float Lifesteal;
        public WeaponKind Weapon;

        public EvolutionDef(string id, EvolutionBranch branch, int tier, string name, string description,
            HullShape hull, float mass, float speed, float damage, float hp, float fireRate, float pickup,
            float shield, int drones, int projectiles, float gravity, float lifesteal, WeaponKind weapon)
        {
            Id = id; Branch = branch; Tier = tier; Name = name; Description = description; Hull = hull;
            MassMul = mass; SpeedMul = speed; DamageMul = damage; HpMul = hp; FireRateMul = fireRate;
            PickupMul = pickup; ShieldMul = shield; Drones = drones; ExtraProjectiles = projectiles;
            GravityPower = gravity; Lifesteal = lifesteal; Weapon = weapon;
        }
    }

    public sealed class AbilityDef
    {
        public AbilityKind Kind;
        public string Name;
        public string Description;
        public float Cooldown;
        public float Duration;
        public float Power;
        public float Radius;

        public AbilityDef(AbilityKind kind, string name, string description, float cooldown, float duration, float power, float radius)
        {
            Kind = kind; Name = name; Description = description; Cooldown = cooldown;
            Duration = duration; Power = power; Radius = radius;
        }
    }

    public sealed class WeaponDef
    {
        public WeaponKind Kind;
        public string Name;
        public float DamageMul;
        public float IntervalMul;
        public float SpeedMul;
        public int Projectiles;
        public float Spread;

        public WeaponDef(WeaponKind kind, string name, float damage, float interval, float speed, int projectiles, float spread)
        {
            Kind = kind; Name = name; DamageMul = damage; IntervalMul = interval;
            SpeedMul = speed; Projectiles = projectiles; Spread = spread;
        }
    }

    public sealed class BotProfile
    {
        public BotPersonality Personality;
        public string Name;
        public string Description;
        public float Aggression;
        public float Greed;
        public float Caution;
        public float Risk;
        public float AimAccuracy;
        public float Reaction;
        public float AbilityBias;
        public float PreferredRange;

        public BotProfile(BotPersonality personality, string name, string description, float aggression,
            float greed, float caution, float risk, float accuracy, float reaction, float abilityBias, float preferredRange)
        {
            Personality = personality; Name = name; Description = description; Aggression = aggression;
            Greed = greed; Caution = caution; Risk = risk; AimAccuracy = accuracy;
            Reaction = reaction; AbilityBias = abilityBias; PreferredRange = preferredRange;
        }
    }

    public sealed class BossDef
    {
        public string Id;
        public string Name;
        public string Title;
        public HullShape Hull;
        public string PrimaryHex;
        public float Mass;
        public float Hp;
        public float Damage;
        public float Speed;
        public int Phases;
        public string AttackPattern;
        public string Weakness;
        public int CrystalReward;
        public int DarkMatterReward;
        public int ArtifactReward;

        public BossDef(string id, string name, string title, HullShape hull, string primary, float mass,
            float hp, float damage, float speed, int phases, string pattern, string weakness,
            int crystals, int darkMatter, int artifacts)
        {
            Id = id; Name = name; Title = title; Hull = hull; PrimaryHex = primary; Mass = mass;
            Hp = hp; Damage = damage; Speed = speed; Phases = phases; AttackPattern = pattern;
            Weakness = weakness; CrystalReward = crystals; DarkMatterReward = darkMatter; ArtifactReward = artifacts;
        }
    }

    public sealed class ModeDef
    {
        public GameModeKind Kind;
        public string Name;
        public string Description;
        public int Participants;
        public float DurationMinutes;
        public float DangerScale;
        public float RewardScale;
        public bool ShrinkingArena;
        public bool Bosses;
        public bool ChaosEvents;
        public int Respawns;
        public Difficulty SuggestedDifficulty;

        public ModeDef(GameModeKind kind, string name, string description, int participants, float duration,
            float danger, float reward, bool shrinking, bool bosses, bool chaos, int respawns, Difficulty difficulty)
        {
            Kind = kind; Name = name; Description = description; Participants = participants;
            DurationMinutes = duration; DangerScale = danger; RewardScale = reward;
            ShrinkingArena = shrinking; Bosses = bosses; ChaosEvents = chaos; Respawns = respawns;
            SuggestedDifficulty = difficulty;
        }
    }

    public sealed class CosmeticDef
    {
        public string Id;
        public string Name;
        public string Category; // ship, trail, death, frame, avatar, title
        public string Hex;
        public int Cost;
        public string UnlockHint;

        public CosmeticDef(string id, string name, string category, string hex, int cost, string hint)
        {
            Id = id; Name = name; Category = category; Hex = hex; Cost = cost; UnlockHint = hint;
        }
    }

    public sealed class AchievementDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int Target;
        public int Credits;
        public int Crystals;

        public AchievementDef(string id, string name, string description, int target, int credits, int crystals)
        {
            Id = id; Name = name; Description = description; Target = target; Credits = credits; Crystals = crystals;
        }
    }

    public sealed class QuestDef
    {
        public string Id;
        public string Name;
        public string Field; // kills, energy, mass, crystals, matches, top3, wins, bosses
        public int Target;
        public int Credits;
        public int Crystals;
        public bool Weekly;

        public QuestDef(string id, string name, string field, int target, int credits, int crystals, bool weekly)
        {
            Id = id; Name = name; Field = field; Target = target; Credits = credits; Crystals = crystals; Weekly = weekly;
        }
    }

    public sealed class ResearchDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int MaxLevel;
        public int BaseCost;
        public float BonusPerLevel;

        public ResearchDef(string id, string name, string description, int maxLevel, int baseCost, float bonus)
        {
            Id = id; Name = name; Description = description; MaxLevel = maxLevel; BaseCost = baseCost; BonusPerLevel = bonus;
        }
    }

    // ───────────────────────────── Database ─────────────────────────────

    /// <summary>Authoritative content tables. Pure C# so unit tests and Unity share the same data.</summary>
    public static class ContentDatabase
    {
        public static readonly ShipVisualDef[] ShipVisuals =
        {
            new ShipVisualDef("ship_nova", "НОВА", HullShape.Dart, "#6CF6D2", "#B6FF3D", 2),
            new ShipVisualDef("ship_vesta", "ВЕСТА", HullShape.Wedge, "#FF8166", "#FFD54A", 2),
            new ShipVisualDef("ship_orion", "ОРИОН", HullShape.Hex, "#9D7BFF", "#3DE6FF", 3),
            new ShipVisualDef("ship_obsidian", "ОБСИДИАН", HullShape.Bulwark, "#5A678F", "#FF4D7E", 4),
            new ShipVisualDef("ship_spectre", "СПЕКТР", HullShape.Blade, "#3DE6FF", "#FFFFFF", 2),
            new ShipVisualDef("ship_helix", "ГЕЛИКС", HullShape.Ring, "#B6FF3D", "#9D7BFF", 3),
            new ShipVisualDef("ship_abyss", "БЕЗДНА", HullShape.Core, "#FF4D7E", "#261044", 4),
            new ShipVisualDef("ship_atlas", "АТЛАС", HullShape.Hex, "#FFD54A", "#FF963D", 4)
        };

        public static readonly EvolutionDef[] Evolutions =
        {
            // TITAN — heavy, durable, slow.
            new EvolutionDef("titan_1", EvolutionBranch.Titan, 1, "ТИТАН", "Корпус тяжелеет: больше масса и прочность, меньше манёвренность.", HullShape.Bulwark, 1.22f, 0.90f, 1.14f, 1.30f, 0.96f, 0.94f, 1.20f, 0, 0, 0f, 0f, WeaponKind.Plasma),
            new EvolutionDef("titan_2", EvolutionBranch.Titan, 2, "БАСТИОН", "Фронтальный щит и усиленные снаряды.", HullShape.Bulwark, 1.30f, 0.84f, 1.24f, 1.48f, 0.92f, 0.90f, 1.55f, 0, 1, 0f, 0f, WeaponKind.Missile),
            new EvolutionDef("titan_3", EvolutionBranch.Titan, 3, "КОЛОСС", "Орбитальные турели превращают корабль в крепость.", HullShape.Hex, 1.42f, 0.74f, 1.38f, 1.72f, 0.88f, 0.86f, 1.80f, 2, 2, 0f, 0f, WeaponKind.Orbital),

            // PHANTOM — fast, evasive, hunter.
            new EvolutionDef("phantom_1", EvolutionBranch.Phantom, 1, "ФАНТОМ", "Рывок чаще, скорость выше, корпус легче.", HullShape.Blade, 0.94f, 1.20f, 1.08f, 0.92f, 1.06f, 1.04f, 0.90f, 0, 0, 0f, 0f, WeaponKind.Laser),
            new EvolutionDef("phantom_2", EvolutionBranch.Phantom, 2, "СТАЛКЕР", "Специализация на охоте: урон по цели и точность.", HullShape.Spike, 0.90f, 1.30f, 1.28f, 0.90f, 1.12f, 1.02f, 0.84f, 0, 1, 0f, 0.06f, WeaponKind.Pulse),
            new EvolutionDef("phantom_3", EvolutionBranch.Phantom, 3, "МИРАЖ", "Фазовые рывки и аналогичный след.", HullShape.Blade, 0.86f, 1.44f, 1.34f, 0.88f, 1.18f, 1.00f, 0.78f, 0, 2, 0f, 0.10f, WeaponKind.Lightning),

            // SWARM — drones, satellites, area control.
            new EvolutionDef("swarm_1", EvolutionBranch.Swarm, 1, "РОЙ", "Первый дрон ведёт огонь по ближайшей цели.", HullShape.Ring, 1.04f, 1.02f, 1.04f, 1.04f, 1.02f, 1.10f, 1.00f, 1, 1, 0f, 0f, WeaponKind.Drone),
            new EvolutionDef("swarm_2", EvolutionBranch.Swarm, 2, "УЛЕЙ", "Два защитных спутника и сетевой огонь.", HullShape.Ring, 1.10f, 0.98f, 1.16f, 1.12f, 1.06f, 1.18f, 1.12f, 2, 2, 0f, 0f, WeaponKind.Drone),
            new EvolutionDef("swarm_3", EvolutionBranch.Swarm, 3, "МАТКА", "Рой дронов контролирует пространство вокруг корабля.", HullShape.Hex, 1.16f, 0.92f, 1.28f, 1.22f, 1.10f, 1.28f, 1.24f, 4, 3, 0f, 0.04f, WeaponKind.Ring),

            // SINGULARITY — gravity, absorption, control.
            new EvolutionDef("singularity_1", EvolutionBranch.Singularity, 1, "СИНГУЛЯРНОСТЬ", "Гравитация притягивает энергию и замедляет врагов.", HullShape.Core, 1.08f, 0.96f, 1.10f, 1.08f, 1.02f, 1.32f, 1.02f, 0, 0, 0.55f, 0.04f, WeaponKind.Pulse),
            new EvolutionDef("singularity_2", EvolutionBranch.Singularity, 2, "ИСТОЧНИК", "Поглощение энергии усиливает защиту и урон.", HullShape.Core, 1.14f, 0.92f, 1.22f, 1.16f, 1.04f, 1.52f, 1.18f, 1, 1, 0.85f, 0.08f, WeaponKind.Plasma),
            new EvolutionDef("singularity_3", EvolutionBranch.Singularity, 3, "ГОРИЗОНТ", "Импульсы контроля: притяжение, удержание и высасывание энергии.", HullShape.Core, 1.20f, 0.88f, 1.38f, 1.28f, 1.08f, 1.70f, 1.34f, 2, 2, 1.25f, 0.12f, WeaponKind.Orbital)
        };

        public static readonly AbilityDef[] Abilities =
        {
            new AbilityDef(AbilityKind.Dash, "РЫВОК", "Мгновенный рывок вперёд с кратким иммунитетом.", 6.5f, 0.24f, 480f, 0f),
            new AbilityDef(AbilityKind.Shield, "ЩИТ", "Временный барьер, поглощающий урон.", 13f, 4.2f, 75f, 0f),
            new AbilityDef(AbilityKind.Nova, "НОВА", "Взрыв вокруг корабля, наносящий урон и выталкивающий врагов.", 16f, 0.5f, 68f, 260f),
            new AbilityDef(AbilityKind.Magnet, "МАГНИТ", "Притягивает ресурсы в радиусе.", 18f, 6f, 1.0f, 620f),
            new AbilityDef(AbilityKind.Overdrive, "ПЕРЕГРУЗКА", "Временное увеличение скорострельности и скорости.", 20f, 6f, 1.45f, 0f),
            new AbilityDef(AbilityKind.BlackHole, "ЧЁРНАЯ ДЫРА", "Создаёт гравитационный колодец, удерживающий врагов.", 26f, 5f, 1.1f, 480f),
            new AbilityDef(AbilityKind.Phase, "ФАЗА", "Прохождение сквозь астероиды и опасные зоны.", 22f, 3.2f, 1f, 0f)
        };

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef(WeaponKind.Laser, "ЛАЗЕР", 1f, 1f, 1f, 1, 0f),
            new WeaponDef(WeaponKind.Plasma, "ПЛАЗМА", 1.22f, 1.12f, 0.94f, 1, 0f),
            new WeaponDef(WeaponKind.Missile, "РАКЕТА", 1.70f, 1.55f, 0.78f, 1, 0f),
            new WeaponDef(WeaponKind.Drone, "ДРОН", 0.82f, 0.86f, 1.08f, 2, 0.18f),
            new WeaponDef(WeaponKind.Ring, "КОЛЬЦО", 0.74f, 0.90f, 0.86f, 4, 0.55f),
            new WeaponDef(WeaponKind.Pulse, "ИМПУЛЬС", 1.12f, 0.94f, 1.18f, 1, 0f),
            new WeaponDef(WeaponKind.Lightning, "РАЗРЯД", 1.32f, 1.04f, 1.30f, 2, 0.12f),
            new WeaponDef(WeaponKind.Orbital, "ОРБИТАЛЬНАЯ ТУРЕЛЬ", 1.48f, 1.30f, 0.92f, 2, 0.22f)
        };

        public static readonly BotProfile[] BotProfiles =
        {
            new BotProfile(BotPersonality.Aggressive, "АГРЕССОР", "Ищет слабых и давит преимуществом.", 0.92f, 0.36f, 0.22f, 0.62f, 0.72f, 0.34f, 0.66f, 210f),
            new BotProfile(BotPersonality.Farmer, "ФЕРМЕР", "Собирает энергию и избегает риска.", 0.14f, 0.96f, 0.72f, 0.12f, 0.55f, 0.58f, 0.24f, 420f),
            new BotProfile(BotPersonality.Survivor, "ВЫЖИВШИЙ", "Убегает раньше, чем возникает угроза.", 0.10f, 0.62f, 0.98f, 0.08f, 0.60f, 0.30f, 0.55f, 520f),
            new BotProfile(BotPersonality.Hunter, "ОХОТНИК", "Выслеживает цель и добивает её.", 0.86f, 0.28f, 0.40f, 0.50f, 0.84f, 0.26f, 0.82f, 300f),
            new BotProfile(BotPersonality.Scavenger, "МУСОРЩИК", "Собирает остатки после боёв и редкие предметы.", 0.30f, 0.88f, 0.58f, 0.42f, 0.62f, 0.46f, 0.48f, 360f),
            new BotProfile(BotPersonality.Elite, "ЭЛИТА", "Адаптируется к ситуации и комбинирует стратегии.", 0.74f, 0.70f, 0.76f, 0.44f, 0.94f, 0.16f, 0.96f, 340f),
            new BotProfile(BotPersonality.Chaotic, "ХАОТ", "Непредсказуемо меняет цели и рискует.", 0.66f, 0.54f, 0.18f, 0.96f, 0.68f, 0.24f, 0.72f, 260f)
        };

        public static readonly string[] BotNames =
        {
            "Астра", "Вектор", "Нуль", "Комет", "Сигма", "Пульсар", "Кварк", "Зенит", "Орбита", "Фотон",
            "Нептун", "Альфа", "Гамма", "Дельта", "Эхо", "Импульс", "Призма", "Вихрь", "Метеор", "Спектр",
            "Гравий", "Кобальт", "Навахо", "Титан", "Фантом", "Скальпель", "Осколок", "Корvus", "Ион", "Сириус",
            "Лазурь", "Полярис", "Ферми", "Гипер", "Апогей", "Цефей", "Неон", "Вертекс", "Калибр", "Заря"
        };

        public static readonly BossDef[] Bosses =
        {
            new BossDef("boss_devourer", "ПОЖИРАТЕЛЬ", "Ядро старой арены", HullShape.Core, "#FF4D7E", 155f, 980f, 34f, 100f, 3, "Всасывание → импульс → ярость", "Ядро уязвимо после импульса", 8, 3, 1),
            new BossDef("boss_leviathan", "ЛЕВИАФАН", "Хищник глубокого космоса", HullShape.Spike, "#9D7BFF", 185f, 1320f, 40f, 118f, 3, "Преследование → залп → рывок", "Задняя защиты слабее", 12, 5, 1),
            new BossDef("boss_bastion", "БАСТИОН", "Орбитальная крепость", HullShape.Bulwark, "#FFD54A", 240f, 1760f, 46f, 72f, 4, "Щит → турели → таран → перезарядка", "Щит отключается при перегрузке", 16, 6, 2),
            new BossDef("boss_wraith", "ПРИЗРАК", "Фазовый охотник", HullShape.Blade, "#3DE6FF", 120f, 860f, 52f, 190f, 3, "Фаза → серия рывков → невидимость", "Появляется в момент атаки", 10, 7, 1),
            new BossDef("boss_singularity", "СИНГУЛЯРНОСТЬ", "Искусственная чёрная дыра", HullShape.Ring, "#B6FF3D", 210f, 2050f, 58f, 84f, 4, "Притяжение → коллапс → выброс → покой", "Ядро открыто в фазе покоя", 20, 10, 2)
        };

        public static readonly ModeDef[] Modes =
        {
            new ModeDef(GameModeKind.Classic, "КЛАССИЧЕСКАЯ АРЕНА", "Рост, поглощение и борьба за первое место.", 28, 8f, 1f, 1f, false, true, false, 1, Difficulty.Normal),
            new ModeDef(GameModeKind.Survival, "ВЫЖИВАНИЕ", "Арена сжимается, опасность растёт.", 24, 7f, 1.28f, 1.25f, true, false, false, 0, Difficulty.Hard),
            new ModeDef(GameModeKind.BossHunt, "ОХОТА НА БОССОВ", "Элитные цели и самые ценные награды.", 22, 9f, 1.18f, 1.60f, false, true, false, 1, Difficulty.Hard),
            new ModeDef(GameModeKind.Chaos, "ХАОС", "Случайные модификаторы и частые события.", 32, 6f, 1.35f, 1.45f, false, true, true, 2, Difficulty.Normal),
            new ModeDef(GameModeKind.Hardcore, "ХАРДКОР", "Одна жизнь. Особые награды.", 20, 6f, 1.55f, 2.10f, true, false, false, 0, Difficulty.Expert),
            new ModeDef(GameModeKind.Infinite, "БЕСКОНЕЧНОСТЬ", "Матч длится, пока вы живы.", 30, 30f, 1.10f, 1.35f, true, true, true, 1, Difficulty.Hard)
        };

        public static readonly CosmeticDef[] Cosmetics =
        {
            new CosmeticDef("ship_nova", "НОВА", "ship", "#6CF6D2", 0, "Доступен с начала"),
            new CosmeticDef("ship_vesta", "ВЕСТА", "ship", "#FF8166", 1200, "Купить за кредиты"),
            new CosmeticDef("ship_orion", "ОРИОН", "ship", "#9D7BFF", 2600, "Купить за кредиты"),
            new CosmeticDef("ship_obsidian", "ОБСИДИАН", "ship", "#5A678F", 4200, "Победите 5 боссов"),
            new CosmeticDef("ship_spectre", "СПЕКТР", "ship", "#3DE6FF", 5600, "Занято 1 место 10 раз"),
            new CosmeticDef("ship_helix", "ГЕЛИКС", "ship", "#B6FF3D", 7400, "Достигните 20 уровня аккаунта"),
            new CosmeticDef("ship_abyss", "БЕЗДНА", "ship", "#FF4D7E", 9800, "Победите СИНГУЛЯРНОСТЬ"),
            new CosmeticDef("ship_atlas", "АТЛАС", "ship", "#FFD54A", 12400, "Пройдите ХАРДКОР"),
            new CosmeticDef("trail_spark", "ИСКРА", "trail", "#FFD54A", 0, "Доступен с начала"),
            new CosmeticDef("trail_ice", "ЛЁД", "trail", "#3DE6FF", 900, "Купить за кредиты"),
            new CosmeticDef("trail_plasma", "ПЛАЗМА", "trail", "#9D7BFF", 2100, "Купить за кредиты"),
            new CosmeticDef("trail_toxic", "ТОКСИН", "trail", "#B6FF3D", 3400, "Соберите 5000 энергии"),
            new CosmeticDef("trail_void", "ПУСТОТА", "trail", "#FF4D7E", 6200, "Соберите 100 тёмной материи"),
            new CosmeticDef("death_burst", "ВСПЫШКА", "death", "#FFD54A", 0, "Доступен с начала"),
            new CosmeticDef("death_implosion", "КОЛЛАПС", "death", "#9D7BFF", 1800, "Купить за кредиты"),
            new CosmeticDef("death_shards", "ОСКОЛКИ", "death", "#3DE6FF", 3600, "Уничтожьте 100 противников"),
            new CosmeticDef("frame_default", "СТАНДАРТ", "frame", "#6CF6D2", 0, "Доступен с начала"),
            new CosmeticDef("frame_solar", "СОЛНЕЧНАЯ", "frame", "#FFD54A", 2400, "Купить за кредиты"),
            new CosmeticDef("frame_void", "ВОИД", "frame", "#FF4D7E", 5400, "Победите 3 боссов"),
            new CosmeticDef("avatar_pilot", "ПИЛОТ", "avatar", "#6CF6D2", 0, "Доступен с начала"),
            new CosmeticDef("avatar_android", "АНДРОИД", "avatar", "#3DE6FF", 1500, "Купить за кредиты"),
            new CosmeticDef("title_rookie", "НОВИЧОК", "title", "#B6FF3D", 0, "Доступен с начала"),
            new CosmeticDef("title_hunter", "ОХОТНИК", "title", "#FF8166", 2800, "50 уничтожений"),
            new CosmeticDef("title_legend", "ЛЕГЕНДА", "title", "#FFD54A", 8800, "10 побед в арене")
        };

        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef("first_blood", "ПЕРВАЯ КРОВЬ", "Уничтожьте первого противника.", 1, 120, 1),
            new AchievementDef("energy_1000", "ЭНЕРГЕТИК", "Соберите 1000 энергии за матч.", 1000, 260, 2),
            new AchievementDef("mass_100", "ГИГАНТ", "Достигните массы 100.", 100, 420, 3),
            new AchievementDef("kills_10", "СТЕРЛИЗАТОР", "10 уничтожений за матч.", 10, 480, 4),
            new AchievementDef("streak_5", "НЕУДЕРЖИМЫЙ", "Серия из 5 уничтожений.", 5, 360, 3),
            new AchievementDef("boss_1", "ОХОТНИК БОССОВ", "Победите первого босса.", 1, 700, 6),
            new AchievementDef("boss_5", "УБИЙЦА ЛЕГЕНД", "Победите 5 боссов.", 5, 1600, 12),
            new AchievementDef("rank_1", "ПЕРВЫЙ", "Занимите 1 место в арене.", 1, 900, 8),
            new AchievementDef("rank_1_10", "ДОМИНИРОВАНИЕ", "Занимите 1 место 10 раз.", 10, 2600, 20),
            new AchievementDef("evolution_3", "ЭВОЛЮЦИЯ", "Получите эволюцию 3 уровня.", 3, 800, 7),
            new AchievementDef("artifact_10", "АРХЕОЛОГ", "Найдите 10 артефактов.", 10, 1100, 9),
            new AchievementDef("dark_100", "ИЗМЕРИТЕЛЬ", "Соберите 100 тёмной материи.", 100, 1300, 10),
            new AchievementDef("hardcore_win", "БЕЗ СТРАХА", "Выживите в ХАРДКОР.", 1, 1800, 14),
            new AchievementDef("long_run", "МАРАФОН", "Продержитесь 15 минут.", 900, 1400, 11),
            new AchievementDef("comeback", "Камбэк", "Победите противника вдвое крупнее.", 1, 640, 5),
            new AchievementDef("collector", "КОЛЛЕКЦИОНЕР", "Откройте 10 косметических предметов.", 10, 1500, 12),
            new AchievementDef("rich", "МАГНАТ", "Накопите 10000 кредитов.", 10000, 2000, 16),
            new AchievementDef("research", "УЧЁНЫЙ", "Исследуйте 5 улучшений.", 5, 1200, 10),
            new AchievementDef("perfect", "ИДЕАЛЬНЫЙ ЗАБЕГ", "Завершите матч без смертей на 1 месте.", 1, 2200, 18)
        };

        public static readonly QuestDef[] Quests =
        {
            new QuestDef("q_kills", "ОХОТА", "kills", 8, 260, 2, false),
            new QuestDef("q_energy", "ПОПОЛНЕНИЕ", "energy", 350, 220, 1, false),
            new QuestDef("q_mass", "РОСТ", "mass", 60, 300, 2, false),
            new QuestDef("q_crystals", "КРИСТАЛЛЫ", "crystals", 6, 340, 3, false),
            new QuestDef("q_top3", "ПРИЗЁР", "top3", 2, 380, 3, false),
            new QuestDef("q_boss", "ОХОТНИК", "bosses", 1, 520, 5, false),
            new QuestDef("w_matches", "НЕ ОСТАНАВЛИВАЙСЯ", "matches", 5, 900, 8, true),
            new QuestDef("w_wins", "ПОБЕДИТЕЛЬ", "wins", 2, 1400, 12, true),
            new QuestDef("w_kills", "РЕЗНЯ", "kills", 40, 1200, 10, true),
            new QuestDef("w_dark", "ТЁМНАЯ СТОРОНА", "darkMatter", 25, 1100, 10, true)
        };

        public static readonly ResearchDef[] Research =
        {
            new ResearchDef("res_energy", "СБОР ЭНЕРГИИ", "+4% энергии за уровень.", 5, 700, 0.04f),
            new ResearchDef("res_agility", "МАНЁВРЕННОСТЬ", "+2% скорости за уровень.", 5, 850, 0.02f),
            new ResearchDef("res_armor", "БРОНЯ", "+3% прочности за уровень.", 5, 900, 0.03f),
            new ResearchDef("res_magnet", "МАГНИТНОЕ ПОЛЕ", "+5% радиуса сбора за уровень.", 4, 800, 0.05f),
            new ResearchDef("res_luck", "УДАЧА", "+2% шанс редких предметов за уровень.", 4, 1200, 0.02f),
            new ResearchDef("res_economy", "ЭКОНОМИКА", "+5% кредитов за матч за уровень.", 5, 650, 0.05f)
        };

        // ─────────────────────────── Lookups ───────────────────────────

        public static EvolutionDef FindEvolution(string id)
        {
            return Find(Evolutions, id, e => e.Id);
        }

        public static EvolutionDef FindEvolution(EvolutionBranch branch, int tier)
        {
            for (int i = 0; i < Evolutions.Length; i++)
                if (Evolutions[i].Branch == branch && Evolutions[i].Tier == tier) return Evolutions[i];
            return null;
        }

        public static AbilityDef FindAbility(AbilityKind kind)
        {
            for (int i = 0; i < Abilities.Length; i++) if (Abilities[i].Kind == kind) return Abilities[i];
            return Abilities[0];
        }

        public static WeaponDef FindWeapon(WeaponKind kind)
        {
            for (int i = 0; i < Weapons.Length; i++) if (Weapons[i].Kind == kind) return Weapons[i];
            return Weapons[0];
        }

        public static BotProfile FindBotProfile(BotPersonality personality)
        {
            for (int i = 0; i < BotProfiles.Length; i++)
                if (BotProfiles[i].Personality == personality) return BotProfiles[i];
            return BotProfiles[1];
        }

        public static BossDef FindBoss(string id)
        {
            return Find(Bosses, id, b => b.Id);
        }

        public static ModeDef FindMode(GameModeKind kind)
        {
            for (int i = 0; i < Modes.Length; i++) if (Modes[i].Kind == kind) return Modes[i];
            return Modes[0];
        }

        public static CosmeticDef FindCosmetic(string id)
        {
            return Find(Cosmetics, id, c => c.Id);
        }

        public static AchievementDef FindAchievement(string id)
        {
            return Find(Achievements, id, a => a.Id);
        }

        public static QuestDef FindQuest(string id)
        {
            return Find(Quests, id, q => q.Id);
        }

        public static ResearchDef FindResearch(string id)
        {
            return Find(Research, id, r => r.Id);
        }

        public static ShipVisualDef FindShipVisual(string id)
        {
            return Find(ShipVisuals, id, s => s.Id);
        }

        private static T Find<T>(T[] items, string id, Func<T, string> selector)
        {
            for (int i = 0; i < items.Length; i++)
                if (string.Equals(selector(items[i]), id, StringComparison.OrdinalIgnoreCase)) return items[i];
            return default(T);
        }

        /// <summary>Deterministic daily quest selection based on the UTC date.</summary>
        public static List<QuestDef> DailyQuests(DateTime date)
        {
            uint seed = Rng.Hash(date.ToString("yyyy-MM-dd"));
            var rng = new Rng(seed);
            var pool = new List<QuestDef>();
            for (int i = 0; i < Quests.Length; i++) if (!Quests[i].Weekly) pool.Add(Quests[i]);
            var result = new List<QuestDef>();
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = rng.NextInt(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        public static List<QuestDef> WeeklyQuests(DateTime date)
        {
            var result = new List<QuestDef>();
            for (int i = 0; i < Quests.Length; i++) if (Quests[i].Weekly) result.Add(Quests[i]);
            return result;
        }
    }
}
