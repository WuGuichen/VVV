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

    private static void CharacterImportMaterials_JsonRoundTrip_PreservesChecklistFields()
    {
        var document = new CharacterImportMaterialsDocument
        {
            SourceRoot = "ArtSource/Characters/Test",
            Items = new List<CharacterImportMaterialItem>
            {
                new CharacterImportMaterialItem
                {
                    Id = "body-model",
                    Label = "Body Model",
                    SourcePath = "body.fbx",
                    PresetId = "characterModel",
                    Slot = "",
                    ResourceKey = "char.test.model.body",
                    BindTarget = "body",
                    Required = true,
                    LastImportedHash = "sha256:abc",
                    LastImportedUtc = "2026-06-22T00:00:00.0000000Z"
                },
                new CharacterImportMaterialItem
                {
                    Id = "locomotion",
                    Label = "Locomotion",
                    SourcePath = "locomotion.fbx",
                    PresetId = "animationClipGroup",
                    Slot = "locomotion",
                    ResourceKey = "char.test.anim.locomotion",
                    Required = false
                }
            }
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        CharacterImportMaterialsDocument roundTrip = JsonSerializer.Deserialize<CharacterImportMaterialsDocument>(json, JsonOptions);

        Require(json.Contains("\"sourceRoot\""), "import materials JSON should use camelCase sourceRoot.");
        Require(roundTrip != null && roundTrip.Items.Count == 2, "import materials checklist should roundtrip.");
        Require(roundTrip.SourceRoot == document.SourceRoot, "sourceRoot should roundtrip.");
        Require(roundTrip.Items[0].BindTarget == "body", "bindTarget should roundtrip.");
        Require(roundTrip.Items[0].LastImportedHash == "sha256:abc", "lastImportedHash should roundtrip.");
        Require(roundTrip.Items[1].Slot == "locomotion", "animation slot should roundtrip.");
        Require(roundTrip.Items[1].Required == false, "required flag should roundtrip.");
    }

    private static void CharacterSourceManifest_JsonRoundTrip_PreservesLayeredSourceRefs()
    {
        var document = new CharacterSourceManifestDocument
        {
            CharacterId = "test",
            PackageId = "test",
            SourceRoot = ".",
            Items = new List<CharacterSourceManifestItem>
            {
                new CharacterSourceManifestItem
                {
                    Id = "body-model",
                    Label = "Body Model",
                    PresetId = "characterModel",
                    BindTarget = "body",
                    Required = true,
                    Source = new CharacterSourceReference
                    {
                        Kind = "file",
                        Scope = "package",
                        Path = "character_00.fbx"
                    },
                    Target = new CharacterSourceTarget
                    {
                        ResourceKey = "char.test.model.body",
                        BindTarget = "body"
                    }
                },
                new CharacterSourceManifestItem
                {
                    Id = "shared-audio",
                    Label = "Shared Audio",
                    PresetId = "audioCue",
                    Required = false,
                    Source = new CharacterSourceReference
                    {
                        Kind = "resourceRef",
                        Scope = "public",
                        Selection = new AuthoringResourceSelectionRef
                        {
            SourceProviderId = AuthoringResourceProviderIds.RuntimeCatalog,
                            BindingKind = AuthoringResourceBindingKind.ResourceManagerAsset,
                            ProviderResourceKey = "global.audio.shared"
                        }
                    },
                    Target = new CharacterSourceTarget
                    {
                        ResourceKey = "char.test.audio.shared"
                    }
                }
            }
        };

        string json = JsonSerializer.Serialize(document, JsonOptions);
        CharacterSourceManifestDocument roundTrip = JsonSerializer.Deserialize<CharacterSourceManifestDocument>(json, JsonOptions);

        Require(json.Contains("\"characterId\""), "source manifest JSON should use camelCase characterId.");
        Require(roundTrip != null && roundTrip.Items.Count == 2, "source manifest should roundtrip.");
        Require(roundTrip.Items[0].Source.Scope == "package", "package-local source scope should roundtrip.");
        Require(roundTrip.Items[0].Target.BindTarget == "body", "source target bindTarget should roundtrip.");
        Require(roundTrip.Items[1].Source.Kind == "resourceRef", "resourceRef source kind should roundtrip.");
        Require(roundTrip.Items[1].Source.Selection.ProviderResourceKey == "global.audio.shared", "resourceRef provider key should roundtrip.");
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

}
