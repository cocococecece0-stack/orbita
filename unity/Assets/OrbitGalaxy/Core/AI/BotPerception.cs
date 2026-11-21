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
        public HazardZone NearestHazard;
        public Anomaly NearestAnomaly;
        public float ThreatScore;
        public float ResourceScore;
        public readonly List<WorldObject> NearbyObjects = new List<WorldObject>(64);
        public readonly List<Ship> NearbyShips = new List<Ship>(16);

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
            NearestHazard = null;
            NearestAnomaly = null;
            ThreatScore = 0f;
            ResourceScore = 0f;
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
                float baseWeight = WeightOf(obj);
                float weightForRank = baseWeight * proximity * (0.65f + Math.Max(1f, obj.Value) / 28f);
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
