using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceEditCommandHandler
    {
        private void RemoveElement(CharacterTestRuntimeAiLabBehaviorCardElementCommand element)
        {
            CharacterTestRuntimeAiLabLogicElementKind kind = element.Kind;
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.None)
                kind = ResolveElementKind(element.StableId);
            if (!TryResolveElement(kind, element.StableId, out int index))
            {
                LastMessage = "待删除元素已失效，草稿保持不变。";
                return;
            }
            if (!CanMutate(out int entryIndex))
                return;
            bool removed;
            string diagnostic;
            string resultingCardDraftId = string.Empty;
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Stage:
                    removed = _controller.TryRemoveLogicStage(entryIndex, index, out diagnostic);
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    if (!TryPrepareGraphMutation(out entryIndex, out var detectorIdentity))
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                            "command",
                            "delete-branch-event",
                            element.StableId,
                            entryIndex,
                            default,
                            "rejected",
                            LastMessage);
                        ClearPendingCascade();
                        return;
                    }
                    int detectorCountBefore =
                        _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? -1;
                    if (!string.Equals(_pendingCascadeDetectorId, element.StableId, StringComparison.Ordinal)
                        || !_pendingCascadeIdentity.Matches(detectorIdentity))
                    {
                        removed = _controller.TryDeleteLogicDetector(
                            entryIndex,
                            detectorIdentity,
                            element.StableId,
                            false,
                            out int linkedCount,
                            out resultingCardDraftId,
                            out diagnostic);
                        if (!removed && linkedCount > 0)
                        {
                            _pendingCascadeDetectorId = element.StableId;
                            _pendingCascadeIdentity = detectorIdentity;
                            LastMessage = "再次点击删除以确认级联删除 " + linkedCount + " 条 Transition。";
                            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                                "command",
                                "delete-branch-event",
                                element.StableId,
                                entryIndex,
                                detectorIdentity,
                                "confirmation-required",
                                LastMessage,
                                detectorCountBefore,
                                detectorCountBefore);
                            _refresh?.Invoke();
                            return;
                        }
                        if (!removed)
                        {
                            LastMessage = diagnostic;
                            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                                "mutation",
                                "delete-branch-event",
                                element.StableId,
                                entryIndex,
                                detectorIdentity,
                                "failed",
                                diagnostic,
                                detectorCountBefore,
                                detectorCountBefore);
                            return;
                        }
                        break;
                    }
                    removed = _controller.TryDeleteLogicDetector(
                        entryIndex,
                        detectorIdentity,
                        element.StableId,
                        true,
                        out _,
                        out resultingCardDraftId,
                        out diagnostic);
                    CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                        "mutation",
                        "delete-branch-event",
                        element.StableId,
                        entryIndex,
                        detectorIdentity,
                        removed ? "committed" : "failed",
                        diagnostic,
                        detectorCountBefore,
                        removed ? Math.Max(0, detectorCountBefore - 1) : detectorCountBefore);
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    if (!TryPrepareGraphMutation(out entryIndex, out var transitionIdentity))
                        return;
                    removed = _controller.TryDeleteLogicTransition(
                        entryIndex,
                        transitionIdentity,
                        element.StableId,
                        out resultingCardDraftId,
                        out diagnostic);
                    break;
                default:
                    LastMessage = "当前没有可删除的时间轴元素。";
                    return;
            }
            if (!removed)
            {
                LastMessage = diagnostic;
                return;
            }
            _setSelection?.Invoke(kind, Math.Max(0, index - 1));
            if (kind != CharacterTestRuntimeAiLabLogicElementKind.Stage)
                PublishGraphContext(CharacterTestRuntimeAiLabLogicElementKind.None, string.Empty, resultingCardDraftId);
            ClearPendingCascade();
            ClearPendingPriority();
            LastMessage = diagnostic;
            _refresh?.Invoke();
            if (kind == CharacterTestRuntimeAiLabLogicElementKind.Detector)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.Write(
                    "refresh",
                    "delete-branch-event",
                    element.StableId,
                    entryIndex,
                    default,
                    "requested",
                    LastMessage,
                    -1,
                    _workspaceView?.Invoke()?.Timeline?.DetectorCount ?? -1);
            }
        }

        private bool CanMutate(out int entryIndex)
        {
            entryIndex = _selectedEntryIndex?.Invoke() ?? -1;
            if (_controller?.IsHistoricalFrameSelected == true
                || _workspaceView?.Invoke()?.Timeline?.Historical == true)
            {
                LastMessage = CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
                    .ReadOnlyDiagnostic;
                return false;
            }
            if (_controller == null || entryIndex < 0)
            {
                LastMessage = "行为卡编辑上下文不可用。";
                return false;
            }
            if (_controller.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer
                || !_controller.CanEditBehaviorCardDraft)
            {
                LastMessage = "当前帧或候选状态不允许修改行为卡草稿。";
                return false;
            }
            return true;
        }

        internal bool TryPrepareGraphMutation(
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity)
        {
            identity = default;
            if (!CanMutate(out entryIndex))
                return false;
            if (_controller.TryResolveStageEditIdentity(entryIndex, out identity, out string diagnostic))
                return true;
            LastMessage = diagnostic;
            return false;
        }

        internal void PublishGraphContext(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            string stableId,
            string resultingCardDraftId)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || (state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    && (state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                        || state.PageContext.TimelineMode
                            != CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)))
                return;
            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                string.IsNullOrWhiteSpace(resultingCardDraftId)
                    ? page.CardDraftId
                    : resultingCardDraftId,
                ToShellElementKind(kind),
                stableId ?? string.Empty,
                page.BehaviorCardViewport);
        }

        internal bool TryResolveElement(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            string stableId,
            out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(stableId))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                _workspaceView?.Invoke()?.Timeline;
            if (timeline == null)
                return false;
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Stage:
                    for (int i = 0; i < Math.Min(timeline.StageCount, timeline.Stages?.Length ?? 0); i++)
                    {
                        if (string.Equals(timeline.Stages[i]?.StageId, stableId, StringComparison.Ordinal))
                        {
                            index = i;
                            return true;
                        }
                    }
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    for (int i = 0; i < Math.Min(timeline.DetectorCount, timeline.Detectors?.Length ?? 0); i++)
                    {
                        if (string.Equals(timeline.Detectors[i]?.DetectorId, stableId, StringComparison.Ordinal))
                        {
                            index = i;
                            return true;
                        }
                    }
                    break;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    for (int i = 0; i < Math.Min(timeline.TransitionCount, timeline.Transitions?.Length ?? 0); i++)
                    {
                        if (string.Equals(timeline.Transitions[i]?.TransitionId, stableId, StringComparison.Ordinal))
                        {
                            index = i;
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }

        private CharacterTestRuntimeAiLabLogicElementKind ResolveElementKind(string stableId)
        {
            if (TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Stage, stableId, out _))
                return CharacterTestRuntimeAiLabLogicElementKind.Stage;
            if (TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Detector, stableId, out _))
                return CharacterTestRuntimeAiLabLogicElementKind.Detector;
            if (TryResolveElement(CharacterTestRuntimeAiLabLogicElementKind.Transition, stableId, out _))
                return CharacterTestRuntimeAiLabLogicElementKind.Transition;
            return CharacterTestRuntimeAiLabLogicElementKind.None;
        }

        private void PublishSelection(CharacterTestRuntimeAiLabLogicElementKind kind, string stableId)
        {
            if (IsHistorical)
                return;
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (!CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
                    .HostsBehaviorCardContext(state))
                return;
            CharacterTestRuntimeAiLabPageContext page = state.PageContext;
            coordinator.TryUpdateBehaviorCardContext(
                state.Generation,
                state.Revision,
                page.GroupDraftId,
                page.EntryId,
                page.CardDraftId,
                ToShellElementKind(kind),
                stableId,
                page.BehaviorCardViewport);
        }

        private static CharacterTestRuntimeAiLabSelectedElementKind ToShellElementKind(
            CharacterTestRuntimeAiLabLogicElementKind kind)
        {
            switch (kind)
            {
                case CharacterTestRuntimeAiLabLogicElementKind.Stage:
                    return CharacterTestRuntimeAiLabSelectedElementKind.Stage;
                case CharacterTestRuntimeAiLabLogicElementKind.Detector:
                    return CharacterTestRuntimeAiLabSelectedElementKind.Detector;
                case CharacterTestRuntimeAiLabLogicElementKind.Transition:
                    return CharacterTestRuntimeAiLabSelectedElementKind.Transition;
                default:
                    return CharacterTestRuntimeAiLabSelectedElementKind.None;
            }
        }

        private void ClearPendingPriority()
        {
            _pendingPriorityDetectorId = string.Empty;
            _pendingPriorityDirection = 0;
            _pendingPriorityIdentity = default;
        }

        private void ClearPendingCascade()
        {
            _pendingCascadeDetectorId = string.Empty;
            _pendingCascadeIdentity = default;
        }
    }
}
