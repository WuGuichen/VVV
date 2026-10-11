using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiArenaSetupPanelPayload
    {
        public int TeamId { get; set; }
        public int SlotIndex { get; set; }
    }

    public sealed class CharacterTestFairyGuiArenaSlotViewModel
    {
        public int TeamId { get; set; }
        public int SlotIndex { get; set; }
        public bool Enabled { get; set; }
        public string ToggleLabel { get; set; } = string.Empty;
        public string SummaryText { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public string StrategyName { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public int BehaviorGroupDraftId { get; set; }
        public string BehaviorGroupName { get; set; } = string.Empty;
        public string BehaviorGroupStatus { get; set; } = string.Empty;
        public string EditGroupLabel { get; set; } = "Edit Group";
    }

    public sealed class CharacterTestFairyGuiArenaSetupPanelViewModel
    {
        private CharacterTestFairyGuiArenaSlotViewModel[] _team1Slots =
            Array.Empty<CharacterTestFairyGuiArenaSlotViewModel>();
        private CharacterTestFairyGuiArenaSlotViewModel[] _team2Slots =
            Array.Empty<CharacterTestFairyGuiArenaSlotViewModel>();

        public string Title { get; set; } = "CharacterTest Arena";
        public string StatusText { get; set; } = "Setup";
        public string Message { get; set; } = string.Empty;
        public string Team1Header { get; set; } = "我方 Team 1";
        public string Team2Header { get; set; } = "敌方 Team 2";
        public string SummaryHeader { get; set; } = "Match Summary";
        public string SummaryText { get; set; } = string.Empty;
        public string ResultText { get; set; } = string.Empty;
        public string BattleStatusText { get; set; } = string.Empty;
        public string CameraModeText { get; set; } = string.Empty;
        public string CameraModeHint { get; set; } = "V to switch";
        public string CameraThirdPersonLabel { get; set; } = "Third";
        public string CameraAutoLabel { get; set; } = "Auto";
        public string StartLabel { get; set; } = "Start Battle";
        public string ReplayLabel { get; set; } = "Run Again";
        public string ReconfigureLabel { get; set; } = "Reconfigure";
        public bool ShowSetup { get; set; } = true;
        public bool ShowBattleStatus { get; set; }
        public bool ShowResult { get; set; }
        public bool ShowCameraControls { get; set; }
        public bool CanStart { get; set; }
        public bool CanReplay { get; set; }
        public CharacterTestCameraMode CurrentCameraMode { get; set; } =
            CharacterTestCameraMode.MultiTargetAuto;
        public IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> Team1Slots => _team1Slots;
        public IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> Team2Slots => _team2Slots;

        public void SetTeam1Slots(IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> slots)
        {
            _team1Slots = CopySlots(slots);
        }

        public void SetTeam2Slots(IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> slots)
        {
            _team2Slots = CopySlots(slots);
        }

        private static CharacterTestFairyGuiArenaSlotViewModel[] CopySlots(
            IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> slots)
        {
            if (slots == null || slots.Count == 0)
                return Array.Empty<CharacterTestFairyGuiArenaSlotViewModel>();

            var copy = new CharacterTestFairyGuiArenaSlotViewModel[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                copy[i] = slots[i];
            return copy;
        }
    }

    internal static class CharacterTestFairyGuiArenaSetupPanelViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiArenaSetupPanelViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(512);
            builder.Append(viewModel.ShowSetup ? '1' : '0').Append('|')
                .Append(viewModel.ShowBattleStatus ? '1' : '0').Append('|')
                .Append(viewModel.ShowResult ? '1' : '0').Append('|')
                .Append(viewModel.CanStart ? '1' : '0').Append('|')
                .Append(viewModel.CanReplay ? '1' : '0').Append('|');
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.StatusText);
            AppendToken(builder, viewModel.Message);
            AppendToken(builder, viewModel.Team1Header);
            AppendToken(builder, viewModel.Team2Header);
            AppendToken(builder, viewModel.SummaryHeader);
            AppendToken(builder, viewModel.SummaryText);
            AppendToken(builder, viewModel.ResultText);
            AppendToken(builder, viewModel.BattleStatusText);
            AppendToken(builder, viewModel.CameraModeText);
            AppendToken(builder, viewModel.CameraModeHint);
            AppendToken(builder, viewModel.CameraThirdPersonLabel);
            AppendToken(builder, viewModel.CameraAutoLabel);
            AppendToken(builder, viewModel.StartLabel);
            AppendToken(builder, viewModel.ReplayLabel);
            AppendToken(builder, viewModel.ReconfigureLabel);
            builder.Append(viewModel.ShowCameraControls ? '1' : '0').Append('|')
                .Append((int)viewModel.CurrentCameraMode).Append('|');
            AppendSlots(builder, viewModel.Team1Slots);
            AppendSlots(builder, viewModel.Team2Slots);
            return builder.ToString();
        }

        private static void AppendSlots(
            StringBuilder builder,
            IReadOnlyList<CharacterTestFairyGuiArenaSlotViewModel> slots)
        {
            builder.Append("slots=").Append(slots != null ? slots.Count : 0).Append('|');
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterTestFairyGuiArenaSlotViewModel slot = slots[i];
                builder.Append(slot.TeamId).Append('|')
                    .Append(slot.SlotIndex).Append('|')
                    .Append(slot.Enabled ? '1' : '0').Append('|');
                AppendToken(builder, slot.ToggleLabel);
                AppendToken(builder, slot.SummaryText);
                AppendToken(builder, slot.CharacterName);
                AppendToken(builder, slot.StrategyName);
                AppendToken(builder, slot.VariantName);
                builder.Append(slot.BehaviorGroupDraftId).Append('|');
                AppendToken(builder, slot.BehaviorGroupName);
                AppendToken(builder, slot.BehaviorGroupStatus);
                AppendToken(builder, slot.EditGroupLabel);
            }
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
