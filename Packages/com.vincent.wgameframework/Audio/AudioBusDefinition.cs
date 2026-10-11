namespace MxFramework.Audio
{
    public readonly struct AudioBusDefinition
    {
        public AudioBusDefinition(int id, string name, string fmodBusPath, string fmodVcaPath = null, float defaultVolume = 1f, bool defaultMuted = false)
        {
            Id = id;
            Name = name ?? string.Empty;
            FmodBusPath = fmodBusPath ?? string.Empty;
            FmodVcaPath = fmodVcaPath ?? string.Empty;
            DefaultVolume = defaultVolume;
            DefaultMuted = defaultMuted;
        }

        public int Id { get; }
        public string Name { get; }
        public string FmodBusPath { get; }
        public string FmodVcaPath { get; }
        public float DefaultVolume { get; }
        public bool DefaultMuted { get; }
    }
}
