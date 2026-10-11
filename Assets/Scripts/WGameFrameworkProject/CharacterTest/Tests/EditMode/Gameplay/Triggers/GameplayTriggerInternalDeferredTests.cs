using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Gameplay.Triggers
{
    public sealed class GameplayTriggerInternalDeferredTests
    {
        private static readonly GameplayTriggerSourceBindingId OriginSource =
            new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayTriggerSourceBindingId TargetSource =
            new GameplayTriggerSourceBindingId(202);
        private static readonly GameplayEntityId Subject = new GameplayEntityId(11, 1);
        private static readonly GameplayEntityId Instigator = new GameplayEntityId(12, 1);

        [Test]
        public void Runtime_QueuesFrozenTargetAndFullOriginPayloadForNextFrame()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 7, 8, 9));

            QueueOrigin(runtime, RuntimeFrame.Zero);

            Assert.AreEqual(1, runtime.DeferredRecordCount);
            Assert.AreEqual(2L, runtime.NextDeferredSequence);
            GameplayTriggerDeferredRecord record = runtime.GetDeferredRecord(0);
            Assert.AreEqual(new RuntimeFrame(1), record.OriginalDueFrame);
            Assert.AreEqual(1L, record.Sequence);
            Assert.AreEqual(runtime.GetInstanceId(1), record.TargetInstanceId);
            Assert.AreEqual(TargetSource, record.TargetSourceBindingId);
            Assert.AreEqual(Subject, record.Subject);
            Assert.AreEqual(Instigator, record.Instigator);
            Assert.AreEqual(7L, record.Payload0);
            Assert.AreEqual(8L, record.Payload1);
            Assert.AreEqual(9L, record.Payload2);
        }

        [TestCase(GameplayTriggerInternalDeferredOperationKind.SetEnabled)]
        [TestCase(GameplayTriggerInternalDeferredOperationKind.Reset)]
        public void DeferredSignalBeforeLifecycle_RemovesEarlierStagedSignalWithoutIndexDrift(
            GameplayTriggerInternalDeferredOperationKind lifecycle)
        {
            ActionSpec lifecycleAction = lifecycle == GameplayTriggerInternalDeferredOperationKind.SetEnabled
                ? new ActionSpec(lifecycle, signalId: 0, payload0: 0)
                : new ActionSpec(lifecycle);
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                lifecycleAction);
            QueueOrigin(runtime, RuntimeFrame.Zero);

            RunDeferredAndCommand(runtime, new RuntimeFrame(1), Array.Empty<RuntimeCommand>());

            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.AreEqual(0, runtime.PendingInputBatchCount);
            if (lifecycle == GameplayTriggerInternalDeferredOperationKind.SetEnabled)
                Assert.IsFalse(runtime.GetInstanceAuthorityState(1).Enabled);
        }

        [TestCase(GameplayTriggerInternalDeferredOperationKind.SetEnabled, 1)]
        [TestCase(GameplayTriggerInternalDeferredOperationKind.Reset, 0)]
        public void DeferredLifecycleBeforeSignal_PreservesLaterSignalAccordingToTargetState(
            GameplayTriggerInternalDeferredOperationKind lifecycle,
            int expectedRemaining)
        {
            ActionSpec lifecycleAction = lifecycle == GameplayTriggerInternalDeferredOperationKind.SetEnabled
                ? new ActionSpec(lifecycle, signalId: 0, payload0: 0)
                : new ActionSpec(lifecycle);
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                lifecycleAction,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(runtime, RuntimeFrame.Zero);

            RunDeferredAndCommand(runtime, new RuntimeFrame(1), Array.Empty<RuntimeCommand>());

            Assert.AreEqual(expectedRemaining, runtime.DeferredRecordCount);
            if (lifecycle == GameplayTriggerInternalDeferredOperationKind.Reset)
            {
                Assert.AreEqual(1, runtime.PendingInputBatchCount);
                Assert.IsTrue(runtime.Evaluate(new RuntimeFrame(1)).Success);
                Assert.AreEqual(1, runtime.OccurrenceCount);
            }
            else
            {
                Assert.AreEqual(0, runtime.PendingInputBatchCount);
                Assert.AreEqual(GameplayTriggerErrorCode.DeferredSignalCollision,
                    runtime.LastDeferredExecutionResult.Result.ErrorCode);
            }
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void ExternalSignalAndLifecycle_RespectCommandTotalOrder(
            bool signalFirst,
            bool disable)
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand signal = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(44, payload0: 88) })[0];
            RuntimeCommand lifecycle = disable
                ? GameplayTriggerRuntimeCommandFactory.SetEnabled(
                    frame,
                    runtime.GetInstanceId(1),
                    false)
                : GameplayTriggerRuntimeCommandFactory.Reset(
                    frame,
                    runtime.GetInstanceId(1));
            RuntimeCommand[] commands = signalFirst
                ? new[] { signal, lifecycle }
                : new[] { lifecycle, signal };

            GameplayTriggerCommandSystem commandSystem = RunDeferredAndCommand(
                runtime,
                frame,
                commands);

            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            int expectedOccurrences = signalFirst || disable ? 0 : 1;
            Assert.AreEqual(expectedOccurrences, runtime.OccurrenceCount);
            if (!signalFirst && disable)
            {
                Assert.IsFalse(commandSystem.LastResult.Result.Success);
                Assert.AreEqual(GameplayTriggerErrorCode.Disabled,
                    commandSystem.LastResult.Result.ErrorCode);
            }
            if (!signalFirst && !disable)
                Assert.AreEqual(88L, runtime.GetOccurrence(0).Payload0);
        }

        [Test]
        public void MissingExternalSignalItem_IsRejectedAtomicallyAndDeferredProgresses()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand[] encoded = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(44, payload0: 88),
                    new GameplayTriggerSignal(45, payload0: 99)
                });

            GameplayTriggerCommandSystem commandSystem = RunDeferredAndCommand(
                runtime,
                frame,
                new[] { encoded[0] });

            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InvalidArgument,
                commandSystem.LastResult.Result.ErrorCode);
            Assert.AreEqual(0, runtime.DeferredRecordCount);
            GameplayTriggerDeferredExecutionResult execution =
                runtime.LastDeferredExecutionResult;
            Assert.AreEqual(runtime.GetInstanceId(1), execution.TargetInstanceId);
            Assert.AreEqual(TargetSource, execution.TargetSourceBindingId);
            Assert.AreEqual(1L, execution.ActivationId);
            Assert.AreEqual(0, execution.ActionIndex);
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(77L, runtime.GetOccurrence(0).Payload0);
        }

        [Test]
        public void RejectedWrongOrderingAndMissingItem_UsesCatalogOrderingForDeferredProgress()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.Ordered,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand[] encoded = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(44),
                    new GameplayTriggerSignal(45)
                });

            GameplayTriggerCommandSystem commandSystem = RunDeferredAndCommand(
                runtime,
                frame,
                new[] { encoded[0] });

            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(77L, runtime.GetOccurrence(0).Payload0);
        }

        [Test]
        public void ExternalReconstructionOverflow_IsRejectedAndDeferredStillProgresses()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.Ordered,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand[] external = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.Ordered,
                new[]
                {
                    new GameplayTriggerSignal(44, sequence: 100),
                    new GameplayTriggerSignal(45, sequence: 101)
                });
            GameplaySystemContext context = Context(frame, external);
            new GameplayTriggerDeferredSystem(runtime).Tick(context);
            var commandSystem = new GameplayTriggerCommandSystem(
                runtime,
                maxSignalCommandItems: 1);

            commandSystem.Tick(context);

            Assert.IsFalse(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.InputCapacityExceeded,
                commandSystem.LastResult.Result.ErrorCode);
            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(77L, runtime.GetOccurrence(0).Payload0);
        }

        [Test]
        public void EmptyOrderedExternalBatch_WinsFrameAndDeferredRetriesNextFrame()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.Ordered,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame firstDue = new RuntimeFrame(1);
            RuntimeCommand emptyExternal = EncodeSignals(
                firstDue,
                GameplayTriggerSignalOrdering.Ordered,
                Array.Empty<GameplayTriggerSignal>())[0];

            GameplayTriggerCommandSystem firstCommand = RunDeferredAndCommand(
                runtime,
                firstDue,
                new[] { emptyExternal });

            Assert.IsTrue(firstCommand.LastResult.Result.Success);
            Assert.AreEqual(1, runtime.DeferredRecordCount);
            Assert.IsTrue(runtime.Evaluate(firstDue).Success);
            Assert.AreEqual(0, runtime.OccurrenceCount);

            RuntimeFrame retry = new RuntimeFrame(2);
            RunDeferredAndCommand(runtime, retry, Array.Empty<RuntimeCommand>());
            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.IsTrue(runtime.Evaluate(retry).Success);
            Assert.AreEqual(1, runtime.OccurrenceCount);
            Assert.AreEqual(77L, runtime.GetOccurrence(0).Payload0);
        }

        [Test]
        public void DeferredQueueLargerThanInputCapacity_DrainsStablePrefixesAcrossFrames()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32, 2),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 33, 3),
                maxInputItems: 1);
            QueueOrigin(runtime, RuntimeFrame.Zero);

            for (int frameValue = 1; frameValue <= 3; frameValue++)
            {
                RuntimeFrame frame = new RuntimeFrame(frameValue);
                RunDeferredAndCommand(runtime, frame, Array.Empty<RuntimeCommand>());
                Assert.AreEqual(3 - frameValue, runtime.DeferredRecordCount);
                if (frameValue < 3)
                {
                    Assert.AreEqual(GameplayTriggerErrorCode.DeferredInputCapacityExceeded,
                        runtime.LastDeferredExecutionResult.Result.ErrorCode);
                }
                Assert.IsTrue(runtime.Evaluate(frame).Success);
                Assert.AreEqual(1, runtime.OccurrenceCount);
                Assert.AreEqual(frameValue, runtime.GetOccurrence(0).Payload0);
            }
        }

        [Test]
        public void FullExternalBatchLeavesZeroDeferredSlots_ReportsCapacityAndRetainsRecord()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 77),
                maxInputItems: 1);
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand external = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(44, payload0: 88) })[0];

            GameplayTriggerCommandSystem commandSystem = RunDeferredAndCommand(
                runtime,
                frame,
                new[] { external });

            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(1, runtime.DeferredRecordCount);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredSignalCollision,
                runtime.LastDeferredExecutionResult.Result.ErrorCode);
        }

        [Test]
        public void ExternalBatchConsumesMaximumDeferredPrefixAndReports26ForRetainedTail()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32, 2),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 33, 3),
                maxInputItems: 2);
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame frame = new RuntimeFrame(1);
            RuntimeCommand external = EncodeSignals(
                frame,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(44, payload0: 88) })[0];

            GameplayTriggerCommandSystem commandSystem = RunDeferredAndCommand(
                runtime,
                frame,
                new[] { external });

            Assert.IsTrue(commandSystem.LastResult.Result.Success);
            Assert.AreEqual(2, runtime.DeferredRecordCount);
            Assert.AreEqual(2L, runtime.GetDeferredRecord(0).Sequence);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredInputCapacityExceeded,
                runtime.LastDeferredExecutionResult.Result.ErrorCode);
            Assert.IsTrue(runtime.Evaluate(frame).Success);
            Assert.AreEqual(2, runtime.OccurrenceCount);
        }

        [Test]
        public void SaveRestoreHashRoundtrip_PreservesSetResetSignalRecordsAndOriginEntities()
        {
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.SetEnabled, payload0: 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Reset),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 7, 8, 9));
            QueueOrigin(source, RuntimeFrame.Zero);
            RuntimeSaveState captured = Capture(source);
            long expectedHash = Hash(source);
            GameplayTriggerRuntime target = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.SetEnabled, payload0: 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Reset),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 7, 8, 9));

            RuntimeSaveStateResult<bool> restore = Participant(target).RestoreSaveState(captured);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(3, target.DeferredRecordCount);
            Assert.AreEqual(source.NextDeferredSequence, target.NextDeferredSequence);
            Assert.AreEqual(expectedHash, Hash(target));
            Assert.AreEqual(Payload(captured), Payload(Capture(target)));
            for (int i = 0; i < target.DeferredRecordCount; i++)
            {
                Assert.AreEqual(Subject, target.GetDeferredRecord(i).Subject);
                Assert.AreEqual(Instigator, target.GetDeferredRecord(i).Instigator);
            }
            Assert.AreEqual(1L, target.GetDeferredRecord(0).Payload0);
            Assert.AreEqual(0L, target.GetDeferredRecord(1).Payload0);
            Assert.AreEqual(7L, target.GetDeferredRecord(2).Payload0);
        }

        [Test]
        public void Restore_RejectsDeferredDueAfterNextNaturalFrameAndRemainsAtomic()
        {
            var endFrame = new RuntimeFrame(4);
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(source, endFrame);
            RuntimeSaveState valid = Capture(source);
            JObject payload = JObject.Parse(Payload(valid));
            payload["deferredRecords"][0]["originalDueFrame"] = endFrame.Value + 2L;

            GameplayTriggerRuntime target = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(target, RuntimeFrame.Zero);
            string beforePayload = Payload(Capture(target));
            long beforeHash = Hash(target);
            long beforeNextActivation = target.NextActivationId;
            long beforeNextDeferred = target.NextDeferredSequence;

            RuntimeSaveStateResult<bool> result = Participant(target).RestoreSaveState(
                WithPayload(valid, payload.ToString(Formatting.None)));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.InvalidDocument, result.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid,
                result.Error.DetailCode);
            Assert.AreEqual(beforePayload, Payload(Capture(target)));
            Assert.AreEqual(beforeHash, Hash(target));
            Assert.AreEqual(beforeNextActivation, target.NextActivationId);
            Assert.AreEqual(beforeNextDeferred, target.NextDeferredSequence);
        }

        [Test]
        public void RestoreThenActivate_CaptureAndSecondRestoreRemainCanonical()
        {
            var endFrame = new RuntimeFrame(4);
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(source, endFrame);
            RuntimeSaveState accepted = Capture(source);
            GameplayTriggerRuntime continued = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            Assert.IsTrue(Participant(continued).RestoreSaveState(accepted).Success);

            QueueOrigin(continued, new RuntimeFrame(endFrame.Value + 1L));

            Assert.AreEqual(2, continued.DeferredRecordCount);
            Assert.AreEqual(new RuntimeFrame(5),
                continued.GetDeferredRecord(0).OriginalDueFrame);
            Assert.AreEqual(new RuntimeFrame(6),
                continued.GetDeferredRecord(1).OriginalDueFrame);
            RuntimeSaveState continuedState = Capture(continued);
            GameplayTriggerRuntime roundTripped = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));

            RuntimeSaveStateResult<bool> secondRestore = Participant(roundTripped)
                .RestoreSaveState(continuedState);

            Assert.IsTrue(secondRestore.Success, secondRestore.Error.ToString());
            Assert.AreEqual(Payload(continuedState), Payload(Capture(roundTripped)));
            Assert.AreEqual(Hash(continued), Hash(roundTripped));
        }

        [Test]
        public void SuccessfulRestore_ClearsDeferredResultAndAllRuntimeOutputs()
        {
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(source, new RuntimeFrame(4));
            RuntimeSaveState accepted = Capture(source);
            GameplayTriggerRuntime target = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(target, RuntimeFrame.Zero);
            RunDeferredAndCommand(
                target,
                new RuntimeFrame(1),
                Array.Empty<RuntimeCommand>());
            Assert.AreNotEqual(0L, target.LastDeferredExecutionResult.Sequence);
            Assert.AreNotEqual(0, target.OccurrenceCount);
            Assert.AreNotEqual(0, target.ActivationCount);
            Assert.AreNotEqual(0, target.IntentCount);

            RuntimeSaveStateResult<bool> restore = Participant(target)
                .RestoreSaveState(accepted);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            GameplayTriggerDeferredExecutionResult last =
                target.LastDeferredExecutionResult;
            Assert.AreEqual(RuntimeFrame.Zero, last.Frame);
            Assert.AreEqual(0L, last.Sequence);
            Assert.AreEqual(default(GameplayTriggerInstanceId), last.TargetInstanceId);
            Assert.AreEqual(default(GameplayTriggerSourceBindingId),
                last.TargetSourceBindingId);
            Assert.AreEqual(0L, last.ActivationId);
            Assert.AreEqual(0, last.ActionIndex);
            Assert.AreEqual(GameplayTriggerInternalDeferredOperationKind.None,
                last.OperationKind);
            Assert.IsFalse(last.Result.Success);
            Assert.AreEqual(GameplayTriggerErrorCode.None, last.Result.ErrorCode);
            Assert.AreEqual(0, target.OccurrenceCount);
            Assert.AreEqual(0, target.ActivationCount);
            Assert.AreEqual(0, target.IntentCount);
            Assert.AreEqual(0, target.DiagnosticCount);
            Assert.AreEqual(Payload(accepted), Payload(Capture(target)));
        }

        [Test]
        public void LegacyV1Migration_RequiresExactOldShapeAndRejectsExplicitNullV2Fields()
        {
            GameplayTriggerRuntime source = CreateExternalOnlyRuntime();
            RuntimeSaveState current = Capture(source);
            JObject legacyPayload = JObject.Parse(Payload(current));
            legacyPayload["schemaVersion"] = 1;
            legacyPayload["definitionCatalogHash"] = source.AuthorityCatalog.LegacyV1DefinitionCatalogHash;
            legacyPayload["bindingCatalogHash"] = source.AuthorityCatalog.LegacyV1BindingCatalogHash;
            legacyPayload["gameplayTriggerCatalogHash"] = source.AuthorityCatalog.LegacyV1GameplayTriggerCatalogHash;
            ((JObject)legacyPayload["options"]).Property("maxDeferredRecords").Remove();
            ((JObject)legacyPayload["options"]).Property("maxReliableOutboxRecords").Remove();
            legacyPayload.Property("nextDeferredSequence").Remove();
            legacyPayload.Property("deferredRecords").Remove();
            legacyPayload.Property("nextReliableOutboxSequence").Remove();
            legacyPayload.Property("reliableOutboxRecords").Remove();
            RuntimeSaveState legacy = WithSchemaAndPayload(
                current,
                1,
                legacyPayload.ToString(Formatting.None));
            GameplayTriggerRuntime migrated = CreateExternalOnlyRuntime();

            RuntimeSaveStateResult<bool> accepted = Participant(migrated).RestoreSaveState(legacy);

            Assert.IsTrue(accepted.Success, accepted.Error.ToString());
            Assert.AreEqual(1L, migrated.NextDeferredSequence);
            Assert.AreEqual(0, migrated.DeferredRecordCount);

            ((JObject)legacyPayload["options"])["maxDeferredRecords"] = JValue.CreateNull();
            legacyPayload["nextDeferredSequence"] = JValue.CreateNull();
            legacyPayload["deferredRecords"] = JValue.CreateNull();
            RuntimeSaveState disguised = WithSchemaAndPayload(
                current,
                1,
                legacyPayload.ToString(Formatting.None));
            RuntimeSaveStateResult<bool> rejected = Participant(CreateExternalOnlyRuntime())
                .RestoreSaveState(disguised);
            Assert.IsFalse(rejected.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, rejected.Error.Code);
            Assert.AreEqual((int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch,
                rejected.Error.DetailCode);
        }

        [Test]
        public void CatalogAndRuntime_FailClosedForDescriptorCapacityAndTerminalFrame()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                maxDeferredRecords: 1);
            Assert.Throws<ArgumentException>(() => CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                maxDeferredRecords: 0));

            QueueOrigin(runtime, new RuntimeFrame(long.MaxValue));

            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredFrameOverflow,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void SaveRestore_AllowsTerminalDeferredDueFrame()
        {
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(source, new RuntimeFrame(long.MaxValue - 1L));
            RuntimeSaveState captured = Capture(source);
            GameplayTriggerRuntime target = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));

            RuntimeSaveStateResult<bool> restore = Participant(target).RestoreSaveState(captured);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(new RuntimeFrame(long.MaxValue),
                target.GetDeferredRecord(0).OriginalDueFrame);
            Assert.AreEqual(Hash(source), Hash(target));
            Assert.AreEqual(Payload(captured), Payload(Capture(target)));

            JObject zeroDuePayload = JObject.Parse(Payload(captured));
            zeroDuePayload["deferredRecords"][0]["originalDueFrame"] = 0L;
            GameplayTriggerRuntime zeroDue = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            Assert.IsTrue(Participant(zeroDue).RestoreSaveState(
                WithPayload(
                    captured,
                    zeroDuePayload.ToString(Formatting.None))).Success);
            Assert.AreEqual(RuntimeFrame.Zero,
                zeroDue.GetDeferredRecord(0).OriginalDueFrame);
        }

        [Test]
        public void SaveRestore_AllowsExhaustedDeferredAllocatorAndPrepareReturns28()
        {
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(source, RuntimeFrame.Zero);
            RuntimeSaveState baseState = Capture(source);
            JObject payload = JObject.Parse(Payload(baseState));
            payload["nextDeferredSequence"] = long.MaxValue;
            RuntimeSaveState exhaustedState = WithPayload(
                baseState,
                payload.ToString(Formatting.None));
            GameplayTriggerRuntime restored = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));

            RuntimeSaveStateResult<bool> firstRestore = Participant(restored)
                .RestoreSaveState(exhaustedState);

            Assert.IsTrue(firstRestore.Success, firstRestore.Error.ToString());
            Assert.AreEqual(long.MaxValue, restored.NextDeferredSequence);
            RuntimeSaveState roundTrip = Capture(restored);
            GameplayTriggerRuntime second = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            Assert.IsTrue(Participant(second).RestoreSaveState(roundTrip).Success);
            Assert.AreEqual(long.MaxValue, second.NextDeferredSequence);
            Assert.AreEqual(Hash(restored), Hash(second));

            QueueOrigin(second, new RuntimeFrame(1));

            Assert.AreEqual(1, second.DeferredRecordCount);
            Assert.AreEqual(long.MaxValue, second.NextDeferredSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredSequenceExhausted,
                second.GetDiagnostic(0).Code);
        }

        [TestCase(DeferredHashField.OriginalDueFrame)]
        [TestCase(DeferredHashField.Sequence)]
        [TestCase(DeferredHashField.OperationKind)]
        [TestCase(DeferredHashField.ActivationId)]
        [TestCase(DeferredHashField.ActionIndex)]
        [TestCase(DeferredHashField.ActionKindId)]
        [TestCase(DeferredHashField.OriginBindingId)]
        [TestCase(DeferredHashField.OriginRouteId)]
        [TestCase(DeferredHashField.TargetInstanceIndex)]
        [TestCase(DeferredHashField.TargetInstanceGeneration)]
        [TestCase(DeferredHashField.TargetSourceBindingId)]
        [TestCase(DeferredHashField.Enabled)]
        [TestCase(DeferredHashField.SignalId)]
        [TestCase(DeferredHashField.SubjectIndex)]
        [TestCase(DeferredHashField.SubjectGeneration)]
        [TestCase(DeferredHashField.InstigatorIndex)]
        [TestCase(DeferredHashField.InstigatorGeneration)]
        [TestCase(DeferredHashField.Payload0)]
        [TestCase(DeferredHashField.Payload1)]
        [TestCase(DeferredHashField.Payload2)]
        public void HashV2_DeferredRecordFieldSeamCoversEveryAuthorityValue(
            DeferredHashField field)
        {
            GameplayTriggerDeferredRecord baseline = DeferredRecord();
            GameplayTriggerDeferredRecord changed = DeferredRecord(field);

            Assert.AreNotEqual(HashRecord(baseline), HashRecord(changed), field.ToString());
        }

        [Test]
        public void HashV2_CoversDeferredCapacityAndNextSequence()
        {
            GameplayTriggerRuntime baseline = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            GameplayTriggerRuntime differentCapacity = new GameplayTriggerRuntime(
                baseline.AuthorityCatalog,
                WithDeferredCapacity(baseline.AuthorityOptions, 9));
            QueueOrigin(baseline, RuntimeFrame.Zero);
            RuntimeSaveState normalState = Capture(baseline);
            JObject exhaustedPayload = JObject.Parse(Payload(normalState));
            exhaustedPayload["nextDeferredSequence"] = long.MaxValue;
            GameplayTriggerRuntime exhausted = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            Assert.IsTrue(Participant(exhausted).RestoreSaveState(
                WithPayload(normalState, exhaustedPayload.ToString(Formatting.None))).Success);

            Assert.AreNotEqual(Hash(new GameplayTriggerRuntime(
                baseline.AuthorityCatalog,
                baseline.AuthorityOptions)), Hash(differentCapacity));
            Assert.AreNotEqual(Hash(baseline), Hash(exhausted));
        }

        [Test]
        public void HashV2_ExcludesStagingResultAndDiagnostics()
        {
            GameplayTriggerRuntime staged = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(staged, RuntimeFrame.Zero);
            long beforeStage = Hash(staged);

            new GameplayTriggerDeferredSystem(staged).Tick(
                Context(new RuntimeFrame(1), Array.Empty<RuntimeCommand>()));

            Assert.AreEqual(beforeStage, Hash(staged),
                "Staging tags and LastDeferredExecutionResult are transient.");

            RuntimeSaveState normal = Capture(CreateQueuedRuntimeForPayload());
            JObject payload = JObject.Parse(Payload(normal));
            payload["nextDeferredSequence"] = long.MaxValue;
            RuntimeSaveState exhausted = WithPayload(
                normal,
                payload.ToString(Formatting.None));
            GameplayTriggerRuntime control = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            GameplayTriggerRuntime diagnostic = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            Assert.IsTrue(Participant(control).RestoreSaveState(exhausted).Success);
            Assert.IsTrue(Participant(diagnostic).RestoreSaveState(exhausted).Success);
            RuntimeFrame frame = new RuntimeFrame(1);
            Assert.IsTrue(control.Evaluate(frame).Success);
            Assert.IsTrue(diagnostic.TrySubmitSignals(
                frame,
                OriginSource,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[] { new GameplayTriggerSignal(1, Subject, Instigator) },
                out _));
            Assert.IsTrue(diagnostic.Evaluate(frame).Success);

            Assert.AreEqual(0, control.DiagnosticCount);
            Assert.AreEqual(1, diagnostic.DiagnosticCount);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredSequenceExhausted,
                diagnostic.GetDiagnostic(0).Code);
            Assert.AreEqual(Hash(control), Hash(diagnostic),
                "Diagnostics do not enter authority hash.");
        }

        [Test]
        public void DeferredFullAndRetryTick_DoesNotAllocateAcross64CyclesAfterWarmup()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32),
                maxInputItems: 1);
            var deferredSystem = new GameplayTriggerDeferredSystem(runtime);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            var world = new GameplayWorld();
            var componentWorld = new GameplayComponentWorld();
            var commandState = new GameplayCommandExecutionState();
            RuntimeCommand[] commands = Array.Empty<RuntimeCommand>();
            var originSignals = new[]
            {
                new GameplayTriggerSignal(1, Subject, Instigator)
            };
            long frame = -1L;
            Assert.IsTrue(RunFullRetryCycle(
                runtime,
                deferredSystem,
                commandSystem,
                world,
                componentWorld,
                commandState,
                commands,
                originSignals,
                ref frame));
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int succeeded = 0;
            for (int i = 0; i < 64; i++)
            {
                if (RunFullRetryCycle(
                        runtime,
                        deferredSystem,
                        commandSystem,
                        world,
                        componentWorld,
                        commandState,
                        commands,
                        originSignals,
                        ref frame))
                {
                    succeeded++;
                }
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(64, succeeded);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void HashContributor_NonEmptyDeferredQueueDoesNotAllocateAcross128Calls()
        {
            GameplayTriggerRuntime runtime = CreateQueuedRuntimeForPayload();
            var contributor = new GameplayTriggerRuntimeHashContributor(runtime);
            var accumulator = new RuntimeHashAccumulator(9492, 1);
            var context = new RuntimeHashContext(RuntimeFrame.Zero);
            for (int i = 0; i < 8; i++)
                contributor.Contribute(context, accumulator);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
                contributor.Contribute(context, accumulator);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void Descriptor_RejectsUnknownOperationEvenWhenUnreferenced()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new GameplayTriggerInternalDeferredActionDescriptor(
                    new GameplayTriggerActionKindId(55),
                    (GameplayTriggerInternalDeferredOperationKind)4));
        }

        [Test]
        public void CatalogHashV2_CoversInternalDescriptorCountOperationAndSignalId()
        {
            GameplayTriggerCatalog reset = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Reset))
                .AuthorityCatalog;
            GameplayTriggerCatalog signal31 = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31))
                .AuthorityCatalog;
            GameplayTriggerCatalog signal32 = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32))
                .AuthorityCatalog;
            GameplayTriggerCatalog twoSignals = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32))
                .AuthorityCatalog;

            Assert.AreNotEqual(reset.DefinitionCatalogHash, signal31.DefinitionCatalogHash);
            Assert.AreNotEqual(signal31.DefinitionCatalogHash, signal32.DefinitionCatalogHash);
            Assert.AreNotEqual(signal31.DefinitionCatalogHash, twoSignals.DefinitionCatalogHash);
            Assert.IsFalse(reset.HasLegacyV1CatalogIdentity);
        }

        [Test]
        public void CheckpointClassifier_CoversReservationAndTaggedSignalFinalization()
        {
            Assert.AreEqual(
                GameplayTriggerCheckpointPendingDetailCode.PendingDeferredReservations,
                GameplayTriggerRuntime.ClassifyCheckpointPendingWork(0, 0, 0, 0, 1, false));
            Assert.AreEqual(
                GameplayTriggerCheckpointPendingDetailCode.PendingDeferredSignalFinalization,
                GameplayTriggerRuntime.ClassifyCheckpointPendingWork(0, 0, 0, 0, 0, true));
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            RuntimeFrame due = new RuntimeFrame(1);
            new GameplayTriggerDeferredSystem(runtime).Tick(
                Context(due, Array.Empty<RuntimeCommand>()));

            RuntimeSaveStateResult<RuntimeSaveState> capture = Participant(runtime)
                .CaptureSaveState();

            Assert.IsFalse(capture.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.PendingWork, capture.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingDeferredSignalFinalization,
                capture.Error.DetailCode);

            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            commandSystem.Tick(Context(due, Array.Empty<RuntimeCommand>()));
            Assert.IsTrue(runtime.Evaluate(due).Success);
            Assert.IsTrue(Participant(runtime).CheckQuiescence(
                new RuntimeCheckpointContext(due.Value)).IsReady);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Reservation_IsObservedAsDetail5ThenAbortOrCommitReturnsReady(
            bool acceptExternal)
        {
            var observer = new ObservingSink(acceptExternal);
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                maxDeferredRecords: 1,
                externalSink: observer,
                appendExternalAction: true);
            observer.Attach(runtime);
            long hashBefore = Hash(runtime);

            QueueOrigin(runtime, RuntimeFrame.Zero);

            Assert.IsFalse(observer.QuiescenceDuringPrepare.IsReady);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingDeferredReservations,
                observer.QuiescenceDuringPrepare.DetailCode);
            Assert.IsFalse(observer.CaptureDuringPrepare.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.PendingWork,
                observer.CaptureDuringPrepare.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingDeferredReservations,
                observer.CaptureDuringPrepare.Error.DetailCode);
            Assert.AreEqual(hashBefore, observer.HashDuringPrepare,
                "A live reservation is transient and must not enter the authority hash.");
            Assert.IsTrue(Participant(runtime).CheckQuiescence(
                new RuntimeCheckpointContext(0L)).IsReady,
                "A committed future queue is authority, not pending checkpoint work.");
            Assert.AreEqual(acceptExternal ? 1 : 0, runtime.DeferredRecordCount);
            Assert.AreEqual(acceptExternal ? 2L : 1L, runtime.NextDeferredSequence);
            Assert.AreEqual(acceptExternal ? 2L : 1L, runtime.NextActivationId);
            if (acceptExternal)
            {
                Assert.AreEqual(1, observer.CommitCount);
                Assert.IsTrue(Participant(runtime).CaptureSaveState().Success);
            }
            else
            {
                Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                    runtime.GetDiagnostic(0).Code,
                    "External sink false remains stable error 13.");
            }
        }

        [Test]
        public void ExternalOnlyPrepare_ReentrantCaptureRemainsPendingInputDetail1()
        {
            var observer = new ObservingSink(true);
            GameplayTriggerRuntime runtime = CreateExternalOnlyRuntime(observer);
            observer.Attach(runtime);

            QueueOrigin(runtime, RuntimeFrame.Zero);

            Assert.IsFalse(observer.QuiescenceDuringPrepare.IsReady);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches,
                observer.QuiescenceDuringPrepare.DetailCode);
            Assert.IsFalse(observer.CaptureDuringPrepare.Success);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingInputBatches,
                observer.CaptureDuringPrepare.Error.DetailCode);
            Assert.IsTrue(Participant(runtime).CheckQuiescence(
                new RuntimeCheckpointContext(0L)).IsReady);
        }

        [Test]
        public void SharedFrozenCatalog_RuntimeAuthorityDoesNotLeakAcrossInstances()
        {
            GameplayTriggerRuntime first = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            GameplayTriggerCatalog catalog = first.AuthorityCatalog;
            GameplayTriggerRuntimeOptions options = first.AuthorityOptions;
            var second = new GameplayTriggerRuntime(catalog, options);
            long secondHashBefore = Hash(second);
            string secondSaveBefore = Payload(Capture(second));

            QueueOrigin(first, RuntimeFrame.Zero);

            Assert.AreEqual(1, first.DeferredRecordCount);
            Assert.AreEqual(2L, first.NextDeferredSequence);
            Assert.AreEqual(0, second.DeferredRecordCount);
            Assert.AreEqual(1L, second.NextDeferredSequence);
            Assert.AreEqual(1L, second.NextActivationId);
            Assert.AreEqual(secondHashBefore, Hash(second));
            Assert.AreEqual(secondSaveBefore, Payload(Capture(second)));
            Assert.AreNotEqual(Hash(first), Hash(second));
        }

        [Test]
        public void OccupancyOrigin_QueuesTargetSignalWithFullFrozenEnvelope()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(
                    GameplayTriggerInternalDeferredOperationKind.Signal,
                    47,
                    201,
                    202,
                    203),
                originSourceKind: GameplayTriggerSourceKind.Occupancy);
            var occupancySubject = new GameplayEntityId(21, 3);

            Assert.IsTrue(runtime.TrySubmitOccupancy(
                RuntimeFrame.Zero,
                OriginSource,
                new[] { occupancySubject },
                out GameplayTriggerResult submit), submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(RuntimeFrame.Zero).Success);

            GameplayTriggerDeferredRecord record = runtime.GetDeferredRecord(0);
            Assert.AreEqual(occupancySubject, record.Subject);
            Assert.AreEqual(default(GameplayEntityId), record.Instigator);
            Assert.AreEqual(47, record.SignalId);
            Assert.AreEqual(201L, record.Payload0);
            Assert.AreEqual(202L, record.Payload1);
            Assert.AreEqual(203L, record.Payload2);
        }

        [Test]
        public void LastDeferredSlotFailure_AbortsWholeActivationWithoutPolicyOrAllocatorCommit()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 32),
                maxDeferredRecords: 1);

            QueueOrigin(runtime, RuntimeFrame.Zero);

            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.AreEqual(1L, runtime.NextDeferredSequence);
            Assert.AreEqual(1L, runtime.NextActivationId);
            Assert.AreEqual(0, runtime.ActivationCount);
            Assert.AreEqual(0, runtime.RouteStateCount);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredQueueCapacityExceeded,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void SignalDescriptorTargetingOccupancySource_FailsPrepareWith29()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31),
                targetSourceKind: GameplayTriggerSourceKind.Occupancy);

            QueueOrigin(runtime, RuntimeFrame.Zero);

            Assert.AreEqual(0, runtime.DeferredRecordCount);
            Assert.AreEqual(1L, runtime.NextDeferredSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.DeferredSourceKindMismatch,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void ChainedInternalSignals_AdvanceExactlyOneHopPerFrameAndLockSystemIdentity()
        {
            var sink = new CountingSink(new GameplayTriggerActionKindId(9001));
            GameplayTriggerRuntime runtime = CreateChainRuntime(sink);
            var deferredSystem = new GameplayTriggerDeferredSystem(runtime);
            Assert.AreEqual(GameplaySystemPhase.PreCommand, deferredSystem.Phase);
            Assert.AreEqual(GameplayTriggerDeferredSystem.DefaultSystemId,
                deferredSystem.SystemId);
            Assert.AreEqual(GameplayTriggerDeferredSystem.DefaultPriority,
                deferredSystem.Priority);

            QueueOrigin(runtime, RuntimeFrame.Zero);
            Assert.AreEqual(TargetSource, runtime.GetDeferredRecord(0).TargetSourceBindingId);
            Assert.AreEqual(new RuntimeFrame(1),
                runtime.GetDeferredRecord(0).OriginalDueFrame);

            RuntimeFrame secondFrame = new RuntimeFrame(1);
            RunDeferredAndCommand(runtime, secondFrame, Array.Empty<RuntimeCommand>());
            Assert.AreEqual(0, sink.CommitCount);
            Assert.IsTrue(runtime.Evaluate(secondFrame).Success);
            Assert.AreEqual(new GameplayTriggerSourceBindingId(303),
                runtime.GetDeferredRecord(0).TargetSourceBindingId);
            Assert.AreEqual(new RuntimeFrame(2),
                runtime.GetDeferredRecord(0).OriginalDueFrame);

            RuntimeFrame thirdFrame = new RuntimeFrame(2);
            RunDeferredAndCommand(runtime, thirdFrame, Array.Empty<RuntimeCommand>());
            Assert.AreEqual(0, sink.CommitCount);
            Assert.IsTrue(runtime.Evaluate(thirdFrame).Success);
            Assert.AreEqual(1, sink.CommitCount);
            Assert.AreEqual(0, runtime.DeferredRecordCount);
        }

        [TestCase(DeferredRestoreFailure.Capacity)]
        [TestCase(DeferredRestoreFailure.DuplicateSequence)]
        [TestCase(DeferredRestoreFailure.Order)]
        [TestCase(DeferredRestoreFailure.Record)]
        [TestCase(DeferredRestoreFailure.Target)]
        [TestCase(DeferredRestoreFailure.Origin)]
        [TestCase(DeferredRestoreFailure.NextSequence)]
        public void Restore_DeferredTailFailuresUseStableDetailsAndRemainAtomic(
            DeferredRestoreFailure failure)
        {
            GameplayTriggerRuntime source = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.SetEnabled, payload0: 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Reset),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 7, 8, 9));
            QueueOrigin(source, RuntimeFrame.Zero);
            RuntimeSaveState valid = Capture(source);
            JObject payload = JObject.Parse(Payload(valid));
            JArray records = (JArray)payload["deferredRecords"];
            GameplayTriggerSaveStateErrorDetailCode expected;
            switch (failure)
            {
                case DeferredRestoreFailure.Capacity:
                    while (records.Count <= 8) records.Add(records[0].DeepClone());
                    expected = GameplayTriggerSaveStateErrorDetailCode.DeferredCapacityExceeded;
                    break;
                case DeferredRestoreFailure.DuplicateSequence:
                    records[1]["sequence"] = records[0]["sequence"];
                    expected = GameplayTriggerSaveStateErrorDetailCode.DuplicateDeferredSequence;
                    break;
                case DeferredRestoreFailure.Order:
                    JToken first = records[0].DeepClone();
                    JToken second = records[1].DeepClone();
                    records[0] = second;
                    records[1] = first;
                    expected = GameplayTriggerSaveStateErrorDetailCode.DeferredOrderInvalid;
                    break;
                case DeferredRestoreFailure.Record:
                    records[2]["payload2"] = 999L;
                    expected = GameplayTriggerSaveStateErrorDetailCode.DeferredRecordInvalid;
                    break;
                case DeferredRestoreFailure.Target:
                    records[0]["targetSourceBindingId"] = 999;
                    expected = GameplayTriggerSaveStateErrorDetailCode.DeferredTargetInvalid;
                    break;
                case DeferredRestoreFailure.Origin:
                    records[0]["originRouteId"] = 999;
                    expected = GameplayTriggerSaveStateErrorDetailCode.DeferredOriginInvalid;
                    break;
                default:
                    payload["nextDeferredSequence"] = 0L;
                    expected = GameplayTriggerSaveStateErrorDetailCode.NextDeferredSequenceInvalid;
                    break;
            }

            GameplayTriggerRuntime target = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.SetEnabled, payload0: 1),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Reset),
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31, 7, 8, 9));
            QueueOrigin(target, RuntimeFrame.Zero);
            string beforePayload = Payload(Capture(target));
            long beforeHash = Hash(target);
            long beforeNext = target.NextDeferredSequence;

            RuntimeSaveStateResult<bool> result = Participant(target).RestoreSaveState(
                WithPayload(valid, payload.ToString(Formatting.None)));

            Assert.IsFalse(result.Success);
            Assert.AreEqual((int)expected, result.Error.DetailCode);
            Assert.AreEqual(beforePayload, Payload(Capture(target)));
            Assert.AreEqual(beforeHash, Hash(target));
            Assert.AreEqual(beforeNext, target.NextDeferredSequence);
        }

        [Test]
        public void Restore_RejectsMixedModuleCustomAndPayloadVersions()
        {
            RuntimeSaveState current = Capture(CreateExternalOnlyRuntime());
            RuntimeModuleSaveState module = current.ModuleStates[0];
            RuntimeSaveState mixedCustom = ReplaceModule(
                current,
                new RuntimeModuleSaveState(
                    module.ModuleId,
                    2,
                    new RuntimeCustomState(module.CustomState.NumericTypeId, module.CustomState.TypeId, 1, module.CustomState.PayloadJson)));
            Assert.IsFalse(Participant(CreateExternalOnlyRuntime())
                .RestoreSaveState(mixedCustom).Success);

            RuntimeSaveState mixedPayload = ReplaceModule(
                current,
                new RuntimeModuleSaveState(
                    module.ModuleId,
                    1,
                    new RuntimeCustomState(module.CustomState.NumericTypeId, module.CustomState.TypeId, 1, module.CustomState.PayloadJson)));
            RuntimeSaveStateResult<bool> result = Participant(CreateExternalOnlyRuntime())
                .RestoreSaveState(mixedPayload);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch, result.Error.Code);
            Assert.AreEqual((int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch,
                result.Error.DetailCode);
        }

        private static GameplayTriggerCommandSystem RunDeferredAndCommand(
            GameplayTriggerRuntime runtime,
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands)
        {
            GameplaySystemContext context = Context(frame, commands);
            new GameplayTriggerDeferredSystem(runtime).Tick(context);
            var commandSystem = new GameplayTriggerCommandSystem(runtime);
            commandSystem.Tick(context);
            return commandSystem;
        }

        private static bool RunFullRetryCycle(
            GameplayTriggerRuntime runtime,
            GameplayTriggerDeferredSystem deferredSystem,
            GameplayTriggerCommandSystem commandSystem,
            GameplayWorld world,
            GameplayComponentWorld componentWorld,
            GameplayCommandExecutionState commandState,
            IReadOnlyList<RuntimeCommand> commands,
            GameplayTriggerSignal[] originSignals,
            ref long frameValue)
        {
            RuntimeFrame originFrame = new RuntimeFrame(++frameValue);
            if (!runtime.TrySubmitSignals(
                    originFrame,
                    OriginSource,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    originSignals,
                    out _) ||
                !runtime.Evaluate(originFrame).Success ||
                runtime.DeferredRecordCount != 2)
            {
                return false;
            }

            RuntimeFrame fullFrame = new RuntimeFrame(++frameValue);
            var fullContext = new GameplaySystemContext(
                fullFrame,
                0d,
                0d,
                world,
                commands,
                componentWorld.Events,
                commandState,
                componentWorld);
            deferredSystem.Tick(fullContext);
            commandSystem.Tick(fullContext);
            if (!runtime.Evaluate(fullFrame).Success ||
                runtime.DeferredRecordCount != 1 ||
                runtime.LastDeferredExecutionResult.Result.ErrorCode !=
                    GameplayTriggerErrorCode.DeferredInputCapacityExceeded)
            {
                return false;
            }

            RuntimeFrame retryFrame = new RuntimeFrame(++frameValue);
            var retryContext = new GameplaySystemContext(
                retryFrame,
                0d,
                0d,
                world,
                commands,
                componentWorld.Events,
                commandState,
                componentWorld);
            deferredSystem.Tick(retryContext);
            commandSystem.Tick(retryContext);
            return runtime.Evaluate(retryFrame).Success &&
                runtime.DeferredRecordCount == 0;
        }

        private static GameplaySystemContext Context(
            RuntimeFrame frame,
            IReadOnlyList<RuntimeCommand> commands)
        {
            var componentWorld = new GameplayComponentWorld();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                commands,
                componentWorld.Events,
                new GameplayCommandExecutionState(),
                componentWorld);
        }

        private static RuntimeCommand[] EncodeSignals(
            RuntimeFrame frame,
            GameplayTriggerSignalOrdering ordering,
            GameplayTriggerSignal[] signals)
        {
            var commands = new RuntimeCommand[Math.Max(1, signals.Length)];
            Assert.IsTrue(GameplayTriggerSignalCommandCodec.TryEncodeBatch(
                frame,
                1,
                RuntimeCommandProvenanceKind.Exogenous,
                TargetSource,
                ordering,
                1L,
                signals,
                commands,
                out int written,
                out GameplayTriggerSignalCommandCodecStatus status),
                status.ToString());
            Assert.AreEqual(commands.Length, written);
            return commands;
        }

        private static void QueueOrigin(GameplayTriggerRuntime runtime, RuntimeFrame frame)
        {
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                OriginSource,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(
                        1,
                        Subject,
                        Instigator,
                        payload0: 101,
                        payload1: 102,
                        payload2: 103)
                },
                out GameplayTriggerResult submit),
                submit.ErrorCode.ToString());
            Assert.IsTrue(runtime.Evaluate(frame).Success);
        }

        private static GameplayTriggerRuntime CreateInternalRuntime(
            GameplayTriggerSignalOrdering targetOrdering,
            ActionSpec first,
            ActionSpec second = default,
            ActionSpec third = default,
            int maxDeferredRecords = 8,
            int maxInputItems = 16,
            GameplayTriggerSourceKind originSourceKind = GameplayTriggerSourceKind.Signal,
            GameplayTriggerSourceKind targetSourceKind = GameplayTriggerSourceKind.Signal,
            IGameplayTriggerActionSink externalSink = null,
            bool appendExternalAction = false)
        {
            int count = third.IsValid ? 3 : second.IsValid ? 2 : 1;
            IGameplayTriggerActionSink sink = externalSink ?? new Sink();
            var actions = new GameplayTriggerActionInstruction[
                count + (appendExternalAction ? 1 : 0)];
            var descriptors = new GameplayTriggerInternalDeferredActionDescriptor[count];
            ActionSpec[] specs = { first, second, third };
            for (int i = 0; i < count; i++)
            {
                GameplayTriggerActionKindId kind = new GameplayTriggerActionKindId(1001 + i);
                actions[i] = new GameplayTriggerActionInstruction(
                    kind,
                    GameplayTriggerTargetSelector.ExplicitBindingTarget,
                    1,
                    specs[i].Payload0,
                    specs[i].Payload1,
                    specs[i].Payload2);
                descriptors[i] = new GameplayTriggerInternalDeferredActionDescriptor(
                    kind,
                    specs[i].Operation,
                    specs[i].SignalId);
            }
            if (appendExternalAction)
            {
                actions[count] = new GameplayTriggerActionInstruction(
                    sink.ActionKindId,
                    GameplayTriggerTargetSelector.Owner);
            }

            GameplayTriggerSourceDefinition originSourceDefinition =
                originSourceKind == GameplayTriggerSourceKind.Signal
                    ? new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(1),
                        GameplayTriggerSourceKind.Signal,
                        GameplayTriggerSignalOrdering.UnorderedMultiset)
                    : new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(1),
                        originSourceKind);
            GameplayTriggerSourceDefinition targetSourceDefinition =
                targetSourceKind == GameplayTriggerSourceKind.Signal
                    ? new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(2),
                        GameplayTriggerSourceKind.Signal,
                        targetOrdering)
                    : new GameplayTriggerSourceDefinition(
                        new GameplayTriggerSourceDefinitionId(2),
                        targetSourceKind);
            var originRoute = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                originSourceKind == GameplayTriggerSourceKind.Signal
                    ? GameplayTriggerOccurrenceKind.Pulsed
                    : GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                actions);
            var targetRoute = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(2),
                targetSourceKind == GameplayTriggerSourceKind.Signal
                    ? GameplayTriggerOccurrenceKind.Pulsed
                    : GameplayTriggerOccurrenceKind.Entered,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        sink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                });
            var originDefinition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                originSourceDefinition.SourceDefinitionId,
                new[] { originRoute });
            var targetDefinition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(2),
                1,
                targetSourceDefinition.SourceDefinitionId,
                new[] { targetRoute });
            var originBinding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                originDefinition.DefinitionId,
                OriginSource,
                new GameplayEntityId(1, 1),
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(TargetSource))
                });
            var targetBinding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(2),
                targetDefinition.DefinitionId,
                TargetSource,
                new GameplayEntityId(2, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { originSourceDefinition, targetSourceDefinition },
                new[] { originDefinition, targetDefinition },
                new[] { originBinding, targetBinding },
                new[] { sink },
                descriptors,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, maxInputItems, 16, 16, 16, 16, 16, 16, 8, 8,
                    maxDeferredRecords));
        }

        private static GameplayTriggerRuntime CreateExternalOnlyRuntime(
            IGameplayTriggerActionSink externalSink = null)
        {
            IGameplayTriggerActionSink sink = externalSink ?? new Sink();
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        sink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                });
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                OriginSource,
                new GameplayEntityId(1, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new[] { sink },
                out GameplayTriggerCatalog catalog,
                out _));
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(8, 16, 16, 16, 16, 16, 16, 16, 8, 8));
        }

        private static GameplayTriggerRuntime CreateQueuedRuntimeForPayload()
        {
            GameplayTriggerRuntime runtime = CreateInternalRuntime(
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new ActionSpec(GameplayTriggerInternalDeferredOperationKind.Signal, 31));
            QueueOrigin(runtime, RuntimeFrame.Zero);
            return runtime;
        }

        private static GameplayTriggerRuntime CreateChainRuntime(CountingSink sink)
        {
            var sourceA = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal);
            var sourceB = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(2),
                GameplayTriggerSourceKind.Signal);
            var sourceC = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(3),
                GameplayTriggerSourceKind.Signal);
            var kindAB = new GameplayTriggerActionKindId(1001);
            var kindBC = new GameplayTriggerActionKindId(1002);
            var routeA = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        kindAB,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        1)
                });
            var routeB = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(2),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        kindBC,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        1)
                });
            var routeC = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(3),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        sink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                });
            var definitionA = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                sourceA.SourceDefinitionId,
                new[] { routeA });
            var definitionB = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(2),
                1,
                sourceB.SourceDefinitionId,
                new[] { routeB });
            var definitionC = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(3),
                1,
                sourceC.SourceDefinitionId,
                new[] { routeC });
            var sourceCBinding = new GameplayTriggerSourceBindingId(303);
            var bindingA = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definitionA.DefinitionId,
                OriginSource,
                new GameplayEntityId(1, 1),
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(TargetSource))
                });
            var bindingB = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(2),
                definitionB.DefinitionId,
                TargetSource,
                new GameplayEntityId(2, 1),
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(sourceCBinding))
                });
            var bindingC = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(3),
                definitionC.DefinitionId,
                sourceCBinding,
                new GameplayEntityId(3, 1));
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { sourceA, sourceB, sourceC },
                new[] { definitionA, definitionB, definitionC },
                new[] { bindingA, bindingB, bindingC },
                new IGameplayTriggerActionSink[] { sink },
                new[]
                {
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        kindAB,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        31),
                    new GameplayTriggerInternalDeferredActionDescriptor(
                        kindBC,
                        GameplayTriggerInternalDeferredOperationKind.Signal,
                        32)
                },
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 16, 16, 16, 16, 16, 16, 16, 8, 8, 8));
        }

        private static GameplayTriggerRuntimeOptions WithDeferredCapacity(
            GameplayTriggerRuntimeOptions value,
            int maxDeferredRecords) =>
            new GameplayTriggerRuntimeOptions(
                value.MaxInputBatches,
                value.MaxInputItems,
                value.MaxRelations,
                value.MaxOccurrences,
                value.MaxCandidates,
                value.MaxRouteStates,
                value.MaxActivations,
                value.MaxIntents,
                value.MaxPreparedActions,
                value.MaxConditionStackDepth,
                maxDeferredRecords);

        private static long HashRecord(GameplayTriggerDeferredRecord record)
        {
            var accumulator = new RuntimeHashAccumulator(9492, 1);
            GameplayTriggerRuntimeHashContributor.ContributeDeferredRecord(
                accumulator,
                in record);
            return accumulator.ToHash();
        }

        private static GameplayTriggerDeferredRecord DeferredRecord(
            DeferredHashField changedField = DeferredHashField.None)
        {
            long due = 1L;
            long sequence = 2L;
            GameplayTriggerInternalDeferredOperationKind operation =
                GameplayTriggerInternalDeferredOperationKind.Signal;
            long activation = 3L;
            int actionIndex = 4;
            int actionKind = 5;
            int originBinding = 6;
            int originRoute = 7;
            int targetIndex = 8;
            int targetGeneration = 9;
            int targetSource = 10;
            bool enabled = true;
            int signalId = 11;
            int subjectIndex = 12;
            int subjectGeneration = 13;
            int instigatorIndex = 14;
            int instigatorGeneration = 15;
            long payload0 = 16L;
            long payload1 = 17L;
            long payload2 = 18L;
            switch (changedField)
            {
                case DeferredHashField.OriginalDueFrame: due++; break;
                case DeferredHashField.Sequence: sequence++; break;
                case DeferredHashField.OperationKind:
                    operation = GameplayTriggerInternalDeferredOperationKind.Reset;
                    break;
                case DeferredHashField.ActivationId: activation++; break;
                case DeferredHashField.ActionIndex: actionIndex++; break;
                case DeferredHashField.ActionKindId: actionKind++; break;
                case DeferredHashField.OriginBindingId: originBinding++; break;
                case DeferredHashField.OriginRouteId: originRoute++; break;
                case DeferredHashField.TargetInstanceIndex: targetIndex++; break;
                case DeferredHashField.TargetInstanceGeneration: targetGeneration++; break;
                case DeferredHashField.TargetSourceBindingId: targetSource++; break;
                case DeferredHashField.Enabled: enabled = false; break;
                case DeferredHashField.SignalId: signalId++; break;
                case DeferredHashField.SubjectIndex: subjectIndex++; break;
                case DeferredHashField.SubjectGeneration: subjectGeneration++; break;
                case DeferredHashField.InstigatorIndex: instigatorIndex++; break;
                case DeferredHashField.InstigatorGeneration: instigatorGeneration++; break;
                case DeferredHashField.Payload0: payload0++; break;
                case DeferredHashField.Payload1: payload1++; break;
                case DeferredHashField.Payload2: payload2++; break;
            }
            return new GameplayTriggerDeferredRecord(
                new RuntimeFrame(due),
                sequence,
                operation,
                activation,
                actionIndex,
                new GameplayTriggerActionKindId(actionKind),
                new GameplayTriggerBindingId(originBinding),
                new GameplayTriggerRouteId(originRoute),
                new GameplayTriggerInstanceId(targetIndex, targetGeneration),
                new GameplayTriggerSourceBindingId(targetSource),
                enabled,
                signalId,
                new GameplayEntityId(subjectIndex, subjectGeneration),
                new GameplayEntityId(instigatorIndex, instigatorGeneration),
                payload0,
                payload1,
                payload2);
        }

        private static GameplayTriggerRuntimeSaveStateParticipant Participant(
            GameplayTriggerRuntime runtime) =>
            new GameplayTriggerRuntimeSaveStateParticipant(runtime, new Validator());

        private static RuntimeSaveState Capture(GameplayTriggerRuntime runtime)
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = Participant(runtime).CaptureSaveState();
            Assert.IsTrue(result.Success, result.Error.ToString());
            return result.Value;
        }

        private static string Payload(RuntimeSaveState state) =>
            state.ModuleStates[0].CustomState.PayloadJson;

        private static RuntimeSaveState WithSchemaAndPayload(
            RuntimeSaveState source,
            int schema,
            string payload)
        {
            RuntimeModuleSaveState old = source.ModuleStates[0];
            var module = new RuntimeModuleSaveState(
                old.ModuleId,
                schema,
                new RuntimeCustomState(old.CustomState.NumericTypeId, old.CustomState.TypeId, schema, payload));
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                source.Frame,
                null,
                null,
                new[] { module },
                null);
        }

        private static RuntimeSaveState WithPayload(
            RuntimeSaveState source,
            string payload) =>
            WithSchemaAndPayload(
                source,
                source.ModuleStates[0].SchemaVersion,
                payload);

        private static RuntimeSaveState ReplaceModule(
            RuntimeSaveState source,
            RuntimeModuleSaveState module) =>
            new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                string.Empty,
                string.Empty,
                string.Empty,
                source.Frame,
                null,
                null,
                new[] { module },
                null);

        private static long Hash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(9492, 1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(runtime.AuthorityLastEvaluatedFrame),
                accumulator);
            return accumulator.ToHash();
        }

        private readonly struct ActionSpec
        {
            internal ActionSpec(
                GameplayTriggerInternalDeferredOperationKind operation,
                int signalId = 0,
                long payload0 = 0,
                long payload1 = 0,
                long payload2 = 0)
            {
                Operation = operation;
                SignalId = signalId;
                Payload0 = payload0;
                Payload1 = payload1;
                Payload2 = payload2;
            }

            internal GameplayTriggerInternalDeferredOperationKind Operation { get; }
            internal int SignalId { get; }
            internal long Payload0 { get; }
            internal long Payload1 { get; }
            internal long Payload2 { get; }
            internal bool IsValid => Operation != GameplayTriggerInternalDeferredOperationKind.None;
        }

        public enum DeferredRestoreFailure
        {
            Capacity,
            DuplicateSequence,
            Order,
            Record,
            Target,
            Origin,
            NextSequence
        }

        public enum DeferredHashField
        {
            None,
            OriginalDueFrame,
            Sequence,
            OperationKind,
            ActivationId,
            ActionIndex,
            ActionKindId,
            OriginBindingId,
            OriginRouteId,
            TargetInstanceIndex,
            TargetInstanceGeneration,
            TargetSourceBindingId,
            Enabled,
            SignalId,
            SubjectIndex,
            SubjectGeneration,
            InstigatorIndex,
            InstigatorGeneration,
            Payload0,
            Payload1,
            Payload2
        }

        private sealed class ObservingSink : IGameplayTriggerActionSink
        {
            private readonly bool _accept;
            private GameplayTriggerRuntime _runtime;

            internal ObservingSink(bool accept)
            {
                _accept = accept;
            }

            public GameplayTriggerActionKindId ActionKindId =>
                new GameplayTriggerActionKindId(9001);
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            internal RuntimeCheckpointQuiescenceResult QuiescenceDuringPrepare { get; private set; }
            internal RuntimeSaveStateResult<RuntimeSaveState> CaptureDuringPrepare { get; private set; }
            internal long HashDuringPrepare { get; private set; }
            internal int CommitCount { get; private set; }

            internal void Attach(GameplayTriggerRuntime runtime)
            {
                _runtime = runtime;
            }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                HashDuringPrepare = Hash(_runtime);
                GameplayTriggerRuntimeSaveStateParticipant participant = Participant(_runtime);
                QuiescenceDuringPrepare = participant.CheckQuiescence(
                    new RuntimeCheckpointContext(preparation.Frame.Value));
                CaptureDuringPrepare = participant.CaptureSaveState();
                token = 1;
                return _accept;
            }

            public void Abort(int token) { }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
            }
        }

        private sealed class CountingSink : IGameplayTriggerActionSink
        {
            internal CountingSink(GameplayTriggerActionKindId actionKindId)
            {
                ActionKindId = actionKindId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            internal int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = 1;
                return true;
            }

            public void Abort(int token) { }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
            }
        }

        private sealed class Sink : IGameplayTriggerActionSink
        {
            public GameplayTriggerActionKindId ActionKindId =>
                new GameplayTriggerActionKindId(9001);
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(in GameplayTriggerActionPreparation preparation, out int token)
            {
                token = 1;
                return true;
            }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class Validator : IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId) => entityId.IsValid;
        }
    }
}
