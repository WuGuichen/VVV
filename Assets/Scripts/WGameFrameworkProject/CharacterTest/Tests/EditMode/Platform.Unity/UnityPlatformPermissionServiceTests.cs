using System;
using System.Threading;
using MxFramework.Platform;
using MxFramework.Platform.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Android;

namespace MxFramework.Tests.PlatformUnity
{
    public sealed class UnityPlatformPermissionServiceTests
    {
        [TestCase(RuntimePlatform.WindowsEditor, (int)UnityMicrophonePermissionBackendKind.NotRequired)]
        [TestCase(RuntimePlatform.OSXPlayer, (int)UnityMicrophonePermissionBackendKind.NotRequired)]
        [TestCase(RuntimePlatform.LinuxServer, (int)UnityMicrophonePermissionBackendKind.NotRequired)]
        [TestCase(RuntimePlatform.Android, (int)UnityMicrophonePermissionBackendKind.Android)]
        [TestCase(RuntimePlatform.IPhonePlayer, (int)UnityMicrophonePermissionBackendKind.IOS)]
        [TestCase(RuntimePlatform.VisionOS, (int)UnityMicrophonePermissionBackendKind.IOS)]
        [TestCase(RuntimePlatform.WebGLPlayer, (int)UnityMicrophonePermissionBackendKind.Unsupported)]
        [TestCase(RuntimePlatform.QNXArm64, (int)UnityMicrophonePermissionBackendKind.Unsupported)]
        public void BackendFactory_UsesPlatformSpecificPermissionApi(
            RuntimePlatform runtimePlatform,
            int expected)
        {
            Assert.That((int)UnityMicrophonePermissionBackendFactory.GetKind(runtimePlatform), Is.EqualTo(expected));
        }

        [Test]
        public void AndroidResultSlot_PublishesOnlyFirstMatchingCallbackAcrossThreads()
        {
            const string microphonePermission = "android.permission.RECORD_AUDIO";
            var slot = new UnityAndroidPermissionResultSlot(1, microphonePermission);
            var thread = new Thread(() => slot.Publish(
                microphonePermission,
                UnityAndroidPermissionCallbackResult.Denied));

            Assert.That(slot.Read(), Is.EqualTo(UnityAndroidPermissionCallbackResult.Pending));
            thread.Start();
            thread.Join();
            slot.Publish(microphonePermission, UnityAndroidPermissionCallbackResult.Granted);

            Assert.That(slot.Read(), Is.EqualTo(UnityAndroidPermissionCallbackResult.Denied));

            slot.Reset();
            slot.Publish("unexpected.permission", UnityAndroidPermissionCallbackResult.Granted);
            Assert.That(slot.Read(), Is.EqualTo(UnityAndroidPermissionCallbackResult.InvalidCallback));
        }

        [Test]
        public void AndroidBackend_StaleCallbackCannotCompleteNextGeneration()
        {
            const string microphonePermission = "android.permission.RECORD_AUDIO";
            var permissionApi = new FakeAndroidPermissionApi();
            var backend = new UnityAndroidMicrophonePermissionBackend(permissionApi);

            Assert.That(backend.TryBeginRequest(out PlatformError firstBeginError), Is.True);
            Assert.That(firstBeginError.IsNone, Is.True);
            UnityAndroidPermissionRequestState oldRequest = backend.ActiveRequest;
            oldRequest.Publish(microphonePermission, UnityAndroidPermissionCallbackResult.Dismissed);
            Assert.That(backend.TryPollRequest(
                out bool firstDone,
                out _,
                out PlatformError firstPollError), Is.True);
            Assert.That(firstDone, Is.True);
            Assert.That(firstPollError.Code, Is.EqualTo(PlatformErrorCode.RequestFailed));

            Assert.That(backend.TryBeginRequest(out PlatformError nextBeginError), Is.True);
            Assert.That(nextBeginError.IsNone, Is.True);
            UnityAndroidPermissionRequestState nextRequest = backend.ActiveRequest;

            oldRequest.Publish(microphonePermission, UnityAndroidPermissionCallbackResult.Granted);

            Assert.That(nextRequest.Generation, Is.GreaterThan(oldRequest.Generation));
            Assert.That(backend.TryPollRequest(
                out bool nextDone,
                out PlatformPermissionStatus nextStatus,
                out PlatformError nextPollError), Is.True);
            Assert.That(nextDone, Is.False);
            Assert.That(nextStatus, Is.EqualTo(PlatformPermissionStatus.NotDetermined));
            Assert.That(nextPollError.IsNone, Is.True);
        }

        [Test]
        public void AndroidBackend_RevokedGrantedFactFallsBackToNotDetermined()
        {
            const string microphonePermission = "android.permission.RECORD_AUDIO";
            var permissionApi = new FakeAndroidPermissionApi { IsAuthorized = true };
            var backend = new UnityAndroidMicrophonePermissionBackend(permissionApi);

            Assert.That(backend.TryBeginRequest(out _), Is.True);
            backend.ActiveRequest.Publish(
                microphonePermission,
                UnityAndroidPermissionCallbackResult.Granted);
            Assert.That(backend.TryPollRequest(out bool isDone, out _, out _), Is.True);
            Assert.That(isDone, Is.True);
            Assert.That(backend.QueryStatus(), Is.EqualTo(PlatformPermissionStatus.Granted));

            permissionApi.IsAuthorized = false;

            Assert.That(backend.QueryStatus(), Is.EqualTo(PlatformPermissionStatus.NotDetermined));
        }

        [Test]
        public void AndroidResultSlot_WarmPublishReadResetAllocateZeroBytes()
        {
            const string microphonePermission = "android.permission.RECORD_AUDIO";
            var slot = new UnityAndroidPermissionResultSlot(1, microphonePermission);
            slot.Publish(microphonePermission, UnityAndroidPermissionCallbackResult.Granted);
            slot.Reset();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++)
            {
                slot.Publish(microphonePermission, UnityAndroidPermissionCallbackResult.Granted);
                _ = slot.Read();
                slot.Reset();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void Query_MapsInvalidUnsupportedAndTrustedFacts()
        {
            var backend = new FakeBackend { QueryResult = PlatformPermissionStatus.Restricted };
            var service = new UnityPlatformPermissionService(backend);

            PlatformPermissionQueryResult invalid = service.Query(default);
            PlatformPermissionQueryResult unsupported = service.Query(new PlatformPermissionId(99));
            PlatformPermissionQueryResult restricted = service.Query(PlatformPermissionId.Microphone);

            Assert.That(invalid.Success, Is.False);
            Assert.That(invalid.Status, Is.EqualTo(PlatformPermissionStatus.Unknown));
            Assert.That(invalid.Error.Code, Is.EqualTo(PlatformErrorCode.InvalidArgument));
            Assert.That(unsupported.Success, Is.True);
            Assert.That(unsupported.Status, Is.EqualTo(PlatformPermissionStatus.Unsupported));
            Assert.That(restricted.Success, Is.True);
            Assert.That(restricted.Status, Is.EqualTo(PlatformPermissionStatus.Restricted));
        }

        [TestCase(PlatformPermissionStatus.Granted)]
        [TestCase(PlatformPermissionStatus.NotRequired)]
        public void Request_AlreadySatisfiedCompletesSuccessfully(PlatformPermissionStatus status)
        {
            var service = new UnityPlatformPermissionService(new FakeBackend { QueryResult = status });

            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            Assert.That(operation, Is.Not.Null);
            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Succeeded));
            Assert.That(operation.PermissionStatus, Is.EqualTo(status));
            Assert.That(operation.Error.IsNone, Is.True);
            Assert.That(service.HasActiveRequest, Is.False);
        }

        [TestCase(PlatformPermissionStatus.Unsupported, PlatformErrorCode.Unsupported)]
        [TestCase(PlatformPermissionStatus.Restricted, PlatformErrorCode.PermissionRestricted)]
        public void Request_NonRequestableFactCompletesFailed(
            PlatformPermissionStatus status,
            PlatformErrorCode errorCode)
        {
            var service = new UnityPlatformPermissionService(new FakeBackend { QueryResult = status });

            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            Assert.That(operation, Is.Not.Null);
            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Failed));
            Assert.That(operation.PermissionStatus, Is.EqualTo(status));
            Assert.That(operation.Error.Code, Is.EqualTo(errorCode));
        }

        [Test]
        public void Request_AllowsOneRealHostRequestAndReturnsIndependentBusyFailure()
        {
            var backend = new FakeBackend();
            var service = new UnityPlatformPermissionService(backend);

            IPlatformPermissionOperation first = service.Request(PlatformPermissionId.Microphone);
            IPlatformPermissionOperation second = service.Request(PlatformPermissionId.Microphone);

            Assert.That(first.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Pending));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Failed));
            Assert.That(second.Error.Code, Is.EqualTo(PlatformErrorCode.Busy));
            Assert.That(backend.BeginCount, Is.EqualTo(1));
        }

        [TestCase(PlatformPermissionStatus.Granted, PlatformPermissionOperationStatus.Succeeded, PlatformErrorCode.None)]
        [TestCase(PlatformPermissionStatus.NotRequired, PlatformPermissionOperationStatus.Succeeded, PlatformErrorCode.None)]
        [TestCase(PlatformPermissionStatus.Denied, PlatformPermissionOperationStatus.Failed, PlatformErrorCode.PermissionDenied)]
        [TestCase(PlatformPermissionStatus.Restricted, PlatformPermissionOperationStatus.Failed, PlatformErrorCode.PermissionRestricted)]
        public void Pump_PublishesStructuredTerminalHostFact(
            PlatformPermissionStatus finalStatus,
            PlatformPermissionOperationStatus expectedOperationStatus,
            PlatformErrorCode expectedError)
        {
            var backend = new FakeBackend();
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);
            backend.IsDone = true;
            backend.FinalStatus = finalStatus;

            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Pending));
            Assert.That(service.Pump(), Is.True);

            Assert.That(operation.OperationStatus, Is.EqualTo(expectedOperationStatus));
            Assert.That(operation.PermissionStatus, Is.EqualTo(finalStatus));
            Assert.That(operation.Error.Code, Is.EqualTo(expectedError));
            Assert.That(service.HasActiveRequest, Is.False);
        }

        [Test]
        public void NonCancellableRequest_DoesNotFabricateCancelled()
        {
            var backend = new FakeBackend { CanCancel = false, CancelSucceeds = true };
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            operation.RequestCancel();
            Assert.That(service.Pump(), Is.False);

            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Pending));
            Assert.That(backend.CancelCount, Is.Zero);
        }

        [Test]
        public void CancellableRequest_OnlyCancelsAfterProviderConfirmation()
        {
            var backend = new FakeBackend { CanCancel = true, CancelSucceeds = true };
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            operation.RequestCancel();
            Assert.That(service.Pump(), Is.True);

            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Cancelled));
            Assert.That(operation.PermissionStatus, Is.EqualTo(PlatformPermissionStatus.Unknown));
            Assert.That(operation.Error.IsNone, Is.True);
        }

        [Test]
        public void LateHostResultWinsOverCancellationRequest()
        {
            var backend = new FakeBackend { CanCancel = true, CancelSucceeds = true };
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            operation.RequestCancel();
            backend.IsDone = true;
            backend.FinalStatus = PlatformPermissionStatus.Granted;
            service.Pump();

            Assert.That(operation.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Succeeded));
            Assert.That(backend.CancelCount, Is.Zero);
        }

        [Test]
        public void BackgroundCompletion_RemainsPendingUntilMainThreadPump()
        {
            var backend = new FakeBackend();
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);

            backend.IsDone = true;
            backend.FinalStatus = PlatformPermissionStatus.Granted;

            Assert.That(operation.IsDone, Is.False);
            Assert.That(operation.PermissionStatus, Is.EqualTo(PlatformPermissionStatus.NotDetermined));
            Assert.That(service.Pump(), Is.True);
            Assert.That(operation.IsDone, Is.True);
        }

        [Test]
        public void AdapterFailuresAreStructuredAndNonNull()
        {
            var queryBackend = new FakeBackend { ThrowOnQuery = true };
            var queryService = new UnityPlatformPermissionService(queryBackend);
            PlatformPermissionQueryResult query = queryService.Query(PlatformPermissionId.Microphone);

            var beginBackend = new FakeBackend
            {
                BeginSucceeds = false,
                BeginError = new PlatformError(PlatformErrorCode.RequestFailed, "fake begin failure")
            };
            var beginService = new UnityPlatformPermissionService(beginBackend);
            IPlatformPermissionOperation begin = beginService.Request(PlatformPermissionId.Microphone);

            Assert.That(query.Success, Is.False);
            Assert.That(query.Error.Code, Is.EqualTo(PlatformErrorCode.AdapterFailure));
            Assert.That(begin, Is.Not.Null);
            Assert.That(begin.OperationStatus, Is.EqualTo(PlatformPermissionOperationStatus.Failed));
            Assert.That(begin.Error.Code, Is.EqualTo(PlatformErrorCode.RequestFailed));
        }

        [Test]
        public void WarmPendingPumpAndOperationPolling_AllocateZeroBytes()
        {
            var backend = new FakeBackend();
            var service = new UnityPlatformPermissionService(backend);
            IPlatformPermissionOperation operation = service.Request(PlatformPermissionId.Microphone);
            service.Pump();

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++)
            {
                service.Pump();
                _ = operation.OperationStatus;
                _ = operation.IsDone;
                _ = operation.CanCancel;
                _ = operation.PermissionStatus;
                _ = operation.Error;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private sealed class FakeBackend : IUnityMicrophonePermissionBackend
        {
            public PlatformPermissionStatus QueryResult = PlatformPermissionStatus.NotDetermined;
            public PlatformPermissionStatus FinalStatus = PlatformPermissionStatus.Granted;
            public PlatformError BeginError = PlatformError.None;
            public PlatformError PollError = PlatformError.None;
            public bool BeginSucceeds = true;
            public bool PollSucceeds = true;
            public bool IsDone;
            public bool CanCancel;
            public bool CancelSucceeds;
            public bool ThrowOnQuery;
            public int BeginCount;
            public int CancelCount;

            public bool CanCancelActiveRequest => CanCancel;

            public PlatformPermissionStatus QueryStatus()
            {
                if (ThrowOnQuery)
                    throw new InvalidOperationException("fake query failure");
                return QueryResult;
            }

            public bool TryBeginRequest(out PlatformError error)
            {
                BeginCount++;
                error = BeginError;
                return BeginSucceeds;
            }

            public bool TryPollRequest(
                out bool isDone,
                out PlatformPermissionStatus permissionStatus,
                out PlatformError error)
            {
                isDone = IsDone;
                permissionStatus = IsDone ? FinalStatus : PlatformPermissionStatus.NotDetermined;
                error = PollError;
                return PollSucceeds;
            }

            public bool TryCancelActiveRequest()
            {
                CancelCount++;
                return CancelSucceeds;
            }
        }

        private sealed class FakeAndroidPermissionApi : IUnityAndroidPermissionApi
        {
            public bool IsAuthorized;
            public bool ShouldShowRationale;
            public int RequestCount;

            public bool HasUserAuthorizedPermission(string permissionName)
            {
                return IsAuthorized;
            }

            public bool ShouldShowRequestPermissionRationale(string permissionName)
            {
                return ShouldShowRationale;
            }

            public void RequestUserPermission(string permissionName, PermissionCallbacks callbacks)
            {
                RequestCount++;
            }
        }
    }
}
