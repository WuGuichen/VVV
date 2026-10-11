using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder
    {
        private static readonly string[] AuthoringContextObjects =
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringContextBar,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringGroupText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringCardText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringProblemText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges
        };

        private static readonly string[] AuthoringHiddenLegacyCommands =
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails
        };

        private static readonly string[] AuthoringHiddenLegacyDraftControls =
        {
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText
        };

        private void ApplyTopToolbarLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool setup = viewModel?.Workspace != null && viewModel.Workspace.ShowSetup
                && viewModel.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                && viewModel.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool runtimeFrame = viewModel?.Workspace != null
                && viewModel.Workspace.ShowDiagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
            if (!runtimeFrame)
                _closeRuntimeTestPopups?.Invoke();
            bool workbench = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool behaviorGroup = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            bool authoringContext = viewModel?.ShowAuthoringContextBar == true;
            bool runtimeTraceAuthoringContext = authoringContext
                && runtimeFrame
                && viewModel?.ShowTimeline == true;
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar), 0f, 0f, 1920f, setup ? 64f : 60f);
            SetObjectVisible(root, RuntimeFrameSlotBadge, runtimeFrame);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, 24f, setup ? 16f : 14f);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, !workbench);
            ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset);
            ClearObjectRelations(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close);
            ApplyAuthoringContextBarLayout(
                root,
                viewModel,
                authoringContext,
                runtimeTraceAuthoringContext);
            if (authoringContext && !runtimeTraceAuthoringContext)
            {
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, false);
                SetObjectVisible(root, RuntimeFrameSlotBadge, false);
                SetObjectVisible(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation,
                    false);
                for (int i = 0; i < AuthoringHiddenLegacyCommands.Length; i++)
                    SetObjectVisible(root, AuthoringHiddenLegacyCommands[i], false);
                for (int i = 0; i < AuthoringHiddenLegacyDraftControls.Length; i++)
                    SetObjectVisible(root, AuthoringHiddenLegacyDraftControls[i], false);
                SetObjectVisible(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                    setup);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, true);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close),
                    1856f,
                    10f,
                    44f,
                    40f);
                Fgui.GObject close = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close);
                if (close != null)
                    close.sortingOrder = 82;
                return;
            }
            if (setup)
            {
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, true);
                BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, "实验设置");
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, false);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset),
                    1800f,
                    10f,
                    44f,
                    44f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close),
                    1856f,
                    10f,
                    44f,
                    44f);
                return;
            }
            if (behaviorGroup)
            {
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, false);
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase,
                    viewModel.WorkbenchEntryCountText);
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary,
                    viewModel.WorkbenchSummaryText);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase),
                    824f,
                    40f,
                    56f,
                    24f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary),
                    916f,
                    40f,
                    520f,
                    24f);
                Fgui.GTextField count = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase)?.asTextField;
                if (count != null)
                {
                    count.align = Fgui.AlignType.Center;
                    count.verticalAlign = Fgui.VertAlignType.Middle;
                    count.sortingOrder = 2;
                }
                Fgui.GObject countBadge = root.GetChild(WorkbenchCountBadge);
                if (countBadge != null)
                    countBadge.sortingOrder = 1;
                Fgui.GTextField summary = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary)?.asTextField;
                if (summary != null)
                {
                    summary.color = new Color32(255, 176, 74, 255);
                    summary.verticalAlign = Fgui.VertAlignType.Middle;
                }

                string[] hiddenWorkbenchCommands =
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails
                };
                for (int i = 0; i < hiddenWorkbenchCommands.Length; i++)
                    SetObjectVisible(root, hiddenWorkbenchCommands[i], false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, false);
                return;
            }
            if (workbench)
            {
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, false);
                string[] hiddenWorkbenchCommands =
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails
                };
                for (int i = 0; i < hiddenWorkbenchCommands.Length; i++)
                    SetObjectVisible(root, hiddenWorkbenchCommands[i], false);
                SetObjectVisible(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                    true);
                return;
            }
            if (runtimeFrame)
            {
                const float sessionBarX =
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelX;
                const float sessionBarWidth =
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelWidth;
                const float sessionBarHeight =
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.SessionHeaderHeight;
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, false);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset, true);
                SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, true);
                SetRuntimeFrameGraphColor(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar),
                    7,
                    14,
                    21,
                    248);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar),
                    sessionBarX,
                    0f,
                    sessionBarWidth,
                    sessionBarHeight);
                SetObjectGeometry(
                    root.GetChild(RuntimeFrameSlotBadge),
                    1416f,
                    10f,
                    72f,
                    40f);
                SetRuntimeFrameGraphColor(root.GetChild(RuntimeFrameSlotBadge), 13, 65, 119);

                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase,
                    ResolveRuntimeFrameSlotLabel(viewModel));
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title,
                    ResolveRuntimeFrameDisplayName(viewModel));
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary,
                    ResolveRuntimeFrameSessionStatus(viewModel));

                string[] detachedRuntimeFrameControls =
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close,
                    RuntimeFrameSlotBadge
                };
                for (int i = 0; i < detachedRuntimeFrameControls.Length; i++)
                    ClearObjectRelations(root, detachedRuntimeFrameControls[i]);

                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase),
                    1416f,
                    10f,
                    72f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title),
                    1500f,
                    6f,
                    180f,
                    26f);
                Fgui.GTextField slot = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase)?.asTextField;
                if (slot != null)
                {
                    slot.align = Fgui.AlignType.Center;
                    slot.verticalAlign = Fgui.VertAlignType.Middle;
                    slot.color = RuntimeFrameSrgb(177, 218, 255);
                    slot.sortingOrder = 61;
                    slot.touchable = false;
                    SetTimelineFontSize(slot, 20);
                }
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary),
                    1500f,
                    32f,
                    180f,
                    20f);
                Fgui.GTextField title = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title)?.asTextField;
                if (title != null)
                {
                    title.verticalAlign = Fgui.VertAlignType.Middle;
                    title.color = RuntimeFrameSrgb(235, 242, 246);
                    title.singleLine = true;
                    title.autoSize = Fgui.AutoSizeType.None;
                    title.sortingOrder = 61;
                    SetTimelineFontSize(title, 22);
                    BindTooltip(title, ResolveRuntimeFrameDisplayName(viewModel));
                }
                Fgui.GTextField frame = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary)?.asTextField;
                if (frame != null)
                {
                    frame.align = Fgui.AlignType.Left;
                    frame.verticalAlign = Fgui.VertAlignType.Middle;
                    frame.color = RuntimeFrameSrgb(93, 218, 193);
                    frame.singleLine = true;
                    frame.autoSize = Fgui.AutoSizeType.None;
                    frame.sortingOrder = 61;
                    SetTimelineFontSize(frame, 16);
                }

                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume),
                    1688f,
                    10f,
                    48f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step),
                    1744f,
                    10f,
                    48f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset),
                    1800f,
                    10f,
                    48f,
                    40f);
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close),
                    1856f,
                    10f,
                    48f,
                    40f);
                Fgui.GObject runtimeTopBar = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar);
                if (runtimeTopBar != null)
                    runtimeTopBar.sortingOrder = 60;
                Fgui.GObject runtimeFrameSlotBadge = root.GetChild(RuntimeFrameSlotBadge);
                if (runtimeFrameSlotBadge != null)
                    runtimeFrameSlotBadge.sortingOrder = 61;
                string[] raisedRuntimeFrameControls =
                {
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close
                };
                for (int i = 0; i < raisedRuntimeFrameControls.Length; i++)
                {
                    Fgui.GObject control = root.GetChild(raisedRuntimeFrameControls[i]);
                    if (control != null)
                        control.sortingOrder = 62;
                }
                Fgui.GButton pause = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume)?.asButton;
                if (pause != null)
                {
                    bool paused = viewModel.ShowPausedToolbar;
                    pause.mode = Fgui.ButtonMode.Common;
                    pause.selected = false;
                    pause.enabled = true;
                    pause.grayed = false;
                    pause.touchable = viewModel.CanPauseResume;
                    pause.alpha = viewModel.CanPauseResume ? 1f : 0.45f;
                    pause.onClick.Set(() =>
                    {
                        _commandSink?.Enqueue(new MxUiCommand(
                            _viewId,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                            null));
                    });
                    ApplyGeneratedRuntimeAiIcon(
                        pause,
                        paused
                            ? "runtime_play_64x64"
                            : "runtime_pause_64x64");
                    BindTooltip(pause, paused ? "继续测试" : "暂停测试");
                }
                Fgui.GButton step = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step)?.asButton;
                if (step != null)
                {
                    step.mode = Fgui.ButtonMode.Common;
                    step.selected = false;
                    step.enabled = true;
                    step.grayed = false;
                    step.touchable = viewModel.CanStep;
                    step.alpha = viewModel.CanStep ? 1f : 0.45f;
                    ApplyGeneratedRuntimeAiIcon(step, "runtime_step_frame_64x64");
                    BindTooltip(step, "推进一帧");
                }
                Fgui.GButton reset = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset)?.asButton;
                if (reset != null)
                {
                    reset.mode = Fgui.ButtonMode.Common;
                    reset.selected = false;
                    reset.enabled = true;
                    reset.grayed = false;
                    reset.touchable = viewModel.CanReset;
                    reset.alpha = viewModel.CanReset ? 1f : 0.45f;
                    reset.onClick.Set(() =>
                    {
                        _commandSink?.Enqueue(new MxUiCommand(
                            _viewId,
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                            null));
                    });
                    ApplyGeneratedRuntimeAiIcon(reset, "runtime_reset_64x64");
                    BindTooltip(reset, "重新开始测试");
                }
                Fgui.GButton back = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close)?.asButton;
                if (back != null)
                {
                    ApplyGeneratedRuntimeAiIcon(back, "runtime_back_64x64");
                    BindTooltip(back, "返回实验设置");
                }
                _bindRuntimeTestAgentSelector?.Invoke(root, viewModel);
                return;
            }
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar, true);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, true);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, true);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, true);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, true);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, 312f, 19f);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, 112f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, 438f, 19f);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, 260f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, 720f, 19f);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, 260f);
            string[] buttons =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close
            };
            float[] xs = { 992f, 1216f, 1112f, 1296f, 1376f, 1456f, 1536f, 1632f, 1716f };
            float[] widths = { 112f, 72f, 96f, 72f, 72f, 72f, 88f, 76f, 72f };
            for (int i = 0; i < buttons.Length; i++)
                SetObjectGeometry(root.GetChild(buttons[i]), xs[i], 14f, widths[i], 32f);
        }

        private void ApplyAuthoringContextBarLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool visible,
            bool compactForRuntimeTrace)
        {
            for (int i = 0; i < AuthoringContextObjects.Length; i++)
                SetObjectVisible(root, AuthoringContextObjects[i], visible);
            if (!visible)
                return;

            SetObjectGeometry(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringContextBar),
                0f,
                0f,
                compactForRuntimeTrace ? 1320f : 1920f,
                60f);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole,
                viewModel?.ShowAuthoringWorkspaceRoleControl == true);
            bool showWriteActions = viewModel?.ShowAuthoringWriteActions == true
                && !compactForRuntimeTrace;
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges,
                showWriteActions);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges,
                showWriteActions);
            bool showReplay = showWriteActions;
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                showReplay);
            if (showReplay)
            {
                Fgui.GButton replay = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset)?.asButton;
                SetObjectGeometry(replay, 1800f, 8f, 44f, 44f);
                if (replay != null)
                {
                    replay.sortingOrder = 81;
                    replay.touchable = viewModel.CanReset;
                    replay.alpha = viewModel.CanReset ? 1f : 0.45f;
                    ApplyGeneratedRuntimeAiIcon(replay, "runtime_reset_64x64");
                    BindTooltip(replay, "重新播放：从第 0 帧重启当前测试");
                }
            }

            for (int i = 0; i < AuthoringContextObjects.Length; i++)
            {
                Fgui.GObject child = root.GetChild(AuthoringContextObjects[i]);
                if (child != null)
                    child.sortingOrder = i == 0 ? 80 : 81;
            }

            Fgui.GTextField group = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringGroupText)?.asTextField;
            Fgui.GTextField card = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringCardText)?.asTextField;
            Fgui.GTextField draft = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText)?.asTextField;
            Fgui.GTextField published = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText)?.asTextField;
            Fgui.GTextField test = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText)?.asTextField;
            Fgui.GTextField problem = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringProblemText)?.asTextField;
            ConfigureAuthoringContextText(group, new Color32(232, 241, 244, 255));
            ConfigureAuthoringContextText(card, new Color32(204, 219, 225, 255));
            if (group != null)
                SetObjectGeometry(group, group.x, 4f, group.width, 22f);
            if (card != null)
                SetObjectGeometry(card, card.x, 30f, card.width, 22f);
            ConfigureAuthoringContextText(draft, new Color32(105, 215, 140, 255));
            ConfigureAuthoringContextText(
                published,
                viewModel?.AuthoringStatus?.BehaviorGroupPublishState ==
                    CharacterTestRuntimeAiLabPublishUxState.GameAvailable
                    && viewModel.AuthoringStatus.BehaviorCardPublishState ==
                    CharacterTestRuntimeAiLabPublishUxState.GameAvailable
                    ? new Color32(105, 215, 140, 255)
                    : new Color32(255, 176, 74, 255));
            ConfigureAuthoringContextText(test, new Color32(120, 184, 255, 255));
            ConfigureAuthoringContextText(
                problem,
                viewModel?.BehaviorGroupHasValidationProblems == true
                    || viewModel?.BehaviorCardHasValidationProblems == true
                    ? new Color32(236, 124, 100, 255)
                    : new Color32(157, 178, 187, 255));
            BindTooltip(group, viewModel?.AuthoringGroupContextText ?? string.Empty);
            BindTooltip(card, viewModel?.AuthoringCardContextText ?? string.Empty);
        }

        private static void ConfigureAuthoringContextText(
            Fgui.GTextField text,
            Color32 color)
        {
            if (text == null)
                return;
            text.singleLine = true;
            text.autoSize = Fgui.AutoSizeType.None;
            text.verticalAlign = Fgui.VertAlignType.Middle;
            text.color = color;
            SetTimelineFontSize(text, 15);
        }

        internal static string ResolveRuntimeFrameSlotLabel(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            string slotLabel = viewModel?.RuntimeFrame?.AgentSlotLabel;
            if (string.IsNullOrWhiteSpace(slotLabel))
                slotLabel = "T1-1";
            CharacterTestRuntimeAiLabSetupSlotRowViewModel[] rows = viewModel?.SetupSlotRows;
            if (string.IsNullOrWhiteSpace(viewModel?.RuntimeFrame?.AgentSlotLabel) && rows != null)
            {
                for (int i = 0; i < rows.Length; i++)
                {
                    CharacterTestRuntimeAiLabSetupSlotRowViewModel row = rows[i];
                    if (row == null || !row.Selected)
                        continue;
                    slotLabel = "T"
                        + row.TeamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "-"
                        + (row.SlotIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                }
            }
            return slotLabel;
        }

        private static string ResolveRuntimeFrameSessionStatus(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            int frame = viewModel?.RuntimeFrame?.SelectedFrame ?? 0;
            string state;
            switch (viewModel?.PresentationContext
                ?? CharacterTestRuntimeAiPlannerDevPresentationContext.Unavailable)
            {
                case CharacterTestRuntimeAiPlannerDevPresentationContext.Live:
                    state = "运行中";
                    break;
                case CharacterTestRuntimeAiPlannerDevPresentationContext.Review:
                    state = "历史帧";
                    break;
                case CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview:
                    state = "循环回看";
                    break;
                case CharacterTestRuntimeAiPlannerDevPresentationContext.Completed:
                    state = "已完成";
                    frame = viewModel?.RuntimeFrame?.LatestFrame ?? frame;
                    break;
                default:
                    state = "已暂停";
                    break;
            }
            return state
                + " · 第 "
                + frame.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " 帧";
        }

        private static string ResolveRuntimeFrameDisplayName(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            string displayName = viewModel?.RuntimeFrame?.AgentDisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = viewModel?.SetupBehaviorGroupText;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                int agentId = viewModel?.RuntimeFrame?.AgentEntityId ?? 0;
                displayName = agentId > 0
                    ? "Agent " + agentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "Runtime AI Planner";
            }
            return displayName;
        }

    }
}
