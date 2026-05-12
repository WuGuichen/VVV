using System;
using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public sealed class RuntimeHost : IDisposable
    {
        private readonly List<IRuntimeModule> _modules = new List<IRuntimeModule>();
        private readonly HashSet<string> _moduleIds = new HashSet<string>(StringComparer.Ordinal);
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
        }

        public RuntimeLifecycleState State { get; private set; }
        public RuntimeHostErrorPolicy ErrorPolicy => _options.ErrorPolicy;
        public long TickCount { get; private set; }
        public IReadOnlyList<RuntimeHostError> Errors => _errors;

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

            _modules.Add(module);
            _sorted = false;
        }

        public void Initialize()
        {
            EnsureState(RuntimeHostOperation.Initialize, RuntimeLifecycleState.Created);
            SortModules();

            int errorCountBefore = _errors.Count;
            for (int i = 0; i < _modules.Count; i++)
            {
                IRuntimeModule module = _modules[i];
                if (!InvokeModule(module, RuntimeHostOperation.Initialize, delegate { module.Initialize(_context); }))
                {
                    break;
                }
            }

            if (_errors.Count == errorCountBefore)
            {
                State = RuntimeLifecycleState.Initialized;
            }
        }

        public void Start()
        {
            EnsureState(RuntimeHostOperation.Start, RuntimeLifecycleState.Initialized);
            SortModules();

            int errorCountBefore = _errors.Count;
            for (int i = 0; i < _modules.Count; i++)
            {
                IRuntimeModule module = _modules[i];
                if (!InvokeModule(module, RuntimeHostOperation.Start, delegate { module.Start(_context); }))
                {
                    break;
                }
            }

            if (_errors.Count == errorCountBefore)
            {
                State = RuntimeLifecycleState.Started;
            }
        }

        public void Tick(long frameIndex, double deltaTime, double elapsedTime = 0d)
        {
            Tick(new RuntimeTickContext(frameIndex, deltaTime, elapsedTime, RuntimeTickStage.PreSimulation));
        }

        public void Tick(RuntimeTickContext context)
        {
            EnsureState(RuntimeHostOperation.Tick, RuntimeLifecycleState.Started);
            SortModules();

            for (int i = 0; i < _modules.Count; i++)
            {
                IRuntimeModule module = _modules[i];
                RuntimeTickContext moduleContext = context.WithStage(module.TickStage);
                if (!InvokeModule(module, RuntimeHostOperation.Tick, delegate { module.Tick(moduleContext); }, context.FrameIndex, module.TickStage))
                {
                    break;
                }
            }

            TickCount++;
        }

        public void Stop()
        {
            if (State == RuntimeLifecycleState.Disposed || State == RuntimeLifecycleState.Stopped)
            {
                return;
            }

            if (State == RuntimeLifecycleState.Started)
            {
                SortModules();
                for (int i = _modules.Count - 1; i >= 0; i--)
                {
                    IRuntimeModule module = _modules[i];
                    if (!InvokeModule(module, RuntimeHostOperation.Stop, delegate { module.Stop(_context); }))
                    {
                        break;
                    }
                }
            }

            State = RuntimeLifecycleState.Stopped;
        }

        public void Dispose()
        {
            if (State == RuntimeLifecycleState.Disposed)
            {
                return;
            }

            Stop();
            SortModules();
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                IRuntimeModule module = _modules[i];
                if (!InvokeModule(module, RuntimeHostOperation.Dispose, module.Dispose))
                {
                    break;
                }
            }

            State = RuntimeLifecycleState.Disposed;
        }

        public RuntimeHostDiagnostics CaptureDiagnostics()
        {
            SortModules();
            var modules = new List<RuntimeModuleDiagnostics>(_modules.Count);
            for (int i = 0; i < _modules.Count; i++)
            {
                IRuntimeModule module = _modules[i];
                modules.Add(new RuntimeModuleDiagnostics(module.ModuleId, module.TickStage, module.Priority));
            }

            return new RuntimeHostDiagnostics(State, TickCount, modules, _errors);
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

        private static int CompareModules(IRuntimeModule left, IRuntimeModule right)
        {
            int stage = left.TickStage.CompareTo(right.TickStage);
            if (stage != 0)
            {
                return stage;
            }

            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return string.Compare(left.ModuleId, right.ModuleId, StringComparison.Ordinal);
        }

        private void EnsureState(RuntimeHostOperation operation, RuntimeLifecycleState expected)
        {
            if (State == expected)
            {
                return;
            }

            throw new InvalidOperationException("RuntimeHost cannot " + operation + " while state is " + State + ". Expected " + expected + ".");
        }

        private bool InvokeModule(IRuntimeModule module, RuntimeHostOperation operation, Action action, long frameIndex = -1, RuntimeTickStage? tickStage = null)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception exception)
            {
                var error = new RuntimeHostError(module.ModuleId, operation, State, exception, frameIndex, tickStage);
                _errors.Add(error);

                if (_options.ErrorPolicy == RuntimeHostErrorPolicy.FailFast)
                {
                    throw new RuntimeHostException(error);
                }

                return _options.ErrorPolicy == RuntimeHostErrorPolicy.CollectAndContinue;
            }
        }
    }
}
