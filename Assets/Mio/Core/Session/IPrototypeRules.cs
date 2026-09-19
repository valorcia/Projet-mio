namespace Mio.Core.Session
{
    /// <summary>
    /// The complete contract a rule set must satisfy.
    ///
    /// Everything here is pure simulation: no rendering, no UnityEngine, no
    /// wall clock. A rule set driven with the same seed and the same
    /// (deltaTime, input) sequence always produces the same result, which is
    /// what makes sessions testable without Unity and reproducible between the
    /// editor, a device and CI.
    ///
    /// Rule sets never see screen pixels and never name a sound, a particle
    /// prefab, an animation or a vibration. They describe what happened; the
    /// presentation layer decides what that looks and feels like.
    /// </summary>
    public interface IPrototypeRules
    {
        PrototypeId Id { get; }

        SessionStatus Status { get; }

        int Score { get; }

        /// <summary>Win-condition fill, 0..1. Drives the on-screen meter.</summary>
        float Progress01 { get; }

        /// <summary>
        /// Player decisions that worked. The runner copies this into the metric
        /// report, so rule sets must count every meaningful decision here.
        /// </summary>
        int SuccessfulActions { get; }

        /// <summary>
        /// Player decisions that did not work. A slip that expresses no
        /// intention — a touch outside the play area, a cancelled drag — must
        /// not be counted, or the success rate becomes meaningless.
        /// </summary>
        int FailedActions { get; }

        /// <summary>Resets to a fresh session built from the given seed.</summary>
        void Begin(int seed, IFeedbackChannel feedback);

        /// <summary>Advances the simulation. Ignored once the session resolves.</summary>
        void Tick(float deltaTime, IFeedbackChannel feedback);

        /// <summary>Feeds one finger event. Ignored once the session resolves.</summary>
        void HandleInput(in InputCommand command, IFeedbackChannel feedback);
    }
}
