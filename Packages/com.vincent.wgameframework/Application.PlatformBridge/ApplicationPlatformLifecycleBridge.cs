using System;
using System.Threading;
using MxFramework.ApplicationStartup;
using MxFramework.Platform;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Application.Unity")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]

namespace MxFramework.ApplicationStartup.PlatformBridge
{
    /// <summary>Application-lifetime sole drain owner and bounded lifecycle checkpoint dispatcher.</summary>
    /// <remarks>Drain, merge, empty pump, and conforming operation polling are NoAlloc after initialization.</remarks>
    public sealed class ApplicationPlatformLifecycleBridge : IApplicationLiveSafeBoundaryCallback
    {
        private readonly struct EvidenceEpoch
        {
            public EvidenceEpoch(ApplicationLifecycleCheckpointEvidence evidence) => Evidence = evidence;
            public ApplicationLifecycleCheckpointEvidence Evidence { get; }
            public bool HasEvidence => Evidence.ChangeCount != 0;

            public EvidenceEpoch Merge(in ApplicationLifecycleChange change)
            {
                long count = SaturatingIncrement(Evidence.ChangeCount);
                bool resync = change.Kind == ApplicationLifecycleChangeKind.StateResyncRequired;
                bool hasLostRange = resync && change.LostChangeCount > 0;
                long changeFirst = hasLostRange ? change.LostRevisionStart : change.Snapshot.Revision;
                long changeLast = hasLostRange ? change.LostRevisionEnd : change.Snapshot.Revision;
                long first = Evidence.ChangeCount == 0 ? changeFirst : Evidence.FirstRevision;
                bool suspend = (change.Kind == ApplicationLifecycleChangeKind.SuspendChanged && change.Snapshot.IsSuspended)
                    || change.SawSuspendInLostRange;
                bool quitting = change.Kind == ApplicationLifecycleChangeKind.Quitting
                    || change.Snapshot.IsQuitting
                    || change.SawQuittingInLostRange;
                return new EvidenceEpoch(new ApplicationLifecycleCheckpointEvidence(
                    first,
                    changeLast,
                    count,
                    SaturatingAdd(Evidence.LostChangeCount, change.LostChangeCount),
                    Evidence.HasLostRevisionRange || hasLostRange,
                    Evidence.HasLostRevisionRange ? Evidence.LostRevisionStart : (hasLostRange ? change.LostRevisionStart : 0L),
                    hasLostRange ? change.LostRevisionEnd : Evidence.LostRevisionEnd,
                    Evidence.SawSuspend || suspend,
                    Evidence.SawQuitting || quitting,
                    Evidence.SawStateResync || resync,
                    change.Snapshot));
            }

            public EvidenceEpoch Merge(in EvidenceEpoch later)
            {
                if (!HasEvidence) return later;
                if (!later.HasEvidence) return this;
                return new EvidenceEpoch(new ApplicationLifecycleCheckpointEvidence(
                    Evidence.FirstRevision,
                    later.Evidence.LastRevision,
                    SaturatingAdd(Evidence.ChangeCount, later.Evidence.ChangeCount),
                    SaturatingAdd(Evidence.LostChangeCount, later.Evidence.LostChangeCount),
                    Evidence.HasLostRevisionRange || later.Evidence.HasLostRevisionRange,
                    Evidence.HasLostRevisionRange ? Evidence.LostRevisionStart : later.Evidence.LostRevisionStart,
                    later.Evidence.HasLostRevisionRange ? later.Evidence.LostRevisionEnd : Evidence.LostRevisionEnd,
                    Evidence.SawSuspend || later.Evidence.SawSuspend,
                    Evidence.SawQuitting || later.Evidence.SawQuitting,
                    Evidence.SawStateResync || later.Evidence.SawStateResync,
                    later.Evidence.LatestSnapshot));
            }
        }

        private readonly IApplicationLifecycleDrainOwner _drain;
        private readonly IApplicationLifecycleSource _source;
        private readonly LiveApplicationStatePublisher _publisher;
        private readonly ApplicationPublicationSaveFence _fence;
        private readonly IApplicationLifecycleCheckpointPolicy _policy;
        private readonly int _drainBudget;
        private EvidenceEpoch _pending;
        private EvidenceEpoch _inFlight;
        private IApplicationLifecycleCheckpointOperation _operation;
        private ApplicationPublicationSaveFenceLease _saveLease;
        private bool _armed;
        private bool _callbackInvoked;
        private ApplicationLifecycleCheckpointBeginResult _callbackResult;
        private LiveApplicationState _callbackState;
        private ApplicationPublicationSaveFenceLease _callbackSaveLease;
        private bool _callbackFenceBusy;
        private bool _callbackPolicyThrew;
        private bool _callbackRuntimeUnqualified;
        private int _pumpInProgress;
        private long _lastDrainedRevision;
        private LiveApplicationState _lastDeferredState;

        private ApplicationPlatformLifecycleBridge(
            IApplicationLifecycleSource source,
            IApplicationLifecycleDrainOwner drain,
            LiveApplicationStatePublisher publisher,
            ApplicationPublicationSaveFence fence,
            IApplicationLifecycleCheckpointPolicy policy,
            int drainBudget)
        {
            _source = source;
            _drain = drain;
            _publisher = publisher;
            _fence = fence;
            _policy = policy;
            _drainBudget = drainBudget;
        }

        public static ApplicationPlatformLifecycleBridgeCreateStatus TryCreate(
            IApplicationLifecycleSource source,
            LiveApplicationStatePublisher publisher,
            ApplicationPublicationSaveFence fence,
            IApplicationLifecycleCheckpointPolicy policy,
            int drainBudget,
            out ApplicationPlatformLifecycleBridge bridge)
        {
            bridge = null;
            if (source == null || publisher == null || fence == null || policy == null || drainBudget <= 0)
                return ApplicationPlatformLifecycleBridgeCreateStatus.InvalidArgument;
            if (!publisher.UsesPublicationSaveFence(fence))
                return ApplicationPlatformLifecycleBridgeCreateStatus.ForeignFence;
            if (!source.TryClaimDrain(out IApplicationLifecycleDrainOwner drain) || drain == null)
                return ApplicationPlatformLifecycleBridgeCreateStatus.DrainAlreadyClaimed;
            bridge = new ApplicationPlatformLifecycleBridge(source, drain, publisher, fence, policy, drainBudget);
            return ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded;
        }

        internal bool UsesComposition(
            LiveApplicationStatePublisher publisher,
            ApplicationPublicationSaveFence fence,
            IApplicationLifecycleSource source) =>
            ReferenceEquals(_publisher, publisher)
            && ReferenceEquals(_fence, fence)
            && ReferenceEquals(_source, source);

        public bool HasPendingEvidence => _pending.HasEvidence;
        public bool HasActiveCheckpoint => _operation != null;
        public ApplicationLifecycleCheckpointEvidence PendingEvidence => _pending.Evidence;
        public ApplicationLifecycleCheckpointOperationStatus LastCheckpointStatus { get; private set; }

        public void RequestRetry()
        {
            if (_pending.HasEvidence && _pending.Evidence.IsCheckpointWorthy)
            {
                _armed = true;
                _lastDeferredState = null;
            }
        }

        public bool RequestActiveCancellation()
        {
            IApplicationLifecycleCheckpointOperation operation = _operation;
            if (operation == null) return false;
            try
            {
                return operation.CanCancel && operation.RequestCancel();
            }
            catch
            {
                // Cancellation is advisory. Keep the save fence until Poll observes a truthful
                // terminal result rather than racing publication against unknown provider work.
                return false;
            }
        }

        public ApplicationPlatformLifecyclePumpStatus Pump(bool checkpointSafePhase)
        {
            if (Interlocked.CompareExchange(ref _pumpInProgress, 1, 0) != 0)
                return ApplicationPlatformLifecyclePumpStatus.Reentrant;
            try
            {
                return PumpCore(checkpointSafePhase);
            }
            finally
            {
                Volatile.Write(ref _pumpInProgress, 0);
            }
        }

        private ApplicationPlatformLifecyclePumpStatus PumpCore(bool checkpointSafePhase)
        {
            int drained = 0;
            while (drained < _drainBudget && _drain.TryDequeue(out ApplicationLifecycleChange change))
            {
                if (!IsValid(change))
                {
                    if (_operation != null)
                        PollActiveOperation();
                    return ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence;
                }
                _lastDrainedRevision = change.Snapshot.Revision;
                _pending = _pending.Merge(change);
                if (IsCheckpointWorthy(change))
                {
                    _armed = true;
                    _lastDeferredState = null;
                }
                drained++;
            }

            if (_operation != null)
                return PollActiveOperation();
            if (drained == _drainBudget)
                return ApplicationPlatformLifecyclePumpStatus.DrainBudgetExhausted;
            if (!checkpointSafePhase)
                return ApplicationPlatformLifecyclePumpStatus.UnsafePhase;
            if (!_pending.HasEvidence || !_pending.Evidence.IsCheckpointWorthy)
                return ApplicationPlatformLifecyclePumpStatus.Idle;
            LiveApplicationState observed = _publisher.Current;
            if (!_armed && _lastDeferredState != null && !ReferenceEquals(observed, _lastDeferredState))
                _armed = true;
            if (!_armed)
                return ApplicationPlatformLifecyclePumpStatus.NotArmed;
            if (ReferenceEquals(observed, _lastDeferredState))
                return ApplicationPlatformLifecyclePumpStatus.NotArmed;
            _callbackInvoked = false;
            _callbackState = null;
            _callbackResult = default;
            _callbackSaveLease = null;
            _callbackFenceBusy = false;
            _callbackPolicyThrew = false;
            _callbackRuntimeUnqualified = false;
            ApplicationLiveSafeBoundaryDispatchStatus dispatch = _publisher.TryDispatchSafeBoundary(this);
            if (dispatch != ApplicationLiveSafeBoundaryDispatchStatus.Succeeded || !_callbackInvoked)
            {
                return ApplicationPlatformLifecyclePumpStatus.PublisherBoundaryBusy;
            }
            if (_callbackFenceBusy)
                return ApplicationPlatformLifecyclePumpStatus.PublicationFenceBusy;
            if (_callbackRuntimeUnqualified)
            {
                _armed = false;
                _lastDeferredState = _callbackState;
                return ApplicationPlatformLifecyclePumpStatus.DeferredUntilSuccessfulRuntimeTick;
            }

            if (_callbackResult.Status == ApplicationLifecycleCheckpointBeginStatus.Started &&
                _callbackResult.Operation != null)
            {
                _operation = _callbackResult.Operation;
                LastCheckpointStatus = ApplicationLifecycleCheckpointOperationStatus.Pending;
                _saveLease = _callbackSaveLease;
                _callbackSaveLease = null;
                _inFlight = _pending;
                _pending = default;
                _armed = false;
                _lastDeferredState = null;
                return ApplicationPlatformLifecyclePumpStatus.CheckpointStarted;
            }

            _callbackSaveLease?.Dispose();
            _callbackSaveLease = null;
            _armed = false;
            _lastDeferredState = _callbackState;
            if (_callbackPolicyThrew)
            {
                LastCheckpointStatus = ApplicationLifecycleCheckpointOperationStatus.Failed;
                return ApplicationPlatformLifecyclePumpStatus.InvalidPolicyResult;
            }
            if (_callbackResult.Status == ApplicationLifecycleCheckpointBeginStatus.Started ||
                (_callbackResult.Status != ApplicationLifecycleCheckpointBeginStatus.Deferred &&
                 _callbackResult.Status != ApplicationLifecycleCheckpointBeginStatus.Failed) ||
                _callbackResult.Operation != null)
                return ApplicationPlatformLifecyclePumpStatus.InvalidPolicyResult;
            if (_callbackResult.Status == ApplicationLifecycleCheckpointBeginStatus.Failed)
                LastCheckpointStatus = ApplicationLifecycleCheckpointOperationStatus.Failed;
            return _callbackResult.Status == ApplicationLifecycleCheckpointBeginStatus.Deferred
                ? (_callbackState == null || !_callbackState.HasRuntimeSession || _callbackState.CheckpointCapability == null
                    ? ApplicationPlatformLifecyclePumpStatus.DeferredAtBootOrMissingCapability
                    : ApplicationPlatformLifecyclePumpStatus.DeferredByPolicy)
                : ApplicationPlatformLifecyclePumpStatus.CheckpointFailed;
        }

        void IApplicationLiveSafeBoundaryCallback.Invoke(LiveApplicationState exactState)
        {
            _callbackInvoked = true;
            _callbackState = exactState;
            IApplicationLiveCheckpointCapability capability = exactState.CheckpointCapability;
            if (!exactState.HasRuntimeSession || capability == null)
            {
                _callbackResult = ApplicationLifecycleCheckpointBeginResult.Deferred;
                return;
            }
            if (!_publisher.IsCheckpointQualified(exactState))
            {
                _callbackRuntimeUnqualified = true;
                _callbackResult = ApplicationLifecycleCheckpointBeginResult.Deferred;
                return;
            }
            if (!_fence.TryAcquireSave(out _callbackSaveLease))
            {
                _callbackFenceBusy = true;
                _callbackResult = ApplicationLifecycleCheckpointBeginResult.Deferred;
                return;
            }
            try
            {
                _callbackResult = _policy.TryBegin(exactState, _pending.Evidence);
            }
            catch
            {
                _callbackPolicyThrew = true;
                _callbackResult = ApplicationLifecycleCheckpointBeginResult.Failed;
            }
        }

        private ApplicationPlatformLifecyclePumpStatus PollActiveOperation()
        {
            ApplicationLifecycleCheckpointOperationStatus status;
            try
            {
                _operation.Poll();
                status = _operation.Status;
            }
            catch { status = ApplicationLifecycleCheckpointOperationStatus.Failed; }
            if (status == ApplicationLifecycleCheckpointOperationStatus.Pending)
                return ApplicationPlatformLifecyclePumpStatus.CheckpointPending;

            bool succeeded = status == ApplicationLifecycleCheckpointOperationStatus.Succeeded;
            bool successorArmed = _armed;
            bool validTerminal = succeeded
                || status == ApplicationLifecycleCheckpointOperationStatus.Failed
                || status == ApplicationLifecycleCheckpointOperationStatus.Cancelled
                || status == ApplicationLifecycleCheckpointOperationStatus.GenerationConflict
                || status == ApplicationLifecycleCheckpointOperationStatus.TimedOut;
            _operation = null;
            LastCheckpointStatus = status;
            _saveLease.Dispose();
            _saveLease = null;
            if (succeeded)
            {
                _inFlight = default;
                return ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded;
            }

            _pending = _inFlight.Merge(_pending);
            _inFlight = default;
            _armed = successorArmed;
            return validTerminal
                ? ApplicationPlatformLifecyclePumpStatus.CheckpointFailed
                : ApplicationPlatformLifecyclePumpStatus.InvalidPolicyResult;
        }

        private static long SaturatingIncrement(long value) =>
            value == long.MaxValue ? long.MaxValue : value + 1L;
        private static long SaturatingAdd(long value, long increment) =>
            value >= long.MaxValue - increment ? long.MaxValue : value + increment;

        private static bool IsCheckpointWorthy(in ApplicationLifecycleChange change) =>
            (change.Kind == ApplicationLifecycleChangeKind.SuspendChanged && change.Snapshot.IsSuspended)
            || change.Kind == ApplicationLifecycleChangeKind.Quitting
            || change.SawSuspendInLostRange
            || change.SawQuittingInLostRange;

        private bool IsValid(in ApplicationLifecycleChange change)
        {
            long expectedRevision = _lastDrainedRevision == long.MaxValue
                ? long.MaxValue
                : _lastDrainedRevision + 1L;
            bool ordinary = change.Kind == ApplicationLifecycleChangeKind.FocusChanged
                || change.Kind == ApplicationLifecycleChangeKind.SuspendChanged
                || change.Kind == ApplicationLifecycleChangeKind.Quitting;
            if (ordinary)
                return _lastDrainedRevision != long.MaxValue
                    && change.Snapshot.Revision == expectedRevision
                    && change.LostRevisionStart == 0
                    && change.LostRevisionEnd == 0
                    && change.LostChangeCount == 0
                    && !change.SawSuspendInLostRange
                    && !change.SawQuittingInLostRange;
            if (change.Kind != ApplicationLifecycleChangeKind.StateResyncRequired)
                return false;
            return change.Snapshot.Revision > 0
                && change.LostRevisionStart == expectedRevision
                && change.LostRevisionEnd >= change.LostRevisionStart
                && change.LostRevisionEnd == change.Snapshot.Revision
                && change.LostChangeCount > 0;
        }
    }
}
