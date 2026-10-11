using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabPanelContentBinder
    {
        internal void BindBehaviorCardRuntimeHeader(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            Fgui.GComponent header = root.GetChild(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardRuntimeHeader)?.asCom;
            if (header == null)
                return;
            BindText(header, "runtimeHeaderText", viewModel.BehaviorCardRuntimeHeaderText);
            BindText(header, "runtimeStateText", viewModel.BehaviorCardRuntimeStateText);
            BindText(header, "executionSummaryText", viewModel.BehaviorCardExecutionSummaryText);
            BindText(header, "recentEventsText", viewModel.BehaviorCardRecentEventsText);
            bool showRecipeIdentity = viewModel.ShowBehaviorLogic
                && !string.IsNullOrWhiteSpace(viewModel.BehaviorCardRuntimeStateText);
            Fgui.GObject runtimeStatePanel = header.GetChild("runtimeStatePanel");
            if (runtimeStatePanel != null)
                runtimeStatePanel.visible = showRecipeIdentity;
            Fgui.GTextField runtimeStateText = header.GetChild("runtimeStateText")?.asTextField;
            if (runtimeStateText != null)
            {
                runtimeStateText.visible = showRecipeIdentity;
                runtimeStateText.textFormat.size = 14;
            }
            _bindBehaviorCardRuntimeControls?.Invoke(header, viewModel);
            _behaviorCardPreviewSurface?.Bind(
                header,
                viewModel,
                viewModel.ShowBehaviorLogic || viewModel.WorkspaceMode == CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
        }

        internal void BindSetupSlotRows(
            Fgui.GComponent root,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel)
        {
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1Header,
                viewModel.SetupTeam1CountText);
            BindText(
                root,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam2Header,
                viewModel.SetupTeam2CountText);
            CharacterTestRuntimeAiLabSetupSlotRowViewModel[] rows = viewModel.SetupSlotRows;
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                teamId <= CharacterTestArenaMatchConfig.Team2Id;
                teamId++)
            {
                for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
                {
                    CharacterTestRuntimeAiLabSetupSlotRowViewModel row = FindSetupSlotRow(
                        rows,
                        teamId,
                        slotIndex);
                    BindButton(
                        root,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex),
                        row?.Label ?? "不可用槽位",
                        viewModel.CanEditSetupPlacement,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectSetupSlot(teamId, slotIndex),
                        row != null && row.Selected,
                        checkMode: true);
                    Fgui.GButton slotButton = root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlot(teamId, slotIndex))?.asButton;
                    if (slotButton != null)
                    {
                        // Disabled placement editing must not erase slot identity from the
                        // setup overview. Keep the card fully legible while the button itself
                        // remains non-interactive through enabled=false.
                        slotButton.grayed = false;
                        slotButton.visible = true;
                        slotButton.text = (teamId == CharacterTestArenaMatchConfig.Team2Id ? "T2·" : "T1·") + (slotIndex + 1);
                        slotButton.titleFontSize = 14;
                        CenterButtonContent(slotButton, 8f);
                        BindTooltip(slotButton, row?.Label ?? "不可用槽位");
                        slotButton.alpha = row != null && row.Enabled ? 1f : 0.42f;
                        Fgui.GLoader slotIcon = slotButton.GetChild("icon")?.asLoader;
                        if (slotIcon != null)
                        {
                            slotIcon.color = row != null && row.HasError
                                ? new Color32(236, 124, 100, 255)
                                : row != null && row.Enabled
                                    ? teamId == CharacterTestArenaMatchConfig.Team2Id
                                        ? new Color32(255, 117, 117, 255)
                                        : new Color32(104, 183, 255, 255)
                                    : new Color32(125, 141, 151, 255);
                        }
                    }
                    Fgui.GTextField slotTitle = slotButton?.GetChild("title")?.asTextField;
                    if (slotTitle != null)
                    {
                        slotTitle.color = row != null && row.HasError
                            ? new Color32(236, 124, 100, 255)
                            : row != null && row.Enabled
                                ? teamId == CharacterTestArenaMatchConfig.Team2Id
                                    ? new Color32(255, 117, 117, 255)
                                    : new Color32(104, 183, 255, 255)
                                : new Color32(125, 141, 151, 255);
                    }
                    Fgui.GTextField slotLabel = root.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSlotLabel(teamId, slotIndex))?.asTextField;
                    if (slotLabel != null)
                    {
                        slotLabel.text = "槽位 " + (slotIndex + 1) + "\n"
                            + (row != null && row.HasError
                                ? "需修复"
                                : row != null && row.Enabled ? "已启用" : "未启用");
                        slotLabel.color = row != null && row.HasError
                            ? new Color32(236, 124, 100, 255)
                            : row != null && row.Enabled
                                ? teamId == CharacterTestArenaMatchConfig.Team2Id
                                    ? new Color32(255, 117, 117, 255)
                                    : new Color32(120, 184, 255, 255)
                                : new Color32(125, 141, 151, 255);
                    }
                }
            }
        }

        internal static void BindSetupTeamCard(
            Fgui.GComponent root,
            string childName,
            string title,
            string count,
            string validation,
            bool team2)
        {
            Fgui.GComponent card = root.GetChild(childName)?.asCom;
            if (card == null)
                return;

            BindText(card, "title", title);
            BindText(card, "count", count);
            BindText(card, "validation", validation);
            Color teamColor = team2 ? new Color32(255, 101, 101, 255) : new Color32(91, 171, 255, 255);
            Fgui.GTextField titleField = card.GetChild("title")?.asTextField;
            if (titleField != null)
                titleField.color = teamColor;
            Fgui.GGraph accent = card.GetChild("teamAccent")?.asGraph;
            if (accent != null)
                accent.color = teamColor;
            Fgui.GTextField validationField = card.GetChild("validation")?.asTextField;
            if (validationField != null)
            {
                validationField.color = validation != null && validation.Contains("有效")
                    ? new Color32(105, 215, 140, 255)
                    : new Color32(236, 124, 100, 255);
            }
        }

        private static CharacterTestRuntimeAiLabSetupSlotRowViewModel FindSetupSlotRow(
            CharacterTestRuntimeAiLabSetupSlotRowViewModel[] rows,
            int teamId,
            int slotIndex)
        {
            if (rows == null)
                return null;

            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestRuntimeAiLabSetupSlotRowViewModel row = rows[i];
                if (row != null && row.TeamId == teamId && row.SlotIndex == slotIndex)
                    return row;
            }

            return null;
        }

        private void BindButton(Fgui.GComponent root, string childName, string label, bool enabled, string commandId, bool selected = false, bool checkMode = false)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null) return;
            if (checkMode) button.mode = Fgui.ButtonMode.Check;
            button.text = label ?? string.Empty; button.enabled = enabled; button.grayed = !enabled;
            button.selected = selected; button.visible = true; CenterButtonContent(button, 10f);
            button.onClick.Set(() => { if (enabled) _commandSink?.Enqueue(new MxUiCommand(_viewId, commandId, null)); });
        }

        private void BindTooltip(Fgui.GObject target, string text, MxFairyGuiTooltipOptions options = default)
        {
            if (_panelOverlayBinder.TooltipsSuppressed) { _rootSession.UnbindTooltip(target); return; }
            _rootSession.BindTooltip(target, text, options);
        }

        private static void CenterButtonContent(Fgui.GButton button, float gap) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.CenterButtonContent(button, gap);
        private static void BindText(Fgui.GComponent root, string childName, string value) => CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.BindText(root, childName, value);
    }
}
