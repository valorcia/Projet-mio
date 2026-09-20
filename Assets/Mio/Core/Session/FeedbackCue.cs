using Mio.Core.Common;

namespace Mio.Core.Session
{
    /// <summary>
    /// What just happened, in gameplay terms. Rule sets emit these; the
    /// presentation layer decides what a "Success" looks, sounds and feels like.
    ///
    /// This is the seam that keeps feel out of the rules. A rule set may emit
    /// semantic cues only: it must never name an audio clip, a particle prefab,
    /// an animation or a vibration. Designers can therefore retune the entire
    /// feel of the game with no risk whatsoever of changing the simulation.
    ///
    /// Prototype-specific kinds (a chain step, a line clear) are added here
    /// alongside the prototype that emits them.
    /// </summary>
    public enum FeedbackCueKind
    {
        None = 0,

        /// <summary>The run started. Good place for the "go" sting.</summary>
        Begin,

        /// <summary>Generic rewarded action.</summary>
        Success,

        /// <summary>Generic rejected action. Must never feel punishing.</summary>
        Fail,

        /// <summary>Progress meter changed. Intensity = new fill 0..1.</summary>
        Progress,

        // ---- combination scale ----
        // Escalating on purpose: a cascade must feel bigger than a single
        // combination without the rules knowing what "bigger" looks like.

        /// <summary>The smallest valid combination. Value = pieces involved.</summary>
        ComboSmall,

        /// <summary>A middling combination.</summary>
        ComboMedium,

        /// <summary>A large combination. Should feel disproportionate.</summary>
        ComboLarge,

        /// <summary>Second link of a chain reaction. Value = depth.</summary>
        Cascade2,

        /// <summary>Third link.</summary>
        Cascade3,

        /// <summary>Fourth link or deeper. The payoff moment.</summary>
        CascadeEpic,

        // ---- production and objective ----

        /// <summary>A finished product was made. Value = tier.</summary>
        ProductCreated,

        /// <summary>One objective line was satisfied.</summary>
        ObjectiveComplete,

        // ---- MIO ----

        /// <summary>The MIO meter filled.</summary>
        MioPowerReady,

        /// <summary>A MIO power was spent. Value = power index.</summary>
        MioPowerUsed,

        /// <summary>Run resolved as a win.</summary>
        Win,

        /// <summary>Run resolved as a loss.</summary>
        Lose
    }

    public readonly struct FeedbackCue
    {
        public readonly FeedbackCueKind Kind;

        /// <summary>Play-field space, matching <see cref="InputCommand"/>.</summary>
        public readonly Vec2 Position;

        /// <summary>0..1 strength. Drives particle count, shake and haptic weight.</summary>
        public readonly float Intensity;

        /// <summary>Kind-specific integer payload, documented per cue kind.</summary>
        public readonly int Value;

        public FeedbackCue(FeedbackCueKind kind, Vec2 position, float intensity = 1f, int value = 0)
        {
            Kind = kind;
            Position = position;
            Intensity = MathK.Clamp01(intensity);
            Value = value;
        }
    }

    public interface IFeedbackChannel
    {
        void Emit(in FeedbackCue cue);
    }

    /// <summary>
    /// Maps combination size and cascade depth onto the shared cue scale.
    ///
    /// Shared deliberately: M0.2 compares mechanics, not presentation, so a
    /// five-piece combination must earn the same cue in all four prototypes.
    /// Leaving each rule set to decide would make one feel juicier than
    /// another for reasons that have nothing to do with its design.
    /// </summary>
    public static class ComboScale
    {
        public static FeedbackCueKind ForSize(int pieces)
        {
            if (pieces >= 8) return FeedbackCueKind.ComboLarge;
            if (pieces >= 5) return FeedbackCueKind.ComboMedium;
            return FeedbackCueKind.ComboSmall;
        }

        /// <summary>Depth 1 is the first link and gets no cascade cue.</summary>
        public static FeedbackCueKind? ForCascadeDepth(int depth)
        {
            if (depth >= 4) return FeedbackCueKind.CascadeEpic;
            if (depth == 3) return FeedbackCueKind.Cascade3;
            if (depth == 2) return FeedbackCueKind.Cascade2;
            return null;
        }

        /// <summary>0..1 strength for a combination, for particle counts and shake.</summary>
        public static float Intensity(int pieces)
        {
            return MathK.Clamp01(pieces / 10f);
        }
    }

    /// <summary>Swallows cues. Used by tests and headless simulation.</summary>
    public sealed class NullFeedbackChannel : IFeedbackChannel
    {
        public static readonly NullFeedbackChannel Instance = new NullFeedbackChannel();
        public void Emit(in FeedbackCue cue) { }
    }
}
