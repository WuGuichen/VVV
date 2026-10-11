using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public interface IAiUtilityConsideration
    {
        float Weight { get; }
        float Score(IAiAction action, IAiWorldState worldState, IAiGoal goal);
    }

    public sealed class AiUtilityPlanner : IAiPlanner
    {
        private readonly IAiGoalSelector _goalSelector;
        private readonly List<IAiUtilityConsideration> _considerations;

        public AiUtilityPlanner(
            IEnumerable<IAiUtilityConsideration> considerations = null,
            IAiGoalSelector goalSelector = null)
        {
            _goalSelector = goalSelector ?? new PriorityGoalSelector();
            _considerations = new List<IAiUtilityConsideration>();
            if (considerations == null)
                return;

            foreach (IAiUtilityConsideration consideration in considerations)
            {
                if (consideration != null)
                    _considerations.Add(consideration);
            }
        }

        public bool TryPlan(
            IAiWorldState worldState,
            IEnumerable<IAiGoal> goals,
            IEnumerable<IAiAction> actions,
            out AiPlan plan)
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

            IAiAction bestAction = null;
            float bestScore = float.MinValue;
            foreach (IAiAction action in actions)
            {
                if (action == null || !action.CanExecute(worldState))
                    continue;

                float score = ScoreAction(action, worldState, goal);
                if (bestAction == null || IsBetter(action, score, bestAction, bestScore))
                {
                    bestAction = action;
                    bestScore = score;
                }
            }

            if (bestAction == null)
                return false;

            plan = new AiPlan(goal, new[] { bestAction }, bestAction.Cost);
            return true;
        }

        private float ScoreAction(IAiAction action, IAiWorldState worldState, IAiGoal goal)
        {
            if (_considerations.Count == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < _considerations.Count; i++)
            {
                IAiUtilityConsideration consideration = _considerations[i];
                total += Sanitize(consideration.Weight) * Sanitize(consideration.Score(action, worldState, goal));
            }

            return total;
        }

        private static bool IsBetter(
            IAiAction candidate,
            float candidateScore,
            IAiAction current,
            float currentScore)
        {
            int scoreCompare = candidateScore.CompareTo(currentScore);
            if (scoreCompare != 0)
                return scoreCompare > 0;

            int costCompare = candidate.Cost.CompareTo(current.Cost);
            if (costCompare != 0)
                return costCompare < 0;

            return candidate.Id < current.Id;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }
}
