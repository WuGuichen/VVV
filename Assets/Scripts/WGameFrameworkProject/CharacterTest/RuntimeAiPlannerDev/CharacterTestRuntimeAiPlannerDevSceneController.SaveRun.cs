using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult _lastBehaviorRecipeSaveRun;
        private CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult _lastSuccessfulBehaviorRecipeSaveRun;
        private int _runningBehaviorRecipeWorkspaceRevision;
        private bool _deferBaselineActiveCommit;
        private bool _behaviorRecipeSaveInProgress;
        private bool _behaviorRecipeSaveQueued;
        private int _queuedBehaviorRecipeEntryIndex = -1;
        private string _behaviorRecipeSaveCheckpointWorkspaceId = string.Empty;
        private readonly Dictionary<string, int> _behaviorCardSaveCheckpointRevisions =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _behaviorGroupSaveCheckpointRevisions =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult LastBehaviorRecipeSaveRun =>
            _lastBehaviorRecipeSaveRun;
        internal bool BehaviorRecipeSaveQueued => _behaviorRecipeSaveQueued;
        internal bool BehaviorRecipeSaveInProgress => _behaviorRecipeSaveInProgress;
        internal bool HasBehaviorRecipeChangesToSave
        {
            get
            {
                CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
                EnsureBehaviorRecipeSaveCheckpoint(workspace);
                foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in workspace.Cards.Values)
                {
                    if (card == null)
                        continue;
                    if (_behaviorCardSaveCheckpointRevisions.TryGetValue(
                            BehaviorCardSaveIdentity(card),
                            out int savedRevision))
                    {
                        if (card.CurrentRevision != savedRevision)
                            return true;
                    }
                    else if (card.CurrentRevision > 1)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        internal bool CanSaveBehaviorRecipeAndRun => HasBehaviorRecipeChangesToSave
            && !_behaviorRecipeSaveInProgress
            && !_behaviorRecipeSaveQueued
            && !IsHistoricalFrameSelected
            && !_seekInProgress
            && !_loopPlaybackActive
            && !_candidateApplyQueued
            && !_branchConfirmationPending
            && (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                || _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running);

        internal bool CanSaveBehaviorRecipeForEntry(int selectedEntryIndex, out string reason)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (!TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || selectedEntryIndex < 0
                || selectedEntryIndex >= group.Entries.Count
                || !workspace.Cards.TryGetValue(
                    group.Entries[selectedEntryIndex].CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || card == null)
            {
                reason = "当前没有可保存的行为卡。";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                _context?.Slice != null
                    ? CompileBehaviorCardLogic(card)
                    : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
            if (compile == null || !compile.Success || compile.Program == null)
            {
                reason = FormatBehaviorCardCompileFailure(card, compile);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        internal CharacterTestRuntimeAiLabBehaviorRecipeSyncState BehaviorRecipeSyncState
        {
            get
            {
                bool sameGroup = TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup)
                    && selectedGroup != null
                    && string.Equals(
                        selectedGroup.GroupDraftId,
                        _lastSuccessfulBehaviorRecipeSaveRun.GroupDraftId,
                        StringComparison.Ordinal);
                if (HasBehaviorRecipeChangesToSave)
                {
                    return _lastSuccessfulBehaviorRecipeSaveRun.Success && sameGroup
                        ? CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningOlderDraft
                        : CharacterTestRuntimeAiLabBehaviorRecipeSyncState.Unsaved;
                }
                if (_lastBehaviorRecipeSaveRun.Status
                    == CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedRuntimeInstallFailed
                    || _lastBehaviorRecipeSaveRun.Status
                    == CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedRuntimeIdentityMismatch)
                {
                    return CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RuntimeLoadFailed;
                }
                if (_lastSuccessfulBehaviorRecipeSaveRun.Success && sameGroup
                    && _runningBehaviorRecipeWorkspaceRevision == DraftWorkspace.CurrentRevision)
                {
                    return CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningCurrentDraft;
                }
                if (_lastSuccessfulBehaviorRecipeSaveRun.Success && sameGroup)
                    return CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningOlderDraft;
                if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                    && _activeRevision?.Profile != null
                    && selectedGroup != null
                    && string.Equals(
                        selectedGroup.GroupDraftId,
                        _activeRevision.GroupDraftId,
                        StringComparison.Ordinal))
                {
                    return CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningPublishedVersion;
                }
                return CharacterTestRuntimeAiLabBehaviorRecipeSyncState.SavedNotRunning;
            }
        }

        internal string BehaviorRecipeSyncStatusText
        {
            get
            {
                TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup);
                CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult successfulRun =
                    _lastSuccessfulBehaviorRecipeSaveRun;
                string cardName = string.Empty;
                if (!string.IsNullOrEmpty(successfulRun.CardDraftId)
                    && DraftWorkspace.Cards.TryGetValue(
                        successfulRun.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft selectedCard))
                {
                    cardName = selectedCard?.DisplayName ?? string.Empty;
                }
                string editIdentity = selectedGroup?.DisplayName ?? "当前行为组";
                if (!string.IsNullOrWhiteSpace(cardName))
                    editIdentity += " / " + cardName;
                string edit = "编辑：" + editIdentity + " · 草稿 r" + DraftWorkspace.CurrentRevision
                    + (HasBehaviorRecipeChangesToSave ? " · 有修改" : " · 已保存");
                CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult run = successfulRun.Success
                    ? successfulRun
                    : _lastBehaviorRecipeSaveRun;
                switch (BehaviorRecipeSyncState)
                {
                    case CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningCurrentDraft:
                        return edit + "\n运行：T1-1 / 槽位 " + run.RuntimeProfileSlot
                            + " · 草稿快照 r" + run.WorkspaceRevision
                            + "\n状态：● 与当前编辑一致";
                    case CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningOlderDraft:
                        return edit + "\n运行：草稿快照 r" + run.WorkspaceRevision
                            + "\n状态：⚠ 正在运行上次保存版本"
                            + (!_lastBehaviorRecipeSaveRun.Success
                                && !string.IsNullOrWhiteSpace(_lastBehaviorRecipeSaveRun.Message)
                                    ? "\n当前草稿：已保存，但未运行\n原因："
                                        + _lastBehaviorRecipeSaveRun.Message
                                    : string.Empty);
                    case CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RuntimeLoadFailed:
                        return edit + "\n运行：未装载\n状态：草稿已保存，但未运行\n原因：" + run.Message;
                    case CharacterTestRuntimeAiLabBehaviorRecipeSyncState.RunningPublishedVersion:
                        return edit + "\n运行：已安装的既有运行快照"
                            + "\n状态：○ 尚未核对为当前草稿";
                    case CharacterTestRuntimeAiLabBehaviorRecipeSyncState.Unsaved:
                        return edit + "\n运行：未装载\n状态：尚未保存";
                    default:
                        return edit + "\n运行：未装载\n状态：草稿已保存，但未运行"
                            + (!string.IsNullOrWhiteSpace(_lastBehaviorRecipeSaveRun.Message)
                                ? "\n原因：" + _lastBehaviorRecipeSaveRun.Message
                                : string.Empty);
                }
            }
        }

        internal bool SaveBehaviorRecipeAndRun(
            out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result)
        {
            return SaveBehaviorRecipeAndRun(-1, out result);
        }

        /// <summary>
        /// Queues the destructive runtime rebuild until the controller's next Update. FairyGUI invokes
        /// commands from an Input System callback, where synchronously rebuilding the GameSlice would
        /// dispose the InputActionState that is still dispatching the Save click.
        /// </summary>
        internal bool QueueBehaviorRecipeSaveAndRun(int selectedEntryIndex, out string message)
        {
            if (_behaviorRecipeSaveQueued || _behaviorRecipeSaveInProgress)
            {
                message = "行为配方正在保存，请等待当前保存完成。";
                _statusMessage = message;
                WriteBehaviorRecipeSaveRequest(selectedEntryIndex, "failed", message);
                return false;
            }
            bool selectedCardDirty = HasBehaviorRecipeChangesToSaveForEntry(selectedEntryIndex);
            if (!CanSaveBehaviorRecipeAndRun || !selectedCardDirty)
            {
                message = selectedCardDirty
                    ? "当前状态暂时不能保存行为配方。"
                    : "当前没有需要保存的修改。";
                _statusMessage = message;
                WriteBehaviorRecipeSaveRequest(selectedEntryIndex, "failed", message);
                return false;
            }
            if (!CanSaveBehaviorRecipeForEntry(selectedEntryIndex, out message))
            {
                _statusMessage = message;
                WriteBehaviorRecipeSaveRequest(selectedEntryIndex, "failed", message);
                return false;
            }

            _queuedBehaviorRecipeEntryIndex = selectedEntryIndex;
            _behaviorRecipeSaveQueued = true;
            message = "正在保存行为配方…";
            _statusMessage = message;
            WriteBehaviorRecipeSaveRequest(selectedEntryIndex, "queued", message);
            return true;
        }

        private void ProcessQueuedBehaviorRecipeSaveAndRun()
        {
            if (!_behaviorRecipeSaveQueued || _behaviorRecipeSaveInProgress)
                return;
            if (TryProcessQueuedTestCurrentChanges())
                return;

            int selectedEntryIndex = _queuedBehaviorRecipeEntryIndex;
            _queuedBehaviorRecipeEntryIndex = -1;
            _behaviorRecipeSaveQueued = false;
            SaveBehaviorRecipeAndRun(
                selectedEntryIndex,
                out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result);
            _statusMessage = result.Message;
        }

        /// <summary>Saves the draft, installs it from frame zero, and restores the prior runtime frame.</summary>
        /// <remarks>LowFreqAlloc: explicit Behavior Recipe authoring transaction only.</remarks>
        internal bool SaveBehaviorRecipeAndRun(
            int selectedEntryIndex,
            out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (_behaviorRecipeSaveInProgress)
            {
                result = CreateRejectedBehaviorRecipeSaveRunResult(
                    "SAVE_IN_PROGRESS",
                    "行为配方正在保存，请等待当前保存完成。");
                _statusMessage = result.Message;
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteSaveRun(result);
                return false;
            }
            if (!HasBehaviorRecipeChangesToSaveForEntry(selectedEntryIndex))
            {
                result = CreateRejectedBehaviorRecipeSaveRunResult(
                    "NO_UNSAVED_CHANGES",
                    "当前没有需要保存的修改。");
                _statusMessage = result.Message;
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteSaveRun(result);
                return false;
            }
            if (!CanSaveBehaviorRecipeForEntry(selectedEntryIndex, out string validationMessage))
            {
                result = CreateRejectedBehaviorRecipeSaveRunResult(
                    "DRAFT_VALIDATION_FAILED",
                    "未保存：" + validationMessage);
                _statusMessage = result.Message;
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteSaveRun(result);
                return false;
            }

            _behaviorRecipeSaveInProgress = true;
            try
            {
                return SaveBehaviorRecipeAndRunCore(selectedEntryIndex, out result);
            }
            finally
            {
                _behaviorRecipeSaveInProgress = false;
            }
        }

        private static void WriteBehaviorRecipeSaveRequest(
            int selectedEntryIndex,
            string result,
            string message)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                "action=save-behavior-logic-request"
                + " result=" + result
                + " entry=" + selectedEntryIndex
                + " message=" + message);
        }

        private bool SaveBehaviorRecipeAndRunCore(
            int selectedEntryIndex,
            out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result)
        {
            string operationId = Guid.NewGuid().ToString("N").Substring(0, 12);
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            bool restoreRunningFrame = _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && _timeline != null
                && _timeline.HasSelection;
            int frameBeforeSave = restoreRunningFrame ? _timeline.SelectedFrame : 0;
            bool pausedBeforeSave = restoreRunningFrame && (_context == null || _context.Paused);
            int workspaceRevision = workspace.CurrentRevision;
            string groupDraftId = string.Empty;
            string cardDraftId = string.Empty;
            long draftSemanticHash = 0L;
            int entityId = 0;
            int profileSlot = 0;
            long installedAuthorityHash = 0L;

            if (TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                && group != null)
            {
                groupDraftId = group.GroupDraftId;
                if (selectedEntryIndex >= 0 && selectedEntryIndex < group.Entries.Count)
                    cardDraftId = group.Entries[selectedEntryIndex]?.CardDraftId ?? string.Empty;
            }

            _statusMessage = "正在保存行为配方…";
            int previousExplicitSaveRevision = workspace.LastExplicitSaveRevision;
            workspace.LastExplicitSaveRevision = workspace.CurrentRevision;
            if (!FlushDraftWorkspace(out string saveMessage))
            {
                workspace.LastExplicitSaveRevision = previousExplicitSaveRevision;
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SaveFailed,
                    "save", "DRAFT_SAVE_FAILED",
                    string.IsNullOrWhiteSpace(saveMessage) ? "草稿保存失败。" : saveMessage,
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash, 0L,
                    entityId, profileSlot, installedAuthorityHash, out result);
            }
            SetBehaviorRecipeSaveCheckpoint(workspace, cardDraftId);

            workspaceRevision = workspace.CurrentRevision;
            _statusMessage = "正在校验行为配方…";
            if (!TryValidateSessionStartConfiguration(out string validationMessage))
            {
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedValidationFailed,
                    "validate", "SAVED_VALIDATION_FAILED",
                    "草稿已保存，但未运行：" + validationMessage,
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash, 0L,
                    entityId, profileSlot, installedAuthorityHash, out result);
            }

            _statusMessage = "正在编译行为配方…";
            BuildCandidateRevision();
            CharacterTestRuntimeAiLabCandidateRevision candidate = _candidateRevision;
            if (candidate?.Profile == null)
            {
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedCompileFailed,
                    "compile", "SAVED_COMPILE_FAILED",
                    "草稿已保存，但未运行：" + (string.IsNullOrWhiteSpace(_candidateDiagnostic)
                        ? "行为配方编译失败。"
                        : _candidateDiagnostic),
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash, 0L,
                    entityId, profileSlot, installedAuthorityHash, out result);
            }

            groupDraftId = candidate.GroupDraftId;
            CharacterTestRuntimeAiLabRevisionCardIdentity selectedIdentity =
                ResolveSaveRunCardIdentity(candidate, cardDraftId);
            if (selectedIdentity == null)
            {
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedCompileFailed,
                    "compile", "SELECTED_CARD_PROGRAM_MISSING",
                    "草稿已保存，但未运行：编译结果中缺少当前行为卡程序。",
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash,
                    candidate.AuthorityHash, entityId, profileSlot, installedAuthorityHash, out result);
            }
            cardDraftId = selectedIdentity.CardDraftId;
            draftSemanticHash = selectedIdentity.CandidateSemanticHash;

            CharacterTestArenaSlotConfig selectedSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            CharacterTestRuntimeAiLabDraftSlot draftSlot = workspace.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (selectedSlot == null
                || !selectedSlot.Enabled
                || draftSlot == null
                || (!HasStandaloneTacticalCard && !string.Equals(draftSlot.GroupDraftId, candidate.GroupDraftId, StringComparison.Ordinal))
                || (draftSlot.ArenaBehaviorGroupDraftId > 0
                    && selectedSlot.BehaviorGroupDraftId != draftSlot.ArenaBehaviorGroupDraftId))
            {
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedTargetUnavailable,
                    "target", "SAVED_TARGET_UNAVAILABLE",
                    "草稿已保存，但未运行：当前角色、行为组或测试槽位不再匹配。",
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash,
                    candidate.AuthorityHash, entityId, profileSlot, installedAuthorityHash, out result);
            }

            _statusMessage = "正在重建测试…";
            CharacterTestRuntimeAiLabShellCoordinator shell = _shellCoordinator;
            CharacterTestRuntimeAiLabShellState shellState = shell?.CurrentState;
            bool useShellTransaction = shellState != null
                && shellState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Visible
                && shellState.PresentationMode == CharacterTestRuntimeAiLabPresentationMode.Workspace
                && shellState.PageContext.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic
                && (shellState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    || shellState.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
            SessionStartRecoverySnapshot recovery = useShellTransaction
                ? CaptureSessionStartRecoverySnapshot()
                : null;
            bool sessionStarted = false;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteVisibility(
                "save-run",
                "before-session-rebuild",
                operationId,
                shellState,
                null,
                null,
                null,
                null,
                null,
                useShellTransaction ? "shell-transaction" : "direct",
                string.Empty);
            Func<bool> rebuildAndStart = () =>
            {
                if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup && !ResetSession())
                    return false;

                _activeRevision = null;
                _deferBaselineActiveCommit = true;
                try
                {
                    sessionStarted = StartSession(
                        CharacterTestRuntimeAiLabSessionStartPresentation.InspectFirstFrame);
                    return sessionStarted;
                }
                finally
                {
                    _deferBaselineActiveCommit = false;
                }
            };
            if (useShellTransaction)
            {
                CharacterTestRuntimeAiLabNavigationResult navigation = shell.BeginSessionStart(
                    shellState.Generation,
                    shellState.Revision,
                    shellState.ActiveWorkspace,
                    shellState.ActiveWorkspace,
                    recovery,
                    rebuildAndStart,
                    RecoverFailedSessionStart,
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic);
                sessionStarted = navigation.Success
                    && shell.CurrentState.LastCompletedOutcome ==
                        CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded
                    && shell.CurrentState.Visibility ==
                        CharacterTestRuntimeAiLabShellVisibility.Visible
                    && shell.CurrentState.PresentationMode ==
                        CharacterTestRuntimeAiLabPresentationMode.Workspace;
                if (!sessionStarted && !string.IsNullOrWhiteSpace(navigation.Message))
                    _statusMessage = navigation.Message;
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteVisibility(
                    "save-run",
                    "after-session-rebuild",
                    operationId,
                    shell.CurrentState,
                    null,
                    null,
                    null,
                    null,
                    null,
                    sessionStarted ? "mounted" : "failed",
                    navigation.Code + ": " + navigation.Message);
            }
            else
            {
                sessionStarted = rebuildAndStart();
            }
            if (!sessionStarted)
            {
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedRuntimeInstallFailed,
                    "install", "RUNTIME_INSTALL_FAILED", "草稿已保存，但未运行：" + _statusMessage,
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash,
                    candidate.AuthorityHash, entityId, profileSlot, installedAuthorityHash, out result);
            }

            _statusMessage = "正在核对运行版本…";
            if (!TryVerifyBehaviorRecipeRuntimeIdentity(
                    candidate, selectedIdentity, out entityId, out profileSlot,
                    out installedAuthorityHash, out string identityDiagnostic))
            {
                SetPaused(true);
                return FinishBehaviorRecipeSaveRun(
                    operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedRuntimeIdentityMismatch,
                    "identity", "RUNTIME_IDENTITY_MISMATCH",
                    "草稿已保存，但未运行：" + identityDiagnostic,
                    workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash,
                    candidate.AuthorityHash, entityId, profileSlot, installedAuthorityHash, out result);
            }

            _activeRevision = candidate;
            workspace.LastAppliedRevision = candidate.Revision;
            _runningBehaviorRecipeWorkspaceRevision = workspaceRevision;
            DisableDebugEditGate();
            if (restoreRunningFrame && !TryRecomputeSavedBehaviorRecipeToFrame(frameBeforeSave))
            {
                SetPaused(true);
                return FinishBehaviorRecipeSaveRun(
                    operationId,
                    CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedRuntimeInstallFailed,
                    "recompute",
                    "RUNTIME_RECOMPUTE_FAILED",
                    "草稿已保存并装载，但未能重新模拟到保存前的帧 " + frameBeforeSave + "。",
                    workspaceRevision,
                    groupDraftId,
                    cardDraftId,
                    draftSemanticHash,
                    candidate.AuthorityHash,
                    entityId,
                    profileSlot,
                    installedAuthorityHash,
                    out result);
            }

            SetPaused(pausedBeforeSave);
            _timeline.SetMode(pausedBeforeSave
                ? CharacterTestRuntimeAiPlannerDevTimelineMode.Paused
                : CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
            _timeline.SelectLatest();
            _gameManager?.RefreshPresentationForRuntimeAiPlannerDev();
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteVisibility(
                "save-run",
                "completed",
                operationId,
                shell?.CurrentState,
                null,
                null,
                null,
                null,
                null,
                "saved-and-running",
                string.Empty);
            int restoredFrame = _timeline.HasSelection ? _timeline.SelectedFrame : 0;
            return FinishBehaviorRecipeSaveRun(
                operationId, CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedAndRunning,
                "running", string.Empty, restoreRunningFrame
                    ? "已保存，并已从第 0 帧重新模拟到保存前的帧 " + restoredFrame + "。"
                    : "已保存，并已装载到首个可见帧。",
                workspaceRevision, groupDraftId, cardDraftId, draftSemanticHash,
                candidate.AuthorityHash, entityId, profileSlot, installedAuthorityHash, out result);
        }

        private bool TryRecomputeSavedBehaviorRecipeToFrame(int targetFrame)
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || _context?.Slice == null
                || _timeline == null)
            {
                return false;
            }

            int currentFrame = (int)Math.Min(int.MaxValue, _context.Slice.CurrentFrame.Value);
            if (targetFrame <= currentFrame)
                return true;

            _statusMessage = "正在从第 0 帧重新模拟到 " + targetFrame + "…";
            while (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && currentFrame < targetFrame)
            {
                if (!AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy.Deferred))
                    return false;
                currentFrame = (int)Math.Min(int.MaxValue, _context.Slice.CurrentFrame.Value);
            }

            return currentFrame == targetFrame;
        }

        private CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult
            CreateRejectedBehaviorRecipeSaveRunResult(string failureCode, string message)
        {
            return new CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SaveFailed,
                "save",
                failureCode,
                message,
                DraftWorkspace.CurrentRevision,
                string.Empty,
                string.Empty,
                0L,
                0L,
                0,
                0,
                0L);
        }

        /// <summary>
        /// Starts a new explicit-save epoch for the active workspace. The revision currently visible when
        /// a workspace becomes active is the editing baseline; only changes made after that point enable Save.
        /// </summary>
        private void ResetBehaviorRecipeSaveCheckpoint(CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            _behaviorRecipeSaveCheckpointWorkspaceId = workspace?.WorkspaceId ?? string.Empty;
            _behaviorCardSaveCheckpointRevisions.Clear();
            _behaviorGroupSaveCheckpointRevisions.Clear();
            if (workspace == null)
                return;
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
            {
                if (group != null)
                    _behaviorGroupSaveCheckpointRevisions[group.GroupDraftId] = group.CurrentRevision;
            }
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in workspace.Cards.Values)
            {
                if (card != null)
                    _behaviorCardSaveCheckpointRevisions[BehaviorCardSaveIdentity(card)] = card.CurrentRevision;
            }
        }

        private void EnsureBehaviorRecipeSaveCheckpoint(CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            if (workspace == null)
                return;
            if (!string.Equals(
                    _behaviorRecipeSaveCheckpointWorkspaceId,
                    workspace.WorkspaceId,
                    StringComparison.Ordinal))
            {
                ResetBehaviorRecipeSaveCheckpoint(workspace);
            }
        }

        private void SetBehaviorRecipeSaveCheckpoint(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string cardDraftId)
        {
            _behaviorRecipeSaveCheckpointWorkspaceId = workspace?.WorkspaceId ?? string.Empty;
            if (workspace == null)
                return;
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            if (!string.IsNullOrWhiteSpace(cardDraftId))
                workspace.Cards.TryGetValue(cardDraftId, out card);
            if (card == null)
            {
                foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in workspace.Cards.Values)
                {
                    if (candidate == null
                        || (_behaviorCardSaveCheckpointRevisions.TryGetValue(
                                BehaviorCardSaveIdentity(candidate),
                                out int savedRevision)
                            && candidate.CurrentRevision == savedRevision))
                    {
                        continue;
                    }
                    if (card != null)
                        return;
                    card = candidate;
                }
            }
            if (card == null)
            {
                return;
            }
            _behaviorCardSaveCheckpointRevisions[BehaviorCardSaveIdentity(card)] = card.CurrentRevision;
            workspace.LastExplicitSaveRevision = workspace.CurrentRevision;
        }

        private void SetBehaviorGroupSaveCheckpoint(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            string groupDraftId)
        {
            _behaviorRecipeSaveCheckpointWorkspaceId = workspace?.WorkspaceId ?? string.Empty;
            if (workspace == null
                || string.IsNullOrWhiteSpace(groupDraftId)
                || !TryGetAuthoringGroup(
                    groupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                return;
            }
            _behaviorGroupSaveCheckpointRevisions[group.GroupDraftId] = group.CurrentRevision;
        }

        internal bool HasBehaviorGroupChangesToSave(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            if (group == null)
                return false;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            EnsureBehaviorRecipeSaveCheckpoint(workspace);
            return !_behaviorGroupSaveCheckpointRevisions.TryGetValue(
                    group.GroupDraftId,
                    out int savedRevision)
                ? group.CurrentRevision > 1
                : group.CurrentRevision != savedRevision;
        }

        internal bool HasBehaviorRecipeChangesToSaveForEntry(int selectedEntryIndex)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            EnsureBehaviorRecipeSaveCheckpoint(workspace);
            if (!TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || selectedEntryIndex < 0
                || selectedEntryIndex >= group.Entries.Count
                || !workspace.Cards.TryGetValue(
                    group.Entries[selectedEntryIndex].CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card))
            {
                return HasBehaviorRecipeChangesToSave;
            }
            return !_behaviorCardSaveCheckpointRevisions.TryGetValue(
                    BehaviorCardSaveIdentity(card),
                    out int savedRevision)
                ? card.CurrentRevision > 1
                : card.CurrentRevision != savedRevision;
        }

        private static string BehaviorCardSaveIdentity(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            return !string.IsNullOrWhiteSpace(card?.PublishedStableId)
                ? card.PublishedStableId
                : "draft:" + (card?.CardDraftId ?? string.Empty);
        }

        private bool TryVerifyBehaviorRecipeRuntimeIdentity(
            CharacterTestRuntimeAiLabCandidateRevision candidate,
            CharacterTestRuntimeAiLabRevisionCardIdentity selectedIdentity,
            out int entityId,
            out int profileSlot,
            out long installedAuthorityHash,
            out string diagnostic)
        {
            entityId = 0;
            profileSlot = ResolveSelectedRuntimeProfileSlot();
            installedAuthorityHash = 0L;
            CharacterTestRuntimeAiPlannerSnapshot planner =
                _context?.Slice?.RuntimeAiPlannerCommands?.CreateSnapshot();
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = planner?.Agents;
            if (agents == null || agents.Count == 0
                || _selectedAgentIndex < 0 || _selectedAgentIndex >= agents.Count)
            {
                diagnostic = "所选测试角色不存在。";
                return false;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent = agents[_selectedAgentIndex];
            entityId = selectedAgent.EntityId;
            profileSlot = selectedAgent.RuntimeAiPlannerProfileSlot;
            var actualRuntime = _context?.Slice?.CharacterRuntime;
            var actualEntity = actualRuntime?.FindEntityById(entityId);
            foreach (var program in candidate.LogicPrograms)
                if (program?.TacticalSource != null && !program.TacticalSource.ValidateBinding(actualRuntime, actualEntity, out diagnostic)) return false;
            CharacterTestRuntimeAiPlannerStateSnapshot state = CaptureRuntimeAiPlannerStateForLab();
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot logicSlot = null;
            for (int i = 0; state?.BehaviorLogicSlots != null && i < state.BehaviorLogicSlots.Length; i++)
            {
                if (state.BehaviorLogicSlots[i]?.Slot == profileSlot)
                {
                    logicSlot = state.BehaviorLogicSlots[i];
                    break;
                }
            }
            if (logicSlot == null)
            {
                diagnostic = "目标槽位缺少行为逻辑 Overlay。";
                return false;
            }
            installedAuthorityHash = logicSlot.AuthorityHash;
            if (installedAuthorityHash != candidate.AuthorityHash)
            {
                diagnostic = "运行 authority hash 与刚保存的草稿快照不一致。";
                return false;
            }

            bool entityAttached = false;
            for (int i = 0; logicSlot.Agents != null && i < logicSlot.Agents.Length; i++)
            {
                if (logicSlot.Agents[i]?.EntityId == entityId)
                {
                    entityAttached = true;
                    break;
                }
            }
            if (!entityAttached)
            {
                diagnostic = "运行 Overlay 未绑定到当前选择的测试角色。";
                return false;
            }
            if (selectedIdentity == null || selectedIdentity.CandidateSemanticHash == 0L)
            {
                diagnostic = "刚保存的 candidate 不包含当前行为卡程序。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        private static CharacterTestRuntimeAiLabRevisionCardIdentity ResolveSaveRunCardIdentity(
            CharacterTestRuntimeAiLabCandidateRevision candidate,
            string cardDraftId)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabRevisionCardIdentity> identities = candidate?.CardIdentities;
            CharacterTestRuntimeAiLabRevisionCardIdentity first = null;
            for (int i = 0; identities != null && i < identities.Count; i++)
            {
                CharacterTestRuntimeAiLabRevisionCardIdentity identity = identities[i];
                if (first == null)
                    first = identity;
                if (identity != null
                    && cardDraftId.Length > 0
                    && string.Equals(identity.CardDraftId, cardDraftId, StringComparison.Ordinal))
                {
                    return identity;
                }
            }
            return cardDraftId.Length == 0 ? first : null;
        }

        private bool FinishBehaviorRecipeSaveRun(
            string operationId,
            CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus status,
            string stage,
            string failureCode,
            string message,
            int workspaceRevision,
            string groupDraftId,
            string cardDraftId,
            long draftSemanticHash,
            long candidateAuthorityHash,
            int selectedEntityId,
            int runtimeProfileSlot,
            long installedAuthorityHash,
            out CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult result)
        {
            result = new CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult(
                operationId, status, stage, failureCode, message, workspaceRevision,
                groupDraftId, cardDraftId, draftSemanticHash, candidateAuthorityHash,
                selectedEntityId, runtimeProfileSlot, installedAuthorityHash);
            _lastBehaviorRecipeSaveRun = result;
            if (result.Success)
                _lastSuccessfulBehaviorRecipeSaveRun = result;
            _statusMessage = message;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteSaveRun(result);
            return result.Success;
        }
    }
}
