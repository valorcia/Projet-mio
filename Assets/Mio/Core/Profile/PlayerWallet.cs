using System;
using Mio.Core.Economy;

namespace Mio.Core.Profile
{
    /// <summary>
    /// The running balance of the placeholder resources, and the only thing
    /// allowed to write the profile.
    ///
    /// Gameplay deposits rewards here and never touches the store directly.
    /// </summary>
    public sealed class PlayerWallet
    {
        private readonly IProfileStore _store;
        private PlayerProfile _profile;

        public PlayerWallet(IProfileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            if (!_store.TryLoad(out _profile) || _profile == null)
            {
                _profile = new PlayerProfile();
            }
        }

        public ResourceBundle Balance => _profile.Wallet;

        /// <summary>Raised with (amount deposited, new balance).</summary>
        public event Action<ResourceBundle, ResourceBundle> Changed;

        /// <summary>
        /// Banks a session's payout. An empty reward is a no-op: it neither
        /// writes the store nor raises Changed, so a sitting full of lost
        /// sessions costs no disk writes and fires no spurious UI updates.
        /// </summary>
        public void Deposit(ResourceBundle reward)
        {
            if (reward.IsEmpty) return;

            _profile.Wallet += reward;
            _store.Save(_profile);
            Changed?.Invoke(reward, _profile.Wallet);
        }

        public void Reset()
        {
            _profile = new PlayerProfile();
            _store.Clear();
            Changed?.Invoke(ResourceBundle.Empty, _profile.Wallet);
        }
    }
}
