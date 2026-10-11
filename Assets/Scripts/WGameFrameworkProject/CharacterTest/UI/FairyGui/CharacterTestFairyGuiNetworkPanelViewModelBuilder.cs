using System;
using MxFramework.Network;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiNetworkPanelViewModelBuilder
    {
        public static CharacterTestFairyGuiNetworkPanelViewModel Build(
            ICharacterTestNetworkSessionController controller,
            IMxUiTextProvider textProvider = null,
            CharacterTestNetworkLaunchMode selectedLaunchMode = CharacterTestNetworkLaunchMode.Room,
            bool useSavedPreferences = false)
        {
            CharacterTestNetworkSessionStatus status = controller != null
                ? controller.CaptureNetworkSessionStatus()
                : CharacterTestNetworkSessionStatus.Offline(
                    CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.message.controller_unavailable", "Network controller unavailable."));
            status = status ?? CharacterTestNetworkSessionStatus.Offline();

            CharacterTestNetworkSessionOptions hostOptions = controller != null
                ? controller.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Host)
                : CharacterTestNetworkSessionOptions.HostDefaults();
            CharacterTestNetworkSessionOptions joinOptions = controller != null
                ? controller.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Join)
                : CharacterTestNetworkSessionOptions.JoinDefaults();
            bool active = status.IsNetworkActive;
            if (active)
                selectedLaunchMode = status.LaunchMode;
            else if (useSavedPreferences)
                selectedLaunchMode = CharacterTestNetworkPanelPreferences.LoadLaunchMode(selectedLaunchMode);
            if (useSavedPreferences)
            {
                hostOptions = CharacterTestNetworkPanelPreferences.Apply(hostOptions);
                joinOptions = CharacterTestNetworkPanelPreferences.Apply(joinOptions);
            }
            hostOptions = PrepareOptions(hostOptions, selectedLaunchMode);
            joinOptions = PrepareOptions(joinOptions, selectedLaunchMode);
            var diagnosticController = controller as ICharacterTestNetworkDiagnosticController;
            CharacterTestNetworkSyncProbeStatus probeStatus = diagnosticController != null
                ? diagnosticController.CaptureNetworkSyncProbeStatus()
                : CharacterTestNetworkSyncProbeStatus.Idle(false, "Sync Probe: diagnostic controller unavailable.");
            var replayController = controller as ICharacterTestNetworkReplayController;
            CharacterTestReplayPlaybackStatus replayStatus = replayController != null
                ? replayController.CaptureNetworkReplayStatus()
                : null;
            bool savedAutoProbe = useSavedPreferences
                && CharacterTestNetworkPanelPreferences.LoadAutoSyncProbeAfterEnterGame();
            bool autoProbeEnabled = active
                ? probeStatus.AutoRunEnabled
                : savedAutoProbe;

            return new CharacterTestFairyGuiNetworkPanelViewModel
            {
                Status = status,
                HostOptions = hostOptions,
                JoinOptions = joinOptions,
                SelectedLaunchMode = selectedLaunchMode,
                CanSelectLaunchMode = controller != null && !active,
                CanHost = controller != null && !active,
                CanJoin = controller != null && !active,
                CanDisconnect = controller != null && status.IsNetworkActive,
                CanReady = controller != null && status.CanSetRoomReady,
                LocalReady = status.LocalRoomReady,
                CanEnterGame = controller != null && status.CanRequestEnterGameplay,
                CanToggleAutoSyncProbe = diagnosticController != null,
                AutoSyncProbeAfterEnterGame = autoProbeEnabled,
                AutoSyncProbePending = probeStatus.AutoRunPending,
                CanRunSyncProbe = diagnosticController != null
                    && status.IsNetworkActive
                    && (probeStatus.IsRunning || probeStatus.CanStart),
                SyncProbeRunning = probeStatus.IsRunning,
                CanClose = !active,
                Title = CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.title", "Network Lobby"),
                StatusText = FormatStatus(status, textProvider),
                EndpointText = FormatEndpoint(status, hostOptions, joinOptions),
                ModeDescriptionText = FormatModeDescription(selectedLaunchMode, textProvider),
                ConnectionHeaderText = CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.connection", "Connection"),
                ConnectionSummaryText = FormatConnectionSummary(status, selectedLaunchMode, textProvider),
                RoomHeaderText = CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.room", "Room"),
                RoomSummaryText = FormatRoomSummary(status, selectedLaunchMode, hostOptions, joinOptions, textProvider),
                MemberSummaryText = FormatMemberSummary(status, textProvider),
                ReadinessSummaryText = FormatReadinessSummary(status, textProvider),
                SyncProbeSummaryText = FormatSyncProbeSummary(probeStatus),
                ReplaySummaryText = FormatReplaySummary(status, replayStatus, replayController != null),
                ReplayTone = ResolveReplayTone(status, replayStatus, replayController != null),
                ReadyButtonText = status.LocalRoomReady
                    ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.unready", "Unready")
                    : CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.ready", "Ready"),
                EnterGameButtonText = FormatEnterGameButtonText(status, selectedLaunchMode, textProvider),
                AutoSyncProbeButtonText = FormatAutoSyncProbeButtonText(autoProbeEnabled, probeStatus, textProvider),
                RunSyncProbeButtonText = probeStatus.IsRunning
                    ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.stop_probe", "Stop Probe")
                    : CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.run_sync_probe", "Run Sync Probe"),
                DetailsHeaderText = CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.details", "Details"),
                DetailTransportText = FormatTransportDetail(status, textProvider),
                DetailPeerText = FormatPeerDetail(status, textProvider),
                DetailRttText = FormatRttDetail(status, textProvider),
                DetailFramesText = FormatFramesDetail(status, textProvider),
                DetailSnapshotText = FormatSnapshotDetail(status, textProvider),
                DetailDisconnectText = FormatDisconnectDetail(status, textProvider),
                Message = status.Message
            };
        }

        private static CharacterTestNetworkSessionOptions PrepareOptions(
            CharacterTestNetworkSessionOptions options,
            CharacterTestNetworkLaunchMode selectedLaunchMode)
        {
            options = options?.Clone() ?? new CharacterTestNetworkSessionOptions();
            options.LaunchMode = selectedLaunchMode;
            if (string.IsNullOrWhiteSpace(options.RoomId))
                options.RoomId = CharacterTestNetworkSessionOptions.DefaultRoomId;
            if (string.IsNullOrWhiteSpace(options.RoomName))
                options.RoomName = options.RoomId;
            if (options.RoomMaxMembers <= 0)
                options.RoomMaxMembers = CharacterTestNetworkSessionOptions.DefaultRoomMaxMembers;
            if (options.RoomMinReadyMembers <= 0)
                options.RoomMinReadyMembers = CharacterTestNetworkSessionOptions.DefaultRoomMinReadyMembers;
            if (string.IsNullOrWhiteSpace(options.DisplayName))
                options.DisplayName = CharacterTestNetworkSessionOptions.DefaultDisplayName;
            if (selectedLaunchMode == CharacterTestNetworkLaunchMode.Room)
            {
                options.RoomClientAutoReady = false;
                options.RoomServerAutoStartWhenReady = false;
            }
            return options;
        }

        private static string FormatStatus(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null || !status.IsNetworkActive)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.status.offline", "Offline");

            if (!string.IsNullOrWhiteSpace(status.DisconnectReason)
                || status.ServerStatus == NetworkSessionStatus.Rejected
                || status.ClientStatus == NetworkSessionStatus.Rejected)
            {
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.status.failed", "Failed");
            }

            if (status.LaunchMode == CharacterTestNetworkLaunchMode.Room)
            {
                if (status.RoomStatus == NetworkRoomStatus.InGame)
                    return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.status.in_game", "In Game");
                if (status.RoomStatus == NetworkRoomStatus.Open)
                    return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.status.room_open", "Room Open");
            }

            switch (status.Kind)
            {
                case CharacterTestNetworkSessionKind.Host:
                    return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.status.hosting", "Hosting")
                        + "  "
                        + status.ServerStatus;
                case CharacterTestNetworkSessionKind.Join:
                    return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.status.joining", "Joining")
                        + "  "
                        + status.ClientStatus;
                default:
                    return status.NetworkMode.ToString();
            }
        }

        private static string FormatEndpoint(
            CharacterTestNetworkSessionStatus status,
            CharacterTestNetworkSessionOptions hostOptions,
            CharacterTestNetworkSessionOptions joinOptions)
        {
            if (status != null && status.IsNetworkActive)
            {
                if (status.Kind == CharacterTestNetworkSessionKind.Host)
                    return EmptyAs(status.BindAddress, CharacterTestNetworkSessionOptions.DefaultBindAddress) + ":" + status.Port;
                if (status.Kind == CharacterTestNetworkSessionKind.Join)
                    return EmptyAs(status.RemoteAddress, CharacterTestNetworkSessionOptions.DefaultRemoteAddress) + ":" + status.Port;
            }

            return EmptyAs(joinOptions?.RemoteAddress, CharacterTestNetworkSessionOptions.DefaultRemoteAddress)
                + ":"
                + (joinOptions != null
                    ? joinOptions.Port
                    : hostOptions != null
                        ? hostOptions.Port
                        : CharacterTestNetworkSessionOptions.DefaultPort);
        }

        private static string FormatModeDescription(
            CharacterTestNetworkLaunchMode selectedLaunchMode,
            IMxUiTextProvider textProvider)
        {
            return selectedLaunchMode == CharacterTestNetworkLaunchMode.Room
                ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.mode.room_desc", "LAN room verifies connection first: Ready, then Host Enter Game for everyone.")
                : CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.mode.direct_desc", "Direct LAN connects straight to a host endpoint.");
        }

        private static string FormatEnterGameButtonText(
            CharacterTestNetworkSessionStatus status,
            CharacterTestNetworkLaunchMode selectedLaunchMode,
            IMxUiTextProvider textProvider)
        {
            CharacterTestNetworkLaunchMode mode = status != null && status.IsNetworkActive
                ? status.LaunchMode
                : selectedLaunchMode;
            if (mode != CharacterTestNetworkLaunchMode.Room)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.enter_game", "Enter Game");

            if (status != null && status.NetworkGameplayEnterPending)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.syncing", "Syncing");
            if (status != null && status.IsNetworkActive && !status.IsRoomHost)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.waiting_host", "Waiting Host");
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.enter_game", "Enter Game");
        }

        private static string FormatConnectionSummary(
            CharacterTestNetworkSessionStatus status,
            CharacterTestNetworkLaunchMode selectedLaunchMode,
            IMxUiTextProvider textProvider)
        {
            string mode = selectedLaunchMode == CharacterTestNetworkLaunchMode.Room
                ? CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.mode.room", "LAN Room")
                : CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.mode.direct_lan", "Direct LAN");
            if (status == null || !status.IsNetworkActive)
                return mode + " · " + CharacterTestUiText.Resolve(textProvider, "ui.character_test.status.offline", "Offline");

            return mode + " · " + status.NetworkMode;
        }

        private static string FormatRoomSummary(
            CharacterTestNetworkSessionStatus status,
            CharacterTestNetworkLaunchMode selectedLaunchMode,
            CharacterTestNetworkSessionOptions hostOptions,
            CharacterTestNetworkSessionOptions joinOptions,
            IMxUiTextProvider textProvider)
        {
            if (selectedLaunchMode != CharacterTestNetworkLaunchMode.Room
                && (status == null || status.LaunchMode != CharacterTestNetworkLaunchMode.Room))
            {
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.room.direct_hint", "Direct LAN does not use room state.");
            }

            string roomName = EmptyAs(status?.RoomName, EmptyAs(hostOptions?.RoomName, EmptyAs(joinOptions?.RoomName, CharacterTestNetworkSessionOptions.DefaultRoomName)));
            string roomId = EmptyAs(status?.RoomId, EmptyAs(hostOptions?.RoomId, EmptyAs(joinOptions?.RoomId, CharacterTestNetworkSessionOptions.DefaultRoomId)));
            NetworkRoomStatus roomStatus = status != null ? status.RoomStatus : NetworkRoomStatus.Closed;
            int ready = status != null ? status.RoomReadyCount : 0;
            int connected = status != null ? status.RoomConnectedCount : 0;
            string reason = status != null ? status.RoomReason : string.Empty;
            string summary = roomName + " (" + roomId + ") · " + roomStatus + " · ready " + ready + "/" + connected;
            return string.IsNullOrWhiteSpace(reason)
                ? summary
                : summary + " · " + reason.Trim();
        }

        private static string FormatMemberSummary(
            CharacterTestNetworkSessionStatus status,
            IMxUiTextProvider textProvider)
        {
            string members = status != null ? status.RoomMembers : string.Empty;
            if (string.IsNullOrWhiteSpace(members) || string.Equals(members, "none", StringComparison.OrdinalIgnoreCase))
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.members_waiting", "Members: waiting");
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.members", "Members") + ": " + members.Trim();
        }

        private static string FormatReadinessSummary(
            CharacterTestNetworkSessionStatus status,
            IMxUiTextProvider textProvider)
        {
            string message = status != null ? status.ReadinessMessage : string.Empty;
            if (string.IsNullOrWhiteSpace(message))
                message = CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.readiness.offline", "Start or join a LAN room to test connectivity.");
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.readiness", "Readiness")
                + ": "
                + message.Trim();
        }

        private static string FormatSyncProbeSummary(CharacterTestNetworkSyncProbeStatus probeStatus)
        {
            if (probeStatus == null)
                return "Sync Probe: not run.";

            string summary = string.IsNullOrWhiteSpace(probeStatus.Summary)
                ? "Sync Probe: not run."
                : probeStatus.Summary.Trim();
            if (string.IsNullOrWhiteSpace(probeStatus.Detail))
                return summary;

            return summary + "\n" + probeStatus.Detail.Trim();
        }

        private static string FormatReplaySummary(
            CharacterTestNetworkSessionStatus sessionStatus,
            CharacterTestReplayPlaybackStatus replayStatus,
            bool hasReplayController)
        {
            if (sessionStatus == null || !sessionStatus.IsNetworkActive)
                return "Replay Check: not run";

            if (!hasReplayController)
                return "Replay Check: host only";

            if (replayStatus == null)
                return sessionStatus.Kind == CharacterTestNetworkSessionKind.Join
                    ? "Replay Check: host only"
                    : "Replay Check: not run";

            if (replayStatus.Recording)
                return "Replay Check: recording authoritative commands";

            if (replayStatus.PlaybackRan && replayStatus.Success)
            {
                return "Replay Check: PASS frames="
                    + replayStatus.FrameCount
                    + " auth="
                    + replayStatus.AuthoritativeCommandCount;
            }

            if (replayStatus.PlaybackRan)
            {
                return "Replay Check: FAIL "
                    + EmptyAs(replayStatus.ResultCode, "FAILED")
                    + " frame="
                    + replayStatus.FailureFrame.Value;
            }

            if (!replayStatus.Available)
            {
                if (sessionStatus.Kind == CharacterTestNetworkSessionKind.Join
                    || IsReplayRunnerUnavailable(replayStatus))
                {
                    return "Replay Check: host only";
                }

                return "Replay Check: not run";
            }

            return "Replay Check: not run";
        }

        private static string ResolveReplayTone(
            CharacterTestNetworkSessionStatus sessionStatus,
            CharacterTestReplayPlaybackStatus replayStatus,
            bool hasReplayController)
        {
            if (sessionStatus == null || !sessionStatus.IsNetworkActive)
                return "idle";

            if (!hasReplayController)
                return "muted";

            if (replayStatus == null)
                return sessionStatus.Kind == CharacterTestNetworkSessionKind.Join ? "muted" : "idle";

            if (replayStatus.Recording)
                return "recording";

            if (replayStatus.PlaybackRan)
                return replayStatus.Success ? "pass" : "fail";

            if (!replayStatus.Available)
                return sessionStatus.Kind == CharacterTestNetworkSessionKind.Join
                    || IsReplayRunnerUnavailable(replayStatus)
                        ? "muted"
                        : "idle";

            return "idle";
        }

        private static bool IsReplayRunnerUnavailable(CharacterTestReplayPlaybackStatus replayStatus)
        {
            if (replayStatus == null)
                return false;

            string summary = replayStatus.Summary ?? string.Empty;
            return summary.IndexOf("does not expose replay", StringComparison.OrdinalIgnoreCase) >= 0
                || summary.IndexOf("controller is unavailable", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatAutoSyncProbeButtonText(
            bool enabled,
            CharacterTestNetworkSyncProbeStatus probeStatus,
            IMxUiTextProvider textProvider)
        {
            if (probeStatus != null && probeStatus.AutoRunPending)
            {
                return CharacterTestUiText.Resolve(
                    textProvider,
                    "ui.character_test.network.auto_probe_pending",
                    "Auto Probe Pending");
            }

            return enabled
                ? CharacterTestUiText.Resolve(
                    textProvider,
                    "ui.character_test.network.auto_probe_on",
                    "Auto Probe On")
                : CharacterTestUiText.Resolve(
                    textProvider,
                    "ui.character_test.network.auto_probe_off",
                    "Auto Probe Off");
        }

        private static string FormatTransportDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null || !status.IsNetworkActive)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.transport", "Transport") + ": none";
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.transport", "Transport")
                + ": "
                + EmptyAs(status.TransportMode, status.NetworkMode.ToString())
                + " / "
                + EmptyAs(status.TransportId, "none");
        }

        private static string FormatPeerDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null)
                return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.peer", "Peer") + ": none";
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.peer", "Peer")
                + ": server="
                + EmptyAs(status.ServerPeerId, "server")
                + " client="
                + EmptyAs(status.ClientPeerId, "client-0")
                + " owned="
                + status.OwnedEntityId;
        }

        private static string FormatRttDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            int rtt = status != null ? status.RttEstimateFrames : 0;
            int avg = status != null ? status.AverageRttFrames : 0;
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.rtt", "RTT")
                + ": "
                + rtt
                + "f avg "
                + avg
                + "f";
        }

        private static string FormatFramesDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            long serverFrame = status != null ? status.ServerFrame.Value : 0L;
            long clientFrame = status != null ? status.ClientFrame.Value : 0L;
            int drift = status != null ? status.FrameDriftFrames : 0;
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.frames", "Frames")
                + ": server "
                + serverFrame
                + " client "
                + clientFrame
                + " drift "
                + drift
                + "f";
        }

        private static string FormatSnapshotDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null)
                status = CharacterTestNetworkSessionStatus.Offline();
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.snapshots", "Snapshots")
                + ": policy "
                + status.StateSyncPolicy
                + " heartbeat "
                + status.HeartbeatIntervalFrames
                + "f resync "
                + status.ResyncCount
                + " mismatch "
                + status.SnapshotMismatchCount
                + " req "
                + status.SnapshotRequestCount
                + " full "
                + (status.FullSnapshotSentCount + status.FullSnapshotReceivedCount)
                + " digest "
                + (status.DigestSnapshotSentCount + status.DigestSnapshotReceivedCount);
        }

        private static string FormatDisconnectDetail(CharacterTestNetworkSessionStatus status, IMxUiTextProvider textProvider)
        {
            string reason = status != null ? status.DisconnectReason : string.Empty;
            return CharacterTestUiText.Resolve(textProvider, "ui.character_test.network.disconnect_reason", "Disconnect")
                + ": "
                + EmptyAs(reason, "none");
        }

        private static string EmptyAs(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
