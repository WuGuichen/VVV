using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace MxFramework.Authoring.Cli;

internal static class CharacterPackageExportCleanupResults
{
    public const string Deleted = "Deleted";
    public const string Missing = "Missing";
    public const string Skipped = "Skipped";
    public const string Conflict = "Conflict";
    public const string Failed = "Failed";

    /// <summary>候选命中受保护路径：保留文件，等待 Unity 侧引用验证后的定向删除。</summary>
    public const string Deferred = "Deferred";
}

internal sealed class CharacterPackageExportCleanupEntry
{
    public string RelativePath { get; set; } = string.Empty;
    public string MetaRelativePath { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string BackupRelativePath { get; set; } = string.Empty;
}

internal sealed class CharacterPackageExportCleanupResult
{
    public string BackupRoot { get; set; } = string.Empty;
    public bool BackupIsVolatile { get; set; }
    public List<CharacterPackageExportCleanupEntry> Entries { get; set; } = new List<CharacterPackageExportCleanupEntry>();

    public bool Succeeded
    {
        get
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i].Result, CharacterPackageExportCleanupResults.Failed, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }

    public int CountOf(string result)
    {
        int count = 0;
        for (int i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Result, result, StringComparison.Ordinal))
                count++;
        }
        return count;
    }
}

/// <summary>
/// 定向导出产物清理器。只拥有本文件列明的废弃路径，不做目录递归、不做通配符匹配。
/// </summary>
/// <remarks>
/// 对应实施方案 §3.1 / §6.1 / §6.2。判定孤立资源时使用本次导入报告 <c>resourceFile</c> 操作集合
/// 作为"已声明"的权威清单，逐项比较规范化后的完整相对路径。
/// </remarks>
internal static class CharacterPackageExportCleanup
{
    internal const string CategoryDeprecatedSnapshot = "deprecatedSnapshot";
    internal const string CategoryDeprecatedMirror = "deprecatedMirror";
    internal const string CategoryOrphanResource = "orphanResource";
    private const string CompileResultRelativePath = "package_cache/compile_result.json";
    private const string AnimationCompileResultRelativePath = "package_cache/animation_compile_result.json";

    /// <summary>孤立资源只在这两个目录内判定；不扩展到 Unity 侧生成资产目录。</summary>
    private static readonly string[] OrphanScanRoots = { "resources", "previews" };

    private static readonly string[] OrphanAllowedExtensions =
    {
        ".fbx", ".glb", ".gltf", ".png", ".tga", ".jpg", ".jpeg", ".wav", ".ogg", ".anim", ".json", ".bytes"
    };

    private static readonly string[] DeprecatedSnapshotPaths =
    {
        CompileResultRelativePath,
        AnimationCompileResultRelativePath
    };

    /// <summary>第二批 B 收敛后的重复镜像路径（第一批 A 尚未生效时不存在，属正常幂等跳过）。</summary>
    private static readonly string[] DeprecatedMirrorPaths =
    {
        "generated/character_application_config_patch.json",
        "generated/character_geometry_binding.json",
        "generated/character_resource_mapping.json"
    };

    internal static CharacterPackageExportCleanupResult Run(
        string packageRootFullPath,
        string packageId,
        string targetRootProjectRelative,
        CharacterUnityImportReport report,
        bool dryRun,
        string backupRoot,
        string runId,
        IReadOnlyCollection<string> protectedRelativePaths = null)
    {
        var result = new CharacterPackageExportCleanupResult();
        if (string.IsNullOrWhiteSpace(packageRootFullPath) || !Directory.Exists(packageRootFullPath))
        {
            result.Entries.Add(new CharacterPackageExportCleanupEntry
            {
                RelativePath = string.Empty,
                Category = CategoryDeprecatedSnapshot,
                Result = CharacterPackageExportCleanupResults.Failed,
                Reason = "Package root was not found: " + (packageRootFullPath ?? string.Empty)
            });
            return result;
        }

        string root = Path.GetFullPath(packageRootFullPath);
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;

        bool useDefaultBackupRoot = string.IsNullOrWhiteSpace(backupRoot);
        result.BackupIsVolatile = useDefaultBackupRoot;
        string effectiveBackupRoot = useDefaultBackupRoot
            ? Path.Combine("Temp", "CharacterPackageExportMigration")
            : backupRoot;
        string backupDirectory = Path.Combine(effectiveBackupRoot, SafeSegment(packageId), SafeSegment(runId));
        result.BackupRoot = backupDirectory.Replace('\\', '/');

        var candidates = new List<Candidate>();
        CollectDeprecatedSnapshots(root, rootPrefix, packageId, candidates);
        CollectOrphanResources(root, rootPrefix, report, targetRootProjectRelative, candidates);

        var backupManifest = new List<string>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            var entry = new CharacterPackageExportCleanupEntry
            {
                RelativePath = candidate.RelativePath,
                MetaRelativePath = candidate.MetaRelativePath,
                Category = candidate.Category
            };
            result.Entries.Add(entry);

            if (candidate.RejectionReason != null)
            {
                entry.Result = CharacterPackageExportCleanupResults.Skipped;
                entry.Reason = candidate.RejectionReason;
                continue;
            }

            // 受保护候选：停止写入后它会立即变成孤立候选，但必须等 Unity 侧引用验证
            // 与定向删除完成，不能由通用孤立扫描抢先删除。
            if (IsProtected(candidate, protectedRelativePaths))
            {
                entry.Result = CharacterPackageExportCleanupResults.Deferred;
                entry.Reason = "Protected by an in-flight export migration; preserved until Unity reference verification completes.";
                continue;
            }

            bool hasFile = File.Exists(candidate.FullPath);
            bool hasMeta = File.Exists(candidate.MetaFullPath);
            if (!hasFile && !hasMeta)
            {
                entry.Result = CharacterPackageExportCleanupResults.Missing;
                entry.Reason = "Target file and meta are already absent (idempotent skip).";
                continue;
            }

            if (dryRun)
            {
                entry.Result = CharacterPackageExportCleanupResults.Skipped;
                entry.Reason = "Dry run: no files were modified.";
                continue;
            }

            try
            {
                if (hasFile)
                {
                    byte[] bytes = File.ReadAllBytes(candidate.FullPath);
                    string hash = CharacterPackageHashUtility.ComputeTextSha256(Encoding.UTF8.GetString(bytes));
                    entry.ContentHash = hash;

                    if (!string.IsNullOrEmpty(candidate.ExpectedContentHash)
                        && !string.Equals(candidate.ExpectedContentHash, hash, StringComparison.Ordinal))
                    {
                        entry.Result = CharacterPackageExportCleanupResults.Conflict;
                        entry.Reason = "Content changed after pre-check; file was preserved.";
                        continue;
                    }

                    string backupPath = BackupFile(backupDirectory, candidate.RelativePath, bytes);
                    entry.BackupRelativePath = backupPath;
                    backupManifest.Add(candidate.RelativePath + "|" + hash + "|" + bytes.Length);

                    if (hasMeta)
                    {
                        byte[] metaBytes = File.ReadAllBytes(candidate.MetaFullPath);
                        BackupFile(backupDirectory, candidate.MetaRelativePath, metaBytes);
                        backupManifest.Add(candidate.MetaRelativePath + "|" + CharacterPackageHashUtility.ComputeTextSha256(Encoding.UTF8.GetString(metaBytes)) + "|" + metaBytes.Length);
                    }
                }

                if (hasMeta)
                    File.Delete(candidate.MetaFullPath);
                if (hasFile)
                    File.Delete(candidate.FullPath);

                entry.Result = CharacterPackageExportCleanupResults.Deleted;
                entry.Reason = hasFile ? "Removed deprecated export artifact and its meta." : "Removed orphaned meta only.";
            }
            catch (Exception exception)
            {
                entry.Result = CharacterPackageExportCleanupResults.Failed;
                entry.Reason = exception.GetType().Name + ": " + exception.Message;
            }
        }

        if (!dryRun && backupManifest.Count > 0)
        {
            try
            {
                Directory.CreateDirectory(backupDirectory);
                File.WriteAllText(
                    Path.Combine(backupDirectory, "backup_manifest.txt"),
                    string.Join("\n", backupManifest) + "\n",
                    new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                result.Entries.Add(new CharacterPackageExportCleanupEntry
                {
                    RelativePath = "backup_manifest.txt",
                    Category = "backupManifest",
                    Result = CharacterPackageExportCleanupResults.Failed,
                    Reason = "Backup manifest write failed: " + exception.Message
                });
            }
        }

        return result;
    }

    private static bool IsProtected(Candidate candidate, IReadOnlyCollection<string> protectedRelativePaths)
    {
        if (candidate == null || protectedRelativePaths == null || protectedRelativePaths.Count == 0)
            return false;

        foreach (string protectedPath in protectedRelativePaths)
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
                continue;

            string normalized = protectedPath.Replace('\\', '/').TrimStart('/');
            if (string.Equals(normalized, candidate.RelativePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed class Candidate
    {
        public string RelativePath = string.Empty;
        public string MetaRelativePath = string.Empty;
        public string Category = string.Empty;
        public string FullPath = string.Empty;
        public string MetaFullPath = string.Empty;
        public string ExpectedContentHash = string.Empty;
        public string RejectionReason;
    }

    private static void CollectDeprecatedSnapshots(string root, string rootPrefix, string packageId, List<Candidate> candidates)
    {
        for (int i = 0; i < DeprecatedSnapshotPaths.Length; i++)
        {
            string relativePath = DeprecatedSnapshotPaths[i];
            Candidate candidate = CreateCandidate(root, rootPrefix, relativePath, CategoryDeprecatedSnapshot);
            if (candidate.RejectionReason == null)
                candidate.RejectionReason = ValidateDeprecatedSnapshot(candidate.FullPath, relativePath, packageId);
            candidates.Add(candidate);
        }

        for (int i = 0; i < DeprecatedMirrorPaths.Length; i++)
        {
            string relativePath = DeprecatedMirrorPaths[i];
            Candidate candidate = CreateCandidate(root, rootPrefix, relativePath, CategoryDeprecatedMirror);
            if (candidate.RejectionReason == null)
                candidate.RejectionReason = ValidateDeprecatedSnapshot(candidate.FullPath, relativePath, packageId);
            candidates.Add(candidate);
        }
    }

    private static void CollectOrphanResources(
        string root,
        string rootPrefix,
        CharacterUnityImportReport report,
        string targetRootProjectRelative,
        List<Candidate> candidates)
    {
        HashSet<string> declared = CollectDeclaredResourcePaths(report, targetRootProjectRelative);
        for (int rootIndex = 0; rootIndex < OrphanScanRoots.Length; rootIndex++)
        {
            string scanRoot = Path.Combine(root, OrphanScanRoots[rootIndex]);
            if (!Directory.Exists(scanRoot))
                continue;

            string[] files = Directory.GetFiles(scanRoot, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string fullPath = files[fileIndex];
                if (fullPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativePath = Path.GetRelativePath(root, fullPath).Replace('\\', '/');
                if (IsDeclaredResourcePath(relativePath, declared))
                    continue;

                string extension = Path.GetExtension(relativePath).ToLowerInvariant();
                if (Array.IndexOf(OrphanAllowedExtensions, extension) < 0)
                    continue;

                Candidate candidate = CreateCandidate(root, rootPrefix, relativePath, CategoryOrphanResource);
                if (candidate.RejectionReason == null
                    && (File.GetAttributes(candidate.FullPath) & FileAttributes.ReparsePoint) != 0)
                {
                    candidate.RejectionReason = "File is a reparse point (symlink/junction); refused.";
                }

                candidates.Add(candidate);
            }
        }
    }

    /// <summary>
    /// 从本次导入报告的 <c>resourceFile</c> 操作中提取"已声明"的包相对路径。
    /// 报告的 TargetPath 是项目相对路径，必须按包根前缀剥离；剥离失败时保留原值并由
    /// <see cref="IsDeclaredResourcePath"/> 的后缀兜底判定，宁可少删不可误删。
    /// </summary>
    private static HashSet<string> CollectDeclaredResourcePaths(CharacterUnityImportReport report, string targetRootProjectRelative)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        List<CharacterUnityImportOperation> operations = report?.Operations;
        if (operations == null)
            return declared;

        string prefix = string.IsNullOrWhiteSpace(targetRootProjectRelative)
            ? string.Empty
            : targetRootProjectRelative.Replace('\\', '/').TrimEnd('/') + "/";

        for (int i = 0; i < operations.Count; i++)
        {
            CharacterUnityImportOperation operation = operations[i];
            if (!string.Equals(operation.Kind, "resourceFile", StringComparison.Ordinal))
                continue;
            if (string.IsNullOrWhiteSpace(operation.TargetPath))
                continue;

            string target = operation.TargetPath.Replace('\\', '/');
            if (prefix.Length > 0 && target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                declared.Add(target.Substring(prefix.Length));
            else
                declared.Add(target);
        }

        return declared;
    }

    /// <summary>
    /// 精确匹配优先；若声明值未被正确前缀剥离，则退化为后缀匹配，避免把已声明资源误判为孤立文件。
    /// </summary>
    private static bool IsDeclaredResourcePath(string relativePath, HashSet<string> declared)
    {
        if (declared.Count == 0)
            return true;

        if (declared.Contains(relativePath))
            return true;

        foreach (string entry in declared)
        {
            if (entry.EndsWith("/" + relativePath, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Candidate CreateCandidate(string root, string rootPrefix, string relativePath, string category)
    {
        var candidate = new Candidate
        {
            RelativePath = relativePath,
            MetaRelativePath = relativePath + ".meta",
            Category = category
        };

        string normalized = relativePath.Replace('\\', '/');
        if (normalized.Split('/').Length != normalized.Split('/').Length
            || Array.IndexOf(normalized.Split('/'), "..") >= 0
            || Path.IsPathRooted(normalized))
        {
            candidate.RejectionReason = "Path escapes or is absolute; refused.";
            return candidate;
        }

        candidate.FullPath = Path.GetFullPath(Path.Combine(root, normalized));
        candidate.MetaFullPath = candidate.FullPath + ".meta";
        if (!candidate.FullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate.RejectionReason = "Resolved path is outside the package root; refused.";
            return candidate;
        }

        return candidate;
    }

    /// <summary>废弃快照必须能识别为已知格式且包身份匹配，否则保留并报告。</summary>
    private static string ValidateDeprecatedSnapshot(string fullPath, string relativePath, string packageId)
    {
        if (!File.Exists(fullPath))
            return null;

        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception exception)
        {
            return "Unable to read candidate for validation: " + exception.Message;
        }

        if (string.IsNullOrWhiteSpace(text))
            return "Candidate is empty; refused.";

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "Candidate is not a JSON object; refused.";

            if (!document.RootElement.TryGetProperty("packageId", out JsonElement packageIdElement)
                || packageIdElement.ValueKind != JsonValueKind.String)
            {
                return "Candidate has no packageId; refused.";
            }

            string candidatePackageId = packageIdElement.GetString() ?? string.Empty;
            bool isAnimationSnapshot = string.Equals(relativePath, AnimationCompileResultRelativePath, StringComparison.Ordinal);
            bool matches = isAnimationSnapshot
                ? string.Equals(candidatePackageId, "animation." + packageId, StringComparison.Ordinal)
                    || string.Equals(candidatePackageId, packageId, StringComparison.Ordinal)
                : string.Equals(candidatePackageId, packageId, StringComparison.Ordinal);

            return matches ? null : "packageId '" + candidatePackageId + "' does not match '" + packageId + "'; preserved.";
        }
        catch (JsonException exception)
        {
            return "Candidate is not valid JSON (" + exception.Message + "); preserved.";
        }
    }

    private static string BackupFile(string backupDirectory, string relativePath, byte[] bytes)
    {
        string target = Path.Combine(backupDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(target, bytes);
        return target.Replace('\\', '/');
    }

    private static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";
        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            builder.Append(char.IsLetterOrDigit(character) || character == '.' || character == '-' || character == '_'
                ? character
                : '_');
        }
        return builder.ToString();
    }
}
