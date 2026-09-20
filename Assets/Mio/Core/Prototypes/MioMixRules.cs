using System;
using System.Collections.Generic;
using Mio.Core.Board;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Prototypes
{
    /// <summary>
    /// What a MIO power does. Deliberately an enum plus data rather than four
    /// classes: the spec warns against hard-coding gameplay around four
    /// permanent powers, and a definition list is the smallest thing that lets
    /// us add, remove or retune powers without touching the rules.
    /// </summary>
    public enum MioPowerKind
    {
        /// <summary>Grows raw resources into worked ones.</summary>
        Seed = 0,

        /// <summary>Upgrades a handful of the lowest-tier pieces.</summary>
        Cub = 1,

        /// <summary>Reorganises the board into a chain reaction.</summary>
        Orb = 2,

        /// <summary>Helps whichever objective line is furthest behind.</summary>
        Mod = 3
    }

    [Serializable]
    public sealed class MioPowerDefinition
    {
        /// <summary>Placeholder label shown on the meter, e.g. "SEED".</summary>
        public string Id = "SEED";

        public MioPowerKind Kind = MioPowerKind.Seed;

        /// <summary>How many pieces the power touches. Tuning lives here.</summary>
        public int Magnitude = 5;

        public MioPowerDefinition() { }

        public MioPowerDefinition(string id, MioPowerKind kind, int magnitude)
        {
            Id = id;
            Kind = kind;
            Magnitude = magnitude;
        }
    }

    [Serializable]
    public sealed class MioMixConfig
    {
        public float Duration = 60f;

        public int Width = 7;
        public int Height = 8;
        public int FamilyCount = 3;
        public int CombineSize = 3;
        public int ProductTier = 3;
        public int MaxCascadeDepth = 8;

        public int ScorePerCombine = 20;
        public float TierScoreMultiplier = 2.5f;

        /// <summary>Bonus for delivering a finished product to the world.</summary>
        public int ScorePerProduct = 250;

        public int GoalCottonProducts = 3;
        public int GoalWoodProducts = 2;
        public int GoalMetalProducts = 1;

        /// <summary>Meter charge per combination, before the tier bonus.</summary>
        public float MioChargePerCombine = 0.14f;

        /// <summary>Extra charge per tier, so deep work charges MIO faster.</summary>
        public float MioChargePerTier = 0.06f;

        public bool MioPowerEnabled = true;

        public MioPowerDefinition[] Powers =
        {
            new MioPowerDefinition("SEED", MioPowerKind.Seed, 5),
            new MioPowerDefinition("CUB", MioPowerKind.Cub, 4),
            new MioPowerDefinition("ORB", MioPowerKind.Orb, 0),
            new MioPowerDefinition("MOD", MioPowerKind.Mod, 4)
        };

        public float FieldMinX = 0.06f;
        public float FieldMaxX = 0.94f;
        public float FieldMinY = 0.22f;
        public float FieldMaxY = 0.80f;

        /// <summary>Touch band for the MIO meter, along the bottom.</summary>
        public float MioButtonY = 0.11f;
        public float MioButtonHalfHeight = 0.06f;

        public MioMixConfig Clone()
        {
            var copy = (MioMixConfig)MemberwiseClone();
            copy.Powers = (MioPowerDefinition[])Powers.Clone();
            return copy;
        }
    }

    /// <summary>
    /// PROTOTYPE D — MIO MIX.
    ///
    /// The same combining verb as STACK, aimed at a different feeling. Here the
    /// point is not the chain: it is that the chain <b>produces something</b>.
    /// A finished product is not another board piece — it is celebrated, taken
    /// off the board, and sent to a world output area that is quietly
    /// preparing the connection to a town that does not exist yet.
    ///
    /// The experiment is whether "I made something" beats "I cleared
    /// something", and whether charging a MIO meter toward a power creates any
    /// attachment to MIO as an idea.
    /// </summary>
    public sealed class MioMixRules : IPrototypeRules
    {
        private readonly MioMixConfig _config;
        private readonly List<int> _scratch = new List<int>();

        private PieceGrid _grid;
        private DeterministicRng _rng;

        private float _elapsed;
        private int _moves;
        private int _combos;
        private int _invalidMoves;
        private int _deepestCascade;
        private int _highestTier;
        private int _productsDelivered;
        private int _powersReady;
        private int _powersUsed;
        private ResourceBundle _earned;
        private float _objectiveCompletedAt = -1f;
        private float _firstProductAt = -1f;

        private bool _dragging;
        private int _dragFromX, _dragFromY;

        public MioMixRules(MioMixConfig config)
        {
            _config = config ?? new MioMixConfig();
        }

        public PrototypeId Id => PrototypeId.MioMix;
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

        /// <summary>Total products sent to the world output this session.</summary>
        public int ProductsDelivered => _productsDelivered;

        /// <summary>MIO meter fill, 0..1.</summary>
        public float MioCharge { get; private set; }

        public bool MioReady => _config.MioPowerEnabled && MioCharge >= 1f;

        /// <summary>The power currently armed, or null when the meter is not full.</summary>
        public MioPowerDefinition ArmedPower { get; private set; }

        public int DragFromIndex => _dragging ? _grid.Index(_dragFromX, _dragFromY) : -1;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _grid = new PieceGrid(_config.Width, _config.Height);
            _grid.FillAvoidingGroups(_rng, _config.FamilyCount, _config.CombineSize);

            Objective = new Objective(
                new ObjectiveGoal("COTTON", _config.GoalCottonProducts),
                new ObjectiveGoal("WOOD", _config.GoalWoodProducts),
                new ObjectiveGoal("METAL", _config.GoalMetalProducts));

            _elapsed = 0f;
            _moves = 0; _combos = 0; _invalidMoves = 0;
            _deepestCascade = 0; _highestTier = 0; _productsDelivered = 0;
            _powersReady = 0; _powersUsed = 0;
            _earned = ResourceBundle.Empty;
            _objectiveCompletedAt = -1f;
            _firstProductAt = -1f;
            _dragging = false;
            MioCharge = 0f;
            ArmedPower = null;
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
                    if (IsMioButton(command.Position) && MioReady)
                    {
                        FirePower(feedback);
                        return;
                    }

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

        public bool IsMioButton(Vec2 position)
        {
            return position.Y >= _config.MioButtonY - _config.MioButtonHalfHeight
                && position.Y <= _config.MioButtonY + _config.MioButtonHalfHeight;
        }

        private void TrySwap(int ax, int ay, int bx, int by, IFeedbackChannel feedback)
        {
            if (ax == bx && ay == by) return;

            var adjacent = Math.Abs(ax - bx) + Math.Abs(ay - by) == 1;
            if (!adjacent) return;

            _moves++;

            var a = _grid.Get(ax, ay);
            var b = _grid.Get(bx, by);
            if (a.IsEmpty || b.IsEmpty) return;

            _grid.Set(ax, ay, b);
            _grid.Set(bx, by, a);

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

            ChargeMio(piece.Tier, at, feedback);

            feedback.Emit(new FeedbackCue(ComboScale.ForSize(size), at, ComboScale.Intensity(size), size));

            var cascade = ComboScale.ForCascadeDepth(depth);
            if (cascade.HasValue)
            {
                feedback.Emit(new FeedbackCue(cascade.Value, at, MathK.Clamp01(depth / 4f), depth));
                if (depth > _deepestCascade) _deepestCascade = depth;
            }

            if (promoted.Tier >= _config.ProductTier) DeliverProduct(piece.Family, at, feedback);
            else _grid.Set(cx, cy, promoted);

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));
            return true;
        }

        /// <summary>
        /// A finished object leaves the board for the world output. This is the
        /// whole emotional claim of the prototype, so it gets its own cue at
        /// full intensity rather than sharing the combination cue.
        /// </summary>
        private void DeliverProduct(ResourceKind family, Vec2 at, IFeedbackChannel feedback)
        {
            _productsDelivered++;
            _earned[family] += 1;
            Score += _config.ScorePerProduct;
            Objective.Add(PopRules.LabelOf(family));

            if (_firstProductAt < 0f) _firstProductAt = _elapsed;

            feedback.Emit(new FeedbackCue(FeedbackCueKind.ProductCreated, at, 1f, _config.ProductTier));

            if (Objective.Completed && _objectiveCompletedAt < 0f)
            {
                _objectiveCompletedAt = _elapsed;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.ObjectiveComplete, at));
            }
        }

        private void ChargeMio(int tier, Vec2 at, IFeedbackChannel feedback)
        {
            if (!_config.MioPowerEnabled || MioReady) return;

            MioCharge = MathK.Clamp01(
                MioCharge + _config.MioChargePerCombine + _config.MioChargePerTier * tier);

            if (MioCharge < 1f) return;

            ArmedPower = PickPower();
            _powersReady++;
            feedback.Emit(new FeedbackCue(FeedbackCueKind.MioPowerReady, at, 1f, (int)(ArmedPower?.Kind ?? 0)));
        }

        private MioPowerDefinition PickPower()
        {
            var powers = _config.Powers;
            if (powers == null || powers.Length == 0) return null;

            // Drawn from the seeded generator so a session stays reproducible,
            // and so a tester sees more than one power in a sitting.
            return powers[_rng.Range(0, powers.Length)];
        }

        private void FirePower(IFeedbackChannel feedback)
        {
            var power = ArmedPower;
            MioCharge = 0f;
            ArmedPower = null;

            if (power == null) return;

            _powersUsed++;
            var at = new Vec2(0.5f, _config.MioButtonY);
            feedback.Emit(new FeedbackCue(FeedbackCueKind.MioPowerUsed, at, 1f, (int)power.Kind));

            switch (power.Kind)
            {
                case MioPowerKind.Seed: PromoteLowest(power.Magnitude, 0, feedback); break;
                case MioPowerKind.Cub: PromoteLowest(power.Magnitude, -1, feedback); break;
                case MioPowerKind.Mod: PromoteNeediestFamily(power.Magnitude, feedback); break;

                case MioPowerKind.Orb:
                    // A controlled reorganisation: reshuffle, then let the
                    // settle loop resolve whatever it created.
                    _grid.Reshuffle(_rng, _config.FamilyCount, _config.CombineSize, sameTier: true);
                    break;
            }

            Settle(feedback, depth: 2);
        }

        /// <summary>
        /// Promotes up to <paramref name="count"/> pieces. When
        /// <paramref name="onlyTier"/> is negative any tier below product is
        /// eligible; otherwise only that exact tier.
        /// </summary>
        private void PromoteLowest(int count, int onlyTier, IFeedbackChannel feedback)
        {
            var promoted = 0;

            for (var i = 0; i < _grid.CellCount && promoted < count; i++)
            {
                var piece = _grid.At(i);
                if (piece.IsEmpty || piece.Tier >= _config.ProductTier - 1) continue;
                if (onlyTier >= 0 && piece.Tier != onlyTier) continue;

                _grid.SetAt(i, piece.Promoted());
                promoted++;
            }
        }

        private void PromoteNeediestFamily(int count, IFeedbackChannel feedback)
        {
            // Whichever objective line is furthest from done, so the power
            // feels like it understood what the player is trying to do.
            var neediest = ResourceKind.Cotton;
            var worst = float.MaxValue;

            for (var i = 0; i < Objective.Count; i++)
            {
                var goal = Objective[i];
                if (goal.Required <= 0) continue;

                var ratio = goal.Counted / (float)goal.Required;
                if (ratio >= worst) continue;

                worst = ratio;
                neediest = ResourceKinds.At(i);
            }

            var promoted = 0;
            for (var i = 0; i < _grid.CellCount && promoted < count; i++)
            {
                var piece = _grid.At(i);
                if (piece.IsEmpty || piece.Family != neediest) continue;
                if (piece.Tier >= _config.ProductTier - 1) continue;

                _grid.SetAt(i, piece.Promoted());
                promoted++;
            }
        }

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
            into["miomix.moves"] = _moves;
            into["miomix.combinations"] = _combos;
            into["miomix.invalidMoves"] = _invalidMoves;
            into["miomix.maxCascade"] = _deepestCascade;
            into["miomix.highestTier"] = _highestTier;
            into["miomix.productsDelivered"] = _productsDelivered;
            into["miomix.firstProductAt"] = _firstProductAt;
            into["miomix.mioPowersReady"] = _powersReady;
            into["miomix.mioPowersUsed"] = _powersUsed;
            into["miomix.objectiveCompletedAt"] = _objectiveCompletedAt;
        }
    }
}
