using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public sealed class PriorityGoalSelector : IAiGoalSelector
    {
        public bool TrySelectGoal(IAiWorldState worldState, IEnumerable<IAiGoal> goals, out IAiGoal goal)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (goals == null)
                throw new ArgumentNullException(nameof(goals));

            goal = null;
            foreach (IAiGoal candidate in goals)
            {
                if (candidate == null || !candidate.IsRelevant(worldState) || candidate.IsSatisfied(worldState))
                    continue;

                if (goal == null || candidate.Priority > goal.Priority)
                    goal = candidate;
            }

            return goal != null;
        }
    }
}
