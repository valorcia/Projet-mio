using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Harness;
using Mio.Core.Metrics;
using Mio.Core.Profile;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    /// <summary>Records every cue so tests can assert on what was emitted.</summary>
    internal sealed class RecordingFeedbackChannel : IFeedbackChannel
    {
        public readonly List<FeedbackCue> Cues = new List<FeedbackCue>();

        public void Emit(in FeedbackCue cue) => Cues.Add(cue);

        public int CountOf(FeedbackCueKind kind)
        {
            var n = 0;
            for (var i = 0; i < Cues.Count; i++)
            {
                if (Cues[i].Kind == kind) n++;
            }

            return n;
        }

        public bool Contains(FeedbackCueKind kind) => CountOf(kind) > 0;
    }

    [TestFixture]
    public class TestRulesetTests
    {
        private static TestRulesetConfig Config(int required = 5)
        {
            return new TestRulesetConfig
            {
                RequiredSuccesses = required,
                ScorePerSuccess = 100,
                Duration = 30f,
                TargetHalfSize = 0.12f
            };
        }

        private static InputCommand TapAt(Vec2 at) =>
            new InputCommand(InputPhase.Began, at, 0f);

        /// <summary>A point guaranteed to be outside the current target.</summary>
        private static Vec2 Miss(TestRuleset rules)
        {
            var target = rules.Target;
            var x = target.X > 0.5f ? 0.02f : 0.98f;
            return new Vec2(x, target.Y);
        }

        [Test]
        public void BeginPutsTheSessionInPlay()
        {
            var rules = new TestRuleset(Config());
            var fx = new RecordingFeedbackChannel();

            rules.Begin(1, fx);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.AreEqual(0, rules.Score);
            Assert.AreEqual(0f, rules.Progress01);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Begin));
        }

        [Test]
        public void IdentifiesItselfAsTheHarness()
        {
            Assert.AreEqual(PrototypeId.TestHarness, new TestRuleset(Config()).Id);
        }

        // ---- deterministic seeding ----

        [Test]
        public void SameSeedProducesTheSameTargetSequence()
        {
            var a = new TestRuleset(Config(10));
            var b = new TestRuleset(Config(10));

            a.Begin(2024, NullFeedbackChannel.Instance);
            b.Begin(2024, NullFeedbackChannel.Instance);

            for (var i = 0; i < 9; i++)
            {
                Assert.AreEqual(a.Target.X, b.Target.X, 0.0001f, "target {0} x", i);
                Assert.AreEqual(a.Target.Y, b.Target.Y, 0.0001f, "target {0} y", i);

                a.HandleInput(TapAt(a.Target), NullFeedbackChannel.Instance);
                b.HandleInput(TapAt(b.Target), NullFeedbackChannel.Instance);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentTargets()
        {
            var a = new TestRuleset(Config());
            var b = new TestRuleset(Config());

            a.Begin(1, NullFeedbackChannel.Instance);
            b.Begin(2, NullFeedbackChannel.Instance);

            // AreNotEqual has no delta overload, so compare the gap directly.
            Assert.Greater(System.Math.Abs(a.Target.X - b.Target.X), 0.0001f);
        }

        [Test]
        public void BeginResetsEverything()
        {
            var rules = new TestRuleset(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);
            rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);
            rules.HandleInput(TapAt(Miss(rules)), NullFeedbackChannel.Instance);

            rules.Begin(1, NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.Score);
            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(SessionStatus.Playing, rules.Status);
        }

        [Test]
        public void TargetStaysInsideThePlayField()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var rules = new TestRuleset(Config(20));
                rules.Begin(seed, NullFeedbackChannel.Instance);

                for (var i = 0; i < 19; i++)
                {
                    Assert.GreaterOrEqual(rules.Target.X, 0f);
                    Assert.LessOrEqual(rules.Target.X, 1f);
                    Assert.GreaterOrEqual(rules.Target.Y, 0f);
                    Assert.LessOrEqual(rules.Target.Y, 1f);

                    rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);
                }
            }
        }

        // ---- input, scoring, cues ----

        [Test]
        public void HittingTheTargetScoresAndEmitsSuccess()
        {
            var rules = new TestRuleset(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.HandleInput(TapAt(rules.Target), fx);

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(100, rules.Score);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Success));
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Progress));
        }

        [Test]
        public void MissingCountsAsAFailedActionAndEmitsFail()
        {
            var rules = new TestRuleset(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.HandleInput(TapAt(Miss(rules)), fx);

            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.AreEqual(1, rules.FailedActions);
            Assert.AreEqual(0, rules.Score);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Fail));
        }

        [Test]
        public void AHitMovesTheTarget()
        {
            var rules = new TestRuleset(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var before = rules.Target;
            rules.HandleInput(TapAt(before), NullFeedbackChannel.Instance);

            var moved = System.Math.Abs(before.X - rules.Target.X)
                      + System.Math.Abs(before.Y - rules.Target.Y);
            Assert.Greater(moved, 0.0001f);
        }

        [Test]
        public void AMissDoesNotMoveTheTarget()
        {
            var rules = new TestRuleset(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var before = rules.Target;
            rules.HandleInput(TapAt(Miss(rules)), NullFeedbackChannel.Instance);

            Assert.AreEqual(before.X, rules.Target.X, 0.0001f);
            Assert.AreEqual(before.Y, rules.Target.Y, 0.0001f);
        }

        [Test]
        public void OnlyAPressCounts()
        {
            var rules = new TestRuleset(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var at = rules.Target;
            rules.HandleInput(new InputCommand(InputPhase.Moved, at, 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, at, 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Canceled, at, 0f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
        }

        [Test]
        public void TargetEdgesAreInclusive()
        {
            var rules = new TestRuleset(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var edge = new Vec2(rules.Target.X + rules.TargetHalfSize, rules.Target.Y);
            rules.HandleInput(TapAt(edge), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.SuccessfulActions);
        }

        [Test]
        public void ProgressTracksHitsTowardTheRequirement()
        {
            var rules = new TestRuleset(Config(4));
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);
            Assert.AreEqual(0.25f, rules.Progress01, 0.0001f);

            rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);
            Assert.AreEqual(0.5f, rules.Progress01, 0.0001f);
        }

        // ---- win and fail ----

        [Test]
        public void CompletesAfterTheConfiguredNumberOfSuccesses()
        {
            var rules = new TestRuleset(Config(3));
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.HandleInput(TapAt(rules.Target), fx);
            rules.HandleInput(TapAt(rules.Target), fx);
            Assert.AreEqual(SessionStatus.Playing, rules.Status);

            rules.HandleInput(TapAt(rules.Target), fx);

            Assert.AreEqual(SessionStatus.Won, rules.Status);
            Assert.AreEqual(1f, rules.Progress01, 0.0001f);
            Assert.AreEqual(300, rules.Score);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Win));
        }

        [Test]
        public void RequiredSuccessesIsConfigurable()
        {
            foreach (var required in new[] { 1, 2, 7 })
            {
                var rules = new TestRuleset(Config(required));
                rules.Begin(1, NullFeedbackChannel.Instance);

                for (var i = 0; i < required; i++)
                {
                    Assert.AreEqual(SessionStatus.Playing, rules.Status, "required={0} i={1}", required, i);
                    rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);
                }

                Assert.AreEqual(SessionStatus.Won, rules.Status, "required={0}", required);
            }
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var config = Config();
            var rules = new TestRuleset(config);
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.Tick(config.Duration + 0.1f, fx);

            Assert.AreEqual(SessionStatus.Lost, rules.Status);
            Assert.AreEqual(0f, rules.TimeRemaining, 0.0001f);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Lose));
        }

        [Test]
        public void ZeroDurationMeansUntimed()
        {
            var config = Config();
            config.Duration = 0f;
            var rules = new TestRuleset(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.Tick(10000f, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.IsTrue(float.IsInfinity(rules.TimeRemaining));
        }

        [Test]
        public void InputIsIgnoredOnceResolved()
        {
            var rules = new TestRuleset(Config(1));
            rules.Begin(1, NullFeedbackChannel.Instance);
            rules.HandleInput(TapAt(rules.Target), NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Won, rules.Status);

            rules.HandleInput(TapAt(Miss(rules)), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(100, rules.Score);
        }

        [Test]
        public void RuleSetNeverEmitsPrototypeSpecificCues()
        {
            // The harness must only speak the shared vocabulary; anything else
            // would mean a rule set naming presentation concepts.
            var rules = new TestRuleset(Config(2));
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.HandleInput(TapAt(Miss(rules)), fx);
            rules.HandleInput(TapAt(rules.Target), fx);
            rules.HandleInput(TapAt(rules.Target), fx);

            // Written without Is.AnyOf: Unity bundles an older NUnit than the
            // one the headless runner uses, and this suite must compile in both.
            var shared = new HashSet<FeedbackCueKind>
            {
                FeedbackCueKind.Begin,
                FeedbackCueKind.Success,
                FeedbackCueKind.Fail,
                FeedbackCueKind.Progress,
                FeedbackCueKind.Win,
                FeedbackCueKind.Lose
            };

            foreach (var cue in fx.Cues)
            {
                Assert.IsTrue(shared.Contains(cue.Kind), "unexpected cue kind {0}", cue.Kind);
            }
        }
    }

    /// <summary>
    /// The end-to-end check M0.1 exists to make: one session through the whole
    /// foundation, from a normalised tap to a persisted reward.
    /// </summary>
    [TestFixture]
    public class SharedFoundationEndToEndTests
    {
        [Test]
        public void ASessionFlowsFromInputToPersistedReward()
        {
            var rules = new TestRuleset(new TestRulesetConfig
            {
                RequiredSuccesses = 3,
                ScorePerSuccess = 100,
                Duration = 30f
            });

            var sink = new InMemoryMetricsSink();
            var store = new InMemoryProfileStore();
            var wallet = new PlayerWallet(store);
            var fx = new RecordingFeedbackChannel();

            var rewards = new RewardTable(
                participation: new ResourceBundle(1, 0, 0),
                completionBonus: new ResourceBundle(0, 0, 5),
                tiers: new[] { new RewardTier(300, new ResourceBundle(0, 2, 0)) });

            var runner = new PrototypeRunner(rules, sink, rewards, wallet);

            runner.Begin(1234, fx);
            runner.Tick(1.5f, fx);

            // Three normalised taps on the target, with one miss in between.
            runner.SubmitInput(new InputCommand(InputPhase.Began, rules.Target, 0f), fx);
            runner.Tick(0.5f, fx);
            runner.SubmitInput(new InputCommand(InputPhase.Began, new Vec2(0.01f, 0.01f), 0f), fx);
            runner.Tick(0.5f, fx);
            runner.SubmitInput(new InputCommand(InputPhase.Began, rules.Target, 0f), fx);
            runner.Tick(0.5f, fx);
            runner.SubmitInput(new InputCommand(InputPhase.Began, rules.Target, 0f), fx);

            // Session resolved
            Assert.AreEqual(SessionStatus.Won, runner.Status);

            // Feedback cues were emitted
            Assert.AreEqual(3, fx.CountOf(FeedbackCueKind.Success));
            Assert.AreEqual(1, fx.CountOf(FeedbackCueKind.Fail));
            Assert.AreEqual(1, fx.CountOf(FeedbackCueKind.Win));

            // Metric report was produced and is complete
            Assert.AreEqual(1, sink.Reports.Count);
            var report = sink.Last;

            Assert.IsNotEmpty(report.SessionId);
            Assert.AreEqual(PrototypeId.TestHarness, report.PrototypeId);
            Assert.AreEqual(1234, report.Seed);
            Assert.AreEqual(3.0f, report.SessionDuration, 0.01f);
            Assert.AreEqual(1.5f, report.TimeToFirstInput, 0.01f);
            Assert.AreEqual(4, report.TotalInputs);
            Assert.AreEqual(3, report.SuccessfulActions);
            Assert.AreEqual(1, report.FailedActions);
            Assert.AreEqual(300, report.Score);
            Assert.AreEqual(1f, report.ObjectiveProgress, 0.0001f);
            Assert.AreEqual(SessionStatus.Won, report.CompletionStatus);
            Assert.IsTrue(report.Completed);
            Assert.IsFalse(report.ReplayRequested);

            // Reward was evaluated and banked
            Assert.AreEqual(1, report.Rewards[ResourceKind.Cotton]);
            Assert.AreEqual(2, report.Rewards[ResourceKind.Wood]);
            Assert.AreEqual(5, report.Rewards[ResourceKind.Metal]);

            Assert.AreEqual(1, wallet.Balance.Cotton);
            Assert.AreEqual(2, wallet.Balance.Wood);
            Assert.AreEqual(5, wallet.Balance.Metal);

            // And it survives a relaunch
            Assert.AreEqual(5, new PlayerWallet(store).Balance.Metal);
        }

        [Test]
        public void AReplayProducesASecondIndependentReport()
        {
            var rules = new TestRuleset(new TestRulesetConfig { RequiredSuccesses = 1, Duration = 30f });
            var sink = new InMemoryMetricsSink();
            var wallet = new PlayerWallet(new InMemoryProfileStore());
            var rewards = new RewardTable(new ResourceBundle(1, 0, 0), ResourceBundle.Empty, new RewardTier[0]);
            var runner = new PrototypeRunner(rules, sink, rewards, wallet);

            runner.Begin(1);
            runner.SubmitInput(new InputCommand(InputPhase.Began, rules.Target, 0f));

            runner.RequestReplay(2);
            runner.SubmitInput(new InputCommand(InputPhase.Began, rules.Target, 0f));

            Assert.AreEqual(2, sink.Reports.Count);
            Assert.IsFalse(sink.Reports[0].ReplayRequested);
            Assert.IsTrue(sink.Reports[1].ReplayRequested);
            Assert.AreNotEqual(sink.Reports[0].SessionId, sink.Reports[1].SessionId);
            Assert.AreEqual(2, wallet.Balance.Cotton, "both sessions paid");
        }
    }
}
