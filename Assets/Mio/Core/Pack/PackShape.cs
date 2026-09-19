using System;

namespace Mio.Core.Pack
{
    [Serializable]
    public struct GridOffset
    {
        public int X;
        public int Y;

        public GridOffset(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// A polyomino, described as cell offsets from its bottom-left corner.
    /// Data only, so the shape catalogue can be authored as an asset and
    /// re-balanced without code.
    /// </summary>
    [Serializable]
    public sealed class PackShape
    {
        public string Name;
        public GridOffset[] Cells;

        public PackShape(string name, params GridOffset[] cells)
        {
            Name = name;
            Cells = cells ?? new GridOffset[0];

            var w = 0;
            var h = 0;
            for (var i = 0; i < Cells.Length; i++)
            {
                if (Cells[i].X + 1 > w) w = Cells[i].X + 1;
                if (Cells[i].Y + 1 > h) h = Cells[i].Y + 1;
            }

            Width = w;
            Height = h;
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => Cells.Length;
    }

    public static class PackShapes
    {
        private static GridOffset O(int x, int y) => new GridOffset(x, y);

        /// <summary>
        /// Default catalogue. Deliberately biased toward small pieces: the point
        /// of M0 is a 60-second win, not a packing brain-teaser.
        /// </summary>
        public static readonly PackShape[] Default =
        {
            new PackShape("Dot", O(0, 0)),
            new PackShape("Duo-H", O(0, 0), O(1, 0)),
            new PackShape("Duo-V", O(0, 0), O(0, 1)),
            new PackShape("Tri-H", O(0, 0), O(1, 0), O(2, 0)),
            new PackShape("Tri-V", O(0, 0), O(0, 1), O(0, 2)),
            new PackShape("Square", O(0, 0), O(1, 0), O(0, 1), O(1, 1)),
            new PackShape("L-SW", O(0, 0), O(0, 1), O(1, 0)),
            new PackShape("L-SE", O(0, 0), O(1, 0), O(1, 1)),
            new PackShape("L-NW", O(0, 0), O(0, 1), O(1, 1)),
            new PackShape("L-NE", O(1, 0), O(0, 1), O(1, 1))
        };
    }
}
