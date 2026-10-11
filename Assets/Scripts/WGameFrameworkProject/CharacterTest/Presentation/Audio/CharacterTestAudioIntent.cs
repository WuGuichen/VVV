using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAudioIntentSource
    {
        Unknown = 0,
        ActionCue = 1,
        PhysicalAudio = 2,
        WeaponHit = 3,
        Interaction = 4
    }

    internal readonly struct CharacterTestAudioIntent
    {
        public const int MaxParameterCount = 5;

        private readonly AudioParameterValue _parameter0;
        private readonly AudioParameterValue _parameter1;
        private readonly AudioParameterValue _parameter2;
        private readonly AudioParameterValue _parameter3;
        private readonly AudioParameterValue _parameter4;

        private CharacterTestAudioIntent(
            CharacterTestAudioIntentSource source,
            int cueId,
            int eventId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            int priority,
            bool is3D,
            IReadOnlyList<AudioParameterValue> parameters,
            int parameterCount)
        {
            Source = source;
            CueId = cueId;
            EventId = eventId < 0 ? 0 : eventId;
            Transform = transform;
            EmitterId = emitterId < 0 ? 0 : emitterId;
            Frame = frame;
            TraceId = traceId;
            Priority = priority;
            Is3D = is3D;
            ParameterCount = ResolveParameterCount(parameters, parameterCount);
            _parameter0 = GetParameter(parameters, 0);
            _parameter1 = GetParameter(parameters, 1);
            _parameter2 = GetParameter(parameters, 2);
            _parameter3 = GetParameter(parameters, 3);
            _parameter4 = GetParameter(parameters, 4);
        }

        public CharacterTestAudioIntentSource Source { get; }
        public int CueId { get; }
        public int EventId { get; }
        public AudioTransform Transform { get; }
        public int EmitterId { get; }
        public RuntimeFrame Frame { get; }
        public AudioTraceIdentity TraceId { get; }
        public int Priority { get; }
        public bool Is3D { get; }
        public int ParameterCount { get; }

        public static CharacterTestAudioIntent CreateCue(
            CharacterTestAudioIntentSource source,
            int cueId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            IReadOnlyList<AudioParameterValue> parameters = null,
            int priority = 0,
            int parameterCount = -1)
        {
            return new CharacterTestAudioIntent(
                source,
                cueId,
                0,
                transform,
                emitterId,
                frame,
                traceId,
                priority,
                is3D: true,
                parameters,
                parameterCount);
        }

        public static CharacterTestAudioIntent CreateEvent(
            CharacterTestAudioIntentSource source,
            int eventId,
            AudioTransform transform,
            int emitterId,
            RuntimeFrame frame,
            AudioTraceIdentity traceId,
            IReadOnlyList<AudioParameterValue> parameters = null,
            int priority = 0,
            bool is3D = true,
            int parameterCount = -1)
        {
            return new CharacterTestAudioIntent(
                source,
                0,
                eventId,
                transform,
                emitterId,
                frame,
                traceId,
                priority,
                is3D,
                parameters,
                parameterCount);
        }

        public void CopyParametersTo(AudioParameterValue[] destination)
        {
            if (destination == null || destination.Length < ParameterCount)
                return;

            if (ParameterCount > 0)
                destination[0] = _parameter0;
            if (ParameterCount > 1)
                destination[1] = _parameter1;
            if (ParameterCount > 2)
                destination[2] = _parameter2;
            if (ParameterCount > 3)
                destination[3] = _parameter3;
            if (ParameterCount > 4)
                destination[4] = _parameter4;
        }

        private static int ResolveParameterCount(
            IReadOnlyList<AudioParameterValue> parameters,
            int requestedCount)
        {
            if (parameters == null || parameters.Count == 0)
                return 0;

            int count = requestedCount < 0 || requestedCount > parameters.Count
                ? parameters.Count
                : requestedCount;
            return count > MaxParameterCount ? MaxParameterCount : count;
        }

        private static AudioParameterValue GetParameter(IReadOnlyList<AudioParameterValue> parameters, int index)
        {
            return parameters != null && index >= 0 && index < parameters.Count
                ? parameters[index]
                : default;
        }
    }
}
