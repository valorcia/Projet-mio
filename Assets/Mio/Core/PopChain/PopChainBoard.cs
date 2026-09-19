using System.Collections.Generic;
using Mio.Core.Common;

namespace Mio.Core.PopChain
{
    /// <summary>
    /// Colour grid with flood-fill, gravity and refill. Separated from the rules
    /// so the board can be unit tested against hand-authored layouts without
    /// involving scoring, the clock or feedback.
    ///
    /// Origin is bottom-left: y = 0 is the floor, gravity pulls toward it.
    /// </summary>
    public sealed class PopChainBoard
    {
        public const int Empty = -1;

        private readonly int[] _cells;
        private readonly bool[] _visited;
        private readonly List<int> _scratchGroup = new List<int>();
        private readonly Stack<int> _stack = new Stack<int>();

        public PopChainBoard(int width, int height)
        {
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            _cells = new int[Width * Height];
            _visited = new bool[Width * Height];
        }

        public int Width { get; }
        public int Height { get; }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        private int Index(int x, int y) => y * Width + x;

        public int Get(int x, int y) => InBounds(x, y) ? _cells[Index(x, y)] : Empty;

        public void Set(int x, int y, int color)
        {
            if (InBounds(x, y)) _cells[Index(x, y)] = color;
        }

        public void Fill(DeterministicRng rng, int colorCount)
        {
            var colors = colorCount < 1 ? 1 : colorCount;
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = rng.Range(0, colors);
            }
        }

        /// <summary>
        /// 4-connected same-colour region containing (x, y). The returned list is
        /// reused between calls, so copy it if it must outlive the next call.
        /// </summary>
        public IReadOnlyList<int> FindGroup(int x, int y)
        {
            _scratchGroup.Clear();
            if (!InBounds(x, y)) return _scratchGroup;

            var color = Get(x, y);
            if (color == Empty) return _scratchGroup;

            System.Array.Clear(_visited, 0, _visited.Length);
            _stack.Clear();

            var start = Index(x, y);
            _visited[start] = true;
            _stack.Push(start);

            while (_stack.Count > 0)
            {
                var index = _stack.Pop();
                _scratchGroup.Add(index);

                var cx = index % Width;
                var cy = index / Width;

                TryPush(cx + 1, cy, color);
                TryPush(cx - 1, cy, color);
                TryPush(cx, cy + 1, color);
                TryPush(cx, cy - 1, color);
            }

            return _scratchGroup;
        }

        private void TryPush(int x, int y, int color)
        {
            if (!InBounds(x, y)) return;
            var index = Index(x, y);
            if (_visited[index]) return;
            if (_cells[index] != color) return;

            _visited[index] = true;
            _stack.Push(index);
        }

        public void Clear(IReadOnlyList<int> indices)
        {
            for (var i = 0; i < indices.Count; i++)
            {
                _cells[indices[i]] = Empty;
            }
        }

        /// <summary>Compacts every column downward. Returns true if anything moved.</summary>
        public bool ApplyGravity()
        {
            var moved = false;

            for (var x = 0; x < Width; x++)
            {
                var write = 0;
                for (var y = 0; y < Height; y++)
                {
                    var value = _cells[Index(x, y)];
                    if (value == Empty) continue;

                    if (write != y)
                    {
                        _cells[Index(x, write)] = value;
                        _cells[Index(x, y)] = Empty;
                        moved = true;
                    }

                    write++;
                }
            }

            return moved;
        }

        /// <summary>Fills holes left after gravity so the board never runs dry.</summary>
        public void Refill(DeterministicRng rng, int colorCount)
        {
            var colors = colorCount < 1 ? 1 : colorCount;
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == Empty) _cells[i] = rng.Range(0, colors);
            }
        }

        /// <summary>True when at least one poppable group of the given size exists.</summary>
        public bool HasMove(int minGroupSize)
        {
            var min = minGroupSize < 2 ? 2 : minGroupSize;

            // A group of >= 2 always contains a horizontally or vertically
            // adjacent matching pair, so for the common min size of 2 a cheap
            // neighbour scan is exact.
            if (min == 2)
            {
                for (var y = 0; y < Height; y++)
                {
                    for (var x = 0; x < Width; x++)
                    {
                        var c = _cells[Index(x, y)];
                        if (c == Empty) continue;
                        if (x + 1 < Width && _cells[Index(x + 1, y)] == c) return true;
                        if (y + 1 < Height && _cells[Index(x, y + 1)] == c) return true;
                    }
                }

                return false;
            }

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (FindGroup(x, y).Count >= min) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reseeds the whole board. Used when a refill leaves no legal move, so
        /// the player is never stranded staring at a dead grid.
        /// </summary>
        public void Reshuffle(DeterministicRng rng, int colorCount, int minGroupSize)
        {
            // Bounded so a pathological config (one colour, huge min group)
            // cannot hang the game loop.
            for (var attempt = 0; attempt < 32; attempt++)
            {
                Fill(rng, colorCount);
                if (HasMove(minGroupSize)) return;
            }
        }
    }
}
