using System;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    public sealed class InputBindingDisplayService
    {
        private readonly InputActionAsset _actions;

        public InputBindingDisplayService(InputActionAsset actions)
        {
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        public bool TryGetDisplayString(string actionId, int bindingIndex, out string displayString)
        {
            return TryGetDisplayString(actionId, bindingIndex, InputBinding.DisplayStringOptions.DontUseShortDisplayNames, out displayString);
        }

        public bool TryGetDisplayString(
            string actionId,
            int bindingIndex,
            InputBinding.DisplayStringOptions options,
            out string displayString)
        {
            displayString = string.Empty;
            InputAction action = ResolveAction(actionId);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return false;
            }

            displayString = action.GetBindingDisplayString(bindingIndex, options);
            return true;
        }

        private InputAction ResolveAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return null;
            }

            InputAction action = _actions.FindAction(actionId, false);
            if (action != null)
            {
                return action;
            }

            int slash = actionId.IndexOf('/');
            if (slash <= 0 || slash >= actionId.Length - 1)
            {
                return null;
            }

            InputActionMap map = _actions.FindActionMap(actionId.Substring(0, slash), false);
            return map != null ? map.FindAction(actionId.Substring(slash + 1), false) : null;
        }
    }
}
