using System;
using System.Collections.Generic;

namespace Orbit.Core
{
    public struct BlockStep
    {
        public int Cleared;
        public int Dropped;
        public bool Locked;
        public bool ToppedOut;
    }

    /// <summary>
    /// Deterministic 10x18 tetromino arena, ported from native BlockArena.kt.
    /// Uses the engine's RNG stream via a float source, so the 7-bag and the orbit
    /// simulation share one deterministic seed.
    /// </summary>
    public sealed class BlockArena
    {
        public const int Cols = 10;
        public const int Rows = 18;
        private const int Types = 7;

        public readonly int[] Grid = new int[Cols * Rows];
        public readonly bool[] ClearRows = new bool[Rows];
        public readonly List<int> Next = new List<int>();

        public int Piece;
        public int Rotation;
        public int X;
        public int Y;
        public int Held = -1;
        public bool CanHold = true;
        public int Lines;
        public int Level = 1;
        public float ClearFlash;
        public bool TopOut;

        private float _fallTimer;
        private Func<float> _random = delegate { return 0.5f; };
        private Rng _rng = new Rng(1);
        private readonly List<int> _bag = new List<int>();
        private readonly int[][][] _shapes = new int[Types][][];

        private static readonly int[] BaseShapes =
        {
            0,0,0,0, 1,1,1,1, 0,0,0,0, 0,0,0,0,
            0,0,0,0, 0,1,1,0, 0,1,1,0, 0,0,0,0,
            0,1,0,0, 1,1,1,0, 0,0,0,0, 0,0,0,0,
            0,0,0,0, 0,1,1,0, 1,1,0,0, 0,0,0,0,
            0,0,0,0, 1,1,0,0, 0,1,1,0, 0,0,0,0,
            0,0,0,0, 1,0,0,0, 1,1,1,0, 0,0,0,0,
            0,0,0,0, 0,0,1,0, 1,1,1,0, 0,0,0,0
        };

        public BlockArena()
        {
            for (int type = 0; type < Types; type++)
            {
                _shapes[type] = new int[4][];
                int[] current = new int[16];
                for (int i = 0; i < 16; i++) current[i] = BaseShapes[type * 16 + i];
                for (int r = 0; r < 4; r++)
                {
                    _shapes[type][r] = (int[])current.Clone();
                    int[] turned = new int[16];
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            turned[col * 4 + (3 - row)] = current[row * 4 + col];
                    current = turned;
                }
            }
        }

        /// <summary>Compatibility overload: uses the supplied RNG as the float source.</summary>
        public void Reset(Rng rng)
        {
            _rng = rng ?? new Rng(1);
            Reset(new Func<float>(_rng.NextFloat));
        }

        /// <summary>Native reset(randomSource): the block bag shares the engine's RNG stream.</summary>
        public void Reset(Func<float> randomSource)
        {
            _random = randomSource ?? delegate { return 0.5f; };
            for (int i = 0; i < Grid.Length; i++) Grid[i] = 0;
            for (int i = 0; i < Rows; i++) ClearRows[i] = false;
            _bag.Clear();
            Next.Clear();
            Piece = 0;
            Rotation = 0;
            X = 0;
            Y = 0;
            Held = -1;
            CanHold = true;
            Lines = 0;
            Level = 1;
            ClearFlash = 0f;
            TopOut = false;
            _fallTimer = 0f;
            for (int i = 0; i < 4; i++) Next.Add(NextType());
            Spawn();
        }

        public int CellAtGrid(int col, int row)
        {
            if (col < 0 || col >= Cols || row < 0 || row >= Rows) return 0;
            return Grid[row * Cols + col];
        }

        /// <summary>
        /// Raw 4x4 shape cell (0/1) for previews, exactly as the previous Unity API exposed.
        /// Placement uses the normalized Cells(type, rotation) list.
        /// </summary>
        public int CellAt(int piece, int rotation, int index)
        {
            if (piece < 0 || piece >= Types) return 0;
            if (index < 0 || index > 15) return 0;
            return _shapes[piece][((rotation % 4) + 4) % 4][index];
        }

        /// <summary>Native cells(type, rot): normalized (x, y) pairs, clockwise rotation.</summary>
        public List<int[]> Cells(int type, int rotation)
        {
            int t = Clamp(type, 0, Types - 1);
            int rot = ((rotation % 4) + 4) % 4;
            int[] current = new int[16];
            for (int i = 0; i < 16; i++) current[i] = BaseShapes[t * 16 + i];
            if (t != 1)
            {
                for (int r = 0; r < rot; r++)
                {
                    int[] turned = new int[16];
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 4; x++)
                            if (current[y * 4 + x] != 0)
                                turned[x * 4 + (3 - y)] = current[y * 4 + x];
                    current = turned;
                }
            }

            int minX = 4;
            int minY = 4;
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    if (current[y * 4 + x] != 0)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                    }

            var result = new List<int[]>();
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    if (current[y * 4 + x] != 0)
                        result.Add(new[] { x - minX, y - minY });
            return result;
        }

        public bool CanPlace(int type, int rotation, int px, int py)
        {
            List<int[]> cells = Cells(type, rotation);
            for (int i = 0; i < cells.Count; i++)
            {
                int cx = px + cells[i][0];
                int cy = py + cells[i][1];
                if (cx < 0 || cx >= Cols || cy >= Rows) return false;
                if (cy >= 0 && Grid[cy * Cols + cx] != 0) return false;
            }
            return true;
        }

        public int GhostY()
        {
            int gy = Y;
            while (CanPlace(Piece, Rotation, X, gy + 1)) gy++;
            return gy;
        }

        public bool Move(int direction)
        {
            if (TopOut || direction == 0) return false;
            if (!CanPlace(Piece, Rotation, X + direction, Y)) return false;
            X += direction;
            return true;
        }

        public bool Rotate()
        {
            if (TopOut) return false;
            int nextRotation = (Rotation + 1) % 4;
            int[][] kicks =
            {
                new[] { 0, 0 }, new[] { -1, 0 }, new[] { 1, 0 },
                new[] { -2, 0 }, new[] { 2, 0 }, new[] { 0, -1 }
            };
            for (int i = 0; i < kicks.Length; i++)
            {
                int nx = X + kicks[i][0];
                int ny = Y + kicks[i][1];
                if (CanPlace(Piece, nextRotation, nx, ny))
                {
                    Rotation = nextRotation;
                    X = nx;
                    Y = ny;
                    return true;
                }
            }
            return false;
        }

        public BlockStep SoftDrop()
        {
            if (TopOut) return default;
            if (CanPlace(Piece, Rotation, X, Y + 1))
            {
                Y++;
                _fallTimer = 0f;
                return default;
            }
            return Lock(0);
        }

        public BlockStep HardDrop()
        {
            if (TopOut) return default;
            int distance = 0;
            while (CanPlace(Piece, Rotation, X, Y + 1))
            {
                Y++;
                distance++;
            }
            return Lock(distance);
        }

        /// <summary>Compatibility: native hold returns an empty step, so the void form remains.</summary>
        public void Hold()
        {
            HoldStep();
        }

        public BlockStep HoldStep()
        {
            if (TopOut || !CanHold) return default;
            int current = Piece;
            if (Held < 0)
            {
                Held = current;
                Spawn();
            }
            else
            {
                Piece = Held;
                Held = current;
                PlaceAtSpawn();
            }
            CanHold = false;
            return default;
        }

        public BlockStep Tick(float dt)
        {
            ClearFlash = Math.Max(0f, ClearFlash - dt);
            if (TopOut) return default;
            _fallTimer += dt;
            float interval = Math.Max(0.075f, 0.78f * (float)Math.Pow(0.84, Level - 1));
            while (_fallTimer >= interval)
            {
                _fallTimer -= interval;
                if (CanPlace(Piece, Rotation, X, Y + 1)) Y++;
                else return Lock(0);
            }
            return default;
        }

        private BlockStep Lock(int dropped)
        {
            bool overflow = false;
            List<int[]> cells = Cells(Piece, Rotation);
            for (int i = 0; i < cells.Count; i++)
            {
                int cx = X + cells[i][0];
                int cy = Y + cells[i][1];
                if (cy < 0) overflow = true;
                else if (cx >= 0 && cx < Cols && cy >= 0 && cy < Rows)
                    Grid[cy * Cols + cx] = Piece + 1;
            }

            for (int i = 0; i < Rows; i++) ClearRows[i] = false;
            int cleared = 0;
            for (int row = Rows - 1; row >= 0; row--)
            {
                bool full = true;
                for (int col = 0; col < Cols; col++)
                    if (Grid[row * Cols + col] == 0) { full = false; break; }
                if (!full) continue;
                ClearRows[row] = true;
                cleared++;
            }

            if (cleared > 0)
            {
                int write = Rows - 1;
                for (int read = Rows - 1; read >= 0; read--)
                {
                    if (ClearRows[read]) continue;
                    if (write != read)
                        for (int col = 0; col < Cols; col++)
                            Grid[write * Cols + col] = Grid[read * Cols + col];
                    write--;
                }
                while (write >= 0)
                {
                    for (int col = 0; col < Cols; col++) Grid[write * Cols + col] = 0;
                    write--;
                }
                Lines += cleared;
                Level = Math.Min(20, 1 + Lines / 8);
            }

            ClearFlash = cleared > 0 ? 0.42f : 0f;
            CanHold = true;
            Spawn();
            return new BlockStep
            {
                Cleared = cleared,
                Dropped = dropped,
                Locked = true,
                ToppedOut = overflow || TopOut
            };
        }

        private void Spawn()
        {
            Piece = Next[0];
            Next.RemoveAt(0);
            while (Next.Count < 4) Next.Add(NextType());
            PlaceAtSpawn();
        }

        private void PlaceAtSpawn()
        {
            Rotation = 0;
            X = 3;
            Y = 0;
            _fallTimer = 0f;
            if (!CanPlace(Piece, Rotation, X, Y)) TopOut = true;
        }

        private int NextType()
        {
            if (_bag.Count == 0)
            {
                for (int i = 0; i < Types; i++) _bag.Add(i);
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = (int)(_random() * (i + 1));
                    if (j < 0) j = 0;
                    if (j > i) j = i;
                    int tmp = _bag[i];
                    _bag[i] = _bag[j];
                    _bag[j] = tmp;
                }
            }
            int type = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            return type;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
