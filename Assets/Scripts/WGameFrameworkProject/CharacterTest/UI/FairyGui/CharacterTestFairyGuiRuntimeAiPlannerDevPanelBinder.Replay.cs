using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private void BindReplayWorkspace(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent replay = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReplayWorkspace)?.asCom;
            if (replay == null)
                return;
            bool visible = viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay;
            replay.visible = visible;
            replay.touchable = visible;
            if (!visible)
            {
                _timelineWorkspaceBinder.HideReplayPooledTimelineItems();
                return;
            }

            CharacterTestRuntimeAiLabReplayWorkspaceViewModel workspace = viewModel.Replay;
            BindText(replay, "replayHeader", "Replay Validation Workspace");
            BindText(replay, "replayState", workspace.StateLabel);
            BindText(replay, "replayStatus", workspace.StatusText);
            BindText(replay, "replayRecordMetadata", workspace.RecordMetadataText);
            BindText(replay, "replayBootstrapIdentity", workspace.BootstrapIdentityText);
            BindText(replay, "replayAuthorityIdentity", workspace.AuthorityIdentityText);
            BindText(replay, "replayResultCode", workspace.ResultCode);
            BindText(replay, "replayResultSummary", workspace.DetailText);
            BindText(
                replay,
                "replayResultEvidence",
                string.IsNullOrEmpty(workspace.ResultEvidenceText)
                    ? workspace.IdentityText
                    : workspace.ResultEvidenceText);
            BindText(
                replay,
                "replayProgressText",
                Mathf.RoundToInt(workspace.Progress01 * 100f) + "%");
            BindButton(
                replay,
                "btnReplayFreeze",
                "Freeze",
                workspace.CanFreeze,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze);
            BindButton(
                replay,
                "btnReplayValidate",
                "Validate",
                workspace.CanValidate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate);
            BindButton(
                replay,
                "btnReplayCancel",
                "Cancel",
                workspace.CanCancel,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayCancel);
            BindButton(
                replay,
                "btnReplayRetry",
                "Retry",
                workspace.CanRetry,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayRetry);
            BindButton(
                replay,
                "btnReplayReturn",
                "Return to source",
                workspace.CanReturn,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayReturn);
            BindButton(
                replay,
                "btnReplayLocateDivergence",
                "Locate divergence",
                workspace.CanLocateDivergence,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayLocateDivergence);
            BindButton(
                replay,
                "btnReplayOpenFacts",
                "Facts",
                workspace.CanOpenFacts,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts);
            BindButton(
                replay,
                "btnReplayOpenBehaviorCard",
                "Behavior Card",
                workspace.CanOpenBehaviorCard,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenBehaviorCard);
            bool hasTimeline = workspace.TimelineProjection != null;
            bool manualViewport = hasTimeline
                && workspace.TimelineProjection.Viewport.Mode
                    == CharacterTestRuntimeAiLabTimelineViewportMode.Manual;
            BindButton(
                replay,
                "btnReplayZoomOut",
                "−",
                hasTimeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomOut);
            BindButton(
                replay,
                "btnReplayZoomIn",
                "+",
                hasTimeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayZoomIn);
            BindButton(
                replay,
                "btnReplayFit",
                "Fit",
                hasTimeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFit);
            BindButton(
                replay,
                "btnReplayPanLeft",
                "◀",
                manualViewport,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanLeft);
            BindButton(
                replay,
                "btnReplayPanRight",
                "▶",
                manualViewport,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayPanRight);

            bool busy = workspace.State == CharacterTestReplayValidationState.InspectingRecord
                || workspace.State == CharacterTestReplayValidationState.RestoringSessionAuthorityBootstrap
                || workspace.State == CharacterTestReplayValidationState.RestoringGameplayBootstrap
                || workspace.State == CharacterTestReplayValidationState.ComparingFrames;
            SetTimelineChildVisible(replay, "btnReplayFreeze", !busy);
            SetTimelineChildVisible(
                replay,
                "btnReplayValidate",
                workspace.State == CharacterTestReplayValidationState.Ready);
            SetTimelineChildVisible(replay, "btnReplayCancel", busy);
            SetTimelineChildVisible(
                replay,
                "btnReplayRetry",
                workspace.State == CharacterTestReplayValidationState.Passed
                || workspace.State == CharacterTestReplayValidationState.Diverged
                || workspace.State == CharacterTestReplayValidationState.Failed
                || workspace.State == CharacterTestReplayValidationState.Cancelled);

            Fgui.GComponent viewport = replay.GetChild("replayTimelineViewport")?.asCom;
            if (viewport != null)
            {
                viewport.touchable = true;
                _timelineWorkspaceBinder.EnsureReplayTimelinePoolsPreheated();
                if (workspace.TimelineProjection != null)
                    _timelineWorkspaceBinder.RenderReplayTimelineProjection(viewport, viewModel);
                else
                    BindReplayTrackAbsence(viewport, workspace);
                SetTimelineChildVisible(
                    viewport,
                    "timelinePlayhead",
                    workspace.TimelineProjection != null);
                SetTimelineChildVisible(
                    viewport,
                    "timelineSelectedFrameLabel",
                    workspace.TimelineProjection != null);
                SetTimelineChildVisible(viewport, "timelinePreviewPlayhead", false);
                SetTimelineChildVisible(viewport, "timelinePreviewFrameLabel", false);
                SetTimelineChildVisible(viewport, "timelineLoopRange", false);
                SetTimelineChildVisible(viewport, "timelineRetainedMask", false);
                SetTimelineChildVisible(viewport, "timelineFutureMask", false);
                SetTimelineChildVisible(viewport, "timelineFutureAdvanceFill", false);
            }

            Fgui.GObject progress = replay.GetChild("replayProgressFill");
            Fgui.GObject progressTrack = replay.GetChild("replayProgressTrack");
            if (progress != null && progressTrack != null)
            {
                progress.visible = workspace.State == CharacterTestReplayValidationState.ComparingFrames;
                progress.width = Mathf.Max(0f, progressTrack.width * Mathf.Clamp01(workspace.Progress01));
            }
        }

        private static void BindReplayTrackAbsence(
            Fgui.GComponent viewport,
            CharacterTestRuntimeAiLabReplayWorkspaceViewModel workspace)
        {
            for (int i = 0; i < TimelineTrackNames.Length; i++)
            {
                Fgui.GComponent track = viewport.GetChild(TimelineTrackNames[i])?.asCom;
                if (track == null)
                    continue;
                BindText(track, "trackLabel", TrackLabel((CharacterTestRuntimeAiLabTimelineTrackKind)i));
                BindText(track, "trackUnavailable", i == 0
                    ? workspace.DetailText
                    : "Not captured in this Replay evidence slice");
            }
        }
    }
}
