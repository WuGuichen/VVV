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

    /// <summary>
    /// Optional draft boundary used by settings screens that expose explicit Apply/Cancel actions.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="IInputRebindingService"/> so existing adapters retain their
    /// immediate-save behavior without a breaking interface change.
    /// </remarks>
    public interface IInputRebindingEditSessionService
    {
        bool IsEditSessionActive { get; }
        bool HasPendingChanges { get; }

        void BeginEditSession();
        void CommitEditSession();
        void RollbackEditSession();
    }

    public sealed class InputRebindingService :
        IInputRebindingService,
        IInputRebindingEditSessionService,
        IDisposable
    {
        private readonly InputActionAsset _actions;
        private readonly string _preferencesKey;
        private readonly Action _bindingsChanged;

        private InputActionRebindingExtensions.RebindingOperation _operation;
        private InputAction _activeAction;
        private bool _activeActionWasEnabled;
        private string _editSessionSnapshot = string.Empty;
        private bool _editSessionActive;
        private bool _hasPendingChanges;

        public InputRebindingService(
            InputActionAsset actions,
            string preferencesKey,
            Action bindingsChanged = null)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _preferencesKey = string.IsNullOrEmpty(preferencesKey) ? "mxframework.input.rebinds" : preferencesKey;
            _bindingsChanged = bindingsChanged;
        }

        public bool IsRebinding => _operation != null;
        public bool IsEditSessionActive => _editSessionActive;
        public bool HasPendingChanges => _hasPendingChanges;

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
                _bindingsChanged?.Invoke();
            }
        }

        public void ResetToDefault()
        {
            Cancel();
            _actions.RemoveAllBindingOverrides();
            if (_editSessionActive)
                _hasPendingChanges = true;
            else
                Save();
            _bindingsChanged?.Invoke();
        }

        public void BeginEditSession()
        {
            if (_editSessionActive)
                return;
            if (IsRebinding)
            {
                throw new InvalidOperationException(
                    "Cannot begin an input edit session while rebinding is in progress.");
            }

            _editSessionSnapshot = _actions.SaveBindingOverridesAsJson() ?? string.Empty;
            _editSessionActive = true;
            _hasPendingChanges = false;
        }

        public void CommitEditSession()
        {
            if (!_editSessionActive)
                return;

            Cancel();
            Save();
            ClearEditSession();
        }

        public void RollbackEditSession()
        {
            if (!_editSessionActive)
                return;

            Cancel();
            _actions.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(_editSessionSnapshot))
                _actions.LoadBindingOverridesFromJson(_editSessionSnapshot);
            ClearEditSession();
            _bindingsChanged?.Invoke();
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
                if (_editSessionActive)
                    _hasPendingChanges = true;
                else
                    Save();
                _bindingsChanged?.Invoke();
            }
        }

        private void ClearEditSession()
        {
            _editSessionSnapshot = string.Empty;
            _editSessionActive = false;
            _hasPendingChanges = false;
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
