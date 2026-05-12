namespace MxFramework.AI
{
    public interface IAiAgent
    {
        int Id { get; }
        IAiWorldState WorldState { get; }
    }
}
