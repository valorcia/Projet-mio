using Mio.Core.Economy;
using Mio.Core.Profile;
using UnityEngine;

namespace Mio.Unity.App
{
    /// <summary>
    /// Temporary prototype persistence.
    ///
    /// <b>PlayerPrefs is a placeholder for M0 only.</b> It is not a save
    /// system: it has no atomicity, no migration story and no size budget, and
    /// it is trivially editable by the player.
    ///
    /// This is the only class in the project allowed to touch PlayerPrefs, and
    /// gameplay cannot reach it even by accident: Mio.Core is compiled with
    /// noEngineReferences, so a Core class that tried would fail to build.
    /// Replacing this with a real store means writing one new class and
    /// changing one line at the composition root.
    /// </summary>
    public sealed class PlayerPrefsProfileStore : IProfileStore
    {
        private const string KeyPrefix = "mio.profile.";
        private const string SchemaKey = KeyPrefix + "schema";

        public bool TryLoad(out PlayerProfile profile)
        {
            profile = null;
            if (!PlayerPrefs.HasKey(SchemaKey)) return false;

            var schema = PlayerPrefs.GetInt(SchemaKey, 0);

            // A save from a future or unknown build is discarded rather than
            // half-read. In M0 losing placeholder resources costs nothing;
            // silently misreading them would cost trust in the data.
            if (schema != PlayerProfile.CurrentSchemaVersion)
            {
                Debug.LogWarning(
                    $"[MIO] Discarding profile with schema {schema}, " +
                    $"expected {PlayerProfile.CurrentSchemaVersion}.");
                return false;
            }

            profile = new PlayerProfile { SchemaVersion = schema };

            var wallet = ResourceBundle.Empty;
            foreach (var kind in ResourceKinds.All)
            {
                wallet[kind] = PlayerPrefs.GetInt(KeyPrefix + kind, 0);
            }

            profile.Wallet = wallet;
            return true;
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) return;

            PlayerPrefs.SetInt(SchemaKey, PlayerProfile.CurrentSchemaVersion);

            foreach (var kind in ResourceKinds.All)
            {
                PlayerPrefs.SetInt(KeyPrefix + kind, profile.Wallet[kind]);
            }

            PlayerPrefs.Save();
        }

        public void Clear()
        {
            PlayerPrefs.DeleteKey(SchemaKey);

            foreach (var kind in ResourceKinds.All)
            {
                PlayerPrefs.DeleteKey(KeyPrefix + kind);
            }

            PlayerPrefs.Save();
        }
    }
}
