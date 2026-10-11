using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.CharacterAction;
using MxFramework.Audio;
using MxFramework.Combat.Physics;
using MxFramework.Network;
using MxFramework.Network.UnityTransport;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestUnityTransportSessionIds
    {
        public const string DirectLan = "character-test-utp-direct";
    }

    public sealed class CharacterTestUnityTransportServerRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkReplayRunner
    {
        private readonly CharacterTestNetworkSnapshotParticipant _snapshotParticipant;
        private readonly CharacterTestReplayRecorder _replayRecorder;
        private readonly CharacterTestNetworkEntityControlGrantState _controlGrants =
            new CharacterTestNetworkEntityControlGrantState();
        private readonly ICharacterTestNetworkPeerControlPolicy _peerControlPolicy =
            new CharacterTestSingleCharacterPeerControlPolicy();
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(256);
        private readonly CharacterTestStoryContent _storyContent;
        private readonly CharacterTestCameraMode _initialCameraMode;
        private readonly CharacterTestCharacterActionConfigSourceOptions _actionConfigOptions;
        private readonly CharacterTestCharacterPhysicsRuntime _physicsRuntimeTemplate;
        private readonly bool _disposeTransport;
        private bool _disposed;
        private long _lastSnapshotHash;
        private long _lastServerReceiveFlowLogFrame = long.MinValue;
        private CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _sessionAuthorityParticipant;
        private CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler _sessionAuthorityWireHandler;
        private bool _sessionAuthoritySnapshotDirty;
        private bool _hasPendingHostReplayFrame;
        private RuntimeFrame _pendingHostReplayFrame;

        public CharacterTestUnityTransportServerRunner(
            INetworkTransport transport,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices resources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            string sessionId = CharacterTestUnityTransportSessionIds.DirectLan,
            string serverPeerId = "server",
            string transportMode = "unity-transport-server",
            bool createServerViews = true,
            bool enableLocalInputCommands = false,
            bool disposeTransport = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _disposeTransport = disposeTransport;

            IRuntimeLogger safeLogger = logger ?? NullRuntimeLogger.Instance;
            _logger = safeLogger;
            _storyContent = storyContent;
            _initialCameraMode = initialCameraMode;
            _actionConfigOptions = actionConfigOptions;
            _physicsRuntimeTemplate = physicsRuntime;
            Config = CharacterTestSessionHashContract.Normalize(config ?? NetworkSessionConfig.Default);
            SessionId = string.IsNullOrEmpty(sessionId) ? CharacterTestUnityTransportSessionIds.DirectLan : sessionId;
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            TransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-server" : transportMode;

            Slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = safeLogger,
                StoryContent = storyContent,
                Resources = resources,
                BaseResourcePreloadGroupId = baseResourcePreloadGroupId,
                InitialCameraMode = initialCameraMode,
                ActionConfigOptions = actionConfigOptions,
                ActionCatalog = actionCatalog,
                PhysicsRuntime = physicsRuntime,
                CreateCharacterViews = createServerViews,
                EnableLocalInputCommands = enableLocalInputCommands,
                AudioService = audioService,
                AudioCueCatalog = audioCueCatalog,
                DisposeAudioService = disposeAudioService,
                RuntimeAiPlannerProfile = runtimeAiPlannerProfile,
                RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileReport,
                SimulationTickRate = Config.TickRate
            });

            _snapshotParticipant = Slice.Services.Network.CreateSnapshotParticipant(
                _controlGrants.GetControlledEntityId,
                _controlGrants.CaptureSnapshot);
            _replayRecorder = new CharacterTestReplayRecorder(Config);
            Session = new NetworkSessionRunner(
                NetworkSessionRole.Server,
                SessionId,
                ServerPeerId,
                ServerPeerId,
                Config,
                Transport,
                Slice.Services.Modules.RuntimeCommands.CommandBuffer,
                new[] { _snapshotParticipant },
                ComputeServerStateHash,
                safeLogger,
                commandAuthorizer: _controlGrants.AuthorizeCommand,
                peerResultHashProvider: ComputePeerStateHash,
                authoritativeCommandObserver: packet => _replayRecorder.RecordAuthoritativeCommands(packet));
            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
        }

        public GameSlice Slice { get; }
        public NetworkSessionConfig Config { get; }
        public string SessionId { get; }
        public string ServerPeerId { get; }
        public string TransportMode { get; }
        public INetworkTransport Transport { get; }
        public NetworkSessionRunner Session { get; }
        public bool IsReplayRecording => _replayRecorder.IsRecording;

        internal CharacterTestRuntimeAiPlannerSessionAuthorityParticipant SessionAuthorityParticipant =>
            _sessionAuthorityParticipant;

        internal void ConfigureRuntimeAiPlannerSessionAuthority(
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant,
            CharacterTestRuntimeAiPlannerSessionAuthorityWireHandler wireHandler)
        {
            ThrowIfDisposed();
            if (_sessionAuthorityParticipant != null)
                throw new InvalidOperationException("Runtime AI Planner session authority is already configured.");
            _sessionAuthorityParticipant = participant ?? throw new ArgumentNullException(nameof(participant));
            _sessionAuthorityWireHandler = wireHandler ?? throw new ArgumentNullException(nameof(wireHandler));
            Session.RegisterParticipant(_sessionAuthorityParticipant);
            Session.SetApplicationControlHandler(HandleSessionAuthorityRequest);
            _sessionAuthoritySnapshotDirty = true;
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            TickCore(deltaTime, deferReplayPublication: false);
        }

        internal void TickForHost(double deltaTime)
        {
            TickCore(deltaTime, deferReplayPublication: true);
        }

        internal void PublishHostReplayFrame()
        {
            Slice.ThrowIfAuthorityUnavailable();
            if (!_hasPendingHostReplayFrame) return;
            RecordReplayFrame(_pendingHostReplayFrame);
            _hasPendingHostReplayFrame = false;
        }

        private void TickCore(double deltaTime, bool deferReplayPublication)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            RuntimeFrame attemptedFrame = Slice.CurrentFrame;
            try
            {

                RuntimeFrame frame = Slice.CurrentFrame;
                if (frame.Value % Config.HeartbeatIntervalFrames == 0L)
                    Session.SendPing(frame);

                RuntimeFrame beforeAckedInputFrame = Session.LastAckedInputFrame;
                int beforePendingCommands = Slice.Services.Modules.PendingRuntimeCommandCount;
                Session.ProcessIncoming(frame);
                SyncEntityControlGrants();
                LogServerReceiveIfNeeded(frame, beforeAckedInputFrame, beforePendingCommands);
                Slice.Tick(ResolveSimulationDeltaTime());
                SyncEntityControlGrants();

                RuntimeFrame postTickFrame = Slice.CurrentFrame;
                RuntimeFrame authoritativeFrame = postTickFrame.Value > 0L
                    ? new RuntimeFrame(postTickFrame.Value - 1L)
                    : RuntimeFrame.Zero;
                if (_sessionAuthoritySnapshotDirty || ShouldBroadcastSnapshot(authoritativeFrame))
                {
                    bool fullSnapshot = _sessionAuthoritySnapshotDirty;
                    _lastSnapshotHash = ComputeServerStateHash(authoritativeFrame);
                    Session.BroadcastSnapshot(authoritativeFrame, fullSnapshot);
                    _sessionAuthoritySnapshotDirty = false;
                }

                Session.ProcessIncoming(postTickFrame);
                SyncEntityControlGrants();
                if (deferReplayPublication)
                {
                    _pendingHostReplayFrame = authoritativeFrame;
                    _hasPendingHostReplayFrame = true;
                }
                else RecordReplayFrame(authoritativeFrame);
            }
            catch (Exception failure)
            {
                Slice.SuspendAuthority(attemptedFrame, failure);
                throw;
            }
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            RuntimeFrame startFrame = Slice.CurrentFrame;
            RuntimeCustomState bootstrap = _snapshotParticipant.CaptureSnapshot(startFrame);
            RuntimeCustomState sessionAuthorityBootstrap =
                _sessionAuthorityParticipant?.CaptureSnapshot(startFrame);
            CharacterTestReplayCapture capture = _replayRecorder.Begin(
                startFrame,
                bootstrap,
                sessionAuthorityBootstrap,
                _sessionAuthorityParticipant?.AuthorityHash ?? 0L,
                _sessionAuthorityParticipant == null
                    ? null
                    : new Func<INetworkSnapshotParticipant>(
                        _sessionAuthorityParticipant.CreateReplayReplica),
                _sessionAuthorityParticipant?.CaptureReplayIdentity());
            return CharacterTestReplayPlaybackStatus.RecordingStarted(capture);
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _replayRecorder.CreateCapture(CreateReplaySlice);
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();

            NetworkTransportSnapshot transport = Transport.CreateSnapshot(ServerPeerId);
            return new CharacterTestNetworkLoopbackDiagnostics(
                available: true,
                serverStatus: Session.Status,
                clientStatus: Session.ConnectedPeerCount > 0
                    ? NetworkSessionStatus.Connected
                    : NetworkSessionStatus.Disconnected,
                serverFrame: Slice.CurrentFrame,
                clientLastServerFrame: RuntimeFrame.Zero,
                lastAckedInputFrame: Session.LastAckedInputFrame,
                rttEstimateFrames: Session.RttEstimateFrames,
                pendingRuntimeCommands: Slice.Services.Modules.PendingRuntimeCommandCount,
                pendingTransportMessages: transport.PendingMessageCount,
                lastSnapshotHash: _lastSnapshotHash,
                resyncCount: 0,
                snapshotApplyFailureCount: Session.SnapshotApplyFailureCount,
                snapshotMismatchCount: Session.SnapshotMismatchCount,
                suppressedSnapshotMismatchCount: Session.SuppressedSnapshotMismatchCount,
                duplicateMessageCount: Session.DuplicateMessageCount,
                outOfOrderMessageCount: Session.OutOfOrderMessageCount,
                codecErrorCount: transport.CodecErrorCount,
                disconnectReason: Session.DisconnectReason,
                transportMode: TransportMode,
                transportId: Transport.TransportId,
                localPeerId: ServerPeerId,
                ownedEntityId: 0,
                connectedPeerCount: Session.ConnectedPeerCount,
                ownershipSummary: _controlGrants.CreateSummary(_peerControlPolicy),
                averageRttFrames: Session.AverageRttFrames,
                maxRttFrames: Session.MaxRttFrames,
                averageSnapshotIntervalFrames: Session.AverageSnapshotIntervalFrames,
                maxSnapshotIntervalFrames: Session.MaxSnapshotIntervalFrames,
                frameDriftFrames: Session.LastFrameDriftFrames,
                averageFrameDriftFrames: Session.AverageFrameDriftFrames,
                maxFrameDriftFrames: Session.MaxFrameDriftFrames,
                comparableSnapshotHashCount: Session.ComparableSnapshotHashCount,
                missingSnapshotHashCount: Session.MissingSnapshotHashCount,
                snapshotRequestCount: Session.SnapshotRequestCount,
                authoritativeCommandSentCount: Session.AuthoritativeCommandSentCount,
                authoritativeCommandReceivedCount: Session.AuthoritativeCommandReceivedCount,
                authoritativeCommandEchoCount: Session.AuthoritativeCommandEchoCount,
                authoritativeCommandEnqueuedCount: Session.AuthoritativeCommandEnqueuedCount,
                authoritativeCommandLateCount: Session.AuthoritativeCommandLateCount,
                authoritativeCommandDroppedCount: Session.AuthoritativeCommandDroppedCount,
                fullSnapshotSentCount: Session.FullSnapshotSentCount,
                digestSnapshotSentCount: Session.DigestSnapshotSentCount,
                fullSnapshotReceivedCount: Session.FullSnapshotReceivedCount,
                digestSnapshotReceivedCount: Session.DigestSnapshotReceivedCount,
                sentBytes: transport.SentBytes,
                deliveredBytes: transport.DeliveredBytes,
                lastSentPayloadBytes: transport.LastSentPayloadBytes,
                maxSentPayloadBytes: transport.MaxSentPayloadBytes,
                runtimeCommandSentBytes: transport.RuntimeCommandSentBytes,
                snapshotSentBytes: transport.SnapshotSentBytes,
                snapshotRequestSentBytes: transport.SnapshotRequestSentBytes,
                heartbeatSentBytes: transport.HeartbeatSentBytes,
                roomSentBytes: transport.RoomSentBytes,
                recentDiagnostics: string.Join(" | ", Session.RecentDiagnostics),
                stateSyncPolicy: Config.StateSyncPolicy,
                heartbeatIntervalFrames: Config.HeartbeatIntervalFrames);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            Slice?.Dispose();
            if (_disposeTransport)
                DisposeTransport(Transport);
            _disposed = true;
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportServerRunner));
        }

        private bool ShouldBroadcastSnapshot(RuntimeFrame authoritativeFrame)
        {
            return Session.HasPendingFullSnapshotReceivers
                || (Config.StateSyncPolicy != NetworkStateSyncPolicy.BootstrapOnly
                    && authoritativeFrame.Value % Config.SnapshotIntervalFrames == 0L);
        }

        private double ResolveSimulationDeltaTime()
        {
            return 1d / Math.Max(1, Config.TickRate);
        }

        private void SyncEntityControlGrants()
        {
            _controlGrants.SyncConnectedPeers(
                Session,
                Slice.Services.Modules.CharacterRuntime,
                Slice.Services.Modules.RuntimeCommands.CommandBuffer,
                _peerControlPolicy);
        }

        private long ComputePeerStateHash(RuntimeFrame frame, string peerId)
        {
            int entityId = _controlGrants.GetControlledEntityId(peerId);
            long gameplayHash = entityId > 0
                ? _snapshotParticipant.ComputeNonOwnedStateHash(frame, entityId)
                : _snapshotParticipant.ComputeStateHash(frame);
            return CombineSessionAuthorityHash(gameplayHash);
        }

        private void RecordReplayFrame(RuntimeFrame authoritativeFrame)
        {
            if (!_replayRecorder.IsRecording
                || Slice.Services.Network.IsNetworkRollbackReplayActive)
                return;

            long hash = ComputeServerStateHash(authoritativeFrame);
            _replayRecorder.RecordFrame(
                authoritativeFrame,
                Slice.Services.Modules.RuntimeCommands.LastDrainedCommands,
                hash,
                CharacterTestReplayDiagnostics.CreateFrameSummary(
                    authoritativeFrame,
                    Slice.Services.Modules.RuntimeCommands.LastDrainedCommands.Count,
                    Slice.Services.Modules.CharacterEntities.Count,
                    hash));
        }

        private NetworkApplicationControlResponsePacket HandleSessionAuthorityRequest(
            string senderPeerId,
            NetworkApplicationControlRequestPacket request)
        {
            if (!CharacterTestRuntimeAiPlannerSessionAuthorityWire.IsRequest(request))
            {
                return new NetworkApplicationControlResponsePacket(
                    request?.TypeId ?? CharacterTestRuntimeAiPlannerSessionAuthorityWire.RequestTypeId,
                    request?.SchemaVersion > 0
                        ? request.SchemaVersion
                        : CharacterTestRuntimeAiPlannerSessionAuthorityWire.SchemaVersion,
                    request?.RequestId ?? 0L,
                    accepted: false,
                    reason: "unsupported CharacterTest application control request");
            }
            NetworkApplicationControlResponsePacket response =
                _sessionAuthorityWireHandler.Handle(senderPeerId, request);
            if (response.Accepted)
                _sessionAuthoritySnapshotDirty = true;
            return response;
        }

        private long ComputeServerStateHash(RuntimeFrame frame)
        {
            return CombineSessionAuthorityHash(_snapshotParticipant.ComputeStateHash(frame));
        }

        private long CombineSessionAuthorityHash(long gameplayHash)
        {
            return _sessionAuthorityParticipant == null
                ? gameplayHash
                : CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(
                    gameplayHash,
                    _sessionAuthorityParticipant.AuthorityHash);
        }

        private GameSlice CreateReplaySlice()
        {
            return new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                StoryContent = _storyContent,
                Resources = null,
                BaseResourcePreloadGroupId = string.Empty,
                InitialCameraMode = _initialCameraMode,
                ActionConfigOptions = _actionConfigOptions,
                PhysicsRuntime = CloneReplayPhysicsRuntime(),
                CreateCharacterViews = false,
                EnableLocalInputCommands = false,
                SimulationTickRate = Config.TickRate
            });
        }

        private CharacterTestCharacterPhysicsRuntime CloneReplayPhysicsRuntime()
        {
            return CloneReplayPhysicsRuntime(_physicsRuntimeTemplate);
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
                if (body.BodyId.Value >= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    continue;

                world.UpsertBody(body);
            }

            var colliders = new List<CombatPhysicsAabbCollider>();
            source.World.CopyAabbCollidersTo(colliders);
            for (int i = 0; i < colliders.Count; i++)
            {
                CombatPhysicsAabbCollider collider = colliders[i];
                if (collider.BodyId.Value >= CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart)
                    continue;

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

        private void LogServerReceiveIfNeeded(
            RuntimeFrame frame,
            RuntimeFrame beforeAckedInputFrame,
            int beforePendingCommands)
        {
            if (!CharacterTestInputFlowLog.Enabled)
                return;

            if (Session.LastAckedInputFrame.Value == beforeAckedInputFrame.Value
                && Slice.Services.Modules.PendingRuntimeCommandCount == beforePendingCommands)
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
                .Append("server-receive frame=").Append(frame.Value)
                .Append(" ackedInput=").Append(Session.LastAckedInputFrame.Value)
                .Append(" pendingCommands=").Append(Slice.Services.Modules.PendingRuntimeCommandCount)
                .Append(" connectedPeers=").Append(Session.ConnectedPeerCount)
                .Append(" transport=").Append(Transport.TransportId);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }
    }

    public sealed class CharacterTestUnityTransportClientRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        private const int ConnectRetryIntervalFrames = 15;
        private const int ConnectionWaitWarningDelayFrames = 60;
        private const int ConnectionWarningRepeatFrames = 300;
        private const int ServerSnapshotStallWarningDelayFrames = 180;
        private const int PredictionHistoryCapacity = 128;
        private const int MaxPredictionLeadExtraFrames = 10;
        private const int MaxBootstrapOnlyCatchUpSteps = 16;
        private const int IdleMismatchWarningGrowthThreshold = 3;
        private const int IdleMismatchWarningRepeatFrames = 180;
        private const int IdleMismatchActivityGraceFrames = 90;
        private const float ActiveActionSecondsEpsilon = 0.001f;

        private readonly CharacterTestNetworkSnapshotParticipant _snapshotParticipant;
        private readonly IRuntimeLogger _logger;
        private readonly RuntimeLogBuffer _inputFlowBuffer = new RuntimeLogBuffer(256);
        private readonly RuntimeLogBuffer _diagnosticLogBuffer = new RuntimeLogBuffer(256);
        private readonly PredictionScheduleState _predictionSchedule = new PredictionScheduleState();
        private readonly PredictionHistory _predictionHistory = new PredictionHistory(PredictionHistoryCapacity);
        private readonly NetworkPredictionRollbackCoordinator _rollbackCoordinator =
            new NetworkPredictionRollbackCoordinator(PredictionHistoryCapacity);
        private readonly string _connectTargetLabel;
        private readonly bool _disposeTransport;
        private readonly bool _deferLocalInputUntilConnected;
        private bool _disposed;
        private CharacterTestRuntimeAiPlannerSessionAuthorityParticipant _sessionAuthorityParticipant;
        private Action<NetworkApplicationControlResponsePacket> _sessionAuthorityResponseObserver;
        private bool _connectEnabled;
        private bool _disconnectedInputWarningLogged;
        private bool _connectedLogEmitted;
        private long _lastConnectAttemptFrame = long.MinValue;
        private long _lastConnectionWaitWarningFrame = long.MinValue;
        private long _lastServerSnapshotProgressFrame;
        private long _lastIdleMismatchWarningFrame = long.MinValue;
        private long _lastLocalPredictionActivityFrame = long.MinValue;
        private RuntimeFrame _networkFrame = RuntimeFrame.Zero;
        private int _lastObservedSnapshotCount;
        private int _lastObservedResyncCount;
        private int _lastIdleSuppressedMismatchCount;
        private int _rollbackSnapshotCorrectionCount;

        public CharacterTestUnityTransportClientRunner(
            INetworkTransport transport,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices resources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            string sessionId = CharacterTestUnityTransportSessionIds.DirectLan,
            string serverPeerId = "server",
            string clientPeerId = "client-0",
            string transportMode = "unity-transport-client",
            bool createClientViews = true,
            string connectTargetLabel = "",
            bool disposeTransport = true,
            bool connectOnCreate = true,
            bool deferLocalInputUntilConnected = false,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _disposeTransport = disposeTransport;

            IRuntimeLogger safeLogger = logger ?? NullRuntimeLogger.Instance;
            _logger = safeLogger;
            Config = CharacterTestSessionHashContract.Normalize(config ?? NetworkSessionConfig.Default);
            SessionId = string.IsNullOrEmpty(sessionId) ? CharacterTestUnityTransportSessionIds.DirectLan : sessionId;
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            ClientPeerId = string.IsNullOrEmpty(clientPeerId) ? "client-0" : clientPeerId;
            TransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-client" : transportMode;
            _connectTargetLabel = string.IsNullOrWhiteSpace(connectTargetLabel)
                ? ServerPeerId
                : connectTargetLabel;
            _connectEnabled = connectOnCreate;
            _deferLocalInputUntilConnected = deferLocalInputUntilConnected;

            Slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = safeLogger,
                StoryContent = storyContent,
                Resources = resources,
                BaseResourcePreloadGroupId = baseResourcePreloadGroupId,
                InitialCameraMode = initialCameraMode,
                ActionConfigOptions = actionConfigOptions,
                ActionCatalog = actionCatalog,
                PhysicsRuntime = physicsRuntime,
                CreateCharacterViews = createClientViews,
                EnableLocalInputCommands = true,
                SmoothNetworkSnapshotViews = true,
                SimulationTickRate = Config.TickRate,
                EnableCharacterSimulation = true,
                AudioService = audioService,
                AudioCueCatalog = audioCueCatalog,
                DisposeAudioService = disposeAudioService,
                RuntimeAiPlannerProfile = runtimeAiPlannerProfile,
                RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileReport,
                StartupCharacterSpawnSpecs = Array.Empty<CharacterTestCharacterSpawnSpec>(),
                SuppressMissingStartupSpawnWarning = true
            });

            _snapshotParticipant = Slice.Services.Network.CreateSnapshotParticipant();
            _rollbackCoordinator.RegisterParticipant(Slice.Services.Network.CreateRollbackParticipant(
                _snapshotParticipant));
            Session = new NetworkSessionRunner(
                NetworkSessionRole.Client,
                SessionId,
                ClientPeerId,
                ServerPeerId,
                Config,
                Transport,
                Slice.Services.Modules.RuntimeCommands.CommandBuffer,
                participants: new[] { _snapshotParticipant },
                resultHashProvider: ComputeClientStateHash,
                logger: safeLogger,
                snapshotApplyPolicy: NetworkSnapshotApplyPolicy.ApplyFullSnapshotOnly,
                snapshotHashLookup: LookupPredictionHash,
                authoritativeCommandReceiveHandler: HandleAuthoritativeCommandsForRollback,
                snapshotCorrectionHandler: HandleSnapshotCorrectionForRollback);

            Slice.Services.Commands.SetRuntimeCommandSubmitter(SubmitClientCommand);
            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
            if (_connectEnabled)
            {
                _logger.Info(
                    "CharacterTest.Network",
                    "Unity Transport client connecting. target="
                    + _connectTargetLabel
                    + " peer="
                    + ClientPeerId
                    + ". Local input predicts on the fixed runtime tick after server authority handshake.");
                TryConnect(RuntimeFrame.Zero, force: true);
            }
            else
            {
                _logger.Info(
                    "CharacterTest.Network",
                    "Unity Transport client waiting for room start before gameplay handshake. target="
                    + _connectTargetLabel
                    + " peer="
                    + ClientPeerId);
            }
        }

        public GameSlice Slice { get; }
        public NetworkSessionConfig Config { get; }
        public string SessionId { get; }
        public string ServerPeerId { get; }
        public string ClientPeerId { get; }
        public string TransportMode { get; }
        public INetworkTransport Transport { get; }
        public NetworkSessionRunner Session { get; }

        internal CharacterTestRuntimeAiPlannerSessionAuthorityParticipant SessionAuthorityParticipant =>
            _sessionAuthorityParticipant;

        internal void ConfigureRuntimeAiPlannerSessionAuthority(
            CharacterTestRuntimeAiPlannerSessionAuthorityParticipant participant,
            Action<NetworkApplicationControlResponsePacket> responseObserver = null)
        {
            ThrowIfDisposed();
            if (_sessionAuthorityParticipant != null)
                throw new InvalidOperationException("Runtime AI Planner session authority is already configured.");
            if (_rollbackCoordinator.HistoryCount != 0)
            {
                throw new InvalidOperationException(
                    "Runtime AI Planner session authority must be configured before rollback history is recorded.");
            }
            _sessionAuthorityParticipant = participant ?? throw new ArgumentNullException(nameof(participant));
            _sessionAuthorityResponseObserver = responseObserver;
            Session.RegisterParticipant(_sessionAuthorityParticipant);
            _rollbackCoordinator.RegisterParticipant(_sessionAuthorityParticipant);
            Session.SetApplicationControlResponseObserver(ObserveSessionAuthorityResponse);
        }

        internal void SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            if (_sessionAuthorityParticipant == null)
                throw new InvalidOperationException("Runtime AI Planner session authority is not configured.");
            Session.SubmitApplicationControl(
                Slice.CurrentFrame,
                CharacterTestRuntimeAiPlannerSessionAuthorityWire.CreateRequestPacket(requestId, request));
        }

        void ICharacterTestRuntimeAiPlannerSessionAuthorityClient.SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            SubmitRuntimeAiPlannerSessionDeckRequest(requestId, request);
        }

        public void BeginConnect(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            _connectEnabled = true;
            if (frame >= _networkFrame)
                _networkFrame = frame.Next();
            TryConnect(frame, force: true);
        }

        private void ObserveSessionAuthorityResponse(
            NetworkApplicationControlResponsePacket response)
        {
            _sessionAuthorityResponseObserver?.Invoke(response);
        }

        private long ComputeClientStateHash(RuntimeFrame frame)
        {
            long gameplayHash = _snapshotParticipant.ComputeStateHash(frame);
            return _sessionAuthorityParticipant == null
                ? gameplayHash
                : CharacterTestRuntimeAiPlannerSessionAuthorityHash.CombineGameplayStateHash(
                    gameplayHash,
                    _sessionAuthorityParticipant.AuthorityHash);
        }

        public RuntimeCommandValidationResult SubmitClientCommand(RuntimeCommand command)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            if (Session.Status != NetworkSessionStatus.Connected)
            {
                LogClientSubmit("client-wait", command, "not connected");
                if (!_deferLocalInputUntilConnected)
                    LogDisconnectedInputIfNeeded(command);
                _predictionSchedule.Reset();
                return RuntimeCommandValidationResult.Accepted(command);
            }

            _disconnectedInputWarningLogged = false;
            if (CharacterTestNetworkEntityControlGrantState.RequiresEntityControl(command)
                && _snapshotParticipant.LastAppliedLocalControlledEntityId <= 0)
            {
                LogClientSubmit("client-wait", command, "waiting for server assignment");
                _predictionSchedule.Reset();
                return RuntimeCommandValidationResult.Accepted(command);
            }

            RuntimeCommand outbound = RewriteOwnedEntityCommand(command);
            LogClientSubmit("client-submit", outbound, "connected");
            RuntimeCommandValidationResult predicted = EnqueuePredictedCommand(outbound);
            if (!predicted.Success)
            {
                LogClientSubmit("client-predict-rejected", outbound, predicted.Error.Message);
                return predicted;
            }

            LogClientSubmit("client-predict", predicted.Command, "queued locally");
            _rollbackCoordinator.RecordPredictedCommand(predicted.Command);
            Session.SubmitCommands(outbound.Frame, new[] { outbound });
            LogClientSubmit("client-send", outbound, "sent to " + ServerPeerId);
            return RuntimeCommandValidationResult.Accepted(outbound);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();
            RuntimeFrame attemptedFrame = Slice.CurrentFrame;
            try
            {

                bool deferLocalInput = ShouldDeferLocalInputUntilConnected();
                RuntimeFrame networkFrame = NextNetworkFrame();
                RuntimeFrame simulationFrame = Slice.CurrentFrame;
                if (_connectEnabled)
                    TryConnect(networkFrame, force: false);
                Session.ProcessIncoming(networkFrame, simulationFrame);
                ClearPredictionHistoryIfResynced();
                if (!deferLocalInput)
                    LogConnectionWaitIfNeeded(networkFrame);

                if (networkFrame.Value % Config.HeartbeatIntervalFrames == 0L)
                    Session.SendPing(networkFrame);

                TickClientSimulation(simulationFrame);

                Session.ProcessIncoming(NextNetworkFrame(), Slice.CurrentFrame);
                ClearPredictionHistoryIfResynced();
                Slice.Services.Modules.CharacterRuntime.TickNetworkPresentation(
                    deltaTime,
                    Config.TickRate,
                    ResolvePresentationDelayFrames());
                if (Session.Status == NetworkSessionStatus.Connected)
                    LogConnectedIfNeeded();
                TrackServerSnapshotProgress(_networkFrame);
                WarnIfIdleSuppressedMismatchGrowing(Slice.CurrentFrame);
            }
            catch (Exception failure)
            {
                Slice.SuspendAuthority(attemptedFrame, failure);
                throw;
            }
        }

        private bool ShouldDeferLocalInputUntilConnected()
        {
            return _deferLocalInputUntilConnected
                && _connectEnabled
                && Session.Status != NetworkSessionStatus.Connected;
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();

            NetworkTransportSnapshot transport = Transport.CreateSnapshot(ClientPeerId);
            NetworkPredictionRollbackDiagnostics rollback = _rollbackCoordinator.Diagnostics;
            NetworkSessionStatus serverStatus = Session.Status == NetworkSessionStatus.Connected
                ? NetworkSessionStatus.Connected
                : NetworkSessionStatus.Disconnected;
            return new CharacterTestNetworkLoopbackDiagnostics(
                available: true,
                serverStatus: serverStatus,
                clientStatus: Session.Status,
                serverFrame: Session.LastServerFrame,
                clientLastServerFrame: Slice.CurrentFrame,
                lastAckedInputFrame: Session.LastAckedInputFrame,
                rttEstimateFrames: Session.RttEstimateFrames,
                pendingRuntimeCommands: Slice.Services.Modules.PendingRuntimeCommandCount,
                pendingTransportMessages: transport.PendingMessageCount,
                lastSnapshotHash: Session.LastSnapshotHash,
                resyncCount: Session.ResyncCount,
                snapshotApplyFailureCount: Session.SnapshotApplyFailureCount,
                snapshotMismatchCount: Session.SnapshotMismatchCount,
                suppressedSnapshotMismatchCount: Session.SuppressedSnapshotMismatchCount,
                duplicateMessageCount: Session.DuplicateMessageCount,
                outOfOrderMessageCount: Session.OutOfOrderMessageCount,
                codecErrorCount: transport.CodecErrorCount,
                disconnectReason: Session.DisconnectReason,
                transportMode: TransportMode,
                transportId: Transport.TransportId,
                localPeerId: ClientPeerId,
                ownedEntityId: _snapshotParticipant.LastAppliedLocalControlledEntityId,
                connectedPeerCount: serverStatus == NetworkSessionStatus.Connected ? 1 : 0,
                ownershipSummary: CreateClientOwnershipSummary(),
                predictionLeadFrames: ResolvePredictionLeadFrames(),
                averageRttFrames: Session.AverageRttFrames,
                maxRttFrames: Session.MaxRttFrames,
                averageSnapshotIntervalFrames: Session.AverageSnapshotIntervalFrames,
                maxSnapshotIntervalFrames: Session.MaxSnapshotIntervalFrames,
                frameDriftFrames: Session.LastFrameDriftFrames,
                averageFrameDriftFrames: Session.AverageFrameDriftFrames,
                maxFrameDriftFrames: Session.MaxFrameDriftFrames,
                predictionHistoryCount: _predictionHistory.Count,
                comparableSnapshotHashCount: Session.ComparableSnapshotHashCount,
                missingSnapshotHashCount: Session.MissingSnapshotHashCount,
                snapshotRequestCount: Session.SnapshotRequestCount,
                authoritativeCommandSentCount: Session.AuthoritativeCommandSentCount,
                authoritativeCommandReceivedCount: Session.AuthoritativeCommandReceivedCount,
                authoritativeCommandEchoCount: Session.AuthoritativeCommandEchoCount,
                authoritativeCommandEnqueuedCount: Session.AuthoritativeCommandEnqueuedCount,
                authoritativeCommandLateCount: Session.AuthoritativeCommandLateCount,
                authoritativeCommandDroppedCount: Session.AuthoritativeCommandDroppedCount,
                fullSnapshotSentCount: Session.FullSnapshotSentCount,
                digestSnapshotSentCount: Session.DigestSnapshotSentCount,
                fullSnapshotReceivedCount: Session.FullSnapshotReceivedCount,
                digestSnapshotReceivedCount: Session.DigestSnapshotReceivedCount,
                sentBytes: transport.SentBytes,
                deliveredBytes: transport.DeliveredBytes,
                lastSentPayloadBytes: transport.LastSentPayloadBytes,
                maxSentPayloadBytes: transport.MaxSentPayloadBytes,
                runtimeCommandSentBytes: transport.RuntimeCommandSentBytes,
                snapshotSentBytes: transport.SnapshotSentBytes,
                snapshotRequestSentBytes: transport.SnapshotRequestSentBytes,
                heartbeatSentBytes: transport.HeartbeatSentBytes,
                roomSentBytes: transport.RoomSentBytes,
                recentDiagnostics: string.Join(" | ", Session.RecentDiagnostics),
                stateSyncPolicy: Config.StateSyncPolicy,
                heartbeatIntervalFrames: Config.HeartbeatIntervalFrames,
                rollbackHistoryCount: rollback.HistoryCount,
                rollbackHistoryDropCount: rollback.HistoryDropCount,
                rollbackCount: rollback.RollbackCount,
                rollbackFailureCount: rollback.RollbackFailureCount,
                rollbackSnapshotCorrectionCount: _rollbackSnapshotCorrectionCount,
                rollbackReplayedFrameCount: rollback.ReplayedFrameCount,
                maxRollbackFrames: rollback.MaxRollbackFrames,
                lastRollbackFrame: rollback.LastRollbackFrame,
                lastRollbackTargetFrame: rollback.LastTargetFrame,
                lastRollbackFailure: rollback.LastFailure);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Commands.SetRuntimeCommandSubmitter(null);
            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            Slice?.Dispose();
            if (_disposeTransport)
                DisposeTransport(Transport);
            _disposed = true;
        }

        private void TryConnect(RuntimeFrame frame, bool force)
        {
            if (Session.Status == NetworkSessionStatus.Connected
                || Session.Status == NetworkSessionStatus.Rejected
                || Session.Status == NetworkSessionStatus.Resynchronizing
                || Session.Status == NetworkSessionStatus.RecoveryFailed)
            {
                return;
            }

            if (Session.Status == NetworkSessionStatus.Disconnected
                && HasTerminalTransportFailure())
            {
                return;
            }

            if (!force
                && _lastConnectAttemptFrame != long.MinValue
                && frame.Value - _lastConnectAttemptFrame < ConnectRetryIntervalFrames)
            {
                return;
            }

            _lastConnectAttemptFrame = frame.Value;
            RestartUnityTransportClientIfNeeded(frame);
            Session.Connect(frame);
        }

        private bool HasTerminalTransportFailure()
        {
            string reason = Session.DisconnectReason;
            return !string.IsNullOrEmpty(reason)
                && reason.IndexOf("Unity Transport update failed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RestartUnityTransportClientIfNeeded(RuntimeFrame frame)
        {
            var unityTransport = Transport as UnityTransportNetworkTransport;
            if (unityTransport == null || unityTransport.HasActiveConnection(ServerPeerId))
                return;

            if (!unityTransport.RestartClient())
                return;

            _connectedLogEmitted = false;
            _disconnectedInputWarningLogged = false;
            _lastConnectionWaitWarningFrame = long.MinValue;
            _logger.Info(
                "CharacterTest.Network",
                "Unity Transport client transport restarted. target="
                + _connectTargetLabel
                + " frame="
                + frame.Value);
        }

        private void LogConnectionWaitIfNeeded(RuntimeFrame frame)
        {
            if (Session.Status == NetworkSessionStatus.Connected)
                return;
            if (frame.Value < ConnectionWaitWarningDelayFrames)
                return;
            if (_lastConnectionWaitWarningFrame != long.MinValue
                && frame.Value - _lastConnectionWaitWarningFrame < ConnectionWarningRepeatFrames)
            {
                return;
            }

            _lastConnectionWaitWarningFrame = frame.Value;
            string reason = !string.IsNullOrEmpty(Session.RejectionReason)
                ? Session.RejectionReason
                : !string.IsNullOrEmpty(Session.DisconnectReason)
                    ? Session.DisconnectReason
                    : "waiting for handshake response";
            _logger.Warning(
                "CharacterTest.Network",
                "Unity Transport client is not connected. target="
                + _connectTargetLabel
                + " status="
                + Session.Status
                + " reason="
                + reason
                + ". Local input prediction is waiting for server authority.");
        }

        private void LogDisconnectedInputIfNeeded(RuntimeCommand command)
        {
            if (_disconnectedInputWarningLogged || !HasActiveControlInput(command))
                return;

            _disconnectedInputWarningLogged = true;
            _logger.Warning(
                "CharacterTest.Network",
                "Local control input ignored because Unity Transport client is not connected. target="
                + _connectTargetLabel
                + " status="
                + Session.Status
                + ". Start a UnityTransportServer or switch Network Mode to Disabled/UnityTransportLoopback for local play.");
        }

        private void LogConnectedIfNeeded()
        {
            if (_connectedLogEmitted)
                return;

            _connectedLogEmitted = true;
            _lastConnectionWaitWarningFrame = long.MinValue;
            _lastObservedSnapshotCount = GetReceivedSnapshotCount();
            _lastServerSnapshotProgressFrame = _networkFrame.Value;
            _logger.Info(
                "CharacterTest.Network",
                "Unity Transport client connected. target=" + _connectTargetLabel);
        }

        private int ResolvePresentationDelayFrames()
        {
            return Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly
                ? Math.Max(1, Config.InputDelayFrames)
                : Math.Max(2, Config.SnapshotIntervalFrames);
        }

        private RuntimeFrame NextNetworkFrame()
        {
            RuntimeFrame frame = _networkFrame;
            _networkFrame = _networkFrame.Next();
            return frame;
        }

        private bool ShouldHoldClientSimulationForServerFrame(RuntimeFrame simulationFrame)
        {
            if (Session.Status != NetworkSessionStatus.Connected)
                return false;
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
            {
                if (Session.LastServerFrame.Value <= 0L)
                    return true;

                long bootstrapAheadFrames = simulationFrame.Value - Session.LastServerFrame.Value;
                return bootstrapAheadFrames >= ResolveMaxClientSimulationAheadFrames();
            }

            if (Session.LastServerFrame.Value <= 0L)
                return false;

            if (Session.DeferredSnapshotCount > 0)
                return false;
            if (Session.FullSnapshotReceivedCount + Session.DigestSnapshotReceivedCount <= 0)
                return false;

            long aheadFrames = simulationFrame.Value - Session.LastServerFrame.Value;
            return aheadFrames >= ResolveMaxClientSimulationAheadFrames();
        }

        private int ResolveMaxClientSimulationAheadFrames()
        {
            return Math.Max(1, ResolvePredictionLeadFrames() - 1);
        }

        private void TickClientSimulation(RuntimeFrame initialSimulationFrame)
        {
            if (Session.Status == NetworkSessionStatus.Resynchronizing
                || Session.Status == NetworkSessionStatus.RecoveryFailed)
                return;
            RuntimeFrame simulationFrame = initialSimulationFrame;
            if (ShouldHoldClientSimulationForServerFrame(simulationFrame))
                return;

            int steps = 0;
            do
            {
                RecordRollbackHistory();
                Slice.Tick(ResolveSimulationDeltaTime());
                TrackLocalPredictionActivity(Slice.CurrentFrame);
                RecordPredictionHistory();
                steps++;
                simulationFrame = Slice.CurrentFrame;
            }
            while (ShouldRunBootstrapOnlyCatchUpStep(simulationFrame, steps)
                && !ShouldHoldClientSimulationForServerFrame(simulationFrame));
        }

        private void RecordRollbackHistory()
        {
            if (Session.Status != NetworkSessionStatus.Connected)
                return;

            _rollbackCoordinator.RecordFrame(Slice.CurrentFrame);
        }

        private bool HandleAuthoritativeCommandsForRollback(
            NetworkAuthoritativeCommandPacket packet,
            RuntimeFrame receiveFrame)
        {
            if (packet == null || packet.Commands == null || packet.Commands.Count == 0)
                return false;

            if (Session.Status == NetworkSessionStatus.Resynchronizing)
            {
                for (int i = 0; i < packet.Commands.Count; i++)
                    _rollbackCoordinator.RecordAuthoritativeCommand(packet.Commands[i]);
                return true;
            }

            NetworkPredictionRollbackResult result = _rollbackCoordinator.ReconcileAuthoritativeCommands(
                packet.Commands,
                Slice.CurrentFrame,
                EnqueueRollbackCommand,
                ReplayRollbackFrame);
            if (result.Success)
            {
                if (result.RolledBack)
                    LogRollbackResult("authoritative-command", result);
                return true;
            }

            _logger.Warning(
                "CharacterTest.Network",
                "Rollback command reconcile failed. receiveFrame="
                + receiveFrame.Value
                + " currentFrame="
                + Slice.CurrentFrame.Value
                + " reason="
                + result.Message);
            Session.RequireRecovery(receiveFrame, result.Message);
            return true;
        }

        private NetworkSnapshotCorrectionResult HandleSnapshotCorrectionForRollback(
            NetworkSnapshotPacket packet,
            RuntimeFrame receiveFrame,
            RuntimeFrame localSimulationFrame)
        {
            if (packet == null || !packet.FullSnapshot || packet.ModuleSnapshots.Count == 0)
                return NetworkSnapshotCorrectionResult.NotHandled();
            if (_snapshotParticipant.LastAppliedLocalControlledEntityId <= 0)
                return NetworkSnapshotCorrectionResult.NotHandled();
            if (_rollbackCoordinator.HistoryCount == 0)
                return NetworkSnapshotCorrectionResult.NotHandled();

            RuntimeCustomState characterSnapshot = FindCharacterSnapshot(packet);
            if (characterSnapshot == null)
                return NetworkSnapshotCorrectionResult.NotHandled();
            RuntimeCustomState sessionAuthoritySnapshot = null;
            if (_sessionAuthorityParticipant != null)
            {
                sessionAuthoritySnapshot = FindSessionAuthoritySnapshot(packet);
                if (sessionAuthoritySnapshot == null)
                {
                    return NetworkSnapshotCorrectionResult.HandledFailed(
                        "Runtime AI Planner session authority snapshot is missing from a full correction.");
                }
                NetworkSnapshotApplyResult authorityValidation =
                    _sessionAuthorityParticipant.ValidateSnapshot(sessionAuthoritySnapshot);
                if (!authorityValidation.Success)
                    return NetworkSnapshotCorrectionResult.HandledFailed(authorityValidation.Message);
            }

            RuntimeFrame targetFrame = Slice.CurrentFrame;
            RuntimeFrame replayStartFrame = packet.AuthoritativeFrame < targetFrame
                ? packet.AuthoritativeFrame.Next()
                : targetFrame;
            double restoredElapsedSeconds = replayStartFrame.Value * ResolveSimulationDeltaTime();
            NetworkSnapshotApplyResult fixedAssemblyPreflight = _snapshotParticipant.PreflightFixedAssemblyRollback(characterSnapshot);
            if (!fixedAssemblyPreflight.Success)
                return NetworkSnapshotCorrectionResult.HandledFailed(fixedAssemblyPreflight.Message);
            Slice.Services.Network.RestoreNetworkRollbackRuntimeState(replayStartFrame, restoredElapsedSeconds);
            NetworkSnapshotApplyResult apply = _snapshotParticipant.ApplyRollbackSnapshot(
                packet.AuthoritativeFrame,
                characterSnapshot,
                preserveLocalPredictedEntity: false);
            if (!apply.Success)
                return NetworkSnapshotCorrectionResult.HandledFailed(apply.Message);

            NetworkPredictionRollbackResult rollback = _rollbackCoordinator.ReplayFromRestoredState(
                replayStartFrame,
                targetFrame,
                EnqueueRollbackCommand,
                ReplayRollbackFrame);
            if (!rollback.Success)
                return NetworkSnapshotCorrectionResult.HandledFailed(rollback.Message);

            if (sessionAuthoritySnapshot != null)
            {
                NetworkSnapshotApplyResult authorityApply =
                    _sessionAuthorityParticipant.ApplySnapshot(
                        packet.AuthoritativeFrame,
                        sessionAuthoritySnapshot);
                if (!authorityApply.Success)
                    return NetworkSnapshotCorrectionResult.HandledFailed(authorityApply.Message);
            }

            if (rollback.RolledBack || rollback.ReplayedFrameCount > 0)
                LogRollbackResult("full-snapshot", rollback);
            _rollbackSnapshotCorrectionCount++;
            return NetworkSnapshotCorrectionResult.HandledApplied(
                "CharacterTest full snapshot correction replayed from frame " + replayStartFrame.Value);
        }

        private RuntimeCustomState FindCharacterSnapshot(NetworkSnapshotPacket packet)
        {
            for (int i = 0; i < packet.ModuleSnapshots.Count; i++)
            {
                RuntimeCustomState snapshot = packet.ModuleSnapshots[i];
                if (snapshot != null
                    && string.Equals(
                        snapshot.TypeId,
                        CharacterTestNetworkSnapshotParticipant.ParticipantIdValue,
                        StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }

            return null;
        }

        private static RuntimeCustomState FindSessionAuthoritySnapshot(NetworkSnapshotPacket packet)
        {
            for (int i = 0; i < packet.ModuleSnapshots.Count; i++)
            {
                RuntimeCustomState snapshot = packet.ModuleSnapshots[i];
                if (snapshot != null
                    && string.Equals(
                        snapshot.TypeId,
                        CharacterTestRuntimeAiPlannerSessionAuthorityParticipant.ParticipantIdValue,
                        StringComparison.Ordinal))
                {
                    return snapshot;
                }
            }
            return null;
        }

        private bool EnqueueRollbackCommand(RuntimeCommand command, out string failure)
        {
            failure = string.Empty;
            Slice.Services.Modules.RuntimeCommands.CommandBuffer.RemoveWhere(
                pending => NetworkPredictionRollbackCoordinator.AreEquivalentCommandIntents(pending, command));
            RuntimeCommandValidationResult result = Slice.Services.Modules.RuntimeCommands.Enqueue(command);
            if (result.Success)
                return true;

            failure = result.Error.Message;
            return false;
        }

        private bool ReplayRollbackFrame(RuntimeFrame frame, out string failure)
        {
            failure = string.Empty;
            if (Slice.CurrentFrame != frame)
            {
                failure = "CharacterTest rollback replay frame mismatch. expected="
                    + frame.Value
                    + " actual="
                    + Slice.CurrentFrame.Value;
                return false;
            }

            Slice.Services.Network.SetNetworkRollbackReplayActive(true);
            try
            {
                Slice.Tick(ResolveSimulationDeltaTime());
            }
            finally
            {
                Slice.Services.Network.SetNetworkRollbackReplayActive(false);
            }

            return true;
        }

        private void LogRollbackResult(string reason, NetworkPredictionRollbackResult result)
        {
            _diagnosticLogBuffer.Clear()
                .Append("Rollback replay completed. reason=").Append(reason)
                .Append(" rollbackFrame=").Append(result.RollbackFrame.Value)
                .Append(" targetFrame=").Append(result.TargetFrame.Value)
                .Append(" replayedFrames=").Append(result.ReplayedFrameCount)
                .Append(" appliedCommands=").Append(result.AppliedCommandCount);
            _logger.Info("CharacterTest.Network", _diagnosticLogBuffer);
        }

        private bool ShouldRunBootstrapOnlyCatchUpStep(RuntimeFrame simulationFrame, int completedSteps)
        {
            if (completedSteps >= MaxBootstrapOnlyCatchUpSteps)
                return false;
            if (Config.StateSyncPolicy != NetworkStateSyncPolicy.BootstrapOnly)
                return false;
            if (Session.Status != NetworkSessionStatus.Connected)
                return false;
            if (Session.LastServerFrame.Value <= 0L)
                return false;

            long behindFrames = Session.LastServerFrame.Value - simulationFrame.Value;
            return behindFrames > ResolveMaxClientSimulationAheadFrames();
        }

        private RuntimeCommandValidationResult EnqueuePredictedCommand(RuntimeCommand command)
        {
            RuntimeCommand predicted = CreatePredictedCommand(command);
            return Slice.Services.Modules.RuntimeCommands.Enqueue(predicted);
        }

        private RuntimeCommand RewriteOwnedEntityCommand(RuntimeCommand command)
        {
            if (!CharacterTestNetworkEntityControlGrantState.RequiresEntityControl(command))
                return command;

            int ownedEntityId = _snapshotParticipant.LastAppliedLocalControlledEntityId;
            if (ownedEntityId <= 0 || command.TargetId == ownedEntityId)
                return command;

            return new RuntimeCommand(
                command.Frame,
                command.SourceId,
                command.CommandId,
                ownedEntityId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                command.Envelope);
        }

        private string CreateClientOwnershipSummary()
        {
            CharacterTestNetworkOwnershipEntry[] entries = _snapshotParticipant.LastAppliedOwnership;
            if (entries == null || entries.Length == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < entries.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(entries[i].PeerId).Append("->").Append(entries[i].ControlledEntityId);
            }

            return builder.ToString();
        }

        private RuntimeCommand CreatePredictedCommand(RuntimeCommand command)
        {
            RuntimeFrame frame = _predictionSchedule.Schedule(
                command.Frame,
                ResolvePredictionScheduleBaseFrame(),
                ResolvePredictionLeadFrames());

            return new RuntimeCommand(
                frame,
                command.SourceId,
                command.CommandId,
                command.TargetId,
                command.Payload0,
                command.Payload1,
                command.Payload2,
                command.TraceId,
                command.Sequence,
                command.Envelope);
        }

        private RuntimeFrame ResolvePredictionScheduleBaseFrame()
        {
            RuntimeFrame localFrame = Slice.CurrentFrame;
            RuntimeFrame serverFrame = Session.LastServerFrame;
            return serverFrame > localFrame ? serverFrame : localFrame;
        }

        private int ResolvePredictionLeadFrames()
        {
            const int processOrderSafetyFrames = 2;
            int leadFrames = Config.InputDelayFrames
                + ResolveEstimatedOneWayFrames()
                + processOrderSafetyFrames;
            int maxLeadFrames = Config.InputDelayFrames + MaxPredictionLeadExtraFrames;
            if (leadFrames < 1)
                return 1;
            return leadFrames > maxLeadFrames ? maxLeadFrames : leadFrames;
        }

        private int ResolveEstimatedOneWayFrames()
        {
            if (Session.RttEstimateFrames <= 0)
                return 2;

            return Math.Max(2, (Session.RttEstimateFrames + 1) / 2);
        }

        private void RecordPredictionHistory()
        {
            if (Session.Status != NetworkSessionStatus.Connected)
                return;

            int ownedEntityId = _snapshotParticipant.LastAppliedLocalControlledEntityId;
            if (ownedEntityId <= 0)
                return;

            RuntimeFrame frame = Slice.CurrentFrame.Value > 0L
                ? new RuntimeFrame(Slice.CurrentFrame.Value - 1L)
                : RuntimeFrame.Zero;
            _predictionHistory.Record(new PredictionHistoryEntry(
                frame,
                _snapshotParticipant.ComputeNonOwnedStateHash(frame, ownedEntityId),
                Slice.Services.Modules.RuntimeCommands.LastDrainedCommands.Count,
                Slice.Services.Modules.PendingRuntimeCommandCount,
                Session.LastSnapshotHash,
                Session.SuppressedSnapshotMismatchCount));
        }

        private void ClearPredictionHistoryIfResynced()
        {
            if (Session.ResyncCount == _lastObservedResyncCount)
                return;

            _predictionHistory.Clear();
            _lastObservedResyncCount = Session.ResyncCount;
        }

        private double ResolveSimulationDeltaTime()
        {
            return 1d / Math.Max(1, Config.TickRate);
        }

        private void TrackServerSnapshotProgress(RuntimeFrame localFrame)
        {
            int receivedSnapshotCount = GetReceivedSnapshotCount();
            if (Config.StateSyncPolicy == NetworkStateSyncPolicy.BootstrapOnly)
            {
                _lastObservedSnapshotCount = receivedSnapshotCount;
                _lastServerSnapshotProgressFrame = localFrame.Value;
                return;
            }

            if (Session.Status != NetworkSessionStatus.Connected)
            {
                _lastObservedSnapshotCount = receivedSnapshotCount;
                _lastServerSnapshotProgressFrame = localFrame.Value;
                return;
            }

            if (receivedSnapshotCount != _lastObservedSnapshotCount)
            {
                _lastObservedSnapshotCount = receivedSnapshotCount;
                _lastServerSnapshotProgressFrame = localFrame.Value;
                return;
            }

            if (localFrame.Value - _lastServerSnapshotProgressFrame < ServerSnapshotStallWarningDelayFrames)
                return;
            if (_lastConnectionWaitWarningFrame != long.MinValue
                && localFrame.Value - _lastConnectionWaitWarningFrame < ConnectionWarningRepeatFrames)
            {
                return;
            }

            _lastConnectionWaitWarningFrame = localFrame.Value;
            _logger.Warning(
                "CharacterTest.Network",
                "Unity Transport client is connected, but authoritative snapshots stopped. target="
                + _connectTargetLabel
                + " lastServerFrame="
                + Session.LastServerFrame.Value
                + ". Server may be stopped, paused, or unreachable.");
        }

        private int GetReceivedSnapshotCount()
        {
            return Session.FullSnapshotReceivedCount + Session.DigestSnapshotReceivedCount;
        }

        private void WarnIfIdleSuppressedMismatchGrowing(RuntimeFrame localFrame)
        {
            int current = Session.SuppressedSnapshotMismatchCount;
            if (current <= _lastIdleSuppressedMismatchCount)
                return;

            if (!IsLocalPredictionIdle())
            {
                _lastIdleSuppressedMismatchCount = current;
                return;
            }
            if (_lastLocalPredictionActivityFrame != long.MinValue
                && localFrame.Value - _lastLocalPredictionActivityFrame < IdleMismatchActivityGraceFrames)
            {
                _lastIdleSuppressedMismatchCount = current;
                return;
            }

            int growth = current - _lastIdleSuppressedMismatchCount;
            if (growth < IdleMismatchWarningGrowthThreshold)
                return;
            if (_lastIdleMismatchWarningFrame != long.MinValue
                && localFrame.Value - _lastIdleMismatchWarningFrame < IdleMismatchWarningRepeatFrames)
            {
                return;
            }

            _lastIdleMismatchWarningFrame = localFrame.Value;
            _lastIdleSuppressedMismatchCount = current;
            _diagnosticLogBuffer.Clear()
                .Append("Idle client prediction hash mismatches are growing.")
                .Append(" suppressedSnapshotMismatches=").Append(current)
                .Append(" growth=").Append(growth)
                .Append(" serverFrame=").Append(Session.LastServerFrame.Value)
                .Append(" clientFrame=").Append(localFrame.Value)
                .Append(" frameDrift=").Append(Session.LastFrameDriftFrames)
                .Append(" predictionLeadFrames=").Append(ResolvePredictionLeadFrames())
                .Append(" predictionHistory=").Append(_predictionHistory.Count)
                .Append(" lastSnapshotHash=").Append(Session.LastSnapshotHash);
            _logger.Warning("CharacterTest.Network", _diagnosticLogBuffer);
        }

        private bool IsLocalPredictionIdle()
        {
            for (int i = 0; i < Slice.Services.Modules.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = Slice.Services.Modules.CharacterEntities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;

                return !HasLocalPredictionActivity(entity);
            }

            return false;
        }

        private NetworkSnapshotHashLookupResult LookupPredictionHash(RuntimeFrame frame)
        {
            return _predictionHistory.TryGetStateHash(frame, out long hash)
                ? NetworkSnapshotHashLookupResult.Found(hash)
                : NetworkSnapshotHashLookupResult.Missing();
        }

        private void TrackLocalPredictionActivity(RuntimeFrame localFrame)
        {
            if (Session.Status != NetworkSessionStatus.Connected)
                return;

            for (int i = 0; i < Slice.Services.Modules.CharacterEntities.Count; i++)
            {
                CharacterTestCharacterEntity entity = Slice.Services.Modules.CharacterEntities[i];
                if (entity.Control.Owner != CharacterTestCharacterControlOwner.LocalPlayer)
                    continue;
                if (!HasLocalPredictionActivity(entity))
                    return;

                _lastLocalPredictionActivityFrame = localFrame.Value;
                return;
            }
        }

        private static bool HasLocalPredictionActivity(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return false;

            return CharacterTestInputFlowLog.HasActiveInput(entity.Input)
                || CharacterTestInputFlowLog.HasMotionEffect(entity)
                || HasActionActivity(entity)
                || HasAnimationActivity(entity)
                || HasCombatActivity(entity)
                || entity.PendingMotionRequestCount > 0
                || entity.PendingCombatRequestCount > 0
                || entity.PendingGameplayRequestCount > 0;
        }

        private static bool HasActionActivity(CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterActionComponent action = entity.Action;
            return action.ActiveActionId.IsValid
                || !string.IsNullOrEmpty(action.AnimationActionKeyThisFrame)
                || action.AnimationTransitionSeconds > ActiveActionSecondsEpsilon
                || action.AnimationHoldSeconds > ActiveActionSecondsEpsilon
                || action.JumpImpulseY > ActiveActionSecondsEpsilon
                || action.MovementLocked
                || action.MovementMode != CharacterMovementMode.Idle;
        }

        private static bool HasAnimationActivity(CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterAnimationComponent animation = entity.Animation;
            return animation.ActionActive
                || !string.IsNullOrEmpty(animation.ActionKey)
                || animation.ActionTransitionSeconds > ActiveActionSecondsEpsilon
                || animation.ActionHoldSeconds > ActiveActionSecondsEpsilon;
        }

        private static bool HasCombatActivity(CharacterTestCharacterEntity entity)
        {
            return entity.CombatState.IsReacting
                || entity.CombatState.IsCohesionBreakReacting
                || entity.LastCombatHitCount > 0;
        }

        private static bool HasActiveControlInput(RuntimeCommand command)
        {
            return CharacterTestInputFlowLog.HasActiveCommand(command);
        }

        private sealed class PredictionScheduleState
        {
            private bool _hasAnchor;
            private long _anchorCommandFrame;
            private long _anchorLocalFrame;

            public RuntimeFrame Schedule(RuntimeFrame commandFrame, RuntimeFrame localFrame, int leadFrames)
            {
                long safeFrame = AddFrames(localFrame.Value, leadFrames);
                long commandScheduledFrame = AddFrames(commandFrame.Value, leadFrames);
                long scheduledFrame;
                if (!_hasAnchor)
                {
                    scheduledFrame = Math.Max(commandScheduledFrame, safeFrame);
                    SetAnchor(commandFrame.Value, scheduledFrame);
                }
                else
                {
                    scheduledFrame = _anchorLocalFrame + (commandFrame.Value - _anchorCommandFrame);
                    if (scheduledFrame < safeFrame)
                    {
                        scheduledFrame = safeFrame;
                        SetAnchor(commandFrame.Value, scheduledFrame);
                    }
                }

                return new RuntimeFrame(scheduledFrame < 0L ? 0L : scheduledFrame);
            }

            public void Reset()
            {
                _hasAnchor = false;
                _anchorCommandFrame = 0L;
                _anchorLocalFrame = 0L;
            }

            private void SetAnchor(long commandFrame, long localFrame)
            {
                _hasAnchor = true;
                _anchorCommandFrame = commandFrame;
                _anchorLocalFrame = localFrame;
            }

            private static long AddFrames(long frame, int offset)
            {
                long result = frame + offset;
                return result < 0L ? 0L : result;
            }
        }

        private readonly struct PredictionHistoryEntry
        {
            public PredictionHistoryEntry(
                RuntimeFrame frame,
                long stateHash,
                int commandCount,
                int pendingCommandCount,
                long snapshotHash,
                int suppressedMismatchCount)
            {
                Frame = frame;
                StateHash = stateHash;
                CommandCount = commandCount;
                PendingCommandCount = pendingCommandCount;
                SnapshotHash = snapshotHash;
                SuppressedMismatchCount = suppressedMismatchCount;
            }

            public RuntimeFrame Frame { get; }
            public long StateHash { get; }
            public int CommandCount { get; }
            public int PendingCommandCount { get; }
            public long SnapshotHash { get; }
            public int SuppressedMismatchCount { get; }
        }

        private sealed class PredictionHistory
        {
            private readonly PredictionHistoryEntry[] _entries;
            private int _nextIndex;
            private int _count;

            public PredictionHistory(int capacity)
            {
                _entries = new PredictionHistoryEntry[capacity > 0 ? capacity : PredictionHistoryCapacity];
            }

            public int Count => _count;

            public void Record(PredictionHistoryEntry entry)
            {
                _entries[_nextIndex] = entry;
                _nextIndex = (_nextIndex + 1) % _entries.Length;
                if (_count < _entries.Length)
                    _count++;
            }

            public void Clear()
            {
                _nextIndex = 0;
                _count = 0;
            }

            public bool TryGetStateHash(RuntimeFrame frame, out long hash)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_entries[i].Frame == frame)
                    {
                        hash = _entries[i].StateHash;
                        return true;
                    }
                }

                hash = 0L;
                return false;
            }
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportClientRunner));
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
            _inputFlowBuffer.Append(" status=").Append(Session.Status.ToString())
                .Append(" target=").Append(_connectTargetLabel);
            if (!string.IsNullOrWhiteSpace(reason))
                _inputFlowBuffer.Append(" reason=").Append(reason);
            _logger.Info(CharacterTestInputFlowLog.Category, _inputFlowBuffer);
        }
    }
}
