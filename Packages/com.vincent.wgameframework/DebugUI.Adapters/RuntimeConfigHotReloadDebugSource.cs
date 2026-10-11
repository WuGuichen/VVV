using System;
using System.Text;
using MxFramework.Config.Runtime;
using MxFramework.Diagnostics;

namespace MxFramework.DebugUI.Adapters
{
    public sealed class RuntimeConfigHotReloadDebugSource : IFrameworkDebugSource
    {
        private readonly Func<RuntimeConfigHotReloadResult> _resultFactory;

        public RuntimeConfigHotReloadDebugSource(
            Func<RuntimeConfigHotReloadResult> resultFactory,
            string name = "ConfigHotReload")
        {
            _resultFactory = resultFactory;
            Name = string.IsNullOrWhiteSpace(name) ? "ConfigHotReload" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _resultFactory != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            RuntimeConfigHotReloadResult result = _resultFactory != null ? _resultFactory() : null;
            if (result == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "reload result unavailable") });
            }

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(result)),
                    new FrameworkDebugSection("Changed Tables", FormatChangedTables(result)),
                    new FrameworkDebugSection("Errors", FormatErrors(result))
                });
        }

        private static string CreateSummary(RuntimeConfigHotReloadResult result)
        {
            return "source: " + result.SourceName
                + "\nsourceId: " + result.SourceId
                + "\nsuccess: " + (result.Success ? "true" : "false")
                + "\nhash: " + result.ContentHash
                + "\ndurationMs: " + result.DurationMilliseconds
                + "\nchanges: " + result.ChangeSet.Count;
        }

        private static string FormatChangedTables(RuntimeConfigHotReloadResult result)
        {
            if (result.ChangedTables.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < result.ChangedTables.Count; i++)
            {
                if (i > 0)
                    builder.Append('\n');
                builder.Append(result.ChangedTables[i]);
            }

            return builder.ToString();
        }

        private static string FormatErrors(RuntimeConfigHotReloadResult result)
        {
            if (result.Errors.Count == 0)
                return "none";

            return result.ErrorSummary;
        }
    }
}
