using System;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiNetworkPanelViewModel
    {
        public CharacterTestNetworkSessionStatus Status { get; set; } =
            CharacterTestNetworkSessionStatus.Offline();
        public CharacterTestNetworkSessionOptions HostOptions { get; set; } =
            CharacterTestNetworkSessionOptions.HostDefaults();
        public CharacterTestNetworkSessionOptions JoinOptions { get; set; } =
            CharacterTestNetworkSessionOptions.JoinDefaults();
        public CharacterTestNetworkLaunchMode SelectedLaunchMode { get; set; } =
            CharacterTestNetworkLaunchMode.DirectLan;
        public bool CanSelectLaunchMode { get; set; } = true;
        public bool CanHost { get; set; } = true;
        public bool CanJoin { get; set; } = true;
        public bool CanDisconnect { get; set; }
        public bool CanReady { get; set; }
        public bool LocalReady { get; set; }
        public bool CanEnterGame { get; set; }
        public bool CanToggleAutoSyncProbe { get; set; }
        public bool AutoSyncProbeAfterEnterGame { get; set; }
        public bool AutoSyncProbePending { get; set; }
        public bool CanRunSyncProbe { get; set; }
        public bool SyncProbeRunning { get; set; }
        public bool CanClose { get; set; } = true;
        public string Title { get; set; } = "Network";
        public string StatusText { get; set; } = "Offline";
        public string EndpointText { get; set; } = "LAN Host / Join";
        public string ModeDescriptionText { get; set; } = string.Empty;
        public string ConnectionHeaderText { get; set; } = "Connection";
        public string ConnectionSummaryText { get; set; } = string.Empty;
        public string RoomHeaderText { get; set; } = "Room";
        public string RoomSummaryText { get; set; } = string.Empty;
        public string MemberSummaryText { get; set; } = string.Empty;
        public string ReadinessSummaryText { get; set; } = string.Empty;
        public string SyncProbeSummaryText { get; set; } = "Sync Probe: not run.";
        public string ReplaySummaryText { get; set; } = "Replay Check: not run";
        public string ReplayTone { get; set; } = "idle";
        public string ReadyButtonText { get; set; } = "Ready";
        public string EnterGameButtonText { get; set; } = "Enter Game";
        public string AutoSyncProbeButtonText { get; set; } = "Auto Probe After Enter Game";
        public string RunSyncProbeButtonText { get; set; } = "Run Sync Probe";
        public string DetailsHeaderText { get; set; } = "Details";
        public string DetailTransportText { get; set; } = string.Empty;
        public string DetailPeerText { get; set; } = string.Empty;
        public string DetailRttText { get; set; } = string.Empty;
        public string DetailFramesText { get; set; } = string.Empty;
        public string DetailSnapshotText { get; set; } = string.Empty;
        public string DetailDisconnectText { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    internal static class CharacterTestFairyGuiNetworkPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiNetworkPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(256);
            CharacterTestNetworkSessionStatus status = viewModel.Status;
            builder.Append(status != null ? status.Kind.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.LaunchMode.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.NetworkMode.ToString() : string.Empty).Append('|')
                .Append(status != null && status.IsNetworkActive ? '1' : '0').Append('|')
                .Append(status != null ? status.ServerStatus.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.ClientStatus.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.RttEstimateFrames : 0).Append('|')
                .Append(status != null ? status.RoomStatus.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.RoomReadyCount : 0).Append('|')
                .Append(status != null ? status.RoomConnectedCount : 0).Append('|')
                .Append(status != null ? status.RoomStateVersion : 0L).Append('|')
                .Append(status != null && status.LocalRoomReady ? '1' : '0').Append('|')
                .Append(status != null && status.CanSetRoomReady ? '1' : '0').Append('|')
                .Append(status != null && status.CanStartRoomGame ? '1' : '0').Append('|')
                .Append(status != null && status.CanRequestEnterGameplay ? '1' : '0').Append('|')
                .Append(status != null && status.NetworkGameplayEnterPending ? '1' : '0').Append('|')
                .Append(status != null && status.CanEnterGameplay ? '1' : '0').Append('|')
                .Append(status != null ? status.StateSyncPolicy.ToString() : string.Empty).Append('|')
                .Append(status != null ? status.HeartbeatIntervalFrames : 0).Append('|')
                .Append(viewModel.SelectedLaunchMode).Append('|')
                .Append(viewModel.CanSelectLaunchMode ? '1' : '0').Append('|')
                .Append(viewModel.CanHost ? '1' : '0').Append('|')
                .Append(viewModel.CanJoin ? '1' : '0').Append('|')
                .Append(viewModel.CanDisconnect ? '1' : '0').Append('|')
                .Append(viewModel.CanReady ? '1' : '0').Append('|')
                .Append(viewModel.LocalReady ? '1' : '0').Append('|')
                .Append(viewModel.CanEnterGame ? '1' : '0').Append('|')
                .Append(viewModel.CanToggleAutoSyncProbe ? '1' : '0').Append('|')
                .Append(viewModel.AutoSyncProbeAfterEnterGame ? '1' : '0').Append('|')
                .Append(viewModel.AutoSyncProbePending ? '1' : '0').Append('|')
                .Append(viewModel.CanRunSyncProbe ? '1' : '0').Append('|')
                .Append(viewModel.SyncProbeRunning ? '1' : '0').Append('|')
                .Append(viewModel.CanClose ? '1' : '0').Append('|');
            AppendToken(builder, status != null ? status.RoomId : string.Empty);
            AppendToken(builder, status != null ? status.RoomName : string.Empty);
            AppendToken(builder, status != null ? status.RoomMembers : string.Empty);
            AppendToken(builder, status != null ? status.RoomReason : string.Empty);
            AppendToken(builder, status != null ? status.ReadinessMessage : string.Empty);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.EndpointText);
            AppendToken(builder, viewModel.ModeDescriptionText);
            AppendToken(builder, viewModel.ConnectionSummaryText);
            AppendToken(builder, viewModel.RoomSummaryText);
            AppendToken(builder, viewModel.MemberSummaryText);
            AppendToken(builder, viewModel.ReadinessSummaryText);
            AppendToken(builder, viewModel.SyncProbeSummaryText);
            AppendToken(builder, viewModel.ReplaySummaryText);
            AppendToken(builder, viewModel.ReplayTone);
            AppendToken(builder, viewModel.ReadyButtonText);
            AppendToken(builder, viewModel.EnterGameButtonText);
            AppendToken(builder, viewModel.AutoSyncProbeButtonText);
            AppendToken(builder, viewModel.RunSyncProbeButtonText);
            AppendToken(builder, viewModel.DetailTransportText);
            AppendToken(builder, viewModel.DetailPeerText);
            AppendToken(builder, viewModel.DetailRttText);
            AppendToken(builder, viewModel.DetailFramesText);
            AppendToken(builder, viewModel.DetailSnapshotText);
            AppendToken(builder, viewModel.DetailDisconnectText);
            AppendToken(builder, viewModel.Message);
            AppendOptions(builder, viewModel.HostOptions);
            AppendOptions(builder, viewModel.JoinOptions);
            return builder.ToString();
        }

        private static void AppendOptions(StringBuilder builder, CharacterTestNetworkSessionOptions options)
        {
            if (options == null)
            {
                builder.Append("null|");
                return;
            }

            builder.Append(options.Kind).Append('|')
                .Append(options.LaunchMode).Append('|')
                .Append(options.Port).Append('|')
                .Append(options.InputDelayFrames).Append('|')
                .Append(options.SnapshotIntervalFrames).Append('|')
                .Append(options.StateSyncPolicy).Append('|')
                .Append(options.HeartbeatIntervalFrames).Append('|')
                .Append(options.RoomMaxMembers).Append('|')
                .Append(options.RoomMinReadyMembers).Append('|')
                .Append(options.RoomClientAutoReady ? '1' : '0').Append('|')
                .Append(options.RoomServerAutoStartWhenReady ? '1' : '0').Append('|');
            AppendToken(builder, options.BindAddress);
            AppendToken(builder, options.RemoteAddress);
            AppendToken(builder, options.ServerPeerId);
            AppendToken(builder, options.ClientPeerId);
            AppendToken(builder, options.RoomId);
            AppendToken(builder, options.RoomName);
            AppendToken(builder, options.DisplayName);
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
