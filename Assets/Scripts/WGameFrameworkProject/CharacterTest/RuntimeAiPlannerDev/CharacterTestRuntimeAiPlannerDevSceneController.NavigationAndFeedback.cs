using System;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabNavigationExitResult _lastNavigationExitResult;
        private CharacterTestRuntimeAiLabNavigationExitIntent _pendingNavigationExitIntent;
        private bool _hasPendingNavigationExit;

        internal CharacterTestRuntimeAiLabNavigationExitResult LastNavigationExitResult =>
            _lastNavigationExitResult;
        internal CharacterTestRuntimeAiLabNavigationFeedback NavigationFeedback =>
            _lastNavigationExitResult.Feedback
            ?? CharacterTestRuntimeAiLabNavigationFeedback.None;
        internal bool HasPendingNavigationExit => _hasPendingNavigationExit;
        internal CharacterTestRuntimeAiLabNavigationExitIntent PendingNavigationExitIntent =>
            _pendingNavigationExitIntent;

        internal bool TryPrepareAuthoringNavigationExit(
            CharacterTestRuntimeAiLabNavigationExitIntent intent,
            bool characterActionOpen,
            bool characterActionDirty,
            out CharacterTestRuntimeAiLabNavigationExitResult result)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = _draftWorkspace;
            bool canWrite = workspace?.IsReadOnly != true;
#if !UNITY_EDITOR
            canWrite = false;
#endif
            var state = new CharacterTestRuntimeAiLabNavigationExitState(
                IsHistoricalFrameSelected,
                IsAuthoringNavigationBusy(out string busyStage),
                busyStage,
                workspace?.Dirty == true,
                canWrite,
                characterActionOpen,
                characterActionDirty);
            result = CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                intent,
                state,
                () =>
                {
                    bool saved = FlushDraftWorkspace(out string diagnostic);
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(saved, diagnostic);
                });
            _lastNavigationExitResult = result;
            _hasPendingNavigationExit = !result.CanProceed;
            _pendingNavigationExitIntent = intent;
            return result.CanProceed;
        }

        internal bool RetryPendingAuthoringNavigationSave(
            out CharacterTestRuntimeAiLabNavigationExitResult result)
        {
            if (!_hasPendingNavigationExit
                || _lastNavigationExitResult.Disposition
                    != CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed)
            {
                result = CancelledNavigationResult("没有需要重试的返回保存操作。");
                return false;
            }

            var state = new CharacterTestRuntimeAiLabNavigationExitState(
                historicalReadOnly: false,
                IsAuthoringNavigationBusy(out string busyStage),
                busyStage,
                draftDirty: _draftWorkspace?.Dirty == true,
                canWriteDraft: _draftWorkspace?.IsReadOnly != true,
                characterActionOpen: false,
                characterActionDirty: false);
            result = CharacterTestRuntimeAiLabNavigationExitPolicy.Evaluate(
                _pendingNavigationExitIntent,
                state,
                () =>
                {
                    bool saved = FlushDraftWorkspace(out string diagnostic);
                    return new CharacterTestRuntimeAiLabNavigationFlushResult(saved, diagnostic);
                });
            _lastNavigationExitResult = result;
            _hasPendingNavigationExit = !result.CanProceed;
            return result.CanProceed;
        }

        internal void CancelPendingAuthoringNavigation()
        {
            _hasPendingNavigationExit = false;
            _lastNavigationExitResult = CancelledNavigationResult("已取消返回，继续留在当前页面。");
        }

        internal void ClearAuthoringNavigationFeedback()
        {
            _hasPendingNavigationExit = false;
            _lastNavigationExitResult = default;
        }

        internal bool DiscardPendingAuthoringNavigationChanges(
            out CharacterTestRuntimeAiLabNavigationExitResult result)
        {
#if UNITY_EDITOR
            if (!_hasPendingNavigationExit
                || _lastNavigationExitResult.Disposition
                    != CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed
                || _draftPersistence == null
                || !_draftPersistence.TryDiscardUnsavedChanges(
                    out CharacterTestRuntimeAiLabDraftWorkspace restored,
                    out string diagnostic))
            {
                result = new CharacterTestRuntimeAiLabNavigationExitResult(
                    CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed,
                    flushAttempted: false,
                    RestoreFailure(
                        "NAVIGATION_DISCARD_FAILED",
                        "无法放弃本次修改",
                        "已保存草稿没有改变，请继续留在当前页面。",
                        _draftPersistence?.LastStatus ?? "Discard callback failed."));
                _lastNavigationExitResult = result;
                return false;
            }

            AdoptRoleWorkspace(restored);
            result = new CharacterTestRuntimeAiLabNavigationExitResult(
                CharacterTestRuntimeAiLabNavigationExitDisposition.Proceed,
                flushAttempted: false,
                CharacterTestRuntimeAiLabNavigationFeedback.None);
            _lastNavigationExitResult = result;
            _hasPendingNavigationExit = false;
            _statusMessage = diagnostic;
            return true;
#else
            result = new CharacterTestRuntimeAiLabNavigationExitResult(
                CharacterTestRuntimeAiLabNavigationExitDisposition.SaveFailed,
                flushAttempted: false,
                RestoreFailure(
                    "NAVIGATION_PLAYER_READ_ONLY",
                    "Player 构建为只读",
                    "当前版本不能放弃或写回项目草稿。",
                    string.Empty));
            _lastNavigationExitResult = result;
            return false;
#endif
        }

        internal CharacterTestRuntimeAiLabStableReturnContext CaptureStableAuthoringReturnContext(
            string timelineId,
            string eventId,
            int inspectorTargetKind,
            CharacterTestRuntimeAiLabPageContext pageContext)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = _draftWorkspace;
            string groupDraftId = pageContext?.GroupDraftId ?? string.Empty;
            string entryId = pageContext?.EntryId ?? string.Empty;
            string cardDraftId = pageContext?.CardDraftId ?? string.Empty;
            string groupPublishedId = string.Empty;
            string cardPublishedId = string.Empty;
            if (workspace != null
                && workspace.Groups.TryGetValue(
                    groupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                groupPublishedId = group?.PublishedStableId ?? string.Empty;
            }
            if (workspace != null
                && workspace.Cards.TryGetValue(
                    cardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card))
            {
                cardPublishedId = card?.PublishedStableId ?? string.Empty;
            }

            int role = 0;
            bool hasRole = false;
#if UNITY_EDITOR
            hasRole = HasActiveAuthoringWorkspaceRole;
            role = hasRole ? (int)ActiveAuthoringWorkspaceRole : 0;
#endif
            return new CharacterTestRuntimeAiLabStableReturnContext(
                role,
                hasRole,
                workspace?.WorkspaceId ?? string.Empty,
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                _selectedAgentIndex,
                ResolveSelectedAgentEntityId(),
                groupDraftId,
                groupPublishedId,
                entryId,
                cardDraftId,
                cardPublishedId,
                timelineId,
                eventId,
                inspectorTargetKind,
                pageContext?.TimelineMode ?? CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                pageContext);
        }

        internal bool TryRestoreStableAuthoringReturnContext(
            in CharacterTestRuntimeAiLabStableReturnContext context,
            out bool exactCardContext,
            out CharacterTestRuntimeAiLabNavigationFeedback feedback)
        {
            exactCardContext = false;
            if (!context.IsValid || _draftWorkspace == null)
            {
                feedback = RestoreFailure(
                    "NAVIGATION_RETURN_CONTEXT_INVALID",
                    "原编辑位置无法恢复",
                    "返回上下文已经失效。",
                    "workspace=" + (context.WorkspaceId ?? string.Empty));
                return false;
            }
#if UNITY_EDITOR
            if (context.HasAuthoringWorkspaceRole
                && (!HasActiveAuthoringWorkspaceRole
                    || (int)ActiveAuthoringWorkspaceRole != context.AuthoringWorkspaceRole))
            {
                feedback = RestoreFailure(
                    "NAVIGATION_WORKSPACE_ROLE_CHANGED",
                    "原编辑位置无法恢复",
                    "工作区角色已经变化；返回操作不会自动切换工作区。",
                    "expectedRole=" + context.AuthoringWorkspaceRole
                        + "; actualRole=" + (HasActiveAuthoringWorkspaceRole
                            ? ((int)ActiveAuthoringWorkspaceRole).ToString()
                            : "none"));
                return false;
            }
#endif
            if (!string.Equals(
                    _draftWorkspace.WorkspaceId,
                    context.WorkspaceId,
                    StringComparison.Ordinal))
            {
                feedback = RestoreFailure(
                    "NAVIGATION_WORKSPACE_CHANGED",
                    "原编辑位置无法恢复",
                    "当前工作区与进入子编辑器时不同。",
                    "expectedWorkspace=" + context.WorkspaceId
                        + "; actualWorkspace=" + _draftWorkspace.WorkspaceId);
                return false;
            }

            if (_matchConfig?.TryGetSlot(context.SetupTeamId, context.SetupSlotIndex) == null)
            {
                feedback = RestoreFailure(
                    "NAVIGATION_AGENT_SLOT_MISSING",
                    "已返回编辑逻辑",
                    "原角色槽位已不存在，无法恢复原角色选择。",
                    "team=" + context.SetupTeamId + "; slot=" + context.SetupSlotIndex);
                return false;
            }
            _selectedSetupTeamId = context.SetupTeamId;
            _selectedSetupSlotIndex = context.SetupSlotIndex;
            if (context.SelectedAgentEntityId <= 0
                || !TrySelectAgentEntityId(context.SelectedAgentEntityId))
            {
                int agentCount = ResolveSelectedAgentCount();
                if (context.SelectedAgentIndex >= 0 && context.SelectedAgentIndex < agentCount)
                    SelectAgent(context.SelectedAgentIndex);
            }

            if (!TryResolveStableBehaviorCardContext(
                    context,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out string missing))
            {
                feedback = RestoreFailure(
                    "NAVIGATION_CARD_CONTEXT_MISSING",
                    "已返回编辑逻辑",
                    "原行为卡定位已不存在：" + missing + "。",
                    "groupDraftId=" + context.GroupDraftId
                        + "; entryId=" + context.EntryId
                        + "; cardDraftId=" + context.CardDraftId);
                return false;
            }

            CharacterTestRuntimeAiLabShellState shellState = _shellCoordinator?.CurrentState;
            if (shellState != null
                && (shellState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    || shellState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame))
            {
                CharacterTestRuntimeAiLabPageContext captured = context.PageContext;
                CharacterTestRuntimeAiLabNavigationResult updated =
                    _shellCoordinator.TryUpdateBehaviorCardContext(
                        shellState.Generation,
                        shellState.Revision,
                        group.GroupDraftId,
                        entry.EntryId,
                        card.CardDraftId,
                        captured?.SelectedElementKind
                            ?? CharacterTestRuntimeAiLabSelectedElementKind.None,
                        captured?.SelectedElementStableId ?? string.Empty,
                        captured?.BehaviorCardViewport
                            ?? CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
                if (!updated.Success)
                {
                    feedback = RestoreFailure(
                        "NAVIGATION_SHELL_CONTEXT_RESTORE_FAILED",
                        "已返回编辑逻辑",
                        "行为卡已恢复，但页面定位恢复失败。",
                        updated.Code + ": " + updated.Message);
                    return false;
                }
                shellState = _shellCoordinator.CurrentState;
                if (shellState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                    && shellState.PageContext.TimelineMode != context.TimelineMode)
                {
                    CharacterTestRuntimeAiLabNavigationResult mode =
                        _shellCoordinator.TrySetTimelineMode(
                            shellState.Generation,
                            shellState.Revision,
                            shellState.ActiveWorkspace,
                            context.TimelineMode,
                            outgoingModeExpanded: true);
                    if (!mode.Success)
                    {
                        feedback = RestoreFailure(
                            "NAVIGATION_TIMELINE_MODE_RESTORE_FAILED",
                            "已返回编辑逻辑",
                            "行为卡已恢复，但时间轴模式恢复失败。",
                            mode.Code + ": " + mode.Message);
                        return false;
                    }
                }
            }

            exactCardContext = true;
            feedback = CharacterTestRuntimeAiLabNavigationFeedback.None;
            return true;
        }

        private bool IsAuthoringNavigationBusy(out string stage)
        {
            if (_testCurrentChangesTracker.IsBusy)
            {
                stage = "测试当前修改";
                return true;
            }
            if (_behaviorRecipeSaveQueued || _behaviorRecipeSaveInProgress)
            {
                stage = "草稿保存";
                return true;
            }
            if (_shellCoordinator?.CurrentState?.PresentationMode
                == CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress)
            {
                stage = "测试重启";
                return true;
            }
            CharacterTestRuntimeAiLabPublishCurrentChangesStatus publishStatus =
                _lastPublishCurrentChangesResult?.Status
                ?? CharacterTestRuntimeAiLabPublishCurrentChangesStatus.None;
            if (publishStatus == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Busy
                || publishStatus == CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CatalogCommitted)
            {
                stage = "发布当前修改";
                return true;
            }
            CharacterTestRuntimeAiLabCharacterActionAuthoringStatus actionStatus =
                CharacterTestRuntimeAiLabCharacterActionAuthoringBridge.LastResult.Status;
            if (actionStatus == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Importing
                || actionStatus == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Reloading
                || actionStatus == CharacterTestRuntimeAiLabCharacterActionAuthoringStatus.Restarting)
            {
                stage = "角色动作应用";
                return true;
            }
            stage = string.Empty;
            return false;
        }

        private bool TryResolveStableBehaviorCardContext(
            in CharacterTestRuntimeAiLabStableReturnContext context,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string missing)
        {
            group = null;
            entry = null;
            card = null;
            missing = string.Empty;
            if (!_draftWorkspace.Groups.TryGetValue(context.GroupDraftId, out group)
                && !TryFindUniqueGroupByPublishedId(context.GroupPublishedStableId, out group))
            {
                missing = "行为组";
                return false;
            }
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft candidate = group.Entries[i];
                if (candidate != null
                    && string.Equals(candidate.EntryId, context.EntryId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    break;
                }
            }
            if (entry == null)
            {
                missing = "行为条目";
                return false;
            }
            if (!_draftWorkspace.Cards.TryGetValue(entry.CardDraftId, out card)
                || card == null)
            {
                if (!TryFindUniqueCardByPublishedId(context.CardPublishedStableId, out card))
                {
                    missing = "行为卡";
                    return false;
                }
            }
            return true;
        }

        private bool TryFindUniqueGroupByPublishedId(
            string stableId,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft match)
        {
            match = null;
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft candidate in _draftWorkspace.Groups.Values)
            {
                if (candidate == null
                    || !string.Equals(candidate.PublishedStableId, stableId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (match != null)
                {
                    match = null;
                    return false;
                }
                match = candidate;
            }
            return match != null;
        }

        private bool TryFindUniqueCardByPublishedId(
            string stableId,
            out CharacterTestRuntimeAiLabBehaviorCardDraft match)
        {
            match = null;
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in _draftWorkspace.Cards.Values)
            {
                if (candidate == null
                    || !string.Equals(candidate.PublishedStableId, stableId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (match != null)
                {
                    match = null;
                    return false;
                }
                match = candidate;
            }
            return match != null;
        }

        private static CharacterTestRuntimeAiLabNavigationExitResult CancelledNavigationResult(
            string message)
        {
            return new CharacterTestRuntimeAiLabNavigationExitResult(
                CharacterTestRuntimeAiLabNavigationExitDisposition.Cancelled,
                flushAttempted: false,
                new CharacterTestRuntimeAiLabNavigationFeedback(
                    "NAVIGATION_CANCELLED",
                    "已取消返回",
                    message,
                    "草稿、游戏版本和当前测试都没有改变。",
                    Array.Empty<CharacterTestRuntimeAiLabNavigationFeedbackAction>()));
        }

        private static CharacterTestRuntimeAiLabNavigationFeedback RestoreFailure(
            string code,
            string title,
            string message,
            string technicalDetails)
        {
            return new CharacterTestRuntimeAiLabNavigationFeedback(
                code,
                title,
                message,
                "草稿仍然安全保留。",
                new[] { CharacterTestRuntimeAiLabNavigationFeedbackAction.ViewDetails },
                technicalDetails);
        }
    }
}
