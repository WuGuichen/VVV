using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.ApplicationStartup;
using MxFramework.Runtime;
using NUnit.Framework;
using static MxFramework.Tests.ApplicationStartup.ApplicationStartupPlanTests;

namespace MxFramework.Tests.ApplicationStartup
{
    public class ApplicationStartupCoordinatorTests
    {
        [Test]
        public void PublisherBindingProof_RequiresExactConcretePublisher()
        {
            var expected = new LiveApplicationStatePublisher();
            var foreign = new LiveApplicationStatePublisher();
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(),
                    new RecordingStep("ready", null, RuntimeOperationStatus.Succeeded)),
                expected);

            Assert.That(coordinator.UsesPublisher(expected), Is.True);
            Assert.That(coordinator.UsesPublisher(foreign), Is.False);
            Assert.That(coordinator.UsesPublisher(null), Is.False);
        }

        [Test]
        public void Run_AllStepsSucceed_CommitsExactlyOnce()
        {
            var log = new List<string>();
            var factory = new RecordingTransactionFactory();
            ApplicationStartupPlan plan = CreatePlan(factory,
                new RecordingStep("a", log, RuntimeOperationStatus.Succeeded),
                new RecordingStep("b", log, RuntimeOperationStatus.Succeeded));
            var publisher = new LiveApplicationStatePublisher();
            var coordinator = new ApplicationStartupCoordinator(plan, publisher);

            coordinator.Start("run-1");
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Succeeded, coordinator.State);
            Assert.AreEqual(1, factory.Last.CommitCount);
            Assert.AreEqual(0, factory.Last.RollbackCount);
            Assert.AreEqual(1, publisher.Current.Revision);
            CollectionAssert.AreEqual(new[] { "begin:a", "begin:b" }, log);
            Assert.AreEqual(1f, coordinator.ForwardProgress);
        }

        [Test]
        public void PublicationDeferred_RetriesWithoutRollbackOrCandidateMutation()
        {
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            var factory = new RecordingTransactionFactory();
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory, new RecordingStep("ready", null, RuntimeOperationStatus.Succeeded)),
                publisher);
            Assert.That(fence.TryAcquireSave(out ApplicationPublicationSaveFenceLease save), Is.True);
            coordinator.Start("deferred");

            for (int i = 0; i < 6; i++) Tick(coordinator);
            Assert.That(coordinator.State, Is.EqualTo(ApplicationStartupState.Running));
            Assert.That(factory.Last.CommitCount, Is.GreaterThan(0));
            Assert.That(factory.Last.RollbackCount, Is.Zero);
            Assert.That(publisher.Current.Revision, Is.Zero);

            save.Dispose();
            PumpToTerminal(coordinator);
            Assert.That(coordinator.State, Is.EqualTo(ApplicationStartupState.Succeeded));
            Assert.That(factory.Last.RollbackCount, Is.Zero);
            Assert.That(publisher.Current.Revision, Is.EqualTo(1L));
        }

        [Test]
        public void Run_OperationAlreadyTerminal_IsCompletedWithoutAnotherTick()
        {
            var operation = new FakeOperation(RuntimeOperationStatus.Succeeded, int.MaxValue, throwOnTickAfterTerminal: true);
            operation.CompleteExternally(RuntimeOperationStatus.Succeeded);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), new ConfigurableStep("sync", operation)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-sync");

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Succeeded, coordinator.State);
        }

        [Test]
        public void ForwardTickException_DrainsOwnedOperationBeforeRollback()
        {
            var log = new List<string>();
            var first = new RecordingStep("first", log, RuntimeOperationStatus.Succeeded);
            var faulted = new FaultDrainOperation(log, "fault", 2, RuntimeOperationStatus.Failed);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), first, new ConfigurableStep("fault", faulted)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-fault-drain");
            Tick(coordinator);
            Tick(coordinator);

            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            Assert.AreSame(faulted, coordinator.CurrentOperation);
            CollectionAssert.DoesNotContain(log, "rollback:first");

            Tick(coordinator);
            Assert.AreSame(faulted, coordinator.CurrentOperation);
            CollectionAssert.DoesNotContain(log, "rollback:first");
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Failed, coordinator.State);
            CollectionAssert.AreEqual(new[] { "begin:first", "tick:fault", "abort:fault", "drain:fault", "drain:fault", "rollback:first" }, log);
        }

        [Test]
        public void RollbackTickException_DrainsOwnedCompensationBeforeFinalize()
        {
            var log = new List<string>();
            var factory = new RecordingTransactionFactory();
            var rollback = new FaultDrainOperation(log, "rollback", 2, RuntimeOperationStatus.Succeeded);
            var completed = new DualOperationStep("completed", new FakeOperation(RuntimeOperationStatus.Succeeded, 1), rollback);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory, completed, new RecordingStep("fail", null, RuntimeOperationStatus.Failed)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-rollback-fault-drain");
            Tick(coordinator);
            Tick(coordinator);
            Tick(coordinator);

            Assert.AreEqual(ApplicationStartupState.RollingBack, coordinator.State);
            Assert.AreSame(rollback, coordinator.CurrentOperation);
            Assert.AreEqual(0, factory.Last.RollbackCount);

            Tick(coordinator);
            Assert.AreSame(rollback, coordinator.CurrentOperation);
            Assert.AreEqual(0, factory.Last.RollbackCount);
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.RollbackFailed, coordinator.State);
            Assert.AreEqual(1, factory.Last.RollbackCount);
            CollectionAssert.AreEqual(new[] { "tick:rollback", "abort:rollback", "drain:rollback", "drain:rollback" }, log);
        }

        [Test]
        public void Run_MiddleStepFails_RollsBackSucceededStepsInReverse()
        {
            var log = new List<string>();
            ApplicationStartupPlan plan = CreatePlan(new RecordingTransactionFactory(),
                new RecordingStep("a", log, RuntimeOperationStatus.Succeeded),
                new RecordingStep("b", log, RuntimeOperationStatus.Failed),
                new RecordingStep("c", log, RuntimeOperationStatus.Succeeded));
            var coordinator = new ApplicationStartupCoordinator(plan, new LiveApplicationStatePublisher());

            coordinator.Start("run-1");
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Failed, coordinator.State);
            Assert.AreEqual(ApplicationStartupErrorCode.StepFailed, coordinator.ForwardError.Code);
            CollectionAssert.AreEqual(new[] { "begin:a", "begin:b", "rollback:a" }, log);
        }

        [Test]
        public void Cancel_NonCancellableCurrentStep_DrainsBeforeRollbackAndStartsNoLaterStep()
        {
            var log = new List<string>();
            var first = new RecordingStep("a", log, RuntimeOperationStatus.Succeeded);
            var draining = new RecordingStep("b", log, RuntimeOperationStatus.Succeeded, ticksToTerminal: 3, canCancel: false);
            var later = new RecordingStep("c", log, RuntimeOperationStatus.Succeeded);
            var coordinator = new ApplicationStartupCoordinator(CreatePlan(new RecordingTransactionFactory(), first, draining, later), new LiveApplicationStatePublisher());
            coordinator.Start("run-1");
            Tick(coordinator);
            Tick(coordinator);
            coordinator.RequestCancel();

            Assert.AreEqual(ApplicationStartupState.Running, coordinator.State);
            Tick(coordinator);
            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Cancelled, coordinator.State);
            CollectionAssert.AreEqual(new[] { "begin:a", "begin:b", "rollback:a" }, log);
        }

        [Test]
        public void Cancel_CancellableOperationCompletesSynchronously_IsNotTickedAfterTerminal()
        {
            var step = new ConfigurableStep("cancel", new FakeOperation(
                RuntimeOperationStatus.Succeeded,
                int.MaxValue,
                canCancel: true,
                cancelCompletes: true,
                throwOnTickAfterTerminal: true));
            var coordinator = new ApplicationStartupCoordinator(CreatePlan(new RecordingTransactionFactory(), step), new LiveApplicationStatePublisher());
            coordinator.Start("run-cancel");
            Tick(coordinator);
            coordinator.RequestCancel();

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Cancelled, coordinator.State);
            Assert.AreEqual(RuntimeOperationStatus.Cancelled, coordinator.DrainedOperationStatus);
        }

        [Test]
        public void Cancel_OperationAlreadyTerminalBeforeDispatch_DoesNotRequestCancelOrTick()
        {
            var operation = new FakeOperation(
                RuntimeOperationStatus.Succeeded,
                int.MaxValue,
                canCancel: true,
                throwOnCancel: true,
                throwOnTickAfterTerminal: true);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), new ConfigurableStep("cancel", operation)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-external-terminal");
            Tick(coordinator);
            coordinator.RequestCancel();
            operation.CompleteExternally(RuntimeOperationStatus.Succeeded);

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Cancelled, coordinator.State);
            Assert.IsFalse(operation.CancelWasRequested);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, coordinator.DrainedOperationStatus);
        }

        [Test]
        public void Cancel_RequestCancelThrows_KeepsOwnershipUntilOperationTerminates()
        {
            var operation = new FakeOperation(RuntimeOperationStatus.Succeeded, 3, canCancel: true, cancelCompletes: false, throwOnCancel: true);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), new ConfigurableStep("cancel", operation)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-cancel-throw");
            Tick(coordinator);
            coordinator.RequestCancel();

            Tick(coordinator);
            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            Assert.AreSame(operation, coordinator.CurrentOperation);
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Cancelled, coordinator.State);
            Assert.IsInstanceOf<InvalidOperationException>(coordinator.ForwardError.Exception);
            Assert.AreEqual(RuntimeOperationStatus.Succeeded, coordinator.DrainedOperationStatus);
        }

        [Test]
        public void Timeout_LatchesOuterOutcomeAndPreservesInnerDrain()
        {
            var log = new List<string>();
            var slow = new RecordingStep("slow", log, RuntimeOperationStatus.Succeeded, ticksToTerminal: 3, canCancel: false, timeout: 0.5d);
            var coordinator = new ApplicationStartupCoordinator(CreatePlan(new RecordingTransactionFactory(), slow), new LiveApplicationStatePublisher());
            coordinator.Start("run-1");

            Tick(coordinator, 0.3d);
            Tick(coordinator, 0.3d);
            Assert.AreEqual(ApplicationStartupState.Draining, coordinator.State);
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.TimedOut, coordinator.State);
            Assert.AreEqual(ApplicationStartupErrorCode.Timeout, coordinator.ForwardError.Code);
        }

        [Test]
        public void Timeout_DrainedFailurePreservesTerminalEvidence()
        {
            var detail = new FakeInnerDetail("resource", "read.failed");
            var operation = new FakeOperation(RuntimeOperationStatus.Failed, 3, canCancel: false, innerErrorDetail: detail);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), new ConfigurableStep("slow", operation, timeout: 0.5d)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-timeout-failure");

            Tick(coordinator, 0.3d);
            Tick(coordinator, 0.3d);
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.TimedOut, coordinator.State);
            Assert.AreEqual(ApplicationStartupErrorCode.Timeout, coordinator.ForwardError.Code);
            Assert.AreEqual(RuntimeOperationStatus.Failed, coordinator.DrainedOperationStatus);
            Assert.AreEqual("fake.failed", coordinator.DrainedOperationError.Code);
            Assert.AreSame(detail, coordinator.DrainedInnerDetail);
            Assert.AreSame(detail, coordinator.ForwardError.InnerDetail);
        }

        [Test]
        public void RollbackFailure_DoesNotStopRemainingRollback()
        {
            var log = new List<string>();
            var a = new RecordingStep("a", log, RuntimeOperationStatus.Succeeded, rollbackStatus: RuntimeOperationStatus.Succeeded);
            var b = new RecordingStep("b", log, RuntimeOperationStatus.Succeeded, rollbackStatus: RuntimeOperationStatus.Failed);
            var c = new RecordingStep("c", log, RuntimeOperationStatus.Failed);
            var coordinator = new ApplicationStartupCoordinator(CreatePlan(new RecordingTransactionFactory(), a, b, c), new LiveApplicationStatePublisher());
            coordinator.Start("run-1");
            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.RollbackFailed, coordinator.State);
            Assert.AreEqual(1, coordinator.RollbackErrorCount);
            CollectionAssert.AreEqual(new[] { "begin:a", "begin:b", "begin:c", "rollback:b", "rollback:a" }, log);
        }

        [Test]
        public void Retry_RequiresFreshCoordinatorAndTransaction()
        {
            var factory = new RecordingTransactionFactory();
            ApplicationStartupPlan plan = CreatePlan(factory, new RecordingStep("a", new List<string>(), RuntimeOperationStatus.Failed));
            var first = new ApplicationStartupCoordinator(plan, new LiveApplicationStatePublisher());
            first.Start("run-1");
            PumpToTerminal(first);
            Assert.Throws<InvalidOperationException>(() => first.Start("run-1-again"));

            var second = new ApplicationStartupCoordinator(plan, new LiveApplicationStatePublisher());
            second.Start("run-2");
            Assert.AreEqual(2, factory.CreateCount);
            Assert.AreNotSame(factory.Created[0], factory.Created[1]);
        }

        [Test]
        public void LivePublisher_PublishesOneImmutableAggregateAndRejectsStaleExpected()
        {
            var publisher = new LiveApplicationStatePublisher(2);
            LiveApplicationState boot = publisher.Current;
            var firstLease = new FakeLease("session-1");
            var firstCandidate = LiveApplicationState.CreateCandidate(true, 42, firstLease);

            ApplicationStartupCommitResult first = publisher.TryPublish(boot, firstCandidate);

            Assert.IsTrue(first.Success);
            Assert.AreEqual(1, publisher.Current.Revision);
            Assert.AreEqual(42, publisher.Current.NextRuntimeFrame);
            Assert.AreEqual("session-1", publisher.Current.Session.SessionId);
            Assert.IsFalse(publisher.Current.Session is IDisposable);
            Assert.AreEqual(1, publisher.RetiredCount);

            var staleCandidate = LiveApplicationState.CreateCandidate(true, 100, new FakeLease("stale"));
            ApplicationStartupCommitResult stale = publisher.TryPublish(boot, staleCandidate);
            Assert.AreEqual(ApplicationStartupCommitStatus.PublicationConflict, stale.Status);
            Assert.AreEqual("session-1", publisher.Current.Session.SessionId);

            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement));
            Assert.AreSame(boot, retirement.State);
            retirement.Dispose();
            retirement.Dispose();

            var secondCandidate = LiveApplicationState.CreateCandidate(true, 101, new FakeLease("session-2"));
            Assert.IsTrue(publisher.TryPublish(publisher.Current, secondCandidate).Success);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long beforeDequeue = GC.GetAllocatedBytesForCurrentThread();
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement firstRetirement));
            long dequeueAllocated = GC.GetAllocatedBytesForCurrentThread() - beforeDequeue;
            Assert.AreEqual(0L, dequeueAllocated);

            LiveApplicationState aliasedRetired = LiveApplicationState.CreateCandidate(true, 102, firstLease);
            ApplicationStartupCommitResult outstandingAlias = publisher.TryPublish(publisher.Current, aliasedRetired);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, outstandingAlias.Status);
            Assert.AreEqual(2, publisher.Current.Revision);
            firstRetirement.Dispose();
            firstRetirement.Dispose();
            Assert.AreEqual(1, firstLease.DisposeCount);
        }

        [Test]
        public void LivePublisher_RuntimeCandidateWithoutPumpFailsBeforeReservationOrRetirement()
        {
            var publisher = new LiveApplicationStatePublisher();
            LiveApplicationState boot = publisher.Current;
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                true, 1, new NonPumpLease("missing-pump"));

            ApplicationStartupCommitResult result = publisher.TryPublish(boot, candidate);

            Assert.That(result.Success, Is.False);
            Assert.That(publisher.Current, Is.SameAs(boot));
            Assert.That(publisher.RetiredCount, Is.Zero);
            Assert.That(candidate.TryReservePublication(), Is.True);
            candidate.ReleasePublicationReservation();
        }

        [Test]
        public void LivePublisher_RejectsConsumedCandidateAndOwnedLeaseAliases()
        {
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState boot = publisher.Current;
            var firstLease = new FakeLease("first");
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(true, 1, firstLease);
            Assert.IsTrue(publisher.TryPublish(boot, candidate).Success);

            ApplicationStartupCommitResult reused = publisher.TryPublish(publisher.Current, candidate);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, reused.Status);
            Assert.AreEqual(1, publisher.Current.Revision);

            var secondPublisher = new LiveApplicationStatePublisher();
            ApplicationStartupCommitResult crossPublisherReuse = secondPublisher.TryPublish(secondPublisher.Current, candidate);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, crossPublisherReuse.Status);
            Assert.AreEqual(0, secondPublisher.Current.Revision);

            var secondLease = new FakeLease("second");
            Assert.IsTrue(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(true, 2, secondLease)).Success);
            LiveApplicationState currentAlias = LiveApplicationState.CreateCandidate(true, 3, secondLease);
            ApplicationStartupCommitResult aliasCurrent = publisher.TryPublish(publisher.Current, currentAlias);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, aliasCurrent.Status);

            LiveApplicationState retiredAlias = LiveApplicationState.CreateCandidate(true, 3, firstLease);
            ApplicationStartupCommitResult aliasRetired = publisher.TryPublish(publisher.Current, retiredAlias);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, aliasRetired.Status);
            Assert.AreEqual(2, publisher.Current.Revision);
        }

        [Test]
        public void LivePublisher_StaleExpectedRace_ClaimsExactlyOneAndLeavesLoserReusable()
        {
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState expected = publisher.Current;
            var firstHandoff = new RecordingPublicationHandoff();
            var secondHandoff = new RecordingPublicationHandoff();
            LiveApplicationState firstCandidate = LiveApplicationState.CreateCandidate(
                true, 1, new FakeLease("first-racer"), firstHandoff);
            LiveApplicationState secondCandidate = LiveApplicationState.CreateCandidate(
                true, 1, new FakeLease("second-racer"), secondHandoff);
            ApplicationStartupCommitResult first = default;
            ApplicationStartupCommitResult second = default;

            Parallel.Invoke(
                () => first = publisher.TryPublish(expected, firstCandidate),
                () => second = publisher.TryPublish(expected, secondCandidate));

            Assert.AreEqual(1, (first.Success ? 1 : 0) + (second.Success ? 1 : 0));
            Assert.AreEqual(1, firstHandoff.ClaimCount + secondHandoff.ClaimCount);
            LiveApplicationState losingCandidate = first.Success ? secondCandidate : firstCandidate;
            RecordingPublicationHandoff losingHandoff = first.Success ? secondHandoff : firstHandoff;
            Assert.AreEqual(0, losingHandoff.PrepareCount);
            Assert.AreEqual(0, losingHandoff.ClaimCount);

            ApplicationStartupCommitResult retry = publisher.TryPublish(publisher.Current, losingCandidate);

            Assert.IsTrue(retry.Success);
            Assert.AreEqual(1, losingHandoff.PrepareCount);
            Assert.AreEqual(1, losingHandoff.ClaimCount);
            Assert.AreEqual(2, publisher.Current.Revision);
        }

        [Test]
        public void LivePublisher_HandoffPreflightFailure_DoesNotConsumeCandidate()
        {
            var publisher = new LiveApplicationStatePublisher(2);
            var handoff = new RecordingPublicationHandoff
            {
                AllowPrepare = false
            };
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                true, 1, new FakeLease("preflight"), handoff);

            ApplicationStartupCommitResult failed = publisher.TryPublish(publisher.Current, candidate);

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, failed.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CommitFailed, failed.Error.Code);
            Assert.AreEqual(1, handoff.PrepareCount);
            Assert.AreEqual(0, handoff.ClaimCount);
            Assert.AreEqual(0, publisher.Current.Revision);

            handoff.AllowPrepare = true;
            ApplicationStartupCommitResult retry = publisher.TryPublish(publisher.Current, candidate);

            Assert.IsTrue(retry.Success);
            Assert.AreSame(retry.PublishedState, publisher.Current);
            Assert.AreEqual(2, handoff.PrepareCount);
            Assert.AreEqual(1, handoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_HandoffPreflightException_DoesNotConsumeCandidate()
        {
            var publisher = new LiveApplicationStatePublisher(2);
            var handoff = new RecordingPublicationHandoff
            {
                ThrowOnPrepare = true
            };
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                true, 1, new FakeLease("preflight-exception"), handoff);

            ApplicationStartupCommitResult failed = publisher.TryPublish(publisher.Current, candidate);

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, failed.Status);
            Assert.IsInstanceOf<InvalidOperationException>(failed.Error.Exception);
            Assert.AreEqual(0, handoff.ClaimCount);
            Assert.AreEqual(0, publisher.Current.Revision);

            handoff.ThrowOnPrepare = false;
            Assert.IsTrue(publisher.TryPublish(publisher.Current, candidate).Success);
            Assert.AreEqual(1, handoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_FinalClaimContradictionFailsBeforeCurrentCas()
        {
            var publisher = new LiveApplicationStatePublisher(2);
            LiveApplicationState expected = publisher.Current;
            var handoff = new RecordingPublicationHandoff
            {
                AllowClaim = false
            };
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                true, 1, new FakeLease("claim-contradiction"), handoff);

            ApplicationStartupCommitResult result = publisher.TryPublish(expected, candidate);

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, result.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CommitFailed, result.Error.Code);
            Assert.AreSame(expected, publisher.Current);
            Assert.AreEqual(1, handoff.PrepareCount);
            Assert.AreEqual(1, handoff.ClaimAttemptCount);
            Assert.AreEqual(0, handoff.ClaimCount);
            Assert.AreEqual(0, publisher.RetiredCount);
        }

        [Test]
        public void LivePublisher_HandoffPreflightReentrantSamePublisher_FailsNestedBeforeMutationAndOuterPublishes()
        {
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState expected = publisher.Current;
            var nestedHandoff = new RecordingPublicationHandoff();
            LiveApplicationState nestedCandidate = LiveApplicationState.CreateCandidate(
                true, 2, new FakeLease("nested"), nestedHandoff);
            ApplicationStartupCommitResult nestedResult = default;
            var outerHandoff = new RecordingPublicationHandoff
            {
                PrepareAction = () => nestedResult = publisher.TryPublish(expected, nestedCandidate)
            };

            ApplicationStartupCommitResult outerResult = publisher.TryPublish(
                expected,
                LiveApplicationState.CreateCandidate(true, 1, new FakeLease("outer"), outerHandoff));

            Assert.IsTrue(outerResult.Success);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, nestedResult.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CommitFailed, nestedResult.Error.Code);
            Assert.AreSame(outerResult.PublishedState, publisher.Current);
            Assert.AreEqual(1, publisher.Current.Revision);
            Assert.AreEqual(0, nestedHandoff.PrepareCount);
            Assert.AreEqual(0, nestedHandoff.ClaimCount);

            ApplicationStartupCommitResult retry = publisher.TryPublish(publisher.Current, nestedCandidate);

            Assert.IsTrue(retry.Success);
            Assert.AreEqual(2, publisher.Current.Revision);
            Assert.AreEqual(1, nestedHandoff.PrepareCount);
            Assert.AreEqual(1, nestedHandoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_HandoffPreflight_CanPublishThroughDifferentPublisher()
        {
            var outerPublisher = new LiveApplicationStatePublisher(2);
            var nestedPublisher = new LiveApplicationStatePublisher(2);
            var nestedHandoff = new RecordingPublicationHandoff();
            ApplicationStartupCommitResult nestedResult = default;
            var outerHandoff = new RecordingPublicationHandoff
            {
                PrepareAction = () => nestedResult = nestedPublisher.TryPublish(
                    nestedPublisher.Current,
                    LiveApplicationState.CreateCandidate(true, 1, new FakeLease("nested-publisher"), nestedHandoff))
            };

            ApplicationStartupCommitResult outerResult = outerPublisher.TryPublish(
                outerPublisher.Current,
                LiveApplicationState.CreateCandidate(true, 1, new FakeLease("outer-publisher"), outerHandoff));

            Assert.IsTrue(nestedResult.Success);
            Assert.IsTrue(outerResult.Success);
            Assert.AreEqual(1, nestedPublisher.Current.Revision);
            Assert.AreEqual(1, outerPublisher.Current.Revision);
            Assert.AreEqual(1, nestedHandoff.PrepareCount);
            Assert.AreEqual(1, nestedHandoff.ClaimCount);
            Assert.AreEqual(1, outerHandoff.PrepareCount);
            Assert.AreEqual(1, outerHandoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_HandoffClaimReentrantSamePublisher_FailsNestedBeforeMutationAndOuterPublishes()
        {
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState expected = publisher.Current;
            var nestedHandoff = new RecordingPublicationHandoff();
            LiveApplicationState nestedCandidate = LiveApplicationState.CreateCandidate(
                true, 2, new FakeLease("claim-nested"), nestedHandoff);
            ApplicationStartupCommitResult nestedResult = default;
            var outerHandoff = new RecordingPublicationHandoff
            {
                ClaimAction = () => nestedResult = publisher.TryPublish(expected, nestedCandidate)
            };

            ApplicationStartupCommitResult outerResult = publisher.TryPublish(
                expected,
                LiveApplicationState.CreateCandidate(true, 1, new FakeLease("claim-outer"), outerHandoff));

            Assert.IsTrue(outerResult.Success);
            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, nestedResult.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CommitFailed, nestedResult.Error.Code);
            Assert.AreSame(outerResult.PublishedState, publisher.Current);
            Assert.AreEqual(1, publisher.Current.Revision);
            Assert.AreEqual(0, nestedHandoff.PrepareCount);
            Assert.AreEqual(0, nestedHandoff.ClaimCount);

            Assert.IsTrue(publisher.TryPublish(publisher.Current, nestedCandidate).Success);
            Assert.AreEqual(2, publisher.Current.Revision);
            Assert.AreEqual(1, nestedHandoff.PrepareCount);
            Assert.AreEqual(1, nestedHandoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_FullRetirementCapacity_DoesNotConsumeCandidate()
        {
            var publisher = new LiveApplicationStatePublisher(1);
            Assert.IsTrue(publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(true, 1, new FakeLease("first"))).Success);
            var handoff = new RecordingPublicationHandoff();
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                true, 2, new FakeLease("capacity-retry"), handoff);

            ApplicationStartupCommitResult full = publisher.TryPublish(publisher.Current, candidate);

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, full.Status);
            Assert.AreEqual(ApplicationStartupErrorCode.CleanupIncomplete, full.Error.Code);
            Assert.AreEqual(0, handoff.PrepareCount);
            Assert.AreEqual(0, handoff.ClaimCount);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement));
            retirement.Dispose();

            Assert.IsTrue(publisher.TryPublish(publisher.Current, candidate).Success);
            Assert.AreEqual(1, handoff.PrepareCount);
            Assert.AreEqual(1, handoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_RevisionOverflow_DoesNotConsumeCandidate()
        {
            var exhaustedPublisher = new LiveApplicationStatePublisher();
            var exhaustedLease = new FakeLease("exhausted");
            var exhaustedState = (LiveApplicationState)Activator.CreateInstance(
                typeof(LiveApplicationState),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { long.MaxValue, false, -1L, exhaustedLease, null, null, null, null },
                null);
            FieldInfo currentField = typeof(LiveApplicationStatePublisher).GetField(
                "_current",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(currentField);
            currentField.SetValue(exhaustedPublisher, exhaustedState);
            var handoff = new RecordingPublicationHandoff();
            LiveApplicationState candidate = LiveApplicationState.CreateCandidate(
                false, -1L, new FakeLease("overflow-retry"), handoff);

            ApplicationStartupCommitResult overflow = exhaustedPublisher.TryPublish(exhaustedState, candidate);

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, overflow.Status);
            Assert.IsInstanceOf<OverflowException>(overflow.Error.Exception);
            Assert.AreEqual(0, handoff.PrepareCount);
            Assert.AreEqual(0, handoff.ClaimCount);
            Assert.AreSame(exhaustedState, exhaustedPublisher.Current);

            var freshPublisher = new LiveApplicationStatePublisher();
            Assert.IsTrue(freshPublisher.TryPublish(freshPublisher.Current, candidate).Success);
            Assert.AreEqual(1, handoff.PrepareCount);
            Assert.AreEqual(1, handoff.ClaimCount);
        }

        [Test]
        public void LivePublisher_RetirementReservationSurvivesConcurrentDisposeWindow()
        {
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState boot = publisher.Current;
            var retiringLease = new BlockingLease("retiring");
            Assert.IsTrue(publisher.TryPublish(boot, LiveApplicationState.CreateCandidate(true, 1, retiringLease)).Success);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement bootRetirement));
            bootRetirement.Dispose();
            Assert.IsTrue(publisher.TryPublish(publisher.Current, LiveApplicationState.CreateCandidate(true, 2, new FakeLease("current"))).Success);
            Assert.IsTrue(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement));

            Task disposeTask = Task.Run(() => retirement.Dispose());
            Assert.IsTrue(retiringLease.DisposeEntered.Wait(TimeSpan.FromSeconds(5)));
            ApplicationStartupCommitResult alias = publisher.TryPublish(
                publisher.Current,
                LiveApplicationState.CreateCandidate(true, 3, retiringLease));

            Assert.AreEqual(ApplicationStartupCommitStatus.Failed, alias.Status);
            Assert.AreEqual(2, publisher.Current.Revision);
            retiringLease.AllowDispose.Set();
            Assert.IsTrue(disposeTask.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, retiringLease.DisposeCount);
        }

        [Test]
        public void PublicationConflict_RollsBackCandidateAndKeepsPublishedState()
        {
            var publisher = new LiveApplicationStatePublisher();
            var first = new ApplicationStartupCoordinator(
                CreatePlan(new RecordingTransactionFactory(), new RecordingStep("first", null, RuntimeOperationStatus.Succeeded)), publisher);
            var secondFactory = new RecordingTransactionFactory();
            var second = new ApplicationStartupCoordinator(
                CreatePlan(secondFactory, new RecordingStep("second", null, RuntimeOperationStatus.Succeeded)), publisher);
            first.Start("run-first");
            second.Start("run-second");

            PumpToTerminal(first);
            PumpToTerminal(second);

            Assert.AreEqual(ApplicationStartupState.Succeeded, first.State);
            Assert.AreEqual(ApplicationStartupState.Failed, second.State);
            Assert.AreEqual(ApplicationStartupErrorCode.PublicationConflict, second.ForwardError.Code);
            Assert.AreEqual(1, secondFactory.Last.RollbackCount);
            Assert.AreEqual(1, publisher.Current.Revision);
        }

        [Test]
        public void CommittedTransactionDisposeFailure_IsCleanupWarningNotCommitFailure()
        {
            var factory = new RecordingTransactionFactory { ThrowOnCommittedDispose = true };
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory, new RecordingStep("a", null, RuntimeOperationStatus.Succeeded)), new LiveApplicationStatePublisher());
            coordinator.Start("run-warning");

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Succeeded, coordinator.State);
            Assert.AreEqual(1, coordinator.CleanupWarningCount);
            Assert.AreEqual(ApplicationStartupErrorCode.CleanupIncomplete, coordinator.GetCleanupWarning(0).Code);
        }

        [Test]
        public void CommitThrowsAfterPublication_UsesRecordedOutcomeAndNeverRollsBackLiveLease()
        {
            var factory = new RecordingTransactionFactory { ThrowAfterPublish = true };
            var publisher = new LiveApplicationStatePublisher();
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory, new RecordingStep("a", null, RuntimeOperationStatus.Succeeded)), publisher);
            coordinator.Start("run-publish-throw");

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Succeeded, coordinator.State);
            Assert.AreEqual(0, factory.Last.RollbackCount);
            Assert.AreEqual(1, publisher.Current.Revision);
            Assert.AreEqual(0, factory.Last.PublishedLease.DisposeCount);
        }

        [Test]
        public void CommitThrowsAfterPublicationAndConcurrentAdvance_DoesNotRollbackRetiredPublishedLease()
        {
            var factory = new RecordingTransactionFactory { ThrowAfterPublish = true, AdvanceAfterPublish = true };
            var publisher = new LiveApplicationStatePublisher(4);
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory, new RecordingStep("a", null, RuntimeOperationStatus.Succeeded)), publisher);
            coordinator.Start("run-publish-advance-throw");

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.Succeeded, coordinator.State);
            Assert.AreEqual(0, factory.Last.RollbackCount);
            Assert.AreEqual(2, publisher.Current.Revision);
            Assert.AreEqual(0, factory.Last.PublishedLease.DisposeCount);
        }

        [Test]
        public void Rollback_RetainsEveryStepFinalizeAndDisposeError()
        {
            var factory = new RecordingTransactionFactory { FailCommit = true, FailFinalizeRollback = true, ThrowOnRollbackDispose = true };
            var coordinator = new ApplicationStartupCoordinator(
                CreatePlan(factory,
                    new RecordingStep("a", null, RuntimeOperationStatus.Succeeded, rollbackStatus: RuntimeOperationStatus.Failed),
                    new RecordingStep("b", null, RuntimeOperationStatus.Succeeded, rollbackStatus: RuntimeOperationStatus.Failed),
                    new RecordingStep("c", null, RuntimeOperationStatus.Succeeded, rollbackStatus: RuntimeOperationStatus.Failed)),
                new LiveApplicationStatePublisher());
            coordinator.Start("run-all-rollback-errors");

            PumpToTerminal(coordinator);

            Assert.AreEqual(ApplicationStartupState.RollbackFailed, coordinator.State);
            Assert.AreEqual(5, coordinator.RollbackErrorCount);
            Assert.AreEqual(ApplicationStartupErrorCode.RollbackFailed, coordinator.GetRollbackError(0).Code);
            Assert.AreEqual(ApplicationStartupErrorCode.RollbackFailed, coordinator.GetRollbackError(1).Code);
            Assert.AreEqual(ApplicationStartupErrorCode.RollbackFailed, coordinator.GetRollbackError(2).Code);
            Assert.AreEqual(ApplicationStartupErrorCode.CleanupIncomplete, coordinator.GetRollbackError(3).Code);
            Assert.AreEqual(ApplicationStartupErrorCode.RollbackException, coordinator.GetRollbackError(4).Code);
        }

        [Test]
        public void SteadyTick_PendingOperationAllocatesZeroBytes()
        {
            var pending = new RecordingStep("pending", null, RuntimeOperationStatus.Succeeded, ticksToTerminal: int.MaxValue);
            var coordinator = new ApplicationStartupCoordinator(CreatePlan(new RecordingTransactionFactory(), pending), new LiveApplicationStatePublisher());
            coordinator.Start("run-gc");
            Tick(coordinator);
            var context = new ApplicationStartupTickContext(0d, 0d);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                coordinator.Tick(in context);
                _ = coordinator.ForwardProgress;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private static ApplicationStartupPlan CreatePlan(RecordingTransactionFactory factory, params IApplicationStartupStep[] steps)
        {
            var descriptors = new ApplicationStartupStepDescriptor[steps.Length];
            var contracts = new IApplicationStartupStep[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                double timeout = steps[i] is RecordingStep recording
                    ? recording.Timeout
                    : steps[i] is ConfigurableStep configurable
                        ? configurable.Timeout
                        : ((DualOperationStep)steps[i]).Timeout;
                descriptors[i] = new ApplicationStartupStepDescriptor(steps[i].StepId, null, i, 1f, timeout);
                contracts[i] = steps[i];
            }

            Assert.IsTrue(ApplicationStartupPlan.TryCreate(new ApplicationStartupPlanId("boot"), descriptors, contracts, factory, out ApplicationStartupPlan plan, out ApplicationStartupError error), error.Message);
            return plan;
        }

        private static void PumpToTerminal(ApplicationStartupCoordinator coordinator)
        {
            for (int i = 0; i < 100 && !coordinator.IsTerminal; i++)
            {
                Tick(coordinator);
            }

            Assert.IsTrue(coordinator.IsTerminal, "Coordinator did not reach a terminal state.");
        }

        private static void Tick(ApplicationStartupCoordinator coordinator, double delta = 0.1d)
        {
            var context = new ApplicationStartupTickContext(delta, coordinator.ElapsedSeconds + delta);
            coordinator.Tick(in context);
        }

        private sealed class RecordingStep : IApplicationStartupStep
        {
            private readonly List<string> _log;
            private readonly RuntimeOperationStatus _forwardStatus;
            private readonly RuntimeOperationStatus _rollbackStatus;
            private readonly int _ticksToTerminal;
            private readonly bool _canCancel;

            public RecordingStep(string id, List<string> log, RuntimeOperationStatus forwardStatus, int ticksToTerminal = 1, bool canCancel = true, double timeout = 0d, RuntimeOperationStatus rollbackStatus = RuntimeOperationStatus.Succeeded)
            {
                StepId = Id(id);
                _log = log;
                _forwardStatus = forwardStatus;
                _rollbackStatus = rollbackStatus;
                _ticksToTerminal = ticksToTerminal;
                _canCancel = canCancel;
                Timeout = timeout;
            }

            public ApplicationStartupStepId StepId { get; }
            public double Timeout { get; }

            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context)
            {
                _log?.Add("begin:" + StepId.Value);
                return new FakeOperation(_forwardStatus, _ticksToTerminal, _canCancel);
            }

            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context)
            {
                _log?.Add("rollback:" + StepId.Value);
                return new FakeOperation(_rollbackStatus, 1);
            }
        }

        private sealed class ConfigurableStep : IApplicationStartupStep
        {
            private readonly IApplicationStartupStepOperation _operation;

            public ConfigurableStep(string id, IApplicationStartupStepOperation operation, double timeout = 0d)
            {
                StepId = Id(id);
                _operation = operation;
                Timeout = timeout;
            }

            public ApplicationStartupStepId StepId { get; }
            public double Timeout { get; }
            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context) => _operation;
            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context) => new FakeOperation(RuntimeOperationStatus.Succeeded, 1);
        }

        private sealed class DualOperationStep : IApplicationStartupStep
        {
            private readonly IApplicationStartupStepOperation _forward;
            private readonly IApplicationStartupStepOperation _rollback;

            public DualOperationStep(string id, IApplicationStartupStepOperation forward, IApplicationStartupStepOperation rollback)
            {
                StepId = Id(id);
                _forward = forward;
                _rollback = rollback;
            }

            public ApplicationStartupStepId StepId { get; }
            public double Timeout => 0d;
            public IApplicationStartupStepOperation Begin(in ApplicationStartupStepContext context) => _forward;
            public IApplicationStartupStepOperation BeginRollback(in ApplicationStartupRollbackContext context) => _rollback;
        }

        private sealed class FakeInnerDetail : IApplicationStartupInnerErrorDetail
        {
            public FakeInnerDetail(string domainId, string stableCode)
            {
                DomainId = domainId;
                StableCode = stableCode;
            }

            public string DomainId { get; }
            public string StableCode { get; }
        }

        private sealed class RecordingTransactionFactory : IApplicationStartupTransactionFactory
        {
            public readonly List<RecordingTransaction> Created = new List<RecordingTransaction>();
            public bool ThrowOnCommittedDispose { get; set; }
            public bool ThrowAfterPublish { get; set; }
            public bool AdvanceAfterPublish { get; set; }
            public bool FailCommit { get; set; }
            public bool FailFinalizeRollback { get; set; }
            public bool ThrowOnRollbackDispose { get; set; }
            public int CreateCount => Created.Count;
            public RecordingTransaction Last => Created[Created.Count - 1];
            public IApplicationStartupTransaction Create(ApplicationStartupPlanId planId)
            {
                var transaction = new RecordingTransaction(ThrowOnCommittedDispose, ThrowAfterPublish, AdvanceAfterPublish, FailCommit, FailFinalizeRollback, ThrowOnRollbackDispose);
                Created.Add(transaction);
                return transaction;
            }
        }

        private sealed class RecordingTransaction : IApplicationStartupTransaction
        {
            private readonly bool _throwOnCommittedDispose;
            private readonly bool _throwAfterPublish;
            private readonly bool _advanceAfterPublish;
            private readonly bool _failCommit;
            private readonly bool _failFinalizeRollback;
            private readonly bool _throwOnRollbackDispose;
            private bool _committed;
            private LiveApplicationState _candidate;

            public RecordingTransaction(bool throwOnCommittedDispose = false, bool throwAfterPublish = false, bool advanceAfterPublish = false, bool failCommit = false, bool failFinalizeRollback = false, bool throwOnRollbackDispose = false)
            {
                _throwOnCommittedDispose = throwOnCommittedDispose;
                _throwAfterPublish = throwAfterPublish;
                _advanceAfterPublish = advanceAfterPublish;
                _failCommit = failCommit;
                _failFinalizeRollback = failFinalizeRollback;
                _throwOnRollbackDispose = throwOnRollbackDispose;
            }
            public string TransactionId => "tx";
            public ApplicationStartupCommitObservation CommitObservation { get; private set; }
            public FakeLease PublishedLease { get; private set; }
            public int CommitCount { get; private set; }
            public int RollbackCount { get; private set; }
            public ApplicationStartupCommitResult TryCommit(LiveApplicationState expected, ILiveApplicationStatePublisher publisher)
            {
                CommitCount++;
                if (_failCommit)
                {
                    CommitObservation = ApplicationStartupCommitObservation.NotPublished;
                    return new ApplicationStartupCommitResult(ApplicationStartupCommitStatus.Failed, default);
                }

                if (_candidate == null)
                {
                    PublishedLease = new FakeLease("published");
                    _candidate = LiveApplicationState.CreateCandidate(false, -1, PublishedLease);
                }
                ApplicationStartupCommitResult result = publisher.TryPublish(expected, _candidate);
                _committed = result.Success;
                CommitObservation = result.Success
                    ? ApplicationStartupCommitObservation.Published(result.PublishedState)
                    : ApplicationStartupCommitObservation.NotPublished;
                if (_advanceAfterPublish && result.Success)
                {
                    Assert.IsTrue(publisher.TryPublish(result.PublishedState, LiveApplicationState.CreateCandidate(false, -1, new FakeLease("newer"))).Success);
                }
                if (_throwAfterPublish && result.Success)
                {
                    throw new InvalidOperationException("post-publication fault");
                }
                return result;
            }

            public ApplicationStartupRollbackResult FinalizeRollback()
            {
                RollbackCount++;
                return _failFinalizeRollback
                    ? new ApplicationStartupRollbackResult(false, new ApplicationStartupError(ApplicationStartupErrorCode.CleanupIncomplete, ApplicationStartupPhase.Cleanup, default, default, null, "finalize failed", null))
                    : ApplicationStartupRollbackResult.Succeeded;
            }

            public void Dispose()
            {
                if (_throwOnCommittedDispose && _committed)
                {
                    throw new InvalidOperationException("dispose failed");
                }

                if (_throwOnRollbackDispose && !_committed)
                {
                    throw new InvalidOperationException("rollback dispose failed");
                }
            }
        }

        private sealed class FaultDrainOperation : IApplicationStartupStepOperation, IApplicationStartupFaultDrainOperation
        {
            private readonly List<string> _log;
            private readonly string _id;
            private readonly int _drainsToTerminal;
            private readonly RuntimeOperationStatus _terminalStatus;
            private int _drains;

            public FaultDrainOperation(List<string> log, string id, int drainsToTerminal, RuntimeOperationStatus terminalStatus)
            {
                _log = log;
                _id = id;
                _drainsToTerminal = drainsToTerminal;
                _terminalStatus = terminalStatus;
                Status = RuntimeOperationStatus.Running;
            }

            public string OperationId => _id;
            public RuntimeOperationStatus Status { get; private set; }
            public float Progress => 0f;
            public RuntimeOperationError Error => Status == RuntimeOperationStatus.Failed ? new RuntimeOperationError("fault.failed", "Fault drain failed.") : default;
            public bool CanCancel => false;
            public IApplicationStartupInnerErrorDetail InnerErrorDetail => null;
            public void Tick(in ApplicationStartupTickContext context)
            {
                _log.Add("tick:" + _id);
                throw new InvalidOperationException("tick fault");
            }
            public void RequestCancel() { }
            public void RequestFaultDrain(Exception exception) => _log.Add("abort:" + _id);
            public void TickFaultDrain(in ApplicationStartupTickContext context)
            {
                _log.Add("drain:" + _id);
                _drains++;
                if (_drains >= _drainsToTerminal)
                {
                    Status = _terminalStatus;
                }
            }
        }

        private sealed class FakeLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump
        {
            public FakeLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) { }
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) =>
                ApplicationLiveRuntimeAttemptStatus.None;
        }

        private sealed class NonPumpLease : IApplicationLiveSessionLease
        {
            public NonPumpLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public void Dispose() { }
        }

        private sealed class RecordingPublicationHandoff : IApplicationLiveSessionPublicationHandoff
        {
            public bool AllowPrepare { get; set; } = true;
            public bool AllowClaim { get; set; } = true;
            public bool ThrowOnPrepare { get; set; }
            public Action PrepareAction { get; set; }
            public Action ClaimAction { get; set; }
            public int PrepareCount { get; private set; }
            public int ClaimAttemptCount { get; private set; }
            public int ClaimCount => Volatile.Read(ref _claimCount);

            public bool TryPreparePublicationHandoff(out ApplicationStartupError error)
            {
                PrepareCount++;
                PrepareAction?.Invoke();
                if (ThrowOnPrepare)
                {
                    throw new InvalidOperationException("Injected publication handoff preflight failure.");
                }

                if (!AllowPrepare)
                {
                    error = new ApplicationStartupError(
                        ApplicationStartupErrorCode.CommitFailed,
                        ApplicationStartupPhase.Commit,
                        default,
                        default,
                        null,
                        "Publication handoff is not ready.",
                        null);
                    return false;
                }

                error = default;
                return true;
            }

            public bool ClaimPublicationHandoff()
            {
                ClaimAction?.Invoke();
                ClaimAttemptCount++;
                if (!AllowClaim)
                    return false;
                Interlocked.Increment(ref _claimCount);
                return true;
            }

            private int _claimCount;
        }

        private sealed class BlockingLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump
        {
            public BlockingLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public ManualResetEventSlim DisposeEntered { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim AllowDispose { get; } = new ManualResetEventSlim(false);
            public int DisposeCount { get; private set; }
            public void Dispose()
            {
                DisposeEntered.Set();
                if (!AllowDispose.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Test did not release retirement disposal.");
                }

                DisposeCount++;
            }
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) { }
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) =>
                ApplicationLiveRuntimeAttemptStatus.None;
        }
    }
}
