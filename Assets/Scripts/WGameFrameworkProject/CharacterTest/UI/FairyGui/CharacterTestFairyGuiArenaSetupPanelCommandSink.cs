using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiArenaSetupPanelCommandSink : IMxUiCommandSink
    {
        private readonly CharacterTestArenaSessionController _controller;
        private readonly Func<bool> _refreshPanel;
        private readonly Func<int, int, bool> _openBehaviorGroupPanel;

        internal CharacterTestFairyGuiArenaSetupPanelCommandSink(
            CharacterTestArenaSessionController controller,
            Func<bool> refreshPanel = null,
            Func<int, int, bool> openBehaviorGroupPanel = null)
        {
            _controller = controller;
            _refreshPanel = refreshPanel;
            _openBehaviorGroupPanel = openBehaviorGroupPanel;
        }

        public int CommandCount { get; private set; }
        public string LastStatusMessage { get; private set; } = string.Empty;
        public MxUiCommand LastCommand { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;
            bool handled = true;
            string commandId = CharacterTestFairyGuiArenaSetupPanelCommandIds.Normalize(command.CommandId);
            switch (commandId)
            {
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.ToggleSlot:
                    ToggleSlot(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.CharacterPrev:
                    SelectCharacter(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, -1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.CharacterNext:
                    SelectCharacter(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, 1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.StrategyPrev:
                    SelectLoadout(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, -1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.StrategyNext:
                    SelectLoadout(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, 1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.VariantPrev:
                    SelectVariant(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, -1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.VariantNext:
                    SelectVariant(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload, 1);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.EditGroup:
                    OpenBehaviorGroup(command.Payload as CharacterTestFairyGuiArenaSetupPanelPayload);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraThirdPerson:
                    SelectCameraMode(CharacterTestCameraMode.ThirdPersonControlled);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraAuto:
                    SelectCameraMode(CharacterTestCameraMode.MultiTargetAuto);
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.StartBattle:
                    StartBattle();
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.Replay:
                    Replay();
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.Reconfigure:
                    Reconfigure();
                    break;
                case CharacterTestFairyGuiArenaSetupPanelCommandIds.Refresh:
                    LastStatusMessage = "Refreshed.";
                    break;
                default:
                    LastStatusMessage = "Unsupported arena command: " + commandId;
                    handled = false;
                    break;
            }

            if (handled)
                _refreshPanel?.Invoke();
        }

        private void ToggleSlot(CharacterTestFairyGuiArenaSetupPanelPayload payload)
        {
            if (!TryResolvePayload(payload, out int teamId, out int slotIndex))
                return;

            CharacterTestArenaSlotConfig slot = _controller.Config.TryGetSlot(teamId, slotIndex);
            bool enabled = slot == null || !slot.Enabled;
            LastStatusMessage = _controller.SetSlotEnabled(teamId, slotIndex, enabled)
                ? "Arena slot toggled."
                : _controller.StatusMessage;
        }

        private void SelectCharacter(CharacterTestFairyGuiArenaSetupPanelPayload payload, int direction)
        {
            if (!TryResolvePayload(payload, out int teamId, out int slotIndex))
                return;

            LastStatusMessage = _controller.SelectNextCharacter(teamId, slotIndex, direction)
                ? "Character selected."
                : _controller.StatusMessage;
        }

        private void SelectLoadout(CharacterTestFairyGuiArenaSetupPanelPayload payload, int direction)
        {
            if (!TryResolvePayload(payload, out int teamId, out int slotIndex))
                return;

            LastStatusMessage = _controller.SelectNextLoadout(teamId, slotIndex, direction)
                ? "Strategy selected."
                : _controller.StatusMessage;
        }

        private void SelectVariant(CharacterTestFairyGuiArenaSetupPanelPayload payload, int direction)
        {
            if (!TryResolvePayload(payload, out int teamId, out int slotIndex))
                return;

            LastStatusMessage = _controller.SelectNextVariant(teamId, slotIndex, direction)
                ? "Variant selected."
                : _controller.StatusMessage;
        }

        private void OpenBehaviorGroup(CharacterTestFairyGuiArenaSetupPanelPayload payload)
        {
            if (!TryResolvePayload(payload, out int teamId, out int slotIndex))
                return;

            LastStatusMessage = _openBehaviorGroupPanel != null
                && _openBehaviorGroupPanel(teamId, slotIndex)
                    ? "Behavior group settings opened."
                    : "Behavior group settings are unavailable.";
        }

        private void StartBattle()
        {
            LastStatusMessage = _controller.StartBattle()
                ? "Arena battle started."
                : _controller.StatusMessage;
        }

        private void SelectCameraMode(CharacterTestCameraMode mode)
        {
            LastStatusMessage = _controller.SelectCameraMode(mode)
                ? "Camera mode selected."
                : _controller.StatusMessage;
        }

        private void Replay()
        {
            LastStatusMessage = _controller.Replay()
                ? "Arena battle restarted."
                : _controller.StatusMessage;
        }

        private void Reconfigure()
        {
            LastStatusMessage = _controller.Reconfigure()
                ? "Arena setup opened."
                : _controller.StatusMessage;
        }

        private bool TryResolvePayload(
            CharacterTestFairyGuiArenaSetupPanelPayload payload,
            out int teamId,
            out int slotIndex)
        {
            teamId = payload != null ? payload.TeamId : 0;
            slotIndex = payload != null ? payload.SlotIndex : -1;
            if (_controller == null)
            {
                LastStatusMessage = "Arena controller is unavailable.";
                return false;
            }

            if (_controller.Config.TryGetSlot(teamId, slotIndex) != null)
                return true;

            LastStatusMessage = "Arena slot is unavailable.";
            return false;
        }
    }
}
