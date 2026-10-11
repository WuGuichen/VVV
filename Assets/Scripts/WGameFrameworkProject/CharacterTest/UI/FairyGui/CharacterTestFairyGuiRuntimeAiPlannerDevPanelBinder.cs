using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder;
using static MxFramework.Game.CharacterTest.CharacterTestFairyGuiRuntimeAiLabVisualBinding;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder :
        IMxFairyGuiViewBinder<CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel>
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly IMxUiTextProvider _textProvider;
        private readonly ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface _behaviorCardPreviewSurface;
        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _behaviorCardWorkspaceCommands;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder _behaviorCardWorkspaceBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder _timelineWorkspaceBinder;
        private readonly IMxFairyGuiPopupService _popupService;
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession _rootSession;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _boundViewModel;
        private Fgui.GComponent _boundRoot;
        private readonly CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder _panelOverlayBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder _panelLayoutBinder;
        private readonly CharacterTestFairyGuiTacticalAuthoringLayoutBinder _tacticalLayoutBinder = new CharacterTestFairyGuiTacticalAuthoringLayoutBinder();
        private CharacterTestFairyGuiTacticalLibraryBrowser _tacticalLibraryBrowser;
        private readonly CharacterTestFairyGuiRuntimeAiLabLegacyTimelineInputBinder
            _legacyTimelineInputBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabPanelContentBinder _panelContentBinder;
#if UNITY_EDITOR
        private readonly CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder
            _authoringWorkspacePickerBinder;
#endif
        private readonly CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
            _publishCurrentChangesPopupBinder;
        private readonly CharacterTestFairyGuiRuntimeAiLabNavigationFeedbackBinder
            _navigationFeedbackBinder;
        private Fgui.GComponent _publishCurrentChangesPopupComponent;
        private Fgui.GComponent _publishCurrentChangesPopupOwnerRoot;

        internal IMxFairyGuiPopupService PopupService => _popupService;

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
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReloadProjectDraft,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReapplyProjectDraft
        };

        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(IMxUiCommandSink commandSink)
            : this(commandSink, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId, null)
        {
        }

        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxUiTextProvider textProvider = null)
            : this(commandSink, viewId, textProvider, null)
        {
        }

        internal CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxUiTextProvider textProvider,
            ICharacterTestRuntimeAiLabBehaviorCardPreviewSurface behaviorCardPreviewSurface,
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler behaviorCardWorkspaceCommands = null,
            IMxFairyGuiPopupService popupService = null)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _behaviorCardPreviewSurface = behaviorCardPreviewSurface;
            _behaviorCardWorkspaceCommands = behaviorCardWorkspaceCommands;
            _popupService = popupService;
            _tacticalLayoutBinder.ConfigureServices(commandSink, viewId, popupService);
            _rootSession = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession(viewId);
            _panelOverlayBinder = new CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder(
                commandSink,
                viewId,
                popupService,
                _rootSession);
            _panelLayoutBinder = new CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder(
                commandSink,
                viewId,
                _rootSession,
                _panelOverlayBinder,
                CloseRuntimeTestPopupsForWorkspaceChange,
                BindRuntimeTestAgentSelector);
            _legacyTimelineInputBinder =
                new CharacterTestFairyGuiRuntimeAiLabLegacyTimelineInputBinder(
                    commandSink,
                    viewId);
            _panelContentBinder = new CharacterTestFairyGuiRuntimeAiLabPanelContentBinder(
                commandSink,
                viewId,
                _rootSession,
                _panelOverlayBinder,
                behaviorCardPreviewSurface,
                BindBehaviorCardRuntimeControls,
                _popupService,
                responsibilityKind =>
                {
                    _behaviorCardWorkspaceCommands?.SetLibraryFilter(
                        CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All);
                    _behaviorCardWorkspaceBinder?.SetLogicLibraryResponsibilityFilter(
                        responsibilityKind);
                    _behaviorCardWorkspaceBinder?.ToggleLogicLibraryDrawer();
                });
            _behaviorCardWorkspaceBinder = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder(
                commandSink,
                viewId,
                behaviorCardWorkspaceCommands,
                () => _timelineWorkspaceBinder?.HandleGameplayPlanDetailChanged(),
                (target, text) => BindTooltip(target, text),
                OnLogicLibrarySelectionChanged);
            _timelineWorkspaceBinder = new CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder(
                this,
                commandSink,
                viewId,
                _behaviorCardWorkspaceBinder);
            _behaviorCardWorkspaceBinder.SetSharedMovementCardPresenter(_timelineWorkspaceBinder);
#if UNITY_EDITOR
            _authoringWorkspacePickerBinder =
                new CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder(
                    commandSink,
                    viewId,
                    popupService);
#endif
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink typedCommandSink =
                commandSink as CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            _publishCurrentChangesPopupBinder =
                new CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder(
                    commandSink,
                    viewId,
                    popupService,
                    typedCommandSink?.PublishCurrentChangesPopupCoordinator);
            _navigationFeedbackBinder =
                new CharacterTestFairyGuiRuntimeAiLabNavigationFeedbackBinder(typedCommandSink);
        }

        public void Bind(
            IMxFairyGuiComponentHandle component,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            var handle = component as MxFairyGuiComponentHandle;
            if (handle == null)
            {
                throw new ArgumentException(
                    "Runtime AI Planner dev binder requires a concrete FairyGUI component handle.",
                    nameof(component));
            }

            viewModel ??= new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            _boundViewModel = viewModel;
            Fgui.GComponent root = handle.Component;
            _tacticalLayoutBinder.BeginBind(root, viewModel);
            _boundRoot = root;
            ResetDynamicVisualsForNewRoot(root);
            BindCardTrialEntry(root);
            _tacticalLibraryBrowser ??= new CharacterTestFairyGuiTacticalLibraryBrowser(_commandSink, _popupService, _viewId);
            if (viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary)
            {
                _behaviorCardPreviewSurface?.SetActive(false);
                _tacticalLibraryBrowser.Bind(root, viewModel);
                return;
            }
            _rootSession.SetTooltipsSuppressed(
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            root.SetSize(root.width > 0f ? root.width : 1920f, root.height > 0f ? root.height : 1080f);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title, viewModel.Title);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Status, viewModel.StatusText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase, viewModel.PhaseText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary, viewModel.FrameSummary);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringGroupText,
                viewModel.AuthoringGroupContextText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringCardText,
                viewModel.AuthoringCardContextText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText,
                viewModel.DraftSaveStatusText);
            BindTooltip(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringDraftStateText),
                "修改后约 0.5 秒自动保存工作区草稿；保存草稿不会发布正式版本。保存失败时请检查错误并重试保存。");
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText,
                viewModel.AuthoringPublishedStateText);
            BindTooltip(root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringPublishedStateText),
                "点击“发布本次修改”才会更新正式版本；自动保存不会改变已发布版本或正在运行的冻结测试。");
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringTestStateText,
                viewModel.CurrentTestSourceStatusText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringProblemText,
                viewModel.AuthoringProblemText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.JumpFrameInput, viewModel.JumpFrameInputText);
            _legacyTimelineInputBinder.BindScrubTrack(root, viewModel);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewHeader, viewModel.PreviewHeader);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewInstructionsText,
                viewModel.PreviewInstructionsText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewStatusText, viewModel.PreviewStatusText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupHeader, viewModel.SetupHeader);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupInstructionsText,
                viewModel.SetupInstructionsText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSelectedSlotText,
                viewModel.SetupSelectedSlotText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupValidationText,
                viewModel.SetupValidationText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupCharacterText,
                viewModel.SetupCharacterText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupText,
                viewModel.SetupBehaviorGroupText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupPositionText,
                viewModel.SetupPositionText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTopValidation, viewModel.SetupTopValidationText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentBadgeText, viewModel.SetupAgentBadgeText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupDistanceText, viewModel.SetupDistanceText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyTitle, viewModel.SetupReadyTitleText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyDescription, viewModel.SetupReadyDescriptionText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneText, viewModel.SetupSceneText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupAgentCountText, viewModel.SetupAgentCountText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupBehaviorGroupCountText, viewModel.SetupBehaviorGroupCountText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupFrameLimitStatusText, viewModel.SetupFrameLimitStatusText);
            CharacterTestFairyGuiRuntimeAiLabPanelContentBinder.BindSetupTeamCard(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1SummaryCard,
                "队伍 1",
                viewModel.SetupTeam1CountText,
                viewModel.SetupTeam1ValidationText,
                false);
            CharacterTestFairyGuiRuntimeAiLabPanelContentBinder.BindSetupTeamCard(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2SummaryCard,
                "队伍 2",
                viewModel.SetupTeam2CountText,
                viewModel.SetupTeam2ValidationText,
                true);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineHeader, viewModel.TimelineHeader);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineText, viewModel.TimelineText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentHeader, viewModel.AgentHeader);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AgentListText, viewModel.AgentListText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DecisionHeader, viewModel.DecisionHeader);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SelectedAgentText, viewModel.SelectedAgentText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsText, viewModel.FactsText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PathText, viewModel.PathText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UtilityText, viewModel.UtilityText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CommandText, viewModel.CommandText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsText, viewModel.DiagnosticsText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.EditGateText, viewModel.EditGateText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.StateValidationText,
                viewModel.StateValidationText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText, viewModel.WorkbenchSummaryText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText, viewModel.WorkbenchDetailText);
            BindText(root, WorkbenchDiagnosticText, viewModel.WorkbenchDiagnosticText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText, viewModel.DraftStatusText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText, viewModel.DraftHashText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText, viewModel.ApplyDraftDisabledReasonText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText, viewModel.PublishDraftStatusText);
            BindProjectCardMetadata(root, viewModel);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineRevisionTrackText, viewModel.TimelineRevisionTrackText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineDecisionTrackText, viewModel.TimelineDecisionTrackText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineActionTrackText, viewModel.TimelineActionTrackText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineCombatTrackText, viewModel.TimelineCombatTrackText);
            BindText(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineWarningTrackText, viewModel.TimelineWarningTrackText);
            _panelContentBinder.BindSetupSlotRows(root, viewModel);
            _panelContentBinder.BindWorkbenchRows(root, viewModel);
            _panelContentBinder.BindBehaviorCardRuntimeHeader(root, viewModel);
            if (viewModel.ShowBehaviorPlan
                && viewModel.BehaviorCardWorkspace?.Timeline != null)
            {
                string characterCommand =
                    CharacterTestFairyGuiRuntimeAiLabTimelineDetailBinder
                        .RuntimeFrameFallbackSummary(viewModel, 2);
                viewModel.BehaviorCardWorkspace.Timeline.CharacterCommandText =
                    string.IsNullOrEmpty(characterCommand) || characterCommand == "—"
                        ? "无命令"
                        : characterCommand;
            }
            _behaviorCardWorkspaceBinder.Bind(
                root,
                viewModel.BehaviorCardWorkspace,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                    ? CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.GroupLibrary
                    : viewModel.ShowBehaviorPlan
                    ? CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Plan
                    : viewModel.ShowBehaviorLogic
                        || viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                            ? CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Logic
                            : CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Hidden);
            BindFactsWorkspace(root, viewModel);

            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Start,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    ? "测试设置"
                    : viewModel.StartLabel,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    || viewModel.CanStart,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume,
                viewModel.PauseResumeLabel,
                viewModel.CanPauseResume,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step,
                viewModel.StepLabel,
                viewModel.CanStep,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step);
            Fgui.GObject advanceFrame = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step);
            if (advanceFrame != null)
            {
                BindTooltip(
                    advanceFrame,
                    viewModel.CanStep
                    ? "生成一个新的运行时帧。"
                    : "请先回到最新帧，再推进模拟。");
            }
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset,
                viewModel.ResetLabel,
                viewModel.CanReset,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LatestFrame,
                viewModel.LatestFrameLabel,
                viewModel.CanSelectFrame,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LatestFrame);
            _legacyTimelineInputBinder.BindJumpFrameButton(root, viewModel);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PlaybackSpeed,
                viewModel.SpeedLabel,
                viewModel.CanChangeSpeed,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopStart,
                viewModel.LoopStartLabel,
                viewModel.CanSetLoopRange,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopStart);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.LoopEnd,
                viewModel.LoopEndLabel,
                viewModel.CanSetLoopRange,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.LoopEnd);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleLoop,
                viewModel.ToggleLoopLabel,
                viewModel.CanSetLoop,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleLoop,
                viewModel.LoopSelected,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousAgent,
                viewModel.PreviousAgentLabel,
                viewModel.CanSelectAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousAgent);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextAgent,
                viewModel.NextAgentLabel,
                viewModel.CanSelectAgent,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextAgent);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEditGate,
                viewModel.ToggleEditGateLabel,
                viewModel.CanToggleEditGate,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEditGate,
                viewModel.EditGateSelected,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TogglePreviewOverlay,
                viewModel.TogglePreviewOverlayLabel,
                viewModel.CanTogglePreviewOverlay,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TogglePreviewOverlay,
                viewModel.PreviewOverlaySelected,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleDetails,
                viewModel.ToggleDetailsLabel,
                viewModel.CanToggleDetails,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleDetails,
                viewModel.ShowWorkbench,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleStateCapture,
                viewModel.ToggleStateCaptureLabel,
                viewModel.CanToggleStateCapture,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleStateCapture,
                viewModel.StateCaptureSelected,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ValidateState,
                viewModel.ValidateStateLabel,
                viewModel.CanValidateState,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ValidateState);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SyncRecordedState,
                viewModel.SyncRecordedStateLabel,
                viewModel.CanSyncRecordedState,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SyncRecordedState);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ColdBootstrapVerify,
                viewModel.ColdBootstrapVerifyLabel,
                viewModel.CanColdBootstrapVerify,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ColdBootstrapVerify);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSetupSnap,
                viewModel.ToggleSetupSnapLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSetupSnap,
                viewModel.SetupSnapSelected,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MirrorSpawns,
                viewModel.MirrorSpawnsLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.MirrorSpawns);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResetSpawnDefaults,
                viewModel.ResetSpawnDefaultsLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ResetSpawnDefaults);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FitSetupView,
                viewModel.FitSetupViewLabel,
                viewModel.CanFitSetupView,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleSelectedSetupSlot,
                string.Empty,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleSelectedSetupSlot);
            CharacterTestFairyGuiRuntimeAiLabPanelLayoutBinder.ApplySelectedSetupSlotToggleVisual(
                root,
                viewModel);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviousSetupCharacter,
                viewModel.PreviousSetupCharacterLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousSetupCharacter);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupCharacter,
                viewModel.NextSetupCharacterLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupCharacter);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.NextSetupBehaviorGroup,
                viewModel.NextSetupBehaviorGroupLabel,
                viewModel.CanEditSetupPlacement,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextSetupBehaviorGroup);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.OpenBehaviorGroup,
                "编辑",
                viewModel.CanEditSetupPlacement && viewModel.CanOpenBehaviorGroup,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close,
                viewModel.CloseLabel,
                true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole,
                viewModel.AuthoringWorkspaceRoleText,
                false,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectAuthoringWorkspaceRole);
            BindTooltip(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.AuthoringWorkspaceRole),
                viewModel.ShowAuthoringWorkspaceRoleControl
                    ? "工作区选择将在 UX-04 接入。"
                    : string.Empty);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges,
                "测试当前修改",
                viewModel.CanTestCurrentChanges,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges);
            BindTooltip(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TestCurrentChanges),
                viewModel.CanTestCurrentChanges
                    ? "使用当前工作区草稿从第 0 帧开始测试。"
                    : viewModel.TestCurrentChangesDisabledReasonText);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges,
                viewModel.PublishCurrentChangesLabel,
                viewModel.CanPublishCurrentChanges,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishCurrentChanges);
            BindTooltip(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges),
                viewModel.CanPublishCurrentChanges
                    ? "校验并发布当前主工作区修改。"
                    : viewModel.PublishCurrentChangesDisabledReasonText);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBack,
                viewModel.IsBehaviorTestSetup ? "返回编辑" : viewModel.WorkspaceBackLabel,
                true, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTab,
                "行为卡",
                viewModel.WorkspaceNavigationEnabled,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsFactsTab,
                "Facts",
                viewModel.WorkspaceNavigationEnabled,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsRuntimeFrameTab,
                "RuntimeFrame",
                viewModel.WorkspaceNavigationEnabled,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DiagnosticsReplayTab,
                "Replay",
                viewModel.WorkspaceNavigationEnabled,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab,
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                && viewModel.DiagnosticsTab == CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                checkMode: true);
            bool behaviorTestComposition =
                viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            bool runtimeBehaviorGroupEditing = viewModel.ShowBehaviorLogic;
            bool behaviorGroupEditing = viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            bool compositionEditing = behaviorTestComposition || runtimeBehaviorGroupEditing;
            CharacterTestRuntimeAiLabWorkbenchRowViewModel selectedCompositionRow = null;
            for (int rowIndex = 0; viewModel.WorkbenchRows != null
                && rowIndex < viewModel.WorkbenchRows.Length; rowIndex++)
            {
                if (viewModel.WorkbenchRows[rowIndex]?.Selected == true)
                {
                    selectedCompositionRow = viewModel.WorkbenchRows[rowIndex];
                    break;
                }
            }
            if (runtimeBehaviorGroupEditing)
            {
                HideButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry);
                HideButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry);
                BindButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard,
                    "新建卡",
                    viewModel.CanEditWorkbench,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.AddWorkbenchCard);
                BindTooltip(
                    root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard),
                    "创建新的项目行为卡并立即保存为 r1；加入当前行为组的变更仍需保存行为组。");
                HideButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry);
                BindLogicLibraryButton(root, viewModel.CanEditWorkbench);
            }
            else
            {
                HideButton(
                    root,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard);
                HideButton(root, WorkbenchEditButton);
                BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry,
                    "添加陪测",
                    compositionEditing && viewModel.CanEditWorkbench,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DuplicateEntry);
                BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
                    viewModel.ToggleEntryLabel, (compositionEditing || behaviorGroupEditing)
                        && viewModel.CanEditWorkbench
                        && selectedCompositionRow != null
                        && (behaviorGroupEditing || !selectedCompositionRow.IsCurrentBehavior),
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry);
                BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                    "从组移除", (compositionEditing || behaviorGroupEditing)
                        && viewModel.CanEditWorkbench
                        && selectedCompositionRow != null
                        && (behaviorGroupEditing || !selectedCompositionRow.IsCurrentBehavior),
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry);
            }
            HideButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp);
            HideButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft, viewModel.UndoDraftLabel, viewModel.CanUndoDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.UndoDraft);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft, viewModel.ApplyDraftLabel, viewModel.CanApplyDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft);
            Fgui.GObject applyDraft = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft);
            if (applyDraft != null)
                BindTooltip(
                    applyDraft,
                    viewModel.CanApplyDraft ? string.Empty : viewModel.ApplyDraftDisabledReasonText);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft, viewModel.PublishDraftLabel, viewModel.CanPublishDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
                viewModel.SaveBehaviorGroupLabel,
                viewModel.CanSaveBehaviorGroup,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SaveBehaviorGroup);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReloadProjectDraft, "重新加载项目版本", viewModel.CanReloadProjectDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReloadProjectDraft);
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ReapplyProjectDraft, "基于新版重新应用", viewModel.CanReapplyProjectDraft, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReapplyProjectDraft);
            Fgui.GObject publishDraft = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft);
            if (publishDraft != null)
                BindTooltip(publishDraft, viewModel.PublishDraftDisabledReasonText);
            Fgui.GObject saveBehaviorGroup = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup);
            if (saveBehaviorGroup != null)
            {
                BindTooltip(
                    saveBehaviorGroup,
                    viewModel.CanSaveBehaviorGroup
                        ? "保存当前行为组的卡片引用、启用状态和职责配置；不会发布行为卡版本。"
                        : viewModel.SaveBehaviorGroupDisabledReasonText);
            }
            BindButton(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameLimit, viewModel.FrameLimitLabel, viewModel.ShowSetupToolbar, CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FrameLimit);
            _panelLayoutBinder.ApplyHudLayout(root, viewModel);
            _panelOverlayBinder.BindSetupBehaviorGroupPicker(root, viewModel);
            BindAuthoringActionPopups(root);
            _tacticalLayoutBinder.PrepareInspectorBounds(root, viewModel);
            _timelineWorkspaceBinder.Bind(root, viewModel);
            BindTimelineModeHost(root, viewModel);
            BindReplayWorkspace(root, viewModel);
            ConfigureFocus(root);
            _navigationFeedbackBinder?.Bind(root);
            _tacticalLayoutBinder.Bind(root, viewModel, () => _behaviorCardWorkspaceBinder.RefreshRecipeGeometry(), _behaviorCardPreviewSurface);
            _tacticalLibraryBrowser ??= new CharacterTestFairyGuiTacticalLibraryBrowser(_commandSink, _popupService, _viewId);
            _tacticalLibraryBrowser.Bind(root, viewModel);
        }

        private void BindAuthoringActionPopups(Fgui.GComponent root)
        {
#if UNITY_EDITOR
            _authoringWorkspacePickerBinder?.Bind(
                root,
                _boundViewModel?.ShowAuthoringWriteActions == true
                    && _boundViewModel?.ShowTimeline != true);
#endif
            if (_publishCurrentChangesPopupBinder == null
                || ReferenceEquals(_publishCurrentChangesPopupOwnerRoot, root))
            {
                return;
            }

            _publishCurrentChangesPopupBinder.Detach();
            _publishCurrentChangesPopupOwnerRoot = root;
            _publishCurrentChangesPopupComponent = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                    .PublishCurrentChangesPopup)?.asCom;
            Fgui.GObject anchor = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishCurrentChanges);
            if (_publishCurrentChangesPopupComponent != null && anchor != null)
            {
                _publishCurrentChangesPopupBinder.TryAttach(
                    _publishCurrentChangesPopupComponent,
                    anchor,
                    out _);
            }
        }

    }
}
