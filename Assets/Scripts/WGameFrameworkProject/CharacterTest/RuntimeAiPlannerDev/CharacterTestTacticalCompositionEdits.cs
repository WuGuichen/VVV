using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Low frequency mutations of the card-owned composition, used inside workspace transactions.</summary>
    internal static class CharacterTestTacticalCompositionEdits
    {
        internal static IEnumerable<CharacterTestTacticalDslNode> Nodes(CharacterTestTacticalDslNode root)
        {
            if (root == null) yield break;
            yield return root;
            foreach (var child in root.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var node in Nodes(child)) yield return node;
        }

        internal static CharacterTestTacticalDslNode Find(CharacterTestTacticalCompositionSource source, string id)
            => Nodes(source.Root).SingleOrDefault(n => n.NodeId == id) ?? throw new ArgumentException("编排项已不存在。");
        internal static CharacterTestTacticalDslNode Parent(CharacterTestTacticalCompositionSource source, string id)
            => Nodes(source.Root).SingleOrDefault(n => n.Children?.Any(c => c.NodeId == id) == true)
                ?? throw new ArgumentException("根编排不能移动、删除或解组。");

        internal static CharacterTestTacticalDslNode NewNode(string kind, string definitionId = null)
        {
            if (kind != "Reference" && kind != "Sequence" && kind != "If" && kind != "Wait" && kind != "Exit")
                throw new ArgumentException("不支持的编排类型。");
            return new CharacterTestTacticalDslNode { NodeId = Guid.NewGuid().ToString("N"), Kind = kind,
                DefinitionId = kind == "Reference" ? definitionId : null,
                Children = kind == "Sequence" || kind == "If" || kind == "Wait" ? new List<CharacterTestTacticalDslNode>() : null };
        }

        internal static void Insert(CharacterTestTacticalCompositionSource source, string parentId, int index, CharacterTestTacticalDslNode node)
        {
            var parent = Find(source, parentId);
            if (parent.Kind != "Sequence" && parent.Kind != "If" && parent.Kind != "Wait") throw new ArgumentException("只能向顺序组、条件组或等待组插入内容。");
            if (parent.Children == null) parent.Children = new List<CharacterTestTacticalDslNode>();
            if (index < 0 || index > parent.Children.Count) throw new ArgumentException("插入位置已变化。");
            if (Nodes(source.Root).Any(n => Nodes(node).Any(added => added.NodeId == n.NodeId)))
                throw new ArgumentException("编排项身份重复。");
            parent.Children.Insert(index, node);
        }

        internal static void Delete(CharacterTestTacticalCompositionSource source, string id)
        { var parent = Parent(source, id); parent.Children.RemoveAll(n => n.NodeId == id); }

        internal static void Move(CharacterTestTacticalCompositionSource source, string id, int delta)
        {
            var parent = Parent(source, id); int index = parent.Children.FindIndex(n => n.NodeId == id);
            int next = checked(index + delta);
            if (next < 0 || next >= parent.Children.Count) throw new ArgumentException("已到当前组的边界。");
            var node = parent.Children[index]; parent.Children.RemoveAt(index); parent.Children.Insert(next, node);
        }

        internal static void Group(CharacterTestTacticalCompositionSource source, string[] ids, string kind)
        {
            if (ids == null || ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("请选择同一组内连续的编排项。");
            if (kind != "Sequence" && kind != "If") throw new ArgumentException("只能创建顺序组或条件组。");
            var parent = Parent(source, ids[0]); var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            var indices = parent.Children.Select((n, i) => new { n, i }).Where(x => selected.Contains(x.n.NodeId)).Select(x => x.i).ToArray();
            if (indices.Length != ids.Length || indices[indices.Length - 1] - indices[0] + 1 != ids.Length)
                throw new ArgumentException("成组只能包含同一组内连续项，不能吸入未选中内容。");
            var group = NewNode(kind); group.Children.AddRange(parent.Children.GetRange(indices[0], ids.Length));
            parent.Children.RemoveRange(indices[0], ids.Length); parent.Children.Insert(indices[0], group);
        }

        internal static void Ungroup(CharacterTestTacticalCompositionSource source, string id)
        {
            var parent = Parent(source, id); var node = Find(source, id);
            if (node.Kind != "Sequence" && node.Kind != "If") throw new ArgumentException("所选项不是内容组。");
            if (node.Condition != null || node.EndCondition != null)
                throw new ArgumentException("请先明确移除组入口及局部结束条件，再解组，避免隐式丢失条件。");
            int index = parent.Children.IndexOf(node); parent.Children.RemoveAt(index);
            if (node.Children != null) parent.Children.InsertRange(index, node.Children);
        }

        internal static CharacterTestRuntimeAiLabBehaviorRecipeConditionNode GetCondition(CharacterTestTacticalCompositionSource source,
            CharacterTestTacticalEditTarget target, string id)
            => target == CharacterTestTacticalEditTarget.CardActivation ? source.ActivationConditionRoot
                : target == CharacterTestTacticalEditTarget.NodeEnd ? Find(source, id).EndCondition : Find(source, id).Condition;

        internal static void SetCondition(CharacterTestTacticalCompositionSource source, CharacterTestTacticalEditTarget target,
            string id, CharacterTestRuntimeAiLabBehaviorRecipeConditionNode condition)
        {
            if (target == CharacterTestTacticalEditTarget.CardActivation) { source.ActivationConditionRoot = condition; return; }
            var node = Find(source, id);
            if (target == CharacterTestTacticalEditTarget.NodeEnd)
            {
                if (node.Kind != "Reference" && node.Kind != "Sequence") throw new ArgumentException("局部结束只能属于封装引用或顺序组。");
                node.EndCondition = condition; return;
            }
            if (target != CharacterTestTacticalEditTarget.NodeCondition || (node.Kind != "If" && node.Kind != "Wait"))
                throw new ArgumentException("请为这次使用创建入口条件组，或选择显式等待。");
            node.Condition = condition;
        }
    }
}
