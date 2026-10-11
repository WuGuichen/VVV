using System;
using System.IO;
using System.Text;
using System.Threading;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.PersistenceFileSystem
{
    [TestFixture]
    public sealed class FileSystemSaveGameStoreTests
    {
        private static readonly SaveGameProfileId Profile = new SaveGameProfileId("profile");
        private static readonly DateTime Timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "mx-fs-b2a-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void CRUD001_CommitPublishesCanonicalGenerationOne()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one")));

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), operation.Error.ToString());
            Assert.That(operation.Result.Generation.Value, Is.EqualTo(1UL));
            string finalPath = GenerationPath(slot, new SaveGameGeneration(1));
            Assert.That(File.Exists(finalPath), Is.True);
            SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(File.ReadAllBytes(finalPath));
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(Encoding.UTF8.GetString(decoded.Envelope.CopyPayload()), Is.EqualTo("one"));
            AssertNoStaging(slot);
        }

        [Test]
        public void CRUD003_RejectsNonCanonicalOrMismatchedCommitEnvelopeWithoutPublication()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            byte[] malformed = Encode(slot, SaveGameGeneration.None, "value").ToArray();
            malformed[0] ^= 0xff;

            ISaveGameOperation<SaveGameCommitResult> malformedOperation = Complete(store.Commit(
                new SaveGameCommitRequest(Profile, slot, SaveGameGeneration.None, new SaveGameEncodedEnvelope(malformed))));
            ISaveGameOperation<SaveGameCommitResult> mismatchOperation = Complete(store.Commit(
                new SaveGameCommitRequest(
                    Profile,
                    slot,
                    SaveGameGeneration.None,
                    Encode(new SaveGameSlotId("other"), SaveGameGeneration.None, "value"))));

            Assert.That(malformedOperation.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
            Assert.That(mismatchOperation.Error.Code, Is.EqualTo(SaveGameErrorCode.EnvelopePayloadMismatch));
            Assert.That(Directory.GetFiles(_root, "generation-*", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void CRUD005_ExpectedHeadMismatchReturnsGenerationConflict()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            AssertSucceeded(Complete(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one"))));

            ISaveGameOperation<SaveGameCommitResult> conflict = Complete(
                store.Commit(CommitRequest(slot, SaveGameGeneration.None, "competitor")));

            Assert.That(conflict.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(conflict.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            Assert.That(File.ReadAllBytes(GenerationPath(slot, new SaveGameGeneration(1))),
                Is.EqualTo(Encode(slot, SaveGameGeneration.None, "one").ToArray()));
        }

        [Test]
        public void CRUD006_EnvelopeMustBeExpectedHeadPlusOne()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            SaveGameEncodedEnvelope generationTwo = Encode(slot, new SaveGameGeneration(1), "two");

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(store.Commit(
                new SaveGameCommitRequest(Profile, slot, SaveGameGeneration.None, generationTwo)));

            Assert.That(operation.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            Assert.That(Directory.GetFiles(_root, "generation-*", SearchOption.AllDirectories), Is.Empty);
        }

        [Test]
        public void CRUD008_StrictHighestGenerationIsAuthoritativeHead()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            AssertSucceeded(Complete(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one"))));
            string generationThree = GenerationPath(slot, new SaveGameGeneration(3));
            File.WriteAllBytes(generationThree, EncodeExact(slot, new SaveGameGeneration(3), "three").ToArray());

            ISaveGameOperation<SaveGameCommitResult> conflict = Complete(
                store.Commit(CommitRequest(slot, new SaveGameGeneration(1), "two")));

            Assert.That(conflict.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            Assert.That(File.Exists(generationThree), Is.True);
        }

        [Test]
        public void CRUD009_DeletePublishesCanonicalTombstoneAtNextGeneration()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            AssertSucceeded(Complete(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one"))));

            ISaveGameOperation<SaveGameDeleteResult> deleted = Complete(store.Delete(
                new SaveGameDeleteRequest(Profile, slot, new SaveGameGeneration(1), Timestamp.AddSeconds(1), Build())));

            Assert.That(deleted.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), deleted.Error.ToString());
            Assert.That(deleted.Result.Generation.Value, Is.EqualTo(2UL));
            SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(
                File.ReadAllBytes(GenerationPath(slot, new SaveGameGeneration(2))));
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(decoded.Envelope.RecordKind, Is.EqualTo(SaveGameRecordKind.Tombstone));
            AssertNoStaging(slot);
        }

        [Test]
        public void Validation_NullBuildTimestampAndExhaustedGenerationReturnStructuredFailures()
        {
            FileSystemSaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");

            ISaveGameOperation<SaveGameCommitResult> nullCommit = Complete(store.Commit(null));
            ISaveGameOperation<SaveGameDeleteResult> nullBuild = Complete(store.Delete(
                new SaveGameDeleteRequest(Profile, slot, SaveGameGeneration.None, Timestamp, null)));
            ISaveGameOperation<SaveGameDeleteResult> invalidTimestamp = Complete(store.Delete(
                new SaveGameDeleteRequest(
                    Profile,
                    slot,
                    SaveGameGeneration.None,
                    DateTime.SpecifyKind(Timestamp, DateTimeKind.Local),
                    Build())));
            ISaveGameOperation<SaveGameCommitResult> exhausted = Complete(store.Commit(
                new SaveGameCommitRequest(
                    Profile,
                    slot,
                    new SaveGameGeneration(ulong.MaxValue),
                    EncodeExact(slot, new SaveGameGeneration(1), "unused"))));

            Assert.That(nullCommit.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidProfile));
            Assert.That(nullBuild.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
            Assert.That(invalidTimestamp.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidTimestamp));
            Assert.That(exhausted.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationExhausted));
        }

        [Test]
        public void QUOTA001_ProbeMapsTypedQuotaFailureWithoutLeakingPhysicalPath()
        {
            var backend = new QuotaFaultBackend(BclFileSystemBackend.Instance)
            {
                Boundary = QuotaFaultBoundary.CreateNew
            };
            PlatformLocationResult location = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                _root,
                true,
                true,
                true);

            FileSystemSaveGameStoreFactoryCreateResult result = FileSystemSaveGameStoreFactory.TryCreate(
                location.Location,
                backend,
                NullFileSystemDiagnosticSink.Instance);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(SaveGameErrorCode.QuotaExceeded));
            Assert.That(result.Error.Path, Is.EqualTo("publish"));
            Assert.That(result.Error.Exception, Is.Null);
            Assert.That(result.Error.ToString(), Does.Not.Contain(_root));
        }

        [Test]
        public void QUOTA002_WriteMapsTypedQuotaFailureAndUnknownIoRemainsIoFailure()
        {
            var backend = new QuotaFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Boundary = QuotaFaultBoundary.CreateNew;

            ISaveGameOperation<SaveGameCommitResult> quota = Complete(store.Commit(
                CommitRequest(new SaveGameSlotId("quota"), SaveGameGeneration.None, "value")));
            backend.ClassifyAsQuota = false;
            ISaveGameOperation<SaveGameCommitResult> unknown = Complete(store.Commit(
                CommitRequest(new SaveGameSlotId("unknown"), SaveGameGeneration.None, "value")));

            Assert.That(quota.Error.Code, Is.EqualTo(SaveGameErrorCode.QuotaExceeded));
            Assert.That(unknown.Error.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
            Assert.That(quota.Error.ToString(), Does.Not.Contain(_root));
            Assert.That(unknown.Error.ToString(), Does.Not.Contain(_root));
        }

        [Test]
        public void QUOTA003_ReadMapsTypedQuotaFailureWithoutRecoveryOrPathLeak()
        {
            var backend = new QuotaFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            var slot = new SaveGameSlotId("quota-read");
            AssertSucceeded(Complete(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value"))));
            backend.Boundary = QuotaFaultBoundary.OpenRead;

            ISaveGameOperation<SaveGameReadResult> read = Complete(store.Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.QuotaExceeded));
            Assert.That(read.Error.ToString(), Does.Not.Contain(_root));
            Assert.That(read.HasResult, Is.False);
        }

        [Test]
        public void MOVE001_NormalMovePublishesSuccess()
        {
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value")));

            AssertSucceeded(operation);
        }

        [Test]
        public void MOVE002_ThrownMoveWithAbsentFinalReturnsIoFailure()
        {
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Mode = MoveFaultMode.ThrowBeforeMove;

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value")));

            Assert.That(operation.Error.Code, Is.EqualTo(SaveGameErrorCode.IoFailure));
            Assert.That(operation.Error.Path, Is.EqualTo("publish"));
            Assert.That(operation.Error.Exception, Is.Null);
            Assert.That(operation.Error.ToString(), Does.Not.Contain(_root));
        }

        [Test]
        public void MOVE002_UnauthorizedMoveWithAbsentFinalReturnsPermissionDenied()
        {
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Mode = MoveFaultMode.ThrowUnauthorizedBeforeMove;

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value")));

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(operation.Error.Code, Is.EqualTo(SaveGameErrorCode.PermissionDenied));
            Assert.That(operation.Error.Phase, Is.EqualTo(SaveGameOperationPhase.Publication));
            Assert.That(operation.Error.Path, Is.EqualTo("publish"));
            Assert.That(operation.Error.Exception, Is.Null);
            Assert.That(operation.Error.ToString(), Does.Not.Contain(_root));
        }

        [Test]
        public void MOVE003_ThrownMoveWithExactFinalReconcilesSuccess()
        {
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Mode = MoveFaultMode.ThrowAfterMove;

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value")));

            AssertSucceeded(operation);
        }

        [Test]
        public void MOVE004_DifferentCompleteCompetitorReturnsConflictAndIsPreserved()
        {
            var slot = new SaveGameSlotId("slot");
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Mode = MoveFaultMode.PublishCompetitorAndThrow;
            backend.CompetitorBytes = Encode(slot, SaveGameGeneration.None, "competitor").ToArray();

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(slot, SaveGameGeneration.None, "request")));

            Assert.That(operation.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            Assert.That(File.ReadAllBytes(GenerationPath(slot, new SaveGameGeneration(1))),
                Is.EqualTo(backend.CompetitorBytes));
            AssertNoStaging(slot);
        }

        [Test]
        public void MOVE005_CorruptPresentFinalReturnsConflictAndIsPreserved()
        {
            var slot = new SaveGameSlotId("slot");
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStore store = CreateStore(backend);
            backend.Mode = MoveFaultMode.PublishCorruptAndThrow;

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                store.Commit(CommitRequest(slot, SaveGameGeneration.None, "request")));

            Assert.That(operation.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            Assert.That(File.ReadAllBytes(GenerationPath(slot, new SaveGameGeneration(1))),
                Is.EqualTo(new byte[] { 0x13, 0x37 }));
            AssertNoStaging(slot);
        }

        [Test]
        public void CANCEL_PrePublicationRequestCancelReturnsCancelledAndRemovesStaging()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.BeforePublication, false);
            FileSystemSaveGameStore store = CreateStore(null, hook);
            var slot = new SaveGameSlotId("slot");
            ISaveGameOperation<SaveGameCommitResult> operation = store.Commit(
                CommitRequest(slot, SaveGameGeneration.None, "value"));
            Assert.That(hook.Entered.Wait(5000), Is.True);

            operation.RequestCancel();
            hook.Release.Set();
            Complete(operation);

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(Directory.GetFiles(_root, "generation-*", SearchOption.AllDirectories), Is.Empty);
            AssertNoStaging(slot);
            hook.Dispose();
        }

        [Test]
        public void CANCEL_AfterPublicationGateRequestCancelCannotPreventSuccess()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.AfterPublicationGate, false);
            FileSystemSaveGameStore store = CreateStore(null, hook);
            var slot = new SaveGameSlotId("slot");
            ISaveGameOperation<SaveGameCommitResult> operation = store.Commit(
                CommitRequest(slot, SaveGameGeneration.None, "value"));
            Assert.That(hook.Entered.Wait(5000), Is.True);

            Assert.That(operation.CanCancel, Is.False);
            operation.RequestCancel();
            hook.Release.Set();
            Complete(operation);

            AssertSucceeded(operation);
            Assert.That(File.Exists(GenerationPath(slot, new SaveGameGeneration(1))), Is.True);
            hook.Dispose();
        }

        [Test]
        public void CANCEL_ImmediatelyAfterPublicationCannotReplaceSuccess()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.AfterPublication, false);
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var slot = new SaveGameSlotId("cancel-after-publication");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));
            ISaveGameOperation<SaveGameCommitResult> operation = factory.CreateStore(hook).Commit(
                CommitRequest(slot, new SaveGameGeneration(1), "new"));
            Assert.That(hook.Entered.Wait(5000), Is.True);

            Assert.That(operation.CanCancel, Is.False);
            operation.RequestCancel();
            hook.Release.Set();
            Complete(operation);

            AssertSucceeded(operation);
            AssertCurrentPayload(factory.CreateStore(), slot, "new", 2);
            AssertNoStaging(slot);
            hook.Dispose();
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(1)]
        public void CANCEL_EveryPrePublicationBoundary_RecreatedStoreKeepsPreviousGeneration(
            int pointValue)
        {
            var point = (FileSystemSaveGameStoreHookPoint)pointValue;
            var hook = new BlockingHook(point, false);
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var slot = new SaveGameSlotId("cancel-boundary-" + pointValue);
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));
            ISaveGameOperation<SaveGameCommitResult> operation = factory.CreateStore(hook).Commit(
                CommitRequest(slot, new SaveGameGeneration(1), "new"));
            Assert.That(hook.Entered.Wait(5000), Is.True);

            Assert.That(operation.CanCancel, Is.True);
            operation.RequestCancel();
            hook.Release.Set();
            Complete(operation);

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            AssertCurrentPayload(factory.CreateStore(), slot, "old", 1);
            AssertNoStaging(slot);
            hook.Dispose();
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(1)]
        [TestCase(2)]
        public void FAULT_PrePublicationBoundary_RecreatedStoreSeesCompletePreviousGeneration(
            int pointValue)
        {
            var point = (FileSystemSaveGameStoreHookPoint)pointValue;
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var slot = new SaveGameSlotId("fault-boundary");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));

            var hook = new ThrowingHook(point);
            ISaveGameOperation<SaveGameCommitResult> failed = Complete(factory.CreateStore(hook).Commit(
                CommitRequest(slot, new SaveGameGeneration(1), "new")));

            Assert.That(failed.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            AssertCurrentPayload(factory.CreateStore(), slot, "old", 1);
            AssertNoStaging(slot);
        }

        [Test]
        public void FAULT_PartialWrite_RecreatedStoreSeesCompletePreviousGeneration()
        {
            var backend = new PartialWriteBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStoreFactory factory = CreateFactory(backend);
            var slot = new SaveGameSlotId("partial-write");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));

            backend.Armed = true;
            ISaveGameOperation<SaveGameCommitResult> failed = Complete(factory.CreateStore().Commit(
                CommitRequest(slot, new SaveGameGeneration(1), "new")));
            backend.Armed = false;

            Assert.That(failed.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            AssertCurrentPayload(factory.CreateStore(), slot, "old", 1);
            AssertNoStaging(slot);
        }

        [Test]
        public void FAULT_StagedCorruption_RecreatedStoreSeesCompletePreviousGeneration()
        {
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var slot = new SaveGameSlotId("staged-corruption");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));

            ISaveGameOperation<SaveGameCommitResult> failed = Complete(
                factory.CreateStore(new CorruptingHook()).Commit(
                    CommitRequest(slot, new SaveGameGeneration(1), "new")));

            Assert.That(failed.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(failed.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
            AssertCurrentPayload(factory.CreateStore(), slot, "old", 1);
            AssertNoStaging(slot);
        }

        [TestCase(1, SaveGameOperationStatus.Failed, "old", 1UL)]
        [TestCase(2, SaveGameOperationStatus.Succeeded, "new", 2UL)]
        public void FAULT_MoveBoundary_RecreatedStoreSeesOnlyCompleteOldOrNewGeneration(
            int modeValue,
            SaveGameOperationStatus expectedStatus,
            string expectedPayload,
            ulong expectedGeneration)
        {
            var mode = (MoveFaultMode)modeValue;
            var backend = new MoveFaultBackend(BclFileSystemBackend.Instance);
            FileSystemSaveGameStoreFactory factory = CreateFactory(backend);
            var slot = new SaveGameSlotId("move-boundary");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));

            backend.Mode = mode;
            ISaveGameOperation<SaveGameCommitResult> operation = Complete(factory.CreateStore().Commit(
                CommitRequest(slot, new SaveGameGeneration(1), "new")));
            backend.Mode = MoveFaultMode.None;

            Assert.That(operation.Status, Is.EqualTo(expectedStatus));
            AssertCurrentPayload(
                factory.CreateStore(),
                slot,
                expectedPayload,
                expectedGeneration);
            AssertNoStaging(slot);
        }

        [Test]
        public void FAULT_AfterPublication_RecreatedStoreSeesCompleteNewGenerationAndReportsSuccess()
        {
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var slot = new SaveGameSlotId("after-publication");
            AssertSucceeded(Complete(factory.CreateStore().Commit(
                CommitRequest(slot, SaveGameGeneration.None, "old"))));

            ISaveGameOperation<SaveGameCommitResult> operation = Complete(
                factory.CreateStore(new ThrowingHook(FileSystemSaveGameStoreHookPoint.AfterPublication)).Commit(
                    CommitRequest(slot, new SaveGameGeneration(1), "new")));

            AssertSucceeded(operation);
            AssertCurrentPayload(factory.CreateStore(), slot, "new", 2);
            AssertNoStaging(slot);
        }

        [Test]
        public void SameStore_TwoExpectedHeadWritersProduceExactlyOneSuccess()
        {
            FileSystemSaveGameStore store = CreateStore();
            AssertExactlyOneWinner(
                store,
                store,
                new SaveGameSlotId("same-store"));
        }

        [Test]
        public void CrossStore_TwoExpectedHeadWritersProduceExactlyOneSuccess()
        {
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            AssertExactlyOneWinner(
                (FileSystemSaveGameStore)factory.CreateStore(),
                (FileSystemSaveGameStore)factory.CreateStore(),
                new SaveGameSlotId("cross-store"));
        }

        [Test]
        public void CrossStore_DifferentSlotsCanReachPublicationConcurrently()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.BeforePublication, false, 2);
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            FileSystemSaveGameStore firstStore = factory.CreateStore(hook);
            FileSystemSaveGameStore secondStore = factory.CreateStore(hook);
            ISaveGameOperation<SaveGameCommitResult> first = firstStore.Commit(
                CommitRequest(new SaveGameSlotId("parallel-a"), SaveGameGeneration.None, "first"));
            ISaveGameOperation<SaveGameCommitResult> second = secondStore.Commit(
                CommitRequest(new SaveGameSlotId("parallel-b"), SaveGameGeneration.None, "second"));

            Assert.That(hook.AllEntered.Wait(5000), Is.True,
                "Different slots must not serialize on a root-wide writer lock.");
            hook.Release.Set();
            Complete(first);
            Complete(second);

            AssertSucceeded(first);
            AssertSucceeded(second);
            hook.Dispose();
        }

        [Test]
        public void Factory_CreateStoreSharesRootScopedCapacity()
        {
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            var first = (FileSystemSaveGameStore)factory.CreateStore();
            var second = (FileSystemSaveGameStore)factory.CreateStore();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Capacity, Is.SameAs(second.Capacity));
            Assert.That(first.Capacity.Maximum, Is.EqualTo(8));
            Assert.That(second.Capacity.Maximum, Is.EqualTo(8));
        }

        [Test]
        public void Capacity_EightAcrossStoresMakesNinthBusyAndReleasesWithoutPoll()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.BeforeWriterLock, false, 8);
            FileSystemSaveGameStoreFactory factory = CreateFactory();
            FileSystemSaveGameStore firstStore = factory.CreateStore(hook);
            FileSystemSaveGameStore secondStore = factory.CreateStore(hook);
            var active = new FileSystemSaveGameOperation<SaveGameCommitResult>[8];
            for (int index = 0; index < active.Length; index++)
            {
                FileSystemSaveGameStore store = (index & 1) == 0 ? firstStore : secondStore;
                active[index] = (FileSystemSaveGameOperation<SaveGameCommitResult>)store.Commit(
                    CommitRequest(new SaveGameSlotId("cross-capacity-" + index), SaveGameGeneration.None, "value"));
            }

            Assert.That(hook.AllEntered.Wait(5000), Is.True);
            ISaveGameOperation<SaveGameCommitResult> ninth = Complete(secondStore.Commit(
                CommitRequest(new SaveGameSlotId("cross-capacity-9"), SaveGameGeneration.None, "value")));
            Assert.That(ninth.Error.Code, Is.EqualTo(SaveGameErrorCode.Busy));

            hook.Release.Set();
            for (int index = 0; index < active.Length; index++)
            {
                Assert.That(SpinWait.SpinUntil(() => active[index].HasWorkerCompletion, 10000), Is.True);
            }

            Assert.That(SpinWait.SpinUntil(() => firstStore.Capacity.ActiveCount == 0, 5000), Is.True);
            hook.Dispose();
        }

        [Test]
        public void Capacity_EightPlusOneIsBusyAndWorkerCompletionReleasesWithoutPoll()
        {
            var hook = new BlockingHook(FileSystemSaveGameStoreHookPoint.BeforeWriterLock, false, 8);
            FileSystemSaveGameStore store = CreateStore(null, hook);
            var active = new FileSystemSaveGameOperation<SaveGameCommitResult>[8];
            for (int index = 0; index < active.Length; index++)
            {
                active[index] = (FileSystemSaveGameOperation<SaveGameCommitResult>)store.Commit(
                    CommitRequest(new SaveGameSlotId("slot-" + index), SaveGameGeneration.None, "value"));
            }

            Assert.That(hook.AllEntered.Wait(5000), Is.True);
            ISaveGameOperation<SaveGameCommitResult> ninth = Complete(store.Commit(
                CommitRequest(new SaveGameSlotId("slot-9"), SaveGameGeneration.None, "value")));
            Assert.That(ninth.Error.Code, Is.EqualTo(SaveGameErrorCode.Busy));

            hook.Release.Set();
            for (int index = 0; index < active.Length; index++)
            {
                Assert.That(SpinWait.SpinUntil(() => active[index].HasWorkerCompletion, 10000), Is.True);
            }

            Assert.That(SpinWait.SpinUntil(() => store.Capacity.ActiveCount == 0, 5000), Is.True);
            ISaveGameOperation<SaveGameCommitResult> next = Complete(store.Commit(
                CommitRequest(new SaveGameSlotId("slot-next"), SaveGameGeneration.None, "value")));
            AssertSucceeded(next);
            hook.Dispose();
        }

        [Test]
        public void ReadAndList_ReturnTerminalNonNullResults()
        {
            ISaveGameStore store = CreateStore();
            var slot = new SaveGameSlotId("slot");
            AssertSucceeded(Complete(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value"))));
            ISaveGameOperation<SaveGameReadResult> read = Complete(store.Read(
                new SaveGameReadRequest(Profile, new SaveGameSlotId("slot"), SaveGameReadSelection.Current)));
            ISaveGameOperation<SaveGameListResult> list = Complete(store.List(new SaveGameListRequest(Profile)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.HasResult, Is.True);
            Assert.That(read.Result, Is.Not.Null);
            Assert.That(list.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), list.Error.ToString());
            Assert.That(list.HasResult, Is.True);
            Assert.That(list.Result, Is.Not.Null);
            Assert.That(list.Result.Count, Is.EqualTo(1));
        }

        private void AssertExactlyOneWinner(
            FileSystemSaveGameStore firstStore,
            FileSystemSaveGameStore secondStore,
            SaveGameSlotId slot)
        {
            ISaveGameOperation<SaveGameCommitResult> first = firstStore.Commit(
                CommitRequest(slot, SaveGameGeneration.None, "first"));
            ISaveGameOperation<SaveGameCommitResult> second = secondStore.Commit(
                CommitRequest(slot, SaveGameGeneration.None, "second"));
            Complete(first);
            Complete(second);
            int succeeded = (first.Status == SaveGameOperationStatus.Succeeded ? 1 : 0)
                + (second.Status == SaveGameOperationStatus.Succeeded ? 1 : 0);
            Assert.That(succeeded, Is.EqualTo(1));
            ISaveGameOperation<SaveGameCommitResult> failed = first.Status == SaveGameOperationStatus.Failed
                ? first
                : second;
            Assert.That(failed.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
        }

        private FileSystemSaveGameStore CreateStore(
            IFileSystemBackend backend = null,
            IFileSystemSaveGameStoreTestHook hook = null)
        {
            FileSystemSaveGameStoreFactory factory = CreateFactory(backend);
            return hook == null
                ? (FileSystemSaveGameStore)factory.CreateStore()
                : factory.CreateStore(hook);
        }

        private FileSystemSaveGameStoreFactory CreateFactory(IFileSystemBackend backend = null)
        {
            PlatformLocationResult location = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                _root,
                true,
                true,
                true);
            Assert.That(location.Success, Is.True, location.Error.ToString());
            FileSystemSaveGameStoreFactoryCreateResult created = backend == null
                ? FileSystemSaveGameStoreFactory.TryCreate(location.Location)
                : FileSystemSaveGameStoreFactory.TryCreate(
                    location.Location,
                    backend,
                    NullFileSystemDiagnosticSink.Instance);
            Assert.That(created.Success, Is.True, created.Error.ToString());
            return created.Factory;
        }

        private string GenerationPath(SaveGameSlotId slot, SaveGameGeneration generation)
        {
            Assert.That(FileSystemSaveGameLayout.TryGetSlotDirectory(
                _root,
                Profile,
                slot,
                BclFileSystemBackend.Instance,
                out string slotPath,
                out SaveGameError error), Is.True, error.ToString());
            return Path.Combine(slotPath, FileSystemGenerationLayout.Format(generation));
        }

        private void AssertNoStaging(SaveGameSlotId slot)
        {
            string generationPath = GenerationPath(slot, new SaveGameGeneration(1));
            string slotPath = Path.GetDirectoryName(generationPath);
            if (Directory.Exists(slotPath))
            {
                Assert.That(Directory.GetFiles(slotPath, ".staging-*"), Is.Empty);
            }
        }

        private static void AssertCurrentPayload(
            ISaveGameStore store,
            SaveGameSlotId slot,
            string expectedPayload,
            ulong expectedGeneration)
        {
            ISaveGameOperation<SaveGameReadResult> read = Complete(store.Read(
                new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)));
            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(expectedGeneration));
            SaveGameEnvelopeResult decoded = SaveGameEnvelopeCodec.Decode(read.Result.Envelope);
            Assert.That(decoded.IsSuccess, Is.True, decoded.Error.ToString());
            Assert.That(Encoding.UTF8.GetString(decoded.Envelope.CopyPayload()), Is.EqualTo(expectedPayload));
        }

        private static ISaveGameOperation<SaveGameCommitResult> Complete(
            ISaveGameOperation<SaveGameCommitResult> operation)
        {
            Assert.That(SpinWait.SpinUntil(
                () => ((FileSystemSaveGameOperation<SaveGameCommitResult>)operation).HasWorkerCompletion,
                10000), Is.True);
            operation.Poll();
            return operation;
        }

        private static ISaveGameOperation<SaveGameDeleteResult> Complete(
            ISaveGameOperation<SaveGameDeleteResult> operation)
        {
            Assert.That(SpinWait.SpinUntil(
                () => ((FileSystemSaveGameOperation<SaveGameDeleteResult>)operation).HasWorkerCompletion,
                10000), Is.True);
            operation.Poll();
            return operation;
        }

        private static ISaveGameOperation<SaveGameReadResult> Complete(
            ISaveGameOperation<SaveGameReadResult> operation)
        {
            Assert.That(SpinWait.SpinUntil(
                () => ((FileSystemSaveGameOperation<SaveGameReadResult>)operation).HasWorkerCompletion,
                10000), Is.True);
            operation.Poll();
            return operation;
        }

        private static ISaveGameOperation<SaveGameListResult> Complete(
            ISaveGameOperation<SaveGameListResult> operation)
        {
            Assert.That(SpinWait.SpinUntil(
                () => ((FileSystemSaveGameOperation<SaveGameListResult>)operation).HasWorkerCompletion,
                10000), Is.True);
            operation.Poll();
            return operation;
        }

        private static void AssertSucceeded(ISaveGameOperation<SaveGameCommitResult> operation)
        {
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), operation.Error.ToString());
        }

        private static SaveGameCommitRequest CommitRequest(
            SaveGameSlotId slot,
            SaveGameGeneration expected,
            string payload)
        {
            return new SaveGameCommitRequest(Profile, slot, expected, Encode(slot, expected, payload));
        }

        private static SaveGameEncodedEnvelope Encode(
            SaveGameSlotId slot,
            SaveGameGeneration expected,
            string payload)
        {
            Assert.That(expected.TryNext(out SaveGameGeneration next), Is.True);
            return EncodeExact(slot, next, payload);
        }

        private static SaveGameEncodedEnvelope EncodeExact(
            SaveGameSlotId slot,
            SaveGameGeneration generation,
            string payload)
        {
            SaveGameEnvelope envelope = SaveGameEnvelope.CreateSave(
                Profile,
                slot,
                generation,
                Timestamp,
                Timestamp,
                Build(),
                Array.Empty<SaveGameKeyValue>(),
                SaveGameEnvelope.RuntimeSaveStateJsonCodecId,
                1,
                0,
                Array.Empty<SaveGameKeyValue>(),
                Encoding.UTF8.GetBytes(payload));
            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(envelope);
            Assert.That(encoded.IsSuccess, Is.True, encoded.Error.ToString());
            return encoded.Encoded;
        }

        private static SaveGameBuildDescriptor Build()
        {
            return new SaveGameBuildDescriptor("1", "build", "config", "resources", "content");
        }

        private enum MoveFaultMode
        {
            None = 0,
            ThrowBeforeMove = 1,
            ThrowAfterMove = 2,
            PublishCompetitorAndThrow = 3,
            ThrowUnauthorizedBeforeMove = 4,
            PublishCorruptAndThrow = 5
        }

        private sealed class MoveFaultBackend : IFileSystemBackend
        {
            private readonly IFileSystemBackend _inner;
            public MoveFaultBackend(IFileSystemBackend inner) { _inner = inner; }
            public MoveFaultMode Mode { get; set; }
            public byte[] CompetitorBytes { get; set; }
            public FileSystemEntryStatus GetStatus(string path) => _inner.GetStatus(path);
            public FileSystemDirectoryEntry[] GetEntries(string directoryPath) => _inner.GetEntries(directoryPath);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public Stream OpenCreateNew(string path) => _inner.OpenCreateNew(path);
            public Stream OpenRead(string path) => _inner.OpenRead(path);
            public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path) => _inner.TryOpenExclusiveLock(path);
            public void FlushDurable(Stream stream) => _inner.FlushDurable(stream);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);

            public void MoveNoOverwrite(string sourcePath, string destinationPath)
            {
                switch (Mode)
                {
                    case MoveFaultMode.ThrowBeforeMove:
                        throw new IOException("move failed before publication");
                    case MoveFaultMode.ThrowUnauthorizedBeforeMove:
                        throw new UnauthorizedAccessException("move denied at " + destinationPath);
                    case MoveFaultMode.ThrowAfterMove:
                        _inner.MoveNoOverwrite(sourcePath, destinationPath);
                        throw new IOException("move result was ambiguous");
                    case MoveFaultMode.PublishCompetitorAndThrow:
                        using (Stream stream = _inner.OpenCreateNew(destinationPath))
                        {
                            stream.Write(CompetitorBytes, 0, CompetitorBytes.Length);
                            _inner.FlushDurable(stream);
                        }
                        throw new IOException("competitor won publication");
                    case MoveFaultMode.PublishCorruptAndThrow:
                        using (Stream stream = _inner.OpenCreateNew(destinationPath))
                        {
                            stream.WriteByte(0x13);
                            stream.WriteByte(0x37);
                            _inner.FlushDurable(stream);
                        }
                        throw new IOException("corrupt competitor occupied publication");
                    default:
                        _inner.MoveNoOverwrite(sourcePath, destinationPath);
                        return;
                }
            }
        }

        private sealed class PartialWriteBackend : IFileSystemBackend
        {
            private readonly IFileSystemBackend _inner;
            public PartialWriteBackend(IFileSystemBackend inner) { _inner = inner; }
            public bool Armed { get; set; }
            public FileSystemEntryStatus GetStatus(string path) => _inner.GetStatus(path);
            public FileSystemDirectoryEntry[] GetEntries(string directoryPath) => _inner.GetEntries(directoryPath);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public Stream OpenCreateNew(string path)
            {
                Stream inner = _inner.OpenCreateNew(path);
                return Armed && Path.GetFileName(path).StartsWith(".staging-", System.StringComparison.Ordinal)
                    ? new PartialWriteStream(inner)
                    : inner;
            }
            public Stream OpenRead(string path) => _inner.OpenRead(path);
            public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path) => _inner.TryOpenExclusiveLock(path);
            public void FlushDurable(Stream stream) => _inner.FlushDurable(stream);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
            public void MoveNoOverwrite(string sourcePath, string destinationPath) =>
                _inner.MoveNoOverwrite(sourcePath, destinationPath);
        }

        private enum QuotaFaultBoundary
        {
            None = 0,
            CreateNew = 1,
            OpenRead = 2
        }

        private sealed class QuotaFaultBackend : IFileSystemBackend, IFileSystemIoFailureClassifier
        {
            private readonly IFileSystemBackend _inner;
            public QuotaFaultBackend(IFileSystemBackend inner) { _inner = inner; }
            public QuotaFaultBoundary Boundary { get; set; }
            public bool ClassifyAsQuota { get; set; } = true;
            public FileSystemIoFailureKind Classify(IOException exception) =>
                ClassifyAsQuota ? FileSystemIoFailureKind.QuotaExceeded : FileSystemIoFailureKind.Unknown;
            public FileSystemEntryStatus GetStatus(string path) => _inner.GetStatus(path);
            public FileSystemDirectoryEntry[] GetEntries(string directoryPath) => _inner.GetEntries(directoryPath);
            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public Stream OpenCreateNew(string path)
            {
                if (Boundary == QuotaFaultBoundary.CreateNew)
                    throw new IOException("quota exceeded at " + path);
                return _inner.OpenCreateNew(path);
            }
            public Stream OpenRead(string path)
            {
                if (Boundary == QuotaFaultBoundary.OpenRead)
                    throw new IOException("quota exceeded at " + path);
                return _inner.OpenRead(path);
            }
            public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path) => _inner.TryOpenExclusiveLock(path);
            public void FlushDurable(Stream stream) => _inner.FlushDurable(stream);
            public void MoveNoOverwrite(string sourcePath, string destinationPath) =>
                _inner.MoveNoOverwrite(sourcePath, destinationPath);
            public void DeleteFile(string path) => _inner.DeleteFile(path);
            public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
        }

        private sealed class PartialWriteStream : Stream
        {
            private readonly Stream _inner;
            public PartialWriteStream(Stream inner) { _inner = inner; }
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count)
            {
                int partialCount = System.Math.Max(1, count / 2);
                _inner.Write(buffer, offset, partialCount);
                throw new IOException("injected partial write");
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class ThrowingHook : IFileSystemSaveGameStoreTestHook
        {
            private readonly FileSystemSaveGameStoreHookPoint _point;
            public ThrowingHook(FileSystemSaveGameStoreHookPoint point) { _point = point; }
            public void Reach(FileSystemSaveGameStoreHookPoint point, FileSystemSaveGameStoreHookContext context)
            {
                if (point == _point) throw new IOException("injected fault at " + point);
            }
        }

        private sealed class CorruptingHook : IFileSystemSaveGameStoreTestHook
        {
            public void Reach(FileSystemSaveGameStoreHookPoint point, FileSystemSaveGameStoreHookContext context)
            {
                if (point != FileSystemSaveGameStoreHookPoint.AfterStagingClosed) return;
                byte[] bytes = File.ReadAllBytes(context.StagingPath);
                bytes[bytes.Length - 1] ^= 0xff;
                File.WriteAllBytes(context.StagingPath, bytes);
            }
        }

        private sealed class BlockingHook : IFileSystemSaveGameStoreTestHook, IDisposable
        {
            private readonly FileSystemSaveGameStoreHookPoint _point;
            private readonly bool _throwAfterRelease;
            private readonly int _targetCount;
            private int _count;

            public BlockingHook(
                FileSystemSaveGameStoreHookPoint point,
                bool throwAfterRelease,
                int targetCount = 1)
            {
                _point = point;
                _throwAfterRelease = throwAfterRelease;
                _targetCount = targetCount;
            }

            public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim AllEntered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

            public void Reach(FileSystemSaveGameStoreHookPoint point, FileSystemSaveGameStoreHookContext context)
            {
                if (point != _point) return;
                Entered.Set();
                if (Interlocked.Increment(ref _count) >= _targetCount) AllEntered.Set();
                Release.Wait();
                if (_throwAfterRelease) throw new IOException("deterministic post-publication fault");
            }

            public void Dispose()
            {
                Release.Set();
                Entered.Dispose();
                AllEntered.Dispose();
                Release.Dispose();
            }
        }
    }
}
