using Mio.Core.Economy;

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

        /// <summary>
        /// The obvious short-term goal. Drives the HUD and objective_progress,
        /// and is the same shape in all four prototypes so their numbers can be
        /// compared.
        /// </summary>
        Objective Objective { get; }

        /// <summary>Win-condition fill, 0..1. Usually the objective's progress.</summary>
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

        /// <summary>Seconds left before the session resolves.</summary>
        float TimeRemaining { get; }

        /// <summary>Length of a full session, for the HUD's timer bar.</summary>
        float Duration { get; }

        /// <summary>Resources this session has earned from play, before rewards.</summary>
        ResourceBundle ResourcesEarned { get; }

        /// <summary>Resets to a fresh session built from the given seed.</summary>
        void Begin(int seed, IFeedbackChannel feedback);

        /// <summary>Advances the simulation. Ignored once the session resolves.</summary>
        void Tick(float deltaTime, IFeedbackChannel feedback);

        /// <summary>Feeds one finger event. Ignored once the session resolves.</summary>
        void HandleInput(in InputCommand command, IFeedbackChannel feedback);

        /// <summary>
        /// Prototype-specific counters for the metric report, e.g. cascade
        /// depth or orders completed. Called once when the session resolves.
        /// </summary>
        void CollectCustomMetrics(System.Collections.Generic.IDictionary<string, double> into);
    }
}
