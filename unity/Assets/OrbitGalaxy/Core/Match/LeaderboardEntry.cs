namespace Orbit.Galaxy
{
    /// <summary>One immutable row of the in-match leaderboard.</summary>
    public sealed class LeaderboardEntry
    {
        public int ShipId;
        public string Name;
        public float Score;
        public float Mass;
        public int Level;
        public int Kills;
        public int Rank;
        public bool IsPlayer;
        public BotPersonality Personality;
        public bool IsBoss;

        public LeaderboardEntry()
        {
            Name = string.Empty;
        }

        public LeaderboardEntry(Ship ship, int rank)
        {
            ShipId = ship == null ? -1 : ship.Id;
            Name = ship == null ? string.Empty : ship.Name;
            Score = ship == null ? 0f : ship.Score;
            Mass = ship == null ? 0f : ship.Mass;
            Level = ship == null ? 0 : (int)ship.Level;
            Kills = ship == null ? 0 : ship.Kills;
            Rank = rank;
            IsPlayer = ship != null && ship.IsPlayer;
            Personality = ship == null ? BotPersonality.Aggressive : ship.Personality;
            IsBoss = ship != null && ship.IsBoss;
        }
    }
}
