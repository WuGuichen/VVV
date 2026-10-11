using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestTacticalAuthoringCompiler
    {
        internal static bool TryFacingThreshold(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root, out float degrees)
        {
            degrees = 0;
            if (root?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || root.Children.Count != 3
                || !root.Children.Exists(n => n?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                    && n.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget && n.Children.Count == 1
                    && n.Children[0]?.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle
                    && n.Children[0].Predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between
                    && n.Children[0].Predicate.Value == 0 && n.Children[0].Predicate.MaxValue == 180)) return false;
            var scope = root.Children.Find(n => n?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                && n.Subject == CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget
                && n.Children.Count == 1 && n.Children[0]?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Not);
            var not = scope?.Children[0];
            if (not?.Children.Count != 1) return false;
            var fact = not.Children[0];
            if (fact?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                || fact.Predicate?.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle
                || fact.Predicate.Operator != CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual) return false;
            degrees = fact.Predicate.Value;
            return degrees > 0 && degrees <= 180 && !float.IsNaN(degrees) && !float.IsInfinity(degrees);
        }
        internal static bool ContainsAttackWaitFact(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
        {
            if (node == null) return false;
            if (node.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetCurrentAttackWindowEnded) return true;
            foreach (var child in node.Children) if (ContainsAttackWaitFact(child)) return true;
            return false;
        }

        internal static void ValidateDistanceData(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalEventBlock block)
        {
            var data = block?.DistanceData ?? throw new ArgumentException("Referenced intent does not publish configuration distance data.");
            if (data.Kind == "Fixed")
            {
                if (!string.IsNullOrEmpty(data.SourceActionEventId) || float.IsNaN(data.Minimum) || float.IsInfinity(data.Minimum)
                    || float.IsNaN(data.Maximum) || float.IsInfinity(data.Maximum) || data.Minimum < 0 || data.Maximum < data.Minimum
                    || data.Metric != "RootPlanarDistance" && data.Metric != "HurtboxSurfaceClearance")
                    throw new ArgumentException("Fixed intent distance requires an explicit finite interval and metric.");
            }
            else if (data.Kind == "BoundActionRange")
            {
                var action = source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e?.EventId == data.SourceActionEventId);
                if (data.Minimum != 0 || data.Maximum != 0 || data.Metric != "HurtboxSurfaceClearance"
                    || action?.Operation?.Kind != "CharacterAction" || !block.EventIds.Contains(action.EventId)
                    || source.Slots.FirstOrDefault(s => s?.SlotId == action.Operation.SlotId)?.IsFunctional == true)
                    throw new ArgumentException("Bound action distance must explicitly publish its own action configuration range.");
            }
            else throw new ArgumentException("Distance data must be Fixed or BoundActionRange configuration; execution results are unavailable.");
        }

        internal static CharacterTestTacticalDistanceData ResolveDistanceData(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, string blockId)
        {
            var block = source.Timelines.SelectMany(t => t.EventBlocks).FirstOrDefault(b => b?.BlockId == blockId);
            ValidateDistanceData(source, block);
            var data = block.DistanceData;
            if (data.Kind == "Fixed") return new CharacterTestTacticalDistanceData { Metric = data.Metric, Minimum = data.Minimum, Maximum = data.Maximum };
            if (inputs == null || !inputs.TryValidatePreparedAction(out _)) throw new ArgumentException("Distance source requires current prepared action configuration.");
            var action = source.Timelines.SelectMany(t => t.Events).Single(e => e.EventId == data.SourceActionEventId);
            var range = inputs.SlotRange(action.Operation.SlotId);
            return new CharacterTestTacticalDistanceData { Metric = "HurtboxSurfaceClearance", Minimum = (float)range.MinDistance.Value, Maximum = (float)range.MaxDistance.Value };
        }

        private sealed partial class Context
        {
            private sealed class IndexedBlock
            {
                internal CharacterTestTacticalEventBlock Source;
                internal string Timeline;
            }
            private readonly Dictionary<string, IndexedBlock> _blocks = new(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _eventBlocks = new(StringComparer.Ordinal);
            private CharacterTestTacticalTimingEvaluation _timing;

            private readonly IReadOnlyDictionary<string, CharacterTestTacticalDistanceData> _distanceInputs;
            private CharacterTestTacticalDistanceData BoundIntentDistance(CharacterTestTacticalPredicateBinding binding, string timeline)
            {
                if (!string.IsNullOrEmpty(binding.SourceBlockId))
                {
                    if (!_blocks.TryGetValue(binding.SourceBlockId, out var source) || timeline != null && source.Timeline != timeline)
                        throw new ArgumentException("Distance reference must select an explicit configuration export in the same timeline.");
                    return ResolveDistanceData(_source, _inputs, binding.SourceBlockId);
                }
                if (_distanceInputs == null || !_distanceInputs.TryGetValue(binding.NodeId, out var supplied) || supplied?.Kind != "Fixed")
                    throw new ArgumentException("Missing required intent configuration distance reference: " + binding.NodeId);
                // External values were resolved by the author compiler, never sampled from execution state.
                ValidateDistanceData(_source, new CharacterTestTacticalEventBlock { DistanceData = supplied });
                return supplied;
            }

            private void ValidatePurposeWait(CharacterTestTacticalEventBlock block, string path)
            {
                if (block.Completion != "Delay" && block.Completion != "Distance") return;
                bool delay = block.Completion == "Delay";
                if (!block.PassiveWait || block.Timing == null || block.EventIds.Count != 1
                    || !_events.TryGetValue(block.CompletionEventId ?? string.Empty, out var gate)
                    || gate.Source.Operation?.Kind != "WaitForCondition" || gate.Source.Start?.Space != "Block" || gate.Source.Start.Frame != 0
                    || gate.Source.Start.Kind != "Absolute" || !string.IsNullOrEmpty(block.SuccessConditionEventId)
                    || block.RequireLivingTarget == delay
                    || (delay ? block.Timing.MinimumFrames != CharacterTestDeterministicUnits.DefaultTickRate * 2
                        || block.Timing.DurationFrames != CharacterTestDeterministicUnits.DefaultTickRate * 2 || gate.Source.ConditionRoot != null
                        || block.DistanceData != null : block.Timing.MinimumFrames != 1 || block.Timing.DurationFrames < 1
                            || !IsDistanceWaitGate(gate.Source.ConditionRoot)))
                    Error("PurposeWaitContract", path, "Purpose wait requires its own passive gate and fixed completion contract.");
            }

            private bool IsDistanceWaitGate(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root)
            {
                if (root?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || root.Children.Count != 2) return false;
                bool alive = false, distance = false;
                foreach (var scope in root.Children)
                {
                    if (scope?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope
                        || scope.Subject != CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget || scope.Children.Count != 1) return false;
                    var leaf = scope.Children[0];
                    if (leaf?.Kind != CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate || leaf.Children.Count != 0) return false;
                    if (leaf.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetState
                        && leaf.Predicate.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                        && leaf.Predicate.AllowedTargetPhases == (uint)CharacterTestRuntimeAiPlannerTargetStateMask.Alive) alive = true;
                    else if (leaf.Predicate == null && _bindings.TryGetValue(leaf.NodeId, out var binding) && binding.Kind == "IntentDistanceRange") distance = true;
                    else return false;
                }
                return alive && distance;
            }

            private void ValidateBlocks()
            {
                foreach (var timeline in _source.Timelines)
                {
                    if (timeline == null) continue;
                    if (timeline.EventBlocks == null) { Error("BlockCollection", "$.eventBlocks", "Event blocks cannot be null."); continue; }
                    if (timeline.EventBlocks.Count > 0 && !_source.HasBlocksVersion)
                        Error("BlockVersion", "$.eventBlocks", "Event blocks require source v3.");
                    foreach (var block in timeline.EventBlocks)
                    {
                        string path = "$.eventBlocks." + block?.BlockId;
                        if (block == null || !ValidId(block.BlockId) || _events.ContainsKey(block.BlockId)
                            || !_blocks.TryAdd(block.BlockId, new IndexedBlock { Source = block, Timeline = timeline.TimelineId }))
                        { Error("BlockIdentity", path, "Missing or duplicate block identity."); continue; }
                        if (block.EventIds == null) { Error("BlockMembers", path, "A member list is required."); continue; }
                        if (block.MaximumGuardFrames < 0 || block.MaximumGuardFrames > 0
                            && (block.Completion != "Action" || block.EventIds.Count != 1
                                || !_events.TryGetValue(block.CompletionEventId, out var guardEvent)
                                || !_source.Slots.Exists(slot => slot != null
                                    && slot.SlotId == guardEvent.Source.Operation?.SlotId && slot.Resolver == "ordinaryGuard")))
                            Error("BlockGuardLimit", path, "防御维持上限只用于独立普通防御动作段，必须为正数；零值保持原动作完成语义。");
                        if (block.PreparationTimeoutFrames < 0 || block.CompletionTimeoutFrames < 0
                            || (block.PreparationTimeoutFrames > 0 || block.CompletionTimeoutFrames > 0) && block.Completion != "Action"
                            || block.RequireLivingTarget && block.Completion != "Action" && block.Completion != "Condition" && block.Completion != "Facing" && block.Completion != "Distance"
                            || (block.PreparationTimeoutFrames > 0) != (block.CompletionTimeoutFrames > 0)
                            || block.ContinueToActionOnPreparationTimeout && (block.Completion != "Action" || block.PreparationTimeoutFrames <= 0))
                            Error("BlockActionLimits", path, "动作封装的准备与执行保护时限必须成对设置为正数。");
                        if (block.PassiveWait && block.Completion != "Condition" && block.Completion != "Delay" && block.Completion != "Distance")
                            Error("BlockPassiveWait", path, "Passive wait is only valid on a condition block.");
                        if (!Enum.TryParse<CharacterTestBehaviorBlockCompletion>(block.Completion, out var completion)
                            || !Enum.IsDefined(typeof(CharacterTestBehaviorBlockCompletion), completion))
                            Error("BlockCompletion", path, "未知封装完成规则。");
                        if (block.Completion != "Timed" && (!block.EventIds.Contains(block.CompletionEventId)
                            || !_events.TryGetValue(block.CompletionEventId, out var completionEvent)
                            || completionEvent.Source.Operation?.Kind != (block.Completion == "Arrived" ? "Movement" : block.Completion == "Condition" || block.Completion == "Facing" || block.Completion == "Delay" || block.Completion == "Distance" ? "WaitForCondition" : "CharacterAction")))
                            Error("BlockCompletionEvent", path, "完成规则必须引用本封装中的移动或动作事件。");
                        if (block.Completion == "Condition" && (!block.RequireLivingTarget || block.Timing == null
                            || block.Timing.MinimumFrames != 1 || block.EventIds.Count != 1))
                            Error("BlockWaitContract", path, "条件等待需要独立等待事件、有效目标，最小预算固定一帧，等待上限至少一帧。");
                        ValidatePurposeWait(block, path);
                        if (block.DistanceData != null)
                        {
                            try { ValidateDistanceData(_source, block); }
                            catch (ArgumentException error) { Error("IntentDistanceData", path, error.Message); }
                        }
                        if (block.Completion == "Facing" && (!block.RequireLivingTarget || block.Timing == null
                            || block.Timing.MinimumFrames != 1 || block.EventIds.Count != 2
                            || !_events.TryGetValue(block.CompletionEventId, out var facingGate)
                            || !TryFacingThreshold(facingGate.Source.ConditionRoot, out _)
                            || !block.EventIds.Exists(id => _events.TryGetValue(id, out var item)
                                && item.Source.Operation?.Kind == "TurnToTarget" && item.Source.Operation.Target == "currentCombatTarget")))
                            Error("BlockFacingContract", path, "面向目标意图需要转向当前目标与朝向等待，最小预算一帧，等待上限至少一帧。");
                        if (!string.IsNullOrEmpty(block.SuccessConditionEventId)
                            && (block.Completion != "Arrived" || !_events.TryGetValue(block.SuccessConditionEventId, out var successEvent) || successEvent.Timeline != timeline.TimelineId))
                            Error("BlockSuccessCondition", path, "成功条件引用的事件必须位于同一时间轴。");
                        if (block.Timing != null && (!_source.HasTimingVersion || block.Timing.MinimumFrames < 0 || block.Timing.DurationFrames < block.Timing.MinimumFrames))
                            Error("BlockTiming", path, "最小持续帧不能为负，制作长度不能小于最小持续帧；时间规则需要 v4。");
                        foreach (string member in block.EventIds)
                        {
                            if (member == null || !_events.TryGetValue(member, out var ev) || ev.Timeline != timeline.TimelineId)
                            { Error("BlockMemberMissing", path, "Block members must be events in the same Timeline: " + member); continue; }
                            if (!_eventBlocks.TryAdd(member, block.BlockId))
                                Error("BlockMemberDuplicate", path, "An event can belong to only one block: " + member);
                        }
                    }
                }
                foreach (var block in _blocks.Values)
                    ValidateFrame(new IndexedEvent { Source = new CharacterTestTacticalAuthoringEvent { EventId = block.Source.BlockId },
                        Timeline = block.Timeline }, block.Source.Start, "$.eventBlocks." + block.Source.BlockId + ".start");
                ValidateAttackWaitConditions();
                ValidateBehindTargetBlocks();
            }

            private void ValidateBehindTargetBlocks()
            {
                foreach (var item in _events.Values)
                {
                    var rear = item.Source.Operation?.Movement?.BehindTarget;
                    if (rear == null) continue;
                    if (!_source.HasTimingVersion || !_eventBlocks.TryGetValue(item.Source.EventId, out string blockId))
                    { Error("BehindTargetContract", item.Path, "背后意图需要 v4 独立封装与显式执行时限。"); continue; }
                    var block = _blocks[blockId].Source;
                    var members = block.EventIds.Select(id => _events.TryGetValue(id, out var ev) ? ev.Source : null).ToArray();
                    if (block.EventIds.Count != 2 || block.Timing == null || block.Timing.MinimumFrames != 1
                        || block.Timing.DurationFrames <= 0 || block.Completion != (rear.Maintain ? "Timed" : "Arrived")
                        || (!rear.Maintain && block.CompletionEventId != item.Source.EventId)
                        || !members.Any(e => e?.Operation?.Kind == "TurnToTarget" && e.Operation.Target == "currentCombatTarget")
                        || members.Any(e => e == null || e.ConditionRoot != null || e.TimingKind != "Duration"
                            || e.Start?.Kind != "Absolute" || e.Start.Space != "Block" || e.Start.Frame != 0
                            || e.End?.Kind != "Anchor" || e.End.TargetKind != "Builtin" || e.End.SourceEventId != blockId
                            || e.End.Anchor != "SimulationEnd" || e.End.OffsetFrames != 0))
                        Error("BehindTargetContract", item.Path, "背后意图需要同段移动与面向当前目标；到达结束和保持到时限不得互换。");
                }
            }

            private void ValidateAttackWaitConditions()
            {
                if (ContainsAttackWaitFact(_source.ActivationConditionRoot)
                    || _source.Timelines.Exists(t => t != null && ContainsAttackWaitFact(t.EntryConditionRoot)))
                    Error("AttackWaitScope", "$.conditions", "当前攻击窗口结束条件只能在独立 Condition 等待封装中使用。");
                foreach (var item in _events.Values)
                {
                    bool wait = _eventBlocks.TryGetValue(item.Source.EventId, out string owner)
                        && _blocks[owner].Source.Completion == "Condition";
                    bool hasFact = ContainsAttackWaitFact(item.Source.ConditionRoot);
                    if (wait && !hasFact || hasFact && !wait)
                        Error("AttackWaitScope", item.Path, "当前攻击窗口结束条件需要所属的独立 Condition 等待封装。");
                }
            }

            private int ResolveFrame(CharacterTestTacticalFrameReference frame, string eventId)
            {
                if (frame.Kind == "Anchor") return checked(Position(frame.SourceEventId, frame.Anchor) + frame.OffsetFrames);
                int origin = frame.Space == "Block" ? Position(_eventBlocks[eventId], "Start") : 0;
                return checked(origin + frame.Frame);
            }
        }
    }
}
