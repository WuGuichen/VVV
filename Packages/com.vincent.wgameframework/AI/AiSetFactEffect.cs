using System;

namespace MxFramework.AI
{
    public sealed class AiSetFactEffect<T> : IAiEffect
    {
        public AiSetFactEffect(AiFactKey key, T value)
        {
            Key = key;
            Value = value;
        }

        public AiFactKey Key { get; }
        public T Value { get; }

        public void Apply(IAiWorldState worldState)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            worldState.SetValue(Key, Value);
        }
    }
}
