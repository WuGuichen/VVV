using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Runtime;
using MxFramework.Settings.UI;
using MxFramework.UI;
using MxFramework.UI.Audio;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(105)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Settings Panel Host")]
    public sealed class CharacterTestFairyGuiSettingsPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private CharacterTestSettingsRuntime _settingsRuntime;
        private CharacterTestLocalizationRuntime _localizationRuntime;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiSettingsPanelCommandSink _commandSink;
        private MxUiFeedbackCommandSink _feedbackCommandSink;
        private IMxUiCommandSink _commandRoute;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private string _lastStatusMessage = string.Empty;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiSettingsPanelIds.ViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiSettingsPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiSettingsPanelCommandSink CommandSink => _commandSink;

        public void Configure(
            GameSlice slice,
            CharacterTestSettingsRuntime settingsRuntime,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null)
        {
            Configure(slice, settingsRuntime, null, null, logger, layerHost);
        }

        public void Configure(
            GameSlice slice,
            CharacterTestSettingsRuntime settingsRuntime,
            CharacterTestLocalizationRuntime localizationRuntime = null,
            IMxUiTextProvider textProvider = null,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Func<bool> closePanel = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _settingsRuntime = settingsRuntime;
            _localizationRuntime = localizationRuntime;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            _logger = logger ?? NullRuntimeLogger.Instance;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Settings panel disabled because resource runtime is not available.");
                return;
            }

            if (_settingsRuntime == null)
            {
                Warn("Settings panel disabled because settings runtime is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiSettingsPanelCommandSink(
                _settingsRuntime,
                closePanel ?? Close,
                _localizationRuntime);
            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _ownsLayerHost = layerHost == null;

            MxUiViewContract contract = CreateViewContract();
            var feedbackPlayer = new MxAudioUiFeedbackPlayer(
                _slice.Services.AudioService,
                _slice.Services.AudioCueCatalog);
            _feedbackCommandSink = new MxUiFeedbackCommandSink(
                _commandSink,
                CreateFeedbackPolicyRegistry(contract),
                feedbackPlayer);
            _commandRoute = new MxFairyGuiModalCommandGate(
                _feedbackCommandSink,
                _layerHost);

            MxFairyGuiRuntimeCatalog catalog = CreateCatalog(
                _commandRoute,
                _textProvider,
                contract);
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics =
                catalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                LogFirstDiagnostic(diagnostics);
                ReleaseFeedbackRoute();
                ReleaseOwnedLayerHost();
                _commandSink = null;
                return;
            }

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
            if (!IsOpen && _settingsRuntime != null)
            {
                MxSettingsUiOperationResult begin =
                    _settingsRuntime.SettingsUi.BeginInputEditSession();
                if (!begin.Success)
                {
                    Warn("Settings panel input draft could not start.");
                    return false;
                }
            }

            bool opened = Refresh(openIfClosed: true);
            if (!opened && _settingsRuntime?.SettingsUi.InputEditSessionActive == true)
                _settingsRuntime.SettingsUi.RollbackInputEditSession();
            return opened;
        }

        public bool Toggle()
        {
            return IsOpen ? Close() : Open();
        }

        private bool Refresh(bool openIfClosed)
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _settingsRuntime == null)
                return false;

            if (!openIfClosed && !IsOpen)
                return false;

            CharacterTestFairyGuiSettingsPanelViewModel viewModel =
                CharacterTestFairyGuiSettingsPanelViewModelBuilder.Build(
                    _settingsRuntime,
                    _commandSink != null
                        ? _commandSink.SelectedPage
                        : CharacterTestSettingsPage.Controls,
                    _commandSink != null
                        ? _commandSink.SelectedInputDevice
                        : CharacterTestSettingsInputDevicePage.KeyboardMouse,
                    _commandSink != null
                        ? _commandSink.ActiveRebindTarget
                        : default,
                    _commandSink != null ? _commandSink.LastStatusMessage : string.Empty,
                    _textProvider);
            string signature = CharacterTestFairyGuiSettingsPanelViewModelSignature.Create(viewModel)
                + "|text="
                + CharacterTestUiText.Signature(_textProvider)
                + "|stage="
                + CreateStageSizeSignature();
            if (IsOpen &&
                _hasLastViewModelSignature &&
                string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal))
            {
                SkippedRefreshCount++;
                LastViewModel = viewModel;
                return true;
            }

            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiSettingsPanelIds.ViewId, viewModel);
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
                Warn("Settings panel open failed: " + result.Message);
            }

            return false;
        }

        public bool Close()
        {
            if (_settingsRuntime?.SettingsUi.InputEditSessionActive == true)
            {
                _settingsRuntime.SettingsUi.RollbackInputEditSession();
                _settingsRuntime.ReloadProfile();
                _localizationRuntime?.SyncFromSettings(_settingsRuntime);
            }

            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiSettingsPanelIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        internal void EnqueueRoutedCommand(MxUiCommand command)
        {
            _commandRoute?.Enqueue(command);
        }

        public void DetachSlice()
        {
            _slice = null;
        }

        public void RebindSlice(GameSlice slice)
        {
            _slice = slice;
            if (_configured && IsOpen)
            {
                Refresh();
            }
        }

        public void Release()
        {
            Close();
            ReleaseFeedbackRoute();
            ReleaseOwnedLayerHost();
            _navigator = null;
            _slice = null;
            _settingsRuntime = null;
            _localizationRuntime = null;
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
            IMxUiTextProvider textProvider,
            MxUiViewContract contract)
        {
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiSettingsPanelManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiSettingsPanelViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiSettingsPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiSettingsPanelIds.ViewId,
                    textProvider)));
            return catalog;
        }

        private static MxUiViewContract CreateViewContract()
        {
            MxUiViewContract contract =
                CharacterTestFairyGuiSettingsPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.Commands = AddCancelCommand(contract.Commands);
            contract.DiagnosticsTags = new[]
            {
                "character-test",
                "fairygui",
                "settings-panel",
                "generated-manifest"
            };
            return contract;
        }

        private static MxUiFeedbackPolicyRegistry CreateFeedbackPolicyRegistry(
            MxUiViewContract contract)
        {
            IReadOnlyList<MxUiCommandDescriptor> commands =
                contract?.Commands ?? Array.Empty<MxUiCommandDescriptor>();
            var bindings = new MxUiFeedbackPolicyBinding[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                bindings[i] = new MxUiFeedbackPolicyBinding(
                    contract.Descriptor.Id,
                    commands[i].CommandId,
                    commands[i].Feedback);
            }

            return new MxUiFeedbackPolicyRegistry(bindings);
        }

        private static IReadOnlyList<MxUiCommandDescriptor> AddCancelCommand(
            IReadOnlyList<MxUiCommandDescriptor> generatedCommands)
        {
            IReadOnlyList<MxUiCommandDescriptor> generated =
                generatedCommands ?? Array.Empty<MxUiCommandDescriptor>();
            var commands = new List<MxUiCommandDescriptor>(generated.Count + 2)
            {
                Command(CharacterTestFairyGuiSettingsPanelCommandSink.VolumeDraftCommand,
                    typeof(CharacterTestSettingsVolumeDraft),
                    new MxUiFeedbackPolicy(MxUiFeedbackKind.None, MxUiFeedbackKind.None, MxUiFeedbackKind.None)),
                Command(
                    MxFairyGuiCommandIds.Cancel,
                    null,
                    new MxUiFeedbackPolicy(
                        MxUiFeedbackKind.Back,
                        MxUiFeedbackKind.None,
                        MxUiFeedbackKind.None))
            };
            for (int i = 0; i < generated.Count; i++)
                commands.Add(generated[i]);

            return commands;
        }

        private static MxUiCommandDescriptor Command(
            string commandId,
            Type payloadType,
            MxUiFeedbackPolicy feedback)
        {
            return new MxUiCommandDescriptor
            {
                CommandId = commandId,
                PayloadType = payloadType != null ? payloadType.FullName : string.Empty,
                Owner = "CharacterTest",
                Feedback = feedback
            };
        }

        private void ReleaseFeedbackRoute()
        {
            _commandRoute = null;
            _feedbackCommandSink?.Dispose();
            _feedbackCommandSink = null;
        }

        private void ReleaseOwnedLayerHost()
        {
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
        }

        private static string CreateStageSizeSignature()
        {
            int width = Mathf.RoundToInt(MxFairyGuiStageLayout.ResolveStageWidth(null, MxFairyGuiStageLayout.DefaultDesignWidth));
            int height = Mathf.RoundToInt(MxFairyGuiStageLayout.ResolveStageHeight(null, MxFairyGuiStageLayout.DefaultDesignHeight));

            return width.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "x"
                + height.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Settings panel FairyGUI catalog diagnostics failed.";
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
