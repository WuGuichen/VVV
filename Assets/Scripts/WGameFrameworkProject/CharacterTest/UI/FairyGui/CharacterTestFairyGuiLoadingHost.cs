using System;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestLoadingViewModel
    {
        internal TacticalLoadingFlow Flow;
        internal bool RecoveryRequired;
    }

    internal sealed class CharacterTestFairyGuiLoadingBinder : IMxFairyGuiViewBinder<CharacterTestLoadingViewModel>, IDisposable
    {
        private readonly Func<bool> _retry, _back;
        private readonly IMxUiTextProvider _text;
        private Fgui.GComponent _root, _panel;
        private Fgui.GButton _retryButton, _backButton;
        private Fgui.EventCallback0 _retryClick, _backClick;
        private CharacterTestLoadingViewModel _model;
        private bool _needsFocus;
        private readonly MxFairyGuiFocusNavigationMetadata _focus = new MxFairyGuiFocusNavigationMetadata(
            CharacterTestFairyGuiLoadingHost.ViewId, "panel/btnRetry", new[] { "panel/btnRetry", "panel/btnBack" });

        internal CharacterTestFairyGuiLoadingBinder(Func<bool> retry, Func<bool> back, IMxUiTextProvider text)
        { _retry = retry; _back = back; _text = text; _retryClick = Retry; _backClick = Back; }

        public void Bind(IMxFairyGuiComponentHandle component, CharacterTestLoadingViewModel model)
        {
            Fgui.GComponent root = (component as MxFairyGuiComponentHandle)?.Component
                ?? throw new InvalidOperationException("Loading view requires a concrete FairyGUI component.");
            if (!ReferenceEquals(_root, root))
            {
                Dispose();
                _root = root;
                _root.opaque = true;
                _root.focusable = true;
                _panel = root.GetChild("panel")?.asCom
                    ?? throw new InvalidOperationException("Published loading panel is missing.");
                _retryButton = _panel.GetChild("btnRetry")?.asButton
                    ?? throw new InvalidOperationException("Published loading retry button is missing.");
                _backButton = _panel.GetChild("btnBack")?.asButton
                    ?? throw new InvalidOperationException("Published loading back button is missing.");
                _retryButton.onClick.Add(_retryClick);
                _backButton.onClick.Add(_backClick);
                _retryButton.focusable = _retryButton.tabStop = true;
                _backButton.focusable = _backButton.tabStop = true;
            }
            _model = model;
            _needsFocus = true;
            MxFairyGuiFocusNavigation.Configure(_root, _focus);
            TacticalLoadingFlow flow = model.Flow;
            bool failed = flow.Status == TacticalLoadingStatus.Failed;
            SetText("title", L(failed ? "failed" : "title", failed ? "加载未完成" : "正在载入"));
            string stage = StageText(flow.Stage);
            if (flow.TotalUnits > 0)
                stage += " · " + string.Format(L("entities", "实体 {0}/{1}"), flow.CompletedUnits, flow.TotalUnits);
            SetText("stage", stage);
            SetText("detail", failed ? flow.Issue : L("waiting", "完成后自动进入下一步。"));
            _retryButton.visible = _backButton.visible = failed;
            _retryButton.enabled = failed && flow.CanRetry;
            _retryButton.text = model.RecoveryRequired ? L("recover", "恢复整备") : L("retry", "重试");
            _backButton.text = model.RecoveryRequired ? L("recover", "恢复整备") : L("back", "返回");
            // A decorative water trace indicates activity. It has no value or completion semantics.
            var trace = _panel.GetChild("waterTrace");
            if (trace != null) { trace.visible = !failed; trace.touchable = false; }
            Tick();
        }

        internal Fgui.GComponent Surface => _root;

        internal void Tick()
        {
            if (_root == null || _root.isDisposed || _panel == null) return;
            if (_needsFocus && _root.onStage)
            {
                if (_model.Flow.Status == TacticalLoadingStatus.Failed)
                    MxFairyGuiFocusNavigation.RequestDefaultFocus(_root);
                else _root.RequestFocus();
                _needsFocus = false;
            }
            float width = MxFairyGuiStageLayout.ResolveStageWidth(_root, 1920);
            float height = MxFairyGuiStageLayout.ResolveStageHeight(_root, 1080);
            _root.SetSize(width, height);
            _root.GetChild("shade")?.SetSize(width, height);
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            _panel.SetScale(scale, scale);
            _panel.SetXY((width - 720f * scale) * .5f, (height - 420f * scale) * .5f);
            var trace = _panel.GetChild("waterTrace");
            if (trace != null && trace.visible)
                trace.alpha = .24f + .12f * Mathf.Sin(Time.unscaledTime * 1.8f);
        }

        private string StageText(TacticalLoadingStage stage)
        {
            switch (stage)
            {
                case TacticalLoadingStage.OpeningProgress: return L("stage.open", "读取角色与整备进度");
                case TacticalLoadingStage.PreparingSession: return L("stage.prepare", "准备战术会话");
                case TacticalLoadingStage.SavingProgress: return L("stage.save", "保存整备进度");
                case TacticalLoadingStage.PublishingSession: return L("stage.publish", "切换战术会话");
                case TacticalLoadingStage.InstallingBattle: return L("stage.battle", "准备战斗运行时");
                case TacticalLoadingStage.SavingSettlement: return L("stage.settlement", "保存战斗结算");
                case TacticalLoadingStage.Exiting: return L("stage.exit", "正在退出");
                default: return string.Empty;
            }
        }

        private string L(string key, string fallback) => CharacterTestUiText.Resolve(_text, "ui.character_test.loading." + key, fallback);
        private void SetText(string name, string value) { var text = _panel.GetChild(name)?.asTextField; if (text != null) text.text = value ?? string.Empty; }
        private void Retry() { if (_model?.Flow.Status == TacticalLoadingStatus.Failed) _retry?.Invoke(); }
        private void Back() { if (_model?.Flow.Status == TacticalLoadingStatus.Failed) _back?.Invoke(); }

        public void Dispose()
        {
            if (_retryButton != null && !_retryButton.isDisposed) _retryButton.onClick.Remove(_retryClick);
            if (_backButton != null && !_backButton.isDisposed) _backButton.onClick.Remove(_backClick);
            _root = null; _panel = null; _retryButton = null; _backButton = null; _model = null;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(114)]
    public sealed class CharacterTestFairyGuiLoadingHost : MonoBehaviour
    {
        internal static readonly MxUiViewId ViewId = new MxUiViewId("ui.character_test.tactical_loading");
        private MxFairyGuiNavigator _navigator;
        private CharacterTestFairyGuiLoadingBinder _binder;
        private TacticalCardSliceCoordinator _coordinator;
        private GameManager _manager;
        private IRuntimeLogger _logger;
        private CharacterTestTacticalHomeWater _water;
        private readonly CharacterTestLoadingViewModel _model = new CharacterTestLoadingViewModel();
        private int _revision = -1;
        private bool _reportedFailure, _waterFailed;
        public bool IsConfigured => _navigator != null;
        public int VisibleFrameCount { get; private set; }
        public bool IsOpen => _navigator?.IsOpen(ViewId) == true;
        internal bool BlocksInput => _coordinator != null && _coordinator.Loading.Visible;
        public string LastStatusMessage { get; private set; } = string.Empty;

        internal void Configure(GameSlice slice, MxFairyGuiRuntimeShell shell, IMxUiTextProvider text, IRuntimeLogger logger)
        {
            Release();
            _logger = logger ?? NullRuntimeLogger.Instance;
            _manager = GetComponent<GameManager>();
            _coordinator = GetComponent<TacticalCardSliceCoordinator>();
            if (shell == null || slice?.Resources?.ResourceManager == null) return;
            _binder = new CharacterTestFairyGuiLoadingBinder(
                () => _coordinator?.RetryLoading() == true,
                () => _coordinator?.ReturnFromLoadingFailure() == true, text);
            var baseContract = CharacterTestFairyGuiMainMenuManifest.CreateViewContract();
            var contract = new MxUiViewContract(new MxUiViewDescriptor(ViewId,
                CharacterTestFairyGuiMainMenuIds.PackageId, "TacticalLoadingSurface", MxUiLayer.Modal)
                { Modal = true, KeepAlive = false, CloseOnSceneChange = true });
            contract.ViewModelType = typeof(CharacterTestLoadingViewModel).FullName;
            contract.RequiredResources = baseContract.RequiredResources;
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestLoadingViewModel>(contract,
                CharacterTestFairyGuiMainMenuManifest.CreatePackageDescriptor(), _binder));
            var diagnostics = catalog.CreateDiagnostics(slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            { Warn(diagnostics.Issues.Count > 0 ? diagnostics.Issues[0].Message : "Loading catalog unavailable."); return; }
            _navigator = shell.Register(catalog);
        }

        private void LateUpdate()
        {
            if (_coordinator == null) _coordinator = GetComponent<TacticalCardSliceCoordinator>();
            if (_navigator == null || _coordinator == null) return;
            TacticalLoadingFlow flow = _coordinator.Loading;
            if (flow.Status == TacticalLoadingStatus.Failed)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard?.escapeKey.wasPressedThisFrame == true) _coordinator.ReturnFromLoadingFailure();
                else if (keyboard?.enterKey.wasPressedThisFrame == true && flow.CanRetry) _coordinator.RetryLoading();
            }
            if (!flow.Visible)
            {
                Close();
                return;
            }
            if (_revision != flow.Revision || !IsOpen)
            {
                _model.Flow = flow;
                _model.RecoveryRequired = _coordinator.LoadingRecoveryRequired;
                var result = _navigator.Open(ViewId, _model);
                if (result.Success) { _revision = flow.Revision; _reportedFailure = false; LastStatusMessage = string.Empty; }
                else if (!_reportedFailure) { _reportedFailure = true; Warn(result.Message); }
            }
            _binder?.Tick();
            if (IsOpen) VisibleFrameCount++;
            UpdateWater();
        }

        private void UpdateWater()
        {
            bool homeWater = _coordinator.State == TacticalPreparationState.Home
                && _manager?.RuntimeContext?.UiRuntime?.IsMainMenuOpen == true;
            bool active = IsOpen && !homeWater;
            if (active && _water == null && !_waterFailed)
            {
                var rendering = _manager?.SceneRendering;
                if (rendering?.Feature != null && rendering.TargetCamera != null)
                {
                    try { _water = new CharacterTestTacticalHomeWater(rendering.Feature, rendering.TargetCamera,
                        debugName: "CharacterTest.TacticalLoading.Water"); }
                    catch (Exception error) { _waterFailed = true; Warn(error.Message); }
                }
            }
            if (_water != null) { _water.Active = active; if (active) _water.SetSurface(_binder?.Surface); }
        }

        private void Close()
        {
            if (IsOpen) _navigator.Close(ViewId);
            _binder?.Dispose();
            _revision = -1;
            if (_water != null) _water.Active = false;
        }

        internal void Release()
        {
            Close();
            _water?.Dispose(); _water = null;
            _binder = null; _navigator = null; _coordinator = null; _manager = null;
            _reportedFailure = false; _waterFailed = false; LastStatusMessage = string.Empty;
            VisibleFrameCount = 0;
        }

        private void OnDisable() { Close(); _water?.Dispose(); _water = null; }
        private void OnDestroy() => Release();
        private void Warn(string issue) { LastStatusMessage = issue ?? string.Empty; _logger?.Warning("TacticalLoading", LastStatusMessage); }
    }
}
