using System;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiTacticalDeckPanelIds
    {
        public const string ViewIdValue = "ui.character_test.tactical_deck";
        public const string PackageId = "MxCharacterTestTacticalDeckUi";
        public const string ComponentName = "TacticalDeckPanel";
        public const string PackageBytesResourceId = "ui.fairygui.charactertest.tactical_deck_ui.fui";
        public const string Atlas0ResourceId = "ui.fairygui.charactertest.tactical_deck_ui.atlas0";
        internal const string Atlas0Page1ResourceId = "ui.fairygui.charactertest.tactical_deck_ui.atlas0_1";
        internal const string Atlas0Page2ResourceId = "ui.fairygui.charactertest.tactical_deck_ui.atlas0_2";
        internal const string Atlas0Page3ResourceId = "ui.fairygui.charactertest.tactical_deck_ui.atlas0_3";
        public static MxUiViewId ViewId => new MxUiViewId(ViewIdValue);
        public static ResourceKey PackageBytesKey => new ResourceKey(
            PackageBytesResourceId,
            MxFairyGuiResourceTypeIds.PackageBytes,
            packageId: PackageId);
        public static ResourceKey Atlas0Key => new ResourceKey(
            Atlas0ResourceId,
            MxFairyGuiResourceTypeIds.PackageAtlas,
            packageId: PackageId);
        internal static ResourceKey Atlas0Page2Key => new ResourceKey(
            Atlas0Page2ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);
        internal static ResourceKey Atlas0Page3Key => new ResourceKey(
            Atlas0Page3ResourceId, MxFairyGuiResourceTypeIds.PackageAtlas, packageId: PackageId);
        internal static ResourceKey Atlas0Page1Key => new ResourceKey(
            Atlas0Page1ResourceId,
            MxFairyGuiResourceTypeIds.PackageAtlas,
            packageId: PackageId);
    }

    public sealed class CharacterTestFairyGuiTacticalDeckPanelViewModel
    {
        public object DeckEntries => null;
        public string SearchText => string.Empty;
        public object PublishedCards => null;
        public string EmptyHint => "从右侧添加行为卡";
        public string Toast => string.Empty;
    }

    internal sealed class CharacterTestFairyGuiTacticalDeckPanelBinder
        : IMxFairyGuiViewBinder<CharacterTestFairyGuiTacticalDeckPanelViewModel>, IDisposable
    {
        private readonly CharacterTestFairyGuiTacticalPreparationView _view =
            new CharacterTestFairyGuiTacticalPreparationView();
        private readonly Func<TacticalCard.TacticalBattlePreparationController> _resolveController;
        private Fgui.GComponent _root;

        internal CharacterTestFairyGuiTacticalDeckPanelBinder(Func<TacticalCard.TacticalBattlePreparationController> resolveController)
        {
            _resolveController = resolveController;
        }

        public void Bind(
            IMxFairyGuiComponentHandle component,
            CharacterTestFairyGuiTacticalDeckPanelViewModel viewModel)
        {
            _root = (component as MxFairyGuiComponentHandle)?.Component
                ?? throw new InvalidOperationException("Tactical deck binder requires a concrete FairyGUI component handle.");
            // Remove the stale generic control when loading an older published package.
            // Return and exit commands belong to the existing flow menu.
            _root.GetChild("btnClose")?.Dispose();
            Tick();
        }

        internal void Tick()
        {
            TacticalCard.TacticalBattlePreparationController controller = _resolveController?.Invoke();
            if (_root != null && controller != null && TacticalCard.CharacterTacticalServices.Player != null
                && TacticalCard.CharacterTacticalServices.Preparation != null)
                _view.Bind(_root, controller, TacticalCard.CharacterTacticalServices.Preparation,
                    TacticalCard.CharacterTacticalServices.Player, TacticalCard.CharacterTacticalServices.InventoryOwner, TacticalCard.CharacterTacticalServices.RoleBindings);
        }

        public void Dispose()
        {
            _view.Dispose();
            _root = null;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(107)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Tactical Deck Panel Host")]
    public sealed class CharacterTestFairyGuiTacticalDeckPanelHost : MonoBehaviour
    {
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiTacticalDeckPanelBinder _binder;
        private CharacterTestFairyGuiTacticalBattleBinder _battleBinder;
        private static readonly MxUiViewId BattleViewId = new MxUiViewId("ui.character_test.tactical_battle");
        private bool _ownsLayerHost;
        private bool _configured;

        public bool IsConfigured => _configured;
        public bool IsOpen => _navigator != null
            && _navigator.IsOpen(CharacterTestFairyGuiTacticalDeckPanelIds.ViewId);
        public string LastStatusMessage { get; private set; } = string.Empty;

        private async void Start()
        {
            try { await TacticalCard.CharacterTacticalServices.OpenAsync(); }
            catch (Exception error) { Warn(error.Message); }
        }

        private void LateUpdate()
        {
            if (IsOpen) _binder?.Tick();
        }

        internal void Configure(
            GameSlice slice,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _logger = logger ?? NullRuntimeLogger.Instance;
            if (slice?.Resources?.ResourceManager == null)
            {
                Warn("Tactical deck panel disabled because runtime resources are unavailable.");
                return;
            }

            _binder = new CharacterTestFairyGuiTacticalDeckPanelBinder(
                () => GetComponent<TacticalCard.TacticalBattlePreparationController>());
            MxUiViewContract contract = CharacterTestFairyGuiTacticalDeckPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiTacticalDeckPanelViewModel>(
                contract,
                CharacterTestFairyGuiTacticalDeckPanelManifest.CreatePackageDescriptor(),
                _binder));
            // Both views share the framework host, so closing preparation cannot release battle atlases.
            _battleBinder = new CharacterTestFairyGuiTacticalBattleBinder(() => GetComponent<TacticalCardHud>());
            var battleContract = new MxUiViewContract(new MxUiViewDescriptor(BattleViewId,
                CharacterTestFairyGuiTacticalDeckPanelIds.PackageId, "TacticalBattleHud", MxUiLayer.Hud)
                { CloseOnSceneChange = true, Modal = false });
            battleContract.ViewModelType = typeof(CharacterTestFairyGuiTacticalDeckPanelViewModel).FullName;
            battleContract.RequiredResources = contract.RequiredResources;
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiTacticalDeckPanelViewModel>(
                battleContract, CharacterTestFairyGuiTacticalDeckPanelManifest.CreatePackageDescriptor(), _battleBinder));
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics = catalog.CreateDiagnostics(slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                Warn(diagnostics.Issues.Count > 0 ? diagnostics.Issues[0].Message : "Tactical deck catalog validation failed.");
                _binder.Dispose();
                _binder = null;
                return;
            }

            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _ownsLayerHost = layerHost == null;
            _navigator = runtimeShell != null
                ? runtimeShell.Register(catalog)
                : MxFairyGuiRuntimeShellComposition.CreateNavigator(
                    slice.Resources.ResourceManager,
                    catalog,
                    host: fairyGuiHost,
                    layerHost: _layerHost,
                    inputBridge: inputBridge);
            _configured = true;
            LastStatusMessage = string.Empty;
        }

        internal CharacterTestFairyGuiTacticalBattleView OpenBattleView()
        {
            if (!_configured || _navigator == null) return null;
            if (_navigator.IsOpen(BattleViewId)) return _battleBinder?.View;
            MxUiOpenResult result = _navigator.Open(BattleViewId, new CharacterTestFairyGuiTacticalDeckPanelViewModel());
            if (!result.Success) { Warn("Battle HUD open failed: " + result.Message); return null; }
            return _battleBinder?.View;
        }

        public bool Open()
        {
            if (!_configured || _navigator == null || TacticalCard.CharacterTacticalServices.Player == null)
                return false;
            MxUiOpenResult result = _navigator.Open(
                CharacterTestFairyGuiTacticalDeckPanelIds.ViewId,
                new CharacterTestFairyGuiTacticalDeckPanelViewModel());
            if (result.Success) return true;
            Warn("Tactical deck panel open failed: " + result.Message);
            return false;
        }

        public bool Close()
        {
            bool closed = _navigator != null
                && _navigator.Close(CharacterTestFairyGuiTacticalDeckPanelIds.ViewId);
            _binder?.Dispose();
            return closed;
        }

        public void Release()
        {
            if (_navigator != null)
            {
                _navigator.ReleaseView(BattleViewId);
                _navigator.ReleaseView(CharacterTestFairyGuiTacticalDeckPanelIds.ViewId);
            }
            _battleBinder?.Dispose();
            _battleBinder = null;
            _binder?.Dispose();
            if (_ownsLayerHost) _layerHost?.Dispose();
            _binder = null;
            _navigator = null;
            _layerHost = null;
            _ownsLayerHost = false;
            _configured = false;
        }

        private void OnDestroy() => Release();

        private void Warn(string message)
        {
            LastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", LastStatusMessage);
        }
    }
}
