using System;
using System.Collections.Generic;
using System.IO;
using Orbit.Galaxy;
using Xunit;

namespace Orbit.Galaxy.Core.Tests
{
    public class CoreTests
    {
        private const float Epsilon = 0.01f;

        private static Ship NewShip(int id)
        {
            return new Ship(id, "S" + id, false, Vec2.Zero);
        }

        private static void Close(float expected, float actual, float tolerance)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                "Expected " + expected.ToString() + " but got " + actual.ToString() + ".");
        }

        private static bool CloseVec(Vec2 a, Vec2 b, float tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
        }

        private static int CountObjects(Arena arena, ObjectKind kind)
        {
            return CountObjects(arena, delegate(WorldObject obj) { return obj != null && obj.Kind == kind; });
        }

        private static int CountObjects(Arena arena, Func<WorldObject, bool> predicate)
        {
            int count = 0;
            if (arena == null || arena.Objects == null) return 0;
            for (int i = 0; i < arena.Objects.Count; i++)
            {
                if (predicate(arena.Objects[i])) count++;
            }
            return count;
        }

        private static int CountSourceObjects(Arena arena, string source)
        {
            return CountObjects(arena, delegate(WorldObject obj)
            {
                HazardZone hazard = obj as HazardZone;
                if (hazard != null) return string.Equals(hazard.SourceEvent, source, StringComparison.Ordinal);
                Anomaly anomaly = obj as Anomaly;
                return anomaly != null && string.Equals(anomaly.SourceEvent, source, StringComparison.Ordinal);
            });
        }

        private static WorldObject FirstObject(Arena arena, ObjectKind kind)
        {
            for (int i = 0; i < arena.Objects.Count; i++)
            {
                if (arena.Objects[i] != null && arena.Objects[i].Kind == kind) return arena.Objects[i];
            }
            return null;
        }

        private static bool HasEvent(List<SimEvent> events, SimEventKind kind)
        {
            if (events == null) return false;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] != null && events[i].Kind == kind) return true;
            }
            return false;
        }

        private static bool AllDistinctIds(List<EvolutionDef> defs)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                for (int j = i + 1; j < defs.Count; j++)
                {
                    if (string.Equals(defs[i].Id, defs[j].Id, StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            return true;
        }

        // 1. RNG and vector foundation.

        [Fact]
        public void Rng_SameSeed_ProducesSameSequence()
        {
            Rng a = new Rng(12345u);
            Rng b = new Rng(12345u);
            for (int i = 0; i < 32; i++) Assert.Equal(a.NextUInt(), b.NextUInt());

            Rng c = new Rng(777u);
            Rng d = new Rng(777u);
            for (int i = 0; i < 16; i++) Assert.Equal(c.NextFloat(), d.NextFloat());

            Rng moved = new Rng(12345u);
            Assert.Equal(12345u, moved.State);
            moved.NextUInt();
            Assert.NotEqual(12345u, moved.State);
            Assert.NotEqual(new Rng(12345u).State, new Rng(54321u).State);
        }

        [Fact]
        public void Rng_PickForkHashAndBounds_AreDeterministic()
        {
            Rng r = new Rng(7u);
            for (int i = 0; i < 128; i++)
            {
                int v = r.NextInt(-2, 3);
                Assert.True(v >= -2 && v < 3, "NextInt left the requested range.");
                float f = r.NextFloat();
                Assert.True(f >= 0f && f < 1f, "NextFloat left [0,1).");
            }
            Assert.Equal(5, new Rng(3u).NextInt(5, 5));

            Rng forkA = new Rng(99u).Fork();
            Rng forkB = new Rng(99u).Fork();
            Assert.Equal(forkA.State, forkB.State);
            Assert.Equal(Rng.Hash("orbita"), Rng.Hash("orbita"));
            Assert.NotEqual(Rng.Hash("orbita"), Rng.Hash("galaxy"));

            string[] names = { "a", "b", "c" };
            Assert.Equal(new Rng(11u).Pick(names), new Rng(11u).Pick(names));
        }

        [Fact]
        public void Vec2_Operations_AreCorrect()
        {
            Vec2 v = new Vec2(3f, 4f);
            Close(5f, v.Magnitude, Epsilon);
            Close(25f, v.SqrMagnitude, Epsilon);
            Close(0.6f, v.Normalized.X, Epsilon);
            Close(0.8f, v.Normalized.Y, Epsilon);
            Close(-4f, v.Perpendicular.X, Epsilon);
            Close(3f, v.Perpendicular.Y, Epsilon);

            Close(3f, Vec2.Dot(v, Vec2.Right), Epsilon);
            Close(1f, Vec2.Cross(Vec2.Right, Vec2.Up), Epsilon);
            Close(5f, Vec2.Distance(v, Vec2.Zero), Epsilon);
            Close(25f, Vec2.DistanceSqr(v, Vec2.Zero), Epsilon);
            Close(2.5f, Vec2.Lerp(Vec2.Zero, new Vec2(10f, 0f), 0.25f).X, Epsilon);
            Close(1f, Vec2.FromAngle(0f).X, Epsilon);
            Assert.True(v.Equals(new Vec2(3f, 4f)));
            Assert.True(Vec2.Zero.Normalized.Equals(Vec2.Zero));
        }

        // 2. Content tables.

        [Fact]
        public void ContentDatabase_AllTablesHaveExpectedCounts()
        {
            Assert.Equal(8, ContentDatabase.ShipVisuals.Length);
            Assert.Equal(12, ContentDatabase.Evolutions.Length);
            Assert.Equal(7, ContentDatabase.Abilities.Length);
            Assert.Equal(8, ContentDatabase.Weapons.Length);
            Assert.Equal(7, ContentDatabase.BotProfiles.Length);
            Assert.Equal(40, ContentDatabase.BotNames.Length);
            Assert.Equal(5, ContentDatabase.Bosses.Length);
            Assert.Equal(6, ContentDatabase.Modes.Length);
            Assert.Equal(24, ContentDatabase.Cosmetics.Length);
            Assert.Equal(19, ContentDatabase.Achievements.Length);
            Assert.Equal(10, ContentDatabase.Quests.Length);
            Assert.Equal(6, ContentDatabase.Research.Length);

            Assert.Equal(4, CountEvolutionTier(1));
            Assert.Equal(4, CountEvolutionTier(2));
            Assert.Equal(4, CountEvolutionTier(3));
        }

        private static int CountEvolutionTier(int tier)
        {
            int count = 0;
            for (int i = 0; i < ContentDatabase.Evolutions.Length; i++)
                if (ContentDatabase.Evolutions[i].Tier == tier) count++;
            return count;
        }

        [Fact]
        public void ContentDatabase_FindersAndQuestSelection_AreDeterministic()
        {
            Assert.Equal("titan_1", ContentDatabase.FindEvolution("TITAN_1").Id);
            Assert.NotNull(ContentDatabase.FindEvolution(EvolutionBranch.Phantom, 2));
            Assert.Null(ContentDatabase.FindEvolution(EvolutionBranch.None, 3));
            Assert.Equal(AbilityKind.Nova, ContentDatabase.FindAbility(AbilityKind.Nova).Kind);
            Assert.Equal(WeaponKind.Orbital, ContentDatabase.FindWeapon(WeaponKind.Orbital).Kind);
            Assert.Equal(BotPersonality.Farmer,
                ContentDatabase.FindBotProfile(BotPersonality.Farmer).Personality);
            Assert.Equal("boss_devourer", ContentDatabase.FindBoss("boss_devourer").Id);
            Assert.Equal(GameModeKind.Chaos, ContentDatabase.FindMode(GameModeKind.Chaos).Kind);
            Assert.Equal("ship_nova", ContentDatabase.FindShipVisual("ship_nova").Id);

            DateTime day = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);
            List<QuestDef> dailyA = ContentDatabase.DailyQuests(day);
            List<QuestDef> dailyB = ContentDatabase.DailyQuests(day);
            Assert.Equal(3, dailyA.Count);
            for (int i = 0; i < dailyA.Count; i++) Assert.Equal(dailyA[i].Id, dailyB[i].Id);
            Assert.Equal(4, ContentDatabase.WeeklyQuests(day).Count);
        }

        // 3-4. Arena, spatial index, shrink, border damage.

        [Fact]
        public void Arena_SameSeed_GeneratesIdenticalWorld()
        {
            Arena a = new Arena(GameModeKind.Classic, 2026, 1f);
            Arena b = new Arena(GameModeKind.Classic, 2026, 1f);
            a.Initialize();
            b.Initialize();

            Assert.Equal(a.Objects.Count, b.Objects.Count);
            Assert.Equal(a.Objects.Count, a.Index.Count);
            Assert.Equal(5, a.SecretAreas.Count);
            Assert.Equal(GameConfig.ArenaRadius, a.Radius);
            Assert.Equal(GameConfig.SafeRadiusStart, a.SafeRadius);
            Assert.False(a.Shrinks);

            WorldObject energyA = FirstObject(a, ObjectKind.Energy);
            WorldObject energyB = FirstObject(b, ObjectKind.Energy);
            Assert.NotNull(energyA);
            Assert.NotNull(energyB);
            Assert.True(CloseVec(energyA.Position, energyB.Position, Epsilon));
            Assert.Equal(energyA.Value, energyB.Value);

            Assert.True(CountObjects(a, ObjectKind.Energy) >= GameConfig.EnergyNodes / 2);
            Assert.True(CountObjects(a, ObjectKind.Crystal) >= GameConfig.CrystalNodes / 2);
            Assert.True(CountObjects(a, ObjectKind.Asteroid) >= GameConfig.Asteroids / 2);
            Assert.True(a.Hazards.Count > 0);
            Assert.True(a.Anomalies.Count > 0);

            WorldObject nearest = a.FindNearestResource(energyA.Position, 50f, ResourceKind.Energy);
            Assert.NotNull(nearest);
            Assert.Equal(ObjectKind.Energy, nearest.Kind);
        }

        [Fact]
        public void Arena_SpatialIndexAndRespawn_Work()
        {
            Arena arena = new Arena(GameModeKind.Classic, 17, 1f);
            arena.Initialize();
            EnergyNode node = FirstObject(arena, ObjectKind.Energy) as EnergyNode;
            Assert.NotNull(node);

            int energyBefore = CountObjects(arena, ObjectKind.Energy);
            int indexBefore = arena.Index.Count;
            Assert.Contains(node, arena.Query(node.Position, 1f));

            arena.Remove(node);
            Assert.False(node.Alive);
            Assert.Equal(energyBefore - 1, CountObjects(arena, ObjectKind.Energy));
            Assert.Equal(indexBefore - 1, arena.Index.Count);
            Assert.DoesNotContain(node, arena.Query(node.Position, 1f));

            arena.Update(25f, new SimLog(), 0f);
            Assert.Equal(energyBefore, CountObjects(arena, ObjectKind.Energy));
            Assert.Equal(arena.Objects.Count, arena.Index.Count);
        }

        [Fact]
        public void Arena_ShrinkAndBorderDamage_Work()
        {
            Arena arena = new Arena(GameModeKind.Survival, 99, 1f);
            arena.Initialize();
            Assert.True(arena.Shrinks);

            float start = arena.SafeRadius;
            arena.Shrink(100f);
            Close(start - 100f, arena.SafeRadius, Epsilon);
            arena.Update(1f, new SimLog(), 1f);
            Assert.True(arena.SafeRadius < start - 100f);

            Ship ship = NewShip(1);
            ship.Position = new Vec2(arena.Radius - 10f, 0f);
            float hpBefore = ship.Hp;
            SimLog log = new SimLog();
            arena.DamageBorder(ship, 1f, log, 2f);
            Assert.True(ship.Hp < hpBefore);

            List<SimEvent> events = log.Drain();
            Assert.True(HasEvent(events, SimEventKind.Warning));
            Assert.True(HasEvent(events, SimEventKind.Damage));
        }

        // 5. Growth.

        [Fact]
        public void Growth_ResourceConversionsAndPickups_MatchConfig()
        {
            Close(GameConfig.MassGainEnergy, GrowthSystem.MassFromResource(ResourceKind.Energy), Epsilon);
            Close(GameConfig.MassGainCrystal, GrowthSystem.MassFromResource(ResourceKind.Crystal), Epsilon);
            Close(GameConfig.MassGainDarkMatter, GrowthSystem.MassFromResource(ResourceKind.DarkMatter), Epsilon);
            Close(GameConfig.MassGainArtifact, GrowthSystem.MassFromResource(ResourceKind.Artifact), Epsilon);
            Close(3f, GrowthSystem.XpFromResource(ResourceKind.Energy), Epsilon);
            Close(80f, GrowthSystem.XpFromResource(ResourceKind.Artifact), Epsilon);

            Ship energyShip = NewShip(1);
            SimLog log = new SimLog();
            GrowthSystem.ApplyPickup(energyShip, new EnergyNode(10, Vec2.Zero, 5), log, 1f, null);
            Close(GameConfig.BaseMass + GameConfig.MassGainEnergy * 5f, energyShip.Mass, Epsilon);
            Close(15f, energyShip.Xp, Epsilon);
            Close(5f, energyShip.EnergyCollected, Epsilon);
            Assert.True(HasEvent(log.Drain(), SimEventKind.Pickup));

            Ship crystalShip = NewShip(2);
            log = new SimLog();
            GrowthSystem.ApplyPickup(crystalShip, new CrystalNode(11, Vec2.Zero, 8), log, 1f, null);
            Assert.Equal(8, crystalShip.Crystals);
            Assert.True(crystalShip.Level >= 2f);
            Assert.True(HasEvent(log.Drain(), SimEventKind.LevelUp));

            Ship artifactShip = NewShip(3);
            GrowthSystem.ApplyPickup(artifactShip, new ArtifactNode(12, Vec2.Zero, "shield_pulse", 1f),
                new SimLog(), 1f, null);
            Assert.Equal(1, artifactShip.Artifacts);
            Assert.True(artifactShip.MaxShield > 0f);
            Assert.True(artifactShip.Lifesteal > 0f);
        }

        [Fact]
        public void Growth_Update_RegeneratesEnergyAndRespawns()
        {
            Ship ship = NewShip(1);
            ship.Energy = 0f;
            GrowthSystem.Update(ship, 1f, new SimLog(), 1f);
            Close(GameConfig.EnergyRegenPerSecond, ship.Energy, Epsilon);

            ship.State = ShipState.Respawning;
            ship.RespawnTimer = 0.5f;
            ship.Hp = 0f;
            GrowthSystem.Update(ship, 0.6f, new SimLog(), 2f);
            Assert.Equal(ShipState.Alive, ship.State);
            Close(ship.MaxHp, ship.Hp, Epsilon);
            Close(GameConfig.RespawnInvulnerability, ship.Invulnerability, Epsilon);
        }

        // 6. Absorption.

        [Fact]
        public void Absorption_RatioAndContactDamage_FollowMass()
        {
            Ship big = NewShip(1);
            big.Mass = 30f;
            big.Damage = 20f;
            Ship small = NewShip(2);
            small.Mass = 10f;
            small.Damage = 10f;

            Close(3f, AbsorptionSystem.MassRatio(big, small), Epsilon);
            Assert.True(AbsorptionSystem.ContactDamage(big, small) >
                        AbsorptionSystem.ContactDamage(small, big));

            Assert.True(AbsorptionSystem.CanAbsorb(big, small));
            small.Position = new Vec2(200f, 0f);
            Assert.False(AbsorptionSystem.CanAbsorb(big, small));
            small.Position = Vec2.Zero;
            small.Invulnerability = 1f;
            Assert.False(AbsorptionSystem.CanAbsorb(big, small));
            small.Invulnerability = 0f;
            small.IsBoss = true;
            Assert.False(AbsorptionSystem.CanAbsorb(big, small));
        }

        [Fact]
        public void Absorption_TryAbsorb_TransfersMassAndKillsTarget()
        {
            Ship big = NewShip(1);
            big.Mass = 30f;
            Ship small = NewShip(2);
            small.Mass = 10f;
            SimLog log = new SimLog();

            Assert.True(AbsorptionSystem.TryAbsorb(big, small, log, 1f));
            Assert.True(big.Mass > 30f);
            Assert.True(small.Mass < 10f);
            Assert.Equal(ShipState.Respawning, small.State);
            Assert.Equal(1, big.Kills);
            Assert.True(big.Score > 0f);
            Assert.True(HasEvent(log.Drain(), SimEventKind.Absorb));
        }

        // 7. Combat.

        [Fact]
        public void Combat_FireSpawnsAndConsumesProjectiles()
        {
            CombatSystem combat = new CombatSystem(new Rng(5u));
            Ship shooter = NewShip(1);
            shooter.Facing = Vec2.Right;
            Ship target = NewShip(2);
            target.Position = new Vec2(200f, 0f);
            List<Ship> ships = new List<Ship> { shooter, target };
            SimLog log = new SimLog();

            combat.Fire(shooter, Vec2.Right, log, 1f);
            Assert.Single(combat.Projectiles);
            Assert.Equal(shooter.Id, combat.Projectiles[0].OwnerId);
            Assert.True(shooter.FireTimer > 0f);

            combat.Fire(shooter, Vec2.Right, log, 1f);
            Assert.Single(combat.Projectiles);

            combat.Update(0.3f, ships, null, log, 1.1f, null);
            Assert.True(target.Hp < target.MaxHp);
            Assert.Empty(combat.Projectiles);
        }

        [Fact]
        public void Combat_ShieldsInvulnerabilityAndKillStreaks_AreCorrect()
        {
            Ship target = NewShip(2);
            target.MaxShield = 50f;
            target.Shield = 50f;
            SimLog log = new SimLog();

            CombatSystem.ApplyDamage(target, 50f, null, log, 1f, false);
            Close(9f, target.Shield, Epsilon);
            Close(91f, target.Hp, Epsilon);

            target.Invulnerability = 1f;
            CombatSystem.ApplyDamage(target, 100f, null, log, 1.1f, false);
            Close(91f, target.Hp, Epsilon);
            Assert.True(HasEvent(log.Drain(), SimEventKind.Damage));

            Ship killer = NewShip(1);
            Ship victim = NewShip(3);
            victim.Rank = 1;
            victim.LastKillTime = -100f;
            CombatSystem.ApplyDamage(victim, victim.Hp + 100f, killer, log, 1f, false);
            Assert.Equal(1, killer.Kills);
            Assert.Equal(1, killer.Streak);
            Assert.Equal(ShipState.Respawning, victim.State);

            victim.State = ShipState.Alive;
            victim.Hp = 100f;
            victim.Shield = 0f;
            victim.Invulnerability = 0f;
            CombatSystem.ApplyDamage(victim, victim.Hp + 100f, killer, log, 2f, false);
            Assert.Equal(2, killer.Kills);
            Assert.Equal(2, killer.Streak);
            Assert.Equal(2, killer.BestStreak);

            List<SimEvent> events = log.Drain();
            Assert.True(HasEvent(events, SimEventKind.Kill));
            Assert.True(HasEvent(events, SimEventKind.LeaderKilled));
        }

        // 8. All seven abilities.

        [Fact]
        public void Ability_Dash_Works()
        {
            Ship ship = NewShip(1);
            ship.Facing = Vec2.Right;
            SimLog log = new SimLog();
            Assert.True(AbilitySystem.CanActivate(ship, AbilityKind.Dash));

            Assert.True(AbilitySystem.Activate(ship, AbilityKind.Dash, log, 1f));
            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.Dash);
            Close(def.Power, ship.Position.X, Epsilon);
            Assert.True(ship.Velocity.X > 0f);
            Assert.True(ship.Invulnerability > 0f);
            Assert.True(ship.Cooldowns[(int)AbilityKind.Dash] > 0f);
            Assert.True(ship.EffectTimers[(int)AbilityKind.Dash] > 0f);
            Assert.True(HasEvent(log.Drain(), SimEventKind.Ability));
        }

        [Fact]
        public void Ability_Shield_Works()
        {
            Ship ship = NewShip(1);
            SimLog log = new SimLog();
            Assert.True(AbilitySystem.Activate(ship, AbilityKind.Shield, log, 1f));

            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.Shield);
            Close(def.Power, ship.MaxShield, Epsilon);
            Close(ship.MaxShield, ship.Shield, Epsilon);
            Assert.True(AbilitySystem.IsActive(ship, AbilityKind.Shield));
            Assert.False(AbilitySystem.CanActivate(ship, AbilityKind.Shield));
            Assert.True(HasEvent(log.Drain(), SimEventKind.Ability));

            List<Ship> ships = new List<Ship> { ship };
            AbilitySystem.Update(def.Duration + 0.1f, ships, null, null, new SimLog(), 2f);
            Assert.False(AbilitySystem.IsActive(ship, AbilityKind.Shield));
            Close(0f, ship.MaxShield, Epsilon);
        }

        [Fact]
        public void Ability_Nova_Works()
        {
            Ship caster = NewShip(1);
            Ship enemy = NewShip(2);
            enemy.Position = new Vec2(100f, 0f);
            List<Ship> ships = new List<Ship> { caster, enemy };
            SimLog log = new SimLog();

            Assert.True(AbilitySystem.Activate(caster, AbilityKind.Nova, log, 1f));
            AbilitySystem.Update(GameConfig.SimulationStep, ships, null, null, log, 1.1f);

            Assert.True(enemy.Hp < enemy.MaxHp);
            Assert.True(enemy.Position.X > 100f);
            Assert.True(HasEvent(log.Drain(), SimEventKind.Ability));
        }

        [Fact]
        public void Ability_Magnet_Works()
        {
            Ship ship = NewShip(1);
            Arena arena = new Arena(GameModeKind.Classic, 1, 1f);
            EnergyNode node = new EnergyNode(50, new Vec2(100f, 0f), 5);
            arena.Add(node);
            SimLog log = new SimLog();

            Assert.True(AbilitySystem.Activate(ship, AbilityKind.Magnet, log, 1f));
            AbilitySystem.Update(0.1f, new List<Ship> { ship }, arena, null, log, 1.1f);
            Assert.True(node.Position.X < 100f);
            Assert.True(AbilitySystem.IsActive(ship, AbilityKind.Magnet));
            List<WorldObject> found = new List<WorldObject>();
            arena.Index.Query(node.Position, 10f, found);
            Assert.Contains(node, found);
        }

        [Fact]
        public void Ability_Overdrive_Works()
        {
            Ship ship = NewShip(1);
            float speedBefore = ship.Speed;
            float intervalBefore = ship.FireInterval;
            SimLog log = new SimLog();

            Assert.True(AbilitySystem.Activate(ship, AbilityKind.Overdrive, log, 1f));
            Assert.True(ship.Speed > speedBefore);
            Assert.True(ship.FireInterval < intervalBefore);

            AbilityDef def = ContentDatabase.FindAbility(AbilityKind.Overdrive);
            AbilitySystem.Update(def.Duration + 0.1f, new List<Ship> { ship }, null, null, new SimLog(), 2f);
            Close(speedBefore, ship.Speed, Epsilon);
            Close(intervalBefore, ship.FireInterval, Epsilon);
        }

        [Fact]
        public void Ability_BlackHole_Works()
        {
            Ship ship = NewShip(1);
            Ship enemy = NewShip(2);
            enemy.Position = new Vec2(100f, 0f);
            SimLog log = new SimLog();

            Assert.True(AbilitySystem.Activate(ship, AbilityKind.BlackHole, log, 1f));
            AbilitySystem.Update(0.1f, new List<Ship> { ship, enemy }, null, null, log, 1.1f);
            Assert.True(enemy.Position.X < 100f);
            Assert.True(AbilitySystem.IsActive(ship, AbilityKind.BlackHole));
        }

        [Fact]
        public void Ability_Phase_Works()
        {
            Ship ship = NewShip(1);
            float hpBefore = ship.Hp;
            SimLog log = new SimLog();

            Assert.True(AbilitySystem.Activate(ship, AbilityKind.Phase, log, 1f));
            Assert.True(AbilitySystem.IsActive(ship, AbilityKind.Phase));
            Assert.True(ship.Invulnerability > 0f);
            Assert.True(ship.DamageReduction > 0.8f);

            CombatSystem.ApplyDamage(ship, 100f, null, log, 1.1f, false);
            Close(hpBefore, ship.Hp, Epsilon);
        }

        // 9. Evolution.

        [Fact]
        public void Evolution_ThresholdsAndOffers_AreDeterministic()
        {
            Ship ship = NewShip(1);
            Assert.Equal(3, EvolutionSystem.NextEvolutionLevel(ship));

            List<EvolutionDef> firstA = EvolutionSystem.Offer(ship, new Rng(44u));
            List<EvolutionDef> firstB = EvolutionSystem.Offer(ship, new Rng(44u));
            Assert.Equal(3, firstA.Count);
            Assert.True(AllDistinctIds(firstA));
            for (int i = 0; i < firstA.Count; i++) Assert.Equal(firstA[i].Id, firstB[i].Id);

            EvolutionSystem.Choose(ship, firstA[0], new SimLog(), 1f, null);
            Assert.Equal(1, ship.EvolutionTier);
            Assert.Equal(7, EvolutionSystem.NextEvolutionLevel(ship));

            List<EvolutionDef> second = EvolutionSystem.Offer(ship, new Rng(45u));
            Assert.Equal(3, second.Count);
            Assert.True(AllDistinctIds(second));
            EvolutionSystem.Choose(ship, second[0], new SimLog(), 2f, null);
            Assert.Equal(2, ship.EvolutionTier);
            Assert.Equal(12, EvolutionSystem.NextEvolutionLevel(ship));

            List<EvolutionDef> third = EvolutionSystem.Offer(ship, new Rng(46u));
            Assert.Equal(3, third.Count);
            EvolutionSystem.Choose(ship, third[0], new SimLog(), 3f, null);
            Assert.Equal(3, ship.EvolutionTier);
            Assert.Equal(int.MaxValue, EvolutionSystem.NextEvolutionLevel(ship));
            Assert.Empty(EvolutionSystem.Offer(ship, new Rng(47u)));
        }

        [Fact]
        public void Evolution_ChooseAndApply_ChangePublicStats()
        {
            Ship ship = NewShip(1);
            EvolutionDef titan = ContentDatabase.FindEvolution("titan_1");
            Assert.NotNull(titan);
            float massBefore = ship.Mass;
            float hpBefore = ship.MaxHp;
            SimLog log = new SimLog();

            Assert.True(EvolutionSystem.IsAvailable(ship, titan));
            EvolutionSystem.Choose(ship, titan, log, 1f, null);
            Assert.True(ship.Mass > massBefore);
            Assert.True(ship.MaxHp > hpBefore);
            Assert.Equal(EvolutionBranch.Titan, ship.Branch);
            Assert.Equal(1, ship.EvolutionTier);
            Assert.Equal("titan_1", ship.EvolutionId);
            Assert.Equal(WeaponKind.Plasma, ship.Weapon);
            Assert.False(EvolutionSystem.IsAvailable(ship, titan));
            Assert.True(HasEvent(log.Drain(), SimEventKind.Evolution));

            Ship swarm = NewShip(2);
            EvolutionDef capstone = ContentDatabase.FindEvolution("swarm_3");
            EvolutionSystem.Apply(swarm, capstone, null);
            Assert.Equal(4, swarm.Drones);
            Assert.Equal(3, swarm.ExtraProjectiles);
            Assert.True(swarm.Lifesteal > 0f);
            Assert.Equal(WeaponKind.Ring, swarm.Weapon);
            Assert.Equal(HullShape.Hex, swarm.Hull);
            Assert.Equal(3, swarm.EvolutionTier);
        }

        [Fact]
        public void MatchSimulation_EvolutionChoice_FreezesArenaUntilResolved()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 4242, Difficulty.Normal, null);
            match.PendingEvolutionChoices.Add(ContentDatabase.Evolutions[0]);
            float timeBefore = match.Time;
            Vec2 positionBefore = match.Player.Position;

            for (int i = 0; i < 12; i++) match.Update(GameConfig.SimulationStep);
            Close(timeBefore, match.Time, 0f);
            Assert.True(CloseVec(positionBefore, match.Player.Position, 0f));
            Assert.True(match.IsEvolutionPaused);

            match.PlayerChooseEvolution(null);
            Assert.Empty(match.PendingEvolutionChoices);
            Assert.False(match.IsEvolutionPaused);
            match.Update(GameConfig.SimulationStep);
            Assert.True(match.Time > timeBefore);
        }

        // 10. Bots.

        [Fact]
        public void Bots_DecisionScores_ReflectThreatPreyAndHazard()
        {
            Ship self = NewShip(1);
            self.Profile = ContentDatabase.FindBotProfile(BotPersonality.Aggressive);
            BotPerception p = new BotPerception();

            Ship threat = NewShip(2);
            threat.Mass = 60f;
            p.NearestThreat = threat;
            p.ThreatScore = 8f;
            Assert.True(BotDecisionSystem.ScoreGoal(BotGoal.Flee, self, p, null) >
                        BotDecisionSystem.ScoreGoal(BotGoal.Patrol, self, p, null));

            BotBrain brain = new BotBrain(self, self.Profile, new Rng(3u), Difficulty.Normal);
            Assert.Equal(BotGoal.Flee, BotDecisionSystem.Decide(brain, self, null, p));

            BotPerception hunt = new BotPerception();
            Ship prey = NewShip(3);
            prey.Mass = 4f;
            hunt.NearestPrey = prey;
            Assert.True(BotDecisionSystem.ScoreGoal(BotGoal.Hunt, self, hunt, null) >
                        BotDecisionSystem.ScoreGoal(BotGoal.Patrol, self, hunt, null));

            HazardZone zone = new HazardZone(99, new Vec2(80f, 0f), 200f, 20f, 0f, true);
            BotPerception hazard = new BotPerception();
            hazard.NearestHazard = zone;
            Assert.True(BotDecisionSystem.ScoreGoal(BotGoal.AvoidHazard, self, hazard, null) >
                        BotDecisionSystem.ScoreGoal(BotGoal.Idle, self, hazard, null));
        }

        [Fact]
        public void Bots_PerceptionFindsNearbyObjectsAndShips()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 88, Difficulty.Normal, null);
            match.Arena.Objects.Clear();
            match.Arena.Index.Clear();
            match.Arena.Hazards.Clear();
            match.Arena.Anomalies.Clear();

            Ship self = NewShip(100);
            self.Position = Vec2.Zero;
            self.Mass = 10f;
            Ship threat = NewShip(101);
            threat.Position = new Vec2(100f, 0f);
            threat.Mass = 20f;
            Ship prey = NewShip(102);
            prey.Position = new Vec2(-120f, 0f);
            prey.Mass = 5f;
            EnergyNode node = new EnergyNode(500, new Vec2(60f, 0f), 7);

            match.Arena.Add(node);
            match.Ships.Clear();
            match.Ships.Add(self);
            match.Ships.Add(threat);
            match.Ships.Add(prey);

            BotBrain brain = new BotBrain(self, ContentDatabase.FindBotProfile(BotPersonality.Aggressive),
                new Rng(3u), Difficulty.Normal);
            BotPerception perception = new BotPerception();
            perception.Refresh(brain, self, match);

            Assert.Same(node, perception.NearestResource);
            Assert.Same(threat, perception.NearestThreat);
            Assert.Same(prey, perception.NearestPrey);
            Assert.True(perception.ThreatScore > 0f);
            Assert.True(perception.ResourceScore > 0f);
            Assert.Equal(2, perception.NearbyShips.Count);
        }

        [Fact]
        public void Bots_LodIntervalGrowsWithDistanceFromPlayer()
        {
            MatchSimulation nearMatch = new MatchSimulation(GameModeKind.Classic, 55, Difficulty.Normal, null);
            MatchSimulation farMatch = new MatchSimulation(GameModeKind.Classic, 55, Difficulty.Normal, null);
            BotBrain nearBrain = nearMatch.Bots[0];
            BotBrain farBrain = farMatch.Bots[0];
            Vec2 nearPlayer = nearMatch.Player.Position;
            Vec2 farPlayer = farMatch.Player.Position;

            for (int i = 0; i < 8; i++)
            {
                nearBrain.Ship.Position = nearPlayer + new Vec2(120f, 0f);
                nearBrain.Update(0.2f, nearMatch, nearMatch.Log);
                farBrain.Ship.Position = farPlayer + new Vec2(2500f, 0f);
                farBrain.Update(0.2f, farMatch, farMatch.Log);
            }

            Assert.True(nearBrain.LodInterval < farBrain.LodInterval,
                "Far bots should think less often than nearby bots.");
        }

        [Fact]
        public void Bots_NavigationAndActionHistory_Work()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 91, Difficulty.Normal, null);
            match.Arena.Objects.Clear();
            match.Arena.Index.Clear();
            match.Arena.Hazards.Clear();
            match.Arena.Anomalies.Clear();

            Ship self = NewShip(200);
            self.Position = Vec2.Zero;
            self.Facing = Vec2.Right;
            match.Ships.Clear();
            match.Ships.Add(self);

            BotBrain brain = new BotBrain(self, ContentDatabase.FindBotProfile(BotPersonality.Aggressive),
                new Rng(6u), Difficulty.Normal);
            brain.Goal = BotGoal.Patrol;
            BotNavigation navigation = new BotNavigation();
            Vec2 steer = navigation.Steer(brain, self, new Vec2(400f, 0f), match);
            Assert.True(steer.X > 0.5f, "Navigation should steer toward a clear goal.");

            for (int i = 0; i < 30; i++) self.Remember("action" + i.ToString());
            Assert.Equal(24, self.ActionHistory.Count);
            Assert.Equal("action29", self.ActionHistory[self.ActionHistory.Count - 1]);
            Assert.Contains("S200", brain.DebugInfo());
        }

        // 11. World events.

        [Fact]
        public void WorldEvents_WarningPhasePrecedesStart()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Chaos, 321, Difficulty.Normal, null);
            SimLog log = new SimLog();
            match.Events.NextEventIn = 0.01f;

            match.Events.Update(0.1f, match, log);
            Assert.True(match.Events.PendingKind.HasValue);
            Assert.False(match.Events.ActiveKind.HasValue);
            Close(GameConfig.EventWarningTime, match.Events.WarningRemaining, 0.2f);
            Assert.True(HasEvent(log.Drain(), SimEventKind.EventWarning));

            match.Events.Update(7.85f, match, log);
            Assert.True(match.Events.PendingKind.HasValue);
            Assert.False(match.Events.ActiveKind.HasValue);

            match.Events.Update(0.2f, match, log);
            Assert.True(match.Events.ActiveKind.HasValue);
            Assert.False(match.Events.PendingKind.HasValue);
            Assert.True(HasEvent(log.Drain(), SimEventKind.EventStart));
        }

        [Fact]
        public void WorldEvents_NoOverlapAndPendingStartAfterEnd()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 99, Difficulty.Normal, null);
            SimLog log = new SimLog();
            match.Events.Trigger(WorldEventKind.MeteorShower, match, log);
            WorldEventKind active = match.Events.ActiveKind.Value;

            match.Events.Trigger(WorldEventKind.VoidZone, match, log);
            Assert.Equal(active, match.Events.ActiveKind.Value);
            Assert.Equal(WorldEventKind.VoidZone, match.Events.PendingKind.Value);

            match.Events.WarningRemaining = 0f;
            match.Events.ActiveRemaining = 0.01f;
            match.Events.Update(0.1f, match, log);
            Assert.Equal(WorldEventKind.VoidZone, match.Events.ActiveKind.Value);
            Assert.False(match.Events.PendingKind.HasValue);
            Assert.True(HasEvent(log.Drain(), SimEventKind.EventEnd));
        }

        [Theory]
        [InlineData(WorldEventKind.MeteorShower)]
        [InlineData(WorldEventKind.BlackHole)]
        [InlineData(WorldEventKind.EnergyStorm)]
        [InlineData(WorldEventKind.VoidZone)]
        [InlineData(WorldEventKind.EliteInvasion)]
        [InlineData(WorldEventKind.AncientRelic)]
        [InlineData(WorldEventKind.SpaceCollapse)]
        public void WorldEvents_AllKinds_MutateArenaAndLog(WorldEventKind kind)
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 4321, Difficulty.Normal, null);
            Arena arena = match.Arena;
            SimLog log = new SimLog();
            int energyBefore = CountObjects(arena, ObjectKind.Energy);
            int artifactsBefore = CountObjects(arena, ObjectKind.Artifact);
            int shipsBefore = match.Ships.Count;
            float safeBefore = arena.SafeRadius;

            match.Events.Trigger(kind, match, log);
            Assert.Equal(kind, match.Events.ActiveKind.Value);
            Assert.True(match.Events.ActiveRemaining > 0f);
            Assert.False(string.IsNullOrEmpty(match.Events.ActiveTitle));
            Assert.True(HasEvent(log.Drain(), SimEventKind.EventStart));

            match.Events.Update(1f, match, log);

            switch (kind)
            {
                case WorldEventKind.MeteorShower:
                    Assert.True(CountSourceObjects(arena, "MeteorShower") > 0);
                    break;
                case WorldEventKind.BlackHole:
                    Assert.True(CountSourceObjects(arena, "BlackHole") >= 2);
                    break;
                case WorldEventKind.EnergyStorm:
                    Assert.True(CountObjects(arena, ObjectKind.Energy) > energyBefore);
                    break;
                case WorldEventKind.VoidZone:
                    Assert.True(CountSourceObjects(arena, "VoidZone") >= 3);
                    break;
                case WorldEventKind.EliteInvasion:
                    Assert.True(match.Ships.Count >= shipsBefore + 2);
                    Assert.True(CountObjects(arena, ObjectKind.Artifact) > artifactsBefore);
                    break;
                case WorldEventKind.AncientRelic:
                    Assert.True(CountObjects(arena, ObjectKind.Artifact) > artifactsBefore);
                    break;
                case WorldEventKind.SpaceCollapse:
                    Assert.True(arena.SafeRadius < safeBefore);
                    Assert.True(CountSourceObjects(arena, "SpaceCollapse") > 0);
                    break;
            }
        }

        // 12. Leaderboard and rewards.

        [Fact]
        public void Leaderboard_RanksAreSortedAndResultIsBuilt()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 777, Difficulty.Normal,
                new AccountProfile());
            match.Player.Score = 1000f;
            match.Finish(false);

            Assert.True(match.Finished);
            Assert.NotNull(match.Result);
            Assert.Equal(1, match.PlayerRank);
            Assert.True(match.Leaderboard[0].IsPlayer);
            for (int i = 0; i < match.Leaderboard.Count; i++)
                Assert.Equal(i + 1, match.Leaderboard[i].Rank);

            Assert.Equal(1, match.Result.Rank);
            Assert.Equal(match.Settings.Participants, match.Result.Participants);
            Assert.True(match.Result.Victory);
            Assert.True(match.Result.Credits > 0);
            Assert.True(match.Result.Xp > 0);
            Assert.True(match.Result.AccountXp > 0);
            Assert.True(HasEvent(match.Log.Drain(), SimEventKind.MatchEnd));
        }

        [Fact]
        public void Rewards_ApplyMatchResultUpdatesProfileAndDailyRollover()
        {
            AccountProfile profile = new AccountProfile();
            profile.Research["res_economy"] = 1;
            MatchResult result = new MatchResult();
            result.Mode = GameModeKind.Classic;
            result.Duration = 120f;
            result.Rank = 1;
            result.Participants = 10;
            result.Score = 1000f;
            result.Mass = 80f;
            result.Level = 5;
            result.Kills = 3;
            result.Deaths = 0;
            result.EnergyCollected = 500f;
            result.Crystals = 8;
            result.DarkMatter = 4;
            result.Artifacts = 2;
            result.BossKills = 1;
            result.BestStreak = 3;
            result.Victory = true;
            result.Credits = 600;
            result.CrystalsReward = 9;
            result.Xp = 100;
            result.AccountXp = 200;

            DateTime now = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);
            ProgressionRewards rewards = ProgressionSystem.ApplyMatchResult(profile, result, now);
            Assert.Equal(rewards.Credits, profile.Credits);
            Assert.True(rewards.Credits >= 600);
            Assert.True(rewards.Crystals >= 9);
            Assert.True(rewards.DarkMatter >= 4);
            Assert.True(rewards.Xp >= 200f);
            Close(profile.Xp, rewards.Xp, Epsilon);
            Assert.True(rewards.Victory);
            Assert.True(rewards.NewRecord);
            Assert.Equal(1, profile.Matches);
            Assert.Equal(1, profile.Wins);
            Assert.Equal(1, profile.WinStreak);
            Assert.Equal(3, profile.Stat(ProgressionSystem.StatKills));
            Assert.True(profile.IsUnlocked(AccountProfile.DefaultShip));
            Assert.Contains("first_blood", profile.Achievements);
            Assert.Contains("ПЕРВАЯ КРОВЬ", rewards.Achievements);
            Assert.Equal(ProgressionSystem.DayKey(now), profile.DailyDate);

            profile.DailyProgress["q_kills"] = 5;
            profile.DailyDate = "2000-01-01";
            DateTime nextDay = now.AddDays(1);
            ProgressionSystem.RollOver(profile, nextDay);
            Assert.Empty(profile.DailyProgress);
            Assert.Equal(ProgressionSystem.DayKey(nextDay), profile.DailyDate);
        }

        // 13. Save and migration.

        [Fact]
        public void SaveMigration_RoundTripIsDeterministic()
        {
            AccountProfile profile = new AccountProfile();
            profile.Level = 12;
            profile.Xp = 456.5f;
            profile.Credits = 1234;
            profile.Crystals = 56;
            profile.DarkMatter = 7;
            profile.SelectedShip = "ship_orion";
            profile.Unlock("ship_orion");
            profile.Research["res_armor"] = 2;
            profile.Stats["kills"] = 9;
            profile.Settings.MusicVolume = 0.3f;
            profile.Settings.Quality = GameSettings.QualityHigh;

            string json = SaveMigration.ToJson(profile);
            string secondJson = SaveMigration.ToJson(SaveMigration.FromJson(json));
            Assert.Equal(json, secondJson);

            AccountProfile loaded = SaveMigration.FromJson(json);
            Assert.Equal(SaveMigration.CurrentSchema, loaded.SchemaVersion);
            Assert.Equal(12, loaded.Level);
            Close(456.5f, loaded.Xp, Epsilon);
            Assert.Equal(1234, loaded.Credits);
            Assert.Equal("ship_orion", loaded.SelectedShip);
            Assert.True(loaded.IsUnlocked("ship_orion"));
            Close(0.06f, loaded.ResearchBonus("res_armor"), Epsilon);
            Assert.Equal(9, loaded.Stat(ProgressionSystem.StatKills));
        }

        [Fact]
        public void SaveMigration_LegacyJsonMigratesToSchema5()
        {
            string legacy = "{\"playerLevel\":9,\"credits\":123,\"inventory\":[\"ship_cyan\"]," +
                            "\"bestScore\":500,\"SaveSchemaVersion\":4}";
            Assert.True(SaveMigration.LooksLikeLegacy(legacy));

            AccountProfile profile = SaveMigration.FromLegacy(legacy);
            Assert.Equal(SaveMigration.CurrentSchema, profile.SchemaVersion);
            Assert.Equal(9, profile.Level);
            Assert.Equal(123, profile.Credits);
            Assert.True(profile.IsUnlocked("ship_nova"));
            Assert.Equal(500, profile.Records["bestScore"]);

            AccountProfile viaManager = SaveManager.Migrate(legacy);
            Assert.Equal(9, viaManager.Level);
            Assert.Equal(123, viaManager.Credits);
        }

        [Fact]
        public void SaveManager_SaveBackupRestore_LoadsBackProfile()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "orbita-galaxy-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                SaveManager.SetPath(directory);
                AccountProfile profile = new AccountProfile();
                profile.Credits = 777;
                profile.SelectedShip = "ship_vesta";
                SaveManager.Save(profile);

                Assert.True(File.Exists(SaveManager.SavePath));
                SaveManager.Backup();
                Assert.True(File.Exists(SaveManager.BackupPath));

                profile.Credits = 1;
                SaveManager.Save(profile);
                Assert.Equal(1, SaveManager.Load().Credits);

                Assert.True(SaveManager.Restore());
                AccountProfile restored = SaveManager.Load();
                Assert.Equal(777, restored.Credits);
                Assert.Equal("ship_vesta", restored.SelectedShip);
            }
            finally
            {
                SaveManager.SetPath(null);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void SaveManager_AutomaticBackup_ProtectsPreviousProfile()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "orbita-galaxy-backup-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                SaveManager.SetPath(directory);
                AccountProfile profile = new AccountProfile();
                profile.Credits = 55;
                profile.SelectedShip = "ship_orig";
                SaveManager.Save(profile);
                Assert.False(File.Exists(SaveManager.BackupPath));

                profile.Credits = 99;
                profile.SelectedShip = "ship_new";
                SaveManager.Save(profile);

                Assert.True(File.Exists(SaveManager.BackupPath));
                Assert.Equal(99, SaveManager.Load().Credits);
                Assert.True(SaveManager.Restore());
                AccountProfile restored = SaveManager.Load();
                Assert.Equal(55, restored.Credits);
                Assert.Equal("ship_orig", restored.SelectedShip);
            }
            finally
            {
                SaveManager.SetPath(null);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        // 14. Match simulation, completion, deterministic replay, 35-player long run.

        [Fact]
        public void MatchSimulation_ConstructorUsesModeSettingsAndStressParticipants()
        {
            AccountProfile profile = new AccountProfile();
            MatchSimulation classic = new MatchSimulation(GameModeKind.Classic, 123, Difficulty.Normal, profile);
            Assert.Equal(GameConfig.TargetParticipants, classic.Settings.Participants);
            Assert.Equal(GameConfig.TargetParticipants, classic.Ships.Count);
            Assert.Equal(GameConfig.TargetParticipants - 1, classic.Bots.Count);
            Assert.Equal(GameConfig.TargetParticipants, classic.Leaderboard.Count);
            Assert.True(classic.Player.IsPlayer);
            Assert.Equal(0f, classic.Time);
            Assert.False(classic.Finished);

            GameModeKind[] modes =
            {
                GameModeKind.Classic, GameModeKind.Survival, GameModeKind.BossHunt,
                GameModeKind.Chaos, GameModeKind.Hardcore, GameModeKind.Infinite
            };
            for (int i = 0; i < modes.Length; i++)
            {
                ModeSettings settings = new ModeSettings(modes[i], Difficulty.Hard);
                ModeDef def = ContentDatabase.FindMode(modes[i]);
                Assert.Equal(def.Name, settings.Title);
                Close(def.DurationMinutes * 60f, settings.DurationSeconds, Epsilon);
                Assert.Equal(def.ShrinkingArena, settings.ShrinkingArena);
                Assert.Equal(def.Bosses, settings.BossesEnabled);
                Assert.Equal(def.ChaosEvents, settings.ChaosEvents);
                Assert.Equal(def.Respawns, settings.Respawns);
                Assert.Equal(Difficulty.Hard, settings.Difficulty);
            }

            MatchSimulation stress = new MatchSimulation(GameModeKind.Chaos, 123, Difficulty.Hard, profile, 99);
            Assert.Equal(GameConfig.MaxParticipants, stress.Settings.Participants);
            Assert.Equal(GameConfig.MaxParticipants, stress.Ships.Count);
            Assert.Equal(GameConfig.MaxParticipants - 1, stress.Bots.Count);
        }

        [Fact]
        public void MatchSimulation_SameSeedReplaysDeterministically()
        {
            MatchSimulation a = new MatchSimulation(GameModeKind.Classic, 4242, Difficulty.Normal,
                new AccountProfile());
            MatchSimulation b = new MatchSimulation(GameModeKind.Classic, 4242, Difficulty.Normal,
                new AccountProfile());

            for (int step = 0; step < 90; step++)
            {
                Vec2 move = step < 45 ? Vec2.Right : Vec2.Up;
                a.PlayerMove(move);
                b.PlayerMove(move);
                a.PlayerBoost(step % 7 == 0);
                b.PlayerBoost(step % 7 == 0);
                a.Update(GameConfig.SimulationStep);
                b.Update(GameConfig.SimulationStep);
            }

            Close(a.Time, b.Time, Epsilon);
            Assert.True(CloseVec(a.Player.Position, b.Player.Position, 0.001f));
            Close(a.Player.Mass, b.Player.Mass, 0.001f);
            Close(a.Player.Score, b.Player.Score, 0.001f);
            Assert.Equal(a.Ships.Count, b.Ships.Count);
            Assert.Equal(a.Leaderboard.Count, b.Leaderboard.Count);
            Assert.Equal(a.Events.ActiveKind, b.Events.ActiveKind);
            Assert.Equal(a.Rng.State, b.Rng.State);
            Assert.Equal(a.Leaderboard[0].ShipId, b.Leaderboard[0].ShipId);
        }

        [Fact]
        public void MatchSimulation_FinishBuildsRewardedResult()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Classic, 808, Difficulty.Normal,
                new AccountProfile());
            match.Time = 123f;
            match.Player.Kills = 2;
            match.Player.Score = 500f;
            match.Finish(false);

            Assert.True(match.Finished);
            Assert.NotNull(match.Result);
            Close(123f, match.Result.Duration, Epsilon);
            Assert.Equal(1, match.Result.Rank);
            Assert.Equal(2, match.Result.Kills);
            Assert.Equal(match.Settings.Participants, match.Result.Leaderboard.Count);
            Assert.True(match.Result.Credits > 0);
            Assert.True(match.Result.CrystalsReward >= 0);
            Assert.True(match.Result.Xp > 0);
            Assert.True(match.Result.AccountXp > 0);
            Assert.NotEmpty(match.Result.Highlights);
        }

        [Fact]
        public void MatchSimulation_35PlayersLongRun_StaysStable()
        {
            MatchSimulation match = new MatchSimulation(GameModeKind.Chaos, 13579, Difficulty.Hard,
                new AccountProfile(), GameConfig.MaxParticipants);
            match.Settings.Respawns = 1000;
            match.Settings.DurationSeconds = 520f;
            int steps = (int)(480f / GameConfig.SimulationStep);
            bool sawEventSystem = false;

            for (int i = 0; i < steps; i++)
            {
                if (match.IsEvolutionPaused) match.PlayerChooseEvolution(null);
                match.PlayerMove(Vec2.FromAngle(i * 0.017f));
                match.PlayerAim(Vec2.FromAngle(i * 0.011f));
                match.Update(GameConfig.SimulationStep);
                if (match.Events.ActiveKind.HasValue || match.Events.PendingKind.HasValue)
                    sawEventSystem = true;
            }

            Assert.False(match.Finished);
            Close(480f, match.Time, 0.1f);
            Assert.True(match.Ships.Count >= GameConfig.MaxParticipants);
            Assert.Equal(match.Ships.Count, match.Leaderboard.Count);

            for (int i = 0; i < match.Ships.Count; i++)
            {
                Ship ship = match.Ships[i];
                Assert.NotNull(ship);
                Assert.False(float.IsNaN(ship.Position.X) || float.IsNaN(ship.Position.Y));
                Assert.True(ship.Energy >= 0f && ship.Energy <= GameConfig.MaxEnergy);
            }
            for (int i = 0; i < match.Leaderboard.Count; i++)
                Assert.Equal(i + 1, match.Leaderboard[i].Rank);

            Assert.True(match.Combat.Projectiles.Count < 1024);
            Assert.True(sawEventSystem);
        }
    }
}
