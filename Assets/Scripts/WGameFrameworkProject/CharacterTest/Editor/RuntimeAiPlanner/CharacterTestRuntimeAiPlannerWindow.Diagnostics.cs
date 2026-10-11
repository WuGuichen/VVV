using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private bool PassesActionFilter(CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action)
        {
            if (string.IsNullOrWhiteSpace(_actionFilter))
                return true;

            string filter = _actionFilter.Trim();
            return ContainsOrdinal(action.StateId, filter)
                || ContainsOrdinal(action.DisplayName, filter)
                || ContainsOrdinal(action.Kind.ToString(), filter)
                || action.ActionId.ToString().Contains(filter);
        }

        private static bool ContainsOrdinal(string value, string filter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool UsesFloatValue(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            return condition.Fact == CharacterTestRuntimeAiPlannerFactKind.TargetDistance
                || condition.Fact == CharacterTestRuntimeAiPlannerFactKind.TargetLastSeenAgeFrames
                || condition.Fact == CharacterTestRuntimeAiPlannerFactKind.TargetThreatScore
                || condition.Fact == CharacterTestRuntimeAiPlannerFactKind.SelfSurfaceKind
                || condition.Fact == CharacterTestRuntimeAiPlannerFactKind.MoveCandidateIndex
                || condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual
                || condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual;
        }

        private static string FormatCondition(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition)
        {
            if (condition == null)
                return "condition=none";

            string value = UsesFloatValue(condition)
                ? condition.FloatValue.ToString("0.###")
                : condition.BoolValue.ToString();
            return condition.Fact + " " + condition.Operator + " " + value;
        }

        private static bool HasDiagnosticForText(
            CharacterTestRuntimeAiPlannerValidationReport report,
            string text)
        {
            if (report == null || string.IsNullOrWhiteSpace(text))
                return false;

            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                if (ContainsOrdinal(report.Diagnostics[i].Message, text))
                    return true;
            }

            return false;
        }

        private static List<CharacterTestRuntimeAiPlannerDiagnostic> FindStateDiagnostics(
            CharacterTestRuntimeAiPlannerValidationReport report,
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            if (report == null || state == null)
                return diagnostics;

            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = report.Diagnostics[i];
                if (!diagnostic.Code.StartsWith("state.", StringComparison.Ordinal))
                    continue;

                if (diagnostic.Code == "state.id.empty" && string.IsNullOrWhiteSpace(state.StateId))
                    diagnostics.Add(diagnostic);
                else if (ContainsOrdinal(diagnostic.Message, state.StateId))
                    diagnostics.Add(diagnostic);
                else if (!string.IsNullOrWhiteSpace(state.ParentStateId)
                    && ContainsOrdinal(diagnostic.Message, state.ParentStateId))
                    diagnostics.Add(diagnostic);
            }

            return diagnostics;
        }

        private static List<CharacterTestRuntimeAiPlannerDiagnostic> FindTransitionDiagnostics(
            CharacterTestRuntimeAiPlannerValidationReport report,
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft> transitions,
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            if (report == null || transition == null)
                return diagnostics;

            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = report.Diagnostics[i];
                if (!diagnostic.Code.StartsWith("transition.", StringComparison.Ordinal))
                    continue;

                if (diagnostic.Code == "transition.source.missing"
                    && ContainsOrdinal(diagnostic.Message, transition.SourceStateId))
                {
                    diagnostics.Add(diagnostic);
                }
                else if (diagnostic.Code == "transition.target.missing"
                    && (string.IsNullOrWhiteSpace(transition.TargetStateId)
                        || ContainsOrdinal(diagnostic.Message, transition.TargetStateId)))
                {
                    diagnostics.Add(diagnostic);
                }
                else if (diagnostic.Code.StartsWith("transition.condition.", StringComparison.Ordinal)
                    && IsConditionInvalid(transition.Condition))
                {
                    diagnostics.Add(diagnostic);
                }
            }

            CharacterTestRuntimeAiPlannerEditorRuleDiagnostics.AddTransitionDiagnostics(
                transitions,
                transition,
                diagnostics);
            return diagnostics;
        }

        private static List<CharacterTestRuntimeAiPlannerDiagnostic> FindActionDiagnostics(
            CharacterTestRuntimeAiPlannerValidationReport report,
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action)
        {
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            if (report == null || action == null)
                return diagnostics;

            string actionId = action.ActionId.ToString();
            for (int i = 0; i < report.Diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = report.Diagnostics[i];
                if (!diagnostic.Code.StartsWith("action.", StringComparison.Ordinal))
                    continue;

                if (diagnostic.Code == "action.id.invalid" && action.ActionId <= 0)
                    diagnostics.Add(diagnostic);
                else if (diagnostic.Code == "action.id.duplicate" && ContainsOrdinal(diagnostic.Message, actionId))
                    diagnostics.Add(diagnostic);
                else if (diagnostic.Code == "action.state.missing"
                    && (string.IsNullOrWhiteSpace(action.StateId)
                        || ContainsOrdinal(diagnostic.Message, action.StateId)))
                    diagnostics.Add(diagnostic);
                else if (ContainsOrdinal(diagnostic.Message, "id=" + actionId))
                    diagnostics.Add(diagnostic);
                else if (diagnostic.Code.StartsWith("action.condition.", StringComparison.Ordinal)
                    && HasInvalidCondition(action))
                    diagnostics.Add(diagnostic);
            }

            return diagnostics;
        }

        private static bool HasInvalidCondition(
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action)
        {
            if (action == null)
                return false;

            for (int i = 0; i < action.Conditions.Count; i++)
            {
                if (IsConditionInvalid(action.Conditions[i]))
                    return true;
            }

            return false;
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

        private void AddDiagnosticsSection(
            VisualElement root,
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return;

            root.Add(CreateSectionTitle("当前项诊断"));
            for (int i = 0; i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiPlannerDiagnostic diagnostic = diagnostics[i];
                root.Add(new HelpBox(
                    diagnostic.Code + ": " + diagnostic.Message,
                    ToHelpBoxType(diagnostic.Severity)));
            }
        }

        private void AddStateQuickFixes(
            VisualElement root,
            CharacterTestRuntimeAiPlannerEditorModel.StateDraft state,
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (state == null || diagnostics == null || diagnostics.Count == 0)
                return;

            VisualElement row = CreateButtonRow();
            if (HasDiagnosticCode(diagnostics, "state.parent.missing")
                || HasDiagnosticCode(diagnostics, "state.parent.cycle"))
            {
                row.Add(CreateMiniButton("清空父状态", () =>
                {
                    state.ParentStateId = string.Empty;
                    MarkDirty("已清空父状态");
                }));
            }

            if (row.childCount > 0)
            {
                root.Add(CreateSectionTitle("快捷修复"));
                root.Add(row);
            }
        }

        private void AddTransitionQuickFixes(
            VisualElement root,
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition,
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (transition == null || diagnostics == null || diagnostics.Count == 0)
                return;

            VisualElement row = CreateButtonRow();
            if (HasDiagnosticCode(diagnostics, "transition.source.missing"))
            {
                row.Add(CreateMiniButton("清空来源状态", () =>
                {
                    transition.SourceStateId = string.Empty;
                    MarkDirty("已清空切换来源状态");
                }));
            }

            if (HasDiagnosticCode(diagnostics, "transition.target.missing"))
            {
                row.Add(CreateMiniButton("指向首个状态", () =>
                {
                    transition.TargetStateId = _model.GetFirstStateId();
                    MarkDirty("已修复切换目标状态");
                }));
            }

            if (HasDiagnosticCodePrefix(diagnostics, "transition.condition."))
            {
                row.Add(CreateMiniButton("重置条件", () =>
                {
                    transition.Condition = new CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft();
                    MarkDirty("已重置切换条件");
                }));
            }

            if (row.childCount > 0)
            {
                root.Add(CreateSectionTitle("快捷修复"));
                root.Add(row);
            }
        }

        private void AddActionQuickFixes(
            VisualElement root,
            CharacterTestRuntimeAiPlannerEditorModel.ActionDraft action,
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (action == null || diagnostics == null || diagnostics.Count == 0)
                return;

            VisualElement row = CreateButtonRow();
            if (HasDiagnosticCode(diagnostics, "action.state.missing"))
            {
                row.Add(CreateMiniButton("挂到首个状态", () =>
                {
                    action.StateId = _model.GetFirstStateId();
                    _selectedStateId = action.StateId;
                    MarkDirty("已修复动作所属状态");
                }));
            }

            if (HasDiagnosticCode(diagnostics, "action.score.invalid"))
            {
                row.Add(CreateMiniButton("分数归零", () =>
                {
                    action.Score = 0f;
                    MarkDirty("已修复动作分数");
                }));
            }

            if (HasDiagnosticCode(diagnostics, "action.cost.invalid"))
            {
                row.Add(CreateMiniButton("Cost 归零", () =>
                {
                    action.Cost = 0f;
                    MarkDirty("已修复动作 Cost");
                }));
            }

            if (HasDiagnosticCode(diagnostics, "action.output.invalid"))
            {
                row.Add(CreateMiniButton("改为 Wait", () =>
                {
                    action.Kind = CharacterTestRuntimeAiPlannerActionKind.Wait;
                    MarkDirty("已修复动作输出类型");
                }));
            }

            if (HasDiagnosticCode(diagnostics, "action.output.slot.missing"))
            {
                row.Add(CreateMiniButton("设为技能槽 1", () =>
                {
                    action.AbilitySlot = 1;
                    MarkDirty("已修复技能槽");
                }));
            }

            if (HasDiagnosticCodePrefix(diagnostics, "action.condition."))
            {
                row.Add(CreateMiniButton("重置非法条件", () =>
                {
                    for (int i = 0; i < action.Conditions.Count; i++)
                    {
                        if (IsConditionInvalid(action.Conditions[i]))
                            action.Conditions[i] = new CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft();
                    }

                    MarkDirty("已重置动作非法条件");
                }));
            }

            if (row.childCount > 0)
            {
                root.Add(CreateSectionTitle("快捷修复"));
                root.Add(row);
            }
        }

        private static string DiagnosticPrefix(
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "  ";

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error)
                    return "!! ";
            }

            return "! ";
        }

        private static bool HasDiagnosticCode(
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics,
            string code)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (string.Equals(diagnostics[i].Code, code, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasDiagnosticCodePrefix(
            IReadOnlyList<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics,
            string prefix)
        {
            if (diagnostics == null)
                return false;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Code.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static HelpBoxMessageType ToHelpBoxType(
            CharacterTestRuntimeAiPlannerDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error:
                    return HelpBoxMessageType.Error;
                case CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning:
                    return HelpBoxMessageType.Warning;
                default:
                    return HelpBoxMessageType.Info;
            }
        }

    }

    internal static class CharacterTestRuntimeAiPlannerEditorRuleDiagnostics
    {
        public const string GlobalRuleMayBeCoveredCode = "transition.global.covered";

        public static void AddTransitionDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft> transitions,
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition,
            List<CharacterTestRuntimeAiPlannerDiagnostic> diagnostics)
        {
            if (diagnostics == null
                || !TryCreateGlobalCoverageDiagnostic(transitions, transition, out CharacterTestRuntimeAiPlannerDiagnostic diagnostic))
            {
                return;
            }

            diagnostics.Add(diagnostic);
        }

        public static bool TryCreateGlobalCoverageDiagnostic(
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft> transitions,
            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft transition,
            out CharacterTestRuntimeAiPlannerDiagnostic diagnostic)
        {
            diagnostic = default;
            if (transitions == null
                || transition == null
                || !string.IsNullOrWhiteSpace(transition.SourceStateId)
                || string.IsNullOrWhiteSpace(transition.TargetStateId))
            {
                return false;
            }

            CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft blocker = null;
            for (int i = 0; i < transitions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorModel.TransitionDraft candidate = transitions[i];
                if (candidate == null
                    || ReferenceEquals(candidate, transition)
                    || !string.IsNullOrWhiteSpace(candidate.SourceStateId)
                    || string.IsNullOrWhiteSpace(candidate.TargetStateId)
                    || string.Equals(candidate.TargetStateId, transition.TargetStateId, StringComparison.Ordinal)
                    || candidate.Priority <= transition.Priority
                    || !ConditionsMayOverlap(candidate.Condition, transition.Condition))
                {
                    continue;
                }

                if (blocker == null || candidate.Priority > blocker.Priority)
                    blocker = candidate;
            }

            if (blocker == null)
                return false;

            diagnostic = new CharacterTestRuntimeAiPlannerDiagnostic(
                CharacterTestRuntimeAiPlannerDiagnosticSeverity.Warning,
                GlobalRuleMayBeCoveredCode,
                "更高优先级的全局规则可能同时成立并覆盖当前规则: p="
                + blocker.Priority
                + " -> "
                + blocker.TargetStateId
                + " ("
                + CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatCondition(blocker.Condition)
                + ")");
            return true;
        }

        private static bool ConditionsMayOverlap(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft left,
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft right)
        {
            if (left == null || right == null)
                return true;

            if (left.Fact != right.Fact)
                return true;

            if (!CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGet(
                    left.Fact,
                    out CharacterTestRuntimeAiPlannerEditorFactMetadata metadata))
            {
                return true;
            }

            if (metadata.ValueType == CharacterTestRuntimeAiPlannerEditorFactValueType.Bool
                && TryResolveBoolValue(left, out bool leftValue)
                && TryResolveBoolValue(right, out bool rightValue))
            {
                return leftValue == rightValue;
            }

            if (left.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                && right.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals)
            {
                return Math.Abs(left.FloatValue - right.FloatValue) < 0.0001f;
            }

            if (TryResolveRange(left, out float leftMin, out float leftMax)
                && TryResolveRange(right, out float rightMin, out float rightMax))
            {
                return leftMin <= rightMax && rightMin <= leftMax;
            }

            return true;
        }

        private static bool TryResolveBoolValue(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            out bool value)
        {
            value = false;
            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals)
            {
                value = condition.BoolValue;
                return true;
            }

            if (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.NotEquals)
            {
                value = !condition.BoolValue;
                return true;
            }

            return false;
        }

        private static bool TryResolveRange(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            out float min,
            out float max)
        {
            min = float.NegativeInfinity;
            max = float.PositiveInfinity;
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiPlannerConditionOperator.Equals:
                    min = condition.FloatValue;
                    max = condition.FloatValue;
                    return true;
                case CharacterTestRuntimeAiPlannerConditionOperator.GreaterOrEqual:
                    min = condition.FloatValue;
                    return true;
                case CharacterTestRuntimeAiPlannerConditionOperator.LessOrEqual:
                    max = condition.FloatValue;
                    return true;
                default:
                    return false;
            }
        }
    }
}
