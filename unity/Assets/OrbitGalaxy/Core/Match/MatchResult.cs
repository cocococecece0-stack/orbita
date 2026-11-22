using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>Final, save-ready summary of a finished match. All text is Russian.</summary>
    public sealed class MatchResult
    {
        public GameModeKind Mode;
        public float Duration;
        public int Rank;
        public int Participants;
        public float Score;
        public float Mass;

        /// <summary>Snake body length at the end of the match, for the result screen's size stat.</summary>
        public int Length;

        /// <summary>Time until the player's first/final death, or the full match time if alive.</summary>
        public float SurvivalTime;

        /// <summary>Highest snake growth tier reached in this match (0-3).</summary>
        public int SnakeEvolutionTier;

        /// <summary>Successful player snake ability activations, for cosmetic goal progress.</summary>
        public int SnakeAbilityUses;
        public int Level;
        public int Kills;
        public int Deaths;
        public float EnergyCollected;
        public int Crystals;
        public int DarkMatter;
        public int Artifacts;
        public int BossKills;
        public int BestStreak;
        public bool Victory;
        public int Credits;
        public int CrystalsReward;
        public int Xp;
        public int AccountXp;
        public List<LeaderboardEntry> Leaderboard;
        public List<SimEvent> Highlights;

        public MatchResult()
        {
            Leaderboard = new List<LeaderboardEntry>();
            Highlights = new List<SimEvent>();
        }
    }
}
