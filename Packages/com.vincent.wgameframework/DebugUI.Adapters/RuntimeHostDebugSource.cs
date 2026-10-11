using System;
using System.Text;
using MxFramework.Diagnostics;
using MxFramework.Runtime;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class RuntimeHostDebugSource : IFrameworkDebugSource
    {
        private readonly RuntimeHost _host;

        public RuntimeHostDebugSource(RuntimeHost host, string name = "RuntimeHost")
        {
            _host = host;
            Name = string.IsNullOrWhiteSpace(name) ? "RuntimeHost" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _host != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_host == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            RuntimeHostDiagnostics diagnostics = _host.CaptureDiagnostics();
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(diagnostics)),
                    new FrameworkDebugSection("Modules", CreateModules(diagnostics)),
                    new FrameworkDebugSection("Errors", CreateErrors(diagnostics))
                });
        }

        private static string CreateSummary(RuntimeHostDiagnostics diagnostics)
        {
            return "state: " + diagnostics.State
                + "\ntickCount: " + diagnostics.TickCount
                + "\nmodules: " + diagnostics.Modules.Count
                + "\nerrors: " + diagnostics.Errors.Count;
        }

        private static string CreateModules(RuntimeHostDiagnostics diagnostics)
        {
            if (diagnostics.Modules.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < diagnostics.Modules.Count; i++)
            {
                RuntimeModuleDiagnostics module = diagnostics.Modules[i];
                builder.Append(module.ModuleId)
                    .Append(" stage=")
                    .Append(module.TickStage)
                    .Append(" priority=")
                    .Append(module.Priority);
                if (i + 1 < diagnostics.Modules.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private static string CreateErrors(RuntimeHostDiagnostics diagnostics)
        {
            if (diagnostics.Errors.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < diagnostics.Errors.Count; i++)
            {
                RuntimeHostError error = diagnostics.Errors[i];
                builder.Append(error.ModuleId)
                    .Append(" operation=")
                    .Append(error.Operation)
                    .Append(" state=")
                    .Append(error.LifecycleState)
                    .Append(" frame=")
                    .Append(error.FrameIndex)
                    .Append(" message=")
                    .Append(error.Message);
                if (i + 1 < diagnostics.Errors.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
