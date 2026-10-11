using System;
using System.Collections.Generic;
using MxFramework.Camera.URP;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(105)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Story Dialog Host")]
    public sealed class CharacterTestFairyGuiStoryDialogHost : MonoBehaviour
    {
        private GameSlice _slice;
        private CharacterTestStoryContent _storyContent;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiStoryCommandSink _commandSink;
        private Func<bool> _suppressOpenPredicate;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private StoryDirectorSnapshot _lastStorySnapshot;
        private CharacterTestUiTextProviderSignature _lastTextProviderSignature;
        private StoryDirectorSnapshot _lastIdleStorySnapshot;
        private CharacterTestUiTextProviderSignature _lastIdleTextProviderSignature;
        private bool _hasLastIdleStorySnapshot;
        private string _lastStatusMessage = string.Empty;

        public bool IsConfigured => _configured;
        public bool IsOpen =>
            _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiStoryDialogIds.ViewId);
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public MxUiCommand LastCommand => _commandSink != null ? _commandSink.LastCommand : default;
        public RuntimeCommand LastRuntimeCommand => _commandSink != null ? _commandSink.LastRuntimeCommand : default;
        public RuntimeCommandValidationResult LastCommandResult =>
            _commandSink != null ? _commandSink.LastResult : default;
        public int AcceptedCommandCount => _commandSink != null ? _commandSink.AcceptedCount : 0;
        public int RejectedCommandCount => _commandSink != null ? _commandSink.RejectedCount : 0;
        public int DuplicateCommandCount => _commandSink != null ? _commandSink.DuplicateIgnoredCount : 0;
        public string LastStatusMessage => _lastStatusMessage;

        public void Configure(
            GameSlice slice,
            CharacterTestStoryContent storyContent = null,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            Func<bool> suppressOpenPredicate = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiHost fairyGuiHost = null,
            MxFairyGuiRuntimeShell runtimeShell = null)
        {
            Release();
            _slice = slice;
            _storyContent = storyContent;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _suppressOpenPredicate = suppressOpenPredicate;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Story dialog disabled because resource runtime is not available.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiStoryCommandSink(_slice);
            MxFairyGuiRuntimeCatalog catalog = CreateCatalog(_commandSink, _textProvider);
            MxFairyGuiRuntimeCatalogDiagnostics diagnostics =
                catalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            if (!diagnostics.Success)
            {
                LogFirstDiagnostic(diagnostics);
                _commandSink = null;
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
            Refresh();
        }

        public void Refresh()
        {
            if (!_configured || _navigator == null || _slice == null)
                return;

            if (_suppressOpenPredicate != null && _suppressOpenPredicate())
            {
                Close();
                return;
            }

            StoryDirectorSnapshot storySnapshot = _slice.StorySnapshot;
            CharacterTestUiTextProviderSignature textProviderSignature =
                CharacterTestUiTextProviderSignature.Create(_textProvider);
            bool isOpen = IsOpen;
            if (!isOpen &&
                _hasLastIdleStorySnapshot &&
                ReferenceEquals(_lastIdleStorySnapshot, storySnapshot) &&
                _lastIdleTextProviderSignature == textProviderSignature)
            {
                SkippedRefreshCount++;
                return;
            }

            if (storySnapshot != null &&
                isOpen &&
                _hasLastViewModelSignature &&
                ReferenceEquals(_lastStorySnapshot, storySnapshot) &&
                _lastTextProviderSignature == textProviderSignature)
            {
                SkippedRefreshCount++;
                return;
            }

            CharacterTestFairyGuiStoryDialogViewModel viewModel;
            if (!CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                    storySnapshot,
                    _slice.StoryDirector,
                    _storyContent,
                    out viewModel,
                    _textProvider))
            {
                if (isOpen)
                    Close();
                else
                    ClearOpenViewState();
                RecordIdleViewState(storySnapshot, textProviderSignature);
                return;
            }

            ClearIdleViewState();
            string signature = CharacterTestFairyGuiStoryDialogViewModelSignature.Create(viewModel)
                + "|text="
                + CharacterTestUiText.Signature(_textProvider);
            if (isOpen &&
                _hasLastViewModelSignature &&
                string.Equals(_lastViewModelSignature, signature, StringComparison.Ordinal))
            {
                SkippedRefreshCount++;
                return;
            }

            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiStoryDialogIds.ViewId, viewModel);
            if (result.Success)
            {
                _lastViewModelSignature = signature;
                _hasLastViewModelSignature = true;
                _lastStorySnapshot = storySnapshot;
                _lastTextProviderSignature = textProviderSignature;
                _commandSink?.SetActiveViewModelSignature(signature);
                SuccessfulRefreshCount++;
                _reportedOpenFailure = false;
                _lastStatusMessage = string.Empty;
                return;
            }

            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Story dialog open failed: " + result.Message);
            }
        }

        public bool Close()
        {
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiStoryDialogIds.ViewId);
            ClearOpenViewState();
            return closed;
        }

        public void DetachSlice()
        {
            _slice = null;
            _commandSink?.UpdateSlice(null);
            ClearIdleViewState();
        }

        public void RebindSlice(GameSlice slice, CharacterTestStoryContent storyContent = null)
        {
            _slice = slice;
            if (storyContent != null)
                _storyContent = storyContent;
            _commandSink?.UpdateSlice(slice);
            ClearIdleViewState();
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
            _commandSink = null;
            _suppressOpenPredicate = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _slice = null;
            _storyContent = null;
            _configured = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
            ClearIdleViewState();
        }

        private static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CharacterTestFairyGuiStoryDialogManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "story", "generated-manifest" };
            MxFairyGuiPackageDescriptor package = CharacterTestFairyGuiStoryDialogManifest.CreatePackageDescriptor();

            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiStoryDialogViewModel>(
                contract,
                package,
                new CharacterTestFairyGuiStoryDialogBinder(
                    commandSink,
                    CharacterTestFairyGuiStoryDialogIds.ViewId,
                    textProvider)));
            return catalog;
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            string message = issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Story dialog FairyGUI catalog diagnostics failed.";
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
            _lastStorySnapshot = null;
            _lastTextProviderSignature = default;
            _commandSink?.SetActiveViewModelSignature(string.Empty);
        }

        private void RecordIdleViewState(
            StoryDirectorSnapshot storySnapshot,
            CharacterTestUiTextProviderSignature textProviderSignature)
        {
            _lastIdleStorySnapshot = storySnapshot;
            _lastIdleTextProviderSignature = textProviderSignature;
            _hasLastIdleStorySnapshot = true;
        }

        private void ClearIdleViewState()
        {
            _lastIdleStorySnapshot = null;
            _lastIdleTextProviderSignature = default;
            _hasLastIdleStorySnapshot = false;
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
