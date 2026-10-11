using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(103)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Main Menu Host")]
    public sealed class CharacterTestFairyGuiMainMenuHost : MonoBehaviour
    {
        private GameSlice _slice;
        private ICharacterTestMainMenuController _controller;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiMainMenuCommandSink _commandSink;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _canOpenSettingsPanel;
        private bool _canOpenAbilitiesPanel;
        private bool _canOpenBehaviorDeckPanel;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _openRequested;
        private bool _reportedOpenFailure;
        private readonly CharacterTestFairyGuiMainMenuViewModel _viewModelBuffer = new();
        private readonly CharacterTestMainMenuStatus _statusBuffer = new();
        private CharacterTestFairyGuiMainMenuViewModelSignature _lastViewModelSignature;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;
        private CharacterTestFairyGuiMainMenuBinder _mainMenuBinder;
        private CharacterTestTacticalHomeWater _homeWater;
        private GameManager _waterOwner;
        private bool _waterFailed;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiMainMenuIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiMainMenuViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiMainMenuCommandSink CommandSink => _commandSink;
        public CharacterTestMainMenuCommandResult LastCommandResult =>
            _commandSink != null
                ? _commandSink.LastResult
                : CharacterTestMainMenuCommandResult.Failed("Main menu command sink unavailable.");

        public void Configure(
            GameSlice slice,
            ICharacterTestMainMenuController controller,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            bool autoOpen = false,
            IMxFairyGuiInputContextBridge inputBridge = null,
            Action toggleSettingsPanel = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Action toggleAbilitiesPanel = null,
            MxFairyGuiRuntimeShell runtimeShell = null,
            Action toggleBehaviorDeckPanel = null)
        {
            Release();
            _slice = slice;
            _controller = controller;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _openRequested = autoOpen;
            _canOpenSettingsPanel = toggleSettingsPanel != null;
            _canOpenAbilitiesPanel = toggleAbilitiesPanel != null;
            _canOpenBehaviorDeckPanel = toggleBehaviorDeckPanel != null;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Main menu disabled because resource runtime is not available.");
                return;
            }

            if (_controller == null)
            {
                Warn("Main menu disabled because controller is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiMainMenuCommandSink(
                _controller,
                toggleSettingsPanel,
                toggleAbilitiesPanel,
                toggleBehaviorDeckPanel);
            MxFairyGuiRuntimeCatalog catalog = CreateCatalog(_commandSink, _textProvider);
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics =
                catalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                LogFirstDiagnostic(diagnostics);
                return;
            }

            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _ownsLayerHost = layerHost == null;
            ConfigureFairyGuiCameraStack();
            _navigator = runtimeShell != null
                ? runtimeShell.Register(catalog)
                : MxFairyGuiRuntimeShellComposition.CreateNavigator(
                    _slice.Resources.ResourceManager,
                    catalog,
                    host: fairyGuiHost,
                    layerHost: _layerHost,
                    inputBridge: inputBridge);
            _configured = true;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            if (_openRequested)
                Refresh();
        }

        public bool Open()
        {
            _openRequested = true;
            Refresh();
            return IsOpen;
        }

        public void Refresh()
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _controller == null)
                return;

            if (!_openRequested && !IsOpen)
                return;

            CharacterTestFairyGuiMainMenuViewModelBuilder.CaptureStatusInto(
                _statusBuffer,
                _controller,
                _textProvider);
            CharacterTestUiTextProviderSignature textSignature =
                CharacterTestUiTextProviderSignature.Create(_textProvider);
            CharacterTestFairyGuiMainMenuViewModelSignature signature =
                CharacterTestFairyGuiMainMenuViewModelSignature.Create(
                    _statusBuffer,
                    canStart: true,
                    canNetwork: true,
                    _canOpenAbilitiesPanel,
                    _canOpenBehaviorDeckPanel,
                    _canOpenSettingsPanel,
                    canRestart: true,
                    canClose: true,
                    textSignature);
            if (IsOpen &&
                _hasLastViewModelSignature &&
                _lastViewModelSignature == signature)
            {
                SkippedRefreshCount++;
                LastViewModel = _viewModelBuffer;
                return;
            }

            CharacterTestFairyGuiMainMenuViewModel viewModel =
                CharacterTestFairyGuiMainMenuViewModelBuilder.BuildInto(
                    _viewModelBuffer,
                    _statusBuffer,
                    controllerAvailable: true,
                    _canOpenSettingsPanel,
                    _canOpenAbilitiesPanel,
                    _canOpenBehaviorDeckPanel,
                    _textProvider);
            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiMainMenuIds.ViewId, viewModel);
            if (result.Success)
            {
                LastViewModel = viewModel;
                _lastViewModelSignature = signature;
                _hasLastViewModelSignature = true;
                SuccessfulRefreshCount++;
                _reportedOpenFailure = false;
                _lastStatusMessage = string.Empty;
                return;
            }

            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Main menu open failed: " + result.Message);
            }
        }

        public bool Close()
        {
            _mainMenuBinder?.ReleaseHomepageCursor();
            if(_homeWater!=null)_homeWater.Active=false;
            _openRequested = false;
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiMainMenuIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        internal bool TryDismissHomepageConfirmation()
            => _commandSink?.TryDismissHomepageConfirmation() == true;

        public void DetachSlice()
        {
            _slice = null;
        }

        public void RebindSlice(GameSlice slice, ICharacterTestMainMenuController controller = null)
        {
            _slice = slice;
            if (controller != null)
                _controller = controller;
            if (_configured && IsOpen)
            {
                Refresh();
            }
        }

        public void Release()
        {
            _homeWater?.Dispose();_homeWater=null;_waterOwner=null;_waterFailed=false;
            Close();
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
            _navigator = null;
            _slice = null;
            _controller = null;
            _commandSink = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _canOpenSettingsPanel = false;
            _canOpenAbilitiesPanel = false;
            _canOpenBehaviorDeckPanel = false;
            _configured = false;
            _openRequested = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            LastViewModel = null;
            RefreshAttemptCount = 0;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
        }

        private MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CharacterTestFairyGuiMainMenuManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "main-menu", "generated-manifest" };
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiMainMenuManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            _mainMenuBinder=new CharacterTestFairyGuiMainMenuBinder(commandSink,CharacterTestFairyGuiMainMenuIds.ViewId,textProvider);
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiMainMenuViewModel>(
                contract,
                package,
                _mainMenuBinder));
            return catalog;
        }

        private void LateUpdate()
        {
            bool home=IsOpen&&LastViewModel?.Status?.IsTacticalSlice==true
                &&(TacticalCard.TacticalPreparationState)LastViewModel.Status.TacticalPhase==TacticalCard.TacticalPreparationState.Home;
            if (home) _mainMenuBinder?.UpdateHomepageFeedback(Time.unscaledDeltaTime);
            if(home&&_homeWater==null&&!_waterFailed)
            {
                if(_waterOwner==null)_waterOwner=GetComponent<GameManager>();
                var configuration=_waterOwner?.SceneRendering;
                if(configuration?.Feature!=null&&configuration.TargetCamera!=null)
                {
                    try{_homeWater=new CharacterTestTacticalHomeWater(configuration.Feature,configuration.TargetCamera);}
                    catch(Exception error){_waterFailed=true;Warn(error.Message);}
                }
            }
            if(_homeWater!=null){_homeWater.Active=home;if(home)_homeWater.SetSurface(_mainMenuBinder?.HomeSurface);}
        }

        private void OnDisable(){_mainMenuBinder?.ReleaseHomepageCursor();_homeWater?.Dispose();_homeWater=null;}
        private void OnDestroy(){_mainMenuBinder?.ReleaseHomepageCursor();_homeWater?.Dispose();_homeWater=null;}

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Main menu FairyGUI catalog diagnostics failed.";
            _logger.Warning("CharacterTest.FairyGUI", message);
            _lastStatusMessage = message;
        }

        private void ConfigureFairyGuiCameraStack()
        {
            if (!Application.isPlaying)
                return;

            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
            SyncFairyGuiOverlayCameraOutput(baseCamera, overlayCamera);

            MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(
                baseCamera,
                overlayCamera,
                excludeOverlayLayersFromBaseCamera: true);
            if (!result.Success)
            {
                Warn(
                    "FairyGUI camera stack bind failed. code=" + result.Code + " message=" + result.Message);
            }
        }

        private void ClearOpenViewState()
        {
            _lastViewModelSignature = default;
            _hasLastViewModelSignature = false;
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", _lastStatusMessage);
        }

        private static void SyncFairyGuiOverlayCameraOutput(UnityCamera baseCamera, UnityCamera overlayCamera)
        {
            if (baseCamera == null || overlayCamera == null)
                return;

            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.targetTexture = baseCamera.targetTexture;
            overlayCamera.targetDisplay = baseCamera.targetDisplay;
            overlayCamera.rect = baseCamera.rect;
            overlayCamera.allowHDR = baseCamera.allowHDR;
            overlayCamera.allowMSAA = baseCamera.allowMSAA;
        }
    }
}
