using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiLabCardTrialIds
    {
        internal const string OpenCommandId = CharacterTestAiLabCardTrialControlContract.OpenCommand;
        internal static readonly MxUiViewId ViewId = new("ui.character_test.ai_lab.card_trial");
    }
    internal sealed class CharacterTestAiLabCardTrialViewModel { }

    /// <summary>Published configuration and observation pages; battle commands retain the existing session owner.</summary>
    internal sealed partial class CharacterTestFairyGuiAiLabCardTrialSurface : IMxFairyGuiViewBinder<CharacterTestAiLabCardTrialViewModel>, IDisposable
    {
        private readonly CharacterTestRuntimeAiLabCardTrialSession _session;
        private readonly GameSlice _slice;
        private readonly CharacterTestFairyGuiCharacterImageBinding _characterImages;
        private readonly MxFairyGuiNavigator _navigator;
        private readonly bool _ownsNavigator;
        private Fgui.GComponent _root;
        private Fgui.GList _configuration, _hand, _frozenHand;
        private Fgui.GTextField _title, _subtitle, _readout, _feedback, _observationFeedback, _conditions, _conditionsDetail, _configurationTitle;
        private Fgui.GTextInput _momentum, _seed, _cost;
        private Fgui.GButton _player, _opponent, _start, _reset, _confirm, _cancel, _resume;
        private Fgui.GObject[] _configurationControls, _observationControls, _pickerControls, _restartControls, _trialMenuControls;
        private readonly CharacterTestFairyGuiTacticalCardSkin _skin = new();
        private readonly MxFairyGuiTooltipService _tooltips = new();
        private readonly Dictionary<TacticalCardDefinition, CharacterTestCardUiPreviewData> _cardData = new();
        private ulong _handRevision = ulong.MaxValue;
        private float _nextObservation;
        private bool _visible = true, _disposed, _binding, _conditionsDetailsVisible, _trialMenuOpen;
        private string _configurationIssue = "";
        private CharacterTestAiLabCardTrialPage _renderedPage;

        internal CharacterTestFairyGuiAiLabCardTrialSurface(CharacterTestRuntimeAiLabCardTrialSession session, GameSlice slice,
            MxFairyGuiLayerHost layers, IMxFairyGuiInputContextBridge input, IMxFairyGuiHost host,
            MxFairyGuiRuntimeShell shell, IMxFairyGuiPopupService popups)
        {
            _session = session;
            _slice = slice;
            _characterImages = new CharacterTestFairyGuiCharacterImageBinding(slice.Resources.ResourceManager);
            var source = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreateViewContract();
            var contract = new MxUiViewContract(new MxUiViewDescriptor(CharacterTestRuntimeAiLabCardTrialIds.ViewId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId, CharacterTestAiLabCardTrialControlContract.Component, MxUiLayer.Panel)
                { CloseOnSceneChange = true, KeepAlive = false, Modal = false });
            contract.ViewModelType = typeof(CharacterTestAiLabCardTrialViewModel).FullName;
            contract.RequiredResources = source.RequiredResources;
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestAiLabCardTrialViewModel>(contract,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor(), session));
            _ownsNavigator = shell == null;
            _navigator = shell != null ? session.RegisterSurface(catalog, shell) : MxFairyGuiRuntimeShellComposition.CreateNavigator(
                slice.Resources.ResourceManager, catalog, host: host, layerHost: layers, inputBridge: input, popupService: popups);
        }

        internal bool Open() => !_disposed && (_navigator.IsOpen(CharacterTestRuntimeAiLabCardTrialIds.ViewId)
            || _navigator.Open(CharacterTestRuntimeAiLabCardTrialIds.ViewId, new CharacterTestAiLabCardTrialViewModel()).Success);

        public void Bind(IMxFairyGuiComponentHandle component, CharacterTestAiLabCardTrialViewModel args)
        {
            _root = (component as MxFairyGuiComponentHandle)?.Component
                ?? throw new InvalidOperationException("独立真实出牌测试需要已发布的 FairyGUI 组件。");
            ValidateControls(_root, CharacterTestAiLabCardTrialControlContract.RootControls);
            _root.opaque = false;
            Required("setupBackdrop").touchable = true;
            foreach (var spec in CharacterTestAiLabCardTrialControlContract.RootControls)
                if (spec.Kind == "text" || spec.Kind == "graph" || spec.Kind == "image") Required(spec.Name).touchable = false;
            Required("setupBackdrop").touchable = true;
            foreach (string name in new[] { "configurationBackground", "headerBackground", "footerBackground", "pickerBackground", "restartShield", "conditionsDetailBackground", "trialMenuBackground" })
                Required(name).touchable = true;
            _configurationControls = Controls(CharacterTestAiLabCardTrialControlContract.ConfigurationControls);
            _observationControls = Controls(CharacterTestAiLabCardTrialControlContract.ObservationControls);
            _pickerControls = Controls(CharacterTestAiLabCardTrialControlContract.PickerControls);
            _restartControls = Controls(CharacterTestAiLabCardTrialControlContract.RestartControls);
            _trialMenuControls = Controls(CharacterTestAiLabCardTrialControlContract.TrialMenuControls);
            _title = Required("title").asTextField; _subtitle = Required("subtitle").asTextField;
            _configuration = Required("initialHand").asList; _hand = Required("hand").asList;
            _readout = Required("observation").asTextField; _feedback = Required("feedback").asTextField;
            _observationFeedback = Required("observationFeedback").asTextField;
            _observationFeedback.autoSize = Fgui.AutoSizeType.None;
            _observationFeedback.singleLine = false;
            _observationFeedback.stroke = 1;
            _observationFeedback.strokeColor = new Color(.08f, .11f, .11f);
            Required("worldHint").asTextField.autoSize = Fgui.AutoSizeType.None;
            _configurationTitle = Required("configurationTitle").asTextField;
            _conditions = Required("conditions").asTextField; _conditionsDetail = Required("conditionsDetail").asTextField;
            _conditionsDetail.autoSize = Fgui.AutoSizeType.None;
            _frozenHand = Required("frozenHand").asList;
            _frozenHand.itemRenderer = RenderFrozenHand; _frozenHand.SetVirtual();
            _frozenHand.numItems = _session.FrozenHand?.Count ?? 0;
            _momentum = Required("initialMomentum").asTextInput; _seed = Required("seed").asTextInput; _cost = Required("preparationCost").asTextInput;
            _player = Button("player", () => OpenPicker(TrialPickerMode.Player));
            _opponent = Button("opponent", () => OpenPicker(TrialPickerMode.OpponentSetup));
            Button("addHand", () => OpenPicker(TrialPickerMode.AddCard));
            _start = Button("start", RequestStart);
            _resume = Button("resumeTrial", () => { _session.ResumeTrial(); RefreshPage(); });
            _reset = Button("reset", () => _session.Start(true));
            Button("trialMenu", () => { _trialMenuOpen = !_trialMenuOpen; RefreshObservationVisibility(); }).tooltips = "试验操作 · F9 隐藏界面";
            Button("editConfiguration", () => { _conditionsDetailsVisible = false; _session.EditConfiguration(); RefreshPage(); });
            Button("showConditions", () => { _trialMenuOpen = false; _conditionsDetailsVisible = !_conditionsDetailsVisible; RefreshPage(); });
            Button("closeConditions", () => { _conditionsDetailsVisible = false; RefreshPage(); });
            Button("returnObservation", BackToEntry);
            Button("leaveTrial", () => _session.ReturnToObservation());
            _confirm = Button("confirm", () => _session.Hud?.TryConfirmExecution());
            _cancel = Button("cancel", () => _session.Hud?.TryCancelPending());
            Button("restartCancel", () => SetVisible(_restartControls, false));
            Button("restartConfirm", () => { if (ReadNumbers()) _session.Start(false); });
            _binding = true;
            _momentum.text = _session.Draft.InitialMomentum.ToString(CultureInfo.InvariantCulture);
            _seed.text = _session.Draft.Seed.ToString(CultureInfo.InvariantCulture);
            _cost.text = _session.Draft.PreparationCost.ToString(CultureInfo.InvariantCulture);
            _binding = false;
            _momentum.onChanged.Set(OnNumbersChanged); _seed.onChanged.Set(OnNumbersChanged); _cost.onChanged.Set(OnNumbersChanged);
            _configuration.itemRenderer = RenderConfiguration; _hand.itemRenderer = RenderHand;
            _hand.opaque = false;
            _configuration.SetVirtual(); _hand.SetVirtual();
            BindPicker();
            BindExperiment();
            RefreshConfiguration(); RefreshPage(); Tick();
        }

        private Fgui.GObject Required(string name) => _root.GetChild(name) ?? throw new InvalidOperationException("试验 Composer 控件缺失：" + name);
        private Fgui.GObject[] Controls(string[] names)
        {
            var controls = new Fgui.GObject[names.Length];
            for (int i = 0; i < names.Length; i++) controls[i] = Required(names[i]);
            return controls;
        }
        private static void SetVisible(Fgui.GObject[] controls, bool visible)
        { foreach (var item in controls) item.visible = visible; }
        private static void ValidateControls(Fgui.GComponent root, (string Name, string Kind)[] controls)
        {
            foreach (var control in controls)
            {
                var item = root.GetChild(control.Name);
                bool valid = control.Kind == "input" ? item is Fgui.GTextInput : control.Kind == "text" ? item is Fgui.GTextField
                    : control.Kind == "graph" ? item is Fgui.GGraph : control.Kind == "list" ? item is Fgui.GList
                    : control.Kind == "view" ? item is Fgui.GComponent : control.Kind == "image" ? item is Fgui.GImage : item is Fgui.GButton;
                if (!valid) throw new InvalidOperationException("试验 Composer 控件类型不匹配：" + control.Name);
            }
        }
        private Fgui.GButton Button(string name, Action action)
        {
            var button = Required(name).asButton ?? throw new InvalidOperationException("试验按钮类型错误：" + name);
            button.onClick.Set(context => { context.StopPropagation(); action(); }); return button;
        }
        private static Fgui.GButton RowButton(Fgui.GComponent row, string name, Action action)
        {
            var button = row.GetChild(name)?.asButton ?? throw new InvalidOperationException("试验配置行缺少按钮：" + name);
            button.onClick.Set(context => { context.StopPropagation(); action(); }); return button;
        }

        private void RenderHand(int index, Fgui.GObject item)
        {
            var hud = _session.Hud; var card = hud?.GetHandSlot(index); var root = item.asCom;
            root.visible = card != null;
            if (card == null) return;
            ValidateControls(root, CharacterTestAiLabCardTrialControlContract.HandCardControls);
            var face = root.GetChild("face").asCom;
            if (!_cardData.TryGetValue(card.Definition, out var data)) _cardData.Add(card.Definition, data = hud.GetBattleCardPresentationData(index));
            CharacterTestFairyGuiPlayerCardPresentation.Initialize(face);
            if (card.Definition.FixedAssembly != null) data.Cost = card.Definition.FixedAssembly.MainConfirmationCost
                + card.Definition.FixedAssembly.AccessoryConfirmationCost;
            CharacterTestFairyGuiPlayerCardPresentation.Bind(face, data, 34); _skin.Bind(face, data);
            if (card.Definition.FixedAssembly != null) _skin.SetPreviewQuality(face, (int)card.Definition.FixedAssembly.Quality - 1);
            BindHandAccessories(root, card.Definition.FixedAssembly);
            if (card.Definition.FixedAssembly != null) _skin.SetAccessoryGraphicLayout(face);
            face.SetScale(220f / 378f, 220f / 378f);
            root.opaque = true; root.alpha = card.Zone == CardZone.Hand && hud.CanSelectHandSlot(index) ? 1 : .65f;
            face.GetChild("focus").visible = card.Zone == CardZone.PendingAction;
            string fixedDetail = FixedCardDetail(card, out _);
            string notice = card.Zone == CardZone.PendingAction ? (hud.HandCardNeedsWorldPoint(index)
                ? "点击有效地面位置，再确认；也可取消待确认。" : "点击确认出牌，或取消待确认。")
                : hud.CanSelectHandSlot(index) ? "点击此牌选择，再点击确认出牌。" : hud.GetHandSlotUseNotice(index);
            root.onClick.Set(context => { context.StopPropagation(); hud.TrySelectHandSlot(index); });
            _tooltips.Bind(root, CharacterTestFairyGuiTacticalDescriptionHints.TooltipText(data) + "\n\n" + fixedDetail + "\n\n" + notice,
                new MxFairyGuiTooltipOptions(preferredPlacement: MxFairyGuiTooltipPlacement.Above, minimumTextWidth: 320, maximumTextWidth: 560, allowPin: false));
        }

        internal void Tick()
        {
            if (_disposed || _root == null || _root.isDisposed) return;
            _root.visible = _session.IsOpen && _visible;
            if (!_root.visible) { _tooltips.Hide(); return; }
            var stageRoot = Fgui.GRoot.inst;
            float scale = Mathf.Min(stageRoot.width / 1920f, stageRoot.height / 1080f);
            _root.SetScale(scale, scale); _root.SetXY((stageRoot.width - 1920 * scale) / 2, stageRoot.height - 1080 * scale);
            _skin.Tick();
            if (_renderedPage != _session.Page) RefreshPage();
            _reset.enabled = _session.CanReset;
            var hud = _session.Hud; var stage = _session.Runtime?.Stage;
            _confirm.enabled = hud?.CanConfirmExecution == true; _cancel.enabled = hud?.CanCancelPending == true;
            RefreshObservationVisibility();
            ulong revision = stage?.SnapshotRevision ?? 0;
            if (_handRevision != revision)
            { _handRevision = revision; _hand.numItems = _session.OwnsSession ? hud?.CardTrialSlotCount ?? 0 : 0; _hand.RefreshVirtualList(); }
            if (Time.unscaledTime < _nextObservation) return;
            _nextObservation = Time.unscaledTime + .1f;
            RefreshReadout();
        }
        internal void ToggleVisibility() { _visible = !_visible; Tick(); }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _navigator.ReleaseView(CharacterTestRuntimeAiLabCardTrialIds.ViewId);
            if (_ownsNavigator) _navigator.Dispose();
            DisposePlacementMarkers();
            _tooltips.Dispose(); _skin.Dispose(); _characterImages.Dispose(); _cardData.Clear(); _previewFaces.Clear(); _fixedFeeViews.Clear(); _fixedFeeViewStage = null; _root = null;
        }
    }
}
