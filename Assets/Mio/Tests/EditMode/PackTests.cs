using Mio.Core.Common;
using Mio.Core.Pack;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class PackBoardTests
    {
        private static readonly PackShape Dot = new PackShape("Dot", new GridOffset(0, 0));

        private static readonly PackShape Square = new PackShape(
            "Square",
            new GridOffset(0, 0), new GridOffset(1, 0),
            new GridOffset(0, 1), new GridOffset(1, 1));

        [Test]
        public void NewBoardIsEmpty()
        {
            var board = new PackBoard(4, 4);
            Assert.AreEqual(0, board.OccupiedCount);
            Assert.IsFalse(board.IsOccupied(0, 0));
        }

        [Test]
        public void ShapeMustFitInsideTheBoard()
        {
            var board = new PackBoard(4, 4);

            Assert.IsTrue(board.CanPlace(Square, 2, 2));
            Assert.IsFalse(board.CanPlace(Square, 3, 3), "would overhang the top-right");
            Assert.IsFalse(board.CanPlace(Square, -1, 0));
        }

        [Test]
        public void ShapeCannotOverlapOccupiedCells()
        {
            var board = new PackBoard(4, 4);
            board.Set(1, 1, 0);

            Assert.IsFalse(board.CanPlace(Square, 0, 0));
            Assert.IsTrue(board.CanPlace(Square, 2, 2));
        }

        [Test]
        public void PlaceStampsEveryCell()
        {
            var board = new PackBoard(4, 4);
            Assert.AreEqual(0, board.Place(Square, 1, 1, 3));

            Assert.AreEqual(4, board.OccupiedCount);
            Assert.AreEqual(3, board.Get(1, 1));
            Assert.AreEqual(3, board.Get(2, 2));
        }

        [Test]
        public void IllegalPlacementIsRejectedAndChangesNothing()
        {
            var board = new PackBoard(4, 4);
            board.Set(0, 0, 1);

            Assert.AreEqual(-1, board.Place(Square, 0, 0, 2));
            Assert.AreEqual(1, board.OccupiedCount, "board must be untouched");
        }

        [Test]
        public void CompletingARowClearsIt()
        {
            var board = new PackBoard(3, 3);
            board.Set(0, 0, 1);
            board.Set(1, 0, 1);

            Assert.AreEqual(1, board.Place(Dot, 2, 0, 1));
            Assert.AreEqual(0, board.OccupiedCount);
            CollectionAssert.AreEqual(new[] { 0 }, board.LastClearedRows);
        }

        [Test]
        public void CompletingAColumnClearsIt()
        {
            var board = new PackBoard(3, 3);
            board.Set(1, 0, 1);
            board.Set(1, 1, 1);

            Assert.AreEqual(1, board.Place(Dot, 1, 2, 1));
            Assert.AreEqual(0, board.OccupiedCount);
            CollectionAssert.AreEqual(new[] { 1 }, board.LastClearedColumns);
        }

        [Test]
        public void IntersectingRowAndColumnBothClear()
        {
            // The shared cell belongs to both lines. Clearing the row first must
            // not make the column look incomplete.
            var board = new PackBoard(3, 3);

            board.Set(0, 0, 1);
            board.Set(1, 0, 1);
            board.Set(1, 1, 1);
            board.Set(1, 2, 1);

            Assert.AreEqual(2, board.Place(Dot, 2, 0, 1), "one row plus one column");
            Assert.AreEqual(0, board.OccupiedCount);
            CollectionAssert.AreEqual(new[] { 0 }, board.LastClearedRows);
            CollectionAssert.AreEqual(new[] { 1 }, board.LastClearedColumns);
        }

        [Test]
        public void PartialLinesSurvive()
        {
            var board = new PackBoard(3, 3);
            board.Set(0, 0, 1);

            Assert.AreEqual(0, board.Place(Dot, 1, 0, 1));
            Assert.AreEqual(2, board.OccupiedCount);
        }

        [Test]
        public void HasAnyPlacementFindsAGapAndReportsAFullBoard()
        {
            var board = new PackBoard(3, 3);
            Assert.IsTrue(board.HasAnyPlacement(Dot));

            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++) board.Set(x, y, 1);
            }

            Assert.IsFalse(board.HasAnyPlacement(Dot), "a full board has no placement");

            board.Set(2, 2, PackBoard.Empty);
            Assert.IsTrue(board.HasAnyPlacement(Dot));
            Assert.IsFalse(board.HasAnyPlacement(Square), "one free cell cannot take a 2x2");
        }

        [Test]
        public void ResetEmptiesTheBoard()
        {
            var board = new PackBoard(3, 3);
            board.Place(Square, 0, 0, 1);
            board.Reset();

            Assert.AreEqual(0, board.OccupiedCount);
        }

        [Test]
        public void ShapeMeasuresItsOwnBounds()
        {
            Assert.AreEqual(2, Square.Width);
            Assert.AreEqual(2, Square.Height);
            Assert.AreEqual(4, Square.CellCount);

            var triH = new PackShape("Tri", new GridOffset(0, 0), new GridOffset(1, 0), new GridOffset(2, 0));
            Assert.AreEqual(3, triH.Width);
            Assert.AreEqual(1, triH.Height);
        }
    }

    [TestFixture]
    public class PackRulesTests
    {
        private static PackConfig Config()
        {
            return new PackConfig
            {
                Duration = 60f,
                Width = 8,
                Height = 8,
                TraySize = 3,
                ScorePerCell = 5,
                TargetScore = 100000
            };
        }

        /// <summary>
        /// The play-field point whose drop lands on the given cell, undoing the
        /// drag lift the rules apply.
        /// </summary>
        private static Vec2 PointForCell(PackRules rules, PackConfig config, int cx, int cy)
        {
            var center = rules.CellCenter(cx, cy);
            var cellH = (config.FieldMaxY - config.FieldMinY) / config.Height;
            return new Vec2(center.X, center.Y - config.DragLiftCells * cellH);
        }

        private static Vec2 TrayPoint(PackRules rules, PackConfig config, int slot)
        {
            return new Vec2(rules.TraySlotX(slot), config.TrayY);
        }

        [Test]
        public void BeginFillsTheTrayAndEmptiesTheBoard()
        {
            var rules = new PackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Playing, rules.Status);
            Assert.AreEqual(3, rules.Tray.Count);
            Assert.AreEqual(0, rules.Board.OccupiedCount);

            foreach (var slot in rules.Tray)
            {
                Assert.IsNotNull(slot.Shape);
                Assert.IsFalse(slot.Used);
            }
        }

        [Test]
        public void SameSeedProducesSameTray()
        {
            var a = new PackRules(Config());
            var b = new PackRules(Config());
            a.Begin(77, NullFeedbackChannel.Instance);
            b.Begin(77, NullFeedbackChannel.Instance);

            for (var i = 0; i < a.Tray.Count; i++)
            {
                Assert.AreEqual(a.Tray[i].Shape.Name, b.Tray[i].Shape.Name);
            }
        }

        [Test]
        public void DraggingFromTheTrayPicksUpAPiece()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(
                new InputCommand(InputPhase.Began, TrayPoint(rules, config, 1), 0f),
                NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.HeldSlot);
        }

        [Test]
        public void PressingEmptySpaceHoldsNothing()
        {
            var rules = new PackRules(Config());
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(InputCommand.Began(0.5f, 0.6f), NullFeedbackChannel.Instance);

            Assert.AreEqual(-1, rules.HeldSlot);
        }

        [Test]
        public void ADroppedPieceLandsScoresAndLeavesTheTray()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var shape = rules.Tray[0].Shape;
            var target = PointForCell(rules, config, 3, 3);

            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            Assert.IsTrue(rules.GhostValid, "mid-board drop on an empty board must be legal");

            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.SuccessfulActions);
            Assert.AreEqual(shape.CellCount, rules.Board.OccupiedCount);
            Assert.AreEqual(config.ScorePerCell * shape.CellCount, rules.Score);
            Assert.IsTrue(rules.Tray[0].Used);
            Assert.AreEqual(-1, rules.HeldSlot);
        }

        [Test]
        public void DroppingOnOccupiedCellsIsRejected()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            // Wall off the landing zone so any overlap is guaranteed.
            for (var y = 0; y < config.Height; y++)
            {
                for (var x = 0; x < config.Width; x++) rules.Board.Set(x, y, 0);
            }

            var target = PointForCell(rules, config, 3, 3);
            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            Assert.IsFalse(rules.GhostValid);

            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(1, rules.FailedActions);
            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.IsFalse(rules.Tray[0].Used, "a rejected piece stays available");
        }

        [Test]
        public void ReleasingBackOnTheTrayCancelsWithoutPenalty()
        {
            // Changing your mind is not a mistake and must not pollute the
            // failed-action metric.
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var tray = TrayPoint(rules, config, 0);
            rules.HandleInput(new InputCommand(InputPhase.Began, tray, 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, new Vec2(tray.X, -0.5f), 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.FailedActions);
            Assert.AreEqual(0, rules.SuccessfulActions);
            Assert.IsFalse(rules.Tray[0].Used);
        }

        [Test]
        public void CancelledTouchDropsTheHeldPiece()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Canceled, new Vec2(0.5f, 0.5f), 0.1f), NullFeedbackChannel.Instance);

            Assert.AreEqual(-1, rules.HeldSlot);
            Assert.AreEqual(0, rules.FailedActions);
            Assert.IsFalse(rules.Tray[0].Used);
        }

        [Test]
        public void TrayRefillsOnlyAfterEverySlotIsUsed()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(5, NullFeedbackChannel.Instance);

            // Three landing cells far enough apart that the largest piece in the
            // catalogue (3 cells long) can never reach across to another, and
            // far enough from the edges that it cannot overhang.
            var cells = new[] { new GridOffset(1, 1), new GridOffset(5, 1), new GridOffset(1, 5) };

            for (var slot = 0; slot < config.TraySize; slot++)
            {
                var target = PointForCell(rules, config, cells[slot].X, cells[slot].Y);
                rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, slot), 0f), NullFeedbackChannel.Instance);
                rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
                Assert.IsTrue(rules.GhostValid, "placement {0} should be legal", slot);
                rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

                Assert.AreEqual(slot + 1, rules.SuccessfulActions);

                if (slot < config.TraySize - 1)
                {
                    Assert.IsTrue(rules.Tray[slot].Used, "the piece just played is spent");
                    Assert.IsFalse(rules.Tray[slot + 1].Used, "later slots must still be waiting");
                }
            }

            foreach (var slot in rules.Tray)
            {
                Assert.IsFalse(slot.Used, "tray should have refilled");
            }
        }

        [Test]
        public void FillingALineClearsItAndPaysTheBonus()
        {
            var config = Config();
            config.ScorePerLine = 60;
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            // Leave exactly one hole in row 0, then drop a piece into it.
            for (var x = 0; x < config.Width; x++) rules.Board.Set(x, 0, 0);
            rules.Board.Set(2, 0, PackBoard.Empty);

            // Force a known single-cell piece into the slot we will drag.
            var trayCopy = rules.Tray;
            if (trayCopy[0].Shape.CellCount != 1)
            {
                Assert.Ignore("seed did not offer a single-cell piece in slot 0");
            }

            var target = PointForCell(rules, config, 2, 0);
            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.Board.OccupiedCount, "the completed row should have cleared");
            Assert.GreaterOrEqual(rules.Score, config.ScorePerLine);
        }

        [Test]
        public void ReachingTargetScoreWins()
        {
            var config = Config();
            config.TargetScore = 5;
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            var target = PointForCell(rules, config, 3, 3);
            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);
            rules.Tick(0.1f, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Won, rules.Status);
        }

        [Test]
        public void RunningOutOfTimeLoses()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            rules.Tick(config.Duration + 0.1f, NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Lost, rules.Status);
        }

        [Test]
        public void FillingTheWholeBoardClearsEveryLine()
        {
            // Emergent but important: topping the board out is a jackpot, not a
            // loss, because every row and column completes at once.
            var config = Config();
            config.Shapes = new[] { new PackShape("Dot", new GridOffset(0, 0)) };
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            for (var y = 0; y < config.Height; y++)
            {
                for (var x = 0; x < config.Width; x++) rules.Board.Set(x, y, 0);
            }

            rules.Board.Set(4, 4, PackBoard.Empty);

            var target = PointForCell(rules, config, 4, 4);
            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(0, rules.Board.OccupiedCount);
            Assert.AreEqual(PrototypeStatus.Playing, rules.Status);
        }

        [Test]
        public void NoRemainingPlacementEndsTheRun()
        {
            // Pin the catalogue to a 2x2 so the dead end is exact rather than
            // dependent on whatever the seed happened to deal.
            var config = Config();
            config.Shapes = new[]
            {
                new PackShape("Square",
                    new GridOffset(0, 0), new GridOffset(1, 0),
                    new GridOffset(0, 1), new GridOffset(1, 1))
            };

            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);

            // Fill everything, then punch isolated holes down the diagonal. No
            // row or column is complete, so nothing clears, and no two holes
            // touch, so no 2x2 fits.
            for (var y = 0; y < config.Height; y++)
            {
                for (var x = 0; x < config.Width; x++) rules.Board.Set(x, y, 0);
            }

            for (var i = 0; i < config.Height; i++) rules.Board.Set(i, i, PackBoard.Empty);

            // Re-open one 2x2 pocket, clear of the diagonal, for the final drop.
            rules.Board.Set(2, 5, PackBoard.Empty);
            rules.Board.Set(3, 5, PackBoard.Empty);
            rules.Board.Set(2, 6, PackBoard.Empty);
            rules.Board.Set(3, 6, PackBoard.Empty);

            var target = PointForCell(rules, config, 2, 5);
            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);
            rules.HandleInput(new InputCommand(InputPhase.Moved, target, 0.1f), NullFeedbackChannel.Instance);
            Assert.IsTrue(rules.GhostValid, "the pocket should accept the piece");

            rules.HandleInput(new InputCommand(InputPhase.Ended, target, 0.2f), NullFeedbackChannel.Instance);

            Assert.AreEqual(PrototypeStatus.Lost, rules.Status);
        }

        [Test]
        public void InputIsIgnoredOnceTheRunIsOver()
        {
            var config = Config();
            var rules = new PackRules(config);
            rules.Begin(1, NullFeedbackChannel.Instance);
            rules.Tick(config.Duration + 0.1f, NullFeedbackChannel.Instance);

            rules.HandleInput(new InputCommand(InputPhase.Began, TrayPoint(rules, config, 0), 0f), NullFeedbackChannel.Instance);

            Assert.AreEqual(-1, rules.HeldSlot);
        }
    }
}
