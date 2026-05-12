using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeGoldenReplayHarnessTests
    {
        [Test]
        public void GoldenReplayFixture_BuildsReplaySnapshot()
        {
            RuntimeGoldenReplayFixture fixture = RuntimeGoldenReplayFixtures.SyntheticCounter();

            RuntimeReplaySnapshot snapshot = fixture.CreateSnapshot();

            Assert.AreEqual("synthetic-counter", fixture.Name);
            Assert.AreEqual(1, snapshot.Header.SchemaVersion);
            Assert.AreEqual("golden-harness-test", snapshot.Header.FrameworkVersion);
            Assert.AreEqual("synthetic-config", snapshot.Header.ConfigHash);
            Assert.AreEqual("synthetic-resources", snapshot.Header.ResourceCatalogHash);
            Assert.AreEqual(RuntimeFrame.Zero, snapshot.Header.StartFrame);
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual(new RuntimeFrame(1), snapshot.Records[1].Frame);
            Assert.AreEqual(1017L, snapshot.Records[1].ResultHash);
            Assert.AreEqual("frame=1 counter=17 commands=2", snapshot.Records[1].DiagnosticsSummary);
        }

        [Test]
        public void GoldenReplayHarness_SuccessfulReplayOutputsExpectedFinalHash()
        {
            RuntimeGoldenReplayFixture fixture = RuntimeGoldenReplayFixtures.SyntheticCounter();
            var harness = new RuntimeGoldenReplayHarness(new FakeRuntimeGoldenReplayDriver());

            RuntimeGoldenReplayHarnessResult result = harness.Run(fixture);

            Assert.IsTrue(result.Success, result.FailureReport);
            Assert.AreEqual(fixture.ExpectedFinalHash, result.FinalHash);
            StringAssert.Contains("counter=17", result.DiagnosticsSummary);
        }

        [Test]
        public void GoldenReplayHarness_FrameHashMismatchReportsFixtureFrameHashesCommandsAndDiagnostics()
        {
            RuntimeGoldenReplayFixture fixture = RuntimeGoldenReplayFixtures.SyntheticCounterWithFrameHashMismatch();
            var harness = new RuntimeGoldenReplayHarness(new FakeRuntimeGoldenReplayDriver());

            RuntimeGoldenReplayHarnessResult result = harness.Run(fixture);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("fixture=synthetic-counter", result.FailureReport);
            StringAssert.Contains("frame=1", result.FailureReport);
            StringAssert.Contains("expected=9999", result.FailureReport);
            StringAssert.Contains("actual=1017", result.FailureReport);
            StringAssert.Contains("commands=add-four,subtract-two", result.FailureReport);
            StringAssert.Contains("expectedDiagnostics=frame=1 counter=17 commands=2", result.FailureReport);
            StringAssert.Contains("actualDiagnostics=frame=1 counter=17 commands=2", result.FailureReport);
        }

        [Test]
        public void GoldenReplayFixture_CommandSequenceOrderIsStableAcrossSnapshots()
        {
            RuntimeGoldenReplayFixture fixture = RuntimeGoldenReplayFixtures.SyntheticCounter();

            RuntimeReplaySnapshot first = fixture.CreateSnapshot();
            RuntimeReplaySnapshot second = fixture.CreateSnapshot();

            CollectionAssert.AreEqual(
                new[] { "add-three", "multiply-five" },
                TraceIds(first.Records[0].Commands));
            CollectionAssert.AreEqual(
                TraceIds(first.Records[0].Commands),
                TraceIds(second.Records[0].Commands));
            CollectionAssert.AreEqual(
                Sequences(first.Records[0].Commands),
                Sequences(second.Records[0].Commands));
        }

        [Test]
        public void GoldenReplayHarness_FailureReportCarriesDiagnosticsSummary()
        {
            RuntimeGoldenReplayFixture fixture = RuntimeGoldenReplayFixtures.SyntheticCounterWithFrameHashMismatch();
            var harness = new RuntimeGoldenReplayHarness(new FakeRuntimeGoldenReplayDriver());

            RuntimeGoldenReplayHarnessResult result = harness.Run(fixture);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("expectedDiagnostics=frame=1 counter=17 commands=2", result.FailureReport);
            StringAssert.Contains("actualDiagnostics=frame=1 counter=17 commands=2", result.FailureReport);
            StringAssert.Contains("traces=add-four,subtract-two", result.FailureReport);
        }

        private static string[] TraceIds(IReadOnlyList<RuntimeCommand> commands)
        {
            var traceIds = new string[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                traceIds[i] = commands[i].TraceId;
            }

            return traceIds;
        }

        private static long[] Sequences(IReadOnlyList<RuntimeCommand> commands)
        {
            var sequences = new long[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                sequences[i] = commands[i].Sequence;
            }

            return sequences;
        }
    }
}
