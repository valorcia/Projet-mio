using Mio.Core.Common;

namespace Mio.Core.Session
{
    public enum InputPhase
    {
        Began = 0,
        Moved = 1,
        Ended = 2,

        /// <summary>OS stole the touch (call, notification, app switch).</summary>
        Canceled = 3
    }

    /// <summary>
    /// One finger, normalised to the play field.
    ///
    /// Position is in play-field space: (0,0) bottom-left, (1,1) top-right.
    /// Rules never see pixels, so a rule tuned on one device behaves identically
    /// on every other aspect ratio, and tests can drive gameplay without Unity.
    /// </summary>
    public readonly struct InputCommand
    {
        public readonly InputPhase Phase;
        public readonly Vec2 Position;

        /// <summary>Seconds since the attempt began.</summary>
        public readonly float Time;

        public InputCommand(InputPhase phase, Vec2 position, float time)
        {
            Phase = phase;
            Position = position;
            Time = time;
        }

        public bool IsPressed => Phase == InputPhase.Began || Phase == InputPhase.Moved;

        public static InputCommand Began(float x, float y, float time = 0f) =>
            new InputCommand(InputPhase.Began, new Vec2(x, y), time);

        public static InputCommand Moved(float x, float y, float time = 0f) =>
            new InputCommand(InputPhase.Moved, new Vec2(x, y), time);

        public static InputCommand Ended(float x, float y, float time = 0f) =>
            new InputCommand(InputPhase.Ended, new Vec2(x, y), time);
    }
}
