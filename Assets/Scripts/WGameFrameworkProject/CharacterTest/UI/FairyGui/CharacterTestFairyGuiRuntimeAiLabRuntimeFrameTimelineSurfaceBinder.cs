using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineSurfaceBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder _owner;
        private readonly ICharacterTestRuntimeAiLabTimelineCommandSink _commands;
        private readonly CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder _runtimeFrameTimeline;
        private const float FormalTimelineRulerX = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LabelWidth;
        private const float FormalTimelineWidth = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasWidth;
        private const float TimelineTrackHeight = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeTrackHeight;
        private static readonly string[] TimelineTrackLabels = { "感知", "决策", "动作", "结果", "警告" };
        private static readonly string[] TimelineTrackIconResourceNames = { "timeline_track_perception_64x64", "timeline_track_decision_64x64", "timeline_track_action_64x64", "timeline_track_result_64x64", "timeline_track_warning_64x64" };
        private static readonly Vector2[] TimelineSelectedBadgePolygon = { new Vector2(0f, 0f), new Vector2(34f, 0f), new Vector2(34f, 18f), new Vector2(21f, 18f), new Vector2(17f, 24f), new Vector2(13f, 18f), new Vector2(0f, 18f) };
        private static readonly string[] TimelinePassiveChildren = { "timelineLoopRange", "timelineRetainedMask", "timelineFutureMask", "timelineFutureAdvanceFill", "timelineHistoryProgress", "timelinePlayhead", "timelinePreviewPlayhead", "timelinePlayheadHitArea", "timelineSelectedFrameBadge", "timelineSelectedFrameLabel", "timelinePreviewFrameBadge", "timelinePreviewFrameLabel", "timelineViewport" };
        private static readonly string[] RuntimeFrameHiddenControls = { "timelineText", "timelineStatus", "btnTimelinePanLeft", "btnTimelinePanRight", "btnLatestFrame", "btnFirstFrame", "btnStepBack10", "btnStepBack", "btnTimelinePlayPause", "btnTimelinePlaybackSpeed", "btnNextFrame", "btnStepForward10", "jumpFrameInput", "btnJumpFrame", "btnLoopStart", "btnLoopEnd", "btnToggleLoop", "branchRecomputeRiskText" };
        private static string[] TimelineTrackNames =>
            CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder.TimelineTrackNames;
        private const int RuntimeFrameEventDetailVisibleMemberCount =
            CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder.VisibleMemberCount;
        internal CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineSurfaceBinder(CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder owner, ICharacterTestRuntimeAiLabTimelineCommandSink commands, CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder runtimeFrameTimeline)
        {
            _owner = owner; _commands = commands; _runtimeFrameTimeline = runtimeFrameTimeline;
        }
        internal void Bind(Fgui.GComponent timeline, Fgui.GComponent viewport, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel, CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace)
        {
            BindFormalTimelineButtons(timeline, viewModel, workspace);
            BindFormalTimelineLayout(timeline, viewport, workspace, viewModel.BranchRecomputeSelected, viewModel.TimelineExpanded);
            BindTrackLabels(viewport);
            RaiseTimelineCursorOverlays(viewport);
        }
        private static void ClearTimelinePreview(Fgui.GComponent timeline)
        {
            SetTimelineChildVisible(timeline, "timelinePreviewPlayhead", false);
            SetTimelineChildVisible(timeline, "timelinePreviewFrameBadge", false);
            SetTimelineChildVisible(timeline, "timelinePreviewFrameLabel", false);
        }
        private void BindButton(Fgui.GComponent root, string childName, string label, bool enabled, string commandId, bool selected = false, bool checkMode = false)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null) return;
            if (checkMode) button.mode = Fgui.ButtonMode.Check;
            button.text = label ?? string.Empty; button.enabled = enabled; button.grayed = !enabled; button.selected = selected; button.visible = true;
            CenterButtonContent(button, 10f);
            button.onClick.Set(() => { if (enabled) _commands.Enqueue(commandId); });
        }
        private void BindTooltip(Fgui.GObject target, string value) { _owner.BindTooltip(target, value); }
        private void BindFormalTimelineButtons(
            Fgui.GComponent timeline,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace)
        {
            CharacterTestRuntimeAiLabTimelineProjection projection = workspace?.Projection;
            bool hasTimeline = workspace != null
                && workspace.CanZoom
                && projection != null
                && projection.Domain.HasFrames;
            int visibleSpan = hasTimeline ? projection.Viewport.FrameSpan : 0;
            bool canZoomOut = hasTimeline
                && CharacterTestRuntimeAiPlannerDevSceneController.CanZoomTimelineSpan(
                    visibleSpan,
                    -1);
            bool canZoomIn = hasTimeline
                && CharacterTestRuntimeAiPlannerDevSceneController.CanZoomTimelineSpan(
                    visibleSpan,
                    1);
            bool canFit = hasTimeline
                && projection.Viewport.Mode
                    != CharacterTestRuntimeAiLabTimelineViewportMode.FitRetained;

            BindButton(timeline, "btnTimelineZoomOut", string.Empty, canZoomOut, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomOut);
            BindButton(timeline, "btnTimelineZoomIn", string.Empty, canZoomIn, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineZoomIn);
            BindButton(timeline, "btnTimelineFit", string.Empty, canFit, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineFit);
            BindButton(timeline, "btnTimelinePanLeft", "◀", workspace.CanPan, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanLeft);
            BindButton(timeline, "btnTimelinePanRight", "▶", workspace.CanPan, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelinePanRight);
            BindButton(timeline, "btnLatestFrame", viewModel.LatestFrameLabel, workspace.CanSelectFrame, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame);
            BindButton(timeline, "btnFirstFrame", "首帧", workspace.CanSelectFrame, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FirstFrame);
            BindButton(timeline, "btnStepBack10", viewModel.StepBack10Label, workspace.CanStepBackward, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack10);
            BindButton(timeline, "btnStepBack", viewModel.StepBackLabel, workspace.CanStepBackward, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepBack);
            BindButton(
                timeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed,
                viewModel.SpeedLabel,
                viewModel.CanChangeSpeed,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed);
            Fgui.GButton duplicatePlayPause = timeline.GetChild(
                "btnTimelinePlayPause")?.asButton;
            if (duplicatePlayPause != null)
            {
                duplicatePlayPause.onClick.Clear();
                duplicatePlayPause.visible = false;
                duplicatePlayPause.touchable = false;
                duplicatePlayPause.enabled = false;
            }
            BindButton(timeline, "btnNextFrame", string.Empty, workspace.CanStepForward, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step);
            BindButton(timeline, "btnStepForward10", viewModel.StepForward10Label, workspace.CanStepForward, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.StepForward10);
            BindButton(timeline, "btnTimelineCollapse", string.Empty, viewModel.CanToggleTimelineExpanded, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineToggleExpanded);
            BindButton(timeline, "btnBranchRecompute", string.Empty, workspace.CanToggleBranchRecompute, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BranchRecompute, viewModel.BranchRecomputeSelected, checkMode: true);
            BindButton(timeline, "btnLoopStart", viewModel.LoopStartLabel, workspace.CanSetLoopRange, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart);
            BindButton(timeline, "btnLoopEnd", viewModel.LoopEndLabel, workspace.CanSetLoopRange, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd);
            BindButton(timeline, "btnToggleLoop", viewModel.ToggleLoopLabel, workspace.CanSetLoop, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop, viewModel.LoopSelected, checkMode: true);

            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnTimelineZoomOut")?.asButton,
                "timeline_zoom_out_64x64");
            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnTimelineZoomIn")?.asButton,
                "timeline_zoom_in_64x64");
            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnTimelineFit")?.asButton,
                "timeline_fit_64x64");
            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnBranchRecompute")?.asButton,
                "timeline_branch_recompute_64x64");
            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnNextFrame")?.asButton,
                "runtime_step_frame_64x64");
            ApplyGeneratedRuntimeAiIcon(
                timeline.GetChild("btnTimelineCollapse")?.asButton,
                viewModel.TimelineExpanded
                    ? "timeline_collapse_64x64"
                    : "timeline_fullscreen_64x64");

            BindTooltip(timeline.GetChild("btnTimelineZoomOut"), "缩小时间轴");
            BindTooltip(timeline.GetChild("btnTimelineZoomIn"), "放大时间轴");
            BindTooltip(timeline.GetChild("btnTimelineFit"), "适配完整历史范围");

            BindTooltip(
                timeline.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed),
                "切换播放倍速；实时测试和历史回看共用当前档位，不改变 60 Hz RuntimeFrame 步长。");

            BindTooltip(
                timeline.GetChild("btnTimelineCollapse"),
                viewModel.TimelineExpanded ? "收起时间轴" : "展开时间轴");

            Fgui.GButton jump = timeline.GetChild("btnJumpFrame")?.asButton;
            if (jump != null)
            {
                jump.text = viewModel.JumpFrameLabel;
                jump.enabled = workspace.CanJumpFrame;
                jump.grayed = !workspace.CanJumpFrame;
                jump.onClick.Set(() =>
                {
                    Fgui.GTextInput input = timeline.GetChild("jumpFrameInput") as Fgui.GTextInput;
                    string text = input?.text ?? string.Empty;
                    object payload = int.TryParse(text, out int frame) ? (object)frame : text;
                    _commands.JumpToRuntimeFrame(payload);
                });
            }

            Fgui.GObject advance = timeline.GetChild("btnStep");
            if (advance != null)
            {
                BindTooltip(
                    advance,
                    viewModel.CanStep
                    ? "基于当前选中帧生成一个新的 RuntimeFrame，完成后保持暂停。"
                    : "历史帧需先选择“从此重算”，Latest 可直接推进。");
            }

            Fgui.GObject branch = timeline.GetChild("btnBranchRecompute");
            if (branch != null)
                BindTooltip(
                    branch,
                    viewModel.BranchRecomputeSelected
                    ? workspace.BranchRecomputeRiskText
                    : "显式允许从当前历史帧重算；首帧成功后旧未来不可恢复。");
            BindText(timeline, "branchRecomputeRiskText", workspace.BranchRecomputeRiskText);
            _owner.BindRuntimeTestTimelineMore(timeline, viewModel, workspace);
        }

        private void BindFormalTimelineLayout(
            Fgui.GComponent timeline,
            Fgui.GComponent viewport,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace,
            bool branchRecomputeSelected,
            bool timelineExpanded)
        {
            const float headerY =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeHeaderY;
            ApplyRuntimeFrameTimelineGeometry(timeline, viewport, timelineExpanded);
            for (int i = 0; i < TimelineTrackNames.Length; i++)
            {
                Fgui.GObject track = viewport.GetChild(TimelineTrackNames[i]);
                if (track != null)
                    track.visible = timelineExpanded;
            }

            bool showBranchRisk = workspace != null
                && workspace.Available
                && workspace.SelectedFrame < workspace.LatestFrame
                && timelineExpanded;
            SetTimelineChildVisible(timeline, "btnBranchRecompute", showBranchRisk);
            Fgui.GButton branchButton = timeline.GetChild("btnBranchRecompute")?.asButton;
            if (branchButton != null)
            {
                branchButton.SetSize(
                    52f,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.TimelineControlHeight);
                branchButton.SetXY(
                    1340f,
                    headerY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .TimelineControlYInset);
                ApplyGeneratedRuntimeAiIcon(
                    branchButton,
                    "timeline_branch_recompute_64x64");
            }
            for (int i = 0; i < RuntimeFrameHiddenControls.Length; i++)
                SetTimelineChildVisible(timeline, RuntimeFrameHiddenControls[i], false);
            SetTimelineChildVisible(timeline, "timelineHeader", false);
            RaiseTimelineHeaderControl(timeline, "btnFirstFrame");
            RaiseTimelineHeaderControl(timeline, "btnStepBack10");
            RaiseTimelineHeaderControl(timeline, "btnStepBack");
            RaiseTimelineHeaderControl(timeline, "btnNextFrame");
            RaiseTimelineHeaderControl(timeline, "btnStepForward10");
            RaiseTimelineHeaderControl(timeline, "btnLatestFrame");
            RaiseTimelineHeaderControl(timeline, "btnBranchRecompute");
            RaiseTimelineHeaderControl(timeline, "btnTimelineZoomOut");
            RaiseTimelineHeaderControl(timeline, "btnTimelineZoomIn");
            RaiseTimelineHeaderControl(timeline, "btnTimelineFit");
            RaiseTimelineHeaderControl(timeline, "btnTimelineCollapse");
            RaiseTimelineHeaderControl(timeline, "btnTimelineMore");
            SetTimelineChildVisible(timeline, "branchRecomputeRiskText", false);
            BindText(timeline, "branchRecomputeRiskText", "重算起点");
            SetObjectGeometry(
                timeline.GetChild("branchRecomputeRiskText"),
                1324f,
                headerY
                    + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .TimelineControlYInset,
                96f,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.TimelineControlHeight);
            Fgui.GTextField branchLabel = timeline.GetChild("branchRecomputeRiskText")?.asTextField;
            if (branchLabel != null)
            {
                branchLabel.align = Fgui.AlignType.Right;
                branchLabel.verticalAlign = Fgui.VertAlignType.Middle;
                branchLabel.color = RuntimeFrameSrgb(166, 181, 190);
                branchLabel.sortingOrder = 100;
            }
            PositionRuntimeFrameHeaderControls(timeline, workspace, headerY, timelineExpanded);
            for (int i = 0; i < TimelinePassiveChildren.Length; i++)
            {
                Fgui.GObject child = viewport.GetChild(TimelinePassiveChildren[i]);
                if (child != null)
                    child.touchable = false;
            }
            if (!CharacterTestFairyGuiRuntimeAiLabRuntimeFrameTimelineBinder.ShouldKeepPreview(
                    _runtimeFrameTimeline.ScrubActive,
                    _runtimeFrameTimeline.Input.Active))
            {
                ClearTimelinePreview(viewport);
            }
        }

        private static void ApplyRuntimeFrameTimelineGeometry(
            Fgui.GComponent timeline,
            Fgui.GComponent viewport,
            bool timelineExpanded)
        {
            Fgui.GObject background = timeline.GetChild("timelineOpaqueBackground");
            if (background != null)
            {
                background.visible = true;
                background.touchable = false;
                background.asGraph.color = RuntimeFrameSrgb(7, 20, 30);
            }

            Fgui.GObject details = timeline.GetChild("timelineEventDetails");
            SetObjectGeometry(
                details,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX,
                0f,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight);
            if (details != null)
                details.sortingOrder = 20;
            const float viewportY =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeViewportY;
            float viewportHeight = timelineExpanded
                ? CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeViewportHeight
                : CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RulerHeight;
            if (background != null)
            {
                background.SetSize(1920f, viewportHeight);
                background.SetXY(0f, viewportY);
            }
            viewport.visible = true;
            viewport.SetSize(1920f, viewportHeight);
            viewport.SetXY(0f, viewportY);
            ApplyRuntimeFrameTimelinePalette(timeline, viewport);

            float rulerWidth = Mathf.Max(
                1f,
                viewport.width
                    - FormalTimelineRulerX
                    - CharacterTestRuntimeAiLabTimelineWorkbenchLayout.CanvasRightPadding);
            SetObjectGeometry(
                viewport.GetChild("timelineOpaqueBackground"),
                0f,
                0f,
                viewport.width,
                viewportHeight);
            SetObjectGeometry(
                viewport.GetChild("timelineRuler"),
                FormalTimelineRulerX,
                0f,
                rulerWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RulerHeight);
            SetObjectGeometry(
                viewport.GetChild("timelineHistoryProgress"),
                FormalTimelineRulerX,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RulerHeight - 3f,
                1f,
                3f);
            const float trackAreaHeight = TimelineTrackHeight * 5f;
            SetObjectGeometry(viewport.GetChild("timelineScrubHitArea"), FormalTimelineRulerX, 40f, rulerWidth, trackAreaHeight);
            SetObjectGeometry(viewport.GetChild("timelineViewport"), FormalTimelineRulerX, 40f, rulerWidth, trackAreaHeight);
            SetObjectGeometry(viewport.GetChild("timelinePlayhead"), FormalTimelineRulerX, 0f, 2f, 40f + trackAreaHeight);
            SetObjectGeometry(viewport.GetChild("timelinePreviewPlayhead"), FormalTimelineRulerX, 0f, 2f, 40f + trackAreaHeight);
            SetObjectGeometry(
                viewport.GetChild("timelinePlayheadHitArea"),
                FormalTimelineRulerX - 12f,
                0f,
                24f,
                40f + trackAreaHeight);
            SetObjectGeometry(viewport.GetChild("timelineSelectedFrameBadge"), FormalTimelineRulerX, 0f, 34f, 24f);
            SetObjectGeometry(viewport.GetChild("timelineSelectedFrameLabel"), FormalTimelineRulerX, 0f, 34f, 18f);
            SetObjectGeometry(viewport.GetChild("timelinePreviewFrameBadge"), FormalTimelineRulerX, 0f, 54f, 24f);
            SetObjectGeometry(viewport.GetChild("timelinePreviewFrameLabel"), FormalTimelineRulerX, 0f, 54f, 24f);

            for (int i = 0; i < TimelineTrackNames.Length; i++)
            {
                Fgui.GComponent track = viewport.GetChild(TimelineTrackNames[i])?.asCom;
                if (track == null)
                    continue;
                SetObjectGeometry(
                    track,
                    0f,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RulerHeight
                        + i * TimelineTrackHeight,
                    viewport.width,
                    TimelineTrackHeight);
                SetObjectGeometry(
                    track.GetChild("trackIcon"),
                    20f,
                    (TimelineTrackHeight - 40f) * 0.5f,
                    40f,
                    40f);
                SetObjectGeometry(track.GetChild("trackLabel"), 72f, 0f, 136f, TimelineTrackHeight);
                SetObjectGeometry(
                    track.GetChild("trackContent"),
                    FormalTimelineRulerX,
                    0f,
                    rulerWidth,
                    TimelineTrackHeight);
                Fgui.GComponent content = track.GetChild("trackContent")?.asCom;
                SetObjectGeometry(
                    content?.GetChild("trackBackground"),
                    0f,
                    0f,
                    rulerWidth,
                    TimelineTrackHeight);
                Fgui.GGraph trackBackground =
                    content?.GetChild("trackBackground")?.asGraph;
                if (trackBackground != null)
                {
                    Color fill = (i & 1) == 0
                        ? new Color32(6, 19, 28, 246)
                        : new Color32(7, 22, 31, 246);
                    trackBackground.DrawRect(
                        rulerWidth,
                        TimelineTrackHeight,
                        1,
                        new Color32(27, 49, 61, 180),
                        fill);
                }
                Fgui.GTextField label = track.GetChild("trackLabel")?.asTextField;
                SetTimelineFontSize(label, 24);
            }

            string[] overlays =
            {
                "timelineLoopRange", "timelineRetainedMask", "timelineFutureMask", "timelineFutureAdvanceFill"
            };
            for (int i = 0; i < overlays.Length; i++)
            {
                Fgui.GObject overlay = viewport.GetChild(overlays[i]);
                if (overlay != null)
                    overlay.SetSize(overlay.width, trackAreaHeight);
            }
            SetTimelineOverlayOrder(viewport, "timelineHistoryProgress", 80);
            SetTimelineOverlayOrder(viewport, "timelinePlayhead", 90);
            SetTimelineOverlayOrder(viewport, "timelinePreviewPlayhead", 91);
            SetTimelineOverlayOrder(viewport, "timelineSelectedFrameBadge", 92);
            SetTimelineOverlayOrder(viewport, "timelineSelectedFrameLabel", 93);
            SetTimelineOverlayOrder(viewport, "timelinePreviewFrameBadge", 94);
            SetTimelineOverlayOrder(viewport, "timelinePreviewFrameLabel", 95);
            SetTimelineOverlayOrder(viewport, "timelinePlayheadHitArea", 96);
        }

        private static void ApplyRuntimeFrameTimelinePalette(
            Fgui.GComponent timeline,
            Fgui.GComponent viewport)
        {
            SetRuntimeFrameGraphColor(timeline?.GetChild("timelineOpaqueBackground"), 7, 20, 30);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineOpaqueBackground"), 7, 20, 30);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineRuler"), 11, 27, 38);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineHistoryProgress"), 54, 200, 195);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineRetainedMask"), 7, 24, 38, 68);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineFutureMask"), 12, 37, 42, 66);
            SetRuntimeFrameGraphColor(viewport?.GetChild("timelineFutureAdvanceFill"), 27, 166, 170, 24);

            for (int i = 0; i < TimelineTrackNames.Length; i++)
            {
                Fgui.GComponent track = viewport?.GetChild(TimelineTrackNames[i])?.asCom;
                Fgui.GComponent content = track?.GetChild("trackContent")?.asCom;
                SetRuntimeFrameGraphColor(content?.GetChild("trackBackground"), 16, 29, 41);
                Fgui.GTextField label = track?.GetChild("trackLabel")?.asTextField;
                if (label != null)
                    label.color = RuntimeFrameSrgb(217, 229, 235);
            }

            Fgui.GComponent details = timeline?.GetChild("timelineEventDetails")?.asCom;
            SetRuntimeFrameGraphColor(details?.GetChild("detailBackground"), 7, 20, 30);
            for (int i = 1; i <= RuntimeFrameEventDetailVisibleMemberCount; i++)
                SetRuntimeFrameGraphColor(details?.GetChild("causalCard" + i), 13, 26, 36, 232);
            for (int i = 1; i <= 3; i++)
                SetRuntimeFrameGraphColor(details?.GetChild("causalConnector" + i), 147, 166, 177, 128);

            Fgui.GTextField selectedLabel =
                viewport?.GetChild("timelineSelectedFrameLabel")?.asTextField;
            if (selectedLabel != null)
                selectedLabel.color = RuntimeFrameSrgb(7, 20, 30);
            Fgui.GTextField previewLabel =
                viewport?.GetChild("timelinePreviewFrameLabel")?.asTextField;
            if (previewLabel != null)
                previewLabel.color = RuntimeFrameSrgb(7, 20, 30);
        }

        private static void SetRuntimeFrameGraphColor(
            Fgui.GObject target,
            byte red,
            byte green,
            byte blue,
            byte alpha = 255)
        {
            Fgui.GGraph graph = target?.asGraph;
            if (graph != null)
                graph.color = RuntimeFrameSrgb(red, green, blue, alpha);
        }

        private static void SetTimelineOverlayOrder(
            Fgui.GComponent viewport,
            string childName,
            int sortingOrder)
        {
            Fgui.GObject child = viewport?.GetChild(childName);
            if (child != null)
                child.sortingOrder = sortingOrder;
        }

        private static void RaiseTimelineCursorOverlays(Fgui.GComponent viewport)
        {
            string[] overlayNames =
            {
                "timelineHistoryProgress",
                "timelinePlayhead",
                "timelinePreviewPlayhead",
                "timelineSelectedFrameBadge",
                "timelineSelectedFrameLabel",
                "timelinePreviewFrameBadge",
                "timelinePreviewFrameLabel",
                "timelinePlayheadHitArea"
            };
            for (int i = 0; i < overlayNames.Length; i++)
            {
                Fgui.GObject overlay = viewport?.GetChild(overlayNames[i]);
                if (overlay != null)
                    viewport.SetChildIndex(overlay, viewport.numChildren - 1);
            }
            Fgui.GGraph playhead = viewport?.GetChild("timelinePlayhead")?.asGraph;
            if (playhead != null)
            {
                Color selectedColor = RuntimeFrameSrgb(78, 219, 213);
                playhead.DrawRect(2f, playhead.height, 0, Color.clear, selectedColor);
                Fgui.GGraph selectedBadge = viewport.GetChild(
                    "timelineSelectedFrameBadge")?.asGraph;
                selectedBadge?.DrawPolygon(
                    34f,
                    24f,
                    TimelineSelectedBadgePolygon,
                    selectedColor);
            }
            Fgui.GGraph previewPlayhead = viewport?.GetChild("timelinePreviewPlayhead")?.asGraph;
            if (previewPlayhead != null)
            {
                Color previewColor = RuntimeFrameSrgb(240, 180, 95);
                previewPlayhead.DrawRect(2f, previewPlayhead.height, 0, Color.clear, previewColor);
            }
        }

        private static void PositionRuntimeFrameHeaderControls(
            Fgui.GComponent timeline,
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel workspace,
            float headerY,
            bool timelineExpanded)
        {
            float controlY = headerY
                + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.TimelineControlYInset;
            float controlHeight =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.TimelineControlHeight;
            bool canSelectFrame = workspace?.CanSelectFrame == true;
            SetTimelineChildVisible(timeline, "btnFirstFrame", canSelectFrame);
            SetTimelineChildVisible(timeline, "btnStepBack10", workspace?.CanStepBackward == true);
            SetTimelineChildVisible(timeline, "btnStepBack", workspace?.CanStepBackward == true);
            SetTimelineChildVisible(timeline, "btnTimelinePlayPause", false);
            Fgui.GButton duplicatePlayPause = timeline?.GetChild(
                "btnTimelinePlayPause")?.asButton;
            if (duplicatePlayPause != null)
            {
                duplicatePlayPause.onClick.Clear();
                duplicatePlayPause.touchable = false;
                duplicatePlayPause.enabled = false;
            }
            SetTimelineChildVisible(timeline, "btnNextFrame", workspace?.CanStepForward == true);
            SetTimelineChildVisible(timeline, "btnStepForward10", workspace?.CanStepForward == true);
            SetTimelineChildVisible(timeline, "btnLatestFrame", canSelectFrame);
            bool canZoom = timelineExpanded && workspace != null && workspace.CanZoom;
            SetTimelineChildVisible(timeline, "btnTimelineZoomOut", canZoom);
            SetTimelineChildVisible(timeline, "btnTimelineZoomIn", canZoom);
            SetTimelineChildVisible(timeline, "btnTimelineFit", canZoom);
            SetTimelineChildVisible(
                timeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed,
                true);
            SetTimelineChildVisible(timeline, "btnTimelineCollapse", true);
            SetTimelineChildVisible(timeline, "btnTimelineMore", true);

            SetRuntimeFrameHeaderControlGeometry(timeline, "btnFirstFrame", 650f, controlY, 82f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnStepBack10", 742f, controlY, 78f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnStepBack", 830f, controlY, 64f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnNextFrame", 904f, controlY, 64f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnStepForward10", 978f, controlY, 78f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(
                timeline,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed,
                1076f,
                controlY,
                80f,
                controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnTimelineZoomOut", 1166f, controlY, 52f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnTimelineZoomIn", 1228f, controlY, 52f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnTimelineFit", 1290f, controlY, 56f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnTimelineCollapse", 1430f, controlY, 52f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnTimelineMore", 1492f, controlY, 52f, controlHeight);
            SetRuntimeFrameHeaderControlGeometry(timeline, "btnLatestFrame", 1554f, controlY, 342f, controlHeight);

            string[] textButtons =
            {
                "btnFirstFrame", "btnStepBack10", "btnStepBack", "btnStepForward10",
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed,
                "btnLatestFrame"
            };
            for (int i = 0; i < textButtons.Length; i++)
            {
                Fgui.GButton button = timeline.GetChild(textButtons[i])?.asButton;
                if (button != null)
                    button.titleFontSize = 18;
            }
        }

        private static void SetRuntimeFrameHeaderControlGeometry(
            Fgui.GComponent timeline,
            string childName,
            float x,
            float y,
            float width,
            float height)
        {
            Fgui.GObject control = timeline?.GetChild(childName);
            if (control == null)
                return;
            control.SetSize(width, height);
            control.SetXY(x, y);
        }

        private static void RaiseTimelineHeaderControl(Fgui.GComponent timeline, string name)
        {
            Fgui.GObject control = timeline?.GetChild(name);
            if (control != null)
                control.sortingOrder = 100;
        }

        internal static void BindTrackLabels(Fgui.GComponent viewport)
        {
            for (int i = 0; i < TimelineTrackNames.Length; i++)
            {
                Fgui.GComponent track = viewport.GetChild(TimelineTrackNames[i])?.asCom;
                if (track == null)
                    continue;
                Fgui.GTextField label = track.GetChild("trackLabel")?.asTextField;
                if (label != null)
                    label.text = TimelineTrackLabels[i];
                Fgui.GImage icon = track.GetChild("trackIcon")?.asImage;
                if (icon != null)
                {
                    string iconUrl = Fgui.UIPackage.GetItemURL(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        TimelineTrackIconResourceNames[i]);
                    Fgui.PackageItem iconItem = Fgui.UIPackage.GetItemByURL(iconUrl);
                    icon.texture = iconItem?.owner.GetItemAsset(iconItem) as Fgui.NTexture;
                    icon.color =
                        CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder.RuntimeFrameTrackAccentColor(
                        (CharacterTestRuntimeAiLabTimelineTrackKind)i);
                }
            }
        }

    }
}
