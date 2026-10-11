using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeHost : IDisposable
    {
        private readonly List<RuntimeHostModuleLifecycle> _modules = new List<RuntimeHostModuleLifecycle>();
        private readonly List<RuntimeHostModuleLifecycle> _registrationOrder = new List<RuntimeHostModuleLifecycle>();
        private readonly HashSet<string> _moduleIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<IRuntimeModule> _registeredModuleInstances = new List<IRuntimeModule>();
        private readonly List<RuntimeHostError> _errors = new List<RuntimeHostError>();
        private readonly RuntimeHostOptions _options;
        private readonly RuntimeHostContext _context;
        private bool _sorted;

        public RuntimeHost()
            : this(new RuntimeHostOptions())
        {
        }

        public RuntimeHost(RuntimeHostOptions options)
        {
            _options = options ?? new RuntimeHostOptions();
            if (_options.Services == null)
            {
                _options.Services = new RuntimeServiceRegistry();
            }

            _context = new RuntimeHostContext(this, _options.Services);
            State = RuntimeLifecycleState.Created;
            LastTickAttemptFrame = -1L;
        }

        public RuntimeLifecycleState State { get; private set; }
        public RuntimeHostErrorPolicy ErrorPolicy => _options.ErrorPolicy;
        public long TickCount { get; private set; }
        public IReadOnlyList<RuntimeHostError> Errors => _errors;

        /// <summary>Gets whether this host is currently executing a Tick attempt.</summary>
        /// <remarks>NoAlloc. This is an observation property and does not add a thread-safety guarantee.</remarks>
        public bool IsTickInProgress { get; private set; }

        /// <summary>Gets whether this host has started at least one Tick attempt.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool HasTickAttempt { get; private set; }

        /// <summary>Gets the frame of the latest Tick attempt, or -1 before the first attempt.</summary>
        /// <remarks>NoAlloc. A failed newer attempt replaces the frame of an earlier successful attempt.</remarks>
        public long LastTickAttemptFrame { get; private set; }

        /// <summary>Gets whether the latest Tick attempt completed without adding a RuntimeHostError.</summary>
        /// <remarks>NoAlloc. This value is false before the first attempt and throughout an in-progress attempt.</remarks>
        public bool LastTickAttemptSucceeded { get; private set; }

        /// <summary>Registers a module and transfers its lifetime ownership to this host.</summary>
        /// <remarks>LowFreqAlloc. After successful registration, the host offers the module exactly one final Dispose attempt.</remarks>
        public void RegisterModule(IRuntimeModule module)
        {
            EnsureState(RuntimeHostOperation.Register, RuntimeLifecycleState.Created);
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (string.IsNullOrWhiteSpace(module.ModuleId))
            {
                throw new ArgumentException("Runtime module id cannot be empty.", nameof(module));
            }

            if (!_moduleIds.Add(module.ModuleId))
            {
                throw new InvalidOperationException("Runtime module is already registered: " + module.ModuleId);
            }

            var lifecycle = new RuntimeHostModuleLifecycle(module);
            _modules.Add(lifecycle);
            _registrationOrder.Add(lifecycle);
            _sorted = false;
            _registeredModuleInstances.Add(module);
        }

        public void Initialize()
        {
            EnsureState(RuntimeHostOperation.Initialize, RuntimeLifecycleState.Created);
            SortModules();

            RuntimeHostError firstForwardError = null;
            for (int i = 0; i < _modules.Count; i++)
            {
                RuntimeHostError error;
                if (!InvokeInitializeModule(_modules[i], out error))
                {
                    if (firstForwardError == null)
                    {
                        firstForwardError = error;
                    }

                    if (_options.ErrorPolicy != RuntimeHostErrorPolicy.CollectAndContinue)
                    {
                        break;
                    }
                }
            }

            if (firstForwardError == null)
            {
                State = RuntimeLifecycleState.Initialized;
                return;
            }

            State = RuntimeLifecycleState.Faulted;
            UnwindFaultedLifecycle();
            ThrowIfFailFast(firstForwardError);
        }

        public void Start()
        {
            EnsureState(RuntimeHostOperation.Start, RuntimeLifecycleState.Initialized);
            SortModules();

            RuntimeHostError firstForwardError = null;
            for (int i = 0; i < _modules.Count; i++)
            {
                RuntimeHostError error;
                if (!InvokeStartModule(_modules[i], out error))
                {
                    if (firstForwardError == null)
                    {
                        firstForwardError = error;
                    }

                    if (_options.ErrorPolicy != RuntimeHostErrorPolicy.CollectAndContinue)
                    {
                        break;
                    }
                }
            }

            if (firstForwardError == null)
            {
                State = RuntimeLifecycleState.Started;
                return;
            }

            State = RuntimeLifecycleState.Faulted;
            UnwindFaultedLifecycle();
            ThrowIfFailFast(firstForwardError);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(long frameIndex, double deltaTime, double elapsedTime = 0d)
        {
            Tick(new RuntimeTickContext(frameIndex, deltaTime, elapsedTime, RuntimeTickStage.PreSimulation));
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public void Tick(RuntimeTickContext context)
        {
            EnsureState(RuntimeHostOperation.Tick, RuntimeLifecycleState.Started);
            SortModules();

            HasTickAttempt = true;
            LastTickAttemptFrame = context.FrameIndex;
            LastTickAttemptSucceeded = false;
            IsTickInProgress = true;
            int errorCountBefore = _errors.Count;

            try
            {
                for (int i = 0; i < _modules.Count; i++)
                {
                    IRuntimeModule module = _modules[i].Module;
                    RuntimeTickContext moduleContext = context.WithStage(module.TickStage);
                    if (!InvokeTickModule(module, moduleContext))
                    {
                        break;
                    }
                }

                TickCount++;
                if (_errors.Count == errorCountBefore)
                {
                    LastTickAttemptSucceeded = true;
                }
            }
            finally
            {
                IsTickInProgress = false;
            }
        }

        public void Stop()
        {
            if (State == RuntimeLifecycleState.Disposed ||
                State == RuntimeLifecycleState.Stopped ||
                State == RuntimeLifecycleState.Faulted)
            {
                return;
            }

            RuntimeHostError firstError = null;
            if (State == RuntimeLifecycleState.Started)
            {
                SortModules();
                firstError = StopEnteredModules();
            }

            State = firstError == null
                ? RuntimeLifecycleState.Stopped
                : RuntimeLifecycleState.Faulted;
            ThrowIfFailFast(firstError);
        }

        public void Dispose()
        {
            if (State == RuntimeLifecycleState.Disposed)
            {
                return;
            }

            SortModules();
            RuntimeHostError firstError = null;

            if (State == RuntimeLifecycleState.Started)
            {
                firstError = StopEnteredModules();
                State = firstError == null
                    ? RuntimeLifecycleState.Stopped
                    : RuntimeLifecycleState.Faulted;
            }
            else if (State != RuntimeLifecycleState.Faulted)
            {
                State = RuntimeLifecycleState.Stopped;
            }

            RuntimeHostError disposeError = DisposeOwnedModules();
            if (firstError == null)
            {
                firstError = disposeError;
            }

            State = RuntimeLifecycleState.Disposed;
            ThrowIfFailFast(firstError);
        }

        public RuntimeHostDiagnostics CaptureDiagnostics()
        {
            SortModules();
            var modules = new List<RuntimeModuleDiagnostics>(_modules.Count);
            for (int i = 0; i < _modules.Count; i++)
            {
                IRuntimeModule module = _modules[i].Module;
                modules.Add(new RuntimeModuleDiagnostics(module.ModuleId, module.TickStage, module.Priority));
            }

            return new RuntimeHostDiagnostics(State, TickCount, modules, _errors);
        }

        internal bool ContainsModuleInstance(IRuntimeModule module)
        {
            if (module == null)
            {
                return false;
            }

            for (int i = 0; i < _registeredModuleInstances.Count; i++)
            {
                if (ReferenceEquals(_registeredModuleInstances[i], module))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Validates the exact AppFlow controller/module pump registration.</summary>
        /// <remarks>
        /// NoAlloc. Pass the expected module for RuntimeModule ownership, or null for Standalone ownership.
        /// The result is false when another wrapper for the same controller is registered.
        /// </remarks>
        internal bool HasExactAppFlowPumpRegistration(
            AppFlowController controller,
            AppFlowRuntimeModule expectedModule)
        {
            if (controller == null)
            {
                return false;
            }

            int matchingCount = 0;
            bool expectedFound = false;
            for (int i = 0; i < _registeredModuleInstances.Count; i++)
            {
                if (_registeredModuleInstances[i] is AppFlowRuntimeModule module &&
                    ReferenceEquals(module.Controller, controller))
                {
                    matchingCount++;
                    if (ReferenceEquals(module, expectedModule))
                    {
                        expectedFound = true;
                    }
                }
            }

            return expectedModule == null
                ? matchingCount == 0
                : matchingCount == 1 && expectedFound;
        }

        /// <summary>Validates the exact SceneFlow controller/module pump registration.</summary>
        /// <remarks>
        /// NoAlloc. Pass the expected module for RuntimeModule ownership, or null for Standalone ownership.
        /// The result is false when another wrapper for the same controller is registered.
        /// </remarks>
        internal bool HasExactSceneFlowPumpRegistration(
            SceneFlowController controller,
            SceneFlowRuntimeModule expectedModule)
        {
            if (controller == null) return false;

            int matchingCount = 0;
            bool expectedFound = false;
            for (int i = 0; i < _registeredModuleInstances.Count; i++)
            {
                if (_registeredModuleInstances[i] is SceneFlowRuntimeModule module &&
                    ReferenceEquals(module.Controller, controller))
                {
                    matchingCount++;
                    if (ReferenceEquals(module, expectedModule)) expectedFound = true;
                }
            }

            return expectedModule == null
                ? matchingCount == 0
                : matchingCount == 1 && expectedFound;
        }

        private void SortModules()
        {
            if (_sorted)
            {
                return;
            }

            _modules.Sort(CompareModules);
            _sorted = true;
        }

        private static int CompareModules(RuntimeHostModuleLifecycle left, RuntimeHostModuleLifecycle right)
        {
            IRuntimeModule leftModule = left.Module;
            IRuntimeModule rightModule = right.Module;
            int stage = leftModule.TickStage.CompareTo(rightModule.TickStage);
            if (stage != 0)
            {
                return stage;
            }

            int priority = leftModule.Priority.CompareTo(rightModule.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.Compare(leftModule.ModuleId, rightModule.ModuleId, StringComparison.Ordinal);
        }

        private void EnsureState(RuntimeHostOperation operation, RuntimeLifecycleState expected)
        {
            if (State == expected)
            {
                return;
            }

            throw new InvalidOperationException("RuntimeHost cannot " + operation + " while state is " + State + ". Expected " + expected + ".");
        }

        private bool InvokeInitializeModule(
            RuntimeHostModuleLifecycle lifecycle,
            out RuntimeHostError error)
        {
            lifecycle.InitializeEntered = true;
            try
            {
                lifecycle.Module.Initialize(_context);
                lifecycle.Initialized = true;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = RecordModuleException(lifecycle.Module, RuntimeHostOperation.Initialize, exception);
                return false;
            }
        }

        private bool InvokeStartModule(
            RuntimeHostModuleLifecycle lifecycle,
            out RuntimeHostError error)
        {
            lifecycle.StartEntered = true;
            try
            {
                lifecycle.Module.Start(_context);
                lifecycle.Started = true;
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = RecordModuleException(lifecycle.Module, RuntimeHostOperation.Start, exception);
                return false;
            }
        }

        private bool InvokeTickModule(IRuntimeModule module, RuntimeTickContext context)
        {
            try
            {
                module.Tick(context);
                return true;
            }
            catch (Exception exception)
            {
                return HandleModuleException(module, RuntimeHostOperation.Tick, exception, context.FrameIndex, context.Stage);
            }
        }

        private bool InvokeStopModule(IRuntimeModule module, out RuntimeHostError error)
        {
            try
            {
                module.Stop(_context);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = RecordModuleException(module, RuntimeHostOperation.Stop, exception);
                return false;
            }
        }

        private bool InvokeDisposeModule(IRuntimeModule module, out RuntimeHostError error)
        {
            try
            {
                module.Dispose();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = RecordModuleException(module, RuntimeHostOperation.Dispose, exception);
                return false;
            }
        }

        private void UnwindFaultedLifecycle()
        {
            StopEnteredModules();
            DisposeOwnedModules();
        }

        private RuntimeHostError StopEnteredModules()
        {
            RuntimeHostError firstError = null;
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                RuntimeHostModuleLifecycle lifecycle = _modules[i];
                if (!lifecycle.StartEntered || lifecycle.StopAttempted)
                {
                    continue;
                }

                lifecycle.StopAttempted = true;
                RuntimeHostError error;
                if (!InvokeStopModule(lifecycle.Module, out error) && firstError == null)
                {
                    firstError = error;
                }
            }

            return firstError;
        }

        private RuntimeHostError DisposeOwnedModules()
        {
            RuntimeHostError firstError = null;

            // Entered modules unwind in the exact reverse order in which Initialize was entered.
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                RuntimeHostModuleLifecycle lifecycle = _modules[i];
                if (!lifecycle.InitializeEntered || lifecycle.DisposeAttempted)
                {
                    continue;
                }

                lifecycle.DisposeAttempted = true;
                RuntimeHostError error;
                if (!InvokeDisposeModule(lifecycle.Module, out error) && firstError == null)
                {
                    firstError = error;
                }
            }

            // Registration transfers lifetime ownership even when Initialize was never entered.
            for (int i = _registrationOrder.Count - 1; i >= 0; i--)
            {
                RuntimeHostModuleLifecycle lifecycle = _registrationOrder[i];
                if (lifecycle.DisposeAttempted)
                {
                    continue;
                }

                lifecycle.DisposeAttempted = true;
                RuntimeHostError error;
                if (!InvokeDisposeModule(lifecycle.Module, out error) && firstError == null)
                {
                    firstError = error;
                }
            }

            return firstError;
        }

        private void ThrowIfFailFast(RuntimeHostError error)
        {
            if (error != null && _options.ErrorPolicy == RuntimeHostErrorPolicy.FailFast)
            {
                throw new RuntimeHostException(error);
            }
        }

        private bool HandleModuleException(
            IRuntimeModule module,
            RuntimeHostOperation operation,
            Exception exception,
            long frameIndex = -1,
            RuntimeTickStage? tickStage = null)
        {
            RuntimeHostError error = RecordModuleException(module, operation, exception, frameIndex, tickStage);

            if (_options.ErrorPolicy == RuntimeHostErrorPolicy.FailFast)
            {
                throw new RuntimeHostException(error);
            }

            return _options.ErrorPolicy == RuntimeHostErrorPolicy.CollectAndContinue;
        }

        private RuntimeHostError RecordModuleException(
            IRuntimeModule module,
            RuntimeHostOperation operation,
            Exception exception,
            long frameIndex = -1,
            RuntimeTickStage? tickStage = null)
        {
            var error = new RuntimeHostError(module.ModuleId, operation, State, exception, frameIndex, tickStage);
            _errors.Add(error);
            return error;
        }

        private sealed class RuntimeHostModuleLifecycle
        {
            public RuntimeHostModuleLifecycle(IRuntimeModule module)
            {
                Module = module;
            }

            public IRuntimeModule Module { get; }
            public bool InitializeEntered { get; set; }
            public bool Initialized { get; set; }
            public bool StartEntered { get; set; }
            public bool Started { get; set; }
            public bool StopAttempted { get; set; }
            public bool DisposeAttempted { get; set; }
        }
    }
}
