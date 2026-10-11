namespace MxFramework.Authoring
{
    /// <summary>Character-owned initial values. Live state and body geometry are owned elsewhere.</summary>
    public sealed class CharacterBaseAttributesAuthoring
    {
        public const string RelativePath = "config/character_attributes.json";
        public const string CurrentSchemaVersion = "1.0";
        public const int MaximumScalarValue = 16777215;
        public string SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int Hp { get; set; } = 100;
        public int Attack { get; set; } = 20;
        public int Defense { get; set; } = 10;
        public int BaseCohesion { get; set; } = 100;
        public int ScatterRecoveryRate { get; set; } = 2;
        public int ScatterRecoveryDelayFrames { get; set; } = 360;
        public int CohesionBreakRecoveryDelayFrames { get; set; } = 270;
    }
}
