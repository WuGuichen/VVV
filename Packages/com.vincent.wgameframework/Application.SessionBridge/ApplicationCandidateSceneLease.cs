using System;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    public enum ApplicationCandidateSceneLeaseStatus
    {
        Invalid = 0,
        Created = 1,
        Loading = 2,
        Loaded = 3,
        HandoffFrozen = 4,
        RollbackOwner = 5,
        Unloading = 6,
        Released = 7,
        CleanupFailed = 8
    }

    public enum ApplicationCandidateSceneIsolationErrorCode
    {
        None = 0,
        UnsupportedCandidateScene = 1,
        IsolationFailed = 2,
        MakeInertFailed = 3
    }

    /// <summary>Restricted noEngine candidate scene request.</summary>
    /// <remarks>Construction and getters are NoAlloc.</remarks>
    public readonly struct ApplicationCandidateSceneRequest
    {
        public ApplicationCandidateSceneRequest(
            string sceneKey,
            SceneFlowLoadMode loadMode = SceneFlowLoadMode.Additive,
            bool unloadPreviousScene = false)
        {
            SceneKey = sceneKey ?? string.Empty;
            LoadMode = loadMode;
            UnloadPreviousScene = unloadPreviousScene;
        }

        public string SceneKey { get; }
        public SceneFlowLoadMode LoadMode { get; }
        public bool UnloadPreviousScene { get; }

        internal bool IsSupported => !string.IsNullOrWhiteSpace(SceneKey) &&
            LoadMode == SceneFlowLoadMode.Additive && !UnloadPreviousScene;

        internal SceneFlowRequest ToSceneFlowRequest() =>
            new SceneFlowRequest(SceneKey, LoadMode, UnloadPreviousScene);
    }

    /// <summary>Result of a candidate scene isolation transition.</summary>
    /// <remarks>Construction and getters are NoAlloc unless an implementation supplies an exception.</remarks>
    public readonly struct ApplicationCandidateSceneIsolationResult
    {
        private ApplicationCandidateSceneIsolationResult(
            ApplicationCandidateSceneIsolationErrorCode errorCode,
            string message,
            Exception exception)
        {
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        public bool Success => ErrorCode == ApplicationCandidateSceneIsolationErrorCode.None;
        public ApplicationCandidateSceneIsolationErrorCode ErrorCode { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static ApplicationCandidateSceneIsolationResult Succeeded =>
            new ApplicationCandidateSceneIsolationResult(
                ApplicationCandidateSceneIsolationErrorCode.None, string.Empty, null);

        public static ApplicationCandidateSceneIsolationResult Failed(
            ApplicationCandidateSceneIsolationErrorCode errorCode,
            string message,
            Exception exception = null)
        {
            if (errorCode == ApplicationCandidateSceneIsolationErrorCode.None)
                throw new ArgumentException("A failed isolation result requires an error code.", nameof(errorCode));
            return new ApplicationCandidateSceneIsolationResult(errorCode, message, exception);
        }
    }

    /// <summary>Opaque reference identity for one candidate-owned scene domain.</summary>
    /// <remarks>Construction is LowFreqAlloc. The identity is read-only and carries no scene authority.</remarks>
    public sealed class ApplicationCandidateSceneOwnershipIdentity
    {
    }

    /// <summary>SceneFlow driver whose load/unload operations address one exact candidate ownership domain.</summary>
    public interface IApplicationCandidateOwnedSceneDriver : ISceneFlowDriver
    {
        /// <remarks>NoAlloc. The same non-null reference must be returned for the driver's lifetime.</remarks>
        ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity { get; }
    }

    /// <summary>Candidate-only side-effect isolation gate.</summary>
    /// <remarks>A failed entry must leave the scene inert. Transitions are LowFreqAlloc only for diagnostics.</remarks>
    public interface IApplicationCandidateSceneIsolationGate
    {
        bool IsIsolated { get; }
        bool IsInert { get; }
        /// <remarks>NoAlloc. Must exactly match the paired candidate scene driver's stable identity.</remarks>
        ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity { get; }
        /// <summary>Whether this gate currently owns the exact candidate scene instance.</summary>
        /// <remarks>NoAlloc. Implementations must fail closed when ownership cannot be determined.</remarks>
        bool HasOwnedScene { get; }
        ApplicationCandidateSceneIsolationResult TryEnterIsolation(in ApplicationCandidateSceneRequest request);
        ApplicationCandidateSceneIsolationResult MakeInert();
    }

    /// <summary>Construction-only scene graph descriptor.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSceneGraph
    {
        public ApplicationCandidateSceneGraph(
            SceneFlowController controller,
            SceneFlowRuntimeModule runtimeModule,
            ApplicationControllerPumpStrategy pumpStrategy,
            IApplicationCandidateSceneIsolationGate isolationGate)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            RuntimeModule = runtimeModule;
            PumpStrategy = pumpStrategy;
            IsolationGate = isolationGate ?? throw new ArgumentNullException(nameof(isolationGate));
            if (!(Controller.Driver is IApplicationCandidateOwnedSceneDriver ownedDriver))
                throw new ArgumentException(
                    "Candidate SceneFlow driver must expose an exact ownership identity.", nameof(controller));
            ApplicationCandidateSceneOwnershipIdentity driverIdentity = ownedDriver.SceneOwnershipIdentity;
            ApplicationCandidateSceneOwnershipIdentity gateIdentity = IsolationGate.SceneOwnershipIdentity;
            if (driverIdentity == null || gateIdentity == null || !ReferenceEquals(driverIdentity, gateIdentity))
                throw new ArgumentException(
                    "Candidate scene driver and isolation gate must share one non-null ownership identity.",
                    nameof(isolationGate));
            OwnershipIdentity = driverIdentity;
        }

        public SceneFlowController Controller { get; }
        public SceneFlowRuntimeModule RuntimeModule { get; }
        public ApplicationControllerPumpStrategy PumpStrategy { get; }
        public IApplicationCandidateSceneIsolationGate IsolationGate { get; }
        internal ISceneFlowDriver DriverIdentity => Controller.Driver;
        internal ApplicationCandidateSceneOwnershipIdentity OwnershipIdentity { get; }
    }

    /// <summary>Stable transaction-owned attachment point for a prepared candidate scene.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc. A transaction must return one stable instance.</remarks>
    public sealed class ApplicationCandidateSceneTransactionPort
    {
        private IApplicationCandidateSceneTransaction _owner;
        private ApplicationCandidateSceneLease _lease;

        public ApplicationCandidateSceneLease SceneLease => Volatile.Read(ref _lease);

        internal bool TryAttach(
            IApplicationCandidateSceneTransaction owner,
            ApplicationCandidateSceneLease lease)
        {
            if (owner == null || lease == null) return false;
            IApplicationCandidateSceneTransaction claimed = Interlocked.CompareExchange(ref _owner, owner, null);
            if (claimed != null && !ReferenceEquals(claimed, owner)) return false;
            return Interlocked.CompareExchange(ref _lease, lease, null) == null;
        }
    }

    public interface IApplicationCandidateSceneTransaction : IApplicationCandidateSessionTransaction
    {
        /// <remarks>NoAlloc. The same reference must be returned for the transaction's entire lifetime.</remarks>
        ApplicationCandidateSceneTransactionPort CandidateScenePort { get; }
    }

    /// <summary>Structured candidate scene forward and cleanup detail.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSceneErrorDetail : IApplicationStartupInnerErrorDetail
    {
        public const string ErrorDomainId = "mx.application.candidate-scene";

        internal ApplicationCandidateSceneErrorDetail(
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateSessionPhase phase,
            ApplicationCandidateSessionErrorCode forwardCode,
            ApplicationCandidateSessionErrorCode cleanupCode,
            SceneFlowError forwardError,
            ApplicationCandidateSceneIsolationResult forwardIsolationError,
            Exception forwardException,
            SceneFlowError cleanupError,
            ApplicationCandidateSceneIsolationResult cleanupIsolationError,
            Exception cleanupException,
            ApplicationCandidateCleanupDebt cleanupDebt)
        {
            Code = code;
            Phase = phase;
            ForwardCode = forwardCode;
            CleanupCode = cleanupCode;
            ForwardError = forwardError;
            ForwardIsolationError = forwardIsolationError;
            ForwardException = forwardException;
            CleanupError = cleanupError;
            CleanupIsolationError = cleanupIsolationError;
            CleanupException = cleanupException;
            CleanupDebt = cleanupDebt;
        }

        public string DomainId => ErrorDomainId;
        public string StableCode => ApplicationCandidateSessionErrorDetail.GetStableCode(Code);
        public ApplicationCandidateSessionErrorCode Code { get; }
        public ApplicationCandidateSessionPhase Phase { get; }
        public ApplicationCandidateSessionErrorCode ForwardCode { get; }
        public ApplicationCandidateSessionErrorCode CleanupCode { get; }
        public SceneFlowError ForwardError { get; }
        public ApplicationCandidateSceneIsolationResult ForwardIsolationError { get; }
        public Exception ForwardException { get; }
        public SceneFlowError CleanupError { get; }
        public ApplicationCandidateSceneIsolationResult CleanupIsolationError { get; }
        public Exception CleanupException { get; }
        public ApplicationCandidateCleanupDebt CleanupDebt { get; }
    }

    /// <summary>Read-only candidate scene owner lease.</summary>
    /// <remarks>Construction is LowFreqAlloc; steady status/progress/error reads are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSceneLease
    {
        private readonly ApplicationCandidateSceneGraph _graph;
        private int _status = (int)ApplicationCandidateSceneLeaseStatus.Created;
        private int _pumpOwnership = (int)ApplicationCandidatePumpOwnership.StartupOwner;
        private int _cancelRequested;
        private bool _isolationEntered;
        private bool _inertAttempted;
        private bool _unloadRequested;
        private ApplicationCandidateSceneRequest _request;
        private ApplicationCandidateSessionErrorCode _forwardCode;
        private SceneFlowError _forwardError;
        private ApplicationCandidateSceneIsolationResult _forwardIsolationError;
        private Exception _forwardException;
        private SceneFlowError _cleanupError;
        private ApplicationCandidateSceneIsolationResult _cleanupIsolationError;
        private Exception _cleanupException;
        private float _progress;

        internal ApplicationCandidateSceneLease(ApplicationCandidateSceneGraph graph) =>
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));

        public ApplicationCandidateSceneLeaseStatus Status =>
            (ApplicationCandidateSceneLeaseStatus)Volatile.Read(ref _status);
        public ApplicationCandidatePumpOwnership PumpOwnership =>
            (ApplicationCandidatePumpOwnership)Volatile.Read(ref _pumpOwnership);
        public string SceneKey => _request.SceneKey ?? string.Empty;
        public float Progress => _progress;
        public SceneFlowOperationType CurrentOperationType => _graph.Controller.CurrentOperationType;
        public bool IsCancelRequested => Volatile.Read(ref _cancelRequested) != 0;
        public bool IsReleased => Status == ApplicationCandidateSceneLeaseStatus.Released;

        internal ApplicationCandidateSessionErrorCode ForwardCode => _forwardCode;
        internal SceneFlowError ForwardError => _forwardError;
        internal ApplicationCandidateSceneIsolationResult ForwardIsolationError => _forwardIsolationError;
        internal Exception ForwardException => _forwardException;
        internal SceneFlowError CleanupError => _cleanupError;
        internal ApplicationCandidateSceneIsolationResult CleanupIsolationError => _cleanupIsolationError;
        internal Exception CleanupException => _cleanupException;
        internal bool HasCleanupFailure =>
            !_cleanupIsolationError.Success || _cleanupException != null;
        internal bool IsReady => Status == ApplicationCandidateSceneLeaseStatus.Loaded;
        internal ApplicationControllerPumpStrategy PumpStrategy => _graph.PumpStrategy;
        internal SceneFlowController Controller => _graph.Controller;

        internal void RecordForwardFault(Exception exception)
        {
            if (exception == null) return;
            _forwardException = Combine(_forwardException, exception);
            _forwardCode = ApplicationCandidateSessionErrorCode.SceneLoadFailed;
            RequestCancel();
        }

        internal void RecordCleanupFault(Exception exception)
        {
            if (exception == null) return;
            _cleanupException = Combine(_cleanupException, exception);
        }

        internal ApplicationCandidateSessionErrorCode BeginPrepare(
            in ApplicationCandidateSceneRequest request)
        {
            if (Status != ApplicationCandidateSceneLeaseStatus.Created)
                return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
            _request = request;
            if (IsCancelRequested)
            {
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Released);
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
                return ApplicationCandidateSessionErrorCode.None;
            }
            if (!request.IsSupported)
            {
                _forwardCode = ApplicationCandidateSessionErrorCode.SceneRequestUnsupported;
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Released);
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
                return _forwardCode;
            }

            if (_graph.Controller.IsBusy || !string.IsNullOrEmpty(_graph.Controller.ActiveSceneKey))
            {
                _forwardCode = ApplicationCandidateSessionErrorCode.InvalidGraph;
                CompleteWithoutLoadedScene();
                return _forwardCode;
            }

            bool isolated = false;
            try
            {
                _forwardIsolationError = _graph.IsolationGate.TryEnterIsolation(in request);
                isolated = _forwardIsolationError.Success && _graph.IsolationGate.IsIsolated;
            }
            catch (Exception exception)
            {
                _forwardException = exception;
                _forwardIsolationError = ApplicationCandidateSceneIsolationResult.Failed(
                    ApplicationCandidateSceneIsolationErrorCode.IsolationFailed,
                    "Candidate scene isolation gate threw.", exception);
            }
            if (!_forwardIsolationError.Success || !isolated)
            {
                _forwardCode = ApplicationCandidateSessionErrorCode.SceneIsolationFailed;
                bool inert = false;
                try { inert = _graph.IsolationGate.IsInert; }
                catch (Exception exception) { _cleanupException = Combine(_cleanupException, exception); }
                if (!inert)
                {
                    _isolationEntered = true;
                    MakeInert();
                }
                CompleteWithoutLoadedScene();
                return _forwardCode;
            }

            _isolationEntered = true;
            SceneFlowResult accepted;
            try { accepted = _graph.Controller.RequestLoad(request.ToSceneFlowRequest()); }
            catch (Exception exception)
            {
                _forwardException = Combine(_forwardException, exception);
                _forwardCode = ApplicationCandidateSessionErrorCode.SceneLoadFailed;
                MakeInert();
                CompleteWithoutLoadedScene();
                return _forwardCode;
            }
            if (accepted.Status != SceneFlowResultStatus.Accepted)
            {
                _forwardError = accepted.Error;
                _forwardCode = ApplicationCandidateSessionErrorCode.SceneLoadFailed;
                MakeInert();
                CompleteWithoutLoadedScene();
                return _forwardCode;
            }

            Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Loading);
            return ApplicationCandidateSessionErrorCode.None;
        }

        internal void RequestCancel() => Volatile.Write(ref _cancelRequested, 1);

        internal void TickPrepare(
            IApplicationCandidateSceneTransaction owner,
            ApplicationCandidateSceneTransactionPort port)
        {
            if (Status == ApplicationCandidateSceneLeaseStatus.Loading)
            {
                _graph.Controller.Tick();
                _progress = _graph.Controller.CurrentProgress;
                if (_graph.Controller.IsBusy) return;
                SceneFlowResult result = _graph.Controller.LastResult;
                if (result.Status == SceneFlowResultStatus.Failed)
                {
                    _forwardError = result.Error;
                    _forwardCode = ApplicationCandidateSessionErrorCode.SceneLoadFailed;
                    if (HasOwnedSceneAfterLoadFailure())
                    {
                        BeginUnload();
                        return;
                    }
                    MakeInert();
                    CompleteWithoutLoadedScene();
                    return;
                }
                if (result.Status != SceneFlowResultStatus.Succeeded) return;

                if (IsCancelRequested)
                {
                    BeginUnload();
                    return;
                }

                if (port == null || !port.TryAttach(owner, this))
                {
                    _forwardCode = ApplicationCandidateSessionErrorCode.SceneAttachmentFailed;
                    BeginUnload();
                    return;
                }
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Loaded);
                return;
            }

            if (Status == ApplicationCandidateSceneLeaseStatus.Unloading)
                TickUnload();
        }

        internal bool BeginRollback()
        {
            ApplicationCandidateSceneLeaseStatus status = Status;
            if (status == ApplicationCandidateSceneLeaseStatus.Released ||
                status == ApplicationCandidateSceneLeaseStatus.CleanupFailed) return true;
            if (PumpOwnership == ApplicationCandidatePumpOwnership.HandoffClaimed) return false;
            if (PumpOwnership != ApplicationCandidatePumpOwnership.RetirementOwner)
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.RollbackOwner);
            if (status == ApplicationCandidateSceneLeaseStatus.Created)
            {
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Released);
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
                return true;
            }
            if (status == ApplicationCandidateSceneLeaseStatus.Loading)
            {
                RequestCancel();
                return true;
            }
            BeginUnload();
            return true;
        }

        internal void TickRollback()
        {
            ApplicationCandidateSceneLeaseStatus status = Status;
            if (status == ApplicationCandidateSceneLeaseStatus.Loading)
            {
                TickPrepare(null, null);
                return;
            }
            if (status == ApplicationCandidateSceneLeaseStatus.Loaded ||
                status == ApplicationCandidateSceneLeaseStatus.HandoffFrozen ||
                status == ApplicationCandidateSceneLeaseStatus.RollbackOwner)
            {
                BeginUnload();
                return;
            }
            if (status == ApplicationCandidateSceneLeaseStatus.Unloading) TickUnload();
        }

        internal bool FreezeForHandoff()
        {
            if (Status == ApplicationCandidateSceneLeaseStatus.HandoffFrozen) return true;
            if (Status != ApplicationCandidateSceneLeaseStatus.Loaded ||
                PumpOwnership != ApplicationCandidatePumpOwnership.StartupOwner) return false;
            Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.HandoffFrozen);
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffFrozen);
            return true;
        }

        internal bool TryClaimFrozenHandoff()
        {
            if (Interlocked.CompareExchange(ref _pumpOwnership,
                    (int)ApplicationCandidatePumpOwnership.HandoffClaimed,
                    (int)ApplicationCandidatePumpOwnership.HandoffFrozen) !=
                (int)ApplicationCandidatePumpOwnership.HandoffFrozen) return false;
            return true;
        }

        internal void ClaimPreparedFrozenHandoff()
        {
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffClaimed);
        }

        internal bool CanClaimPreparedFrozenHandoff =>
            Status == ApplicationCandidateSceneLeaseStatus.HandoffFrozen &&
            PumpOwnership == ApplicationCandidatePumpOwnership.HandoffFrozen;

        internal void PromoteClaimedHandoffToLiveOwner()
        {
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.LiveOwner);
        }

        internal void BeginLiveRetirementOwnership()
        {
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.RetirementOwner);
            // Publication retirement may yield in runtime/app-flow cleanup before scene rollback
            // is reached. Revoke player-visible side effects as part of ownership transfer so the
            // retired generation is inert before any asynchronous cleanup tick can yield.
            MakeInert();
        }

        internal bool CanReleaseResources =>
            Status == ApplicationCandidateSceneLeaseStatus.Created ||
            Status == ApplicationCandidateSceneLeaseStatus.Released;

        internal void FinalizeUnstarted()
        {
            if (Status != ApplicationCandidateSceneLeaseStatus.Created) return;
            Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Released);
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
        }

        private void BeginUnload()
        {
            if (_unloadRequested) return;
            _unloadRequested = true;
            MakeInert();

            SceneFlowResult accepted;
            try { accepted = _graph.Controller.RequestUnload(_request.SceneKey); }
            catch (Exception exception)
            {
                _cleanupException = Combine(_cleanupException, exception);
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.CleanupFailed);
                return;
            }
            if (accepted.Status != SceneFlowResultStatus.Accepted)
            {
                _cleanupError = accepted.Error;
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.CleanupFailed);
                return;
            }
            Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Unloading);
        }

        private void TickUnload()
        {
            _graph.Controller.Tick();
            _progress = _graph.Controller.CurrentProgress;
            if (_graph.Controller.IsBusy) return;
            SceneFlowResult result = _graph.Controller.LastResult;
            bool inert = false;
            try { inert = _graph.IsolationGate.IsInert; }
            catch (Exception exception) { _cleanupException = Combine(_cleanupException, exception); }
            if (result.Status == SceneFlowResultStatus.Succeeded &&
                _cleanupIsolationError.Success && _cleanupException == null && inert)
            {
                Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.Released);
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
                return;
            }
            if (result.Status == SceneFlowResultStatus.Failed) _cleanupError = result.Error;
            Volatile.Write(ref _status, (int)ApplicationCandidateSceneLeaseStatus.CleanupFailed);
        }

        private void MakeInert()
        {
            if (_inertAttempted) return;
            _inertAttempted = true;
            if (!_isolationEntered)
            {
                _cleanupIsolationError = ApplicationCandidateSceneIsolationResult.Succeeded;
                return;
            }
            bool inert = false;
            try
            {
                _cleanupIsolationError = _graph.IsolationGate.MakeInert();
                inert = _cleanupIsolationError.Success && _graph.IsolationGate.IsInert;
            }
            catch (Exception exception)
            {
                _cleanupException = Combine(_cleanupException, exception);
                _cleanupIsolationError = ApplicationCandidateSceneIsolationResult.Failed(
                    ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                    "Candidate scene isolation gate threw while becoming inert.", exception);
            }
            if (_cleanupIsolationError.Success && !inert)
                _cleanupIsolationError = ApplicationCandidateSceneIsolationResult.Failed(
                    ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                    "Candidate scene isolation gate did not become inert.");
        }

        private bool HasOwnedSceneAfterLoadFailure()
        {
            try
            {
                return _graph.IsolationGate.HasOwnedScene;
            }
            catch (Exception exception)
            {
                // Ownership uncertainty is cleanup debt. Drain the exact unload path rather than
                // declaring the failed load safe to release.
                _cleanupException = Combine(_cleanupException, exception);
                return true;
            }
        }

        private void CompleteWithoutLoadedScene()
        {
            bool inert = !_isolationEntered;
            if (_isolationEntered)
            {
                try { inert = _graph.IsolationGate.IsInert; }
                catch (Exception exception) { _cleanupException = Combine(_cleanupException, exception); }
            }
            bool cleanupSucceeded = _cleanupIsolationError.Success && _cleanupException == null && inert;
            Volatile.Write(ref _status, (int)(cleanupSucceeded
                ? ApplicationCandidateSceneLeaseStatus.Released
                : ApplicationCandidateSceneLeaseStatus.CleanupFailed));
            Volatile.Write(ref _pumpOwnership, (int)(cleanupSucceeded
                ? ApplicationCandidatePumpOwnership.Disposed
                : ApplicationCandidatePumpOwnership.RollbackOwner));
        }

        private static Exception Combine(Exception first, Exception second)
        {
            if (first == null) return second;
            if (second == null) return first;
            return new AggregateException(first, second);
        }
    }
}
