using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerSystemIntegrationTests
    {
        private static readonly GameplayTriggerSourceDefinitionId SourceDefinitionId =
            new GameplayTriggerSourceDefinitionId(1);
        private static readonly GameplayTriggerDefinitionId DefinitionId =
            new GameplayTriggerDefinitionId(1);
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(1);
        private static readonly GameplayTriggerActionKindId ActionKindId =
            new GameplayTriggerActionKindId(1);

        [Test]
        public void CommandFactory_EncodesLifecyclePayloadWithoutSidecar()
        {
            var instanceId = new GameplayTriggerInstanceId(3, 7);

            RuntimeCommand enable = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                new RuntimeFrame(4),
                instanceId,
                true,
                sourceId: 9,
                traceId: CharacterTestRuntimeTraceIds.Enable);
            RuntimeCommand reset = GameplayTriggerRuntimeCommandFactory.Reset(
                new RuntimeFrame(5),
                instanceId,
                sourceId: 10,
                traceId: CharacterTestRuntimeTraceIds.Reset);

            Assert.AreEqual(GameplayTriggerRuntimeCommandIds.SetEnabled, enable.CommandId);
            Assert.AreEqual(3, enable.TargetId);
            Assert.AreEqual(7, enable.Payload0);
            Assert.AreEqual(1, enable.Payload1);
            Assert.AreEqual(0, enable.Payload2);
            Assert.AreEqual(GameplayTriggerRuntimeCommandIds.Reset, reset.CommandId);
            Assert.AreEqual(3, reset.TargetId);
            Assert.AreEqual(7, reset.Payload0);
            Assert.AreEqual(0, reset.Payload1);
            Assert.AreEqual(0, reset.Payload2);
        }

        [Test]
        public void SignalCommandCodec_RoundTripsEveryTypedField()
        {
            var signals = new[]
            {
                new GameplayTriggerSignal(
                    11,
                    new GameplayEntityId(3, 4),
                    new GameplayEntityId(5, 6),
                    long.MinValue,
                    0x102030405060708L,
                    long.MaxValue,
                    sequence: 7),
                new GameplayTriggerSignal(
                    12,
                    payload0: -9L,
                    payload1: 10L,
                    payload2: -11L,
                    sequence: 8)
            };
            var commands = new RuntimeCommand[signals.Length];

            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(4),
                sourceId: 21,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                batchId: 99L,
                signals,
                commands,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus encodeStatus,
                traceId: CharacterTestRuntimeTraceIds.SignalBatch));

            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.None, encodeStatus);
            Assert.AreEqual(2, written);
            for (int i = 0; i < written; i++)
            {
                Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                    commands[i],
                    out GameplayTriggerSignalCommandRecord decoded,
                    out GameplayTriggerSignalCommandCodecStatus decodeStatus));
                Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.None, decodeStatus);
                Assert.AreEqual(new RuntimeFrame(4), decoded.Frame);
                Assert.AreEqual(21, decoded.SourceId);
                Assert.AreEqual(SourceBindingId, decoded.SourceBindingId);
                Assert.AreEqual(99L, decoded.BatchId);
                Assert.AreEqual(GameplayTriggerSignalOrdering.Ordered, decoded.Ordering);
                Assert.AreEqual(i, decoded.ItemIndex);
                Assert.AreEqual(2, decoded.ItemCount);
                Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous, decoded.Provenance);
                AssertSignal(signals[i], decoded.Signal);
            }
        }

        [Test]
        public void SignalCommandCodec_RepresentsEmptyBatchExplicitly()
        {
            var commands = new RuntimeCommand[1];

            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                sourceId: 1,
                RuntimeCommandProvenanceKind.Derived,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 2L,
                ReadOnlySpan<GameplayTriggerSignal>.Empty,
                commands,
                out int written,
                out _));
            Assert.AreEqual(1, written);
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                commands[0],
                out GameplayTriggerSignalCommandRecord decoded,
                out _));
            Assert.IsTrue(decoded.IsEmptyBatch);
            Assert.AreEqual(-1, decoded.ItemIndex);
            Assert.AreEqual(0, decoded.ItemCount);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Derived, decoded.Provenance);
        }

        [Test]
        public void SignalCommandCodec_RejectsCapacitySchemaProvenanceAndEntityFailures()
        {
            var signal = new[] { new GameplayTriggerSignal(1) };
            Assert.IsFalse(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                1L,
                signal,
                Span<RuntimeCommand>.Empty,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus status));
            Assert.AreEqual(0, written);
            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.DestinationTooSmall, status);

            var missing = new RuntimeCommand(
                RuntimeFrame.Zero,
                1,
                GameplayTriggerRuntimeCommandIds.SubmitSignalBatch,
                SourceBindingId.Value,
                payload0: (int)GameplayTriggerSignalOrdering.Ordered,
                payload1: 0,
                payload2: 1);
            Assert.IsFalse(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                missing,
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.MissingEnvelope, status);

            RuntimeCommand wrongSchema = WithEnvelope(
                missing,
                new RuntimeCommandEnvelope(
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                    schemaVersion: 2,
                    RuntimeCommandProvenanceKind.Exogenous,
                    data0: 1L,
                    data1: 1L));
            Assert.IsFalse(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                wrongSchema,
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.UnsupportedSchema, status);

            RuntimeCommand unspecified = WithEnvelope(
                missing,
                new RuntimeCommandEnvelope(
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion,
                    RuntimeCommandProvenanceKind.Unspecified,
                    data0: 1L,
                    data1: 1L));
            Assert.IsFalse(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                unspecified,
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.InvalidProvenance, status);

            RuntimeCommand invalidEntity = WithEnvelope(
                missing,
                new RuntimeCommandEnvelope(
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion,
                    RuntimeCommandProvenanceKind.Exogenous,
                    data0: 1L,
                    data1: 1L,
                    data2: 1L));
            Assert.IsFalse(GameplayTriggerSignalCommandCodec.TryDecodeRecord(
                invalidEntity,
                out _,
                out status));
            Assert.AreEqual(GameplayTriggerSignalCommandCodecStatus.InvalidEntity, status);
        }

        [Test]
        public void RuntimeCommandEnvelope_SurvivesSequenceSnapshotAndReplayCopies()
        {
            var commands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                7L,
                new[] { new GameplayTriggerSignal(3, payload0: long.MinValue) },
                commands,
                out _,
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 1);
            RuntimeCommandValidationResult accepted = buffer.Enqueue(commands[0]);
            Assert.IsTrue(accepted.Success);
            Assert.AreEqual(commands[0].Envelope, accepted.Command.Envelope);
            Assert.AreEqual(0L, accepted.Command.Sequence);

            RuntimeCommandBufferSnapshot snapshot = buffer.CaptureSnapshot();
            Assert.AreEqual(commands[0].Envelope, snapshot.PendingCommands[0].Envelope);
            var replay = new RuntimeReplayFrameRecord(
                RuntimeFrame.Zero,
                new[] { accepted.Command },
                resultHash: 0L,
                diagnosticsSummary: string.Empty);
            Assert.AreEqual(commands[0].Envelope, replay.Commands[0].Envelope);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous,
                replay.Commands[0].Envelope.Provenance);

            RuntimeCommand differentEnvelope = WithEnvelope(
                commands[0],
                new RuntimeCommandEnvelope(
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion,
                    RuntimeCommandProvenanceKind.Exogenous,
                    data0: 8L,
                    data1: 3L,
                    data4: long.MinValue));
            Assert.AreNotEqual(commands[0], differentEnvelope);
        }

        [Test]
        public void CommandAuthorizationGate_AllowsSignalIntentAndRejectsRawAuthorityAndForgery()
        {
            const int externalSourceId = 21;
            const int derivedSourceId = 22;
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                externalSourceId,
                RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                out _));
            Assert.IsTrue(registry.TryRegister(
                derivedSourceId,
                RuntimeCommandSourceKind.Derived,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                out _));
            var buffer = new RuntimeCommandBuffer(initialCapacity: 1);
            var externalGate = new RuntimeCommandSubmissionGate(
                registry,
                buffer,
                RuntimeCommandIngressKind.External);
            var signalCommands = new RuntimeCommand[1];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                externalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 1L,
                new[] { new GameplayTriggerSignal(10) },
                signalCommands,
                out _,
                out _));

            RuntimeCommandValidationResult accepted = externalGate.Submit(signalCommands[0]);
            RuntimeCommand rawAuthority = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                RuntimeFrame.Zero,
                new GameplayTriggerInstanceId(1, 1),
                enabled: true,
                sourceId: externalSourceId);
            RuntimeCommandValidationResult missingProvenance = externalGate.Submit(rawAuthority);
            RuntimeCommandValidationResult allowlistRejected = externalGate.Submit(WithEnvelope(
                rawAuthority,
                new RuntimeCommandEnvelope(
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatch,
                    GameplayTriggerRuntimeEnvelopeIds.SignalBatchSchemaVersion,
                    RuntimeCommandProvenanceKind.Exogenous)));
            RuntimeCommand forgedDerived = WithEnvelope(
                signalCommands[0],
                new RuntimeCommandEnvelope(
                    signalCommands[0].Envelope.TypeId,
                    signalCommands[0].Envelope.SchemaVersion,
                    RuntimeCommandProvenanceKind.Derived,
                    signalCommands[0].Envelope.Data0,
                    signalCommands[0].Envelope.Data1,
                    signalCommands[0].Envelope.Data2,
                    signalCommands[0].Envelope.Data3,
                    signalCommands[0].Envelope.Data4,
                    signalCommands[0].Envelope.Data5,
                    signalCommands[0].Envelope.Data6,
                    signalCommands[0].Envelope.Data7));
            RuntimeCommandValidationResult provenanceRejected = externalGate.Submit(forgedDerived);
            RuntimeCommand forgedDerivedSource = new RuntimeCommand(
                forgedDerived.Frame,
                derivedSourceId,
                forgedDerived.CommandId,
                forgedDerived.TargetId,
                forgedDerived.Payload0,
                forgedDerived.Payload1,
                forgedDerived.Payload2,
                forgedDerived.TraceId,
                envelope: forgedDerived.Envelope);
            RuntimeCommandValidationResult sourceRejected = externalGate.Submit(forgedDerivedSource);

            Assert.IsTrue(accepted.Success);
            Assert.AreEqual(RuntimeCommandErrorCode.ProvenanceMismatch, missingProvenance.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedCommand, allowlistRejected.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.ProvenanceMismatch, provenanceRejected.Error.Code);
            Assert.AreEqual(RuntimeCommandErrorCode.UnauthorizedSource, sourceRejected.Error.Code);
            Assert.AreEqual(1, buffer.PendingCount);
        }

        [Test]
        public void ExogenousReplayStream_PreservesSignalRecordsAndExcludesDerivedBatch()
        {
            const int externalSourceId = 21;
            const int derivedSourceId = 22;
            var registry = new RuntimeCommandSourceRegistry(initialCapacity: 2);
            Assert.IsTrue(registry.TryRegister(
                externalSourceId,
                RuntimeCommandSourceKind.Exogenous,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                out _));
            Assert.IsTrue(registry.TryRegister(
                derivedSourceId,
                RuntimeCommandSourceKind.Derived,
                new[] { GameplayTriggerRuntimeCommandIds.SubmitSignalBatch },
                out _));
            var exogenous = new RuntimeCommand[2];
            var derived = new RuntimeCommand[1];
            var signals = new[]
            {
                new GameplayTriggerSignal(10, payload0: 100L, sequence: 1L),
                new GameplayTriggerSignal(11, payload0: 200L, sequence: 2L)
            };
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(3),
                externalSourceId,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                batchId: 5L,
                signals,
                exogenous,
                out _,
                out _));
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                new RuntimeFrame(3),
                derivedSourceId,
                RuntimeCommandProvenanceKind.Derived,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                batchId: 6L,
                new[] { new GameplayTriggerSignal(12) },
                derived,
                out _,
                out _));
            var callerCommands = new List<RuntimeCommand>
            {
                derived[0],
                exogenous[1],
                exogenous[0]
            };
            RuntimeReplayRecorder recorder = RuntimeReplayRecorder.CreateExogenousStream(
                new RuntimeReplayHeader(1, "0.1", "config", "resources", new RuntimeFrame(3)),
                registry);

            RuntimeReplayFrameRecord record = recorder.RecordFrame(
                new RuntimeFrame(3),
                callerCommands,
                resultHash: 9L);
            callerCommands.Clear();
            RuntimeReplaySnapshot snapshot = recorder.CreateSnapshot();

            Assert.AreEqual(2, record.Commands.Count);
            Assert.AreEqual(1, record.Commands[0].Payload1);
            Assert.AreEqual(0, record.Commands[1].Payload1);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous, record.Commands[0].Envelope.Provenance);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous, record.Commands[1].Envelope.Provenance);
            Assert.AreEqual(record.Commands[0], snapshot.Records[0].Commands[0]);
            Assert.AreEqual(record.Commands[1], snapshot.Records[0].Commands[1]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GameplayRuntimeModule_DrainsOnceAndOrdersCommandBeforeSubmitAndEvaluate(
            bool reverseRegistration)
        {
            var sink = new CountingSink();
            GameplayTriggerRuntime runtime = CreateRuntime(sink, initiallyEnabled: false);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var submitSystem = new SignalSubmitSystem(runtime);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);
            var unsupportedSystem = new GameplayUnsupportedCommandSystem();
            var pipeline = new GameplaySystemPipeline();
            if (reverseRegistration)
            {
                pipeline.Add(evaluationSystem);
                pipeline.Add(submitSystem);
                pipeline.Add(unsupportedSystem);
                pipeline.Add(commandSystem);
            }
            else
            {
                pipeline.Add(commandSystem);
                pipeline.Add(unsupportedSystem);
                pipeline.Add(submitSystem);
                pipeline.Add(evaluationSystem);
            }

            var commandBuffer = new RuntimeCommandBuffer(initialCapacity: 2);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            Assert.IsTrue(commandBuffer.Enqueue(
                GameplayTriggerRuntimeCommandFactory.SetEnabled(
                    RuntimeFrame.Zero,
                    instanceId,
                    true)).Success);
            var module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);
            var host = new RuntimeHost();
            host.RegisterModule(module);
            host.Initialize();
            host.Start();

            host.Tick(frameIndex: 0, deltaTime: 0d);

            Assert.AreEqual(new RuntimeFrame(1), commandBuffer.CurrentFrame);
            Assert.AreEqual(0, commandBuffer.PendingCount);
            Assert.AreEqual(1, commandSystem.HandledCommandCount);
            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.IsTrue(submitSystem.LastResult.Success);
            Assert.AreEqual(RuntimeFrame.Zero, evaluationSystem.LastEvaluatedFrame);
            Assert.IsTrue(evaluationSystem.LastResult.Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(1, runtime.ActivationCount);
            Assert.AreEqual(1, sink.CommitCount);
            var events = new List<GameplayRuntimeEvent>();
            Assert.AreEqual(0, module.DrainEvents(RuntimeFrame.Zero, events));

            host.Stop();
            host.Dispose();
        }

        [Test]
        public void DisableCommand_RemovesAlreadyPendingInputBeforeEvaluation()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signals = new[] { new GameplayTriggerSignal(1) };
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            var command = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                RuntimeFrame.Zero,
                instanceId,
                false);
            var commands = new[] { command };
            var commandState = new GameplayCommandExecutionState();
            GameplaySystemContext context = CreateContext(
                RuntimeFrame.Zero,
                commands,
                commandState);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);

            commandSystem.Tick(context);
            evaluationSystem.Tick(context);

            Assert.IsTrue(commandState.IsHandled(command));
            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.IsTrue(evaluationSystem.LastResult.Success);
            Assert.AreEqual(0, runtime.OccurrenceCount);
            Assert.AreEqual(0, runtime.ActivationCount);
        }

        [Test]
        public void ResetCommand_ClearsRouteStateBeforeSameFrameEvaluation()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signals = new[] { new GameplayTriggerSignal(1) };
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.RouteStateCount);
            Assert.AreEqual(2L, runtime.NextActivationId);

            var frame = new RuntimeFrame(1);
            RuntimeCommand reset = GameplayTriggerRuntimeCommandFactory.Reset(
                frame,
                instanceId);
            var commandState = new GameplayCommandExecutionState();
            GameplaySystemContext context = CreateContext(
                frame,
                new[] { reset },
                commandState);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);

            commandSystem.Tick(context);
            evaluationSystem.Tick(context);

            Assert.IsTrue(commandState.IsHandled(reset));
            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.IsTrue(evaluationSystem.LastResult.Success);
            Assert.AreEqual(0, runtime.RouteStateCount);
            Assert.AreEqual(0, runtime.RelationCount);
            Assert.AreEqual(2L, runtime.NextActivationId);
        }

        [Test]
        public void LifecycleCommand_InvalidAndStaleHandlesAreStructuredFailures()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId current = runtime.GetInstanceId(0);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var commandState = new GameplayCommandExecutionState();

            RuntimeCommand stale = GameplayTriggerRuntimeCommandFactory.Reset(
                RuntimeFrame.Zero,
                new GameplayTriggerInstanceId(current.Index, current.Generation + 1));
            commandSystem.Tick(CreateContext(RuntimeFrame.Zero, new[] { stale }, commandState));
            Assert.IsTrue(commandState.IsHandled(stale));
            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(
                GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastResult.Result.ErrorCode);

            commandState.Clear();
            var malformed = new RuntimeCommand(
                RuntimeFrame.Zero,
                sourceId: 0,
                commandId: GameplayTriggerRuntimeCommandIds.SetEnabled,
                targetId: 0,
                payload0: 0,
                payload1: 2,
                payload2: 1);
            commandSystem.Tick(CreateContext(RuntimeFrame.Zero, new[] { malformed }, commandState));
            Assert.IsTrue(commandState.IsHandled(malformed));
            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(
                GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastResult.Result.ErrorCode);
            Assert.AreEqual(2, commandSystem.HandledCommandCount);
            Assert.AreEqual(2, commandSystem.RejectedCommandCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleCommands_DifferentSourcesUseRuntimeTotalOrder(
            bool reverseEnqueue)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                initiallyEnabled: false);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            RuntimeCommand lowerSourceEnables =
                GameplayTriggerRuntimeCommandFactory.SetEnabled(
                    RuntimeFrame.Zero,
                    instanceId,
                    true,
                    sourceId: 10);
            RuntimeCommand higherSourceDisables =
                GameplayTriggerRuntimeCommandFactory.SetEnabled(
                    RuntimeFrame.Zero,
                    instanceId,
                    false,
                    sourceId: 20);

            GameplayTriggerCommandSystem commandSystem = RunLifecycleFrame(
                runtime,
                lowerSourceEnables,
                higherSourceDisables,
                reverseEnqueue);

            Assert.IsFalse(runtime.GetInstanceAuthorityState(0).Enabled);
            Assert.AreEqual(2, commandSystem.HandledCommandCount);
            Assert.AreEqual(
                GameplayTriggerLifecycleCommandKind.SetEnabled,
                commandSystem.LastResult.CommandKind);
            Assert.IsTrue(commandSystem.LastResult.Result.Success);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleCommands_SameKeyUseAcceptanceSequence(
            bool enableLast)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                initiallyEnabled: false);
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            RuntimeCommand enable = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                RuntimeFrame.Zero,
                instanceId,
                true,
                sourceId: 10);
            RuntimeCommand disable = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                RuntimeFrame.Zero,
                instanceId,
                false,
                sourceId: 10);

            GameplayTriggerCommandSystem commandSystem = enableLast
                ? RunLifecycleFrame(runtime, disable, enable, reverseEnqueue: false)
                : RunLifecycleFrame(runtime, enable, disable, reverseEnqueue: false);

            Assert.AreEqual(
                enableLast,
                runtime.GetInstanceAuthorityState(0).Enabled);
            Assert.AreEqual(1L, commandSystem.LastResult.CommandSequence);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LifecycleCommands_SetEnabledAndResetUseCommandIdOrder(
            bool reverseEnqueue)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            var signals = new[] { new GameplayTriggerSignal(1) };
            Assert.IsTrue(runtime.TrySubmitSignals(
                RuntimeFrame.Zero,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                signals,
                out _));
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
            Assert.AreEqual(1, runtime.RouteStateCount);
            RuntimeCommand disable = GameplayTriggerRuntimeCommandFactory.SetEnabled(
                new RuntimeFrame(1),
                instanceId,
                false,
                sourceId: 10);
            RuntimeCommand reset = GameplayTriggerRuntimeCommandFactory.Reset(
                new RuntimeFrame(1),
                instanceId,
                sourceId: 10);

            GameplayTriggerCommandSystem commandSystem = RunLifecycleFrame(
                runtime,
                disable,
                reset,
                reverseEnqueue,
                frameIndex: 1);

            Assert.IsFalse(runtime.GetInstanceAuthorityState(0).Enabled);
            Assert.AreEqual(0, runtime.RouteStateCount);
            Assert.AreEqual(
                GameplayTriggerLifecycleCommandKind.Reset,
                commandSystem.LastResult.CommandKind);
        }

        [Test]
        public void LifecycleCommands_LastRejectedResultSurvivesLaterSuccess()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId current = runtime.GetInstanceId(0);
            GameplayTriggerInstanceId staleId = new GameplayTriggerInstanceId(
                current.Index,
                current.Generation + 1);
            RuntimeCommand stale = GameplayTriggerRuntimeCommandFactory.Reset(
                RuntimeFrame.Zero,
                staleId).WithSequence(4);
            RuntimeCommand valid = GameplayTriggerRuntimeCommandFactory.Reset(
                RuntimeFrame.Zero,
                current).WithSequence(5);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var commandState = new GameplayCommandExecutionState();

            commandSystem.Tick(CreateContext(
                RuntimeFrame.Zero,
                new[] { stale, valid },
                commandState));

            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(5L, commandSystem.LastResult.CommandSequence);
            Assert.IsFalse(commandSystem.LastRejectedResult.Result.Success);
            Assert.AreEqual(4L, commandSystem.LastRejectedResult.CommandSequence);
            Assert.AreEqual(staleId, commandSystem.LastRejectedResult.InstanceId);
            Assert.AreEqual(
                GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastRejectedResult.Result.ErrorCode);
            Assert.AreEqual(1, commandSystem.RejectedCommandCount);
        }

        [Test]
        public void SignalBatch_IncompleteRecordsRejectWithoutSubmittingPartialInput()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var records = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                10L,
                new[]
                {
                    new GameplayTriggerSignal(1, sequence: 1),
                    new GameplayTriggerSignal(2, sequence: 2)
                },
                records,
                out _,
                out _));
            var commandSystem = new GameplayTriggerCommandSystem(runtime, maxSignalCommandItems: 2);
            var commandState = new GameplayCommandExecutionState();

            commandSystem.Tick(CreateContext(
                RuntimeFrame.Zero,
                new[] { records[0] },
                commandState));

            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastResult.Result.ErrorCode);
            Assert.AreEqual(0, runtime.OccurrenceCount);
            Assert.AreEqual(1, commandSystem.HandledCommandCount);
            Assert.AreEqual(1, commandSystem.RejectedCommandCount);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void SignalBatch_MalformedRecordAtAnyPositionRejectsWholeRawBatch(
            int malformedIndex)
        {
            var sink = new CountingSink();
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var validRecords = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                10L,
                new[]
                {
                    new GameplayTriggerSignal(1, sequence: 1),
                    new GameplayTriggerSignal(2, sequence: 2)
                },
                validRecords,
                out _,
                out _));
            RuntimeCommandEnvelope validEnvelope = validRecords[0].Envelope;
            RuntimeCommand malformed = WithEnvelope(
                validRecords[0],
                new RuntimeCommandEnvelope(
                    validEnvelope.TypeId,
                    validEnvelope.SchemaVersion + 1,
                    validEnvelope.Provenance,
                    validEnvelope.Data0,
                    validEnvelope.Data1,
                    validEnvelope.Data2,
                    validEnvelope.Data3,
                    validEnvelope.Data4,
                    validEnvelope.Data5,
                    validEnvelope.Data6,
                    validEnvelope.Data7));
            var commands = new RuntimeCommand[3];
            int validIndex = 0;
            for (int i = 0; i < commands.Length; i++)
            {
                commands[i] = i == malformedIndex
                    ? malformed
                    : validRecords[validIndex++];
            }
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 2);
            var commandState = new GameplayCommandExecutionState();

            commandSystem.Tick(CreateContext(
                RuntimeFrame.Zero,
                commands,
                commandState));
            GameplayTriggerResult evaluation = runtime.Evaluate(RuntimeFrame.Zero);

            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(
                GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastResult.Result.ErrorCode);
            Assert.AreEqual(
                (int)GameplayTriggerSignalCommandCodecStatus.UnsupportedSchema,
                commandSystem.LastResult.Result.Detail0);
            Assert.IsTrue(evaluation.Success);
            Assert.AreEqual(0, runtime.OccurrenceCount);
            Assert.AreEqual(0, sink.CommitCount);
            Assert.AreEqual(3, commandSystem.HandledCommandCount);
            Assert.AreEqual(3, commandSystem.RejectedCommandCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SignalBatch_HostReconstructsItemOrderAcrossEnqueuePermutation(
            bool reverseEnqueue)
        {
            var sink = new CountingSink();
            GameplayTriggerRuntime runtime = CreateRuntime(
                sink,
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var records = new RuntimeCommand[2];
            var expected = new[]
            {
                new GameplayTriggerSignal(
                    11,
                    new GameplayEntityId(3, 1),
                    payload0: -4L,
                    sequence: 10L),
                new GameplayTriggerSignal(
                    12,
                    new GameplayEntityId(4, 1),
                    new GameplayEntityId(5, 1),
                    payload1: 6L,
                    sequence: 20L)
            };
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                7,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                88L,
                expected,
                records,
                out _,
                out _));
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 2);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(evaluationSystem);
            pipeline.Add(commandSystem);
            var commandBuffer = new RuntimeCommandBuffer(initialCapacity: 2);
            Assert.IsTrue(commandBuffer.Enqueue(records[reverseEnqueue ? 1 : 0]).Success);
            Assert.IsTrue(commandBuffer.Enqueue(records[reverseEnqueue ? 0 : 1]).Success);
            var module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);
            var host = new RuntimeHost();
            host.RegisterModule(module);
            host.Initialize();
            host.Start();

            host.Tick(0, 0d);

            Assert.AreEqual(2, commandSystem.HandledCommandCount);
            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(88L, commandSystem.LastResult.BatchId);
            Assert.AreEqual(SourceBindingId, commandSystem.LastResult.SourceBindingId);
            Assert.AreEqual(
                GameplayTriggerRuntimeCommandKind.SubmitSignalBatch,
                commandSystem.LastResult.RuntimeCommandKind);
            Assert.AreEqual(
                GameplayTriggerLifecycleCommandKind.None,
                commandSystem.LastResult.CommandKind);
            Assert.AreEqual(RuntimeCommandProvenanceKind.Exogenous,
                commandSystem.LastResult.Provenance);
            Assert.AreEqual(2, runtime.OccurrenceCount);
            Assert.AreEqual(11, runtime.GetOccurrence(0).SignalId);
            Assert.AreEqual(12, runtime.GetOccurrence(1).SignalId);
            Assert.AreEqual(2, runtime.ActivationCount);
            Assert.AreEqual(2, sink.CommitCount);

            host.Stop();
            host.Dispose();
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        public void SignalCommandSystem_EnforcesNMinusOneNAndNPlusOneCapacity(
            int signalCount,
            bool expectedSuccess)
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var signals = new GameplayTriggerSignal[signalCount];
            for (int i = 0; i < signals.Length; i++)
                signals[i] = new GameplayTriggerSignal(i + 1, sequence: i + 1L);
            var commands = new RuntimeCommand[signalCount];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                1L,
                signals,
                commands,
                out _,
                out _));
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 2);
            var commandState = new GameplayCommandExecutionState();

            commandSystem.Tick(CreateContext(
                RuntimeFrame.Zero,
                commands,
                commandState));

            Assert.AreEqual(expectedSuccess, commandSystem.LastResult.Result.Success);
            if (expectedSuccess)
            {
                Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);
                Assert.AreEqual(signalCount, runtime.OccurrenceCount);
            }
            else
            {
                Assert.AreEqual(
                    GameplayTriggerErrorCode.InputCapacityExceeded,
                    commandSystem.LastResult.Result.ErrorCode);
                Assert.AreEqual(0, runtime.OccurrenceCount);
            }
        }

        [Test]
        public void SignalCommandSystem_OverflowRejectionDoesNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var commands = new RuntimeCommand[3];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                1L,
                new[]
                {
                    new GameplayTriggerSignal(1, sequence: 1),
                    new GameplayTriggerSignal(2, sequence: 2),
                    new GameplayTriggerSignal(3, sequence: 3)
                },
                commands,
                out _,
                out _));
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 2);
            var commandState = new GameplayCommandExecutionState();
            GameplaySystemContext context = CreateContext(
                RuntimeFrame.Zero,
                commands,
                commandState);
            commandSystem.Tick(context);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                commandState.Clear();
                commandSystem.Tick(context);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(
                GameplayTriggerErrorCode.InputCapacityExceeded,
                commandSystem.LastResult.Result.ErrorCode);
        }

        [Test]
        public void SignalCommandSystem_DoesNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new CountingSink(),
                signalOrdering: GameplayTriggerSignalOrdering.Ordered);
            var commands = new RuntimeCommand[2];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                RuntimeFrame.Zero,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                SourceBindingId,
                GameplayTriggerSignalOrdering.Ordered,
                1L,
                new[]
                {
                    new GameplayTriggerSignal(1, sequence: 1),
                    new GameplayTriggerSignal(2, sequence: 2)
                },
                commands,
                out _,
                out _));
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 2);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);
            var commandState = new GameplayCommandExecutionState();
            var world = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                world,
                commands,
                events,
                commandState);
            commandSystem.Tick(context);
            evaluationSystem.Tick(context);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long commandAllocated = 0L;
            long evaluationAllocated = 0L;
            for (int i = 1; i <= 64; i++)
            {
                commandState.Clear();
                context = new GameplaySystemContext(
                    new RuntimeFrame(i),
                    0d,
                    0d,
                    world,
                    commands,
                    events,
                    commandState);
                long commandBefore = GC.GetAllocatedBytesForCurrentThread();
                commandSystem.Tick(context);
                commandAllocated += GC.GetAllocatedBytesForCurrentThread() - commandBefore;
                long evaluationBefore = GC.GetAllocatedBytesForCurrentThread();
                evaluationSystem.Tick(context);
                evaluationAllocated += GC.GetAllocatedBytesForCurrentThread() - evaluationBefore;
            }

            Assert.AreEqual(0L, commandAllocated, "Signal command reconstruction allocated.");
            Assert.AreEqual(0L, evaluationAllocated, "Signal evaluation allocated.");
            Assert.AreEqual(130, commandSystem.HandledCommandCount);
            Assert.AreEqual(65, evaluationSystem.EvaluationCount);
        }

        [Test]
        public void CommandAndEvaluationSystems_DoNotAllocateAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(new CountingSink());
            GameplayTriggerInstanceId instanceId = runtime.GetInstanceId(0);
            RuntimeCommand reset = GameplayTriggerRuntimeCommandFactory.Reset(
                RuntimeFrame.Zero,
                instanceId);
            var commands = new[] { reset };
            var commandState = new GameplayCommandExecutionState();
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var evaluationSystem = new GameplayTriggerEvaluationSystem(runtime);
            var world = new GameplayWorld();
            var events = new RuntimeEventQueue<GameplayRuntimeEvent>();
            var context = new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                world,
                commands,
                events,
                commandState);
            commandSystem.Tick(context);
            evaluationSystem.Tick(context);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 64; i++)
            {
                commandState.Clear();
                context = new GameplaySystemContext(
                    new RuntimeFrame(i),
                    0d,
                    0d,
                    world,
                    commands,
                    events,
                    commandState);
                commandSystem.Tick(context);
                evaluationSystem.Tick(context);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(65, commandSystem.HandledCommandCount);
            Assert.AreEqual(65, evaluationSystem.EvaluationCount);
        }

        private static GameplaySystemContext CreateContext(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands,
            GameplayCommandExecutionState commandState)
        {
            var componentWorld = new GameplayComponentWorld();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                commands,
                componentWorld.Events,
                commandState,
                componentWorld);
        }

        private static GameplayTriggerCommandSystem RunLifecycleFrame(
            GameplayTriggerRuntime runtime,
            RuntimeCommand first,
            RuntimeCommand second,
            bool reverseEnqueue,
            long frameIndex = 0)
        {
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(commandSystem);
            var commandBuffer = new RuntimeCommandBuffer(initialCapacity: 2);
            RuntimeCommand firstEnqueued = reverseEnqueue ? second : first;
            RuntimeCommand secondEnqueued = reverseEnqueue ? first : second;
            Assert.IsTrue(commandBuffer.Enqueue(firstEnqueued).Success);
            Assert.IsTrue(commandBuffer.Enqueue(secondEnqueued).Success);
            var module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                commandBuffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);
            var host = new RuntimeHost();
            host.RegisterModule(module);
            host.Initialize();
            host.Start();

            host.Tick(frameIndex, 0d);

            host.Stop();
            host.Dispose();
            return commandSystem;
        }

        private static GameplayTriggerRuntime CreateRuntime(
            IGameplayTriggerActionSink sink,
            bool initiallyEnabled = true,
            GameplayTriggerSignalOrdering signalOrdering =
                GameplayTriggerSignalOrdering.UnorderedMultiset)
        {
            var source = new GameplayTriggerSourceDefinition(
                SourceDefinitionId,
                GameplayTriggerSourceKind.Signal,
                signalOrdering);
            var action = new GameplayTriggerActionInstruction(
                ActionKindId,
                GameplayTriggerTargetSelector.Owner);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[] { action });
            var definition = new GameplayTriggerDefinition(
                DefinitionId,
                1,
                SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                DefinitionId,
                SourceBindingId,
                new GameplayEntityId(10, 1),
                initiallyEnabled);
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    4,
                    8,
                    8,
                    8,
                    8,
                    8,
                    8,
                    8,
                    4,
                    8));
        }

        private static RuntimeCommand WithEnvelope(
            RuntimeCommand command,
            RuntimeCommandEnvelope envelope)
        {
            return new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                envelope);
        }

        private static void AssertSignal(
            GameplayTriggerSignal expected,
            GameplayTriggerSignal actual)
        {
            Assert.AreEqual(expected.SignalId, actual.SignalId);
            Assert.AreEqual(expected.Subject, actual.Subject);
            Assert.AreEqual(expected.Instigator, actual.Instigator);
            Assert.AreEqual(expected.Payload0, actual.Payload0);
            Assert.AreEqual(expected.Payload1, actual.Payload1);
            Assert.AreEqual(expected.Payload2, actual.Payload2);
            Assert.AreEqual(expected.Sequence, actual.Sequence);
        }

        private sealed class SignalSubmitSystem : IGameplaySystem
        {
            private readonly GameplayTriggerRuntime _runtime;
            private readonly GameplayTriggerSignal[] _signals =
                { new GameplayTriggerSignal(1) };

            public SignalSubmitSystem(GameplayTriggerRuntime runtime)
            {
                _runtime = runtime;
            }

            public string SystemId => "test.gameplay.triggers.signal_submit";
            public GameplaySystemPhase Phase => GameplaySystemPhase.Simulation;
            public int Priority => 100;
            public bool IsEnabled => true;
            public GameplayTriggerResult LastResult { get; private set; }

            public void Tick(GameplaySystemContext context)
            {
                _runtime.TrySubmitSignals(
                    context.Frame,
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    _signals,
                    out GameplayTriggerResult result);
                LastResult = result;
            }
        }

        private sealed class CountingSink : IGameplayTriggerActionSink
        {
            private int _nextToken;

            public GameplayTriggerActionKindId ActionKindId =>
                GameplayTriggerSystemIntegrationTests.ActionKindId;
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = ++_nextToken;
                return true;
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
            }

            public void Abort(int token)
            {
            }
        }
    }
}
