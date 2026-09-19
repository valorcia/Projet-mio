using Mio.Core.Common;
using Mio.Core.PopChain;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class PopChainBoardTests
    {
        /// <summary>Builds a board from rows given top-first, for readability.</summary>
        private static PopChainBoard FromRows(params int[][] rowsTopFirst)
        {
            var height = rowsTopFirst.Length;
            var width = rowsTopFirst[0].Length;
            var board = new PopChainBoard(width, height);

            for (var r = 0; r < height; r++)
            {
                var y = height - 1 - r;
                for (var x = 0; x < width; x++)
                {
                    board.Set(x, y, rowsTopFirst[r][x]);
                }
            }

            return board;
        }

        [Test]
        public void FindGroupCollectsConnectedSameColourCells()
        {
            var board = FromRows(
                new[] { 1, 1, 2 },
                new[] { 1, 2, 2 },
                new[] { 0, 0, 2 });

            var group = board.FindGroup(0, 2); // top-left '1'
            Assert.AreEqual(3, group.Count);
        }

        [Test]
        public void FindGroupIsNotDiagonal()
        {
            var board = FromRows(
                new[] { 1, 0 },
                new[] { 0, 1 });

            Assert.AreEqual(1, board.FindGroup(0, 1).Count);
        }

        [Test]
        public void FindGroupOutOfBoundsIsEmpty()
        {
            var board = new PopChainBoard(3, 3);
            Assert.AreEqual(0, board.FindGroup(-1, 0).Count);
            Assert.AreEqual(0, board.FindGroup(3, 0).Count);
        }

        [Test]
        public void GravityCompactsColumnsDownward()
        {
            var board = FromRows(
                new[] { 5 },
                new[] { PopChainBoard.Empty },
                new[] { 7 });

            Assert.IsTrue(board.ApplyGravity());

            Assert.AreEqual(7, board.Get(0, 0));
            Assert.AreEqual(5, board.Get(0, 1));
            Assert.AreEqual(PopChainBoard.Empty, board.Get(0, 2));
        }

        [Test]
        public void GravityReportsWhenNothingMoves()
        {
            var board = FromRows(
                new[] { 1 },
                new[] { 2 });

            Assert.IsFalse(board.ApplyGravity());
        }

        [Test]
        public void RefillOnlyFillsHoles()
        {
            var board = FromRows(
                new[] { PopChainBoard.Empty },
                new[] { 3 });

            board.Refill(new DeterministicRng(1), 4);

            Assert.AreEqual(3, board.Get(0, 0), "existing cell must be untouched");
            Assert.AreNotEqual(PopChainBoard.Empty, board.Get(0, 1));
        }

        [Test]
        public void HasMoveDetectsAnAdjacentPair()
        {
            var withPair = FromRows(
                new[] { 1, 1 },
                new[] { 2, 3 });
            Assert.IsTrue(withPair.HasMove(2));

            var checkerboard = FromRows(
                new[] { 1, 2, 1 },
                new[] { 2, 1, 2 },
                new[] { 1, 2, 1 });
            Assert.IsFalse(checkerboard.HasMove(2));
        }

        [Test]
        public void HasMoveHandlesLargerMinimumGroups()
        {
            var board = FromRows(
                new[] { 1, 1, 2 },
                new[] { 2, 2, 2 });

            Assert.IsTrue(board.HasMove(3));
            Assert.IsFalse(board.HasMove(5));
        }

        [Test]
        public void ReshuffleAlwaysLeavesALegalMove()
        {
            var board = new PopChainBoard(6, 8);
            for (var seed = 0; seed < 25; seed++)
            {
                board.Reshuffle(new DeterministicRng(seed), 4, 2);
                Assert.IsTrue(board.HasMove(2), "seed {0}", seed);
            }
        }
    }

    [TestFixture]
    public class PopChainRulesTests
    {
        private static PopChainConfig Config()
        {
            return new PopChainConfig
            {
                Duration = 45f,
                Width = 7,
                Height = 9,
                ColorCount = 4,
                MinGroupSize = 2,
                ScorePerCell = 10,
                TargetScore = 100000,
                ChainWindow = 1.5f
            };
        }

        /// <summary>Taps the first poppable group. Returns false if none exists.</summary>
        private static bool TapAnyGroup(PopChainRules rules)
        {
            var board = rules.Board;
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.FindGroup(x, y).Count < 2) continue;

                    var at = rules.CellCenter(x, y);
                    rules.HandleInput(
                        new InputCommand(InputPhase.Began, at, 0f),
                        NullFeedbackChannel.Instance);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Finds a cell that is alone in its colour, if there is one.</summary>
        private static bool TryFindLoneCell(PopChainRules rules, out Vec2 at)
        {
            var board = rules.Board;
            for (var y = 0; y < board.Height; y++)
            {
                for (var x = 0; x < board.Width; x++)
                {
                    if (board.FindGroup(x, y).Count == 1)
                    {
                        at = rules.CellCenter(x, y);
                        return true;
                    }
                }
            }

            at = Vec2.Zero;
            return false;
        }

        [Test]
        public void BeginProducesAFullPlayableBoard()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Playing, rules.Status);
            Assert.IsTrue(rules.Board.HasMove(2));

            for (var y = 0; y < rules.Board.Height; y++)
            {
                for (var x = 0; x < rules.Board.Width; x++)
                {
                    Assert.AreNotEqual(PopChainBoard.Empty, rules.Board.Get(x, y));
                }
            }
        }

        [Test]
        public void SameSeedProducesSameBoard()
        {
            var a = new PopChainRules(Config());
            var b = new PopChainRules(Config());
            a.Begin(404, NullFeedbackChannel.Instance);
            b.Begin(404, NullFeedbackChannel.Instance);

            for (var y = 0; y < a.Board.Height; y++)
            {
                for (var x = 0; x < a.Board.Width; x++)
                {
                    Assert.AreEqual(a.Board.Get(x, y), b.Board.Get(x, y));
                }
            }
        }

        [Test]
        public void PoppingAGroupScoresAndCountsAsSuccess()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            Assert.IsTrue(TapAnyGroup(rules));

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
            Assert.Greater(rules.Score, 0);
        }

        [Test]
        public void TappingATooSmallGroupCountsAsAFailedAction()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            if (!TryFindLoneCell(rules, out var at))
            {
                Assert.Ignore("seed produced no isolated cell");
            }

            rules.HandleInput(new InputCommand(InputPhase.Began, at, 0f), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.FailedActions);
            Assert.AreEqual(0, rules.Score);
        }

        [Test]
        public void TappingOutsideTheBoardIsNotAFailedAction()
        {
            // Missing the grid entirely is a slip, not a wrong decision. Counting
            // it would poison the success-rate metric.
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            rules.HandleInput(InputCommand.Began(0.5f, 0.97f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
        }

        [Test]
        public void OnlyPressResolvesATap()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            var at = rules.CellCenter(0, 0);
            rules.HandleInput(new InputCommand(InputPhase.Moved, at, 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, at, 0f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.Score);
            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.AreEqual(0, rules.FailedActions);
        }

        [Test]
        public void FirstPopDoesNotStartAMultiplier()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            TapAnyGroup(rules);

            Assert.AreEqual(1, rules.ChainMultiplier);
        }

        [Test]
        public void PoppingAgainInsideTheWindowRaisesTheMultiplier()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            TapAnyGroup(rules);
            rules.Tick(0.2f, NullFeedbackChannel.Instance);
            TapAnyGroup(rules);

            Assert.AreEqual(2, rules.ChainMultiplier);
            Assert.Greater(rules.ChainTimeRemaining, 0f);

            rules.Tick(0.2f, NullFeedbackChannel.Instance);
            TapAnyGroup(rules);
            Assert.AreEqual(3, rules.ChainMultiplier);
        }

        [Test]
        public void LettingTheWindowLapseResetsTheMultiplier()
        {
            var config = Config();
            var rules = new PopChainRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            TapAnyGroup(rules);
            rules.Tick(0.2f, NullFeedbackChannel.Instance);
            TapAnyGroup(rules);
            Assert.AreEqual(2, rules.ChainMultiplier);

            rules.Tick(config.ChainWindow + 0.2f, NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.ChainMultiplier);
            Assert.AreEqual(0f, rules.ChainTimeRemaining, 0.0001f);
        }

        [Test]
        public void MultiplierIsCapped()
        {
            var config = Config();
            config.MaxChainMultiplier = 3;
            var rules = new PopChainRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            for (var i = 0; i < 10; i++)
            {
                TapAnyGroup(rules);
                rules.Tick(0.05f, NullFeedbackChannel.Instance);
            }

            Assert.AreEqual(3, rules.ChainMultiplier);
        }

        [Test]
        public void BoardStaysFullAndPlayableAfterManyPops()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(31, NullFeedbackChannel.Instance);

            for (var i = 0; i < 60; i++)
            {
                if (!TapAnyGroup(rules)) Assert.Fail("board became unplayable after {0} pops", i);
                rules.Tick(0.05f, NullFeedbackChannel.Instance);

                for (var y = 0; y < rules.Board.Height; y++)
                {
                    for (var x = 0; x < rules.Board.Width; x++)
                    {
                        Assert.AreNotEqual(PopChainBoard.Empty, rules.Board.Get(x, y));
                    }
                }
            }
        }

        [Test]
        public void ReachingTargetScoreWins()
        {
            var config = Config();
            config.TargetScore = 20;
            var rules = new PopChainRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            TapAnyGroup(rules);
            rules.Tick(0.05f, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Won, rules.Status);
            Assert.AreEqual(1f, rules.Progress01, 0.0001f);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var config = Config();
            var rules = new PopChainRules(config);
            rules.Begin(12, NullFeedbackChannel.Instance);

            rules.Tick(config.Duration + 0.1f, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Lost, rules.Status);
        }

        [Test]
        public void CellMappingRoundTrips()
        {
            var rules = new PopChainRules(Config());
            rules.Begin(12, NullFeedbackChannel.Instance);

            for (var y = 0; y < rules.Board.Height; y++)
            {
                for (var x = 0; x < rules.Board.Width; x++)
                {
                    Assert.IsTrue(rules.TryGetCell(rules.CellCenter(x, y), out var rx, out var ry));
                    Assert.AreEqual(x, rx);
                    Assert.AreEqual(y, ry);
                }
            }
        }
    }
}
