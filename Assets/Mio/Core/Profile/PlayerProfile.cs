using Mio.Core.Economy;

namespace Mio.Core.Profile
{
    /// <summary>
    /// Everything that survives between sessions.
    ///
    /// Deliberately tiny: M0.1 only needs to prove that a session can pay into
    /// a persistent balance. The town will grow this, and the schema version is
    /// here so that when it does, an old save can be recognised and migrated
    /// rather than silently misread.
    /// </summary>
    public sealed class PlayerProfile
    {
        /// <summary>Bump when the stored shape changes incompatibly.</summary>
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;

        public ResourceBundle Wallet = ResourceBundle.Empty;

        public PlayerProfile Clone()
        {
            return new PlayerProfile
            {
                SchemaVersion = SchemaVersion,
                Wallet = Wallet
            };
        }
    }
}
