namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns shell, timeline and inspector navigation state for the Runtime AI Lab panel.</summary>
    internal sealed class CharacterTestRuntimeAiLabPanelNavigationState
    {
        internal CharacterTestRuntimeAiLabWorkspaceMode WorkspaceMode { get; set; } =
            CharacterTestRuntimeAiLabWorkspaceMode.Setup;
        internal CharacterTestRuntimeAiLabDiagnosticsTab DiagnosticsTab { get; set; } =
            CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
        internal CharacterTestRuntimeAiLabTimelineMode TimelineMode { get; set; } =
            CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
        internal CharacterTestRuntimeAiLabWorkspaceMode WorkspaceBeforeDiagnostics { get; set; } =
            CharacterTestRuntimeAiLabWorkspaceMode.Setup;
        internal int SelectedWorkbenchEntryIndex { get; set; }
        internal int WorkbenchScrollOffset { get; set; }
        internal int SelectedCompiledStepIndex { get; set; }
        internal CharacterTestRuntimeAiLabLogicElementKind SelectedLogicElementKind { get; set; } =
            CharacterTestRuntimeAiLabLogicElementKind.Stage;
        internal int SelectedLogicElementIndex { get; set; }
        internal bool HistoricalPresentationSelectionCaptured { get; set; }
        internal int LatestSelectedWorkbenchEntryIndex { get; set; }
        internal int LatestSelectedCompiledStepIndex { get; set; }
        internal CharacterTestRuntimeAiLabLogicElementKind LatestSelectedLogicElementKind { get; set; }
        internal int LatestSelectedLogicElementIndex { get; set; }
        internal CharacterTestRuntimeAiLabReplayReturnToken ReplayReturnToken { get; set; }
        internal bool FollowCurrentRuntimeBehavior { get; set; } = true;
    }
}
