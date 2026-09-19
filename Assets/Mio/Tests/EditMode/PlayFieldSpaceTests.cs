using Mio.Core.Common;
using Mio.Core.Session;
using NUnit.Framework;

namespace Mio.Tests
{
    /// <summary>
    /// The coordinate contract rules depend on. If this drifts, a value tuned
    /// on one device silently means something else on another, and every
    /// playtest number becomes incomparable.
    /// </summary>
    [TestFixture]
    public class PlayFieldSpaceTests
    {
        /// <summary>Rect centred on the origin, as a RectTransform reports it.</summary>
        private static bool Normalise(float localX, float localY, float w, float h, out Vec2 result)
        {
            return PlayFieldSpace.TryNormalise(localX, localY, -w * 0.5f, -h * 0.5f, w, h, out result);
        }

        [Test]
        public void CornersAndCentreMapToTheUnitSquare()
        {
            Assert.IsTrue(Normalise(-540f, -960f, 1080f, 1920f, out var bottomLeft));
            Assert.AreEqual(0f, bottomLeft.X, 0.0001f);
            Assert.AreEqual(0f, bottomLeft.Y, 0.0001f);

            Assert.IsTrue(Normalise(540f, 960f, 1080f, 1920f, out var topRight));
            Assert.AreEqual(1f, topRight.X, 0.0001f);
            Assert.AreEqual(1f, topRight.Y, 0.0001f);

            Assert.IsTrue(Normalise(0f, 0f, 1080f, 1920f, out var centre));
            Assert.AreEqual(0.5f, centre.X, 0.0001f);
            Assert.AreEqual(0.5f, centre.Y, 0.0001f);
        }

        [Test]
        public void TheSameRelativePointNormalisesIdenticallyAtAnyResolution()
        {
            // The whole reason rules never see pixels. A finger 30% across and
            // 70% up the play field must produce (0.3, 0.7) on every device.
            var resolutions = new[]
            {
                new[] { 1080f, 1920f },   // reference
                new[] { 540f, 960f },     // half scale
                new[] { 1440f, 2560f },   // larger phone
                new[] { 2048f, 3640f },   // tablet-ish
                new[] { 100f, 177.78f }   // absurdly small
            };

            foreach (var res in resolutions)
            {
                var w = res[0];
                var h = res[1];

                // 30% across, 70% up, expressed in this rect's own local units.
                var localX = -w * 0.5f + 0.3f * w;
                var localY = -h * 0.5f + 0.7f * h;

                Assert.IsTrue(Normalise(localX, localY, w, h, out var p));
                Assert.AreEqual(0.3f, p.X, 0.0005f, "x at {0}x{1}", w, h);
                Assert.AreEqual(0.7f, p.Y, 0.0005f, "y at {0}x{1}", w, h);
            }
        }

        [Test]
        public void NormalisationIsIndependentOfRectOrigin()
        {
            // A RectTransform's local rect is not always centred on the origin;
            // the mapping must key off min and size, not assume a centre.
            PlayFieldSpace.TryNormalise(300f, 400f, 0f, 0f, 1000f, 1000f, out var fromZero);
            PlayFieldSpace.TryNormalise(-200f, -100f, -500f, -500f, 1000f, 1000f, out var fromCentre);

            Assert.AreEqual(fromZero.X, fromCentre.X, 0.0001f);
            Assert.AreEqual(fromZero.Y, fromCentre.Y, 0.0001f);
        }

        [Test]
        public void PointsOutsideTheRectFallOutsideTheUnitSquare()
        {
            // Not clamped: rules decide what an out-of-field touch means, and
            // several need to tell "just off the edge" from "dead centre".
            Assert.IsTrue(Normalise(-1000f, 0f, 1080f, 1920f, out var left));
            Assert.Less(left.X, 0f);
            Assert.IsFalse(PlayFieldSpace.IsInsideField(left));

            Assert.IsTrue(Normalise(1000f, 0f, 1080f, 1920f, out var right));
            Assert.Greater(right.X, 1f);
            Assert.IsFalse(PlayFieldSpace.IsInsideField(right));
        }

        [Test]
        public void IsInsideFieldAcceptsTheBoundary()
        {
            Assert.IsTrue(PlayFieldSpace.IsInsideField(new Vec2(0f, 0f)));
            Assert.IsTrue(PlayFieldSpace.IsInsideField(new Vec2(1f, 1f)));
            Assert.IsTrue(PlayFieldSpace.IsInsideField(new Vec2(0.5f, 0.5f)));
            Assert.IsFalse(PlayFieldSpace.IsInsideField(new Vec2(1.0001f, 0.5f)));
        }

        [Test]
        public void DegenerateRectIsRejectedRatherThanDividingByZero()
        {
            // What a RectTransform reports on the frame before layout runs.
            Assert.IsFalse(PlayFieldSpace.TryNormalise(0f, 0f, 0f, 0f, 0f, 100f, out var a));
            Assert.AreEqual(0f, a.X);

            Assert.IsFalse(PlayFieldSpace.TryNormalise(0f, 0f, 0f, 0f, 100f, 0f, out _));
            Assert.IsFalse(PlayFieldSpace.TryNormalise(0f, 0f, 0f, 0f, -5f, 100f, out _));
        }

        [Test]
        public void AspectRatioDoesNotLeakIntoTheMapping()
        {
            // A wide rect and a tall rect both map their own centre to (0.5,
            // 0.5). Squareness is the play field's job, not the mapping's.
            Assert.IsTrue(Normalise(0f, 0f, 2000f, 500f, out var wide));
            Assert.IsTrue(Normalise(0f, 0f, 500f, 2000f, out var tall));

            Assert.AreEqual(0.5f, wide.X, 0.0001f);
            Assert.AreEqual(0.5f, wide.Y, 0.0001f);
            Assert.AreEqual(wide.X, tall.X, 0.0001f);
            Assert.AreEqual(wide.Y, tall.Y, 0.0001f);
        }
    }
}
