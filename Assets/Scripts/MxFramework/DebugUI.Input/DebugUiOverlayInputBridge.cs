using MxFramework.DebugUI.Toolkit;
using MxFramework.Input;
using UnityEngine;

namespace MxFramework.DebugUI.Input
{
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    [AddComponentMenu("MxFramework/Debug UI/Debug UI Input Bridge")]
    public sealed class DebugUiOverlayInputBridge : MonoBehaviour, IDebugUiInputTarget
    {
        [SerializeField] private InputService _inputService;
        [SerializeField] private DebugUiOverlayController _overlay;
        [SerializeField] private bool _enableOnStart = true;

        private readonly DebugUiInputAdapter _adapter = new DebugUiInputAdapter();
        private DebugUiInputAdapterResult _lastResult;

        public DebugUiInputAdapterResult LastResult => _lastResult;
        public DebugUiVisibility Visibility => _overlay != null ? _overlay.Visibility : DebugUiVisibility.Hidden;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _adapter.Configure(_inputService);
            _adapter.SetEnabled(_enableOnStart && _inputService != null);
        }

        private void OnDisable()
        {
            _adapter.SetEnabled(false);
        }

        private void Update()
        {
            if (_overlay == null || _inputService == null)
                return;

            _lastResult = _adapter.ProcessFrame(_inputService.LastCommandFrame, this);
        }

        public void SetVisibility(DebugUiVisibility visibility)
        {
            if (_overlay != null)
                _overlay.SetVisibility(visibility);
        }

        public void RefreshNow()
        {
            if (_overlay != null)
                _overlay.RefreshNow();
        }

        public void RequestDebugStep()
        {
        }

        private void ResolveReferences()
        {
            if (_inputService == null)
                _inputService = GetComponent<InputService>();
            if (_overlay == null)
                _overlay = GetComponent<DebugUiOverlayController>();
        }
    }
}
