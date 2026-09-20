using System;
using System.Collections.Generic;
using Mio.Core.Board;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Prototypes
{
    public enum PopSpecial
    {
        None = 0,

        /// <summary>Collects a whole row.</summary>
        Line = 1,

        /// <summary>Collects the surrounding cells.</summary>
        Burst = 2,

        /// <summary>Collects every piece of one resource family.</summary>
        Mio = 3
    }

    [Serializable]
    public sealed class PopConfig
    {
        public float Duration = 60f;

        public int Width = 7;
        public int Height = 9;
        public int FamilyCount = 3;

        /// <summary>Smallest group that can be tapped.</summary>
        public int MinGroup = 2;

        /// <summary>Group size that leaves a special piece behind.</summary>
        public int SpecialThreshold = 8;

        /// <summary>
        /// A group this big forming on its own auto-pops, which is what turns a
        /// single tap into a chain reaction.
        /// </summary>
        public int AutoCascadeMin = 4;

        /// <summary>Safety rail: a refill cannot keep triggering forever.</summary>
        public int MaxCascadeDepth = 6;

        public int ScorePerPiece = 10;

        /// <summary>
        /// Multipliers by band, so a big group pays disproportionately rather
        /// than merely proportionally. Index: 0 = 2, 1 = 3-4, 2 = 5-7, 3 = 8+.
        /// </summary>
        public float[] BandMultipliers = { 1f, 1.2f, 2f, 3.5f };

        public int GoalCotton = 20;
        public int GoalWood = 15;
        public int GoalMetal = 10;

        public float FieldMinX = 0.06f;
        public float FieldMaxX = 0.94f;
        public float FieldMinY = 0.18f;
        public float FieldMaxY = 0.82f;

        public PopConfig Clone()
        {
            var copy = (PopConfig)MemberwiseClone();
            copy.BandMultipliers = (float[])BandMultipliers.Clone();
            return copy;
        }
    }

    /// <summary>
    /// PROTOTYPE C — POP.
    ///
    /// Tap a blob touching a twin. The group collects, the board falls in, and
    /// a big enough accidental group pops on its own, which is where the chain
    /// reactions come from.
    ///
    /// The simplest candidate, and deliberately so: its weakness is
    /// originality, so the experiment is whether raw tactile satisfaction and
    /// instant legibility beat the more distinctive prototypes.
    /// </summary>
    public sealed class PopRules : IPrototypeRules
    {
        private readonly PopConfig _config;
        private readonly List<int> _scratch = new List<int>();

        private PieceGrid _grid;
        private PopSpecial[] _specials;
        private DeterministicRng _rng;

        private float _elapsed;
        private int _taps;
        private int _badTaps;
        private int _piecesCollected;
        private int _largestGroup;
        private int _specialsCreated;
        private int _specialsActivated;
        private int _cascades;
        private int _deepestCascade;
        private ResourceBundle _earned;
        private float _objectiveCompletedAt = -1f;

        public PopRules(PopConfig config)
        {
            _config = config ?? new PopConfig();
        }

        public PrototypeId Id => PrototypeId.Pop;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public int Score { get; private set; }
        public Objective Objective { get; private set; } = new Objective();
        public int SuccessfulActions => _taps;
        public int FailedActions => _badTaps;
        public float Progress01 => Objective.Progress01;
        public float Duration => _config.Duration;
        public ResourceBundle ResourcesEarned => _earned;

        public float TimeRemaining =>
            MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public PieceGrid Grid => _grid;

        public PopSpecial SpecialAt(int x, int y) =>
            _grid != null && _grid.InBounds(x, y) ? _specials[_grid.Index(x, y)] : PopSpecial.None;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _grid = new PieceGrid(_config.Width, _config.Height);
            _specials = new PopSpecial[_grid.CellCount];
            _grid.Reshuffle(_rng, _config.FamilyCount, _config.MinGroup, sameTier: false);

            Objective = new Objective(
                new ObjectiveGoal("COTTON", _config.GoalCotton),
                new ObjectiveGoal("WOOD", _config.GoalWood),
                new ObjectiveGoal("METAL", _config.GoalMetal));

            _elapsed = 0f;
            _taps = 0; _badTaps = 0; _piecesCollected = 0; _largestGroup = 0;
            _specialsCreated = 0; _specialsActivated = 0;
            _cascades = 0; _deepestCascade = 0;
            _earned = ResourceBundle.Empty;
            _objectiveCompletedAt = -1f;
            Score = 0;
            Status = SessionStatus.Playing;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, new Vec2(0.5f, 0.5f)));
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;

            if (Objective.Completed)
            {
                Status = SessionStatus.Won;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Win, new Vec2(0.5f, 0.5f)));
                return;
            }

            if (_elapsed >= _config.Duration)
            {
                Status = SessionStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, new Vec2(0.5f, 0.5f)));
            }
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing) return;

            // Resolve on press: the burst has to land under the finger the
            // moment it touches, or the whole prototype feels dead.
            if (command.Phase != InputPhase.Began) return;

            if (!TryGetCell(command.Position, out var cx, out var cy)) return;

            var index = _grid.Index(cx, cy);

            if (_specials[index] != PopSpecial.None)
            {
                ActivateSpecial(cx, cy, feedback);
                return;
            }

            var found = _grid.FindFamilyGroup(cx, cy);
            if (found.Count < _config.MinGroup)
            {
                _badTaps++;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, CellCenter(cx, cy), 0.3f));
                return;
            }

            CopyScratch(found);
            _taps++;
            Collect(_scratch, CellCenter(cx, cy), feedback, depth: 1);

            if (_scratch.Count >= _config.SpecialThreshold) CreateSpecial(cx, cy, _scratch.Count, feedback);

            Settle(feedback);
        }

        private void CopyScratch(IReadOnlyList<int> source)
        {
            // FindFamilyGroup reuses its buffer and we are about to call back
            // into the grid, so take a copy before mutating anything.
            _scratch.Clear();
            for (var i = 0; i < source.Count; i++) _scratch.Add(source[i]);
        }

        /// <summary>Collects a set of cells, scores them and banks the resources.</summary>
        private void Collect(List<int> cells, Vec2 at, IFeedbackChannel feedback, int depth)
        {
            if (cells.Count == 0) return;

            for (var i = 0; i < cells.Count; i++)
            {
                var piece = _grid.At(cells[i]);
                if (piece.IsEmpty) continue;

                _earned[piece.Family] += 1;
                Objective.Add(LabelOf(piece.Family));
            }

            var size = cells.Count;
            if (size > _largestGroup) _largestGroup = size;
            _piecesCollected += size;

            Score += (int)Math.Round(_config.ScorePerPiece * size * BandMultiplier(size));

            _grid.ClearIndices(cells);

            feedback.Emit(new FeedbackCue(ComboScale.ForSize(size), at, ComboScale.Intensity(size), size));

            var cascade = ComboScale.ForCascadeDepth(depth);
            if (cascade.HasValue)
            {
                feedback.Emit(new FeedbackCue(cascade.Value, at, MathK.Clamp01(depth / 4f), depth));
            }

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));

            if (Objective.Completed && _objectiveCompletedAt < 0f)
            {
                _objectiveCompletedAt = _elapsed;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.ObjectiveComplete, at));
            }
        }

        private float BandMultiplier(int size)
        {
            var m = _config.BandMultipliers;
            if (m == null || m.Length == 0) return 1f;

            var band = size >= 8 ? 3 : size >= 5 ? 2 : size >= 3 ? 1 : 0;
            return m[MathK.Clamp(band, 0, m.Length - 1)];
        }

        private void CreateSpecial(int cx, int cy, int groupSize, IFeedbackChannel feedback)
        {
            // A bigger group earns a better special, so the reward for a huge
            // clear is visible and immediate.
            var kind = groupSize >= 13 ? PopSpecial.Mio
                     : groupSize >= 10 ? PopSpecial.Burst
                     : PopSpecial.Line;

            var index = _grid.Index(cx, cy);
            _grid.SetAt(index, new Piece(ResourceKinds.At(_rng.Range(0, _config.FamilyCount)), 0));
            _specials[index] = kind;
            _specialsCreated++;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.ProductCreated, CellCenter(cx, cy), 1f, (int)kind));
        }

        private void ActivateSpecial(int cx, int cy, IFeedbackChannel feedback)
        {
            var index = _grid.Index(cx, cy);
            var kind = _specials[index];
            _specials[index] = PopSpecial.None;

            _scratch.Clear();

            switch (kind)
            {
                case PopSpecial.Line:
                    for (var x = 0; x < _grid.Width; x++)
                    {
                        if (!_grid.Get(x, cy).IsEmpty) _scratch.Add(_grid.Index(x, cy));
                    }

                    break;

                case PopSpecial.Burst:
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var x = cx + dx;
                            var y = cy + dy;
                            if (_grid.InBounds(x, y) && !_grid.Get(x, y).IsEmpty) _scratch.Add(_grid.Index(x, y));
                        }
                    }

                    break;

                case PopSpecial.Mio:
                {
                    var family = _grid.At(index).Family;
                    for (var i = 0; i < _grid.CellCount; i++)
                    {
                        var piece = _grid.At(i);
                        if (piece.Occupied && piece.Family == family) _scratch.Add(i);
                    }

                    break;
                }
            }

            _taps++;
            _specialsActivated++;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.MioPowerUsed, CellCenter(cx, cy), 1f, (int)kind));
            Collect(_scratch, CellCenter(cx, cy), feedback, depth: 1);
            Settle(feedback);
        }

        /// <summary>
        /// Gravity, refill, and any accidental group big enough to pop on its
        /// own. This loop is where POP's chain reactions live.
        /// </summary>
        private void Settle(IFeedbackChannel feedback)
        {
            for (var depth = 2; depth <= _config.MaxCascadeDepth + 1; depth++)
            {
                MoveSpecialsWithGravity();
                _grid.RefillRaw(_rng, _config.FamilyCount);

                if (!TryFindAutoGroup(out var cx, out var cy)) break;

                var found = _grid.FindFamilyGroup(cx, cy);
                CopyScratch(found);

                _cascades++;
                if (depth - 1 > _deepestCascade) _deepestCascade = depth - 1;

                Collect(_scratch, CellCenter(cx, cy), feedback, depth);
            }

            // A cascade that hit the depth limit left its last collection
            // unfilled, so settle once more before judging the board.
            MoveSpecialsWithGravity();
            _grid.RefillRaw(_rng, _config.FamilyCount);

            // A board with no legal move would strand the player, and POP's
            // whole promise is that you can always just tap something.
            if (!_grid.HasGroup(_config.MinGroup, sameTier: false))
            {
                _grid.Reshuffle(_rng, _config.FamilyCount, _config.MinGroup, sameTier: false);
                Array.Clear(_specials, 0, _specials.Length);
            }
        }

        /// <summary>
        /// Gravity has to carry specials with their pieces, or a special would
        /// stay behind on a cell whose blob fell away.
        /// </summary>
        private void MoveSpecialsWithGravity()
        {
            for (var x = 0; x < _grid.Width; x++)
            {
                var write = 0;
                for (var y = 0; y < _grid.Height; y++)
                {
                    var from = _grid.Index(x, y);
                    if (_grid.At(from).IsEmpty) continue;

                    var to = _grid.Index(x, write);
                    if (to != from)
                    {
                        _grid.SetAt(to, _grid.At(from));
                        _specials[to] = _specials[from];
                        _grid.SetAt(from, Piece.Empty);
                        _specials[from] = PopSpecial.None;
                    }

                    write++;
                }
            }
        }

        private bool TryFindAutoGroup(out int cx, out int cy)
        {
            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    if (_specials[_grid.Index(x, y)] != PopSpecial.None) continue;
                    if (_grid.Get(x, y).IsEmpty) continue;

                    if (_grid.FindFamilyGroup(x, y).Count >= _config.AutoCascadeMin)
                    {
                        cx = x;
                        cy = y;
                        return true;
                    }
                }
            }

            cx = 0;
            cy = 0;
            return false;
        }

        internal static string LabelOf(ResourceKind family)
        {
            switch (family)
            {
                case ResourceKind.Cotton: return "COTTON";
                case ResourceKind.Wood: return "WOOD";
                default: return "METAL";
            }
        }

        public bool TryGetCell(Vec2 position, out int cx, out int cy)
        {
            return _grid.TryGetCell(position,
                _config.FieldMinX, _config.FieldMinY, _config.FieldMaxX, _config.FieldMaxY,
                out cx, out cy);
        }

        public Vec2 CellCenter(int cx, int cy)
        {
            return _grid.CellCenter(cx, cy,
                _config.FieldMinX, _config.FieldMinY, _config.FieldMaxX, _config.FieldMaxY);
        }

        public void CollectCustomMetrics(IDictionary<string, double> into)
        {
            into["pop.taps"] = _taps;
            into["pop.badTaps"] = _badTaps;
            into["pop.piecesCollected"] = _piecesCollected;
            into["pop.averageGroupSize"] = _taps == 0 ? 0d : _piecesCollected / (double)_taps;
            into["pop.largestGroup"] = _largestGroup;
            into["pop.specialsCreated"] = _specialsCreated;
            into["pop.specialsActivated"] = _specialsActivated;
            into["pop.cascades"] = _cascades;
            into["pop.deepestCascade"] = _deepestCascade;
            into["pop.objectiveCompletedAt"] = _objectiveCompletedAt;
        }
    }
}
