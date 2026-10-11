using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiMainMenuCommandSink : IMxUiCommandSink
    {
        private readonly ICharacterTestMainMenuController _controller;
        private readonly Action _toggleSettingsPanel;
        private readonly Action _toggleAbilitiesPanel;
        private readonly Action _toggleBehaviorDeckPanel;
        private Func<bool> _dismissHomepageConfirmation;

        internal void SetHomepageConfirmationDismissal(Func<bool> dismiss)
            => _dismissHomepageConfirmation = dismiss;

        internal bool TryDismissHomepageConfirmation()
            => _dismissHomepageConfirmation?.Invoke() == true;

        public CharacterTestFairyGuiMainMenuCommandSink(ICharacterTestMainMenuController controller)
            : this(controller, null, null, null)
        {
        }

        public CharacterTestFairyGuiMainMenuCommandSink(
            ICharacterTestMainMenuController controller,
            Action toggleSettingsPanel)
            : this(controller, toggleSettingsPanel, null, null)
        {
        }

        public CharacterTestFairyGuiMainMenuCommandSink(
            ICharacterTestMainMenuController controller,
            Action toggleSettingsPanel,
            Action toggleAbilitiesPanel,
            Action toggleBehaviorDeckPanel = null)
        {
            _controller = controller;
            _toggleSettingsPanel = toggleSettingsPanel;
            _toggleAbilitiesPanel = toggleAbilitiesPanel;
            _toggleBehaviorDeckPanel = toggleBehaviorDeckPanel;
        }

        public int CommandCount { get; private set; }
        public int SettingsToggleCount { get; private set; }
        public int AbilitiesToggleCount { get; private set; }
        public int BehaviorDeckToggleCount { get; private set; }
        public MxUiCommand LastCommand { get; private set; }
        public CharacterTestMainMenuCommandResult LastResult { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            CommandCount++;
            LastCommand = command;

            if (command.CommandId == CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle)
            {
                if (_toggleSettingsPanel != null)
                {
                    _toggleSettingsPanel();
                    SettingsToggleCount++;
                    LastResult = CharacterTestMainMenuCommandResult.Succeeded("Settings panel opened.");
                    return;
                }

                LastResult = CharacterTestMainMenuCommandResult.Failed("Settings panel unavailable.");
                return;
            }

            if (command.CommandId == CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle)
            {
                if (_toggleAbilitiesPanel != null)
                {
                    _toggleAbilitiesPanel();
                    AbilitiesToggleCount++;
                    LastResult = CharacterTestMainMenuCommandResult.Succeeded("Abilities panel opened.");
                    return;
                }

                LastResult = CharacterTestMainMenuCommandResult.Failed("Abilities panel unavailable.");
                return;
            }

            if (command.CommandId == CharacterTestFairyGuiMainMenuCommandIds.BehaviorDeckToggle)
            {
                if (_toggleBehaviorDeckPanel != null)
                {
                    _toggleBehaviorDeckPanel();
                    BehaviorDeckToggleCount++;
                    LastResult = CharacterTestMainMenuCommandResult.Succeeded("Behavior deck opened.");
                    return;
                }

                LastResult = CharacterTestMainMenuCommandResult.Failed("Behavior deck unavailable.");
                return;
            }

            if (_controller == null)
            {
                LastResult = CharacterTestMainMenuCommandResult.Failed("Main menu controller unavailable.");
                return;
            }

            switch (command.CommandId)
            {
                case CharacterTestFairyGuiMainMenuCommandIds.Start:
                    LastResult = _controller.StartOrContinueFromMainMenu();
                    break;
                case CharacterTestFairyGuiMainMenuCommandIds.Network:
                    LastResult = _controller.OpenNetworkPanelFromMainMenu();
                    break;
                case CharacterTestFairyGuiMainMenuCommandIds.RestartOffline:
                    LastResult = _controller.RestartOfflineFromMainMenu();
                    break;
                case CharacterTestFairyGuiMainMenuCommandIds.Close:
                    LastResult = _controller.CloseMainMenu();
                    break;
                default:
                    LastResult = CharacterTestMainMenuCommandResult.Failed(
                        "Unsupported main menu command: " + command.CommandId);
                    break;
            }
        }
    }
}
