using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestAuthorityFailureTests
    {
        [Test]
        public void SuspensionRetainsFirstFrameAndExceptionAcrossFurtherFailures()
        {
            var state = new CharacterTestAuthorityTickState();
            var original = new InvalidOperationException("partial mutation");
            state.Suspend(new RuntimeFrame(7), original);
            state.Suspend(new RuntimeFrame(8), new InvalidOperationException("later failure"));
            Assert.That(state.FailedFrame.Value, Is.EqualTo(7));
            Assert.That(state.Failure, Is.SameAs(original));
            Assert.That(Assert.Throws<InvalidOperationException>(state.ThrowIfSuspended).InnerException,
                Is.SameAs(original));
        }

        [Test]
        public void InvalidFailureDoesNotSuspendHealthySession()
        {
            var state = new CharacterTestAuthorityTickState();
            Assert.Throws<ArgumentNullException>(() => state.Suspend(RuntimeFrame.Zero, null));
            Assert.DoesNotThrow(state.ThrowIfSuspended);
        }

        [Test]
        public void RecoveryRequiresNewOwnerAndCannotResetFailedOwner()
        {
            var failed = new CharacterTestAuthorityTickState();
            failed.Suspend(RuntimeFrame.Zero, new InvalidOperationException("fault"));
            var rebuilt = new CharacterTestAuthorityTickState();
            Assert.DoesNotThrow(rebuilt.ThrowIfSuspended);
            Assert.Throws<InvalidOperationException>(failed.ThrowIfSuspended);
        }

        [TestCase(RuntimeTickStage.PreSimulation)]
        [TestCase(RuntimeTickStage.Simulation)]
        [TestCase(RuntimeTickStage.PostSimulation)]
        public void FailFastHostRetainsDiagnosticAndNeverInvokesLaterModule(RuntimeTickStage stage)
        {
            using var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.FailFast });
            var fault = new TickProbe("fault", stage, 0, true);
            var later = new TickProbe("later", stage, 1, false);
            host.RegisterModule(fault); host.RegisterModule(later); host.Initialize(); host.Start();
            RuntimeHostException failure = Assert.Throws<RuntimeHostException>(() => host.Tick(9, 1d / 60d));
            Assert.That(later.Attempts, Is.Zero);
            Assert.That(host.LastTickAttemptSucceeded, Is.False);
            Assert.That(host.IsTickInProgress, Is.False);
            Assert.That(host.CaptureDiagnostics().Errors.Count, Is.EqualTo(1));
            Assert.That(failure.Error.FrameIndex, Is.EqualTo(9));
            Assert.That(failure.Error.TickStage, Is.EqualTo(stage));
        }

        [Test]
        public void SuspendedAdmissionPreventsAutomaticFixedStepRetry()
        {
            var state = new CharacterTestAuthorityTickState();
            var driver = new CharacterTestFixedTickDriver { CanStepPredicate = () => !state.IsSuspended };
            int attempts = 0;
            Action<double> tick = _ => { attempts++; state.Suspend(RuntimeFrame.Zero,
                new InvalidOperationException("tick failed")); throw state.Failure; };
            Assert.Throws<InvalidOperationException>(() => driver.Step(1, 1d / 60d, tick));
            for (int i = 0; i < 20; i++) Assert.That(driver.Step(1, 1d / 60d, tick), Is.Zero);
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(driver.AccumulatorSeconds, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedAuthorityCannotCaptureNormalOrPeerSnapshot(bool peer)
        {
            var state = Suspended();
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, null, null, null, null,
                authorityAvailabilityGuard: state.ThrowIfSuspended);
            Assert.Throws<InvalidOperationException>(() => {
                if (peer) participant.CaptureSnapshot(RuntimeFrame.Zero, "peer");
                else participant.CaptureSnapshot(RuntimeFrame.Zero);
            });
        }

        [Test]
        public void FailedAuthorityCannotCaptureRollbackCheckpoint()
        {
            var state = Suspended();
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, null, null, null, null,
                authorityAvailabilityGuard: state.ThrowIfSuspended);
            Assert.Throws<InvalidOperationException>(() =>
                CharacterTestAuthorityRollbackCheckpoint.Capture(participant, RuntimeFrame.Zero));
        }

        [Test]
        public void ExistingCheckpointCannotResumeFailedAuthority()
        {
            var state = new CharacterTestAuthorityTickState();
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, null, null, null, null,
                authorityAvailabilityGuard: state.ThrowIfSuspended);
            var checkpoint = CharacterTestAuthorityRollbackCheckpoint.Capture(participant, RuntimeFrame.Zero);
            state.Suspend(RuntimeFrame.Zero, new InvalidOperationException("partial frame"));
            Assert.Throws<InvalidOperationException>(() => checkpoint.Restore(participant, RuntimeFrame.Zero, false));
            Assert.That(state.IsSuspended, Is.True);
        }

        [Test]
        public void SuspendedRestoreRejectsBeforeParsingEvenInvalidSnapshot()
        {
            var state = Suspended();
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, null, null, null, null,
                authorityAvailabilityGuard: state.ThrowIfSuspended);
            Assert.Throws<InvalidOperationException>(() => participant.ApplySnapshot(RuntimeFrame.Zero, null));
            Assert.Throws<InvalidOperationException>(() => participant.ApplyRollbackSnapshot(RuntimeFrame.Zero, null, false));
        }

        [Test]
        public void RejectedRollbackRestoreCannotNormalizeOrMutateCallerDocument()
        {
            var state = Suspended();
            var participant = new CharacterTestNetworkSnapshotParticipant(null, null, null, null, null, null,
                authorityAvailabilityGuard: state.ThrowIfSuspended);
            var document = new CharacterTestNetworkSnapshotDocument { SchemaVersion = 0, Characters = null };
            Assert.Throws<InvalidOperationException>(() => participant.ApplyRollbackDocument(RuntimeFrame.Zero, document, false));
            Assert.That(document.SchemaVersion, Is.Zero);
            Assert.That(document.Characters, Is.Null);
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void RealSliceFaultDoesNotAdvanceClockAcceptCommandsOrCheckpointAndAllowsDiagnosticsAndDispose()
        {
            var input = new FaultInput();
            using var slice = CreateSlice(input);
            RuntimeFrame frame = slice.CurrentFrame;
            double elapsed = slice.ElapsedSeconds;
            input.Armed = true;
            RuntimeHostException failure = Assert.Throws<RuntimeHostException>(() => slice.Tick(1d / 60d));
            Assert.That(slice.IsAuthoritySuspended, Is.True);
            Assert.That(slice.AuthorityFailure, Is.SameAs(failure));
            Assert.That(slice.FailedAuthorityFrame, Is.EqualTo(frame));
            Assert.That(slice.CurrentFrame, Is.EqualTo(frame));
            Assert.That(slice.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.Throws<InvalidOperationException>(() => slice.Tick(1d / 60d));
            Assert.That(input.FaultAttempts, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => slice.Services.Commands.EnqueueRuntimeCommand(default));
            var snapshot = slice.Services.Network.CreateSnapshotParticipant();
            Assert.Throws<InvalidOperationException>(() => snapshot.CaptureSnapshot(frame));
            Assert.Throws<InvalidOperationException>(() => CharacterTestAuthorityRollbackCheckpoint.Capture(snapshot, frame));
            Assert.Throws<InvalidOperationException>(() => slice.Services.Network.RestoreNetworkRollbackRuntimeState(frame, elapsed));
            Assert.DoesNotThrow(() => slice.CaptureDiagnostics());
            Assert.DoesNotThrow(slice.Dispose);
            Assert.DoesNotThrow(slice.Dispose);
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void ExplicitNewSliceCanTickAfterOldSliceWasDisposed()
        {
            var input = new FaultInput();
            var failed = CreateSlice(input);
            input.Armed = true;
            Assert.Throws<RuntimeHostException>(() => failed.Tick(1d / 60d));
            failed.Dispose();
            using var rebuilt = CreateSlice(new FaultInput());
            RuntimeFrame before = rebuilt.CurrentFrame;
            rebuilt.Tick(1d / 60d);
            Assert.That(rebuilt.IsAuthoritySuspended, Is.False);
            Assert.That(rebuilt.CurrentFrame, Is.EqualTo(before.Next()));
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void LateNetworkPumpFailureSuspendsBothSlicesWithoutPublishingPartialReplay()
        {
            var transport = new FaultTransport();
            using var runner = new CharacterTestNetworkSessionRunner(transport, transport, createServerViews: false);
            runner.ServerSlice.Services.Commands.SetLocalInputProvider(new FaultInput());
            runner.BeginReplayRecording();
            RuntimeFrame attempted = runner.ServerSlice.CurrentFrame;
            transport.FailOnPoll = transport.Polls + 3; // Initial server/client pump, then post-authority server pump.
            Assert.Throws<InvalidOperationException>(() => runner.Tick(1d / 60d));
            Assert.That(runner.ServerSlice.CurrentFrame, Is.EqualTo(attempted.Next()));
            Assert.That(runner.ServerSlice.IsAuthoritySuspended, Is.True);
            Assert.That(runner.ClientSlice.IsAuthoritySuspended, Is.True);
            Assert.That(runner.CreateReplayCapture().FrameCount, Is.Zero);
            int pollsAfterFailure = transport.Polls;
            Assert.Throws<InvalidOperationException>(() => runner.Tick(1d / 60d));
            Assert.Throws<InvalidOperationException>(() => runner.SubmitClientCommand(default));
            Assert.That(transport.Polls, Is.EqualTo(pollsAfterFailure));
            Assert.DoesNotThrow(() => runner.CaptureDiagnostics());
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void HostClientFailureCannotPublishServerReplayAndClosesBothInputs()
        {
            var transport = new FaultTransport();
            using var host = new CharacterTestUnityTransportHostRunner(transport, transport, createClientViews: false);
            host.Slice.Services.Commands.SetLocalInputProvider(new FaultInput());
            host.BeginReplayRecording();
            RuntimeFrame attempted = host.ServerRunner.Slice.CurrentFrame;
            transport.FailReceiver = "client-0";
            Assert.Throws<InvalidOperationException>(() => host.Tick(1d / 60d));
            Assert.That(host.ServerRunner.Slice.CurrentFrame, Is.EqualTo(attempted.Next()));
            Assert.That(host.ServerRunner.Slice.IsAuthoritySuspended, Is.True);
            Assert.That(host.Slice.IsAuthoritySuspended, Is.True);
            Assert.That(host.CreateReplayCapture().FrameCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => host.Slice.Services.Commands.EnqueueRuntimeCommand(default));
            Assert.Throws<InvalidOperationException>(host.ServerRunner.PublishHostReplayFrame);
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void DeferredHostReplayPublishesExactlyOnceAfterExplicitSuccessfulCommit()
        {
            var transport = new FaultTransport();
            using var server = new CharacterTestUnityTransportServerRunner(transport, createServerViews: false);
            server.BeginReplayRecording();
            server.TickForHost(1d / 60d);
            Assert.That(server.CreateReplayCapture().FrameCount, Is.Zero);
            server.PublishHostReplayFrame();
            server.PublishHostReplayFrame();
            Assert.That(server.CreateReplayCapture().FrameCount, Is.EqualTo(1));
            server.Tick(1d / 60d);
            Assert.That(server.CreateReplayCapture().FrameCount, Is.EqualTo(2));
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void FrameOverflowCannotCommitElapsedTimeOrResumeAuthority()
        {
            using var slice = CreateSlice(new FaultInput());
            slice.Clock.Reset(new RuntimeFrame(long.MaxValue));
            double elapsed = slice.ElapsedSeconds;
            Assert.Catch<Exception>(() => slice.Tick(1d / 60d));
            Assert.That(slice.IsAuthoritySuspended, Is.True);
            Assert.That(slice.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(slice.CurrentFrame.Value, Is.EqualTo(long.MaxValue));
            Assert.Throws<InvalidOperationException>(() => slice.Tick(1d / 60d));
        }

        private static CharacterTestAuthorityTickState Suspended()
        {
            var state = new CharacterTestAuthorityTickState();
            state.Suspend(RuntimeFrame.Zero, new InvalidOperationException("fault"));
            return state;
        }

        private static GameSlice CreateSlice(IInputProvider input)
        {
            var manager = new ResourceManager();
            return new GameSlice(new CharacterTestSliceOptions {
                Resources = new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Editor, manager,
                    new ResourcePreloadService(manager)), CreateCharacterViews = false, EnableTraceFileWrite = false,
                EnableLocalInputCommands = true, InputProvider = input, EnableStoryBootstrap = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true });
        }

        private sealed class TickProbe : RuntimeModule
        {
            private readonly bool _fail;
            internal int Attempts;
            internal TickProbe(string id, RuntimeTickStage stage, int priority, bool fail) : base(id, stage, priority)
                { _fail = fail; }
            public override void Tick(RuntimeTickContext context)
                { Attempts++; if (_fail) throw new InvalidOperationException("partial mutation"); }
        }

        private sealed class FaultInput : IInputProvider
        {
            internal bool Armed;
            internal int FaultAttempts;
            public InputSnapshot Snapshot => default;
            public InputCommandQueue Commands { get; } = new InputCommandQueue();
            public InputContext CurrentContext => InputContext.Gameplay;
            public bool ContainsContext(InputContext context) => true;
            public bool IsContextEnabled(InputContext context)
                { if (Armed) { FaultAttempts++; throw new InvalidOperationException("input tick failure"); } return true; }
            public void SetContext(InputContext context) { }
            public IDisposable PushContext(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive) => null;
        }

        // Fault injection decorates the real loopback transport; it does not replace Unity native semantics.
        private sealed class FaultTransport : INetworkTransport
        {
            private readonly LoopbackNetworkTransport _inner = new LoopbackNetworkTransport();
            internal int Polls;
            internal int FailOnPoll;
            internal string FailReceiver;
            public string TransportId => _inner.TransportId;
            public void Send(NetworkMessageEnvelope message) => _inner.Send(message);
            public int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output)
            {
                Polls++;
                if (Polls == FailOnPoll || receiverPeerId == FailReceiver)
                    throw new InvalidOperationException("injected transport poll failure");
                return _inner.Poll(receiverPeerId, frame, output);
            }
            public int PollEvents(string peerId, RuntimeFrame frame, List<NetworkTransportEvent> output)
                => _inner.PollEvents(peerId, frame, output);
            public NetworkTransportSnapshot CreateSnapshot(string peerId) => _inner.CreateSnapshot(peerId);
            public void Clear() => _inner.Clear();
        }
    }
}
