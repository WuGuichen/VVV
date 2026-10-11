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
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Behavior Deck Panel Host")]
    public sealed class CharacterTestFairyGuiBehaviorDeckPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private CharacterTestFairyGuiBehaviorDeckPanelCommandSink _commandSink;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private bool _ownsLayerHost;
        private bool _configured;
        private bool _reportedOpenFailure;
        private string _lastSignature = string.Empty;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView _lastSourceView;
        private int _lastPageIndex = -1;
        private string _lastReplacementSourceStableCardId = string.Empty;
        private string _lastDetailsStableCardId = string.Empty;
        private int _lastDetailsPageIndex = -1;
        private string _lastCommandStatus = string.Empty;
        private string _lastTextLocale = string.Empty;
        private long _lastTextRevision = long.MinValue;
        private long _lastFeedbackVersion = long.MinValue;
        private long _lastFocusRequestSequence = long.MinValue;
        private bool _lastInstallSelectionActive;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview _lastPresetPreview;
        private long _lastPresetExitFocusSequence = long.MinValue;
        private string _lastPresetExitFocusTargetId = string.Empty;
        private object _shellMountLease;

        public bool IsConfigured => _configured;
        public bool IsOpen => _navigator != null && _navigator.IsOpen(CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId);
        public string LastStatusMessage { get; private set; } = string.Empty;
        public CharacterTestFairyGuiBehaviorDeckPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiBehaviorDeckPanelCommandSink CommandSink => _commandSink;
        public int RefreshAttemptCount { get; private set; }
        internal Fgui.GComponent RootForTests
        {
            get
            {
                IMxFairyGuiComponentHandle component;
                if (_layerHost == null
                    || !_layerHost.TryGetVisibleComponent(
                        CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                        out component))
                {
                    return null;
                }

                return (component as MxFairyGuiComponentHandle)?.Component;
            }
        }

        internal void Configure(
            GameSlice slice,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Func<bool> closePanel = null,
            Func<string, bool> openCardInLab = null,
            Func<bool> continueToSetup = null,
            Func<int, string, bool> updateShellContext = null,
            MxFairyGuiRuntimeShell runtimeShell = null,
            Func<double> unscaledTimeProvider = null,
            Func<bool> hasActiveRuntimeAiLabSession = null)
        {
            Release();
            _slice = slice;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            if (_slice?.Resources?.ResourceManager == null || service?.Workbench == null)
            {
                Warn("Behavior deck panel disabled because runtime resources or workbench are unavailable.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                service,
                closePanel ?? Close,
                _textProvider,
                openCardInLab,
                continueToSetup,
                updateShellContext,
                unscaledTimeProvider,
                hasActiveRuntimeAiLabSession);
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
            LastStatusMessage = string.Empty;
        }

        internal void Configure(
            GameSlice slice,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            Func<bool> closePanel = null,
            Func<string, bool> openCardInLab = null,
            Func<bool> continueToSetup = null,
            Func<int, string, bool> updateShellContext = null,
            MxFairyGuiRuntimeShell runtimeShell = null,
            Func<double> unscaledTimeProvider = null,
            Func<bool> hasActiveRuntimeAiLabSession = null)
        {
            Release();
            _slice = slice;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;
            if (_slice?.Resources?.ResourceManager == null || workbench == null)
            {
                Warn("Behavior deck panel disabled because runtime resources or workbench are unavailable.");
                return;
            }

            _commandSink = new CharacterTestFairyGuiBehaviorDeckPanelCommandSink(
                workbench,
                closePanel ?? Close,
                _textProvider,
                openCardInLab,
                continueToSetup,
                updateShellContext,
                unscaledTimeProvider,
                hasActiveRuntimeAiLabSession);
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
            LastStatusMessage = string.Empty;
        }

        public bool Open() => _shellMountLease == null && Refresh(openIfClosed: true);
        internal bool OpenFromShell(object lease) =>
            ReferenceEquals(_shellMountLease, lease) && Refresh(openIfClosed: true);

        internal void ConfigureShellMountLease(object lease)
        {
            _shellMountLease = lease ?? throw new ArgumentNullException(nameof(lease));
        }

        public void Refresh() => Refresh(openIfClosed: false);

        private bool Refresh(bool openIfClosed)
        {
            RefreshAttemptCount++;
            if (!_configured || _navigator == null || _commandSink == null)
                return false;
            _commandSink.ReconcileOperationFeedback();
            if (!openIfClosed && !IsOpen)
                return false;

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView sourceView = _commandSink.View;
            int pageIndex = _commandSink.PageIndex;
            string replacementSourceStableCardId = _commandSink.ReplacementSourceStableCardId;
            string detailsStableCardId = _commandSink.DetailsStableCardId;
            int detailsPageIndex = _commandSink.DetailsPageIndex;
            string commandStatus = _commandSink.LastStatusMessage;
            string textLocale = _textProvider.CurrentLocale.Value;
            long textRevision = _textProvider.Revision;
            long feedbackVersion = _commandSink.FeedbackVersion;
            long focusRequestSequence = _commandSink.FocusIntent.Sequence;
            bool installSelectionActive = _commandSink.InstallSelectionActive;
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.PresetPreview presetPreview =
                _commandSink.PresetPreview;
            long presetExitFocusSequence = _commandSink.PresetExitFocusSequence;
            string presetExitFocusTargetId = _commandSink.PresetExitFocusTargetId;
            if (IsOpen
                && ReferenceEquals(sourceView, _lastSourceView)
                && pageIndex == _lastPageIndex
                && string.Equals(
                    replacementSourceStableCardId,
                    _lastReplacementSourceStableCardId,
                    StringComparison.Ordinal)
                && string.Equals(commandStatus, _lastCommandStatus, StringComparison.Ordinal)
                && string.Equals(detailsStableCardId, _lastDetailsStableCardId, StringComparison.Ordinal)
                && detailsPageIndex == _lastDetailsPageIndex
                && string.Equals(textLocale, _lastTextLocale, StringComparison.Ordinal)
                && textRevision == _lastTextRevision
                && feedbackVersion == _lastFeedbackVersion
                && focusRequestSequence == _lastFocusRequestSequence
                && installSelectionActive == _lastInstallSelectionActive
                && ReferenceEquals(presetPreview, _lastPresetPreview)
                && presetExitFocusSequence == _lastPresetExitFocusSequence
                && string.Equals(presetExitFocusTargetId, _lastPresetExitFocusTargetId, StringComparison.Ordinal))
            {
                return true;
            }

            CharacterTestFairyGuiBehaviorDeckPanelViewModel viewModel =
                CharacterTestFairyGuiBehaviorDeckPanelViewModelBuilder.Build(
                    sourceView,
                    pageIndex,
                    replacementSourceStableCardId,
                    commandStatus,
                    _textProvider,
                    _commandSink.CanPreviousCharacter,
                    _commandSink.CanNextCharacter,
                    _commandSink.SelectedCharacterIndex,
                    _commandSink.CharacterCount,
                    _commandSink.SelectedCharacterDisplayName,
                    _commandSink.CanPreviewRecommendedPreset,
                    _commandSink.CanUndoRecommendedPreset,
                    _commandSink.CanAuthorInLab,
                    _commandSink.CanContinueToSetup,
                    _commandSink.OperationFeedback,
                    _commandSink.FocusIntent,
                    feedbackVersion,
                    detailsStableCardId,
                    detailsPageIndex,
                    presetPreview,
                    presetExitFocusSequence,
                    presetExitFocusTargetId,
                    installSelectionActive,
                    _commandSink.HasActiveRuntimeAiLabSession);
            string signature = CharacterTestFairyGuiBehaviorDeckPanelViewModelSignature.Create(viewModel)
                + "|text=" + CharacterTestUiText.Signature(_textProvider);
            if (IsOpen && string.Equals(signature, _lastSignature, StringComparison.Ordinal))
            {
                LastViewModel = viewModel;
                _lastSourceView = sourceView;
                _lastPageIndex = pageIndex;
                _lastReplacementSourceStableCardId = replacementSourceStableCardId;
                _lastDetailsStableCardId = detailsStableCardId;
                _lastDetailsPageIndex = detailsPageIndex;
                _lastCommandStatus = commandStatus;
                _lastTextLocale = textLocale;
                _lastTextRevision = textRevision;
                _lastFeedbackVersion = feedbackVersion;
                _lastFocusRequestSequence = focusRequestSequence;
                _lastInstallSelectionActive = installSelectionActive;
                _lastPresetPreview = presetPreview;
                _lastPresetExitFocusSequence = presetExitFocusSequence;
                _lastPresetExitFocusTargetId = presetExitFocusTargetId;
                return true;
            }

            MxUiOpenResult result = _navigator.Open(CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId, viewModel);
            if (result.Success)
            {
                LastViewModel = viewModel;
                _lastSignature = signature;
                _lastSourceView = sourceView;
                _lastPageIndex = pageIndex;
                _lastReplacementSourceStableCardId = replacementSourceStableCardId;
                _lastDetailsStableCardId = detailsStableCardId;
                _lastDetailsPageIndex = detailsPageIndex;
                _lastCommandStatus = commandStatus;
                _lastTextLocale = textLocale;
                _lastTextRevision = textRevision;
                _lastFeedbackVersion = feedbackVersion;
                _lastFocusRequestSequence = focusRequestSequence;
                _lastInstallSelectionActive = installSelectionActive;
                _lastPresetPreview = presetPreview;
                _lastPresetExitFocusSequence = presetExitFocusSequence;
                _lastPresetExitFocusTargetId = presetExitFocusTargetId;
                LastStatusMessage = _commandSink.LastStatusMessage;
                _reportedOpenFailure = false;
                return true;
            }
            if (!_reportedOpenFailure)
            {
                _reportedOpenFailure = true;
                Warn("Behavior deck panel open failed: " + result.Message);
            }
            return false;
        }

        public bool Close()
        {
            if (_shellMountLease != null)
                return false;
            return CloseCore();
        }

        internal bool CloseFromShell(object lease)
        {
            return ReferenceEquals(_shellMountLease, lease) && CloseCore();
        }

        internal bool TryDismissTopModalForVisibilityToggle(
            out bool dismissed,
            out string diagnostic)
        {
            dismissed = false;
            diagnostic = string.Empty;
            if (_commandSink == null)
            {
                diagnostic = "Behavior deck command sink is unavailable.";
                return false;
            }
            dismissed = _commandSink.TryDismissTopModalForVisibilityToggle(out diagnostic);
            if (dismissed)
                Refresh(openIfClosed: false);
            return true;
        }

        private bool CloseCore()
        {
            _commandSink?.ClearOperationState();
            bool closed = _navigator != null && _navigator.Close(CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId);
            _lastSignature = string.Empty;
            return closed;
        }

        public void DetachSlice()
        {
            _slice = null;
            _lastSignature = string.Empty;
            _lastSourceView = null;
            _commandSink?.UpdateService(null);
        }

        internal void RebindSlice(GameSlice slice, CharacterTestRuntimeAiPlannerLocalPlayerDeckService service)
        {
            _slice = slice;
            _commandSink?.UpdateService(service);
            _lastSignature = string.Empty;
            _lastSourceView = null;
            if (_configured && IsOpen)
            {
                Refresh();
            }
        }

        public void Release()
        {
            CloseCore();
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            _slice = null;
            _navigator = null;
            _layerHost = null;
            _commandSink = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _ownsLayerHost = false;
            _configured = false;
            _reportedOpenFailure = false;
            _lastSignature = string.Empty;
            _lastSourceView = null;
            _lastPageIndex = -1;
            _lastReplacementSourceStableCardId = string.Empty;
            _lastDetailsStableCardId = string.Empty;
            _lastDetailsPageIndex = -1;
            _lastCommandStatus = string.Empty;
            _lastTextLocale = string.Empty;
            _lastTextRevision = long.MinValue;
            _lastFeedbackVersion = long.MinValue;
            _lastFocusRequestSequence = long.MinValue;
            _lastInstallSelectionActive = false;
            _lastPresetPreview = null;
            _lastPresetExitFocusSequence = long.MinValue;
            _lastPresetExitFocusTargetId = string.Empty;
            _shellMountLease = null;
            LastStatusMessage = string.Empty;
            LastViewModel = null;
            RefreshAttemptCount = 0;
        }

        private static MxFairyGuiRuntimeCatalog CreateCatalog(
            IMxUiCommandSink commandSink,
            IMxUiTextProvider textProvider)
        {
            MxUiViewContract contract = CreateRuntimeViewContractForTests();
            var catalog = new MxFairyGuiRuntimeCatalog();
            catalog.Register(new MxFairyGuiRuntimeViewRegistration<CharacterTestFairyGuiBehaviorDeckPanelViewModel>(
                contract,
                CharacterTestFairyGuiBehaviorDeckPanelManifest.CreatePackageDescriptor(),
                new CharacterTestFairyGuiBehaviorDeckPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    textProvider)));
            return catalog;
        }

        internal static MxUiViewContract CreateRuntimeViewContractForTests()
        {
            MxUiViewContract contract = CharacterTestFairyGuiBehaviorDeckPanelManifest.CreateViewContract();
            contract.Descriptor.CloseOnSceneChange = true;
            contract.Descriptor.KeepAlive = false;
            contract.Descriptor.Modal = true;
            contract.Commands = AddRuntimeCommands(contract.Commands);
            contract.DiagnosticsTags = new[] { "character-test", "fairygui", "runtime-ai-planner", "behavior-deck" };
            return contract;
        }

        private static IReadOnlyList<MxUiCommandDescriptor> AddRuntimeCommands(
            IReadOnlyList<MxUiCommandDescriptor> generated)
        {
            generated = generated ?? Array.Empty<MxUiCommandDescriptor>();
            var commands = new List<MxUiCommandDescriptor>(generated.Count + 2)
            {
                new MxUiCommandDescriptor
                {
                    CommandId = MxFairyGuiCommandIds.Cancel,
                    Owner = "CharacterTest"
                },
                new MxUiCommandDescriptor
                {
                    CommandId = CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                    PayloadType = typeof(CharacterTestFairyGuiBehaviorDeckCardActionPayload).FullName,
                    Owner = "CharacterTest"
                }
            };
            for (int i = 0; i < generated.Count; i++)
                commands.Add(generated[i]);
            return commands;
        }

        private void LogFirstDiagnostic(MxFairyGuiRuntimeCatalogDiagnostics diagnostics)
        {
            IReadOnlyList<MxFairyGuiRuntimeCatalogIssue> issues = diagnostics.Issues;
            Warn(issues.Count > 0
                ? issues[0].Code + ": " + issues[0].Message
                : "Behavior deck FairyGUI catalog diagnostics failed.");
        }

        private void ConfigureFairyGuiCameraStack()
        {
            if (!Application.isPlaying)
                return;
            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
            if (baseCamera != null && overlayCamera != null)
            {
                overlayCamera.clearFlags = CameraClearFlags.Depth;
                overlayCamera.targetTexture = baseCamera.targetTexture;
                overlayCamera.targetDisplay = baseCamera.targetDisplay;
                overlayCamera.rect = baseCamera.rect;
            }
            MxCameraUrpStackResult result = MxCameraUrpOverlayStackBinder.Bind(
                baseCamera,
                overlayCamera,
                excludeOverlayLayersFromBaseCamera: true);
            if (!result.Success)
                Warn("FairyGUI camera stack bind failed: " + result.Message);
        }

        private void Warn(string message)
        {
            LastStatusMessage = message ?? string.Empty;
            _logger.Warning("CharacterTest.FairyGUI", LastStatusMessage);
        }
    }
}
