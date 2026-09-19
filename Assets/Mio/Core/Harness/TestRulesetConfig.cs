using System;

namespace Mio.Core.Harness
{
    /// <summary>
    /// Tuning for the architecture validation rule set.
    ///
    /// Positions are in play-field space (0..1 on both axes), matching every
    /// other rule set, so this harness exercises the same coordinate contract
    /// the real prototypes will use.
    /// </summary>
    [Serializable]
    public sealed class TestRulesetConfig
    {
        /// <summary>Successful hits needed to win.</summary>
        public int RequiredSuccesses = 5;

        public int ScorePerSuccess = 100;

        /// <summary>Seconds before the session is lost. Zero means no time limit.</summary>
        public float Duration = 30f;

        /// <summary>Half-width of the square target, in field-width units.</summary>
        public float TargetHalfSize = 0.12f;

        /// <summary>
        /// Keeps the target away from the screen edges, where it would be hard
        /// to hit and would not test the coordinate mapping fairly.
        /// </summary>
        public float EdgeMargin = 0.18f;

        public TestRulesetConfig Clone() => (TestRulesetConfig)MemberwiseClone();
    }
}
