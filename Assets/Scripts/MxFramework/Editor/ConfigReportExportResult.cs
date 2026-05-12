using System.Collections.Generic;

namespace MxFramework.Editor
{
    public sealed class ConfigReportExportResult
    {
        private readonly List<string> _files = new List<string>();

        public ConfigReportExportResult(string directory)
        {
            Directory = directory ?? string.Empty;
        }

        public string Directory { get; }
        public IReadOnlyList<string> Files => _files;

        public void AddFile(string path)
        {
            if (!string.IsNullOrEmpty(path))
                _files.Add(path);
        }
    }
}
