using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class AuthoringExternalImportStagingDocument
    {
        public string ScopeId { get; set; } = string.Empty;
        public string SourceRootLabel { get; set; } = string.Empty;
        public List<AuthoringExternalImportStagingFile> Files { get; set; } = new List<AuthoringExternalImportStagingFile>();
        public long MaxFileSizeBytes { get; set; } = 512L * 1024L * 1024L;
    }

    public sealed class AuthoringExternalImportStagingFile
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string BytesBase64 { get; set; } = string.Empty;
        public string SourceHash { get; set; } = string.Empty;
    }
}
