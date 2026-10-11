using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
    private static CharacterAuthoringCompileResult Compile(
        CharacterResourcePackage package,
        bool checkFiles = false,
        bool checkHashes = false,
        CharacterPackageResourceCatalog projectCatalog = null)
    {
        string samplePath = FindSamplePath("character-codex-flow-test");
        string repoRoot = Path.GetFullPath(Path.Combine(samplePath, "..", "..", ".."));
        string catalogPath = Path.Combine(repoRoot, "Tools", "RuntimeHash", "character_definition_ids.json");
        var catalog = CharacterContentIdentityCatalogSnapshot.LoadFromFile(catalogPath)
            ?? new CharacterContentIdentityCatalogSnapshot();
        catalog.AllowAutoRegisterForTests = true;

        return CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = samplePath,
            IdentityCatalog = catalog,
            ExistingProjectResourceCatalogSummary = projectCatalog,
            Options = new CharacterAuthoringCompileOptions
            {
                ValidateResourceFiles = checkFiles || checkHashes,
                ValidateResourceHashes = checkHashes
            }
        });
    }

    private static PatchEntry FindPatchEntry(CharacterAuthoringCompileResult result, string source, string id)
    {
        List<PatchEntry> entries = result?.GeneratedConfigPatch?.Patch?.Entries;
        if (entries == null)
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            PatchEntry entry = entries[i];
            if (entry != null && entry.Source == source && entry.Id == id)
                return entry;
        }

        return null;
    }

    private static bool HasCompilerIssue(CharacterAuthoringCompileResult result, string code, CharacterAuthoringValidationGate gate)
    {
        for (int i = 0; i < result.GateReport.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = result.GateReport.Issues[i];
            if (issue.Code == code && issue.Gate == gate)
                return true;
        }

        return false;
    }

    private static bool HasValidationIssue(CharacterAuthoringValidationReport report, string code, CharacterAuthoringValidationGate gate)
    {
        for (int i = 0; i < report.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = report.Issues[i];
            if (issue.Code == code && issue.Gate == gate)
                return true;
        }

        return false;
    }

    private static CharacterAuthoringValidationIssue FindValidationIssue(CharacterAuthoringValidationReport report, string code, CharacterAuthoringValidationGate gate)
    {
        for (int i = 0; i < report.Issues.Count; i++)
        {
            CharacterAuthoringValidationIssue issue = report.Issues[i];
            if (issue.Code == code && issue.Gate == gate)
                return issue;
        }

        return null;
    }

    private static void AssertStarterBoneColliders(CharacterResourcePackage package, string label)
    {
        var expectedBoneAliases = new HashSet<string>(StringComparer.Ordinal)
        {
            "bone.hips",
            "bone.spine",
            "bone.head",
            "bone.upper_arm.L",
            "bone.forearm.L",
            "bone.hand.L",
            "bone.upper_arm.R",
            "bone.forearm.R",
            "bone.hand.R",
            "bone.thigh.L",
            "bone.shin.L",
            "bone.foot.L",
            "bone.thigh.R",
            "bone.shin.R",
            "bone.foot.R"
        };
        var legacyBoneAliases = new HashSet<string>(StringComparer.Ordinal)
        {
            "bone.Head",
            "bone.RightHand",
            "bone.LeftHand",
            "bone.RightLeg",
            "bone.LeftLeg",
            "bone.Chest",
            "bone.Spine",
            "bone.spine002"
        };

        Require(package.Geometry.Colliders.Count == 10, label + " should include torso/head/arms/legs colliders without foot boxes.");
        for (int i = 0; i < package.Geometry.BodyParts.Count; i++)
        {
            CharacterBodyPartAuthoring part = package.Geometry.BodyParts[i];
            Require(part.PartKind == CharacterAuthoringBodyPartKind.Bone, label + " body part should be bone-bound: " + part.PartId);
            Require(expectedBoneAliases.Contains(part.LocatorId), label + " body part locator should use starter bone alias: " + part.PartId + " -> " + part.LocatorId);
            Require(!legacyBoneAliases.Contains(part.LocatorId), label + " body part should not use legacy locator alias: " + part.LocatorId);
            if (part.PartId == "foot_l")
                Require(part.DefaultHitZoneId == "hit.left_leg", label + " left foot part should default to left leg hit zone.");
            if (part.PartId == "foot_r")
                Require(part.DefaultHitZoneId == "hit.right_leg", label + " right foot part should default to right leg hit zone.");
            Require(part.DefaultHitZoneId != "hit.left_foot" && part.DefaultHitZoneId != "hit.right_foot", label + " should not expose default foot hit zones: " + part.PartId);
        }

        for (int i = 0; i < package.Geometry.Colliders.Count; i++)
        {
            CharacterBodyColliderProfile collider = package.Geometry.Colliders[i];
            Require(collider.ColliderId != "foot_l_box" && collider.ColliderId != "foot_r_box", label + " should not include standalone foot colliders.");
            Require(collider.HitZoneId != "hit.left_foot" && collider.HitZoneId != "hit.right_foot", label + " collider should not use standalone foot hit zones: " + collider.ColliderId);
            Require(collider.LocalPose.ParentKind == CharacterPoseParentKind.Bone, label + " collider should be parented to a bone: " + collider.ColliderId);
            Require(expectedBoneAliases.Contains(collider.LocalPose.ParentPath), label + " collider parentPath should match starter bone aliases: " + collider.ColliderId + " -> " + collider.LocalPose.ParentPath);
            Require(!legacyBoneAliases.Contains(collider.LocalPose.ParentPath), label + " collider should not use legacy parentPath: " + collider.LocalPose.ParentPath);
            Require(Math.Abs(collider.LocalPose.Position.Y) < 0.75f, label + " collider should use bone-local offsets, not world-space placement: " + collider.ColliderId);
            Require(!string.IsNullOrWhiteSpace(collider.PhysicsLayer), label + " collider should declare physics layer: " + collider.ColliderId);
            Require(!string.IsNullOrWhiteSpace(collider.MaterialStableId), label + " collider should declare physics material: " + collider.ColliderId);
        }
    }

    private static void AssertCodexFlowTestSourceAssets(CharacterResourcePackage package, string samplePath)
    {
        Require(package != null, "codex flow test package should load.");
        RequireCatalogFile(package, samplePath, "char.codex_flow_test.model.body", "resources/models/character_00.fbx");
        RequireCatalogFile(package, samplePath, "char.codex_flow_test.texture.body_albedo", "resources/textures/body_albedo.png");
        RequireCatalogFile(package, samplePath, "char.codex_flow_test.preview.body_mesh", "previews/character_00.preview.glb");
    }

    private static void RequireCatalogFile(
        CharacterResourcePackage package,
        string samplePath,
        string resourceKey,
        string relativePath)
    {
        CharacterPackageResourceEntry entry = package.ResourceCatalog.Entries.Find(item => item.ResourceKey == resourceKey);
        Require(entry != null, "starter catalog should contain resource key: " + resourceKey);
        Require(entry.RelativePath == relativePath, "starter catalog resource path should match current source asset: " + resourceKey);
        Require(File.Exists(Path.Combine(samplePath, relativePath.Replace('/', Path.DirectorySeparatorChar))), "starter source asset should exist: " + relativePath);
    }

    private static void AssertHumanoidStarterTPoseArms(JsonElement root)
    {
        Require(TryGetGlbNamedArrayObject(root, "nodes", "upper_arm.L", out JsonElement upperArmL), "starter body.glb should contain upper_arm.L node.");
        Require(TryGetGlbNamedArrayObject(root, "nodes", "upper_arm.R", out JsonElement upperArmR), "starter body.glb should contain upper_arm.R node.");
        Require(TryGetGlbNamedArrayObject(root, "nodes", "forearm.L", out JsonElement forearmL), "starter body.glb should contain forearm.L node.");
        Require(TryGetGlbNamedArrayObject(root, "nodes", "forearm.R", out JsonElement forearmR), "starter body.glb should contain forearm.R node.");

        Require(GetArrayNumber(upperArmL, "translation", 0) < -0.18, "starter upper_arm.L should start on the left shoulder for T Pose.");
        Require(GetArrayNumber(upperArmR, "translation", 0) > 0.18, "starter upper_arm.R should start on the right shoulder for T Pose.");
        Require(GetArrayNumber(forearmL, "translation", 1) > 0.25, "starter forearm.L should extend along the left upper-arm local axis for T Pose.");
        Require(GetArrayNumber(forearmR, "translation", 1) > 0.25, "starter forearm.R should extend along the right upper-arm local axis for T Pose.");

        double leftX = GetArrayNumber(upperArmL, "rotation", 0);
        double leftY = GetArrayNumber(upperArmL, "rotation", 1);
        double leftZ = GetArrayNumber(upperArmL, "rotation", 2);
        double leftW = GetArrayNumber(upperArmL, "rotation", 3);
        Require(leftX < -0.45 && leftY > 0.45 && leftZ > 0.45 && leftW > 0.45, "starter upper_arm.L bind rotation should point laterally for T Pose.");

        double rightX = GetArrayNumber(upperArmR, "rotation", 0);
        double rightY = GetArrayNumber(upperArmR, "rotation", 1);
        double rightZ = GetArrayNumber(upperArmR, "rotation", 2);
        double rightW = GetArrayNumber(upperArmR, "rotation", 3);
        Require(rightX < -0.45 && rightY < -0.45 && rightZ < -0.45 && rightW > 0.45, "starter upper_arm.R bind rotation should point laterally for T Pose.");
    }

    private static JsonDocument ReadGlbJsonDocument(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        string magic = new string(reader.ReadChars(4));
        Require(magic == "glTF", "GLB magic should be glTF for " + path);
        uint version = reader.ReadUInt32();
        Require(version == 2, "GLB version should be 2 for " + path);
        reader.ReadUInt32();
        int jsonLength = checked((int)reader.ReadUInt32());
        string chunkType = new string(reader.ReadChars(4));
        Require(chunkType == "JSON", "GLB first chunk should be JSON for " + path);
        byte[] json = reader.ReadBytes(jsonLength);
        return JsonDocument.Parse(json);
    }

    private static HashSet<string> GetGlbNodeNames(JsonElement root)
    {
        return GetGlbNamedArrayValues(root, "nodes");
    }

    private static HashSet<string> GetGlbNamedArrayValues(JsonElement root, string propertyName)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return names;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                names.Add(name.GetString() ?? string.Empty);
        }

        return names;
    }

    private static bool TryGetGlbNamedArrayObject(JsonElement root, string propertyName, string objectName, out JsonElement result)
    {
        result = default;
        if (!root.TryGetProperty(propertyName, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String && string.Equals(name.GetString(), objectName, StringComparison.Ordinal))
            {
                result = value;
                return true;
            }
        }

        return false;
    }

    private static double GetArrayNumber(JsonElement element, string propertyName, int index)
    {
        Require(element.TryGetProperty(propertyName, out JsonElement values) && values.ValueKind == JsonValueKind.Array, "GLB node should contain array property: " + propertyName);
        Require(index >= 0 && index < values.GetArrayLength(), "GLB node array property should contain index: " + propertyName + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
        return values[index].GetDouble();
    }

    private static bool HasArray(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0;
    }

    private static bool HasSkinnedColorAttribute(JsonElement root)
    {
        if (!root.TryGetProperty("meshes", out JsonElement meshes) || meshes.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement mesh in meshes.EnumerateArray())
        {
            if (!mesh.TryGetProperty("primitives", out JsonElement primitives) || primitives.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement primitive in primitives.EnumerateArray())
            {
                if (!primitive.TryGetProperty("attributes", out JsonElement attributes) || attributes.ValueKind != JsonValueKind.Object)
                    continue;

                if (attributes.TryGetProperty("COLOR_0", out _)
                    && attributes.TryGetProperty("JOINTS_0", out _)
                    && attributes.TryGetProperty("WEIGHTS_0", out _))
                    return true;
            }
        }

        return false;
    }

    private static int GetFirstSkinJointCount(JsonElement root)
    {
        if (!root.TryGetProperty("skins", out JsonElement skins) || skins.ValueKind != JsonValueKind.Array || skins.GetArrayLength() == 0)
            return 0;
        JsonElement first = skins[0];
        if (!first.TryGetProperty("joints", out JsonElement joints) || joints.ValueKind != JsonValueKind.Array)
            return 0;
        return joints.GetArrayLength();
    }

    private static bool HasEmbeddedImage(JsonElement root)
    {
        if (!root.TryGetProperty("images", out JsonElement images) || images.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement image in images.EnumerateArray())
        {
            if (image.TryGetProperty("bufferView", out _))
                return true;
        }

        return false;
    }

    private static bool HasMaterialBaseColorTexture(JsonElement root)
    {
        if (!root.TryGetProperty("materials", out JsonElement materials) || materials.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement material in materials.EnumerateArray())
        {
            if (material.TryGetProperty("pbrMetallicRoughness", out JsonElement pbr)
                && pbr.TryGetProperty("baseColorTexture", out JsonElement texture)
                && texture.TryGetProperty("index", out _))
                return true;
        }

        return false;
    }

    private static CharacterResourcePackage LoadSample(string sampleName)
    {
        return CharacterPackageCommands.ReadPackage(FindSamplePath(sampleName), MxFramework.Authoring.Cli.Program.CreateJsonOptions());
    }

    private static string FindSamplePath(string sampleName)
    {
        string root = FindRepoRoot();
        return Path.Combine(root, "Tools", "MxFramework.Authoring", "samples", sampleName);
    }

    private static string FindRepoRoot()
    {
        string current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            string candidate = Path.Combine(current, "Tools", "MxFramework.Authoring", "MxFramework.Authoring.sln");
            if (File.Exists(candidate))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate WGameFramework repo root from " + Directory.GetCurrentDirectory());
    }

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), overwrite: true);
    }

    private static ConfigSchema FindSchema(IReadOnlyList<ConfigSchema> schemas, string schemaId)
    {
        for (int i = 0; i < schemas.Count; i++)
        {
            if (schemas[i].SchemaId == schemaId)
                return schemas[i];
        }

        return null;
    }

    private static bool HasField(ConfigSchema schema, string fieldName)
    {
        if (schema == null)
            return false;
        for (int i = 0; i < schema.Fields.Count; i++)
        {
            if (schema.Fields[i].Name == fieldName)
                return true;
        }

        return false;
    }

    private static bool HasEnumOption(string enumId, string optionName)
    {
        IReadOnlyList<EnumDomain> enums = CharacterResourcePackageSchemas.CreateEnumDomains();
        for (int i = 0; i < enums.Count; i++)
        {
            if (enums[i].EnumId != enumId)
                continue;
            for (int j = 0; j < enums[i].Options.Count; j++)
            {
                if (enums[i].Options[j].Name == optionName)
                    return true;
            }
        }

        return false;
    }

    private static AuthoringResourceItem CreateGlobalProfileResource(string stableId, string kind, string usage, string runtimeKey, string slotId, string skeletonStableId)
    {
        return new AuthoringResourceItem
        {
            ResourceId = "resourceMapping:" + runtimeKey,
            StableId = stableId,
            DisplayName = stableId,
            Kind = kind,
            Usage = usage,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.GeneratedAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
            Metadata = new Dictionary<string, string>
            {
                { "packageId", "common" }
            },
            Compatibility = new AuthoringResourceCompatibility
            {
                SlotId = slotId,
                SkeletonStableId = skeletonStableId
            },
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    IsPrimary = true,
                    RuntimeResourceKey = runtimeKey,
                    ProviderResourceKey = runtimeKey,
                    AssetType = "UnityEngine.GameObject",
                    Hash = "sha256:" + stableId
                }
            }
        };
    }

    private static bool HasResourceSelection(AuthoringResourceSelectionRef selection)
    {
        return selection != null &&
            (!string.IsNullOrWhiteSpace(selection.ResourceStableId) ||
             !string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.ProviderResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.PackageResourceKey) ||
             !string.IsNullOrWhiteSpace(selection.UnityGuid) ||
             !string.IsNullOrWhiteSpace(selection.UnityAssetPath));
    }

    private static bool ResourceKeysContain(PatchEntry entry, string resourceKey)
    {
        if (entry == null || entry.Fields == null || !entry.Fields.TryGetValue("ResourceKeys", out FieldValue field) || field == null || field.List == null)
            return false;

        for (int i = 0; i < field.List.Count; i++)
        {
            FieldValue item = field.List[i];
            if (item != null && item.Map != null && item.Map.GetScalar("Id") == resourceKey)
                return true;
        }

        return false;
    }

    private static string GetAnonymousPropertyForTest(object value, string propertyName)
    {
        return value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private static string FormatResourcePlanDiagnostics(List<CharacterResourcePlanDiagnostic> diagnostics)
    {
        if (diagnostics == null || diagnostics.Count == 0)
            return string.Empty;

        var parts = new List<string>();
        for (int i = 0; i < diagnostics.Count; i++)
        {
            CharacterResourcePlanDiagnostic diagnostic = diagnostics[i];
            if (diagnostic == null)
                continue;

            parts.Add(diagnostic.Severity + " " + diagnostic.Code + " " + diagnostic.ResourceKey + " " + diagnostic.Message);
        }

        return string.Join(" | ", parts);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception("ASSERTION FAILED: " + message);
    }
}
