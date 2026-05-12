namespace MxFramework.Buffs
{
    public sealed class DefaultBuffStackingPolicy : IBuffStackingPolicy
    {
        public BuffStackResult Apply(IBuff existingBuff, IBuff incomingBuff)
        {
            int overflow = 0;
            int layerDelta = 0;

            if (incomingBuff.CurrentLayers > 0)
            {
                int oldLayer = existingBuff.CurrentLayers;
                overflow = existingBuff.AddLayer(incomingBuff.CurrentLayers);
                layerDelta = existingBuff.CurrentLayers - oldLayer;
            }

            return new BuffStackResult(
                keepExisting: true,
                attachIncoming: false,
                refreshDuration: true,
                layerDelta: layerDelta,
                overflowLayers: overflow);
        }
    }
}
