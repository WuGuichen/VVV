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
    [DefaultExecutionOrder(108)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Arena Setup Panel Host")]
    public sealed class CharacterTestFairyGuiArenaSetupPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private CharacterTestArenaSessionController _controller;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiArenaSetupPanelCommandSink _commandSink;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiArenaSetupPanelIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiArenaSetupPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiArenaSetupPanelCommandSink CommandSink => _commandSink;

        public void Configure(
            GameSlice slice,
            CharacterTestArenaSessionController controller,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            bool autoOpen = false,
            Func<int, int, bool> openBehaviorGroupPanel = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _controller = controller;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Arena setup panel disabled because resource runtime is not available.");
                return;
            }

            if (_controller == null)
            {
                Warn("Arena setup panel disabled because arena controller is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiArenaSetupPanelCommandSink(
                _controller,
                () =>
                {
                    Refresh();
                    return true;
                },
                openBehaviorGroupPanel);
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
            if (autoOpen)
                Open();
        }

        public void Refresh()
        {
            Refresh(openIfClosed: false);
        }

        public bool Open()
        {
            return Refresh(openIfClosed: true);
        }

        public bool Toggle()
        {
            return IsOpen ? Close() : Open();
        }

        private bool Refresh(bool openIfClosed)
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _controller == null)
                return false;

            if (!openIfClosed && !IsOpen)
                return false;

            CharacterTestFairyGuiArenaSetupPanelViewModel viewModel =
                CharacterTestFairyGuiArenaSetupPanelViewModelBuilder.Build(
                    _controller,
                    _textProvider);
            string signature = CharacterTestFairyGuiArenaSetupPanelViewModelSignature.Create(viewModel)
                + "|status="
                + (_commandSink != null ? _commandSink.LastStatusMessage : string.Empty)
                + "|text="
                + CharacterTestUiText.Signature(_textProvider);
            if (IsOpen
                && _hasLastViewModelSignature
                && string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal))
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                return true;
            }

            MxUiOpenResult result =
                _navigator.Open(CharacterTestFairyGuiArenaSetupPanelIds.ViewId, viewModel);
            if (result.Success)
            {
                LastViewModel = viewModel;
                _lastViewModelSignature = signature;
                _hasLastViewModelSignature = true;
                SuccessfulRefreshCount++;
                _reportedOpenFailure = false;
                _lastStatusMessage = _commandSink != null ? _commandSink.LastStatusMessage : string.Empty;
                return true;
            }

            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Arena setup panel open failed: " + result.Message);
            }

            return false;
        }

        public bool Close()
        {
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiArenaSetupPanelIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        public void DetachSlice()
        {
            _slice = null;
        }

        public void RebindSlice(GameSlice slice, CharacterTestArenaSessionController controller = null)
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
            _configured = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            LastViewModel = null;
            RefreshAttemptCount = 0;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
        }

        private static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CreateRuntimeViewContract();
            MxFairyGuiPackageDescriptor package =
                CharacterTestFairyGuiArenaSetupPanelManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiArenaSetupPanelViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiArenaSetupPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiArenaSetupPanelIds.ViewId,
                    textProvider)));
            return catalog;
        }

        internal static MxUiViewContract CreateRuntimeViewContract()
        {
            MxUiViewContract contract = CharacterTestFairyGuiArenaSetupPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.Commands = AddCancelCommand(contract.Commands);
            contract.DiagnosticsTags = new[]
            {
                "character-test",
                "fairygui",
                "arena",
                "generated-manifest"
            };
            return contract;
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
                : "Arena setup panel FairyGUI catalog diagnostics failed.";
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
