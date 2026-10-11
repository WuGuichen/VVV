using System;
using System.Collections.Generic;
using System.Threading;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PersistenceBridge;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.ApplicationStartup.SessionBridge.Unity;
using MxFramework.ApplicationStartup.Unity;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Platform;
using MxFramework.Platform.Unity;
using MxFramework.Resources;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Tests.PlayMode.Application
{
    public enum ApplicationUnityBootValidationScenario
    {
        Success = 0,
        FailFirstAttempt = 1,
        CancelFirstAttempt = 2,
        FailThenCancelThenSuccess = 3,
        RestoreBoundaryFailureThenSuccess = 4
    }

    /// <summary>Process-local controls and evidence for the generated PlayMode validation scene.</summary>
    public static class ApplicationUnityBootValidationProbe
    {
        public static ApplicationUnityBootValidationScenario Scenario { get; private set; }
        public static bool BootstrapExistingLive { get; private set; }
        public static int CompositionCreateCount { get; internal set; }
        public static int RunCreateCount { get; internal set; }
        public static int GraphBuildCount { get; internal set; }
        public static int CommitCount { get; internal set; }
        public static int RuntimeTickCount { get; internal set; }
        public static long RetriedRunFirstTickFrame { get; internal set; }
        public static int ResourceReleaseCount { get; internal set; }
        public static int AppFlowRollbackCount { get; internal set; }
        public static int RestoreBoundaryInjectionCount { get; internal set; }
        public static object FirstGraph { get; private set; }
        public static object SecondGraph { get; private set; }
        public static object ThirdGraph { get; private set; }
        public static LiveApplicationStatePublisher Publisher { get; internal set; }
        public static UnityApplicationLifecycleReporter Reporter { get; internal set; }

        public static void Reset(
            ApplicationUnityBootValidationScenario scenario,
            bool bootstrapExistingLive = false)
        {
            Scenario = scenario;
            BootstrapExistingLive = bootstrapExistingLive;
            CompositionCreateCount = 0;
            RunCreateCount = 0;
            GraphBuildCount = 0;
            CommitCount = 0;
            RuntimeTickCount = 0;
            RetriedRunFirstTickFrame = -1;
            ResourceReleaseCount = 0;
            AppFlowRollbackCount = 0;
            RestoreBoundaryInjectionCount = 0;
            FirstGraph = null;
            SecondGraph = null;
            ThirdGraph = null;
            Publisher = null;
            Reporter = null;
        }

        internal static void RecordGraph(long attempt, ApplicationCandidateSessionGraph graph)
        {
            if (attempt == 1) FirstGraph = graph;
            else if (attempt == 2) SecondGraph = graph;
            else if (attempt == 3) ThirdGraph = graph;
        }
    }

    /// <summary>
    /// Test-owned outer composition for the generated Boot validation scene. It composes production
    /// Application, SessionBridge, Unity scene, Runtime, Resources and Persistence boundaries while
    /// keeping scenario faults outside framework assemblies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationUnityBootValidationCompositionFixture :
        MonoBehaviour,
        IApplicationUnityBootCompositionFactory
    {
        private const int RetirementCapacity = 4;
        private static readonly List<CandidateResourceLifetime> s_testResourceLifetimes =
            new List<CandidateResourceLifetime>();
        private readonly InMemorySaveGameStore _store = new InMemorySaveGameStore();
        private readonly ApplicationPersistenceDurableHeadAuthority _durableHead =
            new ApplicationPersistenceDurableHeadAuthority();
        private RuntimeSaveStateRestoreProfile _profile;
        private RuntimeSaveGameService _loadService;

        internal static void ReleaseRetainedTestResources()
        {
            for (int i = 0; i < s_testResourceLifetimes.Count; i++)
                s_testResourceLifetimes[i].Release();
            s_testResourceLifetimes.Clear();
        }

        public ApplicationUnityBootCompositionCreateResult TryCreate()
        {
            ApplicationUnityBootValidationProbe.CompositionCreateCount++;
            try
            {
                _profile = CreateProfile();
                BootstrapStore();
                var fence = new ApplicationPublicationSaveFence();
                var publisher = new LiveApplicationStatePublisher(RetirementCapacity, fence);
                if (ApplicationUnityBootValidationProbe.BootstrapExistingLive)
                    BootstrapOldLive(publisher);
                ApplicationUnityBootValidationProbe.GraphBuildCount = 0;
                ApplicationUnityBootValidationProbe.CommitCount = 0;
                ApplicationUnityBootValidationProbe.RuntimeTickCount = 0;
                ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame = -1;
                ApplicationUnityBootValidationProbe.ResourceReleaseCount = 0;
                ApplicationUnityBootValidationProbe.AppFlowRollbackCount = 0;
                var reporter = UnityApplicationLifecycleReporter.CreateForCurrentApplication(8);
                ApplicationPlatformLifecycleBridgeCreateStatus bridgeStatus =
                    ApplicationPersistencePlatformLifecycleBridgeFactory.TryCreate(
                        reporter,
                        publisher,
                        fence,
                        new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                        new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                        8,
                        out ApplicationPlatformLifecycleBridge bridge);
                if (bridgeStatus != ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded)
                    return Failed("PLAYMODE_LIFECYCLE_BRIDGE_" + bridgeStatus);

                ApplicationUnityBootValidationProbe.Publisher = publisher;
                ApplicationUnityBootValidationProbe.Reporter = reporter;
                return ApplicationUnityBootCompositionCreateResult.Succeeded(
                    new ApplicationUnityBootComposition(
                        publisher,
                        fence,
                        bridge,
                        reporter,
                        new RunFactory(this),
                        new ApplicationUnityStartupIntent(ApplicationUnityStartupIntentKind.ColdBoot),
                        RetirementCapacity));
            }
            catch (Exception exception)
            {
                return Failed("PLAYMODE_COMPOSITION_" + exception.GetType().Name);
            }
        }

        private static ApplicationUnityBootCompositionCreateResult Failed(string code) =>
            new ApplicationUnityBootCompositionCreateResult(
                ApplicationUnityBootCompositionCreateStatus.Failed, null, code);

        private void BootstrapStore()
        {
            _loadService = new RuntimeSaveGameService(
                _store,
                CreateSourceBarrier(_profile),
                CreateBuildDescriptor(),
                _profile,
                ExactCompatibility.Instance);
            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> planned =
                _loadService.PlanCheckpointSave(new RuntimeSaveGameCheckpointRequest(
                    new SaveGameProfileId("playmode-profile"),
                    new SaveGameSlotId("boot-slot"),
                    SaveGameGeneration.None,
                    new DateTime(638000000000000000L, DateTimeKind.Utc),
                    new DateTime(638000000010000000L, DateTimeKind.Utc),
                    null,
                    null));
            if (!planned.Success)
                throw new InvalidOperationException("Unable to plan the frozen PlayMode bootstrap save.");
            IRuntimeSaveGameOperation<SaveGameCommitResult> committed = _loadService.Commit(planned.Value);
            if (committed.Status != SaveGameOperationStatus.Succeeded)
                throw new InvalidOperationException("Unable to publish the frozen PlayMode bootstrap save.");
        }

        private PreparedRuntimeSaveGame PrepareCurrent()
        {
            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> operation = _loadService.PrepareLoad(
                new RuntimeSaveGameLoadRequest(
                    new SaveGameProfileId("playmode-profile"),
                    new SaveGameSlotId("boot-slot"),
                    SaveGameReadSelection.Current));
            if (operation.Status != SaveGameOperationStatus.Succeeded || !operation.HasResult)
                throw new InvalidOperationException("Unable to prepare the PlayMode bootstrap save.");
            return operation.Result;
        }

        private void BootstrapOldLive(LiveApplicationStatePublisher publisher)
        {
            var factory = new CandidateTransactionFactory(
                PrepareCurrent(),
                _loadService,
                _store,
                _durableHead,
                0,
                publisher,
                withScene: false);
            if (!ApplicationStartupPlanFactory.TryCreate(factory, false, 0, out ApplicationStartupPlan plan))
                throw new InvalidOperationException("Unable to create the old-live bootstrap plan.");
            var coordinator = new ApplicationStartupCoordinator(plan, publisher);
            coordinator.Start("playmode-old-live");
            for (int i = 0; i < 32 && !coordinator.IsTerminal; i++)
            {
                var context = new ApplicationStartupTickContext(0.016d, (i + 1) * 0.016d);
                coordinator.Tick(in context);
            }
            if (coordinator.State != ApplicationStartupState.Succeeded || !publisher.Current.HasRuntimeSession)
                throw new InvalidOperationException("Old-live bootstrap did not publish a runtime session.");
        }

        private static RuntimeCheckpointBarrier CreateSourceBarrier(RuntimeSaveStateRestoreProfile profile)
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(new RuntimeSaveStateRootHeaderProvider(profile.Descriptor).CreateParticipant());
            RuntimeSaveStateCoordinatorFactoryResult result =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);
            if (!result.Success) throw new InvalidOperationException("Source coordinator creation failed.");
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            host.Tick(41, 0.016d);
            return new RuntimeCheckpointBarrier(
                host, result.Coordinator, new RuntimeCheckpointQuiescenceRegistry());
        }

        private static RuntimeSaveStateRestoreProfile CreateProfile() =>
            RuntimeSaveStateRestoreProfile.Create(
                new RuntimeSaveStateRootHeaderDescriptor(0, "framework", "config", "resources"),
                new RuntimeSaveStateMigrationPipeline());

        private static SaveGameBuildDescriptor CreateBuildDescriptor() =>
            new SaveGameBuildDescriptor(
                "framework", "playmode-build", "config", "resources", string.Empty);

        private sealed class RunFactory : IApplicationUnityStartupRunFactory
        {
            private readonly ApplicationUnityBootValidationCompositionFixture _owner;
            public RunFactory(ApplicationUnityBootValidationCompositionFixture owner) => _owner = owner;

            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent,
                long attempt,
                LiveApplicationStatePublisher publisher)
            {
                ApplicationUnityBootValidationProbe.RunCreateCount++;
                var transactionFactory = new CandidateTransactionFactory(
                    _owner.PrepareCurrent(),
                    _owner._loadService,
                    _owner._store,
                    _owner._durableHead,
                    attempt,
                    publisher,
                    withScene: true);
                return ApplicationStartupPlanFactory.TryCreate(transactionFactory, true, attempt, out ApplicationStartupPlan plan)
                    ? ApplicationUnityStartupRunCreateResult.Succeeded(
                        new ApplicationStartupCoordinator(plan, publisher))
                    : new ApplicationUnityStartupRunCreateResult(
                        ApplicationUnityStartupRunCreateStatus.Failed, null, "PLAYMODE_PLAN_INVALID");
            }
        }

        private static class ApplicationStartupPlanFactory
        {
            public static bool TryCreate(
                CandidateTransactionFactory transactionFactory,
                bool withScene,
                long attempt,
                out ApplicationStartupPlan plan)
            {
                var resource = new ApplicationStartupStepId("candidate-resource");
                var runtime = new ApplicationStartupStepId("candidate-runtime");
                var appFlow = new ApplicationStartupStepId("candidate-app-flow");
                var scene = new ApplicationStartupStepId("candidate-scene");
                var validation = new ApplicationStartupStepId("validation-post-scene");
                var handoff = new ApplicationStartupStepId("candidate-handoff");
                ApplicationStartupStepDescriptor[] descriptors = withScene ? new[]
                {
                    new ApplicationStartupStepDescriptor(resource, null, 10, 1f),
                    new ApplicationStartupStepDescriptor(scene, new[] { resource }, 20, 1f),
                    new ApplicationStartupStepDescriptor(runtime, new[] { scene }, 30, 1f),
                    new ApplicationStartupStepDescriptor(appFlow, new[] { runtime }, 40, 1f),
                    new ApplicationStartupStepDescriptor(validation, new[] { scene }, 45, 1f),
                    new ApplicationStartupStepDescriptor(handoff, new[] { validation }, 50, 1f)
                } : new[]
                {
                    new ApplicationStartupStepDescriptor(resource, null, 10, 1f),
                    new ApplicationStartupStepDescriptor(runtime, new[] { resource }, 20, 1f),
                    new ApplicationStartupStepDescriptor(appFlow, new[] { runtime }, 30, 1f),
                    new ApplicationStartupStepDescriptor(handoff, new[] { appFlow }, 50, 1f)
                };
                IApplicationStartupStep[] steps = withScene ? new IApplicationStartupStep[]
                {
                    new ApplicationCandidateResourceStep(resource),
                    new ApplicationCandidateSceneStep(scene,
                        new ApplicationCandidateSceneRequest(ApplicationCandidateUnitySceneFixture.SceneKey)),
                    new ApplicationCandidateRuntimeLifecycleStep(runtime),
                    new ApplicationCandidateAppFlowStep(appFlow),
                    new ScenarioPostSceneStep(validation, attempt),
                    new ApplicationCandidateHandoffStep(handoff)
                } : new IApplicationStartupStep[]
                {
                    new ApplicationCandidateResourceStep(resource),
                    new ApplicationCandidateRuntimeLifecycleStep(runtime),
                    new ApplicationCandidateAppFlowStep(appFlow),
                    new ApplicationCandidateHandoffStep(handoff)
                };
                return ApplicationStartupPlan.TryCreate(
                    new ApplicationStartupPlanId("playmode-application-boot"),
                    descriptors,
                    steps,
                    transactionFactory,
                    out plan,
                    out _);
            }
        }

        private sealed class CandidateTransactionFactory : IApplicationStartupTransactionFactory
        {
            private readonly PreparedRuntimeSaveGame _prepared;
            private readonly RuntimeSaveGameService _loadService;
            private readonly InMemorySaveGameStore _store;
            private readonly ApplicationPersistenceDurableHeadAuthority _durableHead;
            private readonly long _attempt;
            private readonly LiveApplicationStatePublisher _publisher;
            private readonly ApplicationCandidateSessionFactory _sessionFactory;

            public CandidateTransactionFactory(
                PreparedRuntimeSaveGame prepared,
                RuntimeSaveGameService loadService,
                InMemorySaveGameStore store,
                ApplicationPersistenceDurableHeadAuthority durableHead,
                long attempt,
                LiveApplicationStatePublisher publisher,
                bool withScene)
            {
                _prepared = prepared;
                _loadService = loadService;
                _store = store;
                _durableHead = durableHead;
                _attempt = attempt;
                _publisher = publisher;
                _sessionFactory = new ApplicationCandidateSessionFactory(
                    new CandidateGraphBuilder(attempt, withScene));
            }

            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId)
            {
                var transaction = new CandidateTransaction(
                    "playmode-run-" + _attempt,
                    _prepared,
                    _loadService,
                    _store,
                    _durableHead,
                    _attempt,
                    _publisher);
                ApplicationCandidateSessionGraph graph = _sessionFactory.Create(transaction);
                ApplicationUnityBootValidationProbe.RecordGraph(_attempt, graph);
                return transaction;
            }
        }

        private sealed class CandidateTransaction : IApplicationCandidateSceneTransaction
        {
            private readonly PreparedRuntimeSaveGame _prepared;
            private readonly RuntimeSaveGameService _loadService;
            private readonly InMemorySaveGameStore _store;
            private readonly ApplicationPersistenceDurableHeadAuthority _durableHead;
            private readonly long _attempt;
            private readonly LiveApplicationStatePublisher _publisher;
            private ApplicationPersistenceCandidateSession _session;
            private ApplicationStartupCommitObservation _observation;

            public CandidateTransaction(
                string id,
                PreparedRuntimeSaveGame prepared,
                RuntimeSaveGameService loadService,
                InMemorySaveGameStore store,
                ApplicationPersistenceDurableHeadAuthority durableHead,
                long attempt,
                LiveApplicationStatePublisher publisher)
            {
                TransactionId = id;
                _prepared = prepared;
                _loadService = loadService;
                _store = store;
                _durableHead = durableHead;
                _attempt = attempt;
                _publisher = publisher;
                CandidateSessionClaim = new ApplicationCandidateSessionOwnerClaim();
            }

            public string TransactionId { get; }
            public ApplicationCandidateSessionOwnerClaim CandidateSessionClaim { get; }
            public ApplicationCandidateSceneTransactionPort CandidateScenePort { get; } =
                new ApplicationCandidateSceneTransactionPort();
            public ApplicationStartupCommitObservation CommitObservation => _observation;

            public ApplicationStartupCommitResult TryCommit(
                LiveApplicationState expected,
                ILiveApplicationStatePublisher publisher)
            {
                if (!ReferenceEquals(publisher, _publisher))
                    return FailedCommit("Foreign publisher.");
                try
                {
                    _session = ApplicationPersistenceCandidateSessionFactory.Create(
                        TransactionId,
                        CandidateSessionClaim.CandidateGraph,
                        new CandidateCoordinatorBuilder(
                            _prepared.RestorePlan.Profile, _store, _durableHead));
                    RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> restored =
                        _loadService.RestorePrepared(_prepared, _session);
                    if (restored.Success && _attempt == 1 &&
                        ApplicationUnityBootValidationProbe.Scenario ==
                            ApplicationUnityBootValidationScenario.RestoreBoundaryFailureThenSuccess)
                    {
                        ApplicationUnityBootValidationProbe.RestoreBoundaryInjectionCount++;
                        _observation = ApplicationStartupCommitObservation.NotPublished;
                        return FailedCommit("Injected failure at the restore/publication boundary.");
                    }
                    if (!restored.Success ||
                        !_session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _))
                    {
                        _observation = ApplicationStartupCommitObservation.NotPublished;
                        return FailedCommit("Candidate restore did not produce a publication candidate.");
                    }
                    ApplicationStartupCommitResult result = publisher.TryPublish(expected, candidate);
                    if (result.Success)
                    {
                        ApplicationUnityBootValidationProbe.CommitCount++;
                        _observation = ApplicationStartupCommitObservation.Published(result.PublishedState);
                    }
                    else _observation = ApplicationStartupCommitObservation.NotPublished;
                    return result;
                }
                catch (Exception exception)
                {
                    return new ApplicationStartupCommitResult(
                        ApplicationStartupCommitStatus.Failed,
                        new ApplicationStartupError(
                            ApplicationStartupErrorCode.CommitFailed,
                            ApplicationStartupPhase.Commit,
                            default,
                            default,
                            null,
                            "PlayMode candidate commit failed.",
                            exception));
                }
            }

            public ApplicationStartupRollbackResult FinalizeRollback()
            {
                _session?.Dispose();
                return ApplicationStartupRollbackResult.Succeeded;
            }

            public void Dispose() => _prepared.Dispose();

            private static ApplicationStartupCommitResult FailedCommit(string message) =>
                new ApplicationStartupCommitResult(
                    ApplicationStartupCommitStatus.Failed,
                    new ApplicationStartupError(
                        ApplicationStartupErrorCode.CommitFailed,
                        ApplicationStartupPhase.Commit,
                        default,
                        default,
                        null,
                        message,
                        null));
        }

        private sealed class ScenarioPostSceneStep : IApplicationStartupStep
        {
            private readonly long _attempt;

            public ScenarioPostSceneStep(ApplicationStartupStepId stepId, long attempt)
            {
                StepId = stepId;
                _attempt = attempt;
            }

            public ApplicationStartupStepId StepId { get; }

            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
            {
                ApplicationUnityBootValidationScenario scenario = ApplicationUnityBootValidationProbe.Scenario;
                bool fail = _attempt == 1 &&
                    (scenario == ApplicationUnityBootValidationScenario.FailFirstAttempt ||
                     scenario == ApplicationUnityBootValidationScenario.FailThenCancelThenSuccess);
                bool pending = (_attempt == 1 && scenario == ApplicationUnityBootValidationScenario.CancelFirstAttempt) ||
                    (_attempt == 2 && scenario == ApplicationUnityBootValidationScenario.FailThenCancelThenSuccess);
                return new ScenarioPostSceneOperation(fail, pending);
            }

            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context) =>
                new ScenarioPostSceneOperation(false, false);
        }

        private sealed class ScenarioPostSceneOperation : IApplicationStartupStepOperation
        {
            private readonly bool _fail;
            private readonly bool _pending;

            public ScenarioPostSceneOperation(bool fail, bool pending)
            {
                _fail = fail;
                _pending = pending;
                Status = RuntimeOperationStatus.Running;
            }

            public string OperationId => "playmode-post-scene-validation";
            public RuntimeOperationStatus Status { get; private set; }
            public float Progress => Status == RuntimeOperationStatus.Running ? 0f : 1f;
            public RuntimeOperationError Error => Status == RuntimeOperationStatus.Failed
                ? new RuntimeOperationError("PLAYMODE_POST_SCENE_FAILURE", "Injected post-scene validation failure.")
                : RuntimeOperationError.None;
            public bool CanCancel => Status == RuntimeOperationStatus.Running;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;

            public void Tick(in ApplicationStartupTickContext context)
            {
                if (Status != RuntimeOperationStatus.Running || _pending) return;
                Status = _fail ? RuntimeOperationStatus.Failed : RuntimeOperationStatus.Succeeded;
            }

            public void RequestCancel()
            {
                if (Status == RuntimeOperationStatus.Running) Status = RuntimeOperationStatus.Cancelled;
            }
        }

        private sealed class CandidateGraphBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private readonly long _attempt;
            private readonly bool _withScene;
            public CandidateGraphBuilder(long attempt, bool withScene)
            {
                _attempt = attempt;
                _withScene = withScene;
            }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                ApplicationUnityBootValidationProbe.GraphBuildCount++;
                var host = new RuntimeHost();
                host.RegisterModule(new ProbeRuntimeModule(_attempt));
                var controller = new AppFlowController();
                controller.RegisterState(new EmptyAppFlowState());
                var lifecycle = new CandidateAppFlowLifecycle(controller, false, false);
                var manager = new ResourceManager();
                var lifetime = new CandidateResourceLifetime();
                s_testResourceLifetimes.Add(lifetime);
                var resourceGraph = new ApplicationCandidateResourceGraph(
                    manager,
                    manager.SealRuntimeConfiguration(),
                    new ResourcePlanOrchestrator(new ResourcePreloadService(manager), manager),
                    new ResourcePlan("playmode-empty", "v1", Array.Empty<ResourceKey>()),
                    lifetime);
                ApplicationCandidateSceneGraph sceneGraph = _withScene
                    ? new ApplicationCandidateUnitySceneGraphFactory().Create(
                        ApplicationCandidateUnitySceneFixture.LoadApprovedManifest(),
                        new RecordingCandidateUnitySceneArtifactProvider(),
                        ApplicationControllerPumpStrategy.Standalone)
                    : null;
                var components = new ApplicationCandidateSessionComponents(
                    host, lifecycle, resourceGraph, sceneGraph);
                return new CandidateBuildLease(token, components, host, lifecycle, lifetime);
            }
        }

        private sealed class CandidateBuildLease : IApplicationCandidateSessionBuildLease
        {
            private readonly RuntimeHost _host;
            private readonly CandidateAppFlowLifecycle _lifecycle;
            private readonly CandidateResourceLifetime _lifetime;
            private int _state;

            public CandidateBuildLease(
                ApplicationCandidateSessionToken token,
                ApplicationCandidateSessionComponents components,
                RuntimeHost host,
                CandidateAppFlowLifecycle lifecycle,
                CandidateResourceLifetime lifetime)
            {
                Token = token;
                Components = components;
                _host = host;
                _lifecycle = lifecycle;
                _lifetime = lifetime;
            }

            public ApplicationCandidateSessionToken Token { get; }
            public ApplicationCandidateSessionComponents Components { get; }
            public bool TryTransfer() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            public void Abandon(ApplicationCandidateComponentReuse reusedComponents)
            {
                if (Interlocked.CompareExchange(ref _state, 2, 0) != 0) return;
                if ((reusedComponents & ApplicationCandidateComponentReuse.RuntimeHost) == 0) _host.Dispose();
                if ((reusedComponents & ApplicationCandidateComponentReuse.AppFlowLifecycle) == 0) _lifecycle.Dispose();
                if ((reusedComponents & ApplicationCandidateComponentReuse.ResourceLifetime) == 0) _lifetime.Release();
            }
        }

        private sealed class CandidateAppFlowLifecycle : IApplicationCandidateAppFlowLifecycle
        {
            private readonly bool _fail;
            private readonly bool _pendingUntilCancelled;
            public CandidateAppFlowLifecycle(AppFlowController controller, bool fail, bool pendingUntilCancelled)
            {
                Controller = controller;
                _fail = fail;
                _pendingUntilCancelled = pendingUntilCancelled;
            }

            public AppFlowController Controller { get; }
            public AppFlowRuntimeModule RuntimeModule => null;
            public ApplicationControllerPumpStrategy PumpStrategy => ApplicationControllerPumpStrategy.Standalone;
            public bool CanCompensate => true;

            public IApplicationStartupStepOperation PrepareStart() =>
                new AppFlowOperation(Controller, _fail, _pendingUntilCancelled, false);

            public void FreezeForHandoff() { }

            public IApplicationStartupStepOperation BeginRollback()
            {
                ApplicationUnityBootValidationProbe.AppFlowRollbackCount++;
                return new AppFlowOperation(Controller, false, false, true);
            }

            public void Dispose() { }
        }

        private sealed class AppFlowOperation : IApplicationStartupStepOperation
        {
            private readonly AppFlowController _controller;
            private readonly bool _fail;
            private readonly bool _pending;
            private readonly bool _rollback;
            public AppFlowOperation(AppFlowController controller, bool fail, bool pending, bool rollback)
            {
                _controller = controller;
                _fail = fail;
                _pending = pending;
                _rollback = rollback;
                Status = RuntimeOperationStatus.Running;
            }
            public string OperationId => _rollback ? "playmode-appflow-rollback" : "playmode-appflow-prepare";
            public RuntimeOperationStatus Status { get; private set; }
            public float Progress => Status == RuntimeOperationStatus.Running ? 0f : 1f;
            public RuntimeOperationError Error => Status == RuntimeOperationStatus.Failed
                ? new RuntimeOperationError("PLAYMODE_APPFLOW_FAILURE", "Injected PlayMode AppFlow failure.")
                : RuntimeOperationError.None;
            public bool CanCancel => Status == RuntimeOperationStatus.Running;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;
            public void Tick(in ApplicationStartupTickContext context)
            {
                if (Status != RuntimeOperationStatus.Running || _pending) return;
                if (_rollback) { Status = RuntimeOperationStatus.Succeeded; return; }
                if (_fail) { Status = RuntimeOperationStatus.Failed; return; }
                _controller.Start("initial", "playmode-candidate");
                Status = RuntimeOperationStatus.Succeeded;
            }
            public void RequestCancel()
            {
                if (Status == RuntimeOperationStatus.Running) Status = RuntimeOperationStatus.Cancelled;
            }
        }

        private sealed class CandidateResourceLifetime : IApplicationCandidateResourceGraphLifetime
        {
            public bool IsReleased { get; private set; }
            public ApplicationCandidateResourceGraphCleanupResult FinalizeRollback()
            {
                Release();
                return ApplicationCandidateResourceGraphCleanupResult.Succeeded;
            }
            public void Release()
            {
                if (IsReleased) return;
                IsReleased = true;
                ApplicationUnityBootValidationProbe.ResourceReleaseCount++;
            }
        }

        private sealed class ProbeRuntimeModule : RuntimeModule
        {
            private readonly long _attempt;
            public ProbeRuntimeModule(long attempt) : base("playmode-probe") => _attempt = attempt;
            public override void Tick(RuntimeTickContext context)
            {
                ApplicationUnityBootValidationProbe.RuntimeTickCount++;
                if (ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame < 0)
                    ApplicationUnityBootValidationProbe.RetriedRunFirstTickFrame = context.FrameIndex;
            }
        }

        private sealed class EmptyAppFlowState : IAppFlowState
        {
            public string StateId => "initial";
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }

        private sealed class CandidateCoordinatorBuilder :
            IApplicationPersistenceCandidateCoordinatorBuilder
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private readonly InMemorySaveGameStore _store;
            private readonly ApplicationPersistenceDurableHeadAuthority _durableHead;
            public CandidateCoordinatorBuilder(
                RuntimeSaveStateRestoreProfile profile,
                InMemorySaveGameStore store,
                ApplicationPersistenceDurableHeadAuthority durableHead)
            {
                _profile = profile;
                _store = store;
                _durableHead = durableHead;
            }

            public IApplicationPersistenceCandidateCoordinatorBuildLease Build(
                ApplicationCandidateRuntimeCompositionIdentity compositionIdentity,
                RuntimeHost runtimeHost)
            {
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                RuntimeSaveStateCoordinatorFactoryResult result =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                if (!result.Success) throw new InvalidOperationException("Candidate coordinator creation failed.");
                var service = new RuntimeSaveGameService(
                    _store,
                    new RuntimeCheckpointBarrier(
                        runtimeHost,
                        result.Coordinator,
                        new RuntimeCheckpointQuiescenceRegistry()),
                    CreateBuildDescriptor(),
                    _profile,
                    ExactCompatibility.Instance);
                return new CoordinatorBuildLease(
                    compositionIdentity,
                    runtimeHost,
                    result.Coordinator,
                    service,
                    _durableHead);
            }
        }

        private sealed class CoordinatorBuildLease : IApplicationPersistenceCandidateCoordinatorBuildLease
        {
            private int _state;
            public CoordinatorBuildLease(
                ApplicationCandidateRuntimeCompositionIdentity identity,
                RuntimeHost host,
                RuntimeSaveStateCoordinator coordinator,
                RuntimeSaveGameService service,
                ApplicationPersistenceDurableHeadAuthority durableHead)
            {
                CompositionIdentity = identity;
                RuntimeHost = host;
                Coordinator = coordinator;
                CheckpointService = service;
                DurableHeadAuthority = durableHead;
            }
            public ApplicationCandidateRuntimeCompositionIdentity CompositionIdentity { get; }
            public RuntimeHost RuntimeHost { get; }
            public RuntimeSaveStateCoordinator Coordinator { get; }
            public RuntimeSaveGameService CheckpointService { get; }
            public ApplicationPersistenceDurableHeadAuthority DurableHeadAuthority { get; }
            public bool TryTransfer() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            public void Abandon() => Interlocked.CompareExchange(ref _state, 2, 0);
        }

        private sealed class ExactCompatibility : IRuntimeSaveGameCompatibilityChecker
        {
            public static readonly ExactCompatibility Instance = new ExactCompatibility();
            public RuntimeSaveGameCompatibilityResult Check(
                SaveGameBuildDescriptor build,
                RuntimeSaveStateRestoreProfile restoreProfile)
            {
                RuntimeSaveStateRootHeaderDescriptor descriptor = restoreProfile.Descriptor;
                return string.Equals(build.FrameworkVersion, descriptor.FrameworkVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ConfigVersion, descriptor.ConfigVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ResourceCatalogVersion, descriptor.ResourceCatalogVersion, StringComparison.Ordinal)
                    ? RuntimeSaveGameCompatibilityResult.Compatible
                    : RuntimeSaveGameCompatibilityResult.Incompatible(
                        RuntimeSaveGameCompatibilityErrorCode.BuildMismatch);
            }
        }

        private sealed class FixedWallClock : IWallClock
        {
            public FixedWallClock(DateTime utcNow) => UtcNow = utcNow;
            public DateTime UtcNow { get; }
        }
    }
}
