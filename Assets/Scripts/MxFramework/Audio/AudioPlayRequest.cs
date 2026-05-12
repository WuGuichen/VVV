namespace MxFramework.Audio
{
    public enum AudioPlayMode
    {
        OneShot = 0,
        StartEvent = 1
    }

    public readonly struct AudioPlayRequest
    {
        public AudioPlayRequest(
            int eventId,
            AudioTransform transform,
            AudioParameterValue[] parameters = null,
            int emitterId = 0,
            int priority = 0,
            long frame = 0,
            string traceId = null,
            AudioPlayMode playMode = AudioPlayMode.OneShot)
        {
            EventId = eventId;
            Transform = transform;
            Parameters = parameters ?? EmptyParameters;
            EmitterId = emitterId;
            Priority = priority;
            Frame = frame;
            TraceId = traceId ?? string.Empty;
            PlayMode = playMode;
        }

        public int EventId { get; }
        public AudioTransform Transform { get; }
        public AudioParameterValue[] Parameters { get; }
        public int EmitterId { get; }
        public int Priority { get; }
        public long Frame { get; }
        public string TraceId { get; }
        public AudioPlayMode PlayMode { get; }

        public static readonly AudioParameterValue[] EmptyParameters = new AudioParameterValue[0];

        public AudioPlayRequest WithPlayMode(AudioPlayMode playMode)
        {
            return new AudioPlayRequest(EventId, Transform, Parameters, EmitterId, Priority, Frame, TraceId, playMode);
        }

        public static AudioPlayRequest Create2D(int eventId, AudioParameterValue[] parameters = null, int priority = 0, long frame = 0, string traceId = null)
        {
            return new AudioPlayRequest(eventId, AudioTransform.Origin, parameters, 0, priority, frame, traceId);
        }

        public static AudioPlayRequest Create3D(int eventId, AudioTransform transform, int emitterId = 0, AudioParameterValue[] parameters = null, int priority = 0, long frame = 0, string traceId = null)
        {
            return new AudioPlayRequest(eventId, transform, parameters, emitterId, priority, frame, traceId);
        }
    }
}
