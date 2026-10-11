using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Runtime
{
    public class RuntimeFrameCommandReplayTests
    {
        private static class TestTraceIds
        {
            internal static readonly RuntimeTraceIdentity A = new RuntimeTraceIdentity(91020001); // a
            internal static readonly RuntimeTraceIdentity B = new RuntimeTraceIdentity(91020002); // b
            internal static readonly RuntimeTraceIdentity Cast = new RuntimeTraceIdentity(91020003); // cast
            internal static readonly RuntimeTraceIdentity Command2 = new RuntimeTraceIdentity(91020004); // command-2
            internal static readonly RuntimeTraceIdentity First = new RuntimeTraceIdentity(91020005); // first
            internal static readonly RuntimeTraceIdentity Frame1 = new RuntimeTraceIdentity(91020006); // frame-1
            internal static readonly RuntimeTraceIdentity Frame3 = new RuntimeTraceIdentity(91020007); // frame-3
            internal static readonly RuntimeTraceIdentity Full = new RuntimeTraceIdentity(91020008); // full
            internal static readonly RuntimeTraceIdentity Future = new RuntimeTraceIdentity(91020009); // future
            internal static readonly RuntimeTraceIdentity Invalid = new RuntimeTraceIdentity(91020010); // invalid
            internal static readonly RuntimeTraceIdentity InvalidId = new RuntimeTraceIdentity(91020011); // invalid-id
            internal static readonly RuntimeTraceIdentity InvalidPayload = new RuntimeTraceIdentity(91020012); // invalid-payload
            internal static readonly RuntimeTraceIdentity Keep = new RuntimeTraceIdentity(91020013); // keep
            internal static readonly RuntimeTraceIdentity Late = new RuntimeTraceIdentity(91020014); // late
            internal static readonly RuntimeTraceIdentity LateMutation = new RuntimeTraceIdentity(91020015); // late-mutation
            internal static readonly RuntimeTraceIdentity Legacy = new RuntimeTraceIdentity(91020016); // legacy
            internal static readonly RuntimeTraceIdentity MissingEnvelope = new RuntimeTraceIdentity(91020017); // missing-envelope
            internal static readonly RuntimeTraceIdentity Now = new RuntimeTraceIdentity(91020018); // now
            internal static readonly RuntimeTraceIdentity RemoveA = new RuntimeTraceIdentity(91020019); // remove-a
            internal static readonly RuntimeTraceIdentity RemoveB = new RuntimeTraceIdentity(91020020); // remove-b
            internal static readonly RuntimeTraceIdentity SameA = new RuntimeTraceIdentity(91020021); // same-a
            internal static readonly RuntimeTraceIdentity SameB = new RuntimeTraceIdentity(91020022); // same-b
            internal static readonly RuntimeTraceIdentity Second = new RuntimeTraceIdentity(91020023); // second
            internal static readonly RuntimeTraceIdentity SnapshotA = new RuntimeTraceIdentity(91020024); // snapshot-a
            internal static readonly RuntimeTraceIdentity SnapshotB = new RuntimeTraceIdentity(91020025); // snapshot-b
            internal static readonly RuntimeTraceIdentity Source2 = new RuntimeTraceIdentity(91020026); // source-2
            internal static readonly RuntimeTraceIdentity Target2 = new RuntimeTraceIdentity(91020027); // target-2
            internal static readonly RuntimeTraceIdentity TargetExisting = new RuntimeTraceIdentity(91020028); // target-existing
        }

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

            buffer.Enqueue(Command(frame, sourceId: 2, commandId: 1, targetId: 1, traceId: TestTraceIds.Source2));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 2, targetId: 1, traceId: TestTraceIds.Command2));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 2, traceId: TestTraceIds.Target2));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.SameA));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.SameB));

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(frame);

            CollectionAssert.AreEqual(
                new[] { TestTraceIds.SameA, TestTraceIds.SameB, TestTraceIds.Target2, TestTraceIds.Command2, TestTraceIds.Source2 },
                TraceIds(drained));
            Assert.Less(drained[0].Sequence, drained[1].Sequence);
        }

        [Test]
        public void RuntimeCommandBuffer_KeepsFutureFrameCommandsUntilTargetFrame()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeFrame frame0 = RuntimeFrame.Zero;
            RuntimeFrame frame1 = new RuntimeFrame(1);

            buffer.Enqueue(Command(frame1, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.Future));
            buffer.Enqueue(Command(frame0, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.Now));

            IReadOnlyList<RuntimeCommand> firstDrain = buffer.DrainForFrame(frame0);

            Assert.AreEqual(1, firstDrain.Count);
            Assert.AreEqual(TestTraceIds.Now, firstDrain[0].TraceId);
            Assert.AreEqual(1, buffer.PendingCount);

            IReadOnlyList<RuntimeCommand> secondDrain = buffer.DrainForFrame(frame1);

            Assert.AreEqual(1, secondDrain.Count);
            Assert.AreEqual(TestTraceIds.Future, secondDrain[0].TraceId);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandBuffer_DrainsDueCommandsWhenFrameSkipsAhead()
        {
            var buffer = new RuntimeCommandBuffer();
            RuntimeFrame frame1 = new RuntimeFrame(1);
            RuntimeFrame frame3 = new RuntimeFrame(3);

            buffer.Enqueue(Command(frame3, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.Frame3));
            buffer.Enqueue(Command(frame1, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.Frame1));

            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(frame3);

            CollectionAssert.AreEqual(new[] { TestTraceIds.Frame1, TestTraceIds.Frame3 }, TraceIds(drained));
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandBuffer_RemoveWhereOnlyRemovesMatchingPendingCommands()
        {
            var buffer = new RuntimeCommandBuffer();
            buffer.Enqueue(Command(new RuntimeFrame(1), sourceId: 1, commandId: 10, targetId: 100, traceId: TestTraceIds.RemoveA));
            buffer.Enqueue(Command(new RuntimeFrame(2), sourceId: 1, commandId: 20, targetId: 200, traceId: TestTraceIds.Keep));
            buffer.Enqueue(Command(new RuntimeFrame(3), sourceId: 1, commandId: 10, targetId: 100, traceId: TestTraceIds.RemoveB));

            int removed = buffer.RemoveWhere(command => command.TargetId == 100);

            Assert.AreEqual(2, removed);
            Assert.AreEqual(1, buffer.PendingCount);
            IReadOnlyList<RuntimeCommand> drained = buffer.DrainForFrame(new RuntimeFrame(3));
            Assert.AreEqual(1, drained.Count);
            Assert.AreEqual(TestTraceIds.Keep, drained[0].TraceId);
        }

        [Test]
        public void RuntimeCommandBuffer_DrainToListDoesNotAllocateAfterWarmup()
        {
            var buffer = new RuntimeCommandBuffer();
            var drained = new List<RuntimeCommand>(4);
            RuntimeFrame warmupFrame = RuntimeFrame.Zero;
            EnqueueDrainBatch(buffer, warmupFrame);
            Assert.AreEqual(4, buffer.DrainForFrame(warmupFrame, drained));
            drained.Clear();

            RuntimeFrame measuredFrame = new RuntimeFrame(1);
            EnqueueDrainBatch(buffer, measuredFrame);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int drainedCount = buffer.DrainForFrame(measuredFrame, drained);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(4, drainedCount);
            CollectionAssert.AreEqual(
                new[] { TestTraceIds.SameA, TestTraceIds.SameB, TestTraceIds.Target2, TestTraceIds.Source2 },
                TraceIds(drained));
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void RuntimeCommandBuffer_InitialCapacityAvoidsFirstSubmitAllocation()
        {
            var buffer = new RuntimeCommandBuffer(initialCapacity: 4);
            var drained = new List<RuntimeCommand>(4);
            RuntimeFrame frame = RuntimeFrame.Zero;

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            EnqueueDrainBatch(buffer, frame);
            int drainedCount = buffer.DrainForFrame(frame, drained);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(4, drainedCount);
            CollectionAssert.AreEqual(
                new[] { TestTraceIds.SameA, TestTraceIds.SameB, TestTraceIds.Target2, TestTraceIds.Source2 },
                TraceIds(drained));
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void RuntimeCommandBuffer_BoundedAdmissionRejectsAtHardLimitWithoutConsumingSequence()
        {
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(2);

            Assert.IsTrue(buffer.IsPendingCapacityBounded);
            Assert.AreEqual(2, buffer.PendingCapacityLimit);
            Assert.IsTrue(buffer.Enqueue(Command(RuntimeFrame.Zero, 1, 1, 1, TestTraceIds.First)).Success);
            Assert.IsTrue(buffer.Enqueue(Command(RuntimeFrame.Zero, 1, 1, 2, TestTraceIds.Second)).Success);
            RuntimeCommandBufferSnapshot before = buffer.CaptureSnapshot();

            RuntimeCommandValidationResult rejected = buffer.Enqueue(
                Command(RuntimeFrame.Zero, 1, 1, 3, TestTraceIds.Full));
            RuntimeCommandBufferSnapshot after = buffer.CaptureSnapshot();

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.PendingCapacityExceeded, rejected.Error.Code);
            Assert.AreEqual(before.CurrentFrame, after.CurrentFrame);
            Assert.AreEqual(before.NextSequence, after.NextSequence);
            Assert.AreEqual(before.PendingCount, after.PendingCount);
            CollectionAssert.AreEqual(before.PendingCommands, after.PendingCommands);
        }

        [Test]
        public void RuntimeCommandBuffer_ZeroBoundStillPrioritizesValidationBeforeCapacity()
        {
            var validator = new RejectingValidator();
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(
                validator,
                RuntimeFrame.Zero,
                pendingCapacityLimit: 0);

            RuntimeCommandValidationResult invalidId = buffer.Enqueue(
                Command(RuntimeFrame.Zero, 1, -1, 1, TestTraceIds.InvalidId));
            RuntimeCommandValidationResult invalidPayload = buffer.Enqueue(
                Command(RuntimeFrame.Zero, 1, 1, 1, TestTraceIds.InvalidPayload));
            RuntimeCommandValidationResult full = RuntimeCommandBuffer.CreateBounded(0).Enqueue(
                Command(RuntimeFrame.Zero, 1, 1, 1, TestTraceIds.Full));

            Assert.AreEqual(RuntimeCommandErrorCode.InvalidCommandId, invalidId.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.InvalidPayload, invalidPayload.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.PendingCapacityExceeded, full.Error.Code);
            Assert.AreEqual(0, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandBuffer_BoundedRestoreRejectsOversizeSnapshotAtomically()
        {
            RuntimeCommandBuffer target = RuntimeCommandBuffer.CreateBounded(
                validator: null,
                startFrame: new RuntimeFrame(7),
                pendingCapacityLimit: 1);
            Assert.IsTrue(target.Enqueue(
                Command(new RuntimeFrame(8), 1, 1, 10, TestTraceIds.TargetExisting)).Success);
            RuntimeCommandBufferSnapshot before = target.CaptureSnapshot();
            var oversize = new RuntimeCommandBufferSnapshot(
                new RuntimeFrame(20),
                nextSequence: 99,
                pendingCommands: new[]
                {
                    Command(new RuntimeFrame(20), 1, 1, 1, TestTraceIds.SnapshotA),
                    Command(new RuntimeFrame(20), 1, 1, 2, TestTraceIds.SnapshotB)
                });

            Assert.Throws<InvalidOperationException>(() => target.RestoreSnapshot(oversize));
            RuntimeCommandBufferSnapshot after = target.CaptureSnapshot();

            Assert.AreEqual(before.CurrentFrame, after.CurrentFrame);
            Assert.AreEqual(before.NextSequence, after.NextSequence);
            Assert.AreEqual(before.PendingCount, after.PendingCount);
            CollectionAssert.AreEqual(before.PendingCommands, after.PendingCommands);
            Assert.AreEqual(1, target.PendingCapacityLimit);
        }

        [Test]
        public void RuntimeCommandBuffer_UnboundedRestoreKeepsLegacyOversizeBehavior()
        {
            var target = new RuntimeCommandBuffer(initialCapacity: 1);
            var snapshot = new RuntimeCommandBufferSnapshot(
                new RuntimeFrame(3),
                nextSequence: 5,
                pendingCommands: new[]
                {
                    Command(new RuntimeFrame(3), 1, 1, 1, TestTraceIds.A),
                    Command(new RuntimeFrame(3), 1, 1, 2, TestTraceIds.B)
                });

            target.RestoreSnapshot(snapshot);

            Assert.IsFalse(target.IsPendingCapacityBounded);
            Assert.AreEqual(RuntimeCommandBuffer.UnboundedPendingCapacity, target.PendingCapacityLimit);
            Assert.AreEqual(2, target.PendingCount);
            Assert.AreEqual(5, target.CaptureSnapshot().NextSequence);
        }

        [Test]
        public void RuntimeCommandBuffer_RejectsLateAndInvalidCommandsWithStructuredError()
        {
            var buffer = new RuntimeCommandBuffer();
            buffer.DrainForFrame(RuntimeFrame.Zero);

            RuntimeCommandValidationResult late = buffer.Enqueue(Command(RuntimeFrame.Zero, 1, 1, 1, TestTraceIds.Late));
            RuntimeCommandValidationResult invalid = buffer.Enqueue(Command(new RuntimeFrame(1), 1, -1, 1, TestTraceIds.Invalid));

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
                Command(RuntimeFrame.Zero, 1, 10, 100, TestTraceIds.Cast)
            };

            RuntimeReplayFrameRecord record = recorder.RecordFrame(RuntimeFrame.Zero, commands, resultHash: 12345L, diagnosticsSummary: "ok");
            commands.Add(Command(RuntimeFrame.Zero, 2, 20, 200, TestTraceIds.LateMutation));

            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();
            recorder.RecordFrame(new RuntimeFrame(1), commands, resultHash: 67890L, diagnosticsSummary: "next");

            Assert.AreEqual(1, record.Commands.Count);
            Assert.AreEqual(TestTraceIds.Cast, record.Commands[0].TraceId);
            Assert.AreEqual(12345L, record.ResultHash);
            Assert.AreEqual("ok", record.DiagnosticsSummary);
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("config", snapshot.Header.ConfigHash);
            Assert.AreEqual(RuntimeReplayCommandCaptureMode.LegacyUnfiltered, recorder.CaptureMode);

            var snapshotRecords = (IList<RuntimeReplayFrameRecord>)snapshot.Records;
            Assert.IsTrue(snapshotRecords.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => snapshotRecords.Add(record));
        }

        [Test]
        public void RuntimeCommandSourceRegistry_ReturnsStructuredFailuresAndSeals()
        {
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);

            Assert.IsFalse(registry.TryRegister(
                0,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 10 },
                out RuntimeCommandSourceRegistrationResult invalidSource));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.InvalidSourceId, invalidSource.ErrorCode);

            Assert.IsFalse(registry.TryRegister(
                1,
                (RuntimeCommandSourceKind)99,
                new[] { 10 },
                out RuntimeCommandSourceRegistrationResult invalidKind));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.InvalidSourceKind, invalidKind.ErrorCode);

            Assert.IsFalse(registry.TryRegister(
                1,
                RuntimeCommandSourceKind.Exogenous,
                new[] { -1 },
                out RuntimeCommandSourceRegistrationResult invalidCommand));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.InvalidCommandId, invalidCommand.ErrorCode);
            Assert.AreEqual(-1, invalidCommand.RelatedValue);

            Assert.IsFalse(registry.TryRegister(
                1,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 10, 10 },
                out RuntimeCommandSourceRegistrationResult duplicateCommand));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.DuplicateCommandId, duplicateCommand.ErrorCode);
            Assert.AreEqual(10, duplicateCommand.RelatedValue);

            var registeredCommands = new[] { 20, 10 };
            Assert.IsTrue(registry.TryRegister(
                1,
                RuntimeCommandSourceKind.Exogenous,
                registeredCommands,
                out RuntimeCommandSourceRegistrationResult registered));
            registeredCommands[0] = 30;
            Assert.IsTrue(registered.Success);
            Assert.AreEqual(1, registry.Count);
            Assert.IsTrue(registry.TryGet(1, out RuntimeCommandSourceDefinition definition));
            Assert.AreEqual(RuntimeCommandSourceKind.Exogenous, definition.Kind);
            Assert.AreEqual(2, definition.AllowedCommandCount);
            Assert.IsTrue(definition.AllowsCommand(10));
            Assert.IsTrue(definition.AllowsCommand(20));
            Assert.IsFalse(definition.AllowsCommand(30));

            Assert.IsFalse(registry.TryRegister(
                1,
                RuntimeCommandSourceKind.Derived,
                new[] { 30 },
                out RuntimeCommandSourceRegistrationResult duplicateSource));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.DuplicateSourceId, duplicateSource.ErrorCode);

            registry.Seal();
            Assert.IsFalse(registry.TryRegister(
                2,
                RuntimeCommandSourceKind.Derived,
                new[] { 30 },
                out RuntimeCommandSourceRegistrationResult sealedResult));
            Assert.AreEqual(RuntimeCommandSourceRegistrationErrorCode.RegistrySealed, sealedResult.ErrorCode);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_FailsClosedAndLeavesLegacyBufferCompatible()
        {
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            Assert.IsTrue(registry.TryRegister(
                20,
                RuntimeCommandSourceKind.Derived,
                new[] { 200 },
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 4);
            var external = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);

            RuntimeCommandValidationResult accepted = external.Submit(
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous));
            RuntimeCommandValidationResult unknownSource = external.Submit(
                TypedCommand(99, 100, RuntimeCommandProvenanceKind.Exogenous));
            RuntimeCommandValidationResult forgedDerivedSource = external.Submit(
                TypedCommand(20, 200, RuntimeCommandProvenanceKind.Derived));
            RuntimeCommandValidationResult provenanceMismatch = external.Submit(
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Derived));
            RuntimeCommandValidationResult unknownCommand = external.Submit(
                TypedCommand(10, 101, RuntimeCommandProvenanceKind.Exogenous));
            RuntimeCommandValidationResult missingEnvelope = external.Submit(
                Command(RuntimeFrame.Zero, 10, 100, 1, TestTraceIds.MissingEnvelope));

            Assert.IsTrue(accepted.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.UnregisteredSourceId, unknownSource.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedSource, forgedDerivedSource.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.ProvenanceMismatch, provenanceMismatch.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedCommand, unknownCommand.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.ProvenanceMismatch, missingEnvelope.Error.Code);

            RuntimeCommandValidationResult legacy = buffer.Enqueue(
                Command(RuntimeFrame.Zero, sourceId: 0, commandId: 100, targetId: 1, traceId: TestTraceIds.Legacy));
            Assert.IsTrue(legacy.Success);
            Assert.AreEqual(2, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_TrustedDerivedUsesItsOwnIngressAndAllowlist()
        {
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            Assert.IsTrue(registry.TryRegister(
                20,
                RuntimeCommandSourceKind.Derived,
                new[] { 200 },
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 1);
            var derived = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.TrustedDerived);

            RuntimeCommandValidationResult accepted = derived.Submit(
                TypedCommand(20, 200, RuntimeCommandProvenanceKind.Derived));
            RuntimeCommandValidationResult exogenousSource = derived.Submit(
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous));
            RuntimeCommandValidationResult unknownCommand = derived.Submit(
                TypedCommand(20, 201, RuntimeCommandProvenanceKind.Derived));

            Assert.IsTrue(accepted.Success);
            Assert.IsFalse(derived.IsTargetPendingCapacityBounded);
            Assert.AreEqual(RuntimeCommandBuffer.UnboundedPendingCapacity, derived.TargetPendingCapacityLimit);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedSource, exogenousSource.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedCommand, unknownCommand.Error.Code);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_DoesNotAllocateAfterWarmup()
        {
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 1);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 65);
            var gate = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);
            RuntimeCommand acceptedCommand = TypedCommand(
                10,
                100,
                RuntimeCommandProvenanceKind.Exogenous);
            RuntimeCommand rejectedCommand = TypedCommand(
                10,
                101,
                RuntimeCommandProvenanceKind.Exogenous);
            Assert.IsTrue(gate.Submit(acceptedCommand).Success);
            Assert.AreEqual(
                RuntimeCommandErrorCode.UnauthorizedCommand,
                gate.Submit(rejectedCommand).Error.Code);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                gate.Submit(acceptedCommand);
                gate.Submit(rejectedCommand);
            }
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(65, buffer.PendingCount);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_AtomicBatchRequiresBoundedBufferEvenWhenEmpty()
        {
            RuntimeCommandSubmissionGate unbounded = CreateExternalGate(
                new RuntimeCommandBuffer(initialCapacity: 2));

            RuntimeCommandBatchSubmissionResult rejected =
                unbounded.SubmitBatch(ReadOnlySpan<RuntimeCommand>.Empty);

            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(0, rejected.SubmittedCount);
            Assert.AreEqual(-1, rejected.FailedCommandIndex);
            Assert.AreEqual(
                RuntimeCommandErrorCode.AtomicBatchRequiresBoundedBuffer,
                rejected.Error.Code);
            Assert.AreEqual(default(RuntimeCommand), rejected.Error.Command);

            RuntimeCommandSubmissionGate bounded = CreateExternalGate(
                RuntimeCommandBuffer.CreateBounded(2));
            RuntimeCommandBatchSubmissionResult accepted =
                bounded.SubmitBatch(ReadOnlySpan<RuntimeCommand>.Empty);

            Assert.IsTrue(accepted.Success);
            Assert.AreEqual(0, accepted.SubmittedCount);
            Assert.AreEqual(-1, accepted.FailedCommandIndex);
            Assert.IsTrue(accepted.Error.IsNone);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_AtomicBatchCommitsCallerOrderWithConsecutiveSequences()
        {
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(3);
            RuntimeCommandSubmissionGate gate = CreateExternalGate(buffer);
            var commands = new[]
            {
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 11),
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 22),
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 33)
            };

            RuntimeCommandBatchSubmissionResult result = gate.SubmitBatch(commands);
            RuntimeCommandBufferSnapshot snapshot = buffer.CaptureSnapshot();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.SubmittedCount);
            Assert.AreEqual(3, snapshot.PendingCount);
            Assert.AreEqual(3L, snapshot.NextSequence);
            for (int i = 0; i < commands.Length; i++)
            {
                Assert.AreEqual(i, snapshot.PendingCommands[i].Sequence);
                Assert.AreEqual(commands[i].Envelope.Data0, snapshot.PendingCommands[i].Envelope.Data0);
            }
        }

        [Test]
        public void RuntimeCommandSubmissionGate_AtomicFailureLeavesBufferStateUnchanged()
        {
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(3);
            RuntimeCommandSubmissionGate gate = CreateExternalGate(buffer);
            Assert.IsTrue(gate.Submit(TypedCommand(
                10,
                100,
                RuntimeCommandProvenanceKind.Exogenous,
                1)).Success);
            RuntimeCommandBufferSnapshot before = buffer.CaptureSnapshot();
            var commands = new[]
            {
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 2),
                TypedCommand(10, 101, RuntimeCommandProvenanceKind.Exogenous, 3)
            };

            RuntimeCommandBatchSubmissionResult result = gate.SubmitBatch(commands);
            RuntimeCommandBufferSnapshot after = buffer.CaptureSnapshot();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.FailedCommandIndex);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedCommand, result.Error.Code);
            AssertSnapshotsEqual(before, after);
        }

        [Test]
        public void RuntimeCommandSubmissionGate_AtomicPreflightUsesApprovedFailureOrder()
        {
            var registry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            var validator = new RejectPayloadValidator(rejectedPayload: 30);
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(
                validator,
                new RuntimeFrame(5),
                pendingCapacityLimit: 1);
            var gate = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);

            RuntimeCommand unauthorizedLate = TypedCommandAt(
                frame: 4,
                sourceId: 10,
                commandId: 101,
                payload: 10);
            AssertAtomicFailure(
                gate,
                new[] { unauthorizedLate },
                0,
                RuntimeCommandErrorCode.UnauthorizedCommand);

            RuntimeCommand invalidId = TypedCommandAt(
                frame: 5,
                sourceId: 10,
                commandId: -1,
                payload: 20);
            AssertAtomicFailure(
                gate,
                new[] { invalidId },
                0,
                RuntimeCommandErrorCode.UnauthorizedCommand);

            RuntimeCommand late = TypedCommandAt(
                frame: 4,
                sourceId: 10,
                commandId: 100,
                payload: 20);
            AssertAtomicFailure(
                gate,
                new[] { late },
                0,
                RuntimeCommandErrorCode.LateCommand);

            RuntimeCommand validatorRejected = TypedCommandAt(
                frame: 5,
                sourceId: 10,
                commandId: 100,
                payload: 30);
            AssertAtomicFailure(
                gate,
                new[] { validatorRejected },
                0,
                RuntimeCommandErrorCode.InvalidPayload);

            Assert.IsTrue(gate.Submit(TypedCommandAt(5, 10, 100, 40)).Success);
            RuntimeCommandBufferSnapshot full = buffer.CaptureSnapshot();
            RuntimeCommandBatchSubmissionResult capacity = gate.SubmitBatch(
                new[] { TypedCommandAt(5, 10, 100, 50) });
            Assert.IsFalse(capacity.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.PendingCapacityExceeded, capacity.Error.Code);
            AssertSnapshotsEqual(full, buffer.CaptureSnapshot());
        }

        [Test]
        public void RuntimeCommandBuffer_SequenceSentinelRejectsSingleAndAtomicAdmissionWithoutMutation()
        {
            RuntimeCommand command = TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous);
            RuntimeCommandBuffer single = RuntimeCommandBuffer.CreateBounded(1);
            single.RestoreSnapshot(new RuntimeCommandBufferSnapshot(
                RuntimeFrame.Zero,
                long.MaxValue,
                Array.Empty<RuntimeCommand>()));

            RuntimeCommandValidationResult singleResult = single.Enqueue(command);
            Assert.IsFalse(singleResult.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.SequenceExhausted, singleResult.Error.Code);
            Assert.AreEqual(0, single.PendingCount);
            Assert.AreEqual(long.MaxValue, single.CaptureSnapshot().NextSequence);

            RuntimeCommandBuffer atomic = RuntimeCommandBuffer.CreateBounded(2);
            atomic.RestoreSnapshot(new RuntimeCommandBufferSnapshot(
                RuntimeFrame.Zero,
                long.MaxValue - 1L,
                Array.Empty<RuntimeCommand>()));
            RuntimeCommandSubmissionGate gate = CreateExternalGate(atomic);
            RuntimeCommandBufferSnapshot before = atomic.CaptureSnapshot();

            RuntimeCommandBatchSubmissionResult atomicResult = gate.SubmitBatch(
                new[] { command, command });

            Assert.IsFalse(atomicResult.Success);
            Assert.AreEqual(1, atomicResult.FailedCommandIndex);
            Assert.AreEqual(RuntimeCommandErrorCode.SequenceExhausted, atomicResult.Error.Code);
            AssertSnapshotsEqual(before, atomic.CaptureSnapshot());
        }

        [Test]
        public void RuntimeCommandSubmissionGate_AtomicBatchDoesNotAllocateAcross64Cycles()
        {
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(128);
            RuntimeCommandSubmissionGate gate = CreateExternalGate(buffer);
            var commands = new[]
            {
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 1),
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, 2)
            };
            Assert.IsTrue(gate.SubmitBatch(ReadOnlySpan<RuntimeCommand>.Empty).Success);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                gate.SubmitBatch(commands);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(128, buffer.PendingCount);
        }

        [Test]
        public void RuntimeReplayRecorder_ExogenousStreamFiltersWithSharedSourceRegistry()
        {
            var header = new RuntimeReplayHeader(
                schemaVersion: 1,
                frameworkVersion: "0.1",
                configHash: "config",
                resourceCatalogHash: "resources",
                startFrame: RuntimeFrame.Zero);
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            Assert.IsTrue(registry.TryRegister(
                20,
                RuntimeCommandSourceKind.Derived,
                new[] { 200 },
                out _));
            RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                header,
                registry);
            var commands = new List<RuntimeCommand>
            {
                TypedCommand(20, 200, RuntimeCommandProvenanceKind.Derived, payload: 1),
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Exogenous, payload: 2),
                TypedCommand(99, 100, RuntimeCommandProvenanceKind.Exogenous, payload: 3),
                TypedCommand(10, 100, RuntimeCommandProvenanceKind.Derived, payload: 4),
                TypedCommand(10, 101, RuntimeCommandProvenanceKind.Exogenous, payload: 5),
                Command(RuntimeFrame.Zero, 0, 100, 1, TestTraceIds.Legacy)
            };

            RuntimeReplayFrameRecord record = recorder.RecordFrame(
                RuntimeFrame.Zero,
                commands,
                resultHash: 7L);
            commands.Clear();
            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();
            recorder.Clear();

            Assert.AreEqual(RuntimeReplayCommandCaptureMode.ExogenousFiltered, recorder.CaptureMode);
            Assert.AreEqual(1, record.Commands.Count);
            Assert.AreEqual(10, record.Commands[0].SourceId);
            Assert.AreEqual(2L, record.Commands[0].Envelope.Data0);
            Assert.AreEqual(1, snapshot.Records[0].Commands.Count);
            Assert.AreEqual(record.Commands[0], snapshot.Records[0].Commands[0]);
        }

        private static RuntimeCommand Command(RuntimeFrame frame, int sourceId, int commandId, int targetId, RuntimeTraceIdentity traceId)
        {
            return new RuntimeCommand(frame, sourceId, commandId, targetId, traceId: traceId);
        }

        private static RuntimeCommand TypedCommand(
            int sourceId,
            int commandId,
            RuntimeCommandProvenanceKind provenance,
            long payload = 0L)
        {
            return new RuntimeCommand(
                RuntimeFrame.Zero,
                sourceId,
                commandId,
                targetId: 1,
                envelope: new RuntimeCommandEnvelope(
                    typeId: 9001,
                    schemaVersion: 1,
                    provenance: provenance,
                    data0: payload));
        }

        private static RuntimeCommand TypedCommandAt(
            long frame,
            int sourceId,
            int commandId,
            long payload)
        {
            return new RuntimeCommand(
                new RuntimeFrame(frame),
                sourceId,
                commandId,
                targetId: 1,
                envelope: new RuntimeCommandEnvelope(
                    typeId: 9001,
                    schemaVersion: 1,
                    provenance: RuntimeCommandProvenanceKind.Exogenous,
                    data0: payload));
        }

        private static RuntimeCommandSubmissionGate CreateExternalGate(
            RuntimeCommandBuffer buffer)
        {
            var registry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(registry.TryRegister(
                10,
                RuntimeCommandSourceKind.Exogenous,
                new[] { 100 },
                out _));
            return new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);
        }

        private static void AssertAtomicFailure(
            RuntimeCommandSubmissionGate gate,
            RuntimeCommand[] commands,
            int expectedIndex,
            RuntimeCommandErrorCode expectedCode)
        {
            RuntimeCommandBatchSubmissionResult result = gate.SubmitBatch(commands);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(expectedIndex, result.FailedCommandIndex);
            Assert.AreEqual(expectedCode, result.Error.Code);
        }

        private static void AssertSnapshotsEqual(
            RuntimeCommandBufferSnapshot expected,
            RuntimeCommandBufferSnapshot actual)
        {
            Assert.AreEqual(expected.CurrentFrame, actual.CurrentFrame);
            Assert.AreEqual(expected.NextSequence, actual.NextSequence);
            Assert.AreEqual(expected.PendingCount, actual.PendingCount);
            for (int i = 0; i < expected.PendingCount; i++)
                Assert.AreEqual(expected.PendingCommands[i], actual.PendingCommands[i]);
        }

        private static void EnqueueDrainBatch(RuntimeCommandBuffer buffer, RuntimeFrame frame)
        {
            buffer.Enqueue(Command(frame, sourceId: 2, commandId: 1, targetId: 1, traceId: TestTraceIds.Source2));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 2, traceId: TestTraceIds.Target2));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.SameA));
            buffer.Enqueue(Command(frame, sourceId: 1, commandId: 1, targetId: 1, traceId: TestTraceIds.SameB));
        }

        private static RuntimeTraceIdentity[] TraceIds(IReadOnlyList<RuntimeCommand> commands)
        {
            var traceIds = new RuntimeTraceIdentity[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                traceIds[i] = commands[i].TraceId;
            }

            return traceIds;
        }

        private sealed class RejectingValidator : IRuntimeCommandValidator
        {
            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    RuntimeFrame.Zero,
                    "invalid"));
            }
        }

        private sealed class RejectPayloadValidator : IRuntimeCommandValidator
        {
            private readonly long _rejectedPayload;

            public RejectPayloadValidator(long rejectedPayload)
            {
                _rejectedPayload = rejectedPayload;
            }

            public RuntimeCommandValidationResult Validate(RuntimeCommand command)
            {
                return command.Envelope.Data0 == _rejectedPayload
                    ? RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                        RuntimeCommandErrorCode.InvalidPayload,
                        command,
                        RuntimeFrame.Zero,
                        "invalid"))
                    : RuntimeCommandValidationResult.Accepted(command);
            }
        }
    }
}
