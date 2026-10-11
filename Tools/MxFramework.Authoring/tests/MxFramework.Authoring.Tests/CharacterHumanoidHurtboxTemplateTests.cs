using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Tests;

internal static class CharacterHumanoidHurtboxTemplateTests
{
    internal static void RunAll()
    {
        CharacterHumanoidHurtboxRequest request = Fixture();
        string before = JsonSerializer.Serialize(request);
        CharacterHumanoidHurtboxResult result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(result.Success, string.Join(";", result.Issues));
        Check(result.AddedColliders.Count == 11 && result.AddedParts.Count == 11, "Empty geometry must generate exactly eleven parts/colliders.");
        Check(JsonSerializer.Serialize(request) == before, "Generation must not mutate the supplied draft.");
        Check(result.AddedParts.Select(p => p.DefaultHitZoneId).Distinct().Count() == 11, "New hit zones must resolve unambiguously.");
        Check(result.AddedColliders.All(c => c.Shape == CharacterColliderShape.Capsule && c.Height >= 2 * c.Radius
            && c.PhysicsLayer == "CharacterHurtbox" && c.DamageMultiplierOverride == 1 && !c.IsWeakPoint), "Neutral valid capsule defaults.");
        Check(!result.AddedParts.Any(p => p.PartId.StartsWith("hand_") || p.PartId.StartsWith("foot_")), "Hands and feet are endpoints, not independent shapes.");

        var projectedPackage = new CharacterResourcePackage();
        projectedPackage.Manifest.PackageId = "humanoid11_test";
        projectedPackage.Manifest.StableId = "character.humanoid11_test";
        projectedPackage.Geometry.BodyProfile.BodyKind = CharacterAuthoringBodyKind.Skeletal;
        projectedPackage.Geometry.BodyParts.AddRange(result.AddedParts);
        projectedPackage.Geometry.Colliders.AddRange(result.AddedColliders);
        var roundTrip = JsonSerializer.Deserialize<CharacterResourcePackage>(JsonSerializer.Serialize(projectedPackage));
        CharacterAuthoringCompileResult compiled = CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest { Package = roundTrip });
        Check(compiled.GeometryBinding.BodyColliders.Count == 11 && compiled.GeometryBinding.HitZoneBindings.Count == 11,
            "Existing compiler projection must retain all eleven saved geometry leaves and hit-zone mappings.");
        Check(compiled.GeometryBinding.HitZoneBindings.All(binding => binding.DamageMultiplierOverride == 1f
            && binding.ImpactPowerScaleOverride == 1f && !binding.IsWeakPoint), "Neutral template overrides must survive compiler projection.");

        // Exact body binding, dimensions and authored damage metadata survive repeated generation.
        request.Geometry.BodyParts.AddRange(result.AddedParts);
        request.Geometry.Colliders.AddRange(result.AddedColliders);
        request.Geometry.Colliders[0].Radius = 0.333f;
        request.Geometry.Colliders[0].DamageMultiplierOverride = 2.5f;
        before = JsonSerializer.Serialize(request);
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(result.Success && result.AddedColliders.Count == 0 && result.PreservedColliderCount == 11, "Repeated generation must be a no-op.");
        Check(JsonSerializer.Serialize(request) == before, "Manual edits were replaced.");
        request.Geometry.Colliders.RemoveAt(4);
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(result.Success && result.AddedColliders.Count == 1 && result.AddedParts.Count == 0, "Only a missing collider is restored.");

        request = Fixture();
        request.Bones.RemoveAll(b => b.Name == "LeftHand");
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(!result.Success && result.AddedColliders.Count == 0 && result.Roles.Count == 15, "Missing endpoints must reject atomically and return mapping choices.");
        request = Fixture();
        request.Bones.Add(new CharacterHurtboxReferenceBone { Name = "Head", Path = "Other/Head" });
        Check(!CharacterHumanoidHurtboxTemplate.Generate(request).Success, "Ambiguous bone names must not silently select one.");
        request.BoneMappings["head"] = "Rig/Head";
        Check(CharacterHumanoidHurtboxTemplate.Generate(request).Success, "Explicit mapping resolves ambiguous names.");
        request.BoneMappings["head"] = "missing";
        Check(!CharacterHumanoidHurtboxTemplate.Generate(request).Success, "Stale explicit mapping must not fall back to inference.");
        request = Fixture();
        foreach (CharacterHurtboxReferenceBone bone in request.Bones)
        {
            if (bone.Name == "Hips") bone.Name = "pelvisx";
            if (bone.Name == "Chest") bone.Name = "upper_chestx";
            if (bone.Name == "Head") bone.Name = "headx";
            if (bone.Name == "LeftLeg") bone.Name = "lower_legl";
            if (bone.Name == "RightLeg") bone.Name = "lower_legr";
        }
        request.Geometry.BodyParts.Add(new CharacterBodyPartAuthoring { PartId = "torso", BonePath = "bone.spine" });
        Check(CharacterHumanoidHurtboxTemplate.Generate(request).Success, "Project centre-axis names and legacy semantic aliases must resolve.");
        request = Fixture();
        for (int i = 0; i < 22; i++) request.Geometry.Colliders.Add(new CharacterBodyColliderProfile { ColliderId = "custom" + i, PartId = "custom" + i });
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(!result.Success && result.AddedColliders.Count == 0, "Over-budget additions must be rejected atomically.");
        request = Fixture();
        request.Geometry.SchemaVersion = "99.0";
        Check(!CharacterHumanoidHurtboxTemplate.Generate(request).Success, "Future schemas must remain read-only.");
        request = Fixture();
        request.Bones.First(b => b.Name == "LeftHand").Position = request.Bones.First(b => b.Name == "LeftForeArm").Position;
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(!result.Success && result.AddedParts.Count == 0 && result.AddedColliders.Count == 0, "Degenerate limbs cannot return a partial template.");

        // A rotated/scaled reference rig must yield the same local shapes, scaled in metres.
        CharacterHumanoidHurtboxResult baseline = CharacterHumanoidHurtboxTemplate.Generate(Fixture());
        request = Fixture();
        Quaternion q = Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.2f);
        foreach (CharacterHurtboxReferenceBone bone in request.Bones)
        {
            Vector3 p = Vector3.Transform(new Vector3(bone.Position.X, bone.Position.Y, bone.Position.Z) * 2f, q) + new Vector3(3, 4, 5);
            bone.Position = new CharacterAuthoringVector3(p.X, p.Y, p.Z);
            bone.Rotation = new CharacterAuthoringQuaternion(q.X, q.Y, q.Z, q.W);
        }
        result = CharacterHumanoidHurtboxTemplate.Generate(request);
        Check(result.Success, "Transformed rig must remain valid.");
        for (int i = 0; i < 11; i++)
        {
            CharacterBodyColliderProfile a = baseline.AddedColliders[i], b = result.AddedColliders[i];
            Check(Math.Abs(a.Radius * 2 - b.Radius) < 0.00001f && Math.Abs(a.Height * 2 - b.Height) < 0.00001f, "Dimensions must be invariant under rigid transform and scale linearly.");
            Check(Vector3.Distance(new Vector3(a.LocalPose.Position.X, a.LocalPose.Position.Y, a.LocalPose.Position.Z) * 2,
                new Vector3(b.LocalPose.Position.X, b.LocalPose.Position.Y, b.LocalPose.Position.Z)) < 0.00001f, "Bone-local centre conversion failed.");
            Quaternion qa = new Quaternion(a.LocalPose.Rotation.X, a.LocalPose.Rotation.Y, a.LocalPose.Rotation.Z, a.LocalPose.Rotation.W);
            Quaternion qb = new Quaternion(b.LocalPose.Rotation.X, b.LocalPose.Rotation.Y, b.LocalPose.Rotation.Z, b.LocalPose.Rotation.W);
            Check(Math.Abs(Quaternion.Dot(qa, qb)) > .99999f, "Bone-local capsule axis changed under a rigid rig transform.");
        }
    }

    internal static CharacterHumanoidHurtboxRequest Fixture()
    {
        var request = new CharacterHumanoidHurtboxRequest();
        request.Geometry.BodyProfile.BodyKind = CharacterAuthoringBodyKind.Skeletal;
        void Bone(string name, float x, float y, float z = 0) => request.Bones.Add(new CharacterHurtboxReferenceBone
        {
            Name = name, Path = "Rig/" + name, Position = new CharacterAuthoringVector3(x, y, z)
        });
        Bone("Hips", 0, 1); Bone("Chest", 0, 1.4f); Bone("Head", 0, 1.65f);
        foreach (bool left in new[] { true, false })
        {
            string side = left ? "Left" : "Right"; float sign = left ? 1 : -1;
            Bone(side + "Arm", sign * 0.2f, 1.45f); Bone(side + "ForeArm", sign * 0.5f, 1.45f);
            Bone(side + "Hand", sign * 0.75f, 1.45f); Bone(side + "UpLeg", sign * 0.12f, 0.95f);
            Bone(side + "Leg", sign * 0.12f, 0.5f); Bone(side + "Foot", sign * 0.12f, 0.08f);
        }
        return request;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
