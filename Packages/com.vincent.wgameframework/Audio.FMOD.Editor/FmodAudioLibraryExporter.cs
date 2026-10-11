using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Audio.FMOD.Editor
{
    public static class FmodAudioLibraryExporter
    {
        public const string DefaultExportPath = "Assets/MxFrameworkGenerated/Audio/fmod_audio_library.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        [MenuItem("MxFramework/Audio/Export FMOD Audio Library", priority = 1002)]
        public static void ExportCurrentProjectMenu()
        {
            FmodAudioLibrarySnapshot snapshot = CaptureCurrentProject();
            WriteSnapshot(DefaultExportPath, snapshot);

            string summary = "Exported FMOD audio library to " + DefaultExportPath + ". events=" + snapshot.events.Count + ", banks=" + snapshot.banks.Count + ", diagnostics=" + snapshot.diagnostics.Count + ".";
            if (HasDiagnostic(snapshot, "Error"))
            {
                Debug.LogError("[MxAudio] " + summary);
            }
            else if (HasDiagnostic(snapshot, "Warning"))
            {
                Debug.LogWarning("[MxAudio] " + summary);
            }
            else
            {
                Debug.Log("[MxAudio] " + summary);
            }

            AssetDatabase.Refresh();
        }

        public static FmodAudioLibrarySnapshot CaptureCurrentProject()
        {
            return CreateSnapshot(FmodEventManagerAudioLibrarySource.Capture(), DateTime.UtcNow);
        }

        public static FmodAudioLibrarySnapshot CreateSnapshot(FmodAudioLibrarySourceData source, DateTime generatedAtUtc)
        {
            var snapshot = new FmodAudioLibrarySnapshot
            {
                generatedAtUtc = FormatUtc(generatedAtUtc),
                source = source != null ? source.Source ?? string.Empty : "Unavailable",
                cacheValid = source != null && source.IsCacheValid,
                cacheTimeUtc = source != null && source.CacheTimeUtc != DateTime.MinValue ? FormatUtc(source.CacheTimeUtc) : string.Empty
            };

            if (source == null || !source.IsAvailable)
            {
                AddDiagnostic(
                    snapshot,
                    "Error",
                    "FMOD_UNAVAILABLE",
                    source != null && !string.IsNullOrWhiteSpace(source.UnavailableReason) ? source.UnavailableReason : "FMOD editor cache is unavailable.",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Install FMOD Unity Integration, configure FMOD Settings, then refresh banks.");
                return snapshot;
            }

            if (!source.IsCacheValid)
            {
                AddDiagnostic(
                    snapshot,
                    "Warning",
                    "FMOD_CACHE_STALE",
                    "FMOD editor cache is invalid or empty.",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Run FMOD/Refresh Banks before exporting the audio library.");
            }

            CopyBanks(source, snapshot);
            CopyGlobalParameters(source, snapshot);
            CopyEvents(source, snapshot);
            ValidateCacheFreshness(source, snapshot);
            ValidateSnapshot(snapshot);
            return snapshot;
        }

        public static string ToJson(FmodAudioLibrarySnapshot snapshot)
        {
            return JsonConvert.SerializeObject(snapshot, JsonSettings);
        }

        public static void WriteSnapshot(string assetPath, FmodAudioLibrarySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("Export path is empty.", nameof(assetPath));
            }

            string fullPath = Path.GetFullPath(assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, ToJson(snapshot ?? new FmodAudioLibrarySnapshot()));
        }

        private static void CopyBanks(FmodAudioLibrarySourceData source, FmodAudioLibrarySnapshot snapshot)
        {
            for (int i = 0; i < source.Banks.Count; i++)
            {
                FmodAudioLibrarySourceBank sourceBank = source.Banks[i];
                var bank = new FmodAudioLibraryBank
                {
                    name = sourceBank.Name ?? string.Empty,
                    path = sourceBank.Path ?? string.Empty,
                    studioPath = sourceBank.StudioPath ?? string.Empty,
                    lastModifiedUtc = sourceBank.LastModifiedUtc != DateTime.MinValue ? FormatUtc(sourceBank.LastModifiedUtc) : string.Empty
                };

                for (int sizeIndex = 0; sizeIndex < sourceBank.FileSizes.Count; sizeIndex++)
                {
                    FmodAudioLibrarySourceBankFileSize sourceSize = sourceBank.FileSizes[sizeIndex];
                    bank.fileSizes.Add(new FmodAudioLibraryBankFileSize
                    {
                        platform = sourceSize.Platform ?? string.Empty,
                        sizeBytes = sourceSize.SizeBytes
                    });
                }

                snapshot.banks.Add(bank);
            }
        }

        private static void CopyGlobalParameters(FmodAudioLibrarySourceData source, FmodAudioLibrarySnapshot snapshot)
        {
            for (int i = 0; i < source.GlobalParameters.Count; i++)
            {
                snapshot.globalParameters.Add(CopyParameter(source.GlobalParameters[i]));
            }
        }

        private static void CopyEvents(FmodAudioLibrarySourceData source, FmodAudioLibrarySnapshot snapshot)
        {
            for (int i = 0; i < source.Events.Count; i++)
            {
                FmodAudioLibrarySourceEvent sourceEvent = source.Events[i];
                var audioEvent = new FmodAudioLibraryEvent
                {
                    path = sourceEvent.Path ?? string.Empty,
                    guid = sourceEvent.Guid ?? string.Empty,
                    kind = sourceEvent.Kind ?? "Event",
                    is3D = sourceEvent.Is3D,
                    isLoop = sourceEvent.IsLoop,
                    isStream = sourceEvent.IsStream,
                    minDistance = sourceEvent.MinDistance,
                    maxDistance = sourceEvent.MaxDistance,
                    lengthMs = sourceEvent.LengthMs
                };

                for (int bankIndex = 0; bankIndex < sourceEvent.Banks.Count; bankIndex++)
                {
                    audioEvent.banks.Add(sourceEvent.Banks[bankIndex] ?? string.Empty);
                }

                for (int parameterIndex = 0; parameterIndex < sourceEvent.Parameters.Count; parameterIndex++)
                {
                    audioEvent.parameters.Add(CopyParameter(sourceEvent.Parameters[parameterIndex]));
                }

                snapshot.events.Add(audioEvent);
            }
        }

        private static FmodAudioLibraryParameter CopyParameter(FmodAudioLibrarySourceParameter source)
        {
            var parameter = new FmodAudioLibraryParameter
            {
                name = source.Name ?? string.Empty,
                studioPath = source.StudioPath ?? string.Empty,
                idData1 = source.IdData1,
                idData2 = source.IdData2,
                kind = source.Kind ?? "Continuous",
                defaultValue = source.DefaultValue,
                minValue = source.MinValue,
                maxValue = source.MaxValue,
                isGlobal = source.IsGlobal
            };

            for (int i = 0; i < source.Labels.Count; i++)
            {
                parameter.labels.Add(source.Labels[i] ?? string.Empty);
            }

            return parameter;
        }

        private static void ValidateCacheFreshness(FmodAudioLibrarySourceData source, FmodAudioLibrarySnapshot snapshot)
        {
            if (source.CacheTimeUtc == DateTime.MinValue)
            {
                return;
            }

            for (int i = 0; i < source.Banks.Count; i++)
            {
                FmodAudioLibrarySourceBank bank = source.Banks[i];
                if (bank.LastModifiedUtc > source.CacheTimeUtc)
                {
                    snapshot.cacheStale = true;
                    AddDiagnostic(
                        snapshot,
                        "Warning",
                        "FMOD_CACHE_STALE",
                        "FMOD bank '" + bank.Name + "' is newer than the editor cache.",
                        string.Empty,
                        string.Empty,
                        bank.Name,
                        string.Empty,
                        "Run FMOD/Refresh Banks, then export the audio library again.");
                }
            }
        }

        private static void ValidateSnapshot(FmodAudioLibrarySnapshot snapshot)
        {
            if (snapshot.events.Count == 0)
            {
                AddDiagnostic(
                    snapshot,
                    "Warning",
                    "FMOD_EVENT_CACHE_EMPTY",
                    "FMOD editor cache contains no events.",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "Build banks in FMOD Studio and run FMOD/Refresh Banks.");
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < snapshot.events.Count; i++)
            {
                FmodAudioLibraryEvent audioEvent = snapshot.events[i];
                if (string.IsNullOrWhiteSpace(audioEvent.path) || string.IsNullOrWhiteSpace(audioEvent.guid))
                {
                    AddDiagnostic(
                        snapshot,
                        "Error",
                        "RES_LIBRARY_FMOD_GUID_PATH_MISMATCH",
                        "FMOD event is missing path or guid.",
                        audioEvent.path,
                        audioEvent.guid,
                        string.Empty,
                        string.Empty,
                        "Refresh FMOD banks and inspect the event cache.");
                }

                if (!string.IsNullOrWhiteSpace(audioEvent.path) && !paths.Add(audioEvent.path))
                {
                    AddDiagnostic(
                        snapshot,
                        "Error",
                        "RES_LIBRARY_FMOD_GUID_PATH_MISMATCH",
                        "Duplicate FMOD event path in exported cache.",
                        audioEvent.path,
                        audioEvent.guid,
                        string.Empty,
                        string.Empty,
                        "Resolve duplicate FMOD event paths before authoring selection.");
                }

                if (!string.IsNullOrWhiteSpace(audioEvent.guid) && !guids.Add(audioEvent.guid))
                {
                    AddDiagnostic(
                        snapshot,
                        "Error",
                        "RES_LIBRARY_FMOD_GUID_PATH_MISMATCH",
                        "Duplicate FMOD event guid in exported cache.",
                        audioEvent.path,
                        audioEvent.guid,
                        string.Empty,
                        string.Empty,
                        "Refresh FMOD banks and inspect renamed or duplicated events.");
                }

                if (audioEvent.banks.Count == 0)
                {
                    AddDiagnostic(
                        snapshot,
                        "Error",
                        "RES_LIBRARY_FMOD_BANK_MISSING",
                        "FMOD event has no bank binding.",
                        audioEvent.path,
                        audioEvent.guid,
                        string.Empty,
                        string.Empty,
                        "Rebuild banks and refresh the FMOD editor cache.");
                }
            }
        }

        private static void AddDiagnostic(
            FmodAudioLibrarySnapshot snapshot,
            string severity,
            string code,
            string message,
            string eventPath,
            string eventGuid,
            string bankName,
            string parameterName,
            string suggestedFix)
        {
            snapshot.diagnostics.Add(new FmodAudioLibraryDiagnostic
            {
                severity = severity ?? string.Empty,
                code = code ?? string.Empty,
                message = message ?? string.Empty,
                eventPath = eventPath ?? string.Empty,
                eventGuid = eventGuid ?? string.Empty,
                bankName = bankName ?? string.Empty,
                parameterName = parameterName ?? string.Empty,
                suggestedFix = suggestedFix ?? string.Empty
            });
        }

        private static bool HasDiagnostic(FmodAudioLibrarySnapshot snapshot, string severity)
        {
            for (int i = 0; i < snapshot.diagnostics.Count; i++)
            {
                if (string.Equals(snapshot.diagnostics[i].severity, severity, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value.ToString("O") : value.ToUniversalTime().ToString("O");
        }
    }
}
