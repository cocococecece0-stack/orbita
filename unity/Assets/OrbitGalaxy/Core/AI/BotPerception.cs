using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Snapshot of everything a bot currently knows about its surroundings. The object and ship
    /// lists are recycled between Refresh calls so thinking does not allocate every tick.
    /// </summary>
    public sealed class BotPerception
    {
        public Ship NearestThreat;
        public Ship NearestPrey;
        public Ship NearestBoss;
        public WorldObject NearestResource;
        public WorldObject NearestRare;
        public WorldObject NearestEnergy;
        public Ship NearestSnakeThreat;
        public Vec2 PredictedPreyPoint;
        public Vec2 PredictedThreatPoint;
        public HazardZone NearestHazard;
        public Anomaly NearestAnomaly;
        public float ThreatScore;
        public float ResourceScore;
        public float SnakeThreatScore;
        public readonly List<WorldObject> NearbyObjects = new List<WorldObject>(64);
        public readonly List<Ship> NearbyShips = new List<Ship>(16);

        private readonly List<SnakeSegmentRef> _segments = new List<SnakeSegmentRef>(32);
        private float _bestEnergyWeight;
        private float _bestThreatWeight;
        private float _bestPreyWeight;
        private float _bestBossWeight;
        private float _bestResourceWeight;
        private float _bestRareWeight;
        private float _bestHazardWeight;
        private float _bestAnomalyWeight;

        public void Refresh(BotBrain brain, Ship self, MatchSimulation match)
        {
            NearestThreat = null;
            NearestPrey = null;
            NearestBoss = null;
            NearestResource = null;
            NearestRare = null;
            NearestEnergy = null;
            NearestSnakeThreat = null;
            PredictedPreyPoint = Vec2.Zero;
            PredictedThreatPoint = Vec2.Zero;
            NearestHazard = null;
            NearestAnomaly = null;
            ThreatScore = 0f;
            ResourceScore = 0f;
            SnakeThreatScore = 0f;
            _bestEnergyWeight = 0f;
            _bestThreatWeight = 0f;
            _bestPreyWeight = 0f;
            _bestBossWeight = 0f;
            _bestResourceWeight = 0f;
            _bestRareWeight = 0f;
            _bestHazardWeight = 0f;
            _bestAnomalyWeight = 0f;
            NearbyObjects.Clear();
            NearbyShips.Clear();

            if (self == null || match == null || match.Arena == null) return;

            float radius = 820f + MathGalaxy.Clamp(self.Mass, 0f, 260f) * 2.2f;
            if (brain != null && brain.IsElite) radius *= 1.16f;
            float myMass = Math.Max(1f, self.Mass);
            float hpFactor = self.MaxHp > 0f ? MathGalaxy.Clamp(1f - self.Hp / self.MaxHp, 0f, 1f) : 0f;
            float aggression = brain == null || brain.Profile == null ? 0.5f : brain.Profile.Aggression;
            float greed = brain == null || brain.Profile == null ? 0.5f : brain.Profile.Greed;
            float predictHorizon = 1.4f * (brain == null || brain.Tuning == null ? 1f : brain.Tuning.DecisionQuality);

            Arena arena = match.Arena;
            arena.QueryInto(self.Position, radius, NearbyObjects);

            for (int i = 0; i < NearbyObjects.Count; i++)
            {
                WorldObject obj = NearbyObjects[i];
                if (obj == null || !obj.Alive) continue;
                float dist = Vec2.Distance(self.Position, obj.Position);
                float proximity = 1f / (1f + dist / 220f);

                if (obj is HazardZone)
                {
                    HazardZone zone = (HazardZone)obj;
                    float reach = radius + obj.Radius;
                    if (dist > reach) continue;
                    float weight = (0.6f + zone.DamagePerSecond / 10f + (zone.Slows ? 0.8f : 0f)) *
                                   (1f - MathGalaxy.Clamp(dist / Math.Max(1f, reach), 0f, 1f));
                    if (weight > _bestHazardWeight)
                    {
                        _bestHazardWeight = weight;
                        NearestHazard = zone;
                    }
                    ThreatScore += weight * 0.9f;
                    continue;
                }

                if (obj is Anomaly)
                {
                    Anomaly anomaly = (Anomaly)obj;
                    float reach = radius + obj.Radius;
                    if (dist > reach) continue;
                    float weight = (0.4f + anomaly.Pull * 0.6f + (anomaly.GrantsShield ? 0.7f : 0f)) *
                                   (1f - MathGalaxy.Clamp(dist / Math.Max(1f, reach), 0f, 1f));
                    if (weight > _bestAnomalyWeight)
                    {
                        _bestAnomalyWeight = weight;
                        NearestAnomaly = anomaly;
                    }
                    continue;
                }

                if (!IsPickup(obj.Kind)) continue;

                bool rare = IsRare(obj);
                bool energy = obj.Kind == ObjectKind.Energy ||
                              (obj is Drop && ((Drop)obj).Resource == ResourceKind.Energy);
                float baseWeight = WeightOf(obj);
                float weightForRank = baseWeight * proximity * (0.65f + Math.Max(1f, obj.Value) / 28f);
                if (energy && weightForRank > _bestEnergyWeight)
                {
                    _bestEnergyWeight = weightForRank;
                    NearestEnergy = obj;
                }
                if (rare)
                {
                    if (weightForRank > _bestRareWeight)
                    {
                        _bestRareWeight = weightForRank;
                        NearestRare = obj;
                    }
                }
                else if (weightForRank > _bestResourceWeight)
                {
                    _bestResourceWeight = weightForRank;
                    NearestResource = obj;
                }
                ResourceScore += weightForRank * (rare ? 0.85f : 0.42f) * (0.7f + greed);
            }

            List<Ship> ships = match.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship other = ships[i];
                if (other == null || other == self || other.State != ShipState.Alive) continue;
                float dist = Vec2.Distance(self.Position, other.Position);
                if (dist > radius) continue;
                NearbyShips.Add(other);
                float proximity = 1f / (1f + dist / 240f);
                float ratio = Math.Max(0.01f, other.Mass / myMass);

                if (other.IsBoss)
                {
                    if (ratio > _bestBossWeight)
                    {
                        _bestBossWeight = ratio;
                        NearestBoss = other;
                    }
                    ThreatScore += ratio * proximity * 2.4f;
                    ResourceScore += proximity * 0.8f;
                    continue;
                }

                if (ratio >= 0.94f)
                {
                    float weight = ratio * proximity * (other.IsPlayer ? 1.25f : 1f);
                    if (weight > _bestThreatWeight)
                    {
                        _bestThreatWeight = weight;
                        NearestThreat = other;
                        PredictedThreatPoint = Predict(self.Position, other, predictHorizon);
                    }
                    ThreatScore += weight * (1f + hpFactor * 0.9f) * (1f - aggression * 0.25f);
                }
                else if (ratio <= 0.86f)
                {
                    float weight = proximity / Math.Max(0.18f, ratio);
                    if (weight > _bestPreyWeight)
                    {
                        _bestPreyWeight = weight;
                        NearestPrey = other;
                        PredictedPreyPoint = Predict(self.Position, other, predictHorizon);
                    }
                    ResourceScore += weight * aggression * 0.35f;
                }
            }

            // Large zones may have their centre outside the query radius but still overlap the bot.
            if (NearestHazard == null && arena.Hazards != null)
            {
                for (int i = 0; i < arena.Hazards.Count; i++)
                {
                    HazardZone zone = arena.Hazards[i];
                    if (zone == null || !zone.Alive) continue;
                    float reach = zone.Radius + radius + self.Radius;
                    if (Vec2.DistanceSqr(self.Position, zone.Position) > reach * reach) continue;
                    NearestHazard = zone;
                    ThreatScore += 1.2f + zone.DamagePerSecond / 20f;
                    break;
                }
            }
            if (NearestAnomaly == null && arena.Anomalies != null)
            {
                for (int i = 0; i < arena.Anomalies.Count; i++)
                {
                    Anomaly anomaly = arena.Anomalies[i];
                    if (anomaly == null || !anomaly.Alive) continue;
                    float reach = anomaly.Radius + radius + self.Radius;
                    if (Vec2.DistanceSqr(self.Position, anomaly.Position) > reach * reach) continue;
                    NearestAnomaly = anomaly;
                    break;
                }
            }

            ScanSnakes(self, match, myMass, brain);

            // The arena border is a permanent threat for every bot.
            float border = arena.DangerAt(self.Position);
            if (border > 0f)
            {
                ThreatScore += border * 1.8f;
                if (border > 0.55f) ThreatScore += 1.5f;
            }
            float safeRadius = Math.Max(1f, arena.SafeRadius);
            float outside = Vec2.Distance(self.Position, Vec2.Zero) - (safeRadius - self.Radius - 180f);
            if (outside > 0f) ThreatScore += MathGalaxy.Clamp(outside / 140f, 0f, 3f);

            // A wounded bot should value healing drops and safer space more than farming.
            ThreatScore += hpFactor * 2.2f;
            ResourceScore *= (1f + MathGalaxy.Clamp(80f / (self.Mass + 80f), 0f, 0.7f));
            ThreatScore = MathGalaxy.Clamp(ThreatScore, 0f, 12f);
            ResourceScore = MathGalaxy.Clamp(ResourceScore, 0f, 12f);
        }

        /// <summary>
        /// Samples nearby body segments through the match's spatial hash. One narrow query per
        /// think, no all-to-all segment scan: a snake body is lethal on contact even though its
        /// segments are not separate ships.
        /// </summary>
        private void ScanSnakes(Ship self, MatchSimulation match, float myMass, BotBrain brain)
        {
            if (match == null || match.Snakes == null || self == null || self.Body == null) return;
            _segments.Clear();
            Vec2 from = self.Position - self.Velocity * 0.15f;
            Vec2 to = self.Position + self.Velocity * 0.35f;
            float reach = self.Radius + 110f;
            match.Snakes.QuerySegments(from, to, reach, _segments);
            float best = 0f;
            float caution = brain == null || brain.Tuning == null ? 1f : brain.Tuning.Caution;
            for (int i = 0; i < _segments.Count; i++)
            {
                SnakeSegmentRef segment = _segments[i];
                if (segment.Owner == null || segment.Owner == self ||
                    segment.Owner.State != ShipState.Alive) continue;
                float dist = Vec2.Distance(self.Position, segment.Point);
                float influence = reach + segment.Radius;
                if (dist > influence) continue;
                float massRatio = MathGalaxy.Clamp(segment.Owner.Mass / Math.Max(1f, myMass), 0.5f, 3f);
                float weight = (1f - dist / influence) * massRatio * (0.6f + caution * 0.7f);
                SnakeThreatScore += weight;
                if (weight > best)
                {
                    best = weight;
                    NearestSnakeThreat = segment.Owner;
                }
            }
            // Kept separate from general danger: bodies are avoided by steering, and only a very
            // close/pressing snake should justify abandoning a resource goal entirely.
            SnakeThreatScore = MathGalaxy.Clamp(SnakeThreatScore, 0f, 6f);
        }

        /// <summary>
        /// Constant-velocity intercept estimate capped by a skill-dependent horizon. Higher skill
        /// looks further ahead; the result is always clamped by the same arena rules as the target.
        /// </summary>
        public static Vec2 Predict(Vec2 self, Ship other, float horizon)
        {
            if (other == null) return self;
            float distance = Vec2.Distance(self, other.Position);
            float speed = Math.Max(80f, other.Speed);
            float time = MathGalaxy.Clamp(distance / speed, 0f, Math.Max(0.1f, horizon));
            return other.Position + other.Velocity * time;
        }

        /// <summary>True when the object can be absorbed as a resource pickup.</summary>
        public static bool IsPickup(ObjectKind kind)
        {
            return kind == ObjectKind.Energy || kind == ObjectKind.Crystal ||
                   kind == ObjectKind.DarkMatter || kind == ObjectKind.Artifact ||
                   kind == ObjectKind.Drop;
        }

        /// <summary>True for rare resources that justify a dedicated CollectRare goal.</summary>
        public static bool IsRare(WorldObject obj)
        {
            if (obj == null) return false;
            Drop drop = obj as Drop;
            if (drop != null) return drop.Resource == ResourceKind.Crystal ||
                                     drop.Resource == ResourceKind.DarkMatter ||
                                     drop.Resource == ResourceKind.Artifact;
            return obj.Kind == ObjectKind.Crystal || obj.Kind == ObjectKind.DarkMatter ||
                   obj.Kind == ObjectKind.Artifact;
        }

        /// <summary>Approximate growth value used by utility scoring.</summary>
        public static float WeightOf(WorldObject obj)
        {
            if (obj == null) return 0f;
            Drop drop = obj as Drop;
            ResourceKind kind;
            if (drop != null) kind = drop.Resource;
            else
            {
                switch (obj.Kind)
                {
                    case ObjectKind.Energy: kind = ResourceKind.Energy; break;
                    case ObjectKind.Crystal: kind = ResourceKind.Crystal; break;
                    case ObjectKind.DarkMatter: kind = ResourceKind.DarkMatter; break;
                    case ObjectKind.Artifact: kind = ResourceKind.Artifact; break;
                    default: return 0f;
                }
            }
            switch (kind)
            {
                case ResourceKind.Artifact: return 3.4f;
                case ResourceKind.DarkMatter: return 2.2f;
                case ResourceKind.Crystal: return 1.5f;
                default: return 0.8f;
            }
        }
    }
}
