using Mio.Core.Harness;
using Mio.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.View
{
    /// <summary>
    /// Draws the architecture validation rule set: one target square to tap.
    ///
    /// Intentionally the least interesting view that can exist. Its job is to
    /// make the foundation observable by hand — you can see input mapping,
    /// cues, particles, punch, shake and the meter all working — not to be fun.
    /// </summary>
    public sealed class HarnessView : PrototypeView
    {
        private TestRuleset _rules;
        private Image _target;
        private Image _halo;

        public void Bind(TestRuleset rules) => _rules = rules;

        protected override void Build()
        {
            // A soft halo behind the target so the tappable area reads as
            // larger than the strict hit box, which is what makes a touch
            // target feel fair rather than precise.
            _halo = UiFactory.Rect(Root, "TargetHalo", Dim(Palette.Good, 0.18f));
            _target = UiFactory.Rect(Root, "Target", Palette.Good);
        }

        public override void Refresh()
        {
            if (_rules == null || _target == null) return;

            var size = _rules.TargetHalfSize * 2f;
            var center = _rules.Target;

            // A slow breath keeps the target alive and draws the eye to it
            // without any text telling the player what to do.
            var breath = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 3f);

            UiFactory.PlaceSquare((RectTransform)_target.transform, center.X, center.Y, size * breath);
            UiFactory.PlaceSquare((RectTransform)_halo.transform, center.X, center.Y, size * 1.7f);
        }

        public override void OnCue(in FeedbackCue cue)
        {
            if (_target == null) return;

            if (cue.Kind == FeedbackCueKind.Success || cue.Kind == FeedbackCueKind.Win)
            {
                Punch.Play(_target.transform, 0.5f);
            }
        }
    }
}
