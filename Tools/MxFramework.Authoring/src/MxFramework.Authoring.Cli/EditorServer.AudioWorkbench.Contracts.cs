using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal sealed class AudioWorkbenchManifestRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public AudioSemanticManifestDocument manifest { get; set; }
        public string expectedGenerationHash { get; set; } = string.Empty;
        public AudioGenerationManifestDocument generationManifest { get; set; }
        public string expectedPhysicalHash { get; set; } = string.Empty;
        public PhysicalAudioAuthoringManifestDocument physicalManifest { get; set; }
    }
    internal sealed class AudioWorkbenchPublishRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string expectedGenerationHash { get; set; } = string.Empty;
        public string expectedPhysicalHash { get; set; } = string.Empty;
        public bool developmentPublishAll { get; set; }
    }
    internal sealed class AudioWorkbenchBulkApprovalRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
    }
    internal sealed class AudioWorkbenchDirectAdoptionRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string expectedGenerationHash { get; set; } = string.Empty;
        public string expectedPhysicalHash { get; set; } = string.Empty;
    }
    internal sealed class AudioCueRuntimeSyncState
    {
        public string audioCueId { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string label { get; set; } = string.Empty;
        public string detail { get; set; } = string.Empty;
        public string activeSetId { get; set; } = string.Empty;
        public string publishedSetId { get; set; } = string.Empty;
        public string activeSetStatus { get; set; } = string.Empty;
        public string activeBankHash { get; set; } = string.Empty;
        public string gameBankHash { get; set; } = string.Empty;
        public int sourceCount { get; set; }
    }
    internal sealed class AudioWorkbenchSaveResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public AudioSemanticManifestDocument manifest { get; set; }
        public AudioSemanticManifestValidationReport validation { get; set; }
        public string generationManifestHash { get; set; } = string.Empty;
        public AudioGenerationManifestDocument generationManifest { get; set; }
        public string physicalManifestHash { get; set; } = string.Empty;
        public PhysicalAudioAuthoringManifestDocument physicalManifest { get; set; }
        public PhysicalAudioValidationReport physicalValidation { get; set; }
        public static AudioWorkbenchSaveResponse Conflict(string current, string expected) => new AudioWorkbenchSaveResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_MANIFEST_CONFLICT",
            message = "audio_semantic_manifest.json 已变化，请刷新后重试。",
            currentHash = current ?? string.Empty,
            expectedHash = expected ?? string.Empty
        };
        public static AudioWorkbenchSaveResponse GenerationConflict(string current, string expected) => new AudioWorkbenchSaveResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_GENERATION_MANIFEST_CONFLICT",
            message = "audio_generation_manifest.json 已变化，请刷新后重试。",
            generationManifestHash = current ?? string.Empty,
            expectedHash = expected ?? string.Empty
        };
        public static AudioWorkbenchSaveResponse PhysicalConflict(string current, string expected) => new AudioWorkbenchSaveResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_PHYSICAL_MANIFEST_CONFLICT",
            message = "audio_physical_manifest.json 已变化，请刷新后重试。",
            physicalManifestHash = current ?? string.Empty,
            expectedHash = expected ?? string.Empty
        };
    }
    internal sealed class AudioWorkbenchPublishResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string catalogHash { get; set; } = string.Empty;
        public string generationManifestHash { get; set; } = string.Empty;
        public int syncedSetCount { get; set; }
        public int outOfSyncSetCount { get; set; }
        public AudioRuntimeCatalogDocument catalog { get; set; }
        public AudioSemanticManifestValidationReport validation { get; set; }
        public string physicalCatalogHash { get; set; } = string.Empty;
        public PhysicalAudioRuntimeCatalogDocument physicalCatalog { get; set; }
        public PhysicalAudioValidationReport physicalValidation { get; set; }
        public static AudioWorkbenchPublishResponse Conflict(string current, string expected) => new AudioWorkbenchPublishResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_PUBLISH_CONFLICT",
            message = "发布只接受已保存且非 stale 的 manifest。当前 hash 与 expectedHash 不一致。"
        };
        public static AudioWorkbenchPublishResponse PhysicalConflict(string current, string expected) => new AudioWorkbenchPublishResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_PHYSICAL_PUBLISH_CONFLICT",
            message = "发布只接受已保存且非 stale 的 physical manifest。当前 hash 与 expectedPhysicalHash 不一致。",
            physicalCatalogHash = current ?? string.Empty
        };
        public static AudioWorkbenchPublishResponse GenerationConflict(string current, string expected) => new AudioWorkbenchPublishResponse
        {
            success = false,
            httpStatus = 409,
            code = "AUTH_AUDIO_GENERATION_PUBLISH_CONFLICT",
            message = "发布只接受已保存且非 stale 的 generation manifest。当前 hash 与 expectedGenerationHash 不一致。",
            generationManifestHash = current ?? string.Empty
        };
        public static AudioWorkbenchPublishResponse GenerationMissing() => new AudioWorkbenchPublishResponse
        {
            success = false,
            httpStatus = 400,
            code = "AUTH_AUDIO_GENERATION_MANIFEST_MISSING",
            message = "发布被阻止：缺少项目级 audio_generation_manifest.json。请先保存当前 Audio Workbench 配方与采用历史。"
        };
        public static AudioWorkbenchPublishResponse PhysicalMissing() => new AudioWorkbenchPublishResponse
        {
            success = false,
            httpStatus = 400,
            code = "AUTH_AUDIO_PHYSICAL_MANIFEST_MISSING",
            message = "发布被阻止：缺少项目级 audio_physical_manifest.json。旧 Physical Runtime Catalog 不会被视为当前权威数据。"
        };
    }
    internal sealed class AudioPreviewApiRequest
    {
        public string package { get; set; } = string.Empty;
        public string action { get; set; } = "Play";
        public string audioCueId { get; set; } = string.Empty;
        public string fmodEventPath { get; set; } = string.Empty;
        public string fmodEventGuid { get; set; } = string.Empty;
        public Dictionary<string, float> parameters { get; set; } = new Dictionary<string, float>();
        public AudioPreviewPosition position { get; set; } = new AudioPreviewPosition();
        public string eventBindingHash { get; set; } = string.Empty;
    }
    internal sealed class AudioPreviewPosition
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }
    internal sealed class AudioPreviewRequestFile
    {
        public string requestId { get; set; } = string.Empty;
        public string action { get; set; } = "Play";
        public string previewKind { get; set; } = "Event";
        public string createdUtc { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string fmodEventPath { get; set; } = string.Empty;
        public string fmodEventGuid { get; set; } = string.Empty;
        public Dictionary<string, float> parameters { get; set; } = new Dictionary<string, float>();
        public AudioPreviewPosition position { get; set; } = new AudioPreviewPosition();
        public string eventBindingHash { get; set; } = string.Empty;
        public string snapshotHash { get; set; } = string.Empty;
        public PhysicalAudioPreviewRequestFile physical { get; set; }
    }
    internal sealed class AudioPreviewStatusFile
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public string message { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string eventBindingHash { get; set; } = string.Empty;
        public string snapshotHash { get; set; } = string.Empty;
        public string previewKind { get; set; } = "Event";
        public string expectedBasisId { get; set; } = string.Empty;
        public int expectedRuntimeBasisId { get; set; }
        public int expectedRuntimeCueId { get; set; }
        public string physicalCatalogHash { get; set; } = string.Empty;
        public string resolvedBasisId { get; set; } = string.Empty;
        public int resolvedRuntimeBasisId { get; set; }
        public string resolvedAudioCueId { get; set; } = string.Empty;
        public int resolvedRuntimeCueId { get; set; }
        public bool usedFallback { get; set; }
        public float matchDistance { get; set; }
        public List<PhysicalAudioPreviewResolvedParameterFile> resolvedParameters { get; set; } = new List<PhysicalAudioPreviewResolvedParameterFile>();
        public string updatedUtc { get; set; } = string.Empty;
    }
    internal sealed class AudioPreviewApiResponse
    {
        public string requestId { get; set; } = string.Empty;
        public string status { get; set; } = "pending";
        public string message { get; set; } = string.Empty;
        public string eventBindingHash { get; set; } = string.Empty;
        public string snapshotHash { get; set; } = string.Empty;
    }
    internal sealed class AudioSnapshotRefreshRequestFile
    {
        public string requestId { get; set; } = string.Empty;
        public string createdUtc { get; set; } = string.Empty;
    }
    internal sealed class AudioPackageCueSubsetDocument
    {
        public string Format { get; set; } = "mx.characterAudioCueManifest.v2";
        public string SchemaVersion { get; set; } = "2.0";
        public string PackageId { get; set; } = string.Empty;
        public string GlobalCatalogHash { get; set; } = string.Empty;
        public List<AudioPackageCueSubsetEntry> Cues { get; set; } = new List<AudioPackageCueSubsetEntry>();
    }
    internal sealed class AudioPackageCueSubsetEntry
    {
        public string AudioCueId { get; set; } = string.Empty;
        public int RuntimeCueId { get; set; }
        public int RuntimeEventId { get; set; }
        public string EventDefinitionId { get; set; } = string.Empty;
    }
    private sealed class AudioPublicationWrite
    {
        public string path { get; set; } = string.Empty;
        public string json { get; set; } = string.Empty;
        public string tempPath { get; set; } = string.Empty;
        public string backupPath { get; set; } = string.Empty;
        public static AudioPublicationWrite Create<T>(string path, T value, JsonSerializerOptions options) => new AudioPublicationWrite
        {
            path = path,
            json = JsonSerializer.Serialize(value, options)
        };
    }
}
