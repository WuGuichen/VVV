using System;
using System.Reflection;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.ApplicationStartup.Unity;
using MxFramework.Platform.Unity;
using MxFramework.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.ApplicationStartup.Unity
{
    public sealed class ApplicationUnityBootDriverTests
    {
        [SetUp]
        public void SetUp() => ApplicationUnityBootRoot.ResetProcessGateForTests();

        [Test]
        public void TryCreate_RequiresExactGraphAndUnclaimedFrameDriver()
        {
            ApplicationUnityBootComposition composition = CreateComposition(new PendingRunFactory(), out LiveApplicationStatePublisher publisher, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            Assert.That(driver, Is.Not.Null);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out _), Is.EqualTo(ApplicationUnityBootErrorCode.FrameDriverAlreadyClaimed));

            var foreignFence = new ApplicationPublicationSaveFence();
            var foreign = new ApplicationUnityBootComposition(
                publisher, foreignFence, composition.LifecycleBridge, composition.LifecycleReporter,
                composition.RunFactory, composition.InitialIntent, composition.RetirementSlotCapacity);
            Assert.That(ApplicationUnityBootDriver.TryCreate(foreign, null, out _), Is.EqualTo(ApplicationUnityBootErrorCode.ForeignComposition));
        }

        [Test]
        public void ExplicitUnavailable_CanRetryWithFreshCoordinator()
        {
            var surface = new QueueSurface();
            var factory = new UnavailableThenPendingRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out _, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, surface, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));

            Assert.That(driver.StartInitialRun(), Is.False);
            Assert.That(driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.RunFactoryUnavailable));
            Assert.That(driver.Attempt, Is.EqualTo(1));
            Assert.That(driver.SurfaceModel.CreationStableErrorCode, Is.SameAs(factory.UnavailableDetail));
            Assert.That(driver.SurfaceModel.CanRetry, Is.True);

            // Start is not an alias for Retry after an unavailable result.
            surface.Request = new ApplicationUnityStartupSurfaceRequest(1, ApplicationUnityStartupRequestKind.Start);
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            Assert.That(factory.CreateCount, Is.EqualTo(1));

            surface.Request = new ApplicationUnityStartupSurfaceRequest(2, ApplicationUnityStartupRequestKind.Retry);
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            Assert.That(factory.CreateCount, Is.EqualTo(2));
            Assert.That(factory.FirstAttempt, Is.EqualTo(1));
            Assert.That(factory.SecondAttempt, Is.EqualTo(2));
            Assert.That(driver.ActiveCoordinator, Is.Not.Null);
            Assert.That(driver.ActiveCoordinator.State, Is.EqualTo(ApplicationStartupState.Running));
            Assert.That(driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.None));
            Assert.That(driver.SurfaceModel.CreationStableErrorCode, Is.Empty);
        }

        [Test]
        public void UnavailableRetry_ThatThrowsBecomesFatal()
        {
            var surface = new QueueSurface();
            var factory = new UnavailableThenThrowingRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out _, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, surface, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            Assert.That(driver.StartInitialRun(), Is.False);
            surface.Request = new ApplicationUnityStartupSurfaceRequest(1, ApplicationUnityStartupRequestKind.Retry);
            driver.Update(0.01);
            Assert.That(driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.RunFactoryException));
            Assert.That(driver.Attempt, Is.EqualTo(2));
            Assert.That(driver.SurfaceModel.CreationStableErrorCode, Is.Empty);
            Assert.That(driver.SurfaceModel.CanRetry, Is.False);
            surface.Request = new ApplicationUnityStartupSurfaceRequest(2, ApplicationUnityStartupRequestKind.Retry);
            driver.Update(0.01);
            Assert.That(factory.CreateCount, Is.EqualTo(2));
        }

        [Test]
        public void RejectedRun_PreservesStableCreationDetail()
        {
            var factory = new RejectedRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out _, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out ApplicationUnityBootDriver driver),
                Is.EqualTo(ApplicationUnityBootErrorCode.None));

            Assert.That(driver.StartInitialRun(), Is.False);
            Assert.That(driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.RunFactoryRejected));
            Assert.That(driver.Attempt, Is.EqualTo(1));
            Assert.That(driver.SurfaceModel.CreationStableErrorCode, Is.SameAs(factory.Detail));
        }

        [Test]
        public void RetryPolicy_RejectsRollbackFailedAndFatalRootFailures()
        {
            Assert.That(ApplicationUnityStartupRequestPolicy.CanRetry(
                ApplicationUnityBootRootState.Ready, ApplicationUnityBootErrorCode.None, ApplicationStartupState.RollbackFailed), Is.False);
            Assert.That(ApplicationUnityStartupRequestPolicy.CanRetry(
                ApplicationUnityBootRootState.Failed, ApplicationUnityBootErrorCode.RunFactoryException, ApplicationStartupState.Created), Is.False);
            Assert.That(ApplicationUnityStartupRequestPolicy.CanRetry(
                ApplicationUnityBootRootState.Failed, ApplicationUnityBootErrorCode.RunFactoryUnavailable, ApplicationStartupState.Created), Is.True);
            Assert.That(ApplicationUnityStartupRequestPolicy.CanRetry(
                ApplicationUnityBootRootState.Failed, ApplicationUnityBootErrorCode.RetirementOwnershipDebt, ApplicationStartupState.Failed), Is.False);
        }

        [Test]
        public void SurfaceRequests_UseTypedStartRetryCancelEligibility()
        {
            var surface = new QueueSurface();
            var factory = new PendingRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out _, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, surface, out ApplicationUnityBootDriver driver),
                Is.EqualTo(ApplicationUnityBootErrorCode.None));

            // Cancel has no meaning before an active coordinator exists.
            surface.Request = new ApplicationUnityStartupSurfaceRequest(1, ApplicationUnityStartupRequestKind.Cancel);
            driver.Update(0.01);
            Assert.That(factory.CreateCount, Is.Zero);

            surface.Request = new ApplicationUnityStartupSurfaceRequest(2, ApplicationUnityStartupRequestKind.Start);
            driver.Update(0.01);
            Assert.That(factory.CreateCount, Is.EqualTo(1));
            Assert.That(driver.ActiveCoordinator.CancelRequested, Is.False);

            surface.Request = new ApplicationUnityStartupSurfaceRequest(3, ApplicationUnityStartupRequestKind.Cancel);
            driver.Update(0.01);
            Assert.That(driver.ActiveCoordinator.CancelRequested, Is.True);

            // A second Cancel is rejected once cancellation is already requested.
            surface.Request = new ApplicationUnityStartupSurfaceRequest(4, ApplicationUnityStartupRequestKind.Cancel);
            driver.Update(0.01);
            Assert.That(driver.ActiveCoordinator.CancelRequested, Is.True);
        }

        [Test]
        public void TerminalRetry_UsesASecondFreshCoordinator()
        {
            var surface = new QueueSurface();
            var factory = new ImmediateFailedRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out _, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, surface, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            Assert.That(driver.StartInitialRun(), Is.True);
            ApplicationStartupCoordinator first = driver.ActiveCoordinator;
            Assert.That(first.State, Is.EqualTo(ApplicationStartupState.Failed));
            driver.Update(0.01);

            surface.Request = new ApplicationUnityStartupSurfaceRequest(1, ApplicationUnityStartupRequestKind.Retry);
            driver.Update(0.01);
            Assert.That(factory.CreateCount, Is.EqualTo(2));
            Assert.That(driver.ActiveCoordinator, Is.Not.SameAs(first));
            Assert.That(driver.Attempt, Is.EqualTo(2));
        }

        [Test]
        public void Shutdown_WaitsForActiveCheckpointTerminal()
        {
            var operation = new PendingCheckpointOperation();
            var policy = new StartingCheckpointPolicy(operation);
            ApplicationUnityBootComposition composition = CreateComposition(new PendingRunFactory(), out LiveApplicationStatePublisher publisher, out UnityApplicationLifecycleReporter reporter, policy);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            var liveLease = new PumpCheckpointLease("checkpoint-live");
            Assert.That(publisher.TryPublish(
                publisher.Current, LiveApplicationState.CreateCandidate(true, 1, liveLease, null, liveLease)).Success, Is.True);
            Assert.That(driver.FixedUpdate(0.02), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            Assert.That(reporter.ReportSuspend(true), Is.True);
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            Assert.That(composition.LifecycleBridge.HasActiveCheckpoint, Is.True);

            driver.RequestShutdown();
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            Assert.That(driver.RootState, Is.EqualTo(ApplicationUnityBootRootState.ShuttingDown));

            operation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Cancelled;
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.ShutdownDebt));
            Assert.That(driver.RootState, Is.EqualTo(ApplicationUnityBootRootState.ShutdownDebt));
            Assert.That(driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.LiveSessionStillOwned));

            int checkpointBeginCount = policy.BeginCount;
            int liveTickCount = liveLease.TickCount;
            Assert.That(reporter.ReportSuspend(false), Is.True);
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.ShutdownDebt));
            Assert.That(driver.FixedUpdate(0.02), Is.EqualTo(ApplicationUnityDriverUpdateStatus.ShutdownDebt));
            Assert.That(policy.BeginCount, Is.EqualTo(checkpointBeginCount));
            Assert.That(liveLease.TickCount, Is.EqualTo(liveTickCount));
        }

        [Test]
        public void Update_RetiresTransferredBootReceipt_AndWarmBootPhasesAllocateZero()
        {
            ApplicationUnityBootComposition composition = CreateComposition(new PendingRunFactory(), out LiveApplicationStatePublisher publisher, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            var lease = new DisposableLease("live");
            Assert.That(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(false, -1, lease)).Success, Is.True);
            Assert.That(driver.Update(0.01), Is.EqualTo(ApplicationUnityDriverUpdateStatus.Succeeded));
            // Update first ticks receipts already owned by the driver, then dequeues and begins new
            // receipts. The transferred Boot receipt therefore remains occupied until the next tick.
            Assert.That(publisher.RetiredCount, Is.EqualTo(1));
            Assert.That(driver.SurfaceModel.OccupiedRetirementCount, Is.EqualTo(1));

            driver.Update(0.01);
            Assert.That(publisher.RetiredCount, Is.Zero);
            Assert.That(driver.SurfaceModel.OccupiedRetirementCount, Is.Zero);
            driver.FixedUpdate(0.02);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++)
            {
                driver.Update(0.01);
                driver.FixedUpdate(0.02);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.Zero);
        }

        [Test]
        public void FailedRetirement_PermanentlyOccupiesDebtSlot()
        {
            ApplicationUnityBootComposition composition = CreateComposition(new PendingRunFactory(), out LiveApplicationStatePublisher publisher, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, null, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            var failing = new FailingRetirementLease("old-live");
            Assert.That(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(false, -1, failing)).Success, Is.True);
            driver.Update(0.01); // retire Boot
            Assert.That(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(false, -1, new DisposableLease("new-live"))).Success, Is.True);

            driver.Update(0.01);
            Assert.That(driver.SurfaceModel.OccupiedRetirementCount, Is.EqualTo(1));
            Assert.That(driver.SurfaceModel.FailedRetirementCount, Is.EqualTo(1));
            Assert.That(driver.SurfaceModel.OwnershipDebtStableCode, Is.EqualTo("cleanup.failed"));
            Assert.That(publisher.RetiredCount, Is.EqualTo(1));
            driver.Update(0.01);
            Assert.That(publisher.RetiredCount, Is.EqualTo(1));
        }

        [Test]
        public void RetirementDebt_BlocksAQueuedTerminalRetry()
        {
            var surface = new QueueSurface();
            var factory = new ImmediateFailedRunFactory();
            ApplicationUnityBootComposition composition = CreateComposition(factory, out LiveApplicationStatePublisher publisher, out _);
            Assert.That(ApplicationUnityBootDriver.TryCreate(composition, surface, out ApplicationUnityBootDriver driver), Is.EqualTo(ApplicationUnityBootErrorCode.None));
            driver.StartInitialRun();
            driver.Update(0.01); // project terminal

            var failing = new FailingRetirementLease("old-live");
            Assert.That(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(false, -1, failing)).Success, Is.True);
            driver.Update(0.01); // retire Boot
            Assert.That(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(false, -1, new DisposableLease("new-live"))).Success, Is.True);
            driver.Update(0.01); // latch debt
            Assert.That(driver.SurfaceModel.CanRetry, Is.False);

            surface.Request = new ApplicationUnityStartupSurfaceRequest(1, ApplicationUnityStartupRequestKind.Retry);
            driver.Update(0.01);
            Assert.That(factory.CreateCount, Is.EqualTo(1));
        }

        [Test]
        public void Root_FactoryInvocationFailurePoisonsProcessGate()
        {
            var firstRootObject = new GameObject("first-root");
            var firstFactoryObject = new GameObject("first-factory");
            var firstProxyObject = new GameObject("first-proxy");
            var secondRootObject = new GameObject("second-root");
            var secondFactoryObject = new GameObject("second-factory");
            var secondProxyObject = new GameObject("second-proxy");
            try
            {
                ApplicationUnityBootRoot first = firstRootObject.AddComponent<ApplicationUnityBootRoot>();
                var failedFactory = firstFactoryObject.AddComponent<RejectedCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy firstProxy = firstProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(first.TryInitialize(failedFactory, firstProxy, null), Is.False);
                Assert.That(failedFactory.CreateCount, Is.EqualTo(1));
                Assert.That(first.InitializationStableErrorCode, Is.EqualTo("expected-failure"));

                ApplicationUnityBootRoot second = secondRootObject.AddComponent<ApplicationUnityBootRoot>();
                var validFactory = secondFactoryObject.AddComponent<PublishedRuntimeCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy secondProxy = secondProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(second.TryInitialize(validFactory, secondProxy, null), Is.False);
                Assert.That(second.InitializationError, Is.EqualTo(ApplicationUnityBootErrorCode.OwnershipAlreadyClaimed));
                Assert.That(validFactory.CreateCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstRootObject);
                UnityEngine.Object.DestroyImmediate(firstFactoryObject);
                UnityEngine.Object.DestroyImmediate(firstProxyObject);
                UnityEngine.Object.DestroyImmediate(secondRootObject);
                UnityEngine.Object.DestroyImmediate(secondFactoryObject);
                UnityEngine.Object.DestroyImmediate(secondProxyObject);
                ApplicationUnityBootRoot.ResetProcessGateForTests();
            }
        }

        [Test]
        public void Root_ShutdownBecomesInertBeforeGateIsReleased()
        {
            var firstRootObject = new GameObject("first-root");
            var firstFactoryObject = new GameObject("first-factory");
            var firstProxyObject = new GameObject("first-proxy");
            var secondRootObject = new GameObject("second-root");
            var secondFactoryObject = new GameObject("second-factory");
            var secondProxyObject = new GameObject("second-proxy");
            try
            {
                ApplicationUnityBootRoot first = firstRootObject.AddComponent<ApplicationUnityBootRoot>();
                var firstFactory = firstFactoryObject.AddComponent<BootCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy firstProxy = firstProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(first.TryInitialize(firstFactory, firstProxy, null), Is.True);
                first.BeginControlledShutdown();
                InvokeUnityMessage(first, "Update");
                Assert.That(first.Driver.RootState, Is.EqualTo(ApplicationUnityBootRootState.ShutdownComplete));
                Assert.That(ReadPrivateBool(first, "_driving"), Is.False);

                ApplicationUnityBootRoot second = secondRootObject.AddComponent<ApplicationUnityBootRoot>();
                var secondFactory = secondFactoryObject.AddComponent<PublishedRuntimeCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy secondProxy = secondProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(second.TryInitialize(secondFactory, secondProxy, null), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstRootObject);
                UnityEngine.Object.DestroyImmediate(firstFactoryObject);
                UnityEngine.Object.DestroyImmediate(firstProxyObject);
                UnityEngine.Object.DestroyImmediate(secondRootObject);
                UnityEngine.Object.DestroyImmediate(secondFactoryObject);
                UnityEngine.Object.DestroyImmediate(secondProxyObject);
                ApplicationUnityBootRoot.ResetProcessGateForTests();
            }
        }

        [Test]
        public void Root_LiveCurrentShutdownBecomesDebtAndRetainsGate()
        {
            var firstRootObject = new GameObject("first-root-live");
            var firstFactoryObject = new GameObject("first-factory-live");
            var firstProxyObject = new GameObject("first-proxy-live");
            var secondRootObject = new GameObject("second-root-live");
            var secondFactoryObject = new GameObject("second-factory-live");
            var secondProxyObject = new GameObject("second-proxy-live");
            try
            {
                ApplicationUnityBootRoot first = firstRootObject.AddComponent<ApplicationUnityBootRoot>();
                var firstFactory = firstFactoryObject.AddComponent<PublishedRuntimeCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy firstProxy = firstProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(first.TryInitialize(firstFactory, firstProxy, null), Is.True);
                first.BeginControlledShutdown();
                InvokeUnityMessage(first, "Update");
                Assert.That(first.Driver.RootState, Is.EqualTo(ApplicationUnityBootRootState.ShuttingDown));
                // The Boot sentinel receipt was transferred when the fixture published its live
                // state. It is begun on the first shutdown Update and completes on the next poll.
                InvokeUnityMessage(first, "Update");
                Assert.That(first.Driver.RootState, Is.EqualTo(ApplicationUnityBootRootState.ShutdownDebt));
                Assert.That(first.Driver.RootError, Is.EqualTo(ApplicationUnityBootErrorCode.LiveSessionStillOwned));
                Assert.That(ReadPrivateBool(first, "_driving"), Is.False);
                int tickCount = firstFactory.Lease.TickCount;
                InvokeUnityMessage(first, "Update");
                InvokeUnityMessage(first, "FixedUpdate");
                Assert.That(firstFactory.Lease.TickCount, Is.EqualTo(tickCount));

                ApplicationUnityBootRoot second = secondRootObject.AddComponent<ApplicationUnityBootRoot>();
                var secondFactory = secondFactoryObject.AddComponent<BootCompositionFactoryAdapter>();
                UnityApplicationLifecycleProxy secondProxy = secondProxyObject.AddComponent<UnityApplicationLifecycleProxy>();
                Assert.That(second.TryInitialize(secondFactory, secondProxy, null), Is.False);
                Assert.That(second.InitializationError, Is.EqualTo(ApplicationUnityBootErrorCode.OwnershipAlreadyClaimed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstRootObject);
                UnityEngine.Object.DestroyImmediate(firstFactoryObject);
                UnityEngine.Object.DestroyImmediate(firstProxyObject);
                UnityEngine.Object.DestroyImmediate(secondRootObject);
                UnityEngine.Object.DestroyImmediate(secondFactoryObject);
                UnityEngine.Object.DestroyImmediate(secondProxyObject);
                ApplicationUnityBootRoot.ResetProcessGateForTests();
            }
        }

        private static void InvokeUnityMessage(ApplicationUnityBootRoot root, string methodName)
        {
            MethodInfo method = typeof(ApplicationUnityBootRoot).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(root, null);
        }

        private static bool ReadPrivateBool(ApplicationUnityBootRoot root, string fieldName)
        {
            FieldInfo field = typeof(ApplicationUnityBootRoot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(root);
        }

        private static ApplicationUnityBootComposition CreateComposition(
            IApplicationUnityStartupRunFactory runFactory,
            out LiveApplicationStatePublisher publisher,
            out UnityApplicationLifecycleReporter reporter,
            IApplicationLifecycleCheckpointPolicy policy = null)
        {
            var fence = new ApplicationPublicationSaveFence();
            publisher = new LiveApplicationStatePublisher(4, fence);
            reporter = new UnityApplicationLifecycleReporter(true, false, 8);
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                reporter, publisher, fence, policy ?? new DeferredCheckpointPolicy(), 8,
                out ApplicationPlatformLifecycleBridge bridge), Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            return new ApplicationUnityBootComposition(
                publisher, fence, bridge, reporter, runFactory,
                new ApplicationUnityStartupIntent(ApplicationUnityStartupIntentKind.ColdBoot), 4);
        }

        private sealed class QueueSurface : IApplicationUnityStartupSurface
        {
            public ApplicationUnityStartupSurfaceRequest Request;
            public bool TryDequeueRequest(out ApplicationUnityStartupSurfaceRequest request)
            {
                request = Request;
                Request = default;
                return request.IsValid;
            }
            public void Apply(in ApplicationUnityStartupSurfaceModel model) { }
        }

        private sealed class DeferredCheckpointPolicy : IApplicationLifecycleCheckpointPolicy
        {
            public ApplicationLifecycleCheckpointBeginResult TryBegin(LiveApplicationState exactState, in ApplicationLifecycleCheckpointEvidence evidence) =>
                ApplicationLifecycleCheckpointBeginResult.Deferred;
        }

        private sealed class StartingCheckpointPolicy : IApplicationLifecycleCheckpointPolicy
        {
            private readonly IApplicationLifecycleCheckpointOperation _operation;
            public StartingCheckpointPolicy(IApplicationLifecycleCheckpointOperation operation) => _operation = operation;
            public int BeginCount { get; private set; }
            public ApplicationLifecycleCheckpointBeginResult TryBegin(LiveApplicationState exactState, in ApplicationLifecycleCheckpointEvidence evidence)
            {
                BeginCount++;
                return ApplicationLifecycleCheckpointBeginResult.Started(_operation);
            }
        }

        private sealed class PendingCheckpointOperation : IApplicationLifecycleCheckpointOperation
        {
            public ApplicationLifecycleCheckpointOperationStatus StatusValue = ApplicationLifecycleCheckpointOperationStatus.Pending;
            public void Poll() { }
            public ApplicationLifecycleCheckpointOperationStatus Status => StatusValue;
            public bool CanCancel => true;
            public bool RequestCancel() => false;
        }

        private class PendingRunFactory : IApplicationUnityStartupRunFactory
        {
            public int CreateCount { get; private set; }
            public virtual ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher)
            {
                CreateCount++;
                return ApplicationUnityStartupRunCreateResult.Succeeded(CreatePendingCoordinator(publisher));
            }
        }

        private sealed class UnavailableThenPendingRunFactory : PendingRunFactory
        {
            public readonly string UnavailableDetail = "temporarily-unavailable";
            public int FirstAttempt { get; private set; }
            public int SecondAttempt { get; private set; }
            public new int CreateCount { get; private set; }
            public override ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher)
            {
                CreateCount++;
                if (CreateCount == 1) FirstAttempt = (int)attempt;
                else if (CreateCount == 2) SecondAttempt = (int)attempt;
                return CreateCount == 1
                    ? new ApplicationUnityStartupRunCreateResult(ApplicationUnityStartupRunCreateStatus.Unavailable, null, UnavailableDetail)
                    : base.TryCreate(in intent, attempt, publisher);
            }
        }

        private sealed class UnavailableThenThrowingRunFactory : IApplicationUnityStartupRunFactory
        {
            public int CreateCount { get; private set; }
            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher)
            {
                CreateCount++;
                if (CreateCount == 1)
                    return new ApplicationUnityStartupRunCreateResult(ApplicationUnityStartupRunCreateStatus.Unavailable, null, "temporarily-unavailable");
                throw new InvalidOperationException("expected retry failure");
            }
        }

        private sealed class ImmediateFailedRunFactory : IApplicationUnityStartupRunFactory
        {
            public int CreateCount { get; private set; }
            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher)
            {
                CreateCount++;
                return ApplicationUnityStartupRunCreateResult.Succeeded(CreateCoordinator(publisher, new ThrowingTransactionFactory()));
            }
        }

        private sealed class RejectedRunFactory : IApplicationUnityStartupRunFactory
        {
            public readonly string Detail = "run-config-rejected";
            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher) =>
                new ApplicationUnityStartupRunCreateResult(
                    ApplicationUnityStartupRunCreateStatus.Failed, null, Detail);
        }

        private static ApplicationStartupCoordinator CreatePendingCoordinator(LiveApplicationStatePublisher publisher)
            => CreateCoordinator(publisher, new TransactionFactory());

        private static ApplicationStartupCoordinator CreateCoordinator(
            LiveApplicationStatePublisher publisher,
            IApplicationStartupTransactionFactory transactionFactory)
        {
            var id = new ApplicationStartupStepId("pending");
            var step = new PendingStep(id);
            Assert.That(ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("unity-test"),
                new[] { new ApplicationStartupStepDescriptor(id, null, 0, 1f) },
                new IApplicationStartupStep[] { step }, transactionFactory,
                out ApplicationStartupPlan plan, out _), Is.True);
            return new ApplicationStartupCoordinator(plan, publisher);
        }

        private sealed class PendingStep : IApplicationStartupStep
        {
            public PendingStep(ApplicationStartupStepId id) => StepId = id;
            public ApplicationStartupStepId StepId { get; }
            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context) => new PendingOperation();
            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context) => new SucceededOperation();
        }

        private sealed class PendingOperation : IApplicationStartupStepOperation
        {
            public string OperationId => "pending";
            public RuntimeOperationStatus Status => RuntimeOperationStatus.Running;
            public float Progress => 0f;
            public RuntimeOperationError Error => default;
            public bool CanCancel => false;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;
            public void Tick(in ApplicationStartupTickContext context) { }
            public void RequestCancel() { }
        }

        private sealed class SucceededOperation : IApplicationStartupStepOperation
        {
            public string OperationId => "succeeded";
            public RuntimeOperationStatus Status => RuntimeOperationStatus.Succeeded;
            public float Progress => 1f;
            public RuntimeOperationError Error => default;
            public bool CanCancel => false;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;
            public void Tick(in ApplicationStartupTickContext context) { }
            public void RequestCancel() { }
        }

        private sealed class TransactionFactory : IApplicationStartupTransactionFactory
        {
            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId) => new Transaction();
        }

        private sealed class ThrowingTransactionFactory : IApplicationStartupTransactionFactory
        {
            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId) =>
                throw new InvalidOperationException("expected factory failure");
        }

        private sealed class Transaction : IApplicationStartupTransaction
        {
            public string TransactionId => "tx";
            public ApplicationStartupCommitObservation CommitObservation => ApplicationStartupCommitObservation.NotPublished;
            public ApplicationStartupCommitResult TryCommit(LiveApplicationState expected, ILiveApplicationStatePublisher publisher) => default;
            public ApplicationStartupRollbackResult FinalizeRollback() => ApplicationStartupRollbackResult.Succeeded;
            public void Dispose() { }
        }

        private sealed class DisposableLease : IApplicationLiveSessionLease
        {
            public DisposableLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public void Dispose() { }
        }

        private sealed class PumpCheckpointLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump, IApplicationLiveCheckpointCapability
        {
            public PumpCheckpointLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public int TickCount { get; private set; }
            public void Dispose() { }
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) => TickCount++;
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) => ApplicationLiveRuntimeAttemptStatus.Succeeded;
            public bool CanReplace(IApplicationLiveCheckpointCapability currentCapability) => currentCapability == null || currentCapability is PumpCheckpointLease;
        }

        private sealed class FailingRetirementLease : IApplicationLiveSessionLease, IApplicationLiveRetirementSource
        {
            public FailingRetirementLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public void Dispose() { }
            public IApplicationLiveRetirementOperation BeginRetirement(in ApplicationLiveRetirementContext context) => new FailedRetirementOperation();
        }

        private sealed class FailedRetirementOperation : IApplicationLiveRetirementOperation
        {
            public string OperationId => "failed-retirement";
            public RuntimeOperationStatus Status => RuntimeOperationStatus.Failed;
            public float Progress => 0f;
            public RuntimeOperationError Error => new RuntimeOperationError("cleanup.failed", "cleanup failed");
            public int WarningCount => 0;
            public ApplicationLiveRetirementWarning GetWarning(int index) => throw new ArgumentOutOfRangeException(nameof(index));
            public ApplicationLiveRetirementOwnershipDebt OwnershipDebt =>
                new ApplicationLiveRetirementOwnershipDebt("test", "cleanup.failed", "cleanup failed", null);
            public void Tick(in ApplicationLiveRetirementContext context) { }
        }
    }

    internal sealed class RejectedCompositionFactoryAdapter : MonoBehaviour, IApplicationUnityBootCompositionFactory
    {
        public int CreateCount { get; private set; }
        public ApplicationUnityBootCompositionCreateResult TryCreate()
        {
            CreateCount++;
            return new ApplicationUnityBootCompositionCreateResult(
                ApplicationUnityBootCompositionCreateStatus.Failed, null, "expected-failure");
        }
    }

    internal sealed class PublishedRuntimeCompositionFactoryAdapter : MonoBehaviour, IApplicationUnityBootCompositionFactory
    {
        public int CreateCount { get; private set; }
        public CountingRuntimeLease Lease { get; private set; }
        public ApplicationUnityBootCompositionCreateResult TryCreate()
        {
            CreateCount++;
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(4, fence);
            var reporter = new UnityApplicationLifecycleReporter(true, false, 8);
            ApplicationPlatformLifecycleBridge.TryCreate(
                reporter, publisher, fence, new AlwaysDeferredPolicy(), 8,
                out ApplicationPlatformLifecycleBridge bridge);
            Lease = new CountingRuntimeLease("root-live");
            publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(true, 1, Lease));
            var composition = new ApplicationUnityBootComposition(
                publisher, fence, bridge, reporter, new AlwaysUnavailableRunFactory(),
                new ApplicationUnityStartupIntent(ApplicationUnityStartupIntentKind.ColdBoot), 4);
            return ApplicationUnityBootCompositionCreateResult.Succeeded(composition);
        }

        private sealed class AlwaysDeferredPolicy : IApplicationLifecycleCheckpointPolicy
        {
            public ApplicationLifecycleCheckpointBeginResult TryBegin(LiveApplicationState exactState, in ApplicationLifecycleCheckpointEvidence evidence) =>
                ApplicationLifecycleCheckpointBeginResult.Deferred;
        }

        private sealed class AlwaysUnavailableRunFactory : IApplicationUnityStartupRunFactory
        {
            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher) =>
                new ApplicationUnityStartupRunCreateResult(ApplicationUnityStartupRunCreateStatus.Unavailable, null, "unavailable");
        }
    }

    internal sealed class BootCompositionFactoryAdapter : MonoBehaviour, IApplicationUnityBootCompositionFactory
    {
        public int CreateCount { get; private set; }
        public ApplicationUnityBootCompositionCreateResult TryCreate()
        {
            CreateCount++;
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(4, fence);
            var reporter = new UnityApplicationLifecycleReporter(true, false, 8);
            ApplicationPlatformLifecycleBridge.TryCreate(
                reporter, publisher, fence, new AlwaysDeferredPolicy(), 8,
                out ApplicationPlatformLifecycleBridge bridge);
            var composition = new ApplicationUnityBootComposition(
                publisher, fence, bridge, reporter, new AlwaysUnavailableRunFactory(),
                new ApplicationUnityStartupIntent(ApplicationUnityStartupIntentKind.ColdBoot), 4);
            return ApplicationUnityBootCompositionCreateResult.Succeeded(composition);
        }

        private sealed class AlwaysDeferredPolicy : IApplicationLifecycleCheckpointPolicy
        {
            public ApplicationLifecycleCheckpointBeginResult TryBegin(LiveApplicationState exactState, in ApplicationLifecycleCheckpointEvidence evidence) =>
                ApplicationLifecycleCheckpointBeginResult.Deferred;
        }

        private sealed class AlwaysUnavailableRunFactory : IApplicationUnityStartupRunFactory
        {
            public ApplicationUnityStartupRunCreateResult TryCreate(
                in ApplicationUnityStartupIntent intent, long attempt, LiveApplicationStatePublisher publisher) =>
                new ApplicationUnityStartupRunCreateResult(ApplicationUnityStartupRunCreateStatus.Unavailable, null, "unavailable");
        }
    }

    internal sealed class CountingRuntimeLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump
    {
        public CountingRuntimeLease(string sessionId) => SessionId = sessionId;
        public string SessionId { get; }
        public int TickCount { get; private set; }
        public void Dispose() { }
        public void Tick(long frameIndex, double deltaTime, double elapsedTime) => TickCount++;
        public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) => ApplicationLiveRuntimeAttemptStatus.Succeeded;
    }
}
