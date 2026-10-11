using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MxFramework.Input
{
    [DefaultExecutionOrder(-1000)]
    public class InputService : MonoBehaviour, IInputProvider
    {
        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private bool _cloneActionAsset = true;
        [SerializeField] private InputContext _initialContext = InputContext.Gameplay;
        [SerializeField] private int _sourceId = 1;
        [SerializeField] private bool _enqueuePressedCommands = true;
        [SerializeField] private string _bindingOverridesKey = "mxframework.input.rebinds";

        private readonly InputActionCache _cache = new InputActionCache();
        private readonly InputCommandQueue _commands = new InputCommandQueue();
        private readonly InputContextStack _contexts = new InputContextStack();

        private InputActionAsset _runtimeActions;
        private bool _ownsRuntimeActions;
        private long _frame;
        private IInputRebindingService _rebinding;
        private InputBindingDisplayService _bindingDisplay;
        private InputSnapshot _snapshot;
        private bool _contextChangeSubscribed;

        public InputSnapshot Snapshot
        {
            get
            {
                if (isActiveAndEnabled)
                {
                    EnsureActionsReadyForRead();
                    if (_runtimeActions != null && _frame == 0L)
                    {
                        return _cache.ReadSnapshot();
                    }
                }

                return _snapshot;
            }

            private set => _snapshot = value;
        }
        public InputCommandQueue Commands => _commands;
        public InputContextStack Contexts => _contexts;
        public InputContext CurrentContext => _contexts.ActiveContext;
        public long LastCommandFrame => _frame > 0L ? _frame - 1L : _commands.CurrentFrame;
        public InputActionAsset Actions => _runtimeActions;
        public IInputRebindingService Rebinding => _rebinding;
        public InputBindingDisplayService BindingDisplay => _bindingDisplay;

        /// <summary>
        /// Changes a runtime action binding path without creating a saved binding override.
        /// Intended for local environment defaults on cloned runtime actions; user rebinding still uses <see cref="Rebinding"/>.
        /// </summary>
        public bool TryChangeBindingPath(
            string actionId,
            int bindingIndex,
            string bindingPath,
            string expectedCurrentPath = null,
            bool skipWhenBindingHasOverride = true)
        {
            if (string.IsNullOrWhiteSpace(bindingPath))
                return false;

            EnsureActionsReadyForRead();
            InputAction action = ResolveAction(actionId);
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            InputBinding binding = action.bindings[bindingIndex];
            if (skipWhenBindingHasOverride && !string.IsNullOrEmpty(binding.overridePath))
                return false;

            if (!string.IsNullOrEmpty(expectedCurrentPath)
                && !string.Equals(binding.path, expectedCurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(binding.path, bindingPath, StringComparison.OrdinalIgnoreCase))
                return true;

            bool wasEnabled = action.enabled;
            if (wasEnabled)
                action.Disable();

            try
            {
                action.ChangeBinding(bindingIndex).WithPath(bindingPath);
                _bindingDisplay?.NotifyBindingsChanged();
            }
            finally
            {
                if (wasEnabled)
                    action.Enable();
            }

            return true;
        }

        public bool IsContextEnabled(InputContext context)
        {
            return _contexts.IsContextEnabled(context);
        }

        public bool ContainsContext(InputContext context)
        {
            return _contexts.Contains(context);
        }

        public void SetContext(InputContext context)
        {
            _contexts.Set(context);
            Snapshot = InputSnapshot.Empty;
        }

        public IDisposable PushContext(InputContext context, InputContextPolicy policy = InputContextPolicy.Exclusive)
        {
            return _contexts.Push(context, policy);
        }

        protected virtual InputActionAsset CreateRuntimeActions(out bool ownsActions)
        {
            ownsActions = false;
            if (_inputActions == null)
            {
                return null;
            }

            if (!_cloneActionAsset)
            {
                return _inputActions;
            }

            ownsActions = true;
            return Instantiate(_inputActions);
        }

        protected virtual void Awake()
        {
            InitializeActions();
            EnsureContextChangeSubscription();
        }

        protected virtual void OnEnable()
        {
            EnsureContextChangeSubscription();
            if (_runtimeActions == null)
            {
                InitializeActions();
            }

            _frame = 0L;
            _commands.Reset();
            SetContext(_initialContext);
        }

        // Keep Update on the InputActionCache path; action lookup and rebinding stay in low-frequency APIs.
        protected virtual void Update()
        {
            if (_runtimeActions == null)
            {
                return;
            }

            Snapshot = _cache.ReadSnapshot();
            if (_enqueuePressedCommands)
            {
                _cache.EnqueuePressedCommands(_commands, _frame, _sourceId);
            }

            _frame++;
        }

        protected virtual void OnDisable()
        {
            _cache.DisableAllMaps();
            Snapshot = InputSnapshot.Empty;
        }

        protected virtual void OnDestroy()
        {
            if (_contextChangeSubscribed)
            {
                _contexts.Changed -= ApplyContextStack;
                _contextChangeSubscribed = false;
            }

            if (_rebinding is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (_runtimeActions != null && _ownsRuntimeActions)
            {
                Destroy(_runtimeActions);
            }

            _runtimeActions = null;
            _ownsRuntimeActions = false;
        }

        private void InitializeActions()
        {
            if (_runtimeActions != null)
            {
                return;
            }

            _runtimeActions = CreateRuntimeActions(out _ownsRuntimeActions);
            if (_runtimeActions == null)
            {
                Debug.LogWarning("InputService has no InputActionAsset assigned.", this);
                return;
            }

            _cache.Bind(_runtimeActions);
            _bindingDisplay = new InputBindingDisplayService(_runtimeActions);
            _rebinding = new InputRebindingService(
                _runtimeActions,
                _bindingOverridesKey,
                _bindingDisplay.NotifyBindingsChanged);
            _rebinding.Load();
        }

        private void EnsureActionsReadyForRead()
        {
            bool needsApply = false;
            if (_runtimeActions == null)
            {
                InitializeActions();
                needsApply = true;
            }

            if (!_contextChangeSubscribed)
            {
                EnsureContextChangeSubscription();
                needsApply = true;
            }

            if (_runtimeActions == null)
                return;

            if (_contexts.Count == 0)
            {
                SetContext(_initialContext);
                return;
            }

            if (needsApply)
                ApplyContextStack();
        }

        private void EnsureContextChangeSubscription()
        {
            if (_contextChangeSubscribed)
                return;

            _contexts.Changed += ApplyContextStack;
            _contextChangeSubscribed = true;
        }

        private void ApplyContextStack()
        {
            _cache.ApplyContexts(_contexts);
        }

        private InputAction ResolveAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId) || _runtimeActions == null)
                return null;

            InputAction action = _runtimeActions.FindAction(actionId, false);
            if (action != null)
                return action;

            int slash = actionId.IndexOf('/');
            if (slash <= 0 || slash >= actionId.Length - 1)
                return null;

            InputActionMap map = _runtimeActions.FindActionMap(actionId.Substring(0, slash), false);
            return map != null ? map.FindAction(actionId.Substring(slash + 1), false) : null;
        }
    }
}
