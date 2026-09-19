using System;
using Mio.Core.Economy;
using Mio.Core.Metrics;

namespace Mio.Core.Session
{
    /// <summary>
    /// Drives one <see cref="IPrototypeRules"/> through an attempt and produces
    /// the metric report and the payout.
    ///
    /// Every prototype shares this, so "session duration", "first interaction
    /// time" and "replay" mean exactly the same thing in all three data sets.
    /// Without that, cross-prototype comparison in M0 would be meaningless.
    /// </summary>
    public sealed class PrototypeRunner
    {
        private readonly IPrototypeRules _rules;
        private readonly IMetricsSink _sink;
        private readonly RewardTable _rewards;
        private readonly SessionMetricsRecorder _metrics = new SessionMetricsRecorder();

        private int _attemptIndex = -1;
        private bool _reported;

        public PrototypeRunner(IPrototypeRules rules, IMetricsSink sink = null, RewardTable rewards = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _sink = sink ?? NullMetricsSink.Instance;
            _rewards = rewards ?? RewardTable.Empty;
        }

        public IPrototypeRules Rules => _rules;
        public PrototypeStatus Status => _rules.Status;

        /// <summary>How many times Begin has been called. 0 before the first.</summary>
        public int AttemptCount => _attemptIndex + 1;

        /// <summary>Report for the most recently resolved attempt, else null.</summary>
        public MetricReport LastReport { get; private set; }

        /// <summary>Payout for the most recently resolved attempt.</summary>
        public ResourceBundle LastReward { get; private set; }

        /// <summary>Raised when an attempt resolves, after the sink is fed.</summary>
        public event Action<MetricReport, ResourceBundle> AttemptFinished;

        public void Begin(int seed, IFeedbackChannel feedback = null)
        {
            var fx = feedback ?? NullFeedbackChannel.Instance;

            // An unresolved previous attempt counts as abandoned, so restarting
            // mid-run cannot silently drop a data point.
            if (_rules.Status == PrototypeStatus.Playing)
            {
                Abandon();
            }

            _attemptIndex++;
            _reported = false;
            LastReport = null;
            LastReward = ResourceBundle.Empty;

            _metrics.Begin(_rules.Id, seed, _attemptIndex);
            _rules.Begin(seed, fx);
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback = null)
        {
            if (_rules.Status != PrototypeStatus.Playing) return;

            var fx = feedback ?? NullFeedbackChannel.Instance;
            _metrics.Advance(deltaTime);
            _rules.Tick(deltaTime, fx);

            if (_rules.Status.IsResolved()) Report();
        }

        public void SubmitInput(in InputCommand command, IFeedbackChannel feedback = null)
        {
            if (_rules.Status != PrototypeStatus.Playing) return;

            var fx = feedback ?? NullFeedbackChannel.Instance;

            // Time-to-first-touch is the headline M0 metric: it tells us whether
            // the prototype is understandable without a tutorial. Only a press
            // counts; a stray Ended from a cancelled touch is not an intent.
            if (command.Phase == InputPhase.Began) _metrics.NoteInteraction();

            _rules.HandleInput(command, fx);

            if (_rules.Status.IsResolved()) Report();
        }

        /// <summary>Player left before the run resolved. Records, pays nothing.</summary>
        public void Abandon()
        {
            if (_rules.Status != PrototypeStatus.Playing) return;
            Report(PrototypeStatus.Abandoned);
        }

        private void Report(PrototypeStatus? overrideStatus = null)
        {
            if (_reported) return;
            _reported = true;

            var status = overrideStatus ?? _rules.Status;

            var report = _metrics.Build(
                status,
                _rules.Score,
                _rules.SuccessfulActions,
                _rules.FailedActions);

            _rules.CollectCustomMetrics(report.Custom);

            var reward = _rewards.Evaluate(status, _rules.Score);
            reward.CopyInto(report.Rewards);

            LastReport = report;
            LastReward = reward;

            _sink.Submit(report);
            AttemptFinished?.Invoke(report, reward);
        }
    }
}
