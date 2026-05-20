using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Authoring
{
    public sealed class FmodAudioLibraryAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.Fmod; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            AuthoringFmodAudioLibrarySnapshotDocument snapshot = context != null ? context.FmodAudioLibrarySnapshot : null;
            if (snapshot == null)
            {
                return new AuthoringResourceProviderDescriptor
                {
                    ProviderId = ProviderId,
                    DisplayName = "FMOD Audio Library",
                    SourceKind = AuthoringResourceSourceKind.FmodLibrary,
                    Available = false,
                    Status = "Unavailable",
                    DiagnosticCode = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    Message = "FMOD audio library snapshot is not available. Export FMOD audio metadata before selecting audio events."
                };
            }

            AuthoringFmodAudioLibraryDiagnostic unavailable = FindSnapshotDiagnostic(snapshot, "FMOD_UNAVAILABLE");
            if (unavailable != null)
            {
                return new AuthoringResourceProviderDescriptor
                {
                    ProviderId = ProviderId,
                    DisplayName = "FMOD Audio Library",
                    SourceKind = AuthoringResourceSourceKind.FmodLibrary,
                    Available = false,
                    Status = "Unavailable",
                    DiagnosticCode = AuthoringResourceDiagnosticCodes.FmodUnavailable,
                    Message = string.IsNullOrWhiteSpace(unavailable.Message) ? "FMOD audio library is unavailable." : unavailable.Message
                };
            }

            if (snapshot.CacheStale || FindSnapshotDiagnostic(snapshot, "FMOD_CACHE_STALE") != null)
            {
                return new AuthoringResourceProviderDescriptor
                {
                    ProviderId = ProviderId,
                    DisplayName = "FMOD Audio Library",
                    SourceKind = AuthoringResourceSourceKind.FmodLibrary,
                    Available = true,
                    Status = "Stale",
                    DiagnosticCode = AuthoringResourceDiagnosticCodes.FmodSnapshotStale,
                    Message = "FMOD audio library snapshot is stale. Refresh banks and export the snapshot again."
                };
            }

            bool hasErrors = HasSnapshotDiagnosticSeverity(snapshot, "Error");
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "FMOD Audio Library",
                SourceKind = AuthoringResourceSourceKind.FmodLibrary,
                Available = true,
                Status = hasErrors ? "ReadyWithDiagnostics" : "Ready",
                DiagnosticCode = hasErrors ? AuthoringResourceDiagnosticCodes.FmodGuidPathMismatch : string.Empty,
                Message = hasErrors ? "FMOD audio library has diagnostics. Inspect individual events before selecting them." : string.Empty
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            return FromSnapshot(context != null ? context.FmodAudioLibrarySnapshot : null, context);
        }

        public static AuthoringResourceCollection FromSnapshot(
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AuthoringResourceProviderContext context)
        {
            var provider = new FmodAudioLibraryAuthoringResourceProvider();
            if (context != null && snapshot != null && context.FmodAudioLibrarySnapshot == null)
                context.FmodAudioLibrarySnapshot = snapshot;

            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "fmod"
            };

            if (context != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packageId", context.PackageId);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "packagePath", context.PackagePath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "projectRootPath", context.ProjectRootPath);
                AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "fmodAudioLibrarySnapshotPath", context.FmodAudioLibrarySnapshotPath);
            }

            AddSnapshotMetadata(collection, snapshot);
            AuthoringResourceProviderDescriptor descriptor = provider.Describe(context);
            collection.Providers.Add(descriptor);

            if (snapshot == null)
            {
                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = CharacterAuthoringValidationSeverity.Warning,
                    Code = AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                    ProviderId = provider.ProviderId,
                    Message = descriptor.Message,
                    SuggestedFix = "Export FMOD audio metadata from Unity, then refresh the Resource Manager."
                });
                return collection;
            }

            AddSnapshotDiagnostics(collection, snapshot);

            if (snapshot.Events != null)
            {
                for (int i = 0; i < snapshot.Events.Count; i++)
                {
                    AuthoringFmodAudioLibraryEvent audioEvent = snapshot.Events[i];
                    if (audioEvent != null)
                        collection.Items.Add(FromEvent(audioEvent, snapshot, context));
                }
            }

            return collection;
        }

        private static AuthoringResourceItem FromEvent(
            AuthoringFmodAudioLibraryEvent audioEvent,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AuthoringResourceProviderContext context)
        {
            string providerKey = AuthoringResourceProviderUtilities.FirstNonEmpty(audioEvent.Guid, audioEvent.Path);
            string eventStableId = "fmod.event." + AuthoringResourceProviderUtilities.SanitizeStableSegment(NormalizeFmodPath(audioEvent.Path, "event"));
            string audioEventDefinitionId = "audio.event." + AuthoringResourceProviderUtilities.SanitizeStableSegment(NormalizeFmodPath(audioEvent.Path, "event"));
            string audioCueId = "audio.cue." + AuthoringResourceProviderUtilities.SanitizeStableSegment(NormalizeFmodPath(audioEvent.Path, "event"));
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.Fmod, eventStableId, providerKey),
                StableId = eventStableId,
                DisplayName = GetFmodDisplayName(audioEvent.Path, eventStableId),
                Kind = CharacterPackageResourceTypeIds.Audio,
                Usage = CharacterPackageResourceUsageIds.AudioCue,
                SourceProviderId = AuthoringResourceProviderIds.Fmod,
                SourceKind = AuthoringResourceSourceKind.FmodLibrary,
                BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
                ImportStatus = AuthoringResourceImportStatus.Clean,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.AudioCueOnly
            };

            AddEventMetadata(item, audioEvent, snapshot, context, audioEventDefinitionId, audioCueId);
            AddEventBindings(item, audioEvent, audioEventDefinitionId, audioCueId);
            AddEventDiagnostics(item, audioEvent, snapshot);
            return item;
        }

        private static void AddEventMetadata(
            AuthoringResourceItem item,
            AuthoringFmodAudioLibraryEvent audioEvent,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot,
            AuthoringResourceProviderContext context,
            string audioEventDefinitionId,
            string audioCueId)
        {
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "fmodEventPath", audioEvent.Path);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "fmodEventGuid", audioEvent.Guid);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "fmodEventKind", audioEvent.Kind);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "audioEventDefinitionId", audioEventDefinitionId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "audioCueId", audioCueId);
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "banks", Join(audioEvent.Banks));
            AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "parameters", JoinParameterNames(audioEvent.Parameters));
            if (context != null)
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "fmodAudioLibrarySnapshotPath", context.FmodAudioLibrarySnapshotPath);
            if (snapshot != null)
            {
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "generatedAtUtc", snapshot.GeneratedAtUtc);
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "cacheTimeUtc", snapshot.CacheTimeUtc);
                AuthoringResourceProviderUtilities.AddIfPresent(item.Metadata, "snapshotSource", snapshot.Source);
                item.Metadata["cacheValid"] = snapshot.CacheValid ? "true" : "false";
                item.Metadata["cacheStale"] = snapshot.CacheStale ? "true" : "false";
            }

            item.Metadata["is3D"] = audioEvent.Is3D ? "true" : "false";
            item.Metadata["isLoop"] = audioEvent.IsLoop ? "true" : "false";
            item.Metadata["isStream"] = audioEvent.IsStream ? "true" : "false";
            item.Metadata["minDistance"] = audioEvent.MinDistance.ToString(CultureInfo.InvariantCulture);
            item.Metadata["maxDistance"] = audioEvent.MaxDistance.ToString(CultureInfo.InvariantCulture);
            item.Metadata["lengthMs"] = audioEvent.LengthMs.ToString(CultureInfo.InvariantCulture);
        }

        private static void AddEventBindings(
            AuthoringResourceItem item,
            AuthoringFmodAudioLibraryEvent audioEvent,
            string audioEventDefinitionId,
            string audioCueId)
        {
            Dictionary<string, string> providerData = BuildBindingProviderData(audioEvent, audioEventDefinitionId, audioCueId);
            string keyKind = !string.IsNullOrWhiteSpace(audioEvent.Guid)
                ? AuthoringResourceBindingKeyKinds.FmodEventGuid
                : AuthoringResourceBindingKeyKinds.FmodEventPath;

            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioEventDefinition,
                BindingKeyKind = keyKind,
                DisplayValue = AuthoringResourceProviderUtilities.FirstNonEmpty(audioEvent.Path, audioEvent.Guid),
                IsPrimary = true,
                ProviderResourceKey = AuthoringResourceProviderUtilities.FirstNonEmpty(audioEvent.Path, audioEvent.Guid),
                FmodEventPath = audioEvent.Path ?? string.Empty,
                FmodEventGuid = audioEvent.Guid ?? string.Empty,
                AssetType = "FMOD.Event",
                ProviderData = new Dictionary<string, string>(providerData, StringComparer.Ordinal)
            });
            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.Fmod,
                BindingKind = AuthoringResourceBindingKind.AudioCue,
                BindingKeyKind = keyKind,
                DisplayValue = audioCueId,
                ProviderResourceKey = audioCueId,
                FmodEventPath = audioEvent.Path ?? string.Empty,
                FmodEventGuid = audioEvent.Guid ?? string.Empty,
                AssetType = "MxFramework.Audio.AudioCueDefinition",
                ProviderData = new Dictionary<string, string>(providerData, StringComparer.Ordinal)
            });
        }

        private static Dictionary<string, string> BuildBindingProviderData(
            AuthoringFmodAudioLibraryEvent audioEvent,
            string audioEventDefinitionId,
            string audioCueId)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["audioEventDefinitionId"] = audioEventDefinitionId ?? string.Empty,
                ["audioCueId"] = audioCueId ?? string.Empty,
                ["fmodEventPath"] = audioEvent != null ? audioEvent.Path ?? string.Empty : string.Empty,
                ["fmodEventGuid"] = audioEvent != null ? audioEvent.Guid ?? string.Empty : string.Empty,
                ["eventKind"] = audioEvent != null ? audioEvent.Kind ?? string.Empty : string.Empty,
                ["banks"] = audioEvent != null ? Join(audioEvent.Banks) : string.Empty,
                ["parameters"] = audioEvent != null ? JoinParameterNames(audioEvent.Parameters) : string.Empty
            };
            if (audioEvent != null)
            {
                data["is3D"] = audioEvent.Is3D ? "true" : "false";
                data["isLoop"] = audioEvent.IsLoop ? "true" : "false";
                data["isStream"] = audioEvent.IsStream ? "true" : "false";
            }

            return data;
        }

        private static void AddEventDiagnostics(
            AuthoringResourceItem item,
            AuthoringFmodAudioLibraryEvent audioEvent,
            AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (item == null || audioEvent == null)
                return;

            if (string.IsNullOrWhiteSpace(audioEvent.Path) || string.IsNullOrWhiteSpace(audioEvent.Guid))
            {
                item.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.FmodGuidPathMismatch,
                    item,
                    "FMOD event is missing path or guid.",
                    "Refresh FMOD banks and export the audio library again."));
            }

            if (audioEvent.Banks == null || audioEvent.Banks.Count == 0)
            {
                item.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.FmodBankMissing,
                    item,
                    "FMOD event has no bank binding.",
                    "Rebuild banks and refresh the FMOD editor cache."));
            }

            if (snapshot == null || snapshot.Diagnostics == null)
                return;

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                AuthoringFmodAudioLibraryDiagnostic diagnostic = snapshot.Diagnostics[i];
                if (diagnostic == null)
                    continue;
                if (!MatchesEventDiagnostic(diagnostic, audioEvent))
                    continue;

                item.Diagnostics.Add(CreateDiagnostic(
                    MapSeverity(diagnostic.Severity),
                    MapDiagnosticCode(diagnostic.Code),
                    item,
                    diagnostic.Message,
                    diagnostic.SuggestedFix));
            }
        }

        private static void AddSnapshotMetadata(AuthoringResourceCollection collection, AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (collection == null || snapshot == null)
                return;

            AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "fmodGeneratedAtUtc", snapshot.GeneratedAtUtc);
            AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "fmodSource", snapshot.Source);
            AuthoringResourceProviderUtilities.AddIfPresent(collection.Metadata, "fmodCacheTimeUtc", snapshot.CacheTimeUtc);
            collection.Metadata["fmodCacheValid"] = snapshot.CacheValid ? "true" : "false";
            collection.Metadata["fmodCacheStale"] = snapshot.CacheStale ? "true" : "false";
        }

        private static void AddSnapshotDiagnostics(AuthoringResourceCollection collection, AuthoringFmodAudioLibrarySnapshotDocument snapshot)
        {
            if (collection == null || snapshot == null || snapshot.Diagnostics == null)
                return;

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                AuthoringFmodAudioLibraryDiagnostic diagnostic = snapshot.Diagnostics[i];
                if (diagnostic == null)
                    continue;

                collection.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = MapSeverity(diagnostic.Severity),
                    Code = MapDiagnosticCode(diagnostic.Code),
                    ProviderId = AuthoringResourceProviderIds.Fmod,
                    Message = diagnostic.Message ?? string.Empty,
                    SuggestedFix = diagnostic.SuggestedFix ?? string.Empty,
                    SourceField = BuildDiagnosticSourceField(diagnostic)
                });
            }
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            AuthoringResourceItem item,
            string message,
            string suggestedFix)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = item != null ? item.ResourceId ?? string.Empty : string.Empty,
                ResourceStableId = item != null ? item.StableId ?? string.Empty : string.Empty,
                ProviderId = AuthoringResourceProviderIds.Fmod,
                SourceConfigKind = "audio",
                SourceField = "fmodEvent",
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static string MapDiagnosticCode(string code)
        {
            if (string.Equals(code, "FMOD_UNAVAILABLE", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodUnavailable;
            if (string.Equals(code, "FMOD_CACHE_STALE", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodSnapshotStale;
            if (string.Equals(code, "FMOD_EVENT_CACHE_EMPTY", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodEventMissing;
            if (string.Equals(code, "RES_LIBRARY_FMOD_GUID_PATH_MISMATCH", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodGuidPathMismatch;
            if (string.Equals(code, "RES_LIBRARY_FMOD_BANK_MISSING", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodBankMissing;
            if (string.Equals(code, "RES_LIBRARY_FMOD_PARAMETER_MISMATCH", StringComparison.Ordinal))
                return AuthoringResourceDiagnosticCodes.FmodParameterMismatch;

            return string.IsNullOrWhiteSpace(code) ? AuthoringResourceDiagnosticCodes.ProviderUnavailable : code;
        }

        private static CharacterAuthoringValidationSeverity MapSeverity(string severity)
        {
            if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase))
                return CharacterAuthoringValidationSeverity.Error;
            if (string.Equals(severity, "Info", StringComparison.OrdinalIgnoreCase))
                return CharacterAuthoringValidationSeverity.Info;
            return CharacterAuthoringValidationSeverity.Warning;
        }

        private static bool MatchesEventDiagnostic(AuthoringFmodAudioLibraryDiagnostic diagnostic, AuthoringFmodAudioLibraryEvent audioEvent)
        {
            if (diagnostic == null || audioEvent == null)
                return false;

            return (!string.IsNullOrWhiteSpace(diagnostic.EventPath) && string.Equals(diagnostic.EventPath, audioEvent.Path, StringComparison.Ordinal)) ||
                   (!string.IsNullOrWhiteSpace(diagnostic.EventGuid) && string.Equals(diagnostic.EventGuid, audioEvent.Guid, StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildDiagnosticSourceField(AuthoringFmodAudioLibraryDiagnostic diagnostic)
        {
            if (diagnostic == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(diagnostic.EventPath))
                return "events/" + diagnostic.EventPath;
            if (!string.IsNullOrWhiteSpace(diagnostic.BankName))
                return "banks/" + diagnostic.BankName;
            if (!string.IsNullOrWhiteSpace(diagnostic.ParameterName))
                return "parameters/" + diagnostic.ParameterName;
            return "fmodAudioLibrarySnapshot";
        }

        private static AuthoringFmodAudioLibraryDiagnostic FindSnapshotDiagnostic(AuthoringFmodAudioLibrarySnapshotDocument snapshot, string code)
        {
            if (snapshot == null || snapshot.Diagnostics == null)
                return null;

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                AuthoringFmodAudioLibraryDiagnostic diagnostic = snapshot.Diagnostics[i];
                if (diagnostic != null && string.Equals(diagnostic.Code, code, StringComparison.Ordinal))
                    return diagnostic;
            }

            return null;
        }

        private static bool HasSnapshotDiagnosticSeverity(AuthoringFmodAudioLibrarySnapshotDocument snapshot, string severity)
        {
            if (snapshot == null || snapshot.Diagnostics == null)
                return false;

            for (int i = 0; i < snapshot.Diagnostics.Count; i++)
            {
                AuthoringFmodAudioLibraryDiagnostic diagnostic = snapshot.Diagnostics[i];
                if (diagnostic != null && string.Equals(diagnostic.Severity, severity, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeFmodPath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
                return fallback ?? string.Empty;

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("event:/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("event:/".Length);
            else if (normalized.StartsWith("snapshot:/", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("snapshot:/".Length);
            return normalized;
        }

        private static string GetFmodDisplayName(string path, string fallback)
        {
            string normalized = NormalizeFmodPath(path, fallback);
            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < normalized.Length)
                return normalized.Substring(slash + 1);

            return string.IsNullOrWhiteSpace(normalized) ? fallback ?? string.Empty : normalized;
        }

        private static string Join(List<string> values)
        {
            if (values == null || values.Count == 0)
                return string.Empty;

            var result = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    result.Add(values[i]);
            }

            return string.Join(",", result);
        }

        private static string JoinParameterNames(List<AuthoringFmodAudioLibraryParameter> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return string.Empty;

            var result = new List<string>();
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i] != null && !string.IsNullOrWhiteSpace(parameters[i].Name))
                    result.Add(parameters[i].Name);
            }

            return string.Join(",", result);
        }
    }
}
