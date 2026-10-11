using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.Persistence;
using NUnit.Framework;

namespace MxFramework.Tests.Persistence
{
    public sealed class InMemorySaveGameStoreTests
    {
        private static readonly SaveGameProfileId Profile = new SaveGameProfileId("profile");
        private static readonly DateTime Time = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        [Test]
        public void CompetingCommits_OnlyOneExpectedGenerationWins()
        {
            var store = new InMemorySaveGameStore(); var slot = new SaveGameSlotId("slot");
            var requestA = CommitRequest(slot, SaveGameGeneration.None, "a"); var requestB = CommitRequest(slot, SaveGameGeneration.None, "b");
            var first = store.Commit(requestA); var second = store.Commit(requestB);
            Assert.That(first.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(second.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(second.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
        }

        [Test]
        public void DeleteAndRecreate_NeverReuseGenerationAndCurrentTombstoneIsDeleted()
        {
            var store = new InMemorySaveGameStore(); var slot = new SaveGameSlotId("slot");
            Assert.That(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one")).Result.Generation.Value, Is.EqualTo(1UL));
            var deleted = store.Delete(new SaveGameDeleteRequest(Profile, slot, new SaveGameGeneration(1), Time.AddSeconds(1), SaveGameIdentityAndEnvelopeTests.Build()));
            Assert.That(deleted.Result.Generation.Value, Is.EqualTo(2UL));
            Assert.That(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Error.Code, Is.EqualTo(SaveGameErrorCode.SlotDeleted));
            var retained = store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Exact(new SaveGameGeneration(1)), SaveGameRecoveryPolicy.PrimaryOnly));
            Assert.That(retained.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(retained.Result.OriginalGeneration.Value, Is.EqualTo(2UL));
            Assert.That(retained.Result.SelectedGeneration.Value, Is.EqualTo(1UL));
            var recreated = store.Commit(CommitRequest(slot, new SaveGameGeneration(2), "three"));
            Assert.That(recreated.Result.Generation.Value, Is.EqualTo(3UL));
            Assert.That(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Exact(new SaveGameGeneration(1)))).Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationNotFound));
        }

        [Test]
        public void ReadIncludeDeletedReturnsValidatedTombstoneGenerationForRecreation()
        {
            var store = new InMemorySaveGameStore();
            var slot = new SaveGameSlotId("slot");
            store.Commit(CommitRequest(slot, SaveGameGeneration.None, "one"));
            store.Delete(new SaveGameDeleteRequest(Profile, slot, new SaveGameGeneration(1), Time.AddSeconds(1), SaveGameIdentityAndEnvelopeTests.Build()));
            Assert.AreEqual(SaveGameErrorCode.SlotDeleted,
                store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Error.Code);
            var read = store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current,
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood, includeDeleted: true));
            Assert.IsTrue(read.TryGetResult(out var result), read.Error.ToString());
            Assert.AreEqual(2UL, result.OriginalGeneration.Value);
            Assert.AreEqual(2UL, result.SelectedGeneration.Value);
            Assert.AreEqual(SaveGameRecoverySource.Primary, result.RecoverySource);
            var decoded = SaveGameEnvelopeCodec.Decode(result.Envelope);
            Assert.IsTrue(decoded.IsSuccess, decoded.Error.ToString());
            Assert.AreEqual(SaveGameRecordKind.Tombstone, decoded.Envelope.RecordKind);
            Assert.AreEqual(3UL, store.Commit(CommitRequest(slot, result.OriginalGeneration, "three")).Result.Generation.Value);
        }

        [Test]
        public void List_IsOrdinalAndExcludesDeletedSlots()
        {
            var store = new InMemorySaveGameStore();
            store.Commit(CommitRequest(new SaveGameSlotId("z"), SaveGameGeneration.None, "z"));
            store.Commit(CommitRequest(new SaveGameSlotId("a"), SaveGameGeneration.None, "a"));
            store.Commit(CommitRequest(new SaveGameSlotId("m"), SaveGameGeneration.None, "m"));
            store.Delete(new SaveGameDeleteRequest(Profile, new SaveGameSlotId("m"), new SaveGameGeneration(1), Time.AddSeconds(1), SaveGameIdentityAndEnvelopeTests.Build()));
            var list = store.List(new SaveGameListRequest(Profile)).Result;
            Assert.That(list.Count, Is.EqualTo(2)); Assert.That(list.GetSlot(0).Slot.Value, Is.EqualTo("a")); Assert.That(list.GetSlot(1).Slot.Value, Is.EqualTo("z"));
        }

        [Test]
        public void EncodedBuffers_AreOwnedAcrossCommitAndReadBoundaries()
        {
            var store = new InMemorySaveGameStore(); var slot = new SaveGameSlotId("slot"); var encoded = Encode(slot, SaveGameGeneration.None, "owned");
            var callerCopy = encoded.ToArray(); var request = new SaveGameCommitRequest(Profile, slot, SaveGameGeneration.None, encoded); callerCopy[0] = 0;
            Assert.That(store.Commit(request).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            var firstRead = store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Result.Envelope.ToArray(); firstRead[0] = 0;
            var secondRead = store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Result.Envelope.ToArray();
            Assert.That(secondRead[0], Is.EqualTo((byte)'M'));
        }

        [Test]
        public void PreCancelledOperation_HasNoPublicationSideEffect()
        {
            var store = new InMemorySaveGameStore(); var source = new CancellationTokenSource(); source.Cancel();
            var result = store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value"), source.Token);
            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(store.List(new SaveGameListRequest(Profile)).Result.Count, Is.Zero);
            result.RequestCancel(); result.Poll(); Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
        }

        [Test]
        public void InjectedPublicationFault_PreservesAuthoritativeHead()
        {
            var store = new InMemorySaveGameStore(new OneShotFault()); var slot = new SaveGameSlotId("slot");
            var failed = store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value"));
            Assert.That(failed.Error.Code, Is.EqualTo(SaveGameErrorCode.FaultInjected));
            Assert.That(store.List(new SaveGameListRequest(Profile)).Result.Count, Is.Zero);
        }

        [Test]
        public void OperationResult_IsExplicitlyUnavailableOnFailureOrCancellation()
        {
            var store = new InMemorySaveGameStore();
            var failure = store.Read(new SaveGameReadRequest(Profile, new SaveGameSlotId("missing"), SaveGameReadSelection.Current));
            Assert.That(failure.IsDone, Is.True);
            Assert.That(failure.CanCancel, Is.False);
            Assert.That(failure.HasResult, Is.False);
            Assert.That(failure.TryGetResult(out var failedResult), Is.False);
            Assert.That(failedResult, Is.Null);

            var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var cancelled = store.List(new SaveGameListRequest(Profile), cancellation.Token);
            Assert.That(cancelled.IsDone, Is.True);
            Assert.That(cancelled.CanCancel, Is.False);
            Assert.That(cancelled.HasResult, Is.False);
            Assert.That(cancelled.TryGetResult(out var cancelledResult), Is.False);
            Assert.That(cancelledResult, Is.Null);
        }

        [Test]
        public void Read_RejectsInvalidOrExactGenerationRecoveryPolicy()
        {
            var store = new InMemorySaveGameStore();
            var slot = new SaveGameSlotId("slot");
            Assert.That(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value")).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));

            var exactRecovery = store.Read(new SaveGameReadRequest(
                Profile,
                slot,
                SaveGameReadSelection.Exact(new SaveGameGeneration(1)),
                SaveGameRecoveryPolicy.PrimaryThenLastKnownGood));
            Assert.That(exactRecovery.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidSelection));

            var invalidRecovery = store.Read(new SaveGameReadRequest(
                Profile,
                slot,
                SaveGameReadSelection.Current,
                (SaveGameRecoveryPolicy)99));
            Assert.That(invalidRecovery.Error.Code, Is.EqualTo(SaveGameErrorCode.InvalidSelection));
        }

        [Test]
        public void CompletedOperations_ReportTerminalStateAndPollWithoutAllocating()
        {
            var store = new InMemorySaveGameStore();
            ISaveGameOperation<SaveGameListResult> operation = store.List(new SaveGameListRequest(Profile));
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(operation.IsDone, Is.True);
            Assert.That(operation.CanCancel, Is.False);

            operation.Poll();
            operation.RequestCancel();
            _ = operation.TryGetResult(out _);
            _ = GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var observed = false;
            for (var index = 0; index < 1024; index++)
            {
                operation.Poll();
                operation.RequestCancel();
                observed ^= operation.IsDone;
                observed ^= operation.CanCancel;
                observed ^= operation.HasResult;
                observed ^= operation.TryGetResult(out _);
                _ = operation.Status;
                _ = operation.Progress;
                _ = operation.Error;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(observed, Is.False);
            Assert.That(allocated, Is.EqualTo(0L));
        }

        [Test]
        public void CommitAndDeleteSuccess_AreWarningFreeInMemory()
        {
            var store = new InMemorySaveGameStore();
            var slot = new SaveGameSlotId("warning-free");
            var committed = store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value"));
            Assert.That(committed.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(committed.Result.Warning, Is.EqualTo(SaveGameWarning.None));

            var deleted = store.Delete(new SaveGameDeleteRequest(
                Profile,
                slot,
                committed.Result.Generation,
                Time.AddSeconds(1),
                SaveGameIdentityAndEnvelopeTests.Build()));
            Assert.That(deleted.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(deleted.Result.Warning, Is.EqualTo(SaveGameWarning.None));
        }

        [Test]
        public void CancellationLatchedByFaultSeam_PreventsCommitPublication()
        {
            var cancellation = new CancellationTokenSource();
            var store = new InMemorySaveGameStore(new CancellingFault(cancellation, InMemorySaveGameFaultPoint.BeforeCommitPublication));
            var result = store.Commit(CommitRequest(new SaveGameSlotId("slot"), SaveGameGeneration.None, "value"), cancellation.Token);
            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(store.List(new SaveGameListRequest(Profile)).Result.Count, Is.Zero);
        }

        [Test]
        public void CancellationLatchedByFaultSeam_PreventsDeletePublication()
        {
            var cancellation = new CancellationTokenSource();
            var store = new InMemorySaveGameStore(new CancellingFault(cancellation, InMemorySaveGameFaultPoint.BeforeDeletePublication));
            var slot = new SaveGameSlotId("slot");
            Assert.That(store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value")).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            var result = store.Delete(new SaveGameDeleteRequest(Profile, slot, new SaveGameGeneration(1), Time.AddSeconds(1), SaveGameIdentityAndEnvelopeTests.Build()), cancellation.Token);
            Assert.That(result.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
            Assert.That(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
        }

        [Test]
        public void ReentrantFaultWriter_WinsAndOuterCasDetectsConflict()
        {
            var fault = new ReentrantWriterFault();
            var store = new InMemorySaveGameStore(fault); fault.Store = store;
            var slot = new SaveGameSlotId("slot"); fault.Request = CommitRequest(slot, SaveGameGeneration.None, "inner");
            var outer = store.Commit(CommitRequest(slot, SaveGameGeneration.None, "outer"));
            Assert.That(outer.Error.Code, Is.EqualTo(SaveGameErrorCode.GenerationConflict));
            var current = store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current));
            Assert.That(current.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(Encoding.UTF8.GetString(SaveGameEnvelopeCodec.Decode(current.Result.Envelope).Envelope.CopyPayload()), Is.EqualTo("inner"));
        }

        [Test]
        public void ConcurrentCasWriters_ProduceExactlyOneWinner()
        {
            var store = new InMemorySaveGameStore(); var slot = new SaveGameSlotId("slot");
            var start = new ManualResetEventSlim(false); var tasks = new Task<ISaveGameOperation<SaveGameCommitResult>>[8];
            for (var index = 0; index < tasks.Length; index++)
            {
                var request = CommitRequest(slot, SaveGameGeneration.None, "writer-" + index);
                tasks[index] = Task.Run(() => { start.Wait(); return store.Commit(request); });
            }
            start.Set(); Task.WaitAll(tasks);
            var succeeded = 0;
            for (var index = 0; index < tasks.Length; index++) if (tasks[index].Result.Status == SaveGameOperationStatus.Succeeded) succeeded++;
            Assert.That(succeeded, Is.EqualTo(1));
            Assert.That(store.Read(new SaveGameReadRequest(Profile, slot, SaveGameReadSelection.Current)).Result.SelectedGeneration.Value, Is.EqualTo(1UL));
        }

        [Test]
        public void DiagnosticList_IncludesTombstonesWithoutChangingDefaultList()
        {
            var store = new InMemorySaveGameStore(); var slot = new SaveGameSlotId("deleted");
            store.Commit(CommitRequest(slot, SaveGameGeneration.None, "value"));
            store.Delete(new SaveGameDeleteRequest(Profile, slot, new SaveGameGeneration(1), Time.AddSeconds(1), SaveGameIdentityAndEnvelopeTests.Build()));
            Assert.That(store.List(new SaveGameListRequest(Profile)).Result.Count, Is.Zero);
            var diagnostic = store.List(new SaveGameListRequest(Profile, includeDeleted: true)).Result;
            Assert.That(diagnostic.Count, Is.EqualTo(1));
            Assert.That(diagnostic.GetSlot(0).IsDeleted, Is.True);
            Assert.That(diagnostic.GetSlot(0).Generation.Value, Is.EqualTo(2UL));
        }

        private static SaveGameCommitRequest CommitRequest(SaveGameSlotId slot, SaveGameGeneration expected, string payload) => new SaveGameCommitRequest(Profile, slot, expected, Encode(slot, expected, payload));
        private static SaveGameEncodedEnvelope Encode(SaveGameSlotId slot, SaveGameGeneration expected, string payload)
        {
            Assert.That(expected.TryNext(out var next), Is.True);
            var envelope = SaveGameEnvelope.CreateSave(Profile, slot, next, Time, Time, SaveGameIdentityAndEnvelopeTests.Build(), Array.Empty<SaveGameKeyValue>(), SaveGameEnvelope.RuntimeSaveStateJsonCodecId, 1, 0, Array.Empty<SaveGameKeyValue>(), Encoding.UTF8.GetBytes(payload));
            var encoded = SaveGameEnvelopeCodec.Encode(envelope); Assert.That(encoded.IsSuccess, Is.True, encoded.Error.ToString()); return encoded.Encoded;
        }

        private sealed class OneShotFault : IInMemorySaveGameStoreFaultInjector
        {
            public SaveGameError Inject(InMemorySaveGameFaultPoint point, SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation) => new SaveGameError(SaveGameErrorCode.FaultInjected, SaveGameOperationPhase.Publication, "fault", "Injected test fault.");
        }

        private sealed class CancellingFault : IInMemorySaveGameStoreFaultInjector
        {
            private readonly CancellationTokenSource _cancellation;
            private readonly InMemorySaveGameFaultPoint _point;
            public CancellingFault(CancellationTokenSource cancellation, InMemorySaveGameFaultPoint point) { _cancellation = cancellation; _point = point; }
            public SaveGameError Inject(InMemorySaveGameFaultPoint point, SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation) { if (point == _point) _cancellation.Cancel(); return SaveGameError.None; }
        }

        private sealed class ReentrantWriterFault : IInMemorySaveGameStoreFaultInjector
        {
            private int _entered;
            public InMemorySaveGameStore Store;
            public SaveGameCommitRequest Request;
            public SaveGameError Inject(InMemorySaveGameFaultPoint point, SaveGameProfileId profile, SaveGameSlotId slot, SaveGameGeneration generation)
            {
                if (Interlocked.Exchange(ref _entered, 1) == 0) Assert.That(Store.Commit(Request).Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
                return SaveGameError.None;
            }
        }
    }
}
