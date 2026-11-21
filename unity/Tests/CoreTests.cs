using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Orbit.Core;
using Xunit;

namespace Orbit.Core.Tests
{
    public class CoreTests
    {
        [Fact]
        public void Rng_IsDeterministic()
        {
            var a = new Rng(12345);
            var b = new Rng(12345);
            for (int i = 0; i < 32; i++)
                Assert.Equal(a.NextFloat(), b.NextFloat());
            Assert.Equal(a.Range(2f, 5f), b.Range(2f, 5f));
            Assert.Equal(a.NextInt(7), b.NextInt(7));
        }

        [Fact]
        public void Content_NativeIdsAndCounts()
        {
            string[] ids = { "classic", "zen", "sprint", "survival", "daily", "stack" };
            string[] titles = { "КЛАССИКА", "ДЗЕН", "СПРИНТ", "ВЫЖИВАНИЕ", "ДЕЙЛИ", "ШТАБЕЛЬ" };
            string[] shorts = { "КЛАСС", "ДЗЕН", "СПРИНТ", "ВЫЖИВ", "ДЕЙЛИ", "ШТАБ" };
            Assert.Equal(6, Content.Modes.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                Assert.Equal(ids[i], Content.Modes[i].Id);
                Assert.Equal(titles[i], Content.Modes[i].Title);
                Assert.Equal(shorts[i], Content.Modes[i].Short);
                Assert.Equal(i, (int)Content.ModeFromId(ids[i]));
            }
            Assert.Equal(90, Content.Modes[(int)ModeKind.Zen].Seconds);
            Assert.Equal(60, Content.Modes[(int)ModeKind.Sprint].Seconds);

            Assert.Equal(10, Content.Sectors.Length);
            Assert.Equal("ОРБИТА-01", Content.Sectors[0].Name);
            Assert.Equal("Учебный сектор", Content.Sectors[0].Subtitle);
            Assert.Equal(1, Content.Sectors[0].UnlockLevel);
            Assert.Equal(0.75f, Content.Sectors[0].Density);
            Assert.Equal("СЕКРЕТ", Content.Sectors[7].Name);
            Assert.Equal(99, Content.Sectors[7].UnlockLevel);
            Assert.Equal(1.30f, Content.Sectors[7].Density);
            Assert.Equal("СИНГУЛЯРНОСТЬ", Content.Sectors[9].Name);
            Assert.Equal(22, Content.Sectors[9].UnlockLevel);
            Assert.True(Content.Sector(99).Id == 9);
            Assert.True(Content.Sector(-5).Id == 0);
            Assert.True(Content.Sector(7).Has(HazardKind.Boss));

            Assert.Equal(8, Content.Perks.Length);
            Assert.Equal("overclock", Content.Perks[0].Key);
            Assert.Equal("chronos", Content.Perks[7].Key);
            Assert.Equal("+25% очков до конца забега", Content.Perks[0].Detail);
            Assert.Equal(8, Content.PerkIdsAvailable());

            Assert.Equal(19, Content.Achievements.Length);
            AchievementDef stack = FindAchievement("stack_40");
            Assert.NotNull(stack);
            Assert.Equal(260, stack.Reward);
            Assert.Equal(130, stack.Xp);
            Assert.Equal("АРХИТЕКТОР", stack.Title);

            Assert.Equal(25, Content.Shop.Length);
            Assert.Equal(6, Content.Secrets.Length);
            Assert.Equal(8, Content.MissionTemplates.Length);
            Assert.Equal(4, Content.SeasonTiers.Length);
            Assert.Equal(4, Content.SeasonModifiers.Length);
            Assert.Equal("СТАНДАРТ", Content.SeasonModifiers[0].Title);
            Assert.Equal(1.18f, Content.SeasonModifiers[1].Density);
            Assert.Equal(8, Content.PlanetSkins.Length);
            Assert.Equal(3, Content.BossNames.Length);
            Assert.Equal(100, Content.XpPerLevel);
            Assert.StartsWith("S", Content.SeasonIdString());
        }

        [Fact]
        public void Content_ObjectiveTargets()
        {
            Assert.Equal(18, Content.Objective(ModeKind.Zen, 0).Target);
            Assert.Equal(18 + 4, Content.Objective(ModeKind.Zen, 1).Target);
            Assert.Equal(18 + 9 * 4, Content.Objective(ModeKind.Zen, 99).Target); // sector clamps to 9
            Assert.Equal(1200, Content.Objective(ModeKind.Sprint, 0).Target);
            Assert.Equal(1200 + 2 * 300, Content.Objective(ModeKind.Sprint, 2).Target);
            Assert.Equal(10, Content.Objective(ModeKind.Stack, 0).Target);
            Assert.Equal(10 + 3 * 3, Content.Objective(ModeKind.Stack, 3).Target);
            Assert.Equal(600, Content.Objective(ModeKind.Classic, 0).Target);
            Assert.Equal(600 + 2 * 200, Content.Objective(ModeKind.Daily, 2).Target);
            Assert.Equal(ObjectiveField.Lines, Content.Objective(ModeKind.Stack, 0).Field);
            Assert.Equal("lines", Content.Objective(ModeKind.Stack, 0).NativeField);
        }

        [Fact]
        public void Content_FnvSeedAndMissions()
        {
            Assert.Equal(2116413359, Content.SeedFromString("2026-01-05"));
            Assert.Equal("2116413359", Content.SeedFor("2026-01-05"));

            List<Mission> missions = Content.Missions("2026-01-05");
            Assert.Equal(3, missions.Count);
            Assert.Equal("level", missions[0].Field);
            Assert.Equal(9, missions[0].Goal);
            Assert.Equal(132, missions[0].Reward);
            Assert.Equal("nearMisses", missions[1].Field);
            Assert.Equal(11, missions[1].Goal);
            Assert.Equal(100, missions[1].Reward);
            Assert.Equal("boss", missions[2].Field);
            Assert.Equal(1, missions[2].Goal);
            Assert.Equal(192, missions[2].Reward);

            List<string[]> legacy = Content.MissionsFor("2026-01-05");
            Assert.Equal(3, legacy.Count);
            Assert.Equal("level", legacy[0][0]);
            Assert.Equal("9", legacy[0][1]);
            Assert.Equal(Content.Missions("2026-01-05")[0].Title, legacy[0][2]);
        }

        [Fact]
        public void BlockArena_FirstBag_HasAllSevenPieces()
        {
            var arena = new BlockArena();
            arena.Reset(new Rng(7));
            Assert.Equal(4, arena.Next.Count); // native queue length
            var seen = new HashSet<int> { arena.Piece };
            foreach (int id in arena.Next) seen.Add(id);
            for (int i = 0; i < 5; i++)
            {
                arena.HardDrop();
                seen.Add(arena.Piece);
            }
            Assert.Equal(7, seen.Count);
        }

        [Fact]
        public void BlockArena_HardDrop_LocksPiece()
        {
            var arena = new BlockArena();
            arena.Reset(new Rng(11));
            BlockStep step = arena.HardDrop();
            Assert.True(step.Locked);
            Assert.True(step.Dropped > 0);
            int filled = 0;
            foreach (int cell in arena.Grid) if (cell != 0) filled++;
            Assert.Equal(4, filled);
            Assert.True(arena.CanHold);
        }

        [Fact]
        public void BlockArena_ClearsFullLine()
        {
            var arena = new BlockArena();
            arena.Reset(new Rng(3));
            for (int col = 0; col < BlockArena.Cols; col++)
                if (col < 3 || col > 6) arena.Grid[(BlockArena.Rows - 1) * BlockArena.Cols + col] = 1;
            arena.Piece = 0;
            arena.Rotation = 0;
            arena.X = 3;
            arena.Y = BlockArena.Rows - 2;
            BlockStep step = arena.HardDrop();
            Assert.Equal(1, step.Cleared);
            Assert.Equal(1, arena.Lines);
            Assert.Equal(1, arena.Level);
            Assert.True(arena.ClearRows[BlockArena.Rows - 1]);
            Assert.Equal(0.42f, arena.ClearFlash);
            // No rows above were filled, so the cleared bottom row becomes empty.
            for (int col = 0; col < BlockArena.Cols; col++)
                Assert.Equal(0, arena.Grid[(BlockArena.Rows - 1) * BlockArena.Cols + col]);
        }

        [Fact]
        public void BlockArena_Hold_OnlyOncePerPiece()
        {
            var arena = new BlockArena();
            arena.Reset(new Rng(5));
            int first = arena.Piece;
            arena.Hold();
            int held = arena.Held;
            Assert.True(held >= 0);
            Assert.False(arena.CanHold);
            arena.Hold();
            Assert.Equal(held, arena.Held);
            Assert.Equal(first, held);
        }

        [Fact]
        public void BlockArena_SharesEngineRngStream()
        {
            var a = new BlockArena();
            var b = new BlockArena();
            a.Reset(new Rng(42));
            b.Reset(new Rng(42));
            Assert.Equal(a.Piece, b.Piece);
            Assert.Equal(a.Next, b.Next);
            Assert.Equal(a.GhostY(), b.GhostY());
        }

        [Fact]
        public void GameEngine_StackObjective_AddsBonus()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Stack, 0, 42, 0, new GameSettings());
            engine.Stats.orbs = 10;
            Assert.True(engine.ObjectiveDone());
            RunRecord record = engine.FinishRun();
            Assert.True(record.completed);
            Assert.True(engine.Score >= 200);
        }

        [Fact]
        public void GameEngine_SprintTimer_Expires()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Sprint, 0, 5, 0, new GameSettings());
            Assert.Equal(60, engine.TimeLeft);
            for (int i = 0; i < 60 * 40 && engine.Status == GameStatus.Playing; i++)
            {
                if (engine.Draft != null)
                {
                    int choice = 0;
                    for (int c = 0; c < engine.Draft.Options.Length; c++)
                    {
                        int perk = engine.Draft.Options[c];
                        if (perk >= 0 && perk < Content.Perks.Length && Content.Perks[perk].Key != "chronos")
                        {
                            choice = c;
                            break;
                        }
                    }
                    engine.ChoosePerk(choice);
                }
                engine.Shield = true;
                engine.ShieldTime = 999f;
                engine.Update(1f / 30f);
            }
            Assert.Equal(GameStatus.GameOver, engine.Status);
            Assert.Equal(0, engine.TimeLeft);
        }

        [Fact]
        public void GameEngine_SprintHitCostsThreeSeconds()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Sprint, 0, 7, 0, new GameSettings());
            engine.Asteroids.Add(new OrbitThing
            {
                Angle = engine.PlayerAngle,
                Radius = 1f,
                Speed = 0f,
                Size = 0.2f,
                Life = 10f,
                Kind = 0,
                Active = true
            });
            engine.Update(0.01f);
            Assert.InRange(engine.TimeLeftSeconds, 56.95f, 57.05f);
            Assert.Contains(new GameEvent(GameEventKind.Crash), engine.Events);
        }

        [Fact]
        public void GameEngine_ZenHasNoAsteroidSpawnsAndNinetySeconds()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Zen, 0, 9, 0, new GameSettings());
            Assert.Equal(90, engine.TimeLeft);
            for (int i = 0; i < 600; i++)
            {
                if (engine.Draft != null) engine.ChoosePerk(0);
                engine.Update(1f / 30f);
            }
            Assert.Empty(engine.Asteroids);
        }

        [Fact]
        public void GameEngine_ToRecordIncludesMaxCombo()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Classic, 0, 11, 0, new GameSettings());
            engine.MaxCombo = 12;
            engine.Stats.maxCombo = 12;
            Assert.Equal(12, engine.FinishRun().maxCombo);
        }

        [Fact]
        public void GameEngine_BlockInput_IgnoresOrbitModes()
        {
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Classic, 0, 9, 0, new GameSettings());
            int before = engine.Blocks.X;
            engine.MoveBlock(-1);
            Assert.Equal(before, engine.Blocks.X);
        }

        [Fact]
        public void SaveCodec_RoundTrips()
        {
            var save = new SaveData();
            save.credits = 777;
            save.bestByMode["stack"] = 1234;
            save.topByMode["stack"] = new List<int> { 1234, 900 };
            save.settings.colorblind = true;
            string json = SaveCodec.Serialize(save);
            SaveData restored = SaveCodec.Deserialize(json);
            Assert.Equal(777, restored.credits);
            Assert.Equal(1234, restored.bestByMode["stack"]);
            Assert.Equal(2, restored.topByMode["stack"].Count);
            Assert.True(restored.settings.colorblind);
        }

        [Fact]
        public void SaveCodec_UsesNativeV4FieldNames()
        {
            var save = new SaveData();
            save.credits = 5000;
            save.xp = 250;
            save.SeasonIdKey = "S02";
            save.seasonXp = 400;
            save.dailyDate = "2026-01-05";
            save.credits = 5000;
            save.settings.music = 0.42f;
            save.settings.sfx = 0.66f;
            save.settings.largeText = true;
            save.settings.audioProfile = "NIGHT";
            save.upgradeLevels["upg_shield"] = 1;
            save.visitedSectors.Add(7);
            save.history.Add(new RunRecord
            {
                score = 1234, mode = "stack", sector = 1, level = 4, orbs = 10,
                nearMisses = 3, dodges = 5, bossKills = 1, wormholes = 2,
                rareOrbs = 1, seconds = 90, timestamp = 123456789L
            });

            string json = SaveCodec.Serialize(save);
            JObject o = JObject.Parse(json);
            Assert.Equal("S02", (string)o["seasonId"]);
            Assert.Equal(250, (int)o["xp"]);
            Assert.Equal("2026-01-05", (string)o["dailyDate"]);
            Assert.Equal(400, (int)o["seasonXp"]);
            Assert.NotNull(o["bestByMode"]);
            Assert.NotNull(o["topByMode"]);
            Assert.NotNull(o["inventory"]);
            Assert.NotNull(o["upgrades"]);
            Assert.NotNull(o["visitedSectors"]);
            Assert.NotNull(o["lastPlayedDay"]);
            Assert.NotNull(o["zenRuns"]);
            Assert.NotNull(o["mutedTrack"]);
            Assert.NotNull(o["tutorialSeen"]);
            Assert.Equal("NIGHT", (string)o["settings"]["audioProfile"]);
            Assert.True((bool)o["settings"]["largeText"]);
            Assert.InRange((float)o["settings"]["music"], 0.419f, 0.421f);
            Assert.InRange((float)o["settings"]["sfx"], 0.659f, 0.661f);

            JObject history = (JObject)o["history"][0];
            Assert.Equal(3, (int)history["nearMisses"]);
            Assert.Equal(5, (int)history["dodges"]);
            Assert.Equal(1, (int)history["bossKills"]);
            Assert.Equal(2, (int)history["wormholes"]);
            Assert.Equal(1, (int)history["rareOrbs"]);
            Assert.Equal(90, (int)history["seconds"]);
            Assert.Equal(123456789L, (long)history["timestamp"]);
            Assert.Null(history["completed"]);
            Assert.Null(o["ownedShips"]);
            Assert.Null(o["playerLevel"]);

            SaveData restored = SaveCodec.Deserialize(json);
            Assert.Equal("S02", restored.SeasonIdKey);
            Assert.Equal(2, restored.seasonId);
            Assert.Equal(3, restored.history[0].nearMisses);
            Assert.Equal(123456789L, restored.history[0].timestamp);
            Assert.Equal(3, restored.playerLevel); // 1 + xp/100
            Assert.Equal(1, restored.upgradeLevels["upg_shield"]);
            Assert.Contains("ship_cyan", restored.inventory);
        }

        [Fact]
        public void SaveCodec_LegacyFieldsMapOntoInventory()
        {
            var save = new SaveData();
            save.ownedShips = new List<string> { "1", "2" };
            Assert.Contains("ship_rose", save.inventory);
            Assert.Contains("ship_lime", save.inventory);
            Assert.Contains("ship_cyan", save.inventory);
            Assert.Contains("2", save.ownedShips);
            save.upgrades = new List<int> { 1, 0, 1, 0, 0 };
            Assert.Equal(1, save.upgradeLevels["upg_shield"]);
            Assert.Equal(1, save.upgradeLevels["upg_magnet"]);
            Assert.Equal(0, save.upgradeLevels["upg_time"]);
            Assert.Equal(5, save.upgrades.Count);
        }

        [Fact]
        public void SaveCodec_StableLegacyListsSurvivePersistence()
        {
            var save = new SaveData();
            List<string> ships = save.ownedShips;
            ships.Add("1");
            List<int> upgrades = save.upgrades;
            upgrades[0] = 1;

            SaveData restored = SaveCodec.Deserialize(SaveCodec.Serialize(save));

            Assert.Contains("ship_rose", restored.inventory);
            Assert.Equal(1, restored.upgradeLevels["upg_shield"]);
        }

        [Fact]
        public void SaveCodec_MigratesLegacyPlanetIds()
        {
            var save = new SaveData();
            save.inventory.Add("planet_void");
            save.Sanitize();
            Assert.Contains("planet_neptune", save.inventory);
            Assert.DoesNotContain("planet_void", save.inventory);
        }

        [Fact]
        public void SaveCodec_SanitizesBadValues()
        {
            string json = "{\"credits\":-5,\"xp\":999999999,\"seasonId\":7," +
                          "\"equippedShip\":99,\"selectedSector\":42," +
                          "\"dailyProgress\":[1],\"settings\":{\"audioProfile\":\"WRONG\"," +
                          "\"music\":4.0,\"sfx\":-1.0}}";
            SaveData save = SaveCodec.Deserialize(json);
            Assert.Equal(0, save.credits);
            Assert.Equal(SaveData.Max, save.xp);
            Assert.Equal("S07", save.SeasonIdKey);
            Assert.Equal(7, save.seasonId);
            Assert.Equal(7, save.equippedShip);
            Assert.Equal(9, save.selectedSector);
            Assert.Equal(3, save.dailyProgress.Count);
            Assert.Equal("BALANCED", save.settings.audioProfile);
            Assert.Equal(1f, save.settings.music);
            Assert.Equal(0f, save.settings.sfx);
        }

        [Fact]
        public void Progression_UpdatesRecordsAndRewards()
        {
            var save = new SaveData();
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Stack, 0, 1, 0, new GameSettings());
            var record = new RunRecord { mode = "stack", score = 1000, orbs = 10, completed = true };
            RewardSummary reward = Progression.Apply(save, record, engine);
            Assert.Equal(1000, save.bestByMode["stack"]);
            Assert.Single(save.topByMode["stack"]);
            Assert.True(reward.credits > 100);
            Assert.True(reward.xp > 0);
            Assert.Contains("first_run", save.achievements);
            Assert.True(reward.newBest);
            Assert.Equal(0, reward.creditsBefore);
        }

        [Fact]
        public void Progression_Stack40_PaysNativeReward()
        {
            var save = new SaveData();
            save.achievements.Add("first_run"); // isolate the stack_40 reward
            save.dailyClaimed = new List<bool> { true, true, true };
            var engine = new GameEngineCore();
            engine.StartRun(ModeKind.Stack, 0, 1, 0, new GameSettings());
            engine.Stats.orbs = 40;
            var record = new RunRecord { mode = "stack", orbs = 40, completed = true };

            RewardSummary reward = Progression.Apply(save, record, engine);

            Assert.Contains("stack_40", save.achievements);
            // 40 orbs + 120 objective + 260 achievement = 420 credits.
            // 30 objective + 130 achievement = 160 xp.
            Assert.Equal(420, reward.credits);
            Assert.Equal(160, reward.xp);
            Assert.Equal(reward.levels, reward.levelUps);
        }

        private static AchievementDef FindAchievement(string id)
        {
            foreach (AchievementDef a in Content.Achievements)
                if (a.Id == id) return a;
            return null;
        }
    }
}
