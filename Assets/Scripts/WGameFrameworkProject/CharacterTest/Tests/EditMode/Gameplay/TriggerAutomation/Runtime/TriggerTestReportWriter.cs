using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MxFramework.Diagnostics;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal static class TriggerTestReportWriter
    {
        private static readonly string RootPath = Path.Combine(
            "Temp",
            "MxFramework",
            "TriggerTests");

        internal static string RootDirectoryPath =>
            Path.GetFullPath(RootPath);

        internal static string GetScenarioDirectoryPath(
            TriggerTestScenarioId scenarioId) =>
            Path.GetFullPath(Path.Combine(RootPath, scenarioId.Value));

        internal static string GetLatestJsonPath(
            TriggerTestScenarioId scenarioId) =>
            Path.Combine(
                GetScenarioDirectoryPath(scenarioId),
                "latest.json");

        internal static string GetLatestMarkdownPath(
            TriggerTestScenarioId scenarioId) =>
            Path.Combine(
                GetScenarioDirectoryPath(scenarioId),
                "latest.md");

        internal static string WriteLatest(TriggerTestRunResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            string directory =
                GetScenarioDirectoryPath(result.Descriptor.ScenarioId);
            Directory.CreateDirectory(directory);
            var report = new FrameworkSimulationReport(
                new[] { result.SimulationResult });
            string markdown = BuildMarkdown(
                result,
                FrameworkSimulationReportFormatter.ToMarkdown(report));
            string json = BuildJson(
                result,
                FrameworkSimulationReportFormatter.ToJson(report));
            File.WriteAllText(
                GetLatestMarkdownPath(result.Descriptor.ScenarioId),
                markdown,
                new UTF8Encoding(false));
            string jsonPath =
                GetLatestJsonPath(result.Descriptor.ScenarioId);
            File.WriteAllText(
                jsonPath,
                json,
                new UTF8Encoding(false));
            return Path.GetFullPath(jsonPath);
        }

        internal static string WriteIndex(
            IReadOnlyList<ITriggerTestScenario> scenarios)
        {
            Directory.CreateDirectory(RootPath);
            var builder = new StringBuilder();
            builder.Append("{\"schema\":\"mx.trigger.test.index.v1\",\"scenarios\":[");
            if (scenarios != null)
            {
                for (int i = 0; i < scenarios.Count; i++)
                {
                    if (i > 0)
                        builder.Append(',');
                    TriggerTestDescriptor descriptor = scenarios[i].Descriptor;
                    builder.Append("{\"id\":\"");
                    AppendJson(builder, descriptor.ScenarioId.Value);
                    builder.Append("\",\"name\":\"");
                    AppendJson(builder, descriptor.DisplayName);
                    builder.Append("\",\"module\":\"");
                    AppendJson(builder, descriptor.Module);
                    builder.Append("\",\"profile\":\"");
                    builder.Append(descriptor.ExecutionProfile);
                    builder.Append("\",\"maxFrames\":");
                    builder.Append(descriptor.MaxFrames);
                    builder.Append(",\"externalChecks\":");
                    builder.Append(descriptor.ExternalChecks.Count);
                    builder.Append('}');
                }
            }
            builder.Append("]}");

            string path = Path.Combine(RootPath, "index.json");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return Path.GetFullPath(path);
        }

        private static string BuildMarkdown(
            TriggerTestRunResult result,
            string simulationMarkdown)
        {
            var builder = new StringBuilder();
            builder.Append("# Trigger Test Verification\n\n");
            builder.Append("scenario: `");
            builder.Append(result.Descriptor.ScenarioId.Value);
            builder.Append("`\n\nstatus: `");
            builder.Append(result.Status);
            builder.Append("`\n\nexternal evidence:\n");
            if (result.ExternalChecks.Count == 0)
            {
                builder.Append("- none\n\n");
            }
            else
            {
                for (int i = 0; i < result.ExternalChecks.Count; i++)
                {
                    TriggerTestExternalCheck check = result.ExternalChecks[i];
                    builder.Append("- [ ] `");
                    builder.Append(check.CheckId);
                    builder.Append("` ");
                    builder.Append(check.Kind);
                    builder.Append(": ");
                    builder.Append(check.ExpectedEvidence);
                    builder.Append('\n');
                }
                builder.Append('\n');
            }

            builder.Append(simulationMarkdown);
            return builder.ToString();
        }

        private static string BuildJson(
            TriggerTestRunResult result,
            string simulationJson)
        {
            var builder = new StringBuilder();
            builder.Append("{\"schema\":\"mx.trigger.test.report.v1\",\"scenarioId\":\"");
            AppendJson(builder, result.Descriptor.ScenarioId.Value);
            builder.Append("\",\"status\":\"");
            builder.Append(result.Status);
            builder.Append("\",\"externalChecks\":[");
            for (int i = 0; i < result.ExternalChecks.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                TriggerTestExternalCheck check = result.ExternalChecks[i];
                builder.Append("{\"id\":\"");
                AppendJson(builder, check.CheckId);
                builder.Append("\",\"kind\":\"");
                builder.Append(check.Kind);
                builder.Append("\",\"capabilityId\":\"");
                AppendJson(builder, check.CapabilityId);
                builder.Append("\",\"expectedEvidence\":\"");
                AppendJson(builder, check.ExpectedEvidence);
                builder.Append("\"}");
            }
            builder.Append("],\"simulation\":");
            builder.Append(simulationJson);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
        }
    }
}
