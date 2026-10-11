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
    private static void AuthoringResourceReferenceGraph_ScansCrossConsumerReferencesAndDiagnostics()
    {
        var package = new CharacterResourcePackage();
        package.Manifest.PackageId = "test";
        package.Manifest.StableId = "charpkg.test";
        package.ApplicationConfig.CharacterStableId = "character.test";
        package.ApplicationConfig.ResourceKeys.Add("char.test.model.body");
        package.ApplicationConfig.ResourceKeys.Add("char.test.resource.missing");
        package.GlobalWeaponLibrary = GlobalWeaponLibrarySchema.CreateDefault();
        package.GlobalWeaponLibrary.SingleWeaponConfigurations.Add(new SingleWeaponConfiguration
        {
            SingleWeaponConfigurationId = "weapon.test.sword",
            WeaponRef = new AuthoringResourceSelectionRef
            {
                PackageResourceKey = "char.test.weapon.sword"
            }
        });
        package.ApplicationConfig.CombinedWeaponConfigurations.Add(new CombinedWeaponConfigurationAuthoringSummary
        {
            CombinedWeaponConfigurationId = "test",
            Slots = new List<CombinedWeaponConfigurationSlotAuthoringSummary>
            {
                new CombinedWeaponConfigurationSlotAuthoringSummary
                {
                    SlotId = CharacterWeaponConfigurationValidator.MainHandSlotId,
                    SingleWeaponConfigurationId = "weapon.test.sword"
                },
                new CombinedWeaponConfigurationSlotAuthoringSummary
                {
                    SlotId = CharacterWeaponConfigurationValidator.OffHandSlotId,
                    SingleWeaponConfigurationId = GlobalWeaponLibrarySchema.UnarmedConfigurationId
                }
            }
        });

        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.model.body",
            LocalId = "model.body",
            StableId = "charpkg.test.resource.model.body",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            PackageId = "test",
            RelativePath = "resources/models/body.glb",
            Preview = new CharacterPackagePreviewMetadata
            {
                ThumbnailResourceKey = "char.test.preview.thumbnail"
            },
            Dependencies = new List<CharacterPackageResourceDependency>
            {
                new CharacterPackageResourceDependency
                {
                    ResourceKey = "char.test.material.body",
                    Required = true,
                    Relation = "material"
                }
            }
        });
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.weapon.sword",
            StableId = "charpkg.test.resource.weapon.sword",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.WeaponModel,
            PackageId = "test"
        });
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.material.body",
            StableId = "charpkg.test.resource.material.body",
            TypeId = CharacterPackageResourceTypeIds.Material,
            Usage = CharacterPackageResourceUsageIds.Material,
            PackageId = "test"
        });
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.preview.thumbnail",
            StableId = "charpkg.test.resource.preview.thumbnail",
            TypeId = CharacterPackageResourceTypeIds.Preview,
            Usage = CharacterPackageResourceUsageIds.PreviewThumbnail,
            PackageId = "test"
        });
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.unused",
            StableId = "charpkg.test.resource.unused",
            TypeId = CharacterPackageResourceTypeIds.Texture,
            Usage = CharacterPackageResourceUsageIds.Texture,
            PackageId = "test"
        });

        AuthoringResourceCollection collection = CharacterPackageAuthoringResourceProvider.FromPackageResourceCatalog(
            package.ResourceCatalog,
            new AuthoringResourceProviderContext { PackageId = "test" });
        AuthoringResourceItem body = collection.Items.Find(item => item.StableId == "charpkg.test.resource.model.body");
        body.RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable;

        AuthoringResourceReferenceGraph graph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(package, collection);

        Require(graph.Edges.Exists(edge => edge.SourceConfigKind == "character" && edge.TargetProviderResourceKey == "char.test.model.body" && edge.PreloadPolicy == AuthoringResourcePreloadPolicies.SpawnCritical), "character resource key should become a graph edge.");
        Require(graph.Edges.Exists(edge => edge.SourceConfigKind == "weapon" && edge.SourceStableId == "weapon.test.sword" && edge.TargetProviderResourceKey == "char.test.weapon.sword"), "weapon-definition resource should become a graph edge.");
        Require(graph.Edges.Exists(edge => edge.SourceConfigKind == "resource" && edge.SourceField == "dependencies/0/resourceKey" && edge.TargetProviderResourceKey == "char.test.material.body"), "resource dependencies should become graph edges.");
        Require(graph.Edges.Exists(edge => edge.SourceConfigKind == "resource" && edge.SourceField == "preview.thumbnailResourceKey" && edge.TargetProviderResourceKey == "char.test.preview.thumbnail"), "preview metadata should become graph edges.");
        Require(graph.CountReferencesToStableId("charpkg.test.resource.model.body") == 1, "graph should count incoming references by stable id.");
        Require(graph.FindReferencesToResource(body).Count == 1, "graph should find incoming references for an item.");
        Require(graph.HasIncomingReferences(body), "referenceCount > 0 should block destructive delete.");
        Require(graph.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ReferenceBroken && diagnostic.SourceField == "resourceKeys/1"), "missing target should produce a broken reference diagnostic.");
        Require(graph.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.NotRuntimeLoadable && diagnostic.ResourceStableId == "charpkg.test.resource.model.body"), "runtime-required non-loadable target should produce runtime availability diagnostic.");
        Require(graph.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.OrphanCandidate && diagnostic.ResourceStableId == "charpkg.test.resource.unused"), "unreferenced resources should be orphan warnings.");
    }

    private static void ResourceFieldSpec_ResolveSelection_FillsCompiledResourceReference()
    {
        var library = new CharacterResourceLibrary();
        library.Items.Add(new ResourceLibraryItem
        {
            LibraryItemId = "char.test.model.body",
            StableId = "charpkg.test.resource.model.body",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceKind = ResourceLibrarySourceKind.UnityAsset,
            RuntimeBindingKind = RuntimeBindingKind.ResourceManagerAsset,
            RuntimeAvailability = ResourceRuntimeAvailability.RuntimeReady,
            ImportStatus = ResourceImportStatus.Clean,
            ResourceKey = "char.test.model.body.prefab",
            ProviderId = "memory",
            Hash = "sha256:abc",
            Compatibility = new ResourceLibraryCompatibility
            {
                SkeletonStableId = "skeleton.test",
                SlotId = "body"
            }
        });

        var spec = new ResourceFieldSpec
        {
            FieldKey = "Character.Model",
            DisplayName = "Character Model",
            AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
            AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.CharacterModel },
            AcceptedBindingKinds = new List<RuntimeBindingKind> { RuntimeBindingKind.ResourceManagerAsset },
            RequireRuntimeLoadable = true,
            RequireUnityImported = true,
            PreloadPolicy = ResourceLibraryPreloadPolicies.SpawnCritical,
            OutputKind = ResourceSelectionOutputKind.ResourceKey,
            CompatibilityFilter = new ResourceCompatibilityFilter
            {
                SkeletonStableId = "skeleton.test",
                SlotId = "body"
            }
        };

        var selection = new ResourceSelectionRef
        {
            LibraryItemStableId = "charpkg.test.resource.model.body",
            BindingKind = RuntimeBindingKind.ResourceManagerAsset
        };

        ResourceSelectionResolutionResult result = CharacterResourceLibraryBuilder.ResolveSelection(library, spec, selection);

        Require(result.Accepted, "matching model selection should be accepted.");
        Require(result.Diagnostics.Count == 0, "matching model selection should not produce diagnostics.");
        Require(selection.ResourceKey == "char.test.model.body.prefab", "accepted selection should receive compiled ResourceKey.");
        Require(selection.ProviderId == "memory", "accepted selection should receive provider id.");
        Require(selection.ExpectedKind == CharacterPackageResourceTypeIds.Model, "accepted selection should refresh expected kind.");
        Require(selection.ExpectedUsage == CharacterPackageResourceUsageIds.CharacterModel, "accepted selection should refresh expected usage.");
        Require(selection.Hash == "sha256:abc", "accepted selection should receive hash.");
    }

    private static void ResourceReferenceGraph_ValidatesBrokenReferencesAndOrphans()
    {
        var library = new CharacterResourceLibrary();
        library.Items.Add(new ResourceLibraryItem
        {
            LibraryItemId = "char.test.model.body",
            StableId = "charpkg.test.resource.model.body",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            RuntimeBindingKind = RuntimeBindingKind.ResourceManagerAsset,
            ResourceKey = "char.test.model.body"
        });
        library.Items.Add(new ResourceLibraryItem
        {
            LibraryItemId = "char.test.model.unused",
            StableId = "charpkg.test.resource.model.unused",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.WeaponModel,
            RuntimeBindingKind = RuntimeBindingKind.ResourceManagerAsset,
            ResourceKey = "char.test.model.unused"
        });
        library.ReferenceGraph = CharacterResourceLibraryBuilder.BuildReferenceGraph(new[]
        {
            new ResourceReferenceEdge
            {
                SourceConfigKind = "character",
                SourceStableId = "char.test",
                SourceField = "model",
                TargetLibraryItemStableId = "charpkg.test.resource.model.body",
                TargetResourceKey = "char.test.model.body",
                BindingKind = RuntimeBindingKind.ResourceManagerAsset,
                IsRequiredAtRuntime = true,
                PreloadPolicy = ResourceLibraryPreloadPolicies.SpawnCritical
            },
            new ResourceReferenceEdge
            {
                SourceConfigKind = "weapon",
                SourceStableId = "weapon.test",
                SourceField = "icon",
                TargetLibraryItemStableId = "charpkg.test.resource.texture.missing",
                BindingKind = RuntimeBindingKind.ResourceManagerAsset,
                IsRequiredAtRuntime = false,
                PreloadPolicy = ResourceLibraryPreloadPolicies.UiDeferred
            }
        });

        List<ResourceLibraryDiagnostic> diagnostics = CharacterResourceLibraryBuilder.ValidateLibrary(library);

        Require(library.ReferenceGraph.CountReferencesToStableId("charpkg.test.resource.model.body") == 1, "reference graph should count incoming references.");
        Require(diagnostics.Exists(d => d.Code == ResourceLibraryDiagnosticCodes.ReferenceBroken && d.SourceField == "icon"), "broken graph edge should produce stable diagnostic.");
        Require(diagnostics.Exists(d => d.Code == ResourceLibraryDiagnosticCodes.OrphanCandidate && d.LibraryItemStableId == "charpkg.test.resource.model.unused"), "unreferenced library item should be marked orphan candidate.");
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
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.BodyPartSchemaId) != null, "body part schema missing.");
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId) != null, "collider schema missing.");
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId) != null, "validation issue schema missing.");
        Require(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId) != null, "compiler result schema missing.");

        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ManifestSchemaId), "coordinateConvention"), "manifest coordinate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "localId"), "resource localId field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "stableId"), "resource stableId field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "hashes.contentHash"), "resource content hash field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "importHints.targetPathPolicy"), "resource target path policy field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ResourceCatalogSchemaId), "importHints.modelWrapperPose.scale"), "resource model wrapper scale field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyPartSchemaId), "bonePath"), "body part bonePath field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyPartSchemaId), "locatorId"), "body part locatorId field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "shape"), "collider shape field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.BodyColliderSchemaId), "hitZoneId"), "collider hit zone field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.SocketSchemaId), "mirrorPairSocketId"), "socket mirror pair field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.SocketSchemaId), "abstractBoneId"), "socket abstract bone field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.SocketSchemaId), "tags"), "socket tags field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "slotPose"), "character weapon slot pose field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "attachSocketId"), "character weapon slot socket field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponTraceSchemaId), "radius"), "weapon-local trace radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponTraceSchemaId), "proximalExtension"), "weapon trace proximal extension field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponTraceSchemaId), "proximalRadius"), "weapon trace proximal radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId), "gate"), "validation gate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId), "resolverVerificationPlan"), "compiler resolver verification plan field missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "glb"), "resource source format glb option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "fbx"), "resource source format fbx option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "anim"), "resource source format anim option missing.");
        Require(HasEnumOption("character.importTargetPathPolicy", "generatedCharacterPackage"), "import target path policy option missing.");
        Require(HasEnumOption("character.bodyPartKind", "Bone"), "body part kind Bone option missing.");
        Require(HasEnumOption("character.poseParentKind", "Socket"), "pose parent Socket option missing.");
        Require(HasEnumOption("character.validationGate", "Unknown"), "validation gate Unknown option missing.");
        Require(HasEnumOption("character.validationGate", "Reserved1000"), "validation gate reserved option missing.");
        Require(HasEnumOption("character.compilerStatus", "ImportBlocked"), "compiler status ImportBlocked option missing.");
        Require(HasEnumOption("character.colliderShape", "Convex"), "reserved convex shape option missing.");
    }

}
