using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>
    /// Coordinates the full Tactical Card gameplay vertical slice lifecycle:
    /// Character selection整备 -> Dynamic encounter rebuild -> Non-realtime battle -> Outcome & Rematch/Change.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(112)]
    [AddComponentMenu("MxFramework/Game/Character Test/Tactical Card Slice Coordinator")]
    public sealed partial class TacticalCardSliceCoordinator : MonoBehaviour
    {
        private GameManager _gameManager;
        private TacticalCardHud _hud;
        private TacticalBattlePreparationController _preparationController;
        private TacticalDeployment _autoBattleDeployment;
        private bool _autoBattleInstalled;
        private AppFlowController _application;
        private RuntimeHost _applicationHost;
        private long _applicationFrame;
        private bool _busy, _settlementSaved;
        private bool _presentationFault;
        private string _battleId;
        private CharacterTacticalBattleSettlement _settlement;
        internal bool IsBusy => _busy;
        internal bool SettlementSaved => _settlementSaved;
        internal TacticalDeployment BattleDeployment => _autoBattleDeployment;
        public TacticalPreparationState State { get; private set; } = TacticalPreparationState.Home;
        public string StatusMessage { get; private set; } = string.Empty;
        public bool HasBattleSession => _gameManager?.RuntimeContext?.Slice?.Services.Modules.TacticalStage != null
            || _gameManager?.RuntimeContext?.Slice?.Services.Modules.MixedPlayerTacticalStage != null;
        public int CharacterCount => _gameManager?.RuntimeContext?.Slice?.Services.CharacterEntities.Count ?? 0;
        internal bool UsesBattlefieldOverviewPresentation =>
            State == TacticalPreparationState.Preparation || _autoBattleDeployment != null;

        private void Awake() => EnsureDependencies();
        private void Start()
        {
            EnsureDependencies();
            EnsureApplicationFlow();
            _ = OpenInitialAsync();
        }
        private void Update()
        {
            TickApplication(Time.unscaledDeltaTime);
            if (_busy || _presentationFault || State == TacticalPreparationState.Home || State == TacticalPreparationState.Completed) return;
            if (_autoBattleDeployment != null)
            {
                UpdateAutoBattle();
                return;
            }
            var stage = _gameManager?.RuntimeContext?.Slice?.Services.Modules.TacticalStage?.Stage;
            if (stage == null) return;
            if (stage.Phase == TacticalStagePhase.StageCompleted)
            {
                var entities = _gameManager.RuntimeContext.Slice.Services.CharacterEntities;
                int winner = 0; foreach (var entity in entities) if (entity.CombatState.IsAlive) { winner = entity.Team.TeamId; break; }
                CompleteBattle(winner, winner == 0);
                return;
            }
            if (State != TacticalPreparationState.Deploying) return;
            if (stage.Health == TacticalStageHealth.SuspendedByFault)
            {
                string issue = "出战准备失败：" + stage.Fault + " " + stage.ExecutionIssue;
                _preparationController?.ReportBattleIssue(issue);
                HandlePresentationFault(new TacticalSessionPresentationException(issue, new InvalidOperationException(issue)));
                FailLoading(_deploymentLoadingAttempt, StatusMessage);
                return;
            }
            if (stage.Phase == TacticalStagePhase.Planning)
            {
                MoveToPhase(TacticalPreparationState.Battle);
                CompleteDeploymentLoading();
                StatusMessage = string.Empty;
                _hud?.SetPreparationState(State, StatusMessage);
            }
        }
        internal void NotifyPreparationReady(string message)
        {
            EnsureDependencies();
            if (_application == null) { _hud?.SetPreparationState(State, message); return; }
            _presentationFault = false;
            MoveToPhase(TacticalPreparationState.Preparation);
            StatusMessage = message ?? string.Empty;
            _hud?.SetAutomaticBattlePresentation(false);
            _hud?.SetPreparationState(State, StatusMessage);
            _gameManager?.RuntimeContext?.UiRuntime?.OpenTacticalContentPanel();
        }

        private void EnsureDependencies()
        {
            if (_gameManager == null)
                _gameManager = GetComponent<GameManager>() ?? FindAnyObjectByType<GameManager>();
            if (_hud == null)
                _hud = GetComponent<TacticalCardHud>() ?? FindAnyObjectByType<TacticalCardHud>();
            if (_preparationController == null && _gameManager != null)
                _preparationController = _gameManager.GetComponent<TacticalBattlePreparationController>()
                    ?? _gameManager.gameObject.AddComponent<TacticalBattlePreparationController>();
            if (_gameManager != null && _gameManager.GetComponent<TacticalPreparationGroundView>() == null)
                _gameManager.gameObject.AddComponent<TacticalPreparationGroundView>();
        }

        public void OpenSelection()
        {
            if (State == TacticalPreparationState.Preparation)
            { EnsureDependencies(); _gameManager?.RuntimeContext?.UiRuntime?.OpenTacticalContentPanel(); }
            else ReturnToPreparation();
        }
        public void CloseSelection()
        {
            if (State != TacticalPreparationState.Preparation)
                _gameManager?.RuntimeContext?.UiRuntime?.CloseTacticalContentPanel();
        }
        public bool TryDeploy(int heroId, int opponentId, out string issue)
        {
            try
            {
                bool accepted = TryDeployCore(heroId, opponentId, out issue);
                if (!accepted)
                {
                    StatusMessage = issue ?? string.Empty;
                    _hud?.SetPreparationState(State, StatusMessage);
                }
                return accepted;
            }
            catch (Exception error)
            {
                issue = "出战准备失败：" + error.Message;
                if (State == TacticalPreparationState.Deploying) ReturnToPreparation(issue);
                else { StatusMessage = issue; _hud?.SetPreparationState(State, issue); }
                return false;
            }
        }

        private void UpdateAutoBattle()
        {
            var services = _gameManager?.RuntimeContext?.Slice?.Services;
            if (services == null) return;
            if (!_autoBattleInstalled)
            {
                Loading.ReportUnits(_deploymentLoadingAttempt, services.CharacterEntities.Count, _autoBattleDeployment.Participants.Count);
                if (services.CharacterEntities.Count != _autoBattleDeployment.Participants.Count) return;
                if (!TacticalAutoBattleRuntimeInstaller.TryInstall(services, _autoBattleDeployment, out string issue))
                {
                    string failure = "自动对战准备失败：" + issue;
                    _preparationController?.ReportBattleIssue(failure);
                    HandlePresentationFault(new TacticalSessionPresentationException(failure, new InvalidOperationException(failure)));
                    FailLoading(_deploymentLoadingAttempt, StatusMessage);
                    _autoBattleDeployment = null;
                    return;
                }
                _autoBattleInstalled = true; MoveToPhase(TacticalPreparationState.Battle);
                CompleteDeploymentLoading();
                StatusMessage = string.Empty; _hud?.SetPreparationState(State, StatusMessage);
                return;
            }
            TacticalMixedPlayerCardRuntime mixedPlayer = services.Modules.MixedPlayerTacticalStage;
            if (mixedPlayer?.OutcomeReady == true)
            { CompleteBattle(mixedPlayer.WinningTeamId, mixedPlayer.IsDraw); return; }
            if (mixedPlayer?.PlayerDefeated == true)
                _hud?.SetAutomaticBattlePresentation(true);
            var aliveTeams = services.CharacterEntities.Where(entity => entity.CombatState.IsAlive)
                .Select(entity => entity.Team.TeamId).Distinct().ToArray();
            if (aliveTeams.Length > 1) return;
            CompleteBattle(aliveTeams.Length == 0 ? 0 : aliveTeams[0], aliveTeams.Length == 0);
            _hud?.SetPreparationState(State, StatusMessage);
        }

        public bool TryDeployPreparedBattle(out string issue)
        {
            issue = string.Empty;
            try
            {
                EnsureDependencies();
                TacticalBattlePreparationService preparation = CharacterTacticalServices.Preparation;
                if (_busy || Loading.Running || State != TacticalPreparationState.Preparation) { issue = "正在处理流程，请稍候。"; return false; }
                if (CharacterTacticalServices.RoleBindings?.PendingCount > 0) { issue = "请等待动作装配命令完成。"; return false; }
                if (preparation == null || !preparation.EnsurePlayerControlledEntity(out issue)) return false;
                if (preparation == null || !preparation.ValidateForBattle(out issue)) return false;
                if (!TacticalDeployment.TryCreate(preparation.Working, out TacticalDeployment deployment, out issue)) return false;
                _busy = true;
                _ = DeployPreparedAsync(deployment);
                return true;
            }
            catch (Exception error)
            {
                issue = "出战准备失败：" + error.Message;
                if (State == TacticalPreparationState.Deploying) ReturnToPreparation(issue);
                else { StatusMessage = issue; _hud?.SetPreparationState(State, issue); }
                return false;
            }
        }

        private bool TryDeployPreparedCore(TacticalDeployment deployment, out string issue)
        {
            issue = string.Empty;
            if (State != TacticalPreparationState.Preparation) { issue = "正在出战或战斗中，不能重复确认。"; return false; }
            if (_gameManager?.RuntimeContext?.SessionBuilder == null) { issue = "整备服务尚未准备就绪。"; return false; }
            if (!TacticalAutoBattleRuntimeInstaller.TryValidatePreparation(
                _gameManager.RuntimeContext.Slice?.Services, deployment, out issue)) return false;
            MoveToPhase(TacticalPreparationState.Deploying);
            _hud?.SetAutomaticBattlePresentation(!deployment.HasPlayerControlledParticipant);
            StatusMessage = "正在准备多实体自动对战…";
            _hud?.SetPreparationState(State, StatusMessage);
            var previousSlice = _gameManager.RuntimeContext.Slice;
            try
            {
                _autoBattleDeployment = deployment;
                _autoBattleInstalled = false;
                _gameManager.RuntimeContext.SessionBuilder.RebuildTacticalBattleSession(deployment);
                _battleId = Guid.NewGuid().ToString("N"); _settlement = null; _settlementSaved = false;
                _gameManager.RuntimeContext.UiRuntime?.CloseTacticalContentPanel();
                return true;
            }
            catch (TacticalSessionPresentationException) { throw; }
            catch (Exception error)
            {
                if (!ReferenceEquals(_gameManager.RuntimeContext.Slice, previousSlice))
                    throw new TacticalSessionPresentationException("战斗会话已切换，后续界面操作失败：" + error.Message, error);
                issue = "出战准备失败：" + error.Message;
                _autoBattleDeployment = null; _autoBattleInstalled = false;
                NotifyPreparationReady(issue);
                return false;
            }
        }

        private bool TryDeployCore(int heroId, int opponentId, out string issue)
        {
            EnsureDependencies();
            _hud?.SetAutomaticBattlePresentation(false);
            issue = string.Empty;
            if (State != TacticalPreparationState.Preparation) { issue = "正在出战或战斗中，不能重复确认。"; return false; }
            if (_gameManager?.RuntimeContext?.SessionBuilder == null) { issue = "整备服务尚未准备就绪。"; return false; }
            if (!TacticalDeployment.TryCreate(heroId, opponentId, out var deployment, out issue))
            { StatusMessage = issue; _hud?.SetPreparationState(State, issue); return false; }
            var cache = _gameManager.CharacterSceneCache;
            if (cache == null || cache.CharacterInstanceCount < 2)
            {
                issue = "场景缺少双方出生位置配置。"; return false;
            }

            var level = _gameManager.RuntimeContext.SessionBuilder.LoadTacticalStage();
            var oldPlayer = cache.Characters.FirstOrDefault(c => c.StableId == level.PlayerSpawnStableId);
            var oldEnemy = cache.Characters.FirstOrDefault(c => c.StableId == level.OpponentSpawnStableId);
            // The fullscreen preparation session owns the live scene cache and therefore uses
            // battle-entity ids instead of the two legacy spawn ids.  The old manual 1v1 entry
            // remains supported by borrowing those preview poses when its authored ids are absent,
            // then restoring the stable ids required by the legacy tactical stage contract.
            oldPlayer ??= cache.Characters.FirstOrDefault();
            oldEnemy ??= cache.Characters.FirstOrDefault(value => !ReferenceEquals(value, oldPlayer));
            if (oldPlayer == null || oldEnemy == null)
            { issue = "场景缺少双方可用的出生位置。"; return false; }

            var newPlayer = new CharacterTestSceneCharacterCache.CharacterInstance(
                oldPlayer.IsEnabled,
                level.PlayerSpawnStableId,
                oldPlayer.DisplayName,
                heroId,
                oldPlayer.TeamId,
                oldPlayer.ControlOwner,
                oldPlayer.Transform,
                oldPlayer.Group,
                oldPlayer.Notes,
                oldPlayer.RuntimeAiPlannerProfilePath,
                oldPlayer.HasSpawnPose,
                oldPlayer.SpawnPosition,
                oldPlayer.SpawnYawDegrees);

            var newEnemy = new CharacterTestSceneCharacterCache.CharacterInstance(
                oldEnemy.IsEnabled,
                level.OpponentSpawnStableId,
                oldEnemy.DisplayName,
                opponentId,
                oldEnemy.TeamId,
                oldEnemy.ControlOwner,
                oldEnemy.Transform,
                oldEnemy.Group,
                oldEnemy.Notes,
                oldEnemy.RuntimeAiPlannerProfilePath,
                oldEnemy.HasSpawnPose,
                oldEnemy.SpawnPosition,
                oldEnemy.SpawnYawDegrees);

            cache.Configure(new[] { newPlayer, newEnemy });

            MoveToPhase(TacticalPreparationState.Deploying);
            StatusMessage = "正在准备出战…";
            _hud?.SetPreparationState(State, StatusMessage);
            try
            {
                _gameManager.RuntimeContext.SessionBuilder.RebuildTacticalBattleSession(deployment);
                return true;
            }
            catch (TacticalSessionPresentationException error)
            { HandlePresentationFault(error); issue = StatusMessage; return false; }
            catch (Exception error)
            {
                issue = "出战准备失败：" + error.Message;
                ReturnToPreparation(issue);
                return false;
            }
        }

        public bool ReturnToPreparation(string message = "")
            => BeginReturnToPreparation(message, false);

        private bool BeginReturnToPreparation(string message, bool toHome)
        {
            EnsureDependencies();
            var context = _gameManager?.RuntimeContext;
            if (context?.SessionBuilder == null || _busy) return false;
            if (_settlement != null && !_settlementSaved)
            { StatusMessage = "结算尚未保存，请重试保存后再离开。"; ShowResultMenu(); return false; }
            var battle = context.Slice?.Services.Modules.TacticalStage;
            if (battle != null && !battle.CanReturnToPreparation)
            { StatusMessage = "正在完成战斗结算，请稍候再返回整备。"; return false; }
            var mixed = context.Slice?.Services.Modules.MixedPlayerTacticalStage;
            if (mixed != null && !mixed.OutcomeReady && mixed.Stage.Health == TacticalStageHealth.Running
                && (mixed.Stage.Phase == TacticalStagePhase.Executing || mixed.Stage.Phase == TacticalStagePhase.TurnSettling))
            { StatusMessage = "正在完成当前动作，请收势后再返回整备。"; return false; }
            if (Loading.Running) return false;
            _loadingReturnMessage = message; _loadingReturnHome = toHome;
            _busy = true; _ = ReturnToPreparationAsync(message, toHome); return true;
        }

        private async Task DeployPreparedAsync(TacticalDeployment deployment)
        {
            _loadingDeployment = deployment;
            long attempt = BeginLoading(TacticalLoadingOperation.Deploy, TacticalLoadingStage.SavingProgress);
            var context = _gameManager?.RuntimeContext;
            var previousSlice = context?.Slice;
            bool waitingForInstall = false;
            try
            {
                if (!await CharacterTacticalServices.SaveProgressAsync())
                {
                    if (!LoadingCurrent(attempt)) return;
                    NotifyPreparationReady(CharacterTacticalServices.ProgressIssue);
                    FailLoading(attempt, CharacterTacticalServices.ProgressIssue);
                    return;
                }
                if (!LoadingCurrent(attempt) || !ReferenceEquals(_gameManager?.RuntimeContext, context)
                    || !ReferenceEquals(context?.Slice, previousSlice)) return;
                Loading.Report(attempt, TacticalLoadingStage.PreparingSession);
                if (!TryDeployPreparedCore(deployment, out string issue))
                { NotifyPreparationReady(issue); FailLoading(attempt, issue); return; }
                _deploymentLoadingAttempt = attempt;
                Loading.Report(attempt, TacticalLoadingStage.InstallingBattle);
                waitingForInstall = true;
            }
            catch (TacticalSessionPresentationException error)
            { if (LoadingCurrent(attempt)) { HandlePresentationFault(error); FailLoading(attempt, StatusMessage); } }
            catch (Exception error)
            { if (LoadingCurrent(attempt)) { NotifyPreparationReady("出战失败，保留原会话：" + error.Message); FailLoading(attempt, StatusMessage); } }
            finally
            {
                if (Loading.Attempt == attempt)
                { if (!waitingForInstall) FinishLoading(attempt); else _busy = false; }
            }
        }

        private async Task ReturnToPreparationAsync(string message, bool toHome)
        {
            long attempt = BeginLoading(TacticalLoadingOperation.Return, TacticalLoadingStage.PreparingSession);
            TacticalPreparationState previous = State;
            var context = _gameManager?.RuntimeContext;
            var previousSlice = context?.Slice;
            try
            {
                var result = await Loading.ReplaceAsync(attempt,
                    () => context.SessionBuilder.PrepareTacticalSession(),
                    () => CharacterTacticalServices.SaveProgressAsync(),
                    candidate => context.SessionBuilder.PublishTacticalSession(candidate, message),
                    () => LoadingCurrent(attempt) && ReferenceEquals(_gameManager?.RuntimeContext, context) && ReferenceEquals(context?.Slice, previousSlice));
                if (result != TacticalCheckpointTransition.Published)
                { if (LoadingCurrent(attempt)) { StatusMessage = result == TacticalCheckpointTransition.SaveRejected ? CharacterTacticalServices.ProgressIssue : "流程已变更，未替换当前会话。"; FailLoading(attempt, StatusMessage, result != TacticalCheckpointTransition.SavedWithoutPublication); } return; }
                _autoBattleDeployment = null; _autoBattleInstalled = false; _settlement = null;
                _gameManager.RuntimeContext.MainMenuVisible = false;
                _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(false);
                _gameManager.RuntimeContext.UiRuntime?.CloseMainMenu();
                NotifyPreparationReady(message);
                if (toHome) ShowHome("已保存角色清单与整备；继续从整备开始，不恢复战斗中状态。");
            }
            catch (TacticalSessionPresentationException error) { if (LoadingCurrent(attempt)) HandlePresentationFault(error); }
            catch (TacticalCheckpointPublicationException error) { if (LoadingCurrent(attempt)) HandlePresentationFault(new TacticalSessionPresentationException(error.Message, error)); }
            catch (Exception error)
            {
                if (!LoadingCurrent(attempt)) return;
                if (!ReferenceEquals(context?.Slice, previousSlice)) HandlePresentationFault(new TacticalSessionPresentationException("会话已切换，后续界面操作失败：" + error.Message, error));
                else { MoveToPhase(previous); StatusMessage = "返回失败，原会话仍保留：" + error.Message; }
                FailLoading(attempt, StatusMessage);
            }
            finally { if (_presentationFault) FailLoading(attempt, StatusMessage); FinishLoading(attempt); if (this != null && isActiveAndEnabled && Loading.Attempt == attempt) _hud?.SetPreparationState(State, StatusMessage); }
        }

        internal void RequestStartOrContinue()
        {
            if (_busy || Loading.Running) return;
            if (_presentationFault) { ReturnToPreparation("正在重试界面与整备恢复。"); return; }
            if (State == TacticalPreparationState.Completed) { if (!_settlementSaved) RequestSaveSettlement(); else ReturnToPreparation(); return; }
            if (State != TacticalPreparationState.Home)
            {
                _gameManager.RuntimeContext.MainMenuVisible = false;
                _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(false);
                _gameManager.RuntimeContext.UiRuntime?.CloseMainMenu();
                if (State == TacticalPreparationState.Preparation) _gameManager.RuntimeContext.UiRuntime?.OpenTacticalContentPanel();
                return;
            }
            _busy = true; _ = EnterPreparationAsync(!CharacterTacticalServices.HasCheckpoint);
        }

        internal void RequestNewGameOrRetry()
        {
            if (_busy || Loading.Running) return;
            if (_presentationFault) { ReturnToPreparation("正在重试界面与整备恢复。"); return; }
            if (State == TacticalPreparationState.Completed)
            {
                if (!_settlementSaved) { RequestSaveSettlement(); return; }
                TacticalDeployment frozen = _autoBattleDeployment;
                if (frozen == null) { ReturnToPreparation(); return; }
                var previousSlice = _gameManager.RuntimeContext.Slice;
                _loadingDeployment = frozen;
                long attempt = BeginLoading(TacticalLoadingOperation.Deploy, TacticalLoadingStage.PreparingSession);
                try
                {
                    _gameManager.RuntimeContext.SessionBuilder.RebuildTacticalBattleSession(frozen);
                    _autoBattleInstalled = false; _battleId = Guid.NewGuid().ToString("N"); _settlement = null; _settlementSaved = false;
                    MoveToPhase(TacticalPreparationState.Deploying);
                    _deploymentLoadingAttempt = attempt;
                    Loading.Report(attempt, TacticalLoadingStage.InstallingBattle);
                    _gameManager.RuntimeContext.MainMenuVisible = false;
                    _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(false);
                    _gameManager.RuntimeContext.UiRuntime?.CloseMainMenu();
                }
                catch (TacticalSessionPresentationException error) { HandlePresentationFault(error); FailLoading(attempt, StatusMessage); }
                catch (Exception error)
                {
                    if (!ReferenceEquals(_gameManager.RuntimeContext.Slice, previousSlice)) HandlePresentationFault(new TacticalSessionPresentationException("重赛会话已切换，界面操作失败：" + error.Message, error));
                    else StatusMessage = "重试失败，保留结算会话：" + error.Message;
                    FailLoading(attempt, StatusMessage);
                }
                return;
            }
            if (State != TacticalPreparationState.Home) { ReturnToPreparation(); return; }
            _busy = true; _ = EnterPreparationAsync(true);
        }

        internal bool RequestLoadProfile(string profileId, out string issue)
        {
            if (_busy || Loading.Running || (State != TacticalPreparationState.Preparation && State != TacticalPreparationState.Home))
            { issue = "请先返回整备或首页，再选择进度。"; return false; }
            if (!CharacterTacticalServices.Profiles.Any(profile => profile.ProfileId == profileId))
            { issue = "所选进度已不存在，请重新选择。"; return false; }
            _busy = true; _ = EnterPreparationAsync(false, profileId);
            issue = string.Empty; return true;
        }

        private async Task EnterPreparationAsync(bool newGame, string profileId = null)
        {
            _loadingProfileId = profileId;
            long attempt = BeginLoading(newGame ? TacticalLoadingOperation.NewGame : profileId != null
                ? TacticalLoadingOperation.Profile : TacticalLoadingOperation.Continue, TacticalLoadingStage.OpeningProgress);
            var context = _gameManager?.RuntimeContext;
            var previousSlice = context?.Slice;
            try
            {
                CharacterTacticalServiceSet services = null;
                if (newGame) services = await CharacterTacticalServices.PrepareNewGameAsync();
                else if (profileId != null) services = await CharacterTacticalServices.PrepareProfileAsync(profileId);
                else await CharacterTacticalServices.OpenAsync();
                if (!LoadingCurrent(attempt)) return;
                Loading.Report(attempt, TacticalLoadingStage.PreparingSession);
                var result = await Loading.ReplaceAsync(attempt,
                    () => context.SessionBuilder.PrepareTacticalSession(setup: services?.Preparation.Working ?? CharacterTacticalServices.Preparation.Working),
                    () => newGame ? CharacterTacticalServices.CommitNewGameAsync(services)
                        : profileId != null ? CharacterTacticalServices.CommitProfileAsync(services) : CharacterTacticalServices.SaveProgressAsync(),
                    candidate =>
                    {
                        if (services != null) CharacterTacticalServices.PublishNewGame(services);
                        context.SessionBuilder.PublishTacticalSession(candidate, "已恢复战斗外整备进度。");
                    },
                    () => LoadingCurrent(attempt) && ReferenceEquals(_gameManager?.RuntimeContext, context) && ReferenceEquals(context?.Slice, previousSlice));
                if (result != TacticalCheckpointTransition.Published)
                { if (LoadingCurrent(attempt)) { StatusMessage = result == TacticalCheckpointTransition.SaveRejected ? services?.ProgressIssue ?? CharacterTacticalServices.ProgressIssue : "流程已变更，未替换当前会话。"; FailLoading(attempt, StatusMessage, result != TacticalCheckpointTransition.SavedWithoutPublication); } return; }
                _gameManager.RuntimeContext.HasEnteredGameplayFromMainMenu = true;
                _gameManager.RuntimeContext.MainMenuVisible = false;
                _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(false);
                _gameManager.RuntimeContext.UiRuntime?.CloseMainMenu();
                NotifyPreparationReady(newGame ? "新进度已建立，原进度保留。可在“预设与开战”→“工作配置”中选择旧进度。"
                    : profileId != null ? "已切换所选进度，恢复其整备；战斗从确认出战后重新开始。" : "已继续最近保存的整备，请确认出战。");
            }
            catch (TacticalSessionPresentationException error) { if (LoadingCurrent(attempt)) HandlePresentationFault(error); }
            catch (TacticalCheckpointPublicationException error) { if (LoadingCurrent(attempt)) HandlePresentationFault(new TacticalSessionPresentationException(error.Message, error)); }
            catch (Exception error)
            {
                if (!LoadingCurrent(attempt)) return;
                if (!ReferenceEquals(context?.Slice, previousSlice)) HandlePresentationFault(new TacticalSessionPresentationException("整备会话已切换，界面操作失败：" + error.Message, error));
                else StatusMessage = "进入整备失败，原会话保留：" + error.Message;
                FailLoading(attempt, StatusMessage);
            }
            finally { if (_presentationFault) FailLoading(attempt, StatusMessage); FinishLoading(attempt); if (this != null && isActiveAndEnabled && Loading.Attempt == attempt) _hud?.SetPreparationState(State, StatusMessage); }
        }

        private void CompleteBattle(int winner, bool draw)
        {
            if (_settlement != null) return;
            _settlement = new CharacterTacticalBattleSettlement
            { BattleId = _battleId ?? Guid.NewGuid().ToString("N"), WinningTeamId = winner, Draw = draw, CompletedAtUtc = DateTime.UtcNow.ToString("O") };
            MoveToPhase(TacticalPreparationState.Completed);
            _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(true);
            Time.timeScale = 0f;
            StatusMessage = draw ? "双方同时失去战斗能力，平局。" : "阵营 " + winner + " 获胜。";
            ShowResultMenu(); RequestSaveSettlement();
        }

        internal void RequestSaveSettlement()
        { if (_busy || Loading.Running || _settlement == null) return; _busy = true; _ = SaveSettlementAsync(); }
        private async Task SaveSettlementAsync()
        {
            long attempt = BeginLoading(TacticalLoadingOperation.Settlement, TacticalLoadingStage.SavingSettlement);
            try
            {
                bool saved = await CharacterTacticalServices.SaveProgressAsync(_settlement);
                if (!LoadingCurrent(attempt)) return;
                _settlementSaved = saved;
                if (!saved) { StatusMessage = CharacterTacticalServices.ProgressIssue; FailLoading(attempt, StatusMessage); }
            }
            catch (Exception error) { if (LoadingCurrent(attempt)) { StatusMessage = "结算保存失败：" + error.Message; FailLoading(attempt, StatusMessage); } }
            finally { FinishLoading(attempt); if (this != null && isActiveAndEnabled && Loading.Attempt == attempt) ShowResultMenu(); }
        }

        private void ShowResultMenu()
        {
            _hud?.SetPreparationState(State, StatusMessage);
            _gameManager.RuntimeContext.MainMenuVisible = true;
            _gameManager.RuntimeContext.MainMenuCoordinator.SetPaused(false);
            _gameManager.RuntimeContext.MainMenuCoordinator.RefreshEffectivePauseState();
            _gameManager.RuntimeContext.UiRuntime?.CloseTacticalContentPanel();
            _gameManager.RuntimeContext.UiRuntime?.OpenMainMenu();
            _gameManager.RuntimeContext.UiRuntime?.Refresh();
        }

        private void ShowHome(string message)
        {
            MoveToPhase(TacticalPreparationState.Home); StatusMessage = message;
            _hud?.SetPreparationState(State, message);
            _gameManager.RuntimeContext.MainMenuVisible = true;
            _gameManager.RuntimeContext.MainMenuCoordinator.RefreshEffectivePauseState();
            _gameManager.RuntimeContext.UiRuntime?.CloseTacticalContentPanel();
            _gameManager.RuntimeContext.UiRuntime?.OpenMainMenu();
        }

        internal void RequestCloseOrExit()
        {
            if (State != TacticalPreparationState.Home) { BeginReturnToPreparation("已保存进度并返回首页。", true); return; }
            if (_busy || Loading.Running) return; _busy = true; _ = SaveAndExitAsync();
        }
        private async Task SaveAndExitAsync()
        {
            long attempt = BeginLoading(TacticalLoadingOperation.Exit, TacticalLoadingStage.SavingProgress);
            var context = _gameManager?.RuntimeContext;
            var previousSlice = context?.Slice;
            try
            {
                var result = await TacticalFlowCheckpointBarrier.ExitAsync(() => CharacterTacticalServices.SaveProgressAsync(),
                    () => { Loading.Report(attempt, TacticalLoadingStage.Exiting); UnityEngine.Application.Quit(); },
                    () => LoadingCurrent(attempt) && ReferenceEquals(_gameManager?.RuntimeContext, context) && ReferenceEquals(context?.Slice, previousSlice));
                if (LoadingCurrent(attempt) && result != TacticalCheckpointTransition.Published)
                {
                    StatusMessage = result == TacticalCheckpointTransition.SaveRejected ? CharacterTacticalServices.ProgressIssue : "流程已变更，已取消退出。";
                    FailLoading(attempt, StatusMessage);
                }
            }
            catch (Exception error) { if (LoadingCurrent(attempt)) { StatusMessage = "保存并退出失败：" + error.Message; FailLoading(attempt, StatusMessage); } }
            finally { FinishLoading(attempt); }
        }

        private void HandlePresentationFault(TacticalSessionPresentationException error)
        {
            _presentationFault = true;
            StatusMessage = error.Message + "；已暂停后续流程，请重试返回整备。";
            var context = _gameManager?.RuntimeContext;
            if (context != null)
            {
                context.MainMenuVisible = true;
                context.MainMenuCoordinator.SetPaused(true);
                try { context.UiRuntime?.OpenMainMenu(); }
                catch (Exception menuError) { context.Logger?.Warning("TacticalFlow", "恢复菜单绑定失败：" + menuError.Message); }
            }
            _hud?.SetPreparationState(State, StatusMessage);
        }

        private void EnsureApplicationFlow()
        {
            if (_application != null) return;
            _application = new AppFlowController();
            foreach (TacticalPreparationState phase in Enum.GetValues(typeof(TacticalPreparationState))) _application.RegisterState(new SliceState(this, phase));
            _application.Start(TacticalPreparationState.Home.ToString());
            _applicationHost = new RuntimeHost();
            _applicationHost.RegisterModule(new AppFlowRuntimeModule(_application, priority: -1));
            _applicationHost.RegisterModule(new CharacterTacticalRoleBindingRuntimeModule(
                () => CharacterTacticalServices.RoleBindings,
                () => State == TacticalPreparationState.Preparation && !_busy && !_presentationFault));
            _applicationHost.Initialize(); _applicationHost.Start();
        }
        private void MoveToPhase(TacticalPreparationState phase)
        {
            if (_application == null) { State = phase; return; }
            if (_application.CurrentStateId == phase.ToString()) { State = phase; return; }
            _application.RequestTransition(phase.ToString());
            TickApplication(0d);
        }
        private void TickApplication(double delta)
        {
            if (_applicationHost == null) return;
            long savedFrame = CharacterTacticalServices.Player?.LastItemTransactionFrame ?? -1;
            if (savedFrame == long.MaxValue || _applicationFrame == long.MaxValue)
                throw new InvalidOperationException("TACTICAL_APPLICATION_FRAME_EXHAUSTED");
            _applicationFrame = Math.Max(_applicationFrame, savedFrame + 1);
            _applicationHost.Tick(_applicationFrame++, delta, Time.unscaledTimeAsDouble);
        }
        private void DisposeApplicationHost()
        {
            _applicationHost?.Dispose(); _applicationHost = null; _application = null;
        }
        private sealed class SliceState : IAppFlowState
        {
            private readonly TacticalCardSliceCoordinator _owner; private readonly TacticalPreparationState _phase;
            internal SliceState(TacticalCardSliceCoordinator owner, TacticalPreparationState phase) { _owner = owner; _phase = phase; }
            public string StateId => _phase.ToString();
            public void Enter(AppFlowStateContext context, AppFlowTransition transition) => _owner.State = _phase;
            public void Tick(AppFlowTickContext context) { }
            public void Exit(AppFlowStateContext context, AppFlowTransition transition) { }
        }
    }
}
