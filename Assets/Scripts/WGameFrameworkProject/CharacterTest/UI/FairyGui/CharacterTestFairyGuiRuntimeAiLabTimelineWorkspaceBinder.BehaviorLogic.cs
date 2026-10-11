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
        private void BindBehaviorLogicDecisionDetails(
            Fgui.GComponent details,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (HasFocusedBehaviorLogicTypedInput(details))
                return;

            _decisionDetailItem = null;
            const float decisionHeight = BehaviorPlanDecisionHeight;
            details.SetSize(RuntimeFrameEventDetailWidth, decisionHeight);
            SetObjectGeometry(
                details.GetChild("detailBackground"),
                0f,
                0f,
                RuntimeFrameEventDetailWidth,
                decisionHeight);
            _timelineDetail.SetInspectorChromeVisible(details, false);
            _timelineDetail.SetCausalSummaryVisible(details, false);
            SetBehaviorLogicInspectorChrome(details);
            ConfigureBehaviorLogicInspectorGeometry(details);
            SetBehaviorPlanDecisionFieldsVisible(details, true);
            ResetBehaviorLogicInspectorFieldInteractions(details);
            ResetBehaviorLogicTypedControlHost(details);
            SetTimelineChildVisible(details, "detailEntities", false);
            SetTimelineChildVisible(details, "detailMeta", false);

            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                viewModel?.BehaviorCardWorkspace?.Timeline;
            string cardTitle = string.IsNullOrWhiteSpace(timeline?.CardTitleText)
                ? "当前行为卡"
                : timeline.CardTitleText;
            BindText(
                details,
                "detailTitle",
                ResolveBehaviorLogicCardTitle(timeline, cardTitle) + " · 正在执行");
            Fgui.GTextField identity = details.GetChild("detailEntities")?.asTextField;
            if (identity != null)
            {
                identity.SetXY(36f, DecisionContentY + 142f);
                identity.SetSize(448f, 36f);
                identity.align = Fgui.AlignType.Left;
                identity.verticalAlign = Fgui.VertAlignType.Middle;
                identity.singleLine = true;
                SetTimelineFontSize(identity, 18);
            }

            if (timeline == null || !timeline.Available)
            {
                BindText(details, "detailEntities", "没有可编辑的 Logic v2");
                BindGameplayPlanRows(
                    details,
                    "当前选择", "—",
                    "校验", timeline?.ValidationBadgeText ?? "不可用",
                    "修复", "选择行为卡",
                    "应用", "不可用");
                ApplyGameplayPlanSummaryColors(details);
                return;
            }

            int index = timeline.SelectedElementIndex;
            switch (timeline.SelectedElementKind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                        index >= 0 && index < timeline.DetectorCount
                            && index < (timeline.Detectors?.Length ?? 0)
                                ? timeline.Detectors[index]
                                : null;
                    BindText(
                        details,
                        "planActionTitle",
                        "分支事件 · " + ResolveBehaviorLogicEditorName(
                            detector?.Title,
                            "选择检测器"));
                    BindGameplayPlanRows(
                        details,
                        "触发方式", ResolveBehaviorLogicDetectorTrigger(detector),
                        "检测事实", detector?.FactText ?? "尚未配置条件",
                        "监听范围", detector?.ScopeText ?? "—",
                        "监听窗口", detector == null
                            ? "—"
                            : detector.StartFrame.ToString(CultureInfo.InvariantCulture)
                                + "–"
                                + (detector.StartFrame + detector.DurationFrames)
                                    .ToString(CultureInfo.InvariantCulture)
                                + " 帧 · "
                                + ResolveBehaviorLogicDetectorWindowMode(detector.WindowEndMode));
                    BindBehaviorLogicDetectorTypedControls(details, timeline, detector);
                    BindBehaviorLogicContextSummary(
                        details,
                        "条件、结果与运行",
                        "检测事实 · " + (detector?.FactText ?? "尚未配置"),
                        detector == null
                            ? timeline.ValidationBadgeText
                            : BuildBehaviorLogicDetectorResultSummary(timeline, detector)
                                + " · " + detector.StatusText);
                    BindBehaviorLogicRepairAction(details, timeline, detector);
                    BindBehaviorLogicRemoveAction(details, timeline, "删除检测器");
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                        index >= 0 && index < timeline.TransitionCount
                            && index < (timeline.Transitions?.Length ?? 0)
                                ? timeline.Transitions[index]
                                : null;
                    BindText(
                        details,
                        "planActionTitle",
                        "出口 · " + ResolveBehaviorLogicEditorName(
                            transition?.DisplayTitleText,
                            "选择出口"));
                    BindGameplayPlanRows(
                        details,
                        "出口策略", transition?.PolicyText ?? "—",
                        "目标阶段", transition?.TargetText ?? "—",
                        "结果", transition?.EndsCard == true ? "结束行为卡" : "继续执行",
                        "当前状态", transition?.StatusText ?? timeline.ValidationBadgeText);
                    BindBehaviorLogicTransitionTypedControls(details, timeline, transition);
                    BindBehaviorLogicContextSummary(
                        details,
                        "路径结果",
                        "目标 · " + (transition?.TargetText ?? "尚未配置"),
                        transition == null
                            ? timeline.ValidationBadgeText
                            : (transition.EndsCard ? "结束行为卡" : "继续执行")
                                + " · " + transition.StatusText);
                    BindBehaviorLogicRemoveAction(details, timeline, "删除出口");
                    break;
                default:
                    CharacterTestRuntimeAiLabLogicStageViewModel stage =
                        index >= 0 && index < timeline.StageCount
                            && index < (timeline.Stages?.Length ?? 0)
                                ? timeline.Stages[index]
                                : timeline.StageCount > 0 && (timeline.Stages?.Length ?? 0) > 0
                                    ? timeline.Stages[0]
                                    : null;
                    BindText(
                        details,
                        "planActionTitle",
                        "主行为事件 · " + ResolveBehaviorLogicEditorName(
                            stage?.Title,
                            "选择阶段"));
                    int detectorCount = stage == null
                        ? 0
                        : CountBehaviorLogicStageDetectors(timeline, stage.Index);
                    int transitionCount = stage == null
                        ? 0
                        : CountBehaviorLogicStageTransitions(timeline, stage.Index);
                    bool entryEvent = timeline.Presentation.SelectedRecipeEventKind
                        == BehaviorRecipeEventKind.StageEntry;
                    bool movementStage = stage?.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                    if (entryEvent)
                    {
                        BindText(
                            details,
                            "planActionTitle",
                            "入口事件 · " + ResolveBehaviorLogicEditorName(stage?.Title, "选择时间轴"));
                        BindGameplayPlanRows(
                            details,
                            "时间轴", ResolveBehaviorLogicEditorName(stage?.Title, "未命名时间轴"),
                            "行为入口", stage?.Entry == true ? "是 · 行为从这里开始" : "否 · 由其他时间轴进入",
                            "行为激活", timeline.ActivationConditionSummaryText,
                            "阶段入口", stage?.EntryConditionSummaryText ?? "入口条件不可用");
                        BindBehaviorLogicContextSummary(
                            details,
                            "入口条件",
                            timeline.ActivationConditionSummaryText,
                            stage?.EntryConditionSummaryText ?? timeline.ValidationBadgeText);
                        HideBehaviorLogicStageActions(details);
                    }
                    else if (movementStage)
                    {
                        BindText(
                            details,
                            "planActionTitle",
                            ResolveBehaviorLogicEditorName(stage.Title, "移动行为"));
                        BindBehaviorLogicMovementStageInspector(
                            details,
                            timeline,
                            stage);
                        HideBehaviorLogicStageActions(details);
                    }
                    else
                    {
                        bool fixedDuration = stage?.DurationMode
                            == CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
                        string thirdLabel = fixedDuration ? "持续帧数" : "检测器";
                        string thirdValue = fixedDuration
                            ? Math.Max(1, stage?.DurationFrames ?? 1) + " 帧"
                            : detectorCount + " 个";
                        BindGameplayPlanRows(
                            details,
                            "阶段行为", stage == null
                                ? timeline.SummaryText
                                : CharacterTestRuntimeAiPlannerDevPresentationText
                                    .BuildBehaviorGameplayActionLabel(stage),
                            "持续方式", stage == null
                                ? "—"
                                : ResolveBehaviorLogicInspectorDuration(stage),
                            thirdLabel, thirdValue,
                            "出口", transitionCount + " 个");
                        BindBehaviorLogicStageTypedControls(details, timeline, stage);
                        BindBehaviorLogicContextSummary(
                            details,
                            "关联逻辑",
                            BuildBehaviorLogicStageDetectorSummary(timeline, stage),
                            BuildBehaviorLogicStageTransitionSummary(timeline, stage));
                        BindBehaviorLogicStageActions(details, timeline, stage != null);
                    }
                    break;
            }
            ApplyGameplayPlanSummaryColors(details);
            ApplyBehaviorLogicInspectorPresentation(details);
            if (Application.isPlaying)
                Fgui.Stage.inst?.ForceUpdate();
        }

        private static string ResolveBehaviorLogicEditorName(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string BuildBehaviorLogicStageDetectorSummary(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (timeline == null || stage == null)
                return "检测器 · 无";
            var builder = new System.Text.StringBuilder("检测器 · ");
            int count = 0;
            int detectorCount = Math.Min(timeline.DetectorCount, timeline.Detectors?.Length ?? 0);
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[i];
                if (detector?.StageIndex != stage.Index)
                    continue;
                if (count++ > 0)
                    builder.Append("  /  ");
                builder.Append(ResolveBehaviorLogicEditorName(detector.Title, "未命名检测器"));
            }
            return count == 0 ? "检测器 · 无" : builder.ToString();
        }

        private static string BuildBehaviorLogicStageTransitionSummary(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (timeline == null || stage == null)
                return "出口 · 无";
            var builder = new System.Text.StringBuilder("出口 · ");
            int count = 0;
            int transitionCount = Math.Min(
                timeline.TransitionCount,
                timeline.Transitions?.Length ?? 0);
            for (int i = 0; i < transitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                if (transition?.SourceStageIndex != stage.Index)
                    continue;
                if (count++ > 0)
                    builder.Append("  /  ");
                builder.Append(ResolveBehaviorLogicEditorName(
                    transition.DisplayTitleText,
                    transition.EndsCard ? "结束" : "未命名出口"));
            }
            return count == 0 ? "出口 · 无" : builder.ToString();
        }

        private static string BuildBehaviorLogicDetectorResultSummary(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (timeline == null || detector == null)
                return "结果 · 未配置";
            CharacterTestRuntimeAiLabLogicTransitionViewModel effective = null;
            var builder = new System.Text.StringBuilder(128);
            int count = 0;
            int transitionCount = Math.Min(
                timeline.TransitionCount,
                timeline.Transitions?.Length ?? 0);
            for (int i = 0; i < transitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = timeline.Transitions[i];
                if (transition?.DetectorIndex != detector.Index)
                    continue;
                if (effective == null
                    || transition.Priority > effective.Priority
                    || (transition.Priority == effective.Priority
                        && string.CompareOrdinal(transition.TransitionId, effective.TransitionId) < 0))
                    effective = transition;
                if (count++ > 0)
                    builder.Append(" / ");
                builder.Append(ResolveBehaviorLogicEditorName(
                    transition.TargetText,
                    transition.EndsCard ? "结束行为卡" : "未配置目标"));
            }
            if (count == 0)
                return detector.ConditionCount + " 个条件 · 结果未配置";
            return detector.ConditionCount + " 个条件 · 当前结果："
                + ResolveBehaviorLogicEditorName(effective?.TargetText, "未配置目标")
                + (count > 1 ? " · 全部 " + count + " 项：" + builder : string.Empty);
        }

        private static string ResolveBehaviorLogicInspectorDuration(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            switch (stage.DurationMode)
            {
                case CharacterTestRuntimeAiLabLogicDurationMode.Fixed:
                    return Math.Max(1, stage.DurationFrames) + " 帧";
                case CharacterTestRuntimeAiLabLogicDurationMode.Infinite:
                    return "持续运行";
                default:
                    return "直到事件发生";
            }
        }

        private static string ResolveBehaviorLogicMovementStrategy(
            CharacterTestRuntimeAiLabMovementStrategy strategy)
        {
            switch (strategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return "保持距离";
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return "环境避险";
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return "受阻横移";
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return "过近后撤";
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return "环绕目标";
                default:
                    return "接近目标";
            }
        }

        private static string ResolveBehaviorLogicMovementActionSummary(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return "常规移动";

            switch (stage.MovementStrategy)
            {
                case CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget:
                    return stage.MovementTargetKind switch
                    {
                        CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition =>
                            "常规移动  →  指定位置",
                        CharacterTestRuntimeAiLabMovementTargetKind.Entity =>
                            "常规移动  →  指定单位",
                        _ => "常规移动  →  目标"
                    };
                case CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance:
                    return "常规移动  ·  维持目标距离";
                case CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard:
                    return "常规移动  ·  远离危险区域";
                case CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked:
                    return "常规移动  ·  受阻时横移";
                case CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose:
                    return "常规移动  ·  目标过近时后撤";
                case CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget:
                    return "常规移动  ·  环绕目标";
                default:
                    return "常规移动";
            }
        }

        private static string ResolveBehaviorLogicDetectorTrigger(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector == null)
                return "—";
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return "主动作完成";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return "主动作失败";
                default:
                    return "条件成立沿";
            }
        }

        private static string ResolveBehaviorLogicDetectorWindowMode(
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode)
        {
            switch (mode)
            {
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd:
                    return "到阶段结束";
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite:
                    return "持续监听";
                default:
                    return "固定窗口";
            }
        }

        private static void ResetBehaviorLogicInspectorFieldInteractions(Fgui.GComponent details)
        {
            ResetBehaviorLogicInspectorField(details, "planActionStartValue");
            ResetBehaviorLogicInspectorField(details, "planActionDurationValue");
            ResetBehaviorLogicInspectorField(details, "planActionCompleteValue");
            ResetBehaviorLogicInspectorField(details, "planActionFailureValue");
        }

        private static void ResetBehaviorLogicInspectorField(Fgui.GComponent details, string childName)
        {
            Fgui.GObject field = details?.GetChild(childName);
            if (field == null)
                return;
            field.touchable = false;
            field.tooltips = string.Empty;
            field.onClick.Clear();
            field.onTouchBegin.Clear();
            DrawBehaviorLogicInspectorFieldSurface(details, childName, editable: false);
        }

        private void BindBehaviorLogicStageTypedControls(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return;
            bool enabled = CanEditBehaviorLogicFields(timeline);
            BindBehaviorLogicChoiceRow(
                details,
                "stageKind",
                "planActionStartValue",
                new[] { "技能", "动作", "移动", "等待", "逻辑" },
                new[]
                {
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait,
                    stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetStageKind(
                    stage.StageId,
                    index switch
                    {
                        0 => CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                        1 => CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                        2 => CharacterTestRuntimeAiLabCardStepKind.MoveToTarget,
                        3 => CharacterTestRuntimeAiLabCardStepKind.Wait,
                        _ => CharacterTestRuntimeAiLabCardStepKind.Logic
                    }));
            BindBehaviorLogicChoiceRow(
                details,
                "stageDuration",
                "planActionDurationValue",
                new[] { "固定", "到事件", "持续" },
                new[]
                {
                    stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                    stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.UntilEvent,
                    stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Infinite
                },
                enabled,
                index => _behaviorCardWorkspaceBinder.TrySetStageDurationMode(
                    stage.StageId,
                    (CharacterTestRuntimeAiLabLogicDurationMode)index));

            bool fixedDuration = stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
            bool movement = stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
            if (fixedDuration)
            {
                BindBehaviorLogicFrameStepper(
                    details,
                    "stageFrames",
                    "planActionCompleteValue",
                    stage.StageId,
                    Math.Max(1, stage.DurationFrames),
                    enabled);
            }
            if (movement)
            {
                BindBehaviorLogicChoiceRow(
                    details,
                    "movementStrategy",
                    fixedDuration ? "planActionFailureValue" : "planActionCompleteValue",
                    new[] { "接近", "距离带", "避险", "受阻横移", "过近后撤", "环绕" },
                    new[]
                    {
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget,
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance,
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard,
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.StrafeWhenBlocked,
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.RetreatWhenClose,
                        stage.MovementStrategy == CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget
                    },
                    enabled,
                    index => _behaviorCardWorkspaceBinder.TrySetMovementStrategy(
                        stage.StageId,
                        (CharacterTestRuntimeAiLabMovementStrategy)index));
            }
        }

        private void BindBehaviorLogicMovementStageInspector(
            Fgui.GComponent details,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (details == null || stage == null)
                return;

            HideBehaviorLogicInspectorRows(details);
            Fgui.GComponent host = EnsureBehaviorLogicTypedControlHost(details);
            if (host == null)
                return;

            bool enabled = CanEditBehaviorLogicFields(timeline);
            bool expanded = stage.MovementTargetKind
                == CharacterTestRuntimeAiLabMovementTargetKind.WorldPosition
                || stage.MovementTargetKind == CharacterTestRuntimeAiLabMovementTargetKind.Entity;
            ConfigureBehaviorLogicMovementInspectorGeometry(details);
            float editorHeight = expanded ? 196f : 128f;
            AddBehaviorLogicMovementSurface(
                host,
                "movementEditorSurface",
                36f,
                124f,
                448f,
                editorHeight,
                emphasized: true);
            BindBehaviorLogicMovementParameters(host, stage, enabled, 138f);
            AddBehaviorLogicMovementText(
                host,
                "movementInputError",
                string.Empty,
                48f,
                expanded ? 290f : 222f,
                408f,
                26f,
                17,
                RuntimeFrameSrgb(255, 130, 138));

            float feedbackY = 124f + editorHeight + 16f;
            float feedbackHeight = 604f - feedbackY;
            AddBehaviorLogicMovementSurface(
                host,
                "movementFeedbackSurface",
                36f,
                feedbackY,
                448f,
                feedbackHeight,
                emphasized: false);
            BindBehaviorLogicMovementFeedback(
                host,
                timeline?.ObservedTargetDistanceDecimeters ?? -1,
                stage,
                feedbackY,
                feedbackHeight);
        }

    }
}
