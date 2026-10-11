using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class BehaviorRecipeTimelineProjection
    {
        private void AddEntryTrack(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            BehaviorRecipeTrackViewModel track = GetOrCreateTrack();
            track.Kind = BehaviorRecipeTrackKind.Entry;
            track.StageIndex = stage.Index;
            track.VerticalOrder = timeline.TrackCount;
            track.DisplayName = "入口条件";
            track.StableKey = "track:" + stage.StageId + ":entry";
            BehaviorRecipeEventViewModel recipeEvent = GetOrCreateEvent();
            ResetEvent(recipeEvent);
            recipeEvent.StableKey = "event:" + stage.StageId + ":entry";
            recipeEvent.Kind = BehaviorRecipeEventKind.StageEntry;
            recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Marker;
            recipeEvent.SelectionKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            recipeEvent.SelectionIndex = stage.Index;
            recipeEvent.StageIndex = stage.Index;
            recipeEvent.StageId = stage.StageId;
            string entryConditionTitle = string.IsNullOrWhiteSpace(stage.EntryConditionSummaryText)
                ? stage.EntryConditionCount + " 条入口条件"
                : stage.EntryConditionSummaryText;
            recipeEvent.Title = timeline.Title + " · " + entryConditionTitle;
            recipeEvent.Detail = stage.Entry
                ? "行为从本阶段进入，并在 0f 检查这些入口条件"
                : "进入本阶段时在 0f 检查这些入口条件";
            recipeEvent.StartFrame = 0;
            recipeEvent.DurationFrames = 1;
            recipeEvent.Selected = IsSelected(source, recipeEvent, stage.Selected);
            recipeEvent.Running = stage.Current && LocalPlayheadFrame == 0;
            track.Event = recipeEvent;
            timeline.AddTrack(track);
        }

        private void AddPrimaryActionTrack(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            BehaviorRecipeTrackViewModel track = GetOrCreateTrack();
            track.Kind = BehaviorRecipeTrackKind.PrimaryAction;
            track.StageIndex = stage.Index;
            track.VerticalOrder = timeline.TrackCount;
            track.DisplayName = (stage.Current ? "▶ " : string.Empty)
                + (stage.Entry ? "入口 · " : string.Empty)
                + (timeline.Order + 1).ToString("00")
                + " "
                + timeline.Title;
            track.StableKey = "track:" + stage.StageId + ":primary";
            BehaviorRecipeEventViewModel recipeEvent = GetOrCreateEvent();
            ResetEvent(recipeEvent);
            recipeEvent.StableKey = "event:" + stage.StageId + ":primary";
            recipeEvent.Kind = BehaviorRecipeEventKind.PrimaryAction;
            recipeEvent.SelectionKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            recipeEvent.SelectionIndex = stage.Index;
            recipeEvent.StageIndex = stage.Index;
            recipeEvent.StageId = stage.StageId;
            recipeEvent.StartFrame = 0;
            recipeEvent.Title = (stage.Current ? "▶ " : string.Empty)
                + (stage.Entry ? "入口 · " : string.Empty)
                + (timeline.Order + 1).ToString("00", CultureInfo.InvariantCulture)
                + " "
                + timeline.Title
                + " · "
                + ResolvePrimaryActionTitle(stage);
            recipeEvent.Detail = stage.DurationModeText ?? string.Empty;
            recipeEvent.Selected = IsSelected(source, recipeEvent, stage.Selected);
            recipeEvent.Running = stage.Current && (stage.ActionRunning || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic);
            recipeEvent.ProgressFrames = stage.ActionProgressFrames >= 0
                ? stage.ActionProgressFrames
                : stage.Current ? LocalPlayheadFrame : -1;
            ResolvePrimaryActionGeometry(stage, recipeEvent);
            MaximumLocalFrame = Math.Max(
                MaximumLocalFrame,
                recipeEvent.StartFrame + Math.Max(1, recipeEvent.DurationFrames));
            track.Event = recipeEvent;
            timeline.AddTrack(track);
        }

        private void AddBranchTrack(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel source,
            BehaviorRecipeTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int detectorIndex,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            bool shared)
        {
            if (detector == null)
                return;
            BehaviorRecipeTrackViewModel track = GetOrCreateTrack();
            track.Kind = shared
                ? BehaviorRecipeTrackKind.LegacySharedBranch
                : BehaviorRecipeTrackKind.Branch;
            track.StageIndex = timeline.StageIndex;
            track.VerticalOrder = timeline.TrackCount;
            track.DisplayName = shared
                ? "共享分支"
                : "分支 " + (CountBranchTracks(timeline) + 1);
            track.StableKey = "track:" + timeline.StageId + ":detector:" + detector.DetectorId;
            BehaviorRecipeEventViewModel recipeEvent = GetOrCreateEvent();
            ResetEvent(recipeEvent);
            recipeEvent.StableKey = "event:" + timeline.StageId + ":detector:" + detector.DetectorId;
            recipeEvent.Kind = shared
                ? BehaviorRecipeEventKind.LegacySharedBranch
                : BehaviorRecipeEventKind.Branch;
            recipeEvent.SelectionKind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
            recipeEvent.SelectionIndex = detectorIndex;
            recipeEvent.StageIndex = timeline.StageIndex;
            recipeEvent.DetectorIndex = detectorIndex;
            recipeEvent.StageId = timeline.StageId;
            recipeEvent.DetectorId = detector.DetectorId;
            recipeEvent.EventId = NormalizeEventId(detector.DetectorId);
            recipeEvent.Priority = detector.Priority;
            recipeEvent.Shared = shared;
            int resolvedLocalFrame = detector.LocalFrame > 0
                ? detector.LocalFrame
                : detector.LocalFrameAnchor
                    == CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd
                        ? ResolvePrimaryActionEndFrame(timeline)
                        : 0;
            recipeEvent.StartFrame = shared
                ? 0
                : detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                    ? Math.Max(0, resolvedLocalFrame)
                    : Math.Max(0, detector.StartFrame);
            recipeEvent.DurationFrames = Math.Max(1, detector.DurationFrames);
            recipeEvent.CanMove = !shared
                && (detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                    || detector.LocalFrameAnchor ==
                        CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.AbsoluteFrame);
            recipeEvent.CanResize = !shared
                && detector.Trigger != CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame;
            recipeEvent.Primitive = detector.Trigger == CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame
                ? BehaviorRecipeEventPrimitive.Marker
                : detector.WindowEndMode == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed
                    ? BehaviorRecipeEventPrimitive.Span
                    : BehaviorRecipeEventPrimitive.OpenSpan;
            recipeEvent.ProgressFrames = detector.RuntimeElapsedFrames;
            recipeEvent.Running = detector.Active || detector.Armed || detector.Triggered;
            recipeEvent.Selected = IsSelected(source, recipeEvent, detector.Selected);
            CollectTransitions(recipeEvent, detector, transitions, transitionCount);
            CharacterTestRuntimeAiLabLogicTransitionViewModel effective =
                recipeEvent.EffectiveTransitionIndex >= 0
                    ? transitions[recipeEvent.EffectiveTransitionIndex]
                    : null;
            string condition = ResolveTriggerLabel(detector, resolvedLocalFrame);
            string result = ResolveBranchResultLabel(effective);
            recipeEvent.ConditionLabel = condition;
            recipeEvent.ResultLabel = result;
            recipeEvent.Title = condition + " → " + result
                + (recipeEvent.AdditionalResultCount > 0
                    ? "  +" + recipeEvent.AdditionalResultCount
                    : string.Empty);
            recipeEvent.Detail = shared
                ? "共享 · 整卡计时 +" + Math.Max(0, detector.StartFrame) + "f"
                : ResolveDetectorWindowText(detector);
            recipeEvent.Invalid = effective == null || HasInvalidTransition(recipeEvent, transitions);
            MaximumLocalFrame = Math.Max(
                MaximumLocalFrame,
                recipeEvent.StartFrame + Math.Max(1, recipeEvent.DurationFrames));
            track.Event = recipeEvent;
            timeline.AddTrack(track);
        }

        private static int CountBranchTracks(BehaviorRecipeTimelineViewModel timeline)
        {
            int count = 0;
            for (int i = 0; i < timeline.TrackCount; i++)
            {
                BehaviorRecipeTrackKind kind = timeline.GetTrack(i).Kind;
                if (kind is BehaviorRecipeTrackKind.Branch
                    or BehaviorRecipeTrackKind.LegacySharedBranch)
                    count++;
            }
            return count;
        }

        private static string ResolveBranchResultLabel(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition)
        {
            if (transition == null)
                return "未配置结果";
            if (transition.EndsCard)
                return "显式退出行为卡";
            return string.IsNullOrWhiteSpace(transition.TargetText)
                ? "未配置结果"
                : transition.TargetText.Trim();
        }

        private static int ResolvePrimaryActionEndFrame(BehaviorRecipeTimelineViewModel timeline)
        {
            for (int i = 0; timeline != null && i < timeline.TrackCount; i++)
            {
                BehaviorRecipeTrackViewModel track = timeline.GetTrack(i);
                if (track?.Kind == BehaviorRecipeTrackKind.PrimaryAction)
                    return Math.Max(0, track.Event?.DurationFrames ?? 0);
            }
            return 0;
        }

        private static string ResolveTriggerLabel(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int resolvedLocalFrame)
        {
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame:
                    return resolvedLocalFrame > 0
                        ? resolvedLocalFrame.ToString(CultureInfo.InvariantCulture) + "f"
                        : "主行为结束帧（待解析）";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return detector.LegacyUnfilteredSignal
                        ? "兼容事件：动作实例完成"
                        : "动作实例完成";
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return FormatActionSignal("动作实例失败", detector);
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected:
                    return FormatActionSignal("动作实例被拒绝", detector);
                default:
                    return FormatPredicate(detector);
            }
        }

        private static string FormatActionSignal(
            string label,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (!string.IsNullOrWhiteSpace(detector.ActionSignalReasonCode))
                return label + "（" + detector.ActionSignalReasonCode.Trim() + "）";
            return detector.LegacyUnfilteredSignal ? "兼容事件：" + label : label + "（未配置原因）";
        }

        private static string FormatPredicate(CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector.ConditionCount <= 0)
                return "未配置具体条件";
            if (detector.ConditionCount == 1)
                return FormatPredicateCondition(detector.Conditions[0], detector.ExpectedResult);

            var builder = new StringBuilder(128);
            if (!detector.ExpectedResult)
                builder.Append("任一条件不满足：");
            for (int i = 0; i < detector.ConditionCount; i++)
            {
                if (i > 0)
                    builder.Append(detector.ExpectedResult ? " 且 " : " / ");
                builder.Append(FormatPredicateCondition(detector.Conditions[i], expected: true));
            }
            return builder.ToString();
        }

        private static string FormatPredicateCondition(
            CharacterTestRuntimeAiLabLogicConditionViewModel condition,
            bool expected)
        {
            if (condition == null)
                return "无效条件";
            string fact = FormatPredicateFact(condition.Fact);
            if (condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                && condition.Fact != CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames)
            {
                bool value = condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals
                    ? !condition.BoolValue
                    : condition.BoolValue;
                if (!expected)
                    value = !value;
                return fact + " = " + (value ? "是" : "否");
            }

            string valueText = condition.Value.ToString("0.##", CultureInfo.InvariantCulture);
            if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
                valueText += " 米";
            string op = FormatPredicateOperator(condition.Operator, expected);
            if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between)
            {
                string maximum = condition.MaxValue.ToString("0.##", CultureInfo.InvariantCulture);
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    || condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance)
                    maximum += " 米";
                return expected
                    ? fact + " ∈ [" + valueText + ", " + maximum + "]"
                    : fact + " 不在 [" + valueText + ", " + maximum + "]";
            }
            return fact + " " + op + " " + valueText;
        }

        private static string FormatPredicateFact(CharacterTestRuntimeAiLabCardConditionFact fact)
        {
            return fact switch
            {
                CharacterTestRuntimeAiLabCardConditionFact.TargetExists => "目标存在",
                CharacterTestRuntimeAiLabCardConditionFact.TargetVisible => "目标可见",
                CharacterTestRuntimeAiLabCardConditionFact.TargetDead => "目标死亡",
                CharacterTestRuntimeAiLabCardConditionFact.TargetDistance => "目标 Hurtbox 表面距离",
                CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance => "目标根节点平面距离",
                CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable => "可用移动路径",
                CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable => "能力可用",
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning => "自身动作运行中",
                CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible => "自身动作可打断",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf => "目标攻击将命中自身",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames => "预计命中剩余帧",
                CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded => "本次攻击已响应",
                CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady => "闪避冷却就绪",
                CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange => "目标在攻击范围内",
                CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked => "目标视线受阻",
                CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard => "附近存在危险",
                _ => "自身可切换至动作"
            };
        }

        private static string FormatPredicateOperator(
            CharacterTestRuntimeAiLabCardConditionOperator value,
            bool expected)
        {
            if (expected)
            {
                return value switch
                {
                    CharacterTestRuntimeAiLabCardConditionOperator.Equals => "=",
                    CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => "≠",
                    CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => "≤",
                    CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => "≥",
                    CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => ">",
                    _ => "∈"
                };
            }
            return value switch
            {
                CharacterTestRuntimeAiLabCardConditionOperator.Equals => "≠",
                CharacterTestRuntimeAiLabCardConditionOperator.NotEquals => "=",
                CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual => ">",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual => "<",
                CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan => "≤",
                _ => "不在"
            };
        }

        private static void ResolvePrimaryActionGeometry(
            CharacterTestRuntimeAiLabLogicStageViewModel stage,
            BehaviorRecipeEventViewModel recipeEvent)
        {
            recipeEvent.CanMove = false;
            recipeEvent.CanResize = false;
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    recipeEvent.Primitive = BehaviorRecipeEventPrimitive.OpenSpan;
                    recipeEvent.DurationFrames = Math.Max(60, stage.DurationFrames);
                    return;
                case CharacterTestRuntimeAiLabCardStepKind.Wait:
                    recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Span;
                    recipeEvent.DurationFrames = Math.Max(1, stage.WaitFrames);
                    recipeEvent.CanResize = true;
                    return;
                case CharacterTestRuntimeAiLabCardStepKind.Logic:
                    recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Marker;
                    recipeEvent.DurationFrames = 1;
                    return;
                default:
                    recipeEvent.Primitive = BehaviorRecipeEventPrimitive.Span;
                    recipeEvent.DurationFrames = stage.ActualDurationFrames > 0
                        ? stage.ActualDurationFrames
                        : stage.ActionDurationFrames > 0
                            ? stage.ActionDurationFrames
                            : Math.Max(1, stage.DurationFrames);
                    return;
            }
        }

        private static string ResolvePrimaryActionTitle(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (!string.IsNullOrWhiteSpace(stage.ActionText))
                return stage.ActionText.Trim();
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget: return "移动至目标";
                case CharacterTestRuntimeAiLabCardStepKind.Wait: return "等待 " + Math.Max(1, stage.WaitFrames) + "f";
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility: return "使用能力";
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction: return "角色动作";
                case CharacterTestRuntimeAiLabCardStepKind.Logic: return "未配置行为 / 纯逻辑";
                default: return "兼容行为";
            }
        }

        private static string ResolveDetectorWindowText(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            string end = detector.WindowEndMode switch
            {
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed =>
                    "+" + Math.Max(1, detector.DurationFrames) + "f",
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd => "至阶段结束",
                CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.UntilEvent => "直到事件",
                _ => "∞"
            };
            return "阶段 +" + Math.Max(0, detector.StartFrame) + "f · " + end;
        }

    }
}
