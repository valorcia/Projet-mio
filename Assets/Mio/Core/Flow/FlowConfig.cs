using System;

namespace Mio.Core.Flow
{
    /// <summary>
    /// Every tunable number for FLOW. Held by a ScriptableObject in the Unity
    /// layer so a designer can retune the whole prototype without a recompile.
    ///
    /// All positions are in play-field space (0..1 on both axes) and all speeds
    /// are field-units per second, so tuning is resolution independent.
    /// </summary>
    [Serializable]
    public sealed class FlowConfig
    {
        /// <summary>Length of one run, seconds.</summary>
        public float Duration = 45f;

        /// <summary>Height at which the stream head sits and gates resolve.</summary>
        public float HeadY = 0.22f;

        /// <summary>How fast the head chases the finger. Low feels like syrup.</summary>
        public float FollowSpeed = 2.2f;

        /// <summary>Downward travel of the gates. Presentation + read-ahead time.</summary>
        public float ScrollSpeed = 0.55f;

        /// <summary>Grace period before the first gate resolves.</summary>
        public float FirstGateDelay = 1.6f;

        /// <summary>Seconds between gates.</summary>
        public float GateInterval = 0.9f;

        /// <summary>Share of gates that are hazards rather than collectibles.</summary>
        public float HazardChance = 0.35f;

        public float CollectHalfWidth = 0.085f;
        public float HazardHalfWidth = 0.16f;

        /// <summary>Clearance left around a hazard when the track is generated.</summary>
        public float SafeMargin = 0.035f;

        /// <summary>Keeps gates off the screen edges where thumbs cannot reach.</summary>
        public float EdgeMargin = 0.08f;

        public float ProgressPerCollect = 0.055f;
        public float ProgressPenaltyHazard = 0.05f;

        /// <summary>Missing a collectible stings less than hitting a hazard.</summary>
        public float ProgressPenaltyMiss = 0.02f;

        public int ScorePerCollect = 10;

        public FlowConfig Clone() => (FlowConfig)MemberwiseClone();
    }
}
