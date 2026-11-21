using System;
using System.Collections.Generic;

namespace Orbit.Galaxy
{
    /// <summary>
    /// Uniform grid broadphase. Pure C#, no Unity types. Buckets are recycled on removal so a
    /// long-running match does not allocate a new list every time an object crosses a cell border.
    /// </summary>
    public sealed class SpatialHash
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;
        private readonly Dictionary<int, List<WorldObject>> _buckets;
        private readonly Dictionary<WorldObject, int> _cells;
        private readonly Stack<List<WorldObject>> _bucketPool;

        public SpatialHash(float cellSize)
        {
            _cellSize = cellSize > 1f ? cellSize : 1f;
            _invCellSize = 1f / _cellSize;
            _buckets = new Dictionary<int, List<WorldObject>>(256);
            _cells = new Dictionary<WorldObject, int>(1024);
            _bucketPool = new Stack<List<WorldObject>>(64);
        }

        public float CellSize { get { return _cellSize; } }

        /// <summary>Number of objects currently indexed.</summary>
        public int Count { get { return _cells.Count; } }

        private static int CellKey(int cx, int cy)
        {
            return (cx & 0xFFFF) | ((cy & 0xFFFF) << 16);
        }

        private void CellOf(Vec2 p, out int cx, out int cy)
        {
            cx = (int)Math.Floor(p.X * _invCellSize);
            cy = (int)Math.Floor(p.Y * _invCellSize);
        }

        private List<WorldObject> RentBucket()
        {
            if (_bucketPool.Count > 0) return _bucketPool.Pop();
            return new List<WorldObject>(8);
        }

        private void ReturnBucket(List<WorldObject> bucket)
        {
            bucket.Clear();
            if (_bucketPool.Count < 128) _bucketPool.Push(bucket);
        }

        public void Insert(WorldObject o)
        {
            if (o == null) return;
            if (_cells.ContainsKey(o))
            {
                Update(o);
                return;
            }

            int cx, cy;
            CellOf(o.Position, out cx, out cy);
            int key = CellKey(cx, cy);
            List<WorldObject> bucket;
            if (!_buckets.TryGetValue(key, out bucket))
            {
                bucket = RentBucket();
                _buckets.Add(key, bucket);
            }
            bucket.Add(o);
            _cells.Add(o, key);
        }

        public void Remove(WorldObject o)
        {
            if (o == null) return;
            int key;
            if (!_cells.TryGetValue(o, out key)) return;
            _cells.Remove(o);

            List<WorldObject> bucket;
            if (!_buckets.TryGetValue(key, out bucket)) return;
            for (int i = 0; i < bucket.Count; i++)
            {
                if (ReferenceEquals(bucket[i], o))
                {
                    bucket.RemoveAt(i);
                    break;
                }
            }
            if (bucket.Count == 0)
            {
                _buckets.Remove(key);
                ReturnBucket(bucket);
            }
        }

        /// <summary>Reindexes an object after its Position or Radius changed.</summary>
        public void Update(WorldObject o)
        {
            if (o == null) return;

            int cx, cy;
            CellOf(o.Position, out cx, out cy);
            int key = CellKey(cx, cy);

            int oldKey;
            if (!_cells.TryGetValue(o, out oldKey))
            {
                Insert(o);
                return;
            }

            if (oldKey != key)
            {
                Remove(o);
                Insert(o);
                return;
            }

            List<WorldObject> bucket;
            if (_buckets.TryGetValue(key, out bucket))
            {
                for (int i = 0; i < bucket.Count; i++)
                    if (ReferenceEquals(bucket[i], o)) return;
                bucket.Add(o);
            }
            else
            {
                _buckets.Add(key, RentBucket());
                _buckets[key].Add(o);
            }
        }

        /// <summary>Adds every alive object whose centre lies within radius to output.</summary>
        public void Query(Vec2 center, float radius, List<WorldObject> output)
        {
            if (output == null || radius < 0f) return;

            int minX, minY, maxX, maxY;
            CellOf(new Vec2(center.X - radius, center.Y - radius), out minX, out minY);
            CellOf(new Vec2(center.X + radius, center.Y + radius), out maxX, out maxY);
            float radiusSqr = radius * radius;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    List<WorldObject> bucket;
                    if (!_buckets.TryGetValue(CellKey(cx, cy), out bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        WorldObject o = bucket[i];
                        if (o == null || !o.Alive) continue;
                        if (Vec2.DistanceSqr(center, o.Position) <= radiusSqr) output.Add(o);
                    }
                }
            }
        }

        public void Clear()
        {
            foreach (KeyValuePair<int, List<WorldObject>> pair in _buckets)
                ReturnBucket(pair.Value);
            _buckets.Clear();
            _cells.Clear();
        }
    }
}
