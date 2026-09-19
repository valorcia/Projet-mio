using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Mio.Core.Economy;
using Mio.Core.Metrics;
using UnityEngine;

namespace Mio.Unity.App
{
    /// <summary>Prints each finished attempt to the console during development.</summary>
    public sealed class ConsoleMetricsSink : IMetricsSink
    {
        public void Submit(MetricReport report)
        {
            Debug.Log("[MIO metrics] " + report);
        }
    }

    /// <summary>
    /// Appends one JSON object per attempt to a newline-delimited file.
    ///
    /// JSONL rather than a JSON array so a crashed or force-quit session still
    /// leaves every completed attempt readable, which matters when the whole
    /// point is collecting data off a playtester's phone.
    /// </summary>
    public sealed class JsonFileMetricsSink : IMetricsSink
    {
        private readonly string _path;
        private bool _failed;

        public JsonFileMetricsSink(string fileName = "mio-metrics.jsonl")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        /// <summary>Where the log is written. Not named Path: that would shadow System.IO.Path.</summary>
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

        private static string Serialise(MetricReport report)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');

            Text(sb, "ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(',');
            Text(sb, "prototype", report.Prototype.ToString()).Append(',');
            Number(sb, "seed", report.Seed).Append(',');
            Number(sb, "attempt", report.AttemptIndex).Append(',');
            Bool(sb, "replay", report.IsReplay).Append(',');
            Number(sb, "durationSec", report.SessionDuration).Append(',');
            Number(sb, "firstInteractionSec", report.FirstInteractionTime).Append(',');
            Number(sb, "successfulActions", report.SuccessfulActions).Append(',');
            Number(sb, "failedActions", report.FailedActions).Append(',');
            Bool(sb, "completed", report.Completed).Append(',');
            Text(sb, "status", report.Status.ToString()).Append(',');
            Number(sb, "score", report.Score).Append(',');

            sb.Append("\"rewards\":{");
            var firstReward = true;
            foreach (var kind in ResourceKinds.All)
            {
                report.Rewards.TryGetValue(kind, out var amount);
                if (!firstReward) sb.Append(',');
                firstReward = false;
                Number(sb, kind.ToString(), amount);
            }

            sb.Append("},\"custom\":{");
            var firstCustom = true;
            foreach (var pair in report.Custom)
            {
                if (!firstCustom) sb.Append(',');
                firstCustom = false;
                Number(sb, pair.Key, pair.Value);
            }

            sb.Append("}}");
            return sb.ToString();
        }

        private static StringBuilder Text(StringBuilder sb, string key, string value)
        {
            return sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private static StringBuilder Number(StringBuilder sb, string key, double value)
        {
            // Invariant culture: a French-locale device must not emit 1,25.
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

    /// <summary>
    /// Keeps the last few attempts so the in-game debug overlay can show how
    /// this session is trending without reading the file back.
    /// </summary>
    public sealed class RecentAttemptsSink : IMetricsSink
    {
        private readonly Queue<MetricReport> _recent = new Queue<MetricReport>();
        private readonly int _capacity;

        public RecentAttemptsSink(int capacity = 8)
        {
            _capacity = Mathf.Max(1, capacity);
        }

        public IEnumerable<MetricReport> Recent => _recent;
        public int Count => _recent.Count;

        public void Submit(MetricReport report)
        {
            _recent.Enqueue(report);
            while (_recent.Count > _capacity) _recent.Dequeue();
        }
    }
}
