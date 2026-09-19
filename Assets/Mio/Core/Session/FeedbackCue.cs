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

    /// <summary>Swallows cues. Used by tests and headless simulation.</summary>
    public sealed class NullFeedbackChannel : IFeedbackChannel
    {
        public static readonly NullFeedbackChannel Instance = new NullFeedbackChannel();
        public void Emit(in FeedbackCue cue) { }
    }
}
