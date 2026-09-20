using System;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using Mio.Core.Profile;

namespace Mio.Core.Session
{
    /// <summary>
    /// Drives one <see cref="IPrototypeRules"/> through a session and produces
    /// the metric report and the payout.
    ///
    /// Every rule set shares this, so "session duration", "time to first input"
    /// and "replay" mean exactly the same thing in every data set. It also owns
    /// the seed, which is what makes a session reproducible from its report.
    /// </summary>
    public sealed class PrototypeRunner
    {
        private readonly IPrototypeRules _rules;
        private readonly IMetricsSink _sink;
        private readonly RewardTable _rewards;
        private readonly PlayerWallet _wallet;
        private readonly SessionMetricsRecorder _metrics = new SessionMetricsRecorder();

        private int _attemptIndex = -1;
        private bool _reported;
        private bool _notedFirstSuccess;

        /// <summary>Local, non-personal tester label written into every report.</summary>
        public string TesterId { get; set; } = string.Empty;

        public PrototypeRunner(
            IPrototypeRules rules,
            IMetricsSink sink = null,
            RewardTable rewards = null,
            PlayerWallet wallet = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _sink = sink ?? NullMetricsSink.Instance;
            _rewards = rewards ?? RewardTable.Empty;

            // Optional: a headless test can run sessions without any
            // persistence at all.
            _wallet = wallet;
        }

        public IPrototypeRules Rules => _rules;
        public SessionStatus Status => _rules.Status;

        /// <summary>How many sessions have been started. 0 before the first.</summary>
        public int AttemptCount => _attemptIndex + 1;

        /// <summary>Report for the most recently resolved session, else null.</summary>
        public MetricReport LastReport { get; private set; }

        /// <summary>Payout for the most recently resolved session.</summary>
        public ResourceBundle LastReward { get; private set; }

        /// <summary>Raised when a session resolves, after the sink has been fed.</summary>
        public event Action<MetricReport, ResourceBundle> SessionFinished;

        /// <summary>Starts the first session of a sitting.</summary>
        public void Begin(int seed, IFeedbackChannel feedback = null)
        {
            BeginInternal(seed, false, false, feedback);
        }

        /// <summary>
        /// Starts a session in response to the player asking to play again.
        ///
        /// <paramref name="withoutPrompt"/> marks the stronger "one more"
        /// signal: the player reached for the board before any replay control
        /// was offered. The two flags are recorded separately because they are
        /// not the same evidence.
        /// </summary>
        public void RequestReplay(int seed, bool withoutPrompt = false, IFeedbackChannel feedback = null)
        {
            BeginInternal(seed, !withoutPrompt, withoutPrompt, feedback);
        }

        private void BeginInternal(
            int seed,
            bool replayRequested,
            bool replayWithoutPrompt,
            IFeedbackChannel feedback)
        {
            var fx = feedback ?? NullFeedbackChannel.Instance;

            // An unresolved previous session counts as abandoned, so restarting
            // mid-session cannot silently drop a data point.
            if (_rules.Status == SessionStatus.Playing) Abandon();

            _attemptIndex++;
            _reported = false;
            _notedFirstSuccess = false;
            LastReport = null;
            LastReward = ResourceBundle.Empty;

            _metrics.Begin(_rules.Id, TesterId, seed, _attemptIndex, replayRequested, replayWithoutPrompt);
            _rules.Begin(seed, fx);
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback = null)
        {
            if (_rules.Status != SessionStatus.Playing) return;

            var fx = feedback ?? NullFeedbackChannel.Instance;
            _metrics.Advance(deltaTime);
            _rules.Tick(deltaTime, fx);

            NoteFirstSuccess();
            if (_rules.Status.IsResolved()) Report();
        }

        public void SubmitInput(in InputCommand command, IFeedbackChannel feedback = null)
        {
            if (_rules.Status != SessionStatus.Playing) return;

            var fx = feedback ?? NullFeedbackChannel.Instance;

            // Time to first input is the headline M0 metric: it stands in for
            // "did the screen explain itself". Only a press counts as an
            // intention; a move or a release from a cancelled touch does not.
            if (command.Phase == InputPhase.Began) _metrics.NoteInput();

            _rules.HandleInput(command, fx);

            NoteFirstSuccess();
            if (_rules.Status.IsResolved()) Report();
        }

        /// <summary>
        /// The player left before the session resolved. Recorded so the data
        /// has no silent holes, and paid nothing so quitting a bad session is
        /// never a farming strategy.
        /// </summary>
        public void Abandon()
        {
            if (_rules.Status != SessionStatus.Playing) return;
            Report(SessionStatus.Abandoned);
        }

        /// <summary>
        /// Watches the rule set's own success counter rather than asking rule
        /// sets to report the moment themselves, so time_to_first_success
        /// cannot drift between the four prototypes.
        /// </summary>
        private void NoteFirstSuccess()
        {
            if (_notedFirstSuccess || _rules.SuccessfulActions <= 0) return;

            _notedFirstSuccess = true;
            _metrics.NoteFirstSuccess();
        }

        private void Report(SessionStatus? overrideStatus = null)
        {
            if (_reported) return;
            _reported = true;

            var status = overrideStatus ?? _rules.Status;

            var report = _metrics.Build(
                status,
                _rules.Score,
                _rules.Objective,
                _rules.SuccessfulActions,
                _rules.FailedActions,
                _rules.ResourcesEarned);

            _rules.CollectCustomMetrics(report.Custom);

            var reward = _rewards.Evaluate(status, _rules.Score);
            reward.CopyInto(report.Rewards);

            LastReport = report;
            LastReward = reward;

            // Bank before publishing, so anything listening to the event sees a
            // wallet that already includes this session's payout.
            _wallet?.Deposit(reward);

            _sink.Submit(report);
            SessionFinished?.Invoke(report, reward);
        }
    }
}
