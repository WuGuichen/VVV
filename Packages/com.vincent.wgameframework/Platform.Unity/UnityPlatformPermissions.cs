using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Android;

namespace MxFramework.Platform.Unity
{
    /// <summary>
    /// Queries and requests Unity microphone authorization with one main-thread-pumped host request.
    /// </summary>
    public sealed class UnityPlatformPermissionService : IPlatformPermissionService
    {
        private static readonly PlatformError InvalidPermissionError = new PlatformError(
            PlatformErrorCode.InvalidArgument,
            "Permission id must be a positive stable value.");
        private static readonly PlatformError UnsupportedPermissionError = new PlatformError(
            PlatformErrorCode.Unsupported,
            "The requested permission is not supported by this adapter.");
        private static readonly PlatformError BusyPermissionError = new PlatformError(
            PlatformErrorCode.Busy,
            "Another host permission request is already active.");
        private static readonly PlatformError DeniedPermissionError = new PlatformError(
            PlatformErrorCode.PermissionDenied,
            "The host denied the permission request.");
        private static readonly PlatformError RestrictedPermissionError = new PlatformError(
            PlatformErrorCode.PermissionRestricted,
            "The host restricts this permission.");
        private static readonly PlatformError RequestFailedError = new PlatformError(
            PlatformErrorCode.RequestFailed,
            "The host permission request failed.");
        private static readonly PlatformError WrongThreadError = new PlatformError(
            PlatformErrorCode.AdapterFailure,
            "Unity permission operations must run on their owner thread.");

        private readonly IUnityMicrophonePermissionBackend _microphoneBackend;
        private readonly int _ownerThreadId;
        private UnityPlatformPermissionOperation _activeOperation;

        /// <summary>
        /// Creates a Unity permission service on the calling main thread.
        /// </summary>
        /// <remarks>LowFreqAlloc during composition.</remarks>
        public UnityPlatformPermissionService()
            : this(UnityMicrophonePermissionBackendFactory.Create(Application.platform))
        {
        }

        internal UnityPlatformPermissionService(IUnityMicrophonePermissionBackend microphoneBackend)
        {
            _microphoneBackend = microphoneBackend
                ?? throw new ArgumentNullException(nameof(microphoneBackend));
            _ownerThreadId = Environment.CurrentManagedThreadId;
        }

        /// <summary>
        /// Gets whether a real host permission request is pending.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool HasActiveRequest => _activeOperation != null;

        /// <inheritdoc />
        public PlatformPermissionQueryResult Query(PlatformPermissionId permissionId)
        {
            if (!permissionId.IsValid)
                return PlatformPermissionQueryResult.Fail(InvalidPermissionError);

            if (permissionId != PlatformPermissionId.Microphone)
                return PlatformPermissionQueryResult.Ok(PlatformPermissionStatus.Unsupported);

            if (!IsOwnerThread())
                return PlatformPermissionQueryResult.Fail(WrongThreadError);

            try
            {
                PlatformPermissionStatus status = _microphoneBackend.QueryStatus();
                return status == PlatformPermissionStatus.Unknown
                    ? PlatformPermissionQueryResult.Fail(new PlatformError(
                        PlatformErrorCode.AdapterFailure,
                        "Unity returned an unknown permission status."))
                    : PlatformPermissionQueryResult.Ok(status);
            }
            catch (Exception exception)
            {
                return PlatformPermissionQueryResult.Fail(new PlatformError(
                    PlatformErrorCode.AdapterFailure,
                    "Unity permission query failed.",
                    exception));
            }
        }

        /// <inheritdoc />
        public IPlatformPermissionOperation Request(PlatformPermissionId permissionId)
        {
            if (!permissionId.IsValid)
            {
                return CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Unknown,
                    InvalidPermissionError);
            }

            if (permissionId != PlatformPermissionId.Microphone)
            {
                return CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Unsupported,
                    UnsupportedPermissionError);
            }

            if (!IsOwnerThread())
            {
                return CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Unknown,
                    WrongThreadError);
            }

            if (_activeOperation != null)
            {
                return CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Unknown,
                    BusyPermissionError);
            }

            PlatformPermissionQueryResult query = Query(permissionId);
            if (!query.Success)
            {
                return CompletedPlatformPermissionOperation.Failed(
                    PlatformPermissionStatus.Unknown,
                    query.Error);
            }

            switch (query.Status)
            {
                case PlatformPermissionStatus.Granted:
                case PlatformPermissionStatus.NotRequired:
                    return CompletedPlatformPermissionOperation.Succeeded(query.Status);
                case PlatformPermissionStatus.Unsupported:
                    return CompletedPlatformPermissionOperation.Failed(
                        PlatformPermissionStatus.Unsupported,
                        UnsupportedPermissionError);
                case PlatformPermissionStatus.Restricted:
                    return CompletedPlatformPermissionOperation.Failed(
                        PlatformPermissionStatus.Restricted,
                        RestrictedPermissionError);
            }

            try
            {
                if (!_microphoneBackend.TryBeginRequest(out PlatformError beginError))
                {
                    return CompletedPlatformPermissionOperation.Failed(
                        query.Status,
                        beginError.IsNone ? RequestFailedError : beginError);
                }

                _activeOperation = new UnityPlatformPermissionOperation(
                    _ownerThreadId,
                    _microphoneBackend.CanCancelActiveRequest);
                return _activeOperation;
            }
            catch (Exception exception)
            {
                return CompletedPlatformPermissionOperation.Failed(
                    query.Status,
                    new PlatformError(
                        PlatformErrorCode.AdapterFailure,
                        "Unity permission request could not be started.",
                        exception));
            }
        }

        /// <summary>
        /// Polls the active Unity request and publishes any terminal host fact to its operation.
        /// </summary>
        /// <returns>True only when this call transitions the operation to a terminal state.</returns>
        /// <remarks>
        /// NoAlloc after request creation on the normal polling path. Must be called on the owner thread.
        /// Background/provider completion is not visible through the operation until this method runs.
        /// </remarks>
        public bool Pump()
        {
            UnityPlatformPermissionOperation operation = _activeOperation;
            if (operation == null || !IsOwnerThread())
                return false;

            try
            {
                bool pollSucceeded = _microphoneBackend.TryPollRequest(
                    out bool isDone,
                    out PlatformPermissionStatus permissionStatus,
                    out PlatformError pollError);
                if (!pollSucceeded && pollError.IsNone)
                    pollError = RequestFailedError;

                if (pollSucceeded && isDone)
                {
                    CompleteFromHost(operation, permissionStatus, pollError);
                    _activeOperation = null;
                    return true;
                }

                if (!pollError.IsNone)
                {
                    operation.CompleteFailed(PlatformPermissionStatus.Unknown, pollError);
                    _activeOperation = null;
                    return true;
                }

                if (operation.ConsumeCancelRequest()
                    && _microphoneBackend.TryCancelActiveRequest())
                {
                    operation.CompleteCancelled();
                    _activeOperation = null;
                    return true;
                }

                operation.SetCanCancel(_microphoneBackend.CanCancelActiveRequest);
                return false;
            }
            catch (Exception exception)
            {
                operation.CompleteFailed(
                    PlatformPermissionStatus.Unknown,
                    new PlatformError(
                        PlatformErrorCode.AdapterFailure,
                        "Unity permission polling failed.",
                        exception));
                _activeOperation = null;
                return true;
            }
        }

        private static void CompleteFromHost(
            UnityPlatformPermissionOperation operation,
            PlatformPermissionStatus permissionStatus,
            PlatformError pollError)
        {
            if (!pollError.IsNone)
            {
                operation.CompleteFailed(permissionStatus, pollError);
                return;
            }

            switch (permissionStatus)
            {
                case PlatformPermissionStatus.Granted:
                case PlatformPermissionStatus.NotRequired:
                    operation.CompleteSucceeded(permissionStatus);
                    break;
                case PlatformPermissionStatus.Denied:
                    operation.CompleteFailed(permissionStatus, DeniedPermissionError);
                    break;
                case PlatformPermissionStatus.Restricted:
                    operation.CompleteFailed(permissionStatus, RestrictedPermissionError);
                    break;
                case PlatformPermissionStatus.Unsupported:
                    operation.CompleteFailed(permissionStatus, UnsupportedPermissionError);
                    break;
                default:
                    operation.CompleteFailed(permissionStatus, RequestFailedError);
                    break;
            }
        }

        private bool IsOwnerThread()
        {
            return Environment.CurrentManagedThreadId == _ownerThreadId;
        }
    }

    internal interface IUnityMicrophonePermissionBackend
    {
        PlatformPermissionStatus QueryStatus();
        bool TryBeginRequest(out PlatformError error);
        bool TryPollRequest(
            out bool isDone,
            out PlatformPermissionStatus permissionStatus,
            out PlatformError error);
        bool CanCancelActiveRequest { get; }
        bool TryCancelActiveRequest();
    }

    internal enum UnityMicrophonePermissionBackendKind
    {
        Unsupported = 0,
        NotRequired = 1,
        Android = 2,
        IOS = 3
    }

    internal static class UnityMicrophonePermissionBackendFactory
    {
        public static IUnityMicrophonePermissionBackend Create(RuntimePlatform runtimePlatform)
        {
            switch (GetKind(runtimePlatform))
            {
                case UnityMicrophonePermissionBackendKind.NotRequired:
                    return new UnityNotRequiredMicrophonePermissionBackend();
                case UnityMicrophonePermissionBackendKind.Android:
                    return new UnityAndroidMicrophonePermissionBackend();
                case UnityMicrophonePermissionBackendKind.IOS:
                    return new UnityIosMicrophonePermissionBackend();
                default:
                    return new UnityUnsupportedMicrophonePermissionBackend();
            }
        }

        public static UnityMicrophonePermissionBackendKind GetKind(RuntimePlatform runtimePlatform)
        {
            switch (runtimePlatform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsServer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXServer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxServer:
                    return UnityMicrophonePermissionBackendKind.NotRequired;
                case RuntimePlatform.Android:
                    return UnityMicrophonePermissionBackendKind.Android;
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.VisionOS:
                    return UnityMicrophonePermissionBackendKind.IOS;
                default:
                    return UnityMicrophonePermissionBackendKind.Unsupported;
            }
        }
    }

    internal sealed class UnityUnsupportedMicrophonePermissionBackend : IUnityMicrophonePermissionBackend
    {
        private static readonly PlatformError UnsupportedError = new PlatformError(
            PlatformErrorCode.Unsupported,
            "Unity does not expose microphone permission requests on this runtime platform.");

        public bool CanCancelActiveRequest => false;

        public PlatformPermissionStatus QueryStatus()
        {
            return PlatformPermissionStatus.Unsupported;
        }

        public bool TryBeginRequest(out PlatformError error)
        {
            error = UnsupportedError;
            return false;
        }

        public bool TryPollRequest(
            out bool isDone,
            out PlatformPermissionStatus permissionStatus,
            out PlatformError error)
        {
            isDone = true;
            permissionStatus = PlatformPermissionStatus.Unsupported;
            error = UnsupportedError;
            return false;
        }

        public bool TryCancelActiveRequest()
        {
            return false;
        }
    }

    internal sealed class UnityNotRequiredMicrophonePermissionBackend : IUnityMicrophonePermissionBackend
    {
        public bool CanCancelActiveRequest => false;

        public PlatformPermissionStatus QueryStatus()
        {
            return PlatformPermissionStatus.NotRequired;
        }

        public bool TryBeginRequest(out PlatformError error)
        {
            error = PlatformError.None;
            return false;
        }

        public bool TryPollRequest(
            out bool isDone,
            out PlatformPermissionStatus permissionStatus,
            out PlatformError error)
        {
            isDone = true;
            permissionStatus = PlatformPermissionStatus.NotRequired;
            error = PlatformError.None;
            return true;
        }

        public bool TryCancelActiveRequest()
        {
            return false;
        }
    }

    internal sealed class UnityIosMicrophonePermissionBackend : IUnityMicrophonePermissionBackend
    {
        private static readonly PlatformError NullRequestError = new PlatformError(
            PlatformErrorCode.RequestFailed,
            "Unity returned no iOS microphone authorization operation.");

        private AsyncOperation _request;
        private bool _requestFinished;

        public bool CanCancelActiveRequest => false;

        public PlatformPermissionStatus QueryStatus()
        {
            if (Application.HasUserAuthorization(UserAuthorization.Microphone))
                return PlatformPermissionStatus.Granted;
            return _requestFinished
                ? PlatformPermissionStatus.Denied
                : PlatformPermissionStatus.NotDetermined;
        }

        public bool TryBeginRequest(out PlatformError error)
        {
            _request = Application.RequestUserAuthorization(UserAuthorization.Microphone);
            _requestFinished = false;
            if (_request == null)
            {
                error = NullRequestError;
                return false;
            }

            error = PlatformError.None;
            return true;
        }

        public bool TryPollRequest(
            out bool isDone,
            out PlatformPermissionStatus permissionStatus,
            out PlatformError error)
        {
            isDone = _request != null && _request.isDone;
            if (!isDone)
            {
                permissionStatus = PlatformPermissionStatus.NotDetermined;
                error = PlatformError.None;
                return true;
            }

            _requestFinished = true;
            permissionStatus = Application.HasUserAuthorization(UserAuthorization.Microphone)
                ? PlatformPermissionStatus.Granted
                : PlatformPermissionStatus.Denied;
            error = PlatformError.None;
            return true;
        }

        public bool TryCancelActiveRequest()
        {
            return false;
        }
    }

    internal sealed class UnityAndroidMicrophonePermissionBackend : IUnityMicrophonePermissionBackend
    {
        private static readonly PlatformError DismissedError = new PlatformError(
            PlatformErrorCode.RequestFailed,
            "The Android permission request was dismissed without a decision.");
        private static readonly PlatformError InvalidCallbackError = new PlatformError(
            PlatformErrorCode.AdapterFailure,
            "Android reported a callback for an unexpected permission.");

        private readonly string _permissionName;
        private readonly IUnityAndroidPermissionApi _permissionApi;
        private UnityAndroidPermissionRequestState _activeRequest;
        private PlatformPermissionStatus _lastStatus;
        private long _nextGeneration;

        public UnityAndroidMicrophonePermissionBackend()
            : this(new UnityAndroidPermissionApi())
        {
        }

        internal UnityAndroidMicrophonePermissionBackend(IUnityAndroidPermissionApi permissionApi)
        {
            _permissionName = Permission.Microphone;
            _permissionApi = permissionApi
                ?? throw new ArgumentNullException(nameof(permissionApi));
            _lastStatus = PlatformPermissionStatus.NotDetermined;
        }

        public bool CanCancelActiveRequest => false;

        public PlatformPermissionStatus QueryStatus()
        {
            if (_permissionApi.HasUserAuthorizedPermission(_permissionName))
                return PlatformPermissionStatus.Granted;

            return _lastStatus == PlatformPermissionStatus.Granted
                ? PlatformPermissionStatus.NotDetermined
                : _lastStatus;
        }

        public bool TryBeginRequest(out PlatformError error)
        {
            long generation = _nextGeneration == long.MaxValue
                ? long.MaxValue
                : ++_nextGeneration;
            var request = new UnityAndroidPermissionRequestState(generation, _permissionName);
            _activeRequest = request;
            _lastStatus = PlatformPermissionStatus.NotDetermined;
            _permissionApi.RequestUserPermission(_permissionName, request.Callbacks);
            error = PlatformError.None;
            return true;
        }

        public bool TryPollRequest(
            out bool isDone,
            out PlatformPermissionStatus permissionStatus,
            out PlatformError error)
        {
            UnityAndroidPermissionRequestState request = _activeRequest;
            if (request == null)
            {
                isDone = true;
                permissionStatus = PlatformPermissionStatus.Unknown;
                error = InvalidCallbackError;
                return false;
            }

            UnityAndroidPermissionCallbackResult result = request.ResultSlot.Read();
            switch (result)
            {
                case UnityAndroidPermissionCallbackResult.Pending:
                    isDone = false;
                    permissionStatus = PlatformPermissionStatus.NotDetermined;
                    error = PlatformError.None;
                    return true;
                case UnityAndroidPermissionCallbackResult.Granted:
                    isDone = true;
                    permissionStatus = PlatformPermissionStatus.Granted;
                    _lastStatus = permissionStatus;
                    error = PlatformError.None;
                    return true;
                case UnityAndroidPermissionCallbackResult.Denied:
                    isDone = true;
                    permissionStatus = _permissionApi.ShouldShowRequestPermissionRationale(_permissionName)
                        ? PlatformPermissionStatus.Denied
                        : PlatformPermissionStatus.Restricted;
                    _lastStatus = permissionStatus;
                    error = PlatformError.None;
                    return true;
                case UnityAndroidPermissionCallbackResult.Dismissed:
                    isDone = true;
                    permissionStatus = PlatformPermissionStatus.NotDetermined;
                    error = DismissedError;
                    return true;
                default:
                    isDone = true;
                    permissionStatus = PlatformPermissionStatus.Unknown;
                    error = InvalidCallbackError;
                    return false;
            }
        }

        public bool TryCancelActiveRequest()
        {
            return false;
        }

        internal UnityAndroidPermissionRequestState ActiveRequest => _activeRequest;
    }

    internal interface IUnityAndroidPermissionApi
    {
        bool HasUserAuthorizedPermission(string permissionName);
        bool ShouldShowRequestPermissionRationale(string permissionName);
        void RequestUserPermission(string permissionName, PermissionCallbacks callbacks);
    }

    internal sealed class UnityAndroidPermissionApi : IUnityAndroidPermissionApi
    {
        public bool HasUserAuthorizedPermission(string permissionName)
        {
            return Permission.HasUserAuthorizedPermission(permissionName);
        }

        public bool ShouldShowRequestPermissionRationale(string permissionName)
        {
            return Permission.ShouldShowRequestPermissionRationale(permissionName);
        }

        public void RequestUserPermission(string permissionName, PermissionCallbacks callbacks)
        {
            Permission.RequestUserPermission(permissionName, callbacks);
        }
    }

    internal sealed class UnityAndroidPermissionRequestState
    {
        public UnityAndroidPermissionRequestState(long generation, string permissionName)
        {
            Generation = generation;
            ResultSlot = new UnityAndroidPermissionResultSlot(generation, permissionName);
            Callbacks = new PermissionCallbacks();
            Callbacks.PermissionGranted += OnPermissionGranted;
            Callbacks.PermissionDenied += OnPermissionDenied;
            Callbacks.PermissionRequestDismissed += OnPermissionDismissed;
        }

        internal long Generation { get; }
        internal PermissionCallbacks Callbacks { get; }
        internal UnityAndroidPermissionResultSlot ResultSlot { get; }

        private void OnPermissionGranted(string permissionName)
        {
            Publish(permissionName, UnityAndroidPermissionCallbackResult.Granted);
        }

        private void OnPermissionDenied(string permissionName)
        {
            Publish(permissionName, UnityAndroidPermissionCallbackResult.Denied);
        }

        private void OnPermissionDismissed(string permissionName)
        {
            Publish(permissionName, UnityAndroidPermissionCallbackResult.Dismissed);
        }

        internal void Publish(string permissionName, UnityAndroidPermissionCallbackResult result)
        {
            ResultSlot.Publish(permissionName, result);
        }
    }

    internal enum UnityAndroidPermissionCallbackResult
    {
        Pending = 0,
        Granted = 1,
        Denied = 2,
        Dismissed = 3,
        InvalidCallback = 4
    }

    internal sealed class UnityAndroidPermissionResultSlot
    {
        private readonly string _expectedPermissionName;
        private int _result;

        public UnityAndroidPermissionResultSlot(long generation, string expectedPermissionName)
        {
            Generation = generation;
            _expectedPermissionName = expectedPermissionName
                ?? throw new ArgumentNullException(nameof(expectedPermissionName));
        }

        internal long Generation { get; }

        internal UnityAndroidPermissionCallbackResult Read()
        {
            return (UnityAndroidPermissionCallbackResult)Volatile.Read(ref _result);
        }

        internal void Reset()
        {
            Volatile.Write(ref _result, (int)UnityAndroidPermissionCallbackResult.Pending);
        }

        internal void Publish(string permissionName, UnityAndroidPermissionCallbackResult result)
        {
            UnityAndroidPermissionCallbackResult published = string.Equals(
                permissionName,
                _expectedPermissionName,
                StringComparison.Ordinal)
                ? result
                : UnityAndroidPermissionCallbackResult.InvalidCallback;
            Interlocked.CompareExchange(
                ref _result,
                (int)published,
                (int)UnityAndroidPermissionCallbackResult.Pending);
        }
    }

    internal sealed class UnityPlatformPermissionOperation : IPlatformPermissionOperation
    {
        private readonly int _ownerThreadId;
        private bool _canCancel;
        private bool _cancelRequested;

        public UnityPlatformPermissionOperation(
            int ownerThreadId,
            bool canCancel)
        {
            _ownerThreadId = ownerThreadId;
            _canCancel = canCancel;
            OperationStatus = PlatformPermissionOperationStatus.Pending;
            PermissionStatus = PlatformPermissionStatus.NotDetermined;
            Error = PlatformError.None;
        }

        public PlatformPermissionOperationStatus OperationStatus { get; private set; }
        public bool IsDone => OperationStatus != PlatformPermissionOperationStatus.Pending;
        public bool CanCancel => !IsDone && _canCancel;
        public PlatformPermissionStatus PermissionStatus { get; private set; }
        public PlatformError Error { get; private set; }

        public void RequestCancel()
        {
            if (Environment.CurrentManagedThreadId == _ownerThreadId && CanCancel)
                _cancelRequested = true;
        }

        internal bool ConsumeCancelRequest()
        {
            bool requested = _cancelRequested;
            _cancelRequested = false;
            return requested;
        }

        internal void SetCanCancel(bool canCancel)
        {
            _canCancel = canCancel;
        }

        internal void CompleteSucceeded(PlatformPermissionStatus permissionStatus)
        {
            OperationStatus = PlatformPermissionOperationStatus.Succeeded;
            PermissionStatus = permissionStatus;
            Error = PlatformError.None;
            _canCancel = false;
        }

        internal void CompleteFailed(PlatformPermissionStatus permissionStatus, PlatformError error)
        {
            OperationStatus = PlatformPermissionOperationStatus.Failed;
            PermissionStatus = permissionStatus;
            Error = error.IsNone ? RequestFailedErrorForOperation : error;
            _canCancel = false;
        }

        internal void CompleteCancelled()
        {
            OperationStatus = PlatformPermissionOperationStatus.Cancelled;
            PermissionStatus = PlatformPermissionStatus.Unknown;
            Error = PlatformError.None;
            _canCancel = false;
        }

        private static PlatformError RequestFailedErrorForOperation => new PlatformError(
            PlatformErrorCode.RequestFailed,
            "The Unity permission operation failed.");
    }
}
