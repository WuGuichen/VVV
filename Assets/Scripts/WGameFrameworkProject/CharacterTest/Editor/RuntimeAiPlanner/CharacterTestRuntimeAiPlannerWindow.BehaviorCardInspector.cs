using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildBehaviorCardStepInspector(VisualElement root)
        {
            EnsureBehaviorCardDrafts();

            var scroll = new ScrollView();
            scroll.name = BehaviorCardStepInspectorName;
            scroll.style.flexGrow = 1;
            root.Add(scroll);

            scroll.Add(CreateSectionTitle("步骤 Inspector"));
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft = GetSelectedBehaviorCardDraft();
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step = GetSelectedBehaviorCardStep();
            if (draft == null || step == null)
            {
                scroll.Add(CreateWrappedLabel("请选择行为卡和步骤。"));
                return;
            }

            scroll.Add(CreateReadOnlyLine("行为卡", draft.DisplayName));
            scroll.Add(CreateReadOnlyLine("步骤", step.StepId));
            scroll.Add(CreateReadOnlyLine("来源", draft.SourceText));
            scroll.Add(CreateSpacer(4));
            BuildBehaviorCardInspectorSummary(scroll, step);
            BuildBehaviorCardStandardInspector(scroll, draft, step);
            BuildBehaviorCardStepActionRow(scroll, draft);
        }

        private void BuildBehaviorCardStandardInspector(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView view =
                CharacterTestRuntimeAiPlannerBehaviorCardStandardInspector.BuildStepView(draft, step);

            var logic = CreateBehaviorCardSurface(
                new Color(0.07f, 0.10f, 0.12f),
                new Color(0.20f, 0.30f, 0.36f));
            logic.Add(CreateSectionTitle("执行逻辑"));
            logic.Add(BuildBehaviorCardInspectorMetric("步骤模板", view.TemplateName));
            logic.Add(BuildBehaviorCardInspectorMetric(view.ActionLabel, view.ActionValue));
            logic.Add(BuildBehaviorCardInspectorMetric("持续时间", view.DurationText));
            logic.Add(CreateWrappedLabel(view.TemplateDescription));
            root.Add(logic);

            BuildBehaviorCardTemplateParameterFields(root, step, view);
            BuildBehaviorCardGeneratedConditionSection(root, view.EntryConditions);
            BuildBehaviorCardStandardExitSection(root, draft, step, view);
        }

        private void BuildBehaviorCardTemplateParameterFields(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView view)
        {
            var parameters = CreateBehaviorCardSurface(
                new Color(0.06f, 0.08f, 0.10f),
                new Color(0.16f, 0.22f, 0.28f));
            parameters.Add(CreateSectionTitle("模板参数"));

            TextField title = CreateDelayedTextField("显示名称", step.Title);
            title.RegisterValueChangedCallback(evt =>
            {
                step.Title = evt.newValue ?? string.Empty;
                MarkBehaviorCardDraftDirty();
            });
            parameters.Add(title);

            parameters.Add(BuildBehaviorCardInspectorMetric("模板", view.TemplateName));
            switch (step.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility:
                    IntegerField ability = new IntegerField("Ability");
                    ability.value = step.AbilityId;
                    ability.RegisterValueChangedCallback(evt =>
                    {
                        step.AbilityId = Math.Max(0, evt.newValue);
                        MarkBehaviorCardDraftDirty();
                    });
                    parameters.Add(ability);
                    parameters.Add(CreateWrappedLabel("正式行为使用稳定 AbilityId；动作表现仍由 Ability / CharacterAction 绑定决定。"));
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAction:
                    TextField action = CreateDelayedTextField(
                        "CharacterAction",
                        step.CharacterActionId.IsValid ? step.CharacterActionId.ToString() : string.Empty);
                    action.RegisterValueChangedCallback(evt =>
                    {
                        step.CharacterActionId = CharacterActionId.TryParse(evt.newValue, out CharacterActionId parsed)
                            ? parsed
                            : CharacterActionId.None;
                        MarkBehaviorCardDraftDirty();
                    });
                    parameters.Add(action);
                    parameters.Add(CreateWrappedLabel("动作切换窗口、命中帧、root motion 和 VFX 仍由 CharacterAction 决定。"));
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.MoveToTarget:
                    TextField move = CreateDelayedTextField("距离参数", step.MoveDistanceBand);
                    move.RegisterValueChangedCallback(evt =>
                    {
                        step.MoveDistanceBand = evt.newValue ?? string.Empty;
                        MarkBehaviorCardDraftDirty();
                    });
                    parameters.Add(move);
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorCardStepKind.Wait:
                    IntegerField wait = new IntegerField("等待帧");
                    wait.value = step.WaitFrames;
                    wait.RegisterValueChangedCallback(evt =>
                    {
                        step.WaitFrames = Math.Max(0, evt.newValue);
                        MarkBehaviorCardDraftDirty();
                    });
                    parameters.Add(wait);
                    break;
            }

            root.Add(parameters);
        }

        private static void BuildBehaviorCardGeneratedConditionSection(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardConditionGroupView group)
        {
            var conditions = CreateBehaviorCardSurface(
                new Color(0.055f, 0.075f, 0.090f),
                new Color(0.14f, 0.22f, 0.28f));
            conditions.Add(CreateSectionTitle("进入条件（模板生成）"));
            conditions.Add(CreateWrappedLabel("当前版本只支持 AND 条件组；复杂表达式暂不在主编辑中开放。"));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 4;
            conditions.Add(row);
            for (int i = 0; group != null && i < group.Presets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardConditionPresetView preset = group.Presets[i];
                Label chip = CreateMiniBadge(
                    preset.Label + ": " + preset.Value,
                    new Color(0.12f, 0.24f, 0.32f));
                chip.tooltip = preset.Kind.ToString();
                row.Add(chip);
            }

            if (group == null || group.Presets.Count == 0)
                conditions.Add(CreateWrappedLabel("无模板保护条件。"));
            else
                conditions.Add(CreateReadOnlyLine("组合", group.OperatorText));

            if (group != null && !group.IsMainEditorCompatible)
            {
                conditions.Add(CreateMiniBadge(
                    group.WarningText,
                    new Color(0.42f, 0.24f, 0.08f)));
            }

            root.Add(conditions);
        }

        private void BuildBehaviorCardStandardExitSection(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStandardStepView view)
        {
            var exits = CreateBehaviorCardSurface(
                new Color(0.055f, 0.070f, 0.090f),
                new Color(0.16f, 0.23f, 0.30f));
            exits.Add(CreateSectionTitle("出口规则"));
            exits.Add(CreateWrappedLabel("同一行为链只有一个执行中的节点；执行中监听命中后会退出当前节点并进入目标节点。"));

            exits.Add(CreateSectionTitle("执行中监听出口"));
            if (view.RunningExits.Count == 0)
                exits.Add(CreateWrappedLabel("无执行中监听。"));
            else
            {
                for (int i = 0; i < view.RunningExits.Count; i++)
                    exits.Add(BuildBehaviorCardStandardExitCard(draft, step, view.RunningExits[i]));
            }

            exits.Add(CreateSectionTitle("完成出口"));
            exits.Add(BuildBehaviorCardStandardExitCard(draft, step, view.CompletionExit));

            exits.Add(CreateSectionTitle("失败/中断出口"));
            exits.Add(BuildBehaviorCardStandardExitCard(draft, step, view.FailureExit));
            root.Add(exits);
        }

        private VisualElement BuildBehaviorCardStandardExitCard(
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            CharacterTestRuntimeAiPlannerBehaviorCardStepExitView exit)
        {
            Color border = exit.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.RunningMonitor
                ? new Color(0.22f, 0.48f, 0.84f)
                : exit.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Failure
                    ? new Color(0.90f, 0.42f, 0.16f)
                    : new Color(0.24f, 0.62f, 0.30f);
            var card = CreateBehaviorCardSurface(new Color(0.070f, 0.090f, 0.110f), border);
            CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch =
                FindBehaviorCardBranchById(step, exit.BranchId);
            bool selected = branch != null
                && string.Equals(branch.BranchId, _selectedBehaviorCardBranchId, StringComparison.Ordinal);
            if (selected)
            {
                card.style.borderTopWidth = 2;
                card.style.borderBottomWidth = 2;
                card.style.borderLeftWidth = 2;
                card.style.borderRightWidth = 2;
            }

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            card.Add(header);
            header.Add(CreateMiniBadge(FormatBehaviorCardExitKind(exit.Kind), border));
            Label label = CreateWrappedLabel(string.IsNullOrWhiteSpace(exit.Label) ? "未配置" : exit.Label);
            label.style.flexGrow = 1;
            header.Add(label);
            if (selected)
                header.Add(CreateMiniBadge("当前选中", new Color(0.24f, 0.52f, 0.90f)));

            if (branch == null)
            {
                card.Add(CreateWrappedLabel("缺少出口分支，请先修复结构化分支数据。"));
                return card;
            }

            card.Add(CreateReadOnlyLine("条件", string.IsNullOrWhiteSpace(exit.ConditionText) ? "动作完成" : exit.ConditionText));
            card.Add(CreateReadOnlyLine("窗口", exit.WindowText));
            List<string> choices =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildStepChoiceLabels(draft);
            card.Add(CreateBranchField("目标步骤", choices, branch.TargetStepId, value =>
            {
                branch.TargetStepId = value;
                MarkBehaviorCardDraftDirty();
            }));
            return card;
        }

        private static string FormatBehaviorCardExitKind(
            CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.RunningMonitor:
                    return "监听";
                case CharacterTestRuntimeAiPlannerBehaviorCardStepExitKind.Failure:
                    return "失败";
                default:
                    return "完成";
            }
        }

        private void BuildBehaviorCardStepActionRow(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardDraft draft)
        {
            var actions = CreateButtonRow();
            root.Add(actions);
            actions.Add(CreateMiniButton("上移", () => MoveSelectedBehaviorCardStep(-1)));
            actions.Add(CreateMiniButton("下移", () => MoveSelectedBehaviorCardStep(1)));
            Button delete = CreateMiniButton("删除步骤", DeleteSelectedBehaviorCardStep);
            delete.name = BehaviorCardDeleteStepButtonName;
            delete.SetEnabled(draft.Steps.Count > CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.MinStepCount);
            actions.Add(delete);
            Button save = CreatePrimaryButton("保存草稿（内存）", SaveSelectedBehaviorCardDraft);
            save.name = BehaviorCardSaveDraftButtonName;
            actions.Add(save);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft FindBehaviorCardBranchById(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step,
            string branchId)
        {
            if (step == null || string.IsNullOrWhiteSpace(branchId))
                return null;

            for (int i = 0; i < step.Branches.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCardBranchDraft branch = step.Branches[i];
                if (branch != null && string.Equals(branch.BranchId, branchId, StringComparison.Ordinal))
                    return branch;
            }

            return null;
        }

        private static void BuildBehaviorCardInspectorSummary(
            VisualElement root,
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            CharacterTestRuntimeAiPlannerBehaviorCardStepTimingPreview timing =
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.BuildFlowTimePreview(
                    BuildSingleStepDraft(step)).Steps[0];

            var summary = CreateBehaviorCardSurface(
                new Color(0.08f, 0.10f, 0.12f),
                new Color(0.18f, 0.24f, 0.30f));
            summary.Add(CreateSectionTitle("当前步骤摘要"));
            summary.Add(BuildBehaviorCardInspectorMetric("动作", FormatBehaviorCardOutput(step)));
            summary.Add(BuildBehaviorCardInspectorMetric(
                "持续",
                timing.DurationKnown
                    ? timing.DurationFrames.ToString(CultureInfo.InvariantCulture) + " frames"
                    : "? frames"));
            summary.Add(BuildBehaviorCardInspectorMetric("命中窗口", EstimateHitWindow(step)));
            summary.Add(BuildBehaviorCardInspectorMetric("切换窗口", EstimateTransitionWindow(step)));
            summary.Add(BuildBehaviorCardInspectorMetric("距离判断", EstimateDistanceWindow(step)));
            summary.Add(BuildBehaviorCardInspectorMetric(
                "下一步",
                CharacterTestRuntimeAiPlannerBehaviorCardEditorModel.GetDefaultSuccessTarget(step)));
            summary.Add(BuildBehaviorCardInspectorMetric("异常", string.IsNullOrWhiteSpace(timing.WarningText) ? "End" : timing.WarningText));
            root.Add(summary);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCardDraft BuildSingleStepDraft(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            var draft = new CharacterTestRuntimeAiPlannerBehaviorCardDraft("preview", "preview", "preview");
            draft.Steps.Add(step);
            return draft;
        }

        private static VisualElement BuildBehaviorCardInspectorMetric(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 5;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.backgroundColor = new Color(0.10f, 0.13f, 0.16f);

            Label name = CreateWrappedLabel(label);
            name.style.width = 82;
            name.style.color = new Color(0.72f, 0.78f, 0.84f);
            row.Add(name);

            Label text = CreateWrappedLabel(value);
            text.style.flexGrow = 1;
            text.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(text);
            return row;
        }

        private static string EstimateHitWindow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step.Kind == CharacterTestRuntimeAiPlannerBehaviorCardStepKind.UseAbility)
                return "6 - 20";
            if (step.CharacterActionId == new CharacterActionId(301))
                return "18 - 24";
            if (step.CharacterActionId == new CharacterActionId(324))
                return "20 - 26";
            return "-";
        }

        private static string EstimateTransitionWindow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step.CharacterActionId == new CharacterActionId(301))
                return "62 - 72";
            return "-";
        }

        private static string EstimateDistanceWindow(
            CharacterTestRuntimeAiPlannerBehaviorCardStepDraft step)
        {
            if (step.ConditionWindowEndFrame <= 0)
                return "-";
            return step.ConditionWindowStartFrame.ToString(CultureInfo.InvariantCulture)
                + " - "
                + step.ConditionWindowEndFrame.ToString(CultureInfo.InvariantCulture);
        }
    }
}
