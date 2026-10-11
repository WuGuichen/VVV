using System;
using System.Collections.Generic;
using System.Numerics;

namespace MxFramework.Authoring
{
    /// <summary>A rigid bone frame in authoring ModelRoot space, measured in metres in the reference pose.</summary>
    public sealed class CharacterHurtboxReferenceBone
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CharacterAuthoringVector3 Position { get; set; } = new CharacterAuthoringVector3();
        public CharacterAuthoringQuaternion Rotation { get; set; } = new CharacterAuthoringQuaternion();
    }

    public sealed class CharacterHumanoidHurtboxRequest
    {
        public CharacterAuthoringGeometry Geometry { get; set; } = new CharacterAuthoringGeometry();
        public List<CharacterHurtboxReferenceBone> Bones { get; set; } = new List<CharacterHurtboxReferenceBone>();
        public Dictionary<string, string> BoneMappings { get; set; } = new Dictionary<string, string>();
    }

    public sealed class CharacterHumanoidHurtboxRole
    {
        public string Role { get; set; }
        public string Label { get; set; }
        public string BonePath { get; set; }
    }

    public sealed class CharacterHumanoidHurtboxResult
    {
        public bool Success => Issues.Count == 0;
        public string TemplateId { get; set; } = "humanoid11.v1";
        public List<string> Issues { get; } = new List<string>();
        public List<CharacterHumanoidHurtboxRole> Roles { get; } = new List<CharacterHumanoidHurtboxRole>();
        public List<CharacterBodyPartAuthoring> AddedParts { get; } = new List<CharacterBodyPartAuthoring>();
        public List<CharacterBodyColliderProfile> AddedColliders { get; } = new List<CharacterBodyColliderProfile>();
        public int PreservedColliderCount { get; set; }
    }

    /// <summary>Creates an additive eleven-volume humanoid draft. Never writes or modifies the supplied geometry.</summary>
    /// <remarks>LowFreqAlloc. Bone-name inference is exact and ambiguity fails closed; callers may supply explicit mappings.</remarks>
    public static class CharacterHumanoidHurtboxTemplate
    {
        private sealed class RoleDefinition
        {
            internal RoleDefinition(string id, string label, params string[] names) { Id = id; Label = label; Names = names; }
            internal string Id;
            internal string Label;
            internal string[] Names;
        }

        private static readonly RoleDefinition[] Definitions =
        {
            new RoleDefinition("pelvis", "骨盆", "hips", "pelvis", "hip", "pelvisx", "hipsx"),
            new RoleDefinition("torso", "胸部", "chest", "upperchest", "upperchestx", "spine2", "spine02", "spine02x", "spine003", "spine1", "spine01", "spine01x", "spine"),
            new RoleDefinition("head", "头部", "head", "headx"),
            new RoleDefinition("upper_arm_l", "左上臂", "leftupperarm", "leftarm", "upperarml", "lupperarm"),
            new RoleDefinition("forearm_l", "左前臂", "leftlowerarm", "leftforearm", "forearml", "lforearm", "lowerarml"),
            new RoleDefinition("hand_l", "左手端点", "lefthand", "handl", "lhand"),
            new RoleDefinition("upper_arm_r", "右上臂", "rightupperarm", "rightarm", "upperarmr", "rupperarm"),
            new RoleDefinition("forearm_r", "右前臂", "rightlowerarm", "rightforearm", "forearmr", "rforearm", "lowerarmr"),
            new RoleDefinition("hand_r", "右手端点", "righthand", "handr", "rhand"),
            new RoleDefinition("thigh_l", "左大腿", "leftupperleg", "leftupleg", "leftthigh", "thighl", "lthigh"),
            new RoleDefinition("shin_l", "左小腿", "leftlowerleg", "leftleg", "leftcalf", "leftshin", "shinl", "calfl", "lcalf", "lowerlegl"),
            new RoleDefinition("foot_l", "左脚端点", "leftfoot", "footl", "lfoot"),
            new RoleDefinition("thigh_r", "右大腿", "rightupperleg", "rightupleg", "rightthigh", "thighr", "rthigh"),
            new RoleDefinition("shin_r", "右小腿", "rightlowerleg", "rightleg", "rightcalf", "rightshin", "shinr", "calfr", "rcalf", "lowerlegr"),
            new RoleDefinition("foot_r", "右脚端点", "rightfoot", "footr", "rfoot")
        };

        public static CharacterHumanoidHurtboxResult Generate(CharacterHumanoidHurtboxRequest request)
        {
            var result = new CharacterHumanoidHurtboxResult();
            CharacterAuthoringGeometry geometry = request?.Geometry;
            if (geometry == null || geometry.SchemaVersion != "1.0")
            {
                result.Issues.Add("仅支持 geometry schema 1.0；没有修改原始数据。");
                return result;
            }
            if (geometry.BodyProfile?.BodyKind == CharacterAuthoringBodyKind.Primitive)
            {
                result.Issues.Add("11 体模板需要带人形骨骼的角色模型。");
                return result;
            }
            var bones = new Dictionary<string, CharacterHurtboxReferenceBone>(StringComparer.Ordinal);
            foreach (CharacterHurtboxReferenceBone bone in request.Bones ?? new List<CharacterHurtboxReferenceBone>())
            {
                if (bone == null || string.IsNullOrWhiteSpace(bone.Path) || bones.ContainsKey(bone.Path)
                    || !Finite(V(bone.Position)) || !ValidRotation(bone.Rotation))
                {
                    result.Issues.Add("参考骨骼含重复路径、无效坐标或旋转。");
                    return result;
                }
                bones.Add(bone.Path, bone);
            }
            var resolved = new Dictionary<string, CharacterHurtboxReferenceBone>(StringComparer.Ordinal);
            var usedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (RoleDefinition definition in Definitions)
            {
                CharacterHurtboxReferenceBone selected = ResolveBone(definition, request, bones);
                result.Roles.Add(new CharacterHumanoidHurtboxRole { Role = definition.Id, Label = definition.Label, BonePath = selected?.Path ?? string.Empty });
                if (selected == null) result.Issues.Add("请选择“" + definition.Label + "”的参考骨骼（缺失或名称不唯一）。");
                else if (!usedPaths.Add(selected.Path)) result.Issues.Add("多个部位重复绑定骨骼：" + selected.Path);
                else resolved.Add(definition.Id, selected);
            }
            if (!result.Success) return result;

            Vector3 hips = P(resolved, "pelvis"), chest = P(resolved, "torso"), head = P(resolved, "head");
            Vector3 feet = (P(resolved, "foot_l") + P(resolved, "foot_r")) * 0.5f;
            float height = Vector3.Distance(head, feet) * 1.12f;
            if (!Finite(height) || height < 0.1f || height > 100f || Vector3.Distance(head, hips) < height * 0.1f
                || Vector3.Distance(chest, hips) < height * 0.025f || Vector3.Distance(chest, head) < height * 0.025f)
            {
                result.Issues.Add("参考姿态尺寸不合理，请检查模型单位和胸部／骨盆／头部映射。");
                return result;
            }
            Vector3 up = Vector3.Normalize(head - hips);
            Vector3 waist = Vector3.Lerp(hips, chest, 0.55f);
            float shoulders = Vector3.Distance(P(resolved, "upper_arm_l"), P(resolved, "upper_arm_r"));
            Add(result, geometry, resolved["pelvis"], "pelvis", "腰腹／骨盆", "", hips - up * height * 0.025f,
                waist, Clamp(shoulders * 0.34f, height * 0.06f, height * 0.11f));
            Add(result, geometry, resolved["torso"], "torso", "胸部", "pelvis", waist,
                Vector3.Lerp(chest, head, 0.45f), Clamp(shoulders * 0.36f, height * 0.07f, height * 0.12f));
            Add(result, geometry, resolved["head"], "head", "头部", "torso", head,
                head + up * height * 0.07f, height * 0.055f);
            foreach (string side in new[] { "l", "r" })
            {
                string label = side == "l" ? "左" : "右";
                AddLimb(result, geometry, resolved, "upper_arm_" + side, "forearm_" + side, label + "上臂", "torso", 0.19f, 0f, height);
                AddLimb(result, geometry, resolved, "forearm_" + side, "hand_" + side, label + "前臂（含手）", "upper_arm_" + side, 0.17f, 0.30f, height);
                AddLimb(result, geometry, resolved, "thigh_" + side, "shin_" + side, label + "大腿", "pelvis", 0.23f, 0f, height);
                AddLimb(result, geometry, resolved, "shin_" + side, "foot_" + side, label + "小腿（含脚）", "thigh_" + side, 0.18f, 0.12f, height);
            }
            if ((geometry.Colliders?.Count ?? 0) + result.AddedColliders.Count > 32)
                result.Issues.Add("补齐后超过每角色 32 个受击体上限；请先整理已有受击体。");
            if (!result.Success) { result.AddedParts.Clear(); result.AddedColliders.Clear(); }
            return result;
        }

        private static CharacterHurtboxReferenceBone ResolveBone(RoleDefinition role, CharacterHumanoidHurtboxRequest request,
            Dictionary<string, CharacterHurtboxReferenceBone> bones)
        {
            if (request.BoneMappings != null && request.BoneMappings.TryGetValue(role.Id, out string explicitPath))
                return bones.TryGetValue(explicitPath ?? string.Empty, out CharacterHurtboxReferenceBone explicitBone) ? explicitBone : null;
            // An authored part binding takes precedence over inferred names.
            foreach (CharacterBodyPartAuthoring part in request.Geometry.BodyParts ?? new List<CharacterBodyPartAuthoring>())
                if (part != null && part.PartId == role.Id && !string.IsNullOrWhiteSpace(part.BonePath))
                {
                    CharacterHurtboxReferenceBone authored = FindPath(bones, part.BonePath);
                    if (authored != null) return authored;
                    // Legacy bone.<semantic-name> is an alias, not a concrete hierarchy path.
                    if (!part.BonePath.StartsWith("bone.", StringComparison.Ordinal)
                        || Array.IndexOf(role.Names, Normalize(part.BonePath.Substring(5))) < 0) return null;
                    break;
                }
            foreach (string name in role.Names)
            {
                CharacterHurtboxReferenceBone found = null;
                foreach (CharacterHurtboxReferenceBone bone in bones.Values)
                    if (Normalize(bone.Name) == name)
                    {
                        if (found != null) return null;
                        found = bone;
                    }
                if (found != null) return found;
            }
            return null;
        }

        private static CharacterHurtboxReferenceBone FindPath(Dictionary<string, CharacterHurtboxReferenceBone> bones, string path)
        {
            if (bones.TryGetValue(path, out CharacterHurtboxReferenceBone exact)) return exact;
            string name = path.StartsWith("bone.", StringComparison.Ordinal) ? path.Substring(5) : path;
            CharacterHurtboxReferenceBone found = null;
            foreach (CharacterHurtboxReferenceBone bone in bones.Values)
                if (bone.Name == name) { if (found != null) return null; found = bone; }
            return found;
        }

        private static string Normalize(string name)
        {
            name = name ?? string.Empty;
            int colon = name.LastIndexOf(':');
            if (colon >= 0) name = name.Substring(colon + 1);
            if (name.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase)) name = name.Substring(9);
            return name.Replace("_", "").Replace(".", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
        }

        private static void AddLimb(CharacterHumanoidHurtboxResult result, CharacterAuthoringGeometry geometry,
            Dictionary<string, CharacterHurtboxReferenceBone> bones, string role, string endRole, string label,
            string parent, float radiusRatio, float extension, float height)
        {
            Vector3 a = P(bones, role), delta = P(bones, endRole) - a;
            if (delta.Length() < height * 0.025f)
            {
                result.Issues.Add(label + "长度过短，请检查端点骨骼。");
                return;
            }
            Add(result, geometry, bones[role], role, label, parent, a, a + delta * (1f + extension),
                Clamp(delta.Length() * radiusRatio, height * 0.018f, height * 0.065f));
        }

        private static void Add(CharacterHumanoidHurtboxResult result, CharacterAuthoringGeometry geometry,
            CharacterHurtboxReferenceBone bone, string id, string label, string parent, Vector3 a, Vector3 b, float radius)
        {
            string colliderId = "humanoid11." + id;
            CharacterBodyPartAuthoring existingPart = null;
            foreach (CharacterBodyPartAuthoring part in geometry.BodyParts ?? new List<CharacterBodyPartAuthoring>())
                if (part?.PartId == id) { existingPart = part; break; }
            foreach (CharacterBodyColliderProfile collider in geometry.Colliders ?? new List<CharacterBodyColliderProfile>())
            {
                if (collider == null) continue;
                if (collider.ColliderId == colliderId && collider.PartId != id)
                {
                    result.Issues.Add("模板 ID 已被其它部位占用：" + colliderId);
                    return;
                }
                if (collider.PartId == id)
                {
                    result.PreservedColliderCount++;
                    return;
                }
            }
            string hitZone = existingPart?.DefaultHitZoneId;
            if (string.IsNullOrWhiteSpace(hitZone)) hitZone = "hit.humanoid11." + id;
            if (existingPart == null)
                result.AddedParts.Add(new CharacterBodyPartAuthoring
                {
                    PartId = id, DisplayName = label, PartKind = CharacterAuthoringBodyPartKind.Bone,
                    ParentPartId = parent, BonePath = bone.Path, DefaultHitZoneId = hitZone
                });
            Quaternion parentRotation = Q(bone.Rotation);
            Quaternion inverse = Quaternion.Inverse(parentRotation);
            Vector3 localDirection = Vector3.Normalize(Vector3.Transform(b - a, inverse));
            Quaternion rotation = localDirection.Y < -0.999999f ? Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)Math.PI)
                : Quaternion.Normalize(new Quaternion(Vector3.Cross(Vector3.UnitY, localDirection), 1f + localDirection.Y));
            float capsuleHeight = Vector3.Distance(a, b) + 2f * radius;
            result.AddedColliders.Add(new CharacterBodyColliderProfile
            {
                ColliderId = colliderId, PartId = id, HitZoneId = hitZone, Shape = CharacterColliderShape.Capsule,
                Radius = radius, Height = capsuleHeight, Size = D(new Vector3(2f * radius, capsuleHeight, 2f * radius)),
                DamageMultiplierOverride = 1f, ImpactPowerScaleOverride = 1f, PhysicsLayer = "CharacterHurtbox",
                LocalPose = new CharacterAuthoringLocalPose
                {
                    ParentKind = CharacterPoseParentKind.Bone, ParentPath = bone.Path,
                    Position = D(Vector3.Transform((a + b) * 0.5f - V(bone.Position), inverse)),
                    Rotation = new CharacterAuthoringQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W)
                }
            });
        }

        private static Vector3 P(Dictionary<string, CharacterHurtboxReferenceBone> bones, string id) => V(bones[id].Position);
        private static Vector3 V(CharacterAuthoringVector3 value) => value == null ? new Vector3(float.NaN) : new Vector3(value.X, value.Y, value.Z);
        private static CharacterAuthoringVector3 D(Vector3 value) => new CharacterAuthoringVector3(value.X, value.Y, value.Z);
        private static Quaternion Q(CharacterAuthoringQuaternion value) => Quaternion.Normalize(new Quaternion(value.X, value.Y, value.Z, value.W));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.X) && Finite(value.Y) && Finite(value.Z);
        private static bool ValidRotation(CharacterAuthoringQuaternion value) => value != null && Finite(value.X) && Finite(value.Y)
            && Finite(value.Z) && Finite(value.W) && new Quaternion(value.X, value.Y, value.Z, value.W).LengthSquared() > 0.000001f;
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
