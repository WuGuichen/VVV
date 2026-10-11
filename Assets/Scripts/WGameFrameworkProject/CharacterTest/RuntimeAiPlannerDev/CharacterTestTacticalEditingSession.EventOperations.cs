using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalEditingSession
    {
        internal bool CopyEvents(CharacterTestTacticalEditToken token, string timeline, string[] ids)
        {
            return Commit(token, "复制底层事件", source => {
                var copies = CharacterTestTacticalAuthoringEdits.Timeline(source.Clone(), timeline).Events.Where(e => ids.Contains(e.EventId)).ToArray();
                var map = copies.ToDictionary(e => e.EventId, e => "event-" + Guid.NewGuid().ToString("N"));
                var nodeMap = new Dictionary<string, string>();
                void Reference(CharacterTestTacticalFrameReference reference)
                { if (reference?.TargetKind == "Event" && map.TryGetValue(reference.SourceEventId ?? "", out string next)) reference.SourceEventId = next; }
                void Condition(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node)
                {
                    if (node == null) return;
                    string next = "condition-" + Guid.NewGuid().ToString("N"); nodeMap[node.NodeId] = next; node.NodeId = next;
                    if (node.Predicate != null) node.Predicate.ConditionId = next;
                    if (map.TryGetValue(node.SourceEventId ?? "", out string target)) node.SourceEventId = target;
                    foreach (var child in node.Children) Condition(child);
                }
                var owner = CharacterTestTacticalAuthoringEdits.Timeline(source, timeline);
                foreach (var ev in copies)
                {
                    string old = ev.EventId; ev.EventId = map[old]; ev.Title += " 副本";
                    Reference(ev.Start); Reference(ev.End); Condition(ev.ConditionRoot);
                    if (map.TryGetValue(ev.Operation.SourceActionEventId ?? "", out string action)) ev.Operation.SourceActionEventId = action;
                    owner.Events.Add(ev); CharacterTestTacticalBlockEdits.Owner(owner, old)?.EventIds.Add(ev.EventId);
                }
                foreach (var binding in source.PredicateBindings.ToArray())
                    if (nodeMap.TryGetValue(binding.NodeId, out string node)) source.PredicateBindings.Add(new CharacterTestTacticalPredicateBinding {
                        NodeId = node, Kind = binding.Kind, SourceActionEventId = map.TryGetValue(binding.SourceActionEventId, out string action) ? action : binding.SourceActionEventId });
            });
        }
    }
}
