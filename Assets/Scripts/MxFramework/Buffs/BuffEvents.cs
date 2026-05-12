namespace MxFramework.Buffs
{
    public readonly struct BuffEvent
    {
        public readonly int BuffId;
        public readonly BuffEventType Type;
        public readonly int LayerDelta;
        public readonly object Source;

        public BuffEvent(int buffId, BuffEventType type, int layerDelta, object source)
        {
            BuffId = buffId;
            Type = type;
            LayerDelta = layerDelta;
            Source = source;
        }
    }

    public enum BuffEventType
    {
        Added,
        Removed,
        Tick,
        LayerChanged,
        DurationRefreshed
    }
}
