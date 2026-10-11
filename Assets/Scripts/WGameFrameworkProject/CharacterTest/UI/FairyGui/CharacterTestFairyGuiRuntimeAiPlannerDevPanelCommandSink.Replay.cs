using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private string ExecuteJumpFrame(object payload)
        {
            if (_controller == null)
                return "运行时智能规划开发控制器不可用。";

            int frame;
            if (payload is int intFrame)
            {
                frame = intFrame;
            }
            else if (payload is string text && int.TryParse(text, out int parsed))
            {
                frame = parsed;
            }
            else
            {
                return "跳转失败，请输入运行时帧编号。";
            }

            return _controller.JumpToFrame(frame)
                ? "已跳转到指定帧。"
                : "跳转失败。" + _controller.StatusMessage;
        }

        private string LocateBehaviorTransitionRuntimeFrame(object payload)
        {
            if (_controller == null
                || !(payload is CharacterTestRuntimeAiLabBehaviorTransitionNavigationCommand request)
                || !request.IsValid
                || !_controller.TryResolveBehaviorTransitionTimelineLocator(
                    request.TransitionId,
                    request.Frame,
                    out CharacterTestRuntimeAiLabTimelineItemLocator locator))
            {
                return "No exact RuntimeFrame transition event is available.";
            }

            OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
            if (!_controller.SelectTimelineItem(locator))
                return "The RuntimeFrame transition event became stale during navigation.";

            PublishRuntimeFrameContextIfNeeded();
            return _controller.JumpToFrame(request.Frame)
                ? "Opened RuntimeFrame and selected the exact transition event."
                : "Selected the transition event, but the target frame could not be restored.";
        }

        private string NavigateReplayTimelineItem(object payload)
        {
            if (_controller == null
                || !(payload is CharacterTestRuntimeAiLabTimelineNavigationCommand request)
                || !request.Locator.IsValid
                || !_controller.TryResolveReplayTimelineLocator(
                    request.Locator,
                    request.UseEndFrame,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator evidence))
            {
                return "Replay evidence locator is stale or has no live provenance mapping.";
            }
            _replayReturnToken = _controller.CaptureReplayReturnToken();
            OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
            bool selected = _controller.SelectTimelineItem(evidence.LiveItem);
            bool restored = _controller.JumpToFrame(evidence.LiveFrame, evidence.AgentEntityId);
            if (selected)
                PublishRuntimeFrameContextIfNeeded();
            return restored
                ? selected
                    ? "Opened the exact RuntimeFrame evidence."
                    : "Opened the RuntimeFrame; the typed item was no longer retained."
                : "Replay evidence frame could not be restored.";
        }

        private string LocateReplayDivergence()
        {
            string absenceCode = string.Empty;
            if (_controller == null
                || !_controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Divergence,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
                    out absenceCode))
            {
                return string.IsNullOrEmpty(absenceCode)
                    ? "Replay has no divergence frame."
                    : absenceCode;
            }
            _replayReturnToken = _controller.CaptureReplayReturnToken();
            OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame);
            return _controller.JumpToFrame(locator.LiveFrame, locator.AgentEntityId)
                ? "Opened the live RuntimeFrame proven by the first Replay divergence locator."
                : "The divergent RuntimeFrame is no longer retained.";
        }

        private string OpenReplayFactsEvidence()
        {
            string absenceCode = string.Empty;
            if (_controller == null
                || !_controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.Facts,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
                    out absenceCode))
            {
                return string.IsNullOrEmpty(absenceCode)
                    ? "Replay causal evidence is unavailable."
                    : absenceCode;
            }
            _replayReturnToken = _controller.CaptureReplayReturnToken();
            if (!_controller.TryActivateReplayFactsCausalSnapshot(locator, out absenceCode))
            {
                _replayReturnToken = default;
                return string.IsNullOrEmpty(absenceCode)
                    ? "Replay causal evidence is unavailable."
                    : absenceCode;
            }
            OpenDiagnosticsTab(CharacterTestRuntimeAiLabDiagnosticsTab.Facts);
            return "Opened immutable Facts, candidates, arbitration, and commit evidence from the frozen Replay record.";
        }

        private string OpenReplayBehaviorEvidence()
        {
            string absenceCode = string.Empty;
            if (_controller == null
                || !_controller.TryResolveReplayEvidenceLocator(
                    CharacterTestRuntimeAiLabReplayEvidenceTargetKind.BehaviorCard,
                    out CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
                    out absenceCode)
                || !_controller.TryResolveReplayBehaviorSelection(
                    locator,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabLogicElementKind kind,
                    out int elementIndex))
            {
                return string.IsNullOrEmpty(absenceCode)
                    ? "Replay behavior evidence has no resolvable card element."
                    : absenceCode;
            }
            _replayReturnToken = _controller.CaptureReplayReturnToken();
            SelectedWorkbenchEntryIndex = entryIndex;
            SelectedLogicElementKind = kind;
            SelectedLogicElementIndex = elementIndex;
            SelectedCompiledStepIndex = kind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                ? Math.Max(0, elementIndex)
                : 0;
            SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            return "Opened the exact Behavior Card element proven by frozen Replay evidence.";
        }

        private string ExecuteTimelineScrub(object payload, TimelineScrubPhase phase)
        {
            if (!TryResolveFrame(payload, out int frame))
                return "时间轴拖动目标帧无效。";

            switch (phase)
            {
                case TimelineScrubPhase.Begin:
                    _controller.BeginTimelineScrub(frame);
                    break;
                case TimelineScrubPhase.End:
                    _controller.EndTimelineScrub(frame);
                    break;
                default:
                    _controller.UpdateTimelineScrub(frame);
                    break;
            }
            // The controller owns the asynchronous seek result. Keeping its status
            // prevents a generic acknowledgement from hiding replay progress or a
            // non-blocking Frame Sync validation warning.
            return _controller.StatusMessage;
        }

        private static bool TryResolveFrame(object payload, out int frame)
        {
            if (payload is int value)
            {
                frame = value;
                return true;
            }
            return int.TryParse(payload?.ToString(), out frame);
        }

        private enum TimelineScrubPhase
        {
            Begin,
            Update,
            End
        }

    }
}
