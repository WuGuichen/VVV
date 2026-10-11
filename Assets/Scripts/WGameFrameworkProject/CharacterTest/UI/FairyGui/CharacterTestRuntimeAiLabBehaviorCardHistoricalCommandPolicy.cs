using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabHistoricalShellCommandDisposition
    {
        UnknownDenied = 0,
        ReadOnlyAllowed = 1,
        AuthoringDenied = 2
    }

    /// <summary>
    /// Single deny-by-default classification boundary for Behavior Card commands while a retained
    /// RuntimeFrame is selected. Selection and viewport commands are presentation-only; every
    /// authoring command is rejected before it can reach Draft/controller mutation code.
    /// </summary>
    internal static class CharacterTestRuntimeAiLabBehaviorCardHistoricalCommandPolicy
    {
        internal const string ReadOnlyDiagnostic = "HISTORICAL_FRAME_READ_ONLY";

        internal static int RegisteredWorkspaceCommandCount => 40;

        internal static string GetRegisteredWorkspaceCommandId(int index)
        {
            switch (index)
            {
                case 0: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.OpenLibraryCard;
                case 1: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage;
                case 2: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector;
                case 3: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition;
                case 4: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStage;
                case 5: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCardDetector;
                case 6: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageDetector;
                case 7: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddStageBranchAtFrame;
                case 8: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SetEntryStage;
                case 9: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateDetector;
                case 10: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateBranchEvent;
                case 11: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CanonicalizeDetectorWindows;
                case 12: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransition;
                case 13: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorFact;
                case 14: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorTrigger;
                case 15: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorWindowMode;
                case 16: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.MoveDetectorWindowStart;
                case 17: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ResizeDetectorWindow;
                case 18: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AdjustDetectorPriority;
                case 19: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionPolicy;
                case 20: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget;
                case 21: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement;
                case 22: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut;
                case 23: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn;
                case 24: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline;
                case 25: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineLeft;
                case 26: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineRight;
                case 27: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineUp;
                case 28: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineDown;
                case 29: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ToggleStageSnap;
                case 30: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateStages;
                case 31: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteStages;
                case 32: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.UndoStageEdit;
                case 33: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RedoStageEdit;
                case 34: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransitionToStage;
                case 35: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.EditTriggerEvent;
                case 36: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DeleteTriggerEvent;
                case 37: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddInstantEventAtFrame;
                case 38: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddDurationEventAtFrame;
                case 39: return CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddCharacterActionEventAtFrame;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        internal static bool IsWorkspaceCommandReadOnlyAllowed(string commandId)
        {
            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.OpenLibraryCard:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectStage:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineOut:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ZoomTimelineIn:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.FitTimeline:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineLeft:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.PanTimelineRight:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineUp:
                case CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ScrollTimelineDown:
                    return true;
                default:
                    return false;
            }
        }

        internal static CharacterTestRuntimeAiLabHistoricalShellCommandDisposition ClassifyShellCommand(
            string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.UnknownDenied;
            if (commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntryPrefix,
                    StringComparison.Ordinal)
                || commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ScrollWorkbenchPrefix,
                    StringComparison.Ordinal)
                || commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectCompiledStepPrefix,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed;
            }
            if (commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupSlotPrefix,
                    StringComparison.Ordinal)
                || commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .SelectSetupBehaviorGroupPrefix,
                    StringComparison.Ordinal)
                || commandId.StartsWith(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .SelectSetupCharacterPrefix,
                    StringComparison.Ordinal))
            {
                return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.AuthoringDenied;
            }

            switch (commandId)
            {
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack10:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepForward10:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FirstFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.JumpFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleDetails:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateBehaviorNode:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsLocateRuntimeFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                    .BehaviorTransitionLocateRuntimeFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsReturnSourceFrame:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSearch:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleChanged:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleParticipated:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsToggleDelta:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowFacts:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsShowCandidates:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelect:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsTogglePin:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsScroll:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FactsSelectAgent:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellHide:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineToggleExpanded:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomOut:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomIn:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineFit:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanLeft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanRight:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineSelectItem:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineNavigateItem:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineClearItemSelection:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubBegin:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubUpdate:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubEnd:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineScrubCancel:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.EditSelectedBehaviorData:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.IncreasePlaybackSpeed:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DecreasePlaybackSpeed:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecomputeCancel:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayCancel:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayRetry:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayReturn:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayLocateDivergence:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenBehaviorCard:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaySelectItem:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayNavigateItem:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayClearItemSelection:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomOut:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomIn:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFit:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanLeft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanRight:
                    return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.ReadOnlyAllowed;

                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousAgent:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextAgent:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RuntimeTestSelectAgent:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleStateCapture:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ValidateState:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SyncRecordedState:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ColdBootstrapVerify:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Open:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellRestore:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ShellReset:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FrameLimit:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSetupSnap:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MirrorSpawns:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ResetSpawnDefaults:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSelectedSetupSlot:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupCharacter:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupCharacter:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CreateSetupBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteSetupBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveSetupBehaviorGroupBasicInfo:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplaceCoreSlot:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReassignEntryResponsibility:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetEntryResponsibilities:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryUp:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MoveEntryDown:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteProjectCard:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.UndoDraft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveBehaviorGroup:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReloadProjectDraft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReapplyProjectDraft:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDisplayName:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardCost:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardDescription:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardUseSummary:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardRiskSummary:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetProjectCardTags:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayStopDistance:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayThreatWindow:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SetGameplayDodgeCooldown:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectAuthoringWorkspaceRole:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenCharacterActionAuthoring:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.RetryCharacterActionApply:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceCommand:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspaceClose:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.CharacterActionWorkspacePreview:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupBehaviorGroupPrefix:
                case CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupCharacterPrefix:
                    return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.AuthoringDenied;
                default:
                    return CharacterTestRuntimeAiLabHistoricalShellCommandDisposition.UnknownDenied;
            }
        }
    }
}
