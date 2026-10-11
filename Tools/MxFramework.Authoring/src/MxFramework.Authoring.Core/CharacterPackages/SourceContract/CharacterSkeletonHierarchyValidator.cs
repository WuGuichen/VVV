using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MxFramework.Authoring
{
    /// <summary>
    /// 中立骨架结构校验。对应规范 v1.4 第 1.2 节 Schema 与第 7.1 节
    /// <c>E-SRC-109</c> / <c>E-SKEL-101</c>。
    /// </summary>
    /// <remarks>
    /// 规范澄清项（门禁码缺口）：第 7.1 节只为骨架定义了两个门禁码
    /// （<c>E-SRC-109</c> 文件缺失、<c>E-SKEL-101</c> 源网格哈希不匹配），
    /// 没有为第 1.2 节 Schema 的<b>结构违规</b>定义专用码。本实现对结构违规
    /// 复用 <c>E-SRC-109</c>——它同为 Preflight / Fatal / BlockAll，
    /// 是保守且语义最近的选择；是否需要拆分专用码待规范确认。
    /// </remarks>
    public static class CharacterSkeletonHierarchyValidator
    {
        public const string PackageIdPattern = "^[0-9]{6}$";

        public const string Sha256Pattern = "^sha256:[0-9a-f]{64}$";

        private static readonly Regex PackageIdRegex = new Regex(PackageIdPattern, RegexOptions.CultureInvariant);

        private static readonly Regex Sha256Regex = new Regex(Sha256Pattern, RegexOptions.CultureInvariant);

        /// <summary>校验骨架文档结构；<paramref name="expectedModelSha256"/> 非空时同时校验源网格绑定。</summary>
        public static void Validate(
            CharacterSkeletonHierarchy hierarchy,
            string sourcePath,
            string expectedModelSha256 = null)
        {
            if (hierarchy == null)
            {
                throw new CharacterDiagnosticException(
                    sourcePath,
                    "skeleton_hierarchy.json is missing or could not be deserialized. Core cannot perform skeleton validation.",
                    CharacterGateCodes.SkeletonHierarchyMissing,
                    sourcePath: sourcePath);
            }

            if (!string.Equals(hierarchy.SchemaVersion, CharacterSkeletonHierarchy.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new CharacterDiagnosticException(
                    sourcePath + ".schemaVersion",
                    $"skeleton_hierarchy.schemaVersion must be '{CharacterSkeletonHierarchy.CurrentSchemaVersion}', got '{hierarchy.SchemaVersion}'.",
                    CharacterGateCodes.SkeletonHierarchyMissing,
                    sourcePath: sourcePath,
                    field: "schemaVersion");
            }

            if (string.IsNullOrEmpty(hierarchy.PackageId) || !PackageIdRegex.IsMatch(hierarchy.PackageId))
            {
                throw new CharacterDiagnosticException(
                    sourcePath + ".packageId",
                    $"skeleton_hierarchy.packageId must be 6 digits, got '{hierarchy.PackageId}'.",
                    CharacterGateCodes.SkeletonHierarchyMissing,
                    sourcePath: sourcePath,
                    field: "packageId");
            }

            if (string.IsNullOrEmpty(hierarchy.SourceMeshSha256) || !Sha256Regex.IsMatch(hierarchy.SourceMeshSha256))
            {
                throw new CharacterDiagnosticException(
                    sourcePath + ".sourceMeshSha256",
                    $"skeleton_hierarchy.sourceMeshSha256 must match '{Sha256Pattern}', got '{hierarchy.SourceMeshSha256}'.",
                    CharacterGateCodes.SkeletonHierarchyMissing,
                    sourcePath: sourcePath,
                    field: "sourceMeshSha256");
            }

            for (int i = 0; i < hierarchy.Bones.Count; i++)
            {
                CharacterSkeletonBone bone = hierarchy.Bones[i];
                string bonePath = sourcePath + ".bones[" + i + "]";

                if (bone == null)
                {
                    throw new CharacterDiagnosticException(
                        bonePath, "Bone entry is null.", CharacterGateCodes.SkeletonHierarchyMissing, sourcePath: sourcePath);
                }

                if (string.IsNullOrWhiteSpace(bone.BoneName))
                {
                    throw new CharacterDiagnosticException(
                        bonePath + ".boneName", "Bone name is required.",
                        CharacterGateCodes.SkeletonHierarchyMissing, sourcePath: sourcePath, field: "boneName");
                }

                if (string.IsNullOrWhiteSpace(bone.Path))
                {
                    throw new CharacterDiagnosticException(
                        bonePath + ".path", "Bone path is required.",
                        CharacterGateCodes.SkeletonHierarchyMissing, sourcePath: sourcePath, field: "path");
                }

                if (bone.BindPoseWorldMatrix == null ||
                    bone.BindPoseWorldMatrix.Length != CharacterMatrix4x4.ElementCount)
                {
                    throw new CharacterDiagnosticException(
                        bonePath + ".bindPoseWorldMatrix",
                        $"bindPoseWorldMatrix must contain exactly {CharacterMatrix4x4.ElementCount} numbers.",
                        CharacterGateCodes.SkeletonHierarchyMissing,
                        sourcePath: sourcePath,
                        field: "bindPoseWorldMatrix");
                }

                for (int element = 0; element < bone.BindPoseWorldMatrix.Length; element++)
                {
                    if (!float.IsFinite(bone.BindPoseWorldMatrix[element]))
                    {
                        throw new CharacterDiagnosticException(
                            bonePath + ".bindPoseWorldMatrix[" + element + "]",
                            "bindPoseWorldMatrix contains a non-finite value (NaN or Infinity).",
                            CharacterGateCodes.SkeletonHierarchyMissing,
                            sourcePath: sourcePath,
                            field: "bindPoseWorldMatrix");
                    }
                }
            }

            // E-SKEL-101：骨架声明的源网格哈希必须与基础网格 FBX 的实际哈希一致。
            if (!string.IsNullOrEmpty(expectedModelSha256) &&
                !string.Equals(hierarchy.SourceMeshSha256, expectedModelSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CharacterDiagnosticException(
                    sourcePath + ".sourceMeshSha256",
                    $"skeleton_hierarchy.sourceMeshSha256 ({hierarchy.SourceMeshSha256}) does not match the base mesh hash ({expectedModelSha256}).",
                    CharacterGateCodes.SkeletonSourceMeshHashMismatch,
                    sourcePath: sourcePath,
                    field: "sourceMeshSha256");
            }
        }

        /// <summary>校验并构建骨架树。</summary>
        public static CharacterSkeletonTree ValidateAndBuild(
            CharacterSkeletonHierarchy hierarchy,
            string sourcePath,
            string expectedModelSha256 = null)
        {
            Validate(hierarchy, sourcePath, expectedModelSha256);
            return CharacterSkeletonTree.Build(hierarchy, sourcePath);
        }

        /// <summary>非抛出式校验，把结果追加到既有诊断通道。</summary>
        public static bool TryValidate(
            CharacterSkeletonHierarchy hierarchy,
            string sourcePath,
            List<CharacterAuthoringValidationIssue> issues,
            string expectedModelSha256 = null)
        {
            try
            {
                Validate(hierarchy, sourcePath, expectedModelSha256);
                return true;
            }
            catch (CharacterDiagnosticException exception)
            {
                issues?.Add(exception.ToValidationIssue());
                return false;
            }
        }
    }
}
