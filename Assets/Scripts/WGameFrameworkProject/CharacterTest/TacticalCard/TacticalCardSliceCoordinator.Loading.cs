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
        private GameSlice _publishedLevelSlice;
        private ulong _publishedLevelRunId;

        private bool HasPublishedLevelSession => _gameManager?.RuntimeContext?.Slice != null
            && _gameManager.RuntimeContext.ActiveLevelWorldCache != null
            && !string.IsNullOrWhiteSpace(_gameManager.RuntimeContext.ActiveLevelId);

        private bool OwnsPublishedLevelBattle => HasPublishedLevelSession
            && ReferenceEquals(_publishedLevelSlice, _gameManager.RuntimeContext.Slice)
            && _publishedLevelRunId == _publishedLevelSlice.Services.Modules.MixedPlayerTacticalStage?.Stage.StageRunId;

        internal void NotifyLevelSessionPublished(GameSlice slice, TacticalDeployment deployment, ulong runId)
        {
            EnsureDependencies();
            if (!ReferenceEquals(_gameManager?.RuntimeContext?.Slice, slice) || !HasPublishedLevelSession)
                throw new InvalidOperationException("Published level session required.");
            Loading.Interrupt(); // Retire any earlier Home opening, including its late failure callback.
            _busy = false;
            if (deployment == null) return;
            if (slice.Services.Modules.MixedPlayerTacticalStage?.Stage.StageRunId != runId)
                throw new InvalidOperationException("Published level battle identity mismatch.");
            EnsureApplicationFlow();
            _publishedLevelSlice = slice; _publishedLevelRunId = runId;
            _autoBattleDeployment = deployment;
            // PrepareLevelSession already installed the exact authored groups and action bindings.
            _autoBattleInstalled = true;
            _presentationFault = false;
            _battleId = Guid.NewGuid().ToString("N"); _settlement = null; _settlementSaved = false;
            RememberRewardRecipient(deployment);
            MoveToPhase(TacticalPreparationState.Battle);
            StatusMessage = string.Empty;
            _gameManager.RuntimeContext.HasEnteredGameplayFromMainMenu = true;
            _gameManager.RuntimeContext.UiRuntime?.CloseMainMenu();
            _gameManager.RuntimeContext.UiRuntime?.CloseTacticalContentPanel();
            _hud?.SetAutomaticBattlePresentation(false);
            _hud?.SetPreparationState(State, StatusMessage);
        }

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
            _busy = false; _rewardClaimQueued = false;
        }

        private void FailLoading(long attempt, string issue, bool retry = true)
        {
            if (LoadingCurrent(attempt)) Loading.Fail(attempt, issue, retry);
        }

        private Task OpenInitialAsync() => OpenInitialAsync(() => CharacterTacticalServices.OpenAsync());

        internal async Task OpenInitialAsync(Func<Task> openProgress)
        {
            if (!Loading.TryBegin(TacticalLoadingOperation.Opening, TacticalLoadingStage.OpeningProgress, out long attempt)) return;
            var context = _gameManager?.RuntimeContext;
            var initialSlice = context?.Slice;
            bool CurrentOpening() => LoadingCurrent(attempt) && ReferenceEquals(_gameManager?.RuntimeContext, context)
                && ReferenceEquals(context?.Slice, initialSlice);
            _busy = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_gameManager?.InitialLevelId) && !HasPublishedLevelSession)
                    throw new InvalidOperationException("所选关卡未成功发布：" + _gameManager.InitialLevelId);
                await openProgress();
                if (!CurrentOpening()) return;
                if (HasPublishedLevelSession)
                { StatusMessage = string.Empty; _hud?.SetPreparationState(State, StatusMessage); return; }
                Loading.Report(attempt, TacticalLoadingStage.PreparingSession);
                int expected = CharacterTacticalServices.Preparation?.Working?.Entities?.Count ?? 0;
                if (_gameManager?.RuntimeContext?.Slice?.Services.CharacterEntities.Count != expected)
                {
                    try { context?.SessionBuilder?.RebuildTacticalPreparationSession("已载入战场整备配置。"); }
                    finally { initialSlice = context?.Slice; }
                }
                if (!LoadingCurrent(attempt)) return;
                if (!RestorePendingReward()) ShowHome("继续恢复最近保存的角色清单和整备；战斗中退出后从整备重新出战。");
            }
            catch (Exception error)
            {
                if (!CurrentOpening()) return;
                StatusMessage = "整备配置加载失败：" + error.Message;
                FailLoading(attempt, StatusMessage);
                if (!HasPublishedLevelSession) ShowHome(StatusMessage);
                else _hud?.SetPreparationState(State, StatusMessage);
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
            _busy = false; _rewardClaimQueued = false;
        }

        private void OnDestroy() { Loading.Interrupt(); DisposeApplicationHost(); }
    }
}
