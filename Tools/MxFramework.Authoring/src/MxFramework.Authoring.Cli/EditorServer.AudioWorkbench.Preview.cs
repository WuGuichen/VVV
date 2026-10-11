using System.Globalization;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    private static AudioPreviewApiResponse WriteAudioPreviewRequest(
        string rootPath,
        string packageRelative,
        AudioPreviewApiRequest request,
        JsonSerializerOptions jsonOptions)
    {
        request ??= new AudioPreviewApiRequest();
        string requestId = Guid.NewGuid().ToString("N");
        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        var payload = new AudioPreviewRequestFile
        {
            requestId = requestId,
            action = string.IsNullOrWhiteSpace(request.action) ? "Play" : request.action,
            createdUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            audioCueId = request.audioCueId ?? string.Empty,
            fmodEventPath = request.fmodEventPath ?? string.Empty,
            fmodEventGuid = request.fmodEventGuid ?? string.Empty,
            parameters = request.parameters ?? new Dictionary<string, float>(),
            position = request.position ?? new AudioPreviewPosition(),
            eventBindingHash = request.eventBindingHash ?? string.Empty,
            snapshotHash = AudioAuthoringManifestV2.ComputeSnapshotHash(snapshot)
        };
        var status = new AudioPreviewStatusFile
        {
            requestId = requestId,
            status = "pending",
            message = "等待 Unity Editor 执行 FMOD 试听。",
            audioCueId = payload.audioCueId,
            eventBindingHash = payload.eventBindingHash,
            snapshotHash = payload.snapshotHash,
            updatedUtc = payload.createdUtc
        };
        WriteJsonFileAtomic(GetAudioPreviewStatusPath(rootPath, requestId), status, jsonOptions);
        WriteJsonFileAtomic(ResolveAudioBridgePath(rootPath, AudioPreviewRequestRelativePath), payload, jsonOptions);
        return new AudioPreviewApiResponse
        {
            requestId = requestId,
            status = status.status,
            message = status.message,
            eventBindingHash = payload.eventBindingHash,
            snapshotHash = payload.snapshotHash
        };
    }

    private static AudioPreviewStatusFile ReadAudioPreviewStatus(string rootPath, string requestId, JsonSerializerOptions jsonOptions)
    {
        if (!IsSafeAudioRequestId(requestId))
            throw new ArgumentException("preview request id is invalid.");
        string path = GetAudioPreviewStatusPath(rootPath, requestId);
        if (!File.Exists(path))
            return new AudioPreviewStatusFile { requestId = requestId, status = "pending", message = "Unity 尚未回写试听状态。" };
        return JsonSerializer.Deserialize<AudioPreviewStatusFile>(File.ReadAllText(path), jsonOptions)
            ?? new AudioPreviewStatusFile { requestId = requestId, status = "failed", message = "试听状态文件为空。" };
    }

    private static void AddAudioPreviewEvidenceDiagnostics(
        string rootPath,
        AudioSemanticManifestDocument manifest,
        AudioSemanticManifestValidationReport validation,
        JsonSerializerOptions jsonOptions)
    {
        if (manifest?.Cues == null || validation == null)
            return;
        for (int i = 0; i < manifest.Cues.Count; i++)
        {
            AudioCueAuthoringDefinition cue = manifest.Cues[i];
            AudioCueAuditionEvidence evidence = cue?.AuditionEvidence;
            if (string.Equals(evidence?.ReviewMode, AudioCueAuditionReviewModes.BulkOperatorApproval, StringComparison.Ordinal))
                continue;
            if (evidence == null
                || (!string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal)
                    && !string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Rejected, StringComparison.Ordinal)))
                continue;
            AudioPreviewStatusFile status = null;
            if (IsSafeAudioRequestId(evidence.PreviewRequestId))
            {
                string path = GetAudioPreviewStatusPath(rootPath, evidence.PreviewRequestId);
                if (File.Exists(path))
                    status = JsonSerializer.Deserialize<AudioPreviewStatusFile>(File.ReadAllText(path), jsonOptions);
            }
            if (status != null
                && string.Equals(status.status, "succeeded", StringComparison.Ordinal)
                && string.Equals(status.audioCueId, cue.AudioCueId, StringComparison.Ordinal)
                && string.Equals(status.eventBindingHash, evidence.EventBindingHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(status.snapshotHash, evidence.SnapshotHash, StringComparison.OrdinalIgnoreCase))
                continue;

            validation.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = CharacterAuthoringValidationSeverity.Warning,
                Code = "AUTH_AUDIO_PREVIEW_EVIDENCE_INVALID",
                RuntimeResourceKey = cue.AudioCueId,
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "audioSemanticManifest",
                SourceField = "cues/" + i.ToString(CultureInfo.InvariantCulture) + "/auditionEvidence",
                Message = "Approved/Rejected evidence is not backed by a successful matching Unity/FMOD Preview request.",
                SuggestedFix = "若需要保留内容质量记录，请在 Audio Workbench 重新试听，等待 succeeded 后再批准或拒绝。"
            });
        }
    }

    private static string GetAudioPreviewStatusPath(string rootPath, string requestId)
    {
        if (!IsSafeAudioRequestId(requestId))
            throw new ArgumentException("preview request id is invalid.");
        return Path.Combine(ResolveAudioBridgePath(rootPath, AudioPreviewStatusDirectoryRelativePath), requestId + ".json");
    }
    private static bool IsSafeAudioRequestId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            return false;
        for (int i = 0; i < value.Length; i++)
            if (!char.IsLetterOrDigit(value[i]) && value[i] != '-')
                return false;
        return true;
    }
}
