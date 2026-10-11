using System;
using System.Threading;
using MxFramework.Resources;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    public enum ApplicationControllerPumpStrategy
    {
        Invalid = 0,
        RuntimeModule = 1,
        Standalone = 2
    }

    public enum ApplicationCandidatePumpOwnership
    {
        Invalid = 0,
        StartupOwner = 1,
        HandoffFrozen = 2,
        RollbackOwner = 3,
        Disposed = 4,
        HandoffClaimed = 5,
        LiveOwner = 6,
        RetirementOwner = 7
    }

    public enum ApplicationCandidateSessionErrorCode
    {
        None = 0,
        InvalidGraph = 1,
        ForeignTransaction = 2,
        GraphAlreadyClaimed = 3,
        InvalidPumpStrategy = 100,
        PumpOwnershipConflict = 101,
        StartupPumpFrozen = 102,
        RuntimeInitializeFailed = 200,
        RuntimeStartFailed = 201,
        RuntimeNotReady = 202,
        RuntimeTickObserved = 203,
        RuntimeCleanupFailed = 204,
        AppFlowCompensationMissing = 300,
        AppFlowPrepareFailed = 301,
        AppFlowCompensationFailed = 302,
        AppFlowFreezeFailed = 303,
        SceneRequestUnsupported = 350,
        SceneIsolationFailed = 351,
        SceneLoadFailed = 352,
        SceneUnloadFailed = 353,
        SceneAttachmentFailed = 354,
        ResourcePreparationFailed = 400,
        ResourceAcquireFailed = 401,
        ResourceCleanupFailed = 402,
        ResourceGraphCleanupFailed = 403,
        HandoffPreflightFailed = 500,
        SessionCleanupFailed = 600
    }

    public enum ApplicationCandidateSessionPhase
    {
        None = 0,
        GraphClaim = 1,
        ResourcePrepare = 2,
        RuntimeLifecycle = 3,
        AppFlowPrepare = 4,
        HandoffFreeze = 5,
        Rollback = 6,
        ResourceGraphCleanup = 7,
        SessionCleanup = 8,
        ScenePrepare = 9,
        SceneCleanup = 10
    }

    [Flags]
    public enum ApplicationCandidateCleanupDebt
    {
        None = 0,
        RuntimeHost = 1,
        AppFlow = 2,
        ResourcePlan = 4,
        ResourceGraph = 8,
        Scene = 16,
        PumpOwner = 32
    }

    public enum ApplicationCandidateAppFlowLeaseStatus
    {
        Invalid = 0,
        Created = 1,
        Preparing = 2,
        Prepared = 3,
        RollingBack = 4,
        RolledBack = 5,
        Debt = 6,
        Disposed = 7
    }

    [Flags]
    public enum ApplicationCandidateComponentReuse
    {
        None = 0,
        RuntimeHost = 1,
        AppFlowLifecycle = 2,
        AppFlowController = 4,
        AppFlowRuntimeModule = 8,
        ResourceManager = 16,
        ResourceGraph = 32,
        ResourceOrchestrator = 64,
        ResourceConfiguration = 128,
        ResourcePlan = 256,
        ResourceLifetime = 512,
        SceneGraph = 1024,
        SceneController = 2048,
        SceneRuntimeModule = 4096,
        SceneIsolationGate = 8192,
        SceneDriver = 16384
    }

    /// <summary>Opaque identity for exactly one candidate session graph.</summary>
    /// <remarks>Construction is LowFreqAlloc; identity reads are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSessionToken
    {
        internal ApplicationCandidateSessionToken(long ordinal)
        {
            Ordinal = ordinal;
        }

        internal long Ordinal { get; }
    }

    /// <summary>
    /// Opaque builder-issued identity for the RuntimeHost and participant catalog of one candidate
    /// graph generation. It contains no Runtime or restore authority.
    /// </summary>
    /// <remarks>Construction is SessionBridge-owned LowFreqAlloc; reference reads are NoAlloc.</remarks>
    public sealed class ApplicationCandidateRuntimeCompositionIdentity
    {
        internal ApplicationCandidateRuntimeCompositionIdentity() { }
    }

    /// <summary>Structured bridge detail. Construction is LowFreqAlloc; getters are NoAlloc.</summary>
    public sealed class ApplicationCandidateSessionErrorDetail : IApplicationStartupInnerErrorDetail
    {
        private readonly string _stableCode;
        public const string ErrorDomainId = "mx.application.session-bridge";

        internal ApplicationCandidateSessionErrorDetail(
            ApplicationCandidateSessionErrorCode code,
            ApplicationCandidateSessionPhase phase,
            ApplicationCandidateSessionErrorCode cleanupCode = ApplicationCandidateSessionErrorCode.None,
            ApplicationCandidateCleanupDebt cleanupDebts = ApplicationCandidateCleanupDebt.None,
            RuntimeOperationError operationError = default,
            IApplicationStartupInnerErrorDetail innerErrorDetail = null,
            ResourcePlanPreparationError resourceError = default,
            Exception exception = null,
            RuntimeOperationError cleanupOperationError = default,
            IApplicationStartupInnerErrorDetail cleanupInnerErrorDetail = null,
            Exception cleanupException = null,
            ApplicationCandidateSessionErrorCode forwardCode = ApplicationCandidateSessionErrorCode.None)
        {
            Code = code;
            Phase = phase;
            CleanupCode = cleanupCode;
            CleanupDebts = cleanupDebts != ApplicationCandidateCleanupDebt.None
                ? cleanupDebts
                : GetDebt(cleanupCode);
            OperationError = operationError;
            InnerErrorDetail = innerErrorDetail;
            ResourceError = resourceError;
            Exception = exception;
            CleanupOperationError = cleanupOperationError;
            CleanupInnerErrorDetail = cleanupInnerErrorDetail;
            CleanupException = cleanupException;
            ForwardCode = forwardCode != ApplicationCandidateSessionErrorCode.None
                ? forwardCode
                : code == ApplicationCandidateSessionErrorCode.SessionCleanupFailed
                    ? ApplicationCandidateSessionErrorCode.None
                    : code;
            _stableCode = GetStableCode(code);
        }

        public string DomainId => ErrorDomainId;
        public string StableCode => _stableCode;
        public ApplicationCandidateSessionErrorCode Code { get; }
        public ApplicationCandidateSessionPhase Phase { get; }
        public ApplicationCandidateSessionErrorCode CleanupCode { get; }
        public ApplicationCandidateCleanupDebt CleanupDebts { get; }
        public bool HasCleanupDebt => CleanupDebts != ApplicationCandidateCleanupDebt.None;
        public RuntimeOperationError OperationError { get; }
        public IApplicationStartupInnerErrorDetail InnerErrorDetail { get; }
        public ResourcePlanPreparationError ResourceError { get; }
        public Exception Exception { get; }
        public RuntimeOperationError CleanupOperationError { get; }
        public IApplicationStartupInnerErrorDetail CleanupInnerErrorDetail { get; }
        public Exception CleanupException { get; }
        public ApplicationCandidateSessionErrorCode ForwardCode { get; }

        private static ApplicationCandidateCleanupDebt GetDebt(ApplicationCandidateSessionErrorCode cleanupCode)
        {
            switch (cleanupCode)
            {
                case ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed:
                    return ApplicationCandidateCleanupDebt.RuntimeHost;
                case ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed:
                    return ApplicationCandidateCleanupDebt.AppFlow;
                case ApplicationCandidateSessionErrorCode.ResourceCleanupFailed:
                    return ApplicationCandidateCleanupDebt.ResourcePlan;
                case ApplicationCandidateSessionErrorCode.ResourceGraphCleanupFailed:
                    return ApplicationCandidateCleanupDebt.ResourceGraph;
                default:
                    return ApplicationCandidateCleanupDebt.None;
            }
        }

        internal static string GetStableCode(ApplicationCandidateSessionErrorCode code)
        {
            switch (code)
            {
                case ApplicationCandidateSessionErrorCode.InvalidGraph: return "INVALID_GRAPH";
                case ApplicationCandidateSessionErrorCode.ForeignTransaction: return "FOREIGN_TRANSACTION";
                case ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed: return "GRAPH_ALREADY_CLAIMED";
                case ApplicationCandidateSessionErrorCode.InvalidPumpStrategy: return "INVALID_PUMP_STRATEGY";
                case ApplicationCandidateSessionErrorCode.PumpOwnershipConflict: return "PUMP_OWNERSHIP_CONFLICT";
                case ApplicationCandidateSessionErrorCode.StartupPumpFrozen: return "STARTUP_PUMP_FROZEN";
                case ApplicationCandidateSessionErrorCode.RuntimeInitializeFailed: return "RUNTIME_INITIALIZE_FAILED";
                case ApplicationCandidateSessionErrorCode.RuntimeStartFailed: return "RUNTIME_START_FAILED";
                case ApplicationCandidateSessionErrorCode.RuntimeNotReady: return "RUNTIME_NOT_READY";
                case ApplicationCandidateSessionErrorCode.RuntimeTickObserved: return "RUNTIME_TICK_OBSERVED";
                case ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed: return "RUNTIME_CLEANUP_FAILED";
                case ApplicationCandidateSessionErrorCode.AppFlowCompensationMissing: return "APP_FLOW_COMPENSATION_MISSING";
                case ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed: return "APP_FLOW_PREPARE_FAILED";
                case ApplicationCandidateSessionErrorCode.AppFlowCompensationFailed: return "APP_FLOW_COMPENSATION_FAILED";
                case ApplicationCandidateSessionErrorCode.AppFlowFreezeFailed: return "APP_FLOW_FREEZE_FAILED";
                case ApplicationCandidateSessionErrorCode.SceneRequestUnsupported: return "SCENE_REQUEST_UNSUPPORTED";
                case ApplicationCandidateSessionErrorCode.SceneIsolationFailed: return "SCENE_ISOLATION_FAILED";
                case ApplicationCandidateSessionErrorCode.SceneLoadFailed: return "SCENE_LOAD_FAILED";
                case ApplicationCandidateSessionErrorCode.SceneUnloadFailed: return "SCENE_UNLOAD_FAILED";
                case ApplicationCandidateSessionErrorCode.SceneAttachmentFailed: return "SCENE_ATTACHMENT_FAILED";
                case ApplicationCandidateSessionErrorCode.ResourcePreparationFailed: return "RESOURCE_PREPARATION_FAILED";
                case ApplicationCandidateSessionErrorCode.ResourceAcquireFailed: return "RESOURCE_ACQUIRE_FAILED";
                case ApplicationCandidateSessionErrorCode.ResourceCleanupFailed: return "RESOURCE_CLEANUP_FAILED";
                case ApplicationCandidateSessionErrorCode.ResourceGraphCleanupFailed: return "RESOURCE_GRAPH_CLEANUP_FAILED";
                case ApplicationCandidateSessionErrorCode.HandoffPreflightFailed: return "HANDOFF_PREFLIGHT_FAILED";
                case ApplicationCandidateSessionErrorCode.SessionCleanupFailed: return "SESSION_CLEANUP_FAILED";
                default: return "NONE";
            }
        }
    }

    /// <summary>Run-local transaction capability used to atomically claim and locate one candidate graph.</summary>
    /// <remarks>
    /// NoAlloc query. It intentionally exposes no attach, detach, commit or publication capability.
    /// Factory claim occurs before builder invocation; graph attachment is factory-owned.
    /// </remarks>
    public interface IApplicationCandidateSessionTransaction : IApplicationStartupTransaction
    {
        /// <summary>Returns this transaction's exact-once candidate graph claim.</summary>
        /// <remarks>NoAlloc. The same reference must be returned for the transaction's entire lifetime.</remarks>
        ApplicationCandidateSessionOwnerClaim CandidateSessionClaim { get; }
    }

    /// <summary>Stable exact-once claim identity for one candidate transaction.</summary>
    /// <remarks>Construction is LowFreqAlloc; IsClaimed and CandidateGraph reads are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSessionOwnerClaim
    {
        private IApplicationCandidateSessionTransaction _owner;
        private ApplicationCandidateSessionGraph _graph;

        /// <summary>Creates one claim that must remain owned by one transaction.</summary>
        /// <remarks>LowFreqAlloc.</remarks>
        public ApplicationCandidateSessionOwnerClaim() { }

        /// <remarks>NoAlloc.</remarks>
        public bool IsClaimed => Volatile.Read(ref _owner) != null;
        /// <remarks>NoAlloc.</remarks>
        public ApplicationCandidateSessionGraph CandidateGraph => Volatile.Read(ref _graph);

        internal bool TryClaim(IApplicationCandidateSessionTransaction owner) =>
            owner != null && Interlocked.CompareExchange(ref _owner, owner, null) == null;

        internal bool IsOwnedBy(IApplicationCandidateSessionTransaction owner) =>
            owner != null && ReferenceEquals(Volatile.Read(ref _owner), owner);

        internal void Attach(IApplicationCandidateSessionTransaction owner, ApplicationCandidateSessionGraph graph)
        {
            if (!IsOwnedBy(owner) || graph == null ||
                Interlocked.CompareExchange(ref _graph, graph, null) != null)
                throw new InvalidOperationException("Candidate graph claim attachment is invalid or already consumed.");
        }
    }

    /// <summary>Structured low-frequency candidate construction failure.</summary>
    /// <remarks>Construction is LowFreqAlloc; Code is NoAlloc.</remarks>
    public sealed class ApplicationCandidateSessionConstructionException : InvalidOperationException
    {
        internal ApplicationCandidateSessionConstructionException(
            ApplicationCandidateSessionErrorCode code,
            string message,
            Exception innerException = null)
            : base(message, innerException) => Code = code;

        /// <remarks>NoAlloc.</remarks>
        public ApplicationCandidateSessionErrorCode Code { get; }
    }

    public interface IApplicationCandidateSessionFactory
    {
        /// <summary>Creates a fresh, unstarted candidate graph.</summary>
        /// <remarks>AllocByDesign. Implementations must not reuse any component from an earlier graph.</remarks>
        ApplicationCandidateSessionGraph Create(IApplicationCandidateSessionTransaction owner);
    }

    /// <summary>Compensatable AppFlow lifecycle owned by one candidate graph.</summary>
    /// <remarks>Construction transfers exclusive use to one serialized startup owner; callers must not invoke it concurrently after transfer.</remarks>
    public interface IApplicationCandidateAppFlowLifecycle : IDisposable
    {
        AppFlowController Controller { get; }
        AppFlowRuntimeModule RuntimeModule { get; }
        ApplicationControllerPumpStrategy PumpStrategy { get; }
        bool CanCompensate { get; }

        /// <summary>Starts candidate-owned AppFlow preparation.</summary>
        /// <remarks>LowFreqAlloc. Must not begin Enter when CanCompensate is false.</remarks>
        IApplicationStartupStepOperation PrepareStart();

        /// <summary>Freezes the prepared controller without publishing or transferring live ownership.</summary>
        /// <remarks>NoAlloc on success. Idempotent only for the same transferred candidate generation.</remarks>
        void FreezeForHandoff();

        /// <summary>Starts asynchronous compensation for all preparation side effects.</summary>
        /// <remarks>LowFreqAlloc. Must be idempotent for the same transferred candidate generation.</remarks>
        IApplicationStartupStepOperation BeginRollback();
    }

    /// <summary>Final owner of candidate ResourceManager/provider graph lifetime.</summary>
    public interface IApplicationCandidateResourceGraphLifetime
    {
        bool IsReleased { get; }

        /// <summary>Releases only already-owned graph state and starts no new resource or I/O operation.</summary>
        /// <remarks>LowFreqAlloc only for implementation-owned failure diagnostics.</remarks>
        ApplicationCandidateResourceGraphCleanupResult FinalizeRollback();
    }

    public readonly struct ApplicationCandidateResourceGraphCleanupResult
    {
        private ApplicationCandidateResourceGraphCleanupResult(bool success, string errorCode)
        {
            Success = success;
            ErrorCode = errorCode ?? string.Empty;
        }

        public bool Success { get; }
        public string ErrorCode { get; }
        public static ApplicationCandidateResourceGraphCleanupResult Succeeded => new ApplicationCandidateResourceGraphCleanupResult(true, string.Empty);
        public static ApplicationCandidateResourceGraphCleanupResult Failed(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
                throw new ArgumentException("A failed cleanup result requires an error code.", nameof(errorCode));
            return new ApplicationCandidateResourceGraphCleanupResult(false, errorCode);
        }
    }

    public readonly struct ApplicationCandidateSessionCleanupResult
    {
        private ApplicationCandidateSessionCleanupResult(
            ApplicationCandidateCleanupDebt debts,
            string firstErrorCode)
        {
            Debts = debts;
            FirstErrorCode = firstErrorCode ?? string.Empty;
        }

        public bool Success => Debts == ApplicationCandidateCleanupDebt.None;
        public ApplicationCandidateCleanupDebt Debts { get; }
        public string FirstErrorCode { get; }
        public static ApplicationCandidateSessionCleanupResult Succeeded =>
            new ApplicationCandidateSessionCleanupResult(ApplicationCandidateCleanupDebt.None, string.Empty);
        public static ApplicationCandidateSessionCleanupResult Failed(
            ApplicationCandidateCleanupDebt debts,
            string firstErrorCode)
        {
            if (debts == ApplicationCandidateCleanupDebt.None)
                throw new ArgumentException("A failed cleanup result requires cleanup debt.", nameof(debts));
            if (string.IsNullOrWhiteSpace(firstErrorCode))
                throw new ArgumentException("A failed cleanup result requires an error code.", nameof(firstErrorCode));
            return new ApplicationCandidateSessionCleanupResult(debts, firstErrorCode);
        }
    }

    /// <summary>Construction-only candidate resource graph descriptor.</summary>
    /// <remarks>Construction is LowFreqAlloc; getters are NoAlloc.</remarks>
    public sealed class ApplicationCandidateResourceGraph
    {
        public ApplicationCandidateResourceGraph(
            IResourceManager manager,
            ResourceRuntimeConfigurationIdentity configurationIdentity,
            ResourcePlanOrchestrator orchestrator,
            ResourcePlan plan,
            IApplicationCandidateResourceGraphLifetime lifetime)
        {
            Manager = manager ?? throw new ArgumentNullException(nameof(manager));
            ConfigurationIdentity = configurationIdentity ?? throw new ArgumentNullException(nameof(configurationIdentity));
            Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        }

        public IResourceManager Manager { get; }
        public ResourceRuntimeConfigurationIdentity ConfigurationIdentity { get; }
        public ResourcePlanOrchestrator Orchestrator { get; }
        public ResourcePlan Plan { get; }
        public IApplicationCandidateResourceGraphLifetime Lifetime { get; }
    }

    /// <summary>Fresh component set returned by a candidate graph builder.</summary>
    /// <remarks>AllocByDesign. Components must be inert until their forward operation is ticked.</remarks>
    public sealed class ApplicationCandidateSessionComponents
    {
        public ApplicationCandidateSessionComponents(
            RuntimeHost runtimeHost,
            IApplicationCandidateAppFlowLifecycle appFlowLifecycle,
            ApplicationCandidateResourceGraph resourceGraph)
            : this(runtimeHost, appFlowLifecycle, resourceGraph, null)
        {
        }

        public ApplicationCandidateSessionComponents(
            RuntimeHost runtimeHost,
            IApplicationCandidateAppFlowLifecycle appFlowLifecycle,
            ApplicationCandidateResourceGraph resourceGraph,
            ApplicationCandidateSceneGraph sceneGraph)
        {
            RuntimeHost = runtimeHost ?? throw new ArgumentNullException(nameof(runtimeHost));
            AppFlowLifecycle = appFlowLifecycle ?? throw new ArgumentNullException(nameof(appFlowLifecycle));
            ResourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
            SceneGraph = sceneGraph;
        }

        public RuntimeHost RuntimeHost { get; }
        public IApplicationCandidateAppFlowLifecycle AppFlowLifecycle { get; }
        public ApplicationCandidateResourceGraph ResourceGraph { get; }
        public ApplicationCandidateSceneGraph SceneGraph { get; }
    }

    public interface IApplicationCandidateSessionGraphBuilder
    {
        /// <summary>Builds inert components for exactly one supplied token.</summary>
        /// <remarks>AllocByDesign and strong exception guarantee: a throw must leave no owned side effect.</remarks>
        IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token);
    }

    /// <summary>Strong-exception build lease whose components remain inert until transferred.</summary>
    public interface IApplicationCandidateSessionBuildLease
    {
        /// <remarks>NoAlloc.</remarks>
        ApplicationCandidateSessionToken Token { get; }
        /// <remarks>NoAlloc.</remarks>
        ApplicationCandidateSessionComponents Components { get; }
        /// <summary>Transfers all still-owned components exactly once.</summary>
        /// <remarks>NoAlloc.</remarks>
        bool TryTransfer();

        /// <summary>Releases every still-owned component except identities in <paramref name="reusedComponents"/>.</summary>
        /// <remarks>LowFreqAlloc only for implementation-owned cleanup diagnostics. No new operation or I/O. The mask protects owners transferred by an earlier generation.</remarks>
        void Abandon(ApplicationCandidateComponentReuse reusedComponents);
    }

    /// <summary>Read-only proof that one candidate graph crossed the checked freeze boundary.</summary>
    /// <remarks>Getters are NoAlloc. Live pump and publication capabilities are intentionally absent.</remarks>
    public sealed class ApplicationCandidateFrozenHandoffLease
    {
        private ApplicationCandidateFrozenHandoffOwner _owner;

        internal ApplicationCandidateFrozenHandoffLease(ApplicationCandidateSessionGraph graph)
        {
            Graph = graph;
        }

        internal ApplicationCandidateSessionGraph Graph { get; }
        public bool IsValid => Graph != null && Graph.PumpOwnership == ApplicationCandidatePumpOwnership.HandoffFrozen;
        public bool IsClaimed => Volatile.Read(ref _owner) != null;

        internal bool TryClaim(ApplicationCandidateFrozenHandoffOwner owner) =>
            TryClaim(owner, null);

        internal bool TryClaim(
            ApplicationCandidateFrozenHandoffOwner owner,
            Action afterGraphClaimedBeforeSceneClaim)
        {
            if (owner == null || Volatile.Read(ref _owner) != null ||
                !Graph.TryClaimFrozenHandoff(this, owner, afterGraphClaimedBeforeSceneClaim))
                return false;
            Volatile.Write(ref _owner, owner);
            return true;
        }

        internal bool TryPrepareClaim(ApplicationCandidateFrozenHandoffOwner owner) =>
            owner != null && Volatile.Read(ref _owner) == null &&
            Graph.TryPrepareFrozenHandoff(this, owner);

        internal void CancelPrepared(ApplicationCandidateFrozenHandoffOwner owner) =>
            Graph.CancelPreparedFrozenHandoff(this, owner);

        internal bool TryClaimPrepared(ApplicationCandidateFrozenHandoffOwner owner)
        {
            if (!Graph.TryClaimPreparedFrozenHandoff(this, owner))
                return false;
            Volatile.Write(ref _owner, owner);
            return true;
        }
    }

    internal sealed class ApplicationCandidateFrozenHandoffOwner { }
}
