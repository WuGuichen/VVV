using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public readonly struct AiHfsmContext
    {
        public AiHfsmContext(IAiAgent agent, IAiWorldState worldState, int frame)
        {
            Agent = agent;
            WorldState = worldState;
            Frame = frame;
        }

        public IAiAgent Agent { get; }
        public IAiWorldState WorldState { get; }
        public int Frame { get; }
    }

    public interface IAiHfsmState
    {
        int Id { get; }
        int ParentId { get; }
        IAiAction Action { get; }
        void OnEnter(AiHfsmContext context);
        void OnTick(AiHfsmContext context);
        void OnExit(AiHfsmContext context);
    }

    public interface IAiHfsmTransition
    {
        int SourceStateId { get; }
        int TargetStateId { get; }
        int Priority { get; }
        bool CanTransition(AiHfsmContext context);
    }

    public sealed class AiHfsmState : IAiHfsmState
    {
        private readonly Action<AiHfsmContext> _onEnter;
        private readonly Action<AiHfsmContext> _onTick;
        private readonly Action<AiHfsmContext> _onExit;

        public AiHfsmState(
            int id,
            int parentId = 0,
            IAiAction action = null,
            Action<AiHfsmContext> onEnter = null,
            Action<AiHfsmContext> onTick = null,
            Action<AiHfsmContext> onExit = null)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id), "Runtime AI Planner HFSM state id must be positive.");

            Id = id;
            ParentId = parentId;
            Action = action;
            _onEnter = onEnter;
            _onTick = onTick;
            _onExit = onExit;
        }

        public int Id { get; }
        public int ParentId { get; }
        public IAiAction Action { get; }

        public void OnEnter(AiHfsmContext context)
        {
            _onEnter?.Invoke(context);
        }

        public void OnTick(AiHfsmContext context)
        {
            _onTick?.Invoke(context);
        }

        public void OnExit(AiHfsmContext context)
        {
            _onExit?.Invoke(context);
        }
    }

    public sealed class AiHfsmTransition : IAiHfsmTransition
    {
        private readonly Func<AiHfsmContext, bool> _predicate;

        public AiHfsmTransition(
            int sourceStateId,
            int targetStateId,
            int priority,
            Func<AiHfsmContext, bool> predicate)
        {
            if (targetStateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetStateId), "Runtime AI Planner HFSM target state id must be positive.");

            SourceStateId = sourceStateId;
            TargetStateId = targetStateId;
            Priority = priority;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public int SourceStateId { get; }
        public int TargetStateId { get; }
        public int Priority { get; }

        public bool CanTransition(AiHfsmContext context)
        {
            return _predicate(context);
        }
    }

    public sealed class AiHfsmStateMachine
    {
        private readonly Dictionary<int, IAiHfsmState> _states =
            new Dictionary<int, IAiHfsmState>();
        private readonly Dictionary<int, int[]> _statePaths =
            new Dictionary<int, int[]>();
        private readonly List<IAiHfsmTransition> _transitions;
        private readonly List<int> _activePath = new List<int>();
        private bool _entered;

        public AiHfsmStateMachine(
            IEnumerable<IAiHfsmState> states,
            int initialStateId,
            IEnumerable<IAiHfsmTransition> transitions = null)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            if (initialStateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialStateId));

            foreach (IAiHfsmState state in states)
            {
                if (state == null)
                    continue;
                _states.Add(state.Id, state);
            }

            if (!_states.ContainsKey(initialStateId))
                throw new ArgumentException("Runtime AI Planner HFSM initial state is missing.", nameof(initialStateId));

            InitialStateId = initialStateId;
            CurrentStateId = initialStateId;
            CacheStatePaths();
            _activePath.AddRange(_statePaths[initialStateId]);
            _transitions = new List<IAiHfsmTransition>();
            if (transitions != null)
            {
                foreach (IAiHfsmTransition transition in transitions)
                {
                    if (transition != null)
                        _transitions.Add(transition);
                }
            }
        }

        public int InitialStateId { get; }
        public int CurrentStateId { get; private set; }
        public IReadOnlyList<int> ActivePath => _activePath;
        public IAiAction CurrentAction => _states.TryGetValue(CurrentStateId, out IAiHfsmState state)
            ? state.Action
            : null;

        /// <remarks>NoAlloc after construction when state callbacks and transition predicates are NoAlloc.</remarks>
        public void Tick(IAiAgent agent, IAiWorldState worldState, int frame)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            var context = new AiHfsmContext(agent, worldState, frame);
            EnsureEntered(context);
            if (TrySelectTransition(context, out IAiHfsmTransition transition))
                TransitionTo(transition.TargetStateId, context);

            _states[CurrentStateId].OnTick(context);
        }

        public bool TransitionTo(int targetStateId, IAiAgent agent, IAiWorldState worldState, int frame)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            var context = new AiHfsmContext(agent, worldState, frame);
            EnsureEntered(context);
            return TransitionTo(targetStateId, context);
        }

        private void EnsureEntered(AiHfsmContext context)
        {
            if (_entered)
                return;

            for (int i = 0; i < _activePath.Count; i++)
                _states[_activePath[i]].OnEnter(context);
            _entered = true;
        }

        private bool TrySelectTransition(AiHfsmContext context, out IAiHfsmTransition selected)
        {
            selected = null;
            int selectedIndex = int.MaxValue;
            for (int i = 0; i < _transitions.Count; i++)
            {
                IAiHfsmTransition transition = _transitions[i];
                if (!SourceMatches(transition.SourceStateId) || !transition.CanTransition(context))
                    continue;

                if (selected == null
                    || transition.Priority > selected.Priority
                    || (transition.Priority == selected.Priority && i < selectedIndex))
                {
                    selected = transition;
                    selectedIndex = i;
                }
            }

            return selected != null;
        }

        private bool SourceMatches(int sourceStateId)
        {
            if (sourceStateId <= 0)
                return true;

            for (int i = 0; i < _activePath.Count; i++)
            {
                if (_activePath[i] == sourceStateId)
                    return true;
            }

            return false;
        }

        private bool TransitionTo(int targetStateId, AiHfsmContext context)
        {
            if (!_states.ContainsKey(targetStateId))
                throw new ArgumentException("Runtime AI Planner HFSM target state is missing.", nameof(targetStateId));
            if (targetStateId == CurrentStateId)
                return false;

            int[] targetPath = _statePaths[targetStateId];
            int commonCount = 0;
            while (commonCount < _activePath.Count
                && commonCount < targetPath.Length
                && _activePath[commonCount] == targetPath[commonCount])
            {
                commonCount++;
            }

            for (int i = _activePath.Count - 1; i >= commonCount; i--)
                _states[_activePath[i]].OnExit(context);

            _activePath.RemoveRange(commonCount, _activePath.Count - commonCount);
            for (int i = commonCount; i < targetPath.Length; i++)
            {
                _activePath.Add(targetPath[i]);
                _states[targetPath[i]].OnEnter(context);
            }

            CurrentStateId = targetStateId;
            return true;
        }

        private void CacheStatePaths()
        {
            foreach (int stateId in _states.Keys)
                _statePaths[stateId] = BuildPath(stateId);
        }

        private int[] BuildPath(int stateId)
        {
            var reversed = new List<int>();
            var visited = new HashSet<int>();
            int current = stateId;
            while (current > 0)
            {
                if (!visited.Add(current))
                {
                    throw new ArgumentException(
                        "Runtime AI Planner HFSM state path contains a parent cycle.");
                }

                if (!_states.TryGetValue(current, out IAiHfsmState state))
                    throw new ArgumentException("Runtime AI Planner HFSM state path contains a missing parent.");

                reversed.Add(current);
                current = state.ParentId;
            }

            reversed.Reverse();
            return reversed.ToArray();
        }
    }

    public sealed class AiHfsmPlannerAdapter : IAiPlanner
    {
        private readonly AiHfsmStateMachine _stateMachine;
        private readonly IAiGoalSelector _goalSelector;

        public AiHfsmPlannerAdapter(
            AiHfsmStateMachine stateMachine,
            IAiGoalSelector goalSelector = null)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _goalSelector = goalSelector ?? new PriorityGoalSelector();
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
            IAiAction action = _stateMachine.CurrentAction;
            if (action == null || !action.CanExecute(worldState))
                return false;

            if (!_goalSelector.TrySelectGoal(worldState, goals, out IAiGoal goal))
                return false;

            plan = new AiPlan(goal, action, action.Cost);
            return true;
        }
    }
}
