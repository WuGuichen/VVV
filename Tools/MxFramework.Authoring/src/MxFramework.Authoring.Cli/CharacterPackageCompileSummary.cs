using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static class CharacterPackageCompileSummaryFormats
{
    public const string Summary = "mx.characterPackageCompileSummary.v1";
}

internal sealed class CharacterPackageCompileSummaryHashes
{
    public string Algorithm { get; set; } = "sha256";
    public string SourcePackageHash { get; set; } = string.Empty;
    public string GeneratedConfigHash { get; set; } = string.Empty;
    public string GeometryBindingHash { get; set; } = string.Empty;
    public string ResourceMappingHash { get; set; } = string.Empty;
    public string WritePlanHash { get; set; } = string.Empty;
}

internal sealed class CharacterPackageCompileSummaryDiagnosticCounts
{
    public int Error { get; set; }
    public int Warning { get; set; }
}

internal sealed class CharacterPackageCompileSummaryCounts
{
    public int PackageResource { get; set; }
    public int RuntimeResource { get; set; }
    public int AnimationClip { get; set; }
    public int AnimationSet { get; set; }
    public int ConfigEntry { get; set; }
}

internal sealed class CharacterPackageCompileSummaryArtifactRef
{
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 一次导入的附加载荷。完整快照仅在显式诊断导出时填充，默认路径不序列化大对象。
/// </summary>
internal sealed class CharacterPackageImportPayloads
{
    public string CompileResultSnapshotJson { get; set; } = string.Empty;
    public string AnimationCompileResultSnapshotJson { get; set; } = string.Empty;
    public CharacterPackageCompileSummary Summary { get; set; }

    /// <summary>动画运行计划投影失败时的稳定诊断；非空表示必须在写盘前中止。</summary>
    public string AnimationRuntimePlanDiagnostic { get; set; } = string.Empty;
}

/// <summary>
/// 有界、确定性的轻量导出摘要，替代两份完整编译快照的默认落盘。
/// </summary>
/// <remarks>
/// 约束（对应实施方案 §4.1）：不含时间戳、GUID、运行序号或机器路径；相同输入与相同布局生成相同字节。
/// 本摘要不是加载清单、不是发布门禁、不是第二份身份数据库：Runtime 与 Readiness 都不得依赖它。
/// </remarks>
internal sealed class CharacterPackageCompileSummary
{
    public string Format { get; set; } = CharacterPackageCompileSummaryFormats.Summary;
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>
    /// 导出布局版本。刻意不设默认值：历史上曾默认指向布局 1 而调用点传布局 2，
    /// 新增布局时会静默给出错误值。所有构造路径必须显式给出当前布局。
    /// </summary>
    public int ExportLayoutVersion { get; set; }
    public string PackageId { get; set; } = string.Empty;
    public string PackageStableId { get; set; } = string.Empty;
    public string SourcePackageVersion { get; set; } = string.Empty;
    public string CompileStatus { get; set; } = string.Empty;
    public CharacterPackageCompileSummaryHashes CompilerHashes { get; set; } = new CharacterPackageCompileSummaryHashes();
    public CharacterPackageCompileSummaryDiagnosticCounts DiagnosticCounts { get; set; } = new CharacterPackageCompileSummaryDiagnosticCounts();
    public CharacterPackageCompileSummaryCounts Counts { get; set; } = new CharacterPackageCompileSummaryCounts();
    public List<CharacterPackageCompileSummaryArtifactRef> ArtifactRefs { get; set; } = new List<CharacterPackageCompileSummaryArtifactRef>();
}

internal static class CharacterPackageCompileSummaryBuilder
{
    /// <summary>第一批 A 完成后的导出布局版本。</summary>
    internal const int LayoutVersionA = 1;

    /// <summary>第二批 B（三组镜像收敛）完成后的导出布局版本。</summary>
    internal const int LayoutVersionB = 2;

    /// <summary>
    /// 动画资源计划改为五字段运行投影（`mx.animationResourcePlan.v2`）后的导出布局版本。
    /// </summary>
    internal const int LayoutVersionAnimationRuntimePlan = 3;

    /// <summary>
    /// 专用预览 GLB 仅保留在制作侧（运行投影各出口一致排除）后的导出布局版本。
    /// </summary>
    internal const int LayoutVersionAuthoringOnlyPreviewGlb = 4;

    internal const string RelativePath = "package_cache/compile_summary.json";

    /// <summary>000008 实测上限：8 KiB。超过即视为重复结构未收敛，不做截断。</summary>
    internal const int MaxBytes = 8 * 1024;

    private static readonly string[] ArtifactRefKinds =
    {
        "animationClipRegistry|config/animation_clip_registry.json",
        "animationResourcePlan|config/animation_resource_plan.json",
        "animationSetDefinition|config/animation_set_definition.json",
        "characterResourcePlan|config/character_resource_plan.json",
        "generatedConfigPatch|config/character_config_patch.json",
        "geometryBinding|config/geometry_binding.json",
        "resolverVerificationPlan|config/resolver_verification_plan.json",
        "resourceMapping|config/resource_catalog_mapping.json",
        "resourceValidationReport|config/resource_validation_report.json",
        "runtimeResourceCatalog|config/runtime_resource_catalog.json",
        "unityResourceCatalog|config/unity_resource_catalog.json",
        "animationPackageExpectation|package_cache/animation_package_expectation.json",
        "animationValidationReport|package_cache/animation_validation_report.json",
        "importReport|package_cache/import_report.json"
    };

    internal static CharacterPackageCompileSummary Build(
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult compileResult,
        AnimationAuthoringCompileResult animationArtifacts,
        CharacterResourcePlanCompileResult resourcePlanArtifacts,
        string sourcePackageVersion,
        int exportLayoutVersion)
    {
        var summary = new CharacterPackageCompileSummary
        {
            ExportLayoutVersion = exportLayoutVersion,
            PackageId = compileResult?.PackageId ?? string.Empty,
            PackageStableId = compileResult?.PackageStableId ?? string.Empty,
            SourcePackageVersion = sourcePackageVersion ?? string.Empty,
            CompileStatus = compileResult != null ? compileResult.Status.ToString() : string.Empty
        };

        CharacterCompilerHashSet hashes = compileResult?.Hashes;
        if (hashes != null)
        {
            summary.CompilerHashes.Algorithm = hashes.Algorithm;
            summary.CompilerHashes.SourcePackageHash = hashes.SourcePackageHash;
            summary.CompilerHashes.GeneratedConfigHash = hashes.GeneratedConfigHash;
            summary.CompilerHashes.GeometryBindingHash = hashes.GeometryBindingHash;
            summary.CompilerHashes.ResourceMappingHash = hashes.ResourceMappingHash;
            summary.CompilerHashes.WritePlanHash = hashes.WritePlanHash;
        }

        List<CharacterAuthoringValidationIssue> issues = compileResult?.GateReport?.Issues;
        if (issues != null)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    summary.DiagnosticCounts.Error++;
                else
                    summary.DiagnosticCounts.Warning++;
            }
        }

        summary.Counts.PackageResource = package?.ResourceCatalog?.Entries?.Count ?? 0;
        summary.Counts.RuntimeResource = resourcePlanArtifacts?.RuntimeResourceCatalog?.Entries?.Count ?? 0;
        summary.Counts.AnimationClip = animationArtifacts?.AnimationClipRegistry?.Clips?.Count ?? 0;
        summary.Counts.AnimationSet = animationArtifacts?.AnimationSetDefinition?.Sets?.Count ?? 0;
        summary.Counts.ConfigEntry = compileResult?.GeneratedConfigPatch?.Patch?.Entries?.Count ?? 0;

        for (int i = 0; i < ArtifactRefKinds.Length; i++)
        {
            int separator = ArtifactRefKinds[i].IndexOf('|');
            summary.ArtifactRefs.Add(new CharacterPackageCompileSummaryArtifactRef
            {
                Kind = ArtifactRefKinds[i].Substring(0, separator),
                Path = ArtifactRefKinds[i].Substring(separator + 1)
            });
        }

        summary.ArtifactRefs.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return summary;
    }

    /// <summary>确定性序列化：camelCase、缩进、单个末尾换行、无 BOM。</summary>
    internal static string Serialize(CharacterPackageCompileSummary summary, JsonSerializerOptions options)
    {
        string json = JsonSerializer.Serialize(summary, options);
        return json.EndsWith("\n", StringComparison.Ordinal) ? json : json + "\n";
    }

    /// <summary>校验摘要是否满足有界性预算；返回超出字节数（未超出为 0）。</summary>
    internal static int GetOversizeBytes(string json)
    {
        if (string.IsNullOrEmpty(json))
            return 0;
        int bytes = System.Text.Encoding.UTF8.GetByteCount(json);
        return bytes > MaxBytes ? bytes - MaxBytes : 0;
    }
}
