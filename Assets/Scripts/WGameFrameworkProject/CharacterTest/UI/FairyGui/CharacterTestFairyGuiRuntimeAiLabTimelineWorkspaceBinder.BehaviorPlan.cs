using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;
using TimelineGeometry = MxFramework.Game.CharacterTest.CharacterTestRuntimeAiLabTimelineGeometry;


namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder
    {
        private void BindDecisionDetails(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent details = timeline.GetChild("timelineEventDetails")?.asCom;
            Fgui.GComponent triggerInspector =
                (timeline.GetChild("behaviorRecipeInspector")
                    ?? timeline.GetChild("behaviorTriggerEventInspector"))?.asCom;
            if (details == null && triggerInspector == null)
                return;
            _timelineDetail.Suspend();
            _decisionEventDetails = details;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel behaviorTimeline =
                viewModel?.BehaviorCardWorkspace?.Timeline;
            BehaviorRecipeTriggerEventInspectorViewModel triggerModel =
                behaviorTimeline?.RecipeInspector;
            bool unifiedV6 = viewModel?.ShowBehaviorLogic == true
                && CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(
                    behaviorTimeline?.RecipePayload);
            if (unifiedV6)
            {
                if (details != null)
                    details.visible = false;
                if (triggerInspector != null)
                    _behaviorTriggerEventInspector.Bind(triggerInspector, triggerModel);
                return;
            }
            bool showTriggerInspector = viewModel?.ShowBehaviorLogic == true
                && triggerModel?.Available == true;
            if (details != null)
                details.visible = !showTriggerInspector;
            if (triggerInspector != null)
            {
                if (showTriggerInspector)
                    _behaviorTriggerEventInspector.Bind(triggerInspector, triggerModel);
                else
                    _behaviorTriggerEventInspector.Hide(triggerInspector);
            }
            if (showTriggerInspector)
                return;
            if (details == null)
                return;
            EnsureDecisionDetailInteractions(details);
            if (viewModel.ShowBehaviorPlan)
            {
                BindBehaviorPlanDecisionDetails(details, viewModel);
                return;
            }
            BindBehaviorLogicDecisionDetails(details, viewModel);
        }

        private void BindBehaviorPlanDecisionDetails(
            Fgui.GComponent details,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            SetBehaviorLogicInspectorFieldSurfacesVisible(details, false);
            SetTimelineChildVisible(details, "behaviorLogicTypedControls", false);
            _decisionDetailItem = null;
            bool showPlanDetail = _behaviorCardWorkspaceBinder?.GameplayPlanDetailVisible == true;
            const float decisionHeight = BehaviorPlanDecisionHeight;
            details.SetSize(RuntimeFrameEventDetailWidth, decisionHeight);
            SetObjectGeometry(
                details.GetChild("detailBackground"),
                0f,
                0f,
                RuntimeFrameEventDetailWidth,
                decisionHeight);
            Fgui.GGraph background = details.GetChild("detailBackground")?.asGraph;
            if (background != null)
            {
                background.DrawRect(
                    background.width,
                    background.height,
                    1,
                    RuntimeFrameSrgb(42, 66, 80, 220),
                    RuntimeFrameSrgb(5, 15, 22, 250));
            }

            _timelineDetail.SetInspectorChromeVisible(details, false);
            BindText(
                details,
                "detailTitle",
                showPlanDetail
                    ? "计划详情"
                    : _behaviorPlanTechnicalDetailsVisible ? "决策详情" : "下一行为候选");
            SetObjectGeometry(
                details.GetChild("detailTitle"),
                24f,
                DecisionContentY + 4f,
                300f,
                42f);
            SetTimelineFontSize(
                details.GetChild("detailTitle")?.asTextField,
                36);
            for (int i = 0; i < BehaviorPlanDetailHiddenChildren.Length; i++)
                SetTimelineChildVisible(details, BehaviorPlanDetailHiddenChildren[i], false);
            for (int i = 1; i <= RuntimeFrameEventDetailVisibleMemberCount; i++)
                SetTimelineChildVisible(details, "memberRow" + i, false);

            Fgui.GTextField context = details.GetChild("detailMeta")?.asTextField;
            SetTimelineChildVisible(details, "detailMeta", true);
            BindText(details, "detailMeta", BuildBehaviorPlanDecisionContext(viewModel));
            if (context != null)
            {
                context.SetXY(324f, DecisionContentY + 8f);
                context.SetSize(172f, 34f);
                context.align = Fgui.AlignType.Right;
                context.verticalAlign = Fgui.VertAlignType.Middle;
                SetTimelineFontSize(context, 18);
            }

            if (showPlanDetail)
            {
                _timelineDetail.SetCausalSummaryVisible(details, false);
                BindBehaviorPlanActionSummary(details, viewModel?.BehaviorCardWorkspace?.Timeline);
                return;
            }
            if (_behaviorPlanTechnicalDetailsVisible)
            {
                SetBehaviorPlanDecisionFieldsVisible(details, false);
                SetTimelineChildVisible(details, "detailEntities", false);
                _timelineDetail.BindCausalSummary(details, viewModel, compact: true);
                BindBehaviorPlanDecisionDetailsToggle(details, "返回候选列表");
                return;
            }

            _timelineDetail.SetCausalSummaryVisible(details, false);
            BindNextBehaviorCandidates(details, viewModel?.BehaviorCardWorkspace?.Timeline);
        }

        private void BindNextBehaviorCandidates(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            SetBehaviorPlanDecisionFieldsVisible(details, false);
            SetTimelineChildVisible(details, "detailEntities", false);
            SetTimelineChildVisible(details, "btnLocateStart", false);
            SetTimelineChildVisible(details, "btnLocateEnd", false);
            SetTimelineChildVisible(details, "btnCloseDetail", false);

            int count = _behaviorPlanDecisionBinder.BindCandidates(timeline);
            int visibleCount = Math.Min(8, count);
            if (visibleCount <= 0)
            {
                Fgui.GTextField empty = details.GetChild("detailEntities")?.asTextField;
                SetTimelineChildVisible(details, "detailEntities", true);
                if (empty != null)
                {
                    empty.SetXY(32f, DecisionContentY + 92f);
                    empty.SetSize(456f, 64f);
                    empty.align = Fgui.AlignType.Center;
                    empty.verticalAlign = Fgui.VertAlignType.Middle;
                    empty.singleLine = true;
                    empty.color = RuntimeFrameSrgb(137, 158, 169);
                    SetTimelineFontSize(empty, 21);
                    empty.text = "当前没有可用的重决策候选";
                }
                return;
            }

            const float startY = DecisionContentY + 58f;
            const float availableHeight = 330f;
            const float gap = 6f;
            float rowHeight = Mathf.Clamp(
                (availableHeight - (visibleCount - 1) * gap) / visibleCount,
                36f,
                52f);
            for (int index = 0; index < visibleCount; index++)
            {
                int sourceIndex = _behaviorPlanDecisionBinder.GetSourceIndex(index);
                BehaviorPlanCandidateRowSlot slot =
                    _behaviorPlanDecisionBinder.GetRow(details, index, _commands);
                _behaviorPlanDecisionBinder.BindRow(
                    slot,
                    timeline.BehaviorCandidates[sourceIndex],
                    startY + index * (rowHeight + gap),
                    rowHeight);
            }
            _behaviorPlanDecisionBinder.HideRows(visibleCount);
        }


        internal void HandleGameplayPlanDetailChanged()
        {
            if (_bindingRoot == null
                || (_boundViewModel?.ShowBehaviorPlan != true
                    && _boundViewModel?.ShowBehaviorLogic != true))
                return;
            Fgui.GComponent runtimeTimeline = _bindingRoot.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
            if (runtimeTimeline != null)
                BindDecisionDetails(runtimeTimeline, _boundViewModel);
        }


        private static string ResolveBehaviorLogicCardTitle(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            string cardTitle)
        {
            for (int i = 0; i < cardTitle.Length; i++)
            {
                char value = cardTitle[i];
                if (value >= '\u3400' && value <= '\u9fff')
                    return cardTitle;
            }
            CharacterTestRuntimeAiLabLogicStageViewModel stage =
                timeline?.StageCount > 0 && (timeline.Stages?.Length ?? 0) > 0
                    ? timeline.Stages[0]
                    : null;
            return stage == null
                ? "当前行为卡"
                : CharacterTestRuntimeAiPlannerDevPresentationText
                    .BuildBehaviorGameplayActionLabel(stage);
        }

        private static string BuildBehaviorPlanDecisionContext(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel runtime = viewModel?.RuntimeFrame;
            string agent = runtime == null || string.IsNullOrWhiteSpace(runtime.AgentSlotLabel)
                ? "—"
                : runtime.AgentSlotLabel;
            int frame = runtime?.SelectedFrame ?? viewModel?.TimelineSelectedFrame ?? 0;
            return agent
                + " · 第"
                + frame.ToString(CultureInfo.InvariantCulture)
                + "帧";
        }

        private void BindBehaviorPlanActionSummary(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            ConfigureBehaviorPlanActionSummaryGeometry(details);
            bool showPlanDetail = _behaviorCardWorkspaceBinder?.GameplayPlanDetailVisible == true;
            SetBehaviorPlanDecisionFieldsVisible(details, showPlanDetail);
            SetTimelineChildVisible(details, "detailEntities", showPlanDetail);
            if (!showPlanDetail)
                return;
            Fgui.GTextField stageIdentity = details.GetChild("detailEntities")?.asTextField;
            if (stageIdentity != null)
            {
                stageIdentity.SetXY(36f, DecisionContentY + 104f);
                stageIdentity.SetSize(448f, 42f);
                stageIdentity.align = Fgui.AlignType.Left;
                stageIdentity.verticalAlign = Fgui.VertAlignType.Middle;
                stageIdentity.singleLine = true;
                stageIdentity.sortingOrder = 20;
                SetTimelineFontSize(stageIdentity, 22);
            }

            GameplayReadablePlanProjection projection =
                _behaviorCardWorkspaceBinder?.GameplayPlanProjection;
            if (projection == null || projection.StageCount == 0)
            {
                BindEmptyGameplayPlanSummary(details, stageIdentity);
                return;
            }
            if (_behaviorCardWorkspaceBinder.GameplayPlanDetailVisible)
            {
                BindGameplayPlanSelectedDetail(
                    details,
                    stageIdentity,
                    projection,
                    _behaviorCardWorkspaceBinder.GameplayPlanDetailKind,
                    _behaviorCardWorkspaceBinder.GameplayPlanDetailStableId);
                return;
            }
            BindGameplayPlanOverview(details, stageIdentity, projection);
        }

        private void BindCurrentBehaviorSummary(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            ConfigureCurrentBehaviorSummaryGeometry(details);
            SetBehaviorPlanDecisionFieldsVisible(details, true);
            bool available = timeline?.CurrentBehaviorAvailable == true;
            bool warning = timeline?.CurrentBehaviorWarningVisible == true;
            GameplayReadablePlanStageProjection currentStage = null;
            GameplayReadablePlanProjection projection =
                _behaviorCardWorkspaceBinder?.GameplayPlanProjection;
            if (projection != null)
            {
                for (int i = 0; i < projection.StageCount; i++)
                {
                    GameplayReadablePlanStageProjection candidate = projection.GetStage(i);
                    if (candidate.Current)
                    {
                        currentStage = candidate;
                        break;
                    }
                    if (currentStage == null)
                        currentStage = candidate;
                }
            }
            bool hasCompletion = available
                && currentStage != null
                && !string.IsNullOrEmpty(currentStage.MetricCompletionText);
            SetTimelineChildVisible(details, "detailEntities", warning);

            BindText(
                details,
                "planActionTitle",
                available && currentStage != null
                    ? currentStage.StageLabel
                    : available ? timeline.CurrentBehaviorTitleText : "尚未运行行为");
            BindText(
                details,
                "detailEntities",
                warning ? timeline.CurrentBehaviorFactText : string.Empty);
            BindText(details, "planActionStartLabel", "动作");
            BindText(
                details,
                "planActionStartValue",
                available && currentStage != null
                    ? currentStage.ActionLabel
                    : available ? timeline.CurrentBehaviorCommandText : "无命令");
            BindText(details, "planActionCompleteLabel", warning ? "!" : "完成");
            BindText(
                details,
                "planActionCompleteValue",
                warning
                    ? timeline.CurrentBehaviorWarningText
                    : hasCompletion ? currentStage.MetricCompletionText : string.Empty);
            SetTimelineChildVisible(details, "planActionCompleteLabel", warning || hasCompletion);
            SetTimelineChildVisible(details, "planActionCompleteValue", warning || hasCompletion);

            SetTimelineChildVisible(details, "planActionDurationLabel", false);
            SetTimelineChildVisible(details, "planActionDurationValue", false);
            SetTimelineChildVisible(details, "planActionFailureLabel", false);
            SetTimelineChildVisible(details, "planActionFailureValue", false);
            SetTimelineChildVisible(details, "btnLocateEnd", false);
            SetTimelineChildVisible(details, "btnCloseDetail", false);

            Fgui.GTextField identity = details.GetChild("detailEntities")?.asTextField;
            if (identity != null)
            {
                identity.color = warning
                    ? RuntimeFrameSrgb(242, 181, 67)
                    : RuntimeFrameSrgb(198, 222, 232);
                BindTooltip(identity, timeline?.CurrentBehaviorTooltipText);
            }
            SetGameplayPlanValueColor(
                details,
                "planActionStartValue",
                RuntimeFrameSrgb(65, 225, 218));
            SetGameplayPlanValueColor(
                details,
                "planActionCompleteValue",
                warning ? RuntimeFrameSrgb(255, 187, 74) : RuntimeFrameSrgb(129, 190, 154));

            bool canRepair = warning
                && !string.IsNullOrEmpty(timeline?.CurrentBehaviorRepairDetectorStableId);
            Fgui.GButton repair = details.GetChild("btnLocateStart")?.asButton;
            SetTimelineChildVisible(details, "btnLocateStart", canRepair);
            if (repair != null)
            {
                repair.text = "修复";
                repair.SetXY(414f, DecisionContentY + 260f);
                repair.SetSize(70f, 40f);
                repair.titleFontSize = 18;
                repair.grayed = !canRepair;
                repair.touchable = canRepair;
                BindTooltip(repair, "打开“编辑逻辑”并定位到冲突条件；不会自动修改配置。");
            }
            BindBehaviorPlanDecisionDetailsToggle(details, "查看决策详情");
        }

        private static void ConfigureCurrentBehaviorSummaryGeometry(Fgui.GComponent details)
        {
            SetObjectGeometry(
                details.GetChild("planActionBackground"),
                24f,
                DecisionContentY + 56f,
                472f,
                210f);
            Fgui.GGraph background = details.GetChild("planActionBackground")?.asGraph;
            if (background != null)
            {
                background.DrawRect(
                    background.width,
                    background.height,
                    1,
                    RuntimeFrameSrgb(25, 94, 116, 220),
                    RuntimeFrameSrgb(7, 24, 34, 244));
            }
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionTitle")?.asTextField,
                40f,
                DecisionContentY + 66f,
                432f,
                44f,
                28);
            Fgui.GTextField fact = details.GetChild("detailEntities")?.asTextField;
            ConfigureBehaviorPlanActionText(
                fact,
                40f,
                DecisionContentY + 116f,
                432f,
                54f,
                21);
            if (fact != null)
                fact.singleLine = false;
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionStartLabel")?.asTextField,
                40f,
                DecisionContentY + 132f,
                116f,
                42f,
                19);
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionStartValue")?.asTextField,
                164f,
                DecisionContentY + 132f,
                308f,
                42f,
                22);
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionCompleteLabel")?.asTextField,
                40f,
                DecisionContentY + 190f,
                92f,
                42f,
                19);
            ConfigureBehaviorPlanActionText(
                details.GetChild("planActionCompleteValue")?.asTextField,
                164f,
                DecisionContentY + 190f,
                330f,
                42f,
                22);
        }

        private void BindBehaviorPlanDecisionDetailsToggle(
            Fgui.GComponent details,
            string label)
        {
            Fgui.GButton row = details.GetChild("memberRow5")?.asButton;
            if (row == null)
                return;
            row.visible = true;
            row.touchable = true;
            row.grayed = false;
            row.selected = false;
            row.data = null;
            row.text = label + "  ›";
            row.titleFontSize = 18;
            row.SetXY(24f, DecisionContentY + 330f);
            row.SetSize(472f, 42f);
            BindTooltip(row, label);
        }

        private void BindEmptyGameplayPlanSummary(
            Fgui.GComponent details,
            Fgui.GTextField identity)
        {
            BindText(details, "planActionTitle", "当前计划");
            BindText(details, "detailEntities", "当前行为卡没有可显示的计划");
            if (identity != null)
                BindTooltip(identity, string.Empty);
            BindGameplayPlanRows(
                details,
                "当前动作", "—",
                "当前状态", "尚未运行",
                "成功出口", "—",
                "失败出口", "—");
            ApplyGameplayPlanSummaryColors(details);
        }

        private void BindGameplayPlanOverview(
            Fgui.GComponent details,
            Fgui.GTextField identity,
            GameplayReadablePlanProjection projection)
        {
            GameplayReadablePlanStageProjection stage = projection.FindCurrentStage();
            if (stage == null)
                stage = projection.GetStage(0);
            GameplayReadablePlanExitProjection success = stage == null
                ? null
                : projection.FindPrimaryExit(
                    stage.SourceIndex,
                    GameplayReadablePlanExitTone.Success);
            GameplayReadablePlanExitProjection failure = stage == null
                ? null
                : projection.FindPrimaryExit(
                    stage.SourceIndex,
                    GameplayReadablePlanExitTone.Failure);
            BindText(details, "planActionTitle", "当前计划");
            BindText(
                details,
                "detailEntities",
                stage == null ? "没有可用阶段" : stage.RunningLabel);
            if (identity != null)
            {
                BindTooltip(identity, stage?.TooltipText);
                identity.color = RuntimeFrameSrgb(65, 225, 218);
            }
            BindGameplayPlanRows(
                details,
                "当前动作", stage?.ActionLabel ?? "—",
                "当前状态", ResolveGameplayPlanStageState(stage),
                "主要成功", success?.Label ?? "未配置成功出口",
                "失败出口", failure?.Label ?? "未配置失败出口");
            ApplyGameplayPlanSummaryColors(details);
            SetGameplayPlanValueTooltip(details, "planActionCompleteValue", success?.TooltipText);
            SetGameplayPlanValueTooltip(details, "planActionFailureValue", failure?.TooltipText);
        }

        private void BindGameplayPlanSelectedDetail(
            Fgui.GComponent details,
            Fgui.GTextField identity,
            GameplayReadablePlanProjection projection,
            GameplayReadablePlanDetailKind detailKind,
            string stableId)
        {
            if (detailKind == GameplayReadablePlanDetailKind.Exit)
            {
                GameplayReadablePlanExitProjection exit = projection.FindExit(stableId);
                if (exit == null)
                {
                    BindEmptyGameplayPlanSummary(details, identity);
                    return;
                }
                BindText(details, "planActionTitle", "分支出口详情");
                BindText(details, "detailEntities", exit.Label);
                if (identity != null)
                {
                    BindTooltip(identity, exit.TooltipText);
                    identity.color = ResolveGameplayPlanDetailColor(exit.Tone);
                }
                BindGameplayPlanRows(
                    details,
                    "出口类型", GameplayReadablePlanProjection.GetToneText(exit.Tone),
                    "监听窗口", exit.WindowText,
                    "目标", exit.TargetText,
                    "当前状态", GameplayReadablePlanProjection.GetRuntimeStateText(exit.RuntimeState));
                ApplyGameplayPlanDetailColors(details, exit.Tone);
                return;
            }

            GameplayReadablePlanStageProjection stage = projection.FindStage(stableId);
            if (stage == null)
            {
                BindEmptyGameplayPlanSummary(details, identity);
                return;
            }
            GameplayReadablePlanExitProjection success = projection.FindPrimaryExit(
                stage.SourceIndex,
                GameplayReadablePlanExitTone.Success);
            GameplayReadablePlanExitProjection failure = projection.FindPrimaryExit(
                stage.SourceIndex,
                GameplayReadablePlanExitTone.Failure);
            BindText(
                details,
                "planActionTitle",
                detailKind == GameplayReadablePlanDetailKind.Action
                    ? "主动作详情"
                    : "阶段详情");
            BindText(details, "detailEntities", stage.StageLabel);
            if (identity != null)
            {
                BindTooltip(identity, stage.TooltipText);
                identity.color = detailKind == GameplayReadablePlanDetailKind.Action
                    ? RuntimeFrameSrgb(75, 164, 237)
                    : RuntimeFrameSrgb(65, 225, 218);
            }
            BindGameplayPlanRows(
                details,
                "玩法说明", stage.ActionLabel,
                "计划窗口", stage.WindowText,
                "成功出口", success?.Label ?? "未配置成功出口",
                "失败出口", failure?.Label ?? "未配置失败出口");
            ApplyGameplayPlanSummaryColors(details);
            SetGameplayPlanValueTooltip(details, "planActionCompleteValue", success?.TooltipText);
            SetGameplayPlanValueTooltip(details, "planActionFailureValue", failure?.TooltipText);
        }

        private void BindGameplayPlanRows(
            Fgui.GComponent details,
            string firstLabel,
            string firstValue,
            string secondLabel,
            string secondValue,
            string thirdLabel,
            string thirdValue,
            string fourthLabel,
            string fourthValue)
        {
            BindText(details, "planActionStartLabel", firstLabel);
            BindText(details, "planActionStartValue", firstValue);
            BindText(details, "planActionDurationLabel", secondLabel);
            BindText(details, "planActionDurationValue", secondValue);
            BindText(details, "planActionCompleteLabel", thirdLabel);
            BindText(details, "planActionCompleteValue", thirdValue);
            BindText(details, "planActionFailureLabel", fourthLabel);
            BindText(details, "planActionFailureValue", fourthValue);
            SetGameplayPlanValueTooltip(details, "planActionStartValue", firstValue);
            SetGameplayPlanValueTooltip(details, "planActionDurationValue", secondValue);
            SetGameplayPlanValueTooltip(details, "planActionCompleteValue", thirdValue);
            SetGameplayPlanValueTooltip(details, "planActionFailureValue", fourthValue);
        }

        private static string ResolveGameplayPlanStageState(
            GameplayReadablePlanStageProjection stage)
        {
            if (stage == null)
                return "尚未运行";
            if (stage.ActionRunning)
                return "主动作执行中";
            if (stage.Current)
                return "当前阶段运行中";
            return "尚未进入";
        }

        private static void ApplyGameplayPlanSummaryColors(Fgui.GComponent details)
        {
            SetGameplayPlanValueColor(details, "planActionStartValue", RuntimeFrameSrgb(65, 225, 218));
            SetGameplayPlanValueColor(details, "planActionDurationValue", RuntimeFrameSrgb(205, 222, 230));
            SetGameplayPlanValueColor(details, "planActionCompleteValue", RuntimeFrameSrgb(71, 220, 116));
            SetGameplayPlanValueColor(details, "planActionFailureValue", RuntimeFrameSrgb(241, 91, 101));
        }

        private static void ApplyGameplayPlanDetailColors(
            Fgui.GComponent details,
            GameplayReadablePlanExitTone tone)
        {
            Color toneColor = ResolveGameplayPlanDetailColor(tone);
            SetGameplayPlanValueColor(details, "planActionStartValue", toneColor);
            SetGameplayPlanValueColor(details, "planActionDurationValue", RuntimeFrameSrgb(205, 222, 230));
            SetGameplayPlanValueColor(details, "planActionCompleteValue", toneColor);
            SetGameplayPlanValueColor(details, "planActionFailureValue", RuntimeFrameSrgb(205, 222, 230));
        }

        private static Color ResolveGameplayPlanDetailColor(GameplayReadablePlanExitTone tone)
        {
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return RuntimeFrameSrgb(71, 220, 116);
                case GameplayReadablePlanExitTone.Failure:
                    return RuntimeFrameSrgb(241, 91, 101);
                case GameplayReadablePlanExitTone.Condition:
                    return RuntimeFrameSrgb(237, 160, 49);
                default:
                    return RuntimeFrameSrgb(239, 116, 77);
            }
        }

        private static void SetGameplayPlanValueColor(
            Fgui.GComponent details,
            string childName,
            Color color)
        {
            Fgui.GTextField value = details?.GetChild(childName)?.asTextField;
            if (value != null)
                value.color = color;
        }

        private void SetGameplayPlanValueTooltip(
            Fgui.GComponent details,
            string childName,
            string tooltip)
        {
            Fgui.GObject value = details?.GetChild(childName);
            if (value != null)
                BindTooltip(value, tooltip);
        }

        private static void ConfigureBehaviorPlanActionSummaryGeometry(Fgui.GComponent details)
        {
            SetObjectGeometry(
                details.GetChild("planActionBackground"),
                24f,
                DecisionContentY + 56f,
                472f,
                332f);
            Fgui.GTextField title = details.GetChild("planActionTitle")?.asTextField;
            ConfigureBehaviorPlanActionText(
                title,
                36f,
                DecisionContentY + 64f,
                448f,
                36f,
                24);
            if (title != null)
                title.singleLine = true;
            ConfigureBehaviorPlanActionRow(
                details,
                "planActionStartLabel",
                "planActionStartValue",
                DecisionContentY + 154f);
            ConfigureBehaviorPlanActionRow(
                details,
                "planActionDurationLabel",
                "planActionDurationValue",
                DecisionContentY + 208f);
            ConfigureBehaviorPlanActionRow(
                details,
                "planActionCompleteLabel",
                "planActionCompleteValue",
                DecisionContentY + 262f);
            ConfigureBehaviorPlanActionRow(
                details,
                "planActionFailureLabel",
                "planActionFailureValue",
                DecisionContentY + 316f);
        }

        private static void ConfigureBehaviorPlanActionRow(
            Fgui.GComponent details,
            string labelName,
            string valueName,
            float y)
        {
            ConfigureBehaviorPlanActionText(
                details.GetChild(labelName)?.asTextField,
                36f,
                y,
                112f,
                44f,
                22);
            ConfigureBehaviorPlanActionText(
                details.GetChild(valueName)?.asTextField,
                156f,
                y,
                328f,
                44f,
                24);
        }

        private static void ConfigureBehaviorPlanActionText(
            Fgui.GTextField text,
            float x,
            float y,
            float width,
            float height,
            int fontSize)
        {
            if (text == null)
                return;
            text.SetXY(x, y);
            text.SetSize(width, height);
            text.verticalAlign = Fgui.VertAlignType.Middle;
            SetTimelineFontSize(text, fontSize);
        }

        private static void SetBehaviorPlanDecisionFieldsVisible(
            Fgui.GComponent details,
            bool visible)
        {
            for (int i = 0; i < BehaviorPlanDetailFieldNames.Length; i++)
                SetTimelineChildVisible(details, BehaviorPlanDetailFieldNames[i], visible);
            if (!visible)
            {
                SetBehaviorLogicInspectorFieldSurfacesVisible(details, false);
                SetTimelineChildVisible(details, "behaviorLogicTypedControls", false);
            }
        }

        private CharacterTestRuntimeAiLabTimelineDetailViewModel ResolveDecisionTimelineDetail(
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (_boundViewModel?.RuntimeFrame != null
                && _boundViewModel.RuntimeFrame.TryGetTimelineDetail(
                    item,
                    out CharacterTestRuntimeAiLabTimelineDetailViewModel detail))
            {
                return detail;
            }
            return CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, null);
        }

        private void EnsureDecisionDetailInteractions(Fgui.GComponent details)
        {
            if (_decisionDetailInteractionsBound && ReferenceEquals(_decisionEventDetails, details))
                return;
            _decisionEventDetails = details;
            _decisionDetailInteractionsBound = true;
            details.onTouchBegin.Set(context => context.StopPropagation());
            details.onTouchMove.Set(context => context.StopPropagation());
            details.onTouchEnd.Set(context => context.StopPropagation());
            details.onClick.Set(context => context.StopPropagation());

            BindStableTimelineDetailButton(details, "btnCloseDetail", () =>
                _timelineViewportIntentSink?.ClearSelection());
            BindStableTimelineDetailButton(details, "btnLocateStart", () =>
            {
                if (_boundViewModel?.ShowBehaviorPlan == true
                    && TryOpenCurrentBehaviorRepair())
                    return;
                if (_boundViewModel?.ShowBehaviorLogic == true
                    && (_behaviorCardWorkspaceBinder.TryAddDetectorToSelectedStage()
                        || _behaviorCardWorkspaceBinder.TryRepairSelectedLogicElement()))
                    return;
                EnqueueTimelineItemNavigation(_decisionDetailItem, false);
            });
            BindStableTimelineDetailButton(details, "btnLocateEnd", () =>
            {
                if (_boundViewModel?.ShowBehaviorLogic == true
                    && _behaviorCardWorkspaceBinder.TryRemoveSelectedLogicElement())
                    return;
                EnqueueTimelineItemNavigation(_decisionDetailItem, true);
            });

            for (int i = 1; i <= 4; i++)
            {
                Fgui.GButton row = details.GetChild("memberRow" + i)?.asButton;
                if (row == null)
                    continue;
                row.onTouchBegin.Set(context => context.StopPropagation());
                row.onTouchMove.Set(context => context.StopPropagation());
                row.onTouchEnd.Set(context => context.StopPropagation());
                row.onClick.Set(context =>
                {
                    context.StopPropagation();
                    CharacterTestRuntimeAiLabTimelineItem member = row.data as CharacterTestRuntimeAiLabTimelineItem;
                    if (member == null)
                        return;
                    if (context.inputEvent != null && context.inputEvent.isDoubleClick)
                    {
                        EnqueueTimelineItemNavigation(member, false);
                        return;
                    }
                    for (int rowIndex = 1;
                        rowIndex <= RuntimeFrameEventDetailVisibleMemberCount;
                        rowIndex++)
                    {
                        Fgui.GButton candidate = details.GetChild("memberRow" + rowIndex)?.asButton;
                        if (candidate != null)
                            candidate.selected = ReferenceEquals(candidate, row);
                    }
                    CharacterTestRuntimeAiLabTimelineDetailViewModel memberDetail =
                        ResolveDecisionTimelineDetail(member);
                    BindText(details, "detailMeta", memberDetail?.MetaText ?? string.Empty);
                });
            }

            Fgui.GButton behaviorPlanDetails = details.GetChild("memberRow5")?.asButton;
            if (behaviorPlanDetails != null)
            {
                behaviorPlanDetails.onTouchBegin.Set(context => context.StopPropagation());
                behaviorPlanDetails.onTouchMove.Set(context => context.StopPropagation());
                behaviorPlanDetails.onTouchEnd.Set(context => context.StopPropagation());
                behaviorPlanDetails.onClick.Set(context =>
                {
                    context.StopPropagation();
                    if (_boundViewModel?.ShowBehaviorPlan != true
                        || _behaviorCardWorkspaceBinder.GameplayPlanDetailVisible)
                    {
                        return;
                    }
                    _behaviorPlanTechnicalDetailsVisible = !_behaviorPlanTechnicalDetailsVisible;
                    BindBehaviorPlanDecisionDetails(details, _boundViewModel);
                });
            }

            if (details.displayObject != null)
            {
                details.displayObject.onMouseWheel.Set(context =>
                {
                    context.StopPropagation();
                });
            }
        }

        private bool TryOpenCurrentBehaviorRepair()
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _boundViewModel?.BehaviorCardWorkspace?.Timeline;
            string detectorId = timeline?.CurrentBehaviorRepairDetectorStableId;
            if (string.IsNullOrEmpty(detectorId) || _commandSink == null)
                return false;

            _behaviorPlanTechnicalDetailsVisible = false;
            _commandSink.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic,
                null));
            _commandSink.Enqueue(new MxUiCommand(
                _viewId,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(
                    detectorId,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector)));
            return true;
        }

        private static void BindStableTimelineDetailButton(
            Fgui.GComponent details,
            string name,
            Action action)
        {
            Fgui.GButton button = details.GetChild(name)?.asButton;
            if (button == null)
                return;
            button.onTouchBegin.Set(context => context.StopPropagation());
            button.onTouchMove.Set(context => context.StopPropagation());
            button.onTouchEnd.Set(context => context.StopPropagation());
            button.onClick.Set(context =>
            {
                context.StopPropagation();
                action?.Invoke();
            });
        }

        private void EnqueueTimelineItemNavigation(
            CharacterTestRuntimeAiLabTimelineItem item,
            bool useEndFrame)
        {
            if (item == null)
                return;
            _timelineViewportIntentSink?.Navigate(item.CreateLocator(), useEndFrame);
        }

    }
}
