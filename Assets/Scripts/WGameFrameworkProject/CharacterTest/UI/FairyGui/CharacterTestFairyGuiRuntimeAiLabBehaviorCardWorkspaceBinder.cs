using System;
using MxFramework.UI;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode
    {
        Hidden,
        GroupLibrary,
        Plan,
        Logic
    }

    internal sealed class CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder
    {
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder _library;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder _timeline;
        private readonly CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder _planTimeline;
        private readonly IMxUiCommandSink _commandSink;
        private readonly MxUiViewId _viewId;
        private readonly Action<Fgui.GObject, string> _bindTooltip;
        private readonly Action<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>
            _logicLibrarySelectionChanged;
        private Fgui.GComponent _logicLibraryRoot;
        private CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel _logicLibraryViewModel;
        private CharacterTestRuntimeAiLabBehaviorAssetCardViewModel _logicLibrarySelectedCard;
        private CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder _sharedMovementCardPresenter;
        private bool _logicLibraryVisible;

        internal CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler commands,
            Action gameplayPlanDetailChanged = null,
            Action<Fgui.GObject, string> bindTooltip = null,
            Action<CharacterTestRuntimeAiLabBehaviorAssetCardViewModel>
                logicLibrarySelectionChanged = null)
        {
            _commandSink = commandSink;
            _viewId = viewId;
            _bindTooltip = bindTooltip;
            _logicLibrarySelectionChanged = logicLibrarySelectionChanged;
            _library = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardLibraryBinder(
                commands,
                commandSink,
                viewId,
                CloseLogicLibraryDrawer,
                OnLogicLibrarySelectionChanged,
                bindTooltip);
            _timeline = new CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder(
                commands,
                commandSink,
                viewId,
                ToggleLogicLibraryDrawer,
                bindTooltip);
            _planTimeline = new CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder(
                commands,
                commandSink,
                viewId,
                gameplayPlanDetailChanged,
                bindTooltip);
        }

        internal void RefreshRecipeGeometry() => _timeline.RefreshRecipeGeometry();

        internal bool GameplayPlanDetailVisible => _planTimeline.DetailVisible;
        internal bool LogicLibraryVisible => _logicLibraryVisible;
        internal CharacterTestRuntimeAiLabBehaviorAssetCardViewModel LogicLibrarySelectedCard =>
            _logicLibrarySelectedCard;
        internal GameplayReadablePlanDetailKind GameplayPlanDetailKind =>
            _planTimeline.DetailKind;
        internal string GameplayPlanDetailStableId => _planTimeline.DetailStableId;
        internal GameplayReadablePlanProjection GameplayPlanProjection =>
            _planTimeline.Projection;

        internal void RefreshBehaviorLogicFlowAnimation() =>
            _timeline.RefreshBehaviorLogicFlowAnimation();

        internal void RefreshBehaviorLogicRuntimeOverlay(
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel viewModel) =>
            _timeline.RefreshRecipeRuntimeOverlay(viewModel);

        internal void SetSharedMovementCardPresenter(
            CharacterTestFairyGuiRuntimeAiLabTimelineWorkspaceBinder presenter)
        {
            _sharedMovementCardPresenter = presenter;
        }

        internal void SetCharacterActionPicker(
            Action<Fgui.GObject, Action<string>> showCharacterActionPicker) =>
            _timeline.SetCharacterActionPicker(showCharacterActionPicker);

        internal bool TryRepairSelectedLogicElement() =>
            _timeline.TryRepairSelectedLogicElement();

        internal bool TryCommitPendingInspectorEdit() =>
            _timeline.TryCommitPendingInspectorEdit();

        internal void ShowCommandFeedback(string message) =>
            _timeline.ShowCommandFeedback(message);

        internal bool TryAddDetectorToSelectedStage() =>
            _timeline.TryAddDetectorToSelectedStage();

        internal bool TryRemoveSelectedLogicElement() =>
            _timeline.TryRemoveSelectedLogicElement();

        internal bool TryLocateRecipeTimeline(string timelineId) =>
            _timeline.TryLocateRecipeTimeline(timelineId);

        internal bool TryClearRecipeEventSelection() =>
            _timeline.TryClearRecipeEventSelection();

        internal bool TrySetSelectedStageKind(CharacterTestRuntimeAiLabCardStepKind kind) =>
            _timeline.TrySetSelectedStageKind(kind);

        internal bool TrySetStageKind(string stageId, CharacterTestRuntimeAiLabCardStepKind kind) =>
            _timeline.TrySetStageKind(stageId, kind);

        internal bool TrySetSelectedStageDurationMode(CharacterTestRuntimeAiLabLogicDurationMode mode) =>
            _timeline.TrySetSelectedStageDurationMode(mode);

        internal bool TrySetStageDurationMode(string stageId, CharacterTestRuntimeAiLabLogicDurationMode mode) =>
            _timeline.TrySetStageDurationMode(stageId, mode);

        internal bool TrySetSelectedMovementStrategy(CharacterTestRuntimeAiLabMovementStrategy strategy) =>
            _timeline.TrySetSelectedMovementStrategy(strategy);

        internal bool TrySetMovementStrategy(string stageId, CharacterTestRuntimeAiLabMovementStrategy strategy) =>
            _timeline.TrySetMovementStrategy(stageId, strategy);

        internal bool TrySetSelectedMovementTargetKind(CharacterTestRuntimeAiLabMovementTargetKind targetKind) =>
            _timeline.TrySetSelectedMovementTargetKind(targetKind);

        internal bool TrySetMovementTargetKind(string stageId, CharacterTestRuntimeAiLabMovementTargetKind targetKind) =>
            _timeline.TrySetMovementTargetKind(stageId, targetKind);

        internal bool TrySetMoveDistanceBand(string stageId, float minimum, float maximum) =>
            _timeline.TrySetMoveDistanceBand(stageId, minimum, maximum);

        internal bool TrySetMoveTargetPosition(string stageId, float x, float z) =>
            _timeline.TrySetMoveTargetPosition(stageId, x, z);

        internal bool TrySetMoveTargetEntityId(string stageId, int entityId) =>
            _timeline.TrySetMoveTargetEntityId(stageId, entityId);

        internal bool TrySetSelectedStageDurationFrames(int frames) =>
            _timeline.TrySetSelectedStageDurationFrames(frames);

        internal bool TrySetStageDurationFrames(string stageId, int frames) =>
            _timeline.TrySetStageDurationFrames(stageId, frames);

        internal bool TrySetDetectorTrigger(string detectorId, CharacterTestRuntimeAiLabLogicDetectorTrigger trigger) =>
            _timeline.TrySetDetectorTrigger(detectorId, trigger);

        internal bool TrySetDetectorScope(string detectorId, CharacterTestRuntimeAiLabLogicDetectorScope scope) =>
            _timeline.TrySetDetectorScope(detectorId, scope);

        internal bool TrySetDetectorWindowMode(string detectorId, CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode) =>
            _timeline.TrySetDetectorWindowMode(detectorId, mode);

        internal bool TrySetTransitionPolicy(string transitionId, CharacterTestRuntimeAiLabLogicTransitionPolicy policy) =>
            _timeline.TrySetTransitionPolicy(transitionId, policy);

        internal bool TrySetTransitionTarget(string transitionId, string targetStageId) =>
            _timeline.TrySetTransitionTarget(transitionId, targetStageId);

        internal bool TrySetSelectedDetectorTrigger(CharacterTestRuntimeAiLabLogicDetectorTrigger trigger) =>
            _timeline.TrySetSelectedDetectorTrigger(trigger);

        internal bool TrySetSelectedDetectorScope(CharacterTestRuntimeAiLabLogicDetectorScope scope) =>
            _timeline.TrySetSelectedDetectorScope(scope);

        internal bool TrySetSelectedDetectorWindowMode(CharacterTestRuntimeAiLabLogicDetectorWindowEndMode mode) =>
            _timeline.TrySetSelectedDetectorWindowMode(mode);

        internal bool TrySetSelectedTransitionPolicy(CharacterTestRuntimeAiLabLogicTransitionPolicy policy) =>
            _timeline.TrySetSelectedTransitionPolicy(policy);

        internal bool TrySetSelectedTransitionTarget(string targetStageId) =>
            _timeline.TrySetSelectedTransitionTarget(targetStageId);

        internal bool TryCycleSelectedDetectorTrigger() =>
            _timeline.TryCycleSelectedDetectorTrigger();

        internal bool TryToggleSelectedDetectorScope() =>
            _timeline.TryToggleSelectedDetectorScope();

        internal bool TryCycleSelectedDetectorWindowMode() =>
            _timeline.TryCycleSelectedDetectorWindowMode();

        internal bool TryCycleSelectedTransitionPolicy() =>
            _timeline.TryCycleSelectedTransitionPolicy();

        internal bool TryCycleSelectedTransitionTarget() =>
            _timeline.TryCycleSelectedTransitionTarget();

        internal void Bind(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel viewModel,
            CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode mode)
        {
            bool visible = mode != CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Hidden;
            bool groupLibrary = mode == CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.GroupLibrary;
            bool gameplayPlan = mode == CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Plan;
            bool behaviorLogic = mode == CharacterTestRuntimeAiLabBehaviorCardWorkspacePresentationMode.Logic;
            Fgui.GComponent library = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary)?.asCom;
            Fgui.GComponent timeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)?.asCom;
            _logicLibraryRoot = library;
            _logicLibraryViewModel = viewModel?.Library;
            if (behaviorLogic && timeline != null && viewModel?.Timeline?.CompositionEditor == null)
            {
                // Size the formal root before the recipe binder lays out its
                // dynamically-created children. The root includes the 120 px
                // command rail; the recipe host then receives the full 1300 px
                // time-content width. Resizing only after Bind leaves the inner
                // viewport at the former 1180 px width and exposes a blank strip.
                timeline.SetSize(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicTimelineRootWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchHeight);
            }
            if (groupLibrary)
            {
                _logicLibraryVisible = false;
                _library.Bind(library, viewModel?.Library, visible: true, addToBehaviorGroup: true);
                _timeline.HideForPlan(timeline);
                _planTimeline.Hide(timeline);
                return;
            }
            if (!behaviorLogic)
                _logicLibraryVisible = false;
            BindLogicLibraryDrawer(behaviorLogic && _logicLibraryVisible);
            if (gameplayPlan)
            {
                _timeline.HideForPlan(timeline);
                _planTimeline.Bind(timeline, viewModel?.Timeline, visible);
            }
            else
            {
                _planTimeline.Hide(timeline);
                _timeline.Bind(timeline, viewModel?.Timeline, visible, behaviorLogic);
            }
        }

        internal void RestoreLogicLibraryDrawer()
        {
            BindLogicLibraryDrawer(_logicLibraryVisible);
        }

        internal void SetLogicLibraryResponsibilityFilter(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? responsibility)
        {
            _library.SetResponsibilityFilter(responsibility);
        }

        internal void ToggleLogicLibraryDrawer()
        {
            _logicLibraryVisible = !_logicLibraryVisible;
            _library.ClearAssemblySelection();
            BindLogicLibraryDrawer(_logicLibraryVisible);
        }

        private void CloseLogicLibraryDrawer()
        {
            _logicLibraryVisible = false;
            _library.ClearAssemblySelection();
            BindLogicLibraryDrawer(false);
            Fgui.GButton openButton = _logicLibraryRoot?.parent?.asCom?.GetChild(
                "btnWorkbenchEdit")?.asButton;
            if (openButton != null)
                openButton.title = "+ 添加行为卡";
        }

        private void OnLogicLibrarySelectionChanged(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card)
        {
            _logicLibrarySelectedCard = card;
            BindLogicLibrarySelectionDetail(_logicLibraryVisible);
            _logicLibrarySelectionChanged?.Invoke(card);
        }

        private void BindLogicLibrarySelectionDetail(bool visible)
        {
            Fgui.GComponent host = _logicLibraryRoot?.parent;
            if (_sharedMovementCardPresenter?.BindBehaviorCardLibraryMovementDetail(
                    host,
                    _logicLibrarySelectedCard,
                    visible) == true)
                return;
            CharacterTestFairyGuiRuntimeAiLabBehaviorCardSelectionDetailPresenter.Bind(
                host,
                _logicLibrarySelectedCard,
                visible,
                _commandSink,
                _viewId,
                _bindTooltip);
        }

        private void BindLogicLibraryDrawer(bool visible)
        {
            if (_logicLibraryRoot != null && visible)
            {
                // Set size before position because this formal component keeps a
                // right-edge relation to the panel; the reverse order shifts it left.
                _logicLibraryRoot.SetSize(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchHeight);
                _logicLibraryRoot.SetXY(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicSidebarWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY);
                _logicLibraryRoot.sortingOrder = 120;
            }
            _library.Bind(
                _logicLibraryRoot,
                _logicLibraryViewModel,
                visible,
                addToBehaviorGroup: true);
            if (!visible)
            {
                BindLogicLibrarySelectionDetail(false);
                if (_logicLibrarySelectedCard != null)
                {
                    _logicLibrarySelectedCard = null;
                    _logicLibrarySelectionChanged?.Invoke(null);
                }
            }
        }
    }
}
