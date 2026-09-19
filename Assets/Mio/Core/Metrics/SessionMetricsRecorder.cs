using System;
using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// Accumulates the M0.1 metric set for a single session. Owned by
    /// <see cref="PrototypeRunner"/> so every rule set reports the same numbers
    /// without each one re-implementing the bookkeeping — without that,
    /// comparing two prototypes' data would be meaningless.
    /// </summary>
    public sealed class SessionMetricsRecorder
    {
        private const float NoInput = -1f;

        private float _elapsed;
        private float _timeToFirstInput = NoInput;
        private int _inputCount;
        private bool _running;

        public void Begin(PrototypeId prototypeId, int seed, int attemptIndex, bool replayRequested)
        {
            SessionId = Guid.NewGuid().ToString("N");
            PrototypeId = prototypeId;
            Seed = seed;
            AttemptIndex = attemptIndex;
            ReplayRequested = replayRequested;
            SessionStartUtc = DateTime.UtcNow;

            _elapsed = 0f;
            _timeToFirstInput = NoInput;
            _inputCount = 0;
            _running = true;
        }

        public string SessionId { get; private set; } = string.Empty;
        public PrototypeId PrototypeId { get; private set; }
        public int Seed { get; private set; }
        public int AttemptIndex { get; private set; }
        public bool ReplayRequested { get; private set; }
        public DateTime SessionStartUtc { get; private set; }
        public float Elapsed => _elapsed;
        public int InputCount => _inputCount;

        public void Advance(float deltaTime)
        {
            if (!_running) return;
            if (deltaTime > 0f) _elapsed += deltaTime;
        }

        /// <summary>
        /// Called for each distinct touch, meaning a pointer press. Moves and
        /// releases are not counted: a single drag is one input, not the
        /// hundred move events it generates, and a release expresses no new
        /// intention.
        /// </summary>
        public void NoteInput()
        {
            if (!_running) return;

            _inputCount++;
            if (_timeToFirstInput < 0f) _timeToFirstInput = _elapsed;
        }

        public MetricReport Build(
            SessionStatus status,
            int score,
            float progress,
            int successfulActions,
            int failedActions)
        {
            _running = false;

            return new MetricReport
            {
                SessionId = SessionId,
                PrototypeId = PrototypeId,
                Seed = Seed,
                SessionStartUtc = SessionStartUtc,
                SessionEndUtc = DateTime.UtcNow,
                SessionDuration = _elapsed,
                TimeToFirstInput = _timeToFirstInput,
                InputCount = _inputCount,
                SuccessfulActions = successfulActions,
                FailedActions = failedActions,
                Score = score,
                Progress = progress,
                CompletionStatus = status,
                ReplayRequested = ReplayRequested,
                AttemptIndex = AttemptIndex
            };
        }
    }
}
