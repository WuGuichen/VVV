using System;

namespace MxFramework.AI
{
    public sealed class AiFactCondition<T> : IAiCondition
    {
        public AiFactCondition(AiFactKey key, T expectedValue)
        {
            Key = key;
            ExpectedValue = expectedValue;
        }

        public AiFactKey Key { get; }
        public T ExpectedValue { get; }

        public bool IsSatisfied(IAiWorldState worldState)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            return worldState.TryGetValue(Key, out T value) && Equals(value, ExpectedValue);
        }
    }
}
