using System.Globalization;
using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string AudioPreviewRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/preview_request.json";
    private const string AudioPreviewStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/status";
    private const string AudioSnapshotRefreshRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/refresh_snapshot_request.json";
    private const string AudioGeneratedCatalogRelativePath = "Assets/MxFrameworkGenerated/Audio/audio_runtime_catalog.json";
    private const string AudioStreamingCatalogRelativePath = "Assets/StreamingAssets/MxFramework/Audio/audio_runtime_catalog.json";
    private const string AudioGenerationManifestRelativePath = "Assets/Config/MxFramework/Audio/audio_generation_manifest.json";
    private const string AudioAdoptionRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/pending_adoption.json";
    private const string AudioAdoptionStatusRelativePath = "Temp/MxFrameworkAuthoring/Audio/adoption_status.json";

    private static bool TryHandleAudioWorkbenchApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/audio/state" && context.Request.HttpMethod == "GET")
        {
            string package = ResolveCharacterPackageRelative(context, rootPath, defaultPackage);
            WriteJson(context.Response, ReadAudioWorkbenchState(rootPath, package, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/validate" && context.Request.HttpMethod == "POST")
        {
            AudioWorkbenchManifestRequest request = ReadAudioRequest<AudioWorkbenchManifestRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            WriteJson(context.Response, ValidateAudioWorkbenchManifest(rootPath, package, request.manifest, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/save" && context.Request.HttpMethod == "POST")
        {
            AudioWorkbenchManifestRequest request = ReadAudioRequest<AudioWorkbenchManifestRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            AudioWorkbenchSaveResponse response = SaveAudioWorkbenchDraft(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/publish" && context.Request.HttpMethod == "POST")
        {
            AudioWorkbenchPublishRequest request = ReadAudioRequest<AudioWorkbenchPublishRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            AudioWorkbenchPublishResponse response = PublishAudioWorkbench(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/bulk-approve" && context.Request.HttpMethod == "POST")
        {
            AudioWorkbenchBulkApprovalRequest request = ReadAudioRequest<AudioWorkbenchBulkApprovalRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            AudioWorkbenchSaveResponse response = BulkApproveAllAudioCues(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/direct-adopt-current" && context.Request.HttpMethod == "POST")
        {
            AudioWorkbenchDirectAdoptionRequest request = ReadAudioRequest<AudioWorkbenchDirectAdoptionRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            AudioWorkbenchSaveResponse response = DirectAdoptCurrentAudioForAllBasis(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/preview" && context.Request.HttpMethod == "POST")
        {
            AudioPreviewApiRequest request = ReadAudioRequest<AudioPreviewApiRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            WriteJson(context.Response, WriteAudioPreviewRequest(rootPath, package, request, jsonOptions), jsonOptions, 202);
            return true;
        }

        if (path == "/api/authoring/audio/preview-status" && context.Request.HttpMethod == "GET")
        {
            string requestId = context.Request.QueryString["id"] ?? string.Empty;
            WriteJson(context.Response, ReadAudioPreviewStatus(rootPath, requestId, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/stop-preview" && context.Request.HttpMethod == "POST")
        {
            AudioPreviewApiRequest request = ReadAudioRequest<AudioPreviewApiRequest>(context, jsonOptions);
            request.action = "Stop";
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            WriteJson(context.Response, WriteAudioPreviewRequest(rootPath, package, request, jsonOptions), jsonOptions, 202);
            return true;
        }

        if (path == "/api/authoring/audio/refresh-fmod-snapshot" && context.Request.HttpMethod == "POST")
        {
            string requestId = Guid.NewGuid().ToString("N");
            WriteJsonFileAtomic(
                ResolveAudioBridgePath(rootPath, AudioSnapshotRefreshRequestRelativePath),
                new AudioSnapshotRefreshRequestFile
                {
                    requestId = requestId,
                    createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                },
                jsonOptions);
            WriteJson(context.Response, new { requestId, status = "pending", message = "已请求 Unity 刷新 FMOD snapshot。" }, jsonOptions, 202);
            return true;
        }

        if (TryHandlePhysicalAudioManagedEventApi(context, rootPath, defaultPackage, jsonOptions, path))
            return true;

        if (TryHandlePhysicalAudioApi(context, rootPath, defaultPackage, jsonOptions, path))
            return true;

        if (TryHandleAudioGenerationApi(context, rootPath, defaultPackage, jsonOptions, path))
            return true;

        return false;
    }

    internal static object ReadAudioWorkbenchState(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AudioSemanticManifestDocument manifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        manifest = AudioAuthoringManifestV2.Upgrade(manifest, snapshot);
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        AddAudioPreviewEvidenceDiagnostics(rootPath, manifest, validation, jsonOptions);
        AddAudioAdoptionDiagnostics(rootPath, manifest, validation, jsonOptions);
        string snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot);
        string generatedPath = ResolveAudioBridgePath(rootPath, AudioGeneratedCatalogRelativePath);
        string streamingPath = ResolveAudioBridgePath(rootPath, AudioStreamingCatalogRelativePath);
        string generationPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        AudioGenerationManifestDocument generationManifest = UpgradeGenerationManifest(
            ReadOptionalJsonFile<AudioGenerationManifestDocument>(rootPath, generationPath, jsonOptions));
        string generatedCatalogHash = ComputeOptionalFileHash(generatedPath);
        string playerCatalogHash = ComputeOptionalFileHash(streamingPath);
        bool catalogsLinked = !string.IsNullOrWhiteSpace(generatedCatalogHash)
            && string.Equals(generatedCatalogHash, playerCatalogHash, StringComparison.OrdinalIgnoreCase);
        string gameBankHash = ReadCurrentGameBankHash(rootPath);
        var cueStates = new List<object>();
        var runtimeSyncStates = new List<AudioCueRuntimeSyncState>();
        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = manifest.Cues[i];
            if (cue == null)
                continue;
            AudioEventAuthoringDefinition audioEvent =
                AudioAuthoringManifestV2.FindEventDefinition(manifest, cue.EventDefinitionId);
            AudioCueRuntimeSyncState runtimeSync = BuildAudioCueRuntimeSyncState(
                rootPath,
                cue,
                audioEvent,
                generationManifest,
                gameBankHash,
                catalogsLinked);
            runtimeSyncStates.Add(runtimeSync);
            cueStates.Add(new
            {
                audioCueId = cue.AudioCueId,
                status = AudioAuthoringManifestV2.GetCueStatus(manifest, cue, snapshot),
                referenceCount = FindAudioSemanticReferences(rootPath, packageRelative, cue.AudioCueId, jsonOptions).Count,
                bindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, audioEvent),
                syncStatus = runtimeSync.status,
                syncLabel = runtimeSync.label,
                syncDetail = runtimeSync.detail,
                activeSetId = runtimeSync.activeSetId,
                publishedSetId = runtimeSync.publishedSetId,
                activeBankHash = runtimeSync.activeBankHash,
                gameBankHash = runtimeSync.gameBankHash,
                sourceCount = runtimeSync.sourceCount
            });
        }

        AudioPhysicalStateResponse physicalState = ReadAudioPhysicalState(rootPath, manifest, snapshot, jsonOptions);
        AudioRuntimeActivationState runtimeActivation = BuildAudioRuntimeActivationState(
            rootPath,
            manifest,
            validation,
            snapshot,
            generationManifest,
            physicalState,
            jsonOptions);
        return new
        {
            package = packageRelative,
            manifestPath = ToProjectRelativePath(rootPath, manifestPath),
            contentHash = ComputeOptionalFileHash(manifestPath),
            manifest,
            validation,
            cueStates,
            runtimeSync = new
            {
                gameBankHash,
                syncedCount = runtimeSyncStates.Count(item => string.Equals(item.status, "Synced", StringComparison.Ordinal)),
                pendingCount = runtimeSyncStates.Count(item =>
                    string.Equals(item.status, "FmodApplied", StringComparison.Ordinal)
                    || string.Equals(item.status, "BankOutOfSync", StringComparison.Ordinal)
                    || string.Equals(item.status, "CandidateOnly", StringComparison.Ordinal)),
                silentPlaceholderCount = runtimeSyncStates.Count(item => string.Equals(item.status, "SilentPlaceholder", StringComparison.Ordinal))
            },
            snapshot,
            snapshotPath = ToProjectRelativePath(rootPath, snapshotPath),
            snapshotHash,
            snapshotStale = snapshot == null || snapshot.CacheStale,
            generatedCatalog = new
            {
                path = AudioGeneratedCatalogRelativePath,
                exists = File.Exists(generatedPath),
                hash = ComputeOptionalFileHash(generatedPath)
            },
            playerMirror = new
            {
                path = AudioStreamingCatalogRelativePath,
                exists = File.Exists(streamingPath),
                hash = ComputeOptionalFileHash(streamingPath)
            },
            audioGeneration = new
            {
                manifestPath = AudioGenerationManifestRelativePath,
                manifestHash = ComputeOptionalFileHash(generationPath),
                manifest = generationManifest,
                adoptionPending = File.Exists(ResolveAudioBridgePath(rootPath, AudioAdoptionRequestRelativePath)),
                adoptionStatus = ReadOptionalJsonFile<object>(rootPath, ResolveAudioBridgePath(rootPath, AudioAdoptionStatusRelativePath), jsonOptions)
            },
            audioPhysical = physicalState,
            runtimeActivation
        };
    }

    private static AudioCueRuntimeSyncState BuildAudioCueRuntimeSyncState(
        string rootPath,
        AudioCueAuthoringDefinition cue,
        AudioEventAuthoringDefinition audioEvent,
        AudioGenerationManifestDocument generationManifest,
        string gameBankHash,
        bool catalogsLinked)
    {
        AudioAdoptionEventStateV3 eventState = generationManifest?.EventStates?.Find(item =>
            item != null && string.Equals(item.EventDefinitionId, cue?.EventDefinitionId, StringComparison.Ordinal));
        AudioAdoptionSetV3 activeSet = generationManifest?.AdoptionSets?.Find(item =>
            item != null && string.Equals(item.SetId, eventState?.ActiveSetId, StringComparison.Ordinal));
        var result = new AudioCueRuntimeSyncState
        {
            audioCueId = cue?.AudioCueId ?? string.Empty,
            activeSetId = eventState?.ActiveSetId ?? string.Empty,
            publishedSetId = eventState?.PublishedSetId ?? string.Empty,
            activeSetStatus = activeSet?.Status ?? string.Empty,
            activeBankHash = activeSet?.BankHash ?? string.Empty,
            gameBankHash = gameBankHash,
            sourceCount = activeSet?.Sources?.Count ?? 0
        };

        if (activeSet == null)
        {
            AudioAdoptionSetV3 candidate = generationManifest?.AdoptionSets?.Find(item =>
                item != null
                && string.Equals(item.EventDefinitionId, cue?.EventDefinitionId, StringComparison.Ordinal)
                && (string.Equals(item.Status, "Saved", StringComparison.Ordinal)
                    || string.Equals(item.Status, "Applying", StringComparison.Ordinal)
                    || string.Equals(item.Status, "ApplyFailed", StringComparison.Ordinal)));
            if (candidate != null)
            {
                result.status = "CandidateOnly";
                result.label = "仅候选";
                result.detail = "候选版本尚未成功应用到 FMOD。";
                return result;
            }

            List<FmodAudioAdoptionSource> originalSources = audioEvent == null
                ? new List<FmodAudioAdoptionSource>()
                : ReadOriginalEventSources(rootPath, audioEvent.FmodEventGuid);
            result.sourceCount = originalSources.Count;
            if (originalSources.Count == 0)
            {
                result.status = "SilentPlaceholder";
                result.label = "静音占位";
                result.detail = catalogsLinked ? "游戏目录已同步，但当前 FMOD Event 没有素材。" : "当前 FMOD Event 没有素材，且游戏目录待同步。";
                return result;
            }

            result.status = catalogsLinked ? "Synced" : "FmodApplied";
            result.label = catalogsLinked ? "已同步游戏" : "已应用 FMOD";
            result.detail = catalogsLinked ? "当前原始 FMOD Event 已进入游戏目录。" : "当前原始 FMOD Event 尚未同步到游戏目录。";
            return result;
        }

        if (!string.Equals(activeSet.Status, "Applied", StringComparison.Ordinal))
        {
            result.status = "CandidateOnly";
            result.label = string.Equals(activeSet.Status, "Applying", StringComparison.Ordinal) ? "正在应用" : "仅候选";
            result.detail = "当前版本尚未成功应用到 FMOD。";
            return result;
        }

        if (string.IsNullOrWhiteSpace(activeSet.BankHash)
            || string.IsNullOrWhiteSpace(gameBankHash)
            || !string.Equals(activeSet.BankHash, gameBankHash, StringComparison.OrdinalIgnoreCase))
        {
            result.status = "BankOutOfSync";
            result.label = "Bank 未同步";
            result.detail = "当前 Applied 版本与游戏 StreamingAssets Bank 不一致。";
            return result;
        }

        if (!string.Equals(eventState.ActiveSetId, eventState.PublishedSetId, StringComparison.Ordinal)
            || !catalogsLinked)
        {
            result.status = "FmodApplied";
            result.label = "已应用 FMOD";
            result.detail = "FMOD 已采用当前版本，等待一键同步到游戏。";
            return result;
        }

        if (string.Equals(activeSet.SourceType, "CurrentEventState", StringComparison.Ordinal)
            || string.Equals(activeSet.Layout, "EmptyEvent", StringComparison.Ordinal))
        {
            result.status = "SilentPlaceholder";
            result.label = "静音占位";
            result.detail = "已同步到游戏，但当前 FMOD Event 没有素材。";
            return result;
        }

        result.status = "Synced";
        result.label = "已同步游戏";
        result.detail = "当前 Applied 版本、FMOD Bank 与游戏目录一致。";
        return result;
    }

    private static string ReadCurrentGameBankHash(string rootPath)
    {
        try
        {
            return ComputeBankSetHash(ResolveAudioBridgePath(rootPath, "Assets/StreamingAssets"));
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static AudioSemanticManifestValidationReport ValidateAudioWorkbenchManifest(
        string rootPath,
        string packageRelative,
        AudioSemanticManifestDocument manifest,
        JsonSerializerOptions jsonOptions)
    {
        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId),
            jsonOptions);
        manifest = AudioAuthoringManifestV2.Upgrade(manifest, snapshot);
        AudioSemanticManifestValidationReport validation = AudioAuthoringManifestV2.Validate(manifest, snapshot);
        AddAudioPreviewEvidenceDiagnostics(rootPath, manifest, validation, jsonOptions);
        AddAudioAdoptionDiagnostics(rootPath, manifest, validation, jsonOptions);
        return validation;
    }

    private static string ResolveAudioRequestPackage(HttpListenerContext context, string rootPath, string defaultPackage, string requested)
    {
        return string.IsNullOrWhiteSpace(requested)
            ? ResolveCharacterPackageRelative(context, rootPath, defaultPackage)
            : requested;
    }

    private static T ReadAudioRequest<T>(HttpListenerContext context, JsonSerializerOptions jsonOptions) where T : new()
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        return string.IsNullOrWhiteSpace(body) ? new T() : JsonSerializer.Deserialize<T>(body, jsonOptions) ?? new T();
    }

    private static string ResolveAudioBridgePath(string rootPath, string relativePath) => Path.GetFullPath(Path.Combine(NormalizeDirectoryRoot(rootPath), relativePath));
}
