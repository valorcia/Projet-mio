using Mio.Core.Flow;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class FlowRulesTests
    {
        private static FlowConfig Config()
        {
            return new FlowConfig
            {
                Duration = 20f,
                FirstGateDelay = 1f,
                GateInterval = 1f,
                HazardChance = 0f,
                ProgressPerCollect = 0.2f,
                FollowSpeed = 2.2f
            };
        }

        /// <summary>
        /// Plays perfectly: every frame, steer at the next unresolved gate. The
        /// generator guarantees each gate is within reach of the last, so this
        /// should collect essentially everything.
        /// </summary>
        private static void PlayPerfectly(FlowRules rules, float seconds, float dt = 1f / 60f)
        {
            var steps = (int)(seconds / dt);
            for (var i = 0; i < steps && rules.Status == SessionStatus.Playing; i++)
            {
                for (var g = 0; g < rules.Gates.Count; g++)
                {
                    var gate = rules.Gates[g];
                    if (gate.Resolved) continue;

                    var targetX = gate.Kind == FlowGateKind.Collect
                        ? gate.CenterX
                        : SafeSideOf(gate);

                    rules.HandleInput(
                        InputCommand.Moved(targetX, rules.HeadY, rules.Elapsed),
                        NullFeedbackChannel.Instance);
                    break;
                }

                rules.Tick(dt, NullFeedbackChannel.Instance);
            }
        }

        private static float SafeSideOf(FlowGate gate)
        {
            var left = gate.CenterX - gate.HalfWidth - 0.02f;
            return left >= 0f ? left : gate.CenterX + gate.HalfWidth + 0.02f;
        }

        [Test]
        public void SameSeedGeneratesIdenticalTrack()
        {
            var a = new FlowRules(Config());
            var b = new FlowRules(Config());

            a.Begin(2024, NullFeedbackChannel.Instance);
            b.Begin(2024, NullFeedbackChannel.Instance);

            Assert.AreEqual(a.Gates.Count, b.Gates.Count);
            for (var i = 0; i < a.Gates.Count; i++)
            {
                Assert.AreEqual(a.Gates[i].CenterX, b.Gates[i].CenterX, 0.0001f, "gate {0}", i);
                Assert.AreEqual(a.Gates[i].TriggerTime, b.Gates[i].TriggerTime, 0.0001f);
                Assert.AreEqual(a.Gates[i].Kind, b.Gates[i].Kind);
            }
        }

        [Test]
        public void DifferentSeedsGenerateDifferentTracks()
        {
            var a = new FlowRules(Config());
            var b = new FlowRules(Config());

            a.Begin(1, NullFeedbackChannel.Instance);
            b.Begin(2, NullFeedbackChannel.Instance);

            var identical = true;
            for (var i = 0; i < a.Gates.Count && identical; i++)
            {
                if (System.Math.Abs(a.Gates[i].CenterX - b.Gates[i].CenterX) > 0.0001f) identical = false;
            }

            Assert.IsFalse(identical);
        }

        [Test]
        public void GatesAreOrderedInTime()
        {
            var rules = new FlowRules(Config());
            rules.Begin(5, NullFeedbackChannel.Instance);

            for (var i = 1; i < rules.Gates.Count; i++)
            {
                Assert.Greater(rules.Gates[i].TriggerTime, rules.Gates[i - 1].TriggerTime);
            }
        }

        [Test]
        public void EveryGateStaysOnScreen()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var rules = new FlowRules(new FlowConfig { Duration = 45f, HazardChance = 0.5f });
                rules.Begin(seed, NullFeedbackChannel.Instance);

                foreach (var gate in rules.Gates)
                {
                    Assert.GreaterOrEqual(gate.CenterX, 0f, "seed {0}", seed);
                    Assert.LessOrEqual(gate.CenterX, 1f, "seed {0}", seed);
                }
            }
        }

        [Test]
        public void ConsecutiveGatesAreAlwaysReachable()
        {
            // The fairness guarantee: if the player steers optimally they can
            // always make the next gate. A track that violates this would make a
            // playtest fail for a reason that has nothing to do with the design.
            for (var seed = 0; seed < 40; seed++)
            {
                var config = new FlowConfig { Duration = 45f, HazardChance = 0.4f };
                var rules = new FlowRules(config);
                rules.Begin(seed, NullFeedbackChannel.Instance);

                PlayPerfectly(rules, config.Duration);

                var missed = 0;
                foreach (var gate in rules.Gates)
                {
                    if (!gate.Resolved) continue;
                    if (gate.Kind == FlowGateKind.Collect && !gate.WasHit) missed++;
                    if (gate.Kind == FlowGateKind.Hazard && gate.WasHit) missed++;
                }

                Assert.AreEqual(0, missed, "seed {0} produced an unreachable gate", seed);
            }
        }

        [Test]
        public void PerfectPlayFillsTheMeterAndWins()
        {
            var config = Config();
            var rules = new FlowRules(config);
            rules.Begin(11, NullFeedbackChannel.Instance);

            PlayPerfectly(rules, config.Duration);

            Assert.AreEqual(SessionStatus.Won, rules.Status);
            Assert.AreEqual(1f, rules.Progress01, 0.0001f);
            Assert.Greater(rules.Score, 0);
            Assert.GreaterOrEqual(rules.SuccessfulActions, 5);
        }

        [Test]
        public void NeverTouchingTheScreenLosesOnTime()
        {
            var config = Config();
            var rules = new FlowRules(config);
            rules.Begin(3, NullFeedbackChannel.Instance);

            for (var i = 0; i < (int)(config.Duration * 60) + 10; i++)
            {
                rules.Tick(1f / 60f, NullFeedbackChannel.Instance);
            }

            Assert.AreEqual(SessionStatus.Lost, rules.Status);
            Assert.AreEqual(0f, rules.TimeRemaining, 0.0001f);
        }

        [Test]
        public void MissingACollectibleCountsAsAFailedAction()
        {
            var config = new FlowConfig
            {
                Duration = 6f,
                FirstGateDelay = 1f,
                GateInterval = 1f,
                HazardChance = 0f,
                ProgressPerCollect = 0.05f
            };

            var rules = new FlowRules(config);
            rules.Begin(8, NullFeedbackChannel.Instance);

            // Park hard left; any gate not on the left edge is missed.
            rules.HandleInput(InputCommand.Began(0f, 0f), NullFeedbackChannel.Instance);
            for (var i = 0; i < 6 * 60; i++)
            {
                rules.Tick(1f / 60f, NullFeedbackChannel.Instance);
            }

            Assert.Greater(rules.FailedActions, 0);
            Assert.AreEqual(SessionStatus.Lost, rules.Status);
        }

        [Test]
        public void HazardHitCostsProgress()
        {
            var config = new FlowConfig
            {
                Duration = 5f,
                FirstGateDelay = 1f,
                GateInterval = 1f,
                HazardChance = 1f,
                ProgressPenaltyHazard = 0.25f
            };

            var rules = new FlowRules(config);
            rules.Begin(6, NullFeedbackChannel.Instance);

            // Steer straight into the centre of every hazard.
            for (var i = 0; i < 5 * 60 && rules.Status == SessionStatus.Playing; i++)
            {
                for (var g = 0; g < rules.Gates.Count; g++)
                {
                    if (rules.Gates[g].Resolved) continue;
                    rules.HandleInput(
                        InputCommand.Moved(rules.Gates[g].CenterX, rules.HeadY),
                        NullFeedbackChannel.Instance);
                    break;
                }

                rules.Tick(1f / 60f, NullFeedbackChannel.Instance);
            }

            Assert.Greater(rules.FailedActions, 0);
            Assert.AreEqual(0f, rules.Progress01, 0.0001f, "progress must clamp at zero, never go negative");
        }

        [Test]
        public void ProgressNeverExceedsOne()
        {
            var config = Config();
            var rules = new FlowRules(config);
            rules.Begin(77, NullFeedbackChannel.Instance);

            PlayPerfectly(rules, config.Duration);

            Assert.LessOrEqual(rules.Progress01, 1f);
        }

        [Test]
        public void HeadDoesNotTeleportToTheFinger()
        {
            // The stream has weight; that is what makes steering feel physical.
            var rules = new FlowRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(InputCommand.Began(1f, 0.2f), NullFeedbackChannel.Instance);
            rules.Tick(1f / 60f, NullFeedbackChannel.Instance);

            Assert.Less(rules.HeadX, 1f);
            Assert.Greater(rules.HeadX, 0.5f);
        }

        [Test]
        public void ReleasingTheFingerLeavesTheStreamInPlace()
        {
            var rules = new FlowRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(InputCommand.Began(0.2f, 0.2f), NullFeedbackChannel.Instance);
            for (var i = 0; i < 60; i++) rules.Tick(1f / 60f, NullFeedbackChannel.Instance);

            var parked = rules.HeadX;
            rules.HandleInput(InputCommand.Ended(0.9f, 0.2f), NullFeedbackChannel.Instance);
            for (var i = 0; i < 30; i++) rules.Tick(1f / 60f, NullFeedbackChannel.Instance);

            Assert.AreEqual(parked, rules.HeadX, 0.001f);
        }

        [Test]
        public void CountersAreExposedForTheViewAndHud()
        {
            var config = Config();
            var rules = new FlowRules(config);
            rules.Begin(11, NullFeedbackChannel.Instance);
            PlayPerfectly(rules, config.Duration);

            Assert.Greater(rules.Collected, 0);
            Assert.AreEqual(rules.Collected, rules.SuccessfulActions);
            Assert.AreEqual(rules.Missed + rules.HazardsHit, rules.FailedActions);
            Assert.Greater(rules.Gates.Count, 0);
        }

        [Test]
        public void IdentifiesItselfAsFlow()
        {
            Assert.AreEqual(PrototypeId.Flow, new FlowRules(Config()).Id);
        }

        [Test]
        public void FlowRunsThroughTheSharedFoundationEndToEnd()
        {
            // The same path the harness proved in M0.1, now driven by real
            // gameplay: normalised input in, reward banked and reported out.
            var config = Config();
            var rules = new FlowRules(config);
            var sink = new Mio.Core.Metrics.InMemoryMetricsSink();
            var store = new Mio.Core.Profile.InMemoryProfileStore();
            var wallet = new Mio.Core.Profile.PlayerWallet(store);

            var rewards = new Mio.Core.Economy.RewardTable(
                new Mio.Core.Economy.ResourceBundle(1, 0, 0),
                new Mio.Core.Economy.ResourceBundle(0, 0, 5),
                new[] { new Mio.Core.Economy.RewardTier(50, new Mio.Core.Economy.ResourceBundle(0, 2, 0)) });

            var runner = new PrototypeRunner(rules, sink, rewards, wallet);
            runner.Begin(11);

            // A real finger always presses before it drags, so the session
            // opens with Began and then steers with Moved. A whole FLOW run is
            // one continuous touch: input_count is 1 by design, and it is
            // successful_actions that carries the signal for this prototype.
            var dt = 1f / 60f;
            runner.Tick(dt);
            runner.SubmitInput(InputCommand.Began(0.5f, rules.HeadY));

            for (var i = 0; i < (int)(config.Duration / dt) && runner.Status == SessionStatus.Playing; i++)
            {
                for (var g = 0; g < rules.Gates.Count; g++)
                {
                    if (rules.Gates[g].Resolved) continue;
                    runner.SubmitInput(InputCommand.Moved(rules.Gates[g].CenterX, rules.HeadY));
                    break;
                }

                runner.Tick(dt);
            }

            Assert.AreEqual(SessionStatus.Won, runner.Status);

            var report = sink.Last;
            Assert.IsNotNull(report);
            Assert.AreEqual(PrototypeId.Flow, report.PrototypeId);
            Assert.AreEqual(11, report.Seed);
            Assert.IsTrue(report.Completed);
            Assert.Greater(report.Score, 0);
            Assert.AreEqual(1f, report.Progress, 0.0001f);
            Assert.IsTrue(report.HadInput, "the opening press must register");
            Assert.AreEqual(1, report.InputCount,
                "a whole FLOW session is one touch; the drag must not inflate the count");

            // Won above the 50 tier: participation + tier + completion bonus.
            Assert.AreEqual(1, wallet.Balance.Energy);
            Assert.AreEqual(2, wallet.Balance.Material);
            Assert.AreEqual(5, wallet.Balance.Coin);
        }
    }
}
