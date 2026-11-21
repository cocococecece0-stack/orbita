using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit.Core
{
    /// <summary>
    /// Port of native SaveStore.submitRun: rewards, achievements, missions, season tiers,
    /// secrets and streaks. Keeps the existing Unity entry point.
    /// </summary>
    public static class Progression
    {
        public static RewardSummary Apply(SaveData save, RunRecord record, GameEngineCore engine)
        {
            var reward = new RewardSummary();
            if (save == null || record == null) return reward;

            save.EnsureDefaults();
            RunRecord rr = ClampRecord(record);
            string modeId = IsModeId(rr.mode) ? rr.mode : "classic";
            rr.mode = modeId;

            string day = Content.Today();
            if (save.dailyDate != day)
            {
                save.dailyDate = day;
                save.dailyProgress = new List<int> { 0, 0, 0 };
                save.dailyClaimed = new List<bool> { false, false, false };
            }
            string season = Content.SeasonIdString();
            if (save.SeasonIdKey != season)
            {
                save.SeasonIdKey = season;
                save.seasonXp = 0;
                save.seasonClaimed = new List<bool> { false, false, false, false };
            }

            reward.creditsBefore = save.credits;
            int previousBest;
            bool isBest = rr.score > (save.bestByMode.TryGetValue(modeId, out previousBest) ? previousBest : 0);
            reward.newBest = isBest;
            if (isBest) save.bestByMode[modeId] = rr.score;

            List<int> top;
            if (!save.topByMode.TryGetValue(modeId, out top))
            {
                top = new List<int>();
                save.topByMode[modeId] = top;
            }
            top.Add(rr.score);
            top.Sort((a, b) => b.CompareTo(a));
            while (top.Count > 10) top.RemoveAt(top.Count - 1);

            save.history.Insert(0, rr);
            while (save.history.Count > 25) save.history.RemoveAt(save.history.Count - 1);

            if (!save.modesPlayed.Contains(modeId)) save.modesPlayed.Add(modeId);
            if (!save.visitedSectors.Contains(rr.sector)) save.visitedSectors.Add(rr.sector);
            if (modeId == "zen") save.zenRuns = SaveData.SaturatingAdd(save.zenRuns, 1);

            string yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (save.lastPlayedDay == day) { /* keep streak */ }
            else if (save.lastPlayedDay == yesterday) save.dailyStreak = SaveData.SaturatingAdd(save.dailyStreak, 1);
            else save.dailyStreak = 1;
            save.lastPlayedDay = day;

            save.seasonXp = SaveData.SaturatingAdd(save.seasonXp, rr.score / 25);

            int credits = SaveData.SaturatingAdd(rr.score / 10,
                SaveData.SaturatingAdd(rr.orbs, rr.rareOrbs * 20));
            int xp = Math.Max(0, rr.score / 20);
            int levelBefore = save.playerLevel;

            List<string> achieved = engine != null ? engine.AchievedIds() : DeriveAchievements(rr);
            if (rr.completed && !achieved.Contains("objective_done")) achieved.Add("objective_done");

            var newAchievements = new List<string>();
            for (int i = 0; i < Content.Achievements.Length; i++)
            {
                AchievementDef def = Content.Achievements[i];
                if (save.achievements.Contains(def.Id) || !achieved.Contains(def.Id)) continue;
                save.achievements.Add(def.Id);
                credits = SaveData.SaturatingAdd(credits, def.Reward);
                xp = SaveData.SaturatingAdd(xp, def.Xp);
                newAchievements.Add(def.Title);
            }
            reward.achievements.AddRange(newAchievements);

            // Objective completion is paid separately from achievements.
            if (achieved.Contains("objective_done"))
            {
                credits = SaveData.SaturatingAdd(credits, 120);
                xp = SaveData.SaturatingAdd(xp, 30);
            }

            List<Mission> missions = Content.Missions(save.dailyDate);
            var values = new Dictionary<string, int>
            {
                { "orbs", rr.orbs },
                { "dodges", rr.dodges },
                { "nearMisses", rr.nearMisses },
                { "maxCombo", rr.maxCombo },
                { "score", rr.score },
                { "bossKills", rr.bossKills },
                { "wormholes", rr.wormholes },
                { "level", rr.level }
            };
            for (int i = 0; i < missions.Count && i < save.dailyProgress.Count && i < save.dailyClaimed.Count; i++)
            {
                Mission m = missions[i];
                int value;
                if (!values.TryGetValue(m.Field, out value)) value = 0;
                if (value > save.dailyProgress[i]) save.dailyProgress[i] = value;
                if (save.dailyProgress[i] >= m.Goal && !save.dailyClaimed[i])
                {
                    save.dailyClaimed[i] = true;
                    credits = SaveData.SaturatingAdd(credits, m.Reward);
                    xp = SaveData.SaturatingAdd(xp, m.Reward / 3);
                    reward.missions.Add(m.Title + " +" + m.Reward.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (achieved.Contains("objective_done"))
                reward.missions.Add("ЦЕЛЬ ВЫПОЛНЕНА +120");

            List<string> foundSecrets = engine != null ? engine.SecretIds() : DeriveSecrets(rr);
            for (int i = 0; i < foundSecrets.Count; i++)
            {
                string id = foundSecrets[i];
                if (save.secrets.Contains(id)) continue;
                save.secrets.Add(id);
                Secret def = FindSecret(id);
                if (def == null) continue;
                credits = SaveData.SaturatingAdd(credits, 200);
                xp = SaveData.SaturatingAdd(xp, 100);
                reward.secrets.Add(def.Title);
                if (id == "collector") UnlockSector(save, 7);
            }
            if (save.secrets.Contains("eye") || save.achievements.Contains("boss_1"))
                UnlockSector(save, 7);

            for (int i = 0; i < Content.SeasonTiers.Length && i < save.seasonClaimed.Count; i++)
            {
                SeasonTier tier = Content.SeasonTiers[i];
                if (save.seasonXp >= tier.Xp && !save.seasonClaimed[i])
                {
                    save.seasonClaimed[i] = true;
                    credits = SaveData.SaturatingAdd(credits, tier.Reward);
                    reward.seasonTiers.Add(i);
                }
            }

            if (SaveData.SaturatingAdd(save.credits, credits) >= 5000 &&
                !save.achievements.Contains("rich_5000"))
            {
                save.achievements.Add("rich_5000");
                credits = SaveData.SaturatingAdd(credits, 300);
                AchievementDef rich = FindAchievement("rich_5000");
                if (rich != null) reward.achievements.Add(rich.Title);
            }

            save.credits = SaveData.SaturatingAdd(save.credits, credits);
            save.xp = SaveData.SaturatingAdd(save.xp, xp);
            save.EnsureDefaults();

            reward.credits = credits;
            reward.xp = xp;
            reward.levels = Math.Max(0, save.playerLevel - levelBefore);
            return reward;
        }

        private static void UnlockSector(SaveData save, int id)
        {
            SectorDef sector = Content.Sector(id);
            if (sector.UnlockLevel == 99 && !save.visitedSectors.Contains(id))
                save.visitedSectors.Add(id);
        }

        private static RunRecord ClampRecord(RunRecord r)
        {
            return new RunRecord
            {
                score = SaveData.Clamp(r.score, 0, SaveData.Max),
                sector = SaveData.Clamp(r.sector, 0, Content.Sectors.Length - 1),
                level = SaveData.Clamp(r.level, 1, 10000),
                orbs = SaveData.Clamp(r.orbs, 0, SaveData.Max),
                maxCombo = SaveData.Clamp(r.maxCombo, 0, SaveData.Max),
                nearMisses = SaveData.Clamp(r.nearMisses, 0, SaveData.Max),
                dodges = SaveData.Clamp(r.dodges, 0, SaveData.Max),
                bossKills = SaveData.Clamp(r.bossKills, 0, SaveData.Max),
                wormholes = SaveData.Clamp(r.wormholes, 0, SaveData.Max),
                rareOrbs = SaveData.Clamp(r.rareOrbs, 0, SaveData.Max),
                seconds = SaveData.Clamp(r.seconds, 0, 100000),
                timestamp = r.timestamp,
                completed = r.completed,
                mode = string.IsNullOrEmpty(r.mode) ? "classic" : r.mode
            };
        }

        private static bool IsModeId(string id)
        {
            for (int i = 0; i < Content.Modes.Length; i++)
                if (Content.Modes[i].Id == id) return true;
            return false;
        }

        private static List<string> DeriveAchievements(RunRecord r)
        {
            var result = new List<string>();
            Add(result, "first_run", true);
            Add(result, "score_500", r.score >= 500);
            Add(result, "score_1500", r.score >= 1500);
            Add(result, "score_5000", r.score >= 5000);
            Add(result, "combo_10", r.maxCombo >= 10);
            Add(result, "combo_25", r.maxCombo >= 25);
            Add(result, "orbs_50", r.orbs >= 50);
            Add(result, "near_10", r.nearMisses >= 10);
            Add(result, "near_30", r.nearMisses >= 30);
            Add(result, "level_10", r.level >= 10);
            Add(result, "level_20", r.level >= 20);
            Add(result, "boss_1", r.bossKills >= 1);
            Add(result, "boss_5", r.bossKills >= 5);
            Add(result, "worm_5", r.wormholes >= 5);
            Add(result, "stack_40", r.mode == "stack" && r.orbs >= 40);
            Add(result, "objective_done", r.completed);
            return result;
        }

        private static List<string> DeriveSecrets(RunRecord r)
        {
            var result = new List<string>();
            Add(result, "eye", r.rareOrbs >= 3);
            Add(result, "dancer", r.nearMisses >= 25 && r.seconds <= 60);
            return result;
        }

        private static void Add(List<string> list, string id, bool ok)
        {
            if (ok) list.Add(id);
        }

        private static AchievementDef FindAchievement(string id)
        {
            for (int i = 0; i < Content.Achievements.Length; i++)
                if (Content.Achievements[i].Id == id) return Content.Achievements[i];
            return null;
        }

        private static Secret FindSecret(string id)
        {
            for (int i = 0; i < Content.Secrets.Length; i++)
                if (Content.Secrets[i].Id == id) return Content.Secrets[i];
            return null;
        }
    }
}
