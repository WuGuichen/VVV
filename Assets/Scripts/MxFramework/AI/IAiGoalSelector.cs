using System.Collections.Generic;

namespace MxFramework.AI
{
    public interface IAiGoalSelector
    {
        bool TrySelectGoal(IAiWorldState worldState, IEnumerable<IAiGoal> goals, out IAiGoal goal);
    }
}
