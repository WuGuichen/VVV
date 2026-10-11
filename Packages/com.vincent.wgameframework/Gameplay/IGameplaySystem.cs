namespace MxFramework.Gameplay
{
    public interface IGameplaySystem
    {
        string SystemId { get; }
        GameplaySystemPhase Phase { get; }
        int Priority { get; }
        bool IsEnabled { get; }
        void Tick(GameplaySystemContext context);
    }
}
