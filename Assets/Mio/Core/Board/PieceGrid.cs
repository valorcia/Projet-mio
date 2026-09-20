using System;
using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Economy;

namespace Mio.Core.Board
{
    /// <summary>
    /// One board cell: a resource family at a transformation tier.
    ///
    /// Tier 0 is the raw material and each step up is a more valuable object.
    /// That is the identity M0.2 is testing — pieces become <i>things</i>
    /// rather than simply vanishing.
    /// </summary>
    public readonly struct Piece : IEquatable<Piece>
    {
        public readonly ResourceKind Family;
        public readonly int Tier;
        public readonly bool Occupied;

        public Piece(ResourceKind family, int tier)
        {
            Family = family;
            Tier = tier < 0 ? 0 : tier;
            Occupied = true;
        }

        public static Piece Empty => default;

        public bool IsEmpty => !Occupied;

        /// <summary>Same family AND same tier: the condition for combining.</summary>
        public bool Matches(Piece other)
        {
            return Occupied && other.Occupied && Family == other.Family && Tier == other.Tier;
        }

        /// <summary>Same family, any tier: the condition POP uses.</summary>
        public bool SameFamily(Piece other)
        {
            return Occupied && other.Occupied && Family == other.Family;
        }

        public Piece Promoted() => new Piece(Family, Tier + 1);

        public bool Equals(Piece other) =>
            Occupied == other.Occupied && Family == other.Family && Tier == other.Tier;

        public override bool Equals(object obj) => obj is Piece p && Equals(p);
        public override int GetHashCode() => Occupied ? (int)Family * 31 + Tier : -1;
        public override string ToString() => Occupied ? $"{Family}{Tier}" : "-";
    }

    /// <summary>
    /// The board every grid-based prototype shares: storage, gravity, refill
    /// and the two group-detection rules the four candidates need.
    ///
    /// Deliberately a small concrete class, not a framework. It exists because
    /// STACK, POP and MIO MIX would otherwise each reimplement gravity and
    /// flood fill, and a bug in one copy would quietly make one prototype
    /// compare badly against the others for reasons that are not design.
    ///
    /// Origin is bottom-left: y = 0 is the floor, gravity pulls toward it.
    /// </summary>
    public sealed class PieceGrid
    {
        private readonly Piece[] _cells;
        private readonly bool[] _visited;
        private readonly List<int> _group = new List<int>();
        private readonly Stack<int> _stack = new Stack<int>();

        public PieceGrid(int width, int height)
        {
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            _cells = new Piece[Width * Height];
            _visited = new bool[Width * Height];
        }

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _cells.Length;

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public int Index(int x, int y) => y * Width + x;
        public int XOf(int index) => index % Width;
        public int YOf(int index) => index / Width;

        public Piece Get(int x, int y) => InBounds(x, y) ? _cells[Index(x, y)] : Piece.Empty;
        public Piece At(int index) => _cells[index];

        public void Set(int x, int y, Piece piece)
        {
            if (InBounds(x, y)) _cells[Index(x, y)] = piece;
        }

        public void SetAt(int index, Piece piece) => _cells[index] = piece;

        public void Clear()
        {
            for (var i = 0; i < _cells.Length; i++) _cells[i] = Piece.Empty;
        }

        public int OccupiedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i].Occupied) n++;
                }

                return n;
            }
        }

        public float Occupancy => CellCount == 0 ? 0f : OccupiedCount / (float)CellCount;

        /// <summary>Fills every cell with a random raw piece.</summary>
        public void FillRaw(DeterministicRng rng, int familyCount)
        {
            var families = MathK.Clamp(familyCount, 1, ResourceKinds.Count);
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new Piece(ResourceKinds.At(rng.Range(0, families)), 0);
            }
        }

        /// <summary>
        /// Fills the board so that no connected group reaches
        /// <paramref name="maxGroup"/>.
        ///
        /// Retrying a whole random fill until one happens to be clean does not
        /// work: on a dense board with three families a connected triple is
        /// almost certain, so the retry loop would always exhaust and hand back
        /// a board that already had free combinations on it. Choosing each cell
        /// against the neighbours already placed is exact and needs no luck.
        /// </summary>
        public void FillAvoidingGroups(DeterministicRng rng, int familyCount, int maxGroup)
        {
            var families = MathK.Clamp(familyCount, 1, ResourceKinds.Count);
            Clear();

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var offset = rng.Range(0, families);
                    var placed = false;

                    for (var attempt = 0; attempt < families && !placed; attempt++)
                    {
                        var family = ResourceKinds.At(offset + attempt);
                        Set(x, y, new Piece(family, 0));

                        if (FindMatchGroup(x, y).Count < maxGroup) placed = true;
                    }

                    // With one family every choice fails; leave the last try
                    // rather than looping forever.
                    if (!placed) Set(x, y, new Piece(ResourceKinds.At(offset), 0));
                }
            }
        }

        /// <summary>Fills only the empty cells, so existing pieces are untouched.</summary>
        public void RefillRaw(DeterministicRng rng, int familyCount)
        {
            var families = MathK.Clamp(familyCount, 1, ResourceKinds.Count);
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].IsEmpty)
                {
                    _cells[i] = new Piece(ResourceKinds.At(rng.Range(0, families)), 0);
                }
            }
        }

        /// <summary>Compacts every column downward. True if anything moved.</summary>
        public bool ApplyGravity()
        {
            var moved = false;

            for (var x = 0; x < Width; x++)
            {
                var write = 0;
                for (var y = 0; y < Height; y++)
                {
                    var piece = _cells[Index(x, y)];
                    if (piece.IsEmpty) continue;

                    if (write != y)
                    {
                        _cells[Index(x, write)] = piece;
                        _cells[Index(x, y)] = Piece.Empty;
                        moved = true;
                    }

                    write++;
                }
            }

            return moved;
        }

        /// <summary>
        /// 4-connected region of pieces in the same family, ignoring tier.
        /// POP's rule. The returned list is reused between calls, so copy it
        /// if it must outlive the next call.
        /// </summary>
        public IReadOnlyList<int> FindFamilyGroup(int x, int y)
        {
            return Flood(x, y, sameTier: false);
        }

        /// <summary>
        /// 4-connected region of pieces matching in family <b>and</b> tier.
        /// The combining rule for STACK and MIO MIX.
        /// </summary>
        public IReadOnlyList<int> FindMatchGroup(int x, int y)
        {
            return Flood(x, y, sameTier: true);
        }

        private IReadOnlyList<int> Flood(int x, int y, bool sameTier)
        {
            _group.Clear();
            if (!InBounds(x, y)) return _group;

            var origin = Get(x, y);
            if (origin.IsEmpty) return _group;

            Array.Clear(_visited, 0, _visited.Length);
            _stack.Clear();

            var start = Index(x, y);
            _visited[start] = true;
            _stack.Push(start);

            while (_stack.Count > 0)
            {
                var index = _stack.Pop();
                _group.Add(index);

                var cx = XOf(index);
                var cy = YOf(index);

                Push(cx + 1, cy, origin, sameTier);
                Push(cx - 1, cy, origin, sameTier);
                Push(cx, cy + 1, origin, sameTier);
                Push(cx, cy - 1, origin, sameTier);
            }

            return _group;
        }

        private void Push(int x, int y, Piece origin, bool sameTier)
        {
            if (!InBounds(x, y)) return;

            var index = Index(x, y);
            if (_visited[index]) return;

            var piece = _cells[index];
            var ok = sameTier ? origin.Matches(piece) : origin.SameFamily(piece);
            if (!ok) return;

            _visited[index] = true;
            _stack.Push(index);
        }

        /// <summary>True when a group of at least the given size exists.</summary>
        public bool HasGroup(int minSize, bool sameTier)
        {
            var min = minSize < 2 ? 2 : minSize;

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (_cells[Index(x, y)].IsEmpty) continue;
                    var group = sameTier ? FindMatchGroup(x, y) : FindFamilyGroup(x, y);
                    if (group.Count >= min) return true;
                }
            }

            return false;
        }

        public void ClearIndices(IReadOnlyList<int> indices)
        {
            for (var i = 0; i < indices.Count; i++) _cells[indices[i]] = Piece.Empty;
        }

        /// <summary>
        /// Reseeds until a legal move exists, so a player is never stranded
        /// staring at a dead board. Bounded so a pathological config cannot
        /// hang the game loop.
        /// </summary>
        public void Reshuffle(DeterministicRng rng, int familyCount, int minGroup, bool sameTier)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                FillRaw(rng, familyCount);
                if (HasGroup(minGroup, sameTier)) return;
            }
        }

        /// <summary>Play-field position of a cell centre, inside the given rect.</summary>
        public Vec2 CellCenter(int x, int y, float minX, float minY, float maxX, float maxY)
        {
            var spanX = maxX - minX;
            var spanY = maxY - minY;

            return new Vec2(
                minX + (x + 0.5f) / Width * spanX,
                minY + (y + 0.5f) / Height * spanY);
        }

        /// <summary>Maps a play-field point to a cell. False when outside the rect.</summary>
        public bool TryGetCell(Vec2 position, float minX, float minY, float maxX, float maxY,
                               out int cx, out int cy)
        {
            cx = 0;
            cy = 0;

            var spanX = maxX - minX;
            var spanY = maxY - minY;
            if (spanX <= 0f || spanY <= 0f) return false;

            var u = (position.X - minX) / spanX;
            var v = (position.Y - minY) / spanY;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return false;

            cx = MathK.Clamp((int)(u * Width), 0, Width - 1);
            cy = MathK.Clamp((int)(v * Height), 0, Height - 1);
            return true;
        }
    }
}
