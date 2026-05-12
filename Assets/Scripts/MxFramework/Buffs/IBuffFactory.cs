namespace MxFramework.Buffs
{
    public interface IBuffFactory
    {
        bool TryCreate(int buffId, out IBuff buff);
    }
}
