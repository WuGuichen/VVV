using System;

namespace MxFramework.Audio.Physical
{
    public readonly struct ContinuousPhysicalAudioSnapshot
    {
        public ContinuousPhysicalAudioSnapshot(int active, int started, int updated, int stopped, int rejected, int failed)
        {
            Active = active;
            Started = started;
            Updated = updated;
            Stopped = stopped;
            Rejected = rejected;
            Failed = failed;
        }

        public int Active { get; }
        public int Started { get; }
        public int Updated { get; }
        public int Stopped { get; }
        public int Rejected { get; }
        public int Failed { get; }
    }

    /// <summary>Owns Started/Stayed/Ended controlled voices for scrape, roll and compression facts.</summary>
    public sealed class ContinuousPhysicalAudioSystem
    {
        private readonly IAudioService _service;
        private readonly IAudioCueDefinitionProvider _cues;
        private readonly PhysicalAudioResolver _resolver;
        private readonly ActiveVoice[] _voices;
        private readonly AudioParameterValue[] _parameters;
        private int _voiceCount;
        private int _started;
        private int _updated;
        private int _stopped;
        private int _rejected;
        private int _failed;

        /// <remarks>AllocByDesign during composition. Process is NoAlloc on successful backend paths.</remarks>
        public ContinuousPhysicalAudioSystem(
            IAudioService service,
            IAudioCueDefinitionProvider cues,
            PhysicalAudioResolver resolver,
            int voiceCapacity,
            int parameterCapacity)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _cues = cues ?? throw new ArgumentNullException(nameof(cues));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            if (voiceCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(voiceCapacity));
            if (parameterCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(parameterCapacity));
            _voices = new ActiveVoice[voiceCapacity];
            _parameters = new AudioParameterValue[parameterCapacity];
        }

        public ContinuousPhysicalAudioSnapshot Snapshot => new ContinuousPhysicalAudioSnapshot(
            _voiceCount, _started, _updated, _stopped, _rejected, _failed);

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public AudioResult Process(in PhysicalAudioInteractionFact fact)
        {
            if (!IsContinuous(fact.Kind))
            {
                _rejected++;
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Physical interaction is not continuous: " + fact.Kind + ".");
            }

            int index = Find(fact);
            switch (fact.Phase)
            {
                case PhysicalAudioInteractionPhase.Started:
                    return index >= 0 ? Update(index, fact) : Start(fact);
                case PhysicalAudioInteractionPhase.Stayed:
                    if (index < 0)
                    {
                        _rejected++;
                        return AudioResult.Fail(AudioErrorCode.InvalidHandle, "Continuous physical voice was not started.");
                    }
                    return Update(index, fact);
                case PhysicalAudioInteractionPhase.Ended:
                    if (index < 0)
                        return AudioResult.Ok();
                    return Stop(index);
                default:
                    _rejected++;
                    return AudioResult.Fail(AudioErrorCode.RequestRejected, "Unknown physical interaction phase.");
            }
        }

        /// <remarks>NoAlloc on successful backend paths.</remarks>
        public int StopAll(AudioStopMode stopMode = AudioStopMode.AllowFadeout)
        {
            int count = 0;
            for (int i = _voiceCount - 1; i >= 0; i--)
            {
                if (_service.Stop(_voices[i].Handle, stopMode).Success)
                {
                    Remove(i);
                    count++;
                    _stopped++;
                }
            }
            return count;
        }

        private AudioResult Start(in PhysicalAudioInteractionFact fact)
        {
            if (_voiceCount == _voices.Length)
            {
                _rejected++;
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Continuous physical voice capacity is full.");
            }
            PhysicalAudioInteractionFact startedFact = fact.WithPhase(PhysicalAudioInteractionPhase.Started);
            if (!_resolver.TryResolve(startedFact, _parameters, out PhysicalAudioResolvedPlayback resolved)
                || !_cues.TryGetCue(resolved.CueId, out AudioCueDefinition cue)
                || cue.PlayMode != AudioCuePlayMode.StartEvent)
            {
                _failed++;
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Continuous physical fact did not resolve to a StartEvent Cue.");
            }

            var request = AudioPlayRequest.Create3D(
                cue.EventId,
                fact.Transform,
                fact.EmitterId,
                _parameters,
                resolved.Priority,
                fact.Frame,
                new AudioTraceIdentity(AudioTraceKindIds.ContinuousPhysical, fact.EmitterId, fact.Frame, (int)fact.Kind),
                resolved.ParameterCount);
            AudioPlayResult play = _service.StartEvent(request, out AudioHandle handle);
            if (!play.Success || !handle.IsValid)
            {
                _failed++;
                return play.Result;
            }
            _voices[_voiceCount++] = new ActiveVoice(fact, handle, resolved.CueId);
            _started++;
            return AudioResult.Ok();
        }

        private AudioResult Update(int index, in PhysicalAudioInteractionFact fact)
        {
            PhysicalAudioInteractionFact startedFact = fact.WithPhase(PhysicalAudioInteractionPhase.Started);
            if (!_resolver.TryResolve(startedFact, _parameters, out PhysicalAudioResolvedPlayback resolved)
                || resolved.CueId != _voices[index].CueId)
            {
                _failed++;
                return AudioResult.Fail(AudioErrorCode.RequestRejected, "Continuous physical voice changed or lost its resolved Cue.");
            }

            AudioHandle handle = _voices[index].Handle;
            for (int parameter = 0; parameter < resolved.ParameterCount; parameter++)
            {
                AudioResult set = _service.SetParameter(handle, _parameters[parameter].ParameterId, _parameters[parameter].Value);
                if (set.Failed)
                {
                    _failed++;
                    return set;
                }
            }
            if (_service is IAudioSpatialControl spatial)
            {
                AudioResult move = spatial.Update3DAttributes(handle, fact.Transform);
                if (move.Failed)
                {
                    _failed++;
                    return move;
                }
            }
            _updated++;
            return AudioResult.Ok();
        }

        private AudioResult Stop(int index)
        {
            AudioResult result = _service.Stop(_voices[index].Handle, AudioStopMode.AllowFadeout);
            if (result.Failed)
            {
                _failed++;
                return result;
            }
            Remove(index);
            _stopped++;
            return AudioResult.Ok();
        }

        private int Find(in PhysicalAudioInteractionFact fact)
        {
            for (int i = 0; i < _voiceCount; i++)
            {
                if (_voices[i].Kind == fact.Kind
                    && _voices[i].EmitterId == fact.EmitterId
                    && _voices[i].Sequence == fact.Sequence)
                    return i;
            }
            return -1;
        }

        private void Remove(int index)
        {
            int last = --_voiceCount;
            if (index != last)
                _voices[index] = _voices[last];
            _voices[last] = default;
        }

        private static bool IsContinuous(PhysicalAudioInteractionKind kind)
        {
            return kind == PhysicalAudioInteractionKind.Scrape
                || kind == PhysicalAudioInteractionKind.Roll
                || kind == PhysicalAudioInteractionKind.Compression;
        }

        private readonly struct ActiveVoice
        {
            public ActiveVoice(in PhysicalAudioInteractionFact fact, AudioHandle handle, int cueId)
            {
                Kind = fact.Kind;
                EmitterId = fact.EmitterId;
                Sequence = fact.Sequence;
                Handle = handle;
                CueId = cueId;
            }

            public PhysicalAudioInteractionKind Kind { get; }
            public int EmitterId { get; }
            public long Sequence { get; }
            public AudioHandle Handle { get; }
            public int CueId { get; }
        }
    }
}
