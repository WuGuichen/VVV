using System;
using System.Reflection;
using MxFramework.ApplicationStartup;
using MxFramework.ApplicationStartup.PlatformBridge;
using MxFramework.Platform;
using NUnit.Framework;

namespace MxFramework.Tests.ApplicationStartup
{
    public sealed class ApplicationPlatformLifecycleBridgeTests
    {
        [Test]
        public void CompositionProof_RequiresExactPublisherFenceAndSource()
        {
            var source = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(3, fence);
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                source, publisher, fence, new RecordingPolicy(), 4, out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));

            Assert.That(bridge.UsesComposition(publisher, fence, source), Is.True);
            Assert.That(bridge.UsesComposition(new LiveApplicationStatePublisher(3, fence), fence, source), Is.False);
            Assert.That(bridge.UsesComposition(publisher, new ApplicationPublicationSaveFence(), source), Is.False);
            Assert.That(bridge.UsesComposition(
                publisher,
                fence,
                new ApplicationLifecycleBuffer(new ApplicationLifecycleSnapshot(true, false, false, 0), 4)),
                Is.False);
        }

        [Test]
        public void PublisherProof_ReportsExactRetirementCapacityAndFence()
        {
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(7, fence);

            Assert.That(publisher.RetirementCapacity, Is.EqualTo(7));
            Assert.That(publisher.UsesPublicationSaveFence(fence), Is.True);
            Assert.That(publisher.UsesPublicationSaveFence(new ApplicationPublicationSaveFence()), Is.False);
        }

        [Test]
        public void SaveFence_DefersPublicationBeforeCandidateMutation()
        {
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            LiveApplicationState candidate = Candidate("candidate");

            Assert.That(fence.TryAcquireSave(out ApplicationPublicationSaveFenceLease save), Is.True);
            ApplicationStartupCommitResult deferred = publisher.TryPublish(publisher.Current, candidate);
            Assert.That(deferred.Status, Is.EqualTo(ApplicationStartupCommitStatus.PublicationDeferred));

            save.Dispose();
            Assert.That(publisher.TryPublish(publisher.Current, candidate).Success, Is.True);
        }

        [Test]
        public void SecondBridge_FailsBeforeItCanDrainAnyEvidence()
        {
            var buffer = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), 4);
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            var policy = new RecordingPolicy();
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                buffer, publisher, fence, policy, 4, out ApplicationPlatformLifecycleBridge first),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            buffer.ReportSuspend(true);

            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                buffer, publisher, fence, policy, 4, out ApplicationPlatformLifecycleBridge second),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.DrainAlreadyClaimed));
            Assert.That(second, Is.Null);
            Assert.That(first.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DeferredAtBootOrMissingCapability));
            Assert.That(first.PendingEvidence.SawSuspend, Is.True);
        }

        [Test]
        public void InvalidLifecycleEvidence_FailsClosedBeforePendingMutation()
        {
            var source = new InvalidLifecycleSource();
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            var policy = new RecordingPolicy();
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                source, publisher, fence, policy, 4, out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));

            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence));
            Assert.That(bridge.HasPendingEvidence, Is.False);
            Assert.That(policy.BeginCount, Is.Zero);
        }

        [Test]
        public void LifecycleRevisionGap_FailsClosedWithoutAdvancingEvidence()
        {
            var source = new SequenceLifecycleSource(2);
            source.Enqueue(Change(ApplicationLifecycleChangeKind.FocusChanged, 2));
            ApplicationPlatformLifecycleBridge bridge = CreateForSource(source, out _, out _);
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence));
            Assert.That(bridge.HasPendingEvidence, Is.False);
        }

        [Test]
        public void OutOfOrderOrdinary_DoesNotOverwriteLastAcceptedRevision()
        {
            var source = new SequenceLifecycleSource(3);
            source.Enqueue(Change(ApplicationLifecycleChangeKind.FocusChanged, 1));
            source.Enqueue(Change(ApplicationLifecycleChangeKind.FocusChanged, 1));
            ApplicationPlatformLifecycleBridge bridge = CreateForSource(source, out _, out _);
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence));
            Assert.That(bridge.PendingEvidence.ChangeCount, Is.EqualTo(1L));
            source.Enqueue(Change(ApplicationLifecycleChangeKind.FocusChanged, 2));
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.Idle));
            Assert.That(bridge.PendingEvidence.LastRevision, Is.EqualTo(2L));
        }

        [Test]
        public void ContradictoryResyncStart_FailsClosed()
        {
            var source = new SequenceLifecycleSource(1);
            source.Enqueue(Change(ApplicationLifecycleChangeKind.StateResyncRequired, 3, 2, 3, 2));
            ApplicationPlatformLifecycleBridge bridge = CreateForSource(source, out _, out _);
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence));
            Assert.That(bridge.HasPendingEvidence, Is.False);
        }

        [Test]
        public void RevisionMax_AllowsOnlyContinuousSaturatedResync()
        {
            var source = new SequenceLifecycleSource(2);
            source.Enqueue(Change(ApplicationLifecycleChangeKind.StateResyncRequired, long.MaxValue, 1, long.MaxValue, 1));
            source.Enqueue(Change(ApplicationLifecycleChangeKind.StateResyncRequired, long.MaxValue, long.MaxValue, long.MaxValue, 1));
            ApplicationPlatformLifecycleBridge bridge = CreateForSource(source, out _, out _);
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.Idle));
            Assert.That(bridge.PendingEvidence.LostRevisionStart, Is.EqualTo(1L));
            Assert.That(bridge.PendingEvidence.LostRevisionEnd, Is.EqualTo(long.MaxValue));
            Assert.That(bridge.PendingEvidence.ChangeCount, Is.EqualTo(2L));
        }

        [Test]
        public void InvalidEvidenceDuringActiveSave_RemainsObservableAfterTerminalPoll()
        {
            var source = new SequenceLifecycleSource(2);
            source.Enqueue(Change(ApplicationLifecycleChangeKind.SuspendChanged, 1, isSuspended: true));
            ApplicationPlatformLifecycleBridge bridge = CreateForSource(source, out LiveApplicationStatePublisher publisher, out RecordingPolicy policy);
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(publisher.TryPublish(publisher.Current, Candidate("active-invalid")).Success, Is.True);
            Assert.That(driver.Tick(0.016).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            policy.LastOperation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Succeeded;
            source.Enqueue(default);

            Assert.That(bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.InvalidLifecycleEvidence));
            Assert.That(bridge.HasActiveCheckpoint, Is.False);
            Assert.That(bridge.LastCheckpointStatus, Is.EqualTo(ApplicationLifecycleCheckpointOperationStatus.Succeeded));
        }

        [Test]
        public void BootEvidence_IsRetainedAndUsesExactStateAfterPublication()
        {
            Harness harness = CreateHarness();
            harness.Buffer.ReportSuspend(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DeferredAtBootOrMissingCapability));
            Assert.That(harness.Policy.BeginCount, Is.Zero);
            LiveApplicationState candidate = Candidate("live");
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, candidate).Success, Is.True);
            Assert.That(harness.Driver.Tick(0.016).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(harness.Policy.BeginCount, Is.EqualTo(1));
            Assert.That(harness.Policy.LastState, Is.SameAs(harness.Publisher.Current));
        }

        [Test]
        public void BeforeFirstSuccessfulTick_CheckpointIsDeferredWithoutPolicyCall()
        {
            Harness harness = CreateHarness();
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, Candidate("unticked")).Success, Is.True);
            harness.Buffer.ReportSuspend(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DeferredUntilSuccessfulRuntimeTick));
            Assert.That(harness.Policy.BeginCount, Is.Zero);
            Assert.That(harness.Driver.Tick(0.016).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            harness.Bridge.RequestRetry();
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
        }

        [Test]
        public void FailedRuntimeAttempt_DoesNotQualifyCheckpoint()
        {
            Harness harness = CreateHarness();
            Assert.That(harness.Publisher.TryPublish(
                harness.Publisher.Current,
                Candidate("failed", ApplicationLiveRuntimeAttemptStatus.Failed)).Success, Is.True);
            Assert.That(harness.Driver.Tick(0.016).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptFailed));
            harness.Buffer.ReportSuspend(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DeferredUntilSuccessfulRuntimeTick));
            Assert.That(harness.Policy.BeginCount, Is.Zero);
        }

        [Test]
        public void NestedPolicyPump_IsRejectedBeforeScratchMutation()
        {
            Harness harness = CreateLiveHarness();
            ApplicationPlatformLifecyclePumpStatus nested = default;
            harness.Policy.OnBegin = () => nested = harness.Bridge.Pump(true);
            harness.Buffer.ReportSuspend(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(nested, Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.Reentrant));
            Assert.That(harness.Bridge.HasActiveCheckpoint, Is.True);
        }

        [Test]
        public void NestedOperationPoll_IsRejectedWithoutLosingFenceLease()
        {
            Harness harness = CreateLiveHarness();
            harness.Buffer.ReportSuspend(true);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            ApplicationPlatformLifecyclePumpStatus nested = default;
            harness.Policy.LastOperation.OnPoll = () => nested = harness.Bridge.Pump(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointPending));
            Assert.That(nested, Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.Reentrant));
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, Candidate("blocked")).Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationDeferred));
        }

        [Test]
        public void ActiveCheckpoint_PreservesOneSuccessorEpoch()
        {
            Harness harness = CreateLiveHarness();
            harness.Buffer.ReportSuspend(true);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));

            harness.Buffer.ReportSuspend(false);
            harness.Buffer.ReportSuspend(true);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointPending));
            harness.Policy.LastOperation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Succeeded;
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded));
            Assert.That(harness.Bridge.HasPendingEvidence, Is.True);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(harness.Policy.BeginCount, Is.EqualTo(2));
        }

        [Test]
        public void DrainBudgetPressure_CannotStarveActiveTerminalPoll()
        {
            Harness harness = CreateLiveHarness(drainBudget: 1);
            harness.Buffer.ReportSuspend(true);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.DrainBudgetExhausted));
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));

            harness.Policy.LastOperation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Succeeded;
            harness.Buffer.ReportFocus(false);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointSucceeded));
            Assert.That(harness.Policy.LastOperation.PollCount, Is.EqualTo(1));
        }

        [Test]
        public void OverflowHiddenSuspend_IsCheckpointWorthy()
        {
            Harness harness = CreateLiveHarness(capacity: 1);
            harness.Buffer.ReportFocus(false);
            harness.Buffer.ReportSuspend(true);
            harness.Buffer.ReportSuspend(false);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(harness.Policy.LastEvidence.SawSuspend, Is.True);
            Assert.That(harness.Policy.LastEvidence.SawStateResync, Is.True);
            Assert.That(harness.Policy.LastEvidence.HasLostRevisionRange, Is.True);
            Assert.That(harness.Policy.LastEvidence.LostRevisionStart, Is.EqualTo(1L));
            Assert.That(harness.Policy.LastEvidence.LostRevisionEnd, Is.EqualTo(3L));
        }

        [Test]
        public void RevisionExhaustionResync_PreservesExactLostBounds()
        {
            Harness harness = CreateLiveHarness(capacity: 2);
            harness.Buffer.ReportFocus(false);
            FieldInfo current = typeof(ApplicationLifecycleBuffer).GetField(
                "_current", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(current, Is.Not.Null);
            current.SetValue(harness.Buffer, new ApplicationLifecycleSnapshot(false, false, false, long.MaxValue));
            harness.Buffer.ReportSuspend(true);

            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            Assert.That(harness.Policy.LastEvidence.HasLostRevisionRange, Is.True);
            Assert.That(harness.Policy.LastEvidence.LostRevisionStart, Is.EqualTo(1L));
            Assert.That(harness.Policy.LastEvidence.LostRevisionEnd, Is.EqualTo(long.MaxValue));
            Assert.That(harness.Policy.LastEvidence.FirstRevision, Is.EqualTo(1L));
            Assert.That(harness.Policy.LastEvidence.LastRevision, Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void FailedCheckpoint_RequiresExplicitRetryOrNewWorthyFact()
        {
            Harness harness = CreateLiveHarness();
            harness.Buffer.ReportSuspend(true);
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            harness.Policy.LastOperation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Failed;
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointFailed));
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.NotArmed));

            harness.Bridge.RequestRetry();
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
        }

        [Test]
        public void ActiveSave_DefersPublicationUntilTruthfulTerminalPoll()
        {
            Harness harness = CreateLiveHarness();
            harness.Buffer.ReportQuitting();
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointStarted));
            LiveApplicationState replacement = Candidate("replacement");

            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, replacement).Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationDeferred));
            Assert.That(harness.Bridge.RequestActiveCancellation(), Is.True);
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, replacement).Status,
                Is.EqualTo(ApplicationStartupCommitStatus.PublicationDeferred));

            harness.Policy.LastOperation.StatusValue = ApplicationLifecycleCheckpointOperationStatus.Cancelled;
            Assert.That(harness.Bridge.Pump(true), Is.EqualTo(ApplicationPlatformLifecyclePumpStatus.CheckpointFailed));
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, replacement).Success, Is.True);
        }

        [Test]
        public void EmptyWarmPump_AllocatesZeroBytes()
        {
            Harness harness = CreateHarness();
            for (int i = 0; i < 16; i++) harness.Bridge.Pump(true);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++) harness.Bridge.Pump(true);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        private static Harness CreateLiveHarness(int capacity = 8, int drainBudget = 16)
        {
            Harness harness = CreateHarness(capacity, drainBudget);
            Assert.That(harness.Publisher.TryPublish(harness.Publisher.Current, Candidate("initial")).Success, Is.True);
            Assert.That(harness.Driver.Tick(0.016).Status, Is.EqualTo(ApplicationLiveFrameTickStatus.RuntimeAttemptSucceeded));
            return harness;
        }

        private static Harness CreateHarness(int capacity = 8, int drainBudget = 16)
        {
            var buffer = new ApplicationLifecycleBuffer(
                new ApplicationLifecycleSnapshot(true, false, false, 0), capacity);
            var fence = new ApplicationPublicationSaveFence();
            var publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            var policy = new RecordingPolicy();
            Assert.That(publisher.TryClaimLiveFrameDriver(out ApplicationLiveFrameDriver driver), Is.True);
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                buffer, publisher, fence, policy, drainBudget, out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            return new Harness(buffer, publisher, driver, policy, bridge);
        }

        private static ApplicationPlatformLifecycleBridge CreateForSource(
            IApplicationLifecycleSource source,
            out LiveApplicationStatePublisher publisher,
            out RecordingPolicy policy)
        {
            var fence = new ApplicationPublicationSaveFence();
            publisher = new LiveApplicationStatePublisher(publicationSaveFence: fence);
            policy = new RecordingPolicy();
            Assert.That(ApplicationPlatformLifecycleBridge.TryCreate(
                source, publisher, fence, policy, 16, out ApplicationPlatformLifecycleBridge bridge),
                Is.EqualTo(ApplicationPlatformLifecycleBridgeCreateStatus.Succeeded));
            return bridge;
        }

        private static ApplicationLifecycleChange Change(
            ApplicationLifecycleChangeKind kind,
            long revision,
            long lostStart = 0,
            long lostEnd = 0,
            long lostCount = 0,
            bool isSuspended = false)
        {
            return (ApplicationLifecycleChange)Activator.CreateInstance(
                typeof(ApplicationLifecycleChange),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    kind,
                    new ApplicationLifecycleSnapshot(true, isSuspended, false, revision),
                    lostStart,
                    lostEnd,
                    lostCount,
                    false,
                    false
                },
                null);
        }

        private static LiveApplicationState Candidate(string id)
            => Candidate(id, ApplicationLiveRuntimeAttemptStatus.Succeeded);

        private static LiveApplicationState Candidate(string id, ApplicationLiveRuntimeAttemptStatus attemptStatus)
        {
            var lease = new PumpLease(id, attemptStatus);
            return LiveApplicationState.CreateCandidate(true, 1, lease, null, lease);
        }

        private sealed class Harness
        {
            public Harness(ApplicationLifecycleBuffer buffer, LiveApplicationStatePublisher publisher, ApplicationLiveFrameDriver driver, RecordingPolicy policy, ApplicationPlatformLifecycleBridge bridge)
            { Buffer = buffer; Publisher = publisher; Driver = driver; Policy = policy; Bridge = bridge; }
            public ApplicationLifecycleBuffer Buffer { get; }
            public LiveApplicationStatePublisher Publisher { get; }
            public ApplicationLiveFrameDriver Driver { get; }
            public RecordingPolicy Policy { get; }
            public ApplicationPlatformLifecycleBridge Bridge { get; }
        }

        private sealed class PumpLease : IApplicationLiveSessionLease, IApplicationLiveRuntimePump, IApplicationLiveCheckpointCapability
        {
            private readonly ApplicationLiveRuntimeAttemptStatus _attemptStatus;
            public PumpLease(string id, ApplicationLiveRuntimeAttemptStatus attemptStatus)
            { SessionId = id; _attemptStatus = attemptStatus; }
            public string SessionId { get; }
            public bool CanReplace(IApplicationLiveCheckpointCapability currentCapability) =>
                currentCapability == null || currentCapability is PumpLease;
            public void Dispose() { }
            public void Tick(long frameIndex, double deltaTime, double elapsedTime) { }
            public ApplicationLiveRuntimeAttemptStatus GetTickAttemptStatus(long frameIndex) => _attemptStatus;
        }

        private sealed class RecordingPolicy : IApplicationLifecycleCheckpointPolicy
        {
            public int BeginCount { get; private set; }
            public LiveApplicationState LastState { get; private set; }
            public ApplicationLifecycleCheckpointEvidence LastEvidence { get; private set; }
            public RecordingOperation LastOperation { get; private set; }
            public Action OnBegin { get; set; }
            public ApplicationLifecycleCheckpointBeginResult TryBegin(
                LiveApplicationState exactState,
                in ApplicationLifecycleCheckpointEvidence evidence)
            {
                BeginCount++;
                OnBegin?.Invoke();
                LastState = exactState;
                LastEvidence = evidence;
                LastOperation = new RecordingOperation();
                return ApplicationLifecycleCheckpointBeginResult.Started(LastOperation);
            }
        }

        private sealed class RecordingOperation : IApplicationLifecycleCheckpointOperation
        {
            public ApplicationLifecycleCheckpointOperationStatus StatusValue = ApplicationLifecycleCheckpointOperationStatus.Pending;
            public int PollCount;
            public Action OnPoll;
            public void Poll() { PollCount++; OnPoll?.Invoke(); }
            public ApplicationLifecycleCheckpointOperationStatus Status => StatusValue;
            public bool CanCancel => true;
            public bool RequestCancel() => true;
        }

        private sealed class InvalidLifecycleSource : IApplicationLifecycleSource, IApplicationLifecycleDrainOwner
        {
            private bool _claimed;
            private bool _pending = true;
            public bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner)
            {
                if (_claimed) { drainOwner = null; return false; }
                _claimed = true;
                drainOwner = this;
                return true;
            }
            public ApplicationLifecycleSnapshot Current => default;
            public long DroppedChangeCount => 0L;
            public bool TryDequeue(out ApplicationLifecycleChange change)
            {
                if (!_pending) { change = default; return false; }
                _pending = false;
                change = default;
                return true;
            }
        }

        private sealed class SequenceLifecycleSource : IApplicationLifecycleSource, IApplicationLifecycleDrainOwner
        {
            private readonly ApplicationLifecycleChange[] _changes;
            private int _read;
            private int _count;
            private bool _claimed;
            public SequenceLifecycleSource(int capacity) => _changes = new ApplicationLifecycleChange[capacity];
            public void Enqueue(ApplicationLifecycleChange change) => _changes[_count++] = change;
            public bool TryClaimDrain(out IApplicationLifecycleDrainOwner drainOwner)
            {
                if (_claimed) { drainOwner = null; return false; }
                _claimed = true; drainOwner = this; return true;
            }
            public ApplicationLifecycleSnapshot Current => _count == 0 ? default : _changes[_count - 1].Snapshot;
            public long DroppedChangeCount => 0L;
            public bool TryDequeue(out ApplicationLifecycleChange change)
            {
                if (_read >= _count) { change = default; return false; }
                change = _changes[_read++]; return true;
            }
        }
    }
}
