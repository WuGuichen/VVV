using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabActiveActionProgress
    {
        internal CharacterTestRuntimeAiLabActiveActionProgress(
            int entityId,
            long instanceId,
            CharacterActionId actionId,
            int localFrame,
            int durationFrames)
        {
            EntityId = entityId;
            InstanceId = Math.Max(0L, instanceId);
            ActionId = actionId;
            LocalFrame = Math.Max(0, localFrame);
            DurationFrames = Math.Max(0, durationFrames);
        }

        internal int EntityId { get; }
        internal long InstanceId { get; }
        internal CharacterActionId ActionId { get; }
        internal int LocalFrame { get; }
        internal int DurationFrames { get; }
        internal bool Available => EntityId > 0 && InstanceId > 0L && ActionId.IsValid;
        internal bool Determinate => Available && DurationFrames > 0;
        internal int CoveredFrames => !Available
            ? 0
            : DurationFrames > 0
                ? Math.Min(DurationFrames, LocalFrame + 1)
                : LocalFrame + 1;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
    {
        private readonly CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel[] _configurationFields =
        {
            new CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel(),
            new CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel(),
            new CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel(),
            new CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel(),
            new CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel()
        };

        public string StableCardId { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UseText { get; set; } = string.Empty;
        public string RiskText { get; set; } = string.Empty;
        public string TagsText { get; set; } = string.Empty;
        public string CostText { get; set; } = string.Empty;
        public string RevisionText { get; set; } = string.Empty;
        public int PublishedRevision { get; set; }
        public CharacterTestRuntimeAiLabPublishUxState PublishState { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public bool HasValidationProblems { get; set; }
        public string ValidationStatusText { get; set; } = string.Empty;
        public string BlockReasonText { get; set; } = string.Empty;
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CompatibleResponsibilities { get; set; } =
            Array.Empty<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; set; } =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
        public string ResponsibilitySummaryText { get; set; } = string.Empty;
        public int ResponsibilityCompatibilityMask { get; set; }
        public int EntryIndex { get; set; } = -1;
        public CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel[] ConfigurationFields =>
            _configurationFields;
        public int ConfigurationFieldCount { get; set; }
        public string ConfigurationSourceText { get; set; } = string.Empty;
        public string LogicStructureText { get; set; } = string.Empty;
        public int ReferencingGroupCount { get; set; }
        public int ReferenceCount { get; set; }
        public string ReferenceSummaryText { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabLogicStageViewModel PrimaryStage { get; } =
            new CharacterTestRuntimeAiLabLogicStageViewModel();
        public bool HasPrimaryStage { get; set; }
        public bool ConfigurationEditable { get; set; }
        public bool DisplayNameEditable { get; set; }
        public bool HasUnsavedMetadataChanges { get; set; }
        public bool ResponsibilityEditable { get; set; }
        public int ObservedTargetDistanceDecimeters { get; set; } = -1;
        public bool Available { get; set; }
        public bool Unlocked { get; set; }
        public bool Installed { get; set; }
        public bool InCurrentBehaviorGroup { get; set; }
        public bool CurrentEdit { get; set; }
        public bool RuntimeActive { get; set; }
        public bool Draft { get; set; }
        public int PinnedRevision { get; set; }
        public bool HasNewerRevision { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardConfigurationFieldViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel
    {
        public CharacterTestRuntimeAiLabBehaviorAssetCardViewModel[] Cards { get; set; } =
            Array.Empty<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>();
        public string Query { get; set; } = string.Empty;
        public CharacterTestRuntimeAiLabBehaviorCardLibraryFilter Filter { get; set; }
        public string HeaderText { get; set; } = "行为卡牌库";
        public string SummaryText { get; set; } = string.Empty;
        public string EmptyText { get; set; } = string.Empty;
        public bool Available { get; set; }
        public int FirstVisibleIndex { get; set; }
        public int VisibleCapacity { get; set; } = 4;
        public int FocusedIndex { get; set; } = -1;
        public int ActiveCount { get; set; }
        internal long CachedStructureKey { get; set; }
        internal bool HasCachedStructure { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel
    {
        internal BehaviorPlanStructureViewModel Structure { get; } =
            new BehaviorPlanStructureViewModel();
        internal BehaviorPlanRuntimeOverlayViewModel RuntimeOverlay { get; } =
            new BehaviorPlanRuntimeOverlayViewModel();
        internal BehaviorPlanPresentationState Presentation { get; } =
            new BehaviorPlanPresentationState();
        internal BehaviorRecipeTriggerEventInspectorViewModel RecipeInspector { get; set; }
        internal BehaviorRecipeTriggerEventInspectorViewModel TriggerEventInspector
        {
            get => RecipeInspector;
            set => RecipeInspector = value;
        }
        internal CharacterTestTacticalAuthoringPreview TacticalPreview { get; set; }
        internal CharacterTestTacticalCompositionEditingSession CompositionEditor { get; set; }
        internal bool InspectCompiledExecution => CompositionEditor?.NativeExecutionView == true;
        internal CharacterTestNativeExecutionView NativeExecution { get; set; }
        internal CharacterTestRuntimeAiLabBehaviorCardLogicPayload RecipePayload { get; set; }
        internal CharacterTestRuntimeAiLabCompiledRecipeV6 CompiledRecipeV6 { get; set; }
        internal CharacterTestRuntimeAiLabLogicAgentStateSnapshot RuntimeState { get; set; }
        internal BehaviorRecipeRuntimeCursor RuntimeCursor { get; } =
            new BehaviorRecipeRuntimeCursor();
        internal BehaviorRecipeCharacterActionOptionViewModel[] CharacterActionOptions { get; set; } =
            Array.Empty<BehaviorRecipeCharacterActionOptionViewModel>();
        internal long CharacterActionCatalogHash { get; set; }

        public CharacterTestRuntimeAiLabLogicStageViewModel[] Stages
        {
            get => Structure.Stages;
            set => Structure.Stages = value ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
        }
        public CharacterTestRuntimeAiLabLogicDetectorViewModel[] Detectors
        {
            get => Structure.Detectors;
            set => Structure.Detectors = value ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
        }
        public CharacterTestRuntimeAiLabLogicTransitionViewModel[] Transitions
        {
            get => Structure.Transitions;
            set => Structure.Transitions = value ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
        }
        public int StageCount { get => Structure.StageCount; set => Structure.StageCount = value; }
        public int DetectorCount { get => Structure.DetectorCount; set => Structure.DetectorCount = value; }
        public int TransitionCount { get => Structure.TransitionCount; set => Structure.TransitionCount = value; }
        public CharacterTestRuntimeAiLabLogicElementKind SelectedElementKind
        {
            get => Presentation.SelectedElementKind;
            set => Presentation.SelectedElementKind = value;
        }
        public int SelectedElementIndex
        {
            get => Presentation.SelectedElementIndex;
            set => Presentation.SelectedElementIndex = value;
        }
        public bool Available { get; set; }
        public int DurationFrames { get => Structure.DurationFrames; set => Structure.DurationFrames = value; }
        public string CardTitleText { get; set; } = string.Empty;
        public string SemanticHashText { get; set; } = "hash --------";
        public string SummaryText { get; set; } = string.Empty;
        public string DiagnosticsText { get; set; } = string.Empty;
        public string RuntimeStatusText { get; set; } = string.Empty;
        public string ValidationBadgeText { get; set; } = string.Empty;
        public string CommandFeedbackText { get; set; } = string.Empty;
        public int ActivationConditionCount { get; set; }
        public string ActivationConditionSummaryText { get; set; } = string.Empty;
        public bool HasBlockingDiagnostics { get; set; }
        public bool RequiresDetectorWindowCanonicalization { get; set; }
        public bool CanEdit { get; set; }
        public bool CanEditCardDisplayName { get; set; }
        public bool CanEditResponsibilities { get; set; }
        public int EntryIndex { get; set; } = -1;
        public int ResponsibilityCompatibilityMask { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; set; } =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
        internal string BehaviorEntryId { get; set; } = string.Empty;
        public int HorizontalStartFrame
        {
            get => Presentation.HorizontalStartFrame;
            set => Presentation.HorizontalStartFrame = value;
        }
        public int HorizontalEndFrame
        {
            get => Presentation.HorizontalEndFrame;
            set => Presentation.HorizontalEndFrame = value;
        }
        public float ZoomLevel { get => Presentation.ZoomLevel; set => Presentation.ZoomLevel = value; }
        public float VerticalScrollOffset
        {
            get => Presentation.VerticalScrollOffset;
            set => Presentation.VerticalScrollOffset = value;
        }
        public bool FollowCurrent { get => Presentation.FollowCurrent; set => Presentation.FollowCurrent = value; }
        public bool Historical { get => RuntimeOverlay.Historical; set => RuntimeOverlay.Historical = value; }
        public int RuntimeFrame { get => RuntimeOverlay.RuntimeFrame; set => RuntimeOverlay.RuntimeFrame = value; }
        public bool HistoricalEventEvidenceComplete
        {
            get => RuntimeOverlay.HistoricalEventEvidenceComplete;
            set => RuntimeOverlay.HistoricalEventEvidenceComplete = value;
        }
        public int HistoricalFrame
        {
            get => RuntimeOverlay.HistoricalFrame;
            set => RuntimeOverlay.HistoricalFrame = value;
        }
        public int HistoricalLatestFrame
        {
            get => RuntimeOverlay.HistoricalLatestFrame;
            set => RuntimeOverlay.HistoricalLatestFrame = value;
        }
        public string HistoricalDiagnosticCode
        {
            get => RuntimeOverlay.HistoricalDiagnosticCode;
            set => RuntimeOverlay.HistoricalDiagnosticCode = value ?? string.Empty;
        }
        internal string CharacterCommandText { get; set; } = string.Empty;
        internal string CurrentBehaviorTitleText { get; set; } = string.Empty;
        internal string CurrentBehaviorFactText { get; set; } = string.Empty;
        internal string CurrentBehaviorCommandText { get; set; } = string.Empty;
        internal string CurrentBehaviorWarningText { get; set; } = string.Empty;
        internal string CurrentBehaviorTooltipText { get; set; } = string.Empty;
        internal string CurrentBehaviorRepairDetectorStableId { get; set; } = string.Empty;
        internal bool CurrentBehaviorAvailable { get; set; }
        internal bool CurrentBehaviorWarningVisible { get; set; }
        internal bool BehaviorRelationshipAvailable { get; set; }
        internal string PreviousBehaviorText { get; set; } = string.Empty;
        internal string PreviousBehaviorRelationText { get; set; } = string.Empty;
        internal string PreviousBehaviorTooltipText { get; set; } = string.Empty;
        internal int PreviousBehaviorEntryIndex { get; set; } = -1;
        internal string CommittedBehaviorText { get; set; } = string.Empty;
        internal string CommittedBehaviorTooltipText { get; set; } = string.Empty;
        internal int CommittedBehaviorEntryIndex { get; set; } = -1;
        internal string NextBehaviorText { get; set; } = string.Empty;
        internal string NextBehaviorRelationText { get; set; } = string.Empty;
        internal string NextBehaviorTooltipText { get; set; } = string.Empty;
        internal int NextBehaviorEntryIndex { get; set; } = -1;
        internal bool NextBehaviorPending { get; set; }
        internal CharacterTestRuntimeAiLabBehaviorCandidateViewModel[] BehaviorCandidates
        {
            get => RuntimeOverlay.Candidates;
            set => RuntimeOverlay.Candidates = value ?? Array.Empty<CharacterTestRuntimeAiLabBehaviorCandidateViewModel>();
        }
        internal int BehaviorCandidateCount
        {
            get => RuntimeOverlay.CandidateCount;
            set => RuntimeOverlay.CandidateCount = value;
        }
        internal string BehaviorSuccessExitText { get; set; } = "完成";
        internal string BehaviorFailureExitText { get; set; } = "失败";
        internal CharacterTestRuntimeAiLabActiveActionProgress ActiveActionProgress
        {
            get => RuntimeOverlay.ActiveActionProgress;
            set => RuntimeOverlay.ActiveActionProgress = value;
        }
        internal int CardStartedFrame
        {
            get => RuntimeOverlay.CardStartedRuntimeFrame;
            set => RuntimeOverlay.CardStartedRuntimeFrame = value;
        }
        internal int StageEnteredFrame
        {
            get => RuntimeOverlay.StageEnteredRuntimeFrame;
            set => RuntimeOverlay.StageEnteredRuntimeFrame = value;
        }
        internal int ActionEnteredFrame
        {
            get => RuntimeOverlay.ActionEnteredRuntimeFrame;
            set => RuntimeOverlay.ActionEnteredRuntimeFrame = value;
        }
        internal int PrimaryActionState
        {
            get => RuntimeOverlay.PrimaryActionState;
            set => RuntimeOverlay.PrimaryActionState = value;
        }
        internal int PrimaryActionStageIndex
        {
            get => RuntimeOverlay.PrimaryActionStageIndex;
            set => RuntimeOverlay.PrimaryActionStageIndex = value;
        }
        internal int ObservedTargetDistanceDecimeters
        {
            get => RuntimeOverlay.ObservedTargetDistanceDecimeters;
            set => RuntimeOverlay.ObservedTargetDistanceDecimeters = value;
        }
        internal int[] DetectorOrderScratch { get; set; } = Array.Empty<int>();
        internal long CachedStructureKey
        {
            get => Structure.CachedStructureKey;
            set => Structure.CachedStructureKey = value;
        }
        internal bool HasCachedStructure
        {
            get => Structure.HasCachedStructure;
            set => Structure.HasCachedStructure = value;
        }
        internal long AutoFocusedDiagnosticKey { get; set; }
        internal bool HasAutoFocusedDiagnosticKey { get; set; }
    }

    /// <summary>
    /// Read-only cursor evidence projected from the installed v6 runtime snapshot.
    /// Selection and editor presentation never manufacture this state.
    /// </summary>
    internal sealed class BehaviorRecipeRuntimeCursor
    {
        internal bool Available { get; private set; }
        internal bool AuthorityMatched { get; private set; }
        internal bool Historical { get; private set; }
        internal int RuntimeFrame { get; private set; } = -1;
        internal string TimelineId { get; private set; } = string.Empty;
        internal int TimelineIndex { get; private set; } = -1;
        internal int TimelineEnteredFrame { get; private set; } = -1;
        internal int LocalFrame { get; private set; } = -1;
        internal CharacterTestRuntimeAiLabLogicRuntimeState RuntimeState { get; private set; }

        internal void Populate(
            CharacterTestRuntimeAiLabCompiledRecipeV6 recipe,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot state,
            long compiledSemanticHash,
            string expectedBehaviorEntryId,
            int runtimeFrame,
            bool historical)
        {
            Clear();
            Historical = historical;
            RuntimeFrame = runtimeFrame;
            if (recipe == null || state == null)
                return;

            RuntimeState = (CharacterTestRuntimeAiLabLogicRuntimeState)state.RuntimeState;
            AuthorityMatched = compiledSemanticHash != 0L
                && state.ProgramSemanticHash == compiledSemanticHash
                && (string.IsNullOrEmpty(expectedBehaviorEntryId)
                    || string.Equals(
                        state.BehaviorCardStableId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        expectedBehaviorEntryId,
                        StringComparison.Ordinal));
            int timelineIndex = state.CurrentTimelineIndex;
            if (!AuthorityMatched
                || (RuntimeState != CharacterTestRuntimeAiLabLogicRuntimeState.Running
                    && RuntimeState != CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning)
                || timelineIndex < 0
                || timelineIndex >= recipe.Timelines.Count
                || state.LocalFrame < 0)
            {
                return;
            }

            CharacterTestRuntimeAiLabCompiledRecipeV6Timeline timeline =
                recipe.Timelines[timelineIndex];
            if (timeline == null || string.IsNullOrEmpty(timeline.TimelineId))
                return;

            TimelineIndex = timelineIndex;
            TimelineId = timeline.TimelineId;
            TimelineEnteredFrame = state.TimelineEnteredFrame;
            LocalFrame = state.LocalFrame;
            Available = true;
        }

        internal void Clear()
        {
            Available = false;
            AuthorityMatched = false;
            Historical = false;
            RuntimeFrame = -1;
            TimelineId = string.Empty;
            TimelineIndex = -1;
            TimelineEnteredFrame = -1;
            LocalFrame = -1;
            RuntimeState = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCandidateViewModel
    {
        internal const int MaxVisibleConditions = 3;

        internal int EntryIndex { get; set; } = -1;
        internal string StableId { get; set; } = string.Empty;
        internal string Title { get; set; } = string.Empty;
        internal string ScoreText { get; set; } = string.Empty;
        internal string TooltipText { get; set; } = string.Empty;
        internal bool Selected { get; set; }
        internal bool Committed { get; set; }
        internal bool Pending { get; set; }
        internal bool Eligible { get; set; }
        internal bool Evaluated { get; set; }
        internal bool ScoreAvailable { get; set; }
        internal float Score { get; set; }
        internal string[] ConditionTexts { get; } = new string[MaxVisibleConditions];
        internal bool[] ConditionSatisfied { get; } = new bool[MaxVisibleConditions];
        internal bool[] ConditionKnown { get; } = new bool[MaxVisibleConditions];
        internal int ConditionCount { get; set; }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel
    {
        public CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel Library { get; } =
            new CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel();
        public CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel Timeline { get; } =
            new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel();
        public long WorkspaceRevision { get; set; }
    }
}
