using System;
using System.Globalization;
using System.Threading;
using MxFramework.ApplicationStartup.PlatformBridge;

namespace MxFramework.ApplicationStartup.Unity
{
    /// <summary>
    /// Main-thread Unity phase driver. Composition/run creation is AllocByDesign; warm Update and
    /// FixedUpdate are NoAlloc after initialization outside failure diagnostics and phase changes.
    /// </summary>
    public sealed class ApplicationUnityBootDriver
    {
        private struct RetirementSlot
        {
            public LiveApplicationStateRetirement Receipt;
            public bool Failed;
        }

        private readonly int _ownerThreadId;
        private readonly LiveApplicationStatePublisher _publisher;
        private readonly ApplicationPlatformLifecycleBridge _lifecycleBridge;
        private readonly IApplicationUnityStartupRunFactory _runFactory;
        private readonly IApplicationUnityStartupSurface _surface;
        private readonly ApplicationUnityStartupIntent _initialIntent;
        private ApplicationLiveFrameDriver _liveFrameDriver;
        private readonly RetirementSlot[] _retirementSlots;

        private ApplicationStartupCoordinator _coordinator;
        private ApplicationUnityBootRootState _rootState;
        private ApplicationUnityBootErrorCode _rootError;
        private string _creationStableErrorCode;
        private ApplicationLiveFrameTickResult _lastLiveFrame;
        private ApplicationPlatformLifecyclePumpStatus _lastLifecyclePump;
        private ApplicationUnityStartupSurfaceModel _surfaceModel;
        private double _updateElapsed;
        private double _fixedElapsed;
        private long _attempt;
        private long _lastRequestId;
        private bool _terminalProjected;
        private bool _shutdownCancellationSent;
        private int _updateInProgress;
        private int _fixedInProgress;

        public ApplicationUnityBootRootState RootState => _rootState;
        public ApplicationUnityBootErrorCode RootError => _rootError;
        public long Attempt => _attempt;
        public ApplicationStartupCoordinator ActiveCoordinator => _coordinator;
        public ApplicationUnityStartupSurfaceModel SurfaceModel => _surfaceModel;
        public ApplicationLiveFrameTickResult LastLiveFrame => _lastLiveFrame;
        public ApplicationPlatformLifecyclePumpStatus LastLifecyclePumpStatus => _lastLifecyclePump;

        internal static ApplicationUnityBootErrorCode TryCreate(
            ApplicationUnityBootComposition composition,
            IApplicationUnityStartupSurface surface,
            out ApplicationUnityBootDriver driver)
        {
            driver = null;
            if (composition == null || composition.Publisher == null ||
                composition.PublicationSaveFence == null || composition.LifecycleBridge == null ||
                composition.LifecycleReporter == null || composition.RunFactory == null ||
                !composition.InitialIntent.IsValid || composition.RetirementSlotCapacity <= 0 ||
                composition.RetirementSlotCapacity < composition.Publisher.RetirementCapacity)
                return ApplicationUnityBootErrorCode.InvalidComposition;
            if (composition.LifecycleReporter.OwnerThreadId != Environment.CurrentManagedThreadId)
                return ApplicationUnityBootErrorCode.WrongThread;
            if (!composition.Publisher.UsesPublicationSaveFence(composition.PublicationSaveFence) ||
                !composition.LifecycleBridge.UsesComposition(
                    composition.Publisher, composition.PublicationSaveFence, composition.LifecycleReporter))
                return ApplicationUnityBootErrorCode.ForeignComposition;

            // This is the only irreversible core claim and therefore occurs after every fallible
            // validation/allocation needed by the driver has completed.
            var slots = new RetirementSlot[composition.RetirementSlotCapacity];
            var candidate = new ApplicationUnityBootDriver(composition, surface, slots);
            if (!composition.Publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver frameDriver) || frameDriver == null)
                return ApplicationUnityBootErrorCode.FrameDriverAlreadyClaimed;
            candidate._liveFrameDriver = frameDriver;
            driver = candidate;
            return ApplicationUnityBootErrorCode.None;
        }

        private ApplicationUnityBootDriver(
            ApplicationUnityBootComposition composition,
            IApplicationUnityStartupSurface surface,
            RetirementSlot[] retirementSlots)
        {
            _ownerThreadId = Environment.CurrentManagedThreadId;
            _publisher = composition.Publisher;
            _lifecycleBridge = composition.LifecycleBridge;
            _runFactory = composition.RunFactory;
            _surface = surface;
            _initialIntent = composition.InitialIntent;
            _retirementSlots = retirementSlots;
            _rootState = ApplicationUnityBootRootState.Ready;
            _creationStableErrorCode = string.Empty;
            _lastLiveFrame = new ApplicationLiveFrameTickResult(
                ApplicationLiveFrameTickStatus.Boot, composition.Publisher.Current, -1L);
            ProjectModel();
        }

        /// <summary>Starts the composition-selected first run. AllocByDesign.</summary>
        public bool StartInitialRun()
        {
            ApplicationStartupState startupState = _coordinator == null
                ? ApplicationStartupState.Created
                : _coordinator.State;
            if (!IsOwnerThread() || _coordinator != null ||
                !ApplicationUnityStartupRequestPolicy.CanStart(_rootState, startupState, _attempt))
                return false;
            bool started = TryCreateFreshRun();
            // StartInitialRun is invoked before the first Unity Update. Keep the externally
            // observable model coherent even when the outer factory is explicitly unavailable.
            ProjectModel();
            return started;
        }

        public void RequestShutdown()
        {
            if (!IsOwnerThread() || IsShutdownState(_rootState)) return;
            _rootState = ApplicationUnityBootRootState.ShuttingDown;
            if (_coordinator != null && !_coordinator.IsTerminal)
            {
                _coordinator.RequestCancel();
                _shutdownCancellationSent = true;
            }
            _lifecycleBridge.RequestActiveCancellation();
            ProjectModel();
        }

        /// <summary>Executes one ordered Unity Update phase. NoAlloc after initialization in steady state.</summary>
        public ApplicationUnityDriverUpdateStatus Update(double deltaSeconds)
        {
            if (!IsOwnerThread()) return ApplicationUnityDriverUpdateStatus.WrongThread;
            if (_rootState == ApplicationUnityBootRootState.ShutdownComplete)
                return ApplicationUnityDriverUpdateStatus.ShutdownComplete;
            if (_rootState == ApplicationUnityBootRootState.ShutdownDebt)
                return ApplicationUnityDriverUpdateStatus.ShutdownDebt;
            if (!IsFiniteNonNegative(deltaSeconds)) return Fail(ApplicationUnityBootErrorCode.UpdateClockInvalid, ApplicationUnityDriverUpdateStatus.InvalidTime);
            if (Interlocked.CompareExchange(ref _updateInProgress, 1, 0) != 0)
                return ApplicationUnityDriverUpdateStatus.Reentrant;
            try
            {
                _updateElapsed += deltaSeconds;
                if (!IsFiniteNonNegative(_updateElapsed))
                    return Fail(ApplicationUnityBootErrorCode.UpdateClockInvalid, ApplicationUnityDriverUpdateStatus.InvalidTime);

                if (_rootState != ApplicationUnityBootRootState.ShuttingDown &&
                    (!IsTerminalRootState(_rootState) || IsRecoverableRootFailure()))
                    ConsumeOneSurfaceRequest();

                TickCoordinator(deltaSeconds);

                bool checkpointSafe = _rootState != ApplicationUnityBootRootState.ShuttingDown &&
                    (_coordinator == null || !IsUnsafeStartupState(_coordinator.State));
                try { _lastLifecyclePump = _lifecycleBridge.Pump(checkpointSafe); }
                catch { return Fail(ApplicationUnityBootErrorCode.LifecyclePumpFailed, ApplicationUnityDriverUpdateStatus.Succeeded); }

                TickExistingRetirements();
                DequeueRetirements();
                EvaluateRootState();
                ProjectModel();
                ApplySurface();

                if (_rootState == ApplicationUnityBootRootState.ShutdownComplete)
                    return ApplicationUnityDriverUpdateStatus.ShutdownComplete;
                if (_rootState == ApplicationUnityBootRootState.ShutdownDebt)
                    return ApplicationUnityDriverUpdateStatus.ShutdownDebt;
                return ApplicationUnityDriverUpdateStatus.Succeeded;
            }
            finally
            {
                Volatile.Write(ref _updateInProgress, 0);
            }
        }

        /// <summary>Executes the unique live fixed-frame attempt. NoAlloc after initialization.</summary>
        public ApplicationUnityDriverUpdateStatus FixedUpdate(double deltaSeconds)
        {
            if (!IsOwnerThread()) return ApplicationUnityDriverUpdateStatus.WrongThread;
            if (_rootState == ApplicationUnityBootRootState.ShutdownComplete)
                return ApplicationUnityDriverUpdateStatus.ShutdownComplete;
            if (_rootState == ApplicationUnityBootRootState.ShutdownDebt)
                return ApplicationUnityDriverUpdateStatus.ShutdownDebt;
            if (!IsFiniteNonNegative(deltaSeconds)) return Fail(ApplicationUnityBootErrorCode.FixedClockInvalid, ApplicationUnityDriverUpdateStatus.InvalidTime);
            if (Interlocked.CompareExchange(ref _fixedInProgress, 1, 0) != 0)
                return ApplicationUnityDriverUpdateStatus.Reentrant;
            try
            {
                _fixedElapsed += deltaSeconds;
                if (!IsFiniteNonNegative(_fixedElapsed))
                    return Fail(ApplicationUnityBootErrorCode.FixedClockInvalid, ApplicationUnityDriverUpdateStatus.InvalidTime);
                try { _lastLiveFrame = _liveFrameDriver.Tick(deltaSeconds, _fixedElapsed); }
                catch { return Fail(ApplicationUnityBootErrorCode.LiveFrameFailed, ApplicationUnityDriverUpdateStatus.Succeeded); }
                return ApplicationUnityDriverUpdateStatus.Succeeded;
            }
            finally
            {
                Volatile.Write(ref _fixedInProgress, 0);
            }
        }

        internal void RecordUnexpectedDestroy()
        {
            if (_rootState != ApplicationUnityBootRootState.ShutdownComplete)
            {
                _rootError = ApplicationUnityBootErrorCode.UnexpectedDestroy;
                _rootState = ApplicationUnityBootRootState.ShutdownDebt;
                ProjectModel();
            }
        }

        private void ConsumeOneSurfaceRequest()
        {
            if (_surface == null) return;
            ApplicationUnityStartupSurfaceRequest request;
            try
            {
                if (!_surface.TryDequeueRequest(out request)) return;
            }
            catch
            {
                SetFailure(ApplicationUnityBootErrorCode.SurfaceCallbackFailed);
                return;
            }
            if (!request.IsValid || request.RequestId <= _lastRequestId) return;
            _lastRequestId = request.RequestId;
            ApplicationStartupState startupState = _coordinator == null
                ? ApplicationStartupState.Created
                : _coordinator.State;
            if (request.Kind == ApplicationUnityStartupRequestKind.Cancel)
            {
                if (_coordinator != null &&
                    ApplicationUnityStartupRequestPolicy.CanCancel(startupState, _coordinator.CancelRequested))
                    _coordinator.RequestCancel();
                return;
            }
            if (request.Kind == ApplicationUnityStartupRequestKind.Start)
            {
                if (_coordinator == null &&
                    ApplicationUnityStartupRequestPolicy.CanStart(_rootState, startupState, _attempt))
                    TryCreateFreshRun();
                return;
            }
            if (request.Kind == ApplicationUnityStartupRequestKind.Retry)
            {
                if (ApplicationUnityStartupRequestPolicy.CanRetry(_rootState, _rootError, startupState) &&
                    (_coordinator == null || (_coordinator.IsTerminal && _terminalProjected)))
                    TryCreateFreshRun();
            }
        }

        private bool TryCreateFreshRun()
        {
            if (_coordinator != null)
            {
                if (!_coordinator.IsTerminal || !_terminalProjected) return false;
                try { _coordinator.Dispose(); }
                catch
                {
                    SetFailure(ApplicationUnityBootErrorCode.PriorRunReleaseFailed);
                    return false;
                }
                _coordinator = null;
            }
            if (_rootError == ApplicationUnityBootErrorCode.RunFactoryUnavailable)
            {
                _rootError = ApplicationUnityBootErrorCode.None;
                _rootState = ApplicationUnityBootRootState.Ready;
            }
            if (_attempt == long.MaxValue)
            {
                SetFailure(ApplicationUnityBootErrorCode.AttemptExhausted);
                return false;
            }
            long nextAttempt = _attempt + 1L;
            // Commit the attempt before crossing the factory boundary. Every invocation therefore
            // observes a unique monotonic id, including unavailable, rejected, and throwing calls.
            _attempt = nextAttempt;
            _creationStableErrorCode = string.Empty;
            ApplicationUnityStartupRunCreateResult result;
            try { result = _runFactory.TryCreate(in _initialIntent, nextAttempt, _publisher); }
            catch
            {
                SetFailure(ApplicationUnityBootErrorCode.RunFactoryException);
                return false;
            }
            if (!result.Success)
            {
                _creationStableErrorCode = result.StableErrorCode;
                SetFailure(result.Status == ApplicationUnityStartupRunCreateStatus.Unavailable && result.Coordinator == null
                    ? ApplicationUnityBootErrorCode.RunFactoryUnavailable
                    : ApplicationUnityBootErrorCode.RunFactoryRejected);
                return false;
            }
            ApplicationStartupCoordinator candidate = result.Coordinator;
            if (candidate.State != ApplicationStartupState.Created || candidate.IsTerminal)
            {
                SetFailure(ApplicationUnityBootErrorCode.InvalidRun);
                return false;
            }
            if (!candidate.UsesPublisher(_publisher))
            {
                SetFailure(ApplicationUnityBootErrorCode.ForeignPublisher);
                return false;
            }
            string runId = "unity-boot-" + nextAttempt.ToString(CultureInfo.InvariantCulture);
            try { candidate.Start(runId); }
            catch
            {
                SetFailure(ApplicationUnityBootErrorCode.RunStartFailed);
                return false;
            }
            _coordinator = candidate;
            _terminalProjected = false;
            _rootError = ApplicationUnityBootErrorCode.None;
            _creationStableErrorCode = string.Empty;
            _rootState = candidate.IsTerminal ? ApplicationUnityBootRootState.Ready : ApplicationUnityBootRootState.Running;
            return true;
        }

        private void TickCoordinator(double deltaSeconds)
        {
            if (_coordinator == null || _coordinator.IsTerminal) return;
            if (_rootState == ApplicationUnityBootRootState.ShuttingDown && !_shutdownCancellationSent)
            {
                _coordinator.RequestCancel();
                _shutdownCancellationSent = true;
            }
            var context = new ApplicationStartupTickContext(deltaSeconds, _updateElapsed);
            _coordinator.Tick(in context);
        }

        private void TickExistingRetirements()
        {
            for (int i = 0; i < _retirementSlots.Length; i++)
            {
                LiveApplicationStateRetirement receipt = _retirementSlots[i].Receipt;
                if (receipt == null || _retirementSlots[i].Failed) continue;
                try { receipt.TickRetirement(); }
                catch
                {
                    _retirementSlots[i].Failed = true;
                    SetFailure(ApplicationUnityBootErrorCode.RetirementPumpFailed);
                    continue;
                }
                ObserveRetirement(i, receipt);
            }
        }

        private void DequeueRetirements()
        {
            for (int i = 0; i < _retirementSlots.Length; i++)
            {
                if (_retirementSlots[i].Receipt != null) continue;
                if (!_publisher.TryDequeueRetired(out LiveApplicationStateRetirement receipt)) break;
                _retirementSlots[i].Receipt = receipt;
                try { receipt.BeginRetirement(); }
                catch
                {
                    _retirementSlots[i].Failed = true;
                    SetFailure(ApplicationUnityBootErrorCode.RetirementPumpFailed);
                    continue;
                }
                ObserveRetirement(i, receipt);
            }
        }

        private void ObserveRetirement(int slot, LiveApplicationStateRetirement receipt)
        {
            if (!receipt.IsTerminal) return;
            if (receipt.RetirementStatus == ApplicationLiveRetirementStatus.Succeeded && receipt.OwnershipDebt.IsNone)
            {
                _retirementSlots[slot].Receipt = null;
                _retirementSlots[slot].Failed = false;
                return;
            }
            _retirementSlots[slot].Failed = true;
            if (_rootError == ApplicationUnityBootErrorCode.None)
                _rootError = ApplicationUnityBootErrorCode.RetirementOwnershipDebt;
        }

        private void EvaluateRootState()
        {
            if (_rootState == ApplicationUnityBootRootState.ShuttingDown)
            {
                if (_coordinator != null && !_coordinator.IsTerminal) return;
                if (_lifecycleBridge.HasActiveCheckpoint) return;
                int active = 0;
                int failed = 0;
                CountRetirements(out active, out failed);
                if (active != 0) return;
                if (_publisher.Current.HasRuntimeSession)
                {
                    if (_rootError == ApplicationUnityBootErrorCode.None ||
                        _rootError == ApplicationUnityBootErrorCode.RunFactoryUnavailable)
                        _rootError = ApplicationUnityBootErrorCode.LiveSessionStillOwned;
                    _rootState = ApplicationUnityBootRootState.ShutdownDebt;
                    return;
                }
                _rootState = failed == 0
                    ? ApplicationUnityBootRootState.ShutdownComplete
                    : ApplicationUnityBootRootState.ShutdownDebt;
                return;
            }
            if (_rootState == ApplicationUnityBootRootState.Failed || _rootState == ApplicationUnityBootRootState.ShutdownDebt)
                return;
            _rootState = _coordinator != null && !_coordinator.IsTerminal
                ? ApplicationUnityBootRootState.Running
                : ApplicationUnityBootRootState.Ready;
        }

        private void ProjectModel()
        {
            ApplicationStartupCoordinator run = _coordinator;
            LiveApplicationState live = _publisher.Current;
            int occupied;
            int failed;
            CountRetirements(out occupied, out failed);
            string debtDomain = string.Empty;
            string debtCode = string.Empty;
            for (int i = 0; i < _retirementSlots.Length; i++)
            {
                LiveApplicationStateRetirement receipt = _retirementSlots[i].Receipt;
                if (receipt == null || !_retirementSlots[i].Failed) continue;
                ApplicationLiveRetirementOwnershipDebt debt = receipt.OwnershipDebt;
                debtDomain = debt.DomainId;
                debtCode = debt.StableCode;
                break;
            }
            ApplicationStartupError error = run == null ? default : run.ForwardError;
            _surfaceModel = new ApplicationUnityStartupSurfaceModel(
                _rootState, _rootError, _attempt,
                run == null ? default : run.PlanId, run == null ? string.Empty : run.RunId,
                run == null ? ApplicationStartupState.Created : run.State,
                DeterminePhase(run), run == null ? default : run.CurrentStepId,
                run == null ? 0f : run.ForwardProgress, run == null ? 0f : run.RollbackProgress,
                run != null && run.CancelRequested, error.Code, error.InnerError.Code,
                live.Revision, live.HasRuntimeSession, _lastLiveFrame.Status,
                _lastLifecyclePump, _lifecycleBridge.LastCheckpointStatus,
                occupied + failed, failed, debtDomain, debtCode, _creationStableErrorCode);
        }

        private void ApplySurface()
        {
            if (_surface != null)
            {
                try { _surface.Apply(in _surfaceModel); }
                catch { SetFailure(ApplicationUnityBootErrorCode.SurfaceCallbackFailed); }
            }
            if (_coordinator != null && _coordinator.IsTerminal) _terminalProjected = true;
        }

        private void CountRetirements(out int active, out int failed)
        {
            active = 0;
            failed = 0;
            for (int i = 0; i < _retirementSlots.Length; i++)
            {
                if (_retirementSlots[i].Receipt == null) continue;
                if (_retirementSlots[i].Failed) failed++;
                else active++;
            }
        }

        private ApplicationUnityDriverUpdateStatus Fail(ApplicationUnityBootErrorCode code, ApplicationUnityDriverUpdateStatus status)
        {
            SetFailure(code);
            ProjectModel();
            return status;
        }

        private void SetFailure(ApplicationUnityBootErrorCode code)
        {
            if (_rootError == ApplicationUnityBootErrorCode.None) _rootError = code;
            if (_rootState != ApplicationUnityBootRootState.ShuttingDown && !IsShutdownState(_rootState))
                _rootState = ApplicationUnityBootRootState.Failed;
        }

        private bool IsOwnerThread() => Environment.CurrentManagedThreadId == _ownerThreadId;
        private bool IsRecoverableRootFailure() =>
            ApplicationUnityStartupRequestPolicy.CanRetry(
                _rootState, _rootError, ApplicationStartupState.Created);
        private static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        private static bool IsUnsafeStartupState(ApplicationStartupState state) =>
            state == ApplicationStartupState.Running || state == ApplicationStartupState.Draining || state == ApplicationStartupState.RollingBack;
        private static bool IsShutdownState(ApplicationUnityBootRootState state) =>
            state == ApplicationUnityBootRootState.ShuttingDown || state == ApplicationUnityBootRootState.ShutdownComplete || state == ApplicationUnityBootRootState.ShutdownDebt;
        private static bool IsTerminalRootState(ApplicationUnityBootRootState state) =>
            state == ApplicationUnityBootRootState.Failed || state == ApplicationUnityBootRootState.ShutdownComplete || state == ApplicationUnityBootRootState.ShutdownDebt;

        private static ApplicationStartupPhase DeterminePhase(ApplicationStartupCoordinator run)
        {
            if (run == null) return ApplicationStartupPhase.None;
            if (!run.ForwardError.IsNone) return run.ForwardError.Phase;
            if (run.State == ApplicationStartupState.RollingBack) return ApplicationStartupPhase.Rollback;
            if (run.State == ApplicationStartupState.Succeeded) return ApplicationStartupPhase.Commit;
            return run.State == ApplicationStartupState.Created ? ApplicationStartupPhase.None : ApplicationStartupPhase.Forward;
        }
    }
}
