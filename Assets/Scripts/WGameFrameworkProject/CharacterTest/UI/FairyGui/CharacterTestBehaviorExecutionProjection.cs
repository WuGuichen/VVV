using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Read-only compatibility shape for existing timeline inspectors; never passed to an executor.</summary>
    internal static class CharacterTestBehaviorExecutionProjection
    {
        private static readonly ConditionalWeakTable<CharacterTestRuntimeAiLabCompiledLogicProgram, Holder> Cache =
            new ConditionalWeakTable<CharacterTestRuntimeAiLabCompiledLogicProgram, Holder>();
        private static readonly ConditionalWeakTable<CharacterTestRuntimeAiLabCompiledLogicProgram, Holder>.CreateValueCallback Factory = Build;
        public static CharacterTestRuntimeAiLabCompiledRecipeV6 Get(CharacterTestRuntimeAiLabCompiledLogicProgram source)
        {
            if (source == null) return null;
            if (source.Execution == null) return source.RecipeV6;
            return Cache.GetValue(source, Factory).Recipe;
        }
        private sealed class Holder { public CharacterTestRuntimeAiLabCompiledRecipeV6 Recipe; }
        private static Holder Build(CharacterTestRuntimeAiLabCompiledLogicProgram source)
        {
            var program = source.Execution; var map = source.ExecutionDebugMap;
            var events = new CharacterTestRuntimeAiLabCompiledRecipeV6Event[program.Events.Length];
            var timelines = new CharacterTestRuntimeAiLabCompiledRecipeV6Timeline[program.Timelines.Length];
            for (int i = 0; i < events.Length; i++)
            {
                var evt = program.Events[i];
                int target = evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline ? program.Flows[evt.Parameter].TargetTimeline : -1;
                var projection = new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                {
                    EventId = map?.Event(i) ?? string.Empty, TimelineId = map?.Timeline(evt.TimelineIndex) ?? string.Empty,
                    StartFrame = evt.StartFrame, DurationFrames = evt.DurationFrames, TimingKind = evt.Timing,
                    MaxExecutionsPerPass = evt.MaxExecutionsPerPass, CooldownFrames = evt.CooldownFrames,
                    ClockOrigin = evt.Clock, LegacySharedEventId = evt.Shared ? map?.Event(i) ?? "shared" : string.Empty,
                    Priority = evt.Priority, Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                    {
                        Kind = evt.Kind, MaxLoopRepeats = evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion
                            ? program.Flows[evt.Parameter].MaxLoopRepeats : 0
                    }
                };
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    projection.Operation.CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(program.Actions[evt.Parameter].ActionId);
                if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                {
                    var move = program.Movements[evt.Parameter];
                    projection.Operation.MovementStrategy = move.Strategy; projection.Operation.MovementTargetKind = move.Target;
                    projection.Operation.MoveTargetEntityId = move.EntityId; projection.Operation.MoveTargetPositionX = move.X; projection.Operation.MoveTargetPositionZ = move.Z;
                    projection.Operation.MoveMinDistance = move.Minimum; projection.Operation.MoveMaxDistance = move.Maximum; projection.Operation.MoveSpeedMultiplier = move.Speed;
                }
                events[i] = new CharacterTestRuntimeAiLabCompiledRecipeV6Event(i, evt.TimelineIndex, projection,
                    Condition(program, map, evt.Condition), null, target);
            }
            for (int i = 0; i < timelines.Length; i++)
            {
                var timeline = program.Timelines[i];
                var local = new CharacterTestRuntimeAiLabCompiledRecipeV6Event[timeline.EventCount];
                for (int j = 0; j < local.Length; j++) local[j] = events[program.TimelineEvents[timeline.EventStart + j]];
                timelines[i] = new CharacterTestRuntimeAiLabCompiledRecipeV6Timeline(i, map?.Timeline(i) ?? string.Empty,
                    Condition(program, map, timeline.EntryCondition), null, local, timeline.DurationFrames);
            }
            return new Holder { Recipe = new CharacterTestRuntimeAiLabCompiledRecipeV6(program.EntryTimeline,
                Condition(program, map, program.ActivationCondition), timelines, events) };
        }
        private static CharacterTestRuntimeAiLabCompiledRecipeV6Condition Condition(CharacterTestBehaviorExecutionProgram program,
            CharacterTestBehaviorExecutionDebugMap map, int index)
        {
            if (index < 0) return null;
            var node = program.Conditions[index];
            var children = new CharacterTestRuntimeAiLabCompiledRecipeV6Condition[node.ChildCount];
            for (int i = 0; i < children.Length; i++) children[i] = Condition(program, map, program.Children[node.ChildStart + i]);
            CharacterTestRuntimeAiLabCompiledCondition? predicate = node.Predicate >= 0 ? program.Predicates[node.Predicate].AsFactRequest() : (CharacterTestRuntimeAiLabCompiledCondition?)null;
            return new CharacterTestRuntimeAiLabCompiledRecipeV6Condition(node.Kind, node.Subject, predicate, node.SourceEvent, node.Signal, children);
        }
    }
}
