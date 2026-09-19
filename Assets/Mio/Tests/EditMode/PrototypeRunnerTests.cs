using System;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using Mio.Core.Profile;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    /// <summary>
    /// Stand-in rule set with directly controllable state, so the runner's
    /// bookkeeping can be tested without any real rule set's behaviour.
    /// </summary>
    internal sealed class FakeRules : IPrototypeRules
    {
        public PrototypeId Id => PrototypeId.TestHarness;
        public SessionStatus Status { get; set; } = SessionStatus.Idle;
        public int Score { get; set; }
        public float Progress01 { get; set; }
        public int SuccessfulActions { get; set; }
        public int FailedActions { get; set; }

        public int BeginCount { get; private set; }
        public int LastSeed { get; private set; }
        public int InputsSeen { get; private set; }

        /// <summary>Set to have the next Tick resolve the session.</summary>
        public SessionStatus? ResolveOnNextTick;

        /// <summary>Set to have the next input resolve the session.</summary>
        public SessionStatus? ResolveOnNextInput;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            BeginCount++;
            LastSeed = seed;
            Status = SessionStatus.Playing;
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (!ResolveOnNextTick.HasValue) return;
            Status = ResolveOnNextTick.Value;
            ResolveOnNextTick = null;
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            InputsSeen++;
            if (!ResolveOnNextInput.HasValue) return;
            Status = ResolveOnNextInput.Value;
            ResolveOnNextInput = null;
        }
    }

    [TestFixture]
    public class PrototypeRunnerTests
    {
        private FakeRules _rules;
        private InMemoryMetricsSink _sink;
        private InMemoryProfileStore _store;
        private PlayerWallet _wallet;
        private PrototypeRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _rules = new FakeRules();
            _sink = new InMemoryMetricsSink();
            _store = new InMemoryProfileStore();
            _wallet = new PlayerWallet(_store);

            _runner = new PrototypeRunner(
                _rules,
                _sink,
                new RewardTable(
                    new ResourceBundle(2, 0, 0),
                    new ResourceBundle(0, 0, 5),
                    new[] { new RewardTier(50, new ResourceBundle(0, 3, 0)) }),
                _wallet);
        }

        private void Advance(float seconds, float step = 0.1f)
        {
            var remaining = seconds;
            while (remaining > 0f)
            {
                var dt = remaining < step ? remaining : step;
                _runner.Tick(dt);
                remaining -= dt;
            }
        }

        // ---- lifecycle ----

        [Test]
        public void ConstructorRejectsNullRules()
        {
            Assert.Throws<ArgumentNullException>(() => new PrototypeRunner(null));
        }

        [Test]
        public void BeginStartsTheRuleSet()
        {
            _runner.Begin(7);

            Assert.AreEqual(1, _rules.BeginCount);
            Assert.AreEqual(SessionStatus.Playing, _runner.Status);
            Assert.AreEqual(1, _runner.AttemptCount);
        }

        [Test]
        public void ResolvedSessionIsReportedExactlyOnce()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;

            _runner.Tick(0.1f);
            _runner.Tick(0.1f);
            _runner.Tick(0.1f);

            Assert.AreEqual(1, _sink.Reports.Count);
        }

        [Test]
        public void TickIsIgnoredBeforeBegin()
        {
            _runner.Tick(1f);
            Assert.AreEqual(0, _sink.Reports.Count);
            Assert.AreEqual(0, _rules.BeginCount);
        }

        [Test]
        public void InputAfterResolutionIsIgnored()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            _rules.ResolveOnNextInput = SessionStatus.Lost;
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Assert.AreEqual(1, _sink.Reports.Count);
            Assert.AreEqual(SessionStatus.Won, _sink.Last.CompletionStatus);
        }

        [Test]
        public void ResolvingOnInputStillReports()
        {
            _runner.Begin(1);
            Advance(1f);
            _rules.ResolveOnNextInput = SessionStatus.Won;
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Assert.AreEqual(1, _sink.Reports.Count);
            Assert.AreEqual(SessionStatus.Won, _sink.Last.CompletionStatus);
        }

        [Test]
        public void RestartingMidSessionRecordsTheAbandonedOne()
        {
            _runner.Begin(1);
            Advance(3f);

            _runner.Begin(2);

            Assert.AreEqual(1, _sink.Reports.Count, "the interrupted session must still be recorded");
            Assert.AreEqual(SessionStatus.Abandoned, _sink.Reports[0].CompletionStatus);
            Assert.IsFalse(_sink.Reports[0].Completed);
        }

        [Test]
        public void AbandonIsIgnoredWhenNothingIsRunning()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            _runner.Abandon();

            Assert.AreEqual(1, _sink.Reports.Count);
        }

        // ---- replay ----

        [Test]
        public void FirstSessionIsNotAReplay()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.IsFalse(_sink.Last.ReplayRequested);
            Assert.AreEqual(0, _sink.Last.AttemptIndex);
        }

        [Test]
        public void RequestReplayFlagsTheNextReport()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            _runner.RequestReplay(2);
            _rules.ResolveOnNextTick = SessionStatus.Lost;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _sink.Reports.Count);
            Assert.IsTrue(_sink.Reports[1].ReplayRequested);
            Assert.AreEqual(1, _sink.Reports[1].AttemptIndex);
        }

        [Test]
        public void AttemptIndexKeepsClimbing()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            for (var i = 0; i < 3; i++)
            {
                _runner.RequestReplay(i);
                _rules.ResolveOnNextTick = SessionStatus.Won;
                _runner.Tick(0.1f);
            }

            Assert.AreEqual(4, _sink.Reports.Count);
            Assert.AreEqual(3, _sink.Last.AttemptIndex);
            Assert.AreEqual(4, _runner.AttemptCount);
        }

        // ---- metric report ----

        [Test]
        public void SessionIdIsPresentAndUniquePerSession()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            _runner.RequestReplay(1);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            var a = _sink.Reports[0].SessionId;
            var b = _sink.Reports[1].SessionId;

            Assert.IsNotEmpty(a);
            Assert.IsNotEmpty(b);
            Assert.AreNotEqual(a, b, "the same seed must not produce the same session id");
        }

        [Test]
        public void SessionTimestampsAreOrdered()
        {
            _runner.Begin(1);
            Advance(0.5f);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            var report = _sink.Last;
            Assert.AreNotEqual(default(DateTime), report.SessionStartUtc);
            Assert.GreaterOrEqual(report.SessionEndUtc, report.SessionStartUtc);
            Assert.AreEqual(DateTimeKind.Utc, report.SessionStartUtc.Kind);
        }

        [Test]
        public void SessionDurationTracksSimulatedTime()
        {
            _runner.Begin(1);
            Advance(4.0f);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.5f);

            Assert.AreEqual(4.5f, _sink.Last.SessionDuration, 0.01f);
        }

        [Test]
        public void TimeToFirstInputIsMeasuredFromSessionStart()
        {
            _runner.Begin(1);
            Advance(2.0f);

            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Advance(1.0f);
            _rules.ResolveOnNextTick = SessionStatus.Lost;
            _runner.Tick(0.1f);

            var report = _sink.Last;
            Assert.IsTrue(report.HadInput);
            Assert.AreEqual(2.0f, report.TimeToFirstInput, 0.001f);
        }

        [Test]
        public void OnlyTheFirstTouchSetsTimeToFirstInput()
        {
            _runner.Begin(1);
            Advance(0.5f);
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));
            Advance(3.0f);
            _runner.SubmitInput(InputCommand.Began(0.2f, 0.2f));

            _rules.ResolveOnNextTick = SessionStatus.Lost;
            _runner.Tick(0.1f);

            Assert.AreEqual(0.5f, _sink.Last.TimeToFirstInput, 0.001f);
        }

        [Test]
        public void NeverTouchingReportsNoInput()
        {
            // This is the signal that a prototype failed the "no tutorial" bar.
            _runner.Begin(1);
            Advance(5f);
            _rules.ResolveOnNextTick = SessionStatus.Lost;
            _runner.Tick(0.1f);

            Assert.IsFalse(_sink.Last.HadInput);
            Assert.Less(_sink.Last.TimeToFirstInput, 0f);
            Assert.AreEqual(0, _sink.Last.InputCount);
        }

        [Test]
        public void InputCountCountsDistinctTouchesOnly()
        {
            _runner.Begin(1);

            // One drag: press, many moves, release. That is one input.
            _runner.SubmitInput(InputCommand.Began(0.1f, 0.1f));
            for (var i = 0; i < 20; i++) _runner.SubmitInput(InputCommand.Moved(0.2f, 0.2f));
            _runner.SubmitInput(InputCommand.Ended(0.3f, 0.3f));

            _runner.SubmitInput(InputCommand.Began(0.4f, 0.4f));

            _rules.ResolveOnNextTick = SessionStatus.Lost;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _sink.Last.InputCount);
            Assert.AreEqual(23, _rules.InputsSeen, "the rule set still sees every event");
        }

        [Test]
        public void ReportCarriesScoreProgressAndActionCounts()
        {
            _runner.Begin(1);
            _rules.Score = 80;
            _rules.Progress01 = 0.625f;
            _rules.SuccessfulActions = 9;
            _rules.FailedActions = 3;
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            var report = _sink.Last;

            Assert.AreEqual(80, report.Score);
            Assert.AreEqual(0.625f, report.Progress, 0.0001f);
            Assert.AreEqual(9, report.SuccessfulActions);
            Assert.AreEqual(3, report.FailedActions);
            Assert.AreEqual(12, report.TotalActions);
            Assert.AreEqual(0.75f, report.SuccessRate, 0.0001f);
            Assert.IsTrue(report.Completed);
            Assert.AreEqual(PrototypeId.TestHarness, report.PrototypeId);
        }

        [Test]
        public void SeedIsForwardedToRulesAndReport()
        {
            _runner.Begin(4321);
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(4321, _rules.LastSeed);
            Assert.AreEqual(4321, _sink.Last.Seed);
        }

        // ---- rewards and wallet ----

        [Test]
        public void WinPaysParticipationTierAndCompletionBonus()
        {
            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _runner.LastReward.Energy);
            Assert.AreEqual(3, _runner.LastReward.Material);
            Assert.AreEqual(5, _runner.LastReward.Coin);
        }

        [Test]
        public void RewardIsBankedInTheWallet()
        {
            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _wallet.Balance.Energy);
            Assert.AreEqual(3, _wallet.Balance.Material);
            Assert.AreEqual(5, _wallet.Balance.Coin);
        }

        [Test]
        public void AbandonedSessionEarnsNothingAndBanksNothing()
        {
            _runner.Begin(1);
            _rules.Score = 500;
            Advance(3f);
            _runner.Abandon();

            Assert.IsTrue(_runner.LastReward.IsEmpty);
            Assert.AreEqual(0, _sink.Last.Rewards[ResourceKind.Energy]);
            Assert.IsTrue(_wallet.Balance.IsEmpty);
        }

        [Test]
        public void WalletIsAlreadyUpdatedWhenTheEventFires()
        {
            var balanceAtEvent = ResourceBundle.Empty;
            _runner.SessionFinished += (_, __) => balanceAtEvent = _wallet.Balance;

            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, balanceAtEvent.Energy, "listeners must not see a stale wallet");
        }

        [Test]
        public void SessionFinishedCarriesReportAndReward()
        {
            MetricReport captured = null;
            var payout = ResourceBundle.Empty;
            _runner.SessionFinished += (report, reward) => { captured = report; payout = reward; };

            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = SessionStatus.Won;
            _runner.Tick(0.1f);

            Assert.IsNotNull(captured);
            Assert.AreEqual(3, payout.Material);
        }

        [Test]
        public void RunnerWorksWithoutASinkWalletOrTable()
        {
            // A headless test must be able to run a session with no wiring.
            var bare = new PrototypeRunner(new FakeRules());
            bare.Begin(1);
            bare.Tick(0.1f);

            Assert.AreEqual(SessionStatus.Playing, bare.Status);
            Assert.DoesNotThrow(() => bare.Abandon());
            Assert.IsTrue(bare.LastReward.IsEmpty);
        }
    }
}
