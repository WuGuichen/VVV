using System;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    /// <summary>Prepares and compensates one isolated additive candidate scene.</summary>
    public sealed class ApplicationCandidateSceneStep : ApplicationCandidateSessionStep
    {
        private readonly ApplicationCandidateSceneRequest _request;

        public ApplicationCandidateSceneStep(
            ApplicationStartupStepId stepId,
            ApplicationCandidateSceneRequest request)
            : base(stepId) => _request = request;

        /// <remarks>LowFreqAlloc. The returned operation performs no scene I/O until Tick.</remarks>
        public override IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-scene-forward",
                out IApplicationStartupStepOperation failure);
            if (failure != null) return failure;
            if (!(context.Transaction is IApplicationCandidateSceneTransaction owner) ||
                owner.CandidateScenePort == null || graph.SceneLease == null)
                return CandidateStepOperation.Failed(
                    "candidate-scene-forward",
                    ApplicationCandidateSessionErrorCode.InvalidGraph,
                    ApplicationCandidateSessionPhase.ScenePrepare);
            return new CandidateSceneForwardOperation(graph, owner, owner.CandidateScenePort, _request);
        }

        /// <remarks>LowFreqAlloc. Scene unload starts only when the returned operation is ticked.</remarks>
        public override IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
        {
            ApplicationCandidateSessionGraph graph = ResolveGraph(
                context.Transaction,
                "candidate-scene-rollback",
                out IApplicationStartupStepOperation failure);
            if (failure != null) return failure;
            if (graph.SceneLease == null)
                return CandidateStepOperation.Failed(
                    "candidate-scene-rollback",
                    ApplicationCandidateSessionErrorCode.InvalidGraph,
                    ApplicationCandidateSessionPhase.SceneCleanup);
            return new CandidateSceneRollbackOperation(graph);
        }
    }

    internal sealed class CandidateSceneForwardOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly IApplicationCandidateSceneTransaction _owner;
        private readonly ApplicationCandidateSceneTransactionPort _port;
        private readonly ApplicationCandidateSceneRequest _request;
        private bool _begun;

        internal CandidateSceneForwardOperation(
            ApplicationCandidateSessionGraph graph,
            IApplicationCandidateSceneTransaction owner,
            ApplicationCandidateSceneTransactionPort port,
            ApplicationCandidateSceneRequest request)
            : base("candidate-scene-forward")
        {
            _graph = graph;
            _owner = owner;
            _port = port;
            _request = request;
        }

        public override bool CanCancel => true;

        /// <remarks>NoAlloc on steady polling; terminal/error transitions may allocate diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (!_graph.CanStartupPump)
            {
                Fail(ApplicationCandidateSessionErrorCode.StartupPumpFrozen,
                    ApplicationCandidateSessionPhase.ScenePrepare);
                return;
            }
            if (!_graph.IsResourceReady)
            {
                Fail(ApplicationCandidateSessionErrorCode.InvalidGraph,
                    ApplicationCandidateSessionPhase.ScenePrepare);
                return;
            }

            if (!_begun)
            {
                _begun = true;
                ApplicationCandidateSessionErrorCode begin = _graph.SceneLease.BeginPrepare(in _request);
                if (begin != ApplicationCandidateSessionErrorCode.None)
                {
                    Finish();
                    return;
                }
            }

            ApplicationCandidateSceneLeaseStatus before = _graph.SceneLease.Status;
            try
            {
                _graph.SceneLease.TickPrepare(_owner, _port);
                ReportProgress(_graph.SceneLease.Progress);
            }
            catch (Exception exception)
            {
                if (before == ApplicationCandidateSceneLeaseStatus.Unloading)
                    _graph.SceneLease.RecordCleanupFault(exception);
                else
                    _graph.SceneLease.RecordForwardFault(exception);
                return;
            }
            Finish();
        }

        public override void RequestCancel()
        {
            if (!IsTerminal) _graph.SceneLease.RequestCancel();
        }

        private void Finish()
        {
            ApplicationCandidateSceneLeaseStatus status = _graph.SceneLease.Status;
            if (status == ApplicationCandidateSceneLeaseStatus.Loaded)
            {
                _graph.MarkSceneReady();
                Succeed();
                return;
            }
            if (status == ApplicationCandidateSceneLeaseStatus.Released)
            {
                if (_graph.SceneLease.ForwardCode == ApplicationCandidateSessionErrorCode.None &&
                    _graph.SceneLease.IsCancelRequested)
                    Cancel();
                else
                    FailScene(_graph.SceneLease.ForwardCode != ApplicationCandidateSessionErrorCode.None
                        ? _graph.SceneLease.ForwardCode
                        : ApplicationCandidateSessionErrorCode.SceneLoadFailed,
                        ApplicationCandidateCleanupDebt.None);
                return;
            }
            if (status == ApplicationCandidateSceneLeaseStatus.CleanupFailed)
                FailScene(ApplicationCandidateSessionErrorCode.SessionCleanupFailed,
                    ApplicationCandidateCleanupDebt.Scene);
        }

        private void FailScene(
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateCleanupDebt debt)
        {
            var detail = new ApplicationCandidateSceneErrorDetail(
                code,
                debt == ApplicationCandidateCleanupDebt.None
                    ? ApplicationCandidateSessionPhase.ScenePrepare
                    : ApplicationCandidateSessionPhase.SceneCleanup,
                _graph.SceneLease.ForwardCode,
                debt == ApplicationCandidateCleanupDebt.None
                    ? ApplicationCandidateSessionErrorCode.None
                    : ApplicationCandidateSessionErrorCode.SceneUnloadFailed,
                _graph.SceneLease.ForwardError,
                _graph.SceneLease.ForwardIsolationError,
                _graph.SceneLease.ForwardException,
                _graph.SceneLease.CleanupError,
                _graph.SceneLease.CleanupIsolationError,
                _graph.SceneLease.CleanupException,
                debt);
            FailWithDetail(code, detail);
        }
    }

    internal sealed class CandidateSceneRollbackOperation : CandidateStepOperation
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly bool _retirementOwner;
        private bool _begun;

        internal CandidateSceneRollbackOperation(
            ApplicationCandidateSessionGraph graph,
            bool retirementOwner = false)
            : base("candidate-scene-rollback")
        {
            _graph = graph;
            _retirementOwner = retirementOwner;
        }

        /// <remarks>NoAlloc on steady polling; terminal/error transitions may allocate diagnostics.</remarks>
        public override void Tick(in ApplicationStartupTickContext context)
        {
            if (IsTerminal) return;
            if (!_begun)
            {
                _begun = true;
                ApplicationCandidateSessionErrorCode ownership = _retirementOwner
                    ? _graph.ConfirmLiveRetirementOwnership()
                    : _graph.BeginRollbackOwnership();
                if (ownership != ApplicationCandidateSessionErrorCode.None)
                {
                    Fail(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict,
                        ApplicationCandidateSessionPhase.SceneCleanup);
                    return;
                }
                if (!_graph.SceneLease.BeginRollback())
                {
                    Fail(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict,
                        ApplicationCandidateSessionPhase.SceneCleanup);
                    return;
                }
            }

            try
            {
                _graph.SceneLease.TickRollback();
                ReportProgress(_graph.SceneLease.Progress);
            }
            catch (Exception exception)
            {
                _graph.SceneLease.RecordCleanupFault(exception);
                return;
            }
            ApplicationCandidateSceneLeaseStatus status = _graph.SceneLease.Status;
            if (status == ApplicationCandidateSceneLeaseStatus.Released)
            {
                Succeed();
                return;
            }
            if (status != ApplicationCandidateSceneLeaseStatus.CleanupFailed) return;

            var detail = new ApplicationCandidateSceneErrorDetail(
                ApplicationCandidateSessionErrorCode.SceneUnloadFailed,
                ApplicationCandidateSessionPhase.SceneCleanup,
                _graph.SceneLease.ForwardCode,
                ApplicationCandidateSessionErrorCode.SceneUnloadFailed,
                _graph.SceneLease.ForwardError,
                _graph.SceneLease.ForwardIsolationError,
                _graph.SceneLease.ForwardException,
                _graph.SceneLease.CleanupError,
                _graph.SceneLease.CleanupIsolationError,
                _graph.SceneLease.CleanupException,
                ApplicationCandidateCleanupDebt.Scene);
            FailWithDetail(ApplicationCandidateSessionErrorCode.SceneUnloadFailed, detail);
        }
    }
}
