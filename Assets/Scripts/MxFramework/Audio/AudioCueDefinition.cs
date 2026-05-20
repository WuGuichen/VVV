namespace MxFramework.Audio
{
    public enum AudioCuePlayMode
    {
        OneShot = 0,
        StartEvent = 1
    }

    public enum AudioCueMissingPolicy
    {
        Mute = 0,
        FailRequest = 1
    }

    public readonly struct AudioCueDefinition
    {
        public AudioCueDefinition(
            int id,
            string name,
            int eventId,
            AudioCuePlayMode playMode = AudioCuePlayMode.OneShot,
            AudioParameterValue[] defaultParameters = null,
            AudioCueMissingPolicy missingPolicy = AudioCueMissingPolicy.Mute,
            string[] labels = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            EventId = eventId;
            PlayMode = playMode;
            DefaultParameters = defaultParameters ?? EmptyParameters;
            MissingPolicy = missingPolicy;
            Labels = labels ?? EmptyLabels;
        }

        public int Id { get; }
        public string Name { get; }
        public int EventId { get; }
        public AudioCuePlayMode PlayMode { get; }
        public AudioParameterValue[] DefaultParameters { get; }
        public AudioCueMissingPolicy MissingPolicy { get; }
        public string[] Labels { get; }

        public static readonly AudioParameterValue[] EmptyParameters = new AudioParameterValue[0];
        public static readonly string[] EmptyLabels = new string[0];

        public AudioPlayMode ToAudioPlayMode()
        {
            return PlayMode == AudioCuePlayMode.StartEvent ? AudioPlayMode.StartEvent : AudioPlayMode.OneShot;
        }
    }

    public interface IAudioCueDefinitionProvider
    {
        bool TryGetCue(int cueId, out AudioCueDefinition definition);
    }
}
