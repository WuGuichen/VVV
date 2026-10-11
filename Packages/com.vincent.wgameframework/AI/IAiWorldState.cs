using System.Collections.Generic;

namespace MxFramework.AI
{
    public interface IAiWorldState
    {
        bool Contains(AiFactKey key);
        bool TryGetValue<T>(AiFactKey key, out T value);
        object GetRawValue(AiFactKey key);
        void SetValue<T>(AiFactKey key, T value);
        bool Remove(AiFactKey key);
        IAiWorldState Clone();
        IReadOnlyDictionary<AiFactKey, object> Snapshot();
    }
}
