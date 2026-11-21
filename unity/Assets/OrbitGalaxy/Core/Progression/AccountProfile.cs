using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Профиль аккаунта ORBITA GALAXY. Все числа безопасны для сохранения:
    /// значения ограничиваются, списки и словари всегда не равны null.
    /// </summary>
    public sealed class AccountProfile
    {
        public const int MaxLevel = 10000;
        public const int MaxResource = 2000000000;
        private const float MaxXp = 1e12f;

        public const string DefaultShip = "ship_nova";
        public const string DefaultTrail = "trail_spark";
        public const string DefaultDeathFx = "death_burst";
        public const string DefaultFrame = "frame_default";
        public const string DefaultAvatar = "avatar_pilot";
        public const string DefaultTitle = "title_rookie";

        public int SchemaVersion;
        public int Level;
        public float Xp;
        public int Credits;
        public int Crystals;
        public int DarkMatter;
        public string SelectedShip;
        public string SelectedTrail;
        public string SelectedDeathFx;
        public string SelectedFrame;
        public string SelectedAvatar;
        public string SelectedTitle;
        public List<string> Owned;
        public List<string> Achievements;
        public List<string> Secrets;
        public Dictionary<string, int> Records;
        public Dictionary<string, int> Research;
        public Dictionary<string, int> Stats;
        public Dictionary<string, int> QuestProgress;
        public Dictionary<string, int> DailyProgress;
        public Dictionary<string, int> WeeklyProgress;
        public string DailyDate;
        public string WeeklyKey;
        public int WinStreak;
        public int BestWinStreak;
        public int Matches;
        public int Wins;
        public int BossKills;
        public GameSettings Settings;

        public AccountProfile()
        {
            SchemaVersion = GameConfig.SaveSchemaVersion;
            Level = 1;
            Xp = 0f;
            Credits = 0;
            Crystals = 0;
            DarkMatter = 0;
            SelectedShip = DefaultShip;
            SelectedTrail = DefaultTrail;
            SelectedDeathFx = DefaultDeathFx;
            SelectedFrame = DefaultFrame;
            SelectedAvatar = DefaultAvatar;
            SelectedTitle = DefaultTitle;
            Owned = new List<string>();
            Achievements = new List<string>();
            Secrets = new List<string>();
            Records = new Dictionary<string, int>(StringComparer.Ordinal);
            Research = new Dictionary<string, int>(StringComparer.Ordinal);
            Stats = new Dictionary<string, int>(StringComparer.Ordinal);
            QuestProgress = new Dictionary<string, int>(StringComparer.Ordinal);
            DailyProgress = new Dictionary<string, int>(StringComparer.Ordinal);
            WeeklyProgress = new Dictionary<string, int>(StringComparer.Ordinal);
            DailyDate = string.Empty;
            WeeklyKey = string.Empty;
            WinStreak = 0;
            BestWinStreak = 0;
            Matches = 0;
            Wins = 0;
            BossKills = 0;
            Settings = new GameSettings();
            Sanitize();
        }

        public AccountProfile(AccountProfile other) : this()
        {
            CopyFrom(other);
        }

        public AccountProfile Clone()
        {
            return new AccountProfile(this);
        }

        public void CopyFrom(AccountProfile other)
        {
            if (other == null) return;
            SchemaVersion = other.SchemaVersion;
            Level = other.Level;
            Xp = other.Xp;
            Credits = other.Credits;
            Crystals = other.Crystals;
            DarkMatter = other.DarkMatter;
            SelectedShip = other.SelectedShip;
            SelectedTrail = other.SelectedTrail;
            SelectedDeathFx = other.SelectedDeathFx;
            SelectedFrame = other.SelectedFrame;
            SelectedAvatar = other.SelectedAvatar;
            SelectedTitle = other.SelectedTitle;
            Owned = CopyList(other.Owned);
            Achievements = CopyList(other.Achievements);
            Secrets = CopyList(other.Secrets);
            Records = CopyMap(other.Records);
            Research = CopyMap(other.Research);
            Stats = CopyMap(other.Stats);
            QuestProgress = CopyMap(other.QuestProgress);
            DailyProgress = CopyMap(other.DailyProgress);
            WeeklyProgress = CopyMap(other.WeeklyProgress);
            DailyDate = other.DailyDate;
            WeeklyKey = other.WeeklyKey;
            WinStreak = other.WinStreak;
            BestWinStreak = other.BestWinStreak;
            Matches = other.Matches;
            Wins = other.Wins;
            BossKills = other.BossKills;
            Settings = other.Settings == null ? new GameSettings() : new GameSettings(other.Settings);
            Sanitize();
        }

        /// <summary>Опыт, необходимый для перехода с уровня level на level+1 (Таблица GameConfig).</summary>
        public static float XpForLevel(int level)
        {
            if (level < 1) level = 1;
            double need = GameConfig.XpPerLevelBase * Math.Pow(GameConfig.XpLevelGrowth, level - 1);
            if (double.IsNaN(need) || double.IsInfinity(need) || need > MaxXp) return MaxXp;
            return (float)Math.Round(need, MidpointRounding.AwayFromZero);
        }

        /// <summary>Суммарный опыт для достижения уровня level.</summary>
        public static float TotalXpForLevel(int level)
        {
            if (level <= 1) return 0f;
            if (level > MaxLevel) level = MaxLevel;
            double total = 0d;
            for (int i = 1; i < level; i++)
            {
                total += XpForLevel(i);
                if (total >= MaxXp) return MaxXp;
            }
            return (float)total;
        }

        /// <summary>Уровень по накопленному опыту (nullptr и отрицания не влияют).</summary>
        public static int LevelForXp(float xp)
        {
            if (float.IsNaN(xp) || xp <= 0f) return 1;
            double remaining = xp;
            int level = 1;
            while (level < MaxLevel)
            {
                double need = XpForLevel(level);
                if (remaining < need) break;
                remaining -= need;
                level++;
            }
            return level;
        }

        /// <summary>Добавляет накопленный опыт и пересчитывает уровень по кривой GameConfig.</summary>
        public void AddXp(float amount)
        {
            if (float.IsNaN(amount) || amount <= 0f) return;
            Xp = ClampFloat(Xp + amount, 0f, MaxXp);
            Level = Math.Max(Level, LevelForXp(Xp));
            if (Level > MaxLevel) Level = MaxLevel;
        }

        public void AddCredits(int amount)
        {
            Credits = SaturatingAdd(Credits, amount);
        }

        public void AddCrystals(int amount)
        {
            Crystals = SaturatingAdd(Crystals, amount);
        }

        public void AddDarkMatter(int amount)
        {
            DarkMatter = SaturatingAdd(DarkMatter, amount);
        }

        /// <summary>Выдаёт предмет (косметику, корабль, эволюцию) без списания валюты.</summary>
        public void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string clean = id.Trim();
            if (clean.Length == 0) return;
            for (int i = 0; i < Owned.Count; i++)
            {
                if (string.Equals(Owned[i], clean, StringComparison.OrdinalIgnoreCase)) return;
            }
            Owned.Add(clean);
        }

        public bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < Owned.Count; i++)
            {
                if (string.Equals(Owned[i], id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>Бонус исследования из ContentDatabase.Research (доля, например 0.08 = +8%).</summary>
        public float ResearchBonus(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0f;
            ResearchDef def = ContentDatabase.FindResearch(id);
            if (def == null && !id.StartsWith("res_", StringComparison.Ordinal))
                def = ContentDatabase.FindResearch("res_" + id);
            if (def == null) return 0f;
            int level = 0;
            if (!Research.TryGetValue(def.Id, out level))
            {
                foreach (KeyValuePair<string, int> pair in Research)
                {
                    if (string.Equals(pair.Key, def.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        level = pair.Value;
                        break;
                    }
                }
            }
            if (level <= 0) return 0f;
            if (level > def.MaxLevel) level = def.MaxLevel;
            return level * def.BonusPerLevel;
        }

        /// <summary>Применяет результат матча: награды, квесты, достижения, секреты, рекорды.</summary>
        public void ApplyResult(MatchResult result, DateTime now)
        {
            ProgressionSystem.ApplyMatchResult(this, result, now);
        }

        public int Stat(string key)
        {
            int value;
            if (string.IsNullOrEmpty(key) || Stats == null || !Stats.TryGetValue(key, out value)) return 0;
            return value;
        }

        public void AddStat(string key, int amount)
        {
            if (string.IsNullOrEmpty(key) || Stats == null) return;
            Stats[key] = SaturatingAdd(Stat(key), amount);
        }

        /// <summary>Восстанавливает значения по умолчанию и ограничения после загрузки JSON.</summary>
        public void Sanitize()
        {
            SchemaVersion = GameConfig.SaveSchemaVersion;
            Xp = ClampFloat(Xp, 0f, MaxXp);
            Level = MathGalaxy.Clamp(Level, 1, MaxLevel);
            Credits = MathGalaxy.Clamp(Credits, 0, MaxResource);
            Crystals = MathGalaxy.Clamp(Crystals, 0, MaxResource);
            DarkMatter = MathGalaxy.Clamp(DarkMatter, 0, MaxResource);

            SelectedShip = ValidString(SelectedShip, DefaultShip);
            SelectedTrail = ValidString(SelectedTrail, DefaultTrail);
            SelectedDeathFx = ValidString(SelectedDeathFx, DefaultDeathFx);
            SelectedFrame = ValidString(SelectedFrame, DefaultFrame);
            SelectedAvatar = ValidString(SelectedAvatar, DefaultAvatar);
            SelectedTitle = ValidString(SelectedTitle, DefaultTitle);

            Owned = CleanList(Owned, new string[] { DefaultShip, DefaultTrail, DefaultDeathFx, DefaultFrame, DefaultAvatar, DefaultTitle });
            Achievements = CleanList(Achievements, null);
            Secrets = CleanList(Secrets, null);
            Records = CleanMap(Records);
            Research = CleanMap(Research);
            Stats = CleanMap(Stats);
            QuestProgress = CleanMap(QuestProgress);
            DailyProgress = CleanMap(DailyProgress);
            WeeklyProgress = CleanMap(WeeklyProgress);

            DailyDate = DailyDate ?? string.Empty;
            WeeklyKey = WeeklyKey ?? string.Empty;
            WinStreak = MathGalaxy.Clamp(WinStreak, 0, MaxResource);
            BestWinStreak = MathGalaxy.Clamp(BestWinStreak, WinStreak, MaxResource);
            Matches = MathGalaxy.Clamp(Matches, 0, MaxResource);
            Wins = MathGalaxy.Clamp(Wins, 0, MaxResource);
            BossKills = MathGalaxy.Clamp(BossKills, 0, MaxResource);

            if (Settings == null) Settings = new GameSettings();
            Settings.Sanitize();
        }

        internal static int SaturatingAdd(int current, int amount)
        {
            if (amount <= 0) return MathGalaxy.Clamp(current, 0, MaxResource);
            long baseValue = MathGalaxy.Clamp(current, 0, MaxResource);
            long sum = baseValue + (long)amount;
            if (sum > MaxResource) return MaxResource;
            return (int)sum;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static string ValidString(string value, string fallback)
        {
            if (value == null) return fallback;
            string text = value.Trim();
            return text.Length == 0 ? fallback : text;
        }

        private static List<string> CopyList(List<string> source)
        {
            List<string> result = new List<string>();
            if (source != null) result.AddRange(source);
            return result;
        }

        private static Dictionary<string, int> CopyMap(Dictionary<string, int> source)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KeyValuePair<string, int> pair in source) result[pair.Key] = pair.Value;
            }
            return result;
        }

        private static List<string> CleanList(List<string> source, string[] defaults)
        {
            List<string> result = new List<string>();
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    string value = source[i];
                    if (string.IsNullOrEmpty(value)) continue;
                    value = value.Trim();
                    bool duplicate = false;
                    for (int j = 0; j < result.Count; j++)
                    {
                        if (string.Equals(result[j], value, StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }
                    }
                    if (!duplicate) result.Add(value);
                }
            }
            if (defaults != null)
            {
                for (int i = 0; i < defaults.Length; i++)
                {
                    bool found = false;
                    for (int j = 0; j < result.Count; j++)
                    {
                        if (string.Equals(result[j], defaults[i], StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    }
                    if (!found) result.Add(defaults[i]);
                }
            }
            return result;
        }

        private static Dictionary<string, int> CleanMap(Dictionary<string, int> source)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null) return result;
            foreach (KeyValuePair<string, int> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Key.Trim().Length == 0) continue;
                result[pair.Key.Trim()] = MathGalaxy.Clamp(pair.Value, 0, MaxResource);
            }
            return result;
        }
    }
}
