using System.Collections.Generic;

namespace MxFramework.Authoring
{
    public sealed class ReportBundle
    {
        public ModPackageManifest Package { get; set; } = new ModPackageManifest();
        public ValidationReport Validation { get; set; } = new ValidationReport();
        public List<MergePreview> MergePreviews { get; set; } = new List<MergePreview>();
    }

    public sealed class ReportBundleIndex
    {
        public string PackageId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<string> Files { get; set; } = new List<string>();
    }
}
