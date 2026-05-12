namespace MxFramework.Buffs
{
    public interface IBuffPipeline
    {
        IBuff AddBuff(IBuff buff, IBuffTarget target);
        bool TryAddBuff(int buffId, IBuffTarget target, out IBuff buff);
        bool RemoveBuff(int buffId);
        void RemoveAllBuffs();
        void TickAll(float deltaTime);

        IBuff GetBuff(int buffId);
        bool TryGetBuff(int buffId, out IBuff buff);
        bool HasBuff(int buffId);
        int GetBuffLayer(int buffId);
        BuffSnapshot[] CreateSnapshot();
    }
}
