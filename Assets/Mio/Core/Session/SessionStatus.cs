namespace Mio.Core.Session
{
    /// <summary>
    /// Identifies which rule set produced a session. Written into every metric
    /// report as prototype_id, and the key the comparison report groups by.
    /// </summary>
    public enum PrototypeId
    {
        /// <summary>The architecture validation rule set. Not a game.</summary>
        TestHarness = 0,

        /// <summary>A: swap to combine three, transforming into the next tier.</summary>
        Stack = 1,

        /// <summary>B: drag to merge pairs, delivering products to orders.</summary>
        MergeFactory = 2,

        /// <summary>C: tap connected groups. The simplest candidate.</summary>
        Pop = 3,

        /// <summary>D: combine to build finished products. The hybrid.</summary>
        MioMix = 4
    }

    public static class PrototypeIds
    {
        /// <summary>The four candidates under comparison, in spec order.</summary>
        public static readonly PrototypeId[] Candidates =
        {
            PrototypeId.Stack,
            PrototypeId.MergeFactory,
            PrototypeId.Pop,
            PrototypeId.MioMix
        };
    }

    public enum SessionStatus
    {
        /// <summary>Not begun yet.</summary>
        Idle = 0,

        /// <summary>Begun and accepting input.</summary>
        Playing = 1,

        /// <summary>Reached the win condition.</summary>
        Won = 2,

        /// <summary>Ran out of time, or hit a fail condition.</summary>
        Lost = 3,

        /// <summary>Left before the session resolved. Pays nothing.</summary>
        Abandoned = 4
    }

    public static class SessionStatusExtensions
    {
        public static bool IsResolved(this SessionStatus status)
        {
            return status == SessionStatus.Won
                || status == SessionStatus.Lost
                || status == SessionStatus.Abandoned;
        }

        /// <summary>True for the two outcomes that a reward table will pay.</summary>
        public static bool IsPlayedToEnd(this SessionStatus status)
        {
            return status == SessionStatus.Won || status == SessionStatus.Lost;
        }
    }
}
