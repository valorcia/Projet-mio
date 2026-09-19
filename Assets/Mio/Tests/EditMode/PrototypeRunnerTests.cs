using System.Collections.Generic;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    /// <summary>
    /// Stand-in rules with directly controllable state, so the runner's
    /// bookkeeping can be tested without any real prototype's behaviour.
    /// </summary>
    internal sealed class FakeRules : IPrototypeRules
    {
        public PrototypeId Id => PrototypeId.Flow;
        public PrototypeStatus Status { get; set; } = PrototypeStatus.Idle;
        public int Score { get; set; }
        public int SuccessfulActions { get; set; }
        public int FailedActions { get; set; }
        public float Progress01 => 0f;
        public float TimeRemaining => 0f;

        public int BeginCount { get; private set; }
        public int LastSeed { get; private set; }

        /// <summary>Set to have the next Tick resolve the run.</summary>
        public PrototypeStatus? ResolveOnNextTick;

        /// <summary>Set to have the next input resolve the run.</summary>
        public PrototypeStatus? ResolveOnNextInput;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            BeginCount++;
            LastSeed = seed;
            Status = PrototypeStatus.Playing;
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (ResolveOnNextTick.HasValue)
            {
                Status = ResolveOnNextTick.Value;
                ResolveOnNextTick = null;
            }
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (ResolveOnNextInput.HasValue)
            {
                Status = ResolveOnNextInput.Value;
                ResolveOnNextInput = null;
            }
        }

        public void CollectCustomMetrics(IDictionary<string, double> into)
        {
            into["fake.marker"] = 1d;
        }
    }

    [TestFixture]
    public class PrototypeRunnerTests
    {
        private FakeRules _rules;
        private InMemoryMetricsSink _sink;
        private PrototypeRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _rules = new FakeRules();
            _sink = new InMemoryMetricsSink();
            _runner = new PrototypeRunner(
                _rules,
                _sink,
                new RewardTable(
                    new ResourceBundle(2, 0, 0),
                    new ResourceBundle(0, 0, 5),
                    new[] { new RewardTier(50, new ResourceBundle(0, 3, 0)) }));
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

        [Test]
        public void ResolvedAttemptIsReportedExactlyOnce()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;

            _runner.Tick(0.1f);
            _runner.Tick(0.1f);
            _runner.Tick(0.1f);

            Assert.AreEqual(1, _sink.Reports.Count);
        }

        [Test]
        public void FirstInteractionTimeIsMeasuredFromBegin()
        {
            _runner.Begin(1);
            Advance(2.0f);

            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Advance(1.0f);
            _rules.ResolveOnNextTick = PrototypeStatus.Lost;
            _runner.Tick(0.1f);

            var report = _sink.Last;
            Assert.IsTrue(report.HadInteraction);
            Assert.AreEqual(2.0f, report.FirstInteractionTime, 0.001f);
        }

        [Test]
        public void OnlyTheFirstTouchSetsFirstInteractionTime()
        {
            _runner.Begin(1);
            Advance(0.5f);
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));
            Advance(3.0f);
            _runner.SubmitInput(InputCommand.Began(0.2f, 0.2f));

            _rules.ResolveOnNextTick = PrototypeStatus.Lost;
            _runner.Tick(0.1f);

            Assert.AreEqual(0.5f, _sink.Last.FirstInteractionTime, 0.001f);
        }

        [Test]
        public void NeverTouchingReportsNoInteraction()
        {
            // This is the signal that a prototype failed the "no tutorial" bar.
            _runner.Begin(1);
            Advance(5f);
            _rules.ResolveOnNextTick = PrototypeStatus.Lost;
            _runner.Tick(0.1f);

            Assert.IsFalse(_sink.Last.HadInteraction);
            Assert.Less(_sink.Last.FirstInteractionTime, 0f);
        }

        [Test]
        public void MovesAndReleasesDoNotCountAsFirstInteraction()
        {
            _runner.Begin(1);
            Advance(1f);
            _runner.SubmitInput(InputCommand.Moved(0.5f, 0.5f));
            _runner.SubmitInput(InputCommand.Ended(0.5f, 0.5f));

            _rules.ResolveOnNextTick = PrototypeStatus.Lost;
            _runner.Tick(0.1f);

            Assert.IsFalse(_sink.Last.HadInteraction);
        }

        [Test]
        public void SessionDurationTracksElapsedTime()
        {
            _runner.Begin(1);
            Advance(4.0f);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.5f);

            Assert.AreEqual(4.5f, _sink.Last.SessionDuration, 0.01f);
        }

        [Test]
        public void ReplayIsFlaggedByAttemptIndex()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            _runner.Begin(2);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _sink.Reports.Count);
            Assert.IsFalse(_sink.Reports[0].IsReplay);
            Assert.AreEqual(0, _sink.Reports[0].AttemptIndex);
            Assert.IsTrue(_sink.Reports[1].IsReplay);
            Assert.AreEqual(1, _sink.Reports[1].AttemptIndex);
        }

        [Test]
        public void RestartingMidRunRecordsTheAbandonedAttempt()
        {
            _runner.Begin(1);
            Advance(3f);

            _runner.Begin(2);

            Assert.AreEqual(1, _sink.Reports.Count, "the interrupted run must still be recorded");
            Assert.AreEqual(PrototypeStatus.Abandoned, _sink.Reports[0].Status);
            Assert.IsFalse(_sink.Reports[0].Completed);
        }

        [Test]
        public void AbandonedAttemptEarnsNothing()
        {
            _runner.Begin(1);
            _rules.Score = 500;
            Advance(3f);
            _runner.Abandon();

            Assert.IsTrue(_runner.LastReward.IsEmpty);
            Assert.AreEqual(0, _sink.Last.Rewards[ResourceKind.Energy]);
        }

        [Test]
        public void WinPaysParticipationTierAndCompletionBonus()
        {
            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(2, _runner.LastReward.Energy);
            Assert.AreEqual(3, _runner.LastReward.Material);
            Assert.AreEqual(5, _runner.LastReward.Coin);
        }

        [Test]
        public void ReportCarriesRewardsAndCustomMetrics()
        {
            _runner.Begin(1);
            _rules.Score = 80;
            _rules.SuccessfulActions = 9;
            _rules.FailedActions = 3;
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            var report = _sink.Last;

            Assert.AreEqual(80, report.Score);
            Assert.AreEqual(9, report.SuccessfulActions);
            Assert.AreEqual(3, report.FailedActions);
            Assert.AreEqual(12, report.TotalActions);
            Assert.AreEqual(0.75f, report.SuccessRate, 0.0001f);
            Assert.IsTrue(report.Completed);
            Assert.AreEqual(3, report.Rewards[ResourceKind.Material]);
            Assert.AreEqual(1d, report.Custom["fake.marker"]);
        }

        [Test]
        public void InputAfterResolutionIsIgnored()
        {
            _runner.Begin(1);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            _rules.ResolveOnNextInput = PrototypeStatus.Lost;
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Assert.AreEqual(1, _sink.Reports.Count);
            Assert.AreEqual(PrototypeStatus.Won, _sink.Last.Status);
        }

        [Test]
        public void ResolvingOnInputStillReports()
        {
            _runner.Begin(1);
            Advance(1f);
            _rules.ResolveOnNextInput = PrototypeStatus.Won;
            _runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));

            Assert.AreEqual(1, _sink.Reports.Count);
            Assert.AreEqual(PrototypeStatus.Won, _sink.Last.Status);
        }

        [Test]
        public void AttemptFinishedEventFires()
        {
            MetricReport captured = null;
            var payout = ResourceBundle.Empty;
            _runner.AttemptFinished += (report, reward) => { captured = report; payout = reward; };

            _runner.Begin(1);
            _rules.Score = 80;
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            Assert.IsNotNull(captured);
            Assert.AreEqual(3, payout.Material);
        }

        [Test]
        public void SeedIsForwardedToRulesAndReport()
        {
            _runner.Begin(4321);
            _rules.ResolveOnNextTick = PrototypeStatus.Won;
            _runner.Tick(0.1f);

            Assert.AreEqual(4321, _rules.LastSeed);
            Assert.AreEqual(4321, _sink.Last.Seed);
        }
    }
}
