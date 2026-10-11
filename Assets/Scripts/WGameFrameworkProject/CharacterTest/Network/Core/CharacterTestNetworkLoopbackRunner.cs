using System;
using MxFramework.Network;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestNetworkLoopbackRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkReplayRunner
    {
        private readonly CharacterTestNetworkSessionRunner _runner;
        private bool _disposed;

        public CharacterTestNetworkLoopbackRunner(
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices serverResources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            NetworkSessionConfig? config = null,
            LoopbackNetworkTransportOptions? transportOptions = null,
            bool createServerViews = true)
        {
            Config = CharacterTestSessionHashContract.Normalize(config ?? NetworkSessionConfig.Default);
            LoopbackNetworkTransportOptions safeTransportOptions =
                transportOptions ?? new LoopbackNetworkTransportOptions(latencyFrames: 0);
            Transport = new LoopbackNetworkTransport(safeTransportOptions);
            _runner = new CharacterTestNetworkSessionRunner(
                Transport,
                Transport,
                logger,
                storyContent,
                serverResources,
                baseResourcePreloadGroupId,
                initialCameraMode,
                actionConfigOptions,
                physicsRuntime,
                Config,
                sessionId: "character-test-loopback",
                transportMode: "loopback",
                createServerViews: createServerViews);
        }

        public GameSlice ServerSlice => _runner.ServerSlice;
        public GameSlice ClientSlice => _runner.ClientSlice;
        public GameSlice Slice => ServerSlice;
        public NetworkSessionConfig Config { get; }
        public LoopbackNetworkTransport Transport { get; }
        public NetworkSessionRunner ServerSession => _runner.ServerSession;
        public NetworkSessionRunner ClientSession => _runner.ClientSession;
        public bool IsReplayRecording => _runner.IsReplayRecording;

        public RuntimeCommandValidationResult SubmitClientCommand(RuntimeCommand command)
        {
            ThrowIfDisposed();
            return _runner.SubmitClientCommand(command);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();
            _runner.Tick(deltaTime);
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return _runner.CaptureDiagnostics();
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            return _runner.BeginReplayRecording();
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _runner.CreateReplayCapture();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _runner?.Dispose();
            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestNetworkLoopbackRunner));
        }
    }

    public readonly struct CharacterTestNetworkLoopbackDiagnostics
    {
        public CharacterTestNetworkLoopbackDiagnostics(
            bool available,
            NetworkSessionStatus serverStatus,
            NetworkSessionStatus clientStatus,
            RuntimeFrame serverFrame,
            RuntimeFrame clientLastServerFrame,
            RuntimeFrame lastAckedInputFrame,
            int rttEstimateFrames,
            int pendingRuntimeCommands,
            int pendingTransportMessages,
            long lastSnapshotHash,
            int resyncCount,
            int snapshotApplyFailureCount,
            int snapshotMismatchCount = 0,
            int suppressedSnapshotMismatchCount = 0,
            int duplicateMessageCount = 0,
            int outOfOrderMessageCount = 0,
            long codecErrorCount = 0L,
            string disconnectReason = "",
            string transportMode = "",
            string transportId = "",
            string localPeerId = "",
            int ownedEntityId = 0,
            int connectedPeerCount = 0,
            string ownershipSummary = "",
            int predictionLeadFrames = 0,
            int averageRttFrames = 0,
            int maxRttFrames = 0,
            int averageSnapshotIntervalFrames = 0,
            int maxSnapshotIntervalFrames = 0,
            int frameDriftFrames = 0,
            int averageFrameDriftFrames = 0,
            int maxFrameDriftFrames = 0,
            int predictionHistoryCount = 0,
            int comparableSnapshotHashCount = 0,
            int missingSnapshotHashCount = 0,
            int snapshotRequestCount = 0,
            int authoritativeCommandSentCount = 0,
            int authoritativeCommandReceivedCount = 0,
            int authoritativeCommandEchoCount = 0,
            int authoritativeCommandEnqueuedCount = 0,
            int authoritativeCommandLateCount = 0,
            int authoritativeCommandDroppedCount = 0,
            int fullSnapshotSentCount = 0,
            int digestSnapshotSentCount = 0,
            int fullSnapshotReceivedCount = 0,
            int digestSnapshotReceivedCount = 0,
            long sentBytes = 0L,
            long deliveredBytes = 0L,
            int lastSentPayloadBytes = 0,
            int maxSentPayloadBytes = 0,
            long runtimeCommandSentBytes = 0L,
            long snapshotSentBytes = 0L,
            long snapshotRequestSentBytes = 0L,
            long heartbeatSentBytes = 0L,
            long roomSentBytes = 0L,
            NetworkRoomStatus roomStatus = NetworkRoomStatus.Closed,
            string roomId = "",
            string roomMembers = "",
            int roomReadyCount = 0,
            int roomConnectedCount = 0,
            long roomStateVersion = 0L,
            string roomReason = "",
            string recentDiagnostics = "",
            NetworkStateSyncPolicy stateSyncPolicy = NetworkStateSyncPolicy.SnapshotVerification,
            int heartbeatIntervalFrames = NetworkSessionConfig.DefaultHeartbeatIntervalFrames,
            int rollbackHistoryCount = 0,
            int rollbackHistoryDropCount = 0,
            int rollbackCount = 0,
            int rollbackFailureCount = 0,
            int rollbackSnapshotCorrectionCount = 0,
            int rollbackReplayedFrameCount = 0,
            int maxRollbackFrames = 0,
            RuntimeFrame lastRollbackFrame = default(RuntimeFrame),
            RuntimeFrame lastRollbackTargetFrame = default(RuntimeFrame),
            string lastRollbackFailure = "")
        {
            Available = available;
            ServerStatus = serverStatus;
            ClientStatus = clientStatus;
            ServerFrame = serverFrame;
            ClientLastServerFrame = clientLastServerFrame;
            LastAckedInputFrame = lastAckedInputFrame;
            RttEstimateFrames = rttEstimateFrames;
            PendingRuntimeCommands = pendingRuntimeCommands;
            PendingTransportMessages = pendingTransportMessages;
            LastSnapshotHash = lastSnapshotHash;
            ResyncCount = resyncCount;
            SnapshotApplyFailureCount = snapshotApplyFailureCount;
            SnapshotMismatchCount = snapshotMismatchCount;
            SuppressedSnapshotMismatchCount = suppressedSnapshotMismatchCount;
            DuplicateMessageCount = duplicateMessageCount;
            OutOfOrderMessageCount = outOfOrderMessageCount;
            CodecErrorCount = codecErrorCount;
            DisconnectReason = disconnectReason ?? string.Empty;
            TransportMode = string.IsNullOrEmpty(transportMode) ? "none" : transportMode;
            TransportId = transportId ?? string.Empty;
            LocalPeerId = localPeerId ?? string.Empty;
            OwnedEntityId = ownedEntityId > 0 ? ownedEntityId : 0;
            ConnectedPeerCount = connectedPeerCount < 0 ? 0 : connectedPeerCount;
            OwnershipSummary = string.IsNullOrWhiteSpace(ownershipSummary) ? "none" : ownershipSummary;
            PredictionLeadFrames = predictionLeadFrames;
            AverageRttFrames = averageRttFrames;
            MaxRttFrames = maxRttFrames;
            AverageSnapshotIntervalFrames = averageSnapshotIntervalFrames;
            MaxSnapshotIntervalFrames = maxSnapshotIntervalFrames;
            FrameDriftFrames = frameDriftFrames;
            AverageFrameDriftFrames = averageFrameDriftFrames;
            MaxFrameDriftFrames = maxFrameDriftFrames;
            PredictionHistoryCount = predictionHistoryCount;
            ComparableSnapshotHashCount = comparableSnapshotHashCount;
            MissingSnapshotHashCount = missingSnapshotHashCount;
            SnapshotRequestCount = snapshotRequestCount;
            AuthoritativeCommandSentCount = authoritativeCommandSentCount;
            AuthoritativeCommandReceivedCount = authoritativeCommandReceivedCount;
            AuthoritativeCommandEchoCount = authoritativeCommandEchoCount;
            AuthoritativeCommandEnqueuedCount = authoritativeCommandEnqueuedCount;
            AuthoritativeCommandLateCount = authoritativeCommandLateCount;
            AuthoritativeCommandDroppedCount = authoritativeCommandDroppedCount;
            FullSnapshotSentCount = fullSnapshotSentCount;
            DigestSnapshotSentCount = digestSnapshotSentCount;
            FullSnapshotReceivedCount = fullSnapshotReceivedCount;
            DigestSnapshotReceivedCount = digestSnapshotReceivedCount;
            SentBytes = sentBytes;
            DeliveredBytes = deliveredBytes;
            LastSentPayloadBytes = lastSentPayloadBytes;
            MaxSentPayloadBytes = maxSentPayloadBytes;
            RuntimeCommandSentBytes = runtimeCommandSentBytes;
            SnapshotSentBytes = snapshotSentBytes;
            SnapshotRequestSentBytes = snapshotRequestSentBytes;
            HeartbeatSentBytes = heartbeatSentBytes;
            RoomSentBytes = roomSentBytes;
            RoomStatus = roomStatus;
            RoomId = string.IsNullOrWhiteSpace(roomId) ? "none" : roomId;
            RoomMembers = string.IsNullOrWhiteSpace(roomMembers) ? "none" : roomMembers;
            RoomReadyCount = roomReadyCount < 0 ? 0 : roomReadyCount;
            RoomConnectedCount = roomConnectedCount < 0 ? 0 : roomConnectedCount;
            RoomStateVersion = roomStateVersion < 0L ? 0L : roomStateVersion;
            RoomReason = roomReason ?? string.Empty;
            RecentDiagnostics = recentDiagnostics ?? string.Empty;
            StateSyncPolicy = stateSyncPolicy;
            HeartbeatIntervalFrames = heartbeatIntervalFrames > 0
                ? heartbeatIntervalFrames
                : NetworkSessionConfig.DefaultHeartbeatIntervalFrames;
            RollbackHistoryCount = rollbackHistoryCount < 0 ? 0 : rollbackHistoryCount;
            RollbackHistoryDropCount = rollbackHistoryDropCount < 0 ? 0 : rollbackHistoryDropCount;
            RollbackCount = rollbackCount < 0 ? 0 : rollbackCount;
            RollbackFailureCount = rollbackFailureCount < 0 ? 0 : rollbackFailureCount;
            RollbackSnapshotCorrectionCount = rollbackSnapshotCorrectionCount < 0 ? 0 : rollbackSnapshotCorrectionCount;
            RollbackReplayedFrameCount = rollbackReplayedFrameCount < 0 ? 0 : rollbackReplayedFrameCount;
            MaxRollbackFrames = maxRollbackFrames < 0 ? 0 : maxRollbackFrames;
            LastRollbackFrame = lastRollbackFrame;
            LastRollbackTargetFrame = lastRollbackTargetFrame;
            LastRollbackFailure = lastRollbackFailure ?? string.Empty;
        }

        public bool Available { get; }
        public NetworkSessionStatus ServerStatus { get; }
        public NetworkSessionStatus ClientStatus { get; }
        public RuntimeFrame ServerFrame { get; }
        public RuntimeFrame ClientLastServerFrame { get; }
        public RuntimeFrame LastAckedInputFrame { get; }
        public int RttEstimateFrames { get; }
        public int PendingRuntimeCommands { get; }
        public int PendingTransportMessages { get; }
        public long LastSnapshotHash { get; }
        public int ResyncCount { get; }
        public int SnapshotApplyFailureCount { get; }
        public int SnapshotMismatchCount { get; }
        public int SuppressedSnapshotMismatchCount { get; }
        public int DuplicateMessageCount { get; }
        public int OutOfOrderMessageCount { get; }
        public long CodecErrorCount { get; }
        public string DisconnectReason { get; }
        public string TransportMode { get; }
        public string TransportId { get; }
        public string LocalPeerId { get; }
        public int OwnedEntityId { get; }
        public int ConnectedPeerCount { get; }
        public string OwnershipSummary { get; }
        public int PredictionLeadFrames { get; }
        public int AverageRttFrames { get; }
        public int MaxRttFrames { get; }
        public int AverageSnapshotIntervalFrames { get; }
        public int MaxSnapshotIntervalFrames { get; }
        public int FrameDriftFrames { get; }
        public int AverageFrameDriftFrames { get; }
        public int MaxFrameDriftFrames { get; }
        public int PredictionHistoryCount { get; }
        public int ComparableSnapshotHashCount { get; }
        public int MissingSnapshotHashCount { get; }
        public int SnapshotRequestCount { get; }
        public int AuthoritativeCommandSentCount { get; }
        public int AuthoritativeCommandReceivedCount { get; }
        public int AuthoritativeCommandEchoCount { get; }
        public int AuthoritativeCommandEnqueuedCount { get; }
        public int AuthoritativeCommandLateCount { get; }
        public int AuthoritativeCommandDroppedCount { get; }
        public int FullSnapshotSentCount { get; }
        public int DigestSnapshotSentCount { get; }
        public int FullSnapshotReceivedCount { get; }
        public int DigestSnapshotReceivedCount { get; }
        public long SentBytes { get; }
        public long DeliveredBytes { get; }
        public int LastSentPayloadBytes { get; }
        public int MaxSentPayloadBytes { get; }
        public long RuntimeCommandSentBytes { get; }
        public long SnapshotSentBytes { get; }
        public long SnapshotRequestSentBytes { get; }
        public long HeartbeatSentBytes { get; }
        public long RoomSentBytes { get; }
        public NetworkRoomStatus RoomStatus { get; }
        public string RoomId { get; }
        public string RoomMembers { get; }
        public int RoomReadyCount { get; }
        public int RoomConnectedCount { get; }
        public long RoomStateVersion { get; }
        public string RoomReason { get; }
        public string RecentDiagnostics { get; }
        public NetworkStateSyncPolicy StateSyncPolicy { get; }
        public int HeartbeatIntervalFrames { get; }
        public int RollbackHistoryCount { get; }
        public int RollbackHistoryDropCount { get; }
        public int RollbackCount { get; }
        public int RollbackFailureCount { get; }
        public int RollbackSnapshotCorrectionCount { get; }
        public int RollbackReplayedFrameCount { get; }
        public int MaxRollbackFrames { get; }
        public RuntimeFrame LastRollbackFrame { get; }
        public RuntimeFrame LastRollbackTargetFrame { get; }
        public string LastRollbackFailure { get; }

        public CharacterTestNetworkLoopbackDiagnostics WithRoom(
            NetworkRoomStatus roomStatus,
            string roomId,
            string roomMembers,
            int roomReadyCount,
            int roomConnectedCount,
            long roomStateVersion,
            string roomReason,
            long roomSentBytes)
        {
            return new CharacterTestNetworkLoopbackDiagnostics(
                Available,
                ServerStatus,
                ClientStatus,
                ServerFrame,
                ClientLastServerFrame,
                LastAckedInputFrame,
                RttEstimateFrames,
                PendingRuntimeCommands,
                PendingTransportMessages,
                LastSnapshotHash,
                ResyncCount,
                SnapshotApplyFailureCount,
                SnapshotMismatchCount,
                SuppressedSnapshotMismatchCount,
                DuplicateMessageCount,
                OutOfOrderMessageCount,
                CodecErrorCount,
                DisconnectReason,
                TransportMode,
                TransportId,
                LocalPeerId,
                OwnedEntityId,
                ConnectedPeerCount,
                OwnershipSummary,
                PredictionLeadFrames,
                AverageRttFrames,
                MaxRttFrames,
                AverageSnapshotIntervalFrames,
                MaxSnapshotIntervalFrames,
                FrameDriftFrames,
                AverageFrameDriftFrames,
                MaxFrameDriftFrames,
                PredictionHistoryCount,
                ComparableSnapshotHashCount,
                MissingSnapshotHashCount,
                SnapshotRequestCount,
                AuthoritativeCommandSentCount,
                AuthoritativeCommandReceivedCount,
                AuthoritativeCommandEchoCount,
                AuthoritativeCommandEnqueuedCount,
                AuthoritativeCommandLateCount,
                AuthoritativeCommandDroppedCount,
                FullSnapshotSentCount,
                DigestSnapshotSentCount,
                FullSnapshotReceivedCount,
                DigestSnapshotReceivedCount,
                SentBytes,
                DeliveredBytes,
                LastSentPayloadBytes,
                MaxSentPayloadBytes,
                RuntimeCommandSentBytes,
                SnapshotSentBytes,
                SnapshotRequestSentBytes,
                HeartbeatSentBytes,
                roomSentBytes,
                roomStatus,
                roomId,
                roomMembers,
                roomReadyCount,
                roomConnectedCount,
                roomStateVersion,
                roomReason,
                RecentDiagnostics,
                StateSyncPolicy,
                HeartbeatIntervalFrames,
                RollbackHistoryCount,
                RollbackHistoryDropCount,
                RollbackCount,
                RollbackFailureCount,
                RollbackSnapshotCorrectionCount,
                RollbackReplayedFrameCount,
                MaxRollbackFrames,
                LastRollbackFrame,
                LastRollbackTargetFrame,
                LastRollbackFailure);
        }

        public static CharacterTestNetworkLoopbackDiagnostics Unavailable =>
            new CharacterTestNetworkLoopbackDiagnostics(
                available: false,
                serverStatus: NetworkSessionStatus.Disconnected,
                clientStatus: NetworkSessionStatus.Disconnected,
                serverFrame: RuntimeFrame.Zero,
                clientLastServerFrame: RuntimeFrame.Zero,
                lastAckedInputFrame: RuntimeFrame.Zero,
                rttEstimateFrames: 0,
                pendingRuntimeCommands: 0,
                pendingTransportMessages: 0,
                lastSnapshotHash: 0L,
                resyncCount: 0,
                snapshotApplyFailureCount: 0);
    }
}
