namespace Mio.Core.Economy
{
    /// <summary>
    /// The three placeholder resource families every M0.2 prototype shares.
    ///
    /// Comparability is the whole point of M0.2: four prototypes are only worth
    /// comparing if a tester recognises the same three materials in all of
    /// them. Visual treatment may differ where a mechanic demands it, but the
    /// identity must stay obvious.
    ///
    /// Still placeholders — the town economy will rename and re-balance them,
    /// so nothing should branch on a specific value beyond presentation.
    /// </summary>
    public enum ResourceKind
    {
        Cotton = 0,
        Wood = 1,
        Metal = 2
    }

    public static class ResourceKinds
    {
        /// <summary>Stable iteration order for UI, metrics and serialisation.</summary>
        public static readonly ResourceKind[] All =
        {
            ResourceKind.Cotton,
            ResourceKind.Wood,
            ResourceKind.Metal
        };

        public const int Count = 3;

        public static ResourceKind At(int index)
        {
            var i = index % Count;
            if (i < 0) i += Count;
            return All[i];
        }
    }
}
