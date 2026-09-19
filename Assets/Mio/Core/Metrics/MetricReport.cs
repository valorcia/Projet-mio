using System.Collections.Generic;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// One completed play attempt. This is the unit we ship to analytics and the
    /// unit we compare when A/B testing a tuning change.
    /// </summary>
    public sealed class MetricReport
    {
        public PrototypeId Prototype;
        public int Seed;

        /// <summary>0 for the first play of a session, 1+ for each replay.</summary>
        public int AttemptIndex;

        public bool IsReplay => AttemptIndex > 0;

        /// <summary>Wall-clock seconds from Begin to the end of the attempt.</summary>
        public float SessionDuration;

        /// <summary>
        /// Seconds from Begin to the very first touch. Negative means the player
        /// never touched the screen, which is the signal that the prototype
        /// failed to communicate its controls.
        /// </summary>
        public float FirstInteractionTime;

        public bool HadInteraction => FirstInteractionTime >= 0f;

        public int SuccessfulActions;
        public int FailedActions;
        public bool Completed;
        public PrototypeStatus Status;
        public int Score;

        /// <summary>Resources granted for this attempt, by resource id.</summary>
        public readonly Dictionary<ResourceKind, int> Rewards = new Dictionary<ResourceKind, int>();

        /// <summary>Prototype-specific counters (longest chain, pieces placed...).</summary>
        public readonly Dictionary<string, double> Custom = new Dictionary<string, double>();

        public int TotalActions => SuccessfulActions + FailedActions;

        public float SuccessRate =>
            TotalActions == 0 ? 0f : SuccessfulActions / (float)TotalActions;

        public override string ToString()
        {
            return $"{Prototype} seed={Seed} attempt={AttemptIndex} status={Status} " +
                   $"score={Score} dur={SessionDuration:0.00}s first={FirstInteractionTime:0.00}s " +
                   $"ok={SuccessfulActions} fail={FailedActions}";
        }
    }
}
