namespace MxFramework.Buffs
{
    public interface IBuffStackingPolicy
    {
        BuffStackResult Apply(IBuff existingBuff, IBuff incomingBuff);
    }

    public readonly struct BuffStackResult
    {
        public readonly bool KeepExisting;
        public readonly bool AttachIncoming;
        public readonly bool RefreshDuration;
        public readonly int LayerDelta;
        public readonly int OverflowLayers;

        public BuffStackResult(
            bool keepExisting,
            bool attachIncoming,
            bool refreshDuration,
            int layerDelta,
            int overflowLayers)
        {
            KeepExisting = keepExisting;
            AttachIncoming = attachIncoming;
            RefreshDuration = refreshDuration;
            LayerDelta = layerDelta;
            OverflowLayers = overflowLayers;
        }
    }
}
