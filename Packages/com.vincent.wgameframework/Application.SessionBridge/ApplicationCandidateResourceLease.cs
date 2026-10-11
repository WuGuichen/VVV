using System;
using System.Threading;
using MxFramework.Resources;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    public enum ApplicationCandidateResourceLeaseStatus
    {
        Invalid = 0,
        Created = 1,
        Preparing = 2,
        Acquired = 3,
        Releasing = 4,
        Released = 5,
        CleanupFailed = 6
    }

    /// <summary>Owner-bound candidate ResourcePlan and resource-graph lease.</summary>
    /// <remarks>Construction is LowFreqAlloc; Status/Progress/ErrorCode polling is NoAlloc after preparation begins.</remarks>
    internal sealed class ApplicationCandidateResourceLease
    {
        private readonly ApplicationCandidateSessionToken _token;
        private readonly ApplicationCandidateResourceGraph _graph;
        private ResourcePlanPreparation _preparation;
        private ResourcePlanSession _session;
        private int _status = (int)ApplicationCandidateResourceLeaseStatus.Created;
        private ApplicationCandidateSessionErrorCode _forwardErrorCode;
        private ApplicationCandidateSessionErrorCode _cleanupErrorCode;
        private ResourcePlanPreparationError _preparationError;
        private Exception _cleanupException;
        private int _graphFinalized;
        private int _graphFinalizationAttempted;
        private ApplicationCandidateResourceGraphCleanupResult _graphFinalizationResult;

        internal ApplicationCandidateResourceLease(
            ApplicationCandidateSessionToken token,
            ApplicationCandidateResourceGraph graph)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        internal ApplicationCandidateSessionToken Token => _token;
        internal IResourceManager Manager => _graph.Manager;
        internal ResourceRuntimeConfigurationIdentity ConfigurationIdentity => _graph.ConfigurationIdentity;
        internal ResourcePlan Plan => _graph.Plan;
        internal ResourcePlanSession Session => Volatile.Read(ref _session);
        internal ApplicationCandidateResourceLeaseStatus Status =>
            (ApplicationCandidateResourceLeaseStatus)Volatile.Read(ref _status);
        internal bool IsAcquired => Status == ApplicationCandidateResourceLeaseStatus.Acquired;
        internal bool IsReleased => Status == ApplicationCandidateResourceLeaseStatus.Released;
        internal ApplicationCandidateSessionErrorCode ErrorCode =>
            _cleanupErrorCode != ApplicationCandidateSessionErrorCode.None
                ? _cleanupErrorCode
                : _forwardErrorCode;
        internal ApplicationCandidateSessionErrorCode ForwardErrorCode => _forwardErrorCode;
        internal ApplicationCandidateSessionErrorCode CleanupErrorCode => _cleanupErrorCode;
        internal ResourcePlanPreparationError PreparationError => _preparationError;
        internal Exception CleanupException => _cleanupException;
        internal void RecordCleanupException(Exception exception)
        {
            if (exception == null) return;
            _cleanupException = _cleanupException == null
                ? exception
                : new AggregateException(_cleanupException, exception);
        }

        internal float Progress
        {
            get
            {
                ResourcePlanPreparation preparation = _preparation;
                ApplicationCandidateResourceLeaseStatus status = Status;
                if (status == ApplicationCandidateResourceLeaseStatus.Created)
                    return 0f;
                if (status == ApplicationCandidateResourceLeaseStatus.Preparing && preparation != null)
                    return preparation.Progress;
                return 1f;
            }
        }

        internal void BeginPrepare()
        {
            if (Interlocked.CompareExchange(
                    ref _status,
                    (int)ApplicationCandidateResourceLeaseStatus.Preparing,
                    (int)ApplicationCandidateResourceLeaseStatus.Created) !=
                (int)ApplicationCandidateResourceLeaseStatus.Created)
            {
                return;
            }

            _preparation = _graph.Orchestrator.Prepare(_graph.ConfigurationIdentity, _graph.Plan);
        }

        internal ApplicationCandidateResourceLeaseStatus PollPrepare()
        {
            if (Status == ApplicationCandidateResourceLeaseStatus.Created)
                BeginPrepare();
            if (Status != ApplicationCandidateResourceLeaseStatus.Preparing)
                return Status;

            ResourcePlanPreparationStatus preparationStatus = _preparation.Status;
            if (preparationStatus == ResourcePlanPreparationStatus.Prepared)
            {
                ResourcePlanAcquireResult acquire = _graph.Orchestrator.AcquirePrepared(_graph.Plan, _preparation);
                if (acquire.Success)
                {
                    Volatile.Write(ref _session, acquire.Session);
                    Volatile.Write(ref _status, (int)ApplicationCandidateResourceLeaseStatus.Acquired);
                    return Status;
                }

                _forwardErrorCode = ApplicationCandidateSessionErrorCode.ResourceAcquireFailed;
                _preparationError = acquire.Error;
                BeginRelease();
                return Status;
            }

            if (preparationStatus == ResourcePlanPreparationStatus.Failed ||
                preparationStatus == ResourcePlanPreparationStatus.Invalid ||
                preparationStatus == ResourcePlanPreparationStatus.Released)
            {
                _forwardErrorCode = ApplicationCandidateSessionErrorCode.ResourcePreparationFailed;
                _preparationError = _preparation.Error;
                BeginRelease();
            }

            return Status;
        }

        internal void CancelPrepare()
        {
            if (Status == ApplicationCandidateResourceLeaseStatus.Preparing && _preparation != null)
                _preparation.Cancel();
            BeginRelease();
        }

        internal void BeginRelease()
        {
            while (true)
            {
                ApplicationCandidateResourceLeaseStatus status = Status;
                if (status == ApplicationCandidateResourceLeaseStatus.Releasing ||
                    status == ApplicationCandidateResourceLeaseStatus.Released ||
                    status == ApplicationCandidateResourceLeaseStatus.CleanupFailed)
                    return;
                if (Interlocked.CompareExchange(
                        ref _status,
                        (int)ApplicationCandidateResourceLeaseStatus.Releasing,
                        (int)status) == (int)status)
                {
                    break;
                }
            }

            ResourcePlanSession session = Volatile.Read(ref _session);
            if (session != null)
            {
                try
                {
                    _graph.Orchestrator.Release(session);
                }
                catch (Exception exception)
                {
                    RecordCleanupException(exception);
                    _cleanupErrorCode = ApplicationCandidateSessionErrorCode.ResourceCleanupFailed;
                    Volatile.Write(ref _status, (int)ApplicationCandidateResourceLeaseStatus.CleanupFailed);
                    return;
                }
            }
            else if (_preparation != null)
            {
                try { _preparation.Release(); }
                catch (Exception exception)
                {
                    RecordCleanupException(exception);
                    _cleanupErrorCode = ApplicationCandidateSessionErrorCode.ResourceCleanupFailed;
                    Volatile.Write(ref _status, (int)ApplicationCandidateResourceLeaseStatus.CleanupFailed);
                    return;
                }
            }

            PollRelease();
        }

        internal ApplicationCandidateResourceLeaseStatus PollRelease()
        {
            if (Status != ApplicationCandidateResourceLeaseStatus.Releasing)
                return Status;

            if (_session == null && _preparation != null &&
                _preparation.Status != ResourcePlanPreparationStatus.Released)
            {
                return Status;
            }

            Volatile.Write(ref _status, (int)ApplicationCandidateResourceLeaseStatus.Released);
            return Status;
        }

        internal ApplicationCandidateResourceGraphCleanupResult FinalizeGraphRollback()
        {
            if (!IsReleased)
                return ApplicationCandidateResourceGraphCleanupResult.Failed("RESOURCE_LEASE_ACTIVE");
            if (Volatile.Read(ref _graphFinalized) != 0)
                return ApplicationCandidateResourceGraphCleanupResult.Succeeded;
            if (Volatile.Read(ref _graphFinalizationAttempted) != 0)
                return _graphFinalizationResult;

            ApplicationCandidateResourceGraphCleanupResult result = _graph.Lifetime.FinalizeRollback();
            _graphFinalizationResult = result;
            Volatile.Write(ref _graphFinalizationAttempted, 1);
            if (result.Success && _graph.Lifetime.IsReleased)
                Volatile.Write(ref _graphFinalized, 1);
            else
            {
                _cleanupErrorCode = ApplicationCandidateSessionErrorCode.ResourceGraphCleanupFailed;
                return result.Success
                    ? ApplicationCandidateResourceGraphCleanupResult.Failed("RESOURCE_GRAPH_NOT_RELEASED")
                    : result;
            }
            return result;
        }
    }
}
