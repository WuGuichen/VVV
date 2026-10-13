using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Cold proof over the final immutable execution program. This is an upper bound,
    /// not a second executor or an authoring restriction. Unsupported combinations retain old naked-card semantics.</summary>
    internal sealed class CharacterTestFixedActionBudgetProof
    {
        // Proof-work limits only. No published card-count, loop or timeline gameplay limit is introduced.
        private const int MaximumProofSteps = 65536;
        private readonly CharacterTestBehaviorExecutionProgram _program;
        private readonly Dictionary<int, int> _costs;
        private CharacterTestFixedActionBudgetProof(CharacterTestBehaviorExecutionProgram program,
            Dictionary<int, int> costs, int maximum)
        { _program = program; _costs = costs; MaximumMomentum = maximum; }
        internal int MaximumMomentum { get; }
        internal long ProgramIdentity => _program.Identity;
        internal bool Matches(CharacterTestBehaviorExecutionProgram program) => ReferenceEquals(_program, program);
        internal bool TryGetActionCost(int actionId, out int cost) => _costs.TryGetValue(actionId, out cost);
        internal bool UsesOnlyFrozenBindings(IReadOnlyDictionary<string, int> bindings)
        {
            foreach (int action in _costs.Keys)
            {
                bool found = false;
                foreach (var binding in bindings) if (binding.Value == action) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        /// <remarks>LowFreqAlloc. The resolver must query the real action/Ability definition owner and reject
        /// unsupported resource kinds; no current balance, reward or wall-clock tick is used by this proof.
        /// Conditions are conservatively unknown. Timeline alternatives are mutually exclusive; action conditions
        /// never prove exclusion because the evaluator ignores them for scheduled CharacterAction events.</remarks>
        internal static bool TryCreate(CharacterTestBehaviorExecutionProgram program, Func<int, int?> resolveMomentumCost,
            out CharacterTestFixedActionBudgetProof proof, out string issue)
        {
            proof = null; issue = null;
            if (program == null || resolveMomentumCost == null) { issue = "budget:MissingProgramOrCostOwner"; return false; }
            var builder = new Builder(program, resolveMomentumCost);
            try
            {
                int maximum;
                bool supported = program.Blocks.Length == 0 ? builder.TryTimeline(program.EntryTimeline, out maximum)
                    : builder.TryBlockTimeline(program.EntryTimeline, out maximum);
                if (!supported) { issue = builder.Issue; return false; }
                proof = new CharacterTestFixedActionBudgetProof(program, builder.Costs, maximum); return true;
            }
            catch (OverflowException) { issue = "budget:Overflow"; return false; }
        }

        private sealed class Builder
        {
            private readonly CharacterTestBehaviorExecutionProgram _program;
            private readonly Func<int, int?> _resolve;
            private readonly byte[] _visiting;
            private readonly int[] _budgets;
            private int _steps;
            internal readonly Dictionary<int, int> Costs = new Dictionary<int, int>();
            internal string Issue { get; private set; }
            internal Builder(CharacterTestBehaviorExecutionProgram program, Func<int, int?> resolve)
            { _program = program; _resolve = resolve; _visiting = new byte[program.Timelines.Length]; _budgets = new int[_visiting.Length]; }
            private bool Fail(string issue) { Issue = issue; return false; }
            private bool IsFlow(in CharacterTestBehaviorExecutionEvent evt, int frame)
                => evt.Timing == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                    ? frame == evt.StartFrame : frame >= evt.StartFrame && frame < evt.EndFrame;

            // Goal blocks hold or advance the cursor; completion may skip events but cannot create another
            // entry of a timeline event. Sum every action once (including possibly skipped actions), plus the
            // largest reachable DAG continuation. Reject rewinds/shared clocks rather than invent a card rule.
            internal bool TryBlockTimeline(int index, out int budget)
            {
                budget = 0;
                if (_visiting[index] == 1) return Fail("budget:UnsupportedTimelineReentry");
                if (_visiting[index] == 2) { budget = _budgets[index]; return true; }
                int depth = 0; for (int i = 0; i < _visiting.Length; i++) if (_visiting[i] == 1) depth++;
                if (depth >= 128) return Fail("budget:ProofComplexityExceeded");
                _visiting[index] = 1; int own = 0, continuation = 0;
                var timeline = _program.Timelines[index];
                for (int i = 0; i < timeline.EventCount; i++)
                {
                    if (++_steps > MaximumProofSteps) return Fail("budget:ProofComplexityExceeded");
                    var evt = _program.Events[_program.TimelineEvents[timeline.EventStart + i]];
                    if (evt.Shared || evt.Clock != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline)
                        return Fail("budget:UnsupportedSharedOrRecipeClock");
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                        return Fail("budget:UnsupportedBlockRewind");
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        if (evt.CooldownFrames != 0) return Fail("budget:UnsupportedActionEventCooldown");
                        int action = _program.Actions[evt.Parameter].ActionId;
                        if (!Costs.TryGetValue(action, out int cost))
                        {
                            int? resolved = _resolve(action);
                            if (!resolved.HasValue || resolved.Value < 0) return Fail("budget:UnsupportedActionCost");
                            cost = resolved.Value; Costs.Add(action, cost);
                        }
                        own = checked(own + cost);
                    }
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                    {
                        if (!TryBlockTimeline(_program.Flows[evt.Parameter].TargetTimeline, out int child)) return false;
                        continuation = Math.Max(continuation, child);
                    }
                    else if (evt.Kind != CharacterTestBehaviorExecutionOperations.CompleteBlock
                        && evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                        && evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                        && evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                        && evt.Kind != CharacterTestBehaviorExecutionOperations.TurnToTarget)
                        return Fail("budget:UnsupportedOperation");
                }
                budget = checked(own + continuation); _budgets[index] = budget; _visiting[index] = 2; return true;
            }

            internal bool TryTimeline(int index, out int budget)
            {
                budget = 0;
                if (_visiting[index] == 1) return Fail("budget:UnsupportedTimelineReentry");
                if (_visiting[index] == 2) { budget = _budgets[index]; return true; }
                // Avoid recursion exhaustion independently of content legality.
                int visiting = 0;
                for (int i = 0; i < _visiting.Length; i++) if (_visiting[i] == 1) visiting++;
                if (visiting >= 128) return Fail("budget:ProofComplexityExceeded");
                _visiting[index] = 1;
                var timeline = _program.Timelines[index];
                var loops = new List<int>();
                var childBudgets = new Dictionary<int, int>();
                for (int offset = 0; offset < timeline.EventCount; offset++)
                {
                    int eventIndex = _program.TimelineEvents[timeline.EventStart + offset];
                    var evt = _program.Events[eventIndex];
                    if (evt.Shared || evt.Clock != CharacterTestRuntimeAiLabBehaviorRecipeClockOrigin.Timeline)
                        return Fail("budget:UnsupportedSharedOrRecipeClock");
                    if (evt.Kind == CharacterTestBehaviorExecutionOperations.CompleteBlock)
                        return Fail("budget:UnsupportedExecutionBlocks");
                    if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    {
                        // Cooldown can suppress the last eligible action and expose an earlier action at that frame.
                        // This subset does not model world-time cooldown histories; it must reject rather than undercount.
                        if (evt.CooldownFrames != 0) return Fail("budget:UnsupportedActionEventCooldown");
                        int action = _program.Actions[evt.Parameter].ActionId;
                        if (!Costs.ContainsKey(action))
                        {
                            int? cost = _resolve(action);
                            if (!cost.HasValue || cost.Value < 0) return Fail("budget:UnsupportedActionCost");
                            Costs.Add(action, cost.Value);
                        }
                    }
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                    {
                        int target = _program.Flows[evt.Parameter].TargetTimeline;
                        if (!TryTimeline(target, out int child)) return false;
                        childBudgets[eventIndex] = child;
                    }
                    else if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion)
                    {
                        if (_program.Flows[evt.Parameter].MaxLoopRepeats <= 0)
                            return Fail("budget:UnsupportedUnboundedLoop");
                        foreach (int otherIndex in loops)
                        {
                            var other = _program.Events[otherIndex];
                            if (evt.StartFrame < other.EndFrame && other.StartFrame < evt.EndFrame)
                                return Fail("budget:UnsupportedNestedOrOverlappingLoop");
                        }
                        loops.Add(eventIndex);
                    }
                    else if (evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget
                        && evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner
                        && evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition
                        && evt.Kind != CharacterTestBehaviorExecutionOperations.TurnToTarget)
                        return Fail("budget:UnsupportedOperation");
                }

                var counts = new int[_program.Events.Length];
                var repeats = new int[_program.Events.Length];
                int frame = 0, previous = -1, paidPrefix = 0, maximum = 0;
                while (frame < timeline.DurationFrames)
                {
                    if (++_steps > MaximumProofSteps) return Fail("budget:ProofComplexityExceeded");
                    int actionIndex = -1, bestJump = -1;
                    bool forcedJump = false, forcedStop = false;
                    for (int offset = 0; offset < timeline.EventCount; offset++)
                    {
                        int eventIndex = _program.TimelineEvents[timeline.EventStart + offset];
                        var evt = _program.Events[eventIndex];
                        if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                        {
                            if (frame == evt.StartFrame && frame != previous
                                && (evt.MaxExecutionsPerPass == 0 || counts[eventIndex] < evt.MaxExecutionsPerPass))
                                actionIndex = eventIndex; // Last eligible action wins, as in Sample.
                            continue;
                        }
                        if (!IsFlow(evt, frame)) continue;
                        if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner && evt.Condition < 0)
                        { forcedStop = true; break; }
                        if (evt.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline && !forcedJump)
                        {
                            bestJump = Math.Max(bestJump, childBudgets[eventIndex]);
                            if (evt.Condition < 0) forcedJump = true; // First eligible unconditional jump masks later jumps.
                        }
                    }
                    if (forcedStop) { maximum = Math.Max(maximum, paidPrefix); break; }
                    if (bestJump >= 0) maximum = Math.Max(maximum, checked(paidPrefix + bestJump));
                    if (forcedJump) break; // Jump suppresses this frame's action.
                    if (actionIndex >= 0)
                    {
                        counts[actionIndex]++;
                        int action = _program.Actions[_program.Events[actionIndex].Parameter].ActionId;
                        paidPrefix = checked(paidPrefix + Costs[action]);
                        // Failed receipt continuation can select a conditional jump in the same world frame.
                        // Its paid edge is actually zero; retaining this prefix is a conservative upper bound.
                        if (bestJump >= 0) maximum = Math.Max(maximum, checked(paidPrefix + bestJump));
                    }
                    maximum = Math.Max(maximum, paidPrefix);
                    previous = frame;
                    bool rewind = false;
                    foreach (int loopIndex in loops)
                    {
                        var loop = _program.Events[loopIndex];
                        if (frame != loop.EndFrame - 1 || repeats[loopIndex] >= _program.Flows[loop.Parameter].MaxLoopRepeats) continue;
                        repeats[loopIndex]++;
                        for (int e = 0; e < _program.Events.Length; e++)
                        {
                            var reset = _program.Events[e];
                            if (reset.TimelineIndex == index && !reset.Shared && reset.StartFrame >= loop.StartFrame && reset.EndFrame <= loop.EndFrame)
                                counts[e] = 0; // Loop quota itself is retained, matching Advance.
                        }
                        frame = loop.StartFrame; rewind = true; break;
                    }
                    if (!rewind) frame++;
                }
                _visiting[index] = 2; _budgets[index] = maximum; budget = maximum; return true;
            }
        }
    }
}
