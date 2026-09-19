namespace Mio.Core.Session
{
    /// <summary>
    /// Identifies which rule set produced a session. Written into every metric
    /// report as prototype_id.
    ///
    /// Prototypes are added here as they land.
    /// </summary>
    public enum PrototypeId
    {
        /// <summary>The architecture validation rule set. Not a game.</summary>
        TestHarness = 0,

        /// <summary>A: hold and steer a stream through gates.</summary>
        Flow = 1
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
