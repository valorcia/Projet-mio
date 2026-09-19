using System.Collections.Generic;

namespace Mio.Core.Session
{
    /// <summary>
    /// The whole contract a prototype must satisfy. Everything here is pure
    /// simulation: no rendering, no Unity, no wall clock. A rules object driven
    /// with the same seed and the same (dt, input) sequence always produces the
    /// same result, which is what makes the prototypes testable and A/B
    /// comparable.
    /// </summary>
    public interface IPrototypeRules
    {
        PrototypeId Id { get; }

        PrototypeStatus Status { get; }

        int Score { get; }

        /// <summary>
        /// Actions the player got right. The runner copies this into metrics, so
        /// rules must count every meaningful player decision here.
        /// </summary>
        int SuccessfulActions { get; }

        int FailedActions { get; }

        /// <summary>Win-condition fill, 0..1. Drives the on-screen meter.</summary>
        float Progress01 { get; }

        /// <summary>Seconds left before the run resolves.</summary>
        float TimeRemaining { get; }

        void Begin(int seed, IFeedbackChannel feedback);

        /// <summary>Advance the simulation. Ignored once the run is resolved.</summary>
        void Tick(float deltaTime, IFeedbackChannel feedback);

        /// <summary>Feed one finger event. Ignored once the run is resolved.</summary>
        void HandleInput(in InputCommand command, IFeedbackChannel feedback);

        /// <summary>
        /// Prototype-specific numbers to attach to the metric report, e.g.
        /// longest chain. Called once when the run resolves.
        /// </summary>
        void CollectCustomMetrics(IDictionary<string, double> into);
    }
}
