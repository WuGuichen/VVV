using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Persistence.RuntimeBridge
{
    public sealed class RuntimeSaveGameServiceTests
    {
        public enum PrepareMutation
        {
            CorruptHash,
            MalformedEnvelope,
            NonCanonicalHeader,
            TombstoneKind,
            WrongCodec,
            WrongProfile,
            WrongSlot,
            WrongGeneration,
            SchemaMirror,
            FrameMirror,
            FrameworkMirror,
            ConfigMirror,
            ResourceMirror,
            InvalidRuntimeUtf8
        }

        [Test]
        public void Constructor_RequiresAuthoritativeBarrierBoundToExactProfileIdentity()
        {
            RuntimeSaveStateRestoreProfile expected = CreateProfile();
            RuntimeSaveStateRestoreProfile sameDescriptorOtherIdentity = CreateProfile();
            RuntimeCheckpointBarrier authoritative = CreateBarrier(expected, true);
            RuntimeCheckpointBarrier legacy = CreateBarrier(CreateProfile(), false);

            Assert.DoesNotThrow(() => new RuntimeSaveGameService(new InMemorySaveGameStore(), authoritative,
                CreateBuild(), expected, new ExactCompatibility()));
            Assert.Throws<ArgumentException>(() => new RuntimeSaveGameService(new InMemorySaveGameStore(), authoritative,
                CreateBuild(), sameDescriptorOtherIdentity, new ExactCompatibility()));
            Assert.Throws<ArgumentException>(() => new RuntimeSaveGameService(new InMemorySaveGameStore(), legacy,
                CreateBuild(), legacy.RestoreProfile ?? CreateProfile(), new ExactCompatibility()));
        }

        [Test]
        public void CompositionBinding_RequiresExactHostCoordinatorAndProfileReferences()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(new RuntimeSaveStateRootHeaderProvider(profile.Descriptor).CreateParticipant());
            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);
            Assert.That(factory.Success, Is.True);
            var host = new RuntimeHost();
            var barrier = new RuntimeCheckpointBarrier(
                host,
                factory.Coordinator,
                new RuntimeCheckpointQuiescenceRegistry());
            var service = new RuntimeSaveGameService(
                new InMemorySaveGameStore(), barrier, CreateBuild(), profile, new ExactCompatibility());

            Assert.That(service.IsBoundTo(host, factory.Coordinator, profile), Is.True);
            Assert.That(service.IsBoundTo(new RuntimeHost(), factory.Coordinator, profile), Is.False);

            var otherRegistry = new RuntimeSaveStateRegistry();
            otherRegistry.Register(new RuntimeSaveStateRootHeaderProvider(profile.Descriptor).CreateParticipant());
            RuntimeSaveStateCoordinatorFactoryResult otherFactory =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(otherRegistry, profile);
            Assert.That(service.IsBoundTo(host, otherFactory.Coordinator, profile), Is.False);
            Assert.That(service.IsBoundTo(host, factory.Coordinator, CreateProfile()), Is.False);
        }

        [Test]
        public void EndToEnd_PlanCommitPrepareRestore_PreservesProvenanceAndFPlusOne()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameCheckpointRequest request = CreateCheckpointRequest("slot", SaveGameGeneration.None);

            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> planned = service.PlanCheckpointSave(request);
            Assert.That(planned.Success, Is.True);
            IRuntimeSaveGameOperation<SaveGameCommitResult> commit = service.Commit(planned.Value);
            Assert.That(commit.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(commit.Result.Generation, Is.EqualTo(new SaveGameGeneration(1)));
            Assert.That(planned.Value.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));

            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> load = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current));
            Assert.That(load.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            PreparedRuntimeSaveGame prepared = load.Result;
            Assert.That(prepared.CompletedFrame, Is.EqualTo(41));
            Assert.That(prepared.NextFrame, Is.EqualTo(42));
            Assert.That(prepared.CreatedAtUtc, Is.EqualTo(request.CreatedAtUtc));
            Assert.That(prepared.RestorePlan.Profile, Is.Not.SameAs(profile));
            Assert.That(prepared.RestorePlan.Profile.Descriptor, Is.EqualTo(profile.Descriptor));

            var target = new CandidateTarget(prepared.RestorePlan.Profile);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> restored = service.RestorePrepared(prepared, target);
            Assert.That(restored.Success, Is.True);
            Assert.That(restored.Value.TargetId, Is.EqualTo("candidate"));
            Assert.That(restored.Value.CompletedFrame, Is.EqualTo(41));
            Assert.That(restored.Value.NextFrame, Is.EqualTo(42));
            Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Consumed));
            Assert.That(target.CompletedFrame, Is.EqualTo(41));
            Assert.That(target.NextFrame, Is.EqualTo(42));
            Assert.That(target.PublishCount, Is.Zero);
            Assert.That(target.TickCount, Is.Zero);
        }

        [Test]
        public void PlanCheckpointSave_ReservesPerSlotUntilPlanDisposal()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameCheckpointRequest request = CreateCheckpointRequest("same", SaveGameGeneration.None);

            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> first = service.PlanCheckpointSave(request);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> busy = service.PlanCheckpointSave(request);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> other = service.PlanCheckpointSave(
                CreateCheckpointRequest("other", SaveGameGeneration.None));

            Assert.That(first.Success, Is.True);
            Assert.That(busy.Success, Is.False);
            Assert.That(busy.ErrorDetail.PersistenceError.Code, Is.EqualTo(SaveGameErrorCode.Busy));
            Assert.That(other.Success, Is.True);
            first.Value.Dispose();
            other.Value.Dispose();
            Assert.That(service.PlanCheckpointSave(request).Success, Is.True);
        }

        [Test]
        public void ActiveProviderOperation_PollAndPublicReadsAllocateZeroBytesAfterInitialization()
        {
            var store = new SteadyRunningStore();
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(store, profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            IRuntimeSaveGameOperation<SaveGameCommitResult> operation = service.Commit(plan);

            operation.Poll();
            _ = operation.OperationId;
            _ = operation.Status;
            _ = operation.Progress;
            _ = operation.IsDone;
            _ = operation.CanCancel;
            _ = operation.ErrorDetail;
            _ = operation.HasResult;
            _ = operation.Result;
            operation.TryGetResult(out _);
            operation.RequestCancel();
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 512; i++)
            {
                operation.Poll();
                _ = operation.OperationId;
                _ = operation.Status;
                _ = operation.Progress;
                _ = operation.IsDone;
                _ = operation.CanCancel;
                _ = operation.ErrorDetail;
                _ = operation.HasResult;
                _ = operation.Result;
                operation.TryGetResult(out _);
                operation.RequestCancel();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
            store.Operation.Complete();
            operation.Poll();
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
        }

        [Test]
        public void InvalidCheckpointMetadata_FailsBeforeCaptureOrStoreAccess()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new CountingNullStore();
            var checker = new CountingQuiescenceChecker();
            var service = new RuntimeSaveGameService(store, CreateBarrier(profile, true, 41, checker),
                CreateBuild(), profile, new ExactCompatibility());

            Assert.Throws<ArgumentException>(() => service.PlanCheckpointSave(
                new RuntimeSaveGameCheckpointRequest(
                    new SaveGameProfileId("profile"), new SaveGameSlotId("slot"), SaveGameGeneration.None,
                    new DateTime(638000000000000000L, DateTimeKind.Utc),
                    new DateTime(638000000010000000L, DateTimeKind.Utc),
                    new[] { new SaveGameKeyValue("duplicate", "a"), new SaveGameKeyValue("duplicate", "b") },
                    null)));

            Assert.That(checker.CheckCount, Is.Zero);
            Assert.That(store.CommitCount, Is.Zero);
            Assert.That(store.ReadCount, Is.Zero);
        }

        [Test]
        public void CheckpointRejection_MapsToStablePersistenceError()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new CountingNullStore();
            var service = new RuntimeSaveGameService(store,
                CreateBarrier(profile, true, 41, new PendingQuiescenceChecker()),
                CreateBuild(), profile, new ExactCompatibility());

            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> result = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None));

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorDetail.PersistenceError.Code,
                Is.EqualTo(SaveGameErrorCode.RuntimeCheckpointRejected));
            Assert.That(store.CommitCount, Is.Zero);
            Assert.That(store.ReadCount, Is.Zero);
        }

        [Test]
        public void Commit_NullProviderOperationFailsClosedAndReleasesReservation()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new NullOperationStore(), profile);
            RuntimeSaveGameCheckpointRequest request = CreateCheckpointRequest("slot", SaveGameGeneration.None);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(request).Value;

            IRuntimeSaveGameOperation<SaveGameCommitResult> operation = service.Commit(plan);

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(operation.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation));
            Assert.That(operation.OperationId.IsValid, Is.True);
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            Assert.That(service.PlanCheckpointSave(request).Success, Is.True);
        }

        [Test]
        public void PrepareLoad_RejectsProviderSuccessWithContradictoryResultShape()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new ContradictoryReadStore(), profile);

            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> operation = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current));

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(operation.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation));
            Assert.That(operation.OperationId.IsValid, Is.True);
        }

        [Test]
        public void PreCancelledCallsDoNoStoreWorkAndCommitReleasesPlan()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new CountingNullStore();
            var service = CreateService(store, profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            var cancelled = new CancellationToken(true);

            IRuntimeSaveGameOperation<SaveGameCommitResult> commit = service.Commit(plan, cancelled);
            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> load = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current), cancelled);

            Assert.That(commit.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(load.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(commit.OperationId.IsValid, Is.True);
            Assert.That(load.OperationId.IsValid, Is.True);
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            Assert.That(store.CommitCount, Is.Zero);
            Assert.That(store.ReadCount, Is.Zero);
        }

        [Test]
        public void ProviderProgressRollbackAndCancelThrowFailClosedAndReleaseExactlyOnce()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new HostileCommitStore();
            var service = CreateService(store, profile);
            RuntimeSaveGameCheckpointRequest request = CreateCheckpointRequest("slot", SaveGameGeneration.None);

            RuntimeSaveGameWritePlan rollbackPlan = service.PlanCheckpointSave(request).Value;
            IRuntimeSaveGameOperation<SaveGameCommitResult> rollback = service.Commit(rollbackPlan);
            Assert.That(rollback.Status, Is.EqualTo(SaveGameOperationStatus.Running));
            rollback.Poll();
            Assert.That(rollback.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(rollback.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation));
            Assert.That(rollbackPlan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));

            store.ThrowOnCancel = true;
            RuntimeSaveGameWritePlan cancelPlan = service.PlanCheckpointSave(request).Value;
            IRuntimeSaveGameOperation<SaveGameCommitResult> cancel = service.Commit(cancelPlan);
            cancel.RequestCancel();
            Assert.That(cancel.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(cancelPlan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            cancel.RequestCancel();
            Assert.That(service.PlanCheckpointSave(request).Success, Is.True);
        }

        [Test]
        public void ProviderIdStatusAndTryResultContradictionsFailClosed()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new ContradictoryStateStore();
            var service = CreateService(store, profile);
            foreach (ContradictoryStateMode mode in new[]
                     { ContradictoryStateMode.ChangingId, ContradictoryStateMode.RunningToPending,
                       ContradictoryStateMode.HasResultMismatch })
            {
                store.Mode = mode;
                RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                    CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
                IRuntimeSaveGameOperation<SaveGameCommitResult> operation = service.Commit(plan);
                if (!operation.IsDone) operation.Poll();
                Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Failed), mode.ToString());
                Assert.That(operation.ErrorDetail.Code,
                    Is.EqualTo(RuntimeSaveGameBridgeErrorCode.InvalidProviderOperation), mode.ToString());
                Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released), mode.ToString());
            }
        }

        [Test]
        public void ProviderAsyncSuccessRetainsReservationUntilTerminalAndPreservesWarning()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new AsyncSuccessStore(), profile);
            RuntimeSaveGameCheckpointRequest request = CreateCheckpointRequest("slot", SaveGameGeneration.None);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(request).Value;

            IRuntimeSaveGameOperation<SaveGameCommitResult> operation = service.Commit(plan);
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Pending));
            Assert.That(operation.CanCancel, Is.True);
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Transferred));
            Assert.That(service.PlanCheckpointSave(request).ErrorDetail.PersistenceError.Code,
                Is.EqualTo(SaveGameErrorCode.Busy));
            operation.Poll();
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Running));
            Assert.That(operation.CanCancel, Is.True);
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Transferred));
            operation.Poll();
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Running));
            Assert.That(operation.CanCancel, Is.False);
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Transferred));
            operation.Poll();

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(operation.Result.Generation, Is.EqualTo(new SaveGameGeneration(1)));
            Assert.That(operation.Result.Warning, Is.EqualTo(SaveGameWarning.RetentionCleanupFailed));
            Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> replanned = service.PlanCheckpointSave(request);
            Assert.That(replanned.Success, Is.True);
            replanned.Value.Dispose();
        }

        [Test]
        public void ProviderLegalFailureTimeoutAndCancellationTerminalsArePreserved()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new TerminalCommitStore();
            var service = CreateService(store, profile);
            foreach (SaveGameOperationStatus terminal in new[]
                     { SaveGameOperationStatus.Failed, SaveGameOperationStatus.TimedOut,
                       SaveGameOperationStatus.Cancelled })
            {
                store.Terminal = terminal;
                RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                    CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
                IRuntimeSaveGameOperation<SaveGameCommitResult> operation = service.Commit(plan);

                Assert.That(operation.Status, Is.EqualTo(terminal));
                Assert.That(operation.IsDone, Is.True);
                Assert.That(operation.CanCancel, Is.False);
                Assert.That(operation.HasResult, Is.False);
                Assert.That(plan.State, Is.EqualTo(RuntimeSaveGameWritePlanState.Released));
                if (terminal == SaveGameOperationStatus.Failed)
                {
                    Assert.That(operation.ErrorDetail.PersistenceError.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
                    Assert.That(operation.ErrorDetail.Phase, Is.EqualTo(RuntimeSaveGameBridgePhase.StoreCommit));
                }
                else if (terminal == SaveGameOperationStatus.TimedOut)
                {
                    Assert.That(operation.ErrorDetail.PersistenceError.Code, Is.EqualTo(SaveGameErrorCode.Timeout));
                    Assert.That(operation.ErrorDetail.Phase, Is.EqualTo(RuntimeSaveGameBridgePhase.StoreCommit));
                }
                else
                {
                    Assert.That(operation.ErrorDetail, Is.Null);
                }
            }
        }

        [Test]
        public void PrepareLoad_EnforcesCurrentRecoveryProvenanceButAllowsOlderExactPrimary()
        {
            var sourceProfile = CreateProfile();
            var sourceStore = new InMemorySaveGameStore();
            var source = CreateService(sourceStore, sourceProfile);
            RuntimeSaveGameWritePlan plan = source.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            Assert.That(source.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            SaveGameReadResult valid = sourceStore.Read(new SaveGameReadRequest(
                new SaveGameProfileId("profile"), new SaveGameSlotId("slot"), SaveGameReadSelection.Current)).Result;

            var mismatchStore = new FixedReadStore(new SaveGameReadResult(valid.Envelope,
                new SaveGameGeneration(2), new SaveGameGeneration(1), SaveGameRecoverySource.Primary));
            var mismatch = CreateService(mismatchStore, CreateProfile());
            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> rejected = mismatch.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current));
            Assert.That(rejected.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(rejected.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.InvalidProvenance));

            var exactStore = new FixedReadStore(new SaveGameReadResult(valid.Envelope,
                new SaveGameGeneration(2), new SaveGameGeneration(1), SaveGameRecoverySource.Primary));
            var exact = CreateService(exactStore, CreateProfile());
            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> accepted = exact.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Exact(new SaveGameGeneration(1))));
            Assert.That(accepted.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(accepted.Result.OriginalGeneration, Is.EqualTo(new SaveGameGeneration(2)));
        }

        [Test]
        public void PrepareLoad_RejectsCompletedFrameMaxBeforeCreatingPreparedLease()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var store = new InMemorySaveGameStore();
            var barrier = CreateBarrier(profile, true, long.MaxValue);
            var service = new RuntimeSaveGameService(store, barrier, CreateBuild(), profile, new ExactCompatibility());
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));

            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> load = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current));

            Assert.That(load.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(load.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.FrameOverflow));
            Assert.That(load.ErrorDetail.PersistenceError.Code,
                Is.EqualTo(SaveGameErrorCode.CandidateRestoreFailed));
            Assert.That(load.HasResult, Is.False);
        }

        [TestCase(PrepareMutation.CorruptHash)]
        [TestCase(PrepareMutation.MalformedEnvelope)]
        [TestCase(PrepareMutation.NonCanonicalHeader)]
        [TestCase(PrepareMutation.TombstoneKind)]
        [TestCase(PrepareMutation.WrongCodec)]
        [TestCase(PrepareMutation.WrongProfile)]
        [TestCase(PrepareMutation.WrongSlot)]
        [TestCase(PrepareMutation.WrongGeneration)]
        [TestCase(PrepareMutation.SchemaMirror)]
        [TestCase(PrepareMutation.FrameMirror)]
        [TestCase(PrepareMutation.FrameworkMirror)]
        [TestCase(PrepareMutation.ConfigMirror)]
        [TestCase(PrepareMutation.ResourceMirror)]
        [TestCase(PrepareMutation.InvalidRuntimeUtf8)]
        public void PrepareLoad_RejectsEnvelopeAndRuntimeProvenanceMatrix(PrepareMutation mutation)
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            RuntimeSaveState state = CreatePayloadState(0, 41, "framework", "config", "resources");
            SaveGameProfileId envelopeProfile = new SaveGameProfileId(
                mutation == PrepareMutation.WrongProfile ? "other-profile" : "profile");
            SaveGameSlotId envelopeSlot = new SaveGameSlotId(
                mutation == PrepareMutation.WrongSlot ? "other-slot" : "slot");
            SaveGameGeneration envelopeGeneration = new SaveGameGeneration(
                mutation == PrepareMutation.WrongGeneration ? 2UL : 1UL);
            SaveGameBuildDescriptor envelopeBuild = mutation == PrepareMutation.FrameworkMirror
                ? new SaveGameBuildDescriptor("other-framework", "build", "config", "resources", string.Empty)
                : mutation == PrepareMutation.ConfigMirror
                    ? new SaveGameBuildDescriptor("framework", "build", "other-config", "resources", string.Empty)
                    : mutation == PrepareMutation.ResourceMirror
                        ? new SaveGameBuildDescriptor("framework", "build", "config", "other-resources", string.Empty)
                        : CreateBuild();
            SaveGameEncodedEnvelope encoded;
            if (mutation == PrepareMutation.TombstoneKind)
            {
                encoded = SaveGameEnvelopeCodec.Encode(SaveGameEnvelope.CreateTombstone(
                    envelopeProfile, envelopeSlot, envelopeGeneration,
                    new DateTime(638000000000000000L, DateTimeKind.Utc), envelopeBuild)).Encoded;
            }
            else if (mutation == PrepareMutation.InvalidRuntimeUtf8)
            {
                SaveGameEnvelope invalidRuntime = SaveGameEnvelope.CreateSave(
                    envelopeProfile, envelopeSlot, envelopeGeneration,
                    new DateTime(638000000000000000L, DateTimeKind.Utc),
                    new DateTime(638000000010000000L, DateTimeKind.Utc), envelopeBuild,
                    Array.Empty<SaveGameKeyValue>(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId,
                    0, 41, Array.Empty<SaveGameKeyValue>(), new byte[] { 0xff });
                encoded = SaveGameEnvelopeCodec.Encode(invalidRuntime).Encoded;
            }
            else
            {
                encoded = EncodeState(state, envelopeProfile, envelopeSlot, envelopeGeneration, envelopeBuild,
                    mutation == PrepareMutation.WrongCodec ? "other-codec" : SaveGameEnvelope.RuntimeSaveStateJsonCodecId,
                    mutation == PrepareMutation.SchemaMirror ? 1 : 0,
                    mutation == PrepareMutation.FrameMirror ? 42 : 41);
                if (mutation == PrepareMutation.CorruptHash)
                {
                    byte[] bytes = encoded.ToArray();
                    bytes[bytes.Length - 1] ^= 0x01;
                    encoded = new SaveGameEncodedEnvelope(bytes);
                }
                else if (mutation == PrepareMutation.MalformedEnvelope)
                {
                    byte[] bytes = encoded.ToArray();
                    var truncated = new byte[bytes.Length - 1];
                    Buffer.BlockCopy(bytes, 0, truncated, 0, truncated.Length);
                    encoded = new SaveGameEncodedEnvelope(truncated);
                }
                else if (mutation == PrepareMutation.NonCanonicalHeader)
                {
                    encoded = InsertNonCanonicalHeaderWhitespace(encoded);
                }
            }
            var read = new SaveGameReadResult(encoded, new SaveGameGeneration(1), new SaveGameGeneration(1),
                SaveGameRecoverySource.Primary);
            var service = CreateService(new FixedReadStore(read), profile,
                mutation == PrepareMutation.FrameworkMirror || mutation == PrepareMutation.ConfigMirror ||
                mutation == PrepareMutation.ResourceMirror
                    ? (IRuntimeSaveGameCompatibilityChecker)new AlwaysCompatible()
                    : new ExactCompatibility());

            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> result = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current));

            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(result.HasResult, Is.False);
            Assert.That(result.ErrorDetail.Code, Is.EqualTo(ExpectedMutationCode(mutation)));
            Assert.That(result.ErrorDetail.Phase, Is.EqualTo(ExpectedMutationPhase(mutation)));
            if (mutation == PrepareMutation.SchemaMirror || mutation == PrepareMutation.InvalidRuntimeUtf8)
                Assert.That(result.ErrorDetail.PersistenceError.Code, Is.EqualTo(
                    mutation == PrepareMutation.SchemaMirror
                        ? SaveGameErrorCode.RuntimeCompatibilityFailed
                        : SaveGameErrorCode.RuntimeSerializationFailed));
        }

        [Test]
        public void PrepareLoad_CompatibilityDefaultThrowAndIncompatibleFailClosed()
        {
            foreach (CompatibilityMode mode in new[]
                     { CompatibilityMode.Default, CompatibilityMode.Throw, CompatibilityMode.Incompatible })
            {
                RuntimeSaveStateRestoreProfile profile = CreateProfile();
                SaveGameEncodedEnvelope encoded = EncodeState(CreatePayloadState(0, 41, "framework", "config", "resources"),
                    new SaveGameProfileId("profile"), new SaveGameSlotId("slot"), new SaveGameGeneration(1),
                    CreateBuild(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 0, 41);
                var service = CreateService(new FixedReadStore(new SaveGameReadResult(encoded,
                    new SaveGameGeneration(1), new SaveGameGeneration(1), SaveGameRecoverySource.Primary)),
                    profile, new CompatibilityFixture(mode));

                IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> result = service.PrepareLoad(
                    new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                        SaveGameReadSelection.Current));

                Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Failed), mode.ToString());
                Assert.That(result.ErrorDetail.Code, Is.EqualTo(RuntimeSaveGameBridgeErrorCode.CompatibilityFailure),
                    mode.ToString());
                Assert.That(result.ErrorDetail.PersistenceError.Code,
                    Is.EqualTo(SaveGameErrorCode.RuntimeCompatibilityFailed), mode.ToString());
            }
        }

        [Test]
        public void PrepareLoad_MigrationAndLastKnownGoodAllowDenyMatrix()
        {
            var pipeline = new RuntimeSaveStateMigrationPipeline();
            pipeline.Register(new ZeroToOneMigration());
            RuntimeSaveStateRestoreProfile migrationProfile = RuntimeSaveStateRestoreProfile.Create(
                new RuntimeSaveStateRootHeaderDescriptor(1, "framework", "config", "resources"), pipeline);
            SaveGameEncodedEnvelope encoded = EncodeState(CreatePayloadState(0, 41, "framework", "config", "resources"),
                new SaveGameProfileId("profile"), new SaveGameSlotId("slot"), new SaveGameGeneration(1),
                CreateBuild(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 0, 41);
            var migrationRead = new SaveGameReadResult(encoded, new SaveGameGeneration(1), new SaveGameGeneration(1),
                SaveGameRecoverySource.Primary);
            var migrated = CreateService(new FixedReadStore(migrationRead), migrationProfile,
                new CompatibilityFixture(CompatibilityMode.MigrationRequired)).PrepareLoad(
                    new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                        SaveGameReadSelection.Current));
            Assert.That(migrated.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(migrated.Result.RestorePlan.State.SchemaVersion, Is.EqualTo(1));

            var lkgRead = new SaveGameReadResult(encoded, new SaveGameGeneration(2), new SaveGameGeneration(1),
                SaveGameRecoverySource.LastKnownGood);
            var denied = CreateService(new FixedReadStore(lkgRead), CreateProfile()).PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current, SaveGameRecoveryPolicy.PrimaryOnly));
            Assert.That(denied.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            var allowed = CreateService(new FixedReadStore(lkgRead), CreateProfile()).PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current, SaveGameRecoveryPolicy.PrimaryThenLastKnownGood));
            Assert.That(allowed.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(allowed.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.LastKnownGood));
        }

        [Test]
        public void RestorePrepared_ContradictoryAcquirePoisonsReturnedLeaseAndDoesNotClaimPrepared()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            PreparedRuntimeSaveGame prepared = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current)).Result;
            var target = new ContradictoryAcquireTarget(prepared.RestorePlan.Profile);

            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> result = service.RestorePrepared(prepared, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorDetail.Phase, Is.EqualTo(RuntimeSaveGameBridgePhase.CandidateAcquisition));
            Assert.That(result.ErrorDetail.TargetId, Is.EqualTo("contradictory"));
            Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Ready));
            Assert.That(target.LeaseDisposed, Is.True);
        }

        [Test]
        public void RestorePrepared_ReportsThrowingLeaseDisposeAsTargetContractViolation()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            PreparedRuntimeSaveGame prepared = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current)).Result;
            var target = new ThrowingDisposeTarget(prepared.RestorePlan.Profile);

            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> result = service.RestorePrepared(prepared, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorDetail.TargetValidationError.Code,
                Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.ContractViolation));
            Assert.That(result.ErrorDetail.TargetId, Is.EqualTo("throwing"));
            Assert.That(target.DisposeAttempted, Is.True);
            Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Ready));
        }

        [Test]
        public void RestorePrepared_ParticipantAndCompletionFailuresPoisonCandidateWithoutPublishOrTick()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            foreach (CandidateFailureMode mode in new[]
                     { CandidateFailureMode.Participant, CandidateFailureMode.CompleteFalse,
                       CandidateFailureMode.CompleteThrow })
            {
                string slot = "slot-" + mode;
                RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                    CreateCheckpointRequest(slot, SaveGameGeneration.None)).Value;
                Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
                PreparedRuntimeSaveGame prepared = service.PrepareLoad(
                    new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId(slot),
                        SaveGameReadSelection.Current)).Result;
                var target = new FailingCandidateTarget(prepared.RestorePlan.Profile, mode);

                RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> result = service.RestorePrepared(prepared, target);

                Assert.That(result.Success, Is.False, mode.ToString());
                Assert.That(result.ErrorDetail.TargetId, Is.EqualTo("failing-candidate"), mode.ToString());
                Assert.That(result.ErrorDetail.PersistenceError.Code,
                    Is.EqualTo(SaveGameErrorCode.CandidateRestoreFailed), mode.ToString());
                Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Released), mode.ToString());
                Assert.That(target.Poisoned, Is.True, mode.ToString());
                Assert.That(target.PublishCount, Is.Zero, mode.ToString());
                Assert.That(target.TickCount, Is.Zero, mode.ToString());
            }
        }

        [Test]
        public void RestorePrepared_RepeatedAndConcurrentLosersAcquireBeforePreparedClaim()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("slot", SaveGameGeneration.None)).Value;
            Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            PreparedRuntimeSaveGame prepared = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(new SaveGameProfileId("profile"), new SaveGameSlotId("slot"),
                    SaveGameReadSelection.Current)).Result;
            using var acquired = new Barrier(2);
            var first = new CandidateTarget(prepared.RestorePlan.Profile, acquired);
            var second = new CandidateTarget(prepared.RestorePlan.Profile, acquired);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> firstResult = default;
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> secondResult = default;
            using (var start = new Barrier(3))
            {
                Task a = Task.Run(() => { start.SignalAndWait(); firstResult = service.RestorePrepared(prepared, first); });
                Task b = Task.Run(() => { start.SignalAndWait(); secondResult = service.RestorePrepared(prepared, second); });
                start.SignalAndWait();
                Task.WaitAll(a, b);
            }

            Assert.That((firstResult.Success ? 1 : 0) + (secondResult.Success ? 1 : 0), Is.EqualTo(1));
            Assert.That(first.AcquireCount + second.AcquireCount, Is.EqualTo(2));
            var loser = firstResult.Success ? second : first;
            Assert.That(loser.AcquireCount, Is.EqualTo(1));
            var repeat = new CandidateTarget(prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, repeat).Success, Is.False);
            Assert.That(repeat.AcquireCount, Is.Zero);
        }

        [Test]
        public void RestorePrepared_SamePreparedAndCandidate_AcquireLoserCannotReleaseWinnerClaim()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            var service = CreateService(new InMemorySaveGameStore(), profile);
            RuntimeSaveGameWritePlan plan = service.PlanCheckpointSave(
                CreateCheckpointRequest("same-candidate-race", SaveGameGeneration.None)).Value;
            Assert.That(service.Commit(plan).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            PreparedRuntimeSaveGame prepared = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(
                    new SaveGameProfileId("profile"),
                    new SaveGameSlotId("same-candidate-race"),
                    SaveGameReadSelection.Current)).Result;
            using var target = new SingleAcquireBlockingCandidateTarget(prepared.RestorePlan.Profile);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> winnerResult = default;
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> loserResult = default;
            Task loser = Task.Factory.StartNew(
                () => loserResult = service.RestorePrepared(prepared, target),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Task winner = null;
            try
            {
                Assert.That(target.FirstAcquireEntered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "Acquisition loser did not reach the deterministic pre-acquire gate.");
                winner = Task.Factory.StartNew(
                    () => winnerResult = service.RestorePrepared(prepared, target),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
                Assert.That(target.CompletionEntered.Wait(TimeSpan.FromSeconds(5)), Is.True,
                    "Winner did not reach completion after claiming the prepared restore.");
                target.AllowFirstAcquire.Set();
                Assert.That(loser.Wait(TimeSpan.FromSeconds(5)), Is.True);

                Assert.That(loserResult.Success, Is.False);
                Assert.That(loserResult.ErrorDetail.Phase,
                    Is.EqualTo(RuntimeSaveGameBridgePhase.CandidateAcquisition));
                Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.RestoreClaimed),
                    "An acquisition loser must not release another call's prepared claim.");
            }
            finally
            {
                target.AllowFirstAcquire.Set();
                target.AllowCompletion.Set();
            }

            Assert.That(winner, Is.Not.Null);
            Assert.That(winner.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(winnerResult.Success, Is.True);
            Assert.That(winnerResult.Value.TargetId, Is.EqualTo("single-candidate"));
            Assert.That(prepared.State, Is.EqualTo(PreparedRuntimeSaveGameState.Consumed));
            Assert.That(target.AcquireCount, Is.EqualTo(2));
            Assert.That(target.CompleteCount, Is.EqualTo(1));
        }

        private static RuntimeSaveGameService CreateService(ISaveGameStore store, RuntimeSaveStateRestoreProfile profile)
        {
            return CreateService(store, profile, new ExactCompatibility());
        }

        private static RuntimeSaveGameService CreateService(ISaveGameStore store,
            RuntimeSaveStateRestoreProfile profile, IRuntimeSaveGameCompatibilityChecker checker)
        {
            RuntimeCheckpointBarrier barrier = CreateBarrier(profile, true);
            return new RuntimeSaveGameService(store, barrier, CreateBuild(), profile, checker);
        }

        private static RuntimeCheckpointBarrier CreateBarrier(RuntimeSaveStateRestoreProfile profile, bool authoritative,
            long frame = 41, IRuntimeCheckpointQuiescenceChecker checker = null)
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(new RuntimeSaveStateRootHeaderProvider(profile.Descriptor).CreateParticipant());
            RuntimeSaveStateCoordinator coordinator;
            if (authoritative)
            {
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);
                Assert.That(factory.Success, Is.True);
                coordinator = factory.Coordinator;
            }
            else
            {
                coordinator = new RuntimeSaveStateCoordinator(registry);
            }
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            host.Tick(frame, 0.016d);
            var quiescence = new RuntimeCheckpointQuiescenceRegistry();
            if (checker != null)
                Assert.That(quiescence.Register("test.counter", 0, checker).Success, Is.True);
            return new RuntimeCheckpointBarrier(host, coordinator, quiescence);
        }

        private static RuntimeSaveStateRestoreProfile CreateProfile() => RuntimeSaveStateRestoreProfile.Create(
            new RuntimeSaveStateRootHeaderDescriptor(0, "framework", "config", "resources"),
            new RuntimeSaveStateMigrationPipeline());

        private static SaveGameBuildDescriptor CreateBuild() =>
            new SaveGameBuildDescriptor("framework", "build", "config", "resources", string.Empty);

        private static RuntimeSaveState CreatePayloadState(int schema, long frame, string framework,
            string config, string resources) => new RuntimeSaveState(schema,
                new DateTime(638000000000000000L, DateTimeKind.Utc), framework, config, resources, frame,
                null, null, null);

        private static SaveGameEncodedEnvelope EncodeState(RuntimeSaveState state, SaveGameProfileId profile,
            SaveGameSlotId slot, SaveGameGeneration generation, SaveGameBuildDescriptor build, string codec,
            int envelopeSchema, long envelopeFrame)
        {
            RuntimeSaveStateResult<byte[]> payload = RuntimeSaveStateJson.SaveToUtf8(
                state, SaveGameLimits.MaximumPayloadBytes, 16);
            Assert.That(payload.Success, Is.True);
            SaveGameEnvelope envelope = SaveGameEnvelope.CreateSave(profile, slot, generation,
                new DateTime(638000000000000000L, DateTimeKind.Utc),
                new DateTime(638000000010000000L, DateTimeKind.Utc), build,
                Array.Empty<SaveGameKeyValue>(), codec, envelopeSchema, envelopeFrame,
                Array.Empty<SaveGameKeyValue>(), payload.Value);
            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(envelope);
            Assert.That(encoded.IsSuccess, Is.True);
            return encoded.Encoded;
        }

        private static SaveGameEncodedEnvelope InsertNonCanonicalHeaderWhitespace(SaveGameEncodedEnvelope encoded)
        {
            const int prefixLength = 24;
            const int trailerLength = 32;
            byte[] source = encoded.ToArray();
            int headerLength = source[12] | source[13] << 8 | source[14] << 16 | source[15] << 24;
            int sourceTrailer = source.Length - trailerLength;
            var mutated = new byte[source.Length + 1];
            Buffer.BlockCopy(source, 0, mutated, 0, prefixLength + 1);
            mutated[prefixLength + 1] = 0x20;
            Buffer.BlockCopy(source, prefixLength + 1, mutated, prefixLength + 2,
                sourceTrailer - prefixLength - 1);
            int newHeaderLength = headerLength + 1;
            mutated[12] = (byte)newHeaderLength;
            mutated[13] = (byte)(newHeaderLength >> 8);
            mutated[14] = (byte)(newHeaderLength >> 16);
            mutated[15] = (byte)(newHeaderLength >> 24);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(mutated, 0, mutated.Length - trailerLength);
                Buffer.BlockCopy(hash, 0, mutated, mutated.Length - trailerLength, trailerLength);
            }
            return new SaveGameEncodedEnvelope(mutated);
        }

        private static RuntimeSaveGameBridgeErrorCode ExpectedMutationCode(PrepareMutation mutation)
        {
            if (mutation == PrepareMutation.CorruptHash || mutation == PrepareMutation.MalformedEnvelope ||
                mutation == PrepareMutation.NonCanonicalHeader) return RuntimeSaveGameBridgeErrorCode.PersistenceFailure;
            if (mutation == PrepareMutation.SchemaMirror || mutation == PrepareMutation.InvalidRuntimeUtf8)
                return RuntimeSaveGameBridgeErrorCode.InvalidRuntimePayload;
            return RuntimeSaveGameBridgeErrorCode.InvalidProvenance;
        }

        private static RuntimeSaveGameBridgePhase ExpectedMutationPhase(PrepareMutation mutation)
        {
            return mutation == PrepareMutation.SchemaMirror || mutation == PrepareMutation.InvalidRuntimeUtf8
                ? RuntimeSaveGameBridgePhase.RuntimePreparation
                : RuntimeSaveGameBridgePhase.EnvelopeValidation;
        }

        private static RuntimeSaveGameCheckpointRequest CreateCheckpointRequest(string slot, SaveGameGeneration expected) =>
            new RuntimeSaveGameCheckpointRequest(new SaveGameProfileId("profile"), new SaveGameSlotId(slot), expected,
                new DateTime(638000000000000000L, DateTimeKind.Utc),
                new DateTime(638000000010000000L, DateTimeKind.Utc), null, null);

        private sealed class ExactCompatibility : IRuntimeSaveGameCompatibilityChecker
        {
            public RuntimeSaveGameCompatibilityResult Check(SaveGameBuildDescriptor build,
                RuntimeSaveStateRestoreProfile restoreProfile)
            {
                RuntimeSaveStateRootHeaderDescriptor descriptor = restoreProfile.Descriptor;
                return string.Equals(build.FrameworkVersion, descriptor.FrameworkVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ConfigVersion, descriptor.ConfigVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ResourceCatalogVersion, descriptor.ResourceCatalogVersion, StringComparison.Ordinal)
                    ? RuntimeSaveGameCompatibilityResult.Compatible
                    : RuntimeSaveGameCompatibilityResult.Incompatible(RuntimeSaveGameCompatibilityErrorCode.BuildMismatch);
            }
        }

        private sealed class AlwaysCompatible : IRuntimeSaveGameCompatibilityChecker
        {
            public RuntimeSaveGameCompatibilityResult Check(SaveGameBuildDescriptor build,
                RuntimeSaveStateRestoreProfile restoreProfile) => RuntimeSaveGameCompatibilityResult.Compatible;
        }

        private enum CompatibilityMode { Default, Throw, Incompatible, MigrationRequired }

        private sealed class CompatibilityFixture : IRuntimeSaveGameCompatibilityChecker
        {
            private readonly CompatibilityMode _mode;
            public CompatibilityFixture(CompatibilityMode mode) { _mode = mode; }
            public RuntimeSaveGameCompatibilityResult Check(SaveGameBuildDescriptor build,
                RuntimeSaveStateRestoreProfile restoreProfile)
            {
                if (_mode == CompatibilityMode.Throw) throw new InvalidOperationException("compatibility failed");
                if (_mode == CompatibilityMode.Incompatible)
                    return RuntimeSaveGameCompatibilityResult.Incompatible(
                        RuntimeSaveGameCompatibilityErrorCode.BuildMismatch);
                if (_mode == CompatibilityMode.MigrationRequired)
                    return RuntimeSaveGameCompatibilityResult.MigrationRequired;
                return default;
            }
        }

        private sealed class ZeroToOneMigration : IRuntimeSaveStateMigration
        {
            public int FromSchemaVersion => 0;
            public int ToSchemaVersion => 1;
            public RuntimeSaveStateResult<RuntimeSaveState> Migrate(RuntimeSaveState saveState) =>
                RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(new RuntimeSaveState(1,
                    saveState.CreatedAtUtc, saveState.FrameworkVersion, saveState.ConfigVersion,
                    saveState.ResourceCatalogVersion, saveState.Frame, saveState.Entities,
                    saveState.GlobalCounters, saveState.ModuleStates, saveState.Metadata));
        }

        private class NullOperationStore : ISaveGameStore
        {
            public virtual ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request, CancellationToken cancellationToken = default) => null;
            public virtual ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request, CancellationToken cancellationToken = default) => null;
            public virtual ISaveGameOperation<SaveGameListResult> List(SaveGameListRequest request, CancellationToken cancellationToken = default) => null;
            public virtual ISaveGameOperation<SaveGameDeleteResult> Delete(SaveGameDeleteRequest request, CancellationToken cancellationToken = default) => null;
        }

        private sealed class ContradictoryReadStore : NullOperationStore
        {
            public override ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request, CancellationToken cancellationToken = default) =>
                new ContradictoryReadOperation();
        }

        private sealed class CountingNullStore : NullOperationStore
        {
            public int CommitCount { get; private set; }
            public int ReadCount { get; private set; }
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) { CommitCount++; return null; }
            public override ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request,
                CancellationToken cancellationToken = default) { ReadCount++; return null; }
        }

        private sealed class CountingQuiescenceChecker : IRuntimeCheckpointQuiescenceChecker
        {
            public int CheckCount { get; private set; }
            public RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context)
            {
                CheckCount++;
                return RuntimeCheckpointQuiescenceResult.Ready();
            }
        }

        private sealed class PendingQuiescenceChecker : IRuntimeCheckpointQuiescenceChecker
        {
            public RuntimeCheckpointQuiescenceResult CheckQuiescence(in RuntimeCheckpointContext context) =>
                RuntimeCheckpointQuiescenceResult.Pending(1);
        }

        private sealed class FixedReadStore : NullOperationStore
        {
            private readonly SaveGameReadResult _result;
            public FixedReadStore(SaveGameReadResult result) { _result = result; }
            public override ISaveGameOperation<SaveGameReadResult> Read(SaveGameReadRequest request,
                CancellationToken cancellationToken = default) => new ImmediateOperation<SaveGameReadResult>(_result);
        }

        private sealed class ImmediateOperation<T> : ISaveGameOperation<T>
        {
            private readonly T _result;
            public ImmediateOperation(T result) { _result = result; }
            public SaveGameOperationId OperationId => new SaveGameOperationId("provider-op");
            public SaveGameOperationStatus Status => SaveGameOperationStatus.Succeeded;
            public float Progress => 1f;
            public bool IsDone => true;
            public bool CanCancel => false;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => true;
            public T Result => _result;
            public bool TryGetResult(out T result) { result = _result; return true; }
            public void Poll() { }
            public void RequestCancel() { }
        }

        private sealed class HostileCommitStore : NullOperationStore
        {
            public bool ThrowOnCancel { get; set; }
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) => new HostileCommitOperation(this);
        }

        private sealed class HostileCommitOperation : ISaveGameOperation<SaveGameCommitResult>
        {
            private readonly HostileCommitStore _owner;
            private int _pollCount;
            public HostileCommitOperation(HostileCommitStore owner) { _owner = owner; }
            public SaveGameOperationId OperationId => new SaveGameOperationId("hostile-provider");
            public SaveGameOperationStatus Status => SaveGameOperationStatus.Running;
            public float Progress => _pollCount <= 1 ? 0.5f : 0.25f;
            public bool IsDone => false;
            public bool CanCancel => true;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => false;
            public SaveGameCommitResult Result => default;
            public bool TryGetResult(out SaveGameCommitResult result) { result = default; return false; }
            public void Poll() { _pollCount++; }
            public void RequestCancel()
            {
                if (_owner.ThrowOnCancel) throw new InvalidOperationException("cancel failed");
            }
        }

        private enum ContradictoryStateMode { ChangingId, RunningToPending, HasResultMismatch }

        private sealed class ContradictoryStateStore : NullOperationStore
        {
            public ContradictoryStateMode Mode { get; set; }
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) => new ContradictoryStateOperation(Mode);
        }

        private sealed class ContradictoryStateOperation : ISaveGameOperation<SaveGameCommitResult>
        {
            private readonly ContradictoryStateMode _mode;
            private int _pollCount;
            public ContradictoryStateOperation(ContradictoryStateMode mode) { _mode = mode; }
            public SaveGameOperationId OperationId => new SaveGameOperationId(
                _mode == ContradictoryStateMode.ChangingId && _pollCount > 1 ? "changed" : "initial");
            public SaveGameOperationStatus Status =>
                _mode == ContradictoryStateMode.RunningToPending && _pollCount > 1
                    ? SaveGameOperationStatus.Pending
                    : SaveGameOperationStatus.Running;
            public float Progress => 0.5f;
            public bool IsDone => false;
            public bool CanCancel => true;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => _mode == ContradictoryStateMode.HasResultMismatch;
            public SaveGameCommitResult Result => default;
            public bool TryGetResult(out SaveGameCommitResult result) { result = default; return false; }
            public void Poll() { _pollCount++; }
            public void RequestCancel() { }
        }

        private sealed class AsyncSuccessStore : NullOperationStore
        {
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) => new AsyncSuccessOperation();
        }

        private sealed class SteadyRunningStore : NullOperationStore
        {
            public SteadyRunningOperation Operation { get; } = new SteadyRunningOperation();
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) => Operation;
        }

        private sealed class SteadyRunningOperation : ISaveGameOperation<SaveGameCommitResult>
        {
            private bool _complete;
            private readonly SaveGameCommitResult _result = new SaveGameCommitResult(
                new SaveGameGeneration(1), SaveGameWarning.None);
            public SaveGameOperationId OperationId => new SaveGameOperationId("steady-provider");
            public SaveGameOperationStatus Status => _complete
                ? SaveGameOperationStatus.Succeeded
                : SaveGameOperationStatus.Running;
            public float Progress => _complete ? 1f : 0.5f;
            public bool IsDone => _complete;
            public bool CanCancel => !_complete;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => _complete;
            public SaveGameCommitResult Result => _result;
            public bool TryGetResult(out SaveGameCommitResult result) { result = _result; return _complete; }
            public void Poll() { }
            public void RequestCancel() { }
            public void Complete() { _complete = true; }
        }

        private sealed class AsyncSuccessOperation : ISaveGameOperation<SaveGameCommitResult>
        {
            private int _stage;
            private readonly SaveGameCommitResult _result = new SaveGameCommitResult(
                new SaveGameGeneration(1), SaveGameWarning.RetentionCleanupFailed);
            public SaveGameOperationId OperationId => new SaveGameOperationId("async-provider");
            public SaveGameOperationStatus Status => _stage <= 1
                ? SaveGameOperationStatus.Pending
                : _stage < 4 ? SaveGameOperationStatus.Running : SaveGameOperationStatus.Succeeded;
            public float Progress => _stage <= 1 ? 0.1f : _stage == 2 ? 0.4f : _stage == 3 ? 0.8f : 1f;
            public bool IsDone => _stage >= 4;
            public bool CanCancel => _stage < 3;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => _stage >= 4;
            public SaveGameCommitResult Result => _result;
            public bool TryGetResult(out SaveGameCommitResult result) { result = _result; return HasResult; }
            public void Poll() { if (_stage < 4) _stage++; }
            public void RequestCancel() { }
        }

        private sealed class TerminalCommitStore : NullOperationStore
        {
            public SaveGameOperationStatus Terminal { get; set; }
            public override ISaveGameOperation<SaveGameCommitResult> Commit(SaveGameCommitRequest request,
                CancellationToken cancellationToken = default) => new TerminalCommitOperation(Terminal);
        }

        private sealed class TerminalCommitOperation : ISaveGameOperation<SaveGameCommitResult>
        {
            private readonly SaveGameOperationStatus _terminal;
            public TerminalCommitOperation(SaveGameOperationStatus terminal) { _terminal = terminal; }
            public SaveGameOperationId OperationId => new SaveGameOperationId("terminal-provider");
            public SaveGameOperationStatus Status => _terminal;
            public float Progress => 0f;
            public bool IsDone => true;
            public bool CanCancel => false;
            public SaveGameError Error => _terminal == SaveGameOperationStatus.Failed
                ? new SaveGameError(SaveGameErrorCode.IoFailure, SaveGameOperationPhase.Publication,
                    "publish", "injected")
                : _terminal == SaveGameOperationStatus.TimedOut
                    ? new SaveGameError(SaveGameErrorCode.Timeout, SaveGameOperationPhase.Publication,
                        "publish", "timed out")
                    : SaveGameError.None;
            public bool HasResult => false;
            public SaveGameCommitResult Result => default;
            public bool TryGetResult(out SaveGameCommitResult result) { result = default; return false; }
            public void Poll() { }
            public void RequestCancel() { }
        }

        private sealed class ContradictoryReadOperation : ISaveGameOperation<SaveGameReadResult>
        {
            public SaveGameOperationId OperationId => new SaveGameOperationId("contradictory");
            public SaveGameOperationStatus Status => SaveGameOperationStatus.Succeeded;
            public float Progress => 1f;
            public bool IsDone => true;
            public bool CanCancel => false;
            public SaveGameError Error => SaveGameError.None;
            public bool HasResult => false;
            public SaveGameReadResult Result => null;
            public bool TryGetResult(out SaveGameReadResult result) { result = null; return false; }
            public void Poll() { }
            public void RequestCancel() { }
        }

        private sealed class CandidateTarget : IRuntimeSaveGameRestoreTarget
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private readonly Barrier _acquiredBarrier;
            private int _acquireCount;
            public CandidateTarget(RuntimeSaveStateRestoreProfile profile, Barrier acquiredBarrier = null)
            {
                _profile = profile;
                _acquiredBarrier = acquiredBarrier;
            }
            public int AcquireCount => Volatile.Read(ref _acquireCount);
            public long CompletedFrame { get; private set; } = -1;
            public long NextFrame { get; private set; } = -1;
            public int PublishCount { get; private set; }
            public int TickCount { get; private set; }
            public bool TryAcquireRestoreLease(RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                Interlocked.Increment(ref _acquireCount);
                if (!ReferenceEquals(_profile, expectedRestoreProfile))
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidComposition, "profile");
                    return false;
                }
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                lease = new CandidateLease(this, _profile, factory.Coordinator);
                validationError = RuntimeSaveGameTargetValidationError.None;
                _acquiredBarrier?.SignalAndWait();
                return true;
            }
            private sealed class CandidateLease : IRuntimeSaveGameCandidateRestoreLease
            {
                private readonly CandidateTarget _target;
                private bool _completed;
                private bool _disposed;
                public CandidateLease(CandidateTarget target, RuntimeSaveStateRestoreProfile profile,
                    RuntimeSaveStateCoordinator coordinator) { _target = target; RestoreProfile = profile; Coordinator = coordinator; }
                public string TargetId => "candidate";
                public RuntimeSaveStateCoordinator Coordinator { get; }
                public RuntimeSaveStateRestoreProfile RestoreProfile { get; }
                public bool TryCompleteRestore(in RuntimeSaveGameRestoreCompletion completion,
                    out RuntimeSaveGameTargetValidationError validationError)
                {
                    if (_disposed || _completed || completion.CompletedFrame == long.MaxValue ||
                        completion.NextFrame != completion.CompletedFrame + 1)
                    {
                        validationError = new RuntimeSaveGameTargetValidationError(
                            RuntimeSaveGameTargetValidationErrorCode.CompletionRejected, "completion");
                        return false;
                    }
                    _completed = true;
                    _target.CompletedFrame = completion.CompletedFrame;
                    _target.NextFrame = completion.NextFrame;
                    validationError = RuntimeSaveGameTargetValidationError.None;
                    return true;
                }
                public void Dispose() { _disposed = true; }
            }
        }

        private sealed class SingleAcquireBlockingCandidateTarget :
            IRuntimeSaveGameRestoreTarget,
            IDisposable
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private int _acquired;
            private int _acquireCount;
            private int _completeCount;

            public SingleAcquireBlockingCandidateTarget(RuntimeSaveStateRestoreProfile profile)
            {
                _profile = profile;
            }

            public ManualResetEventSlim CompletionEntered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim AllowCompletion { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim FirstAcquireEntered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim AllowFirstAcquire { get; } = new ManualResetEventSlim(false);
            public int AcquireCount => Volatile.Read(ref _acquireCount);
            public int CompleteCount => Volatile.Read(ref _completeCount);

            public bool TryAcquireRestoreLease(
                RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                int acquireCall = Interlocked.Increment(ref _acquireCount);
                if (!ReferenceEquals(_profile, expectedRestoreProfile))
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.InvalidComposition,
                        "profile");
                    return false;
                }
                if (acquireCall == 1)
                {
                    FirstAcquireEntered.Set();
                    AllowFirstAcquire.Wait();
                }
                if (Interlocked.CompareExchange(ref _acquired, 1, 0) != 0)
                {
                    lease = null;
                    validationError = new RuntimeSaveGameTargetValidationError(
                        RuntimeSaveGameTargetValidationErrorCode.Unavailable,
                        "candidate.acquire");
                    return false;
                }

                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                Assert.That(factory.Success, Is.True);
                lease = new BlockingCompletionLease(this, factory.Coordinator);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }

            public void Dispose()
            {
                CompletionEntered.Dispose();
                AllowCompletion.Dispose();
                FirstAcquireEntered.Dispose();
                AllowFirstAcquire.Dispose();
            }

            private sealed class BlockingCompletionLease : IRuntimeSaveGameCandidateRestoreLease
            {
                private readonly SingleAcquireBlockingCandidateTarget _owner;
                private int _completed;

                public BlockingCompletionLease(
                    SingleAcquireBlockingCandidateTarget owner,
                    RuntimeSaveStateCoordinator coordinator)
                {
                    _owner = owner;
                    Coordinator = coordinator;
                }

                public string TargetId => "single-candidate";
                public RuntimeSaveStateCoordinator Coordinator { get; }
                public RuntimeSaveStateRestoreProfile RestoreProfile => _owner._profile;

                public bool TryCompleteRestore(
                    in RuntimeSaveGameRestoreCompletion completion,
                    out RuntimeSaveGameTargetValidationError validationError)
                {
                    _owner.CompletionEntered.Set();
                    _owner.AllowCompletion.Wait();
                    if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0 ||
                        completion.CompletedFrame < 0 || completion.CompletedFrame == long.MaxValue ||
                        completion.NextFrame != completion.CompletedFrame + 1L)
                    {
                        validationError = new RuntimeSaveGameTargetValidationError(
                            RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                            "candidate.complete");
                        return false;
                    }

                    Interlocked.Increment(ref _owner._completeCount);
                    validationError = RuntimeSaveGameTargetValidationError.None;
                    return true;
                }

                public void Dispose() { }
            }
        }

        private sealed class ContradictoryAcquireTarget : IRuntimeSaveGameRestoreTarget
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            public ContradictoryAcquireTarget(RuntimeSaveStateRestoreProfile profile) { _profile = profile; }
            public bool LeaseDisposed { get; private set; }
            public bool TryAcquireRestoreLease(RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                lease = new ContradictoryLease(this, _profile);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return false;
            }
            private sealed class ContradictoryLease : IRuntimeSaveGameCandidateRestoreLease
            {
                private readonly ContradictoryAcquireTarget _owner;
                public ContradictoryLease(ContradictoryAcquireTarget owner, RuntimeSaveStateRestoreProfile profile)
                { _owner = owner; RestoreProfile = profile; }
                public string TargetId => "contradictory";
                public RuntimeSaveStateCoordinator Coordinator => null;
                public RuntimeSaveStateRestoreProfile RestoreProfile { get; }
                public bool TryCompleteRestore(in RuntimeSaveGameRestoreCompletion completion,
                    out RuntimeSaveGameTargetValidationError validationError)
                { validationError = RuntimeSaveGameTargetValidationError.None; return true; }
                public void Dispose() { _owner.LeaseDisposed = true; }
            }
        }

        private sealed class ThrowingDisposeTarget : IRuntimeSaveGameRestoreTarget
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            public ThrowingDisposeTarget(RuntimeSaveStateRestoreProfile profile) { _profile = profile; }
            public bool DisposeAttempted { get; private set; }
            public bool TryAcquireRestoreLease(RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                lease = new ThrowingLease(this, _profile);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return false;
            }
            private sealed class ThrowingLease : IRuntimeSaveGameCandidateRestoreLease
            {
                private readonly ThrowingDisposeTarget _owner;
                public ThrowingLease(ThrowingDisposeTarget owner, RuntimeSaveStateRestoreProfile profile)
                { _owner = owner; RestoreProfile = profile; }
                public string TargetId => "throwing";
                public RuntimeSaveStateCoordinator Coordinator => null;
                public RuntimeSaveStateRestoreProfile RestoreProfile { get; }
                public bool TryCompleteRestore(in RuntimeSaveGameRestoreCompletion completion,
                    out RuntimeSaveGameTargetValidationError validationError)
                { validationError = RuntimeSaveGameTargetValidationError.None; return false; }
                public void Dispose()
                {
                    _owner.DisposeAttempted = true;
                    throw new InvalidOperationException("contract violation");
                }
            }
        }

        private enum CandidateFailureMode { Participant, CompleteFalse, CompleteThrow }

        private sealed class FailingCandidateTarget : IRuntimeSaveGameRestoreTarget
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private readonly CandidateFailureMode _mode;
            public FailingCandidateTarget(RuntimeSaveStateRestoreProfile profile, CandidateFailureMode mode)
            { _profile = profile; _mode = mode; }
            public bool Poisoned { get; private set; }
            public int PublishCount { get; private set; }
            public int TickCount { get; private set; }
            public bool TryAcquireRestoreLease(RuntimeSaveStateRestoreProfile expectedRestoreProfile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                if (_mode == CandidateFailureMode.Participant)
                    registry.Register(new RuntimeSaveStateParticipant("failing", 10, null, new FailingRestorer()));
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                lease = new FailingLease(this, _profile, factory.Coordinator, _mode);
                validationError = RuntimeSaveGameTargetValidationError.None;
                return true;
            }
            private sealed class FailingRestorer : IRuntimeSaveStateRestorer
            {
                public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState) =>
                    RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.InvalidDocument, "$.failing", "injected"));
            }
            private sealed class FailingLease : IRuntimeSaveGameCandidateRestoreLease
            {
                private readonly FailingCandidateTarget _target;
                private readonly CandidateFailureMode _mode;
                private bool _completed;
                public FailingLease(FailingCandidateTarget target, RuntimeSaveStateRestoreProfile profile,
                    RuntimeSaveStateCoordinator coordinator, CandidateFailureMode mode)
                { _target = target; RestoreProfile = profile; Coordinator = coordinator; _mode = mode; }
                public string TargetId => "failing-candidate";
                public RuntimeSaveStateCoordinator Coordinator { get; }
                public RuntimeSaveStateRestoreProfile RestoreProfile { get; }
                public bool TryCompleteRestore(in RuntimeSaveGameRestoreCompletion completion,
                    out RuntimeSaveGameTargetValidationError validationError)
                {
                    if (_mode == CandidateFailureMode.CompleteThrow) throw new InvalidOperationException("complete failed");
                    if (_mode == CandidateFailureMode.CompleteFalse)
                    {
                        validationError = RuntimeSaveGameTargetValidationError.None;
                        return false;
                    }
                    _completed = true;
                    validationError = RuntimeSaveGameTargetValidationError.None;
                    return true;
                }
                public void Dispose()
                {
                    if (!_completed) _target.Poisoned = true;
                }
            }
        }
    }
}
