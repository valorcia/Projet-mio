using System.Collections.Generic;

namespace Mio.Core.Pack
{
    /// <summary>
    /// The container grid: occupancy, placement validity and line clearing.
    /// Pure data structure, no scoring and no timing, so it can be tested
    /// against hand-built layouts.
    /// </summary>
    public sealed class PackBoard
    {
        public const int Empty = -1;

        private readonly int[] _cells;
        private readonly List<int> _fullRows = new List<int>();
        private readonly List<int> _fullColumns = new List<int>();

        public PackBoard(int width, int height)
        {
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            _cells = new int[Width * Height];
            Reset();
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Rows cleared by the last <see cref="Place"/> call.</summary>
        public IReadOnlyList<int> LastClearedRows => _fullRows;

        /// <summary>Columns cleared by the last <see cref="Place"/> call.</summary>
        public IReadOnlyList<int> LastClearedColumns => _fullColumns;

        public void Reset()
        {
            for (var i = 0; i < _cells.Length; i++) _cells[i] = Empty;
            _fullRows.Clear();
            _fullColumns.Clear();
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        private int Index(int x, int y) => y * Width + x;

        public int Get(int x, int y) => InBounds(x, y) ? _cells[Index(x, y)] : Empty;

        public void Set(int x, int y, int value)
        {
            if (InBounds(x, y)) _cells[Index(x, y)] = value;
        }

        public bool IsOccupied(int x, int y) => Get(x, y) != Empty;

        public int OccupiedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] != Empty) n++;
                }

                return n;
            }
        }

        public bool CanPlace(PackShape shape, int anchorX, int anchorY)
        {
            if (shape == null || shape.CellCount == 0) return false;

            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var x = anchorX + shape.Cells[i].X;
                var y = anchorY + shape.Cells[i].Y;
                if (!InBounds(x, y)) return false;
                if (_cells[Index(x, y)] != Empty) return false;
            }

            return true;
        }

        /// <summary>True when the shape fits anywhere on the current board.</summary>
        public bool HasAnyPlacement(PackShape shape)
        {
            if (shape == null || shape.CellCount == 0) return false;

            for (var y = 0; y <= Height - shape.Height; y++)
            {
                for (var x = 0; x <= Width - shape.Width; x++)
                {
                    if (CanPlace(shape, x, y)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stamps the shape and clears any full rows and columns. Returns the
        /// number of lines cleared, or -1 if the placement was illegal.
        /// </summary>
        public int Place(PackShape shape, int anchorX, int anchorY, int colorId)
        {
            if (!CanPlace(shape, anchorX, anchorY)) return -1;

            for (var i = 0; i < shape.Cells.Length; i++)
            {
                var x = anchorX + shape.Cells[i].X;
                var y = anchorY + shape.Cells[i].Y;
                _cells[Index(x, y)] = colorId;
            }

            return ClearFullLines();
        }

        private int ClearFullLines()
        {
            _fullRows.Clear();
            _fullColumns.Clear();

            for (var y = 0; y < Height; y++)
            {
                var full = true;
                for (var x = 0; x < Width; x++)
                {
                    if (_cells[Index(x, y)] == Empty) { full = false; break; }
                }

                if (full) _fullRows.Add(y);
            }

            for (var x = 0; x < Width; x++)
            {
                var full = true;
                for (var y = 0; y < Height; y++)
                {
                    if (_cells[Index(x, y)] == Empty) { full = false; break; }
                }

                if (full) _fullColumns.Add(x);
            }

            // Both passes read the board before either writes, so a row and a
            // column that intersect both count. Clearing as we went would make
            // the second pass miss the shared cell.
            for (var i = 0; i < _fullRows.Count; i++)
            {
                var y = _fullRows[i];
                for (var x = 0; x < Width; x++) _cells[Index(x, y)] = Empty;
            }

            for (var i = 0; i < _fullColumns.Count; i++)
            {
                var x = _fullColumns[i];
                for (var y = 0; y < Height; y++) _cells[Index(x, y)] = Empty;
            }

            return _fullRows.Count + _fullColumns.Count;
        }
    }
}
