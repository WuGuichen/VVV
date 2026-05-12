namespace MxFramework.Buffs
{
    public readonly struct BuffSnapshot
    {
        public readonly int Id;
        public readonly float Duration;
        public readonly float RemainingTime;
        public readonly int CurrentLayers;
        public readonly int MaxLayers;
        public readonly bool IsPermanent;

        public BuffSnapshot(IBuff buff)
        {
            Id = buff.Id;
            Duration = buff.Duration;
            RemainingTime = buff.RemainingTime;
            CurrentLayers = buff.CurrentLayers;
            MaxLayers = buff.MaxLayers;
            IsPermanent = buff.IsPermanent;
        }
    }
}
