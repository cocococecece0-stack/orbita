using Orbit.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Orbit.Unity
{
    /// <summary>10x18 стакан: сетка, призрак, hold и next-5, вспышка очистки.</summary>
    public sealed class StackBoardView : MonoBehaviour
    {
        private const float Cell = 52f;
        private const int Cols = BlockArena.Cols;
        private const int Rows = BlockArena.Rows;

        private OrbitApp _app;
        private RectTransform _root;
        private Image[] _cells;
        private Image[] _holdCells;
        private Image[] _nextCells;
        private Image _clearOverlay;

        private static readonly Color EmptyColor = new Color32(17, 22, 42, 255);
        private static readonly Color GhostTint = new Color32(90, 100, 135, 255);

        private static readonly Color[] PieceColors =
        {
            UiFactory.Lime,
            UiFactory.Coral,
            UiFactory.Sun,
            UiFactory.Violet,
            UiFactory.Cyan,
            UiFactory.Orange,
            UiFactory.Pink
        };

        public void Initialize(OrbitApp app, Transform parent)
        {
            _app = app;
            if (_root != null) return;
            _root = UiFactory.Place(parent, "Стакан", UiBox.Full);
            Build();
        }

        private void Build()
        {
            UiFactory.Panel(_root, "Поле стакана", UiFactory.WithAlpha(UiFactory.Deep, 0.92f),
                UiBox.At(0f, 0f, Cols * Cell + 16f, Rows * Cell + 16f), UiFactory.RoundedSprite).type = Image.Type.Sliced;

            _cells = new Image[Cols * Rows];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    float cx = -Cols * Cell * 0.5f + col * Cell + Cell * 0.5f;
                    float cy = Rows * Cell * 0.5f - row * Cell - Cell * 0.5f;
                    _cells[row * Cols + col] = UiFactory.Panel(_root, "Клетка " + row + ":" + col, EmptyColor,
                        UiBox.At(cx, cy, Cell - 3f, Cell - 3f));
                }
            }

            _clearOverlay = UiFactory.Panel(_root, "Вспышка очистки", new Color(1f, 1f, 1f, 0f),
                UiBox.At(0f, 0f, Cols * Cell, Rows * Cell));

            BuildGutter("HoldPanel", "HOLD", -390f, 150f, 210f, 540f, 26f, out _holdCells);
            BuildNextGutter();
        }

        private void BuildGutter(string name, string title, float cx, float cy, float w, float h, float mini, out Image[] cells)
        {
            UiFactory.Panel(_root, name, UiFactory.WithAlpha(UiFactory.Slate, 0.88f),
                UiBox.At(cx, cy, w, h), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(_root, title, 26, UiFactory.Lime, TextAnchor.MiddleCenter,
                UiBox.At(cx, cy + h * 0.5f - 45f, w - 10f, 52f), FontStyle.Bold);

            cells = new Image[16];
            float gridSize = mini * 4f;
            for (int i = 0; i < 16; i++)
            {
                int r = i / 4;
                int c = i % 4;
                float px = cx - gridSize * 0.5f + c * mini + mini * 0.5f;
                float py = cy + 60f - gridSize * 0.5f + r * mini - mini * 0.5f;
                cells[i] = UiFactory.Panel(_root, name + " мини " + i, new Color(0f, 0f, 0f, 0.08f),
                    UiBox.At(px, py, mini - 2f, mini - 2f));
            }
        }

        private void BuildNextGutter()
        {
            const float cx = 390f;
            const float mini = 20f;
            UiFactory.Panel(_root, "NextPanel", UiFactory.WithAlpha(UiFactory.Slate, 0.88f),
                UiBox.At(cx, 150f, 210f, 700f), UiFactory.RoundedSprite).type = Image.Type.Sliced;
            UiFactory.TextLabel(_root, "NEXT", 26, UiFactory.Lime, TextAnchor.MiddleCenter,
                UiBox.At(cx, 480f, 200f, 52f), FontStyle.Bold);

            _nextCells = new Image[5 * 16];
            for (int piece = 0; piece < 5; piece++)
            {
                float baseY = 400f - piece * 128f;
                for (int i = 0; i < 16; i++)
                {
                    int r = i / 4;
                    int c = i % 4;
                    float px = cx - mini * 2f + c * mini + mini * 0.5f;
                    float py = baseY + mini * 1.5f - r * mini + mini * 0.5f;
                    _nextCells[piece * 16 + i] = UiFactory.Panel(_root, "Next " + piece + ":" + i,
                        new Color(0f, 0f, 0f, 0.08f), UiBox.At(px, py, mini - 2f, mini - 2f));
                }
            }
        }

        private static Color ColorForPiece(int piece)
        {
            if (piece < 0 || piece >= PieceColors.Length) return UiFactory.Paper;
            return PieceColors[piece];
        }

        private void Update()
        {
            if (_app == null || _app.Engine == null || _cells == null) return;
            BlockArena arena = _app.Engine.Blocks;
            if (arena == null) return;

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int value = arena.Grid[row * Cols + col];
                    Image cell = _cells[row * Cols + col];
                    cell.color = value > 0 ? ColorForPiece(value - 1) : EmptyColor;
                }
            }

            int ghostY = arena.GhostY();
            DrawGhost(arena, ghostY);
            DrawActive(arena);
            DrawHold(arena);
            DrawNext(arena);

            if (_clearOverlay != null)
                _clearOverlay.color = UiFactory.WithAlpha(UiFactory.Lime, arena.ClearFlash * 0.42f);
        }

        private void DrawGhost(BlockArena arena, int ghostY)
        {
            if (arena == null) return;
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if (arena.CellAt(arena.Piece, arena.Rotation, row * 4 + col) == 0) continue;
                    int gy = ghostY + row;
                    int gx = arena.X + col;
                    if (gy < 0 || gy >= Rows || gx < 0 || gx >= Cols) continue;
                    int index = gy * Cols + gx;
                    if (arena.Grid[index] != 0) continue;
                    Color baseColor = ColorForPiece(arena.Piece);
                    _cells[index].color = UiFactory.WithAlpha(
                        new Color((baseColor.r + GhostTint.r) * 0.5f, (baseColor.g + GhostTint.g) * 0.5f,
                            (baseColor.b + GhostTint.b) * 0.5f, 1f), 0.65f);
                }
            }
        }

        private void DrawActive(BlockArena arena)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if (arena.CellAt(arena.Piece, arena.Rotation, row * 4 + col) == 0) continue;
                    int gy = arena.Y + row;
                    int gx = arena.X + col;
                    if (gy < 0 || gy >= Rows || gx < 0 || gx >= Cols) continue;
                    _cells[gy * Cols + gx].color = ColorForPiece(arena.Piece);
                }
            }
        }

        private void DrawHold(BlockArena arena)
        {
            if (_holdCells == null) return;
            int piece = arena.Held;
            for (int i = 0; i < 16; i++)
            {
                int filled = piece < 0 ? 0 : arena.CellAt(piece, 0, i);
                _holdCells[i].color = filled != 0
                    ? ColorForPiece(piece)
                    : new Color(0f, 0f, 0f, 0.08f);
            }
        }

        private void DrawNext(BlockArena arena)
        {
            if (_nextCells == null) return;
            for (int piece = 0; piece < 5; piece++)
            {
                int type = piece < arena.Next.Count ? arena.Next[piece] : -1;
                for (int i = 0; i < 16; i++)
                {
                    int filled = type < 0 ? 0 : arena.CellAt(type, 0, i);
                    _nextCells[piece * 16 + i].color = filled != 0
                        ? ColorForPiece(type)
                        : new Color(0f, 0f, 0f, 0.08f);
                }
            }
        }
    }
}
