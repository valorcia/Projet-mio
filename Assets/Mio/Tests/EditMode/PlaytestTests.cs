using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using Mio.Core.Playtest;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    // =================================================================
    // Objective
    // =================================================================
    [TestFixture]
    public class ObjectiveTests
    {
        [Test]
        public void ProgressIsPerItemNotPerGoal()
        {
            // A goal asking for 20 must weigh more than one asking for 1,
            // because that is what the player feels.
            var objective = new Objective(
                new ObjectiveGoal("A", 1),
                new ObjectiveGoal("B", 9));

            objective.Add("A");

            Assert.AreEqual(0.1f, objective.Progress01, 0.0001f);
            Assert.IsFalse(objective.Completed);
        }

        [Test]
        public void OvershootingOneGoalCannotMaskAnother()
        {
            var objective = new Objective(
                new ObjectiveGoal("A", 2),
                new ObjectiveGoal("B", 2));

            objective.Add("A", 100);

            Assert.AreEqual(0.5f, objective.Progress01, 0.0001f);
            Assert.IsFalse(objective.Completed, "B is still outstanding");
        }

        [Test]
        public void CompletesWhenEveryGoalIsMet()
        {
            var objective = new Objective(
                new ObjectiveGoal("A", 1),
                new ObjectiveGoal("B", 2));

            objective.Add("A");
            objective.Add("B", 2);

            Assert.IsTrue(objective.Completed);
            Assert.AreEqual(1f, objective.Progress01, 0.0001f);
        }

        [Test]
        public void UnknownLabelsAreIgnoredRatherThanThrowing()
        {
            // A prototype may make things the objective never asked for.
            var objective = new Objective(new ObjectiveGoal("A", 1));

            Assert.IsFalse(objective.Add("Z"));
            Assert.AreEqual(0f, objective.Progress01, 0.0001f);
        }

        [Test]
        public void AnEmptyObjectiveIsAlreadyComplete()
        {
            var objective = new Objective();

            Assert.IsTrue(objective.Completed);
            Assert.AreEqual(1f, objective.Progress01, 0.0001f);
        }

        [Test]
        public void ResetClearsProgressButKeepsGoals()
        {
            var objective = new Objective(new ObjectiveGoal("A", 2));
            objective.Add("A", 2);
            objective.Reset();

            Assert.AreEqual(0f, objective.Progress01, 0.0001f);
            Assert.AreEqual(2, objective[0].Required);
        }
    }

    // =================================================================
    // The "one more" experiment
    // =================================================================
    [TestFixture]
    public class ReplayExperimentTests
    {
        [Test]
        public void NoPromptIsOfferedDuringTheQuietWindow()
        {
            var experiment = new ReplayExperiment(quietWindow: 2.5f, inputLockout: 0.5f);
            experiment.OnSessionResolved();

            Assert.IsTrue(experiment.IsActive);
            Assert.IsFalse(experiment.PromptVisible);

            experiment.Tick(1f);
            Assert.IsFalse(experiment.PromptVisible);

            experiment.Tick(2f);
            Assert.IsTrue(experiment.PromptVisible);
        }

        [Test]
        public void TheTapThatFinishedTheSessionIsNotAReachForMore()
        {
            var experiment = new ReplayExperiment(quietWindow: 2.5f, inputLockout: 0.6f);
            experiment.OnSessionResolved();

            experiment.Tick(0.1f);

            Assert.IsFalse(experiment.HandlePress(), "still inside the lockout");
            Assert.IsFalse(experiment.ReachedWithoutPrompt);
        }

        [Test]
        public void ReachingForADeadBoardIsRecordedAndStartsTheSession()
        {
            var experiment = new ReplayExperiment(quietWindow: 2.5f, inputLockout: 0.5f);
            experiment.OnSessionResolved();

            experiment.Tick(1f);

            Assert.IsTrue(experiment.HandlePress(), "enthusiasm must not be made to wait");
            Assert.IsTrue(experiment.ReachedWithoutPrompt);
        }

        [Test]
        public void UsingThePromptIsTheWeakerSignal()
        {
            var experiment = new ReplayExperiment(quietWindow: 2.5f, inputLockout: 0.5f);
            experiment.OnSessionResolved();

            experiment.Tick(3f);

            Assert.IsTrue(experiment.PromptVisible);
            Assert.IsTrue(experiment.HandlePress());
            Assert.IsFalse(experiment.ReachedWithoutPrompt);

            experiment.ReadFlags(out var requested, out var withoutPrompt);
            Assert.IsTrue(requested);
            Assert.IsFalse(withoutPrompt);
        }

        [Test]
        public void FlagsFavourTheStrongerSignal()
        {
            var experiment = new ReplayExperiment(quietWindow: 2.5f, inputLockout: 0.5f);
            experiment.OnSessionResolved();
            experiment.Tick(1f);
            experiment.HandlePress();

            experiment.ReadFlags(out var requested, out var withoutPrompt);

            Assert.IsTrue(withoutPrompt);
            Assert.IsFalse(requested, "an unprompted reach is not also a prompted request");
        }

        [Test]
        public void PressesBeforeASessionResolvesDoNothing()
        {
            var experiment = new ReplayExperiment();

            Assert.IsFalse(experiment.HandlePress());
            Assert.IsFalse(experiment.IsActive);
        }

        [Test]
        public void ResetDisarmsForTheNextSession()
        {
            var experiment = new ReplayExperiment();
            experiment.OnSessionResolved();
            experiment.Tick(5f);
            experiment.Reset();

            Assert.IsFalse(experiment.IsActive);
            Assert.IsFalse(experiment.PromptVisible);
        }
    }

    // =================================================================
    // Runner integration for the replay flags
    // =================================================================
    [TestFixture]
    public class ReplayFlagRunnerTests
    {
        [Test]
        public void AnUnpromptedReplayIsFlaggedSeparately()
        {
            var rules = new FakeRules();
            var sink = new InMemoryMetricsSink();
            var runner = new PrototypeRunner(rules, sink) { TesterId = "T007" };

            runner.Begin(1);
            rules.ResolveOnNextTick = SessionStatus.Lost;
            runner.Tick(0.1f);

            runner.RequestReplay(2, withoutPrompt: true);
            rules.ResolveOnNextTick = SessionStatus.Lost;
            runner.Tick(0.1f);

            var second = sink.Reports[1];
            Assert.IsTrue(second.ReplayWithoutPrompt);
            Assert.IsFalse(second.ReplayRequested, "the two signals are not the same evidence");
            Assert.AreEqual("T007", second.TesterId);
        }

        [Test]
        public void TesterIdIsWrittenIntoEveryReport()
        {
            var rules = new FakeRules();
            var sink = new InMemoryMetricsSink();
            var runner = new PrototypeRunner(rules, sink) { TesterId = "T042" };

            runner.Begin(1);
            rules.ResolveOnNextTick = SessionStatus.Won;
            runner.Tick(0.1f);

            Assert.AreEqual("T042", sink.Last.TesterId);
        }

        [Test]
        public void TimeToFirstSuccessIsMeasuredFromSessionStart()
        {
            var rules = new FakeRules();
            var sink = new InMemoryMetricsSink();
            var runner = new PrototypeRunner(rules, sink);

            runner.Begin(1);
            for (var i = 0; i < 20; i++) runner.Tick(0.1f);   // 2.0s

            rules.SuccessfulActions = 1;
            runner.Tick(0.1f);                                 // noticed here

            for (var i = 0; i < 10; i++) runner.Tick(0.1f);
            rules.ResolveOnNextTick = SessionStatus.Lost;
            runner.Tick(0.1f);

            Assert.IsTrue(sink.Last.HadSuccess);
            Assert.AreEqual(2.1f, sink.Last.TimeToFirstSuccess, 0.05f);
        }

        [Test]
        public void NeverSucceedingIsRecordedAsSuch()
        {
            var rules = new FakeRules();
            var sink = new InMemoryMetricsSink();
            var runner = new PrototypeRunner(rules, sink);

            runner.Begin(1);
            runner.SubmitInput(InputCommand.Began(0.5f, 0.5f));
            rules.ResolveOnNextTick = SessionStatus.Lost;
            runner.Tick(0.1f);

            Assert.IsTrue(sink.Last.HadInput);
            Assert.IsFalse(sink.Last.HadSuccess, "touched the screen but never got anywhere");
        }

        [Test]
        public void CustomMetricsReachTheReport()
        {
            var rules = new FakeRules();
            var sink = new InMemoryMetricsSink();
            var runner = new PrototypeRunner(rules, sink);

            runner.Begin(1);
            rules.ResolveOnNextTick = SessionStatus.Won;
            runner.Tick(0.1f);

            Assert.AreEqual(1d, sink.Last.Custom_("fake.marker"));
        }
    }

    // =================================================================
    // Playtest round
    // =================================================================
    [TestFixture]
    public class PlaytestRoundTests
    {
        [Test]
        public void ARoundCoversEveryCandidateExactlyOnce()
        {
            var round = new PlaytestRound("T001", new DeterministicRng(5));

            Assert.AreEqual(4, round.Total);
            CollectionAssert.AreEquivalent(PrototypeIds.Candidates, round.Order);
        }

        [Test]
        public void OrderIsShuffledSoTheFirstSlotIsNotAlwaysTheSame()
        {
            // Whichever prototype goes first meets a fresh tester. Always
            // testing in one order would bake that advantage in invisibly.
            var firsts = new HashSet<PrototypeId>();
            for (var seed = 0; seed < 40; seed++)
            {
                firsts.Add(new PlaytestRound("T001", new DeterministicRng(seed)).Order[0]);
            }

            Assert.Greater(firsts.Count, 1, "the running order must actually vary");
        }

        [Test]
        public void TheSameSeedReproducesTheSameOrder()
        {
            var a = new PlaytestRound("T001", new DeterministicRng(9));
            var b = new PlaytestRound("T001", new DeterministicRng(9));

            CollectionAssert.AreEqual(a.Order, b.Order);
        }

        [Test]
        public void AdvancingWalksThroughAllFourThenFinishes()
        {
            var round = new PlaytestRound("T001", new DeterministicRng(3));
            var seen = new List<PrototypeId>();

            while (!round.Finished)
            {
                seen.Add(round.Current.Value);
                round.Advance();
            }

            Assert.AreEqual(4, seen.Count);
            Assert.IsTrue(round.Finished);
            Assert.IsNull(round.Current);
            Assert.AreEqual(4, round.Completed);
        }

        [Test]
        public void WithoutAGeneratorTheOrderIsLeftAlone()
        {
            var round = new PlaytestRound("T001");
            CollectionAssert.AreEqual(PrototypeIds.Candidates, round.Order);
        }

        [Test]
        public void AnswersAreStoredAgainstTheTester()
        {
            var round = new PlaytestRound("T003", new DeterministicRng(1));

            round.Answers.Answer(PostTestQuestion.PlayAgain, PrototypeId.MioMix);
            round.Answers.Answer(PostTestQuestion.EasiestToUnderstand, PrototypeId.Pop);
            round.Answers.Answer(PostTestQuestion.MostSatisfying, PrototypeId.MioMix);
            round.Answers.Answer(PostTestQuestion.MostDifferent, PrototypeId.MergeFactory);
            round.Answers.Answer(PostTestQuestion.WouldOpenNow, PrototypeId.MioMix);

            Assert.AreEqual("T003", round.Answers.TesterId);
            Assert.IsTrue(round.Answers.Complete);
            Assert.IsTrue(round.Answers.TryGet(PostTestQuestion.PlayAgain, out var choice));
            Assert.AreEqual(PrototypeId.MioMix, choice);
        }

        [Test]
        public void EveryQuestionHasText()
        {
            foreach (PostTestQuestion q in System.Enum.GetValues(typeof(PostTestQuestion)))
            {
                Assert.IsNotEmpty(PostTestAnswers.TextOf(q));
            }
        }
    }

    // =================================================================
    // Comparison report
    // =================================================================
    [TestFixture]
    public class PrototypeComparisonReportTests
    {
        private static MetricReport Report(
            PrototypeId id,
            string tester = "T001",
            int score = 100,
            bool objectiveDone = false,
            bool replay = false,
            bool unprompted = false,
            float firstSuccess = 2f,
            int ok = 10,
            int fail = 2,
            float duration = 60f,
            SessionStatus status = SessionStatus.Lost)
        {
            return new MetricReport
            {
                PrototypeId = id,
                TesterId = tester,
                Score = score,
                ObjectiveCompleted = objectiveDone,
                ReplayRequested = replay,
                ReplayWithoutPrompt = unprompted,
                TimeToFirstInput = 1f,
                TimeToFirstSuccess = firstSuccess,
                SuccessfulActions = ok,
                FailedActions = fail,
                SessionDuration = duration,
                CompletionStatus = status
            };
        }

        [Test]
        public void EmptyInputProducesAnEmptyReport()
        {
            var report = PrototypeComparisonReport.Build(null);

            Assert.AreEqual(0, report.TotalSessions);
            Assert.IsNull(report.For(PrototypeId.Pop));
        }

        [Test]
        public void AbandonedSessionsAreExcluded()
        {
            // An interruption says nothing about the design.
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.Pop),
                Report(PrototypeId.Pop, status: SessionStatus.Abandoned)
            });

            Assert.AreEqual(1, report.TotalSessions);
            Assert.AreEqual(1, report.For(PrototypeId.Pop).SessionsPlayed);
        }

        [Test]
        public void CompletionAndReplayRatesAreShares()
        {
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.Stack, objectiveDone: true, replay: true),
                Report(PrototypeId.Stack, objectiveDone: false),
                Report(PrototypeId.Stack, objectiveDone: true, unprompted: true),
                Report(PrototypeId.Stack, objectiveDone: false)
            });

            var s = report.For(PrototypeId.Stack);

            Assert.AreEqual(4, s.SessionsPlayed);
            Assert.AreEqual(0.5f, s.CompletionRate, 0.0001f);
            Assert.AreEqual(0.5f, s.ReplayRate, 0.0001f, "both replay kinds count");
            Assert.AreEqual(0.25f, s.ReplayWithoutPromptRate, 0.0001f);
        }

        [Test]
        public void SessionsThatNeverSucceededDoNotDragTheAverageBelowZero()
        {
            // Averaging in a -1 sentinel would hide the real figure.
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.Pop, firstSuccess: 4f),
                Report(PrototypeId.Pop, firstSuccess: -1f)
            });

            var s = report.For(PrototypeId.Pop);

            Assert.AreEqual(4d, s.AverageTimeToFirstSuccess, 0.0001d);
            Assert.AreEqual(1, s.SessionsWithoutSuccess);
        }

        [Test]
        public void FailureRateIsOverActionsNotSessions()
        {
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.MergeFactory, ok: 8, fail: 2),
                Report(PrototypeId.MergeFactory, ok: 12, fail: 3)
            });

            Assert.AreEqual(0.2f, report.For(PrototypeId.MergeFactory).FailureRate, 0.0001f);
        }

        [Test]
        public void SessionsPerTesterCountsDistinctTesters()
        {
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.MioMix, tester: "T001"),
                Report(PrototypeId.MioMix, tester: "T001"),
                Report(PrototypeId.MioMix, tester: "T002")
            });

            var s = report.For(PrototypeId.MioMix);

            Assert.AreEqual(2, s.TesterCount);
            Assert.AreEqual(1.5d, s.AverageSessionsPerTester, 0.0001d);
        }

        [Test]
        public void LargestComboReadsThePrototypesOwnCounter()
        {
            var a = Report(PrototypeId.Pop);
            a.Custom["pop.largestGroup"] = 9d;

            var b = Report(PrototypeId.Pop);
            b.Custom["pop.largestGroup"] = 14d;

            Assert.AreEqual(14d, PrototypeComparisonReport.Build(new[] { a, b })
                .For(PrototypeId.Pop).LargestCombo, 0.0001d);
        }

        [Test]
        public void ObjectiveTimeIgnoresSessionsThatNeverGotThere()
        {
            var a = Report(PrototypeId.MioMix, objectiveDone: true);
            a.Custom["miomix.objectiveCompletedAt"] = 30d;

            var b = Report(PrototypeId.MioMix);
            b.Custom["miomix.objectiveCompletedAt"] = -1d;

            Assert.AreEqual(30d, PrototypeComparisonReport.Build(new[] { a, b })
                .For(PrototypeId.MioMix).AverageObjectiveSeconds, 0.0001d);
        }

        [Test]
        public void ActionsPerMinuteUsesSessionDuration()
        {
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.Pop, ok: 30, fail: 0, duration: 60f)
            });

            Assert.AreEqual(30d, report.For(PrototypeId.Pop).AverageActionsPerMinute, 0.01d);
        }

        [Test]
        public void TheTableRendersAllFourColumnsAndNamesNoWinner()
        {
            var report = PrototypeComparisonReport.Build(new[]
            {
                Report(PrototypeId.Stack),
                Report(PrototypeId.MergeFactory),
                Report(PrototypeId.Pop),
                Report(PrototypeId.MioMix)
            });

            var table = report.ToTable();

            StringAssert.Contains("STACK", table);
            StringAssert.Contains("MERGE", table);
            StringAssert.Contains("POP", table);
            StringAssert.Contains("MIO MIX", table);
            StringAssert.Contains("one-more rate", table);
            StringAssert.Contains("No winner", table);
        }

        [Test]
        public void MissingPrototypesRenderAsAPlaceholderRatherThanZero()
        {
            // A prototype nobody played must not look like one that scored 0.
            var report = PrototypeComparisonReport.Build(new[] { Report(PrototypeId.Pop) });

            Assert.IsNull(report.For(PrototypeId.Stack));
            StringAssert.Contains("—", report.ToTable());
        }
    }
}
