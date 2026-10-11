using MxFramework.Diagnostics;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Diagnostics
{
    public sealed class FrameworkSimulationHarnessTests
    {
        [Test]
        public void BatchRunner_CollectsScenarioMetricsAndReportsMarkdown()
        {
            var scenario = new DelegateFrameworkSimulationScenario(
                "pressure-combat",
                () => new FrameworkSimulationScenarioResult(
                    "pressure-combat",
                    3,
                    new[]
                    {
                        new FrameworkSimulationMetric("pressure", "bandChanges", 2),
                        new FrameworkSimulationMetric("combat", "hitResolves", 1)
                    },
                    new[]
                    {
                        new FrameworkSimulationTimelineEvent(1, "Gameplay", "Pressure", "1:1", "trace-a", "Stable->Pressed"),
                        new FrameworkSimulationTimelineEvent(2, "Combat", "Hit", "2", "trace-b", "Damage")
                    },
                    null));

            FrameworkSimulationReport report = new FrameworkSimulationBatchRunner().Run(new[] { scenario });
            string markdown = FrameworkSimulationReportFormatter.ToMarkdown(report);
            string json = FrameworkSimulationReportFormatter.ToJson(report);

            Assert.IsTrue(report.Success);
            Assert.That(markdown, Does.Contain("pressure/bandChanges"));
            Assert.That(markdown, Does.Contain("combat/hitResolves"));
            Assert.That(json, Does.Contain("\"scenarioCount\":1"));
            Assert.That(json, Does.Contain("\"traceId\":\"trace-a\""));
        }

        [Test]
        public void BatchRunner_CapturesScenarioExceptionAsFailure()
        {
            var scenario = new DelegateFrameworkSimulationScenario(
                "broken",
                () => throw new System.InvalidOperationException("failed"));

            FrameworkSimulationReport report = new FrameworkSimulationBatchRunner().Run(new[] { scenario });

            Assert.IsFalse(report.Success);
            Assert.AreEqual("failed", report.Scenarios[0].Failures[0].Message);
        }

        [Test]
        public void ToJson_EscapesControlCharactersAndParsesBack()
        {
            const string controlText = "quote\" slash\\ backspace\b formfeed\f nul\0 newline\n";
            var report = new FrameworkSimulationReport(new[]
            {
                new FrameworkSimulationScenarioResult(
                    controlText,
                    1,
                    new[] { new FrameworkSimulationMetric("cat\b", "metric\f", 7, "u\0") },
                    new[]
                    {
                        new FrameworkSimulationTimelineEvent(1, "src\0", "cat\n", "entity\t", "trace\r", controlText)
                    },
                    new[]
                    {
                        new FrameworkSimulationFailure(1, "entity\b", "trace\f", controlText, "diag\0")
                    })
            });

            string json = FrameworkSimulationReportFormatter.ToJson(report);
            ParsedSimulationReport parsed = JsonUtility.FromJson<ParsedSimulationReport>(json);
            string jsonRoundtripText = controlText.Replace('\0', '\u2400');

            Assert.That(json, Does.Contain("\u2400"));
            Assert.That(json, Does.Contain("\\u0008"));
            Assert.That(json, Does.Contain("\\u000c"));
            Assert.AreEqual(1, parsed.scenarioCount);
            Assert.AreEqual(jsonRoundtripText, parsed.scenarios[0].name);
            Assert.AreEqual(jsonRoundtripText, parsed.scenarios[0].events[0].summary);
            Assert.AreEqual(jsonRoundtripText, parsed.scenarios[0].failures[0].message);
        }

        [Test]
        public void ReportDebugSource_ExportsMarkdownReport()
        {
            var report = new FrameworkSimulationReport(new[]
            {
                new FrameworkSimulationScenarioResult(
                    "smoke",
                    1,
                    null,
                    null,
                    null)
            });

            FrameworkDebugSnapshot snapshot = new FrameworkSimulationReportDebugSource(() => report).CreateSnapshot();

            Assert.AreEqual("SimulationHarness", snapshot.SourceName);
            Assert.That(snapshot.Sections[0].Body, Does.Contain("success: true"));
            Assert.That(snapshot.Sections[1].Body, Does.Contain("# Simulation Report"));
        }

#pragma warning disable 0649
        [System.Serializable]
        private sealed class ParsedSimulationReport
        {
            public bool success;
            public int scenarioCount;
            public ParsedSimulationScenario[] scenarios;
        }

        [System.Serializable]
        private sealed class ParsedSimulationScenario
        {
            public string name;
            public bool success;
            public int frames;
            public ParsedSimulationMetric[] metrics;
            public ParsedSimulationEvent[] events;
            public ParsedSimulationFailure[] failures;
        }

        [System.Serializable]
        private sealed class ParsedSimulationMetric
        {
            public string category;
            public string name;
            public int value;
            public string unit;
        }

        [System.Serializable]
        private sealed class ParsedSimulationEvent
        {
            public int frame;
            public string source;
            public string category;
            public string entity;
            public string traceId;
            public string summary;
        }

        [System.Serializable]
        private sealed class ParsedSimulationFailure
        {
            public int frame;
            public string entity;
            public string traceId;
            public string message;
            public string diagnosticsSummary;
        }
#pragma warning restore 0649
    }
}
