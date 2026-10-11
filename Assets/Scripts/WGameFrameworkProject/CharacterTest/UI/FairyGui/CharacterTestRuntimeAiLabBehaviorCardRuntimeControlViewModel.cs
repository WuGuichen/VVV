namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel
    {
        internal bool Available { get; set; }
        internal bool Paused { get; set; }
        internal bool ReplayInProgress { get; set; }
        internal bool HistoryPlaybackActive { get; set; }
        internal bool FutureAdvanceActive { get; set; }
        internal bool BranchRecomputeArmed { get; set; }
        internal bool BranchConfirmationPending { get; set; }
        internal long BranchConfirmationGeneration { get; set; }
        internal bool CanSeek { get; set; }
        internal bool CanFirst { get; set; }
        internal bool CanLatest { get; set; }
        internal bool CanJump { get; set; }
        internal bool CanStepBackward { get; set; }
        internal bool CanStepForward { get; set; }
        internal bool CanPlayPause { get; set; }
        internal bool CanBranchRecompute { get; set; }
        internal bool HasDifferentActiveCard { get; set; }
        internal int SessionStartFrame { get; set; }
        internal int RetainedStartFrame { get; set; }
        internal int SelectedFrame { get; set; }
        internal int LatestFrame { get; set; }
        internal int SessionEndFrame { get; set; }
        internal int FrameLimit { get; set; }
        internal int FutureAdvanceTargetFrame { get; set; }
        internal int PendingTargetFrame { get; set; }
        internal int QueuedTargetFrame { get; set; }
        internal int HoverTargetFrame { get; set; }
        internal long CommitGeneration { get; set; }
        internal long AuthorityHash { get; set; }
        internal CharacterTestRuntimeAiLabFrameSelectionPhase SelectionPhase { get; set; }
        internal string FirstDisabledReasonCode { get; set; } = string.Empty;
        internal string FirstDisabledReasonText { get; set; } = string.Empty;
        internal string BackwardDisabledReasonCode { get; set; } = string.Empty;
        internal string BackwardDisabledReasonText { get; set; } = string.Empty;
        internal string ForwardDisabledReasonCode { get; set; } = string.Empty;
        internal string ForwardDisabledReasonText { get; set; } = string.Empty;
        internal string LatestDisabledReasonCode { get; set; } = string.Empty;
        internal string LatestDisabledReasonText { get; set; } = string.Empty;
        internal string PlayDisabledReasonCode { get; set; } = string.Empty;
        internal string PlayDisabledReasonText { get; set; } = string.Empty;
        internal string JumpDisabledReasonCode { get; set; } = string.Empty;
        internal string JumpDisabledReasonText { get; set; } = string.Empty;
        internal string BranchDisabledReasonCode { get; set; } = string.Empty;
        internal string BranchDisabledReasonText { get; set; } = string.Empty;
        internal string PlayPauseLabel { get; set; } = "播放";
        internal string FrameInputText { get; set; } = string.Empty;
        internal string FrameSummaryText { get; set; } = "尚未捕获运行帧";
        internal string SeekStatusText { get; set; } = string.Empty;
        internal string BranchActionLabel { get; set; } = "从此重算";
        internal string BranchRiskText { get; set; } = string.Empty;
        internal string ActiveCardName { get; set; } = string.Empty;
    }
}
