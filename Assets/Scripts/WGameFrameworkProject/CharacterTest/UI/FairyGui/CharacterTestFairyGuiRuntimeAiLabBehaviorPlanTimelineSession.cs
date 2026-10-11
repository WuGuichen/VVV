using System;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns selection and inspector state for the read-only behavior-plan timeline.</summary>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineSession
    {
        internal CharacterTestRuntimeAiLabLogicElementKind SelectedKind { get; private set; }
        internal string SelectedStableId { get; private set; } = string.Empty;
        internal GameplayReadablePlanDetailKind DetailKind { get; set; }
        internal string DetailStableId { get; set; } = string.Empty;
        internal int PreviousBehaviorEntryIndex { get; set; } = -1;
        internal int CommittedBehaviorEntryIndex { get; set; } = -1;
        internal int NextBehaviorEntryIndex { get; set; } = -1;

        internal void ResolveSelection(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors,
            int detectorCount,
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions,
            int transitionCount)
        {
            SelectedKind = viewModel.SelectedElementKind;
            int index = viewModel.SelectedElementIndex;
            SelectedStableId = string.Empty;
            if (SelectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                && index >= 0 && index < stageCount)
                SelectedStableId = stages[index]?.StageId ?? string.Empty;
            else if (SelectedKind == CharacterTestRuntimeAiLabLogicElementKind.Detector
                && index >= 0 && index < detectorCount)
                SelectedStableId = detectors[index]?.DetectorId ?? string.Empty;
            else if (SelectedKind == CharacterTestRuntimeAiLabLogicElementKind.Transition
                && index >= 0 && index < transitionCount)
                SelectedStableId = transitions[index]?.TransitionId ?? string.Empty;
        }

        internal void Reset()
        {
            SelectedKind = CharacterTestRuntimeAiLabLogicElementKind.None;
            SelectedStableId = string.Empty;
            DetailKind = GameplayReadablePlanDetailKind.None;
            DetailStableId = string.Empty;
            PreviousBehaviorEntryIndex = -1;
            CommittedBehaviorEntryIndex = -1;
            NextBehaviorEntryIndex = -1;
        }
    }
}
