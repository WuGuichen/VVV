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
        UnityAssetDatabaseProvider_ProjectsSnapshotAndUnavailableState();
        UnityProjectAssetProvider_DiscoversAnimationAssets();
        RuntimeCatalogProvider_ProjectsRuntimeReadyEntries();
        AuthoringResourceCollectionMerger_EnrichesRuntimeAnimationWithUnityAssetLink();
        FmodAudioLibraryProvider_ProjectsEventsAndUnavailableState();
        ExternalImportStagingProvider_FiltersFolderEntriesAndDetectsDuplicates();
        AuthoringResourceSelectionContracts_JsonRoundTrip();
        CharacterAnimationAuthoringSummary_JsonRoundTrip_PreservesSlots();
        AnimationAuthoringPackage_JsonRoundTrip_PreservesAnimationEditorContracts();
        AnimationAuthoringCompiler_EmitsRuntimePlanArtifacts();
        AnimationAuthoringResourceFieldSpecs_FilterAndResolveContracts();
        EditorServer_AnimationPackageApi_IsFileBackedAndValidatesShallowDraft();
        EditorServer_AnimationPreview_IsCompilerBackedAndResolvesResources();
        AuthoringResourceSelectionService_FiltersAndResolvesFieldSpecs();
        AuthoringResourceSelectionService_SourceClipAndFmodMultiBinding_DoNotDriftResolution();
        AuthoringResourceReferenceGraph_ScansCrossConsumerReferencesAndDiagnostics();
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
        CharacterResourcePlanCompiler_FmodAudioCueDoesNotEnterRuntimeCatalog();
        CharacterResourcePlanCompiler_ResolvesAuthoringSelectionManifest();
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

    private static void UnityAssetDatabaseProvider_ProjectsSnapshotAndUnavailableState()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-authoring-unity-provider-" + Guid.NewGuid().ToString("N"));
        string assetPath = Path.Combine(tempRoot, "Assets", "Characters", "body.prefab");
        string modelPath = Path.Combine(tempRoot, "Assets", "Characters", "hero.fbx");
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
        File.WriteAllText(assetPath, "prefab");
        File.WriteAllText(modelPath, "fbx");

        var snapshot = new AuthoringUnityResourceCatalogDocument
        {
            CatalogId = "unity.test",
            PackageId = "test"
        };
        snapshot.Entries.Add(new AuthoringUnityResourceCatalogEntry
        {
            Id = "char.test.model.body",
            Type = "GameObject",
            PackageId = "test",
            PackageResourceKey = "char.test.model.body",
            StableId = "charpkg.test.resource.model.body",
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            UnityAssetGuid = "guid-body",
            UnityAssetPath = "Assets/Characters/body.prefab",
            UnityMainObjectType = "GameObject",
            ImporterKind = "PrefabImporter",
            ImportStatus = "Imported",
            Labels = new List<string> { "character.model" },
            ProviderData = new Dictionary<string, string> { { "sourceFormat", "prefab" } }
        });
        snapshot.Entries.Add(new AuthoringUnityResourceCatalogEntry
        {
            Id = "char.test.texture.icon",
            Type = "Texture2D",
            PackageResourceKey = "char.test.texture.icon",
            StableId = "charpkg.test.resource.texture.icon",
            Usage = CharacterPackageResourceUsageIds.PreviewThumbnail,
            UnityAssetPath = "Assets/Characters/missing.png",
            ImportStatus = "Imported"
        });
        snapshot.Entries.Add(new AuthoringUnityResourceCatalogEntry
        {
            Id = "unity.anim.standing_idle",
            Type = "AnimationClip",
            StableId = "unity.anim.standing_idle",
            Usage = CharacterPackageResourceUsageIds.AnimationClipGroup,
            UnityAssetGuid = "guid-fbx",
            UnityAssetPath = "Assets/Characters/body.prefab",
            UnityMainObjectType = "GameObject",
            ImporterKind = "ModelImporter",
            ImportStatus = "Imported",
            Labels = new List<string> { "character.animation" },
            ProviderData = new Dictionary<string, string>
            {
                { "unitySubAssetKey", "guid-fbx:12345" },
                { "unityObjectType", "AnimationClip" },
                { "unityObjectName", "standing_idle" },
                { "unityLocalFileId", "12345" },
                { "parentUnityAssetPath", "Assets/Characters/body.prefab" }
            }
        });
        var modelEntry = new AuthoringUnityResourceCatalogEntry
        {
            Id = "unity.model.hero",
            Type = "GameObject",
            StableId = "unity.model.hero",
            Usage = CharacterPackageResourceUsageIds.PreviewMesh,
            UnityAssetGuid = "guid-hero-fbx",
            UnityAssetPath = "Assets/Characters/hero.fbx",
            UnityMainObjectType = "GameObject",
            ImporterKind = "ModelImporter",
            ImportStatus = "Imported",
            SourceFormat = "fbx",
            Labels = new List<string> { "character.model", "character.animation" }
        };
        modelEntry.SubAssets.Add(new AuthoringUnityResourceCatalogSubAsset
        {
            SubAssetId = "walk-forward",
            SubAssetName = "Walk Forward",
            SubAssetType = "AnimationClip",
            UnitySubAssetKey = "guid-hero-fbx:walk-forward",
            UnityLocalFileId = "7400000",
            DurationSeconds = 1.25f,
            LoopTime = true,
            HumanMotion = true
        });
        snapshot.Entries.Add(modelEntry);

        try
        {
            AuthoringResourceCollection collection = UnityAssetDatabaseAuthoringResourceProvider.FromUnityResourceCatalog(
                snapshot,
                new AuthoringResourceProviderContext
                {
                    ScopeId = "test",
                    PackageId = "test",
                    ProjectRootPath = tempRoot,
                    UnityResourceCatalogPath = "Assets/MxFrameworkGenerated/test/config/unity_resource_catalog.json"
                });

            Require(collection.Providers.Count == 1 && collection.Providers[0].Available, "Unity provider should be available with a snapshot.");
            AuthoringResourceItem body = collection.Items.Find(item => item.Metadata.ContainsKey("packageResourceKey") && item.Metadata["packageResourceKey"] == "char.test.model.body");
            Require(body != null, "Unity body item should be projected.");
            Require(body.SourceProviderId == AuthoringResourceProviderIds.UnityAssetDatabase, "Unity item should use Unity provider.");
            Require(body.BindingKind == AuthoringResourceBindingKind.UnityAsset, "Unity item should use Unity asset binding.");
            Require(body.RuntimeAvailability == AuthoringResourceRuntimeAvailability.EditorOnly, "Unity snapshot item should remain editor-only by default.");
            Require(body.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.UnityGuid && binding.UnityGuid == "guid-body"), "Unity GUID should be a provider binding.");
            Require(body.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.PackageResourceKey && binding.PackageResourceKey == "char.test.model.body"), "Unity item should preserve package resource key mapping.");

            AuthoringResourceItem missing = collection.Items.Find(item => item.Metadata.ContainsKey("packageResourceKey") && item.Metadata["packageResourceKey"] == "char.test.texture.icon");
            Require(missing != null && missing.ImportStatus == AuthoringResourceImportStatus.UnityMissing, "missing Unity asset should be marked UnityMissing.");
            Require(missing.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.UnityAssetMissing), "missing Unity asset should emit a diagnostic.");

            AuthoringResourceItem clip = collection.Items.Find(item => item.StableId == "unity.unity.anim.standing_idle");
            Require(clip != null, "Unity animation sub-asset should be projected.");
            Require(clip.DisplayName == "standing_idle", "Unity animation sub-asset should use object name as display name.");
            Require(clip.Kind == CharacterPackageResourceTypeIds.Animation, "Unity AnimationClip should map to animation kind.");
            Require(clip.Usage == AnimationAuthoringResourceUsages.AnimationClip, "Unity AnimationClip entries should normalize to animationClip usage.");
            Require(clip.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.UnitySubAssetKey && binding.ProviderResourceKey == "guid-fbx:12345"), "Unity animation sub-asset should use unique sub-asset key binding.");
            Require(clip.Metadata["unityLocalFileId"] == "12345", "Unity animation sub-asset should preserve local file id metadata.");
            Require(clip.Metadata["clipName"] == "standing_idle", "Unity animation sub-asset should expose clipName metadata.");
            Require(clip.Metadata["subClipName"] == "standing_idle", "Unity animation sub-asset should expose subClipName metadata.");
            Require(clip.Metadata["subClipId"] == "guid-fbx:12345", "Unity animation sub-asset should expose subClipId metadata.");
            Require(clip.Metadata["preloadPolicy"] == AuthoringResourcePreloadPolicies.AnimationWarmup, "Unity animation sub-asset should expose animation warmup metadata.");
            Require(clip.Metadata["runtimeAvailability"] == AuthoringResourceRuntimeAvailability.EditorOnly.ToString(), "Unity animation sub-asset should expose editor-only loadability metadata.");
            AuthoringResourceProviderBinding clipBinding = clip.ProviderBindings.Find(binding => binding.IsPrimary);
            Require(clipBinding != null && clipBinding.ProviderData["sourceFormat"] == "prefab", "Unity animation sub-asset binding should preserve sourceFormat metadata.");
            Require(clipBinding.ProviderData["subClipName"] == "standing_idle", "Unity animation sub-asset binding should expose picker subClipName metadata.");

            AuthoringResourceItem nestedSubClip = collection.Items.Find(item => item.Metadata.ContainsKey("unitySubAssetKey") && item.Metadata["unitySubAssetKey"] == "guid-hero-fbx:walk-forward");
            Require(nestedSubClip != null, "Unity model importer nested AnimationClip sub-asset should be projected.");
            Require(nestedSubClip.Kind == CharacterPackageResourceTypeIds.Animation && nestedSubClip.Usage == AnimationAuthoringResourceUsages.AnimationClip, "Unity model sub-clips should be animationClip resources.");
            Require(nestedSubClip.BindingKind == AuthoringResourceBindingKind.UnityAsset, "Unity model sub-clips should use Unity asset binding.");
            Require(nestedSubClip.RuntimeAvailability == AuthoringResourceRuntimeAvailability.EditorOnly, "Unity model sub-clips should remain editor-only until runtime catalog sync.");
            Require(nestedSubClip.Metadata["sourceFormat"] == "fbx", "Unity model sub-clips should preserve model source format.");
            Require(nestedSubClip.Metadata["clipName"] == "Walk Forward", "Unity model sub-clips should expose clipName metadata.");
            Require(nestedSubClip.Metadata["subClipName"] == "Walk Forward", "Unity model sub-clips should expose subClipName metadata.");
            Require(nestedSubClip.Metadata["subClipId"] == "walk-forward", "Unity model sub-clips should expose stable subClipId metadata.");
            Require(nestedSubClip.Metadata["loopTime"] == "true", "Unity model sub-clips should preserve loopTime metadata.");
            Require(nestedSubClip.Metadata["humanMotion"] == "true", "Unity model sub-clips should preserve humanMotion metadata.");
            AuthoringResourceProviderBinding nestedBinding = nestedSubClip.ProviderBindings.Find(binding => binding.IsPrimary);
            Require(nestedBinding != null && nestedBinding.ProviderData["subClipId"] == "walk-forward", "Unity model sub-clip binding metadata should include subClipId.");
            Require(nestedBinding.ProviderData["preloadPolicy"] == AuthoringResourcePreloadPolicies.AnimationWarmup, "Unity model sub-clip binding metadata should include preload policy.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        AuthoringResourceCollection unavailable = new UnityAssetDatabaseAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext());
        Require(unavailable.Providers.Count == 1 && !unavailable.Providers[0].Available, "Unity provider should expose unavailable state without a snapshot.");
        Require(unavailable.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ProviderUnavailable), "unavailable Unity provider should emit a diagnostic.");
    }

    private static void UnityProjectAssetProvider_DiscoversAnimationAssets()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-authoring-project-assets-" + Guid.NewGuid().ToString("N"));
        string animationRoot = Path.Combine(tempRoot, "Assets", "Art", "Characters", "Skeleton", "AnimationClips");
        string modelRoot = Path.Combine(tempRoot, "Assets", "Art", "Characters", "Skeleton", "Models");
        string generatedRoot = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "CharacterPackages", "test", "resources", "animations");
        Directory.CreateDirectory(animationRoot);
        Directory.CreateDirectory(modelRoot);
        Directory.CreateDirectory(generatedRoot);
        File.WriteAllText(Path.Combine(animationRoot, "standing_idle.anim"), "anim");
        File.WriteAllText(Path.Combine(animationRoot, "standing_idle.anim.meta"), "meta");
        File.WriteAllText(Path.Combine(animationRoot, "Standing Run Forward.fbx"), "fbx");
        File.WriteAllText(Path.Combine(animationRoot, "Standing Run Forward.fbx.meta"), "meta");
        File.WriteAllText(Path.Combine(animationRoot, "Standing Run Forward.glb"), "glb");
        File.WriteAllText(Path.Combine(modelRoot, "Skeleton.fbx"), "model");
        File.WriteAllText(Path.Combine(generatedRoot, "placeholder.glb"), "generated");

        try
        {
            AuthoringResourceCollection collection = new UnityProjectAssetAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                PackageId = "test",
                ProjectRootPath = tempRoot
            });

            Require(collection.Providers.Count == 1 && collection.Providers[0].ProviderId == AuthoringResourceProviderIds.UnityProjectAssets, "Unity project asset provider should be declared.");
            Require(collection.Providers[0].Available, "Unity project asset provider should be available when Assets exists.");
            Require(collection.Items.Count == 2, "Unity project asset provider should discover animation clips and animation containers only.");

            AuthoringResourceItem idle = collection.Items.Find(item => item.Metadata.ContainsKey("unityAssetPath") && item.Metadata["unityAssetPath"].EndsWith("standing_idle.anim", StringComparison.Ordinal));
            Require(idle != null, "direct .anim clip should be discovered.");
            Require(idle.Kind == CharacterPackageResourceTypeIds.Animation && idle.Usage == "animationClip", ".anim clip should be exposed as an animationClip resource.");
            Require(idle.SourceProviderId == AuthoringResourceProviderIds.UnityProjectAssets, ".anim clip should use Unity project asset provider.");
            Require(idle.BindingKind == AuthoringResourceBindingKind.UnityEditorOnlyAsset, ".anim clip should be editor-only until compiled into runtime catalogs.");
            Require(idle.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.UnityAssetPath && binding.UnityAssetPath.EndsWith("standing_idle.anim", StringComparison.Ordinal)), ".anim clip should expose Unity asset path binding.");
            Require(idle.Metadata["sourceFormat"] == "anim", ".anim clip should expose sourceFormat metadata.");
            Require(idle.Metadata["clipName"] == "standing_idle", ".anim clip should expose clipName metadata.");
            Require(idle.Metadata["subClipName"] == "standing_idle", ".anim clip should expose subClipName metadata for picker defaults.");
            Require(idle.Metadata["subClipId"] == "standing_idle", ".anim clip should expose subClipId metadata for picker defaults.");
            Require(idle.Metadata["preloadPolicy"] == AuthoringResourcePreloadPolicies.AnimationWarmup, ".anim clip should expose animation warmup metadata.");
            Require(idle.Metadata["runtimeAvailability"] == AuthoringResourceRuntimeAvailability.EditorOnly.ToString(), ".anim clip should expose editor-only loadability metadata.");
            AuthoringResourceProviderBinding idleBinding = idle.ProviderBindings.Find(binding => binding.IsPrimary);
            Require(idleBinding != null && idleBinding.ProviderData["sourceFormat"] == "anim", ".anim provider binding should expose sourceFormat metadata.");
            Require(idleBinding.ProviderData["subClipId"] == "standing_idle", ".anim provider binding should expose subClipId metadata.");

            AuthoringResourceItem fbx = collection.Items.Find(item => item.Metadata.ContainsKey("unityAssetPath") && item.Metadata["unityAssetPath"].EndsWith("Standing Run Forward.fbx", StringComparison.Ordinal));
            Require(fbx == null, "FBX files should not be exposed as animationClipGroup resources.");
            Require(collection.Items.TrueForAll(item => !item.Metadata["unityAssetPath"].EndsWith(".meta", StringComparison.OrdinalIgnoreCase)), ".meta files should not be counted as Unity project resources.");

            AuthoringResourceItem run = collection.Items.Find(item => item.Metadata.ContainsKey("unityAssetPath") && item.Metadata["unityAssetPath"].EndsWith("Standing Run Forward.glb", StringComparison.Ordinal));
            Require(run != null && run.Usage == CharacterPackageResourceUsageIds.AnimationClipGroup, "animation-folder GLB should be exposed as animationClipGroup.");
            Require(collection.Items.TrueForAll(item => !item.Metadata["unityAssetPath"].Contains("MxFrameworkGenerated")), "generated package animation files should not be duplicated as project assets.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void RuntimeCatalogProvider_ProjectsRuntimeReadyEntries()
    {
        var catalog = new RuntimeResourceCatalogDocument
        {
            CatalogId = "runtime.test",
            PackageId = "test"
        };
        catalog.Entries.Add(new RuntimeResourceCatalogEntryDocument
        {
            Id = "char.test.model.body",
            Type = "GameObject",
            PackageId = "test",
            Provider = "assetBundle",
            Address = "bundles/test/body.prefab",
            Labels = new List<string> { "spawnCritical" },
            Hash = "sha256:body",
            Size = 42,
            ProviderData = new Dictionary<string, string>
            {
                { "stableId", "charpkg.test.resource.model.body" },
                { "packageResourceKey", "char.test.model.body" },
                { "usage", CharacterPackageResourceUsageIds.CharacterModel },
                { "retainPolicy", "KeepAlive" }
            }
        });
        catalog.Entries.Add(new RuntimeResourceCatalogEntryDocument
        {
            Id = "char.test.anim.walk",
            Type = "AnimationClip",
            PackageId = "test",
            Provider = "assetBundle",
            Address = "bundles/test/walk.anim",
            Labels = new List<string> { "animationWarmup" },
            Hash = "sha256:walk",
            Size = 24,
            ProviderData = new Dictionary<string, string>
            {
                { "stableId", "charpkg.test.resource.anim.walk" },
                { "packageResourceKey", "char.test.anim.walk" },
                { "usage", CharacterPackageResourceUsageIds.AnimationClipGroup },
                { "sourceFormat", "anim" },
                { "clipName", "Walk" },
                { "subClipName", "Walk" },
                { "subClipId", "walk" },
                { "retainPolicy", "RetainUntilSceneUnload" }
            }
        });

        AuthoringResourceCollection collection = RuntimeCatalogAuthoringResourceProvider.FromRuntimeResourceCatalog(
            catalog,
            new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                PackageId = "test",
                RuntimeResourceCatalogPath = "Assets/MxFrameworkGenerated/test/config/runtime_resource_catalog.json"
            });

        Require(collection.Providers.Count == 1 && collection.Providers[0].Available, "runtime provider should be available with a catalog.");
        Require(collection.Items.Count == 2, "runtime catalog provider should project entries.");
        AuthoringResourceItem item = collection.Items.Find(candidate => candidate.Metadata.ContainsKey("packageResourceKey") && candidate.Metadata["packageResourceKey"] == "char.test.model.body");
        Require(item.SourceProviderId == AuthoringResourceProviderIds.RuntimeCatalog, "runtime item should use runtime catalog provider.");
        Require(item.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset, "runtime item should use ResourceManager binding.");
        Require(item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeReady, "runtime catalog item should be runtime-ready.");
        Require(item.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.RuntimeResourceKey && binding.RuntimeResourceKey == "char.test.model.body"), "runtime resource key binding should be explicit.");
        Require(item.ProviderBindings.Exists(binding => binding.BindingKeyKind == AuthoringResourceBindingKeyKinds.PackageResourceKey && binding.PackageResourceKey == "char.test.model.body"), "runtime item should preserve package key mapping.");
        Require(item.Metadata["retainPolicy"] == "KeepAlive", "runtime provider should preserve retain policy metadata.");

        AuthoringResourceItem runtimeClip = collection.Items.Find(candidate => candidate.StableId == "runtime.charpkg.test.resource.anim.walk");
        Require(runtimeClip != null, "runtime AnimationClip item should be projected.");
        Require(runtimeClip.Kind == CharacterPackageResourceTypeIds.Animation, "runtime AnimationClip should map to animation kind.");
        Require(runtimeClip.Usage == AnimationAuthoringResourceUsages.AnimationClip, "runtime AnimationClip should normalize to animationClip usage.");
        Require(runtimeClip.RuntimeAvailability == AuthoringResourceRuntimeAvailability.RuntimeReady, "runtime AnimationClip should be runtime-ready.");
        Require(runtimeClip.Metadata["clipName"] == "Walk", "runtime AnimationClip should expose clipName metadata.");
        Require(runtimeClip.Metadata["subClipName"] == "Walk", "runtime AnimationClip should expose subClipName metadata.");
        Require(runtimeClip.Metadata["subClipId"] == "walk", "runtime AnimationClip should expose subClipId metadata.");
        Require(runtimeClip.Metadata["sourceFormat"] == "anim", "runtime AnimationClip should expose sourceFormat metadata.");
        Require(runtimeClip.Metadata["preloadPolicy"] == AuthoringResourcePreloadPolicies.AnimationWarmup, "runtime AnimationClip should expose preloadPolicy metadata.");
        Require(runtimeClip.Metadata["runtimeAvailability"] == AuthoringResourceRuntimeAvailability.RuntimeReady.ToString(), "runtime AnimationClip should expose runtime-ready loadability metadata.");
        AuthoringResourceProviderBinding runtimeClipBinding = runtimeClip.ProviderBindings.Find(binding => binding.IsPrimary);
        Require(runtimeClipBinding != null && runtimeClipBinding.ProviderData["subClipId"] == "walk", "runtime AnimationClip binding should expose subClipId metadata.");
        Require(runtimeClipBinding.ProviderData["runtimeAvailability"] == AuthoringResourceRuntimeAvailability.RuntimeReady.ToString(), "runtime AnimationClip binding should expose runtime-ready metadata.");

        AuthoringResourceCollection unavailable = new RuntimeCatalogAuthoringResourceProvider().BuildResourceCollection(new AuthoringResourceProviderContext());
        Require(unavailable.Providers.Count == 1 && !unavailable.Providers[0].Available, "runtime provider should expose unavailable state without a catalog.");
        Require(unavailable.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ProviderUnavailable), "unavailable runtime provider should emit a diagnostic.");
    }

    private static void AuthoringResourceCollectionMerger_EnrichesRuntimeAnimationWithUnityAssetLink()
    {
        var runtime = new AuthoringResourceCollection();
        var runtimeItem = new AuthoringResourceItem
        {
            ResourceId = "runtime:standing_run_left",
            StableId = "runtime.art.character.skeleton.animation.standing_run_left",
            DisplayName = "standing_run_left",
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = AnimationAuthoringResourceUsages.AnimationClip,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady
        };
        runtimeItem.Metadata["runtimeResourceKey"] = "art.character.skeleton.animation.standing_run_left";
        runtimeItem.Metadata["clipName"] = "standing_run_left";
        runtimeItem.Metadata["subClipName"] = "standing_run_left";
        runtimeItem.Metadata["subClipId"] = "standing_run_left";
        runtimeItem.ProviderBindings.Add(new AuthoringResourceProviderBinding
        {
            ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
            IsPrimary = true,
            ProviderResourceKey = "art.character.skeleton.animation.standing_run_left",
            RuntimeResourceKey = "art.character.skeleton.animation.standing_run_left",
            ProviderData = new Dictionary<string, string>()
        });
        runtime.Items.Add(runtimeItem);

        var unity = new AuthoringResourceCollection();
        var unityItem = new AuthoringResourceItem
        {
            ResourceId = "unity:standing_run_left",
            StableId = "unity.project.assets.art.mxframework.samples.characters.skeleton.animationclips.standing_run_left.anim",
            DisplayName = "standing_run_left",
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = AnimationAuthoringResourceUsages.AnimationClip,
            SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly
        };
        unityItem.Metadata["clipName"] = "standing_run_left";
        unityItem.ProviderBindings.Add(new AuthoringResourceProviderBinding
        {
            ProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
            BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
            IsPrimary = true,
            ProviderResourceKey = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim",
            UnityAssetPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim"
        });
        unity.Items.Add(unityItem);

        AuthoringResourceCollection merged = AuthoringResourceCollectionMerger.Merge(runtime, unity);
        AuthoringResourceItem mergedRuntime = merged.Items.Find(item => item.ResourceId == "runtime:standing_run_left");

        Require(mergedRuntime != null, "merged collection should preserve runtime item.");
        Require(mergedRuntime.ProviderBindings.Count > 0, "merged runtime item should still have bindings.");
        Require(
            mergedRuntime.ProviderBindings[0].UnityAssetPath == "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim",
            "runtime animation binding should be enriched with matching Unity asset path.");
        Require(
            mergedRuntime.Metadata.TryGetValue("unityAssetPath", out string linkedPath) &&
            linkedPath == "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_left.anim",
            "runtime animation metadata should expose the enriched Unity asset path.");
    }

    private static void FmodAudioLibraryProvider_ProjectsEventsAndUnavailableState()
    {
        var snapshot = new AuthoringFmodAudioLibrarySnapshotDocument
        {
            Source = "unit-test",
            GeneratedAtUtc = "2026-05-20T08:00:00Z",
            CacheTimeUtc = "2026-05-20T07:59:00Z",
            CacheValid = true
        };
        snapshot.Banks.Add(new AuthoringFmodAudioLibraryBank
        {
            Name = "Character",
            Path = "/Banks/Character.bank",
            StudioPath = "bank:/Character"
        });
        var audioEvent = new AuthoringFmodAudioLibraryEvent
        {
            Path = "event:/Character/IronVanguard/SwordSlash",
            Guid = "{11111111-2222-3333-4444-555555555555}",
            Kind = "Event",
            Is3D = true,
            IsLoop = false,
            LengthMs = 850,
            MaxDistance = 25f
        };
        audioEvent.Banks.Add("Character");
        audioEvent.Parameters.Add(new AuthoringFmodAudioLibraryParameter
        {
            Name = "Impact",
            Kind = "Labeled",
            IdData1 = 17,
            IdData2 = 23
        });
        snapshot.Events.Add(audioEvent);
        snapshot.Diagnostics.Add(new AuthoringFmodAudioLibraryDiagnostic
        {
            Severity = "Warning",
            Code = "FMOD_CACHE_STALE",
            Message = "cache stale",
            SuggestedFix = "refresh banks"
        });

        AuthoringResourceCollection collection = FmodAudioLibraryAuthoringResourceProvider.FromSnapshot(
            snapshot,
            new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                PackageId = "test",
                FmodAudioLibrarySnapshotPath = "Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json"
            });

        Require(collection.Providers.Count == 1 && collection.Providers[0].ProviderId == AuthoringResourceProviderIds.Fmod, "FMOD provider should be declared.");
        Require(collection.Providers[0].Available, "stale FMOD provider should remain available.");
        Require(collection.Providers[0].Status == "Stale", "stale FMOD snapshot should be visible as provider status.");
        Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.FmodSnapshotStale), "stale FMOD snapshot should emit a diagnostic.");
        Require(collection.Items.Count == 1, "FMOD provider should project one audio event item.");

        AuthoringResourceItem item = collection.Items[0];
        Require(item.SourceProviderId == AuthoringResourceProviderIds.Fmod, "FMOD item should use FMOD provider.");
        Require(item.SourceKind == AuthoringResourceSourceKind.FmodLibrary, "FMOD item should use FmodLibrary source kind.");
        Require(item.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition, "FMOD event primary binding should be AudioEventDefinition.");
        Require(item.RuntimeAvailability == AuthoringResourceRuntimeAvailability.AudioCueOnly, "FMOD event should be audio-cue-only.");
        Require(item.ProviderBindings.Exists(binding => binding.BindingKind == AuthoringResourceBindingKind.AudioEventDefinition && binding.FmodEventGuid == "{11111111-2222-3333-4444-555555555555}"), "FMOD event definition binding should carry event guid.");
        Require(item.ProviderBindings.Exists(binding => binding.BindingKind == AuthoringResourceBindingKind.AudioCue && binding.ProviderData["audioCueId"].StartsWith("audio.cue.", StringComparison.Ordinal)), "FMOD event should expose an AudioCue bridge binding.");
        Require(item.ProviderBindings.TrueForAll(binding => string.IsNullOrEmpty(binding.RuntimeResourceKey)), "FMOD event must not gain a runtime resource key.");
        Require(item.Metadata["banks"] == "Character", "FMOD banks should be projected as metadata.");
        Require(item.Metadata["parameters"] == "Impact", "FMOD parameters should be projected as metadata.");

        var service = new AuthoringResourceSelectionService();
        AuthoringResourceSelectionResolutionResult cueResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "CombatAction.HitSfx",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            new AuthoringResourceConsumerContext { ConsumerKind = "combatAction" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = item.StableId,
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });
        Require(cueResult.Accepted, "FMOD audio cue selection should resolve.");
        Require(cueResult.Selection.AudioCueId.StartsWith("audio.cue.", StringComparison.Ordinal), "FMOD audio cue selection should output AudioCueId.");
        Require(string.IsNullOrEmpty(cueResult.Selection.RuntimeResourceKey), "FMOD audio cue selection must not output runtime resource key.");

        AuthoringResourceCollection unavailable = FmodAudioLibraryAuthoringResourceProvider.FromSnapshot(null, new AuthoringResourceProviderContext());
        Require(unavailable.Providers.Count == 1 && !unavailable.Providers[0].Available, "FMOD provider should expose unavailable state without a snapshot.");
        Require(unavailable.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.ProviderUnavailable), "unavailable FMOD provider should emit a provider diagnostic.");
    }

    private static void ExternalImportStagingProvider_FiltersFolderEntriesAndDetectsDuplicates()
    {
        byte[] bodyBytes = System.Text.Encoding.UTF8.GetBytes("body");
        string bodyHash = CharacterPackageHashUtility.ComputeSha256(bodyBytes);
        var catalog = new CharacterPackageResourceCatalog();
        catalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "char.test.model.existing",
            StableId = "charpkg.test.resource.model.existing",
            TypeId = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.PreviewMesh,
            Hash = bodyHash
        });

        var staging = new AuthoringExternalImportStagingDocument
        {
            SourceRootLabel = "unit-test-folder",
            MaxFileSizeBytes = 1024
        };
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "body.glb",
            RelativePath = "Characters/body.glb",
            SizeBytes = bodyBytes.Length,
            BytesBase64 = Convert.ToBase64String(bodyBytes)
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "body.glb.meta",
            RelativePath = "Characters/body.glb.meta",
            SizeBytes = 12
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "notes.txt",
            RelativePath = "Characters/notes.txt",
            SizeBytes = 3,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("txt"))
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "large.wav",
            RelativePath = "Audio/large.wav",
            SizeBytes = 2048,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("wav"))
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "icon.png",
            RelativePath = "Textures/icon.png",
            SizeBytes = 4,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("icon"))
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "Standing Run Forward.fbx",
            RelativePath = "Art/Animations/Standing Run Forward.fbx",
            SizeBytes = 6,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("runfbx"))
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "Standing Run Forward.fbx.meta",
            RelativePath = "Art/Animations/Standing Run Forward.fbx.meta",
            SizeBytes = 4
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "Standing Run Forward.glb",
            RelativePath = "Art/Animations/Standing Run Forward.glb",
            SizeBytes = 6,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("runglb"))
        });
        staging.Files.Add(new AuthoringExternalImportStagingFile
        {
            FileName = "Idle.anim",
            RelativePath = "Art/Clips/Idle.anim",
            SizeBytes = 4,
            BytesBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("anim"))
        });

        AuthoringResourceCollection collection = ExternalImportStagingAuthoringResourceProvider.FromStagingDocument(
            staging,
            new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                PackageId = "test",
                PackageResourceCatalog = catalog
            });

        Require(collection.Providers.Count == 1 && collection.Providers[0].ProviderId == AuthoringResourceProviderIds.ExternalImportStaging, "external staging provider should be declared.");
        Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.IgnoredImportFile), ".meta files should be ignored as diagnostics instead of primary resources.");
        Require(collection.Items.Count == 7, "hidden/meta file should not become a staged resource item.");

        AuthoringResourceItem duplicate = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Characters/body.glb");
        Require(duplicate != null && duplicate.ImportStatus == AuthoringResourceImportStatus.Conflict, "duplicate source hash should be staged as a conflict.");
        Require(duplicate.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.SourceHashDuplicate), "duplicate source hash should be diagnosed.");
        Require(duplicate.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable, "staged duplicate should not be runtime-loadable.");

        AuthoringResourceItem unsupported = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Characters/notes.txt");
        Require(unsupported != null && unsupported.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.UnsupportedFormat), "unsupported files should stay visible as diagnostics.");
        Require(unsupported.BindingKind == AuthoringResourceBindingKind.ExternalSource, "unsupported files should remain external source bindings.");

        AuthoringResourceItem tooLarge = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Audio/large.wav");
        Require(tooLarge != null && tooLarge.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.SourceFileTooLarge), "oversized files should be diagnosed.");

        AuthoringResourceItem icon = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Textures/icon.png");
        Require(icon != null && icon.ImportStatus == AuthoringResourceImportStatus.New, "supported unique files should be importable staged items.");
        Require(icon.Kind == CharacterPackageResourceTypeIds.Texture && icon.Usage == CharacterPackageResourceUsageIds.Texture, "texture import candidates should infer kind and usage.");
        Require(icon.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable, "staged items should remain not runtime-loadable until promoted.");
        Require(icon.Metadata["selectable"] == "true", "supported unique staged items should be selectable for promotion.");

        AuthoringResourceItem fbxCandidate = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Art/Animations/Standing Run Forward.fbx");
        Require(fbxCandidate != null && fbxCandidate.Kind == CharacterPackageResourceTypeIds.Model, "FBX staging candidates should stay model resources even under animation folders.");
        Require(fbxCandidate.Usage == CharacterPackageResourceUsageIds.PreviewMesh, "FBX staging candidates should not infer animationClipGroup usage.");
        Require(fbxCandidate.SourceKind == AuthoringResourceSourceKind.ExternalFile, "FBX staging candidates should remain external source files.");
        Require(fbxCandidate.BindingKind == AuthoringResourceBindingKind.ExternalSource, "FBX staging candidates should use external source binding.");
        Require(fbxCandidate.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable, "FBX staging candidates should not be runtime-ready clips.");
        Require(fbxCandidate.Metadata["sourceFormat"] == "fbx", "FBX staging candidates should expose sourceFormat metadata.");
        Require(fbxCandidate.ProviderBindings[0].ProviderData["runtimeAvailability"] == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable.ToString(), "FBX staging binding should expose not-runtime-loadable metadata.");

        AuthoringResourceItem animation = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Art/Animations/Standing Run Forward.glb");
        Require(animation != null && animation.Kind == CharacterPackageResourceTypeIds.Animation, "animation folder GLB should infer animation kind.");
        Require(animation.Usage == CharacterPackageResourceUsageIds.AnimationClipGroup, "animation folder GLB should infer animationClipGroup usage.");

        AuthoringResourceItem unityClip = collection.Items.Find(item => item.Metadata.ContainsKey("relativePath") && item.Metadata["relativePath"] == "Art/Clips/Idle.anim");
        Require(unityClip != null && unityClip.Kind == CharacterPackageResourceTypeIds.Animation, "Unity .anim files should infer animation kind.");
        Require(unityClip.Usage == AnimationAuthoringResourceUsages.AnimationClip, "Unity .anim files should infer animationClip usage.");
        Require(unityClip.RuntimeAvailability == AuthoringResourceRuntimeAvailability.NotRuntimeLoadable, "staged .anim files should not be runtime-ready before Unity import/runtime sync.");
        Require(unityClip.Metadata["clipName"] == "Idle", "staged .anim files should expose clipName metadata.");
        Require(unityClip.Metadata["subClipName"] == "Idle", "staged .anim files should expose subClipName metadata.");
        Require(unityClip.Metadata["subClipId"] == "Idle", "staged .anim files should expose subClipId metadata.");
        Require(unityClip.Metadata["sourceFormat"] == "anim", "staged .anim files should expose sourceFormat metadata.");
        Require(unityClip.Metadata["preloadPolicy"] == AuthoringResourcePreloadPolicies.AnimationWarmup, "staged .anim files should expose animation warmup metadata.");
        Require(unityClip.ProviderBindings[0].ProviderData["subClipId"] == "Idle", "staged .anim binding metadata should expose subClipId.");
    }

    private static void AuthoringResourceSelectionContracts_JsonRoundTrip()
    {
        var spec = new AuthoringResourceFieldSpec
        {
            FieldKey = "CombatAction.HitSfx",
            EditorKind = "CombatEditor",
            DisplayName = "Hit Sfx",
            AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
            AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
            AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
            AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
            PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
            OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
        };
        var context = new AuthoringResourceConsumerContext
        {
            ConsumerKind = "combatAction",
            ConsumerStableId = "combat.iron_vanguard.light_attack",
            ScopeId = "character.iron_vanguard",
            PackageId = "iron_vanguard",
            PackagePath = "Tools/MxFramework.Authoring/samples/character-iron-vanguard",
            WeaponClass = "sword"
        };
        var selection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "audio.iron_vanguard.hit",
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            AudioCueId = "cue.iron_vanguard.hit"
        };
        var reason = new AuthoringResourceSelectionReason
        {
            Code = AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable,
            Category = "runtime",
            Severity = CharacterAuthoringValidationSeverity.Error,
            BlocksSelection = true,
            FieldKey = spec.FieldKey,
            ResourceStableId = selection.ResourceStableId,
            ProviderId = selection.SourceProviderId,
            ExpectedValue = "RuntimeReady",
            ActualValue = "NotRuntimeLoadable",
            BindingKind = AuthoringResourceBindingKind.ExternalSource,
            BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
            Message = "not runtime loadable"
        };

        string json = JsonSerializer.Serialize(new AuthoringResourceSelectionResolutionResult
        {
            Accepted = false,
            Selection = selection,
            Reasons = new List<AuthoringResourceSelectionReason> { reason }
        }, JsonOptions);
        AuthoringResourceSelectionResolutionResult roundTrip = JsonSerializer.Deserialize<AuthoringResourceSelectionResolutionResult>(json, JsonOptions);
        string specJson = JsonSerializer.Serialize(spec, JsonOptions);
        AuthoringResourceFieldSpec specRoundTrip = JsonSerializer.Deserialize<AuthoringResourceFieldSpec>(specJson, JsonOptions);
        string contextJson = JsonSerializer.Serialize(context, JsonOptions);
        AuthoringResourceConsumerContext contextRoundTrip = JsonSerializer.Deserialize<AuthoringResourceConsumerContext>(contextJson, JsonOptions);

        Require(roundTrip != null && roundTrip.Selection.AudioCueId == "cue.iron_vanguard.hit", "authoring selection ref should roundtrip audio cue id.");
        Require(roundTrip.Reasons.Count == 1 && roundTrip.Reasons[0].BlocksSelection, "selection reason should roundtrip blocksSelection.");
        Require(specRoundTrip != null && specRoundTrip.AcceptedProviderIds[0] == AuthoringResourceProviderIds.Fmod, "field spec should roundtrip accepted provider ids.");
        Require(specRoundTrip.OutputKind == AuthoringResourceSelectionOutputKind.AudioCueId, "field spec should roundtrip output kind.");
        Require(contextRoundTrip != null && contextRoundTrip.ConsumerKind == "combatAction", "consumer context should roundtrip.");
    }

    private static void CharacterAnimationAuthoringSummary_JsonRoundTrip_PreservesSlots()
    {
        var summary = new CharacterApplicationAuthoringSummary
        {
            CharacterStableId = "char.iron_vanguard",
            AnimationGroups = new List<CharacterAnimationGroupAuthoringSummary>
            {
                new CharacterAnimationGroupAuthoringSummary
                {
                    GroupId = "anim.locomotion",
                    DisplayName = "Locomotion",
                    Description = "Idle / walk / run clip group.",
                    SourceResourceKey = "char.iron_vanguard.anim.locomotion",
                    AnimationPolicy = "splitByClipNames",
                    DefaultBlendSpaceId = "locomotion2d",
                    Clips = new List<CharacterAnimationClipAuthoringSummary>
                    {
                        new CharacterAnimationClipAuthoringSummary
                        {
                            ClipId = "idle",
                            SourceClipName = "Idle",
                            DisplayName = "Idle",
                            Loop = true,
                            RootMotionPolicy = "Ignore",
                            Speed = 1f
                        },
                        new CharacterAnimationClipAuthoringSummary
                        {
                            ClipId = "run",
                            SourceClipName = "Run Forward",
                            DisplayName = "Run Forward",
                            Loop = true,
                            RootMotionPolicy = "MotionDelta",
                            Speed = 1.2f
                        }
                    },
                    BlendSpaces = new List<CharacterAnimationBlendSpaceAuthoringSummary>
                    {
                        new CharacterAnimationBlendSpaceAuthoringSummary
                        {
                            BlendSpaceId = "locomotion2d",
                            DisplayName = "Locomotion 2D",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Clips = new List<CharacterAnimationBlendSpaceClipAuthoringSummary>
                            {
                                new CharacterAnimationBlendSpaceClipAuthoringSummary { ClipId = "idle", X = 0f, Y = 0f },
                                new CharacterAnimationBlendSpaceClipAuthoringSummary { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    }
                }
            },
            AnimationProfiles = new List<CharacterAnimationProfileAuthoringSummary>
            {
                new CharacterAnimationProfileAuthoringSummary
                {
                    ProfileId = "anim_profile.default",
                    DisplayName = "Default Animation Profile",
                    Slots = new List<CharacterAnimationSlotAuthoringSummary>
                    {
                        new CharacterAnimationSlotAuthoringSummary
                        {
                            SlotId = "locomotion",
                            DisplayName = "Locomotion",
                            Purpose = "idle walk run",
                            AnimationGroupId = "anim.locomotion",
                            ResourceKey = "char.iron_vanguard.anim.locomotion",
                            PreloadPolicy = AuthoringResourcePreloadPolicies.AnimationWarmup,
                            Required = true,
                            ResourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "charpkg.iron_vanguard.resource.anim.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
                                BindingKind = AuthoringResourceBindingKind.PackageResource,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                PackageResourceKey = "char.iron_vanguard.anim.locomotion"
                            }
                        }
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(summary, JsonOptions);
        CharacterApplicationAuthoringSummary roundTrip = JsonSerializer.Deserialize<CharacterApplicationAuthoringSummary>(json, JsonOptions);
        Require(json.Contains("\"animationGroups\""), "character application config should serialize animationGroups.");
        Require(json.Contains("\"animationProfiles\""), "character application config should serialize animationProfiles.");
        Require(roundTrip != null && roundTrip.AnimationGroups.Count == 1, "animation groups should roundtrip.");
        Require(roundTrip.AnimationGroups[0].Clips.Count == 2, "animation group clips should roundtrip.");
        Require(roundTrip.AnimationGroups[0].BlendSpaces.Count == 1, "animation group blend spaces should roundtrip.");
        Require(roundTrip.AnimationGroups[0].BlendSpaces[0].Clips[1].ClipId == "run", "animation blend space points should roundtrip.");
        Require(roundTrip != null && roundTrip.AnimationProfiles.Count == 1, "animation profiles should roundtrip.");
        Require(roundTrip.AnimationProfiles[0].Slots.Count == 1, "animation profile slots should roundtrip.");
        Require(roundTrip.AnimationProfiles[0].Slots[0].AnimationGroupId == "anim.locomotion", "animation slot group reference should roundtrip.");
        Require(roundTrip.AnimationProfiles[0].Slots[0].ResourceSelection.PackageResourceKey == "char.iron_vanguard.anim.locomotion", "animation slot ResourceSelectionRef should roundtrip.");
        Require(roundTrip.AnimationProfiles[0].Slots[0].PreloadPolicy == AuthoringResourcePreloadPolicies.AnimationWarmup, "animation slot preload policy should roundtrip.");
    }

    private static void AnimationAuthoringPackage_JsonRoundTrip_PreservesAnimationEditorContracts()
    {
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.iron_vanguard",
            StableId = "anim.iron_vanguard",
            DisplayName = "Iron Vanguard Animation",
            SkeletonProfileId = "skeleton.humanoid",
            AvatarProfileId = "avatar.iron_vanguard",
            Diagnostics = new List<AnimationAuthoringDiagnostic>
            {
                new AnimationAuthoringDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Gate = CharacterAuthoringValidationGate.WarningOnly,
                    Code = "ANIM_BLEND_POINT_MISSING_CLIP",
                    SourceObjectPath = "sets/base/groups/locomotion/blend2D/locomotion2d",
                    Field = "points[2].clipId",
                    SetId = "set.base",
                    GroupId = "group.locomotion",
                    BlendId = "locomotion2d",
                    Message = "Blend point references a missing local clip."
                }
            }
        };

        package.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            DisplayName = "Base Set",
            DefaultClipId = "idle",
            FallbackClipId = "idle",
            Layers = new List<AnimationLayerAuthoring>
            {
                new AnimationLayerAuthoring
                {
                    LayerId = "base",
                    DisplayName = "Base",
                    Weight = 1f,
                    RootMotionPolicy = "MotionDelta",
                    AvatarMaskSelection = new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "mask.full_body",
                        SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                        BindingKind = AuthoringResourceBindingKind.UnityAsset,
                        ExpectedKind = "avatarMask",
                        ExpectedUsage = "avatarMask",
                        UnityGuid = "mask-guid"
                    }
                }
            },
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.locomotion",
                    DisplayName = "Locomotion",
                    Usage = "locomotion",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            DisplayName = "Idle",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "anim.source.iron_vanguard.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                UnityGuid = "anim-guid",
                                UnityAssetPath = "Assets/Art/Characters/IronVanguard/locomotion.glb"
                            },
                            SourceSubClipId = "Take 001/Idle",
                            SourceClipName = "Idle",
                            RuntimeResourceKey = "char.iron_vanguard.anim.idle",
                            Loop = true,
                            Speed = 1f,
                            RootMotionPolicy = "Ignore",
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 0f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 1.2f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                }
                            },
                            Tags = new List<string> { "locomotion", "idle" },
                            GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef>
                            {
                                new AuthoringResourceSelectionRef
                                {
                                    ResourceStableId = "artifact.idle.bake",
                                    SourceProviderId = AuthoringResourceProviderIds.GeneratedAssets,
                                    BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                                    ExpectedKind = CharacterPackageResourceTypeIds.Config,
                                    ExpectedUsage = "animationBakeArtifact",
                                    ProviderResourceKey = "generated/idle_bake.json"
                                }
                            }
                        },
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "run",
                            DisplayName = "Run",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "anim.source.iron_vanguard.locomotion",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = CharacterPackageResourceUsageIds.AnimationClipGroup,
                                UnityGuid = "anim-guid",
                                UnityAssetPath = "Assets/Art/Characters/IronVanguard/locomotion.glb"
                            },
                            SourceSubClipId = "Take 001/Run",
                            SourceClipName = "Run Forward",
                            RuntimeResourceKey = "char.iron_vanguard.anim.run",
                            Loop = true,
                            Speed = 1.1f,
                            RootMotionPolicy = "MotionDelta",
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 2.2f,
                                PlaybackSpeed = 1.1f,
                                CycleDurationSeconds = 0.75f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.1f, EndNormalized = 0.35f, Confidence = 0.8f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.6f, EndNormalized = 0.85f, Confidence = 0.8f }
                                }
                            }
                        }
                    },
                    Blend1D = new List<AnimationBlend1DAuthoring>
                    {
                        new AnimationBlend1DAuthoring
                        {
                            BlendId = "locomotion_speed",
                            Parameter = "speed",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend1DPointAuthoring>
                            {
                                new AnimationBlend1DPointAuthoring { ClipId = "idle", Value = 0f },
                                new AnimationBlend1DPointAuthoring { ClipId = "run", Value = 1f }
                            }
                        }
                    },
                    Blend2D = new List<AnimationBlend2DAuthoring>
                    {
                        new AnimationBlend2DAuthoring
                        {
                            BlendId = "locomotion2d",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend2DPointAuthoring>
                            {
                                new AnimationBlend2DPointAuthoring { ClipId = "idle", X = 0f, Y = 0f },
                                new AnimationBlend2DPointAuthoring { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    },
                    Timelines = new List<AnimationTimelineAuthoring>
                    {
                        new AnimationTimelineAuthoring
                        {
                            TimelineId = "idle.events",
                            ClipId = "idle",
                            TimeDomain = "NormalizedTime",
                            Events = new List<AnimationTimelineEventAuthoring>
                            {
                                new AnimationTimelineEventAuthoring
                                {
                                    EventId = "idle.audio.breath",
                                    ClipId = "idle",
                                    TimeDomain = "NormalizedTime",
                                    Time = 0.5f,
                                    EventKind = "AudioCue",
                                    ResourceSelection = new AuthoringResourceSelectionRef
                                    {
                                        ResourceStableId = "audio.cue.breath",
                                        SourceProviderId = AuthoringResourceProviderIds.Fmod,
                                        BindingKind = AuthoringResourceBindingKind.AudioCue,
                                        ExpectedKind = CharacterPackageResourceTypeIds.Audio,
                                        ExpectedUsage = CharacterPackageResourceUsageIds.AudioCue,
                                        AudioCueId = "cue.iron_vanguard.breath",
                                        AudioEventDefinitionId = "event:/Character/IronVanguard/Breath"
                                    },
                                    PayloadJson = "{\"volume\":0.8}"
                                },
                                new AnimationTimelineEventAuthoring
                                {
                                    EventId = "idle.vfx.steam",
                                    ClipId = "idle",
                                    TimeDomain = "Seconds",
                                    Time = 0.2f,
                                    EventKind = "Vfx",
                                    ResourceSelection = new AuthoringResourceSelectionRef
                                    {
                                        ResourceStableId = "vfx.steam",
                                        SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                                        BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                                        ExpectedKind = CharacterPackageResourceTypeIds.Vfx,
                                        ExpectedUsage = CharacterPackageResourceUsageIds.VfxCue,
                                        RuntimeResourceKey = "vfx.iron_vanguard.steam"
                                    }
                                }
                            }
                        }
                    }
                }
            },
            ActionBindings = new List<AnimationActionBindingAuthoring>
            {
                new AnimationActionBindingAuthoring
                {
                    BindingId = "action.move",
                    ActionId = "character.move",
                    GroupId = "group.locomotion",
                    BlendId = "locomotion2d",
                    Required = true
                }
            },
            Compatibility = new AnimationCompatibilityExpectationAuthoring
            {
                CompatibilityId = "compat.humanoid",
                SkeletonProfileId = "skeleton.humanoid",
                AvatarProfileId = "avatar.iron_vanguard",
                CoordinateConvention = "UnityYUp",
                AllowRetargeting = true,
                CompatibilityProfileSelection = new AuthoringResourceSelectionRef
                {
                    ResourceStableId = "compat.profile.humanoid",
                    SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    ExpectedKind = CharacterPackageResourceTypeIds.Config,
                    ExpectedUsage = "animationCompatibilityProfile",
                    RuntimeResourceKey = "animation.compatibility.humanoid"
                },
                AvatarMaskSelection = new AuthoringResourceSelectionRef
                {
                    ResourceStableId = "mask.full_body",
                    SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    ExpectedKind = "avatarMask",
                    ExpectedUsage = "avatarMask",
                    UnityGuid = "mask-guid"
                },
                RequiredBoneIds = new List<string> { "hips", "spine" },
                RequiredSocketIds = new List<string> { "weapon.main" }
            },
            Warmup = new AnimationWarmupAuthoring
            {
                WarmupId = "warmup.base",
                RequiredClipIds = new List<string> { "idle", "run" },
                RequiredBlendIds = new List<string> { "locomotion2d" },
                AudioCueSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "audio.cue.breath",
                        SourceProviderId = AuthoringResourceProviderIds.Fmod,
                        BindingKind = AuthoringResourceBindingKind.AudioCue,
                        AudioCueId = "cue.iron_vanguard.breath"
                    }
                },
                VfxSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "vfx.steam",
                        SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                        BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                        RuntimeResourceKey = "vfx.iron_vanguard.steam"
                    }
                },
                GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef>
                {
                    new AuthoringResourceSelectionRef
                    {
                        ResourceStableId = "artifact.idle.bake",
                        SourceProviderId = AuthoringResourceProviderIds.GeneratedAssets,
                        BindingKind = AuthoringResourceBindingKind.GeneratedPreviewOnly,
                        ProviderResourceKey = "generated/idle_bake.json"
                    }
                }
            }
        });

        package.Profiles.Add(new AnimationAuthoringProfile
        {
            ProfileId = "profile.default",
            DisplayName = "Default",
            DefaultSetId = "set.base",
            DefaultGroupId = "group.locomotion",
            Slots = new List<AnimationProfileSlotAuthoring>
            {
                new AnimationProfileSlotAuthoring
                {
                    SlotId = "locomotion",
                    Purpose = "base movement",
                    SetId = "set.base",
                    GroupId = "group.locomotion",
                    DefaultClipId = "idle",
                    DefaultBlendId = "locomotion2d",
                    Required = true
                }
            }
        });

        string json = JsonSerializer.Serialize(package, JsonOptions);
        AnimationAuthoringPackage roundTrip = JsonSerializer.Deserialize<AnimationAuthoringPackage>(json, JsonOptions);

        Require(json.Contains("\"sourceSelection\""), "animation clip sourceSelection should serialize.");
        Require(json.Contains("\"sourceSubClipId\""), "animation clip sourceSubClipId should serialize.");
        Require(json.Contains("\"audioCueId\""), "timeline AudioCue ResourceSelectionRef should serialize.");
        Require(roundTrip != null && roundTrip.Sets.Count == 1, "animation set should roundtrip.");
        AnimationGroupAuthoring group = roundTrip.Sets[0].Groups[0];
        Require(group.Clips[0].SourceSelection.ResourceStableId == "anim.source.iron_vanguard.locomotion", "source selection should be the primary clip identity.");
        Require(group.Clips[0].SourceSubClipId == "Take 001/Idle", "source sub clip id should roundtrip.");
        Require(group.Clips[0].SourceClipName == "Idle", "source clip name should roundtrip as metadata.");
        Require(group.Clips[0].Calibration.CycleDurationSeconds == 1.2f, "clip calibration cycle duration should roundtrip.");
        Require(group.Clips[0].Calibration.LeftFootContactWindows.Count == 1, "left foot contact windows should roundtrip.");
        Require(group.Clips[1].Calibration.NativeVelocityY == 2.2f, "clip native velocity should roundtrip.");
        Require(group.Blend1D[0].Points[1].ClipId == "run", "1D blend points should reference local clip ids.");
        Require(group.Blend2D[0].Points[1].ClipId == "run", "2D blend points should reference local clip ids.");
        Require(group.Timelines[0].Events[0].ResourceSelection.BindingKind == AuthoringResourceBindingKind.AudioCue, "timeline audio event should use AudioCue ResourceSelectionRef.");
        Require(group.Timelines[0].Events[0].ResourceSelection.AudioCueId == "cue.iron_vanguard.breath", "timeline audio cue id should roundtrip.");
        Require(group.Timelines[0].Events[1].ResourceSelection.RuntimeResourceKey == "vfx.iron_vanguard.steam", "timeline VFX selection should roundtrip.");
        Require(roundTrip.Sets[0].Layers[0].AvatarMaskSelection.UnityGuid == "mask-guid", "AvatarMask selection should roundtrip.");
        Require(roundTrip.Sets[0].Compatibility.CompatibilityProfileSelection.RuntimeResourceKey == "animation.compatibility.humanoid", "compatibility profile selection should roundtrip.");
        Require(roundTrip.Sets[0].Compatibility.RequiredSocketIds[0] == "weapon.main", "compatibility socket expectation should roundtrip.");
        Require(roundTrip.Sets[0].Warmup.AudioCueSelections[0].AudioCueId == "cue.iron_vanguard.breath", "warmup AudioCue selection should roundtrip.");
        Require(roundTrip.Sets[0].Warmup.GeneratedArtifactSelections[0].ProviderResourceKey == "generated/idle_bake.json", "warmup generated artifact selection should roundtrip.");
        Require(roundTrip.Profiles[0].Slots[0].GroupId == "group.locomotion", "profile group reference should roundtrip.");
        Require(roundTrip.Diagnostics[0].Field == "points[2].clipId", "animation diagnostics should roundtrip.");
    }

    private static void AnimationAuthoringCompiler_EmitsRuntimePlanArtifacts()
    {
        var package = new AnimationAuthoringPackage
        {
            PackageId = "animation.test",
            StableId = "character.test.animation",
            DisplayName = "Test Animation",
            SkeletonProfileId = "skeleton.humanoid",
            AvatarProfileId = "avatar.test"
        };
        var idleSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.idle",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Animation,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
            RuntimeResourceKey = "runtime.anim.idle"
        };
        var runSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.run",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Animation,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
            RuntimeResourceKey = "runtime.anim.run"
        };
        var maskSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.mask.full_body",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = AnimationAuthoringResourceKinds.AvatarMask,
            ExpectedUsage = AnimationAuthoringResourceUsages.AvatarMask,
            RuntimeResourceKey = "runtime.mask.full_body"
        };
        var bakeSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.anim.idle.bake",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Config,
            ExpectedUsage = AnimationAuthoringResourceUsages.AnimationBakeArtifact,
            RuntimeResourceKey = "runtime.anim.idle.bake"
        };
        var vfxSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.vfx.footstep",
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ExpectedKind = CharacterPackageResourceTypeIds.Vfx,
            ExpectedUsage = CharacterPackageResourceUsageIds.VfxCue,
            RuntimeResourceKey = "runtime.vfx.footstep"
        };
        var audioSelection = new AuthoringResourceSelectionRef
        {
            ResourceStableId = "stable.audio.footstep",
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            ExpectedKind = CharacterPackageResourceTypeIds.Audio,
            ExpectedUsage = CharacterPackageResourceUsageIds.AudioCue,
            AudioCueId = "cue.test.footstep"
        };

        package.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            DisplayName = "Base",
            DefaultClipId = "idle",
            FallbackClipId = "run",
            Layers = new List<AnimationLayerAuthoring>
            {
                new AnimationLayerAuthoring { LayerId = "base", AvatarMaskSelection = maskSelection }
            },
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.locomotion",
                    Usage = "locomotion",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            DisplayName = "Idle",
                            SourceSubClipId = "Idle",
                            SourceClipName = "Idle",
                            SourceSelection = idleSelection,
                            Loop = true,
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 0f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 1.2f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0f, EndNormalized = 1f, Confidence = 1f }
                                }
                            }
                        },
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "run",
                            DisplayName = "Run",
                            SourceSubClipId = "Run",
                            SourceClipName = "Run",
                            SourceSelection = runSelection,
                            Loop = true,
                            Calibration = new AnimationClipCalibrationAuthoring
                            {
                                NativeVelocityX = 0f,
                                NativeVelocityY = 2.3f,
                                PlaybackSpeed = 1f,
                                CycleDurationSeconds = 0.7f,
                                LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.08f, EndNormalized = 0.3f, Confidence = 0.85f }
                                },
                                RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
                                {
                                    new AnimationFootContactWindowAuthoring { StartNormalized = 0.58f, EndNormalized = 0.8f, Confidence = 0.85f }
                                }
                            }
                        }
                    },
                    Blend2D = new List<AnimationBlend2DAuthoring>
                    {
                        new AnimationBlend2DAuthoring
                        {
                            BlendId = "locomotion",
                            XParameter = "moveX",
                            YParameter = "moveY",
                            DefaultClipId = "idle",
                            Points = new List<AnimationBlend2DPointAuthoring>
                            {
                                new AnimationBlend2DPointAuthoring { ClipId = "idle", X = 0f, Y = 0f },
                                new AnimationBlend2DPointAuthoring { ClipId = "run", X = 0f, Y = 1f }
                            }
                        }
                    },
                    Timelines = new List<AnimationTimelineAuthoring>
                    {
                        new AnimationTimelineAuthoring
                        {
                            TimelineId = "run.events",
                            ClipId = "run",
                            Events = new List<AnimationTimelineEventAuthoring>
                            {
                                new AnimationTimelineEventAuthoring { EventId = "run.vfx.footstep", ClipId = "run", EventKind = "Vfx", ResourceSelection = vfxSelection },
                                new AnimationTimelineEventAuthoring { EventId = "run.audio.footstep", ClipId = "run", EventKind = "AudioCue", ResourceSelection = audioSelection }
                            }
                        }
                    }
                }
            },
            ActionBindings = new List<AnimationActionBindingAuthoring>
            {
                new AnimationActionBindingAuthoring { BindingId = "move", ActionId = "character.move", GroupId = "group.locomotion", BlendId = "locomotion", Required = true }
            },
            Warmup = new AnimationWarmupAuthoring
            {
                RequiredClipIds = new List<string> { "idle", "run" },
                AvatarMaskSelections = new List<AuthoringResourceSelectionRef> { maskSelection },
                GeneratedArtifactSelections = new List<AuthoringResourceSelectionRef> { bakeSelection },
                VfxSelections = new List<AuthoringResourceSelectionRef> { vfxSelection },
                AudioCueSelections = new List<AuthoringResourceSelectionRef> { audioSelection }
            }
        });

        package.Profiles.Add(new AnimationAuthoringProfile
        {
            ProfileId = "profile.default",
            DefaultSetId = "set.base",
            DefaultGroupId = "group.locomotion",
            Slots = new List<AnimationProfileSlotAuthoring>
            {
                new AnimationProfileSlotAuthoring { SlotId = "locomotion", SetId = "set.base", GroupId = "group.locomotion", DefaultBlendId = "locomotion", Required = true }
            }
        });

        var catalog = new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle", StableId = "stable.anim.idle", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/idle.anim", Hash = "sha256:idle" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.run", StableId = "stable.anim.run", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/run.anim", Hash = "sha256:run" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.mask.full_body", StableId = "stable.mask.full_body", TypeId = AnimationAuthoringResourceKinds.AvatarMask, Usage = AnimationAuthoringResourceUsages.AvatarMask, RelativePath = "resources/animations/full_body.mask", Hash = "sha256:mask" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle.bake", StableId = "stable.anim.idle.bake", TypeId = CharacterPackageResourceTypeIds.Config, Usage = AnimationAuthoringResourceUsages.AnimationBakeArtifact, RelativePath = "generated/animation/idle_bake.json", Hash = "sha256:bake" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.vfx.footstep", StableId = "stable.vfx.footstep", TypeId = CharacterPackageResourceTypeIds.Vfx, Usage = CharacterPackageResourceUsageIds.VfxCue, RelativePath = "resources/vfx/footstep.prefab", Hash = "sha256:vfx" }
            }
        };

        AnimationAuthoringCompileResult result = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = package,
            ResourceCatalog = catalog
        });

        Require(result.AnimationSetDefinition.Format == AnimationAuthoringCompileFormats.AnimationSetDefinition, "animation set definition should declare format.");
        Require(result.AnimationSetDefinition.Sets.Count == 1, "animation set definition should contain the authored set.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[0].RuntimeResourceKey == "runtime.anim.idle", "animation set definition should use resolved runtime clip keys.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[1].Calibration.NativeVelocityY == 2.3f, "animation set definition should include clip calibration native velocity.");
        Require(result.AnimationSetDefinition.Sets[0].Groups[0].Clips[1].Calibration.LeftFootContactWindows.Count == 1, "animation set definition should include left foot contact windows.");
        Require(result.AnimationClipRegistry.Clips.Count == 2, "animation clip registry should contain authored clips.");
        Require(result.AnimationClipRegistry.Clips[0].SourceSelection.ResourceStableId == "stable.anim.idle", "clip registry should retain source selection.");
        Require(result.AnimationClipRegistry.Clips[0].SourceSubClipId == "Idle", "clip registry should retain source sub-clip.");
        Require(result.AnimationClipRegistry.Clips[0].RuntimeResourceKey == "runtime.anim.idle", "clip registry should retain resolved runtime key.");
        Require(result.AnimationClipRegistry.Clips[0].Hash == "sha256:idle", "clip registry should retain runtime resource hash.");
        Require(result.AnimationResourcePlan.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "runtime.anim.idle"), "animation runtime catalog should include idle.");
        RuntimeResourceCatalogEntryDocument idleRuntimeEntry = result.AnimationResourcePlan.RuntimeResourceCatalog.Entries.Find(entry => entry.Id == "runtime.anim.idle");
        Require(idleRuntimeEntry != null && idleRuntimeEntry.Type == "AnimationClip", "runtime idle entry should declare AnimationClip type.");
        Require(idleRuntimeEntry.Address == "resources/animations/idle.anim", "runtime idle entry should use package/runtime address, not Unity asset path.");
        Require(!idleRuntimeEntry.Address.StartsWith("Assets/", StringComparison.Ordinal), "runtime idle entry should not use Unity asset paths as runtime input.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.run"), "animation warmup should include run.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.idle"), "animation warmup should include default idle.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.mask.full_body"), "animation warmup should include AvatarMask resources.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.AnimationWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.anim.idle.bake"), "animation warmup should include bake artifacts.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.VfxWarmup.Resources.Exists(resource => resource.ResourceKey == "runtime.vfx.footstep"), "VFX timeline events should enter VfxWarmup.");
        Require(result.AnimationResourcePlan.CharacterResourcePlan.Audio.RequiredCues.Contains("cue.test.footstep"), "AudioCue timeline events should enter the Audio plan.");
        Require(result.AnimationResourcePlan.AudioCueManifest.Cues.Exists(cue => cue.CueId == "cue.test.footstep"), "AudioCue manifest should include FMOD cue references.");
        Require(!string.IsNullOrWhiteSpace(result.AnimationResourcePlan.PlanHash), "animation resource plan should have a deterministic hash.");
        Require(!result.AnimationValidationReport.HasBlockingIssues, "valid animation authoring package should compile without blocking issues.");
        Require(!result.AnimationValidationReport.Issues.Exists(issue => issue.Code == "LOCO_CAL_CLIP_METADATA_MISSING"), "complete locomotion calibration should not warn.");

        package.Sets[0].Groups[0].Clips[1].Calibration = new AnimationClipCalibrationAuthoring();
        AnimationAuthoringCompileResult missingCalibration = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(missingCalibration.AnimationValidationReport.Issues.Exists(issue => issue.Code == "LOCO_CAL_CLIP_METADATA_MISSING"), "locomotion clips missing calibration metadata should emit warning diagnostics.");
        package.Sets[0].Groups[0].Clips[1].Calibration = new AnimationClipCalibrationAuthoring
        {
            NativeVelocityX = 0f,
            NativeVelocityY = 2.3f,
            PlaybackSpeed = 1f,
            CycleDurationSeconds = 0.7f,
            LeftFootContactWindows = new List<AnimationFootContactWindowAuthoring>
            {
                new AnimationFootContactWindowAuthoring { StartNormalized = 0.08f, EndNormalized = 0.3f, Confidence = 0.85f }
            },
            RightFootContactWindows = new List<AnimationFootContactWindowAuthoring>
            {
                new AnimationFootContactWindowAuthoring { StartNormalized = 0.58f, EndNormalized = 0.8f, Confidence = 0.85f }
            }
        };

        var groupCatalog = new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.idle", StableId = "stable.anim.idle", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = CharacterPackageResourceUsageIds.AnimationClipGroup, RelativePath = "resources/animations/idle.glb", Hash = "sha256:idle" },
                new CharacterPackageResourceEntry { ResourceKey = "runtime.anim.run", StableId = "stable.anim.run", TypeId = CharacterPackageResourceTypeIds.Animation, Usage = AnimationAuthoringResourceUsages.AnimationClip, RelativePath = "resources/animations/run.anim", Hash = "sha256:run" }
            }
        };
        AnimationAuthoringCompileResult groupInvalid = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = groupCatalog });
        Require(groupInvalid.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_RUNTIME_TYPE_MISMATCH"), "animationClipGroup runtime bindings should be rejected as runtime clips.");

        var missingRuntimePackage = new AnimationAuthoringPackage { PackageId = "animation.missing_runtime", StableId = "animation.missing_runtime" };
        missingRuntimePackage.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            Groups = new List<AnimationGroupAuthoring>
            {
                new AnimationGroupAuthoring
                {
                    GroupId = "group.base",
                    Clips = new List<AnimationClipMappingAuthoring>
                    {
                        new AnimationClipMappingAuthoring
                        {
                            ClipId = "idle",
                            SourceSelection = new AuthoringResourceSelectionRef
                            {
                                ResourceStableId = "unity.project.idle",
                                SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                                BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                                ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
                                RuntimeResourceKey = "runtime.anim.editor_only"
                            }
                        }
                    }
                }
            }
        });
        AnimationAuthoringCompileResult notReady = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingRuntimePackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(notReady.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_SOURCE_NOT_RUNTIME_READY"), "editor-only required runtime clips should emit source-not-runtime-ready diagnostics.");

        missingRuntimePackage.Sets[0].Groups[0].Clips[0].SourceSelection.RuntimeResourceKey = string.Empty;
        AnimationAuthoringCompileResult missingRuntimeKey = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingRuntimePackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(missingRuntimeKey.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_RUNTIME_KEY_MISSING"), "required runtime clips without runtime keys should emit structured missing-key diagnostics.");

        var missingWarmupClipPackage = new AnimationAuthoringPackage { PackageId = "animation.missing_warmup", StableId = "animation.missing_warmup" };
        missingWarmupClipPackage.Sets.Add(new AnimationAuthoringSet
        {
            SetId = "set.base",
            Warmup = new AnimationWarmupAuthoring { RequiredClipIds = new List<string> { "missing" } }
        });
        AnimationAuthoringCompileResult missingWarmupClip = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = missingWarmupClipPackage, ResourceCatalog = new CharacterPackageResourceCatalog() });
        Require(missingWarmupClip.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_WARMUP_REQUIRED_CLIP_MISSING"), "missing warmup required clips should emit structured diagnostics.");

        package.Sets[0].Groups[0].Blend2D[0].Points.Add(new AnimationBlend2DPointAuthoring { ClipId = "missing", X = 1f, Y = 1f });
        AnimationAuthoringCompileResult invalid = AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest { Package = package, ResourceCatalog = catalog });
        Require(invalid.AnimationValidationReport.HasBlockingIssues, "missing clip references should block animation compile.");
        Require(invalid.AnimationValidationReport.Issues.Exists(issue => issue.Code == "ANIM_MISSING_CLIP_REFERENCE"), "missing clip references should emit a stable diagnostic code.");
    }

    private static void AnimationAuthoringResourceFieldSpecs_FilterAndResolveContracts()
    {
        var collection = new AuthoringResourceCollection { ScopeId = "animation.test" };
        collection.Items.Add(CreateAnimationSourceItem(
            "unity.project.idle",
            AnimationAuthoringResourceUsages.AnimationClip,
            AuthoringResourceProviderIds.UnityProjectAssets,
            AuthoringResourceSourceKind.UnityAsset,
            AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            "Assets/Art/Animations/idle.anim",
            string.Empty));
        collection.Items.Add(CreateAnimationSourceItem(
            "runtime.anim.locomotion",
            CharacterPackageResourceUsageIds.AnimationClipGroup,
            AuthoringResourceProviderIds.RuntimeCatalog,
            AuthoringResourceSourceKind.RuntimeCatalogAsset,
            AuthoringResourceBindingKind.ResourceManagerAsset,
            "char.test.anim.locomotion",
            "char.test.anim.locomotion"));
        collection.Items.Add(CreateAnimationSourceItem(
            "charpkg.test.anim.locomotion",
            CharacterPackageResourceUsageIds.AnimationClipGroup,
            AuthoringResourceProviderIds.CharacterPackage,
            AuthoringResourceSourceKind.PackageResource,
            AuthoringResourceBindingKind.PackageResource,
            "char.test.anim.locomotion",
            string.Empty));
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "unity:fbx",
            StableId = "unity.project.fbx.container",
            DisplayName = "Standing Run Forward.fbx",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.PreviewMesh,
            SourceProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
            SourceKind = AuthoringResourceSourceKind.UnityAsset,
            BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.EditorOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.UnityProjectAssets,
                    BindingKind = AuthoringResourceBindingKind.UnityEditorOnlyAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    IsPrimary = true,
                    UnityAssetPath = "Assets/Art/Animations/Standing Run Forward.fbx",
                    ProviderResourceKey = "Assets/Art/Animations/Standing Run Forward.fbx"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:event",
            StableId = "fmod.event.footstep",
            DisplayName = "Footstep",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = AnimationAuthoringResourceUsages.FmodEvent,
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
                    IsPrimary = true,
                    ProviderResourceKey = "event:/Character/Test/Footstep",
                    FmodEventGuid = "{footstep}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "audio.cue.character.test.footstep" },
                        { "audioEventDefinitionId", "audio.event.character.test.footstep" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    ProviderResourceKey = "audio.cue.character.test.footstep",
                    FmodEventGuid = "{footstep}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "audio.cue.character.test.footstep" },
                        { "audioEventDefinitionId", "audio.event.character.test.footstep" }
                    }
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "runtime:vfx",
            StableId = "runtime.vfx.spark",
            DisplayName = "Spark",
            Kind = CharacterPackageResourceTypeIds.Vfx,
            Usage = CharacterPackageResourceUsageIds.VfxCue,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady
        });

        var service = new AuthoringResourceSelectionService();
        AuthoringResourceFieldSpec sourceClipSpec = AnimationAuthoringResourceFieldSpecs.CreateSourceClip();
        AuthoringResourcePickerQueryResult sourceQuery = service.Query(collection, sourceClipSpec, new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" });
        Require(sourceClipSpec.FieldKey == AnimationAuthoringResourceFieldKeys.SourceClip, "source clip field key should be stable.");
        Require(sourceClipSpec.PreloadPolicy == AuthoringResourcePreloadPolicies.AnimationWarmup, "source clip should use animation warmup preload policy.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "unity.project.idle").Selectable, "direct Unity .anim source clip should be selectable.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "runtime.anim.locomotion").Selectable, "runtime animation clip group should be selectable.");
        Require(sourceQuery.Items.Find(item => item.Item.StableId == "charpkg.test.anim.locomotion").Selectable, "package animation clip group should be selectable.");
        AuthoringResourcePickerItem fbxCandidate = sourceQuery.Items.Find(item => item.Item.StableId == "unity.project.fbx.container");
        Require(fbxCandidate != null && !fbxCandidate.Selectable, "FBX/model containers should not be selectable as Animation.SourceClip.");
        Require(fbxCandidate.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "FBX/model rejection should include a kind mismatch.");

        AuthoringResourceSelectionResolutionResult sourceResult = service.Resolve(
            collection,
            sourceClipSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.locomotion",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog
            });
        Require(sourceResult.Accepted, "Animation.SourceClip should resolve runtime clip groups.");
        Require(sourceResult.Selection.RuntimeResourceKey == "char.test.anim.locomotion", "Animation.SourceClip runtime selection should preserve runtime resource key.");

        AuthoringResourceFieldSpec audioCueSpec = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue();
        AuthoringResourceSelectionResolutionResult audioCueResult = service.Resolve(
            collection,
            audioCueSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.footstep",
                SourceProviderId = AuthoringResourceProviderIds.Fmod
            });
        Require(audioCueResult.Accepted, "Animation.EventAudioCue should accept FMOD event audio resources.");
        Require(audioCueResult.Selection.AudioCueId == "audio.cue.character.test.footstep", "Animation.EventAudioCue default output should fill AudioCueId.");
        Require(audioCueSpec.AcceptedUsages.Contains(AnimationAuthoringResourceUsages.FmodEvent), "Animation.EventAudioCue should accept fmodEvent usage.");

        AuthoringResourceSelectionResolutionResult audioEventResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioEventDefinitionId),
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.footstep",
                SourceProviderId = AuthoringResourceProviderIds.Fmod
            });
        Require(audioEventResult.Accepted, "Animation.EventAudioCue should resolve audio event definitions when requested.");
        Require(audioEventResult.Selection.AudioEventDefinitionId == "audio.event.character.test.footstep", "Animation.EventAudioCue should fill AudioEventDefinitionId output.");

        AuthoringResourceSelectionResolutionResult wrongAudioResult = service.Resolve(
            collection,
            audioCueSpec,
            new AuthoringResourceConsumerContext(),
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.spark",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog
            });
        Require(!wrongAudioResult.Accepted, "Animation.EventAudioCue should reject VFX resources.");
        Require(wrongAudioResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "Animation.EventAudioCue kind mismatch should be structured.");
    }

    private static AuthoringResourceItem CreateAnimationSourceItem(
        string stableId,
        string usage,
        string providerId,
        AuthoringResourceSourceKind sourceKind,
        AuthoringResourceBindingKind bindingKind,
        string providerKey,
        string runtimeKey)
    {
        return new AuthoringResourceItem
        {
            ResourceId = providerId + ":" + stableId,
            StableId = stableId,
            DisplayName = stableId,
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = usage,
            SourceProviderId = providerId,
            SourceKind = sourceKind,
            BindingKind = bindingKind,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = string.IsNullOrWhiteSpace(runtimeKey) ? AuthoringResourceRuntimeAvailability.EditorOnly : AuthoringResourceRuntimeAvailability.RuntimeReady,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = providerId,
                    BindingKind = bindingKind,
                    BindingKeyKind = string.IsNullOrWhiteSpace(runtimeKey) ? AuthoringResourceBindingKeyKinds.UnityAssetPath : AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    IsPrimary = true,
                    ProviderResourceKey = providerKey,
                    RuntimeResourceKey = runtimeKey,
                    PackageResourceKey = bindingKind == AuthoringResourceBindingKind.PackageResource ? providerKey : string.Empty,
                    UnityAssetPath = bindingKind == AuthoringResourceBindingKind.UnityEditorOnlyAsset || bindingKind == AuthoringResourceBindingKind.UnityAsset ? providerKey : string.Empty
                }
            }
        };
    }

    private static void EditorServer_AnimationPackageApi_IsFileBackedAndValidatesShallowDraft()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-animation-api-" + Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "animation-test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "animation_test",
            StableId = "char.animation_test",
            DisplayName = "Animation Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog(), JsonOptions));

        try
        {
            string packageRelative = "Tools/MxFramework.Authoring/samples/animation-test";
            List<object> packages = MxFramework.Authoring.Cli.EditorServer.ListAnimationPackages(tempRoot, packageRelative, JsonOptions);
            Require(packages.Count == 1, "animation package list should include character package scopes.");
            Require(GetAnonymousPropertyForTest(packages[0], "packageId") == "animation.animation_test", "animation package list should seed package id from character manifest.");
            Require(GetAnonymousPropertyForTest(packages[0], "characterPackageId") == "animation_test", "animation package list should expose the source character package id.");

            object initialState = MxFramework.Authoring.Cli.EditorServer.ReadAnimationPackageState(tempRoot, "animation_test", packageRelative, JsonOptions);
            Require(GetAnonymousPropertyForTest(initialState, "documentRelative").EndsWith("config/animation_authoring.json", StringComparison.Ordinal), "animation load should resolve id to a package-scoped document.");

            var draft = new AnimationAuthoringPackage
            {
                PackageId = "animation.animation_test",
                StableId = "anim.animation_test",
                DisplayName = "Animation Test"
            };
            draft.Sets.Add(new AnimationAuthoringSet
            {
                SetId = "set.base",
                Groups = new List<AnimationGroupAuthoring>
                {
                    new AnimationGroupAuthoring
                    {
                        GroupId = "group.locomotion",
                        Clips = new List<AnimationClipMappingAuthoring>
                        {
                            new AnimationClipMappingAuthoring { ClipId = "idle" },
                            new AnimationClipMappingAuthoring { ClipId = "idle", SourceSelection = new AuthoringResourceSelectionRef { ResourceStableId = "unity.project.idle" } }
                        }
                    }
                }
            });

            CharacterAuthoringValidationReport validation = MxFramework.Authoring.Cli.EditorServer.ValidateAnimationPackage(draft);
            Require(validation.Issues.Exists(issue => issue.Code == "ANIM_DUPLICATE_CLIP_ID"), "animation validation should report duplicate clip ids.");
            Require(validation.Issues.Exists(issue => issue.Code == "ANIM_MISSING_SOURCE_SELECTION"), "animation validation should report missing source selections.");

            MxFramework.Authoring.Cli.EditorServer.SaveAnimationPackage(tempRoot, packageRelative, packageRelative, draft, JsonOptions);
            string documentPath = Path.Combine(packagePath, "config", "animation_authoring.json");
            Require(File.Exists(documentPath), "animation save should write the package-scoped animation authoring document.");
            AnimationAuthoringPackage saved = JsonSerializer.Deserialize<AnimationAuthoringPackage>(File.ReadAllText(documentPath), JsonOptions);
            Require(saved != null && saved.PackageId == "animation.animation_test", "animation save should persist the draft package.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void EditorServer_AnimationPreview_IsCompilerBackedAndResolvesResources()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-animation-preview-" + Guid.NewGuid().ToString("N"));
        string packageRelative = "Tools/MxFramework.Authoring/samples/animation-preview-test";
        string packagePath = Path.Combine(tempRoot, "Tools", "MxFramework.Authoring", "samples", "animation-preview-test");
        Directory.CreateDirectory(Path.Combine(packagePath, "config"));
        Directory.CreateDirectory(Path.Combine(packagePath, "resources", "animations"));
        File.WriteAllText(Path.Combine(packagePath, "resources", "animations", "idle.glb"), "preview glb placeholder");
        File.WriteAllText(Path.Combine(packagePath, "manifest.json"), JsonSerializer.Serialize(new CharacterPackageManifest
        {
            PackageId = "animation_preview_test",
            StableId = "char.animation_preview_test",
            DisplayName = "Animation Preview Test"
        }, JsonOptions));
        File.WriteAllText(Path.Combine(packagePath, "resource_catalog.json"), JsonSerializer.Serialize(new CharacterPackageResourceCatalog
        {
            Entries = new List<CharacterPackageResourceEntry>
            {
                new CharacterPackageResourceEntry
                {
                    ResourceKey = "runtime.anim.idle",
                    StableId = "stable.anim.idle",
                    TypeId = CharacterPackageResourceTypeIds.Animation,
                    Usage = AnimationAuthoringResourceUsages.AnimationClip,
                    RelativePath = "resources/animations/idle.anim",
                    Hash = "sha256:idle"
                }
            }
        }, JsonOptions));

        try
        {
            var sourceSelection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "stable.anim.idle",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                ExpectedKind = CharacterPackageResourceTypeIds.Animation,
                ExpectedUsage = AnimationAuthoringResourceUsages.AnimationClip,
                RuntimeResourceKey = "runtime.anim.idle"
            };
            var draft = new AnimationAuthoringPackage
            {
                PackageId = "animation.preview",
                StableId = "animation.preview",
                DisplayName = "Animation Preview"
            };
            draft.Sets.Add(new AnimationAuthoringSet
            {
                SetId = "set.base",
                DisplayName = "Base",
                DefaultClipId = "idle",
                Groups = new List<AnimationGroupAuthoring>
                {
                    new AnimationGroupAuthoring
                    {
                        GroupId = "group.locomotion",
                        Usage = "locomotion",
                        Clips = new List<AnimationClipMappingAuthoring>
                        {
                            new AnimationClipMappingAuthoring
                            {
                                ClipId = "idle",
                                DisplayName = "Idle",
                                RuntimeResourceKey = "runtime.anim.idle",
                                SourceSelection = sourceSelection,
                                Loop = true
                            }
                        }
                    }
                },
                Warmup = new AnimationWarmupAuthoring
                {
                    RequiredClipIds = new List<string> { "idle" }
                }
            });

            object preview = MxFramework.Authoring.Cli.EditorServer.ReadAnimationPreview(tempRoot, packageRelative, packageRelative, draft, JsonOptions);
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(preview, JsonOptions));
            JsonElement root = document.RootElement;
            Require(root.GetProperty("serviceStatus").GetString() == "ready", "animation preview endpoint should report ready service status.");
            Require(root.GetProperty("animationSetDefinition").GetProperty("sets").GetArrayLength() == 1, "animation preview should expose compiled animation set definition.");
            Require(root.GetProperty("animationClipRegistry").GetProperty("clips").GetArrayLength() == 1, "animation preview should expose compiled clip registry.");
            JsonElement animationClip = root.GetProperty("previewResources").GetProperty("animationClips")[0];
            Require(animationClip.GetProperty("runtimeResourceKey").GetString() == "runtime.anim.idle", "animation preview should map clip ids to runtime resource keys.");
            Require(animationClip.GetProperty("loop").GetBoolean(), "animation preview should expose authoring loop metadata for the player.");
            Require(Math.Abs(animationClip.GetProperty("speed").GetSingle() - 1f) < 0.001f, "animation preview should expose authoring speed metadata for the player.");
            Require(animationClip.GetProperty("rootMotionPolicy").GetString() == "Ignore", "animation preview should expose root motion policy metadata for the player.");
            JsonElement resource = animationClip.GetProperty("resource");
            Require(resource.GetProperty("resourceKey").GetString() == "runtime.anim.idle", "animation preview should resolve runtime resource metadata.");
            Require(resource.GetProperty("url").GetString().EndsWith("/resources/animations/idle.anim", StringComparison.Ordinal), "animation preview should expose a static resource URL for the viewport.");
            Require(resource.GetProperty("exists").GetBoolean(), "animation preview should mark existing preview resource files.");
            Require(root.GetProperty("animationValidationReport").GetProperty("hasBlockingIssues").GetBoolean() == false, "valid animation preview draft should compile without blocking diagnostics.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void AuthoringResourceSelectionService_FiltersAndResolvesFieldSpecs()
    {
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        var service = new AuthoringResourceSelectionService();
        var context = new AuthoringResourceConsumerContext
        {
            ConsumerKind = "CharacterStudio",
            ConsumerStableId = "character.iron_vanguard",
            ScopeId = "character.iron_vanguard",
            SkeletonStableId = "skeleton.humanoid"
        };

        var characterModelSpec = new AuthoringResourceFieldSpec
        {
            FieldKey = "Character.Model",
            EditorKind = "CharacterStudio",
            AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
            AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.CharacterModel },
            AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.RuntimeCatalog },
            AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
            RequireRuntimeLoadable = true,
            RequireUnityImported = true,
            PreloadPolicy = AuthoringResourcePreloadPolicies.SpawnCritical,
            OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
        };

        AuthoringResourcePickerQueryResult query = service.Query(collection, characterModelSpec, context);
        Require(query.Items.Count == collection.Items.Count, "query should return one picker item per resource.");
        AuthoringResourcePickerItem modelCandidate = query.Items.Find(item => item.Item.StableId == "runtime.character.body");
        Require(modelCandidate != null && modelCandidate.Selectable, "runtime character model should be selectable.");
        AuthoringResourcePickerItem audioCandidate = query.Items.Find(item => item.Item.StableId == "audio.hit");
        Require(audioCandidate != null && !audioCandidate.Selectable, "audio item should not be selectable for character model field.");
        Require(audioCandidate.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.KindMismatch), "blocked candidate should include structured kind mismatch reason.");

        AuthoringResourceSelectionResolutionResult modelResult = service.Resolve(
            collection,
            characterModelSpec,
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.character.body",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(modelResult.Accepted, "runtime character model selection should resolve.");
        Require(modelResult.Selection.RuntimeResourceKey == "char.iron_vanguard.model.body.prefab", "runtime selection should fill runtime resource key.");
        Require(string.IsNullOrEmpty(modelResult.Selection.PackageResourceKey), "runtime selection should not gain package-local key.");

        AuthoringResourceSelectionResolutionResult packageResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "ResourceLibrary.PackageResource",
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.CharacterPackage },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.PackageResource },
                OutputKind = AuthoringResourceSelectionOutputKind.PackageResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "charpkg.iron_vanguard.resource.model.body",
                SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
                BindingKind = AuthoringResourceBindingKind.PackageResource
            });
        Require(packageResult.Accepted, "package resource selection should resolve.");
        Require(packageResult.Selection.PackageResourceKey == "char.iron_vanguard.model.body", "package resource should fill package key.");
        Require(string.IsNullOrEmpty(packageResult.Selection.RuntimeResourceKey), "package resource must not be copied into runtime key.");

        AuthoringResourceSelectionResolutionResult iconResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Ui.Icon",
                EditorKind = "UiEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Texture },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.PreviewThumbnail },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.UnityAssetDatabase },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.UnityAsset },
                RequireUnityImported = true,
                OutputKind = AuthoringResourceSelectionOutputKind.UnityGuid
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "unity.icon.portrait",
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset
            });
        Require(iconResult.Accepted, "UI icon Unity asset should resolve.");
        Require(iconResult.Selection.UnityGuid == "guid-portrait", "Unity icon selection should fill Unity GUID.");
        Require(string.IsNullOrEmpty(iconResult.Selection.RuntimeResourceKey), "Unity-only selection must not fill runtime key.");

        AuthoringResourceSelectionResolutionResult audioResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "CombatAction.HitSfx",
                EditorKind = "CombatEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "audio.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });
        Require(audioResult.Accepted, "FMOD audio cue should resolve.");
        Require(audioResult.Selection.AudioCueId == "cue.hit", "audio selection should fill audio cue id.");
        Require(string.IsNullOrEmpty(audioResult.Selection.RuntimeResourceKey), "audio cue must not gain runtime key.");

        AuthoringResourceSelectionResolutionResult externalResult = service.Resolve(
            collection,
            characterModelSpec,
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "external.body.fbx",
                SourceProviderId = AuthoringResourceProviderIds.ExternalImportStaging,
                BindingKind = AuthoringResourceBindingKind.ExternalSource
            });
        Require(!externalResult.Accepted, "external source should be blocked when runtime loadable model is required.");
        Require(externalResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.ProviderMismatch), "external source should report provider mismatch.");
        Require(externalResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.NotRuntimeLoadable), "external source should report runtime availability mismatch.");

        AuthoringResourceSelectionResolutionResult warnResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Equipment.MainHand.Model",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Model },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.WeaponModel },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                AllowIncompatibleWithWarning = true,
                CompatibilityFilter = new AuthoringResourceCompatibility { SlotId = "mainHand" },
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.weapon.offhand",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(warnResult.Accepted, "incompatible weapon slot should be selectable when warnings are allowed.");
        Require(warnResult.Reasons.Exists(reason => reason.Code == AuthoringResourceSelectionReasonCodes.SlotMismatch && !reason.BlocksSelection), "slot mismatch should be a non-blocking structured warning.");

        AuthoringResourceSelectionResolutionResult animationResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Animation.ClipGroup",
                EditorKind = "AnimationEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Animation },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AnimationClipGroup },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.locomotion",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(animationResult.Accepted && animationResult.Selection.RuntimeResourceKey == "char.iron_vanguard.anim.locomotion", "animation clip group field should resolve through same contract.");

        AuthoringResourceSelectionResolutionResult vfxResult = service.Resolve(
            collection,
            new AuthoringResourceFieldSpec
            {
                FieldKey = "Vfx.Prefab",
                EditorKind = "VfxEditor",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Vfx },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.VfxCue },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            context,
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.slash",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });
        Require(vfxResult.Accepted && vfxResult.Selection.RuntimeResourceKey == "char.iron_vanguard.vfx.slash", "VFX prefab field should resolve through same contract.");
    }

    private static void AuthoringResourceSelectionService_SourceClipAndFmodMultiBinding_DoNotDriftResolution()
    {
        var service = new AuthoringResourceSelectionService();
        var collection = new AuthoringResourceCollection { ScopeId = "selection-regression" };

        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "runtime:anim.run_r",
            StableId = "runtime.anim.run_r",
            DisplayName = "Run_R",
            Kind = CharacterPackageResourceTypeIds.Animation,
            Usage = CharacterPackageResourceUsageIds.AnimationClip,
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
                    IsPrimary = true,
                    ProviderResourceKey = "char.iron_vanguard.anim.run_r",
                    RuntimeResourceKey = "char.iron_vanguard.anim.run_r"
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                    BindingKind = AuthoringResourceBindingKind.UnityAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.UnityAssetPath,
                    ProviderResourceKey = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim",
                    UnityAssetPath = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim"
                }
            }
        });

        AuthoringResourceSelectionResolutionResult sourceClipResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateSourceClip(),
            new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.anim.run_r",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            });

        Require(sourceClipResult.Accepted, "source clip selection should resolve for runtime animation item.");
        Require(sourceClipResult.Selection.BindingKind == AuthoringResourceBindingKind.ResourceManagerAsset, "source clip selection should keep the intended binding kind.");
        Require(sourceClipResult.Selection.RuntimeResourceKey == "char.iron_vanguard.anim.run_r", "source clip selection should keep runtime resource key.");
        Require(sourceClipResult.Selection.UnityAssetPath == "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/standing_run_right.anim", "source clip selection should backfill unity asset path from sibling binding.");

        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:event.hit",
            StableId = "fmod.event.hit",
            DisplayName = "Hit",
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
                    IsPrimary = true,
                    ProviderResourceKey = "event:/Character/Hit",
                    FmodEventGuid = "{fmod-hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    ProviderResourceKey = "cue.hit",
                    FmodEventGuid = "{fmod-hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                },
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.RuntimeResourceKey,
                    ProviderResourceKey = "runtime.should.not.leak",
                    RuntimeResourceKey = "runtime.should.not.leak"
                }
            }
        });

        AuthoringResourceSelectionResolutionResult audioCueResult = service.Resolve(
            collection,
            AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioCueId),
            new AuthoringResourceConsumerContext { ConsumerKind = "AnimationEditor" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = "fmod.event.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });

        Require(audioCueResult.Accepted, "fmod audio cue selection should resolve.");
        Require(audioCueResult.Selection.BindingKind == AuthoringResourceBindingKind.AudioCue, "fmod cue selection should keep audio cue binding kind.");
        Require(audioCueResult.Selection.AudioCueId == "cue.hit", "fmod cue selection should preserve cue id.");
        Require(string.IsNullOrEmpty(audioCueResult.Selection.RuntimeResourceKey), "fmod cue selection should not backfill runtime keys from sibling bindings.");
        Require(string.IsNullOrEmpty(audioCueResult.Selection.UnityAssetPath), "fmod cue selection should not backfill unity asset path from sibling bindings.");
    }

    private static AuthoringResourceCollection BuildAuthoringSelectionCollection()
    {
        var collection = new AuthoringResourceCollection { ScopeId = "test" };
        collection.Items.Add(CreateRuntimeResource("runtime.character.body", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.CharacterModel, "char.iron_vanguard.model.body.prefab", "body", "skeleton.humanoid"));
        collection.Items.Add(CreateRuntimeResource("runtime.anim.locomotion", CharacterPackageResourceTypeIds.Animation, CharacterPackageResourceUsageIds.AnimationClipGroup, "char.iron_vanguard.anim.locomotion", "", "skeleton.humanoid"));
        collection.Items.Add(CreateRuntimeResource("runtime.vfx.slash", CharacterPackageResourceTypeIds.Vfx, CharacterPackageResourceUsageIds.VfxCue, "char.iron_vanguard.vfx.slash", "", ""));
        collection.Items.Add(CreateRuntimeResource("runtime.weapon.offhand", CharacterPackageResourceTypeIds.Model, CharacterPackageResourceUsageIds.WeaponModel, "char.iron_vanguard.weapon.shield.prefab", "offHand", "skeleton.humanoid"));
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "characterPackage:charpkg.iron_vanguard.resource.model.body",
            StableId = "charpkg.iron_vanguard.resource.model.body",
            DisplayName = "Body Package Resource",
            Kind = CharacterPackageResourceTypeIds.Model,
            Usage = CharacterPackageResourceUsageIds.CharacterModel,
            SourceProviderId = AuthoringResourceProviderIds.CharacterPackage,
            SourceKind = AuthoringResourceSourceKind.PackageResource,
            BindingKind = AuthoringResourceBindingKind.PackageResource,
            ImportStatus = AuthoringResourceImportStatus.New,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.Unknown,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.CharacterPackage,
                    BindingKind = AuthoringResourceBindingKind.PackageResource,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.PackageResourceKey,
                    IsPrimary = true,
                    ProviderResourceKey = "char.iron_vanguard.model.body",
                    PackageResourceKey = "char.iron_vanguard.model.body"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "unity:portrait",
            StableId = "unity.icon.portrait",
            DisplayName = "Portrait",
            Kind = CharacterPackageResourceTypeIds.Texture,
            Usage = CharacterPackageResourceUsageIds.PreviewThumbnail,
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
                    IsPrimary = true,
                    UnityGuid = "guid-portrait",
                    UnityAssetPath = "Assets/UI/portrait.png"
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "fmod:hit",
            StableId = "audio.hit",
            DisplayName = "Hit",
            Kind = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            SourceProviderId = AuthoringResourceProviderIds.Fmod,
            SourceKind = AuthoringResourceSourceKind.FmodLibrary,
            BindingKind = AuthoringResourceBindingKind.AudioCue,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly,
            ProviderBindings = new List<AuthoringResourceProviderBinding>
            {
                new AuthoringResourceProviderBinding
                {
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    BindingKind = AuthoringResourceBindingKind.AudioCue,
                    BindingKeyKind = AuthoringResourceBindingKeyKinds.FmodEventGuid,
                    IsPrimary = true,
                    FmodEventPath = "event:/Character/IronVanguard/Hit",
                    FmodEventGuid = "{hit}",
                    ProviderData = new Dictionary<string, string>
                    {
                        { "audioCueId", "cue.hit" },
                        { "audioEventDefinitionId", "event.hit" }
                    }
                }
            }
        });
        collection.Items.Add(new AuthoringResourceItem
        {
            ResourceId = "external:body.fbx",
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
                    IsPrimary = true,
                    ExternalSourcePath = "/imports/body.fbx"
                }
            }
        });
        return collection;
    }

    private static AuthoringResourceItem CreateRuntimeResource(string stableId, string kind, string usage, string runtimeKey, string slotId, string skeletonStableId)
    {
        return new AuthoringResourceItem
        {
            ResourceId = "runtime:" + runtimeKey,
            StableId = stableId,
            DisplayName = stableId,
            Kind = kind,
            Usage = usage,
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
            SourceKind = AuthoringResourceSourceKind.RuntimeCatalogAsset,
            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
            ImportStatus = AuthoringResourceImportStatus.Clean,
            RuntimeAvailability = AuthoringResourceRuntimeAvailability.RuntimeReady,
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

    private static void AuthoringResourceReferenceGraph_ScansCrossConsumerReferencesAndDiagnostics()
    {
        var package = new CharacterResourcePackage();
        package.Manifest.PackageId = "test";
        package.Manifest.StableId = "charpkg.test";
        package.ApplicationConfig.CharacterStableId = "character.test";
        package.ApplicationConfig.ResourceKeys.Add("char.test.model.body");
        package.ApplicationConfig.ResourceKeys.Add("char.test.resource.missing");
        package.Geometry.WeaponAttachments.Add(new WeaponAttachmentProfile
        {
            WeaponId = "weapon.test.sword",
            PreviewResourceKey = "char.test.weapon.sword"
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
        Require(graph.Edges.Exists(edge => edge.SourceConfigKind == "weapon" && edge.SourceStableId == "weapon.test.sword" && edge.TargetProviderResourceKey == "char.test.weapon.sword"), "weapon preview resource should become a graph edge.");
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
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.SocketSchemaId), "tags"), "socket tags field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceRadius"), "weapon trace radius field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.WeaponAttachmentSchemaId), "traceEndSocketId"), "weapon trace end socket field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.ValidationIssueSchemaId), "gate"), "validation gate field missing.");
        Require(HasField(FindSchema(schemas, CharacterResourcePackageSchemas.CompilerResultSchemaId), "resolverVerificationPlan"), "compiler resolver verification plan field missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "glb"), "resource source format glb option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "fbx"), "resource source format fbx future option missing.");
        Require(HasEnumOption("character.resourceSourceFormat", "anim"), "resource source format anim option missing.");
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

    private static void CharacterResourcePlanCompiler_FmodAudioCueDoesNotEnterRuntimeCatalog()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        package.ResourceCatalog.Entries.Add(new CharacterPackageResourceEntry
        {
            ResourceKey = "audio.cue.iron_vanguard.sword_slash",
            LocalId = "audio.sword_slash",
            StableId = "charpkg.iron_vanguard.resource.audio.sword_slash",
            TypeId = CharacterPackageResourceTypeIds.Audio,
            Usage = CharacterPackageResourceUsageIds.AudioCue,
            PackageId = "iron_vanguard",
            ImportHints = new CharacterPackageImportHint
            {
                ProviderId = AuthoringResourceProviderIds.Fmod,
                Metadata = new Dictionary<string, string>
                {
                    { "runtimeBindingKind", AuthoringResourceBindingKind.AudioCue.ToString() },
                    { "audioCueId", "audio.cue.iron_vanguard.sword_slash" },
                    { "audioEventDefinitionId", "audio.event.iron_vanguard.sword_slash" },
                    { "fmodEventPath", "event:/Character/IronVanguard/SwordSlash" },
                    { "fmodGuid", "{11111111-2222-3333-4444-555555555555}" },
                    { "bank", "Character" }
                }
            }
        });
        package.ApplicationConfig.ResourceKeys.Add("audio.cue.iron_vanguard.sword_slash");

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-iron-vanguard")
        });

        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "audio.cue.iron_vanguard.sword_slash"), "FMOD audio cue must not enter ordinary runtime resource catalog.");
        Require(result.AudioCueManifest.Cues.Exists(cue =>
            cue.CueId == "audio.cue.iron_vanguard.sword_slash" &&
            cue.EventPath == "event:/Character/IronVanguard/SwordSlash"), "FMOD audio cue should enter audio cue manifest.");
        Require(result.AudioCueManifest.Banks.Contains("Character"), "FMOD audio cue should contribute required banks.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Contains("audio.cue.iron_vanguard.sword_slash"), "FMOD audio cue should be required through Audio plan group.");
        Require(result.CharacterResourcePlan.Audio.RequiredBanks.Contains("Character"), "FMOD bank should be required through Audio plan group.");
    }

    private static void CharacterResourcePlanCompiler_ResolvesAuthoringSelectionManifest()
    {
        CharacterResourcePackage package = LoadSample("character-iron-vanguard");
        AuthoringResourceCollection collection = BuildAuthoringSelectionCollection();
        var manifest = new AuthoringResourceSelectionManifestDocument
        {
            PackageId = "iron_vanguard",
            ConsumerKind = "character",
            ConsumerStableId = "character.iron_vanguard"
        };
        manifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "vfx",
            SourceStableId = "vfx.iron_vanguard.slash",
            SourceField = "prefab",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "Vfx.Prefab",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Vfx },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.VfxCue },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.ResourceManagerAsset },
                RequireRuntimeLoadable = true,
                PreloadPolicy = AuthoringResourcePreloadPolicies.VfxWarmup,
                OutputKind = AuthoringResourceSelectionOutputKind.RuntimeResourceKey
            },
            Context = new AuthoringResourceConsumerContext
            {
                ConsumerKind = "vfx",
                ConsumerStableId = "vfx.iron_vanguard.slash",
                PackageId = "iron_vanguard"
            },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "runtime.vfx.slash",
                SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset
            }
        });
        manifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "combatAction",
            SourceStableId = "combat.iron_vanguard.hit",
            SourceField = "hitSfx",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "CombatAction.HitSfx",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Audio },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.AudioCue },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.Fmod },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.AudioCue },
                PreloadPolicy = AuthoringResourcePreloadPolicies.AudioBank,
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            Context = new AuthoringResourceConsumerContext
            {
                ConsumerKind = "combatAction",
                ConsumerStableId = "combat.iron_vanguard.hit",
                PackageId = "iron_vanguard"
            },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "audio.hit",
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            }
        });

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            AuthoringResources = collection,
            ResourceSelectionManifest = manifest
        });

        Require(result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "char.iron_vanguard.vfx.slash"), "selection manifest should add runtime catalog entries from ResourceSelectionRef.");
        Require(result.CharacterResourcePlan.VfxWarmup.Resources.Exists(resource => resource.ResourceKey == "char.iron_vanguard.vfx.slash"), "selection preload policy should place runtime resource into VfxWarmup.");
        Require(result.AudioCueManifest.Cues.Exists(cue => cue.CueId == "cue.hit"), "audio ResourceSelectionRef should compile into audio cue manifest.");
        Require(result.CharacterResourcePlan.Audio.RequiredCues.Contains("cue.hit"), "audio ResourceSelectionRef should compile into Audio plan group.");
        Require(!result.RuntimeResourceCatalog.Entries.Exists(entry => entry.Id == "cue.hit"), "audio ResourceSelectionRef must not enter ordinary runtime catalog.");
        Require(!result.ResourceValidationReport.HasErrors, "valid ResourceSelectionRef manifest should not block resource plan.");

        var invalidManifest = new AuthoringResourceSelectionManifestDocument
        {
            PackageId = "iron_vanguard",
            ConsumerKind = "ui",
            ConsumerStableId = "ui.iron_vanguard"
        };
        invalidManifest.Selections.Add(new AuthoringResourceSelectionCompileInput
        {
            SourceConfigKind = "ui",
            SourceStableId = "ui.iron_vanguard.icon",
            SourceField = "icon",
            FieldSpec = new AuthoringResourceFieldSpec
            {
                FieldKey = "Ui.Icon",
                AcceptedKinds = new List<string> { CharacterPackageResourceTypeIds.Texture },
                AcceptedUsages = new List<string> { CharacterPackageResourceUsageIds.PreviewThumbnail },
                AcceptedProviderIds = new List<string> { AuthoringResourceProviderIds.UnityAssetDatabase },
                AcceptedBindingKinds = new List<AuthoringResourceBindingKind> { AuthoringResourceBindingKind.UnityAsset },
                RequireRuntimeLoadable = true,
                PreloadPolicy = AuthoringResourcePreloadPolicies.UiDeferred,
                OutputKind = AuthoringResourceSelectionOutputKind.UnityGuid
            },
            Context = new AuthoringResourceConsumerContext { ConsumerKind = "ui", PackageId = "iron_vanguard" },
            Selection = new AuthoringResourceSelectionRef
            {
                ResourceStableId = "unity.icon.portrait",
                SourceProviderId = AuthoringResourceProviderIds.UnityAssetDatabase,
                BindingKind = AuthoringResourceBindingKind.UnityAsset
            }
        });

        CharacterResourcePlanCompileResult invalid = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = FindSamplePath("character-iron-vanguard"),
            AuthoringResources = collection,
            ResourceSelectionManifest = invalidManifest
        });

        Require(invalid.ResourceValidationReport.HasErrors, "runtime-required editor-only selection should block resource plan.");
        Require(invalid.ResourceValidationReport.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.EditorOnlySelectedForRuntime), "editor-only runtime field should produce structured diagnostic.");
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
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_set_definition.json")), "Unity import should write runtime animation set definition.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_clip_registry.json")), "Unity import should write runtime animation clip registry.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "animation_resource_plan.json")), "Unity import should write runtime animation resource plan.");
        Require(File.Exists(Path.Combine(targetRoot, "config", "character_resource_plan.json")), "Unity import should write character resource plan.");
        Require(File.Exists(Path.Combine(targetRoot, "package_cache", "animation_compile_result.json")), "Unity import should preserve animation compile result in package cache.");

        CharacterUnityImportReport firstReport = JsonSerializer.Deserialize<CharacterUnityImportReport>(File.ReadAllText(reportPath), JsonOptions);
        Require(firstReport != null && firstReport.Status == CharacterAuthoringCompilerStatus.Ready.ToString(), "first import report should be Ready.");
        Require(firstReport.AddedCount > 0, "first import should add files.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_set_definition.json", StringComparison.Ordinal)), "import report should include animation set definition write.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_clip_registry.json", StringComparison.Ordinal)), "import report should include animation clip registry write.");
        Require(firstReport.Operations.Exists(operation => operation.TargetPath.EndsWith("config/animation_resource_plan.json", StringComparison.Ordinal)), "import report should include animation resource plan write.");
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

    private static string GetAnonymousPropertyForTest(object value, string propertyName)
    {
        return value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception("ASSERTION FAILED: " + message);
    }
}
