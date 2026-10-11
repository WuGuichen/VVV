using System.Globalization;
using System.Net;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private const string AudioPhysicalManifestRelativePath = "Assets/Config/MxFramework/Audio/audio_physical_manifest.json";
    private const string AudioPhysicalGeneratedCatalogRelativePath = "Assets/MxFrameworkGenerated/Audio/audio_physical_runtime_catalog.json";
    private const string AudioPhysicalStreamingCatalogRelativePath = "Assets/StreamingAssets/MxFramework/Audio/audio_physical_runtime_catalog.json";
    private const string AudioPhysicalPreviewCatalogDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_preview_catalogs";
    private const string AudioPhysicalCaptureRequestRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_capture_request.json";
    private const string AudioPhysicalCaptureStatusDirectoryRelativePath = "Temp/MxFrameworkAuthoring/Audio/physical_capture_status";

    private static bool TryHandlePhysicalAudioApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (path == "/api/authoring/audio/physical/resolve" && context.Request.HttpMethod == "POST")
        {
            PhysicalAudioResolveApiRequest request = ReadAudioRequest<PhysicalAudioResolveApiRequest>(context, jsonOptions);
            PhysicalAudioAuthoringManifestDocument manifest = request.physicalManifest ?? ReadPhysicalAudioManifest(rootPath, jsonOptions);
            WriteJson(context.Response, PhysicalAudioAuthoringManifest.Resolve(manifest, request.fact), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/physical/preview" && context.Request.HttpMethod == "POST")
        {
            PhysicalAudioPreviewApiRequest request = ReadAudioRequest<PhysicalAudioPreviewApiRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            PhysicalAudioPreviewApiResponse response = WritePhysicalAudioPreviewRequest(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/physical/capture" && context.Request.HttpMethod == "POST")
        {
            PhysicalAudioCaptureApiResponse response = WritePhysicalAudioCaptureRequest(rootPath, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        if (path == "/api/authoring/audio/physical/capture-status" && context.Request.HttpMethod == "GET")
        {
            string requestId = context.Request.QueryString["id"] ?? string.Empty;
            WriteJson(context.Response, ReadPhysicalAudioCaptureStatus(rootPath, requestId, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/physical/coverage" && context.Request.HttpMethod == "POST")
        {
            PhysicalAudioManifestApiRequest request = ReadAudioRequest<PhysicalAudioManifestApiRequest>(context, jsonOptions);
            PhysicalAudioAuthoringManifestDocument manifest = request.physicalManifest ?? ReadPhysicalAudioManifest(rootPath, jsonOptions);
            WriteJson(context.Response, PhysicalAudioAuthoringManifest.AnalyzeCoverage(manifest), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/physical/promote" && context.Request.HttpMethod == "POST")
        {
            PhysicalAudioPromoteApiRequest request = ReadAudioRequest<PhysicalAudioPromoteApiRequest>(context, jsonOptions);
            string package = ResolveAudioRequestPackage(context, rootPath, defaultPackage, request.package);
            PhysicalAudioPromoteApiResponse response = PromotePhysicalAudioBasis(rootPath, package, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }

        return false;
    }

    /// <remarks>LowFreqAlloc. Compiles an immutable preview catalog and writes one Unity Editor bridge request.</remarks>
    internal static PhysicalAudioPreviewApiResponse WritePhysicalAudioPreviewRequest(
        string rootPath,
        string packageRelative,
        PhysicalAudioPreviewApiRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new PhysicalAudioPreviewApiRequest();
        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(
            rootPath,
            ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId),
            jsonOptions);
        AudioSemanticManifestDocument semantic = AudioAuthoringManifestV2.Upgrade(
            request.semanticManifest ?? ReadOptionalJsonFile<AudioSemanticManifestDocument>(
                rootPath,
                ResolveAudioSemanticManifestPath(rootPath),
                jsonOptions),
            snapshot);
        PhysicalAudioAuthoringManifestDocument physical = PhysicalAudioAuthoringManifest.Upgrade(
            request.physicalManifest ?? ReadPhysicalAudioManifest(rootPath, jsonOptions));
        PhysicalAudioResolvedPlaybackAuthoring expected = PhysicalAudioAuthoringManifest.Resolve(physical, request.fact);
        if (!expected.Success)
            return PhysicalAudioPreviewApiResponse.Invalid(expected.Code, "当前事实未解析到可试听的 Basis Sound。");

        PhysicalAudioBasisSoundAuthoring basis = PhysicalAudioAuthoringManifest.FindBasis(physical, expected.BasisId);
        AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semantic, expected.AudioCueId);
        AudioEventAuthoringDefinition audioEvent = cue == null
            ? null
            : AudioAuthoringManifestV2.FindEventDefinition(semantic, cue.EventDefinitionId);
        if (basis == null || cue == null || audioEvent == null)
            return PhysicalAudioPreviewApiResponse.Invalid(
                "AUTH_AUDIO_PHYSICAL_PREVIEW_BINDING_MISSING",
                "解析结果缺少 Basis、Cue 或 Event binding，无法交给 Unity 试听。");

        string physicalManifestJson = JsonSerializer.Serialize(physical, jsonOptions);
        string physicalManifestHash = CharacterPackageHashUtility.ComputeTextSha256(physicalManifestJson);
        string semanticManifestHash = CharacterPackageHashUtility.ComputeTextSha256(JsonSerializer.Serialize(semantic, jsonOptions));
        AudioRuntimeCatalogDocument audioCatalog = AudioAuthoringManifestV2.CompileRuntimeCatalog(
            semantic,
            semanticManifestHash,
            AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot));
        string audioCatalogHash = CharacterPackageHashUtility.ComputeTextSha256(JsonSerializer.Serialize(audioCatalog, jsonOptions));
        PhysicalAudioRuntimeCatalogDocument runtimeCatalog = PhysicalAudioAuthoringManifest.CompileRuntimeCatalog(
            physical,
            semantic,
            physicalManifestHash,
            audioCatalogHash);

        string requestId = Guid.NewGuid().ToString("N");
        string catalogRelativePath = Path.Combine(
            AudioPhysicalPreviewCatalogDirectoryRelativePath,
            requestId + ".json").Replace('\\', '/');
        string catalogJson = JsonSerializer.Serialize(runtimeCatalog, jsonOptions);
        string catalogHash = CharacterPackageHashUtility.ComputeTextSha256(catalogJson);
        WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, catalogRelativePath), runtimeCatalog, jsonOptions);

        var parameterBindings = new List<PhysicalAudioPreviewParameterBindingFile>();
        for (int i = 0; audioEvent.Parameters != null && i < audioEvent.Parameters.Count; i++)
        {
            AudioParameterAuthoringDefinition parameter = audioEvent.Parameters[i];
            if (parameter == null || parameter.RuntimeParameterId <= 0 || string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            parameterBindings.Add(new PhysicalAudioPreviewParameterBindingFile
            {
                parameterId = parameter.RuntimeParameterId,
                name = parameter.Name
            });
        }

        PhysicalAudioRuntimeFactFile runtimeFact;
        try
        {
            runtimeFact = BuildPhysicalAudioRuntimeFact(physical, request.fact, request.position);
        }
        catch (Exception exception)
        {
            return PhysicalAudioPreviewApiResponse.Invalid(
                "AUTH_AUDIO_PHYSICAL_PREVIEW_FACT_INVALID",
                "当前物理交互事实无法编译：" + exception.Message);
        }

        string createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var payload = new AudioPreviewRequestFile
        {
            requestId = requestId,
            action = "Play",
            previewKind = "Physical",
            createdUtc = createdUtc,
            audioCueId = cue.AudioCueId,
            fmodEventPath = audioEvent.FmodEventPath,
            fmodEventGuid = audioEvent.FmodEventGuid,
            position = request.position ?? new AudioPreviewPosition(),
            eventBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, audioEvent),
            snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot),
            physical = new PhysicalAudioPreviewRequestFile
            {
                runtimeCatalogPath = catalogRelativePath,
                runtimeCatalogHash = catalogHash,
                physicalManifestHash = physicalManifestHash,
                audioCatalogHash = audioCatalogHash,
                fact = runtimeFact,
                expectedBasisId = basis.Id,
                expectedRuntimeBasisId = basis.RuntimeBasisId,
                expectedAudioCueId = cue.AudioCueId,
                expectedRuntimeCueId = cue.RuntimeCueId,
                parameterBindings = parameterBindings
            }
        };
        var status = new AudioPreviewStatusFile
        {
            requestId = requestId,
            status = "pending",
            message = "等待 Unity 使用 Runtime Physical Catalog 解析并试听 FMOD Event。",
            audioCueId = cue.AudioCueId,
            eventBindingHash = payload.eventBindingHash,
            snapshotHash = payload.snapshotHash,
            previewKind = payload.previewKind,
            expectedBasisId = basis.Id,
            expectedRuntimeBasisId = basis.RuntimeBasisId,
            expectedRuntimeCueId = cue.RuntimeCueId,
            physicalCatalogHash = catalogHash,
            updatedUtc = createdUtc
        };
        WriteJsonFileAtomic(GetAudioPreviewStatusPath(rootPath, requestId), status, jsonOptions);
        WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, AudioPreviewRequestRelativePath), payload, jsonOptions);
        return new PhysicalAudioPreviewApiResponse
        {
            success = true,
            httpStatus = 202,
            requestId = requestId,
            status = status.status,
            message = status.message,
            audioCueId = cue.AudioCueId,
            expectedBasisId = basis.Id,
            expectedRuntimeBasisId = basis.RuntimeBasisId,
            expectedRuntimeCueId = cue.RuntimeCueId,
            eventBindingHash = payload.eventBindingHash,
            snapshotHash = payload.snapshotHash,
            physicalCatalogHash = catalogHash
        };
    }

    private static PhysicalAudioRuntimeFactFile BuildPhysicalAudioRuntimeFact(
        PhysicalAudioAuthoringManifestDocument manifest,
        PhysicalAudioInteractionFactAuthoring fact,
        AudioPreviewPosition position)
    {
        fact ??= new PhysicalAudioInteractionFactAuthoring();
        position ??= new AudioPreviewPosition();
        return new PhysicalAudioRuntimeFactFile
        {
            interactionKind = fact.InteractionKind ?? string.Empty,
            interactionMode = fact.InteractionMode ?? string.Empty,
            phase = fact.Phase ?? string.Empty,
            impactor = BuildPhysicalAudioRuntimeBody(manifest, fact.Impactor),
            receiver = BuildPhysicalAudioRuntimeBody(manifest, fact.Receiver),
            relativeSpeed = fact.RelativeSpeed,
            normalSpeed = fact.NormalSpeed,
            tangentialSpeed = fact.TangentialSpeed > 0f ? fact.TangentialSpeed : fact.TangentialRatio * fact.RelativeSpeed,
            pressure = fact.Pressure,
            impulse = fact.Impulse,
            angleDegrees = fact.AngleDegrees,
            intensity = fact.Intensity,
            contactNormal = BuildPhysicalAudioRuntimeDirection(fact.ContactNormal),
            position = new AudioPreviewPosition { x = position.x, y = position.y, z = position.z },
            frame = fact.Frame,
            sequence = fact.Sequence
        };
    }

    private static PhysicalAudioRuntimeDirectionFile BuildPhysicalAudioRuntimeDirection(
        PhysicalAudioDirectionAuthoring direction)
    {
        direction ??= new PhysicalAudioDirectionAuthoring();
        float lengthSquared = (direction.X * direction.X)
            + (direction.Y * direction.Y)
            + (direction.Z * direction.Z);
        if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared <= 0.000000000001f)
            return new PhysicalAudioRuntimeDirectionFile();
        float inverseLength = 1f / (float)Math.Sqrt(lengthSquared);
        return new PhysicalAudioRuntimeDirectionFile
        {
            x = direction.X * inverseLength,
            y = direction.Y * inverseLength,
            z = direction.Z * inverseLength
        };
    }

    private static PhysicalAudioRuntimeBodyFile BuildPhysicalAudioRuntimeBody(
        PhysicalAudioAuthoringManifestDocument manifest,
        PhysicalAudioBodyFactAuthoring body)
    {
        body ??= new PhysicalAudioBodyFactAuthoring();
        PhysicalAudioMaterialProfileAuthoring material = manifest.MaterialProfiles.Find(item => item != null
            && string.Equals(item.Id, body.MaterialId, StringComparison.Ordinal));
        if (material == null)
            throw new InvalidDataException("未知声学材质：" + (body.MaterialId ?? string.Empty));
        return new PhysicalAudioRuntimeBodyFile
        {
            runtimeMaterialId = material.RuntimeMaterialId,
            mass = body.Mass,
            contactArea = body.ContactArea,
            surface = body.Surface ?? string.Empty,
            construction = body.Construction ?? string.Empty
        };
    }

    /// <remarks>LowFreqAlloc. Creates a shelf request for Unity's latest real resolved physical-audio interaction.</remarks>
    internal static PhysicalAudioCaptureApiResponse WritePhysicalAudioCaptureRequest(
        string rootPath,
        JsonSerializerOptions jsonOptions)
    {
        string requestId = Guid.NewGuid().ToString("N");
        string createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        WriteJsonFileAtomic(
            ResolveAudioBridgePath(rootPath, AudioPhysicalCaptureRequestRelativePath),
            new PhysicalAudioCaptureRequestFile { requestId = requestId, createdUtc = createdUtc },
            jsonOptions);
        WriteJsonFileAtomic(
            GetPhysicalAudioCaptureStatusPath(rootPath, requestId),
            new PhysicalAudioCaptureStatusFile
            {
                requestId = requestId,
                status = "pending",
                code = "AUDIO_PHYSICAL_CAPTURE_PENDING",
                message = "等待 Unity 查询可用的 Physical Audio fact source。",
                updatedUtc = createdUtc
            },
            jsonOptions);
        return new PhysicalAudioCaptureApiResponse
        {
            success = true,
            httpStatus = 202,
            requestId = requestId,
            status = "pending",
            message = "已请求 Unity 捕获最近一次物理音频事实。"
        };
    }

    private static PhysicalAudioCaptureStatusFile ReadPhysicalAudioCaptureStatus(
        string rootPath,
        string requestId,
        JsonSerializerOptions jsonOptions)
    {
        if (!IsSafeAudioRequestId(requestId))
            throw new ArgumentException("physical audio capture request id is invalid.");
        string path = GetPhysicalAudioCaptureStatusPath(rootPath, requestId);
        if (!File.Exists(path))
            return new PhysicalAudioCaptureStatusFile
            {
                requestId = requestId,
                status = "pending",
                code = "AUDIO_PHYSICAL_CAPTURE_PENDING",
                message = "Unity 尚未回写事实捕获状态。"
            };
        PhysicalAudioCaptureStatusFile status =
            JsonSerializer.Deserialize<PhysicalAudioCaptureStatusFile>(File.ReadAllText(path), jsonOptions)
            ?? new PhysicalAudioCaptureStatusFile
            {
                requestId = requestId,
                status = "failed",
                code = "AUDIO_PHYSICAL_CAPTURE_STATUS_INVALID",
                message = "事实捕获状态文件为空。"
            };
        AddPhysicalAudioCapturedParameterNames(rootPath, status, jsonOptions);
        return status;
    }

    /// <remarks>LowFreqAlloc. Enriches Unity's numeric runtime capture for operator-facing diagnostics.</remarks>
    private static void AddPhysicalAudioCapturedParameterNames(
        string rootPath,
        PhysicalAudioCaptureStatusFile status,
        JsonSerializerOptions jsonOptions)
    {
        if (status?.parameters == null || status.parameters.Count == 0)
            return;

        AudioSemanticManifestDocument semantic = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(
                rootPath,
                ResolveAudioSemanticManifestPath(rootPath),
                jsonOptions),
            null);
        List<AudioEventAuthoringDefinition> events = semantic?.EventDefinitions;
        if (events == null)
            return;

        for (int capturedIndex = 0; capturedIndex < status.parameters.Count; capturedIndex++)
        {
            PhysicalAudioCapturedParameterFile captured = status.parameters[capturedIndex];
            if (captured == null || captured.parameterId <= 0 || !string.IsNullOrWhiteSpace(captured.name))
                continue;

            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                List<AudioParameterAuthoringDefinition> parameters = events[eventIndex]?.Parameters;
                for (int parameterIndex = 0; parameters != null && parameterIndex < parameters.Count; parameterIndex++)
                {
                    AudioParameterAuthoringDefinition parameter = parameters[parameterIndex];
                    if (parameter == null || parameter.RuntimeParameterId != captured.parameterId)
                        continue;
                    captured.name = parameter.Name ?? string.Empty;
                    break;
                }
                if (!string.IsNullOrWhiteSpace(captured.name))
                    break;
            }
        }
    }

    private static string GetPhysicalAudioCaptureStatusPath(string rootPath, string requestId)
    {
        if (!IsSafeAudioRequestId(requestId))
            throw new ArgumentException("physical audio capture request id is invalid.");
        return Path.Combine(
            ResolveAudioBridgePath(rootPath, AudioPhysicalCaptureStatusDirectoryRelativePath),
            requestId + ".json");
    }

    internal static AudioPhysicalStateResponse ReadAudioPhysicalState(
        string rootPath,
        AudioSemanticManifestDocument semanticManifest,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        PhysicalAudioAuthoringManifestDocument manifest = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        PhysicalAudioValidationReport validation = PhysicalAudioAuthoringManifest.Validate(manifest, semanticManifest);
        AddPhysicalAudioAdoptionDiagnostics(
            manifest,
            semanticManifest,
            ReadGenerationManifest(rootPath, jsonOptions),
            validation);
        string manifestHash = ComputeOptionalFileHash(manifestPath);
        AddPhysicalAudioAuditionDiagnostics(manifest, semanticManifest, validation);
        PhysicalAudioAuthoringManifest.ValidateSnapshotParameterEvidence(
            manifest,
            semanticManifest,
            snapshot,
            validation);
        string generatedPath = ResolveAudioBridgePath(rootPath, AudioPhysicalGeneratedCatalogRelativePath);
        string streamingPath = ResolveAudioBridgePath(rootPath, AudioPhysicalStreamingCatalogRelativePath);
        return new AudioPhysicalStateResponse
        {
            manifestPath = AudioPhysicalManifestRelativePath,
            manifestHash = manifestHash,
            manifest = manifest,
            validation = validation,
            coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(manifest),
            generatedCatalog = new AudioCatalogFileState { path = AudioPhysicalGeneratedCatalogRelativePath, exists = File.Exists(generatedPath), hash = ComputeOptionalFileHash(generatedPath) },
            playerMirror = new AudioCatalogFileState { path = AudioPhysicalStreamingCatalogRelativePath, exists = File.Exists(streamingPath), hash = ComputeOptionalFileHash(streamingPath) }
        };
    }

    private static PhysicalAudioAuthoringManifestDocument ReadPhysicalAudioManifest(string rootPath, JsonSerializerOptions jsonOptions)
    {
        string path = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        return PhysicalAudioAuthoringManifest.Upgrade(
            ReadOptionalJsonFile<PhysicalAudioAuthoringManifestDocument>(rootPath, path, jsonOptions));
    }

    internal static void AddPhysicalAudioAdoptionDiagnostics(
        PhysicalAudioAuthoringManifestDocument manifest,
        AudioSemanticManifestDocument semanticManifest,
        AudioGenerationManifestDocument generationManifest,
        PhysicalAudioValidationReport validation)
    {
        if (manifest?.BasisSounds == null || validation == null)
            return;
        for (int i = 0; i < manifest.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
            if (basis == null || !string.Equals(basis.Lifecycle, PhysicalAudioLifecycles.Active, StringComparison.Ordinal))
                continue;
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, basis.AudioCueId);
            AudioAdoptionSetV3 set = generationManifest?.AdoptionSets?.Find(item => item != null
                && string.Equals(item.SetId, basis.AdoptionSetId, StringComparison.Ordinal));
            if (set == null || !string.Equals(set.Status, "Applied", StringComparison.Ordinal)
                || cue == null || !string.Equals(set.EventDefinitionId, cue.EventDefinitionId, StringComparison.Ordinal))
            {
                validation.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Error,
                    Code = "AUTH_AUDIO_PHYSICAL_ADOPTION_NOT_APPLIED",
                    ResourceId = basis.Id,
                    ResourceStableId = basis.Id,
                    SourceConfigKind = PhysicalAudioAuthoringFormats.Authoring,
                    Message = "Active Basis 必须绑定该 Cue 当前已 Applied 的 adoption set。",
                    SuggestedFix = "在候选与采用页应用音效版本，再将该版本提升或更新到 Basis Sound。"
                });
            }
        }
        validation.HasErrors = validation.Diagnostics.Exists(item => item != null
            && item.Severity == CharacterAuthoringValidationSeverity.Error);
    }

    internal static void AddPhysicalAudioAuditionDiagnostics(
        PhysicalAudioAuthoringManifestDocument manifest,
        AudioSemanticManifestDocument semanticManifest,
        PhysicalAudioValidationReport validation)
    {
        if (manifest?.BasisSounds == null || validation == null)
            return;
        for (int i = 0; i < manifest.BasisSounds.Count; i++)
        {
            PhysicalAudioBasisSoundAuthoring basis = manifest.BasisSounds[i];
            if (basis == null || !string.Equals(basis.Lifecycle, PhysicalAudioLifecycles.Active, StringComparison.Ordinal))
                continue;
            AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, basis.AudioCueId);
            AudioCueAuditionEvidence evidence = cue?.AuditionEvidence;
            bool current = evidence != null
                && string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal)
                && string.Equals(evidence.AudioBasisId, basis.Id, StringComparison.Ordinal)
                && evidence.RuntimeBasisId == basis.RuntimeBasisId
                && string.Equals(evidence.AppliedSetId, basis.AdoptionSetId, StringComparison.Ordinal);
            if (current)
                continue;
            validation.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Info,
                Code = "AUTH_AUDIO_PHYSICAL_AUDITION_EVIDENCE_STALE",
                RuntimeResourceKey = basis.AudioCueId,
                ResourceId = basis.Id,
                ResourceStableId = basis.Id,
                SourceConfigKind = PhysicalAudioAuthoringFormats.Authoring,
                SourceField = "basisSounds/" + i.ToString(CultureInfo.InvariantCulture) + "/auditionEvidence",
                Message = "当前可选试听记录未同时绑定 Basis、runtime Basis ID 与 adoption set。",
                SuggestedFix = "若本次需要内容质量验收，可使用 Unity/FMOD 试听当前 Basis 素材版本并重新记录批准。"
            });
        }
        validation.HasErrors = validation.Diagnostics.Exists(item => item != null
            && item.Severity == CharacterAuthoringValidationSeverity.Error);
    }

    internal static PhysicalAudioPromoteApiResponse PromotePhysicalAudioBasis(
        string rootPath,
        string packageRelative,
        PhysicalAudioPromoteApiRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new PhysicalAudioPromoteApiRequest();
        string semanticPath = ResolveAudioSemanticManifestPath(rootPath);
        string generationPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        string physicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalManifestRelativePath);
        string currentSemanticHash = ComputeOptionalFileHash(semanticPath);
        string currentGenerationHash = ComputeOptionalFileHash(generationPath);
        string currentPhysicalHash = ComputeOptionalFileHash(physicalPath);
        string expectedSemanticHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedSemanticHash);
        string expectedGenerationHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedGenerationHash);
        string expectedPhysicalHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedPhysicalHash);
        if (!File.Exists(semanticPath)
            || !string.Equals(currentSemanticHash, expectedSemanticHash, StringComparison.OrdinalIgnoreCase))
            return PhysicalAudioPromoteApiResponse.Conflict(
                "AUTH_AUDIO_SEMANTIC_MANIFEST_CONFLICT",
                "audio_semantic_manifest.json 已变化，请刷新后重试。",
                currentSemanticHash,
                expectedSemanticHash);
        if (!File.Exists(generationPath)
            || !string.Equals(currentGenerationHash, expectedGenerationHash, StringComparison.OrdinalIgnoreCase))
            return PhysicalAudioPromoteApiResponse.Conflict(
                "AUTH_AUDIO_GENERATION_MANIFEST_CONFLICT",
                "audio_generation_manifest.json 已变化，请刷新后重试。",
                currentGenerationHash,
                expectedGenerationHash);
        if (!File.Exists(physicalPath)
            || !string.Equals(currentPhysicalHash, expectedPhysicalHash, StringComparison.OrdinalIgnoreCase))
            return PhysicalAudioPromoteApiResponse.Conflict(
                "AUTH_AUDIO_PHYSICAL_MANIFEST_CONFLICT",
                "audio_physical_manifest.json 已变化，请刷新后重试。",
                currentPhysicalHash,
                expectedPhysicalHash);

        AudioSemanticManifestDocument semantic = AudioAuthoringManifestV2.Upgrade(
            ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, ResolveAudioSemanticManifestPath(rootPath), jsonOptions), null);
        AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semantic, request.audioCueId);
        if (cue == null)
            return PhysicalAudioPromoteApiResponse.Invalid("AUTH_AUDIO_PHYSICAL_CUE_MISSING", "要提升的 Audio Cue 不存在。", currentPhysicalHash);

        AudioGenerationManifestDocument generation = ReadGenerationManifest(rootPath, jsonOptions);
        AudioAdoptionSetV3 adoptionSet = generation.AdoptionSets.Find(item => item != null
            && string.Equals(item.SetId, request.adoptionSetId, StringComparison.Ordinal));
        if (adoptionSet == null || !string.Equals(adoptionSet.EventDefinitionId, cue.EventDefinitionId, StringComparison.Ordinal))
            return PhysicalAudioPromoteApiResponse.Invalid("AUTH_AUDIO_PHYSICAL_ADOPTION_SET_INVALID", "采用版本不存在或不属于该 Cue 的 Event。", currentPhysicalHash);
        if (!string.Equals(adoptionSet.Status, "Applied", StringComparison.Ordinal))
            return PhysicalAudioPromoteApiResponse.Invalid("AUTH_AUDIO_PHYSICAL_ADOPTION_SET_NOT_APPLIED", "只有已完整应用到 FMOD/Bank/Snapshot 的采用版本才能提升为基础音效。", currentPhysicalHash);
        AudioAdoptionEventStateV3 eventState = generation.EventStates.Find(item => item != null
            && string.Equals(item.EventDefinitionId, cue.EventDefinitionId, StringComparison.Ordinal));
        if (eventState == null || !string.Equals(eventState.ActiveSetId, adoptionSet.SetId, StringComparison.Ordinal))
            return PhysicalAudioPromoteApiResponse.Invalid("AUTH_AUDIO_PHYSICAL_ADOPTION_SET_NOT_ACTIVE", "采用版本不是该 Event 当前 activeSetId，不能提升为基础音效。", currentPhysicalHash);

        PhysicalAudioAuthoringManifestDocument manifest = ReadPhysicalAudioManifest(rootPath, jsonOptions);
        PhysicalAudioBasisSoundAuthoring promoted = PhysicalAudioAuthoringManifest.PromoteBasis(
            manifest,
            cue.AudioCueId,
            adoptionSet.SetId,
            request.familyId,
            request.collisionProfile);
        PhysicalAudioBasisSoundAuthoring existing = manifest.BasisSounds.Find(item => item != null
            && string.Equals(item.AudioCueId, cue.AudioCueId, StringComparison.Ordinal));
        if (existing == null)
        {
            EnsureUniqueBasisId(manifest, promoted);
            manifest.BasisSounds.Add(promoted);
        }
        else
        {
            existing.DisplayName = string.IsNullOrWhiteSpace(request.displayName) ? cue.DisplayName : request.displayName.Trim();
            existing.FamilyId = promoted.FamilyId;
            existing.MaterialPairId = promoted.MaterialPairId;
            existing.VariantKind = promoted.VariantKind;
            existing.AdoptionSetId = promoted.AdoptionSetId;
            if (request.collisionProfile != null)
            {
                existing.Impactor = promoted.Impactor;
                existing.Receiver = promoted.Receiver;
                existing.SideA = promoted.SideA;
                existing.SideB = promoted.SideB;
                existing.RelativeSpeed = promoted.RelativeSpeed;
                existing.Intensity = promoted.Intensity;
                existing.AngleDegrees = promoted.AngleDegrees;
                existing.SourceCollisionProfile = promoted.SourceCollisionProfile;
            }
            promoted = existing;
        }
        manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        PhysicalAudioValidationReport validation = PhysicalAudioAuthoringManifest.Validate(manifest, semantic);
        WriteJsonFileAtomic(physicalPath, manifest, jsonOptions);
        return new PhysicalAudioPromoteApiResponse
        {
            success = true,
            httpStatus = 200,
            message = "已提升为基础音效草稿；发布前仍需配置覆盖范围并完成真实试听。",
            currentHash = ComputeOptionalFileHash(physicalPath),
            manifest = manifest,
            basis = promoted,
            validation = validation,
            coverage = PhysicalAudioAuthoringManifest.AnalyzeCoverage(manifest)
        };
    }

    private static void EnsureUniqueBasisId(PhysicalAudioAuthoringManifestDocument manifest, PhysicalAudioBasisSoundAuthoring basis)
    {
        if (PhysicalAudioAuthoringManifest.FindBasis(manifest, basis.Id) == null)
            return;
        string prefix = basis.Id;
        int suffix = 2;
        while (PhysicalAudioAuthoringManifest.FindBasis(manifest, prefix + "." + suffix) != null)
            suffix++;
        basis.Id = prefix + "." + suffix;
    }

    internal sealed class PhysicalAudioManifestApiRequest
    {
        public PhysicalAudioAuthoringManifestDocument physicalManifest { get; set; }
    }

    internal sealed class PhysicalAudioResolveApiRequest
    {
        public PhysicalAudioAuthoringManifestDocument physicalManifest { get; set; }
        public PhysicalAudioInteractionFactAuthoring fact { get; set; } = new PhysicalAudioInteractionFactAuthoring();
    }

    internal sealed class PhysicalAudioPreviewApiRequest
    {
        public string package { get; set; } = string.Empty;
        public AudioSemanticManifestDocument semanticManifest { get; set; }
        public PhysicalAudioAuthoringManifestDocument physicalManifest { get; set; }
        public PhysicalAudioInteractionFactAuthoring fact { get; set; } = new PhysicalAudioInteractionFactAuthoring();
        public AudioPreviewPosition position { get; set; } = new AudioPreviewPosition();
    }

    internal sealed class PhysicalAudioPreviewApiResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string expectedBasisId { get; set; } = string.Empty;
        public int expectedRuntimeBasisId { get; set; }
        public int expectedRuntimeCueId { get; set; }
        public string eventBindingHash { get; set; } = string.Empty;
        public string snapshotHash { get; set; } = string.Empty;
        public string physicalCatalogHash { get; set; } = string.Empty;

        public static PhysicalAudioPreviewApiResponse Invalid(string code, string message) => new PhysicalAudioPreviewApiResponse
        {
            success = false,
            httpStatus = 400,
            code = code ?? string.Empty,
            status = "failed",
            message = message ?? string.Empty
        };
    }

    internal sealed class PhysicalAudioPreviewRequestFile
    {
        public string runtimeCatalogPath { get; set; } = string.Empty;
        public string runtimeCatalogHash { get; set; } = string.Empty;
        public string physicalManifestHash { get; set; } = string.Empty;
        public string audioCatalogHash { get; set; } = string.Empty;
        public PhysicalAudioRuntimeFactFile fact { get; set; } = new PhysicalAudioRuntimeFactFile();
        public string expectedBasisId { get; set; } = string.Empty;
        public int expectedRuntimeBasisId { get; set; }
        public string expectedAudioCueId { get; set; } = string.Empty;
        public int expectedRuntimeCueId { get; set; }
        public List<PhysicalAudioPreviewParameterBindingFile> parameterBindings { get; set; } = new List<PhysicalAudioPreviewParameterBindingFile>();
    }

    internal sealed class PhysicalAudioPreviewParameterBindingFile
    {
        public int parameterId { get; set; }
        public string name { get; set; } = string.Empty;
    }

    internal sealed class PhysicalAudioPreviewResolvedParameterFile
    {
        public int parameterId { get; set; }
        public string name { get; set; } = string.Empty;
        public float value { get; set; }
    }

    internal sealed class PhysicalAudioRuntimeFactFile
    {
        public string interactionKind { get; set; } = string.Empty;
        public string interactionMode { get; set; } = string.Empty;
        public string phase { get; set; } = string.Empty;
        public string dynamicsSource { get; set; } = string.Empty;
        public PhysicalAudioRuntimeBodyFile impactor { get; set; } = new PhysicalAudioRuntimeBodyFile();
        public PhysicalAudioRuntimeBodyFile receiver { get; set; } = new PhysicalAudioRuntimeBodyFile();
        public float relativeSpeed { get; set; }
        public float normalSpeed { get; set; }
        public float tangentialSpeed { get; set; }
        public float pressure { get; set; }
        public float impulse { get; set; }
        public float angleDegrees { get; set; }
        public float intensity { get; set; }
        public float rawNormalImpulseNewtonSeconds { get; set; }
        public float rawAveragePressurePascals { get; set; }
        public PhysicalAudioRuntimeDirectionFile contactNormal { get; set; } = new PhysicalAudioRuntimeDirectionFile();
        public AudioPreviewPosition position { get; set; } = new AudioPreviewPosition();
        public int emitterId { get; set; }
        public long frame { get; set; }
        public long sequence { get; set; }
    }

    internal sealed class PhysicalAudioRuntimeDirectionFile
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    internal sealed class PhysicalAudioRuntimeBodyFile
    {
        public int runtimeMaterialId { get; set; }
        public float mass { get; set; }
        public float contactArea { get; set; }
        public string surface { get; set; } = string.Empty;
        public string construction { get; set; } = string.Empty;
    }

    internal sealed class PhysicalAudioCaptureRequestFile
    {
        public string requestId { get; set; } = string.Empty;
        public string createdUtc { get; set; } = string.Empty;
    }

    internal sealed class PhysicalAudioCaptureStatusFile
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public PhysicalAudioRuntimeFactFile fact { get; set; }
        public int runtimeFamilyId { get; set; }
        public int runtimeBasisId { get; set; }
        public int runtimeCueId { get; set; }
        public bool usedFallback { get; set; }
        public float matchDistance { get; set; }
        public int captureRevision { get; set; }
        public List<PhysicalAudioCapturedParameterFile> parameters { get; set; } = new List<PhysicalAudioCapturedParameterFile>();
        public string updatedUtc { get; set; } = string.Empty;
    }

    internal sealed class PhysicalAudioCapturedParameterFile
    {
        public int parameterId { get; set; }
        public string name { get; set; } = string.Empty;
        public float value { get; set; }
    }

    internal sealed class PhysicalAudioCaptureApiResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class PhysicalAudioPromoteApiRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedSemanticHash { get; set; } = string.Empty;
        public string expectedGenerationHash { get; set; } = string.Empty;
        public string expectedPhysicalHash { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string adoptionSetId { get; set; } = string.Empty;
        public string familyId { get; set; } = string.Empty;
        public string displayName { get; set; } = string.Empty;
        public AudioCollisionProfileV1 collisionProfile { get; set; }
    }

    internal sealed class PhysicalAudioPromoteApiResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public PhysicalAudioAuthoringManifestDocument manifest { get; set; }
        public PhysicalAudioBasisSoundAuthoring basis { get; set; }
        public PhysicalAudioValidationReport validation { get; set; }
        public PhysicalAudioCoverageReport coverage { get; set; }

        public static PhysicalAudioPromoteApiResponse Conflict(string code, string message, string current, string expected) => new PhysicalAudioPromoteApiResponse
        {
            success = false,
            httpStatus = 409,
            code = code ?? string.Empty,
            message = message ?? string.Empty,
            currentHash = current ?? string.Empty,
            expectedHash = expected ?? string.Empty
        };

        public static PhysicalAudioPromoteApiResponse Invalid(string code, string message, string current) => new PhysicalAudioPromoteApiResponse
        {
            success = false,
            httpStatus = 400,
            code = code ?? string.Empty,
            message = message ?? string.Empty,
            currentHash = current ?? string.Empty
        };
    }

    internal sealed class AudioPhysicalStateResponse
    {
        public string manifestPath { get; set; } = string.Empty;
        public string manifestHash { get; set; } = string.Empty;
        public PhysicalAudioAuthoringManifestDocument manifest { get; set; }
        public PhysicalAudioValidationReport validation { get; set; }
        public PhysicalAudioCoverageReport coverage { get; set; }
        public AudioCatalogFileState generatedCatalog { get; set; }
        public AudioCatalogFileState playerMirror { get; set; }
    }

    internal sealed class AudioCatalogFileState
    {
        public string path { get; set; } = string.Empty;
        public bool exists { get; set; }
        public string hash { get; set; } = string.Empty;
    }
}
