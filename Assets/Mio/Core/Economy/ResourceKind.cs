namespace Mio.Core.Economy
{
    /// <summary>
    /// The three placeholder resources for M0.
    ///
    /// These names are deliberately generic: M0 only needs to prove that a
    /// prototype can pay out. The town economy will rename and re-balance them,
    /// so nothing should branch on a specific value beyond presentation.
    /// </summary>
    public enum ResourceKind
    {
        Energy = 0,
        Material = 1,
        Coin = 2
    }

    public static class ResourceKinds
    {
        /// <summary>Stable iteration order for UI and serialisation.</summary>
        public static readonly ResourceKind[] All =
        {
            ResourceKind.Energy,
            ResourceKind.Material,
            ResourceKind.Coin
        };
    }
}
