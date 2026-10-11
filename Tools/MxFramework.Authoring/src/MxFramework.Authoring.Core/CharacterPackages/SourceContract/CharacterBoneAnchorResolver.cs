using System;

namespace MxFramework.Authoring
{
    /// <summary>骨骼锚点的解析来源。</summary>
    public enum CharacterBoneAnchorSource
    {
        None = 0,

        /// <summary>命中骨架树中的具体路径。</summary>
        ConcretePath = 1,

        /// <summary>通过项目既有约定 <c>bone.&lt;name&gt;</c> 别名按唯一骨骼名命中。</summary>
        LegacyNameAlias = 2,

        /// <summary>路径不可解析，由 Humanoid 语义备用锚点命中。</summary>
        HumanoidSemantic = 3
    }

    /// <summary>骨骼锚点解析裁决。</summary>
    public enum CharacterBoneAnchorVerdict
    {
        Resolved = 0,

        /// <summary><c>E-BONE-201</c>：路径无法解析且无 Humanoid 语义备用锚点。</summary>
        PathUnresolvedWithoutSemanticFallback = 1,

        /// <summary><c>E-BONE-202</c>：路径与 Humanoid 语义指向不同骨骼。</summary>
        PathSemanticConflict = 2
    }

    /// <summary>骨骼锚点解析结果。</summary>
    public sealed class CharacterBoneAnchorResult
    {
        public CharacterBoneAnchorResult(
            CharacterBoneAnchorVerdict verdict,
            CharacterSkeletonBone bone,
            CharacterBoneAnchorSource source,
            string gateCode,
            string message)
        {
            Verdict = verdict;
            Bone = bone;
            Source = source;
            GateCode = gateCode ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public CharacterBoneAnchorVerdict Verdict { get; }

        /// <summary>裁决为 Resolved 时指向命中的骨骼，否则为 null。</summary>
        public CharacterSkeletonBone Bone { get; }

        public CharacterBoneAnchorSource Source { get; }

        /// <summary>未通过时的门禁码。</summary>
        public string GateCode { get; }

        public string Message { get; }

        public bool IsResolved => Verdict == CharacterBoneAnchorVerdict.Resolved && Bone != null;

        /// <summary>未通过时抛出携带对应门禁码的诊断异常。</summary>
        public void ThrowIfNotResolved(string objectPath)
        {
            if (IsResolved)
            {
                return;
            }

            throw new CharacterDiagnosticException(
                objectPath,
                Message,
                GateCode,
                field: "parentPath");
        }
    }

    /// <summary>
    /// 骨骼双锚点解析。对应规范 v1.4 第 7.1 节 <c>E-BONE-201</c> / <c>E-BONE-202</c>。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 路径解析兼容项目既有约定：骨骼引用有两种形态——具体层级路径，
    /// 以及遗留别名 <c>bone.&lt;name&gt;</c>（见仓库既有实现
    /// <c>CharacterHumanoidHurtboxTemplate</c> 对二者的区分，以及
    /// <c>geometry/body_colliders.json</c> 中的真实取值 <c>bone.spine</c>）。
    /// 别名不是具体层级路径，因此必须能由 Humanoid 语义备用锚点兜底，
    /// 否则即触发 <c>E-BONE-201</c>。
    /// </para>
    /// <para>
    /// 语义歧义（同一 Humanoid 语义被多根骨骼声明）按"不可用"处理，
    /// 返回 null 并归入 <c>E-BONE-201</c>，不做静默择一。
    /// </para>
    /// </remarks>
    public static class CharacterBoneAnchorResolver
    {
        /// <summary>遗留骨骼名别名前缀。</summary>
        public const string LegacyBoneAliasPrefix = "bone.";

        /// <summary>
        /// 解析骨骼锚点。不抛异常，由调用方按 <see cref="CharacterBoneAnchorResult.Verdict"/> 裁决。
        /// </summary>
        public static CharacterBoneAnchorResult Resolve(
            CharacterSkeletonTree tree,
            string bonePath,
            CharacterHumanoidBone humanoidSemantic)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            CharacterBoneAnchorSource pathSource;
            CharacterSkeletonBone pathBone = ResolveByPath(tree, bonePath, out pathSource);

            CharacterSkeletonBone semanticBone = humanoidSemantic == CharacterHumanoidBone.None
                ? null
                : tree.FindUniqueBySemantic(humanoidSemantic);

            // E-BONE-202：两个锚点都命中但指向不同骨骼。
            if (pathBone != null && semanticBone != null &&
                !string.Equals(pathBone.Path, semanticBone.Path, StringComparison.Ordinal))
            {
                return new CharacterBoneAnchorResult(
                    CharacterBoneAnchorVerdict.PathSemanticConflict,
                    null,
                    CharacterBoneAnchorSource.None,
                    CharacterGateCodes.BonePathSemanticConflict,
                    $"Bone path '{bonePath}' resolves to '{pathBone.Path}' while humanoid semantic " +
                    $"'{humanoidSemantic}' resolves to '{semanticBone.Path}'. Anchors conflict.");
            }

            if (pathBone != null)
            {
                return new CharacterBoneAnchorResult(
                    CharacterBoneAnchorVerdict.Resolved,
                    pathBone,
                    pathSource,
                    string.Empty,
                    string.Empty);
            }

            if (semanticBone != null)
            {
                return new CharacterBoneAnchorResult(
                    CharacterBoneAnchorVerdict.Resolved,
                    semanticBone,
                    CharacterBoneAnchorSource.HumanoidSemantic,
                    string.Empty,
                    string.Empty);
            }

            // E-BONE-201：路径不可解析且无语义备用锚点。
            return new CharacterBoneAnchorResult(
                CharacterBoneAnchorVerdict.PathUnresolvedWithoutSemanticFallback,
                null,
                CharacterBoneAnchorSource.None,
                CharacterGateCodes.BonePathUnresolvedWithoutSemantic,
                $"Bone path '{bonePath}' cannot be resolved in the skeleton hierarchy and no usable " +
                "humanoid semantic fallback anchor is configured.");
        }

        /// <summary>解析并断言成功，失败时抛出携带门禁码的诊断异常。</summary>
        public static CharacterSkeletonBone ResolveOrThrow(
            CharacterSkeletonTree tree,
            string bonePath,
            CharacterHumanoidBone humanoidSemantic,
            string objectPath)
        {
            CharacterBoneAnchorResult result = Resolve(tree, bonePath, humanoidSemantic);
            result.ThrowIfNotResolved(objectPath);
            return result.Bone;
        }

        /// <summary>判断某个路径是否为遗留 <c>bone.&lt;name&gt;</c> 别名。</summary>
        public static bool IsLegacyBoneAlias(string bonePath)
        {
            return !string.IsNullOrEmpty(bonePath) &&
                   bonePath.StartsWith(LegacyBoneAliasPrefix, StringComparison.Ordinal);
        }

        private static CharacterSkeletonBone ResolveByPath(
            CharacterSkeletonTree tree,
            string bonePath,
            out CharacterBoneAnchorSource source)
        {
            source = CharacterBoneAnchorSource.None;

            if (string.IsNullOrEmpty(bonePath))
            {
                return null;
            }

            CharacterSkeletonBone concrete = tree.FindByPath(bonePath);
            if (concrete != null)
            {
                source = CharacterBoneAnchorSource.ConcretePath;
                return concrete;
            }

            if (!IsLegacyBoneAlias(bonePath))
            {
                return null;
            }

            string name = bonePath.Substring(LegacyBoneAliasPrefix.Length);

            // 先做序号精确匹配（与仓库既有 FindPath 语义一致），
            // 再退化到规范化匹配以容忍 DCC 命名差异。两条路径都要求唯一命中。
            CharacterSkeletonBone alias = tree.FindUniqueByName(name) ?? tree.FindUniqueByNormalizedName(name);
            if (alias != null)
            {
                source = CharacterBoneAnchorSource.LegacyNameAlias;
            }

            return alias;
        }
    }
}
