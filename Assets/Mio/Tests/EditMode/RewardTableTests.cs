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
        public void AbandonedSessionPaysNothing()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Abandoned, 5000);
            Assert.IsTrue(reward.IsEmpty, "quitting a good session must not be farmable");
        }

        [Test]
        public void IdleAndPlayingPayNothing()
        {
            Assert.IsTrue(BuildTable().Evaluate(SessionStatus.Idle, 5000).IsEmpty);
            Assert.IsTrue(BuildTable().Evaluate(SessionStatus.Playing, 5000).IsEmpty);
        }

        [Test]
        public void LostSessionStillPaysParticipation()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Lost, 0);

            Assert.AreEqual(1, reward.Cotton);
            Assert.AreEqual(0, reward.Wood);
            Assert.AreEqual(0, reward.Metal);
        }

        [Test]
        public void HighestReachedTierPaysAndOnlyOnce()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Lost, 600);

            Assert.AreEqual(1, reward.Cotton, "participation");
            Assert.AreEqual(5, reward.Wood, "only the 500 tier, not 500 + 100");
        }

        [Test]
        public void ExactThresholdCounts()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Lost, 500);
            Assert.AreEqual(5, reward.Wood);
        }

        [Test]
        public void WinAddsCompletionBonusOnTop()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Won, 1200);

            Assert.AreEqual(1, reward.Cotton);
            Assert.AreEqual(12, reward.Wood);
            Assert.AreEqual(10, reward.Metal);
        }

        [Test]
        public void ScoreBelowEveryTierPaysParticipationOnly()
        {
            var reward = BuildTable().Evaluate(SessionStatus.Lost, 99);

            Assert.AreEqual(1, reward.Cotton);
            Assert.AreEqual(0, reward.Wood);
        }

        [Test]
        public void EmptyTablePaysNothing()
        {
            Assert.IsTrue(RewardTable.Empty.Evaluate(SessionStatus.Won, 9999).IsEmpty);
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
            bundle[ResourceKind.Wood] = 7;

            Assert.AreEqual(7, bundle.Wood);
            Assert.AreEqual(7, bundle[ResourceKind.Wood]);
            Assert.IsFalse(bundle.IsEmpty);
        }

        [Test]
        public void BundlesAddComponentwise()
        {
            var total = new ResourceBundle(1, 2, 3) + new ResourceBundle(10, 20, 30);

            Assert.AreEqual(11, total.Cotton);
            Assert.AreEqual(22, total.Wood);
            Assert.AreEqual(33, total.Metal);
        }
    }
}
