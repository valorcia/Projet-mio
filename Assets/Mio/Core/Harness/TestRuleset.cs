using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Harness
{
    /// <summary>
    /// The architecture validation rule set. <b>This is not one of the three
    /// game prototypes and must never ship.</b>
    ///
    /// Its only job is to exercise every seam of the shared foundation exactly
    /// once: it receives normalised input, counts successful and failed
    /// actions, emits semantic feedback cues, raises a score, reports progress,
    /// and completes after a configurable number of successes so the runner can
    /// pay a reward, bank it in the wallet and produce a metric report.
    ///
    /// Deliberately the dumbest rule set that touches all of that: tap the
    /// square. If this works end to end, the foundation is sound and the real
    /// prototypes only have to implement their own fun.
    /// </summary>
    public sealed class TestRuleset : IPrototypeRules
    {
        private readonly TestRulesetConfig _config;

        private DeterministicRng _rng;
        private float _elapsed;
        private int _hits;
        private int _misses;

        public TestRuleset(TestRulesetConfig config)
        {
            _config = config ?? new TestRulesetConfig();
        }

        public PrototypeId Id => PrototypeId.TestHarness;

        /// <summary>One goal, so the harness exercises the objective seam too.</summary>
        public Objective Objective { get; private set; } = new Objective();

        public float Duration => _config.Duration;

        /// <summary>The harness produces nothing; it only proves the plumbing.</summary>
        public ResourceBundle ResourcesEarned => ResourceBundle.Empty;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public int Score { get; private set; }
        public int SuccessfulActions => _hits;
        public int FailedActions => _misses;

        public float Progress01
        {
            get
            {
                if (_config.RequiredSuccesses <= 0) return 1f;
                return MathK.Clamp01(_hits / (float)_config.RequiredSuccesses);
            }
        }

        /// <summary>Current target centre, in play-field space. For the view.</summary>
        public Vec2 Target { get; private set; } = new Vec2(0.5f, 0.5f);

        public float TargetHalfSize => _config.TargetHalfSize;

        /// <summary>Seconds left, or float.PositiveInfinity when untimed.</summary>
        public float TimeRemaining =>
            _config.Duration <= 0f
                ? float.PositiveInfinity
                : MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            // Same seed, same sequence of target positions: the harness is as
            // reproducible as a real prototype must be.
            _rng = new DeterministicRng(seed);
            Objective = new Objective(new ObjectiveGoal("TAPS", _config.RequiredSuccesses));

            _elapsed = 0f;
            _hits = 0;
            _misses = 0;
            Score = 0;
            Status = SessionStatus.Playing;

            MoveTarget();
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, Target));
        }

        private void MoveTarget()
        {
            var lo = _config.EdgeMargin;
            var hi = 1f - _config.EdgeMargin;
            if (hi <= lo) { lo = hi = 0.5f; }

            Target = new Vec2(_rng.Range(lo, hi), _rng.Range(lo, hi));
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;

            if (_config.Duration > 0f && _elapsed >= _config.Duration)
            {
                Status = SessionStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, Target));
            }
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing) return;

            // Resolve on press so the screen answers the finger immediately,
            // and so one drag cannot register as many actions.
            if (command.Phase != InputPhase.Began) return;

            var p = command.Position;
            var inside = p.X >= Target.X - _config.TargetHalfSize
                      && p.X <= Target.X + _config.TargetHalfSize
                      && p.Y >= Target.Y - _config.TargetHalfSize
                      && p.Y <= Target.Y + _config.TargetHalfSize;

            if (!inside)
            {
                _misses++;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, p, 0.3f));
                return;
            }

            _hits++;
            Objective.Add("TAPS");
            Score += _config.ScorePerSuccess;

            var intensity = MathK.Clamp01(_hits / (float)System.Math.Max(1, _config.RequiredSuccesses));
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Success, Target, intensity, _config.ScorePerSuccess));
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, Target, Progress01));

            if (_hits >= _config.RequiredSuccesses)
            {
                Status = SessionStatus.Won;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Win, Target));
                return;
            }

            MoveTarget();
        }

        public void CollectCustomMetrics(IDictionary<string, double> into)
        {
            into["harness.hits"] = _hits;
            into["harness.misses"] = _misses;
        }
    }
}
