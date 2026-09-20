using Mio.Core.Common;

namespace Mio.Core.Session
{
    /// <summary>
    /// The "one more" experiment, identical in all four prototypes.
    ///
    /// When a session ends the result is shown but <b>no replay control is
    /// offered</b> for a short quiet window. If the player reaches for the
    /// board during that silence — taps it, swipes it, pokes at it — that is
    /// the most honest signal we have that the prototype is compelling: nobody
    /// paws at a dead screen for a game they were relieved to finish.
    ///
    /// Only after the window does PLAY AGAIN appear, and using it is recorded
    /// separately as a weaker, prompted signal.
    ///
    /// Lives in Core, with no engine types, so the timing is deterministic and
    /// testable rather than something only a playtest could verify.
    /// </summary>
    public sealed class ReplayExperiment
    {
        private readonly float _quietWindow;
        private readonly float _inputLockout;

        private float _sinceResolved;
        private bool _armed;

        /// <param name="quietWindow">
        /// Seconds the result is shown with no replay control.
        /// </param>
        /// <param name="inputLockout">
        /// Seconds at the very start during which a touch is ignored entirely,
        /// so the tap that finished the session cannot be mistaken for the
        /// player reaching for more.
        /// </param>
        public ReplayExperiment(float quietWindow = 2.5f, float inputLockout = 0.6f)
        {
            _quietWindow = quietWindow < 0f ? 0f : quietWindow;
            _inputLockout = MathK.Clamp(inputLockout, 0f, _quietWindow);
        }

        /// <summary>True between the session resolving and the next one starting.</summary>
        public bool IsActive => _armed;

        /// <summary>True once the quiet window has elapsed and PLAY AGAIN is offered.</summary>
        public bool PromptVisible => _armed && _sinceResolved >= _quietWindow;

        /// <summary>
        /// True when the player reached for the board during the silence. Read
        /// by the next session's metric report.
        /// </summary>
        public bool ReachedWithoutPrompt { get; private set; }

        /// <summary>Seconds remaining before the prompt appears.</summary>
        public float QuietRemaining =>
            !_armed ? 0f : MathK.Clamp(_quietWindow - _sinceResolved, 0f, _quietWindow);

        /// <summary>Call the moment a session resolves.</summary>
        public void OnSessionResolved()
        {
            _armed = true;
            _sinceResolved = 0f;
            ReachedWithoutPrompt = false;
        }

        public void Tick(float deltaTime)
        {
            if (!_armed || deltaTime <= 0f) return;
            _sinceResolved += deltaTime;
        }

        /// <summary>
        /// Feeds a touch that arrived while the result was on screen. Returns
        /// true when it should start a new session.
        /// </summary>
        public bool HandlePress()
        {
            if (!_armed) return false;

            // The tap that won the game is not a request for another one.
            if (_sinceResolved < _inputLockout) return false;

            if (!PromptVisible)
            {
                // Reaching for a board that offers nothing. Recorded, and it
                // starts the next session anyway: making the player wait out
                // the window would punish exactly the enthusiasm we are
                // trying to measure.
                ReachedWithoutPrompt = true;
            }

            return true;
        }

        /// <summary>Call when the next session begins, to disarm.</summary>
        public void Reset()
        {
            _armed = false;
            _sinceResolved = 0f;
        }

        /// <summary>
        /// How the replay that just happened should be recorded: unprompted
        /// beats prompted, and both are false for the first session of a
        /// sitting.
        /// </summary>
        public void ReadFlags(out bool requested, out bool withoutPrompt)
        {
            withoutPrompt = ReachedWithoutPrompt;
            requested = !withoutPrompt;
        }
    }
}
