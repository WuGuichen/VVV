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
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
    {
        private void OnLogicLibrarySelectionChanged(
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel card)
        {
            if (_boundRoot == null || _boundViewModel?.ShowBehaviorLogic != true)
                return;
            HideButton(_boundRoot, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry);
        }

        private void BindProjectCardDeleteButton(
            Fgui.GComponent root,
            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel selectedCard)
        {
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                "删除卡",
                selectedCard != null
                    && !string.IsNullOrWhiteSpace(selectedCard.StableCardId),
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                payload: selectedCard?.StableCardId);
            BindTooltip(
                root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry),
                "删除选中的项目行为卡及其单卡文件；如果已被正式行为组引用，删除会被阻止。");
        }

        private void BindLogicLibraryButton(Fgui.GComponent root, bool enabled)
        {
            Fgui.GButton button = root?.GetChild(WorkbenchEditButton)?.asButton;
            if (button == null)
                return;
            button.visible = true;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.title = "+ 添加行为卡";
            button.titleFontSize = 18;
            button.icon = string.Empty;
            Fgui.GObject baseImage = button.GetChild("base");
            if (baseImage != null) baseImage.visible = false;
            Fgui.GObject overOverlay = button.GetChild("overOverlay");
            if (overOverlay != null) overOverlay.visible = false;
            Fgui.GObject downOverlay = button.GetChild("downOverlay");
            if (downOverlay != null) downOverlay.visible = false;
            Fgui.GObject icon = button.GetChild("icon");
            if (icon != null)
                icon.visible = false;
            Fgui.GGraph surface = button.GetChild("logicLibrarySurface")?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = "logicLibrarySurface",
                    touchable = false
                };
                button.AddChild(surface);
            }
            surface.SetSize(button.width, button.height);
            surface.SetXY(0f, 0f);
            surface.DrawRoundRect(
                surface.width,
                surface.height,
                new Color32(32, 120, 104, 255),
                new float[] { 6f, 6f, 6f, 6f });
            button.titleFontSize = 13;
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = new Color32(255, 255, 255, 255);
                title.sortingOrder = 4;
            }
            button.onClick.Set(() =>
            {
                if (!enabled)
                    return;
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? activeResponsibility =
                    _panelContentBinder?.ActiveResponsibilityFilter;
                _behaviorCardWorkspaceCommands?.SetLibraryFilter(
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All);
                _behaviorCardWorkspaceBinder?.SetLogicLibraryResponsibilityFilter(
                    activeResponsibility);
                _behaviorCardWorkspaceBinder?.ToggleLogicLibraryDrawer();
                button.title = "+ 添加行为卡";
            });
        }

        internal void RefreshBehaviorLogicRuntimeOverlay(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (viewModel?.ShowBehaviorLogic != true)
                return;

            _boundViewModel = viewModel;
            if (viewModel.BehaviorCardWorkspace?.Timeline?.CompositionEditor != null || viewModel.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null)
            {
                var header = root.GetChild(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader)?.asCom;
                if (header != null) BindText(header, "runtimeFrameSummaryText", viewModel.TacticalFrameSummary?.Invoke());
            }
            _panelContentBinder.RefreshWorkbenchRowsIfNeeded(root, viewModel);
            _behaviorCardWorkspaceBinder.RefreshBehaviorLogicRuntimeOverlay(
                viewModel.BehaviorCardWorkspace?.Timeline);
            _timelineWorkspaceBinder.RefreshBehaviorLogicRuntimeOverlay(viewModel);
        }

        private void BindTimelineModeHost(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            bool showHost = viewModel?.ShowTimelineModeHost == true;
            bool showPlan = showHost
                && viewModel.ShowBehaviorPlan
                && viewModel.BehaviorPlanExpanded;
            bool showLogic = showHost && viewModel.ShowBehaviorLogic;
            const float modeStripY = TimelineModeHostPlanY;
            const float modeButtonWidth =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonWidth;
            const float runtimeButtonX =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeModeButtonX;
            const float planButtonX =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.PlanModeButtonX;
            const float logicButtonX =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LogicModeButtonX;
            const int modeButtonFontSize =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonFontSize;
            Fgui.GObject host = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeHost);
            Fgui.GObject runtimeButton = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace);
            Fgui.GObject planButton = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan);
            Fgui.GObject logicButton = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic);
            Fgui.GObject behaviorTimeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline);
            Fgui.GObject runtimeTimeline = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace);

            if (host != null)
            {
                host.visible = showHost;
                host.SetSize(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonHeight + 8f);
                host.SetXY(
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeHostX,
                    modeStripY + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonYInset - 4f);
                host.sortingOrder = 5;
                Fgui.GGraph hostGraph = host.asGraph;
                if (hostGraph != null)
                {
                    hostGraph.DrawRoundRect(
                        host.width,
                        host.height,
                        new Color32(14, 20, 26, 230),
                        new float[] { 8f, 8f, 8f, 8f });
                }
            }

            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace,
                viewModel?.RuntimeTraceModeLabel ?? "运行轨迹",
                viewModel?.CanSwitchTimelineMode == true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeRuntimeTrace,
                viewModel?.RuntimeTraceModeSelected == true,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan,
                viewModel?.BehaviorPlanModeLabel ?? "行为计划",
                viewModel?.CanSwitchTimelineMode == true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorPlan,
                viewModel?.BehaviorPlanModeSelected == true,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic,
                viewModel?.BehaviorLogicModeLabel ?? "编辑逻辑",
                viewModel?.CanSwitchTimelineMode == true,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TimelineModeBehaviorLogic,
                viewModel?.BehaviorLogicModeSelected == true,
                checkMode: true);
            if (runtimeButton != null)
            {
                runtimeButton.visible = showHost;
                runtimeButton.SetSize(
                    modeButtonWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonHeight);
                runtimeButton.SetXY(
                    runtimeButtonX,
                    modeStripY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonYInset);
                runtimeButton.sortingOrder = 30;
                Fgui.GButton button = runtimeButton.asButton;
                if (button != null)
                {
                    button.titleFontSize = modeButtonFontSize;
                    ApplySegmentedModeButtonVisual(button, viewModel?.RuntimeTraceModeSelected == true);
                }
                BindTooltip(
                    runtimeButton,
                    viewModel?.CanSwitchTimelineMode == true
                        ? "查看 RuntimeFrame 运行轨迹。"
                        : viewModel?.TimelineModeDisabledReasonText ?? string.Empty);
            }
            if (planButton != null)
            {
                planButton.visible = showHost;
                planButton.SetSize(
                    modeButtonWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonHeight);
                planButton.SetXY(
                    planButtonX,
                    modeStripY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonYInset);
                planButton.sortingOrder = 30;
                Fgui.GButton button = planButton.asButton;
                if (button != null)
                {
                    button.titleFontSize = modeButtonFontSize;
                    ApplySegmentedModeButtonVisual(button, viewModel?.BehaviorPlanModeSelected == true);
                }
                BindTooltip(
                    planButton,
                    viewModel?.CanSwitchTimelineMode == true
                        ? "查看当前行为卡的计划时间轴。"
                        : viewModel?.TimelineModeDisabledReasonText ?? string.Empty);
            }
            if (logicButton != null)
            {
                logicButton.visible = showHost;
                logicButton.SetSize(
                    modeButtonWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonHeight);
                logicButton.SetXY(
                    logicButtonX,
                    modeStripY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.ModeButtonYInset);
                logicButton.sortingOrder = 30;
                Fgui.GButton button = logicButton.asButton;
                if (button != null)
                {
                    button.titleFontSize = modeButtonFontSize;
                    ApplySegmentedModeButtonVisual(button, viewModel?.BehaviorLogicModeSelected == true);
                }
                BindTooltip(
                    logicButton,
                    viewModel?.CanSwitchTimelineMode == true
                        ? "在当前工作台编辑行为卡逻辑。"
                        : viewModel?.TimelineModeDisabledReasonText ?? string.Empty);
            }

            if (behaviorTimeline != null && showHost)
            {
                behaviorTimeline.visible = showPlan || showLogic;
                behaviorTimeline.SetSize(
                    showLogic
                        ? CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicTimelineRootWidth
                        : BehaviorPlanTimelineWidth,
                    BehaviorPlanTimelineHeight);
                behaviorTimeline.SetXY(
                    showLogic
                        ? CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicTimelineRootX
                        : BehaviorPlanTimelineX,
                    BehaviorPlanTimelineY);
                behaviorTimeline.sortingOrder = 20;
            }
            if (runtimeTimeline != null && showHost)
            {
                // RuntimeFrame owns the permanent upper-right decision surface in
                // all modes. Plan and Logic only replace its lower viewport and
                // contextual inspector projection.
                runtimeTimeline.visible = viewModel.ShowTimeline
                    || viewModel.ShowBehaviorPlan
                    || viewModel.ShowBehaviorLogic;
                runtimeTimeline.SetSize(
                    RuntimeTimelineWidth,
                    RuntimeTimelineHeight);
                runtimeTimeline.SetXY(RuntimeTimelineX, RuntimeTimelineY);
                runtimeTimeline.sortingOrder = 10;
            }

            Fgui.GObject apply = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft);
            if (apply != null && showHost)
            {
                apply.visible = showLogic;
                if (showLogic)
                {
                    BindButton(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ApplyDraft,
                        "保存",
                        viewModel.CanApplyDraft,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft);
                    BindTooltip(
                        apply,
                        "保存当前修改；运行中的测试会从第 0 帧重新模拟到保存前所在帧。不会发布正式版本。"
                            + (viewModel.CanApplyDraft
                                || string.IsNullOrWhiteSpace(viewModel.ApplyDraftDisabledReasonText)
                                ? string.Empty
                                : "\n" + viewModel.ApplyDraftDisabledReasonText));
                    Fgui.GButton saveAndRunButton = apply.asButton;
                    if (saveAndRunButton != null)
                    {
                        saveAndRunButton.onClick.Set(() =>
                        {
                            if (!viewModel.CanApplyDraft)
                                return;
                            if (!_behaviorCardWorkspaceBinder.TryCommitPendingInspectorEdit())
                            {
                                const string saveBlockedMessage = "保存失败：请先修正并提交当前字段。";
                                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation(
                                    "action=save-behavior-logic-click result=blocked"
                                    + " reason=pending-inspector-edit"
                                    + " message=" + saveBlockedMessage);
                                _behaviorCardWorkspaceBinder.ShowCommandFeedback(saveBlockedMessage);
                                return;
                            }
                            _commandSink?.Enqueue(new MxUiCommand(
                                _viewId,
                                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft,
                                null));
                        });
                    }
                    apply.SetSize(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicSaveWidth,
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicFloatingControlHeight);
                    apply.SetXY(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicTimelineRootX
                            + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                .BehaviorLogicCommandRailButtonX,
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY
                            + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                .BehaviorLogicCommandRailSaveY);
                    apply.sortingOrder = 40;
                    Fgui.GButton button = apply.asButton;
                    if (button != null)
                    {
                        button.titleFontSize = 15;
                        ApplyCommandRailButtonVisual(button, viewModel.CanApplyDraft, isPrimary: false);
                        Fgui.GObject icon = button.GetChild("icon");
                        if (icon != null)
                            icon.visible = false;
                        Fgui.GTextField title = button.GetChild("title")?.asTextField;
                        if (title != null)
                        {
                            title.relations?.ClearAll();
                            title.SetXY(0f, 0f);
                            title.SetSize(
                                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                    .BehaviorLogicSaveWidth,
                                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                    .BehaviorLogicFloatingControlHeight);
                            title.align = Fgui.AlignType.Center;
                            title.verticalAlign = Fgui.VertAlignType.Middle;
                        }
                    }
                }
            }
            Fgui.GObject publish = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft);
            if (publish != null && showHost)
            {
                publish.visible = showLogic;
                if (showLogic)
                {
                    BindButton(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
                        "发布版本",
                        viewModel.CanPublishDraft,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft);
                    BindTooltip(
                        publish,
                        viewModel.CanPublishDraft
                            ? "把已保存草稿追加为正式版本；不会改变当前运行快照。"
                            : string.IsNullOrWhiteSpace(viewModel.PublishDraftDisabledReasonText)
                                ? "请先保存当前修改。"
                                : viewModel.PublishDraftDisabledReasonText);
                    publish.SetSize(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicSaveWidth,
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicFloatingControlHeight);
                    publish.SetXY(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineRootX
                            + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailButtonX,
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY
                            + CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicCommandRailPublishY);
                    publish.sortingOrder = 40;
                    Fgui.GButton publishButton = publish.asButton;
                    if (publishButton != null)
                    {
                        publishButton.titleFontSize = 15;
                        ApplyCommandRailButtonVisual(publishButton, viewModel.CanPublishDraft, isPrimary: false);
                        Fgui.GObject icon = publishButton.GetChild("icon");
                        if (icon != null)
                            icon.visible = false;
                        Fgui.GTextField title = publishButton.GetChild("title")?.asTextField;
                        if (title != null)
                        {
                            title.relations?.ClearAll();
                            title.SetXY(0f, 0f);
                            title.SetSize(
                                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                    .BehaviorLogicSaveWidth,
                                CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                    .BehaviorLogicFloatingControlHeight);
                            title.align = Fgui.AlignType.Center;
                            title.verticalAlign = Fgui.VertAlignType.Middle;
                        }
                    }
                }
            }
            if (showLogic)
                _behaviorCardWorkspaceBinder.RestoreLogicLibraryDrawer();
        }

        private static void ApplySegmentedModeButtonVisual(Fgui.GButton button, bool selected)
        {
            if (button == null)
                return;
            Fgui.GObject baseImage = button.GetChild("base");
            if (baseImage != null) baseImage.visible = false;
            Fgui.GObject overOverlay = button.GetChild("overOverlay");
            if (overOverlay != null) overOverlay.visible = false;
            Fgui.GObject downOverlay = button.GetChild("downOverlay");
            if (downOverlay != null) downOverlay.visible = false;
            Fgui.GObject iconObj = button.GetChild("icon");
            if (iconObj != null) iconObj.visible = false;

            Fgui.GGraph surface = button.GetChild("segmentedSurface")?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = "segmentedSurface",
                    touchable = false,
                    sortingOrder = 1
                };
                button.AddChildAt(surface, 0);
            }
            surface.visible = selected;
            surface.SetXY(0f, 0f);
            surface.SetSize(button.width, button.height);
            float[] radii = new float[] { 6f, 6f, 6f, 6f };
            if (selected)
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(32, 115, 102, 255),
                    radii);
            }
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = selected
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(148, 166, 180, 255);
                title.sortingOrder = 4;
            }
        }

        internal static void ApplyCommandRailButtonVisual(
            Fgui.GButton button,
            bool enabled,
            bool isPrimary)
        {
            if (button == null)
                return;
            Fgui.GObject baseImage = button.GetChild("base");
            if (baseImage != null) baseImage.visible = false;
            Fgui.GObject overOverlay = button.GetChild("overOverlay");
            if (overOverlay != null) overOverlay.visible = false;
            Fgui.GObject downOverlay = button.GetChild("downOverlay");
            if (downOverlay != null) downOverlay.visible = false;
            Fgui.GObject iconObj = button.GetChild("icon");
            if (iconObj != null) iconObj.visible = false;

            Fgui.GGraph surface = button.GetChild("commandRailSurface")?.asGraph;
            if (surface == null)
            {
                surface = new Fgui.GGraph
                {
                    name = "commandRailSurface",
                    touchable = false,
                    sortingOrder = 1
                };
                button.AddChildAt(surface, 0);
            }
            surface.visible = true;
            surface.SetXY(0f, 0f);
            surface.SetSize(button.width, button.height);
            float[] radii = new float[] { 6f, 6f, 6f, 6f };
            if (isPrimary && enabled)
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(32, 120, 104, 255),
                    radii);
            }
            else if (enabled)
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(32, 44, 54, 240),
                    radii);
            }
            else
            {
                surface.DrawRoundRect(
                    button.width,
                    button.height,
                    new Color32(20, 26, 32, 140),
                    radii);
            }
            Fgui.GTextField title = button.GetChild("title")?.asTextField;
            if (title != null)
            {
                title.color = isPrimary && enabled
                    ? new Color32(255, 255, 255, 255)
                    : enabled
                        ? new Color32(215, 230, 240, 255)
                        : new Color32(90, 104, 115, 255);
                title.sortingOrder = 4;
            }
        }



    }
}
