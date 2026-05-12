using System;

namespace MxFramework.AI
{
    public sealed class AiFactGoal<T> : IAiGoal
    {
        private readonly IAiCondition _condition;

        public AiFactGoal(int id, float priority, AiFactKey key, T expectedValue)
        {
            Id = id;
            Priority = priority;
            _condition = new AiFactCondition<T>(key, expectedValue);
        }

        public int Id { get; }
        public float Priority { get; }

        public bool IsRelevant(IAiWorldState worldState)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            return true;
        }

        public bool IsSatisfied(IAiWorldState worldState)
        {
            return _condition.IsSatisfied(worldState);
        }
    }
}
