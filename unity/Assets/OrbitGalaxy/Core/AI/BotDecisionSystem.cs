using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Utility scoring for every BotGoal. Higher is better; scores are roughly 0..12 and are
    /// comparable between goals so hysteresis can be applied uniformly.
    /// </summary>
    public static class BotDecisionSystem
    {
        private static readonly BotGoal[] AllGoals =
        {
            BotGoal.Idle, BotGoal.Farm, BotGoal.Hunt, BotGoal.Flee, BotGoal.CollectRare,
            BotGoal.AvoidHazard, BotGoal.ContestEvent, BotGoal.AttackBoss, BotGoal.Patrol,
            BotGoal.Regroup
        };

        public static BotGoal Decide(BotBrain brain, Ship self, MatchSimulation match, BotPerception perception)
        {
            if (self == null || self.State != ShipState.Alive) return BotGoal.Idle;
            if (perception == null) perception = new BotPerception();

            BotProfile profile = brain == null ? self.Profile : brain.Profile;
            if (profile == null) profile = ContentDatabase.BotProfiles[1];
            BotTuning tuning = brain == null ? null : brain.Tuning;
            float switchRate = Math.Max(0.5f, tuning == null ? 1f : tuning.SwitchRate);
            BotGoal current = brain == null ? BotGoal.Idle : brain.Goal;
            float currentScore = ScoreGoal(current, brain, self, perception, match);
            float bestScore = float.NegativeInfinity;
            BotGoal best = current;

            // Keeping the current goal is cheap; switching must beat that inherent advantage.
            // Archetype switch rate replaces the old hard-coded Chaotic special case.
            float hysteresis = (1.05f + profile.Reaction * 1.55f) / switchRate;
            if (brain != null && brain.IsElite) hysteresis += 0.35f / switchRate;

            for (int i = 0; i < AllGoals.Length; i++)
            {
                BotGoal goal = AllGoals[i];
                float score = ScoreGoal(goal, brain, self, perception, match);
                if (goal == current) score += hysteresis;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = goal;
                }
            }

            float margin = (0.35f + profile.Reaction * 0.55f) / switchRate;
            if (best != current && bestScore - currentScore < margin) return current;
            return best;
        }

        public static float ScoreGoal(BotGoal goal, Ship self, BotPerception p, MatchSimulation match)
        {
            return ScoreGoal(goal, null, self, p, match);
        }

        /// <summary>
        /// Scores one goal with the bot's archetype and difficulty tuning applied. The four-argument
        /// overload above keeps the original utility signature for plain ships and tests.
        /// </summary>
        public static float ScoreGoal(BotGoal goal, BotBrain brain, Ship self, BotPerception p,
            MatchSimulation match)
        {
            if (self == null || self.State != ShipState.Alive || p == null) return 0f;
            BotProfile profile = brain != null && brain.Profile != null ? brain.Profile :
                                 (self.Profile != null ? self.Profile : ContentDatabase.BotProfiles[1]);
            BotTuning tuning = brain == null ? null : brain.Tuning;
            float aggression = MathGalaxy.Clamp(profile.Aggression * (tuning == null ? 1f : tuning.Aggression), 0f, 1f);
            float greed = MathGalaxy.Clamp(profile.Greed * (tuning == null ? 1f : tuning.Greed), 0f, 1f);
            float caution = MathGalaxy.Clamp(profile.Caution * (tuning == null ? 1f : tuning.Caution), 0f, 1f);
            float risk = MathGalaxy.Clamp(profile.Risk * (tuning == null ? 1f : tuning.Risk), 0f, 1f);
            float hpRatio = self.MaxHp > 0f ? MathGalaxy.Clamp(self.Hp / self.MaxHp, 0f, 1f) : 1f;

            // Other snakes' segments are lethal to a head. Awareness of them feeds danger scoring
            // and makes cautious bots pick safer routes instead of blindly taking the closest mote.
            float snakeThreat = p.SnakeThreatScore;
            float catchUp = 0f;
            bool competitor = brain != null && brain.Archetype != null &&
                              string.Equals(brain.Archetype.Id, "competitor", StringComparison.Ordinal);
            if (competitor && match != null && self.Rank > 0 && match.Leaderboard.Count > 1)
            {
                float standing = (float)self.Rank / match.Leaderboard.Count;
                catchUp = MathGalaxy.Clamp(standing - 0.35f, 0f, 1f) * 3.2f;
            }

            switch (goal)
            {
                case BotGoal.Farm:
                {
                    float score = p.ResourceScore * (0.9f + greed * 0.75f) + catchUp;
                    if (p.NearestResource != null || p.NearestEnergy != null) score += 1.2f;
                    score -= snakeThreat * caution * 0.25f;
                    if (hpRatio < 0.4f) score -= 1.5f;
                    return score;
                }
                case BotGoal.Hunt:
                {
                    if (p.NearestPrey == null) return -2f;
                    float ratio = Math.Max(0.01f, self.Mass / Math.Max(1f, p.NearestPrey.Mass));
                    float score = 2.2f + aggression * 4.6f + MathGalaxy.Clamp(ratio - 1f, 0f, 2f) * 1.8f + catchUp;
                    score -= p.ThreatScore * (0.5f + caution * 0.6f);
                    score -= snakeThreat * (0.2f + caution * 0.4f);
                    if (hpRatio < 0.35f) score -= 3f;
                    return score;
                }
                case BotGoal.Flee:
                {
                    float snakeDanger = snakeThreat * (0.3f + caution * 0.35f);
                    if (p.NearestThreat == null && p.ThreatScore < 2f && snakeDanger < 1.2f) return -3f;
                    float danger = p.ThreatScore + snakeDanger;
                    float score = danger * (0.75f + caution * 1.05f);
                    if (hpRatio < 0.42f) score += (0.42f - hpRatio) * 12f;
                    if (p.NearestThreat != null && p.NearestThreat.IsBoss) score += 2.5f;
                    return score;
                }
                case BotGoal.CollectRare:
                {
                    if (p.NearestRare == null) return -2f;
                    float score = 2.5f + greed * 4.2f + risk * 1.4f + catchUp;
                    score -= (p.ThreatScore * (0.55f + caution * 0.45f)) + snakeThreat * caution * 0.2f;
                    return score;
                }
                case BotGoal.AvoidHazard:
                {
                    if (p.NearestHazard == null) return -4f;
                    float dist = Vec2.Distance(self.Position, p.NearestHazard.Position) -
                                 p.NearestHazard.Radius;
                    float proximity = MathGalaxy.Clamp(1f - dist / 260f, 0f, 1f);
                    float score = 3.5f + proximity * 5f + caution * 1.5f;
                    if (p.NearestHazard.Slows) score += 1.2f;
                    return score;
                }
                case BotGoal.ContestEvent:
                {
                    if (match == null || match.Events == null) return -3f;
                    bool active = match.Events.ActiveKind.HasValue;
                    bool warning = match.Events.PendingKind.HasValue;
                    if (!active && !warning) return -3f;
                    float score = active ? 4.8f : 3.4f;
                    score += greed * 1.7f + risk * 1.4f + aggression * 0.8f + catchUp;
                    score -= p.ThreatScore * caution * 0.45f;
                    if (hpRatio < 0.3f) score -= 2f;
                    return score;
                }
                case BotGoal.AttackBoss:
                {
                    if (p.NearestBoss == null) return -3f;
                    float hpRatioBoss = p.NearestBoss.MaxHp > 0f
                        ? MathGalaxy.Clamp(p.NearestBoss.Hp / p.NearestBoss.MaxHp, 0f, 1f)
                        : 1f;
                    float score = 3.2f + aggression * 2.6f + risk * 2f + (1f - hpRatioBoss) * 2.3f;
                    if (profile.Personality == BotPersonality.Hunter ||
                        profile.Personality == BotPersonality.Elite) score += 1.6f;
                    if (hpRatio < 0.4f) score -= 4f;
                    score -= p.ThreatScore * 0.35f;
                    return score;
                }
                case BotGoal.Patrol:
                {
                    float score = 1.3f + (p.NearbyObjects.Count < 4 ? 0.8f : 0f);
                    if (p.NearestThreat == null && p.NearestPrey == null && p.NearestResource == null)
                        score += 1.4f;
                    return score;
                }
                case BotGoal.Regroup:
                {
                    if (hpRatio > 0.62f && p.ThreatScore < 2f) return -2.5f;
                    int allies = 0;
                    for (int i = 0; i < p.NearbyShips.Count; i++)
                    {
                        Ship other = p.NearbyShips[i];
                        if (other != null && other.Mass <= self.Mass * 1.4f) allies++;
                    }
                    if (allies == 0) return -1f;
                    return 2.1f + (1f - hpRatio) * 4f + caution * 1.2f;
                }
                case BotGoal.Idle:
                default:
                    return 0.15f;
            }
        }
    }
}
