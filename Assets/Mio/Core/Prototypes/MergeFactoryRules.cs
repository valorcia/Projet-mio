using System;
using System.Collections.Generic;
using Mio.Core.Board;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Prototypes
{
    /// <summary>One order waiting at a factory: a product of a family and tier.</summary>
    public struct FactoryOrder
    {
        public ResourceKind Family;
        public int Tier;
        public bool Filled;

        /// <summary>Run-seconds when the order appeared, for time-to-complete.</summary>
        public float IssuedAt;

        public FactoryOrder(ResourceKind family, int tier, float issuedAt)
        {
            Family = family;
            Tier = tier;
            Filled = false;
            IssuedAt = issuedAt;
        }
    }

    [Serializable]
    public sealed class MergeFactoryConfig
    {
        public float Duration = 60f;

        public int Width = 6;
        public int Height = 7;
        public int FamilyCount = 3;

        /// <summary>Tier a product must reach to fill an order.</summary>
        public int OrderTier = 2;

        /// <summary>Highest tier a merge can produce.</summary>
        public int MaxTier = 3;

        /// <summary>Share of cells holding a piece at the start.</summary>
        public float StartFill = 0.45f;

        /// <summary>Orders visible at once.</summary>
        public int OrderSlots = 3;

        public int ScorePerMerge = 15;
        public float TierScoreMultiplier = 2f;
        public int ScorePerOrder = 200;

        public int GoalOrders = 6;

        /// <summary>
        /// Seconds between new raw pieces arriving. Zero disables the stream
        /// entirely — speed must never become the challenge.
        /// </summary>
        public float SupplyInterval = 4.5f;

        /// <summary>Occupancy above which the board counts as "near full".</summary>
        public float NearFullThreshold = 0.85f;

        public float FieldMinX = 0.08f;
        public float FieldMaxX = 0.92f;
        public float FieldMinY = 0.16f;
        public float FieldMaxY = 0.74f;

        public MergeFactoryConfig Clone() => (MergeFactoryConfig)MemberwiseClone();
    }

    /// <summary>
    /// PROTOTYPE B — MERGE FACTORY.
    ///
    /// Drag one piece onto an identical one and the pair becomes the next tier.
    /// Reach the order tier and the product leaves for the factory, filling an
    /// order and freeing the cell.
    ///
    /// The board is deliberately sparse and does not refill itself, so the
    /// tension is spatial rather than temporal: "I need room" and "if I
    /// combine this now I can complete that order". The slow supply stream is
    /// configurable and can be switched off entirely, because the moment speed
    /// becomes the challenge this stops being the experiment we wanted.
    /// </summary>
    public sealed class MergeFactoryRules : IPrototypeRules
    {
        private readonly MergeFactoryConfig _config;

        private PieceGrid _grid;
        private DeterministicRng _rng;
        private FactoryOrder[] _orders;

        private float _elapsed;
        private float _sinceSupply;
        private float _nearFullTime;
        private float _orderTimeTotal;
        private int _merges;
        private int _invalidMerges;
        private int _highestTier;
        private int _ordersCompleted;
        private float _occupancySum;
        private int _occupancySamples;
        private ResourceBundle _earned;
        private float _objectiveCompletedAt = -1f;

        private bool _dragging;
        private int _dragFromX, _dragFromY;

        public MergeFactoryRules(MergeFactoryConfig config)
        {
            _config = config ?? new MergeFactoryConfig();
        }

        public PrototypeId Id => PrototypeId.MergeFactory;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public int Score { get; private set; }
        public Objective Objective { get; private set; } = new Objective();
        public int SuccessfulActions => _merges;
        public int FailedActions => _invalidMerges;
        public float Progress01 => Objective.Progress01;
        public float Duration => _config.Duration;
        public ResourceBundle ResourcesEarned => _earned;

        public float TimeRemaining =>
            MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public PieceGrid Grid => _grid;
        public IReadOnlyList<FactoryOrder> Orders => _orders;
        public int OrderTier => _config.OrderTier;
        public float Occupancy => _grid?.Occupancy ?? 0f;
        public int DragFromIndex => _dragging ? _grid.Index(_dragFromX, _dragFromY) : -1;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _grid = new PieceGrid(_config.Width, _config.Height);
            _grid.Clear();

            // Sparse on purpose: room to work is the resource being managed.
            var target = (int)(_grid.CellCount * MathK.Clamp01(_config.StartFill));
            for (var placed = 0; placed < target; placed++) SpawnRaw();

            _orders = new FactoryOrder[_config.OrderSlots < 1 ? 1 : _config.OrderSlots];
            for (var i = 0; i < _orders.Length; i++) _orders[i] = NewOrder(0f);

            Objective = new Objective(new ObjectiveGoal("ORDERS", _config.GoalOrders));

            _elapsed = 0f; _sinceSupply = 0f; _nearFullTime = 0f; _orderTimeTotal = 0f;
            _merges = 0; _invalidMerges = 0; _highestTier = 0; _ordersCompleted = 0;
            _occupancySum = 0f; _occupancySamples = 0;
            _earned = ResourceBundle.Empty;
            _objectiveCompletedAt = -1f;
            _dragging = false;
            Score = 0;
            Status = SessionStatus.Playing;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, new Vec2(0.5f, 0.5f)));
        }

        private FactoryOrder NewOrder(float at)
        {
            return new FactoryOrder(
                ResourceKinds.At(_rng.Range(0, _config.FamilyCount)),
                _config.OrderTier,
                at);
        }

        /// <summary>Places one raw piece in a free cell. False when full.</summary>
        private bool SpawnRaw()
        {
            var free = 0;
            for (var i = 0; i < _grid.CellCount; i++)
            {
                if (_grid.At(i).IsEmpty) free++;
            }

            if (free == 0) return false;

            var pick = _rng.Range(0, free);
            for (var i = 0; i < _grid.CellCount; i++)
            {
                if (!_grid.At(i).IsEmpty) continue;
                if (pick-- > 0) continue;

                _grid.SetAt(i, new Piece(ResourceKinds.At(_rng.Range(0, _config.FamilyCount)), 0));
                return true;
            }

            return false;
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;

            _occupancySum += _grid.Occupancy;
            _occupancySamples++;
            if (_grid.Occupancy >= _config.NearFullThreshold) _nearFullTime += deltaTime;

            if (_config.SupplyInterval > 0f)
            {
                _sinceSupply += deltaTime;
                if (_sinceSupply >= _config.SupplyInterval)
                {
                    _sinceSupply = 0f;
                    SpawnRaw();
                }
            }

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
                    _dragging = TryGetCell(command.Position, out _dragFromX, out _dragFromY)
                             && !_grid.Get(_dragFromX, _dragFromY).IsEmpty;
                    break;

                case InputPhase.Ended:
                    if (!_dragging) break;
                    _dragging = false;

                    if (!TryGetCell(command.Position, out var tx, out var ty)) break;
                    TryMerge(_dragFromX, _dragFromY, tx, ty, feedback);
                    break;

                case InputPhase.Canceled:
                    _dragging = false;
                    break;
            }
        }

        /// <summary>
        /// Any distance, not just neighbours: the point is choosing what to
        /// combine, not executing a precise gesture.
        /// </summary>
        private void TryMerge(int ax, int ay, int bx, int by, IFeedbackChannel feedback)
        {
            // Dropping a piece back where it started is a change of mind.
            if (ax == bx && ay == by) return;

            var source = _grid.Get(ax, ay);
            var target = _grid.Get(bx, by);
            var at = CellCenter(bx, by);

            if (source.IsEmpty) return;

            if (target.IsEmpty)
            {
                // Moving a piece to free space is legitimate tidying, not an
                // error, so it costs a move but is not a failed action.
                _grid.Set(bx, by, source);
                _grid.Set(ax, ay, Piece.Empty);
                return;
            }

            if (!source.Matches(target) || source.Tier >= _config.MaxTier)
            {
                _invalidMerges++;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, at, 0.3f));
                return;
            }

            var promoted = source.Promoted();
            _grid.Set(ax, ay, Piece.Empty);
            _grid.Set(bx, by, promoted);

            _merges++;
            if (promoted.Tier > _highestTier) _highestTier = promoted.Tier;

            Score += (int)Math.Round(
                _config.ScorePerMerge * Math.Pow(_config.TierScoreMultiplier, source.Tier));

            feedback.Emit(new FeedbackCue(
                ComboScale.ForSize(2 + promoted.Tier * 2), at,
                MathK.Clamp01(promoted.Tier / 3f), promoted.Tier));

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));

            TryFillOrder(bx, by, promoted, at, feedback);
        }

        /// <summary>
        /// A product that matches a waiting order travels to the factory by
        /// itself. Making the player carry it there would add a chore, not a
        /// decision.
        /// </summary>
        private void TryFillOrder(int cx, int cy, Piece piece, Vec2 at, IFeedbackChannel feedback)
        {
            if (piece.Tier < _config.OrderTier) return;

            for (var i = 0; i < _orders.Length; i++)
            {
                if (_orders[i].Filled) continue;
                if (_orders[i].Family != piece.Family) continue;
                if (piece.Tier < _orders[i].Tier) continue;

                _grid.Set(cx, cy, Piece.Empty);

                _ordersCompleted++;
                _orderTimeTotal += _elapsed - _orders[i].IssuedAt;
                _earned[piece.Family] += 1;
                Score += _config.ScorePerOrder;
                Objective.Add("ORDERS");

                _orders[i] = NewOrder(_elapsed);

                feedback.Emit(new FeedbackCue(FeedbackCueKind.ProductCreated, at, 1f, piece.Tier));

                if (Objective.Completed && _objectiveCompletedAt < 0f)
                {
                    _objectiveCompletedAt = _elapsed;
                    feedback.Emit(new FeedbackCue(FeedbackCueKind.ObjectiveComplete, at));
                }

                return;
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
            into["merge.merges"] = _merges;
            into["merge.invalidMerges"] = _invalidMerges;
            into["merge.highestTier"] = _highestTier;
            into["merge.ordersCompleted"] = _ordersCompleted;
            into["merge.finalOccupancy"] = _grid?.Occupancy ?? 0f;
            into["merge.averageOccupancy"] =
                _occupancySamples == 0 ? 0d : _occupancySum / _occupancySamples;
            into["merge.nearFullSeconds"] = _nearFullTime;
            into["merge.averageOrderSeconds"] =
                _ordersCompleted == 0 ? 0d : _orderTimeTotal / _ordersCompleted;
            into["merge.objectiveCompletedAt"] = _objectiveCompletedAt;
        }
    }
}
