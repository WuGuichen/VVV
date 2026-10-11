using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalCompositionBinder
    {
        private void ShowMovementEnd(CharacterTestTacticalEditToken token, bool missing)
        {
            var scope = _session.SelectedNode;
            if (scope == null || scope.Kind != "Reference" && scope.Kind != "Sequence")
            { _session.Feedback("请选择要结束的封装或顺序组。"); return; }
            var choices = new List<(string, Action)>();
            foreach (var use in CharacterTestTacticalCompositionEdits.Nodes(scope).Where(n => n.Kind == "Reference"))
            {
                var definition = _session.Document.Definitions.FirstOrDefault(d => d.DefinitionId == use.DefinitionId);
                if (definition == null) continue;
                foreach (var evt in definition.Source.Timelines.SelectMany(t => t.Events).Where(e => e.Operation.Kind == "Movement"
                    && e.Operation.Movement != null && (missing || e.Operation.Movement.UsesDistance)))
                {
                    string reference = use.NodeId + "/" + evt.EventId;
                    choices.Add((definition.Source.Name + " / " + evt.Title + (missing ? " · 目标失效" : " · 达到所配距离"), () =>
                        _session.Edit(token, "添加移动局部结束条件", source => {
                            var node = CharacterTestTacticalCompositionEdits.Find(source, token.NodeId);
                            var condition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {
                                NodeId = "movement-" + Guid.NewGuid().ToString("N"), Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate,
                                SourceEventId = reference, MovementQuery = missing ? "TargetMissing" : "Arrived" };
                            // Keep existing user conditions; either reason can end the selected range.
                            if (node.EndCondition == null) node.EndCondition = condition;
                            else
                            {
                                var any = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode { NodeId = "end-any-" + Guid.NewGuid().ToString("N"),
                                    Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.Any };
                                any.Children.Add(node.EndCondition); any.Children.Add(condition); node.EndCondition = any;
                            }
                        })));
                }
            }
            if (choices.Count == 0) { _session.Feedback(missing ? "所选范围中没有统一移动事件。" : "所选范围中没有配置到位距离的移动事件；横移和持续远离可按持续时间或其它条件结束。"); return; }
            ShowChoices(choices);
        }
    }
}
