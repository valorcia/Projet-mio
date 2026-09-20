using System;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// Accumulates the M0.2 metric set for a single session. Owned by
    /// <see cref="PrototypeRunner"/> so every rule set reports the same numbers
    /// without each one re-implementing the bookkeeping — without that,
    /// comparing four prototypes would be meaningless, which is the entire
    /// point of this milestone.
    /// </summary>
    public sealed class SessionMetricsRecorder
    {
        private const float NotYet = -1f;

        private float _elapsed;
        private float _timeToFirstInput = NotYet;
        private float _timeToFirstSuccess = NotYet;
        private int _totalInputs;
        private bool _running;

        public void Begin(
            PrototypeId prototypeId,
            string testerId,
            int seed,
            int attemptIndex,
            bool replayRequested,
            bool replayWithoutPrompt)
        {
            SessionId = Guid.NewGuid().ToString("N");
            TesterId = testerId ?? string.Empty;
            PrototypeId = prototypeId;
            Seed = seed;
            AttemptIndex = attemptIndex;
            ReplayRequested = replayRequested;
            ReplayWithoutPrompt = replayWithoutPrompt;
            SessionStartUtc = DateTime.UtcNow;

            _elapsed = 0f;
            _timeToFirstInput = NotYet;
            _timeToFirstSuccess = NotYet;
            _totalInputs = 0;
            _running = true;
        }

        public string SessionId { get; private set; } = string.Empty;
        public string TesterId { get; private set; } = string.Empty;
        public PrototypeId PrototypeId { get; private set; }
        public int Seed { get; private set; }
        public int AttemptIndex { get; private set; }
        public bool ReplayRequested { get; private set; }
        public bool ReplayWithoutPrompt { get; private set; }
        public DateTime SessionStartUtc { get; private set; }
        public float Elapsed => _elapsed;
        public int TotalInputs => _totalInputs;

        public void Advance(float deltaTime)
        {
            if (!_running) return;
            if (deltaTime > 0f) _elapsed += deltaTime;
        }

        /// <summary>
        /// Called for each distinct touch, meaning a pointer press. Moves and
        /// releases are not counted: a single drag is one input, not the
        /// hundred move events it generates, and a release expresses no new
        /// intention. Keeping the definition identical across prototypes is
        /// what lets a tap game and a drag game be compared at all.
        /// </summary>
        public void NoteInput()
        {
            if (!_running) return;

            _totalInputs++;
            if (_timeToFirstInput < 0f) _timeToFirstInput = _elapsed;
        }

        /// <summary>
        /// Called the first time an action works. The gap from first input to
        /// first success is how long the prototype took to become legible.
        /// </summary>
        public void NoteFirstSuccess()
        {
            if (!_running) return;
            if (_timeToFirstSuccess < 0f) _timeToFirstSuccess = _elapsed;
        }

        public MetricReport Build(
            SessionStatus status,
            int score,
            Objective objective,
            int successfulActions,
            int failedActions,
            ResourceBundle resourcesEarned)
        {
            _running = false;

            return new MetricReport
            {
                SessionId = SessionId,
                TesterId = TesterId,
                PrototypeId = PrototypeId,
                Seed = Seed,
                SessionStartUtc = SessionStartUtc,
                SessionEndUtc = DateTime.UtcNow,
                SessionDuration = _elapsed,
                TimeToFirstInput = _timeToFirstInput,
                TimeToFirstSuccess = _timeToFirstSuccess,
                TotalInputs = _totalInputs,
                SuccessfulActions = successfulActions,
                FailedActions = failedActions,
                Score = score,
                ObjectiveProgress = objective?.Progress01 ?? 0f,
                ObjectiveCompleted = objective?.Completed ?? false,
                CompletionStatus = status,
                ResourcesEarned = resourcesEarned,
                ReplayRequested = ReplayRequested,
                ReplayWithoutPrompt = ReplayWithoutPrompt,
                AttemptIndex = AttemptIndex
            };
        }
    }
}
