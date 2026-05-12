using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public readonly struct RuntimeModuleDiagnostics
    {
        public RuntimeModuleDiagnostics(string moduleId, RuntimeTickStage tickStage, int priority)
        {
            ModuleId = moduleId ?? string.Empty;
            TickStage = tickStage;
            Priority = priority;
        }

        public string ModuleId { get; }
        public RuntimeTickStage TickStage { get; }
        public int Priority { get; }
    }

    public sealed class RuntimeHostDiagnostics
    {
        private readonly List<RuntimeModuleDiagnostics> _modules;
        private readonly List<RuntimeHostError> _errors;

        public RuntimeHostDiagnostics(
            RuntimeLifecycleState state,
            long tickCount,
            IReadOnlyList<RuntimeModuleDiagnostics> modules,
            IReadOnlyList<RuntimeHostError> errors)
        {
            State = state;
            TickCount = tickCount;
            _modules = modules != null ? new List<RuntimeModuleDiagnostics>(modules) : new List<RuntimeModuleDiagnostics>();
            _errors = errors != null ? new List<RuntimeHostError>(errors) : new List<RuntimeHostError>();
        }

        public RuntimeLifecycleState State { get; }
        public long TickCount { get; }
        public IReadOnlyList<RuntimeModuleDiagnostics> Modules => _modules;
        public IReadOnlyList<RuntimeHostError> Errors => _errors;
        public RuntimeHostError LastError => _errors.Count == 0 ? null : _errors[_errors.Count - 1];
    }
}
