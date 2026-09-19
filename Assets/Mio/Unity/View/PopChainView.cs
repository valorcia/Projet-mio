using Mio.Core.PopChain;
using Mio.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.View
{
    /// <summary>
    /// Draws POP CHAIN: a grid of coloured blobs that burst when tapped.
    /// </summary>
    public sealed class PopChainView : PrototypeView
    {
        /// <summary>Fraction of a slot a blob fills, leaving a visible gutter.</summary>
        private const float CellInset = 0.86f;

        private PopChainRules _rules;
        private Image[] _cells;
        private Image _backdrop;
        private int _width;
        private int _height;
        private float _cellWidth;
        private float _cellHeight;

        public void Bind(PopChainRules rules) => _rules = rules;

        protected override void Build()
        {
            _backdrop = UiFactory.Rect(Root, "Backdrop", Palette.FieldTint);
        }

        public override void OnRunBegan()
        {
            var board = _rules.Board;

            // Derive the cell pitch from the rules' own mapping, so what the
            // player sees and what a tap hits can never drift apart.
            _cellWidth = board.Width > 1
                ? _rules.CellCenter(1, 0).X - _rules.CellCenter(0, 0).X
                : 0.1f;

            _cellHeight = board.Height > 1
                ? _rules.CellCenter(0, 1).Y - _rules.CellCenter(0, 0).Y
                : 0.1f;

            if (_cells == null || _width != board.Width || _height != board.Height)
            {
                RebuildCells(board.Width, board.Height);
            }

            PlaceBackdrop();
            Refresh();
        }

        private void RebuildCells(int width, int height)
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
                    var image = UiFactory.Rect(Root, $"Cell_{x}_{y}", Color.white);
                    var center = _rules.CellCenter(x, y);

                    UiFactory.Place((RectTransform)image.transform,
                        center.X, center.Y,
                        _cellWidth * CellInset, _cellHeight * CellInset);

                    _cells[y * width + x] = image;
                }
            }
        }

        private void PlaceBackdrop()
        {
            var min = _rules.CellCenter(0, 0);
            var max = _rules.CellCenter(_width - 1, _height - 1);

            var centerX = (min.X + max.X) * 0.5f;
            var centerY = (min.Y + max.Y) * 0.5f;
            var spanX = max.X - min.X + _cellWidth;
            var spanY = max.Y - min.Y + _cellHeight;

            UiFactory.Place((RectTransform)_backdrop.transform, centerX, centerY, spanX, spanY);
        }

        public override void Refresh()
        {
            if (_rules == null || _cells == null) return;

            var board = _rules.Board;

            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var image = _cells[y * _width + x];
                    var value = board.Get(x, y);

                    if (value == PopChainBoard.Empty)
                    {
                        UiFactory.SetActive(image, false);
                        continue;
                    }

                    UiFactory.SetActive(image, true);
                    image.color = Palette.Piece(value);
                }
            }
        }

        public override void OnCue(in FeedbackCue cue)
        {
            if (cue.Kind != FeedbackCueKind.Pop && cue.Kind != FeedbackCueKind.Fail) return;

            // Cues can arrive during the run's first Begin, before the grid has
            // been built for this attempt.
            if (_cells == null) return;
            if (!_rules.TryGetCell(cue.Position, out var x, out var y)) return;

            var image = _cells[y * _width + x];
            if (image == null) return;

            // A rejected tap wobbles; a pop bangs. Both are instant, so the
            // screen always answers the finger.
            Punch.Play(image.transform, cue.Kind == FeedbackCueKind.Pop ? 0.45f : 0.18f);
        }
    }
}
