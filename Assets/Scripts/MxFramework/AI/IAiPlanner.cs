using System.Collections.Generic;

namespace MxFramework.AI
{
    public interface IAiPlanner
    {
        bool TryPlan(IAiWorldState worldState, IEnumerable<IAiGoal> goals, IEnumerable<IAiAction> actions, out AiPlan plan);
    }
}
