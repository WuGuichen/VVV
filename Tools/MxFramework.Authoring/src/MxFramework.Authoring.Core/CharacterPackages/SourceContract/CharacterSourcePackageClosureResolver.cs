using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 第 1.1 节动态展开集合的输入描述。
    /// </summary>
    /// <remarks>
    /// 该类型刻意与 <c>manifest.json</c> 的 DTO 解耦：规范第 2.1 节的
    /// <c>assetEntries</c> / <c>featureFlags</c> 契约尚未在仓库中落地，
    /// 把展开算法做成对显式输入描述的纯函数，可以在不锁定 manifest 契约的前提下
    /// 先固定算法行为与回归用例。
    /// </remarks>
    public sealed class CharacterSourcePackageClosureRequest
    {
        /// <summary>无条件进入集合的条目（如 manifest.json、resource_catalog.json）。</summary>
        public List<string> AlwaysIncludedEntries { get; } = new List<string>();

        /// <summary>必需条目：条目名 → 相对路径。规范第 1.1 节步骤 2。</summary>
        public Dictionary<string, string> RequiredEntries { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>可选条目：条目名 → 相对路径或 null。规范第 1.1 节步骤 3。</summary>
        public Dictionary<string, string> OptionalEntries { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// 可选条目名 → 门控 featureFlag 名。
        /// </summary>
        /// <remarks>
        /// 规范第 1.1 节要求"若值为 string 且 featureFlags 对应开启"才纳入，
        /// 但<b>未定义条目与开关的对应表</b>（例如 <c>paletteBinding</c> 在
        /// <c>featureFlags</c> 中就没有对应开关，而主文档第 3.1 节用的是
        /// <c>enablePaletteCustomization</c>）。因此对应关系必须由调用方显式给出，
        /// 禁止在此处猜测。
        /// </remarks>
        public Dictionary<string, string> OptionalEntryFeatureGates { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>featureFlag 名 → 是否开启。</summary>
        public Dictionary<string, bool> FeatureFlags { get; } =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>动画条目数组。规范第 1.1 节步骤 4。</summary>
        public List<string> AnimationEntries { get; } = new List<string>();
    }

    /// <summary>条目被排除出展开集合的原因。</summary>
    public sealed class CharacterClosureExclusion
    {
        public CharacterClosureExclusion(string entryName, string relativePath, string reason)
        {
            EntryName = entryName ?? string.Empty;
            RelativePath = relativePath ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string EntryName { get; }

        public string RelativePath { get; }

        public string Reason { get; }
    }

    /// <summary>
    /// 源数据包输入闭包展开。对应规范 v1.4 第 1.1 节。
    /// </summary>
    public static class CharacterSourcePackageClosureResolver
    {
        /// <summary>
        /// 按第 1.1 节步骤 1–5 解析展开集合。
        /// </summary>
        /// <param name="request">展开输入描述。</param>
        /// <param name="exclusions">被排除的条目及原因，供诊断使用。</param>
        /// <returns>规范化、去重、按 Ordinal 升序的相对路径列表。</returns>
        public static IReadOnlyList<string> Resolve(
            CharacterSourcePackageClosureRequest request,
            out IReadOnlyList<CharacterClosureExclusion> exclusions)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var included = new HashSet<string>(StringComparer.Ordinal);
            var excluded = new List<CharacterClosureExclusion>();

            // 步骤 2 前置：无条件条目（manifest.json / resource_catalog.json）。
            foreach (string entry in request.AlwaysIncludedEntries)
            {
                AddIfNotBlank(included, entry);
            }

            // 步骤 2：必需项直接加入集合。
            foreach (KeyValuePair<string, string> entry in request.RequiredEntries.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    excluded.Add(new CharacterClosureExclusion(
                        entry.Key,
                        entry.Value,
                        "Required entry is not declared."));
                    continue;
                }

                AddIfNotBlank(included, entry.Value);
            }

            // 步骤 3：可选条目按值是否存在 + 门控开关是否开启决定。
            foreach (KeyValuePair<string, string> entry in request.OptionalEntries.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(entry.Value))
                {
                    excluded.Add(new CharacterClosureExclusion(entry.Key, entry.Value, "Optional entry is null."));
                    continue;
                }

                if (request.OptionalEntryFeatureGates.TryGetValue(entry.Key, out string flagName) &&
                    !string.IsNullOrEmpty(flagName) &&
                    (!request.FeatureFlags.TryGetValue(flagName, out bool enabled) || !enabled))
                {
                    excluded.Add(new CharacterClosureExclusion(
                        entry.Key,
                        entry.Value,
                        $"Feature flag '{flagName}' is not enabled."));
                    continue;
                }

                AddIfNotBlank(included, entry.Value);
            }

            // 步骤 4：动画条目逐一声明即加入。
            for (int i = 0; i < request.AnimationEntries.Count; i++)
            {
                AddIfNotBlank(included, request.AnimationEntries[i]);
            }

            // 步骤 3f 同构要求：集合本身也按 Ordinal 稳定排序，保证可复现。
            var ordered = included.ToList();
            ordered.Sort(StringComparer.Ordinal);

            exclusions = excluded;
            return ordered;
        }

        /// <summary>简化重载，丢弃排除明细。</summary>
        public static IReadOnlyList<string> Resolve(CharacterSourcePackageClosureRequest request)
        {
            return Resolve(request, out _);
        }

        /// <summary>
        /// 步骤 5：草稿文件隔离审计。
        /// </summary>
        /// <remarks>
        /// 返回物理存在于包目录、但未被展开集合声明的文件。
        /// 这些文件<b>绝不进入输入集合，严禁参与哈希计算</b>。
        /// 本方法只做审计与告警，不参与摘要计算。
        /// </remarks>
        public static IReadOnlyList<string> DetectUndeclaredFiles(
            string packageRootPath,
            IEnumerable<string> declaredRelativePaths)
        {
            if (string.IsNullOrEmpty(packageRootPath) || !Directory.Exists(packageRootPath))
            {
                return Array.Empty<string>();
            }

            var declared = new HashSet<string>(StringComparer.Ordinal);
            if (declaredRelativePaths != null)
            {
                foreach (string path in declaredRelativePaths)
                {
                    string normalized = CharacterSourcePackageCanonicalDigest.NormalizeRelativePath(path);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        declared.Add(normalized);
                    }
                }
            }

            var undeclared = new List<string>();
            foreach (string file in Directory.EnumerateFiles(packageRootPath, "*", SearchOption.AllDirectories))
            {
                string relative = CharacterSourcePackageCanonicalDigest.NormalizeRelativePath(
                    Path.GetRelativePath(packageRootPath, file));
                if (!declared.Contains(relative))
                {
                    undeclared.Add(relative);
                }
            }

            undeclared.Sort(StringComparer.Ordinal);
            return undeclared;
        }

        private static void AddIfNotBlank(HashSet<string> target, string relativePath)
        {
            string normalized = CharacterSourcePackageCanonicalDigest.NormalizeRelativePath(relativePath);
            if (!string.IsNullOrEmpty(normalized))
            {
                target.Add(normalized);
            }
        }
    }
}
