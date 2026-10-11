using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Audio;
using MxFramework.Game.CharacterTest;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    [Parallelizable(ParallelScope.None)]
    public sealed class CharacterTestCompositionLifetimeTests
    {
        [Test]
        public void PrewarmFailureUnwindsAcquiredConsumersBeforeResourcesAndKeepsEveryError()
        {
            var scope = new CharacterTestCleanupScope();
            var released = new List<string>();
            scope.Own("resources", () => released.Add("resources"));
            scope.Own("views", () => { released.Add("views"); throw new InvalidOperationException("view release"); });
            scope.Own("presenter", () => { released.Add("presenter"); throw new InvalidOperationException("pool release"); });
            var errors = new List<Exception> { new InvalidOperationException("prewarm") };

            scope.Cleanup(errors);
            scope.Cleanup(errors);

            Assert.That(released, Is.EqualTo(new[] { "presenter", "views", "resources" }));
            Assert.That(errors.Count, Is.EqualTo(3));
            Assert.That(errors[0].Message, Is.EqualTo("prewarm"));
            Assert.That(errors[1].InnerException.Message, Is.EqualTo("pool release"));
            Assert.That(errors[2].InnerException.Message, Is.EqualTo("view release"));
        }

        [Test]
        public void RejectedRegistrationRetainsCallerOwnershipAndDoesNotDoubleDisposeAcceptedModule()
        {
            var released = new List<string>();
            var host = new RuntimeHost();
            var composition = new CharacterTestSliceComposition(new CharacterTestSliceModules { Host = host });
            composition.OwnCleanup("resources", () => released.Add("resources"));
            composition.OwnCleanup("host", composition.DisposeHost);
            var accepted = composition.OwnRuntimeModule(new ProbeModule("duplicate", "accepted", released));
            var rejected = composition.OwnRuntimeModule(new ProbeModule("duplicate", "rejected", released));

            composition.RegisterRuntimeModule(accepted, profiled: false);
            Assert.Throws<InvalidOperationException>(() => composition.RegisterRuntimeModule(rejected, profiled: false));
            composition.Dispose();
            composition.Dispose();

            Assert.That(released, Is.EqualTo(new[] { "rejected", "accepted", "resources" }));
            Assert.That(accepted.DisposeCount, Is.EqualTo(1));
            Assert.That(rejected.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedInitializeAlreadyCompensatedByHostIsNotDisposedAgainByComposition()
        {
            var released = new List<string>();
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue });
            var composition = new CharacterTestSliceComposition(new CharacterTestSliceModules { Host = host });
            composition.OwnCleanup("resources", () => released.Add("resources"));
            composition.OwnCleanup("host", composition.DisposeHost);
            var first = composition.OwnRuntimeModule(new ProbeModule("a", "first", released) { FailInitialize = true });
            var unentered = composition.OwnRuntimeModule(new ProbeModule("b", "unentered", released));
            composition.RegisterRuntimeModule(first, profiled: false);
            composition.RegisterRuntimeModule(unentered, profiled: false);

            host.Initialize();
            Assert.That(host.State, Is.EqualTo(RuntimeLifecycleState.Faulted));
            composition.Dispose();

            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(unentered.DisposeCount, Is.EqualTo(1));
            Assert.That(released.Last(), Is.EqualTo("resources"));
        }

        [TestCase(RuntimeHostErrorPolicy.CollectAndContinue)]
        [TestCase(RuntimeHostErrorPolicy.FailFast)]
        public void StopAndDisposeFaultsReleaseResourcesAndReportEveryHostError(RuntimeHostErrorPolicy policy)
        {
            var released = new List<string>();
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = policy });
            var composition = new CharacterTestSliceComposition(new CharacterTestSliceModules { Host = host });
            composition.OwnCleanup("resources", () => released.Add("resources"));
            composition.OwnCleanup("host", composition.DisposeHost);
            var first = composition.OwnRuntimeModule(new ProbeModule("a", "first", released) { FailStop = true, FailDispose = true });
            var second = composition.OwnRuntimeModule(new ProbeModule("b", "second", released) { FailStop = true, FailDispose = true });
            composition.RegisterRuntimeModule(first, profiled: false);
            composition.RegisterRuntimeModule(second, profiled: false);
            host.Initialize();
            host.Start();

            AggregateException failure = Assert.Throws<AggregateException>(() => composition.Dispose());
            composition.Dispose();

            Assert.That(failure.InnerExceptions.OfType<RuntimeHostException>().Count(), Is.EqualTo(4));
            Assert.That(released.Last(), Is.EqualTo("resources"));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(second.DisposeCount, Is.EqualTo(1));
            Assert.That(host.State, Is.EqualTo(RuntimeLifecycleState.Disposed));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CollectingLifecycleFaultCannotPublishStartedComposition(bool failStart)
        {
            var released = new List<string>();
            var host = new RuntimeHost(new RuntimeHostOptions { ErrorPolicy = RuntimeHostErrorPolicy.CollectAndContinue });
            var composition = new CharacterTestSliceComposition(new CharacterTestSliceModules { Host = host });
            composition.OwnCleanup("resources", () => released.Add("resources"));
            composition.OwnCleanup("host", composition.DisposeHost);
            var module = composition.OwnRuntimeModule(new ProbeModule("fault", "fault", released)
                { FailInitialize = !failStart, FailStart = failStart });
            composition.RegisterRuntimeModule(module, profiled: false);

            RuntimeHostException failure = Assert.Throws<RuntimeHostException>(() => composition.InitializeAndStartHost());
            composition.Dispose();

            Assert.That(failure.Error.Operation, Is.EqualTo(failStart ? RuntimeHostOperation.Start : RuntimeHostOperation.Initialize));
            Assert.That(module.DisposeCount, Is.EqualTo(1));
            Assert.That(released.Last(), Is.EqualTo("resources"));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("RequiresUnityNative")]
        public void ConstructionFailureBeforeModulesHonorsIncomingAudioOwnership(bool owned)
        {
            var audio = new ProbeAudio();
            var options = HeadlessOptions(audio, owned);
            options.Logger = new ThrowingLogger("Construct");

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => new GameSlice(options));

            Assert.That(failure.Message, Is.EqualTo("log Construct"));
            Assert.That(audio.DisposeCount, Is.EqualTo(owned ? 1 : 0));
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void BootstrapFailurePreservesOriginalExceptionWhenAudioRollbackAlsoFails()
        {
            var audio = new ProbeAudio { FailDispose = true };
            var options = HeadlessOptions(audio, true);
            var original = new InvalidOperationException("bootstrap");
            options.SubscribeBeforeStoryBootstrap = _ => throw original;

            AggregateException failure = Assert.Throws<AggregateException>(() => new GameSlice(options));

            Assert.That(failure.InnerExceptions[0], Is.SameAs(original));
            Assert.That(failure.Flatten().InnerExceptions.Any(error => error.InnerException?.Message == "audio release"), Is.True);
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void ThrowingBootstrapSubscriptionCannotSkipAudioCleanupOrReviveDisposedSlice()
        {
            var audio = new ProbeAudio();
            var subscription = new ProbeSubscription();
            var options = HeadlessOptions(audio, true);
            options.SubscribeBeforeStoryBootstrap = _ => subscription;
            var slice = new GameSlice(options);

            Assert.Throws<AggregateException>(() => slice.Dispose());
            slice.Dispose();

            Assert.That(subscription.DisposeCount, Is.EqualTo(1));
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => slice.Tick(1d / 60d));
        }

        [Test]
        public void AudioSnapshotFailureReleasesUnpublishedServiceOnce()
        {
            var audio = new ProbeAudio { FailSnapshot = true };
            Assert.That(CharacterTestAudioStartupFactory.TryCreate(() => audio, null), Is.Null);
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulAudioInitializationTransfersLiveServiceToCaller()
        {
            var audio = new ProbeAudio { Initialized = true };
            Assert.That(CharacterTestAudioStartupFactory.TryCreate(() => audio, null), Is.SameAs(audio));
            Assert.That(audio.DisposeCount, Is.Zero);
            audio.Dispose();
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void AudioPublicationDiagnosticFailureDoesNotLeakInitializedService()
        {
            var audio = new ProbeAudio { Initialized = true };
            var logger = new ThrowingLogger("CharacterTest audio service initialized.");
            Assert.That(CharacterTestAudioStartupFactory.TryCreate(() => audio, logger), Is.Null);
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void UnavailableAudioIsReleasedBeforeOptionalFallback()
        {
            var audio = new ProbeAudio();
            Assert.That(CharacterTestAudioStartupFactory.TryCreate(() => audio, null), Is.Null);
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void UnavailableAudioCleanupFaultCannotBeHiddenAsOptionalFallback()
        {
            var audio = new ProbeAudio { FailDispose = true };
            var failure = Assert.Throws<AggregateException>(() => CharacterTestAudioStartupFactory.TryCreate(() => audio, null));
            Assert.That(failure.Flatten().InnerExceptions.Any(error => error.InnerException?.Message == "audio release"), Is.True);
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void AudioInitializationAndCleanupFailuresKeepOriginalCauseAndAttemptOnce()
        {
            var audio = new ProbeAudio { FailSnapshot = true, FailDispose = true };
            var failure = Assert.Throws<AggregateException>(() => CharacterTestAudioStartupFactory.TryCreate(() => audio, null));
            Assert.That(failure.InnerExceptions[0].Message, Is.EqualTo("audio snapshot"));
            Assert.That(failure.InnerExceptions[1].InnerException.Message, Is.EqualTo("audio release"));
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void AudioFactoryRollbackFailureRemainsObservableWithoutAPublishedCandidate()
        {
            var original = new AggregateException("backend rollback", new InvalidOperationException("backend release"));
            var failure = Assert.Throws<AggregateException>(() => CharacterTestAudioStartupFactory.TryCreate(() => throw original, null));
            Assert.That(failure.InnerExceptions[0], Is.SameAs(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("RequiresUnityNative")]
        public void NetworkAcceptanceFailureReleasesEachOwnedTransportExactlyOnce(bool sharedTransport)
        {
            var released = new List<string>();
            var server = new ProbeTransport("server", released);
            var client = sharedTransport ? server : new ProbeTransport("client", released);
            var audio = new ProbeAudio();
            var original = new InvalidOperationException("acceptance");

            var failure = Assert.Throws<InvalidOperationException>(() => new CharacterTestNetworkSessionRunner(
                server, client, useExpandedReplayContract: true, audioService: audio, disposeAudioService: true,
                incomingOwnershipAccepted: () => throw original));

            Assert.That(failure, Is.SameAs(original));
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
            Assert.That(server.DisposeCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
            Assert.That(released, Is.EqualTo(sharedTransport ? new[] { "server" } : new[] { "client", "server" }));
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void NetworkAcceptanceFailureDoesNotReleaseBorrowedAudioOrTransports()
        {
            var released = new List<string>();
            var transport = new ProbeTransport("borrowed", released);
            var audio = new ProbeAudio();
            Assert.Throws<InvalidOperationException>(() => new CharacterTestNetworkSessionRunner(
                transport, transport, useExpandedReplayContract: true, disposeTransports: false,
                audioService: audio, disposeAudioService: false,
                incomingOwnershipAccepted: () => throw new InvalidOperationException("acceptance")));
            Assert.That(audio.DisposeCount, Is.Zero);
            Assert.That(transport.DisposeCount, Is.Zero);
        }

        [Test]
        [Category("RequiresUnityNative")]
        public void NetworkAcceptanceFailureRetainsOriginalAndEveryRollbackFailure()
        {
            var released = new List<string>();
            var server = new ProbeTransport("server", released) { FailDispose = true };
            var client = new ProbeTransport("client", released) { FailDispose = true };
            var audio = new ProbeAudio { FailDispose = true };
            var original = new InvalidOperationException("acceptance");
            var failure = Assert.Throws<AggregateException>(() => new CharacterTestNetworkSessionRunner(
                server, client, useExpandedReplayContract: true, audioService: audio, disposeAudioService: true,
                incomingOwnershipAccepted: () => throw original));
            Assert.That(failure.InnerExceptions[0], Is.SameAs(original));
            Assert.That(failure.InnerExceptions.Count, Is.EqualTo(4));
            Assert.That(audio.DisposeCount, Is.EqualTo(1));
            Assert.That(server.DisposeCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
            Assert.That(released, Is.EqualTo(new[] { "client", "server" }));
        }

        private static CharacterTestSliceOptions HeadlessOptions(ProbeAudio audio, bool owned)
        {
            var manager = new ResourceManager();
            return new CharacterTestSliceOptions
            {
                Resources = new GlobalResourceRuntimeServices(GlobalResourceRuntimeMode.Editor, manager, new ResourcePreloadService(manager)),
                AudioService = audio,
                DisposeAudioService = owned,
                EnableTraceFileWrite = false,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                EnableStoryBootstrap = false,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true
            };
        }

        private sealed class ProbeModule : RuntimeModule
        {
            private readonly string _label;
            private readonly List<string> _released;
            internal bool FailInitialize;
            internal bool FailStart;
            internal bool FailStop;
            internal bool FailDispose;
            internal int DisposeCount;
            internal ProbeModule(string id, string label, List<string> released) : base(id)
            { _label = label; _released = released; }
            public override void Initialize(RuntimeHostContext context)
            { if (FailInitialize) throw new InvalidOperationException("initialize " + _label); }
            public override void Start(RuntimeHostContext context)
            { if (FailStart) throw new InvalidOperationException("start " + _label); }
            public override void Stop(RuntimeHostContext context)
            { if (FailStop) throw new InvalidOperationException("stop " + _label); }
            public override void Dispose()
            {
                DisposeCount++;
                _released.Add(_label);
                if (FailDispose) throw new InvalidOperationException("dispose " + _label);
            }
        }

        private sealed class ThrowingLogger : IRuntimeLogger
        {
            private readonly string _message;
            internal ThrowingLogger(string message) { _message = message; }
            public void Log(RuntimeLogLevel level, string category, string message)
            { if (message == _message) throw new InvalidOperationException("log " + message); }
        }

        private sealed class ProbeSubscription : IDisposable
        {
            internal int DisposeCount;
            public void Dispose() { DisposeCount++; throw new InvalidOperationException("subscription release"); }
        }

        private sealed class ProbeTransport : INetworkTransport, IDisposable
        {
            private readonly List<string> _released;
            internal int DisposeCount;
            internal bool FailDispose;
            internal ProbeTransport(string id, List<string> released) { TransportId = id; _released = released; }
            public string TransportId { get; }
            public void Send(NetworkMessageEnvelope message) { }
            public int Poll(string receiverPeerId, RuntimeFrame frame, List<NetworkMessageEnvelope> output) => 0;
            public int PollEvents(string peerId, RuntimeFrame frame, List<NetworkTransportEvent> output) => 0;
            public NetworkTransportSnapshot CreateSnapshot(string peerId) => default;
            public void Clear() { }
            public void Dispose()
            {
                DisposeCount++;
                _released.Add(TransportId);
                if (FailDispose) throw new InvalidOperationException("transport release " + TransportId);
            }
        }

        private sealed class ProbeAudio : IAudioService, IDisposable
        {
            internal bool FailDispose;
            internal bool FailSnapshot;
            internal bool Initialized;
            internal int DisposeCount;
            public AudioPlayResult PlayOneShot(in AudioPlayRequest request) => default;
            public AudioPlayResult StartEvent(in AudioPlayRequest request, out AudioHandle handle) { handle = default; return default; }
            public AudioResult Stop(AudioHandle handle, AudioStopMode stopMode) => default;
            public AudioResult SetParameter(AudioHandle handle, int parameterId, float value) => default;
            public AudioResult SetBusVolume(int busId, float volume) => default;
            public AudioResult SetBusMuted(int busId, bool muted) => default;
            public AudioDebugSnapshot CaptureSnapshot()
            {
                if (FailSnapshot) throw new InvalidOperationException("audio snapshot");
                return new AudioDebugSnapshot(Initialized, 0, 0, 0, null, null, null);
            }
            public void Tick(float deltaTime) { }
            public void Dispose() { DisposeCount++; if (FailDispose) throw new InvalidOperationException("audio release"); }
        }
    }
}
