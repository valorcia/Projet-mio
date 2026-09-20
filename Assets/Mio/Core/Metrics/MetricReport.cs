using System;
using System.Collections.Generic;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// One completed session. The unit shipped to analytics, and the unit the
    /// comparison report aggregates.
    ///
    /// Field names here are C#; the wire names emitted by the JSONL sink are
    /// the snake_case ones from the M0.2 spec.
    /// </summary>
    public sealed class MetricReport
    {
        /// <summary>Unique per session. Lets replays of one sitting be grouped.</summary>
        public string SessionId = string.Empty;

        /// <summary>Local, non-personal tester label, e.g. "T001".</summary>
        public string TesterId = string.Empty;

        public PrototypeId PrototypeId;

        public int Seed;

        /// <summary>UTC wall clock at session start, for ordering the log.</summary>
        public DateTime SessionStartUtc;

        public DateTime SessionEndUtc;

        /// <summary>
        /// Seconds of simulated play, accumulated from deltaTime rather than
        /// read off the wall clock, so a frame hitch or a breakpoint does not
        /// corrupt it.
        /// </summary>
        public float SessionDuration;

        /// <summary>
        /// Seconds to the first touch. Negative means the player never touched
        /// the screen, which is the signal that the prototype failed to
        /// communicate its controls.
        /// </summary>
        public float TimeToFirstInput;

        /// <summary>
        /// Seconds to the first action that worked. Negative means the player
        /// never managed one. The gap between this and TimeToFirstInput is how
        /// long the prototype took to become understandable.
        /// </summary>
        public float TimeToFirstSuccess;

        public bool HadInput => TimeToFirstInput >= 0f;
        public bool HadSuccess => TimeToFirstSuccess >= 0f;

        /// <summary>Distinct touches. See SessionMetricsRecorder for the definition.</summary>
        public int TotalInputs;

        public int SuccessfulActions;

        public int FailedActions;

        public int Score;

        /// <summary>Objective fill at the moment the session resolved, 0..1.</summary>
        public float ObjectiveProgress;

        public bool ObjectiveCompleted;

        public SessionStatus CompletionStatus;

        public bool Completed => CompletionStatus == SessionStatus.Won;

        /// <summary>Resources produced by play, before the reward table.</summary>
        public ResourceBundle ResourcesEarned;

        /// <summary>
        /// True when this session was started by an explicit replay request,
        /// meaning the player used the PLAY AGAIN control.
        /// </summary>
        public bool ReplayRequested;

        /// <summary>
        /// True when this session was started by the player reaching for the
        /// board <b>before</b> any replay control was offered.
        ///
        /// This is the "one more" signal, and the most honest read we have on
        /// whether a prototype is compelling: nobody paws at a dead screen for
        /// a game they were relieved to finish.
        /// </summary>
        public bool ReplayWithoutPrompt;

        /// <summary>0 for the first session of a sitting, 1+ for each replay.</summary>
        public int AttemptIndex;

        /// <summary>Resources granted by the reward table, by resource id.</summary>
        public readonly Dictionary<ResourceKind, int> Rewards = new Dictionary<ResourceKind, int>();

        /// <summary>Prototype-specific counters: cascade depth, orders, and so on.</summary>
        public readonly Dictionary<string, double> Custom = new Dictionary<string, double>();

        public int TotalActions => SuccessfulActions + FailedActions;

        public float SuccessRate =>
            TotalActions == 0 ? 0f : SuccessfulActions / (float)TotalActions;

        public float FailureRate =>
            TotalActions == 0 ? 0f : FailedActions / (float)TotalActions;

        /// <summary>Actions per minute. The pace comparison across prototypes.</summary>
        public float ActionsPerMinute =>
            SessionDuration <= 0f ? 0f : TotalActions / SessionDuration * 60f;

        public double Custom_(string key)
        {
            return Custom.TryGetValue(key, out var value) ? value : 0d;
        }

        public override string ToString()
        {
            return $"{PrototypeId} [{TesterId}] seed={Seed} attempt={AttemptIndex} " +
                   $"status={CompletionStatus} score={Score} obj={ObjectiveProgress:0.00} " +
                   $"dur={SessionDuration:0.00}s firstInput={TimeToFirstInput:0.00}s " +
                   $"firstSuccess={TimeToFirstSuccess:0.00}s inputs={TotalInputs} " +
                   $"ok={SuccessfulActions} fail={FailedActions}";
        }
    }
}
