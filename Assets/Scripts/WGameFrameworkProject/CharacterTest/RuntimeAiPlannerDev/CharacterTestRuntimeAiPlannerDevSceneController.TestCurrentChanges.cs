using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabTestCurrentChangesRequest? _singleCardTestRequest;
        private string _playAfterTestOperationId;
        private readonly CharacterTestRuntimeAiLabTestCurrentChangesQueue
            _testCurrentChangesQueue = new CharacterTestRuntimeAiLabTestCurrentChangesQueue();
        private readonly CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker
            _testCurrentChangesTracker =
                new CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker();

        internal CharacterTestRuntimeAiLabTestCurrentChangesOperationState
            TestCurrentChangesOperationState => _testCurrentChangesTracker.State;
        internal CharacterTestRuntimeAiLabTestCurrentChangesResult LastTestCurrentChangesResult =>
            _testCurrentChangesTracker.LastResult;
        internal bool TestCurrentChangesQueued => _testCurrentChangesQueue.HasPending;
        internal bool TestCurrentChangesInProgress => _testCurrentChangesTracker.IsBusy
            && !_testCurrentChangesQueue.HasPending;
        internal string LastTestRestartDiagnostic { get; private set; } = string.Empty;
        internal string LastTestRecoveryDiagnostic { get; private set; } = string.Empty;

        internal CharacterTestRuntimeAiLabCurrentTestSource CurrentTestSource
        {
            get
            {
                CharacterTestRuntimeAiLabDraftWorkspace workspace = _draftWorkspace;
                return _testCurrentChangesTracker.ResolveCurrentTestSource(
                    _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                        || _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed,
                    _activeRevision?.AuthorityHash ?? 0L,
                    workspace?.WorkspaceId ?? string.Empty,
                    workspace?.CurrentRevision ?? 0,
                    _candidateRevision?.AuthorityHash ?? 0L);
            }
        }

        internal string CurrentTestSourceStatusText
        {
            get
            {
                if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
                {
                    switch (CurrentTestSource)
                    {
                        case CharacterTestRuntimeAiLabCurrentTestSource.PublishedVersion:
                            return "当前测试：测试已完成（正式版本）";
                        case CharacterTestRuntimeAiLabCurrentTestSource.UsingCurrentChanges:
                            return "当前测试：测试已完成（本次修改）";
                        case CharacterTestRuntimeAiLabCurrentTestSource.CurrentChangesUpdated:
                            return "当前测试：当前修改已更新，请重新测试";
                        default:
                            return "当前测试：测试已完成";
                    }
                }
                switch (CurrentTestSource)
                {
                    case CharacterTestRuntimeAiLabCurrentTestSource.PublishedVersion:
                        return "当前测试：正式版本";
                    case CharacterTestRuntimeAiLabCurrentTestSource.UsingCurrentChanges:
                        return _activeRevision?.IsSingleCardTest == true
                            ? "当前测试：选中角色仅使用此卡；其他角色按配置"
                            : "当前测试：正在使用本次修改";
                    case CharacterTestRuntimeAiLabCurrentTestSource.CurrentChangesUpdated:
                        return "当前测试：当前修改已更新，请重新测试";
                    case CharacterTestRuntimeAiLabCurrentTestSource.Unknown:
                        return "当前测试：来源未知";
                    default:
                        return "当前测试：尚未开始测试";
                }
            }
        }

        internal string TestCurrentChangesOperationStatusText
        {
            get
            {
                switch (_testCurrentChangesTracker.State)
                {
                    case CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Saving:
                        return "正在保存当前 Workspace 草稿…";
                    case CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Validating:
                        return "正在校验当前修改并构建 Candidate…";
                    case CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Restarting:
                        return "正在从第 0 帧重新开始测试…";
                    case CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Succeeded:
                    case CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Failed:
                        return _testCurrentChangesTracker.LastResult.Message;
                    default:
                        return "可以测试当前 Workspace 的修改。";
                }
            }
        }

        internal bool CanTestCurrentChanges(int selectedEntryIndex, out string diagnostic)
        {
#if !UNITY_EDITOR
            diagnostic = "Player 构建不写入 Runtime AI Lab Workspace 草稿。";
            return false;
#else
            // Authored sources now use the same save-first transaction. Fresh compilation and
            // actual-actor binding checks run after Flush and before installing the new baseline.
            if (_testCurrentChangesTracker.LastResult.RequiresRecovery)
            {
                diagnostic = "上一次重启失败且旧测试未能恢复；请先重置实验。";
                return false;
            }
            if (_behaviorRecipeSaveQueued
                || _behaviorRecipeSaveInProgress
                || _testCurrentChangesQueue.HasPending
                || _testCurrentChangesTracker.IsBusy)
            {
                diagnostic = "测试当前修改正在执行，请等待本次操作完成。";
                return false;
            }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                && _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
            {
                diagnostic = "当前会话状态不能测试 Workspace 修改。";
                return false;
            }
            if (IsHistoricalFrameSelected)
            {
                diagnostic = "历史 RuntimeFrame 为只读；请先返回当前帧。";
                return false;
            }
            if (_seekInProgress || _loopPlaybackActive || _historyPlaybackActive)
            {
                diagnostic = "回放、跳转或循环播放结束后才能测试当前修改。";
                return false;
            }
            if (_candidateApplyQueued || _branchConfirmationPending)
            {
                diagnostic = "已有 Candidate 或分支操作待处理，不能重复重启测试。";
                return false;
            }
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out diagnostic))
            {
                return false;
            }
            if (workspace.IsReadOnly)
            {
                diagnostic = "当前 Workspace 为只读，不能保存后测试。";
                return false;
            }
            if (!TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                diagnostic = "当前角色槽位没有可测试的行为组。";
                return false;
            }
            if (selectedEntryIndex < 0 || selectedEntryIndex >= group.Entries.Count)
            {
                diagnostic = "当前没有可测试的行为卡。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[selectedEntryIndex];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.EntryId)
                || string.IsNullOrWhiteSpace(entry.CardDraftId)
                || !workspace.Cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || card == null)
            {
                diagnostic = "当前行为卡缺少稳定 Draft 身份。";
                return false;
            }
            diagnostic = string.Empty;
            return true;
#endif
        }

        /// <summary>
        /// Queues one save/validate/restart transaction for the next Update. Clean workspaces are
        /// intentionally allowed so the command never becomes a silent no-op.
        /// </summary>
        internal bool QueueTestCurrentChanges(int selectedEntryIndex, out string message, bool playAfterStart = false)
        {
#if !UNITY_EDITOR
            message = "Player 构建不支持写入并测试 Workspace 草稿。";
            _statusMessage = message;
            LogTestStartFailure("test-current-changes", "UnsupportedPlayer", message);
            return false;
#else
            if (HasStandaloneTacticalCard && !IsConfiguredTestCardEditing)
            {
                ShowTestCardDialog?.Invoke("draft:" + TacticalCardContext.CardId, true);
                message = "请先将当前配置保存为测试单卡。"; _statusMessage = message; return false;
            }
            // A disabled/re-enabled component clears the shared SaveRun flag in OnDisable. Do not
            // let a stale process-local queue marker survive that cancellation boundary.
            if (_testCurrentChangesQueue.HasPending && !_behaviorRecipeSaveQueued)
                _testCurrentChangesQueue.Cancel();
            if (HasStandaloneTacticalCard && !TacticalCardTestMode
                && _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                if (!BeginTacticalCardTest(out _, out message))
                { _statusMessage = message; return false; }
                SelectTacticalTestCard();
            }
            if (TryDeferTacticalPlayForSummary(selectedEntryIndex, playAfterStart, out message))
                return true;
            if (TacticalCardTestMode && !ValidateTacticalCardPlay(out message))
            { _statusMessage = message; return false; }
            if (TacticalCardTestMode) playAfterStart = true;
            if (!CanTestCurrentChanges(selectedEntryIndex, out message))
            {
                _statusMessage = message;
                LogTestStartFailure("test-current-changes", "RequestValidation", message);
                return false;
            }
            try
            {
                if (!TryGetSelectedAuthoringGroup(out var identityGroup))
                    throw new InvalidOperationException("The selected authoring group is unavailable.");
                CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(DraftWorkspace, identityGroup);
            }
            catch (Exception error) when (error is InvalidOperationException || error is System.IO.IOException)
            {
                message = "制作数字身份准备失败：" + error.Message;
                _statusMessage = message;
                return false;
            }
            if (!TryCaptureTestCurrentChangesRequest(
                    selectedEntryIndex,
                    out CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
                    out message))
            {
                _statusMessage = message;
                LogTestStartFailure("test-current-changes", "CaptureRequest", message);
                return false;
            }
            if (TacticalCardTestMode && _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                bool played = PlayNextTacticalCard(request, out message);
                _statusMessage = message;
                return played;
            }
            if (!_testCurrentChangesQueue.TryEnqueue(request, out message)
                || !_testCurrentChangesTracker.TryBegin(request, out message))
            {
                _testCurrentChangesQueue.Cancel();
                _statusMessage = message;
                LogTestStartFailure(request.OperationId, "QueueRequest", message, request.CardDraftId);
                return false;
            }

            _queuedBehaviorRecipeEntryIndex = selectedEntryIndex;
            _playAfterTestOperationId = playAfterStart ? request.OperationId : null;
            _behaviorRecipeSaveQueued = true;
            message = DraftWorkspace.Cards.TryGetValue(request.CardDraftId, out var requestedCard) && requestedCard.HasAuthoredSource
                ? "正在保存草稿并准备当前角色的单卡测试；其他角色保持原配置…"
                : "正在保存当前 Workspace 草稿…";
            _statusMessage = message;
            CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                "action=test-current-changes-request"
                + " result=queued"
                + " operation=" + request.OperationId
                + " workspace=" + request.WorkspaceId
                + " group=" + request.GroupDraftId
                + " card=" + request.CardDraftId);
            return true;
#endif
        }

        /// <summary>
        /// Called by the existing delayed SaveRun queue before its compatibility save handler.
        /// </summary>
        private bool TryProcessQueuedTestCurrentChanges()
        {
            using var startupProfile = CharacterTestStartupProfiler.TestCurrentChanges.Auto();
            if (!_testCurrentChangesQueue.TryTake(
                    out CharacterTestRuntimeAiLabTestCurrentChangesRequest request))
            {
                return false;
            }

            _queuedBehaviorRecipeEntryIndex = -1;
            bool playAfterStart = string.Equals(_playAfterTestOperationId, request.OperationId, StringComparison.Ordinal);
            _playAfterTestOperationId = null;
            _behaviorRecipeSaveQueued = false;
            _behaviorRecipeSaveInProgress = true;
            var previousSingleCardTest = _singleCardTestRequest;
            try
            {
                CharacterTestRuntimeAiLabDraftWorkspace workspace = _draftWorkspace;
                if (workspace != null && workspace.Cards.TryGetValue(request.CardDraftId, out var testingCard) && testingCard.HasAuthoredSource)
                    _singleCardTestRequest = request;
                int previousExplicitSaveRevision = workspace?.LastExplicitSaveRevision ?? 0;

                bool Flush(out string diagnostic)
                {
                    if (workspace == null
                        || !string.Equals(
                            workspace.WorkspaceId,
                            request.WorkspaceId,
                            StringComparison.Ordinal))
                    {
                        diagnostic = "排队后当前 Workspace 已变化。";
                        return false;
                    }

                    workspace.LastExplicitSaveRevision = workspace.CurrentRevision;
                    if (!FlushDraftWorkspace(out diagnostic))
                    {
                        workspace.LastExplicitSaveRevision = previousExplicitSaveRevision;
                        return false;
                    }
                    SetBehaviorRecipeSaveCheckpoint(workspace, request.CardDraftId);
                    SetBehaviorGroupSaveCheckpoint(workspace, request.GroupDraftId);
                    return true;
                }

                CharacterTestRuntimeAiLabTestCurrentChangesResult result =
                    CharacterTestRuntimeAiLabTestCurrentChangesTransaction.Execute(
                        _testCurrentChangesTracker,
                        Flush,
                        markRestarting => ApplyBehaviorTestAndRerunCore(
                            request,
                            markRestarting,
                            preserveShellContext: true,
                            verifySelectedCardIdentity: true));
                RecordTestCurrentChangesAsCompatibilitySaveRun(result);
                _statusMessage = result.Message;
                if (playAfterStart && result.Success && result.ContextRestored)
                {
                    if (TacticalCardTestMode) MarkTacticalCardPlayed(request.CardDraftId);
                    if (!TogglePause())
                        UnityEngine.Debug.LogError("[Runtime AI Lab][TestPlaybackFailed] operation=" + result.OperationId + " reason=" + _statusMessage);
                }
                if (!result.Success)
                    LogTestStartFailure(result.OperationId, result.FailureStage + "/" + result.FailureCode, result.Message, result.CardDraftId);
                else if (!result.ContextRestored)
                    UnityEngine.Debug.LogError("[Runtime AI Lab][TestPageRestoreFailed] operation=" + result.OperationId
                        + " card=" + result.CardDraftId + "\n" + result.Message, this);
                return true;
            }
            finally
            {
                _singleCardTestRequest = previousSingleCardTest;
                _behaviorRecipeSaveInProgress = false;
            }
        }

        private bool TryCaptureTestCurrentChangesRequest(
            int selectedEntryIndex,
            out CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
            out string diagnostic)
        {
            request = default;
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out diagnostic)
                || !TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                    diagnostic = "当前 Workspace 行为组不可用。";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection identity =
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.BuildProjection(
                    this,
                    group,
                    selectedEntryIndex,
                    0L,
                    runtime: null,
                    default(CharacterTestRuntimeAiPlannerAgentSnapshot),
                    hasSelectedAgent: false);
            if (identity.EditResolutionStatus
                    != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved
                || identity.EditCard == null
                || identity.EditCard.DraftEntryId.Length == 0
                || identity.EditCard.CardDraftId.Length == 0)
            {
                diagnostic = string.IsNullOrWhiteSpace(identity.EditDiagnostic)
                    ? "当前行为卡身份无法解析。"
                    : identity.EditDiagnostic;
                return false;
            }

            CharacterTestRuntimeAiLabPageContext page =
                _shellCoordinator?.CurrentState?.PageContext;
            if (page == null
                || !string.Equals(page.GroupDraftId, group.GroupDraftId, StringComparison.Ordinal)
                || !string.Equals(
                    page.CardDraftId,
                    identity.EditCard.CardDraftId,
                    StringComparison.Ordinal))
            {
                TryCaptureBehaviorCardShellContext(
                    selectedEntryIndex,
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    0,
                    BehaviorCardPresentationViewport,
                    out page);
            }

            request = new CharacterTestRuntimeAiLabTestCurrentChangesRequest(
                Guid.NewGuid().ToString("N").Substring(0, 12),
                workspace.WorkspaceId,
                workspace.CurrentRevision,
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                group.GroupDraftId,
                identity.EditCard.DraftEntryId,
                identity.EditCard.CardDraftId,
                identity.EditCard.PublishedStableId,
                page);
            diagnostic = request.IsValid
                ? string.Empty
                : "测试当前修改请求缺少稳定身份。";
            return request.IsValid;
        }

        private CharacterTestRuntimeAiLabTestCurrentChangesRequest
            CreateCompatibilityBehaviorTestRequest()
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            CharacterTestRuntimeAiLabBehaviorGroupDraft group = null;
            TryGetSelectedAuthoringGroup(out group);
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry =
                group != null && group.Entries.Count > 0 ? group.Entries[0] : null;
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            if (entry != null)
                workspace?.Cards.TryGetValue(entry.CardDraftId, out card);
            return new CharacterTestRuntimeAiLabTestCurrentChangesRequest(
                "compatibility",
                workspace?.WorkspaceId ?? "unavailable",
                Math.Max(1, workspace?.CurrentRevision ?? 0),
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                group?.GroupDraftId ?? "unavailable",
                entry?.EntryId ?? "unavailable",
                card?.CardDraftId ?? entry?.CardDraftId ?? "unavailable",
                card?.PublishedStableId ?? string.Empty,
                _shellCoordinator?.CurrentState?.PageContext);
        }

        /// <summary>
        /// Shared implementation for the compatibility ApplyDraft flow and UX-05. Candidate and
        /// session algorithms remain owned by the controller; the command sink only queues intent.
        /// </summary>
        private CharacterTestRuntimeAiLabTestCurrentChangesApplyResult
            ApplyBehaviorTestAndRerunCore(
                CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
                Action markRestarting,
                bool preserveShellContext,
                bool verifySelectedCardIdentity)
        {
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out string workspaceDiagnostic)
                || !string.Equals(
                    workspace.WorkspaceId,
                    request.WorkspaceId,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Target,
                    "WORKSPACE_CHANGED",
                    "当前 Workspace 已变化，当前测试没有改变。 " + workspaceDiagnostic);
            }

            BuildCandidateRevision();
            CharacterTestRuntimeAiLabCandidateRevision candidate = _candidateRevision;
            if (candidate?.Profile == null)
            {
                int count = CountTestCurrentChangesBlockingDiagnostics(request.GroupDraftId);
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Candidate,
                    "CANDIDATE_BUILD_FAILED",
                    "Candidate 构建失败，当前测试没有改变。 "
                        + (string.IsNullOrWhiteSpace(_candidateDiagnostic)
                            ? "行为配方无法编译。"
                            : _candidateDiagnostic),
                    Math.Max(1, count));
            }
            if (!TryValidateSessionStartConfiguration(out string validationDiagnostic))
            {
                int count = CountTestCurrentChangesBlockingDiagnostics(request.GroupDraftId);
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Validation,
                    "TEST_VALIDATION_FAILED",
                    "当前修改校验失败，当前测试没有改变。 " + validationDiagnostic,
                    Math.Max(1, count));
            }
            if (!string.Equals(
                    candidate.GroupDraftId,
                    request.GroupDraftId,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Target,
                    "GROUP_IDENTITY_CHANGED",
                    "当前行为组在排队后已变化，当前测试没有改变。");
            }

            CharacterTestRuntimeAiLabRevisionCardIdentity selectedIdentity =
                ResolveSaveRunCardIdentity(candidate, request.CardDraftId);
            if (verifySelectedCardIdentity && selectedIdentity == null)
            {
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Candidate,
                    "SELECTED_CARD_PROGRAM_MISSING",
                    "Candidate 中缺少当前行为卡程序，当前测试没有改变。",
                    1);
            }
            if (_matchConfig?.TryGetSlot(request.SetupTeamId, request.SetupSlotIndex) == null)
            {
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Target,
                    "TEST_TARGET_UNAVAILABLE",
                    "原测试角色槽位已不存在，当前测试没有改变。");
            }

            bool publishedContentPristine = IsTestCurrentChangesPublishedContentPristine(
                workspace,
                request);
            bool hadPriorRunningSession =
                _phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || _activeRevision != null;
            int previousTimelineSessionGeneration = _timelineSessionGeneration;
            int selectedEntityId = 0;
            int runtimeProfileSlot = 0;
            long installedAuthorityHash = 0L;
            int firstVisibleFrame = 0;
            string restartFailureCode = string.Empty;
            string restartFailureMessage = string.Empty;
            bool restartedFromFrameZeroBaseline = false;

            bool RestartAndVerify()
            {
                if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                    && !ResetSession())
                {
                    restartFailureCode = "TEST_RESET_FAILED";
                    restartFailureMessage = _statusMessage;
                    return false;
                }
                // ResetSession intentionally chooses a default UI slot. The UX-05 request restores
                // its stable setup identity before StartSession builds the new baseline Agent.
                _selectedSetupTeamId = request.SetupTeamId;
                _selectedSetupSlotIndex = request.SetupSlotIndex;
                _activeRevision = null;
                _deferBaselineActiveCommit = true;
                bool started;
                try
                {
                    started = StartSession(
                        CharacterTestRuntimeAiLabSessionStartPresentation.InspectFirstFrame);
                }
                finally
                {
                    _deferBaselineActiveCommit = false;
                }
                if (!started)
                {
                    restartFailureCode = "TEST_START_FAILED";
                    restartFailureMessage = _statusMessage;
                    return false;
                }

                CharacterTestRuntimeAiLabCandidateRevision runningCandidate = _candidateRevision;
                if (runningCandidate?.Profile == null
                    || runningCandidate.AuthorityHash != candidate.AuthorityHash
                    || !string.Equals(
                        runningCandidate.GroupDraftId,
                        candidate.GroupDraftId,
                        StringComparison.Ordinal))
                {
                    restartFailureCode = "RUNTIME_CANDIDATE_CHANGED";
                    restartFailureMessage =
                        "重启期间 Candidate 身份发生变化，未确认当前测试来源。";
                    return false;
                }

                CharacterTestRuntimeAiLabRevisionCardIdentity runningIdentity =
                    ResolveSaveRunCardIdentity(runningCandidate, request.CardDraftId);
                if (verifySelectedCardIdentity
                    && !TryVerifyBehaviorRecipeRuntimeIdentity(
                        runningCandidate,
                        runningIdentity,
                        out selectedEntityId,
                        out runtimeProfileSlot,
                        out installedAuthorityHash,
                        out string identityDiagnostic))
                {
                    restartFailureCode = "RUNTIME_IDENTITY_MISMATCH";
                    restartFailureMessage = identityDiagnostic;
                    return false;
                }

                _activeRevision = runningCandidate;
                workspace.LastAppliedRevision = runningCandidate.Revision;
                _runningBehaviorRecipeWorkspaceRevision = workspace.CurrentRevision;
                DisableDebugEditGate();
                SetPaused(true);
                _timeline?.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
                _timeline?.SelectLatest();
                _gameManager?.RefreshPresentationForRuntimeAiPlannerDev();
                firstVisibleFrame = _timeline != null && _timeline.HasSelection
                    ? _timeline.SelectedFrame
                    : 0;
                restartedFromFrameZeroBaseline = _timelineSessionStarted
                    && _timelineSessionGeneration != previousTimelineSessionGeneration;
                if (!restartedFromFrameZeroBaseline)
                {
                    restartFailureCode = "SESSION_BASELINE_NOT_RECREATED";
                    restartFailureMessage =
                        "新测试没有建立独立的第 0 帧 authority 基线。";
                    return false;
                }
                return true;
            }

            markRestarting?.Invoke();
            CharacterTestRuntimeAiLabShellCoordinator shell = _shellCoordinator;
            CharacterTestRuntimeAiLabShellState shellState = shell?.CurrentState;
            bool useShellTransaction = preserveShellContext
                && shellState != null
                && shellState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Visible
                && shellState.PresentationMode == CharacterTestRuntimeAiLabPresentationMode.Workspace;
            SessionStartRecoverySnapshot recovery = CaptureSessionStartRecoverySnapshot();
            bool restarted;
            bool previousSessionRecovered = false;
            string recoveryDiagnostic = string.Empty;
            if (useShellTransaction)
            {
                CharacterTestRuntimeAiLabNavigationResult navigation = shell.BeginSessionStart(
                    shellState.Generation,
                    shellState.Revision,
                    shellState.ActiveWorkspace,
                    shellState.ActiveWorkspace,
                    recovery,
                    RestartAndVerify,
                    RecoverFailedSessionStart,
                    request.PageContext?.TimelineMode);
                restarted = navigation.Success
                    && shell.CurrentState.LastCompletedOutcome
                        == CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded
                    && shell.CurrentState.PresentationMode
                        == CharacterTestRuntimeAiLabPresentationMode.Workspace;
                previousSessionRecovered = !restarted
                    && shell.CurrentState.LastCompletedOutcome
                        == CharacterTestRuntimeAiLabSessionStartOutcome.Failed
                    && shell.CurrentState.PresentationMode
                        == CharacterTestRuntimeAiLabPresentationMode.Workspace;
                if (!restarted && string.IsNullOrWhiteSpace(restartFailureMessage))
                {
                    restartFailureCode = navigation.Code;
                    restartFailureMessage = navigation.Message;
                }
                if (!restarted && !previousSessionRecovered) recoveryDiagnostic = navigation.Message + " " + _statusMessage;
            }
            else
            {
                restarted = RestartAndVerify();
                if (!restarted)
                {
                    previousSessionRecovered = RecoverFailedSessionStart(recovery);
                    if (!previousSessionRecovered) recoveryDiagnostic = _statusMessage;
                }
            }

            LastTestRestartDiagnostic = restarted ? string.Empty : restartFailureCode + ": " + restartFailureMessage;
            LastTestRecoveryDiagnostic = recoveryDiagnostic;
            if (!restarted)
            {
                bool requiresRecovery = hadPriorRunningSession && !previousSessionRecovered;
                if (requiresRecovery)
                {
                    return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                        CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Recovery,
                        "TEST_RESTART_RECOVERY_REQUIRED",
                        "重新开始测试失败，而且旧测试未能精确恢复。请重置实验后重试。 启动原因："
                            + LastTestRestartDiagnostic + " 恢复原因：" + LastTestRecoveryDiagnostic,
                        previousSessionRecovered: false,
                        requiresRecovery: true);
                }
                CharacterTestRuntimeAiLabTestCurrentChangesFailureStage stage =
                    string.Equals(
                        restartFailureCode,
                        "RUNTIME_IDENTITY_MISMATCH",
                        StringComparison.Ordinal)
                    || string.Equals(
                        restartFailureCode,
                        "RUNTIME_CANDIDATE_CHANGED",
                        StringComparison.Ordinal)
                        ? CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.RuntimeIdentity
                        : CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Restart;
                return CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    stage,
                    string.IsNullOrWhiteSpace(restartFailureCode)
                        ? "TEST_RESTART_FAILED"
                        : restartFailureCode,
                    "重新开始测试失败；没有把当前修改标记为已生效。 "
                        + (restartFailureMessage ?? string.Empty),
                    previousSessionRecovered: previousSessionRecovered);
            }

            string contextDiagnostic = string.Empty;
            bool contextRestored = !useShellTransaction
                || IsTestCurrentChangesPageContextRestored(
                    request.PageContext,
                    shell.CurrentState.PageContext,
                    out contextDiagnostic);
            string successMessage =
                "已保存当前 Workspace 草稿，并已从新的第 0 帧基线重新开始测试。";
            if (!contextRestored)
                successMessage += " 原编辑定位恢复失败：" + contextDiagnostic;
            return new CharacterTestRuntimeAiLabTestCurrentChangesApplyResult(
                true,
                CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.None,
                string.Empty,
                successMessage,
                0,
                candidate.AuthorityHash,
                verifySelectedCardIdentity ? installedAuthorityHash : candidate.AuthorityHash,
                selectedEntityId,
                runtimeProfileSlot,
                firstVisibleFrame,
                restartedFromFrameZero: restartedFromFrameZeroBaseline,
                contextRestored,
                previousSessionRecovered: false,
                publishedContentPristine);
        }

        private int CountTestCurrentChangesBlockingDiagnostics(string groupDraftId)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = _draftWorkspace;
            if (workspace == null
                || !TryGetAuthoringGroup(
                    groupDraftId ?? string.Empty,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                return 1;
            }

            int count = 0;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry == null
                    || !workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || card == null)
                {
                    count++;
                    continue;
                }
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                    CompileBehaviorCardLogic(card);
                if (compile?.Success == true && compile.Program != null)
                    continue;
                count += Math.Max(1, compile?.Diagnostics?.Count ?? 0);
            }
            return count;
        }

        private bool IsTestCurrentChangesPublishedContentPristine(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request)
        {
            if (workspace == null
                || !TryGetAuthoringGroup(
                    request.GroupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group?.HasProjectSource != true
                || HasUnpublishedBehaviorGroupContent(group, out _))
            {
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry == null
                    || !string.Equals(
                        entry.CardDraftId,
                        request.CardDraftId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                return !HasUnpublishedBehaviorCardContent(group, i, out _);
            }
            return false;
        }

        private bool IsTestCurrentChangesPageContextRestored(
            CharacterTestRuntimeAiLabPageContext expected,
            CharacterTestRuntimeAiLabPageContext actual,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (expected == null)
                return true;
            if (actual == null)
            {
                diagnostic = "Shell PageContext 不可用。";
                return false;
            }
            if (!string.Equals(expected.GroupDraftId, actual.GroupDraftId, StringComparison.Ordinal)
                || !string.Equals(expected.EntryId, actual.EntryId, StringComparison.Ordinal)
                || !string.Equals(expected.CardDraftId, actual.CardDraftId, StringComparison.Ordinal))
            {
                diagnostic = "原行为组、行为卡或条目身份已不存在。";
                return false;
            }
            if (expected.SelectedElementKind != actual.SelectedElementKind
                || !string.Equals(
                    expected.SelectedElementStableId,
                    actual.SelectedElementStableId,
                    StringComparison.Ordinal))
            {
                diagnostic = "原 Timeline/Event 稳定身份已不存在。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorCardViewport left = expected.BehaviorCardViewport;
            CharacterTestRuntimeAiLabBehaviorCardViewport right = actual.BehaviorCardViewport;
            if (left.HorizontalStartFrame != right.HorizontalStartFrame
                || left.HorizontalEndFrame != right.HorizontalEndFrame
                || Math.Abs(left.ZoomLevel - right.ZoomLevel) > 0.0001f
                || Math.Abs(left.VerticalScrollOffset - right.VerticalScrollOffset) > 0.0001f
                || left.FollowCurrent != right.FollowCurrent)
            {
                diagnostic = "行为时间轴 zoom/scroll viewport 未能精确恢复。";
                return false;
            }
            if (expected.TimelineMode != actual.TimelineMode)
            {
                diagnostic = "原行为时间轴模式未能恢复。";
                return false;
            }
            return true;
        }

        private void RecordTestCurrentChangesAsCompatibilitySaveRun(
            CharacterTestRuntimeAiLabTestCurrentChangesResult result)
        {
            CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus status;
            switch (result.FailureStage)
            {
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.None:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedAndRunning;
                    break;
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Save:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SaveFailed;
                    break;
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Validation:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedValidationFailed;
                    break;
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Candidate:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedCompileFailed;
                    break;
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Target:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedTargetUnavailable;
                    break;
                case CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.RuntimeIdentity:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus
                        .SavedRuntimeIdentityMismatch;
                    break;
                default:
                    status = CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus
                        .SavedRuntimeInstallFailed;
                    break;
            }

            FinishBehaviorRecipeSaveRun(
                result.OperationId,
                status,
                result.FailureStage.ToString().ToLowerInvariant(),
                result.FailureCode,
                result.Message,
                result.WorkspaceRevision,
                result.GroupDraftId,
                result.CardDraftId,
                0L,
                result.CandidateAuthorityHash,
                result.SelectedEntityId,
                result.RuntimeProfileSlot,
                result.InstalledAuthorityHash,
                out _);
        }
    }
}
