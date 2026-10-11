using MxFramework.Authoring;
namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal sealed class AudioGenerationBriefRequest
    {
        public AudioCueAuthoringDefinition cue { get; set; }
        public AudioEventAuthoringDefinition audioEvent { get; set; }
    }
    internal sealed class AudioGenerationStartRequest
    {
        public AudioGenerationBriefV1 brief { get; set; }
        public int seed { get; set; }
        public bool is3D { get; set; } = true;
        public int recipeRevision { get; set; }
        public string recipeHash { get; set; } = string.Empty;
    }

    internal sealed class AudioGenerationSessionDocument
    {
        public string Format { get; set; } = "mx.audioGenerationSessions.v1";
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public List<AudioGenerationSessionV1> Sessions { get; set; } = new List<AudioGenerationSessionV1>();
    }
    internal sealed class AudioGenerationSessionV1
    {
        public string AudioCueId { get; set; } = string.Empty;
        public string ActiveJobId { get; set; } = string.Empty;
        public string RecipeHash { get; set; } = string.Empty;
        public Dictionary<string, string> Reviews { get; set; } = new Dictionary<string, string>();
        public List<string> AdoptionCandidateIds { get; set; } = new List<string>();
        public Dictionary<string, string> MixA { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> MixB { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, AudioCandidateEditV1> CandidateEdits { get; set; } = new Dictionary<string, AudioCandidateEditV1>();
        public string ActiveMix { get; set; } = "A";
        public string UpdatedAtUtc { get; set; } = string.Empty;
    }
    internal sealed class AudioGenerationSessionSaveRequest
    {
        public AudioGenerationSessionDocument document { get; set; }
        public string expectedHash { get; set; } = string.Empty;
    }
    internal sealed class AudioCandidateRepairRequest
    {
        public string candidateId { get; set; } = string.Empty;
        public string sourceHash { get; set; } = string.Empty;
    }
    internal sealed class AudioGenerationAdoptRequest
    {
        public string jobId { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string eventDefinitionId { get; set; } = string.Empty;
        public string expectedBindingHash { get; set; } = string.Empty;
        public string expectedGenerationManifestHash { get; set; } = string.Empty;
        public List<string> selectedCandidateIds { get; set; } = new List<string>();
        public Dictionary<string, AudioCandidateEditV1> candidateEdits { get; set; } = new Dictionary<string, AudioCandidateEditV1>();
        public string layout { get; set; } = "MultiInstrument";
        public string prompt { get; set; } = string.Empty;
        public string negativePrompt { get; set; } = string.Empty;
        public string intentZh { get; set; } = string.Empty;
        public List<AudioGenerationLayerPromptV2> layerPrompts { get; set; } = new List<AudioGenerationLayerPromptV2>();
        public AudioCollisionProfileV1 collisionProfile { get; set; }
        public int recipeRevision { get; set; }
        public string durationMode { get; set; } = "LegacyFixed";
        public string durationCharacter { get; set; } = "Auto";
        public string decayProfile { get; set; } = "Natural";
        public float maxDurationSeconds { get; set; }
        public string derivationProvider { get; set; } = string.Empty;
        public string adoptedBy { get; set; } = string.Empty;
    }
    internal sealed class AudioGenerationAdoptResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public FmodAudioAdoptionRequestV1 adoptionRequest { get; set; }
        public AudioApplicationStatus application { get; set; }
        public static AudioGenerationAdoptResponse Error(int status, string errorCode, string errorMessage, string hash) => new AudioGenerationAdoptResponse
        {
            success = false, httpStatus = status, code = errorCode, message = errorMessage, currentHash = hash
        };
    }
    internal sealed class AudioGenerationManifestDocument
    {
        public string Format { get; set; } = "mx.audioGenerationManifest.v3";
        public string SchemaVersion { get; set; } = "3.0";
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public List<AudioGenerationRecipeV2> Recipes { get; set; } = new List<AudioGenerationRecipeV2>();
        public List<AudioGenerationManifestEntry> Entries { get; set; } = new List<AudioGenerationManifestEntry>();
        public List<AudioAdoptionSetV3> AdoptionSets { get; set; } = new List<AudioAdoptionSetV3>();
        public List<AudioAdoptionEventStateV3> EventStates { get; set; } = new List<AudioAdoptionEventStateV3>();
    }

}
