using System.Collections.Generic;
using System.Globalization;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiArenaSetupPanelViewModelBuilder
    {
        private static readonly CharacterTestCharacterDefinitionRegistry CharacterDefinitions =
            new CharacterTestCharacterDefinitionRegistry();

        public static CharacterTestFairyGuiArenaSetupPanelViewModel Build(
            CharacterTestArenaSessionController controller,
            IMxUiTextProvider textProvider = null)
        {
            CharacterTestArenaSnapshot snapshot = controller != null
                ? controller.CaptureSnapshot()
                : default;
            var viewModel = new CharacterTestFairyGuiArenaSetupPanelViewModel
            {
                Title = L(textProvider, "ui.character_test.arena.title", "CharacterTest Arena"),
                StatusText = FormatStatus(snapshot),
                Message = snapshot.StatusMessage,
                Team1Header = L(textProvider, "ui.character_test.arena.team1", "我方 Team 1"),
                Team2Header = L(textProvider, "ui.character_test.arena.team2", "敌方 Team 2"),
                SummaryHeader = L(textProvider, "ui.character_test.arena.summary", "Match Summary"),
                CameraModeText = "Camera: " + CharacterTestArenaSessionController.FormatCameraMode(snapshot.CameraMode),
                CameraModeHint = "V to switch",
                CameraThirdPersonLabel = "Third",
                CameraAutoLabel = "Auto",
                StartLabel = L(textProvider, "ui.character_test.arena.start", "Start Battle"),
                ReplayLabel = L(textProvider, "ui.character_test.arena.replay", "Run Again"),
                ReconfigureLabel = L(textProvider, "ui.character_test.arena.reconfigure", "Reconfigure")
            };

            viewModel.ShowSetup = snapshot.Phase == CharacterTestArenaMatchPhase.Setup;
            viewModel.ShowBattleStatus = snapshot.Phase == CharacterTestArenaMatchPhase.Running;
            viewModel.ShowResult = snapshot.Phase == CharacterTestArenaMatchPhase.Finished;
            viewModel.ShowCameraControls = snapshot.Phase == CharacterTestArenaMatchPhase.Running;
            viewModel.CurrentCameraMode = snapshot.CameraMode;
            viewModel.CanReplay = snapshot.Phase == CharacterTestArenaMatchPhase.Finished;
            viewModel.SetTeam1Slots(CreateSlotRows(
                snapshot.Config?.Team1Slots,
                snapshot.Config?.BehaviorGroups));
            viewModel.SetTeam2Slots(CreateSlotRows(
                snapshot.Config?.Team2Slots,
                snapshot.Config?.BehaviorGroups));
            viewModel.CanStart = snapshot.Config != null
                && snapshot.Config.CountEnabledSlots(CharacterTestArenaMatchConfig.Team1Id) > 0
                && snapshot.Config.CountEnabledSlots(CharacterTestArenaMatchConfig.Team2Id) > 0;
            viewModel.SummaryText = FormatSetupSummary(snapshot);
            viewModel.BattleStatusText = FormatBattleStatus(snapshot);
            viewModel.ResultText = FormatResult(snapshot);
            return viewModel;
        }

        private static CharacterTestFairyGuiArenaSlotViewModel[] CreateSlotRows(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            CharacterTestArenaBehaviorGroupDraftStore drafts)
        {
            var rows = new CharacterTestFairyGuiArenaSlotViewModel[
                CharacterTestFairyGuiArenaSetupPanelIds.SlotsPerTeam];
            for (int i = 0; i < rows.Length; i++)
            {
                CharacterTestArenaSlotConfig slot = slots != null && i < slots.Count
                    ? slots[i]
                    : null;
                rows[i] = CreateSlotRow(slot, drafts);
            }

            return rows;
        }

        private static CharacterTestFairyGuiArenaSlotViewModel CreateSlotRow(
            CharacterTestArenaSlotConfig slot,
            CharacterTestArenaBehaviorGroupDraftStore drafts)
        {
            if (slot == null)
            {
                return new CharacterTestFairyGuiArenaSlotViewModel
                {
                    ToggleLabel = "OFF",
                    SummaryText = "Empty slot"
                };
            }

            string characterName = ResolveCharacterName(slot.CharacterId);
            ResolveBehaviorGroupNames(
                drafts,
                slot.BehaviorGroupDraftId,
                out string strategyName,
                out string variantName,
                out bool compileSuccess);
            return new CharacterTestFairyGuiArenaSlotViewModel
            {
                TeamId = slot.TeamId,
                SlotIndex = slot.SlotIndex,
                Enabled = slot.Enabled,
                ToggleLabel = slot.Enabled ? "ON" : "OFF",
                CharacterName = characterName,
                StrategyName = strategyName,
                VariantName = variantName,
                BehaviorGroupDraftId = slot.BehaviorGroupDraftId,
                BehaviorGroupName = strategyName,
                BehaviorGroupStatus = variantName,
                EditGroupLabel = "Edit Group",
                SummaryText = "Slot "
                    + (slot.SlotIndex + 1).ToString(CultureInfo.InvariantCulture)
                    + " | "
                    + (slot.Enabled ? "Enabled" : "Disabled")
                    + " | "
                    + (compileSuccess ? "Ready" : "Needs fix")
            };
        }

        private static string ResolveCharacterName(int characterId)
        {
            return CharacterDefinitions.TryGet(characterId, out CharacterTestCharacterDefinition definition)
                ? definition.DisplayName
                : "Unknown character";
        }

        private static void ResolveBehaviorGroupNames(
            CharacterTestArenaBehaviorGroupDraftStore drafts,
            int draftId,
            out string groupName,
            out string statusText,
            out bool compileSuccess)
        {
            if (drafts != null
                && drafts.TryGetDraftDisplay(draftId, out groupName, out statusText, out compileSuccess))
            {
                return;
            }

            groupName = "Unavailable behavior group";
            statusText = "Missing draft";
            compileSuccess = false;
        }

        private static string FormatStatus(CharacterTestArenaSnapshot snapshot)
        {
            switch (snapshot.Phase)
            {
                case CharacterTestArenaMatchPhase.Running:
                    return "Battle running";
                case CharacterTestArenaMatchPhase.Finished:
                    return "Battle finished";
                default:
                    return "Setup";
            }
        }

        private static string FormatSetupSummary(CharacterTestArenaSnapshot snapshot)
        {
            CharacterTestArenaMatchConfig config = snapshot.Config;
            if (config == null)
                return "Arena configuration is unavailable.";

            return "我方启用: "
                + config.CountEnabledSlots(CharacterTestArenaMatchConfig.Team1Id).ToString(CultureInfo.InvariantCulture)
                + " / 敌方启用: "
                + config.CountEnabledSlots(CharacterTestArenaMatchConfig.Team2Id).ToString(CultureInfo.InvariantCulture)
                + "\n默认镜头: "
                + CharacterTestArenaSessionController.FormatCameraMode(config.PreferredCameraMode)
                + " / 战斗中按 V 切换镜头."
                + "\nArena 表现: 我方 = Team 1 / blue, 敌方 = Team 2 / red."
                + "\n队伍设置只选择角色和行为组草稿；行为条目和参数请进入 Edit Group."
                + "\n"
                + FormatCapabilityMatchup(config);
        }

        private static string FormatCapabilityMatchup(CharacterTestArenaMatchConfig config)
        {
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport ally =
                AnalyzeTeamCapabilities(config, CharacterTestArenaMatchConfig.Team1Id);
            CharacterTestRuntimeAiPlannerBehaviorCapabilityReport enemy =
                AnalyzeTeamCapabilities(config, CharacterTestArenaMatchConfig.Team2Id);
            return CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.FormatArenaMatchupText(
                ally,
                enemy);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCapabilityReport AnalyzeTeamCapabilities(
            CharacterTestArenaMatchConfig config,
            int teamId)
        {
            var aggregate = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "character-test.arena.team." + teamId.ToString(CultureInfo.InvariantCulture));
            CharacterTestArenaBehaviorGroupDraftStore drafts = config?.BehaviorGroups;
            IReadOnlyList<CharacterTestArenaSlotConfig> slots =
                teamId == CharacterTestArenaMatchConfig.Team2Id
                    ? config?.Team2Slots
                    : config?.Team1Slots;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterTestArenaSlotConfig slot = slots[i];
                if (slot == null || !slot.Enabled || drafts == null)
                    continue;
                if (!drafts.TryGetDraft(slot.BehaviorGroupDraftId, out CharacterTestArenaBehaviorGroupDraft draft))
                    continue;
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries =
                    draft.Profile?.Entries;
                for (int entryIndex = 0; entries != null && entryIndex < entries.Count; entryIndex++)
                    aggregate.Entries.Add(entries[entryIndex].Clone());
            }

            return CharacterTestRuntimeAiPlannerBehaviorCapabilityAnalyzer.Analyze(
                aggregate,
                drafts?.DefinitionCatalog);
        }

        internal static string FormatBattleStatus(CharacterTestArenaSnapshot snapshot)
        {
            return "我方(蓝) "
                + snapshot.Team1Alive.ToString(CultureInfo.InvariantCulture)
                + "/"
                + snapshot.Team1Total.ToString(CultureInfo.InvariantCulture)
                + "    敌方(红) "
                + snapshot.Team2Alive.ToString(CultureInfo.InvariantCulture)
                + "/"
                + snapshot.Team2Total.ToString(CultureInfo.InvariantCulture)
                + "    Time "
                + snapshot.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                + "s";
        }

        internal static string FormatResult(CharacterTestArenaSnapshot snapshot)
        {
            if (snapshot.Phase != CharacterTestArenaMatchPhase.Finished)
                return string.Empty;

            string result = snapshot.WinnerTeamId > 0
                ? FormatWinner(snapshot.WinnerTeamId)
                : "Draw";
            return result
                + " | "
                + snapshot.ElapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                + "s";
        }

        private static string FormatWinner(int winnerTeamId)
        {
            if (winnerTeamId == CharacterTestArenaMatchConfig.Team1Id)
                return "我方胜利";
            if (winnerTeamId == CharacterTestArenaMatchConfig.Team2Id)
                return "敌方胜利";

            return "Team " + winnerTeamId.ToString(CultureInfo.InvariantCulture) + " wins";
        }

        private static string L(IMxUiTextProvider textProvider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(textProvider, key, fallback);
        }
    }
}
