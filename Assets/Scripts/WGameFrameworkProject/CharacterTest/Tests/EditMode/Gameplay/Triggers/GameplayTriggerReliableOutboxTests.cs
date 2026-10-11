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
    public sealed class GameplayTriggerReliableOutboxTests
    {
        private static readonly GameplayTriggerSourceBindingId SourceBindingId =
            new GameplayTriggerSourceBindingId(101);
        private static readonly GameplayEntityId Owner = new GameplayEntityId(7, 2);
        private static readonly GameplayEntityId Subject = new GameplayEntityId(11, 3);
        private static readonly GameplayEntityId Instigator = new GameplayEntityId(12, 4);

        [Test]
        public void RuntimeOptions_PreserveTenAndElevenParameterConstructors()
        {
            var ten = new GameplayTriggerRuntimeOptions(
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            var eleven = new GameplayTriggerRuntimeOptions(
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
            var twelve = new GameplayTriggerRuntimeOptions(
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);

            Assert.AreEqual(0, ten.MaxDeferredRecords);
            Assert.AreEqual(0, ten.MaxReliableOutboxRecords);
            Assert.AreEqual(11, eleven.MaxDeferredRecords);
            Assert.AreEqual(0, eleven.MaxReliableOutboxRecords);
            Assert.AreEqual(11, twelve.MaxDeferredRecords);
            Assert.AreEqual(12, twelve.MaxReliableOutboxRecords);
        }

        [Test]
        public void ReliableAction_QueuesFullFrozenRecordWithoutTouchingLegacySink()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, maxReliableRecords: 2);

            QueueSignal(runtime, new RuntimeFrame(4));

            Assert.AreEqual(0, reliable.TryPrepareCount);
            Assert.AreEqual(0, reliable.AbortCount);
            Assert.AreEqual(0, reliable.CommitCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(2L, runtime.NextReliableOutboxSequence);
            GameplayTriggerReliableOutboxRecord record = runtime.GetReliableOutboxRecord(0);
            Assert.AreEqual(new RuntimeFrame(4), record.OriginalActivationFrame);
            Assert.AreEqual(new RuntimeFrame(5), record.OriginalDueFrame);
            Assert.AreEqual(1L, record.Sequence);
            Assert.AreEqual(1L, record.ActivationId);
            Assert.AreEqual(0, record.ActionIndex);
            Assert.AreEqual(reliable.ActionKindId, record.ActionKindId);
            Assert.AreEqual(new GameplayTriggerBindingId(1), record.OriginBindingId);
            Assert.AreEqual(new GameplayTriggerRouteId(1), record.OriginRouteId);
            Assert.AreEqual(runtime.GetInstanceId(0), record.OriginInstanceId);
            Assert.AreEqual(GameplayTriggerOccurrenceKind.Pulsed, record.OccurrenceKind);
            Assert.AreEqual(31, record.OccurrenceSignalId);
            Assert.AreEqual(101L, record.OccurrencePayload0);
            Assert.AreEqual(102L, record.OccurrencePayload1);
            Assert.AreEqual(103L, record.OccurrencePayload2);
            Assert.AreEqual(GameplayTriggerTargetRef.FromGameplayEntity(Owner), record.ResolvedTarget);
            Assert.AreEqual(Owner, record.Owner);
            Assert.AreEqual(Subject, record.Subject);
            Assert.AreEqual(Instigator, record.Instigator);
            Assert.AreEqual(201L, record.ActionPayload0);
            Assert.AreEqual(202L, record.ActionPayload1);
            Assert.AreEqual(203L, record.ActionPayload2);
        }

        [Test]
        public void LaterActionRejects_AbortsReliableReservationWithoutAllocatorGap()
        {
            var reliable = new ThrowingReliableSink(1001);
            var rejecting = new RejectingSink(1002);
            GameplayTriggerRuntime runtime = CreateRuntime(
                reliable,
                maxReliableRecords: 1,
                secondSink: rejecting);

            QueueSignal(runtime, RuntimeFrame.Zero);

            Assert.AreEqual(0, reliable.TryPrepareCount);
            Assert.AreEqual(0, reliable.AbortCount);
            Assert.AreEqual(0, reliable.CommitCount);
            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, runtime.NextReliableOutboxSequence);
            Assert.AreEqual(1L, runtime.NextActivationId);
            Assert.AreEqual(GameplayTriggerErrorCode.ReservationRejected,
                runtime.GetDiagnostic(0).Code);
        }

        [Test]
        public void SharedCatalog_RuntimesKeepIndependentQueuesSequencesAndAuthority()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime first = CreateRuntime(reliable, maxReliableRecords: 2);
            var second = new GameplayTriggerRuntime(
                first.AuthorityCatalog,
                first.AuthorityOptions);

            QueueSignal(first, RuntimeFrame.Zero);

            Assert.AreEqual(1, first.ReliableOutboxRecordCount);
            Assert.AreEqual(2L, first.NextReliableOutboxSequence);
            Assert.AreEqual(0, second.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, second.NextReliableOutboxSequence);
            Assert.AreEqual(1L, second.NextActivationId);
        }

        [Test]
        public void ReliableCatalog_RequiresCapacityAndTerminalFrameFailsClosed()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime valid = CreateRuntime(reliable, maxReliableRecords: 1);
            Assert.Throws<ArgumentException>(() =>
                CreateRuntime(new ThrowingReliableSink(1001), maxReliableRecords: 0));

            QueueSignal(valid, new RuntimeFrame(long.MaxValue));

            Assert.AreEqual(0, valid.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, valid.NextReliableOutboxSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.ReliableOutboxFrameOverflow,
                valid.GetDiagnostic(0).Code);
        }

        [Test]
        public void TransferRegistry_RejectsInvalidEntriesWithoutPoisoningRuntimeBinding()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);

            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[] { null }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[]
                    {
                        new FakeTransfer(default, "target")
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[]
                    {
                        new FakeTransfer(reliable.ActionKindId, " ")
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[]
                    {
                        new FakeTransfer(new GameplayTriggerActionKindId(9999), "target")
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[]
                    {
                        new FakeTransfer(reliable.ActionKindId, "target"),
                        new FakeTransfer(reliable.ActionKindId, "target")
                    }));
            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    Array.Empty<IGameplayTriggerReliableTargetTransfer>(),
                    moduleId: " "));

            var valid = new GameplayTriggerReliableOutboxModule(runtime);
            Assert.NotNull(valid);
            var host = new RuntimeHost();
            Assert.DoesNotThrow(() => host.RegisterModule(valid));
            Assert.DoesNotThrow(host.Initialize);
            Assert.Throws<InvalidOperationException>(() =>
                new GameplayTriggerReliableOutboxModule(runtime));
        }

        [Test]
        public void TransferRegistry_RejectsCatalogActionThatIsNotReliableDeferred()
        {
            var reliable = new ThrowingReliableSink(1001);
            var sameFrame = new AcceptingSink(1002);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2, sameFrame);

            Assert.Throws<ArgumentException>(() =>
                new GameplayTriggerReliableOutboxModule(
                    runtime,
                    new IGameplayTriggerReliableTargetTransfer[]
                    {
                        new FakeTransfer(sameFrame.ActionKindId, "target")
                    }));
        }

        [Test]
        public void MissingTransferAndMissingTargetModule_RetainAndPublishDeterministicResult()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime missingTransferRuntime = CreateRuntime(reliable, 2);
            QueueSignal(missingTransferRuntime, RuntimeFrame.Zero);
            var missingTransferModule = new GameplayTriggerReliableOutboxModule(
                missingTransferRuntime);
            RuntimeHost missingTransferHost = RunHost(
                missingTransferModule,
                null,
                frame: 1L);

            Assert.AreEqual(1, missingTransferRuntime.ReliableOutboxRecordCount);
            Assert.AreEqual(
                GameplayTriggerReliableTransferStatus.TargetMissing,
                missingTransferRuntime.LastReliableOutboxExecutionResult.TransferResult.Status);
            Assert.AreEqual(new RuntimeFrame(1),
                missingTransferRuntime.LastReliableOutboxExecutionResult.AttemptFrame);
            missingTransferHost.Dispose();

            GameplayTriggerRuntime missingModuleRuntime = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            QueueSignal(missingModuleRuntime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(
                new GameplayTriggerActionKindId(1001),
                "missing.target");
            var missingModuleOutbox = new GameplayTriggerReliableOutboxModule(
                missingModuleRuntime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            RuntimeHost missingModuleHost = RunHost(
                missingModuleOutbox,
                null,
                frame: 1L);

            Assert.AreEqual(0, transfer.CallCount);
            Assert.AreEqual(1, missingModuleRuntime.ReliableOutboxRecordCount);
            Assert.AreEqual(
                GameplayTriggerReliableTransferStatus.TargetModuleUnavailable,
                missingModuleRuntime.LastReliableOutboxExecutionResult.TransferResult.Status);
            missingModuleHost.Dispose();
        }

        [TestCase(GameplayTriggerReliableTransferStatus.TargetMissing)]
        [TestCase(GameplayTriggerReliableTransferStatus.TargetDisabled)]
        [TestCase(GameplayTriggerReliableTransferStatus.TargetCapacityExceeded)]
        [TestCase(GameplayTriggerReliableTransferStatus.TransientRejected)]
        [TestCase(GameplayTriggerReliableTransferStatus.TargetModuleUnavailable)]
        public void TransientStatuses_RetainRecordUnchanged(
            GameplayTriggerReliableTransferStatus status)
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(
                reliable.ActionKindId,
                "target",
                new GameplayTriggerReliableTransferResult(status, 77));
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);

            RuntimeHost host = RunHost(outbox, target, 1L);

            Assert.AreEqual(1, transfer.CallCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, runtime.GetReliableOutboxRecord(0).Sequence);
            Assert.AreEqual(status,
                runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
            Assert.AreEqual(77,
                runtime.LastReliableOutboxExecutionResult.TransferResult.DetailCode);
            host.Dispose();
        }

        [Test]
        public void Accepted_RemovesExactlyOnceAndUsesCurrentRetryFrame()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(
                reliable.ActionKindId,
                "target",
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected));
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);
            var host = new RuntimeHost();
            host.RegisterModule(target);
            host.RegisterModule(outbox);
            host.Initialize();
            host.Start();

            host.Tick(1L, 0d);
            transfer.Result = new GameplayTriggerReliableTransferResult(
                GameplayTriggerReliableTransferStatus.Accepted);
            host.Tick(4L, 0d);
            host.Tick(5L, 0d);

            Assert.AreEqual(2, transfer.CallCount);
            Assert.AreEqual(new RuntimeFrame(4), transfer.LastFrame);
            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.Accepted,
                runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
            host.Dispose();
        }

        [Test]
        public void DisabledTransferAndDisabledModule_RetainWithoutCallback()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(reliable.ActionKindId, "target")
            {
                Enabled = false
            };
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(target);
            host.Initialize();
            host.Start();

            host.Tick(1L, 0d);
            outbox.SetEnabled(false);
            transfer.Enabled = true;
            host.Tick(2L, 0d);

            Assert.AreEqual(0, transfer.CallCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(GameplayTriggerReliableTransferStatus.TargetDisabled,
                runtime.LastReliableOutboxExecutionResult.TransferResult.Status);
            host.Dispose();
        }

        [TestCase(RuntimeTickStage.PreSimulation, -1, "target", false)]
        [TestCase(RuntimeTickStage.PreSimulation, 0, "aaa.target", false)]
        [TestCase(RuntimeTickStage.PreSimulation, 0, "zzz.target", true)]
        [TestCase(RuntimeTickStage.PreSimulation, 1, "target", true)]
        [TestCase(RuntimeTickStage.Simulation, -100, "target", true)]
        public void Initialize_RequiresExactStrictlyLaterSchedulingTuple(
            RuntimeTickStage targetStage,
            int targetPriority,
            string targetModuleId,
            bool accepted)
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            var transfer = new FakeTransfer(reliable.ActionKindId, targetModuleId);
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule(targetModuleId, targetStage, targetPriority);
            var host = new RuntimeHost();
            host.RegisterModule(target);
            host.RegisterModule(outbox);

            if (accepted)
                Assert.DoesNotThrow(host.Initialize);
            else
                Assert.Throws<RuntimeHostException>(host.Initialize);
        }

        [Test]
        public void InvalidStatusAndThrownCallback_PropagateAndKeepCurrentRecord()
        {
            AssertInvalidTransferLeavesRecord(
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.None),
                throwCallback: false);
            AssertInvalidTransferLeavesRecord(
                new GameplayTriggerReliableTransferResult(
                    (GameplayTriggerReliableTransferStatus)99),
                throwCallback: false);
            AssertInvalidTransferLeavesRecord(default, throwCallback: true);
        }

        [Test]
        public void CallbackCapture_ReportsPendingReliableTransferDetailSeven()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(
                reliable.ActionKindId,
                "target",
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected));
            transfer.CaptureRuntime = runtime;
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);

            RuntimeHost host = RunHost(outbox, target, 1L);

            Assert.IsFalse(transfer.CaptureDuringCallback.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.PendingWork,
                transfer.CaptureDuringCallback.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingReliableOutboxTransfer,
                transfer.CaptureDuringCallback.Error.DetailCode);
            Assert.IsFalse(runtime.ReliableTransferInFlight);
            host.Dispose();
        }

        [Test]
        public void MixedActionPreparation_CaptureReportsExistingReservationDetail()
        {
            var reliable = new ThrowingReliableSink(1001);
            var observer = new CapturingSink(1002);
            GameplayTriggerRuntime runtime = CreateRuntime(
                reliable,
                maxReliableRecords: 2,
                secondSink: observer);
            observer.Runtime = runtime;

            QueueSignal(runtime, RuntimeFrame.Zero);

            Assert.IsFalse(observer.CaptureDuringPrepare.Success);
            Assert.AreEqual(
                RuntimeSaveStateErrorCode.PendingWork,
                observer.CaptureDuringPrepare.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerCheckpointPendingDetailCode.PendingDeferredReservations,
                observer.CaptureDuringPrepare.Error.DetailCode);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
        }

        [Test]
        public void AcceptedBeforeTargetThrow_RemainsRemovedAndCheckpointRejectsFailedTick()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            RuntimeSaveState priorCheckpoint = Capture(runtime);
            var transfer = new FakeTransfer(reliable.ActionKindId, "target.throwing");
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(new ThrowingTickModule("target.throwing"));
            host.Initialize();
            host.Start();

            Assert.Throws<RuntimeHostException>(() => host.Tick(1L, 0d));

            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1, transfer.CallCount);
            Assert.IsFalse(host.LastTickAttemptSucceeded);
            var checkpointRegistry = new RuntimeSaveStateRegistry();
            Assert.IsTrue(checkpointRegistry.Register(
                new RuntimeSaveStateRootHeaderProvider(
                    new RuntimeSaveStateRootHeaderDescriptor(
                        RuntimeSaveState.CurrentSchemaVersion,
                        "framework-test",
                        "config-test",
                        "resources-test"))
                    .CreateParticipant()).Success);
            RuntimeSaveStateCoordinatorFactoryResult checkpointFactory =
                RuntimeSaveStateCoordinator.CreateAuthoritative(checkpointRegistry);
            Assert.IsTrue(checkpointFactory.Success, checkpointFactory.Error?.ToString());
            var barrier = new RuntimeCheckpointBarrier(
                host,
                checkpointFactory.Coordinator,
                new RuntimeCheckpointQuiescenceRegistry());
            RuntimeSaveStateCoordinatorResult<RuntimeSaveState> checkpoint =
                barrier.CaptureCheckpoint();
            Assert.IsFalse(checkpoint.Success);
            Assert.AreEqual(1, checkpoint.Errors.Count);
            Assert.AreEqual(
                (int)RuntimeCheckpointErrorDetailCode.HostLastTickAttemptFailed,
                checkpoint.Errors[0].Error.DetailCode);
            host.Dispose();

            GameplayTriggerRuntime rebuilt = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            Assert.IsTrue(Participant(rebuilt).RestoreSaveState(priorCheckpoint).Success);
            var rebuiltTransfer = new FakeTransfer(
                new GameplayTriggerActionKindId(1001),
                "target.rebuilt");
            var rebuiltOutbox = new GameplayTriggerReliableOutboxModule(
                rebuilt,
                new IGameplayTriggerReliableTargetTransfer[] { rebuiltTransfer });
            RuntimeHost rebuiltHost = RunHost(
                rebuiltOutbox,
                new TestModule(
                    "target.rebuilt",
                    RuntimeTickStage.Simulation,
                    0),
                1L);
            Assert.AreEqual(0, rebuilt.ReliableOutboxRecordCount);
            Assert.AreEqual(1, rebuiltTransfer.CallCount);
            rebuiltHost.Dispose();
        }

        [Test]
        public void EarlierTransientRecord_DoesNotBlockLaterDifferentTarget()
        {
            var firstSink = new ThrowingReliableSink(1001);
            var secondSink = new ThrowingReliableSink(1002);
            GameplayTriggerRuntime runtime = CreateRuntime(firstSink, 2, secondSink);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var firstTransfer = new FakeTransfer(
                firstSink.ActionKindId,
                "target.first",
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected));
            var secondTransfer = new FakeTransfer(
                secondSink.ActionKindId,
                "target.second");
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[]
                {
                    secondTransfer,
                    firstTransfer
                });
            var host = new RuntimeHost();
            host.RegisterModule(new TestModule(
                "target.second",
                RuntimeTickStage.Simulation,
                1));
            host.RegisterModule(outbox);
            host.RegisterModule(new TestModule(
                "target.first",
                RuntimeTickStage.Simulation,
                0));
            host.Initialize();
            host.Start();

            host.Tick(1L, 0d);

            Assert.AreEqual(1, firstTransfer.CallCount);
            Assert.AreEqual(1, secondTransfer.CallCount);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, runtime.GetReliableOutboxRecord(0).Sequence);
            host.Dispose();
        }

        [Test]
        public void TransferRouting_FreezesActionKindAndTargetModuleAtConstruction()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            var transfer = new FakeTransfer(reliable.ActionKindId, "target.original");
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            transfer.ActionKindId = new GameplayTriggerActionKindId(9999);
            transfer.TargetModuleId = "target.mutated";
            QueueSignal(runtime, RuntimeFrame.Zero);

            RuntimeHost host = RunHost(
                outbox,
                new TestModule("target.original", RuntimeTickStage.Simulation, 0),
                1L);

            Assert.AreEqual(1, transfer.CallCount);
            Assert.AreEqual(0, runtime.ReliableOutboxRecordCount);
            host.Dispose();
        }

        [Test]
        public void Initialize_NonFailFastHostFailsClosed()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            var outbox = new GameplayTriggerReliableOutboxModule(runtime);
            var options = new RuntimeHostOptions
            {
                ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue
            };
            var host = new RuntimeHost(options);
            host.RegisterModule(outbox);

            host.Initialize();

            Assert.AreEqual(RuntimeLifecycleState.Faulted, host.State);
            Assert.AreEqual(1, host.Errors.Count);
            Assert.IsInstanceOf<InvalidOperationException>(host.Errors[0].Exception);
        }

        [Test]
        public void SaveRestore_RoundTripsFullReliableRecordAndStateHash()
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            QueueSignal(source, new RuntimeFrame(4));
            RuntimeSaveState captured = Capture(source);
            long expectedHash = Hash(source);
            GameplayTriggerRuntime target = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);

            RuntimeSaveStateResult<bool> restore = Participant(target)
                .RestoreSaveState(captured);

            Assert.IsTrue(restore.Success, restore.Error.ToString());
            Assert.AreEqual(3,
                captured.ModuleStates[0].SchemaVersion);
            Assert.AreEqual(1, target.ReliableOutboxRecordCount);
            Assert.AreEqual(2L, target.NextReliableOutboxSequence);
            Assert.AreEqual(expectedHash, Hash(target));
            Assert.AreEqual(Payload(captured), Payload(Capture(target)));
        }

        [Test]
        public void MixedInternalAndReliable_SaveRestoreExecutesEachOnceAtDueFrame()
        {
            GameplayTriggerRuntime source = CreateMixedDeferredReliableRuntime();
            QueueSignal(source, RuntimeFrame.Zero);
            Assert.AreEqual(1, source.DeferredRecordCount);
            Assert.AreEqual(1, source.ReliableOutboxRecordCount);
            RuntimeSaveState checkpoint = Capture(source);
            GameplayTriggerRuntime restored = CreateMixedDeferredReliableRuntime();
            Assert.IsTrue(Participant(restored).RestoreSaveState(checkpoint).Success);
            var transfer = new FakeTransfer(
                new GameplayTriggerActionKindId(1002),
                "target.mixed");
            var outbox = new GameplayTriggerReliableOutboxModule(
                restored,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(new TestModule(
                "target.mixed",
                RuntimeTickStage.Simulation,
                0));
            host.Initialize();
            host.Start();

            new GameplayTriggerDeferredSystem(restored).Tick(
                Context(new RuntimeFrame(1)));
            host.Tick(1L, 0d);

            Assert.AreEqual(0, restored.DeferredRecordCount);
            Assert.AreEqual(0, restored.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, restored.LastDeferredExecutionResult.Sequence);
            Assert.AreEqual(1, transfer.CallCount);

            new GameplayTriggerDeferredSystem(restored).Tick(
                Context(new RuntimeFrame(2)));
            host.Tick(2L, 0d);
            Assert.AreEqual(1, transfer.CallCount);
            host.Dispose();
        }

        [Test]
        public void CheckpointRestoreAndExogenousReplay_ReconstructsDerivedDeliveryOnce()
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            QueueSignal(source, RuntimeFrame.Zero);
            RuntimeSaveState checkpoint = Capture(source);

            DerivedDeliveryRun first = RunDerivedDelivery(source, 1L);
            Assert.AreEqual(1, first.Target.ReceivedCount);
            Assert.IsTrue(first.Target.LastCommandValid);
            Assert.AreEqual(0, source.ReliableOutboxRecordCount);

            var recorder = RuntimeReplayRecorder.CreateExogenousStream(
                new RuntimeReplayHeader(1, "test", "config", "resources", RuntimeFrame.Zero),
                first.SourceRegistry);
            RuntimeReplayFrameRecord frame = recorder.RecordFrame(
                new RuntimeFrame(1),
                new[] { first.Transfer.LastAcceptedCommand },
                0L);
            Assert.AreEqual(RuntimeReplayCommandCaptureMode.ExogenousFiltered,
                recorder.CaptureMode);
            Assert.AreEqual(0, frame.Commands.Count);
            first.Host.Dispose();

            GameplayTriggerRuntime replay = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            Assert.IsTrue(Participant(replay).RestoreSaveState(checkpoint).Success);
            DerivedDeliveryRun reconstructed = RunDerivedDelivery(replay, 1L);

            Assert.AreEqual(1, reconstructed.Target.ReceivedCount);
            Assert.IsTrue(reconstructed.Target.LastCommandValid);
            Assert.AreEqual(0, replay.ReliableOutboxRecordCount);
            reconstructed.Host.Tick(2L, 0d);
            Assert.AreEqual(1, reconstructed.Target.ReceivedCount);
            reconstructed.Host.Dispose();
        }

        [Test]
        public void RestoreThenEvaluate_AppendsCanonicalAndSecondRestoreRemainsValid()
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                3);
            QueueSignal(source, new RuntimeFrame(4));
            RuntimeSaveState accepted = Capture(source);
            GameplayTriggerRuntime continued = CreateRuntime(
                new ThrowingReliableSink(1001),
                3);
            Assert.IsTrue(Participant(continued).RestoreSaveState(accepted).Success);

            QueueSignal(continued, new RuntimeFrame(5));

            Assert.AreEqual(2, continued.ReliableOutboxRecordCount);
            Assert.AreEqual(new RuntimeFrame(5),
                continued.GetReliableOutboxRecord(0).OriginalDueFrame);
            Assert.AreEqual(new RuntimeFrame(6),
                continued.GetReliableOutboxRecord(1).OriginalDueFrame);
            Assert.AreEqual(1L, continued.GetReliableOutboxRecord(0).Sequence);
            Assert.AreEqual(2L, continued.GetReliableOutboxRecord(1).Sequence);
            RuntimeSaveState roundTrip = Capture(continued);
            GameplayTriggerRuntime second = CreateRuntime(
                new ThrowingReliableSink(1001),
                3);
            RuntimeSaveStateResult<bool> secondRestore = Participant(second)
                .RestoreSaveState(roundTrip);
            Assert.IsTrue(secondRestore.Success, secondRestore.Error.ToString());
            Assert.AreEqual(Payload(roundTrip), Payload(Capture(second)));
            Assert.AreEqual(Hash(continued), Hash(second));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void LegacyMigration_InjectsEmptyReliableOutboxAndSequenceOne(int schema)
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            RuntimeSaveState current = Capture(source);
            JObject payload = JObject.Parse(Payload(current));
            payload["schemaVersion"] = schema;
            JObject options = (JObject)payload["options"];
            options.Property("maxReliableOutboxRecords").Remove();
            payload.Property("nextReliableOutboxSequence").Remove();
            payload.Property("reliableOutboxRecords").Remove();
            if (schema == 1)
            {
                payload["definitionCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1DefinitionCatalogHash;
                payload["bindingCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1BindingCatalogHash;
                payload["gameplayTriggerCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1GameplayTriggerCatalogHash;
                options.Property("maxDeferredRecords").Remove();
                payload.Property("nextDeferredSequence").Remove();
                payload.Property("deferredRecords").Remove();
            }
            RuntimeSaveState legacy = WithSchemaAndPayload(
                current,
                schema,
                payload.ToString(Formatting.None));
            GameplayTriggerRuntime migrated = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);

            RuntimeSaveStateResult<bool> result = Participant(migrated)
                .RestoreSaveState(legacy);

            Assert.IsTrue(result.Success, result.Error.ToString());
            Assert.AreEqual(0, migrated.ReliableOutboxRecordCount);
            Assert.AreEqual(1L, migrated.NextReliableOutboxSequence);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void LegacyPayload_RejectsExplicitV3FieldSmuggling(int schema)
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            RuntimeSaveState current = Capture(source);
            JObject payload = JObject.Parse(Payload(current));
            payload["schemaVersion"] = schema;
            JObject options = (JObject)payload["options"];
            if (schema == 1)
            {
                payload["definitionCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1DefinitionCatalogHash;
                payload["bindingCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1BindingCatalogHash;
                payload["gameplayTriggerCatalogHash"] =
                    source.AuthorityCatalog.LegacyV1GameplayTriggerCatalogHash;
                options.Property("maxDeferredRecords").Remove();
                payload.Property("nextDeferredSequence").Remove();
                payload.Property("deferredRecords").Remove();
            }
            options["maxReliableOutboxRecords"] = JValue.CreateNull();
            payload["nextReliableOutboxSequence"] = JValue.CreateNull();
            payload["reliableOutboxRecords"] = JValue.CreateNull();

            RuntimeSaveStateResult<bool> result = Participant(
                    CreateRuntime(new ThrowingReliableSink(1001), 2))
                .RestoreSaveState(WithSchemaAndPayload(
                    current,
                    schema,
                    payload.ToString(Formatting.None)));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeSaveStateErrorCode.CustomStateMismatch,
                result.Error.Code);
            Assert.AreEqual(
                (int)GameplayTriggerSaveStateErrorDetailCode.PayloadSchemaMismatch,
                result.Error.DetailCode);
        }

        [TestCase(ReliableRestoreFailure.Capacity)]
        [TestCase(ReliableRestoreFailure.DuplicateSequence)]
        [TestCase(ReliableRestoreFailure.DuplicateOrigin)]
        [TestCase(ReliableRestoreFailure.Order)]
        [TestCase(ReliableRestoreFailure.Record)]
        [TestCase(ReliableRestoreFailure.Target)]
        [TestCase(ReliableRestoreFailure.Origin)]
        [TestCase(ReliableRestoreFailure.NextSequence)]
        public void Restore_ReliableTailFailuresUseStableDetailsAndRemainAtomic(
            ReliableRestoreFailure failure)
        {
            var firstSink = new ThrowingReliableSink(1001);
            var secondSink = new ThrowingReliableSink(1002);
            GameplayTriggerRuntime source = CreateRuntime(firstSink, 2, secondSink);
            QueueSignal(source, RuntimeFrame.Zero);
            RuntimeSaveState valid = Capture(source);
            JObject payload = JObject.Parse(Payload(valid));
            JArray records = (JArray)payload["reliableOutboxRecords"];
            GameplayTriggerSaveStateErrorDetailCode expected;
            switch (failure)
            {
                case ReliableRestoreFailure.Capacity:
                    records.Add(records[0].DeepClone());
                    expected = GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxCapacityExceeded;
                    break;
                case ReliableRestoreFailure.DuplicateSequence:
                    records[1]["sequence"] = records[0]["sequence"];
                    expected = GameplayTriggerSaveStateErrorDetailCode.DuplicateReliableOutboxSequence;
                    break;
                case ReliableRestoreFailure.DuplicateOrigin:
                    records[1]["activationId"] = records[0]["activationId"];
                    records[1]["actionIndex"] = records[0]["actionIndex"];
                    expected = GameplayTriggerSaveStateErrorDetailCode.DuplicateReliableOutboxOrigin;
                    break;
                case ReliableRestoreFailure.Order:
                    JToken first = records[0].DeepClone();
                    records[0] = records[1].DeepClone();
                    records[1] = first;
                    expected = GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOrderInvalid;
                    break;
                case ReliableRestoreFailure.Record:
                    records[1]["occurrencePresenceEpoch"] = 1L;
                    expected = GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxRecordInvalid;
                    break;
                case ReliableRestoreFailure.Target:
                    records[1]["targetValue"] = 999L;
                    expected = GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxTargetInvalid;
                    break;
                case ReliableRestoreFailure.Origin:
                    records[1]["originRouteId"] = 999;
                    expected = GameplayTriggerSaveStateErrorDetailCode.ReliableOutboxOriginInvalid;
                    break;
                default:
                    payload["nextReliableOutboxSequence"] = 0L;
                    expected = GameplayTriggerSaveStateErrorDetailCode.NextReliableOutboxSequenceInvalid;
                    break;
            }

            GameplayTriggerRuntime target = CreateRuntime(
                new ThrowingReliableSink(1001),
                2,
                new ThrowingReliableSink(1002));
            QueueSignal(target, new RuntimeFrame(4));
            string beforePayload = Payload(Capture(target));
            long beforeHash = Hash(target);

            RuntimeSaveStateResult<bool> result = Participant(target)
                .RestoreSaveState(WithPayload(
                    valid,
                    payload.ToString(Formatting.None)));

            Assert.IsFalse(result.Success);
            Assert.AreEqual((int)expected, result.Error.DetailCode);
            Assert.AreEqual(beforePayload, Payload(Capture(target)));
            Assert.AreEqual(beforeHash, Hash(target));
        }

        [Test]
        public void Restore_AllowsExhaustedReliableAllocatorAndPrepareReturns32()
        {
            GameplayTriggerRuntime source = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            QueueSignal(source, RuntimeFrame.Zero);
            RuntimeSaveState state = Capture(source);
            JObject payload = JObject.Parse(Payload(state));
            payload["nextReliableOutboxSequence"] = long.MaxValue;
            GameplayTriggerRuntime restored = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            Assert.IsTrue(Participant(restored).RestoreSaveState(
                WithPayload(state, payload.ToString(Formatting.None))).Success);

            QueueSignal(restored, new RuntimeFrame(1));

            Assert.AreEqual(1, restored.ReliableOutboxRecordCount);
            Assert.AreEqual(long.MaxValue, restored.NextReliableOutboxSequence);
            Assert.AreEqual(GameplayTriggerErrorCode.ReliableOutboxSequenceExhausted,
                restored.GetDiagnostic(0).Code);
        }

        [Test]
        public void TransferDiagnosticsAndModuleEnablement_DoNotEnterHashOrSave()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            long beforeHash = Hash(runtime);
            string beforePayload = Payload(Capture(runtime));
            var transfer = new FakeTransfer(
                reliable.ActionKindId,
                "target",
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected,
                    77));
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            outbox.SetEnabled(false);
            outbox.SetEnabled(true);
            RuntimeHost host = RunHost(
                outbox,
                new TestModule("target", RuntimeTickStage.Simulation, 0),
                1L);

            Assert.AreEqual(beforeHash, Hash(runtime));
            Assert.AreEqual(beforePayload, Payload(Capture(runtime)));
            host.Dispose();
        }

        [Test]
        public void ReliableRecordHash_CoversEveryFrozenFieldAndSchemaSplit()
        {
            Assert.AreEqual(2, GameplayTriggerCatalog.CurrentCatalogHashSchemaVersion);
            Assert.AreEqual(3,
                GameplayTriggerRuntimeHashContributor.CurrentStateSchemaVersion);
            Assert.AreEqual(3,
                GameplayTriggerRuntimeSaveStateParticipant.CurrentPayloadSchemaVersion);
            long baseline = HashRecord(ReliableRecord(ReliableHashField.None));

            foreach (ReliableHashField field in Enum.GetValues(typeof(ReliableHashField)))
            {
                if (field == ReliableHashField.None) continue;
                Assert.AreNotEqual(
                    baseline,
                    HashRecord(ReliableRecord(field)),
                    field.ToString());
            }
        }

        [Test]
        public void DeliveryRetryAndCapacityOverflow_DoNotAllocateAfterWarmup()
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 1);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(
                reliable.ActionKindId,
                "target",
                new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.TransientRejected));
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(target);
            host.Initialize();
            host.Start();
            var signals = new[]
            {
                new GameplayTriggerSignal(31, Subject, Instigator, 101, 102, 103)
            };
            for (long frame = 1L; frame <= 8L; frame++)
            {
                host.Tick(frame, 0d);
                runtime.TrySubmitSignals(
                    new RuntimeFrame(frame),
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    signals,
                    out _);
                runtime.Evaluate(new RuntimeFrame(frame));
            }
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (long frame = 9L; frame < 73L; frame++)
            {
                host.Tick(frame, 0d);
                runtime.TrySubmitSignals(
                    new RuntimeFrame(frame),
                    SourceBindingId,
                    GameplayTriggerSignalOrdering.UnorderedMultiset,
                    signals,
                    out _);
                runtime.Evaluate(new RuntimeFrame(frame));
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.AreEqual(72, transfer.CallCount);
            host.Dispose();
        }

        [Test]
        public void NonEmptyReliableOutboxHash_DoesNotAllocateAcross128Calls()
        {
            GameplayTriggerRuntime runtime = CreateRuntime(
                new ThrowingReliableSink(1001),
                2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var contributor = new GameplayTriggerRuntimeHashContributor(runtime);
            var accumulator = new RuntimeHashAccumulator(9500, 1);
            var context = new RuntimeHashContext(RuntimeFrame.Zero);
            for (int i = 0; i < 8; i++) contributor.Contribute(context, accumulator);
            _ = GC.GetAllocatedBytesForCurrentThread();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) contributor.Contribute(context, accumulator);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0L, allocated);
        }

        private static RuntimeHost RunHost(
            GameplayTriggerReliableOutboxModule outbox,
            RuntimeModule target,
            long frame)
        {
            var host = new RuntimeHost();
            if (target != null) host.RegisterModule(target);
            host.RegisterModule(outbox);
            host.Initialize();
            host.Start();
            host.Tick(frame, 0d);
            return host;
        }

        private static void AssertInvalidTransferLeavesRecord(
            GameplayTriggerReliableTransferResult result,
            bool throwCallback)
        {
            var reliable = new ThrowingReliableSink(1001);
            GameplayTriggerRuntime runtime = CreateRuntime(reliable, 2);
            QueueSignal(runtime, RuntimeFrame.Zero);
            var transfer = new FakeTransfer(reliable.ActionKindId, "target", result)
            {
                ThrowOnTransfer = throwCallback
            };
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var target = new TestModule("target", RuntimeTickStage.Simulation, 0);
            var host = new RuntimeHost();
            host.RegisterModule(outbox);
            host.RegisterModule(target);
            host.Initialize();
            host.Start();

            Assert.Throws<RuntimeHostException>(() => host.Tick(1L, 0d));
            Assert.AreEqual(1, runtime.ReliableOutboxRecordCount);
            Assert.IsFalse(runtime.ReliableTransferInFlight);
        }

        private static GameplayTriggerRuntime CreateRuntime(
            ThrowingReliableSink reliable,
            int maxReliableRecords,
            IGameplayTriggerActionSink secondSink = null)
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var actions = secondSink == null
                ? new[]
                {
                    new GameplayTriggerActionInstruction(
                        reliable.ActionKindId,
                        GameplayTriggerTargetSelector.Owner,
                        payload0: 201,
                        payload1: 202,
                        payload2: 203)
                }
                : new[]
                {
                    new GameplayTriggerActionInstruction(
                        reliable.ActionKindId,
                        GameplayTriggerTargetSelector.Owner,
                        payload0: 201,
                        payload1: 202,
                        payload2: 203),
                    new GameplayTriggerActionInstruction(
                        secondSink.ActionKindId,
                        GameplayTriggerTargetSelector.Owner)
                };
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                actions);
            var definition = new GameplayTriggerDefinition(
                new GameplayTriggerDefinitionId(1),
                1,
                source.SourceDefinitionId,
                new[] { route });
            var binding = new GameplayTriggerBinding(
                new GameplayTriggerBindingId(1),
                definition.DefinitionId,
                SourceBindingId,
                Owner);
            IGameplayTriggerActionSink[] sinks = secondSink == null
                ? new IGameplayTriggerActionSink[] { reliable }
                : new[] { reliable, secondSink };
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                sinks,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 16, 16, 16, 16, 16, 16, 16, 8, 8, 0,
                    maxReliableRecords));
        }

        private static GameplayTriggerRuntime CreateMixedDeferredReliableRuntime()
        {
            var source = new GameplayTriggerSourceDefinition(
                new GameplayTriggerSourceDefinitionId(1),
                GameplayTriggerSourceKind.Signal,
                GameplayTriggerSignalOrdering.UnorderedMultiset);
            var internalKind = new GameplayTriggerActionKindId(1001);
            var reliable = new ThrowingReliableSink(1002);
            var route = new GameplayTriggerRouteDefinition(
                new GameplayTriggerRouteId(1),
                GameplayTriggerOccurrenceKind.Pulsed,
                GameplayTriggerConditionProgram.Always,
                new[]
                {
                    new GameplayTriggerActionInstruction(
                        internalKind,
                        GameplayTriggerTargetSelector.ExplicitBindingTarget,
                        targetSlotId: 1),
                    new GameplayTriggerActionInstruction(
                        reliable.ActionKindId,
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
                SourceBindingId,
                Owner,
                explicitTargets: new[]
                {
                    new GameplayTriggerBindingTarget(
                        1,
                        GameplayTriggerTargetRef.FromTriggerSourceBinding(
                            SourceBindingId))
                });
            var descriptors = new[]
            {
                new GameplayTriggerInternalDeferredActionDescriptor(
                    internalKind,
                    GameplayTriggerInternalDeferredOperationKind.Reset)
            };
            Assert.IsTrue(GameplayTriggerCatalog.TryCreate(
                new[] { source },
                new[] { definition },
                new[] { binding },
                new IGameplayTriggerActionSink[] { reliable },
                descriptors,
                out GameplayTriggerCatalog catalog,
                out GameplayTriggerCatalogValidationResult validation),
                validation.ErrorCode.ToString());
            return new GameplayTriggerRuntime(
                catalog,
                new GameplayTriggerRuntimeOptions(
                    8, 16, 16, 16, 16, 16, 16, 16, 8, 8, 2, 2));
        }

        private static GameplaySystemContext Context(RuntimeFrame frame)
        {
            var componentWorld = new GameplayComponentWorld();
            return new GameplaySystemContext(
                frame,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                componentWorld.Events,
                new GameplayCommandExecutionState(),
                componentWorld);
        }

        private static DerivedDeliveryRun RunDerivedDelivery(
            GameplayTriggerRuntime runtime,
            long frame)
        {
            const int sourceId = 707;
            const int commandId = 808;
            const int envelopeTypeId = 909;
            var sourceRegistry = new RuntimeCommandSourceRegistry(1);
            Assert.IsTrue(sourceRegistry.TryRegister(
                sourceId,
                RuntimeCommandSourceKind.Derived,
                new[] { commandId },
                out RuntimeCommandSourceRegistrationResult registration),
                registration.ErrorCode.ToString());
            var commandBuffer = new RuntimeCommandBuffer(initialCapacity: 2);
            var gate = new RuntimeCommandSubmissionGate(
                sourceRegistry,
                commandBuffer,
                RuntimeCommandIngressKind.TrustedDerived);
            var transfer = new DerivedCommandTransfer(
                new GameplayTriggerActionKindId(1001),
                "target.derived",
                gate,
                sourceId,
                commandId,
                envelopeTypeId);
            var target = new DerivedCommandTargetModule(
                "target.derived",
                commandBuffer,
                sourceId,
                commandId,
                envelopeTypeId);
            var outbox = new GameplayTriggerReliableOutboxModule(
                runtime,
                new IGameplayTriggerReliableTargetTransfer[] { transfer });
            var host = new RuntimeHost();
            host.RegisterModule(target);
            host.RegisterModule(outbox);
            host.Initialize();
            host.Start();
            host.Tick(frame, 0d);
            return new DerivedDeliveryRun(
                host,
                sourceRegistry,
                transfer,
                target);
        }

        private static void QueueSignal(GameplayTriggerRuntime runtime, RuntimeFrame frame)
        {
            Assert.IsTrue(runtime.TrySubmitSignals(
                frame,
                SourceBindingId,
                GameplayTriggerSignalOrdering.UnorderedMultiset,
                new[]
                {
                    new GameplayTriggerSignal(
                        31,
                        Subject,
                        Instigator,
                        101,
                        102,
                        103)
                },
                out GameplayTriggerResult result),
                result.ErrorCode.ToString());
            runtime.Evaluate(frame);
        }

        private static GameplayTriggerRuntimeSaveStateParticipant Participant(
            GameplayTriggerRuntime runtime) =>
            new GameplayTriggerRuntimeSaveStateParticipant(runtime, new Validator());

        private static RuntimeSaveState Capture(GameplayTriggerRuntime runtime)
        {
            RuntimeSaveStateResult<RuntimeSaveState> result = Participant(runtime)
                .CaptureSaveState();
            Assert.IsTrue(result.Success, result.Error.ToString());
            return result.Value;
        }

        private static string Payload(RuntimeSaveState state) =>
            state.ModuleStates[0].CustomState.PayloadJson;

        private static RuntimeSaveState WithPayload(
            RuntimeSaveState source,
            string payload) =>
            WithSchemaAndPayload(
                source,
                source.ModuleStates[0].SchemaVersion,
                payload);

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

        private static long Hash(GameplayTriggerRuntime runtime)
        {
            var accumulator = new RuntimeHashAccumulator(9500, 1);
            new GameplayTriggerRuntimeHashContributor(runtime).Contribute(
                new RuntimeHashContext(runtime.AuthorityLastEvaluatedFrame),
                accumulator);
            return accumulator.ToHash();
        }

        private static long HashRecord(GameplayTriggerReliableOutboxRecord record)
        {
            var accumulator = new RuntimeHashAccumulator(9500, 1);
            GameplayTriggerRuntimeHashContributor.ContributeReliableOutboxRecord(
                accumulator,
                in record);
            return accumulator.ToHash();
        }

        private static GameplayTriggerReliableOutboxRecord ReliableRecord(
            ReliableHashField changed)
        {
            long originalActivationFrame = 4L;
            long originalDueFrame = 5L;
            long sequence = 1L;
            long activationId = 1L;
            int actionIndex = 0;
            int actionKindId = 1001;
            int bindingId = 1;
            int routeId = 1;
            int instanceIndex = 1;
            int instanceGeneration = 1;
            GameplayTriggerOccurrenceKind occurrenceKind =
                GameplayTriggerOccurrenceKind.Pulsed;
            int occurrenceSignalId = 31;
            long previousScalarRaw = 0L;
            long currentScalarRaw = 0L;
            long occurrencePayload0 = 101L;
            long occurrencePayload1 = 102L;
            long occurrencePayload2 = 103L;
            long occurrenceOrderingValue = 7L;
            long occurrencePresenceEpoch = 0L;
            long occurrencePresenceStartFrame = 0L;
            int targetDomainId = 1;
            long targetValue = 7L;
            int targetGeneration = 2;
            int ownerIndex = 7;
            int ownerGeneration = 2;
            int subjectIndex = 11;
            int subjectGeneration = 3;
            int instigatorIndex = 12;
            int instigatorGeneration = 4;
            long actionPayload0 = 201L;
            long actionPayload1 = 202L;
            long actionPayload2 = 203L;
            switch (changed)
            {
                case ReliableHashField.OriginalActivationFrame: originalActivationFrame++; break;
                case ReliableHashField.OriginalDueFrame: originalDueFrame++; break;
                case ReliableHashField.Sequence: sequence++; break;
                case ReliableHashField.ActivationId: activationId++; break;
                case ReliableHashField.ActionIndex: actionIndex++; break;
                case ReliableHashField.ActionKindId: actionKindId++; break;
                case ReliableHashField.OriginBindingId: bindingId++; break;
                case ReliableHashField.OriginRouteId: routeId++; break;
                case ReliableHashField.OriginInstanceIndex: instanceIndex++; break;
                case ReliableHashField.OriginInstanceGeneration: instanceGeneration++; break;
                case ReliableHashField.OccurrenceKind: occurrenceKind = GameplayTriggerOccurrenceKind.Entered; break;
                case ReliableHashField.OccurrenceSignalId: occurrenceSignalId++; break;
                case ReliableHashField.PreviousScalarRaw: previousScalarRaw++; break;
                case ReliableHashField.CurrentScalarRaw: currentScalarRaw++; break;
                case ReliableHashField.OccurrencePayload0: occurrencePayload0++; break;
                case ReliableHashField.OccurrencePayload1: occurrencePayload1++; break;
                case ReliableHashField.OccurrencePayload2: occurrencePayload2++; break;
                case ReliableHashField.OccurrenceOrderingValue: occurrenceOrderingValue++; break;
                case ReliableHashField.OccurrencePresenceEpoch: occurrencePresenceEpoch++; break;
                case ReliableHashField.OccurrencePresenceStartFrame: occurrencePresenceStartFrame++; break;
                case ReliableHashField.TargetDomainId: targetDomainId++; break;
                case ReliableHashField.TargetValue: targetValue++; break;
                case ReliableHashField.TargetGeneration: targetGeneration++; break;
                case ReliableHashField.OwnerIndex: ownerIndex++; break;
                case ReliableHashField.OwnerGeneration: ownerGeneration++; break;
                case ReliableHashField.SubjectIndex: subjectIndex++; break;
                case ReliableHashField.SubjectGeneration: subjectGeneration++; break;
                case ReliableHashField.InstigatorIndex: instigatorIndex++; break;
                case ReliableHashField.InstigatorGeneration: instigatorGeneration++; break;
                case ReliableHashField.ActionPayload0: actionPayload0++; break;
                case ReliableHashField.ActionPayload1: actionPayload1++; break;
                case ReliableHashField.ActionPayload2: actionPayload2++; break;
            }
            return new GameplayTriggerReliableOutboxRecord(
                new RuntimeFrame(originalActivationFrame),
                new RuntimeFrame(originalDueFrame),
                sequence,
                activationId,
                actionIndex,
                new GameplayTriggerActionKindId(actionKindId),
                new GameplayTriggerBindingId(bindingId),
                new GameplayTriggerRouteId(routeId),
                new GameplayTriggerInstanceId(instanceIndex, instanceGeneration),
                occurrenceKind,
                occurrenceSignalId,
                previousScalarRaw,
                currentScalarRaw,
                occurrencePayload0,
                occurrencePayload1,
                occurrencePayload2,
                occurrenceOrderingValue,
                occurrencePresenceEpoch,
                occurrencePresenceStartFrame,
                new GameplayTriggerTargetRef(
                    targetDomainId,
                    targetValue,
                    targetGeneration),
                new GameplayEntityId(ownerIndex, ownerGeneration),
                new GameplayEntityId(subjectIndex, subjectGeneration),
                new GameplayEntityId(instigatorIndex, instigatorGeneration),
                actionPayload0,
                actionPayload1,
                actionPayload2);
        }

        public enum ReliableRestoreFailure
        {
            Capacity,
            DuplicateSequence,
            DuplicateOrigin,
            Order,
            Record,
            Target,
            Origin,
            NextSequence
        }

        public enum ReliableHashField
        {
            None,
            OriginalActivationFrame,
            OriginalDueFrame,
            Sequence,
            ActivationId,
            ActionIndex,
            ActionKindId,
            OriginBindingId,
            OriginRouteId,
            OriginInstanceIndex,
            OriginInstanceGeneration,
            OccurrenceKind,
            OccurrenceSignalId,
            PreviousScalarRaw,
            CurrentScalarRaw,
            OccurrencePayload0,
            OccurrencePayload1,
            OccurrencePayload2,
            OccurrenceOrderingValue,
            OccurrencePresenceEpoch,
            OccurrencePresenceStartFrame,
            TargetDomainId,
            TargetValue,
            TargetGeneration,
            OwnerIndex,
            OwnerGeneration,
            SubjectIndex,
            SubjectGeneration,
            InstigatorIndex,
            InstigatorGeneration,
            ActionPayload0,
            ActionPayload1,
            ActionPayload2
        }

        private sealed class ThrowingReliableSink : IGameplayTriggerActionSink
        {
            public ThrowingReliableSink(int actionKindId)
            {
                ActionKindId = new GameplayTriggerActionKindId(actionKindId);
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.ReliableDeferred;
            public int TryPrepareCount { get; private set; }
            public int AbortCount { get; private set; }
            public int CommitCount { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                TryPrepareCount++;
                throw new InvalidOperationException("Reliable legacy TryPrepare must not run.");
            }

            public void Abort(int token)
            {
                AbortCount++;
                throw new InvalidOperationException("Reliable legacy Abort must not run.");
            }

            public void Commit(int token, in GameplayTriggerActionIntent intent)
            {
                CommitCount++;
                throw new InvalidOperationException("Reliable legacy Commit must not run.");
            }
        }

        private sealed class RejectingSink : IGameplayTriggerActionSink
        {
            public RejectingSink(int actionKindId)
            {
                ActionKindId = new GameplayTriggerActionKindId(actionKindId);
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = 0;
                return false;
            }

            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class AcceptingSink : IGameplayTriggerActionSink
        {
            public AcceptingSink(int actionKindId)
            {
                ActionKindId = new GameplayTriggerActionKindId(actionKindId);
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                token = 1;
                return true;
            }
            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class CapturingSink : IGameplayTriggerActionSink
        {
            public CapturingSink(int actionKindId)
            {
                ActionKindId = new GameplayTriggerActionKindId(actionKindId);
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public GameplayTriggerDeliveryContract DeliveryContract =>
                GameplayTriggerDeliveryContract.SameFrameReserved;
            public GameplayTriggerRuntime Runtime { get; set; }
            public RuntimeSaveStateResult<RuntimeSaveState> CaptureDuringPrepare { get; private set; }

            public bool TryPrepare(
                in GameplayTriggerActionPreparation preparation,
                out int token)
            {
                CaptureDuringPrepare = Participant(Runtime).CaptureSaveState();
                token = 1;
                return true;
            }

            public void Abort(int token) { }
            public void Commit(int token, in GameplayTriggerActionIntent intent) { }
        }

        private sealed class DerivedCommandTransfer : IGameplayTriggerReliableTargetTransfer
        {
            private readonly RuntimeCommandSubmissionGate _gate;
            private readonly int _sourceId;
            private readonly int _commandId;
            private readonly int _envelopeTypeId;

            public DerivedCommandTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId,
                RuntimeCommandSubmissionGate gate,
                int sourceId,
                int commandId,
                int envelopeTypeId)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
                _gate = gate;
                _sourceId = sourceId;
                _commandId = commandId;
                _envelopeTypeId = envelopeTypeId;
            }

            public GameplayTriggerActionKindId ActionKindId { get; }
            public string TargetModuleId { get; }
            public bool IsEnabled => true;
            public RuntimeCommand LastAcceptedCommand { get; private set; }

            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                var command = new RuntimeCommand(
                    currentFrame,
                    _sourceId,
                    _commandId,
                    targetId: 42,
                    envelope: new RuntimeCommandEnvelope(
                        _envelopeTypeId,
                        1,
                        RuntimeCommandProvenanceKind.Derived,
                        data0: record.ActivationId,
                        data1: record.ActionIndex,
                        data2: record.Sequence));
                RuntimeCommandValidationResult submission = _gate.Submit(command);
                if (!submission.Success)
                {
                    return new GameplayTriggerReliableTransferResult(
                        GameplayTriggerReliableTransferStatus.TransientRejected,
                        (int)submission.Error.Code);
                }

                LastAcceptedCommand = submission.Command;
                return new GameplayTriggerReliableTransferResult(
                    GameplayTriggerReliableTransferStatus.Accepted);
            }
        }

        private sealed class DerivedCommandTargetModule : RuntimeModule
        {
            private readonly RuntimeCommandBuffer _buffer;
            private readonly List<RuntimeCommand> _drained = new List<RuntimeCommand>(2);
            private readonly int _sourceId;
            private readonly int _commandId;
            private readonly int _envelopeTypeId;

            public DerivedCommandTargetModule(
                string moduleId,
                RuntimeCommandBuffer buffer,
                int sourceId,
                int commandId,
                int envelopeTypeId)
                : base(moduleId, RuntimeTickStage.Simulation, 0)
            {
                _buffer = buffer;
                _sourceId = sourceId;
                _commandId = commandId;
                _envelopeTypeId = envelopeTypeId;
            }

            public int ReceivedCount { get; private set; }
            public bool LastCommandValid { get; private set; }

            public override void Tick(RuntimeTickContext context)
            {
                _drained.Clear();
                _buffer.DrainForFrame(new RuntimeFrame(context.FrameIndex), _drained);
                for (int i = 0; i < _drained.Count; i++)
                {
                    RuntimeCommand command = _drained[i];
                    ReceivedCount++;
                    LastCommandValid =
                        command.Frame == new RuntimeFrame(context.FrameIndex) &&
                        command.SourceId == _sourceId &&
                        command.CommandId == _commandId &&
                        command.TargetId == 42 &&
                        command.Envelope.TypeId == _envelopeTypeId &&
                        command.Envelope.SchemaVersion == 1 &&
                        command.Envelope.Provenance ==
                            RuntimeCommandProvenanceKind.Derived &&
                        command.Envelope.Data0 == 1L &&
                        command.Envelope.Data1 == 0L &&
                        command.Envelope.Data2 == 1L;
                }
            }
        }

        private readonly struct DerivedDeliveryRun
        {
            public DerivedDeliveryRun(
                RuntimeHost host,
                RuntimeCommandSourceRegistry sourceRegistry,
                DerivedCommandTransfer transfer,
                DerivedCommandTargetModule target)
            {
                Host = host;
                SourceRegistry = sourceRegistry;
                Transfer = transfer;
                Target = target;
            }

            public RuntimeHost Host { get; }
            public RuntimeCommandSourceRegistry SourceRegistry { get; }
            public DerivedCommandTransfer Transfer { get; }
            public DerivedCommandTargetModule Target { get; }
        }

        private sealed class FakeTransfer : IGameplayTriggerReliableTargetTransfer
        {
            public FakeTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId)
                : this(
                    actionKindId,
                    targetModuleId,
                    new GameplayTriggerReliableTransferResult(
                        GameplayTriggerReliableTransferStatus.Accepted))
            {
            }

            public FakeTransfer(
                GameplayTriggerActionKindId actionKindId,
                string targetModuleId,
                GameplayTriggerReliableTransferResult result)
            {
                ActionKindId = actionKindId;
                TargetModuleId = targetModuleId;
                Result = result;
            }

            public GameplayTriggerActionKindId ActionKindId { get; set; }
            public string TargetModuleId { get; set; }
            public bool IsEnabled => Enabled;
            public bool Enabled { get; set; } = true;
            public GameplayTriggerReliableTransferResult Result { get; set; }
            public int CallCount { get; private set; }
            public RuntimeFrame LastFrame { get; private set; }
            public bool ThrowOnTransfer { get; set; }
            public GameplayTriggerRuntime CaptureRuntime { get; set; }
            public RuntimeSaveStateResult<RuntimeSaveState> CaptureDuringCallback { get; private set; }

            public GameplayTriggerReliableTransferResult TryTransfer(
                RuntimeFrame currentFrame,
                in GameplayTriggerReliableOutboxRecord record)
            {
                CallCount++;
                LastFrame = currentFrame;
                if (CaptureRuntime != null)
                {
                    CaptureDuringCallback = new GameplayTriggerRuntimeSaveStateParticipant(
                        CaptureRuntime,
                        new Validator()).CaptureSaveState();
                }
                if (ThrowOnTransfer)
                    throw new InvalidOperationException("transfer failure");
                return Result;
            }
        }

        private sealed class TestModule : RuntimeModule
        {
            public TestModule(
                string moduleId,
                RuntimeTickStage stage,
                int priority)
                : base(moduleId, stage, priority)
            {
            }
        }

        private sealed class ThrowingTickModule : RuntimeModule
        {
            public ThrowingTickModule(string moduleId)
                : base(moduleId, RuntimeTickStage.Simulation, 0)
            {
            }

            public override void Tick(RuntimeTickContext context)
            {
                throw new InvalidOperationException("target tick failed");
            }
        }

        private sealed class Validator : IGameplayTriggerRestoreEntityValidator
        {
            public bool IsRestoredEntityValid(GameplayEntityId entityId) => entityId.IsValid;
        }
    }
}
