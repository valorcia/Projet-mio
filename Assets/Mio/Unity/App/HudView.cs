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
    /// Minimal chrome: a progress meter, a timer and a score, plus a result
    /// panel that takes a tap to replay.
    ///
    /// There is no start screen, no settings and no pause. The session is
    /// already running when the scene opens.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private PrototypePalette _palette;

        private Image _meterFill;
        private Image _timerFill;
        private Text _score;
        private Text _wallet;

        private RectTransform _endPanel;
        private Text _endTitle;
        private Text _endDetail;

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

            _wallet = UiFactory.Label(root, "Wallet", string.Empty, 38,
                Dim(palette.Neutral, 0.75f), TextAnchor.MiddleRight);
            if (_wallet != null) UiFactory.Place((RectTransform)_wallet.transform, 0.70f, 0.885f, 0.44f, 0.04f);

            BuildEndPanel(root, palette);
        }

        private void BuildEndPanel(RectTransform root, PrototypePalette palette)
        {
            _endPanel = UiFactory.Node(root, "EndPanel");
            UiFactory.Fill(_endPanel);

            var dim = UiFactory.Rect(_endPanel, "Dim", new Color(0f, 0f, 0f, 0.62f));
            UiFactory.Fill((RectTransform)dim.transform);

            _endTitle = UiFactory.Label(_endPanel, "Title", string.Empty, 120, palette.Neutral);
            if (_endTitle != null) UiFactory.Place((RectTransform)_endTitle.transform, 0.5f, 0.60f, 0.9f, 0.10f);

            _endDetail = UiFactory.Label(_endPanel, "Detail", string.Empty, 46, palette.Neutral);
            if (_endDetail != null) UiFactory.Place((RectTransform)_endDetail.transform, 0.5f, 0.45f, 0.9f, 0.16f);

            var hint = UiFactory.Label(_endPanel, "Hint", "tap to play again", 42, Dim(palette.Neutral, 0.7f));
            if (hint != null) UiFactory.Place((RectTransform)hint.transform, 0.5f, 0.32f, 0.9f, 0.06f);

            _endPanel.gameObject.SetActive(false);
        }

        public void OnSessionBegan()
        {
            _meterDisplay = 0f;
            _endPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// <paramref name="duration"/> may be infinite for an untimed rule set,
        /// in which case the timer bar is simply hidden.
        /// </summary>
        public void Refresh(IPrototypeRules rules, float timeRemaining, float duration)
        {
            if (rules == null) return;

            // The meter eases rather than snapping, so a jump in progress reads
            // as a surge instead of a cut.
            _meterDisplay = Mathf.MoveTowards(_meterDisplay, rules.Progress01,
                Mathf.Max(0.35f, Mathf.Abs(rules.Progress01 - _meterDisplay) * 6f) * Time.deltaTime);

            Bar(_meterFill, 0.955f, 0.018f, _meterDisplay);

            var timed = duration > 0f && !float.IsInfinity(duration);
            if (timed)
            {
                var left = Mathf.Clamp01(timeRemaining / duration);
                Bar(_timerFill, 0.925f, 0.007f, left);

                // Turns red in the last moments: urgency without text.
                _timerFill.color = left < 0.18f ? _palette.Bad : _palette.TimerFill;
            }
            else
            {
                UiFactory.SetActive(_timerFill, false);
            }

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

        public void SetWallet(ResourceBundle balance)
        {
            UiFactory.SetText(_wallet, $"{balance.Energy} / {balance.Material} / {balance.Coin}");
        }

        public void ShowResult(MetricReport report, ResourceBundle reward)
        {
            _endPanel.gameObject.SetActive(true);
            _endPanel.SetAsLastSibling();

            var won = report.CompletionStatus == SessionStatus.Won;

            UiFactory.SetText(_endTitle, won ? "DONE!" : "TIME");
            if (_endTitle != null) _endTitle.color = won ? _palette.Good : _palette.Neutral;

            UiFactory.SetText(_endDetail, Describe(report, reward));
        }

        private static string Describe(MetricReport report, ResourceBundle reward)
        {
            var sb = new StringBuilder();
            sb.Append(report.Score).Append(" points");
            sb.Append("\n").Append(report.SuccessfulActions).Append(" hits, ")
              .Append(report.FailedActions).Append(" misses");

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
