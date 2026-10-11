using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Display-only projection of existing composition controls and recognized intent segments.</summary>
    internal sealed class CharacterTestTacticalDescriptionFlow
    {
        public string Schema => "mx.tactical-card.description-flow.v1";
        public bool Complete { get; private set; }
        public CharacterTestTacticalDescriptionFlowNode Root { get; private set; }
        public CharacterTestTacticalDescriptionFlowRow[] Rows { get; private set; }
        public string Text { get; private set; }
        internal JObject ToJson() => JObject.FromObject(this, JsonSerializer.Create(new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), NullValueHandling = NullValueHandling.Ignore }));

        internal static string Term(string text) => (text ?? string.Empty).TrimEnd().TrimEnd('。', '.', ';', '；');
        internal static CharacterTestTacticalDescriptionFlow Build(CharacterTestTacticalDslNode root,
            CharacterTestTacticalDescriptionSegment[] segments, Func<CharacterTestRuntimeAiLabBehaviorRecipeConditionNode, string> condition,
            Func<CharacterTestTacticalDslNode, bool> alwaysEnds, IMxUiTextProvider provider, string fallback)
        {
            string L(string key, string value) => CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description." + key, value);
            bool complete = true;
            CharacterTestTacticalDescriptionFlowNode Project(CharacterTestTacticalDslNode node)
            {
                var value = new CharacterTestTacticalDescriptionFlowNode { Kind = node.Kind.ToLowerInvariant(), NodeId = node.NodeId };
                if (node.Kind == "Reference")
                {
                    var matched = segments.Select((segment, index) => (segment, index)).Where(p => p.segment.NodeId == node.NodeId).ToArray();
                    if (matched.Length == 0) { value.Kind = "opaque"; complete = false; }
                    else
                    {
                        value.Kind = "sequence";
                        value.Children = matched.Select(p => new CharacterTestTacticalDescriptionFlowNode { Kind = "intent", NodeId = node.NodeId,
                            BlockId = p.segment.BlockId, Text = p.segment.Text, SegmentIndex = p.index }).ToArray();
                    }
                }
                else if (node.Kind == "Sequence" || node.Kind == "If" || node.Kind == "Wait")
                {
                    var children = new List<CharacterTestTacticalDescriptionFlowNode>();
                    foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                    { children.Add(Project(child)); if (alwaysEnds(child)) break; }
                    value.Children = children.ToArray();
                    if (node.Kind != "Sequence") value.ConditionText = condition(node.Condition);
                    if (node.Kind == "If") value.OtherwiseText = L("flow_if_skip", "不成立：略过本段");
                    if (node.Kind == "Wait")
                    { value.MaximumWaitFrames = node.MaximumWaitFrames ?? 60; value.OtherwiseText = L("flow_wait_timeout", "超时：结束本卡"); }
                }
                else if (node.Kind == "Exit") value.Text = L("flow_exit", "结束本卡");
                else { value.Kind = "opaque"; complete = false; }
                if (node.EndCondition != null) value = new CharacterTestTacticalDescriptionFlowNode { Kind = "interrupt", NodeId = node.NodeId,
                    ConditionText = condition(node.EndCondition), Children = new[] { value } };
                return value;
            }
            var flow = new CharacterTestTacticalDescriptionFlow { Root = Project(root), Complete = complete };
            if (!complete) { flow.Text = fallback; flow.Rows = Array.Empty<CharacterTestTacticalDescriptionFlowRow>(); return flow; }
            var rows = new List<CharacterTestTacticalDescriptionFlowRow>();
            string arrow = " " + L("flow_arrow", "→") + " ";
            IEnumerable<CharacterTestTacticalDescriptionFlowNode> Flatten(CharacterTestTacticalDescriptionFlowNode node)
            {
                if (node.Kind == "sequence") foreach (var child in node.Children ?? Array.Empty<CharacterTestTacticalDescriptionFlowNode>())
                    foreach (var item in Flatten(child)) yield return item;
                else yield return node;
            }
            int group = 0;
            void Control(string text, int indent, string kind) => rows.Add(new CharacterTestTacticalDescriptionFlowRow {
                Kind = kind, Text = text, Indent = indent, BreakBefore = true, GroupId = ++group });
            void Sequence(IEnumerable<CharacterTestTacticalDescriptionFlowNode> nodes, int indent)
            {
                var values = nodes.SelectMany(Flatten).ToArray();
                bool previousIntent = false; int run = ++group;
                for (int index = 0; index < values.Length; index++)
                {
                    var value = values[index];
                    if (value.Kind == "intent" || value.Kind == "exit")
                    {
                        bool chain = previousIntent || index + 1 < values.Length && (values[index + 1].Kind == "intent" || values[index + 1].Kind == "exit");
                        rows.Add(new CharacterTestTacticalDescriptionFlowRow { Kind = value.Kind, Text = chain ? Term(value.Text) : value.Text,
                            Prefix = previousIntent ? arrow : string.Empty, Indent = indent, SegmentIndex = value.SegmentIndex,
                            GroupId = run, BreakBefore = !previousIntent, StripSentenceEnd = chain });
                        previousIntent = true; continue;
                    }
                    if (index > 0 && previousIntent) Control(L("flow_merge", "↓"), indent, "join");
                    if (value.Kind == "if")
                    {
                        Control(L("flow_if", "若{condition}：").Replace("{condition}", value.ConditionText), indent, "if");
                        Sequence(value.Children, indent + 1);
                        Control(value.OtherwiseText, indent, "otherwise");
                    }
                    else if (value.Kind == "wait")
                    {
                        Control(L("flow_wait", "等待 ≤ {frames} 帧，直到{condition}：").Replace("{frames}", value.MaximumWaitFrames.ToString())
                            .Replace("{condition}", value.ConditionText), indent, "wait");
                        Sequence(value.Children, indent + 1);
                        Control(value.OtherwiseText, indent, "timeout");
                    }
                    else if (value.Kind == "interrupt")
                    {
                        Control(L("flow_interrupt", "本段期间若{condition}则中止本段：").Replace("{condition}", value.ConditionText), indent, "interrupt");
                        Sequence(value.Children, indent + 1);
                    }
                    if (index + 1 < values.Length) Control(L("flow_merge", "↓"), indent, "join");
                    previousIntent = false; run = ++group;
                }
            }
            Sequence(new[] { flow.Root }, 0);
            flow.Rows = rows.ToArray();
            var lines = new List<string>();
            foreach (var row in rows)
            {
                string text = row.Prefix + row.Text;
                if (!row.BreakBefore && lines.Count > 0) lines[lines.Count - 1] += text;
                else lines.Add(new string(' ', row.Indent * 2) + text);
            }
            flow.Text = string.Join("\n", lines);
            return flow;
        }
    }
    internal sealed class CharacterTestTacticalDescriptionFlowNode
    {
        public string Kind { get; set; }
        public string NodeId { get; set; }
        public string BlockId { get; set; }
        public string Text { get; set; }
        public string ConditionText { get; set; }
        public string OtherwiseText { get; set; }
        public int? MaximumWaitFrames { get; set; }
        public int? SegmentIndex { get; set; }
        public CharacterTestTacticalDescriptionFlowNode[] Children { get; set; }
    }
    internal sealed class CharacterTestTacticalDescriptionFlowRow
    {
        public string Kind { get; set; }
        public string Text { get; set; }
        public string Prefix { get; set; } = string.Empty;
        public int Indent { get; set; }
        public int? SegmentIndex { get; set; }
        public int GroupId { get; set; }
        public bool BreakBefore { get; set; }
        public bool StripSentenceEnd { get; set; }
    }
}
