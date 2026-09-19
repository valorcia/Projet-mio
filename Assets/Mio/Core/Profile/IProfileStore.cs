namespace Mio.Core.Profile
{
    /// <summary>
    /// Persistence behind an interface.
    ///
    /// This is the line the brief asks for: nothing above this interface knows
    /// whether the profile lives in PlayerPrefs, a JSON file or a server.
    ///
    /// The interface sits in Mio.Core, which cannot reference UnityEngine at
    /// all, so it is impossible for a gameplay class to reach PlayerPrefs even
    /// by accident — the compiler enforces it rather than a code review.
    /// </summary>
    public interface IProfileStore
    {
        /// <summary>Returns false when no profile has been saved yet.</summary>
        bool TryLoad(out PlayerProfile profile);

        void Save(PlayerProfile profile);

        /// <summary>Wipes the stored profile. Used by tests and a debug reset.</summary>
        void Clear();
    }

    /// <summary>
    /// Keeps the profile in memory only. The default for tests and for a
    /// headless session, so nothing has to stub the interface by hand.
    /// </summary>
    public sealed class InMemoryProfileStore : IProfileStore
    {
        private PlayerProfile _profile;

        public int SaveCount { get; private set; }

        public bool TryLoad(out PlayerProfile profile)
        {
            // Hand out a copy: callers must not be able to mutate stored state
            // without going through Save, or a test would pass for the wrong
            // reason and a real store would behave differently.
            profile = _profile?.Clone();
            return profile != null;
        }

        public void Save(PlayerProfile profile)
        {
            _profile = profile?.Clone();
            SaveCount++;
        }

        public void Clear()
        {
            _profile = null;
        }
    }
}
