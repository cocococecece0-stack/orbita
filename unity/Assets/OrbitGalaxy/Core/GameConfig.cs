namespace Orbit.Galaxy
{
    /// <summary>Central balance table. Runtime data assets mirror these values; the core remains testable without Unity.</summary>
    public static class GameConfig
    {
        public const string GameTitle = "ORBITA GALAXY";
        public const string SaveFileName = "orbita_galaxy_save_v5.json";
        public const int SaveSchemaVersion = 5;

        // Arena and simulation.
        public const float ArenaRadius = 2600f;
        public const float SafeRadiusStart = 2480f;
        public const float CellSize = 180f;
        public const float SimulationStep = 1f / 30f;
        public const float MaxFrameDelta = 0.25f;
        public const float BorderDamagePerSecond = 14f;
        public const int TargetParticipants = 28;
        public const int MinParticipants = 21;
        public const int MaxParticipants = 35;

        // Player ship baseline.
        public const float BaseMass = 10f;
        public const float BaseRadius = 16f;
        public const float BaseSpeed = 230f;
        public const float BaseHp = 100f;
        public const float BaseTurnSpeed = 5.4f;
        public const float BasePickupRadius = 46f;
        public const float BaseDamage = 12f;
        public const float BaseFireInterval = 0.42f;
        public const float BaseProjectileSpeed = 620f;

        // Growth curve.
        public const float MassPerLevel = 14f;
        public const float XpPerLevelBase = 60f;
        public const float XpLevelGrowth = 1.22f;
        public const float MassGainEnergy = 1.6f;
        public const float MassGainCrystal = 9f;
        public const float MassGainDarkMatter = 16f;
        public const float MassGainArtifact = 32f;
        public const float AbsorbEfficiency = 0.62f;
        public const float AbsorbMassRatio = 1.18f;
        public const float CloseFightRatio = 0.78f;

        // Movement consequences of size. Sizeable ships are strong but sluggish.
        public const float SpeedMassExponent = 0.22f;
        public const float MinimumSpeedFactor = 0.46f;
        public const float BoostMultiplier = 1.62f;
        public const float BoostEnergyDrain = 24f;
        public const float EnergyRegenPerSecond = 14f;
        public const float MaxEnergy = 100f;

        // Combat.
        public const float DamageMassExponent = 0.35f;
        public const float ShieldAbsorption = 0.82f;
        public const float RespawnDelay = 3.2f;
        public const float RespawnInvulnerability = 2.2f;
        public const int MultiKillWindow = 4;
        public const float MultiKillTime = 3.4f;

        // World population.
        public const int EnergyNodes = 620;
        public const int CrystalNodes = 90;
        public const int DarkMatterNodes = 42;
        public const int ArtifactNodes = 12;
        public const int Asteroids = 150;
        public const int Hazards = 16;
        public const int Anomalies = 9;
        public const float ResourceRespawnSeconds = 16f;
        public const float AsteroidRespawnSeconds = 38f;

        // Events.
        public const float FirstEventDelay = 32f;
        public const float EventWarningTime = 8f;
        public const float EventDurationMin = 22f;
        public const float EventDurationMax = 38f;
        public const float EventIntervalMin = 30f;
        public const float EventIntervalMax = 52f;

        // AI cadence tiers (seconds). Far bots think less often but still simulate movement.
        public const float AiNearInterval = 0.18f;
        public const float AiMidInterval = 0.42f;
        public const float AiFarInterval = 0.95f;
        public const float AiNearRange = 900f;
        public const float AiMidRange = 1800f;

        // Presentation caps.
        public const int MaxVisibleParticles = 520;
        public const int MaxVisibleShips = 40;
        public const int FxPoolSize = 360;
    }
}
