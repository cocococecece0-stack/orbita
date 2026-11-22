using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>One body sample with its owner, for the snake-only broadphase.</summary>
    public struct SnakeSegmentRef
    {
        public Ship Owner;
        public int Index;
        public Vec2 Point;
        public float Radius;
    }

    /// <summary>
    /// Uniform grid over body samples. Only rebuilt once per step after every tail has moved, so
    /// a head test touches a handful of nearby cells instead of every segment in the match.
    /// </summary>
    public sealed class SnakeSegmentIndex
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;
        private readonly Dictionary<long, List<SnakeSegmentRef>> _buckets =
            new Dictionary<long, List<SnakeSegmentRef>>(256);
        private readonly Stack<List<SnakeSegmentRef>> _bucketPool = new Stack<List<SnakeSegmentRef>>(64);
        private readonly List<long> _usedKeys = new List<long>(256);

        public SnakeSegmentIndex(float cellSize)
        {
            _cellSize = cellSize > 1f ? cellSize : 1f;
            _invCellSize = 1f / _cellSize;
        }

        public void Rebuild(List<Ship> snakes)
        {
            for (int i = 0; i < _usedKeys.Count; i++)
            {
                List<SnakeSegmentRef> bucket;
                if (_buckets.TryGetValue(_usedKeys[i], out bucket))
                {
                    bucket.Clear();
                    _bucketPool.Push(bucket);
                }
            }
            _buckets.Clear();
            _usedKeys.Clear();
            if (snakes == null) return;

            for (int i = 0; i < snakes.Count; i++)
            {
                Ship ship = snakes[i];
                if (ship == null || ship.Body == null || ship.State != ShipState.Alive) continue;
                SnakeBody body = ship.Body;
                for (int s = 0; s < body.Count; s++)
                {
                    SnakeSegmentRef segment = new SnakeSegmentRef
                    {
                        Owner = ship,
                        Index = s,
                        Point = body.GetPoint(s),
                        Radius = body.GetRadius(s, Math.Min(ship.Radius, SnakeConfig.MaxCollisionRadius))
                    };
                    int cx, cy;
                    CellOf(segment.Point, out cx, out cy);
                    long key = CellKey(cx, cy);
                    List<SnakeSegmentRef> bucket;
                    if (!_buckets.TryGetValue(key, out bucket))
                    {
                        bucket = _bucketPool.Count > 0 ? _bucketPool.Pop() : new List<SnakeSegmentRef>(8);
                        _buckets.Add(key, bucket);
                        _usedKeys.Add(key);
                    }
                    bucket.Add(segment);
                }
            }
        }

        /// <summary>
        /// Adds every segment whose sampled point lies within reach of the swept head path. The
        /// cell range is widened by the largest possible segment radius before the narrow test,
        /// so a fat tail just outside the head's reach is still checked and cannot cause a
        /// "missed collision" death.
        /// </summary>
        public void QuerySwept(Vec2 from, Vec2 to, float reach, List<SnakeSegmentRef> output)
        {
            if (output == null || reach < 0f) return;
            float broadReach = reach + SnakeConfig.MaxCollisionRadius;
            float minX = Math.Min(from.X, to.X) - broadReach;
            float maxX = Math.Max(from.X, to.X) + broadReach;
            float minY = Math.Min(from.Y, to.Y) - broadReach;
            float maxY = Math.Max(from.Y, to.Y) + broadReach;
            int minCx, minCy, maxCx, maxCy;
            CellOf(new Vec2(minX, minY), out minCx, out minCy);
            CellOf(new Vec2(maxX, maxY), out maxCx, out maxCy);

            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cy = minCy; cy <= maxCy; cy++)
                {
                    List<SnakeSegmentRef> bucket;
                    if (!_buckets.TryGetValue(CellKey(cx, cy), out bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        SnakeSegmentRef segment = bucket[i];
                        float combined = reach + segment.Radius;
                        if (SegmentDistanceSqr(from, to, segment.Point) <= combined * combined)
                            output.Add(segment);
                    }
                }
            }
        }

        private void CellOf(Vec2 point, out int cx, out int cy)
        {
            cx = (int)Math.Floor(point.X * _invCellSize);
            cy = (int)Math.Floor(point.Y * _invCellSize);
        }

        /// <summary>
        /// Injective for the arena's coordinate range. The old XOR hash could alias two visited
        /// cells to one bucket and report the same segment twice, which double-counted avoidance
        /// and made the "no duplicates" invariant false.
        /// </summary>
        private static long CellKey(int cx, int cy)
        {
            return unchecked((long)(uint)cx << 32 | (uint)cy);
        }

        private static float SegmentDistanceSqr(Vec2 a, Vec2 b, Vec2 point)
        {
            Vec2 ab = b - a;
            float lengthSqr = ab.SqrMagnitude;
            float t = lengthSqr > 0.000001f
                ? MathGalaxy.Clamp(Vec2.Dot(point - a, ab) / lengthSqr, 0f, 1f)
                : 0f;
            return Vec2.DistanceSqr(a + ab * t, point);
        }
    }

    /// <summary>
    /// Head-versus-body deaths. Head-to-head contact stays with the existing circle contact code,
    /// which already handles mass absorption and predictable separation. Lethal hits are gathered
    /// first and applied in three phases, so two snakes that hit each other's bodies on the same
    /// step both die and both get credit instead of the earlier list entry winning by accident.
    /// Every death is routed through <see cref="CombatSystem.HandleKill"/> so kills, streaks and
    /// the leaderboard stay consistent with every other death source.
    /// </summary>
    public sealed class SnakeCollisionSystem
    {
        private const float HeadReachFactor = 0.82f;

        private struct PendingBodyHit
        {
            public Ship Head;
            public Ship Owner;
            public float VictimMass;
            public float Reward;
        }

        private readonly SnakeSegmentIndex _index = new SnakeSegmentIndex(SnakeConfig.SegmentCellSize);
        private readonly List<SnakeSegmentRef> _hits = new List<SnakeSegmentRef>(32);
        private readonly List<PendingBodyHit> _pending = new List<PendingBodyHit>(16);
        private readonly Rng _rng;

        public SnakeCollisionSystem(Rng rng)
        {
            _rng = rng;
        }

        /// <summary>
        /// Read-only narrow query over the index built on the previous step. AI uses it for body
        /// avoidance; because the grid is already built, this adds no rebuild or all-to-all scan.
        /// </summary>
        public void QuerySwept(Vec2 from, Vec2 to, float reach, List<SnakeSegmentRef> output)
        {
            _index.QuerySwept(from, to, reach, output);
        }

        public void Resolve(List<Ship> snakes, Arena arena, SimLog log, float time)
        {
            if (snakes == null || snakes.Count == 0) return;
            _index.Rebuild(snakes);
            _pending.Clear();

            // Phase 1: one lethal hit per head, gathered before any state changes.
            for (int i = 0; i < snakes.Count; i++)
            {
                Ship head = snakes[i];
                if (head == null || head.Body == null || head.State != ShipState.Alive ||
                    head.Hp <= 0f || head.Invulnerability > 0f) continue;

                float reach = Math.Max(4f,
                    Math.Min(head.Radius, SnakeConfig.MaxCollisionRadius) * HeadReachFactor);
                _hits.Clear();
                _index.QuerySwept(head.Body.PreviousHead, head.Position, reach, _hits);
                if (_hits.Count == 0) continue;

                for (int h = 0; h < _hits.Count; h++)
                {
                    Ship owner = _hits[h].Owner;
                    if (!IsLethalOwner(owner, head)) continue;
                    PendingBodyHit hit = new PendingBodyHit
                    {
                        Head = head,
                        Owner = owner,
                        VictimMass = head.Mass,
                        Reward = head.Mass * SnakeConfig.BodyKillMassTransfer
                    };
                    _pending.Add(hit);
                    break;
                }
            }
            if (_pending.Count == 0) return;

            // Phase 2: pay rewards and spawn drops from the captured mass. A snake that is itself
            // dying in this step still owns its body hit, and still leaves the same-size pile.
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingBodyHit hit = _pending[i];
                if (hit.Reward > 0f) hit.Owner.AddMass(hit.Reward, log, time);
                DeathDropSystem.Spawn(arena, hit.Head, log, time, _rng, hit.VictimMass);
            }

            // Phase 3: register the deaths. killerMayBeDying lets mutual hits credit both sides.
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingBodyHit hit = _pending[i];
                if (hit.Head.State != ShipState.Alive) continue;
                CombatSystem.HandleKill(hit.Head, hit.Owner, log, time, true);
            }
        }

        /// <summary>
        /// Own body is intentionally harmless: bots re-steer several times a second and sampled
        /// tails can cut corners. A shield covers the whole snake, so an invulnerable owner's body
        /// is not a hidden lethal obstacle either. Other vulnerable bodies remain lethal.
        /// </summary>
        private static bool IsLethalOwner(Ship owner, Ship head)
        {
            return owner != null && owner != head && owner.State == ShipState.Alive &&
                   owner.Hp > 0f && owner.Invulnerability <= 0f;
        }
    }
}
