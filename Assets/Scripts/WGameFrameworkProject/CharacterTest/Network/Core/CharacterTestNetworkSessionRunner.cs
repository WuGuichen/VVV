using System;
using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Combat.Physics;
using MxFramework.Network;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestNetworkSessionRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkReplayRunner,
        ICharacterTestNetworkReplayEvidenceProvider,
        ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        private static readonly Unity.Profiling.ProfilerMarker NetworkTickMarker =
            new Unity.Profiling.ProfilerMarker("CharacterTest.Network.SessionTick");
        private const int MirrorSnapshotRequestMismatchThreshold = 3;
        private const int MirrorSnapshotRequestCooldownFrames = 8;

        private readonly CharacterTestNetworkSnapshotParticipant _serverSnapshotParticipant;
        private readonly CharacterTestNetworkSnapshotParticipant _clientSnapshotParticipant;
        private readonly CharacterTestReplayRecorder _replayRecorder;
        private readonly CharacterTestNetworkEntityControlGrantState _controlGrants;
        private readonly ICharacterTestNetworkPeerControlPolicy _peerControlPolicy;
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(256);
        private readonly bool _disposeTransports;
        private readonly CharacterTestCleanupScope _lifetime;
        private readonly Func<CharacterTestCharacterPhysicsRuntime, GameSlice> _replaySliceFactory;
        private Func<GameSlice> _activeReplaySliceFactory;
        private readonly CharacterTestCharacterPhysicsRuntime _physicsRuntimeTemplate;
        private bool _disposed;
        private long _lastServerReceiveFlowLogFrame = long.MinValue;
        private CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _serverSessionAuthorityParticipant;
        private CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _clientSessionAuthorityParticipant;
        private CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler _sessionAuthorityWireHandler;
        private Action<NetworkApplicationControlResponsePacket> _sessionAuthorityResponseObserver;
        private bool _sessionAuthoritySnapshotDirty;
        private RuntimeFrame _latestReplayEvidenceFrame;
        private long _latestReplayEvidenceHash;
        private bool _hasLatestReplayEvidence;
        private bool _runtimeAiLabHistoricalReviewActive;

        public CharacterTestNetworkSessionRunner(
            INetworkTransport serverTransport,
            INetworkTransport clientTransport,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices serverResources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            string sessionId = "character-test-network",
            string serverPeerId = "server",
            string clientPeerId = "client-0",
            string transportMode = "custom",
            bool createServerViews = true,
            bool disposeTransports = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null)
            : this(
                serverTransport,
                clientTransport,
                true,
                logger,
                storyContent,
                serverResources,
                baseResourcePreloadGroupId,
                initialCameraMode,
                actionConfigOptions,
                physicsRuntime,
                config,
                sessionId,
                serverPeerId,
                clientPeerId,
                transportMode,
                createServerViews,
                disposeTransports,
                audioService,
                audioCueCatalog,
                disposeAudioService,
                runtimeAiPlannerProfile,
                runtimeAiPlannerProfileReport,
                null,
                null,
                null,
                null,
                false,
                0,
                CharacterTestNetworkPeerControlMode.SingleCharacterPlayer,
                null)
        {
        }

        internal CharacterTestNetworkSessionRunner(
            INetworkTransport serverTransport,
            INetworkTransport clientTransport,
            bool useExpandedReplayContract,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices serverResources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            string sessionId = "character-test-network",
            string serverPeerId = "server",
            string clientPeerId = "client-0",
            string transportMode = "custom",
            bool createServerViews = true,
            bool disposeTransports = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> runtimeAiPlannerProfileBindings = null,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtimeAiBehaviorLoadouts = null,
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> movableNaturalObjectSpawnSpecs = null,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupCharacterSpawnSpecs = null,
            bool suppressMissingStartupSpawnWarning = false,
            int presentationAllyTeamId = 0,
            CharacterTestNetworkPeerControlMode peerControlMode =
                CharacterTestNetworkPeerControlMode.SingleCharacterPlayer,
            CharacterTestCharacterActionCatalog actionCatalog = null,
            Action incomingOwnershipAccepted = null)
        {
            _lifetime = new CharacterTestCleanupScope();
            CharacterTestCleanupScope.Entry resourceOwnership = serverResources != null
                ? _lifetime.Own("IncomingResources", serverResources.Dispose) : null;
            CharacterTestCleanupScope.Entry audioOwnership = disposeAudioService && audioService is IDisposable audio
                ? _lifetime.Own("IncomingAudio", audio.Dispose) : null;
            if (disposeTransports)
            {
                if (!ReferenceEquals(serverTransport, clientTransport) && clientTransport != null)
                    _lifetime.Own("ClientTransport", () => ReleaseTransport(clientTransport), first: true);
                if (serverTransport != null)
                    _lifetime.Own("ServerTransport", () => ReleaseTransport(serverTransport), first: true);
            }
            try
            {
                incomingOwnershipAccepted?.Invoke();
                ServerTransport = serverTransport ?? throw new ArgumentNullException(nameof(serverTransport));
                ClientTransport = clientTransport ?? serverTransport;
                _disposeTransports = disposeTransports;
                _controlGrants = new CharacterTestNetworkEntityControlGrantState();
                _peerControlPolicy =
                    CharacterTestNetworkPeerControlPolicyFactory.Create(peerControlMode);

                IRuntimeLogger safeLogger = logger ?? NullRuntimeLogger.Instance;
                _logger = safeLogger;
                Config = CharacterTestSessionHashContract.Normalize(config ?? NetworkSessionConfig.Default);
                SessionId = string.IsNullOrEmpty(sessionId) ? "character-test-network" : sessionId;
                ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
                ClientPeerId = string.IsNullOrEmpty(clientPeerId) ? "client-0" : clientPeerId;
                TransportMode = string.IsNullOrEmpty(transportMode) ? "custom" : transportMode;
                _physicsRuntimeTemplate = physicsRuntime;

                ServerSlice = new GameSlice(new CharacterTestSliceOptions
                {
                    Logger = safeLogger,
                    StoryContent = storyContent,
                    Resources = serverResources,
                    IncomingOwnershipAccepted = () => { resourceOwnership?.Transfer(); audioOwnership?.Transfer(); },
                    BaseResourcePreloadGroupId = baseResourcePreloadGroupId,
                    InitialCameraMode = initialCameraMode,
                    ActionConfigOptions = actionConfigOptions,
                    ActionCatalog = actionCatalog,
                    PhysicsRuntime = physicsRuntime,
                    CreateCharacterViews = createServerViews,
                    AudioService = audioService,
                    AudioCueCatalog = audioCueCatalog,
                    DisposeAudioService = disposeAudioService,
                    RuntimeAiPlannerProfile = runtimeAiPlannerProfile,
                    RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileReport,
                    RuntimeAiPlannerProfileBindings = runtimeAiPlannerProfileBindings,
                    RuntimeAiBehaviorLoadouts = runtimeAiBehaviorLoadouts,
                    MovableNaturalObjectSpawnSpecs = movableNaturalObjectSpawnSpecs,
                    StartupCharacterSpawnSpecs = startupCharacterSpawnSpecs,
                    SuppressMissingStartupSpawnWarning = suppressMissingStartupSpawnWarning,
                    PresentationAllyTeamId = presentationAllyTeamId,
                    SimulationTickRate = Config.TickRate
                });
                _lifetime.Own("ServerSlice", ServerSlice.Dispose);
                _replaySliceFactory = CreateReplaySlice;
                ClientSlice = new GameSlice(new CharacterTestSliceOptions
                {
                    Logger = safeLogger,
                    StoryContent = storyContent,
                    Resources = null,
                    BaseResourcePreloadGroupId = string.Empty,
                    InitialCameraMode = initialCameraMode,
                    ActionConfigOptions = actionConfigOptions,
                    ActionCatalog = actionCatalog,
                    PhysicsRuntime = null,
                    CreateCharacterViews = false,
                    EnableLocalInputCommands = false,
                    StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                    SuppressMissingStartupSpawnWarning = true,
                    SimulationTickRate = Config.TickRate
                });

                GameSlice CreateReplaySlice(CharacterTestCharacterPhysicsRuntime replayPhysicsRuntime)
                {
                    var replaySlice = new GameSlice(new CharacterTestSliceOptions
                    {
                    Logger = safeLogger,
                    StoryContent = storyContent,
                    InitialCameraMode = initialCameraMode,
                    ActionConfigOptions = actionConfigOptions,
                    ActionCatalog = actionCatalog,
                    PhysicsRuntime = replayPhysicsRuntime,
                    CreateCharacterViews = false,
                    EnableLocalInputCommands = false,
                    RuntimeAiPlannerProfile = runtimeAiPlannerProfile,
                    RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileReport,
                    RuntimeAiPlannerProfileBindings = runtimeAiPlannerProfileBindings,
                    RuntimeAiBehaviorLoadouts = runtimeAiBehaviorLoadouts,
                    MovableNaturalObjectSpawnSpecs = movableNaturalObjectSpawnSpecs,
                    StartupCharacterSpawnSpecs = startupCharacterSpawnSpecs,
                    SuppressMissingStartupSpawnWarning = suppressMissingStartupSpawnWarning,
                    PresentationAllyTeamId = presentationAllyTeamId,
                    SimulationTickRate = Config.TickRate
                    });
                    return replaySlice;
                }

                _lifetime.Own("ClientSlice", ClientSlice.Dispose);
                _serverSnapshotParticipant = ServerSlice.Services.Network.CreateSnapshotParticipant(
                    _controlGrants.GetControlledEntityId,
                    _controlGrants.CaptureSnapshot);
                _clientSnapshotParticipant = ClientSlice.Services.Network.CreateSnapshotParticipant();
                _replayRecorder = new CharacterTestReplayRecorder(Config);

                ServerSession = new NetworkSessionRunner(
                    NetworkSessionRole.Server,
                    SessionId,
                    ServerPeerId,
                    ServerPeerId,
                    Config,
                    ServerTransport,
                    ServerSlice.Services.Modules.RuntimeCommands.CommandBuffer,
                    new[] { _serverSnapshotParticipant },
                    ComputeServerStateHash,
                    safeLogger,
                    commandAuthorizer: _controlGrants.AuthorizeCommand,
                    peerResultHashProvider: ComputePeerStateHash,
                    authoritativeCommandObserver: packet => _replayRecorder.RecordAuthoritativeCommands(packet));
                ClientSession = new NetworkSessionRunner(
                    NetworkSessionRole.Client,
                    SessionId,
                    ClientPeerId,
                    ServerPeerId,
                    Config,
                    ClientTransport,
                    ClientSlice.Services.Modules.RuntimeCommands.CommandBuffer,
                    participants: new[] { _clientSnapshotParticipant },
                    resultHashProvider: ComputeClientMirrorStateHash,
                    logger: safeLogger,
                    snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                    snapshotRequestMismatchThreshold: MirrorSnapshotRequestMismatchThreshold,
                    snapshotRequestCooldownFrames: MirrorSnapshotRequestCooldownFrames,
                    applyOwnAuthoritativeCommands: true,
                    authoritativeCommandReceiveHandler:
                        SuppressClientAuthoritativeCommandsDuringRuntimeAiLabReplay);

                ServerSlice.Services.Commands.SetRuntimeCommandSubmitter(SubmitClientCommand);
                ServerSlice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
                ClientSession.Connect(RuntimeFrame.Zero);
                PumpNetwork(RuntimeFrame.Zero);
            }
            catch (Exception failure)
            {
                var errors = new List<Exception>();
                _lifetime.Cleanup(errors);
                if (errors.Count != 0)
                {
                    errors.Insert(0, failure);
                    throw new AggregateException("CharacterTest network construction and rollback failed.", errors);
                }
                throw;
            }
        }

        public GameSlice ServerSlice { get; }
        public GameSlice ClientSlice { get; }
        public GameSlice Slice => ServerSlice;
        public NetworkSessionConfig Config { get; }
        public string SessionId { get; }
        public string ServerPeerId { get; }
        public string ClientPeerId { get; }
        public string TransportMode { get; }
        public INetworkTransport ServerTransport { get; }
        public INetworkTransport ClientTransport { get; }
        public NetworkSessionRunner ServerSession { get; }
        public NetworkSessionRunner ClientSession { get; }
        public bool IsReplayRecording => _replayRecorder.IsRecording;

        internal CharacterTestRuntimeAiPlannerSessionDeckLock ServerSessionDeckLock =>
            _serverSessionAuthorityParticipant?.SessionLock;
        internal CharacterTestRuntimeAiPlannerSessionDeckLock ClientSessionDeckLock =>
            _clientSessionAuthorityParticipant?.SessionLock;
        internal CharacterTestRuntimeAiPlannerSessionAuthorityParticipant ServerSessionAuthorityParticipant =>
            _serverSessionAuthorityParticipant;
        internal CharacterTestRuntimeAiPlannerSessionAuthorityParticipant ClientSessionAuthorityParticipant =>
            _clientSessionAuthorityParticipant;

        internal void SetRuntimeAiLabHistoricalReviewActive(bool active)
        {
            _runtimeAiLabHistoricalReviewActive = active;
        }

        internal void ConfigureRuntimeAiPlannerSessionAuthority(
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant serverParticipant,
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant clientParticipant,
            CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler wireHandler,
            Action<NetworkApplicationControlResponsePacket> responseObserver = null)
        {
            ThrowIfDisposed();
            if (_serverSessionAuthorityParticipant != null || _clientSessionAuthorityParticipant != null)
                throw new InvalidOperationException("Runtime AI Planner session authority is already configured.");
            _serverSessionAuthorityParticipant = serverParticipant
                ?? throw new ArgumentNullException(nameof(serverParticipant));
            _clientSessionAuthorityParticipant = clientParticipant
                ?? throw new ArgumentNullException(nameof(clientParticipant));
            _sessionAuthorityWireHandler = wireHandler
                ?? throw new ArgumentNullException(nameof(wireHandler));
            _sessionAuthorityResponseObserver = responseObserver;
            ServerSession.RegisterParticipant(_serverSessionAuthorityParticipant);
            ClientSession.RegisterParticipant(_clientSessionAuthorityParticipant);
            ServerSession.SetApplicationControlHandler(HandleSessionAuthorityRequest);
            ClientSession.SetApplicationControlResponseObserver(ObserveSessionAuthorityResponse);
            _sessionAuthoritySnapshotDirty = true;
            PumpNetwork(ServerSlice.CurrentFrame);
        }

        internal void SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            ThrowIfDisposed();
            ServerSlice.ThrowIfAuthorityUnavailable();
            ClientSlice.ThrowIfAuthorityUnavailable();
            if (_clientSessionAuthorityParticipant == null)
                throw new InvalidOperationException("Runtime AI Planner session authority is not configured.");
            ClientSession.SubmitApplicationControl(
                ClientSlice.CurrentFrame,
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.CreateRequestPacket(requestId, request));
        }

        void ICharacterTestRuntimeAiPlannerSessionAuthorityClient.SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            SubmitRuntimeAiPlannerSessionDeckRequest(requestId, request);
        }

        public RuntimeCommandValidationResult SubmitClientCommand(RuntimeCommand command)
        {
            ThrowIfDisposed();
            ServerSlice.ThrowIfAuthorityUnavailable();
            ClientSlice.ThrowIfAuthorityUnavailable();
            if (ClientSession.Status != NetworkSessionStatus.Connected)
            {
                LogClientSubmit("loopback-client-wait", command, "not connected");
                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    ServerSlice.CurrentFrame,
                    "CharacterTest network client is not connected."));
            }

            if (CharacterTestNetworkEntityControlGrantState.RequiresEntityControl(command)
                && _controlGrants.GetControlledEntityId(ClientPeerId) <= 0)
            {
                if (_peerControlPolicy != null)
                {
                    LogClientSubmit(
                        "loopback-client-wait",
                        command,
                        "waiting for entity-control grant");
                    return RuntimeCommandValidationResult.Accepted(command);
                }

                return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    ServerSlice.CurrentFrame,
                    "CharacterTest network peer has no entity-control grant."));
            }

            RuntimeCommand outbound = command;
            if (_controlGrants.TryRewriteEntityControlCommand(
                    command,
                    ClientPeerId,
                    out RuntimeCommand rewritten))
            {
                outbound = rewritten;
            }

            LogClientSubmit("loopback-client-submit", outbound, "connected");
            ClientSession.SubmitCommands(outbound.Frame, new[] { outbound });
            LogClientSubmit("loopback-client-send", outbound, "sent to " + ServerPeerId);
            return RuntimeCommandValidationResult.Accepted(outbound);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            using var tickScope = NetworkTickMarker.Auto();
            ThrowIfDisposed();
            RuntimeFrame frame = ServerSlice.CurrentFrame;
            ServerSlice.ThrowIfAuthorityUnavailable();
            ClientSlice.ThrowIfAuthorityUnavailable();
            try
            {
                if (frame.Value % Config.HeartbeatIntervalFrames == 0L)
                    ClientSession.SendPing(frame);
                PumpNetwork(frame);
                ServerSlice.Tick(ResolveSimulationDeltaTime());
                SyncEntityControlGrants();

                RuntimeFrame postTickFrame = ServerSlice.CurrentFrame;
                RuntimeFrame authoritativeFrame = postTickFrame.Value > 0L
                    ? new RuntimeFrame(postTickFrame.Value - 1L)
                    : RuntimeFrame.Zero;
                if (ShouldBroadcastSnapshot(authoritativeFrame))
                    ServerSession.BroadcastSnapshot(authoritativeFrame, fullSnapshot: false);
                PumpNetwork(postTickFrame);
                if (ClientSession.Status != NetworkSessionStatus.Resynchronizing
                    && ClientSession.Status != NetworkSessionStatus.RecoveryFailed)
                    ClientSlice.Tick(ResolveSimulationDeltaTime());
                RecordReplayFrame(authoritativeFrame);
            }
            catch (Exception failure)
            {
                ServerSlice.SuspendAuthority(frame, failure);
                ClientSlice.SuspendAuthority(frame, failure);
                throw;
            }
        }

        private int _replayFrameCapacity;
        private int _replayCommandCapacity;
        internal void PrepareReplayCaptureCapacity(int frameCapacity, int commandCapacity)
        {
            _replayFrameCapacity = frameCapacity;
            _replayCommandCapacity = commandCapacity;
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            ServerSlice.ThrowIfAuthorityUnavailable();
            ClientSlice.ThrowIfAuthorityUnavailable();
            RuntimeFrame startFrame = ServerSlice.CurrentFrame;
            RuntimeCustomState bootstrap = _serverSnapshotParticipant.CaptureSnapshot(startFrame);
            RuntimeCustomState sessionAuthorityBootstrap =
                _serverSessionAuthorityParticipant?.CaptureSnapshot(startFrame);
            CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[] replayOverlays =
                ServerSlice.Services.Modules.RuntimeAiPlannerCommands
                    ?.CaptureReplayOverlayInstallRequests()
                ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>();
            CharacterTestCharacterPhysicsRuntime replayPhysicsSnapshot =
                CloneReplayPhysicsRuntime(_physicsRuntimeTemplate);
            _activeReplaySliceFactory = () => CreateReplaySliceWithOverlays(
                replayOverlays,
                replayPhysicsSnapshot);
            CharacterTestReplayCapture capture = _replayRecorder.Begin(
                startFrame,
                bootstrap,
                sessionAuthorityBootstrap,
                _serverSessionAuthorityParticipant?.AuthorityHash ?? 0L,
                _serverSessionAuthorityParticipant == null
                    ? null
                    : new Func<INetworkSnapshotParticipant>(
                        _serverSessionAuthorityParticipant.CreateReplayReplica),
                _serverSessionAuthorityParticipant?.CaptureReplayIdentity());
            if (_replayFrameCapacity > 0)
                _replayRecorder.PrepareCaptureCapacity(_replayFrameCapacity, _replayCommandCapacity,
                    ServerSlice.Services.Modules.CharacterEntities.Count);
            return CharacterTestReplayPlaybackStatus.RecordingStarted(capture);
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _replayRecorder.CreateCapture(
                _activeReplaySliceFactory ?? CreateReplaySliceFromLiveTemplate);
        }

        private GameSlice CreateReplaySliceWithOverlays(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest> replayOverlays,
            CharacterTestCharacterPhysicsRuntime replayPhysicsSnapshot)
        {
            GameSlice replaySlice = _replaySliceFactory(
                CloneReplayPhysicsRuntime(replayPhysicsSnapshot));
            if (replayOverlays == null || replayOverlays.Count == 0)
                return replaySlice;
            CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result = default;
            if (replaySlice.Services.Modules.RuntimeAiPlannerCommands != null
                && replaySlice.Services.Modules.RuntimeAiPlannerCommands
                    .TryApplyBehaviorCompositionOverlayBatch(replayOverlays, out result))
            {
                return replaySlice;
            }
            replaySlice.Dispose();
            throw new InvalidOperationException(
                result.ReportText ?? "Replay behavior overlay bootstrap failed.");
        }

        bool ICharacterTestNetworkReplayEvidenceProvider.TryGetLatestReplayEvidence(
            out RuntimeFrame frame,
            out long resultHash)
        {
            frame = _latestReplayEvidenceFrame;
            resultHash = _latestReplayEvidenceHash;
            return _hasLatestReplayEvidence;
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            NetworkTransportSnapshot clientTransport = ClientTransport.CreateSnapshot(ClientPeerId);
            bool sharedTransport = ReferenceEquals(ServerTransport, ClientTransport);
            NetworkTransportSnapshot serverTransport = sharedTransport
                ? clientTransport
                : ServerTransport.CreateSnapshot(ServerPeerId);
            int pendingMessages = sharedTransport
                ? clientTransport.PendingMessageCount
                : clientTransport.PendingMessageCount + serverTransport.PendingMessageCount;
            long codecErrors = sharedTransport
                ? clientTransport.CodecErrorCount
                : clientTransport.CodecErrorCount + serverTransport.CodecErrorCount;
            long sentBytes = sharedTransport
                ? clientTransport.SentBytes
                : clientTransport.SentBytes + serverTransport.SentBytes;
            long deliveredBytes = sharedTransport
                ? clientTransport.DeliveredBytes
                : clientTransport.DeliveredBytes + serverTransport.DeliveredBytes;
            int lastPayloadBytes = Math.Max(clientTransport.LastSentPayloadBytes, serverTransport.LastSentPayloadBytes);
            int maxPayloadBytes = Math.Max(clientTransport.MaxSentPayloadBytes, serverTransport.MaxSentPayloadBytes);
            long runtimeCommandBytes = sharedTransport
                ? clientTransport.RuntimeCommandSentBytes
                : clientTransport.RuntimeCommandSentBytes + serverTransport.RuntimeCommandSentBytes;
            long snapshotBytes = sharedTransport
                ? clientTransport.SnapshotSentBytes
                : clientTransport.SnapshotSentBytes + serverTransport.SnapshotSentBytes;
            long snapshotRequestBytes = sharedTransport
                ? clientTransport.SnapshotRequestSentBytes
                : clientTransport.SnapshotRequestSentBytes + serverTransport.SnapshotRequestSentBytes;
            long heartbeatBytes = sharedTransport
                ? clientTransport.HeartbeatSentBytes
                : clientTransport.HeartbeatSentBytes + serverTransport.HeartbeatSentBytes;
            return new CharacterTestNetworkLoopbackDiagnostics(
                available: true,
                serverStatus: ServerSession.Status,
                clientStatus: ClientSession.Status,
                serverFrame: ServerSlice.CurrentFrame,
                clientLastServerFrame: ClientSession.LastServerFrame,
                lastAckedInputFrame: ServerSession.LastAckedInputFrame,
                rttEstimateFrames: ClientSession.RttEstimateFrames,
                pendingRuntimeCommands: ServerSlice.Services.Modules.PendingRuntimeCommandCount,
                pendingTransportMessages: pendingMessages,
                lastSnapshotHash: ClientSession.LastSnapshotHash,
                resyncCount: ClientSession.ResyncCount,
                snapshotApplyFailureCount: ClientSession.SnapshotApplyFailureCount,
                snapshotMismatchCount: ClientSession.SnapshotMismatchCount,
                suppressedSnapshotMismatchCount: ClientSession.SuppressedSnapshotMismatchCount,
                duplicateMessageCount: ClientSession.DuplicateMessageCount + ServerSession.DuplicateMessageCount,
                outOfOrderMessageCount: ClientSession.OutOfOrderMessageCount + ServerSession.OutOfOrderMessageCount,
                codecErrorCount: codecErrors,
                disconnectReason: ClientSession.DisconnectReason,
                transportMode: TransportMode,
                transportId: ClientTransport.TransportId,
                localPeerId: ClientPeerId,
                ownedEntityId: _clientSnapshotParticipant.LastAppliedLocalControlledEntityId,
                connectedPeerCount: ServerSession.ConnectedPeerCount,
                ownershipSummary: _controlGrants.CreateSummary(_peerControlPolicy),
                averageRttFrames: ClientSession.AverageRttFrames,
                maxRttFrames: ClientSession.MaxRttFrames,
                averageSnapshotIntervalFrames: ClientSession.AverageSnapshotIntervalFrames,
                maxSnapshotIntervalFrames: ClientSession.MaxSnapshotIntervalFrames,
                frameDriftFrames: ClientSession.LastFrameDriftFrames,
                averageFrameDriftFrames: ClientSession.AverageFrameDriftFrames,
                maxFrameDriftFrames: ClientSession.MaxFrameDriftFrames,
                comparableSnapshotHashCount: ClientSession.ComparableSnapshotHashCount,
                missingSnapshotHashCount: ClientSession.MissingSnapshotHashCount,
                snapshotRequestCount: ClientSession.SnapshotRequestCount + ServerSession.SnapshotRequestCount,
                authoritativeCommandSentCount: ServerSession.AuthoritativeCommandSentCount,
                authoritativeCommandReceivedCount: ClientSession.AuthoritativeCommandReceivedCount,
                authoritativeCommandEchoCount: ClientSession.AuthoritativeCommandEchoCount,
                authoritativeCommandEnqueuedCount: ClientSession.AuthoritativeCommandEnqueuedCount,
                authoritativeCommandLateCount: ClientSession.AuthoritativeCommandLateCount,
                authoritativeCommandDroppedCount: ClientSession.AuthoritativeCommandDroppedCount,
                fullSnapshotSentCount: ServerSession.FullSnapshotSentCount,
                digestSnapshotSentCount: ServerSession.DigestSnapshotSentCount,
                fullSnapshotReceivedCount: ClientSession.FullSnapshotReceivedCount,
                digestSnapshotReceivedCount: ClientSession.DigestSnapshotReceivedCount,
                sentBytes: sentBytes,
                deliveredBytes: deliveredBytes,
                lastSentPayloadBytes: lastPayloadBytes,
                maxSentPayloadBytes: maxPayloadBytes,
                runtimeCommandSentBytes: runtimeCommandBytes,
                snapshotSentBytes: snapshotBytes,
                snapshotRequestSentBytes: snapshotRequestBytes,
                heartbeatSentBytes: heartbeatBytes,
                roomSentBytes: clientTransport.RoomSentBytes + serverTransport.RoomSentBytes,
                recentDiagnostics: string.Join(" | ", ClientSession.RecentDiagnostics),
                stateSyncPolicy: Config.StateSyncPolicy,
                heartbeatIntervalFrames: Config.HeartbeatIntervalFrames);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            var errors = new List<Exception>();
            CharacterTestCleanupScope.Attempt(errors, "CommandSubmitter", () => ServerSlice?.Services.Commands.SetRuntimeCommandSubmitter(null));
            CharacterTestCleanupScope.Attempt(errors, "NetworkDiagnostics", () => ServerSlice?.Services.Network.SetNetworkDiagnosticsProvider(null));
            _lifetime.Cleanup(errors);
            CharacterTestCleanupScope.ThrowIfFailed(errors, "CharacterTest network cleanup failed.");
        }

        private void PumpNetwork(RuntimeFrame frame)
        {
            RuntimeFrame beforeAckedInputFrame = ServerSession.LastAckedInputFrame;
            int beforePendingCommands = ServerSlice.Services.Modules.PendingRuntimeCommandCount;
            ServerSession.ProcessIncoming(frame);
            if (_sessionAuthoritySnapshotDirty)
            {
                ServerSession.BroadcastSnapshot(frame, fullSnapshot: true);
                _sessionAuthoritySnapshotDirty = false;
            }
            SyncEntityControlGrants();
            LogServerReceiveIfNeeded(frame, beforeAckedInputFrame, beforePendingCommands);
            ClientSession.ProcessIncoming(frame);
        }

        private NetworkApplicationControlResponsePacket HandleSessionAuthorityRequest(
            string senderPeerId,
            NetworkApplicationControlRequestPacket request)
        {
            if (!CharacterTestRuntimeAiPlannerSessionAuthorityWire.IsRequest(request))
            {
                return new NetworkApplicationControlResponsePacket(
                    request.TypeId,
                    request.SchemaVersion,
                    request.RequestId,
                    accepted: false,
                    reason: "unsupported CharacterTest application control request");
            }
            NetworkApplicationControlResponsePacket response =
                _sessionAuthorityWireHandler.Handle(senderPeerId, request);
            if (response.Accepted)
                _sessionAuthoritySnapshotDirty = true;
            return response;
        }

        private void ObserveSessionAuthorityResponse(
            NetworkApplicationControlResponsePacket response)
        {
            _sessionAuthorityResponseObserver?.Invoke(response);
        }

        private void SyncEntityControlGrants()
        {
            _controlGrants.SyncConnectedPeers(
                ServerSession,
                ServerSlice.Services.Modules.CharacterRuntime,
                ServerSlice.Services.Modules.RuntimeCommands.CommandBuffer,
                _peerControlPolicy);
        }

        private bool SuppressClientAuthoritativeCommandsDuringRuntimeAiLabReplay(
            NetworkAuthoritativeCommandPacket packet,
            RuntimeFrame frame)
        {
            // Runtime AI Lab rewinds only the authoritative server slice. The client
            // mirror deliberately remains on its Live frame and therefore must not
            // classify historical replay echoes as real late-network incidents.
            return _runtimeAiLabHistoricalReviewActive
                || ServerSlice.Services.Network.IsNetworkRollbackReplayActive;
        }

        private void RecordReplayFrame(RuntimeFrame authoritativeFrame)
        {
            if (!_replayRecorder.IsRecording
                || ServerSlice.Services.Network.IsNetworkRollbackReplayActive)
                return;

            long hash = ComputeServerStateHash(authoritativeFrame);
            _latestReplayEvidenceFrame = authoritativeFrame;
            _latestReplayEvidenceHash = hash;
            _hasLatestReplayEvidence = true;
            _replayRecorder.RecordFrame(
                authoritativeFrame,
                ServerSlice.Services.Modules.RuntimeCommands.LastDrainedCommands,
                hash,
                ServerSlice.Services.Modules.CharacterEntities.Count);
        }

        private long ComputePeerStateHash(RuntimeFrame frame, string peerId)
        {
            int entityId = _controlGrants.GetControlledEntityId(peerId);
            long gameplayHash = entityId > 0
                ? _serverSnapshotParticipant.ComputeNonOwnedStateHash(frame, entityId)
                : _serverSnapshotParticipant.ComputeStateHash(frame);
            return CombineSessionAuthorityHash(gameplayHash, _serverSessionAuthorityParticipant);
        }

        private long ComputeClientMirrorStateHash(RuntimeFrame frame)
        {
            int entityId = _clientSnapshotParticipant.LastAppliedLocalControlledEntityId;
            long gameplayHash = entityId > 0
                ? _clientSnapshotParticipant.ComputeNonOwnedStateHash(frame, entityId)
                : _clientSnapshotParticipant.ComputeStateHash(frame);
            return CombineSessionAuthorityHash(gameplayHash, _clientSessionAuthorityParticipant);
        }

        private long ComputeServerStateHash(RuntimeFrame frame)
        {
            return CombineSessionAuthorityHash(
                _serverSnapshotParticipant.ComputeStateHash(frame),
                _serverSessionAuthorityParticipant);
        }

        private static long CombineSessionAuthorityHash(
            long gameplayHash,
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant)
        {
            return participant == null
                ? gameplayHash
                : CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(
                    gameplayHash,
                    participant.AuthorityHash);
        }

        private bool ShouldBroadcastSnapshot(RuntimeFrame authoritativeFrame)
        {
            return ServerSession.HasPendingFullSnapshotReceivers
                || (Config.StateSyncPolicy != NetworkStateSyncPolicy.BootstrapOnly
                    && authoritativeFrame.Value % Config.SnapshotIntervalFrames == 0L);
        }

        private double ResolveSimulationDeltaTime()
        {
            return 1d / System.Math.Max(1, Config.TickRate);
        }

        private GameSlice CreateReplaySliceFromLiveTemplate()
        {
            return _replaySliceFactory(CloneReplayPhysicsRuntime(_physicsRuntimeTemplate));
        }

        internal static CharacterTestCharacterPhysicsRuntime CloneReplayPhysicsRuntime(
            CharacterTestCharacterPhysicsRuntime source)
        {
            if (source == null || !source.IsReady || source.World == null)
                return source;

            var world = new CombatPhysicsWorld();
            var bodies = new List<CombatPhysicsBody>();
            source.World.CopyBodiesTo(bodies);
            for (int i = 0; i < bodies.Count; i++)
            {
                CombatPhysicsBody body = bodies[i];
                if (body.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    world.UpsertBody(body);
            }

            var colliders = new List<CombatPhysicsAabbCollider>();
            source.World.CopyAabbCollidersTo(colliders);
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = colliders[i];
                if (collider.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    world.UpsertAabbCollider(collider);
            }

            var spheres = new List<CombatPhysicsSphereCollider>();
            source.World.CopySphereCollidersTo(spheres);
            for (int i = 0; i < spheres.Count; i++)
            {
                CombatPhysicsSphereCollider collider = spheres[i];
                if (collider.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    world.UpsertSphereCollider(collider);
            }

            var capsules = new List<CombatPhysicsCapsuleCollider>();
            source.World.CopyCapsuleCollidersTo(capsules);
            for (int i = 0; i < capsules.Count; i++)
            {
                CombatPhysicsCapsuleCollider collider = capsules[i];
                if (collider.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    world.UpsertCapsuleCollider(collider);
            }

            var obbs = new List<CombatPhysicsObbCollider>();
            source.World.CopyObbCollidersTo(obbs);
            for (int i = 0; i < obbs.Count; i++)
            {
                CombatPhysicsObbCollider collider = obbs[i];
                if (collider.BodyId.Value < CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    world.UpsertObbCollider(collider);
            }

            var clone = new CharacterTestCharacterPhysicsRuntime(
                world,
                source.ObstacleLayerMask,
                source.CharacterHalfExtents,
                source.SkinWidth,
                source.MaxStepHeight,
                world.TotalColliderCount,
                source.SourceDescription,
                source.GroundSnapDistance);
            clone.SetNavigationBuildInput(
                source.NavigationBuildInput,
                source.NavigationInputResult);
            return clone;
        }

        private void DisposeTransports()
        {
            if (!ReferenceEquals(ServerTransport, ClientTransport))
                DisposeTransport(ClientTransport);
            DisposeTransport(ServerTransport);
        }

        private static void DisposeTransport(INetworkTransport transport)
        {
            if (transport is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }

            transport?.Clear();
        }

        private static void ReleaseTransport(INetworkTransport transport)
        {
            if (transport is IDisposable disposable) disposable.Dispose();
            else transport.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestNetworkSessionRunner));
        }

        private void LogClientSubmit(string phase, RuntimeCommand command, string reason)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (!CharacterTestInputFlowLog.HasActiveCommand(command))
                return;

            _inputFlowBuffer.Clear()
                .Append(phase).Append(' ');
            CharacterTestInputFlowLog.AppendCommand(_inputFlowBuffer, command);
            _inputFlowBuffer.Append(" status=").Append(ClientSession.Status.ToString())
                .Append(" transport=").Append(TransportMode);
            if (!string.IsNullOrWhiteSpace(reason))
                _inputFlowBuffer.Append(" reason=").Append(reason);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }

        private void LogServerReceiveIfNeeded(
            RuntimeFrame frame,
            RuntimeFrame beforeAckedInputFrame,
            int beforePendingCommands)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (ServerSession.LastAckedInputFrame.Value == beforeAckedInputFrame.Value
                && ServerSlice.Services.Modules.PendingRuntimeCommandCount == beforePendingCommands)
            {
                return;
            }

            if (_lastServerReceiveFlowLogFrame != long.MinValue
                && frame.Value - _lastServerReceiveFlowLogFrame < CharacterTestInputFlowLog.ActiveRepeatFrames)
            {
                return;
            }

            _lastServerReceiveFlowLogFrame = frame.Value;

            _inputFlowBuffer.Clear()
                .Append("loopback-server-receive frame=").Append(frame.Value)
                .Append(" ackedInput=").Append(ServerSession.LastAckedInputFrame.Value)
                .Append(" pendingCommands=").Append(ServerSlice.Services.Modules.PendingRuntimeCommandCount)
                .Append(" connectedPeers=").Append(ServerSession.ConnectedPeerCount)
                .Append(" transport=").Append(TransportMode);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }
    }
}
