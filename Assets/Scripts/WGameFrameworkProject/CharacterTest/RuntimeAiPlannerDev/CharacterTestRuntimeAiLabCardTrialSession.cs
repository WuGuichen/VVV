using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.UI.FairyGui;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestAiLabCardTrialPage { Configuration, Observation }
    /// <summary>Owns transient Lab configuration and the published UI lifetime. Execution remains in the production mixed runtime.</summary>
    internal sealed class CharacterTestRuntimeAiLabCardTrialSession : IDisposable, IMxFairyGuiViewBinder<CharacterTestAiLabCardTrialViewModel>
    {
        private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private readonly GameManager _manager;
        private readonly CharacterTestRuntimeContext _context;
        private readonly CharacterTestRuntimeSessionBuilder _builder;
        private CharacterTestFairyGuiAiLabCardTrialSurface _surface;
        private MxFairyGuiRuntimeShell _registeredShell;
        private TacticalCardHud _hud;
        private CharacterTestRuntimeAiLabCardTrialConfiguration _frozen;
        private TacticalDeployment _deployment;
        private TacticalTimeCoordinator _configurationPause;
        internal CharacterTestAiLabCardTrialPage Page { get; private set; }
        internal CharacterTestRuntimeAiLabCardTrialContent Content { get; private set; }
        internal CharacterTestRuntimeAiLabCardTrialConfiguration Draft { get; private set; }
        internal bool IsOpen { get; private set; }
        internal bool OwnsSession { get; private set; }
        internal bool CanReset => OwnsSession && _frozen != null;
        internal string Issue { get; private set; } = "";
        internal string FrozenConditions => _frozen == null ? "尚未建立试验会话。"
            : $"玩家：{Content.Character(_frozen.PlayerCharacterId).DisplayName}\n对手：{Content.Character(_frozen.OpponentCharacterId).DisplayName}\n初始动势 {_frozen.InitialMomentum} · 种子 {_frozen.Seed} · 测试准备费用 {_frozen.PreparationCost}\n初始手牌 {_frozen.InitialHand.Count} 张";
        internal bool HasConfigurationChanges => _frozen != null && !Draft.HasSameConditions(_frozen);
        internal IReadOnlyList<CharacterTestRuntimeAiLabCardTrialSelection> FrozenHand => _frozen?.InitialHand;
        internal int FrozenPlayerCharacterId => _frozen?.PlayerCharacterId ?? 0;
        internal int FrozenPreparationCost => _frozen?.PreparationCost ?? 0;
        internal MxFramework.UI.IMxUiTextProvider CardTextProvider => _controller.CardDescriptionTextProvider;
        internal double CardFrameSeconds => _controller.CardDescriptionFrameSeconds;
        internal TacticalCardHud Hud => _hud;
        internal TacticalMixedPlayerCardRuntime Runtime => OwnsSession ? _context.Slice?.Services.Modules.MixedPlayerTacticalStage : null;
        internal long CurrentFrame => _context.Slice?.CurrentFrame.Value ?? -1;

        internal CharacterTestRuntimeAiLabCardTrialSession(CharacterTestRuntimeAiPlannerDevSceneController controller,
            GameManager manager, CharacterTestRuntimeContext context, CharacterTestRuntimeSessionBuilder builder)
        { _controller = controller; _manager = manager; _context = context; _builder = builder; }

        internal MxFairyGuiNavigator RegisterSurface(MxFairyGuiRuntimeCatalog catalog, MxFairyGuiRuntimeShell shell)
        {
            // The shell survives Slice rebinds. Its registration retains this stable owner;
            // only the disposable rendered surface changes on a same-condition reset.
            if (!ReferenceEquals(_registeredShell, shell)) { shell.Register(catalog); _registeredShell = shell; }
            return shell.Navigator;
        }
        public void Bind(IMxFairyGuiComponentHandle component, CharacterTestAiLabCardTrialViewModel model)
        {
            if (_surface == null) throw new InvalidOperationException("试验界面 owner 已释放。");
            _surface.Bind(component, model);
        }

        internal void ConfigureSurface(GameSlice slice, MxFairyGuiLayerHost layers, IMxFairyGuiInputContextBridge input,
            IMxFairyGuiHost host, MxFairyGuiRuntimeShell shell, IMxFairyGuiPopupService popups)
        {
            ReleaseSurface();
            if (slice?.Resources?.ResourceManager == null) return;
            _surface = new CharacterTestFairyGuiAiLabCardTrialSurface(this, slice, layers, input, host, shell, popups);
            if (IsOpen && !_surface.Open()) throw new InvalidOperationException("独立真实出牌界面尚未成功加载，请核对 Composer 发布产物。");
        }

        internal bool Open()
        {
            try
            {
                Content ??= CharacterTestRuntimeAiLabCardTrialContent.Load();
                Draft ??= Content.CreateDefault();
                Page = CharacterTestAiLabCardTrialPage.Configuration;
                IsOpen = true; Issue = "";
                _controller.SetCardTrialShellVisible(false);
                if (_surface != null && _surface.Open()) return true;
                IsOpen = false; _controller.SetCardTrialShellVisible(true);
                return SetIssue("独立真实出牌界面尚未成功加载，请核对 Composer 发布产物。");
            }
            catch (Exception error) { IsOpen = false; _controller.SetCardTrialShellVisible(true); return SetIssue(error.Message); }
        }

        internal bool Start(bool reset)
        {
            if (!IsOpen || reset && !CanReset) return false;
            var previousSlice = _context.Slice;
            var previousDeployment = _deployment;
            var previousFrozen = _frozen;
            bool previousOwnership = OwnsSession;
            var previousPage = Page;
            try
            {
                var config = (reset ? _frozen : Draft).Clone();
                var deployment = Content.Freeze(config, _builder.ReserveCardTrialStageRunId());
                _hud ??= _manager.GetComponent<TacticalCardHud>() ?? _manager.gameObject.AddComponent<TacticalCardHud>();
                _hud.ConfigureCardTrialPresentation(deployment, Content.Catalog); _hud.enabled = true;
                // Publish rebinds UI synchronously, so expose the replacement's frozen conditions first.
                _deployment = deployment; _frozen = config; OwnsSession = true; Issue = "";
                Page = CharacterTestAiLabCardTrialPage.Observation;
                _builder.RebuildCardTrialBattleSession(deployment);
                // The replacement owns a new time adapter. Never resume the disposed previous adapter.
                _configurationPause = null;
                _controller.SetCardTrialShellVisible(false);
                return true;
            }
            catch (Exception error)
            {
                if (ReferenceEquals(previousSlice, _context.Slice))
                {
                    _deployment = previousDeployment; _frozen = previousFrozen; OwnsSession = previousOwnership;
                    Page = previousPage;
                    if (previousDeployment != null) _hud?.ConfigureCardTrialPresentation(previousDeployment, Content.Catalog);
                    else if (_hud != null) { _hud.Bind(null, null); _hud.enabled = false; }
                }
                return SetIssue(error.Message);
            }
        }

        internal bool ReturnToObservation()
        {
            if (!IsOpen) return false;
            ReleaseConfigurationPause();
            IsOpen = false; OwnsSession = false;
            ReleaseSurface();
            if (_hud != null) { _hud.Bind(null, null); _hud.enabled = false; }
            _deployment = null; _frozen = null;
            try
            {
                bool result = _controller.ResetSession();
                _controller.SetCardTrialShellVisible(true);
                return result;
            }
            catch (Exception error) { return SetIssue(error.Message); }
        }

        internal void Tick() => _surface?.Tick();
        internal void EditConfiguration()
        {
            if (!IsOpen || !OwnsSession) return;
            Page = CharacterTestAiLabCardTrialPage.Configuration;
            var time = Runtime?.Stage.Time;
            if (time != null && !time.HasPauseReason(TacticalPauseReason.UserMenu))
            {
                _configurationPause = time;
                time.RequestPause(TacticalPauseReason.UserMenu);
            }
        }
        internal void ResumeTrial()
        {
            if (!IsOpen || !OwnsSession) return;
            Page = CharacterTestAiLabCardTrialPage.Observation;
            ReleaseConfigurationPause();
        }
        private void ReleaseConfigurationPause()
        {
            if (_configurationPause == null) return;
            if (!_context.Paused) _configurationPause.RequestResume(TacticalPauseReason.UserMenu);
            _configurationPause = null;
        }
        internal void ToggleVisibility() => _surface?.ToggleVisibility();
        internal bool SetIssue(string message)
        { Issue = message ?? ""; _controller.ReportCardTrialIssue(Issue); return false; }
        internal void ReleaseSurface() { _surface?.Dispose(); _surface = null; }

        public void Dispose()
        {
            ReleaseConfigurationPause();
            ReleaseSurface();
            if (OwnsSession) _hud?.CardTrialStage?.ReleaseControl();
            if (_hud != null) { _hud.Bind(null, null); _hud.enabled = false; }
            IsOpen = OwnsSession = false; _deployment = null; _frozen = null;
        }
    }
}
