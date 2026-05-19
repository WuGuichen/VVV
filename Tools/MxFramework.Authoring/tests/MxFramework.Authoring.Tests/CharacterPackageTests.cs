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
        ResourceKeyGenerator_GeneratesStablePackageLocalKey();
        CoordinateConvention_JsonRoundTrip_PreservesUnityTargetConvention();
        Schemas_ExposeC0ContractFields();
        IronVanguardSample_ValidatesAsReady();
        IronVanguardSample_ResourceFilesAndHashesValidate();
        ResourceDependencyGraph_MissingDependencyBlocksImport();
        ResourceDependencyGraph_DuplicateDependencyProducesDiagnostic();
        MissingResourceFile_BlocksImport();
        ResourceHashMismatch_BlocksImport();
        InvalidImportTargetPath_BlocksImport();
        UnsupportedConvexShape_ProducesExportBlockedIssue();
        SlimeSample_UsesSameDtoForPrimitiveBody();
        IronVanguardSample_CompilesToConfigPatchGeometryMappingAndWritePlan();
        Compiler_ApplicationResourceKeysAreCharacterReferences();
        Compiler_ModelWrapperPoseChangesImportAndResourceMappingHash();
        Compiler_UnsupportedConvexShape_BlocksExport();
        Compiler_MissingSocket_BlocksSpawnOnly();
        Compiler_MissingResource_BlocksImport();
        Compiler_CoordinateMismatch_ReportsWarningOnly();
        Compiler_HashMismatch_BlocksImport();
        Compiler_ResourceKeyConflict_BlocksImport();
        UnityImportBridge_ImportsIronVanguardAndSkipsRepeat();
        UnityImportBridge_SpawnBlockedWritesButMarksNotSpawnable();
        UnityImportBridge_ImportBlockedDoesNotWriteProjectTarget();
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
            LocalId = "model.body",
            StableId = "charpkg.test.resource.model.body",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Variant = "default",
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceFormat = CharacterPackageResourceFormatIds.Glb,
            PackageId = "test",
            RelativePath = "resources/models/test.glb",
            Hash = "sha256:abc",
            Hashes = new CharacterPackageResourceHashes
            {
                ContentHash = "sha256:abc",
                ImportHash = "sha256:def",
                DependencyHash = "sha256:123"
            },
            ImportHints = new CharacterPackageImportHint
            {
                TargetPathPolicy = "generatedCharacterPackage",
                TargetRelativePath = "resources/models/test.glb",
                Scale = 1f,
                ModelWrapperPose = new CharacterAuthoringLocalPose
                {
                    Position = new CharacterAuthoringVector3(0.1f, 0.2f, 0.3f),
                    Scale = new CharacterAuthoringVector3(2f, 2f, 2f),
                    EulerHint = new CharacterAuthoringVector3(0f, 90f, 0f)
                },
                ProviderId = "unityAsset",
                UpAxis = "Y+",
                ForwardAxis = "Z+",
                CollisionPolicy = "authoringGeometryOnly",
                PhysicsDataPolicy = "separateGeometryBinding"
            },
            Preview = new CharacterPackagePreviewMetadata
            {
                ThumbnailResourceKey = "char.test.preview.thumbnail",
                PreviewMeshResourceKey = "char.test.model.body",
                IsPlaceholder = true
            },
            Provenance = new CharacterPackageResourceProvenance
            {
                SourceTool = "unit-test",
                SourceFile = "resources/models/test.glb",
                AuthoringSchemaVersion = "1.0"
            }
        });

        string json = JsonSerializer.Serialize(catalog, JsonOptions);
        CharacterPackageResourceCatalog roundTrip = JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.Entries.Count == 1, "catalog entry should roundtrip.");
        CharacterPackageResourceEntry entry = roundTrip.Entries[0];
        Require(entry.ResourceKey == "char.test.model.body", "resourceKey should roundtrip.");
        Require(entry.LocalId == "model.body", "localId should roundtrip.");
        Require(entry.StableId == "charpkg.test.resource.model.body", "stableId should roundtrip.");
        Require(entry.Usage == CharacterPackageResourceUsageIds.CharacterModel, "usage should roundtrip.");
        Require(entry.SourceFormat == CharacterPackageResourceFormatIds.Glb, "sourceFormat should roundtrip.");
        Require(entry.RelativePath == "resources/models/test.glb", "relativePath should roundtrip.");
        Require(entry.Hash == "sha256:abc", "hash should roundtrip.");
        Require(entry.Hashes.ImportHash == "sha256:def", "import hash should roundtrip.");
        Require(entry.ImportHints.ProviderId == "unityAsset", "import hint should roundtrip.");
        Require(entry.ImportHints.ModelWrapperPose.Position.X == 0.1f, "model wrapper position should roundtrip.");
        Require(entry.ImportHints.ModelWrapperPose.Scale.X == 2f, "model wrapper scale should roundtrip.");
        Require(entry.ImportHints.ModelWrapperPose.EulerHint.Y == 90f, "model wrapper euler hint should roundtrip.");
        Require(entry.ImportHints.CollisionPolicy == "authoringGeometryOnly", "collision policy should roundtrip.");
        Require(entry.Preview.ThumbnailResourceKey == "char.test.preview.thumbnail", "preview metadata should roundtrip.");
        Require(entry.Provenance.SourceTool == "unit-test", "provenance should roundtrip.");
    }

    private static void ResourceKeyGenerator_GeneratesStablePackageLocalKey()
    {
        string key = CharacterPackageResourceKeyGenerator.Generate("iron_vanguard", CharacterPackageResourceTypeIds.Animation, "Locomotion");

        Require(key == "char.iron_vanguard.anim.locomotion", "resource key generator should use stable normalized package/type/local segments.");
        Require(CharacterPackageResourceKeyGenerator.IsValidResourceKey(key), "generated key should be valid ResourceKey syntax.");
        Require(!CharacterPackageResourceKeyGenerator.IsValidResourceKey("Char.Bad Key"), "invalid resource key should be rejected.");
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
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId) != null, "compiler result schema missing.");

        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ManifestSchemaId), "coordinateConvention"), "manifest coordinate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "localId"), "resource localId field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "stableId"), "resource stableId field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "hashes.contentHash"), "resource content hash field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "importHints.targetPathPolicy"), "resource target path policy field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "importHints.modelWrapperPose.scale"), "resource model wrapper scale field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "shape"), "collider shape field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "hitZoneId"), "collider hit zone field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceRadius"), "weapon trace radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId), "gate"), "validation gate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId), "resolverVerificationPlan"), "compiler resolver verification plan field missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "glb"), "resource source format glb option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "fbx"), "resource source format fbx future option missing.");
        Require(HasEnumOption("character.importTargetPathPolicy", "generatedCharacterPackage"), "import target path policy option missing.");
        Require(HasEnumOption("character.validationGate", "Unknown"), "validation gate Unknown option missing.");
        Require(HasEnumOption("character.validationGate", "Reserved1000"), "validation gate reserved option missing.");
        Require(HasEnumOption("character.compilerStatus", "ImportBlocked"), "compiler status ImportBlocked option missing.");
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

    private static void IronVanguardSample_ResourceFilesAndHashesValidate()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });
        CharacterPackageResourceHashReport hashReport = CharacterPackageResourcePipeline.BuildHashReport(package, FindSamplePath("character-iron-vanguard"));

        Require(!report.HasBlockingIssues, "Iron Vanguard resource files and hashes should validate: " + report.ToText());
        Require(!hashReport.HasBlockingIssues, "Iron Vanguard hash report should not block: " + hashReport.Diagnostics.ToText());
        Require(hashReport.Entries.Exists(entry => entry.ResourceKey == "char.iron_vanguard.model.body" && entry.Exists && entry.DeclaredContentHash == entry.ComputedContentHash), "body model hash report should include matching computed hash.");
    }

    private static void ResourceDependencyGraph_MissingDependencyBlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].Dependencies.Add(new CharacterPackageResourceDependency
        {
            ResourceKey = "char.iron_vanguard.missing.texture",
            Required = true,
            Relation = "uses"
        });

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "missing resource dependency should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.MissingResourceDependency &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "missing dependency should produce stable ImportBlocked issue.");
    }

    private static void ResourceDependencyGraph_DuplicateDependencyProducesDiagnostic()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterPackageResourceEntry combat = package.ResourceCatalog.Entries.Find(entry => entry.ResourceKey == "char.iron_vanguard.anim.combat");
        Require(combat != null, "combat animation resource missing from sample.");
        combat.Dependencies.Add(new CharacterPackageResourceDependency
        {
            ResourceKey = "char.iron_vanguard.model.body",
            Required = true,
            Relation = "retargetsToSkeleton"
        });

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);
        CharacterPackageDependencyGraph graph = CharacterPackageResourcePipeline.BuildDependencyGraph(package.ResourceCatalog);

        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.DuplicateResourceDependency), "duplicate dependency should produce stable diagnostic.");
        Require(graph.Edges.Exists(edge => edge.FromResourceKey == "char.iron_vanguard.anim.combat" && edge.ToResourceKey == "char.iron_vanguard.model.body"), "dependency graph should include combat -> body edge.");
    }

    private static void ResourceHashMismatch_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].Hashes.ContentHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(report.HasBlockingIssues, "hash mismatch should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.ResourceHashMismatch &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "hash mismatch should produce stable ImportBlocked issue.");
    }

    private static void MissingResourceFile_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].RelativePath = "resources/models/missing.glb";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package, new CharacterResourcePackageValidationOptions
        {
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ValidateResourceFiles = true
        });

        Require(report.HasBlockingIssues, "missing resource file should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.MissingResourceFile &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "missing resource file should produce stable ImportBlocked issue.");
    }

    private static void InvalidImportTargetPath_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].ImportHints.TargetRelativePath = "../outside.glb";

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(report.HasBlockingIssues, "invalid Unity target path should block import.");
        Require(report.Issues.Exists(issue =>
            issue.Code == CharacterAuthoringValidationCodes.InvalidImportTargetPath &&
            issue.Gate == CharacterAuthoringValidationGate.ImportBlocked), "invalid import target path should produce stable ImportBlocked issue.");
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

    private static void IronVanguardSample_CompilesToConfigPatchGeometryMappingAndWritePlan()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);
        CharacterAuthoringCompileResult second = Compile(LoadSample("character-iron-vanguard"), checkFiles: true, checkHashes: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.Ready, "Iron Vanguard compiler status should be Ready.");
        Require(result.IsDeterministicFullCompile, "v1 compiler should declare deterministic full compile.");
        Require(result.Hashes.SourcePackageHash == second.Hashes.SourcePackageHash, "source package hash should be deterministic.");
        Require(result.Hashes.GeneratedConfigHash == second.Hashes.GeneratedConfigHash, "generated config hash should be deterministic.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.CharacterConfig), "compiler should generate CharacterConfig patch entry.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.EquipmentStateConfig), "compiler should generate EquipmentStateConfig patch entries.");
        Require(result.GeneratedConfigPatch.Patch.Entries.Exists(entry => entry.Source == CharacterApplicationCompilerTableNames.CombatActionSetConfig), "compiler should generate CombatActionSetConfig patch entries.");
        Require(result.GeometryBinding.BodyColliders.Exists(collider => collider.ColliderId == "head_sphere" && collider.Shape == CharacterColliderShape.Sphere), "compiler geometry binding should include body collider data.");
        Require(result.GeometryBinding.WeaponAttachments.Exists(attachment => attachment.WeaponId == "weapon.iron_sword" && attachment.TraceId == "trace.iron_sword.blade"), "compiler geometry binding should include weapon attachment and trace binding.");
        Require(result.ResourceMapping.Entries.Exists(entry => entry.PackageResourceKey == "char.iron_vanguard.model.body" && entry.ImportTargetPath.Contains("Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard")), "compiler resource mapping should map package ResourceKey to Unity import target.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "Ready compiler result should be importable.");
        Require(result.UnityImportWritePlan.CanSpawnAfterImport, "Ready compiler result should be spawnable after import.");
        Require(result.UnityImportWritePlan.Writes.Exists(write => write.Kind == CharacterAuthoringCompilerWriteKinds.GeneratedConfigPatch), "write plan should include generated config patch target.");
        Require(result.UnityImportWritePlan.Writes.Exists(write => write.Kind == CharacterAuthoringCompilerWriteKinds.ResourceFile && write.SourcePath == "resources/models/iron_vanguard.glb"), "write plan should include resource file copy target.");
        Require(result.ResolverVerificationPlan.Status == "Ready", "resolver verification plan should be ready.");
        Require(result.ResolverVerificationPlan.ExpectedResolverEntrypoint == "CharacterPackageResolver.Resolve", "resolver verification plan should name the runtime resolver.");
        Require(result.ResolverVerificationPlan.DefaultLoadoutStableId == "equip_loadout.iron_vanguard.sword_shield", "default loadout should be sword shield.");
        Require(result.ResolverVerificationPlan.ExpectedActiveEquipmentStateStableId == "equip_state.iron_vanguard.sword_shield", "default active equipment state should match sword shield.");
        Require(result.ResolverVerificationPlan.RequiredTables.Count >= 12, "resolver verification plan should enumerate all Character Application tables.");
        Require(result.ResolverVerificationPlan.KnownAbilityIds.Contains(900001), "resolver verification plan should include generated base ability ids.");
    }

    private static void Compiler_ModelWrapperPoseChangesImportAndResourceMappingHash()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterPackageResourceEntry body = package.ResourceCatalog.Entries.Find(entry => entry.ResourceKey == "char.iron_vanguard.model.body");
        Require(body != null, "body model resource missing from sample.");

        body.ImportHints.ModelWrapperPose = new CharacterAuthoringLocalPose();
        string baselineImportHash = CharacterPackageResourcePipeline.ComputeImportHash(body);
        CharacterAuthoringCompileResult baseline = Compile(package);

        body.ImportHints.ModelWrapperPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.05f, -0.1f, 0.2f),
            Scale = new CharacterAuthoringVector3(1.25f, 1.25f, 1.25f),
            EulerHint = new CharacterAuthoringVector3(0f, 90f, 0f)
        };

        string adjustedImportHash = CharacterPackageResourcePipeline.ComputeImportHash(body);
        CharacterAuthoringCompileResult adjusted = Compile(package);
        CharacterPackageResourceMappingEntry mappedBody = adjusted.ResourceMapping.Entries.Find(entry => entry.PackageResourceKey == "char.iron_vanguard.model.body");

        Require(baselineImportHash != adjustedImportHash, "model wrapper pose should affect import hash.");
        Require(baseline.Hashes.ResourceMappingHash != adjusted.Hashes.ResourceMappingHash, "model wrapper pose should affect resource mapping hash.");
        Require(mappedBody != null && mappedBody.ModelWrapperPose.Scale.X == 1.25f, "resource mapping should carry model wrapper pose.");
    }

    private static void Compiler_ApplicationResourceKeysAreCharacterReferences()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ApplicationConfig.ResourceKeys.Remove("char.iron_vanguard.weapon.shield.model");

        CharacterAuthoringCompileResult result = Compile(package);
        PatchEntry presentation = result.GeneratedConfigPatch.Patch.Entries.Find(entry => entry.Source == CharacterApplicationCompilerTableNames.CharacterPresentationProfileConfig);
        PatchEntry shieldWeapon = result.GeneratedConfigPatch.Patch.Entries.Find(entry =>
            entry.Source == CharacterApplicationCompilerTableNames.WeaponConfig &&
            entry.Fields.GetScalar("StableId") == "mx.weapon.weapon.kite_shield");

        Require(result.ResourceMapping.Entries.Exists(entry => entry.PackageResourceKey == "char.iron_vanguard.weapon.shield.model"), "resource mapping should keep standalone catalog resources.");
        Require(!ResourceKeysContain(presentation, "char.iron_vanguard.weapon.shield.model"), "character presentation references should follow application resource keys.");
        Require(ResourceKeysContain(shieldWeapon, "char.iron_vanguard.weapon.shield.model"), "weapon config should keep its own model reference.");
    }

    private static void Compiler_UnsupportedConvexShape_BlocksExport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.Geometry.Colliders[0].Shape = CharacterColliderShape.Convex;

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.ExportBlocked, "convex collider should block export/importable artifact.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject, "ExportBlocked result must not be writable to Unity project.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.UnsupportedColliderShape, CharacterAuthoringValidationGate.ExportBlocked), "unsupported collider shape should keep stable ExportBlocked issue.");
    }

    private static void Compiler_MissingSocket_BlocksSpawnOnly()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.Geometry.WeaponAttachments[0].AttachSocketId = "missing.socket";

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.SpawnBlocked, "missing weapon socket should block spawn.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "SpawnBlocked result can still import resources/config.");
        Require(!result.UnityImportWritePlan.CanSpawnAfterImport, "SpawnBlocked result must not spawn after import.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.MissingAttachmentSocket, CharacterAuthoringValidationGate.SpawnBlocked), "missing attachment socket should keep stable SpawnBlocked issue.");
    }

    private static void Compiler_MissingResource_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].RelativePath = "resources/models/missing.glb";

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "missing resource should block import.");
        Require(!result.UnityImportWritePlan.CanWriteToUnityProject, "ImportBlocked result must not write Unity project.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.MissingResourceFile, CharacterAuthoringValidationGate.ImportBlocked), "missing resource should keep stable ImportBlocked issue.");
    }

    private static void Compiler_CoordinateMismatch_ReportsWarningOnly()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.Manifest.CoordinateConvention.UpAxis = CharacterCoordinateAxis.ZPositive;

        CharacterAuthoringCompileResult result = Compile(package);

        Require(result.Status == CharacterAuthoringCompilerStatus.WarningOnly, "coordinate mismatch should be warning-only when conversion plan is available.");
        Require(result.GeometryBinding.CoordinateConversion.RequiresConversion, "coordinate mismatch should emit conversion plan.");
        Require(result.UnityImportWritePlan.CanWriteToUnityProject, "WarningOnly result should remain importable.");
        Require(HasCompilerIssue(result, CharacterAuthoringCompilerValidationCodes.CoordinateTargetMismatch, CharacterAuthoringValidationGate.WarningOnly), "coordinate mismatch should use stable compiler warning code.");
    }

    private static void Compiler_HashMismatch_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries[0].Hashes.ContentHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        CharacterAuthoringCompileResult result = Compile(package, checkFiles: true, checkHashes: true);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "hash mismatch should block import.");
        Require(HasCompilerIssue(result, CharacterAuthoringValidationCodes.ResourceHashMismatch, CharacterAuthoringValidationGate.ImportBlocked), "hash mismatch should keep stable ImportBlocked issue.");
    }

    private static void Compiler_ResourceKeyConflict_BlocksImport()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        var projectCatalog = new CharacterPackageResourceCatalog();
        projectCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = package.ResourceCatalog.Entries[0].ResourceKey,
            StableId = package.ResourceCatalog.Entries[0].StableId,
            TypeId = package.ResourceCatalog.Entries[0].TypeId,
            Usage = package.ResourceCatalog.Entries[0].Usage,
            RelativePath = "Assets/Existing/iron_vanguard.glb",
            Hashes = new CharacterPackageResourceHashes
            {
                ContentHash = "sha256:1111111111111111111111111111111111111111111111111111111111111111"
            }
        });

        CharacterAuthoringCompileResult result = Compile(package, projectCatalog: projectCatalog);

        Require(result.Status == CharacterAuthoringCompilerStatus.ImportBlocked, "ResourceKey conflict with different hash should block import.");
        Require(HasCompilerIssue(result, CharacterAuthoringCompilerValidationCodes.ResourceKeyConflict, CharacterAuthoringValidationGate.ImportBlocked), "ResourceKey conflict should use stable compiler issue code.");
    }

    private static void UnityImportBridge_ImportsIronVanguardAndSkipsRepeat()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-ready");
        ResetDirectory(root);

        int firstExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            FindSamplePath("character-iron-vanguard"),
            "--project-root",
            root,
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "iron_vanguard");
        string reportPath = Path.Combine(targetRoot, "package_cache", "import_report.json");
        string catalogPath = Path.Combine(targetRoot, "config", "unity_resource_catalog.json");
        Require(firstExit == MxFramework.Authoring.Cli.Program.ExitReady, "first Unity import should be ready.");
        Require(File.Exists(Path.Combine(targetRoot, "resources", "models", "iron_vanguard.glb")), "Unity import should copy model resource.");
        Require(File.Exists(Path.Combine(targetRoot, "generated", "character_application_config_patch.json")), "Unity import should write compiler config patch target.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "geometry_binding.json")), "Unity import should write readable config geometry binding alias.");
        Require(File.Exists(catalogPath), "Unity import should write project ResourceCatalog JSON.");

        CharacterUnityImportReport firstReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(firstReport != null && firstReport.Status == CharacterAuthoringCompilerStatus.Ready.ToString(), "first import report should be Ready.");
        Require(firstReport.AddedCount > 0, "first import should add files.");
        string catalogJson = File.ReadAllText(catalogPath);
        Require(catalogJson.Contains("\"provider\": \"memory\""), "generated Unity ResourceCatalog should use v1 memory provider bridge.");
        Require(catalogJson.Contains("\"assetPath\": \"Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard/resources/models/iron_vanguard.glb\""), "generated ResourceCatalog should preserve Unity assetPath mapping.");

        int secondExit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            FindSamplePath("character-iron-vanguard"),
            "--project-root",
            root,
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        CharacterUnityImportReport secondReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(secondExit == MxFramework.Authoring.Cli.Program.ExitReady, "second Unity import should be ready.");
        Require(secondReport != null && secondReport.SkippedCount >= firstReport.AddedCount, "repeat import should skip unchanged write plan targets.");
        Require(secondReport.AddedCount == 0 && secondReport.UpdatedCount == 0, "repeat import should not rewrite unchanged targets.");
    }

    private static void UnityImportBridge_ImportBlockedDoesNotWriteProjectTarget()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-blocked");
        ResetDirectory(root);
        string packageCopy = Path.Combine(root, "package-copy");
        CopyDirectory(FindSamplePath("character-iron-vanguard"), packageCopy);
        File.Delete(Path.Combine(packageCopy, "resources", "models", "iron_vanguard.glb"));

        string reportOut = Path.Combine(root, "report-out");
        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--check-files",
            "--check-hashes",
            "--report-out",
            reportOut
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "iron_vanguard");
        Require(exit == MxFramework.Authoring.Cli.Program.ExitValidationBlocked, "ImportBlocked package should return validation-blocked exit.");
        Require(!Directory.Exists(targetRoot), "ImportBlocked package must not write Unity project target root.");
        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(Path.Combine(reportOut, "import_report.json")), JsonOptions);
        Require(report != null && report.Status == CharacterAuthoringCompilerStatus.ImportBlocked.ToString(), "blocked import report should preserve ImportBlocked status.");
        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.MissingResourceFile), "blocked import report should include missing resource diagnostic.");
    }

    private static void UnityImportBridge_SpawnBlockedWritesButMarksNotSpawnable()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue222-import-spawn-blocked");
        ResetDirectory(root);
        string packageCopy = Path.Combine(root, "package-copy");
        CopyDirectory(FindSamplePath("character-iron-vanguard"), packageCopy);
        string attachmentsPath = Path.Combine(packageCopy, "geometry", "weapon_attachments.json");
        File.WriteAllText(attachmentsPath, File.ReadAllText(attachmentsPath).Replace("\"attachSocketId\": \"mainHand\"", "\"attachSocketId\": \"missing.socket\""));

        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "import-unity",
            "--package",
            packageCopy,
            "--project-root",
            root,
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string targetRoot = Path.Combine(root, "Assets", "MxFrameworkGenerated", "CharacterPackages", "iron_vanguard");
        string reportPath = Path.Combine(targetRoot, "package_cache", "import_report.json");
        Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "SpawnBlocked import should still complete project writes.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "geometry_binding.json")), "SpawnBlocked import should write geometry binding for inspection.");
        CharacterUnityImportReport report = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(report != null && report.Status == CharacterAuthoringCompilerStatus.SpawnBlocked.ToString(), "SpawnBlocked import report should preserve status.");
        Require(!report.CanSpawnAfterImport, "SpawnBlocked import must mark runtime spawn unavailable.");
        Require(report.Issues.Exists(issue => issue.Code == CharacterAuthoringValidationCodes.MissingAttachmentSocket), "SpawnBlocked report should include socket diagnostic.");
    }

    private static CharacterAuthoringCompileResult Compile(
        CharacterResourcePackage package,
        bool checkFiles = false,
        bool checkHashes = false,
        CharacterPackageResourceCatalog projectCatalog = null)
    {
        return CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ExistingProjectResourceCatalogSummary = projectCatalog,
            Options = new CharacterAuthoringCompileOptions
            {
                ValidateResourceFiles = checkFiles || checkHashes,
                ValidateResourceHashes = checkHashes
            }
        });
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception("ASSERTION FAILED: " + message);
    }
}
