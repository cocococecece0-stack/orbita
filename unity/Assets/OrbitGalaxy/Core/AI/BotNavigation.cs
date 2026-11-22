using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Steering for bots. It combines goal seeking with separation, obstacle slides, border
    /// avoidance and a small deterministic wander so ships never lock against geometry.
    /// </summary>
    public sealed class BotNavigation
    {
        private readonly List<WorldObject> _nearby = new List<WorldObject>(48);
        private readonly List<SnakeSegmentRef> _snakeSegments = new List<SnakeSegmentRef>(24);

        public Vec2 Steer(BotBrain brain, Ship self, Vec2 goal, MatchSimulation match)
        {
            if (self == null || match == null || match.Arena == null) return Vec2.Zero;
            Vec2 toGoal = goal - self.Position;
            if (toGoal.SqrMagnitude < 400f)
            {
                return self.Facing.SqrMagnitude > 0.01f ? self.Facing.Normalized : Vec2.Zero;
            }

            float distance = toGoal.Magnitude;
            Vec2 desired = toGoal / distance;
            BotProfile profile = brain == null ? self.Profile : brain.Profile;
            if (profile == null) profile = ContentDatabase.BotProfiles[1];

            float orbit = 0f;
            if (brain != null && (brain.Goal == BotGoal.Hunt || brain.Goal == BotGoal.AttackBoss))
            {
                if (distance < profile.PreferredRange * 0.85f)
                {
                    orbit = brain.OrbitSign * 0.85f * (1f - distance / Math.Max(1f, profile.PreferredRange));
                }
            }
            if (brain != null && brain.Goal == BotGoal.Flee && brain.TargetShip != null)
            {
                Vec2 away = self.Position - brain.TargetShip.Position;
                if (away.SqrMagnitude > 1f) desired = desired + away.Normalized * 0.85f;
            }

            if (orbit != 0f)
            {
                Vec2 side = desired.Perpendicular * orbit;
                desired = desired + side;
            }

            desired = desired + Separation(self, match) * 0.85f;
            desired = desired + AvoidObstacles(self, desired, match) * 1.25f;
            desired = desired + AvoidSnakeBodies(brain, self, match) * 1.4f;

            float safeRadius = Math.Max(10f, match.Arena.SafeRadius);
            float fromCenter = self.Position.Magnitude;
            float margin = safeRadius - fromCenter;
            if (margin < 260f && fromCenter > 1f)
            {
                float urgency = MathGalaxy.Clamp((260f - margin) / 190f, 0f, 1f);
                desired = desired + (self.Position / fromCenter) * (-1.6f * urgency);
            }

            // Gravity anomalies bias the path; shield anomalies are safe to approach.
            List<Anomaly> anomalies = match.Arena.Anomalies;
            if (anomalies != null)
            {
                for (int i = 0; i < anomalies.Count; i++)
                {
                    Anomaly anomaly = anomalies[i];
                    if (anomaly == null || !anomaly.Alive) continue;
                    Vec2 toAnomaly = anomaly.Position - self.Position;
                    float d2 = toAnomaly.SqrMagnitude;
                    float range = anomaly.Radius + 340f;
                    if (d2 > range * range || d2 < 1f) continue;
                    float d = (float)Math.Sqrt(d2);
                    float strength = MathGalaxy.Clamp(1f - d / range, 0f, 1f);
                    if (anomaly.GrantsShield) desired = desired + (toAnomaly / d) * (strength * 0.35f);
                    else desired = desired - (toAnomaly / d) * (strength * anomaly.Pull * 0.6f);
                }
            }

            if (brain != null)
            {
                if (brain.StuckTimer > 0.45f)
                {
                    Vec2 nudge = desired.Perpendicular * brain.StuckSign;
                    desired = desired + nudge * MathGalaxy.Clamp(brain.StuckTimer * 0.9f, 0f, 1.2f);
                }
                float variance = brain.Tuning == null ? 1f : brain.Tuning.RouteVariance;
                float wander = Math.Min(0.32f,
                    (profile.Risk * 0.12f + (brain.IsElite ? 0.02f : 0f)) * variance);
                if (wander > 0f)
                {
                    Rng rng = brain.Rng;
                    float angle = (rng.NextFloat() - 0.5f) * wander;
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);
                    desired = new Vec2(desired.X * cos - desired.Y * sin,
                                       desired.X * sin + desired.Y * cos);
                    brain.Rng = rng;
                }
            }

            float magnitude = desired.Magnitude;
            if (magnitude > 1f) desired = desired / magnitude;
            if (distance < 220f)
            {
                desired = desired * MathGalaxy.Clamp(distance / 220f, 0.35f, 1f);
            }
            return desired;
        }

        /// <summary>
        /// Repulsion from other snakes' sampled bodies. Only snake ships can die this way, and the
        /// query uses the match's existing segment grid, so this stays a handful of cell lookups.
        /// Cautious bots react more strongly; aggressives accept more risk when intercepting.
        /// </summary>
        private Vec2 AvoidSnakeBodies(BotBrain brain, Ship self, MatchSimulation match)
        {
            if (self == null || self.Body == null || match == null || match.Snakes == null) return Vec2.Zero;
            _snakeSegments.Clear();
            Vec2 from = self.Position - self.Velocity * 0.15f;
            Vec2 to = self.Position + self.Velocity * 0.35f;
            float reach = self.Radius + 110f;
            match.Snakes.QuerySegments(from, to, reach, _snakeSegments);
            if (_snakeSegments.Count == 0) return Vec2.Zero;

            float caution = brain == null || brain.Tuning == null ? 1f : brain.Tuning.Caution;
            float aggression = brain == null || brain.Tuning == null ? 1f : brain.Tuning.Aggression;
            Vec2 result = Vec2.Zero;
            for (int i = 0; i < _snakeSegments.Count; i++)
            {
                SnakeSegmentRef segment = _snakeSegments[i];
                if (segment.Owner == null || segment.Owner == self ||
                    segment.Owner.State != ShipState.Alive) continue;
                Vec2 away = self.Position - segment.Point;
                float influence = reach + segment.Radius;
                float distSqr = away.SqrMagnitude;
                if (distSqr > influence * influence) continue;
                float dist = (float)Math.Sqrt(distSqr);
                if (dist < 0.01f) away = Vec2.Up;
                else away = away / dist;
                float massRatio = MathGalaxy.Clamp(segment.Owner.Mass / Math.Max(1f, self.Mass), 0.5f, 2.5f);
                float strength = (1f - dist / influence) * massRatio * caution / (0.7f + aggression * 0.5f);
                result = result + away * strength;
                float sideSign = Vec2.Cross(to - from, away) >= 0f ? 1f : -1f;
                result = result + away.Perpendicular * (sideSign * strength * 0.35f);
            }
            float magnitude = result.Magnitude;
            if (magnitude > 2f) result = result / magnitude * 2f;
            return result;
        }

        /// <summary>
        /// Returns a repulsion vector from solid objects, danger zones and the arena edge.
        /// Steer() decides how strongly to apply it.
        /// </summary>
        public Vec2 AvoidObstacles(Ship self, Vec2 desired, MatchSimulation match)
        {
            if (self == null || match == null || match.Arena == null) return Vec2.Zero;
            Vec2 result = Vec2.Zero;
            _nearby.Clear();
            float range = self.Radius + 150f + self.Speed * 0.25f;
            match.Arena.QueryInto(self.Position, range, _nearby);
            bool phasing = AbilitySystem.IsActive(self, AbilityKind.Phase);

            for (int i = 0; i < _nearby.Count; i++)
            {
                WorldObject obj = _nearby[i];
                if (obj == null || !obj.Alive) continue;
                bool solid = obj is Asteroid;
                bool danger = !phasing && obj is HazardZone;
                if (!solid && !danger) continue;

                Vec2 away = self.Position - obj.Position;
                float influence = obj.Radius + self.Radius + (solid ? 110f : 70f);
                float distSqr = away.SqrMagnitude;
                if (distSqr > influence * influence) continue;
                float dist = (float)Math.Sqrt(distSqr);
                if (dist < 0.01f) away = desired.Perpendicular;
                else away = away / dist;

                float strength = (1f - dist / influence) * (solid ? 2.3f : 1.7f);
                result = result + away * strength;
                // Sliding instead of stopping keeps fast ships from sticking to asteroids.
                float sideSign = Vec2.Cross(desired, away) >= 0f ? 1f : -1f;
                result = result + away.Perpendicular * (sideSign * strength * 0.55f);
            }

            float safeRadius = Math.Max(10f, match.Arena.SafeRadius - self.Radius - 90f);
            float centerDist = self.Position.Magnitude;
            if (centerDist > safeRadius && centerDist > 1f)
            {
                float urgency = MathGalaxy.Clamp((centerDist - safeRadius) / 180f, 0f, 1f);
                result = result - (self.Position / centerDist) * (2.2f * urgency);
            }
            float magnitude = result.Magnitude;
            if (magnitude > 2.5f) result = result / magnitude * 2.5f;
            return result;
        }

        /// <summary>Repulsion from nearby ships, scaled by their relative mass.</summary>
        public Vec2 Separation(Ship self, MatchSimulation match)
        {
            if (self == null || match == null) return Vec2.Zero;
            Vec2 result = Vec2.Zero;
            List<Ship> ships = match.Ships;
            for (int i = 0; i < ships.Count; i++)
            {
                Ship other = ships[i];
                if (other == null || other == self || other.State != ShipState.Alive) continue;
                Vec2 away = self.Position - other.Position;
                float minDist = (self.Radius + other.Radius) * 2.15f + 34f;
                float distSqr = away.SqrMagnitude;
                if (distSqr > minDist * minDist || distSqr < 0.01f) continue;
                float dist = (float)Math.Sqrt(distSqr);
                float massFactor = MathGalaxy.Clamp(other.Mass / Math.Max(1f, self.Mass), 0.4f, 2.4f);
                result = result + (away / dist) * ((1f - dist / minDist) * massFactor);
            }
            float magnitude = result.Magnitude;
            if (magnitude > 1.6f) result = result / magnitude * 1.6f;
            return result;
        }
    }
}
