using System;
using Mio.Core.Economy;
using UnityEngine;

namespace Mio.Unity.App
{
    /// <summary>
    /// Persistence behind an interface.
    ///
    /// The brief calls for the save system to be abstracted from gameplay, and
    /// this is where that line is drawn: nothing above this interface knows
    /// whether we are on PlayerPrefs, a JSON file or a server. M0 only needs to
    /// prove a prototype can pay into a persistent balance.
    /// </summary>
    public interface IProfileStore
    {
        bool TryLoad(out ResourceBundle wallet);
        void Save(ResourceBundle wallet);
        void Clear();
    }

    /// <summary>
    /// PlayerPrefs implementation. Deliberately the dumbest thing that works:
    /// the real save system arrives with the town, and swapping it out means
    /// writing one new class.
    /// </summary>
    public sealed class PlayerPrefsProfileStore : IProfileStore
    {
        private const string KeyPrefix = "mio.wallet.";

        public bool TryLoad(out ResourceBundle wallet)
        {
            wallet = ResourceBundle.Empty;
            if (!PlayerPrefs.HasKey(KeyPrefix + ResourceKind.Energy)) return false;

            foreach (var kind in ResourceKinds.All)
            {
                wallet[kind] = PlayerPrefs.GetInt(KeyPrefix + kind, 0);
            }

            return true;
        }

        public void Save(ResourceBundle wallet)
        {
            foreach (var kind in ResourceKinds.All)
            {
                PlayerPrefs.SetInt(KeyPrefix + kind, wallet[kind]);
            }

            PlayerPrefs.Save();
        }

        public void Clear()
        {
            foreach (var kind in ResourceKinds.All)
            {
                PlayerPrefs.DeleteKey(KeyPrefix + kind);
            }

            PlayerPrefs.Save();
        }
    }

    /// <summary>Running balance of the three placeholder resources.</summary>
    public sealed class PlayerWallet
    {
        private readonly IProfileStore _store;
        private ResourceBundle _balance;

        public PlayerWallet(IProfileStore store)
        {
            _store = store ?? new PlayerPrefsProfileStore();
            if (_store.TryLoad(out var loaded)) _balance = loaded;
        }

        public ResourceBundle Balance => _balance;

        public event Action<ResourceBundle, ResourceBundle> Changed;

        public void Deposit(ResourceBundle reward)
        {
            if (reward.IsEmpty) return;

            _balance += reward;
            _store.Save(_balance);
            Changed?.Invoke(reward, _balance);
        }

        public void Reset()
        {
            _balance = ResourceBundle.Empty;
            _store.Clear();
            Changed?.Invoke(ResourceBundle.Empty, _balance);
        }
    }
}
