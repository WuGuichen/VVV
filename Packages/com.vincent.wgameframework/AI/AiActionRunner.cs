using System;
using System.Collections.Generic;

namespace MxFramework.AI
{
    public enum AiActionRunnerState
    {
        Idle = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Canceled = 4
    }

    public readonly struct AiActionRunnerStatus
    {
        public AiActionRunnerStatus(
            AiActionRunnerState state,
            int activeActionId,
            int actionIndex,
            int actionCount,
            string reason)
        {
            State = state;
            ActiveActionId = activeActionId;
            ActionIndex = actionIndex;
            ActionCount = actionCount;
            Reason = reason ?? string.Empty;
        }

        public AiActionRunnerState State { get; }
        public int ActiveActionId { get; }
        public int ActionIndex { get; }
        public int ActionCount { get; }
        public string Reason { get; }
        public bool IsTerminal =>
            State == AiActionRunnerState.Completed
            || State == AiActionRunnerState.Failed
            || State == AiActionRunnerState.Canceled;
    }

    public interface IAiActionRunner
    {
        AiActionRunnerStatus Status { get; }
        bool Start(AiPlan plan, IAiAgent agent, IAiWorldState worldState);
        /// <remarks>NoAlloc while a valid plan is running when actions are NoAlloc.</remarks>
        AiActionRunnerStatus Tick(IAiAgent agent, IAiWorldState worldState);
        AiActionRunnerStatus Cancel(string reason = "");
    }

    public sealed class AiSequenceActionRunner : IAiActionRunner
    {
        private AiPlan _plan;
        private int _actionIndex;

        public AiSequenceActionRunner()
        {
            Status = new AiActionRunnerStatus(AiActionRunnerState.Idle, 0, 0, 0, string.Empty);
        }

        public AiActionRunnerStatus Status { get; private set; }

        public bool Start(AiPlan plan, IAiAgent agent, IAiWorldState worldState)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            if (plan == null || !plan.IsValid || plan.Actions.Count == 0)
            {
                _plan = plan;
                _actionIndex = 0;
                Status = new AiActionRunnerStatus(AiActionRunnerState.Failed, 0, 0, 0, "Plan is empty.");
                return false;
            }

            _plan = plan;
            _actionIndex = 0;
            Status = new AiActionRunnerStatus(
                AiActionRunnerState.Running,
                plan.Actions[0].Id,
                _actionIndex,
                plan.Actions.Count,
                string.Empty);
            return true;
        }

        /// <remarks>NoAlloc while a valid plan is running when actions are NoAlloc.</remarks>
        public AiActionRunnerStatus Tick(IAiAgent agent, IAiWorldState worldState)
        {
            if (agent == null)
                throw new ArgumentNullException(nameof(agent));
            if (worldState == null)
                throw new ArgumentNullException(nameof(worldState));

            if (Status.State != AiActionRunnerState.Running)
                return Status;

            IAiAction action = _plan.Actions[_actionIndex];
            if (!action.CanExecute(worldState))
            {
                Status = new AiActionRunnerStatus(
                    AiActionRunnerState.Failed,
                    action.Id,
                    _actionIndex,
                    _plan.Actions.Count,
                    "Action preconditions are not satisfied.");
                return Status;
            }

            action.Apply(worldState);
            _actionIndex++;
            if (_actionIndex >= _plan.Actions.Count)
            {
                Status = new AiActionRunnerStatus(
                    AiActionRunnerState.Completed,
                    action.Id,
                    _plan.Actions.Count - 1,
                    _plan.Actions.Count,
                    string.Empty);
                return Status;
            }

            IAiAction next = _plan.Actions[_actionIndex];
            Status = new AiActionRunnerStatus(
                AiActionRunnerState.Running,
                next.Id,
                _actionIndex,
                _plan.Actions.Count,
                string.Empty);
            return Status;
        }

        public AiActionRunnerStatus Cancel(string reason = "")
        {
            if (Status.State == AiActionRunnerState.Running)
            {
                Status = new AiActionRunnerStatus(
                    AiActionRunnerState.Canceled,
                    Status.ActiveActionId,
                    Status.ActionIndex,
                    Status.ActionCount,
                    reason);
            }

            return Status;
        }
    }

    public readonly struct AiAgentBrainTickResult
    {
        public AiAgentBrainTickResult(
            int frame,
            bool planned,
            int selectedActionId,
            AiPlan plan,
            AiActionRunnerStatus runnerStatus,
            AiSensorManagerDiagnostics sensorDiagnostics,
            string suppressedReason)
        {
            Frame = frame;
            Planned = planned;
            SelectedActionId = selectedActionId;
            Plan = plan;
            RunnerStatus = runnerStatus;
            SensorDiagnostics = sensorDiagnostics;
            SuppressedReason = suppressedReason ?? string.Empty;
        }

        public int Frame { get; }
        public bool Planned { get; }
        public int SelectedActionId { get; }
        public AiPlan Plan { get; }
        public AiActionRunnerStatus RunnerStatus { get; }
        public AiSensorManagerDiagnostics SensorDiagnostics { get; }
        public string SuppressedReason { get; }
    }

    public sealed class AiAgentBrain : IAiAgent
    {
        private readonly IAiPlanner _planner;
        private readonly List<IAiGoal> _goals;
        private readonly List<IAiAction> _actions;

        public AiAgentBrain(
            int id,
            IAiWorldState worldState,
            IAiPlanner planner,
            IEnumerable<IAiGoal> goals,
            IEnumerable<IAiAction> actions,
            IAiSensorManager sensors = null,
            IAiActionRunner actionRunner = null,
            IAiBlackboard blackboard = null)
        {
            if (id < 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            Id = id;
            Blackboard = blackboard;
            WorldState = worldState ?? (IAiWorldState)blackboard ?? new AiWorldState();
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _goals = Copy(goals);
            _actions = Copy(actions);
            Sensors = sensors ?? new AiSensorManager();
            ActionRunner = actionRunner ?? new AiSequenceActionRunner();
            LastTickResult = new AiAgentBrainTickResult(
                -1,
                planned: false,
                selectedActionId: 0,
                plan: null,
                runnerStatus: ActionRunner.Status,
                sensorDiagnostics: Sensors.Diagnostics,
                suppressedReason: string.Empty);
        }

        public int Id { get; }
        public IAiWorldState WorldState { get; }
        public IAiBlackboard Blackboard { get; }
        public IAiSensorManager Sensors { get; }
        public IAiActionRunner ActionRunner { get; }
        public AiAgentBrainTickResult LastTickResult { get; private set; }

        /// <remarks>
        /// LowFreqAlloc when planning creates a new plan; the running-action path is NoAlloc when sensors and actions are NoAlloc.
        /// </remarks>
        public AiAgentBrainTickResult Tick(int frame)
        {
            if (frame < 0)
                throw new ArgumentOutOfRangeException(nameof(frame));

            AiSensorManagerDiagnostics sensorDiagnostics = Sensors.Tick(this, WorldState, frame);
            if (ActionRunner.Status.State == AiActionRunnerState.Running)
            {
                AiActionRunnerStatus status = ActionRunner.Tick(this, WorldState);
                LastTickResult = new AiAgentBrainTickResult(
                    frame,
                    planned: false,
                    selectedActionId: status.ActiveActionId,
                    plan: null,
                    runnerStatus: status,
                    sensorDiagnostics: sensorDiagnostics,
                    suppressedReason: string.Empty);
                return LastTickResult;
            }

            if (!_planner.TryPlan(WorldState, _goals, _actions, out AiPlan plan))
            {
                LastTickResult = new AiAgentBrainTickResult(
                    frame,
                    planned: false,
                    selectedActionId: 0,
                    plan: null,
                    runnerStatus: ActionRunner.Status,
                    sensorDiagnostics: sensorDiagnostics,
                    suppressedReason: "PlanUnavailable");
                return LastTickResult;
            }

            if (plan.Actions.Count == 0)
            {
                LastTickResult = new AiAgentBrainTickResult(
                    frame,
                    planned: false,
                    selectedActionId: 0,
                    plan: plan,
                    runnerStatus: ActionRunner.Status,
                    sensorDiagnostics: sensorDiagnostics,
                    suppressedReason: "EmptyPlan");
                return LastTickResult;
            }

            int selectedActionId = plan.Actions[0].Id;
            bool started = ActionRunner.Start(plan, this, WorldState);
            AiActionRunnerStatus runnerStatus = started
                ? ActionRunner.Tick(this, WorldState)
                : ActionRunner.Status;
            LastTickResult = new AiAgentBrainTickResult(
                frame,
                planned: started,
                selectedActionId: selectedActionId,
                plan: plan,
                runnerStatus: runnerStatus,
                sensorDiagnostics: sensorDiagnostics,
                suppressedReason: started ? string.Empty : "RunnerStartFailed");
            return LastTickResult;
        }

        private static List<T> Copy<T>(IEnumerable<T> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            var list = new List<T>();
            foreach (T value in values)
            {
                if (value != null)
                    list.Add(value);
            }

            return list;
        }
    }
}
