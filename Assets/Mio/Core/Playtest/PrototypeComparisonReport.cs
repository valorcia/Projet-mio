using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Mio.Core.Metrics;
using Mio.Core.Session;

namespace Mio.Core.Playtest
{
    /// <summary>Everything M0.2 compares, for one prototype.</summary>
    public sealed class PrototypeSummary
    {
        public PrototypeId PrototypeId;

        public int SessionsPlayed;
        public int SessionsCompleted;

        /// <summary>Share of sessions where the objective was met, 0..1.</summary>
        public float CompletionRate;

        public double AverageScore;
        public int BestScore;

        /// <summary>Seconds, averaged over sessions where it happened at all.</summary>
        public double AverageTimeToFirstInput;
        public double AverageTimeToFirstSuccess;

        /// <summary>Sessions where the player never managed a successful action.</summary>
        public int SessionsWithoutSuccess;

        public double AverageActionsPerMinute;

        /// <summary>Failed actions over total actions, 0..1.</summary>
        public float FailureRate;

        /// <summary>Share of sessions that were a replay of any kind, 0..1.</summary>
        public float ReplayRate;

        /// <summary>
        /// Share of sessions started by the player reaching for a board that
        /// offered nothing. The "one more" signal.
        /// </summary>
        public float ReplayWithoutPromptRate;

        public double AverageSessionsPerTester;
        public int TesterCount;

        /// <summary>Seconds to meet the objective, over sessions that met it.</summary>
        public double AverageObjectiveSeconds;

        /// <summary>Largest combination or cascade seen, where the prototype reports one.</summary>
        public double LargestCombo;

        public double AverageSessionSeconds;
    }

    /// <summary>
    /// Aggregates metric reports into a side-by-side view of the four
    /// candidates.
    ///
    /// It deliberately produces <b>no winner</b>. The numbers say which
    /// prototype was replayed and which was understood fastest; they cannot
    /// say which one PROJECT MIO should be. That call is a human reading these
    /// alongside what testers said and how they looked while playing.
    /// </summary>
    public sealed class PrototypeComparisonReport
    {
        private readonly Dictionary<PrototypeId, PrototypeSummary> _summaries =
            new Dictionary<PrototypeId, PrototypeSummary>();

        public IReadOnlyDictionary<PrototypeId, PrototypeSummary> Summaries => _summaries;

        public int TotalSessions { get; private set; }

        public PrototypeSummary For(PrototypeId id) =>
            _summaries.TryGetValue(id, out var s) ? s : null;

        /// <summary>Keys a prototype's "largest combo" onto its own custom metric.</summary>
        private static readonly Dictionary<PrototypeId, string> ComboKeys =
            new Dictionary<PrototypeId, string>
            {
                { PrototypeId.Stack, "stack.maxCascade" },
                { PrototypeId.MioMix, "miomix.maxCascade" },
                { PrototypeId.Pop, "pop.largestGroup" },
                { PrototypeId.MergeFactory, "merge.highestTier" }
            };

        private static readonly Dictionary<PrototypeId, string> ObjectiveKeys =
            new Dictionary<PrototypeId, string>
            {
                { PrototypeId.Stack, "stack.objectiveCompletedAt" },
                { PrototypeId.MioMix, "miomix.objectiveCompletedAt" },
                { PrototypeId.Pop, "pop.objectiveCompletedAt" },
                { PrototypeId.MergeFactory, "merge.objectiveCompletedAt" }
            };

        public static PrototypeComparisonReport Build(IEnumerable<MetricReport> reports)
        {
            var report = new PrototypeComparisonReport();
            if (reports == null) return report;

            var groups = new Dictionary<PrototypeId, List<MetricReport>>();

            foreach (var r in reports)
            {
                if (r == null) continue;

                // An abandoned session says nothing about the design, only that
                // someone was interrupted, so it never enters the comparison.
                if (r.CompletionStatus == SessionStatus.Abandoned) continue;

                if (!groups.TryGetValue(r.PrototypeId, out var list))
                {
                    list = new List<MetricReport>();
                    groups[r.PrototypeId] = list;
                }

                list.Add(r);
                report.TotalSessions++;
            }

            foreach (var pair in groups)
            {
                report._summaries[pair.Key] = Summarise(pair.Key, pair.Value);
            }

            return report;
        }

        private static PrototypeSummary Summarise(PrototypeId id, List<MetricReport> rows)
        {
            var s = new PrototypeSummary { PrototypeId = id, SessionsPlayed = rows.Count };

            double score = 0, apm = 0, seconds = 0;
            double firstInput = 0, firstSuccess = 0;
            var firstInputCount = 0;
            var firstSuccessCount = 0;

            long okActions = 0, failActions = 0;
            var replays = 0;
            var unprompted = 0;

            double objectiveSeconds = 0;
            var objectiveCount = 0;
            double largestCombo = 0;

            var testers = new HashSet<string>(StringComparer.Ordinal);

            ComboKeys.TryGetValue(id, out var comboKey);
            ObjectiveKeys.TryGetValue(id, out var objectiveKey);

            foreach (var r in rows)
            {
                score += r.Score;
                if (r.Score > s.BestScore) s.BestScore = r.Score;

                seconds += r.SessionDuration;
                apm += r.ActionsPerMinute;

                okActions += r.SuccessfulActions;
                failActions += r.FailedActions;

                if (r.ObjectiveCompleted) s.SessionsCompleted++;
                if (r.ReplayRequested || r.ReplayWithoutPrompt) replays++;
                if (r.ReplayWithoutPrompt) unprompted++;

                // Averaging in a -1 for a session that never got there would
                // quietly drag the mean below zero and hide the real figure.
                if (r.HadInput) { firstInput += r.TimeToFirstInput; firstInputCount++; }

                if (r.HadSuccess) { firstSuccess += r.TimeToFirstSuccess; firstSuccessCount++; }
                else s.SessionsWithoutSuccess++;

                if (!string.IsNullOrEmpty(r.TesterId)) testers.Add(r.TesterId);

                if (comboKey != null)
                {
                    var combo = r.Custom_(comboKey);
                    if (combo > largestCombo) largestCombo = combo;
                }

                if (objectiveKey != null)
                {
                    var at = r.Custom_(objectiveKey);
                    if (at >= 0d) { objectiveSeconds += at; objectiveCount++; }
                }
            }

            var n = rows.Count;
            s.CompletionRate = n == 0 ? 0f : s.SessionsCompleted / (float)n;
            s.AverageScore = n == 0 ? 0d : score / n;
            s.AverageSessionSeconds = n == 0 ? 0d : seconds / n;
            s.AverageActionsPerMinute = n == 0 ? 0d : apm / n;

            var totalActions = okActions + failActions;
            s.FailureRate = totalActions == 0 ? 0f : failActions / (float)totalActions;

            s.ReplayRate = n == 0 ? 0f : replays / (float)n;
            s.ReplayWithoutPromptRate = n == 0 ? 0f : unprompted / (float)n;

            s.AverageTimeToFirstInput = firstInputCount == 0 ? -1d : firstInput / firstInputCount;
            s.AverageTimeToFirstSuccess = firstSuccessCount == 0 ? -1d : firstSuccess / firstSuccessCount;

            s.TesterCount = testers.Count;
            s.AverageSessionsPerTester = testers.Count == 0 ? n : n / (double)testers.Count;

            s.AverageObjectiveSeconds = objectiveCount == 0 ? -1d : objectiveSeconds / objectiveCount;
            s.LargestCombo = largestCombo;

            return s;
        }

        /// <summary>A fixed-width table for the console. No winner is marked.</summary>
        public string ToTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROJECT MIO — PROTOTYPE COMPARISON");
            sb.AppendLine("==================================");
            sb.AppendLine($"{TotalSessions} session(s) compared. No winner is computed; read these together.");
            sb.AppendLine();

            sb.AppendLine(Row("metric", "STACK", "MERGE", "POP", "MIO MIX"));
            sb.AppendLine(new string('-', 62));

            sb.AppendLine(Row("sessions", Pick(s => s.SessionsPlayed.ToString())));
            sb.AppendLine(Row("completion rate", Pick(s => Pct(s.CompletionRate))));
            sb.AppendLine(Row("avg score", Pick(s => s.AverageScore.ToString("0", Inv))));
            sb.AppendLine(Row("first input (s)", Pick(s => Secs(s.AverageTimeToFirstInput))));
            sb.AppendLine(Row("first success (s)", Pick(s => Secs(s.AverageTimeToFirstSuccess))));
            sb.AppendLine(Row("actions / min", Pick(s => s.AverageActionsPerMinute.ToString("0.0", Inv))));
            sb.AppendLine(Row("failure rate", Pick(s => Pct(s.FailureRate))));
            sb.AppendLine(Row("replay rate", Pick(s => Pct(s.ReplayRate))));
            sb.AppendLine(Row("one-more rate", Pick(s => Pct(s.ReplayWithoutPromptRate))));
            sb.AppendLine(Row("sessions / tester", Pick(s => s.AverageSessionsPerTester.ToString("0.0", Inv))));
            sb.AppendLine(Row("objective (s)", Pick(s => Secs(s.AverageObjectiveSeconds))));
            sb.AppendLine(Row("largest combo", Pick(s => s.LargestCombo.ToString("0", Inv))));

            return sb.ToString();
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static string Pct(float v) => (v * 100f).ToString("0", Inv) + "%";

        private static string Secs(double v) => v < 0d ? "—" : v.ToString("0.0", Inv);

        private string[] Pick(Func<PrototypeSummary, string> select)
        {
            var values = new string[PrototypeIds.Candidates.Length];
            for (var i = 0; i < values.Length; i++)
            {
                var summary = For(PrototypeIds.Candidates[i]);
                values[i] = summary == null ? "—" : select(summary);
            }

            return values;
        }

        private static string Row(string label, string[] values)
        {
            return Row(label, values[0], values[1], values[2], values[3]);
        }

        private static string Row(string label, string a, string b, string c, string d)
        {
            return label.PadRight(20) + a.PadLeft(10) + b.PadLeft(10) + c.PadLeft(10) + d.PadLeft(10);
        }
    }
}
