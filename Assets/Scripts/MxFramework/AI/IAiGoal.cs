namespace MxFramework.AI
{
    public interface IAiGoal
    {
        int Id { get; }
        float Priority { get; }
        bool IsRelevant(IAiWorldState worldState);
        bool IsSatisfied(IAiWorldState worldState);
    }
}
