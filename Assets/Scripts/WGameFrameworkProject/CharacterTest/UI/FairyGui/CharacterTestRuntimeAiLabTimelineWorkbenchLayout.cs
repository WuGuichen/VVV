namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Shared presentation geometry for the Runtime AI Lab timeline workbench.
    /// All three workbench modes keep this shell and only replace their toolbar actions
    /// and track projection.
    /// </summary>
    /// <remarks>NoAlloc: compile-time presentation constants only.</remarks>
    internal static class CharacterTestRuntimeAiLabTimelineWorkbenchLayout
    {
        internal const float DesignWidth = 1920f;
        // Behavior Logic keeps the time-content boundary at x=620, but the
        // component root starts 120 px earlier so it can own the vertical
        // command rail beside the narrower behavior list.
        internal const float BehaviorLogicSidebarWidth = 620f;
        internal const float BehaviorLogicCommandRailWidth = 120f;
        internal const float BehaviorLogicListPanelWidth =
            BehaviorLogicSidebarWidth - BehaviorLogicCommandRailWidth;
        internal const float BehaviorLogicTimelineRootX = BehaviorLogicListPanelWidth;
        internal const float BehaviorLogicTimelineRootWidth =
            DesignWidth - BehaviorLogicTimelineRootX;
        internal const float BehaviorLogicTimelineContentX = BehaviorLogicCommandRailWidth;
        internal const float BehaviorLogicTimelineWidth =
            DesignWidth - BehaviorLogicSidebarWidth;
        internal const float RuntimeTimelineY = 0f;
        internal const float RuntimeTimelineHeight = 1080f;

        // Keep the timeline close to two thirds of its previous 608 px height.
        // Anchoring it to the bottom lets the decision and content surfaces consume the freed space.
        internal const float WorkbenchHeight = 412f;
        internal const float WorkbenchY = RuntimeTimelineHeight - WorkbenchHeight;
        internal const float HeaderHeight = 72f;
        internal const float RulerHeight = 40f;
        internal const float TrackAreaHeight = WorkbenchHeight - HeaderHeight - RulerHeight;

        internal const float ModeHostX = 16f;
        internal const float ModeHostY = WorkbenchY - 48f;
        internal const float ModeHostWidth = 444f;
        internal const float ModeButtonYInset = 4f;
        internal const float ModeButtonWidth = 142f;
        internal const float ModeButtonHeight = 38f;
        internal const float LogicModeButtonX = 20f;
        internal const float PlanModeButtonX = 164f;
        internal const float RuntimeModeButtonX = 308f;
        internal const int ModeButtonFontSize = 16;

        internal const float TimelineTitleX = 340f;
        internal const float TimelineTitleWidth = 300f;
        internal const float TimelineControlYInset = 10f;
        internal const float TimelineControlHeight = 52f;

        internal const float BehaviorLogicPanelPadding = 16f;
        internal const float BehaviorLogicFloatingControlYInset = 10f;
        internal const float BehaviorLogicFloatingControlHeight = 36f;
        internal const float BehaviorLogicTopCommandWidth = 98f;
        internal const float BehaviorLogicTopCommandGap = 8f;
        internal const float BehaviorLogicTopBarHeight = 52f;
        internal const float BehaviorLogicCanvasRightPadding = 20f;
        internal const float BehaviorLogicAddStageWidth = 126f;
        internal const float BehaviorLogicAddDetectorWidth = 150f;
        internal const float BehaviorLogicStructureActionHeight = 36f;
        internal const float BehaviorLogicAddCardWidth = 126f;
        internal const float BehaviorLogicSaveGroupWidth = 110f;
        internal const float BehaviorLogicSaveWidth = BehaviorLogicTopCommandWidth;
        internal const float BehaviorLogicResponsibilityTabsTopInset = 52f;
        internal const float BehaviorLogicResponsibilityTabHeight = 30f;
        internal const float BehaviorLogicResponsibilityTabGap = 4f;
        internal const float BehaviorLogicListTopInset = 90f;
        internal const float BehaviorLogicListBottomInset = 12f;
        internal const float BehaviorLogicListWidth =
            BehaviorLogicListPanelWidth - BehaviorLogicPanelPadding * 2f;
        internal const float BehaviorLogicCommandRailButtonX =
            (BehaviorLogicCommandRailWidth - BehaviorLogicTopCommandWidth) * 0.5f;
        internal const float BehaviorLogicCommandRailUndoY = 62f;
        internal const float BehaviorLogicCommandRailButtonStep = 38f;
        internal const float BehaviorLogicCommandRailRedoY = 102f;
        internal const float BehaviorLogicCommandRailSaveY = 148f;
        internal const float BehaviorLogicCommandRailPublishY = 188f;
        internal const float BehaviorLogicCommandRailCreateCardY = 236f;
        internal const float BehaviorLogicCommandRailDeleteCardY =
            BehaviorLogicCommandRailCreateCardY + BehaviorLogicCommandRailButtonStep;

        internal const float LabelWidth = 224f;
        internal const float CanvasRightPadding = 20f;
        internal const float CanvasWidth = DesignWidth - LabelWidth - CanvasRightPadding;

        internal const float DecisionPanelX = 1400f;
        internal const float DecisionPanelWidth = 520f;
        internal const float DecisionPanelHeight = WorkbenchY - RuntimeTimelineY;
        internal const float SessionHeaderHeight = 60f;
        internal const float DecisionContentY = SessionHeaderHeight;
        internal const float DecisionContentHeight =
            DecisionPanelHeight - DecisionContentY;

        internal const float RuntimeHeaderY = WorkbenchY - RuntimeTimelineY;
        internal const float RuntimeViewportY = RuntimeHeaderY + HeaderHeight;
        internal const float RuntimeViewportHeight = WorkbenchHeight - HeaderHeight;
        internal const int RuntimeTrackCount = 5;
        internal const float RuntimeTrackHeight = TrackAreaHeight / RuntimeTrackCount;
    }
}
