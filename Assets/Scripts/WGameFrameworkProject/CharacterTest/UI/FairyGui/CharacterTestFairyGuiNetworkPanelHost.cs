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
    [DefaultExecutionOrder(104)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Network Panel Host")]
    public sealed class CharacterTestFairyGuiNetworkPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private ICharacterTestNetworkSessionController _controller;
        private ICharacterTestNetworkLobbyController _lobbyController;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiNetworkPanelCommandSink _commandSink;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private Action _closeRequested;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _autoOpen = true;
        private bool _openRequested;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;
        private CharacterTestNetworkLaunchMode _selectedLaunchMode = CharacterTestNetworkLaunchMode.Room;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiNetworkPanelIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiNetworkPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiNetworkPanelCommandSink CommandSink => _commandSink;
        public CharacterTestNetworkSessionResult LastCommandResult =>
            _commandSink != null
                ? _commandSink.LastResult
                : CharacterTestNetworkSessionResult.Failed(
                    CharacterTestNetworkSessionKind.Offline,
                    CharacterTestNetworkMode.Disabled,
                    "Network panel command sink unavailable.");

        public void Configure(
            GameSlice slice,
            ICharacterTestNetworkSessionController controller,
            ICharacterTestNetworkLobbyController lobbyController = null,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            bool autoOpen = true,
            Action closeRequested = null,
            Action sessionStarted = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost fairyGuiHost = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _controller = controller;
            _lobbyController = lobbyController ?? controller as ICharacterTestNetworkLobbyController;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _autoOpen = autoOpen;
            _openRequested = autoOpen;
            _closeRequested = closeRequested;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Network panel disabled because resource runtime is not available.");
                return;
            }

            if (_controller == null)
            {
                Warn("Network panel disabled because session controller is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                _controller,
                _lobbyController,
                HandleCloseRequested,
                HandleLaunchModeChanged,
                persistPreferences: true);
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

            if (!_autoOpen && !_openRequested && !IsOpen)
                return;

            CharacterTestFairyGuiNetworkPanelViewModel viewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(
                    _controller,
                    _textProvider,
                    _selectedLaunchMode,
                    useSavedPreferences: true);
            string signature = CharacterTestFairyGuiNetworkPanelViewModelSignature.Create(viewModel)
                + "|text="
                + CharacterTestUiText.Signature(_textProvider);
            if (IsOpen &&
                _hasLastViewModelSignature &&
                string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal))
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                return;
            }

            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiNetworkPanelIds.ViewId, viewModel);
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
                Warn("Network panel open failed: " + result.Message);
            }
        }

        public bool Close()
        {
            _autoOpen = false;
            _openRequested = false;
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiNetworkPanelIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        public void DetachSlice()
        {
            _slice = null;
        }

        public void RebindSlice(
            GameSlice slice,
            ICharacterTestNetworkSessionController controller = null,
            ICharacterTestNetworkLobbyController lobbyController = null)
        {
            _slice = slice;
            if (controller != null)
                _controller = controller;
            if (lobbyController != null)
                _lobbyController = lobbyController;
            if (_configured && IsOpen)
            {
                Refresh();
            }
        }

        public void Release()
        {
            Close();
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
            _navigator = null;
            _slice = null;
            _controller = null;
            _lobbyController = null;
            _commandSink = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _closeRequested = null;
            _configured = false;
            _autoOpen = true;
            _openRequested = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            _selectedLaunchMode = CharacterTestNetworkLaunchMode.Room;
            LastViewModel = null;
            RefreshAttemptCount = 0;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
        }

        private void HandleLaunchModeChanged(CharacterTestNetworkLaunchMode launchMode)
        {
            if (_selectedLaunchMode == launchMode)
                return;

            _selectedLaunchMode = launchMode;
            _hasLastViewModelSignature = false;
            Refresh();
        }

        private static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CharacterTestFairyGuiNetworkPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.Commands = AddCancelCommand(contract.Commands);
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "network-panel", "generated-manifest" };
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiNetworkPanelManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiNetworkPanelViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiNetworkPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    textProvider)));
            return catalog;
        }

        private static IReadOnlyList<MxUiCommandDescriptor> AddCancelCommand(
            IReadOnlyList<MxUiCommandDescriptor> generatedCommands)
        {
            IReadOnlyList<MxUiCommandDescriptor> generated =
                generatedCommands ?? Array.Empty<MxUiCommandDescriptor>();
            var commands = new List<MxUiCommandDescriptor>(generated.Count + 1)
            {
                Command(MxFairyGuiCommandIds.Cancel, null)
            };
            for (int i = 0; i < generated.Count; i++)
                commands.Add(generated[i]);

            return commands;
        }

        private static MxUiCommandDescriptor Command(string commandId, Type payloadType)
        {
            return new MxUiCommandDescriptor
            {
                CommandId = commandId,
                PayloadType = payloadType != null ? payloadType.FullName : string.Empty,
                Owner = "CharacterTest"
            };
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Network panel FairyGUI catalog diagnostics failed.";
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
            _lastViewModelSignature = string.Empty;
            _hasLastViewModelSignature = false;
        }

        private void Warn(string message)
        {
            _lastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", _lastStatusMessage);
        }

        private void HandleCloseRequested()
        {
            if (_closeRequested != null)
            {
                _closeRequested.Invoke();
                return;
            }

            Close();
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
