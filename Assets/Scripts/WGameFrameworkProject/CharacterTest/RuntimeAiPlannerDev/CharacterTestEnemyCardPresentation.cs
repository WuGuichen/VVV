using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestEnemyIntentState { Unavailable, NotStarted, Running, Completed, Failed }

    /// <summary>Caller-owned copy of accepted AI state. Reading never selects, ticks or submits commands.</summary>
    internal sealed class CharacterTestEnemyCardPresentation
    {
        internal string EntryId { get; private set; }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; private set; }
        internal CharacterTestRuntimeAiLabLogicRuntimeState State { get; private set; }
        internal bool Pending { get; private set; }
        private CharacterTestRuntimeAiLabLogicEvaluatorSnapshot _copy;
        private CharacterTestBehaviorExecutionState _execution;
        private readonly Dictionary<CharacterTestTacticalDescriptionSegment, int> _blocks = new();

        internal void Read(string entry, CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator evaluator, bool pending, bool automatic)
        {
            if (!ReferenceEquals(Program, program)) { _blocks.Clear(); _copy = null; }
            EntryId = entry; Program = program; Pending = pending;
            State = pending ? CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning
                : automatic ? CharacterTestRuntimeAiLabLogicRuntimeState.Running : evaluator.State;
            _execution = null;
            if (!pending && !automatic && program.Execution != null)
            {
                _copy = evaluator.CreateSnapshot(_copy);
                _execution = _copy.ExecutionState;
            }
        }

        // Cold mapping uses the compiler's authored event locations, never description order or action names.
        internal void BindSegments(CharacterTestTacticalDescriptionSegment[] segments)
        {
            _blocks.Clear();
            var source = Program?.TacticalSource;
            if (segments == null || source == null || Program.Execution == null || Program.ExecutionDebugMap == null) return;
            var legacy = source.IsLegacy ? source.CloneLegacySource() : null;
            foreach (var segment in segments)
            {
                int found = -1;
                for (int b = 0; b < Program.Execution.Blocks.Length; b++)
                {
                    var block = Program.Execution.Blocks[b];
                    bool match = false;
                    foreach (var index in Program.Execution.TimelineEvents)
                    {
                        var evt = Program.Execution.Events[index];
                        if (evt.TimelineIndex != block.Timeline || evt.StartFrame < block.Start || evt.EndFrame > block.End) continue;
                        string eventId = Program.ExecutionDebugMap.Event(index);
                        if (legacy == null)
                        {
                            match = source.Locations.TryGetValue(eventId, out var location)
                                && location.NodeId == segment.NodeId && location.BlockId == segment.BlockId;
                        }
                        else
                        {
                            foreach (var timeline in legacy.Timelines)
                            {
                                if (timeline.TimelineId != Program.ExecutionDebugMap.Timeline(block.Timeline)) continue;
                                foreach (var authored in timeline.EventBlocks)
                                    if (authored.BlockId == segment.BlockId && authored.EventIds.Contains(eventId)) match = true;
                            }
                        }
                        if (match) break;
                    }
                    if (!match) continue;
                    if (found >= 0) { found = -1; break; } // Ambiguous authored mapping is unavailable.
                    found = b;
                }
                _blocks[segment] = found;
            }
        }

        internal CharacterTestEnemyIntentState Intent(CharacterTestTacticalDescriptionSegment segment)
        {
            if (segment == null || !_blocks.TryGetValue(segment, out int index) || index < 0)
                return CharacterTestEnemyIntentState.Unavailable;
            if (Pending) return CharacterTestEnemyIntentState.NotStarted;
            if (_execution?.BlockResults == null || index >= _execution.BlockResults.Length)
                return CharacterTestEnemyIntentState.Unavailable;
            return ResolveIntent(_execution.BlockResults[index], State);
        }

        internal static CharacterTestEnemyIntentState ResolveIntent(int result, CharacterTestRuntimeAiLabLogicRuntimeState state)
        {
            if (result == 2) return CharacterTestEnemyIntentState.Completed;
            if (result == 3) return CharacterTestEnemyIntentState.Failed;
            if (result == 0) return CharacterTestEnemyIntentState.NotStarted;
            if (result != 1) return CharacterTestEnemyIntentState.Unavailable;
            return state == CharacterTestRuntimeAiLabLogicRuntimeState.Running
                || state == CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning ? CharacterTestEnemyIntentState.Running
                : state == CharacterTestRuntimeAiLabLogicRuntimeState.Failed ? CharacterTestEnemyIntentState.Failed
                : CharacterTestEnemyIntentState.Unavailable;
        }

        internal int RemainingDelayFrames(CharacterTestTacticalDescriptionSegment segment)
        {
            if (Intent(segment) != CharacterTestEnemyIntentState.Running) return -1;
            int index = _blocks[segment]; var block = Program.Execution.Blocks[index];
            if (block.Completion != CharacterTestBehaviorBlockCompletion.Delay || _execution.BlockStarts[index] < 0) return -1;
            return (int)Math.Max(0L, (long)block.Duration - Math.Max(0L, (long)_execution.LastWorldFrame - _execution.BlockStarts[index]));
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay
    {
        internal bool ReadEnemyCard(int entityId, CharacterTestEnemyCardPresentation destination)
        {
            var agent = FindAgent(entityId);
            if (agent == null || destination == null) return false;
            bool pending = agent.ActiveBindingIndex < 0;
            int index = pending ? agent.PendingBindingIndex : agent.ActiveBindingIndex;
            if (index < 0 || index >= _bindings.Length) return false;
            var binding = _bindings[index];
            destination.Read(binding.EntryId, binding.Program, agent.Evaluators[index], pending, agent.AutomaticExecutionId != 0);
            return true;
        }
    }
}
