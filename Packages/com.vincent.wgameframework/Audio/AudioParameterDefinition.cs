namespace MxFramework.Audio
{
    public enum AudioParameterKind
    {
        Continuous = 0,
        Discrete = 1,
        Labeled = 2
    }

    public readonly struct AudioParameterDefinition
    {
        public AudioParameterDefinition(int id, string name, AudioParameterKind kind = AudioParameterKind.Continuous, float defaultValue = 0f, float minValue = 0f, float maxValue = 1f)
        {
            Id = id;
            Name = name ?? string.Empty;
            Kind = kind;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public int Id { get; }
        public string Name { get; }
        public AudioParameterKind Kind { get; }
        public float DefaultValue { get; }
        public float MinValue { get; }
        public float MaxValue { get; }
    }

    public readonly struct AudioParameterValue
    {
        public AudioParameterValue(int parameterId, float value)
        {
            ParameterId = parameterId;
            Value = value;
        }

        public int ParameterId { get; }
        public float Value { get; }
    }
}
