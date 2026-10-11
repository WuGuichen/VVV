using System;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestNetworkCoordinator :
        ICharacterTestNetworkSessionController,
        ICharacterTestNetworkLobbyController,
        ICharacterTestNetworkDiagnosticController,
        ICharacterTestNetworkReplayController,
        ICharacterTestNetworkAutomationController
    {
        private readonly CharacterTestRuntimeContext _context;
        private readonly CharacterTestSceneProfile _profile;
        private readonly CharacterTestRuntimeSessionBuilder _sessionBuilder;

        public CharacterTestNetworkCoordinator(
            CharacterTestRuntimeContext context,
            CharacterTestSceneProfile profile,
            CharacterTestRuntimeSessionBuilder sessionBuilder)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _profile = profile ?? CharacterTestSceneProfile.LoadDefault();
            _sessionBuilder = sessionBuilder ?? throw new ArgumentNullException(nameof(sessionBuilder));
        }

        public CharacterTestNetworkSessionOptions CreateDefaultNetworkSessionOptions(
            CharacterTestNetworkSessionKind kind)
        {
            string clientPeerId = ResolveRuntimeClientPeerId();
            CharacterTestNetworkSessionOptions options;
            switch (kind)
            {
                case CharacterTestNetworkSessionKind.Host:
                    options = CharacterTestNetworkSessionOptions.HostDefaults(clientPeerId: clientPeerId);
                    break;
                case CharacterTestNetworkSessionKind.Join:
                    options = CharacterTestNetworkSessionOptions.JoinDefaults(clientPeerId: clientPeerId);
                    break;
                default:
                    options = new CharacterTestNetworkSessionOptions
                    {
                        Kind = CharacterTestNetworkSessionKind.Offline,
                        ClientPeerId = clientPeerId
                    };
                    break;
            }

            options.InputDelayFrames = Mathf.Max(0, _profile.NetworkInputDelayFrames);
            options.SnapshotIntervalFrames = Mathf.Max(1, _profile.NetworkSnapshotIntervalFrames);
            options.StateSyncPolicy = NetworkStateSyncPolicy.BootstrapOnly;
            options.HeartbeatIntervalFrames = Mathf.Max(1, _profile.NetworkHeartbeatIntervalFrames);
            ApplyNetworkRoomDefaults(options);
            return options;
        }

        public CharacterTestNetworkSessionResult StartNetworkSession(
            CharacterTestNetworkSessionOptions options)
        {
            options = options?.Clone() ?? CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Join);
            CharacterTestNetworkEndpoint.ApplyEndpointOverrides(options);
            _context.NetworkSyncProbeAutoRunEnabled =
                CharacterTestNetworkPanelPreferences.LoadAutoSyncProbeAfterEnterGame(
                    _context.NetworkSyncProbeAutoRunEnabled);
            if (options.Kind != CharacterTestNetworkSessionKind.Host
                && options.Kind != CharacterTestNetworkSessionKind.Join)
            {
                return CharacterTestNetworkSessionResult.Failed(
                    options.Kind,
                    CharacterTestNetworkMode.Disabled,
                    "CharacterTest network session start requires Host or Join.");
            }

            CharacterTestNetworkMode networkMode =
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(options.Kind, options.LaunchMode);
            bool preserveNetworkPanel = IsNetworkPanelOpen();
            try
            {
                string endpoint = options.Kind == CharacterTestNetworkSessionKind.Host
                    ? NormalizeBindAddress(options.BindAddress) + ":" + NormalizePort(
                        options.Port,
                        CharacterTestNetworkSessionOptions.DefaultPort)
                    : NormalizeRemoteAddress(options.RemoteAddress) + ":" + NormalizePort(
                        options.Port,
                        CharacterTestNetworkSessionOptions.DefaultPort);
                string message = CreateNetworkSessionStartMessage(options, endpoint);
                _sessionBuilder.RebuildRuntimeSession(
                    options.Kind,
                    networkMode,
                    options,
                    message,
                    preserveNetworkPanel);
                CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
                return CharacterTestNetworkSessionResult.Succeeded(
                    options.Kind,
                    networkMode,
                    _context.LastNetworkSessionMessage,
                    status);
            }
            catch (Exception exception)
            {
                string message = "CharacterTest network session start failed: " + exception.Message;
                _context.Logger?.Warning("GameManager", message);
                try
                {
                    _sessionBuilder.RebuildRuntimeSession(
                        CharacterTestNetworkSessionKind.Offline,
                        CharacterTestNetworkMode.Disabled,
                        null,
                        "Offline after network start failure.",
                        preserveNetworkPanel);
                }
                catch (Exception fallbackException)
                {
                    message += " Offline fallback failed: " + fallbackException.Message;
                    _context.Logger?.Warning("GameManager", message);
                }

                return CharacterTestNetworkSessionResult.Failed(
                    options.Kind,
                    networkMode,
                    message,
                    CaptureNetworkSessionStatus());
            }
        }

        public CharacterTestNetworkSessionResult StopNetworkSession()
        {
            bool preserveNetworkPanel = IsNetworkPanelOpen();
            try
            {
                _sessionBuilder.RebuildRuntimeSession(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    null,
                    "Offline",
                    preserveNetworkPanel);
                CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
                return CharacterTestNetworkSessionResult.Succeeded(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    _context.LastNetworkSessionMessage,
                    status);
            }
            catch (Exception exception)
            {
                string message = "CharacterTest network session stop failed: " + exception.Message;
                _context.Logger?.Warning("GameManager", message);
                return CharacterTestNetworkSessionResult.Failed(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    message,
                    CaptureNetworkSessionStatus());
            }
        }

        public CharacterTestNetworkSessionResult SetLocalReady(bool ready)
        {
            if (_context.NetworkRunner == null)
            {
                return CharacterTestNetworkSessionResult.Failed(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    "No active network room session.",
                    CaptureNetworkSessionStatus());
            }

            var lobby = _context.NetworkRunner as ICharacterTestNetworkLobbyRunner;
            if (lobby == null)
            {
                CharacterTestNetworkSessionStatus unsupportedStatus = CaptureNetworkSessionStatus();
                return CharacterTestNetworkSessionResult.Failed(
                    unsupportedStatus.Kind,
                    unsupportedStatus.NetworkMode,
                    "Ready is only available in LAN Room mode.",
                    unsupportedStatus);
            }

            bool success = lobby.TrySetLocalRoomReady(ready, out string reason);
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            string message = success
                ? (ready ? "Local room member marked ready." : "Local room member marked not ready.")
                : "Ready change failed: " + (string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason);
            _context.LastNetworkSessionMessage = message;
            status.Message = message;
            return success
                ? CharacterTestNetworkSessionResult.Succeeded(status.Kind, status.NetworkMode, message, status)
                : CharacterTestNetworkSessionResult.Failed(status.Kind, status.NetworkMode, message, status);
        }

        public CharacterTestNetworkSessionResult StartRoomGame()
        {
            if (_context.NetworkRunner == null)
            {
                return CharacterTestNetworkSessionResult.Failed(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    "No active network room session.",
                    CaptureNetworkSessionStatus());
            }

            var lobby = _context.NetworkRunner as ICharacterTestNetworkLobbyRunner;
            if (lobby == null)
            {
                CharacterTestNetworkSessionStatus unsupportedStatus = CaptureNetworkSessionStatus();
                return CharacterTestNetworkSessionResult.Failed(
                    unsupportedStatus.Kind,
                    unsupportedStatus.NetworkMode,
                    "Start is only available in LAN Room mode.",
                    unsupportedStatus);
            }

            bool success = lobby.TryStartRoomGame(out string reason);
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            string message = success
                ? "Room gameplay start requested."
                : "Room start failed: " + (string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason);
            _context.LastNetworkSessionMessage = message;
            status.Message = message;
            return success
                ? CharacterTestNetworkSessionResult.Succeeded(status.Kind, status.NetworkMode, message, status)
                : CharacterTestNetworkSessionResult.Failed(status.Kind, status.NetworkMode, message, status);
        }

        public CharacterTestNetworkSessionResult EnterNetworkGameplay()
        {
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            if (status.LaunchMode == CharacterTestNetworkLaunchMode.Room)
                return RequestRoomNetworkGameplayEnter(status);

            if (!status.CanEnterGameplay)
            {
                string message = string.IsNullOrWhiteSpace(status.ReadinessMessage)
                    ? "Network gameplay is not ready to enter."
                    : status.ReadinessMessage;
                return CharacterTestNetworkSessionResult.Failed(
                    status.Kind,
                    status.NetworkMode,
                    message,
                    status);
            }

            return CompleteNetworkGameplayEnter("Network gameplay entered.");
        }

        public void SetNetworkSyncProbeAutoRun(bool enabled)
        {
            _context.NetworkSyncProbeAutoRunEnabled = enabled;
            if (!enabled)
                _context.NetworkSyncProbeAutoRunPending = false;
            _context.Logger?.Info(
                "GameManager",
                enabled
                    ? "Auto Sync Probe after Enter Game enabled."
                    : "Auto Sync Probe after Enter Game disabled.");
        }

        public CharacterTestNetworkSessionStatus CaptureNetworkSessionStatus()
        {
            CharacterTestNetworkSessionStatus status = _context.ActiveNetworkSessionOptions != null
                ? CreateStatusFromOptions(_context.ActiveNetworkSessionKind, _context.ActiveNetworkSessionOptions)
                : CharacterTestNetworkSessionStatus.Offline(_context.LastNetworkSessionMessage);
            status.Message = _context.LastNetworkSessionMessage;
            status.NetworkTickRate = Mathf.Max(1, _profile.TargetFrameRate);
            status.NetworkMode = _context.NetworkRunner != null
                ? CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    _context.ActiveNetworkSessionKind,
                    status.LaunchMode)
                : CharacterTestNetworkMode.Disabled;

            if (_context.NetworkRunner == null || _context.Slice == null)
            {
                status.Kind = CharacterTestNetworkSessionKind.Offline;
                status.NetworkMode = CharacterTestNetworkMode.Disabled;
                PopulateNetworkReadinessStatus(status);
                return status;
            }

            CharacterTestNetworkLoopbackDiagnostics diagnostics = _context.NetworkRunner.CaptureDiagnostics();
            if (diagnostics.Available)
            {
                status.TransportMode = diagnostics.TransportMode;
                status.TransportId = diagnostics.TransportId;
                status.ServerStatus = diagnostics.ServerStatus;
                status.ClientStatus = diagnostics.ClientStatus;
                status.OwnedEntityId = diagnostics.OwnedEntityId;
                status.ServerFrame = diagnostics.ServerFrame;
                status.ClientFrame = diagnostics.ClientLastServerFrame;
                status.RttEstimateFrames = diagnostics.RttEstimateFrames;
                status.DisconnectReason = diagnostics.DisconnectReason;
                status.RoomStatus = diagnostics.RoomStatus;
                status.RoomId = EmptyDiagnosticsAs(diagnostics.RoomId, status.RoomId);
                status.RoomMembers = EmptyDiagnosticsAs(diagnostics.RoomMembers, status.RoomMembers);
                status.RoomReadyCount = diagnostics.RoomReadyCount;
                status.RoomConnectedCount = diagnostics.RoomConnectedCount;
                status.RoomStateVersion = diagnostics.RoomStateVersion;
                status.RoomReason = diagnostics.RoomReason;
                status.AverageRttFrames = diagnostics.AverageRttFrames;
                status.FrameDriftFrames = diagnostics.FrameDriftFrames;
                status.AverageFrameDriftFrames = diagnostics.AverageFrameDriftFrames;
                status.MaxFrameDriftFrames = diagnostics.MaxFrameDriftFrames;
                status.ResyncCount = diagnostics.ResyncCount;
                status.SnapshotMismatchCount = diagnostics.SnapshotMismatchCount;
                status.SnapshotRequestCount = diagnostics.SnapshotRequestCount;
                status.AuthoritativeCommandSentCount = diagnostics.AuthoritativeCommandSentCount;
                status.AuthoritativeCommandReceivedCount = diagnostics.AuthoritativeCommandReceivedCount;
                status.AuthoritativeCommandEchoCount = diagnostics.AuthoritativeCommandEchoCount;
                status.AuthoritativeCommandEnqueuedCount = diagnostics.AuthoritativeCommandEnqueuedCount;
                status.AuthoritativeCommandLateCount = diagnostics.AuthoritativeCommandLateCount;
                status.AuthoritativeCommandDroppedCount = diagnostics.AuthoritativeCommandDroppedCount;
                status.FullSnapshotSentCount = diagnostics.FullSnapshotSentCount;
                status.DigestSnapshotSentCount = diagnostics.DigestSnapshotSentCount;
                status.FullSnapshotReceivedCount = diagnostics.FullSnapshotReceivedCount;
                status.DigestSnapshotReceivedCount = diagnostics.DigestSnapshotReceivedCount;
                status.StateSyncPolicy = diagnostics.StateSyncPolicy;
                status.HeartbeatIntervalFrames = diagnostics.HeartbeatIntervalFrames;
                status.PendingRuntimeCommands = diagnostics.PendingRuntimeCommands;
                status.PendingTransportMessages = diagnostics.PendingTransportMessages;
                status.SentBytes = diagnostics.SentBytes;
                status.DeliveredBytes = diagnostics.DeliveredBytes;
                status.LastSentPayloadBytes = diagnostics.LastSentPayloadBytes;
                status.MaxSentPayloadBytes = diagnostics.MaxSentPayloadBytes;
                status.RuntimeCommandSentBytes = diagnostics.RuntimeCommandSentBytes;
                status.SnapshotSentBytes = diagnostics.SnapshotSentBytes;
                status.SnapshotRequestSentBytes = diagnostics.SnapshotRequestSentBytes;
                status.HeartbeatSentBytes = diagnostics.HeartbeatSentBytes;
                status.RoomSentBytes = diagnostics.RoomSentBytes;
                status.RecentDiagnostics = diagnostics.RecentDiagnostics;
            }

            PopulateNetworkReadinessStatus(status);
            return status;
        }

        internal CharacterTestNetworkSessionStatus CaptureCombatHudNetworkSessionStatus()
        {
            if (_context.NetworkRunner == null)
                return null;

            return CaptureNetworkSessionStatus();
        }

        public CharacterTestNetworkSyncProbeStatus StartNetworkSyncProbe()
        {
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            CharacterTestNetworkLoopbackDiagnostics diagnostics = _context.NetworkRunner != null
                ? _context.NetworkRunner.CaptureDiagnostics()
                : CharacterTestNetworkLoopbackDiagnostics.Unavailable;
            CharacterTestNetworkSyncProbeStatus probeStatus =
                _context.NetworkSyncProbe.Start(_context.Slice, diagnostics, status);
            if (probeStatus != null && probeStatus.Result == CharacterTestNetworkSyncProbeResult.Running)
            {
                InstallNetworkSyncProbeInputProvider();
                BeginNetworkReplayRecording();
                _context.NetworkSyncProbeAutoRunPending = false;
                if (_context.NetworkSyncProbeAutoRunEnabled)
                    _context.NetworkSyncProbeAutoRunConsumed = true;
                _context.NetworkReplayValidationPending = true;
            }
            _context.LastNetworkSessionMessage = probeStatus.Summary;
            _context.Logger?.Info("GameManager", probeStatus.Summary);
            return probeStatus;
        }

        public CharacterTestNetworkSyncProbeStatus StopNetworkSyncProbe()
        {
            CharacterTestNetworkSyncProbeStatus probeStatus = _context.NetworkSyncProbe.Stop(cancelled: true);
            RestoreNetworkSyncProbeInputProvider();
            _context.LastNetworkSessionMessage = probeStatus.Summary;
            _context.Logger?.Info("GameManager", probeStatus.Summary);
            return probeStatus;
        }

        public CharacterTestNetworkSyncProbeStatus CaptureNetworkSyncProbeStatus()
        {
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            bool canStart = status != null
                && status.IsNetworkActive
                && status.CanEnterGameplay;
            return _context.NetworkSyncProbe.CaptureStatus(
                canStart,
                _context.NetworkSyncProbeAutoRunEnabled,
                _context.NetworkSyncProbeAutoRunPending);
        }

        public CharacterTestReplayPlaybackStatus BeginNetworkReplayRecording()
        {
            var replayRunner = _context.NetworkRunner as ICharacterTestNetworkReplayRunner;
            if (replayRunner == null)
            {
                _context.NetworkReplayStatus =
                    CharacterTestReplayPlaybackStatus.Unavailable("Active CharacterTest network runner does not expose replay.");
                return _context.NetworkReplayStatus;
            }

            _context.NetworkReplayStatus = replayRunner.BeginReplayRecording();
            _context.Logger?.Info("GameManager", _context.NetworkReplayStatus.Summary);
            return _context.NetworkReplayStatus;
        }

        public CharacterTestReplayPlaybackStatus ValidateNetworkReplay()
        {
            var replayRunner = _context.NetworkRunner as ICharacterTestNetworkReplayRunner;
            if (replayRunner == null)
            {
                _context.NetworkReplayStatus =
                    CharacterTestReplayPlaybackStatus.Unavailable("Active CharacterTest network runner does not expose replay.");
                return _context.NetworkReplayStatus;
            }

            CharacterTestReplayCapture capture = replayRunner.CreateReplayCapture();
            _context.NetworkReplayStatus = CharacterTestReplayPlayback.Validate(capture);
            _context.Logger?.Log(
                _context.NetworkReplayStatus.Success ? RuntimeLogLevel.Info : RuntimeLogLevel.Warning,
                "CharacterTest.Replay",
                _context.NetworkReplayStatus.Summary);
            return _context.NetworkReplayStatus;
        }

        public CharacterTestReplayPlaybackStatus CaptureNetworkReplayStatus()
        {
            return _context.NetworkReplayStatus;
        }

        /// <summary>Freezes the currently recorded CharacterTest replay for Runtime AI Lab validation.</summary>
        /// <remarks>AllocByDesign: capture creation is an explicit low-frequency Lab command.</remarks>
        internal bool TryCreateRuntimeAiLabReplayCapture(
            out CharacterTestReplayCapture capture,
            out CharacterTestReplayPlaybackStatus status)
        {
            capture = null;
            var replayRunner = _context.NetworkRunner as ICharacterTestNetworkReplayRunner;
            if (replayRunner == null)
            {
                status = CharacterTestReplayPlaybackStatus.Unavailable(
                    "Active CharacterTest session does not expose replay recording.");
                _context.NetworkReplayStatus = status;
                return false;
            }

            try
            {
                capture = replayRunner.CreateReplayCapture();
                if (capture == null || capture.FrameCount == 0)
                {
                    status = CharacterTestReplayPlaybackStatus.Unavailable(
                        "Replay recording has no captured RuntimeFrame records yet.");
                    _context.NetworkReplayStatus = status;
                    return false;
                }

                status = new CharacterTestReplayPlaybackStatus
                {
                    Available = true,
                    Recording = replayRunner.IsReplayRecording,
                    ResultCode = "CAPTURE_READY",
                    Summary = "CharacterTest replay capture is ready.",
                    FrameCount = capture.FrameCount,
                    AuthoritativeCommandCount = capture.AuthoritativeCommandCount
                };
                _context.NetworkReplayStatus = status;
                return true;
            }
            catch (Exception exception)
            {
                status = CharacterTestReplayPlaybackStatus.Unavailable(
                    "Replay capture failed: " + exception.Message);
                _context.NetworkReplayStatus = status;
                return false;
            }
        }

        public void ConfigureNetworkAutomationRunner()
        {
            if (!CharacterTestNetworkAutomationReportStore.TryLoadRequest(
                    out CharacterTestNetworkAutomationRequest request))
            {
                _context.NetworkAutomationRunner = null;
                return;
            }

            _context.NetworkAutomationRunner = new CharacterTestNetworkAutomationRunner(
                request,
                this,
                new CharacterTestNetworkAutomationPlayerContext(),
                _context.Logger);
            _context.Logger?.Info(
                "GameManager",
                "CharacterTest network automation armed. runId=" + _context.NetworkAutomationRunner.RunId);
        }

        public bool ShouldTickNetworkWhileNetworkPanelOpen()
        {
            return _context.NetworkRunner != null
                && _context.UiRuntime != null
                && _context.UiRuntime.IsNetworkPanelOpen;
        }

        public bool ShouldTickNetworkAutomation()
        {
            return _context.NetworkAutomationRunner != null && _context.NetworkAutomationRunner.IsActive;
        }

        public bool TryAutoEnterNetworkGameplayFromPanel()
        {
            if (_context.NetworkRunner == null
                || _context.UiRuntime == null
                || _context.NetworkGameplayEntered
                || !_context.UiRuntime.IsNetworkPanelOpen)
            {
                return false;
            }

            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            if (status.LaunchMode != CharacterTestNetworkLaunchMode.Room
                || status.RoomStatus != NetworkRoomStatus.InGame
                || !status.CanEnterGameplay)
            {
                return false;
            }

            CompleteNetworkGameplayEnter("Network gameplay entered.");
            return true;
        }

        public void TickNetworkSyncProbe(double deltaTime)
        {
            if (_context.NetworkRunner == null || _context.Slice == null)
                return;

            TryStartPendingNetworkSyncProbeAutoRun();
            bool wasRunning = _context.NetworkSyncProbe
                .CaptureStatus(
                    canStart: true,
                    autoRunEnabled: _context.NetworkSyncProbeAutoRunEnabled,
                    autoRunPending: _context.NetworkSyncProbeAutoRunPending)
                .IsRunning;
            _context.NetworkSyncProbe.Tick(
                deltaTime,
                _context.Slice,
                _context.NetworkRunner.CaptureDiagnostics(),
                _context.Logger,
                submitProbeCommands: false);
            CharacterTestNetworkSyncProbeStatus probeStatus = _context.NetworkSyncProbe.CaptureStatus(
                canStart: true,
                autoRunEnabled: _context.NetworkSyncProbeAutoRunEnabled,
                autoRunPending: _context.NetworkSyncProbeAutoRunPending);
            RestoreNetworkSyncProbeInputProviderIfInactive();
            if (_context.NetworkReplayValidationPending
                && wasRunning
                && probeStatus != null
                && !probeStatus.IsRunning
                && probeStatus.Result != CharacterTestNetworkSyncProbeResult.Idle
                && probeStatus.Result != CharacterTestNetworkSyncProbeResult.Rejected)
            {
                _context.NetworkReplayValidationPending = false;
                ValidateNetworkReplay();
            }
        }

        public void TickAutomation()
        {
            _context.NetworkAutomationRunner?.Tick();
        }

        public void ResetNetworkSyncProbeAutoRunSessionState()
        {
            RestoreNetworkSyncProbeInputProvider();
            _context.NetworkSyncProbeAutoRunPending = false;
            _context.NetworkSyncProbeAutoRunConsumed = false;
            _context.NetworkReplayValidationPending = false;
            _context.NetworkReplayStatus =
                CharacterTestReplayPlaybackStatus.Unavailable("CharacterTest replay has not run.");
        }

        public void UpdateNetworkSyncProbeInputProviderBeforeNetworkTick()
        {
            if (!_context.NetworkSyncProbeInputProviderActive
                || _context.NetworkSyncProbeInputProvider == null)
                return;

            if (_context.NetworkSyncProbe.TryCreateFixedInputSnapshot(out InputSnapshot snapshot))
            {
                _context.NetworkSyncProbeInputProvider.SetSnapshot(snapshot);
                return;
            }

            RestoreNetworkSyncProbeInputProvider();
        }

        public void RestoreNetworkSyncProbeInputProviderIfInactive()
        {
            if (_context.NetworkSyncProbeInputProviderActive && !_context.NetworkSyncProbe.IsRunning)
                RestoreNetworkSyncProbeInputProvider();
        }

        private CharacterTestNetworkSessionResult RequestRoomNetworkGameplayEnter(
            CharacterTestNetworkSessionStatus status)
        {
            status = status ?? CaptureNetworkSessionStatus();
            if (status.RoomStatus == NetworkRoomStatus.Open)
            {
                if (!status.IsRoomHost)
                {
                    string message = "Only the room host can enter LAN Room gameplay.";
                    return CharacterTestNetworkSessionResult.Failed(
                        status.Kind,
                        status.NetworkMode,
                        message,
                        status);
                }

                if (!status.CanStartRoomGame)
                {
                    string message = string.IsNullOrWhiteSpace(status.ReadinessMessage)
                        ? "Room members are not ready."
                        : status.ReadinessMessage;
                    return CharacterTestNetworkSessionResult.Failed(
                        status.Kind,
                        status.NetworkMode,
                        message,
                        status);
                }

                var lobby = _context.NetworkRunner as ICharacterTestNetworkLobbyRunner;
                if (lobby == null)
                {
                    string message = "Enter Game is only available in LAN Room mode.";
                    return CharacterTestNetworkSessionResult.Failed(
                        status.Kind,
                        status.NetworkMode,
                        message,
                        status);
                }

                bool success = lobby.TryStartRoomGame(out string reason);
                status = CaptureNetworkSessionStatus();
                string resultMessage = success
                    ? "Host entered game. Waiting for gameplay synchronization."
                    : "Room enter failed: " + (string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason);
                _context.NetworkGameplayEnterPending = success;
                _context.LastNetworkSessionMessage = resultMessage;
                status.NetworkGameplayEnterPending = _context.NetworkGameplayEnterPending;
                status.Message = resultMessage;
                return success
                    ? CharacterTestNetworkSessionResult.Succeeded(status.Kind, status.NetworkMode, resultMessage, status)
                    : CharacterTestNetworkSessionResult.Failed(status.Kind, status.NetworkMode, resultMessage, status);
            }

            if (status.RoomStatus == NetworkRoomStatus.InGame)
            {
                _context.NetworkGameplayEnterPending = true;
                if (status.CanEnterGameplay)
                    return CompleteNetworkGameplayEnter("Network gameplay entered.");

                string message = string.IsNullOrWhiteSpace(status.ReadinessMessage)
                    ? "Waiting for gameplay synchronization."
                    : status.ReadinessMessage;
                status.NetworkGameplayEnterPending = true;
                status.Message = message;
                _context.LastNetworkSessionMessage = message;
                return CharacterTestNetworkSessionResult.Succeeded(
                    status.Kind,
                    status.NetworkMode,
                    message,
                    status);
            }

            string failedMessage = string.IsNullOrWhiteSpace(status.ReadinessMessage)
                ? "Room is not ready to enter gameplay."
                : status.ReadinessMessage;
            return CharacterTestNetworkSessionResult.Failed(
                status.Kind,
                status.NetworkMode,
                failedMessage,
                status);
        }

        private CharacterTestNetworkSessionResult CompleteNetworkGameplayEnter(string successMessage)
        {
            CharacterTestMainMenuCommandResult result =
                _context.MainMenuCoordinator.StartOrContinueFromMainMenu();
            _context.NetworkGameplayEnterPending = false;
            if (result.Success)
            {
                _context.NetworkGameplayEntered = true;
                ArmNetworkSyncProbeAutoRunAfterEnter();
            }
            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            string resultMessage = result.Success
                ? successMessage
                : result.Message;
            _context.LastNetworkSessionMessage = resultMessage;
            status.Message = resultMessage;
            return result.Success
                ? CharacterTestNetworkSessionResult.Succeeded(status.Kind, status.NetworkMode, resultMessage, status)
                : CharacterTestNetworkSessionResult.Failed(status.Kind, status.NetworkMode, resultMessage, status);
        }

        private void PopulateNetworkReadinessStatus(CharacterTestNetworkSessionStatus status)
        {
            if (status == null)
                return;

            var lobby = _context.NetworkRunner as ICharacterTestNetworkLobbyRunner;
            if (lobby != null)
            {
                status.IsRoomHost = lobby.IsRoomHost;
                status.LocalRoomReady = lobby.LocalRoomReady;
                status.CanSetRoomReady = lobby.CanSetRoomReady;
                status.CanStartRoomGame = lobby.CanStartRoomGame;
            }

            status.NetworkGameplayEnterPending = _context.NetworkGameplayEnterPending;
            status.CanEnterGameplay = IsLocalNetworkGameplayReady(status);
            status.CanRequestEnterGameplay = CanRequestNetworkGameplayEnter(status);
            status.ReadinessMessage = FormatNetworkReadinessMessage(status);
        }

        private void ArmNetworkSyncProbeAutoRunAfterEnter()
        {
            if (!_context.NetworkSyncProbeAutoRunEnabled || _context.NetworkSyncProbeAutoRunConsumed)
                return;

            _context.NetworkSyncProbeAutoRunPending = true;
            _context.LastNetworkSessionMessage = "Auto Sync Probe armed. Waiting for gameplay verification.";
        }

        private void TryStartPendingNetworkSyncProbeAutoRun()
        {
            if (!_context.NetworkSyncProbeAutoRunEnabled
                || !_context.NetworkSyncProbeAutoRunPending
                || _context.NetworkSyncProbeAutoRunConsumed)
            {
                return;
            }

            CharacterTestNetworkSessionStatus status = CaptureNetworkSessionStatus();
            if (status == null || !status.IsNetworkActive || !status.CanEnterGameplay)
                return;

            CharacterTestNetworkSyncProbeStatus current =
                _context.NetworkSyncProbe.CaptureStatus(
                    canStart: true,
                    autoRunEnabled: _context.NetworkSyncProbeAutoRunEnabled,
                    autoRunPending: _context.NetworkSyncProbeAutoRunPending);
            if (current.IsRunning)
            {
                _context.NetworkSyncProbeAutoRunPending = false;
                _context.NetworkSyncProbeAutoRunConsumed = true;
                return;
            }

            CharacterTestNetworkSyncProbeStatus started = StartNetworkSyncProbe();
            _context.NetworkSyncProbeAutoRunPending = false;
            _context.NetworkSyncProbeAutoRunConsumed = true;
            if (started != null && started.Result == CharacterTestNetworkSyncProbeResult.Running)
            {
                _context.LastNetworkSessionMessage = "Auto " + started.Summary;
                _context.Logger?.Info("GameManager", "Auto Sync Probe started after Enter Game.");
            }
            else if (started != null)
            {
                _context.Logger?.Warning("GameManager", "Auto Sync Probe did not start: " + started.Summary);
            }
        }

        private void InstallNetworkSyncProbeInputProvider()
        {
            if (_context.Slice == null)
                return;

            if (_context.NetworkSyncProbeInputProvider == null)
                _context.NetworkSyncProbeInputProvider = new FakeInputProvider();

            _context.NetworkSyncProbeInputProvider.SetContext(InputContext.Gameplay);
            _context.NetworkSyncProbeInputProvider.SetSnapshot(InputSnapshot.Empty);
            _context.Slice.Services.Commands.SetLocalInputProvider(_context.NetworkSyncProbeInputProvider);
            _context.NetworkSyncProbeInputProviderActive = true;
        }

        private void RestoreNetworkSyncProbeInputProvider()
        {
            if (!_context.NetworkSyncProbeInputProviderActive)
                return;

            _context.NetworkSyncProbeInputProviderActive = false;
            _context.NetworkSyncProbeInputProvider?.SetSnapshot(InputSnapshot.Empty);
            if (_context.Slice != null)
                _context.Slice.Services.Commands.SetLocalInputProvider(_sessionBuilder.ResolvePlayerUiInputProvider());
        }

        private bool IsNetworkPanelOpen()
        {
            return _context.UiRuntime != null && _context.UiRuntime.IsNetworkPanelOpen;
        }

        private CharacterTestNetworkSessionStatus CreateStatusFromOptions(
            CharacterTestNetworkSessionKind kind,
            CharacterTestNetworkSessionOptions options)
        {
            options = options?.Clone() ?? CreateDefaultNetworkSessionOptions(kind);
            CharacterTestNetworkEndpoint.ApplyEndpointOverrides(options);
            return new CharacterTestNetworkSessionStatus
            {
                Kind = kind,
                LaunchMode = options.LaunchMode,
                NetworkMode = CharacterTestNetworkSessionModeMapper.ToNetworkMode(kind, options.LaunchMode),
                NetworkTickRate = Mathf.Max(1, _profile.TargetFrameRate),
                Message = _context.LastNetworkSessionMessage,
                BindAddress = NormalizeBindAddress(options.BindAddress),
                RemoteAddress = NormalizeRemoteAddress(options.RemoteAddress),
                Port = NormalizePort(options.Port, CharacterTestNetworkSessionOptions.DefaultPort),
                ServerPeerId = NormalizeServerPeerId(options.ServerPeerId),
                ClientPeerId = NormalizeClientPeerId(options.ClientPeerId),
                StateSyncPolicy = options.StateSyncPolicy,
                HeartbeatIntervalFrames = Mathf.Max(1, options.HeartbeatIntervalFrames),
                RoomId = NormalizeRoomId(options.RoomId),
                RoomName = NormalizeRoomName(options.RoomName),
                RoomReadyCount = 0,
                RoomConnectedCount = 0
            };
        }

        private string ResolveRuntimeClientPeerId()
        {
            if (!string.IsNullOrWhiteSpace(_context.RuntimeClientPeerId))
                return _context.RuntimeClientPeerId;

            string configured = NormalizeClientPeerId(CharacterTestNetworkSessionOptions.DefaultClientPeerId);
            _context.RuntimeClientPeerId = string.Equals(
                    configured,
                    CharacterTestNetworkSessionOptions.DefaultClientPeerId,
                    StringComparison.Ordinal)
                ? "client-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                : configured;
            return _context.RuntimeClientPeerId;
        }

        private void ApplyNetworkRoomDefaults(CharacterTestNetworkSessionOptions options)
        {
            if (options == null)
                return;

            options.RoomId = NormalizeRoomId(options.RoomId);
            options.RoomName = NormalizeRoomName(options.RoomName);
            options.RoomMaxMembers = NormalizeRoomMaxMembers(options.RoomMaxMembers);
            options.RoomMinReadyMembers = NormalizeRoomMinReadyMembers(options.RoomMinReadyMembers);
            options.DisplayName = NormalizeRoomDisplayName(options.DisplayName);
        }

        private static bool IsLocalNetworkGameplayReady(CharacterTestNetworkSessionStatus status)
        {
            if (status == null || !status.IsNetworkActive)
                return false;
            if (HasNetworkFailure(status))
                return false;

            bool gameplayHandshakeReady =
                status.ClientStatus == NetworkSessionStatus.Connected
                && status.OwnedEntityId > 0
                && status.FullSnapshotReceivedCount > 0;
            if (status.LaunchMode == CharacterTestNetworkLaunchMode.Room)
                return status.RoomStatus == NetworkRoomStatus.InGame && gameplayHandshakeReady;

            return gameplayHandshakeReady;
        }

        private static bool CanRequestNetworkGameplayEnter(CharacterTestNetworkSessionStatus status)
        {
            if (status == null || !status.IsNetworkActive)
                return false;
            if (HasNetworkFailure(status))
                return false;

            if (status.LaunchMode != CharacterTestNetworkLaunchMode.Room)
                return status.CanEnterGameplay;

            return status.IsRoomHost
                && status.RoomStatus == NetworkRoomStatus.Open
                && status.CanStartRoomGame
                && !status.NetworkGameplayEnterPending;
        }

        private static string FormatNetworkReadinessMessage(CharacterTestNetworkSessionStatus status)
        {
            if (status == null || !status.IsNetworkActive)
                return "Offline.";
            if (HasNetworkFailure(status))
            {
                string reason = !string.IsNullOrWhiteSpace(status.DisconnectReason)
                    ? status.DisconnectReason
                    : !string.IsNullOrWhiteSpace(status.RoomReason)
                        ? status.RoomReason
                        : "network session failed";
                return "Network failed: " + reason.Trim();
            }

            if (status.LaunchMode == CharacterTestNetworkLaunchMode.Room)
                return FormatRoomReadinessMessage(status);

            return FormatDirectLanReadinessMessage(status);
        }

        private static string FormatRoomReadinessMessage(CharacterTestNetworkSessionStatus status)
        {
            if (status.RoomStatus != NetworkRoomStatus.InGame)
            {
                if (status.RoomStatus != NetworkRoomStatus.Open)
                    return "Connecting to LAN room.";
                if (!status.LocalRoomReady && status.CanSetRoomReady)
                    return "Room connected. Click Ready when this player is ready.";
                if (status.CanStartRoomGame)
                    return status.IsRoomHost
                        ? "All required members are ready. Host can enter gameplay."
                        : "All required members are ready. Waiting for host to enter gameplay.";
                if (status.IsRoomHost)
                {
                    return "Waiting for room members. Ready "
                        + status.RoomReadyCount
                        + "/"
                        + status.RoomConnectedCount
                        + ".";
                }

                return "Waiting for host to enter gameplay. Ready "
                    + status.RoomReadyCount
                    + "/"
                    + status.RoomConnectedCount
                    + ".";
            }

            if (status.NetworkGameplayEnterPending)
                return "Host entered game. Synchronizing gameplay.";
            return FormatGameplayHandshakeMessage(status);
        }

        private static string FormatDirectLanReadinessMessage(CharacterTestNetworkSessionStatus status)
        {
            return FormatGameplayHandshakeMessage(status);
        }

        private static string FormatGameplayHandshakeMessage(CharacterTestNetworkSessionStatus status)
        {
            if (status.ClientStatus != NetworkSessionStatus.Connected)
                return "Waiting for gameplay client connection.";
            if (status.OwnedEntityId <= 0)
                return "Waiting for server-owned player entity.";
            if (status.FullSnapshotReceivedCount <= 0)
                return "Waiting for first full gameplay snapshot.";
            return "Network gameplay verified. Enter Game is available.";
        }

        private static bool HasNetworkFailure(CharacterTestNetworkSessionStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.DisconnectReason)
                    || status.ServerStatus == NetworkSessionStatus.Rejected
                    || status.ClientStatus == NetworkSessionStatus.Rejected);
        }

        internal static string NormalizeBindAddress(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultBindAddress
                : value.Trim();
        }

        internal static string NormalizeRemoteAddress(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultRemoteAddress
                : value.Trim();
        }

        internal static ushort NormalizePort(ushort value, ushort fallback)
        {
            return value == 0 ? fallback : value;
        }

        internal static string NormalizeServerPeerId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultServerPeerId
                : value.Trim();
        }

        internal static string NormalizeClientPeerId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultClientPeerId
                : value.Trim();
        }

        internal static string NormalizeRoomId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultRoomId
                : value.Trim();
        }

        internal static string NormalizeRoomName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultRoomName
                : value.Trim();
        }

        internal static int NormalizeRoomMaxMembers(int value)
        {
            return Mathf.Max(1, value);
        }

        internal static int NormalizeRoomMinReadyMembers(int value)
        {
            return Mathf.Max(1, value);
        }

        internal static string NormalizeRoomDisplayName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? CharacterTestNetworkSessionOptions.DefaultDisplayName
                : value.Trim();
        }

        private static string CreateNetworkSessionStartMessage(
            CharacterTestNetworkSessionOptions options,
            string endpoint)
        {
            bool room = options != null && options.LaunchMode == CharacterTestNetworkLaunchMode.Room;
            string roomId = options != null
                ? NormalizeRoomId(options.RoomId)
                : CharacterTestNetworkSessionOptions.DefaultRoomId;
            if (options != null && options.Kind == CharacterTestNetworkSessionKind.Host)
            {
                return room
                    ? "Hosting LAN room '" + roomId + "' on " + endpoint
                    : "Hosting LAN session on " + endpoint;
            }

            return room
                ? "Joining LAN room '" + roomId + "' at " + endpoint
                : "Joining LAN session at " + endpoint;
        }

        private static string EmptyDiagnosticsAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                ? fallback ?? string.Empty
                : value.Trim();
        }
    }
}
