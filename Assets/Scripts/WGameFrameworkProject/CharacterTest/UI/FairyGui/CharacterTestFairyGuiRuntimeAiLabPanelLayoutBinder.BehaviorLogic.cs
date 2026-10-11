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
        private static void ApplyRuntimeBehaviorGroupLogicLayout(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            if (root == null || viewModel?.ShowBehaviorLogic != true)
                return;

            const float panelX = 0f;
            const float panelY = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY;
            const float panelWidth =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicListPanelWidth;
            const float panelHeight = CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchHeight;
            Fgui.GObject panel = root.GetChild(WorkbenchListPanel);
            if (panel != null)
            {
                panel.visible = true;
                SetObjectGeometry(panel, panelX, panelY, panelWidth, panelHeight);
                panel.sortingOrder = 24;
            }

            Fgui.GObject summary = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchSummaryText);
            if (summary != null)
            {
                summary.visible = false;
                SetObjectGeometry(
                    summary,
                    24f,
                    panelY + 20f,
                    panelWidth
                        - 24f
                        - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicPanelPadding
                        - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicAddCardWidth
                        - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicSaveGroupWidth
                        - 12f
                        - 12f,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicFloatingControlHeight);
                summary.sortingOrder = 30;
                Fgui.GTextField text = summary.asTextField;
                if (text != null)
                    text.textFormat.size = 22;
            }

            Fgui.GObject workbenchList = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList);
            string[] responsibilityTabs =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther
            };
            float[] responsibilityTabWidths = { 74f, 74f, 74f, 74f, 74f, 74f };
            float responsibilityTabX =
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicPanelPadding;
            float responsibilityTabY = panelY
                + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                    .BehaviorLogicResponsibilityTabsTopInset;
            for (int i = 0; i < responsibilityTabs.Length; i++)
            {
                Fgui.GObject tab = root.GetChild(responsibilityTabs[i]);
                if (tab == null)
                    continue;
                tab.visible = true;
                // These are local filters, so edit/read-only state must never disable them.
                // Restore the full enabled state here because this is the final layout pass and
                // older workspace visibility binding may have made the control non-touchable.
                tab.enabled = true;
                tab.grayed = false;
                SetObjectGeometry(
                    tab,
                    responsibilityTabX,
                    responsibilityTabY,
                    responsibilityTabWidths[i],
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicResponsibilityTabHeight);
                tab.sortingOrder = 32;
                responsibilityTabX += responsibilityTabWidths[i] + 4f;
            }
            if (workbenchList != null)
            {
                workbenchList.visible = true;
                float listY = panelY
                    + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicListTopInset;
                float listHeight = panelHeight
                    - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicListTopInset
                    - CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicListBottomInset;
                SetObjectGeometry(
                    workbenchList,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicPanelPadding,
                    listY,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicListWidth,
                    listHeight);
                workbenchList.sortingOrder = 31;
            }

            string[] hiddenControls =
            {
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchDetailText,
                WorkbenchDetailSurface,
                WorkbenchDetailIconBackground,
                WorkbenchDetailIcon,
                WorkbenchDiagnosticBackground,
                WorkbenchDiagnosticText,
                WorkbenchMoreButton,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftActionBar,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftStatusText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftApplyReasonText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraftStatusText,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.UndoDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PublishDraft,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup
            };
            for (int i = 0; i < hiddenControls.Length; i++)
                SetObjectVisible(root, hiddenControls[i], false);

            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry,
                false);
            SetObjectVisible(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry,
                false);

            string[] operationButtons =
            {
                WorkbenchEditButton,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup
            };
            float[] widths =
            {
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicAddCardWidth,
                CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicSaveGroupWidth
            };
            float operationX = panelWidth
                - CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicPanelPadding
                - widths[0]
                - 8f
                - widths[1];
            for (int i = 0; i < operationButtons.Length; i++)
            {
                Fgui.GObject buttonObject = root.GetChild(operationButtons[i]);
                if (buttonObject == null)
                    continue;
                buttonObject.visible = true;
                SetObjectGeometry(
                    buttonObject,
                    operationX,
                    panelY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicFloatingControlYInset,
                    widths[i],
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicFloatingControlHeight);
                buttonObject.sortingOrder = 32;
                Fgui.GButton button = buttonObject.asButton;
                if (button != null)
                {
                    if (string.Equals(
                        operationButtons[i],
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
                        StringComparison.Ordinal))
                    {
                        Fgui.GObject icon = button.GetChild("icon");
                        icon?.SetSize(22f, 22f);
                    }
                    button.titleFontSize = 15;
                    CenterButtonContent(button, 10f);
                }
                if (string.Equals(operationButtons[i], WorkbenchEditButton, StringComparison.Ordinal))
                {
                    Fgui.GGraph surface = button?.GetChild("logicLibrarySurface")?.asGraph;
                    if (surface != null)
                    {
                        surface.SetXY(0f, 0f);
                        surface.SetSize(
                            widths[i],
                            CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                                .BehaviorLogicFloatingControlHeight);
                    }
                }
                else if (string.Equals(
                    operationButtons[i],
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SaveBehaviorGroup,
                    StringComparison.Ordinal))
                {
                    buttonObject.enabled = viewModel.CanSaveBehaviorGroup;
                    buttonObject.grayed = !viewModel.CanSaveBehaviorGroup;
                    if (button != null)
                    {
                        button.title = viewModel.SaveBehaviorGroupLabel;
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(
                            button,
                            viewModel.CanSaveBehaviorGroup,
                            isPrimary: false);
                        Fgui.GObject icon = button.GetChild("icon");
                        if (icon != null)
                            icon.visible = false;
                    }
                }
                operationX += widths[i] + 8f;
            }

            Fgui.GObject createCard = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.CreateBehaviorCard);
            if (createCard != null)
            {
                createCard.visible = true;
                SetObjectGeometry(
                    createCard,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTimelineRootX
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicCommandRailButtonX,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.WorkbenchY
                        + CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                            .BehaviorLogicCommandRailCreateCardY,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout.BehaviorLogicTopCommandWidth,
                    CharacterTestRuntimeAiLabTimelineWorkbenchLayout
                        .BehaviorLogicFloatingControlHeight);
                createCard.sortingOrder = 40;
                Fgui.GButton createButton = createCard.asButton;
                if (createButton != null)
                {
                    createButton.titleFontSize = 14;
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyCommandRailButtonVisual(
                        createButton,
                        enabled: true,
                        isPrimary: true);
                    Fgui.GObject icon = createButton.GetChild("icon");
                    if (icon != null)
                        icon.visible = false;
                    CenterButtonContent(createButton, 10f);
                }
            }

            Fgui.GObject deleteCard = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry);
            if (deleteCard != null)
            {
                deleteCard.visible = false;
                deleteCard.touchable = false;
            }
        }

        private static void ClearObjectRelations(Fgui.GComponent root, string childName)
        {
            Fgui.GObject child = root?.GetChild(childName);
            child?.relations?.ClearAll();
        }

        private static void ApplyCompactObservationLayout(
            Fgui.GComponent root,
            bool compact,
            float workspaceX,
            float workspaceWidth)
        {
            float contentX = compact ? workspaceX + 24f : 1440f;
            float contentWidth = compact ? workspaceWidth - 48f : 420f;
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle, contentX, 136f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb, contentX, 168f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewHeader, contentX, 216f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewInstructionsText, contentX, 246f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewStatusText, contentX, 280f);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceTitle, contentWidth);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspaceBreadcrumb, contentWidth);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewHeader, contentWidth);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewInstructionsText, contentWidth);
            SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PreviewStatusText, contentWidth);
        }

        private static void ApplyTimelineLayout(Fgui.GComponent root, bool expanded)
        {
            float panelY = expanded ? 838f : 1016f;
            float headerY = expanded ? 850f : 1024f;
            float trackY = expanded ? 1048f : 1048f;
            float scrubY = expanded ? 982f : 1028f;
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BottomPanel, 24f, panelY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineHeader, 48f, headerY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineText, 166f, headerY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressTrack, 48f, trackY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressFill, 48f, trackY);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineProgressThumb, 44f, trackY - 5f);
            SetObjectPosition(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineScrubTrack, 48f, scrubY);
            if (expanded)
            {
                SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineRevisionTrackText, 1000f);
                SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineDecisionTrackText, 1000f);
                SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineActionTrackText, 1000f);
                SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineCombatTrackText, 1000f);
                SetObjectWidth(root, CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineWarningTrackText, 1000f);
            }
        }

        private static void SetSetupSlotRowsVisible(Fgui.GComponent root, bool visible)
        {
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    SetObjectVisible(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex),
                        visible);
                    SetObjectVisible(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlotCard(teamId, slotIndex),
                        false);
                    SetObjectVisible(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlotLabel(teamId, slotIndex),
                        false);
                }
            }
        }


        private void BindButton(Fgui.GComponent root, string childName, string label, bool enabled, string commandId, bool selected = false, bool checkMode = false)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null) return;
            if (checkMode) button.mode = Fgui.ButtonMode.Check;
            button.text = label ?? string.Empty; button.enabled = enabled; button.grayed = !enabled;
            button.selected = selected; button.visible = true;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(button, 10f);
            button.onClick.Set(() => { if (enabled) _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null)); });
        }
        private void BindTooltip(Fgui.GObject target, string text, MxFairyGuiTooltipOptions options = default)
        {
            if (_panelOverlayBinder.TooltipsSuppressed) { _rootSession.UnbindTooltip(target); return; }
            _rootSession.BindTooltip(target, text, options);
        }
        private static void BindText(Fgui.GComponent root, string childName, string value) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.BindText(root, childName, value);
        private static void CenterButtonContent(Fgui.GButton button, float gap) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(button, gap);
        private static void ApplyGeneratedRuntimeAiIcon(Fgui.GButton button, string resourceName) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ApplyGeneratedRuntimeAiIcon(button, resourceName);
        private static void SetObjectVisible(Fgui.GComponent root, string childName, bool visible) { var o = root?.GetChild(childName); if (o != null) o.visible = visible; }
        private static void SetObjectSize(Fgui.GComponent root, string childName, float width, float height) { var o = root?.GetChild(childName); if (o != null) o.SetSize(width, height); }
        private static void SetObjectWidth(Fgui.GComponent root, string childName, float width) { var o = root?.GetChild(childName); if (o != null) o.width = width; }
        private static void SetObjectPosition(Fgui.GComponent root, string childName, float x, float y) { var o = root?.GetChild(childName); if (o != null) o.SetPosition(x, y, o.z); }

    }
}
