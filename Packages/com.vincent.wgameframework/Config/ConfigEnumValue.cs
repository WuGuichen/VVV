namespace MxFramework.Config
{
    public readonly struct ConfigEnumValue
    {
        public ConfigEnumValue(int value, string name, string displayName = "", string description = "")
        {
            Value = value;
            Name = name ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public int Value { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Name);
    }
}
