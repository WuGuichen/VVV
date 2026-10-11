using System.Globalization;
using System.Text.Json;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static object ReadAudioSemanticManifestState(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        AudioSemanticManifestDocument manifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions) ?? new AudioSemanticManifestDocument();
        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        manifest = AudioAuthoringManifestV2.Upgrade(manifest, snapshot);
        AudioSemanticManifestValidationReport validation = AudioSemanticManifestValidator.Validate(manifest, snapshot);
        return new
        {
            manifestPath = ToProjectRelativePath(rootPath, manifestPath),
            exists = File.Exists(manifestPath),
            contentHash = ComputeOptionalFileHash(manifestPath),
            manifest,
            validation,
            fmodSnapshotPath = ToProjectRelativePath(rootPath, snapshotPath),
            fmodSnapshotExists = File.Exists(snapshotPath)
        };
    }

    internal static AudioSemanticManifestSaveResponse SaveAudioSemanticManifest(
        string rootPath,
        string packageRelative,
        AudioSemanticManifestSaveRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        AudioSemanticManifestDocument previousManifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions) ?? new AudioSemanticManifestDocument();
        string currentHash = ComputeOptionalFileHash(manifestPath);
        string expectedHash = CharacterPackageHashUtility.NormalizeSha256(request != null ? request.expectedHash : string.Empty);
        if (File.Exists(manifestPath) && string.IsNullOrWhiteSpace(expectedHash))
        {
            return AudioSemanticManifestSaveResponse.Conflict(
                ToProjectRelativePath(rootPath, manifestPath),
                expectedHash,
                currentHash,
                "保存音频语义前必须提供 expectedHash。");
        }

        if (File.Exists(manifestPath) && !string.Equals(expectedHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return AudioSemanticManifestSaveResponse.Conflict(
                ToProjectRelativePath(rootPath, manifestPath),
                expectedHash,
                currentHash,
                "audio_semantic_manifest.json 已被外部修改，请刷新后再保存。");
        }

        AudioSemanticManifestDocument manifest = request != null && request.manifest != null
            ? request.manifest
            : new AudioSemanticManifestDocument();

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        NormalizeAudioSemanticManifest(manifest);
        AudioSemanticManifestValidationReport validation = AudioSemanticManifestValidator.Validate(manifest, snapshot);
        AddChangedBindingDiagnostics(rootPath, packageRelative, previousManifest, manifest, snapshot, validation, jsonOptions);
        if (validation.HasErrors)
        {
            return AudioSemanticManifestSaveResponse.Invalid(
                ToProjectRelativePath(rootPath, manifestPath),
                currentHash,
                "音频语义清单存在错误，未保存。",
                manifest,
                validation);
        }

        manifest = AudioAuthoringManifestV2.Upgrade(manifest, snapshot);
        AudioAuthoringManifestV2.Normalize(manifest, snapshot);
        manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        WriteJsonFileAtomic(manifestPath, manifest, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);
        string nextHash = ComputeOptionalFileHash(manifestPath);
        return AudioSemanticManifestSaveResponse.Success(
            ToProjectRelativePath(rootPath, manifestPath),
            currentHash,
            nextHash,
            manifest,
            validation);
    }

    internal static AudioSemanticReplacementValidationResult ValidateAudioSemanticReplacement(
        string rootPath,
        string packageRelative,
        AudioSemanticReplacementApiRequest request,
        JsonSerializerOptions jsonOptions)
    {
        string manifestPath = ResolveAudioSemanticManifestPath(rootPath);
        AudioSemanticManifestDocument manifest = ReadOptionalJsonFile<AudioSemanticManifestDocument>(rootPath, manifestPath, jsonOptions) ?? new AudioSemanticManifestDocument();

        string packageId = ResolvePackageId(rootPath, packageRelative, jsonOptions);
        string snapshotPath = ResolveFmodAudioLibrarySnapshotPath(rootPath, packageId);
        AuthoringFmodAudioLibrarySnapshotDocument snapshot = ReadOptionalJsonFile<AuthoringFmodAudioLibrarySnapshotDocument>(rootPath, snapshotPath, jsonOptions);
        List<AuthoringResourceReferenceEdge> references = FindAudioSemanticReferences(rootPath, packageRelative, request != null ? request.audioCueId : string.Empty, jsonOptions);
        return ValidateAudioSemanticReplacementRequest(manifest, snapshot, request, references);
    }

    internal static AudioSemanticReplacementValidationResult ValidateAudioSemanticReplacementRequest(
        AudioSemanticManifestDocument manifest,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        AudioSemanticReplacementApiRequest request,
        List<AuthoringResourceReferenceEdge> references)
    {
        var coreRequest = new AudioSemanticReplacementValidationRequest
        {
            AudioCueId = request != null ? request.audioCueId ?? string.Empty : string.Empty,
            TargetFmodEventPath = request != null ? request.targetFmodEventPath ?? string.Empty : string.Empty,
            TargetFmodEventGuid = request != null ? request.targetFmodEventGuid ?? string.Empty : string.Empty,
            DraftEntry = request != null ? request.draftEntry : null
        };
        AudioSemanticReplacementValidationResult result = AudioSemanticManifestValidator.ValidateReplacement(
            manifest,
            snapshot,
            coreRequest,
            references != null ? references.Count : 0);
        result.AffectedReferences = references ?? new List<AuthoringResourceReferenceEdge>();
        return result;
    }

    private static string ResolveAudioSemanticManifestPath(string rootPath)
    {
        return Path.Combine(rootPath, "Assets", "Config", "MxFramework", "Audio", "audio_semantic_manifest.json");
    }

    private static void AddChangedBindingDiagnostics(
        string rootPath,
        string packageRelative,
        AudioSemanticManifestDocument previousManifest,
        AudioSemanticManifestDocument nextManifest,
        AuthoringFmodAudioLibrarySnapshotDocument snapshot,
        AudioSemanticManifestValidationReport validation,
        JsonSerializerOptions jsonOptions)
    {
        if (previousManifest == null || nextManifest == null || nextManifest.Entries == null || validation == null)
            return;

        for (int i = 0; i < nextManifest.Entries.Count; i++)
        {
            AudioSemanticManifestEntry nextEntry = nextManifest.Entries[i];
            if (nextEntry == null || string.IsNullOrWhiteSpace(nextEntry.AudioCueId))
                continue;

            AudioSemanticManifestEntry previousEntry = AudioSemanticManifestValidator.FindEntry(
                previousManifest,
                nextEntry.AudioCueId,
                string.Empty,
                string.Empty);
            if (previousEntry == null || !HasAudioBindingChanged(previousEntry, nextEntry))
                continue;

            List<AuthoringResourceReferenceEdge> references = FindAudioSemanticReferences(rootPath, packageRelative, nextEntry.AudioCueId, jsonOptions);
            AudioSemanticReplacementValidationResult replacement = ValidateAudioSemanticReplacementRequest(
                previousManifest,
                snapshot,
                new AudioSemanticReplacementApiRequest
                {
                    audioCueId = nextEntry.AudioCueId,
                    targetFmodEventPath = nextEntry.FmodEventPath,
                    targetFmodEventGuid = nextEntry.FmodEventGuid,
                    draftEntry = nextEntry
                },
                references);
            if (replacement.Diagnostics != null)
                validation.Diagnostics.AddRange(replacement.Diagnostics);
        }

        validation.HasErrors = HasAudioSemanticErrors(validation.Diagnostics);
    }

    private static bool HasAudioBindingChanged(AudioSemanticManifestEntry previousEntry, AudioSemanticManifestEntry nextEntry)
    {
        return !string.Equals(previousEntry.FmodEventPath ?? string.Empty, nextEntry.FmodEventPath ?? string.Empty, StringComparison.Ordinal) ||
               !string.Equals(previousEntry.FmodEventGuid ?? string.Empty, nextEntry.FmodEventGuid ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAudioSemanticErrors(List<AuthoringResourceDiagnostic> diagnostics)
    {
        if (diagnostics == null)
            return false;
        for (int i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i] != null && diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                return true;
        }
        return false;
    }

    private static string ResolvePackageId(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(packageRelative))
            return string.Empty;

        try
        {
            string packagePath = ResolveSafePath(rootPath, packageRelative);
            CharacterResourcePackage package = CharacterPackageCommands.ReadPackage(packagePath, jsonOptions);
            return package.Manifest != null ? package.Manifest.PackageId ?? string.Empty : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static List<AuthoringResourceReferenceEdge> FindAudioSemanticReferences(
        string rootPath,
        string packageRelative,
        string audioCueId,
        JsonSerializerOptions jsonOptions)
    {
        var references = new List<AuthoringResourceReferenceEdge>();
        if (string.IsNullOrWhiteSpace(audioCueId))
            return references;

        try
        {
            AuthoringResourceCollection collection = ReadAuthoringResources(rootPath, packageRelative, jsonOptions);
            AuthoringResourceItem item = FindAuthoringResourceItem(collection, audioCueId);
            if (item != null && collection.ReferenceGraph != null)
                references.AddRange(collection.ReferenceGraph.FindReferencesToResource(item));
        }
        catch
        {
        }

        return references;
    }

    private static void NormalizeAudioSemanticManifest(AudioSemanticManifestDocument manifest)
    {
        if (manifest == null)
            return;

        if (manifest.SchemaVersion >= 2)
        {
            AudioAuthoringManifestV2.Normalize(manifest, null);
            manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return;
        }

        if (manifest.SchemaVersion <= 0)
            manifest.SchemaVersion = 1;
        if (string.IsNullOrWhiteSpace(manifest.ManifestId))
            manifest.ManifestId = "mxframework.audio.semantic";
        manifest.UpdatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        if (manifest.Entries == null)
            manifest.Entries = new List<AudioSemanticManifestEntry>();
        if (manifest.Metadata == null)
            manifest.Metadata = new Dictionary<string, string>();

        for (int i = 0; i < manifest.Entries.Count; i++)
        {
            AudioSemanticManifestEntry entry = manifest.Entries[i];
            if (entry == null)
                continue;

            entry.AudioCueId = Trim(entry.AudioCueId);
            entry.AudioEventDefinitionId = Trim(entry.AudioEventDefinitionId);
            entry.FmodEventPath = Trim(entry.FmodEventPath);
            entry.FmodEventGuid = Trim(entry.FmodEventGuid);
            entry.DisplayName = Trim(entry.DisplayName);
            entry.Purpose = Trim(entry.Purpose);
            entry.Context = Trim(entry.Context);
            entry.SourcePreviewPath = Trim(entry.SourcePreviewPath);
            entry.AuditionStatus = string.IsNullOrWhiteSpace(entry.AuditionStatus) ? AudioSemanticAuditionStatuses.Needed : Trim(entry.AuditionStatus);
            if (entry.Tags == null)
                entry.Tags = new List<string>();
            if (entry.ReplacementContract == null)
                entry.ReplacementContract = new AudioReplacementContract();
            if (entry.ReplacementContract.RequiredParameters == null)
                entry.ReplacementContract.RequiredParameters = new List<string>();
            if (entry.ReplacementContract.RequiredBanks == null)
                entry.ReplacementContract.RequiredBanks = new List<string>();
            if (entry.Metadata == null)
                entry.Metadata = new Dictionary<string, string>();
        }
    }

    private static string ComputeOptionalFileHash(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return string.Empty;

        return CharacterPackageHashUtility.ComputeFileSha256(path);
    }

    private static string Trim(string value)
    {
        return value == null ? string.Empty : value.Trim();
    }

    internal sealed class AudioSemanticManifestSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public AudioSemanticManifestDocument manifest { get; set; }
    }

    internal sealed class AudioSemanticReplacementApiRequest
    {
        public string package { get; set; } = string.Empty;
        public string audioCueId { get; set; } = string.Empty;
        public string targetFmodEventPath { get; set; } = string.Empty;
        public string targetFmodEventGuid { get; set; } = string.Empty;
        public AudioSemanticManifestEntry draftEntry { get; set; }
    }

    internal sealed class AudioSemanticManifestSaveResponse
    {
        public bool success { get; set; }
        public int httpStatus { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string manifestPath { get; set; } = string.Empty;
        public string previousHash { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public AudioSemanticManifestDocument manifest { get; set; }
        public AudioSemanticManifestValidationReport validation { get; set; }

        public static AudioSemanticManifestSaveResponse Success(
            string manifestPath,
            string previousHash,
            string currentHash,
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestValidationReport validation)
        {
            return new AudioSemanticManifestSaveResponse
            {
                success = true,
                httpStatus = 200,
                message = "音频语义清单已保存。",
                manifestPath = manifestPath ?? string.Empty,
                previousHash = previousHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                manifest = manifest,
                validation = validation
            };
        }

        public static AudioSemanticManifestSaveResponse Invalid(
            string manifestPath,
            string currentHash,
            string message,
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestValidationReport validation)
        {
            return new AudioSemanticManifestSaveResponse
            {
                success = false,
                httpStatus = 400,
                code = AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing,
                message = message ?? string.Empty,
                manifestPath = manifestPath ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                manifest = manifest,
                validation = validation
            };
        }

        public static AudioSemanticManifestSaveResponse Conflict(
            string manifestPath,
            string expectedHash,
            string currentHash,
            string message)
        {
            return new AudioSemanticManifestSaveResponse
            {
                success = false,
                httpStatus = 409,
                code = "AUTH_RES_AUDIO_SEMANTICS_CONFLICT",
                message = message ?? string.Empty,
                manifestPath = manifestPath ?? string.Empty,
                expectedHash = expectedHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty
            };
        }
    }
}
