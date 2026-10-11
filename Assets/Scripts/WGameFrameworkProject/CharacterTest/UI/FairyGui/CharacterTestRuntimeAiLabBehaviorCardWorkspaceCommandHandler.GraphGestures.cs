using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        private CharacterTestRuntimeAiLabStageEditIdentity _transitionConnectIdentity;
        private long _transitionConnectEpoch;
        private string _transitionConnectDetectorId = string.Empty;

        internal bool BeginTransitionConnect(string detectorId, out long epoch)
        {
            epoch = 0L;
            if (!TryResolveElement(
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    detectorId,
                    out _)
                || !TryPrepareGraphMutation(out _, out CharacterTestRuntimeAiLabStageEditIdentity identity))
            {
                LastMessage = "无法开始 Transition 连线；Detector 或编辑身份已失效。";
                return false;
            }
            CancelTransitionConnect();
            _transitionConnectIdentity = identity;
            _transitionConnectDetectorId = detectorId;
            AdvanceTransitionConnectEpoch();
            epoch = _transitionConnectEpoch;
            LastMessage = "拖动到 Stage 输入端口或 End 端口；释放到空白将取消。";
            return true;
        }

        internal bool CommitTransitionConnect(
            long epoch,
            string detectorId,
            string targetStageId)
        {
            if (epoch == 0L
                || epoch != _transitionConnectEpoch
                || !string.Equals(detectorId, _transitionConnectDetectorId, StringComparison.Ordinal)
                || !TryPrepareGraphMutation(out int entryIndex, out CharacterTestRuntimeAiLabStageEditIdentity current)
                || !current.Matches(_transitionConnectIdentity))
            {
                LastMessage = "Transition 连线事件已过期；草稿未改变。";
                CancelTransitionConnect();
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline = _workspaceView?.Invoke()?.Timeline;
            if (!TryResolveElement(
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    detectorId,
                    out int detectorIndex)
                || detectorIndex < 0
                || detectorIndex >= (timeline?.DetectorCount ?? 0))
            {
                LastMessage = "Transition 来源 Detector 已失效；草稿未改变。";
                CancelTransitionConnect();
                return false;
            }
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector = timeline.Detectors[detectorIndex];
            string sourceStageId = detector.StageIndex >= 0
                && detector.StageIndex < timeline.StageCount
                    ? timeline.Stages[detector.StageIndex].StageId
                    : string.Empty;
            bool endsCard = string.Equals(
                targetStageId,
                CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                StringComparison.Ordinal);
            if (!endsCard
                && !TryResolveElement(
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    targetStageId,
                    out _))
            {
                LastMessage = "Transition 目标端口已失效；草稿未改变。";
                CancelTransitionConnect();
                return false;
            }
            int transitionIndex = timeline.TransitionCount;
            if (!_controller.TryConnectLogicTransition(
                    entryIndex,
                    current,
                    detectorId,
                    sourceStageId,
                    targetStageId,
                    out string transitionId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                CancelTransitionConnect();
                return false;
            }
            for (int i = 0; i < timeline.TransitionCount; i++)
            {
                if (string.Equals(
                        timeline.Transitions[i]?.TransitionId,
                        transitionId,
                        StringComparison.Ordinal))
                {
                    transitionIndex = i;
                    break;
                }
            }
            _setSelection?.Invoke(CharacterTestRuntimeAiLabLogicElementKind.Transition, transitionIndex);
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Transition,
                transitionId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            CancelTransitionConnect();
            _refresh?.Invoke();
            return true;
        }

        internal void CancelTransitionConnect()
        {
            _transitionConnectIdentity = default;
            _transitionConnectDetectorId = string.Empty;
            if (_transitionConnectEpoch != 0L)
                AdvanceTransitionConnectEpoch();
        }

        private void AdvanceTransitionConnectEpoch()
        {
            _transitionConnectEpoch = _transitionConnectEpoch == long.MaxValue
                ? 1L
                : _transitionConnectEpoch + 1L;
        }
    }
}
