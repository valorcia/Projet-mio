using System;
using Mio.Core.Economy;
using Mio.Core.Profile;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class PlayerWalletTests
    {
        private InMemoryProfileStore _store;
        private PlayerWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryProfileStore();
            _wallet = new PlayerWallet(_store);
        }

        [Test]
        public void NewWalletStartsEmpty()
        {
            Assert.IsTrue(_wallet.Balance.IsEmpty);
        }

        [Test]
        public void ConstructorRejectsNullStore()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerWallet(null));
        }

        [Test]
        public void DepositAccumulates()
        {
            _wallet.Deposit(new ResourceBundle(1, 2, 3));
            _wallet.Deposit(new ResourceBundle(10, 20, 30));

            Assert.AreEqual(11, _wallet.Balance.Energy);
            Assert.AreEqual(22, _wallet.Balance.Material);
            Assert.AreEqual(33, _wallet.Balance.Coin);
        }

        [Test]
        public void DepositPersists()
        {
            _wallet.Deposit(new ResourceBundle(4, 5, 6));

            // A second wallet over the same store is what happens on app
            // relaunch.
            var reloaded = new PlayerWallet(_store);

            Assert.AreEqual(4, reloaded.Balance.Energy);
            Assert.AreEqual(5, reloaded.Balance.Material);
            Assert.AreEqual(6, reloaded.Balance.Coin);
        }

        [Test]
        public void EmptyDepositDoesNotTouchTheStore()
        {
            // A sitting full of lost sessions must not cost a disk write per
            // session.
            _wallet.Deposit(ResourceBundle.Empty);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void EmptyDepositDoesNotRaiseChanged()
        {
            var raised = 0;
            _wallet.Changed += (_, __) => raised++;

            _wallet.Deposit(ResourceBundle.Empty);

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void ChangedReportsDeltaAndNewBalance()
        {
            _wallet.Deposit(new ResourceBundle(1, 0, 0));

            var delta = ResourceBundle.Empty;
            var balance = ResourceBundle.Empty;
            _wallet.Changed += (d, b) => { delta = d; balance = b; };

            _wallet.Deposit(new ResourceBundle(2, 0, 0));

            Assert.AreEqual(2, delta.Energy, "the amount just deposited");
            Assert.AreEqual(3, balance.Energy, "the running total");
        }

        [Test]
        public void ResetClearsBalanceAndStore()
        {
            _wallet.Deposit(new ResourceBundle(9, 9, 9));
            _wallet.Reset();

            Assert.IsTrue(_wallet.Balance.IsEmpty);
            Assert.IsFalse(_store.TryLoad(out _), "the stored profile should be gone");
        }

        [Test]
        public void StoreHandsOutCopiesNotLiveState()
        {
            // If the store returned its own instance, mutating a loaded profile
            // would silently rewrite persisted state without a Save.
            _wallet.Deposit(new ResourceBundle(5, 0, 0));

            Assert.IsTrue(_store.TryLoad(out var first));
            first.Wallet = new ResourceBundle(999, 0, 0);

            Assert.IsTrue(_store.TryLoad(out var second));
            Assert.AreEqual(5, second.Wallet.Energy);
        }

        [Test]
        public void ProfileCarriesCurrentSchemaVersion()
        {
            _wallet.Deposit(new ResourceBundle(1, 0, 0));

            Assert.IsTrue(_store.TryLoad(out var profile));
            Assert.AreEqual(PlayerProfile.CurrentSchemaVersion, profile.SchemaVersion);
        }
    }
}
