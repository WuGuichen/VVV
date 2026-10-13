using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.PersistenceFileSystem
{
    [TestFixture]
    public sealed class FileSystemSaveGameStoreReadRetentionTests
    {
        private static readonly SaveGameProfileId Profile = new SaveGameProfileId("profile");
        private static readonly DateTime Time = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        private string _root;
        private TestBackend _backend;
        private FileSystemSaveGameStoreFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "mx-fs-b2b-" + Guid.NewGuid().ToString("N"));
            _backend = new TestBackend(BclFileSystemBackend.Instance);
            PlatformLocationResult location = PlatformLocation.TryCreate(
                PlatformLocationKind.FileSystemPath,
                _root,
                true,
                true,
                true);
            Assert.That(location.Success, Is.True, location.Error.ToString());
            FileSystemSaveGameStoreFactoryCreateResult created = FileSystemSaveGameStoreFactory.TryCreate(
                location.Location,
                _backend,
                NullFileSystemDiagnosticSink.Instance);
            Assert.That(created.Success, Is.True, created.Error.ToString());
            _factory = created.Factory;
        }

        [TearDown]
        public void TearDown()
        {
            _backend.ThrowGetEntries = false;
            _backend.ThrowGenerationDelete = false;
            _backend.UnauthorizedReadPath = string.Empty;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ReadIncludeDeletedReturnsFullyValidatedHeadWithoutCrossingDeletionBarrier()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteTombstone(slot, 2);
            var store = _factory.CreateStore(null);
            Assert.AreEqual(SaveGameErrorCode.SlotDeleted, Complete(store.Read(
                new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current))).Error.Code);
            var read = Complete(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current,
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood, includeDeleted: true)));
            Assert.IsTrue(read.TryGetResult(out var result), read.Error.ToString());
            Assert.AreEqual(2UL, result.OriginalGeneration.Value);
            Assert.AreEqual(2UL, result.SelectedGeneration.Value);
            Assert.AreEqual(SaveGameRecordKind.Tombstone, SaveGameEnvelopeCodec.Decode(result.Envelope).Envelope.RecordKind);
            WriteBytes(slot, 3, Corrupt(EncodeSave(slot, 3, "three")));
            var blocked = Complete(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current,
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood, includeDeleted: true)));
            Assert.AreEqual(SaveGameOperationStatus.Failed, blocked.Status);
            Assert.IsFalse(blocked.HasResult, "IncludeDeleted must never recover an older Save across a tombstone.");
        }

        [Test]
        public void READ001_CurrentAndExactReturnExplicitGenerationAndSource()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteSave(slot, 2, "two");
            FileSystemSaveGameStore store = _factory.CreateStore(null);

            ISaveGameOperation<SaveGameReadResult> current = Complete(store.Read(
                new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)));
            ISaveGameOperation<SaveGameReadResult> exact = Complete(store.Read(
                new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Exact(new SaveGameGeneration(1)))));

            Assert.That(current.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), current.Error.ToString());
            Assert.That(current.Result.OriginalGeneration.Value, Is.EqualTo(2UL));
            Assert.That(current.Result.SelectedGeneration.Value, Is.EqualTo(2UL));
            Assert.That(current.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.Primary));
            Assert.That(exact.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), exact.Error.ToString());
            Assert.That(exact.Result.OriginalGeneration.Value, Is.EqualTo(2UL));
            Assert.That(exact.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            Assert.That(exact.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.Primary));
        }

        [Test]
        public void READ002_ExactMissingReturnsGenerationNotFound()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Exact(new SaveGameGeneration(2)),
                    SaveGameRecoveryPolicy.PrimaryOnly)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationNotFound));
        }

        [Test]
        public void READ003_IntegrityFailureRecoversWhenCurrentAllowsIt()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, Corrupt(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.OriginalGeneration.Value, Is.EqualTo(2UL));
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            Assert.That(read.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.LastKnownGood));
        }

        [Test]
        public void READ004_IntegrityFailureWithoutRecoveryFailsAtPrimary()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, Corrupt(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryOnly)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
            Assert.That(read.HasResult, Is.False);
        }

        [Test]
        public void READ005_ExactCorruptNeverFallsBack()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, Corrupt(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Exact(new SaveGameGeneration(2)),
                    SaveGameRecoveryPolicy.PrimaryOnly)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
        }

        [Test]
        public void READ005B_ExactSelectionRejectsRecoveryPolicy()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Exact(new SaveGameGeneration(1)),
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidSelection));
            Assert.That(read.HasResult, Is.False);
        }

        [Test]
        public void READ006_UnsupportedPrimaryNeverFallsBack()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, UnsupportedVersion(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.UnsupportedEnvelopeVersion));
        }

        [Test]
        public void READ007_UnsupportedCandidateIsRecoveryBarrier()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, UnsupportedVersion(EncodeSave(slot, 2, "two")));
            WriteBytes(slot, 3, Corrupt(EncodeSave(slot, 3, "three")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.UnsupportedEnvelopeVersion));
        }

        [Test]
        public void READ008_ValidTombstoneIsCurrentAndRecoveryBarrier()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteTombstone(slot, 2);
            ISaveGameOperation<SaveGameReadResult> currentTombstone = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));
            WriteBytes(slot, 3, Corrupt(EncodeSave(slot, 3, "three")));
            ISaveGameOperation<SaveGameReadResult> corruptAboveBarrier = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(currentTombstone.Error.Code, Is.EqualTo(SaveGameErrorCode.SlotDeleted));
            Assert.That(corruptAboveBarrier.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
        }

        [Test]
        public void READ009_PathEnvelopeMirrorMismatchCanRecover()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, EncodeSave(new SaveGameSlotId("other"), 2, "wrong"));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
        }

        [Test]
        public void READ010_BothGenerationsCorruptFailsAndPreservesFiles()
        {
            var slot = new SaveGameSlotId("slot");
            WriteBytes(slot, 1, Corrupt(EncodeSave(slot, 1, "one")));
            WriteBytes(slot, 2, Corrupt(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(File.Exists(GenerationPath(slot, 1)), Is.True);
            Assert.That(File.Exists(GenerationPath(slot, 2)), Is.True);
        }

        [Test]
        public void READ011_PermissionFailureNeverFallsBackAndIsSanitized()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteSave(slot, 2, "two");
            _backend.UnauthorizedReadPath = GenerationPath(slot, 2);

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.PermissionDenied));
            Assert.That(read.Error.Exception, Is.Null);
            Assert.That(read.Error.ToString(), Does.Not.Contain(_root));
        }

        [Test]
        public void READ012_StorageLengthLimitRecoversWithoutOpeningOversizedHead()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteSave(slot, 2, "two");
            _backend.OverrideLengthPath = GenerationPath(slot, 2);
            _backend.OverrideLength = SaveGameLimits.MaximumEnvelopeBytes + 1L;

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            Assert.That(_backend.OverridePathOpenReadCount, Is.Zero);
        }

        [Test]
        public void READ013_NonEmptyTransformIsTerminalAndNeverFallsBack()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteBytes(slot, 2, WithNonEmptyTransform(EncodeSave(slot, 2, "two")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.UnsupportedTransform));
            Assert.That(read.HasResult, Is.False);
        }

        [Test]
        public void READ013B_DeclaredLengthLimitCorruptionRecoversWithoutAllocation()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            byte[] framedLimitViolation = EncodeSave(slot, 2, "two");
            WriteUInt32(
                framedLimitViolation,
                12,
                (uint)SaveGameLimits.MaximumHeaderBytes + 1U);
            RewriteTrailer(framedLimitViolation);
            Assert.That(framedLimitViolation.Length, Is.LessThanOrEqualTo(SaveGameLimits.MaximumEnvelopeBytes));
            WriteBytes(slot, 2, framedLimitViolation);

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.OriginalGeneration.Value, Is.EqualTo(2UL));
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            Assert.That(read.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.LastKnownGood));
        }

        [Test]
        public void READ013C_RawTombstoneFlagOnInvalidRecordIsNotRecoveryBarrier()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            byte[] falseTombstone = EncodeSave(slot, 2, "two");
            falseTombstone[10] = 1;
            RewriteTrailer(falseTombstone);
            WriteBytes(slot, 2, falseTombstone);
            WriteBytes(slot, 3, Corrupt(EncodeSave(slot, 3, "three")));

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Current,
                    SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));

            Assert.That(read.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), read.Error.ToString());
            Assert.That(read.Result.OriginalGeneration.Value, Is.EqualTo(3UL));
            Assert.That(read.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            Assert.That(read.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.LastKnownGood));
        }

        [Test]
        public void READ014_MalformedReservedGenerationFailsClosed()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            File.WriteAllBytes(Path.Combine(SlotPath(slot), "generation-1.mxsave"), new byte[] { 1 });

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        [Test]
        public void READ015_ExactTombstoneReturnsSlotDeleted()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteTombstone(slot, 2);

            ISaveGameOperation<SaveGameReadResult> read = Complete(_factory.CreateStore(null).Read(
                new SaveGameReadRequest(
                    Profile,
                    slot,
                    SaveGameReadSelection.Exact(new SaveGameGeneration(2)))));

            Assert.That(read.Error.Code, Is.EqualTo(SaveGameErrorCode.SlotDeleted));
        }

        [Test]
        public void LIST001_OrdinalListExcludesDeletedAndIncludeDeletedReturnsTombstone()
        {
            WriteSave(new SaveGameSlotId("z"), 1, "z");
            WriteSave(new SaveGameSlotId("a"), 1, "a");
            WriteTombstone(new SaveGameSlotId("m"), 1);
            FileSystemSaveGameStore store = _factory.CreateStore(null);

            ISaveGameOperation<SaveGameListResult> normal = Complete(store.List(new SaveGameListRequest(Profile)));
            ISaveGameOperation<SaveGameListResult> diagnostic = Complete(store.List(
                new SaveGameListRequest(Profile, includeDeleted: true)));

            Assert.That(normal.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), normal.Error.ToString());
            Assert.That(normal.Result.Count, Is.EqualTo(2));
            Assert.That(normal.Result.GetSlot(0).Slot.Value, Is.EqualTo("a"));
            Assert.That(normal.Result.GetSlot(1).Slot.Value, Is.EqualTo("z"));
            Assert.That(diagnostic.Result.Count, Is.EqualTo(3));
            Assert.That(diagnostic.Result.GetSlot(1).Slot.Value, Is.EqualTo("m"));
            Assert.That(diagnostic.Result.GetSlot(1).IsDeleted, Is.True);
            Assert.That(diagnostic.Result.GetSlot(1).UpdatedAtUtc, Is.EqualTo(Time));
        }

        [Test]
        public void LIST002_DeletedHeadVisibilityPreservesDecodedSummaryFields()
        {
            var saveSlot = new SaveGameSlotId("save");
            var deletedSlot = new SaveGameSlotId("deleted");
            WriteSave(saveSlot, 2, "save");
            WriteTombstone(deletedSlot, 3);
            FileSystemSaveGameStore store = _factory.CreateStore(null);

            ISaveGameOperation<SaveGameListResult> normal = Complete(store.List(
                new SaveGameListRequest(Profile)));
            ISaveGameOperation<SaveGameListResult> diagnostic = Complete(store.List(
                new SaveGameListRequest(Profile, includeDeleted: true)));

            Assert.That(normal.Result.Count, Is.EqualTo(1));
            Assert.That(normal.Result.GetSlot(0).Slot, Is.EqualTo(saveSlot));
            Assert.That(diagnostic.Result.Count, Is.EqualTo(2));
            SaveGameSlotSummary deleted = diagnostic.Result.GetSlot(0);
            Assert.That(deleted.Slot, Is.EqualTo(deletedSlot));
            Assert.That(deleted.Generation.Value, Is.EqualTo(3UL));
            Assert.That(deleted.RecordKind, Is.EqualTo(SaveGameRecordKind.Tombstone));
            Assert.That(deleted.UpdatedAtUtc, Is.EqualTo(Time.AddSeconds(2)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LIST003_CorruptOrUnsupportedHeadFailsWholeList(bool unsupported)
        {
            WriteSave(new SaveGameSlotId("a"), 1, "a");
            var badSlot = new SaveGameSlotId("b");
            byte[] bytes = EncodeSave(badSlot, 1, "b");
            WriteBytes(badSlot, 1, unsupported ? UnsupportedVersion(bytes) : Corrupt(bytes));

            ISaveGameOperation<SaveGameListResult> list = Complete(
                _factory.CreateStore(null).List(new SaveGameListRequest(Profile)));

            Assert.That(list.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(list.HasResult, Is.False);
            Assert.That(list.Error.Code, Is.EqualTo(unsupported
                ? SaveGameErrorCode.UnsupportedEnvelopeVersion
                : SaveGameErrorCode.IntegrityFailed));
        }

        [Test]
        public void LIST004_UnsupportedHighestHeadFailsWholeListWithoutFallbackOrPartialResult()
        {
            WriteSave(new SaveGameSlotId("a"), 1, "a");
            var slot = new SaveGameSlotId("b");
            WriteSave(slot, 1, "older");
            WriteBytes(slot, 2, UnsupportedVersion(EncodeSave(slot, 2, "head")));

            ISaveGameOperation<SaveGameListResult> list = Complete(_factory.CreateStore(null).List(
                new SaveGameListRequest(Profile)));

            Assert.That(list.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(list.Error.Code, Is.EqualTo(SaveGameErrorCode.UnsupportedEnvelopeVersion));
            Assert.That(list.HasResult, Is.False);
        }

        [Test]
        public void LIST005_IgnoresNonReservedEntriesAndWriterArtifacts()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            File.WriteAllText(Path.Combine(SlotPath(slot), ".staging-leftover"), "ignored");
            File.WriteAllText(Path.Combine(SlotPath(slot), ".writer.lock"), "ignored");
            File.WriteAllText(Path.Combine(SlotPath(slot), "notes.txt"), "ignored");
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(SlotPath(slot)), "misc"));

            ISaveGameOperation<SaveGameListResult> list = Complete(
                _factory.CreateStore(null).List(new SaveGameListRequest(Profile)));

            Assert.That(list.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), list.Error.ToString());
            Assert.That(list.Result.Count, Is.EqualTo(1));
        }

        [Test]
        public void LIST006_MalformedReservedSlotOrGenerationFailsClosed()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(SlotPath(slot)), "s-NOTHEX"));

            ISaveGameOperation<SaveGameListResult> malformedSlot = Complete(
                _factory.CreateStore(null).List(new SaveGameListRequest(Profile)));
            Directory.Delete(Path.Combine(Path.GetDirectoryName(SlotPath(slot)), "s-NOTHEX"));
            File.WriteAllBytes(Path.Combine(SlotPath(slot), "generation-bad.mxsave"), new byte[] { 1 });
            ISaveGameOperation<SaveGameListResult> malformedGeneration = Complete(
                _factory.CreateStore(null).List(new SaveGameListRequest(Profile)));

            Assert.That(malformedSlot.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidSlot));
            Assert.That(malformedGeneration.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
        }

        [Test]
        public void LIST007_MaximumListedSlotsIsEnforcedBeforeHeadReads()
        {
            for (int index = 0; index <= SaveGameLimits.MaximumListedSlots; index++)
            {
                Directory.CreateDirectory(SlotPath(new SaveGameSlotId("slot-" + index)));
            }

            ISaveGameOperation<SaveGameListResult> list = Complete(
                _factory.CreateStore(null).List(new SaveGameListRequest(Profile)));

            Assert.That(list.Error.Code, Is.EqualTo(SaveGameErrorCode.LimitExceeded));
        }

        [Test]
        public void RET001_PublishRetainsHeadAndNearestValidPrevious()
        {
            FileSystemSaveGameStore store = _factory.CreateStore(null);
            var slot = new SaveGameSlotId("slot");
            AssertCommit(store, slot, 0, "one");
            AssertCommit(store, slot, 1, "two");
            ISaveGameOperation<SaveGameCommitResult> third = AssertCommit(store, slot, 2, "three");

            Assert.That(third.Result.Warning, Is.EqualTo(SaveGameWarning.None));
            AssertGenerations(slot, 2, 3);
        }

        [Test]
        public void RET002_CorruptImmediatePreviousIsSkippedForValidOlderRecord()
        {
            FileSystemSaveGameStore store = _factory.CreateStore(null);
            var slot = new SaveGameSlotId("slot");
            AssertCommit(store, slot, 0, "one");
            AssertCommit(store, slot, 1, "two");
            File.WriteAllBytes(GenerationPath(slot, 2), Corrupt(File.ReadAllBytes(GenerationPath(slot, 2))));

            AssertCommit(store, slot, 2, "three");

            AssertGenerations(slot, 1, 3);
        }

        [Test]
        public void RET003_ValidTombstoneCanBeNearestPreviousRecord()
        {
            FileSystemSaveGameStore store = _factory.CreateStore(null);
            var slot = new SaveGameSlotId("slot");
            AssertCommit(store, slot, 0, "one");
            Complete(store.Delete(new SaveGameDeleteRequest(
                Profile,
                slot,
                new SaveGameGeneration(1),
                Time.AddSeconds(1),
                Build())));
            AssertCommit(store, slot, 2, "three");

            AssertGenerations(slot, 2, 3);
        }

        [Test]
        public void RET004_DeleteFailureAggregatesWarningWithoutReversingCommit()
        {
            FileSystemSaveGameStore store = _factory.CreateStore(null);
            var slot = new SaveGameSlotId("slot");
            AssertCommit(store, slot, 0, "one");
            AssertCommit(store, slot, 1, "two");
            _backend.ThrowGenerationDelete = true;

            ISaveGameOperation<SaveGameCommitResult> third = AssertCommit(store, slot, 2, "three");

            Assert.That(third.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(third.Result.Warning.Code, Is.EqualTo(SaveGameWarningCode.RetentionCleanupFailed));
            Assert.That(third.Result.Warning.Phase, Is.EqualTo(SaveGameOperationPhase.Retention));
            Assert.That(third.Result.Warning.Path, Is.EqualTo("retention"));
            Assert.That(File.Exists(GenerationPath(slot, 3)), Is.True);
        }

        [Test]
        public void RET005_EnumerationFailureReturnsSingleWarningAndPublishedSuccess()
        {
            var hook = new ArmEnumerationFaultHook(_backend);
            FileSystemSaveGameStore store = _factory.CreateStore(hook);
            var slot = new SaveGameSlotId("slot");
            AssertCommit(store, slot, 0, "one");
            hook.Armed = true;

            ISaveGameOperation<SaveGameCommitResult> second = AssertCommit(store, slot, 1, "two");

            Assert.That(second.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(second.Result.Warning, Is.EqualTo(SaveGameWarning.RetentionCleanupFailed));
            Assert.That(File.Exists(GenerationPath(slot, 2)), Is.True);
        }

        [Test]
        public void RET006_DeleteResultCarriesSingularWarningForMultipleCleanupFailures()
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            WriteSave(slot, 2, "two");
            WriteSave(slot, 3, "three");
            _backend.ThrowGenerationDelete = true;
            FileSystemSaveGameStore store = _factory.CreateStore(null);

            ISaveGameOperation<SaveGameDeleteResult> deleted = Complete(store.Delete(
                new SaveGameDeleteRequest(
                    Profile,
                    slot,
                    new SaveGameGeneration(3),
                    Time.AddSeconds(4),
                    Build())));

            Assert.That(deleted.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), deleted.Error.ToString());
            Assert.That(deleted.Result.Warning, Is.EqualTo(SaveGameWarning.RetentionCleanupFailed));
            Assert.That(_backend.GenerationDeleteFailureCount, Is.EqualTo(2));
            Assert.That(File.Exists(GenerationPath(slot, 4)), Is.True);
        }

        [Test]
        public void RET007_ReadUncertaintyPreservesPreviousAndPublishedSuccess(
            [Values(false, true)] bool previousTombstone,
            [Values(false, true)] bool deleteHead,
            [Values(SaveGameErrorCode.QuotaExceeded, SaveGameErrorCode.IoFailure, SaveGameErrorCode.PermissionDenied)] SaveGameErrorCode fault)
        {
            var slot = new SaveGameSlotId("slot");
            FileSystemSaveGameStore store = _factory.CreateStore(null);
            AssertCommit(store, slot, 0, "one");
            if (previousTombstone)
            {
                var deleted = Complete(store.Delete(new SaveGameDeleteRequest(Profile, slot,
                    new SaveGameGeneration(1), Time.AddSeconds(1), Build())));
                Assert.That(deleted.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            }
            else AssertCommit(store, slot, 1, "two");

            _backend.RetentionReadPath = GenerationPath(slot, 2);
            _backend.RetentionPublishedPath = GenerationPath(slot, 3);
            _backend.RetentionReadFault = fault;
            _backend.RetentionReadPending = true;
            SaveGameWarning warning;
            if (deleteHead)
            {
                var head = Complete(store.Delete(new SaveGameDeleteRequest(Profile, slot,
                    new SaveGameGeneration(2), Time.AddSeconds(2), Build())));
                Assert.That(head.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), head.Error.ToString());
                Assert.That(head.Result.Generation.Value, Is.EqualTo(3UL));
                warning = head.Result.Warning;
            }
            else warning = AssertCommit(store, slot, 2, "three").Result.Warning;
            Assert.That(_backend.RetentionReadPending, Is.False, "Fault must occur after generation 3 is published.");
            Assert.That(warning, Is.EqualTo(SaveGameWarning.RetentionCleanupFailed));
            AssertGenerations(slot, 1, 2, 3);

            var exact = Complete(store.Read(new SaveGameReadRequest(Profile, slot,
                SaveGameReadSelection.Exact(new SaveGameGeneration(2)))));
            Assert.That(exact.Error.Code, Is.EqualTo(previousTombstone ? SaveGameErrorCode.SlotDeleted : SaveGameErrorCode.None));
            if (!previousTombstone) Assert.That(exact.Result.SelectedGeneration.Value, Is.EqualTo(2UL));

            // The successful publication remains the CAS head even though retention warned.
            var stale = Complete(store.Commit(new SaveGameCommitRequest(Profile, slot, new SaveGameGeneration(2),
                new SaveGameEncodedEnvelope(EncodeSave(slot, 3, "stale")))));
            Assert.That(stale.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            File.WriteAllBytes(GenerationPath(slot, 3), Corrupt(File.ReadAllBytes(GenerationPath(slot, 3))));
            var recovered = Complete(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current,
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood)));
            if (previousTombstone)
            {
                Assert.That(recovered.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
                Assert.That(recovered.Error.Code, Is.EqualTo(SaveGameErrorCode.IntegrityFailed));
            }
            else
            {
                Assert.That(recovered.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), recovered.Error.ToString());
                Assert.That(recovered.Result.OriginalGeneration.Value, Is.EqualTo(3UL));
                Assert.That(recovered.Result.SelectedGeneration.Value, Is.EqualTo(2UL));
                Assert.That(recovered.Result.RecoverySource, Is.EqualTo(SaveGameRecoverySource.LastKnownGood));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RET008_CorruptOrUnsupportedPreviousStillAllowsBoundedCleanup(bool unsupported)
        {
            var slot = new SaveGameSlotId("slot");
            WriteSave(slot, 1, "one");
            byte[] previous = EncodeSave(slot, 2, "two");
            WriteBytes(slot, 2, unsupported ? UnsupportedVersion(previous) : Corrupt(previous));
            var head = AssertCommit(_factory.CreateStore(null), slot, 2, "three");
            Assert.That(head.Result.Warning, Is.EqualTo(SaveGameWarning.None));
            AssertGenerations(slot, 1, 3);
        }

        private ISaveGameOperation<SaveGameCommitResult> AssertCommit(
            FileSystemSaveGameStore store,
            SaveGameSlotId slot,
            ulong expected,
            string payload)
        {
            ISaveGameOperation<SaveGameCommitResult> operation = Complete(store.Commit(
                new SaveGameCommitRequest(
                    Profile,
                    slot,
                    new SaveGameGeneration(expected),
                    new SaveGameEncodedEnvelope(EncodeSave(slot, expected + 1, payload)))));
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded), operation.Error.ToString());
            return operation;
        }

        private void AssertGenerations(SaveGameSlotId slot, params ulong[] expected)
        {
            string[] files = Directory.GetFiles(SlotPath(slot), "generation-*.mxsave");
            Array.Sort(expected);
            var actual = new ulong[files.Length];
            for (int index = 0; index < files.Length; index++)
            {
                Assert.That(FileSystemGenerationLayout.TryParse(
                    Path.GetFileName(files[index]),
                    out SaveGameGeneration generation,
                    out _), Is.True);
                actual[index] = generation.Value;
            }
            Array.Sort(actual);
            Assert.That(actual, Is.EqualTo(expected));
        }

        private void WriteSave(SaveGameSlotId slot, ulong generation, string payload)
        {
            WriteBytes(slot, generation, EncodeSave(slot, generation, payload));
        }

        private void WriteTombstone(SaveGameSlotId slot, ulong generation)
        {
            SaveGameEnvelope tombstone = SaveGameEnvelope.CreateTombstone(
                Profile,
                slot,
                new SaveGameGeneration(generation),
                Time.AddSeconds(generation - 1),
                Build());
            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(tombstone);
            Assert.That(encoded.IsSuccess, Is.True, encoded.Error.ToString());
            WriteBytes(slot, generation, encoded.Encoded.ToArray());
        }

        private void WriteBytes(SaveGameSlotId slot, ulong generation, byte[] bytes)
        {
            Directory.CreateDirectory(SlotPath(slot));
            File.WriteAllBytes(GenerationPath(slot, generation), bytes);
        }

        private string SlotPath(SaveGameSlotId slot)
        {
            Assert.That(FileSystemSaveGameLayout.TryGetSlotDirectory(
                _root,
                Profile,
                slot,
                BclFileSystemBackend.Instance,
                out string path,
                out SaveGameError error), Is.True, error.ToString());
            return path;
        }

        private string GenerationPath(SaveGameSlotId slot, ulong generation)
        {
            return Path.Combine(
                SlotPath(slot),
                FileSystemGenerationLayout.Format(new SaveGameGeneration(generation)));
        }

        private static byte[] EncodeSave(SaveGameSlotId slot, ulong generation, string payload)
        {
            SaveGameEnvelope envelope = SaveGameEnvelope.CreateSave(
                Profile,
                slot,
                new SaveGameGeneration(generation),
                Time,
                Time,
                Build(),
                Array.Empty<SaveGameKeyValue>(),
                SaveGameEnvelope.RuntimeSaveStateJsonCodecId,
                1,
                0,
                Array.Empty<SaveGameKeyValue>(),
                Encoding.UTF8.GetBytes(payload));
            SaveGameEnvelopeResult encoded = SaveGameEnvelopeCodec.Encode(envelope);
            Assert.That(encoded.IsSuccess, Is.True, encoded.Error.ToString());
            return encoded.Encoded.ToArray();
        }

        private static byte[] Corrupt(byte[] bytes)
        {
            var copy = (byte[])bytes.Clone();
            copy[copy.Length - 1] ^= 0x5a;
            return copy;
        }

        private static byte[] UnsupportedVersion(byte[] bytes)
        {
            var copy = (byte[])bytes.Clone();
            copy[8] = 2;
            copy[9] = 0;
            return copy;
        }

        private static byte[] WithNonEmptyTransform(byte[] source)
        {
            const string oldText = "\"transformIds\":[]";
            const string newText = "\"transformIds\":[\"gzip\"]";
            uint oldHeaderLength = ReadUInt32(source, 12);
            ulong payloadLength = ReadUInt64(source, 16);
            string header = Encoding.UTF8.GetString(source, 24, (int)oldHeaderLength)
                .Replace(oldText, newText);
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            var result = new byte[24 + headerBytes.Length + (int)payloadLength + 32];
            Buffer.BlockCopy(source, 0, result, 0, 24);
            WriteUInt32(result, 12, (uint)headerBytes.Length);
            Buffer.BlockCopy(headerBytes, 0, result, 24, headerBytes.Length);
            Buffer.BlockCopy(
                source,
                24 + (int)oldHeaderLength,
                result,
                24 + headerBytes.Length,
                (int)payloadLength);
            RewriteTrailer(result);
            return result;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset]
                | bytes[offset + 1] << 8
                | bytes[offset + 2] << 16
                | bytes[offset + 3] << 24);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int index = 0; index < 8; index++)
            {
                value |= (ulong)bytes[offset + index] << (index * 8);
            }

            return value;
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            for (int index = 0; index < 4; index++)
            {
                bytes[offset + index] = (byte)(value >> (index * 8));
            }
        }

        private static void RewriteTrailer(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes, 0, bytes.Length - 32);
                Buffer.BlockCopy(hash, 0, bytes, bytes.Length - 32, 32);
            }
        }

        private static SaveGameBuildDescriptor Build()
        {
            return new SaveGameBuildDescriptor("1", "build", "config", "resources", "content");
        }

        private static ISaveGameOperation<TResult> Complete<TResult>(ISaveGameOperation<TResult> operation)
        {
            var worker = (FileSystemSaveGameOperation<TResult>)operation;
            Assert.That(SpinWait.SpinUntil(() => worker.HasWorkerCompletion, 10000), Is.True);
            operation.Poll();
            return operation;
        }

        private sealed class ArmEnumerationFaultHook : IFileSystemSaveGameStoreTestHook
        {
            private readonly TestBackend _backend;
            public ArmEnumerationFaultHook(TestBackend backend) { _backend = backend; }
            public bool Armed { get; set; }
            public void Reach(FileSystemSaveGameStoreHookPoint point, FileSystemSaveGameStoreHookContext context)
            {
                if (Armed && point == FileSystemSaveGameStoreHookPoint.AfterPublication)
                {
                    _backend.ThrowGetEntries = true;
                }
            }
        }

        private sealed class TestBackend : IFileSystemBackend, IFileSystemIoFailureClassifier
        {
            private readonly IFileSystemBackend _inner;
            public TestBackend(IFileSystemBackend inner) { _inner = inner; }
            public string UnauthorizedReadPath { get; set; }
            public string OverrideLengthPath { get; set; }
            public long OverrideLength { get; set; }
            public int OverridePathOpenReadCount { get; private set; }
            public bool ThrowGenerationDelete { get; set; }
            public int GenerationDeleteFailureCount { get; private set; }
            public bool ThrowGetEntries { get; set; }
            public string RetentionReadPath { get; set; }
            public string RetentionPublishedPath { get; set; }
            public SaveGameErrorCode RetentionReadFault { get; set; }
            public bool RetentionReadPending { get; set; }
            public FileSystemIoFailureKind Classify(IOException exception) =>
                RetentionReadFault == SaveGameErrorCode.QuotaExceeded
                    ? FileSystemIoFailureKind.QuotaExceeded : FileSystemIoFailureKind.Unknown;

            public FileSystemEntryStatus GetStatus(string path)
            {
                if (!string.IsNullOrEmpty(OverrideLengthPath)
                    && string.Equals(path, OverrideLengthPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new FileSystemEntryStatus(FileSystemEntryKind.File, OverrideLength);
                }
                return _inner.GetStatus(path);
            }

            public FileSystemDirectoryEntry[] GetEntries(string directoryPath)
            {
                if (ThrowGetEntries) throw new IOException("enumeration failed at " + directoryPath);
                return _inner.GetEntries(directoryPath);
            }

            public Stream OpenRead(string path)
            {
                if (RetentionReadPending && string.Equals(path, RetentionReadPath, StringComparison.Ordinal)
                    && File.Exists(RetentionPublishedPath))
                {
                    RetentionReadPending = false;
                    if (RetentionReadFault == SaveGameErrorCode.PermissionDenied)
                        throw new UnauthorizedAccessException("injected retention read denial");
                    throw new IOException("injected retention read failure");
                }
                if (!string.IsNullOrEmpty(UnauthorizedReadPath)
                    && string.Equals(path, UnauthorizedReadPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("read denied at " + path);
                }
                if (!string.IsNullOrEmpty(OverrideLengthPath)
                    && string.Equals(path, OverrideLengthPath, StringComparison.OrdinalIgnoreCase))
                {
                    OverridePathOpenReadCount++;
                }
                return _inner.OpenRead(path);
            }

            public void DeleteFile(string path)
            {
                if (ThrowGenerationDelete
                    && Path.GetFileName(path).StartsWith("generation-", StringComparison.Ordinal))
                {
                    GenerationDeleteFailureCount++;
                    throw new IOException("delete failed at " + path);
                }
                _inner.DeleteFile(path);
            }

            public void CreateDirectory(string path) => _inner.CreateDirectory(path);
            public Stream OpenCreateNew(string path) => _inner.OpenCreateNew(path);
            public FileSystemExclusiveLockAttempt TryOpenExclusiveLock(string path) => _inner.TryOpenExclusiveLock(path);
            public void FlushDurable(Stream stream) => _inner.FlushDurable(stream);
            public void MoveNoOverwrite(string sourcePath, string destinationPath) => _inner.MoveNoOverwrite(sourcePath, destinationPath);
            public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
        }
    }
}
