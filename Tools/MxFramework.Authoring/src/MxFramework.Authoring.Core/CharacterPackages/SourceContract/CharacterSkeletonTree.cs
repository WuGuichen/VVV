using System;
using System.Collections.Generic;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 中立骨架的索引化骨架树，供骨骼路径解析与语义锚点判定使用。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 名称查询刻意采用"唯一匹配"语义：同名骨骼存在多个时返回 null。
    /// 这与仓库既有约定一致——<c>CharacterHumanoidHurtboxTemplate</c> 在按名称
    /// 解析骨骼时同样对歧义返回 null，避免静默选中错误骨骼。
    /// </para>
    /// </remarks>
    public sealed class CharacterSkeletonTree
    {
        private readonly List<CharacterSkeletonBone> _bones;
        private readonly Dictionary<string, CharacterSkeletonBone> _byPath;
        private readonly Dictionary<string, List<CharacterSkeletonBone>> _byName;
        private readonly Dictionary<string, List<CharacterSkeletonBone>> _byNormalizedName;
        private readonly Dictionary<CharacterHumanoidBone, List<CharacterSkeletonBone>> _bySemantic;

        private CharacterSkeletonTree(
            List<CharacterSkeletonBone> bones,
            Dictionary<string, CharacterSkeletonBone> byPath,
            Dictionary<string, List<CharacterSkeletonBone>> byName,
            Dictionary<string, List<CharacterSkeletonBone>> byNormalizedName,
            Dictionary<CharacterHumanoidBone, List<CharacterSkeletonBone>> bySemantic)
        {
            _bones = bones;
            _byPath = byPath;
            _byName = byName;
            _byNormalizedName = byNormalizedName;
            _bySemantic = bySemantic;
        }

        /// <summary>
        /// 骨骼名规范化。规则以仓库既有实现 <c>CharacterHumanoidHurtboxTemplate.Normalize</c> 为基准：
        /// 去掉命名空间前缀、去掉 <c>mixamorig</c> 前缀、移除 <c>_ . -</c> 与空格、统一小写。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 复用该规则的理由：DCC 工具导出的骨骼名在大小写、命名空间前缀与分隔符上差异很大，
        /// 而 <c>geometry/body_colliders.json</c> 中的真实别名写作 <c>bone.spine</c>（小写），
        /// 对应的骨骼名却可能是 <c>Spine</c> 或 <c>mixamorig:Spine</c>。仅做序号精确匹配会误判为
        /// 不可解析，从而错误触发 <c>E-BONE-201</c>。
        /// </para>
        /// <para>
        /// 对既有规则的扩展（需规范确认）：既有实现只剥离 <c>:</c> 之前的前缀，
        /// 未覆盖 Blender 的 <c>Armature|Spine</c> 形式。<c>|</c> 与 <c>:</c> 同为命名空间分隔符，
        /// 因此本实现同时剥离两者中<b>最后一个</b>出现的前缀，否则 Blender 导出的骨架会被
        /// 全部误判为不可解析。该扩展不放松任何唯一性要求：规范化后产生歧义仍返回 null。
        /// </para>
        /// </remarks>
        public static string NormalizeBoneName(string boneName)
        {
            string name = boneName ?? string.Empty;

            int separator = Math.Max(name.LastIndexOf(':'), name.LastIndexOf('|'));
            if (separator >= 0)
            {
                name = name.Substring(separator + 1);
            }

            if (name.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(9);
            }

            return name.Replace("_", string.Empty)
                .Replace(".", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }

        /// <summary>全部骨骼，保持文档中的声明顺序。</summary>
        public IReadOnlyList<CharacterSkeletonBone> Bones => _bones;

        public int Count => _bones.Count;

        /// <summary>构建索引。路径重复或父路径悬空时抛出诊断异常。</summary>
        public static CharacterSkeletonTree Build(CharacterSkeletonHierarchy hierarchy, string objectPath)
        {
            if (hierarchy == null)
            {
                throw new CharacterDiagnosticException(
                    objectPath,
                    "Skeleton hierarchy is null.",
                    CharacterGateCodes.SkeletonHierarchyMissing);
            }

            var bones = new List<CharacterSkeletonBone>(hierarchy.Bones.Count);
            var byPath = new Dictionary<string, CharacterSkeletonBone>(StringComparer.Ordinal);
            var byName = new Dictionary<string, List<CharacterSkeletonBone>>(StringComparer.Ordinal);
            var byNormalizedName = new Dictionary<string, List<CharacterSkeletonBone>>(StringComparer.Ordinal);
            var bySemantic = new Dictionary<CharacterHumanoidBone, List<CharacterSkeletonBone>>();

            for (int i = 0; i < hierarchy.Bones.Count; i++)
            {
                CharacterSkeletonBone bone = hierarchy.Bones[i];
                string bonePath = objectPath + ".bones[" + i + "]";

                if (bone == null)
                {
                    throw new CharacterDiagnosticException(bonePath, "Bone entry is null.");
                }

                if (string.IsNullOrWhiteSpace(bone.Path))
                {
                    throw new CharacterDiagnosticException(bonePath + ".path", "Bone path is required.");
                }

                if (byPath.ContainsKey(bone.Path))
                {
                    throw new CharacterDiagnosticException(
                        bonePath + ".path",
                        $"Duplicate bone path '{bone.Path}'.");
                }

                byPath.Add(bone.Path, bone);
                bones.Add(bone);

                string name = bone.BoneName ?? string.Empty;
                if (!byName.TryGetValue(name, out List<CharacterSkeletonBone> sameName))
                {
                    sameName = new List<CharacterSkeletonBone>();
                    byName.Add(name, sameName);
                }

                sameName.Add(bone);

                string normalized = NormalizeBoneName(bone.BoneName);
                if (!byNormalizedName.TryGetValue(normalized, out List<CharacterSkeletonBone> sameNormalizedName))
                {
                    sameNormalizedName = new List<CharacterSkeletonBone>();
                    byNormalizedName.Add(normalized, sameNormalizedName);
                }

                sameNormalizedName.Add(bone);

                if (bone.HasHumanoidSemantic)
                {
                    if (!bySemantic.TryGetValue(bone.HumanoidSemantic, out List<CharacterSkeletonBone> sameSemantic))
                    {
                        sameSemantic = new List<CharacterSkeletonBone>();
                        bySemantic.Add(bone.HumanoidSemantic, sameSemantic);
                    }

                    sameSemantic.Add(bone);
                }
            }

            // 父路径必须可解析，且不得形成环。
            for (int i = 0; i < bones.Count; i++)
            {
                CharacterSkeletonBone bone = bones[i];
                string parent = bone.ParentPath ?? string.Empty;
                if (parent.Length == 0)
                {
                    continue;
                }

                if (!byPath.ContainsKey(parent))
                {
                    throw new CharacterDiagnosticException(
                        objectPath + ".bones[" + i + "].parentPath",
                        $"Bone '{bone.Path}' references missing parent '{parent}'.");
                }

                if (string.Equals(parent, bone.Path, StringComparison.Ordinal))
                {
                    throw new CharacterDiagnosticException(
                        objectPath + ".bones[" + i + "].parentPath",
                        $"Bone '{bone.Path}' is its own parent.");
                }
            }

            AssertAcyclic(bones, byPath, objectPath);

            return new CharacterSkeletonTree(bones, byPath, byName, byNormalizedName, bySemantic);
        }

        public bool ContainsPath(string path)
        {
            return !string.IsNullOrEmpty(path) && _byPath.ContainsKey(path);
        }

        public CharacterSkeletonBone FindByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return _byPath.TryGetValue(path, out CharacterSkeletonBone bone) ? bone : null;
        }

        /// <summary>按骨骼名查询，存在同名歧义时返回 null。</summary>
        public CharacterSkeletonBone FindUniqueByName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName) || !_byName.TryGetValue(boneName, out List<CharacterSkeletonBone> matches))
            {
                return null;
            }

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>按骨骼名查询全部匹配项。</summary>
        public IReadOnlyList<CharacterSkeletonBone> FindAllByName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName) || !_byName.TryGetValue(boneName, out List<CharacterSkeletonBone> matches))
            {
                return Array.Empty<CharacterSkeletonBone>();
            }

            return matches;
        }

        /// <summary>
        /// 按规范化骨骼名查询，存在歧义时返回 null。
        /// 用于容忍 DCC 导出在大小写、命名空间前缀与分隔符上的差异。
        /// </summary>
        public CharacterSkeletonBone FindUniqueByNormalizedName(string boneName)
        {
            string normalized = NormalizeBoneName(boneName);
            if (normalized.Length == 0 ||
                !_byNormalizedName.TryGetValue(normalized, out List<CharacterSkeletonBone> matches))
            {
                return null;
            }

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>按 Humanoid 语义查询，存在歧义时返回 null。</summary>
        public CharacterSkeletonBone FindUniqueBySemantic(CharacterHumanoidBone semantic)
        {
            if (semantic == CharacterHumanoidBone.None ||
                !_bySemantic.TryGetValue(semantic, out List<CharacterSkeletonBone> matches))
            {
                return null;
            }

            return matches.Count == 1 ? matches[0] : null;
        }

        /// <summary>返回从根到该骨骼的祖先路径（不含自身），顺序为根在前。</summary>
        public IReadOnlyList<CharacterSkeletonBone> GetAncestors(string path)
        {
            var ancestors = new List<CharacterSkeletonBone>();
            CharacterSkeletonBone current = FindByPath(path);
            if (current == null)
            {
                return ancestors;
            }

            var guard = new HashSet<string>(StringComparer.Ordinal);
            string parentPath = current.ParentPath ?? string.Empty;

            while (parentPath.Length > 0 && guard.Add(parentPath))
            {
                CharacterSkeletonBone parent = FindByPath(parentPath);
                if (parent == null)
                {
                    break;
                }

                ancestors.Insert(0, parent);
                parentPath = parent.ParentPath ?? string.Empty;
            }

            return ancestors;
        }

        private static void AssertAcyclic(
            List<CharacterSkeletonBone> bones,
            Dictionary<string, CharacterSkeletonBone> byPath,
            string objectPath)
        {
            // 迭代式走链：每个骨骼向上回溯，用"本次已访问"集合检测环。
            for (int i = 0; i < bones.Count; i++)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                string current = bones[i].Path;

                while (!string.IsNullOrEmpty(current))
                {
                    if (!visited.Add(current))
                    {
                        throw new CharacterDiagnosticException(
                            objectPath + ".bones[" + i + "].parentPath",
                            $"Skeleton hierarchy contains a parent cycle at '{current}'.");
                    }

                    if (!byPath.TryGetValue(current, out CharacterSkeletonBone bone))
                    {
                        break;
                    }

                    current = bone.ParentPath ?? string.Empty;
                }
            }
        }
    }
}
