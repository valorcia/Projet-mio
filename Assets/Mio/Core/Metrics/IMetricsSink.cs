using System.Collections.Generic;

namespace Mio.Core.Metrics
{
    /// <summary>
    /// Destination for finished attempts. Gameplay only ever sees this
    /// interface, so swapping console logging for a real analytics backend
    /// later touches no gameplay code.
    /// </summary>
    public interface IMetricsSink
    {
        void Submit(MetricReport report);
    }

    /// <summary>Discards everything. Useful as a default and in tests.</summary>
    public sealed class NullMetricsSink : IMetricsSink
    {
        public static readonly NullMetricsSink Instance = new NullMetricsSink();
        public void Submit(MetricReport report) { }
    }

    /// <summary>Keeps reports in memory. Used by tests and the in-editor HUD.</summary>
    public sealed class InMemoryMetricsSink : IMetricsSink
    {
        private readonly List<MetricReport> _reports = new List<MetricReport>();

        public IReadOnlyList<MetricReport> Reports => _reports;
        public MetricReport Last => _reports.Count == 0 ? null : _reports[_reports.Count - 1];

        public void Submit(MetricReport report) => _reports.Add(report);
        public void Clear() => _reports.Clear();
    }

    /// <summary>Forwards to several sinks, e.g. console plus file.</summary>
    public sealed class CompositeMetricsSink : IMetricsSink
    {
        private readonly IMetricsSink[] _sinks;

        public CompositeMetricsSink(params IMetricsSink[] sinks)
        {
            _sinks = sinks ?? new IMetricsSink[0];
        }

        public void Submit(MetricReport report)
        {
            for (var i = 0; i < _sinks.Length; i++)
            {
                _sinks[i]?.Submit(report);
            }
        }
    }
}
