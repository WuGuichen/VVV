using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestRuntimeAiPlannerFlowPreview
    {
        private void AddGlobalNode(VisualElement canvas, Rect rect, FlowMetrics metrics)
        {
            var node = new VisualElement { name = GlobalNodeName };
            ApplyLegendRect(node, rect, metrics);
            node.tooltip = "SourceStateId 为空的切换表示任何状态满足条件后可进入目标状态。";
            node.style.backgroundColor = new Color(0.18f, 0.18f, 0.20f, 0.84f);
            node.style.borderLeftWidth = 1;
            node.style.borderRightWidth = 1;
            node.style.borderTopWidth = 1;
            node.style.borderBottomWidth = 1;
            node.style.borderLeftColor = new Color(0.34f, 0.34f, 0.40f);
            node.style.borderRightColor = new Color(0.34f, 0.34f, 0.40f);
            node.style.borderTopColor = new Color(0.34f, 0.34f, 0.40f);
            node.style.borderBottomColor = new Color(0.34f, 0.34f, 0.40f);
            node.Add(CreateNodeLabel("全局规则", metrics.SmallFontSize, FontStyle.Bold));
            node.Add(CreateNodeLabel("空 SourceStateId", metrics.SmallFontSize, FontStyle.Normal));
            canvas.Add(node);
        }

        private static void AddMissingTargetNode(VisualElement canvas, Rect rect, FlowMetrics metrics)
        {
            var node = CreateStaticNode(
                MissingTargetNodeName,
                "缺失目标",
                "请修复切换",
                rect,
                new Color(0.34f, 0.18f, 0.18f),
                new Color(0.78f, 0.22f, 0.22f),
                metrics);
            canvas.Add(node);
        }

        private static VisualElement CreateStaticNode(
            string nodeName,
            string title,
            string subtitle,
            Rect rect,
            Color background,
            Color border,
            FlowMetrics metrics)
        {
            var node = new VisualElement { name = nodeName };
            ApplyNodeRect(node, rect, metrics);
            ApplyNodeTone(node, background, border);
            node.Add(CreateNodeLabel(title, metrics.TitleFontSize, FontStyle.Bold));
            node.Add(CreateNodeLabel(subtitle, metrics.SmallFontSize, FontStyle.Normal));
            return node;
        }

        private Button CreateStateNode(
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state,
            Rect rect,
            FlowMetrics metrics)
        {
            int actionCount = CountActions(state.StateId);
            int depth = _model.GetStateDepth(state.StateId);
            bool selected = string.Equals(state.StateId, _selectedStateId, StringComparison.Ordinal)
                && _selectedTransitionIndex < 0;
            bool initial = string.Equals(_model.InitialStateId, state.StateId, StringComparison.Ordinal);
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity = GetStateSeverity(state);
            Color background = selected
                ? new Color(0.20f, 0.34f, 0.52f)
                : initial
                    ? new Color(0.22f, 0.30f, 0.22f)
                    : new Color(0.22f, 0.22f, 0.24f);
            Color border = selected
                ? new Color(0.36f, 0.58f, 0.90f)
                : ResolveNodeBorder(severity, initial);
            string displayName = string.IsNullOrWhiteSpace(state.DisplayName)
                ? state.StateId
                : state.DisplayName;

            string stateId = state.StateId;
            var node = new Button(() => _selectState?.Invoke(stateId))
            {
                name = StateNodeName(stateId),
                tooltip = "state=" + stateId
            };
            node.AddToClassList(StateNodeClassName);
            ApplyNodeRect(node, rect, metrics);
            ApplyNodeTone(node, background, border);
            node.style.unityTextAlign = TextAnchor.UpperLeft;
            node.style.whiteSpace = WhiteSpace.Normal;
            node.style.fontSize = metrics.TitleFontSize;
            node.text = CreateStateNodeText(displayName, state.StateId, depth, actionCount, initial);
            return node;
        }

        private string CreateStateNodeText(
            string displayName,
            string stateId,
            int depth,
            int actionCount,
            bool initial)
        {
            if (_mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview)
            {
                return displayName
                    + "\n(" + stateId + ")"
                    + "\nL" + depth
                    + " · A" + actionCount
                    + (initial ? " · 初始" : string.Empty);
            }

            return displayName
                + "\n(" + stateId + ")"
                + "\n层级 " + depth
                + " · 动作 " + actionCount
                + (initial ? " · 初始" : string.Empty);
        }

        private void AddTransitionRuleNodes(
            VisualElement canvas,
            IReadOnlyList<RuleDrawData> rules,
            FlowMetrics metrics)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                RuleDrawData rule = rules[i];
                if (rule.TransitionIndex < 0)
                    continue;

                int transitionIndex = rule.TransitionIndex;
                var ruleNode = new Button(() => _selectTransition?.Invoke(transitionIndex))
                {
                    name = TransitionEdgeName(transitionIndex),
                    text = rule.Label,
                    tooltip = rule.Tooltip
                };
                ruleNode.AddToClassList(TransitionEdgeClassName);
                ruleNode.style.position = Position.Absolute;
                ruleNode.style.left = rule.Rect.x;
                ruleNode.style.top = rule.Rect.y;
                ruleNode.style.width = rule.Rect.width;
                ruleNode.style.height = rule.Rect.height;
                ruleNode.style.minWidth = rule.Rect.width;
                ruleNode.style.minHeight = rule.Rect.height;
                ruleNode.style.paddingLeft = metrics.NodePaddingX;
                ruleNode.style.paddingRight = metrics.NodePaddingX;
                ruleNode.style.paddingTop = 2;
                ruleNode.style.paddingBottom = 2;
                ruleNode.style.fontSize = metrics.SmallFontSize;
                ruleNode.style.unityTextAlign = TextAnchor.MiddleLeft;
                ruleNode.style.whiteSpace = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail
                    ? WhiteSpace.Normal
                    : WhiteSpace.NoWrap;
                ruleNode.style.textOverflow = _mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Detail
                    ? TextOverflow.Clip
                    : TextOverflow.Ellipsis;
                ruleNode.style.overflow = Overflow.Hidden;
                ruleNode.style.borderLeftWidth = 1;
                ruleNode.style.borderRightWidth = 1;
                ruleNode.style.borderTopWidth = 1;
                ruleNode.style.borderBottomWidth = 1;
                ruleNode.style.borderLeftColor = rule.Color;
                ruleNode.style.borderRightColor = rule.Color;
                ruleNode.style.borderTopColor = rule.Color;
                ruleNode.style.borderBottomColor = rule.Color;
                ruleNode.style.backgroundColor = rule.Selected
                    ? new Color(0.20f, 0.34f, 0.52f)
                    : new Color(0.20f, 0.20f, 0.22f);
                canvas.Add(ruleNode);
            }
        }

        private int CountActions(string stateId)
        {
            int count = 0;
            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (string.Equals(_model.Actions[i].StateId, stateId, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private CharacterTestRuntimeAiPlannerDiagnosticSeverity GetStateSeverity(
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state)
        {
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity =
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Info;
            if (_report == null || state == null)
                return severity;

            for (int i = 0; i < _report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = _report.Diagnostics[i];
                if (!diagnostic.Code.StartsWith("state.", StringComparison.Ordinal))
                    continue;

                if (ContainsOrdinal(diagnostic.Message, state.StateId)
                    || (!string.IsNullOrWhiteSpace(state.ParentStateId)
                        && ContainsOrdinal(diagnostic.Message, state.ParentStateId))
                    || (diagnostic.Code == "state.id.empty"
                        && string.IsNullOrWhiteSpace(state.StateId)))
                {
                    severity = MaxSeverity(severity, diagnostic.Severity);
                }
            }

            return severity;
        }

        private CharacterTestRuntimeAiPlannerDiagnosticSeverity GetTransitionSeverity(
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition)
        {
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity =
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Info;
            if (_report == null || transition == null)
                return severity;

            for (int i = 0; i < _report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = _report.Diagnostics[i];
                if (!diagnostic.Code.StartsWith("transition.", StringComparison.Ordinal))
                    continue;

                if (diagnostic.Code == "transition.source.missing"
                    && ContainsOrdinal(diagnostic.Message, transition.SourceStateId))
                {
                    severity = MaxSeverity(severity, diagnostic.Severity);
                }
                else if (diagnostic.Code == "transition.target.missing"
                    && (string.IsNullOrWhiteSpace(transition.TargetStateId)
                        || ContainsOrdinal(diagnostic.Message, transition.TargetStateId)))
                {
                    severity = MaxSeverity(severity, diagnostic.Severity);
                }
                else if (diagnostic.Code.StartsWith("transition.condition.", StringComparison.Ordinal)
                    && IsConditionInvalid(transition.Condition))
                {
                    severity = MaxSeverity(severity, diagnostic.Severity);
                }
            }

            return severity;
        }

        private static bool IsConditionInvalid(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            if (condition == null)
                return true;

            if (!CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGet(condition.Fact, out _))
                return true;

            if ((condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual
                    || condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual)
                && (float.IsNaN(condition.FloatValue) || float.IsInfinity(condition.FloatValue)))
            {
                return true;
            }

            return false;
        }

        private static CharacterTestRuntimeAiPlannerDiagnosticSeverity MaxSeverity(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity left,
            CharacterTestRuntimeAiPlannerDiagnosticSeverity right)
        {
            return (int)right > (int)left ? right : left;
        }

        private static Color ResolveNodeBorder(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity,
            bool initial)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error:
                    return new Color(0.88f, 0.24f, 0.24f);
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning:
                    return new Color(0.85f, 0.63f, 0.22f);
                default:
                    return initial
                        ? new Color(0.45f, 0.70f, 0.38f)
                        : new Color(0.38f, 0.38f, 0.42f);
            }
        }

        private static Color ResolveLineColor(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity,
            bool selected)
        {
            if (selected)
                return new Color(0.46f, 0.65f, 0.95f);

            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error:
                    return new Color(0.90f, 0.30f, 0.30f);
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning:
                    return new Color(0.86f, 0.64f, 0.22f);
                default:
                    return new Color(0.45f, 0.52f, 0.64f);
            }
        }

        private static void ApplyNodeRect(VisualElement node, Rect rect, FlowMetrics metrics)
        {
            node.style.position = Position.Absolute;
            node.style.left = rect.x;
            node.style.top = rect.y;
            node.style.width = rect.width;
            node.style.height = rect.height;
            node.style.minWidth = rect.width;
            node.style.minHeight = rect.height;
            node.style.paddingLeft = metrics.NodePaddingX;
            node.style.paddingRight = metrics.NodePaddingX;
            node.style.paddingTop = metrics.NodePaddingY;
            node.style.paddingBottom = metrics.NodePaddingY;
        }

        private static void ApplyLegendRect(VisualElement node, Rect rect, FlowMetrics metrics)
        {
            node.style.position = Position.Absolute;
            node.style.left = rect.x;
            node.style.top = rect.y;
            node.style.width = rect.width;
            node.style.height = rect.height;
            node.style.minWidth = rect.width;
            node.style.minHeight = rect.height;
            node.style.paddingLeft = metrics.NodePaddingX;
            node.style.paddingRight = metrics.NodePaddingX;
            node.style.paddingTop = 3;
            node.style.paddingBottom = 3;
        }

        private static void ApplyNodeTone(VisualElement node, Color background, Color border)
        {
            node.style.backgroundColor = background;
            node.style.borderLeftWidth = 2;
            node.style.borderRightWidth = 2;
            node.style.borderTopWidth = 2;
            node.style.borderBottomWidth = 2;
            node.style.borderLeftColor = border;
            node.style.borderRightColor = border;
            node.style.borderTopColor = border;
            node.style.borderBottomColor = border;
        }

        private static Label CreateNodeLabel(
            string text,
            float fontSize,
            FontStyle fontStyle)
        {
            var label = new Label(text ?? string.Empty);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.overflow = Overflow.Hidden;
            return label;
        }

        private string FormatTransitionLabel(
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition)
        {
            if (transition == null)
                return "transition";

            string displayName = string.IsNullOrWhiteSpace(transition.DisplayName)
                ? "切换"
                : transition.DisplayName;
            bool global = string.IsNullOrWhiteSpace(transition.SourceStateId);
            if (global)
            {
                string target = string.IsNullOrWhiteSpace(transition.TargetStateId)
                    ? "?"
                    : transition.TargetStateId;
                if (_mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview)
                    return "全局 p=" + transition.Priority + " -> " + target;

                return "全局 p=" + transition.Priority + " " + displayName
                    + " -> " + target
                    + "\n" + CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatCondition(transition.Condition);
            }

            if (_mode == CharacterTestRuntimeAiPlannerFlowPreviewMode.Overview)
                return "p=" + transition.Priority + " " + displayName;

            return "p=" + transition.Priority + " " + displayName
                + "  " + CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatCondition(transition.Condition);
        }

        private static string CreateTransitionTooltip(
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition)
        {
            if (transition == null)
                return string.Empty;

            string source = string.IsNullOrWhiteSpace(transition.SourceStateId)
                ? "全局"
                : transition.SourceStateId;
            return source + " -> " + transition.TargetStateId
                + "\npriority=" + transition.Priority
                + "\n" + CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatCondition(transition.Condition);
        }

    }
}
