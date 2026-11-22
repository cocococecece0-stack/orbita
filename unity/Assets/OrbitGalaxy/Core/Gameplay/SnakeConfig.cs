namespace Orbit.Galaxy
{
    /// <summary>
    /// Balance constants for the cosmic snake body. Kept in Gameplay instead of GameConfig.cs so
    /// the protected foundation files do not have to change.
    /// </summary>
    public static class SnakeConfig
    {
        /// <summary>Maximum number of body segments drawn/simulated per participant.</summary>
        public const int MaxSegments = 48;

        /// <summary>Segments a newly spawned snake starts with.</summary>
        public const int InitialSegments = 5;

        /// <summary>
        /// Segment spacing is derived from the base radius and stays constant as the snake grows.
        /// Segments overlap heavily at high mass, which produces a smooth continuous body and —
        /// unlike radius-linked spacing — never tears old samples apart after a level-up.
        /// </summary>
        public const float SpacingFactor = 0.78f;

        /// <summary>Absolute minimum spacing, needed for very small/large heads.</summary>
        public const float MinSpacing = 11f;

        /// <summary>Extra segments per square root of mass relative to the base mass.</summary>
        public const float SegmentsPerMassRoot = 3.1f;

        /// <summary>How quickly the body visually catches up with a bigger mass.</summary>
        public const float GrowthPerSecond = 7f;

        /// <summary>How quickly a shrinking snake sheds segments.</summary>
        public const float ShrinkPerSecond = 4f;

        /// <summary>Smallest possible segment radius.</summary>
        public const float MinSegmentRadius = 4f;

        /// <summary>Tail taper: the last segment is this fraction of the head radius.</summary>
        public const float TailRadiusFactor = 0.48f;

        /// <summary>
        /// Body segments this close to a snake's own head are never lethal to another head.
        /// Prevents accidental deaths when two heads pass each other.
        /// </summary>
        public const int IgnoreLeadingSegments = 4;

        /// <summary>Cell size for the dedicated segment broadphase.</summary>
        public const float SegmentCellSize = 96f;

        /// <summary>Fraction of victim mass transferred to the killer on a body hit.</summary>
        public const float BodyKillMassTransfer = 0.55f;

        /// <summary>Number of energy drops spawned when a snake dies.</summary>
        public const int DeathDrops = 5;

        /// <summary>Lifetime of freshly spawned drops.</summary>
        public const float DropLifetime = 22f;

        public static int SegmentsForMass(float mass)
        {
            float root = (float)System.Math.Sqrt(System.Math.Max(1f, mass) / GameConfig.BaseMass);
            int target = InitialSegments + (int)(System.Math.Max(0f, root - 1f) * SegmentsPerMassRoot);
            return MathGalaxy.Clamp(target, InitialSegments, MaxSegments);
        }

        public static float SpacingForRadius(float radius)
        {
            return System.Math.Max(MinSpacing, GameConfig.BaseRadius * SpacingFactor);
        }
    }
}
