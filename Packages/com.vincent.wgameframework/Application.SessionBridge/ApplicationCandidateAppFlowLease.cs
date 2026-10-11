using System;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    /// <summary>Assembly-owned authority wrapper around the construction-time AppFlow lifecycle.</summary>
    internal sealed class ApplicationCandidateAppFlowLease
    {
        private readonly IApplicationCandidateAppFlowLifecycle _lifecycle;
        private IApplicationStartupStepOperation _rollbackOperation;
        private int _status = (int)ApplicationCandidateAppFlowLeaseStatus.Created;

        public ApplicationCandidateAppFlowLease(IApplicationCandidateAppFlowLifecycle lifecycle) =>
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));

        internal IApplicationCandidateAppFlowLifecycle Lifecycle => _lifecycle;
        internal AppFlowController Controller => _lifecycle.Controller;
        internal bool CanCompensate => _lifecycle.CanCompensate;
        internal ApplicationCandidateAppFlowLeaseStatus Status =>
            (ApplicationCandidateAppFlowLeaseStatus)Volatile.Read(ref _status);

        internal IApplicationStartupStepOperation BeginPrepare()
        {
            if (Interlocked.CompareExchange(ref _status,
                    (int)ApplicationCandidateAppFlowLeaseStatus.Preparing,
                    (int)ApplicationCandidateAppFlowLeaseStatus.Created) !=
                (int)ApplicationCandidateAppFlowLeaseStatus.Created)
                throw new InvalidOperationException("AppFlow preparation authority was already consumed.");
            return _lifecycle.PrepareStart();
        }

        internal void MarkPrepared()
        {
            if (Interlocked.CompareExchange(ref _status,
                    (int)ApplicationCandidateAppFlowLeaseStatus.Prepared,
                    (int)ApplicationCandidateAppFlowLeaseStatus.Preparing) !=
                (int)ApplicationCandidateAppFlowLeaseStatus.Preparing)
                throw new InvalidOperationException("AppFlow prepare did not own the lease.");
        }

        internal void FreezeForHandoff() => _lifecycle.FreezeForHandoff();

        internal IApplicationStartupStepOperation BeginRollback()
        {
            while (true)
            {
                ApplicationCandidateAppFlowLeaseStatus status = Status;
                if (status == ApplicationCandidateAppFlowLeaseStatus.RollingBack ||
                    status == ApplicationCandidateAppFlowLeaseStatus.RolledBack)
                    return _rollbackOperation;
                if (status == ApplicationCandidateAppFlowLeaseStatus.Debt)
                    throw new InvalidOperationException("AppFlow lease is poisoned by cleanup debt.");
                if (Interlocked.CompareExchange(ref _status,
                        (int)ApplicationCandidateAppFlowLeaseStatus.RollingBack,
                        (int)status) == (int)status)
                    break;
            }
            try
            {
                IApplicationStartupStepOperation operation = _lifecycle.BeginRollback();
                Volatile.Write(ref _rollbackOperation, operation);
                if (operation == null) MarkDebt();
                return operation;
            }
            catch
            {
                MarkDebt();
                throw;
            }
        }

        internal void MarkRolledBack()
        {
            if (Interlocked.CompareExchange(ref _status,
                    (int)ApplicationCandidateAppFlowLeaseStatus.RolledBack,
                    (int)ApplicationCandidateAppFlowLeaseStatus.RollingBack) !=
                (int)ApplicationCandidateAppFlowLeaseStatus.RollingBack)
                throw new InvalidOperationException("AppFlow rollback did not own the lease.");
        }

        internal void MarkDebt() => Volatile.Write(ref _status, (int)ApplicationCandidateAppFlowLeaseStatus.Debt);

        internal void MarkRolledBackIfNeverPrepared()
        {
            Interlocked.CompareExchange(ref _status,
                (int)ApplicationCandidateAppFlowLeaseStatus.RolledBack,
                (int)ApplicationCandidateAppFlowLeaseStatus.Created);
        }

        internal void DisposeRolledBack()
        {
            if (Status == ApplicationCandidateAppFlowLeaseStatus.Disposed) return;
            if (Status != ApplicationCandidateAppFlowLeaseStatus.RolledBack)
                throw new InvalidOperationException("AppFlow may be disposed only after rollback terminal success.");
            try
            {
                _lifecycle.Dispose();
                Volatile.Write(ref _status, (int)ApplicationCandidateAppFlowLeaseStatus.Disposed);
            }
            catch
            {
                MarkDebt();
                throw;
            }
        }
    }
}
