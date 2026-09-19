using System;
using Mio.Core.Session;

namespace Mio.Core.Economy
{
    /// <summary>
    /// One score threshold and the bundle it pays. Sorted ascending by the
    /// table; the highest threshold the player reaches is the one that pays.
    /// </summary>
    [Serializable]
    public struct RewardTier
    {
        public int MinScore;
        public ResourceBundle Payout;

        public RewardTier(int minScore, ResourceBundle payout)
        {
            MinScore = minScore;
            Payout = payout;
        }
    }

    /// <summary>
    /// Turns an attempt result into resources. Pure data + pure function: no
    /// economy number is hard-coded in gameplay, and the whole payout curve can
    /// be re-tuned from a ScriptableObject without touching code.
    /// </summary>
    public sealed class RewardTable
    {
        private readonly RewardTier[] _tiers;

        public RewardTable(
            ResourceBundle participation,
            ResourceBundle completionBonus,
            RewardTier[] tiers)
        {
            Participation = participation;
            CompletionBonus = completionBonus;

            _tiers = tiers ?? new RewardTier[0];
            // Copy before sorting so we never mutate the caller's array (which,
            // coming from a ScriptableObject, is shared asset data).
            _tiers = (RewardTier[])_tiers.Clone();
            Array.Sort(_tiers, (a, b) => a.MinScore.CompareTo(b.MinScore));
        }

        /// <summary>Paid for finishing an attempt at all, win or lose.</summary>
        public ResourceBundle Participation { get; }

        /// <summary>Added only when the attempt is won.</summary>
        public ResourceBundle CompletionBonus { get; }

        /// <summary>A table that pays nothing. Keeps callers null-free.</summary>
        public static RewardTable Empty { get; } =
            new RewardTable(ResourceBundle.Empty, ResourceBundle.Empty, new RewardTier[0]);

        public ResourceBundle Evaluate(SessionStatus status, int score)
        {
            // An abandoned session pays nothing, otherwise quitting early
            // would be a viable farming strategy.
            if (!status.IsPlayedToEnd()) return ResourceBundle.Empty;

            var total = Participation;

            for (var i = _tiers.Length - 1; i >= 0; i--)
            {
                if (score >= _tiers[i].MinScore)
                {
                    total += _tiers[i].Payout;
                    break;
                }
            }

            if (status == SessionStatus.Won)
            {
                total += CompletionBonus;
            }

            return total;
        }
    }
}
