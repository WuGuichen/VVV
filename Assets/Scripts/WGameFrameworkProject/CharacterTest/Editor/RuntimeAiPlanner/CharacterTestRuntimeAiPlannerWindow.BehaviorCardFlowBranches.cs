using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static string FormatBehaviorCardOutput(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step == null)
                return "无输出";

            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    return step.AbilityId > 0 ? "DashStrikeAbilityId" : "鏈粦瀹?Ability";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    return !step.CharacterActionId.IsValid
                        ? "鏈粦瀹?CharacterAction"
                        : step.CharacterActionId.ToString();
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    return "MoveToTarget";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    return "Wait";
                default:
                    return "End";
            }
        }

        private static VisualElement BuildBehaviorCardBranchLine(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 4;

            if (string.IsNullOrWhiteSpace(branch.FromStepId))
            {
                row.Add(CreateMiniBadge("?", new Color(0.28f, 0.28f, 0.28f)));
                Label missing = CreateWrappedLabel("分支未配置");
                missing.style.flexGrow = 1;
                row.Add(missing);
                return row;
            }

            Color color = GetBehaviorCardBranchColor(branch.Kind);
            Label arrow = CreateWrappedLabel(GetBehaviorCardBranchArrow(branch.Kind));
            arrow.style.width = 30;
            arrow.style.fontSize = 18;
            arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            arrow.style.color = color;
            row.Add(arrow);

            Label text = CreateWrappedLabel(FormatBehaviorCardBranchLine(branch));
            text.style.flexGrow = 1;
            text.style.color = new Color(0.86f, 0.90f, 0.94f);
            row.Add(text);
            return row;
        }

        private static string FormatBehaviorCardBranchLine(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            string target = branch.TargetIsEnd ? "End" : ShortenForUi(branch.TargetTitle, 10);
            string reason = BuildBehaviorCardBranchReason(branch);
            return reason + " -> " + target;
        }

        private static string FormatBehaviorCardBranchChip(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            string target = branch.TargetIsEnd ? "End" : ShortenForUi(branch.TargetTitle, 14);
            if (!string.IsNullOrWhiteSpace(branch.Label))
                return ShortenForUi(branch.Label, 16) + " -> " + target;

            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success)
                return "鎴愬姛 -> " + target;
            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                return "澶辫触 -> " + target;

            return "鏉′欢 -> " + target;
        }

        private static string BuildBehaviorCardBranchReason(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (!string.IsNullOrWhiteSpace(branch.Label))
                return branch.Label;

            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet)
            {
                return "绐楀彛 "
                    + branch.WindowStartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + branch.WindowEndFrame.ToString(CultureInfo.InvariantCulture);
            }

            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success)
                return "鎴愬姛瀹屾垚";
            if (branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure)
                return branch.TargetIsEnd ? "失败/中断" : "失败后继续";

            return "鏉′欢婊¤冻";
        }

        private static Color GetBehaviorCardBranchColor(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success:
                    return new Color(0.30f, 0.85f, 0.42f);
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure:
                    return new Color(1.0f, 0.65f, 0.18f);
                default:
                    return new Color(0.28f, 0.58f, 1.0f);
            }
        }

        private static string GetBehaviorCardBranchArrow(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success:
                    return "=>";
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure:
                    return "!>";
                default:
                    return "~>";
            }
        }

        private static VisualElement BuildBehaviorCardBranchChip(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            if (string.IsNullOrWhiteSpace(branch.FromStepId))
                return CreateMiniBadge("分支未配置", new Color(0.28f, 0.28f, 0.28f));

            Color color;
            switch (branch.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success:
                    color = new Color(0.12f, 0.28f, 0.16f);
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure:
                    color = new Color(0.34f, 0.14f, 0.14f);
                    break;
                default:
                    color = new Color(0.12f, 0.20f, 0.36f);
                    break;
            }

            var chip = new VisualElement();
            chip.style.marginRight = 5;
            chip.style.marginBottom = 5;
            chip.style.paddingLeft = 6;
            chip.style.paddingRight = 6;
            chip.style.paddingTop = 4;
            chip.style.paddingBottom = 4;
            chip.style.backgroundColor = color;
            chip.style.maxWidth = 260;
            chip.Add(CreateWrappedLabel(FormatBranchChipText(branch)));
            return chip;
        }

        private static string FormatBranchChipText(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch)
        {
            string window = branch.Kind == CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.ConditionMet
                ? " @ "
                    + branch.WindowStartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + branch.WindowEndFrame.ToString(CultureInfo.InvariantCulture)
                    + "f"
                : string.Empty;
            return FormatBranchKind(branch.Kind)
                + window
                + ": "
                + ShortenBranchReason(branch.ConditionSummary)
                + " -> "
                + (branch.TargetIsEnd ? "End" : branch.TargetTitle);
        }

        private static string ShortenBranchReason(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "姝ラ缁撴潫";

            return text.Length <= 34 ? text : text.Substring(0, 34) + "...";
        }

        private static string ShortenForUi(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (maxLength <= 3 || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength - 3) + "...";
        }

        private static string FormatFrameRange(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step)
        {
            return step.ConditionWindowStartFrame.ToString(CultureInfo.InvariantCulture)
                + "-"
                + step.ConditionWindowEndFrame.ToString(CultureInfo.InvariantCulture);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview FindBehaviorCardStepTiming(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId)
        {
            if (preview == null)
                return default;

            for (int i = 0; i < preview.Steps.Count; i++)
            {
                if (string.Equals(preview.Steps[i].StepId, stepId, StringComparison.Ordinal))
                    return preview.Steps[i];
            }

            return default;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardStepDraft FindBehaviorCardStepById(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return null;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step != null && string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                    return step;
            }

            return null;
        }

        private static int FindBehaviorCardStepIndexById(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            string stepId)
        {
            if (draft == null || string.IsNullOrWhiteSpace(stepId))
                return -1;

            for (int i = 0; i < draft.Steps.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = draft.Steps[i];
                if (step != null && string.Equals(step.StepId, stepId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview FindBehaviorCardBranch(
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview,
            string stepId,
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind)
        {
            if (preview == null)
                return default;

            for (int i = 0; i < preview.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchPreview branch = preview.Branches[i];
                if (branch.Kind == kind
                    && string.Equals(branch.FromStepId, stepId, StringComparison.Ordinal))
                {
                    return branch;
                }
            }

            return default;
        }

        private static void BuildBehaviorCardTerminalPanel(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardFlowTimePreview preview)
        {
            var terminalPanel = new VisualElement();
            terminalPanel.name = BehaviorCardTerminalPanelName;
            terminalPanel.style.marginTop = 8;
            terminalPanel.style.marginBottom = 8;
            terminalPanel.style.paddingTop = 7;
            terminalPanel.style.paddingBottom = 7;
            terminalPanel.style.paddingLeft = 8;
            terminalPanel.style.paddingRight = 8;
            terminalPanel.style.alignSelf = Align.Center;
            terminalPanel.style.minWidth = 280;
            terminalPanel.style.backgroundColor = new Color(0.09f, 0.11f, 0.13f);
            terminalPanel.style.borderTopWidth = 1;
            terminalPanel.style.borderBottomWidth = 1;
            terminalPanel.style.borderLeftWidth = 1;
            terminalPanel.style.borderRightWidth = 1;
            terminalPanel.style.borderTopColor = new Color(0.32f, 0.36f, 0.40f);
            terminalPanel.style.borderBottomColor = new Color(0.32f, 0.36f, 0.40f);
            terminalPanel.style.borderLeftColor = new Color(0.32f, 0.36f, 0.40f);
            terminalPanel.style.borderRightColor = new Color(0.32f, 0.36f, 0.40f);
            root.Add(terminalPanel);
            Label title = CreateSectionTitle("End");
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            terminalPanel.Add(title);
            Label subtitle = CreateWrappedLabel("交回行为组重新决策");
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            terminalPanel.Add(subtitle);
            if (preview == null || preview.Terminals.Count == 0)
            {
                terminalPanel.Add(CreateWrappedLabel("当前没有显式终点。"));
                return;
            }

            for (int i = 0; i < preview.Terminals.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardTerminalPreview terminal = preview.Terminals[i];
                Label incoming = CreateWrappedLabel(
                    terminal.IncomingBranchCount.ToString(CultureInfo.InvariantCulture)
                    + " 条分支进入终点");
                incoming.style.unityTextAlign = TextAnchor.MiddleCenter;
                incoming.style.color = new Color(0.72f, 0.78f, 0.84f);
                terminalPanel.Add(incoming);
            }
        }

        private static string FormatStepTimingLabel(
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview step)
        {
            string range = step.DurationKnown
                ? step.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + step.EndFrame.ToString(CultureInfo.InvariantCulture)
                : "?";
            return step.StepId
                + " ["
                + range
                + "] "
                + step.DurationSource;
        }

        private static string FormatBranchKind(
            CharacterTestRuntimeAiPlannerBehaviorCardBranchKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Success:
                    return "鎴愬姛鍒嗘敮";
                case CharacterTestRuntimeAiPlannerBehaviorCardBranchKind.Failure:
                    return "澶辫触鍒嗘敮";
                default:
                    return "鏉′欢婊¤冻鍒嗘敮";
            }
        }
    }
}
