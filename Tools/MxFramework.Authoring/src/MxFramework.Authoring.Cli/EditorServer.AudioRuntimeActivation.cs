using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static AudioRuntimeActivationState BuildAudioRuntimeActivationState(
        string rootPath,
        AudioSemanticManifestDocument semanticManifest,
        AudioSemanticManifestValidationReport semanticValidation,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        AudioGenerationManifestDocument generationManifest,
        AudioPhysicalStateResponse physicalState,
        JsonSerializerOptions jsonOptions)
    {
        semanticManifest ??= new AudioSemanticManifestDocument();
        generationManifest = UpgradeGenerationManifest(generationManifest);
        physicalState ??= new AudioPhysicalStateResponse();
        var state = new AudioRuntimeActivationState
        {
            snapshotReady = snapshot != null && !snapshot.CacheStale,
            coverageRequired = physicalState.coverage?.RequiredCellCount ?? 0,
            coverageMissing = physicalState.coverage?.MissingCellCount ?? 0,
            coverageAmbiguous = physicalState.coverage?.AmbiguousCellCount ?? 0
        };

        for (int i = 0; i < semanticManifest.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = semanticManifest.Cues[i];
            if (cue == null || !string.Equals(cue.Lifecycle, AudioCueLifecycles.Active, StringComparison.Ordinal))
                continue;
            state.activeCueCount++;
            if (string.Equals(
                    AudioAuthoringManifestV2.GetCueStatus(semanticManifest, cue, snapshot),
                    AudioSemanticEntryStatuses.Ready,
                    StringComparison.Ordinal))
                state.readyCueCount++;
        }

        PhysicalAudioAuthoringManifestDocument physicalManifest = physicalState.manifest;
        if (physicalManifest?.BasisSounds != null)
        {
            for (int i = 0; i < physicalManifest.BasisSounds.Count; i++)
            {
                PhysicalAudioBasisSoundAuthoring basis = physicalManifest.BasisSounds[i];
                if (basis == null || !string.Equals(basis.Lifecycle, PhysicalAudioLifecycles.Active, StringComparison.Ordinal))
                    continue;
                state.activeBasisCount++;
                AudioCueAuthoringDefinition cue = AudioAuthoringManifestV2.FindCue(semanticManifest, basis.AudioCueId);
                AudioAdoptionSetV3 set = generationManifest.AdoptionSets.Find(item => item != null
                    && string.Equals(item.SetId, basis.AdoptionSetId, StringComparison.Ordinal));
                bool adopted = set != null
                    && string.Equals(set.Status, "Applied", StringComparison.Ordinal)
                    && cue != null
                    && string.Equals(set.EventDefinitionId, cue.EventDefinitionId, StringComparison.Ordinal);
                if (adopted)
                    state.adoptedBasisCount++;

                AudioCueAuditionEvidence evidence = cue?.AuditionEvidence;
                if (adopted
                    && evidence != null
                    && string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal)
                    && string.Equals(evidence.AudioBasisId, basis.Id, StringComparison.Ordinal)
                    && evidence.RuntimeBasisId == basis.RuntimeBasisId
                    && string.Equals(evidence.AppliedSetId, basis.AdoptionSetId, StringComparison.Ordinal))
                    state.approvedBasisCount++;
            }
        }

        AddActivationDiagnostics(state, semanticValidation?.Diagnostics, "Audition");
        AddActivationDiagnostics(state, physicalState.validation?.Diagnostics, "Adoption");
        if (!state.snapshotReady)
        {
            state.blockers.Add(AudioRuntimeActivationBlocker.Create(
                "AUTH_AUDIO_ACTIVATION_SNAPSHOT_STALE",
                "Audition",
                string.Empty,
                "FMOD snapshot 缺失或已过期。",
                "在 Unity 刷新 FMOD snapshot，再重新执行真实 Event 试听。"));
        }
        if (state.coverageMissing > 0)
        {
            state.blockers.Add(AudioRuntimeActivationBlocker.Create(
                "AUTH_AUDIO_ACTIVATION_COVERAGE_MISSING",
                "Coverage",
                string.Empty,
                "Physical Audio Required coverage 仍有 " + state.coverageMissing + " 个空洞。",
                "打开公共材质矩阵，补齐 Required Pair × Variant。"));
        }
        if (state.coverageAmbiguous > 0)
        {
            state.blockers.Add(AudioRuntimeActivationBlocker.Create(
                "AUTH_AUDIO_ACTIVATION_COVERAGE_AMBIGUOUS",
                "Coverage",
                string.Empty,
                "Physical Audio coverage 仍有 " + state.coverageAmbiguous + " 个歧义。",
                "调整 Basis 范围、优先级或 fallback，直到解析结果唯一。"));
        }

        state.publishReady = state.snapshotReady
            && state.activeCueCount > 0
            && state.activeBasisCount > 0
            && state.adoptedBasisCount == state.activeBasisCount
            && state.coverageMissing == 0
            && state.coverageAmbiguous == 0
            && !(semanticValidation?.HasErrors ?? true)
            && !(physicalState.validation?.HasErrors ?? true);

        string generatedAudioPath = ResolveAudioBridgePath(rootPath, AudioGeneratedCatalogRelativePath);
        string mirrorAudioPath = ResolveAudioBridgePath(rootPath, AudioStreamingCatalogRelativePath);
        string generatedPhysicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalGeneratedCatalogRelativePath);
        string mirrorPhysicalPath = ResolveAudioBridgePath(rootPath, AudioPhysicalStreamingCatalogRelativePath);
        state.generatedPair = InspectAudioCatalogPair(rootPath, generatedAudioPath, generatedPhysicalPath, jsonOptions);
        state.playerPair = InspectAudioCatalogPair(rootPath, mirrorAudioPath, mirrorPhysicalPath, jsonOptions);
        state.mirrorsMatch = state.generatedPair.complete
            && state.playerPair.complete
            && string.Equals(ComputeOptionalFileHash(generatedAudioPath), ComputeOptionalFileHash(mirrorAudioPath), StringComparison.OrdinalIgnoreCase)
            && string.Equals(ComputeOptionalFileHash(generatedPhysicalPath), ComputeOptionalFileHash(mirrorPhysicalPath), StringComparison.OrdinalIgnoreCase);

        if (!state.generatedPair.complete || !state.generatedPair.linked)
            AddCatalogBlocker(state, "AUTH_AUDIO_ACTIVATION_GENERATED_PAIR_INVALID", "Generated", state.generatedPair);
        if (!state.playerPair.complete || !state.playerPair.linked)
            AddCatalogBlocker(state, "AUTH_AUDIO_ACTIVATION_PLAYER_PAIR_INVALID", "Player mirror", state.playerPair);
        if (!state.mirrorsMatch)
        {
            state.blockers.Add(AudioRuntimeActivationBlocker.Create(
                "AUTH_AUDIO_ACTIVATION_MIRROR_MISMATCH",
                "Runtime",
                string.Empty,
                "Generated 与 StreamingAssets 的 Global/Physical Catalog 未保持同一发布版本。",
                "在所有内容门禁通过后重新执行原子发布。"));
        }

        state.ready = state.generatedPair.linked
            && state.playerPair.linked
            && state.mirrorsMatch;
        return state;
    }

    private static AudioRuntimeCatalogPairState InspectAudioCatalogPair(
        string rootPath,
        string audioPath,
        string physicalPath,
        JsonSerializerOptions jsonOptions)
    {
        var result = new AudioRuntimeCatalogPairState
        {
            audioExists = File.Exists(audioPath),
            physicalExists = File.Exists(physicalPath)
        };
        result.complete = result.audioExists && result.physicalExists;
        if (!result.complete)
        {
            result.message = "catalog pair incomplete";
            return result;
        }

        result.audioHash = ComputeOptionalFileHash(audioPath);
        result.physicalHash = ComputeOptionalFileHash(physicalPath);
        try
        {
            PhysicalAudioRuntimeCatalogDocument physical = JsonSerializer.Deserialize<PhysicalAudioRuntimeCatalogDocument>(
                File.ReadAllText(physicalPath),
                jsonOptions);
            result.expectedAudioHash = physical?.AudioCatalogHash ?? string.Empty;
            result.linked = !string.IsNullOrWhiteSpace(result.expectedAudioHash)
                && string.Equals(result.expectedAudioHash, result.audioHash, StringComparison.OrdinalIgnoreCase);
            result.message = result.linked
                ? "catalog pair linked"
                : "physical audioCatalogHash does not match global catalog";
        }
        catch (Exception exception)
        {
            result.message = "catalog parse failed: " + exception.Message;
        }
        return result;
    }

    private static void AddActivationDiagnostics(
        AudioRuntimeActivationState state,
        IReadOnlyList<AuthoringResourceDiagnostic> diagnostics,
        string defaultStage)
    {
        if (diagnostics == null)
            return;
        for (int i = 0; i < diagnostics.Count; i++)
        {
            AuthoringResourceDiagnostic diagnostic = diagnostics[i];
            if (diagnostic == null || diagnostic.Severity != CharacterAuthoringValidationSeverity.Error)
                continue;
            string subject = FirstNonEmptyActivationValue(
                diagnostic.ResourceStableId,
                diagnostic.ResourceId,
                diagnostic.RuntimeResourceKey,
                diagnostic.SourceStableId);
            if (ContainsBlocker(state.blockers, diagnostic.Code, subject))
                continue;
            state.blockers.Add(AudioRuntimeActivationBlocker.Create(
                diagnostic.Code,
                ResolveActivationStage(diagnostic.Code, defaultStage),
                subject,
                diagnostic.Message,
                diagnostic.SuggestedFix));
        }
    }

    private static void AddCatalogBlocker(
        AudioRuntimeActivationState state,
        string code,
        string label,
        AudioRuntimeCatalogPairState pair)
    {
        state.blockers.Add(AudioRuntimeActivationBlocker.Create(
            code,
            "Runtime",
            string.Empty,
            label + " Catalog pair 未就绪：" + pair.message + "。",
            state.publishReady ? "执行原子发布以重建 Global/Physical Catalog pair。" : "先处理 Adoption、Audition 与 Coverage 阶段的阻断项。"));
    }

    private static string ResolveActivationStage(string code, string fallback)
    {
        if (code?.Contains("ADOPTION", StringComparison.Ordinal) == true)
            return "Adoption";
        if (code?.Contains("AUDITION", StringComparison.Ordinal) == true
            || code?.Contains("PREVIEW", StringComparison.Ordinal) == true)
            return "Audition";
        if (code?.Contains("COVERAGE", StringComparison.Ordinal) == true)
            return "Coverage";
        return fallback;
    }

    private static bool ContainsBlocker(
        IReadOnlyList<AudioRuntimeActivationBlocker> blockers,
        string code,
        string subjectId)
    {
        for (int i = 0; i < blockers.Count; i++)
        {
            if (string.Equals(blockers[i].code, code, StringComparison.Ordinal)
                && string.Equals(blockers[i].subjectId, subjectId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string FirstNonEmptyActivationValue(params string[] values)
    {
        for (int i = 0; i < values.Length; i++)
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i];
        return string.Empty;
    }

    internal sealed class AudioRuntimeActivationState
    {
        public bool ready { get; set; }
        public bool publishReady { get; set; }
        public bool snapshotReady { get; set; }
        public int activeCueCount { get; set; }
        public int readyCueCount { get; set; }
        public int activeBasisCount { get; set; }
        public int adoptedBasisCount { get; set; }
        public int approvedBasisCount { get; set; }
        public int coverageRequired { get; set; }
        public int coverageMissing { get; set; }
        public int coverageAmbiguous { get; set; }
        public bool mirrorsMatch { get; set; }
        public AudioRuntimeCatalogPairState generatedPair { get; set; } = new AudioRuntimeCatalogPairState();
        public AudioRuntimeCatalogPairState playerPair { get; set; } = new AudioRuntimeCatalogPairState();
        public List<AudioRuntimeActivationBlocker> blockers { get; set; } = new List<AudioRuntimeActivationBlocker>();
    }

    internal sealed class AudioRuntimeCatalogPairState
    {
        public bool complete { get; set; }
        public bool linked { get; set; }
        public bool audioExists { get; set; }
        public bool physicalExists { get; set; }
        public string audioHash { get; set; } = string.Empty;
        public string physicalHash { get; set; } = string.Empty;
        public string expectedAudioHash { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
    }

    internal sealed class AudioRuntimeActivationBlocker
    {
        public string code { get; set; } = string.Empty;
        public string stage { get; set; } = string.Empty;
        public string subjectId { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string action { get; set; } = string.Empty;

        public static AudioRuntimeActivationBlocker Create(
            string code,
            string stage,
            string subjectId,
            string message,
            string action) => new AudioRuntimeActivationBlocker
        {
            code = code ?? string.Empty,
            stage = stage ?? string.Empty,
            subjectId = subjectId ?? string.Empty,
            message = message ?? string.Empty,
            action = action ?? string.Empty
        };
    }
}
