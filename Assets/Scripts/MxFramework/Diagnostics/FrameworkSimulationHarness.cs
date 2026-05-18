using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Diagnostics
{
    public readonly struct FrameworkSimulationMetric
    {
        public FrameworkSimulationMetric(string category, string name, long value, string unit = "count")
        {
            Category = string.IsNullOrWhiteSpace(category) ? "runtime" : category;
            Name = string.IsNullOrWhiteSpace(name) ? "metric" : name;
            Value = value;
            Unit = string.IsNullOrWhiteSpace(unit) ? "count" : unit;
        }

        public string Category { get; }
        public string Name { get; }
        public long Value { get; }
        public string Unit { get; }
    }

    public readonly struct FrameworkSimulationTimelineEvent
    {
        public FrameworkSimulationTimelineEvent(
            long frame,
            string source,
            string category,
            string entityId,
            string traceId,
            string summary)
        {
            if (frame < 0L)
                throw new ArgumentOutOfRangeException(nameof(frame), "Simulation event frame cannot be negative.");

            Frame = frame;
            Source = source ?? string.Empty;
            Category = category ?? string.Empty;
            EntityId = entityId ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            Summary = summary ?? string.Empty;
        }

        public long Frame { get; }
        public string Source { get; }
        public string Category { get; }
        public string EntityId { get; }
        public string TraceId { get; }
        public string Summary { get; }
    }

    public readonly struct FrameworkSimulationFailure
    {
        public FrameworkSimulationFailure(
            long frame,
            string entityId,
            string traceId,
            string message,
            string diagnosticsSummary)
        {
            if (frame < 0L)
                throw new ArgumentOutOfRangeException(nameof(frame), "Simulation failure frame cannot be negative.");

            Frame = frame;
            EntityId = entityId ?? string.Empty;
            TraceId = traceId ?? string.Empty;
            Message = message ?? string.Empty;
            DiagnosticsSummary = diagnosticsSummary ?? string.Empty;
        }

        public long Frame { get; }
        public string EntityId { get; }
        public string TraceId { get; }
        public string Message { get; }
        public string DiagnosticsSummary { get; }
    }

    public sealed class FrameworkSimulationScenarioResult
    {
        private readonly List<FrameworkSimulationMetric> _metrics;
        private readonly List<FrameworkSimulationTimelineEvent> _events;
        private readonly List<FrameworkSimulationFailure> _failures;

        public FrameworkSimulationScenarioResult(
            string scenarioName,
            int frameCount,
            IReadOnlyList<FrameworkSimulationMetric> metrics,
            IReadOnlyList<FrameworkSimulationTimelineEvent> events,
            IReadOnlyList<FrameworkSimulationFailure> failures)
        {
            if (frameCount < 0)
                throw new ArgumentOutOfRangeException(nameof(frameCount), "Simulation frame count cannot be negative.");

            ScenarioName = string.IsNullOrWhiteSpace(scenarioName) ? "Scenario" : scenarioName;
            FrameCount = frameCount;
            _metrics = metrics != null ? new List<FrameworkSimulationMetric>(metrics) : new List<FrameworkSimulationMetric>();
            _events = events != null ? new List<FrameworkSimulationTimelineEvent>(events) : new List<FrameworkSimulationTimelineEvent>();
            _failures = failures != null ? new List<FrameworkSimulationFailure>(failures) : new List<FrameworkSimulationFailure>();
            _events.Sort(CompareEvents);
        }

        public string ScenarioName { get; }
        public int FrameCount { get; }
        public bool Success => _failures.Count == 0;
        public IReadOnlyList<FrameworkSimulationMetric> Metrics => _metrics;
        public IReadOnlyList<FrameworkSimulationTimelineEvent> Events => _events;
        public IReadOnlyList<FrameworkSimulationFailure> Failures => _failures;

        private static int CompareEvents(
            FrameworkSimulationTimelineEvent left,
            FrameworkSimulationTimelineEvent right)
        {
            int frame = left.Frame.CompareTo(right.Frame);
            if (frame != 0)
                return frame;

            int source = string.Compare(left.Source, right.Source, StringComparison.Ordinal);
            if (source != 0)
                return source;

            return string.Compare(left.TraceId, right.TraceId, StringComparison.Ordinal);
        }
    }

    public interface IFrameworkSimulationScenario
    {
        string Name { get; }
        FrameworkSimulationScenarioResult Run();
    }

    public sealed class DelegateFrameworkSimulationScenario : IFrameworkSimulationScenario
    {
        private readonly Func<FrameworkSimulationScenarioResult> _runner;

        public DelegateFrameworkSimulationScenario(string name, Func<FrameworkSimulationScenarioResult> runner)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Scenario" : name;
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public string Name { get; }

        public FrameworkSimulationScenarioResult Run()
        {
            return _runner();
        }
    }

    public sealed class FrameworkSimulationReport
    {
        private readonly List<FrameworkSimulationScenarioResult> _scenarios;

        public FrameworkSimulationReport(IReadOnlyList<FrameworkSimulationScenarioResult> scenarios)
        {
            _scenarios = scenarios != null
                ? new List<FrameworkSimulationScenarioResult>(scenarios)
                : new List<FrameworkSimulationScenarioResult>();
        }

        public IReadOnlyList<FrameworkSimulationScenarioResult> Scenarios => _scenarios;
        public int ScenarioCount => _scenarios.Count;
        public bool Success
        {
            get
            {
                for (int i = 0; i < _scenarios.Count; i++)
                {
                    if (!_scenarios[i].Success)
                        return false;
                }

                return true;
            }
        }
    }

    public sealed class FrameworkSimulationBatchRunner
    {
        public FrameworkSimulationReport Run(IReadOnlyList<IFrameworkSimulationScenario> scenarios)
        {
            var results = new List<FrameworkSimulationScenarioResult>();
            if (scenarios == null)
                return new FrameworkSimulationReport(results);

            for (int i = 0; i < scenarios.Count; i++)
            {
                IFrameworkSimulationScenario scenario = scenarios[i];
                if (scenario == null)
                    continue;

                try
                {
                    FrameworkSimulationScenarioResult result = scenario.Run();
                    results.Add(result ?? CreateFailure(scenario.Name, "Scenario returned no result."));
                }
                catch (Exception exception)
                {
                    results.Add(CreateFailure(scenario.Name, exception.Message));
                }
            }

            return new FrameworkSimulationReport(results);
        }

        private static FrameworkSimulationScenarioResult CreateFailure(string scenarioName, string message)
        {
            return new FrameworkSimulationScenarioResult(
                scenarioName,
                0,
                Array.Empty<FrameworkSimulationMetric>(),
                Array.Empty<FrameworkSimulationTimelineEvent>(),
                new[] { new FrameworkSimulationFailure(0, string.Empty, string.Empty, message, string.Empty) });
        }
    }

    public sealed class FrameworkSimulationReportDebugSource : IFrameworkDebugSource
    {
        private readonly Func<FrameworkSimulationReport> _reportFactory;

        public FrameworkSimulationReportDebugSource(
            Func<FrameworkSimulationReport> reportFactory,
            string name = "SimulationHarness")
        {
            _reportFactory = reportFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "SimulationHarness" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _reportFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            FrameworkSimulationReport report = _reportFactory != null ? _reportFactory() : null;
            if (report == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "report unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", "success: " + (report.Success ? "true" : "false") + "\nscenarios: " + report.ScenarioCount),
                    new FrameworkDebugSection("Simulation Report", FrameworkSimulationReportFormatter.ToMarkdown(report))
                });
        }
    }

    public static class FrameworkSimulationReportFormatter
    {
        public static string ToMarkdown(FrameworkSimulationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var builder = new StringBuilder();
            builder.Append("# Simulation Report\n\n");
            builder.Append("success: ").Append(report.Success ? "true" : "false").Append('\n');
            builder.Append("scenarios: ").Append(report.ScenarioCount).Append('\n');
            for (int i = 0; i < report.Scenarios.Count; i++)
            {
                FrameworkSimulationScenarioResult scenario = report.Scenarios[i];
                builder.Append("\n## ").Append(scenario.ScenarioName).Append('\n');
                builder.Append("success: ").Append(scenario.Success ? "true" : "false").Append('\n');
                builder.Append("frames: ").Append(scenario.FrameCount).Append('\n');
                builder.Append("metrics:\n");
                AppendMetrics(builder, scenario.Metrics);
                builder.Append("events:\n");
                AppendEvents(builder, scenario.Events);
                builder.Append("failures:\n");
                AppendFailures(builder, scenario.Failures);
            }

            return builder.ToString();
        }

        public static string ToJson(FrameworkSimulationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var builder = new StringBuilder();
            builder.Append('{');
            builder.Append("\"success\":").Append(report.Success ? "true" : "false");
            builder.Append(",\"scenarioCount\":").Append(report.ScenarioCount);
            builder.Append(",\"scenarios\":[");
            for (int i = 0; i < report.Scenarios.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AppendScenarioJson(builder, report.Scenarios[i]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private static void AppendMetrics(StringBuilder builder, IReadOnlyList<FrameworkSimulationMetric> metrics)
        {
            if (metrics == null || metrics.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < metrics.Count; i++)
            {
                FrameworkSimulationMetric metric = metrics[i];
                builder.Append("- ")
                    .Append(metric.Category)
                    .Append('/')
                    .Append(metric.Name)
                    .Append(": ")
                    .Append(metric.Value)
                    .Append(' ')
                    .Append(metric.Unit)
                    .Append('\n');
            }
        }

        private static void AppendEvents(StringBuilder builder, IReadOnlyList<FrameworkSimulationTimelineEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                FrameworkSimulationTimelineEvent evt = events[i];
                builder.Append("- frame=")
                    .Append(evt.Frame)
                    .Append(" source=")
                    .Append(evt.Source)
                    .Append(" category=")
                    .Append(evt.Category)
                    .Append(" entity=")
                    .Append(evt.EntityId)
                    .Append(" trace=")
                    .Append(evt.TraceId)
                    .Append(" ")
                    .Append(evt.Summary)
                    .Append('\n');
            }
        }

        private static void AppendFailures(StringBuilder builder, IReadOnlyList<FrameworkSimulationFailure> failures)
        {
            if (failures == null || failures.Count == 0)
            {
                builder.Append("- none\n");
                return;
            }

            for (int i = 0; i < failures.Count; i++)
            {
                FrameworkSimulationFailure failure = failures[i];
                builder.Append("- frame=")
                    .Append(failure.Frame)
                    .Append(" entity=")
                    .Append(failure.EntityId)
                    .Append(" trace=")
                    .Append(failure.TraceId)
                    .Append(" message=")
                    .Append(failure.Message)
                    .Append(" diagnostics=")
                    .Append(failure.DiagnosticsSummary)
                    .Append('\n');
            }
        }

        private static void AppendScenarioJson(StringBuilder builder, FrameworkSimulationScenarioResult scenario)
        {
            builder.Append('{');
            AppendJsonProperty(builder, "name", scenario.ScenarioName);
            builder.Append(",\"success\":").Append(scenario.Success ? "true" : "false");
            builder.Append(",\"frames\":").Append(scenario.FrameCount);
            builder.Append(",\"metrics\":[");
            for (int i = 0; i < scenario.Metrics.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                FrameworkSimulationMetric metric = scenario.Metrics[i];
                builder.Append('{');
                AppendJsonProperty(builder, "category", metric.Category);
                builder.Append(',');
                AppendJsonProperty(builder, "name", metric.Name);
                builder.Append(",\"value\":").Append(metric.Value);
                builder.Append(',');
                AppendJsonProperty(builder, "unit", metric.Unit);
                builder.Append('}');
            }

            builder.Append("],\"events\":[");
            for (int i = 0; i < scenario.Events.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                FrameworkSimulationTimelineEvent evt = scenario.Events[i];
                builder.Append('{');
                builder.Append("\"frame\":").Append(evt.Frame).Append(',');
                AppendJsonProperty(builder, "source", evt.Source);
                builder.Append(',');
                AppendJsonProperty(builder, "category", evt.Category);
                builder.Append(',');
                AppendJsonProperty(builder, "entity", evt.EntityId);
                builder.Append(',');
                AppendJsonProperty(builder, "traceId", evt.TraceId);
                builder.Append(',');
                AppendJsonProperty(builder, "summary", evt.Summary);
                builder.Append('}');
            }

            builder.Append("],\"failures\":[");
            for (int i = 0; i < scenario.Failures.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                FrameworkSimulationFailure failure = scenario.Failures[i];
                builder.Append('{');
                builder.Append("\"frame\":").Append(failure.Frame).Append(',');
                AppendJsonProperty(builder, "entity", failure.EntityId);
                builder.Append(',');
                AppendJsonProperty(builder, "traceId", failure.TraceId);
                builder.Append(',');
                AppendJsonProperty(builder, "message", failure.Message);
                builder.Append(',');
                AppendJsonProperty(builder, "diagnosticsSummary", failure.DiagnosticsSummary);
                builder.Append('}');
            }

            builder.Append("]}");
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value)
        {
            builder.Append('"').Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value)).Append('"');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    default:
                        if (ch < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
