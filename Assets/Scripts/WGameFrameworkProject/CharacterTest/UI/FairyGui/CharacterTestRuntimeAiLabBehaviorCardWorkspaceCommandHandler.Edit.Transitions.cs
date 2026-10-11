using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceEditCommandHandler
    {
        private void AddTransition(
            string detectorId,
            CharacterTestRuntimeAiLabTransitionResultPreset preset)
        {
            AddTransition(detectorId, preset, -1);
        }

        private void AddTransitionToStage(string detectorId, int targetStageIndex)
        {
            AddTransition(
                detectorId,
                CharacterTestRuntimeAiLabTransitionResultPreset.Automatic,
                targetStageIndex);
        }

        private void AddTransition(
            string detectorId,
            CharacterTestRuntimeAiLabTransitionResultPreset preset,
            int explicitTargetStageIndex)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out int detectorIndex))
            {
                LastMessage = "请先选择 Detector。";
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                detectorIndex >= 0 && detectorIndex < (timeline?.DetectorCount ?? 0)
                    ? timeline.Detectors[detectorIndex]
                    : null;
            string sourceStageId = detector != null
                && detector.StageIndex >= 0
                && detector.StageIndex < (timeline?.StageCount ?? 0)
                    ? timeline.Stages[detector.StageIndex].StageId
                    : string.Empty;
            int entryStageIndex = 0;
            for (int i = 0; i < (timeline?.StageCount ?? 0); i++)
            {
                if (timeline.Stages[i]?.Entry == true)
                {
                    entryStageIndex = i;
                    break;
                }
            }
            int targetIndex = detector?.StageIndex >= 0
                ? detector.StageIndex + 1
                : entryStageIndex;
            bool hasNextStage = targetIndex >= 0
                && targetIndex < (timeline?.StageCount ?? 0);
            string targetStageId;
            string labelOverride = null;
            string endReasonOverride = null;
            if (explicitTargetStageIndex >= 0)
            {
                if (explicitTargetStageIndex >= (timeline?.StageCount ?? 0)
                    || timeline.Stages[explicitTargetStageIndex] == null)
                {
                    LastMessage = "目标阶段已失效，请重新选择。";
                    return;
                }
                targetStageId = timeline.Stages[explicitTargetStageIndex].StageId;
                labelOverride = "进入“" + timeline.Stages[explicitTargetStageIndex].Title + "”";
                endReasonOverride = string.Empty;
            }
            else switch (preset)
            {
                case CharacterTestRuntimeAiLabTransitionResultPreset.NextStage:
                    if (!hasNextStage)
                    {
                        LastMessage = "当前阶段后面还没有阶段；请先添加阶段。";
                        return;
                    }
                    targetStageId = timeline.Stages[targetIndex].StageId;
                    labelOverride = "进入下一阶段";
                    endReasonOverride = string.Empty;
                    break;
                case CharacterTestRuntimeAiLabTransitionResultPreset.EndBehaviorCard:
                    targetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
                    labelOverride = "结束行为卡";
                    endReasonOverride = "Completed";
                    break;
                case CharacterTestRuntimeAiLabTransitionResultPreset.Replan:
                    targetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
                    labelOverride = "Replan";
                    endReasonOverride = "Replan";
                    break;
                default:
                    targetStageId = hasNextStage
                        ? timeline.Stages[targetIndex].StageId
                        : CharacterTestRuntimeAiLabLogicTransition.EndTarget;
                    break;
            }
            int transitionIndex = _workspaceView?.Invoke()?.Timeline?.TransitionCount ?? 0;
            if (!_controller.TryConnectLogicTransition(
                    entryIndex,
                    identity,
                    detectorId,
                    sourceStageId,
                    targetStageId,
                    labelOverride,
                    endReasonOverride,
                    out string createdTransitionId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            for (int i = 0; i < (timeline?.TransitionCount ?? 0); i++)
            {
                if (string.Equals(
                        timeline.Transitions[i]?.TransitionId,
                        createdTransitionId,
                        StringComparison.Ordinal))
                {
                    transitionIndex = i;
                    break;
                }
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Transition,
                createdTransitionId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void CycleDetectorFact(string detectorId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out int index)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            if (!_controller.TryCycleLogicDetectorFact(
                    entryIndex,
                    identity,
                    detectorId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, index);
            PublishGraphContext(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void CycleDetectorTrigger(string detectorId)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = ResolveDetector(detectorId);
            if (detector == null || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            CharacterTestRuntimeAiLabLogicDetectorTrigger next = detector.Trigger switch
            {
                CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge =>
                    CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame,
                CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame =>
                    CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted,
                CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted =>
                    CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed,
                CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed =>
                    CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionRejected,
                _ => CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge
            };
            CommitDetectorField(
                entryIndex,
                identity,
                detector,
                CharacterTestRuntimeAiLabDetectorFieldKind.Trigger,
                new CharacterTestRuntimeAiLabDetectorFieldValue(trigger: next));
        }

        private void CycleDetectorWindowMode(string detectorId)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = ResolveDetector(detectorId);
            if (detector == null || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            CharacterTestRuntimeAiLabLogicDetectorWindowEndMode next;
            switch (detector.WindowEndMode)
            {
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed:
                    next = detector.CardScope
                        ? CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
                        : CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd;
                    break;
                case CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd:
                    next = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite;
                    break;
                default:
                    next = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;
                    break;
            }
            CommitDetectorField(
                entryIndex,
                identity,
                detector,
                CharacterTestRuntimeAiLabDetectorFieldKind.WindowEndMode,
                new CharacterTestRuntimeAiLabDetectorFieldValue(
                    intValue: Math.Max(1, detector.DurationFrames),
                    windowEndMode: next));
        }

        private void CommitDetectorField(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value)
        {
            if (!_controller.TryEditLogicDetectorField(
                    entryIndex,
                    identity,
                    detector.DetectorId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, detector.Index);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detector.DetectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void MoveDetectorWindow(string detectorId, int direction)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out int index)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            int delta = direction < 0 ? -15 : 15;
            if (!_controller.TryMoveLogicDetectorWindow(
                    entryIndex,
                    identity,
                    detectorId,
                    delta,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, index);
            PublishGraphContext(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void ResizeDetectorWindow(string detectorId, int direction)
        {
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = ResolveDetector(detectorId);
            if (detector == null)
                return;
            if (detector.WindowEndMode != CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed)
            {
                LastMessage = "只有 Fixed Detector window 可以调整持续时间。";
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            int delta = direction < 0 ? -15 : 15;
            if (!_controller.TryResizeLogicDetectorWindow(
                    entryIndex,
                    identity,
                    detectorId,
                    delta,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, detector.Index);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private CharacterTestRuntimeAiLabLogicDetectorViewModel ResolveDetector(string detectorId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out int index))
            {
                LastMessage = "Detector 已失效，草稿保持不变。";
                return null;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            return index >= 0 && index < (timeline?.DetectorCount ?? 0)
                ? timeline.Detectors[index]
                : null;
        }

        private void CycleTransitionPolicy(string transitionId)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionId, out int index)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            if (!_controller.TryCycleLogicTransitionPolicy(
                    entryIndex,
                    identity,
                    transitionId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Transition, index);
            PublishGraphContext(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionId, resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void CycleTransitionTarget(string transitionId, int direction)
        {
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionId, out int index)
                || !TryPrepareGraphMutation(out int entryIndex, out var identity))
                return;
            if (!_controller.TryCycleLogicTransitionTarget(
                    entryIndex,
                    identity,
                    transitionId,
                    direction,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Transition, index);
            PublishGraphContext(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionId, resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void MutateDetector(string stableId, ElementMutation mutation)
        {
            MutateElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, stableId, mutation);
        }

        private void MutateTransition(string stableId, ElementMutation mutation)
        {
            MutateElement(CharacterTestRuntimeAiLabLogicElementKind.Transition, stableId, mutation);
        }

        private void MutateElement(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            string stableId,
            ElementMutation mutation)
        {
            if (!TryResolveElement(kind, stableId, out int index))
            {
                LastMessage = "时间轴元素已失效，未修改草稿。";
                return;
            }
            if (!CanMutate(out int entryIndex))
                return;
            _setSelection?.Invoke(kind, index);
            if (!mutation(entryIndex, index, out string diagnostic))
            {
                LastMessage = diagnostic;
                return;
            }
            ClearPendingPriority();
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

        private void AdjustDetectorPriority(string detectorId, int direction)
        {
            direction = direction < 0 ? -1 : 1;
            if (!TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, detectorId, out int index))
            {
                LastMessage = "Detector 已失效，未修改优先级。";
                ClearPendingPriority();
                return;
            }
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
            {
                ClearPendingPriority();
                return;
            }
            if (!string.Equals(_pendingPriorityDetectorId, detectorId, StringComparison.Ordinal)
                || _pendingPriorityDirection != direction
                || !_pendingPriorityIdentity.Matches(identity))
            {
                _pendingPriorityDetectorId = detectorId;
                _pendingPriorityDirection = direction;
                _pendingPriorityIdentity = identity;
                _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, index);
                LastMessage = direction < 0
                    ? "再次点击确认提高 Detector 优先级；这会改变行为语义。"
                    : "再次点击确认降低 Detector 优先级；这会改变行为语义。";
                _refresh?.Invoke();
                return;
            }
            if (!_controller.TryNudgeLogicDetectorPriority(
                    entryIndex,
                    identity,
                    detectorId,
                    direction,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                ClearPendingPriority();
                return;
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Detector, index);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            ClearPendingPriority();
            LastMessage = diagnostic;
            _refresh?.Invoke();
        }

    }
}
