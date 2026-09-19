using Mio.Core.Flow;
using Mio.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.View
{
    /// <summary>
    /// Draws FLOW: a stream head that chases the finger, and a track of orbs
    /// and blocks scrolling down to meet it.
    /// </summary>
    public sealed class FlowView : PrototypeView
    {
        private const int TrailLength = 7;
        private const float HeadSize = 0.075f;
        private const float HazardHeight = 0.05f;

        private FlowRules _rules;
        private Image _head;
        private Image[] _trail;
        private Vector2[] _trailPositions;
        private Image[] _gates;
        private Image _headGuide;

        public void Bind(FlowRules rules) => _rules = rules;

        protected override void Build()
        {
            var lane = UiFactory.Rect(Root, "Lane", Palette.FieldTint);
            UiFactory.Fill((RectTransform)lane.transform);

            // A faint line at the resolve height, so the player can see where
            // the orbs are going to be judged before the first one arrives.
            _headGuide = UiFactory.Rect(Root, "HeadGuide", Dim(Palette.GridLine, 1.4f));

            _trail = new Image[TrailLength];
            _trailPositions = new Vector2[TrailLength];
            for (var i = 0; i < TrailLength; i++)
            {
                // Oldest segment is faintest and smallest: a comet tail.
                var t = 1f - i / (float)TrailLength;
                _trail[i] = UiFactory.Rect(Root, "Trail" + i, Dim(Palette.MeterFill, 0.30f * t));
            }

            _head = UiFactory.Rect(Root, "Head", Palette.MeterFill);
        }

        public override void OnRunBegan()
        {
            BuildGates();

            var start = new Vector2(_rules.HeadX, _rules.HeadY);
            for (var i = 0; i < _trailPositions.Length; i++) _trailPositions[i] = start;
        }

        private void BuildGates()
        {
            var needed = _rules.Gates.Count;

            if (_gates == null || _gates.Length < needed)
            {
                var grown = new Image[needed];
                if (_gates != null) System.Array.Copy(_gates, grown, _gates.Length);

                for (var i = _gates?.Length ?? 0; i < needed; i++)
                {
                    grown[i] = UiFactory.Rect(Root, "Gate" + i, Color.white);
                    // Gates sit behind the head so the stream always reads as
                    // passing through them, never hidden by them.
                    grown[i].transform.SetSiblingIndex(1);
                }

                _gates = grown;
            }

            for (var i = 0; i < _gates.Length; i++)
            {
                if (i >= needed) { UiFactory.SetActive(_gates[i], false); continue; }

                var gate = _rules.Gates[i];
                _gates[i].color = gate.Kind == FlowGateKind.Collect ? Palette.Good : Palette.Hazard;
                UiFactory.SetActive(_gates[i], true);
            }
        }

        public override void Refresh()
        {
            if (_rules == null || _gates == null) return;

            UiFactory.Place((RectTransform)_headGuide.transform, 0.5f, _rules.HeadY, 1f, 0.003f);

            RefreshGates();
            RefreshHead();
        }

        private void RefreshGates()
        {
            for (var i = 0; i < _rules.Gates.Count; i++)
            {
                var gate = _rules.Gates[i];
                var image = _gates[i];

                // A resolved gate has been consumed; its burst already played.
                if (gate.Resolved)
                {
                    UiFactory.SetActive(image, false);
                    continue;
                }

                var y = _rules.GateY(gate);
                if (y > 1.1f || y < -0.1f)
                {
                    UiFactory.SetActive(image, false);
                    continue;
                }

                UiFactory.SetActive(image, true);

                var width = gate.HalfWidth * 2f;
                var rect = (RectTransform)image.transform;

                if (gate.Kind == FlowGateKind.Collect)
                {
                    UiFactory.PlaceSquare(rect, gate.CenterX, y, width);
                }
                else
                {
                    UiFactory.Place(rect, gate.CenterX, y, width, HazardHeight);
                }

                // Fade in as it enters, so nothing ever pops into existence.
                var alpha = Mathf.Clamp01((1.05f - y) * 4f);
                var color = gate.Kind == FlowGateKind.Collect ? Palette.Good : Palette.Hazard;
                image.color = Dim(color, alpha);
            }
        }

        private void RefreshHead()
        {
            var position = new Vector2(_rules.HeadX, _rules.HeadY);

            // Each segment eases toward the one in front of it. Cheap, stable,
            // and it reads as liquid rather than as a rigid chain.
            var follow = 1f - Mathf.Exp(-18f * Time.deltaTime);
            _trailPositions[0] = position;
            for (var i = 1; i < _trailPositions.Length; i++)
            {
                _trailPositions[i] = Vector2.Lerp(_trailPositions[i], _trailPositions[i - 1], follow);
            }

            for (var i = 0; i < _trail.Length; i++)
            {
                var scale = Mathf.Lerp(HeadSize * 0.85f, HeadSize * 0.25f, i / (float)TrailLength);
                UiFactory.PlaceSquare((RectTransform)_trail[i].transform,
                    _trailPositions[i].x, _trailPositions[i].y, scale);
            }

            UiFactory.PlaceSquare((RectTransform)_head.transform, position.x, position.y, HeadSize);
        }

        public override void OnCue(in FeedbackCue cue)
        {
            if (cue.Kind == FeedbackCueKind.Collect || cue.Kind == FeedbackCueKind.Hazard)
            {
                Punch.Play(_head.transform, cue.Kind == FeedbackCueKind.Collect ? 0.35f : 0.5f);
            }
        }
    }
}
