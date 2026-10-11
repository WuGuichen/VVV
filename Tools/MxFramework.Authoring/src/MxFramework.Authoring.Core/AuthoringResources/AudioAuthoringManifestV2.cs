using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Authoring
{
    /// <summary>Independent monotonic runtime-id spaces owned by the project audio manifest.</summary>
    /// <remarks>LowFreqAlloc. Authoring-only JSON DTO.</remarks>
    public sealed class AudioIdAllocationState
    {
        public int NextCueId { get; set; } = 500001;
        public int NextEventId { get; set; } = 500001;
        public int NextBusId { get; set; } = 500001;
        public int NextParameterId { get; set; } = 500001;
        public List<int> RetiredCueIds { get; set; } = new List<int>();
        public List<int> RetiredEventIds { get; set; } = new List<int>();
        public List<int> RetiredBusIds { get; set; } = new List<int>();
        public List<int> RetiredParameterIds { get; set; } = new List<int>();
    }

    public sealed class AudioBusAuthoringDefinition
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeBusId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string FmodBusPath { get; set; } = string.Empty;
        public string FmodVcaPath { get; set; } = string.Empty;
        public float DefaultVolume { get; set; } = 1f;
        public bool DefaultMuted { get; set; }
    }

    public sealed class AudioEventAuthoringDefinition
    {
        public string Id { get; set; } = string.Empty;
        public int RuntimeEventId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string FmodEventPath { get; set; } = string.Empty;
        public string FmodEventGuid { get; set; } = string.Empty;
        public string BusId { get; set; } = string.Empty;
        public string Kind { get; set; } = "Event";
        public bool Is3D { get; set; }
        public bool IsLoop { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }
        public List<AudioParameterAuthoringDefinition> Parameters { get; set; } = new List<AudioParameterAuthoringDefinition>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AudioParameterAuthoringDefinition
    {
        public string Name { get; set; } = string.Empty;
        public int RuntimeParameterId { get; set; }
        public string Kind { get; set; } = "Continuous";
        public float DefaultValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; } = 1f;
    }

    public sealed class AudioCueAuthoringDefinition
    {
        public string AudioCueId { get; set; } = string.Empty;
        public int RuntimeCueId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string SourcePreviewPath { get; set; } = string.Empty;
        public string EventDefinitionId { get; set; } = string.Empty;
        public string PlayMode { get; set; } = "OneShot";
        public string MissingPolicy { get; set; } = "Mute";
        public List<AudioCueDefaultParameter> DefaultParameters { get; set; } = new List<AudioCueDefaultParameter>();
        public AudioReplacementContract ReplacementContract { get; set; } = new AudioReplacementContract();
        public AudioCueAuditionEvidence AuditionEvidence { get; set; } = new AudioCueAuditionEvidence();
        public string Lifecycle { get; set; } = AudioCueLifecycles.Draft;
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    public sealed class AudioCueDefaultParameter
    {
        public string Name { get; set; } = string.Empty;
        public float Value { get; set; }
    }

    public sealed class AudioCueAuditionEvidence
    {
        public string Status { get; set; } = AudioSemanticAuditionStatuses.Needed;
        public string ReviewMode { get; set; } = AudioCueAuditionReviewModes.PerCueAudition;
        public string PreviewRequestId { get; set; } = string.Empty;
        public string EventBindingHash { get; set; } = string.Empty;
        public string SnapshotHash { get; set; } = string.Empty;
        public string AppliedSetId { get; set; } = string.Empty;
        public string BankHash { get; set; } = string.Empty;
        public string AudioBasisId { get; set; } = string.Empty;
        public int RuntimeBasisId { get; set; }
        public string PhysicalManifestHash { get; set; } = string.Empty;
        public string ReviewedAtUtc { get; set; } = string.Empty;
    }

    public static class AudioCueAuditionReviewModes
    {
        public const string PerCueAudition = "PerCueAudition";
        public const string BulkOperatorApproval = "BulkOperatorApproval";
    }

    public static class AudioCueLifecycles
    {
        public const string Draft = "Draft";
        public const string Active = "Active";
        public const string Archived = "Archived";
    }

    public sealed class AudioRuntimeCatalogDocument
    {
        public string Format { get; set; } = "mx.audioRuntimeCatalog.v1";
        public string SchemaVersion { get; set; } = "1.0";
        public string ManifestHash { get; set; } = string.Empty;
        public string SnapshotHash { get; set; } = string.Empty;
        public string GeneratedAtUtc { get; set; } = string.Empty;
        public List<AudioRuntimeBusDefinition> Buses { get; set; } = new List<AudioRuntimeBusDefinition>();
        public List<AudioRuntimeEventDefinition> Events { get; set; } = new List<AudioRuntimeEventDefinition>();
        public List<AudioRuntimeCueDefinition> Cues { get; set; } = new List<AudioRuntimeCueDefinition>();
    }

    public sealed class AudioRuntimeBusDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FmodBusPath { get; set; } = string.Empty;
        public string FmodVcaPath { get; set; } = string.Empty;
        public float DefaultVolume { get; set; } = 1f;
        public bool DefaultMuted { get; set; }
    }

    public sealed class AudioRuntimeEventDefinition
    {
        public int Id { get; set; }
        public string StableId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FmodEventPath { get; set; } = string.Empty;
        public string FmodEventGuid { get; set; } = string.Empty;
        public string Kind { get; set; } = "Event";
        public int BusId { get; set; }
        public bool Is3D { get; set; }
        public bool IsLoop { get; set; }
        public float MaxDistance { get; set; }
        public List<AudioRuntimeParameterDefinition> Parameters { get; set; } = new List<AudioRuntimeParameterDefinition>();
        public List<string> Labels { get; set; } = new List<string>();
    }

    public sealed class AudioRuntimeParameterDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = "Continuous";
        public float DefaultValue { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; } = 1f;
    }

    public sealed class AudioRuntimeCueDefinition
    {
        public int Id { get; set; }
        public string AudioCueId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventDefinitionId { get; set; } = string.Empty;
        public string PlayMode { get; set; } = "OneShot";
        public string MissingPolicy { get; set; } = "Mute";
        public List<AudioRuntimeParameterValue> DefaultParameters { get; set; } = new List<AudioRuntimeParameterValue>();
        public List<string> Labels { get; set; } = new List<string>();
    }

    public sealed class AudioRuntimeParameterValue
    {
        public int ParameterId { get; set; }
        public float Value { get; set; }
    }

    public static class AudioAuthoringManifestV2
    {
        public const int MinimumRuntimeId = 500000;
        public const int MaximumRuntimeId = 599999;
        public const string UiFeedbackCueSetMetadataKey = "uiFeedbackCueSet";
        public const string UiFeedbackCueSetV1 = "v1";

        /// <summary>Upgrades a v1 entries manifest in memory and allocates stable runtime ids.</summary>
        /// <remarks>LowFreqAlloc. Migration runs only while loading authoring configuration.</remarks>
        public static AudioSemanticManifestDocument Upgrade(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            manifest ??= new AudioSemanticManifestDocument();
            if (manifest.SchemaVersion >= 2 && manifest.Cues != null && manifest.EventDefinitions != null)
            {
                Normalize(manifest, snapshot);
                return manifest;
            }

            List<AudioSemanticManifestEntry> entries = manifest.Entries ?? new List<AudioSemanticManifestEntry>();
            manifest.SchemaVersion = 2;
            manifest.IdAllocation ??= new AudioIdAllocationState();
            manifest.Buses = new List<AudioBusAuthoringDefinition>
            {
                new AudioBusAuthoringDefinition
                {
                    Id = "audio.bus.master",
                    RuntimeBusId = 500001,
                    DisplayName = "Master",
                    FmodBusPath = "bus:/"
                }
            };
            manifest.EventDefinitions = new List<AudioEventAuthoringDefinition>();
            manifest.Cues = new List<AudioCueAuthoringDefinition>();

            for (int i = 0; i < entries.Count; i++)
            {
                AudioSemanticManifestEntry entry = entries[i];
                if (entry == null)
                    continue;
                string eventId = string.IsNullOrWhiteSpace(entry.AudioEventDefinitionId)
                    ? AuthoringResourceNaming.BuildAudioEventDefinitionId(entry.FmodEventPath)
                    : entry.AudioEventDefinitionId.Trim();
                AuthoringFmodAudioLibraryEvent snapshotEvent = AudioSemanticManifestValidator.FindEvent(
                    snapshot,
                    entry.FmodEventPath,
                    entry.FmodEventGuid);
                AudioEventAuthoringDefinition eventDefinition = FindEventDefinition(manifest, eventId);
                if (eventDefinition == null)
                {
                    eventDefinition = FromLegacyEvent(manifest, entry, eventId, snapshotEvent);
                    manifest.EventDefinitions.Add(eventDefinition);
                }

                manifest.Cues.Add(new AudioCueAuthoringDefinition
                {
                    AudioCueId = entry.AudioCueId ?? string.Empty,
                    RuntimeCueId = AllocateCueId(manifest),
                    DisplayName = entry.DisplayName ?? string.Empty,
                    Purpose = entry.Purpose ?? string.Empty,
                    Context = entry.Context ?? string.Empty,
                    Tags = entry.Tags ?? new List<string>(),
                    SourcePreviewPath = entry.SourcePreviewPath ?? string.Empty,
                    EventDefinitionId = eventDefinition.Id,
                    PlayMode = snapshotEvent != null && snapshotEvent.IsLoop ? "StartEvent" : "OneShot",
                    ReplacementContract = entry.ReplacementContract ?? new AudioReplacementContract(),
                    AuditionEvidence = new AudioCueAuditionEvidence { Status = entry.AuditionStatus ?? AudioSemanticAuditionStatuses.Needed },
                    Lifecycle = AudioCueLifecycles.Draft,
                    Metadata = entry.Metadata ?? new Dictionary<string, string>()
                });
            }

            manifest.Entries = null;
            Normalize(manifest, snapshot);
            return manifest;
        }

        /// <summary>Normalizes nullable collections and advances monotonic allocation cursors.</summary>
        /// <remarks>LowFreqAlloc. Authoring save-time normalization.</remarks>
        public static void Normalize(AudioSemanticManifestDocument manifest, AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (manifest == null)
                return;
            manifest.SchemaVersion = 2;
            manifest.ManifestId = string.IsNullOrWhiteSpace(manifest.ManifestId) ? "mxframework.audio.semantic" : manifest.ManifestId.Trim();
            manifest.IdAllocation ??= new AudioIdAllocationState();
            manifest.Buses ??= new List<AudioBusAuthoringDefinition>();
            manifest.EventDefinitions ??= new List<AudioEventAuthoringDefinition>();
            manifest.Cues ??= new List<AudioCueAuthoringDefinition>();
            manifest.Metadata ??= new Dictionary<string, string>();

            for (int i = 0; i < manifest.Buses.Count; i++)
            {
                AudioBusAuthoringDefinition bus = manifest.Buses[i];
                if (bus == null)
                    continue;
                bus.Id = Trim(bus.Id);
                bus.DisplayName = Trim(bus.DisplayName);
                bus.FmodBusPath = Trim(bus.FmodBusPath);
                bus.FmodVcaPath = Trim(bus.FmodVcaPath);
                if (bus.RuntimeBusId <= 0)
                    bus.RuntimeBusId = AllocateBusId(manifest);
                manifest.IdAllocation.NextBusId = Advance(manifest.IdAllocation.NextBusId, bus.RuntimeBusId);
            }

            for (int i = 0; i < manifest.EventDefinitions.Count; i++)
            {
                AudioEventAuthoringDefinition audioEvent = manifest.EventDefinitions[i];
                if (audioEvent == null)
                    continue;
                audioEvent.Id = Trim(audioEvent.Id);
                audioEvent.DisplayName = Trim(audioEvent.DisplayName);
                audioEvent.FmodEventPath = Trim(audioEvent.FmodEventPath);
                audioEvent.FmodEventGuid = Trim(audioEvent.FmodEventGuid);
                audioEvent.BusId = Trim(audioEvent.BusId);
                audioEvent.Parameters ??= new List<AudioParameterAuthoringDefinition>();
                audioEvent.Metadata ??= new Dictionary<string, string>();
                if (audioEvent.RuntimeEventId <= 0)
                    audioEvent.RuntimeEventId = AllocateEventId(manifest);
                manifest.IdAllocation.NextEventId = Advance(manifest.IdAllocation.NextEventId, audioEvent.RuntimeEventId);
                SynchronizeEventFacts(manifest, audioEvent, snapshot);
                for (int p = 0; p < audioEvent.Parameters.Count; p++)
                {
                    AudioParameterAuthoringDefinition parameter = audioEvent.Parameters[p];
                    if (parameter == null)
                        continue;
                    parameter.Name = Trim(parameter.Name);
                    if (parameter.RuntimeParameterId <= 0)
                        parameter.RuntimeParameterId = AllocateParameterId(manifest);
                    manifest.IdAllocation.NextParameterId = Advance(manifest.IdAllocation.NextParameterId, parameter.RuntimeParameterId);
                }
            }

            for (int i = 0; i < manifest.Cues.Count; i++)
            {
                AudioCueAuthoringDefinition cue = manifest.Cues[i];
                if (cue == null)
                    continue;
                cue.AudioCueId = Trim(cue.AudioCueId);
                cue.DisplayName = Trim(cue.DisplayName);
                cue.Purpose = Trim(cue.Purpose);
                cue.Context = Trim(cue.Context);
                cue.SourcePreviewPath = Trim(cue.SourcePreviewPath);
                cue.EventDefinitionId = Trim(cue.EventDefinitionId);
                cue.PlayMode = string.IsNullOrWhiteSpace(cue.PlayMode) ? "OneShot" : cue.PlayMode.Trim();
                cue.MissingPolicy = string.IsNullOrWhiteSpace(cue.MissingPolicy) ? "Mute" : cue.MissingPolicy.Trim();
                cue.Lifecycle = string.IsNullOrWhiteSpace(cue.Lifecycle) ? AudioCueLifecycles.Draft : cue.Lifecycle.Trim();
                cue.Tags ??= new List<string>();
                cue.DefaultParameters ??= new List<AudioCueDefaultParameter>();
                cue.ReplacementContract ??= new AudioReplacementContract();
                cue.AuditionEvidence ??= new AudioCueAuditionEvidence();
                cue.Metadata ??= new Dictionary<string, string>();
                if (cue.RuntimeCueId <= 0)
                    cue.RuntimeCueId = AllocateCueId(manifest);
                manifest.IdAllocation.NextCueId = Advance(manifest.IdAllocation.NextCueId, cue.RuntimeCueId);
                string currentBindingHash = ComputeBindingHash(cue, FindEventDefinition(manifest, cue.EventDefinitionId));
                if (!string.IsNullOrWhiteSpace(cue.AuditionEvidence.EventBindingHash)
                    && !string.Equals(cue.AuditionEvidence.EventBindingHash, currentBindingHash, StringComparison.OrdinalIgnoreCase))
                {
                    cue.AuditionEvidence = new AudioCueAuditionEvidence { Status = AudioSemanticAuditionStatuses.Needed };
                }
            }

            manifest.Entries = null;
        }

        /// <summary>Validates schema, identities, references, FMOD facts, and publication readiness.</summary>
        /// <remarks>LowFreqAlloc. Authoring diagnostics own allocated collections.</remarks>
        public static AudioSemanticManifestValidationReport Validate(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            var report = new AudioSemanticManifestValidationReport();
            if (manifest == null)
                return report;

            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            var cueRuntimeIds = new HashSet<int>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var eventRuntimeIds = new HashSet<int>();
            var busIds = new HashSet<string>(StringComparer.Ordinal);
            var busRuntimeIds = new HashSet<int>();
            var parameterRuntimeIds = new Dictionary<int, string>();

            ValidateBuses(manifest, report, busIds, busRuntimeIds);
            ValidateEvents(manifest, snapshot, report, eventIds, eventRuntimeIds, busIds, parameterRuntimeIds);
            ValidateCues(manifest, snapshot, report, cueIds, cueRuntimeIds, eventIds);
            ValidateUiFeedbackCueSet(manifest, report);
            report.HasErrors = HasErrors(report.Diagnostics);
            return report;
        }

        /// <summary>Compiles Active cues plus explicitly controlled Events to the startup runtime catalog.</summary>
        /// <remarks>LowFreqAlloc. Publication builds a complete immutable DTO graph.</remarks>
        public static AudioRuntimeCatalogDocument CompileRuntimeCatalog(
            AudioSemanticManifestDocument manifest,
            string manifestHash,
            string snapshotHash)
        {
            var catalog = new AudioRuntimeCatalogDocument
            {
                ManifestHash = manifestHash ?? string.Empty,
                SnapshotHash = snapshotHash ?? string.Empty,
                GeneratedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            if (manifest == null)
                return catalog;

            var requiredEventIds = new HashSet<string>(StringComparer.Ordinal);
            var requiredBusIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < manifest.Cues.Count; i++)
            {
                AudioCueAuthoringDefinition cue = manifest.Cues[i];
                if (cue == null || !string.Equals(cue.Lifecycle, AudioCueLifecycles.Active, StringComparison.Ordinal))
                    continue;
                AudioEventAuthoringDefinition audioEvent = FindEventDefinition(manifest, cue.EventDefinitionId);
                if (audioEvent == null)
                    continue;
                requiredEventIds.Add(audioEvent.Id);
                requiredBusIds.Add(audioEvent.BusId);
                catalog.Cues.Add(BuildRuntimeCue(cue, audioEvent));
            }

            for (int i = 0; i < manifest.EventDefinitions.Count; i++)
            {
                AudioEventAuthoringDefinition audioEvent = manifest.EventDefinitions[i];
                if (IsRuntimeControlledEvent(audioEvent))
                {
                    requiredEventIds.Add(audioEvent.Id);
                    requiredBusIds.Add(audioEvent.BusId);
                }
            }

            for (int i = 0; i < manifest.EventDefinitions.Count; i++)
            {
                AudioEventAuthoringDefinition audioEvent = manifest.EventDefinitions[i];
                if (audioEvent != null && requiredEventIds.Contains(audioEvent.Id))
                    catalog.Events.Add(BuildRuntimeEvent(audioEvent, FindBusDefinition(manifest, audioEvent.BusId)));
            }

            for (int i = 0; i < manifest.Buses.Count; i++)
            {
                AudioBusAuthoringDefinition bus = manifest.Buses[i];
                if (bus != null && requiredBusIds.Contains(bus.Id))
                    catalog.Buses.Add(BuildRuntimeBus(bus));
            }
            return catalog;
        }

        private static bool IsRuntimeControlledEvent(AudioEventAuthoringDefinition audioEvent)
        {
            return audioEvent != null
                && audioEvent.Metadata != null
                && audioEvent.Metadata.TryGetValue("runtimeControlled", out string value)
                && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static AudioEventAuthoringDefinition FindEventDefinition(AudioSemanticManifestDocument manifest, string id)
        {
            if (manifest == null || manifest.EventDefinitions == null || string.IsNullOrWhiteSpace(id))
                return null;
            for (int i = 0; i < manifest.EventDefinitions.Count; i++)
            {
                AudioEventAuthoringDefinition item = manifest.EventDefinitions[i];
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        public static AudioCueAuthoringDefinition FindCue(AudioSemanticManifestDocument manifest, string audioCueId)
        {
            if (manifest == null || manifest.Cues == null || string.IsNullOrWhiteSpace(audioCueId))
                return null;
            for (int i = 0; i < manifest.Cues.Count; i++)
            {
                AudioCueAuthoringDefinition cue = manifest.Cues[i];
                if (cue != null && string.Equals(cue.AudioCueId, audioCueId, StringComparison.Ordinal))
                    return cue;
            }
            return null;
        }

        public static string GetCueStatus(
            AudioSemanticManifestDocument manifest,
            AudioCueAuthoringDefinition cue,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (cue == null)
                return AudioSemanticEntryStatuses.Missing;
            if (string.Equals(cue.Lifecycle, AudioCueLifecycles.Archived, StringComparison.Ordinal))
                return AudioCueLifecycles.Archived;
            if (cue.AuditionEvidence != null && string.Equals(cue.AuditionEvidence.Status, AudioSemanticAuditionStatuses.Rejected, StringComparison.Ordinal))
                return AudioSemanticEntryStatuses.Rejected;
            AudioEventAuthoringDefinition audioEvent = FindEventDefinition(manifest, cue.EventDefinitionId);
            if (string.IsNullOrWhiteSpace(cue.AudioCueId)
                || string.IsNullOrWhiteSpace(cue.DisplayName)
                || string.IsNullOrWhiteSpace(cue.Purpose)
                || string.IsNullOrWhiteSpace(cue.Context)
                || audioEvent == null
                || AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid) == null)
                return AudioSemanticEntryStatuses.Draft;
            return AudioSemanticEntryStatuses.Ready;
        }

        public static string ComputeBindingHash(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent)
        {
            if (cue == null || audioEvent == null)
                return string.Empty;
            var builder = new StringBuilder(256);
            builder.Append(cue.AudioCueId).Append('|')
                .Append(cue.EventDefinitionId).Append('|')
                .Append(audioEvent.FmodEventPath).Append('|')
                .Append(audioEvent.FmodEventGuid).Append('|')
                .Append(audioEvent.Is3D).Append('|')
                .Append(audioEvent.IsLoop).Append('|')
                .Append(audioEvent.MinDistance.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(audioEvent.MaxDistance.ToString(CultureInfo.InvariantCulture));
            if (audioEvent.Parameters != null)
            {
                for (int i = 0; i < audioEvent.Parameters.Count; i++)
                    builder.Append('|').Append(audioEvent.Parameters[i]?.Name);
            }
            return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
        }

        public static string ComputeSnapshotHash(AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (snapshot == null)
                return string.Empty;
            var builder = new StringBuilder(1024);
            builder.Append(snapshot.GeneratedAtUtc).Append('|').Append(snapshot.CacheTimeUtc).Append('|').Append(snapshot.CacheValid).Append('|').Append(snapshot.CacheStale);
            if (snapshot.Events != null)
            {
                for (int i = 0; i < snapshot.Events.Count; i++)
                {
                    AuthoringFmodAudioLibraryEvent item = snapshot.Events[i];
                    if (item == null)
                        continue;
                    builder.Append('\n').Append(item.Path).Append('|').Append(item.Guid).Append('|').Append(item.Is3D).Append('|').Append(item.IsLoop)
                        .Append('|').Append(item.MinDistance.ToString(CultureInfo.InvariantCulture)).Append('|').Append(item.MaxDistance.ToString(CultureInfo.InvariantCulture));
                    if (item.Parameters != null)
                    {
                        for (int p = 0; p < item.Parameters.Count; p++)
                            builder.Append('|').Append(item.Parameters[p]?.Name);
                    }
                }
            }
            return CharacterPackageHashUtility.ComputeTextSha256(builder.ToString());
        }

        public static AudioSemanticManifestEntry ToLegacyEntry(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent)
        {
            if (cue == null)
                return null;
            return new AudioSemanticManifestEntry
            {
                AudioCueId = cue.AudioCueId ?? string.Empty,
                AudioEventDefinitionId = cue.EventDefinitionId ?? string.Empty,
                FmodEventPath = audioEvent != null ? audioEvent.FmodEventPath ?? string.Empty : string.Empty,
                FmodEventGuid = audioEvent != null ? audioEvent.FmodEventGuid ?? string.Empty : string.Empty,
                DisplayName = cue.DisplayName ?? string.Empty,
                Purpose = cue.Purpose ?? string.Empty,
                Context = cue.Context ?? string.Empty,
                Tags = cue.Tags ?? new List<string>(),
                SourcePreviewPath = cue.SourcePreviewPath ?? string.Empty,
                AuditionStatus = cue.AuditionEvidence != null ? cue.AuditionEvidence.Status ?? AudioSemanticAuditionStatuses.Needed : AudioSemanticAuditionStatuses.Needed,
                ReplacementContract = cue.ReplacementContract ?? new AudioReplacementContract(),
                Metadata = cue.Metadata ?? new Dictionary<string, string>()
            };
        }

        public static int AllocateCueId(AudioSemanticManifestDocument manifest)
        {
            int next = manifest.IdAllocation.NextCueId;
            int value = Allocate(ref next, manifest.IdAllocation.RetiredCueIds);
            manifest.IdAllocation.NextCueId = next;
            return value;
        }

        public static int AllocateEventId(AudioSemanticManifestDocument manifest)
        {
            int next = manifest.IdAllocation.NextEventId;
            int value = Allocate(ref next, manifest.IdAllocation.RetiredEventIds);
            manifest.IdAllocation.NextEventId = next;
            return value;
        }

        public static int AllocateBusId(AudioSemanticManifestDocument manifest)
        {
            int next = manifest.IdAllocation.NextBusId;
            int value = Allocate(ref next, manifest.IdAllocation.RetiredBusIds);
            manifest.IdAllocation.NextBusId = next;
            return value;
        }

        public static int AllocateParameterId(AudioSemanticManifestDocument manifest)
        {
            int next = manifest.IdAllocation.NextParameterId;
            int value = Allocate(ref next, manifest.IdAllocation.RetiredParameterIds);
            manifest.IdAllocation.NextParameterId = next;
            return value;
        }

        private static AudioEventAuthoringDefinition FromLegacyEvent(
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestEntry entry,
            string eventId,
            AuthoringFmodAudioLibraryEvent snapshotEvent)
        {
            var result = new AudioEventAuthoringDefinition
            {
                Id = eventId,
                RuntimeEventId = AllocateEventId(manifest),
                DisplayName = entry.DisplayName ?? eventId,
                FmodEventPath = entry.FmodEventPath ?? string.Empty,
                FmodEventGuid = entry.FmodEventGuid ?? string.Empty,
                BusId = "audio.bus.master"
            };
            ApplySnapshotFacts(manifest, result, snapshotEvent);
            return result;
        }

        private static void SynchronizeEventFacts(
            AudioSemanticManifestDocument manifest,
            AudioEventAuthoringDefinition audioEvent,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            AuthoringFmodAudioLibraryEvent snapshotEvent = AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid);
            if (snapshotEvent != null)
                ApplySnapshotFacts(manifest, audioEvent, snapshotEvent);
        }

        private static void ApplySnapshotFacts(
            AudioSemanticManifestDocument manifest,
            AudioEventAuthoringDefinition audioEvent,
            AuthoringFmodAudioLibraryEvent snapshotEvent)
        {
            if (audioEvent == null || snapshotEvent == null)
                return;
            audioEvent.FmodEventPath = snapshotEvent.Path ?? audioEvent.FmodEventPath;
            audioEvent.FmodEventGuid = snapshotEvent.Guid ?? audioEvent.FmodEventGuid;
            audioEvent.Kind = snapshotEvent.Kind ?? "Event";
            audioEvent.Is3D = snapshotEvent.Is3D;
            audioEvent.IsLoop = snapshotEvent.IsLoop;
            audioEvent.MinDistance = snapshotEvent.MinDistance;
            audioEvent.MaxDistance = snapshotEvent.MaxDistance;
            audioEvent.Parameters ??= new List<AudioParameterAuthoringDefinition>();
            for (int i = 0; snapshotEvent.Parameters != null && i < snapshotEvent.Parameters.Count; i++)
            {
                AuthoringFmodAudioLibraryParameter snapshotParameter = snapshotEvent.Parameters[i];
                if (snapshotParameter == null || string.IsNullOrWhiteSpace(snapshotParameter.Name))
                    continue;
                AudioParameterAuthoringDefinition parameter = FindParameter(audioEvent, snapshotParameter.Name);
                if (parameter == null)
                {
                    parameter = new AudioParameterAuthoringDefinition
                    {
                        Name = snapshotParameter.Name,
                        RuntimeParameterId = AllocateParameterId(manifest)
                    };
                    audioEvent.Parameters.Add(parameter);
                }
                parameter.Kind = snapshotParameter.Kind ?? "Continuous";
                parameter.DefaultValue = snapshotParameter.DefaultValue;
                parameter.MinValue = snapshotParameter.MinValue;
                parameter.MaxValue = snapshotParameter.MaxValue;
            }
        }

        private static void ValidateBuses(
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestValidationReport report,
            HashSet<string> stableIds,
            HashSet<int> runtimeIds)
        {
            for (int i = 0; i < manifest.Buses.Count; i++)
            {
                AudioBusAuthoringDefinition bus = manifest.Buses[i];
                if (bus == null)
                    continue;
                if (string.IsNullOrWhiteSpace(bus.Id) || !stableIds.Add(bus.Id))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_BUS_ID_DUPLICATE", bus?.Id, "buses/" + i, "Bus stable id is missing or duplicated.");
                if (!IsRuntimeId(bus.RuntimeBusId) || !runtimeIds.Add(bus.RuntimeBusId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_BUS_RUNTIME_ID_DUPLICATE", bus.Id, "buses/" + i + "/runtimeBusId", "Bus runtime id must be unique and stay in 500000-599999.");
            }
        }

        private static void ValidateEvents(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AudioSemanticManifestValidationReport report,
            HashSet<string> stableIds,
            HashSet<int> runtimeIds,
            HashSet<string> busIds,
            Dictionary<int, string> parameterRuntimeIds)
        {
            for (int i = 0; i < manifest.EventDefinitions.Count; i++)
            {
                AudioEventAuthoringDefinition audioEvent = manifest.EventDefinitions[i];
                if (audioEvent == null)
                    continue;
                if (string.IsNullOrWhiteSpace(audioEvent.Id) || !stableIds.Add(audioEvent.Id))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_EVENT_ID_DUPLICATE", audioEvent.Id, "eventDefinitions/" + i, "Event Definition stable id is missing or duplicated.");
                if (!IsRuntimeId(audioEvent.RuntimeEventId) || !runtimeIds.Add(audioEvent.RuntimeEventId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_EVENT_RUNTIME_ID_DUPLICATE", audioEvent.Id, "eventDefinitions/" + i + "/runtimeEventId", "Event runtime id must be unique and stay in 500000-599999.");
                if (!busIds.Contains(audioEvent.BusId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_EVENT_BUS_MISSING", audioEvent.Id, "eventDefinitions/" + i + "/busId", "Event Definition references a missing Bus.");
                if (AudioSemanticManifestValidator.FindEvent(snapshot, audioEvent.FmodEventPath, audioEvent.FmodEventGuid) == null)
                    Add(report, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing, audioEvent.Id, "eventDefinitions/" + i, "Event Definition is not present in the current FMOD snapshot.");
                for (int p = 0; p < audioEvent.Parameters.Count; p++)
                {
                    AudioParameterAuthoringDefinition parameter = audioEvent.Parameters[p];
                    if (parameter == null)
                        continue;
                    if (!IsRuntimeId(parameter.RuntimeParameterId))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_PARAMETER_RUNTIME_ID_INVALID", audioEvent.Id, "eventDefinitions/" + i + "/parameters/" + p, "Parameter runtime id must stay in 500000-599999.");
                    }
                    else if (parameterRuntimeIds.TryGetValue(parameter.RuntimeParameterId, out string knownName)
                        && !string.Equals(knownName, parameter.Name, StringComparison.Ordinal))
                    {
                        Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_PARAMETER_RUNTIME_ID_DUPLICATE", audioEvent.Id, "eventDefinitions/" + i + "/parameters/" + p, "A parameter runtime id may only be reused for the same parameter name.");
                    }
                    else
                    {
                        parameterRuntimeIds[parameter.RuntimeParameterId] = parameter.Name;
                    }
                }
            }
        }

        private static void ValidateCues(
            AudioSemanticManifestDocument manifest,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AudioSemanticManifestValidationReport report,
            HashSet<string> stableIds,
            HashSet<int> runtimeIds,
            HashSet<string> eventIds)
        {
            string snapshotHash = ComputeSnapshotHash(snapshot);
            for (int i = 0; i < manifest.Cues.Count; i++)
            {
                AudioCueAuthoringDefinition cue = manifest.Cues[i];
                if (cue == null)
                    continue;
                if (string.IsNullOrWhiteSpace(cue.AudioCueId) || !stableIds.Add(cue.AudioCueId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.AudioSemanticCueDuplicate, cue.AudioCueId, "cues/" + i, "Cue id is missing or duplicated.");
                if (!IsRuntimeId(cue.RuntimeCueId) || !runtimeIds.Add(cue.RuntimeCueId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_CUE_RUNTIME_ID_DUPLICATE", cue.AudioCueId, "cues/" + i + "/runtimeCueId", "Cue runtime id must be unique and stay in 500000-599999.");
                if (!eventIds.Contains(cue.EventDefinitionId))
                    Add(report, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.AudioSemanticEventMissing, cue.AudioCueId, "cues/" + i + "/eventDefinitionId", "Cue references a missing Event Definition.");
                if (!IsLifecycle(cue.Lifecycle))
                    Add(report, CharacterAuthoringValidationSeverity.Error, "AUTH_AUDIO_CUE_LIFECYCLE_INVALID", cue.AudioCueId, "cues/" + i + "/lifecycle", "Cue lifecycle must be Draft, Active, or Archived.");
                if (string.Equals(cue.Lifecycle, AudioCueLifecycles.Active, StringComparison.Ordinal))
                {
                    AudioEventAuthoringDefinition audioEvent = FindEventDefinition(manifest, cue.EventDefinitionId);
                    if (!HasValidAuditionEvidence(cue, audioEvent, snapshotHash))
                        Add(report, CharacterAuthoringValidationSeverity.Info, AuthoringResourceDiagnosticCodes.AudioSemanticAuditionRecommended, cue.AudioCueId, "cues/" + i + "/auditionEvidence", "Active Cue has no current optional Unity/FMOD audition review.");
                    if (string.IsNullOrWhiteSpace(cue.DisplayName) || string.IsNullOrWhiteSpace(cue.Purpose) || string.IsNullOrWhiteSpace(cue.Context))
                        Add(report, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.AudioSemanticIncomplete, cue.AudioCueId, "cues/" + i, "Active Cue requires displayName, purpose, and context.");
                }
            }
            if (snapshot != null && snapshot.CacheStale)
                Add(report, CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.AudioSemanticSnapshotStale, string.Empty, "fmodSnapshot", "FMOD snapshot is stale; publication is blocked.");
        }

        private static void ValidateUiFeedbackCueSet(
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestValidationReport report)
        {
            if (manifest.Metadata == null ||
                !manifest.Metadata.TryGetValue(
                    UiFeedbackCueSetMetadataKey,
                    out string cueSet))
            {
                return;
            }
            if (!string.Equals(
                    cueSet,
                    UiFeedbackCueSetV1,
                    StringComparison.Ordinal))
            {
                Add(
                    report,
                    CharacterAuthoringValidationSeverity.Error,
                    "AUTH_AUDIO_UI_FEEDBACK_SET_VERSION_UNSUPPORTED",
                    string.Empty,
                    "metadata/" + UiFeedbackCueSetMetadataKey,
                    "UI feedback Cue set version must be v1.");
                return;
            }

            ValidateUiFeedbackCue(
                manifest,
                report,
                "ui.click",
                "Click");
            ValidateUiFeedbackCue(
                manifest,
                report,
                "ui.confirm",
                "Confirm");
            ValidateUiFeedbackCue(
                manifest,
                report,
                "ui.back",
                "Back");
            ValidateUiFeedbackCue(
                manifest,
                report,
                "ui.error",
                "Error");
        }

        private static void ValidateUiFeedbackCue(
            AudioSemanticManifestDocument manifest,
            AudioSemanticManifestValidationReport report,
            string cueId,
            string feedbackKind)
        {
            AudioCueAuthoringDefinition cue = FindCue(manifest, cueId);
            if (cue == null)
            {
                Add(
                    report,
                    CharacterAuthoringValidationSeverity.Error,
                    "AUTH_AUDIO_UI_FEEDBACK_SET_INCOMPLETE",
                    cueId,
                    "cues",
                    "UI feedback Cue set v1 requires " + cueId + ".");
                return;
            }

            if (!string.Equals(cue.PlayMode, "OneShot", StringComparison.Ordinal) ||
                !string.Equals(cue.MissingPolicy, "Mute", StringComparison.Ordinal))
            {
                Add(
                    report,
                    CharacterAuthoringValidationSeverity.Error,
                    "AUTH_AUDIO_UI_FEEDBACK_CUE_CONTRACT_INVALID",
                    cueId,
                    "cues/" + cueId,
                    "UI feedback Cue must use OneShot and MissingPolicy=Mute.");
            }

            AudioEventAuthoringDefinition audioEvent = FindEventDefinition(
                manifest,
                cue.EventDefinitionId);
            if (audioEvent == null)
                return;

            bool kindMatches =
                audioEvent.Metadata != null &&
                audioEvent.Metadata.TryGetValue(
                    "uiFeedback",
                    out string mappedKind) &&
                string.Equals(
                    mappedKind,
                    feedbackKind,
                    StringComparison.Ordinal);
            if (audioEvent.Is3D ||
                audioEvent.IsLoop ||
                !string.Equals(
                    audioEvent.Kind,
                    "Event",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    audioEvent.BusId,
                    "audio.bus.ui",
                    StringComparison.Ordinal) ||
                !kindMatches)
            {
                Add(
                    report,
                    CharacterAuthoringValidationSeverity.Error,
                    "AUTH_AUDIO_UI_FEEDBACK_EVENT_CONTRACT_INVALID",
                    cueId,
                    "eventDefinitions/" + cue.EventDefinitionId,
                    "UI feedback Event must be a 2D non-loop Event on audio.bus.ui with matching uiFeedback metadata.");
            }
        }

        private static bool HasValidAuditionEvidence(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent, string snapshotHash)
        {
            AudioCueAuditionEvidence evidence = cue?.AuditionEvidence;
            return evidence != null
                && string.Equals(evidence.Status, AudioSemanticAuditionStatuses.Approved, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(evidence.PreviewRequestId)
                && !string.IsNullOrWhiteSpace(evidence.ReviewedAtUtc)
                && string.Equals(evidence.EventBindingHash, ComputeBindingHash(cue, audioEvent), StringComparison.OrdinalIgnoreCase)
                && string.Equals(evidence.SnapshotHash, snapshotHash, StringComparison.OrdinalIgnoreCase);
        }

        private static AudioRuntimeCueDefinition BuildRuntimeCue(AudioCueAuthoringDefinition cue, AudioEventAuthoringDefinition audioEvent)
        {
            var result = new AudioRuntimeCueDefinition
            {
                Id = cue.RuntimeCueId,
                AudioCueId = cue.AudioCueId,
                Name = cue.DisplayName,
                EventId = audioEvent.RuntimeEventId,
                EventDefinitionId = audioEvent.Id,
                PlayMode = cue.PlayMode,
                MissingPolicy = cue.MissingPolicy,
                Labels = new List<string>(cue.Tags ?? new List<string>())
            };
            for (int i = 0; i < cue.DefaultParameters.Count; i++)
            {
                AudioCueDefaultParameter source = cue.DefaultParameters[i];
                AudioParameterAuthoringDefinition parameter = FindParameter(audioEvent, source?.Name);
                if (source != null && parameter != null)
                    result.DefaultParameters.Add(new AudioRuntimeParameterValue { ParameterId = parameter.RuntimeParameterId, Value = source.Value });
            }
            return result;
        }

        private static AudioRuntimeEventDefinition BuildRuntimeEvent(AudioEventAuthoringDefinition source, AudioBusAuthoringDefinition bus)
        {
            var result = new AudioRuntimeEventDefinition
            {
                Id = source.RuntimeEventId,
                StableId = source.Id,
                Name = source.DisplayName,
                FmodEventPath = source.FmodEventPath,
                FmodEventGuid = source.FmodEventGuid,
                Kind = source.Kind,
                BusId = bus != null ? bus.RuntimeBusId : 0,
                Is3D = source.Is3D,
                IsLoop = source.IsLoop,
                MaxDistance = source.MaxDistance
            };
            for (int i = 0; i < source.Parameters.Count; i++)
            {
                AudioParameterAuthoringDefinition parameter = source.Parameters[i];
                if (parameter == null)
                    continue;
                result.Parameters.Add(new AudioRuntimeParameterDefinition
                {
                    Id = parameter.RuntimeParameterId,
                    Name = parameter.Name,
                    Kind = parameter.Kind,
                    DefaultValue = parameter.DefaultValue,
                    MinValue = parameter.MinValue,
                    MaxValue = parameter.MaxValue
                });
            }
            return result;
        }

        private static AudioRuntimeBusDefinition BuildRuntimeBus(AudioBusAuthoringDefinition source)
        {
            return new AudioRuntimeBusDefinition
            {
                Id = source.RuntimeBusId,
                Name = source.DisplayName,
                FmodBusPath = source.FmodBusPath,
                FmodVcaPath = source.FmodVcaPath,
                DefaultVolume = source.DefaultVolume,
                DefaultMuted = source.DefaultMuted
            };
        }

        private static AudioBusAuthoringDefinition FindBusDefinition(AudioSemanticManifestDocument manifest, string id)
        {
            if (manifest?.Buses == null)
                return null;
            for (int i = 0; i < manifest.Buses.Count; i++)
            {
                AudioBusAuthoringDefinition item = manifest.Buses[i];
                if (item != null && string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private static AudioParameterAuthoringDefinition FindParameter(AudioEventAuthoringDefinition audioEvent, string name)
        {
            if (audioEvent?.Parameters == null || string.IsNullOrWhiteSpace(name))
                return null;
            for (int i = 0; i < audioEvent.Parameters.Count; i++)
            {
                AudioParameterAuthoringDefinition item = audioEvent.Parameters[i];
                if (item != null && string.Equals(item.Name, name, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private static int Allocate(ref int next, List<int> retired)
        {
            if (next < MinimumRuntimeId)
                next = MinimumRuntimeId;
            var retiredSet = new HashSet<int>(retired ?? new List<int>());
            while (retiredSet.Contains(next) && next <= MaximumRuntimeId)
                next++;
            if (next > MaximumRuntimeId)
                throw new InvalidOperationException("Audio runtime id space 500000-599999 is exhausted.");
            return next++;
        }

        private static int Advance(int next, int used)
        {
            if (used >= next)
                next = used + 1;
            if (next < MinimumRuntimeId)
                next = MinimumRuntimeId;
            return next;
        }

        private static bool IsRuntimeId(int value) => value >= MinimumRuntimeId && value <= MaximumRuntimeId;
        private static bool IsLifecycle(string value) => string.Equals(value, AudioCueLifecycles.Draft, StringComparison.Ordinal)
            || string.Equals(value, AudioCueLifecycles.Active, StringComparison.Ordinal)
            || string.Equals(value, AudioCueLifecycles.Archived, StringComparison.Ordinal);
        private static bool HasErrors(List<AuthoringResourceDiagnostic> diagnostics)
        {
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
                if (diagnostics[i] != null && diagnostics[i].Severity == CharacterAuthoringValidationSeverity.Error)
                    return true;
            return false;
        }
        private static void Add(AudioSemanticManifestValidationReport report, CharacterAuthoringValidationSeverity severity, string code, string id, string field, string message)
        {
            report.Diagnostics.Add(new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                RuntimeResourceKey = id ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "audioSemanticManifest",
                SourceField = field ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = "在 Audio Workbench 修正后重新校验。"
            });
        }
        private static string Trim(string value) => value == null ? string.Empty : value.Trim();
    }
}
