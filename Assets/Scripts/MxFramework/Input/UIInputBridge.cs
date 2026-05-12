using System;
using UnityEngine;

namespace MxFramework.Input
{
    public sealed class UIInputBridge : MonoBehaviour
    {
        [SerializeField] private InputService _inputService;
        [SerializeField] private InputContextPolicy _policy = InputContextPolicy.Exclusive;
        [SerializeField] private bool _enterOnEnable = true;

        private IDisposable _scope;

        public void Enter()
        {
            Exit();
            if (_inputService != null)
            {
                _scope = _inputService.PushContext(InputContext.UI, _policy);
            }
        }

        public void Exit()
        {
            if (_scope == null)
            {
                return;
            }

            _scope.Dispose();
            _scope = null;
        }

        private void Reset()
        {
            _inputService = FindFirstObjectByType<InputService>();
        }

        private void OnEnable()
        {
            if (_enterOnEnable)
            {
                Enter();
            }
        }

        private void OnDisable()
        {
            Exit();
        }
    }
}
