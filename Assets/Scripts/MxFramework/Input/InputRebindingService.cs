using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public interface IInputRebindingService
    {
        bool IsRebinding { get; }

        void StartRebind(string actionId, int bindingIndex);
        void Cancel();
        void Save();
        void Load();
        void ResetToDefault();
    }

    public sealed class InputRebindingService : IInputRebindingService, IDisposable
    {
        private readonly InputActionAsset _actions;
        private readonly string _preferencesKey;

        private InputActionRebindingExtensions.RebindingOperation _operation;
        private InputAction _activeAction;
        private bool _activeActionWasEnabled;

        public InputRebindingService(InputActionAsset actions, string preferencesKey)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _preferencesKey = string.IsNullOrEmpty(preferencesKey) ? "mxframework.input.rebinds" : preferencesKey;
        }

        public bool IsRebinding => _operation != null;

        public void StartRebind(string actionId, int bindingIndex)
        {
            if (IsRebinding)
            {
                throw new InvalidOperationException("An input rebinding operation is already running.");
            }

            InputAction action = ResolveAction(actionId);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex), "Input binding index is outside the action binding list.");
            }

            _activeAction = action;
            _activeActionWasEnabled = action.enabled;
            action.Disable();

            _operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(OnRebindComplete)
                .OnCancel(OnRebindCanceled);

            _operation.Start();
        }

        public void Cancel()
        {
            if (_operation == null)
            {
                return;
            }

            _operation.Cancel();
        }

        public void Save()
        {
            string json = _actions.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(_preferencesKey, json);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            string json = PlayerPrefs.GetString(_preferencesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                _actions.LoadBindingOverridesFromJson(json);
            }
        }

        public void ResetToDefault()
        {
            Cancel();
            _actions.RemoveAllBindingOverrides();
            Save();
        }

        public void Dispose()
        {
            if (_operation != null)
            {
                _operation.Dispose();
                _operation = null;
            }

            RestoreActiveAction();
        }

        private void OnRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishOperation(operation, true);
        }

        private void OnRebindCanceled(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishOperation(operation, false);
        }

        private void FinishOperation(InputActionRebindingExtensions.RebindingOperation operation, bool save)
        {
            operation.Dispose();
            _operation = null;
            RestoreActiveAction();

            if (save)
            {
                Save();
            }
        }

        private void RestoreActiveAction()
        {
            if (_activeAction == null)
            {
                return;
            }

            if (_activeActionWasEnabled)
            {
                _activeAction.Enable();
            }

            _activeAction = null;
            _activeActionWasEnabled = false;
        }

        private InputAction ResolveAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                throw new ArgumentException("Input action id cannot be null or empty.", nameof(actionId));
            }

            InputAction action = _actions.FindAction(actionId, false);
            if (action != null)
            {
                return action;
            }

            int slash = actionId.IndexOf('/');
            if (slash > 0 && slash < actionId.Length - 1)
            {
                string mapName = actionId.Substring(0, slash);
                string actionName = actionId.Substring(slash + 1);
                InputActionMap map = _actions.FindActionMap(mapName, false);
                action = map != null ? map.FindAction(actionName, false) : null;
                if (action != null)
                {
                    return action;
                }
            }

            throw new InvalidOperationException("Input action was not found: " + actionId + ".");
        }
    }
}
