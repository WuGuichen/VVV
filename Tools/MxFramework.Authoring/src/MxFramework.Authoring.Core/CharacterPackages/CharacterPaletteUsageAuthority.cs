using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MxFramework.Authoring
{
    /// <summary>
    /// Surface Palette usage 的权威判定码。
    /// 这些码同时出现在保存接口的 409 响应、编译门禁的 issue 与校验脚本的输出中，必须保持稳定。
    /// </summary>
    public static class CharacterPaletteUsageCodes
    {
        public const string BodyModelUndeclared = "BODY_MODEL_UNDECLARED";
        public const string BodyModelAmbiguous = "BODY_MODEL_AMBIGUOUS";
        public const string BodyModelPathUnsupported = "BODY_MODEL_PATH_UNSUPPORTED";
        public const string BodyModelFileMissing = "BODY_MODEL_FILE_MISSING";
        public const string ModelHashMissing = "MODEL_HASH_MISSING";
        public const string ModelHashMismatch = "MODEL_HASH_MISMATCH";
        public const string PaletteUsageMissing = "PALETTE_USAGE_MISSING";
        public const string PaletteUsageInvalid = "PALETTE_USAGE_INVALID";
        public const string PaletteBindingInvalid = "PALETTE_BINDING_INVALID";
    }

    public enum CharacterBodyModelResolutionStatus
    {
        Resolved = 0,

        /// <summary>资源目录里没有任何 typeId=model 且 usage=characterModel 的条目。</summary>
        Undeclared = 1,

        /// <summary>命中多于一条本体模型候选。必须由人修正资源目录，禁止任选其一。</summary>
        Ambiguous = 2,

        /// <summary>条目存在但 relativePath 不是安全的包内相对路径（为空、绝对路径、含 ..）。</summary>
        UnsafePath = 3,

        /// <summary>包根路径已知但文件不存在。</summary>
        FileMissing = 4,

        /// <summary>没有可用的包或资源目录。</summary>
        PackageMissing = 5
    }

    public sealed class CharacterBodyModelResolution
    {
        public CharacterBodyModelResolutionStatus Status { get; set; } = CharacterBodyModelResolutionStatus.PackageMissing;

        /// <summary>失败时取 <see cref="CharacterPaletteUsageCodes"/> 中的一个；成功时为空。</summary>
        public string Code { get; set; } = CharacterPaletteUsageCodes.BodyModelUndeclared;

        public string Message { get; set; } = string.Empty;

        /// <summary>命中的资源目录条目；仅在 <see cref="Status"/> 为 Resolved 时非空。</summary>
        public CharacterPackageResourceEntry Entry { get; set; }

        /// <summary>候选条目的 resourceKey 列表，用于 Ambiguous 场景的报错信息。</summary>
        public List<string> CandidateResourceKeys { get; set; } = new List<string>();

        public string RelativePath { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        /// <summary>本体模型源文件字节的 sha256（自带 sha256: 前缀）。未校验或文件不可读时为空。</summary>
        public string ModelHash { get; set; } = string.Empty;

        public bool IsResolved => Status == CharacterBodyModelResolutionStatus.Resolved;
    }

    public sealed class CharacterPaletteUsageCheckResult
    {
        public bool IsValid { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        /// <summary>参与比较的期望值（本体模型源文件哈希）。</summary>
        public string ExpectedModelHash { get; set; } = string.Empty;

        /// <summary>usage 文档中声明的值。</summary>
        public string ActualModelHash { get; set; } = string.Empty;

        public static CharacterPaletteUsageCheckResult Ok(string expected, string actual) => new CharacterPaletteUsageCheckResult
        {
            IsValid = true,
            ExpectedModelHash = expected ?? string.Empty,
            ActualModelHash = actual ?? string.Empty
        };

        public static CharacterPaletteUsageCheckResult Fail(string code, string message, string expected, string actual) => new CharacterPaletteUsageCheckResult
        {
            IsValid = false,
            Code = code ?? string.Empty,
            Message = message ?? string.Empty,
            ExpectedModelHash = expected ?? string.Empty,
            ActualModelHash = actual ?? string.Empty
        };
    }

    /// <summary>
    /// Surface Palette usage 的单一权威判定入口。
    ///
    /// 不变量：
    /// - I-1: usage.modelHash 必须等于包声明的本体模型源文件哈希。
    /// - I-3: tokenUsages 必须与 modelHash 指向同一模型版本。
    ///
    /// 本类型只负责「模型版本标签」这一半：它无法重算 tokenUsages（面数统计只在浏览器端 three.js 中实现），
    /// 因此任何 modelHash 不一致都必须被拒绝，而不是被就地改写。
    ///
    /// 本类型位于 Core（netstandard2.1），不得引用 UnityEngine / UnityEditor。
    /// </summary>
    public static class CharacterPaletteUsageAuthority
    {
        public const string BindingRelativePath = "config/character_palette_binding.json";
        public const string UsageRelativePath = "config/character_palette_usage.json";

        /// <summary>
        /// 解析包声明的本体模型。规则为 typeId == model 且 usage == characterModel，命中多于一条时报错而非任选。
        /// </summary>
        /// <param name="package">已加载的角色资源包。</param>
        /// <param name="packageRootPath">包根路径。为空时跳过文件存在性与哈希校验。</param>
        /// <param name="verifyFileHash">是否读取文件字节计算 sha256。仅在包根路径有效时生效。</param>
        public static CharacterBodyModelResolution ResolveBodyModel(
            CharacterResourcePackage package,
            string packageRootPath = "",
            bool verifyFileHash = false)
        {
            var result = new CharacterBodyModelResolution();

            CharacterPackageResourceCatalog catalog = package?.ResourceCatalog;
            if (catalog == null || catalog.Entries == null)
            {
                result.Status = CharacterBodyModelResolutionStatus.PackageMissing;
                result.Code = CharacterPaletteUsageCodes.BodyModelUndeclared;
                result.Message = "Character resource package or resource catalog is missing.";
                return result;
            }

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                CharacterPackageResourceEntry entry = catalog.Entries[i];
                if (entry == null)
                    continue;
                if (!string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase))
                    continue;
                result.CandidateResourceKeys.Add(entry.ResourceKey ?? string.Empty);
                if (result.Entry == null)
                    result.Entry = entry;
            }

            if (result.Entry == null)
            {
                result.Status = CharacterBodyModelResolutionStatus.Undeclared;
                result.Code = CharacterPaletteUsageCodes.BodyModelUndeclared;
                result.Message = "The package does not declare a body model resource "
                    + "(typeId=" + CharacterPackageResourceTypeIds.Model + ", usage=" + CharacterPackageResourceUsageIds.CharacterModel + ").";
                return result;
            }

            if (result.CandidateResourceKeys.Count > 1)
            {
                CharacterPackageResourceEntry first = result.Entry;
                result.Entry = null;
                result.Status = CharacterBodyModelResolutionStatus.Ambiguous;
                result.Code = CharacterPaletteUsageCodes.BodyModelAmbiguous;
                result.Message = "The package declares " + result.CandidateResourceKeys.Count
                    + " body model resources; exactly one is required: "
                    + string.Join(", ", result.CandidateResourceKeys);
                return result;
            }

            string relativePath = result.Entry.RelativePath ?? string.Empty;
            result.RelativePath = relativePath;
            if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(relativePath))
            {
                result.Status = CharacterBodyModelResolutionStatus.UnsafePath;
                result.Code = CharacterPaletteUsageCodes.BodyModelPathUnsupported;
                result.Message = "Body model relativePath is not a safe package-relative path: '" + relativePath
                    + "'. Palette usage authority requires an in-package model file.";
                return result;
            }

            if (string.IsNullOrWhiteSpace(packageRootPath))
            {
                result.Status = CharacterBodyModelResolutionStatus.Resolved;
                result.Code = string.Empty;
                return result;
            }

            string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRootPath, relativePath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                result.Status = CharacterBodyModelResolutionStatus.UnsafePath;
                result.Code = CharacterPaletteUsageCodes.BodyModelPathUnsupported;
                result.Message = "Body model path could not be resolved inside the package: '" + relativePath + "'.";
                return result;
            }

            result.FullPath = fullPath;
            if (!File.Exists(fullPath))
            {
                result.Status = CharacterBodyModelResolutionStatus.FileMissing;
                result.Code = CharacterPaletteUsageCodes.BodyModelFileMissing;
                result.Message = "Body model file is missing: " + relativePath;
                return result;
            }

            result.Status = CharacterBodyModelResolutionStatus.Resolved;
            result.Code = string.Empty;

            if (verifyFileHash)
            {
                try
                {
                    result.ModelHash = CharacterPackageHashUtility.ComputeFileSha256(fullPath);
                }
                catch (IOException ex)
                {
                    result.Status = CharacterBodyModelResolutionStatus.FileMissing;
                    result.Code = CharacterPaletteUsageCodes.BodyModelFileMissing;
                    result.Message = "Body model file could not be read: " + relativePath + " (" + ex.Message + ")";
                }
            }

            return result;
        }

        /// <summary>
        /// 判定 usage 文档声明的 modelHash 是否等于本体模型源文件哈希。
        /// </summary>
        public static CharacterPaletteUsageCheckResult ValidateUsageModelHash(
            CharacterBodyModelResolution body,
            string usageModelHash)
        {
            if (body == null || !body.IsResolved)
            {
                string code = body?.Code ?? CharacterPaletteUsageCodes.BodyModelUndeclared;
                string message = body?.Message ?? "Body model could not be resolved.";
                return CharacterPaletteUsageCheckResult.Fail(code, message, body?.ModelHash, usageModelHash);
            }

            if (string.IsNullOrWhiteSpace(usageModelHash))
            {
                return CharacterPaletteUsageCheckResult.Fail(
                    CharacterPaletteUsageCodes.ModelHashMissing,
                    "Palette usage does not declare a modelHash.",
                    body.ModelHash,
                    usageModelHash);
            }

            string expected = CharacterPackageHashUtility.NormalizeSha256(body.ModelHash);
            string actual = CharacterPackageHashUtility.NormalizeSha256(usageModelHash);
            if (expected.Length == 0)
            {
                return CharacterPaletteUsageCheckResult.Fail(
                    CharacterPaletteUsageCodes.BodyModelFileMissing,
                    "Body model hash was not computed; cannot verify palette usage modelHash.",
                    string.Empty,
                    actual);
            }

            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return CharacterPaletteUsageCheckResult.Fail(
                    CharacterPaletteUsageCodes.ModelHashMismatch,
                    "Palette usage modelHash does not match the package body model. expected=" + expected + ", actual=" + actual,
                    expected,
                    actual);
            }

            return CharacterPaletteUsageCheckResult.Ok(expected, actual);
        }

        /// <summary>
        /// 从 usage 文档 JSON 文本中取出 modelHash。返回 null 表示文档无法解析。
        /// </summary>
        public static string ReadUsageModelHash(string usageJson, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(usageJson))
            {
                error = "Palette usage document is empty.";
                return null;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(usageJson))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        error = "Palette usage document root must be an object.";
                        return null;
                    }

                    if (!document.RootElement.TryGetProperty("modelHash", out JsonElement modelHash)
                        || modelHash.ValueKind != JsonValueKind.String)
                    {
                        return string.Empty;
                    }

                    return modelHash.GetString() ?? string.Empty;
                }
            }
            catch (JsonException ex)
            {
                error = "Palette usage document is malformed: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 读取色板绑定文件中的 enabled 标记。返回 null 表示绑定文件不存在或无法解析。
        /// </summary>
        public static bool? ReadBindingEnabled(string packageRootPath, out string error)
        {
            error = string.Empty;
            string path = CharacterPackageResourcePipeline.ResolvePackagePath(packageRootPath, BindingRelativePath);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        error = "Palette binding document root must be an object.";
                        return null;
                    }

                    if (document.RootElement.TryGetProperty("enabled", out JsonElement enabled)
                        && (enabled.ValueKind == JsonValueKind.True || enabled.ValueKind == JsonValueKind.False))
                        return enabled.GetBoolean();

                    return false;
                }
            }
            catch (JsonException ex)
            {
                error = "Palette binding document is malformed: " + ex.Message;
                return null;
            }
            catch (IOException ex)
            {
                error = "Palette binding document could not be read: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 编译门禁使用的聚合判定。仅在色板绑定启用时产出问题；未配置色板的包返回空列表。
        /// </summary>
        public static List<CharacterPaletteUsageGateIssue> EvaluatePaletteUsage(
            CharacterResourcePackage package,
            string packageRootPath)
        {
            var issues = new List<CharacterPaletteUsageGateIssue>();
            if (package == null || string.IsNullOrWhiteSpace(packageRootPath))
                return issues;
            if (CharacterBakedSurfaceAuthority.Exists(packageRootPath))
                return CharacterBakedSurfaceAuthority.Validate(packageRootPath, package.Manifest.PackageId);

            bool? enabled = ReadBindingEnabled(packageRootPath, out string bindingError);
            if (enabled == null)
            {
                // 绑定文件缺失 = 该包未启用色板，不是错误；文件存在但损坏才报错。
                string bindingPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRootPath, BindingRelativePath);
                if (!string.IsNullOrWhiteSpace(bindingPath) && File.Exists(bindingPath))
                {
                    issues.Add(new CharacterPaletteUsageGateIssue
                    {
                        Code = CharacterPaletteUsageCodes.PaletteBindingInvalid,
                        Message = bindingError,
                        SourcePath = BindingRelativePath
                    });
                }

                return issues;
            }

            if (!enabled.Value)
                return issues;

            CharacterBodyModelResolution body = ResolveBodyModel(package, packageRootPath, true);
            if (!body.IsResolved)
            {
                issues.Add(new CharacterPaletteUsageGateIssue
                {
                    Code = body.Code,
                    Message = body.Message,
                    SourcePath = body.RelativePath
                });
                return issues;
            }

            string usagePath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRootPath, UsageRelativePath);
            if (string.IsNullOrWhiteSpace(usagePath) || !File.Exists(usagePath))
            {
                issues.Add(new CharacterPaletteUsageGateIssue
                {
                    Code = CharacterPaletteUsageCodes.PaletteUsageMissing,
                    Message = "Palette binding is enabled but " + UsageRelativePath + " is missing.",
                    SourcePath = UsageRelativePath,
                    Field = "modelHash"
                });
                return issues;
            }

            string usageJson;
            try
            {
                usageJson = File.ReadAllText(usagePath);
            }
            catch (IOException ex)
            {
                issues.Add(new CharacterPaletteUsageGateIssue
                {
                    Code = CharacterPaletteUsageCodes.PaletteUsageInvalid,
                    Message = "Palette usage document could not be read: " + ex.Message,
                    SourcePath = UsageRelativePath
                });
                return issues;
            }

            string usageModelHash = ReadUsageModelHash(usageJson, out string usageError);
            if (usageModelHash == null)
            {
                issues.Add(new CharacterPaletteUsageGateIssue
                {
                    Code = CharacterPaletteUsageCodes.PaletteUsageInvalid,
                    Message = usageError,
                    SourcePath = UsageRelativePath
                });
                return issues;
            }

            CharacterPaletteUsageCheckResult check = ValidateUsageModelHash(body, usageModelHash);
            if (!check.IsValid)
            {
                issues.Add(new CharacterPaletteUsageGateIssue
                {
                    Code = check.Code,
                    Message = check.Message,
                    SourcePath = UsageRelativePath,
                    Field = "modelHash",
                    ExpectedValue = check.ExpectedModelHash,
                    ActualValue = check.ActualModelHash
                });
            }

            return issues;
        }
    }

    public sealed class CharacterPaletteUsageGateIssue
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string SourcePath { get; set; } = string.Empty;

        public string Field { get; set; } = string.Empty;

        public string ExpectedValue { get; set; } = string.Empty;

        public string ActualValue { get; set; } = string.Empty;
    }
}
