using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private bool TryHandleWorkbenchCommand(MxUiCommand command)
        {
            string commandId = command.CommandId;
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry:
                    if (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                    {
                        bool added = _controller.TryAddNextProjectCardToTestComposition(
                            out int companionIndex,
                            out string companionMessage);
                        if (added)
                            SelectedWorkbenchEntryIndex = companionIndex;
                        LastStatusMessage = added
                            ? companionMessage
                            : "添加陪测行为失败：" + companionMessage;
                    }
                    else
                    {
                        LastStatusMessage = MutateSelectedEntry(_controller.TryDuplicateDraftEntry);
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard:
                    string createCardMessage = "Runtime AI Lab 项目行为卡编辑器不可用。";
                    int createdEntryIndex = -1;
                    bool created = _controller != null
                        && _controller.TryCreateProjectCardInCurrentBehaviorGroup(
                            out createdEntryIndex,
                            out _,
                            out createCardMessage);
                    if (created)
                    {
                        SelectedWorkbenchEntryIndex = createdEntryIndex;
                        SelectedCompiledStepIndex = 0;
                        SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
                        SelectedLogicElementIndex = 0;
                    }
                    LastStatusMessage = created
                        ? createCardMessage
                        : "创建行为卡失败：" + createCardMessage;
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryUp:
                    LastStatusMessage = MutateSelectedEntry(
                        (int index, out string message) =>
                            _controller.TryMoveDraftEntry(index, -1, out message));
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryDown:
                    LastStatusMessage = MutateSelectedEntry(
                        (int index, out string message) =>
                            _controller.TryMoveDraftEntry(index, 1, out message));
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry:
                    LastStatusMessage = MutateEntry(
                        ResolveEntryMutationIndex(command.Payload),
                        _controller.TryToggleDraftEntry);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry:
                    DeleteEntry(ResolveEntryMutationIndex(command.Payload));
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteProjectCard:
                    DeleteProjectCard(command.Payload as string);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReassignEntryResponsibility:
                    if (TryParseResponsibilityMove(
                            command.Payload as string,
                            out int responsibilityEntryIndex,
                            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind))
                    {
                        LastStatusMessage = _controller.TrySetDraftEntryResponsibility(
                                responsibilityEntryIndex,
                                responsibilityKind,
                                out string responsibilityMessage)
                            ? responsibilityMessage
                            : responsibilityMessage;
                        if (string.IsNullOrEmpty(LastStatusMessage))
                            LastStatusMessage = "行为职责没有变化。";
                    }
                    else
                    {
                        LastStatusMessage = "行为职责拖放数据无效。";
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetEntryResponsibilities:
                    if (TryParseResponsibilitySet(
                            command.Payload as string,
                            out int responsibilitySetEntryIndex,
                            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] responsibilityKinds))
                    {
                        _controller.TrySetDraftEntryResponsibilities(
                            responsibilitySetEntryIndex,
                            responsibilityKinds,
                            out string responsibilitySetMessage);
                        LastStatusMessage = responsibilitySetMessage;
                    }
                    else
                    {
                        LastStatusMessage = "职责集合命令无效。";
                    }
                    _refreshPanel?.Invoke();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.UndoDraft:
                    if (_controller?.CanUndoProjectCardDeletion == true)
                    {
                        bool restored = _controller.TryUndoProjectCardDeletion(
                            out int restoredEntryIndex,
                            out string restoreMessage);
                        if (restored && restoredEntryIndex >= 0)
                            SelectedWorkbenchEntryIndex = restoredEntryIndex;
                        LastStatusMessage = restored
                            ? restoreMessage
                            : "撤销删除失败。" + restoreMessage;
                        ShowToast(LastStatusMessage, !restored);
                        PublishBehaviorCardContextIfNeeded();
                    }
                    else if (_behaviorCardWorkspaceCommands != null
                        && (_controller.ShellCoordinator?.CurrentState?.ActiveWorkspace
                                == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                            || (_controller.ShellCoordinator?.CurrentState?.ActiveWorkspace
                                    == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                                && TimelineMode
                                    != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)))
                    {
                        _behaviorCardWorkspaceCommands.UndoStageEdit();
                        LastStatusMessage = _behaviorCardWorkspaceCommands.LastMessage;
                        ShowToast(LastStatusMessage, isWarning: false);
                    }
                    else
                    {
                        bool ok = _controller.TryUndoDraft(out string undoMessage);
                        LastStatusMessage = ok
                            ? undoMessage
                            : "撤销失败。" + undoMessage;
                        ShowToast(LastStatusMessage, !ok);
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft:
                    if (_controller?.HasStandaloneProjectCardChanges(_selectedLibraryStableCardId) == true)
                    {
                        bool standaloneSaved = _controller.TryPublishStandaloneProjectCard(
                            _selectedLibraryStableCardId,
                            out string standaloneSaveMessage);
                        LastStatusMessage = standaloneSaved
                            ? standaloneSaveMessage
                            : "保存失败：" + standaloneSaveMessage;
                        CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                            "action=save-project-card"
                            + " result=" + (standaloneSaved ? "success" : "failed")
                            + " card=" + _selectedLibraryStableCardId
                            + " message=" + standaloneSaveMessage);
                        _panelStatusSink?.Invoke(LastStatusMessage);
                        _refreshPanel?.Invoke();
                        break;
                    }
                    if (TimelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic)
                    {
                        _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
                        // Mutations can replace or delete the selected card identity. Commit the
                        // post-edit selection before the queued session rebuild captures the shell
                        // source context, otherwise remounting can target an identity that no longer exists.
                        PublishBehaviorCardContextIfNeeded();
                        bool saveQueued = _controller.QueueBehaviorRecipeSaveAndRun(
                            SelectedWorkbenchEntryIndex,
                            out string saveRunMessage);
                        LastStatusMessage = saveRunMessage;
                        _behaviorRecipeSaveStatusPending = saveQueued;
                        _panelStatusSink?.Invoke(LastStatusMessage);
                        _refreshPanel?.Invoke();
                        break;
                    }
                    _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
                    if (IsBehaviorTestAuthoringContext())
                    {
                        bool reran = _controller.ApplyBehaviorTestAndRerun(out string rerunMessage);
                        LastStatusMessage = reran
                            ? rerunMessage
                            : "应用并重跑失败：" + rerunMessage;
                        if (reran)
                        {
                            string completedMessage = LastStatusMessage;
                            SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
                            LastStatusMessage = completedMessage;
                        }
                    }
                    else
                    {
                        LastStatusMessage = Execute(
                            _controller.QueueCandidateApply,
                            "候选版本已排队，将应用到下一帧。",
                            "候选版本无法应用。");
                    }
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft:
                    bool publishBehaviorGroup =
                        WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                        || (WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                            && TimelineMode == CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic);
                    bool published = publishBehaviorGroup
                        ? _controller.TryPublishBehaviorGroupDraft(out string publishMessage)
                        : _controller.TryPublishBehaviorCardDraft(
                            SelectedWorkbenchEntryIndex,
                            out publishMessage);
                    LastStatusMessage = published
                        ? publishBehaviorGroup
                            ? "行为组已保存；当前测试仍使用启动时版本，请重新开始测试。"
                            : "行为版本已发布；当前测试仍使用启动时版本。"
                        : "保存失败：" + publishMessage;
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveBehaviorGroup:
                    bool groupSaved = _controller.TryPublishBehaviorGroupDraft(
                        out string saveBehaviorGroupMessage);
                    LastStatusMessage = groupSaved
                        ? "行为组已保存；当前测试仍使用启动时版本，重新开始后生效。"
                        : "保存行为组失败：" + saveBehaviorGroupMessage;
                    CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                        "action=save-behavior-group"
                        + " result=" + (groupSaved ? "success" : "failed")
                        + " message=" + saveBehaviorGroupMessage);
                    // The behavior-logic workspace renders command status as its feedback floater.
                    // Refresh immediately so the result is visible and the save button is rebound
                    // from the newly updated group checkpoint on both success and failure.
                    _panelStatusSink?.Invoke(LastStatusMessage);
                    _refreshPanel?.Invoke();
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReloadProjectDraft:
                    LastStatusMessage = _controller.TryReloadCurrentProjectGroup(out string reloadMessage)
                        ? reloadMessage
                        : "重新加载失败：" + reloadMessage;
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReapplyProjectDraft:
                    LastStatusMessage = _controller.TryReapplyCurrentProjectGroupOnLatest(out string reapplyMessage)
                        ? reapplyMessage
                        : "重新应用失败：" + reapplyMessage;
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.DisplayName, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardCost:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.Cost, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDescription:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.ShortDescription, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardUseSummary:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.UseSummary, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardRiskSummary:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.RiskSummary, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardTags:
                    SetProjectCardMetadata(CharacterTestRuntimeAiLabProjectCardMetadataField.Tags, command.Payload);
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayStopDistance:
                    LastStatusMessage = command.Payload is float stopDistance
                        ? MutateSelectedEntry(
                            (int selected, out string message) => _controller.TrySetGameplayStopDistance(
                                selected,
                                stopDistance,
                                out message))
                        : "停止距离输入无效，草稿未修改。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayThreatWindow:
                    LastStatusMessage = command.Payload is CharacterTestRuntimeAiLabThreatWindowEdit threatWindow
                        ? MutateSelectedEntry(
                            (int selected, out string message) => _controller.TrySetGameplayThreatParameters(
                                selected,
                                threatWindow.MinimumImpactFrames,
                                threatWindow.MaximumImpactFrames,
                                threatWindow.CooldownFrames,
                                out message))
                        : "威胁窗口输入无效，草稿未修改。";
                    break;
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayDodgeCooldown:
                    LastStatusMessage = command.Payload is int cooldownFrames
                        ? MutateSelectedEntry(
                            (int selected, out string message) => _controller.TrySetGameplayDodgeCooldown(
                                selected,
                                cooldownFrames,
                                out message))
                        : "闪避冷却输入无效，草稿未修改。";
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
