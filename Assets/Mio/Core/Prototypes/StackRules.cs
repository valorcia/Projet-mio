using System;
using System.Collections.Generic;
using Mio.Core.Board;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Prototypes
{
    [Serializable]
    public sealed class StackConfig
    {
        public float Duration = 60f;

        public int Width = 7;
        public int Height = 8;
        public int FamilyCount = 3;

        /// <summary>Pieces that must meet to transform. Three, per the spec.</summary>
        public int CombineSize = 3;

        /// <summary>
        /// Tier of a finished product. With 4 tiers: raw, worked, refined,
        /// product. The spec caps V1 at four.
        /// </summary>
        public int ProductTier = 3;

        public int MaxCascadeDepth = 8;

        public int ScorePerCombine = 20;

        /// <summary>Each tier is worth more, so setting up a deep chain pays.</summary>
        public float TierScoreMultiplier = 2.5f;

        public int GoalCottonProducts = 3;
        public int GoalWoodProducts = 2;
        public int GoalMetalProducts = 1;

        public float FieldMinX = 0.06f;
        public float FieldMaxX = 0.94f;
        public float FieldMinY = 0.18f;
        public float FieldMaxY = 0.80f;

        public StackConfig Clone() => (StackConfig)MemberwiseClone();
    }

    /// <summary>
    /// PROTOTYPE A — STACK.
    ///
    /// Swap two neighbours. Three matching pieces do not vanish: they
    /// <b>become</b> one piece of the next tier, and that new piece may itself
    /// complete another set. Raw cotton becomes thread becomes fabric becomes
    /// a finished product.
    ///
    /// The identity under test is transformation rather than destruction, and
    /// the pleasure of setting up a chain you could see coming. That is why an
    /// invalid swap is simply undone rather than punished: the interesting
    /// failure is a move that achieved less than you hoped, not one the game
    /// refused.
    /// </summary>
    public sealed class StackRules : IPrototypeRules
    {
        private readonly StackConfig _config;
        private readonly List<int> _scratch = new List<int>();

        private PieceGrid _grid;
        private DeterministicRng _rng;

        private float _elapsed;
        private int _moves;
        private int _combos;
        private int _invalidMoves;
        private int _deepestCascade;
        private int _highestTier;
        private int _productsMade;
        private ResourceBundle _earned;
        private float _objectiveCompletedAt = -1f;

        private bool _dragging;
        private int _dragFromX, _dragFromY;

        public StackRules(StackConfig config)
        {
            _config = config ?? new StackConfig();
        }

        public PrototypeId Id => PrototypeId.Stack;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public int Score { get; private set; }
        public Objective Objective { get; private set; } = new Objective();
        public int SuccessfulActions => _combos;
        public int FailedActions => _invalidMoves;
        public float Progress01 => Objective.Progress01;
        public float Duration => _config.Duration;
        public ResourceBundle ResourcesEarned => _earned;

        public float TimeRemaining =>
            MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public PieceGrid Grid => _grid;
        public int ProductTier => _config.ProductTier;

        /// <summary>Cell the finger is currently holding, for the view. -1 when idle.</summary>
        public int DragFromIndex => _dragging ? _grid.Index(_dragFromX, _dragFromY) : -1;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _grid = new PieceGrid(_config.Width, _config.Height);

            // Start from a board with no free combinations, so the first
            // cascade is something the player caused rather than something
            // that was sitting there.
            _grid.FillAvoidingGroups(_rng, _config.FamilyCount, _config.CombineSize);

            Objective = new Objective(
                new ObjectiveGoal("COTTON", _config.GoalCottonProducts),
                new ObjectiveGoal("WOOD", _config.GoalWoodProducts),
                new ObjectiveGoal("METAL", _config.GoalMetalProducts));

            _elapsed = 0f;
            _moves = 0; _combos = 0; _invalidMoves = 0;
            _deepestCascade = 0; _highestTier = 0; _productsMade = 0;
            _earned = ResourceBundle.Empty;
            _objectiveCompletedAt = -1f;
            _dragging = false;
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

            switch (command.Phase)
            {
                case InputPhase.Began:
                    _dragging = TryGetCell(command.Position, out _dragFromX, out _dragFromY);
                    break;

                case InputPhase.Ended:
                    if (!_dragging) break;
                    _dragging = false;

                    if (!TryGetCell(command.Position, out var tx, out var ty)) break;
                    TrySwap(_dragFromX, _dragFromY, tx, ty, feedback);
                    break;

                case InputPhase.Canceled:
                    _dragging = false;
                    break;
            }
        }

        private void TrySwap(int ax, int ay, int bx, int by, IFeedbackChannel feedback)
        {
            // Releasing on the cell you started from is a change of mind, not
            // a mistake, so it is not counted as a failed move.
            if (ax == bx && ay == by) return;

            var adjacent = Math.Abs(ax - bx) + Math.Abs(ay - by) == 1;
            if (!adjacent) return;

            _moves++;

            var a = _grid.Get(ax, ay);
            var b = _grid.Get(bx, by);
            if (a.IsEmpty || b.IsEmpty) return;

            _grid.Set(ax, ay, b);
            _grid.Set(bx, by, a);

            // A swap is only legal if it actually makes something, which is
            // what forces the player to look before moving.
            var madeSomething = TryCombineAt(bx, by, feedback, depth: 1)
                              | TryCombineAt(ax, ay, feedback, depth: 1);

            if (!madeSomething)
            {
                _grid.Set(ax, ay, a);
                _grid.Set(bx, by, b);
                _invalidMoves++;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, CellCenter(bx, by), 0.3f));
                return;
            }

            Settle(feedback, depth: 2);
        }

        /// <summary>
        /// Combines the group at a cell, if it is big enough, into one piece of
        /// the next tier placed at that cell.
        /// </summary>
        private bool TryCombineAt(int cx, int cy, IFeedbackChannel feedback, int depth)
        {
            if (_grid.Get(cx, cy).IsEmpty) return false;

            var found = _grid.FindMatchGroup(cx, cy);
            if (found.Count < _config.CombineSize) return false;

            _scratch.Clear();
            for (var i = 0; i < found.Count; i++) _scratch.Add(found[i]);

            var piece = _grid.Get(cx, cy);
            var size = _scratch.Count;
            var at = CellCenter(cx, cy);

            _grid.ClearIndices(_scratch);

            var promoted = piece.Promoted();
            if (promoted.Tier > _highestTier) _highestTier = promoted.Tier;

            _combos++;
            Score += (int)Math.Round(
                _config.ScorePerCombine * size * Math.Pow(_config.TierScoreMultiplier, piece.Tier));

            feedback.Emit(new FeedbackCue(ComboScale.ForSize(size), at, ComboScale.Intensity(size), size));

            var cascade = ComboScale.ForCascadeDepth(depth);
            if (cascade.HasValue)
            {
                feedback.Emit(new FeedbackCue(cascade.Value, at, MathK.Clamp01(depth / 4f), depth));
                if (depth > _deepestCascade) _deepestCascade = depth;
            }

            if (promoted.Tier >= _config.ProductTier)
            {
                // A finished product leaves the board and counts. Leaving it
                // in place would clog the grid and stall the very cascades
                // this prototype exists to test.
                _productsMade++;
                _earned[piece.Family] += 1;
                Objective.Add(PopRules.LabelOf(piece.Family));

                feedback.Emit(new FeedbackCue(FeedbackCueKind.ProductCreated, at, 1f, promoted.Tier));

                if (Objective.Completed && _objectiveCompletedAt < 0f)
                {
                    _objectiveCompletedAt = _elapsed;
                    feedback.Emit(new FeedbackCue(FeedbackCueKind.ObjectiveComplete, at));
                }
            }
            else
            {
                _grid.Set(cx, cy, promoted);
            }

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));
            return true;
        }

        /// <summary>
        /// Gravity, refill, then any set the settle happened to complete. This
        /// is the chain the player was trying to set up.
        /// </summary>
        private void Settle(IFeedbackChannel feedback, int depth)
        {
            for (var d = depth; d <= _config.MaxCascadeDepth; d++)
            {
                _grid.ApplyGravity();
                _grid.RefillRaw(_rng, _config.FamilyCount);

                if (!TryFindGroup(out var cx, out var cy)) break;
                TryCombineAt(cx, cy, feedback, d);
            }

            _grid.ApplyGravity();
            _grid.RefillRaw(_rng, _config.FamilyCount);
        }

        private bool TryFindGroup(out int cx, out int cy)
        {
            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    if (_grid.Get(x, y).IsEmpty) continue;
                    if (_grid.FindMatchGroup(x, y).Count >= _config.CombineSize)
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
            into["stack.moves"] = _moves;
            into["stack.combinations"] = _combos;
            into["stack.invalidMoves"] = _invalidMoves;
            into["stack.maxCascade"] = _deepestCascade;
            into["stack.highestTier"] = _highestTier;
            into["stack.productsMade"] = _productsMade;
            into["stack.objectiveCompletedAt"] = _objectiveCompletedAt;
        }
    }
}
