using MxFramework.Diagnostics;
using NUnit.Framework;

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
    }
}
