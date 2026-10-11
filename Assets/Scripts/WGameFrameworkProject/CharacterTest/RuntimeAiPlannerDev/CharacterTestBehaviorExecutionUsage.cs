using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Low-frequency conservative control-flow usage, without evaluating current facts.</summary>
    internal sealed class CharacterTestBehaviorExecutionUsage
    {
        private CharacterTestBehaviorExecutionUsage(SortedSet<int> actions, SortedSet<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities)
        { ActionIds = Array.AsReadOnly(actions.ToArray()); Capabilities = Array.AsReadOnly(capabilities.ToArray()); }
        internal ReadOnlyCollection<int> ActionIds { get; }
        internal ReadOnlyCollection<CharacterTestRuntimeAiPlannerBehaviorCapability> Capabilities { get; }

        internal static CharacterTestBehaviorExecutionUsage Analyze(CharacterTestRuntimeAiLabCompiledLogicProgram program, bool basicAttackSource = false)
        {
            var actions = new SortedSet<int>(); var capabilities = new SortedSet<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            var execution = program?.Execution;
            if (execution == null) return new CharacterTestBehaviorExecutionUsage(actions, capabilities);
            var seen = new bool[execution.Timelines.Length]; var queue = new Queue<int>(); queue.Enqueue(execution.EntryTimeline);
            bool activity = false, explicitIdle = false, unknown = false;
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(); if (index < 0 || index >= seen.Length || seen[index]) continue;
                seen[index] = true; var timeline = execution.Timelines[index];
                int returnAt = timeline.DurationFrames, jumpAt = timeline.DurationFrames, unconditionalJumpOffset = int.MaxValue;
                for (int offset = 0; offset < timeline.EventCount; offset++)
                {
                    var evt = execution.Events[execution.TimelineEvents[timeline.EventStart + offset]];
                    if (evt.Condition >= 0 || evt.Clock != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline) continue;
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner) returnAt = Math.Min(returnAt, evt.StartFrame);
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline && evt.StartFrame < jumpAt)
                    { jumpAt = evt.StartFrame; unconditionalJumpOffset = offset; }
                }
                int cutoff = Math.Min(returnAt, jumpAt);
                for (int offset = 0; offset < timeline.EventCount; offset++)
                {
                    int eventIndex = execution.TimelineEvents[timeline.EventStart + offset]; var evt = execution.Events[eventIndex];
                    bool recipeClock = evt.Clock == CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Recipe;
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                    {
                        if (recipeClock || (evt.StartFrame < returnAt && evt.StartFrame <= jumpAt
                            && (evt.StartFrame != jumpAt || offset <= unconditionalJumpOffset)))
                            queue.Enqueue(execution.Flows[evt.Parameter].TargetTimeline);
                        continue;
                    }
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner)
                    {
                        if (recipeClock || (evt.StartFrame <= returnAt && evt.StartFrame < jumpAt))
                        {
                            string id = program.ExecutionDebugMap?.Event(eventIndex);
                            explicitIdle |= program.TacticalSource == null || (id != null && program.TacticalSource.Locations.TryGetValue(id, out var location)
                                && (location.Field == "exit" || !string.IsNullOrEmpty(location.EventId)));
                        }
                        continue;
                    }
                    if (!recipeClock && evt.StartFrame >= cutoff) continue;
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        actions.Add(execution.Actions[evt.Parameter].ActionId); activity = true;
                        // The tactical source contract validates its slot as BasicAttack with real HitTrace data.
                        if (basicAttackSource) capabilities.Add(CharacterTestRuntimeAiPlannerBehaviorCapability.Damage);
                    }
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                    {
                        activity = true; capabilities.Add(CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation);
                        var move = execution.Movements[evt.Parameter];
                        if (move.IsOptimalRange || move.Strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget)
                            capabilities.Add(CharacterTestRuntimeAiPlannerBehaviorCapability.Engage);
                        else if (move.Strategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard
                            || move.Strategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose)
                            capabilities.Add(CharacterTestRuntimeAiPlannerBehaviorCapability.Kite);
                    }
                    else if (evt.Kind == CharacterTestBehaviorExecutionOperations.TurnToTarget) activity = true;
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition) explicitIdle = true;
                    else unknown = true;
                }
            }
            if (!activity && explicitIdle && !unknown) capabilities.Add(CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback);
            return new CharacterTestBehaviorExecutionUsage(actions, capabilities);
        }
    }
}
