using System.Collections.Generic;

namespace MxFramework.Diagnostics
{
    public sealed class FrameworkDebugSnapshot
    {
        private readonly List<FrameworkDebugSection> _sections;

        public FrameworkDebugSnapshot(string sourceName, FrameworkDebugMode mode, IReadOnlyList<FrameworkDebugSection> sections)
        {
            SourceName = sourceName ?? string.Empty;
            Mode = mode;
            _sections = sections != null ? new List<FrameworkDebugSection>(sections) : new List<FrameworkDebugSection>();
        }

        public string SourceName { get; }
        public FrameworkDebugMode Mode { get; }
        public IReadOnlyList<FrameworkDebugSection> Sections => _sections;
    }
}
