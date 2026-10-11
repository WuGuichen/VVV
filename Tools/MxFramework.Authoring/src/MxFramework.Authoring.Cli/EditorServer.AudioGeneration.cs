using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static readonly HttpClient AudioGenerationClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    private const string AudioGenerationSessionsRelativePath = "Temp/MxFrameworkAuthoring/Audio/generation_sessions.json";

    private static bool TryHandleAudioGenerationApi(
        HttpListenerContext context,
        string rootPath,
        string defaultPackage,
        JsonSerializerOptions jsonOptions,
        string path)
    {
        if (TryHandleAudioApplicationApi(context, rootPath, jsonOptions, path))
            return true;
        if (path == "/api/authoring/audio/generation/health" && context.Request.HttpMethod == "GET")
        {
            AudioGenerationHttpResult result = SendAudioGeneration(HttpMethod.Get, "/health", null);
            AudioGenerationBootstrapStatus bootstrap = GetActiveAudioGenerationBootstrap();
            if (result.failure != null)
            {
                WriteJson(context.Response, new AudioGenerationHealthStatus
                {
                    status = "offline",
                    code = result.failure.code,
                    message = result.failure.message,
                    detail = result.failure.detail,
                    retryable = result.failure.retryable,
                    startupAvailable = true,
                    bootstrapStatus = bootstrap
                }, jsonOptions);
            }
            else
            {
                JsonObject health = JsonNode.Parse(result.body)?.AsObject() ?? new JsonObject();
                health["startupAvailable"] = true;
                health["bootstrapStatus"] = bootstrap == null ? null : JsonSerializer.SerializeToNode(bootstrap, jsonOptions);
                WriteJson(context.Response, health, jsonOptions);
            }
            return true;
        }

        if (path == "/api/authoring/audio/generation/host/ensure" && context.Request.HttpMethod == "POST")
        {
            WriteJson(context.Response, EnsureAudioGenerationHost(rootPath, jsonOptions), jsonOptions, 202);
            return true;
        }

        const string bootstrapPrefix = "/api/authoring/audio/generation/host/bootstrap/";
        if (path.StartsWith(bootstrapPrefix, StringComparison.Ordinal) && context.Request.HttpMethod == "GET")
        {
            WriteJson(context.Response, GetAudioGenerationBootstrap(path.Substring(bootstrapPrefix.Length)), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/generation/brief" && context.Request.HttpMethod == "POST")
        {
            AudioGenerationBriefRequest request = ReadAudioRequest<AudioGenerationBriefRequest>(context, jsonOptions);
            WriteJson(context.Response, AudioGenerationBriefBuilder.BuildRecipe(
                request.cue,
                request.audioEvent,
                AudioAuthoringManifestV2.ComputeBindingHash(request.cue, request.audioEvent)), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/generation/prompt/optimize" && context.Request.HttpMethod == "POST")
        {
            AudioPromptOptimizeRequest request = ReadAudioRequest<AudioPromptOptimizeRequest>(context, jsonOptions);
            WriteJson(context.Response, OptimizeAudioPrompt(rootPath, request, jsonOptions), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/generation/sessions" && context.Request.HttpMethod == "GET")
        {
            string sessionPath = ResolveAudioBridgePath(rootPath, AudioGenerationSessionsRelativePath);
            AudioGenerationSessionDocument document = File.Exists(sessionPath)
                ? JsonSerializer.Deserialize<AudioGenerationSessionDocument>(File.ReadAllText(sessionPath), jsonOptions) ?? new AudioGenerationSessionDocument()
                : new AudioGenerationSessionDocument();
            WriteJson(context.Response, new { document, hash = ComputeOptionalFileHash(sessionPath) }, jsonOptions);
            return true;
        }
        if (path == "/api/authoring/audio/generation/sessions" && context.Request.HttpMethod == "POST")
        {
            AudioGenerationSessionSaveRequest request = ReadAudioRequest<AudioGenerationSessionSaveRequest>(context, jsonOptions);
            string sessionPath = ResolveAudioBridgePath(rootPath, AudioGenerationSessionsRelativePath);
            string currentHash = ComputeOptionalFileHash(sessionPath);
            if (!string.Equals(currentHash, CharacterPackageHashUtility.NormalizeSha256(request.expectedHash ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context.Response, new { code = "AUTH_AUDIO_GENERATION_SESSION_CONFLICT", message = "生成会话已变化，请刷新后重试。", currentHash }, jsonOptions, 409);
                return true;
            }
            request.document ??= new AudioGenerationSessionDocument();
            request.document.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteJsonFileAtomic(sessionPath, request.document, jsonOptions);
            WriteJson(context.Response, new { success = true, hash = ComputeOptionalFileHash(sessionPath) }, jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/generation/jobs" && context.Request.HttpMethod == "GET")
        {
            string cueId = context.Request.QueryString["audioCueId"] ?? string.Empty;
            WriteAudioGenerationJson(context, SendAudioGeneration(HttpMethod.Get, "/v1/sfx/jobs?audioCueId=" + Uri.EscapeDataString(cueId), null), jsonOptions);
            return true;
        }

        if (path == "/api/authoring/audio/generation/jobs" && context.Request.HttpMethod == "POST")
        {
            AudioGenerationStartRequest request = ReadAudioRequest<AudioGenerationStartRequest>(context, jsonOptions);
            ValidateGenerationBrief(request.brief);
            var payload = new
            {
                prompt = request.brief.PositivePrompt,
                audioCueId = request.brief.AudioCueId,
                eventDefinitionId = request.brief.EventDefinitionId,
                recipeRevision = request.recipeRevision,
                recipeHash = request.recipeHash,
                negativePrompt = request.brief.NegativePrompt,
                durationSeconds = request.brief.TargetDurationSeconds,
                durationMode = request.brief.DurationMode,
                durationCharacter = request.brief.DurationCharacter,
                decayProfile = request.brief.DecayProfile,
                maxDurationSeconds = request.brief.MaxDurationSeconds > 0 ? request.brief.MaxDurationSeconds : request.brief.TargetDurationSeconds,
                seed = request.seed,
                channels = request.is3D ? 1 : 2,
                layers = request.brief.Layers.Where(item => item.Enabled).Select(item => new { name = item.Name, takes = item.Takes, prompt = string.IsNullOrWhiteSpace(item.Prompt) ? request.brief.PositivePrompt : item.Prompt, maxDurationSeconds = item.MaxDurationSeconds > 0 ? item.MaxDurationSeconds : request.brief.MaxDurationSeconds }).ToArray()
            };
            string json = JsonSerializer.Serialize(payload, jsonOptions);
            WriteAudioGenerationJson(context, SendAudioGeneration(HttpMethod.Post, "/v1/sfx/jobs", json), jsonOptions, 202);
            return true;
        }

        const string jobsPrefix = "/api/authoring/audio/generation/jobs/";
        if (path.StartsWith(jobsPrefix, StringComparison.Ordinal))
        {
            string suffix = path.Substring(jobsPrefix.Length);
            string[] parts = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && context.Request.HttpMethod == "GET")
            {
                EnsureSafeGenerationId(parts[0]);
                WriteAudioGenerationJson(context, SendAudioGeneration(HttpMethod.Get, "/v1/sfx/jobs/" + parts[0], null), jsonOptions);
                return true;
            }
            if (parts.Length == 2 && parts[1] == "cancel" && context.Request.HttpMethod == "POST")
            {
                EnsureSafeGenerationId(parts[0]);
                WriteAudioGenerationJson(context, SendAudioGeneration(HttpMethod.Post, "/v1/sfx/jobs/" + parts[0] + "/cancel", "{}"), jsonOptions, 202);
                return true;
            }
            if (parts.Length == 2 && parts[1] == "extend" && context.Request.HttpMethod == "POST")
            {
                EnsureSafeGenerationId(parts[0]);
                AudioGenerationStartRequest request = ReadAudioRequest<AudioGenerationStartRequest>(context, jsonOptions);
                ValidateGenerationBrief(request.brief);
                var payload = new
                {
                    prompt = request.brief.PositivePrompt, negativePrompt = request.brief.NegativePrompt,
                    audioCueId = request.brief.AudioCueId, eventDefinitionId = request.brief.EventDefinitionId,
                    recipeRevision = request.recipeRevision, recipeHash = request.recipeHash,
                    durationSeconds = request.brief.TargetDurationSeconds,
                    durationMode = request.brief.DurationMode, durationCharacter = request.brief.DurationCharacter,
                    decayProfile = request.brief.DecayProfile,
                    maxDurationSeconds = request.brief.MaxDurationSeconds > 0 ? request.brief.MaxDurationSeconds : request.brief.TargetDurationSeconds,
                    seed = request.seed, channels = request.is3D ? 1 : 2,
                    layers = request.brief.Layers.Where(item => item.Enabled).Select(item => new { name = item.Name, takes = item.Takes, prompt = string.IsNullOrWhiteSpace(item.Prompt) ? request.brief.PositivePrompt : item.Prompt, maxDurationSeconds = item.MaxDurationSeconds > 0 ? item.MaxDurationSeconds : request.brief.MaxDurationSeconds }).ToArray()
                };
                WriteAudioGenerationJson(context, SendAudioGeneration(HttpMethod.Post, "/v1/sfx/jobs/" + parts[0] + "/extend", JsonSerializer.Serialize(payload, jsonOptions)), jsonOptions, 202);
                return true;
            }
            if (parts.Length == 3 && parts[1] == "candidates" && context.Request.HttpMethod == "GET")
            {
                EnsureSafeGenerationId(parts[0]);
                EnsureSafeGenerationId(parts[2]);
                try
                {
                    ProxyAudioCandidate(context.Response, parts[0], parts[2]);
                }
                catch (AudioGenerationTransportException exception)
                {
                    WriteJson(context.Response, exception.Failure, jsonOptions, exception.StatusCode);
                }
                return true;
            }
            if (parts.Length == 4 && parts[1] == "candidates" && parts[3] == "repair" && context.Request.HttpMethod == "POST")
            {
                EnsureSafeGenerationId(parts[0]);
                EnsureSafeGenerationId(parts[2]);
                AudioCandidateRepairRequest request = ReadAudioRequest<AudioCandidateRepairRequest>(context, jsonOptions);
                if (!string.Equals(request.candidateId, parts[2], StringComparison.Ordinal)
                    || !IsRawAudioSha256(request.sourceHash))
                    throw new ArgumentException("Candidate repair request is invalid.");
                WriteAudioGenerationJson(
                    context,
                    SendAudioGeneration(
                        HttpMethod.Post,
                        "/v1/sfx/jobs/" + parts[0] + "/candidates/" + parts[2] + "/repair",
                        JsonSerializer.Serialize(request, jsonOptions)),
                    jsonOptions);
                return true;
            }
        }

        if ((path == "/api/authoring/audio/generation/adopt" || path == "/api/authoring/audio/generation/apply") && context.Request.HttpMethod == "POST")
        {
            AudioGenerationAdoptRequest request = ReadAudioRequest<AudioGenerationAdoptRequest>(context, jsonOptions);
            AudioGenerationAdoptResponse response = AdoptGeneratedAudio(rootPath, request, jsonOptions);
            WriteJson(context.Response, response, jsonOptions, response.httpStatus);
            return true;
        }
        return false;
    }

    private static void AddAudioAdoptionDiagnostics(
        string rootPath,
        AudioSemanticManifestDocument manifest,
        AudioSemanticManifestValidationReport validation,
        JsonSerializerOptions jsonOptions)
    {
        if (validation == null)
            return;
        AudioGenerationManifestDocument generationManifest = ReadGenerationManifest(rootPath, jsonOptions);
        foreach (AudioAdoptionEventStateV3 eventState in generationManifest.EventStates)
        {
            if (string.IsNullOrWhiteSpace(eventState.ActiveSetId)) continue;
            AudioAdoptionSetV3 activeSet = generationManifest.AdoptionSets.Find(item => string.Equals(item.SetId, eventState.ActiveSetId, StringComparison.Ordinal));
            if (activeSet == null || !string.Equals(activeSet.Status, "Applied", StringComparison.Ordinal))
            {
                validation.Diagnostics.Add(new AuthoringResourceDiagnostic { Severity = CharacterAuthoringValidationSeverity.Error, Code = "AUTH_AUDIO_SET_NOT_APPLIED", ProviderId = AuthoringResourceProviderIds.Fmod, Message = "当前音频版本尚未完整应用。", SuggestedFix = "在 Workbench 重试应用该版本。" }); validation.HasErrors = true; continue;
            }
            string buildHash;
            string mirrorHash;
            try
            {
                buildHash = ComputeBankSetHash(ResolveAudioBridgePath(rootPath, "FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop"));
                mirrorHash = ComputeBankSetHash(ResolveAudioBridgePath(rootPath, "Assets/StreamingAssets"));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException)
            {
                validation.Diagnostics.Add(new AuthoringResourceDiagnostic { Severity = CharacterAuthoringValidationSeverity.Error, Code = "AUTH_AUDIO_BANKS_UNAVAILABLE", ProviderId = AuthoringResourceProviderIds.Fmod, Message = "无法验证当前 FMOD Banks：" + SafeFailure(exception.Message), SuggestedFix = "重新应用当前音频版本并确认 Desktop Banks 构建成功。" }); validation.HasErrors = true; continue;
            }
            if (!string.Equals(activeSet.BankHash, buildHash, StringComparison.Ordinal) || !string.Equals(buildHash, mirrorHash, StringComparison.Ordinal))
            {
                validation.Diagnostics.Add(new AuthoringResourceDiagnostic { Severity = CharacterAuthoringValidationSeverity.Error, Code = "AUTH_AUDIO_BANK_HASH_STALE", ProviderId = AuthoringResourceProviderIds.Fmod, Message = "FMOD Build 与运行时 Bank mirror 不一致。", SuggestedFix = "重新应用当前音频版本。" }); validation.HasErrors = true;
            }
        }
        string requestPath = ResolveAudioBridgePath(rootPath, AudioAdoptionRequestRelativePath);
        if (File.Exists(requestPath))
        {
            validation.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Info,
                Code = "AUTH_AUDIO_ADOPTION_PENDING",
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "fmodAudioAdoptionRequest",
                SourceField = AudioAdoptionRequestRelativePath,
                Message = "FMOD audio adoption request is pending and has not been explicitly applied in FMOD Studio.",
                SuggestedFix = "在 FMOD Studio 执行 MxFramework/Apply Pending Audio Adoption，然后刷新 bank 与 snapshot。"
            });
            validation.HasErrors = true;
        }
        string statusPath = ResolveAudioBridgePath(rootPath, AudioAdoptionStatusRelativePath);
        if (!File.Exists(statusPath))
            return;
        FmodAudioAdoptionStatus status = JsonSerializer.Deserialize<FmodAudioAdoptionStatus>(File.ReadAllText(statusPath), jsonOptions);
        if (status == null || !string.Equals(status.status, "Succeeded", StringComparison.Ordinal))
        {
            validation.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Code = "AUTH_AUDIO_ADOPTION_FAILED",
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "fmodAudioAdoptionStatus",
                SourceField = AudioAdoptionStatusRelativePath,
                Message = "The latest FMOD audio adoption did not succeed: " + (status?.message ?? "missing status"),
                SuggestedFix = "解决 FMOD 脚本错误并重新执行采用。"
            });
            validation.HasErrors = true;
            return;
        }
        if (!DateTime.TryParse(status.appliedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime appliedAt) || manifest?.Cues == null)
            return;
        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = manifest.Cues[i];
            if (cue == null || !string.Equals(cue.EventDefinitionId, status.eventDefinitionId, StringComparison.Ordinal))
                continue;
            AudioCueAuditionEvidence evidence = cue.AuditionEvidence;
            bool reviewedAfterAdoption = evidence != null
                && string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal)
                && DateTime.TryParse(evidence.ReviewedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime reviewedAt)
                && reviewedAt >= appliedAt;
            AudioAdoptionEventStateV3 setState = generationManifest.EventStates.Find(item => string.Equals(item.EventDefinitionId, status.eventDefinitionId, StringComparison.Ordinal));
            AudioAdoptionSetV3 currentSet = generationManifest.AdoptionSets.Find(item => string.Equals(item.SetId, setState?.ActiveSetId, StringComparison.Ordinal));
            reviewedAfterAdoption = reviewedAfterAdoption && currentSet != null
                && string.Equals(evidence.AppliedSetId, currentSet.SetId, StringComparison.Ordinal)
                && string.Equals(evidence.BankHash, currentSet.BankHash, StringComparison.OrdinalIgnoreCase);
            if (reviewedAfterAdoption)
                continue;
            validation.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Error,
                Code = "AUTH_AUDIO_ADOPTION_REQUIRES_REVIEW",
                RuntimeResourceKey = cue.AudioCueId,
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "audioSemanticManifest",
                SourceField = "cues/" + i.ToString(CultureInfo.InvariantCulture) + "/auditionEvidence",
                Message = "FMOD source set changed after the latest optional human audition; the quality-review record is stale.",
                SuggestedFix = "若本次需要内容质量验收，可刷新 snapshot，并在 Unity/FMOD 真实试听后重新记录批准。"
            });
        }
    }

    internal static void ValidateGenerationBrief(AudioGenerationBriefV1 brief)
    {
        if (brief == null || string.IsNullOrWhiteSpace(brief.AudioCueId) || string.IsNullOrWhiteSpace(brief.EventDefinitionId))
            throw new ArgumentException("Generation brief must identify a Cue and Event Definition.");
        if (string.IsNullOrWhiteSpace(brief.PositivePrompt) || brief.PositivePrompt.Length < 8)
            throw new ArgumentException("Generation prompt is too short.");
        if (!AudioGenerationBriefBuilder.IsValidCollisionProfile(brief.CollisionProfile))
            throw new ArgumentException("Generation collision profile is invalid.");
        if (brief.Layers == null || brief.Layers.Count == 0 || !brief.Layers.Any(item => item != null && item.Enabled))
            throw new ArgumentException("Generation brief requires at least one layer.");
        int totalTakes = 0;
        for (int i = 0; i < brief.Layers.Count; i++)
        {
            AudioGenerationLayerRequest layer = brief.Layers[i];
            if (layer != null && !layer.Enabled)
                continue;
            if (layer == null || !IsSafeAudioSegment(layer.Name) || layer.Takes < 1 || layer.Takes > 12)
                throw new ArgumentException("Generation layer is invalid.");
            if (layer.GainDb < -24f || layer.GainDb > 6f || layer.DelayMs < 0 || layer.DelayMs > 250)
                throw new ArgumentException("Generation layer mix parameters are invalid.");
            int adoptionTakes = layer.AdoptionTakes > 0 ? layer.AdoptionTakes : brief.AdoptionCountPerLayer;
            if (adoptionTakes < 1 || adoptionTakes > layer.Takes)
                throw new ArgumentException("Generation layer adoption count is invalid.");
            totalTakes += layer.Takes;
        }
        if (totalTakes > 36)
            throw new ArgumentException("Generation job cannot exceed 36 candidates.");
    }


    private static AudioGenerationAdoptResponse AdoptGeneratedAudio(
        string rootPath,
        AudioGenerationAdoptRequest request,
        JsonSerializerOptions jsonOptions)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.jobId) || request.selectedCandidateIds == null || request.selectedCandidateIds.Count == 0)
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_INVALID", "至少选择一个生成候选。", string.Empty);
        if (!AudioGenerationBriefBuilder.IsValidCollisionProfile(request.collisionProfile))
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_COLLISION_PROFILE_INVALID", "接触音效物理配方无效。", string.Empty);
        EnsureSafeGenerationId(request.jobId);
        string manifestPath = ResolveAudioBridgePath(rootPath, AudioGenerationManifestRelativePath);
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request.expectedGenerationManifestHash ?? string.Empty);
        if (File.Exists(manifestPath) && !string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return AudioGenerationAdoptResponse.Error(409, "AUTH_AUDIO_GENERATION_MANIFEST_CONFLICT", "generation manifest 已变化，请刷新后重试。", currentHash);

        AudioGenerationHttpResult jobResult = SendAudioGeneration(HttpMethod.Get, "/v1/sfx/jobs/" + request.jobId, null);
        if (jobResult.failure != null)
            return AudioGenerationAdoptResponse.Error(jobResult.status, jobResult.failure.code, jobResult.failure.message, currentHash);
        AudioGenerationJob job = DeserializeGenerationJob(jobResult.body, jsonOptions);
        if (!string.Equals(job.Status, "Succeeded", StringComparison.Ordinal))
            return AudioGenerationAdoptResponse.Error(409, "AUTH_AUDIO_JOB_NOT_READY", "生成 Job 尚未成功完成。", currentHash);
        if (job.Fake)
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_FAKE_ADOPTION_BLOCKED", "假模型候选仅供部署 smoke，禁止采用。", currentHash);
        AudioSemanticManifestDocument audioManifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, ResolveAudioSemanticManifestPath(rootPath), jsonOptions);
        audioManifest = AudioAuthoringManifestV2.Upgrade(audioManifest, null);
        AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(audioManifest, request.audioCueId);
        AudioEventAuthoringDefinition audioEvent = AudioAuthoringManifestV2.FindEventDefinition(audioManifest, request.eventDefinitionId);
        if (cue == null || audioEvent == null || !string.Equals(cue.EventDefinitionId, audioEvent.Id, StringComparison.Ordinal))
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_BINDING_INVALID", "Cue 与 Event Definition 绑定不匹配。", currentHash);
        string actualBindingHash = AudioAuthoringManifestV2.ComputeBindingHash(cue, audioEvent);
        if (!string.Equals(actualBindingHash, request.expectedBindingHash, StringComparison.OrdinalIgnoreCase))
            return AudioGenerationAdoptResponse.Error(409, "AUTH_AUDIO_ADOPTION_BINDING_STALE", "Event binding 已变化，拒绝采用旧候选。", currentHash);

        var selected = new List<AudioGenerationCandidate>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < request.selectedCandidateIds.Count; i++)
        {
            string candidateId = request.selectedCandidateIds[i];
            EnsureSafeGenerationId(candidateId);
            AudioGenerationCandidate candidate = job.Candidates.Find(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal));
            if (candidate == null || !unique.Add(candidateId) || !string.Equals(candidate.Status, "Ready", StringComparison.Ordinal))
                return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_CANDIDATE_INVALID", "候选不存在、重复或未通过技术校验：" + candidateId, currentHash);
            selected.Add(candidate);
        }
        AudioGenerationBriefV1 expectedBrief = AudioGenerationBriefBuilder.Build(cue, audioEvent);
        List<AudioGenerationLayerPromptV2> requestedLayers = request.layerPrompts ?? new List<AudioGenerationLayerPromptV2>();
        bool usesLayerTargets = requestedLayers.Count > 0;
        Dictionary<string, int> selectedByLayer = selected
            .GroupBy(item => item.Layer, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        List<AudioGenerationLayerPromptV2> activeLayers = usesLayerTargets
            ? requestedLayers.Where(item => item.Enabled).ToList()
            : expectedBrief.Layers.Select(item => new AudioGenerationLayerPromptV2 { Name = item.Name, Enabled = true, Takes = item.Takes, AdoptionTakes = expectedBrief.AdoptionCountPerLayer }).ToList();
        if (activeLayers.Count == 0)
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_LAYER_INVALID", "至少启用一个 layer。", currentHash);
        for (int i = 0; i < activeLayers.Count; i++)
        {
            AudioGenerationLayerPromptV2 requestedLayer = activeLayers[i];
            string layer = requestedLayer.Name;
            int required = requestedLayer?.AdoptionTakes > 0 ? requestedLayer.AdoptionTakes : expectedBrief.AdoptionCountPerLayer;
            int generated = requestedLayer?.Takes > 0 ? requestedLayer.Takes : 1;
            if (required < 1 || required > generated || generated > 12)
                return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_COUNT_INVALID", layer + " layer 的生成或采用数量无效。", currentHash);
            if (selected.Any(item => string.Equals(item.Layer, layer, StringComparison.OrdinalIgnoreCase) && (item.Take < 1 || item.Take > generated)))
                return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_CANDIDATE_INVALID", layer + " layer 包含不属于当前 Recipe 生成范围的候选。", currentHash);
            selectedByLayer.TryGetValue(layer, out int count);
            if (count != required)
                return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_SET_INCOMPLETE", layer + " layer 必须选择 " + required.ToString(CultureInfo.InvariantCulture) + " 个候选。", currentHash);
        }
        if (selectedByLayer.Count != activeLayers.Count)
            return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_ADOPTION_LAYER_INVALID", "选择中包含当前 Cue 不需要的 layer。", currentHash);

        string eventSegment = SafeAudioProjectSegment(audioEvent.Id);
        string transaction = Guid.NewGuid().ToString("N");
        string setId = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "-" + transaction.Substring(0, 8);
        string stagingRoot = Path.Combine(ResolveAudioBridgePath(rootPath, "Temp/MxFrameworkAuthoring/Audio/Adoption"), transaction);
        var sources = new List<FmodAudioAdoptionSource>();
        var manifestEntries = new List<AudioGenerationManifestEntry>();
        var adoptedFiles = new List<AudioAdoptedFileWrite>();
        bool publicationCommitted = false;
        try
        {
            for (int i = 0; i < selected.Count; i++)
            {
                AudioGenerationCandidate candidate = selected[i];
                string layer = SafeAudioSegment(candidate.Layer);
                AudioCandidateEditV1 candidateEdit = null;
                request.candidateEdits?.TryGetValue(candidate.CandidateId, out candidateEdit);
                AudioGenerationCandidate adoptedCandidate = candidate;
                byte[] bytes;
                if (candidateEdit != null)
                {
                    if (!string.Equals(candidateEdit.JobId, request.jobId, StringComparison.Ordinal)
                        || !string.Equals(candidateEdit.CandidateId, candidate.CandidateId, StringComparison.Ordinal)
                        || !string.Equals(candidateEdit.SourceHash, candidate.Sha256, StringComparison.OrdinalIgnoreCase))
                        return AudioGenerationAdoptResponse.Error(409, "AUTH_AUDIO_CANDIDATE_EDIT_STALE", "候选剪辑已过期，请重置后再采用。", currentHash);
                    string renderJson = JsonSerializer.Serialize(new
                    {
                        candidateId = candidateEdit.CandidateId, sourceHash = candidateEdit.SourceHash,
                        inMs = candidateEdit.InMs, outMs = candidateEdit.OutMs,
                        fadeInMs = candidateEdit.FadeInMs, fadeOutMs = candidateEdit.FadeOutMs,
                        revision = candidateEdit.Revision
                    }, jsonOptions);
                    AudioGenerationHttpResult renderResult = SendAudioGeneration(HttpMethod.Post,
                        "/v1/sfx/jobs/" + request.jobId + "/candidates/" + candidate.CandidateId + "/render", renderJson);
                    if (renderResult.failure != null)
                        return AudioGenerationAdoptResponse.Error(renderResult.status, renderResult.failure.code, renderResult.failure.message, currentHash);
                    adoptedCandidate = DeserializeGenerationCandidate(renderResult.body, jsonOptions);
                    if (!string.Equals(adoptedCandidate.Status, "Ready", StringComparison.Ordinal))
                        return AudioGenerationAdoptResponse.Error(400, "AUTH_AUDIO_EDITED_CANDIDATE_INVALID", "剪辑后的候选未通过技术校验。", currentHash);
                    bytes = SendAudioGenerationBytes(adoptedCandidate.Url);
                }
                else
                    bytes = SendAudioGenerationBytes("/v1/sfx/jobs/" + request.jobId + "/candidates/" + candidate.CandidateId);
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(hash, adoptedCandidate.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("candidate hash mismatch: " + candidate.CandidateId);
                string fileName = candidate.Take.ToString("00", CultureInfo.InvariantCulture) + ".wav";
                string staged = Path.Combine(stagingRoot, eventSegment, "sets", setId, layer, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(staged));
                File.WriteAllBytes(staged, bytes);
                string relative = "Assets/Audio/MxFramework/Generated/" + eventSegment + "/sets/" + setId + "/" + layer + "/" + fileName;
                sources.Add(new FmodAudioAdoptionSource { Layer = candidate.Layer, Take = candidate.Take, ProjectRelativePath = relative, Sha256 = hash });
                manifestEntries.Add(new AudioGenerationManifestEntry
                {
                    AudioCueId = cue.AudioCueId, EventDefinitionId = audioEvent.Id, Layer = candidate.Layer, Take = candidate.Take,
                    ProjectRelativePath = relative, Status = "Adopted",
                    Provenance = new AudioGenerationProvenance
                    {
                        IntentZh = request.intentZh,
                        Prompt = request.prompt, NegativePrompt = request.negativePrompt, Seed = job.Seed + candidate.Take - 1,
                        LayerPrompts = request.layerPrompts ?? new List<AudioGenerationLayerPromptV2>(),
                        RecipeRevision = request.recipeRevision,
                        DurationMode = request.durationMode,
                        DurationCharacter = request.durationCharacter,
                        DecayProfile = request.decayProfile,
                        MaxDurationSeconds = request.maxDurationSeconds,
                        DerivationProvider = request.derivationProvider,
                        GeneratedCandidateCount = (request.layerPrompts ?? new List<AudioGenerationLayerPromptV2>()).Sum(item => item.Takes),
                        AdoptionCandidateCount = (request.layerPrompts ?? new List<AudioGenerationLayerPromptV2>()).Sum(item => item.AdoptionTakes > 0 ? item.AdoptionTakes : 0),
                        Provider = job.Provider, ModelRevision = job.Model + "@" + job.Revision,
                        GeneratedAtUtc = job.CreatedUtc, OriginalHash = candidate.RawSha256,
                        ProcessingSteps = candidateEdit == null
                            ? new List<string> { "48kHz", "24-bit PCM", audioEvent.Is3D ? "mono" : "stereo", "DC removal", string.Equals(request.durationMode, "NaturalDecay", StringComparison.Ordinal) ? "natural decay trim" : "conservative trim", "3ms fades", "-3 dBFS peak ceiling" }
                            : new List<string> { "48kHz", "24-bit PCM", audioEvent.Is3D ? "mono" : "stereo", "non-destructive candidate trim", "zero-crossing snap", "custom fades", "-3 dBFS peak ceiling", "post-edit quality gate" },
                        AdoptedBy = request.adoptedBy ?? string.Empty, FinalFileHash = hash,
                        CandidateEdit = candidateEdit,
                        CollisionProfile = AudioGenerationBriefBuilder.CloneCollisionProfile(request.collisionProfile)
                    }
                });
            }

            for (int i = 0; i < sources.Count; i++)
            {
                string staged = Path.Combine(stagingRoot, eventSegment, "sets", setId, SafeAudioSegment(sources[i].Layer), Path.GetFileName(sources[i].ProjectRelativePath));
                string destination = ResolveSafePath(rootPath, sources[i].ProjectRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                var write = new AudioAdoptedFileWrite { destination = destination, backup = destination + ".bak." + transaction, existed = File.Exists(destination) };
                if (write.existed)
                    File.Copy(destination, write.backup, overwrite: true);
                adoptedFiles.Add(write);
                File.Move(staged, destination, overwrite: true);
            }

            AudioGenerationManifestDocument generationManifest = UpgradeGenerationManifest(File.Exists(manifestPath)
                ? JsonSerializer.Deserialize<AudioGenerationManifestDocument>(File.ReadAllText(manifestPath), jsonOptions) ?? new AudioGenerationManifestDocument()
                : new AudioGenerationManifestDocument());
            for (int i = 0; i < generationManifest.Entries.Count; i++)
                if (string.Equals(generationManifest.Entries[i].EventDefinitionId, audioEvent.Id, StringComparison.Ordinal))
                    generationManifest.Entries[i].Status = "Superseded";
            generationManifest.Entries.AddRange(manifestEntries);
            generationManifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var adoption = new FmodAudioAdoptionRequestV1
            {
                RequestId = Guid.NewGuid().ToString("N"), SetId = setId, EventDefinitionId = audioEvent.Id,
                TargetEventPath = audioEvent.FmodEventPath, TargetEventGuid = audioEvent.FmodEventGuid,
                ExpectedBindingHash = actualBindingHash, Layout = request.layout ?? "MultiInstrument",
                Sources = sources, Randomization = BuildAdoptionRandomization(request.layout, sources),
                LayerMix = activeLayers.Select(item => new FmodAudioAdoptionLayerMix { Layer = item.Name, GainDb = item.GainDb, DelayMs = item.DelayMs }).ToList(),
                CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            adoption.RequestHash = ComputeObjectSha256(adoption, jsonOptions);
            var adoptionSet = new AudioAdoptionSetV3
            {
                SetId = setId, AudioCueId = cue.AudioCueId, EventDefinitionId = audioEvent.Id,
                BindingHash = actualBindingHash, SourceType = "Generated", Status = "Saved", Layout = adoption.Layout,
                Sources = sources, Randomization = adoption.Randomization, LayerMix = adoption.LayerMix,
                Provenance = manifestEntries.Select(item => item.Provenance).Where(item => item != null).ToList(),
                CreatedAtUtc = adoption.CreatedUtc
            };
            adoptionSet.SetHash = ComputeTextSha256(JsonSerializer.Serialize(adoptionSet, jsonOptions));
            generationManifest.AdoptionSets.Add(adoptionSet);
            AudioAdoptionEventStateV3 eventState = generationManifest.EventStates.Find(item => string.Equals(item.EventDefinitionId, audioEvent.Id, StringComparison.Ordinal));
            if (eventState == null)
            {
                eventState = new AudioAdoptionEventStateV3 { EventDefinitionId = audioEvent.Id };
                generationManifest.EventStates.Add(eventState);
            }
            generationManifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            WriteAudioPublicationAtomically(new List<AudioPublicationWrite>
            {
                AudioPublicationWrite.Create(manifestPath, generationManifest, jsonOptions),
                AudioPublicationWrite.Create(ResolveAudioBridgePath(rootPath, AudioAdoptionRequestRelativePath), adoption, jsonOptions)
            });
            publicationCommitted = true;
            for (int i = 0; i < adoptedFiles.Count; i++)
                if (File.Exists(adoptedFiles[i].backup))
                    File.Delete(adoptedFiles[i].backup);
            AudioApplicationStatus application = StartAudioApplication(rootPath, adoption, jsonOptions);
            return new AudioGenerationAdoptResponse { success = true, httpStatus = 202, message = "版本已保存，正在自动替换 FMOD Event。", currentHash = ComputeOptionalFileHash(manifestPath), adoptionRequest = adoption, application = application };
        }
        catch (AudioGenerationTransportException exception)
        {
            return AudioGenerationAdoptResponse.Error(exception.StatusCode, exception.Failure.code, exception.Failure.message, currentHash);
        }
        catch (Exception exception)
        {
            if (!publicationCommitted)
            {
                for (int i = adoptedFiles.Count - 1; i >= 0; i--)
                {
                    AudioAdoptedFileWrite write = adoptedFiles[i];
                    if (write.existed && File.Exists(write.backup))
                        File.Move(write.backup, write.destination, overwrite: true);
                    else if (!write.existed && File.Exists(write.destination))
                        File.Delete(write.destination);
                }
            }
            return AudioGenerationAdoptResponse.Error(500, "AUTH_AUDIO_ADOPTION_WRITE_FAILED", "采用失败：" + exception.Message, currentHash);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    private static List<FmodAudioAdoptionRandomization> BuildAdoptionRandomization(string layout, List<FmodAudioAdoptionSource> sources)
    {
        var result = new List<FmodAudioAdoptionRandomization>();
        var layers = sources.GroupBy(item => item.Layer, StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, FmodAudioAdoptionSource> group in layers)
        {
            if (group.Count() <= 1)
                continue;
            string layer = group.Key;
            float pitch = string.Equals(layer, "transient", StringComparison.OrdinalIgnoreCase) ? 0.3f
                : string.Equals(layer, "tail", StringComparison.OrdinalIgnoreCase) ? 0.2f
                : string.Equals(layout, "MultiInstrument", StringComparison.OrdinalIgnoreCase) ? 0.8f : 0.5f;
            float volume = string.Equals(layout, "MultiInstrument", StringComparison.OrdinalIgnoreCase) ? 2f : 1f;
            result.Add(new FmodAudioAdoptionRandomization { Layer = layer, PitchSemitones = pitch, VolumeDb = volume });
        }
        return result;
    }

    private sealed class AudioAdoptedFileWrite
    {
        public string destination { get; set; } = string.Empty;
        public string backup { get; set; } = string.Empty;
        public bool existed { get; set; }
    }
}
