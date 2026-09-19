using System.Collections.Generic;
using Mio.Core.Common;
using Mio.Core.Session;

namespace Mio.Core.Pack
{
    /// <summary>One tray slot: a shape, a colour, and whether it is still available.</summary>
    public struct PackTraySlot
    {
        public PackShape Shape;
        public int ColorId;
        public bool Used;
    }

    /// <summary>
    /// PACK: drag a block from the tray onto the board. Fill a whole row or
    /// column and it clears.
    ///
    /// One finger, one verb: drag. The held piece floats above the thumb and
    /// the landing spot is previewed, so the rule ("it must fit") is learned by
    /// seeing a ghost turn red rather than by reading anything.
    /// </summary>
    public sealed class PackRules : IPrototypeRules
    {
        private readonly PackConfig _config;
        private readonly PackShape[] _catalog;

        private PackBoard _board;
        private DeterministicRng _rng;
        private PackTraySlot[] _tray;

        private float _elapsed;
        private int _placements;
        private int _rejected;
        private int _linesCleared;
        private int _bestCombo;

        private int _heldSlot = -1;
        private bool _hasGhost;
        private int _ghostX;
        private int _ghostY;
        private bool _ghostValid;

        public PackRules(PackConfig config)
        {
            _config = config ?? new PackConfig();
            _catalog = _config.ResolveShapes();
        }

        public PrototypeId Id => PrototypeId.Pack;
        public PrototypeStatus Status { get; private set; } = PrototypeStatus.Idle;
        public int Score { get; private set; }
        public int SuccessfulActions => _placements;
        public int FailedActions => _rejected;

        public float Progress01 =>
            _config.TargetScore <= 0 ? 1f : MathK.Clamp01(Score / (float)_config.TargetScore);

        public float TimeRemaining => MathK.Clamp(_config.Duration - _elapsed, 0f, _config.Duration);

        public PackBoard Board => _board;
        public IReadOnlyList<PackTraySlot> Tray => _tray;

        /// <summary>Tray slot currently under the finger, or -1.</summary>
        public int HeldSlot => _heldSlot;

        public bool HasGhost => _hasGhost;
        public int GhostX => _ghostX;
        public int GhostY => _ghostY;

        /// <summary>Whether the previewed drop would be legal. Drives ghost colour.</summary>
        public bool GhostValid => _ghostValid;

        public void Begin(int seed, IFeedbackChannel feedback)
        {
            _rng = new DeterministicRng(seed);
            _board = new PackBoard(_config.Width, _config.Height);
            _tray = new PackTraySlot[_config.TraySize < 1 ? 1 : _config.TraySize];

            _elapsed = 0f;
            _placements = 0;
            _rejected = 0;
            _linesCleared = 0;
            _bestCombo = 0;
            Score = 0;
            ClearDrag();
            Status = PrototypeStatus.Playing;

            RefillTray();
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Begin, new Vec2(0.5f, _config.TrayY)));
        }

        private void RefillTray()
        {
            for (var i = 0; i < _tray.Length; i++)
            {
                _tray[i] = new PackTraySlot
                {
                    Shape = _catalog[_rng.Range(0, _catalog.Length)],
                    ColorId = _rng.Range(0, 4),
                    Used = false
                };
            }
        }

        public void Tick(float deltaTime, IFeedbackChannel feedback)
        {
            if (Status != PrototypeStatus.Playing || deltaTime <= 0f) return;

            _elapsed += deltaTime;

            if (Score >= _config.TargetScore)
            {
                Status = PrototypeStatus.Won;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Win, new Vec2(0.5f, 0.5f)));
                return;
            }

            if (_elapsed >= _config.Duration)
            {
                Status = PrototypeStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, new Vec2(0.5f, 0.5f)));
            }
        }

        public void HandleInput(in InputCommand command, IFeedbackChannel feedback)
        {
            if (Status != PrototypeStatus.Playing) return;

            switch (command.Phase)
            {
                case InputPhase.Began:
                    BeginDrag(command.Position, feedback);
                    break;

                case InputPhase.Moved:
                    UpdateGhost(command.Position);
                    break;

                case InputPhase.Ended:
                    UpdateGhost(command.Position);
                    Drop(feedback);
                    break;

                case InputPhase.Canceled:
                    ClearDrag();
                    break;
            }
        }

        private void BeginDrag(Vec2 position, IFeedbackChannel feedback)
        {
            ClearDrag();

            var slot = TraySlotAt(position);
            if (slot < 0 || _tray[slot].Used)
            {
                return;
            }

            _heldSlot = slot;
            UpdateGhost(position);
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Success, position, 0.25f));
        }

        private void UpdateGhost(Vec2 position)
        {
            if (_heldSlot < 0) return;

            var shape = _tray[_heldSlot].Shape;
            _hasGhost = TryGetAnchor(position, shape, out _ghostX, out _ghostY);
            _ghostValid = _hasGhost && _board.CanPlace(shape, _ghostX, _ghostY);
        }

        private void Drop(IFeedbackChannel feedback)
        {
            if (_heldSlot < 0) return;

            var slot = _tray[_heldSlot];
            var shape = slot.Shape;

            if (!_ghostValid)
            {
                // Releasing over the tray is a cancel, not a mistake: only a real
                // attempt to place on the board counts as a failed action.
                if (_hasGhost)
                {
                    _rejected++;
                    feedback.Emit(new FeedbackCue(FeedbackCueKind.Fail, CellCenter(_ghostX, _ghostY), 0.35f));
                }

                ClearDrag();
                return;
            }

            var lines = _board.Place(shape, _ghostX, _ghostY, slot.ColorId);

            _tray[_heldSlot].Used = true;
            _placements++;
            Score += _config.ScorePerCell * shape.CellCount;

            var at = CellCenter(_ghostX, _ghostY);
            feedback.Emit(new FeedbackCue(FeedbackCueKind.Place, at, 0.5f, shape.CellCount));

            if (lines > 0)
            {
                _linesCleared += lines;
                if (lines > _bestCombo) _bestCombo = lines;

                Score += _config.ScorePerLine * lines
                       + _config.ComboBonusPerExtraLine * (lines - 1);

                feedback.Emit(new FeedbackCue(FeedbackCueKind.Clear, at, MathK.Clamp01(lines / 3f), lines));
            }

            feedback.Emit(new FeedbackCue(FeedbackCueKind.Progress, at, Progress01));

            ClearDrag();

            if (AllSlotsUsed()) RefillTray();

            // Checked after the refill so the player is only ever stranded on a
            // tray they can actually see.
            if (!AnySlotPlaceable())
            {
                Status = PrototypeStatus.Lost;
                feedback.Emit(new FeedbackCue(FeedbackCueKind.Lose, at));
            }
        }

        private bool AllSlotsUsed()
        {
            for (var i = 0; i < _tray.Length; i++)
            {
                if (!_tray[i].Used) return false;
            }

            return true;
        }

        private bool AnySlotPlaceable()
        {
            for (var i = 0; i < _tray.Length; i++)
            {
                if (_tray[i].Used) continue;
                if (_board.HasAnyPlacement(_tray[i].Shape)) return true;
            }

            return false;
        }

        private void ClearDrag()
        {
            _heldSlot = -1;
            _hasGhost = false;
            _ghostValid = false;
            _ghostX = 0;
            _ghostY = 0;
        }

        /// <summary>Tray slot whose touch target contains the point, or -1.</summary>
        public int TraySlotAt(Vec2 position)
        {
            if (_tray == null) return -1;
            if (position.Y < _config.TrayY - _config.TrayTouchRadius) return -1;
            if (position.Y > _config.TrayY + _config.TrayTouchRadius) return -1;

            for (var i = 0; i < _tray.Length; i++)
            {
                var centerX = TraySlotX(i);
                var halfSpan = 0.5f / _tray.Length;
                if (position.X >= centerX - halfSpan && position.X <= centerX + halfSpan) return i;
            }

            return -1;
        }

        /// <summary>Horizontal centre of a tray slot, in play-field space.</summary>
        public float TraySlotX(int slot)
        {
            return (slot + 0.5f) / _tray.Length;
        }

        /// <summary>
        /// Converts a finger position into the shape's bottom-left anchor cell,
        /// lifting the piece above the thumb and centring it on the cursor.
        /// </summary>
        public bool TryGetAnchor(Vec2 position, PackShape shape, out int anchorX, out int anchorY)
        {
            anchorX = 0;
            anchorY = 0;
            if (shape == null) return false;

            var spanX = _config.FieldMaxX - _config.FieldMinX;
            var spanY = _config.FieldMaxY - _config.FieldMinY;
            if (spanX <= 0f || spanY <= 0f) return false;

            var cellH = spanY / _board.Height;
            var lifted = position.Y + _config.DragLiftCells * cellH;

            var u = (position.X - _config.FieldMinX) / spanX;
            var v = (lifted - _config.FieldMinY) / spanY;

            // A generous margin keeps the ghost alive just off the board edge so
            // edge placements do not require pixel-perfect aim.
            if (u < -0.35f || u > 1.35f || v < -0.35f || v > 1.35f) return false;

            var cx = (int)System.Math.Floor(u * _board.Width);
            var cy = (int)System.Math.Floor(v * _board.Height);

            anchorX = cx - (shape.Width - 1) / 2;
            anchorY = cy - (shape.Height - 1) / 2;
            return true;
        }

        /// <summary>Centre of a board cell in play-field space.</summary>
        public Vec2 CellCenter(int cx, int cy)
        {
            var spanX = _config.FieldMaxX - _config.FieldMinX;
            var spanY = _config.FieldMaxY - _config.FieldMinY;

            return new Vec2(
                _config.FieldMinX + (cx + 0.5f) / _board.Width * spanX,
                _config.FieldMinY + (cy + 0.5f) / _board.Height * spanY);
        }

        public void CollectCustomMetrics(IDictionary<string, double> into)
        {
            into["pack.placements"] = _placements;
            into["pack.rejected"] = _rejected;
            into["pack.linesCleared"] = _linesCleared;
            into["pack.bestCombo"] = _bestCombo;
            into["pack.occupancy"] = _board == null
                ? 0d
                : _board.OccupiedCount / (double)(_board.Width * _board.Height);
        }
    }
}
