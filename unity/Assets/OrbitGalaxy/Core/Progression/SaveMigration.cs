using System;
using System.Collections.Generic;
using System.Globalization;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Детерминированное преобразование профиля в JSON схемы v5 и обратно.
    /// Старые сохранения v3/v4 читаются напрямую по старым ключам и не удаляются.
    /// Внешних зависимостей нет: используется встроенный кодек из этой же папки.
    /// </summary>
    public static class SaveMigration
    {
        public const int CurrentSchema = 5;

        /// <summary>Читает legacy v3/v4 JSON максимально поверхностно и безопасно.</summary>
        public static AccountProfile FromLegacy(string json)
        {
            AccountProfile profile = new AccountProfile();
            GalaxyJsonObject root = TryParse(json);
            if (root == null) return profile;
            Read(profile, root, true);
            profile.SchemaVersion = CurrentSchema;
            profile.Sanitize();
            return profile;
        }

        /// <summary>Читает сохранение v5; старые ключи тоже понимаются для совместимости.</summary>
        public static AccountProfile FromJson(string json)
        {
            AccountProfile profile = new AccountProfile();
            GalaxyJsonObject root = TryParse(json);
            if (root == null) return profile;
            Read(profile, root, false);
            profile.SchemaVersion = CurrentSchema;
            profile.Sanitize();
            return profile;
        }

        /// <summary>Возвращает стабильный JSON с фиксированным порядком полей.</summary>
        public static string ToJson(AccountProfile profile)
        {
            AccountProfile p = profile ?? new AccountProfile();
            int schema = p.SchemaVersion > 0 ? p.SchemaVersion : CurrentSchema;

            GalaxyJsonObject root = new GalaxyJsonObject();
            root.Set("schemaVersion", schema);
            root.Set("version", schema);
            root.Set("level", p.Level);
            root.Set("xp", p.Xp);
            root.Set("credits", p.Credits);
            root.Set("crystals", p.Crystals);
            root.Set("darkMatter", p.DarkMatter);
            root.Set("selectedShip", SafeText(p.SelectedShip));
            root.Set("selectedTrail", SafeText(p.SelectedTrail));
            root.Set("selectedDeathFx", SafeText(p.SelectedDeathFx));
            root.Set("selectedFrame", SafeText(p.SelectedFrame));
            root.Set("selectedAvatar", SafeText(p.SelectedAvatar));
            root.Set("selectedTitle", SafeText(p.SelectedTitle));
            root.Set("owned", GalaxyJson.StringArray(p.Owned));
            root.Set("achievements", GalaxyJson.StringArray(p.Achievements));
            root.Set("secrets", GalaxyJson.StringArray(p.Secrets));
            root.Set("records", GalaxyJson.MapObject(p.Records));
            root.Set("research", GalaxyJson.MapObject(p.Research));
            root.Set("stats", GalaxyJson.MapObject(p.Stats));
            root.Set("questProgress", GalaxyJson.MapObject(p.QuestProgress));
            root.Set("dailyProgress", GalaxyJson.MapObject(p.DailyProgress));
            root.Set("weeklyProgress", GalaxyJson.MapObject(p.WeeklyProgress));
            root.Set("dailyDate", SafeText(p.DailyDate));
            root.Set("weeklyKey", SafeText(p.WeeklyKey));
            root.Set("winStreak", p.WinStreak);
            root.Set("bestWinStreak", p.BestWinStreak);
            root.Set("matches", p.Matches);
            root.Set("wins", p.Wins);
            root.Set("bossKills", p.BossKills);
            root.Set("settings", SettingsToJson(p.Settings));
            return GalaxyJson.Stringify(root);
        }

        /// <summary>Эвристика для вызова явной миграции извне (файл при этом не трогается).</summary>
        public static bool LooksLikeLegacy(string json)
        {
            GalaxyJsonObject root = TryParse(json);
            if (root == null) return false;
            object schema = GalaxyJson.Get(root, "schemaVersion", "SaveSchemaVersion");
            if (schema != null) return GalaxyJson.Int(schema, CurrentSchema) < CurrentSchema;
            object version = GalaxyJson.Get(root, "version");
            if (version != null) return GalaxyJson.Int(version, CurrentSchema) < CurrentSchema;
            return GalaxyJson.Get(root, "playerLevel", "inventory", "bestByMode") != null;
        }

        private static GalaxyJsonObject TryParse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return GalaxyJson.Object(GalaxyJson.Parse(json));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string SafeText(string text)
        {
            return text ?? string.Empty;
        }

        private static GalaxyJsonObject SettingsToJson(GameSettings settings)
        {
            GameSettings s = settings ?? new GameSettings();
            GalaxyJsonObject obj = new GalaxyJsonObject();
            obj.Set("musicVolume", s.MusicVolume);
            obj.Set("sfxVolume", s.SfxVolume);
            obj.Set("muted", s.Muted);
            obj.Set("haptics", s.Haptics);
            obj.Set("reducedMotion", s.ReducedMotion);
            obj.Set("colorblind", s.Colorblind);
            obj.Set("largeText", s.LargeText);
            obj.Set("quality", SafeText(s.Quality));
            obj.Set("controlLayout", SafeText(s.ControlLayout));
            obj.Set("autoFire", s.AutoFire);
            obj.Set("showFps", s.ShowFps);
            return obj;
        }

        private static void Read(AccountProfile p, GalaxyJsonObject root, bool legacy)
        {
            object xpValue = GalaxyJson.Get(root, "xp", "accountXp", "experience", "exp");
            object levelValue = GalaxyJson.Get(root, "level", "playerLevel", "accountLevel", "lvl");
            int level = GalaxyJson.Int(levelValue, -1);
            if (xpValue == null && level > 1 && legacy) p.Xp = AccountProfile.TotalXpForLevel(level);
            else p.Xp = GalaxyJson.Float(xpValue, p.Xp);
            p.Level = level > 0 ? level : AccountProfile.LevelForXp(p.Xp);

            p.Credits = GalaxyJson.Int(GalaxyJson.Get(root, "credits", "credit", "money"), p.Credits);
            p.Crystals = GalaxyJson.Int(GalaxyJson.Get(root, "crystals", "crystal", "gems"), p.Crystals);
            p.DarkMatter = GalaxyJson.Int(GalaxyJson.Get(root, "darkMatter", "darkmatter", "dark_matter"), p.DarkMatter);

            p.SelectedShip = GalaxyJson.Text(GalaxyJson.Get(root, "selectedShip", "ship", "shipId"), null);
            p.SelectedTrail = GalaxyJson.Text(GalaxyJson.Get(root, "selectedTrail", "trail", "trailId"), null);
            p.SelectedDeathFx = GalaxyJson.Text(GalaxyJson.Get(root, "selectedDeathFx", "deathFx", "death"), null);
            p.SelectedFrame = GalaxyJson.Text(GalaxyJson.Get(root, "selectedFrame", "frame"), null);
            p.SelectedAvatar = GalaxyJson.Text(GalaxyJson.Get(root, "selectedAvatar", "avatar"), null);
            p.SelectedTitle = GalaxyJson.Text(GalaxyJson.Get(root, "selectedTitle", "title"), null);

            //Legacy v3/v4 хранил экипировку числовыми индексами.
            if (p.SelectedShip == null) p.SelectedShip = IndexedId(root, "equippedShip", ContentDatabase.ShipVisuals);
            if (p.SelectedTrail == null) p.SelectedTrail = IndexedCosmetic(root, "equippedTrail", "trail");
            if (p.SelectedFrame == null && legacy) p.SelectedFrame = IndexedCosmetic(root, "equippedPlanet", "frame");

            List<string> owned = GalaxyJson.StringList(GalaxyJson.Get(root, "owned", "inventory", "unlocked", "ownedItems", "ownedShips"));
            owned.AddRange(GalaxyJson.StringList(GalaxyJson.Get(root, "ownedTrails", "trails")));
            owned.AddRange(GalaxyJson.StringList(GalaxyJson.Get(root, "ownedPlanets", "planets")));
            for (int i = 0; i < owned.Count; i++) owned[i] = MapLegacyId(owned[i]);
            p.Owned = owned;

            p.Achievements = GalaxyJson.StringList(GalaxyJson.Get(root, "achievements", "achieved"));
            p.Secrets = GalaxyJson.StringList(GalaxyJson.Get(root, "secrets", "foundSecrets"));

            p.Records = GalaxyJson.IntMap(GalaxyJson.Get(root, "records", "bestByMode", "bests", "highScores"));
            object bestScore = GalaxyJson.Get(root, "bestScore", "best", "score");
            if (bestScore != null && !p.Records.ContainsKey("bestScore")) p.Records["bestScore"] = GalaxyJson.Int(bestScore, 0);

            p.Research = GalaxyJson.IntMap(GalaxyJson.Get(root, "research", "researches", "upgradeLevels", "upgrades"));
            Dictionary<string, int> mappedResearch = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in p.Research)
            {
                string id = MapLegacyResearch(pair.Key);
                if (!mappedResearch.ContainsKey(id) || mappedResearch[id] < pair.Value) mappedResearch[id] = pair.Value;
            }
            p.Research = mappedResearch;

            p.Stats = GalaxyJson.IntMap(GalaxyJson.Get(root, "stats", "statistics", "lifetimeStats"));
            p.QuestProgress = GalaxyJson.IntMap(GalaxyJson.Get(root, "questProgress", "quests"));

            DateTime date = ParseDate(GalaxyJson.Text(GalaxyJson.Get(root, "dailyDate", "day"), null));
            p.DailyDate = GalaxyJson.Text(GalaxyJson.Get(root, "dailyDate", "day"), date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            p.WeeklyKey = GalaxyJson.Text(GalaxyJson.Get(root, "weeklyKey", "week"), null);

            object daily = GalaxyJson.Get(root, "dailyProgress", "daily");
            p.DailyProgress = ReadProgress(daily, date, false);
            ApplyLegacyClaims(p.DailyProgress, GalaxyJson.Get(root, "dailyClaimed"), date, false);

            object weekly = GalaxyJson.Get(root, "weeklyProgress", "weekly");
            p.WeeklyProgress = ReadProgress(weekly, date, true);
            ApplyLegacyClaims(p.WeeklyProgress, GalaxyJson.Get(root, "weeklyClaimed"), date, true);

            p.WinStreak = GalaxyJson.Int(GalaxyJson.Get(root, "winStreak", "streak", "dailyStreak"), p.WinStreak);
            p.BestWinStreak = GalaxyJson.Int(GalaxyJson.Get(root, "bestWinStreak", "bestStreak"), p.BestWinStreak);
            p.Matches = GalaxyJson.Int(GalaxyJson.Get(root, "matches", "games", "runCount"), p.Matches);
            p.Wins = GalaxyJson.Int(GalaxyJson.Get(root, "wins", "victories"), p.Wins);
            p.BossKills = GalaxyJson.Int(GalaxyJson.Get(root, "bossKills", "bosses"), p.BossKills);

            object settingsValue = GalaxyJson.Get(root, "settings", "options");
            GalaxyJsonObject settingsObject = GalaxyJson.Object(settingsValue);
            if (settingsObject != null) ReadSettings(p.Settings, settingsObject);

            object mode = GalaxyJson.Get(root, "selectedMode", "mode");
            if (mode != null) p.Stats["mode"] = GalaxyJson.Int(mode, 0);
        }

        private static void ReadSettings(GameSettings s, GalaxyJsonObject obj)
        {
            s.MusicVolume = GalaxyJson.Float(GalaxyJson.Get(obj, "musicVolume", "music", "music_volume"), s.MusicVolume);
            s.SfxVolume = GalaxyJson.Float(GalaxyJson.Get(obj, "sfxVolume", "sfx", "sfx_volume"), s.SfxVolume);
            s.Muted = GalaxyJson.Bool(GalaxyJson.Get(obj, "muted"), s.Muted);
            s.Haptics = GalaxyJson.Bool(GalaxyJson.Get(obj, "haptics"), s.Haptics);
            s.ReducedMotion = GalaxyJson.Bool(GalaxyJson.Get(obj, "reducedMotion", "reduced_motion"), s.ReducedMotion);
            s.Colorblind = GalaxyJson.Bool(GalaxyJson.Get(obj, "colorblind"), s.Colorblind);
            s.LargeText = GalaxyJson.Bool(GalaxyJson.Get(obj, "largeText", "large_text"), s.LargeText);
            s.Quality = GalaxyJson.Text(GalaxyJson.Get(obj, "quality", "qualityLevel"), s.Quality);
            s.ControlLayout = GalaxyJson.Text(GalaxyJson.Get(obj, "controlLayout", "controls", "layout"), s.ControlLayout);
            s.AutoFire = GalaxyJson.Bool(GalaxyJson.Get(obj, "autoFire", "auto_fire"), s.AutoFire);
            s.ShowFps = GalaxyJson.Bool(GalaxyJson.Get(obj, "showFps", "show_fps"), s.ShowFps);
            s.Sanitize();
        }

        private static string IndexedId(GalaxyJsonObject root, string key, ShipVisualDef[] defs)
        {
            int index = GalaxyJson.Int(GalaxyJson.Get(root, key), -1);
            if (index < 0 || defs == null || index >= defs.Length) return null;
            return defs[index].Id;
        }

        private static string IndexedCosmetic(GalaxyJsonObject root, string key, string category)
        {
            int index = GalaxyJson.Int(GalaxyJson.Get(root, key), -1);
            if (index < 0) return null;
            int current = 0;
            for (int i = 0; i < ContentDatabase.Cosmetics.Length; i++)
            {
                CosmeticDef def = ContentDatabase.Cosmetics[i];
                if (!string.Equals(def.Category, category, StringComparison.OrdinalIgnoreCase)) continue;
                if (current == index) return def.Id;
                current++;
            }
            return null;
        }

        private static Dictionary<string, int> ReadProgress(object value, DateTime date, bool weekly)
        {
            Dictionary<string, int> result = GalaxyJson.IntMap(value);
            List<object> array = GalaxyJson.Array(value);
            if (array != null)
            {
                List<QuestDef> quests = weekly ? ContentDatabase.WeeklyQuests(date) : ContentDatabase.DailyQuests(date);
                for (int i = 0; i < array.Count; i++)
                {
                    string key = quests != null && i < quests.Count ? quests[i].Id : "slot_" + i.ToString(CultureInfo.InvariantCulture);
                    if (!result.ContainsKey(key)) result[key] = GalaxyJson.Int(array[i], 0);
                }
            }
            return result;
        }

        private static void ApplyLegacyClaims(Dictionary<string, int> progress, object claimed, DateTime date, bool weekly)
        {
            List<object> array = GalaxyJson.Array(claimed);
            if (array == null) return;
            List<QuestDef> quests = weekly ? ContentDatabase.WeeklyQuests(date) : ContentDatabase.DailyQuests(date);
            for (int i = 0; i < array.Count; i++)
            {
                if (!GalaxyJson.Bool(array[i], false)) continue;
                string id = quests != null && i < quests.Count ? quests[i].Id : "slot_" + i.ToString(CultureInfo.InvariantCulture);
                progress["claimed:" + id] = 1;
            }
        }

        private static DateTime ParseDate(string text)
        {
            DateTime value;
            if (!string.IsNullOrEmpty(text) &&
                DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return value;
            return DateTime.UtcNow;
        }

        private static string MapLegacyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            string[] oldShips = { "ship_cyan", "ship_rose", "ship_lime", "ship_violet", "ship_amber", "ship_magenta", "ship_ice", "ship_mint" };
            for (int i = 0; i < oldShips.Length; i++)
            {
                if (string.Equals(id, oldShips[i], StringComparison.OrdinalIgnoreCase) &&
                    i < ContentDatabase.ShipVisuals.Length) return ContentDatabase.ShipVisuals[i].Id;
            }
            switch (id)
            {
                case "trail_smoke": return "trail_ice";
                case "trail_rainbow": return "trail_plasma";
                case "trail_wave": return "trail_toxic";
                case "planet_earth": return "frame_default";
                case "planet_mars": return "frame_solar";
                case "planet_venus": return "frame_void";
                default: return id;
            }
        }

        private static string MapLegacyResearch(string key)
        {
            switch (key)
            {
                case "0": return "res_armor";
                case "1": return "res_agility";
                case "2": return "res_magnet";
                case "3": return "res_luck";
                case "4": return "res_economy";
                case "upg_shield": return "res_armor";
                case "upg_time": return "res_agility";
                case "upg_magnet": return "res_magnet";
                case "upg_slow": return "res_luck";
                case "upg_double": return "res_economy";
                default: return key;
            }
        }
    }
}
