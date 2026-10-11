using System;
using System.Collections.Generic;

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
            DefaultParameters = defaultParameters == null || defaultParameters.Length == 0
                ? Array.AsReadOnly(EmptyParameters)
                : Array.AsReadOnly((AudioParameterValue[])defaultParameters.Clone());
            MissingPolicy = missingPolicy;
            Labels = labels == null || labels.Length == 0
                ? Array.AsReadOnly(EmptyLabels)
                : Array.AsReadOnly((string[])labels.Clone());
        }

        public int Id { get; }
        public string Name { get; }
        public int EventId { get; }
        public AudioCuePlayMode PlayMode { get; }
        public IReadOnlyList<AudioParameterValue> DefaultParameters { get; }
        public AudioCueMissingPolicy MissingPolicy { get; }
        public IReadOnlyList<string> Labels { get; }

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

    /// <summary>Resolves a gameplay-facing stable Cue key without exposing FMOD bindings.</summary>
    public interface IAudioCueKeyDefinitionProvider
    {
        /// <remarks>NoAlloc. Implementations should build their lookup table during initialization.</remarks>
        bool TryGetCue(string audioCueId, out AudioCueDefinition definition);
    }
}
