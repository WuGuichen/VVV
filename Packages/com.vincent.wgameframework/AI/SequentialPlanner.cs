using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public sealed class SequentialPlanner : IAiPlanner
    {
        private readonly IAiGoalSelector _goalSelector;

        public SequentialPlanner(int maxDepth = 6, IAiGoalSelector goalSelector = null)
        {
            if (maxDepth < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDepth));

            MaxDepth = maxDepth;
            _goalSelector = goalSelector ?? new PriorityGoalSelector();
        }

        public int MaxDepth { get; }

        public bool TryPlan(IAiWorldState worldState, IEnumerable<IAiGoal> goals, IEnumerable<IAiAction> actions, out AiPlan plan)
        {
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (goals == null)
                throw new ArgumentNullException(nameof(goals));
            if (actions == null)
                throw new ArgumentNullException(nameof(actions));

            plan = null;
            if (!_goalSelector.TrySelectGoal(worldState, goals, out IAiGoal goal))
                return false;

            if (goal.IsSatisfied(worldState))
            {
                plan = AiPlan.Empty(goal);
                return true;
            }

            var actionList = new List<IAiAction>();
            foreach (IAiAction action in actions)
            {
                if (action != null)
                    actionList.Add(action);
            }

            return TryFindPlan(worldState, goal, actionList, out plan);
        }

        private bool TryFindPlan(IAiWorldState startState, IAiGoal goal, IReadOnlyList<IAiAction> actions, out AiPlan plan)
        {
            var open = new Queue<SearchNode>();
            open.Enqueue(new SearchNode(startState.Clone(), new List<IAiAction>(), 0f));

            while (open.Count > 0)
            {
                SearchNode node = open.Dequeue();
                if (goal.IsSatisfied(node.WorldState))
                {
                    plan = new AiPlan(goal, node.Actions, node.TotalCost);
                    return true;
                }

                if (node.Actions.Count >= MaxDepth)
                    continue;

                for (int i = 0; i < actions.Count; i++)
                {
                    IAiAction action = actions[i];
                    if (!action.CanExecute(node.WorldState))
                        continue;

                    IAiWorldState nextState = node.WorldState.Clone();
                    action.Apply(nextState);
                    var nextActions = new List<IAiAction>(node.Actions) { action };
                    open.Enqueue(new SearchNode(nextState, nextActions, node.TotalCost + action.Cost));
                }
            }

            plan = null;
            return false;
        }

        private readonly struct SearchNode
        {
            public SearchNode(IAiWorldState worldState, List<IAiAction> actions, float totalCost)
            {
                WorldState = worldState;
                Actions = actions;
                TotalCost = totalCost;
            }

            public IAiWorldState WorldState { get; }
            public List<IAiAction> Actions { get; }
            public float TotalCost { get; }
        }
    }
}
