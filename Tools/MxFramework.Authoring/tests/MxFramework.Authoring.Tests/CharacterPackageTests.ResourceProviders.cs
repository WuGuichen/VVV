using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.Authoring.Cli;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Tests;

internal static partial class CharacterPackageTests
{
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

    private static void ResourceMappingProvider_NormalizesRuntimeTypeAndUsageTaxonomy()
    {
        var profile = new ResourceMappingProfileV2
        {
            ProfileId = "resource-mapping.authoring-test",
            CatalogId = "global.runtime"
        };
        profile.Entries.Add(new ResourceMappingEntry
        {
            ResourceKey = new ResourceMappingResourceKey
            {
                Id = "weapon.common_weapon_1.model",
                TypeId = "GameObject",
                PackageId = "common"
            },
            SourceLocator = new ResourceMappingSourceLocator
            {
                Kind = ResourceMappingSourceLocatorKind.UnityAsset,
                CachedPath = "Assets/MxFrameworkGenerated/Common/Weapons/common_weapon_1/prefabs/common_weapon_1_weapon.prefab",
                UnityGuid = "11111111111111111111111111111111"
            },
            DeliveryIntent = ResourceMappingDeliveryIntent.Internal,
            Labels = new List<string> { "domain.weapon", "usage.weaponmodel" },
            ProviderData = new Dictionary<string, string>
            {
                { "modelWrapperPose", "{\"position\":{\"x\":0,\"y\":-0.125054,\"z\":0}}" },
                { "rawGeneratedAssetPath", "Assets/MxFrameworkGenerated/Common/Weapons/common_weapon_1/model/common_weapon_1.fbx" }
            }
        });
        profile.Entries.Add(new ResourceMappingEntry
        {
            ResourceKey = new ResourceMappingResourceKey
            {
                Id = "art.character.test.animation.idle",
                TypeId = "AnimationClip",
                PackageId = "test"
            },
            SourceLocator = new ResourceMappingSourceLocator
            {
                Kind = ResourceMappingSourceLocatorKind.UnityAsset,
                CachedPath = "Assets/Characters/Test/idle.anim"
            },
            DeliveryIntent = ResourceMappingDeliveryIntent.Internal,
            Labels = new List<string> { "domain.animation" }
        });

        AuthoringResourceCollection collection = new ResourceMappingAuthoringResourceProvider().BuildResourceCollection(
            new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                ResourceMappingProfile = profile
            });

        AuthoringResourceItem weapon = collection.Items.Find(item => item.ProviderBindings.Exists(binding =>
            binding.RuntimeResourceKey == "weapon.common_weapon_1.model"));
        Require(weapon != null, "resource mapping provider should project the public weapon.");
        Require(weapon.Kind == CharacterPackageResourceTypeIds.Model, "resource mapping GameObject should normalize to the authoring model kind.");
        Require(weapon.Usage == CharacterPackageResourceUsageIds.WeaponModel, "usage.weaponmodel should normalize to weaponModel.");
        AuthoringResourceProviderBinding weaponBinding = weapon.ProviderBindings.Find(binding => binding.IsPrimary);
        Require(weaponBinding != null && weaponBinding.ProviderData["unityAssetGuid"] == "11111111111111111111111111111111", "resource mapping binding should preserve the Unity guid.");
        Require(weaponBinding.ProviderData.ContainsKey("modelWrapperPose") && weaponBinding.ProviderData.ContainsKey("rawGeneratedAssetPath"), "resource mapping binding should preserve wrapper pose and raw model diagnostics.");

        AuthoringResourceItem animation = collection.Items.Find(item => item.ProviderBindings.Exists(binding =>
            binding.RuntimeResourceKey == "art.character.test.animation.idle"));
        Require(animation != null, "resource mapping provider should project the animation clip.");
        Require(animation.Kind == CharacterPackageResourceTypeIds.Animation, "resource mapping AnimationClip should normalize to the authoring animation kind.");
        Require(animation.Usage == AnimationAuthoringResourceUsages.AnimationClip, "resource mapping AnimationClip should infer animationClip usage.");
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
        Require(item.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticMissing), "FMOD event without semantic overlay should report missing semantics.");

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

    private static void FmodAudioLibraryProvider_MergesSemanticOverlay()
    {
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = CreateAudioSemanticTestSnapshot();
        var manifest = new AudioSemanticManifestDocument();
        manifest.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "audio.cue.semantic.sword_slash",
            AudioEventDefinitionId = "audio.event.semantic.sword_slash",
            FmodEventPath = "event:/Character/IronVanguard/SwordSlash",
            FmodEventGuid = "{11111111-2222-3333-4444-555555555555}",
            DisplayName = "Semantic Sword Slash",
            Purpose = "Primary melee hit confirm.",
            Context = "Agent-readable semantic context.",
            Tags = new List<string> { "combat", "sword" },
            SourcePreviewPath = "Assets/Audio/Test/sword.wav",
            AuditionStatus = AudioSemanticAuditionStatuses.Needed,
            ReplacementContract = new AudioReplacementContract
            {
                RequireSameSpatial = true,
                RequireSameLoop = true,
                RequiredParameters = new List<string> { "Impact" },
                RequiredBanks = new List<string> { "Character" },
                MaxLengthMs = 1200
            }
        });

        AuthoringResourceCollection collection = FmodAudioLibraryAuthoringResourceProvider.FromSnapshot(
            snapshot,
            new AuthoringResourceProviderContext
            {
                ScopeId = "test",
                PackageId = "test",
                FmodAudioLibrarySnapshotPath = "Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json",
                AudioSemanticManifestPath = "Assets/Config/MxFramework/Audio/audio_semantic_manifest.json",
                AudioSemanticManifest = manifest
            });

        Require(collection.Items.Count == 2, "FMOD provider should project both test events.");
        AuthoringResourceItem item = collection.Items.Find(candidate => candidate.Metadata["fmodEventPath"] == "event:/Character/IronVanguard/SwordSlash");
        Require(item != null, "semantic test item should exist.");
        Require(item.DisplayName == "Semantic Sword Slash", "semantic display name should override the FMOD leaf name.");
        Require(item.Metadata["audioCueId"] == "audio.cue.semantic.sword_slash", "semantic audioCueId should override generated cue id.");
        Require(item.Metadata["audioSemanticPurpose"] == "Primary melee hit confirm.", "semantic purpose should be projected.");
        Require(item.Metadata["audioSemanticStatus"] == AudioSemanticEntryStatuses.Ready, "complete semantic metadata with a valid FMOD binding should be technically ready without audition.");
        Require(item.Metadata["sourcePreviewPath"] == "Assets/Audio/Test/sword.wav", "source preview path should be projected.");
        Require(item.Metadata["replacementRequiredParameters"] == "Impact", "replacement required parameters should be projected.");
        Require(item.Tags.Contains("combat") && item.Tags.Contains("sword"), "semantic tags should merge into authoring item tags.");
        Require(item.ProviderBindings.Exists(binding => binding.BindingKind == AuthoringResourceBindingKind.AudioCue && binding.ProviderData["audioCueId"] == "audio.cue.semantic.sword_slash"), "AudioCue binding should carry semantic stable cue id.");
        Require(!item.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticMissing), "semantic item should not report missing semantics.");
        Require(collection.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticAuditionRecommended), "semantic collection should keep optional human audition visible without duplicating the item diagnostic.");

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
                OutputKind = AuthoringResourceSelectionOutputKind.AudioCueId
            },
            new AuthoringResourceConsumerContext { ConsumerKind = "combatAction" },
            new AuthoringResourceSelectionRef
            {
                ResourceStableId = item.StableId,
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue
            });

        Require(cueResult.Accepted, "semantic FMOD audio cue selection should resolve.");
        Require(cueResult.Selection.AudioCueId == "audio.cue.semantic.sword_slash", "semantic cue id should flow through selection output.");
        Require(string.IsNullOrEmpty(cueResult.Selection.RuntimeResourceKey), "semantic audio cue must still avoid runtime resource keys.");
    }

    private static void AudioSemanticManifestValidator_ReportsDuplicatesAndValidatesReplacement()
    {
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = CreateAudioSemanticTestSnapshot();
        var manifest = new AudioSemanticManifestDocument();
        manifest.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "audio.cue.semantic.sword_slash",
            FmodEventPath = "event:/Character/IronVanguard/SwordSlash",
            FmodEventGuid = "{11111111-2222-3333-4444-555555555555}",
            DisplayName = "Sword Slash",
            Purpose = "Primary melee hit confirm.",
            Context = "Used by the test combat action.",
            AuditionStatus = AudioSemanticAuditionStatuses.Approved,
            ReplacementContract = new AudioReplacementContract
            {
                RequireSameSpatial = true,
                RequireSameLoop = true,
                RequiredParameters = new List<string> { "Impact" },
                RequiredBanks = new List<string> { "Character" },
                MaxLengthMs = 1200
            }
        });
        manifest.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "audio.cue.semantic.sword_slash",
            FmodEventPath = "event:/Character/IronVanguard/SwordSlashVariant",
            AuditionStatus = AudioSemanticAuditionStatuses.Approved
        });

        AudioSemanticManifestValidationReport duplicateReport = AudioSemanticManifestValidator.Validate(manifest, snapshot);
        Require(duplicateReport.HasErrors, "duplicate semantic cue ids should be errors.");
        Require(duplicateReport.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticCueDuplicate), "duplicate cue diagnostic should be emitted.");

        manifest.Entries.RemoveAt(1);
        Require(AudioSemanticManifestValidator.GetEntryStatus(manifest.Entries[0], snapshot) == AudioSemanticEntryStatuses.Ready, "complete approved semantics should be ready.");
        Require(AudioSemanticManifestValidator.GetEntryStatus(
            new AudioSemanticManifestEntry
            {
                AudioCueId = "audio.cue.semantic.incomplete",
                FmodEventPath = "event:/Character/IronVanguard/SwordSlash",
                AuditionStatus = AudioSemanticAuditionStatuses.Needed
            },
            snapshot) == AudioSemanticEntryStatuses.Draft, "incomplete semantics should remain draft even when the FMOD binding exists.");

        AudioSemanticReplacementValidationResult accepted = AudioSemanticManifestValidator.ValidateReplacement(
            manifest,
            snapshot,
            new AudioSemanticReplacementValidationRequest
            {
                AudioCueId = "audio.cue.semantic.sword_slash",
                TargetFmodEventPath = "event:/Character/IronVanguard/SwordSlashVariant",
                DraftEntry = manifest.Entries[0]
            },
            affectedReferenceCount: 2);
        Require(accepted.Accepted, "compatible replacement should be accepted.");
        Require(accepted.AffectedReferenceCount == 2, "replacement validation should carry reference impact count.");

        snapshot.Events[1].Is3D = false;
        snapshot.Events[1].Parameters.Clear();
        AudioSemanticReplacementValidationResult rejected = AudioSemanticManifestValidator.ValidateReplacement(
            manifest,
            snapshot,
            new AudioSemanticReplacementValidationRequest
            {
                AudioCueId = "audio.cue.semantic.sword_slash",
                TargetFmodEventPath = "event:/Character/IronVanguard/SwordSlashVariant",
                DraftEntry = manifest.Entries[0]
            },
            affectedReferenceCount: 0);
        Require(!rejected.Accepted, "spatial and parameter mismatch should reject replacement.");
        Require(rejected.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected), "replacement contract mismatch should produce a rejection diagnostic.");
        Require(rejected.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.FmodParameterMismatch), "missing parameter should produce FMOD parameter diagnostic.");
    }

    private static void EditorServer_AudioSemanticReplacement_KeepsOriginalBinding()
    {
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = CreateAudioSemanticTestSnapshot();
        snapshot.Events[1].Is3D = false;
        var manifest = new AudioSemanticManifestDocument();
        manifest.Entries.Add(new AudioSemanticManifestEntry
        {
            AudioCueId = "audio.cue.semantic.sword_slash",
            FmodEventPath = "event:/Character/IronVanguard/SwordSlash",
            FmodEventGuid = "{11111111-2222-3333-4444-555555555555}",
            DisplayName = "Sword Slash",
            Purpose = "Primary melee hit confirm.",
            Context = "Used by the test combat action.",
            AuditionStatus = AudioSemanticAuditionStatuses.Approved,
            ReplacementContract = new AudioReplacementContract
            {
                RequireSameSpatial = true,
                RequireSameLoop = true,
                RequiredParameters = new List<string> { "Impact" },
                RequiredBanks = new List<string> { "Character" }
            }
        });

        var request = new MxFramework.Authoring.Cli.EditorServer.AudioSemanticReplacementApiRequest
        {
            audioCueId = "audio.cue.semantic.sword_slash",
            targetFmodEventPath = "event:/Character/IronVanguard/SwordSlashVariant",
            targetFmodEventGuid = "{22222222-3333-4444-5555-666666666666}",
            draftEntry = new AudioSemanticManifestEntry
            {
                AudioCueId = "audio.cue.semantic.sword_slash",
                FmodEventPath = "event:/Character/IronVanguard/SwordSlashVariant",
                FmodEventGuid = "{22222222-3333-4444-5555-666666666666}",
                DisplayName = "Sword Slash",
                Purpose = "Primary melee hit confirm.",
                Context = "Used by the test combat action.",
                AuditionStatus = AudioSemanticAuditionStatuses.Approved,
                ReplacementContract = new AudioReplacementContract
                {
                    RequireSameSpatial = true,
                    RequireSameLoop = true,
                    RequiredParameters = new List<string> { "Impact" },
                    RequiredBanks = new List<string> { "Character" }
                }
            }
        };

        AudioSemanticReplacementValidationResult result =
            MxFramework.Authoring.Cli.EditorServer.ValidateAudioSemanticReplacementRequest(
                manifest,
                snapshot,
                request,
                new List<AuthoringResourceReferenceEdge>());

        Require(!result.Accepted, "EditorServer replacement orchestration must compare the draft target against the original binding.");
        Require(result.CurrentFmodEventPath == "event:/Character/IronVanguard/SwordSlash", "replacement validation should preserve the original current event.");
        Require(result.TargetFmodEventPath == "event:/Character/IronVanguard/SwordSlashVariant", "replacement validation should report the requested target event.");
        Require(result.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected), "3D to 2D replacement should be rejected by the server orchestration path.");

        string tempRoot = Path.Combine(Path.GetTempPath(), "mx-audio-semantic-save-" + Guid.NewGuid().ToString("N"));
        string manifestPath = Path.Combine(tempRoot, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json");
        string snapshotPath = Path.Combine(tempRoot, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library_snapshot.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath));
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
            File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonOptions));

            var changedManifest = new AudioSemanticManifestDocument();
            changedManifest.Entries.Add(request.draftEntry);
            MxFramework.Authoring.Cli.EditorServer.AudioSemanticManifestSaveResponse saveResult =
                MxFramework.Authoring.Cli.EditorServer.SaveAudioSemanticManifest(
                    tempRoot,
                    string.Empty,
                    new MxFramework.Authoring.Cli.EditorServer.AudioSemanticManifestSaveRequest
                    {
                        expectedHash = CharacterPackageHashUtility.ComputeFileSha256(manifestPath),
                        manifest = changedManifest
                    },
                    JsonOptions);

            Require(!saveResult.success, "save API must reject an incompatible binding change even when the frontend validation gate is bypassed.");
            Require(saveResult.validation.Diagnostics.Exists(diagnostic => diagnostic.Code == AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected), "save rejection should retain the replacement-contract diagnostic.");
            AudioSemanticManifestDocument persisted = JsonSerializer.Deserialize<AudioSemanticManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
            Require(persisted.Entries[0].FmodEventPath == "event:/Character/IronVanguard/SwordSlash", "rejected save must leave the original manifest binding unchanged.");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static AuthoringFmodAudioLibrarySnapshotDocument CreateAudioSemanticTestSnapshot()
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
        snapshot.Events.Add(CreateAudioSemanticTestEvent(
            "event:/Character/IronVanguard/SwordSlash",
            "{11111111-2222-3333-4444-555555555555}",
            is3D: true,
            lengthMs: 850));
        snapshot.Events.Add(CreateAudioSemanticTestEvent(
            "event:/Character/IronVanguard/SwordSlashVariant",
            "{22222222-3333-4444-5555-666666666666}",
            is3D: true,
            lengthMs: 900));
        return snapshot;
    }

    private static AuthoringFmodAudioLibraryEvent CreateAudioSemanticTestEvent(string path, string guid, bool is3D, int lengthMs)
    {
        var audioEvent = new AuthoringFmodAudioLibraryEvent
        {
            Path = path,
            Guid = guid,
            Kind = "Event",
            Is3D = is3D,
            IsLoop = false,
            LengthMs = lengthMs,
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
        return audioEvent;
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

}
