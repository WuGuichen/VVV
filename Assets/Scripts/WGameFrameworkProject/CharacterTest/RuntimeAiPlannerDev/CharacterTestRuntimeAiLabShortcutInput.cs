using System;
using UnityEngine.InputSystem;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabShortcut
    {
        TogglePlayPause,
        PreviousFrame,
        NextFrame,
        IncreasePlaybackSpeed,
        DecreasePlaybackSpeed,
        CancelPopup
    }

    internal sealed class CharacterTestRuntimeAiLabShortcutInput : IDisposable
    {
        private readonly InputActionMap _actionMap;
        private bool _dispatchingShortcut;
        private bool _disposeScheduled;
        private bool _disposed;

        public CharacterTestRuntimeAiLabShortcutInput(
            Action<CharacterTestRuntimeAiLabShortcut> shortcutSink)
        {
            if (shortcutSink == null)
                throw new ArgumentNullException(nameof(shortcutSink));

            _actionMap = new InputActionMap("RuntimeAiLabShortcuts");
            Bind("TogglePlayPause", "<Keyboard>/space", CharacterTestRuntimeAiLabShortcut.TogglePlayPause);
            Bind("PreviousFrame", "<Keyboard>/leftArrow", CharacterTestRuntimeAiLabShortcut.PreviousFrame);
            Bind("NextFrame", "<Keyboard>/rightArrow", CharacterTestRuntimeAiLabShortcut.NextFrame);
            Bind("IncreasePlaybackSpeed", "<Keyboard>/upArrow", CharacterTestRuntimeAiLabShortcut.IncreasePlaybackSpeed);
            Bind("DecreasePlaybackSpeed", "<Keyboard>/downArrow", CharacterTestRuntimeAiLabShortcut.DecreasePlaybackSpeed);
            Bind("CancelPopup", "<Keyboard>/escape", CharacterTestRuntimeAiLabShortcut.CancelPopup);

            void Bind(string actionName, string binding, CharacterTestRuntimeAiLabShortcut shortcut)
            {
                InputAction action = _actionMap.AddAction(
                    actionName,
                    InputActionType.Button,
                    binding);
                action.performed += _ => DispatchShortcut(shortcutSink, shortcut);
            }
        }

        public void Enable()
        {
            if (_disposed || _disposeScheduled)
                return;
            _actionMap.Enable();
        }

        public void Dispose()
        {
            if (_disposed || _disposeScheduled)
                return;
            if (_dispatchingShortcut)
            {
                _disposeScheduled = true;
                InputSystem.onAfterUpdate += DisposeAfterInputUpdate;
                return;
            }
            DisposeCore();
        }

        private void DispatchShortcut(
            Action<CharacterTestRuntimeAiLabShortcut> shortcutSink,
            CharacterTestRuntimeAiLabShortcut shortcut)
        {
            if (_disposed || _disposeScheduled)
                return;
            _dispatchingShortcut = true;
            try
            {
                shortcutSink(shortcut);
            }
            finally
            {
                _dispatchingShortcut = false;
            }
        }

        private void DisposeAfterInputUpdate()
        {
            InputSystem.onAfterUpdate -= DisposeAfterInputUpdate;
            DisposeCore();
        }

        private void DisposeCore()
        {
            if (_disposed)
                return;
            _disposed = true;
            _disposeScheduled = false;
            _actionMap.Disable();
            _actionMap.Dispose();
        }
    }
}
