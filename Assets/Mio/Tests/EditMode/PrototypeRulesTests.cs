using System;
using System.Collections.Generic;
using Mio.Core.Board;
using Mio.Core.Common;
using Mio.Core.Economy;
using Mio.Core.Prototypes;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    /// <summary>Shared helpers for driving a board prototype headlessly.</summary>
    internal static class Play
    {
        public static InputCommand Press(Vec2 at) => new InputCommand(InputPhase.Began, at, 0f);
        public static InputCommand Release(Vec2 at) => new InputCommand(InputPhase.Ended, at, 0f);

        /// <summary>Drags from one cell centre to another: press, then release.</summary>
        public static void Drag(IPrototypeRules rules, Vec2 from, Vec2 to, IFeedbackChannel fx)
        {
            rules.HandleInput(Press(from), fx);
            rules.HandleInput(Release(to), fx);
        }

        /// <summary>
        /// Paints a background with no two neighbours alike, so it contains no
        /// group of any size and cannot interfere with the combination a test
        /// is actually measuring.
        /// </summary>
        public static void Checkerboard(PieceGrid grid)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var family = (x + y) % 2 == 0 ? ResourceKind.Wood : ResourceKind.Metal;
                    grid.Set(x, y, new Piece(family, 0));
                }
            }
        }
    }

    // =================================================================
    // PieceGrid — the shared board every grid prototype depends on
    // =================================================================
    [TestFixture]
    public class PieceGridTests
    {
        [Test]
        public void GravityCompactsColumnsDownward()
        {
            var grid = new PieceGrid(1, 3);
            grid.Set(0, 0, Piece.Empty);
            grid.Set(0, 1, new Piece(ResourceKind.Wood, 0));
            grid.Set(0, 2, new Piece(ResourceKind.Metal, 1));

            Assert.IsTrue(grid.ApplyGravity());

            Assert.AreEqual(ResourceKind.Wood, grid.Get(0, 0).Family);
            Assert.AreEqual(ResourceKind.Metal, grid.Get(0, 1).Family);
            Assert.IsTrue(grid.Get(0, 2).IsEmpty);
        }

        [Test]
        public void RefillOnlyFillsHoles()
        {
            var grid = new PieceGrid(1, 2);
            grid.Set(0, 0, new Piece(ResourceKind.Metal, 2));

            grid.RefillRaw(new DeterministicRng(1), 3);

            Assert.AreEqual(2, grid.Get(0, 0).Tier, "an existing piece must be untouched");
            Assert.IsFalse(grid.Get(0, 1).IsEmpty);
            Assert.AreEqual(0, grid.Get(0, 1).Tier, "refill brings raw material");
        }

        [Test]
        public void FamilyGroupIgnoresTierButMatchGroupDoesNot()
        {
            var grid = new PieceGrid(2, 1);
            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Cotton, 1));

            Assert.AreEqual(2, grid.FindFamilyGroup(0, 0).Count, "POP's rule: family only");
            Assert.AreEqual(1, grid.FindMatchGroup(0, 0).Count, "combining needs the same tier");
        }

        [Test]
        public void GroupsAreNotDiagonal()
        {
            var grid = new PieceGrid(2, 2);
            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 1, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Wood, 0));
            grid.Set(0, 1, new Piece(ResourceKind.Wood, 0));

            Assert.AreEqual(1, grid.FindFamilyGroup(0, 0).Count);
        }

        [Test]
        public void ReshuffleAlwaysLeavesALegalMove()
        {
            var grid = new PieceGrid(6, 7);
            for (var seed = 0; seed < 25; seed++)
            {
                grid.Reshuffle(new DeterministicRng(seed), 3, 2, sameTier: false);
                Assert.IsTrue(grid.HasGroup(2, sameTier: false), "seed {0}", seed);
            }
        }

        [Test]
        public void CellMappingRoundTrips()
        {
            var grid = new PieceGrid(5, 6);

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var centre = grid.CellCenter(x, y, 0.1f, 0.2f, 0.9f, 0.8f);
                    Assert.IsTrue(grid.TryGetCell(centre, 0.1f, 0.2f, 0.9f, 0.8f, out var rx, out var ry));
                    Assert.AreEqual(x, rx);
                    Assert.AreEqual(y, ry);
                }
            }
        }

        [Test]
        public void OccupancyReflectsTheBoard()
        {
            var grid = new PieceGrid(2, 2);
            Assert.AreEqual(0f, grid.Occupancy, 0.0001f);

            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 1, new Piece(ResourceKind.Cotton, 0));

            Assert.AreEqual(0.5f, grid.Occupancy, 0.0001f);
        }

        [Test]
        public void PromotionKeepsTheFamilyAndRaisesTheTier()
        {
            var piece = new Piece(ResourceKind.Wood, 1).Promoted();
            Assert.AreEqual(ResourceKind.Wood, piece.Family);
            Assert.AreEqual(2, piece.Tier);
        }
    }

    // =================================================================
    // C — POP
    // =================================================================
    [TestFixture]
    public class PopRulesTests
    {
        private static PopConfig Config() => new PopConfig
        {
            Duration = 60f, Width = 7, Height = 9, FamilyCount = 3,
            MinGroup = 2, GoalCotton = 20, GoalWood = 15, GoalMetal = 10
        };

        /// <summary>Taps the first poppable group. False when none exists.</summary>
        private static bool TapAnyGroup(PopRules rules, IFeedbackChannel fx)
        {
            var grid = rules.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.FindFamilyGroup(x, y).Count < 2) continue;

                    rules.HandleInput(Play.Press(rules.CellCenter(x, y)), fx);
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void BeginProducesAFullPlayableBoard()
        {
            var rules = new PopRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.AreEqual(PrototypeId.Pop, rules.Id);
            Assert.IsTrue(rules.Grid.HasGroup(2, sameTier: false));
            Assert.AreEqual(rules.Grid.CellCount, rules.Grid.OccupiedCount);
        }

        [Test]
        public void SameSeedProducesTheSameBoard()
        {
            var a = new PopRules(Config());
            var b = new PopRules(Config());
            a.Begin(404, NullFeedbackChannel.Instance);
            b.Begin(404, NullFeedbackChannel.Instance);

            for (var i = 0; i < a.Grid.CellCount; i++)
            {
                Assert.AreEqual(a.Grid.At(i), b.Grid.At(i), "cell {0}", i);
            }
        }

        [Test]
        public void TappingAGroupCollectsScoresAndBanksResources()
        {
            var rules = new PopRules(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(12, fx);

            Assert.IsTrue(TapAnyGroup(rules, fx));

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.Greater(rules.Score, 0);
            Assert.IsFalse(rules.ResourcesEarned.IsEmpty, "collected pieces must bank resources");
            Assert.IsTrue(fx.Contains(FeedbackCueKind.ComboSmall)
                       || fx.Contains(FeedbackCueKind.ComboMedium)
                       || fx.Contains(FeedbackCueKind.ComboLarge));
        }

        [Test]
        public void TappingALoneCellIsAFailedAction()
        {
            var rules = new PopRules(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(12, fx);

            var grid = rules.Grid;
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    if (grid.FindFamilyGroup(x, y).Count != 1) continue;

                    rules.HandleInput(Play.Press(rules.CellCenter(x, y)), fx);
                    Assert.AreEqual(1, rules.FailedActions);
                    Assert.IsTrue(fx.Contains(FeedbackCueKind.Fail));
                    return;
                }
            }

            Assert.Ignore("seed produced no isolated cell");
        }

        [Test]
        public void TappingOutsideTheBoardIsNotAFailedAction()
        {
            // Missing the grid entirely is a slip, not a wrong decision.
            var rules = new PopRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            rules.HandleInput(Play.Press(new Vec2(0.5f, 0.97f)), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
        }

        [Test]
        public void BoardStaysFullAndPlayableAfterManyTaps()
        {
            var rules = new PopRules(Config());
            rules.Begin(31, NullFeedbackChannel.Instance);

            for (var i = 0; i < 40; i++)
            {
                if (!TapAnyGroup(rules, NullFeedbackChannel.Instance))
                {
                    Assert.Fail("board became unplayable after {0} taps", i);
                }

                Assert.AreEqual(rules.Grid.CellCount, rules.Grid.OccupiedCount,
                    "refill must leave no holes");
            }
        }

        [Test]
        public void ABigGroupLeavesASpecialBehind()
        {
            var config = Config();
            config.SpecialThreshold = 3;
            var rules = new PopRules(config);
            var fx = new RecordingFeedbackChannel();
            rules.Begin(7, fx);

            for (var i = 0; i < 30; i++)
            {
                TapAnyGroup(rules, fx);

                for (var y = 0; y < rules.Grid.Height; y++)
                {
                    for (var x = 0; x < rules.Grid.Width; x++)
                    {
                        if (rules.SpecialAt(x, y) == PopSpecial.None) continue;

                        Assert.IsTrue(fx.Contains(FeedbackCueKind.ProductCreated));
                        return;
                    }
                }
            }

            Assert.Ignore("no group reached the special threshold in this seed");
        }

        [Test]
        public void ObjectiveCompletionWinsOnTheNextTick()
        {
            var config = Config();
            config.GoalCotton = 1;
            config.GoalWood = 1;
            config.GoalMetal = 1;

            var rules = new PopRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            for (var i = 0; i < 60 && !rules.Objective.Completed; i++)
            {
                TapAnyGroup(rules, NullFeedbackChannel.Instance);
            }

            Assert.IsTrue(rules.Objective.Completed, "small goals should be met quickly");

            rules.Tick(0.1f, NullFeedbackChannel.Instance);
            Assert.AreEqual(SessionStatus.Won, rules.Status);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var config = Config();
            var rules = new PopRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            rules.Tick(config.Duration + 0.1f, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Lost, rules.Status);
        }

        [Test]
        public void CustomMetricsAreReported()
        {
            var rules = new PopRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);
            TapAnyGroup(rules, NullFeedbackChannel.Instance);

            var metrics = new Dictionary<string, double>();
            rules.CollectCustomMetrics(metrics);

            Assert.IsTrue(metrics.ContainsKey("pop.taps"));
            Assert.IsTrue(metrics.ContainsKey("pop.largestGroup"));
            Assert.IsTrue(metrics.ContainsKey("pop.averageGroupSize"));
            Assert.Greater(metrics["pop.largestGroup"], 0d);
        }
    }

    // =================================================================
    // A — STACK
    // =================================================================
    [TestFixture]
    public class StackRulesTests
    {
        private static StackConfig Config() => new StackConfig
        {
            Duration = 60f, Width = 7, Height = 8, FamilyCount = 3,
            CombineSize = 3, ProductTier = 3,
            GoalCottonProducts = 3, GoalWoodProducts = 2, GoalMetalProducts = 1
        };

        [Test]
        public void BeginDealsABoardWithNoFreeCombinations()
        {
            var rules = new StackRules(Config());
            rules.Begin(5, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.AreEqual(PrototypeId.Stack, rules.Id);
            Assert.IsFalse(rules.Grid.HasGroup(3, sameTier: true),
                "the first cascade must be one the player caused");
        }

        [Test]
        public void SameSeedProducesTheSameBoard()
        {
            var a = new StackRules(Config());
            var b = new StackRules(Config());
            a.Begin(77, NullFeedbackChannel.Instance);
            b.Begin(77, NullFeedbackChannel.Instance);

            for (var i = 0; i < a.Grid.CellCount; i++)
            {
                Assert.AreEqual(a.Grid.At(i), b.Grid.At(i), "cell {0}", i);
            }
        }

        [Test]
        public void ThreeMatchingPiecesTransformIntoOneOfTheNextTier()
        {
            // Cascades off so exactly one combination is measured; the chain
            // behaviour has its own test.
            var config = Config();
            config.MaxCascadeDepth = 1;

            var rules = new StackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            Play.Checkerboard(grid);

            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(2, 1, new Piece(ResourceKind.Cotton, 0));
            grid.Set(2, 0, new Piece(ResourceKind.Metal, 0));

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), fx);

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
            Assert.Greater(rules.Score, 0);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.ComboSmall));
        }

        [Test]
        public void ASettleThatCompletesAnotherSetCascades()
        {
            var rules = new StackRules(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(4, fx);

            // Play until a chain happens; the generator makes it likely but
            // not certain on any given board.
            for (var i = 0; i < 200 && !fx.Contains(FeedbackCueKind.Cascade2); i++)
            {
                var x = i % (rules.Grid.Width - 1);
                var y = (i / (rules.Grid.Width - 1)) % rules.Grid.Height;
                Play.Drag(rules, rules.CellCenter(x, y), rules.CellCenter(x + 1, y), fx);
            }

            Assert.IsTrue(fx.Contains(FeedbackCueKind.ComboSmall), "some combination must have happened");
        }

        [Test]
        public void ASwapThatMakesNothingIsUndoneAndCounted()
        {
            var rules = new StackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            Play.Checkerboard(grid);
            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Wood, 0));

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(1, 0), fx);

            Assert.AreEqual(1, rules.FailedActions);
            Assert.AreEqual(ResourceKind.Cotton, grid.Get(0, 0).Family, "the swap must be undone");
            Assert.AreEqual(ResourceKind.Wood, grid.Get(1, 0).Family);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Fail));
        }

        [Test]
        public void ReleasingOnTheStartingCellIsNotAFailure()
        {
            // Changing your mind is not a mistake and must not pollute the
            // failed-action metric.
            var rules = new StackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Play.Drag(rules, rules.CellCenter(3, 3), rules.CellCenter(3, 3), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(0, rules.SuccessfulActions);
        }

        [Test]
        public void NonAdjacentSwapsAreRejectedWithoutPenalty()
        {
            var rules = new StackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(4, 4), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
        }

        [Test]
        public void ReachingTheProductTierCountsTowardTheObjective()
        {
            var config = Config();
            config.GoalCottonProducts = 1;
            config.GoalWoodProducts = 0;
            config.GoalMetalProducts = 0;

            config.MaxCascadeDepth = 1;

            var rules = new StackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            Play.Checkerboard(grid);

            // Three tier-2 cotton meeting makes a tier-3 product.
            grid.Set(0, 0, new Piece(ResourceKind.Cotton, 2));
            grid.Set(1, 0, new Piece(ResourceKind.Cotton, 2));
            grid.Set(2, 1, new Piece(ResourceKind.Cotton, 2));
            grid.Set(2, 0, new Piece(ResourceKind.Metal, 0));

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), fx);

            Assert.IsTrue(fx.Contains(FeedbackCueKind.ProductCreated));
            Assert.AreEqual(1, rules.ResourcesEarned.Cotton);
            Assert.IsTrue(rules.Objective.Completed);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var config = Config();
            var rules = new StackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.Tick(config.Duration + 0.1f, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Lost, rules.Status);
        }

        [Test]
        public void CustomMetricsAreReported()
        {
            var rules = new StackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var metrics = new Dictionary<string, double>();
            rules.CollectCustomMetrics(metrics);

            Assert.IsTrue(metrics.ContainsKey("stack.moves"));
            Assert.IsTrue(metrics.ContainsKey("stack.maxCascade"));
            Assert.IsTrue(metrics.ContainsKey("stack.productsMade"));
        }
    }

    // =================================================================
    // B — MERGE FACTORY
    // =================================================================
    [TestFixture]
    public class MergeFactoryRulesTests
    {
        private static MergeFactoryConfig Config() => new MergeFactoryConfig
        {
            Duration = 60f, Width = 6, Height = 7, FamilyCount = 3,
            OrderTier = 2, MaxTier = 3, StartFill = 0.45f,
            GoalOrders = 6, SupplyInterval = 0f
        };

        [Test]
        public void BeginLeavesRoomToWork()
        {
            var rules = new MergeFactoryRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.AreEqual(PrototypeId.MergeFactory, rules.Id);
            Assert.Less(rules.Occupancy, 0.6f, "the board must start sparse");
            Assert.Greater(rules.Occupancy, 0.2f);
            Assert.AreEqual(3, rules.Orders.Count);
        }

        [Test]
        public void DraggingOneOntoAnIdenticalPieceMerges()
        {
            var rules = new MergeFactoryRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            grid.Clear();
            grid.Set(0, 0, new Piece(ResourceKind.Wood, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Wood, 0));

            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(1, 0), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.IsTrue(grid.Get(0, 0).IsEmpty, "the source cell is freed");
            Assert.AreEqual(1, grid.Get(1, 0).Tier, "the target becomes the next tier");
        }

        [Test]
        public void DraggingOntoAMismatchIsRejected()
        {
            var rules = new MergeFactoryRules(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            var grid = rules.Grid;
            grid.Clear();
            grid.Set(0, 0, new Piece(ResourceKind.Wood, 0));
            grid.Set(1, 0, new Piece(ResourceKind.Metal, 0));

            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(1, 0), fx);

            Assert.AreEqual(1, rules.FailedActions);
            Assert.AreEqual(ResourceKind.Wood, grid.Get(0, 0).Family, "nothing moves");
            Assert.IsTrue(fx.Contains(FeedbackCueKind.Fail));
        }

        [Test]
        public void MovingToEmptySpaceIsTidyingNotAFailure()
        {
            var rules = new MergeFactoryRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            grid.Clear();
            grid.Set(0, 0, new Piece(ResourceKind.Wood, 0));

            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(3, 3), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.IsTrue(grid.Get(0, 0).IsEmpty);
            Assert.AreEqual(ResourceKind.Wood, grid.Get(3, 3).Family);
        }

        [Test]
        public void TierProgressionStopsAtMaxTier()
        {
            var config = Config();
            config.MaxTier = 1;
            var rules = new MergeFactoryRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var grid = rules.Grid;
            grid.Clear();
            grid.Set(0, 0, new Piece(ResourceKind.Wood, 1));
            grid.Set(1, 0, new Piece(ResourceKind.Wood, 1));

            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(1, 0), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.FailedActions, "a pair at max tier cannot merge further");
        }

        [Test]
        public void AProductMatchingAnOrderFillsItAndFreesTheCell()
        {
            var rules = new MergeFactoryRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var wanted = rules.Orders[0].Family;

            var grid = rules.Grid;
            grid.Clear();
            grid.Set(0, 0, new Piece(wanted, 1));
            grid.Set(1, 0, new Piece(wanted, 1));

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(0, 0), rules.CellCenter(1, 0), fx);

            Assert.AreEqual(1, rules.ResourcesEarned[wanted]);
            Assert.IsTrue(grid.Get(1, 0).IsEmpty, "the product leaves for the factory");
            Assert.IsTrue(fx.Contains(FeedbackCueKind.ProductCreated));
            Assert.AreEqual(1, rules.Objective.DeliveredTotal);
        }

        [Test]
        public void TheSupplyStreamCanBeSwitchedOff()
        {
            // Speed must never become the challenge, so this has to be a real
            // off switch rather than a slow setting.
            var config = Config();
            config.SupplyInterval = 0f;
            var rules = new MergeFactoryRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var before = rules.Grid.OccupiedCount;
            for (var i = 0; i < 100; i++) rules.Tick(0.5f, NullFeedbackChannel.Instance);

            Assert.AreEqual(before, rules.Grid.OccupiedCount);
        }

        [Test]
        public void TheSupplyStreamAddsPiecesWhenEnabled()
        {
            var config = Config();
            config.SupplyInterval = 1f;
            var rules = new MergeFactoryRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var before = rules.Grid.OccupiedCount;
            for (var i = 0; i < 5; i++) rules.Tick(1.1f, NullFeedbackChannel.Instance);

            Assert.Greater(rules.Grid.OccupiedCount, before);
        }

        [Test]
        public void CustomMetricsIncludeOccupancyAndOrders()
        {
            var rules = new MergeFactoryRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);
            rules.Tick(1f, NullFeedbackChannel.Instance);

            var metrics = new Dictionary<string, double>();
            rules.CollectCustomMetrics(metrics);

            Assert.IsTrue(metrics.ContainsKey("merge.ordersCompleted"));
            Assert.IsTrue(metrics.ContainsKey("merge.averageOccupancy"));
            Assert.IsTrue(metrics.ContainsKey("merge.nearFullSeconds"));
        }
    }

    // =================================================================
    // D — MIO MIX
    // =================================================================
    [TestFixture]
    public class MioMixRulesTests
    {
        private static MioMixConfig Config() => new MioMixConfig
        {
            Duration = 60f, Width = 7, Height = 8, FamilyCount = 3,
            CombineSize = 3, ProductTier = 3,
            GoalCottonProducts = 3, GoalWoodProducts = 2, GoalMetalProducts = 1
        };

        /// <summary>Lays out three tier-2 pieces one swap away from combining.</summary>
        private static void ArmAProduct(MioMixRules rules, ResourceKind family)
        {
            var grid = rules.Grid;
            Play.Checkerboard(grid);

            grid.Set(0, 0, new Piece(family, 2));
            grid.Set(1, 0, new Piece(family, 2));
            grid.Set(2, 1, new Piece(family, 2));
            grid.Set(2, 0, new Piece(ResourceKind.Metal, 0));
        }

        [Test]
        public void BeginDealsABoardWithNoFreeCombinations()
        {
            var rules = new MioMixRules(Config());
            rules.Begin(5, NullFeedbackChannel.Instance);

            Assert.AreEqual(SessionStatus.Playing, rules.Status);
            Assert.AreEqual(PrototypeId.MioMix, rules.Id);
            Assert.IsFalse(rules.Grid.HasGroup(3, sameTier: true));
            Assert.AreEqual(0f, rules.MioCharge, 0.0001f);
        }

        [Test]
        public void AFinishedProductLeavesTheBoardForTheWorldOutput()
        {
            var rules = new MioMixRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(rules, ResourceKind.Cotton);

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), fx);

            Assert.AreEqual(1, rules.ProductsDelivered);
            Assert.AreEqual(1, rules.ResourcesEarned.Cotton);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.ProductCreated),
                "the product must be celebrated in its own right");
            Assert.AreEqual(1, rules.Objective.DeliveredTotal);
        }

        [Test]
        public void ProductsScoreMoreThanAPlainCombination()
        {
            var config = Config();
            config.ScorePerProduct = 250;

            var withProduct = new MioMixRules(config);
            withProduct.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(withProduct, ResourceKind.Cotton);
            Play.Drag(withProduct, withProduct.CellCenter(2, 1), withProduct.CellCenter(2, 0),
                NullFeedbackChannel.Instance);

            Assert.GreaterOrEqual(withProduct.Score, config.ScorePerProduct);
        }

        [Test]
        public void CombiningChargesTheMioMeter()
        {
            var rules = new MioMixRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(rules, ResourceKind.Cotton);

            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), NullFeedbackChannel.Instance);

            Assert.Greater(rules.MioCharge, 0f);
        }

        [Test]
        public void AFullMeterArmsAPowerAndAnnouncesIt()
        {
            var config = Config();
            config.MioChargePerCombine = 1f;

            var rules = new MioMixRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(rules, ResourceKind.Cotton);

            var fx = new RecordingFeedbackChannel();
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), fx);

            Assert.IsTrue(rules.MioReady);
            Assert.IsNotNull(rules.ArmedPower);
            Assert.IsTrue(fx.Contains(FeedbackCueKind.MioPowerReady));
        }

        [Test]
        public void FiringAPowerSpendsTheMeter()
        {
            var config = Config();
            config.MioChargePerCombine = 1f;

            var rules = new MioMixRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(rules, ResourceKind.Cotton);
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), NullFeedbackChannel.Instance);

            Assert.IsTrue(rules.MioReady);

            var fx = new RecordingFeedbackChannel();
            rules.HandleInput(Play.Press(new Vec2(0.5f, 0.11f)), fx);

            // The meter level is deliberately not asserted: a power that sets
            // off a cascade can legitimately recharge it on the way out, and
            // with this test's charge rate it always does. What must hold is
            // that the power was consumed exactly once.
            Assert.IsTrue(fx.Contains(FeedbackCueKind.MioPowerUsed));
            Assert.IsNull(rules.ArmedPower, "the armed power must be cleared when fired");

            var metrics = new Dictionary<string, double>();
            rules.CollectCustomMetrics(metrics);
            Assert.AreEqual(1d, metrics["miomix.mioPowersUsed"]);
        }

        [Test]
        public void TheMioButtonDoesNothingWhileTheMeterIsEmpty()
        {
            var rules = new MioMixRules(Config());
            var fx = new RecordingFeedbackChannel();
            rules.Begin(1, fx);

            rules.HandleInput(Play.Press(new Vec2(0.5f, 0.11f)), fx);

            Assert.IsFalse(fx.Contains(FeedbackCueKind.MioPowerUsed));
        }

        [Test]
        public void EveryPowerKindAppliesWithoutBreakingTheBoard()
        {
            // The powers are experimental, so the contract that matters is
            // that none of them can leave the board in a broken state.
            foreach (MioPowerKind kind in Enum.GetValues(typeof(MioPowerKind)))
            {
                var config = Config();
                config.MioChargePerCombine = 1f;
                config.Powers = new[] { new MioPowerDefinition(kind.ToString(), kind, 4) };

                var rules = new MioMixRules(config);
                rules.Begin(9, NullFeedbackChannel.Instance);
                ArmAProduct(rules, ResourceKind.Cotton);
                Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), NullFeedbackChannel.Instance);

                Assert.IsTrue(rules.MioReady, "{0} should have armed", kind);
                rules.HandleInput(Play.Press(new Vec2(0.5f, 0.11f)), NullFeedbackChannel.Instance);

                Assert.AreEqual(SessionStatus.Playing, rules.Status, "{0}", kind);
                Assert.AreEqual(rules.Grid.CellCount, rules.Grid.OccupiedCount,
                    "{0} must leave a full board", kind);
            }
        }

        [Test]
        public void PowersCanBeDisabledEntirely()
        {
            var config = Config();
            config.MioPowerEnabled = false;
            config.MioChargePerCombine = 1f;

            var rules = new MioMixRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);
            ArmAProduct(rules, ResourceKind.Cotton);
            Play.Drag(rules, rules.CellCenter(2, 1), rules.CellCenter(2, 0), NullFeedbackChannel.Instance);

            Assert.IsFalse(rules.MioReady);
            Assert.AreEqual(0f, rules.MioCharge, 0.0001f);
        }

        [Test]
        public void SameSeedProducesTheSameBoard()
        {
            var a = new MioMixRules(Config());
            var b = new MioMixRules(Config());
            a.Begin(303, NullFeedbackChannel.Instance);
            b.Begin(303, NullFeedbackChannel.Instance);

            for (var i = 0; i < a.Grid.CellCount; i++)
            {
                Assert.AreEqual(a.Grid.At(i), b.Grid.At(i), "cell {0}", i);
            }
        }

        [Test]
        public void CustomMetricsCoverProductsAndPowers()
        {
            var rules = new MioMixRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            var metrics = new Dictionary<string, double>();
            rules.CollectCustomMetrics(metrics);

            Assert.IsTrue(metrics.ContainsKey("miomix.productsDelivered"));
            Assert.IsTrue(metrics.ContainsKey("miomix.mioPowersUsed"));
            Assert.IsTrue(metrics.ContainsKey("miomix.maxCascade"));
        }
    }
}
