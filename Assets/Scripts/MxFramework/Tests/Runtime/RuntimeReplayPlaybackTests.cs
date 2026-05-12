using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeReplayPlaybackTests
    {
        [Test]
        public void Play_EmptyReplaySucceedsWithNoFrameDiagnostics()
        {
            var snapshot = new RuntimeReplaySnapshot(Header(), null);
            var driver = new RecordingDriver();

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(0, result.FramesPlayed);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.None, result.FailureCode);
            Assert.IsTrue(driver.ResetCalled);
            Assert.AreEqual("config", driver.ResetHeader.ConfigHash);
            Assert.AreEqual(0, driver.Records.Count);
            StringAssert.Contains("no frame records", result.DiagnosticsSummary);
        }

        [Test]
        public void Play_SingleFrameSucceedsWhenHashMatches()
        {
            RuntimeReplayFrameRecord record = Record(RuntimeFrame.Zero, 42L, "expected-ok", Command(RuntimeFrame.Zero, 1, 10, 100, "cast"));
            var snapshot = new RuntimeReplaySnapshot(Header(), new[] { record });
            var driver = new RecordingDriver();

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(1, result.FramesPlayed);
            Assert.AreEqual(1, result.FrameResults.Count);
            Assert.AreEqual(RuntimeFrame.Zero, result.FrameResults[0].Frame);
            Assert.AreEqual(42L, result.FrameResults[0].ActualResultHash);
            Assert.AreEqual(1, driver.Records.Count);
            Assert.AreEqual(record.Frame, driver.Records[0].Frame);
            Assert.AreEqual(record.ResultHash, driver.Records[0].ResultHash);
            Assert.AreEqual("cast", driver.Records[0].Commands[0].TraceId);
        }

        [Test]
        public void Play_MultipleFramesRunsRecordsAndCommandsInSnapshotOrder()
        {
            RuntimeFrame frame2 = new RuntimeFrame(2);
            RuntimeFrame frame1 = new RuntimeFrame(1);
            var snapshot = new RuntimeReplaySnapshot(
                Header(),
                new[]
                {
                    Record(frame2, 20L, "frame-2", Command(frame2, 1, 1, 1, "second-b"), Command(frame2, 1, 1, 1, "second-a")),
                    Record(frame1, 10L, "frame-1", Command(frame1, 1, 1, 1, "first"))
                });
            var driver = new RecordingDriver();

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(2, result.FramesPlayed);
            CollectionAssert.AreEqual(new[] { frame2, frame1 }, driver.Frames);
            CollectionAssert.AreEqual(new[] { "second-b", "second-a" }, TraceIds(driver.Records[0].Commands));
        }

        [Test]
        public void Play_HashMismatchReturnsExpectedActualAndFailureContext()
        {
            RuntimeCommand command = Command(RuntimeFrame.Zero, 1, 10, 100, "cast");
            RuntimeReplayFrameRecord record = Record(RuntimeFrame.Zero, 42L, "expected-diagnostics", command);
            var snapshot = new RuntimeReplaySnapshot(Header(), new[] { record });
            var commandError = new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                command,
                RuntimeFrame.Zero,
                "payload rejected");
            var driver = new RecordingDriver(new[]
            {
                new RuntimeReplayPlaybackFrameResult(RuntimeFrame.Zero, 99L, "actual-diagnostics", new[] { commandError })
            });

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.HashMismatch, result.FailureCode);
            Assert.AreEqual(1, result.FramesPlayed);
            Assert.IsTrue(result.HasFailureFrame);
            Assert.AreEqual(RuntimeFrame.Zero, result.FailureFrame);
            Assert.AreEqual(42L, result.ExpectedResultHash);
            Assert.AreEqual(99L, result.ActualResultHash);
            Assert.AreEqual("expected-diagnostics", result.ExpectedDiagnosticsSummary);
            Assert.AreEqual("actual-diagnostics", result.ActualDiagnosticsSummary);
            Assert.AreEqual(1, result.FailureCommands.Count);
            Assert.AreEqual("cast", result.FailureCommands[0].TraceId);
            Assert.AreEqual(1, result.CommandErrors.Count);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, result.CommandErrors[0].Code);
            StringAssert.Contains("frame 0", result.FailureMessage);
        }

        [Test]
        public void Play_DriverExceptionReturnsStructuredFailure()
        {
            RuntimeCommand command = Command(RuntimeFrame.Zero, 1, 10, 100, "cast");
            RuntimeReplayFrameRecord record = Record(RuntimeFrame.Zero, 42L, "expected-diagnostics", command);
            var snapshot = new RuntimeReplaySnapshot(Header(), new[] { record });
            var exception = new InvalidOperationException("driver failed");
            var driver = new RecordingDriver
            {
                RunException = exception
            };

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeReplayPlaybackFailureCode.DriverException, result.FailureCode);
            Assert.AreEqual(0, result.FramesPlayed);
            Assert.IsTrue(result.HasFailureFrame);
            Assert.AreEqual(RuntimeFrame.Zero, result.FailureFrame);
            Assert.AreEqual(42L, result.ExpectedResultHash);
            Assert.AreEqual(1, result.FailureCommands.Count);
            Assert.AreSame(exception, result.Exception);
            StringAssert.Contains("driver failed", result.FailureMessage);
        }

        [Test]
        public void Play_SnapshotCopyIsNotAffectedByRecorderAppends()
        {
            var recorder = new RuntimeReplayRecorder(Header());
            recorder.RecordFrame(RuntimeFrame.Zero, new[] { Command(RuntimeFrame.Zero, 1, 10, 100, "snapshot") }, 1L, "snapshot");
            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();
            recorder.RecordFrame(new RuntimeFrame(1), new[] { Command(new RuntimeFrame(1), 1, 10, 100, "later") }, 2L, "later");
            var driver = new RecordingDriver();

            RuntimeReplayPlaybackResult result = new RuntimeReplayPlaybackRunner().Play(snapshot, driver);

            Assert.IsTrue(result.Success, result.FailureMessage);
            Assert.AreEqual(1, result.FramesPlayed);
            Assert.AreEqual(1, driver.Records.Count);
            Assert.AreEqual("snapshot", driver.Records[0].Commands[0].TraceId);
        }

        private static RuntimeReplayHeader Header()
        {
            return new RuntimeReplayHeader(1, "0.1", "config", "resources", RuntimeFrame.Zero);
        }

        private static RuntimeReplayFrameRecord Record(RuntimeFrame frame, long resultHash, string diagnosticsSummary, params RuntimeCommand[] commands)
        {
            return new RuntimeReplayFrameRecord(frame, commands, resultHash, diagnosticsSummary);
        }

        private static RuntimeCommand Command(RuntimeFrame frame, int sourceId, int commandId, int targetId, string traceId)
        {
            return new RuntimeCommand(frame, sourceId, commandId, targetId, traceId: traceId);
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

        private sealed class RecordingDriver : IRuntimeReplayFrameDriver
        {
            private readonly Queue<RuntimeReplayPlaybackFrameResult> _results;

            public RecordingDriver()
                : this(null)
            {
            }

            public RecordingDriver(IReadOnlyList<RuntimeReplayPlaybackFrameResult> results)
            {
                _results = new Queue<RuntimeReplayPlaybackFrameResult>();
                if (results != null)
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        _results.Enqueue(results[i]);
                    }
                }
            }

            public bool ResetCalled { get; private set; }
            public RuntimeReplayHeader ResetHeader { get; private set; }
            public Exception RunException { get; set; }
            public List<RuntimeReplayFrameRecord> Records { get; } = new List<RuntimeReplayFrameRecord>();
            public List<RuntimeFrame> Frames { get; } = new List<RuntimeFrame>();

            public void Reset(RuntimeReplayHeader header)
            {
                ResetCalled = true;
                ResetHeader = header;
            }

            public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
            {
                if (RunException != null)
                {
                    throw RunException;
                }

                Records.Add(record);
                Frames.Add(record.Frame);
                if (_results.Count > 0)
                {
                    return _results.Dequeue();
                }

                return new RuntimeReplayPlaybackFrameResult(record.Frame, record.ResultHash, "actual:" + record.DiagnosticsSummary);
            }
        }
    }
}
