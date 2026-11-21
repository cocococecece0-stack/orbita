using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit.Core
{
    /// <summary>
    /// Static content mirrored from the shipped native v3.8 implementation
    /// (app/.../game/Content.kt, ios/Orbit/Content.swift).
    /// </summary>
    public static class Content
    {
        public static readonly ModeDef[] Modes =
        {
            new ModeDef("classic", "КЛАСС", "КЛАССИКА", "Бесконечный полёт. Один промах — конец.", ObjectiveField.Score, 0),
            new ModeDef("zen", "ДЗЕН", "ДЗЕН", "90 секунд без астероидов. Собирай сферы и бонусы.", ObjectiveField.Orbs, 90),
            new ModeDef("sprint", "СПРИНТ", "СПРИНТ", "60 секунд на максимальный счёт. Удар стоит 3 секунды.", ObjectiveField.Score, 60),
            new ModeDef("survival", "ВЫЖИВ", "ВЫЖИВАНИЕ", "Плотность растёт постоянно. Ошибка — конец.", ObjectiveField.Score, 0),
            new ModeDef("daily", "ДЕЙЛИ", "ДЕЙЛИ", "Фиксированный сид дня. Общий вызов для всех.", ObjectiveField.Score, 0),
            new ModeDef("stack", "ШТАБ", "ШТАБЕЛЬ", "Падающие блоки: закрывай строки, держи серию и не допускай верха.", ObjectiveField.Lines, 0)
        };

        public static readonly string[] PlanetSkins =
        {
            "ЗЕМЛЯ", "МАРС", "ЮПИТЕР", "НЕПТУН", "ВЕНЕРА", "САТУРН", "ЛУНА", "ЭКЗО"
        };

        public static readonly string[] BossNames = { "ЯДРО", "РОЙ", "ИМПУЛЬС" };

        // Lowercase aliases matching the native member names.
        public static readonly string[] planetSkins = PlanetSkins;
        public static readonly string[] bossNames = BossNames;

        public static readonly SectorDef[] Sectors =
        {
            new SectorDef(0, "ОРБИТА-01", "Учебный сектор", 1,
                new[] { HazardKind.Rock }, 0.75f),
            new SectorDef(1, "ПЕПЕЛ", "Горящий пояс", 2,
                new[] { HazardKind.Rock, HazardKind.Comet }, 0.90f),
            new SectorDef(2, "НЕОНОВЫЙ ПОЯС", "Свет и помехи", 3,
                new[] { HazardKind.Rock, HazardKind.Comet, HazardKind.Mine }, 1.00f),
            new SectorDef(3, "ЛЕДЯНОЕ КОЛЬЦО", "Тяжёлые льды", 5,
                new[] { HazardKind.Rock, HazardKind.Gravity }, 1.05f),
            new SectorDef(4, "СЕРДЦЕ ШТОРМА", "Залпы и вспышки", 7,
                new[] { HazardKind.Rock, HazardKind.Comet, HazardKind.Flare }, 1.15f),
            new SectorDef(5, "ЧЕРВОТОЧИНА", "Пространство рвётся", 9,
                new[] { HazardKind.Rock, HazardKind.Wormhole }, 1.10f),
            new SectorDef(6, "ЯДРО ГИГАНТА", "Стража ядра", 12,
                new[] { HazardKind.Rock, HazardKind.Comet, HazardKind.Boss }, 1.20f),
            new SectorDef(7, "СЕКРЕТ", "Скрытый сектор", 99,
                new[] { HazardKind.Rock, HazardKind.Mine, HazardKind.Boss }, 1.30f),
            new SectorDef(8, "ПОЛЯРНАЯ КОРОНА", "Ледяные арки и кометы", 15,
                new[] { HazardKind.Rock, HazardKind.Comet, HazardKind.Flare }, 1.22f),
            new SectorDef(9, "СИНГУЛЯРНОСТЬ", "Гравитация и боссы", 22,
                new[] { HazardKind.Rock, HazardKind.Gravity, HazardKind.Boss }, 1.28f)
        };

        public static readonly PerkDef[] Perks =
        {
            new PerkDef(0, "✦", "ПЕРЕГРУЗ", "+25% очков до конца забега", "Угрозы на 10% быстрее", "overclock"),
            new PerkDef(1, "◆", "БАРЬЕР", "Щит на 14 секунд", "−10% очков", "barrier"),
            new PerkDef(2, "◎", "МАГНИТ", "Сферы притягиваются 10 секунд", "Угрозы на 10% плотнее", "magnet"),
            new PerkDef(3, "◐", "ЗАМЕДЛЕНИЕ", "Угрозы летят медленнее 10 секунд", "−10% очков", "slowmo"),
            new PerkDef(4, "✺", "ИМПУЛЬС", "Волна резонанса очищает орбиту", "Сбрасывает комбо", "pulse"),
            new PerkDef(5, "◇", "ФАНТОМ", "Неуязвимость на 8 секунд", "Сферы появляются реже", "ghost"),
            new PerkDef(6, "●", "УРОЖАЙ", "Сферы дают двойной опыт", "−10% очков", "harvest"),
            new PerkDef(7, "◷", "ХРОНОС", "+12 секунд, если есть таймер", "Угрозы на 15% плотнее", "chronos")
        };

        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef("first_run", "ПЕРВЫЙ ВЫЛЕТ", "Завершить забег", 40, 20),
            new AchievementDef("score_500", "ПОЛУТЫСЯЧНИК", "Набрать 500 очков за забег", 80, 40),
            new AchievementDef("score_1500", "МАСТЕР ОРБИТЫ", "Набрать 1500 очков за забег", 160, 80),
            new AchievementDef("score_5000", "ЛЕГЕНДА", "Набрать 5000 очков за забег", 400, 200),
            new AchievementDef("combo_10", "ЦЕПНАЯ РЕАКЦИЯ", "Достичь комбо x10", 100, 50),
            new AchievementDef("combo_25", "НЕУДЕРЖИМЫЙ", "Достичь комбо x25", 220, 110),
            new AchievementDef("orbs_50", "СОБИРАТЕЛЬ", "50 сфер за забег", 90, 45),
            new AchievementDef("near_10", "НА ВОЛОСКЕ", "10 близких пролётов за забег", 120, 60),
            new AchievementDef("near_30", "АКРОБАТ", "30 близких пролётов за забег", 260, 130),
            new AchievementDef("level_10", "ДОЛГОЛЁТ", "Достичь 10 уровня", 140, 70),
            new AchievementDef("level_20", "ВЕТЕРАН", "Достичь 20 уровня", 300, 150),
            new AchievementDef("boss_1", "ОХОТНИК", "Уничтожить ядро босса", 180, 90),
            new AchievementDef("boss_5", "УБИЙЦА ГИГАНТОВ", "5 ядер за один забег", 480, 240),
            new AchievementDef("worm_5", "ПУТЕШЕСТВЕННИК", "5 червоточин за забег", 130, 65),
            new AchievementDef("stack_40", "АРХИТЕКТОР", "40 строк в штабеле", 260, 130),
            new AchievementDef("all_modes", "УНИВЕРСАЛ", "Сыграть во все режимы", 200, 100),
            new AchievementDef("sector_7", "ТАЙНАЯ ДОРОГА", "Открыть секрет сектор", 500, 250),
            new AchievementDef("daily_7", "ДИСЦИПЛИНА", "7 дней подряд с заданиями", 350, 175),
            new AchievementDef("rich_5000", "МАГНАТ", "Накопить 5000 жетонов", 300, 150)
        };

        public static readonly SeasonModifier[] SeasonModifiers =
        {
            new SeasonModifier("СТАНДАРТ", 1.00f),
            new SeasonModifier("МЕТЕОРНЫЙ СЕЗОН", 1.18f),
            new SeasonModifier("ЭНЕРГЕТИЧЕСКИЙ СЕЗОН", 1.10f),
            new SeasonModifier("ТИХИЙ СЕЗОН", 0.90f)
        };

        public static readonly ShopItem[] Shop =
        {
            new ShopItem("ship_cyan", "КОМЕТА", "Стандартный корпус", 0, "ship", 0),
            new ShopItem("ship_rose", "МАЯК", "Коралловый корпус", 300, "ship", 1),
            new ShopItem("ship_lime", "ИМПУЛЬС", "Лаймовый корпус", 600, "ship", 2),
            new ShopItem("ship_violet", "ПРИЗРАК", "Фиолетовый корпус", 900, "ship", 3),
            new ShopItem("ship_amber", "СОЛНЦЕ", "Янтарный корпус", 1400, "ship", 4),
            new ShopItem("ship_magenta", "НОВАЯ", "Пурпурный корпус", 2000, "ship", 5),
            new ShopItem("ship_ice", "АЙСБЕРГ", "Ледяной корпус", 2800, "ship", 6),
            new ShopItem("ship_mint", "ХЛОРОФИЛЛ", "Мятный корпус", 3600, "ship", 7),
            new ShopItem("trail_spark", "ИСКРЫ", "Классический шлейф", 0, "trail", 0),
            new ShopItem("trail_smoke", "ДЫМ", "Плотный шлейф", 450, "trail", 1),
            new ShopItem("trail_rainbow", "СПЕКТР", "Радужный шлейф", 1200, "trail", 2),
            new ShopItem("trail_wave", "ВОЛНА", "Волнистый шлейф", 2200, "trail", 3),
            new ShopItem("planet_earth", "ЗЕМЛЯ", "Сине-зелёная планета", 0, "planet", 0),
            new ShopItem("planet_mars", "МАРС", "Красная планета", 700, "planet", 1),
            new ShopItem("planet_jupiter", "ЮПИТЕР", "Газовый гигант", 1500, "planet", 2),
            new ShopItem("planet_neptune", "НЕПТУН", "Ледяной гигант", 2600, "planet", 3),
            new ShopItem("planet_venus", "ВЕНЕРА", "Раскалённый мир", 3400, "planet", 4),
            new ShopItem("planet_saturn", "САТУРН", "Кольцевая планета", 4200, "planet", 5),
            new ShopItem("planet_moon", "ЛУНА", "Каменный спутник", 5000, "planet", 6),
            new ShopItem("planet_exo", "ЭКЗО", "Неизведанный мир", 6000, "planet", 7),
            new ShopItem("upg_shield", "РЕЗЕРВНЫЙ ЩИТ", "Щит в начале каждого забега", 800, "upgrade", 1),
            new ShopItem("upg_time", "ХРОНОМЕТР", "+8 секунд в дзен и спринте", 1000, "upgrade", 1),
            new ShopItem("upg_magnet", "ПОСТОЯННЫЙ МАГНИТ", "Сферы притягиваются слабее, но всегда", 1500, "upgrade", 1),
            new ShopItem("upg_slow", "УДЛИНЁННОЕ ЗАМЕДЛЕНИЕ", "Замедление длится дольше", 1800, "upgrade", 1),
            new ShopItem("upg_double", "БОНУСНЫЙ МНОЖИТЕЛЬ", "Двойные очки дольше", 2400, "upgrade", 1)
        };

        public static readonly Secret[] Secrets =
        {
            new Secret("eye", "НАБЛЮДАТЕЛЬ", "Собери 3 редкие сферы за забег"),
            new Secret("dancer", "ТАНЦОР", "25 близких пролётов за 60 секунд"),
            new Secret("survivor", "ЖИВУЧИЙ", "Доживи до 20 уровня без щита"),
            new Secret("collector", "КОЛЛЕКЦИОНЕР", "Купи все 8 корпусов"),
            new Secret("wanderer", "БРОДЯГА", "Побывай во всех секторах"),
            new Secret("hermit", "ОТШЕЛЬНИК", "Сыграй 10 забегов в дзен-режиме")
        };

        public static readonly MissionTemplate[] MissionTemplates =
        {
            new MissionTemplate("orbs", "СОБРАТЬ СФЕР", "orbs", 24, 70),
            new MissionTemplate("dodge", "УКЛОНИТЬСЯ", "dodges", 30, 80),
            new MissionTemplate("near", "РИСКНУТЬ", "nearMisses", 8, 100),
            new MissionTemplate("combo", "ДЕРЖАТЬ КОМБО", "maxCombo", 10, 90),
            new MissionTemplate("score", "НАБРАТЬ ОЧКОВ", "score", 600, 130),
            new MissionTemplate("boss", "СБИТЬ ЯДРО", "bossKills", 1, 160),
            new MissionTemplate("worm", "ВОЙТИ В ПОРТАЛ", "wormholes", 3, 110),
            new MissionTemplate("level", "ДОСТИЧЬ УРОВНЯ", "level", 8, 120)
        };

        public static readonly SeasonTier[] SeasonTiers =
        {
            new SeasonTier(120, 90),
            new SeasonTier(400, 160),
            new SeasonTier(1000, 300),
            new SeasonTier(2400, 600)
        };

        public const int XpPerLevel = 100;
        public const int XpPerLevelCompat = XpPerLevel;

        public static int PerkIdsAvailable()
        {
            return Perks.Length;
        }

        public static ModeDef Mode(ModeKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Modes.Length) index = 0;
            return Modes[index];
        }

        public static ModeKind ModeFromId(string id)
        {
            for (int i = 0; i < Modes.Length; i++)
                if (Modes[i].Id == id) return (ModeKind)i;
            return ModeKind.Classic;
        }

        public static SectorDef Sector(int index)
        {
            if (index < 0) index = 0;
            if (index >= Sectors.Length) index = Sectors.Length - 1;
            return Sectors[index];
        }

        public static ObjectiveDef Objective(ModeKind mode, int sectorId)
        {
            // Native: val tier = sector(sectorId).id (the clamped sector id, not the raw argument).
            int tier = Sector(sectorId).Id;
            switch (mode)
            {
                case ModeKind.Zen:
                    return new ObjectiveDef("СОБРАТЬ СФЕР", ObjectiveField.Orbs, 18 + tier * 4, "orbs");
                case ModeKind.Sprint:
                    return new ObjectiveDef("НАБРАТЬ ОЧКОВ", ObjectiveField.Score, 1200 + tier * 300, "score");
                case ModeKind.Stack:
                    return new ObjectiveDef("ЗАКРЫТЬ СТРОК", ObjectiveField.Lines, 10 + tier * 3, "lines");
                default:
                    return new ObjectiveDef("НАБРАТЬ ОЧКОВ", ObjectiveField.Score, 600 + tier * 200, "score");
            }
        }

        public static ObjectiveDef Objective(string modeId, int sectorId)
        {
            return Objective(ModeFromId(modeId), sectorId);
        }

        /// <summary>Native seasonModifier(seasonId: String): char-code sum modulo the table size.</summary>
        public static SeasonModifier SeasonModifierFor(string seasonId)
        {
            if (string.IsNullOrEmpty(seasonId)) seasonId = SeasonIdString();
            int sum = 0;
            for (int i = 0; i < seasonId.Length; i++) sum += seasonId[i];
            int count = SeasonModifiers.Length;
            int index = ((sum % count) + count) % count;
            return SeasonModifiers[index];
        }

        /// <summary>Compatibility wrapper: maps an integer season number to "Sxx".</summary>
        public static SeasonModifier SeasonModifier(int seasonId)
        {
            return SeasonModifierFor(SeasonKey(seasonId));
        }

        public static string SeasonKey(int seasonId)
        {
            if (seasonId < 1) seasonId = 1;
            return "S" + seasonId.ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>Native SaveStore.seasonId(): 56-day seasons starting 2026-01-05, "Sxx".</summary>
        public static string SeasonIdString()
        {
            DateTime epoch = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
            long days = DaysSinceEpoch(DateTime.UtcNow) - DaysSinceEpoch(epoch);
            long week = days / 56L; // DateTime is after the epoch, so truncation equals floorDiv.
            if (week < 0) week = 0;
            return "S" + Math.Max(1L, week + 1L).ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>Compatibility wrapper returning the numeric part of the native season id.</summary>
        public static int SeasonId()
        {
            string id = SeasonIdString();
            int value;
            if (id.Length > 1 && int.TryParse(id.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return value;
            return 1;
        }

        public static string Today()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>32-bit FNV-1a (Android seedFor / iOS seedFor), masked to 31 bits.</summary>
        public static int SeedFromString(string text)
        {
            if (text == null) text = "";
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    h ^= text[i];
                    h *= 16777619u;
                }
                return (int)(h & 0x7fffffffu);
            }
        }

        public static int SeedForInt(string text)
        {
            return SeedFromString(text);
        }

        /// <summary>Compatibility: the canonical seed rendered as a decimal string.</summary>
        public static string SeedFor(string text)
        {
            return SeedFromString(text).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Native missionsFor(date): deterministic templates with scaled goals/rewards.</summary>
        public static List<Mission> Missions(string date)
        {
            if (string.IsNullOrEmpty(date)) date = Today();
            // Kotlin uses Int overflow; iOS uses UInt32 wrapping. After masking with
            // 0x7fffffff both produce the same seed as the 32-bit FNV-1a above.
            int seed = SeedFromString(date);
            int offset = seed % MissionTemplates.Length;
            var result = new List<Mission>();
            for (int i = 0; i < 3; i++)
            {
                MissionTemplate t = MissionTemplates[(offset + i * 3) % MissionTemplates.Length];
                int scaleBits = (seed >> (i * 4 + 3)) & 3;
                int rewardBits = (seed >> (i * 6 + 5)) & 3;
                float scale = 1f + scaleBits * 0.15f;
                float bonus = 1f + rewardBits * 0.1f;
                int goal = Math.Max(1, (int)(t.Base * scale));
                int reward = (int)(t.Reward * bonus);
                result.Add(new Mission(t.Id, t.Title, t.Field, goal, reward));
            }
            return result;
        }

        /// <summary>Compatibility shape: [field, goal, title].</summary>
        public static List<string[]> MissionsFor(string date)
        {
            List<Mission> missions = Missions(date);
            var result = new List<string[]>();
            for (int i = 0; i < missions.Count; i++)
            {
                Mission m = missions[i];
                result.Add(new[] { m.Field, m.Goal.ToString(CultureInfo.InvariantCulture), m.Title });
            }
            return result;
        }

        private static long DaysSinceEpoch(DateTime date)
        {
            return (long)(date.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        }
    }
}
