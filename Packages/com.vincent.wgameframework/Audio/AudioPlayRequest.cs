using System.Collections.Generic;

namespace MxFramework.Audio
{
    public enum AudioPlayMode
    {
        OneShot = 0,
        StartEvent = 1
    }

    public enum AudioSpatialMode
    {
        TwoDimensional = 0,
        ThreeDimensional = 1
    }

    public readonly struct AudioPlayRequest
    {
        public AudioPlayRequest(
            int eventId,
            AudioTransform transform,
            IReadOnlyList<AudioParameterValue> parameters = null,
            int emitterId = 0,
            int priority = 0,
            long frame = 0,
            AudioTraceIdentity traceId = default,
            AudioPlayMode playMode = AudioPlayMode.OneShot,
            AudioSpatialMode spatialMode = AudioSpatialMode.TwoDimensional,
            int parameterCount = -1)
        {
            EventId = eventId;
            Transform = transform;
            Parameters = parameters ?? EmptyParameters;
            ParameterCount = parameterCount < 0 || parameterCount > Parameters.Count
                ? Parameters.Count
                : parameterCount;
            EmitterId = emitterId;
            Priority = priority;
            Frame = frame;
            TraceId = traceId;
            PlayMode = playMode;
            SpatialMode = spatialMode;
        }

        public int EventId { get; }
        public AudioTransform Transform { get; }
        /// <summary>Caller-owned parameter view that must remain stable until the service call returns.</summary>
        /// <remarks>NoAlloc. The request does not copy parameter values.</remarks>
        public IReadOnlyList<AudioParameterValue> Parameters { get; }
        public int ParameterCount { get; }
        public int EmitterId { get; }
        public int Priority { get; }
        public long Frame { get; }
        public AudioTraceIdentity TraceId { get; }
        public AudioPlayMode PlayMode { get; }
        public AudioSpatialMode SpatialMode { get; }

        public static readonly AudioParameterValue[] EmptyParameters = new AudioParameterValue[0];

        public AudioPlayRequest WithPlayMode(AudioPlayMode playMode)
        {
            return new AudioPlayRequest(EventId, Transform, Parameters, EmitterId, Priority, Frame, TraceId, playMode, SpatialMode, ParameterCount);
        }

        public static AudioPlayRequest Create2D(int eventId, IReadOnlyList<AudioParameterValue> parameters = null, int priority = 0, long frame = 0, AudioTraceIdentity traceId = default)
        {
            return new AudioPlayRequest(
                eventId,
                AudioTransform.Origin,
                parameters,
                0,
                priority,
                frame,
                traceId,
                AudioPlayMode.OneShot,
                AudioSpatialMode.TwoDimensional);
        }

        public static AudioPlayRequest Create3D(int eventId, AudioTransform transform, int emitterId = 0, IReadOnlyList<AudioParameterValue> parameters = null, int priority = 0, long frame = 0, AudioTraceIdentity traceId = default, int parameterCount = -1)
        {
            return new AudioPlayRequest(
                eventId,
                transform,
                parameters,
                emitterId,
                priority,
                frame,
                traceId,
                AudioPlayMode.OneShot,
                AudioSpatialMode.ThreeDimensional,
                parameterCount);
        }
    }
}
