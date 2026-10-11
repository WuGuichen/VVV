using System;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    public abstract class ApplicationCandidateSessionStep : IApplicationStartupStep
    {
        protected ApplicationCandidateSessionStep(ApplicationStartupStepId stepId)
        {
            if (!stepId.IsValid)
                throw new ArgumentException("Step id must be valid.", nameof(stepId));
            StepId = stepId;
        }

        public ApplicationStartupStepId StepId { get; }
        public abstract IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context);
        public abstract IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context);

        internal static ApplicationCandidateSessionGraph ResolveGraph(
            IApplicationStartupTransaction transaction,
            string operationId,
            out IApplicationStartupStepOperation failure)
        {
            if (!(transaction is IApplicationCandidateSessionTransaction candidateTransaction) ||
                candidateTransaction.CandidateSessionClaim == null ||
                candidateTransaction.CandidateSessionClaim.CandidateGraph == null)
            {
                failure = CandidateStepOperation.Failed(
                    operationId,
                    ApplicationCandidateSessionErrorCode.InvalidGraph,
                    ApplicationCandidateSessionPhase.GraphClaim);
                return null;
            }

            ApplicationCandidateSessionGraph graph = candidateTransaction.CandidateSessionClaim.CandidateGraph;
            ApplicationCandidateSessionErrorCode claim = graph.ValidateTransaction(transaction);
            if (claim != ApplicationCandidateSessionErrorCode.None)
            {
                failure = CandidateStepOperation.Failed(
                    operationId,
                    claim,
                    ApplicationCandidateSessionPhase.GraphClaim);
                return null;
            }

            failure = null;
            return graph;
        }
    }

    public sealed class ApplicationCandidateResourceStep : ApplicationCandidateSessionStep
    {
        public ApplicationCandidateResourceStep(ApplicationStartupStepId stepId) : base(stepId) { }

        public override IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-resource-forward",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateResourceForwardOperation(graph);
        }

        public override IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-resource-rollback",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateResourceRollbackOperation(graph);
        }
    }

    public sealed class ApplicationCandidateRuntimeLifecycleStep : ApplicationCandidateSessionStep
    {
        public ApplicationCandidateRuntimeLifecycleStep(ApplicationStartupStepId stepId) : base(stepId) { }

        public override IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-runtime-forward",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateRuntimeForwardOperation(graph);
        }

        public override IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-runtime-rollback",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateRuntimeRollbackOperation(graph);
        }
    }

    public sealed class ApplicationCandidateAppFlowStep : ApplicationCandidateSessionStep
    {
        public ApplicationCandidateAppFlowStep(ApplicationStartupStepId stepId) : base(stepId) { }

        public override IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-app-flow-forward",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateAppFlowForwardOperation(graph);
        }

        public override IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-app-flow-rollback",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateAppFlowRollbackOperation(graph);
        }
    }

    public sealed class ApplicationCandidateHandoffStep : ApplicationCandidateSessionStep
    {
        public ApplicationCandidateHandoffStep(ApplicationStartupStepId stepId) : base(stepId) { }

        public override IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-handoff-forward",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateHandoffForwardOperation(graph);
        }

        public override IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-handoff-rollback",
                out IApplicationStartupStepOperation failure);
            return failure ?? new CandidateHandoffRollbackOperation(graph);
        }
    }

    internal abstract class CandidateStepOperation : IApplicationStartupStepOperation
    {
        private readonly RuntimeOperation _operation;

        protected CandidateStepOperation(string operationId)
        {
            _operation = new RuntimeOperation(operationId);
            _operation.Start();
        }

        public string OperationId => _operation.OperationId;
        public RuntimeOperationStatus Status => _operation.Status;
        public float Progress => _operation.Progress;
        public RuntimeOperationError Error => _operation.Error;
        public virtual bool CanCancel => false;
        public IApplicationStartupInnerErrorDetail InnerErrorDetail { get; protected set; }

        /// <remarks>NoAlloc on steady polling; implementation-owned terminal/error transitions may allocate diagnostics.</remarks>
        public abstract void Tick(in ApplicationStartupTickContext context);
        public virtual void RequestCancel() { }

        protected bool IsTerminal => Status == RuntimeOperationStatus.Succeeded ||
                                     Status == RuntimeOperationStatus.Failed ||
                                     Status == RuntimeOperationStatus.Cancelled ||
                                     Status == RuntimeOperationStatus.TimedOut;
        protected void Succeed() => _operation.Succeed();
        protected void Cancel() => _operation.Cancel();
        protected void ReportProgress(float value) => _operation.ReportProgress(Clamp01(value));

        protected void FailWithDetail(
            ApplicationCandidateSessionErrorCode code,
            IApplicationStartupInnerErrorDetail detail)
        {
            InnerErrorDetail = detail;
            _operation.Fail(new RuntimeOperationError(GetStableErrorCode(code), GetStableErrorMessage(code)));
        }

        protected void Fail(
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateSessionPhase phase,
            ApplicationCandidateSessionErrorCode cleanupCode = ApplicationCandidateSessionErrorCode.None,
            ApplicationCandidateCleanupDebt cleanupDebts = ApplicationCandidateCleanupDebt.None)
        {
            Fail(code, phase, cleanupCode, cleanupDebts, default, null, default, null);
        }

        protected void Fail(
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateSessionPhase phase,
            ApplicationCandidateSessionErrorCode cleanupCode,
            ApplicationCandidateCleanupDebt cleanupDebts,
            RuntimeOperationError operationError,
            IApplicationStartupInnerErrorDetail innerErrorDetail,
            MxFramework.Resources.ResourcePlanPreparationError resourceError,
            Exception exception,
            RuntimeOperationError cleanupOperationError = default,
            IApplicationStartupInnerErrorDetail cleanupInnerErrorDetail = null,
            Exception cleanupException = null,
            ApplicationCandidateSessionErrorCode forwardCode = ApplicationCandidateSessionErrorCode.None)
        {
            InnerErrorDetail = new ApplicationCandidateSessionErrorDetail(
                code, phase, cleanupCode, cleanupDebts, operationError, innerErrorDetail, resourceError, exception,
                cleanupOperationError, cleanupInnerErrorDetail, cleanupException, forwardCode);
            _operation.Fail(new RuntimeOperationError(GetStableErrorCode(code), GetStableErrorMessage(code)));
        }

        protected void FailCleanup(
            ApplicationCandidateSessionCleanupResult cleanup,
            Exception cleanupException = null,
            MxFramework.Resources.ResourcePlanPreparationError resourceError = default,
            Exception forwardException = null,
            ApplicationCandidateSessionErrorCode forwardCode = ApplicationCandidateSessionErrorCode.None)
        {
            ApplicationCandidateSessionErrorCode cleanupCode = ApplicationCandidateSessionErrorCode.None;
            if ((cleanup.Debts & ApplicationCandidateCleanupDebt.AppFlow) != 0)
                cleanupCode = ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed;
            else if ((cleanup.Debts & ApplicationCandidateCleanupDebt.RuntimeHost) != 0)
                cleanupCode = ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed;
            else if ((cleanup.Debts & ApplicationCandidateCleanupDebt.ResourcePlan) != 0)
                cleanupCode = ApplicationCandidateSessionErrorCode.ResourceCleanupFailed;
            else if ((cleanup.Debts & ApplicationCandidateCleanupDebt.ResourceGraph) != 0)
                cleanupCode = ApplicationCandidateSessionErrorCode.ResourceGraphCleanupFailed;
            Fail(ApplicationCandidateSessionErrorCode.SessionCleanupFailed,
                ApplicationCandidateSessionPhase.SessionCleanup, cleanupCode, cleanup.Debts,
                default, null, resourceError, forwardException, default, null, cleanupException, forwardCode);
        }

        internal static IApplicationStartupStepOperation Failed(
            string operationId,
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateSessionPhase phase)
        {
            var operation = new ImmediateFailedOperation(operationId, code, phase);
            return operation;
        }

        internal static string GetStableErrorCode(ApplicationCandidateSessionErrorCode code)
        {
            return "MXAPPSESSION_" + ((int)code).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string GetStableErrorMessage(ApplicationCandidateSessionErrorCode code)
        {
            switch (code)
            {
                case ApplicationCandidateSessionErrorCode.InvalidGraph: return "Candidate graph is missing or invalid.";
                case ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed: return "Candidate graph belongs to another transaction.";
                case ApplicationCandidateSessionErrorCode.InvalidPumpStrategy: return "Candidate pump strategy is invalid.";
                case ApplicationCandidateSessionErrorCode.PumpOwnershipConflict: return "Candidate pump ownership conflicts with this operation.";
                case ApplicationCandidateSessionErrorCode.StartupPumpFrozen: return "Startup pumping is frozen.";
                case ApplicationCandidateSessionErrorCode.RuntimeInitializeFailed: return "Candidate RuntimeHost initialization failed.";
                case ApplicationCandidateSessionErrorCode.RuntimeStartFailed: return "Candidate RuntimeHost start failed.";
                case ApplicationCandidateSessionErrorCode.RuntimeNotReady: return "Candidate RuntimeHost is not ready.";
                case ApplicationCandidateSessionErrorCode.RuntimeTickObserved: return "Candidate RuntimeHost was ticked before publication.";
                case ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed: return "Candidate RuntimeHost cleanup failed.";
                case ApplicationCandidateSessionErrorCode.AppFlowCompensationMissing: return "Candidate AppFlow compensation is not available.";
                case ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed: return "Candidate AppFlow preparation failed.";
                case ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed: return "Candidate AppFlow compensation failed.";
                case ApplicationCandidateSessionErrorCode.AppFlowFreezeFailed: return "Candidate AppFlow handoff freeze failed.";
                case ApplicationCandidateSessionErrorCode.SceneRequestUnsupported: return "Candidate scene request is unsupported.";
                case ApplicationCandidateSessionErrorCode.SceneIsolationFailed: return "Candidate scene isolation failed.";
                case ApplicationCandidateSessionErrorCode.SceneLoadFailed: return "Candidate scene load failed.";
                case ApplicationCandidateSessionErrorCode.SceneUnloadFailed: return "Candidate scene unload failed.";
                case ApplicationCandidateSessionErrorCode.SceneAttachmentFailed: return "Candidate scene attachment failed.";
                case ApplicationCandidateSessionErrorCode.ResourcePreparationFailed: return "Candidate resource preparation failed.";
                case ApplicationCandidateSessionErrorCode.ResourceAcquireFailed: return "Candidate resource acquisition failed.";
                case ApplicationCandidateSessionErrorCode.ResourceCleanupFailed: return "Candidate resource cleanup failed.";
                case ApplicationCandidateSessionErrorCode.ResourceGraphCleanupFailed: return "Candidate resource graph cleanup failed.";
                case ApplicationCandidateSessionErrorCode.HandoffPreflightFailed: return "Candidate handoff preflight failed.";
                case ApplicationCandidateSessionErrorCode.SessionCleanupFailed: return "Candidate session cleanup failed.";
                default: return "Candidate session operation failed.";
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private sealed class ImmediateFailedOperation : CandidateStepOperation
        {
            public ImmediateFailedOperation(
                string operationId,
                ApplicationCandidateSessionErrorCode code,
                ApplicationCandidateSessionPhase phase)
                : base(operationId)
            {
                Fail(code, phase);
            }

            /// <remarks>NoAlloc.</remarks>
            public override void Tick(in ApplicationStartupTickContext context) { }
        }
    }

    internal sealed class CandidateResourceForwardOperation : CandidateStepOperation, IApplicationStartupFaultDrainOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private bool _cancelRequested;
        private ApplicationCandidateSessionErrorCode _failure;
        private Exception _forwardException;

        public CandidateResourceForwardOperation(ApplicationCandidateSessionGraph graph)
            : base("candidate-resource-forward") => _graph = graph;

        public override bool CanCancel => true;

        /// <remarks>NoAlloc on steady polling; terminal/error transitions may allocate structured diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (!_graph.CanStartupPump && !_cancelRequested)
            {
                Fail(ApplicationCandidateSessionErrorCode.StartupPumpFrozen, ApplicationCandidateSessionPhase.ResourcePrepare);
                return;
            }

            if (_cancelRequested || _failure != ApplicationCandidateSessionErrorCode.None)
            {
                DrainAndFinish();
                return;
            }

            ApplicationCandidateResourceLeaseStatus status;
            try
            {
                status = _graph.ResourceLease.PollPrepare();
                ReportProgress(_graph.ResourceLease.Progress);
            }
            catch (Exception exception)
            {
                _failure = ApplicationCandidateSessionErrorCode.ResourcePreparationFailed;
                _forwardException = exception;
                try { _graph.ResourceLease.BeginRelease(); }
                catch (Exception cleanupException)
                {
                    _graph.ResourceLease.RecordCleanupException(cleanupException);
                }
                DrainAndFinish();
                return;
            }
            if (status == ApplicationCandidateResourceLeaseStatus.Acquired)
            {
                _graph.MarkResourceReady();
                Succeed();
            }
            else if (status == ApplicationCandidateResourceLeaseStatus.Releasing ||
                     status == ApplicationCandidateResourceLeaseStatus.Released ||
                     status == ApplicationCandidateResourceLeaseStatus.CleanupFailed)
            {
                _failure = _graph.ResourceLease.ForwardErrorCode != ApplicationCandidateSessionErrorCode.None
                    ? _graph.ResourceLease.ForwardErrorCode
                    : ApplicationCandidateSessionErrorCode.ResourcePreparationFailed;
                DrainAndFinish();
            }
        }

        public override void RequestCancel()
        {
            if (IsTerminal || _cancelRequested) return;
            _cancelRequested = true;
            try { _graph.ResourceLease.CancelPrepare(); }
            catch (Exception exception)
            {
                _graph.ResourceLease.RecordCleanupException(exception);
            }
        }

        public void RequestFaultDrain(Exception exception)
        {
            _failure = ApplicationCandidateSessionErrorCode.ResourcePreparationFailed;
            _forwardException = exception;
            try { _graph.ResourceLease.BeginRelease(); }
            catch (Exception cleanupException)
            {
                _graph.ResourceLease.RecordCleanupException(cleanupException);
            }
        }

        public void TickFaultDrain(in ApplicationStartupTickContext context) => DrainAndFinish();

        private void DrainAndFinish()
        {
            _graph.BeginRollbackOwnership();
            ApplicationCandidateResourceLeaseStatus status = _graph.ResourceLease.Status;
            try
            {
                _graph.ResourceLease.BeginRelease();
                status = _graph.ResourceLease.PollRelease();
            }
            catch (Exception exception)
            {
                _graph.ResourceLease.RecordCleanupException(exception);
                status = ApplicationCandidateResourceLeaseStatus.CleanupFailed;
            }
            if (status == ApplicationCandidateResourceLeaseStatus.Releasing) return;

            _graph.DisposeRuntimeHost();

            ApplicationCandidateSessionCleanupResult cleanup = _graph.FinalizeRollback();
            if (!cleanup.Success)
            {
                FailCleanup(cleanup, _graph.CleanupException,
                    _graph.ResourceLease.PreparationError, _forwardException, _failure);
                return;
            }

            if (_cancelRequested && _failure == ApplicationCandidateSessionErrorCode.None) Cancel();
            else Fail(_failure, ApplicationCandidateSessionPhase.ResourcePrepare,
                ApplicationCandidateSessionErrorCode.None, ApplicationCandidateCleanupDebt.None,
                default, null, _graph.ResourceLease.PreparationError, _forwardException,
                default, null, _graph.CleanupException);
        }
    }

    internal sealed class CandidateResourceRollbackOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly bool _retirementOwner;

        public CandidateResourceRollbackOperation(
            ApplicationCandidateSessionGraph graph,
            bool retirementOwner = false)
            : base("candidate-resource-rollback")
        {
            _graph = graph;
            _retirementOwner = retirementOwner;
        }

        /// <remarks>NoAlloc on steady polling; terminal/error transitions may allocate structured diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            ApplicationCandidateSessionErrorCode ownership = _retirementOwner
                ? _graph.ConfirmLiveRetirementOwnership()
                : _graph.BeginRollbackOwnership();
            if (ownership != ApplicationCandidateSessionErrorCode.None)
            {
                Fail(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict, ApplicationCandidateSessionPhase.Rollback);
                return;
            }

            _graph.SceneLease?.FinalizeUnstarted();
            if (_graph.SceneLease != null && !_graph.SceneLease.CanReleaseResources)
            {
                ApplicationCandidateSessionCleanupResult blocked = ApplicationCandidateSessionCleanupResult.Failed(
                    ApplicationCandidateCleanupDebt.Scene | ApplicationCandidateCleanupDebt.ResourcePlan,
                    "CANDIDATE_SCENE_ACTIVE");
                FailCleanup(blocked, _graph.CleanupException);
                return;
            }
            _graph.DisposeRuntimeHost();
            ApplicationCandidateResourceLeaseStatus status;
            try
            {
                _graph.ResourceLease.BeginRelease();
                status = _graph.ResourceLease.PollRelease();
            }
            catch (Exception exception)
            {
                _graph.ResourceLease.RecordCleanupException(exception);
                status = ApplicationCandidateResourceLeaseStatus.CleanupFailed;
            }
            if (status == ApplicationCandidateResourceLeaseStatus.Releasing)
            {
                ReportProgress(_graph.ResourceLease.Progress);
                return;
            }
            ApplicationCandidateSessionCleanupResult cleanup = _graph.FinalizeRollback();
            if (!cleanup.Success)
            {
                FailCleanup(cleanup, _graph.CleanupException);
                return;
            }
            Succeed();
        }
    }

    internal sealed class CandidateRuntimeForwardOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private bool _cancelRequested;

        public CandidateRuntimeForwardOperation(ApplicationCandidateSessionGraph graph)
            : base("candidate-runtime-forward") => _graph = graph;

        public override bool CanCancel => true;

        /// <remarks>NoAlloc on steady polling; lifecycle/error transitions may allocate implementation diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (_cancelRequested)
            {
                if (_graph.DisposeRuntimeHost()) Cancel();
                else Fail(ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed,
                    ApplicationCandidateSessionPhase.RuntimeLifecycle,
                    ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed,
                    ApplicationCandidateCleanupDebt.RuntimeHost);
                return;
            }
            if (!_graph.CanStartupPump || !_graph.IsResourceReady || !_graph.IsSceneReady)
            {
                Fail(ApplicationCandidateSessionErrorCode.RuntimeNotReady, ApplicationCandidateSessionPhase.RuntimeLifecycle);
                return;
            }

            ApplicationCandidateSessionErrorCode failure = ApplicationCandidateSessionErrorCode.None;
            ApplicationCandidateSessionErrorCode activeStage = ApplicationCandidateSessionErrorCode.RuntimeInitializeFailed;
            try
            {
                _graph.RuntimeHost.Initialize();
                if (_graph.RuntimeHost.State != RuntimeLifecycleState.Initialized || _graph.RuntimeHost.Errors.Count != 0)
                    failure = ApplicationCandidateSessionErrorCode.RuntimeInitializeFailed;
                else
                {
                    activeStage = ApplicationCandidateSessionErrorCode.RuntimeStartFailed;
                    _graph.RuntimeHost.Start();
                }

                if (failure == ApplicationCandidateSessionErrorCode.None &&
                    (_graph.RuntimeHost.State != RuntimeLifecycleState.Started || _graph.RuntimeHost.Errors.Count != 0))
                {
                    failure = ApplicationCandidateSessionErrorCode.RuntimeStartFailed;
                }
                if (failure == ApplicationCandidateSessionErrorCode.None &&
                    (_graph.RuntimeHost.HasTickAttempt || _graph.RuntimeHost.TickCount != 0))
                {
                    failure = ApplicationCandidateSessionErrorCode.RuntimeTickObserved;
                }
            }
            catch
            {
                failure = activeStage;
            }

            if (failure != ApplicationCandidateSessionErrorCode.None)
            {
                CleanupAndFail(failure);
                return;
            }

            _graph.MarkRuntimeReady();
            Succeed();
        }

        public override void RequestCancel() => _cancelRequested = true;

        private void CleanupAndFail(ApplicationCandidateSessionErrorCode failure)
        {
            if (!_graph.DisposeRuntimeHost())
            {
                Fail(failure, ApplicationCandidateSessionPhase.RuntimeLifecycle, ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed);
                return;
            }
            Fail(failure, ApplicationCandidateSessionPhase.RuntimeLifecycle);
        }
    }

    internal sealed class CandidateRuntimeRollbackOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly bool _retirementOwner;
        public CandidateRuntimeRollbackOperation(
            ApplicationCandidateSessionGraph graph,
            bool retirementOwner = false)
            : base("candidate-runtime-rollback")
        {
            _graph = graph;
            _retirementOwner = retirementOwner;
        }

        /// <remarks>NoAlloc on steady polling; lifecycle/error transitions may allocate implementation diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            ApplicationCandidateSessionErrorCode ownership = _retirementOwner
                ? _graph.ConfirmLiveRetirementOwnership()
                : _graph.BeginRollbackOwnership();
            if (ownership != ApplicationCandidateSessionErrorCode.None)
            {
                Fail(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict,
                    ApplicationCandidateSessionPhase.Rollback);
                return;
            }
            if (_graph.DisposeRuntimeHost()) Succeed();
            else Fail(ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed,
                ApplicationCandidateSessionPhase.Rollback,
                ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed,
                ApplicationCandidateCleanupDebt.RuntimeHost);
        }
    }

    internal sealed class CandidateAppFlowForwardOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private IApplicationStartupStepOperation _forward;
        private IApplicationStartupStepOperation _rollback;
        private RuntimeOperationStatus _desiredTerminal;
        private ApplicationCandidateSessionErrorCode _forwardFailure;
        private RuntimeOperationError _nestedError;
        private IApplicationStartupInnerErrorDetail _nestedDetail;
        private Exception _nestedException;
        private RuntimeOperationError _cleanupNestedError;
        private IApplicationStartupInnerErrorDetail _cleanupNestedDetail;
        private Exception _cleanupNestedException;
        private bool _started;
        private bool _drainingForwardFault;
        private bool _drainingRollbackFault;
        private bool _forwardDrainRequested;
        private bool _rollbackDrainRequested;

        public CandidateAppFlowForwardOperation(ApplicationCandidateSessionGraph graph)
            : base("candidate-app-flow-forward") => _graph = graph;

        public override bool CanCancel => true;

        /// <remarks>NoAlloc on bridge steady polling outside implementation-owned AppFlow operations; terminal/error transitions may allocate diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (_drainingForwardFault) { PumpForwardFaultDrain(context); return; }
            if (_drainingRollbackFault) { PumpRollbackFaultDrain(context); return; }
            if (_rollback != null) { PumpRollback(context); return; }
            if (!_started)
            {
                _started = true;
                if (!_graph.CanStartupPump || !_graph.IsRuntimeReady)
                {
                    Fail(ApplicationCandidateSessionErrorCode.RuntimeNotReady, ApplicationCandidateSessionPhase.AppFlowPrepare);
                    return;
                }
                if (!_graph.AppFlowLease.CanCompensate)
                {
                    Fail(ApplicationCandidateSessionErrorCode.AppFlowCompensationMissing, ApplicationCandidateSessionPhase.AppFlowPrepare);
                    return;
                }

                try { _forward = _graph.AppFlowLease.BeginPrepare(); }
                catch (Exception exception)
                {
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                    _nestedException = exception;
                    BeginCompensation();
                    return;
                }
                if (_forward == null)
                {
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                    BeginCompensation();
                    return;
                }
            }

            RuntimeOperationStatus forwardStatus;
            try
            {
                forwardStatus = _forward.Status;
                if (IsOperationTerminal(forwardStatus))
                {
                    HandleForwardTerminal(forwardStatus);
                    return;
                }
                _forward.Tick(in context);
                ReportProgress(_forward.Progress);
                forwardStatus = _forward.Status;
            }
            catch (Exception exception)
            {
                _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                CaptureNested(_forward, exception);
                BeginForwardFaultDrain(exception);
                return;
            }
            HandleForwardTerminal(forwardStatus);
        }

        private void HandleForwardTerminal(RuntimeOperationStatus forwardStatus)
        {
            if (forwardStatus == RuntimeOperationStatus.Succeeded && _desiredTerminal == RuntimeOperationStatus.Pending)
            {
                if (_graph.IsAppFlowUntickedAndReady())
                {
                    _graph.AppFlowLease.MarkPrepared();
                    _graph.MarkAppFlowReady();
                    Succeed();
                }
                else
                {
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                    BeginCompensation();
                }
            }
            else if (IsOperationTerminal(forwardStatus))
            {
                CaptureNested(_forward, null);
                if (_desiredTerminal == RuntimeOperationStatus.Pending)
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                BeginCompensation();
            }
        }

        public override void RequestCancel()
        {
            if (IsTerminal || _desiredTerminal == RuntimeOperationStatus.Cancelled) return;
            _desiredTerminal = RuntimeOperationStatus.Cancelled;
            bool canCancel = false;
            bool terminal = false;
            if (_forward != null)
            {
                try
                {
                    canCancel = _forward.CanCancel;
                    terminal = IsOperationTerminal(_forward.Status);
                }
                catch (Exception exception)
                {
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                    CaptureNested(_forward, exception);
                    BeginForwardFaultDrain(exception);
                    return;
                }
            }
            if (_forward != null && canCancel && !terminal)
            {
                try { _forward.RequestCancel(); }
                catch (Exception exception)
                {
                    _forwardFailure = ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed;
                    CaptureNested(_forward, exception);
                    BeginForwardFaultDrain(exception);
                }
            }
            else if (_forward == null)
            {
                BeginCompensation();
            }
        }

        private void BeginCompensation()
        {
            if (_rollback != null) return;
            _graph.BeginRollbackOwnership();
            try { _rollback = _graph.AppFlowLease.BeginRollback(); }
            catch (Exception exception)
            {
                _graph.AppFlowLease.MarkDebt();
                _cleanupNestedException = Combine(_cleanupNestedException, exception);
                Fail(
                    _forwardFailure == ApplicationCandidateSessionErrorCode.None ? ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed : _forwardFailure,
                    ApplicationCandidateSessionPhase.AppFlowPrepare,
                    ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                    ApplicationCandidateCleanupDebt.AppFlow,
                    _nestedError, _nestedDetail, default, _nestedException,
                    _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
                return;
            }

            if (_rollback == null)
            {
                _graph.AppFlowLease.MarkDebt();
                Fail(
                    _forwardFailure == ApplicationCandidateSessionErrorCode.None ? ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed : _forwardFailure,
                    ApplicationCandidateSessionPhase.AppFlowPrepare,
                    ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                    ApplicationCandidateCleanupDebt.AppFlow,
                    _nestedError, _nestedDetail, default, _nestedException,
                    _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
            }
        }

        private void PumpRollback(in ApplicationStartupTickContext context)
        {
            RuntimeOperationStatus rollbackStatus;
            try
            {
                rollbackStatus = _rollback.Status;
                if (IsOperationTerminal(rollbackStatus))
                {
                    CompleteRollback(rollbackStatus);
                    return;
                }
                _rollback.Tick(in context);
                rollbackStatus = _rollback.Status;
            }
            catch (Exception exception)
            {
                CaptureNested(_rollback, exception, cleanup: true);
                BeginRollbackFaultDrain(exception);
                return;
            }
            if (!IsOperationTerminal(rollbackStatus)) return;
            CompleteRollback(rollbackStatus);
        }

        private void CompleteRollback(RuntimeOperationStatus rollbackStatus)
        {
            if (rollbackStatus != RuntimeOperationStatus.Succeeded)
            {
                CaptureNested(_rollback, null, cleanup: true);
                _graph.AppFlowLease.MarkDebt();
                Fail(
                    _forwardFailure == ApplicationCandidateSessionErrorCode.None ? ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed : _forwardFailure,
                    ApplicationCandidateSessionPhase.AppFlowPrepare,
                    ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                    ApplicationCandidateCleanupDebt.AppFlow,
                    _nestedError, _nestedDetail, default, _nestedException,
                    _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
            }
            else
            {
                _graph.AppFlowLease.MarkRolledBack();
                if (_desiredTerminal == RuntimeOperationStatus.Cancelled) Cancel();
                else Fail(
                    _forwardFailure == ApplicationCandidateSessionErrorCode.None ? ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed : _forwardFailure,
                    ApplicationCandidateSessionPhase.AppFlowPrepare,
                    ApplicationCandidateSessionErrorCode.None,
                    ApplicationCandidateCleanupDebt.None,
                    _nestedError, _nestedDetail, default, _nestedException,
                    _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
            }
        }

        private void BeginForwardFaultDrain(Exception exception)
        {
            if (_forward is IApplicationStartupFaultDrainOperation drain)
            {
                _forwardDrainRequested = true;
                try { drain.RequestFaultDrain(exception); }
                catch (Exception drainException) { _nestedException = Combine(_nestedException, drainException); }
            }
            if (TryReadTerminal(_forward)) { BeginCompensation(); return; }
            _drainingForwardFault = true;
        }

        private void PumpForwardFaultDrain(in ApplicationStartupTickContext context)
        {
            if (_forwardDrainRequested)
            {
                try { ((IApplicationStartupFaultDrainOperation)_forward).TickFaultDrain(in context); }
                catch (Exception exception) { CaptureNested(_forward, exception); }
            }
            if (!TryReadTerminal(_forward)) return;
            _drainingForwardFault = false;
            CaptureNested(_forward, null);
            BeginCompensation();
        }

        private void BeginRollbackFaultDrain(Exception exception)
        {
            if (_rollback is IApplicationStartupFaultDrainOperation drain)
            {
                _rollbackDrainRequested = true;
                try { drain.RequestFaultDrain(exception); }
                catch (Exception drainException) { _cleanupNestedException = Combine(_cleanupNestedException, drainException); }
            }
            _drainingRollbackFault = true;
            CompleteRollbackFault();
        }

        private void PumpRollbackFaultDrain(in ApplicationStartupTickContext context)
        {
            if (_rollbackDrainRequested)
            {
                try { ((IApplicationStartupFaultDrainOperation)_rollback).TickFaultDrain(in context); }
                catch (Exception exception) { CaptureNested(_rollback, exception, cleanup: true); }
            }
            if (!TryReadTerminal(_rollback)) return;
            _drainingRollbackFault = false;
            CompleteRollbackFault();
        }

        private void CompleteRollbackFault()
        {
            CaptureNested(_rollback, null, cleanup: true);
            if (!TryReadTerminal(_rollback))
                return;
            if (TryReadStatus(_rollback) == RuntimeOperationStatus.Succeeded)
            {
                _graph.AppFlowLease.MarkRolledBack();
                if (_desiredTerminal == RuntimeOperationStatus.Cancelled) Cancel();
                else Fail(ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed,
                    ApplicationCandidateSessionPhase.AppFlowPrepare,
                    ApplicationCandidateSessionErrorCode.None, ApplicationCandidateCleanupDebt.None,
                    _nestedError, _nestedDetail, default, _nestedException,
                    _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
                return;
            }
            _graph.AppFlowLease.MarkDebt();
            Fail(ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed,
                ApplicationCandidateSessionPhase.AppFlowPrepare,
                ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                ApplicationCandidateCleanupDebt.AppFlow,
                _nestedError, _nestedDetail, default, _nestedException,
                _cleanupNestedError, _cleanupNestedDetail, _cleanupNestedException);
        }

        private void CaptureNested(IApplicationStartupStepOperation operation, Exception exception, bool cleanup = false)
        {
            if (cleanup && exception != null)
                _cleanupNestedException = Combine(_cleanupNestedException, exception);
            else if (!cleanup && exception != null)
                _nestedException = Combine(_nestedException, exception);
            if (operation == null) return;
            if (cleanup)
            {
                try { _cleanupNestedError = operation.Error; } catch { }
                try { _cleanupNestedDetail = operation.InnerErrorDetail; } catch { }
            }
            else
            {
                try { _nestedError = operation.Error; } catch { }
                try { _nestedDetail = operation.InnerErrorDetail; } catch { }
            }
        }

        private static bool TryReadTerminal(IApplicationStartupStepOperation operation) =>
            IsOperationTerminal(TryReadStatus(operation));
        private static RuntimeOperationStatus TryReadStatus(IApplicationStartupStepOperation operation)
        {
            try { return operation != null ? operation.Status : RuntimeOperationStatus.Pending; }
            catch { return RuntimeOperationStatus.Pending; }
        }

        private static Exception Combine(Exception first, Exception second) =>
            first == null ? second : second == null ? first : new AggregateException(first, second);

        private static bool IsOperationTerminal(RuntimeOperationStatus status) =>
            status == RuntimeOperationStatus.Succeeded || status == RuntimeOperationStatus.Failed ||
            status == RuntimeOperationStatus.Cancelled || status == RuntimeOperationStatus.TimedOut;
    }

    internal sealed class CandidateAppFlowRollbackOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly bool _retirementOwner;
        private IApplicationStartupStepOperation _rollback;
        private bool _started;
        private bool _drainingFault;
        private bool _faultDrainRequested;
        private RuntimeOperationError _nestedError;
        private IApplicationStartupInnerErrorDetail _nestedDetail;
        private Exception _nestedException;
        public CandidateAppFlowRollbackOperation(
            ApplicationCandidateSessionGraph graph,
            bool retirementOwner = false)
            : base("candidate-app-flow-rollback")
        {
            _graph = graph;
            _retirementOwner = retirementOwner;
        }

        /// <remarks>NoAlloc on bridge steady polling outside implementation-owned AppFlow operations; terminal/error transitions may allocate diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (_drainingFault) { PumpFaultDrain(context); return; }
            if (!_started)
            {
                _started = true;
                ApplicationCandidateSessionErrorCode ownership = _retirementOwner
                    ? _graph.ConfirmLiveRetirementOwnership()
                    : _graph.BeginRollbackOwnership();
                if (ownership != ApplicationCandidateSessionErrorCode.None)
                {
                    Fail(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict, ApplicationCandidateSessionPhase.Rollback);
                    return;
                }
                try { _rollback = _graph.AppFlowLease.BeginRollback(); }
                catch (Exception exception)
                {
                    _graph.AppFlowLease.MarkDebt();
                    FailNested(exception);
                    return;
                }
                if (_rollback == null)
                {
                    if (_graph.AppFlowLease.Status == ApplicationCandidateAppFlowLeaseStatus.RolledBack)
                        Succeed();
                    else
                    {
                        _graph.AppFlowLease.MarkDebt();
                        FailNested(null);
                    }
                    return;
                }
            }

            RuntimeOperationStatus rollbackStatus;
            try
            {
                rollbackStatus = _rollback.Status;
                if (IsOperationTerminal(rollbackStatus)) { Complete(rollbackStatus); return; }
                _rollback.Tick(in context);
                rollbackStatus = _rollback.Status;
            }
            catch (Exception exception)
            {
                CaptureNested(exception);
                if (_rollback is IApplicationStartupFaultDrainOperation drain)
                {
                    _faultDrainRequested = true;
                    try { drain.RequestFaultDrain(exception); }
                    catch (Exception drainException) { _nestedException = Combine(_nestedException, drainException); }
                }
                _drainingFault = true;
                // Fail closed: a non-terminal inner handle keeps ownership and this wrapper running.
                if (ReadStatus() != RuntimeOperationStatus.Pending && IsOperationTerminal(ReadStatus()))
                    Complete(ReadStatus());
                return;
            }
            if (IsOperationTerminal(rollbackStatus)) Complete(rollbackStatus);
        }

        private void PumpFaultDrain(in ApplicationStartupTickContext context)
        {
            if (_faultDrainRequested)
            {
                try { ((IApplicationStartupFaultDrainOperation)_rollback).TickFaultDrain(in context); }
                catch (Exception exception) { CaptureNested(exception); }
            }
            RuntimeOperationStatus status = ReadStatus();
            if (!IsOperationTerminal(status)) return;
            _drainingFault = false;
            Complete(status);
        }

        private void Complete(RuntimeOperationStatus status)
        {
            CaptureNested(null);
            if (status == RuntimeOperationStatus.Succeeded)
            {
                if (_graph.AppFlowLease.Status == ApplicationCandidateAppFlowLeaseStatus.RollingBack)
                    _graph.AppFlowLease.MarkRolledBack();
                Succeed();
            }
            else
            {
                _graph.AppFlowLease.MarkDebt();
                FailNested(null);
            }
        }

        private void CaptureNested(Exception exception)
        {
            if (exception != null) _nestedException = Combine(_nestedException, exception);
            try { _nestedError = _rollback.Error; } catch { }
            try { _nestedDetail = _rollback.InnerErrorDetail; } catch { }
        }

        private RuntimeOperationStatus ReadStatus()
        {
            try { return _rollback.Status; }
            catch { return RuntimeOperationStatus.Pending; }
        }

        private void FailNested(Exception exception)
        {
            if (exception != null) _nestedException = Combine(_nestedException, exception);
            Fail(ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                ApplicationCandidateSessionPhase.Rollback,
                ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed,
                ApplicationCandidateCleanupDebt.AppFlow,
                _nestedError, _nestedDetail, default, _nestedException);
        }

        private static Exception Combine(Exception first, Exception second) =>
            first == null ? second : second == null ? first : new AggregateException(first, second);

        private static bool IsOperationTerminal(RuntimeOperationStatus status) =>
            status == RuntimeOperationStatus.Succeeded || status == RuntimeOperationStatus.Failed ||
            status == RuntimeOperationStatus.Cancelled || status == RuntimeOperationStatus.TimedOut;
    }

    internal sealed class CandidateHandoffForwardOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        public CandidateHandoffForwardOperation(ApplicationCandidateSessionGraph graph)
            : base("candidate-handoff-forward") => _graph = graph;

        /// <remarks>NoAlloc on success; error transition may allocate structured diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            ApplicationCandidateSessionErrorCode result = _graph.FreezeForHandoff();
            if (result == ApplicationCandidateSessionErrorCode.None) Succeed();
            else Fail(result, ApplicationCandidateSessionPhase.HandoffFreeze);
        }
    }

    internal sealed class CandidateHandoffRollbackOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        public CandidateHandoffRollbackOperation(ApplicationCandidateSessionGraph graph)
            : base("candidate-handoff-rollback") => _graph = graph;

        /// <remarks>NoAlloc on success; error transition may allocate structured diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            ApplicationCandidateSessionErrorCode result = _graph.BeginRollbackOwnership();
            if (result == ApplicationCandidateSessionErrorCode.None) Succeed();
            else Fail(result, ApplicationCandidateSessionPhase.Rollback);
        }
    }
}
