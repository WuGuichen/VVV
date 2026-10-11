using System;
using System.Text;
using MxFramework.Audio;
using MxFramework.Network;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestUnityTransportRoomServerRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkLobbyRunner,
        ICharacterTestNetworkReplayRunner
    {
        private readonly CharacterTestUnityTransportServerRunner _gameplayRunner;
        private readonly bool _autoStartWhenReady;
        private readonly bool _disposeTransport;
        private RuntimeFrame _roomFrame = RuntimeFrame.Zero;
        private bool _gameplayStarted;
        private bool _disposed;

        public CharacterTestUnityTransportRoomServerRunner(
            INetworkTransport transport,
            NetworkRoomConfig roomConfig,
            bool autoStartWhenReady,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices resources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            string serverPeerId = "server",
            string transportMode = "unity-transport-room-server",
            bool createServerViews = true,
            bool disposeTransport = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            RoomConfig = CharacterTestSessionHashContract.Normalize(roomConfig);
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            TransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-room-server" : transportMode;
            _autoStartWhenReady = autoStartWhenReady;
            _disposeTransport = disposeTransport;

            RoomSession = new NetworkRoomSessionRunner(
                NetworkSessionRole.Server,
                ServerPeerId,
                ServerPeerId,
                RoomConfig,
                Transport,
                logger);
            RoomSession.OpenRoom(RuntimeFrame.Zero);

            _gameplayRunner = new CharacterTestUnityTransportServerRunner(
                Transport,
                logger,
                storyContent,
                resources,
                baseResourcePreloadGroupId,
                initialCameraMode,
                actionConfigOptions,
                physicsRuntime,
                RoomConfig.GameplaySessionConfig,
                sessionId: RoomConfig.RoomId + "-game",
                serverPeerId: ServerPeerId,
                transportMode: TransportMode,
                createServerViews: createServerViews,
                enableLocalInputCommands: false,
                disposeTransport: false,
                audioService: audioService,
                audioCueCatalog: audioCueCatalog,
                disposeAudioService: disposeAudioService,
                runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                actionCatalog: actionCatalog);
            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
        }

        public GameSlice Slice => _gameplayRunner.Slice;
        public INetworkTransport Transport { get; }
        public NetworkRoomConfig RoomConfig { get; }
        public NetworkRoomSessionRunner RoomSession { get; }
        public NetworkSessionRunner GameplaySession => _gameplayRunner.Session;
        internal CharacterTestUnityTransportServerRunner GameplayRunner => _gameplayRunner;
        public NetworkSessionConfig Config => _gameplayRunner.Config;
        public string ServerPeerId { get; }
        public string TransportMode { get; }
        public bool IsRoomHost => true;
        public bool LocalRoomReady => false;
        public bool CanSetRoomReady => false;
        public bool IsReplayRecording => _gameplayRunner.IsReplayRecording;
        public bool CanStartRoomGame
        {
            get
            {
                ThrowIfDisposed();
                return RoomSession.CanStartGame(out _);
            }
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

        internal void PublishHostReplayFrame() => _gameplayRunner.PublishHostReplayFrame();

        private void TickCore(double deltaTime, bool deferReplayPublication)
        {
            ThrowIfDisposed();
            Slice.ThrowIfAuthorityUnavailable();

            if (!_gameplayStarted)
            {
                RoomSession.ProcessIncoming(_roomFrame);
                if (_autoStartWhenReady
                    && RoomSession.State.ConnectedMemberCount >= RoomConfig.MinReadyMembers
                    && RoomSession.State.ReadyMemberCount >= RoomConfig.MinReadyMembers)
                {
                    RoomSession.TryStartGame(_roomFrame, out _);
                }
                _gameplayStarted = RoomSession.IsInGame;
                AdvanceRoomFrame();
                if (!_gameplayStarted)
                    return;
            }

            if (deferReplayPublication) _gameplayRunner.TickForHost(deltaTime);
            else _gameplayRunner.Tick(deltaTime);
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return _gameplayRunner.CaptureDiagnostics().WithRoom(
                RoomSession.Status,
                RoomConfig.RoomId,
                FormatMembers(RoomSession.State),
                RoomSession.State.ReadyMemberCount,
                RoomSession.State.ConnectedMemberCount,
                RoomSession.State.StateVersion,
                RoomSession.LastResponseReason,
                Transport.CreateSnapshot(ServerPeerId).RoomSentBytes);
        }

        public CharacterTestReplayPlaybackStatus BeginReplayRecording()
        {
            ThrowIfDisposed();
            return _gameplayRunner.BeginReplayRecording();
        }

        public CharacterTestReplayCapture CreateReplayCapture()
        {
            ThrowIfDisposed();
            return _gameplayRunner.CreateReplayCapture();
        }

        public bool TrySetLocalRoomReady(bool ready, out string reason)
        {
            ThrowIfDisposed();
            reason = "server-only room runner has no local room member";
            return false;
        }

        public bool TryStartRoomGame(out string reason)
        {
            ThrowIfDisposed();
            bool started = RoomSession.TryStartGame(_roomFrame, out reason);
            if (started)
                _gameplayStarted = true;
            return started;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            _gameplayRunner?.Dispose();
            if (_disposeTransport)
                DisposeTransport(Transport);
            _disposed = true;
        }

        private void AdvanceRoomFrame()
        {
            _roomFrame = _roomFrame.Next();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportRoomServerRunner));
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

        internal static string FormatMembers(NetworkRoomState state)
        {
            if (state == null || state.Members.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < state.Members.Count; i++)
            {
                NetworkRoomMember member = state.Members[i];
                if (i > 0)
                    builder.Append(',');
                builder.Append(member.SlotIndex)
                    .Append(':')
                    .Append(member.PeerId)
                    .Append(member.Ready ? "[ready]" : "[wait]")
                    .Append(member.Connected ? "[on]" : "[off]");
            }

            return builder.ToString();
        }
    }

    public sealed class CharacterTestUnityTransportRoomClientRunner :
        ICharacterTestNetworkRunner,
        ICharacterTestNetworkLobbyRunner,
        ICharacterTestRuntimeAiPlannerSessionAuthorityClient
    {
        private readonly CharacterTestUnityTransportClientRunner _gameplayRunner;
        private readonly string _displayName;
        private readonly bool _autoReady;
        private readonly bool _disposeTransport;
        private RuntimeFrame _roomFrame = RuntimeFrame.Zero;
        private bool _joinSent;
        private bool _readySent;
        private bool _gameplayStarted;
        private bool _disposed;

        public CharacterTestUnityTransportRoomClientRunner(
            INetworkTransport transport,
            NetworkRoomConfig roomConfig,
            string displayName,
            bool autoReady,
            IRuntimeLogger logger = null,
            CharacterTestStoryContent storyContent = null,
            GlobalResourceRuntimeServices resources = null,
            string baseResourcePreloadGroupId = "",
            CharacterTestCameraMode initialCameraMode = CharacterTestCameraMode.ThirdPersonControlled,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions = null,
            CharacterTestCharacterPhysicsRuntime physicsRuntime = null,
            string serverPeerId = "server",
            string clientPeerId = "client-0",
            string transportMode = "unity-transport-room-client",
            bool createClientViews = true,
            string connectTargetLabel = "",
            bool disposeTransport = true,
            IAudioService audioService = null,
            ICharacterTestAudioCueCatalog audioCueCatalog = null,
            bool disposeAudioService = false,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null,
            CharacterTestCharacterActionCatalog actionCatalog = null)
        {
            Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            RoomConfig = CharacterTestSessionHashContract.Normalize(roomConfig);
            ServerPeerId = string.IsNullOrEmpty(serverPeerId) ? "server" : serverPeerId;
            ClientPeerId = string.IsNullOrEmpty(clientPeerId) ? "client-0" : clientPeerId;
            TransportMode = string.IsNullOrEmpty(transportMode) ? "unity-transport-room-client" : transportMode;
            _displayName = string.IsNullOrWhiteSpace(displayName) ? ClientPeerId : displayName;
            _autoReady = autoReady;
            _disposeTransport = disposeTransport;

            RoomSession = new NetworkRoomSessionRunner(
                NetworkSessionRole.Client,
                ClientPeerId,
                ServerPeerId,
                RoomConfig,
                Transport,
                logger);

            _gameplayRunner = new CharacterTestUnityTransportClientRunner(
                Transport,
                logger,
                storyContent,
                resources,
                baseResourcePreloadGroupId,
                initialCameraMode,
                actionConfigOptions,
                physicsRuntime,
                RoomConfig.GameplaySessionConfig,
                sessionId: RoomConfig.RoomId + "-game",
                serverPeerId: ServerPeerId,
                clientPeerId: ClientPeerId,
                transportMode: TransportMode,
                createClientViews: createClientViews,
                connectTargetLabel: connectTargetLabel,
                disposeTransport: false,
                connectOnCreate: false,
                deferLocalInputUntilConnected: true,
                audioService: audioService,
                audioCueCatalog: audioCueCatalog,
                disposeAudioService: disposeAudioService,
                runtimeAiPlannerProfile: runtimeAiPlannerProfile,
                runtimeAiPlannerProfileReport: runtimeAiPlannerProfileReport,
                actionCatalog: actionCatalog);
            Slice.Services.Network.SetNetworkDiagnosticsProvider(CaptureDiagnostics);
        }

        public GameSlice Slice => _gameplayRunner.Slice;
        public INetworkTransport Transport { get; }
        public NetworkRoomConfig RoomConfig { get; }
        public NetworkRoomSessionRunner RoomSession { get; }
        public NetworkSessionRunner GameplaySession => _gameplayRunner.Session;
        internal CharacterTestUnityTransportClientRunner GameplayRunner => _gameplayRunner;
        public NetworkSessionConfig Config => _gameplayRunner.Config;
        public string ServerPeerId { get; }
        public string ClientPeerId { get; }
        public string TransportMode { get; }
        public bool IsRoomHost => false;
        public bool LocalRoomReady
        {
            get
            {
                ThrowIfDisposed();
                NetworkRoomMember member = RoomSession.State.FindMember(ClientPeerId);
                return member != null && member.Ready;
            }
        }

        public bool CanSetRoomReady
        {
            get
            {
                ThrowIfDisposed();
                return CanSetLocalRoomReady(out _);
            }
        }

        public bool CanStartRoomGame => false;

        void ICharacterTestRuntimeAiPlannerSessionAuthorityClient.SubmitRuntimeAiPlannerSessionDeckRequest(
            long requestId,
            CharacterTestRuntimeAiPlannerSessionDeckRequest request)
        {
            ((ICharacterTestRuntimeAiPlannerSessionAuthorityClient)_gameplayRunner)
                .SubmitRuntimeAiPlannerSessionDeckRequest(requestId, request);
        }

        /// <remarks>LowFreqAlloc at the transport boundary; steady-state framework bookkeeping avoids temporary collections with stable inputs.</remarks>
        public void Tick(double deltaTime)
        {
            ThrowIfDisposed();

            if (!_gameplayStarted)
            {
                if (!_joinSent)
                {
                    RoomSession.JoinRoom(_roomFrame, _displayName);
                    _joinSent = true;
                }

                RoomSession.ProcessIncoming(_roomFrame);
                TryAutoReady();
                _gameplayStarted = RoomSession.IsInGame;
                if (_gameplayStarted)
                    _gameplayRunner.BeginConnect(RuntimeFrame.Zero);

                AdvanceRoomFrame();
                if (!_gameplayStarted)
                    return;
            }

            _gameplayRunner.Tick(deltaTime);
        }

        public CharacterTestNetworkLoopbackDiagnostics CaptureDiagnostics()
        {
            ThrowIfDisposed();
            return _gameplayRunner.CaptureDiagnostics().WithRoom(
                RoomSession.Status,
                RoomConfig.RoomId,
                CharacterTestUnityTransportRoomServerRunner.FormatMembers(RoomSession.State),
                RoomSession.State.ReadyMemberCount,
                RoomSession.State.ConnectedMemberCount,
                RoomSession.State.StateVersion,
                RoomSession.LastResponseReason,
                Transport.CreateSnapshot(ClientPeerId).RoomSentBytes);
        }

        public bool TrySetLocalRoomReady(bool ready, out string reason)
        {
            ThrowIfDisposed();
            if (!CanSetLocalRoomReady(out reason))
                return false;

            if (LocalRoomReady == ready)
            {
                reason = string.Empty;
                return true;
            }

            RoomSession.SetReady(_roomFrame, ready);
            _readySent = ready;
            reason = string.Empty;
            return true;
        }

        public bool TryStartRoomGame(out string reason)
        {
            ThrowIfDisposed();
            reason = "only host can start room gameplay";
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Slice?.Services.Network.SetNetworkDiagnosticsProvider(null);
            _gameplayRunner?.Dispose();
            if (_disposeTransport)
                DisposeTransport(Transport);
            _disposed = true;
        }

        private void TryAutoReady()
        {
            if (!_autoReady || _readySent || RoomSession.Status != NetworkRoomStatus.Open)
                return;

            NetworkRoomMember member = RoomSession.State.FindMember(ClientPeerId);
            if (member == null || !member.Connected || member.Ready)
                return;

            RoomSession.SetReady(_roomFrame, true);
            _readySent = true;
        }

        private bool CanSetLocalRoomReady(out string reason)
        {
            if (_gameplayStarted || RoomSession.Status == NetworkRoomStatus.InGame)
            {
                reason = "room is already in game";
                return false;
            }

            if (RoomSession.Status != NetworkRoomStatus.Open)
            {
                reason = "room is not open yet";
                return false;
            }

            NetworkRoomMember member = RoomSession.State.FindMember(ClientPeerId);
            if (member == null || !member.Connected)
            {
                reason = "local room member is not connected yet";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void AdvanceRoomFrame()
        {
            _roomFrame = _roomFrame.Next();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestUnityTransportRoomClientRunner));
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
    }
}
