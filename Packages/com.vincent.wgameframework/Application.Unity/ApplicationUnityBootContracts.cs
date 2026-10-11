using System;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.Platform.Unity;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MxFramework.Tests.PlayMode.Application")]

namespace MxFramework.ApplicationStartup.Unity
{
    public enum ApplicationUnityBootRootState
    {
        Unknown = 0,
        Uninitialized = 1,
        Ready = 2,
        Running = 3,
        ShuttingDown = 4,
        ShutdownComplete = 5,
        Failed = 6,
        ShutdownDebt = 7
    }

    public enum ApplicationUnityBootErrorCode
    {
        None = 0,
        InvalidShell = 1,
        OwnershipAlreadyClaimed = 2,
        FactoryException = 3,
        FactoryRejected = 4,
        InvalidComposition = 5,
        ForeignComposition = 6,
        FrameDriverAlreadyClaimed = 7,
        ProxyBindFailed = 8,
        WrongThread = 9,
        AttemptExhausted = 10,
        RunFactoryException = 11,
        RunFactoryRejected = 12,
        InvalidRun = 13,
        ForeignPublisher = 14,
        RunStartFailed = 15,
        FixedClockInvalid = 16,
        UpdateClockInvalid = 17,
        RetirementOwnershipDebt = 18,
        UnexpectedDestroy = 19,
        SurfaceCallbackFailed = 20,
        LifecyclePumpFailed = 21,
        RetirementPumpFailed = 22,
        LiveFrameFailed = 23,
        RunFactoryUnavailable = 24,
        LiveSessionStillOwned = 25,
        PriorRunReleaseFailed = 26
    }

    public enum ApplicationUnityBootCompositionCreateStatus
    {
        Succeeded = 0,
        InvalidConfiguration = 1,
        Failed = 2
    }

    public enum ApplicationUnityStartupRunCreateStatus
    {
        Succeeded = 0,
        InvalidIntent = 1,
        Unavailable = 2,
        Failed = 3
    }

    public enum ApplicationUnityStartupIntentKind
    {
        Unknown = 0,
        ColdBoot = 1,
        NewSession = 2,
        LoadExact = 3
    }

    public enum ApplicationUnityStartupRequestKind
    {
        Unknown = 0,
        Start = 1,
        Retry = 2,
        Cancel = 3
    }

    public enum ApplicationUnityDriverUpdateStatus
    {
        Succeeded = 0,
        WrongThread = 1,
        InvalidTime = 2,
        Reentrant = 3,
        ShutdownComplete = 4,
        ShutdownDebt = 5
    }

    /// <summary>Typed, immutable startup selection. Construction may validate strings and is LowFreqAlloc.</summary>
    public readonly struct ApplicationUnityStartupIntent
    {
        public ApplicationUnityStartupIntent(ApplicationUnityStartupIntentKind kind, string profileId = null, string slotId = null)
        {
            Kind = kind;
            ProfileId = profileId ?? string.Empty;
            SlotId = slotId ?? string.Empty;
        }

        public ApplicationUnityStartupIntentKind Kind { get; }
        public string ProfileId { get; }
        public string SlotId { get; }
        public bool IsValid =>
            Kind == ApplicationUnityStartupIntentKind.ColdBoot ||
            Kind == ApplicationUnityStartupIntentKind.NewSession ||
            (Kind == ApplicationUnityStartupIntentKind.LoadExact &&
             !string.IsNullOrWhiteSpace(ProfileId) && !string.IsNullOrWhiteSpace(SlotId));
    }

    /// <summary>Bounded surface command. Value construction and reads are NoAlloc.</summary>
    public readonly struct ApplicationUnityStartupSurfaceRequest
    {
        public ApplicationUnityStartupSurfaceRequest(long requestId, ApplicationUnityStartupRequestKind kind)
        {
            RequestId = requestId;
            Kind = kind;
        }

        public long RequestId { get; }
        public ApplicationUnityStartupRequestKind Kind { get; }
        public bool IsValid => RequestId > 0 &&
            (Kind == ApplicationUnityStartupRequestKind.Start ||
             Kind == ApplicationUnityStartupRequestKind.Retry ||
             Kind == ApplicationUnityStartupRequestKind.Cancel);
    }

    public interface IApplicationUnityStartupSurface
    {
        /// <remarks>NoAlloc after surface initialization.</remarks>
        bool TryDequeueRequest(out ApplicationUnityStartupSurfaceRequest request);
        /// <remarks>NoAlloc while facts are unchanged; display-value transitions may allocate by design.</remarks>
        void Apply(in ApplicationUnityStartupSurfaceModel model);
    }

    /// <summary>Shared typed request eligibility. All methods are NoAlloc.</summary>
    public static class ApplicationUnityStartupRequestPolicy
    {
        public static bool CanStart(ApplicationUnityBootRootState rootState, ApplicationStartupState startupState, long attempt) =>
            rootState == ApplicationUnityBootRootState.Ready && startupState == ApplicationStartupState.Created && attempt == 0;

        public static bool CanRetry(
            ApplicationUnityBootRootState rootState,
            ApplicationUnityBootErrorCode rootError,
            ApplicationStartupState startupState) =>
            (rootState == ApplicationUnityBootRootState.Failed &&
             rootError == ApplicationUnityBootErrorCode.RunFactoryUnavailable) ||
            (rootState == ApplicationUnityBootRootState.Ready &&
             rootError == ApplicationUnityBootErrorCode.None &&
             (startupState == ApplicationStartupState.Failed ||
              startupState == ApplicationStartupState.Cancelled ||
              startupState == ApplicationStartupState.TimedOut));

        public static bool CanCancel(ApplicationStartupState startupState, bool cancelRequested) =>
            !cancelRequested &&
            (startupState == ApplicationStartupState.Running ||
             startupState == ApplicationStartupState.Draining ||
             startupState == ApplicationStartupState.RollingBack);
    }

    public interface IApplicationUnityStartupRunFactory
    {
        /// <remarks>AllocByDesign. Every success must return a fresh, unstarted coordinator.</remarks>
        ApplicationUnityStartupRunCreateResult TryCreate(
            in ApplicationUnityStartupIntent intent,
            long attempt,
            LiveApplicationStatePublisher publisher);
    }

    public interface IApplicationUnityBootCompositionFactory
    {
        /// <remarks>AllocByDesign during Boot-root initialization.</remarks>
        ApplicationUnityBootCompositionCreateResult TryCreate();
    }

    public readonly struct ApplicationUnityBootCompositionCreateResult
    {
        public ApplicationUnityBootCompositionCreateResult(
            ApplicationUnityBootCompositionCreateStatus status,
            ApplicationUnityBootComposition composition,
            string stableErrorCode = null)
        {
            Status = status;
            Composition = composition;
            StableErrorCode = stableErrorCode ?? string.Empty;
        }

        public ApplicationUnityBootCompositionCreateStatus Status { get; }
        public ApplicationUnityBootComposition Composition { get; }
        public string StableErrorCode { get; }
        public bool Success => Status == ApplicationUnityBootCompositionCreateStatus.Succeeded && Composition != null;
        public static ApplicationUnityBootCompositionCreateResult Succeeded(ApplicationUnityBootComposition composition) =>
            new ApplicationUnityBootCompositionCreateResult(ApplicationUnityBootCompositionCreateStatus.Succeeded, composition);
    }

    public readonly struct ApplicationUnityStartupRunCreateResult
    {
        public ApplicationUnityStartupRunCreateResult(
            ApplicationUnityStartupRunCreateStatus status,
            ApplicationStartupCoordinator coordinator,
            string stableErrorCode = null)
        {
            Status = status;
            Coordinator = coordinator;
            StableErrorCode = stableErrorCode ?? string.Empty;
        }

        public ApplicationUnityStartupRunCreateStatus Status { get; }
        public ApplicationStartupCoordinator Coordinator { get; }
        public string StableErrorCode { get; }
        public bool Success => Status == ApplicationUnityStartupRunCreateStatus.Succeeded && Coordinator != null;
        public static ApplicationUnityStartupRunCreateResult Succeeded(ApplicationStartupCoordinator coordinator) =>
            new ApplicationUnityStartupRunCreateResult(ApplicationUnityStartupRunCreateStatus.Succeeded, coordinator);
    }

    /// <summary>Immutable exact-reference Boot composition. Construction is AllocByDesign.</summary>
    public sealed class ApplicationUnityBootComposition
    {
        public ApplicationUnityBootComposition(
            LiveApplicationStatePublisher publisher,
            ApplicationPublicationSaveFence publicationSaveFence,
            ApplicationPlatformLifecycleBridge lifecycleBridge,
            UnityApplicationLifecycleReporter lifecycleReporter,
            IApplicationUnityStartupRunFactory runFactory,
            in ApplicationUnityStartupIntent initialIntent,
            int retirementSlotCapacity)
        {
            Publisher = publisher;
            PublicationSaveFence = publicationSaveFence;
            LifecycleBridge = lifecycleBridge;
            LifecycleReporter = lifecycleReporter;
            RunFactory = runFactory;
            InitialIntent = initialIntent;
            RetirementSlotCapacity = retirementSlotCapacity;
        }

        public LiveApplicationStatePublisher Publisher { get; }
        public ApplicationPublicationSaveFence PublicationSaveFence { get; }
        public ApplicationPlatformLifecycleBridge LifecycleBridge { get; }
        public UnityApplicationLifecycleReporter LifecycleReporter { get; }
        public IApplicationUnityStartupRunFactory RunFactory { get; }
        public ApplicationUnityStartupIntent InitialIntent { get; }
        public int RetirementSlotCapacity { get; }
    }

    /// <summary>Cached read-only presentation value. Construction and property reads are NoAlloc.</summary>
    public readonly struct ApplicationUnityStartupSurfaceModel
    {
        internal ApplicationUnityStartupSurfaceModel(
            ApplicationUnityBootRootState rootState, ApplicationUnityBootErrorCode rootError,
            long attempt, ApplicationStartupPlanId planId, string runId,
            ApplicationStartupState startupState, ApplicationStartupPhase phase,
            ApplicationStartupStepId stepId, float forwardProgress, float rollbackProgress,
            bool cancelRequested, ApplicationStartupErrorCode applicationErrorCode,
            string runtimeErrorCode, long liveRevision, bool hasRuntimeSession,
            ApplicationLiveFrameTickStatus liveFrameStatus,
            ApplicationPlatformLifecyclePumpStatus lifecyclePumpStatus,
            ApplicationLifecycleCheckpointOperationStatus lifecycleCheckpointStatus,
            int occupiedRetirementCount, int failedRetirementCount,
            string ownershipDebtDomainId, string ownershipDebtStableCode,
            string creationStableErrorCode = null)
        {
            RootState = rootState; RootError = rootError; Attempt = attempt; PlanId = planId;
            RunId = runId ?? string.Empty; StartupState = startupState; Phase = phase; StepId = stepId;
            ForwardProgress = forwardProgress; RollbackProgress = rollbackProgress;
            CancelRequested = cancelRequested; ApplicationErrorCode = applicationErrorCode;
            RuntimeErrorCode = runtimeErrorCode ?? string.Empty; LiveRevision = liveRevision;
            HasRuntimeSession = hasRuntimeSession; LiveFrameStatus = liveFrameStatus;
            LifecyclePumpStatus = lifecyclePumpStatus; LifecycleCheckpointStatus = lifecycleCheckpointStatus;
            OccupiedRetirementCount = occupiedRetirementCount; FailedRetirementCount = failedRetirementCount;
            OwnershipDebtDomainId = ownershipDebtDomainId ?? string.Empty;
            OwnershipDebtStableCode = ownershipDebtStableCode ?? string.Empty;
            CreationStableErrorCode = creationStableErrorCode ?? string.Empty;
        }

        public ApplicationUnityBootRootState RootState { get; }
        public ApplicationUnityBootErrorCode RootError { get; }
        public long Attempt { get; }
        public ApplicationStartupPlanId PlanId { get; }
        public string RunId { get; }
        public ApplicationStartupState StartupState { get; }
        public ApplicationStartupPhase Phase { get; }
        public ApplicationStartupStepId StepId { get; }
        public float ForwardProgress { get; }
        public float RollbackProgress { get; }
        public bool CancelRequested { get; }
        public ApplicationStartupErrorCode ApplicationErrorCode { get; }
        public string RuntimeErrorCode { get; }
        public long LiveRevision { get; }
        public bool HasRuntimeSession { get; }
        public ApplicationLiveFrameTickStatus LiveFrameStatus { get; }
        public ApplicationPlatformLifecyclePumpStatus LifecyclePumpStatus { get; }
        public ApplicationLifecycleCheckpointOperationStatus LifecycleCheckpointStatus { get; }
        public int OccupiedRetirementCount { get; }
        public int FailedRetirementCount { get; }
        public string OwnershipDebtDomainId { get; }
        public string OwnershipDebtStableCode { get; }
        /// <summary>Stable detail returned by the most recent failed composition/run creation. NoAlloc.</summary>
        public string CreationStableErrorCode { get; }
        public bool CanStart => ApplicationUnityStartupRequestPolicy.CanStart(RootState, StartupState, Attempt);
        public bool CanRetry => ApplicationUnityStartupRequestPolicy.CanRetry(RootState, RootError, StartupState);
        public bool CanCancel => ApplicationUnityStartupRequestPolicy.CanCancel(StartupState, CancelRequested);
    }
}
