using System;
using System.Collections.Generic;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests
{
    /// <summary>
    /// 规范 v1.4 中立骨架链路（第 1.2 节 Schema、第 4.1 节双锚点重绑定）
    /// 与剩余语义门禁（E-CAT-201 / E-SLOT-301）的回归测试。
    /// </summary>
    public static class CharacterSkeletonContractTests
    {
        public static void RunAll()
        {
            RunMatrixMath();
            RunSkeletonHierarchyValidation();
            RunSkeletonTree();
            RunBoneAnchorResolution();
            RunBoneRebind();
            RunCatalogSemanticGates();
            RunWeaponSlotSemanticGates();
        }

        // ------------------------------------------------------------------
        // §4.1 数学基础
        // ------------------------------------------------------------------

        private static void RunMatrixMath()
        {
            CharacterMatrix4x4 identity = CharacterMatrix4x4.Identity();

            CharacterMatrix4x4 product = CharacterMatrix4x4.Multiply(identity, identity);
            Require(CharacterMatrix4x4.MaxAbsoluteDeviation(product, identity) == 0f,
                "Identity * Identity must be Identity.");

            CharacterMatrix4x4 inverseIdentity = identity.Inverse("m");
            Require(CharacterMatrix4x4.MaxAbsoluteDeviation(inverseIdentity, identity) == 0f,
                "Inverse of Identity must be Identity.");

            // 一般仿射矩阵的逆回环：M · M⁻¹ ≈ I
            var affine = CharacterMatrix4x4.FromArray(new[]
            {
                0.5f, 0f, 0f, 3f,
                0f, 2f, 0f, -1f,
                0f, 0f, 4f, 0.25f,
                0f, 0f, 0f, 1f
            }, "m");

            CharacterMatrix4x4 roundTrip = CharacterMatrix4x4.Multiply(affine, affine.Inverse("m"));
            Require(CharacterMatrix4x4.MaxAbsoluteDeviation(roundTrip, identity) < 1e-4f,
                "M * Inverse(M) must be Identity for an invertible affine matrix.");

            // 奇异矩阵必须抛出，而不是产出 NaN。
            var singular = CharacterMatrix4x4.FromArray(new[]
            {
                1f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f
            }, "m");

            bool singularThrew = false;
            try
            {
                singular.Inverse("m");
            }
            catch (CharacterDiagnosticException)
            {
                singularThrew = true;
            }

            Require(singularThrew, "Singular matrix inversion must throw.");

            // 长度与非有限值必须前置拦截。
            RequireThrows(() => CharacterMatrix4x4.FromArray(new float[15], "m"), "15-element matrix must be rejected.");
            RequireThrows(() => CharacterMatrix4x4.FromArray(new float[17], "m"), "17-element matrix must be rejected.");
            RequireThrows(
                () => CharacterMatrix4x4.FromArray(
                    new[]
                    {
                        1f, 0f, 0f, 0f,
                        0f, 1f, 0f, 0f,
                        0f, 0f, 1f, 0f,
                        0f, 0f, 0f, float.NaN
                    }, "m"),
                "NaN matrix element must be rejected.");

            // 姿态 ↔ 矩阵互转必须闭合。
            var pose = new CharacterAuthoringPose
            {
                Position = new CharacterAuthoringVector3(0.25f, -1.5f, 3f),
                Rotation = QuaternionFromAxisAngleZ(90f),
                Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
            };

            CharacterAuthoringPose restored = CharacterMatrix4x4.FromPose(pose).ToPose("p");
            Require(NearlyEqual(restored.Position.X, pose.Position.X) &&
                    NearlyEqual(restored.Position.Y, pose.Position.Y) &&
                    NearlyEqual(restored.Position.Z, pose.Position.Z),
                "Pose -> Matrix -> Pose must preserve translation.");
            Require(NearlyEqual(restored.Rotation.Z, pose.Rotation.Z) &&
                    NearlyEqual(restored.Rotation.W, pose.Rotation.W),
                "Pose -> Matrix -> Pose must preserve rotation.");
            Require(NearlyEqual(restored.Scale.X, 1f) && NearlyEqual(restored.Scale.Y, 1f) && NearlyEqual(restored.Scale.Z, 1f),
                "Pose -> Matrix -> Pose must preserve unit scale.");
        }

        // ------------------------------------------------------------------
        // §1.2 中立骨架校验
        // ------------------------------------------------------------------

        private static void RunSkeletonHierarchyValidation()
        {
            // E-SRC-109：文档缺失
            bool missingThrew = false;
            try
            {
                CharacterSkeletonHierarchyValidator.Validate(null, "geometry/skeleton_hierarchy.json");
            }
            catch (CharacterDiagnosticException exception)
            {
                missingThrew = true;
                Require(exception.GateCode == CharacterGateCodes.SkeletonHierarchyMissing,
                    "Missing skeleton hierarchy must report E-SRC-109.");
            }

            Require(missingThrew, "Null skeleton hierarchy must be rejected.");

            RequireThrows(() => CharacterSkeletonHierarchyValidator.Validate(MakeSkeleton(schemaVersion: "2.0.0"), "s"),
                "Wrong schemaVersion must be rejected.");
            RequireThrows(() => CharacterSkeletonHierarchyValidator.Validate(MakeSkeleton(packageId: "abc"), "s"),
                "Non-numeric packageId must be rejected.");
            RequireThrows(() => CharacterSkeletonHierarchyValidator.Validate(MakeSkeleton(sourceMeshSha256: "sha256:XYZ"), "s"),
                "Malformed sourceMeshSha256 must be rejected.");

            CharacterSkeletonHierarchy shortMatrix = MakeSkeleton();
            shortMatrix.Bones[0].BindPoseWorldMatrix = new float[12];
            RequireThrows(() => CharacterSkeletonHierarchyValidator.Validate(shortMatrix, "s"),
                "bindPoseWorldMatrix with 12 elements must be rejected.");

            CharacterSkeletonHierarchy nanMatrix = MakeSkeleton();
            nanMatrix.Bones[0].BindPoseWorldMatrix[5] = float.PositiveInfinity;
            RequireThrows(() => CharacterSkeletonHierarchyValidator.Validate(nanMatrix, "s"),
                "bindPoseWorldMatrix with Infinity must be rejected.");

            // E-SKEL-101：源网格哈希绑定
            string modelHash = "sha256:" + new string('a', 64);
            CharacterSkeletonHierarchy matching = MakeSkeleton(sourceMeshSha256: modelHash);
            CharacterSkeletonHierarchyValidator.Validate(matching, "s", modelHash);

            bool mismatchThrew = false;
            try
            {
                CharacterSkeletonHierarchyValidator.Validate(
                    matching, "s", "sha256:" + new string('b', 64));
            }
            catch (CharacterDiagnosticException exception)
            {
                mismatchThrew = true;
                Require(exception.GateCode == CharacterGateCodes.SkeletonSourceMeshHashMismatch,
                    "Hash mismatch must report E-SKEL-101.");
            }

            Require(mismatchThrew, "Skeleton bound to a different mesh hash must be rejected.");

            // 非抛出式通道
            var issues = new List<CharacterAuthoringValidationIssue>();
            Require(!CharacterSkeletonHierarchyValidator.TryValidate(null, "s", issues),
                "TryValidate must return false for a missing hierarchy.");
            Require(issues.Count == 1 && issues[0].Code == CharacterGateCodes.SkeletonHierarchyMissing,
                "TryValidate must surface the gate code.");
        }

        // ------------------------------------------------------------------
        // §1.2 骨架树
        // ------------------------------------------------------------------

        private static void RunSkeletonTree()
        {
            CharacterSkeletonTree tree = CharacterSkeletonTree.Build(MakeSkeleton(), "s");
            Require(tree.Count == 5, "Tree must index all bones. Got " + tree.Count);
            Require(tree.ContainsPath("Root/Hips"), "Tree must index concrete paths.");
            Require(tree.FindByPath("Root/Hips/Spine") != null, "Tree must resolve nested paths.");
            Require(tree.FindByPath("Root/Nope") == null, "Unknown path must resolve to null.");

            // 唯一名查询：歧义返回 null（与仓库既有约定一致）。
            Require(tree.FindUniqueByName("Hips") != null, "Unique bone name must resolve.");
            Require(tree.FindUniqueByName("Duplicate") == null, "Ambiguous bone name must resolve to null.");
            Require(tree.FindAllByName("Duplicate").Count == 2, "FindAllByName must return every match.");

            // 祖先链：根在前
            IReadOnlyList<CharacterSkeletonBone> ancestors = tree.GetAncestors("Root/Hips/Spine");
            Require(ancestors.Count == 2, "Spine must have two ancestors.");
            Require(ancestors[0].Path == "Root" && ancestors[1].Path == "Root/Hips",
                "Ancestors must be ordered root-first.");

            // 语义唯一性
            Require(tree.FindUniqueBySemantic(CharacterHumanoidBone.Hips) != null,
                "Unique humanoid semantic must resolve.");
            Require(tree.FindUniqueBySemantic(CharacterHumanoidBone.None) == null,
                "None semantic must never resolve to a bone.");

            // 结构违规
            CharacterSkeletonHierarchy duplicatePath = MakeSkeleton();
            duplicatePath.Bones[1].Path = "Root";
            RequireThrows(() => CharacterSkeletonTree.Build(duplicatePath, "s"), "Duplicate path must be rejected.");

            CharacterSkeletonHierarchy missingParent = MakeSkeleton();
            missingParent.Bones[1].ParentPath = "Root/Absent";
            RequireThrows(() => CharacterSkeletonTree.Build(missingParent, "s"), "Missing parent must be rejected.");

            CharacterSkeletonHierarchy selfParent = MakeSkeleton();
            selfParent.Bones[1].ParentPath = selfParent.Bones[1].Path;
            RequireThrows(() => CharacterSkeletonTree.Build(selfParent, "s"), "Self-parent must be rejected.");

            var cyclic = new CharacterSkeletonHierarchy { PackageId = "000001", SourceMeshSha256 = Sha('a') };
            cyclic.Bones.Add(MakeBone("A", "A", "B"));
            cyclic.Bones.Add(MakeBone("B", "B", "A"));
            RequireThrows(() => CharacterSkeletonTree.Build(cyclic, "s"), "Parent cycle must be rejected.");
        }

        // ------------------------------------------------------------------
        // §7.1 E-BONE-201 / E-BONE-202
        // ------------------------------------------------------------------

        private static void RunBoneAnchorResolution()
        {
            CharacterSkeletonTree tree = CharacterSkeletonTree.Build(MakeSkeleton(), "s");

            // 具体路径命中
            CharacterBoneAnchorResult concrete = CharacterBoneAnchorResolver.Resolve(
                tree, "Root/Hips", CharacterHumanoidBone.None);
            Require(concrete.IsResolved && concrete.Source == CharacterBoneAnchorSource.ConcretePath,
                "Concrete path must resolve directly.");

            // 遗留别名命中（bone.<name>）——项目既有约定
            Require(CharacterBoneAnchorResolver.IsLegacyBoneAlias("bone.spine"), "bone.spine must be recognized as an alias.");
            CharacterBoneAnchorResult alias = CharacterBoneAnchorResolver.Resolve(
                tree, "bone.spine", CharacterHumanoidBone.None);
            Require(alias.IsResolved && alias.Source == CharacterBoneAnchorSource.LegacyNameAlias,
                "Legacy bone.<name> alias must resolve via unique bone name.");

            // 别名歧义 -> E-BONE-201
            CharacterBoneAnchorResult ambiguous = CharacterBoneAnchorResolver.Resolve(
                tree, "bone.Duplicate", CharacterHumanoidBone.None);
            Require(ambiguous.Verdict == CharacterBoneAnchorVerdict.PathUnresolvedWithoutSemanticFallback,
                "Ambiguous alias must fall through to E-BONE-201.");
            Require(ambiguous.GateCode == CharacterGateCodes.BonePathUnresolvedWithoutSemantic,
                "Ambiguous alias must report E-BONE-201.");

            // 路径不可解析 + 无语义 -> E-BONE-201
            CharacterBoneAnchorResult unresolved = CharacterBoneAnchorResolver.Resolve(
                tree, "bone.does_not_exist", CharacterHumanoidBone.None);
            Require(unresolved.Verdict == CharacterBoneAnchorVerdict.PathUnresolvedWithoutSemanticFallback,
                "Unresolvable path without semantic must be E-BONE-201.");
            RequireThrows(
                () => CharacterBoneAnchorResolver.ResolveOrThrow(tree, "bone.does_not_exist", CharacterHumanoidBone.None, "c[0]"),
                "ResolveOrThrow must throw for E-BONE-201.");

            // 路径不可解析 + 语义备用锚点 -> 解析成功
            CharacterBoneAnchorResult viaSemantic = CharacterBoneAnchorResolver.Resolve(
                tree, "bone.does_not_exist", CharacterHumanoidBone.Hips);
            Require(viaSemantic.IsResolved && viaSemantic.Source == CharacterBoneAnchorSource.HumanoidSemantic,
                "Humanoid semantic must act as the fallback anchor.");
            Require(viaSemantic.Bone.Path == "Root/Hips", "Fallback anchor must select the semantic bone.");

            // 路径与语义冲突 -> E-BONE-202
            CharacterBoneAnchorResult conflict = CharacterBoneAnchorResolver.Resolve(
                tree, "Root/Hips/Spine", CharacterHumanoidBone.Hips);
            Require(conflict.Verdict == CharacterBoneAnchorVerdict.PathSemanticConflict,
                "Path/semantic disagreement must be E-BONE-202.");
            Require(conflict.GateCode == CharacterGateCodes.BonePathSemanticConflict,
                "Conflict must report E-BONE-202.");

            // 路径与语义一致 -> 通过
            CharacterBoneAnchorResult agreeing = CharacterBoneAnchorResolver.Resolve(
                tree, "Root/Hips", CharacterHumanoidBone.Hips);
            Require(agreeing.IsResolved && agreeing.Source == CharacterBoneAnchorSource.ConcretePath,
                "Agreeing anchors must resolve via the concrete path.");

            // 骨骼名规范化：与仓库既有 CharacterHumanoidHurtboxTemplate.Normalize 规则一致
            Require(CharacterSkeletonTree.NormalizeBoneName("mixamorig:Hips") == "hips",
                "mixamorig prefix must be normalized away.");
            Require(CharacterSkeletonTree.NormalizeBoneName("Armature|Spine") == "spine",
                "Namespace prefix before ':' must be normalized away.");
            Require(CharacterSkeletonTree.NormalizeBoneName("Left_Arm") == "leftarm",
                "Separators must be normalized away.");
            Require(CharacterSkeletonTree.NormalizeBoneName("mixamorig:Hips") ==
                    CharacterSkeletonTree.NormalizeBoneName("hips"),
                "Normalization must be case-insensitive.");

            // DCC 命名差异下的别名解析
            var dccSkeleton = new CharacterSkeletonHierarchy { PackageId = "000001", SourceMeshSha256 = Sha('a') };
            dccSkeleton.Bones.Add(MakeBone("mixamorig:Hips", "Hips", string.Empty, CharacterHumanoidBone.Hips));
            CharacterSkeletonTree dccTree = CharacterSkeletonTree.Build(dccSkeleton, "s");
            Require(CharacterBoneAnchorResolver.Resolve(dccTree, "bone.hips", CharacterHumanoidBone.None).IsResolved,
                "bone.hips must resolve against a bone named 'mixamorig:Hips'.");

            // 规范化后产生歧义时必须回到 E-BONE-201，不得静默择一
            var ambiguousAfterNormalization = new CharacterSkeletonHierarchy
            {
                PackageId = "000001",
                SourceMeshSha256 = Sha('a')
            };
            ambiguousAfterNormalization.Bones.Add(MakeBone("Left_Arm", "A", string.Empty));
            ambiguousAfterNormalization.Bones.Add(MakeBone("LeftArm", "B", string.Empty));
            CharacterSkeletonTree ambiguousTree = CharacterSkeletonTree.Build(ambiguousAfterNormalization, "s");
            CharacterBoneAnchorResult normalizationAmbiguity = CharacterBoneAnchorResolver.Resolve(
                ambiguousTree, "bone.leftarm", CharacterHumanoidBone.None);
            Require(normalizationAmbiguity.Verdict == CharacterBoneAnchorVerdict.PathUnresolvedWithoutSemanticFallback,
                "Names that collide after normalization must fall through to E-BONE-201.");
        }

        // ------------------------------------------------------------------
        // §4.1 双锚点重绑定
        // ------------------------------------------------------------------

        private static void RunBoneRebind()
        {
            // Δ_B = 0 -> 透明重绑定，姿态保持
            CharacterMatrix4x4 bind = CharacterMatrix4x4.Identity();
            var pose = new CharacterAuthoringPose
            {
                Position = new CharacterAuthoringVector3(1f, 2f, 3f),
                Rotation = new CharacterAuthoringQuaternion(0f, 0f, 0f, 1f)
            };

            CharacterBoneRebindEntry unchanged = CharacterBoneRebindSolver.RebindBone(
                "Root/Hips", pose, bind, bind, "c[0]");
            Require(unchanged.Verdict == CharacterBoneRebindVerdict.NotRequired,
                "Identical bind matrices must not require rebinding.");
            Require(ReferenceEquals(unchanged.AuthoredPose, unchanged.CandidatePose),
                "Transparent rebind must keep the authored pose untouched.");
            Require(CharacterBoneRebindSolver.ComputeDeltaB(bind, bind) == 0f, "Δ_B of identical matrices must be 0.");

            // 容差内 -> 仍视为透明
            CharacterMatrix4x4 nearIdentical = CharacterMatrix4x4.FromArray(new[]
            {
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f
            }, "m");
            nearIdentical.Values[3] = 1e-5f;
            Require(CharacterBoneRebindSolver.ComputeDeltaB(bind, nearIdentical) <= CharacterBoneRebindSolver.DeltaBTolerance,
                "Δ_B below tolerance must stay within the transparent band.");

            // 超容差 -> 人工审查
            CharacterMatrix4x4 moved = CharacterMatrix4x4.FromArray(new[]
            {
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0.25f,
                0f, 0f, 1f, 0f,
                0f, 0f, 0f, 1f
            }, "m");

            CharacterBoneRebindEntry movedEntry = CharacterBoneRebindSolver.RebindBone(
                "Root/Hips", pose, bind, moved, "c[0]");
            Require(movedEntry.Verdict == CharacterBoneRebindVerdict.ManualReviewRequired,
                "Δ_B above tolerance must require manual review.");
            Require(movedEntry.DeltaB > CharacterBoneRebindSolver.DeltaBTolerance,
                "Reported Δ_B must exceed tolerance.");

            // 候选姿态：纯平移偏移 -> P_new = T(1, 2, 3) 平移 -0.25
            Require(NearlyEqual(movedEntry.CandidatePose.Position.Y, 2f - 0.25f),
                "Candidate pose must apply B_new^-1 * B_old to the authored pose. Got " +
                movedEntry.CandidatePose.Position.Y);

            // 候选姿态：绕 Z 轴 90° 的骨架变化
            var rotationPose = new CharacterAuthoringPose
            {
                Position = new CharacterAuthoringVector3(1f, 0f, 0f),
                Rotation = new CharacterAuthoringQuaternion(0f, 0f, 0f, 1f)
            };
            CharacterMatrix4x4 rotatedBind = CharacterMatrix4x4.FromPose(new CharacterAuthoringPose
            {
                Position = new CharacterAuthoringVector3(0f, 0f, 0f),
                Rotation = QuaternionFromAxisAngleZ(90f)
            });

            CharacterBoneRebindEntry rotated = CharacterBoneRebindSolver.RebindBone(
                "Root/Hips", rotationPose, bind, rotatedBind, "c[0]");
            Require(rotated.Verdict == CharacterBoneRebindVerdict.ManualReviewRequired,
                "Rotated bind must require manual review.");
            Require(NearlyEqual(rotated.CandidatePose.Position.X, 0f) &&
                    NearlyEqual(rotated.CandidatePose.Position.Y, -1f),
                "Candidate pose must rotate the offset by B_new^-1. Got (" +
                rotated.CandidatePose.Position.X + ", " + rotated.CandidatePose.Position.Y + ")");
            Require(NearlyEqual(rotated.CandidatePose.Scale.X, 1f),
                "Candidate pose scale must remain unit for a pure rotation rebase.");

            // 奇异绑定矩阵必须抛出
            var singular = CharacterMatrix4x4.FromArray(new[]
            {
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 0f,
                0f, 0f, 0f, 1f
            }, "m");
            RequireThrows(
                () => CharacterBoneRebindSolver.RebindBone("b", pose, bind, singular, "c[0]"),
                "Singular new bind matrix must be rejected.");

            // 报告聚合与人工审查闸门
            CharacterRebindCandidateReport report = CharacterBoneRebindSolver.BuildReport(
                "000001",
                Sha('c'),
                new[] { unchanged, movedEntry },
                snapshotMissing: false);
            Require(report.ManualReviewCount == 1 && report.TransparentCount == 1,
                "Report must classify each bone.");
            Require(report.RequiresManualReview, "Report with a moved bone must require manual review.");
            Require(report.ManualReviewBones.Count == 1 && report.ManualReviewBones[0] == "Root/Hips",
                "Report must list the bones needing review.");
            RequireThrows(() => CharacterBoneRebindSolver.AssertNoManualReview(report, "body_colliders"),
                "AssertNoManualReview must block a report with pending items.");

            // 快照丢失 -> 整体转人工
            CharacterRebindCandidateReport lostSnapshot = CharacterBoneRebindSolver.BuildReport(
                "000001", Sha('c'), new[] { unchanged }, snapshotMissing: true);
            Require(lostSnapshot.RequiresManualReview,
                "A missing authored skeleton snapshot must force manual review even when Δ_B is zero.");
            RequireThrows(() => CharacterBoneRebindSolver.AssertNoManualReview(lostSnapshot, "body_colliders"),
                "Missing snapshot must block automatic rebinding.");

            // 全部透明时不得阻断
            CharacterRebindCandidateReport clean = CharacterBoneRebindSolver.BuildReport(
                "000001", Sha('c'), new[] { unchanged }, snapshotMissing: false);
            CharacterBoneRebindSolver.AssertNoManualReview(clean, "body_colliders");
        }

        // ------------------------------------------------------------------
        // §1.4 E-CAT-201
        // ------------------------------------------------------------------

        private static void RunCatalogSemanticGates()
        {
            var valid = new List<CharacterCatalogEntryView>
            {
                new CharacterCatalogEntryView
                {
                    ResourceKey = "char.000001.model.lod0",
                    RelativePath = "resources/models/000001_lod0.fbx",
                    ContentType = CharacterCatalogSemanticValidator.ModelContentType
                },
                new CharacterCatalogEntryView
                {
                    ResourceKey = "char.000001.anim.idle",
                    RelativePath = "resources/animations/idle.fbx",
                    ContentType = CharacterCatalogSemanticValidator.AnimationContentType
                },
                new CharacterCatalogEntryView
                {
                    ResourceKey = "char.000001.tex.palette",
                    RelativePath = "resources/textures/palette_base.png",
                    ContentType = CharacterCatalogSemanticValidator.TextureContentType
                }
            };

            Require(CharacterCatalogSemanticValidator.Validate(valid, "resource_catalog.json").Count == 0,
                "Valid catalog entries must pass.");

            var duplicated = new List<CharacterCatalogEntryView>(valid)
            {
                new CharacterCatalogEntryView
                {
                    ResourceKey = "char.000001.model.lod0",
                    RelativePath = "resources/models/000001_lod0_alt.fbx",
                    ContentType = CharacterCatalogSemanticValidator.ModelContentType
                }
            };

            IReadOnlyList<CharacterAuthoringValidationIssue> duplicateIssues =
                CharacterCatalogSemanticValidator.Validate(duplicated, "resource_catalog.json");
            Require(duplicateIssues.Count == 1 && duplicateIssues[0].Code == CharacterGateCodes.ResourceCatalogDuplicateKey,
                "Duplicate resourceKey must report E-CAT-201.");
            RequireThrows(
                () => CharacterCatalogSemanticValidator.AssertValid(duplicated, "resource_catalog.json"),
                "AssertValid must throw on duplicate resourceKey.");

            // 扩展名与 contentType 强绑定
            var mismatched = new List<CharacterCatalogEntryView>
            {
                new CharacterCatalogEntryView
                {
                    ResourceKey = "k",
                    RelativePath = "resources/models/a.fbx",
                    ContentType = CharacterCatalogSemanticValidator.TextureContentType
                }
            };
            Require(CharacterCatalogSemanticValidator.Validate(mismatched, "c").Count == 1,
                "contentType must be bound to the resource location.");

            // 遗留 .tga 已移除
            var tga = new List<CharacterCatalogEntryView>
            {
                new CharacterCatalogEntryView
                {
                    ResourceKey = "k",
                    RelativePath = "resources/textures/legacy.tga",
                    ContentType = CharacterCatalogSemanticValidator.TextureContentType
                }
            };
            Require(CharacterCatalogSemanticValidator.Validate(tga, "c").Count == 1,
                "Legacy .tga must be rejected.");

            // 注册位置之外的路径（例如 previews/）不得通过
            var outsideRegistry = new List<CharacterCatalogEntryView>
            {
                new CharacterCatalogEntryView
                {
                    ResourceKey = "k",
                    RelativePath = "previews/thumbnail.png",
                    ContentType = CharacterCatalogSemanticValidator.TextureContentType
                }
            };
            Require(CharacterCatalogSemanticValidator.Validate(outsideRegistry, "c").Count == 1,
                "Paths outside the registered resource locations must be rejected.");
            Require(CharacterCatalogSemanticValidator.ExpectedContentType("previews/thumbnail.png") == null,
                "previews/ is not a registered resource location under section 1.4.");
        }

        // ------------------------------------------------------------------
        // §1.5 E-SLOT-301
        // ------------------------------------------------------------------

        private static void RunWeaponSlotSemanticGates()
        {
            var sockets = new[] { "socket.hand.R", "socket.back" };

            var valid = new List<CharacterWeaponSlotView>
            {
                new CharacterWeaponSlotView
                {
                    SlotId = "mainHand",
                    AttachSocketId = "socket.hand.R",
                    AllowedWeaponTypes = new List<string> { "SwordSingle", "Dagger" }
                },
                new CharacterWeaponSlotView
                {
                    SlotId = "back",
                    AttachSocketId = "socket.back",
                    AllowedWeaponTypes = new List<string> { "GreatSword" }
                }
            };

            Require(CharacterWeaponSlotSemanticValidator.Validate(valid, sockets, "weapon_slots.json").Count == 0,
                "Valid weapon slots must pass.");

            // 悬空 attachSocketId -> E-SLOT-301
            var dangling = new List<CharacterWeaponSlotView>
            {
                new CharacterWeaponSlotView
                {
                    SlotId = "mainHand",
                    AttachSocketId = "socket.missing",
                    AllowedWeaponTypes = new List<string> { "SwordSingle" }
                }
            };

            IReadOnlyList<CharacterAuthoringValidationIssue> danglingIssues =
                CharacterWeaponSlotSemanticValidator.Validate(dangling, sockets, "weapon_slots.json");
            Require(danglingIssues.Count == 1 && danglingIssues[0].Code == CharacterGateCodes.WeaponSlotSocketDangling,
                "Dangling attachSocketId must report E-SLOT-301.");
            RequireThrows(
                () => CharacterWeaponSlotSemanticValidator.AssertValid(dangling, sockets, "weapon_slots.json"),
                "AssertValid must throw on a dangling socket.");

            // slotId 唯一性
            var duplicateSlot = new List<CharacterWeaponSlotView>
            {
                new CharacterWeaponSlotView
                {
                    SlotId = "mainHand", AttachSocketId = "socket.hand.R",
                    AllowedWeaponTypes = new List<string> { "SwordSingle" }
                },
                new CharacterWeaponSlotView
                {
                    SlotId = "mainHand", AttachSocketId = "socket.back",
                    AllowedWeaponTypes = new List<string> { "Bow" }
                }
            };
            Require(CharacterWeaponSlotSemanticValidator.Validate(duplicateSlot, sockets, "w").Count == 1,
                "Duplicate slotId must be rejected.");

            // allowedWeaponTypes 空
            var emptyTypes = new List<CharacterWeaponSlotView>
            {
                new CharacterWeaponSlotView
                {
                    SlotId = "s", AttachSocketId = "socket.back", AllowedWeaponTypes = new List<string>()
                }
            };
            Require(CharacterWeaponSlotSemanticValidator.Validate(emptyTypes, sockets, "w").Count == 1,
                "Empty allowedWeaponTypes must be rejected.");

            // 未知武器类型
            var unknownType = new List<CharacterWeaponSlotView>
            {
                new CharacterWeaponSlotView
                {
                    SlotId = "s", AttachSocketId = "socket.back",
                    AllowedWeaponTypes = new List<string> { "Halberd" }
                }
            };
            Require(CharacterWeaponSlotSemanticValidator.Validate(unknownType, sockets, "w").Count == 1,
                "Weapon types outside the section 1.5 enum must be rejected.");
        }

        // ------------------------------------------------------------------
        // 夹具
        // ------------------------------------------------------------------

        /// <summary>
        /// 三骨骼夹具：Root（无父）→ Root/Hips（Hips 语义）→ Root/Hips/Spine（Spine 语义），
        /// 外加两个同名骨骼 Duplicate 用于歧义用例。
        /// </summary>
        private static CharacterSkeletonHierarchy MakeSkeleton(
            string schemaVersion = CharacterSkeletonHierarchy.CurrentSchemaVersion,
            string packageId = "000001",
            string sourceMeshSha256 = null)
        {
            var hierarchy = new CharacterSkeletonHierarchy
            {
                SchemaVersion = schemaVersion,
                PackageId = packageId,
                SourceMeshSha256 = sourceMeshSha256 ?? Sha('a')
            };

            hierarchy.Bones.Add(MakeBone("Root", "Root", string.Empty, CharacterHumanoidBone.None));
            hierarchy.Bones.Add(MakeBone("Hips", "Root/Hips", "Root", CharacterHumanoidBone.Hips));
            hierarchy.Bones.Add(MakeBone("Spine", "Root/Hips/Spine", "Root/Hips", CharacterHumanoidBone.Spine));

            // 同名歧义夹具
            hierarchy.Bones.Add(MakeBone("Duplicate", "Root/DuplicateA", "Root", CharacterHumanoidBone.None));
            hierarchy.Bones.Add(MakeBone("Duplicate", "Root/DuplicateB", "Root", CharacterHumanoidBone.None));

            return hierarchy;
        }

        private static CharacterSkeletonBone MakeBone(
            string name,
            string path,
            string parentPath,
            CharacterHumanoidBone semantic = CharacterHumanoidBone.None)
        {
            return new CharacterSkeletonBone
            {
                BoneName = name,
                Path = path,
                ParentPath = parentPath,
                HumanoidSemantic = semantic,
                BindPoseWorldMatrix = new[]
                {
                    1f, 0f, 0f, 0f,
                    0f, 1f, 0f, 0f,
                    0f, 0f, 1f, 0f,
                    0f, 0f, 0f, 1f
                }
            };
        }

        private static CharacterAuthoringQuaternion QuaternionFromAxisAngleZ(float degrees)
        {
            double half = degrees * Math.PI / 180.0 / 2.0;
            return new CharacterAuthoringQuaternion(0f, 0f, (float)Math.Sin(half), (float)Math.Cos(half));
        }

        private static string Sha(char fill)
        {
            return "sha256:" + new string(fill, 64);
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) <= 1e-4f;
        }

        private static void RequireThrows(Action action, string message)
        {
            bool threw = false;
            try
            {
                action();
            }
            catch (CharacterDiagnosticException)
            {
                threw = true;
            }

            Require(threw, message);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
