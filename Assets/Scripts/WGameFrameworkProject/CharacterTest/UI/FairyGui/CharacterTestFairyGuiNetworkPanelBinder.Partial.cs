using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiNetworkPanelBinder
    {
        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiNetworkPanelViewModel viewModel)
        {
            ApplyFullScreenLayout(root);
            ApplyReplayTone(root, viewModel != null ? viewModel.ReplayTone : string.Empty);
        }

        private static CharacterTestNetworkSessionOptions CreateHostOptions(
            Fgui.GComponent root,
            CharacterTestFairyGuiNetworkPanelViewModel viewModel)
        {
            CharacterTestNetworkSessionOptions options =
                viewModel.HostOptions?.Clone() ?? CharacterTestNetworkSessionOptions.HostDefaults();
            options.Kind = CharacterTestNetworkSessionKind.Host;
            options.LaunchMode = viewModel.SelectedLaunchMode;
            options.Port = ReadPort(root, options.Port);
            CharacterTestNetworkEndpoint.ApplyEndpointOverrides(options);
            ApplyRoomInputs(root, options);
            return options;
        }

        private static CharacterTestNetworkSessionOptions CreateJoinOptions(
            Fgui.GComponent root,
            CharacterTestFairyGuiNetworkPanelViewModel viewModel)
        {
            CharacterTestNetworkSessionOptions options =
                viewModel.JoinOptions?.Clone() ?? CharacterTestNetworkSessionOptions.JoinDefaults();
            options.Kind = CharacterTestNetworkSessionKind.Join;
            options.LaunchMode = viewModel.SelectedLaunchMode;
            options.RemoteAddress = ReadText(
                root,
                CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput,
                options.RemoteAddress);
            options.Port = ReadPort(root, options.Port);
            CharacterTestNetworkEndpoint.ApplyEndpointOverrides(options);
            ApplyRoomInputs(root, options);
            return options;
        }

        private static void ApplyRoomInputs(
            Fgui.GComponent root,
            CharacterTestNetworkSessionOptions options)
        {
            string roomName = ReadText(
                root,
                CharacterTestFairyGuiNetworkPanelIds.RoomNameInput,
                ResolveRoomName(options, options));
            options.RoomName = roomName;
            options.RoomId = roomName;
            options.DisplayName = ReadText(
                root,
                CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput,
                ResolveDisplayName(options, options));
        }

        private static ushort ReadPort(Fgui.GComponent root, ushort fallback)
        {
            string text = ReadText(root, CharacterTestFairyGuiNetworkPanelIds.PortInput, fallback.ToString());
            ushort parsed;
            return ushort.TryParse(text, out parsed) && parsed > 0 ? parsed : fallback;
        }

        private static void ApplyFullScreenLayout(Fgui.GComponent root)
        {
            if (root == null)
                return;

            float width = MxFairyGuiStageLayout.ResolveStageWidth(root, 1280f);
            float height = MxFairyGuiStageLayout.ResolveStageHeight(root, 720f);

            root.SetSize(width, height);
            SetChildBounds(root, "modalShade", 0f, 0f, width, height);
            SetChildBounds(root, CharacterTestFairyGuiNetworkPanelIds.PanelFrame, 0f, 0f, width, height);
            SetChildBounds(root, CharacterTestFairyGuiNetworkPanelIds.PanelTrim, 0f, 0f, width, 3f);
            SetChildBounds(root, "modeBand", 32f, 96f, Mathf.Max(1f, width - 64f), 64f);
            SetChildBounds(root, "detailsPanel", 32f, Mathf.Max(552f, height - 168f), Mathf.Max(1f, width - 64f), 128f);
        }

        private static void SetChildBounds(
            Fgui.GComponent root,
            string childName,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child == null)
                return;

            child.SetXY(x, y);
            child.SetSize(width, height);
        }

        private static void ApplyReplayTone(Fgui.GComponent root, string replayTone)
        {
            Fgui.GTextField text = root.GetChild(CharacterTestFairyGuiNetworkPanelIds.ReplaySummary)?.asTextField;
            if (text == null)
                return;

            switch (replayTone ?? string.Empty)
            {
                case "pass":
                    text.color = new Color32(104, 220, 141, 255);
                    break;
                case "recording":
                    text.color = new Color32(244, 192, 95, 255);
                    break;
                case "fail":
                    text.color = new Color32(255, 120, 120, 255);
                    break;
                case "muted":
                    text.color = new Color32(132, 144, 150, 255);
                    break;
                default:
                    text.color = new Color32(172, 180, 186, 255);
                    break;
            }
        }

        private static string ResolveRemoteAddress(CharacterTestNetworkSessionOptions options)
        {
            return string.IsNullOrWhiteSpace(options?.RemoteAddress)
                ? "127.0.0.1"
                : options.RemoteAddress.Trim();
        }

        private static string ResolvePortText(
            CharacterTestNetworkSessionOptions joinOptions,
            CharacterTestNetworkSessionOptions hostOptions)
        {
            ushort port = joinOptions != null && joinOptions.Port > 0
                ? joinOptions.Port
                : hostOptions != null && hostOptions.Port > 0
                    ? hostOptions.Port
                    : (ushort)7777;
            return port.ToString();
        }

        private static string ResolveRoomName(
            CharacterTestNetworkSessionOptions joinOptions,
            CharacterTestNetworkSessionOptions hostOptions)
        {
            if (!string.IsNullOrWhiteSpace(joinOptions?.RoomName))
                return joinOptions.RoomName.Trim();
            if (!string.IsNullOrWhiteSpace(hostOptions?.RoomName))
                return hostOptions.RoomName.Trim();
            if (!string.IsNullOrWhiteSpace(joinOptions?.RoomId))
                return joinOptions.RoomId.Trim();
            if (!string.IsNullOrWhiteSpace(hostOptions?.RoomId))
                return hostOptions.RoomId.Trim();
            return "CharacterTest LAN Room";
        }

        private static string ResolveDisplayName(
            CharacterTestNetworkSessionOptions joinOptions,
            CharacterTestNetworkSessionOptions hostOptions)
        {
            if (!string.IsNullOrWhiteSpace(joinOptions?.DisplayName))
                return joinOptions.DisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(hostOptions?.DisplayName))
                return hostOptions.DisplayName.Trim();
            return "Player";
        }
    }
}
