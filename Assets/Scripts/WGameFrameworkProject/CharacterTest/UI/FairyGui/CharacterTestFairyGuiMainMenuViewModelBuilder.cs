using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiMainMenuViewModelBuilder
    {
        public static CharacterTestFairyGuiMainMenuViewModel Build(
            ICharacterTestMainMenuController controller,
            bool canOpenSettings = false,
            IMxUiTextProvider textProvider = null)
        {
            return Build(controller, canOpenSettings, canOpenAbilities: false, textProvider);
        }

        public static CharacterTestFairyGuiMainMenuViewModel Build(
            ICharacterTestMainMenuController controller,
            bool canOpenSettings,
            bool canOpenAbilities,
            IMxUiTextProvider textProvider = null)
        {
            var viewModel = new CharacterTestFairyGuiMainMenuViewModel();
            var status = new CharacterTestMainMenuStatus();
            CaptureStatusInto(status, controller, textProvider);
            return BuildInto(
                viewModel,
                status,
                controller != null,
                canOpenSettings,
                canOpenAbilities,
                canOpenBehaviorDeck: false,
                textProvider);
        }

        internal static CharacterTestFairyGuiMainMenuViewModel BuildInto(
            CharacterTestFairyGuiMainMenuViewModel target,
            CharacterTestMainMenuStatus status,
            bool controllerAvailable,
            bool canOpenSettings,
            bool canOpenAbilities,
            bool canOpenBehaviorDeck,
            IMxUiTextProvider textProvider = null)
        {
            target ??= new CharacterTestFairyGuiMainMenuViewModel();
            target.Status = status ?? new CharacterTestMainMenuStatus();
            target.Title = L(textProvider, "ui.character_test.main.title", "Pause Menu");
            target.CanStart = controllerAvailable;
            target.CanNetwork = controllerAvailable;
            target.CanAbilities = canOpenAbilities;
            target.CanBehaviorDeck = canOpenBehaviorDeck;
            target.CanSettings = canOpenSettings;
            target.CanRestart = controllerAvailable;
            target.CanClose = controllerAvailable;
            target.StartLabel = target.Status.HasEnteredGameplay
                ? L(textProvider, "ui.character_test.main.resume", "Resume")
                : L(textProvider, "ui.character_test.main.start", "Start");
            target.StatusText = FormatStatus(target.Status, textProvider);
            target.Message = FormatMessage(target.Status, textProvider);
            if (target.Status.IsTacticalSlice)
            {
                var phase = (TacticalCard.TacticalPreparationState)target.Status.TacticalPhase;
                bool home = phase == TacticalCard.TacticalPreparationState.Home;
                bool outcome = phase == TacticalCard.TacticalPreparationState.Completed;
                target.Title = home ? "战术对战" : outcome ? "战斗结算" : "暂停";
                target.StatusText = target.Status.TacticalBusy ? "正在保存或准备会话…" : home ? "战斗外进度" : outcome ? "战斗已冻结" : "世界已暂停";
                target.StartLabel = home ? (target.Status.HasTacticalCheckpoint ? "继续最近整备" : "开始游戏")
                    : outcome ? (target.Status.TacticalSettlementSaved ? "返回整备" : "重试保存结算")
                    : phase == TacticalCard.TacticalPreparationState.Preparation ? "继续整备" : "继续战斗";
                target.CanNetwork = target.CanAbilities = target.CanBehaviorDeck = false;
                target.CanStart = target.CanRestart = target.CanClose = !target.Status.TacticalBusy;
                if (phase == TacticalCard.TacticalPreparationState.Deploying) target.CanStart = target.CanRestart = false;
                if (outcome) target.CanSettings = false;
                if (home && string.IsNullOrWhiteSpace(target.Message)) target.Message = "继续恢复角色清单和战斗外整备。战斗中退出后从整备重新出战。";
            }
            return target;
        }

        internal static void CaptureStatusInto(
            CharacterTestMainMenuStatus target,
            ICharacterTestMainMenuController controller,
            IMxUiTextProvider textProvider = null)
        {
            if (target == null)
                return;

            if (controller is ICharacterTestMainMenuStatusWriter writer)
            {
                writer.CaptureMainMenuStatusInto(target);
                return;
            }

            CharacterTestMainMenuStatus source = controller != null
                ? controller.CaptureMainMenuStatus()
                : null;
            if (source == null)
            {
                target.IsPaused = true;
                target.IsMenuVisible = false;
                target.MenuPausesRuntime = true;
                target.HasEnteredGameplay = false;
                target.IsNetworkSessionActive = false;
                target.NetworkSessionKind = default;
                target.RuntimeFrame = 0L;
                target.Message = L(
                    textProvider,
                    "ui.character_test.main.message.controller_unavailable",
                    "Main menu controller unavailable.");
                return;
            }

            CopyStatus(target, source);
        }

        private static void CopyStatus(
            CharacterTestMainMenuStatus target,
            CharacterTestMainMenuStatus source)
        {
            target.IsPaused = source.IsPaused;
            target.IsMenuVisible = source.IsMenuVisible;
            target.MenuPausesRuntime = source.MenuPausesRuntime;
            target.HasEnteredGameplay = source.HasEnteredGameplay;
            target.IsNetworkSessionActive = source.IsNetworkSessionActive;
            target.NetworkSessionKind = source.NetworkSessionKind;
            target.RuntimeFrame = source.RuntimeFrame;
            target.Message = source.Message;
        }

        private static string FormatStatus(CharacterTestMainMenuStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null)
                return L(textProvider, "ui.character_test.status.unavailable", "Unavailable");

            string pause = status.IsPaused
                ? L(textProvider, "ui.character_test.status.paused", "Paused")
                : L(textProvider, "ui.character_test.status.running", "Running");
            string menuMode = status.MenuPausesRuntime
                ? L(textProvider, "ui.character_test.main.status.menu_pauses", "Menu Pauses")
                : L(textProvider, "ui.character_test.main.status.live_menu", "Live Menu");
            string network = status.IsNetworkSessionActive
                ? status.NetworkSessionKind.ToString()
                : L(textProvider, "ui.character_test.status.offline", "Offline");
            string frame = L(textProvider, "ui.character_test.status.frame", "frame");
            return pause + "  " + menuMode + "  " + network + "  " + frame + " " + Math.Max(0, status.RuntimeFrame);
        }

        private static string FormatMessage(CharacterTestMainMenuStatus status, IMxUiTextProvider textProvider)
        {
            if (status == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(status.Message))
                return status.Message;

            return status.HasEnteredGameplay
                ? L(textProvider, "ui.character_test.main.message.resume", "Resume or manage the current CharacterTest session.")
                : L(textProvider, "ui.character_test.main.message.start", "Start or configure the CharacterTest session.");
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}
