namespace Mio.Core.Session
{
    public enum PrototypeId
    {
        Flow = 0,
        PopChain = 1,
        Pack = 2
    }

    public enum PrototypeStatus
    {
        /// <summary>Not begun yet.</summary>
        Idle = 0,

        /// <summary>Begun and accepting input.</summary>
        Playing = 1,

        /// <summary>Reached the win condition.</summary>
        Won = 2,

        /// <summary>Ran out of time, or hit a fail condition.</summary>
        Lost = 3,

        /// <summary>Left before the run resolved. Pays nothing.</summary>
        Abandoned = 4
    }

    public static class PrototypeStatusExtensions
    {
        public static bool IsResolved(this PrototypeStatus status)
        {
            return status == PrototypeStatus.Won
                || status == PrototypeStatus.Lost
                || status == PrototypeStatus.Abandoned;
        }
    }
}
