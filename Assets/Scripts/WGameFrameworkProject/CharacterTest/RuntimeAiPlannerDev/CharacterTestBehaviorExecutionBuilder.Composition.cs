using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestBehaviorExecutionBuilder
    {
        private readonly List<string> _flowTargets = new List<string>();
        private readonly Dictionary<int, int> _loopRepeats = new();
        internal void AddLoop(string id, string timeline, int frame, int duration, int repeats, int condition = -1, int priority = 0)
        {
            int parameter = _flowTargets.Count; _flowTargets.Add(null); _loopRepeats.Add(parameter, repeats);
            AddEvent(id, timeline, frame, duration, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion, parameter, condition, priority);
        }

        internal void AddJump(string id, string timeline, int frame, string target, int condition = -1, int priority = 0, int duration = 1)
        {
            int parameter = _flowTargets.Count;
            _flowTargets.Add(target);
            AddEvent(id, timeline, frame, duration, CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                parameter, condition, priority);
        }

        private CharacterTestBehaviorExecutionFlow[] BuildFlows(Dictionary<string, int> timelines)
        {
            var result = new CharacterTestBehaviorExecutionFlow[_flowTargets.Count];
            for (int i = 0; i < result.Length; i++)
            {
                if (_loopRepeats.TryGetValue(i, out int repeats)) { result[i] = new CharacterTestBehaviorExecutionFlow(-1, repeats); continue; }
                if (!timelines.TryGetValue(_flowTargets[i], out int target))
                    throw new ArgumentException("Unknown composition successor: " + _flowTargets[i]);
                result[i] = new CharacterTestBehaviorExecutionFlow(target, 0);
            }
            return result;
        }

        // A constant owner prefix preserves internal ordinal sampling order and prevents aliasing.
        internal static string CompositionId(string owner, string local) => owner.Length + ":" + owner + ":" + local;

        internal int ImportConditions(CharacterTestRuntimeAiLabCompiledLogicProgram source, string owner)
        {
            var program = source.Execution;
            int conditionOffset = _conditions.Count, predicateOffset = _predicates.Count, childOffset = _children.Count;
            foreach (var predicate in program.Predicates) _predicates.Add(predicate);
            foreach (int child in program.Children) _children.Add(child + conditionOffset);
            for (int i = 0; i < program.Conditions.Length; i++)
            {
                var value = program.Conditions[i];
                _conditionIds.Add(CompositionId(owner, "condition:" + i));
                if (value.SourceEvent >= 0)
                    _conditionEventReferences.Add(_conditions.Count,
                        CompositionId(owner, "event:" + source.ExecutionDebugMap.Event(value.SourceEvent)));
                _conditions.Add(new CharacterTestBehaviorExecutionCondition(value.Kind, value.Subject,
                    value.Predicate < 0 ? -1 : value.Predicate + predicateOffset,
                    value.ChildStart + childOffset, value.ChildCount, -1, value.Signal));
            }
            return conditionOffset;
        }

        internal int ImportSingleTimeline(CharacterTestRuntimeAiLabCompiledLogicProgram source, string owner, string successor)
        {
            var program = source.Execution;
            if (program.Timelines.Length != 1 || program.ActivationCondition >= 0)
                throw new ArgumentException("Composition requires one timeline without a shared activation condition or internal jumps.");
            int conditions = ImportConditions(source, owner);
            var timeline = program.Timelines[0];
            int boundary = timeline.DurationFrames;
            AddTimeline(owner, successor == null ? boundary : checked(boundary + 1), timeline.EntryCondition < 0 ? -1 : conditions + timeline.EntryCondition);
            foreach (int index in program.TimelineEvents)
            {
                var evt = program.Events[index];
                string id = CompositionId(owner, "event:" + source.ExecutionDebugMap.Event(index));
                int condition = evt.Condition < 0 ? -1 : conditions + evt.Condition;
                switch (evt.Kind)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion:
                        AddLoop(id, owner, evt.StartFrame, evt.DurationFrames, program.Flows[evt.Parameter].MaxLoopRepeats, condition, evt.Priority); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline:
                        if (program.Flows[evt.Parameter].TargetTimeline != 0) throw new ArgumentException("Shared jump leaves its Timeline.");
                        AddJump(id, owner, evt.StartFrame, owner, condition, evt.Priority, evt.DurationFrames); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction:
                        AddAction(id, owner, evt.StartFrame, evt.DurationFrames, program.Actions[evt.Parameter].ActionId, condition, evt.Priority); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget:
                        AddMovement(id, owner, evt.StartFrame, evt.DurationFrames, program.Movements[evt.Parameter], condition, evt.Priority); break;
                    case CharacterTestBehaviorExecutionOperations.TurnToTarget:
                        AddTurn(id, owner, evt.StartFrame, evt.DurationFrames, program.Turns[evt.Parameter], condition, evt.Priority); break;
                    case CharacterTestBehaviorExecutionOperations.CompleteBlock:
                        AddCompleteBlock(id, owner, evt.StartFrame, evt.DurationFrames, evt.Parameter, condition, evt.Priority); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition:
                        AddGate(id, owner, evt.StartFrame, condition, evt.Priority); break;
                    case CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner:
                        AddReturn(id, owner, evt.StartFrame, evt.DurationFrames, condition, evt.Priority); break;
                    default: throw new ArgumentException("Unsupported shared operation: " + evt.Kind);
                }
                SetRepetition(id, evt.MaxExecutionsPerPass, evt.CooldownFrames);
                SetTiming(id, evt.Timing);
            }
            if (successor != null) AddJump(CompositionId(owner, "$next"), owner, boundary, successor);
            for (int i = 0; i < program.Blocks.Length; i++)
            {
                var block = program.Blocks[i];
                AddBlock(CompositionId(owner, "block:" + i), owner, block.Start, block.Duration, block.Completion,
                    block.CompletionEvent < 0 ? null : CompositionId(owner, "event:" + source.ExecutionDebugMap.Event(block.CompletionEvent)),
                    block.SuccessCondition < 0 ? -1 : conditions + block.SuccessCondition, block.HasAction,
                    block.PreparationTimeoutFrames, block.CompletionTimeoutFrames, block.RequireLivingTarget, block.ContinueToActionOnPreparationTimeout, block.PassiveWait);
            }
            return boundary;
        }
    }
}
