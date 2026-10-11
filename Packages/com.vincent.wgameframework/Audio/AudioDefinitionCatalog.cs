using System;
using System.Collections.Generic;

namespace MxFramework.Audio
{
    /// <summary>Immutable-after-construction runtime lookup catalog for buses, events, parameters, and semantic Cues.</summary>
    /// <remarks>LowFreqAlloc during startup construction. All lookup methods are NoAlloc.</remarks>
    public sealed class AudioDefinitionCatalog : IAudioDefinitionProvider, IAudioCueDefinitionProvider, IAudioCueKeyDefinitionProvider
    {
        private readonly Dictionary<int, AudioBusDefinition> _buses;
        private readonly Dictionary<int, AudioEventDefinition> _events;
        private readonly Dictionary<int, AudioCueDefinition> _cuesById;
        private readonly Dictionary<string, AudioCueDefinition> _cuesByKey;
        private readonly Dictionary<int, Dictionary<int, AudioParameterDefinition>> _parametersByEvent;
        private readonly Dictionary<int, Dictionary<string, AudioParameterDefinition>> _parametersByEventName;

        public AudioDefinitionCatalog(
            AudioBusDefinition[] buses,
            AudioEventDefinition[] events,
            AudioCueDefinition[] cues,
            string manifestHash = "",
            string snapshotHash = "")
        {
            Buses = Array.AsReadOnly(buses == null ? Array.Empty<AudioBusDefinition>() : (AudioBusDefinition[])buses.Clone());
            Events = Array.AsReadOnly(events == null ? Array.Empty<AudioEventDefinition>() : (AudioEventDefinition[])events.Clone());
            Cues = Array.AsReadOnly(cues == null ? Array.Empty<AudioCueDefinition>() : (AudioCueDefinition[])cues.Clone());
            ManifestHash = manifestHash ?? string.Empty;
            SnapshotHash = snapshotHash ?? string.Empty;
            _buses = new Dictionary<int, AudioBusDefinition>(Buses.Count);
            _events = new Dictionary<int, AudioEventDefinition>(Events.Count);
            _cuesById = new Dictionary<int, AudioCueDefinition>(Cues.Count);
            _cuesByKey = new Dictionary<string, AudioCueDefinition>(Cues.Count, StringComparer.Ordinal);
            _parametersByEvent = new Dictionary<int, Dictionary<int, AudioParameterDefinition>>(Events.Count);
            _parametersByEventName = new Dictionary<int, Dictionary<string, AudioParameterDefinition>>(Events.Count);

            for (int i = 0; i < Buses.Count; i++)
                AddUnique(_buses, Buses[i].Id, Buses[i], "bus");
            for (int i = 0; i < Events.Count; i++)
            {
                AudioEventDefinition audioEvent = Events[i];
                AddUnique(_events, audioEvent.Id, audioEvent, "event");
                var byId = new Dictionary<int, AudioParameterDefinition>(audioEvent.Parameters.Count);
                var byName = new Dictionary<string, AudioParameterDefinition>(audioEvent.Parameters.Count, StringComparer.Ordinal);
                for (int p = 0; p < audioEvent.Parameters.Count; p++)
                {
                    AudioParameterDefinition parameter = audioEvent.Parameters[p];
                    AddUnique(byId, parameter.Id, parameter, "parameter");
                    if (string.IsNullOrWhiteSpace(parameter.Name) || byName.ContainsKey(parameter.Name))
                        throw new ArgumentException("Audio parameter name is empty or duplicated for event " + audioEvent.Id + ".");
                    byName.Add(parameter.Name, parameter);
                }
                _parametersByEvent.Add(audioEvent.Id, byId);
                _parametersByEventName.Add(audioEvent.Id, byName);
            }
            for (int i = 0; i < Cues.Count; i++)
            {
                AudioCueDefinition cue = Cues[i];
                AddUnique(_cuesById, cue.Id, cue, "cue");
                if (string.IsNullOrWhiteSpace(cue.Name) || _cuesByKey.ContainsKey(cue.Name))
                    throw new ArgumentException("Audio Cue key is empty or duplicated: " + cue.Name);
                if (!_events.ContainsKey(cue.EventId))
                    throw new ArgumentException("Audio Cue references missing event id " + cue.EventId + ".");
                _cuesByKey.Add(cue.Name, cue);
            }
        }

        public string ManifestHash { get; }
        public string SnapshotHash { get; }
        public IReadOnlyList<AudioBusDefinition> Buses { get; }
        public IReadOnlyList<AudioEventDefinition> Events { get; }
        public IReadOnlyList<AudioCueDefinition> Cues { get; }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetCue(string audioCueId, out AudioCueDefinition definition)
        {
            if (string.IsNullOrEmpty(audioCueId))
            {
                definition = default;
                return false;
            }
            return _cuesByKey.TryGetValue(audioCueId, out definition);
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetCue(int cueId, out AudioCueDefinition definition) => _cuesById.TryGetValue(cueId, out definition);

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetEvent(int eventId, out AudioEventDefinition definition) => _events.TryGetValue(eventId, out definition);

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetBus(int busId, out AudioBusDefinition definition) => _buses.TryGetValue(busId, out definition);

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetParameter(int eventId, int parameterId, out AudioParameterDefinition definition)
        {
            if (_parametersByEvent.TryGetValue(eventId, out Dictionary<int, AudioParameterDefinition> parameters))
                return parameters.TryGetValue(parameterId, out definition);
            definition = default;
            return false;
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetParameter(int eventId, string parameterName, out AudioParameterDefinition definition)
        {
            if (!string.IsNullOrEmpty(parameterName)
                && _parametersByEventName.TryGetValue(eventId, out Dictionary<string, AudioParameterDefinition> parameters))
                return parameters.TryGetValue(parameterName, out definition);
            definition = default;
            return false;
        }

        private static void AddUnique<T>(Dictionary<int, T> target, int id, T value, string label)
        {
            if (id <= 0 || target.ContainsKey(id))
                throw new ArgumentException("Audio " + label + " id is invalid or duplicated: " + id + ".");
            target.Add(id, value);
        }
    }
}
