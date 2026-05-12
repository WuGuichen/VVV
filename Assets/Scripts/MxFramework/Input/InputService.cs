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

        public InputSnapshot Snapshot
        {
            get
            {
                if (_runtimeActions != null && isActiveAndEnabled)
                {
                    return _cache.ReadSnapshot();
                }

                return _snapshot;
            }

            private set => _snapshot = value;
        }
        public InputCommandQueue Commands => _commands;
        public InputContextStack Contexts => _contexts;
        public InputContext CurrentContext => _contexts.ActiveContext;
        public InputActionAsset Actions => _runtimeActions;
        public IInputRebindingService Rebinding => _rebinding;
        public InputBindingDisplayService BindingDisplay => _bindingDisplay;

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
            _contexts.Changed += ApplyContextStack;
        }

        protected virtual void OnEnable()
        {
            if (_runtimeActions == null)
            {
                InitializeActions();
            }

            _frame = 0L;
            _commands.Reset();
            SetContext(_initialContext);
        }

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
            _contexts.Changed -= ApplyContextStack;

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
            _rebinding = new InputRebindingService(_runtimeActions, _bindingOverridesKey);
            _rebinding.Load();
            _bindingDisplay = new InputBindingDisplayService(_runtimeActions);
        }

        private void ApplyContextStack()
        {
            _cache.ApplyContexts(_contexts);
        }
    }
}
