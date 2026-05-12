using System;
using System.Collections.Generic;

namespace MxFramework.Config.Runtime
{
    public sealed class RuntimeModDiagnosticSummary
    {
        public int Discovered { get; set; }
        public int Valid { get; set; }
        public int Invalid { get; set; }
        public int Enabled { get; set; }
        public int Ordered { get; set; }
        public int Skipped { get; set; }
        public int Overrides { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
    }

    public sealed class RuntimeModDiagnosticLoadoutSummary
    {
        public bool IsDefaultAll { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public IReadOnlyList<string> EnabledPackageKeys { get; set; } = Array.Empty<string>();
    }

    public sealed class RuntimeModDiagnosticPackageSummary
    {
        public string PackageKey { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string ContainerPath { get; set; } = string.Empty;
        public string PackageRelativePath { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public bool IsEnabled { get; set; }
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    public sealed class RuntimeModDiagnosticLoadPlanItem
    {
        public string PackageKey { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string SkipReason { get; set; } = string.Empty;
        public int OrderIndex { get; set; } = -1;
    }

    public sealed class RuntimeModDiagnosticOverride
    {
        public string ConfigType { get; set; } = string.Empty;
        public int Id { get; set; }
        public IReadOnlyList<string> PackageChain { get; set; } = Array.Empty<string>();
        public string WinnerPackageKey { get; set; } = string.Empty;
        public string WinnerPackageId { get; set; } = string.Empty;
    }

    public sealed class RuntimeModDiagnosticIssue
    {
        public string Severity { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class RuntimeModDiagnosticSnapshot
    {
        public const string ExpectedFormat = "mx.modDiagnosticSnapshot.v1";

        public string Format { get; set; } = ExpectedFormat;
        public string GeneratedUtc { get; set; } = string.Empty;
        public bool Success { get; set; }
        public RuntimeModDiagnosticSummary Summary { get; set; } = new RuntimeModDiagnosticSummary();
        public RuntimeModDiagnosticLoadoutSummary Loadout { get; set; } = new RuntimeModDiagnosticLoadoutSummary();
        public IReadOnlyList<RuntimeModDiagnosticPackageSummary> Packages { get; set; } = Array.Empty<RuntimeModDiagnosticPackageSummary>();
        public IReadOnlyList<RuntimeModDiagnosticLoadPlanItem> LoadPlan { get; set; } = Array.Empty<RuntimeModDiagnosticLoadPlanItem>();
        public IReadOnlyList<RuntimeModDiagnosticOverride> Overrides { get; set; } = Array.Empty<RuntimeModDiagnosticOverride>();
        public IReadOnlyList<RuntimeModDiagnosticIssue> Errors { get; set; } = Array.Empty<RuntimeModDiagnosticIssue>();
        public IReadOnlyList<RuntimeModDiagnosticIssue> Warnings { get; set; } = Array.Empty<RuntimeModDiagnosticIssue>();
    }
}
