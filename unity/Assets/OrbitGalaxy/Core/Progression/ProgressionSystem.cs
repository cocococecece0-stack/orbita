using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Мета-прогрессия: награды за матч, достижения, задания, секреты, рекорды
    /// и смена суточного/недельного периода. Все таблицы берутся из ContentDatabase.
    /// </summary>
    public static class ProgressionSystem
    {
        public const string StatKills = "kills";
        public const string StatDeaths = "deaths";
        public const string StatEnergy = "energy";
        public const string StatCrystals = "crystals";
        public const string StatDarkMatter = "darkMatter";
        public const string StatArtifacts = "artifacts";
        public const string StatBosses = "bosses";
        public const string StatScore = "score";
        public const string StatRank1 = "rank1";
        public const string StatBestMass = "bestMass";
        public const string StatBestStreak = "bestStreak";
        public const string StatEvolutionTier = "evolutionTier";

        private const string ClaimPrefix = "claimed:";

        /// <summary>Полный цикл применения результата матча к профилю.</summary>
        public static ProgressionRewards ApplyMatchResult(AccountProfile profile, MatchResult result, DateTime now)
        {
            ProgressionRewards rewards = new ProgressionRewards();
            if (profile == null || result == null) return rewards;

            profile.Sanitize();
            RollOver(profile, now);

            int score = PositiveInt(result.Score);
            int kills = PositiveInt(result.Kills);
            int deaths = PositiveInt(result.Deaths);
            int energy = PositiveInt(result.EnergyCollected);
            int crystals = PositiveInt(result.Crystals);
            int darkMatter = PositiveInt(result.DarkMatter);
            int artifacts = PositiveInt(result.Artifacts);
            int bosses = PositiveInt(result.BossKills);
            int streak = PositiveInt(result.BestStreak);
            int mass = PositiveInt(result.Mass);
            int rank = PositiveInt(result.Rank);
            int duration = PositiveInt(result.Duration);
            bool won = result.Victory || result.Rank == 1;
            int evolutionTier = HighestEvolutionTier(result);
            int comeback = HasAbsorb(result) ? 1 : 0;

            Dictionary<string, int> metrics = new Dictionary<string, int>(StringComparer.Ordinal);
            metrics["score"] = score;
            metrics["kills"] = kills;
            metrics["deaths"] = deaths;
            metrics["energy"] = energy;
            metrics["crystals"] = crystals;
            metrics["darkMatter"] = darkMatter;
            metrics["artifacts"] = artifacts;
            metrics["bosses"] = bosses;
            metrics["streak"] = streak;
            metrics["mass"] = mass;
            metrics["rank"] = rank;
            metrics["duration"] = duration;
            metrics["matches"] = 1;
            metrics["wins"] = won ? 1 : 0;
            metrics["top3"] = rank > 0 && rank <= 3 ? 1 : 0;
            metrics["hardcoreWin"] = won && result.Mode == GameModeKind.Hardcore ? 1 : 0;
            metrics["perfect"] = won && deaths == 0 ? 1 : 0;
            metrics["comeback"] = comeback;
            metrics["evolution"] = evolutionTier;

            bool newRecord = score > Get(profile.Records, "bestScore");
            UpdateRecords(profile, result, score, kills, mass, rank, newRecord);
            UpdateStats(profile, score, kills, deaths, energy, crystals, darkMatter, artifacts, bosses,
                streak, mass, won, rank, evolutionTier, comeback);

            profile.Matches = AccountProfile.SaturatingAdd(profile.Matches, 1);
            if (won) profile.Wins = AccountProfile.SaturatingAdd(profile.Wins, 1);
            profile.BossKills = AccountProfile.SaturatingAdd(profile.BossKills, bosses);
            if (won)
            {
                profile.WinStreak = AccountProfile.SaturatingAdd(profile.WinStreak, 1);
                if (profile.WinStreak > profile.BestWinStreak) profile.BestWinStreak = profile.WinStreak;
            }
            else profile.WinStreak = 0;

            int credits = PositiveInt(result.Credits);
            int crystalReward = PositiveInt(result.CrystalsReward);
            float xp = result.AccountXp > 0 ? (float)result.AccountXp : (float)result.Xp;
            if (xp < 0f) xp = 0f;

            ModeDef mode = ContentDatabase.FindMode(result.Mode);
            float scale = mode == null ? 1f : Math.Max(0f, mode.RewardScale);
            if (credits == 0) credits = Scaled((double)score / 10d + (double)kills * 18d + (double)bosses * 320d + (double)artifacts * 70d + (double)streak * 12d, scale);
            if (crystalReward == 0) crystalReward = Scaled((double)crystals / 5d + (double)bosses * 2d + artifacts, scale);
            if (xp <= 0f) xp = (float)Scaled((double)score / 20d + (double)kills * 12d + (double)energy * 0.05d + (double)bosses * 140d, scale);
            credits = WithEconomy(profile, credits);

            profile.AddCredits(credits);
            profile.AddCrystals(crystalReward);
            profile.AddDarkMatter(darkMatter);
            int levelBefore = profile.Level;
            profile.AddXp(xp);

            rewards.Credits = credits;
            rewards.Crystals = crystalReward;
            rewards.DarkMatter = darkMatter;
            rewards.Xp = xp;
            rewards.Levels = Math.Max(0, profile.Level - levelBefore);
            rewards.Matches = 1;
            rewards.Victory = won;
            rewards.Rank = rank;
            rewards.NewRecord = newRecord;

            ApplyAchievements(profile, rewards, metrics);
            ApplyQuests(profile, rewards, metrics, now);
            ApplySecrets(profile, rewards, metrics);
            SnakeProgression.Apply(profile, result, rewards);
            profile.Sanitize();
            return rewards;
        }

        public static ProgressionRewards ApplyMatchResult(AccountProfile profile, MatchResult result)
        {
            return ApplyMatchResult(profile, result, DateTime.UtcNow);
        }

        /// <summary>Алиас для совместимости с вызовом ApplyResult из профиля.</summary>
        public static ProgressionRewards ApplyResult(AccountProfile profile, MatchResult result, DateTime now)
        {
            return ApplyMatchResult(profile, result, now);
        }

        public static ProgressionRewards Apply(AccountProfile profile, MatchResult result, DateTime now)
        {
            return ApplyMatchResult(profile, result, now);
        }

        public static ProgressionRewards Apply(AccountProfile profile, MatchResult result)
        {
            return ApplyMatchResult(profile, result, DateTime.UtcNow);
        }

        /// <summary>Сбрасывает суточные и недельные задания при смене периода.</summary>
        public static void RollOver(AccountProfile profile, DateTime now)
        {
            if (profile == null) return;
            EnsureDailyRollover(profile, now);
            EnsureWeeklyRollover(profile, now);
        }

        public static void EnsureRollover(AccountProfile profile, DateTime now)
        {
            RollOver(profile, now);
        }

        public static void EnsureDailyRollover(AccountProfile profile, DateTime now)
        {
            if (profile == null) return;
            string today = DayKey(now);
            if (string.IsNullOrEmpty(profile.DailyDate))
            {
                profile.DailyDate = today;
                return;
            }
            if (profile.DailyDate != today)
            {
                profile.DailyDate = today;
                profile.DailyProgress.Clear();
            }
        }

        public static void EnsureWeeklyRollover(AccountProfile profile, DateTime now)
        {
            if (profile == null) return;
            string key = WeekKey(now);
            if (string.IsNullOrEmpty(profile.WeeklyKey))
            {
                profile.WeeklyKey = key;
                return;
            }
            if (profile.WeeklyKey != key)
            {
                profile.WeeklyKey = key;
                profile.WeeklyProgress.Clear();
            }
        }

        public static string DayKey(DateTime now)
        {
            return now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Ключ недели — дата её понедельника (UTC не используется: дата берётся как передана).</summary>
        public static string WeekKey(DateTime now)
        {
            DateTime date = now.Date;
            int shift = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-shift).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>Покупка косметического предмета по цене из ContentDatabase.</summary>
        public static bool TryPurchase(AccountProfile profile, string cosmeticId)
        {
            if (profile == null) return false;
            CosmeticDef def = ContentDatabase.FindCosmetic(cosmeticId);
            if (def == null || profile.IsUnlocked(def.Id)) return false;
            if (def.Cost <= 0)
            {
                profile.Unlock(def.Id);
                return true;
            }
            if (profile.Credits < def.Cost) return false;
            profile.Credits -= def.Cost;
            profile.Unlock(def.Id);
            return true;
        }

        public static int ResearchCost(AccountProfile profile, string researchId)
        {
            if (profile == null) return -1;
            ResearchDef def = ContentDatabase.FindResearch(researchId);
            if (def == null) return -1;
            int level = Get(profile.Research, def.Id);
            if (level < 0) level = 0;
            if (level >= def.MaxLevel) return -1;
            long cost = (long)def.BaseCost * (level + 1);
            return cost > AccountProfile.MaxResource ? AccountProfile.MaxResource : (int)cost;
        }

        /// <summary>Повышает исследование, если хватает кредитов.</summary>
        public static bool TryResearch(AccountProfile profile, string researchId)
        {
            if (profile == null) return false;
            ResearchDef def = ContentDatabase.FindResearch(researchId);
            if (def == null) return false;
            int cost = ResearchCost(profile, def.Id);
            if (cost < 0 || profile.Credits < cost) return false;
            profile.Credits -= cost;
            profile.Research[def.Id] = Get(profile.Research, def.Id) + 1;
            return true;
        }

        private static void UpdateRecords(AccountProfile profile, MatchResult result, int score, int kills, int mass, int rank, bool newRecord)
        {
            SetMax(profile.Records, "bestScore", score);
            SetMax(profile.Records, "bestMass", mass);
            SetMax(profile.Records, "bestKills", kills);
            profile.Records["lastScore"] = score;
            string mode = result.Mode.ToString();
            SetMax(profile.Records, "score_" + mode, score);
            SetMax(profile.Records, "kills_" + mode, kills);
            if (rank > 0)
            {
                int bestRank;
                if (!profile.Records.TryGetValue("bestRank", out bestRank) || rank < bestRank) profile.Records["bestRank"] = rank;
            }
            if (newRecord) profile.Records["recordCount"] = Get(profile.Records, "recordCount") + 1;
        }

        private static void UpdateStats(AccountProfile profile, int score, int kills, int deaths, int energy,
            int crystals, int darkMatter, int artifacts, int bosses, int streak, int mass, bool won,
            int rank, int evolutionTier, int comeback)
        {
            Bump(profile.Stats, StatScore, score);
            Bump(profile.Stats, StatKills, kills);
            Bump(profile.Stats, StatDeaths, deaths);
            Bump(profile.Stats, StatEnergy, energy);
            Bump(profile.Stats, StatCrystals, crystals);
            Bump(profile.Stats, StatDarkMatter, darkMatter);
            Bump(profile.Stats, StatArtifacts, artifacts);
            Bump(profile.Stats, StatBosses, bosses);
            Bump(profile.Stats, StatRank1, rank == 1 ? 1 : 0);
            SetMax(profile.Stats, StatBestMass, mass);
            SetMax(profile.Stats, StatBestStreak, streak);
            SetMax(profile.Stats, StatEvolutionTier, evolutionTier);
            if (comeback > 0) SetMax(profile.Stats, "comeback", 1);
        }

        private static void ApplyAchievements(AccountProfile profile, ProgressionRewards rewards, Dictionary<string, int> metrics)
        {
            for (int i = 0; i < ContentDatabase.Achievements.Length; i++)
            {
                AchievementDef def = ContentDatabase.Achievements[i];
                if (def == null || ContainsId(profile.Achievements, def.Id)) continue;
                if (AchievementMetric(profile, metrics, def) < def.Target) continue;
                profile.Achievements.Add(def.Id);
                profile.AddCredits(def.Credits);
                profile.AddCrystals(def.Crystals);
                rewards.Achievements.Add(def.Name);
                rewards.Messages.Add("Достижение «" + def.Name + "»: +" + def.Credits.ToString(CultureInfo.InvariantCulture) +
                    " кр., +" + def.Crystals.ToString(CultureInfo.InvariantCulture) + " крист.");
                rewards.Credits += def.Credits;
                rewards.Crystals += def.Crystals;
            }
        }

        private static int AchievementMetric(AccountProfile profile, Dictionary<string, int> metrics, AchievementDef def)
        {
            switch (def.Id)
            {
                case "first_blood": return profile.Stat(StatKills);
                case "energy_1000": return Math.Max(profile.Stat(StatEnergy), Get(metrics, "energy"));
                case "mass_100": return Math.Max(profile.Stat(StatBestMass), Get(metrics, "mass"));
                case "kills_10": return profile.Stat(StatKills);
                case "streak_5": return Math.Max(profile.Stat(StatBestStreak), Get(metrics, "streak"));
                case "boss_1":
                case "boss_5": return profile.BossKills;
                case "rank_1":
                case "rank_1_10": return profile.Stat(StatRank1);
                case "evolution_3": return profile.Stat(StatEvolutionTier);
                case "artifact_10": return profile.Stat(StatArtifacts);
                case "dark_100": return profile.Stat(StatDarkMatter);
                case "hardcore_win": return Get(metrics, "hardcoreWin");
                case "long_run": return Get(metrics, "duration");
                case "comeback": return profile.Stat("comeback");
                case "collector": return profile.Owned.Count;
                case "rich": return profile.Credits;
                case "research": return TotalResearch(profile);
                case "perfect": return Get(metrics, "perfect");
                default:
                    int value;
                    return metrics.TryGetValue(def.Id, out value) ? value : 0;
            }
        }

        private static int TotalResearch(AccountProfile profile)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> pair in profile.Research) total += Math.Max(0, pair.Value);
            return total;
        }

        private static void ApplyQuests(AccountProfile profile, ProgressionRewards rewards,
            Dictionary<string, int> metrics, DateTime now)
        {
            List<QuestDef> dailySelected = ContentDatabase.DailyQuests(now);
            List<QuestDef> weeklySelected = ContentDatabase.WeeklyQuests(now);
            for (int i = 0; i < ContentDatabase.Quests.Length; i++)
            {
                QuestDef quest = ContentDatabase.Quests[i];
                if (quest == null) continue;
                bool weekly = quest.Weekly;
                Dictionary<string, int> period = weekly ? profile.WeeklyProgress : profile.DailyProgress;
                List<QuestDef> selected = weekly ? weeklySelected : dailySelected;
                int value = QuestMetric(quest.Field, metrics);
                Bump(profile.QuestProgress, quest.Id, value);
                if (string.Equals(quest.Field, "mass", StringComparison.OrdinalIgnoreCase))
                    SetMax(period, quest.Id, value);
                else Bump(period, quest.Id, value);

                if (!ContainsId(selected, quest.Id)) continue;
                if (Get(period, ClaimPrefix + quest.Id) > 0) continue;
                if (Get(period, quest.Id) < quest.Target) continue;
                period[ClaimPrefix + quest.Id] = 1;
                profile.AddCredits(quest.Credits);
                profile.AddCrystals(quest.Crystals);
                rewards.Quests.Add(quest.Name);
                rewards.Messages.Add("Задание «" + quest.Name + "»: +" + quest.Credits.ToString(CultureInfo.InvariantCulture) +
                    " кр., +" + quest.Crystals.ToString(CultureInfo.InvariantCulture) + " крист.");
                rewards.Credits += quest.Credits;
                rewards.Crystals += quest.Crystals;
            }
        }

        private static int QuestMetric(string field, Dictionary<string, int> metrics)
        {
            switch (field)
            {
                case "kills": return Get(metrics, "kills");
                case "energy": return Get(metrics, "energy");
                case "mass": return Get(metrics, "mass");
                case "crystals": return Get(metrics, "crystals");
                case "darkMatter": return Get(metrics, "darkMatter");
                case "matches": return 1;
                case "top3": return Get(metrics, "top3");
                case "wins": return Get(metrics, "wins");
                case "bosses": return Get(metrics, "bosses");
                default: return 0;
            }
        }

        private static void ApplySecrets(AccountProfile profile, ProgressionRewards rewards, Dictionary<string, int> metrics)
        {
            TrySecret(profile, rewards, "secret_flawless", "БЕЗУПРЕЧНЫЙ ПОЛЁТ", Get(metrics, "perfect") > 0);
            TrySecret(profile, rewards, "secret_streak", "ЦЕПНАЯ РЕАКЦИЯ", Get(metrics, "streak") >= 10);
            TrySecret(profile, rewards, "secret_titan", "ТИТАН", Get(metrics, "mass") >= 200);
            TrySecret(profile, rewards, "secret_ancient", "АРХЕОЛОГ", profile.Stat(StatArtifacts) >= 5);
            TrySecret(profile, rewards, "secret_collector", "КОЛЛЕКЦИОНЕР", profile.Owned.Count >= 10);
            TrySecret(profile, rewards, "secret_boss", "ОХОТНИК", profile.BossKills >= 1);
        }

        private static void TrySecret(AccountProfile profile, ProgressionRewards rewards, string id, string title, bool found)
        {
            if (!found || ContainsId(profile.Secrets, id)) return;
            profile.Secrets.Add(id);
            profile.AddCredits(200);
            profile.AddCrystals(1);
            profile.AddXp(50f);
            rewards.Secrets.Add(title);
            rewards.Messages.Add("Секрет «" + title + "»: +200 кр., +1 крист., +50 опыта.");
            rewards.Credits += 200;
            rewards.Crystals += 1;
            rewards.Xp += 50f;
        }

        private static bool ContainsId(List<string> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool ContainsId(List<QuestDef> list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static int HighestEvolutionTier(MatchResult result)
        {
            int best = 0;
            if (result.Highlights == null) return best;
            for (int i = 0; i < result.Highlights.Count; i++)
            {
                SimEvent e = result.Highlights[i];
                if (e == null || e.Kind != SimEventKind.Evolution) continue;
                if (e.Value > best) best = e.Value;
                EvolutionDef def = ContentDatabase.FindEvolution(e.Text);
                if (def != null && def.Tier > best) best = def.Tier;
            }
            return best;
        }

        private static bool HasAbsorb(MatchResult result)
        {
            if (result.Highlights == null) return false;
            for (int i = 0; i < result.Highlights.Count; i++)
            {
                if (result.Highlights[i] != null && result.Highlights[i].Kind == SimEventKind.Absorb) return true;
            }
            return false;
        }

        private static int WithEconomy(AccountProfile profile, int credits)
        {
            float bonus = profile.ResearchBonus("res_economy");
            if (bonus <= 0f) return credits;
            double value = Math.Round(credits * (1d + bonus), MidpointRounding.AwayFromZero);
            return ToInt(value);
        }

        private static int Scaled(double value, float scale)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            double factor = scale < 0f ? 0d : scale;
            return ToInt(Math.Round(value * factor, MidpointRounding.AwayFromZero));
        }

        private static int PositiveInt(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return 0;
            if (value >= AccountProfile.MaxResource) return AccountProfile.MaxResource;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static int PositiveInt(int value)
        {
            return value > 0 ? value : 0;
        }

        private static int ToInt(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            if (value <= 0d) return 0;
            if (value >= AccountProfile.MaxResource) return AccountProfile.MaxResource;
            return (int)value;
        }

        private static int Get(Dictionary<string, int> map, string key)
        {
            int value;
            if (map == null || string.IsNullOrEmpty(key) || !map.TryGetValue(key, out value)) return 0;
            return value;
        }

        private static void Bump(Dictionary<string, int> map, string key, int amount)
        {
            if (map == null || string.IsNullOrEmpty(key) || amount <= 0) return;
            int current = Get(map, key);
            map[key] = AccountProfile.SaturatingAdd(current, amount);
        }

        private static void SetMax(Dictionary<string, int> map, string key, int value)
        {
            if (map == null || string.IsNullOrEmpty(key)) return;
            if (value > Get(map, key)) map[key] = value;
        }
    }
}
