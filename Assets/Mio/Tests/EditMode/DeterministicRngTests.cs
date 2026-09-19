using Mio.Core.Common;
using NUnit.Framework;

namespace Mio.Tests
{
    [TestFixture]
    public class DeterministicRngTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);

            for (var i = 0; i < 256; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), "diverged at draw {0}", i);
            }
        }

        [Test]
        public void NeighbouringSeedsProduceDifferentSequences()
        {
            // Seeds 0..9 are what a designer types first; they must not all
            // generate near-identical boards.
            var first = new int[10];
            for (var seed = 0; seed < 10; seed++)
            {
                first[seed] = new DeterministicRng(seed).Range(0, 1000000);
            }

            CollectionAssert.AllItemsAreUnique(first);
        }

        [Test]
        public void RangeStaysWithinBounds()
        {
            var rng = new DeterministicRng(7);
            for (var i = 0; i < 10000; i++)
            {
                var value = rng.Range(3, 9);
                Assert.GreaterOrEqual(value, 3);
                Assert.Less(value, 9);
            }
        }

        [Test]
        public void RangeWithEmptySpanReturnsMinimum()
        {
            var rng = new DeterministicRng(7);
            Assert.AreEqual(5, rng.Range(5, 5));
            Assert.AreEqual(5, rng.Range(5, 2));
        }

        [Test]
        public void Next01StaysInUnitInterval()
        {
            var rng = new DeterministicRng(99);
            for (var i = 0; i < 10000; i++)
            {
                var value = rng.Next01();
                Assert.GreaterOrEqual(value, 0f);
                Assert.Less(value, 1f);
            }
        }

        [Test]
        public void RangeCoversEveryBucket()
        {
            var rng = new DeterministicRng(4242);
            var counts = new int[6];
            for (var i = 0; i < 60000; i++)
            {
                counts[rng.Range(0, 6)]++;
            }

            // Uniform enough that no bucket is starved. A modulo-biased
            // generator would skew the tail buckets well outside this band.
            foreach (var count in counts)
            {
                Assert.Greater(count, 9000, "bucket badly under-represented");
                Assert.Less(count, 11000, "bucket badly over-represented");
            }
        }

        [Test]
        public void ChanceHonoursCertainty()
        {
            var rng = new DeterministicRng(1);
            Assert.IsFalse(rng.Chance(0f));
            Assert.IsTrue(rng.Chance(1f));
        }
    }
}
