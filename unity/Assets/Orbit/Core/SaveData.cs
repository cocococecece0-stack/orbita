using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Orbit.Core
{
    /// <summary>Native GameSettings (JSON "settings") with legacy Unity volume aliases.</summary>
    public sealed class GameSettings
    {
        [JsonProperty("music")] public float music = 0.7f;
        [JsonProperty("sfx")] public float sfx = 0.9f;
        [JsonProperty("muted")] public bool muted;
        [JsonProperty("haptics")] public bool haptics = true;
        [JsonProperty("reducedMotion")] public bool reducedMotion;
        [JsonProperty("colorblind")] public bool colorblind;
        [JsonProperty("largeText")] public bool largeText;
        [JsonProperty("audioProfile")] public string audioProfile = "BALANCED";
        [JsonProperty("hapticProfile")] public string hapticProfile = "CLASSIC";

        [JsonIgnore]
        public float musicVolume
        {
            get { return music; }
            set { music = value; }
        }

        [JsonIgnore]
        public float sfxVolume
        {
            get { return sfx; }
            set { sfx = value; }
        }
    }

    /// <summary>Run statistics. Native RunStats plus the fields the Unity UI already reads.</summary>
    public sealed class RunStats
    {
        public int score;
        public int level = 1;
        public int orbs;
        public int goldenOrbs;
        public int rareOrbs;
        public int maxCombo;
        public int nearMisses;
        public int dodges;
        public int bossKills;
        public int wormholes;
        public int seconds;
        public int powerups;
        public int hits;

        // Legacy aliases used by the Unity HUD.
        [JsonIgnore] public int RareOrbs { get { return rareOrbs; } set { rareOrbs = value; } }
        [JsonIgnore] public int MaxCombo { get { return maxCombo; } set { maxCombo = value; } }
    }

    /// <summary>JSON v4 run record. The native schema has no "completed" key.</summary>
    public sealed class RunRecord
    {
        [JsonProperty("score")] public int score;
        [JsonProperty("mode")] public string mode = "classic";
        [JsonProperty("sector")] public int sector;
        [JsonProperty("level")] public int level = 1;
        [JsonProperty("orbs")] public int orbs;
        [JsonProperty("maxCombo")] public int maxCombo;
        [JsonProperty("nearMisses")] public int nearMisses;
        [JsonProperty("dodges")] public int dodges;
        [JsonProperty("bossKills")] public int bossKills;
        [JsonProperty("wormholes")] public int wormholes;
        [JsonProperty("rareOrbs")] public int rareOrbs;
        [JsonProperty("seconds")] public int seconds;
        [JsonProperty("timestamp")] public long timestamp;

        /// <summary>Unity-only flag: objective completed. Not part of the native save schema.</summary>
        [JsonIgnore] public bool completed;
    }

    public sealed class RewardSummary
    {
        public int credits;
        public int xp;
        public int levels;
        public readonly List<string> achievements = new List<string>();

        // Native RewardSummary extras.
        public readonly List<string> missions = new List<string>();
        public readonly List<string> secrets = new List<string>();
        public readonly List<int> seasonTiers = new List<int>();
        public bool newBest;
        public int creditsBefore;

        [JsonIgnore]
        public int levelUps
        {
            get { return levels; }
            set { levels = value; }
        }
    }

    /// <summary>
    /// JSON v4 save data. Canonical fields use native names; the old Unity-only fields survive
    /// as [JsonIgnore] compatibility properties.
    ///
    /// seasonId note: the native schema stores a STRING such as "S02". The public int field
    /// "seasonId" is kept for source compatibility and maps onto the canonical string property
    /// SeasonIdKey, which is the one emitted as JSON "seasonId".
    /// </summary>
    public sealed class SaveData
    {
        public const int Max = 100000000;
        public const int MAX = Max; // native SaveData.MAX alias

        [JsonProperty("version")] public int version = 4;
        [JsonProperty("credits")] public int credits;
        [JsonProperty("xp")] public int xp;
        [JsonProperty("bestByMode")] public Dictionary<string, int> bestByMode = new Dictionary<string, int>();
        [JsonProperty("topByMode")] public Dictionary<string, List<int>> topByMode = new Dictionary<string, List<int>>();
        [JsonProperty("history")] public List<RunRecord> history = new List<RunRecord>();
        [JsonProperty("achievements")] public List<string> achievements = new List<string>();
        [JsonProperty("dailyDate")] public string dailyDate = "";
        [JsonProperty("dailyProgress")] public List<int> dailyProgress = new List<int> { 0, 0, 0 };
        [JsonProperty("dailyClaimed")] public List<bool> dailyClaimed = new List<bool> { false, false, false };
        [JsonProperty("dailyStreak")] public int dailyStreak;
        [JsonProperty("lastPlayedDay")] public string lastPlayedDay = "";
        [JsonProperty("seasonId")] public string SeasonIdKey = "";
        [JsonProperty("seasonXp")] public int seasonXp;
        [JsonProperty("seasonClaimed")] public List<bool> seasonClaimed = new List<bool> { false, false, false, false };
        [JsonProperty("inventory")] public List<string> inventory = new List<string> { "ship_cyan", "trail_spark", "planet_earth" };
        [JsonProperty("equippedShip")] public int equippedShip;
        [JsonProperty("equippedTrail")] public int equippedTrail;
        [JsonProperty("equippedPlanet")] public int equippedPlanet;
        [JsonProperty("upgrades")] public Dictionary<string, int> upgradeLevels = new Dictionary<string, int>();
        [JsonProperty("secrets")] public List<string> secrets = new List<string>();
        [JsonProperty("visitedSectors")] public List<int> visitedSectors = new List<int>();
        [JsonProperty("modesPlayed")] public List<string> modesPlayed = new List<string>();
        [JsonProperty("zenRuns")] public int zenRuns;
        [JsonProperty("selectedMode")] public string selectedMode = "classic";
        [JsonProperty("selectedSector")] public int selectedSector;
        [JsonProperty("mutedTrack")] public int mutedTrack = -1;
        [JsonProperty("tutorialSeen")] public bool tutorialSeen;
        [JsonProperty("settings")] public GameSettings settings = new GameSettings();

        private static readonly string[] UpgradeIds =
        {
            "upg_shield", "upg_time", "upg_magnet", "upg_slow", "upg_double"
        };

        // Stable views for the legacy Unity shop. The shop holds on to these lists and mutates
        // them in place; they are flushed into the canonical inventory/upgrades at every
        // persistence boundary. The initialized flags keep deserialized native JSON authoritative.
        private List<string> _legacyShips = new List<string>();
        private List<string> _legacyTrails = new List<string>();
        private List<string> _legacyPlanets = new List<string>();
        private List<int> _legacyUpgrades = new List<int>();
        private bool _legacyShipsInitialized;
        private bool _legacyTrailsInitialized;
        private bool _legacyPlanetsInitialized;
        private bool _legacyUpgradesInitialized;

        // ---------------- Legacy compatibility surface ----------------

        /// <summary>Computed exactly like native: 1 + floor(xp / 100).</summary>
        [JsonIgnore]
        public int playerLevel
        {
            get { return 1 + (Math.Max(0, xp) / Content.XpPerLevel); }
            set { xp = Math.Max(0, (value - 1) * Content.XpPerLevel); }
        }

        [JsonIgnore]
        public string seasonIdString
        {
            get { return SeasonIdKey; }
            set { SeasonIdKey = value; }
        }

        [JsonIgnore]
        public int seasonId
        {
            get
            {
                string id = SeasonIdKey;
                if (string.IsNullOrEmpty(id)) return Content.SeasonId();
                int value;
                string digits = (id[0] == 'S' || id[0] == 's') ? id.Substring(1) : id;
                if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                    return Math.Max(1, value);
                return Content.SeasonId();
            }
            set { SeasonIdKey = Content.SeasonKey(value); }
        }

        [JsonIgnore]
        public List<string> ownedShips
        {
            get { return GetOwnedCache("ship", _legacyShips, ref _legacyShipsInitialized); }
            set { SetOwnedCache("ship", value, _legacyShips, ref _legacyShipsInitialized); }
        }

        [JsonIgnore]
        public List<string> ownedTrails
        {
            get { return GetOwnedCache("trail", _legacyTrails, ref _legacyTrailsInitialized); }
            set { SetOwnedCache("trail", value, _legacyTrails, ref _legacyTrailsInitialized); }
        }

        [JsonIgnore]
        public List<string> ownedPlanets
        {
            get { return GetOwnedCache("planet", _legacyPlanets, ref _legacyPlanetsInitialized); }
            set { SetOwnedCache("planet", value, _legacyPlanets, ref _legacyPlanetsInitialized); }
        }

        /// <summary>Legacy five-slot upgrade list mapped onto native upgrade ids.</summary>
        [JsonIgnore]
        public List<int> upgrades
        {
            get { return GetUpgradeCache(ref _legacyUpgradesInitialized); }
            set { SetUpgradeCache(value, ref _legacyUpgradesInitialized); }
        }

        private List<string> GetOwnedCache(string category, List<string> cache, ref bool initialized)
        {
            RefreshOwnedCache(category, cache);
            initialized = true;
            return cache;
        }

        private void SetOwnedCache(string category, List<string> values, List<string> cache, ref bool initialized)
        {
            cache.Clear();
            if (values != null) cache.AddRange(values);
            ApplyOwnedValues(category, values);
            initialized = true;
        }

        private List<int> GetUpgradeCache(ref bool initialized)
        {
            var result = _legacyUpgrades;
            result.Clear();
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                int value;
                result.Add(upgradeLevels != null && upgradeLevels.TryGetValue(UpgradeIds[i], out value) ? value : 0);
            }
            initialized = true;
            return result;
        }

        private void SetUpgradeCache(List<int> values, ref bool initialized)
        {
            _legacyUpgrades.Clear();
            for (int i = 0; i < UpgradeIds.Length; i++)
                _legacyUpgrades.Add(values != null && i < values.Count ? values[i] : 0);
            ApplyUpgradeValues(_legacyUpgrades);
            initialized = true;
        }

        /// <summary>Called before sanitizing so shop mutations survive a save round-trip.</summary>
        internal void SyncLegacyToCanonical()
        {
            if (_legacyShipsInitialized) ApplyOwnedValues("ship", _legacyShips);
            if (_legacyTrailsInitialized) ApplyOwnedValues("trail", _legacyTrails);
            if (_legacyPlanetsInitialized) ApplyOwnedValues("planet", _legacyPlanets);
            if (_legacyUpgradesInitialized) ApplyUpgradeValues(_legacyUpgrades);
        }

        /// <summary>Called after sanitizing so the stable lists mirror canonical JSON state.</summary>
        private void SyncLegacyFromCanonical()
        {
            if (_legacyShipsInitialized) RefreshOwnedCache("ship", _legacyShips);
            if (_legacyTrailsInitialized) RefreshOwnedCache("trail", _legacyTrails);
            if (_legacyPlanetsInitialized) RefreshOwnedCache("planet", _legacyPlanets);
            if (_legacyUpgradesInitialized)
            {
                _legacyUpgrades.Clear();
                for (int i = 0; i < UpgradeIds.Length; i++)
                {
                    int value;
                    _legacyUpgrades.Add(upgradeLevels != null && upgradeLevels.TryGetValue(UpgradeIds[i], out value) ? value : 0);
                }
            }
        }

        private List<string> OwnedByCategory(string category)
        {
            var result = new List<string>();
            if (inventory == null) return result;
            for (int i = 0; i < inventory.Count; i++)
            {
                ShopItem item = FindShopItem(inventory[i]);
                if (item != null && item.Category == category)
                    result.Add(item.Value.ToString(CultureInfo.InvariantCulture));
            }
            return result;
        }

        private void ApplyOwnedValues(string category, List<string> values)
        {
            if (inventory == null) inventory = new List<string>();
            for (int i = inventory.Count - 1; i >= 0; i--)
            {
                ShopItem item = FindShopItem(inventory[i]);
                if (item != null && item.Category == category) inventory.RemoveAt(i);
            }
            if (values == null) values = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                int value;
                if (!int.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) continue;
                string id = FindShopId(category, value);
                if (id != null && !inventory.Contains(id)) inventory.Add(id);
            }
            // Native sanitize() always keeps the free starter item in every category.
            string fallback = category == "ship" ? "ship_cyan" : (category == "trail" ? "trail_spark" : "planet_earth");
            if (!inventory.Contains(fallback)) inventory.Add(fallback);
        }

        private void RefreshOwnedCache(string category, List<string> cache)
        {
            cache.Clear();
            cache.AddRange(OwnedByCategory(category));
        }

        private void ApplyUpgradeValues(List<int> values)
        {
            if (upgradeLevels == null) upgradeLevels = new Dictionary<string, int>();
            for (int i = 0; i < UpgradeIds.Length; i++)
            {
                int v = values != null && i < values.Count ? values[i] : 0;
                upgradeLevels[UpgradeIds[i]] = v < 0 ? 0 : (v > 1 ? 1 : v);
            }
        }

        private static ShopItem FindShopItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Content.Shop.Length; i++)
                if (Content.Shop[i].Id == id) return Content.Shop[i];
            return null;
        }

        private static string FindShopId(string category, int value)
        {
            for (int i = 0; i < Content.Shop.Length; i++)
                if (Content.Shop[i].Category == category && Content.Shop[i].Value == value)
                    return Content.Shop[i].Id;
            return null;
        }

        // ---------------- Validation (native SaveData.sanitize) ----------------

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int SaturatingAdd(int a, int b)
        {
            long sum = Math.Max(0, a) + (long)Math.Max(0, b);
            if (sum > Max) return Max;
            return (int)sum;
        }

        /// <summary>Equivalent of native SaveData.sanitize().</summary>
        public void Sanitize()
        {
            version = 4;
            credits = Clamp(credits, 0, Max);
            xp = Clamp(xp, 0, Max);
            seasonXp = Clamp(seasonXp, 0, Max);
            dailyStreak = Clamp(dailyStreak, 0, 3650);
            zenRuns = Clamp(zenRuns, 0, Max);
            selectedSector = Clamp(selectedSector, 0, Content.Sectors.Length - 1);
            equippedShip = Clamp(equippedShip, 0, 7);
            equippedTrail = Clamp(equippedTrail, 0, 7);
            equippedPlanet = Clamp(equippedPlanet, 0, 7);
            if (mutedTrack < -1 || mutedTrack > 7) mutedTrack = -1;

            if (bestByMode == null) bestByMode = new Dictionary<string, int>();
            var bestKeys = new List<string>(bestByMode.Keys);
            for (int i = 0; i < bestKeys.Count; i++)
                bestByMode[bestKeys[i]] = Clamp(bestByMode[bestKeys[i]], 0, Max);

            if (topByMode == null) topByMode = new Dictionary<string, List<int>>();
            var topKeys = new List<string>(topByMode.Keys);
            for (int i = 0; i < topKeys.Count; i++)
            {
                List<int> list = topByMode[topKeys[i]] ?? new List<int>();
                var cleaned = new List<int>();
                for (int j = 0; j < list.Count; j++) cleaned.Add(Clamp(list[j], 0, Max));
                cleaned.Sort((a, b) => b.CompareTo(a));
                while (cleaned.Count > 10) cleaned.RemoveAt(cleaned.Count - 1);
                topByMode[topKeys[i]] = cleaned;
            }

            if (history == null) history = new List<RunRecord>();
            for (int i = 0; i < history.Count; i++)
            {
                RunRecord r = history[i];
                if (r == null) { history.RemoveAt(i); i--; continue; }
                r.score = Clamp(r.score, 0, Max);
                r.level = Clamp(r.level, 1, 10000);
                r.orbs = Clamp(r.orbs, 0, Max);
                r.maxCombo = Clamp(r.maxCombo, 0, Max);
                r.nearMisses = Clamp(r.nearMisses, 0, Max);
                r.dodges = Clamp(r.dodges, 0, Max);
                r.bossKills = Clamp(r.bossKills, 0, Max);
                r.wormholes = Clamp(r.wormholes, 0, Max);
                r.rareOrbs = Clamp(r.rareOrbs, 0, Max);
                r.seconds = Clamp(r.seconds, 0, 100000);
                r.sector = Clamp(r.sector, 0, Content.Sectors.Length - 1);
                if (string.IsNullOrEmpty(r.mode)) r.mode = "classic";
            }
            while (history.Count > 25) history.RemoveAt(history.Count - 1);

            if (dailyProgress == null || dailyProgress.Count != 3)
                dailyProgress = new List<int> { 0, 0, 0 };
            if (dailyClaimed == null || dailyClaimed.Count != 3)
                dailyClaimed = new List<bool> { false, false, false };
            if (seasonClaimed == null || seasonClaimed.Count != 4)
                seasonClaimed = new List<bool> { false, false, false, false };
            for (int i = 0; i < dailyProgress.Count; i++)
                dailyProgress[i] = Clamp(dailyProgress[i], 0, Max);

            if (upgradeLevels == null) upgradeLevels = new Dictionary<string, int>();
            var upgradeKeys = new List<string>(upgradeLevels.Keys);
            for (int i = 0; i < upgradeKeys.Count; i++)
                upgradeLevels[upgradeKeys[i]] = Clamp(upgradeLevels[upgradeKeys[i]], 0, 1);

            if (inventory == null) inventory = new List<string>();
            string[] legacyIds = { "planet_nebula", "planet_graphite", "planet_solar", "planet_void" };
            string[] modernIds = { "planet_earth", "planet_mars", "planet_jupiter", "planet_neptune" };
            for (int i = 0; i < inventory.Count; i++)
            {
                for (int j = 0; j < legacyIds.Length; j++)
                    if (inventory[i] == legacyIds[j]) inventory[i] = modernIds[j];
            }
            for (int i = inventory.Count - 1; i >= 0; i--)
                if (FindShopItem(inventory[i]) == null) inventory.RemoveAt(i);
            string[] defaults = { "ship_cyan", "trail_spark", "planet_earth" };
            for (int i = 0; i < defaults.Length; i++)
                if (!inventory.Contains(defaults[i])) inventory.Add(defaults[i]);

            if (visitedSectors == null) visitedSectors = new List<int>();
            for (int i = visitedSectors.Count - 1; i >= 0; i--)
                if (visitedSectors[i] < 0 || visitedSectors[i] >= Content.Sectors.Length)
                    visitedSectors.RemoveAt(i);
            if (modesPlayed == null) modesPlayed = new List<string>();
            if (achievements == null) achievements = new List<string>();
            if (secrets == null) secrets = new List<string>();
            if (string.IsNullOrEmpty(selectedMode)) selectedMode = "classic";
            if (Content.ModeFromId(selectedMode) == ModeKind.Classic && selectedMode != "classic")
                selectedMode = "classic";
            selectedSector = Clamp(selectedSector, 0, Content.Sectors.Length - 1);

            if (settings == null) settings = new GameSettings();
            settings.music = ClampFloat(settings.music, 0f, 1f, 0.7f);
            settings.sfx = ClampFloat(settings.sfx, 0f, 1f, 0.9f);
            if (settings.audioProfile != "BALANCED" && settings.audioProfile != "NIGHT" && settings.audioProfile != "BASS")
                settings.audioProfile = "BALANCED";
            if (settings.hapticProfile != "CLASSIC" && settings.hapticProfile != "SOFT" && settings.hapticProfile != "STRONG")
                settings.hapticProfile = "CLASSIC";

            string season = SeasonIdKey;
            if (string.IsNullOrEmpty(season)) season = Content.SeasonIdString();
            if (season.Length > 0 && season[0] != 'S' && season[0] != 's')
            {
                int numeric;
                season = int.TryParse(season, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)
                    ? Content.SeasonKey(numeric)
                    : Content.SeasonIdString();
            }
            SeasonIdKey = season;
        }

        private static float ClampFloat(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value)) return fallback;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Null checks + sanitize + native day/season rollover.</summary>
        public void EnsureDefaults()
        {
            SyncLegacyToCanonical();
            if (string.IsNullOrEmpty(dailyDate)) dailyDate = Content.Today();
            if (string.IsNullOrEmpty(SeasonIdKey)) SeasonIdKey = Content.SeasonIdString();
            Sanitize();
            SyncLegacyFromCanonical();

            if (dailyDate != Content.Today())
            {
                dailyDate = Content.Today();
                dailyProgress = new List<int> { 0, 0, 0 };
                dailyClaimed = new List<bool> { false, false, false };
            }
            if (SeasonIdKey != Content.SeasonIdString())
            {
                SeasonIdKey = Content.SeasonIdString();
                seasonXp = 0;
                seasonClaimed = new List<bool> { false, false, false, false };
            }
        }
    }

    public static class SaveCodec
    {
        public static string Serialize(SaveData save)
        {
            if (save == null) save = new SaveData();
            // Encoding must not roll dates/seasons: UnitySaveStore.Load applies EnsureDefaults.
            save.SyncLegacyToCanonical();
            save.Sanitize();
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(save, settings);
        }

        public static SaveData Deserialize(string json)
        {
            SaveData save;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) save = new SaveData();
                else save = JsonConvert.DeserializeObject<SaveData>(json) ?? new SaveData();
            }
            catch (JsonException)
            {
                save = new SaveData();
            }
            catch (Exception)
            {
                save = new SaveData();
            }
            // Day/season rollover belongs to EnsureDefaults, which UnitySaveStore.Load calls.
            save.SyncLegacyToCanonical();
            save.Sanitize();
            return save;
        }
    }
}
