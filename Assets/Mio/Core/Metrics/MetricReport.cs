using System;
using System.Collections.Generic;
using Mio.Core.Economy;
using Mio.Core.Session;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// One completed session. This is the unit shipped to analytics and the
    /// unit compared when A/B testing a tuning change.
    ///
    /// Field names here are C#; the wire names emitted by the JSONL sink are
    /// the snake_case ones given in the M0.1 spec.
    /// </summary>
    public sealed class MetricReport
    {
        /// <summary>Unique per session. Lets replays of one sitting be grouped.</summary>
        public string SessionId = string.Empty;

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
        /// Seconds from session start to the first touch. Negative means the
        /// player never touched the screen, which is the signal that the
        /// prototype failed to communicate its controls.
        /// </summary>
        public float TimeToFirstInput;

        public bool HadInput => TimeToFirstInput >= 0f;

        /// <summary>Distinct touches. See SessionMetricsRecorder for the definition.</summary>
        public int InputCount;

        public int SuccessfulActions;

        public int FailedActions;

        public int Score;

        /// <summary>Win-condition fill at the moment the session resolved, 0..1.</summary>
        public float Progress;

        public SessionStatus CompletionStatus;

        public bool Completed => CompletionStatus == SessionStatus.Won;

        /// <summary>
        /// True when this session was started by an explicit replay request.
        /// The first session of a sitting is false.
        /// </summary>
        public bool ReplayRequested;

        /// <summary>0 for the first session of a sitting, 1+ for each replay.</summary>
        public int AttemptIndex;

        /// <summary>Resources granted for this session, by resource id.</summary>
        public readonly Dictionary<ResourceKind, int> Rewards = new Dictionary<ResourceKind, int>();

        public int TotalActions => SuccessfulActions + FailedActions;

        public float SuccessRate =>
            TotalActions == 0 ? 0f : SuccessfulActions / (float)TotalActions;

        public override string ToString()
        {
            return $"{PrototypeId} seed={Seed} attempt={AttemptIndex} status={CompletionStatus} " +
                   $"score={Score} progress={Progress:0.00} dur={SessionDuration:0.00}s " +
                   $"firstInput={TimeToFirstInput:0.00}s inputs={InputCount} " +
                   $"ok={SuccessfulActions} fail={FailedActions}";
        }
    }
}
