using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using MxFramework.Persistence;
using MxFramework.Persistence.FileSystem;
using NUnit.Framework;

namespace MxFramework.Tests.PersistenceFileSystem
{
    public sealed class FileSystemOperationAndGenerationTests
    {
        [TestCase(1, 39, 1)]
        [TestCase(1, 112, 1)]
        [TestCase(1, 28, 0)]
        [TestCase(2, 28, 1)]
        [TestCase(2, 122, 1)]
        [TestCase(2, 69, 0)]
        [TestCase(3, 28, 1)]
        [TestCase(3, 69, 1)]
        [TestCase(3, 122, 0)]
        [TestCase(0, 112, 0)]
        public void BclBackend_QuotaClassifierUsesPlatformNativeCodes(
            int platformValue,
            int nativeCode,
            int expectedValue)
        {
            FileSystemIoFailureKind actual = BclFileSystemBackend.ClassifyIoFailure(
                new SyntheticIOException(nativeCode),
                (FileSystemLockErrorPlatform)platformValue);

            Assert.That(actual, Is.EqualTo((FileSystemIoFailureKind)expectedValue));
        }

        [Test]
        public void PublicationGate_StableStatesAndSingleWinnerAreLinearized()
        {
            Assert.That((int)FileSystemPublicationGateState.Active, Is.Zero);
            Assert.That((int)FileSystemPublicationGateState.CancelRequested, Is.EqualTo(1));
            Assert.That((int)FileSystemPublicationGateState.Publishing, Is.EqualTo(2));
            Assert.That((int)FileSystemPublicationGateState.Sealed, Is.EqualTo(3));

            var cancelled = new FileSystemPublicationGate();
            Assert.That(cancelled.RequestCancel(), Is.True);
            Assert.That(cancelled.RequestCancel(), Is.False);
            Assert.That(cancelled.TryEnterPublishing(), Is.False);
            Assert.That(cancelled.State, Is.EqualTo(FileSystemPublicationGateState.CancelRequested));
            Assert.That(cancelled.CanCancel, Is.False);

            var publishing = new FileSystemPublicationGate();
            Assert.That(publishing.TryEnterPublishing(), Is.True);
            Assert.That(publishing.RequestCancel(), Is.False);
            Assert.That(publishing.TryEnterPublishing(), Is.False);
            Assert.That(publishing.State, Is.EqualTo(FileSystemPublicationGateState.Publishing));
            Assert.That(publishing.CanCancel, Is.False);
        }

        [Test]
        public void PublicationGate_RacingCancelAndSealLinearizesWorkerOutcome()
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var gate = new FileSystemPublicationGate();
                var start = new ManualResetEventSlim(false);
                bool cancelWon = false;
                bool cancellationObserved = false;
                var cancelThread = new Thread(() => { start.Wait(); cancelWon = gate.RequestCancel(); });
                var sealThread = new Thread(() => { start.Wait(); gate.Seal(out cancellationObserved); });
                cancelThread.Start();
                sealThread.Start();
                start.Set();
                cancelThread.Join();
                sealThread.Join();
                start.Dispose();

                Assert.That(cancellationObserved, Is.EqualTo(cancelWon));
                Assert.That(gate.State, Is.EqualTo(FileSystemPublicationGateState.Sealed));
                Assert.That(gate.RequestCancel(), Is.False);
            }
        }

        [Test]
        public void PublicationGate_RacingCancelAndPublishHaveExactlyOneWinner()
        {
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var gate = new FileSystemPublicationGate();
                var start = new ManualResetEventSlim(false);
                bool cancelWon = false;
                bool publishWon = false;
                var cancelThread = new Thread(() => { start.Wait(); cancelWon = gate.RequestCancel(); });
                var publishThread = new Thread(() => { start.Wait(); publishWon = gate.TryEnterPublishing(); });
                cancelThread.Start();
                publishThread.Start();
                start.Set();
                cancelThread.Join();
                publishThread.Join();
                start.Dispose();

                Assert.That(cancelWon ^ publishWon, Is.True);
                Assert.That(
                    gate.State,
                    Is.EqualTo(cancelWon
                        ? FileSystemPublicationGateState.CancelRequested
                        : FileSystemPublicationGateState.Publishing));
            }
        }

        [Test]
        public void Operation_WorkerCompletionRequiresOwnerPollForTerminalPromotion()
        {
            var capacity = new FileSystemWorkerCapacity();
            var release = new ManualResetEventSlim(false);
            FileSystemSaveGameOperation<int> operation = FileSystemSaveGameOperation<int>.Start(
                new SaveGameOperationId("operation-owner"),
                capacity,
                context =>
                {
                    context.ReportProgress(500);
                    release.Wait();
                    return context.TryEnterPublishing()
                        ? FileSystemOperationCompletion<int>.Success(42)
                        : FileSystemOperationCompletion<int>.Cancelled();
                });

            Assert.That(SpinWait.SpinUntil(() => operation.Status == SaveGameOperationStatus.Running, 5000), Is.True);
            Assert.That(operation.IsDone, Is.False);
            Assert.That(SpinWait.SpinUntil(() => operation.Progress == 0.5f, 5000), Is.True);
            Assert.That(operation.Progress, Is.EqualTo(0.5f));
            release.Set();
            Assert.That(SpinWait.SpinUntil(() => operation.HasWorkerCompletion, 5000), Is.True);
            Assert.That(operation.IsDone, Is.False);

            operation.Poll();

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(operation.IsDone, Is.True);
            Assert.That(operation.CanCancel, Is.False);
            Assert.That(operation.TryGetResult(out int result), Is.True);
            Assert.That(result, Is.EqualTo(42));
            Assert.That(operation.Progress, Is.EqualTo(1f));
            release.Dispose();
        }

        [Test]
        public void Operation_OffOwnerPollIsNoOp()
        {
            var capacity = new FileSystemWorkerCapacity();
            FileSystemSaveGameOperation<int> operation = StartImmediateSuccess("operation-off-owner", capacity, 7);
            Assert.That(SpinWait.SpinUntil(() => operation.HasWorkerCompletion, 5000), Is.True);
            var pollThread = new Thread(operation.Poll);

            pollThread.Start();
            pollThread.Join();

            Assert.That(operation.IsDone, Is.False);
            operation.Poll();
            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(operation.Result, Is.EqualTo(7));
        }

        [Test]
        public void Operation_LateCancelAfterPublishingCannotReplaceSuccess()
        {
            var capacity = new FileSystemWorkerCapacity();
            var publishing = new ManualResetEventSlim(false);
            var finish = new ManualResetEventSlim(false);
            FileSystemSaveGameOperation<int> operation = FileSystemSaveGameOperation<int>.Start(
                new SaveGameOperationId("operation-late-cancel"),
                capacity,
                context =>
                {
                    if (!context.TryEnterPublishing()) return FileSystemOperationCompletion<int>.Cancelled();
                    publishing.Set();
                    finish.Wait();
                    return FileSystemOperationCompletion<int>.Success(99);
                });

            Assert.That(publishing.Wait(5000), Is.True);
            Assert.That(operation.CanCancel, Is.False);
            operation.RequestCancel();
            Assert.That(operation.PublicationGate.State, Is.EqualTo(FileSystemPublicationGateState.Publishing));
            finish.Set();
            Assert.That(SpinWait.SpinUntil(() => operation.HasWorkerCompletion, 5000), Is.True);
            Assert.That(operation.PublicationGate.State, Is.EqualTo(FileSystemPublicationGateState.Sealed));
            operation.Poll();

            Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            Assert.That(operation.Result, Is.EqualTo(99));
            publishing.Dispose();
            finish.Dispose();
        }

        [Test]
        public void WorkerCapacity_ReleasesInWorkerFinallyWhenCallersNeverPoll()
        {
            var capacity = new FileSystemWorkerCapacity();
            var release = new ManualResetEventSlim(false);
            var operations = new FileSystemSaveGameOperation<int>[SaveGameLimits.MaximumConcurrentOperations];
            for (int index = 0; index < operations.Length; index++)
            {
                int result = index;
                operations[index] = FileSystemSaveGameOperation<int>.Start(
                    new SaveGameOperationId("operation-capacity-" + index),
                    capacity,
                    context =>
                    {
                        release.Wait();
                        return context.TryEnterPublishing()
                            ? FileSystemOperationCompletion<int>.Success(result)
                            : FileSystemOperationCompletion<int>.Cancelled();
                    });
            }

            Assert.That(capacity.ActiveCount, Is.EqualTo(SaveGameLimits.MaximumConcurrentOperations));
            FileSystemSaveGameOperation<int> rejected = StartImmediateSuccess(
                "operation-capacity-rejected",
                capacity,
                100);
            rejected.Poll();
            Assert.That(rejected.Status, Is.EqualTo(SaveGameOperationStatus.Failed));
            Assert.That(rejected.Error.Code, Is.EqualTo(SaveGameErrorCode.Busy));

            release.Set();
            Assert.That(SpinWait.SpinUntil(() => capacity.ActiveCount == 0, 5000), Is.True);
            for (int index = 0; index < operations.Length; index++)
            {
                Assert.That(operations[index].IsDone, Is.False, "Caller did not poll operation " + index + ".");
            }

            FileSystemSaveGameOperation<int> next = StartImmediateSuccess(
                "operation-capacity-next",
                capacity,
                101);
            Assert.That(SpinWait.SpinUntil(() => next.HasWorkerCompletion, 5000), Is.True);
            next.Poll();
            Assert.That(next.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            release.Dispose();
        }

        [Test]
        public void Operation_TerminalPollGettersAndCancelAllocateZeroBytes()
        {
            var capacity = new FileSystemWorkerCapacity();
            FileSystemSaveGameOperation<int> operation = StartImmediateSuccess("operation-noalloc", capacity, 3);
            Assert.That(SpinWait.SpinUntil(() => operation.HasWorkerCompletion, 5000), Is.True);
            operation.Poll();
            int sink = 0;
            for (int index = 0; index < 32; index++)
            {
                ExerciseTerminal(operation, ref sink);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                ExerciseTerminal(operation, ref sink);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.Zero);
            Assert.That(sink, Is.Not.Zero);
        }

        [Test]
        public void GenerationNames_AreStrictCaseSensitiveAndNumericallyOrdered()
        {
            Assert.That(
                FileSystemGenerationLayout.Format(new SaveGameGeneration(1)),
                Is.EqualTo("generation-00000000000000000001.mxsave"));
            Assert.That(
                FileSystemGenerationLayout.Format(new SaveGameGeneration(ulong.MaxValue)),
                Is.EqualTo("generation-18446744073709551615.mxsave"));
            Assert.That(
                FileSystemGenerationLayout.TryParse(
                    "generation-00000000000000000010.mxsave",
                    out SaveGameGeneration ten,
                    out bool malformed),
                Is.True);
            Assert.That(ten.Value, Is.EqualTo(10UL));
            Assert.That(malformed, Is.False);

            AssertMalformed("Generation-00000000000000000010.mxsave");
            AssertMalformed("generation-00000000000000000010.MXSAVE");
            AssertMalformed("generation-00000000000000000000.mxsave");
            AssertMalformed("generation-0000000000000000001x.mxsave");
            Assert.That(
                FileSystemGenerationLayout.TryParse("unrelated.txt", out _, out bool unrelatedMalformed),
                Is.False);
            Assert.That(unrelatedMalformed, Is.False);

            var entries = new[]
            {
                Entry("generation-00000000000000000009.mxsave"),
                Entry(".staging-abc"),
                Entry("generation-00000000000000000100.mxsave"),
                Entry("generation-00000000000000000010.mxsave")
            };
            Assert.That(
                FileSystemGenerationLayout.TryFindHighest(
                    entries,
                    SaveGameOperationPhase.Listing,
                    out SaveGameGeneration highest,
                    out SaveGameError error),
                Is.True,
                error.ToString());
            Assert.That(highest.Value, Is.EqualTo(100UL));
        }

        [Test]
        public void GenerationScan_MalformedReservedNameOrNonFileFailsClosed()
        {
            var malformedEntries = new[]
            {
                Entry("generation-00000000000000000002.mxsave"),
                Entry("generation-bad.mxsave")
            };
            Assert.That(
                FileSystemGenerationLayout.TryFindHighest(
                    malformedEntries,
                    SaveGameOperationPhase.Listing,
                    out _,
                    out SaveGameError malformedError),
                Is.False);
            Assert.That(malformedError.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
            Assert.That(malformedError.Path, Is.EqualTo("generation"));
            Assert.That(malformedError.Phase, Is.EqualTo(SaveGameOperationPhase.Listing));

            var directoryEntry = new[]
            {
                new FileSystemDirectoryEntry(
                    "generation-00000000000000000002.mxsave",
                    FileSystemEntryKind.Directory,
                    0)
            };
            Assert.That(
                FileSystemGenerationLayout.TryFindHighest(
                    directoryEntry,
                    SaveGameOperationPhase.Read,
                    out _,
                    out SaveGameError kindError),
                Is.False);
            Assert.That(kindError.Code, Is.EqualTo(SaveGameErrorCode.InvalidEnvelope));
            Assert.That(kindError.Phase, Is.EqualTo(SaveGameOperationPhase.Read));
        }

        [Test]
        public void SlotWriterLease_SerializesSameSlotAndReleasesIdempotently()
        {
            var coordinator = new FileSystemSlotWriterLeaseCoordinator();
            string firstPath = Path.Combine(Path.GetTempPath(), "mx-slot-lease", "slot-a");
            string otherPath = Path.Combine(Path.GetTempPath(), "mx-slot-lease", "slot-b");
            Assert.That(coordinator.TryAcquire(firstPath, out FileSystemSlotWriterLease first), Is.True);
            Assert.That(coordinator.TryAcquire(firstPath, out _), Is.False);
            Assert.That(coordinator.TryAcquire(otherPath, out FileSystemSlotWriterLease other), Is.True);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(coordinator.TryAcquire(firstPath.ToUpperInvariant(), out _), Is.False);
            }

            first.Dispose();
            first.Dispose();
            Assert.That(coordinator.TryAcquire(firstPath, out FileSystemSlotWriterLease reacquired), Is.True);
            reacquired.Dispose();
            other.Dispose();
        }

        [Test]
        public void SlotWriterLease_WaitsWithoutTimeoutAndCancellationUnblocksWaiter()
        {
            var coordinator = new FileSystemSlotWriterLeaseCoordinator();
            string slotPath = Path.Combine(Path.GetTempPath(), "mx-slot-lease", "slot-wait");
            Assert.That(coordinator.TryAcquire(slotPath, out FileSystemSlotWriterLease first), Is.True);
            var acquired = new ManualResetEventSlim(false);
            FileSystemSlotWriterLease waitedLease = null;
            var waiter = new Thread(() =>
            {
                if (coordinator.WaitAcquire(slotPath, CancellationToken.None, out waitedLease))
                {
                    acquired.Set();
                }
            });
            waiter.Start();
            Assert.That(acquired.Wait(50), Is.False);
            first.Dispose();
            Assert.That(acquired.Wait(5000), Is.True);
            waiter.Join();
            waitedLease.Dispose();

            Assert.That(coordinator.TryAcquire(slotPath, out FileSystemSlotWriterLease held), Is.True);
            using (var cancellation = new CancellationTokenSource())
            {
                bool result = true;
                var cancelledWaiter = new Thread(() =>
                    result = coordinator.WaitAcquire(slotPath, cancellation.Token, out _));
                cancelledWaiter.Start();
                cancellation.Cancel();
                cancelledWaiter.Join();
                Assert.That(result, Is.False);
            }

            held.Dispose();
            acquired.Dispose();
        }

        [Test]
        public void SlotWriterLease_OperationCancelUnblocksWithoutReleasingCurrentWriter()
        {
            var coordinator = new FileSystemSlotWriterLeaseCoordinator();
            var capacity = new FileSystemWorkerCapacity();
            string slotPath = Path.Combine(Path.GetTempPath(), "mx-slot-lease", "slot-operation-cancel");
            Assert.That(coordinator.TryAcquire(slotPath, out FileSystemSlotWriterLease first), Is.True);
            var waiting = new ManualResetEventSlim(false);

            try
            {
                FileSystemSaveGameOperation<int> operation = FileSystemSaveGameOperation<int>.Start(
                    new SaveGameOperationId("operation-slot-wait-cancel"),
                    capacity,
                    context =>
                    {
                        waiting.Set();
                        if (!coordinator.WaitAcquire(
                                slotPath,
                                context.PublicationGate,
                                CancellationToken.None,
                                out FileSystemSlotWriterLease lease))
                        {
                            return FileSystemOperationCompletion<int>.Cancelled();
                        }

                        lease.Dispose();
                        return FileSystemOperationCompletion<int>.Success(1);
                    });

                Assert.That(waiting.Wait(5000), Is.True);
                operation.RequestCancel();
                Assert.That(SpinWait.SpinUntil(() => operation.HasWorkerCompletion, 5000), Is.True);
                operation.Poll();

                Assert.That(operation.Status, Is.EqualTo(SaveGameOperationStatus.Cancelled));
                Assert.That(coordinator.TryAcquire(slotPath, out _), Is.False,
                    "Cancelling the waiter must not release the current writer's lease.");
            }
            finally
            {
                first.Dispose();
                waiting.Dispose();
            }
        }

        [Test]
        public void BclBackend_EnumeratesTypedEntriesAndOpensExclusiveLock()
        {
            string root = Path.Combine(Path.GetTempPath(), "mx-backend-b1-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string directory = Path.Combine(root, "directory");
            string file = Path.Combine(root, "file");
            string lockPath = Path.Combine(root, ".writer.lock");
            Directory.CreateDirectory(directory);
            using (Stream stream = BclFileSystemBackend.Instance.OpenCreateNew(file))
            {
                stream.WriteByte(1);
            }

            try
            {
                FileSystemDirectoryEntry[] entries = BclFileSystemBackend.Instance.GetEntries(root);
                Assert.That(Array.Exists(entries, entry => entry.Name == "directory" && entry.Kind == FileSystemEntryKind.Directory), Is.True);
                Assert.That(Array.Exists(entries, entry => entry.Name == "file" && entry.Kind == FileSystemEntryKind.File && entry.Length == 1), Is.True);
                FileSystemExclusiveLockAttempt first = BclFileSystemBackend.Instance.TryOpenExclusiveLock(lockPath);
                Assert.That(first.Status, Is.EqualTo(FileSystemExclusiveLockAttemptStatus.Acquired));
                Assert.That(first.Stream, Is.Not.Null);
                using (first.Stream)
                {
                    FileSystemExclusiveLockAttempt second = BclFileSystemBackend.Instance.TryOpenExclusiveLock(lockPath);
                    Assert.That(second.Status, Is.EqualTo(FileSystemExclusiveLockAttemptStatus.Contended));
                    Assert.That(second.Stream, Is.Null);
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void BclBackend_SharingViolationClassifierAcceptsOnlyPlatformCode()
        {
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x80070020)),
                FileSystemLockErrorPlatform.Windows), Is.True);
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x80070021)),
                FileSystemLockErrorPlatform.Windows), Is.True);
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x8007000B)),
                FileSystemLockErrorPlatform.LinuxOrAndroid), Is.True);
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x80070023)),
                FileSystemLockErrorPlatform.AppleOrBsd), Is.True);
        }

        [Test]
        public void BclBackend_SharingViolationClassifierRejectsOtherOrMissingErrors()
        {
            var other = new SyntheticIOException(unchecked((int)0x80070005));

            Assert.That(BclFileSystemBackend.IsSharingViolation(other, FileSystemLockErrorPlatform.Windows), Is.False);
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x80070020)),
                FileSystemLockErrorPlatform.LinuxOrAndroid), Is.False);
            Assert.That(BclFileSystemBackend.IsSharingViolation(null, FileSystemLockErrorPlatform.Windows), Is.False);
            Assert.That(BclFileSystemBackend.IsSharingViolation(
                new SyntheticIOException(unchecked((int)0x8007000B)),
                FileSystemLockErrorPlatform.Unknown), Is.False);
        }

        private static FileSystemSaveGameOperation<int> StartImmediateSuccess(
            string operationId,
            FileSystemWorkerCapacity capacity,
            int result)
        {
            return FileSystemSaveGameOperation<int>.Start(
                new SaveGameOperationId(operationId),
                capacity,
                context => context.TryEnterPublishing()
                    ? FileSystemOperationCompletion<int>.Success(result)
                    : FileSystemOperationCompletion<int>.Cancelled());
        }

        private static void ExerciseTerminal(FileSystemSaveGameOperation<int> operation, ref int sink)
        {
            operation.Poll();
            operation.RequestCancel();
            unchecked
            {
                sink += (int)operation.Status;
                sink += operation.IsDone ? 1 : 0;
                sink += operation.CanCancel ? 1 : 0;
                sink += operation.HasResult ? 1 : 0;
                sink += operation.Result;
                sink += (int)operation.Error.Code;
                sink += (int)(operation.Progress * 1000f);
                if (operation.TryGetResult(out int result)) sink += result;
            }
        }

        private static FileSystemDirectoryEntry Entry(string name) =>
            new FileSystemDirectoryEntry(name, FileSystemEntryKind.File, 1);

        private sealed class SyntheticIOException : IOException
        {
            public SyntheticIOException(int hResult)
            {
                HResult = hResult;
            }
        }

        private static void AssertMalformed(string name)
        {
            Assert.That(
                FileSystemGenerationLayout.TryParse(name, out _, out bool malformed),
                Is.False);
            Assert.That(malformed, Is.True);
        }
    }
}
