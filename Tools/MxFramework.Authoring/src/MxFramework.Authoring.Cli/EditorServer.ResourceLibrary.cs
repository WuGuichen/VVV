using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static CharacterResourceLibrary ReadCharacterResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterResourceLibrary library = CharacterResourceLibraryBuilder.FromPackageResourceCatalog(package.ResourceCatalog);
        library.PackageId = package.Manifest != null ? package.Manifest.PackageId : library.PackageId;
        library.ReferenceGraph = BuildCharacterResourceReferenceGraph(package);
        ApplyCharacterResourceRuntimeState(rootPath, packagePath, package, library, jsonOptions);
        library.Diagnostics.Clear();
        library.Diagnostics.AddRange(CharacterResourceLibraryBuilder.ValidateLibrary(library));
        return library;
    }

    private static AuthoringResourceCollection ReadAuthoringResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string cacheKey = BuildAuthoringResourceCacheKey(rootPath, packageRelative);
        DateTime utcNow = DateTime.UtcNow;
        lock (AuthoringResourceCacheGate)
        {
            if (AuthoringResourceCache.TryGetValue(cacheKey, out AuthoringResourceCacheEntry cached) &&
                utcNow - cached.CachedAtUtc <= AuthoringResourceCacheDuration)
                return cached.Collection;
        }

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
        string scopeId = "characterPackage:" + packageId;
        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId);
        string unityResourceCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string runtimeResourceCatalogPath = Path.Combine(generatedRoot, "config", "runtime_resource_catalog.json");
        string resourceMappingProfilePath = ResolveResourceMappingProfilePath(rootPath);
        string globalRuntimeCatalogPath = Path.Combine(rootPath, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_runtime_catalog.json");
        string globalPreloadGroupsPath = Path.Combine(rootPath, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_preload_groups.json");
        string globalBundleDependenciesPath = Path.Combine(rootPath, "Assets", "StreamingAssets", "MxFramework", "Resources", "global_bundle_dependencies.json");
        string globalResourceBuildReportPath = Path.Combine(rootPath, "Assets", "Config", "MxFramework", "ResourceBuildReports", "global_resource_build_report.json");
        string fmodAudioLibrarySnapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        string audioSemanticManifestPath = ResolveAudioSemanticManifestPath(rootPath);
        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: false, checkHashes: false, jsonOptions);
        RuntimeResourceCatalogDocument projectRuntimeCatalog = AnimationPackageCommands.ReadProjectRuntimeResourceCatalogs(rootPath, jsonOptions);
        RuntimeResourceCatalogDocument runtimeCatalog = AnimationPackageCommands.MergeRuntimeResourceCatalogs(projectRuntimeCatalog, plan != null ? plan.RuntimeResourceCatalog : null);
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = scopeId,
            PackageId = packageId,
            PackagePath = packageRelative,
            ProjectRootPath = rootPath,
            PackageResourceCatalog = package.ResourceCatalog,
            UnityResourceCatalog = ReadOptionalJsonFile<AuthoringUnityResourceCatalogDocument>(rootPath, unityResourceCatalogPath, jsonOptions),
            RuntimeResourceCatalog = runtimeCatalog,
            ResourceMappingProfile = ReadResourceMappingProfile(rootPath),
            FmodAudioLibrarySnapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, fmodAudioLibrarySnapshotPath, jsonOptions),
            AudioSemanticManifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, audioSemanticManifestPath, jsonOptions),
            UnityResourceCatalogPath = ToProjectRelativePath(rootPath, unityResourceCatalogPath),
            RuntimeResourceCatalogPath = ToProjectRelativePath(rootPath, runtimeResourceCatalogPath),
            ResourceMappingProfilePath = ToProjectRelativePath(rootPath, resourceMappingProfilePath),
            GlobalRuntimeCatalogPath = ToProjectRelativePath(rootPath, globalRuntimeCatalogPath),
            GlobalPreloadGroupsPath = ToProjectRelativePath(rootPath, globalPreloadGroupsPath),
            GlobalBundleDependenciesPath = ToProjectRelativePath(rootPath, globalBundleDependenciesPath),
            GlobalResourceBuildReportPath = ToProjectRelativePath(rootPath, globalResourceBuildReportPath),
            FmodAudioLibrarySnapshotPath = ToProjectRelativePath(rootPath, fmodAudioLibrarySnapshotPath),
            AudioSemanticManifestPath = ToProjectRelativePath(rootPath, audioSemanticManifestPath)
        };
        context.Metadata["externalImportStaging"] = "empty";
        AuthoringResourceCollection collection = AuthoringResourceCollectionMerger.Merge(
            new CharacterPackageAuthoringResourceProvider().BuildResourceCollection(context),
            new CharacterActionAuthoringResourceProvider().BuildResourceCollection(context),
            new UnityAssetDatabaseAuthoringResourceProvider().BuildResourceCollection(context),
            new UnityProjectAssetAuthoringResourceProvider().BuildResourceCollection(context),
            new ArtSourceAuthoringResourceProvider().BuildResourceCollection(context),
            new StorySourceAuthoringResourceProvider().BuildResourceCollection(context),
            new ResourceMappingAuthoringResourceProvider().BuildResourceCollection(context),
            new RuntimeCatalogAuthoringResourceProvider().BuildResourceCollection(context),
            new FmodAudioLibraryAuthoringResourceProvider().BuildResourceCollection(context),
            new ExternalImportStagingAuthoringResourceProvider().BuildResourceCollection(context));
        collection.ScopeId = scopeId;
        collection.ReferenceGraph = AuthoringResourceReferenceGraphBuilder.FromCharacterPackage(package, collection);
        collection.Diagnostics.AddRange(collection.ReferenceGraph.Diagnostics);
        lock (AuthoringResourceCacheGate)
        {
            AuthoringResourceCache[cacheKey] = new AuthoringResourceCacheEntry(collection, DateTime.UtcNow);
        }
        return collection;
    }

    private static ResourceMappingProfileV2 ReadResourceMappingProfile(string rootPath)
    {
        string path = ResolveResourceMappingProfilePath(rootPath);
        if (!File.Exists(path)) return null;
        ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(path));
        return read.Success ? read.Profile : null;
    }

    private static string BuildAuthoringResourceCacheKey(string rootPath, string packageRelative)
    {
        string root = NormalizeDirectoryRoot(rootPath);
        string package = (packageRelative ?? string.Empty).Replace('\\', '/').TrimStart('/');
        return root + "|" + package;
    }

    private static void InvalidateAuthoringResourceCache(string rootPath, string packageRelative = "")
    {
        string root = NormalizeDirectoryRoot(rootPath);
        lock (AuthoringResourceCacheGate)
        {
            if (!string.IsNullOrWhiteSpace(packageRelative))
            {
                AuthoringResourceCache.Remove(BuildAuthoringResourceCacheKey(rootPath, packageRelative));
                return;
            }

            string prefix = root + "|";
            foreach (string key in AuthoringResourceCache.Keys.ToArray())
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    AuthoringResourceCache.Remove(key);
            }
        }
    }

    private static string ResolveFmodAudioLibrarySnapshotPath(string rootPath, string packageId)
    {
        string packageSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", packageId ?? string.Empty, "config", "fmod_audio_library_snapshot.json");
        if (File.Exists(packageSnapshot))
            return packageSnapshot;

        string globalSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library_snapshot.json");
        if (File.Exists(globalSnapshot))
            return globalSnapshot;

        string legacyGlobalSnapshot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "Audio", "fmod_audio_library.json");
        if (File.Exists(legacyGlobalSnapshot))
            return legacyGlobalSnapshot;

        return packageSnapshot;
    }

    private static object ReadAuthoringResourceInspect(string rootPath, string packageRelative, string id, JsonSerializerOptions jsonOptions)
    {
        AuthoringResourceCollection collection = ReadAuthoringResources(rootPath, packageRelative, jsonOptions);
        AuthoringResourceItem item = FindAuthoringResourceItem(collection, id);
        if (item == null)
            return null;

        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        List<AuthoringResourceReferenceEdge> references = collection.ReferenceGraph != null
            ? collection.ReferenceGraph.FindReferencesToResource(item)
            : new List<AuthoringResourceReferenceEdge>();
        List<object> plans = BuildAuthoringResourceInspectPlans(item, plan);

        return new
        {
            packageId = GetAuthoringResourceMetadata(item, "packageId"),
            item,
            authoring = BuildAuthoringResourceInspectAuthoring(item),
            unity = BuildAuthoringResourceInspectUnity(item),
            runtime = BuildAuthoringResourceInspectRuntime(item, plans),
            references,
            referenceImpact = new
            {
                referenceCount = references.Count,
                blocksDelete = references.Count > 0,
                destructiveDeleteAllowed = references.Count == 0
            },
            plans,
            diagnostics = BuildAuthoringResourceInspectDiagnostics(item, collection, plan),
            collection = new
            {
                scopeId = collection.ScopeId,
                providers = collection.Providers,
                diagnostics = collection.Diagnostics
            }
        };
    }

    private static AuthoringResourceItem FindAuthoringResourceItem(AuthoringResourceCollection collection, string id)
    {
        if (collection == null || collection.Items == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < collection.Items.Count; i++)
        {
            AuthoringResourceItem item = collection.Items[i];
            if (AuthoringResourceMatches(id, item, string.Empty))
                return item;
        }

        return null;
    }

    private static object BuildAuthoringResourceInspectAuthoring(AuthoringResourceItem item)
    {
        return new
        {
            resourceId = item.ResourceId,
            stableId = item.StableId,
            sourceProviderId = item.SourceProviderId,
            sourceKind = item.SourceKind,
            bindingKind = item.BindingKind,
            providerBindings = item.ProviderBindings,
            sourcePath = FirstNonEmpty(GetAuthoringResourceMetadata(item, "relativePath"), GetAuthoringResourceExternalSourcePath(item)),
            tags = item.Tags,
            metadata = item.Metadata,
            diagnostics = item.Diagnostics
        };
    }

    private static object BuildAuthoringResourceInspectUnity(AuthoringResourceItem item)
    {
        AuthoringResourceProviderBinding binding = GetAuthoringUnityBinding(item);
        return new
        {
            unityAssetGuid = binding != null ? binding.UnityGuid : string.Empty,
            unityAssetPath = binding != null ? binding.UnityAssetPath : string.Empty,
            importStatus = item.ImportStatus.ToString(),
            diagnostics = item.Diagnostics != null ? item.Diagnostics.FindAll(diagnostic => diagnostic != null && (diagnostic.Code ?? string.Empty).IndexOf("UNITY", StringComparison.OrdinalIgnoreCase) >= 0) : new List<AuthoringResourceDiagnostic>(),
            binding
        };
    }

    private static object BuildAuthoringResourceInspectRuntime(AuthoringResourceItem item, List<object> plans)
    {
        AuthoringResourceProviderBinding binding = GetAuthoringRuntimeBinding(item) ?? GetAuthoringPrimaryBinding(item);
        List<string> groupNames = GetPlanGroupNames(plans);
        return new
        {
            runtimeBindingKind = item.BindingKind,
            runtimeAvailability = item.RuntimeAvailability,
            resourceKey = binding != null ? FirstNonEmpty(binding.RuntimeResourceKey, binding.PackageResourceKey, binding.ProviderResourceKey) : string.Empty,
            runtimeResourceKey = binding != null ? binding.RuntimeResourceKey : string.Empty,
            providerResourceKey = binding != null ? binding.ProviderResourceKey : string.Empty,
            packageResourceKey = binding != null ? binding.PackageResourceKey : string.Empty,
            providerId = binding != null ? binding.ProviderId : item.SourceProviderId,
            address = binding != null ? binding.Address : string.Empty,
            assetType = binding != null ? FirstNonEmpty(binding.AssetType, item.Kind) : item.Kind,
            hash = binding != null ? FirstNonEmpty(binding.Hash, GetAuthoringResourceMetadata(item, "contentHash")) : GetAuthoringResourceMetadata(item, "contentHash"),
            preloadPolicy = groupNames.Count > 0 ? groupNames[0] : AuthoringResourcePreloadPolicies.None,
            includedRuntimePlanGroups = groupNames,
            providerData = binding != null ? binding.ProviderData : new Dictionary<string, string>()
        };
    }

    private static List<object> BuildAuthoringResourceInspectPlans(AuthoringResourceItem item, CharacterResourcePlanCompileResult result)
    {
        var plans = new List<object>();
        if (item == null || result == null || result.CharacterResourcePlan == null)
            return plans;

        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.SpawnCritical, result.CharacterResourcePlan.SpawnCritical, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.PresentationCritical, result.CharacterResourcePlan.PresentationCritical, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.EquipmentInitial, result.CharacterResourcePlan.EquipmentInitial, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.AnimationWarmup, result.CharacterResourcePlan.AnimationWarmup, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.VfxWarmup, result.CharacterResourcePlan.VfxWarmup, item);
        AddAuthoringPlanGroupIfContains(plans, CharacterResourcePlanGroups.UiDeferred, result.CharacterResourcePlan.UiDeferred, item);

        AudioCueManifestEntry cue = FindAuthoringAudioCue(result.AudioCueManifest, item);
        if (cue != null)
        {
            plans.Add(new
            {
                groupName = CharacterResourcePlanGroups.Audio,
                required = result.CharacterResourcePlan.Audio != null && result.CharacterResourcePlan.Audio.Required,
                failurePolicy = result.CharacterResourcePlan.Audio != null ? result.CharacterResourcePlan.Audio.FailurePolicy : string.Empty,
                resource = cue
            });
        }

        return plans;
    }

    private static void AddAuthoringPlanGroupIfContains(List<object> plans, string groupName, CharacterResourcePlanGroup group, AuthoringResourceItem item)
    {
        if (plans == null || group == null || group.Resources == null || item == null)
            return;

        for (int i = 0; i < group.Resources.Count; i++)
        {
            CharacterResourcePlanResourceRef resource = group.Resources[i];
            if (resource == null)
                continue;

            if (!AuthoringResourceMatches(resource.ResourceKey, item, string.Empty) &&
                !AuthoringResourceMatches(resource.StableId, item, string.Empty))
                continue;

            plans.Add(new
            {
                groupName,
                required = group.Required,
                failurePolicy = group.FailurePolicy,
                resource
            });
        }
    }

    private static List<object> BuildAuthoringResourceInspectDiagnostics(
        AuthoringResourceItem item,
        AuthoringResourceCollection collection,
        CharacterResourcePlanCompileResult plan)
    {
        var diagnostics = new List<object>();
        if (item == null)
            return diagnostics;

        if (item.Diagnostics != null)
        {
            for (int i = 0; i < item.Diagnostics.Count; i++)
                AddAuthoringResourceDiagnostic(diagnostics, "item", item.Diagnostics[i], item);
        }

        if (collection != null && collection.Diagnostics != null)
        {
            for (int i = 0; i < collection.Diagnostics.Count; i++)
                AddAuthoringResourceDiagnostic(diagnostics, "collection", collection.Diagnostics[i], item);
        }

        if (plan != null && plan.CharacterResourcePlan != null && plan.CharacterResourcePlan.Diagnostics != null)
        {
            for (int i = 0; i < plan.CharacterResourcePlan.Diagnostics.Count; i++)
                AddAuthoringResourcePlanDiagnostic(diagnostics, "resourcePlan", plan.CharacterResourcePlan.Diagnostics[i], item);
        }

        if (plan != null && plan.ResourceValidationReport != null && plan.ResourceValidationReport.Diagnostics != null)
        {
            for (int i = 0; i < plan.ResourceValidationReport.Diagnostics.Count; i++)
                AddAuthoringResourcePlanDiagnostic(diagnostics, "validationReport", plan.ResourceValidationReport.Diagnostics[i], item);
        }

        return diagnostics;
    }

    private static void AddAuthoringResourceDiagnostic(List<object> diagnostics, string source, AuthoringResourceDiagnostic diagnostic, AuthoringResourceItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!AuthoringResourceMatches(diagnostic.ResourceId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.ResourceStableId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.RuntimeResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity.ToString(),
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            resourceId = diagnostic.ResourceId,
            resourceStableId = diagnostic.ResourceStableId,
            runtimeResourceKey = diagnostic.RuntimeResourceKey,
            providerId = diagnostic.ProviderId,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceStableId = diagnostic.SourceStableId,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddAuthoringResourcePlanDiagnostic(List<object> diagnostics, string source, CharacterResourcePlanDiagnostic diagnostic, AuthoringResourceItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!AuthoringResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !AuthoringResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity,
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceField = diagnostic.SourceField
        });
    }

    private static AudioCueManifestEntry FindAuthoringAudioCue(AudioCueManifestDocument manifest, AuthoringResourceItem item)
    {
        if (manifest == null || manifest.Cues == null || item == null)
            return null;

        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueManifestEntry cue = manifest.Cues[i];
            if (cue == null)
                continue;

            if (AuthoringResourceMatches(cue.CueId, item, string.Empty) ||
                AuthoringResourceMatches(cue.StableId, item, string.Empty) ||
                AuthoringResourceMatches(cue.ResourceKey, item, string.Empty))
                return cue;
        }

        return null;
    }

    private static bool AuthoringResourceMatches(string value, AuthoringResourceItem item, string id)
    {
        if (item == null)
            return false;

        string candidate = FirstNonEmpty(value, id);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.Equals(candidate, item.ResourceId, StringComparison.Ordinal) ||
            string.Equals(candidate, item.StableId, StringComparison.Ordinal) ||
            string.Equals(candidate, item.DisplayName, StringComparison.Ordinal))
            return true;

        if (item.Metadata != null)
        {
            string metadataValue;
            if ((item.Metadata.TryGetValue("localId", out metadataValue) && string.Equals(candidate, metadataValue, StringComparison.Ordinal)) ||
                (item.Metadata.TryGetValue("relativePath", out metadataValue) && string.Equals(candidate, metadataValue, StringComparison.Ordinal)))
                return true;
        }

        if (item.ProviderBindings == null)
            return false;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding == null)
                continue;

            if (string.Equals(candidate, binding.ProviderResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.PackageResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.RuntimeResourceKey, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.UnityGuid, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.UnityAssetPath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.FmodEventGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, binding.FmodEventPath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.ExternalSourcePath, StringComparison.Ordinal) ||
                string.Equals(candidate, binding.DisplayValue, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static AuthoringResourceProviderBinding GetAuthoringPrimaryBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null || item.ProviderBindings.Count == 0)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && binding.IsPrimary)
                return binding;
        }

        return item.ProviderBindings[0];
    }

    private static AuthoringResourceProviderBinding GetAuthoringRuntimeBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.RuntimeResourceKey))
                return binding;
        }

        return null;
    }

    private static AuthoringResourceProviderBinding GetAuthoringUnityBinding(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return null;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && (!string.IsNullOrWhiteSpace(binding.UnityGuid) || !string.IsNullOrWhiteSpace(binding.UnityAssetPath)))
                return binding;
        }

        return null;
    }

    private static string GetAuthoringResourceExternalSourcePath(AuthoringResourceItem item)
    {
        if (item == null || item.ProviderBindings == null)
            return string.Empty;

        for (int i = 0; i < item.ProviderBindings.Count; i++)
        {
            AuthoringResourceProviderBinding binding = item.ProviderBindings[i];
            if (binding != null && !string.IsNullOrWhiteSpace(binding.ExternalSourcePath))
                return binding.ExternalSourcePath;
        }

        return string.Empty;
    }

    private static string GetAuthoringResourceMetadata(AuthoringResourceItem item, string key)
    {
        if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;

        string value;
        return item.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private static CharacterResourcePlanCompileResult ReadCharacterResourcePlan(
        string rootPath,
        string packageRelative,
        bool checkFiles,
        bool checkHashes,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        return CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            ValidateResourceFiles = checkFiles || checkHashes,
            ValidateResourceHashes = checkHashes
        });
    }

    private static object ReadCharacterResourceInspect(string rootPath, string packageRelative, string id, JsonSerializerOptions jsonOptions)
    {
        CharacterResourceLibrary library = ReadCharacterResources(rootPath, packageRelative, jsonOptions);
        ResourceLibraryItem item = FindCharacterResourceLibraryItem(library, id);
        if (item == null)
            return null;

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry authoringEntry = FindCharacterPackageResourceEntry(package.ResourceCatalog, item, id);
        CharacterResourcePlanCompileResult plan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", library.PackageId);
        string unityCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string importReportPath = Path.Combine(generatedRoot, "package_cache", "import_report.json");
        JsonElement? unityEntry = FindResourceJsonEntry(rootPath, unityCatalogPath, item);
        JsonElement? importOperation = FindImportReportOperation(rootPath, importReportPath, item, authoringEntry, unityEntry);
        RuntimeResourceCatalogEntryDocument runtimeEntry = FindRuntimeCatalogEntry(plan.RuntimeResourceCatalog, item);
        AudioCueManifestEntry audioCue = FindAudioCue(plan.AudioCueManifest, item);
        List<object> plans = BuildCharacterResourceInspectPlans(item, plan);
        List<ResourceReferenceEdge> references = FindCharacterResourceReferences(library.ReferenceGraph, item);

        return new
        {
            packageId = library.PackageId,
            item,
            authoring = BuildCharacterResourceInspectAuthoring(packagePath, packageRelative, authoringEntry, item),
            unity = BuildCharacterResourceInspectUnity(rootPath, unityCatalogPath, importReportPath, unityEntry, importOperation, item),
            runtime = BuildCharacterResourceInspectRuntime(item, runtimeEntry, audioCue, plans),
            references,
            plans,
            diagnostics = BuildCharacterResourceInspectDiagnostics(item, library, plan, unityEntry)
        };
    }

    private static ResourceLibraryWriteRequest ReadResourceLibraryWriteRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new ResourceLibraryWriteRequest()
            : (JsonSerializer.Deserialize<ResourceLibraryWriteRequest>(body, jsonOptions) ?? new ResourceLibraryWriteRequest());
    }

    private static ExternalImportStageRequest ReadExternalImportStageRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body)
            ? new ExternalImportStageRequest()
            : (JsonSerializer.Deserialize<ExternalImportStageRequest>(body, jsonOptions) ?? new ExternalImportStageRequest());
    }

    private static AuthoringResourceCollection StageExternalImport(
        string rootPath,
        string packageRelative,
        ExternalImportStageRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        string packageId = package.Manifest != null ? package.Manifest.PackageId : string.Empty;
        var context = new AuthoringResourceProviderContext
        {
            ScopeId = "externalImportStaging:" + packageId,
            PackageId = packageId,
            PackagePath = packageRelative,
            ProjectRootPath = rootPath,
            PackageResourceCatalog = package.ResourceCatalog
        };
        var document = new AuthoringExternalImportStagingDocument
        {
            ScopeId = context.ScopeId,
            SourceRootLabel = request.sourceRootLabel ?? string.Empty,
            Files = request.files ?? new List<AuthoringExternalImportStagingFile>(),
            MaxFileSizeBytes = request.maxFileSizeBytes
        };
        return ExternalImportStagingAuthoringResourceProvider.FromStagingDocument(document, context);
    }

    private static object ImportCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string extension = ValidateResourceFileName(request.fileName);
        string typeId = NormalizeResourceTypeId(request.kind, extension);
        CharacterPackageResourceEntry entry = string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase)
            ? ImportCharacterModel(rootPath, packageRelative, new CharacterStudioModelImportRequest
            {
                fileName = request.fileName,
                role = string.IsNullOrWhiteSpace(request.role) ? "preview" : request.role,
                resourceKey = request.resourceKey,
                bytesBase64 = request.bytesBase64
            }, jsonOptions)
            : ImportGenericCharacterResource(rootPath, packageRelative, request, existingEntry: null, preserveExistingPath: false, jsonOptions);

        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "import", entry, null, jsonOptions);
    }

    private static object ReimportCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string id = GetResourceLibraryWriteRequestId(request);
        if (string.IsNullOrWhiteSpace(id))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_WRITE_TARGET_REQUIRED", "id, resourceKey, stableId, or libraryItemId is required.", 400);

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = FindCharacterPackageResourceEntryById(package.ResourceCatalog, id);
        if (entry == null)
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_ITEM_NOT_FOUND", "Resource library item was not found: " + id, 404);

        string sourcePath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, entry.RelativePath);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_SOURCE_MISSING", "Resource source file was not found: " + entry.RelativePath, 409);

        ResourceIdentitySnapshot before = ResourceIdentitySnapshot.FromEntry(entry);
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hash = CharacterPackageHashUtility.ComputeFileSha256(sourcePath);
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, package.ResourceCatalog);
        TouchResourceProvenance(entry, "ResourceLibraryEditor/Reimport", entry.Provenance?.SourceFile);

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "reimport", entry, before, jsonOptions);
    }

    private static object ReplaceCharacterResourceSource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string id = GetResourceLibraryWriteRequestId(request);
        if (string.IsNullOrWhiteSpace(id))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_WRITE_TARGET_REQUIRED", "id, resourceKey, stableId, or libraryItemId is required.", 400);

        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceEntry entry = FindCharacterPackageResourceEntryById(package.ResourceCatalog, id);
        if (entry == null)
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_ITEM_NOT_FOUND", "Resource library item was not found: " + id, 404);

        ResourceIdentitySnapshot before = ResourceIdentitySnapshot.FromEntry(entry);
        CharacterPackageResourceEntry updated;
        if (string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(entry.ResourceKey))
                throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_RESOURCE_KEY_MISSING", "Model resource cannot be replaced because resourceKey is empty.", 409);
            ValidateModelReplaceFormatChange(entry, request);
            updated = ImportCharacterModel(rootPath, packageRelative, new CharacterStudioModelImportRequest
            {
                fileName = request.fileName,
                role = string.IsNullOrWhiteSpace(request.role) ? GetModelImportRole(package, entry) : request.role,
                resourceKey = entry.ResourceKey,
                bytesBase64 = request.bytesBase64
            }, jsonOptions);
        }
        else
        {
            updated = ImportGenericCharacterResource(rootPath, packageRelative, request, entry, preserveExistingPath: true, jsonOptions);
        }

        return BuildCharacterResourceWriteResponse(rootPath, packageRelative, "replace-source", updated, before, jsonOptions);
    }

    private static void ValidateModelReplaceFormatChange(CharacterPackageResourceEntry entry, ResourceLibraryWriteRequest request)
    {
        string sourceExtension = ValidateResourceFileName(request.fileName);
        string targetExtension = string.Equals(sourceExtension, ".fbx", StringComparison.OrdinalIgnoreCase) ? ".glb" : sourceExtension;
        string currentExtension = Path.GetExtension(entry.RelativePath ?? string.Empty);
        if (!request.allowFormatChange && !string.IsNullOrWhiteSpace(currentExtension) &&
            !string.Equals(currentExtension, targetExtension, StringComparison.OrdinalIgnoreCase))
            throw new ResourceLibraryWriteException(
                "RESOURCE_LIBRARY_FORMAT_CHANGE_REQUIRES_CONFIRMATION",
                "Replacing this model changes the package format from " + currentExtension + " to " + targetExtension + ". Set allowFormatChange to true to confirm.",
                409);
    }

    private static CharacterPackageResourceEntry ImportGenericCharacterResource(
        string rootPath,
        string packageRelative,
        ResourceLibraryWriteRequest request,
        CharacterPackageResourceEntry existingEntry,
        bool preserveExistingPath,
        JsonSerializerOptions jsonOptions)
    {
        string extension = ValidateResourceFileName(request.fileName);
        byte[] bytes = DecodeResourceWriteBytes(request);
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        CharacterPackageResourceCatalog catalog = package.ResourceCatalog ?? new CharacterPackageResourceCatalog();
        package.ResourceCatalog = catalog;

        CharacterPackageResourceEntry entry = existingEntry;
        if (entry == null && !string.IsNullOrWhiteSpace(request.resourceKey))
            entry = CharacterPackageResourcePipeline.FindByKey(catalog, request.resourceKey);
        if (entry == null)
        {
            entry = new CharacterPackageResourceEntry();
            catalog.Entries.Add(entry);
        }

        string packageId = package.Manifest?.PackageId ?? string.Empty;
        string typeId = NormalizeResourceTypeId(!string.IsNullOrWhiteSpace(request.kind) ? request.kind : entry.TypeId, extension);
        string format = NormalizeSourceFormat(extension);
        string targetExtension = extension;
        byte[] packageResourceBytes = bytes;
        string fileStem = CharacterPackageResourceKeyGenerator.NormalizeSegment(Path.GetFileNameWithoutExtension(request.fileName)).Replace('.', '_');
        string rigType = GlobalWeaponConfigurationResolver.ResolveCharacterRigType(package);
        bool preserveNativeGenericFbx = string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase)
            && string.Equals(format, CharacterPackageResourceFormatIds.Fbx, StringComparison.OrdinalIgnoreCase)
            && string.Equals(rigType, GlobalAnimationRigTypes.Generic, StringComparison.Ordinal);
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(format, CharacterPackageResourceFormatIds.Fbx, StringComparison.OrdinalIgnoreCase) &&
            !preserveNativeGenericFbx)
        {
            packageResourceBytes = ConvertFbxToGlb(rootPath, fileStem, request.fileName, bytes);
            targetExtension = ".glb";
            format = CharacterPackageResourceFormatIds.Glb;
        }

        string usage = !string.IsNullOrWhiteSpace(request.usage) ? request.usage.Trim() : (!string.IsNullOrWhiteSpace(entry.Usage) ? entry.Usage : GetDefaultResourceUsage(typeId));
        if (!CharacterPackageResourcePipeline.IsSupportedV1Format(new CharacterPackageResourceEntry { TypeId = typeId, SourceFormat = format }))
            throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_UNSUPPORTED_FORMAT", "Resource type '" + typeId + "' does not support ." + format + " files.", 400);
        if (preserveExistingPath && !request.allowFormatChange && !string.IsNullOrWhiteSpace(entry.SourceFormat) &&
            !string.Equals(entry.SourceFormat, format, StringComparison.OrdinalIgnoreCase))
            throw new ResourceLibraryWriteException(
                "RESOURCE_LIBRARY_FORMAT_CHANGE_REQUIRES_CONFIRMATION",
                "Replacing this resource changes the source format from ." + entry.SourceFormat + " to ." + format + ". Set allowFormatChange to true to confirm.",
                409);

        string localId = !string.IsNullOrWhiteSpace(request.localId)
            ? CharacterPackageResourceKeyGenerator.NormalizeSegment(request.localId)
            : (!string.IsNullOrWhiteSpace(entry.LocalId) ? entry.LocalId : typeId + "." + fileStem);
        string targetRelativePath = ResolveResourceWriteTargetRelativePath(request, entry, preserveExistingPath, typeId, fileStem, targetExtension);
        string outputPath = Path.Combine(packagePath, targetRelativePath);
        string contentHash = CharacterPackageHashUtility.ComputeSha256(packageResourceBytes);
        if (!File.Exists(outputPath) || !string.Equals(CharacterPackageHashUtility.ComputeFileSha256(outputPath), contentHash, StringComparison.OrdinalIgnoreCase))
            WriteBytesAtomic(outputPath, packageResourceBytes);

        if (string.IsNullOrWhiteSpace(entry.ResourceKey))
            entry.ResourceKey = ResolveRequestedOrGeneratedResourceKey(request.resourceKey, packageId, typeId, localId);
        if (string.IsNullOrWhiteSpace(entry.LocalId))
            entry.LocalId = localId;
        if (string.IsNullOrWhiteSpace(entry.StableId))
            entry.StableId = "charpkg." + CharacterPackageResourceKeyGenerator.NormalizeSegment(packageId) + ".resource." + CharacterPackageResourceKeyGenerator.NormalizeSegment(entry.LocalId);

        entry.TypeId = typeId;
        entry.Variant = string.IsNullOrWhiteSpace(entry.Variant) ? "default" : entry.Variant;
        entry.Usage = usage;
        entry.SourceFormat = format;
        entry.PackageId = packageId;
        entry.RelativePath = targetRelativePath;
        entry.ImportHints ??= new CharacterPackageImportHint();
        entry.ImportHints.TargetPathPolicy = CharacterPackageImportTargetPathPolicies.GeneratedCharacterPackage;
        entry.ImportHints.TargetRelativePath = targetRelativePath;
        entry.ImportHints.ProviderId = string.IsNullOrWhiteSpace(entry.ImportHints.ProviderId) ? "unityAsset" : entry.ImportHints.ProviderId;
        entry.Tags ??= new List<string>();
        AddTag(entry.Tags, "resourcelibrary-import");
        if (request.tags != null)
        {
            for (int i = 0; i < request.tags.Count; i++)
                AddTag(entry.Tags, request.tags[i]);
        }

        entry.Hash = contentHash;
        entry.Hashes ??= new CharacterPackageResourceHashes();
        entry.Hashes.Algorithm = "sha256";
        entry.Hashes.ContentHash = entry.Hash;
        entry.Hashes.ImportHash = CharacterPackageResourcePipeline.ComputeImportHash(entry);
        entry.Hashes.DependencyHash = CharacterPackageResourcePipeline.ComputeDependencyHash(entry, catalog);
        TouchResourceProvenance(entry, "ResourceLibraryEditor", request.fileName);

        SaveCharacterPackage(rootPath, packageRelative, package, jsonOptions);
        return entry;
    }

    private static string ResolveResourceWriteTargetRelativePath(
        ResourceLibraryWriteRequest request,
        CharacterPackageResourceEntry entry,
        bool preserveExistingPath,
        string typeId,
        string fileStem,
        string extension)
    {
        if (!string.IsNullOrWhiteSpace(request.targetRelativePath))
        {
            if (!CharacterPackageResourcePipeline.IsSafePackageRelativePath(request.targetRelativePath))
                throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_UNSAFE_TARGET_PATH", "targetRelativePath must stay inside the character package.", 400);
            return request.targetRelativePath.Replace('\\', '/').TrimStart('/');
        }

        if (preserveExistingPath && entry != null && !string.IsNullOrWhiteSpace(entry.RelativePath))
        {
            string existingExtension = Path.GetExtension(entry.RelativePath);
            if (string.Equals(existingExtension, extension, StringComparison.OrdinalIgnoreCase))
                return entry.RelativePath.Replace('\\', '/').TrimStart('/');
        }

        return (GetResourceTypeDirectory(typeId) + "/" + fileStem + extension).Replace('\\', '/');
    }

    private static string GetResourceTypeDirectory(string typeId)
    {
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return "resources/models";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return "resources/textures";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
            return "resources/materials";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return "resources/animations";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return "resources/audio";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
            return "resources/vfx";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
            return "resources/geometry";
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            return "resources/previews";
        return "resources/config";
    }

    private static string NormalizeResourceTypeId(string typeId, string extension)
    {
        if (!string.IsNullOrWhiteSpace(typeId))
            return typeId.Trim();
        string format = NormalizeSourceFormat(extension);
        if (format == CharacterPackageResourceFormatIds.Glb || format == CharacterPackageResourceFormatIds.Gltf || format == CharacterPackageResourceFormatIds.Fbx)
            return CharacterPackageResourceTypeIds.Model;
        if (format == CharacterPackageResourceFormatIds.Png || format == CharacterPackageResourceFormatIds.Jpg || format == CharacterPackageResourceFormatIds.Jpeg || format == CharacterPackageResourceFormatIds.Tga)
            return CharacterPackageResourceTypeIds.Texture;
        if (format == CharacterPackageResourceFormatIds.Wav || format == CharacterPackageResourceFormatIds.Ogg)
            return CharacterPackageResourceTypeIds.Audio;
        return CharacterPackageResourceTypeIds.Config;
    }

    private static string GetDefaultResourceUsage(string typeId)
    {
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.PreviewMesh;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Texture, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.Texture;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Material, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.Material;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Animation, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.AnimationClipGroup;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Audio, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.AudioCue;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Vfx, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.VfxCue;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Geometry, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.GeometryAuthoring;
        if (string.Equals(typeId, CharacterPackageResourceTypeIds.Preview, StringComparison.OrdinalIgnoreCase))
            return CharacterPackageResourceUsageIds.PreviewThumbnail;
        return CharacterPackageResourceUsageIds.CharacterConfig;
    }

    private static string ResolveRequestedOrGeneratedResourceKey(string requestedResourceKey, string packageId, string typeId, string localId)
    {
        if (!string.IsNullOrWhiteSpace(requestedResourceKey))
        {
            string trimmed = requestedResourceKey.Trim();
            if (!CharacterPackageResourceKeyGenerator.IsValidResourceKey(trimmed))
                throw new ResourceLibraryWriteException("RESOURCE_LIBRARY_INVALID_RESOURCE_KEY", "resourceKey contains unsupported characters: " + trimmed, 400);
            return trimmed;
        }

        return CharacterPackageResourceKeyGenerator.Generate(packageId, typeId, localId);
    }

    private static string NormalizeSourceFormat(string extension)
    {
        return extension.TrimStart('.').Trim().ToLowerInvariant();
    }

    private static string ValidateResourceFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName is required.");
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("fileName must include an extension.");
        return extension;
    }

    private static byte[] DecodeResourceWriteBytes(ResourceLibraryWriteRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.bytesBase64))
            throw new ArgumentException("bytesBase64 is required.");
        try
        {
            return Convert.FromBase64String(request.bytesBase64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("bytesBase64 is not valid base64.", ex);
        }
    }

    private static string GetResourceLibraryWriteRequestId(ResourceLibraryWriteRequest request)
    {
        if (request == null)
            return string.Empty;
        return FirstNonEmpty(request.id, request.libraryItemId, request.stableId, request.resourceKey);
    }

    private static CharacterPackageResourceEntry FindCharacterPackageResourceEntryById(CharacterPackageResourceCatalog catalog, string id)
    {
        if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(id))
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;
            if (string.Equals(entry.ResourceKey, id, StringComparison.Ordinal) ||
                string.Equals(entry.StableId, id, StringComparison.Ordinal) ||
                string.Equals(entry.LocalId, id, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static string GetModelImportRole(CharacterResourcePackage package, CharacterPackageResourceEntry entry)
    {
        if (entry == null)
            return "preview";
        if (string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.LocalId, "model.body", StringComparison.OrdinalIgnoreCase))
            return "body";
        if (package?.Geometry?.WeaponAttachments != null)
        {
            WeaponAttachmentProfile attachment = package.Geometry.WeaponAttachments.FirstOrDefault(item =>
                item != null && string.Equals(item.PreviewResourceKey, entry.ResourceKey, StringComparison.Ordinal));
            if (attachment != null && !string.IsNullOrWhiteSpace(attachment.EquipSlot))
                return attachment.EquipSlot;
        }
        if (entry.Tags != null)
        {
            if (entry.Tags.Contains("mainHand", StringComparer.OrdinalIgnoreCase))
                return "mainHand";
            if (entry.Tags.Contains("offHand", StringComparer.OrdinalIgnoreCase))
                return "offHand";
        }
        return "preview";
    }

    private static object BuildCharacterResourceWriteResponse(
        string rootPath,
        string packageRelative,
        string action,
        CharacterPackageResourceEntry entry,
        ResourceIdentitySnapshot before,
        JsonSerializerOptions jsonOptions)
    {
        string selectedId = FirstNonEmpty(entry?.ResourceKey, entry?.StableId, entry?.LocalId);
        CharacterResourceLibrary resources = ReadCharacterResources(rootPath, packageRelative, jsonOptions);
        ResourceLibraryItem item = FindCharacterResourceLibraryItem(resources, selectedId);
        List<ResourceReferenceEdge> references = item != null ? FindCharacterResourceReferences(resources.ReferenceGraph, item) : new List<ResourceReferenceEdge>();
        CharacterResourcePlanCompileResult resourcePlan = ReadCharacterResourcePlan(rootPath, packageRelative, checkFiles: true, checkHashes: false, jsonOptions);
        object inspect = string.IsNullOrWhiteSpace(selectedId) ? null : ReadCharacterResourceInspect(rootPath, packageRelative, selectedId, jsonOptions);

        return new
        {
            success = true,
            action,
            packageRelative,
            selectedId,
            selectedResourceKey = entry?.ResourceKey ?? string.Empty,
            item,
            inspect,
            resources,
            resourcePlan,
            referenceImpact = new
            {
                preservesStableId = before == null || string.Equals(before.StableId, entry?.StableId, StringComparison.Ordinal),
                preservesResourceKey = before == null || string.Equals(before.ResourceKey, entry?.ResourceKey, StringComparison.Ordinal),
                affectedReferenceCount = references.Count,
                changedFields = before == null ? Array.Empty<string>() : before.GetChangedFields(entry)
            },
            diagnostics = item?.Diagnostics ?? new List<ResourceLibraryDiagnostic>()
        };
    }

    private static object BuildResourceLibraryWriteError(string code, string message)
    {
        return new
        {
            error = string.IsNullOrWhiteSpace(code) ? "RESOURCE_LIBRARY_WRITE_FAILED" : code,
            message = string.IsNullOrWhiteSpace(message) ? "Resource library write failed." : message
        };
    }

    private static CharacterAuthoringCompileResult CompileCharacterPackage(
        string rootPath,
        string packageRelative,
        bool checkFiles,
        bool checkHashes,
        JsonSerializerOptions jsonOptions)
    {
        string packagePath = ResolveSafePath(rootPath, packageRelative);
        CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
        return CharacterAuthoringCompiler.Compile(new CharacterAuthoringCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            Options = new CharacterAuthoringCompileOptions
            {
                ValidateResourceFiles = checkFiles || checkHashes,
                ValidateResourceHashes = checkHashes
            }
        });
    }

    private static ResourceLibraryItem FindCharacterResourceLibraryItem(CharacterResourceLibrary library, string id)
    {
        if (library == null || library.Items == null || string.IsNullOrWhiteSpace(id))
            return null;

        ResourceLibraryItem fallback = null;
        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item == null)
                continue;

            if (string.Equals(item.LibraryItemId, id, StringComparison.Ordinal) ||
                string.Equals(item.StableId, id, StringComparison.Ordinal) ||
                string.Equals(item.ResourceKey, id, StringComparison.Ordinal))
                return item;

            if (fallback == null && string.Equals(item.DisplayName, id, StringComparison.Ordinal))
                fallback = item;
        }

        if (fallback != null)
            return fallback;

        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item != null && string.Equals(item.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private static CharacterPackageResourceEntry FindCharacterPackageResourceEntry(CharacterPackageResourceCatalog catalog, ResourceLibraryItem item, string id)
    {
        if (catalog == null || catalog.Entries == null || item == null)
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null)
                continue;

            if (ResourceMatches(entry.ResourceKey, item, id) ||
                ResourceMatches(entry.StableId, item, id) ||
                ResourceMatches(entry.LocalId, item, id))
                return entry;
        }

        return null;
    }

    private static bool ResourceMatches(string value, ResourceLibraryItem item, string id)
    {
        if (string.IsNullOrWhiteSpace(value) || item == null)
            return false;

        return string.Equals(value, item.ResourceKey, StringComparison.Ordinal) ||
            string.Equals(value, item.StableId, StringComparison.Ordinal) ||
            string.Equals(value, item.LibraryItemId, StringComparison.Ordinal) ||
            string.Equals(value, item.DisplayName, StringComparison.Ordinal) ||
            string.Equals(value, id, StringComparison.Ordinal);
    }

    private static object BuildCharacterResourceInspectAuthoring(
        string packagePath,
        string packageRelative,
        CharacterPackageResourceEntry entry,
        ResourceLibraryItem item)
    {
        string sourcePath = entry != null ? entry.RelativePath : item.SourcePath;
        string sourceFullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packagePath, sourcePath);
        bool sourceExists = !string.IsNullOrWhiteSpace(sourceFullPath) && File.Exists(sourceFullPath);
        long sourceSize = sourceExists ? new FileInfo(sourceFullPath).Length : 0;

        return new
        {
            packageRelative,
            resourceKey = entry != null ? entry.ResourceKey : item.ResourceKey,
            localId = entry != null ? entry.LocalId : item.DisplayName,
            stableId = entry != null ? entry.StableId : item.StableId,
            typeId = entry != null ? entry.TypeId : item.Kind,
            variant = entry != null ? entry.Variant : string.Empty,
            usage = entry != null ? entry.Usage : item.Usage,
            sourceFormat = entry != null ? entry.SourceFormat : string.Empty,
            sourcePath,
            sourceExists,
            sourceSize,
            hash = entry != null ? CharacterPackageResourcePipeline.GetDeclaredContentHash(entry) : item.Hash,
            hashes = entry != null ? entry.Hashes : null,
            importHints = entry != null ? entry.ImportHints : null,
            dependencies = entry != null && entry.Dependencies != null ? entry.Dependencies : new List<CharacterPackageResourceDependency>(),
            tags = entry != null ? entry.Tags : item.Tags,
            conflictPolicy = entry != null ? entry.ConflictPolicy : null,
            preview = entry != null ? entry.Preview : null,
            provenance = entry != null ? entry.Provenance : null,
            entry
        };
    }

    private static object BuildCharacterResourceInspectUnity(
        string rootPath,
        string unityCatalogPath,
        string importReportPath,
        JsonElement? unityEntry,
        JsonElement? importOperation,
        ResourceLibraryItem item)
    {
        return new
        {
            unityResourceCatalogPath = ToProjectRelativePath(rootPath, unityCatalogPath),
            importReportPath = ToProjectRelativePath(rootPath, importReportPath),
            unityAssetGuid = GetJsonString(unityEntry, "unityAssetGuid"),
            unityAssetPath = FirstNonEmpty(GetJsonString(unityEntry, "unityAssetPath"), item.UnityAssetPath),
            importerKind = GetJsonString(unityEntry, "importerKind"),
            mainObjectType = GetJsonString(unityEntry, "unityMainObjectType"),
            subAssets = Array.Empty<object>(),
            importStatus = FirstNonEmpty(GetJsonString(unityEntry, "importStatus"), item.ImportStatus.ToString()),
            lastImportOperation = importOperation,
            diagnostics = GetJsonArray(unityEntry, "diagnostics"),
            catalogEntry = unityEntry
        };
    }

    private static object BuildCharacterResourceInspectRuntime(
        ResourceLibraryItem item,
        RuntimeResourceCatalogEntryDocument runtimeEntry,
        AudioCueManifestEntry audioCue,
        List<object> plans)
    {
        List<string> groupNames = GetPlanGroupNames(plans);
        return new
        {
            runtimeBindingKind = item.RuntimeBindingKind,
            runtimeAvailability = item.RuntimeAvailability,
            resourceKey = item.ResourceKey,
            providerId = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Provider : string.Empty, item.ProviderId),
            address = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Address : string.Empty, item.UnityAssetPath, item.SourcePath),
            assetType = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Type : string.Empty, item.Kind),
            hash = FirstNonEmpty(runtimeEntry != null ? runtimeEntry.Hash : string.Empty, item.Hash),
            preloadPolicy = groupNames.Count > 0 ? groupNames[0] : ResourceLibraryPreloadPolicies.None,
            includedRuntimePlanGroups = groupNames,
            runtimeCatalogEntry = runtimeEntry,
            audioCue
        };
    }

    private static List<object> BuildCharacterResourceInspectPlans(ResourceLibraryItem item, CharacterResourcePlanCompileResult result)
    {
        var plans = new List<object>();
        if (item == null || result == null || result.CharacterResourcePlan == null)
            return plans;

        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.SpawnCritical, result.CharacterResourcePlan.SpawnCritical, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.PresentationCritical, result.CharacterResourcePlan.PresentationCritical, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.EquipmentInitial, result.CharacterResourcePlan.EquipmentInitial, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.AnimationWarmup, result.CharacterResourcePlan.AnimationWarmup, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.VfxWarmup, result.CharacterResourcePlan.VfxWarmup, item);
        AddPlanGroupIfContains(plans, CharacterResourcePlanGroups.UiDeferred, result.CharacterResourcePlan.UiDeferred, item);

        AudioCueManifestEntry cue = FindAudioCue(result.AudioCueManifest, item);
        if (cue != null)
        {
            plans.Add(new
            {
                groupName = CharacterResourcePlanGroups.Audio,
                required = result.CharacterResourcePlan.Audio != null && result.CharacterResourcePlan.Audio.Required,
                failurePolicy = result.CharacterResourcePlan.Audio != null ? result.CharacterResourcePlan.Audio.FailurePolicy : string.Empty,
                resource = cue
            });
        }

        return plans;
    }

    private static void AddPlanGroupIfContains(List<object> plans, string groupName, CharacterResourcePlanGroup group, ResourceLibraryItem item)
    {
        if (plans == null || group == null || group.Resources == null || item == null)
            return;

        for (int i = 0; i < group.Resources.Count; i++)
        {
            CharacterResourcePlanResourceRef resource = group.Resources[i];
            if (resource == null)
                continue;

            if (!ResourceMatches(resource.ResourceKey, item, string.Empty) &&
                !ResourceMatches(resource.StableId, item, string.Empty))
                continue;

            plans.Add(new
            {
                groupName,
                required = group.Required,
                failurePolicy = group.FailurePolicy,
                resource
            });
        }
    }

    private static List<string> GetPlanGroupNames(List<object> plans)
    {
        var result = new List<string>();
        if (plans == null)
            return result;

        for (int i = 0; i < plans.Count; i++)
        {
            string groupName = GetAnonymousProperty(plans[i], "groupName");
            if (!string.IsNullOrWhiteSpace(groupName) && !result.Contains(groupName))
                result.Add(groupName);
        }

        return result;
    }

    private static List<ResourceReferenceEdge> FindCharacterResourceReferences(ResourceReferenceGraph graph, ResourceLibraryItem item)
    {
        var references = new List<ResourceReferenceEdge>();
        if (graph == null || graph.Edges == null || item == null)
            return references;

        for (int i = 0; i < graph.Edges.Count; i++)
        {
            ResourceReferenceEdge edge = graph.Edges[i];
            if (edge == null)
                continue;

            if (ResourceMatches(edge.TargetLibraryItemStableId, item, string.Empty) ||
                ResourceMatches(edge.TargetResourceKey, item, string.Empty))
                references.Add(edge);
        }

        return references;
    }

    private static RuntimeResourceCatalogEntryDocument FindRuntimeCatalogEntry(RuntimeResourceCatalogDocument catalog, ResourceLibraryItem item)
    {
        if (catalog == null || catalog.Entries == null || item == null)
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            RuntimeResourceCatalogEntryDocument entry = catalog.Entries[i];
            if (entry == null)
                continue;

            if (ResourceMatches(entry.Id, item, string.Empty) ||
                (entry.ProviderData != null && ResourceMatches(GetProviderData(entry.ProviderData, "stableId"), item, string.Empty)) ||
                (entry.ProviderData != null && ResourceMatches(GetProviderData(entry.ProviderData, "packageResourceKey"), item, string.Empty)))
                return entry;
        }

        return null;
    }

    private static AudioCueManifestEntry FindAudioCue(AudioCueManifestDocument manifest, ResourceLibraryItem item)
    {
        if (manifest == null || manifest.Cues == null || item == null)
            return null;

        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueManifestEntry cue = manifest.Cues[i];
            if (cue == null)
                continue;

            if (ResourceMatches(cue.CueId, item, string.Empty) ||
                ResourceMatches(cue.StableId, item, string.Empty) ||
                ResourceMatches(cue.ResourceKey, item, string.Empty))
                return cue;
        }

        return null;
    }

    private static string GetProviderData(Dictionary<string, string> providerData, string key)
    {
        if (providerData == null || string.IsNullOrWhiteSpace(key))
            return string.Empty;
        string value;
        return providerData.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
    }

    private static List<object> BuildCharacterResourceInspectDiagnostics(
        ResourceLibraryItem item,
        CharacterResourceLibrary library,
        CharacterResourcePlanCompileResult plan,
        JsonElement? unityEntry)
    {
        var diagnostics = new List<object>();
        if (item == null)
            return diagnostics;

        if (item.Diagnostics != null)
        {
            for (int i = 0; i < item.Diagnostics.Count; i++)
                AddResourceLibraryDiagnostic(diagnostics, "item", item.Diagnostics[i], item);
        }

        if (library != null && library.Diagnostics != null)
        {
            for (int i = 0; i < library.Diagnostics.Count; i++)
                AddResourceLibraryDiagnostic(diagnostics, "library", library.Diagnostics[i], item);
        }

        if (plan != null && plan.CharacterResourcePlan != null && plan.CharacterResourcePlan.Diagnostics != null)
        {
            for (int i = 0; i < plan.CharacterResourcePlan.Diagnostics.Count; i++)
                AddResourcePlanDiagnostic(diagnostics, "resourcePlan", plan.CharacterResourcePlan.Diagnostics[i], item);
        }

        if (plan != null && plan.ResourceValidationReport != null && plan.ResourceValidationReport.Diagnostics != null)
        {
            for (int i = 0; i < plan.ResourceValidationReport.Diagnostics.Count; i++)
                AddResourcePlanDiagnostic(diagnostics, "validationReport", plan.ResourceValidationReport.Diagnostics[i], item);
        }

        if (unityEntry.HasValue)
        {
            JsonElement[] unityDiagnostics = GetJsonArray(unityEntry, "diagnostics");
            for (int i = 0; i < unityDiagnostics.Length; i++)
                AddUnityDiagnostic(diagnostics, unityDiagnostics[i], item);
        }

        return diagnostics;
    }

    private static void AddResourceLibraryDiagnostic(List<object> diagnostics, string source, ResourceLibraryDiagnostic diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!ResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !ResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity.ToString(),
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceStableId = diagnostic.SourceStableId,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddResourcePlanDiagnostic(List<object> diagnostics, string source, CharacterResourcePlanDiagnostic diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || diagnostic == null || item == null)
            return;

        if (!ResourceMatches(diagnostic.LibraryItemStableId, item, string.Empty) &&
            !ResourceMatches(diagnostic.ResourceKey, item, string.Empty))
            return;

        diagnostics.Add(new
        {
            source,
            severity = diagnostic.Severity,
            code = diagnostic.Code,
            message = diagnostic.Message,
            suggestedFix = diagnostic.SuggestedFix,
            libraryItemStableId = diagnostic.LibraryItemStableId,
            resourceKey = diagnostic.ResourceKey,
            sourceConfigKind = diagnostic.SourceConfigKind,
            sourceField = diagnostic.SourceField
        });
    }

    private static void AddUnityDiagnostic(List<object> diagnostics, JsonElement diagnostic, ResourceLibraryItem item)
    {
        if (diagnostics == null || item == null || diagnostic.ValueKind != JsonValueKind.Object)
            return;

        diagnostics.Add(new
        {
            source = "unity",
            severity = TryGetString(diagnostic, "severity"),
            code = TryGetString(diagnostic, "code"),
            message = TryGetString(diagnostic, "message"),
            suggestedFix = string.Empty,
            libraryItemStableId = item.StableId,
            resourceKey = item.ResourceKey,
            sourcePath = TryGetString(diagnostic, "sourcePath"),
            sourceField = TryGetString(diagnostic, "field")
        });
    }

    private static JsonElement? FindResourceJsonEntry(string rootPath, string path, ResourceLibraryItem item)
    {
        if (item == null)
            return null;

        string fullPath = Path.GetFullPath(path);
        if (!IsPathAllowed(rootPath, fullPath) || !File.Exists(fullPath))
            return null;

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (JsonResourceMatches(entry, item))
                return entry.Clone();
        }

        return null;
    }

    private static JsonElement? FindImportReportOperation(
        string rootPath,
        string path,
        ResourceLibraryItem item,
        CharacterPackageResourceEntry authoringEntry,
        JsonElement? unityEntry)
    {
        string fullPath = Path.GetFullPath(path);
        if (item == null || !IsPathAllowed(rootPath, fullPath) || !File.Exists(fullPath))
            return null;

        string sourcePath = authoringEntry != null ? authoringEntry.RelativePath : item.SourcePath;
        string unityAssetPath = GetJsonString(unityEntry, "unityAssetPath");
        string address = GetJsonString(unityEntry, "address");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fullPath));
        if (!document.RootElement.TryGetProperty("operations", out JsonElement operations) || operations.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement operation in operations.EnumerateArray())
        {
            if (string.Equals(TryGetString(operation, "sourcePath"), sourcePath, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "targetPath"), unityAssetPath, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "targetPath"), address, StringComparison.Ordinal) ||
                string.Equals(TryGetString(operation, "contentHash"), item.Hash, StringComparison.Ordinal))
                return operation.Clone();
        }

        return null;
    }

    private static bool JsonResourceMatches(JsonElement entry, ResourceLibraryItem item)
    {
        if (entry.ValueKind != JsonValueKind.Object || item == null)
            return false;

        return ResourceMatches(TryGetString(entry, "id"), item, string.Empty) ||
            ResourceMatches(TryGetString(entry, "packageResourceKey"), item, string.Empty) ||
            ResourceMatches(TryGetString(entry, "stableId"), item, string.Empty);
    }

    private static string GetJsonString(JsonElement? element, string propertyName)
    {
        if (!element.HasValue)
            return string.Empty;
        return TryGetString(element.Value, propertyName);
    }

    private static JsonElement[] GetJsonArray(JsonElement? element, string propertyName)
    {
        if (!element.HasValue || element.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<JsonElement>();
        if (!element.Value.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
            return Array.Empty<JsonElement>();

        var result = new List<JsonElement>();
        foreach (JsonElement item in property.EnumerateArray())
            result.Add(item.Clone());
        return result.ToArray();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        }

        return string.Empty;
    }

    private static ResourceReferenceGraph BuildCharacterResourceReferenceGraph(CharacterResourcePackage package)
    {
        var graph = new ResourceReferenceGraph();
        if (package == null || package.ResourceCatalog == null)
            return graph;

        Dictionary<string, CharacterPackageResourceEntry> byKey = BuildPackageResourceEntryLookup(package.ResourceCatalog);
        CharacterApplicationAuthoringSummary application = package.ApplicationConfig;
        if (application != null && application.ResourceKeys != null)
        {
            string sourceStableId = !string.IsNullOrWhiteSpace(application.CharacterStableId)
                ? application.CharacterStableId
                : package.Manifest != null ? package.Manifest.StableId : string.Empty;
            for (int i = 0; i < application.ResourceKeys.Count; i++)
            {
                string resourceKey = application.ResourceKeys[i];
                AddResourceReferenceEdge(
                    graph,
                    byKey,
                    "character",
                    sourceStableId,
                    "resourceKeys/" + i.ToString(CultureInfo.InvariantCulture),
                    resourceKey,
                    ResourceLibraryPreloadPolicies.SpawnCritical,
                    isRequiredAtRuntime: true);
            }
        }

        List<SingleWeaponConfiguration> singles = package.GlobalWeaponLibrary?.SingleWeaponConfigurations;
        if (application != null && singles != null)
        {
            for (int i = 0; i < singles.Count; i++)
            {
                SingleWeaponConfiguration definition = singles[i];
                if (definition == null || !IsSingleWeaponReferenced(application, definition.SingleWeaponConfigurationId))
                    continue;

                AddResourceReferenceEdge(
                    graph,
                    byKey,
                    "weapon",
                    definition.SingleWeaponConfigurationId,
                    "singleWeaponConfigurations/" + i.ToString(CultureInfo.InvariantCulture) + "/weaponRef",
                    GetWeaponDefinitionReferenceKey(definition),
                    ResourceLibraryPreloadPolicies.EquipmentInitial,
                    isRequiredAtRuntime: true);
            }
        }

        return graph;
    }

    private static string GetWeaponDefinitionReferenceKey(SingleWeaponConfiguration definition)
    {
        if (definition == null)
            return string.Empty;

        if (!IsSelectionEmpty(definition.WeaponRef))
        {
            return FirstNonEmpty(
                definition.WeaponRef.RuntimeResourceKey,
                definition.WeaponRef.PackageResourceKey,
                definition.WeaponRef.ProviderResourceKey,
                definition.WeaponRef.ResourceStableId);
        }
        return string.Empty;
    }

    private static bool IsSingleWeaponReferenced(
        CharacterApplicationAuthoringSummary application,
        string singleWeaponConfigurationId)
    {
        List<CombinedWeaponConfigurationAuthoringSummary> configurations = application?.CombinedWeaponConfigurations;
        for (int i = 0; configurations != null && i < configurations.Count; i++)
        {
            List<CombinedWeaponConfigurationSlotAuthoringSummary> slots = configurations[i]?.Slots;
            for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                if (slots[slotIndex] != null && string.Equals(
                    slots[slotIndex].SingleWeaponConfigurationId,
                    singleWeaponConfigurationId,
                    StringComparison.Ordinal))
                    return true;
        }
        return false;
    }

    private static Dictionary<string, CharacterPackageResourceEntry> BuildPackageResourceEntryLookup(CharacterPackageResourceCatalog catalog)
    {
        var lookup = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
        if (catalog == null || catalog.Entries == null)
            return lookup;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.ResourceKey))
                lookup[entry.ResourceKey] = entry;
        }

        return lookup;
    }

    private static void AddResourceReferenceEdge(
        ResourceReferenceGraph graph,
        Dictionary<string, CharacterPackageResourceEntry> byKey,
        string sourceConfigKind,
        string sourceStableId,
        string sourceField,
        string resourceKey,
        string preloadPolicy,
        bool isRequiredAtRuntime)
    {
        if (graph == null || string.IsNullOrWhiteSpace(resourceKey))
            return;

        CharacterPackageResourceEntry target;
        byKey.TryGetValue(resourceKey, out target);
        graph.Edges.Add(new ResourceReferenceEdge
        {
            SourceConfigKind = sourceConfigKind ?? string.Empty,
            SourceStableId = sourceStableId ?? string.Empty,
            SourceField = sourceField ?? string.Empty,
            TargetLibraryItemStableId = target != null ? target.StableId : string.Empty,
            TargetResourceKey = resourceKey,
            BindingKind = RuntimeBindingKind.ResourceManagerAsset,
            IsRequiredAtRuntime = isRequiredAtRuntime,
            PreloadPolicy = preloadPolicy ?? ResourceLibraryPreloadPolicies.None
        });
    }

    private static void ApplyCharacterResourceRuntimeState(
        string rootPath,
        string packagePath,
        CharacterResourcePackage package,
        CharacterResourceLibrary library,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null || package.Manifest == null || library == null)
            return;

        string generatedRoot = Path.Combine(rootPath, "Assets", "MxFrameworkGenerated", "CharacterPackages", package.Manifest.PackageId);
        string unityCatalogPath = Path.Combine(generatedRoot, "config", "unity_resource_catalog.json");
        string runtimeCatalogPath = Path.Combine(generatedRoot, "config", "runtime_resource_catalog.json");
        HashSet<string> unityResourceKeys = ReadResourceKeySetFromCatalog(unityCatalogPath, jsonOptions);
        HashSet<string> runtimeResourceKeys = ReadResourceKeySetFromCatalog(runtimeCatalogPath, jsonOptions);
        AddCompiledRuntimeResourceKeys(packagePath, package, runtimeResourceKeys);

        for (int i = 0; i < library.Items.Count; i++)
        {
            ResourceLibraryItem item = library.Items[i];
            if (item == null)
                continue;

            if (item.RuntimeBindingKind == RuntimeBindingKind.ResourceManagerAsset)
            {
                item.ImportStatus = unityResourceKeys.Contains(item.ResourceKey)
                    ? ResourceImportStatus.Clean
                    : ResourceImportStatus.UnityMissing;
                item.RuntimeAvailability = runtimeResourceKeys.Contains(item.ResourceKey)
                    ? ResourceRuntimeAvailability.RuntimeReady
                    : ResourceRuntimeAvailability.RuntimeMissing;
            }
            else if (item.RuntimeBindingKind == RuntimeBindingKind.GeneratedPreviewOnly)
            {
                item.ImportStatus = ResourceImportStatus.Clean;
                item.RuntimeAvailability = ResourceRuntimeAvailability.PreviewOnly;
            }
        }
    }

    private static void AddCompiledRuntimeResourceKeys(
        string packagePath,
        CharacterResourcePackage package,
        HashSet<string> runtimeResourceKeys)
    {
        if (package == null || runtimeResourceKeys == null)
            return;

        CharacterResourcePlanCompileResult result = CharacterResourcePlanCompiler.Compile(new CharacterResourcePlanCompileRequest
        {
            Package = package,
            PackageRootPath = packagePath,
            ValidateResourceFiles = false,
            ValidateResourceHashes = false
        });
        if (result == null || result.RuntimeResourceCatalog == null || result.RuntimeResourceCatalog.Entries == null)
            return;

        for (int i = 0; i < result.RuntimeResourceCatalog.Entries.Count; i++)
        {
            RuntimeResourceCatalogEntryDocument entry = result.RuntimeResourceCatalog.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                runtimeResourceKeys.Add(entry.Id);
        }
    }

    private static HashSet<string> ReadResourceKeySetFromCatalog(string path, JsonSerializerOptions jsonOptions)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return keys;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
                return keys;

            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string id = TryGetString(entry, "id");
                if (!string.IsNullOrWhiteSpace(id))
                    keys.Add(id);
                string packageResourceKey = TryGetString(entry, "packageResourceKey");
                if (!string.IsNullOrWhiteSpace(packageResourceKey))
                    keys.Add(packageResourceKey);
            }
        }
        catch
        {
        }

        return keys;
    }

    private static string TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(propertyName))
            return string.Empty;
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            return string.Empty;
        return property.GetString() ?? string.Empty;
    }

    private sealed class ResourceLibraryWriteRequest
    {
        public string package { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public string libraryItemId { get; set; } = string.Empty;
        public string stableId { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public string fileName { get; set; } = string.Empty;
        public string role { get; set; } = string.Empty;
        public string kind { get; set; } = string.Empty;
        public string usage { get; set; } = string.Empty;
        public string localId { get; set; } = string.Empty;
        public string targetRelativePath { get; set; } = string.Empty;
        public string bytesBase64 { get; set; } = string.Empty;
        public List<string> tags { get; set; } = new();
        public bool allowFormatChange { get; set; }
        public bool dryRun { get; set; }
        public bool importUnity { get; set; }
        public bool checkHashes { get; set; }
    }

    private sealed class ExternalImportStageRequest
    {
        public string package { get; set; } = string.Empty;
        public string sourceRootLabel { get; set; } = string.Empty;
        public long maxFileSizeBytes { get; set; } = 512L * 1024L * 1024L;
        public List<AuthoringExternalImportStagingFile> files { get; set; } = new();
    }

    private sealed class AuthoringResourceSelectionRequest
    {
        public string package { get; set; } = string.Empty;
        public AuthoringResourceFieldSpec fieldSpec { get; set; } = new AuthoringResourceFieldSpec();
        public AuthoringResourceConsumerContext context { get; set; } = new AuthoringResourceConsumerContext();
        public AuthoringResourceSelectionRef selection { get; set; } = new AuthoringResourceSelectionRef();
    }

    private sealed class ResourceLibraryWriteException : Exception
    {
        public ResourceLibraryWriteException(string code, string message, int statusCode)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
        }

        public string Code { get; }
        public int StatusCode { get; }
    }

    private sealed class ResourceIdentitySnapshot
    {
        public string ResourceKey { get; private init; } = string.Empty;
        public string StableId { get; private init; } = string.Empty;
        public string LocalId { get; private init; } = string.Empty;
        public string RelativePath { get; private init; } = string.Empty;
        public string Hash { get; private init; } = string.Empty;
        public string SourceFormat { get; private init; } = string.Empty;
        public string TypeId { get; private init; } = string.Empty;
        public string Usage { get; private init; } = string.Empty;

        public static ResourceIdentitySnapshot FromEntry(CharacterPackageResourceEntry entry)
        {
            return new ResourceIdentitySnapshot
            {
                ResourceKey = entry?.ResourceKey ?? string.Empty,
                StableId = entry?.StableId ?? string.Empty,
                LocalId = entry?.LocalId ?? string.Empty,
                RelativePath = entry?.RelativePath ?? string.Empty,
                Hash = entry?.Hash ?? string.Empty,
                SourceFormat = entry?.SourceFormat ?? string.Empty,
                TypeId = entry?.TypeId ?? string.Empty,
                Usage = entry?.Usage ?? string.Empty
            };
        }

        public string[] GetChangedFields(CharacterPackageResourceEntry entry)
        {
            var changed = new List<string>();
            AddIfChanged(changed, nameof(ResourceKey), ResourceKey, entry?.ResourceKey);
            AddIfChanged(changed, nameof(StableId), StableId, entry?.StableId);
            AddIfChanged(changed, nameof(LocalId), LocalId, entry?.LocalId);
            AddIfChanged(changed, nameof(RelativePath), RelativePath, entry?.RelativePath);
            AddIfChanged(changed, nameof(Hash), Hash, entry?.Hash);
            AddIfChanged(changed, nameof(SourceFormat), SourceFormat, entry?.SourceFormat);
            AddIfChanged(changed, nameof(TypeId), TypeId, entry?.TypeId);
            AddIfChanged(changed, nameof(Usage), Usage, entry?.Usage);
            return changed.ToArray();
        }

        private static void AddIfChanged(List<string> changed, string name, string before, string after)
        {
            if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
                changed.Add(name);
        }
    }
}
