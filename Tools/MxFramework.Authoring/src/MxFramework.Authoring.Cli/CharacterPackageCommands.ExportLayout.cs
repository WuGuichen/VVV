using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

/// <summary>
/// 导出布局（第一批 A）相关的 CLI 收口逻辑：诊断目标预检、迁移结果入报告、摘要发布、显式诊断导出。
/// </summary>
internal static partial class CharacterPackageCommands
{
    private const string ExportCleanupOperationKind = "exportCleanup";
    private const string ExportCleanupIssueCode = "CHARPKG_EXPORT_CLEANUP_FAILED";

    /// <summary>诊断导出禁止写入的仓库目录（相对项目根）。</summary>
    private static readonly string[] ForbiddenDiagnosticsRoots =
    {
        "Assets",
        "Packages",
        "Authoring",
        "ArtSource",
        "WeaponSource",
        "CharacterActionSource"
    };

    /// <summary>
    /// 诊断输出路径预检，必须在导入写盘之前完成。返回 null 表示通过。
    /// </summary>
    private static string ValidateDiagnosticsTarget(string diagnosticsOut, string projectRoot)
    {
        string candidate;
        try
        {
            candidate = Path.GetFullPath(diagnosticsOut);
        }
        catch (Exception exception)
        {
            return "Diagnostics path is invalid: " + exception.Message;
        }

        string resolvedProjectRoot = string.IsNullOrWhiteSpace(projectRoot)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(projectRoot);

        for (int i = 0; i < ForbiddenDiagnosticsRoots.Length; i++)
        {
            string forbidden = Path.GetFullPath(Path.Combine(resolvedProjectRoot, ForbiddenDiagnosticsRoots[i]));
            if (IsSameOrDescendantPath(candidate, forbidden))
            {
                return "Diagnostics output must not be written under " + ForbiddenDiagnosticsRoots[i] + "/.";
            }
        }

        return null;
    }

    private static bool IsSameOrDescendantPath(string candidate, string root)
    {
        string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 专用预览 GLB 在生成包中的历史相对路径。
    /// </summary>
    /// <remarks>
    /// 停止生成这些副本后，它们会立即满足通用孤立资源扫描的候选条件。
    /// 必须延期保护到 Unity 侧完成引用验证与定向删除，不能由 CLI 阶段的孤立扫描抢先删除。
    /// 路径直接取自源条目的 <c>relativePath</c>——生成副本与源保持同一包内相对路径。
    /// </remarks>
    internal static List<string> CollectDeferredPreviewPaths(CharacterResourcePackage package)
    {
        var paths = new List<string>();
        List<CharacterPackageResourceEntry> entries = package?.ResourceCatalog?.Entries;
        if (entries == null)
            return paths;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = entries[i];
            if (entry == null || !CharacterPackageResourceExportPolicy.IsAuthoringPreviewGlb(entry))
                continue;
            if (string.IsNullOrWhiteSpace(entry.RelativePath))
                continue;

            string relativePath = entry.RelativePath.Replace('\\', '/').TrimStart('/');
            if (relativePath.Length > 0 && !paths.Contains(relativePath))
                paths.Add(relativePath);
        }

        return paths;
    }

    /// <summary>
    /// 把清理结果写入导入报告的 operation / issue 机制；返回 false 表示清理失败（报告已被标记为 Failed）。
    /// </summary>
    private static bool ApplyCleanupToReport(
        CharacterUnityImportReport report,
        CharacterPackageExportCleanupResult cleanup,
        string targetRoot)
    {
        if (report == null || cleanup == null)
            return true;

        for (int i = 0; i < cleanup.Entries.Count; i++)
        {
            CharacterPackageExportCleanupEntry entry = cleanup.Entries[i];
            report.Operations.Add(new CharacterUnityImportOperation
            {
                Kind = ExportCleanupOperationKind,
                Owner = CharacterAuthoringCompilerOwnerKinds.UnityImporter,
                SourcePath = entry.RelativePath,
                TargetPath = string.IsNullOrEmpty(entry.RelativePath)
                    ? string.Empty
                    : CombineProjectPath(targetRoot, entry.RelativePath),
                WritePolicy = "Recreate",
                Action = entry.Result,
                ContentHash = entry.ContentHash,
                Message = entry.Category + ": " + entry.Reason
            });
        }

        if (cleanup.Succeeded)
            return true;

        int failed = cleanup.CountOf(CharacterPackageExportCleanupResults.Failed);
        report.ErrorCount += failed;
        report.Status = "Failed";
        report.CanSpawnAfterImport = false;

        for (int i = 0; i < cleanup.Entries.Count; i++)
        {
            CharacterPackageExportCleanupEntry entry = cleanup.Entries[i];
            if (!string.Equals(entry.Result, CharacterPackageExportCleanupResults.Failed, StringComparison.Ordinal))
                continue;

            report.Issues.Add(new CharacterAuthoringValidationIssue
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Gate = CharacterAuthoringValidationGate.ImportBlocked,
                Code = ExportCleanupIssueCode,
                SourcePath = "package_cache",
                SourceObjectPath = entry.RelativePath,
                Field = ExportCleanupOperationKind,
                Message = entry.Reason,
                SuggestedFix = "Inspect the migration backup and retry the import; do not hand-delete exported files."
            });
        }

        return false;
    }

    /// <summary>
    /// 发布轻量摘要：同目录临时文件 + 覆盖替换，保持既有 <c>.meta</c> 不变（不删除重建，避免 Unity 重分配 GUID）。
    /// 返回 false 表示失败（含超出有界预算），调用方必须返回非成功。
    /// </summary>
    private static bool TryWriteCompileSummary(
        string projectRoot,
        string targetRoot,
        CharacterPackageCompileSummary summary,
        JsonSerializerOptions options,
        out bool written)
    {
        written = false;
        if (summary == null)
            return false;

        string json = CharacterPackageCompileSummaryBuilder.Serialize(summary, options);
        int oversize = CharacterPackageCompileSummaryBuilder.GetOversizeBytes(json);
        if (oversize > 0)
        {
            Console.Error.WriteLine(
                "compileSummary=Oversize (exceeds " + CharacterPackageCompileSummaryBuilder.MaxBytes
                + " bytes by " + oversize + "); not written. Reduce duplicated structure instead of truncating diagnostics.");
            return false;
        }

        string projectPath = CombineProjectPath(targetRoot, CharacterPackageCompileSummaryBuilder.RelativePath);
        string absolutePath = Path.GetFullPath(CombineProjectPath(projectRoot, projectPath));
        string directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        byte[] bytes = new UTF8Encoding(false).GetBytes(json);
        if (File.Exists(absolutePath) && FileBytesEqual(File.ReadAllBytes(absolutePath), bytes))
        {
            Console.WriteLine("compileSummary=Skipped (" + projectPath + ")");
            return true;
        }

        try
        {
            string temporary = absolutePath + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, absolutePath, overwrite: true);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("compileSummary=Failed (" + exception.GetType().Name + ": " + exception.Message + ")");
            return false;
        }

        written = true;
        Console.WriteLine("compileSummary=" + projectPath);
        return true;
    }

    private static bool FileBytesEqual(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 显式诊断导出：写同一次导入的完整对象与导入报告。仅在调用方显式请求时执行。
    /// </summary>
    private static void WriteCharacterPackageDiagnostics(
        string diagnosticsOut,
        CharacterResourcePackage package,
        CharacterAuthoringCompileResult result,
        CharacterPackageImportPayloads payloads,
        string reportJson,
        string reportText,
        JsonSerializerOptions options)
    {
        Directory.CreateDirectory(diagnosticsOut);
        var utf8 = new UTF8Encoding(false);

        if (!string.IsNullOrEmpty(payloads?.CompileResultSnapshotJson))
            File.WriteAllText(Path.Combine(diagnosticsOut, "compile_result.json"), payloads.CompileResultSnapshotJson, utf8);
        if (!string.IsNullOrEmpty(payloads?.AnimationCompileResultSnapshotJson))
            File.WriteAllText(Path.Combine(diagnosticsOut, "animation_compile_result.json"), payloads.AnimationCompileResultSnapshotJson, utf8);
        File.WriteAllText(Path.Combine(diagnosticsOut, "import_report.json"), reportJson, utf8);
        File.WriteAllText(Path.Combine(diagnosticsOut, "import_report.txt"), reportText, utf8);

        var manifest = new
        {
            format = "mx.characterPackageDiagnostics.v1",
            schemaVersion = "1.0",
            packageId = result != null ? result.PackageId : string.Empty,
            sourcePackageVersion = package?.Manifest != null ? package.Manifest.Version : string.Empty,
            stages = new[] { "compiler", "preparedAnimation" },
            compilerHashes = new
            {
                algorithm = result?.Hashes != null ? result.Hashes.Algorithm : "sha256",
                sourcePackageHash = result?.Hashes != null ? result.Hashes.SourcePackageHash : string.Empty,
                generatedConfigHash = result?.Hashes != null ? result.Hashes.GeneratedConfigHash : string.Empty,
                geometryBindingHash = result?.Hashes != null ? result.Hashes.GeometryBindingHash : string.Empty,
                resourceMappingHash = result?.Hashes != null ? result.Hashes.ResourceMappingHash : string.Empty,
                writePlanHash = result?.Hashes != null ? result.Hashes.WritePlanHash : string.Empty
            },
            note = "Snapshots are the in-memory objects at stage compiler / preparedAnimation; they are not the identity-converted final JSON written to the package."
        };

        File.WriteAllText(
            Path.Combine(diagnosticsOut, "diagnostics_manifest.json"),
            JsonSerializer.Serialize(manifest, options) + "\n",
            utf8);

        Console.WriteLine("diagnosticsOut=" + diagnosticsOut.Replace('\\', '/'));
    }
}
