using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// Accumulates the M0 metric set for a single attempt. Owned by
    /// <see cref="PrototypeRunner"/> so that every prototype reports the same
    /// numbers without each one re-implementing the bookkeeping.
    /// </summary>
    public sealed class SessionMetricsRecorder
    {
        private const float NoInteraction = -1f;

        private float _elapsed;
        private float _firstInteractionTime = NoInteraction;
        private bool _running;

        public void Begin(PrototypeId prototype, int seed, int attemptIndex)
        {
            Prototype = prototype;
            Seed = seed;
            AttemptIndex = attemptIndex;
            _elapsed = 0f;
            _firstInteractionTime = NoInteraction;
            _running = true;
        }

        public PrototypeId Prototype { get; private set; }
        public int Seed { get; private set; }
        public int AttemptIndex { get; private set; }
        public float Elapsed => _elapsed;

        public void Advance(float deltaTime)
        {
            if (!_running) return;
            if (deltaTime > 0f) _elapsed += deltaTime;
        }

        /// <summary>
        /// Called on every touch. Only the first one moves the needle, but it is
        /// cheap to call unconditionally so callers cannot forget.
        /// </summary>
        public void NoteInteraction()
        {
            if (!_running) return;
            if (_firstInteractionTime < 0f) _firstInteractionTime = _elapsed;
        }

        public MetricReport Build(
            PrototypeStatus status,
            int score,
            int successfulActions,
            int failedActions)
        {
            _running = false;

            return new MetricReport
            {
                Prototype = Prototype,
                Seed = Seed,
                AttemptIndex = AttemptIndex,
                SessionDuration = _elapsed,
                FirstInteractionTime = _firstInteractionTime,
                SuccessfulActions = successfulActions,
                FailedActions = failedActions,
                Completed = status == PrototypeStatus.Won,
                Status = status,
                Score = score
            };
        }
    }
}
