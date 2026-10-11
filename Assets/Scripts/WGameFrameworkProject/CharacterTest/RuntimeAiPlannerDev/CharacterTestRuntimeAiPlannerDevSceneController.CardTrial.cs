using MxFramework.UI;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabCardTrialSession _cardTrial;
        internal bool CardTrialOpen => _cardTrial?.IsOpen == true;
        internal bool CardTrialOwnsSession => _cardTrial?.OwnsSession == true;
        internal bool CardTrialConfigurationOpen => _cardTrial?.IsOpen == true && _cardTrial.OwnsSession
            && _cardTrial.Page == CharacterTestAiLabCardTrialPage.Configuration;

        internal void ConfigureCardTrialSurface(GameSlice slice, MxFairyGuiLayerHost layers,
            IMxFairyGuiInputContextBridge input, IMxFairyGuiHost host, MxFairyGuiRuntimeShell shell,
            IMxFairyGuiPopupService popups)
        {
            _cardTrial ??= new CharacterTestRuntimeAiLabCardTrialSession(this, _gameManager, _context, _sessionBuilder);
            _cardTrial.ConfigureSurface(slice, layers, input, host, shell, popups);
        }

        internal bool OpenCardTrial()
        {
            if (!EnsureRuntimeReady()) return false;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            { _statusMessage = "请先重置单行为观察，再进入独立真实出牌测试。"; return false; }
            RestoreUnityTimeScale();
            return _cardTrial != null && _cardTrial.Open();
        }

        internal void ReleaseCardTrialSurface() => _cardTrial?.ReleaseSurface();
        internal void ReportCardTrialIssue(string message) => _statusMessage = message;
        internal void SetCardTrialShellVisible(bool visible)
        {
            if (_shellCoordinator != null && (_shellCoordinator.CurrentState.Visibility == CharacterTestRuntimeAiLabShellVisibility.Visible) != visible)
                _shellCoordinator.ToggleVisibility();
        }
    }
}
