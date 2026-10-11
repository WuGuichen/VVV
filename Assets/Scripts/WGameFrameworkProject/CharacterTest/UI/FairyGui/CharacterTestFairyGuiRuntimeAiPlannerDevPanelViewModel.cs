using System;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerDevPresentationContext
    {
        Unavailable,
        Setup,
        Live,
        PausedInspect,
        Review,
        LoopReview,
        Completed
    }

    internal enum CharacterTestRuntimeAiLabWorkspaceMode
    {
        Setup,
        BehaviorGroup,
        BehaviorCard,
        Diagnostics,
        TacticalLibrary,

        // Compatibility alias for tests and call sites that still describe the
        // old combined runtime-details workspace as Context.
        Context = Diagnostics
    }

    internal enum CharacterTestRuntimeAiLabDiagnosticsTab
    {
        Facts,
        RuntimeFrame,
        Replay
    }

    internal enum CharacterTestRuntimeAiLabPublishUxState
    {
        Unavailable = 0,
        GameAvailable = 1,
        HasUnpublishedChanges = 2,
        WorkspaceOnly = 3
    }

    internal enum CharacterTestRuntimeAiLabDraftSaveUxState
    {
        Unavailable = 0,
        Saving = 1,
        Saved = 2,
        SaveFailed = 3,
        ReadOnly = 4
    }

    internal enum CharacterTestRuntimeAiLabTestSourceUxState
    {
        NotStarted = 0,
        PublishedVersion = 1,
        UsingCurrentChanges = 2,
        BehindCurrentChanges = 3,
        Unknown = 4
    }

    internal sealed class CharacterTestRuntimeAiLabAuthoringUxStatusViewModel
    {
        public CharacterTestRuntimeAiLabPublishUxState BehaviorGroupPublishState { get; set; }
        public CharacterTestRuntimeAiLabPublishUxState BehaviorCardPublishState { get; set; }
        public CharacterTestRuntimeAiLabDraftSaveUxState DraftSaveState { get; set; }
        public CharacterTestRuntimeAiLabTestSourceUxState TestSourceState { get; set; }
        public bool BehaviorGroupHasValidationProblems { get; set; }
        public bool BehaviorCardHasValidationProblems { get; set; }
        public string BehaviorGroupPublishStatusText { get; set; } = string.Empty;
        public string BehaviorCardPublishStatusText { get; set; } = string.Empty;
        public string DraftSaveStatusText { get; set; } = string.Empty;
        public string TestSourceStatusText { get; set; } = "尚未开始测试";
        public string BehaviorGroupValidationStatusText { get; set; } = string.Empty;
        public string BehaviorCardValidationStatusText { get; set; } = string.Empty;
        public string AdvancedDiagnosticText { get; internal set; } = string.Empty;
    }

    internal static class CharacterTestRuntimeAiLabAuthoringUxStatusFormatter
    {
        internal static void Reset(CharacterTestRuntimeAiLabAuthoringUxStatusViewModel target)
        {
            if (target == null)
                return;
            target.BehaviorGroupPublishState = CharacterTestRuntimeAiLabPublishUxState.Unavailable;
            target.BehaviorCardPublishState = CharacterTestRuntimeAiLabPublishUxState.Unavailable;
            target.DraftSaveState = CharacterTestRuntimeAiLabDraftSaveUxState.Unavailable;
            target.TestSourceState = CharacterTestRuntimeAiLabTestSourceUxState.NotStarted;
            target.BehaviorGroupHasValidationProblems = false;
            target.BehaviorCardHasValidationProblems = false;
            target.BehaviorGroupPublishStatusText = string.Empty;
            target.BehaviorCardPublishStatusText = string.Empty;
            target.DraftSaveStatusText = string.Empty;
            target.TestSourceStatusText = FormatTestSourceState(target.TestSourceState);
            target.BehaviorGroupValidationStatusText = string.Empty;
            target.BehaviorCardValidationStatusText = string.Empty;
            target.AdvancedDiagnosticText = string.Empty;
        }

        internal static CharacterTestRuntimeAiLabPublishUxState ResolvePublishState(
            bool hasPublishedSource,
            bool hasUnpublishedChanges)
        {
            if (!hasPublishedSource)
                return CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly;
            return hasUnpublishedChanges
                ? CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges
                : CharacterTestRuntimeAiLabPublishUxState.GameAvailable;
        }

        internal static CharacterTestRuntimeAiLabDraftSaveUxState ResolveDraftSaveState(
            bool available,
            bool readOnly,
            bool dirty,
            string persistenceStatus)
        {
            if (!available)
                return CharacterTestRuntimeAiLabDraftSaveUxState.Unavailable;
            if (readOnly)
                return CharacterTestRuntimeAiLabDraftSaveUxState.ReadOnly;
            if (IsSaveFailure(persistenceStatus))
                return CharacterTestRuntimeAiLabDraftSaveUxState.SaveFailed;
            return dirty ? CharacterTestRuntimeAiLabDraftSaveUxState.Saving
                : CharacterTestRuntimeAiLabDraftSaveUxState.Saved;
        }

        internal static CharacterTestRuntimeAiLabTestSourceUxState ResolveTestSourceState(
            bool sessionStarted,
            bool runtimeIdentityAvailable,
            bool candidateMatchesRuntime,
            bool currentMatchesCandidate,
            bool currentCardMatchesCandidate,
            bool publishedContentPristine)
        {
            if (!sessionStarted)
                return CharacterTestRuntimeAiLabTestSourceUxState.NotStarted;
            if (!runtimeIdentityAvailable)
                return CharacterTestRuntimeAiLabTestSourceUxState.Unknown;
            if (!candidateMatchesRuntime || !currentMatchesCandidate || !currentCardMatchesCandidate)
                return CharacterTestRuntimeAiLabTestSourceUxState.BehindCurrentChanges;
            return publishedContentPristine
                ? CharacterTestRuntimeAiLabTestSourceUxState.PublishedVersion
                : CharacterTestRuntimeAiLabTestSourceUxState.UsingCurrentChanges;
        }

        internal static string FormatPublishState(CharacterTestRuntimeAiLabPublishUxState state)
        {
            switch (state)
            {
                case CharacterTestRuntimeAiLabPublishUxState.GameAvailable:
                    return "已同步";
                case CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges:
                    return "有未发布修改";
                case CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly:
                    return "尚未发布";
                default:
                    return string.Empty;
            }
        }

        internal static string FormatDraftSaveState(CharacterTestRuntimeAiLabDraftSaveUxState state, bool automatic = false)
        {
            switch (state)
            {
                case CharacterTestRuntimeAiLabDraftSaveUxState.Saving:
                    return "草稿待自动保存";
                case CharacterTestRuntimeAiLabDraftSaveUxState.Saved:
                    return automatic ? "草稿已自动保存" : "草稿已保存";
                case CharacterTestRuntimeAiLabDraftSaveUxState.SaveFailed:
                    return "草稿保存失败";
                case CharacterTestRuntimeAiLabDraftSaveUxState.ReadOnly:
                    return "只读";
                default:
                    return string.Empty;
            }
        }

        internal static string FormatTestSourceState(CharacterTestRuntimeAiLabTestSourceUxState state)
        {
            switch (state)
            {
                case CharacterTestRuntimeAiLabTestSourceUxState.PublishedVersion:
                    return "正式版本";
                case CharacterTestRuntimeAiLabTestSourceUxState.UsingCurrentChanges:
                    return "正在使用本次修改";
                case CharacterTestRuntimeAiLabTestSourceUxState.BehindCurrentChanges:
                    return "当前测试落后于修改";
                case CharacterTestRuntimeAiLabTestSourceUxState.Unknown:
                    return "当前测试来源未知";
                default:
                    return "尚未开始测试";
            }
        }

        internal static string FormatValidationState(bool hasValidationProblems) =>
            hasValidationProblems ? "存在需要处理的问题" : "配置有效";

        private static bool IsSaveFailure(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;
            return status.IndexOf("save failed", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("not saved", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("failed validation", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("保存失败", StringComparison.Ordinal) >= 0
                || status.IndexOf("未保存", StringComparison.Ordinal) >= 0;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabWorkspaceViewModel
    {
        public CharacterTestRuntimeAiLabWorkspaceMode ActiveWorkspace { get; set; }
        public CharacterTestRuntimeAiLabDiagnosticsTab ActiveDiagnosticsTab { get; set; }
        public CharacterTestRuntimeAiLabTimelineMode ActiveTimelineMode { get; set; }
        public bool ShowSetup { get; set; }
        public bool ShowBehaviorGroup { get; set; }
        public bool ShowBehaviorCard { get; set; }
        public bool ShowDiagnostics { get; set; }
    }

    internal enum CharacterTestRuntimeAiLabExecutionDisplayState
    {
        None,
        RunningNow,
        ExecutedAtSelectedFrame
    }

    internal enum CharacterTestRuntimeAiLabLogicElementKind
    {
        None = -1,
        Stage = 0,
        Detector = 1,
        Transition = 2
    }

    internal sealed class CharacterTestRuntimeAiLabLogicStageViewModel
    {
        public int Index { get; set; }
        public int Order { get; set; }
        public int Column { get; set; }
        public float Width { get; set; } = 1f;
        public float Offset { get; set; }
        public int StartFrame { get; set; }
        public int DurationFrames { get; set; }
        public int ActualDurationFrames { get; set; } = -1;
        public CharacterTestRuntimeAiLabCardStepKind Kind { get; set; }
        public CharacterTestRuntimeAiLabLogicDurationMode DurationMode { get; set; }
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
        public string CharacterActionDisplayName { get; set; } = string.Empty;
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; set; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; set; }
        public CharacterTestRuntimeAiLabMovementTargetKind MovementTargetKind { get; set; }
        public float MoveTargetPositionX { get; set; }
        public float MoveTargetPositionZ { get; set; }
        public int MoveTargetEntityId { get; set; }
        public float MoveMinDistance { get; set; }
        public float MoveMaxDistance { get; set; }
        public int WaitFrames { get; set; }
        public string Note { get; set; } = string.Empty;
        public string DurationModeText { get; set; } = string.Empty;
        public int EntryConditionCount { get; set; }
        public string EntryConditionSummaryText { get; set; } = string.Empty;
        public string StageId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string DetectorSummaryText { get; set; } = string.Empty;
        public string TransitionSummaryText { get; set; } = string.Empty;
        public string RuntimeStatusText { get; set; } = string.Empty;
        public bool OwnsPrimaryAction { get; set; }
        public bool Entry { get; set; }
        public bool Selected { get; set; }
        public bool PrimarySelection { get; set; }
        public bool Expanded { get; set; }
        public int SelectionCount { get; set; }
        public bool Current { get; set; }
        public bool ActionRunning { get; set; }
        public bool ActionConflict { get; set; }
        public int ActionDurationFrames { get; set; }
        public int ActionProgressFrames { get; set; } = -1;
        public long ActionInstanceId { get; set; }
        public int ActionRuntimeState { get; set; }
        public bool ActionProgressIndeterminate { get; set; }
        public bool ActionProgressOverrun { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicDetectorViewModel
    {
        public int Index { get; set; }
        public int PriorityRank { get; set; }
        public int Priority { get; set; }
        public int StageIndex { get; set; } = -1;
        public int StartFrame { get; set; }
        public int DurationFrames { get; set; }
        public int LinkedTransitionCount { get; set; }
        public int ConditionCount { get; set; }
        public CharacterTestRuntimeAiLabLogicConditionViewModel[] Conditions { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabLogicConditionViewModel>();
        public CharacterTestRuntimeAiLabLogicDetectorScope Scope { get; set; }
        public CharacterTestRuntimeAiLabLogicDetectorTrigger Trigger { get; set; }
        public CharacterTestRuntimeAiLabLogicDetectorWindowEndMode WindowEndMode { get; set; }
        public bool ExpectedResult { get; set; } = true;
        public CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor LocalFrameAnchor { get; set; }
        public int LocalFrame { get; set; }
        public string ActionSignalReasonCode { get; set; } = string.Empty;
        public bool LegacyUnfilteredSignal { get; set; }
        public string WindowEndModeText { get; set; } = string.Empty;
        public string DetectorId { get; set; } = string.Empty;
        public string OwningStageId { get; set; } = string.Empty;
        public string HeaderText { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FactText { get; set; } = string.Empty;
        public string ScopeText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public bool CardScope { get; set; }
        public bool Selected { get; set; }
        public bool Active { get; set; }
        public bool Armed { get; set; }
        public bool Triggered { get; set; }
        public int RuntimeElapsedFrames { get; set; } = -1;
    }

    internal sealed class CharacterTestRuntimeAiLabLogicConditionViewModel
    {
        public string ConditionId { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabCardConditionFact Fact { get; set; }
        public CharacterTestRuntimeAiLabCardConditionOperator Operator { get; set; }
        public bool BoolValue { get; set; }
        public float Value { get; set; }
        public float MaxValue { get; set; }
        public int AbilityId { get; set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabLogicTransitionViewModel
    {
        public int Index { get; set; }
        public int SourceStageIndex { get; set; } = -1;
        public int TargetStageIndex { get; set; } = -1;
        public int DetectorIndex { get; set; } = -1;
        public int Priority { get; set; }
        public CharacterTestRuntimeAiLabLogicTransitionPolicy Policy { get; set; }
        public string TransitionId { get; set; } = string.Empty;
        public string SourceStageId { get; set; } = string.Empty;
        public string TargetStageId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string EndReason { get; set; } = string.Empty;
        public string DisplayTitleText { get; set; } = string.Empty;
        public string DetailText { get; set; } = string.Empty;
        public string PolicyText { get; set; } = string.Empty;
        public string TargetText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public bool EndsCard { get; set; }
        public bool SourceEditable { get; set; }
        public bool IsBackEdge { get; set; }
        public bool Selected { get; set; }
        public bool DetectorHit { get; set; }
        public bool Pending { get; set; }
        public bool Committed { get; set; }
        public bool PreviousCommitted { get; set; }
        public bool Invalid { get; set; }
        public int RuntimeFrame { get; set; } = -1;
    }

    internal enum CharacterTestRuntimeAiLabWorkbenchRowKind
    {
        ResponsibilityHeader = 0,
        BehaviorCard = 1
    }

    internal sealed class CharacterTestRuntimeAiLabWorkbenchRowViewModel
    {
        public CharacterTestRuntimeAiLabWorkbenchRowKind RowKind { get; set; } =
            CharacterTestRuntimeAiLabWorkbenchRowKind.BehaviorCard;
        public int EntryIndex { get; set; } = -1;
        public string CardDraftId { get; set; } = string.Empty;
        public string PublishedStableId { get; set; } = string.Empty;
        public string StableCardId
        {
            get => PublishedStableId;
            set => PublishedStableId = value ?? string.Empty;
        }
        public int DraftRevision { get; set; }
        public CharacterTestRuntimeAiLabPublishUxState PublishState { get; set; }
        public string PublishStatusText { get; set; } = string.Empty;
        public bool HasValidationProblems { get; set; }
        public string ValidationStatusText { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabBehaviorCardLogicPayload LogicPayload { get; set; }
        public string IconKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string TitleText { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string TriggerText { get; set; } = string.Empty;
        public string ActionText { get; set; } = string.Empty;
        public string OutcomeText { get; set; } = string.Empty;
        public string PriorityText { get; set; } = string.Empty;
        public string ExecutionDetailText { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabExecutionDisplayState ExecutionState { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; set; } =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ResponsibilityKind { get; set; }
        public int ResponsibilityCompatibilityMask { get; set; }
        public string ResponsibilityDisplayText { get; set; } = string.Empty;
        public bool HasResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility) =>
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Contains(
                ResponsibilityKinds,
                responsibility);
        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind CoreSlotKind
        {
            get => CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToLegacySlot(ResponsibilityKind);
            set => ResponsibilityKind = CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(value);
        }
        public bool ResponsibilityExpanded { get; set; }
        public int ResponsibilityTotalCount { get; set; }
        public int ResponsibilityEnabledCount { get; set; }
        public string ResponsibilitySummaryText { get; set; } = string.Empty;
        public string ResponsibilityColor { get; set; } = string.Empty;
        public bool ResponsibilityWarning { get; set; }
        public bool IsHeader => RowKind == CharacterTestRuntimeAiLabWorkbenchRowKind.ResponsibilityHeader;
        public bool IsCoreSlot => false;
        public string ExecutionStepId { get; set; } = string.Empty;
        public bool Visible { get; set; }
        public bool Selected { get; set; }
        public bool IsCurrentBehavior { get; set; }
        public bool Disabled { get; set; }
        public bool Invalid { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel
    {
        public string CardDraftId { get; set; } = string.Empty;
        public string BehaviorId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MonogramText { get; set; } = string.Empty;
        public string CategoryText { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public bool Installed { get; set; }
        public bool Available { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabSetupSlotRowViewModel
    {
        public int TeamId { get; set; }
        public int SlotIndex { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public bool Selected { get; set; }
        public bool HasError { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel
    {
        public int DraftId { get; set; }
        public string GroupDraftId { get; set; } = string.Empty;
        public int ArenaBehaviorGroupDraftId { get; set; }
        public bool IsLocalDraft { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string MonogramText { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public string UnavailableReasonText { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabPublishUxState PublishState { get; set; }
        public string PublishStatusText { get; set; } = string.Empty;
        public bool HasValidationProblems { get; set; }
        public string ValidationStatusText { get; set; } = string.Empty;
        public bool Available { get; set; }
        public bool Selected { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabSetupCharacterOptionViewModel
    {
        public int CharacterId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string MonogramText { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }

    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
    {
        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel()
            : this(null)
        {
        }

        internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel behaviorCardWorkspace)
        {
            BehaviorCardWorkspace = behaviorCardWorkspace
                ?? new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
        }

        internal CharacterTestRuntimeAiPlannerDevPresentationContext PresentationContext { get; set; }
        internal CharacterTestRuntimeAiLabSetupSlotRowViewModel[] SetupSlotRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabSetupSlotRowViewModel>();
        internal CharacterTestRuntimeAiLabSetupCharacterOptionViewModel[] SetupCharacterOptions { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabSetupCharacterOptionViewModel>();
        internal CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel[] SetupBehaviorGroupOptions { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabSetupBehaviorGroupOptionViewModel>();
        internal CharacterTestRuntimeAiLabWorkspaceMode WorkspaceMode { get; set; }
        internal CharacterTestRuntimeAiLabDiagnosticsTab DiagnosticsTab { get; set; }
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode { get; set; } =
            CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
        internal bool WorkspaceNavigationEnabled { get; set; } = true;
        internal CharacterTestRuntimeAiLabWorkspaceViewModel Workspace { get; set; } =
            new CharacterTestRuntimeAiLabWorkspaceViewModel();
        internal bool IsSelectedCardExpanded { get; set; }
        internal CharacterTestRuntimeAiLabWorkbenchRowViewModel[] WorkbenchRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabWorkbenchRowViewModel>();
        internal int WorkbenchRowCount { get; set; }
        internal int[] WorkbenchResponsibilityCounts { get; set; } = new int[5];
        internal string WorkbenchGroupIdentity { get; set; } = string.Empty;
        internal int WorkbenchScrollOffset { get; set; }
        internal int WorkbenchMaxScrollOffset { get; set; }
        internal CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel[] WorkbenchLibraryRows { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabWorkbenchLibraryRowViewModel>();
        internal bool WorkbenchCoreReplacementActive { get; set; }
        internal string BehaviorCardRuntimeHeaderText { get; set; } = string.Empty;
        internal string BehaviorCardRuntimeStateText { get; set; } = string.Empty;
        internal string BehaviorCardExecutionSummaryText { get; set; } = string.Empty;
        internal string BehaviorCardRecentEventsText { get; set; } = string.Empty;
        internal CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel BehaviorCardRuntimeControl { get; set; } =
            new CharacterTestRuntimeAiLabBehaviorCardRuntimeControlViewModel();
        internal CharacterTestRuntimeAiLabBehaviorCardIdentityProjection BehaviorCardIdentity { get; set; } =
            new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection();
        internal CharacterTestRuntimeAiLabAuthoringUxStatusViewModel AuthoringStatus { get; } =
            new CharacterTestRuntimeAiLabAuthoringUxStatusViewModel();
        internal CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel BehaviorCardWorkspace { get; set; }
        internal long BehaviorCardBuildSourceRevision { get; set; }
        internal bool HasBehaviorCardBuildSourceRevision { get; set; }
        internal CharacterTestRuntimeAiLabTimelineProjection TimelineProjection { get; set; }
        internal CharacterTestRuntimeAiLabTimelineItem TimelineSelectedItem { get; set; }
        internal string TimelineSelectedVisualStableKey { get; set; } = string.Empty;
        internal CharacterTestRuntimeAiLabPlayerLegalityState PlayerLegalityState { get; set; }

        public string Title { get; set; } = "运行时智能实验室";
        public string StatusText { get; set; } = string.Empty;
        public string PhaseText { get; set; } = string.Empty;
        public string FrameSummary { get; set; } = string.Empty;
        public string PreviewHeader { get; set; } = "当前智能体";
        public string PreviewInstructionsText { get; set; } = string.Empty;
        public string PreviewStatusText { get; set; } = string.Empty;
        public string SetupHeader { get; set; } = "测试配置";
        public string SetupInstructionsText { get; set; } = string.Empty;
        public string SetupSelectedSlotText { get; set; } = string.Empty;
        public string SetupValidationText { get; set; } = string.Empty;
        public string SetupCharacterText { get; set; } = string.Empty;
        public string SetupBehaviorGroupText { get; set; } = string.Empty;
        public string SetupPositionText { get; set; } = string.Empty;
        public string SetupTopValidationText { get; set; } = string.Empty;
        public string SetupTeam1CountText { get; set; } = string.Empty;
        public string SetupTeam1ValidationText { get; set; } = string.Empty;
        public string SetupTeam2CountText { get; set; } = string.Empty;
        public string SetupTeam2ValidationText { get; set; } = string.Empty;
        public string SetupAgentBadgeText { get; set; } = string.Empty;
        public string SetupDistanceText { get; set; } = string.Empty;
        public string SetupReadyTitleText { get; set; } = string.Empty;
        public string SetupReadyDescriptionText { get; set; } = string.Empty;
        public string SetupSceneText { get; set; } = string.Empty;
        public string SetupAgentCountText { get; set; } = string.Empty;
        public string SetupBehaviorGroupCountText { get; set; } = string.Empty;
        public string SetupFrameLimitStatusText { get; set; } = string.Empty;
        public string TimelineHeader { get; set; } = "帧时间轴";
        public string TimelineText { get; set; } = string.Empty;
        public string AgentHeader { get; set; } = "智能体";
        public string AgentListText { get; set; } = string.Empty;
        public string DecisionHeader { get; set; } = "决策详情";
        public string SelectedAgentText { get; set; } = string.Empty;
        public string FactsText { get; set; } = string.Empty;
        public string PathText { get; set; } = string.Empty;
        public string UtilityText { get; set; } = string.Empty;
        public string CommandText { get; set; } = string.Empty;
        public string DiagnosticsText { get; set; } = string.Empty;
        public string EditGateText { get; set; } = string.Empty;
        public string StateValidationText { get; set; } = string.Empty;
        public string WorkbenchTitleText { get; set; } = string.Empty;
        public string WorkbenchEntryCountText { get; set; } = string.Empty;
        public string WorkbenchSummaryText { get; set; } = string.Empty;
        public string WorkbenchDetailText { get; set; } = string.Empty;
        public string WorkbenchDiagnosticText { get; set; } = string.Empty;
        public bool ShowGameplayStopDistanceControl { get; set; }
        public bool ShowGameplayThreatControls { get; set; }
        public float GameplayStopDistance { get; set; } = 1.5f;
        public int GameplayThreatMinimumFrames { get; set; } = 6;
        public int GameplayThreatMaximumFrames { get; set; } = 12;
        public int GameplayDodgeCooldownFrames { get; set; } = 30;
        public string GameplayParameterValidationText { get; set; } = string.Empty;
        public string CandidatePreviewTitleText { get; set; } = "候选预演";
        public string CandidatePreviewBodyText { get; set; } = string.Empty;
        public bool ShowAuthoringContextBar { get; set; }
        public bool ShowAuthoringWorkspaceRoleControl { get; set; }
        public bool ShowAuthoringWriteActions { get; set; }
        public string AuthoringWorkspaceRoleText { get; set; } = "主工作区";
        public string AuthoringGroupContextText { get; set; } = "行为组：未选择";
        public string AuthoringCardContextText { get; set; } = "行为卡：未选择";
        public string AuthoringPublishedStateText { get; set; } = "正式版本：不可用";
        public string AuthoringProblemText { get; set; } = "问题：0";
        public bool CanTestCurrentChanges { get; set; }
        public bool CanPublishCurrentChanges { get; set; }
        public string PublishCurrentChangesLabel { get; set; } = "发布本次修改";
        public string TestCurrentChangesDisabledReasonText { get; set; } =
            "测试当前修改将在 UX-05 接入。";
        public string PublishCurrentChangesDisabledReasonText { get; set; } =
            "发布本次修改将在 UX-06 接入。";
        public string BehaviorGroupPublishStatusText => AuthoringStatus.BehaviorGroupPublishStatusText;
        public string BehaviorCardPublishStatusText => AuthoringStatus.BehaviorCardPublishStatusText;
        public string DraftSaveStatusText => AuthoringStatus.DraftSaveStatusText;
        public string CurrentTestSourceStatusText => AuthoringStatus.TestSourceStatusText;
        public bool BehaviorGroupHasValidationProblems =>
            AuthoringStatus.BehaviorGroupHasValidationProblems;
        public bool BehaviorCardHasValidationProblems =>
            AuthoringStatus.BehaviorCardHasValidationProblems;
        public string BehaviorGroupValidationStatusText =>
            AuthoringStatus.BehaviorGroupValidationStatusText;
        public string BehaviorCardValidationStatusText =>
            AuthoringStatus.BehaviorCardValidationStatusText;
        public string AuthoringAdvancedDiagnosticText => AuthoringStatus.AdvancedDiagnosticText;
        public string DraftStatusText { get; set; } = string.Empty;
        public string PlayerLegalityStatusText { get; set; } = string.Empty;
        public string PlayerLegalityReasonText { get; set; } = string.Empty;
        public string DraftHashText { get; set; } = "hash --------";
        public string ApplyDraftDisabledReasonText { get; set; } = "等待不可变 Revision Runtime";
        public string PublishDraftStatusText { get; set; } = "尚未发布当前草稿";
        public string PublishDraftDisabledReasonText { get; set; } = "等待 Publish 门禁";
        public string SaveBehaviorGroupDisabledReasonText { get; set; } = "当前行为组没有需要保存的修改。";
        public string TimelineRevisionTrackText { get; set; } = string.Empty;
        public string TimelineDecisionTrackText { get; set; } = string.Empty;
        public string TimelineActionTrackText { get; set; } = string.Empty;
        public string TimelineCombatTrackText { get; set; } = string.Empty;
        public string TimelineWarningTrackText { get; set; } = string.Empty;
        public string FrameLimitLabel { get; set; } = "帧上限 900";
        public string TimelineZoomLabel { get; set; } = "适配";
        public string TimelineStatusText { get; set; } = string.Empty;
        public string RuntimeTraceModeLabel { get; set; } = "运行轨迹";
        public string BehaviorPlanModeLabel { get; set; } = "行为计划";
        public string BehaviorLogicModeLabel { get; set; } = "编辑逻辑";
        public string TimelineModeDisabledReasonText { get; set; } = string.Empty;

        public string StartLabel { get; set; } = "开始测试";
        public string PauseResumeLabel { get; set; } = "暂停";
        public string StepLabel { get; set; } = "推进 +1";
        public string BranchRecomputeLabel { get; set; } = "从此重算";
        public string BranchRecomputeRiskText { get; set; } = string.Empty;
        public string StepBackLabel { get; set; } = "-1";
        public string StepBack10Label { get; set; } = "-10";
        public string StepForward10Label { get; set; } = "+10";
        public string ResetLabel { get; set; } = "重置";
        public string PreviousFrameLabel { get; set; } = "< Frame";
        public string NextFrameLabel { get; set; } = "Frame >";
        public string LatestFrameLabel { get; set; } = "最新帧";
        public string JumpFrameLabel { get; set; } = "跳转";
        public string JumpFrameInputText { get; set; } = string.Empty;
        public string SpeedLabel { get; set; } = "1x";
        public string LoopStartLabel { get; set; } = "A";
        public string LoopEndLabel { get; set; } = "B";
        public string ToggleLoopLabel { get; set; } = "循环";
        public string PreviousAgentLabel { get; set; } = "< 智能体";
        public string NextAgentLabel { get; set; } = "智能体 >";
        public string ToggleEditGateLabel { get; set; } = "编辑锁";
        public string TogglePreviewOverlayLabel { get; set; } = "场景标记";
        public string ToggleDetailsLabel { get; set; } = "详情";
        public string ToggleStateCaptureLabel { get; set; } = "状态采集";
        public string ValidateStateLabel { get; set; } = "校验状态";
        public string SyncRecordedStateLabel { get; set; } = "同步记录状态";
        public string ToggleSetupSnapLabel { get; set; } = "吸附 1 米";
        public string ColdBootstrapVerifyLabel { get; set; } = "冷启动校验";
        public string MirrorSpawnsLabel { get; set; } = "镜像站位";
        public string ResetSpawnDefaultsLabel { get; set; } = "重置站位";
        public string FitSetupViewLabel { get; set; } = "适配视野";
        public string ToggleSetupSlotLabel { get; set; } = "禁用";
        public string PreviousSetupCharacterLabel { get; set; } = "<";
        public string NextSetupCharacterLabel { get; set; } = ">";
        public string PreviousSetupBehaviorGroupLabel { get; set; } = "<";
        public string NextSetupBehaviorGroupLabel { get; set; } = ">";
        public string SetupBehaviorGroupPickerCreateLabel { get; set; } = "新建副本";
        public string SetupBehaviorGroupPickerDeleteLabel { get; set; } = "删除选中";
        public string SetupBehaviorGroupPickerDeleteConfirmLabel { get; set; } = "再次点击确认删除";
        public string SetupBehaviorGroupPickerCreateDisabledReasonText { get; set; } = string.Empty;
        public string SetupBehaviorGroupPickerDeleteDisabledReasonText { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorGroupDraftId { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorStableId { get; set; } = string.Empty;
        public int SetupBehaviorGroupEditorSourceRevision { get; set; }
        public long SetupBehaviorGroupEditorSourceContentHash { get; set; }
        public string SetupBehaviorGroupEditorIdentityText { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorDisplayName { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorDescription { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorTagsText { get; set; } = string.Empty;
        public bool SetupBehaviorGroupEditorIsLocalDraft { get; set; }
        public string SetupBehaviorGroupEditorDisabledReasonText { get; set; } = string.Empty;
        public string SetupBehaviorGroupEditorSaveLabel { get; set; } = "保存修改";
        public string CloseLabel { get; set; } = "隐藏";
        public string WorkspaceBackLabel { get; set; } = "返回";
        public string DuplicateEntryLabel { get; set; } = "复制";
        public string MoveEntryUpLabel { get; set; } = "上移";
        public string MoveEntryDownLabel { get; set; } = "下移";
        public string ToggleEntryLabel { get; set; } = "禁用";
        public string DeleteEntryLabel { get; set; } = "删除";
        public string UndoDraftLabel { get; set; } = "撤销";
        public string ApplyDraftLabel { get; set; } = "Apply F+1";
        public string PublishDraftLabel { get; set; } = "Publish";
        public string SaveBehaviorGroupLabel { get; set; } = "保存行为组";
        public string OpenBehaviorGroupLabel { get; set; } = "编辑当前行为";

        public bool CanStart { get; set; }
        public bool CanPauseResume { get; set; }
        public bool CanStep { get; set; }
        public bool CanToggleBranchRecompute { get; set; }
        public bool BranchRecomputeSelected { get; set; }
        public bool CanStepBack { get; set; }
        public bool CanStep10 { get; set; }
        public bool CanJumpFrame { get; set; }
        public bool CanChangeSpeed { get; set; }
        public bool CanSetLoop { get; set; }
        public bool CanSetLoopRange { get; set; }
        public bool CanReset { get; set; }
        public bool CanSelectFrame { get; set; }
        public bool CanSelectAgent { get; set; }
        public bool CanToggleEditGate { get; set; }
        public bool CanTogglePreviewOverlay { get; set; } = true;
        public bool CanToggleDetails { get; set; } = true;
        public bool CanToggleStateCapture { get; set; }
        public bool CanValidateState { get; set; }
        public bool CanSyncRecordedState { get; set; }
        public bool StateCaptureSelected { get; set; }
        public bool CanColdBootstrapVerify { get; set; }
        public bool CanEditSetupPlacement { get; set; }
        public bool CanOpenBehaviorGroup { get; set; }
        public bool CanCreateSetupBehaviorGroup { get; set; }
        public bool CanDeleteSetupBehaviorGroup { get; set; }
        public bool CanEditSetupBehaviorGroupBasicInfo { get; set; }
        public bool SetupBehaviorGroupDeleteConfirming { get; set; }
        public int SetupBehaviorGroupDeleteConfirmingDraftId { get; set; } = -1;
        public bool IsBehaviorTestSetup { get; set; }
        public bool CanFitSetupView { get; set; }
        public bool SetupSnapSelected { get; set; } = true;
        public bool EditGateSelected { get; set; }
        public bool PreviewOverlaySelected { get; set; } = true;
        public bool DetailsExpanded { get; set; }
        public bool LoopSelected { get; set; }
        public bool ShowSetupToolbar { get; set; }
        public bool ShowLiveToolbar { get; set; }
        public bool ShowPausedToolbar { get; set; }
        public bool ShowReviewState { get; set; }
        public bool ShowTimeline { get; set; }
        public bool ShowTimelineModeHost { get; set; }
        public bool ShowBehaviorPlan { get; set; }
        public bool ShowBehaviorLogic { get; set; }
        internal long TacticalPresentationRevision { get; set; }
        internal Func<string> TacticalFrameSummary { get; set; }
        internal Func<bool> HasTacticalTestFrame { get; set; }
        internal Action CreateTacticalBlank { get; set; }
        internal Action CreateTacticalTemplate { get; set; }
        internal Action ImportTacticalSource { get; set; }
        internal Action PrepareTacticalSummary { get; set; }
        internal Action RequeryTacticalSource { get; set; }
        public bool CanSwitchTimelineMode { get; set; }
        public bool RuntimeTraceModeSelected { get; set; }
        public bool BehaviorPlanModeSelected { get; set; }
        public bool BehaviorLogicModeSelected { get; set; }
        public bool RuntimeTraceExpanded { get; set; } = true;
        public bool BehaviorPlanExpanded { get; set; } = true;
        public bool TimelineExpanded { get; set; }
        public bool CanToggleTimelineExpanded { get; set; }
        public bool CanZoomTimeline { get; set; }
        public bool CanPanTimeline { get; set; }
        public bool ShowAdvancedDiagnostics { get; set; }
        public bool ShowWorkbench { get; set; }
        public bool CanEditWorkbench { get; set; }
        public bool CanUndoDraft { get; set; }
        public bool CanSaveDraft { get; set; }
        public bool CanApplyDraft { get; set; }
        public bool CanPublishDraft { get; set; }
        public bool CanSaveBehaviorGroup { get; set; }
        public bool CanReloadProjectDraft { get; set; }
        public bool CanReapplyProjectDraft { get; set; }
        public bool ShowProjectCardMetadata { get; set; }
        public bool CanEditProjectCardMetadata { get; set; }
        public string ProjectCardMetadataTitle { get; set; } = "项目行为卡元数据";
        public string ProjectCardDisplayName { get; set; } = string.Empty;
        public string ProjectCardCost { get; set; } = string.Empty;
        public string ProjectCardDescription { get; set; } = string.Empty;
        public string ProjectCardUseSummary { get; set; } = string.Empty;
        public string ProjectCardRiskSummary { get; set; } = string.Empty;
        public string ProjectCardTags { get; set; } = string.Empty;
        public int TimelineStartFrame { get; set; }
        public int TimelineEndFrame { get; set; }
        public int TimelineSelectedFrame { get; set; }
        public int TimelineRetainedStartFrame { get; set; }
        public int TimelineLoopStartFrame { get; set; }
        public int TimelineLoopEndFrame { get; set; }
        public bool TimelineHasLoopRange { get; set; }
        public bool TimelineFutureAdvanceActive { get; set; }
        public int TimelineFutureAdvanceStartFrame { get; set; }
        public int TimelineFutureAdvanceTargetFrame { get; set; }
    }
}
