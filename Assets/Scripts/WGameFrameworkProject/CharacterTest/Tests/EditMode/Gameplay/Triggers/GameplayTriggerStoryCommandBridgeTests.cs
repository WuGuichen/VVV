using System;
using System.Reflection;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.StoryBridge;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.Tests.Story;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerStoryCommandBridgeTests
    {
        private const int TargetSourceBindingId = 730;
        private const int ExplicitTargetSlotId = 1;
        private static readonly GameplayTriggerSourceBindingId InputSourceBindingId =
            new GameplayTriggerSourceBindingId(731);

        [Test]
        public void PublicContract_FreezesIdsDeliveryAndStoryResultShape()
        {
            Assert.AreEqual(
                StoryRuntimeCommandIds.RaiseTrigger,
                GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue);
            Assert.AreEqual(
                GameplayTriggerDeliveryContract.ReliableDeferred,
                GameplayTriggerStoryCommandActionKinds.DeliveryContract);
            Assert.AreEqual(1002105, GameplayTriggerStoryCommandContracts.EnvelopeTypeId);
            Assert.AreEqual(1, GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion);
            Assert.AreEqual(1001, GameplayTriggerStoryCommandContracts.TargetHostNotStartedDetailCode);
            Assert.AreEqual(1002, GameplayTriggerStoryCommandContracts.TargetModuleNotRegisteredDetailCode);

            FieldInfo[] fields = typeof(StoryRuntimeCommandResult).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                Assert.AreNotEqual(typeof(string), type, fields[i].Name);
                Assert.AreNotEqual(typeof(object), type, fields[i].Name);
                Assert.IsFalse(typeof(Delegate).IsAssignableFrom(type), fields[i].Name);
                Assert.IsFalse(
                    type.Namespace != null &&
                    type.Namespace.Contains("Gameplay.Triggers"),
                    fields[i].Name);
            }
        }

        [Test]
        public void StructuralPreflight_FreezesTargetAndPayloadBoundariesBeforeMutation()
        {
            var sink = new GameplayTriggerStoryReliableCommandActionSink(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
            Assert.IsTrue(sink.TryValidateReliableAction(Preparation(
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                    int.MaxValue,
                    0),
                int.MaxValue,
                int.MinValue,
                int.MaxValue)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayTriggerSourceBindingDomainId,
                    1,
                    1),
                1L,
                0L,
                0L)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                new GameplayTriggerTargetRef(
                    GameplayTriggerTargetRef.GameplayEntityDomainId,
                    1,
                    1),
                1L,
                0L,
                0L)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                StoryTarget(),
                0L,
                0L,
                0L)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                StoryTarget(),
                (long)int.MaxValue + 1L,
                0L,
                0L)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                StoryTarget(),
                1L,
                (long)int.MaxValue + 1L,
                0L)));
            Assert.IsFalse(sink.TryValidateReliableAction(Preparation(
                StoryTarget(),
                1L,
                0L,
                (long)int.MinValue - 1L)));

            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                triggerId: 0,
                param0: 0,
                param1: 0,
                new GameplayTriggerRuntimeEventSource(8));
            Queue(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));

            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.IntentCount);
            Assert.AreEqual(0, runtime.RouteStateCount);
            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, runtime.NextActivationId);
            Assert.AreEqual(1L, runtime.NextReliableOutboxSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void LegacyReliableSink_RemainsBypassedAndQueuesUnchanged()
        {
            var sink = new LegacyReliableSink(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                StoryTestGraphs.TriggerId,
                -2,
                3,
                new GameplayTriggerRuntimeEventSource(8));

            Queue(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));

            Assert.AreEqual(0, sink.TryPrepareCount);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            Assert.AreEqual(StoryTestGraphs.TriggerId, record.ActionPayload0);
            Assert.AreEqual(-2L, record.ActionPayload1);
            Assert.AreEqual(3L, record.ActionPayload2);
        }

        [Test]
        public void TransferComposition_RejectsEveryApproximateComposition()
        {
            var director = new StoryDirector();
            var validator = new StoryRuntimeCommandValidator(director);
            RuntimeCommandBuffer bounded = RuntimeCommandBuffer.CreateBounded(
                validator,
                RuntimeFrame.Zero,
                1);
            var module = new StoryRuntimeModule(director, bounded, null, validator);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    new GameplayTriggerActionKindId(StoryRuntimeCommandIds.SelectChoice),
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    module,
                    CreateGate(bounded, RuntimeCommandIngressKind.TrustedDerived)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge + 1,
                    new RuntimeHost(),
                    module,
                    CreateGate(bounded, RuntimeCommandIngressKind.TrustedDerived)));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    module,
                    CreateGate(bounded, RuntimeCommandIngressKind.External)));

            RuntimeCommandBuffer unbounded = new RuntimeCommandBuffer(validator);
            var unboundedModule = new StoryRuntimeModule(
                director,
                unbounded,
                null,
                validator);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    unboundedModule,
                    CreateGate(unbounded, RuntimeCommandIngressKind.TrustedDerived)));

            RuntimeCommandBuffer other = RuntimeCommandBuffer.CreateBounded(
                validator,
                RuntimeFrame.Zero,
                1);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    module,
                    CreateGate(other, RuntimeCommandIngressKind.TrustedDerived)));

            var mismatchedValidator = new StoryRuntimeCommandValidator(director);
            RuntimeCommandBuffer mismatchedBuffer = RuntimeCommandBuffer.CreateBounded(
                mismatchedValidator,
                RuntimeFrame.Zero,
                1);
            var mismatchedModule = new StoryRuntimeModule(
                director,
                mismatchedBuffer,
                null,
                validator);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    mismatchedModule,
                    CreateGate(mismatchedBuffer, RuntimeCommandIngressKind.TrustedDerived)));

            var customValidator = new StoryRuntimeCommandValidator(
                director,
                StoryRuntimeCommandRegistry.CreateDefault());
            RuntimeCommandBuffer customBuffer = RuntimeCommandBuffer.CreateBounded(
                customValidator,
                RuntimeFrame.Zero,
                1);
            var customModule = new StoryRuntimeModule(
                director,
                customBuffer,
                null,
                customValidator);
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    customModule,
                    CreateGate(customBuffer, RuntimeCommandIngressKind.TrustedDerived)));

            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    module,
                    CreateGate(
                        bounded,
                        RuntimeCommandIngressKind.TrustedDerived,
                        RuntimeCommandSourceKind.Exogenous)));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    new RuntimeHost(),
                    module,
                    CreateGate(
                        bounded,
                        RuntimeCommandIngressKind.TrustedDerived,
                        RuntimeCommandSourceKind.Derived,
                        StoryRuntimeCommandIds.SelectChoice)));
        }

        [Test]
        public void TransferRuntimeIdentity_ReturnsExactUnavailableDetails()
        {
            GameplayTriggerRuntime runtime = QueuedRuntime();
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            var director = new StoryDirector();
            var validator = new StoryRuntimeCommandValidator(director);
            RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(
                validator,
                RuntimeFrame.Zero,
                1);
            var module = new StoryRuntimeModule(director, buffer, null, validator);
            var host = new RuntimeHost();
            var transfer = new GameplayTriggerStoryReliableCommandTransfer(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                StoryRuntimeCommandSources.GameplayBridge,
                host,
                module,
                CreateGate(buffer, RuntimeCommandIngressKind.TrustedDerived));

            GameplayTriggerReliableTransferResult notStarted = transfer.TryTransfer(
                new RuntimeFrame(1),
                in record);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                notStarted.Status);
            Assert.AreEqual(
                GameplayTriggerStoryCommandContracts.TargetHostNotStartedDetailCode,
                notStarted.DetailCode);

            var impostorDirector = new StoryDirector();
            var impostorValidator = new StoryRuntimeCommandValidator(impostorDirector);
            var impostor = new StoryRuntimeModule(
                impostorDirector,
                RuntimeCommandBuffer.CreateBounded(
                    impostorValidator,
                    RuntimeFrame.Zero,
                    1),
                null,
                impostorValidator);
            host.RegisterModule(impostor);
            host.Initialize();
            host.Start();

            GameplayTriggerReliableTransferResult missing = transfer.TryTransfer(
                new RuntimeFrame(1),
                in record);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                missing.Status);
            Assert.AreEqual(
                GameplayTriggerStoryCommandContracts.TargetModuleNotRegisteredDetailCode,
                missing.DetailCode);
            host.Dispose();
        }

        [Test]
        public void FullTargetRetainsWithoutSequenceConsumptionThenAcceptsExactlyOnce()
        {
            using (StoryBridgeHarness harness = StoryBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: true,
                loadStoryGraph: true))
            {
                harness.Queue(RuntimeFrame.Zero);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(1, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetCapacityExceeded,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
                Assert.AreEqual((int)RuntimeCommandErrorCode.PendingCapacityExceeded,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.DetailCode);
                Assert.AreEqual(1L, harness.CommandBuffer.CaptureSnapshot().NextSequence);
                Assert.AreEqual(0, harness.CountActionResults());

                harness.Host.Tick(2L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(2L, harness.CommandBuffer.CaptureSnapshot().NextSequence);
                Assert.AreEqual(1, harness.CountActionResults());
                harness.Host.Tick(3L, 0d);
                Assert.AreEqual(1, harness.CountActionResults());
            }
        }

        [Test]
        public void DisabledTransferRetainsAndReenableCompletes()
        {
            using (StoryBridgeHarness harness = StoryBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: false,
                loadStoryGraph: true))
            {
                harness.Queue(RuntimeFrame.Zero);
                harness.Transfer.SetEnabled(false);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(1, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetDisabled,
                    harness.Runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
                Assert.AreEqual(0L, harness.CommandBuffer.CaptureSnapshot().NextSequence);

                harness.Transfer.SetEnabled(true);
                harness.Host.Tick(2L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(1L, harness.CommandBuffer.CaptureSnapshot().NextSequence);
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded,
                    harness.OnlyActionResult().Status);
            }
        }

        [Test]
        public void MultipleRecordsPreserveCanonicalOutboxAndStoryCommandOrder()
        {
            using (StoryBridgeHarness harness = StoryBridgeHarness.Create(
                commandCapacity: 2,
                prefillFrameOne: false,
                loadStoryGraph: false))
            {
                QueueMany(
                    harness.Runtime,
                    RuntimeFrame.Zero,
                    new GameplayEntityId(2, 1),
                    new GameplayEntityId(1, 1));
                Assert.AreEqual(2, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(1L, harness.Runtime.GetReliableOutboxRecord(0).Sequence);
                Assert.AreEqual(2L, harness.Runtime.GetReliableOutboxRecord(1).Sequence);

                harness.Host.Tick(1L, 0d);

                Assert.AreEqual(0, harness.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(2, harness.Module.LastDrainedCommands.Count);
                Assert.AreEqual(1L, harness.Module.LastDrainedCommands[0].Envelope.Data3);
                Assert.AreEqual(2L, harness.Module.LastDrainedCommands[1].Envelope.Data3);
                GameplayTriggerRuntimeEvent first = harness.ActionResultAt(0);
                GameplayTriggerRuntimeEvent second = harness.ActionResultAt(1);
                Assert.Less(first.SourceSequence, second.SourceSequence);
                Assert.Less(first.ActivationId, second.ActivationId);
            }
        }

        [Test]
        public void EndToEndSuccessAndSemanticRejectionsAreTerminalAndExact()
        {
            using (StoryBridgeHarness success = StoryBridgeHarness.Create(
                2,
                false,
                true,
                param0: int.MinValue,
                param1: int.MaxValue))
            {
                success.Queue(RuntimeFrame.Zero);
                success.Host.Tick(1L, 0d);

                GameplayTriggerRuntimeEvent result = success.OnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Succeeded, result.Status);
                Assert.AreEqual((int)StoryDirectorResultCode.Success, result.ReasonCode);
                Assert.AreEqual(StoryTestGraphs.GraphId, result.ResultPayload0);
                Assert.AreEqual(StoryTestGraphs.EntryBeatId, result.ResultPayload1);
                Assert.Greater(result.ResultPayload2, 0L);
                Assert.AreEqual(StoryTarget(), result.Target);
                Assert.AreEqual(0, success.Runtime.ReliableOutboxRecordCount);
            }

            using (StoryBridgeHarness missing = StoryBridgeHarness.Create(1, false, false))
            {
                missing.Queue(RuntimeFrame.Zero);
                missing.Host.Tick(1L, 0d);

                GameplayTriggerRuntimeEvent result = missing.OnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)StoryDirectorResultCode.TriggerNotFound, result.ReasonCode);
                Assert.AreEqual(StoryTestGraphs.TriggerId, result.ResultPayload0);
                Assert.AreEqual(0L, result.ResultPayload1);
                Assert.AreEqual(0L, result.ResultPayload2);
                Assert.AreEqual(0, missing.Runtime.ReliableOutboxRecordCount);
            }

            using (StoryBridgeHarness guarded = StoryBridgeHarness.Create(
                1,
                false,
                false,
                graph: GuardLoopGraph()))
            {
                guarded.Queue(RuntimeFrame.Zero);
                guarded.Host.Tick(1L, 0d);

                GameplayTriggerRuntimeEvent result = guarded.OnlyActionResult();
                Assert.AreEqual(GameplayTriggerRuntimeEventStatus.Rejected, result.Status);
                Assert.AreEqual((int)StoryDirectorResultCode.DirectorGuardExceeded,
                    result.ReasonCode);
                Assert.AreEqual(StoryTestGraphs.TriggerId, result.ResultPayload0);
                Assert.AreEqual(0, guarded.Runtime.ReliableOutboxRecordCount);
            }
        }

        [Test]
        public void StrictRelayIgnoresInvalidMatrixBeforeSourceAndOverflowIsPresentationOnly()
        {
            var eventSource = new GameplayTriggerRuntimeEventSource(0);
            CreateRuntime(
                new GameplayTriggerStoryReliableCommandActionSink(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger),
                StoryTestGraphs.TriggerId,
                0,
                0,
                eventSource);
            var relay = new GameplayTriggerStoryCommandResultRelay(eventSource);

            relay.Publish(default);
            StoryRuntimeCommandResult unrelated = ValidResult(
                sourceId: StoryRuntimeCommandSources.GameplayBridge + 1);
            relay.Publish(in unrelated);
            StoryRuntimeCommandResult unknownCode = ValidResult(
                status: StoryRuntimeCommandResultStatus.Rejected,
                code: StoryDirectorResultCode.GraphNotLoaded,
                result0: StoryTestGraphs.TriggerId,
                result1: 0L,
                result2: 0L);
            relay.Publish(in unknownCode);
            StoryRuntimeCommandResult invalidEnvelope = ValidResult(
                envelope: new RuntimeCommandEnvelope(
                    GameplayTriggerStoryCommandContracts.EnvelopeTypeId,
                    GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion,
                    RuntimeCommandProvenanceKind.Derived,
                    1L,
                    0L,
                    GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue,
                    0L,
                    0L,
                    1L,
                    0L,
                    0L));
            relay.Publish(in invalidEnvelope);

            Assert.AreEqual(0L, eventSource.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.None,
                eventSource.LastDropReason);

            StoryRuntimeCommandResult valid = ValidResult();
            relay.Publish(in valid);
            Assert.AreEqual(1L, eventSource.DroppedCount);
            Assert.AreEqual(GameplayTriggerRuntimeEventDropReason.SourceCapacityExceeded,
                eventSource.LastDropReason);
        }

        [Test]
        public void EndOfTickCheckpointRoundTripRegeneratesOnceAndFiltersDerivedCommand()
        {
            var sourceEvents = new GameplayTriggerRuntimeEventSource(32);
            var sourceSink = new GameplayTriggerStoryReliableCommandActionSink(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
            GameplayTriggerRuntime sourceRuntime = CreateRuntime(
                sourceSink,
                StoryTestGraphs.TriggerId,
                0,
                0,
                sourceEvents);
            var sourceDirector = new StoryDirector();
            sourceDirector.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
            using (StoryBridgeHarness source = StoryBridgeHarness.CreateForRuntime(
                sourceRuntime,
                sourceEvents,
                sourceDirector,
                commandCapacity: 1))
            {
                source.Queue(RuntimeFrame.Zero);
                source.Host.Tick(0L, 0d);
                Assert.IsTrue(source.Host.LastTickAttemptSucceeded);
                Assert.AreEqual(1, source.Runtime.ReliableOutboxRecordCount);
                Assert.AreEqual(0, source.CommandBuffer.PendingCount);

                RuntimeSaveState checkpoint = CaptureCheckpoint(source);
                string beforeJson = RuntimeSaveStateJson.SaveToJson(checkpoint);
                long beforeTriggerHash = TriggerHash(source.Runtime);
                long beforeStoryHash = StoryHash(source.Director, RuntimeFrame.Zero);
                Assert.AreEqual(0L, checkpoint.Frame);
                Assert.AreEqual(2, checkpoint.ModuleStates.Count);
                Assert.AreEqual(
                    GameplayTriggerRuntimeSaveStateParticipant.CurrentPayloadSchemaVersion,
                    FindModule(checkpoint, GameplayTriggerRuntimeSaveStateParticipant.ModuleId)
                        .SchemaVersion);
                Assert.AreEqual(
                    StoryRuntimeSaveStateProvider.SchemaVersion,
                    FindModule(checkpoint, StoryRuntimeSaveStateProvider.ModuleId)
                        .SchemaVersion);
                StringAssert.DoesNotContain("envelopeTypeId", beforeJson);
                StringAssert.DoesNotContain("commandSequence", beforeJson);
                StringAssert.DoesNotContain("resultPayload", beforeJson);

                var restoredEvents = new GameplayTriggerRuntimeEventSource(32);
                var restoredSink = new GameplayTriggerStoryReliableCommandActionSink(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
                GameplayTriggerRuntime restoredRuntime = CreateRuntime(
                    restoredSink,
                    StoryTestGraphs.TriggerId,
                    0,
                    0,
                    restoredEvents);
                var restoredDirector = new StoryDirector();
                RestoreCheckpoint(checkpoint, restoredRuntime, restoredDirector);
                Assert.AreEqual(beforeTriggerHash, TriggerHash(restoredRuntime));
                Assert.AreEqual(
                    beforeStoryHash,
                    StoryHash(restoredDirector, RuntimeFrame.Zero));

                using (StoryBridgeHarness restored = StoryBridgeHarness.CreateForRuntime(
                    restoredRuntime,
                    restoredEvents,
                    restoredDirector,
                    commandCapacity: 1))
                {
                    restored.Host.Tick(1L, 0d);

                    Assert.AreEqual(0, restored.Runtime.ReliableOutboxRecordCount);
                    Assert.AreEqual(1L, restored.CommandBuffer.CaptureSnapshot().NextSequence);
                    Assert.AreEqual(1, restored.CountActionResults());
                    Assert.AreEqual(1, restored.Module.LastDrainedCommands.Count);
                    RuntimeCommand derived = restored.Module.LastDrainedCommands[0];
                    restored.Host.Tick(2L, 0d);
                    Assert.AreEqual(1L, restored.CommandBuffer.CaptureSnapshot().NextSequence);
                    Assert.AreEqual(1, restored.CountActionResults());

                    RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                        new RuntimeReplayHeader(
                            1,
                            "trigger-story-bridge",
                            "config",
                            "resources",
                            RuntimeFrame.Zero),
                        restored.SourceRegistry);
                    RuntimeReplayFrameRecord replayFrame = recorder.RecordFrame(
                        new RuntimeFrame(1),
                        new[] { derived },
                        0L);
                    Assert.AreEqual(RuntimeReplayCommandCaptureMode.ExogenousFiltered,
                        recorder.CaptureMode);
                    Assert.AreEqual(0, replayFrame.Commands.Count);
                }
            }
        }

        [Test]
        public void BridgeOwnedPreflightRetainAcceptAndRelayAllocateZeroAfterWarmup()
        {
            var sink = new GameplayTriggerStoryReliableCommandActionSink(
                GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
            GameplayTriggerActionPreparation preparation = Preparation(
                StoryTarget(),
                StoryTestGraphs.TriggerId,
                int.MinValue,
                int.MaxValue);
            Assert.IsTrue(sink.TryValidateReliableAction(in preparation));
            long preflightBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                sink.TryValidateReliableAction(in preparation);
            Assert.AreEqual(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - preflightBefore);

            using (StoryBridgeHarness harness = StoryBridgeHarness.Create(
                commandCapacity: 1,
                prefillFrameOne: false,
                loadStoryGraph: false))
            {
                harness.Queue(RuntimeFrame.Zero);
                GameplayTriggerReliableOutboxRecord record =
                    harness.Runtime.GetReliableOutboxRecord(0);
                var drained = new System.Collections.Generic.List<RuntimeCommand>(1);

                Assert.IsTrue(harness.Transfer.TryTransfer(
                    new RuntimeFrame(1),
                    in record).Accepted);
                harness.CommandBuffer.DrainForFrame(new RuntimeFrame(1), drained);
                drained.Clear();

                long acceptedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    RuntimeFrame frame = harness.CommandBuffer.CurrentFrame;
                    harness.Transfer.TryTransfer(frame, in record);
                    harness.CommandBuffer.DrainForFrame(frame, drained);
                    drained.Clear();
                }
                Assert.AreEqual(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - acceptedBefore);

                Assert.IsTrue(harness.CommandBuffer.Enqueue(
                    StoryRuntimeCommandFactory.RaiseTrigger(
                        harness.CommandBuffer.CurrentFrame,
                        StoryRuntimeCommandSources.TestDriver,
                        999999)).Success);
                harness.Transfer.TryTransfer(harness.CommandBuffer.CurrentFrame, in record);
                long retainedBefore = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                    harness.Transfer.TryTransfer(harness.CommandBuffer.CurrentFrame, in record);
                Assert.AreEqual(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - retainedBefore);
            }

            var relayEvents = new GameplayTriggerRuntimeEventSource(1);
            CreateRuntime(sink, StoryTestGraphs.TriggerId, 0, 0, relayEvents);
            var relay = new GameplayTriggerStoryCommandResultRelay(relayEvents);
            StoryRuntimeCommandResult valid = ValidResult();
            relay.Publish(in valid);
            relayEvents.ConsumePrefix(1);
            long relayBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                relay.Publish(in valid);
                relayEvents.ConsumePrefix(1);
            }
            Assert.AreEqual(
                0L,
                GC.GetAllocatedBytesForCurrentThread() - relayBefore);
        }

        private static GameplayTriggerActionPreparation Preparation(
            GameplayTriggerTargetRef target,
            long triggerId,
            long param0,
            long param1)
        {
            return new GameplayTriggerActionPreparation(
                RuntimeFrame.Zero,
                new GameplayTriggerBindingId(1),
                new GameplayEntityId(99, 1),
                target,
                new GameplayTriggerRouteId(1),
                0,
                default,
                new GameplayTriggerActionInstruction(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    ExplicitTargetSlotId,
                    triggerId,
                    param0,
                    param1));
        }

        private static GameplayTriggerTargetRef StoryTarget()
        {
            return GameplayTriggerTargetRef.FromTriggerSourceBinding(
                new GameplayTriggerSourceBindingId(TargetSourceBindingId));
        }

        private static GameplayTriggerRuntime CreateRuntime(
            IGameplayTriggerActionSink sink,
            long triggerId,
            long param0,
            long param1,
            GameplayTriggerRuntimeEventSource eventSource)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var action = new GameplayTriggerActionInstruction(
                sink.ActionKindId,
                GameplayTriggerTargetSelector.ExplicitBindingTarget,
                ExplicitTargetSlotId,
                triggerId,
                param0,
                param1);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[] { action },
                new GameplayTriggerActivationPolicy(
                    GameplayTriggerStateScope.PerSubject));
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                InputSourceBindingId,
                new GameplayEntityId(99, 1),
                true,
                new[]
                {
                    new GameplayTriggerBindingTarget(
                        ExplicitTargetSlotId,
                        StoryTarget())
                });
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            var options = new GameplayTriggerRuntimeOptions(
                8, 32, 32, 32, 32, 32, 32, 32, 8, 16, 0, 8);
            return new GameplayTriggerRuntime(catalog, options, eventSource);
        }

        private static GameplayTriggerRuntime QueuedRuntime()
        {
            var eventSource = new GameplayTriggerRuntimeEventSource(8);
            GameplayTriggerRuntime runtime = CreateRuntime(
                new GameplayTriggerStoryReliableCommandActionSink(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger),
                StoryTestGraphs.TriggerId,
                0,
                0,
                eventSource);
            Queue(runtime, RuntimeFrame.Zero, new GameplayEntityId(1, 1));
            return runtime;
        }

        private static void Queue(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame,
            GameplayEntityId subject)
        {
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                InputSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1, subject) },
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(frame).Success);
        }

        private static void QueueMany(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame,
            params GameplayEntityId[] subjects)
        {
            var signals = new GameplayTriggerSignal[subjects.Length];
            for (int i = 0; i < subjects.Length; i++)
                signals[i] = new GameplayTriggerSignal(1, subjects[i]);
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                InputSourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(frame).Success);
        }

        private static RuntimeCommandSubmissionGate CreateGate(
            RuntimeCommandBuffer buffer,
            RuntimeCommandIngressKind ingressKind,
            RuntimeCommandSourceKind sourceKind = RuntimeCommandSourceKind.Derived,
            int commandId = StoryRuntimeCommandIds.RaiseTrigger)
        {
            var registry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(registry.TryRegister(
                StoryRuntimeCommandSources.GameplayBridge,
                sourceKind,
                new[] { commandId },
                out RuntimeCommandSourceRegistrationResult registration),
                registration.ErrorCode.ToString());
            return new RuntimeCommandSubmissionGate(registry, buffer, ingressKind);
        }

        private static StoryRuntimeCommandResult ValidResult(
            int sourceId = StoryRuntimeCommandSources.GameplayBridge,
            StoryRuntimeCommandResultStatus status =
                StoryRuntimeCommandResultStatus.Succeeded,
            StoryDirectorResultCode code = StoryDirectorResultCode.Success,
            long result0 = StoryTestGraphs.GraphId,
            long result1 = StoryTestGraphs.ChoiceBeatId,
            long result2 = 1L,
            RuntimeCommandEnvelope envelope = default)
        {
            if (!envelope.IsPresent)
            {
                envelope = new RuntimeCommandEnvelope(
                    GameplayTriggerStoryCommandContracts.EnvelopeTypeId,
                    GameplayTriggerStoryCommandContracts.EnvelopeSchemaVersion,
                    RuntimeCommandProvenanceKind.Derived,
                    1L,
                    0L,
                    GameplayTriggerStoryCommandActionKinds.RaiseTriggerValue,
                    1L,
                    0L,
                    1L,
                    0L,
                    0L);
            }
            return new StoryRuntimeCommandResult(
                new RuntimeFrame(1),
                sourceId,
                StoryRuntimeCommandIds.RaiseTrigger,
                TargetSourceBindingId,
                StoryTestGraphs.TriggerId,
                0,
                0,
                0L,
                true,
                envelope,
                status,
                code,
                result0,
                result1,
                result2);
        }

        private static StoryGraphDefinition GuardLoopGraph()
        {
            const int graphId = 910;
            const int beatId = 911;
            return new StoryGraphDefinition(
                graphId,
                1,
                beatId,
                new[]
                {
                    new StoryBeatDefinition(
                        beatId,
                        Array.Empty<StoryStepDefinition>(),
                        branches: new[]
                        {
                            new StoryBranchDefinition(
                                1,
                                beatId,
                                isFallback: true)
                        },
                        triggerIds: new[] { StoryTestGraphs.TriggerId })
                });
        }

        private static GameplayTriggerRuntimeSaveStateParticipant TriggerParticipant(
            GameplayTriggerRuntime runtime)
        {
            return new GameplayTriggerRuntimeSaveStateParticipant(
                runtime,
                new RestoreEntityValidator());
        }

        private static RuntimeSaveState CaptureCheckpoint(StoryBridgeHarness harness)
        {
            GameplayTriggerRuntimeSaveStateParticipant trigger =
                TriggerParticipant(harness.Runtime);
            var story = new StoryRuntimeSaveStateProvider(
                harness.Director,
                () => harness.Host.LastTickAttemptFrame);
            var registry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(registry.Register(
                GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
                trigger,
                trigger).Success);
            Assert.IsTrue(registry.Register(
                StoryRuntimeSaveStateProvider.ModuleId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder + 1,
                story,
                story).Success);
            var quiescence = new RuntimeCheckpointQuiescenceRegistry();
            Assert.IsTrue(quiescence.Register(
                GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultQuiescenceOrder,
                trigger).Success);
            var barrier = new RuntimeCheckpointBarrier(
                harness.Host,
                new RuntimeSaveStateCoordinator(registry),
                quiescence);
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> capture =
                barrier.CaptureCheckpoint();
            Assert.IsTrue(capture.Success,
                capture.FirstError == null ? string.Empty : capture.FirstError.ToString());
            return capture.Value;
        }

        private static void RestoreCheckpoint(
            RuntimeSaveState checkpoint,
            GameplayTriggerRuntime runtime,
            StoryDirector director)
        {
            GameplayTriggerRuntimeSaveStateParticipant trigger = TriggerParticipant(runtime);
            var story = new StoryRuntimeSaveStateProvider(director);
            var registry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(registry.Register(
                GameplayTriggerRuntimeSaveStateParticipant.ParticipantId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder,
                trigger,
                trigger).Success);
            Assert.IsTrue(registry.Register(
                StoryRuntimeSaveStateProvider.ModuleId,
                GameplayTriggerRuntimeSaveStateParticipant.DefaultSaveStateOrder + 1,
                story,
                story).Success);
            RuntimeSaveStateCoordinatorResult<bool> restore =
                new RuntimeSaveStateCoordinator(registry).RestoreSaveState(checkpoint);
            Assert.IsTrue(restore.Success,
                restore.FirstError == null ? string.Empty : restore.FirstError.ToString());
        }

        private static RuntimeModuleSaveState FindModule(
            RuntimeSaveState state,
            string moduleId)
        {
            for (int i = 0; i < state.ModuleStates.Count; i++)
            {
                if (state.ModuleStates[i].ModuleId == moduleId)
                    return state.ModuleStates[i];
            }
            Assert.Fail("Missing module state: " + moduleId);
            return null;
        }

        private static long TriggerHash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.GameplayTriggers, RuntimeHashSchemaIds.Version1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(runtime.AuthorityLastEvaluatedFrame),
                accumulator);
            return accumulator.ToHash();
        }

        private static long StoryHash(StoryDirector director, RuntimeFrame frame)
        {
            return RuntimeHashCombiner.ComputeHash(
                frame,
                new IRuntimeHashContributor[]
                {
                    new StoryRuntimeHashContributor(director)
                });
        }

        private sealed class LegacyReliableSink : IGameplayTriggerActionSink
        {
            public LegacyReliableSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.ReliableDeferred;
            public int TryPrepareCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                TryPrepareCount++;
                token = 0;
                throw new InvalidOperationException(
                    "Legacy reliable callbacks must remain bypassed.");
            }

            public void Abort(int token) =>
                throw new InvalidOperationException(
                    "Legacy reliable callbacks must remain bypassed.");

            public void Commit(int token, in GameplayTriggerActionIntent intent) =>
                throw new InvalidOperationException(
                    "Legacy reliable callbacks must remain bypassed.");
        }

        private sealed class RestoreEntityValidator :
            IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId) =>
                entityId.IsValid;
        }

        private sealed class StoryBridgeHarness : IDisposable
        {
            private StoryBridgeHarness(
                GameplayTriggerRuntime runtime,
                GameplayTriggerRuntimeEventSource eventSource,
                StoryDirector director,
                RuntimeHost host,
                RuntimeCommandSourceRegistry sourceRegistry,
                RuntimeCommandBuffer commandBuffer,
                StoryRuntimeModule module,
                GameplayTriggerStoryReliableCommandTransfer transfer)
            {
                Runtime = runtime;
                EventSource = eventSource;
                Director = director;
                Host = host;
                SourceRegistry = sourceRegistry;
                CommandBuffer = commandBuffer;
                Module = module;
                Transfer = transfer;
            }

            public GameplayTriggerRuntime Runtime { get; }
            public GameplayTriggerRuntimeEventSource EventSource { get; }
            public StoryDirector Director { get; }
            public RuntimeHost Host { get; }
            public RuntimeCommandSourceRegistry SourceRegistry { get; }
            public RuntimeCommandBuffer CommandBuffer { get; }
            public StoryRuntimeModule Module { get; }
            public GameplayTriggerStoryReliableCommandTransfer Transfer { get; }

            public static StoryBridgeHarness Create(
                int commandCapacity,
                bool prefillFrameOne,
                bool loadStoryGraph,
                int param0 = 0,
                int param1 = 0,
                StoryGraphDefinition graph = null)
            {
                var eventSource = new GameplayTriggerRuntimeEventSource(64);
                var sink = new GameplayTriggerStoryReliableCommandActionSink(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger);
                GameplayTriggerRuntime runtime = CreateRuntime(
                    sink,
                    StoryTestGraphs.TriggerId,
                    param0,
                    param1,
                    eventSource);
                var director = new StoryDirector();
                if (loadStoryGraph)
                    director.LoadGraph(StoryTestGraphs.MinimalChoiceGraph());
                if (graph != null)
                    director.LoadGraph(graph);
                return CreateForRuntime(
                    runtime,
                    eventSource,
                    director,
                    commandCapacity,
                    prefillFrameOne);
            }

            public static StoryBridgeHarness CreateForRuntime(
                GameplayTriggerRuntime runtime,
                GameplayTriggerRuntimeEventSource eventSource,
                StoryDirector director,
                int commandCapacity,
                bool prefillFrameOne = false)
            {
                var validator = new StoryRuntimeCommandValidator(director);
                RuntimeCommandBuffer buffer = RuntimeCommandBuffer.CreateBounded(
                    validator,
                    RuntimeFrame.Zero,
                    commandCapacity);
                if (prefillFrameOne)
                {
                    Assert.IsTrue(buffer.Enqueue(
                        StoryRuntimeCommandFactory.RaiseTrigger(
                            new RuntimeFrame(1),
                            StoryRuntimeCommandSources.TestDriver,
                            999999)).Success);
                }
                var sourceRegistry = new RuntimeCommandSourceRegistry(1);
                Assert.IsTrue(sourceRegistry.TryRegister(
                    StoryRuntimeCommandSources.GameplayBridge,
                    RuntimeCommandSourceKind.Derived,
                    new[] { StoryRuntimeCommandIds.RaiseTrigger },
                    out RuntimeCommandSourceRegistrationResult registration),
                    registration.ErrorCode.ToString());
                var gate = new RuntimeCommandSubmissionGate(
                    sourceRegistry,
                    buffer,
                    RuntimeCommandIngressKind.TrustedDerived);
                var relay = new GameplayTriggerStoryCommandResultRelay(eventSource);
                var module = new StoryRuntimeModule(
                    director,
                    buffer,
                    null,
                    validator,
                    relay);
                var host = new RuntimeHost();
                var transfer = new GameplayTriggerStoryReliableCommandTransfer(
                    GameplayTriggerStoryCommandActionKinds.RaiseTrigger,
                    StoryRuntimeCommandSources.GameplayBridge,
                    host,
                    module,
                    gate);
                var outbox = new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[] { transfer });
                host.RegisterModule(outbox);
                host.RegisterModule(module);
                host.Initialize();
                host.Start();
                return new StoryBridgeHarness(
                    runtime,
                    eventSource,
                    director,
                    host,
                    sourceRegistry,
                    buffer,
                    module,
                    transfer);
            }

            public void Queue(RuntimeFrame frame)
            {
                GameplayTriggerStoryCommandBridgeTests.Queue(
                    Runtime,
                    frame,
                    new GameplayEntityId(1, 1));
            }

            public int CountActionResults()
            {
                int count = 0;
                for (int i = 0; i < EventSource.PendingCount; i++)
                {
                    if (EventSource.GetPending(i).Kind ==
                        GameplayTriggerRuntimeEventKind.ActionResult)
                    {
                        count++;
                    }
                }
                return count;
            }

            public GameplayTriggerRuntimeEvent OnlyActionResult()
            {
                Assert.AreEqual(1, CountActionResults());
                return ActionResultAt(0);
            }

            public GameplayTriggerRuntimeEvent ActionResultAt(int resultIndex)
            {
                int found = 0;
                for (int i = 0; i < EventSource.PendingCount; i++)
                {
                    GameplayTriggerRuntimeEvent evt = EventSource.GetPending(i);
                    if (evt.Kind != GameplayTriggerRuntimeEventKind.ActionResult)
                        continue;
                    if (found == resultIndex)
                        return evt;
                    found++;
                }
                Assert.Fail("Missing action result index " + resultIndex + ".");
                return default;
            }

            public void Dispose()
            {
                Host.Dispose();
            }
        }
    }
}
