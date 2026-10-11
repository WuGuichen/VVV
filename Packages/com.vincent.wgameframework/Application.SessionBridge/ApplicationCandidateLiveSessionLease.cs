using System;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    /// <summary>
    /// Internal aggregate owner used by outer composition bridges after one candidate graph freezes.
    /// It keeps publication preparation reversible and owns ordered live retirement.
    /// </summary>
    /// <remarks>
    /// Construction and retirement creation are LowFreqAlloc. Publication final claim and steady
    /// retirement polling are NoAlloc after initialization.
    /// </remarks>
    internal sealed class ApplicationCandidateLiveSessionLease :
        IApplicationLiveSessionLease,
        IApplicationLiveSessionPublicationHandoff,
        IApplicationLiveRetirementSource,
        IApplicationLiveRuntimePump
    {
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly ApplicationCandidateFrozenHandoffLease _frozenHandoff;
        private readonly ApplicationCandidateFrozenHandoffOwner _handoffOwner;
        private readonly object _gate = new object();
        private int _state;
        private ApplicationCandidateLiveRetirementOperation _cleanupOperation;

        internal ApplicationCandidateLiveSessionLease(string sessionId, ApplicationCandidateSessionGraph graph)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A live candidate session id is required.", nameof(sessionId));
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            if (!graph.TryGetFrozenHandoffLease(out _frozenHandoff) || _frozenHandoff == null)
                throw new InvalidOperationException("Candidate graph must be frozen before live aggregation.");
            SessionId = sessionId;
            _handoffOwner = new ApplicationCandidateFrozenHandoffOwner();
        }

        public string SessionId { get; }
        internal bool HasLiveOwnership
        {
            get
            {
                int state = Volatile.Read(ref _state);
                return state == 2 || state == 3;
            }
        }
        internal bool IsRetiring => Volatile.Read(ref _state) == 3;
        internal bool IsPoisoned => Volatile.Read(ref _state) >= 4;
        internal IApplicationLiveRetirementOperation CleanupOperation =>
            Volatile.Read(ref _cleanupOperation);

        public bool TryPreparePublicationHandoff(out ApplicationStartupError error)
        {
            lock (_gate)
            {
                int state = Volatile.Read(ref _state);
                if (state == 1)
                {
                    error = ApplicationStartupError.None;
                    return true;
                }
                if (state != 0 || !_frozenHandoff.TryPrepareClaim(_handoffOwner))
                {
                    error = CreateHandoffError("Candidate graph is not available for publication handoff.");
                    return false;
                }
                Volatile.Write(ref _state, 1);
                error = ApplicationStartupError.None;
                return true;
            }
        }

        public bool ClaimPublicationHandoff()
        {
            lock (_gate)
            {
                if (Volatile.Read(ref _state) != 1)
                    return false;
                if (!_frozenHandoff.TryClaimPrepared(_handoffOwner))
                    return false;
                _graph.PromoteClaimedHandoffToLiveOwner();
                Volatile.Write(ref _state, 2);
                return true;
            }
        }

        public IApplicationLiveRetirementOperation BeginRetirement(in ApplicationLiveRetirementContext context)
        {
            lock (_gate)
            {
                ApplicationCandidateLiveRetirementOperation existing = _cleanupOperation;
                if (existing != null)
                    return existing;
                if (Volatile.Read(ref _state) != 2)
                    return null;
                var operation = new ApplicationCandidateLiveRetirementOperation(_graph, true);
                Volatile.Write(ref _cleanupOperation, operation);
                Volatile.Write(ref _state, 3);
                return operation;
            }
        }

        internal bool TryPoisonAndBeginRollback(
            out IApplicationLiveRetirementOperation cleanupOperation)
        {
            lock (_gate)
            {
                int state = Volatile.Read(ref _state);
                // A successful publication preflight is the publisher's no-failure boundary.
                // From that point onward only the final claim may consume the reservation.
                if (state != 0)
                {
                    cleanupOperation = _cleanupOperation;
                    return false;
                }
                var operation = new ApplicationCandidateLiveRetirementOperation(_graph, false);
                Volatile.Write(ref _cleanupOperation, operation);
                Volatile.Write(ref _state, 4);
                cleanupOperation = operation;
                return true;
            }
        }

        public void Dispose()
        {
            int state = Volatile.Read(ref _state);
            if (state < 2)
            {
                if (TryPoisonAndBeginRollback(out IApplicationLiveRetirementOperation cleanup))
                    cleanup.Tick(default);
                return;
            }
            // Once published, only the publisher-owned retirement receipt may start cleanup.
        }

        void IApplicationLiveRuntimePump.Tick(long frameIndex, double deltaTime, double elapsedTime)
        {
            if (Volatile.Read(ref _state) != 2)
                throw new InvalidOperationException("Only the published live owner may pump this session.");
            _graph.TickLive(frameIndex, deltaTime, elapsedTime);
        }

        ApplicationLiveRuntimeAttemptStatus IApplicationLiveRuntimePump.GetTickAttemptStatus(
            long frameIndex) => _graph.GetRuntimeTickAttemptStatus(frameIndex);

        private static ApplicationStartupError CreateHandoffError(string message) =>
            new ApplicationStartupError(
                ApplicationStartupErrorCode.CommitFailed,
                ApplicationStartupPhase.Commit,
                default,
                RuntimeOperationError.None,
                null,
                message,
                null);
    }

    internal sealed class ApplicationCandidateLiveRetirementOperation : IApplicationLiveRetirementOperation
    {
        private const int DomainCount = 4;
        private readonly ApplicationCandidateSessionGraph _graph;
        private readonly bool _fromLiveOwner;
        private readonly IApplicationStartupStepOperation[] _operations;
        private readonly ApplicationLiveRetirementWarning[] _warnings;
        private readonly ApplicationStartupTickContext _tickContext;
        private RuntimeOperationStatus _status;
        private RuntimeOperationError _error;
        private ApplicationLiveRetirementOwnershipDebt _debt;
        private int _warningCount;
        private int _operationIndex;
        private int _debtDomainIndex = int.MaxValue;
        private bool _ownershipClaimed;

        internal ApplicationCandidateLiveRetirementOperation(
            ApplicationCandidateSessionGraph graph,
            bool fromLiveOwner)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _fromLiveOwner = fromLiveOwner;
            _operations = new IApplicationStartupStepOperation[]
            {
                new CandidateRuntimeRollbackOperation(graph, fromLiveOwner),
                new CandidateAppFlowRollbackOperation(graph, fromLiveOwner),
                graph.SceneLease != null ? new CandidateSceneRollbackOperation(graph, fromLiveOwner) : null,
                new CandidateResourceRollbackOperation(graph, fromLiveOwner)
            };
            _warnings = new ApplicationLiveRetirementWarning[DomainCount];
            _tickContext = new ApplicationStartupTickContext(0d, 0d);
            _status = RuntimeOperationStatus.Running;
            _error = RuntimeOperationError.None;
        }

        public string OperationId => "mxframework.application.session-bridge.live-retirement";
        public RuntimeOperationStatus Status => _status;
        public float Progress => _status == RuntimeOperationStatus.Succeeded || _status == RuntimeOperationStatus.Failed
            ? 1f
            : (_operationIndex + ReadProgress(_operations[_operationIndex])) / DomainCount;
        public RuntimeOperationError Error => _error;
        public int WarningCount => _warningCount;
        public ApplicationLiveRetirementOwnershipDebt OwnershipDebt => _debt;

        public ApplicationLiveRetirementWarning GetWarning(int index)
        {
            if ((uint)index >= (uint)_warningCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _warnings[index];
        }

        /// <remarks>NoAlloc after initialization outside implementation-owned failure diagnostics.</remarks>
        public void Tick(in ApplicationLiveRetirementContext context)
        {
            if (_status != RuntimeOperationStatus.Running)
                return;
            if (!_ownershipClaimed)
            {
                _ownershipClaimed = true;
                ApplicationCandidateSessionErrorCode ownership = _fromLiveOwner
                    ? _graph.BeginLiveRetirementOwnership()
                    : _graph.BeginRollbackOwnership();
                if (ownership != ApplicationCandidateSessionErrorCode.None)
                {
                    RecordFailure(0, "ownership.conflict", RuntimeOperationError.None, null);
                    _status = RuntimeOperationStatus.Failed;
                    return;
                }
                if (_fromLiveOwner && _graph.SceneLease != null &&
                    _graph.SceneLease.HasCleanupFailure)
                {
                    RecordDebt(
                        2,
                        "scene.make-inert-failed",
                        "Retired candidate scene could not be made inert during ownership transfer.",
                        _graph.SceneLease.CleanupException);
                }
            }

            while (_operationIndex < DomainCount)
            {
                IApplicationStartupStepOperation operation = _operations[_operationIndex];
                if (operation == null)
                {
                    _operationIndex++;
                    continue;
                }

                RuntimeOperationStatus nestedStatus;
                try
                {
                    nestedStatus = operation.Status;
                    if (!IsTerminal(nestedStatus))
                    {
                        operation.Tick(in _tickContext);
                        nestedStatus = operation.Status;
                    }
                }
                catch (Exception exception)
                {
                    RecordFailure(_operationIndex, "operation.exception", RuntimeOperationError.None, exception);
                    _operationIndex++;
                    continue;
                }

                if (!IsTerminal(nestedStatus))
                    return;
                if (nestedStatus != RuntimeOperationStatus.Succeeded)
                {
                    RuntimeOperationError nestedError;
                    try { nestedError = operation.Error; }
                    catch { nestedError = RuntimeOperationError.None; }
                    RecordFailure(_operationIndex, "operation.failed", nestedError, null);
                }
                _operationIndex++;
            }

            _status = _debt.IsNone ? RuntimeOperationStatus.Succeeded : RuntimeOperationStatus.Failed;
            if (_status == RuntimeOperationStatus.Succeeded)
                _error = RuntimeOperationError.None;
        }

        private void RecordFailure(int domainIndex, string stableCode, RuntimeOperationError innerError, Exception exception)
        {
            string domain = GetDomain(domainIndex);
            const string message = "Candidate live-session retirement did not discharge one ownership domain.";
            if (_warningCount < _warnings.Length)
            {
                _warnings[_warningCount++] = new ApplicationLiveRetirementWarning(
                    domain, stableCode, innerError, message, exception);
            }
            RecordDebt(domainIndex, stableCode, message, exception);
            if (_error.IsNone)
                _error = innerError.IsNone ? new RuntimeOperationError(stableCode, message) : innerError;
        }

        private void RecordDebt(int domainIndex, string stableCode, string message, Exception exception)
        {
            if (domainIndex >= _debtDomainIndex)
                return;
            _debtDomainIndex = domainIndex;
            _debt = new ApplicationLiveRetirementOwnershipDebt(
                GetDomain(domainIndex), stableCode, message, exception);
        }

        private static string GetDomain(int index)
        {
            switch (index)
            {
                case 0: return "runtime";
                case 1: return "app-flow";
                case 2: return "scene";
                default: return "resources";
            }
        }

        private static float ReadProgress(IApplicationStartupStepOperation operation)
        {
            if (operation == null)
                return 1f;
            try { return operation.Progress; }
            catch { return 0f; }
        }

        private static bool IsTerminal(RuntimeOperationStatus status) =>
            status == RuntimeOperationStatus.Succeeded ||
            status == RuntimeOperationStatus.Failed ||
            status == RuntimeOperationStatus.Cancelled ||
            status == RuntimeOperationStatus.TimedOut;
    }
}
