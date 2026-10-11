using System;
using System.Threading.Tasks;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed partial class TacticalCardSliceCoordinator
    {
        internal TacticalLoadingFlow Loading { get; } = new TacticalLoadingFlow();
        internal bool LoadingRecoveryRequired => _presentationFault;
        private string _loadingProfileId;
        private string _loadingReturnMessage;
        private bool _loadingReturnHome;
        private TacticalDeployment _loadingDeployment;
        private long _deploymentLoadingAttempt;

        private long BeginLoading(TacticalLoadingOperation operation, TacticalLoadingStage stage)
        {
            Loading.TryBegin(operation, stage, out long attempt);
            return attempt;
        }

        private bool LoadingCurrent(long attempt) => this != null && isActiveAndEnabled && Loading.IsCurrent(attempt);

        private void FinishLoading(long attempt)
        {
            if (Loading.Attempt != attempt) return;
            Loading.Complete(attempt);
            _busy = false;
        }

        private void FailLoading(long attempt, string issue, bool retry = true)
        {
            if (LoadingCurrent(attempt)) Loading.Fail(attempt, issue, retry);
        }

        private async Task OpenInitialAsync()
        {
            long attempt = BeginLoading(TacticalLoadingOperation.Opening, TacticalLoadingStage.OpeningProgress);
            _busy = true;
            try
            {
                await CharacterTacticalServices.OpenAsync();
                if (!LoadingCurrent(attempt)) return;
                Loading.Report(attempt, TacticalLoadingStage.PreparingSession);
                int expected = CharacterTacticalServices.Preparation?.Working?.Entities?.Count ?? 0;
                if (_gameManager?.RuntimeContext?.Slice?.Services.CharacterEntities.Count != expected)
                    _gameManager?.RuntimeContext?.SessionBuilder?.RebuildTacticalPreparationSession("已载入战场整备配置。");
                if (!LoadingCurrent(attempt)) return;
                ShowHome("继续恢复最近保存的角色清单和整备；战斗中退出后从整备重新出战。");
            }
            catch (Exception error)
            {
                if (!LoadingCurrent(attempt)) return;
                StatusMessage = "整备配置加载失败：" + error.Message;
                FailLoading(attempt, StatusMessage);
                ShowHome(StatusMessage);
            }
            finally { FinishLoading(attempt); }
        }

        internal bool RetryLoading()
        {
            if (_busy || Loading.Status != TacticalLoadingStatus.Failed || !Loading.CanRetry) return false;
            if (_presentationFault)
                return BeginReturnToPreparation("正在恢复已保存的整备与界面。", false);
            switch (Loading.Operation)
            {
                case TacticalLoadingOperation.Opening: _ = OpenInitialAsync(); return true;
                case TacticalLoadingOperation.Continue: _busy = true; _ = EnterPreparationAsync(false); return true;
                case TacticalLoadingOperation.NewGame: _busy = true; _ = EnterPreparationAsync(true); return true;
                case TacticalLoadingOperation.Profile: _busy = true; _ = EnterPreparationAsync(false, _loadingProfileId); return true;
                case TacticalLoadingOperation.Return: return BeginReturnToPreparation(_loadingReturnMessage, _loadingReturnHome);
                case TacticalLoadingOperation.Deploy:
                    if (State != TacticalPreparationState.Preparation || _loadingDeployment == null) return false;
                    _busy = true; _ = DeployPreparedAsync(_loadingDeployment); return true;
                case TacticalLoadingOperation.Settlement: RequestSaveSettlement(); return _busy;
                case TacticalLoadingOperation.Exit: _busy = true; _ = SaveAndExitAsync(); return true;
                default: return false;
            }
        }

        internal bool ReturnFromLoadingFailure()
        {
            if (_busy || Loading.Status != TacticalLoadingStatus.Failed) return false;
            if (_presentationFault)
                return BeginReturnToPreparation("正在恢复已保存的整备与界面。", false);
            return Loading.DismissFailure(); // Keep the previous live session and its existing navigation.
        }

        private void CompleteDeploymentLoading()
        {
            if (Loading.IsCurrent(_deploymentLoadingAttempt)) Loading.Complete(_deploymentLoadingAttempt);
        }

        private void OnDisable()
        {
            CharacterTacticalServices.RoleBindings?.Cancel("整备入口已停用。");
            Loading.Interrupt();
            _busy = false;
        }

        private void OnDestroy() { Loading.Interrupt(); DisposeApplicationHost(); }
    }
}
