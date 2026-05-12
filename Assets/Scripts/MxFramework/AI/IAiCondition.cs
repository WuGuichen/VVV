namespace MxFramework.AI
{
    public interface IAiCondition
    {
        bool IsSatisfied(IAiWorldState worldState);
    }
}
