namespace MxFramework.AI
{
    public interface IAiSensor
    {
        void Sense(IAiAgent agent, IAiWorldState worldState);
    }
}
