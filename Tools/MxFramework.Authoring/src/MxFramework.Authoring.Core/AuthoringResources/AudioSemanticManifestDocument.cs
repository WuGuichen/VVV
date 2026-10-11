using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    /// <summary>Authoring-only semantic catalog for stable audio cues.</summary>
    /// <remarks>LowFreqAlloc. JSON DTO loaded and saved by Authoring tools.</remarks>
    public sealed class AudioSemanticManifestDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string ManifestId { get; set; } = "mxframework.audio.semantic";
        public string UpdatedAtUtc { get; set; } = string.Empty;
        public AudioIdAllocationState IdAllocation { get; set; } = new AudioIdAllocationState();
        public List<AudioBusAuthoringDefinition> Buses { get; set; } = new List<AudioBusAuthoringDefinition>();
        public List<AudioEventAuthoringDefinition> EventDefinitions { get; set; } = new List<AudioEventAuthoringDefinition>();
        public List<AudioCueAuthoringDefinition> Cues { get; set; } = new List<AudioCueAuthoringDefinition>();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AudioSemanticManifestEntry> Entries { get; set; } = new List<AudioSemanticManifestEntry>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>Human-facing semantics and FMOD binding for one stable audio cue.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class AudioSemanticManifestEntry
    {
        public string AudioCueId { get; set; } = string.Empty;
        public string AudioEventDefinitionId { get; set; } = string.Empty;
        public string FmodEventPath { get; set; } = string.Empty;
        public string FmodEventGuid { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string SourcePreviewPath { get; set; } = string.Empty;
        public string AuditionStatus { get; set; } = AudioSemanticAuditionStatuses.Needed;
        public AudioReplacementContract ReplacementContract { get; set; } = new AudioReplacementContract();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>Compatibility constraints that a replacement FMOD event must satisfy.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class AudioReplacementContract
    {
        public bool RequireSameSpatial { get; set; } = true;
        public bool RequireSameLoop { get; set; } = true;
        public int MinLengthMs { get; set; }
        public int MaxLengthMs { get; set; }
        public List<string> RequiredParameters { get; set; } = new List<string>();
        public List<string> RequiredBanks { get; set; } = new List<string>();
    }

    public static class AudioSemanticAuditionStatuses
    {
        public const string Needed = "Needed";
        public const string Recommended = "Recommended";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public static class AudioSemanticEntryStatuses
    {
        public const string Missing = "Missing";
        public const string Draft = "Draft";
        public const string ReviewNeeded = "ReviewNeeded";
        public const string Ready = "Ready";
        public const string Rejected = "Rejected";
    }

    /// <summary>Validation result for an audio semantic manifest.</summary>
    /// <remarks>LowFreqAlloc. Owns a newly allocated diagnostics collection.</remarks>
    public sealed class AudioSemanticManifestValidationReport
    {
        public bool HasErrors { get; set; }
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    /// <summary>Authoring request to validate a proposed FMOD event replacement.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only request DTO.</remarks>
    public sealed class AudioSemanticReplacementValidationRequest
    {
        public string AudioCueId { get; set; } = string.Empty;
        public string TargetFmodEventPath { get; set; } = string.Empty;
        public string TargetFmodEventGuid { get; set; } = string.Empty;
        public AudioSemanticManifestEntry DraftEntry { get; set; }
    }

    /// <summary>Compatibility and reference-impact result for an audio replacement.</summary>
    /// <remarks>LowFreqAlloc. Owns newly allocated reference and diagnostics collections.</remarks>
    public sealed class AudioSemanticReplacementValidationResult
    {
        public bool Accepted { get; set; }
        public string AudioCueId { get; set; } = string.Empty;
        public string CurrentFmodEventPath { get; set; } = string.Empty;
        public string TargetFmodEventPath { get; set; } = string.Empty;
        public int AffectedReferenceCount { get; set; }
        public List<AuthoringResourceReferenceEdge> AffectedReferences { get; set; } = new List<AuthoringResourceReferenceEdge>();
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();
    }

    public static class AudioSemanticManifestValidator
    {
        /// <summary>Validates cue identity, semantic completeness, FMOD bindings, and audition state.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time validation creates a report and diagnostics.</remarks>
        public static AudioSemanticManifestValidationReport Validate(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (manifest != null && manifest.SchemaVersion >= 2)
                return AudioAuthoringManifestV2.Validate(manifest, snapshot);

            var report = new AudioSemanticManifestValidationReport();
            if (manifest == null)
                return report;

            AddDuplicateCueDiagnostics(manifest, report.Diagnostics);

            if (snapshot != null && (snapshot.CacheStale || HasSnapshotDiagnostic(snapshot, "FMOD_CACHE_STALE")))
            {
                report.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.AudioSemanticSnapshotStale,
                    string.Empty,
                    string.Empty,
                    "FMOD snapshot is stale; semantic replacement checks may be using old technical facts.",
                    "Refresh FMOD banks and export the audio library snapshot before saving replacement decisions.",
                    "fmodAudioLibrarySnapshot"));
            }

            if (manifest.Entries != null)
            {
                for (int i = 0; i < manifest.Entries.Count; i++)
                {
                    AudioSemanticManifestEntry entry = manifest.Entries[i];
                    if (entry == null)
                        continue;

                    string audioCueId = Normalize(entry.AudioCueId);
                    if (string.IsNullOrWhiteSpace(audioCueId))
                    {
                        report.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Error,
                            AuthoringResourceDiagnosticCodes.AudioSemanticCueMissing,
                            string.Empty,
                            string.Empty,
                            "Audio semantic entry is missing audioCueId.",
                            "Bind each semantic row to a stable audioCueId.",
                            "entries/" + i + "/audioCueId"));
                    }

                    List<string> missingSemanticFields = GetMissingSemanticFields(entry);
                    if (missingSemanticFields.Count > 0)
                    {
                        report.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Warning,
                            AuthoringResourceDiagnosticCodes.AudioSemanticIncomplete,
                            audioCueId,
                            entry.FmodEventPath,
                            "Audio semantic entry is incomplete: " + string.Join(", ", missingSemanticFields) + ".",
                            "Complete the user-facing name, purpose, and context before marking this cue ready.",
                            "entries/" + i));
                    }

                    bool hasEventBinding = !string.IsNullOrWhiteSpace(entry.FmodEventPath) || !string.IsNullOrWhiteSpace(entry.FmodEventGuid);
                    if (!hasEventBinding || (snapshot != null && FindEvent(snapshot, entry.FmodEventPath, entry.FmodEventGuid) == null))
                    {
                        report.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Error,
                            AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing,
                            audioCueId,
                            entry.FmodEventPath,
                            hasEventBinding
                                ? "Semantic audio cue references a missing FMOD event: " + FirstNonEmpty(entry.FmodEventPath, entry.FmodEventGuid)
                                : "Semantic audio cue is not bound to an FMOD event.",
                            "Refresh the FMOD snapshot or rebind this cue to an existing event.",
                            "entries/" + i + "/fmodEventPath"));
                    }

                    string auditionStatus = NormalizeAuditionStatus(entry.AuditionStatus);
                    if (!IsKnownAuditionStatus(auditionStatus))
                    {
                        report.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Error,
                            AuthoringResourceDiagnosticCodes.AudioSemanticStatusInvalid,
                            audioCueId,
                            entry.FmodEventPath,
                            "Audio semantic auditionStatus is invalid: " + auditionStatus,
                            "Use Needed, Recommended, Approved, or Rejected.",
                            "entries/" + i + "/auditionStatus"));
                    }
                    else if (!string.Equals(auditionStatus, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal) &&
                             !string.Equals(auditionStatus, AudioSemanticAuditionStatuses.Rejected, StringComparison.Ordinal))
                    {
                        report.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Info,
                            AuthoringResourceDiagnosticCodes.AudioSemanticAuditionRecommended,
                            audioCueId,
                            entry.FmodEventPath,
                            "Optional human audition has not been completed for this audio cue.",
                            "Audition in FMOD/Unity when content-quality review is needed; runtime synchronization does not require approval.",
                            "entries/" + i + "/auditionStatus"));
                    }
                }
            }

            report.HasErrors = HasErrors(report.Diagnostics);
            return report;
        }

        /// <summary>Derives the review state of one semantic entry from its fields and FMOD evidence.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time status derivation may allocate a missing-field list.</remarks>
        public static string GetEntryStatus(
            AudioSemanticManifestEntry entry,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (entry == null)
                return AudioSemanticEntryStatuses.Missing;

            string auditionStatus = NormalizeAuditionStatus(entry.AuditionStatus);
            if (string.Equals(auditionStatus, AudioSemanticAuditionStatuses.Rejected, StringComparison.Ordinal))
                return AudioSemanticEntryStatuses.Rejected;

            if (!IsKnownAuditionStatus(auditionStatus) ||
                GetMissingSemanticFields(entry).Count > 0 ||
                string.IsNullOrWhiteSpace(entry.AudioCueId) ||
                (string.IsNullOrWhiteSpace(entry.FmodEventPath) && string.IsNullOrWhiteSpace(entry.FmodEventGuid)) ||
                snapshot == null ||
                FindEvent(snapshot, entry.FmodEventPath, entry.FmodEventGuid) == null)
            {
                return AudioSemanticEntryStatuses.Draft;
            }

            return AudioSemanticEntryStatuses.Ready;
        }

        /// <summary>Finds a semantic entry by stable cue id, FMOD path, or FMOD guid.</summary>
        /// <remarks>NoAlloc. Performs a linear lookup over the existing manifest collection.</remarks>
        public static AudioSemanticManifestEntry FindEntry(
            AudioSemanticManifestDocument manifest,
            string audioCueId,
            string fmodEventPath,
            string fmodEventGuid)
        {
            if (manifest == null)
                return null;

            if (manifest.SchemaVersion >= 2 && manifest.Cues != null)
            {
                for (int i = 0; i < manifest.Cues.Count; i++)
                {
                    AudioCueAuthoringDefinition cue = manifest.Cues[i];
                    if (cue == null)
                        continue;
                    AudioEventAuthoringDefinition eventDefinition = AudioAuthoringManifestV2.FindEventDefinition(
                        manifest,
                        cue.EventDefinitionId);
                    bool matches = !string.IsNullOrWhiteSpace(audioCueId)
                        && string.Equals(cue.AudioCueId, audioCueId, StringComparison.Ordinal);
                    if (!matches && eventDefinition != null && !string.IsNullOrWhiteSpace(fmodEventPath))
                        matches = string.Equals(eventDefinition.FmodEventPath, fmodEventPath, StringComparison.Ordinal);
                    if (!matches && eventDefinition != null && !string.IsNullOrWhiteSpace(fmodEventGuid))
                        matches = string.Equals(eventDefinition.FmodEventGuid, fmodEventGuid, StringComparison.OrdinalIgnoreCase);
                    if (!matches)
                        continue;

                    return AudioAuthoringManifestV2.ToLegacyEntry(cue, eventDefinition);
                }
            }

            if (manifest.Entries == null)
                return null;

            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                AudioSemanticManifestEntry entry = manifest.Entries[i];
                if (entry == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(audioCueId) &&
                    string.Equals(entry.AudioCueId, audioCueId, StringComparison.Ordinal))
                    return entry;
                if (!string.IsNullOrWhiteSpace(fmodEventPath) &&
                    string.Equals(entry.FmodEventPath, fmodEventPath, StringComparison.Ordinal))
                    return entry;
                if (!string.IsNullOrWhiteSpace(fmodEventGuid) &&
                    string.Equals(entry.FmodEventGuid, fmodEventGuid, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        /// <summary>Finds an FMOD snapshot event by path or guid.</summary>
        /// <remarks>NoAlloc. Performs a linear lookup over the existing snapshot collection.</remarks>
        public static AuthoringFmodAudioLibraryEvent FindEvent(
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            string fmodEventPath,
            string fmodEventGuid)
        {
            if (snapshot == null || snapshot.Events == null)
                return null;

            for (int i = 0; i < snapshot.Events.Count; i++)
            {
                AuthoringFmodAudioLibraryEvent audioEvent = snapshot.Events[i];
                if (audioEvent == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(fmodEventPath) &&
                    string.Equals(audioEvent.Path, fmodEventPath, StringComparison.Ordinal))
                    return audioEvent;
                if (!string.IsNullOrWhiteSpace(fmodEventGuid) &&
                    string.Equals(audioEvent.Guid, fmodEventGuid, StringComparison.OrdinalIgnoreCase))
                    return audioEvent;
            }

            return null;
        }

        /// <summary>Resolves a stable cue id to its current FMOD snapshot event.</summary>
        /// <remarks>LowFreqAlloc. The fallback stable-id derivation can allocate strings.</remarks>
        public static AuthoringFmodAudioLibraryEvent FindEventByCueId(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            string audioCueId)
        {
            if (snapshot == null || snapshot.Events == null || string.IsNullOrWhiteSpace(audioCueId))
                return null;

            AudioSemanticManifestEntry entry = FindEntry(manifest, audioCueId, string.Empty, string.Empty);
            if (entry != null)
            {
                AuthoringFmodAudioLibraryEvent bound = FindEvent(snapshot, entry.FmodEventPath, entry.FmodEventGuid);
                if (bound != null)
                    return bound;
            }

            for (int i = 0; i < snapshot.Events.Count; i++)
            {
                AuthoringFmodAudioLibraryEvent audioEvent = snapshot.Events[i];
                if (audioEvent == null)
                    continue;
                if (string.Equals(AuthoringResourceNaming.BuildAudioCueId(audioEvent.Path), audioCueId, StringComparison.Ordinal))
                    return audioEvent;
            }

            return null;
        }

        /// <summary>Validates a proposed target event against the current binding and replacement contract.</summary>
        /// <remarks>LowFreqAlloc. Authoring-time validation creates a result and diagnostics.</remarks>
        public static AudioSemanticReplacementValidationResult ValidateReplacement(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AudioSemanticReplacementValidationRequest request,
            int affectedReferenceCount)
        {
            var result = new AudioSemanticReplacementValidationResult
            {
                AudioCueId = request != null ? request.AudioCueId ?? string.Empty : string.Empty,
                AffectedReferenceCount = affectedReferenceCount
            };

            if (request == null || string.IsNullOrWhiteSpace(request.AudioCueId))
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                    string.Empty,
                    string.Empty,
                    "Replacement validation requires audioCueId.",
                    "Select an AudioCue row before validating replacement.",
                    "audioCueId"));
                result.Accepted = false;
                return result;
            }

            AddDuplicateCueDiagnostics(manifest, result.Diagnostics);

            if (snapshot == null)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.FmodUnavailable,
                    request.AudioCueId,
                    string.Empty,
                    "FMOD snapshot is not available.",
                    "Export FMOD audio metadata before validating replacements.",
                    "fmodAudioLibrarySnapshot"));
                result.Accepted = false;
                return result;
            }

            if (snapshot.CacheStale || HasSnapshotDiagnostic(snapshot, "FMOD_CACHE_STALE"))
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.AudioSemanticSnapshotStale,
                    request.AudioCueId,
                    string.Empty,
                    "FMOD snapshot is stale.",
                    "Refresh the FMOD snapshot before final replacement approval.",
                    "fmodAudioLibrarySnapshot"));
            }

            AuthoringFmodAudioLibraryEvent currentEvent = FindEventByCueId(manifest, snapshot, request.AudioCueId);
            if (currentEvent == null)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing,
                    request.AudioCueId,
                    string.Empty,
                    "Current audioCueId is not bound to any FMOD event in the snapshot.",
                    "Refresh FMOD metadata or restore the semantic manifest binding.",
                    "audioCueId"));
                result.Accepted = false;
                return result;
            }

            string targetPath = FirstNonEmpty(request.TargetFmodEventPath, request.DraftEntry != null ? request.DraftEntry.FmodEventPath : string.Empty, currentEvent.Path);
            string targetGuid = FirstNonEmpty(request.TargetFmodEventGuid, string.IsNullOrWhiteSpace(targetPath) && request.DraftEntry != null ? request.DraftEntry.FmodEventGuid : string.Empty);
            AuthoringFmodAudioLibraryEvent targetEvent = !string.IsNullOrWhiteSpace(targetPath)
                ? FindEvent(snapshot, targetPath, string.Empty)
                : FindEvent(snapshot, string.Empty, targetGuid);
            result.CurrentFmodEventPath = currentEvent.Path ?? string.Empty;
            result.TargetFmodEventPath = targetEvent != null ? targetEvent.Path ?? string.Empty : targetPath;

            if (targetEvent == null)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing,
                    request.AudioCueId,
                    targetPath,
                    "Replacement target FMOD event is missing: " + FirstNonEmpty(targetPath, targetGuid),
                    "Pick an FMOD event from the current snapshot.",
                    "targetFmodEventPath"));
                result.Accepted = false;
                return result;
            }

            AudioReplacementContract contract = request.DraftEntry != null && request.DraftEntry.ReplacementContract != null
                ? request.DraftEntry.ReplacementContract
                : FindEntry(manifest, request.AudioCueId, string.Empty, string.Empty)?.ReplacementContract ?? new AudioReplacementContract();

            ValidateReplacementFacts(result, request.AudioCueId, currentEvent, targetEvent, contract);
            result.Accepted = !HasErrors(result.Diagnostics);
            return result;
        }

        private static void ValidateReplacementFacts(
            AudioSemanticReplacementValidationResult result,
            string audioCueId,
            AuthoringFmodAudioLibraryEvent currentEvent,
            AuthoringFmodAudioLibraryEvent targetEvent,
            AudioReplacementContract contract)
        {
            if (targetEvent.Banks == null || targetEvent.Banks.Count == 0)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.FmodBankMissing,
                    audioCueId,
                    targetEvent.Path,
                    "Replacement target has no bank binding.",
                    "Rebuild banks or select an event with a bank.",
                    "banks"));
            }

            if (contract == null)
                contract = new AudioReplacementContract();

            if (contract.RequireSameSpatial && currentEvent.Is3D != targetEvent.Is3D)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                    audioCueId,
                    targetEvent.Path,
                    "Replacement changes 2D/3D behavior.",
                    "Choose an event with the same spatial behavior or explicitly change the replacement contract.",
                    "replacementContract.requireSameSpatial"));
            }

            if (contract.RequireSameLoop && currentEvent.IsLoop != targetEvent.IsLoop)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                    audioCueId,
                    targetEvent.Path,
                    "Replacement changes loop behavior.",
                    "Choose an event with the same loop behavior or explicitly change the replacement contract.",
                    "replacementContract.requireSameLoop"));
            }

            List<string> requiredParameters = contract.RequiredParameters != null && contract.RequiredParameters.Count > 0
                ? contract.RequiredParameters
                : GetParameterNames(currentEvent.Parameters);
            AddMissingListDiagnostics(result, audioCueId, targetEvent.Path, "parameter", requiredParameters, GetParameterNames(targetEvent.Parameters), AuthoringResourceDiagnosticCodes.FmodParameterMismatch, "replacementContract.requiredParameters");

            if (contract.RequiredBanks != null && contract.RequiredBanks.Count > 0)
                AddMissingListDiagnostics(result, audioCueId, targetEvent.Path, "bank", contract.RequiredBanks, targetEvent.Banks, AuthoringResourceDiagnosticCodes.FmodBankMissing, "replacementContract.requiredBanks");

            if (contract.MinLengthMs > 0 && targetEvent.LengthMs > 0 && targetEvent.LengthMs < contract.MinLengthMs)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                    audioCueId,
                    targetEvent.Path,
                    "Replacement target is shorter than the configured minimum length.",
                    "Pick a longer event or update replacementContract.minLengthMs.",
                    "replacementContract.minLengthMs"));
            }

            if (contract.MaxLengthMs > 0 && targetEvent.LengthMs > contract.MaxLengthMs)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.AudioSemanticReplacementRejected,
                    audioCueId,
                    targetEvent.Path,
                    "Replacement target is longer than the configured maximum length.",
                    "Pick a shorter event or update replacementContract.maxLengthMs.",
                    "replacementContract.maxLengthMs"));
            }
        }

        private static void AddDuplicateCueDiagnostics(AudioSemanticManifestDocument manifest, List<AuthoringResourceDiagnostic> diagnostics)
        {
            if (manifest == null || diagnostics == null)
                return;

            if (manifest.SchemaVersion >= 2)
                return;
            if (manifest.Entries == null)
                return;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.Entries.Count; i++)
            {
                AudioSemanticManifestEntry entry = manifest.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.AudioCueId))
                    continue;

                if (!seen.Add(entry.AudioCueId))
                {
                    diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.AudioSemanticCueDuplicate,
                        entry.AudioCueId,
                        entry.FmodEventPath,
                        "Duplicate audioCueId in audio semantic manifest: " + entry.AudioCueId,
                        "Keep exactly one semantic entry per stable audioCueId.",
                        "entries/" + i + "/audioCueId"));
                }
            }
        }

        private static void AddMissingListDiagnostics(
            AudioSemanticReplacementValidationResult result,
            string audioCueId,
            string fmodEventPath,
            string label,
            List<string> required,
            List<string> actual,
            string code,
            string sourceField)
        {
            if (required == null || required.Count == 0)
                return;

            var actualSet = new HashSet<string>(StringComparer.Ordinal);
            if (actual != null)
            {
                for (int i = 0; i < actual.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(actual[i]))
                        actualSet.Add(actual[i]);
                }
            }

            for (int i = 0; i < required.Count; i++)
            {
                string requiredValue = Normalize(required[i]);
                if (string.IsNullOrWhiteSpace(requiredValue) || actualSet.Contains(requiredValue))
                    continue;

                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    code,
                    audioCueId,
                    fmodEventPath,
                    "Replacement target is missing required " + label + ": " + requiredValue,
                    "Pick a target event that keeps the required " + label + " or update the replacement contract.",
                    sourceField));
            }
        }

        private static List<string> GetParameterNames(List<AuthoringFmodAudioLibraryParameter> parameters)
        {
            var names = new List<string>();
            if (parameters == null)
                return names;

            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && !string.IsNullOrWhiteSpace(parameters[i].Name))
                    names.Add(parameters[i].Name);
            }

            return names;
        }

        private static bool HasSnapshotDiagnostic(AuthoringFmodAudioLibrarySnapshotDocument snapshot, string code)
        {
            if (snapshot == null || snapshot.Diagnostics == null)
                return false;

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                AuthoringFmodAudioLibraryDiagnostic diagnostic = snapshot.Diagnostics[i];
                if (diagnostic != null && string.Equals(diagnostic.Code, code, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasErrors(List<AuthoringResourceDiagnostic> diagnostics)
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

        private static List<string> GetMissingSemanticFields(AudioSemanticManifestEntry entry)
        {
            var fields = new List<string>();
            if (entry == null)
                return fields;

            if (string.IsNullOrWhiteSpace(entry.DisplayName))
                fields.Add("displayName");
            if (string.IsNullOrWhiteSpace(entry.Purpose))
                fields.Add("purpose");
            if (string.IsNullOrWhiteSpace(entry.Context))
                fields.Add("context");
            return fields;
        }

        private static bool IsKnownAuditionStatus(string status)
        {
            return string.Equals(status, AudioSemanticAuditionStatuses.Needed, StringComparison.Ordinal) ||
                   string.Equals(status, AudioSemanticAuditionStatuses.Recommended, StringComparison.Ordinal) ||
                   string.Equals(status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal) ||
                   string.Equals(status, AudioSemanticAuditionStatuses.Rejected, StringComparison.Ordinal);
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string audioCueId,
            string fmodEventPath,
            string message,
            string suggestedFix,
            string sourceField)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                RuntimeResourceKey = audioCueId ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "audioSemanticManifest",
                SourceStableId = fmodEventPath ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static string NormalizeAuditionStatus(string status)
        {
            string value = Normalize(status);
            return string.IsNullOrWhiteSpace(value) ? AudioSemanticAuditionStatuses.Needed : value;
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
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
    }
}
