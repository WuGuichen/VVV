using System;
using System.Collections.Generic;
using MxFramework.Audio;
using Newtonsoft.Json;

namespace MxFramework.Audio.Config
{
    /// <summary>Startup-only JSON adapter for mx.audioRuntimeCatalog.v1.</summary>
    public static class AudioRuntimeCatalogJson
    {
        /// <summary>Parses a complete runtime catalog without merging another source.</summary>
        /// <remarks>LowFreqAlloc. JSON parsing and all lookup dictionaries are built during startup.</remarks>
        public static bool TryLoad(string json, out AudioDefinitionCatalog catalog, out string error)
        {
            catalog = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Audio runtime catalog JSON is empty.";
                return false;
            }

            try
            {
                AudioRuntimeCatalogDto dto = JsonConvert.DeserializeObject<AudioRuntimeCatalogDto>(json);
                if (dto == null || !string.Equals(dto.Format, "mx.audioRuntimeCatalog.v1", StringComparison.Ordinal))
                    throw new JsonSerializationException("Audio runtime catalog format must be mx.audioRuntimeCatalog.v1.");

                AudioBusDefinition[] buses = BuildBuses(dto.Buses);
                AudioEventDefinition[] events = BuildEvents(dto.Events);
                AudioCueDefinition[] cues = BuildCues(dto.Cues);
                catalog = new AudioDefinitionCatalog(buses, events, cues, dto.ManifestHash, dto.SnapshotHash);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static AudioBusDefinition[] BuildBuses(List<AudioRuntimeBusDto> source)
        {
            source ??= new List<AudioRuntimeBusDto>();
            var result = new AudioBusDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                AudioRuntimeBusDto item = source[i] ?? new AudioRuntimeBusDto();
                result[i] = new AudioBusDefinition(item.Id, item.Name, item.FmodBusPath, item.FmodVcaPath, item.DefaultVolume, item.DefaultMuted);
            }
            return result;
        }

        private static AudioEventDefinition[] BuildEvents(List<AudioRuntimeEventDto> source)
        {
            source ??= new List<AudioRuntimeEventDto>();
            var result = new AudioEventDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                AudioRuntimeEventDto item = source[i] ?? new AudioRuntimeEventDto();
                result[i] = new AudioEventDefinition(
                    item.Id,
                    string.IsNullOrWhiteSpace(item.StableId) ? item.Name : item.StableId,
                    item.FmodEventPath,
                    item.FmodEventGuid,
                    ParseEnum(item.Kind, AudioEventKind.Event),
                    item.BusId,
                    item.Is3D,
                    item.IsLoop,
                    item.MaxDistance,
                    BuildParameters(item.Parameters),
                    item.Labels?.ToArray());
            }
            return result;
        }

        private static AudioParameterDefinition[] BuildParameters(List<AudioRuntimeParameterDto> source)
        {
            source ??= new List<AudioRuntimeParameterDto>();
            var result = new AudioParameterDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                AudioRuntimeParameterDto item = source[i] ?? new AudioRuntimeParameterDto();
                result[i] = new AudioParameterDefinition(item.Id, item.Name, ParseEnum(item.Kind, AudioParameterKind.Continuous), item.DefaultValue, item.MinValue, item.MaxValue);
            }
            return result;
        }

        private static AudioCueDefinition[] BuildCues(List<AudioRuntimeCueDto> source)
        {
            source ??= new List<AudioRuntimeCueDto>();
            var result = new AudioCueDefinition[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                AudioRuntimeCueDto item = source[i] ?? new AudioRuntimeCueDto();
                result[i] = new AudioCueDefinition(
                    item.Id,
                    item.AudioCueId,
                    item.EventId,
                    ParseEnum(item.PlayMode, AudioCuePlayMode.OneShot),
                    BuildParameterValues(item.DefaultParameters),
                    ParseEnum(item.MissingPolicy, AudioCueMissingPolicy.Mute),
                    item.Labels?.ToArray());
            }
            return result;
        }

        private static AudioParameterValue[] BuildParameterValues(List<AudioRuntimeParameterValueDto> source)
        {
            source ??= new List<AudioRuntimeParameterValueDto>();
            var result = new AudioParameterValue[source.Count];
            for (int i = 0; i < result.Length; i++)
            {
                AudioRuntimeParameterValueDto item = source[i] ?? new AudioRuntimeParameterValueDto();
                result[i] = new AudioParameterValue(item.ParameterId, item.Value);
            }
            return result;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse(value, true, out T result) ? result : fallback;
        }

        private sealed class AudioRuntimeCatalogDto
        {
            public string Format { get; set; } = string.Empty;
            public string ManifestHash { get; set; } = string.Empty;
            public string SnapshotHash { get; set; } = string.Empty;
            public List<AudioRuntimeBusDto> Buses { get; set; } = new List<AudioRuntimeBusDto>();
            public List<AudioRuntimeEventDto> Events { get; set; } = new List<AudioRuntimeEventDto>();
            public List<AudioRuntimeCueDto> Cues { get; set; } = new List<AudioRuntimeCueDto>();
        }
        private sealed class AudioRuntimeBusDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string FmodBusPath { get; set; } = string.Empty;
            public string FmodVcaPath { get; set; } = string.Empty;
            public float DefaultVolume { get; set; } = 1f;
            public bool DefaultMuted { get; set; }
        }
        private sealed class AudioRuntimeEventDto
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
            public List<AudioRuntimeParameterDto> Parameters { get; set; } = new List<AudioRuntimeParameterDto>();
            public List<string> Labels { get; set; } = new List<string>();
        }
        private sealed class AudioRuntimeParameterDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Kind { get; set; } = "Continuous";
            public float DefaultValue { get; set; }
            public float MinValue { get; set; }
            public float MaxValue { get; set; } = 1f;
        }
        private sealed class AudioRuntimeCueDto
        {
            public int Id { get; set; }
            public string AudioCueId { get; set; } = string.Empty;
            public int EventId { get; set; }
            public string PlayMode { get; set; } = "OneShot";
            public string MissingPolicy { get; set; } = "Mute";
            public List<AudioRuntimeParameterValueDto> DefaultParameters { get; set; } = new List<AudioRuntimeParameterValueDto>();
            public List<string> Labels { get; set; } = new List<string>();
        }
        private sealed class AudioRuntimeParameterValueDto
        {
            public int ParameterId { get; set; }
            public float Value { get; set; }
        }
    }
}
