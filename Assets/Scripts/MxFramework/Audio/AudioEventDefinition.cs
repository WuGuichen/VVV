namespace MxFramework.Audio
{
    public enum AudioEventKind
    {
        Event = 0,
        Snapshot = 1
    }

    public readonly struct AudioEventDefinition
    {
        public AudioEventDefinition(
            int id,
            string name,
            string fmodEventPath,
            string fmodEventGuid,
            AudioEventKind kind,
            int busId,
            bool is3D,
            bool isLoop,
            float maxDistance,
            AudioParameterDefinition[] parameters = null,
            string[] labels = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            FmodEventPath = fmodEventPath ?? string.Empty;
            FmodEventGuid = fmodEventGuid ?? string.Empty;
            Kind = kind;
            BusId = busId;
            Is3D = is3D;
            IsLoop = isLoop;
            MaxDistance = maxDistance;
            Parameters = parameters ?? EmptyParameters;
            Labels = labels ?? EmptyLabels;
        }

        public int Id { get; }
        public string Name { get; }
        public string FmodEventPath { get; }
        public string FmodEventGuid { get; }
        public AudioEventKind Kind { get; }
        public int BusId { get; }
        public bool Is3D { get; }
        public bool IsLoop { get; }
        public float MaxDistance { get; }
        public AudioParameterDefinition[] Parameters { get; }
        public string[] Labels { get; }

        public static readonly AudioParameterDefinition[] EmptyParameters = new AudioParameterDefinition[0];
        public static readonly string[] EmptyLabels = new string[0];
    }
}
