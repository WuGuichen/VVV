using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Explicit source migration of the retired move/wait/action block; preserves action slots and existing end anchors.</summary>
    /// <remarks>LowFreqAlloc. Called by authoring templates and the migration transaction, never by execution.</remarks>
    internal static class CharacterTestTacticalApproachActionSplit
    {
        internal const string ApproachTitle = "常规移动面向目标接近";
        internal const string ActionTitle = "面向目标出招";
        internal const int DefaultApproachFrames = 30;

        internal static CharacterTestTacticalPublishedSource UpgradePublished(CharacterTestTacticalPublishedSource original, out int added)
        {
            added = 0;
            var json = original.ToJson();
            if (original.IsLegacy)
            {
                var source = original.CloneLegacy(); added = AddVisibleCompletionEvents(source);
                if (added > 0) json["legacy"] = Newtonsoft.Json.Linq.JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source));
            }
            else
            {
                var definitions = original.CloneDefinitions();
                foreach (var definition in definitions)
                {
                    int count = AddVisibleCompletionEvents(definition.Source);
                    if (count > 0) definition.Revision = checked(definition.Revision + 1);
                    added += count;
                }
                if (added > 0) json["definitions"] = new Newtonsoft.Json.Linq.JArray(definitions.Select(d =>
                    Newtonsoft.Json.Linq.JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(d))));
            }
            return added == 0 ? original : CharacterTestTacticalPublishedSource.Read(json);
        }

        /// <summary>Explicit authoring upgrade: the completion rule is a persisted, editable timeline event.</summary>
        internal static int AddVisibleCompletionEvents(CharacterTestTacticalAuthoringSource source)
        {
            if (!source.HasTimingVersion) return 0;
            int added = 0;
            foreach (var timeline in source.Timelines)
                foreach (var block in timeline.EventBlocks)
                {
                    if (block.Timing == null || block.Completion != "Timed" && !IsImplicitArrival(source, block)) continue;
                    var members = timeline.Events.Where(e => block.EventIds.Contains(e.EventId)).ToArray();
                    var existingCompletion = members.FirstOrDefault(e => e.Operation.Kind == "CompleteBlock");
                    if (members.Length != (existingCompletion == null ? 2 : 3)) continue;
                    var move = members.SingleOrDefault(e => e.Operation.Kind == "Movement"
                        && e.Operation.Movement?.Strategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                        && e.Operation.Movement.Target == CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget);
                    var turn = members.SingleOrDefault(e => e.Operation.Kind == "TurnToTarget" && e.Operation.Target == "currentCombatTarget");
                    if (move == null || turn == null) continue;
                    if (existingCompletion != null)
                    {
                        if (BindCompletionToTurn(source, timeline, move, turn, existingCompletion)) added++;
                        continue;
                    }
                    string id = block.BlockId + ".complete";
                    int suffix = 2;
                    while (timeline.Events.Any(e => e.EventId == id)) id = block.BlockId + ".complete." + suffix++;
                    var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".target",
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                        Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget };
                    var all = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".all",
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All };
                    all.Children.Add(new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".arrived",
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate });
                    all.Children.Add(new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".facing",
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                        Predicate = new CharacterTestRuntimeAiLabCardCondition { ConditionId = id + ".facing",
                            Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle,
                            Operator = CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual, Value = DefaultFacingTolerance() } });
                    scope.Children.Add(all);
                    source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding { NodeId = id + ".arrived",
                        Kind = "MovementArrived", SourceMovementEventId = move.EventId });
                    timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = id, Title = "到位且面向达标 → 结束接近段",
                        Start = CharacterTestTacticalBlockEdits.Local(0), End = new CharacterTestTacticalFrameReference {
                            Kind = "Anchor", TargetKind = "Builtin", SourceEventId = block.BlockId, Anchor = "SimulationEnd" },
                        Operation = new CharacterTestTacticalAuthoringOperation { Kind = "CompleteBlock" }, ConditionRoot = scope, Priority = 100 });
                    block.EventIds.Add(id);
                    BindCompletionToTurn(source, timeline, move, turn, timeline.Events.Last());
                    block.Completion = "Timed"; block.CompletionEventId = block.SuccessConditionEventId = string.Empty;
                    added++;
                }
            return added;
        }

        internal static float DefaultFacingTolerance()
        {
            CharacterTestRuntimeAiLabBehaviorConditionCatalog.TryGet(CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle, out var descriptor);
            return descriptor.DefaultValue;
        }

        private static bool BindCompletionToTurn(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalAuthoringTimeline timeline,
            CharacterTestTacticalAuthoringEvent move, CharacterTestTacticalAuthoringEvent turn, CharacterTestTacticalAuthoringEvent completion)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Find(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null) return null;
                if (node.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetFacingAngle
                    || source.PredicateBindings.Any(b => b.NodeId == node.NodeId && (b.Kind == "TurnAligned" || b.Kind == "TargetFacing"))) return node;
                foreach (var child in node.Children) { var found = Find(child); if (found != null) return found; }
                return null;
            }
            var facing = Find(completion.ConditionRoot);
            if (facing == null || source.PredicateBindings.Any(b => b.NodeId == facing.NodeId && b.Kind == "TurnAligned")) return false;
            if (turn.Operation.FacingSource == null)
            {
                var action = timeline.Events.FirstOrDefault(e => e.Operation.Kind == "CharacterAction"
                    && (string.IsNullOrEmpty(move.Operation.Movement.ActionSlotId) || e.Operation.SlotId == move.Operation.Movement.ActionSlotId));
                turn.Operation.FacingSource = action == null ? "Manual" : "ActionFacing";
                turn.Operation.FacingActionSlotId = action?.Operation.SlotId;
                turn.Operation.FacingToleranceDegrees = action == null ? facing.Predicate?.Value ?? DefaultFacingTolerance() : null;
            }
            facing.Predicate = null;
            source.PredicateBindings.RemoveAll(b => b.NodeId == facing.NodeId);
            source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding { NodeId = facing.NodeId, Kind = "TurnAligned", SourceTurnEventId = turn.EventId });
            return true;
        }

        internal static bool IsImplicitArrival(CharacterTestTacticalAuthoringSource source, CharacterTestTacticalEventBlock block)
        {
            if (block.Completion != "Arrived") return false;
            var ev = source.Timelines.SelectMany(t => t.Events).FirstOrDefault(e => e.EventId == block.CompletionEventId);
            return ev?.Operation?.Kind == "Movement" && ev.Operation.Movement?.BehindTarget == null
                && ev.Operation.Movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition;
        }

        internal static bool Apply(CharacterTestTacticalAuthoringSource source)
        {
            if (!source.HasTimingVersion) return false;
            bool changed = false;
            var identities = new HashSet<string>(source.Timelines.SelectMany(t => t.Events.Select(e => e.EventId)
                .Concat(t.EventBlocks.Select(b => b.BlockId))), StringComparer.Ordinal);
            void AddConditions(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            { if (node == null) return; identities.Add(node.NodeId); if (node.Children != null) foreach (var child in node.Children) AddConditions(child); }
            AddConditions(source.ActivationConditionRoot);
            foreach (var timeline in source.Timelines)
            { AddConditions(timeline.EntryConditionRoot); foreach (var ev in timeline.Events) AddConditions(ev.ConditionRoot); }
            string Unique(string prefix)
            {
                string result = prefix; int index = 2;
                while (!identities.Add(result)) result = prefix + "." + index++;
                return result;
            }
            foreach (var timeline in source.Timelines)
                foreach (var block in timeline.EventBlocks.ToArray())
                {
                    if (IsImplicitArrival(source, block))
                    {
                        block.Completion = "Timed";
                        block.CompletionEventId = block.SuccessConditionEventId = string.Empty;
                        changed = true;
                    }
                    if (block.Completion != "Timed") continue;
                    var events = timeline.Events.Where(e => block.EventIds.Contains(e.EventId)).ToArray();
                    if (events.Length == 2)
                    {
                        continue;
                    }
                    if (events.Length != 4) continue;
                    var move = events.SingleOrDefault(e => e.Operation.Kind == "Movement" || e.Operation.Kind == "ApproachOptimalRange");
                    var face = events.SingleOrDefault(e => e.Operation.Kind == "TurnToTarget");
                    var gate = events.SingleOrDefault(e => e.Operation.Kind == "WaitForCondition");
                    var action = events.SingleOrDefault(e => e.Operation.Kind == "CharacterAction");
                    if (move == null || face == null || gate == null || action == null || gate.ConditionRoot == null || block.Timing == null
                        || move.Operation.Kind == "Movement" && (move.Operation.Movement?.Strategy != CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget
                            || move.Operation.Movement.Target != CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget)
                        || face.Operation.Target != "currentCombatTarget" || move.ConditionRoot != null || face.ConditionRoot != null
                        || action.Start?.Kind != "Anchor" || action.Start.SourceEventId != gate.EventId || action.Start.Anchor != "End"
                        || action.Start.OffsetFrames != 0 || action.Start.TargetKind != "Event"
                        || move.End?.Kind != "Anchor" || move.End.SourceEventId != action.EventId || move.End.Anchor != "Start"
                        || move.End.OffsetFrames != 0 || move.End.TargetKind != "Event") continue;
                    if (timeline.Events.Except(events).Any(e => e.Start?.SourceEventId == gate.EventId || e.End?.SourceEventId == gate.EventId))
                        throw new InvalidOperationException("不能自动拆分：其它事件引用了等待事件 " + gate.EventId);

                    string approachId = Unique(block.BlockId + ".approach"), actionFaceId = Unique(face.EventId + ".action");
                    var approach = new CharacterTestTacticalEventBlock {
                        BlockId = approachId, Title = ApproachTitle, Start = block.Start,
                        Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = 1, DurationFrames = DefaultApproachFrames } };
                    approach.EventIds.AddRange(new[] { move.EventId, face.EventId });
                    move.Title = "接近移动";
                    if (move.Operation.Kind == "ApproachOptimalRange")
                        move.Operation = new CharacterTestTacticalAuthoringOperation { Kind = "Movement", Speed = move.Operation.Speed,
                            Movement = new CharacterTestTacticalMovement { Strategy = CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                                DistanceSource = "ActionOptimal", ActionSlotId = action.Operation.SlotId } };
                    move.Start = CharacterTestTacticalBlockEdits.Local(0); move.End = CharacterTestTacticalAuthoringTemplates.VisualEnd(approachId);
                    face.Start = CharacterTestTacticalBlockEdits.Local(0); face.End = CharacterTestTacticalAuthoringTemplates.VisualEnd(approachId);
                    block.Title = ActionTitle; block.Start = CharacterTestTacticalBlockEdits.BlockEnd(approachId);
                    block.Completion = "Action"; block.CompletionEventId = action.EventId;
                    block.EventIds.Clear(); block.EventIds.AddRange(new[] { action.EventId, actionFaceId });
                    action.Start = CharacterTestTacticalBlockEdits.Local(0);
                    action.ConditionRoot = null;
                    timeline.Events.Remove(gate);
                    var removedConditions = new HashSet<string>(StringComparer.Ordinal);
                    Collect(gate.ConditionRoot, removedConditions);
                    var retainedConditions = new HashSet<string>(StringComparer.Ordinal);
                    Collect(source.ActivationConditionRoot, retainedConditions);
                    foreach (var owner in source.Timelines)
                    {
                        Collect(owner.EntryConditionRoot, retainedConditions);
                        foreach (var ev in owner.Events) Collect(ev.ConditionRoot, retainedConditions);
                    }
                    source.PredicateBindings.RemoveAll(b => removedConditions.Contains(b.NodeId) && !retainedConditions.Contains(b.NodeId));
                    timeline.Events.Add(new CharacterTestTacticalAuthoringEvent { EventId = actionFaceId, Title = "面向目标",
                        Start = CharacterTestTacticalBlockEdits.Local(0), End = CharacterTestTacticalAuthoringTemplates.VisualEnd(block.BlockId),
                        Operation = new CharacterTestTacticalAuthoringOperation { Kind = "TurnToTarget", Target = face.Operation.Target, Speed = face.Operation.Speed } });
                    timeline.EventBlocks.Insert(timeline.EventBlocks.IndexOf(block), approach);
                    changed = true;
                }
            return changed;
        }

        private static void Collect(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, HashSet<string> ids)
        {
            if (node == null) return;
            ids.Add(node.NodeId);
            if (node.Children != null) foreach (var child in node.Children) Collect(child, ids);
        }
    }
}
