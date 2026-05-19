using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;

namespace MxFramework.Authoring.Tests;

internal static class CharacterPackageTests
{
    public static void RunAll()
    {
        ValidationIssue_JsonRoundTrip_PreservesGateAndSourceFields();
        ResourceCatalogEntry_JsonRoundTrip_PreservesResourceFields();
        CoordinateConvention_JsonRoundTrip_PreservesUnityTargetConvention();
        Schemas_ExposeC0ContractFields();
        IronVanguardSample_ValidatesAsReady();
        UnsupportedConvexShape_ProducesExportBlockedIssue();
        SlimeSample_UsesSameDtoForPrimitiveBody();
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void ValidationIssue_JsonRoundTrip_PreservesGateAndSourceFields()
    {
        var issue = new CharacterAuthoringValidationIssue
        {
            Severity = CharacterAuthoringValidationSeverity.Error,
            Gate = CharacterAuthoringValidationGate.ExportBlocked,
            Code = "CHARPKG_UNSUPPORTED_COLLIDER_SHAPE",
            SourcePath = "geometry/body_colliders.json",
            SourceObjectPath = "geometry/colliders/head_01",
            Field = "shape",
            Message = "v1 only supports capsule, box and sphere.",
            SuggestedFix = "Replace convex collider with capsule, box or sphere."
        };

        string json = JsonSerializer.Serialize(issue, JsonOptions);
        CharacterAuthoringValidationIssue roundTrip = JsonSerializer.Deserialize<CharacterAuthoringValidationIssue>(json, JsonOptions);

        Require(roundTrip != null, "Validation issue should deserialize.");
        Require(roundTrip.Code == issue.Code, "code should roundtrip.");
        Require(roundTrip.Severity == issue.Severity, "severity should roundtrip.");
        Require(roundTrip.Gate == issue.Gate, "gate should roundtrip.");
        Require(roundTrip.SourcePath == issue.SourcePath, "sourcePath should roundtrip.");
        Require(roundTrip.Field == issue.Field, "field should roundtrip.");
        Require(roundTrip.SuggestedFix == issue.SuggestedFix, "suggestedFix should roundtrip.");
    }

    private static void ResourceCatalogEntry_JsonRoundTrip_PreservesResourceFields()
    {
        var catalog = new CharacterPackageResourceCatalog();
        catalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.model.body",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Variant = "default",
            PackageId = "test",
            RelativePath = "resources/models/test.glb",
            Hash = "sha256:abc",
            ImportHints = new CharacterPackageImportHint
            {
                TargetPathPolicy = "generatedCharacterPackage",
                TargetRelativePath = "resources/models/test.glb",
                Scale = 1f,
                ProviderId = "unityAsset"
            }
        });

        string json = JsonSerializer.Serialize(catalog, JsonOptions);
        CharacterPackageResourceCatalog roundTrip = JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.Entries.Count == 1, "catalog entry should roundtrip.");
        CharacterPackageResourceEntry entry = roundTrip.Entries[0];
        Require(entry.ResourceKey == "char.test.model.body", "resourceKey should roundtrip.");
        Require(entry.RelativePath == "resources/models/test.glb", "relativePath should roundtrip.");
        Require(entry.Hash == "sha256:abc", "hash should roundtrip.");
        Require(entry.ImportHints.ProviderId == "unityAsset", "import hint should roundtrip.");
    }

    private static void CoordinateConvention_JsonRoundTrip_PreservesUnityTargetConvention()
    {
        var manifest = new CharacterPackageManifest
        {
            PackageId = "coord",
            StableId = "charpkg.coord"
        };

        string json = JsonSerializer.Serialize(manifest, JsonOptions);
        CharacterPackageManifest roundTrip = JsonSerializer.Deserialize<CharacterPackageManifest>(json, JsonOptions);

        Require(roundTrip != null, "manifest should deserialize.");
        Require(roundTrip.CoordinateConvention.UpAxis == CharacterCoordinateAxis.YPositive, "Unity target up axis should be Y+.");
        Require(roundTrip.CoordinateConvention.ForwardAxis == CharacterCoordinateAxis.ZPositive, "Unity target forward axis should be Z+.");
        Require(roundTrip.CoordinateConvention.UnitScaleMeters == 1f, "unit scale should default to meters.");
        Require(roundTrip.CoordinateConvention.RotationStorage == CharacterRotationStorage.Quaternion, "quaternion should be authoritative.");
    }

    private static void Schemas_ExposeC0ContractFields()
    {
        IReadOnlyList<ConfigSchema> schemas = CharacterResourcePackageSchemas.CreateAll();
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.ManifestSchemaId) != null, "manifest schema missing.");
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId) != null, "collider schema missing.");
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId) != null, "validation issue schema missing.");

        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ManifestSchemaId), "coordinateConvention"), "manifest coordinate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "shape"), "collider shape field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "hitZoneId"), "collider hit zone field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceRadius"), "weapon trace radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId), "gate"), "validation gate field missing.");
        Require(HasEnumOption("character.validationGate", "Unknown"), "validation gate Unknown option missing.");
        Require(HasEnumOption("character.validationGate", "Reserved1000"), "validation gate reserved option missing.");
        Require(HasEnumOption("character.colliderShape", "Convex"), "reserved convex shape option missing.");
    }

    private static void IronVanguardSample_ValidatesAsReady()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(!report.HasBlockingIssues, "Iron Vanguard sample should not have blocking validation issues: " + report.ToText());
        Require(package.Geometry.BodyProfile.HeightMeters > 1.8f, "Iron Vanguard height should be practical data.");
        Require(package.Geometry.Colliders.Exists(c => c.ColliderId == "head_sphere" && c.Shape == CharacterColliderShape.Sphere), "head collider missing.");
        Require(package.Geometry.Sockets.Exists(s => s.SocketId == "mainHand"), "main hand socket missing.");
        Require(package.Geometry.WeaponAttachments.Exists(a => a.WeaponId == "weapon.iron_sword" && a.TraceRadius > 0f), "sword attachment trace data missing.");
    }

    private static void UnsupportedConvexShape_ProducesExportBlockedIssue()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.Geometry.Colliders[0].Shape = CharacterColliderShape.Convex;

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "convex shape should block v1 export/importable artifact.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.UnsupportedColliderShape &&
            issue.Gate == CharacterAuthoringValidationGate.ExportBlocked &&
            issue.Field == "shape"), "unsupported convex shape should produce stable ExportBlocked issue.");
    }

    private static void SlimeSample_UsesSameDtoForPrimitiveBody()
    {
        CharacterResourcePackage package = LoadSample("character-slime");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(!report.HasBlockingIssues, "Slime sample should validate with same DTO: " + report.ToText());
        Require(package.Geometry.BodyProfile.BodyKind == CharacterAuthoringBodyKind.Primitive, "slime should be primitive body kind.");
        Require(package.Geometry.BodyParts.Exists(part => part.PartId == "core" && part.PartKind == CharacterAuthoringBodyPartKind.Primitive), "slime core part missing.");
        Require(package.Geometry.Colliders.Exists(collider => collider.Shape == CharacterColliderShape.Sphere && collider.PartId == "core"), "slime core sphere collider missing.");
        Require(package.Geometry.Sockets.Exists(socket => socket.SocketId == "frontAttack" && socket.Usage == CharacterSocketUsage.Gameplay), "slime gameplay socket missing.");
    }

    private static CharacterResourcePackage LoadSample(string sampleName)
    {
        string root = FindRepoRoot();
        string packagePath = Path.Combine(root, "Tools", "MxFramework.Authoring", "samples", sampleName);
        return CharacterPackageCommands.ReadPackage(packagePath, MxFramework.Authoring.Cli.Program.CreateJsonOptions());
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception("ASSERTION FAILED: " + message);
    }
}
