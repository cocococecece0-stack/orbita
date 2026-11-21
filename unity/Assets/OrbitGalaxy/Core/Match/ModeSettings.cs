namespace Orbit.Galaxy
{
    /// <summary>Runtime-resolved rules of one match. Values come from ContentDatabase.Modes.</summary>
    public sealed class ModeSettings
    {
        public GameModeKind Mode;
        public string Title;
        public string Description;
        public int Participants;
        public float DurationSeconds;
        public float DangerScale;
        public float RewardScale;
        public bool ShrinkingArena;
        public bool BossesEnabled;
        public bool ChaosEvents;
        public int Respawns;
        public Difficulty Difficulty;

        public ModeSettings()
            : this(GameModeKind.Classic, Difficulty.Normal)
        {
        }

        public ModeSettings(GameModeKind mode, Difficulty difficulty)
        {
            ModeDef def = ContentDatabase.FindMode(mode);
            Mode = mode;
            Title = def == null ? "АРЕНА" : def.Name;
            Description = def == null ? "Сражение в галактике." : def.Description;
            Participants = def == null ? GameConfig.TargetParticipants :
                MathGalaxy.Clamp(def.Participants, GameConfig.MinParticipants, GameConfig.MaxParticipants);
            DurationSeconds = (def == null ? 8f : def.DurationMinutes) * 60f;
            DangerScale = def == null ? 1f : def.DangerScale;
            RewardScale = def == null ? 1f : def.RewardScale;
            ShrinkingArena = def != null && def.ShrinkingArena;
            BossesEnabled = def == null || def.Bosses;
            ChaosEvents = def != null && def.ChaosEvents;
            Respawns = def == null ? 1 : def.Respawns;
            Difficulty = difficulty;
        }

        public bool HasRespawns { get { return Respawns > 0; } }

        public bool EndsOnPlayerDeath
        {
            get { return Mode == GameModeKind.Survival || Mode == GameModeKind.Hardcore ||
                         Mode == GameModeKind.Infinite; }
        }
    }
}
