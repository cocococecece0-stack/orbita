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
        private readonly Dictionary<int, List<SnakeSegmentRef>> _buckets =
            new Dictionary<int, List<SnakeSegmentRef>>(256);
        private readonly Stack<List<SnakeSegmentRef>> _bucketPool = new Stack<List<SnakeSegmentRef>>(64);
        private readonly List<int> _usedKeys = new List<int>(256);

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
                    int key = CellKey(cx, cy);
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
        /// Adds every segment whose sampled point lies within reach of the swept head path.
        /// A segment lives in exactly one cell, so results never contain duplicates.
        /// </summary>
        public void QuerySwept(Vec2 from, Vec2 to, float reach, List<SnakeSegmentRef> output)
        {
            if (output == null || reach < 0f) return;
            float minX = Math.Min(from.X, to.X) - reach;
            float maxX = Math.Max(from.X, to.X) + reach;
            float minY = Math.Min(from.Y, to.Y) - reach;
            float maxY = Math.Max(from.Y, to.Y) + reach;
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

        private static int CellKey(int cx, int cy)
        {
            return unchecked((cx * 73856093) ^ (cy * 19349663));
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
    /// which already handles mass absorption and predictable separation. Every lethal body hit is
    /// routed through <see cref="CombatSystem.HandleKill"/> so kills, streaks, rewards and the
    /// leaderboard stay consistent with every other death source.
    /// </summary>
    public sealed class SnakeCollisionSystem
    {
        private const float HeadReachFactor = 0.82f;

        private readonly SnakeSegmentIndex _index = new SnakeSegmentIndex(SnakeConfig.SegmentCellSize);
        private readonly List<SnakeSegmentRef> _hits = new List<SnakeSegmentRef>(32);
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
                    SnakeSegmentRef segment = _hits[h];
                    Ship owner = segment.Owner;
                    if (owner == null || owner.State != ShipState.Alive || owner.Hp <= 0f) continue;
                    // Own body is intentionally harmless. Bots re-steer several times a second and
                    // sampled tails can cut corners; treating those overlaps as deaths produced
                    // constant unfair suicides. Other snakes' bodies remain lethal.
                    if (owner == head) continue;

                    Ship killer = owner;
                    float reward = head.Mass * SnakeConfig.BodyKillMassTransfer;
                    if (reward > 0f) killer.AddMass(reward, log, time);
                    SpawnDrops(arena, head);
                    CombatSystem.HandleKill(head, killer, log, time);
                    break;
                }
            }
        }

        private void SpawnDrops(Arena arena, Ship victim)
        {
            if (arena == null) return;
            int value = Math.Max(1, (int)(victim.Mass / SnakeConfig.DeathDrops));
            for (int i = 0; i < SnakeConfig.DeathDrops; i++)
            {
                float angle = _rng.Range(0f, 6.2831853f);
                float radius = _rng.Range(6f, Math.Max(12f, victim.Radius));
                Vec2 position = arena.ClampToSafe(victim.Position + Vec2.FromAngle(angle) * radius);
                Drop drop = new Drop(arena.NextId(), position, ResourceKind.Energy,
                    value, SnakeConfig.DropLifetime);
                arena.Add(drop);
            }
        }
    }
}
