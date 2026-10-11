using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiNetworkPanelCommandSink : IMxUiCommandSink
    {
        private readonly ICharacterTestNetworkSessionController _controller;
        private readonly ICharacterTestNetworkLobbyController _lobbyController;
        private readonly ICharacterTestNetworkDiagnosticController _diagnosticController;
        private readonly Action _closeRequested;
        private readonly Action<CharacterTestNetworkLaunchMode> _launchModeChanged;
        private readonly bool _persistPreferences;

        public CharacterTestFairyGuiNetworkPanelCommandSink(ICharacterTestNetworkSessionController controller)
            : this(controller, null)
        {
        }

        public CharacterTestFairyGuiNetworkPanelCommandSink(
            ICharacterTestNetworkSessionController controller,
            Action closeRequested)
            : this(controller, controller as ICharacterTestNetworkLobbyController, closeRequested, null)
        {
        }

        public CharacterTestFairyGuiNetworkPanelCommandSink(
            ICharacterTestNetworkSessionController controller,
            Action closeRequested,
            Action<CharacterTestNetworkLaunchMode> launchModeChanged)
            : this(controller, controller as ICharacterTestNetworkLobbyController, closeRequested, launchModeChanged)
        {
        }

        public CharacterTestFairyGuiNetworkPanelCommandSink(
            ICharacterTestNetworkSessionController controller,
            Action closeRequested,
            Action sessionStarted,
            Action<CharacterTestNetworkLaunchMode> launchModeChanged)
            : this(controller, controller as ICharacterTestNetworkLobbyController, closeRequested, launchModeChanged)
        {
        }

        public CharacterTestFairyGuiNetworkPanelCommandSink(
            ICharacterTestNetworkSessionController controller,
            ICharacterTestNetworkLobbyController lobbyController,
            Action closeRequested,
            Action<CharacterTestNetworkLaunchMode> launchModeChanged,
            bool persistPreferences = false,
            ICharacterTestNetworkDiagnosticController diagnosticController = null)
        {
            _controller = controller;
            _lobbyController = lobbyController;
            _diagnosticController = diagnosticController ?? controller as ICharacterTestNetworkDiagnosticController;
            _closeRequested = closeRequested;
            _launchModeChanged = launchModeChanged;
            _persistPreferences = persistPreferences;
        }

        public int CommandCount { get; private set; }
        public MxUiCommand LastCommand { get; private set; }
        public CharacterTestNetworkSessionResult LastResult { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;

            if (string.Equals(command.CommandId, CharacterTestFairyGuiNetworkPanelCommandIds.DirectLanMode, StringComparison.Ordinal)
                || string.Equals(command.CommandId, CharacterTestFairyGuiNetworkPanelCommandIds.RoomMode, StringComparison.Ordinal))
            {
                CharacterTestNetworkLaunchMode launchMode =
                    string.Equals(command.CommandId, CharacterTestFairyGuiNetworkPanelCommandIds.RoomMode, StringComparison.Ordinal)
                        ? CharacterTestNetworkLaunchMode.Room
                        : CharacterTestNetworkLaunchMode.DirectLan;
                if (_persistPreferences)
                    CharacterTestNetworkPanelPreferences.SaveLaunchMode(launchMode);
                _launchModeChanged?.Invoke(launchMode);
                CharacterTestNetworkSessionStatus status = _controller != null
                    ? _controller.CaptureNetworkSessionStatus()
                    : CharacterTestNetworkSessionStatus.Offline("Network mode changed.");
                status = status ?? CharacterTestNetworkSessionStatus.Offline("Network mode changed.");
                LastResult = CharacterTestNetworkSessionResult.Succeeded(
                    status.Kind,
                    status.NetworkMode,
                    "Network mode changed.",
                    status);
                return;
            }

            if (_controller == null)
            {
                if (IsCloseCommand(command.CommandId))
                {
                    _closeRequested?.Invoke();
                    LastResult = CharacterTestNetworkSessionResult.Succeeded(
                        CharacterTestNetworkSessionKind.Offline,
                        CharacterTestNetworkMode.Disabled,
                        "Network panel closed.",
                        CharacterTestNetworkSessionStatus.Offline("Network panel closed."));
                }
                else
                {
                    LastResult = CharacterTestNetworkSessionResult.Failed(
                        CharacterTestNetworkSessionKind.Offline,
                        CharacterTestNetworkMode.Disabled,
                        "Network controller unavailable.");
                }

                return;
            }

            switch (command.CommandId)
            {
                case CharacterTestFairyGuiNetworkPanelCommandIds.Host:
                {
                    CharacterTestNetworkSessionOptions options =
                        command.Payload as CharacterTestNetworkSessionOptions
                        ?? _controller.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Host);
                    if (_persistPreferences)
                        CharacterTestNetworkPanelPreferences.Save(options);
                    LastResult = _controller.StartNetworkSession(options);
                    break;
                }
                case CharacterTestFairyGuiNetworkPanelCommandIds.Join:
                {
                    CharacterTestNetworkSessionOptions options =
                        command.Payload as CharacterTestNetworkSessionOptions
                        ?? _controller.CreateDefaultNetworkSessionOptions(CharacterTestNetworkSessionKind.Join);
                    if (_persistPreferences)
                        CharacterTestNetworkPanelPreferences.Save(options);
                    LastResult = _controller.StartNetworkSession(options);
                    break;
                }
                case CharacterTestFairyGuiNetworkPanelCommandIds.Disconnect:
                    LastResult = _controller.StopNetworkSession();
                    break;
                case CharacterTestFairyGuiNetworkPanelCommandIds.Ready:
                    LastResult = _lobbyController != null
                        ? _lobbyController.SetLocalReady(command.Payload is bool ready && ready)
                        : CreateMissingLobbyResult("Ready is only available in LAN Room mode.");
                    break;
                case CharacterTestFairyGuiNetworkPanelCommandIds.EnterGame:
                    LastResult = _lobbyController != null
                        ? _lobbyController.EnterNetworkGameplay()
                        : CreateMissingLobbyResult("Network gameplay is not ready to enter.");
                    break;
                case CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe:
                    LastResult = SetAutoSyncProbe(command.Payload is bool enabled && enabled);
                    break;
                case CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe:
                    LastResult = RunOrStopSyncProbe();
                    break;
                case CharacterTestFairyGuiNetworkPanelCommandIds.Close:
                case MxFairyGuiCommandIds.Cancel:
                    _closeRequested?.Invoke();
                    CharacterTestNetworkSessionStatus status = _controller.CaptureNetworkSessionStatus()
                        ?? CharacterTestNetworkSessionStatus.Offline("Network panel closed.");
                    LastResult = CharacterTestNetworkSessionResult.Succeeded(
                        status.Kind,
                        status.NetworkMode,
                        "Network panel closed.",
                        status);
                    break;
                default:
                    LastResult = CharacterTestNetworkSessionResult.Failed(
                        CharacterTestNetworkSessionKind.Offline,
                        CharacterTestNetworkMode.Disabled,
                        "Unsupported network panel command: " + command.CommandId);
                    break;
            }
        }

        private static bool IsCloseCommand(string commandId)
        {
            return string.Equals(commandId, CharacterTestFairyGuiNetworkPanelCommandIds.Close, StringComparison.Ordinal)
                || string.Equals(commandId, MxFairyGuiCommandIds.Cancel, StringComparison.Ordinal);
        }

        private CharacterTestNetworkSessionResult RunOrStopSyncProbe()
        {
            if (_diagnosticController == null)
                return CreateMissingDiagnosticResult("Sync Probe is unavailable.");

            CharacterTestNetworkSyncProbeStatus probeStatus =
                _diagnosticController.CaptureNetworkSyncProbeStatus();
            probeStatus = probeStatus != null && probeStatus.IsRunning
                ? _diagnosticController.StopNetworkSyncProbe()
                : _diagnosticController.StartNetworkSyncProbe();
            CharacterTestNetworkSessionStatus status = _controller != null
                ? _controller.CaptureNetworkSessionStatus()
                : CharacterTestNetworkSessionStatus.Offline(probeStatus?.Summary);
            status = status ?? CharacterTestNetworkSessionStatus.Offline(probeStatus?.Summary);
            string message = probeStatus != null ? probeStatus.Summary : "Sync Probe command finished.";
            status.Message = message;
            bool success = probeStatus != null
                && probeStatus.Result != CharacterTestNetworkSyncProbeResult.Rejected
                && probeStatus.Result != CharacterTestNetworkSyncProbeResult.Fail;
            return success
                ? CharacterTestNetworkSessionResult.Succeeded(status.Kind, status.NetworkMode, message, status)
                : CharacterTestNetworkSessionResult.Failed(status.Kind, status.NetworkMode, message, status);
        }

        private CharacterTestNetworkSessionResult SetAutoSyncProbe(bool enabled)
        {
            if (_persistPreferences)
                CharacterTestNetworkPanelPreferences.SaveAutoSyncProbeAfterEnterGame(enabled);
            if (_diagnosticController == null)
                return CreateMissingDiagnosticResult("Auto Sync Probe is unavailable.");

            _diagnosticController.SetNetworkSyncProbeAutoRun(enabled);
            CharacterTestNetworkSessionStatus status = _controller != null
                ? _controller.CaptureNetworkSessionStatus()
                : CharacterTestNetworkSessionStatus.Offline();
            status = status ?? CharacterTestNetworkSessionStatus.Offline();
            string message = enabled
                ? "Auto Sync Probe after Enter Game enabled."
                : "Auto Sync Probe after Enter Game disabled.";
            status.Message = message;
            return CharacterTestNetworkSessionResult.Succeeded(
                status.Kind,
                status.NetworkMode,
                message,
                status);
        }

        private CharacterTestNetworkSessionResult CreateMissingLobbyResult(string message)
        {
            CharacterTestNetworkSessionStatus status = _controller != null
                ? _controller.CaptureNetworkSessionStatus()
                : CharacterTestNetworkSessionStatus.Offline(message);
            status = status ?? CharacterTestNetworkSessionStatus.Offline(message);
            return CharacterTestNetworkSessionResult.Failed(
                status.Kind,
                status.NetworkMode,
                message,
                status);
        }

        private CharacterTestNetworkSessionResult CreateMissingDiagnosticResult(string message)
        {
            CharacterTestNetworkSessionStatus status = _controller != null
                ? _controller.CaptureNetworkSessionStatus()
                : CharacterTestNetworkSessionStatus.Offline(message);
            status = status ?? CharacterTestNetworkSessionStatus.Offline(message);
            return CharacterTestNetworkSessionResult.Failed(
                status.Kind,
                status.NetworkMode,
                message,
                status);
        }
    }
}
