using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class GameplayReadablePlanProjection
    {
        private int ResolveProjectedDetectorStart(
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            if (detector == null)
                return 0;
            int start = detector.StartFrame;
            if (!detector.CardScope)
            {
                GameplayReadablePlanStageProjection stage =
                    FindStageBySourceIndex(detector.StageIndex);
                if (stage != null)
                    start += stage.StartFrame;
            }
            return start;
        }

        private GameplayReadablePlanStageProjection FindStageBySourceIndex(int sourceIndex)
        {
            for (int i = 0; i < _stageCount; i++)
            {
                GameplayReadablePlanStageProjection stage = _stagePool[i];
                if (stage.SourceIndex == sourceIndex)
                    return stage;
            }
            return null;
        }

        private void RefineWaitActionLabels(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount)
        {
            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[stageIndex];
                if (stage == null
                    || stage.Kind != CharacterTestRuntimeAiLabCardStepKind.Logic
                    || !string.Equals(stage.Title, "Wait", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool waitsForActionTransition = false;
                for (int detectorIndex = 0; detectorIndex < detectorCount; detectorIndex++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[detectorIndex];
                    if (detector == null || detector.StageIndex != stageIndex)
                        continue;
                    CharacterTestRuntimeAiLabLogicConditionViewModel[] conditions =
                        detector.Conditions ?? Array.Empty<CharacterTestRuntimeAiLabLogicConditionViewModel>();
                    int conditionCount = Math.Min(detector.ConditionCount, conditions.Length);
                    for (int conditionIndex = 0; conditionIndex < conditionCount; conditionIndex++)
                    {
                        if (conditions[conditionIndex]?.Fact
                            == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction)
                        {
                            waitsForActionTransition = true;
                            break;
                        }
                    }
                    if (waitsForActionTransition)
                        break;
                }

                if (!waitsForActionTransition)
                    continue;
                GameplayReadablePlanStageProjection projected = FindStageBySourceIndex(stageIndex);
                if (projected == null)
                    continue;
                projected.ActionLabel = "等待动作可切换";
                projected.RunningLabel = "正在等待动作可切换";
                projected.TooltipText = projected.StageLabel
                    + "\n玩法：" + projected.ActionLabel
                    + "\n" + projected.WindowText
                    + "\n阶段标识：" + projected.StableId
                    + "\n原始名称：" + (stage.Title ?? string.Empty);
            }
        }

        private static int ResolvePresentationStageDuration(
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            if (stage == null)
                return OpenStagePresentationFrames;
            return stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                ? Math.Max(1, stage.DurationFrames)
                : OpenStagePresentationFrames;
        }

        private static int FindNextPresentationStage(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            bool[] ordered)
        {
            int best = -1;
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel candidate = stages[i];
                if (candidate == null || ordered[i])
                    continue;
                if (best < 0)
                {
                    best = i;
                    continue;
                }
                CharacterTestRuntimeAiLabLogicStageViewModel current = stages[best];
                if (candidate.Column < current.Column
                    || candidate.Column == current.Column && candidate.Order < current.Order
                    || candidate.Column == current.Column && candidate.Order == current.Order && i < best)
                {
                    best = i;
                }
            }
            return best;
        }

        private static void CopyStageRuntime(
            GameplayReadablePlanStageProjection projected,
            CharacterTestRuntimeAiLabLogicStageViewModel stage)
        {
            projected.ActualDurationFrames = stage.ActualDurationFrames;
            projected.Selected = stage.Selected;
            projected.Current = stage.Current;
            projected.ActionRunning = stage.ActionRunning;
            projected.ActionConflict = stage.ActionConflict;
            projected.ActionProgressFrames = stage.ActionProgressFrames;
            projected.ActionInstanceId = stage.ActionInstanceId;
            projected.ActionDurationFrames = Math.Max(0, stage.ActionDurationFrames);
            CharacterTestRuntimeAiLabLogicActionState actionState =
                (CharacterTestRuntimeAiLabLogicActionState)stage.ActionRuntimeState;
            if (actionState == CharacterTestRuntimeAiLabLogicActionState.Completed)
            {
                projected.ActionProgressMode = GameplayReadablePlanProgressMode.Completed;
            }
            else if (actionState == CharacterTestRuntimeAiLabLogicActionState.Failed)
            {
                projected.ActionProgressMode = GameplayReadablePlanProgressMode.Failed;
            }
            else if (actionState != CharacterTestRuntimeAiLabLogicActionState.Running)
            {
                projected.ActionProgressMode = GameplayReadablePlanProgressMode.None;
            }
            else if (stage.ActionProgressIndeterminate || projected.ActionDurationFrames <= 0)
            {
                projected.ActionProgressMode = GameplayReadablePlanProgressMode.Indeterminate;
            }
            else
            {
                projected.ActionProgressMode = stage.ActionProgressOverrun
                    ? GameplayReadablePlanProgressMode.Overrun
                    : GameplayReadablePlanProgressMode.Determinate;
            }
        }

        /// <remarks>NoAlloc. Distance labels are prebuilt once and selected by decimeter.</remarks>
        private static void UpdateStageMetricRuntime(
            GameplayReadablePlanStageProjection projected,
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            if (projected.MetricKind != GameplayReadablePlanMetricKind.Distance)
                return;
            int decimeters = timeline?.ObservedTargetDistanceDecimeters ?? -1;
            projected.MetricCurrentText = decimeters < 0
                ? "距离 —"
                : DistanceCurrentLabels[Math.Min(decimeters, DistanceLabelLimitDecimeters)];
        }

        private static string[] CreateDistanceCurrentLabels()
        {
            var labels = new string[DistanceLabelLimitDecimeters + 1];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = "距离 "
                    + (i * 0.1f).ToString("0.0", CultureInfo.InvariantCulture)
                    + "m";
            }
            return labels;
        }

        private GameplayReadablePlanStageProjection GetOrCreateStage(int index)
        {
            while (_stagePool.Count <= index)
                _stagePool.Add(new GameplayReadablePlanStageProjection());
            return _stagePool[index];
        }

        private GameplayReadablePlanExitProjection GetOrCreateExit(int index)
        {
            while (_exitPool.Count <= index)
                _exitPool.Add(new GameplayReadablePlanExitProjection());
            GameplayReadablePlanExitProjection exit = _exitPool[index];
            exit.SourceTransitionIndex = -1;
            exit.SourceDetectorIndex = -1;
            exit.SourceStageIndex = -1;
            exit.TargetStageIndex = -1;
            exit.Selected = false;
            exit.DetectorActive = false;
            exit.DetectorTriggered = false;
            exit.Pending = false;
            exit.Committed = false;
            exit.Invalid = false;
            exit.ListeningLabel = string.Empty;
            exit.TriggeredLabel = string.Empty;
            exit.CommittedLabel = string.Empty;
            return exit;
        }

        private GameplayReadablePlanExitGroupProjection GetOrCreateGroup(int index)
        {
            while (_groupPool.Count <= index)
                _groupPool.Add(new GameplayReadablePlanExitGroupProjection());
            return _groupPool[index];
        }

        private void EnsureTransitionScratch(int count)
        {
            if (_projectedTransitions.Length >= count)
                return;
            Array.Resize(ref _projectedTransitions, Math.Max(count, _projectedTransitions.Length * 2 + 4));
        }

        private void EnsureStageScratch(int count)
        {
            if (_orderedStages.Length >= count)
                return;
            int capacity = Math.Max(count, _orderedStages.Length * 2 + 4);
            Array.Resize(ref _orderedStages, capacity);
            Array.Resize(ref _stagePresentationStarts, capacity);
        }
    }
}
