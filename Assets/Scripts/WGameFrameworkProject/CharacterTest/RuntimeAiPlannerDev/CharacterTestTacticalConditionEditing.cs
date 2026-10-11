using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalConditionEditing
    {
        internal static void AddBinding(CharacterTestTacticalAuthoringSource source, string timeline, string eventId,
            string actionEvent, string kind, string parentNodeId)
        {
            if (!new[] { "RootDistanceRange", "TargetFacing", "MovementArrived", "TurnAligned" }.Contains(kind)) throw new ArgumentException("不支持此动作数据条件；动作机会已停用，请使用明确的距离或朝向条件。");
            if (CharacterTestTacticalAuthoringEdits.Event(source, timeline, actionEvent).Operation.Kind != (kind == "MovementArrived" ? "Movement" : kind == "TurnAligned" ? "TurnToTarget" : "CharacterAction"))
                throw new ArgumentException("请选择当前时间轴中的动作事件。");
            string id = "bound-" + Guid.NewGuid().ToString("N");
            var leaf = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id, Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate };
            var scope = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".target", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.SubjectScope,
                Subject = CharacterTestRuntimeAiLabBehaviorRecipeConditionSubjectKind.CurrentCombatTarget };
            scope.Children.Add(leaf);
            var root = eventId == "$activation" ? source.ActivationConditionRoot : eventId == "$timeline-entry"
                ? CharacterTestTacticalAuthoringEdits.Timeline(source, timeline).EntryConditionRoot
                : CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).ConditionRoot;
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode Insert(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null) return scope;
                if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All || node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any)
                { node.Children.Add(scope); return node; }
                var all = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = id + ".all", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.All };
                all.Children.Add(node); all.Children.Add(scope); return all;
            }
            bool inserted = false;
            void Find(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
            {
                if (node == null || inserted) return;
                for (int i = 0; i < node.Children.Count; i++)
                    if (node.Children[i].NodeId == parentNodeId) { node.Children[i] = Insert(node.Children[i]); inserted = true; return; }
                    else Find(node.Children[i]);
            }
            if (root == null || string.IsNullOrEmpty(parentNodeId) || root.NodeId == parentNodeId) root = Insert(root);
            else { Find(root); if (!inserted) throw new ArgumentException("条件组已不存在。"); }
            if (eventId == "$activation") source.ActivationConditionRoot = root;
            else if (eventId == "$timeline-entry") CharacterTestTacticalAuthoringEdits.Timeline(source, timeline).EntryConditionRoot = root;
            else CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).ConditionRoot = root;
            source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding { NodeId = id, Kind = kind,
                SourceActionEventId = kind == "MovementArrived" || kind == "TurnAligned" ? string.Empty : actionEvent,
                SourceMovementEventId = kind == "MovementArrived" ? actionEvent : null,
                SourceTurnEventId = kind == "TurnAligned" ? actionEvent : null });
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardLogicPayload Projection(CharacterTestTacticalAuthoringSource source)
        {
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload {
                BehaviorRecipeVersion = 11, EntryTimelineId = source.EntryTimelineId,
                ActivationConditionRoot = source.ActivationConditionRoot?.Clone() };
            foreach (var timeline in source.Timelines)
            {
                var copy = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = timeline.TimelineId,
                    Title = timeline.Title, EntryConditionRoot = timeline.EntryConditionRoot?.Clone() };
                foreach (var ev in timeline.Events)
                    copy.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = ev.EventId,
                        TimelineId = timeline.TimelineId, Title = ev.Title, ConditionRoot = ev.ConditionRoot?.Clone(),
                        TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                        Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation {
                            Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition } });
                payload.Timelines.Add(copy);
            }
            return payload;
        }

        internal static void Apply(CharacterTestTacticalAuthoringSource source, string timeline, string eventId,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField field, int index,
            CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventFieldValue value,
            IEnumerable<string> executionEventSources = null)
        {
            if (!(field.ToString().Contains("Condition") || field.ToString().StartsWith("Add")
                || field == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventField.PredicateGroupMode))
                throw new ArgumentException("该命令不是条件编辑命令。");
            var proxy = Projection(source);
            bool executionCondition = executionEventSources != null && eventId == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId;
            if (executionCondition)
            {
                var owner = proxy.Timelines.Single(t => t.TimelineId == timeline);
                foreach (string reference in executionEventSources.Distinct(StringComparer.Ordinal))
                    owner.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = reference, TimelineId = timeline,
                        Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition } });
                eventId = "$tactical-condition-owner";
                owner.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = eventId, TimelineId = timeline,
                    ConditionRoot = proxy.ActivationConditionRoot, Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation {
                        Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition } });
                proxy.ActivationConditionRoot = null;
            }
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode ReadRoot()
                => eventId == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId
                    ? proxy.ActivationConditionRoot
                    : eventId == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId
                        ? proxy.Timelines.Single(t => t.TimelineId == timeline).EntryConditionRoot
                        : proxy.Timelines.Single(t => t.TimelineId == timeline).Events.Single(e => e.EventId == eventId).ConditionRoot;
            Dictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode> Nodes(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode root)
            {
                var nodes = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode>(StringComparer.Ordinal);
                void Visit(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node) { if (node == null) return; nodes.Add(node.NodeId, node); foreach (var child in node.Children) Visit(child); }
                Visit(root); return nodes;
            }
            var before = Nodes(ReadRoot());
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TryApply(proxy, timeline, eventId,
                field, index, value, out _, out string issue)) throw new ArgumentException(issue);
            var root = ReadRoot();
            var after = Nodes(root);
            foreach (var node in after.Values)
                if (node.MovementQuery != null && node.Predicate != null)
                { node.MovementQuery = null; node.SourceEventId = string.Empty; }
            source.PredicateBindings.RemoveAll(b => before.ContainsKey(b.NodeId)
                && (!after.TryGetValue(b.NodeId, out var node) || node.Predicate != null));
            if (executionCondition || eventId == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.ActivationConditionOwnerId)
                source.ActivationConditionRoot = root;
            else if (eventId == CharacterTestRuntimeAiLabBehaviorRecipeTriggerEventMutation.TimelineEntryConditionOwnerId)
                CharacterTestTacticalAuthoringEdits.Timeline(source, timeline).EntryConditionRoot = root;
            else CharacterTestTacticalAuthoringEdits.Event(source, timeline, eventId).ConditionRoot = root;
        }
    }
}
