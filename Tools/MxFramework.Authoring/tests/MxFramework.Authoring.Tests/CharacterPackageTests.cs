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
        ResourceLibraryItem_JsonRoundTrip_PreservesRuntimeContract();
        AuthoringResourceItem_JsonRoundTrip_PreservesProviderBindings();
        CharacterPackageProvider_ProjectsPackageResourceWithoutRuntimeKey();
        CharacterPackageProvider_ReportsDuplicateStableIdsAndProviderKeys();
        ResourceFieldSpec_ResolveSelection_FillsCompiledResourceReference();
        ResourceReferenceGraph_ValidatesBrokenReferencesAndOrphans();
        ResourceKeyGenerator_GeneratesStablePackageLocalKey();
        CoordinateConvention_JsonRoundTrip_PreservesUnityTargetConvention();
        Schemas_ExposeC0ContractFields();
        BodyPartAuthoring_JsonRoundTrip_PreservesSkeletonBindingFields();
        SkeletonBindingValidation_ReportsBrokenReferences();
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
        IronVanguardSample_CompilesResourcePlanArtifacts();
        CharacterResourcesPlanCommand_WritesRuntimePlanArtifacts();
        Compiler_ApplicationResourceKeysAreCharacterReferences();
        Compiler_ModelWrapperPoseChangesImportAndResourceMappingHash();
        Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes();
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

    private static void ResourceLibraryItem_JsonRoundTrip_PreservesRuntimeContract()
    {
        var library = new CharacterResourceLibrary
        {
            PackageId = "test"
        };
        library.Items.Add(new ResourceLibraryItem
        {
            LibraryItemId = "lib.test.audio.hit",
            StableId = "charpkg.test.resource.audio.hit",
            DisplayName = "Hit Sfx",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            SourceKind = ResourceLibrarySourceKind.FmodLibrary,
            RuntimeBindingKind = RuntimeBindingKind.AudioCue,
            ImportStatus = ResourceImportStatus.Clean,
            RuntimeAvailability = ResourceRuntimeAvailability.AudioCueOnly,
            FmodEventPath = "event:/Character/Test/Hit",
            AudioCueId = "cue.character.test.hit",
            AudioEventDefinitionId = "event.character.test.hit",
            Tags = new List<string> { "combat", "hit" },
            Diagnostics = new List<ResourceLibraryDiagnostic>
            {
                new ResourceLibraryDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = ResourceLibraryDiagnosticCodes.FmodBankMissing,
                    LibraryItemStableId = "charpkg.test.resource.audio.hit",
                    SourceConfigKind = "audio",
                    SourceField = "hitSfx",
                    Message = "bank missing",
                    SuggestedFix = "sync fmod banks"
                }
            }
        });

        string json = JsonSerializer.Serialize(library, JsonOptions);
        CharacterResourceLibrary roundTrip = JsonSerializer.Deserialize<CharacterResourceLibrary>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.Items.Count == 1, "resource library should roundtrip.");
        ResourceLibraryItem item = roundTrip.Items[0];
        Require(item.StableId == "charpkg.test.resource.audio.hit", "library stable id should roundtrip.");
        Require(item.SourceKind == ResourceLibrarySourceKind.FmodLibrary, "source kind should roundtrip.");
        Require(item.RuntimeBindingKind == RuntimeBindingKind.AudioCue, "runtime binding kind should roundtrip.");
        Require(item.RuntimeAvailability == ResourceRuntimeAvailability.AudioCueOnly, "runtime availability should roundtrip.");
        Require(item.ImportStatus == ResourceImportStatus.Clean, "import status should roundtrip.");
        Require(item.AudioCueId == "cue.character.test.hit", "audio cue id should roundtrip.");
        Require(item.Diagnostics.Count == 1 && item.Diagnostics[0].Code == ResourceLibraryDiagnosticCodes.FmodBankMissing, "resource diagnostics should roundtrip.");
    }

    private static void AuthoringResourceItem_JsonRoundTrip_PreservesProviderBindings()
    {
        var collection = new AuthoringResourceCollection
        {
            ScopeId = "global"
        };
        collection.Providers.Add(new AuthoringResourceProviderDescriptor
        {
            ProviderId = AuthoringResourceProviderIds.Fmod,
            DisplayName = "FMOD",
            SourceKind = AuthoringResourceSourceKind.FmodLibrary,
            Available = true,
            Status = "Ready"
        });
        collection.Providers.Add(new AuthoringResourceProviderDescriptor
        {
            ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
            DisplayName = "Unity AssetDatabase",
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            Available = true,
            Status = "Ready"
        });
        collection.Providers.Add(new AuthoringResourceProviderDescriptor
        {
            ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            DisplayName = "Runtime Catalog",
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            Available = true,
            Status = "Ready"
        });
        collection.Providers.Add(new AuthoringResourceProviderDescriptor
        {
            ProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
            DisplayName = "External Import Staging",
            SourceKind = AuthoringResourceSourceKind.ExternalFile,
            Available = true,
            Status = "Ready"
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:event.character.test.hit",
            StableId = "audio.event.character.test.hit",
            DisplayName = "Hit Sfx",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            SourceKind = AuthoringResourceSourceKind.FmodLibrary,
            BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    DisplayValue = "{00000000-0000-0000-0000-000000000001}",
                    IsPrimary = true,
                    ProviderResourceKey = "event:/Character/Test/Hit",
                    FmodEventPath = "event:/Character/Test/Hit",
                    FmodEventGuid = "{00000000-0000-0000-0000-000000000001}",
                    Hash = "sha256:fmod"
                }
            },
            Diagnostics = new List<AuthoringResourceDiagnostic>
            {
                new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.FmodBankMissing,
                    ResourceId = "fmod:event.character.test.hit",
                    ResourceStableId = "audio.event.character.test.hit",
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    Message = "bank missing"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "unity:asset.guid.body",
            StableId = "unity.asset.body.prefab",
            DisplayName = "Body Prefab",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            BindingKind = AuthoringResourceBindingKind.UnityAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityGuid,
                    DisplayValue = "guid-body",
                    IsPrimary = true,
                    UnityGuid = "guid-body"
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    DisplayValue = "Assets/Characters/body.prefab",
                    UnityAssetPath = "Assets/Characters/body.prefab"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "runtime:char.test.model.body.prefab",
            StableId = "runtime.char.test.model.body.prefab",
            DisplayName = "Body Runtime Prefab",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    DisplayValue = "char.test.model.body.prefab",
                    IsPrimary = true,
                    RuntimeResourceKey = "char.test.model.body.prefab",
                    Address = "bundles/characters/body.prefab",
                    AssetType = "UnityEngine.GameObject",
                    Hash = "sha256:runtime"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "external:/imports/body.fbx",
            StableId = "external.body.fbx",
            DisplayName = "body.fbx",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
            SourceKind = AuthoringResourceSourceKind.ExternalFile,
            BindingKind = AuthoringResourceBindingKind.ExternalSource,
            ImportStatus = AuthoringResourceImportStatus.New,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                    BindingKind = AuthoringResourceBindingKind.ExternalSource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
                    DisplayValue = "/imports/body.fbx",
                    IsPrimary = true,
                    ExternalSourcePath = "/imports/body.fbx"
                }
            }
        });

        string json = JsonSerializer.Serialize(collection, JsonOptions);
        AuthoringResourceCollection roundTrip = JsonSerializer.Deserialize<AuthoringResourceCollection>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.Items.Count == 4, "authoring resource collection should roundtrip.");
        AuthoringResourceItem item = roundTrip.Items.Find(i => i.ResourceId == "fmod:event.character.test.hit");
        Require(item != null, "FMOD item should roundtrip.");
        Require(item.ResourceId == "fmod:event.character.test.hit", "resourceId should roundtrip.");
        Require(item.SourceProviderId == AuthoringResourceProviderIds.Fmod, "source provider should roundtrip.");
        Require(item.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition, "binding kind should roundtrip.");
        Require(item.ProviderBindings.Count == 1, "provider binding should roundtrip.");
        Require(item.ProviderBindings[0].BindingKeyKind == AuthoringResourceBindingKeyKinds.FmodEventGuid, "provider binding key kind should roundtrip.");
        Require(item.ProviderBindings[0].FmodEventGuid == "{00000000-0000-0000-0000-000000000001}", "FMOD guid should roundtrip.");
        Require(string.IsNullOrEmpty(item.ProviderBindings[0].RuntimeResourceKey), "FMOD event must not gain a runtime resource key.");
        Require(item.Diagnostics.Count == 1 && item.Diagnostics[0].ResourceId == "fmod:event.character.test.hit", "authoring resource diagnostic should retain resource id.");
        Require(item.Diagnostics[0].Code == AuthoringResourceDiagnosticCodes.FmodBankMissing, "authoring resource diagnostics should roundtrip.");

        AuthoringResourceItem unityItem = roundTrip.Items.Find(i => i.ResourceId == "unity:asset.guid.body");
        Require(unityItem != null, "Unity item should roundtrip.");
        Require(unityItem.ProviderBindings.Exists(b => b.BindingKeyKind == AuthoringResourceBindingKeyKinds.UnityGuid && b.UnityGuid == "guid-body"), "Unity GUID binding should not be represented as a runtime key.");
        Require(unityItem.ProviderBindings.Exists(b => b.BindingKeyKind == AuthoringResourceBindingKeyKinds.UnityAssetPath && b.UnityAssetPath == "Assets/Characters/body.prefab"), "Unity path binding should roundtrip.");
        Require(unityItem.ProviderBindings.TrueForAll(b => string.IsNullOrEmpty(b.RuntimeResourceKey)), "Unity editor bindings should not gain runtime keys.");

        AuthoringResourceItem runtimeItem = roundTrip.Items.Find(i => i.ResourceId == "runtime:char.test.model.body.prefab");
        Require(runtimeItem != null, "runtime item should roundtrip.");
        Require(runtimeItem.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeReady, "runtime catalog item should preserve runtime availability.");
        Require(runtimeItem.ProviderBindings[0].RuntimeResourceKey == "char.test.model.body.prefab", "runtime resource key should be explicit.");
        Require(string.IsNullOrEmpty(runtimeItem.ProviderBindings[0].PackageResourceKey), "runtime resource key must not be copied into package-local key.");

        AuthoringResourceItem externalItem = roundTrip.Items.Find(i => i.ResourceId == "external:/imports/body.fbx");
        Require(externalItem != null, "external source item should roundtrip.");
        Require(externalItem.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable, "external source should remain not runtime-loadable before import.");
        Require(externalItem.ProviderBindings[0].ExternalSourcePath == "/imports/body.fbx", "external source path should roundtrip.");
        Require(string.IsNullOrEmpty(externalItem.ProviderBindings[0].RuntimeResourceKey), "external source must not gain a runtime resource key.");
    }

    private static void CharacterPackageProvider_ProjectsPackageResourceWithoutRuntimeKey()
    {
        var catalog = new CharacterPackageResourceCatalog();
        catalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.model.body",
            LocalId = "model.body",
            StableId = "charpkg.test.resource.model.body",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceFormat = CharacterPackageResourceFormatIds.Glb,
            PackageId = "test",
            RelativePath = "resources/models/body.glb",
            Hashes = new CharacterPackageResourceHashes
            {
                ContentHash = "sha256:body"
            },
            Tags = new List<string> { "body", "main" },
            Preview = new CharacterPackagePreviewMetadata
            {
                ThumbnailResourceKey = "char.test.preview.body",
                PreviewMeshResourceKey = "char.test.model.body"
            }
        });

        AuthoringResourceCollection collection = CharacterPackageAuthoringResourceProvider.FromPackageResourceCatalog(
            catalog,
            new AuthoringResourceProviderContext
            {
                ScopeId = "sample.character.test",
                PackageId = "test",
                PackagePath = "samples/test"
            });

        Require(collection.ScopeId == "sample.character.test", "scope id should be preserved.");
        Require(collection.Providers.Count == 1 && collection.Providers[0].ProviderId == AuthoringResourceProviderIds.CharacterPackage, "character package provider should be declared.");
        Require(collection.Items.Count == 1, "package catalog should project one resource item.");

        AuthoringResourceItem item = collection.Items[0];
        Require(item.ResourceId == "characterPackage:charpkg.test.resource.model.body", "resourceId should be provider-qualified stable id.");
        Require(item.StableId == "charpkg.test.resource.model.body", "stable id should be preserved.");
        Require(item.SourceProviderId == AuthoringResourceProviderIds.CharacterPackage, "source provider should be characterPackage.");
        Require(item.BindingKind == AuthoringResourceBindingKind.PackageResource, "package resources should use provider-local binding.");
        Require(item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.Unknown, "package resources should not be marked runtime-ready before compiler/runtime catalog mapping.");
        Require(item.ProviderBindings.Count == 2, "provider-local key and package path bindings should be present.");
        Require(item.ProviderBindings[0].BindingKeyKind == AuthoringResourceBindingKeyKinds.PackageResourceKey, "primary binding should identify package-local key.");
        Require(item.ProviderBindings[0].IsPrimary, "package-local key binding should be primary.");
        Require(item.ProviderBindings[0].ProviderResourceKey == "char.test.model.body", "provider-local resource key should be preserved.");
        Require(item.ProviderBindings[0].PackageResourceKey == "char.test.model.body", "package resource key should be provider-local.");
        Require(string.IsNullOrEmpty(item.ProviderBindings[0].RuntimeResourceKey), "package-local key must not be copied into runtime resource key.");
        Require(item.ProviderBindings[1].BindingKeyKind == AuthoringResourceBindingKeyKinds.PackageRelativePath, "source path should use its own binding key kind.");
        Require(item.ProviderBindings[1].ExternalSourcePath == "resources/models/body.glb", "source path should be retained as provider data.");
        Require(item.Preview.ThumbnailProviderResourceKey == "char.test.preview.body", "preview provider key should be retained.");

        AuthoringResourceCollection fromInterface = new CharacterPackageAuthoringResourceProvider().BuildResourceCollection(
            new AuthoringResourceProviderContext
            {
                ScopeId = "sample.character.test",
                PackageId = "test",
                PackageResourceCatalog = catalog
            });
        Require(fromInterface.Items.Count == 1, "provider interface should build from injected package catalog.");
    }

    private static void CharacterPackageProvider_ReportsDuplicateStableIdsAndProviderKeys()
    {
        var catalog = new CharacterPackageResourceCatalog();
        for (int i = 0; i < 2; i++)
        {
            catalog.Entries.Add(new CharacterPackageResourceEntry
            {
                ResourceKey = "char.test.model.body",
                LocalId = "model.body." + i,
                StableId = "charpkg.test.resource.model.body",
                TypeId = CharacterPackageResourceTypeIds.Model,
                Usage = CharacterPackageResourceUsageIds.CharacterModel,
                PackageId = "test"
            });
        }

        AuthoringResourceCollection collection = CharacterPackageAuthoringResourceProvider.FromPackageResourceCatalog(
            catalog,
            new AuthoringResourceProviderContext { PackageId = "test" });

        Require(collection.Diagnostics.Count == 2, "duplicate stable id and package-local key should both be diagnosed.");
        Require(collection.Diagnostics.Exists(d => d.Code == AuthoringResourceDiagnosticCodes.StableIdDuplicate), "duplicate stable id diagnostic should be emitted.");
        Require(collection.Diagnostics.Exists(d => d.Code == AuthoringResourceDiagnosticCodes.ResourceKeyDuplicate), "duplicate package key diagnostic should be emitted.");
        Require(collection.Diagnostics.TrueForAll(d => !string.IsNullOrWhiteSpace(d.ResourceId)), "duplicate diagnostics should include resource id.");
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
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.SocketSchemaId), "tags"), "socket tags field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceRadius"), "weapon trace radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceEndSocketId"), "weapon trace end socket field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId), "gate"), "validation gate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId), "resolverVerificationPlan"), "compiler resolver verification plan field missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "glb"), "resource source format glb option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "fbx"), "resource source format fbx future option missing.");
        Require(HasEnumOption("character.importTargetPathPolicy", "generatedCharacterPackage"), "import target path policy option missing.");
        Require(HasEnumOption("character.bodyPartKind", "Bone"), "body part kind Bone option missing.");
        Require(HasEnumOption("character.poseParentKind", "Socket"), "pose parent Socket option missing.");
        Require(HasEnumOption("character.validationGate", "Unknown"), "validation gate Unknown option missing.");
        Require(HasEnumOption("character.validationGate", "Reserved1000"), "validation gate reserved option missing.");
        Require(HasEnumOption("character.compilerStatus", "ImportBlocked"), "compiler status ImportBlocked option missing.");
        Require(HasEnumOption("character.colliderShape", "Convex"), "reserved convex shape option missing.");
    }

    private static void BodyPartAuthoring_JsonRoundTrip_PreservesSkeletonBindingFields()
    {
        var geometry = new CharacterAuthoringGeometry();
        geometry.BodyParts.Add(new CharacterBodyPartAuthoring
        {
            PartId = "right_hand",
            DisplayName = "Right Hand",
            PartKind = CharacterAuthoringBodyPartKind.Bone,
            ParentPartId = "torso",
            BonePath = "Armature/Hips/Spine/RightHand",
            LocatorId = "bone.RightHand",
            DefaultHitZoneId = "hit.right_hand",
            ReactionGroupId = "reaction.humanoid.limb",
            Tags = new List<string> { "hand", "weapon" }
        });

        string json = JsonSerializer.Serialize(geometry, JsonOptions);
        CharacterAuthoringGeometry roundTrip = JsonSerializer.Deserialize<CharacterAuthoringGeometry>(json, JsonOptions);

        Require(roundTrip != null && roundTrip.BodyParts.Count == 1, "body part geometry should roundtrip.");
        CharacterBodyPartAuthoring part = roundTrip.BodyParts[0];
        Require(part.PartKind == CharacterAuthoringBodyPartKind.Bone, "body part kind should roundtrip.");
        Require(part.BonePath == "Armature/Hips/Spine/RightHand", "bonePath should roundtrip.");
        Require(part.LocatorId == "bone.RightHand", "locatorId should roundtrip.");
        Require(part.Tags.Count == 2 && part.Tags.Contains("weapon"), "body part tags should roundtrip.");
    }

    private static void SkeletonBindingValidation_ReportsBrokenReferences()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterBodyPartAuthoring torso = package.Geometry.BodyParts.Find(part => part.PartId == "torso") ?? package.Geometry.BodyParts[0];
        torso.PartKind = CharacterAuthoringBodyPartKind.Bone;
        torso.BonePath = string.Empty;
        torso.LocatorId = string.Empty;

        CharacterBodyColliderProfile collider = package.Geometry.Colliders[0];
        collider.Shape = CharacterColliderShape.Sphere;
        collider.Radius = 0f;
        package.Geometry.Colliders.Add(new CharacterBodyColliderProfile
        {
            ColliderId = collider.ColliderId,
            PartId = torso.PartId,
            HitZoneId = "hit.duplicate",
            Shape = CharacterColliderShape.Sphere,
            Radius = 0.2f
        });

        package.Geometry.Sockets.Add(new CharacterSocketProfile
        {
            SocketId = "brokenMirror",
            ParentPartId = torso.PartId,
            Usage = CharacterSocketUsage.Weapon,
            MirrorPairSocketId = "missing.socket"
        });

        package.Geometry.WeaponAttachments[0].TraceStartSocketId = "missing.trace.start";
        package.Geometry.WeaponAttachments[0].TraceRadius = -0.01f;

        CharacterAuthoringValidationReport report = CharacterResourcePackageValidator.Validate(package);

        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingBodyPartLocator, CharacterAuthoringValidationGate.SpawnBlocked), "missing representative bone/locator should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.DuplicateCollider, CharacterAuthoringValidationGate.ExportBlocked), "duplicate collider id should block export.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.InvalidColliderDimensions, CharacterAuthoringValidationGate.SpawnBlocked), "invalid collider dimensions should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingMirrorSocket, CharacterAuthoringValidationGate.SpawnBlocked), "missing mirror socket should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.MissingAttachmentTraceSocket, CharacterAuthoringValidationGate.SpawnBlocked), "missing trace socket should block spawn.");
        Require(HasValidationIssue(report, CharacterAuthoringValidationCodes.InvalidAttachmentTraceRadius, CharacterAuthoringValidationGate.SpawnBlocked), "negative trace radius should block spawn.");
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
        Require(result.UnityImportWritePlan.Writes.Exists(write => write.Kind == CharacterAuthoringCompilerWriteKinds.ResourceFile && write.SourcePath == "resources/models/skeleton.glb"), "write plan should include resource file copy target.");
        Require(result.ResolverVerificationPlan.Status == "Ready", "resolver verification plan should be ready.");
        Require(result.ResolverVerificationPlan.ExpectedResolverEntrypoint == "CharacterPackageResolver.Resolve", "resolver verification plan should name the runtime resolver.");
        Require(result.ResolverVerificationPlan.DefaultLoadoutStableId == "equip_loadout.iron_vanguard.sword_shield", "default loadout should be sword shield.");
        Require(result.ResolverVerificationPlan.ExpectedActiveEquipmentStateStableId == "equip_state.iron_vanguard.sword_shield", "default active equipment state should match sword shield.");
        Require(result.ResolverVerificationPlan.RequiredTables.Count >= 12, "resolver verification plan should enumerate all Character Application tables.");
        Require(result.ResolverVerificationPlan.KnownAbilityIds.Contains(900001), "resolver verification plan should include generated base ability ids.");
    }

    private static void IronVanguardSample_CompilesResourcePlanArtifacts()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });
        CharacterResourcePlanCompileResult second = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = LoadSample("character-iron-vanguard"),
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            ValidateResourceFiles = true,
            ValidateResourceHashes = true
        });

        Require(result.RuntimeResourceCatalog.Format == CharacterResourcePlanFormats.RuntimeResourceCatalog, "runtime resource catalog should declare character runtime catalog format.");
        Require(result.RuntimeResourceCatalog.SchemaVersion == 1, "runtime resource catalog should use ResourceCatalog schemaVersion 1.");
        Require(result.RuntimeResourceCatalog.Entries.Count == 6, "Iron Vanguard runtime catalog should include the referenced ResourceManager-loadable entries.");
        Require(result.RuntimeResourceCatalog.Entries.Exists(entry =>
            entry.Id == "char.iron_vanguard.model.body" &&
            entry.Type == "GameObject" &&
            entry.Provider == "memory" &&
            entry.ProviderData["packageResourceKey"] == "char.iron_vanguard.model.body"), "body model should map to ResourceCatalogEntry-compatible GameObject entry.");
        Require(result.RuntimeResourceCatalog.Entries.Exists(entry =>
            entry.Id == "char.iron_vanguard.anim.locomotion" &&
            entry.Type == "AnimationClip"), "locomotion animation should map to AnimationClip runtime type.");
        Require(result.CharacterResourcePlan.SpawnCritical.Resources.Count == 1, "resource plan should include SpawnCritical body resource.");
        Require(result.CharacterResourcePlan.EquipmentInitial.Resources.Count == 2, "resource plan should include initial weapon equipment resources.");
        Require(result.CharacterResourcePlan.AnimationWarmup.Resources.Count == 2, "resource plan should include animation warmup resources.");
        Require(result.CharacterResourcePlan.UiDeferred.Resources.Count == 1, "resource plan should include deferred UI thumbnail resource.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Count == 0, "sample has no audio cues yet but should still expose Audio group.");
        Require(result.AudioCueManifest.Format == CharacterResourcePlanFormats.AudioCueManifest, "audio cue manifest should be emitted even when empty.");
        Require(result.ResourceValidationReport.Status == "Ready", "clean Iron Vanguard resource plan should be ready.");
        Require(!result.ResourceValidationReport.HasErrors, "clean Iron Vanguard resource plan should not have errors.");
        Require(result.ResourceValidationReport.GroupCounts.SpawnCritical == 1, "validation report should summarize SpawnCritical count.");
        Require(result.CharacterResourcePlan.PlanHash == second.CharacterResourcePlan.PlanHash, "resource plan hash should be deterministic.");
    }

    private static void CharacterResourcesPlanCommand_WritesRuntimePlanArtifacts()
    {
        string root = Path.Combine(FindRepoRoot(), "Temp", "MxFrameworkAuthoringTests", "issue280-resource-plan");
        ResetDirectory(root);

        int exit = CharacterPackageCommands.Dispatch(new[]
        {
            "character",
            "resources",
            "plan",
            "--package",
            FindSamplePath("character-iron-vanguard"),
            "--out",
            root,
            "--check-files",
            "--check-hashes"
        }, MxFramework.Authoring.Cli.Program.CreateJsonOptions());

        string runtimeCatalogPath = Path.Combine(root, "runtime_resource_catalog.json");
        string resourcePlanPath = Path.Combine(root, "character_resource_plan.json");
        string audioManifestPath = Path.Combine(root, "audio_cue_manifest.json");
        string validationReportPath = Path.Combine(root, "resource_validation_report.json");

        Require(exit == MxFramework.Authoring.Cli.Program.ExitReady, "resource plan command should return ready for Iron Vanguard.");
        Require(File.Exists(runtimeCatalogPath), "resource plan command should write runtime_resource_catalog.json.");
        Require(File.Exists(resourcePlanPath), "resource plan command should write character_resource_plan.json.");
        Require(File.Exists(audioManifestPath), "resource plan command should write audio_cue_manifest.json.");
        Require(File.Exists(validationReportPath), "resource plan command should write resource_validation_report.json.");

        string planJson = File.ReadAllText(resourcePlanPath);
        Require(planJson.Contains("\"spawnCritical\""), "character resource plan should contain SpawnCritical group.");
        Require(planJson.Contains("\"equipmentInitial\""), "character resource plan should contain EquipmentInitial group.");
        Require(planJson.Contains("\"animationWarmup\""), "character resource plan should contain AnimationWarmup group.");
        Require(planJson.Contains("\"uiDeferred\""), "character resource plan should contain UiDeferred group.");
        Require(planJson.Contains("\"audio\""), "character resource plan should contain Audio group.");

        ResourceValidationReportDocument report = JsonSerializer.Deserialize<ResourceValidationReportDocument>(File.ReadAllText(validationReportPath), JsonOptions);
        Require(report != null && report.Status == "Ready", "resource validation report should deserialize as Ready.");
        Require(report.GroupCounts.EquipmentInitial == 2 && report.GroupCounts.AnimationWarmup == 2, "resource validation report should include expected group counts.");
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

    private static void Compiler_SkeletonBindingsFlowToGeometryBindingAndHashes()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        CharacterBodyPartAuthoring handPart = package.Geometry.BodyParts.Find(part => part.PartId == "right_hand") ?? package.Geometry.BodyParts[0];
        handPart.PartKind = CharacterAuthoringBodyPartKind.Bone;
        handPart.BonePath = "Armature/Hips/Spine/RightShoulder/RightArm/RightForeArm/RightHand";
        handPart.LocatorId = "bone.RightHand";
        handPart.Tags = new List<string> { "hand", "main" };

        CharacterBodyColliderProfile collider = package.Geometry.Colliders[0];
        collider.PartId = handPart.PartId;
        collider.LocalPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.01f, 0.02f, 0.03f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };

        CharacterSocketProfile mainHand = package.Geometry.Sockets.Find(socket => socket.SocketId == "mainHand") ?? package.Geometry.Sockets[0];
        mainHand.ParentPartId = handPart.PartId;
        mainHand.BonePath = handPart.BonePath;
        mainHand.LocatorPath = string.Empty;
        mainHand.LocalPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0.05f, 0.01f, 0.02f),
            EulerHint = new CharacterAuthoringVector3(0f, 90f, 0f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };
        mainHand.MirrorPairSocketId = "offHand";
        mainHand.Handedness = CharacterSocketHandedness.Right;
        mainHand.SideTag = CharacterSocketSideTag.Right;
        mainHand.Tags = new List<string> { "weapon", "main" };

        CharacterSocketProfile offHand = package.Geometry.Sockets.Find(socket => socket.SocketId == "offHand");
        if (offHand == null)
        {
            offHand = new CharacterSocketProfile
            {
                SocketId = "offHand",
                ParentPartId = handPart.PartId,
                BonePath = "Armature/Hips/Spine/LeftShoulder/LeftArm/LeftForeArm/LeftHand",
                Usage = CharacterSocketUsage.Weapon
            };
            package.Geometry.Sockets.Add(offHand);
        }

        WeaponAttachmentProfile attachment = package.Geometry.WeaponAttachments.Find(item => item.WeaponId == "weapon.iron_sword") ?? package.Geometry.WeaponAttachments[0];
        attachment.AttachSocketId = "mainHand";
        attachment.LocalGripPose = new CharacterAuthoringLocalPose
        {
            Position = new CharacterAuthoringVector3(0f, -0.02f, 0.01f),
            Scale = new CharacterAuthoringVector3(1f, 1f, 1f)
        };
        attachment.TraceId = "trace.iron_sword.blade";
        attachment.TraceStartSocketId = "mainHand";
        attachment.TraceEndSocketId = "offHand";
        attachment.TraceRadius = 0.123f;
        attachment.TraceSampleRule = WeaponTraceSampleRule.FixedSamples;

        CharacterAuthoringCompileResult baseline = Compile(package);
        CharacterBodyPartBinding partBinding = baseline.GeometryBinding.BodyParts.Find(part => part.PartId == handPart.PartId);
        CharacterBodyColliderBinding colliderBinding = baseline.GeometryBinding.BodyColliders.Find(item => item.ColliderId == collider.ColliderId);
        CharacterSocketBinding socketBinding = baseline.GeometryBinding.Sockets.Find(socket => socket.SocketId == "mainHand");
        CharacterWeaponAttachmentBinding attachmentBinding = baseline.GeometryBinding.WeaponAttachments.Find(item => item.WeaponId == attachment.WeaponId);

        Require(partBinding != null && partBinding.BonePath == handPart.BonePath && partBinding.Tags.Contains("main"), "geometry binding should carry body part bone binding.");
        Require(colliderBinding != null && colliderBinding.LocalPose.ParentKind == CharacterPoseParentKind.BodyPart && colliderBinding.LocalPose.ParentPath == handPart.PartId, "collider local pose should default to its body part parent.");
        Require(socketBinding != null && socketBinding.LocalPose.ParentKind == CharacterPoseParentKind.Bone && socketBinding.LocalPose.ParentPath == handPart.BonePath, "socket local pose should default to bone parent.");
        Require(socketBinding.MirrorPairSocketId == "offHand" && socketBinding.SideTag == CharacterSocketSideTag.Right && socketBinding.Tags.Contains("weapon"), "socket binding should carry mirror, side and tags.");
        Require(attachmentBinding != null && attachmentBinding.LocalGripPose.ParentKind == CharacterPoseParentKind.Socket && attachmentBinding.LocalGripPose.ParentPath == "mainHand", "weapon grip pose should default to attach socket parent.");
        Require(attachmentBinding.TraceEndSocketId == "offHand" && attachmentBinding.TraceRadius == 0.123f && attachmentBinding.TraceSampleRule == WeaponTraceSampleRule.FixedSamples, "weapon attachment trace overrides should flow to geometry binding.");
        Require(baseline.SourceMappings.Exists(mapping => mapping.TargetPath == "sockets/mainHand" && mapping.TargetField == "socket"), "source mappings should include socket geometry binding targets.");

        mainHand.SideTag = CharacterSocketSideTag.Left;
        CharacterAuthoringCompileResult changed = Compile(package);

        Require(baseline.Hashes.SourcePackageHash != changed.Hashes.SourcePackageHash, "socket side tag should affect source package hash.");
        Require(baseline.Hashes.GeometryBindingHash != changed.Hashes.GeometryBindingHash, "socket side tag should affect geometry binding hash.");
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
        Require(File.Exists(Path.Combine(targetRoot, "resources", "models", "skeleton.glb")), "Unity import should copy model resource.");
        Require(File.Exists(Path.Combine(targetRoot, "generated", "character_application_config_patch.json")), "Unity import should write compiler config patch target.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "geometry_binding.json")), "Unity import should write readable config geometry binding alias.");
        Require(File.Exists(catalogPath), "Unity import should write project ResourceCatalog JSON.");

        CharacterUnityImportReport firstReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(firstReport != null && firstReport.Status == CharacterAuthoringCompilerStatus.Ready.ToString(), "first import report should be Ready.");
        Require(firstReport.AddedCount > 0, "first import should add files.");
        string catalogJson = File.ReadAllText(catalogPath);
        Require(catalogJson.Contains("\"format\": \"mx.characterUnityResourceCatalog.v1\""), "generated Unity ResourceCatalog should declare C2 catalog format.");
        Require(catalogJson.Contains("\"packageResourceKey\": \"char.iron_vanguard.model.body\""), "generated Unity ResourceCatalog should expose package resource key as a top-level ledger field.");
        Require(catalogJson.Contains("\"stableId\": \"charpkg.iron_vanguard.resource.model.body\""), "generated Unity ResourceCatalog should expose stable id as a top-level ledger field.");
        Require(catalogJson.Contains("\"sourceRelativePath\": \"resources/models/skeleton.glb\""), "generated Unity ResourceCatalog should expose source relative path.");
        Require(catalogJson.Contains("\"contentHash\": \"sha256:"), "generated Unity ResourceCatalog should expose resolved source content hash.");
        Require(catalogJson.Contains("\"importHash\": \"sha256:"), "generated Unity ResourceCatalog should expose import hash.");
        Require(catalogJson.Contains("\"diagnostics\": []"), "clean import-ready resources should have empty structured diagnostics.");
        Require(catalogJson.Contains("\"orphanedUnityAssets\": []"), "generated Unity ResourceCatalog should expose deterministic orphan list.");
        Require(catalogJson.Contains("\"provider\": \"memory\""), "generated Unity ResourceCatalog should use v1 memory provider bridge.");
        Require(catalogJson.Contains("\"assetPath\": \"Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard/resources/models/skeleton.glb\""), "generated ResourceCatalog should preserve Unity assetPath mapping.");
        Require(catalogJson.Contains("\"unityAssetPath\": \"Assets/MxFrameworkGenerated/CharacterPackages/iron_vanguard/resources/models/skeleton.glb\""), "generated ResourceCatalog should expose Unity asset path for editor resolution.");
        Require(catalogJson.Contains("\"unityAssetGuid\": \"\""), "CLI-generated ResourceCatalog should leave Unity GUID empty until AssetDatabase enrichment.");
        Require(catalogJson.Contains("\"unityMainObjectType\": \"\""), "CLI-generated ResourceCatalog should leave Unity main object type empty until AssetDatabase enrichment.");
        Require(catalogJson.Contains("\"importerKind\": \"unity-gltf\""), "generated ResourceCatalog should record expected Unity importer kind.");
        Require(catalogJson.Contains("\"importStatus\": \"PendingUnityImport\""), "CLI-generated ResourceCatalog should mark entries pending Unity AssetDatabase import.");

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
        File.Delete(Path.Combine(packageCopy, "resources", "models", "skeleton.glb"));

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
