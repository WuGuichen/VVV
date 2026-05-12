using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeFrameCommandReplayTests
    {
        [Test]
        public void RuntimeClock_StepAndResetAdvanceFromExplicitFrame()
        {
            var clock = new RuntimeClock();

            Assert.AreEqual(new RuntimeFrame(0), clock.CurrentFrame);
            Assert.AreEqual(new RuntimeFrame(1), clock.Step());

            clock.Reset(new RuntimeFrame(5));
            Assert.AreEqual(new RuntimeFrame(5), clock.CurrentFrame);
            Assert.AreEqual(new RuntimeFrame(6), clock.Step());

            clock.Reset(new RuntimeFrame(2));
            Assert.AreEqual(new RuntimeFrame(3), clock.Step());
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeFrame(-1));
        }

        [Test]
        public void RuntimeCommandBuffer_DrainsSameFrameInStableOrder()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeFrame frame = RuntimeFrame.Zero;

            buffer.Enqueue(Command(frame, sourceId: 2, commandId: 1, targetId: 1, traceId: "source-2"));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 2, targetId: 1, traceId: "command-2"));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 2, traceId: "target-2"));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: "same-a"));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: "same-b"));

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(frame);

            CollectionAssert.AreEqual(
                new[] { "same-a", "same-b", "target-2", "command-2", "source-2" },
                TraceIds(drained));
            Assert.Less(drained[0].Sequence, drained[1].Sequence);
        }

        [Test]
        public void RuntimeCommandBuffer_KeepsFutureFrameCommandsUntilTargetFrame()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeFrame frame0 = RuntimeFrame.Zero;
            RuntimeFrame frame1 = new RuntimeFrame(1);

            buffer.Enqueue(Command(frame1, sourceId: 1, commandId: 1, targetId: 1, traceId: "future"));
            buffer.Enqueue(Command(frame0, sourceId: 1, commandId: 1, targetId: 1, traceId: "now"));

            IReadOnlyList<RuntimeCommand> firstDrain = buffer.DrainForFrame(frame0);

            Assert.AreEqual(1, firstDrain.Count);
            Assert.AreEqual("now", firstDrain[0].TraceId);
            Assert.AreEqual(1, buffer.PendingCount);

            IReadOnlyList<RuntimeCommand> secondDrain = buffer.DrainForFrame(frame1);

            Assert.AreEqual(1, secondDrain.Count);
            Assert.AreEqual("future", secondDrain[0].TraceId);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandBuffer_DrainsDueCommandsWhenFrameSkipsAhead()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeFrame frame1 = new RuntimeFrame(1);
            RuntimeFrame frame3 = new RuntimeFrame(3);

            buffer.Enqueue(Command(frame3, sourceId: 1, commandId: 1, targetId: 1, traceId: "frame-3"));
            buffer.Enqueue(Command(frame1, sourceId: 1, commandId: 1, targetId: 1, traceId: "frame-1"));

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(frame3);

            CollectionAssert.AreEqual(new[] { "frame-1", "frame-3" }, TraceIds(drained));
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandBuffer_RejectsLateAndInvalidCommandsWithStructuredError()
        {
            var buffer = new RuntimeCommandBuffer();
            buffer.DrainForFrame(RuntimeFrame.Zero);

            RuntimeCommandValidationResult late = buffer.Enqueue(Command(RuntimeFrame.Zero, 1, 1, 1, "late"));
            RuntimeCommandValidationResult invalid = buffer.Enqueue(Command(new RuntimeFrame(1), 1, -1, 1, "invalid"));

            Assert.IsFalse(late.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.LateCommand, late.Error.Code);
            Assert.AreEqual(new RuntimeFrame(1), late.Error.CurrentFrame);

            Assert.IsFalse(invalid.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidCommandId, invalid.Error.Code);
        }

        [Test]
        public void RuntimeReplayRecorder_RecordsCopiesAndReturnsReadonlySnapshot()
        {
            var header = new RuntimeReplayHeader(
                schemaVersion: 1,
                frameworkVersion: "0.1",
                configHash: "config",
                resourceCatalogHash: "resources",
                startFrame: RuntimeFrame.Zero);
            var recorder = new RuntimeReplayRecorder(header);
            var commands = new List<RuntimeCommand>
            {
                Command(RuntimeFrame.Zero, 1, 10, 100, "cast")
            };

            RuntimeReplayFrameRecord record = recorder.RecordFrame(RuntimeFrame.Zero, commands, resultHash: 12345L, diagnosticsSummary: "ok");
            commands.Add(Command(RuntimeFrame.Zero, 2, 20, 200, "late-mutation"));

            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();
            recorder.RecordFrame(new RuntimeFrame(1), commands, resultHash: 67890L, diagnosticsSummary: "next");

            Assert.AreEqual(1, record.Commands.Count);
            Assert.AreEqual("cast", record.Commands[0].TraceId);
            Assert.AreEqual(12345L, record.ResultHash);
            Assert.AreEqual("ok", record.DiagnosticsSummary);
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("config", snapshot.Header.ConfigHash);

            var snapshotRecords = (IList<RuntimeReplayFrameRecord>)snapshot.Records;
            Assert.IsTrue(snapshotRecords.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => snapshotRecords.Add(record));
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
    }
}
