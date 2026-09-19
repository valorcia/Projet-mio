using Mio.Core.Economy;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class RewardTableTests
    {
        private static RewardTable BuildTable()
        {
            return new RewardTable(
                participation: new ResourceBundle(1, 0, 0),
                completionBonus: new ResourceBundle(0, 0, 10),
                tiers: new[]
                {
                    // Deliberately out of order: the table must sort them.
                    new RewardTier(500, new ResourceBundle(0, 5, 0)),
                    new RewardTier(100, new ResourceBundle(0, 1, 0)),
                    new RewardTier(1000, new ResourceBundle(0, 12, 0))
                });
        }

        [Test]
        public void AbandonedAttemptPaysNothing()
        {
            var reward = BuildTable().Evaluate(PrototypeStatus.Abandoned, 5000);
            Assert.IsTrue(reward.IsEmpty, "quitting a good run must not be farmable");
        }

        [Test]
        public void LostAttemptStillPaysParticipation()
        {
            var reward = BuildTable().Evaluate(PrototypeStatus.Lost, 0);

            Assert.AreEqual(1, reward.Energy);
            Assert.AreEqual(0, reward.Material);
            Assert.AreEqual(0, reward.Coin);
        }

        [Test]
        public void HighestReachedTierPaysAndOnlyOnce()
        {
            var reward = BuildTable().Evaluate(PrototypeStatus.Lost, 600);

            Assert.AreEqual(1, reward.Energy, "participation");
            Assert.AreEqual(5, reward.Material, "only the 500 tier, not 500 + 100");
        }

        [Test]
        public void WinAddsCompletionBonusOnTop()
        {
            var reward = BuildTable().Evaluate(PrototypeStatus.Won, 1200);

            Assert.AreEqual(1, reward.Energy);
            Assert.AreEqual(12, reward.Material);
            Assert.AreEqual(10, reward.Coin);
        }

        [Test]
        public void ScoreBelowEveryTierPaysParticipationOnly()
        {
            var reward = BuildTable().Evaluate(PrototypeStatus.Lost, 99);

            Assert.AreEqual(1, reward.Energy);
            Assert.AreEqual(0, reward.Material);
        }

        [Test]
        public void EmptyTablePaysNothing()
        {
            Assert.IsTrue(RewardTable.Empty.Evaluate(PrototypeStatus.Won, 9999).IsEmpty);
        }

        [Test]
        public void ConstructorDoesNotMutateCallerArray()
        {
            // Tier arrays come from shared ScriptableObject data; sorting in
            // place would quietly reorder the designer's asset.
            var tiers = new[]
            {
                new RewardTier(500, new ResourceBundle(0, 5, 0)),
                new RewardTier(100, new ResourceBundle(0, 1, 0))
            };

            _ = new RewardTable(ResourceBundle.Empty, ResourceBundle.Empty, tiers);

            Assert.AreEqual(500, tiers[0].MinScore);
            Assert.AreEqual(100, tiers[1].MinScore);
        }

        [Test]
        public void BundleIndexerRoundTrips()
        {
            var bundle = ResourceBundle.Empty;
            bundle[ResourceKind.Material] = 7;

            Assert.AreEqual(7, bundle.Material);
            Assert.AreEqual(7, bundle[ResourceKind.Material]);
            Assert.IsFalse(bundle.IsEmpty);
        }
    }
}
