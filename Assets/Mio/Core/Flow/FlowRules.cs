using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Session;

namespace Mio.Core.Flow
{
    public enum FlowGateKind
    {
        Collect = 0,
        Hazard = 1
    }

    public struct FlowGate
    {
        /// <summary>Time, in run-seconds, at which this gate reaches the head.</summary>
        public float TriggerTime;

        public float CenterX;
        public float HalfWidth;
        public FlowGateKind Kind;
        public bool Resolved;

        /// <summary>True when the head was inside at resolve time.</summary>
        public bool WasHit;
    }

    /// <summary>
    /// FLOW: hold a finger on the screen and steer the stream left and right.
    /// Pass through the orbs to fill the meter, slide around the blocks.
    ///
    /// One finger, no buttons, no text. The whole run is a single continuous
    /// drag, which is why it reads without a tutorial: the stream follows your
    /// thumb the instant you touch, and that is the entire control scheme.
    /// </summary>
    public sealed class FlowRules : IPrototypeRules
    {
        private readonly FlowConfig _config;
        private readonly List<FlowGate> _gates = new List<FlowGate>();

        private float _elapsed;
        private float _headX;
        private float _targetX;
        private float _progress;
        private int _collected;
        private int _missed;
        private int _hazardsHit;

        public FlowRules(FlowConfig config)
        {
            _config = config ?? new FlowConfig();
        }

        public PrototypeId Id => PrototypeId.Flow;
        public SessionStatus Status { get; private set; } = SessionStatus.Idle;
        public int Score { get; private set; }
        public int SuccessfulActions => _collected;
        public int FailedActions => _missed + _hazardsHit;
        public float Progress01 => _progress;
        public float TimeRemaining => MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        /// <summary>Current head position, for the view.</summary>
        public float HeadX => _headX;
        public float HeadY => _config.HeadY;

        /// <summary>The generated track, for the view. Read-only by contract.</summary>
        public IReadOnlyList<FlowGate> Gates => _gates;

        public float Elapsed => _elapsed;

        /// <summary>Where a gate sits right now, in play-field space.</summary>
        public float GateY(in FlowGate gate)
        {
            return _config.HeadY + (gate.TriggerTime - _elapsed) * _config.ScrollSpeed;
        }

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _elapsed = 0f;
            _headX = 0.5f;
            _targetX = 0.5f;
            _progress = 0f;
            Score = 0;
            _collected = 0;
            _missed = 0;
            _hazardsHit = 0;
            Status = SessionStatus.Playing;

            GenerateTrack(new DeterministicRng(seed));

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, new Vec2(_headX, _config.HeadY)));
        }

        /// <summary>
        /// The track is generated up front rather than streamed, so a seed fully
        /// describes a run. That is what lets us replay a session from its
        /// metrics and A/B two tunings against an identical track.
        /// </summary>
        private void GenerateTrack(DeterministicRng rng)
        {
            _gates.Clear();

            var lo = _config.EdgeMargin;
            var hi = 1f - _config.EdgeMargin;
            if (hi < lo) { lo = hi = 0.5f; }

            // How far the head can physically travel between two gates. Every
            // gate is placed within this of the last one, so the track is always
            // beatable and "unfair" never becomes a reason a playtest fails.
            var reach = _config.FollowSpeed * _config.GateInterval;

            var requiredX = 0.5f;
            var count = _config.GateInterval <= 0f
                ? 0
                : (int)((_config.Duration - _config.FirstGateDelay) / _config.GateInterval);

            for (var i = 0; i < count; i++)
            {
                var min = MathK.Clamp(requiredX - reach, lo, hi);
                var max = MathK.Clamp(requiredX + reach, lo, hi);
                requiredX = max <= min ? min : rng.Range(min, max);

                var gate = new FlowGate
                {
                    TriggerTime = _config.FirstGateDelay + i * _config.GateInterval,
                    Resolved = false
                };

                if (rng.Chance(_config.HazardChance))
                {
                    gate.Kind = FlowGateKind.Hazard;
                    gate.HalfWidth = _config.HazardHalfWidth;
                    gate.CenterX = PlaceHazardAway(requiredX, gate.HalfWidth, lo, hi);
                }
                else
                {
                    gate.Kind = FlowGateKind.Collect;
                    gate.HalfWidth = _config.CollectHalfWidth;
                    gate.CenterX = requiredX;
                }

                _gates.Add(gate);
            }
        }

        /// <summary>
        /// Puts a hazard block to one side of <paramref name="safeX"/> so that
        /// standing on safeX is guaranteed to clear it.
        /// </summary>
        private float PlaceHazardAway(float safeX, float halfWidth, float lo, float hi)
        {
            var offset = halfWidth + _config.SafeMargin;

            // Prefer the side with more room, so the block stays on screen.
            var right = safeX + offset;
            var left = safeX - offset;

            var rightFits = right + halfWidth <= 1f;
            var leftFits = left - halfWidth >= 0f;

            float center;
            if (rightFits && leftFits) center = safeX < 0.5f ? right : left;
            else if (rightFits) center = right;
            else if (leftFits) center = left;
            else center = right; // Field narrower than the block; cannot happen with sane config.

            return MathK.Clamp(center, lo, hi);
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing) return;

            // Any press or drag steers. Lifting the finger leaves the stream
            // where it is rather than snapping it back, so a re-grip is forgiving.
            if (command.IsPressed)
            {
                _targetX = MathK.Clamp01(command.Position.X);
            }
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != SessionStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;
            _headX = MathK.MoveTowards(_headX, _targetX, _config.FollowSpeed * deltaTime);

            ResolveDueGates(feedback);

            if (_progress >= 1f)
            {
                _progress = 1f;
                Status = SessionStatus.Won;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Win, new Vec2(_headX, _config.HeadY)));
                return;
            }

            if (_elapsed >= _config.Duration)
            {
                Status = SessionStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, new Vec2(_headX, _config.HeadY)));
            }
        }

        private void ResolveDueGates(IFeedbackChannel feedback)
        {
            for (var i = 0; i < _gates.Count; i++)
            {
                var gate = _gates[i];
                if (gate.Resolved) continue;

                // Gates are ordered by trigger time, so the first pending gate in
                // the future means nothing after it is due either.
                if (gate.TriggerTime > _elapsed) break;

                var inside = _headX >= gate.CenterX - gate.HalfWidth
                          && _headX <= gate.CenterX + gate.HalfWidth;

                gate.Resolved = true;
                gate.WasHit = inside;
                _gates[i] = gate;

                var at = new Vec2(gate.CenterX, _config.HeadY);

                if (gate.Kind == FlowGateKind.Collect)
                {
                    if (inside)
                    {
                        _collected++;
                        Score += _config.ScorePerCollect;
                        AddProgress(_config.ProgressPerCollect, feedback, at);
                        feedback.Emit(new FeedbackCue(FeedbackCueKind.Collect, at, 1f, _config.ScorePerCollect));
                    }
                    else
                    {
                        _missed++;
                        AddProgress(-_config.ProgressPenaltyMiss, feedback, at);
                        feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, at, 0.35f));
                    }
                }
                else if (inside)
                {
                    _hazardsHit++;
                    AddProgress(-_config.ProgressPenaltyHazard, feedback, at);
                    feedback.Emit(new FeedbackCue(FeedbackCueKind.Hazard, new Vec2(_headX, _config.HeadY)));
                }
            }
        }

        private void AddProgress(float delta, IFeedbackChannel feedback, Vec2 at)
        {
            var next = MathK.Clamp01(_progress + delta);
            if (next == _progress) return;

            _progress = next;
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, _progress));
        }

        /// <summary>Orbs taken. Public for the view; not part of the contract.</summary>
        public int Collected => _collected;

        /// <summary>Orbs missed.</summary>
        public int Missed => _missed;

        /// <summary>Blocks struck.</summary>
        public int HazardsHit => _hazardsHit;
    }
}
