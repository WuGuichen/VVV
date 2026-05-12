namespace MxFramework.Config
{
    public readonly struct ConfigIdRange
    {
        public ConfigIdRange(int minInclusive, int maxInclusive)
        {
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
        }

        public int MinInclusive { get; }
        public int MaxInclusive { get; }
        public bool IsValid => MinInclusive > 0 && MaxInclusive >= MinInclusive;

        public bool Contains(int id)
        {
            return IsValid && id >= MinInclusive && id <= MaxInclusive;
        }
    }
}
