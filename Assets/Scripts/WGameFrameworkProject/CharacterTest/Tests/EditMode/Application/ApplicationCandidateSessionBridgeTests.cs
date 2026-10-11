using System;
using System.Collections.Generic;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationCandidateSessionBridgeTests
    {
        private static readonly ApplicationStartupTickContext TickContext =
            new ApplicationStartupTickContext(0.016, 0.016);

        [Test]
        public void Factory_CreateTwice_UsesFreshGraphAndComponentReferences()
        {
            var builder = new FreshBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);

            ApplicationCandidateSessionGraph first = Create(factory, out TestTransaction firstTransaction);
            ApplicationCandidateSessionGraph second = Create(factory, out TestTransaction secondTransaction);

            Assert.AreNotSame(first.Token, second.Token);
            Assert.AreNotSame(first.RuntimeHost, second.RuntimeHost);
            Assert.AreNotSame(first.AppFlowLease.Controller, second.AppFlowLease.Controller);
            Assert.AreNotSame(first.ResourceLease.Manager, second.ResourceLease.Manager);
            Assert.AreNotSame(first.ResourceLease, second.ResourceLease);
            Assert.AreNotSame(first.ResourceLease.ConfigurationIdentity, second.ResourceLease.ConfigurationIdentity);
            Assert.AreNotSame(first.ResourceLease.Plan, second.ResourceLease.Plan);
            Assert.AreNotSame(builder.Lifetimes[0], builder.Lifetimes[1]);

            IApplicationStartupStepOperation firstForward =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource-a")).Begin(Forward(firstTransaction));
            IApplicationStartupStepOperation secondForward =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource-b")).Begin(Forward(secondTransaction));
            Assert.AreNotSame(firstForward, secondForward);
        }

        [Test]
        public void Factory_ReusedComponent_RejectsWithoutAbandoningPriorGraph()
        {
            var builder = new ReusingBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);
            ApplicationCandidateSessionGraph first = factory.Create(new TestTransaction());

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(ApplicationCandidatePumpOwnership.StartupOwner, first.PumpOwnership);
            Assert.AreEqual(1, builder.SecondLease.AbandonCount);
        }

        [Test]
        public void Factory_ReusedPlanWithOtherwiseFreshGraph_Rejects()
        {
            var builder = new ReusedPlanBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);
            ApplicationCandidateSessionGraph first = factory.Create(new TestTransaction());

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(ApplicationCandidatePumpOwnership.StartupOwner, first.PumpOwnership);
            Assert.IsFalse(builder.FirstLifetime.IsReleased);
            Assert.IsTrue(builder.SecondLifetime.IsReleased);
            Assert.AreEqual(1, builder.SecondLease.AbandonCount);
        }

        [Test]
        public void Factory_InvalidPumpOwnership_AbandonsInertBuildBeforeSideEffects()
        {
            var builder = new InvalidStandaloneBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(1, builder.LastLease.AbandonCount);
            Assert.AreEqual(0, builder.Lifecycle.PrepareCount);
            Assert.IsTrue(builder.Lifetime.IsReleased);
        }

        [Test]
        public void Factory_RuntimeModuleWrapperMismatch_RejectsBeforePrepareStart()
        {
            var builder = new InvalidStandaloneBuilder(runtimeWrapperMismatch: true);
            var factory = new ApplicationCandidateSessionFactory(builder);

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(1, builder.LastLease.AbandonCount);
            Assert.AreEqual(0, builder.Lifecycle.PrepareCount);
        }

        [TestCase("host")]
        [TestCase("app-flow")]
        public void Factory_PollutedFreshReference_RejectsAndAbandonsBeforeResourcePrepare(string target)
        {
            var builder = new PollutedBuilder(target);
            var factory = new ApplicationCandidateSessionFactory(builder);

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(1, builder.Lease.AbandonCount);
            Assert.IsTrue(builder.Lifetime.IsReleased);
            Assert.AreEqual(0, builder.Lifecycle.PrepareCount);
        }

        [Test]
        public void Factory_SameOwnerRepeatedCreate_RejectsBeforeSecondBuildWithGraphAlreadyClaimed()
        {
            var builder = new FreshBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);
            var owner = new TestTransaction();
            ApplicationCandidateSessionGraph first = factory.Create(owner);

            var exception = Assert.Throws<ApplicationCandidateSessionConstructionException>(() => factory.Create(owner));

            Assert.AreEqual(ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed, exception.Code);
            Assert.AreEqual(1, builder.BuildCount);
            Assert.AreSame(first, owner.CandidateSessionClaim.CandidateGraph);
        }

        [Test]
        public void Factory_FailedBuildLeavesOwnerClaimPoisonedAndRejectsBeforeSecondBuild()
        {
            var builder = new InvalidStandaloneBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);
            var owner = new TestTransaction();
            Assert.Throws<InvalidOperationException>(() => factory.Create(owner));

            var exception = Assert.Throws<ApplicationCandidateSessionConstructionException>(() => factory.Create(owner));

            Assert.AreEqual(ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed, exception.Code);
            Assert.AreEqual(1, builder.BuildCount);
            Assert.IsTrue(owner.CandidateSessionClaim.IsClaimed);
            Assert.IsNull(owner.CandidateSessionClaim.CandidateGraph);
        }

        [Test]
        public void Factory_SharedClaimCannotBeUsedByForeignTransaction()
        {
            var builder = new FreshBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);
            var claim = new ApplicationCandidateSessionOwnerClaim();
            var owner = new TestTransaction(claim);
            var foreign = new TestTransaction(claim);
            factory.Create(owner);

            var exception = Assert.Throws<ApplicationCandidateSessionConstructionException>(() => factory.Create(foreign));

            Assert.AreEqual(ApplicationCandidateSessionErrorCode.GraphAlreadyClaimed, exception.Code);
            Assert.AreEqual(1, builder.BuildCount);
        }

        [Test]
        public void Factory_CrossRoleAlias_RejectsBeforeTransferAndAbandons()
        {
            var builder = new AliasedOwnerBuilder();
            var factory = new ApplicationCandidateSessionFactory(builder);

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(0, builder.Lease.TransferCount);
            Assert.AreEqual(1, builder.Lease.AbandonCount);
            Assert.IsTrue(builder.Owner.Disposed);
        }

        [Test]
        public void Steps_SuccessFreezeAndRollback_NoTickAndExactOwnership()
        {
            var builder = new FreshBuilder(ApplicationControllerPumpStrategy.RuntimeModule);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            var resourceStep = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            var runtimeStep = new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime"));
            var appFlowStep = new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow"));
            var handoffStep = new ApplicationCandidateHandoffStep(new ApplicationStartupStepId("handoff"));

            AssertSucceeded(resourceStep.Begin(Forward(transaction)));
            AssertSucceeded(runtimeStep.Begin(Forward(transaction)));
            AssertSucceeded(appFlowStep.Begin(Forward(transaction)));
            AssertSucceeded(handoffStep.Begin(Forward(transaction)));

            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffFrozen, graph.PumpOwnership);
            Assert.IsTrue(graph.TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease firstLease));
            Assert.IsTrue(graph.TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease secondLease));
            Assert.AreSame(firstLease, secondLease);
            Assert.IsTrue(firstLease.IsValid);
            Assert.AreEqual(RuntimeLifecycleState.Started, graph.RuntimeHost.State);
            Assert.IsFalse(graph.RuntimeHost.HasTickAttempt);
            Assert.AreEqual(0, graph.RuntimeHost.TickCount);
            Assert.AreEqual(1, builder.LastLifecycle.FreezeCount);

            IApplicationStartupStepOperation frozenForward = appFlowStep.Begin(Forward(transaction));
            Pump(frozenForward);
            Assert.AreEqual(RuntimeOperationStatus.Failed, frozenForward.Status);
            IApplicationStartupStepOperation frozenResource = resourceStep.Begin(Forward(transaction));
            IApplicationStartupStepOperation frozenRuntime = runtimeStep.Begin(Forward(transaction));
            Pump(frozenResource);
            Pump(frozenRuntime);
            Assert.AreEqual(RuntimeOperationStatus.Failed, frozenResource.Status);
            Assert.AreEqual(RuntimeOperationStatus.Failed, frozenRuntime.Status);

            AssertSucceeded(handoffStep.BeginRollback(Rollback(transaction)));
            AssertSucceeded(appFlowStep.BeginRollback(Rollback(transaction)));
            AssertSucceeded(runtimeStep.BeginRollback(Rollback(transaction)));
            AssertSucceeded(resourceStep.BeginRollback(Rollback(transaction)));

            Assert.AreEqual(ApplicationCandidatePumpOwnership.Disposed, graph.PumpOwnership);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
            Assert.AreEqual(1, builder.LastLifecycle.RollbackCount);
        }

        [Test]
        public void AppFlowPrepareFailure_CompensatesBeforeForwardReportsFailure()
        {
            var builder = new FreshBuilder(appFlowPrepareStatus: RuntimeOperationStatus.Failed);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(1, builder.LastLifecycle.PrepareCount);
            Assert.AreEqual(1, builder.LastLifecycle.RollbackCount);
            Assert.AreEqual(0, builder.LastLifecycle.SideEffectCount);
            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed, detail.Code);
            Assert.IsFalse(detail.HasCleanupDebt);
        }

        [Test]
        public void AppFlowMissingCompensation_RejectsBeforePrepareStart()
        {
            var builder = new FreshBuilder(canCompensate: false);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(0, builder.LastLifecycle.PrepareCount);
            Assert.AreEqual(0, builder.LastLifecycle.SideEffectCount);
        }

        [Test]
        public void RuntimeCancelBeforeFirstTick_DisposesHostBeforeCancelledTerminal()
        {
            var builder = new FreshBuilder();
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction));

            operation.RequestCancel();
            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Cancelled, operation.Status);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, graph.RuntimeHost.State);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RuntimeLifecycleFault_DisposesPoisonedHostAfterReverseUnwind(bool initializeFault)
        {
            var builder = new FreshBuilder(runtimeFault: initializeFault ? "initialize" : "start");
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, graph.RuntimeHost.State);
            Assert.AreEqual(
                initializeFault
                    ? ApplicationCandidateSessionErrorCode.RuntimeInitializeFailed
                    : ApplicationCandidateSessionErrorCode.RuntimeStartFailed,
                ((ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail).Code);
            int secondCleanup = builder.FaultLog.IndexOf(initializeFault ? "dispose:b" : "stop:b");
            int firstCleanup = builder.FaultLog.IndexOf(initializeFault ? "dispose:a" : "stop:a");
            Assert.GreaterOrEqual(secondCleanup, 0);
            Assert.Greater(firstCleanup, secondCleanup);
        }

        [TestCase(RuntimeHostErrorPolicy.FailFast, "stop")]
        [TestCase(RuntimeHostErrorPolicy.FailFast, "dispose")]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, "stop")]
        [TestCase(RuntimeHostErrorPolicy.CollectAndStopFrame, "dispose")]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, "stop")]
        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue, "dispose")]
        public void RuntimeRollback_CleanupLedgerFault_IsDebtEvenWhenHostIsDisposed(
            RuntimeHostErrorPolicy policy,
            string fault)
        {
            var builder = new FreshBuilder(runtimeFault: fault, runtimePolicy: policy);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));

            IApplicationStartupStepOperation rollback =
                new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime-rb")).BeginRollback(Rollback(transaction));
            Pump(rollback);

            Assert.AreEqual(RuntimeOperationStatus.Failed, rollback.Status);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, graph.RuntimeHost.State);
            var detail = (ApplicationCandidateSessionErrorDetail)rollback.InnerErrorDetail;
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.RuntimeCleanupFailed, detail.Code);
            Assert.IsTrue((detail.CleanupDebts & ApplicationCandidateCleanupDebt.RuntimeHost) != 0);
        }

        [TestCase("throw")]
        [TestCase("null")]
        public void AppFlowRollbackCreationFailure_IsTerminalWithCleanupDebt(string mode)
        {
            var builder = new FreshBuilder(appFlowPrepareStatus: RuntimeOperationStatus.Failed, rollbackMode: mode);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.AppFlowPrepareFailed, detail.Code);
            Assert.IsTrue((detail.CleanupDebts & ApplicationCandidateCleanupDebt.AppFlow) != 0);
        }

        [Test]
        public void AppFlowLateCancel_DrainsForwardThenRollbackBeforeCancelledTerminal()
        {
            var builder = new FreshBuilder(prepareTicks: 2, rollbackTicks: 2, cancelCompletes: false);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));

            operation.Tick(in TickContext);
            operation.RequestCancel();
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, builder.LastLifecycle.SideEffectCount);
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Cancelled, operation.Status);
            Assert.AreEqual(0, builder.LastLifecycle.SideEffectCount);
            Assert.AreEqual(1, builder.LastLifecycle.RollbackCount);
        }

        [Test]
        public void AppFlowForwardTickThrow_RequestDrainThrow_StillDrainsSameHandleBeforeRollback()
        {
            var builder = new FreshBuilder(
                prepareTickThrows: true,
                faultDrainRequestThrows: true,
                faultDrainTickThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            CollectionAssert.IsSubsetOf(
                new[] { "prepare:tick-throw", "prepare:request-drain", "prepare:tick-drain", "rollback:begin" },
                builder.LastLifecycle.NestedLog);
            Assert.Less(builder.LastLifecycle.NestedLog.IndexOf("prepare:tick-drain"),
                builder.LastLifecycle.NestedLog.IndexOf("rollback:begin"));
            Assert.AreEqual(ApplicationCandidateAppFlowLeaseStatus.RolledBack, graph.AppFlowStatus);
            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(3, ((AggregateException)detail.Exception).Flatten().InnerExceptions.Count);
        }

        [Test]
        public void AppFlowRollbackTickThrow_DrainsSameHandleBeforeTerminalSuccess()
        {
            var builder = new FreshBuilder(rollbackTickThrows: true, faultDrainRequestThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));

            IApplicationStartupStepOperation rollback =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow-rb")).BeginRollback(Rollback(transaction));
            Pump(rollback);

            Assert.AreEqual(RuntimeOperationStatus.Succeeded, rollback.Status);
            CollectionAssert.Contains(builder.LastLifecycle.NestedLog, "rollback:tick-drain");
            Assert.AreEqual(ApplicationCandidateAppFlowLeaseStatus.RolledBack, graph.AppFlowStatus);
        }

        [Test]
        public void FreezeFailure_TransfersToRollbackOwnerAndNeverPublishes()
        {
            var builder = new FreshBuilder(freezeThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation handoff =
                new ApplicationCandidateHandoffStep(new ApplicationStartupStepId("handoff")).Begin(Forward(transaction));

            Pump(handoff);

            Assert.AreEqual(RuntimeOperationStatus.Failed, handoff.Status);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.RollbackOwner, graph.PumpOwnership);
            Assert.AreEqual(0, graph.RuntimeHost.TickCount);
        }

        [Test]
        public void FrozenHandoff_InternalClaim_IsExactOnceAndBlocksRollback()
        {
            ApplicationCandidateSessionGraph graph = CreateFrozenGraph(out TestTransaction transaction);
            Assert.IsTrue(graph.TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease lease));
            var owner = new ApplicationCandidateFrozenHandoffOwner();
            var foreign = new ApplicationCandidateFrozenHandoffOwner();

            Assert.IsTrue(lease.TryClaim(owner));
            Assert.IsFalse(lease.TryClaim(owner));
            Assert.IsFalse(lease.TryClaim(foreign));
            Assert.IsTrue(lease.IsClaimed);
            Assert.IsFalse(lease.IsValid);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.PumpOwnership);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict,
                graph.BeginRollbackOwnership());
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.PumpOwnership);
        }

        [Test]
        public void FrozenHandoff_InternalClaim_RaceHasOneWinner()
        {
            ApplicationCandidateSessionGraph graph = CreateFrozenGraph(out _);
            Assert.IsTrue(graph.TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease lease));
            bool first = false;
            bool second = false;
            System.Threading.Tasks.Parallel.Invoke(
                () => first = lease.TryClaim(new ApplicationCandidateFrozenHandoffOwner()),
                () => second = lease.TryClaim(new ApplicationCandidateFrozenHandoffOwner()));

            Assert.AreEqual(1, (first ? 1 : 0) + (second ? 1 : 0));
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.PumpOwnership);
        }

        [Test]
        public void ResourcePrepareException_CurrentOperationReleasesGraphBeforeFailure()
        {
            var builder = new FreshBuilder(resourcePrepareThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.IsTrue(graph.ResourceLease.IsReleased);
            Assert.AreEqual(RuntimeLifecycleState.Disposed, graph.RuntimeHost.State);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.Disposed, graph.PumpOwnership);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
        }

        [Test]
        public void ResourceForwardAndCleanupFaults_PreservePrimaryAndCleanupSlots()
        {
            var builder = new FreshBuilder(resourcePollAndCancelThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));

            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.SessionCleanupFailed, detail.Code);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.ResourcePreparationFailed, detail.ForwardCode);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.ResourceCleanupFailed, detail.CleanupCode);
            StringAssert.Contains("poll", detail.Exception.Message.ToLowerInvariant());
            Assert.IsNotNull(detail.CleanupException);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.CleanupFailed, graph.ResourceStatus);
        }

        [Test]
        public void ResourceCancelException_ReportsPlanDebtWithoutClaimingReleased()
        {
            var builder = new FreshBuilder(resourceCancelThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));

            operation.Tick(in TickContext);
            operation.RequestCancel();
            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.CleanupFailed, graph.ResourceStatus);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.RollbackOwner, graph.PumpOwnership);
            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            Assert.IsTrue((detail.CleanupDebts & ApplicationCandidateCleanupDebt.ResourcePlan) != 0);
            Assert.IsNotNull(detail.CleanupException);
            Assert.GreaterOrEqual(((AggregateException)detail.CleanupException).Flatten().InnerExceptions.Count, 2);
            Assert.AreEqual(1, builder.LastLifecycle.DisposeCount);

            IApplicationStartupStepOperation repeated =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource-rb")).BeginRollback(Rollback(transaction));
            Pump(repeated);
            Assert.AreEqual(RuntimeOperationStatus.Failed, repeated.Status);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.CleanupFailed, graph.ResourceStatus);
            Assert.AreEqual(1, builder.LastLifecycle.DisposeCount);
        }

        [Test]
        public void FinalizeRollback_PreservesAppFlowAndResourceGraphDebts()
        {
            var builder = new FreshBuilder(failResourceGraphCleanup: true, disposeThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            var resourceStep = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            AssertSucceeded(resourceStep.Begin(Forward(transaction)));
            graph.RuntimeHost.Dispose();
            graph.BeginRollbackOwnership();
            IApplicationStartupStepOperation rollback = resourceStep.BeginRollback(Rollback(transaction));
            Pump(rollback);

            ApplicationCandidateSessionCleanupResult result = graph.FinalizeRollback();
            var operationDetail = (ApplicationCandidateSessionErrorDetail)rollback.InnerErrorDetail;
            Assert.IsFalse(result.Success);
            Assert.IsTrue((result.Debts & ApplicationCandidateCleanupDebt.AppFlow) != 0);
            Assert.IsTrue((result.Debts & ApplicationCandidateCleanupDebt.ResourceGraph) != 0);
            Assert.AreEqual(result.Debts, operationDetail.CleanupDebts);
            Assert.AreEqual("APP_FLOW_DISPOSE_FAILED", result.FirstErrorCode);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.RollbackOwner, graph.PumpOwnership);
        }

        [TestCase(true, false, ApplicationCandidateCleanupDebt.AppFlow)]
        [TestCase(false, true, ApplicationCandidateCleanupDebt.ResourceGraph)]
        public void FinalizeRollback_ReportsExactSingleCleanupDebt(
            bool appFlowDisposeThrows,
            bool resourceGraphFails,
            ApplicationCandidateCleanupDebt expectedDebt)
        {
            var builder = new FreshBuilder(
                failResourceGraphCleanup: resourceGraphFails,
                disposeThrows: appFlowDisposeThrows);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            var resourceStep = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            AssertSucceeded(resourceStep.Begin(Forward(transaction)));
            graph.DisposeRuntimeHost();
            graph.BeginRollbackOwnership();
            IApplicationStartupStepOperation rollback = resourceStep.BeginRollback(Rollback(transaction));
            Pump(rollback);

            var detail = (ApplicationCandidateSessionErrorDetail)rollback.InnerErrorDetail;
            Assert.AreEqual(RuntimeOperationStatus.Failed, rollback.Status);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.SessionCleanupFailed, detail.Code);
            Assert.AreEqual(ApplicationCandidateSessionPhase.SessionCleanup, detail.Phase);
            Assert.AreEqual(expectedDebt, detail.CleanupDebts);
        }

        [Test]
        public void FinalizeRollback_AppFlowAndResourceGraphThrow_PreservesBothExceptions()
        {
            var builder = new FreshBuilder(disposeThrows: true, resourceGraphThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            var resourceStep = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            AssertSucceeded(resourceStep.Begin(Forward(transaction)));
            graph.DisposeRuntimeHost();
            graph.BeginRollbackOwnership();
            IApplicationStartupStepOperation rollback = resourceStep.BeginRollback(Rollback(transaction));
            Pump(rollback);

            var detail = (ApplicationCandidateSessionErrorDetail)rollback.InnerErrorDetail;
            var aggregate = detail.CleanupException as AggregateException;
            Assert.IsNotNull(aggregate);
            Assert.AreEqual(2, aggregate.Flatten().InnerExceptions.Count);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.AppFlow | ApplicationCandidateCleanupDebt.ResourceGraph,
                detail.CleanupDebts);
        }

        [Test]
        public void FinalizeRollback_AppFlowAndResourceLeaseThrow_MergesAllCleanupExceptions()
        {
            var builder = new FreshBuilder(resourceCancelThrows: true, disposeThrows: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));
            operation.Tick(in TickContext);
            operation.RequestCancel();
            Pump(operation);

            var detail = (ApplicationCandidateSessionErrorDetail)operation.InnerErrorDetail;
            var aggregate = detail.CleanupException as AggregateException;
            Assert.IsNotNull(aggregate);
            Assert.GreaterOrEqual(aggregate.Flatten().InnerExceptions.Count, 3);
            Assert.IsTrue((detail.CleanupDebts & ApplicationCandidateCleanupDebt.AppFlow) != 0);
            Assert.IsTrue((detail.CleanupDebts & ApplicationCandidateCleanupDebt.ResourcePlan) != 0);
        }

        [Test]
        public void ForeignTransaction_IsRejectedBeforeResourcePreparation()
        {
            var builder = new FreshBuilder();
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction owner);
            var foreign = new TestTransaction(graph);
            var step = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            IApplicationStartupStepOperation first = step.Begin(Forward(owner));
            IApplicationStartupStepOperation rejected = step.Begin(Forward(foreign));

            Pump(rejected);

            Assert.AreEqual(RuntimeOperationStatus.Failed, rejected.Status);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.Created, graph.ResourceLease.Status);
            Assert.AreEqual(0, builder.LastLifecycle.PrepareCount);
            Assert.AreEqual(RuntimeOperationStatus.Running, first.Status);
        }

        [Test]
        public void ResourceCleanupFailure_DoesNotClaimReleasedOrDisposeGraph()
        {
            var builder = new FreshBuilder(failResourceGraphCleanup: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            var resourceStep = new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource"));
            AssertSucceeded(resourceStep.Begin(Forward(transaction)));
            graph.RuntimeHost.Dispose();
            graph.BeginRollbackOwnership();
            IApplicationStartupStepOperation rollback = resourceStep.BeginRollback(Rollback(transaction));

            Pump(rollback);

            Assert.AreEqual(RuntimeOperationStatus.Failed, rollback.Status);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.RollbackOwner, graph.PumpOwnership);
            Assert.IsTrue(graph.ResourceLease.IsReleased);
            Assert.IsFalse(builder.LastLifetime.IsReleased);
            Assert.IsFalse(graph.FinalizeRollback().Success);
        }

        [Test]
        public void WarmedStatePolling_AllocatesZeroBytes()
        {
            var builder = new FreshBuilder(resourcePending: true);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));
            operation.Tick(in TickContext);
            _ = graph.PumpOwnership;
            _ = graph.ResourceLease.Status;
            _ = graph.ResourceLease.Progress;
            _ = operation.Status;
            _ = operation.Progress;
            _ = operation.Error;
            _ = operation.InnerErrorDetail;
            _ = graph.RuntimeHost.HasExactAppFlowPumpRegistration(graph.AppFlowLease.Controller, null);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                operation.Tick(in TickContext);
                _ = graph.PumpOwnership;
                _ = graph.IsReadyForHandoff;
                _ = graph.ResourceLease.Status;
                _ = graph.ResourceLease.Progress;
                _ = graph.ResourceLease.ErrorCode;
                _ = operation.Status;
                _ = operation.Progress;
                _ = operation.Error;
                _ = operation.InnerErrorDetail;
                _ = graph.RuntimeHost.HasExactAppFlowPumpRegistration(graph.AppFlowLease.Controller, null);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before);
            Assert.GreaterOrEqual(builder.LastPendingResourceOperation.StatusReadCount, 1000);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceLease_AcquiredAndReleasingPolling_AllocatesZeroBytes(bool releasing)
        {
            var builder = new FreshBuilder(resourcePending: releasing);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction));
            operation.Tick(in TickContext);
            if (releasing) operation.RequestCancel();
            else Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.Acquired, graph.ResourceStatus);
            if (releasing) Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.Releasing, graph.ResourceStatus);

            _ = graph.ResourceStatus; _ = graph.ResourceProgress; _ = graph.ResourceErrorCode;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = graph.ResourceStatus;
                _ = graph.ResourceProgress;
                _ = graph.ResourceErrorCode;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before);
        }

        [Test]
        public void AppFlowPendingOperation_SteadyNestedTick_AllocatesZeroBytes()
        {
            var builder = new FreshBuilder(prepareTicks: 2005);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation =
                new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction));
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, builder.LastLifecycle.LastPrepareOperation.TickCount);

            _ = operation.Status; _ = operation.Progress; _ = operation.Error; _ = operation.InnerErrorDetail;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                operation.Tick(in TickContext);
                _ = operation.Status;
                _ = operation.Progress;
                _ = operation.Error;
                _ = operation.InnerErrorDetail;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1001, builder.LastLifecycle.LastPrepareOperation.TickCount);
            Assert.AreEqual(ApplicationCandidateAppFlowLeaseStatus.Preparing, graph.AppFlowStatus);
        }

        [Test]
        public void PublicEnumValues_AreStable()
        {
            Assert.AreEqual(0, (int)ApplicationControllerPumpStrategy.Invalid);
            Assert.AreEqual(1, (int)ApplicationControllerPumpStrategy.RuntimeModule);
            Assert.AreEqual(2, (int)ApplicationControllerPumpStrategy.Standalone);
            Assert.AreEqual(0, (int)ApplicationCandidatePumpOwnership.Invalid);
            Assert.AreEqual(1, (int)ApplicationCandidatePumpOwnership.StartupOwner);
            Assert.AreEqual(2, (int)ApplicationCandidatePumpOwnership.HandoffFrozen);
            Assert.AreEqual(3, (int)ApplicationCandidatePumpOwnership.RollbackOwner);
            Assert.AreEqual(4, (int)ApplicationCandidatePumpOwnership.Disposed);
            Assert.AreEqual(5, (int)ApplicationCandidatePumpOwnership.HandoffClaimed);
            Assert.AreEqual(6, (int)ApplicationCandidatePumpOwnership.LiveOwner);
            Assert.AreEqual(7, (int)ApplicationCandidatePumpOwnership.RetirementOwner);
            Assert.AreEqual(1, (int)ApplicationCandidateCleanupDebt.RuntimeHost);
            Assert.AreEqual(2, (int)ApplicationCandidateCleanupDebt.AppFlow);
            Assert.AreEqual(4, (int)ApplicationCandidateCleanupDebt.ResourcePlan);
            Assert.AreEqual(8, (int)ApplicationCandidateCleanupDebt.ResourceGraph);
            Assert.AreEqual(16, (int)ApplicationCandidateCleanupDebt.Scene);
            Assert.AreEqual(32, (int)ApplicationCandidateCleanupDebt.PumpOwner);
            AssertEnumValues<ApplicationCandidateAppFlowLeaseStatus>(0, 1, 2, 3, 4, 5, 6, 7);
            AssertEnumValues<ApplicationCandidateResourceLeaseStatus>(0, 1, 2, 3, 4, 5, 6);
            AssertEnumValues<ApplicationCandidateSessionPhase>(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            AssertEnumValues<ApplicationCandidateSessionErrorCode>(
                0, 1, 2, 3, 100, 101, 102, 200, 201, 202, 203, 204,
                300, 301, 302, 303, 350, 351, 352, 353, 354,
                400, 401, 402, 403, 500, 600);
            AssertEnumValues<ApplicationCandidateComponentReuse>(
                0, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384);
            AssertEnumValues<ApplicationCandidateSceneLeaseStatus>(0, 1, 2, 3, 4, 5, 6, 7, 8);
            AssertEnumValues<ApplicationCandidateSceneIsolationErrorCode>(0, 1, 2, 3);
        }

        [Test]
        public void PublicGraphSurface_ContainsNoRawOwnerOrCleanupMutator()
        {
            Type graph = typeof(ApplicationCandidateSessionGraph);
            Assert.IsNull(graph.GetProperty("Token"));
            Assert.IsNull(graph.GetProperty("RuntimeHost"));
            Assert.IsNull(graph.GetProperty("AppFlowLease"));
            Assert.IsNull(graph.GetProperty("ResourceLease"));
            Assert.IsNull(graph.GetMethod("FreezeForHandoff"));
            Assert.IsNull(graph.GetMethod("BeginRollbackOwnership"));
            Assert.IsNull(graph.GetMethod("FinalizeRollback"));
            Assert.IsFalse(typeof(IDisposable).IsAssignableFrom(graph));
            Assert.AreEqual(typeof(IApplicationCandidateSessionTransaction),
                typeof(IApplicationCandidateSessionFactory).GetMethod("Create").GetParameters()[0].ParameterType);
        }

        [Test]
        public void Coordinator_EndToEnd_CommitFailureRollsBackExactGraphAndRetryIsFresh()
        {
            var builder = new FreshBuilder(ApplicationControllerPumpStrategy.RuntimeModule);
            var transactionFactory = new CandidateTransactionFactory(builder);
            var resourceId = new ApplicationStartupStepId("resource");
            var runtimeId = new ApplicationStartupStepId("runtime");
            var appFlowId = new ApplicationStartupStepId("app-flow");
            var handoffId = new ApplicationStartupStepId("handoff");
            IApplicationStartupStep[] steps =
            {
                new ApplicationCandidateResourceStep(resourceId),
                new ApplicationCandidateRuntimeLifecycleStep(runtimeId),
                new ApplicationCandidateAppFlowStep(appFlowId),
                new ApplicationCandidateHandoffStep(handoffId)
            };
            ApplicationStartupStepDescriptor[] descriptors =
            {
                new ApplicationStartupStepDescriptor(resourceId, null, 0, 1f),
                new ApplicationStartupStepDescriptor(runtimeId, new[] { resourceId }, 1, 1f),
                new ApplicationStartupStepDescriptor(appFlowId, new[] { runtimeId }, 2, 1f),
                new ApplicationStartupStepDescriptor(handoffId, new[] { appFlowId }, 3, 1f)
            };
            Assert.IsTrue(ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("candidate-e2e"), descriptors, steps, transactionFactory,
                out ApplicationStartupPlan plan, out ApplicationStartupError planError), planError.Message);
            var publisher = new LiveApplicationStatePublisher();
            LiveApplicationState initial = publisher.Current;

            var first = new ApplicationStartupCoordinator(plan, publisher);
            first.Start("first");
            PumpCoordinator(first);
            ApplicationCandidateSessionGraph firstGraph = transactionFactory.Graphs[0];
            Assert.AreEqual(ApplicationStartupState.Failed, first.State);
            Assert.AreSame(initial, publisher.Current);
            Assert.AreEqual(0, publisher.Current.Revision);
            Assert.AreEqual(-1, publisher.Current.NextRuntimeFrame);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.Disposed, firstGraph.PumpOwnership);
            Assert.AreEqual(0, firstGraph.RuntimeHost.TickCount);

            var retry = new ApplicationStartupCoordinator(plan, publisher);
            retry.Start("retry");
            PumpCoordinator(retry);
            Assert.AreEqual(ApplicationStartupState.Failed, retry.State);
            Assert.AreEqual(2, transactionFactory.Graphs.Count);
            Assert.AreNotSame(firstGraph, transactionFactory.Graphs[1]);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.Disposed, transactionFactory.Graphs[1].PumpOwnership);
            Assert.AreSame(initial, publisher.Current);
        }

        [Test]
        public void SceneRequest_RejectsUnsupportedShapeAndIsolationFailureBeforeDriverLoad()
        {
            var driver = new TestSceneFlowDriver();
            var gate = new TestSceneIsolationGate(failEnter: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));

            IApplicationStartupStepOperation unsupported = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene-single"),
                new ApplicationCandidateSceneRequest("candidate", SceneFlowLoadMode.Single)).Begin(Forward(transaction));
            Pump(unsupported);
            Assert.AreEqual(RuntimeOperationStatus.Failed, unsupported.Status);
            Assert.AreEqual(0, driver.LoadCount);

            // Use a fresh generation because a rejected candidate lease is poisoned by design.
            driver = new TestSceneFlowDriver();
            gate = new TestSceneIsolationGate(failEnter: true);
            builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            graph = Create(new ApplicationCandidateSessionFactory(builder), out transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation rejected = SceneForward(transaction, "candidate");
            Pump(rejected);
            Assert.AreEqual(RuntimeOperationStatus.Failed, rejected.Status);
            Assert.AreEqual(0, driver.LoadCount);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
        }

        [Test]
        public void SceneCancelBeforeFirstTick_StartsNoIsolationOrLoad()
        {
            var driver = new TestSceneFlowDriver();
            var gate = new TestSceneIsolationGate();
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");

            operation.RequestCancel();
            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Cancelled, operation.Status);
            Assert.AreEqual(0, gate.EnterCount);
            Assert.AreEqual(0, driver.LoadCount);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
        }

        [Test]
        public void SceneCancel_NonCancellableLoadLateSuccessWaitsForExactUnloadTerminal()
        {
            var load = new TestSceneFlowOperation("candidate", 0.25f);
            var unload = new TestSceneFlowOperation("candidate", 0.5f);
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate();
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");

            operation.Tick(in TickContext);
            operation.RequestCancel();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(0, driver.UnloadCount);

            load.CompleteSuccess();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.IsTrue(gate.IsInert);
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);

            unload.CompleteSuccess();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Cancelled, operation.Status);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            Assert.IsNull(transaction.CandidateScenePort.SceneLease);
        }

        [Test]
        public void SceneRollback_UnloadFailureReportsTypedDebtAndBlocksResourceRelease()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation forward = SceneForward(transaction, "candidate");
            forward.Tick(in TickContext);
            load.CompleteSuccess();
            forward.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, forward.Status);
            Assert.AreSame(graph.SceneLease, transaction.CandidateScenePort.SceneLease);

            IApplicationStartupStepOperation rollback = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"),
                new ApplicationCandidateSceneRequest("candidate")).BeginRollback(Rollback(transaction));
            rollback.Tick(in TickContext);
            Assert.IsTrue(builder.LastSceneGate.IsInert);
            Assert.AreEqual(1, driver.UnloadCount);
            unload.CompleteFailure(new SceneFlowError(SceneFlowErrorCode.OperationFailed, "candidate", "unload failed"));
            rollback.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, rollback.Status);
            var detail = (ApplicationCandidateSceneErrorDetail)rollback.InnerErrorDetail;
            Assert.AreEqual(SceneFlowErrorCode.OperationFailed, detail.CleanupError.Code);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.Scene, detail.CleanupDebt);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.CleanupFailed, graph.SceneStatus);

            IApplicationStartupStepOperation resourceRollback = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).BeginRollback(Rollback(transaction));
            resourceRollback.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Failed, resourceRollback.Status);
            Assert.IsFalse(builder.LastLifetime.IsReleased);
        }

        [Test]
        public void ResourceFailureBeforeScene_ReleasesResourcesWithoutStartingSceneIo()
        {
            var driver = new TestSceneFlowDriver();
            var builder = new FreshBuilder(resourcePrepareThrows: true, withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            IApplicationStartupStepOperation resource = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction));

            Pump(resource);

            Assert.AreEqual(RuntimeOperationStatus.Failed, resource.Status);
            Assert.AreEqual(0, driver.LoadCount);
            Assert.AreEqual(0, driver.UnloadCount);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
        }

        [Test]
        public void SceneRetry_UsesFreshControllerDriverLeaseAndLeavesUnrelatedLiveControllerUntouched()
        {
            var liveDriver = new TestSceneFlowDriver();
            var liveController = new SceneFlowController(liveDriver, "live");
            var builder = new FreshBuilder(withScene: true);
            var factory = new ApplicationCandidateSessionFactory(builder);

            ApplicationCandidateSessionGraph first = Create(factory, out _);
            SceneFlowController firstController = builder.LastSceneController;
            TestSceneFlowDriver firstDriver = builder.LastSceneDriver;
            ApplicationCandidateSceneLease firstLease = first.SceneLease;
            ApplicationCandidateSessionGraph second = Create(factory, out _);

            Assert.AreNotSame(firstController, builder.LastSceneController);
            Assert.AreNotSame(firstDriver, builder.LastSceneDriver);
            Assert.AreNotSame(firstLease, second.SceneLease);
            Assert.AreEqual("live", liveController.ActiveSceneKey);
            Assert.AreEqual(0, liveDriver.LoadCount);
        }

        [Test]
        public void Factory_ReusedActualSceneDriverWithFreshController_RejectsSecondGeneration()
        {
            var sharedDriver = new TestSceneFlowDriver();
            var builder = new FreshBuilder(withScene: true, sceneDriver: sharedDriver);
            var factory = new ApplicationCandidateSessionFactory(builder);
            ApplicationCandidateSessionGraph first = factory.Create(new TestTransaction());

            Assert.Throws<InvalidOperationException>(() => factory.Create(new TestTransaction()));

            Assert.AreEqual(ApplicationCandidatePumpOwnership.StartupOwner, first.PumpOwnership);
            Assert.AreSame(sharedDriver, builder.LastSceneController.Driver);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScenePostFactoryControllerPollution_TerminatesReleasedWithInvalidGraph(bool completePollution)
        {
            var pollution = new TestSceneFlowOperation("pollution");
            var driver = new TestSceneFlowDriver(pollution);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            Assert.AreEqual(SceneFlowResultStatus.Accepted,
                builder.LastSceneController.RequestLoad(new SceneFlowRequest("pollution", SceneFlowLoadMode.Additive)).Status);
            if (completePollution)
            {
                pollution.CompleteSuccess();
                builder.LastSceneController.Tick();
            }

            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.InvalidGraph,
                ((ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail).ForwardCode);
            Assert.AreEqual(0, builder.LastSceneGate.EnterCount);
            Assert.AreEqual(1, driver.LoadCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneDriverLoadStartFailure_UsesForwardSlotAndReleases(bool returnsNull)
        {
            var driver = new TestSceneFlowDriver(
                throwLoadStart: !returnsNull,
                nullLoadStart: returnsNull);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));

            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            Pump(operation);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            var detail = (ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, detail.ForwardError.Code);
            Assert.IsTrue(detail.CleanupError.IsNone);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            Assert.IsTrue(builder.LastSceneGate.IsInert);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneDriverUnloadStartFailure_UsesCleanupSlotAndKeepsDebt(bool returnsNull)
        {
            var load = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(
                load,
                throwUnloadStart: !returnsNull,
                nullUnloadStart: returnsNull);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation forward = SceneForward(transaction, "candidate");
            forward.Tick(in TickContext);
            load.CompleteSuccess();
            forward.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, forward.Status);

            IApplicationStartupStepOperation rollback = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"),
                new ApplicationCandidateSceneRequest("candidate")).BeginRollback(Rollback(transaction));
            rollback.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, rollback.Status);
            var detail = (ApplicationCandidateSceneErrorDetail)rollback.InnerErrorDetail;
            Assert.IsTrue(detail.ForwardError.IsNone);
            Assert.AreEqual(SceneFlowErrorCode.DriverFailure, detail.CleanupError.Code);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.Scene, detail.CleanupDebt);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.CleanupFailed, graph.SceneStatus);
            Assert.IsTrue(builder.LastSceneGate.IsInert);
            Assert.AreEqual(1, driver.UnloadCount);
        }

        [Test]
        public void FrozenSceneHandoff_GraphFirstClaimDeterministicallyRejectsInterleavedRollback()
        {
            var load = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation sceneForward = SceneForward(transaction, "candidate");
            sceneForward.Tick(in TickContext);
            load.CompleteSuccess();
            sceneForward.Tick(in TickContext);
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateHandoffStep(new ApplicationStartupStepId("handoff")).Begin(Forward(transaction)));
            Assert.IsTrue(graph.TryGetFrozenHandoffLease(out ApplicationCandidateFrozenHandoffLease lease));
            var owner = new ApplicationCandidateFrozenHandoffOwner();
            Assert.Throws<InvalidOperationException>(() =>
                lease.TryClaim(owner, () => throw new InvalidOperationException("Injected claim hook failure.")));
            Assert.IsFalse(lease.IsClaimed);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffFrozen, graph.PumpOwnership);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffFrozen, graph.ScenePumpOwnership);
            bool hookRan = false;
            ApplicationCandidateSessionErrorCode rollbackResult = ApplicationCandidateSessionErrorCode.None;

            bool claimed = lease.TryClaim(owner, () =>
            {
                hookRan = true;
                Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.PumpOwnership);
                Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffFrozen, graph.ScenePumpOwnership);
                rollbackResult = graph.BeginRollbackOwnership();
            });

            Assert.IsTrue(hookRan);
            Assert.IsTrue(claimed);
            Assert.IsTrue(lease.IsClaimed);
            Assert.AreEqual(ApplicationCandidateSessionErrorCode.PumpOwnershipConflict, rollbackResult);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.PumpOwnership);
            Assert.AreEqual(ApplicationCandidatePumpOwnership.HandoffClaimed, graph.ScenePumpOwnership);
            Assert.AreEqual(0, driver.UnloadCount);
        }

        [Test]
        public void Coordinator_SceneTimeoutLateSuccessDrainsExactUnloadAndKeepsOuterTimedOut()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            var transactionFactory = new CandidateTransactionFactory(builder);
            var resourceId = new ApplicationStartupStepId("resource");
            var sceneId = new ApplicationStartupStepId("scene");
            IApplicationStartupStep[] steps =
            {
                new ApplicationCandidateResourceStep(resourceId),
                new ApplicationCandidateSceneStep(sceneId, new ApplicationCandidateSceneRequest("candidate"))
            };
            ApplicationStartupStepDescriptor[] descriptors =
            {
                new ApplicationStartupStepDescriptor(resourceId, null, 0, 1f),
                new ApplicationStartupStepDescriptor(sceneId, new[] { resourceId }, 1, 1f, 0.1d)
            };
            Assert.IsTrue(ApplicationStartupPlan.TryCreate(
                new ApplicationStartupPlanId("candidate-scene-timeout"), descriptors, steps,
                transactionFactory, out ApplicationStartupPlan plan, out ApplicationStartupError error),
                error.Message);
            var coordinator = new ApplicationStartupCoordinator(plan, new LiveApplicationStatePublisher());
            coordinator.Start("scene-timeout");
            for (int i = 0; i < 8 && driver.LoadCount == 0; i++)
            {
                var tick = new ApplicationStartupTickContext(0.01d, 0.01d * (i + 1));
                coordinator.Tick(in tick);
            }
            Assert.AreEqual(1, driver.LoadCount);

            var timeoutTick = new ApplicationStartupTickContext(0.2d, 0.3d);
            coordinator.Tick(in timeoutTick);
            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            load.CompleteSuccess();
            coordinator.Tick(in timeoutTick);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            unload.CompleteSuccess();
            coordinator.Tick(in timeoutTick);
            PumpCoordinator(coordinator);

            Assert.AreEqual(ApplicationStartupState.TimedOut, coordinator.State);
            Assert.AreEqual(ApplicationStartupErrorCode.Timeout, coordinator.ForwardError.Code);
            Assert.AreEqual(RuntimeOperationStatus.Cancelled, coordinator.DrainedOperationStatus);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released,
                transactionFactory.Graphs[0].SceneStatus);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
        }

        [Test]
        public void ScenePendingOperation_Warmed1000PollsAllocateZeroBytesAndNeverReadsTerminalFields()
        {
            var load = new TestSceneFlowOperation("candidate", 0.25f);
            var driver = new TestSceneFlowDriver(load, new TestSceneFlowOperation("candidate"));
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            _ = operation.Status; _ = operation.Progress; _ = operation.Error; _ = operation.InnerErrorDetail;
            _ = graph.SceneStatus; _ = graph.SceneProgress; _ = graph.ScenePumpOwnership;

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                operation.Tick(in TickContext);
                _ = operation.Status; _ = operation.Progress; _ = operation.Error; _ = operation.InnerErrorDetail;
                _ = graph.SceneStatus; _ = graph.SceneProgress; _ = graph.ScenePumpOwnership;
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.GreaterOrEqual(load.IsDoneReadCount, 1001);
            Assert.AreEqual(0, load.TerminalReadCount);
        }

        [Test]
        public void SceneLoadFailure_PreservesExactErrorAndUnrelatedLiveController()
        {
            var liveController = new SceneFlowController(new TestSceneFlowDriver(), "live");
            var load = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            var exact = new SceneFlowError(SceneFlowErrorCode.DriverFailure, "candidate", "exact-load-failure");
            load.CompleteFailure(exact);

            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            var detail = (ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(exact.Code, detail.ForwardError.Code);
            Assert.AreEqual(exact.Message, detail.ForwardError.Message);
            Assert.AreEqual(0, driver.UnloadCount);
            Assert.AreEqual("live", liveController.ActiveSceneKey);
        }

        [Test]
        public void SceneLoadFailure_WithOwnedSceneMakesInertAndDrainsUnloadBeforeFailing()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(hasOwnedScene: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            var exact = new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "load-failed-after-scene-created");
            load.CompleteFailure(exact);

            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Unloading, graph.SceneStatus);
            Assert.AreEqual(1, gate.InertCount);
            Assert.IsTrue(gate.IsInert);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.IsFalse(graph.ResourceLease.IsReleased);

            unload.CompleteSuccess();
            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            var detail = (ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(exact.Code, detail.ForwardError.Code);
            Assert.AreEqual(exact.Message, detail.ForwardError.Message);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.None, detail.CleanupDebt);
        }

        [Test]
        public void SceneLoadFailure_WithOwnedSceneUnloadFailurePreservesForwardAndCleanupErrors()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(hasOwnedScene: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            var forward = new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "owned-load-failure");
            var cleanup = new SceneFlowError(
                SceneFlowErrorCode.DriverFailure, "candidate", "owned-unload-failure");
            load.CompleteFailure(forward);
            operation.Tick(in TickContext);
            unload.CompleteFailure(cleanup);

            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.CleanupFailed, graph.SceneStatus);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            var detail = (ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(forward.Message, detail.ForwardError.Message);
            Assert.AreEqual(cleanup.Message, detail.CleanupError.Message);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.Scene, detail.CleanupDebt);
        }

        [Test]
        public void SceneCancelledLoadFailure_WithOwnedSceneStillDrainsUnloadBeforeTerminal()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(hasOwnedScene: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            operation.RequestCancel();
            load.CompleteFailure(new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "cancelled-owned-load-failure"));

            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.IsFalse(graph.ResourceLease.IsReleased);

            unload.CompleteSuccess();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Released, graph.SceneStatus);
            Assert.AreEqual("cancelled-owned-load-failure",
                ((ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail).ForwardError.Message);
        }

        [Test]
        public void SceneLoadFailure_OwnedSceneGetterFaultFailsClosedThroughUnloadDebt()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(throwOwnedScene: 1);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            var exact = new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "load-failure-before-owned-getter-fault");
            load.CompleteFailure(exact);

            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            unload.CompleteSuccess();
            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.CleanupFailed, graph.SceneStatus);
            var detail = (ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail;
            Assert.AreEqual(exact.Message, detail.ForwardError.Message);
            Assert.IsNotNull(detail.CleanupException);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.Scene, detail.CleanupDebt);
        }

        [Test]
        public void ResourceRollback_OwnedLoadFailureBlocksUntilExactSceneUnloadThenReleases()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(hasOwnedScene: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation forward = SceneForward(transaction, "candidate");
            forward.Tick(in TickContext);
            load.CompleteFailure(new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "owned-before-resource-rollback"));
            forward.Tick(in TickContext);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.Unloading, graph.SceneStatus);

            IApplicationStartupStepOperation blocked = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).BeginRollback(Rollback(transaction));
            blocked.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, blocked.Status);
            var blockedDetail = (ApplicationCandidateSessionErrorDetail)blocked.InnerErrorDetail;
            Assert.AreEqual(
                ApplicationCandidateCleanupDebt.Scene | ApplicationCandidateCleanupDebt.ResourcePlan,
                blockedDetail.CleanupDebts);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.Acquired, graph.ResourceStatus);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            Assert.IsFalse(builder.LastLifetime.IsReleased);

            IApplicationStartupStepOperation sceneRollback = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"),
                new ApplicationCandidateSceneRequest("candidate")).BeginRollback(Rollback(transaction));
            sceneRollback.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, sceneRollback.Status);
            Assert.IsFalse(builder.LastLifetime.IsReleased);
            unload.CompleteSuccess();
            sceneRollback.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, sceneRollback.Status);
            Assert.AreEqual("owned-before-resource-rollback", graph.SceneLease.ForwardError.Message);

            IApplicationStartupStepOperation released = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).BeginRollback(Rollback(transaction));
            Pump(released);

            Assert.AreEqual(RuntimeOperationStatus.Succeeded, released.Status);
            Assert.IsTrue(graph.ResourceLease.IsReleased);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ResourceRollback_SceneUnloadOrUnknownOwnershipDebtRemainsBlocked(bool ownershipGetterFault)
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = ownershipGetterFault
                ? new TestSceneIsolationGate(throwOwnedScene: 1)
                : new TestSceneIsolationGate(hasOwnedScene: true);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation forward = SceneForward(transaction, "candidate");
            forward.Tick(in TickContext);
            load.CompleteFailure(new SceneFlowError(
                SceneFlowErrorCode.OperationFailed, "candidate", "owned-debt"));
            forward.Tick(in TickContext);

            IApplicationStartupStepOperation sceneRollback = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"),
                new ApplicationCandidateSceneRequest("candidate")).BeginRollback(Rollback(transaction));
            sceneRollback.Tick(in TickContext);
            if (ownershipGetterFault)
                unload.CompleteSuccess();
            else
                unload.CompleteFailure(new SceneFlowError(
                    SceneFlowErrorCode.DriverFailure, "candidate", "unload-debt"));
            sceneRollback.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Failed, sceneRollback.Status);
            Assert.AreEqual(ApplicationCandidateSceneLeaseStatus.CleanupFailed, graph.SceneStatus);

            IApplicationStartupStepOperation resourceRollback = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).BeginRollback(Rollback(transaction));
            resourceRollback.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, resourceRollback.Status);
            var detail = (ApplicationCandidateSessionErrorDetail)resourceRollback.InnerErrorDetail;
            Assert.AreEqual(
                ApplicationCandidateCleanupDebt.Scene | ApplicationCandidateCleanupDebt.ResourcePlan,
                detail.CleanupDebts);
            Assert.AreEqual(ApplicationCandidateResourceLeaseStatus.Acquired, graph.ResourceStatus);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            Assert.IsFalse(builder.LastLifetime.IsReleased);
        }

        [Test]
        public void SceneCancel_LateLoadFailureDoesNotUnloadAndPreservesLoadError()
        {
            var load = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            operation.RequestCancel();
            load.CompleteFailure(new SceneFlowError(SceneFlowErrorCode.OperationFailed, "candidate", "late-failure"));

            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(0, driver.UnloadCount);
            Assert.AreEqual("late-failure", ((ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail).ForwardError.Message);
        }

        [Test]
        public void SceneSuccessfulRollback_UnloadsExactlyOnceBeforeResourceReleaseAndGraphFinalize()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            ApplicationCandidateSessionGraph graph = Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation forward = SceneForward(transaction, "candidate");
            forward.Tick(in TickContext);
            load.CompleteSuccess();
            forward.Tick(in TickContext);
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(
                new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(
                new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));

            AssertSucceeded(new ApplicationCandidateAppFlowStep(
                new ApplicationStartupStepId("app-flow")).BeginRollback(Rollback(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(
                new ApplicationStartupStepId("runtime")).BeginRollback(Rollback(transaction)));

            var sceneStep = new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"), new ApplicationCandidateSceneRequest("candidate"));
            IApplicationStartupStepOperation rollback = sceneStep.BeginRollback(Rollback(transaction));
            rollback.Tick(in TickContext);
            Assert.AreEqual(1, driver.UnloadCount);
            Assert.IsFalse(graph.ResourceLease.IsReleased);
            Assert.IsFalse(builder.LastLifetime.IsReleased);
            unload.CompleteSuccess();
            rollback.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, rollback.Status);
            Assert.IsTrue(graph.SceneLease.IsReleased);
            Assert.IsFalse(graph.ResourceLease.IsReleased);

            IApplicationStartupStepOperation repeated = sceneStep.BeginRollback(Rollback(transaction));
            repeated.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, repeated.Status);
            Assert.AreEqual(1, driver.UnloadCount);

            IApplicationStartupStepOperation resourceRollback = new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).BeginRollback(Rollback(transaction));
            Pump(resourceRollback);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, resourceRollback.Status);
            Assert.IsTrue(graph.ResourceLease.IsReleased);
            Assert.IsTrue(builder.LastLifetime.IsReleased);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SceneOperationGetterFault_RemainsNonTerminalUntilSameLoadDrains(
            bool throwIsDone,
            bool throwProgress)
        {
            var load = new TestSceneFlowOperation(
                "candidate", 0.2f, throwIsDone ? 1 : 0, throwProgress ? 1 : 0);
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver);
            Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");

            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(0, driver.UnloadCount);
            load.CompleteSuccess();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Running, operation.Status);
            Assert.AreEqual(1, driver.UnloadCount);
            unload.CompleteSuccess();
            operation.Tick(in TickContext);
            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
        }

        [Test]
        public void SceneMakeInertGetterFault_StillStartsUnloadAndReportsSceneDebt()
        {
            var load = new TestSceneFlowOperation("candidate");
            var unload = new TestSceneFlowOperation("candidate");
            var driver = new TestSceneFlowDriver(load, unload);
            var gate = new TestSceneIsolationGate(throwInertAfterEnter: 1);
            var builder = new FreshBuilder(withScene: true, sceneDriver: driver, sceneGate: gate);
            Create(new ApplicationCandidateSessionFactory(builder), out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            IApplicationStartupStepOperation operation = SceneForward(transaction, "candidate");
            operation.Tick(in TickContext);
            operation.RequestCancel();
            load.CompleteSuccess();

            operation.Tick(in TickContext);
            Assert.AreEqual(1, driver.UnloadCount);
            unload.CompleteSuccess();
            operation.Tick(in TickContext);

            Assert.AreEqual(RuntimeOperationStatus.Failed, operation.Status);
            Assert.AreEqual(ApplicationCandidateCleanupDebt.Scene,
                ((ApplicationCandidateSceneErrorDetail)operation.InnerErrorDetail).CleanupDebt);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ScenePumpMismatch_RejectsGraphBeforeLoad(bool runtimeModuleMissingRegistration)
        {
            var builder = new ScenePumpMismatchBuilder(runtimeModuleMissingRegistration);

            Assert.Throws<InvalidOperationException>(() =>
                new ApplicationCandidateSessionFactory(builder).Create(new TestTransaction()));

            Assert.AreEqual(0, builder.Driver.LoadCount);
        }

        [Test]
        public void SceneOwnershipIdentity_MismatchOrNullRejectsGraphConstruction()
        {
            var driverIdentity = new ApplicationCandidateSceneOwnershipIdentity();
            var gateIdentity = new ApplicationCandidateSceneOwnershipIdentity();
            var driver = new TestSceneFlowDriver(ownershipIdentity: driverIdentity);

            Assert.Throws<ArgumentException>(() => new ApplicationCandidateSceneGraph(
                new SceneFlowController(driver),
                null,
                ApplicationControllerPumpStrategy.Standalone,
                new TestSceneIsolationGate(ownershipIdentity: gateIdentity)));
            Assert.Throws<ArgumentException>(() => new ApplicationCandidateSceneGraph(
                new SceneFlowController(new NullOwnershipSceneFlowDriver()),
                null,
                ApplicationControllerPumpStrategy.Standalone,
                new TestSceneIsolationGate(ownershipIdentity: driverIdentity)));
        }

        [Test]
        public void SceneOwnershipIdentity_MutationAfterDescriptorConstructionRejectsSessionGraph()
        {
            var builder = new SceneOwnershipMutationBuilder();

            Assert.Throws<InvalidOperationException>(() =>
                new ApplicationCandidateSessionFactory(builder).Create(new TestTransaction()));

            Assert.IsTrue(builder.BuildCompleted);
        }

        private static IApplicationStartupStepOperation SceneForward(TestTransaction transaction, string sceneKey) =>
            new ApplicationCandidateSceneStep(
                new ApplicationStartupStepId("scene"),
                new ApplicationCandidateSceneRequest(sceneKey)).Begin(Forward(transaction));

        private static ApplicationStartupStepContext Forward(TestTransaction transaction) =>
            new ApplicationStartupStepContext("test-run", transaction);
        private static ApplicationStartupRollbackContext Rollback(TestTransaction transaction) =>
            new ApplicationStartupRollbackContext("test-run", transaction, ApplicationStartupError.None);

        private static ApplicationCandidateSessionGraph Create(
            ApplicationCandidateSessionFactory factory,
            out TestTransaction transaction)
        {
            transaction = new TestTransaction();
            ApplicationCandidateSessionGraph graph = factory.Create(transaction);
            return graph;
        }

        private static ApplicationCandidateSessionGraph CreateFrozenGraph(out TestTransaction transaction)
        {
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(new FreshBuilder(ApplicationControllerPumpStrategy.RuntimeModule)),
                out transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateHandoffStep(new ApplicationStartupStepId("handoff")).Begin(Forward(transaction)));
            return graph;
        }

        internal static ApplicationCandidateSessionGraph CreateFrozenGraphForPersistenceBridgeTests(
            bool withScene,
            out IApplicationCandidateSceneIsolationGate isolationGate,
            out Action completeSceneUnload)
        {
            return CreateFrozenGraphForPersistenceBridgeTests(
                withScene,
                1,
                false,
                out isolationGate,
                out completeSceneUnload,
                out _,
                out _);
        }

        internal static ApplicationCandidateSessionGraph CreateFrozenGraphForPersistenceBridgeTests(
            bool withScene,
            int appFlowRollbackTicks,
            bool failSceneMakeInert,
            out IApplicationCandidateSceneIsolationGate isolationGate,
            out Action completeSceneUnload,
            out Action failSceneUnload,
            out Func<bool> isResourceGraphReleased,
            ApplicationControllerPumpStrategy appFlowStrategy = ApplicationControllerPumpStrategy.RuntimeModule,
            ApplicationControllerPumpStrategy sceneStrategy = ApplicationControllerPumpStrategy.Standalone,
            bool failLiveTick = false,
            RuntimeHostErrorPolicy runtimePolicy = RuntimeHostErrorPolicy.FailFast)
        {
            TestSceneFlowOperation load = withScene ? new TestSceneFlowOperation("candidate") : null;
            TestSceneFlowOperation unload = withScene ? new TestSceneFlowOperation("candidate") : null;
            TestSceneFlowDriver driver = withScene ? new TestSceneFlowDriver(load, unload) : null;
            TestSceneIsolationGate gate = withScene
                ? new TestSceneIsolationGate(failMakeInert: failSceneMakeInert)
                : null;
            var builder = new FreshBuilder(
                appFlowStrategy,
                rollbackTicks: appFlowRollbackTicks,
                withScene: withScene,
                sceneDriver: driver,
                sceneGate: gate,
                sceneStrategy: sceneStrategy,
                failLiveTick: failLiveTick,
                runtimePolicy: runtimePolicy);
            ApplicationCandidateSessionGraph graph = Create(
                new ApplicationCandidateSessionFactory(builder),
                out TestTransaction transaction);
            AssertSucceeded(new ApplicationCandidateResourceStep(
                new ApplicationStartupStepId("resource")).Begin(Forward(transaction)));
            if (withScene)
            {
                IApplicationStartupStepOperation scene = SceneForward(transaction, "candidate");
                scene.Tick(in TickContext);
                load.CompleteSuccess();
                scene.Tick(in TickContext);
                Assert.AreEqual(RuntimeOperationStatus.Succeeded, scene.Status);
            }
            AssertSucceeded(new ApplicationCandidateRuntimeLifecycleStep(
                new ApplicationStartupStepId("runtime")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateAppFlowStep(
                new ApplicationStartupStepId("app-flow")).Begin(Forward(transaction)));
            AssertSucceeded(new ApplicationCandidateHandoffStep(
                new ApplicationStartupStepId("handoff")).Begin(Forward(transaction)));
            isolationGate = builder.LastSceneGate;
            completeSceneUnload = unload != null ? (Action)unload.CompleteSuccess : null;
            failSceneUnload = unload != null
                ? (Action)(() => unload.CompleteFailure(new SceneFlowError(
                    SceneFlowErrorCode.OperationFailed,
                    "candidate",
                    "injected persistence bridge retirement unload failure")))
                : null;
            isResourceGraphReleased = () => builder.LastLifetime.IsReleased;
            return graph;
        }

        private static void AssertSucceeded(IApplicationStartupStepOperation operation)
        {
            Pump(operation);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, operation.Status, operation.Error.Message);
        }

        private static void AssertEnumValues<T>(params int[] expected) where T : struct, Enum
        {
            Array values = Enum.GetValues(typeof(T));
            var actual = new int[values.Length];
            for (int i = 0; i < values.Length; i++) actual[i] = (int)Convert.ChangeType(values.GetValue(i), typeof(int));
            CollectionAssert.AreEqual(expected, actual);
        }

        private static void PumpCoordinator(ApplicationStartupCoordinator coordinator)
        {
            for (int i = 0; i < 100 && !coordinator.IsTerminal; i++)
            {
                var context = new ApplicationStartupTickContext(0.1d, coordinator.ElapsedSeconds + 0.1d);
                coordinator.Tick(in context);
            }
            Assert.IsTrue(coordinator.IsTerminal);
        }

        private static void Pump(IApplicationStartupStepOperation operation)
        {
            for (int i = 0; i < 32 && !IsTerminal(operation.Status); i++)
                operation.Tick(in TickContext);
        }

        private static bool IsTerminal(RuntimeOperationStatus status) =>
            status == RuntimeOperationStatus.Succeeded || status == RuntimeOperationStatus.Failed ||
            status == RuntimeOperationStatus.Cancelled || status == RuntimeOperationStatus.TimedOut;

        private sealed class FreshBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private readonly ApplicationControllerPumpStrategy _strategy;
            private readonly RuntimeOperationStatus _appFlowPrepareStatus;
            private readonly bool _canCompensate;
            private readonly bool _failResourceGraphCleanup;
            private readonly string _rollbackMode;
            private readonly bool _freezeThrows;
            private readonly bool _disposeThrows;
            private readonly bool _resourcePrepareThrows;
            private readonly bool _resourceCancelThrows;
            private readonly bool _resourcePending;
            private readonly bool _resourceGraphThrows;
            private readonly bool _resourcePollAndCancelThrows;
            private readonly string _runtimeFault;
            private readonly int _prepareTicks;
            private readonly int _rollbackTicks;
            private readonly bool _cancelCompletes;
            private readonly RuntimeHostErrorPolicy _runtimePolicy;
            private readonly bool _prepareTickThrows;
            private readonly bool _rollbackTickThrows;
            private readonly bool _faultDrainRequestThrows;
            private readonly bool _faultDrainTickThrows;
            private readonly bool _withScene;
            private readonly TestSceneFlowDriver _sceneDriver;
            private readonly TestSceneIsolationGate _sceneGate;
            private readonly ApplicationControllerPumpStrategy _sceneStrategy;
            private readonly bool _failLiveTick;

            public FreshBuilder(
                ApplicationControllerPumpStrategy strategy = ApplicationControllerPumpStrategy.Standalone,
                RuntimeOperationStatus appFlowPrepareStatus = RuntimeOperationStatus.Succeeded,
                bool canCompensate = true,
                bool failResourceGraphCleanup = false,
                string rollbackMode = "success",
                bool freezeThrows = false,
                bool disposeThrows = false,
                bool resourcePrepareThrows = false,
                bool resourceCancelThrows = false,
                string runtimeFault = "",
                int prepareTicks = 1,
                int rollbackTicks = 1,
                bool cancelCompletes = true,
                bool prepareTickThrows = false,
                bool rollbackTickThrows = false,
                bool faultDrainRequestThrows = false,
                RuntimeHostErrorPolicy runtimePolicy = RuntimeHostErrorPolicy.FailFast,
                bool resourcePending = false,
                bool faultDrainTickThrows = false,
                bool resourceGraphThrows = false,
                bool resourcePollAndCancelThrows = false,
                bool withScene = false,
                TestSceneFlowDriver sceneDriver = null,
                TestSceneIsolationGate sceneGate = null,
                ApplicationControllerPumpStrategy sceneStrategy = ApplicationControllerPumpStrategy.Standalone,
                bool failLiveTick = false)
            {
                _strategy = strategy;
                _appFlowPrepareStatus = appFlowPrepareStatus;
                _canCompensate = canCompensate;
                _failResourceGraphCleanup = failResourceGraphCleanup;
                _rollbackMode = rollbackMode;
                _freezeThrows = freezeThrows;
                _disposeThrows = disposeThrows;
                _resourcePrepareThrows = resourcePrepareThrows;
                _resourceCancelThrows = resourceCancelThrows;
                _runtimeFault = runtimeFault;
                _prepareTicks = prepareTicks;
                _rollbackTicks = rollbackTicks;
                _cancelCompletes = cancelCompletes;
                _prepareTickThrows = prepareTickThrows;
                _rollbackTickThrows = rollbackTickThrows;
                _faultDrainRequestThrows = faultDrainRequestThrows;
                _runtimePolicy = runtimePolicy;
                _resourcePending = resourcePending;
                _faultDrainTickThrows = faultDrainTickThrows;
                _resourceGraphThrows = resourceGraphThrows;
                _resourcePollAndCancelThrows = resourcePollAndCancelThrows;
                _withScene = withScene;
                _sceneDriver = sceneDriver;
                _sceneGate = sceneGate;
                _sceneStrategy = sceneStrategy;
                _failLiveTick = failLiveTick;
            }

            public TestAppFlowLifecycle LastLifecycle { get; private set; }
            public TestResourceGraphLifetime LastLifetime { get; private set; }
            public System.Collections.Generic.List<TestResourceGraphLifetime> Lifetimes { get; } =
                new System.Collections.Generic.List<TestResourceGraphLifetime>();
            public System.Collections.Generic.List<string> FaultLog { get; } =
                new System.Collections.Generic.List<string>();
            public PendingResourceOperation LastPendingResourceOperation { get; private set; }
            public int BuildCount { get; private set; }
            public SceneFlowController LastSceneController { get; private set; }
            public TestSceneFlowDriver LastSceneDriver { get; private set; }
            public TestSceneIsolationGate LastSceneGate { get; private set; }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                BuildCount++;
                AppFlowController controller = new AppFlowController();
                controller.RegisterState(new EmptyState());
                var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = _runtimePolicy });
                if (!string.IsNullOrEmpty(_runtimeFault))
                {
                    host.RegisterModule(new FaultModule("a", FaultLog, string.Empty));
                    host.RegisterModule(new FaultModule("b", FaultLog, _runtimeFault));
                    host.RegisterModule(new FaultModule("c", FaultLog, string.Empty));
                }
                if (_failLiveTick)
                    host.RegisterModule(new LiveTickFaultModule());
                AppFlowRuntimeModule module = null;
                if (_strategy == ApplicationControllerPumpStrategy.RuntimeModule)
                {
                    module = new AppFlowRuntimeModule(controller);
                    host.RegisterModule(module);
                }

                LastLifecycle = new TestAppFlowLifecycle(
                    controller,
                    module,
                    _strategy,
                    _canCompensate,
                    _appFlowPrepareStatus,
                    _rollbackMode,
                    _freezeThrows,
                    _disposeThrows,
                    _prepareTicks,
                    _rollbackTicks,
                    _cancelCompletes,
                    _prepareTickThrows,
                    _rollbackTickThrows,
                    _faultDrainRequestThrows,
                    _faultDrainTickThrows);
                ResourceManager manager = new ResourceManager();
                ResourceRuntimeConfigurationIdentity identity = manager.SealRuntimeConfiguration();
                IResourcePreloadService preloadService;
                if (_resourcePrepareThrows) preloadService = new ThrowingPreloadService();
                else if (_resourcePollAndCancelThrows) preloadService = new ThrowingPollAndCancelPreloadService();
                else if (_resourceCancelThrows) preloadService = new ThrowingCancelPreloadService();
                else if (_resourcePending)
                {
                    LastPendingResourceOperation = new PendingResourceOperation();
                    preloadService = new PendingPreloadService(LastPendingResourceOperation);
                }
                else preloadService = new ResourcePreloadService(manager);
                var orchestrator = new ResourcePlanOrchestrator(preloadService, manager);
                var plan = new ResourcePlan("candidate.empty", "v1", Array.Empty<ResourceKey>());
                LastLifetime = new TestResourceGraphLifetime(_failResourceGraphCleanup, _resourceGraphThrows);
                Lifetimes.Add(LastLifetime);
                var resourceGraph = new ApplicationCandidateResourceGraph(
                    manager,
                    identity,
                    orchestrator,
                    plan,
                    LastLifetime);
                ApplicationCandidateSceneGraph sceneGraph = null;
                if (_withScene)
                {
                    LastSceneDriver = _sceneDriver ?? new TestSceneFlowDriver();
                    LastSceneGate = _sceneGate ?? new TestSceneIsolationGate();
                    LastSceneGate.BindOwnershipIdentity(LastSceneDriver.SceneOwnershipIdentity);
                    LastSceneController = new SceneFlowController(LastSceneDriver);
                    SceneFlowRuntimeModule sceneModule = null;
                    if (_sceneStrategy == ApplicationControllerPumpStrategy.RuntimeModule)
                    {
                        sceneModule = new SceneFlowRuntimeModule(LastSceneController);
                        host.RegisterModule(sceneModule);
                    }
                    sceneGraph = new ApplicationCandidateSceneGraph(
                        LastSceneController,
                        sceneModule,
                        _sceneStrategy,
                        LastSceneGate);
                }
                var components = new ApplicationCandidateSessionComponents(host, LastLifecycle, resourceGraph, sceneGraph);
                return new TestBuildLease(token, components, () =>
                {
                    host.Dispose();
                    LastLifecycle.Dispose();
                    LastLifetime.ReleaseForAbandon();
                });
            }
        }

        private sealed class ReusingBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private ApplicationCandidateSessionComponents _components;
            public TestBuildLease SecondLease { get; private set; }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                if (_components == null)
                {
                    var fresh = new FreshBuilder();
                    var firstLease = (TestBuildLease)fresh.Build(token);
                    _components = firstLease.Components;
                    return firstLease;
                }
                SecondLease = new TestBuildLease(token, _components, () => { });
                return SecondLease;
            }
        }

        private sealed class ScenePumpMismatchBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private readonly bool _runtimeModuleMissingRegistration;

            public ScenePumpMismatchBuilder(bool runtimeModuleMissingRegistration)
            {
                _runtimeModuleMissingRegistration = runtimeModuleMissingRegistration;
                Driver = new TestSceneFlowDriver();
            }

            public TestSceneFlowDriver Driver { get; }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                var baseBuilder = new FreshBuilder();
                var baseLease = (TestBuildLease)baseBuilder.Build(token);
                ApplicationCandidateSessionComponents original = baseLease.Components;
                var controller = new SceneFlowController(Driver);
                var module = new SceneFlowRuntimeModule(controller);
                ApplicationControllerPumpStrategy strategy;
                SceneFlowRuntimeModule descriptorModule;
                if (_runtimeModuleMissingRegistration)
                {
                    strategy = ApplicationControllerPumpStrategy.RuntimeModule;
                    descriptorModule = module;
                }
                else
                {
                    original.RuntimeHost.RegisterModule(module);
                    strategy = ApplicationControllerPumpStrategy.Standalone;
                    descriptorModule = null;
                }
                var scene = new ApplicationCandidateSceneGraph(
                    controller,
                    descriptorModule,
                    strategy,
                    new TestSceneIsolationGate(ownershipIdentity: Driver.SceneOwnershipIdentity));
                var components = new ApplicationCandidateSessionComponents(
                    original.RuntimeHost,
                    original.AppFlowLifecycle,
                    original.ResourceGraph,
                    scene);
                return new TestBuildLease(token, components, () =>
                    baseLease.Abandon(ApplicationCandidateComponentReuse.None));
            }
        }

        private sealed class SceneOwnershipMutationBuilder : IApplicationCandidateSessionGraphBuilder
        {
            public bool BuildCompleted { get; private set; }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                var baseBuilder = new FreshBuilder();
                var baseLease = (TestBuildLease)baseBuilder.Build(token);
                ApplicationCandidateSessionComponents original = baseLease.Components;
                var identity = new ApplicationCandidateSceneOwnershipIdentity();
                var driver = new TestSceneFlowDriver(ownershipIdentity: identity);
                var gate = new TestSceneIsolationGate(ownershipIdentity: identity);
                var scene = new ApplicationCandidateSceneGraph(
                    new SceneFlowController(driver),
                    null,
                    ApplicationControllerPumpStrategy.Standalone,
                    gate);
                gate.OverrideOwnershipIdentity(new ApplicationCandidateSceneOwnershipIdentity());
                BuildCompleted = true;
                return new TestBuildLease(
                    token,
                    new ApplicationCandidateSessionComponents(
                        original.RuntimeHost,
                        original.AppFlowLifecycle,
                        original.ResourceGraph,
                        scene),
                    () => baseLease.Abandon(ApplicationCandidateComponentReuse.None));
            }
        }

        private sealed class PollutedBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private readonly string _target;
            public PollutedBuilder(string target) => _target = target;
            public TestBuildLease Lease { get; private set; }
            public TestResourceGraphLifetime Lifetime { get; private set; }
            public TestAppFlowLifecycle Lifecycle { get; private set; }
            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                var fresh = new FreshBuilder();
                Lease = (TestBuildLease)fresh.Build(token);
                Lifetime = fresh.LastLifetime;
                Lifecycle = fresh.LastLifecycle;
                if (_target == "host")
                    Lease.Components.RuntimeHost.Initialize();
                else
                    Lifecycle.Controller.Start("initial", "polluted-build");
                return Lease;
            }
        }

        private sealed class ReusedPlanBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private ResourcePlan _firstPlan;
            public TestBuildLease SecondLease { get; private set; }
            public TestResourceGraphLifetime FirstLifetime { get; private set; }
            public TestResourceGraphLifetime SecondLifetime { get; private set; }
            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                var fresh = new FreshBuilder();
                var inner = (TestBuildLease)fresh.Build(token);
                if (_firstPlan == null)
                {
                    _firstPlan = inner.Components.ResourceGraph.Plan;
                    FirstLifetime = fresh.LastLifetime;
                    return inner;
                }

                SecondLifetime = fresh.LastLifetime;

                ApplicationCandidateResourceGraph original = inner.Components.ResourceGraph;
                var resource = new ApplicationCandidateResourceGraph(
                    original.Manager,
                    original.ConfigurationIdentity,
                    original.Orchestrator,
                    _firstPlan,
                    original.Lifetime);
                var components = new ApplicationCandidateSessionComponents(
                    inner.Components.RuntimeHost,
                    inner.Components.AppFlowLifecycle,
                    resource);
                SecondLease = new TestBuildLease(token, components,
                    () => inner.Abandon(ApplicationCandidateComponentReuse.None));
                return SecondLease;
            }
        }

        private sealed class InvalidStandaloneBuilder : IApplicationCandidateSessionGraphBuilder
        {
            private readonly bool _runtimeWrapperMismatch;
            public InvalidStandaloneBuilder(bool runtimeWrapperMismatch = false) =>
                _runtimeWrapperMismatch = runtimeWrapperMismatch;
            public TestBuildLease LastLease { get; private set; }
            public TestAppFlowLifecycle Lifecycle { get; private set; }
            public TestResourceGraphLifetime Lifetime { get; private set; }
            public int BuildCount { get; private set; }

            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                BuildCount++;
                var controller = new AppFlowController();
                controller.RegisterState(new EmptyState());
                var host = new RuntimeHost();
                var registeredModule = new AppFlowRuntimeModule(controller, "registered-app-flow");
                host.RegisterModule(registeredModule);
                AppFlowRuntimeModule expectedModule = _runtimeWrapperMismatch
                    ? new AppFlowRuntimeModule(controller, "foreign-app-flow")
                    : null;
                Lifecycle = new TestAppFlowLifecycle(
                    controller,
                    expectedModule,
                    _runtimeWrapperMismatch
                        ? ApplicationControllerPumpStrategy.RuntimeModule
                        : ApplicationControllerPumpStrategy.Standalone,
                    true,
                    RuntimeOperationStatus.Succeeded);
                var manager = new ResourceManager();
                var identity = manager.SealRuntimeConfiguration();
                var orchestrator = new ResourcePlanOrchestrator(new ResourcePreloadService(manager), manager);
                Lifetime = new TestResourceGraphLifetime(false);
                var resource = new ApplicationCandidateResourceGraph(
                    manager, identity, orchestrator,
                    new ResourcePlan("invalid", "v1", Array.Empty<ResourceKey>()), Lifetime);
                LastLease = new TestBuildLease(
                    token,
                    new ApplicationCandidateSessionComponents(host, Lifecycle, resource),
                    () => { host.Dispose(); Lifecycle.Dispose(); Lifetime.ReleaseForAbandon(); });
                return LastLease;
            }
        }

        private sealed class AliasedOwnerBuilder : IApplicationCandidateSessionGraphBuilder
        {
            public TestBuildLease Lease { get; private set; }
            public AliasedLifecycleLifetime Owner { get; private set; }
            public IApplicationCandidateSessionBuildLease Build(ApplicationCandidateSessionToken token)
            {
                var controller = new AppFlowController();
                controller.RegisterState(new EmptyState());
                Owner = new AliasedLifecycleLifetime(controller);
                var host = new RuntimeHost();
                var manager = new ResourceManager();
                var resourceGraph = new ApplicationCandidateResourceGraph(
                    manager,
                    manager.SealRuntimeConfiguration(),
                    new ResourcePlanOrchestrator(new ResourcePreloadService(manager), manager),
                    new ResourcePlan("alias", "v1", Array.Empty<ResourceKey>()),
                    Owner);
                Lease = new TestBuildLease(token,
                    new ApplicationCandidateSessionComponents(host, Owner, resourceGraph),
                    () => { host.Dispose(); Owner.Dispose(); });
                return Lease;
            }
        }

        private sealed class AliasedLifecycleLifetime :
            IApplicationCandidateAppFlowLifecycle,
            IApplicationCandidateResourceGraphLifetime
        {
            public AliasedLifecycleLifetime(AppFlowController controller) => Controller = controller;
            public AppFlowController Controller { get; }
            public AppFlowRuntimeModule RuntimeModule => null;
            public ApplicationControllerPumpStrategy PumpStrategy => ApplicationControllerPumpStrategy.Standalone;
            public bool CanCompensate => true;
            public bool IsReleased => Disposed;
            public bool Disposed { get; private set; }
            public IApplicationStartupStepOperation PrepareStart() =>
                new TestOperation("alias-prepare", RuntimeOperationStatus.Succeeded, null);
            public void FreezeForHandoff() { }
            public IApplicationStartupStepOperation BeginRollback() =>
                new TestOperation("alias-rollback", RuntimeOperationStatus.Succeeded, null);
            public ApplicationCandidateResourceGraphCleanupResult FinalizeRollback()
            {
                Disposed = true;
                return ApplicationCandidateResourceGraphCleanupResult.Succeeded;
            }
            public void Dispose() => Disposed = true;
        }

        private sealed class TestBuildLease : IApplicationCandidateSessionBuildLease
        {
            private readonly Action _abandon;
            private int _state;
            public TestBuildLease(ApplicationCandidateSessionToken token, ApplicationCandidateSessionComponents components, Action abandon)
            {
                Token = token;
                Components = components;
                _abandon = abandon;
            }
            public ApplicationCandidateSessionToken Token { get; }
            public ApplicationCandidateSessionComponents Components { get; }
            public int AbandonCount { get; private set; }
            public int TransferCount { get; private set; }
            public bool TryTransfer()
            {
                if (_state != 0) return false;
                _state = 1;
                TransferCount++;
                return true;
            }
            public void Abandon(ApplicationCandidateComponentReuse reusedComponents)
            {
                if (_state != 0) return;
                _state = 2;
                AbandonCount++;
                if ((reusedComponents & (ApplicationCandidateComponentReuse.RuntimeHost |
                    ApplicationCandidateComponentReuse.AppFlowLifecycle |
                    ApplicationCandidateComponentReuse.ResourceLifetime)) != 0)
                    return;
                _abandon?.Invoke();
            }
        }

        private sealed class TestResourceGraphLifetime : IApplicationCandidateResourceGraphLifetime
        {
            private readonly bool _fail;
            private readonly bool _throw;
            public TestResourceGraphLifetime(bool fail, bool throwOnFinalize = false)
            {
                _fail = fail;
                _throw = throwOnFinalize;
            }
            public bool IsReleased { get; private set; }
            public ApplicationCandidateResourceGraphCleanupResult FinalizeRollback()
            {
                if (_throw) throw new InvalidOperationException("Injected resource graph finalize failure.");
                if (_fail) return ApplicationCandidateResourceGraphCleanupResult.Failed("TEST_RESOURCE_GRAPH_FAILURE");
                IsReleased = true;
                return ApplicationCandidateResourceGraphCleanupResult.Succeeded;
            }
            public void ReleaseForAbandon() => IsReleased = true;
        }

        private sealed class TestAppFlowLifecycle : IApplicationCandidateAppFlowLifecycle
        {
            private readonly RuntimeOperationStatus _prepareStatus;
            private readonly string _rollbackMode;
            private readonly bool _freezeThrows;
            private readonly bool _disposeThrows;
            private readonly int _prepareTicks;
            private readonly int _rollbackTicks;
            private readonly bool _cancelCompletes;
            private readonly bool _prepareTickThrows;
            private readonly bool _rollbackTickThrows;
            private readonly bool _faultDrainRequestThrows;
            private readonly bool _faultDrainTickThrows;
            public TestAppFlowLifecycle(
                AppFlowController controller,
                AppFlowRuntimeModule module,
                ApplicationControllerPumpStrategy strategy,
                bool canCompensate,
                RuntimeOperationStatus prepareStatus,
                string rollbackMode = "success",
                bool freezeThrows = false,
                bool disposeThrows = false,
                int prepareTicks = 1,
                int rollbackTicks = 1,
                bool cancelCompletes = true,
                bool prepareTickThrows = false,
                bool rollbackTickThrows = false,
                bool faultDrainRequestThrows = false,
                bool faultDrainTickThrows = false)
            {
                Controller = controller;
                RuntimeModule = module;
                PumpStrategy = strategy;
                CanCompensate = canCompensate;
                _prepareStatus = prepareStatus;
                _rollbackMode = rollbackMode;
                _freezeThrows = freezeThrows;
                _disposeThrows = disposeThrows;
                _prepareTicks = prepareTicks;
                _rollbackTicks = rollbackTicks;
                _cancelCompletes = cancelCompletes;
                _prepareTickThrows = prepareTickThrows;
                _rollbackTickThrows = rollbackTickThrows;
                _faultDrainRequestThrows = faultDrainRequestThrows;
                _faultDrainTickThrows = faultDrainTickThrows;
            }
            public AppFlowController Controller { get; }
            public AppFlowRuntimeModule RuntimeModule { get; }
            public ApplicationControllerPumpStrategy PumpStrategy { get; }
            public bool CanCompensate { get; }
            public int PrepareCount { get; private set; }
            public int RollbackCount { get; private set; }
            public int FreezeCount { get; private set; }
            public int SideEffectCount { get; private set; }
            public int DisposeCount { get; private set; }
            public TestOperation LastPrepareOperation { get; private set; }
            public System.Collections.Generic.List<string> NestedLog { get; } =
                new System.Collections.Generic.List<string>();
            public IApplicationStartupStepOperation PrepareStart()
            {
                PrepareCount++;
                LastPrepareOperation = new TestOperation("test-app-prepare", _prepareStatus, () =>
                {
                    SideEffectCount++;
                    if (_prepareStatus == RuntimeOperationStatus.Succeeded)
                        Controller.Start("initial", "candidate-prepare");
                }, _prepareTicks, _cancelCompletes, _prepareTickThrows,
                    RuntimeOperationStatus.Failed, _faultDrainRequestThrows, NestedLog, "prepare",
                    _faultDrainTickThrows);
                return LastPrepareOperation;
            }
            public void FreezeForHandoff()
            {
                FreezeCount++;
                if (_freezeThrows) throw new InvalidOperationException("Injected freeze failure.");
            }
            public IApplicationStartupStepOperation BeginRollback()
            {
                RollbackCount++;
                NestedLog.Add("rollback:begin");
                if (_rollbackMode == "throw") throw new InvalidOperationException("Injected rollback factory failure.");
                if (_rollbackMode == "null") return null;
                return new TestOperation("test-app-rollback", RuntimeOperationStatus.Succeeded, () =>
                {
                    if (SideEffectCount > 0) SideEffectCount--;
                }, _rollbackTicks, true, _rollbackTickThrows,
                    RuntimeOperationStatus.Succeeded, _faultDrainRequestThrows, NestedLog, "rollback",
                    _faultDrainTickThrows);
            }
            public void Dispose()
            {
                DisposeCount++;
                if (_disposeThrows) throw new InvalidOperationException("Injected dispose failure.");
            }
        }

        private sealed class ThrowingPreloadService : IResourcePreloadService
        {
            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Injected preload failure.");
            public void ReleaseGroup(ResourceGroupHandle handle) { }
        }

        private sealed class ThrowingCancelPreloadService : IResourcePreloadService
        {
            private readonly ThrowingCancelResourceOperation _operation = new ThrowingCancelResourceOperation();
            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default) => _operation;
            public void ReleaseGroup(ResourceGroupHandle handle) { }
        }

        private sealed class ThrowingPollAndCancelPreloadService : IResourcePreloadService
        {
            private readonly ThrowingPollAndCancelResourceOperation _operation =
                new ThrowingPollAndCancelResourceOperation();
            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default) => _operation;
            public void ReleaseGroup(ResourceGroupHandle handle) { }
        }

        private sealed class ThrowingPollAndCancelResourceOperation : IResourceOperation<ResourcePreloadResult>
        {
            public bool IsDone => throw new InvalidOperationException("Injected resource poll failure.");
            public bool IsCancelled => false;
            public float Progress => 0f;
            public ResourceLoadResult<ResourcePreloadResult> Result =>
                ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed, default, string.Empty, "Poll failed."));
            public void Cancel() => throw new InvalidOperationException("Injected resource cleanup failure.");
        }

        private sealed class ThrowingCancelResourceOperation : IResourceOperation<ResourcePreloadResult>
        {
            public bool IsDone => false;
            public bool IsCancelled => false;
            public float Progress => 0f;
            public ResourceLoadResult<ResourcePreloadResult> Result =>
                ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed, default, string.Empty, "Pending."));
            public void Cancel() => throw new InvalidOperationException("Injected cancel failure.");
        }

        private sealed class PendingPreloadService : IResourcePreloadService
        {
            private readonly PendingResourceOperation _operation;
            public PendingPreloadService(PendingResourceOperation operation) => _operation = operation;
            public IResourceOperation<ResourcePreloadResult> PreloadAsync(
                ResourcePreloadPlan plan,
                System.Threading.CancellationToken cancellationToken = default) => _operation;
            public void ReleaseGroup(ResourceGroupHandle handle) { }
        }

        private sealed class PendingResourceOperation : IResourceOperation<ResourcePreloadResult>
        {
            public int StatusReadCount { get; private set; }
            public bool IsDone { get { StatusReadCount++; return false; } }
            public bool IsCancelled => false;
            public float Progress => 0.25f;
            public ResourceLoadResult<ResourcePreloadResult> Result =>
                ResourceLoadResult<ResourcePreloadResult>.Failed(new ResourceError(
                    ResourceErrorCode.ProviderFailed, default, string.Empty, "Pending."));
            public void Cancel() { }
        }

        private sealed class FaultModule : RuntimeModule
        {
            private readonly System.Collections.Generic.List<string> _log;
            private readonly string _fault;
            public FaultModule(string id, System.Collections.Generic.List<string> log, string fault)
                : base(id) { _log = log; _fault = fault; }
            public override void Initialize(RuntimeHostContext context)
            {
                _log.Add("initialize:" + ModuleId);
                if (_fault == "initialize") throw new InvalidOperationException("Injected initialize failure.");
            }
            public override void Start(RuntimeHostContext context)
            {
                _log.Add("start:" + ModuleId);
                if (_fault == "start") throw new InvalidOperationException("Injected start failure.");
            }
            public override void Stop(RuntimeHostContext context)
            {
                _log.Add("stop:" + ModuleId);
                if (_fault == "stop") throw new InvalidOperationException("Injected stop failure.");
            }
            public override void Dispose()
            {
                _log.Add("dispose:" + ModuleId);
                if (_fault == "dispose") throw new InvalidOperationException("Injected dispose failure.");
            }
        }

        private sealed class LiveTickFaultModule : RuntimeModule
        {
            public LiveTickFaultModule() : base("zz-live-tick-fault") { }
            public override void Tick(RuntimeTickContext context) =>
                throw new InvalidOperationException("Injected live Tick failure.");
        }

        private sealed class TestOperation : IApplicationStartupStepOperation, IApplicationStartupFaultDrainOperation
        {
            private readonly RuntimeOperationStatus _terminal;
            private readonly Action _action;
            private readonly bool _cancelCompletes;
            private int _ticksRemaining;
            private readonly bool _tickThrows;
            private readonly RuntimeOperationStatus _faultDrainTerminal;
            private readonly bool _requestDrainThrows;
            private readonly System.Collections.Generic.List<string> _log;
            private readonly string _logPrefix;
            private int _faultDrainTickThrowsRemaining;
            public TestOperation(
                string id,
                RuntimeOperationStatus terminal,
                Action action,
                int ticksToTerminal = 1,
                bool cancelCompletes = true,
                bool tickThrows = false,
                RuntimeOperationStatus faultDrainTerminal = RuntimeOperationStatus.Failed,
                bool requestDrainThrows = false,
                System.Collections.Generic.List<string> log = null,
                string logPrefix = "operation",
                bool faultDrainTickThrows = false)
            {
                OperationId = id;
                _terminal = terminal;
                _action = action;
                _ticksRemaining = ticksToTerminal < 1 ? 1 : ticksToTerminal;
                _cancelCompletes = cancelCompletes;
                _tickThrows = tickThrows;
                _faultDrainTerminal = faultDrainTerminal;
                _requestDrainThrows = requestDrainThrows;
                _log = log;
                _logPrefix = logPrefix;
                _faultDrainTickThrowsRemaining = faultDrainTickThrows ? 1 : 0;
                Status = RuntimeOperationStatus.Running;
            }
            public string OperationId { get; }
            public RuntimeOperationStatus Status { get; private set; }
            public int TickCount { get; private set; }
            public float Progress => Status == RuntimeOperationStatus.Running ? 0f : 1f;
            public RuntimeOperationError Error => Status == RuntimeOperationStatus.Failed
                ? new RuntimeOperationError("TEST_FAILURE", "Injected test failure.")
                : RuntimeOperationError.None;
            public bool CanCancel => true;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;
            public void Tick(in ApplicationStartupTickContext context)
            {
                if (Status != RuntimeOperationStatus.Running) return;
                TickCount++;
                if (_tickThrows)
                {
                    _log?.Add(_logPrefix + ":tick-throw");
                    throw new InvalidOperationException("Injected nested Tick failure.");
                }
                _ticksRemaining--;
                if (_ticksRemaining > 0) return;
                _action?.Invoke();
                Status = _terminal;
            }
            public void RequestCancel()
            {
                if (_cancelCompletes) Status = RuntimeOperationStatus.Cancelled;
            }
            public void RequestFaultDrain(Exception exception)
            {
                _log?.Add(_logPrefix + ":request-drain");
                if (_requestDrainThrows) throw new InvalidOperationException("Injected RequestFaultDrain failure.");
            }
            public void TickFaultDrain(in ApplicationStartupTickContext context)
            {
                _log?.Add(_logPrefix + ":tick-drain");
                if (_faultDrainTickThrowsRemaining > 0)
                {
                    _faultDrainTickThrowsRemaining--;
                    throw new InvalidOperationException("Injected TickFaultDrain failure.");
                }
                Status = _faultDrainTerminal;
            }
        }

        private sealed class TestSceneFlowDriver : IApplicationCandidateOwnedSceneDriver
        {
            private readonly Queue<TestSceneFlowOperation> _loads = new Queue<TestSceneFlowOperation>();
            private readonly Queue<TestSceneFlowOperation> _unloads = new Queue<TestSceneFlowOperation>();
            private readonly bool _throwLoadStart;
            private readonly bool _throwUnloadStart;
            private readonly bool _nullLoadStart;
            private readonly bool _nullUnloadStart;

            public TestSceneFlowDriver(
                TestSceneFlowOperation load = null,
                TestSceneFlowOperation unload = null,
                bool throwLoadStart = false,
                bool throwUnloadStart = false,
                bool nullLoadStart = false,
                bool nullUnloadStart = false,
                ApplicationCandidateSceneOwnershipIdentity ownershipIdentity = null)
            {
                if (load != null) _loads.Enqueue(load);
                if (unload != null) _unloads.Enqueue(unload);
                _throwLoadStart = throwLoadStart;
                _throwUnloadStart = throwUnloadStart;
                _nullLoadStart = nullLoadStart;
                _nullUnloadStart = nullUnloadStart;
                SceneOwnershipIdentity = ownershipIdentity ?? new ApplicationCandidateSceneOwnershipIdentity();
            }

            public ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity { get; }
            public int LoadCount { get; private set; }
            public int UnloadCount { get; private set; }
            public SceneFlowRequest LastRequest { get; private set; }

            public ISceneFlowOperation LoadScene(SceneFlowRequest request)
            {
                LoadCount++;
                LastRequest = request;
                if (_throwLoadStart) throw new InvalidOperationException("Injected load start failure.");
                if (_nullLoadStart) return null;
                return _loads.Count > 0 ? _loads.Dequeue() : new TestSceneFlowOperation(request.SceneKey);
            }

            public ISceneFlowOperation UnloadScene(string sceneKey)
            {
                UnloadCount++;
                if (_throwUnloadStart) throw new InvalidOperationException("Injected unload start failure.");
                if (_nullUnloadStart) return null;
                return _unloads.Count > 0 ? _unloads.Dequeue() : new TestSceneFlowOperation(sceneKey);
            }
        }

        private sealed class NullOwnershipSceneFlowDriver : IApplicationCandidateOwnedSceneDriver
        {
            public ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity => null;
            public ISceneFlowOperation LoadScene(SceneFlowRequest request) =>
                new TestSceneFlowOperation(request.SceneKey);
            public ISceneFlowOperation UnloadScene(string sceneKey) =>
                new TestSceneFlowOperation(sceneKey);
        }

        private sealed class TestSceneFlowOperation : ISceneFlowOperation
        {
            private SceneFlowError _error;
            private bool _done;
            private bool _success;
            private int _throwIsDoneRemaining;
            private int _throwProgressRemaining;
            private float _progress;

            public TestSceneFlowOperation(
                string sceneKey,
                float progress = 0f,
                int throwIsDoneCount = 0,
                int throwProgressCount = 0)
            {
                SceneKey = sceneKey;
                _progress = progress;
                _throwIsDoneRemaining = throwIsDoneCount;
                _throwProgressRemaining = throwProgressCount;
                _error = SceneFlowError.None;
            }

            public string SceneKey { get; }
            public int IsDoneReadCount { get; private set; }
            public int TerminalReadCount { get; private set; }
            public bool IsDone
            {
                get
                {
                    IsDoneReadCount++;
                    if (_throwIsDoneRemaining > 0)
                    {
                        _throwIsDoneRemaining--;
                        throw new InvalidOperationException("Injected IsDone getter fault.");
                    }
                    return _done;
                }
            }
            public float Progress
            {
                get
                {
                    if (_throwProgressRemaining > 0)
                    {
                        _throwProgressRemaining--;
                        throw new InvalidOperationException("Injected Progress getter fault.");
                    }
                    return _progress;
                }
            }
            public bool Success
            {
                get
                {
                    TerminalReadCount++;
                    if (!_done) throw new InvalidOperationException("Success read before terminal.");
                    return _success;
                }
            }
            public SceneFlowError Error
            {
                get
                {
                    TerminalReadCount++;
                    if (!_done) throw new InvalidOperationException("Error read before terminal.");
                    return _error;
                }
            }

            public void CompleteSuccess()
            {
                _done = true;
                _success = true;
                _progress = 1f;
                _error = SceneFlowError.None;
            }

            public void CompleteFailure(SceneFlowError error)
            {
                _done = true;
                _success = false;
                _progress = 1f;
                _error = error;
            }
        }

        private sealed class TestSceneIsolationGate : IApplicationCandidateSceneIsolationGate
        {
            private readonly bool _failEnter;
            private int _throwInertAfterEnter;
            private int _throwOwnedScene;
            private bool _isInert;
            private readonly bool _hasOwnedScene;
            private readonly bool _failMakeInert;
            private ApplicationCandidateSceneOwnershipIdentity _ownershipIdentity;

            public TestSceneIsolationGate(
                bool failEnter = false,
                int throwInertAfterEnter = 0,
                bool hasOwnedScene = false,
                int throwOwnedScene = 0,
                ApplicationCandidateSceneOwnershipIdentity ownershipIdentity = null,
                bool failMakeInert = false)
            {
                _failEnter = failEnter;
                _throwInertAfterEnter = throwInertAfterEnter;
                _hasOwnedScene = hasOwnedScene;
                _throwOwnedScene = throwOwnedScene;
                _ownershipIdentity = ownershipIdentity;
                _failMakeInert = failMakeInert;
                _isInert = true;
            }

            public bool IsIsolated { get; private set; }
            public bool IsInert
            {
                get
                {
                    if (EnterCount > 0 && _throwInertAfterEnter > 0)
                    {
                        _throwInertAfterEnter--;
                        throw new InvalidOperationException("Injected IsInert getter fault.");
                    }
                    return _isInert;
                }
            }
            public bool HasOwnedScene
            {
                get
                {
                    if (_throwOwnedScene > 0)
                    {
                        _throwOwnedScene--;
                        throw new InvalidOperationException("Injected HasOwnedScene getter fault.");
                    }
                    return _hasOwnedScene;
                }
            }
            public ApplicationCandidateSceneOwnershipIdentity SceneOwnershipIdentity => _ownershipIdentity;
            public int EnterCount { get; private set; }
            public int InertCount { get; private set; }

            public void BindOwnershipIdentity(ApplicationCandidateSceneOwnershipIdentity identity)
            {
                if (_ownershipIdentity == null) _ownershipIdentity = identity;
            }

            public void OverrideOwnershipIdentity(ApplicationCandidateSceneOwnershipIdentity identity) =>
                _ownershipIdentity = identity;

            public ApplicationCandidateSceneIsolationResult TryEnterIsolation(
                in ApplicationCandidateSceneRequest request)
            {
                EnterCount++;
                if (_failEnter)
                    return ApplicationCandidateSceneIsolationResult.Failed(
                        ApplicationCandidateSceneIsolationErrorCode.UnsupportedCandidateScene,
                        "Injected isolation rejection.");
                IsIsolated = true;
                _isInert = false;
                return ApplicationCandidateSceneIsolationResult.Succeeded;
            }

            public ApplicationCandidateSceneIsolationResult MakeInert()
            {
                InertCount++;
                if (_failMakeInert)
                    return ApplicationCandidateSceneIsolationResult.Failed(
                        ApplicationCandidateSceneIsolationErrorCode.MakeInertFailed,
                        "Injected make-inert failure.");
                _isInert = true;
                IsIsolated = false;
                return ApplicationCandidateSceneIsolationResult.Succeeded;
            }
        }

        private sealed class EmptyState : IAppFlowState
        {
            public string StateId => "initial";
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) { }
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }

        private sealed class TestTransaction : IApplicationCandidateSceneTransaction
        {
            public TestTransaction() : this(new ApplicationCandidateSessionOwnerClaim()) { }
            public TestTransaction(ApplicationCandidateSessionOwnerClaim claim) =>
                CandidateSessionClaim = claim ?? throw new ArgumentNullException(nameof(claim));
            public TestTransaction(ApplicationCandidateSessionGraph graph) : this()
            {
                CandidateSessionClaim.TryClaim(this);
                CandidateSessionClaim.Attach(this, graph);
            }
            public string TransactionId => "test-transaction";
            public ApplicationCandidateSessionOwnerClaim CandidateSessionClaim { get; }
            public ApplicationCandidateSceneTransactionPort CandidateScenePort { get; } =
                new ApplicationCandidateSceneTransactionPort();
            public ApplicationStartupCommitObservation CommitObservation => ApplicationStartupCommitObservation.NotPublished;
            public ApplicationStartupCommitResult TryCommit(LiveApplicationState expected, ILiveApplicationStatePublisher publisher) =>
                new ApplicationStartupCommitResult(
                    ApplicationStartupCommitStatus.Failed,
                    new ApplicationStartupError(ApplicationStartupErrorCode.CommitFailed, ApplicationStartupPhase.Commit, default, default, null, "Not used.", null));
            public ApplicationStartupRollbackResult FinalizeRollback() => ApplicationStartupRollbackResult.Succeeded;
            public void Dispose() { }
        }

        private sealed class CandidateTransactionFactory : IApplicationStartupTransactionFactory
        {
            private readonly ApplicationCandidateSessionFactory _factory;
            public CandidateTransactionFactory(IApplicationCandidateSessionGraphBuilder builder) =>
                _factory = new ApplicationCandidateSessionFactory(builder);
            public System.Collections.Generic.List<ApplicationCandidateSessionGraph> Graphs { get; } =
                new System.Collections.Generic.List<ApplicationCandidateSessionGraph>();
            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId)
            {
                var transaction = new TestTransaction();
                ApplicationCandidateSessionGraph graph = _factory.Create(transaction);
                Graphs.Add(graph);
                return transaction;
            }
        }
    }
}
