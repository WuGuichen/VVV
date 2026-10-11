using MxFramework.ApplicationStartup;
using MxFramework.Platform;

namespace MxFramework.ApplicationStartup.PlatformBridge
{
    public readonly struct ApplicationLifecycleCheckpointEvidence
    {
        internal ApplicationLifecycleCheckpointEvidence(
            long firstRevision,
            long lastRevision,
            long changeCount,
            long lostChangeCount,
            bool hasLostRevisionRange,
            long lostRevisionStart,
            long lostRevisionEnd,
            bool sawSuspend,
            bool sawQuitting,
            bool sawStateResync,
            ApplicationLifecycleSnapshot latestSnapshot)
        {
            FirstRevision = firstRevision;
            LastRevision = lastRevision;
            ChangeCount = changeCount;
            LostChangeCount = lostChangeCount;
            HasLostRevisionRange = hasLostRevisionRange;
            LostRevisionStart = lostRevisionStart;
            LostRevisionEnd = lostRevisionEnd;
            SawSuspend = sawSuspend;
            SawQuitting = sawQuitting;
            SawStateResync = sawStateResync;
            LatestSnapshot = latestSnapshot;
        }

        public long FirstRevision { get; }
        public long LastRevision { get; }
        public long ChangeCount { get; }
        public long LostChangeCount { get; }
        public bool HasLostRevisionRange { get; }
        public long LostRevisionStart { get; }
        public long LostRevisionEnd { get; }
        public bool SawSuspend { get; }
        public bool SawQuitting { get; }
        public bool SawStateResync { get; }
        public ApplicationLifecycleSnapshot LatestSnapshot { get; }
        public bool IsCheckpointWorthy => SawSuspend || SawQuitting;
    }

    public enum ApplicationLifecycleCheckpointOperationStatus
    {
        Unknown = 0,
        Pending = 1,
        Succeeded = 2,
        Failed = 3,
        Cancelled = 4,
        GenerationConflict = 5,
        TimedOut = 6
    }

    public interface IApplicationLifecycleCheckpointOperation
    {
        /// <remarks>NoAlloc after operation initialization for conforming providers.</remarks>
        void Poll();
        ApplicationLifecycleCheckpointOperationStatus Status { get; }
        bool CanCancel { get; }
        bool RequestCancel();
    }

    public enum ApplicationLifecycleCheckpointBeginStatus
    {
        Started = 0,
        Deferred = 1,
        Failed = 2
    }

    public readonly struct ApplicationLifecycleCheckpointBeginResult
    {
        public ApplicationLifecycleCheckpointBeginResult(
            ApplicationLifecycleCheckpointBeginStatus status,
            IApplicationLifecycleCheckpointOperation operation)
        {
            Status = status;
            Operation = operation;
        }
        public ApplicationLifecycleCheckpointBeginStatus Status { get; }
        public IApplicationLifecycleCheckpointOperation Operation { get; }
        public static ApplicationLifecycleCheckpointBeginResult Started(IApplicationLifecycleCheckpointOperation operation) =>
            new ApplicationLifecycleCheckpointBeginResult(ApplicationLifecycleCheckpointBeginStatus.Started, operation);
        public static ApplicationLifecycleCheckpointBeginResult Deferred =>
            new ApplicationLifecycleCheckpointBeginResult(ApplicationLifecycleCheckpointBeginStatus.Deferred, null);
        public static ApplicationLifecycleCheckpointBeginResult Failed =>
            new ApplicationLifecycleCheckpointBeginResult(ApplicationLifecycleCheckpointBeginStatus.Failed, null);
    }

    public interface IApplicationLifecycleCheckpointPolicy
    {
        /// <remarks>LowFreqAlloc. The capability is bound to exactState.</remarks>
        ApplicationLifecycleCheckpointBeginResult TryBegin(
            LiveApplicationState exactState,
            in ApplicationLifecycleCheckpointEvidence evidence);
    }

    public enum ApplicationPlatformLifecycleBridgeCreateStatus
    {
        Succeeded = 0,
        InvalidArgument = 1,
        DrainAlreadyClaimed = 2,
        ForeignFence = 3
    }

    public enum ApplicationPlatformLifecyclePumpStatus
    {
        Idle = 0,
        DrainBudgetExhausted = 1,
        CheckpointStarted = 2,
        CheckpointPending = 3,
        CheckpointSucceeded = 4,
        CheckpointFailed = 5,
        DeferredAtBootOrMissingCapability = 6,
        DeferredByPolicy = 7,
        PublicationFenceBusy = 8,
        UnsafePhase = 9,
        NotArmed = 10,
        PublisherBoundaryBusy = 11,
        InvalidPolicyResult = 12,
        Reentrant = 13,
        DeferredUntilSuccessfulRuntimeTick = 14,
        InvalidLifecycleEvidence = 15
    }
}
