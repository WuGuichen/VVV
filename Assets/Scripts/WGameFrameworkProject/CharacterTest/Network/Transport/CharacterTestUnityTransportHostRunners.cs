using System;
using MxFramework.Audio;
using MxFramework.Network;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestUnityTransportHostRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkReplayRunner,
        ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        private readonly CharacterTestUnityTransportServerRunner _serverRunner;
        private readonly CharacterTestUnityTransportClientRunner _localClientRunner;
        private readonly string _transportMode;
        private readonly bool _disposeTransports;
        private bool _disposed;

        public CharacterTestUnityTransportHostRunner(
            INetworkTransport serverTransport,
            INetworkTransport localClientTransport,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices localClientResources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            string sessionId = CharacterTestUnityTransportSessionIds.DirectLan,
            string serverPeerId = "server",
            string localClientPeerId = "client-0",
            string transportMode = "unity-transport-host",
            string connectTargetLabel = "127.0.0.1",
            bool createClientViews = true,
            bool disposeTransports = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            if (serverTransport == null)
                throw new ArgumentNullException(nameof(serverTransport));
            if (localClientTransport == null)
                throw new ArgumentNullException(nameof(localClientTransport));

            _disposeTransports = disposeTransports;
            string safeTransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-host" : transportMode;
            _transportMode = safeTransportMode;
            NetworkSessionConfig safeConfig = CharacterTestSessionHashContract.Normalize(config ?? NetworkSessionConfig.Default);
            string safeSessionId = string.IsNullOrEmpty(sessionId) ? CharacterTestUnityTransportSessionIds.DirectLan : sessionId;
            string safeServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            string safeClientPeerId = string.IsNullOrEmpty(localClientPeerId) ? "client-0" : localClientPeerId;

            try
            {
                _serverRunner = new CharacterTestUnityTransportServerRunner(
                    serverTransport,
                    logger,
                    storyContent,
                    resources: null,
                    baseResourcePreloadGroupId: string.Empty,
                    initialCameraMode: initialCameraMode,
                    actionConfigOptions: actionConfigOptions,
                    physicsRuntime: physicsRuntime,
                    config: safeConfig,
                    sessionId: safeSessionId,
                    serverPeerId: safeServerPeerId,
                    transportMode: safeTransportMode + "-server",
                    createServerViews: false,
                    enableLocalInputCommands: false,
                    disposeTransport: false,
                    runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                    actionCatalog: actionCatalog);
                _localClientRunner = new CharacterTestUnityTransportClientRunner(
                    localClientTransport,
                    logger,
                    storyContent,
                    localClientResources,
                    baseResourcePreloadGroupId,
                    initialCameraMode,
                    actionConfigOptions,
                    physicsRuntime,
                    safeConfig,
                    sessionId: safeSessionId,
                    serverPeerId: safeServerPeerId,
                    clientPeerId: safeClientPeerId,
                    transportMode: safeTransportMode,
                    createClientViews: createClientViews,
                    connectTargetLabel: connectTargetLabel,
                    disposeTransport: false,
                    connectOnCreate: true,
                    audioService: audioService,
                    audioCueCatalog: audioCueCatalog,
                    disposeAudioService: disposeAudioService,
                    runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                    actionCatalog: actionCatalog);
            }
            catch
            {
                _localClientRunner?.Dispose();
                _serverRunner?.Dispose();
                if (_disposeTransports)
                    DisposeTransports(serverTransport, localClientTransport);
                throw;
            }

            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
        }

        public GameSlice Slice => _localClientRunner.Slice;
        public CharacterTestUnityTransportServerRunner ServerRunner => _serverRunner;
        public NetworkSessionConfig Config => _serverRunner.Config;
        public CharacterTestUnityTransportClientRunner LocalClientRunner => _localClientRunner;
        public NetworkSessionRunner ServerSession => _serverRunner.Session;
        public NetworkSessionRunner LocalClientSession => _localClientRunner.Session;
        public INetworkTransport ServerTransport => _serverRunner.Transport;
        public INetworkTransport LocalClientTransport => _localClientRunner.Transport;
        public bool IsReplayRecording => _serverRunner.IsReplayRecording;

        void ICharacterTestRuntimeAiPlannerSessionAuthorityClient.SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)_localClientRunner)
                .SubmitRuntimeAiPlannerSessionDeckRequest(requestId, request);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();
            _serverRunner.Slice.ThrowIfAuthorityUnavailable();
            Slice.ThrowIfAuthorityUnavailable();
            RuntimeFrame frame = _serverRunner.Slice.CurrentFrame;
            try
            {
                _serverRunner.TickForHost(deltaTime);
                _localClientRunner.Tick(deltaTime);
                _serverRunner.PublishHostReplayFrame();
            }
            catch (Exception failure)
            {
                _serverRunner.Slice.SuspendAuthority(frame, failure);
                Slice.SuspendAuthority(frame, failure);
                throw;
            }
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return MergeDiagnostics(
                _serverRunner.CaptureDiagnostics(),
                _localClientRunner.CaptureDiagnostics(),
                _transportMode);
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            return _serverRunner.BeginReplayRecording();
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _serverRunner.CreateReplayCapture();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            _localClientRunner?.Dispose();
            _serverRunner?.Dispose();
            if (_disposeTransports)
                DisposeTransports(ServerTransport, LocalClientTransport);
            _disposed = true;
        }

        internal static CharacterTestNetworkLoopbackDiagnostics MergeDiagnostics(
            CharacterTestNetworkLoopbackDiagnostics server,
            CharacterTestNetworkLoopbackDiagnostics localClient,
            string transportMode)
        {
            return new CharacterTestNetworkLoopbackDiagnostics(
                available: server.Available && localClient.Available,
                serverStatus: server.ServerStatus,
                clientStatus: localClient.ClientStatus,
                serverFrame: server.ServerFrame,
                clientLastServerFrame: localClient.ClientLastServerFrame,
                lastAckedInputFrame: server.LastAckedInputFrame,
                rttEstimateFrames: localClient.RttEstimateFrames,
                pendingRuntimeCommands: server.PendingRuntimeCommands + localClient.PendingRuntimeCommands,
                pendingTransportMessages: server.PendingTransportMessages + localClient.PendingTransportMessages,
                lastSnapshotHash: localClient.LastSnapshotHash,
                resyncCount: localClient.ResyncCount,
                snapshotApplyFailureCount: server.SnapshotApplyFailureCount + localClient.SnapshotApplyFailureCount,
                snapshotMismatchCount: localClient.SnapshotMismatchCount,
                suppressedSnapshotMismatchCount: localClient.SuppressedSnapshotMismatchCount,
                duplicateMessageCount: server.DuplicateMessageCount + localClient.DuplicateMessageCount,
                outOfOrderMessageCount: server.OutOfOrderMessageCount + localClient.OutOfOrderMessageCount,
                codecErrorCount: server.CodecErrorCount + localClient.CodecErrorCount,
                disconnectReason: CombineReason(server.DisconnectReason, localClient.DisconnectReason),
                transportMode: string.IsNullOrEmpty(transportMode) ? localClient.TransportMode : transportMode,
                transportId: CombineTransportId(server.TransportId, localClient.TransportId),
                localPeerId: localClient.LocalPeerId,
                ownedEntityId: localClient.OwnedEntityId,
                connectedPeerCount: server.ConnectedPeerCount,
                ownershipSummary: server.OwnershipSummary,
                predictionLeadFrames: localClient.PredictionLeadFrames,
                averageRttFrames: localClient.AverageRttFrames,
                maxRttFrames: localClient.MaxRttFrames,
                averageSnapshotIntervalFrames: localClient.AverageSnapshotIntervalFrames,
                maxSnapshotIntervalFrames: localClient.MaxSnapshotIntervalFrames,
                frameDriftFrames: localClient.FrameDriftFrames,
                averageFrameDriftFrames: localClient.AverageFrameDriftFrames,
                maxFrameDriftFrames: localClient.MaxFrameDriftFrames,
                predictionHistoryCount: localClient.PredictionHistoryCount,
                comparableSnapshotHashCount: localClient.ComparableSnapshotHashCount,
                missingSnapshotHashCount: localClient.MissingSnapshotHashCount,
                snapshotRequestCount: server.SnapshotRequestCount + localClient.SnapshotRequestCount,
                authoritativeCommandSentCount: server.AuthoritativeCommandSentCount + localClient.AuthoritativeCommandSentCount,
                authoritativeCommandReceivedCount: server.AuthoritativeCommandReceivedCount + localClient.AuthoritativeCommandReceivedCount,
                authoritativeCommandEchoCount: server.AuthoritativeCommandEchoCount + localClient.AuthoritativeCommandEchoCount,
                authoritativeCommandEnqueuedCount: server.AuthoritativeCommandEnqueuedCount + localClient.AuthoritativeCommandEnqueuedCount,
                authoritativeCommandLateCount: server.AuthoritativeCommandLateCount + localClient.AuthoritativeCommandLateCount,
                authoritativeCommandDroppedCount: server.AuthoritativeCommandDroppedCount + localClient.AuthoritativeCommandDroppedCount,
                fullSnapshotSentCount: server.FullSnapshotSentCount,
                digestSnapshotSentCount: server.DigestSnapshotSentCount,
                fullSnapshotReceivedCount: localClient.FullSnapshotReceivedCount,
                digestSnapshotReceivedCount: localClient.DigestSnapshotReceivedCount,
                sentBytes: server.SentBytes + localClient.SentBytes,
                deliveredBytes: server.DeliveredBytes + localClient.DeliveredBytes,
                lastSentPayloadBytes: Math.Max(server.LastSentPayloadBytes, localClient.LastSentPayloadBytes),
                maxSentPayloadBytes: Math.Max(server.MaxSentPayloadBytes, localClient.MaxSentPayloadBytes),
                runtimeCommandSentBytes: server.RuntimeCommandSentBytes + localClient.RuntimeCommandSentBytes,
                snapshotSentBytes: server.SnapshotSentBytes + localClient.SnapshotSentBytes,
                snapshotRequestSentBytes: server.SnapshotRequestSentBytes + localClient.SnapshotRequestSentBytes,
                heartbeatSentBytes: server.HeartbeatSentBytes + localClient.HeartbeatSentBytes,
                roomSentBytes: server.RoomSentBytes + localClient.RoomSentBytes,
                recentDiagnostics: CombineReason(server.RecentDiagnostics, localClient.RecentDiagnostics),
                stateSyncPolicy: localClient.StateSyncPolicy,
                heartbeatIntervalFrames: localClient.HeartbeatIntervalFrames,
                rollbackHistoryCount: localClient.RollbackHistoryCount,
                rollbackHistoryDropCount: localClient.RollbackHistoryDropCount,
                rollbackCount: localClient.RollbackCount,
                rollbackFailureCount: localClient.RollbackFailureCount,
                rollbackSnapshotCorrectionCount: localClient.RollbackSnapshotCorrectionCount,
                rollbackReplayedFrameCount: localClient.RollbackReplayedFrameCount,
                maxRollbackFrames: localClient.MaxRollbackFrames,
                lastRollbackFrame: localClient.LastRollbackFrame,
                lastRollbackTargetFrame: localClient.LastRollbackTargetFrame,
                lastRollbackFailure: localClient.LastRollbackFailure);
        }

        private static string CombineTransportId(string left, string right)
        {
            if (string.IsNullOrEmpty(left))
                return right ?? string.Empty;
            if (string.IsNullOrEmpty(right) || string.Equals(left, right, StringComparison.Ordinal))
                return left;
            return left + "+" + right;
        }

        private static string CombineReason(string serverReason, string clientReason)
        {
            if (string.IsNullOrEmpty(serverReason))
                return clientReason ?? string.Empty;
            if (string.IsNullOrEmpty(clientReason))
                return serverReason;
            if (string.Equals(serverReason, clientReason, StringComparison.Ordinal))
                return serverReason;
            return serverReason + "; " + clientReason;
        }

        private static void DisposeTransports(INetworkTransport serverTransport, INetworkTransport localClientTransport)
        {
            if (localClientTransport is IDisposable localClientDisposable)
                localClientDisposable.Dispose();
            if (!ReferenceEquals(serverTransport, localClientTransport)
                && serverTransport is IDisposable serverDisposable)
            {
                serverDisposable.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportHostRunner));
        }
    }

    public sealed class CharacterTestUnityTransportRoomHostRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkLobbyRunner,
        ICharacterTestNetworkReplayRunner,
        ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        private readonly CharacterTestUnityTransportRoomServerRunner _serverRunner;
        private readonly CharacterTestUnityTransportRoomClientRunner _localClientRunner;
        private readonly string _transportMode;
        private readonly bool _disposeTransports;
        private bool _disposed;

        public CharacterTestUnityTransportRoomHostRunner(
            INetworkTransport serverTransport,
            INetworkTransport localClientTransport,
            NetworkRoomConfig roomConfig,
            string displayName,
            bool autoReady,
            bool autoStartWhenReady,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices localClientResources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            string serverPeerId = "server",
            string localClientPeerId = "client-0",
            string transportMode = "unity-transport-room-host",
            string connectTargetLabel = "127.0.0.1",
            bool createClientViews = true,
            bool disposeTransports = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            if (serverTransport == null)
                throw new ArgumentNullException(nameof(serverTransport));
            if (localClientTransport == null)
                throw new ArgumentNullException(nameof(localClientTransport));

            _disposeTransports = disposeTransports;
            string safeTransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-room-host" : transportMode;
            _transportMode = safeTransportMode;
            string safeServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            string safeClientPeerId = string.IsNullOrEmpty(localClientPeerId) ? "client-0" : localClientPeerId;

            try
            {
                _serverRunner = new CharacterTestUnityTransportRoomServerRunner(
                    serverTransport,
                    roomConfig,
                    autoStartWhenReady,
                    logger,
                    storyContent,
                    resources: null,
                    baseResourcePreloadGroupId: string.Empty,
                    initialCameraMode: initialCameraMode,
                    actionConfigOptions: actionConfigOptions,
                    physicsRuntime: physicsRuntime,
                    serverPeerId: safeServerPeerId,
                    transportMode: safeTransportMode + "-server",
                    createServerViews: false,
                    disposeTransport: false,
                    runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                    actionCatalog: actionCatalog);
                _localClientRunner = new CharacterTestUnityTransportRoomClientRunner(
                    localClientTransport,
                    roomConfig,
                    displayName,
                    autoReady,
                    logger,
                    storyContent,
                    localClientResources,
                    baseResourcePreloadGroupId,
                    initialCameraMode,
                    actionConfigOptions,
                    physicsRuntime,
                    serverPeerId: safeServerPeerId,
                    clientPeerId: safeClientPeerId,
                    transportMode: safeTransportMode,
                    createClientViews: createClientViews,
                    connectTargetLabel: connectTargetLabel,
                    disposeTransport: false,
                    audioService: audioService,
                    audioCueCatalog: audioCueCatalog,
                    disposeAudioService: disposeAudioService,
                    runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                    runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                    actionCatalog: actionCatalog);
            }
            catch
            {
                _localClientRunner?.Dispose();
                _serverRunner?.Dispose();
                if (_disposeTransports)
                    DisposeTransports(serverTransport, localClientTransport);
                throw;
            }

            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
        }

        public GameSlice Slice => _localClientRunner.Slice;
        public CharacterTestUnityTransportRoomServerRunner ServerRunner => _serverRunner;
        public NetworkRoomConfig RoomConfig => _serverRunner.RoomConfig;
        public NetworkSessionConfig Config => _serverRunner.Config;
        public CharacterTestUnityTransportRoomClientRunner LocalClientRunner => _localClientRunner;
        public NetworkRoomSessionRunner ServerRoomSession => _serverRunner.RoomSession;
        public NetworkRoomSessionRunner LocalClientRoomSession => _localClientRunner.RoomSession;
        public NetworkSessionRunner ServerGameplaySession => _serverRunner.GameplaySession;
        public NetworkSessionRunner LocalClientGameplaySession => _localClientRunner.GameplaySession;
        public INetworkTransport ServerTransport => _serverRunner.Transport;
        public INetworkTransport LocalClientTransport => _localClientRunner.Transport;
        public bool IsRoomHost => true;
        public bool LocalRoomReady => _localClientRunner.LocalRoomReady;
        public bool CanSetRoomReady => _localClientRunner.CanSetRoomReady;
        public bool CanStartRoomGame => _serverRunner.CanStartRoomGame;
        public bool IsReplayRecording => _serverRunner.IsReplayRecording;

        void ICharacterTestRuntimeAiPlannerSessionAuthorityClient.SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)_localClientRunner)
                .SubmitRuntimeAiPlannerSessionDeckRequest(requestId, request);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();
            _serverRunner.Slice.ThrowIfAuthorityUnavailable();
            Slice.ThrowIfAuthorityUnavailable();
            RuntimeFrame frame = _serverRunner.Slice.CurrentFrame;
            try
            {
                _serverRunner.TickForHost(deltaTime);
                _localClientRunner.Tick(deltaTime);
                _serverRunner.PublishHostReplayFrame();
            }
            catch (Exception failure)
            {
                _serverRunner.Slice.SuspendAuthority(frame, failure);
                Slice.SuspendAuthority(frame, failure);
                throw;
            }
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            CharacterTestNetworkLoopbackDiagnostics merged =
                CharacterTestUnityTransportHostRunner.MergeDiagnostics(
                    _serverRunner.CaptureDiagnostics(),
                    _localClientRunner.CaptureDiagnostics(),
                    _transportMode);
            NetworkTransportSnapshot serverTransport = ServerTransport.CreateSnapshot(ServerRoomSession.PeerId);
            NetworkTransportSnapshot localTransport = LocalClientTransport.CreateSnapshot(LocalClientRoomSession.PeerId);
            return merged.WithRoom(
                ServerRoomSession.Status,
                ServerRoomSession.Config.RoomId,
                CharacterTestUnityTransportRoomServerRunner.FormatMembers(ServerRoomSession.State),
                ServerRoomSession.State.ReadyMemberCount,
                ServerRoomSession.State.ConnectedMemberCount,
                ServerRoomSession.State.StateVersion,
                ServerRoomSession.LastResponseReason,
                serverTransport.RoomSentBytes + localTransport.RoomSentBytes);
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            return _serverRunner.BeginReplayRecording();
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _serverRunner.CreateReplayCapture();
        }

        public bool TrySetLocalRoomReady(bool ready, out string reason)
        {
            ThrowIfDisposed();
            return _localClientRunner.TrySetLocalRoomReady(ready, out reason);
        }

        public bool TryStartRoomGame(out string reason)
        {
            ThrowIfDisposed();
            return _serverRunner.TryStartRoomGame(out reason);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            _localClientRunner?.Dispose();
            _serverRunner?.Dispose();
            if (_disposeTransports)
                DisposeTransports(ServerTransport, LocalClientTransport);
            _disposed = true;
        }

        private static void DisposeTransports(INetworkTransport serverTransport, INetworkTransport localClientTransport)
        {
            if (localClientTransport is IDisposable localClientDisposable)
                localClientDisposable.Dispose();
            if (!ReferenceEquals(serverTransport, localClientTransport)
                && serverTransport is IDisposable serverDisposable)
            {
                serverDisposable.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportRoomHostRunner));
        }
    }
}
