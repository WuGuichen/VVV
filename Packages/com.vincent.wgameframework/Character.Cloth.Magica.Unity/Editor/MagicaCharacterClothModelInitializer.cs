using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MxFramework.CharacterCloth.Magica.Unity.Editor
{
    internal sealed class MagicaCharacterClothModelInitializationResult
    {
        internal bool Succeeded { get; private set; }
        internal string Message { get; private set; } = string.Empty;
        internal MagicaCharacterClothAuthoringEntry[] Cloths { get; private set; } =
            Array.Empty<MagicaCharacterClothAuthoringEntry>();
        internal int ExcludedBaseBoneCount { get; private set; }
        internal int ExcludedBreastChainCount { get; private set; }
        internal int ExcludedSingleBoneChainCount { get; private set; }
        internal int ExcludedZeroWeightChainCount { get; private set; }
        internal int CreatedColliderCount { get; private set; }

        internal static MagicaCharacterClothModelInitializationResult Failed(string message) =>
            new MagicaCharacterClothModelInitializationResult { Message = message ?? string.Empty };

        internal static MagicaCharacterClothModelInitializationResult Created(
            List<MagicaCharacterClothAuthoringEntry> cloths,
            int baseBones,
            int breastChains,
            int singleBoneChains,
            int zeroWeightChains,
            int colliderCount)
        {
            MagicaCharacterClothAuthoringEntry[] created = cloths?.ToArray()
                ?? Array.Empty<MagicaCharacterClothAuthoringEntry>();
            return new MagicaCharacterClothModelInitializationResult
            {
                Succeeded = created.Length > 0,
                Cloths = created,
                ExcludedBaseBoneCount = baseBones,
                ExcludedBreastChainCount = breastChains,
                ExcludedSingleBoneChainCount = singleBoneChains,
                ExcludedZeroWeightChainCount = zeroWeightChains,
                CreatedColliderCount = colliderCount,
                Message = created.Length > 0
                    ? $"已根据模型创建 {created.Length} 个 Cloth 候选和 {colliderCount} 个防穿模 Collider；排除基础骨 {baseBones} 根、胸部链 {breastChains} 条、单骨链 {singleBoneChains} 条、零权重链 {zeroWeightChains} 条。请检查 Root，并微调 Preset 与 Collider 后保存。"
                    : $"模型中没有可初始化的 Cloth 骨链；已排除基础骨 {baseBones} 根、胸部链 {breastChains} 条、单骨链 {singleBoneChains} 条、零权重链 {zeroWeightChains} 条。",
            };
        }
    }

    internal static class MagicaCharacterClothModelInitializer
    {
        private sealed class CandidateGroup
        {
            internal string Id = string.Empty;
            internal readonly List<Transform> Roots = new List<Transform>();
        }

        private const string CapePresetPath = "Assets/MagicaCloth2/Res/Preset/MC2_Preset_Cape.json";
        private const float WeightEpsilon = 0.000001f;

        private static readonly HashSet<string> BaseBodyBoneNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "root.x", "pelvis.x", "spine_01.x", "spine_02.x", "upper_chest.x", "neck.x", "head.x",
            "shoulder.l", "upper_arm.l", "forearm.l", "hand.l",
            "shoulder.r", "upper_arm.r", "forearm.r", "hand.r",
            "thigh.l", "lower_leg.l", "foot.l", "toes.l",
            "thigh.r", "lower_leg.r", "foot.r", "toes.r",
        };

        private static readonly string[] FingerPrefixes =
        {
            "thumb", "index", "middle", "ring", "pinky",
        };

        internal static MagicaCharacterClothModelInitializationResult Create(Transform bodyModel)
        {
            if (bodyModel == null)
                return MagicaCharacterClothModelInitializationResult.Failed(
                    "模型数据初始化失败：当前 Preview 中没有 BodyModel。");

            SkinnedMeshRenderer[] renderers = bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
                return MagicaCharacterClothModelInitializationResult.Failed(
                    "模型数据初始化失败：BodyModel 中没有 SkinnedMeshRenderer。");

            var allSkinnedBones = new HashSet<Transform>();
            var weightedBones = new HashSet<Transform>();
            try
            {
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    CollectRendererBones(renderers[rendererIndex], allSkinnedBones, weightedBones);
            }
            catch (UnityException exception)
            {
                return MagicaCharacterClothModelInitializationResult.Failed(
                    "模型数据初始化失败：无法读取蒙皮权重。" + exception.Message);
            }

            int baseBoneCount = 0;
            var customBones = new HashSet<Transform>();
            foreach (Transform bone in allSkinnedBones)
            {
                if (bone == null) continue;
                if (IsBaseBoneName(bone.name)) baseBoneCount++;
                else customBones.Add(bone);
            }

            var roots = new List<Transform>();
            foreach (Transform bone in customBones)
                if (bone != null && (bone.parent == null || !customBones.Contains(bone.parent)))
                    roots.Add(bone);
            roots.Sort((left, right) => string.CompareOrdinal(
                AnimationUtility.CalculateTransformPath(left, bodyModel),
                AnimationUtility.CalculateTransformPath(right, bodyModel)));

            if (!TryResolvePreset(CapePresetPath, out string capeHash, out string presetError))
                return MagicaCharacterClothModelInitializationResult.Failed(presetError);

            var groups = new Dictionary<string, CandidateGroup>(StringComparer.Ordinal);
            int breastChains = 0;
            int singleBoneChains = 0;
            int zeroWeightChains = 0;
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                Transform root = roots[rootIndex];
                List<Transform> chain = CollectCustomSubtree(root, customBones);
                if (IsBreastChain(chain))
                {
                    breastChains++;
                    continue;
                }
                if (chain.Count < 2)
                {
                    singleBoneChains++;
                    continue;
                }
                if (!chain.Exists(weightedBones.Contains))
                {
                    zeroWeightChains++;
                    continue;
                }

                ResolveGroup(root, out string groupId);
                if (!groups.TryGetValue(groupId, out CandidateGroup group))
                {
                    group = new CandidateGroup { Id = groupId };
                    groups.Add(groupId, group);
                }
                group.Roots.Add(root);
            }

            var orderedGroups = new List<CandidateGroup>(groups.Values);
            orderedGroups.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            var cloths = new List<MagicaCharacterClothAuthoringEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int colliderCount = 0;
            for (int groupIndex = 0; groupIndex < orderedGroups.Count; groupIndex++)
            {
                CandidateGroup group = orderedGroups[groupIndex];
                group.Roots.Sort((left, right) => string.CompareOrdinal(
                    AnimationUtility.CalculateTransformPath(left, bodyModel),
                    AnimationUtility.CalculateTransformPath(right, bodyModel)));
                string[] rootPaths = group.Roots.ConvertAll(
                    root => AnimationUtility.CalculateTransformPath(root, bodyModel).Replace('\\', '/')).ToArray();
                var entry = new MagicaCharacterClothAuthoringEntry
                {
                    id = UniqueId(group.Id, ids),
                    clothType = "BoneCloth",
                    rootBonePaths = rootPaths,
                    connectionMode = "Line",
                    presetAssetPath = CapePresetPath,
                    presetSha256 = capeHash,
                    parameterOverrides = CreateStableDefaultOverrides(group.Id),
                };
                entry.colliders = CreateCollidersForCloth(bodyModel, entry);
                colliderCount += entry.colliders.Length;
                cloths.Add(entry);
            }

            return MagicaCharacterClothModelInitializationResult.Created(
                cloths,
                baseBoneCount,
                breastChains,
                singleBoneChains,
                zeroWeightChains,
                colliderCount);
        }

        private static MagicaCharacterClothParameterOverrides CreateStableDefaultOverrides(
            string groupId)
        {
            bool lowerGarment = string.Equals(groupId, "lower_garment", StringComparison.Ordinal);
            return new MagicaCharacterClothParameterOverrides
            {
                damping = new MagicaCharacterClothFloatOverride(lowerGarment ? 0.14f : 0.17f),
                worldInertia = new MagicaCharacterClothFloatOverride(lowerGarment ? 0.55f : 0.40f),
                localInertia = new MagicaCharacterClothFloatOverride(lowerGarment ? 0.70f : 0.60f),
                movementInertiaSmoothing = new MagicaCharacterClothFloatOverride(lowerGarment ? 0.55f : 0.60f),
                movementSpeedLimit = new MagicaCharacterClothLimitOverride(
                    true,
                    lowerGarment ? 3.5f : 3.0f),
                rotationSpeedLimit = new MagicaCharacterClothLimitOverride(
                    true,
                    lowerGarment ? 540f : 480f),
                particleSpeedLimit = new MagicaCharacterClothLimitOverride(
                    true,
                    lowerGarment ? 3.5f : 3.0f),
            };
        }

        internal static MagicaCharacterClothAuthoringCollider[] CreateCollidersForCloth(
            Transform bodyModel,
            MagicaCharacterClothAuthoringEntry cloth)
        {
            if (bodyModel == null || cloth?.rootBonePaths == null || cloth.rootBonePaths.Length == 0)
                return Array.Empty<MagicaCharacterClothAuthoringCollider>();
            float height = EstimateCharacterHeight(bodyModel);
            var colliders = new List<MagicaCharacterClothAuthoringCollider>();
            var baseParents = new List<Transform>();
            for (int rootIndex = 0; rootIndex < cloth.rootBonePaths.Length; rootIndex++)
            {
                Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(
                    bodyModel,
                    cloth.rootBonePaths[rootIndex],
                    out bool ambiguous);
                if (root == null || ambiguous) continue;
                Transform baseParent = FindNearestBaseParent(root);
                if (baseParent != null && !baseParents.Contains(baseParent)) baseParents.Add(baseParent);
            }
            if (baseParents.Count == 0)
                return Array.Empty<MagicaCharacterClothAuthoringCollider>();
            baseParents.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            bool lowerBody = baseParents.Exists(IsLowerBodyClothParent);
            if (lowerBody)
            {
                Transform thighLeft = FindBone(bodyModel, "thigh.l");
                Transform thighRight = FindBone(bodyModel, "thigh.r");
                Transform lowerLegLeft = FindBone(bodyModel, "lower_leg.l");
                if (thighLeft != null && lowerLegLeft != null)
                    colliders.Add(CreateCapsuleAcross(
                        bodyModel,
                        "thigh_l",
                        thighLeft,
                        thighLeft.position,
                        lowerLegLeft.position,
                        height * 0.045f));
                Transform lowerLegRight = FindBone(bodyModel, "lower_leg.r");
                if (thighRight != null && lowerLegRight != null)
                    colliders.Add(CreateCapsuleAcross(
                        bodyModel,
                        "thigh_r",
                        thighRight,
                        thighRight.position,
                        lowerLegRight.position,
                        height * 0.045f));
                return colliders.ToArray();
            }

            bool upperBody = baseParents.Exists(IsUpperBodyClothParent);
            if (upperBody)
            {
                colliders.Add(CreateTorsoCollider(bodyModel, height));
                string side = ResolveUpperBodySide(bodyModel, cloth, baseParents);
                AddUpperArmCollider(bodyModel, colliders, side, height);
                return colliders.ToArray();
            }

            if (colliders.Count == 0)
                colliders.Add(CreateSphere(bodyModel, "body", baseParents[0], height * 0.05f));
            return colliders.ToArray();
        }

        private static bool IsLowerBodyClothParent(Transform value)
        {
            string name = value != null ? value.name : string.Empty;
            return string.Equals(name, "pelvis.x", StringComparison.Ordinal)
                || name.StartsWith("thigh.", StringComparison.Ordinal)
                || name.StartsWith("lower_leg.", StringComparison.Ordinal);
        }

        private static bool IsUpperBodyClothParent(Transform value)
        {
            string name = value != null ? value.name : string.Empty;
            return string.Equals(name, "upper_chest.x", StringComparison.Ordinal)
                || name.StartsWith("shoulder.", StringComparison.Ordinal)
                || name.StartsWith("upper_arm.", StringComparison.Ordinal)
                || name.StartsWith("forearm.", StringComparison.Ordinal)
                || name.StartsWith("hand.", StringComparison.Ordinal);
        }

        private static string ResolveUpperBodySide(
            Transform bodyModel,
            MagicaCharacterClothAuthoringEntry cloth,
            List<Transform> baseParents)
        {
            int left = 0;
            int right = 0;
            for (int parentIndex = 0; parentIndex < baseParents.Count; parentIndex++)
                CountUpperBodySide(baseParents[parentIndex], ref left, ref right);

            var roots = new List<Transform>();
            for (int rootIndex = 0; rootIndex < cloth.rootBonePaths.Length; rootIndex++)
            {
                Transform root = MagicaCharacterClothAuthoringValidator.FindTransform(
                    bodyModel,
                    cloth.rootBonePaths[rootIndex],
                    out bool ambiguous);
                if (root == null || ambiguous) continue;
                roots.Add(root);
                Transform current = root;
                while (current != null && current != bodyModel)
                {
                    CountUpperBodySide(current, ref left, ref right);
                    current = current.parent;
                }
            }
            if (left > right) return "l";
            if (right > left) return "r";

            Transform upperArmLeft = FindBone(bodyModel, "upper_arm.l");
            Transform upperArmRight = FindBone(bodyModel, "upper_arm.r");
            if (upperArmLeft == null) return upperArmRight != null ? "r" : string.Empty;
            if (upperArmRight == null) return "l";
            if (roots.Count == 0) return "l";

            float leftDistance = 0f;
            float rightDistance = 0f;
            for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
            {
                leftDistance += (roots[rootIndex].position - upperArmLeft.position).sqrMagnitude;
                rightDistance += (roots[rootIndex].position - upperArmRight.position).sqrMagnitude;
            }
            return leftDistance <= rightDistance ? "l" : "r";
        }

        private static void CountUpperBodySide(Transform value, ref int left, ref int right)
        {
            string name = value != null ? value.name : string.Empty;
            if (name.EndsWith(".l", StringComparison.Ordinal)) left++;
            else if (name.EndsWith(".r", StringComparison.Ordinal)) right++;
        }

        private static void AddUpperArmCollider(
            Transform bodyModel,
            List<MagicaCharacterClothAuthoringCollider> colliders,
            string side,
            float height)
        {
            if (string.IsNullOrWhiteSpace(side)) return;
            Transform upperArm = FindBone(bodyModel, "upper_arm." + side);
            if (upperArm == null) return;
            Transform forearm = FindBone(bodyModel, "forearm." + side);
            if (forearm != null)
            {
                colliders.Add(CreateCapsuleAcross(
                    bodyModel,
                    "upper_arm_" + side,
                    upperArm,
                    upperArm.position,
                    forearm.position,
                    height * 0.04f));
                return;
            }
            colliders.Add(CreateSphere(
                bodyModel,
                "upper_arm_" + side,
                upperArm,
                height * 0.055f));
        }

        private static MagicaCharacterClothAuthoringCollider CreateTorsoCollider(
            Transform bodyModel,
            float height)
        {
            Transform pelvis = FindBone(bodyModel, "pelvis.x");
            Transform upperEnd = FindBone(bodyModel, "neck.x")
                ?? FindBone(bodyModel, "upper_chest.x")
                ?? FindBone(bodyModel, "spine_02.x");
            if (pelvis != null && upperEnd != null)
                return CreateCapsuleAcross(
                    bodyModel,
                    "torso",
                    pelvis,
                    pelvis.position,
                    upperEnd.position,
                    height * 0.075f);
            Transform fallback = pelvis ?? upperEnd ?? bodyModel;
            return CreateSphere(bodyModel, "torso", fallback, height * 0.08f);
        }

        private static void ResolveGroup(Transform root, out string groupId)
        {
            Transform baseParent = FindNearestBaseParent(root);
            string parentName = baseParent != null ? baseParent.name : string.Empty;
            if (string.Equals(parentName, "pelvis.x", StringComparison.Ordinal))
            {
                groupId = "lower_garment";
                return;
            }
            if (string.Equals(parentName, "upper_chest.x", StringComparison.Ordinal))
            {
                groupId = "upper_side_accessories";
                return;
            }
            if (parentName.StartsWith("upper_arm.", StringComparison.Ordinal))
            {
                groupId = "arm_accessories";
                return;
            }
            groupId = root != null ? root.name : "cloth";
        }

        private static MagicaCharacterClothAuthoringCollider CreateSphere(
            Transform bodyModel,
            string id,
            Transform parent,
            float radius)
        {
            return new MagicaCharacterClothAuthoringCollider
            {
                id = id,
                type = "Sphere",
                parentBonePath = RelativePath(parent, bodyModel),
                localPosition = new MagicaCharacterClothAuthoringVector(),
                localRotationEuler = new MagicaCharacterClothAuthoringVector(),
                radius = Mathf.Max(0.015f, radius),
                endRadius = 0f,
                length = 0f,
                direction = "Y",
            };
        }

        private static MagicaCharacterClothAuthoringCollider CreateCapsuleAcross(
            Transform bodyModel,
            string id,
            Transform parent,
            Vector3 worldStart,
            Vector3 worldEnd,
            float radius)
        {
            Vector3 localStart = parent.InverseTransformPoint(worldStart);
            Vector3 localEnd = parent.InverseTransformPoint(worldEnd);
            Vector3 span = localEnd - localStart;
            float safeRadius = Mathf.Max(0.015f, radius);
            if (span.sqrMagnitude < 0.00000001f)
                return CreateSphere(bodyModel, id, parent, safeRadius);
            Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, span.normalized);
            Vector3 euler = localRotation.eulerAngles;
            return new MagicaCharacterClothAuthoringCollider
            {
                id = id,
                type = "Capsule",
                parentBonePath = RelativePath(parent, bodyModel),
                localPosition = ToAuthoringVector((localStart + localEnd) * 0.5f),
                localRotationEuler = ToRotationVector(euler),
                radius = safeRadius,
                endRadius = safeRadius,
                length = Mathf.Max(span.magnitude + safeRadius * 2f, safeRadius * 2f + 0.001f),
                direction = "Y",
            };
        }

        private static Transform FindNearestBaseParent(Transform root)
        {
            Transform current = root?.parent;
            while (current != null)
            {
                if (IsBaseBoneName(current.name)) return current;
                current = current.parent;
            }
            return null;
        }

        private static Transform FindBone(Transform bodyModel, string name)
        {
            Transform bone = MagicaCharacterClothAuthoringValidator.FindTransform(
                bodyModel,
                name,
                out bool ambiguous);
            return ambiguous ? null : bone;
        }

        private static float EstimateCharacterHeight(Transform bodyModel)
        {
            Transform[] transforms = bodyModel.GetComponentsInChildren<Transform>(true);
            if (transforms.Length == 0) return 1f;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
            {
                Vector3 position = bodyModel.InverseTransformPoint(transforms[transformIndex].position);
                min = Mathf.Min(min, position.y);
                max = Mathf.Max(max, position.y);
            }
            float height = max - min;
            return float.IsNaN(height) || float.IsInfinity(height) || height < 0.1f ? 1f : height;
        }

        private static string RelativePath(Transform transform, Transform bodyModel) =>
            AnimationUtility.CalculateTransformPath(transform, bodyModel).Replace('\\', '/');

        private static MagicaCharacterClothAuthoringVector ToAuthoringVector(Vector3 value) =>
            new MagicaCharacterClothAuthoringVector(
                MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.x),
                MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.y),
                MagicaCharacterClothAuthoringNormalization.NormalizePosition(value.z));

        private static MagicaCharacterClothAuthoringVector ToRotationVector(Vector3 value) =>
            new MagicaCharacterClothAuthoringVector(
                MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.x),
                MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.y),
                MagicaCharacterClothAuthoringNormalization.NormalizeRotation(value.z));

        private static void CollectRendererBones(
            SkinnedMeshRenderer renderer,
            HashSet<Transform> allSkinnedBones,
            HashSet<Transform> weightedBones)
        {
            if (renderer == null) return;
            Transform[] bones = renderer.bones ?? Array.Empty<Transform>();
            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                if (bones[boneIndex] != null) allSkinnedBones.Add(bones[boneIndex]);

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) return;
            BoneWeight[] weights = mesh.boneWeights;
            for (int vertexIndex = 0; vertexIndex < weights.Length; vertexIndex++)
            {
                BoneWeight weight = weights[vertexIndex];
                AddWeightedBone(bones, weight.boneIndex0, weight.weight0, weightedBones);
                AddWeightedBone(bones, weight.boneIndex1, weight.weight1, weightedBones);
                AddWeightedBone(bones, weight.boneIndex2, weight.weight2, weightedBones);
                AddWeightedBone(bones, weight.boneIndex3, weight.weight3, weightedBones);
            }
        }

        private static void AddWeightedBone(
            Transform[] bones,
            int boneIndex,
            float weight,
            HashSet<Transform> weightedBones)
        {
            if (weight <= WeightEpsilon || boneIndex < 0 || boneIndex >= bones.Length) return;
            Transform bone = bones[boneIndex];
            if (bone != null) weightedBones.Add(bone);
        }

        private static List<Transform> CollectCustomSubtree(Transform root, HashSet<Transform> customBones)
        {
            var result = new List<Transform>();
            var pending = new Queue<Transform>();
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();
                if (current == null || !customBones.Contains(current) || result.Contains(current)) continue;
                result.Add(current);
                var children = new List<Transform>();
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    Transform child = current.GetChild(childIndex);
                    if (customBones.Contains(child)) children.Add(child);
                }
                children.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                    pending.Enqueue(children[childIndex]);
            }
            return result;
        }

        private static bool IsBaseBoneName(string name)
        {
            if (BaseBodyBoneNames.Contains(name ?? string.Empty)) return true;
            for (int prefixIndex = 0; prefixIndex < FingerPrefixes.Length; prefixIndex++)
                if ((name ?? string.Empty).StartsWith(FingerPrefixes[prefixIndex] + "_", StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsBreastChain(List<Transform> chain)
        {
            for (int boneIndex = 0; boneIndex < chain.Count; boneIndex++)
                if (chain[boneIndex] != null
                    && chain[boneIndex].name.StartsWith("breast_", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool TryResolvePreset(string assetPath, out string hash, out string error)
        {
            hash = string.Empty;
            error = string.Empty;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!File.Exists(fullPath))
            {
                error = "模型数据初始化失败：缺少默认 Magica Cloth Preset：" + assetPath;
                return false;
            }
            hash = MagicaCharacterClothAuthoringRepository.ComputeFileHash(fullPath);
            return true;
        }

        private static string UniqueId(string seed, HashSet<string> existing)
        {
            string baseId = string.IsNullOrWhiteSpace(seed) ? "cloth" : seed;
            string value = baseId;
            int suffix = 2;
            while (!existing.Add(value)) value = baseId + "_" + suffix++;
            return value;
        }
    }
}
