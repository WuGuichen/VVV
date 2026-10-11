using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    [RequireComponent(typeof(PlayerInput))]
    public sealed class LocalUserInputAdapter : InputService
    {
        private PlayerInput _inputUser;

        public PlayerInput InputUser => _inputUser;

        protected override InputActionAsset CreateRuntimeActions(out bool ownsActions)
        {
            ownsActions = false;
            _inputUser = GetComponent<PlayerInput>();
            return _inputUser != null ? _inputUser.actions : null;
        }
    }
}
