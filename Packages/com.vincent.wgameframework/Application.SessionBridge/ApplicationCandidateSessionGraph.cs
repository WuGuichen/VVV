using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.Runtime;

namespace MxFramework.ApplicationStartup.SessionBridge
{
    internal interface IApplicationCandidatePumpCapability
    {
        bool CanFreeze { get; }
        void Freeze();
    }

    /// <summary>One isolated, owner-bound candidate generation.</summary>
    /// <remarks>Construction is AllocByDesign; public state reads are NoAlloc.</remarks>
    public sealed class ApplicationCandidateSessionGraph
    {
        private readonly IApplicationStartupTransaction _transactionOwner;
        private readonly List<IApplicationCandidatePumpCapability> _pumpCapabilities;
        private int _pumpOwnership;
        private int _finalized;
        private int _finalizationAttempted;
        private ApplicationCandidateSessionCleanupResult _finalizationResult;
        private bool _resourceReady;
        private bool _runtimeReady;
        private bool _appFlowReady;
        private bool _sceneReady;
        private bool _runtimeCleanupDebt;
        private readonly ApplicationCandidateFrozenHandoffLease _frozenLease;
        private readonly object _handoffGate = new object();
        private ApplicationCandidateFrozenHandoffOwner _preparedHandoffOwner;
        private Exception _cleanupException;
        private long _liveStandalonePumpSequence;
        private long _liveStandaloneAppFlowTickCount;
        private long _liveStandaloneSceneTickCount;
        private long _lastLiveStandaloneAppFlowSequence;
        private long _lastLiveStandaloneSceneSequence;

        internal ApplicationCandidateSessionGraph(
            IApplicationStartupTransaction transactionOwner,
            ApplicationCandidateSessionToken token,
            ApplicationCandidateSessionComponents components)
        {
            _transactionOwner = transactionOwner ?? throw new ArgumentNullException(nameof(transactionOwner));
            Token = token ?? throw new ArgumentNullException(nameof(token));
            if (components == null) throw new ArgumentNullException(nameof(components));

            RuntimeHost = components.RuntimeHost;
            RuntimeCompositionIdentity = new ApplicationCandidateRuntimeCompositionIdentity();
            AppFlowLease = new ApplicationCandidateAppFlowLease(components.AppFlowLifecycle);
            if (RuntimeHost.State != RuntimeLifecycleState.Created || RuntimeHost.Errors.Count != 0 ||
                RuntimeHost.HasTickAttempt || RuntimeHost.TickCount != 0)
                throw new InvalidOperationException("Candidate RuntimeHost must be fresh and unstarted.");
            if (AppFlowLease.Controller == null || AppFlowLease.Controller.IsStarted || AppFlowLease.Controller.TickCount != 0)
                throw new InvalidOperationException("Candidate AppFlow controller must be fresh and unstarted.");
            if (components.ResourceGraph.Lifetime.IsReleased)
                throw new InvalidOperationException("Candidate resource graph lifetime is already released.");

            ValidateAppFlowPumpRegistration(RuntimeHost, AppFlowLease.Lifecycle);
            ResourceLease = new ApplicationCandidateResourceLease(Token, components.ResourceGraph);
            if (components.SceneGraph != null)
            {
                ValidateSceneOwnership(components.SceneGraph);
                ValidateScenePumpRegistration(RuntimeHost, components.SceneGraph);
                if (components.SceneGraph.Controller.IsBusy ||
                    !string.IsNullOrEmpty(components.SceneGraph.Controller.ActiveSceneKey) ||
                    components.SceneGraph.Controller.LastResult.Status != SceneFlowResultStatus.None)
                    throw new InvalidOperationException("Candidate SceneFlow controller must be fresh and idle.");
                if (components.SceneGraph.IsolationGate.IsIsolated || !components.SceneGraph.IsolationGate.IsInert)
                    throw new InvalidOperationException("Candidate scene isolation gate must begin inert and unclaimed.");
                SceneLease = new ApplicationCandidateSceneLease(components.SceneGraph);
            }
            _pumpCapabilities = new List<IApplicationCandidatePumpCapability>(2)
            {
                new AppFlowPumpCapability(AppFlowLease)
            };
            if (SceneLease != null) _pumpCapabilities.Add(new ScenePumpCapability(SceneLease));
            _pumpOwnership = (int)ApplicationCandidatePumpOwnership.StartupOwner;
            _frozenLease = new ApplicationCandidateFrozenHandoffLease(this);
        }

        internal ApplicationCandidateSessionToken Token { get; }
        internal RuntimeHost RuntimeHost { get; }
        internal ApplicationCandidateRuntimeCompositionIdentity RuntimeCompositionIdentity { get; }
        internal ApplicationCandidateAppFlowLease AppFlowLease { get; }
        internal ApplicationCandidateResourceLease ResourceLease { get; }
        internal ApplicationCandidateSceneLease SceneLease { get; }
        public ApplicationCandidatePumpOwnership PumpOwnership =>
            (ApplicationCandidatePumpOwnership)Volatile.Read(ref _pumpOwnership);
        public bool IsResourceReady => _resourceReady;
        public bool IsRuntimeReady => _runtimeReady;
        public bool IsAppFlowReady => _appFlowReady;
        public bool IsSceneReady => SceneLease == null || _sceneReady;
        public bool IsReadyForHandoff => _resourceReady && _runtimeReady && _appFlowReady && IsSceneReady;
        public ApplicationCandidateAppFlowLeaseStatus AppFlowStatus => AppFlowLease.Status;
        public ApplicationCandidateResourceLeaseStatus ResourceStatus => ResourceLease.Status;
        public float ResourceProgress => ResourceLease.Progress;
        public ApplicationCandidateSessionErrorCode ResourceErrorCode => ResourceLease.ErrorCode;
        public ApplicationCandidateSceneLeaseStatus SceneStatus =>
            SceneLease != null ? SceneLease.Status : ApplicationCandidateSceneLeaseStatus.Invalid;
        public float SceneProgress => SceneLease != null ? SceneLease.Progress : 0f;
        public ApplicationCandidatePumpOwnership ScenePumpOwnership =>
            SceneLease != null ? SceneLease.PumpOwnership : ApplicationCandidatePumpOwnership.Invalid;
        internal long LiveStandaloneAppFlowTickCount => _liveStandaloneAppFlowTickCount;
        internal long LiveStandaloneSceneTickCount => _liveStandaloneSceneTickCount;
        internal long LastLiveStandaloneAppFlowSequence => _lastLiveStandaloneAppFlowSequence;
        internal long LastLiveStandaloneSceneSequence => _lastLiveStandaloneSceneSequence;
        internal Exception CleanupException
        {
            get
            {
                Exception result = _cleanupException;
                result = Combine(result, SceneLease?.CleanupException);
                return Combine(result, ResourceLease.CleanupException);
            }
        }

        /// <summary>Returns a read-only proof only after checked freeze succeeded.</summary>
        /// <remarks>NoAlloc. It exposes no live pump or publication capability.</remarks>
        public bool TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease lease)
        {
            if (PumpOwnership != ApplicationCandidatePumpOwnership.HandoffFrozen)
            {
                lease = null;
                return false;
            }
            lease = _frozenLease;
            return true;
        }

        internal ApplicationCandidateSessionErrorCode ValidateTransaction(IApplicationStartupTransaction transaction) =>
            transaction != null && ReferenceEquals(_transactionOwner, transaction)
                ? ApplicationCandidateSessionErrorCode.None
                : ApplicationCandidateSessionErrorCode.ForeignTransaction;

        internal bool CanStartupPump => PumpOwnership == ApplicationCandidatePumpOwnership.StartupOwner;
        internal bool CanRollbackPump => PumpOwnership == ApplicationCandidatePumpOwnership.RollbackOwner;
        internal void MarkResourceReady() => _resourceReady = true;
        internal void MarkRuntimeReady() => _runtimeReady = true;
        internal void MarkAppFlowReady() => _appFlowReady = true;
        internal void MarkSceneReady() => _sceneReady = true;

        internal void RegisterPumpCapability(IApplicationCandidatePumpCapability capability)
        {
            if (capability == null) throw new ArgumentNullException(nameof(capability));
            if (PumpOwnership != ApplicationCandidatePumpOwnership.StartupOwner)
                throw new InvalidOperationException("Pump capabilities must be registered before startup.");
            _pumpCapabilities.Add(capability);
        }

        internal ApplicationCandidateSessionErrorCode FreezeForHandoff()
        {
            ApplicationCandidatePumpOwnership ownership = PumpOwnership;
            if (ownership == ApplicationCandidatePumpOwnership.HandoffFrozen)
                return ApplicationCandidateSessionErrorCode.None;
            if (ownership != ApplicationCandidatePumpOwnership.StartupOwner)
                return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
            if (!IsReadyForHandoff || !IsRuntimeUntickedAndReady() || !IsAppFlowUntickedAndReady())
                return ApplicationCandidateSessionErrorCode.HandoffPreflightFailed;
            for (int i = 0; i < _pumpCapabilities.Count; i++)
                if (!_pumpCapabilities[i].CanFreeze)
                    return ApplicationCandidateSessionErrorCode.HandoffPreflightFailed;

            if (Interlocked.CompareExchange(ref _pumpOwnership,
                    (int)ApplicationCandidatePumpOwnership.HandoffFrozen,
                    (int)ApplicationCandidatePumpOwnership.StartupOwner) !=
                (int)ApplicationCandidatePumpOwnership.StartupOwner)
                return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
            try
            {
                for (int i = 0; i < _pumpCapabilities.Count; i++) _pumpCapabilities[i].Freeze();
            }
            catch
            {
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.RollbackOwner);
                return ApplicationCandidateSessionErrorCode.AppFlowFreezeFailed;
            }
            return ApplicationCandidateSessionErrorCode.None;
        }

        internal ApplicationCandidateSessionErrorCode BeginRollbackOwnership()
        {
            lock (_handoffGate)
            {
                if (_preparedHandoffOwner != null)
                    return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
                while (true)
                {
                    ApplicationCandidatePumpOwnership ownership = PumpOwnership;
                    if (ownership == ApplicationCandidatePumpOwnership.RollbackOwner)
                    {
                        AppFlowLease.MarkRolledBackIfNeverPrepared();
                        return ApplicationCandidateSessionErrorCode.None;
                    }
                    if (ownership == ApplicationCandidatePumpOwnership.Disposed || ownership == ApplicationCandidatePumpOwnership.Invalid)
                        return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
                    if (ownership == ApplicationCandidatePumpOwnership.HandoffClaimed ||
                        ownership == ApplicationCandidatePumpOwnership.LiveOwner ||
                        ownership == ApplicationCandidatePumpOwnership.RetirementOwner)
                        return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
                    if (Interlocked.CompareExchange(ref _pumpOwnership,
                            (int)ApplicationCandidatePumpOwnership.RollbackOwner,
                            (int)ownership) == (int)ownership)
                    {
                        AppFlowLease.MarkRolledBackIfNeverPrepared();
                        return ApplicationCandidateSessionErrorCode.None;
                    }
                }
            }
        }

        internal ApplicationCandidateSessionErrorCode ConfirmLiveRetirementOwnership()
        {
            lock (_handoffGate)
            {
                return _preparedHandoffOwner == null &&
                       PumpOwnership == ApplicationCandidatePumpOwnership.RetirementOwner
                    ? ApplicationCandidateSessionErrorCode.None
                    : ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
            }
        }

        internal ApplicationCandidateSessionErrorCode BeginLiveRetirementOwnership()
        {
            lock (_handoffGate)
            {
                if (_preparedHandoffOwner != null)
                    return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
                ApplicationCandidatePumpOwnership ownership = PumpOwnership;
                if (ownership == ApplicationCandidatePumpOwnership.RetirementOwner)
                    return ApplicationCandidateSessionErrorCode.None;
                if (ownership != ApplicationCandidatePumpOwnership.LiveOwner)
                    return ApplicationCandidateSessionErrorCode.PumpOwnershipConflict;
                SceneLease?.BeginLiveRetirementOwnership();
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.RetirementOwner);
                return ApplicationCandidateSessionErrorCode.None;
            }
        }

        internal void PromoteClaimedHandoffToLiveOwner()
        {
            lock (_handoffGate)
            {
                SceneLease?.PromoteClaimedHandoffToLiveOwner();
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.LiveOwner);
            }
        }

        internal bool DisposeRuntimeHost()
        {
            int errorCount = RuntimeHost.Errors.Count;
            bool threw = false;
            try { RuntimeHost.Dispose(); }
            catch (Exception exception) { threw = true; AppendCleanupException(exception); }
            bool cleanupError = threw || RuntimeHost.State != RuntimeLifecycleState.Disposed;
            for (int i = errorCount; i < RuntimeHost.Errors.Count; i++)
            {
                RuntimeHostOperation operation = RuntimeHost.Errors[i].Operation;
                if (operation == RuntimeHostOperation.Stop || operation == RuntimeHostOperation.Dispose)
                    cleanupError = true;
            }
            if (cleanupError) _runtimeCleanupDebt = true;
            return !_runtimeCleanupDebt;
        }

        internal bool TryPrepareFrozenHandoff(
            ApplicationCandidateFrozenHandoffLease lease,
            ApplicationCandidateFrozenHandoffOwner owner)
        {
            lock (_handoffGate)
            {
                if (!ReferenceEquals(lease, _frozenLease) || owner == null ||
                    PumpOwnership != ApplicationCandidatePumpOwnership.HandoffFrozen ||
                    (SceneLease != null && SceneLease.Status != ApplicationCandidateSceneLeaseStatus.HandoffFrozen))
                    return false;
                if (_preparedHandoffOwner == null)
                    _preparedHandoffOwner = owner;
                return ReferenceEquals(_preparedHandoffOwner, owner);
            }
        }

        internal bool TryClaimFrozenHandoff(
            ApplicationCandidateFrozenHandoffLease lease,
            ApplicationCandidateFrozenHandoffOwner owner,
            Action afterGraphClaimedBeforeSceneClaim)
        {
            lock (_handoffGate)
            {
                if (!ReferenceEquals(lease, _frozenLease) || owner == null ||
                    _preparedHandoffOwner != null ||
                    PumpOwnership != ApplicationCandidatePumpOwnership.HandoffFrozen ||
                    (SceneLease != null && SceneLease.Status != ApplicationCandidateSceneLeaseStatus.HandoffFrozen))
                    return false;
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffClaimed);
                try
                {
                    afterGraphClaimedBeforeSceneClaim?.Invoke();
                }
                catch
                {
                    Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffFrozen);
                    throw;
                }
                if (SceneLease == null || SceneLease.TryClaimFrozenHandoff())
                    return true;
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffFrozen);
                return false;
            }
        }

        internal void CancelPreparedFrozenHandoff(
            ApplicationCandidateFrozenHandoffLease lease,
            ApplicationCandidateFrozenHandoffOwner owner)
        {
            lock (_handoffGate)
            {
                if (ReferenceEquals(lease, _frozenLease) && ReferenceEquals(_preparedHandoffOwner, owner))
                    _preparedHandoffOwner = null;
            }
        }

        internal bool TryClaimPreparedFrozenHandoff(
            ApplicationCandidateFrozenHandoffLease lease,
            ApplicationCandidateFrozenHandoffOwner owner)
        {
            lock (_handoffGate)
            {
                if (!ReferenceEquals(lease, _frozenLease) || owner == null ||
                    !ReferenceEquals(_preparedHandoffOwner, owner) ||
                    PumpOwnership != ApplicationCandidatePumpOwnership.HandoffFrozen ||
                    (SceneLease != null && !SceneLease.CanClaimPreparedFrozenHandoff))
                    return false;
                SceneLease?.ClaimPreparedFrozenHandoff();
                Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.HandoffClaimed);
                _preparedHandoffOwner = null;
                return true;
            }
        }

        internal ApplicationCandidateSessionCleanupResult FinalizeRollback()
        {
            if (Volatile.Read(ref _finalized) != 0) return ApplicationCandidateSessionCleanupResult.Succeeded;
            if (Volatile.Read(ref _finalizationAttempted) != 0) return _finalizationResult;
            ApplicationCandidateCleanupDebt debts = ApplicationCandidateCleanupDebt.None;
            string firstError = string.Empty;
            if (PumpOwnership != ApplicationCandidatePumpOwnership.RollbackOwner &&
                PumpOwnership != ApplicationCandidatePumpOwnership.RetirementOwner)
            {
                debts |= ApplicationCandidateCleanupDebt.PumpOwner;
                firstError = "PUMP_OWNER_ACTIVE";
            }
            if (RuntimeHost.State != RuntimeLifecycleState.Disposed || _runtimeCleanupDebt)
            {
                debts |= ApplicationCandidateCleanupDebt.RuntimeHost;
                if (string.IsNullOrEmpty(firstError)) firstError = "RUNTIME_HOST_CLEANUP_INCOMPLETE";
            }
            bool resourceReleased = ResourceLease.IsReleased;
            if (!resourceReleased)
            {
                debts |= ApplicationCandidateCleanupDebt.ResourcePlan;
                if (string.IsNullOrEmpty(firstError)) firstError = "RESOURCE_LEASE_ACTIVE";
            }
            ApplicationCandidateAppFlowLeaseStatus appFlowStatus = AppFlowLease.Status;
            if (appFlowStatus != ApplicationCandidateAppFlowLeaseStatus.RolledBack &&
                appFlowStatus != ApplicationCandidateAppFlowLeaseStatus.Disposed)
            {
                debts |= ApplicationCandidateCleanupDebt.AppFlow;
                if (string.IsNullOrEmpty(firstError)) firstError = "APP_FLOW_ROLLBACK_INCOMPLETE";
            }
            if (appFlowStatus == ApplicationCandidateAppFlowLeaseStatus.RolledBack)
            {
                try { AppFlowLease.DisposeRolledBack(); }
                catch (Exception exception)
                {
                    AppendCleanupException(exception);
                    debts |= ApplicationCandidateCleanupDebt.AppFlow;
                    if (string.IsNullOrEmpty(firstError)) firstError = "APP_FLOW_DISPOSE_FAILED";
                }
            }

            SceneLease?.FinalizeUnstarted();
            bool sceneReleased = SceneLease == null || SceneLease.IsReleased;
            if (!sceneReleased)
            {
                debts |= ApplicationCandidateCleanupDebt.Scene;
                if (string.IsNullOrEmpty(firstError)) firstError = "CANDIDATE_SCENE_ACTIVE";
            }

            if (sceneReleased && resourceReleased && RuntimeHost.State == RuntimeLifecycleState.Disposed)
            {
                try
                {
                    ApplicationCandidateResourceGraphCleanupResult resourceResult = ResourceLease.FinalizeGraphRollback();
                    if (!resourceResult.Success)
                    {
                        debts |= ApplicationCandidateCleanupDebt.ResourceGraph;
                        if (string.IsNullOrEmpty(firstError)) firstError = resourceResult.ErrorCode;
                    }
                }
                catch (Exception exception)
                {
                    AppendCleanupException(exception);
                    debts |= ApplicationCandidateCleanupDebt.ResourceGraph;
                    if (string.IsNullOrEmpty(firstError)) firstError = "RESOURCE_GRAPH_FINALIZE_THROW";
                }
            }

            _finalizationResult = debts == ApplicationCandidateCleanupDebt.None
                ? ApplicationCandidateSessionCleanupResult.Succeeded
                : ApplicationCandidateSessionCleanupResult.Failed(debts, firstError);
            ApplicationCandidateResourceLeaseStatus resourceStatus = ResourceLease.Status;
            ApplicationCandidateAppFlowLeaseStatus finalAppFlowStatus = AppFlowLease.Status;
            ApplicationCandidateSceneLeaseStatus finalSceneStatus = SceneLease != null
                ? SceneLease.Status
                : ApplicationCandidateSceneLeaseStatus.Released;
            bool cleanupTerminal = (PumpOwnership == ApplicationCandidatePumpOwnership.RollbackOwner ||
                                    PumpOwnership == ApplicationCandidatePumpOwnership.RetirementOwner) &&
                RuntimeHost.State == RuntimeLifecycleState.Disposed &&
                (resourceStatus == ApplicationCandidateResourceLeaseStatus.Released ||
                 resourceStatus == ApplicationCandidateResourceLeaseStatus.CleanupFailed) &&
                (finalSceneStatus == ApplicationCandidateSceneLeaseStatus.Released ||
                 finalSceneStatus == ApplicationCandidateSceneLeaseStatus.CleanupFailed) &&
                (finalAppFlowStatus == ApplicationCandidateAppFlowLeaseStatus.Disposed ||
                 finalAppFlowStatus == ApplicationCandidateAppFlowLeaseStatus.Debt);
            if (cleanupTerminal) Volatile.Write(ref _finalizationAttempted, 1);
            if (!_finalizationResult.Success) return _finalizationResult;
            Volatile.Write(ref _pumpOwnership, (int)ApplicationCandidatePumpOwnership.Disposed);
            Volatile.Write(ref _finalized, 1);
            return ApplicationCandidateSessionCleanupResult.Succeeded;
        }

        internal bool IsRuntimeUntickedAndReady() => RuntimeHost.State == RuntimeLifecycleState.Started &&
            RuntimeHost.Errors.Count == 0 && !RuntimeHost.HasTickAttempt && RuntimeHost.TickCount == 0;
        internal bool IsAppFlowUntickedAndReady() => AppFlowLease.Controller.IsStarted &&
            AppFlowLease.Controller.TickCount == 0;

        internal void TickLive(long frameIndex, double deltaTime, double elapsedTime)
        {
            if (PumpOwnership != ApplicationCandidatePumpOwnership.LiveOwner)
                throw new InvalidOperationException("Only the live owner may pump a candidate graph.");

            RuntimeHost.Tick(frameIndex, deltaTime, elapsedTime);

            if (AppFlowLease.Lifecycle.PumpStrategy == ApplicationControllerPumpStrategy.Standalone)
            {
                _lastLiveStandaloneAppFlowSequence = ++_liveStandalonePumpSequence;
                AppFlowLease.Controller.Tick(frameIndex, deltaTime, elapsedTime);
                _liveStandaloneAppFlowTickCount++;
            }
            if (SceneLease != null &&
                SceneLease.PumpStrategy == ApplicationControllerPumpStrategy.Standalone)
            {
                _lastLiveStandaloneSceneSequence = ++_liveStandalonePumpSequence;
                SceneLease.Controller.Tick();
                _liveStandaloneSceneTickCount++;
            }
        }

        internal ApplicationLiveRuntimeAttemptStatus GetRuntimeTickAttemptStatus(long frameIndex)
        {
            if (!RuntimeHost.HasTickAttempt || RuntimeHost.LastTickAttemptFrame != frameIndex)
                return ApplicationLiveRuntimeAttemptStatus.None;
            return RuntimeHost.LastTickAttemptSucceeded
                ? ApplicationLiveRuntimeAttemptStatus.Succeeded
                : ApplicationLiveRuntimeAttemptStatus.Failed;
        }

        private void AppendCleanupException(Exception exception)
        {
            if (exception == null) return;
            _cleanupException = _cleanupException == null
                ? exception
                : new AggregateException(_cleanupException, exception);
        }

        private static Exception Combine(Exception first, Exception second)
        {
            if (first == null) return second;
            if (second == null) return first;
            return new AggregateException(first, second);
        }

        private static void ValidateAppFlowPumpRegistration(RuntimeHost host, IApplicationCandidateAppFlowLifecycle lifecycle)
        {
            AppFlowController controller = lifecycle.Controller;
            if (controller == null) throw new ArgumentException("Candidate AppFlow lifecycle requires a controller.", nameof(lifecycle));
            switch (lifecycle.PumpStrategy)
            {
                case ApplicationControllerPumpStrategy.RuntimeModule:
                    if (lifecycle.RuntimeModule == null || !ReferenceEquals(lifecycle.RuntimeModule.Controller, controller) ||
                        !host.HasExactAppFlowPumpRegistration(controller, lifecycle.RuntimeModule))
                        throw new InvalidOperationException("RuntimeModule AppFlow pump registration is not exact and exclusive.");
                    break;
                case ApplicationControllerPumpStrategy.Standalone:
                    if (lifecycle.RuntimeModule != null || !host.HasExactAppFlowPumpRegistration(controller, null))
                        throw new InvalidOperationException("Standalone AppFlow controller is also RuntimeModule-pumped.");
                    break;
                default:
                    throw new InvalidOperationException("Candidate AppFlow pump strategy is invalid.");
            }
        }

        private static void ValidateScenePumpRegistration(RuntimeHost host, ApplicationCandidateSceneGraph scene)
        {
            SceneFlowController controller = scene.Controller;
            switch (scene.PumpStrategy)
            {
                case ApplicationControllerPumpStrategy.RuntimeModule:
                    if (scene.RuntimeModule == null || !ReferenceEquals(scene.RuntimeModule.Controller, controller) ||
                        !host.HasExactSceneFlowPumpRegistration(controller, scene.RuntimeModule))
                        throw new InvalidOperationException("RuntimeModule SceneFlow pump registration is not exact and exclusive.");
                    break;
                case ApplicationControllerPumpStrategy.Standalone:
                    if (scene.RuntimeModule != null || !host.HasExactSceneFlowPumpRegistration(controller, null))
                        throw new InvalidOperationException("Standalone SceneFlow controller is also RuntimeModule-pumped.");
                    break;
                default:
                    throw new InvalidOperationException("Candidate SceneFlow pump strategy is invalid.");
            }
        }

        private static void ValidateSceneOwnership(ApplicationCandidateSceneGraph scene)
        {
            if (!(scene.DriverIdentity is IApplicationCandidateOwnedSceneDriver ownedDriver))
                throw new InvalidOperationException("Candidate scene driver lost its ownership contract.");
            ApplicationCandidateSceneOwnershipIdentity driverIdentity = ownedDriver.SceneOwnershipIdentity;
            ApplicationCandidateSceneOwnershipIdentity gateIdentity = scene.IsolationGate.SceneOwnershipIdentity;
            if (driverIdentity == null || gateIdentity == null ||
                !ReferenceEquals(driverIdentity, gateIdentity) ||
                !ReferenceEquals(driverIdentity, scene.OwnershipIdentity))
                throw new InvalidOperationException(
                    "Candidate scene ownership identity changed or no longer matches its isolation gate.");
        }

        private sealed class AppFlowPumpCapability : IApplicationCandidatePumpCapability
        {
            private readonly ApplicationCandidateAppFlowLease _lease;
            public AppFlowPumpCapability(ApplicationCandidateAppFlowLease lease) => _lease = lease;
            public bool CanFreeze => _lease.CanCompensate;
            public void Freeze() => _lease.FreezeForHandoff();
        }

        private sealed class ScenePumpCapability : IApplicationCandidatePumpCapability
        {
            private readonly ApplicationCandidateSceneLease _lease;
            public ScenePumpCapability(ApplicationCandidateSceneLease lease) => _lease = lease;
            public bool CanFreeze => _lease.IsReady;
            public void Freeze()
            {
                if (!_lease.FreezeForHandoff())
                    throw new InvalidOperationException("Candidate scene pump cannot freeze for handoff.");
            }
        }
    }

    /// <summary>Fresh graph factory with exact owner binding and reference-identity reuse rejection.</summary>
    public sealed class ApplicationCandidateSessionFactory : IApplicationCandidateSessionFactory
    {
        private readonly object _gate = new object();
        private readonly IApplicationCandidateSessionGraphBuilder _builder;
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, UsedComponentMarker> _used =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, UsedComponentMarker>();
        private long _nextOrdinal;

        public ApplicationCandidateSessionFactory(IApplicationCandidateSessionGraphBuilder builder) =>
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));

        public ApplicationCandidateSessionGraph Create(IApplicationCandidateSessionTransaction owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            ApplicationCandidateSessionOwnerClaim ownerClaim = owner.CandidateSessionClaim;
            if (ownerClaim == null || !ownerClaim.TryClaim(owner))
                throw new ApplicationCandidateSessionConstructionException(
                    ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed,
                    "Candidate transaction already claimed a graph generation.");

            var token = new ApplicationCandidateSessionToken(Interlocked.Increment(ref _nextOrdinal));
            IApplicationCandidateSessionBuildLease buildLease = null;
            ApplicationCandidateComponentReuse reused = ApplicationCandidateComponentReuse.None;
            try
            {
                buildLease = _builder.Build(token);
                if (buildLease == null || !ReferenceEquals(buildLease.Token, token) || buildLease.Components == null)
                    throw new InvalidOperationException("Candidate graph builder returned an invalid build lease.");
                ApplicationCandidateSessionComponents components = buildLease.Components;
                lock (_gate)
                {
                    if (HasCrossRoleAlias(components))
                        throw new InvalidOperationException("Candidate graph contains a cross-role reference alias.");
                    reused = GetReused(components);
                    if (reused != ApplicationCandidateComponentReuse.None)
                        throw new InvalidOperationException("Candidate graph builder reused a component from an earlier generation.");
                    var graph = new ApplicationCandidateSessionGraph(owner, token, components);
                    RememberAll(components);
                    if (!buildLease.TryTransfer())
                        throw new InvalidOperationException("Candidate build lease was already consumed.");
                    ownerClaim.Attach(owner, graph);
                    return graph;
                }
            }
            catch (Exception primary)
            {
                AbandonOrAggregate(buildLease, reused, primary);
                throw;
            }
        }

        private static bool HasCrossRoleAlias(ApplicationCandidateSessionComponents c)
        {
            var identities = new HashSet<object>(ReferenceIdentityComparer.Instance);
            if (!identities.Add(c.RuntimeHost) ||
                !identities.Add(c.AppFlowLifecycle) ||
                !identities.Add(c.AppFlowLifecycle.Controller) ||
                (c.AppFlowLifecycle.RuntimeModule != null && !identities.Add(c.AppFlowLifecycle.RuntimeModule)) ||
                !identities.Add(c.ResourceGraph.Manager) ||
                !identities.Add(c.ResourceGraph) ||
                !identities.Add(c.ResourceGraph.Orchestrator) ||
                !identities.Add(c.ResourceGraph.ConfigurationIdentity) ||
                !identities.Add(c.ResourceGraph.Plan) ||
                !identities.Add(c.ResourceGraph.Lifetime))
                return true;
            if (c.SceneGraph != null &&
                (!identities.Add(c.SceneGraph) ||
                 !identities.Add(c.SceneGraph.Controller) ||
                 (c.SceneGraph.RuntimeModule != null && !identities.Add(c.SceneGraph.RuntimeModule)) ||
                 !identities.Add(c.SceneGraph.IsolationGate) ||
                 !identities.Add(c.SceneGraph.DriverIdentity)))
                return true;
            return false;
        }

        private ApplicationCandidateComponentReuse GetReused(ApplicationCandidateSessionComponents c)
        {
            ApplicationCandidateComponentReuse result = ApplicationCandidateComponentReuse.None;
            if (IsUsed(c.RuntimeHost)) result |= ApplicationCandidateComponentReuse.RuntimeHost;
            if (IsUsed(c.AppFlowLifecycle)) result |= ApplicationCandidateComponentReuse.AppFlowLifecycle;
            if (IsUsed(c.AppFlowLifecycle.Controller)) result |= ApplicationCandidateComponentReuse.AppFlowController;
            if (c.AppFlowLifecycle.RuntimeModule != null && IsUsed(c.AppFlowLifecycle.RuntimeModule)) result |= ApplicationCandidateComponentReuse.AppFlowRuntimeModule;
            if (IsUsed(c.ResourceGraph.Manager)) result |= ApplicationCandidateComponentReuse.ResourceManager;
            if (IsUsed(c.ResourceGraph)) result |= ApplicationCandidateComponentReuse.ResourceGraph;
            if (IsUsed(c.ResourceGraph.Orchestrator)) result |= ApplicationCandidateComponentReuse.ResourceOrchestrator;
            if (IsUsed(c.ResourceGraph.ConfigurationIdentity)) result |= ApplicationCandidateComponentReuse.ResourceConfiguration;
            if (IsUsed(c.ResourceGraph.Plan)) result |= ApplicationCandidateComponentReuse.ResourcePlan;
            if (IsUsed(c.ResourceGraph.Lifetime)) result |= ApplicationCandidateComponentReuse.ResourceLifetime;
            if (c.SceneGraph != null)
            {
                if (IsUsed(c.SceneGraph)) result |= ApplicationCandidateComponentReuse.SceneGraph;
                if (IsUsed(c.SceneGraph.Controller)) result |= ApplicationCandidateComponentReuse.SceneController;
                if (c.SceneGraph.RuntimeModule != null && IsUsed(c.SceneGraph.RuntimeModule)) result |= ApplicationCandidateComponentReuse.SceneRuntimeModule;
                if (IsUsed(c.SceneGraph.IsolationGate)) result |= ApplicationCandidateComponentReuse.SceneIsolationGate;
                if (IsUsed(c.SceneGraph.DriverIdentity)) result |= ApplicationCandidateComponentReuse.SceneDriver;
            }
            return result;
        }

        private void RememberAll(ApplicationCandidateSessionComponents c)
        {
            Remember(c.RuntimeHost); Remember(c.AppFlowLifecycle); Remember(c.AppFlowLifecycle.Controller);
            if (c.AppFlowLifecycle.RuntimeModule != null) Remember(c.AppFlowLifecycle.RuntimeModule);
            Remember(c.ResourceGraph.Manager); Remember(c.ResourceGraph); Remember(c.ResourceGraph.Orchestrator);
            Remember(c.ResourceGraph.ConfigurationIdentity); Remember(c.ResourceGraph.Plan); Remember(c.ResourceGraph.Lifetime);
            if (c.SceneGraph != null)
            {
                Remember(c.SceneGraph); Remember(c.SceneGraph.Controller);
                if (c.SceneGraph.RuntimeModule != null) Remember(c.SceneGraph.RuntimeModule);
                Remember(c.SceneGraph.IsolationGate);
                Remember(c.SceneGraph.DriverIdentity);
            }
        }

        private bool IsUsed(object value) => value == null || _used.TryGetValue(value, out _);
        private void Remember(object value) => _used.Add(value, UsedComponentMarker.Instance);
        private static void AbandonOrAggregate(
            IApplicationCandidateSessionBuildLease lease,
            ApplicationCandidateComponentReuse reused,
            Exception primary)
        {
            if (lease == null) return;
            try { lease.Abandon(reused); }
            catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
        }

        private sealed class UsedComponentMarker
        {
            public static readonly UsedComponentMarker Instance = new UsedComponentMarker();
        }

        private sealed class ReferenceIdentityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceIdentityComparer Instance = new ReferenceIdentityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }
}
