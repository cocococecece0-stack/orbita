using System;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Smooth trailing body of one snake. The head remains the authoritative Ship position; the
    /// body is a path sampled at a fixed arc-length interval. Because samples are inserted by
    /// travelled distance (not by frame), the tail follows the exact same curve at 30/60/144 FPS
    /// without popping or changing shape.
    /// </summary>
    public sealed class SnakeBody
    {
        /// <summary>Set by SnakeSystem so growth upgrades can extend the visible body.</summary>
        internal Ship Owner;

        private readonly Vec2[] _points = new Vec2[SnakeConfig.MaxSegments + 2];
        private Vec2 _lastSample;
        private Vec2 _lastHead;
        private float _growth;
        private int _count;

        public int Count { get { return _count; } }

        /// <summary>Head position at the previous simulation step, used for swept collision tests.</summary>
        public Vec2 PreviousHead { get; private set; }

        public Vec2 GetPoint(int index)
        {
            return index >= 0 && index < _count ? _points[index] : Vec2.Zero;
        }

        public float GetRadius(int index, float headRadius)
        {
            if (index < 0 || index >= _count) return 0f;
            float t = _count > 1 ? index / (float)(_count - 1) : 0f;
            float factor = 1f - (1f - SnakeConfig.TailRadiusFactor) * t;
            return Math.Max(SnakeConfig.MinSegmentRadius, headRadius * factor * 0.86f);
        }

        public void Reset(Vec2 head, Vec2 facing, float radius, float mass)
        {
            _count = Math.Min(Math.Max(SnakeConfig.InitialSegments, GetTargetCount(mass)), SnakeConfig.MaxSegments);
            _growth = _count;
            _lastHead = head;
            PreviousHead = head;

            float spacing = SnakeConfig.SpacingForRadius(radius);
            Vec2 direction = facing.SqrMagnitude > 0.0001f ? facing : Vec2.Up;
            for (int i = 0; i < _count; i++)
                _points[i] = head - direction * (spacing * (i + 1));
            // The newest sample must be point 0, not the head, otherwise the first insertion
            // would create a double-sized gap between the new point and the old tail.
            _lastSample = _points[0];
        }

        /// <summary>
        /// Samples the head path by distance and eases the visible segment count toward the count
        /// implied by the current mass. No allocations, stable at any step size.
        /// </summary>
        public void Update(Vec2 head, float radius, float mass, float dt)
        {
            PreviousHead = _lastHead;
            _lastHead = head;

            float spacing = SnakeConfig.SpacingForRadius(radius);

            float target = GetTargetCount(mass);
            if (_growth < target) _growth = Math.Min(target, _growth + SnakeConfig.GrowthPerSecond * dt);
            else if (_growth > target) _growth = Math.Max(target, _growth - SnakeConfig.ShrinkPerSecond * dt);

            int targetCount = Math.Max(1, (int)_growth);
            if (_count > targetCount) _count = targetCount;

            Vec2 toHead = head - _lastSample;
            float distance = toHead.Magnitude;
            int guard = 0;
            while (distance >= spacing && guard++ < SnakeConfig.MaxSegments)
            {
                _lastSample = _lastSample + toHead * (spacing / distance);
                PushFront(_lastSample, targetCount);
                toHead = head - _lastSample;
                distance = toHead.Magnitude;
            }

            while (_count < targetCount) ExtendTail(head, spacing);
        }

        /// <summary>Includes match-scoped length upgrades; the hard segment cap is still authoritative.</summary>
        private int GetTargetCount(float mass)
        {
            int bonus = 0;
            if (Owner != null) bonus = Math.Max(0, Owner.SnakeLengthBonus);
            return Math.Min(SnakeConfig.MaxSegments, SnakeConfig.SegmentsForMass(mass) + bonus);
        }

        private void PushFront(Vec2 sample, int maxCount)
        {
            int limit = Math.Min(maxCount, SnakeConfig.MaxSegments);
            int keep = Math.Min(_count, limit - 1);
            for (int i = keep; i > 0; i--) _points[i] = _points[i - 1];
            _points[0] = sample;
            if (_count < limit) _count++;
        }

        private void ExtendTail(Vec2 head, float spacing)
        {
            Vec2 tail = _count > 0 ? _points[_count - 1] : _lastSample;
            Vec2 before = _count > 1 ? _points[_count - 2] : head;
            Vec2 direction = tail - before;
            if (direction.SqrMagnitude < 0.001f) direction = tail - head;
            if (direction.SqrMagnitude < 0.001f) direction = Vec2.Up;
            _points[_count] = tail + direction.Normalized * spacing;
            _count++;
        }
    }
}
