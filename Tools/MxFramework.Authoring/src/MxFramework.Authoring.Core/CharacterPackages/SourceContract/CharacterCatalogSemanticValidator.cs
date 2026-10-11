using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 资源目录条目的校验视图。刻意与 <c>resource_catalog.json</c> 的实际 DTO 解耦：
    /// 该文件的形态尚待裁决（规范第 1.4 节的简化 Schema 与仓库现有的
    /// ResourceMapping V2 文档互斥），因此语义门禁只依赖这三个必要字段。
    /// </summary>
    public sealed class CharacterCatalogEntryView
    {
        public string ResourceKey { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 资源目录语义门禁。对应规范 v1.4 第 1.4 节与第 7.1 节 <c>E-CAT-201</c>。
    /// </summary>
    /// <remarks>
    /// <para>门禁内容：</para>
    /// <list type="bullet">
    /// <item><description><c>E-CAT-201</c>：<c>resourceKey</c> 重复。</description></item>
    /// <item><description>扩展名与 <c>contentType</c> 强绑定；目录段决定
    /// <c>resources/models</c> → <c>Model/FBX</c>、<c>resources/animations</c> → <c>Animation/FBX</c>、
    /// <c>resources/textures</c> → <c>Texture/PNG</c>。该映射由第 1.4 节
    /// <c>relativePath</c> 正则与 <c>contentType</c> 枚举共同唯一确定。</description></item>
    /// <item><description>遗留 <c>.tga</c> 已按修订矩阵移除，出现即判违规。</description></item>
    /// </list>
    /// </remarks>
    public static class CharacterCatalogSemanticValidator
    {
        public const string ModelContentType = "Model/FBX";

        public const string AnimationContentType = "Animation/FBX";

        public const string TextureContentType = "Texture/PNG";

        /// <summary>校验并返回全部问题（不抛异常）。</summary>
        public static IReadOnlyList<CharacterAuthoringValidationIssue> Validate(
            IReadOnlyList<CharacterCatalogEntryView> entries,
            string sourcePath)
        {
            var issues = new List<CharacterAuthoringValidationIssue>();
            if (entries == null)
            {
                return issues;
            }

            var seenKeys = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterCatalogEntryView entry = entries[i];
                string path = "entries[" + i + "]";

                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.ResourceKey))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.ResourceCatalogDuplicateKey,
                        sourcePath,
                        path + ".resourceKey",
                        "resourceKey",
                        "resourceKey is required."));
                }
                else if (seenKeys.TryGetValue(entry.ResourceKey, out int firstIndex))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.ResourceCatalogDuplicateKey,
                        sourcePath,
                        path + ".resourceKey",
                        "resourceKey",
                        $"Duplicate resourceKey '{entry.ResourceKey}' (first declared at entries[{firstIndex}])."));
                }
                else
                {
                    seenKeys.Add(entry.ResourceKey, i);
                }

                string expectedContentType = ExpectedContentType(entry.RelativePath);
                if (expectedContentType == null)
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.ResourceCatalogDuplicateKey,
                        sourcePath,
                        path + ".relativePath",
                        "relativePath",
                        $"relativePath '{entry.RelativePath}' is not one of the registered resource locations " +
                        "(resources/models/*.fbx, resources/animations/*.fbx, resources/textures/*.png)."));
                }
                else if (!string.Equals(expectedContentType, entry.ContentType, StringComparison.Ordinal))
                {
                    issues.Add(CharacterGateCodes.CreateIssue(
                        CharacterGateCodes.ResourceCatalogDuplicateKey,
                        sourcePath,
                        path + ".contentType",
                        "contentType",
                        $"contentType '{entry.ContentType}' does not match the location '{entry.RelativePath}' " +
                        $"(expected '{expectedContentType}')."));
                }
            }

            return issues;
        }

        /// <summary>校验并在首个违规处抛出诊断异常。</summary>
        public static void AssertValid(IReadOnlyList<CharacterCatalogEntryView> entries, string sourcePath)
        {
            IReadOnlyList<CharacterAuthoringValidationIssue> issues = Validate(entries, sourcePath);
            if (issues.Count == 0)
            {
                return;
            }

            CharacterAuthoringValidationIssue issue = issues[0];
            throw new CharacterDiagnosticException(
                issue.SourceObjectPath,
                issue.Message,
                issue.Code,
                sourcePath,
                issue.Field);
        }

        /// <summary>
        /// 由相对路径推导期望的 <c>contentType</c>；不在注册位置内时返回 null。
        /// </summary>
        public static string ExpectedContentType(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            string normalized = relativePath.Replace('\\', '/');
            string extension = Path.GetExtension(normalized).ToLowerInvariant();

            if (normalized.StartsWith("resources/models/", StringComparison.Ordinal))
            {
                return extension == ".fbx" ? ModelContentType : null;
            }

            if (normalized.StartsWith("resources/animations/", StringComparison.Ordinal))
            {
                return extension == ".fbx" ? AnimationContentType : null;
            }

            if (normalized.StartsWith("resources/textures/", StringComparison.Ordinal))
            {
                return extension == ".png" ? TextureContentType : null;
            }

            return null;
        }
    }
}
