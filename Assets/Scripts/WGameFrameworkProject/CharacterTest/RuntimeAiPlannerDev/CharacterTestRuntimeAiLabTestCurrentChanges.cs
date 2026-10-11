using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabTestCurrentChangesOperationState
    {
        Ready = 0,
        Saving = 1,
        Validating = 2,
        Restarting = 3,
        Succeeded = 4,
        Failed = 5
    }

    internal enum CharacterTestRuntimeAiLabTestCurrentChangesFailureStage
    {
        None = 0,
        Save = 1,
        Validation = 2,
        Candidate = 3,
        Target = 4,
        Restart = 5,
        RuntimeIdentity = 6,
        ContextRestore = 7,
        Recovery = 8
    }

    internal enum CharacterTestRuntimeAiLabCurrentTestSource
    {
        NotStarted = 0,
        PublishedVersion = 1,
        UsingCurrentChanges = 2,
        CurrentChangesUpdated = 3,
        Unknown = 4
    }

    /// <summary>
    /// Stable authoring identity captured before the scene-bound Runtime AI Lab session is rebuilt.
    /// No list index is used as the return authority.
    /// </summary>
    internal readonly struct CharacterTestRuntimeAiLabTestCurrentChangesRequest
    {
        internal CharacterTestRuntimeAiLabTestCurrentChangesRequest(
            string operationId,
            string workspaceId,
            int workspaceRevision,
            int setupTeamId,
            int setupSlotIndex,
            string groupDraftId,
            string draftEntryId,
            string cardDraftId,
            string publishedStableCardId,
            CharacterTestRuntimeAiLabPageContext pageContext)
        {
            OperationId = operationId ?? string.Empty;
            WorkspaceId = workspaceId ?? string.Empty;
            WorkspaceRevision = workspaceRevision;
            SetupTeamId = setupTeamId;
            SetupSlotIndex = setupSlotIndex;
            GroupDraftId = groupDraftId ?? string.Empty;
            DraftEntryId = draftEntryId ?? string.Empty;
            CardDraftId = cardDraftId ?? string.Empty;
            PublishedStableCardId = publishedStableCardId ?? string.Empty;
            PageContext = pageContext;
        }

        internal string OperationId { get; }
        internal string WorkspaceId { get; }
        internal int WorkspaceRevision { get; }
        internal int SetupTeamId { get; }
        internal int SetupSlotIndex { get; }
        internal string GroupDraftId { get; }
        internal string DraftEntryId { get; }
        internal string CardDraftId { get; }
        internal string PublishedStableCardId { get; }
        internal CharacterTestRuntimeAiLabPageContext PageContext { get; }

        internal bool IsValid => OperationId.Length > 0
            && WorkspaceId.Length > 0
            && WorkspaceRevision > 0
            && SetupTeamId > 0
            && SetupSlotIndex >= 0
            && GroupDraftId.Length > 0
            && DraftEntryId.Length > 0
            && CardDraftId.Length > 0;
    }

    internal readonly struct CharacterTestRuntimeAiLabTestCurrentChangesApplyResult
    {
        internal CharacterTestRuntimeAiLabTestCurrentChangesApplyResult(
            bool success,
            CharacterTestRuntimeAiLabTestCurrentChangesFailureStage failureStage,
            string failureCode,
            string message,
            int blockingDiagnosticCount,
            long candidateAuthorityHash,
            long installedAuthorityHash,
            int selectedEntityId,
            int runtimeProfileSlot,
            int firstVisibleFrame,
            bool restartedFromFrameZero,
            bool contextRestored,
            bool previousSessionRecovered,
            bool publishedContentPristine,
            bool requiresRecovery = false)
        {
            Success = success;
            FailureStage = failureStage;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
            BlockingDiagnosticCount = Math.Max(0, blockingDiagnosticCount);
            CandidateAuthorityHash = candidateAuthorityHash;
            InstalledAuthorityHash = installedAuthorityHash;
            SelectedEntityId = selectedEntityId;
            RuntimeProfileSlot = runtimeProfileSlot;
            FirstVisibleFrame = Math.Max(0, firstVisibleFrame);
            RestartedFromFrameZero = restartedFromFrameZero;
            ContextRestored = contextRestored;
            PreviousSessionRecovered = previousSessionRecovered;
            PublishedContentPristine = publishedContentPristine;
            RequiresRecovery = requiresRecovery;
        }

        internal bool Success { get; }
        internal CharacterTestRuntimeAiLabTestCurrentChangesFailureStage FailureStage { get; }
        internal string FailureCode { get; }
        internal string Message { get; }
        internal int BlockingDiagnosticCount { get; }
        internal long CandidateAuthorityHash { get; }
        internal long InstalledAuthorityHash { get; }
        internal int SelectedEntityId { get; }
        internal int RuntimeProfileSlot { get; }
        internal int FirstVisibleFrame { get; }
        internal bool RestartedFromFrameZero { get; }
        internal bool ContextRestored { get; }
        internal bool PreviousSessionRecovered { get; }
        internal bool PublishedContentPristine { get; }
        internal bool RequiresRecovery { get; }

        internal static CharacterTestRuntimeAiLabTestCurrentChangesApplyResult Failed(
            CharacterTestRuntimeAiLabTestCurrentChangesFailureStage stage,
            string code,
            string message,
            int blockingDiagnosticCount = 0,
            bool previousSessionRecovered = false,
            bool requiresRecovery = false)
        {
            return new CharacterTestRuntimeAiLabTestCurrentChangesApplyResult(
                false,
                stage,
                code,
                message,
                blockingDiagnosticCount,
                0L,
                0L,
                0,
                0,
                0,
                false,
                false,
                previousSessionRecovered,
                false,
                requiresRecovery);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabTestCurrentChangesResult
    {
        internal CharacterTestRuntimeAiLabTestCurrentChangesResult(
            string operationId,
            CharacterTestRuntimeAiLabTestCurrentChangesOperationState state,
            CharacterTestRuntimeAiLabTestCurrentChangesFailureStage failureStage,
            string failureCode,
            string message,
            int blockingDiagnosticCount,
            string workspaceId,
            int workspaceRevision,
            string groupDraftId,
            string draftEntryId,
            string cardDraftId,
            string publishedStableCardId,
            long candidateAuthorityHash,
            long installedAuthorityHash,
            int selectedEntityId,
            int runtimeProfileSlot,
            int firstVisibleFrame,
            bool restartedFromFrameZero,
            bool contextRestored,
            bool previousSessionRecovered,
            bool publishedContentPristine,
            bool requiresRecovery = false)
        {
            OperationId = operationId ?? string.Empty;
            State = state;
            FailureStage = failureStage;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
            BlockingDiagnosticCount = Math.Max(0, blockingDiagnosticCount);
            WorkspaceId = workspaceId ?? string.Empty;
            WorkspaceRevision = workspaceRevision;
            GroupDraftId = groupDraftId ?? string.Empty;
            DraftEntryId = draftEntryId ?? string.Empty;
            CardDraftId = cardDraftId ?? string.Empty;
            PublishedStableCardId = publishedStableCardId ?? string.Empty;
            CandidateAuthorityHash = candidateAuthorityHash;
            InstalledAuthorityHash = installedAuthorityHash;
            SelectedEntityId = selectedEntityId;
            RuntimeProfileSlot = runtimeProfileSlot;
            FirstVisibleFrame = Math.Max(0, firstVisibleFrame);
            RestartedFromFrameZero = restartedFromFrameZero;
            ContextRestored = contextRestored;
            PreviousSessionRecovered = previousSessionRecovered;
            PublishedContentPristine = publishedContentPristine;
            RequiresRecovery = requiresRecovery;
        }

        internal string OperationId { get; }
        internal CharacterTestRuntimeAiLabTestCurrentChangesOperationState State { get; }
        internal CharacterTestRuntimeAiLabTestCurrentChangesFailureStage FailureStage { get; }
        internal string FailureCode { get; }
        internal string Message { get; }
        internal int BlockingDiagnosticCount { get; }
        internal string WorkspaceId { get; }
        internal int WorkspaceRevision { get; }
        internal string GroupDraftId { get; }
        internal string DraftEntryId { get; }
        internal string CardDraftId { get; }
        internal string PublishedStableCardId { get; }
        internal long CandidateAuthorityHash { get; }
        internal long InstalledAuthorityHash { get; }
        internal int SelectedEntityId { get; }
        internal int RuntimeProfileSlot { get; }
        internal int FirstVisibleFrame { get; }
        internal bool RestartedFromFrameZero { get; }
        internal bool ContextRestored { get; }
        internal bool PreviousSessionRecovered { get; }
        internal bool PublishedContentPristine { get; }
        internal bool RequiresRecovery { get; }
        internal bool Success => State == CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Succeeded
            && FailureStage == CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.None
            && CandidateAuthorityHash != 0L
            && CandidateAuthorityHash == InstalledAuthorityHash
            && RestartedFromFrameZero;
    }

    /// <summary>One-consumer in-memory queue. It is deliberately not persisted across reloads.</summary>
    internal sealed class CharacterTestRuntimeAiLabTestCurrentChangesQueue
    {
        private CharacterTestRuntimeAiLabTestCurrentChangesRequest _pending;

        internal bool HasPending { get; private set; }

        internal bool TryEnqueue(
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
            out string diagnostic)
        {
            if (HasPending)
            {
                diagnostic = "测试当前修改已在队列中，请等待本次操作完成。";
                return false;
            }
            if (!request.IsValid)
            {
                diagnostic = "测试当前修改缺少稳定的 Workspace、行为组或行为卡身份。";
                return false;
            }

            _pending = request;
            HasPending = true;
            diagnostic = string.Empty;
            return true;
        }

        internal bool TryTake(out CharacterTestRuntimeAiLabTestCurrentChangesRequest request)
        {
            request = _pending;
            if (!HasPending)
                return false;
            _pending = default;
            HasPending = false;
            return true;
        }

        internal void Cancel()
        {
            _pending = default;
            HasPending = false;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker
    {
        private CharacterTestRuntimeAiLabTestCurrentChangesRequest _activeRequest;
        private CharacterTestRuntimeAiLabTestCurrentChangesResult _lastResult;
        private CharacterTestRuntimeAiLabTestCurrentChangesResult _lastSuccessfulResult;

        internal CharacterTestRuntimeAiLabTestCurrentChangesOperationState State { get; private set; } =
            CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Ready;
        internal CharacterTestRuntimeAiLabTestCurrentChangesResult LastResult => _lastResult;
        internal CharacterTestRuntimeAiLabTestCurrentChangesResult LastSuccessfulResult =>
            _lastSuccessfulResult;
        internal bool IsBusy => State == CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Saving
            || State == CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Validating
            || State == CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Restarting;

        internal bool TryBegin(
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
            out string diagnostic)
        {
            if (IsBusy)
            {
                diagnostic = "测试当前修改正在执行，不能重复排队。";
                return false;
            }
            if (!request.IsValid)
            {
                diagnostic = "测试当前修改请求缺少稳定身份。";
                return false;
            }

            _activeRequest = request;
            State = CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Saving;
            diagnostic = string.Empty;
            return true;
        }

        internal void MarkValidating()
        {
            if (IsBusy)
                State = CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Validating;
        }

        internal void MarkRestarting()
        {
            if (IsBusy)
                State = CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Restarting;
        }

        internal CharacterTestRuntimeAiLabTestCurrentChangesResult Complete(
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult apply)
        {
            CharacterTestRuntimeAiLabTestCurrentChangesOperationState finalState = apply.Success
                ? CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Succeeded
                : CharacterTestRuntimeAiLabTestCurrentChangesOperationState.Failed;
            _lastResult = CreateResult(_activeRequest, finalState, apply);
            State = finalState;
            if (_lastResult.Success)
                _lastSuccessfulResult = _lastResult;
            _activeRequest = default;
            return _lastResult;
        }

        internal CharacterTestRuntimeAiLabTestCurrentChangesResult FailSave(
            string code,
            string message)
        {
            return Complete(CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Save,
                code,
                message));
        }

        internal CharacterTestRuntimeAiLabCurrentTestSource ResolveCurrentTestSource(
            bool sessionRunning,
            long runtimeAuthorityHash,
            string currentWorkspaceId,
            int currentWorkspaceRevision,
            long currentCandidateAuthorityHash)
        {
            if (!sessionRunning || string.IsNullOrEmpty(_lastResult.OperationId))
                return CharacterTestRuntimeAiLabCurrentTestSource.NotStarted;
            if (!_lastSuccessfulResult.Success)
                return CharacterTestRuntimeAiLabCurrentTestSource.Unknown;
            if (runtimeAuthorityHash == 0L
                || runtimeAuthorityHash != _lastSuccessfulResult.InstalledAuthorityHash)
            {
                return CharacterTestRuntimeAiLabCurrentTestSource.Unknown;
            }
            if (!string.Equals(
                    currentWorkspaceId,
                    _lastSuccessfulResult.WorkspaceId,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabCurrentTestSource.Unknown;
            }
            if (currentWorkspaceRevision != _lastSuccessfulResult.WorkspaceRevision
                || currentCandidateAuthorityHash == 0L
                || currentCandidateAuthorityHash != _lastSuccessfulResult.CandidateAuthorityHash)
            {
                return CharacterTestRuntimeAiLabCurrentTestSource.CurrentChangesUpdated;
            }
            return _lastSuccessfulResult.PublishedContentPristine
                ? CharacterTestRuntimeAiLabCurrentTestSource.PublishedVersion
                : CharacterTestRuntimeAiLabCurrentTestSource.UsingCurrentChanges;
        }

        private static CharacterTestRuntimeAiLabTestCurrentChangesResult CreateResult(
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
            CharacterTestRuntimeAiLabTestCurrentChangesOperationState state,
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult apply)
        {
            return new CharacterTestRuntimeAiLabTestCurrentChangesResult(
                request.OperationId,
                state,
                apply.FailureStage,
                apply.FailureCode,
                apply.Message,
                apply.BlockingDiagnosticCount,
                request.WorkspaceId,
                request.WorkspaceRevision,
                request.GroupDraftId,
                request.DraftEntryId,
                request.CardDraftId,
                request.PublishedStableCardId,
                apply.CandidateAuthorityHash,
                apply.InstalledAuthorityHash,
                apply.SelectedEntityId,
                apply.RuntimeProfileSlot,
                apply.FirstVisibleFrame,
                apply.RestartedFromFrameZero,
                apply.ContextRestored,
                apply.PreviousSessionRecovered,
                apply.PublishedContentPristine,
                apply.RequiresRecovery);
        }
    }

    internal delegate bool CharacterTestRuntimeAiLabTestCurrentChangesFlush(
        out string diagnostic);

    internal delegate CharacterTestRuntimeAiLabTestCurrentChangesApplyResult
        CharacterTestRuntimeAiLabTestCurrentChangesApply(Action markRestarting);

    /// <summary>
    /// Sequences persistence before validation/restart. There is intentionally no Published writer
    /// dependency in this transaction.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabTestCurrentChangesTransaction
    {
        internal static CharacterTestRuntimeAiLabTestCurrentChangesResult Execute(
            CharacterTestRuntimeAiLabTestCurrentChangesOperationTracker tracker,
            CharacterTestRuntimeAiLabTestCurrentChangesFlush flush,
            CharacterTestRuntimeAiLabTestCurrentChangesApply apply)
        {
            if (tracker == null)
                throw new ArgumentNullException(nameof(tracker));
            if (!tracker.IsBusy)
                throw new InvalidOperationException("A test-current-changes operation was not begun.");
            string saveDiagnostic = string.Empty;
            bool saved = flush != null && flush(out saveDiagnostic);
            if (!saved)
            {
                return tracker.FailSave(
                    "DRAFT_SAVE_FAILED",
                    "草稿保存失败，当前测试没有改变。"
                    + (string.IsNullOrWhiteSpace(saveDiagnostic)
                        ? string.Empty
                        : " " + saveDiagnostic));
            }

            tracker.MarkValidating();
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult applyResult = apply != null
                ? apply(tracker.MarkRestarting)
                : CharacterTestRuntimeAiLabTestCurrentChangesApplyResult.Failed(
                    CharacterTestRuntimeAiLabTestCurrentChangesFailureStage.Restart,
                    "TEST_APPLY_UNAVAILABLE",
                    "测试当前修改的运行时应用入口不可用，当前测试没有改变。");
            return tracker.Complete(applyResult);
        }
    }
}
