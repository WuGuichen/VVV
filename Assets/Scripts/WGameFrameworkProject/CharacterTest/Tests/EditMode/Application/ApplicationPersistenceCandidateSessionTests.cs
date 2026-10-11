using System;
using System.Threading;
using System.Threading.Tasks;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PersistenceBridge;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.ApplicationStartup.SessionBridge;
using MxFramework.Persistence;
using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Platform;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationPersistenceCandidateSessionTests
    {
        [Test]
        public void RestorePrepared_PublishesExactFrozenGenerationAndPreservesFPlusOneWithoutTick()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new CoordinatorBuilder(prepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create("candidate-42", graph, builder);

            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> restored =
                service.RestorePrepared(prepared, session);

            Assert.That(restored.Success, Is.True);
            Assert.That(restored.Value.TargetId, Is.EqualTo("candidate-42"));
            Assert.That(restored.Value.CompletedFrame, Is.EqualTo(41));
            Assert.That(restored.Value.NextFrame, Is.EqualTo(42));
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Restored));
            Assert.That(session.CompletedFrame, Is.EqualTo(41));
            Assert.That(session.NextRuntimeFrame, Is.EqualTo(42));
            Assert.That(session.IsCandidateUnticked, Is.True);
            Assert.That(session.RestoreProfile, Is.SameAs(prepared.RestorePlan.Profile));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));

            Assert.That(session.TryCreatePublicationCandidate(
                out LiveApplicationState firstCandidate,
                out RuntimeSaveGameTargetValidationError createError), Is.True);
            Assert.That(createError.IsNone, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState sameCandidate, out _), Is.True);
            Assert.That(sameCandidate, Is.SameAs(firstCandidate));
            Assert.That(firstCandidate.NextRuntimeFrame, Is.EqualTo(42));
            Assert.That(firstCandidate.HasRuntimeSession, Is.True);
            var checkpointCapability = firstCandidate.CheckpointCapability as
                ApplicationPersistenceLiveCheckpointCapability;
            Assert.That(checkpointCapability, Is.Not.Null);
            Assert.That(checkpointCapability.CheckpointService, Is.SameAs(builder.CheckpointService));
            Assert.That(checkpointCapability.Profile, Is.EqualTo(prepared.Profile));
            Assert.That(checkpointCapability.Slot, Is.EqualTo(prepared.Slot));
            Assert.That(checkpointCapability.CreatedAtUtc, Is.EqualTo(prepared.CreatedAtUtc));
            Assert.That(checkpointCapability.TryGetExpectedHead(out SaveGameGeneration expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(prepared.OriginalGeneration));

            Assert.That(session.PublicationHandoff.TryPreparePublicationHandoff(
                out ApplicationStartupError firstPreflight), Is.True);
            Assert.That(firstPreflight.IsNone, Is.True);
            Assert.That(session.PublicationHandoff.TryPreparePublicationHandoff(
                out ApplicationStartupError repeatPreflight), Is.True);
            Assert.That(repeatPreflight.IsNone, Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));

            var publisher = new LiveApplicationStatePublisher();
            ApplicationStartupCommitResult published = publisher.TryPublish(publisher.Current, firstCandidate);

            Assert.That(published.Success, Is.True);
            Assert.That(publisher.Current, Is.SameAs(published.PublishedState));
            Assert.That(publisher.Current.NextRuntimeFrame, Is.EqualTo(42));
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
            Assert.That(session.IsCandidateUnticked, Is.True);
        }

        [Test]
        public void Publication_PreflightThenDispose_PreservesReservationUntilFinalClaim()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            Assert.That(session.PublicationHandoff.TryPreparePublicationHandoff(out _), Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));

            session.Dispose();

            Assert.That(session.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.PublicationCandidateCreated));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));

            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
        }

        [Test]
        public void Publication_BootAcceptsInitializedCurrentPersistenceCandidate()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session =
                CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(
                out LiveApplicationState candidate, out _), Is.True);

            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.Current.HasRuntimeSession, Is.False);
            ApplicationStartupCommitResult result = publisher.TryPublish(
                publisher.Current, candidate);

            Assert.That(result.Success, Is.True);
            Assert.That(publisher.Current, Is.SameAs(result.PublishedState));
            Assert.That(publisher.Current.Revision, Is.EqualTo(1));
            Assert.That(publisher.Current.SessionLease, Is.SameAs(candidate.SessionLease));
            Assert.That(publisher.Current.CheckpointCapability,
                Is.SameAs(candidate.CheckpointCapability));
            Assert.That(session.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
        }

        [Test]
        public void Publication_DisposeWinsBeforePreflight_PoisonsAndRejectsPreflight()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);

            session.Dispose();

            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
            Assert.That(session.PublicationHandoff.TryPreparePublicationHandoff(
                out ApplicationStartupError error), Is.False);
            Assert.That(error.IsNone, Is.False);
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.HandoffClaimed));
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
        }

        [Test]
        public void RestoreLease_RequiresExactSiblingProfileAndIncompleteDisposePoisonsCandidate()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, profile);

            Assert.That(session.TryAcquireRestoreLease(
                CreateProfile(), out IRuntimeSaveGameCandidateRestoreLease foreignLease,
                out RuntimeSaveGameTargetValidationError foreignError), Is.False);
            Assert.That(foreignLease, Is.Null);
            Assert.That(foreignError.Code,
                Is.EqualTo(RuntimeSaveGameTargetValidationErrorCode.InvalidComposition));
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Created));

            Assert.That(session.TryAcquireRestoreLease(
                profile, out IRuntimeSaveGameCandidateRestoreLease lease,
                out RuntimeSaveGameTargetValidationError acquireError), Is.True);
            Assert.That(acquireError.IsNone, Is.True);
            Assert.That(lease.RestoreProfile, Is.SameAs(profile));
            Assert.That(lease.Coordinator.RestoreProfile, Is.SameAs(profile));

            lease.Dispose();

            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
            Assert.That(session.TryCreatePublicationCandidate(out _, out _), Is.False);
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
        }

        [Test]
        public void RestoreLease_ConcurrentCompletionAndDisposeHasOneLinearizedOutcome()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, profile);
            Assert.That(session.TryAcquireRestoreLease(
                profile,
                out IRuntimeSaveGameCandidateRestoreLease lease,
                out _), Is.True);
            var ready = new CountdownEvent(2);
            var start = new ManualResetEventSlim(false);
            bool completionSucceeded = false;
            var restoreCompletion = new RuntimeSaveGameRestoreCompletion(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                new SaveGameGeneration(1),
                new SaveGameGeneration(1),
                SaveGameRecoverySource.Primary,
                new DateTime(638000000000000000L, DateTimeKind.Utc),
                41,
                42);
            Task completion = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                completionSucceeded = lease.TryCompleteRestore(in restoreCompletion, out _);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task disposal = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                lease.Dispose();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(ready.Wait(5000), Is.True);
            start.Set();
            Assert.That(Task.WaitAll(new[] { completion, disposal }, 5000), Is.True);

            if (completionSucceeded)
            {
                Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Restored));
                Assert.That(session.CompletedFrame, Is.EqualTo(41));
                Assert.That(session.NextRuntimeFrame, Is.EqualTo(42));
            }
            else
            {
                Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
                Assert.That(session.TryCreatePublicationCandidate(out _, out _), Is.False);
            }
            Assert.That(completionSucceeded &&
                        session.Status == ApplicationPersistenceCandidateStatus.Poisoned, Is.False);
        }

        [Test]
        public void RestoreLease_LastKnownGoodCompletionKeepsOriginalGenerationAsCheckpointHead()
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, profile);
            Assert.That(session.TryAcquireRestoreLease(
                profile, out IRuntimeSaveGameCandidateRestoreLease lease, out _), Is.True);
            var completion = new RuntimeSaveGameRestoreCompletion(
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"),
                new SaveGameGeneration(8),
                new SaveGameGeneration(7),
                SaveGameRecoverySource.LastKnownGood,
                new DateTime(638000000000000000L, DateTimeKind.Utc),
                41,
                42);

            Assert.That(lease.TryCompleteRestore(in completion, out _), Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var capability = candidate.CheckpointCapability as
                ApplicationPersistenceLiveCheckpointCapability;
            Assert.That(capability, Is.Not.Null);
            Assert.That(capability.TryGetExpectedHead(out SaveGameGeneration expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(new SaveGameGeneration(8)));
        }

        [Test]
        [TestCase(CoordinatorContradiction.WrongIdentity)]
        [TestCase(CoordinatorContradiction.WrongHost)]
        [TestCase(CoordinatorContradiction.NonAuthoritativeCoordinator)]
        [TestCase(CoordinatorContradiction.CheckpointServiceWrongHost)]
        public void CompositionFactory_RejectsEachSingleFieldContradictionBeforeOwnershipTransfer(
            CoordinatorContradiction contradiction)
        {
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new ContradictoryCoordinatorBuilder(
                CreateProfile(),
                contradiction,
                CoordinatorTransferBehavior.Succeed);

            Assert.Throws<InvalidOperationException>(() =>
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "contradictory", graph, builder));

            Assert.That(builder.Lease, Is.Not.Null);
            Assert.That(builder.Lease.TransferCount, Is.Zero);
            Assert.That(builder.Lease.AbandonCount, Is.EqualTo(1));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
        }

        [TestCase(CoordinatorTransferBehavior.ReturnFalse)]
        [TestCase(CoordinatorTransferBehavior.Throw)]
        public void CompositionFactory_TransferFailureAbandonsExactlyOnceAndLeavesGraphFrozen(
            CoordinatorTransferBehavior transferBehavior)
        {
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new ContradictoryCoordinatorBuilder(
                CreateProfile(),
                CoordinatorContradiction.None,
                transferBehavior);

            Assert.Throws<InvalidOperationException>(() =>
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "transfer-failure", graph, builder));

            Assert.That(builder.Lease, Is.Not.Null);
            Assert.That(builder.Lease.TransferCount, Is.EqualTo(1));
            Assert.That(builder.Lease.AbandonCount, Is.EqualTo(1));
            Assert.DoesNotThrow(builder.Lease.Abandon);
            Assert.That(builder.Lease.AbandonCount, Is.EqualTo(1));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
        }

        [Test]
        public void RestorePrepared_ParticipantFailurePoisonsCandidateAndNeverCreatesPublication()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "failing-candidate",
                    graph,
                    new CoordinatorBuilder(prepared.RestorePlan.Profile, failParticipant: true));
            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> restored =
                service.RestorePrepared(prepared, session);

            Assert.That(restored.Success, Is.False);
            Assert.That(restored.ErrorDetail.Phase,
                Is.EqualTo(RuntimeSaveGameBridgePhase.CandidateRestore));
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
            Assert.That(session.TryCreatePublicationCandidate(out _, out _), Is.False);
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.HandoffClaimed));
            Assert.That(graph.PumpOwnership, Is.Not.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
        }

        [Test]
        public void RestorePrepared_FailureExposesCallerOwnedCleanupUntilSceneAndResourcesRelease()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true,
                    1,
                    false,
                    out _,
                    out Action completeSceneUnload,
                    out _,
                    out Func<bool> isResourceGraphReleased);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "failing-cleanup-candidate",
                    graph,
                    new CoordinatorBuilder(prepared.RestorePlan.Profile, failParticipant: true));

            RuntimeSaveGameBridgeResult<RuntimeSaveGameRestoreResult> restored =
                service.RestorePrepared(prepared, session);

            Assert.That(restored.Success, Is.False);
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
            Assert.That(session.CandidateCleanupOperation, Is.Not.Null);
            Assert.That(session.CandidateCleanupOperation.Status,
                Is.EqualTo(RuntimeOperationStatus.Running));
            Assert.That(graph.SceneLease.Status,
                Is.EqualTo(ApplicationCandidateSceneLeaseStatus.Unloading));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
            Assert.That(isResourceGraphReleased(), Is.False);

            completeSceneUnload();
            for (int i = 0; i < 16 &&
                 session.CandidateCleanupOperation.Status == RuntimeOperationStatus.Running; i++)
                session.TickCandidateCleanup();

            Assert.That(session.CandidateCleanupOperation.Status,
                Is.EqualTo(RuntimeOperationStatus.Succeeded));
            Assert.That(graph.SceneLease.IsReleased, Is.True);
            Assert.That(graph.ResourceLease.IsReleased, Is.True);
            Assert.That(isResourceGraphReleased(), Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.Disposed));
        }

        [Test]
        public void Publisher_StaleExpectedDoesNotClaimCandidateAndSameCandidateCanPublishLater()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            LiveApplicationState stale = publisher.Current;
            Assert.That(publisher.TryPublish(
                stale,
                LiveApplicationState.CreateCandidate(false, -1, new RecordingLease("intervening"))).Success,
                Is.True);

            ApplicationStartupCommitResult rejected = publisher.TryPublish(stale, candidate);

            Assert.That(rejected.Success, Is.False);
            Assert.That(rejected.Status, Is.EqualTo(ApplicationStartupCommitStatus.PublicationConflict));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
            Assert.That(session.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.PublicationCandidateCreated));
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
        }

        [Test]
        public void PublishedSession_DisposeCannotStartCleanupAndPublisherRetirementStillSucceeds()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);

            session.Dispose();

            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
            Assert.That(session.CandidateCleanupOperation, Is.Null);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));

            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            Assert.That(publisher.TryPublish(
                publisher.Current,
                CreateDifferentSlotReplacementCandidate(
                    "published-session-replacement")).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);
            retirement.BeginRetirement();
            retirement.TickRetirement();

            Assert.That(retirement.RetirementStatus,
                Is.EqualTo(ApplicationLiveRetirementStatus.Succeeded));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.Disposed));
        }

        [Test]
        public void OrdinaryRollback_CannotTakeOwnershipFromPublishedLiveGraph()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            var ordinaryRollback = new CandidateResourceRollbackOperation(graph);

            ordinaryRollback.Tick(default);

            Assert.That(ordinaryRollback.Status, Is.EqualTo(RuntimeOperationStatus.Failed));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
        }

        [Test]
        public void OrdinaryRollback_CannotTakeOwnershipFromRetirementGraph()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true, 3, false, out _, out _, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            Assert.That(publisher.TryPublish(
                publisher.Current,
                CreateDifferentSlotReplacementCandidate(
                    "rollback-retirement-replacement")).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);
            retirement.BeginRetirement();
            retirement.TickRetirement();
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.RetirementOwner));
            var ordinaryRollback = new CandidateResourceRollbackOperation(graph);

            ordinaryRollback.Tick(default);

            Assert.That(ordinaryRollback.Status, Is.EqualTo(RuntimeOperationStatus.Failed));
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.RetirementOwner));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
        }

        [Test]
        public void LiveRetirement_ConcurrentBeginReturnsOneExactCachedOperation()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            Assert.That(publisher.TryPublish(
                publisher.Current,
                CreateDifferentSlotReplacementCandidate(
                    "concurrent-retirement-replacement")).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);
            var source = (IApplicationLiveRetirementSource)session.LiveSessionLease;
            var start = new ManualResetEventSlim(false);
            var ready = new CountdownEvent(2);
            var context = new ApplicationLiveRetirementContext(
                retirement.State.Revision,
                retirement.State.Session.SessionId);
            Task<IApplicationLiveRetirementOperation> first = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                return source.BeginRetirement(in context);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task<IApplicationLiveRetirementOperation> second = Task.Factory.StartNew(() =>
            {
                ready.Signal();
                start.Wait();
                return source.BeginRetirement(in context);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(ready.Wait(5000), Is.True);
            start.Set();
            Assert.That(Task.WaitAll(new Task[] { first, second }, 5000), Is.True);

            Assert.That(first.Result, Is.Not.Null);
            Assert.That(second.Result, Is.SameAs(first.Result));
            Assert.That(session.Status, Is.EqualTo(ApplicationPersistenceCandidateStatus.Retiring));

            retirement.BeginRetirement();
            retirement.TickRetirement();
            Assert.That(retirement.RetirementStatus,
                Is.EqualTo(ApplicationLiveRetirementStatus.Succeeded));
        }

        [Test]
        public void LiveRetirement_TransfersGraphAndSceneTogetherMakesSceneInertBeforeUnloadAndCleansInOrder()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                true,
                3,
                false,
                out IApplicationCandidateSceneIsolationGate isolationGate,
                out Action completeSceneUnload,
                out _,
                out Func<bool> isResourceGraphReleased);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
            Assert.That(graph.ScenePumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            Assert.That(publisher.TryPublish(
                publisher.Current,
                CreateDifferentSlotReplacementCandidate(
                    "ordered-retirement-replacement")).Success,
                Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);

            retirement.BeginRetirement();
            retirement.TickRetirement();

            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.RetirementOwner));
            Assert.That(graph.ScenePumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.RetirementOwner));
            Assert.That(isolationGate.IsInert, Is.True,
                "Scene must become inert before an earlier async retirement domain yields.");
            Assert.That(retirement.RetirementStatus, Is.EqualTo(ApplicationLiveRetirementStatus.Running));
            Assert.That(graph.SceneLease.Status,
                Is.EqualTo(ApplicationCandidateSceneLeaseStatus.HandoffFrozen));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
            Assert.That(isResourceGraphReleased(), Is.False);

            for (int i = 0; i < 16 &&
                 graph.SceneLease.Status != ApplicationCandidateSceneLeaseStatus.Unloading; i++)
                retirement.TickRetirement();

            Assert.That(graph.SceneLease.Status,
                Is.EqualTo(ApplicationCandidateSceneLeaseStatus.Unloading));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
            Assert.That(isResourceGraphReleased(), Is.False);

            completeSceneUnload();
            for (int i = 0; i < 16 && !retirement.IsTerminal; i++)
                retirement.TickRetirement();

            Assert.That(retirement.RetirementStatus, Is.EqualTo(ApplicationLiveRetirementStatus.Succeeded));
            Assert.That(retirement.WarningCount, Is.Zero);
            Assert.That(retirement.OwnershipDebt.IsNone, Is.True);
            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.Disposed));
        }

        [Test]
        public void LiveRetirement_MakeInertFailureIsTypedDebtOnOwnershipTransfer()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true,
                    3,
                    true,
                    out IApplicationCandidateSceneIsolationGate isolationGate,
                    out Action completeSceneUnload,
                    out _,
                    out _);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            Assert.That(publisher.TryPublish(
                publisher.Current,
                CreateDifferentSlotReplacementCandidate(
                    "inert-failure-replacement")).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);

            retirement.BeginRetirement();
            retirement.TickRetirement();

            Assert.That(graph.PumpOwnership, Is.EqualTo(ApplicationCandidatePumpOwnership.RetirementOwner));
            Assert.That(isolationGate.IsInert, Is.False);
            Assert.That(retirement.RetirementStatus, Is.EqualTo(ApplicationLiveRetirementStatus.Running));
            Assert.That(retirement.OwnershipDebt.DomainId, Is.EqualTo("scene"));
            Assert.That(retirement.OwnershipDebt.StableCode, Is.EqualTo("scene.make-inert-failed"));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);

            for (int i = 0; i < 16 &&
                 graph.SceneLease.Status != ApplicationCandidateSceneLeaseStatus.Unloading; i++)
                retirement.TickRetirement();
            completeSceneUnload();
            for (int i = 0; i < 16 && !retirement.IsTerminal; i++)
                retirement.TickRetirement();

            Assert.That(retirement.RetirementStatus, Is.EqualTo(ApplicationLiveRetirementStatus.Failed));
            Assert.That(retirement.OwnershipDebt.DomainId, Is.EqualTo("scene"));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
        }

        [Test]
        public void LiveRetirement_SceneUnloadFailureRetainsResourcesAndReportsOrderedDebtWithoutReplacingCurrent()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true,
                    1,
                    false,
                    out _,
                    out _,
                    out Action failSceneUnload,
                    out Func<bool> isResourceGraphReleased);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher(4);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement boot), Is.True);
            boot.Dispose();
            LiveApplicationState replacement = CreateDifferentSlotReplacementCandidate(
                "scene-failure-replacement");
            Assert.That(publisher.TryPublish(publisher.Current, replacement).Success, Is.True);
            LiveApplicationState currentReplacement = publisher.Current;
            Assert.That(publisher.TryDequeueRetired(out LiveApplicationStateRetirement retirement), Is.True);

            retirement.BeginRetirement();
            for (int i = 0; i < 16 &&
                 graph.SceneLease.Status != ApplicationCandidateSceneLeaseStatus.Unloading; i++)
                retirement.TickRetirement();
            Assert.That(graph.SceneLease.Status,
                Is.EqualTo(ApplicationCandidateSceneLeaseStatus.Unloading));

            failSceneUnload();
            for (int i = 0; i < 16 && !retirement.IsTerminal; i++)
                retirement.TickRetirement();

            Assert.That(retirement.RetirementStatus, Is.EqualTo(ApplicationLiveRetirementStatus.Failed));
            Assert.That(retirement.WarningCount, Is.EqualTo(2));
            Assert.That(retirement.GetWarning(0).DomainId, Is.EqualTo("scene"));
            Assert.That(retirement.GetWarning(1).DomainId, Is.EqualTo("resources"));
            Assert.That(retirement.OwnershipDebt.DomainId, Is.EqualTo("scene"));
            Assert.That(graph.ResourceLease.IsReleased, Is.False);
            Assert.That(isResourceGraphReleased(), Is.False);
            Assert.That(publisher.Current, Is.SameAs(currentReplacement));
        }

        [Test]
        public void RestoredLiveDriver_RuntimeModuleAppFlowAndStandaloneScenePumpExactlyOnce()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true,
                    1,
                    false,
                    out _,
                    out Action completeSceneUnload,
                    out _,
                    out _,
                    ApplicationControllerPumpStrategy.RuntimeModule,
                    ApplicationControllerPumpStrategy.Standalone);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);

            Assert.That(graph.SceneLease.Controller.RequestUnload("candidate").Status,
                Is.EqualTo(SceneFlowResultStatus.Accepted));
            completeSceneUnload();
            ApplicationLiveFrameTickResult first = driver.Tick(0.016d);
            ApplicationLiveFrameTickResult second = driver.Tick(0.016d);

            Assert.That(first.Frame, Is.EqualTo(42));
            Assert.That(second.Frame, Is.EqualTo(43));
            Assert.That(graph.RuntimeHost.TickCount, Is.EqualTo(2));
            Assert.That(graph.AppFlowLease.Controller.TickCount, Is.EqualTo(2));
            Assert.That(graph.SceneLease.Controller.IsBusy, Is.False);
            Assert.That(graph.SceneLease.Controller.ActiveSceneKey, Is.Empty);
        }

        [Test]
        public void RestoredLiveDriver_StandaloneAppFlowAndRuntimeModuleScenePumpExactlyOnce()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true,
                    1,
                    false,
                    out _,
                    out Action completeSceneUnload,
                    out _,
                    out _,
                    ApplicationControllerPumpStrategy.Standalone,
                    ApplicationControllerPumpStrategy.RuntimeModule);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);

            Assert.That(graph.SceneLease.Controller.RequestUnload("candidate").Status,
                Is.EqualTo(SceneFlowResultStatus.Accepted));
            completeSceneUnload();
            ApplicationLiveFrameTickResult first = driver.Tick(0.016d);
            ApplicationLiveFrameTickResult second = driver.Tick(0.016d);

            Assert.That(first.Frame, Is.EqualTo(42));
            Assert.That(second.Frame, Is.EqualTo(43));
            Assert.That(graph.RuntimeHost.TickCount, Is.EqualTo(2));
            Assert.That(graph.AppFlowLease.Controller.TickCount, Is.EqualTo(2));
            Assert.That(graph.SceneLease.Controller.IsBusy, Is.False);
            Assert.That(graph.SceneLease.Controller.ActiveSceneKey, Is.Empty);
        }

        [Test]
        public void RestoredLiveDriver_RealFailFastHostAttemptConsumesFrameWithoutRetry()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    false,
                    1,
                    false,
                    out _,
                    out _,
                    out _,
                    out _,
                    ApplicationControllerPumpStrategy.RuntimeModule,
                    ApplicationControllerPumpStrategy.Standalone,
                    true);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);

            Assert.Catch<Exception>(() => driver.Tick(0.016d));
            Assert.That(graph.RuntimeHost.LastTickAttemptFrame, Is.EqualTo(42));
            Assert.That(graph.RuntimeHost.LastTickAttemptSucceeded, Is.False);
            Assert.Catch<Exception>(() => driver.Tick(0.016d));
            Assert.That(graph.RuntimeHost.LastTickAttemptFrame, Is.EqualTo(43));
        }

        [Test]
        public void RestoredLiveDriver_CollectFailureConsumesFrameAndReturnsFailedAttempt()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    false, 1, false, out _, out _, out _, out _,
                    ApplicationControllerPumpStrategy.RuntimeModule,
                    ApplicationControllerPumpStrategy.Standalone,
                    true,
                    RuntimeHostErrorPolicy.CollectAndContinue);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);

            ApplicationLiveFrameTickResult first = driver.Tick(0.016d);
            ApplicationLiveFrameTickResult second = driver.Tick(0.016d);

            Assert.That(first.Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptFailed));
            Assert.That(first.Frame, Is.EqualTo(42));
            Assert.That(second.Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptFailed));
            Assert.That(second.Frame, Is.EqualTo(43));
        }

        [Test]
        public void RestoredLiveDriver_AllStandaloneUsesAppFlowThenSceneAndWarmedPathAllocatesZero()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService service);
            ApplicationCandidateSessionGraph graph =
                ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                    true, 1, false, out _, out _, out _, out _,
                    ApplicationControllerPumpStrategy.Standalone,
                    ApplicationControllerPumpStrategy.Standalone);
            ApplicationPersistenceCandidateSession session = CreateSession(graph, prepared.RestorePlan.Profile);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var publisher = new LiveApplicationStatePublisher();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            driver.Tick(0.016d);

            Assert.That(graph.LiveStandaloneAppFlowTickCount, Is.EqualTo(1));
            Assert.That(graph.LiveStandaloneSceneTickCount, Is.EqualTo(1));
            Assert.That(graph.LastLiveStandaloneAppFlowSequence,
                Is.LessThan(graph.LastLiveStandaloneSceneSequence));

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) driver.Tick(0.016d);
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.Zero);
            Assert.That(graph.LiveStandaloneAppFlowTickCount, Is.EqualTo(1001));
            Assert.That(graph.LiveStandaloneSceneTickCount, Is.EqualTo(1001));
        }

        [Test]
        public void LifecycleCheckpointPolicy_UsesExactCandidateServiceAndAdvancesHeadOnce()
        {
            var store = new InMemorySaveGameStore();
            PreparedRuntimeSaveGame prepared = CreatePrepared(store, out RuntimeSaveGameService loadService);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new CoordinatorBuilder(prepared.RestorePlan.Profile, false, store);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create("checkpoint-candidate", graph, builder);
            Assert.That(loadService.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);

            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(8, fence);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            var lifecycle = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            Assert.That(ApplicationPersistencePlatformLifecycleBridgeFactory.TryCreate(
                lifecycle,
                publisher,
                fence,
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                4,
                out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            Assert.That(lifecycle.ReportSuspend(true), Is.True);

            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded));
            var capability = publisher.Current.CheckpointCapability as
                ApplicationPersistenceLiveCheckpointCapability;
            Assert.That(capability.TryGetExpectedHead(out SaveGameGeneration expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(new SaveGameGeneration(2)));
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.Idle));
            Assert.That(capability.TryGetExpectedHead(out expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(new SaveGameGeneration(2)));
        }

        [Test]
        public void Publication_SameSlotFreshAuthorityIsRejectedButDifferentSlotIsAccepted()
        {
            var sameSlotStore = new InMemorySaveGameStore();
            PreparedRuntimeSaveGame firstPrepared = CreatePrepared(
                sameSlotStore, out RuntimeSaveGameService sameSlotLoadService);
            PreparedRuntimeSaveGame freshAuthorityPrepared = PrepareCurrent(
                sameSlotLoadService, new SaveGameProfileId("profile"), new SaveGameSlotId("slot"));

            ApplicationCandidateSessionGraph firstGraph = CreateGraph(false, out _, out _);
            var firstBuilder = new CoordinatorBuilder(firstPrepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession firstSession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "same-slot-current", firstGraph, firstBuilder);
            Assert.That(sameSlotLoadService.RestorePrepared(firstPrepared, firstSession).Success, Is.True);
            Assert.That(firstSession.TryCreatePublicationCandidate(
                out LiveApplicationState firstCandidate, out _), Is.True);

            ApplicationCandidateSessionGraph freshAuthorityGraph = CreateGraph(false, out _, out _);
            var freshAuthorityBuilder = new CoordinatorBuilder(
                freshAuthorityPrepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession freshAuthoritySession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "same-slot-fresh-authority", freshAuthorityGraph, freshAuthorityBuilder);
            Assert.That(sameSlotLoadService.RestorePrepared(
                freshAuthorityPrepared, freshAuthoritySession).Success, Is.True);
            Assert.That(freshAuthoritySession.TryCreatePublicationCandidate(
                out LiveApplicationState freshAuthorityCandidate, out _), Is.True);

            var publisher = new LiveApplicationStatePublisher(8);
            Assert.That(publisher.TryPublish(publisher.Current, firstCandidate).Success, Is.True);
            LiveApplicationState exactCurrent = publisher.Current;
            ApplicationStartupCommitResult sameSlotRejected = publisher.TryPublish(
                exactCurrent, freshAuthorityCandidate);
            Assert.That(sameSlotRejected.Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationConflict));
            Assert.That(sameSlotRejected.Error.Code,
                Is.EqualTo(ApplicationStartupErrorCode.PublicationConflict));
            Assert.That(publisher.Current, Is.SameAs(exactCurrent));
            Assert.That(freshAuthorityGraph.PumpOwnership,
                Is.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));

            var differentSlotStore = new InMemorySaveGameStore();
            PreparedRuntimeSaveGame differentSlotPrepared = CreatePrepared(
                differentSlotStore,
                out RuntimeSaveGameService differentSlotLoadService,
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("other-slot"));
            ApplicationCandidateSessionGraph differentSlotGraph = CreateGraph(false, out _, out _);
            var differentSlotBuilder = new CoordinatorBuilder(
                differentSlotPrepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession differentSlotSession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "different-slot", differentSlotGraph, differentSlotBuilder);
            Assert.That(differentSlotLoadService.RestorePrepared(
                differentSlotPrepared, differentSlotSession).Success, Is.True);
            Assert.That(differentSlotSession.TryCreatePublicationCandidate(
                out LiveApplicationState differentSlotCandidate, out _), Is.True);

            ApplicationStartupCommitResult differentSlotPublished = publisher.TryPublish(
                publisher.Current, differentSlotCandidate);
            Assert.That(differentSlotPublished.Success, Is.True);
            Assert.That(publisher.Current, Is.SameAs(differentSlotPublished.PublishedState));
            Assert.That(publisher.Current.Revision, Is.EqualTo(2));
            Assert.That(publisher.Current.SessionLease,
                Is.SameAs(differentSlotCandidate.SessionLease));
            Assert.That(publisher.Current.CheckpointCapability,
                Is.SameAs(differentSlotCandidate.CheckpointCapability));
            Assert.That(differentSlotSession.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
        }

        [Test]
        public void LifecycleSave_StalePreparedCandidateNeverPublishesAndRebuiltHeadSucceeds()
        {
            var store = new InMemorySaveGameStore();
            PreparedRuntimeSaveGame currentPrepared = CreatePrepared(
                store, out RuntimeSaveGameService loadService);
            PreparedRuntimeSaveGame stalePrepared = PrepareCurrent(
                loadService, new SaveGameProfileId("profile"), new SaveGameSlotId("slot"));
            var authority = new ApplicationPersistenceDurableHeadAuthority();

            ApplicationCandidateSessionGraph currentGraph = CreateGraph(false, out _, out _);
            var currentBuilder = new CoordinatorBuilder(
                currentPrepared.RestorePlan.Profile, false, store, null, authority);
            ApplicationPersistenceCandidateSession currentSession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "durable-current", currentGraph, currentBuilder);
            Assert.That(loadService.RestorePrepared(currentPrepared, currentSession).Success, Is.True);
            Assert.That(currentSession.TryCreatePublicationCandidate(
                out LiveApplicationState currentCandidate, out _), Is.True);

            ApplicationCandidateSessionGraph staleGraph = CreateGraph(false, out _, out _);
            var staleBuilder = new CoordinatorBuilder(
                stalePrepared.RestorePlan.Profile, false, store, null, authority);
            ApplicationPersistenceCandidateSession staleSession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "stale-candidate", staleGraph, staleBuilder);
            Assert.That(loadService.RestorePrepared(stalePrepared, staleSession).Success, Is.True);
            Assert.That(staleSession.TryCreatePublicationCandidate(
                out LiveApplicationState staleCandidate, out _), Is.True);

            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(8, fence);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, currentCandidate).Success, Is.True);
            LiveApplicationState exactCurrent = publisher.Current;
            Assert.That(driver.Tick(0.016d).HasRuntimeAttempt, Is.True);

            var lifecycle = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            Assert.That(ApplicationPersistencePlatformLifecycleBridgeFactory.TryCreate(
                lifecycle,
                publisher,
                fence,
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                4,
                out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            Assert.That(lifecycle.ReportSuspend(true), Is.True);
            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));

            ApplicationStartupCommitResult deferred = publisher.TryPublish(
                exactCurrent, staleCandidate);
            Assert.That(deferred.Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationDeferred));
            Assert.That(publisher.Current, Is.SameAs(exactCurrent));
            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded));

            ApplicationStartupCommitResult staleRejected = publisher.TryPublish(
                exactCurrent, staleCandidate);
            Assert.That(staleRejected.Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationConflict));
            Assert.That(staleRejected.Error.Code,
                Is.EqualTo(ApplicationStartupErrorCode.PublicationConflict));
            Assert.That(publisher.TryPublish(exactCurrent, staleCandidate).Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationConflict));
            Assert.That(publisher.Current, Is.SameAs(exactCurrent));
            Assert.That(staleSession.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.PublicationCandidateCreated));

            staleSession.Dispose();
            staleSession.TickCandidateCleanup();
            Assert.That(staleSession.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.Poisoned));
            Assert.That(staleGraph.PumpOwnership,
                Is.Not.EqualTo(ApplicationCandidatePumpOwnership.HandoffFrozen));
            Assert.That(staleGraph.PumpOwnership,
                Is.Not.EqualTo(ApplicationCandidatePumpOwnership.LiveOwner));

            PreparedRuntimeSaveGame rebuiltPrepared = PrepareCurrent(
                loadService, new SaveGameProfileId("profile"), new SaveGameSlotId("slot"));
            Assert.That(rebuiltPrepared.OriginalGeneration,
                Is.EqualTo(new SaveGameGeneration(2)));
            ApplicationCandidateSessionGraph rebuiltGraph = CreateGraph(false, out _, out _);
            var rebuiltBuilder = new CoordinatorBuilder(
                rebuiltPrepared.RestorePlan.Profile, false, store, null, authority);
            ApplicationPersistenceCandidateSession rebuiltSession =
                ApplicationPersistenceCandidateSessionFactory.Create(
                    "rebuilt-candidate", rebuiltGraph, rebuiltBuilder);
            Assert.That(loadService.RestorePrepared(rebuiltPrepared, rebuiltSession).Success, Is.True);
            Assert.That(rebuiltSession.TryCreatePublicationCandidate(
                out LiveApplicationState rebuiltCandidate, out _), Is.True);
            ApplicationStartupCommitResult rebuiltPublished = publisher.TryPublish(
                exactCurrent, rebuiltCandidate);
            Assert.That(rebuiltPublished.Success, Is.True);
            Assert.That(publisher.Current, Is.SameAs(rebuiltPublished.PublishedState));
            Assert.That(publisher.Current.SessionLease,
                Is.SameAs(rebuiltCandidate.SessionLease));
            Assert.That(publisher.Current.CheckpointCapability,
                Is.SameAs(rebuiltCandidate.CheckpointCapability));
            Assert.That(rebuiltSession.Status,
                Is.EqualTo(ApplicationPersistenceCandidateStatus.Published));
        }

        [Test]
        public void LifecycleCheckpointPolicy_MapsExactStoreGenerationConflict()
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(out RuntimeSaveGameService loadService);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new CoordinatorBuilder(prepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create("conflict-candidate", graph, builder);
            Assert.That(loadService.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(8, fence);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            Assert.That(driver.Tick(0.016d).HasRuntimeAttempt, Is.True);
            var lifecycle = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            Assert.That(ApplicationPersistencePlatformLifecycleBridgeFactory.TryCreate(
                lifecycle,
                publisher,
                fence,
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                4,
                out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            lifecycle.ReportSuspend(true);

            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointFailed));
            Assert.That(bridge.LastCheckpointStatus,
                Is.EqualTo(ApplicationLifecycleCheckpointOperationStatus.GenerationConflict));
            var capability = publisher.Current.CheckpointCapability as
                ApplicationPersistenceLiveCheckpointCapability;
            Assert.That(capability.TryGetExpectedHead(out SaveGameGeneration expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(new SaveGameGeneration(1)));
        }

        [Test]
        public void LifecycleCheckpointPolicy_InvalidClocksRejectBeforeCheckpointCapture()
        {
            IWallClock[] clocks =
            {
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Local)),
                new ThrowingWallClock()
            };
            for (int i = 0; i < clocks.Length; i++)
            {
                var checker = new CountingQuiescenceChecker();
                CheckpointFixture fixture = CreateCheckpointFixture(
                    clocks[i],
                    new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                    checker,
                    tickBeforeSuspend: true);

                Assert.That(fixture.Bridge.Pump(true),
                    Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointFailed));
                Assert.That(checker.CheckCount, Is.Zero);
                Assert.That(fixture.Bridge.HasPendingEvidence, Is.True);
            }
        }

        [Test]
        public void LifecycleCheckpointPolicy_WrongRequestIdentityRejectsBeforeCheckpointCapture()
        {
            var checker = new CountingQuiescenceChecker();
            CheckpointFixture fixture = CreateCheckpointFixture(
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                new WrongProfileRequestPolicy(),
                checker,
                tickBeforeSuspend: true);

            Assert.That(fixture.Bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointFailed));
            Assert.That(checker.CheckCount, Is.Zero);
            Assert.That(fixture.Bridge.HasPendingEvidence, Is.True);
        }

        [Test]
        public void LifecycleCheckpointPolicy_BeforeFirstLiveTickDefersThenExplicitRetrySucceedsOnce()
        {
            var checker = new CountingQuiescenceChecker();
            CheckpointFixture fixture = CreateCheckpointFixture(
                new FixedWallClock(new DateTime(638000000020000000L, DateTimeKind.Utc)),
                new ApplicationPersistenceEmptyMetadataCheckpointRequestPolicy(),
                checker,
                tickBeforeSuspend: false);

            Assert.That(fixture.Bridge.Pump(true),
                Is.EqualTo(
                    ApplicationPlatformLifecyclePumpStatus.DeferredUntilSuccessfulRuntimeTick));
            Assert.That(checker.CheckCount, Is.Zero);
            Assert.That(fixture.Bridge.HasPendingEvidence, Is.True);
            Assert.That(fixture.Driver.Tick(0.016d).Status,
                Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            fixture.Bridge.RequestRetry();
            Assert.That(fixture.Bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(checker.CheckCount, Is.EqualTo(1));
            Assert.That(fixture.Bridge.Pump(true),
                Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded));
            Assert.That(fixture.Capability.TryGetExpectedHead(out SaveGameGeneration expectedHead), Is.True);
            Assert.That(expectedHead, Is.EqualTo(new SaveGameGeneration(2)));
        }

        private static CheckpointFixture CreateCheckpointFixture(
            IWallClock wallClock,
            IApplicationPersistenceLifecycleCheckpointRequestPolicy requestPolicy,
            IRuntimeCheckpointQuiescenceChecker checker,
            bool tickBeforeSuspend)
        {
            var store = new InMemorySaveGameStore();
            PreparedRuntimeSaveGame prepared = CreatePrepared(store, out RuntimeSaveGameService loadService);
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new CoordinatorBuilder(prepared.RestorePlan.Profile, false, store, checker);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create("policy-fixture", graph, builder);
            Assert.That(loadService.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(out LiveApplicationState candidate, out _), Is.True);
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(8, fence);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
            if (tickBeforeSuspend)
                Assert.That(driver.Tick(0.016d).HasRuntimeAttempt, Is.True);
            var lifecycle = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            Assert.That(ApplicationPersistencePlatformLifecycleBridgeFactory.TryCreate(
                lifecycle, publisher, fence, wallClock, requestPolicy, 4,
                out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            Assert.That(lifecycle.ReportSuspend(true), Is.True);
            return new CheckpointFixture(
                bridge,
                driver,
                publisher.Current.CheckpointCapability as ApplicationPersistenceLiveCheckpointCapability);
        }

        private static ApplicationCandidateSessionGraph CreateGraph(
            bool withScene,
            out IApplicationCandidateSceneIsolationGate isolationGate,
            out Action completeSceneUnload) =>
            ApplicationCandidateSessionBridgeTests.CreateFrozenGraphForPersistenceBridgeTests(
                withScene, out isolationGate, out completeSceneUnload);

        private static ApplicationPersistenceCandidateSession CreateSession(
            ApplicationCandidateSessionGraph graph,
            RuntimeSaveStateRestoreProfile profile) =>
            ApplicationPersistenceCandidateSessionFactory.Create(
                "candidate-42",
                graph,
                new CoordinatorBuilder(profile));

        private static LiveApplicationState CreateDifferentSlotReplacementCandidate(
            string targetId)
        {
            PreparedRuntimeSaveGame prepared = CreatePrepared(
                new InMemorySaveGameStore(),
                out RuntimeSaveGameService service,
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("retirement-slot"));
            ApplicationCandidateSessionGraph graph = CreateGraph(false, out _, out _);
            var builder = new CoordinatorBuilder(prepared.RestorePlan.Profile);
            ApplicationPersistenceCandidateSession session =
                ApplicationPersistenceCandidateSessionFactory.Create(targetId, graph, builder);
            Assert.That(service.RestorePrepared(prepared, session).Success, Is.True);
            Assert.That(session.TryCreatePublicationCandidate(
                out LiveApplicationState candidate, out _), Is.True);
            return candidate;
        }

        private static PreparedRuntimeSaveGame CreatePrepared(out RuntimeSaveGameService service) =>
            CreatePrepared(new InMemorySaveGameStore(), out service);

        private static PreparedRuntimeSaveGame CreatePrepared(
            InMemorySaveGameStore store,
            out RuntimeSaveGameService service)
            => CreatePrepared(
                store,
                out service,
                new SaveGameProfileId("profile"),
                new SaveGameSlotId("slot"));

        private static PreparedRuntimeSaveGame CreatePrepared(
            InMemorySaveGameStore store,
            out RuntimeSaveGameService service,
            SaveGameProfileId profileId,
            SaveGameSlotId slotId)
        {
            RuntimeSaveStateRestoreProfile profile = CreateProfile();
            service = new RuntimeSaveGameService(
                store,
                CreateBarrier(profile),
                CreateBuild(),
                profile,
                new ExactCompatibility());
            RuntimeSaveGameBridgeResult<RuntimeSaveGameWritePlan> planned =
                service.PlanCheckpointSave(new RuntimeSaveGameCheckpointRequest(
                    profileId,
                    slotId,
                    SaveGameGeneration.None,
                    new DateTime(638000000000000000L, DateTimeKind.Utc),
                    new DateTime(638000000010000000L, DateTimeKind.Utc),
                    null,
                    null));
            Assert.That(planned.Success, Is.True);
            IRuntimeSaveGameOperation<SaveGameCommitResult> committed = service.Commit(planned.Value);
            Assert.That(committed.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            return PrepareCurrent(service, profileId, slotId);
        }

        private static PreparedRuntimeSaveGame PrepareCurrent(
            RuntimeSaveGameService service,
            SaveGameProfileId profileId,
            SaveGameSlotId slotId)
        {
            IRuntimeSaveGameOperation<PreparedRuntimeSaveGame> loaded = service.PrepareLoad(
                new RuntimeSaveGameLoadRequest(profileId, slotId, SaveGameReadSelection.Current));
            Assert.That(loaded.Status, Is.EqualTo(SaveGameOperationStatus.Succeeded));
            return loaded.Result;
        }

        private static RuntimeCheckpointBarrier CreateBarrier(RuntimeSaveStateRestoreProfile profile)
        {
            var registry = new RuntimeSaveStateRegistry();
            registry.Register(new RuntimeSaveStateRootHeaderProvider(profile.Descriptor).CreateParticipant());
            RuntimeSaveStateCoordinatorFactoryResult factory =
                RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, profile);
            Assert.That(factory.Success, Is.True);
            var host = new RuntimeHost();
            host.Initialize();
            host.Start();
            host.Tick(41, 0.016d);
            return new RuntimeCheckpointBarrier(
                host,
                factory.Coordinator,
                new RuntimeCheckpointQuiescenceRegistry());
        }

        private static RuntimeSaveGameService CreateCheckpointService(
            RuntimeHost runtimeHost,
            RuntimeSaveStateCoordinator coordinator,
            RuntimeSaveStateRestoreProfile profile,
            InMemorySaveGameStore store = null,
            IRuntimeCheckpointQuiescenceChecker checkpointChecker = null)
        {
            var quiescence = new RuntimeCheckpointQuiescenceRegistry();
            if (checkpointChecker != null)
                quiescence.Register(new RuntimeCheckpointQuiescenceParticipant(
                    "test.checkpoint", 10, checkpointChecker));
            return new RuntimeSaveGameService(
                store ?? new InMemorySaveGameStore(),
                new RuntimeCheckpointBarrier(
                    runtimeHost,
                    coordinator,
                    quiescence),
                CreateBuild(),
                profile,
                new ExactCompatibility());
        }

        private static RuntimeSaveStateRestoreProfile CreateProfile() =>
            RuntimeSaveStateRestoreProfile.Create(
                new RuntimeSaveStateRootHeaderDescriptor(
                    0, "framework", "config", "resources"),
                new RuntimeSaveStateMigrationPipeline());

        private static SaveGameBuildDescriptor CreateBuild() =>
            new SaveGameBuildDescriptor(
                "framework", "build", "config", "resources", string.Empty);

        private sealed class CoordinatorBuilder : IApplicationPersistenceCandidateCoordinatorBuilder
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private readonly bool _failParticipant;
            private readonly InMemorySaveGameStore _checkpointStore;
            private readonly IRuntimeCheckpointQuiescenceChecker _checkpointChecker;
            private readonly ApplicationPersistenceDurableHeadAuthority _durableHeadAuthority;

            public CoordinatorBuilder(
                RuntimeSaveStateRestoreProfile profile,
                bool failParticipant = false,
                InMemorySaveGameStore checkpointStore = null,
                IRuntimeCheckpointQuiescenceChecker checkpointChecker = null,
                ApplicationPersistenceDurableHeadAuthority durableHeadAuthority = null)
            {
                _profile = profile;
                _failParticipant = failParticipant;
                _checkpointStore = checkpointStore;
                _checkpointChecker = checkpointChecker;
                _durableHeadAuthority = durableHeadAuthority ??
                    new ApplicationPersistenceDurableHeadAuthority();
            }

            public IApplicationPersistenceCandidateCoordinatorBuildLease Build(
                ApplicationCandidateRuntimeCompositionIdentity compositionIdentity,
                RuntimeHost runtimeHost)
            {
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                if (_failParticipant)
                    registry.Register(new RuntimeSaveStateParticipant(
                        "failing", 10, null, new FailingRestorer()));
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                Assert.That(factory.Success, Is.True);
                CheckpointService = CreateCheckpointService(
                    runtimeHost, factory.Coordinator, _profile, _checkpointStore, _checkpointChecker);
                return new CoordinatorBuildLease(
                    compositionIdentity,
                    runtimeHost,
                    factory.Coordinator,
                    CheckpointService,
                    _durableHeadAuthority);
            }

            public RuntimeSaveGameService CheckpointService { get; private set; }
        }

        public enum CoordinatorContradiction
        {
            None = 0,
            WrongIdentity = 1,
            WrongHost = 2,
            NonAuthoritativeCoordinator = 3,
            CheckpointServiceWrongHost = 4
        }

        public enum CoordinatorTransferBehavior
        {
            Succeed = 0,
            ReturnFalse = 1,
            Throw = 2
        }

        private sealed class ContradictoryCoordinatorBuilder :
            IApplicationPersistenceCandidateCoordinatorBuilder
        {
            private readonly RuntimeSaveStateRestoreProfile _profile;
            private readonly CoordinatorContradiction _contradiction;
            private readonly CoordinatorTransferBehavior _transferBehavior;
            private readonly ApplicationPersistenceDurableHeadAuthority _durableHeadAuthority =
                new ApplicationPersistenceDurableHeadAuthority();

            public ContradictoryCoordinatorBuilder(
                RuntimeSaveStateRestoreProfile profile,
                CoordinatorContradiction contradiction,
                CoordinatorTransferBehavior transferBehavior)
            {
                _profile = profile;
                _contradiction = contradiction;
                _transferBehavior = transferBehavior;
            }

            public CoordinatorBuildLease Lease { get; private set; }

            public IApplicationPersistenceCandidateCoordinatorBuildLease Build(
                ApplicationCandidateRuntimeCompositionIdentity compositionIdentity,
                RuntimeHost runtimeHost)
            {
                var registry = new RuntimeSaveStateRegistry();
                registry.Register(new RuntimeSaveStateRootHeaderProvider(_profile.Descriptor).CreateParticipant());
                RuntimeSaveStateCoordinatorFactoryResult factory =
                    RuntimeSaveStateCoordinator.CreateAuthoritativeWithProfile(registry, _profile);
                RuntimeSaveGameService checkpointService = CreateCheckpointService(
                    _contradiction == CoordinatorContradiction.CheckpointServiceWrongHost
                        ? new RuntimeHost()
                        : runtimeHost,
                    factory.Coordinator,
                    _profile);
                Lease = new CoordinatorBuildLease(
                    _contradiction == CoordinatorContradiction.WrongIdentity
                        ? new ApplicationCandidateRuntimeCompositionIdentity()
                        : compositionIdentity,
                    _contradiction == CoordinatorContradiction.WrongHost
                        ? new RuntimeHost()
                        : runtimeHost,
                    _contradiction == CoordinatorContradiction.NonAuthoritativeCoordinator
                        ? new RuntimeSaveStateCoordinator(registry)
                        : factory.Coordinator,
                    checkpointService,
                    _durableHeadAuthority,
                    _transferBehavior);
                return Lease;
            }
        }

        private sealed class CoordinatorBuildLease :
            IApplicationPersistenceCandidateCoordinatorBuildLease
        {
            private int _state;
            private readonly CoordinatorTransferBehavior _transferBehavior;

            public CoordinatorBuildLease(
                ApplicationCandidateRuntimeCompositionIdentity compositionIdentity,
                RuntimeHost runtimeHost,
                RuntimeSaveStateCoordinator coordinator,
                RuntimeSaveGameService checkpointService,
                ApplicationPersistenceDurableHeadAuthority durableHeadAuthority,
                CoordinatorTransferBehavior transferBehavior = CoordinatorTransferBehavior.Succeed)
            {
                CompositionIdentity = compositionIdentity;
                RuntimeHost = runtimeHost;
                Coordinator = coordinator;
                CheckpointService = checkpointService;
                DurableHeadAuthority = durableHeadAuthority;
                _transferBehavior = transferBehavior;
            }

            public ApplicationCandidateRuntimeCompositionIdentity CompositionIdentity { get; }
            public RuntimeHost RuntimeHost { get; }
            public RuntimeSaveStateCoordinator Coordinator { get; }
            public RuntimeSaveGameService CheckpointService { get; }
            public ApplicationPersistenceDurableHeadAuthority DurableHeadAuthority { get; }
            public bool Transferred => Volatile.Read(ref _state) == 1;
            public bool Abandoned => Volatile.Read(ref _state) == 2;
            public int TransferCount { get; private set; }
            public int AbandonCount { get; private set; }
            public bool TryTransfer()
            {
                TransferCount++;
                if (_transferBehavior == CoordinatorTransferBehavior.Throw)
                    throw new InvalidOperationException("Injected coordinator ownership transfer failure.");
                if (_transferBehavior == CoordinatorTransferBehavior.ReturnFalse)
                    return false;
                return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            }
            public void Abandon()
            {
                if (Interlocked.CompareExchange(ref _state, 2, 0) == 0)
                    AbandonCount++;
            }
        }

        private sealed class FailingRestorer : IRuntimeSaveStateRestorer
        {
            public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState) =>
                RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.failing",
                    "injected"));
        }

        private sealed class ExactCompatibility : IRuntimeSaveGameCompatibilityChecker
        {
            public RuntimeSaveGameCompatibilityResult Check(
                SaveGameBuildDescriptor build,
                RuntimeSaveStateRestoreProfile restoreProfile)
            {
                RuntimeSaveStateRootHeaderDescriptor descriptor = restoreProfile.Descriptor;
                return string.Equals(build.FrameworkVersion, descriptor.FrameworkVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ConfigVersion, descriptor.ConfigVersion, StringComparison.Ordinal) &&
                       string.Equals(build.ResourceCatalogVersion,
                           descriptor.ResourceCatalogVersion,
                           StringComparison.Ordinal)
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

        private sealed class ThrowingWallClock : IWallClock
        {
            public DateTime UtcNow => throw new InvalidOperationException("injected clock failure");
        }

        private sealed class WrongProfileRequestPolicy :
            IApplicationPersistenceLifecycleCheckpointRequestPolicy
        {
            public bool TryCreate(
                in ApplicationLifecycleCheckpointEvidence evidence,
                SaveGameProfileId profile,
                SaveGameSlotId slot,
                SaveGameGeneration expectedHead,
                DateTime createdAtUtc,
                DateTime observedUtc,
                out RuntimeSaveGameCheckpointRequest request)
            {
                request = new RuntimeSaveGameCheckpointRequest(
                    new SaveGameProfileId("wrong-profile"),
                    slot,
                    expectedHead,
                    createdAtUtc,
                    observedUtc,
                    null,
                    null);
                return true;
            }
        }

        private sealed class CountingQuiescenceChecker : IRuntimeCheckpointQuiescenceChecker
        {
            public int CheckCount { get; private set; }
            public RuntimeCheckpointQuiescenceResult CheckQuiescence(
                in RuntimeCheckpointContext context)
            {
                CheckCount++;
                return RuntimeCheckpointQuiescenceResult.Ready();
            }
        }

        private sealed class CheckpointFixture
        {
            public CheckpointFixture(
                ApplicationPlatformLifecycleBridge bridge,
                ApplicationLiveFrameDriver driver,
                ApplicationPersistenceLiveCheckpointCapability capability)
            {
                Bridge = bridge;
                Driver = driver;
                Capability = capability;
            }
            public ApplicationPlatformLifecycleBridge Bridge { get; }
            public ApplicationLiveFrameDriver Driver { get; }
            public ApplicationPersistenceLiveCheckpointCapability Capability { get; }
        }

        private sealed class RecordingLease : IApplicationLiveSessionLease
        {
            public RecordingLease(string sessionId) => SessionId = sessionId;
            public string SessionId { get; }
            public void Dispose() { }
        }
    }
}
