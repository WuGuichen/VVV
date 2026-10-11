using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus
    {
        None = 0,
        SaveFailed = 1,
        SavedValidationFailed = 2,
        SavedCompileFailed = 3,
        SavedTargetUnavailable = 4,
        SavedRuntimeInstallFailed = 5,
        SavedRuntimeIdentityMismatch = 6,
        SavedAndRunning = 7
    }

    internal enum CharacterTestRuntimeAiLabBehaviorRecipeSyncState
    {
        Unsaved = 0,
        SavedNotRunning = 1,
        RunningCurrentDraft = 2,
        RunningOlderDraft = 3,
        RunningPublishedVersion = 4,
        RuntimeLoadFailed = 5
    }

    /// <summary>Low-frequency result for one explicit Behavior Recipe save-and-run transaction.</summary>
    /// <remarks>LowFreqAlloc: Runtime AI Lab authoring command only.</remarks>
    internal readonly struct CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult
    {
        internal CharacterTestRuntimeAiLabBehaviorRecipeSaveRunResult(
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
            long installedAuthorityHash)
        {
            OperationId = operationId ?? string.Empty;
            Status = status;
            Stage = stage ?? string.Empty;
            FailureCode = failureCode ?? string.Empty;
            Message = message ?? string.Empty;
            WorkspaceRevision = workspaceRevision;
            GroupDraftId = groupDraftId ?? string.Empty;
            CardDraftId = cardDraftId ?? string.Empty;
            DraftSemanticHash = draftSemanticHash;
            CandidateAuthorityHash = candidateAuthorityHash;
            SelectedEntityId = selectedEntityId;
            RuntimeProfileSlot = runtimeProfileSlot;
            InstalledAuthorityHash = installedAuthorityHash;
        }

        internal string OperationId { get; }
        internal CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus Status { get; }
        internal string Stage { get; }
        internal string FailureCode { get; }
        internal string Message { get; }
        internal int WorkspaceRevision { get; }
        internal string GroupDraftId { get; }
        internal string CardDraftId { get; }
        internal long DraftSemanticHash { get; }
        internal long CandidateAuthorityHash { get; }
        internal int SelectedEntityId { get; }
        internal int RuntimeProfileSlot { get; }
        internal long InstalledAuthorityHash { get; }
        internal bool Success => Status == CharacterTestRuntimeAiLabBehaviorRecipeSaveRunStatus.SavedAndRunning;
    }
}
