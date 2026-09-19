using System.Text;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using Mio.Core.Session;
using Mio.Unity.Config;
using Mio.Unity.View;
using UnityEngine;
using UnityEngine.UI;

namespace Mio.Unity.App
{
    /// <summary>
    /// The only chrome in the game: a fill meter, a timer and a score.
    ///
    /// Design principle 4 says avoid unnecessary menus, and the M0 bar is that
    /// a stranger can play without being told anything. So there is no start
    /// screen, no settings and no pause; the run is already going when the
    /// scene opens, and the end panel is one line plus "tap to play again".
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private PrototypePalette _palette;

        private Image _meterFill;
        private Image _timerFill;
        private Text _score;
        private Text _extra;

        private RectTransform _endPanel;
        private Image _endDim;
        private Text _endTitle;
        private Text _endDetail;
        private Text _endHint;

        private float _meterDisplay;

        public void Build(RectTransform root, PrototypePalette palette)
        {
            _palette = palette;

            var meterTrack = UiFactory.Rect(root, "MeterTrack", palette.MeterTrack);
            UiFactory.Place((RectTransform)meterTrack.transform, 0.5f, 0.955f, 0.88f, 0.018f);

            _meterFill = UiFactory.Rect(root, "MeterFill", palette.MeterFill);

            var timerTrack = UiFactory.Rect(root, "TimerTrack", Dim(palette.MeterTrack, 0.6f));
            UiFactory.Place((RectTransform)timerTrack.transform, 0.5f, 0.925f, 0.88f, 0.007f);

            _timerFill = UiFactory.Rect(root, "TimerFill", palette.TimerFill);

            _score = UiFactory.Label(root, "Score", "0", 54, palette.Neutral, TextAnchor.MiddleLeft);
            if (_score != null) UiFactory.Place((RectTransform)_score.transform, 0.30f, 0.885f, 0.44f, 0.04f);

            _extra = UiFactory.Label(root, "Extra", string.Empty, 46, palette.Good, TextAnchor.MiddleRight);
            if (_extra != null) UiFactory.Place((RectTransform)_extra.transform, 0.70f, 0.885f, 0.44f, 0.04f);

            BuildEndPanel(root, palette);
        }

        private void BuildEndPanel(RectTransform root, PrototypePalette palette)
        {
            _endPanel = UiFactory.Node(root, "EndPanel");
            UiFactory.Fill(_endPanel);

            _endDim = UiFactory.Rect(_endPanel, "Dim", new Color(0f, 0f, 0f, 0.62f));
            UiFactory.Fill((RectTransform)_endDim.transform);

            _endTitle = UiFactory.Label(_endPanel, "Title", string.Empty, 130, palette.Neutral);
            if (_endTitle != null) UiFactory.Place((RectTransform)_endTitle.transform, 0.5f, 0.60f, 0.9f, 0.10f);

            _endDetail = UiFactory.Label(_endPanel, "Detail", string.Empty, 52, palette.Neutral);
            if (_endDetail != null) UiFactory.Place((RectTransform)_endDetail.transform, 0.5f, 0.48f, 0.9f, 0.10f);

            _endHint = UiFactory.Label(_endPanel, "Hint", "tap to play again", 46, Dim(palette.Neutral, 0.7f));
            if (_endHint != null) UiFactory.Place((RectTransform)_endHint.transform, 0.5f, 0.36f, 0.9f, 0.06f);

            _endPanel.gameObject.SetActive(false);
        }

        public void OnRunBegan()
        {
            _meterDisplay = 0f;
            _endPanel.gameObject.SetActive(false);
            SetExtra(string.Empty);
        }

        public void Refresh(IPrototypeRules rules, float duration)
        {
            if (rules == null) return;

            // The meter eases rather than snapping, so a big chain reads as a
            // surge instead of a jump cut.
            _meterDisplay = Mathf.MoveTowards(_meterDisplay, rules.Progress01,
                Mathf.Max(0.35f, Mathf.Abs(rules.Progress01 - _meterDisplay) * 6f) * Time.deltaTime);

            Bar(_meterFill, 0.955f, 0.018f, _meterDisplay);

            var timeLeft = duration <= 0f ? 0f : Mathf.Clamp01(rules.TimeRemaining / duration);
            Bar(_timerFill, 0.925f, 0.007f, timeLeft);

            // The timer turns red in the last few seconds: urgency without text.
            _timerFill.color = timeLeft < 0.18f ? _palette.Bad : _palette.TimerFill;

            UiFactory.SetText(_score, rules.Score.ToString());
        }

        private void Bar(Image fill, float centerY, float height, float amount)
        {
            const float full = 0.88f;
            var width = full * Mathf.Clamp01(amount);

            // Grow from the left edge rather than from the centre.
            UiFactory.Place((RectTransform)fill.transform,
                0.5f - full * 0.5f + width * 0.5f, centerY, width, height);

            UiFactory.SetActive(fill, width > 0.0005f);
        }

        public void SetExtra(string value) => UiFactory.SetText(_extra, value);

        public void ShowResult(MetricReport report, ResourceBundle reward)
        {
            _endPanel.gameObject.SetActive(true);
            _endPanel.SetAsLastSibling();

            var won = report.Status == PrototypeStatus.Won;

            UiFactory.SetText(_endTitle, won ? "FULL!" : "TIME");
            if (_endTitle != null) _endTitle.color = won ? _palette.Good : _palette.Neutral;

            UiFactory.SetText(_endDetail, Describe(report, reward));
        }

        private static string Describe(MetricReport report, ResourceBundle reward)
        {
            var sb = new StringBuilder();
            sb.Append(report.Score).Append(" points");

            if (!reward.IsEmpty)
            {
                sb.Append("\n+").Append(reward.Energy).Append(" energy");
                sb.Append("   +").Append(reward.Material).Append(" material");
                sb.Append("   +").Append(reward.Coin).Append(" coin");
            }

            return sb.ToString();
        }

        private static Color Dim(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }
    }
}
