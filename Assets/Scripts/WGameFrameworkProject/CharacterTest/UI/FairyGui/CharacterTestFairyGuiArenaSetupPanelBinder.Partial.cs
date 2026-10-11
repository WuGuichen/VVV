using System;
using System.Collections.Generic;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiArenaSetupPanelBinder
    {
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;
        private static readonly string[] SetupOnlyDecorationNames =
        {
            "team1Panel",
            "team2Panel",
            "team1Slot0Frame",
            "team1Slot0CharacterLabel",
            "team1Slot0StrategyLabel",
            "team1Slot0VariantLabel",
            "team1Slot0EditGroup",
            "team1Slot1Frame",
            "team1Slot1CharacterLabel",
            "team1Slot1StrategyLabel",
            "team1Slot1VariantLabel",
            "team1Slot1EditGroup",
            "team1Slot2Frame",
            "team1Slot2CharacterLabel",
            "team1Slot2StrategyLabel",
            "team1Slot2VariantLabel",
            "team1Slot2EditGroup",
            "team2Slot0Frame",
            "team2Slot0CharacterLabel",
            "team2Slot0StrategyLabel",
            "team2Slot0VariantLabel",
            "team2Slot0EditGroup",
            "team2Slot1Frame",
            "team2Slot1CharacterLabel",
            "team2Slot1StrategyLabel",
            "team2Slot1VariantLabel",
            "team2Slot1EditGroup",
            "team2Slot2Frame",
            "team2Slot2CharacterLabel",
            "team2Slot2StrategyLabel",
            "team2Slot2VariantLabel",
            "team2Slot2EditGroup"
        };

        partial void OnAfterGeneratedBind(
            Fgui.GComponent root,
            CharacterTestFairyGuiArenaSetupPanelViewModel viewModel)
        {
            ApplyFullScreenRootSize(root);
            ApplyModeVisibility(root, viewModel);
            BindCameraControls(root, viewModel);
            BindSlots(root, viewModel.Team1Slots, CharacterTestArenaMatchConfig.Team1Id, viewModel.ShowSetup);
            BindSlots(root, viewModel.Team2Slots, CharacterTestArenaMatchConfig.Team2Id, viewModel.ShowSetup);
        }

        private void ApplyModeVisibility(
            Fgui.GComponent root,
            CharacterTestFairyGuiArenaSetupPanelViewModel viewModel)
        {
            bool setupOrResult = viewModel.ShowSetup || viewModel.ShowResult;
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Scrim, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.SetupFrame, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Team1Header, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Team2Header, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.SummaryHeader, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.SummaryText, setupOrResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.ResultText, viewModel.ShowResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.BattleStatusFrame, viewModel.ShowBattleStatus);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.BattleStatusText, viewModel.ShowBattleStatus);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.CameraModeText, viewModel.ShowCameraControls);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.CameraModeHint, viewModel.ShowCameraControls);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.CameraThirdPerson, viewModel.ShowCameraControls);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.CameraAuto, viewModel.ShowCameraControls);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Start, viewModel.ShowSetup);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Replay, viewModel.ShowResult);
            SetVisible(root, CharacterTestFairyGuiArenaSetupPanelIds.Reconfigure,
                viewModel.ShowBattleStatus || viewModel.ShowResult);
            SetSetupDecorationVisible(root, viewModel.ShowSetup);
        }

        private void BindCameraControls(
            Fgui.GComponent root,
            CharacterTestFairyGuiArenaSetupPanelViewModel viewModel)
        {
            bool visible = viewModel.ShowCameraControls;
            BindSlotText(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.CameraModeText,
                visible ? viewModel.CameraModeText : string.Empty,
                visible);
            BindSlotText(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.CameraModeHint,
                visible ? viewModel.CameraModeHint : string.Empty,
                visible);
            BindButton(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.CameraThirdPerson,
                viewModel.CameraThirdPersonLabel,
                visible,
                CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraThirdPerson,
                null,
                selected: viewModel.CurrentCameraMode == CharacterTestCameraMode.ThirdPersonControlled,
                checkMode: true);
            BindButton(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.CameraAuto,
                viewModel.CameraAutoLabel,
                visible,
                CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraAuto,
                null,
                selected: viewModel.CurrentCameraMode == CharacterTestCameraMode.MultiTargetAuto,
                checkMode: true);
        }

        private void BindSlots(
            Fgui.GComponent root,
            IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> slots,
            int teamId,
            bool showSetup)
        {
            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
            {
                CharacterTestFairyGuiArenaSlotViewModel slot = slots != null && i < slots.Count
                    ? slots[i]
                    : null;
                bool visible = showSetup && slot != null;
                BindSlotText(root, CharacterTestFairyGuiArenaSetupPanelIds.SlotSummary(teamId, i),
                    visible ? slot.SummaryText : string.Empty,
                    visible);
                BindSelectorLabels(root, teamId, i, slot, visible);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.SlotToggle(teamId, i),
                    visible ? slot.ToggleLabel : string.Empty,
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.ToggleSlot,
                    teamId,
                    i,
                    selected: visible && slot.Enabled);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.CharacterPrev(teamId, i),
                    "<",
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.CharacterPrev,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.CharacterNext(teamId, i),
                    ">",
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.CharacterNext,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.StrategyPrev(teamId, i),
                    "<",
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.StrategyPrev,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.StrategyNext(teamId, i),
                    ">",
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.StrategyNext,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.VariantPrev(teamId, i),
                    "<",
                    false,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.VariantPrev,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.VariantNext(teamId, i),
                    ">",
                    false,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.VariantNext,
                    teamId,
                    i);
                BindSlotButton(
                    root,
                    CharacterTestFairyGuiArenaSetupPanelIds.EditGroup(teamId, i),
                    visible ? slot.EditGroupLabel : string.Empty,
                    visible,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.EditGroup,
                    teamId,
                    i);
            }
        }

        private static void BindSelectorLabels(
            Fgui.GComponent root,
            int teamId,
            int slotIndex,
            CharacterTestFairyGuiArenaSlotViewModel slot,
            bool visible)
        {
            BindSlotText(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.CharacterLabel(teamId, slotIndex),
                visible ? "Character: " + (slot.CharacterName ?? string.Empty) : string.Empty,
                visible);
            BindSlotText(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.StrategyLabel(teamId, slotIndex),
                visible ? "Behavior Group: " + (slot.BehaviorGroupName ?? string.Empty) : string.Empty,
                visible);
            BindSlotText(
                root,
                CharacterTestFairyGuiArenaSetupPanelIds.VariantLabel(teamId, slotIndex),
                visible ? "Status: " + (slot.BehaviorGroupStatus ?? string.Empty) : string.Empty,
                visible);
        }

        private static void BindSlotText(
            Fgui.GComponent root,
            string childName,
            string value,
            bool visible)
        {
            Fgui.GTextField text = root.GetChild(childName)?.asTextField;
            if (text == null)
                return;

            text.visible = visible;
            text.text = value ?? string.Empty;
        }

        private void BindSlotButton(
            Fgui.GComponent root,
            string childName,
            string label,
            bool visible,
            string commandId,
            int teamId,
            int slotIndex,
            bool selected = false)
        {
            Fgui.GButton button = root.GetChild(childName)?.asButton;
            if (button == null)
                return;

            button.visible = visible;
            button.enabled = visible;
            button.grayed = !visible;
            if (string.Equals(
                    commandId,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.ToggleSlot,
                    StringComparison.Ordinal))
            {
                button.mode = Fgui.ButtonMode.Check;
            }
            button.selected = selected;
            button.text = label ?? string.Empty;
            button.onClick.Set(() =>
            {
                if (!visible)
                    return;

                _commandSink?.Enqueue(new MxUiCommand(
                    _viewId,
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.ForSlot(commandId, teamId, slotIndex),
                    new CharacterTestFairyGuiArenaSetupPanelPayload
                    {
                        TeamId = teamId,
                        SlotIndex = slotIndex
                    }));
            });
        }

        private static void SetSetupDecorationVisible(Fgui.GComponent root, bool visible)
        {
            for (int i = 0; i < SetupOnlyDecorationNames.Length; i++)
                SetVisible(root, SetupOnlyDecorationNames[i], visible);
        }

        private static void ApplyFullScreenRootSize(Fgui.GComponent root)
        {
            if (root == null)
                return;

            root.SetSize(
                MxFairyGuiStageLayout.ResolveStageWidth(root, DesignWidth),
                MxFairyGuiStageLayout.ResolveStageHeight(root, DesignHeight));
        }
    }
}
