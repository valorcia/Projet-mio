using Mio.Core.Common;

namespace Mio.Core.Session
{
    /// <summary>
    /// The coordinate contract between the screen and the rules.
    ///
    /// Rules work in play-field space: (0,0) bottom-left, (1,1) top-right of a
    /// fixed-aspect box. This converts a point expressed in that box's own
    /// local units into that space.
    ///
    /// It lives in Core, with no engine types, specifically so the mapping can
    /// be tested headlessly. Resolution independence is the property the whole
    /// input design rests on — a tuning value that meant something different on
    /// a taller phone would silently invalidate every playtest — so it is
    /// verified by tests rather than asserted in a comment.
    /// </summary>
    public static class PlayFieldSpace
    {
        /// <summary>
        /// Maps a local point inside a rect to play-field space.
        ///
        /// Returns false for a degenerate rect, which is what a RectTransform
        /// reports on the frame before layout has run.
        /// </summary>
        public static bool TryNormalise(
            float localX, float localY,
            float rectMinX, float rectMinY,
            float rectWidth, float rectHeight,
            out Vec2 normalised)
        {
            if (rectWidth <= 0f || rectHeight <= 0f)
            {
                normalised = Vec2.Zero;
                return false;
            }

            normalised = new Vec2(
                (localX - rectMinX) / rectWidth,
                (localY - rectMinY) / rectHeight);

            return true;
        }

        /// <summary>True when a play-field point is inside the field itself.</summary>
        public static bool IsInsideField(Vec2 point)
        {
            return point.X >= 0f && point.X <= 1f
                && point.Y >= 0f && point.Y <= 1f;
        }
    }
}
