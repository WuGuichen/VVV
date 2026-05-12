using System;

namespace MxFramework.Gameplay
{
    public interface IGameplayComponentStore
    {
        Type ComponentType { get; }
        int Count { get; }
        bool Remove(GameplayEntityId entityId);
        void Clear();
    }
}
