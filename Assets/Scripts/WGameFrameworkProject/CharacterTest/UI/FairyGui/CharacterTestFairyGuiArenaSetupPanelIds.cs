using System;
using MxFramework.Resources;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiArenaSetupPanelIds
    {
        public const string ViewIdValue = "ui.character_test.arena.setup";
        public const string PackageId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageId;
        public const string ComponentName = "ArenaSetupPanel";
        public const string PackageBytesResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesResourceId;
        public const string Atlas0ResourceId = CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0ResourceId;
        public const int SlotsPerTeam = CharacterTestArenaMatchConfig.SlotsPerTeam;

        public const string Scrim = "scrim";
        public const string SetupFrame = "setupFrame";
        public const string BattleStatusFrame = "battleStatusFrame";
        public const string Title = "title";
        public const string Status = "status";
        public const string Message = "message";
        public const string Team1Header = "team1Header";
        public const string Team2Header = "team2Header";
        public const string SummaryHeader = "summaryHeader";
        public const string SummaryText = "summaryText";
        public const string ResultText = "resultText";
        public const string BattleStatusText = "battleStatusText";
        public const string CameraModeText = "cameraModeText";
        public const string CameraModeHint = "cameraModeHint";
        public const string CameraThirdPerson = "btnCameraThirdPerson";
        public const string CameraAuto = "btnCameraAuto";
        public const string Start = "btnStart";
        public const string Replay = "btnReplay";
        public const string Reconfigure = "btnReconfigure";

        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);
        public static ResourceKey PackageBytesKey => CharacterTestFairyGuiLegacyPanelPackageIds.PackageBytesKey;
        public static ResourceKey Atlas0Key => CharacterTestFairyGuiLegacyPanelPackageIds.Atlas0Key;

        public static string SlotToggle(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "Toggle";
        }

        public static string SlotSummary(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "Summary";
        }

        public static string CharacterPrev(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "CharacterPrev";
        }

        public static string CharacterLabel(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "CharacterLabel";
        }

        public static string CharacterNext(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "CharacterNext";
        }

        public static string StrategyPrev(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "StrategyPrev";
        }

        public static string StrategyLabel(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "StrategyLabel";
        }

        public static string StrategyNext(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "StrategyNext";
        }

        public static string VariantPrev(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "VariantPrev";
        }

        public static string VariantLabel(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "VariantLabel";
        }

        public static string VariantNext(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "VariantNext";
        }

        public static string EditGroup(int teamId, int slotIndex)
        {
            return TeamPrefix(teamId) + "Slot" + slotIndex + "EditGroup";
        }

        private static string TeamPrefix(int teamId)
        {
            return teamId == CharacterTestArenaMatchConfig.Team2Id ? "team2" : "team1";
        }
    }

    public static class CharacterTestFairyGuiArenaSetupPanelCommandIds
    {
        public const string ToggleSlot = "character_test.arena.toggle_slot";
        public const string CharacterPrev = "character_test.arena.character_prev";
        public const string CharacterNext = "character_test.arena.character_next";
        public const string StrategyPrev = "character_test.arena.strategy_prev";
        public const string StrategyNext = "character_test.arena.strategy_next";
        public const string VariantPrev = "character_test.arena.variant_prev";
        public const string VariantNext = "character_test.arena.variant_next";
        public const string EditGroup = "character_test.arena.edit_group";
        public const string CameraThirdPerson = "character_test.arena.camera_third_person";
        public const string CameraAuto = "character_test.arena.camera_auto";
        public const string StartBattle = "character_test.arena.start_battle";
        public const string Replay = "character_test.arena.replay";
        public const string Reconfigure = "character_test.arena.reconfigure";
        public const string Refresh = "character_test.arena.refresh";

        public static string ForSlot(string baseCommandId, int teamId, int slotIndex)
        {
            return baseCommandId
                + ".team"
                + teamId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + ".slot"
                + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Normalize(string commandId)
        {
            if (MatchesSlotCommand(commandId, ToggleSlot))
                return ToggleSlot;
            if (MatchesSlotCommand(commandId, CharacterPrev))
                return CharacterPrev;
            if (MatchesSlotCommand(commandId, CharacterNext))
                return CharacterNext;
            if (MatchesSlotCommand(commandId, StrategyPrev))
                return StrategyPrev;
            if (MatchesSlotCommand(commandId, StrategyNext))
                return StrategyNext;
            if (MatchesSlotCommand(commandId, VariantPrev))
                return VariantPrev;
            if (MatchesSlotCommand(commandId, VariantNext))
                return VariantNext;
            if (MatchesSlotCommand(commandId, EditGroup))
                return EditGroup;
            return commandId ?? string.Empty;
        }

        private static bool MatchesSlotCommand(string commandId, string baseCommandId)
        {
            if (string.Equals(commandId, baseCommandId, StringComparison.Ordinal))
                return true;
            if (string.IsNullOrEmpty(commandId) || commandId.Length <= baseCommandId.Length)
                return false;
            return commandId[baseCommandId.Length] == '.'
                && commandId.StartsWith(baseCommandId, StringComparison.Ordinal);
        }
    }
}
