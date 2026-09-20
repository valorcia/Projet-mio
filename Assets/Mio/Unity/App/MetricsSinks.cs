using System;
using System.Globalization;
using System.IO;
using System.Text;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using UnityEngine;

namespace Mio.Unity.App
{
    /// <summary>Prints each finished session to the console during development.</summary>
    public sealed class ConsoleMetricsSink : IMetricsSink
    {
        public void Submit(MetricReport report)
        {
            Debug.Log("[MIO metrics] " + report);
        }
    }

    /// <summary>
    /// Appends one JSON object per session to a newline-delimited file.
    ///
    /// JSONL rather than a JSON array so a crashed or force-quit session still
    /// leaves every completed session readable, which matters when the whole
    /// point is collecting data off a playtester's phone.
    /// </summary>
    public sealed class JsonlMetricsSink : IMetricsSink
    {
        private readonly string _path;
        private bool _failed;

        public JsonlMetricsSink(string fileName = "mio-metrics.jsonl")
        {
            _path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        }

        /// <summary>Where the log is written.</summary>
        public string FilePath => _path;

        public void Submit(MetricReport report)
        {
            // One failure (read-only storage, full disk) disables the sink for
            // the session. Telemetry must never take gameplay down with it.
            if (_failed) return;

            try
            {
                File.AppendAllText(_path, Serialise(report) + "\n", Encoding.UTF8);
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogWarning($"[MIO metrics] disabled, could not write {_path}: {e.Message}");
            }
        }

        /// <summary>
        /// Hand-rolled rather than JsonUtility: the wire format is snake_case
        /// and carries a nested rewards object, neither of which JsonUtility
        /// can express without a parallel set of DTO classes.
        /// </summary>
        private static string Serialise(MetricReport report)
        {
            var sb = new StringBuilder(384);
            sb.Append('{');

            Text(sb, "session_id", report.SessionId).Append(',');
            Text(sb, "tester_id", report.TesterId).Append(',');
            Text(sb, "prototype_id", report.PrototypeId.ToString()).Append(',');
            Number(sb, "seed", report.Seed).Append(',');
            Text(sb, "session_start", Iso(report.SessionStartUtc)).Append(',');
            Text(sb, "session_end", Iso(report.SessionEndUtc)).Append(',');
            Number(sb, "session_duration", report.SessionDuration).Append(',');
            Number(sb, "time_to_first_input", report.TimeToFirstInput).Append(',');
            Number(sb, "time_to_first_success", report.TimeToFirstSuccess).Append(',');
            Number(sb, "total_inputs", report.TotalInputs).Append(',');
            Number(sb, "successful_actions", report.SuccessfulActions).Append(',');
            Number(sb, "failed_actions", report.FailedActions).Append(',');
            Number(sb, "score", report.Score).Append(',');
            Number(sb, "objective_progress", report.ObjectiveProgress).Append(',');
            Bool(sb, "objective_completed", report.ObjectiveCompleted).Append(',');
            Text(sb, "completion_status", report.CompletionStatus.ToString()).Append(',');
            Bool(sb, "replay_requested", report.ReplayRequested).Append(',');
            Bool(sb, "replay_without_prompt", report.ReplayWithoutPrompt).Append(',');
            Number(sb, "attempt_index", report.AttemptIndex).Append(',');

            sb.Append("\"rewards\":{");
            var first = true;
            foreach (var kind in ResourceKinds.All)
            {
                report.Rewards.TryGetValue(kind, out var amount);
                if (!first) sb.Append(',');
                first = false;
                Number(sb, kind.ToString().ToLowerInvariant(), amount);
            }

            sb.Append("},\"resources_earned\":{");
            first = true;
            foreach (var kind in ResourceKinds.All)
            {
                if (!first) sb.Append(',');
                first = false;
                Number(sb, kind.ToString().ToLowerInvariant(), report.ResourcesEarned[kind]);
            }

            sb.Append("},\"custom\":{");
            first = true;
            foreach (var pair in report.Custom)
            {
                if (!first) sb.Append(',');
                first = false;
                Number(sb, pair.Key, pair.Value);
            }

            sb.Append("}}");
            return sb.ToString();
        }

        private static string Iso(DateTime value) =>
            value.ToString("o", CultureInfo.InvariantCulture);

        private static StringBuilder Text(StringBuilder sb, string key, string value)
        {
            return sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static StringBuilder Number(StringBuilder sb, string key, double value)
        {
            // Invariant culture: a French-locale device must not emit 1,25 and
            // break every parser downstream.
            return sb.Append('"').Append(Escape(key)).Append("\":")
                     .Append(value.ToString("0.####", CultureInfo.InvariantCulture));
        }

        private static StringBuilder Bool(StringBuilder sb, string key, bool value)
        {
            return sb.Append('"').Append(Escape(key)).Append("\":").Append(value ? "true" : "false");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
