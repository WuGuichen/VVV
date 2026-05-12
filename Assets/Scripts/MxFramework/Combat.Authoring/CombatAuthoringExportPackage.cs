using System;

namespace MxFramework.Combat.Authoring
{
    public sealed class CombatAuthoringExportFile : IComparable<CombatAuthoringExportFile>
    {
        public CombatAuthoringExportFile(string path, string content, string hash)
        {
            Path = path ?? string.Empty;
            Content = content ?? string.Empty;
            Hash = hash ?? string.Empty;
        }

        public string Path { get; }

        public string Content { get; }

        public string Hash { get; }

        public int CompareTo(CombatAuthoringExportFile other)
        {
            if (other == null)
            {
                return 1;
            }

            return string.CompareOrdinal(Path, other.Path);
        }
    }

    public sealed class CombatAuthoringExportPackage
    {
        private readonly CombatAuthoringExportFile[] _files;

        public CombatAuthoringExportPackage(CombatAuthoringExportFile[] files)
        {
            if (files == null || files.Length == 0)
            {
                _files = Array.Empty<CombatAuthoringExportFile>();
                return;
            }

            _files = (CombatAuthoringExportFile[])files.Clone();
            Array.Sort(_files);
        }

        public int FileCount => _files.Length;

        public CombatAuthoringExportFile[] Files => (CombatAuthoringExportFile[])_files.Clone();

        public CombatAuthoringExportFile GetFile(int index)
        {
            return _files[index];
        }
    }

    public sealed class CombatAuthoringExportResult
    {
        public CombatAuthoringExportResult(
            bool success,
            CombatAuthoringReport validationReport,
            CombatAuthoringManifest manifest,
            CombatAuthoringExportContext context,
            CombatAuthoringExportPackage package,
            string reportText)
        {
            Success = success;
            ValidationReport = validationReport;
            Manifest = manifest;
            Context = context;
            Package = package ?? new CombatAuthoringExportPackage(null);
            ReportText = reportText ?? string.Empty;
        }

        public bool Success { get; }

        public CombatAuthoringReport ValidationReport { get; }

        public CombatAuthoringManifest Manifest { get; }

        public CombatAuthoringExportContext Context { get; }

        public CombatAuthoringExportPackage Package { get; }

        public int FileCount => Package.FileCount;

        public CombatAuthoringExportFile[] Files => Package.Files;

        public string ReportText { get; }

        public CombatAuthoringExportFile GetFile(int index)
        {
            return Package.GetFile(index);
        }
    }
}
