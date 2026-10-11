using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using MxFramework.Authoring;
using MxFramework.ResourceMapping.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string CommonResourcesRootRelativePath = "ArtSource/Common";
    private const string CommonResourcesGeneratedRootRelativePath = "Assets/MxFrameworkGenerated/Common";
    private const string CommonResourcesStateRelativePath = "Assets/MxFrameworkGenerated/Common/common_resource_import_state.json";
    private const string CommonCharacterSourcesFileName = "character_sources.json";
    private const string CommonWeaponSourcesFileName = "weapon_sources.json";
    private const string CommonResourceSourcesFileName = "resource_sources.json";
    private const string CommonModelsBundleRuleId = "common.models";
    private const string CommonTexturesBundleRuleId = "common.textures";
    private const string CommonMaterialsBundleRuleId = "common.materials";
    private const string CommonAudioBundleRuleId = "common.audio";
    private const string CommonVfxBundleRuleId = "common.vfx";
    private const string CommonConfigBundleRuleId = "common.config";
    private const string CommonWeaponRuntimePrefabFolderName = "prefabs";
    private const string CommonWeaponRuntimePrefabSuffix = "_weapon.prefab";

    private static bool TryHandleCommonResourcesApi(
        HttpListenerContext context,
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/common-resources" && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, ReadCommonResources(rootPath, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/common-resources/sync" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            CommonResourceSyncRequest request = string.IsNullOrWhiteSpace(body)
                ? new CommonResourceSyncRequest()
                : (JsonSerializer.Deserialize<CommonResourceSyncRequest>(body, jsonOptions) ?? new CommonResourceSyncRequest());
            CommonResourceSyncResponse response = SyncCommonResources(rootPath, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.success ? 200 : 409);
            return true;
        }

        if (path == "/api/authoring/common-resources/save" && context.Request.HttpMethod == "POST")
        {
            string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
            try
            {
                CommonResourceSaveRequest request = string.IsNullOrWhiteSpace(body)
                    ? new CommonResourceSaveRequest()
                    : (JsonSerializer.Deserialize<CommonResourceSaveRequest>(body, jsonOptions) ?? new CommonResourceSaveRequest());
                CommonResourceSaveResponse response = SaveCommonResourceManifestImportHints(rootPath, request, jsonOptions);
                WriteJson(context.Response, response, jsonOptions, response.success ? 200 : 409);
            }
            catch (JsonException ex)
            {
                WriteJson(context.Response, new CommonResourceSaveResponse
                {
                    success = false,
                    message = "Common resource save request is not valid JSON: " + ex.Message
                }, jsonOptions, 400);
            }
            catch (Exception ex)
            {
                WriteJson(context.Response, new CommonResourceSaveResponse
                {
                    success = false,
                    message = "Common resource save failed: " + ex.Message
                }, jsonOptions, 500);
            }
            return true;
        }

        return false;
    }

    internal static CommonResourceState ReadCommonResources(string rootPath, JsonSerializerOptions jsonOptions)
    {
        ResourceMappingProfileV2 profile = ReadCommonResourceMappingProfile(rootPath);
        var state = new CommonResourceState
        {
            success = true,
            sourceRoot = CommonResourcesRootRelativePath,
            generatedRoot = CommonResourcesGeneratedRootRelativePath,
            profilePath = ToProjectRelativePath(rootPath, ResolveResourceMappingProfilePath(rootPath)),
            statePath = CommonResourcesStateRelativePath
        };

        List<CommonResourceManifestRecord> manifests = ScanCommonResourceManifests(rootPath, jsonOptions);
        for (int i = 0; i < manifests.Count; i++)
        {
            CommonResourceManifestRecord manifest = manifests[i];
            state.manifests.Add(manifest);
            for (int itemIndex = 0; itemIndex < manifest.document.Items.Count; itemIndex++)
                state.items.Add(BuildCommonResourceItemStatus(rootPath, profile, manifest, manifest.document.Items[itemIndex], itemIndex));
        }

        state.summary = BuildCommonResourceSummary(state.items);
        return state;
    }

    internal static CommonResourceSyncResponse SyncCommonResources(
        string rootPath,
        CommonResourceSyncRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new CommonResourceSyncRequest();
        CommonResourceState before = ReadCommonResources(rootPath, jsonOptions);
        var selectedIds = new HashSet<string>(request.itemIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        bool explicitSelection = selectedIds.Count > 0;
        bool changedOnly = request.allChanged || !explicitSelection;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var response = new CommonResourceSyncResponse
        {
            success = true,
            dryRun = request.dryRun,
            state = before
        };

        bool hasRequestedOperations = false;
        for (int i = 0; i < before.items.Count; i++)
        {
            if (!ShouldSyncCommonResource(before.items[i], explicitSelection, selectedIds, changedOnly, request.force))
                continue;
            hasRequestedOperations = true;
            break;
        }
        if (!request.dryRun && hasRequestedOperations)
        {
            try
            {
                request.buildTarget = ResourceMappingBuildTargetResolver.Resolve(rootPath, request.buildTarget);
            }
            catch (InvalidOperationException ex)
            {
                response.success = false;
                response.operations.Add(new CommonResourceSyncOperation
                {
                    success = false,
                    code = "RESOURCE_MAPPING_BUILD_TARGET_REQUIRED",
                    message = ex.Message
                });
                return response;
            }
        }

        for (int i = 0; i < before.items.Count; i++)
        {
            CommonResourceItemStatus item = before.items[i];
            if (!ShouldSyncCommonResource(item, explicitSelection, selectedIds, changedOnly, request.force))
                continue;

            CommonResourceSyncOperation operation = SyncCommonResourceItem(rootPath, item, now, request, jsonOptions);
            response.operations.Add(operation);
            if (!operation.success && !operation.skipped)
                response.success = false;
        }

        if (!request.dryRun && response.operations.Count > 0)
        {
            WriteCommonResourceImportState(rootPath, response.operations, now, jsonOptions);
            if (!ApplyCommonResourceProducerSnapshot(rootPath, jsonOptions, request.buildTarget, out string mappingError))
            {
                response.success = false;
                response.operations.Add(new CommonResourceSyncOperation
                {
                    success = false,
                    code = "COMMON_RESOURCE_MAPPING_SYNC_FAILED",
                    message = mappingError
                });
            }
            InvalidateAuthoringResourceCache(rootPath);
        }

        response.state = ReadCommonResources(rootPath, jsonOptions);
        return response;
    }

    /// <remarks>AllocByDesign. Common-resource authoring is a low-frequency producer synchronization path.</remarks>
    private static bool ApplyCommonResourceProducerSnapshot(
        string rootPath,
        JsonSerializerOptions jsonOptions,
        string buildTarget,
        out string error)
    {
        ResourceMappingFirstPartySyncResult applied = ResourceMappingFirstPartyCoordinator.ApplyProducer(
            rootPath, jsonOptions, "demo.samples.global-profile", buildTarget);
        error = applied.Success ? string.Empty : applied.ErrorCode + ": " + applied.Message;
        if (!applied.Success && applied.Diagnostics.Count > 0)
        {
            var details = new List<string>();
            for (int i = 0; i < applied.Diagnostics.Count; i++)
            {
                ResourceMappingDiagnostic diagnostic = applied.Diagnostics[i];
                if (diagnostic != null && diagnostic.Severity == ResourceMappingDiagnosticSeverity.Error)
                    details.Add(diagnostic.Code + " at " + diagnostic.Path + ": " + diagnostic.Message);
            }
            if (details.Count > 0)
                error += " | " + string.Join(" | ", details);
        }
        return applied.Success;
    }

    private static bool ShouldSyncCommonResource(
        CommonResourceItemStatus item,
        bool explicitSelection,
        HashSet<string> selectedIds,
        bool changedOnly,
        bool force)
    {
        if (item == null)
            return false;
        if (explicitSelection && !selectedIds.Contains(item.id))
            return false;
        if (force || explicitSelection && !changedOnly)
            return true;
        return item.status == "MissingGenerated" ||
            item.status == "Changed" ||
            item.status == "ProfileMissing" ||
            item.status == "NeedsUnityImport";
    }

    private static CommonResourceSyncOperation SyncCommonResourceItem(
        string rootPath,
        CommonResourceItemStatus item,
        string syncUtc,
        CommonResourceSyncRequest request,
        JsonSerializerOptions jsonOptions)
    {
        var operation = new CommonResourceSyncOperation
        {
            itemId = item.id,
            resourceKey = item.resourceKey,
            sourcePath = item.sourcePath,
            generatedAssetPath = item.generatedAssetPath,
            rawGeneratedAssetPath = item.rawGeneratedAssetPath,
            unityRuntimeAssetPath = item.unityRuntimeAssetPath,
            statusBefore = item.status
        };

        if (!item.sourceSafe)
            return FailCommonResourceOperation(operation, false, "COMMON_RESOURCE_UNSAFE_SOURCE", item.message);
        if (!item.sourceExists)
            return FailCommonResourceOperation(operation, false, "COMMON_RESOURCE_SOURCE_MISSING", item.message);
        if (!request.force && item.status == "Ready")
            return FailCommonResourceOperation(operation, true, "COMMON_RESOURCE_UP_TO_DATE", "Common resource is already synced and registered.");
        if (request.dryRun)
        {
            operation.success = true;
            operation.skipped = true;
            operation.statusAfter = item.status;
            operation.message = "Dry run: common resource would be copied and profile registration refreshed.";
            return operation;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(item.generatedFullPath) ?? rootPath);
        bool copied = !File.Exists(item.generatedFullPath) ||
            !string.Equals(CharacterPackageHashUtility.ComputeFileSha256(item.generatedFullPath), item.sourceHash, StringComparison.OrdinalIgnoreCase);
        if (copied)
            File.Copy(item.sourceFullPath, item.generatedFullPath, overwrite: true);

        string unityGuid = ReadUnityMetaGuid(item.unityRuntimeFullPath + ".meta");

        operation.success = true;
        operation.skipped = !copied;
        operation.sourceHash = item.sourceHash;
        operation.unityGuid = unityGuid;
        operation.statusAfter = string.IsNullOrWhiteSpace(unityGuid) ? "NeedsUnityImport" : "Ready";
        operation.message = copied
            ? "Copied common source; Unity import and producer synchronization are required next."
            : "Generated asset already matched source hash; producer synchronization will refresh the canonical Profile after Unity import.";
        return operation;
    }

    private static CommonResourceSyncOperation FailCommonResourceOperation(
        CommonResourceSyncOperation operation,
        bool skipped,
        string code,
        string message)
    {
        operation.success = skipped;
        operation.skipped = skipped;
        operation.code = code;
        operation.statusAfter = operation.statusBefore;
        operation.message = string.IsNullOrWhiteSpace(message) ? code : message;
        return operation;
    }

    private static int TryParseNonNegativeInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0
            ? result
            : 0;
    }

    private static CommonResourceSaveResponse SaveCommonResourceManifestImportHints(
        string rootPath,
        CommonResourceSaveRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new CommonResourceSaveRequest();
        string manifestPath = NormalizeImportMaterialRelativePath(request.manifestPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(manifestPath)
            || IsUnsafeImportMaterialRelativePath(manifestPath, allowEmpty: false)
            || !manifestPath.StartsWith(CommonResourcesRootRelativePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return new CommonResourceSaveResponse
            {
                success = false,
                message = "Common resource manifest path must stay under ArtSource/Common."
            };
        }

        string fullManifestPath = ResolveSafePath(rootPath, manifestPath);
        string publicRoot = Path.GetFullPath(Path.Combine(rootPath, CommonResourcesRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsPathAtOrUnderRoot(publicRoot, fullManifestPath) || !File.Exists(fullManifestPath))
        {
            return new CommonResourceSaveResponse
            {
                success = false,
                message = "Common resource manifest was not found under ArtSource/Common."
            };
        }

        CommonResourceSourceManifestDocument manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<CommonResourceSourceManifestDocument>(File.ReadAllText(fullManifestPath), jsonOptions)
                ?? new CommonResourceSourceManifestDocument();
        }
        catch (JsonException ex)
        {
            return new CommonResourceSaveResponse
            {
                success = false,
                message = "Common resource manifest is not valid JSON: " + ex.Message
            };
        }

        manifest.Items ??= new List<CommonResourceSourceManifestItem>();
        CommonResourceSourceManifestItem item = manifest.Items.FirstOrDefault(candidate =>
            candidate != null &&
            ((!string.IsNullOrWhiteSpace(request.itemId) && string.Equals(candidate.Id, request.itemId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(request.resourceKey) && string.Equals(candidate.Target?.ResourceKey, request.resourceKey, StringComparison.Ordinal))));

        if (item == null)
        {
            return new CommonResourceSaveResponse
            {
                success = false,
                message = "Common resource item was not found in manifest."
            };
        }

        item.Target ??= new CommonResourceSourceTarget();
        item.Target.ImportHints = request.importHints ?? new CharacterPackageImportHint();
        WriteJsonFileAtomic(fullManifestPath, manifest, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);

        return new CommonResourceSaveResponse
        {
            success = true,
            message = "Saved common resource import hints.",
            state = ReadCommonResources(rootPath, jsonOptions)
        };
    }

    private static List<string> BuildCommonResourceLabels(CommonResourceItemStatus item, bool unityReady)
    {
        string bundleRuleId = GetCommonResourceBundleRuleId(item);
        var labels = new SortedSet<string>(StringComparer.Ordinal)
        {
            "package.common",
            "public.common",
            "domain." + GetCommonResourceDomain(item),
            "usage." + NormalizeCommonResourceLabel(item.usage)
        };
        if (unityReady)
            labels.Add("bundle." + bundleRuleId);
        return new List<string>(labels);
    }

    private static CommonResourceItemStatus BuildCommonResourceItemStatus(
        string rootPath,
        ResourceMappingProfileV2 profile,
        CommonResourceManifestRecord manifest,
        CommonResourceSourceManifestItem item,
        int itemIndex)
    {
        item ??= new CommonResourceSourceManifestItem();
        string usage = FirstNonEmpty(item.Target?.ResourceUsage, InferCommonResourceUsage(manifest.kind));
        string resourceKind = FirstNonEmpty(item.Target?.ResourceKind, InferCommonResourceKind(usage, manifest.kind));
        string bindTarget = FirstNonEmpty(item.Target?.BindTarget, item.BindTarget);
        string resourceId = manifest.resourceId;
        string resourceKey = FirstNonEmpty(item.Target?.ResourceKey, BuildDefaultCommonResourceKey(manifest.kind, resourceId, usage, bindTarget, item.Id));
        string sourcePath = NormalizeImportMaterialRelativePath(item.Source?.Path ?? string.Empty);
        string sourceProjectPath = JoinImportMaterialPath(CommonResourcesRootRelativePath, sourcePath);
        string generatedAssetPath = JoinImportMaterialPath(CommonResourcesGeneratedRootRelativePath, sourcePath);
        bool usesUnityWrapperPrefab = ShouldUseCommonWeaponWrapperPrefab(manifest.kind, usage);
        string unityRuntimeAssetPath = usesUnityWrapperPrefab
            ? BuildCommonWeaponWrapperPrefabAssetPath(resourceId)
            : generatedAssetPath;
        string sourceFullPath = string.Empty;
        string generatedFullPath = string.Empty;
        string unityRuntimeFullPath = string.Empty;
        bool safe = true;
        string message = string.Empty;

        if (!string.Equals(item.Source?.Kind, CharacterSourceFileKind, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(item.Source?.Scope, CharacterSourcePublicScope, StringComparison.OrdinalIgnoreCase))
        {
            safe = false;
            message = "Common resource source must be a public file reference.";
        }
        else if (string.IsNullOrWhiteSpace(sourcePath) || IsUnsafeImportMaterialRelativePath(sourcePath, allowEmpty: false))
        {
            safe = false;
            message = "Common resource source path must be relative and cannot contain '..'.";
        }
        else
        {
            sourceFullPath = Path.GetFullPath(Path.Combine(rootPath, sourceProjectPath.Replace('/', Path.DirectorySeparatorChar)));
            generatedFullPath = Path.GetFullPath(Path.Combine(rootPath, generatedAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            unityRuntimeFullPath = Path.GetFullPath(Path.Combine(rootPath, unityRuntimeAssetPath.Replace('/', Path.DirectorySeparatorChar)));
            string publicRoot = Path.GetFullPath(Path.Combine(rootPath, CommonResourcesRootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsPathAtOrUnderRoot(publicRoot, sourceFullPath))
            {
                safe = false;
                message = "Common resource source escapes ArtSource/Common.";
            }
        }

        bool sourceExists = safe && File.Exists(sourceFullPath);
        bool generatedExists = safe && File.Exists(generatedFullPath);
        string sourceHash = sourceExists ? CharacterPackageHashUtility.ComputeFileSha256(sourceFullPath) : string.Empty;
        string generatedHash = generatedExists ? CharacterPackageHashUtility.ComputeFileSha256(generatedFullPath) : string.Empty;
        ResourceMappingEntry profileEntry = FindCommonResourceProfileEntry(profile, resourceKey);
        bool profileRuntimeAssetMatches = profileEntry != null &&
            string.Equals(NormalizeImportMaterialRelativePath(profileEntry.SourceLocator?.CachedPath ?? string.Empty), unityRuntimeAssetPath, StringComparison.OrdinalIgnoreCase);
        string profileGuid = profileRuntimeAssetMatches ? profileEntry?.SourceLocator?.UnityGuid ?? string.Empty : string.Empty;
        string metaGuid = File.Exists(unityRuntimeFullPath)
            ? ReadUnityMetaGuid(unityRuntimeFullPath + ".meta")
            : string.Empty;
        string unityGuid = FirstNonEmpty(metaGuid, profileGuid);
        bool runtimeReady = profileEntry != null && profileRuntimeAssetMatches && profileEntry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal && !string.IsNullOrWhiteSpace(unityGuid);
        string status;
        if (!safe)
            status = "Blocked";
        else if (!sourceExists)
            status = "MissingSource";
        else if (!generatedExists)
            status = "MissingGenerated";
        else if (!string.Equals(sourceHash, generatedHash, StringComparison.OrdinalIgnoreCase))
            status = "Changed";
        else if (profileEntry == null)
            status = "ProfileMissing";
        else if (!runtimeReady)
            status = "NeedsUnityImport";
        else
            status = "Ready";

        return new CommonResourceItemStatus
        {
            index = itemIndex,
            id = manifest.kind + ":" + resourceId + ":" + FirstNonEmpty(item.Id, resourceKey),
            manifestPath = manifest.manifestRelativePath,
            kind = manifest.kind,
            resourceId = resourceId,
            displayName = FirstNonEmpty(item.Label, manifest.displayName, resourceId),
            sourceSafe = safe,
            sourceExists = sourceExists,
            sourcePath = sourceProjectPath,
            sourceFullPath = sourceFullPath,
            generatedExists = generatedExists,
            generatedAssetPath = generatedAssetPath,
            generatedFullPath = generatedFullPath,
            rawGeneratedAssetPath = generatedAssetPath,
            rawGeneratedFullPath = generatedFullPath,
            unityRuntimeAssetPath = unityRuntimeAssetPath,
            unityRuntimeFullPath = unityRuntimeFullPath,
            usesUnityWrapperPrefab = usesUnityWrapperPrefab,
            sourceHash = sourceHash,
            generatedHash = generatedHash,
            resourceKey = resourceKey,
            resourceKind = resourceKind,
            usage = usage,
            bindTarget = bindTarget,
            paletteMode = manifest.document.PaletteMode,
            importHints = item.Target?.ImportHints ?? new CharacterPackageImportHint(),
            modelScaleDiagnostic = BuildCommonResourceModelScaleDiagnostic(item.Target, sourceFullPath, generatedFullPath, sourceExists, resourceKind, usage),
            profileRegistered = profileEntry != null,
            profileRuntimeLoadable = runtimeReady,
            unityGuid = unityGuid,
            weaponMaterialBindingStatus = ReadCommonResourceProfileData(profileEntry, "weaponMaterialBindingStatus"),
            weaponMaterialTemplateId = ReadCommonResourceProfileData(profileEntry, "weaponMaterialTemplateId"),
            weaponMaterialPath = ReadCommonResourceProfileData(profileEntry, "weaponMaterialPath"),
            weaponMaterialTextureResourceKey = ReadCommonResourceProfileData(profileEntry, "weaponMaterialTextureResourceKey"),
            weaponMaterialSource = ReadCommonResourceProfileData(profileEntry, "weaponMaterialSource"),
            weaponMaterialRendererCount = TryParseNonNegativeInt(ReadCommonResourceProfileData(profileEntry, "weaponMaterialRendererCount")),
            status = status,
            message = string.IsNullOrWhiteSpace(message) ? BuildCommonResourceStatusMessage(status) : message
        };
    }

    private static CommonResourceSummary BuildCommonResourceSummary(List<CommonResourceItemStatus> items)
    {
        var summary = new CommonResourceSummary();
        if (items == null)
            return summary;
        summary.total = items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            CommonResourceItemStatus item = items[i];
            if (item == null)
                continue;
            if (item.status == "Ready")
                summary.ready++;
            else if (item.status == "MissingSource" || item.status == "Blocked")
                summary.blocked++;
            else if (item.status == "MissingGenerated" || item.status == "Changed" || item.status == "ProfileMissing")
                summary.changed++;
            else if (item.status == "NeedsUnityImport")
                summary.unityPending++;
        }

        return summary;
    }

    private static List<CommonResourceManifestRecord> ScanCommonResourceManifests(string rootPath, JsonSerializerOptions jsonOptions)
    {
        var result = new List<CommonResourceManifestRecord>();
        AddCommonResourceManifests(rootPath, jsonOptions, result, "character", "Characters", CommonCharacterSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "weapon", "Weapons", CommonWeaponSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "texture", "Textures", CommonResourceSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "material", "Materials", CommonResourceSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "audio", "Audio", CommonResourceSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "vfx", "Vfx", CommonResourceSourcesFileName);
        AddCommonResourceManifests(rootPath, jsonOptions, result, "config", "Config", CommonResourceSourcesFileName);
        result.Sort((left, right) => string.Compare(left.manifestRelativePath, right.manifestRelativePath, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    private static void AddCommonResourceManifests(
        string rootPath,
        JsonSerializerOptions jsonOptions,
        List<CommonResourceManifestRecord> result,
        string kind,
        string subDirectory,
        string fileName)
    {
        string root = Path.Combine(rootPath, CommonResourcesRootRelativePath, subDirectory);
        if (!Directory.Exists(root))
            return;

        foreach (string manifestPath in Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories))
        {
            CommonResourceSourceManifestDocument document;
            try
            {
                document = JsonSerializer.Deserialize<CommonResourceSourceManifestDocument>(File.ReadAllText(manifestPath), jsonOptions)
                    ?? new CommonResourceSourceManifestDocument();
            }
            catch (JsonException)
            {
                continue;
            }

            string resourceId = kind == "weapon"
                ? FirstNonEmpty(document.WeaponId, document.ResourceId, Path.GetFileName(Path.GetDirectoryName(manifestPath)))
                : kind == "character"
                    ? FirstNonEmpty(document.CharacterId, document.ResourceId, Path.GetFileName(Path.GetDirectoryName(manifestPath)))
                    : FirstNonEmpty(document.ResourceId, Path.GetFileName(Path.GetDirectoryName(manifestPath)));
            result.Add(new CommonResourceManifestRecord
            {
                kind = kind,
                resourceId = resourceId,
                displayName = document.DisplayName ?? string.Empty,
                manifestRelativePath = ToProjectRelativePath(rootPath, manifestPath),
                document = document
            });
        }
    }

    private static ResourceMappingEntry FindCommonResourceProfileEntry(ResourceMappingProfileV2 profile, string resourceKey)
    {
        return profile?.Entries?.FirstOrDefault(entry =>
            entry != null &&
            entry.ResourceKey != null &&
            string.Equals(entry.ResourceKey.Id, resourceKey, StringComparison.Ordinal));
    }

    private static ResourceMappingProfileV2 ReadCommonResourceMappingProfile(string rootPath)
    {
        string path = ResolveResourceMappingProfilePath(rootPath);
        if (!File.Exists(path)) return new ResourceMappingProfileV2();
        ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(path));
        return read.Success ? read.Profile : new ResourceMappingProfileV2();
    }

    private static string ReadCommonResourceProfileData(ResourceMappingEntry entry, string key)
    {
        return entry?.ProviderData != null && entry.ProviderData.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
    }

    private static string BuildDefaultCommonResourceKey(string kind, string resourceId, string usage = "", string bindTarget = "", string itemId = "")
    {
        string normalizedId = CharacterPackageResourceKeyGenerator.NormalizeSegment(resourceId);
        if (string.Equals(kind, "weapon", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(usage, CharacterPackageResourceUsageIds.Texture, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(bindTarget, "weapon.albedo", StringComparison.OrdinalIgnoreCase))
                return "weapon." + normalizedId + ".texture.albedo";

            string textureId = CharacterPackageResourceKeyGenerator.NormalizeSegment(FirstNonEmpty(itemId, bindTarget, "texture"));
            return "weapon." + normalizedId + ".texture." + textureId;
        }

        if (string.Equals(kind, "weapon", StringComparison.OrdinalIgnoreCase))
            return "weapon." + normalizedId + ".model";
        if (string.Equals(kind, "character", StringComparison.OrdinalIgnoreCase))
            return "character." + normalizedId + ".model";
        return CharacterPackageResourceKeyGenerator.NormalizeSegment(kind) + "." + normalizedId;
    }

    private static string InferCommonResourceUsage(string kind)
    {
        string normalized = string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
        return normalized switch
        {
            "weapon" => CharacterPackageResourceUsageIds.WeaponModel,
            "texture" => CharacterPackageResourceUsageIds.Texture,
            "material" => CharacterPackageResourceUsageIds.Material,
            "audio" => CharacterPackageResourceUsageIds.AudioCue,
            "vfx" => CharacterPackageResourceUsageIds.VfxCue,
            "config" => CharacterPackageResourceUsageIds.CharacterConfig,
            _ => CharacterPackageResourceUsageIds.CharacterModel
        };
    }

    private static string InferCommonResourceKind(string usage, string kind)
    {
        string normalizedUsage = string.IsNullOrWhiteSpace(usage) ? string.Empty : usage.Trim().ToLowerInvariant();
        if (normalizedUsage == CharacterPackageResourceUsageIds.Texture.ToLowerInvariant() ||
            normalizedUsage == CharacterPackageResourceUsageIds.PreviewThumbnail.ToLowerInvariant())
            return "Texture";
        if (normalizedUsage == CharacterPackageResourceUsageIds.Material.ToLowerInvariant())
            return "Material";
        if (normalizedUsage == CharacterPackageResourceUsageIds.AudioCue.ToLowerInvariant())
            return "Audio";
        if (normalizedUsage == CharacterPackageResourceUsageIds.VfxCue.ToLowerInvariant())
            return "Vfx";
        if (normalizedUsage == CharacterPackageResourceUsageIds.CharacterConfig.ToLowerInvariant())
            return "Config";
        string normalizedKind = string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
        return normalizedKind switch
        {
            "texture" => "Texture",
            "material" => "Material",
            "audio" => "Audio",
            "vfx" => "Vfx",
            "config" => "Config",
            _ => "Model"
        };
    }

    private static string GetCommonResourceUnityTypeId(CommonResourceItemStatus item)
    {
        string kind = item?.resourceKind ?? string.Empty;
        if (string.Equals(kind, "Texture", StringComparison.OrdinalIgnoreCase))
            return "Texture2D";
        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
            return "Material";
        if (string.Equals(kind, "Audio", StringComparison.OrdinalIgnoreCase))
            return "AudioClip";
        if (string.Equals(kind, "Vfx", StringComparison.OrdinalIgnoreCase))
            return "GameObject";
        if (string.Equals(kind, "Config", StringComparison.OrdinalIgnoreCase))
            return "TextAsset";
        return "GameObject";
    }

    private static string GetCommonResourceBundleRuleId(CommonResourceItemStatus item)
    {
        string kind = item?.resourceKind ?? string.Empty;
        if (string.Equals(kind, "Texture", StringComparison.OrdinalIgnoreCase))
            return CommonTexturesBundleRuleId;
        if (string.Equals(kind, "Material", StringComparison.OrdinalIgnoreCase))
            return CommonMaterialsBundleRuleId;
        if (string.Equals(kind, "Audio", StringComparison.OrdinalIgnoreCase))
            return CommonAudioBundleRuleId;
        if (string.Equals(kind, "Vfx", StringComparison.OrdinalIgnoreCase))
            return CommonVfxBundleRuleId;
        if (string.Equals(kind, "Config", StringComparison.OrdinalIgnoreCase))
            return CommonConfigBundleRuleId;
        return CommonModelsBundleRuleId;
    }

    private static string GetCommonResourceDomain(CommonResourceItemStatus item)
    {
        string usage = item?.usage ?? string.Empty;
        if (string.Equals(usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase))
            return "character";
        if (string.Equals(usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.OrdinalIgnoreCase))
            return "weapon";
        return NormalizeCommonResourceLabel(item?.resourceKind ?? item?.kind ?? "resource");
    }

    private static bool ShouldUseCommonWeaponWrapperPrefab(string kind, string usage)
    {
        return string.Equals(kind, "weapon", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommonWeaponWrapperPrefabAssetPath(string resourceId)
    {
        string safeResourceId = NormalizeCommonResourceLabel(FirstNonEmpty(resourceId, "weapon"));
        return JoinImportMaterialPath(
            JoinImportMaterialPath(
                JoinImportMaterialPath(
                    JoinImportMaterialPath(CommonResourcesGeneratedRootRelativePath, "Weapons"),
                    safeResourceId),
                CommonWeaponRuntimePrefabFolderName),
            safeResourceId + CommonWeaponRuntimePrefabSuffix);
    }

    private static string BuildCommonResourceStatusMessage(string status)
    {
        return status switch
        {
            "MissingSource" => "Source file is missing under ArtSource/Common.",
            "MissingGenerated" => "Common source has not been copied to Assets/MxFrameworkGenerated/Common.",
            "Changed" => "Generated common asset differs from the source hash.",
            "ProfileMissing" => "Global Resource Build Profile does not contain this common resource key.",
            "NeedsUnityImport" => "Generated asset is copied, but Unity has not imported it or no guid is available yet.",
            "Ready" => "Common resource is copied, registered, and has a Unity guid.",
            _ => "Common resource needs attention."
        };
    }

    private static CommonResourceModelScaleDiagnostic BuildCommonResourceModelScaleDiagnostic(
        CommonResourceSourceTarget target,
        string sourceFullPath,
        string generatedFullPath,
        bool sourceExists,
        string resourceKind,
        string usage)
    {
        var diagnostic = new CommonResourceModelScaleDiagnostic();
        if (!string.Equals(resourceKind, "Model", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(usage, CharacterPackageResourceUsageIds.WeaponModel, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase))
        {
            diagnostic.status = "NotModel";
            diagnostic.reason = "Scale diagnostics only apply to model resources.";
            return diagnostic;
        }

        if (!sourceExists || string.IsNullOrWhiteSpace(sourceFullPath))
        {
            diagnostic.status = "SourceMissing";
            diagnostic.reason = "Source model is missing.";
            return diagnostic;
        }

        if (!string.Equals(Path.GetExtension(sourceFullPath), ".fbx", StringComparison.OrdinalIgnoreCase))
        {
            diagnostic.status = "NotFbx";
            diagnostic.reason = "Unit scale diagnostics currently inspect FBX source files.";
            return diagnostic;
        }

        if (!TryReadFbxUnitScaleFactor(sourceFullPath, out double unitScaleFactor))
        {
            diagnostic.status = "Unknown";
            diagnostic.reason = "FBX UnitScaleFactor could not be read.";
            return diagnostic;
        }

        float previewUnitScale = ClampScale((float)(unitScaleFactor / 100.0d));
        float currentScale = GetRepresentativeWrapperScale(target?.ImportHints?.ModelWrapperPose);
        diagnostic.unitScaleFactor = unitScaleFactor;
        diagnostic.previewUnitScale = previewUnitScale;
        diagnostic.recommendedScale = 1f;
        diagnostic.currentScale = currentScale;
        diagnostic.source = "FbxUnitScaleFactor";

        if (Math.Abs(previewUnitScale - 1f) <= 0.0001f)
        {
            diagnostic.status = "UnitOk";
            diagnostic.reason = "FBX unit metadata already maps to meter scale.";
            return diagnostic;
        }

        diagnostic.likelyUnitScaleMismatch = previewUnitScale <= 0.1f || previewUnitScale >= 10f;
        if (TryReadUnityFbxUnitConversion(generatedFullPath, out bool useFileUnits, out bool useFileScale)
            && (useFileUnits || useFileScale))
        {
            diagnostic.source = "UnityModelImporter";
            diagnostic.unityConvertsUnits = true;
            if (IsScaleApproximately(currentScale, previewUnitScale))
            {
                diagnostic.status = "WrapperScaleMismatch";
                diagnostic.reason = "Unity ModelImporter Convert Units already applies FBX unit conversion; wrapper scale appears to duplicate it.";
            }
            else
            {
                diagnostic.status = "UnityConvertsUnits";
                diagnostic.reason = "Unity ModelImporter Convert Units applies FBX unit conversion; wrapper scale should only represent art transform edits.";
            }

            return diagnostic;
        }

        diagnostic.status = "PreviewUnitScale";
        diagnostic.reason = "CharacterStudio should apply this unit scale only while previewing the raw FBX; it should not be saved into the model wrapper pose.";
        return diagnostic;
    }

    private static bool TryReadUnityFbxUnitConversion(string generatedFullPath, out bool useFileUnits, out bool useFileScale)
    {
        useFileUnits = false;
        useFileScale = false;
        if (string.IsNullOrWhiteSpace(generatedFullPath)
            || !string.Equals(Path.GetExtension(generatedFullPath), ".fbx", StringComparison.OrdinalIgnoreCase))
            return false;

        string metaPath = generatedFullPath + ".meta";
        if (!File.Exists(metaPath))
            return false;

        foreach (string line in File.ReadLines(metaPath))
        {
            string trimmed = line.Trim();
            if (trimmed.Equals("useFileUnits: 1", StringComparison.OrdinalIgnoreCase))
                useFileUnits = true;
            else if (trimmed.Equals("useFileScale: 1", StringComparison.OrdinalIgnoreCase))
                useFileScale = true;
        }

        return true;
    }

    private static bool TryReadFbxUnitScaleFactor(string sourceFullPath, out double unitScaleFactor)
    {
        unitScaleFactor = 0;
        try
        {
            byte[] bytes = File.ReadAllBytes(sourceFullPath);
            if (TryReadFbxAsciiUnitScaleFactor(bytes, out unitScaleFactor))
                return true;
            return IsFbxBinary(bytes) && TryReadFbxBinaryUnitScaleFactor(bytes, out unitScaleFactor);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadFbxAsciiUnitScaleFactor(byte[] bytes, out double unitScaleFactor)
    {
        unitScaleFactor = 0;
        int length = Math.Min(bytes?.Length ?? 0, 1024 * 1024);
        if (length == 0)
            return false;

        string text = Encoding.UTF8.GetString(bytes, 0, length);
        using var reader = new StringReader(text);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!line.Contains("UnitScaleFactor", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string token = parts[i].Trim().Trim('"');
                if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out unitScaleFactor))
                    return true;
            }
        }

        return false;
    }

    private static bool IsFbxBinary(byte[] bytes)
    {
        if (bytes == null || bytes.Length < 27)
            return false;
        string header = Encoding.ASCII.GetString(bytes, 0, Math.Min(21, bytes.Length));
        return header.StartsWith("Kaydara FBX Binary", StringComparison.Ordinal);
    }

    private static bool TryReadFbxBinaryUnitScaleFactor(byte[] bytes, out double unitScaleFactor)
    {
        unitScaleFactor = 0;
        if (bytes == null || bytes.Length < 27)
            return false;

        int version = BitConverter.ToInt32(bytes, 23);
        int offset = 27;
        return TryScanFbxBinaryNodes(bytes, version, ref offset, bytes.Length, 0, out unitScaleFactor);
    }

    private static bool TryScanFbxBinaryNodes(
        byte[] bytes,
        int version,
        ref int offset,
        long parentEndOffset,
        int depth,
        out double unitScaleFactor)
    {
        unitScaleFactor = 0;
        if (depth > 64)
            return false;

        int nullRecordSize = version >= 7500 ? 25 : 13;
        while (offset + nullRecordSize <= bytes.Length && offset < parentEndOffset)
        {
            int nodeStart = offset;
            if (IsFbxNullRecord(bytes, nodeStart, nullRecordSize))
            {
                offset = nodeStart + nullRecordSize;
                return false;
            }

            if (!TryReadFbxBinaryNodeHeader(bytes, version, ref offset, out long endOffset, out long propertyCount, out long propertyListLength, out string name)
                || endOffset <= 0
                || endOffset > bytes.Length
                || endOffset <= offset)
            {
                offset = nodeStart + nullRecordSize;
                continue;
            }

            long propertiesEnd = Math.Min(bytes.Length, offset + propertyListLength);
            List<FbxPropertyValue> properties = ReadFbxBinaryProperties(bytes, ref offset, propertiesEnd, propertyCount);
            offset = (int)Math.Min(propertiesEnd, bytes.Length);

            if (string.Equals(name, "P", StringComparison.Ordinal)
                && TryGetUnitScaleFactorFromFbxPropertyNode(properties, out unitScaleFactor))
            {
                return true;
            }

            int childOffset = offset;
            long childEnd = Math.Max(childOffset, endOffset - nullRecordSize);
            if (childOffset < childEnd)
            {
                if (TryScanFbxBinaryNodes(bytes, version, ref childOffset, childEnd, depth + 1, out unitScaleFactor))
                    return true;
            }

            offset = (int)Math.Min(endOffset, bytes.Length);
        }

        return false;
    }

    private static bool TryReadFbxBinaryNodeHeader(
        byte[] bytes,
        int version,
        ref int offset,
        out long endOffset,
        out long propertyCount,
        out long propertyListLength,
        out string name)
    {
        endOffset = 0;
        propertyCount = 0;
        propertyListLength = 0;
        name = string.Empty;

        if (version >= 7500)
        {
            if (offset + 25 > bytes.Length)
                return false;
            endOffset = (long)BitConverter.ToUInt64(bytes, offset);
            propertyCount = (long)BitConverter.ToUInt64(bytes, offset + 8);
            propertyListLength = (long)BitConverter.ToUInt64(bytes, offset + 16);
            offset += 24;
        }
        else
        {
            if (offset + 13 > bytes.Length)
                return false;
            endOffset = BitConverter.ToUInt32(bytes, offset);
            propertyCount = BitConverter.ToUInt32(bytes, offset + 4);
            propertyListLength = BitConverter.ToUInt32(bytes, offset + 8);
            offset += 12;
        }

        int nameLength = bytes[offset++];
        if (offset + nameLength > bytes.Length)
            return false;
        name = Encoding.UTF8.GetString(bytes, offset, nameLength);
        offset += nameLength;
        return true;
    }

    private static List<FbxPropertyValue> ReadFbxBinaryProperties(
        byte[] bytes,
        ref int offset,
        long propertiesEnd,
        long propertyCount)
    {
        var properties = new List<FbxPropertyValue>();
        for (long i = 0; i < propertyCount && offset < propertiesEnd && offset < bytes.Length; i++)
        {
            if (!TryReadFbxBinaryProperty(bytes, ref offset, propertiesEnd, out FbxPropertyValue value))
                break;
            properties.Add(value);
        }

        return properties;
    }

    private static bool TryReadFbxBinaryProperty(byte[] bytes, ref int offset, long propertiesEnd, out FbxPropertyValue value)
    {
        value = new FbxPropertyValue();
        if (offset >= propertiesEnd || offset >= bytes.Length)
            return false;

        char type = (char)bytes[offset++];
        switch (type)
        {
            case 'Y':
                if (offset + 2 > propertiesEnd) return false;
                value.NumericValue = BitConverter.ToInt16(bytes, offset);
                offset += 2;
                return true;
            case 'C':
                if (offset + 1 > propertiesEnd) return false;
                value.NumericValue = bytes[offset++] != 0 ? 1 : 0;
                return true;
            case 'I':
                if (offset + 4 > propertiesEnd) return false;
                value.NumericValue = BitConverter.ToInt32(bytes, offset);
                offset += 4;
                return true;
            case 'F':
                if (offset + 4 > propertiesEnd) return false;
                value.NumericValue = BitConverter.ToSingle(bytes, offset);
                offset += 4;
                return true;
            case 'D':
                if (offset + 8 > propertiesEnd) return false;
                value.NumericValue = BitConverter.ToDouble(bytes, offset);
                offset += 8;
                return true;
            case 'L':
                if (offset + 8 > propertiesEnd) return false;
                value.NumericValue = BitConverter.ToInt64(bytes, offset);
                offset += 8;
                return true;
            case 'S':
            case 'R':
                if (offset + 4 > propertiesEnd) return false;
                int length = BitConverter.ToInt32(bytes, offset);
                offset += 4;
                if (length < 0 || offset + length > propertiesEnd) return false;
                if (type == 'S')
                    value.StringValue = Encoding.UTF8.GetString(bytes, offset, length);
                offset += length;
                return true;
            case 'b':
            case 'c':
            case 'd':
            case 'f':
            case 'i':
            case 'l':
                if (offset + 12 > propertiesEnd) return false;
                int arrayLength = BitConverter.ToInt32(bytes, offset);
                int compressedLength = BitConverter.ToInt32(bytes, offset + 8);
                offset += 12;
                if (arrayLength < 0 || compressedLength < 0 || offset + compressedLength > propertiesEnd) return false;
                offset += compressedLength;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetUnitScaleFactorFromFbxPropertyNode(List<FbxPropertyValue> properties, out double unitScaleFactor)
    {
        unitScaleFactor = 0;
        if (properties == null || properties.Count == 0 || !string.Equals(properties[0].StringValue, "UnitScaleFactor", StringComparison.Ordinal))
            return false;

        for (int i = properties.Count - 1; i >= 1; i--)
        {
            if (properties[i].NumericValue.HasValue)
            {
                unitScaleFactor = properties[i].NumericValue.Value;
                return true;
            }
        }

        return false;
    }

    private static bool IsFbxNullRecord(byte[] bytes, int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset + length > bytes.Length)
            return false;
        for (int i = 0; i < length; i++)
        {
            if (bytes[offset + i] != 0)
                return false;
        }

        return true;
    }

    private static float GetRepresentativeWrapperScale(CharacterAuthoringLocalPose pose)
    {
        CharacterAuthoringVector3 scale = pose?.Scale;
        if (scale == null)
            return 1f;
        float x = scale.X > 0 ? scale.X : 1f;
        float y = scale.Y > 0 ? scale.Y : x;
        float z = scale.Z > 0 ? scale.Z : x;
        return (x + y + z) / 3f;
    }

    private static float ClampScale(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
            return 1f;
        return Math.Clamp(value, 0.0001f, 1000f);
    }

    private static bool IsScaleApproximately(float left, float right)
    {
        float tolerance = Math.Max(0.0001f, Math.Abs(right) * 0.05f);
        return Math.Abs(left - right) <= tolerance;
    }

    private static string ReadUnityMetaGuid(string metaPath)
    {
        if (string.IsNullOrWhiteSpace(metaPath) || !File.Exists(metaPath))
            return string.Empty;
        foreach (string line in File.ReadLines(metaPath))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
                continue;
            return trimmed.Substring("guid:".Length).Trim();
        }

        return string.Empty;
    }

    private static string NormalizeCommonResourceLabel(string value)
    {
        return CharacterPackageResourceKeyGenerator.NormalizeSegment(value);
    }

    private static void WriteCommonResourceImportState(
        string rootPath,
        List<CommonResourceSyncOperation> operations,
        string syncUtc,
        JsonSerializerOptions jsonOptions)
    {
        var state = new CommonResourceImportStateDocument
        {
            schemaVersion = "1.0",
            sourceRoot = CommonResourcesRootRelativePath,
            lastSyncUtc = syncUtc,
            operations = operations ?? new List<CommonResourceSyncOperation>()
        };
        string statePath = ResolveSafePath(rootPath, CommonResourcesStateRelativePath);
        WriteJsonFileAtomic(statePath, state, jsonOptions);
    }

    internal sealed class CommonResourceSyncRequest
    {
        public string buildTarget { get; set; } = string.Empty;
        public bool allChanged { get; set; }
        public bool force { get; set; }
        public bool dryRun { get; set; }
        public List<string> itemIds { get; set; } = new();
    }

    internal sealed class CommonResourceSyncResponse
    {
        public bool success { get; set; }
        public bool dryRun { get; set; }
        public List<CommonResourceSyncOperation> operations { get; set; } = new();
        public CommonResourceState state { get; set; } = new();
    }

    internal sealed class CommonResourceSaveRequest
    {
        public string manifestPath { get; set; } = string.Empty;
        public string itemId { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public CharacterPackageImportHint importHints { get; set; } = new();
    }

    internal sealed class CommonResourceSaveResponse
    {
        public bool success { get; set; }
        public string message { get; set; } = string.Empty;
        public CommonResourceState state { get; set; } = new();
    }

    internal sealed class CommonResourceSyncOperation
    {
        public string itemId { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public string sourcePath { get; set; } = string.Empty;
        public string generatedAssetPath { get; set; } = string.Empty;
        public string rawGeneratedAssetPath { get; set; } = string.Empty;
        public string unityRuntimeAssetPath { get; set; } = string.Empty;
        public string sourceHash { get; set; } = string.Empty;
        public string unityGuid { get; set; } = string.Empty;
        public string statusBefore { get; set; } = string.Empty;
        public string statusAfter { get; set; } = string.Empty;
        public bool success { get; set; }
        public bool skipped { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CommonResourceState
    {
        public bool success { get; set; }
        public string sourceRoot { get; set; } = string.Empty;
        public string generatedRoot { get; set; } = string.Empty;
        public string profilePath { get; set; } = string.Empty;
        public string statePath { get; set; } = string.Empty;
        public CommonResourceSummary summary { get; set; } = new();
        public List<CommonResourceManifestRecord> manifests { get; set; } = new();
        public List<CommonResourceItemStatus> items { get; set; } = new();
    }

    internal sealed class CommonResourceSummary
    {
        public int total { get; set; }
        public int ready { get; set; }
        public int changed { get; set; }
        public int unityPending { get; set; }
        public int blocked { get; set; }
    }

    internal sealed class CommonResourceItemStatus
    {
        public int index { get; set; }
        public string id { get; set; } = string.Empty;
        public string manifestPath { get; set; } = string.Empty;
        public string kind { get; set; } = string.Empty;
        public string resourceId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public bool sourceSafe { get; set; }
        public bool sourceExists { get; set; }
        public string sourcePath { get; set; } = string.Empty;
        public string sourceFullPath { get; set; } = string.Empty;
        public bool generatedExists { get; set; }
        public string generatedAssetPath { get; set; } = string.Empty;
        public string generatedFullPath { get; set; } = string.Empty;
        public string rawGeneratedAssetPath { get; set; } = string.Empty;
        public string rawGeneratedFullPath { get; set; } = string.Empty;
        public string unityRuntimeAssetPath { get; set; } = string.Empty;
        public string unityRuntimeFullPath { get; set; } = string.Empty;
        public bool usesUnityWrapperPrefab { get; set; }
        public string sourceHash { get; set; } = string.Empty;
        public string generatedHash { get; set; } = string.Empty;
        public string resourceKey { get; set; } = string.Empty;
        public string resourceKind { get; set; } = string.Empty;
        public string usage { get; set; } = string.Empty;
        public string bindTarget { get; set; } = string.Empty;
        public string paletteMode { get; set; } = string.Empty;
        public CharacterPackageImportHint importHints { get; set; } = new();
        public CommonResourceModelScaleDiagnostic modelScaleDiagnostic { get; set; } = new();
        public bool profileRegistered { get; set; }
        public bool profileRuntimeLoadable { get; set; }
        public string unityGuid { get; set; } = string.Empty;
        public string weaponMaterialBindingStatus { get; set; } = string.Empty;
        public string weaponMaterialTemplateId { get; set; } = string.Empty;
        public string weaponMaterialPath { get; set; } = string.Empty;
        public string weaponMaterialTextureResourceKey { get; set; } = string.Empty;
        public string weaponMaterialSource { get; set; } = string.Empty;
        public int weaponMaterialRendererCount { get; set; }
        public string status { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class CommonResourceManifestRecord
    {
        public string kind { get; set; } = string.Empty;
        public string resourceId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public string manifestRelativePath { get; set; } = string.Empty;
        public CommonResourceSourceManifestDocument document { get; set; } = new();
    }

    internal sealed class CommonResourceSourceManifestDocument
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string ResourceId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public string WeaponId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PaletteMode { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = CommonResourcesRootRelativePath;
        public List<CommonResourceSourceManifestItem> Items { get; set; } = new();
    }

    internal sealed class CommonResourceSourceManifestItem
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string PresetId { get; set; } = string.Empty;
        public string BindTarget { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public CharacterSourceReference Source { get; set; } = new();
        public CommonResourceSourceTarget Target { get; set; } = new();
    }

    internal sealed class CommonResourceSourceTarget
    {
        public string ResourceKey { get; set; } = string.Empty;
        public string ResourceKind { get; set; } = string.Empty;
        public string ResourceUsage { get; set; } = string.Empty;
        public string BindTarget { get; set; } = string.Empty;
        public string Slot { get; set; } = string.Empty;
        public CharacterPackageImportHint ImportHints { get; set; } = new();
    }

    internal sealed class CommonResourceModelScaleDiagnostic
    {
        public string status { get; set; } = string.Empty;
        public string source { get; set; } = string.Empty;
        public string reason { get; set; } = string.Empty;
        public bool likelyUnitScaleMismatch { get; set; }
        public bool unityConvertsUnits { get; set; }
        public double? unitScaleFactor { get; set; }
        public float previewUnitScale { get; set; } = 1f;
        public float recommendedScale { get; set; } = 1f;
        public float currentScale { get; set; } = 1f;
    }

    private sealed class FbxPropertyValue
    {
        public string StringValue { get; set; } = string.Empty;
        public double? NumericValue { get; set; }
    }

    internal sealed class CommonResourceImportStateDocument
    {
        public string schemaVersion { get; set; } = "1.0";
        public string sourceRoot { get; set; } = string.Empty;
        public string lastSyncUtc { get; set; } = string.Empty;
        public List<CommonResourceSyncOperation> operations { get; set; } = new();
    }
}
