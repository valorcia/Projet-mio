using System.Collections.Generic;
using Mio.Core.Pack;
using Mio.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.View
{
    /// <summary>
    /// Draws PACK: the container grid, the tray of pieces, and a live ghost of
    /// where the held piece would land.
    /// </summary>
    public sealed class PackView : PrototypeView
    {
        private const float CellInset = 0.88f;
        private const float TraySlotScale = 0.62f;

        private PackRules _rules;
        private Image[] _cells;
        private Image[] _slots;
        private readonly List<Image> _ghost = new List<Image>();
        private readonly List<Image> _trayCells = new List<Image>();

        private Image _backdrop;
        private int _width;
        private int _height;
        private float _cellWidth;
        private float _cellHeight;

        public void Bind(PackRules rules) => _rules = rules;

        protected override void Build()
        {
            _backdrop = UiFactory.Rect(Root, "Backdrop", Palette.FieldTint);
        }

        public override void OnRunBegan()
        {
            var board = _rules.Board;

            _cellWidth = board.Width > 1
                ? _rules.CellCenter(1, 0).X - _rules.CellCenter(0, 0).X
                : 0.1f;

            _cellHeight = board.Height > 1
                ? _rules.CellCenter(0, 1).Y - _rules.CellCenter(0, 0).Y
                : 0.1f;

            if (_cells == null || _width != board.Width || _height != board.Height)
            {
                RebuildGrid(board.Width, board.Height);
            }

            PlaceBackdrop();
            Refresh();
        }

        private void RebuildGrid(int width, int height)
        {
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i] != null) Destroy(_cells[i].gameObject);
                }
            }

            _width = width;
            _height = height;
            _cells = new Image[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Every slot gets a permanent faint tile, so the container
                    // reads as a container even when it is empty.
                    var slot = UiFactory.Rect(Root, $"Slot_{x}_{y}", Palette.GridLine);
                    PlaceCell((RectTransform)slot.transform, x, y);

                    var image = UiFactory.Rect(Root, $"Cell_{x}_{y}", Color.white);
                    PlaceCell((RectTransform)image.transform, x, y);
                    _cells[y * width + x] = image;
                }
            }
        }

        private void PlaceCell(RectTransform rect, int x, int y)
        {
            var center = _rules.CellCenter(x, y);
            UiFactory.Place(rect, center.X, center.Y, _cellWidth * CellInset, _cellHeight * CellInset);
        }

        private void PlaceBackdrop()
        {
            var min = _rules.CellCenter(0, 0);
            var max = _rules.CellCenter(_width - 1, _height - 1);

            UiFactory.Place((RectTransform)_backdrop.transform,
                (min.X + max.X) * 0.5f,
                (min.Y + max.Y) * 0.5f,
                max.X - min.X + _cellWidth,
                max.Y - min.Y + _cellHeight);
        }

        public override void Refresh()
        {
            if (_rules == null || _cells == null) return;

            RefreshBoard();
            RefreshTray();
            RefreshGhost();
        }

        private void RefreshBoard()
        {
            var board = _rules.Board;

            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var image = _cells[y * _width + x];
                    var value = board.Get(x, y);

                    if (value == PackBoard.Empty)
                    {
                        UiFactory.SetActive(image, false);
                        continue;
                    }

                    UiFactory.SetActive(image, true);
                    image.color = Palette.Piece(value);
                }
            }
        }

        private void RefreshTray()
        {
            var tray = _rules.Tray;
            EnsureSlots(tray.Count);

            var used = 0;

            for (var i = 0; i < tray.Count; i++)
            {
                var slot = tray[i];
                var centerX = _rules.TraySlotX(i);

                // The slot the finger is holding is lifted out of the tray, so
                // it is obvious which piece is in play.
                var held = _rules.HeldSlot == i;
                UiFactory.SetActive(_slots[i], !slot.Used);

                if (slot.Used || slot.Shape == null) continue;

                var shape = slot.Shape;
                var scale = TraySlotScale;

                for (var c = 0; c < shape.Cells.Length; c++)
                {
                    var image = TrayCell(used++);
                    UiFactory.SetActive(image, !held);
                    if (held) continue;

                    // Centre the shape on its own bounding box inside the slot.
                    var offsetX = (shape.Cells[c].X - (shape.Width - 1) * 0.5f) * _cellWidth * scale;
                    var offsetY = (shape.Cells[c].Y - (shape.Height - 1) * 0.5f) * _cellHeight * scale;

                    image.color = Palette.Piece(slot.ColorId);
                    UiFactory.Place((RectTransform)image.transform,
                        centerX + offsetX,
                        TrayCenterY() + offsetY,
                        _cellWidth * scale * CellInset,
                        _cellHeight * scale * CellInset);
                }
            }

            for (var i = used; i < _trayCells.Count; i++)
            {
                UiFactory.SetActive(_trayCells[i], false);
            }
        }

        /// <summary>
        /// The tray's vertical centre, taken from the rules so the drawn pieces
        /// sit exactly on their touch targets.
        /// </summary>
        private float TrayCenterY() => _rules.TrayY;

        private void EnsureSlots(int count)
        {
            if (_slots != null && _slots.Length >= count) return;

            var grown = new Image[count];
            if (_slots != null) System.Array.Copy(_slots, grown, _slots.Length);

            for (var i = _slots?.Length ?? 0; i < count; i++)
            {
                grown[i] = UiFactory.Rect(Root, "TraySlot" + i, Dim(Palette.GridLine, 0.7f));
            }

            _slots = grown;

            var trayY = TrayCenterY();
            for (var i = 0; i < _slots.Length; i++)
            {
                UiFactory.Place((RectTransform)_slots[i].transform,
                    _rules.TraySlotX(i), trayY, 1f / count * 0.88f, _cellHeight * 2.6f);
            }
        }

        private Image TrayCell(int index)
        {
            while (_trayCells.Count <= index)
            {
                _trayCells.Add(UiFactory.Rect(Root, "TrayCell" + _trayCells.Count, Color.white));
            }

            return _trayCells[index];
        }

        private void RefreshGhost()
        {
            var held = _rules.HeldSlot;

            if (held < 0 || !_rules.HasGhost)
            {
                for (var i = 0; i < _ghost.Count; i++) UiFactory.SetActive(_ghost[i], false);
                return;
            }

            var shape = _rules.Tray[held].Shape;
            var valid = _rules.GhostValid;

            // Green means it will land, red means it will not. That single
            // colour swap is the whole rules explanation.
            var color = valid
                ? Dim(Palette.Piece(_rules.Tray[held].ColorId), 0.95f)
                : Dim(Palette.Bad, 0.55f);

            for (var i = 0; i < shape.Cells.Length; i++)
            {
                while (_ghost.Count <= i)
                {
                    _ghost.Add(UiFactory.Rect(Root, "Ghost" + _ghost.Count, Color.white));
                }

                var image = _ghost[i];
                UiFactory.SetActive(image, true);
                image.color = color;
                image.transform.SetAsLastSibling();

                PlaceCell((RectTransform)image.transform,
                    _rules.GhostX + shape.Cells[i].X,
                    _rules.GhostY + shape.Cells[i].Y);
            }

            for (var i = shape.Cells.Length; i < _ghost.Count; i++)
            {
                UiFactory.SetActive(_ghost[i], false);
            }
        }

        public override void OnCue(in FeedbackCue cue)
        {
            if (cue.Kind != FeedbackCueKind.Clear && cue.Kind != FeedbackCueKind.Place) return;
            if (_backdrop == null) return;

            // Clearing a line is a whole-board event, so the container itself
            // reacts rather than any single cell.
            Punch.Play(_backdrop.transform, cue.Kind == FeedbackCueKind.Clear ? 0.5f : 0.25f);
        }
    }
}
