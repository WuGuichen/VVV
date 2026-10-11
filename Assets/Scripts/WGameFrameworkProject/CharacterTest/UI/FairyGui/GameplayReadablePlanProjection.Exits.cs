using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class GameplayReadablePlanProjection
    {
        private void AddExitGroup(
            int sourceStageIndex,
            int sourceDetectorIndex,
            bool cardScope,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount,
            int orphanTransitionIndex = -1)
        {
            GameplayReadablePlanExitGroupProjection group = GetOrCreateGroup(_groupCount++);
            group.SourceStageIndex = sourceStageIndex;
            group.SourceDetectorIndex = sourceDetectorIndex;
            group.FirstExitIndex = _exitCount;
            group.ExitCount = 0;
            group.CardScope = cardScope;
            CharacterTestRuntimeAiLabLogicDetectorViewModel groupDetector =
                sourceDetectorIndex >= 0 && sourceDetectorIndex < detectorCount
                    ? detectors[sourceDetectorIndex]
                    : null;
            if (cardScope && groupDetector != null)
            {
                group.WindowStartFrame = groupDetector.StartFrame;
                group.WindowDurationFrames = Math.Max(1, groupDetector.DurationFrames);
                group.AnchorFrame = group.WindowStartFrame + group.WindowDurationFrames;
                group.StableKey = "全局:" + groupDetector.DetectorId;
                group.ListeningLabel = "全局 · " + BuildDetectorFactText(groupDetector);
                group.TooltipText = group.ListeningLabel
                    + "\n" + BuildWindowText(groupDetector, true)
                    + "\n检测标识：" + groupDetector.DetectorId;
            }
            else
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage =
                    sourceStageIndex >= 0 && sourceStageIndex < stageCount
                        ? stages[sourceStageIndex]
                        : null;
                GameplayReadablePlanStageProjection projectedStage =
                    FindStageBySourceIndex(sourceStageIndex);
                group.WindowStartFrame = projectedStage?.StartFrame ?? 0;
                group.WindowDurationFrames = Math.Max(1, projectedStage?.DurationFrames ?? 1);
                group.AnchorFrame = group.WindowStartFrame + group.WindowDurationFrames;
                group.StableKey = stage == null ? "无来源阶段" : stage.StageId;
                group.ListeningLabel = stage == null
                    ? "来源缺失"
                    : CharacterTestRuntimeAiPlannerDevPresentationText
                        .BuildBehaviorStageLabel(stage.Title, stage.Kind);
                group.TooltipText = stage == null
                    ? "出口缺少有效来源阶段"
                    : "“" + CharacterTestRuntimeAiPlannerDevPresentationText
                        .BuildBehaviorStageLabel(stage.Title, stage.Kind) + "”的互斥出口";
            }
            group.AnchorOutsideViewport = group.AnchorFrame < PresentationStartFrame
                || group.AnchorFrame > PresentationEndFrame;

            for (int toneValue = (int)GameplayReadablePlanExitTone.Success;
                 toneValue <= (int)GameplayReadablePlanExitTone.Invalid;
                 toneValue++)
            {
                GameplayReadablePlanExitTone tone = (GameplayReadablePlanExitTone)toneValue;
                for (int transitionIndex = 0; transitionIndex < transitionCount; transitionIndex++)
                {
                    if (_projectedTransitions[transitionIndex])
                        continue;
                    CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[transitionIndex];
                    if (transition == null
                        || orphanTransitionIndex >= 0 && transitionIndex != orphanTransitionIndex
                        || orphanTransitionIndex < 0
                            && !TransitionBelongsToGroup(
                                transition,
                                sourceStageIndex,
                                sourceDetectorIndex,
                                cardScope,
                                detectors,
                                detectorCount))
                    {
                        continue;
                    }
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                        transition.DetectorIndex >= 0 && transition.DetectorIndex < detectorCount
                            ? detectors[transition.DetectorIndex]
                            : null;
                    GameplayReadablePlanExitTone transitionTone = ResolveTone(transition, detector);
                    if (transitionTone != tone)
                        continue;
                    AddTransitionExit(
                        transitionIndex,
                        transition,
                        detector,
                        stages,
                        stageCount);
                    _projectedTransitions[transitionIndex] = true;
                    group.ExitCount++;
                }
            }

            if (orphanTransitionIndex < 0)
            {
                for (int detectorIndex = 0; detectorIndex < detectorCount; detectorIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[detectorIndex];
                    if (detector == null
                        || detector.LinkedTransitionCount > 0
                        || !DetectorBelongsToGroup(
                            detector,
                            detectorIndex,
                            sourceStageIndex,
                            sourceDetectorIndex,
                            cardScope))
                    {
                        continue;
                    }
                    AddDetectorPlaceholder(detectorIndex, detector, sourceStageIndex);
                    group.ExitCount++;
                }
            }
        }

        private void AddTransitionExit(
            int transitionIndex,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            GameplayReadablePlanExitProjection exit = GetOrCreateExit(_exitCount++);
            exit.SourceTransitionIndex = transitionIndex;
            exit.SourceDetectorIndex = transition.DetectorIndex;
            exit.SourceStageIndex = transition.SourceStageIndex;
            exit.TargetStageIndex = transition.TargetStageIndex;
            exit.ElementKind = CharacterTestRuntimeAiLabLogicElementKind.Transition;
            exit.Tone = ResolveTone(transition, detector);
            exit.StableId = transition.TransitionId ?? string.Empty;
            exit.DetectorStableId = detector?.DetectorId ?? string.Empty;
            exit.CardScope = detector?.CardScope == true;
            exit.WindowStartFrame = ResolveProjectedDetectorStart(detector);
            exit.WindowDurationFrames = Math.Max(1, detector?.DurationFrames ?? 1);
            exit.FactText = BuildExitFactText(exit.Tone, detector, transition, stages, stageCount);
            exit.TargetText = BuildTargetText(exit.Tone, transition, stages, stageCount);
            string prefix = exit.CardScope ? "全局 · " : string.Empty;
            string fullLabel = prefix + GetTonePrefix(exit.Tone) + exit.FactText
                + " → " + exit.TargetText;
            string compactTargetText = BuildCompactTargetText(
                transition,
                stages,
                stageCount,
                exit.TargetText);
            exit.Label = BuildCompactExitLabel(
                exit.Tone,
                detector,
                exit.FactText,
                compactTargetText,
                exit.CardScope);
            CacheExitRuntimeLabels(exit, compactTargetText);
            exit.WindowText = detector == null
                ? "监听窗口缺失"
                : BuildWindowText(detector, exit.CardScope);
            exit.TooltipText = fullLabel
                + "\n" + exit.WindowText
                + "\n出口标识：" + exit.StableId
                + (string.IsNullOrEmpty(exit.DetectorStableId)
                    ? string.Empty
                    : "\n检测标识：" + exit.DetectorStableId)
                + "\n策略：" + transition.Policy;
            exit.Invalid = exit.Tone == GameplayReadablePlanExitTone.Invalid;
        }

        private static string BuildCompactExitLabel(
            GameplayReadablePlanExitTone tone,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            string factText,
            string targetText,
            bool cardScope)
        {
            string prefix = cardScope ? "全局 · " : string.Empty;
            string detectorLabel = ResolveCompactDetectorLabel(tone, detector, factText);
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return prefix + detectorLabel + " → " + CompactTargetText(targetText);
                case GameplayReadablePlanExitTone.Failure:
                    return prefix + detectorLabel + " → " + CompactTargetText(targetText);
                case GameplayReadablePlanExitTone.Condition:
                    return prefix + detectorLabel
                        + " → " + CompactTargetText(targetText);
                default:
                    return prefix + "无效出口";
            }
        }

        private static string ResolveCompactDetectorLabel(
            GameplayReadablePlanExitTone tone,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            string factText)
        {
            string authoredLabel = CharacterTestRuntimeAiPlannerDevPresentationText
                .BuildBehaviorDetectorLabel(detector?.Title, detector?.CardScope == true);
            if (ContainsChineseText(authoredLabel)
                && !string.Equals(authoredLabel, "阶段条件", StringComparison.Ordinal)
                && !string.Equals(authoredLabel, "卡级条件", StringComparison.Ordinal)
                && !string.Equals(authoredLabel, "完成条件", StringComparison.Ordinal)
                && !string.Equals(authoredLabel, "失败条件", StringComparison.Ordinal))
            {
                return authoredLabel;
            }

            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return "完成";
                case GameplayReadablePlanExitTone.Failure:
                    return "失败";
                default:
                    return string.IsNullOrWhiteSpace(factText) ? "条件满足" : factText;
            }
        }

        private static bool ContainsChineseText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (int index = 0; index < text.Length; index++)
            {
                char value = text[index];
                if (value >= '\u3400' && value <= '\u9FFF')
                    return true;
            }
            return false;
        }

        private static void CacheExitRuntimeLabels(
            GameplayReadablePlanExitProjection exit,
            string compactTargetText)
        {
            exit.ListeningLabel = exit.Label;
            exit.TriggeredLabel = "已触发 · " + exit.Label;
            exit.CommittedLabel = "已进入 · " + compactTargetText;
        }

        private static string CompactTargetText(string targetText)
        {
            if (string.Equals(targetText, "正常结束", StringComparison.Ordinal)
                || string.Equals(targetText, "失败结束", StringComparison.Ordinal)
                || string.Equals(targetText, "结束行为卡", StringComparison.Ordinal))
            {
                return "重新决策";
            }
            if (!string.IsNullOrEmpty(targetText)
                && targetText.StartsWith("进入“", StringComparison.Ordinal)
                && targetText.EndsWith("”", StringComparison.Ordinal))
            {
                return targetText.Substring(3, targetText.Length - 4);
            }
            return targetText ?? string.Empty;
        }

        private void AddDetectorPlaceholder(
            int detectorIndex,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int sourceStageIndex)
        {
            GameplayReadablePlanExitProjection exit = GetOrCreateExit(_exitCount++);
            exit.SourceTransitionIndex = -1;
            exit.SourceDetectorIndex = detectorIndex;
            exit.SourceStageIndex = sourceStageIndex;
            exit.TargetStageIndex = -1;
            exit.ElementKind = CharacterTestRuntimeAiLabLogicElementKind.Detector;
            exit.Tone = GameplayReadablePlanExitTone.Invalid;
            exit.StableId = detector.DetectorId ?? string.Empty;
            exit.DetectorStableId = exit.StableId;
            exit.CardScope = detector.CardScope;
            exit.WindowStartFrame = detector.CardScope
                ? detector.StartFrame
                : ResolveProjectedDetectorStart(detector);
            exit.WindowDurationFrames = Math.Max(1, detector.DurationFrames);
            exit.FactText = BuildDetectorFactText(detector);
            exit.TargetText = "尚未配置目标";
            exit.WindowText = BuildWindowText(detector, detector.CardScope);
            exit.Label = (detector.CardScope ? "全局 · " : string.Empty)
                + "配置无效：监听条件没有出口";
            exit.ListeningLabel = exit.Label;
            exit.TriggeredLabel = exit.Label;
            exit.CommittedLabel = exit.Label;
            exit.TooltipText = exit.Label
                + "\n条件：" + exit.FactText
                + "\n" + exit.WindowText
                + "\n检测标识：" + exit.StableId;
            exit.Invalid = true;
        }

        private static bool HasStageExitGroup(
            int stageIndex,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            for (int i = 0; i < detectorCount; i++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[i];
                if (detector != null && !detector.CardScope && detector.StageIndex == stageIndex)
                    return true;
            }
            for (int i = 0; i < transitionCount; i++)
            {
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition = transitions[i];
                if (transition == null || transition.SourceStageIndex != stageIndex)
                    continue;
                if (transition.DetectorIndex < 0 || transition.DetectorIndex >= detectorCount)
                    return true;
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[transition.DetectorIndex];
                if (detector == null || !detector.CardScope)
                    return true;
            }
            return false;
        }

        private static bool TransitionBelongsToGroup(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            int sourceStageIndex,
            int sourceDetectorIndex,
            bool cardScope,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                transition.DetectorIndex >= 0 && transition.DetectorIndex < detectorCount
                    ? detectors[transition.DetectorIndex]
                    : null;
            if (cardScope)
                return transition.DetectorIndex == sourceDetectorIndex;
            if (detector?.CardScope == true)
                return false;
            if (detector != null && detector.StageIndex >= 0)
                return detector.StageIndex == sourceStageIndex;
            return transition.SourceStageIndex == sourceStageIndex;
        }

        private static bool DetectorBelongsToGroup(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            int detectorIndex,
            int sourceStageIndex,
            int sourceDetectorIndex,
            bool cardScope)
        {
            return cardScope
                ? detectorIndex == sourceDetectorIndex
                : !detector.CardScope && detector.StageIndex == sourceStageIndex;
        }

        private static GameplayReadablePlanExitTone ResolveTone(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (transition == null || transition.Invalid || detector == null)
                return GameplayReadablePlanExitTone.Invalid;
            switch (detector.Trigger)
            {
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted:
                    return GameplayReadablePlanExitTone.Success;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed:
                    return GameplayReadablePlanExitTone.Failure;
                case CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge:
                    return GameplayReadablePlanExitTone.Condition;
                default:
                    return GameplayReadablePlanExitTone.Invalid;
            }
        }

        private static string BuildExitFactText(
            GameplayReadablePlanExitTone tone,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel source =
                transition != null && transition.SourceStageIndex >= 0
                    && transition.SourceStageIndex < stageCount
                        ? stages[transition.SourceStageIndex]
                        : null;
            bool targeting = source != null
                && (string.Equals(source.Title, "Targeting.NearestEnemy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(source.Title, "NearestEnemy", StringComparison.OrdinalIgnoreCase));
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return targeting ? "已锁定有效目标" : "主动作已完成";
                case GameplayReadablePlanExitTone.Failure:
                    return targeting ? "没有有效目标或写入失败" : "主动作执行失败";
                case GameplayReadablePlanExitTone.Condition:
                    return BuildDetectorFactText(detector);
                default:
                    return detector == null ? "缺少监听条件" : "出口配置不可用";
            }
        }

        private string BuildTargetText(
            GameplayReadablePlanExitTone tone,
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            if (transition == null)
                return "目标阶段缺失";
            if (transition.EndsCard)
            {
                if (tone == GameplayReadablePlanExitTone.Success)
                    return "正常结束";
                if (tone == GameplayReadablePlanExitTone.Failure)
                    return "失败结束";
                return "结束行为卡";
            }
            if (transition.TargetStageIndex < 0 || transition.TargetStageIndex >= stageCount
                || stages[transition.TargetStageIndex] == null)
            {
                return "目标阶段缺失";
            }
            string targetLabel = CharacterTestRuntimeAiPlannerDevPresentationText.BuildBehaviorStageLabel(
                stages[transition.TargetStageIndex].Title,
                stages[transition.TargetStageIndex].Kind);
            if (transition.TargetStageIndex == transition.SourceStageIndex)
                return "继续“" + targetLabel + "” ↻";
            if (transition.IsBackEdge)
                return "回到“" + targetLabel + "”";
            return "进入“" + targetLabel + "”";
        }

        private static string BuildCompactTargetText(
            CharacterTestRuntimeAiLabLogicTransitionViewModel transition,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string fallback)
        {
            if (transition == null || transition.EndsCard
                || transition.TargetStageIndex < 0
                || transition.TargetStageIndex >= stageCount
                || stages[transition.TargetStageIndex] == null)
            {
                return CompactTargetText(fallback);
            }

            CharacterTestRuntimeAiLabLogicStageViewModel targetStage =
                stages[transition.TargetStageIndex];
            string actionLabel = CharacterTestRuntimeAiPlannerDevPresentationText
                .BuildBehaviorGameplayActionLabel(targetStage);
            if (transition.TargetStageIndex == transition.SourceStageIndex)
                return "继续“" + actionLabel + "” ↻";
            if (transition.IsBackEdge)
                return "回到“" + actionLabel + "”";
            return actionLabel;
        }

        private static string BuildDetectorFactText(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector == null)
                return "条件缺失";
            int count = Math.Min(detector.ConditionCount, detector.Conditions?.Length ?? 0);
            if (count <= 0)
            {
                return CharacterTestRuntimeAiPlannerDevPresentationText.BuildBehaviorDetectorLabel(
                    detector.Title,
                    detector.CardScope);
            }
            if (count == 1)
                return BuildConditionText(detector.Conditions[0]);
            var builder = new StringBuilder(96);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(" 且 ");
                builder.Append(BuildConditionText(detector.Conditions[i]));
            }
            return builder.ToString();
        }

        private static string BuildConditionText(
            CharacterTestRuntimeAiLabLogicConditionViewModel condition)
        {
            if (condition == null)
                return "条件缺失";
            switch (condition.Fact)
            {
                case CharacterTestRuntimeAiLabCardConditionFact.TargetExists:
                    return BuildBooleanCondition("存在有效目标", "没有有效目标", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetVisible:
                    return BuildBooleanCondition("目标可见", "目标不可见", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDead:
                    return BuildBooleanCondition("目标死亡", "目标存活", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistance:
                    return "锁定受击框净空" + BuildNumericOperator(condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetDistanceAvailable:
                    return BuildBooleanCondition("受击框距离可用", "受击框距离不可用", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetRootDistance:
                    return "目标根距离" + BuildNumericOperator(condition);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionRunning:
                    return BuildBooleanCondition("自身动作正在执行", "自身动作已结束", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfActionInterruptible:
                    return BuildBooleanCondition("自身动作可打断", "自身动作不可打断", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction:
                    return BuildBooleanCondition("允许切换角色动作", "暂不能切换角色动作", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable:
                    return BuildBooleanCondition("能力可用", "能力不可用", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable:
                    return BuildBooleanCondition("存在可用移动路径", "没有可用移动路径", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf:
                    return BuildBooleanCondition("敌方攻击将命中自身", "敌方攻击不会命中自身", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackImpactFrames:
                    return "敌方攻击命中倒计时" + BuildNumericOperator(condition) + "帧";
                case CharacterTestRuntimeAiLabCardConditionFact.TargetAttackInstanceResponded:
                    return BuildBooleanCondition("本次敌方攻击已响应", "本次敌方攻击尚未响应", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.SelfDodgeCooldownReady:
                    return BuildBooleanCondition("闪避已就绪", "闪避尚未就绪", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetInAttackRange:
                    return BuildBooleanCondition("目标已进入攻击范围", "目标仍在攻击范围外", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.TargetLineOfSightBlocked:
                    return BuildBooleanCondition("目标视线受阻", "目标视线畅通", condition);
                case CharacterTestRuntimeAiLabCardConditionFact.EnvironmentNearHazard:
                    return BuildBooleanCondition("附近存在危险", "附近没有危险", condition);
                default:
                    return "存在尚未支持的条件事实";
            }
        }

        private static string BuildBooleanCondition(
            string trueText,
            string falseText,
            CharacterTestRuntimeAiLabLogicConditionViewModel condition)
        {
            bool expected = condition.BoolValue;
            if (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.NotEquals)
                expected = !expected;
            return expected ? trueText : falseText;
        }

        private static string BuildNumericOperator(
            CharacterTestRuntimeAiLabLogicConditionViewModel condition)
        {
            string value = condition.Value.ToString("0.##", CultureInfo.InvariantCulture);
            switch (condition.Operator)
            {
                case CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual:
                    return " ≤ " + value;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual:
                    return " ≥ " + value;
                case CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan:
                    return " > " + value;
                case CharacterTestRuntimeAiLabCardConditionOperator.NotEquals:
                    return " ≠ " + value;
                case CharacterTestRuntimeAiLabCardConditionOperator.Between:
                    return "在 " + value + " 至 "
                        + condition.MaxValue.ToString("0.##", CultureInfo.InvariantCulture) + " 之间";
                default:
                    return " = " + value;
            }
        }

        private static string BuildWindowText(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            bool cardScope)
        {
            int end = detector.StartFrame + Math.Max(1, detector.DurationFrames);
            return cardScope
                ? "全局监听 · 第 " + detector.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + "–" + end.ToString(CultureInfo.InvariantCulture) + " 帧"
                : "阶段内监听 · +" + detector.StartFrame.ToString(CultureInfo.InvariantCulture)
                    + " 至 +" + end.ToString(CultureInfo.InvariantCulture) + " 帧";
        }

        private static string GetTonePrefix(GameplayReadablePlanExitTone tone)
        {
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return "成功：";
                case GameplayReadablePlanExitTone.Failure:
                    return "失败：";
                case GameplayReadablePlanExitTone.Condition:
                    return "条件：";
                default:
                    return "配置无效：";
            }
        }

    }
}
