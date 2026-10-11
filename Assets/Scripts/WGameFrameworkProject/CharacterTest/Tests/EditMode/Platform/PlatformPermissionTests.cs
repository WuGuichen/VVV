using System;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.Platform
{
    public sealed class PlatformPermissionTests
    {
        [Test]
        public void PermissionValues_AreStableAndDefaultIsNotPending()
        {
            Assert.AreEqual(0, (int)PlatformPermissionStatus.Unknown);
            Assert.AreEqual(1, (int)PlatformPermissionStatus.Unsupported);
            Assert.AreEqual(2, (int)PlatformPermissionStatus.NotRequired);
            Assert.AreEqual(3, (int)PlatformPermissionStatus.NotDetermined);
            Assert.AreEqual(4, (int)PlatformPermissionStatus.Granted);
            Assert.AreEqual(5, (int)PlatformPermissionStatus.Denied);
            Assert.AreEqual(6, (int)PlatformPermissionStatus.Restricted);

            Assert.AreEqual(0, (int)PlatformPermissionOperationStatus.Unknown);
            Assert.AreEqual(1, (int)PlatformPermissionOperationStatus.Pending);
            Assert.AreEqual(2, (int)PlatformPermissionOperationStatus.Succeeded);
            Assert.AreEqual(3, (int)PlatformPermissionOperationStatus.Failed);
            Assert.AreEqual(4, (int)PlatformPermissionOperationStatus.Cancelled);
            Assert.AreEqual(
                PlatformPermissionOperationStatus.Unknown,
                default(PlatformPermissionOperationStatus));
        }

        [Test]
        public void PermissionId_ZeroIsInvalidAndMicrophoneIsOne()
        {
            Assert.IsFalse(default(PlatformPermissionId).IsValid);
            Assert.IsFalse(new PlatformPermissionId(-1).IsValid);
            Assert.IsTrue(PlatformPermissionId.Microphone.IsValid);
            Assert.AreEqual(1, PlatformPermissionId.Microphone.Value);
        }

        [Test]
        public void QueryResult_UnsupportedIsTrustedButFailureIsUnknown()
        {
            PlatformPermissionQueryResult unsupported =
                PlatformPermissionQueryResult.Ok(PlatformPermissionStatus.Unsupported);
            PlatformPermissionQueryResult invalid = PlatformPermissionQueryResult.Fail(
                new PlatformError(PlatformErrorCode.InvalidArgument, "Invalid permission id."));

            Assert.IsTrue(unsupported.Success);
            Assert.AreEqual(PlatformPermissionStatus.Unsupported, unsupported.Status);
            Assert.IsTrue(unsupported.Error.IsNone);

            Assert.IsFalse(invalid.Success);
            Assert.AreEqual(PlatformPermissionStatus.Unknown, invalid.Status);
            Assert.AreEqual(PlatformErrorCode.InvalidArgument, invalid.Error.Code);
        }

        [TestCase(PlatformPermissionStatus.Granted)]
        [TestCase(PlatformPermissionStatus.NotRequired)]
        public void CompletedSuccess_HasOnlySuccessfulTerminalFacts(PlatformPermissionStatus status)
        {
            CompletedPlatformPermissionOperation operation =
                CompletedPlatformPermissionOperation.Succeeded(status);

            Assert.AreEqual(PlatformPermissionOperationStatus.Succeeded, operation.OperationStatus);
            Assert.AreEqual(status, operation.PermissionStatus);
            Assert.IsTrue(operation.IsDone);
            Assert.IsFalse(operation.CanCancel);
            Assert.IsTrue(operation.Error.IsNone);

            operation.RequestCancel();
            Assert.AreEqual(PlatformPermissionOperationStatus.Succeeded, operation.OperationStatus);
        }

        [TestCase(PlatformPermissionStatus.Denied, PlatformErrorCode.PermissionDenied)]
        [TestCase(PlatformPermissionStatus.Restricted, PlatformErrorCode.PermissionRestricted)]
        [TestCase(PlatformPermissionStatus.Unsupported, PlatformErrorCode.Unsupported)]
        [TestCase(PlatformPermissionStatus.Unknown, PlatformErrorCode.Busy)]
        public void CompletedFailure_PreservesTerminalFactAndError(
            PlatformPermissionStatus status,
            PlatformErrorCode errorCode)
        {
            CompletedPlatformPermissionOperation operation =
                CompletedPlatformPermissionOperation.Failed(
                    status,
                    new PlatformError(errorCode, "terminal"));

            Assert.AreEqual(PlatformPermissionOperationStatus.Failed, operation.OperationStatus);
            Assert.AreEqual(status, operation.PermissionStatus);
            Assert.AreEqual(errorCode, operation.Error.Code);
            Assert.IsTrue(operation.IsDone);
            Assert.IsFalse(operation.CanCancel);
        }

        [Test]
        public void CompletedCancellation_HasNoPermissionFactOrError()
        {
            CompletedPlatformPermissionOperation operation =
                CompletedPlatformPermissionOperation.Cancelled();

            Assert.AreEqual(PlatformPermissionOperationStatus.Cancelled, operation.OperationStatus);
            Assert.AreEqual(PlatformPermissionStatus.Unknown, operation.PermissionStatus);
            Assert.IsTrue(operation.Error.IsNone);
            Assert.IsTrue(operation.IsDone);
            Assert.IsFalse(operation.CanCancel);
        }

        [Test]
        public void CompletedOperation_RejectsContradictoryTerminalStates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CompletedPlatformPermissionOperation.Succeeded(PlatformPermissionStatus.Denied));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Granted,
                    new PlatformError(PlatformErrorCode.RequestFailed)));
            Assert.Throws<ArgumentException>(() =>
                CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Denied,
                    PlatformError.None));
        }

        [Test]
        public void DeterministicFake_ModelsCooperativeAndNonCancellableRequests()
        {
            var cancellable = new DeterministicPermissionOperation(canCancel: true);
            cancellable.RequestCancel();
            Assert.AreEqual(PlatformPermissionOperationStatus.Pending, cancellable.OperationStatus);
            Assert.IsTrue(cancellable.CancellationRequested);
            Assert.IsTrue(cancellable.TryConfirmCancellation());
            Assert.AreEqual(PlatformPermissionOperationStatus.Cancelled, cancellable.OperationStatus);

            var nonCancellable = new DeterministicPermissionOperation(canCancel: false);
            nonCancellable.RequestCancel();
            Assert.IsFalse(nonCancellable.CancellationRequested);
            Assert.IsFalse(nonCancellable.TryConfirmCancellation());
            Assert.IsTrue(nonCancellable.TryComplete(PlatformPermissionStatus.Granted));
            Assert.AreEqual(PlatformPermissionOperationStatus.Succeeded, nonCancellable.OperationStatus);
        }

        [Test]
        public void DeterministicFake_RejectsConcurrentRequestWithIndependentBusyOperation()
        {
            var service = new DeterministicPermissionService();

            IPlatformPermissionOperation first = service.Request(PlatformPermissionId.Microphone);
            IPlatformPermissionOperation second = service.Request(PlatformPermissionId.Microphone);

            Assert.AreEqual(PlatformPermissionOperationStatus.Pending, first.OperationStatus);
            Assert.AreEqual(PlatformPermissionOperationStatus.Failed, second.OperationStatus);
            Assert.AreEqual(PlatformErrorCode.Busy, second.Error.Code);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void CompletedOperationPolling_AllocatesZeroBytes()
        {
            IPlatformPermissionOperation operation =
                CompletedPlatformPermissionOperation.Succeeded(PlatformPermissionStatus.Granted);
            int checksum = 0;

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                checksum += (int)operation.OperationStatus;
                checksum += (int)operation.PermissionStatus;
                checksum += operation.IsDone ? 1 : 0;
                checksum += operation.CanCancel ? 1 : 0;
                checksum += (int)operation.Error.Code;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
            Assert.Greater(checksum, 0);
        }

        private sealed class DeterministicPermissionService : IPlatformPermissionService
        {
            private DeterministicPermissionOperation _active;

            public PlatformPermissionQueryResult Query(PlatformPermissionId permissionId)
            {
                return permissionId.IsValid
                    ? PlatformPermissionQueryResult.Ok(PlatformPermissionStatus.NotDetermined)
                    : PlatformPermissionQueryResult.Fail(
                        new PlatformError(PlatformErrorCode.InvalidArgument));
            }

            public IPlatformPermissionOperation Request(PlatformPermissionId permissionId)
            {
                if (!permissionId.IsValid)
                {
                    return CompletedPlatformPermissionOperation.Failed(
                        PlatformPermissionStatus.Unknown,
                        new PlatformError(PlatformErrorCode.InvalidArgument));
                }

                if (_active != null && !_active.IsDone)
                {
                    return CompletedPlatformPermissionOperation.Failed(
                        PlatformPermissionStatus.Unknown,
                        new PlatformError(PlatformErrorCode.Busy));
                }

                _active = new DeterministicPermissionOperation(canCancel: true);
                return _active;
            }
        }

        private sealed class DeterministicPermissionOperation : IPlatformPermissionOperation
        {
            private readonly bool _canCancel;

            public DeterministicPermissionOperation(bool canCancel)
            {
                _canCancel = canCancel;
                OperationStatus = PlatformPermissionOperationStatus.Pending;
                PermissionStatus = PlatformPermissionStatus.NotDetermined;
            }

            public PlatformPermissionOperationStatus OperationStatus { get; private set; }
            public bool IsDone => OperationStatus != PlatformPermissionOperationStatus.Pending;
            public bool CanCancel => !IsDone && _canCancel && !CancellationRequested;
            public PlatformPermissionStatus PermissionStatus { get; private set; }
            public PlatformError Error { get; private set; }
            public bool CancellationRequested { get; private set; }

            public void RequestCancel()
            {
                if (CanCancel)
                    CancellationRequested = true;
            }

            public bool TryConfirmCancellation()
            {
                if (IsDone || !CancellationRequested)
                    return false;

                OperationStatus = PlatformPermissionOperationStatus.Cancelled;
                PermissionStatus = PlatformPermissionStatus.Unknown;
                Error = PlatformError.None;
                return true;
            }

            public bool TryComplete(PlatformPermissionStatus status)
            {
                if (IsDone)
                    return false;

                PermissionStatus = status;
                if (status == PlatformPermissionStatus.Granted
                    || status == PlatformPermissionStatus.NotRequired)
                {
                    OperationStatus = PlatformPermissionOperationStatus.Succeeded;
                    Error = PlatformError.None;
                    return true;
                }

                OperationStatus = PlatformPermissionOperationStatus.Failed;
                Error = new PlatformError(
                    status == PlatformPermissionStatus.Denied
                        ? PlatformErrorCode.PermissionDenied
                        : PlatformErrorCode.RequestFailed);
                return true;
            }
        }
    }
}
