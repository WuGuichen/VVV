using System;
using MxFramework.Input;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestMainMenuCoordinator :
        ICharacterTestMainMenuController,
        ICharacterTestMainMenuStatusWriter
    {
        private readonly CharacterTestRuntimeContext _context;
        private readonly CharacterTestSceneProfile _profile;
        private readonly CharacterTestRuntimeSessionBuilder _sessionBuilder;

        public CharacterTestMainMenuCoordinator(
            CharacterTestRuntimeContext context,
            CharacterTestSceneProfile profile,
            CharacterTestRuntimeSessionBuilder sessionBuilder)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _profile = profile ?? CharacterTestSceneProfile.LoadDefault();
            _sessionBuilder = sessionBuilder ?? throw new ArgumentNullException(nameof(sessionBuilder));
        }

        public void Initialize()
        {
            _context.MainMenuVisible = CharacterTestUiDefaults.EnableFairyGuiMainMenu
                && CharacterTestUiDefaults.ShowMainMenuOnStart;
            _context.ManualPaused = _profile.StartPaused;
            RefreshEffectivePauseState();
        }

        public void SetPaused(bool paused)
        {
            _context.ManualPaused = paused;
            RefreshEffectivePauseState();
            _context.LogBuffer.Clear()
                .Append("SetPaused manual=")
                .Append(_context.ManualPaused)
                .Append(", effective=")
                .Append(_context.Paused);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        public void TogglePaused()
        {
            _context.ManualPaused = !_context.ManualPaused;
            RefreshEffectivePauseState();
            _context.LogBuffer.Clear()
                .Append("TogglePaused manual=")
                .Append(_context.ManualPaused)
                .Append(", effective=")
                .Append(_context.Paused);
            _context.Logger?.Info("GameManager", _context.LogBuffer);
        }

        public CharacterTestMainMenuStatus CaptureMainMenuStatus()
        {
            var status = new CharacterTestMainMenuStatus();
            CaptureMainMenuStatusInto(status);
            return status;
        }

        public void CaptureMainMenuStatusInto(CharacterTestMainMenuStatus target)
        {
            if (target == null)
                return;

            target.IsPaused = _context.Paused;
            target.IsMenuVisible = _context.MainMenuVisible;
            target.MenuPausesRuntime = CharacterTestUiDefaults.PauseRuntimeWhenMainMenuOpen;
            target.HasEnteredGameplay = _context.HasEnteredGameplayFromMainMenu;
            target.IsNetworkSessionActive = _context.NetworkRunner != null;
            target.NetworkSessionKind = _context.ActiveNetworkSessionKind;
            target.RuntimeFrame = _context.Slice != null ? _context.Slice.CurrentFrame.Value : 0L;
            target.Message = _context.LastNetworkSessionMessage;
            var tactical = _context.Owner.GetComponent<TacticalCard.TacticalCardSliceCoordinator>();
            target.IsTacticalSlice = tactical != null;
            if (tactical != null)
            {
                target.TacticalPhase = (int)tactical.State;
                target.TacticalBusy = tactical.IsBusy;
                target.TacticalSettlementSaved = tactical.SettlementSaved;
                target.TacticalRewardState = tactical.RewardState;
                target.TacticalRewardClaimFailed = tactical.RewardClaimFailed;
                target.TacticalRewardClaiming = tactical.RewardClaiming;
                target.HasTacticalCheckpoint = TacticalCard.CharacterTacticalServices.HasCheckpoint;
                string message = !tactical.IsBusy && tactical.RewardClaimFailed
                    ? "奖励尚未领取，请重试。"
                    : !tactical.IsBusy && tactical.State == TacticalCard.TacticalPreparationState.Completed && !tactical.SettlementSaved
                        ? "结算尚未保存，请重试。" : tactical.StatusMessage;
                target.Message = string.IsNullOrEmpty(tactical.RewardSummary) ? message
                    : tactical.RewardSummary + "\n" + message;
            }
        }

        public CharacterTestMainMenuCommandResult StartOrContinueFromMainMenu()
        {
            var tactical = _context.Owner.GetComponent<TacticalCard.TacticalCardSliceCoordinator>();
            if (tactical != null)
            {
                tactical.RequestStartOrContinue();
                return CharacterTestMainMenuCommandResult.Succeeded("已请求继续战斗或恢复最近整备。");
            }
            if (_context.Slice == null)
                return CharacterTestMainMenuCommandResult.Failed("CharacterTest runtime is not available.");

            bool arenaBattlePending = _context.ArenaSessionController != null
                && _context.ArenaSessionController.Phase == CharacterTestArenaMatchPhase.Setup;
            bool hasArenaSurface = _context.ArenaSessionController != null;
            if (!arenaBattlePending
                && _context.PlayerBehaviorDeckService != null
                && !_context.PlayerBehaviorDeckService.TryLockAndInstallForSession(
                    _context.Slice.Services.Modules.RuntimeAiPlannerCommands,
                    _context.Slice.Services.CharacterEntities,
                    out string lockError))
            {
                string message = "Behavior deck must be repaired before battle: " + lockError;
                _context.LastNetworkSessionMessage = message;
                _context.Logger?.Warning("RuntimeAiPlanner", message);
                _context.UiRuntime?.Refresh();
                return CharacterTestMainMenuCommandResult.Failed(message);
            }

            _context.HasEnteredGameplayFromMainMenu = true;
            _context.MainMenuVisible = false;
            _context.UiRuntime?.FairyGuiNetworkPanelHost?.Close();
            _context.UiRuntime?.CloseMainMenu();
            if (hasArenaSurface)
                _context.UiRuntime?.OpenArenaSetupPanel();
            RefreshEffectivePauseState();
            if (!_context.Paused && UnityEngine.Time.timeScale <= 0f)
            {
                UnityEngine.Time.timeScale = 1f;
            }
            _context.UiRuntime?.Refresh();
            _context.LastNetworkSessionMessage = _context.NetworkRunner != null
                ? _context.LastNetworkSessionMessage
                : "Running offline.";
            string resultMessage = _context.Paused
                ? "CharacterTest menu closed; runtime remains paused."
                : "CharacterTest menu closed; runtime ticking resumed.";
            _context.Logger?.Info("GameManager", resultMessage);
            return CharacterTestMainMenuCommandResult.Succeeded(resultMessage);
        }

        public CharacterTestMainMenuCommandResult RestartOfflineFromMainMenu()
        {
            var tactical = _context.Owner.GetComponent<TacticalCard.TacticalCardSliceCoordinator>();
            if (tactical != null)
            {
                tactical.RequestNewGameOrRetry();
                return CharacterTestMainMenuCommandResult.Succeeded("已请求新进度、重试或返回整备。");
            }
            try
            {
                const string message = "Local offline CharacterTest session reset.";
                _context.MainMenuVisible = true;
                _context.HasEnteredGameplayFromMainMenu = false;
                RefreshEffectivePauseState();
                _sessionBuilder.RebuildRuntimeSession(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    null,
                    message);
                _context.MainMenuVisible = true;
                RefreshEffectivePauseState();
                if (_context.UiRuntime != null)
                {
                    bool opened = _context.UiRuntime.OpenMainMenu();
                    if (!opened)
                    {
                        _context.Logger?.Warning(
                            "GameManager",
                            "CharacterTest offline restart completed, but the main menu could not be reopened.");
                    }

                    _context.UiRuntime.Refresh();
                }

                return CharacterTestMainMenuCommandResult.Succeeded(message);
            }
            catch (Exception exception)
            {
                string message = "CharacterTest offline restart failed: " + exception.Message;
                _context.Logger?.Warning("GameManager", message);
                return CharacterTestMainMenuCommandResult.Failed(message);
            }
        }

        public CharacterTestMainMenuCommandResult OpenNetworkPanelFromMainMenu()
        {
            _context.MainMenuVisible = true;
            RefreshEffectivePauseState();
            bool opened = _context.UiRuntime != null && _context.UiRuntime.OpenNetworkPanelFromMenu();
            if (!opened)
            {
                _context.UiRuntime?.OpenMainMenu();
                return CharacterTestMainMenuCommandResult.Failed("Network panel is unavailable.");
            }

            return CharacterTestMainMenuCommandResult.Succeeded("Network panel opened.");
        }

        public CharacterTestMainMenuCommandResult CloseMainMenu()
        {
            if (_context.Owner.GetComponent<CharacterTestFairyGuiMainMenuHost>()?.TryDismissHomepageConfirmation() == true)
                return CharacterTestMainMenuCommandResult.Succeeded("已取消首页操作。");
            var tactical = _context.Owner.GetComponent<TacticalCard.TacticalCardSliceCoordinator>();
            if (tactical != null)
            { tactical.RequestCloseOrExit(); return CharacterTestMainMenuCommandResult.Succeeded("已请求关闭菜单或保存后退出。"); }
            return StartOrContinueFromMainMenu();
        }

        public void HandleMainMenuInput()
        {
            if (!CharacterTestUiDefaults.EnableFairyGuiMainMenu
                || !CharacterTestUiDefaults.ToggleMainMenuWithPauseInput)
                return;

            // The loading modal owns Escape/Enter, including its failure recovery.
            // Do not close the underlying menu before the modal receives that key.
            if (_context.UiRuntime?.HasTacticalLoading == true)
                return;

            IInputProvider input = _sessionBuilder.ResolveMainMenuInputProvider();
            if (input == null)
                return;

            InputSnapshot snapshot = input.Snapshot;
            bool mainMenuOpen = IsMainMenuOpenForInput();
            if (mainMenuOpen && (snapshot.PausePressed || snapshot.CancelPressed))
            {
                CloseMainMenu();
                return;
            }

            if (!snapshot.PausePressed)
                return;

            if (_context.UiRuntime != null && _context.UiRuntime.HasNonMainMenuPlayerModalOpen)
                return;

            OpenMainMenuFromPauseInput();
        }

        public bool RefreshEffectivePauseState()
        {
            bool previous = _context.Paused;
            _context.Paused = _context.ManualPaused
                || (_context.MainMenuVisible && CharacterTestUiDefaults.PauseRuntimeWhenMainMenuOpen);
            return previous != _context.Paused;
        }

        private void OpenMainMenuFromPauseInput()
        {
            _context.MainMenuVisible = true;
            _context.HasEnteredGameplayFromMainMenu = true;
            RefreshEffectivePauseState();
            bool opened = _context.UiRuntime != null && _context.UiRuntime.OpenMainMenu();
            if (!opened)
            {
                _context.Logger?.Warning(
                    "GameManager",
                    "CharacterTest main menu requested from input but UI host is unavailable.");
            }
        }

        private bool IsMainMenuOpenForInput()
        {
            if (_context.UiRuntime != null)
                return _context.UiRuntime.IsMainMenuOpen;

            return _context.MainMenuVisible;
        }
    }
}
