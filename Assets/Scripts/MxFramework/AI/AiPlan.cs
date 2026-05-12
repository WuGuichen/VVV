using System.Collections.Generic;

namespace MxFramework.AI
{
    public sealed class AiPlan
    {
        private readonly List<IAiAction> _actions;

        public AiPlan(IAiGoal goal, IReadOnlyList<IAiAction> actions, float totalCost)
        {
            Goal = goal;
            _actions = actions != null ? new List<IAiAction>(actions) : new List<IAiAction>();
            TotalCost = totalCost;
        }

        public IAiGoal Goal { get; }
        public IReadOnlyList<IAiAction> Actions => _actions;
        public float TotalCost { get; }
        public bool IsValid => Goal != null;

        public static AiPlan Empty(IAiGoal goal)
        {
            return new AiPlan(goal, new IAiAction[0], 0f);
        }
    }
}
