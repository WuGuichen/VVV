using System;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup
{
    /// <summary>
    /// Sequential noEngine startup runner. Start and phase transitions are LowFreqAlloc;
    /// steady Tick, progress, status, and cancellation polling are NoAlloc after Start.
    /// </summary>
    public sealed class ApplicationStartupCoordinator : IDisposable
    {
        private readonly ApplicationStartupPlan _plan;
        private readonly ILiveApplicationStatePublisher _publisher;
        private readonly ApplicationStartupError[] _rollbackErrors;
        private readonly ApplicationStartupError[] _cleanupWarnings;

        private IApplicationStartupTransaction _transaction;
        private LiveApplicationState _expectedState;
        private IApplicationStartupStepOperation _operation;
        private string _runId;
        private int _forwardIndex;
        private int _completedCount;
        private int _rollbackIndex;
        private int _rollbackErrorCount;
        private int _cleanupWarningCount;
        private float _completedWeight;
        private float _rollbackCompletedWeight;
        private double _elapsedSeconds;
        private double _stepElapsedSeconds;
        private bool _cancelRequested;
        private bool _cancelSent;
        private bool _operationFaultDraining;
        private bool _faultDrainRequested;
        private bool _commitExceptionPending;
        private ApplicationStartupState _requestedTerminalState;
        private ApplicationStartupError _forwardError;
        private RuntimeOperationStatus _drainedOperationStatus;
        private RuntimeOperationError _drainedOperationError;
        private IApplicationStartupInnerErrorDetail _drainedInnerDetail;

        public ApplicationStartupCoordinator(ApplicationStartupPlan plan, ILiveApplicationStatePublisher publisher)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _rollbackErrors = new ApplicationStartupError[plan.StepCount + 2];
            _cleanupWarnings = new ApplicationStartupError[2];
            State = ApplicationStartupState.Created;
            CurrentStepId = default;
            _requestedTerminalState = ApplicationStartupState.Failed;
        }

        public ApplicationStartupState State { get; private set; }
        public ApplicationStartupPlanId PlanId => _plan.PlanId;
        public string RunId => _runId ?? string.Empty;
        public ApplicationStartupStepId CurrentStepId { get; private set; }
        public int CompletedStepCount => _completedCount;
        public bool CancelRequested => _cancelRequested;
        public double ElapsedSeconds => _elapsedSeconds;
        public ApplicationStartupError ForwardError => _forwardError;
        public RuntimeOperationStatus DrainedOperationStatus => _drainedOperationStatus;
        public RuntimeOperationError DrainedOperationError => _drainedOperationError;
        public IApplicationStartupInnerErrorDetail DrainedInnerDetail => _drainedInnerDetail;
        public int RollbackErrorCount => _rollbackErrorCount;
        public int CleanupWarningCount => _cleanupWarningCount;
        public IApplicationStartupStepOperation CurrentOperation => _operation;
        public bool IsTerminal => IsTerminalState(State);
        internal bool UsesPublisher(LiveApplicationStatePublisher publisher) =>
            ReferenceEquals(_publisher, publisher);

        public float ForwardProgress
        {
            get
            {
                if (State == ApplicationStartupState.Succeeded)
                {
                    return 1f;
                }

                float current = 0f;
                if (_operation != null && (State == ApplicationStartupState.Running || State == ApplicationStartupState.Draining))
                {
                    float progress = _operation.Progress;
                    if (progress < 0f) progress = 0f;
                    if (progress > 1f) progress = 1f;
                    current = _plan.GetDescriptorInExecutionOrder(_forwardIndex).ProgressWeight * progress;
                }

                float value = (_completedWeight + current) / _plan.TotalWeight;
                if (value < 0f) return 0f;
                if (value > 1f) return 1f;
                return value;
            }
        }

        public float RollbackProgress
        {
            get
            {
                if (_completedWeight <= 0f)
                {
                    return State == ApplicationStartupState.RollingBack ? 1f : 0f;
                }

                float current = 0f;
                if (State == ApplicationStartupState.RollingBack && _operation != null && _rollbackIndex >= 0)
                {
                    float progress = _operation.Progress;
                    if (progress < 0f) progress = 0f;
                    if (progress > 1f) progress = 1f;
                    current = _plan.GetDescriptorInExecutionOrder(_rollbackIndex).ProgressWeight * progress;
                }

                float value = (_rollbackCompletedWeight + current) / _completedWeight;
                if (value < 0f) return 0f;
                if (value > 1f) return 1f;
                return value;
            }
        }

        public ApplicationStartupError GetRollbackError(int index)
        {
            if ((uint)index >= (uint)_rollbackErrorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _rollbackErrors[index];
        }

        public ApplicationStartupError GetCleanupWarning(int index)
        {
            if ((uint)index >= (uint)_cleanupWarningCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _cleanupWarnings[index];
        }

        public void Start(string runId)
        {
            if (State != ApplicationStartupState.Created)
            {
                throw new InvalidOperationException("A startup run is single-use.");
            }

            _runId = ApplicationStartupPlanId.Validate(runId, nameof(runId));
            try
            {
                _expectedState = _publisher.Current;
            }
            catch (Exception exception)
            {
                FailWithoutTransaction(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, "Reading the current live state threw.", exception);
                return;
            }

            if (_expectedState == null)
            {
                FailWithoutTransaction(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, "Publisher Current must never be null.", null);
                return;
            }

            try
            {
                _transaction = _plan.TransactionFactory.Create(_plan.PlanId);
            }
            catch (Exception exception)
            {
                FailWithoutTransaction(ApplicationStartupErrorCode.StepBeginFailed, ApplicationStartupPhase.Forward, default, "Transaction creation threw.", exception);
                return;
            }

            if (_transaction == null || string.IsNullOrWhiteSpace(_transaction.TransactionId))
            {
                FailWithoutTransaction(ApplicationStartupErrorCode.InvalidPlan, ApplicationStartupPhase.Forward, default, "Transaction factory returned an invalid transaction.", null);
                return;
            }

            State = ApplicationStartupState.Running;
        }

        public void RequestCancel()
        {
            if (State == ApplicationStartupState.Running || State == ApplicationStartupState.Draining)
            {
                _cancelRequested = true;
            }
        }

        /// <summary>Advances the active operation. NoAlloc after Start outside phase transitions and user operation code.</summary>
        public void Tick(in ApplicationStartupTickContext context)
        {
            if (State != ApplicationStartupState.Running && State != ApplicationStartupState.Draining && State != ApplicationStartupState.RollingBack)
            {
                return;
            }

            _elapsedSeconds += context.DeltaSeconds;

            if (State == ApplicationStartupState.RollingBack)
            {
                TickRollback(in context);
                return;
            }

            if (_operation == null)
            {
                if (_commitExceptionPending)
                {
                    ResolveCommitException();
                    return;
                }

                if (_cancelRequested)
                {
                    _requestedTerminalState = ApplicationStartupState.Cancelled;
                    _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.Cancelled, ApplicationStartupPhase.Forward, default, default, null, "Startup cancellation requested.", null);
                    StartRollback();
                    return;
                }

                if (_forwardIndex >= _plan.StepCount)
                {
                    TryCommit();
                    return;
                }

                BeginForwardOperation();
                if (_operation == null)
                {
                    return;
                }
            }

            _stepElapsedSeconds += context.DeltaSeconds;
            ApplicationStartupStepDescriptor descriptor = _plan.GetDescriptorInExecutionOrder(_forwardIndex);

            if (State == ApplicationStartupState.Running && _cancelRequested)
            {
                LatchDrain(
                    ApplicationStartupState.Cancelled,
                    new ApplicationStartupError(ApplicationStartupErrorCode.Cancelled, ApplicationStartupPhase.Forward, descriptor.StepId, default, null, "Startup cancellation requested.", null));
            }
            else if (State == ApplicationStartupState.Running && descriptor.TimeoutSeconds > 0d && _stepElapsedSeconds >= descriptor.TimeoutSeconds)
            {
                LatchDrain(
                    ApplicationStartupState.TimedOut,
                    new ApplicationStartupError(ApplicationStartupErrorCode.Timeout, ApplicationStartupPhase.Forward, descriptor.StepId, default, null, "Startup step timed out.", null));
            }

            if (State == ApplicationStartupState.Draining && IsTerminalOperation(_operation.Status))
            {
                CompleteForwardTerminal(descriptor);
                return;
            }


            if (IsTerminalOperation(_operation.Status))
            {
                CompleteForwardTerminal(descriptor);
                return;
            }

            if (_operationFaultDraining)
            {
                TickFaultDrain(in context, descriptor, false);
                return;
            }

            if (State == ApplicationStartupState.Draining && !_cancelSent && _operation.CanCancel)
            {
                _cancelSent = true;
                try
                {
                    _operation.RequestCancel();
                }
                catch (Exception exception)
                {
                    // RequestCancel is advisory. A throwing provider still owns potentially in-flight
                    // work, so keep draining it instead of racing rollback against an unknown state.
                    _forwardError = new ApplicationStartupError(
                        _requestedTerminalState == ApplicationStartupState.TimedOut ? ApplicationStartupErrorCode.Timeout : ApplicationStartupErrorCode.Cancelled,
                        ApplicationStartupPhase.Forward,
                        descriptor.StepId,
                        _operation.Error,
                        _operation.InnerErrorDetail,
                        "Startup cancellation dispatch threw; draining continued to a truthful terminal state.",
                        exception);
                }

                if (IsTerminalOperation(_operation.Status))
                {
                    CompleteForwardTerminal(descriptor);
                    return;
                }
            }

            try
            {
                _operation.Tick(in context);
            }
            catch (Exception exception)
            {
                _forwardError = new ApplicationStartupError(
                    ApplicationStartupErrorCode.StepException,
                    ApplicationStartupPhase.Forward,
                    descriptor.StepId,
                    _operation.Error,
                    _operation.InnerErrorDetail,
                    "Startup step Tick threw.",
                    exception);
                _requestedTerminalState = ApplicationStartupState.Failed;
                State = ApplicationStartupState.Draining;
                _operationFaultDraining = true;
                RequestFaultDrain(exception);
                if (IsTerminalOperation(_operation.Status))
                {
                    CompleteForwardTerminal(descriptor);
                }
                return;
            }

            if (!IsTerminalOperation(_operation.Status))
            {
                return;
            }

            CompleteForwardTerminal(descriptor);
        }

        public ApplicationStartupSnapshot CaptureSnapshot()
        {
            return new ApplicationStartupSnapshot(
                RunId,
                PlanId,
                _transaction == null ? string.Empty : _transaction.TransactionId,
                State,
                CurrentStepId,
                _completedCount,
                ForwardProgress,
                RollbackProgress,
                _cancelRequested,
                _elapsedSeconds,
                _stepElapsedSeconds,
                _forwardError,
                _rollbackErrorCount,
                _cleanupWarningCount,
                _operation == null ? RuntimeOperationStatus.Pending : _operation.Status,
                _operation == null ? default : _operation.Error,
                _operation == null ? null : _operation.InnerErrorDetail,
                _drainedOperationStatus,
                _drainedOperationError,
                _drainedInnerDetail);
        }

        public void Dispose()
        {
            if (State == ApplicationStartupState.Disposed)
            {
                return;
            }

            if (!IsTerminal && State != ApplicationStartupState.Created)
            {
                throw new InvalidOperationException("Drain and finalize the startup run before disposal.");
            }

            _transaction?.Dispose();
            _transaction = null;
            _operation = null;
            State = ApplicationStartupState.Disposed;
        }

        private void BeginForwardOperation()
        {
            ApplicationStartupStepDescriptor descriptor = _plan.GetDescriptorInExecutionOrder(_forwardIndex);
            IApplicationStartupStep step = _plan.GetStepInExecutionOrder(_forwardIndex);
            CurrentStepId = descriptor.StepId;
            _stepElapsedSeconds = 0d;
            _cancelSent = false;
            _operationFaultDraining = false;
            _faultDrainRequested = false;

            try
            {
                var context = new ApplicationStartupStepContext(RunId, _transaction);
                _operation = step.Begin(in context);
            }
            catch (Exception exception)
            {
                _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.StepBeginFailed, ApplicationStartupPhase.Forward, descriptor.StepId, default, null, "Startup step Begin threw.", exception);
                _requestedTerminalState = ApplicationStartupState.Failed;
                StartRollback();
                return;
            }

            if (_operation == null)
            {
                _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.StepBeginFailed, ApplicationStartupPhase.Forward, descriptor.StepId, default, null, "Startup step returned a null operation.", null);
                _requestedTerminalState = ApplicationStartupState.Failed;
                StartRollback();
            }
        }

        private void LatchDrain(ApplicationStartupState terminalState, ApplicationStartupError error)
        {
            State = ApplicationStartupState.Draining;
            _requestedTerminalState = terminalState;
            _forwardError = error;
        }

        private void CompleteForwardTerminal(ApplicationStartupStepDescriptor descriptor)
        {
            RuntimeOperationStatus terminalStatus = _operation.Status;
            RuntimeOperationError terminalError = _operation.Error;
            IApplicationStartupInnerErrorDetail terminalDetail = _operation.InnerErrorDetail;

            if (State == ApplicationStartupState.Draining)
            {
                _drainedOperationStatus = terminalStatus;
                _drainedOperationError = terminalError;
                _drainedInnerDetail = terminalDetail;
                _forwardError = new ApplicationStartupError(
                    _forwardError.Code,
                    _forwardError.Phase,
                    _forwardError.StepId,
                    terminalError,
                    terminalDetail,
                    _forwardError.Message,
                    _forwardError.Exception);
                StartRollback();
                return;
            }

            if (terminalStatus == RuntimeOperationStatus.Succeeded)
            {
                _completedWeight += descriptor.ProgressWeight;
                _completedCount++;
                _forwardIndex++;
                _operation = null;
                CurrentStepId = default;
                _stepElapsedSeconds = 0d;
                _cancelSent = false;
                return;
            }

            ApplicationStartupErrorCode code = terminalStatus == RuntimeOperationStatus.Cancelled
                ? ApplicationStartupErrorCode.StepCancelled
                : terminalStatus == RuntimeOperationStatus.TimedOut
                    ? ApplicationStartupErrorCode.StepTimedOut
                    : ApplicationStartupErrorCode.StepFailed;
            _forwardError = new ApplicationStartupError(
                code,
                ApplicationStartupPhase.Forward,
                descriptor.StepId,
                terminalError,
                terminalDetail,
                "Startup step reached a non-success terminal state.",
                null);
            _requestedTerminalState = ApplicationStartupState.Failed;
            StartRollback();
        }

        private void TryCommit()
        {
            ApplicationStartupCommitResult result;
            try
            {
                result = _transaction.TryCommit(_expectedState, _publisher);
            }
            catch (Exception exception)
            {
                _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Transaction commit threw; resolving the recorded publication outcome.", exception);
                _commitExceptionPending = true;
                ResolveCommitException();
                return;
            }

            if (result.Success)
            {
                ApplicationStartupCommitObservation observation = _transaction.CommitObservation;
                if (observation.Outcome != ApplicationStartupPublicationOutcome.Published ||
                    !ReferenceEquals(observation.PublishedState, result.PublishedState))
                {
                    _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Transaction reported success without a deterministic published observation.", null);
                    _commitExceptionPending = true;
                    State = ApplicationStartupState.Draining;
                    return;
                }

                CompleteCommittedTransaction();
                return;
            }

            // The shared publication/save fence rejected this attempt before candidate
            // reservation or ownership handoff. Keep the transaction rollback-owned and retry
            // from the next coordinator tick after the save reaches a truthful terminal state.
            if (result.Status == ApplicationStartupCommitStatus.PublicationDeferred)
            {
                return;
            }

            if (_transaction.CommitObservation.Outcome == ApplicationStartupPublicationOutcome.Published)
            {
                CompleteCommittedTransaction();
                return;
            }

            if (_transaction.CommitObservation.Outcome == ApplicationStartupPublicationOutcome.NotAttempted)
            {
                _forwardError = new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Transaction returned without a deterministic publication outcome.", null);
                _commitExceptionPending = true;
                State = ApplicationStartupState.Draining;
                return;
            }

            _forwardError = result.Error.IsNone
                ? new ApplicationStartupError(
                    result.Status == ApplicationStartupCommitStatus.PublicationConflict ? ApplicationStartupErrorCode.PublicationConflict : ApplicationStartupErrorCode.CommitFailed,
                    ApplicationStartupPhase.Commit,
                    default,
                    default,
                    null,
                    "Transaction commit failed.",
                    null)
                : result.Error;
            _requestedTerminalState = ApplicationStartupState.Failed;
            StartRollback();
        }

        private void StartRollback()
        {
            _operation = null;
            CurrentStepId = default;
            _rollbackIndex = _completedCount - 1;
            _rollbackCompletedWeight = 0f;
            _operationFaultDraining = false;
            _faultDrainRequested = false;
            _commitExceptionPending = false;
            State = ApplicationStartupState.RollingBack;
        }

        private void TickRollback(in ApplicationStartupTickContext context)
        {
            while (_operation == null && _rollbackIndex >= 0)
            {
                ApplicationStartupStepDescriptor descriptor = _plan.GetDescriptorInExecutionOrder(_rollbackIndex);
                IApplicationStartupStep step = _plan.GetStepInExecutionOrder(_rollbackIndex);
                CurrentStepId = descriptor.StepId;
                try
                {
                    var rollbackContext = new ApplicationStartupRollbackContext(RunId, _transaction, _forwardError);
                    _operation = step.BeginRollback(in rollbackContext);
                }
                catch (Exception exception)
                {
                    AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackBeginFailed, ApplicationStartupPhase.Rollback, descriptor.StepId, default, null, "Rollback Begin threw.", exception));
                    CompleteRollbackStep(descriptor);
                    continue;
                }

                if (_operation == null)
                {
                    AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackBeginFailed, ApplicationStartupPhase.Rollback, descriptor.StepId, default, null, "Rollback returned a null operation.", null));
                    CompleteRollbackStep(descriptor);
                }
            }

            if (_rollbackIndex < 0)
            {
                FinalizeRollback();
                return;
            }

            ApplicationStartupStepDescriptor currentDescriptor = _plan.GetDescriptorInExecutionOrder(_rollbackIndex);
            if (IsTerminalOperation(_operation.Status))
            {
                if (!_operationFaultDraining && _operation.Status != RuntimeOperationStatus.Succeeded)
                {
                    AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackFailed, ApplicationStartupPhase.Rollback, currentDescriptor.StepId, _operation.Error, _operation.InnerErrorDetail, "Rollback operation failed.", null));
                }

                _operation = null;
                CompleteRollbackStep(currentDescriptor);
                return;
            }

            if (_operationFaultDraining)
            {
                TickFaultDrain(in context, currentDescriptor, true);
                return;
            }

            try
            {
                _operation.Tick(in context);
            }
            catch (Exception exception)
            {
                AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackException, ApplicationStartupPhase.Rollback, currentDescriptor.StepId, _operation.Error, _operation.InnerErrorDetail, "Rollback Tick threw; retaining ownership until a safe terminal boundary.", exception));
                _operationFaultDraining = true;
                RequestFaultDrain(exception);
                return;
            }

            if (!IsTerminalOperation(_operation.Status))
            {
                return;
            }

            if (_operation.Status != RuntimeOperationStatus.Succeeded)
            {
                AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackFailed, ApplicationStartupPhase.Rollback, currentDescriptor.StepId, _operation.Error, _operation.InnerErrorDetail, "Rollback operation failed.", null));
            }

            _operation = null;
            CompleteRollbackStep(currentDescriptor);
        }

        private void CompleteRollbackStep(ApplicationStartupStepDescriptor descriptor)
        {
            _rollbackCompletedWeight += descriptor.ProgressWeight;
            _rollbackIndex--;
            CurrentStepId = default;
            _operationFaultDraining = false;
            _faultDrainRequested = false;
        }

        private void RequestFaultDrain(Exception exception)
        {
            if (_faultDrainRequested)
            {
                return;
            }

            _faultDrainRequested = true;
            if (_operation is IApplicationStartupFaultDrainOperation faultDrain)
            {
                try
                {
                    faultDrain.RequestFaultDrain(exception);
                }
                catch
                {
                    // Fail closed: retain the operation and retry only its dedicated drain pump.
                }
            }
        }

        private void TickFaultDrain(in ApplicationStartupTickContext context, ApplicationStartupStepDescriptor descriptor, bool rollback)
        {
            if (!(_operation is IApplicationStartupFaultDrainOperation faultDrain))
            {
                // A legacy operation may only throw with a strong exception guarantee and must already
                // be terminal. Staying here is deliberately fail-closed instead of racing rollback.
                return;
            }

            try
            {
                faultDrain.TickFaultDrain(in context);
            }
            catch
            {
                return;
            }

            if (!IsTerminalOperation(_operation.Status))
            {
                return;
            }

            if (rollback)
            {
                _operation = null;
                CompleteRollbackStep(descriptor);
            }
            else
            {
                CompleteForwardTerminal(descriptor);
            }
        }

        private void ResolveCommitException()
        {
            ApplicationStartupCommitObservation observation = _transaction.CommitObservation;
            if (observation.Outcome == ApplicationStartupPublicationOutcome.NotAttempted)
            {
                // Publication ownership is unresolved. Retain the transaction and fail closed until its
                // observation becomes deterministic; never rollback or dispose a possibly published lease.
                State = ApplicationStartupState.Draining;
                return;
            }

            _commitExceptionPending = false;
            if (observation.Outcome == ApplicationStartupPublicationOutcome.Published)
            {
                CompleteCommittedTransaction();
                return;
            }

            _requestedTerminalState = ApplicationStartupState.Failed;
            StartRollback();
        }

        private void CompleteCommittedTransaction()
        {
            try
            {
                _transaction.Dispose();
            }
            catch (Exception exception)
            {
                AddCleanupWarning(new ApplicationStartupError(ApplicationStartupErrorCode.CleanupIncomplete, ApplicationStartupPhase.Cleanup, default, default, null, "Committed transaction scaffold disposal failed.", exception));
            }
            _transaction = null;
            State = ApplicationStartupState.Succeeded;
        }

        private void FinalizeRollback()
        {
            if (_transaction != null)
            {
                try
                {
                    ApplicationStartupRollbackResult result = _transaction.FinalizeRollback();
                    if (!result.Success)
                    {
                        AddRollbackError(result.Error.IsNone
                            ? new ApplicationStartupError(ApplicationStartupErrorCode.CleanupIncomplete, ApplicationStartupPhase.Cleanup, default, default, null, "Transaction cleanup was incomplete.", null)
                            : result.Error);
                    }
                }
                catch (Exception exception)
                {
                    AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackException, ApplicationStartupPhase.Cleanup, default, default, null, "FinalizeRollback threw.", exception));
                }

                try
                {
                    _transaction.Dispose();
                }
                catch (Exception exception)
                {
                    AddRollbackError(new ApplicationStartupError(ApplicationStartupErrorCode.RollbackException, ApplicationStartupPhase.Cleanup, default, default, null, "Transaction disposal threw after rollback.", exception));
                }
                _transaction = null;
            }

            CurrentStepId = default;
            _operation = null;
            State = _rollbackErrorCount > 0 ? ApplicationStartupState.RollbackFailed : _requestedTerminalState;
        }

        private void AddRollbackError(ApplicationStartupError error)
        {
            if (_rollbackErrorCount < _rollbackErrors.Length)
            {
                _rollbackErrors[_rollbackErrorCount++] = error;
            }
        }

        private void AddCleanupWarning(ApplicationStartupError warning)
        {
            if (_cleanupWarningCount < _cleanupWarnings.Length)
            {
                _cleanupWarnings[_cleanupWarningCount++] = warning;
            }
        }

        private void FailWithoutTransaction(ApplicationStartupErrorCode code, ApplicationStartupPhase phase, ApplicationStartupStepId stepId, string message, Exception exception)
        {
            _forwardError = new ApplicationStartupError(code, phase, stepId, default, null, message, exception);
            if (_transaction != null)
            {
                try
                {
                    _transaction.Dispose();
                }
                catch (Exception cleanupException)
                {
                    AddCleanupWarning(new ApplicationStartupError(ApplicationStartupErrorCode.CleanupIncomplete, ApplicationStartupPhase.Cleanup, stepId, default, null, "Invalid transaction disposal failed.", cleanupException));
                }
            }
            _transaction = null;
            State = ApplicationStartupState.Failed;
        }

        private static bool IsTerminalOperation(RuntimeOperationStatus status)
        {
            return status == RuntimeOperationStatus.Succeeded || status == RuntimeOperationStatus.Failed ||
                status == RuntimeOperationStatus.Cancelled || status == RuntimeOperationStatus.TimedOut;
        }

        private static bool IsTerminalState(ApplicationStartupState state)
        {
            return state == ApplicationStartupState.Succeeded || state == ApplicationStartupState.Failed ||
                state == ApplicationStartupState.Cancelled || state == ApplicationStartupState.TimedOut ||
                state == ApplicationStartupState.RollbackFailed || state == ApplicationStartupState.Disposed;
        }
    }
}
