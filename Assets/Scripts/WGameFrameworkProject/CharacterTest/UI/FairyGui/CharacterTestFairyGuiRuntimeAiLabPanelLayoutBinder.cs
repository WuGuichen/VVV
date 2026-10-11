using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Owns page visibility and FairyGUI geometry for the Runtime AI Lab shell.</summary>
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession _rootSession;
        private readonly CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder _panelOverlayBinder;
        private readonly Action _closeRuntimeTestPopups;
        private readonly Action<Fgui.GComponent, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>
            _bindRuntimeTestAgentSelector;

        private const int SetupBehaviorGroupPickerRowCapacity = 7;
        private const float SetupBehaviorGroupPickerPanelWidth = 570f;
        private const float SetupBehaviorGroupPickerRowsY = 126f;
        private const float SetupBehaviorGroupPickerRowHeight = 58f;
        private const float SetupBehaviorGroupPickerRowStride = 66f;
        private const float SetupBehaviorGroupPickerPanelBottomPadding = 20f;
        private const float SetupBehaviorGroupPickerScrollTrackY = 126f;
        private const string SetupBehaviorGroupPickerBackground = "setupBehaviorGroupPickerBackground";
        private const string SetupBehaviorGroupPickerSearchIcon = "setupBehaviorGroupPickerSearchIcon";
        private const string SetupBehaviorGroupPickerCount = "setupBehaviorGroupPickerCount";
        private const string SetupBehaviorGroupPickerSearchBackground =
            "setupBehaviorGroupPickerSearchBackground";
        private const string SetupBehaviorGroupPickerSearchInput = "setupBehaviorGroupPickerSearchInput";
        private const string SetupBehaviorGroupPickerEmpty = "setupBehaviorGroupPickerEmpty";
        private const string SetupBehaviorGroupPickerScrollTrack =
            "setupBehaviorGroupPickerScrollTrack";
        private const string SetupBehaviorGroupPickerScrollThumb =
            "setupBehaviorGroupPickerScrollThumb";
        private const string WorkbenchEditButton = "btnWorkbenchEdit";
        private const string WorkbenchMoreButton = "btnWorkbenchMore";
        private const string WorkbenchListPanel = "timelinePanel";
        private const string WorkbenchCountBadge = "workbenchCountBadge";
        private const string RuntimeFrameSlotBadge = "runtimeFrameSlotBadge";
        private const string WorkbenchDetailSurface = "workbenchDetailSurface";
        private const string WorkbenchDetailIconBackground = "workbenchDetailIconBackground";
        private const string WorkbenchDetailIcon = "workbenchDetailIcon";
        private const string WorkbenchDiagnosticBackground = "workbenchDiagnosticBackground";
        private const string WorkbenchDiagnosticText = "workbenchDiagnosticText";
        internal const float TimelineModeHostX =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostX;
        internal const float TimelineModeHostPlanY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostY;
        internal const float TimelineModeHostRuntimeY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostY;
        internal const float TimelineModeHostWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostWidth;
        internal const float TimelineModeStripHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.HeaderHeight;
        internal const float BehaviorPlanModeStripHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.HeaderHeight;
        internal const float BehaviorPlanTimelineX = 0f;
        internal const float BehaviorPlanTimelineY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY;
        internal const float BehaviorPlanTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DesignWidth;
        internal const float BehaviorPlanTimelineHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchHeight;
        internal const float RuntimeTimelineX = 0f;
        internal const float RuntimeTimelineY =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeTimelineY;
        internal const float RuntimeTimelineWidth =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DesignWidth;
        internal const float RuntimeTimelineHeight =
            CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeTimelineHeight;
        private const float BehaviorGroupDrawerWidth = 650f;
        private const float BehaviorGroupDrawerHeight = 930f;
        private static readonly string[] BehaviorGroupDrawerChildNames =
        {
            WorkbenchListPanel,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
            WorkbenchCountBadge,
            WorkbenchDetailSurface,
            WorkbenchDetailIconBackground,
            WorkbenchDetailIcon,
            WorkbenchDiagnosticBackground,
            WorkbenchDiagnosticText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard,
            WorkbenchEditButton,
            WorkbenchMoreButton,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup
        };

        internal CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession rootSession,
            CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder panelOverlayBinder,
            Action closeRuntimeTestPopups,
            Action<Fgui.GComponent, CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>
                bindRuntimeTestAgentSelector)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _rootSession = rootSession;
            _panelOverlayBinder = panelOverlayBinder;
            _closeRuntimeTestPopups = closeRuntimeTestPopups;
            _bindRuntimeTestAgentSelector = bindRuntimeTestAgentSelector;
        }

        internal void ApplyHudLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool detailsExpanded = viewModel != null && viewModel.DetailsExpanded;
            bool setupPhase = viewModel != null && viewModel.ShowSetupToolbar;
            bool setup = viewModel?.Workspace != null && viewModel.Workspace.ShowSetup
                && viewModel.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                && viewModel.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool live = viewModel != null && viewModel.ShowLiveToolbar;
            bool paused = viewModel != null && viewModel.ShowPausedToolbar;
            bool diagnostics = viewModel?.Workspace != null && viewModel.Workspace.ShowDiagnostics;
            bool runtime = diagnostics && (live || paused);
            bool runtimeFrame = diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
            bool expanded = paused || detailsExpanded;
            bool workbench = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool behaviorGroup = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            bool cardEditor = viewModel != null
                && viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            float workspaceWidth = ResolveWorkspaceWidth(viewModel);
            float workspaceHeight = workbench
                ? 1020f
                : setup ? 814f : expanded ? 700f : 230f;
            float workspaceX = workbench ? 960f : 1920f - 24f - workspaceWidth;
            SetObjectSize(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel,
                workspaceWidth,
                workspaceHeight);
            SetObjectPosition(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel,
                workspaceX,
                workbench ? 60f : 76f);
            if (runtimeFrame)
            {
                SetObjectGeometry(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace),
                    RuntimeTimelineX,
                    RuntimeTimelineY,
                    RuntimeTimelineWidth,
                    RuntimeTimelineHeight);
            }
            ApplyCompactObservationLayout(root, live && !expanded, workspaceX, workspaceWidth);
            ApplyWorkbenchLayout(root, viewModel);
            ApplyRuntimeBehaviorGroupLogicLayout(root, viewModel);
            ApplySetupLayout(root, viewModel, show: setup && !workbench);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel,
                !workbench && !cardEditor && !diagnostics && !setup);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                cardEditor || (!diagnostics && !setup));
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb,
                !workbench && !cardEditor && !diagnostics && !setup);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                workbench
                    ? cardEditor
                    ? "行为卡"
                    : "步骤详情 · " + viewModel.WorkbenchTitleText
                : setup
                    ? "实验准备"
                    : viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame
                        ? "运行调试 · RuntimeFrame"
                        : viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay
                            ? "运行调试 · Replay"
                            : "运行调试 · 实时监视");
            if (cardEditor)
            {
                string cardEditorTitle = "当前行为";
                for (int rowIndex = 0; viewModel?.WorkbenchRows != null
                    && rowIndex < viewModel.WorkbenchRows.Length; rowIndex++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel row =
                        viewModel.WorkbenchRows[rowIndex];
                    if (row?.IsCurrentBehavior != true)
                        continue;
                    cardEditorTitle = !string.IsNullOrWhiteSpace(row.TitleText)
                        ? row.TitleText
                        : !string.IsNullOrWhiteSpace(row.Label)
                            ? row.Label
                            : cardEditorTitle;
                    break;
                }
                BindText(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                    cardEditorTitle);
            }
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb,
                workbench
                    ? cardEditor
                    ? "实验 / 行为组 / 行为卡"
                    : "实验 / 行为组"
                : setup
                    ? "配置双方智能体并确认测试条件"
                    : "实验 / 运行调试 / " + viewModel.DiagnosticsTab);
            bool showSetup = setup && !workbench;
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupHeader, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupInstructionsText, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedSlotText, false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText,
                showSetup && !string.IsNullOrEmpty(viewModel.SetupValidationText));
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1Header, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2Header, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupPositionText, showSetup);
            SetSetupSlotRowsVisible(root, showSetup);
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    SetObjectVisible(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LegacySetupSlot(teamId, slotIndex),
                        false);
                }
            }
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupBehaviorGroup, false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup,
                showSetup && viewModel?.CanOpenBehaviorGroup == true);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit, showSetup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewHeader, runtime && !workbench && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewInstructionsText, runtime && !workbench && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewStatusText, runtime && !workbench && !runtimeFrame);
            bool runtimeDetails = false;
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DecisionHeader, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SelectedAgentText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentHeader, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentListText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PathText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UtilityText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CommandText, runtimeDetails);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsText, runtimeDetails);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.EditGateText,
                viewModel != null && viewModel.ShowAdvancedDiagnostics);

            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                setupPhase && !workbench && !behaviorGroup);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume, runtime);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step, paused);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset, !workbench && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay, runtime && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails, runtime && !runtimeFrame);
            bool showWorkspaceTabs = !setup && !workbench && !runtimeFrame && !behaviorGroup;
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTab, showWorkspaceTabs);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsFactsTab, showWorkspaceTabs);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsRuntimeFrameTab, showWorkspaceTabs);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsReplayTab, showWorkspaceTabs);
            float tabY = behaviorGroup ? 108f : 76f;
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTab, 16f, tabY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsFactsTab, 144f, tabY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsRuntimeFrameTab, 232f, tabY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsReplayTab, 372f, tabY);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEditGate,
                viewModel != null && viewModel.ShowAdvancedDiagnostics);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent, runtime && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent, runtime && !runtimeFrame);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed, live && !runtimeFrame);

            // RuntimeFrame owns history navigation inside its workspace. These root-level
            // migration controls must never bleed into Setup, Behavior Card, Facts or Replay.
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LatestFrame, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrameInputBackground, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrameInput, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrame, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopStart, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopEnd, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleLoop, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleStateCapture, paused);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ValidateState, paused);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SyncRecordedState, paused);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ColdBootstrapVerify, paused);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.StateValidationText, paused);

            bool showTimeline = viewModel != null
                && viewModel.ShowTimeline
                && !cardEditor;
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BottomPanel, showTimeline);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineHeader, showTimeline);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineText, showTimeline);
            bool timelineExpanded = viewModel != null && viewModel.TimelineExpanded;
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineScrubTrack,
                showTimeline && timelineExpanded);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressTrack, showTimeline);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressFill, showTimeline);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressThumb, showTimeline);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineRevisionTrackText, showTimeline && timelineExpanded);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineDecisionTrackText, showTimeline && timelineExpanded);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineActionTrackText, showTimeline && timelineExpanded);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineCombatTrackText, showTimeline && timelineExpanded);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineWarningTrackText, showTimeline && timelineExpanded);
            SetObjectSize(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BottomPanel,
                1872f,
                timelineExpanded ? 220f : 40f);
            ApplyTimelineLayout(root, timelineExpanded);
            // Run the shell layout last because the generic visibility pass above
            // intentionally hides commands for the other diagnostics tabs.
            ApplyTopToolbarLayout(root, viewModel);
        }

        private void ApplySetupLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel,
            bool show)
        {
            string[] setupObjects =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupWorkspaceBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopBreadcrumb,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1SummaryCard,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2SummaryCard,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedCardBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadge,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadgeText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesHeader,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupDistanceText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyBackground,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyTitle,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyDescription,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneStatusBar,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentCountText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupCountText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupFrameLimitStatusText,
                "setupCharacterIcon",
                "setupBehaviorIcon"
            };
            for (int i = 0; i < setupObjects.Length; i++)
            {
                Fgui.GObject child = root.GetChild(setupObjects[i]);
                if (child == null)
                    continue;
                child.visible = show;
                child.sortingOrder = show ? 10 : 0;
            }

            // The dedicated Runtime AI Lab Composer package uses WorkspacePanel as the
            // setup workspace background. The richer legacy setup background is optional
            // during migration and must not make the whole setup surface transparent when
            // it is absent from the published package.
            bool hasDedicatedSetupBackground = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupWorkspaceBackground) != null;
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel,
                !show || !hasDedicatedSetupBackground);
            if (!show)
                return;

            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopBreadcrumb, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1SummaryCard, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2SummaryCard, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedSlotText, false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText,
                viewModel != null && !string.IsNullOrEmpty(viewModel.SetupValidationText));
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyDescription, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentCountText, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupCountText, false);
            SetObjectVisible(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupFrameLimitStatusText, false);

            string[] setupInteractiveObjects =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1Header,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2Header,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedSlotText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupPositionText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close
            };
            for (int i = 0; i < setupInteractiveObjects.Length; i++)
            {
                Fgui.GObject child = root.GetChild(setupInteractiveObjects[i]);
                if (child != null)
                    child.sortingOrder = 11;
            }
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    Fgui.GObject slot = root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex));
                    if (slot != null)
                        slot.sortingOrder = 12;
                    Fgui.GObject slotCard = root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlotCard(teamId, slotIndex));
                    if (slotCard != null)
                        slotCard.sortingOrder = 10;
                    Fgui.GObject slotLabel = root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlotLabel(teamId, slotIndex));
                    if (slotLabel != null)
                        slotLabel.sortingOrder = 11;
                }
            }

            Fgui.GGraph workspaceBg = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupWorkspaceBackground)?.asGraph;
            if (workspaceBg != null)
            {
                SetObjectGeometry(workspaceBg, 1370f, 64f, 550f, 1016f);
                workspaceBg.DrawRect(550f, 1016f, 1, new Color32(24, 60, 72, 255), new Color32(6, 17, 24, 252));
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation), 850f, 18f, 220f, 28f);

            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1Header), 1392f, 88f, 506f, 24f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2Header), 1392f, 214f, 506f, 24f);
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                float slotY = teamId == CharacterTestArenaMatchConfig.Team2Id ? 244f : 118f;
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    float slotX = 1392f + slotIndex * 173f;
                    SetObjectGeometry(
                        root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex)),
                        slotX,
                        slotY,
                        160f,
                        76f);
                }
            }

            Fgui.GGraph selectedCardBg = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedCardBackground)?.asGraph;
            if (selectedCardBg != null)
            {
                SetObjectGeometry(selectedCardBg, 1392f, 338f, 506f, 228f);
                selectedCardBg.DrawRect(506f, 228f, 1, new Color32(28, 72, 84, 255), new Color32(8, 24, 32, 255));
            }
            Fgui.GGraph badgeBg = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadge)?.asGraph;
            if (badgeBg != null)
            {
                SetObjectGeometry(badgeBg, 1408f, 354f, 84f, 132f);
                bool isTeam2 = viewModel != null && viewModel.SetupAgentBadgeText != null && viewModel.SetupAgentBadgeText.StartsWith("T2");
                Color32 badgeBorder = isTeam2 ? new Color32(140, 52, 60, 255) : new Color32(36, 100, 132, 255);
                Color32 badgeFill = isTeam2 ? new Color32(32, 14, 18, 255) : new Color32(10, 28, 38, 255);
                badgeBg.DrawRect(84f, 132f, 1, badgeBorder, badgeFill);
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadgeText), 1408f, 362f, 84f, 44f);
            SetObjectGeometry(root.GetChild("setupCharacterIcon"), 1512f, 378f, 28f, 28f);
            SetObjectGeometry(root.GetChild("setupBehaviorIcon"), 1512f, 432f, 28f, 28f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot), 1426f, 422f, 48f, 48f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText), 1512f, 376f, 368f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter), 1512f, 376f, 0f, 0f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter), 1504f, 354f, 380f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText), 1512f, 430f, 204f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup), 1504f, 404f, 284f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupBehaviorGroup), 1726f, 424f, 58f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup), 1796f, 404f, 88f, 42f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupPositionText), 1506f, 458f, 376f, 24f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText), 1506f, 488f, 376f, 24f);

            Fgui.GGraph rulesBg = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesBackground)?.asGraph;
            if (rulesBg != null)
            {
                SetObjectGeometry(rulesBg, 1392f, 582f, 506f, 180f);
                rulesBg.DrawRect(506f, 180f, 1, new Color32(28, 72, 84, 255), new Color32(8, 24, 32, 255));
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupRulesHeader), 1412f, 598f, 200f, 24f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupDistanceText), 1412f, 638f, 466f, 32f);
            Fgui.GTextField distText = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupDistanceText)?.asTextField;
            if (distText != null)
            {
                distText.align = Fgui.AlignType.Center;
                distText.color = new Color32(142, 219, 208, 255);
                distText.textFormat.size = 14;
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit), 1412f, 686f, 466f, 44f);

            Fgui.GGraph sceneBar = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneStatusBar)?.asGraph;
            if (sceneBar != null)
            {
                SetObjectGeometry(sceneBar, 18f, 82f, 250f, 68f);
                sceneBar.DrawRect(250f, 68f, 1, new Color32(28, 72, 84, 255), new Color32(8, 24, 32, 245));
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView), 28f, 90f, 52f, 52f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap), 88f, 90f, 52f, 52f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns), 148f, 90f, 52f, 52f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults), 208f, 90f, 52f, 52f);

            Fgui.GGraph readyBg = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyBackground)?.asGraph;
            if (readyBg != null)
            {
                SetObjectGeometry(readyBg, 18f, 1018f, 270f, 42f);
                readyBg.DrawRect(270f, 42f, 1, new Color32(28, 72, 84, 255), new Color32(8, 24, 32, 245));
            }
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneText), 32f, 1028f, 240f, 22f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyTitle), 1404f, 954f, 480f, 26f);
            SetObjectGeometry(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start), 1392f, 990f, 506f, 58f);

            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView, "适配视图");
            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap, "吸附站位");
            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns, "镜像双方站位");
            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults, "恢复默认站位");
            ConfigureSetupIconButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot,
                viewModel != null && string.Equals(viewModel.ToggleSetupSlotLabel, "禁用", StringComparison.Ordinal)
                    ? "禁用此槽位"
                    : "启用此槽位");
            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset, "重置实验");
            ConfigureSetupIconButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close, "关闭");
            ConfigurePrimaryActionButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                viewModel != null && viewModel.CanStart);

            Fgui.GTextField readyTitle = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyTitle)?.asTextField;
            if (readyTitle != null)
                readyTitle.color = viewModel != null && viewModel.CanStart
                    ? new Color32(105, 215, 140, 255)
                    : new Color32(236, 124, 100, 255);
            Fgui.GTextField topValidation = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation)?.asTextField;
            if (topValidation != null)
                topValidation.color = viewModel != null && viewModel.CanStart
                    ? new Color32(105, 215, 140, 255)
                    : new Color32(236, 124, 100, 255);
        }

        private static void ConfigurePrimaryActionButton(
            Fgui.GComponent root,
            string childName,
            bool enabled)
        {
            Fgui.GButton button = root?.GetChild(childName)?.asButton;
            if (button == null)
                return;

            Fgui.GImage baseImg = button.GetChild("base")?.asImage;
            if (baseImg != null)
                baseImg.visible = false;

            Fgui.GGraph overlay = button.GetChild("overOverlay")?.asGraph;
            if (overlay != null)
                overlay.visible = false;
            Fgui.GGraph downOverlay = button.GetChild("downOverlay")?.asGraph;
            if (downOverlay != null)
                downOverlay.visible = false;

            Fgui.GGraph bg = button.GetChild("customPrimaryBg")?.asGraph;
            if (bg == null)
            {
                bg = new Fgui.GGraph();
                bg.name = "customPrimaryBg";
                bg.touchable = false;
                button.AddChildAt(bg, 0);
            }

            Fgui.GGraph topAccent = button.GetChild("customTopAccent")?.asGraph;
            if (topAccent == null)
            {
                topAccent = new Fgui.GGraph();
                topAccent.name = "customTopAccent";
                topAccent.touchable = false;
                button.AddChildAt(topAccent, 1);
            }

            float width = button.width;
            float height = button.height;
            bg.SetSize(width, height);
            topAccent.SetSize(width - 4f, 2f);
            topAccent.x = 2f;
            topAccent.y = 2f;

            Color32 fill = enabled ? new Color32(14, 56, 48, 255) : new Color32(14, 22, 28, 255);
            Color32 border = enabled ? new Color32(52, 211, 153, 255) : new Color32(32, 48, 56, 255);
            Color32 accent = enabled ? new Color32(110, 245, 195, 160) : new Color32(48, 64, 72, 80);

            bg.DrawRect(width, height, 1, border, fill);
            topAccent.DrawRect(width - 4f, 2f, 0, Color.clear, accent);

            Fgui.GLoader icon = button.GetChild("icon")?.asLoader;
            if (icon != null)
            {
                icon.SetSize(22f, 22f);
                icon.color = enabled ? new Color32(94, 234, 212, 255) : new Color32(75, 95, 105, 255);
            }

            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = enabled ? new Color32(236, 253, 245, 255) : new Color32(75, 95, 105, 255);
                title.textFormat.size = 18;
                title.textFormat.bold = true;
                title.verticalAlign = Fgui.VertAlignType.Middle;
            }

            CenterButtonContent(button, 10f);

            button.onRollOver.Set(() =>
            {
                if (button.enabled)
                {
                    bg.DrawRect(width, height, 1, new Color32(110, 245, 195, 255), new Color32(20, 78, 66, 255));
                    topAccent.DrawRect(width - 4f, 2f, 0, Color.clear, new Color32(140, 255, 215, 220));
                }
            });
            button.onRollOut.Set(() =>
            {
                if (button.enabled)
                {
                    bg.DrawRect(width, height, 1, new Color32(52, 211, 153, 255), new Color32(14, 56, 48, 255));
                    topAccent.DrawRect(width - 4f, 2f, 0, Color.clear, new Color32(110, 245, 195, 160));
                }
            });
        }

        internal static void ApplySelectedSetupSlotToggleVisual(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GButton button =
                root?.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot)?.asButton;
            if (button == null)
                return;

            bool slotEnabled = viewModel != null
                && string.Equals(viewModel.ToggleSetupSlotLabel, "禁用", StringComparison.Ordinal);
            button.text = string.Empty;
            button.icon = Fgui.UIPackage.GetItemURL(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                slotEnabled
                    ? "action_slot_disable_32x32"
                    : "action_slot_enable_32x32");
            Fgui.GLoader icon = button.GetChild("icon")?.asLoader;
            if (icon != null)
                icon.color = Color.white;
            CenterButtonContent(button, 0f);
        }

        private void ConfigureSetupIconButton(
            Fgui.GComponent root,
            string childName,
            string tooltip)
        {
            Fgui.GButton button = root?.GetChild(childName)?.asButton;
            if (button == null)
                return;

            button.text = string.Empty;
            CenterButtonContent(button, 10f);
            BindTooltip(button, tooltip);
        }

        private static float ResolveWorkspaceWidth(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel == null)
                return 360f;
            if (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard)
                return 960f;
            return viewModel.ShowSetupToolbar
                ? 720f
                : 360f;
        }

    }
}
