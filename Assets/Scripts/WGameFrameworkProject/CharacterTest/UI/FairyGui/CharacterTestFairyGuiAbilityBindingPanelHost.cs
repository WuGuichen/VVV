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
    [DefaultExecutionOrder(106)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Ability Binding Panel Host")]
    public sealed class CharacterTestFairyGuiAbilityBindingPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private CharacterTestAbilityHotbarRuntime _hotbarRuntime;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiAbilityBindingPanelCommandSink _commandSink;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiAbilityBindingPanelIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiAbilityBindingPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiAbilityBindingPanelCommandSink CommandSink => _commandSink;

        public void Configure(
            GameSlice slice,
            CharacterTestAbilityHotbarRuntime hotbarRuntime,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Func<bool> closePanel = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _hotbarRuntime = hotbarRuntime;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Ability binding panel disabled because resource runtime is not available.");
                return;
            }

            if (_hotbarRuntime == null)
            {
                Warn("Ability binding panel disabled because hotbar runtime is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiAbilityBindingPanelCommandSink(
                _hotbarRuntime,
                closePanel ?? Close);
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
        }

        public void Refresh()
        {
            Refresh(openIfClosed: false);
        }

        public bool Open()
        {
            _commandSink?.ResetWorkingFromRuntime();
            return Refresh(openIfClosed: true);
        }

        public bool Toggle()
        {
            return IsOpen ? Close() : Open();
        }

        private bool Refresh(bool openIfClosed)
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _hotbarRuntime == null)
                return false;

            if (!openIfClosed && !IsOpen)
                return false;

            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel =
                CharacterTestFairyGuiAbilityBindingPanelViewModelBuilder.Build(
                    _hotbarRuntime,
                    _commandSink != null ? _commandSink.WorkingBindings : null,
                    _commandSink != null ? _commandSink.SelectedSlot : 1,
                    _commandSink != null && _commandSink.Dirty,
                    _commandSink != null ? _commandSink.LastStatusMessage : string.Empty,
                    _textProvider);
            string signature = CharacterTestFairyGuiAbilityBindingPanelViewModelSignature.Create(viewModel)
                + "|text="
                + CharacterTestUiText.Signature(_textProvider);
            if (IsOpen &&
                _hasLastViewModelSignature &&
                string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal))
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                return true;
            }

            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiAbilityBindingPanelIds.ViewId, viewModel);
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
                Warn("Ability binding panel open failed: " + result.Message);
            }

            return false;
        }

        public bool Close()
        {
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiAbilityBindingPanelIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        public void DetachSlice()
        {
            _slice = null;
        }

        public void RebindSlice(GameSlice slice, CharacterTestAbilityHotbarRuntime hotbarRuntime = null)
        {
            _slice = slice;
            if (hotbarRuntime != null)
                _hotbarRuntime = hotbarRuntime;
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
            _hotbarRuntime = null;
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
            MxUiViewContract contract = CharacterTestFairyGuiAbilityBindingPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.Commands = AddCancelCommand(contract.Commands);
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "ability-bindings", "generated-manifest" };
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiAbilityBindingPanelManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiAbilityBindingPanelViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiAbilityBindingPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
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
                : "Ability binding panel FairyGUI catalog diagnostics failed.";
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
