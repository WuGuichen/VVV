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
    [DefaultExecutionOrder(109)]
    [AddComponentMenu("MxFramework/Game/Character Test/FairyGUI Runtime AI Planner Dev Panel Host")]
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost : MonoBehaviour
    {
        private GameSlice _slice;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        private IRuntimeLogger _logger = NullRuntimeLogger.Instance;
        private MxFairyGuiNavigator _navigator;
        private MxFairyGuiLayerHost _layerHost;
        private IMxFairyGuiPopupService _popupService;
        private bool _ownsPopupService;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink _commandSink;
        private CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler _behaviorCardWorkspaceCommands;
        private readonly CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel _behaviorCardWorkspaceViewModel =
            new CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel();
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel _retainedBehaviorCardViewModel;
        private IMxUiTextProvider _textProvider = MxUiNullTextProvider.Instance;
        private CharacterTestRuntimeAiLabBehaviorCardPreviewHost _behaviorCardPreviewHost;
        private bool _ownsLayerHost;
        private bool _configured;
        private static bool s_characterActionPanelRemountRequested;
        private bool _recoveryConfigured;
        private bool _reportedOpenFailure;
        private string _lastViewModelSignature = string.Empty;
        private bool _hasLastViewModelSignature;
        private long _lastBehaviorCardWorkspaceRevision;
        private CharacterTestUiTextProviderSignature _lastBehaviorCardTextSignature;
        private bool _hasLastBehaviorCardWorkspaceRevision;
        private string _lastStatusMessage = string.Empty;
        private object _shellMountLease;
        private Action<string, string> _previewContractFaultSink;
        private CharacterTestRuntimeAiLabShortcutInput _shortcutInput;
        private CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder _workspaceBinder;
        private readonly CharacterTestFairyGuiToastPresenter _toastPresenter =
            new CharacterTestFairyGuiToastPresenter();

        public bool IsConfigured => _configured;
        internal bool IsRecoveryConfigured => _recoveryConfigured;
        public bool IsOpen => IsWorkspaceOpen || IsRecoveryOpen;
        private bool IsWorkspaceOpen =>
            _navigator != null
            && _navigator.IsOpen(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
        private bool IsRecoveryOpen =>
            _navigator != null
            && _navigator.IsOpen(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
        public int RefreshAttemptCount { get; private set; }
        public int SuccessfulRefreshCount { get; private set; }
        public int SkippedRefreshCount { get; private set; }
        public string LastStatusMessage => _lastStatusMessage;
        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel LastViewModel { get; private set; }
        public CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink CommandSink => _commandSink;
        internal bool HasBoundPreviewTexture => _behaviorCardPreviewHost?.BoundRenderTexture != null;
        internal int PreviewResourceGeneration => _behaviorCardPreviewHost?.ResourceGeneration ?? 0;
        internal Fgui.GComponent RootForTests
        {
            get
            {
                IMxFairyGuiComponentHandle component;
                if (_layerHost == null
                    || !_layerHost.TryGetVisibleComponent(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        out component))
                {
                    return null;
                }

                return (component as MxFairyGuiComponentHandle)?.Component;
            }
        }

        internal IMxFairyGuiPopupService PopupServiceForTests => _popupService;
        internal bool IsActuallyVisible
        {
            get
            {
                Fgui.GComponent root = RootForTests;
                return IsWorkspaceOpen
                    && root != null
                    && !root.isDisposed
                    && root.visible
                    && root.onStage
                    && root.parent != null;
            }
        }

        public void Configure(
            GameSlice slice,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            IRuntimeLogger logger = null,
            MxFairyGuiLayerHost layerHost = null,
            IMxUiTextProvider textProvider = null,
            IMxFairyGuiInputContextBridge inputBridge = null,
            IMxFairyGuiHost fairyGuiHost = null,
            bool autoOpen = false,
            MxFairyGuiRuntimeShell runtimeShell = null,
            IMxFairyGuiPopupService popupService = null)
        {
            bool reopenPanel = autoOpen;
            Release();
            _slice = slice;
            _controller = controller;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _textProvider = textProvider ?? MxUiNullTextProvider.Instance;

            _shortcutInput = new CharacterTestRuntimeAiLabShortcutInput(HandleShortcut);
            _shortcutInput.Enable();

            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                Warn("Runtime AI Planner dev panel disabled because resource runtime is not available.");
                return;
            }

            if (_controller == null)
            {
                Warn("Runtime AI Planner dev panel disabled because dev controller is not available.");
                return;
            }

            _layerHost = layerHost ?? new MxFairyGuiLayerHost();
            _ownsLayerHost = layerHost == null;
            _toastPresenter.Configure(_layerHost);

            _commandSink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(
                _controller,
                Close,
                () =>
                {
                    Refresh();
                    return true;
                },
                status =>
                {
                    _commandSink?.SetShellStatus(status);
                    if (!string.IsNullOrWhiteSpace(status))
                        _toastPresenter.Show(status, isWarning: false);
                },
                CharacterTestRuntimeAiLabBehaviorRecipeEditorLog.WriteWorkbenchMutation,
                (message, isWarning) => _toastPresenter.Show(message, isWarning),
                remountPanel: RequestCharacterActionPanelRemount);
            _behaviorCardWorkspaceCommands =
                new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
            _commandSink.ConfigureBehaviorCardWorkspaceCommands(_behaviorCardWorkspaceCommands);
            ConfigureBehaviorCardWorkspaceCommands(null, null);
            _behaviorCardPreviewHost =
                new CharacterTestRuntimeAiLabBehaviorCardPreviewHost(_controller);
            _popupService = popupService ?? new MxFairyGuiPopupService();
            _ownsPopupService = popupService == null;
            MxFairyGuiRuntimeCatalog workspaceCatalog =
                CreateWorkspaceCatalog(_commandSink, _textProvider);
            MxFairyGuiRuntimeCatalog recoveryCatalog = CreateRecoveryCatalog(_commandSink);
            MxFairyGuiRuntimeCatalogDiagnostics workspaceDiagnostics =
                workspaceCatalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            MxFairyGuiRuntimeCatalogDiagnostics recoveryDiagnostics =
                recoveryCatalog.CreateDiagnostics(_slice.Resources.ResourceManager);
            ApplyCatalogDiagnostics(workspaceDiagnostics, recoveryDiagnostics);

            var catalog = new MxFairyGuiRuntimeCatalog();
            CopyRegistrations(workspaceCatalog, catalog);
            CopyRegistrations(recoveryCatalog, catalog);

            ConfigureFairyGuiCameraStack();
            _navigator = runtimeShell != null
                ? runtimeShell.Register(catalog)
                : MxFairyGuiRuntimeShellComposition.CreateNavigator(
                    _slice.Resources.ResourceManager,
                    catalog,
                    host: fairyGuiHost,
                    layerHost: _layerHost,
                    inputBridge: inputBridge,
                    popupService: _popupService);
            _reportedOpenFailure = false;
            if (_configured)
                _lastStatusMessage = string.Empty;
            if (reopenPanel)
                Open();
        }

        internal bool TryRevalidateConfiguration(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_slice == null || _slice.Resources?.ResourceManager == null)
            {
                _configured = false;
                _recoveryConfigured = false;
                diagnostic = "Runtime AI Lab resource runtime is unavailable.";
                Warn(diagnostic);
                return false;
            }

            MxFairyGuiRuntimeCatalogDiagnostics workspaceDiagnostics =
                CreateWorkspaceCatalog(_commandSink, _textProvider)
                    .CreateDiagnostics(_slice.Resources.ResourceManager);
            MxFairyGuiRuntimeCatalogDiagnostics recoveryDiagnostics =
                CreateRecoveryCatalog(_commandSink)
                    .CreateDiagnostics(_slice.Resources.ResourceManager);
            bool success = ApplyCatalogDiagnostics(workspaceDiagnostics, recoveryDiagnostics);
            diagnostic = success ? string.Empty : _lastStatusMessage;
            return success;
        }

        public void Refresh()
        {
            Refresh(openIfClosed: false);
        }

        public bool Open()
        {
            if (_shellMountLease != null)
                return false;
            return Refresh(openIfClosed: true);
        }

        public bool Close()
        {
            if (_shellMountLease != null)
                return false;
            return CloseCore();
        }

        private void HandleShortcut(CharacterTestRuntimeAiLabShortcut shortcut)
        {
            if (_controller?.CardTrialOpen == true) return;
            if (!IsWorkspaceOpen)
                return;

            Fgui.GRoot root = Fgui.GRoot.inst;
            bool textInputFocused = root != null && root.focus is Fgui.GTextInput;
            bool popupOpen = _popupService?.HasOpenPopup == true;
            bool runtimeControlsPopupOpen = popupOpen
                && string.Equals(
                    _popupService.TopPopup?.name,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder
                        .RuntimeTestAdvancedPopupName,
                    StringComparison.Ordinal);
            switch (shortcut)
            {
                case CharacterTestRuntimeAiLabShortcut.CancelPopup:
                    if (popupOpen) _popupService.HideTop(MxFairyGuiPopupCloseReason.Cancel);
                    break;
                case CharacterTestRuntimeAiLabShortcut.TogglePlayPause:
                    ProcessSpaceShortcut(true, textInputFocused, popupOpen);
                    break;
                case CharacterTestRuntimeAiLabShortcut.PreviousFrame:
                    ProcessPlaybackNavigationShortcut(true, false, textInputFocused, popupOpen);
                    break;
                case CharacterTestRuntimeAiLabShortcut.NextFrame:
                    ProcessPlaybackNavigationShortcut(false, true, textInputFocused, popupOpen);
                    break;
                case CharacterTestRuntimeAiLabShortcut.IncreasePlaybackSpeed:
                    ProcessPlaybackSpeedShortcut(
                        true,
                        textInputFocused,
                        popupOpen && !runtimeControlsPopupOpen);
                    break;
                case CharacterTestRuntimeAiLabShortcut.DecreasePlaybackSpeed:
                    ProcessPlaybackSpeedShortcut(
                        false,
                        textInputFocused,
                        popupOpen && !runtimeControlsPopupOpen);
                    break;
            }
        }

        internal bool ProcessSpaceShortcut(
            bool shortcutPressed,
            bool textInputFocused,
            bool popupOpen)
        {
            if (!shortcutPressed
                || textInputFocused
                || popupOpen
                || !IsWorkspaceOpen
                || _commandSink == null
                || LastViewModel == null)
            {
                return false;
            }

            string commandId;
            object payload = null;
            switch (LastViewModel.WorkspaceMode)
            {
                case CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    when LastViewModel.BehaviorCardWorkspace?.Timeline?.TacticalPreview != null
                        && LastViewModel.PresentationContext == CharacterTestRuntimeAiPlannerDevPresentationContext.Setup
                        && LastViewModel.CanTestCurrentChanges:
                    // Match the preview Play button: prepare the frozen single-card test,
                    // then play it when the normal test transaction has finished starting.
                    commandId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.TestCurrentChanges;
                    payload = true;
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard
                    when LastViewModel.PresentationContext != CharacterTestRuntimeAiPlannerDevPresentationContext.Setup
                        && LastViewModel.BehaviorCardRuntimeControl?.CanPlayPause == true:
                    commandId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume;
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.Setup
                    when LastViewModel.CanStart:
                    commandId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start;
                    break;
                case CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                    when (LastViewModel.ShowBehaviorLogic || LastViewModel.ShowBehaviorPlan)
                        ? LastViewModel.BehaviorCardRuntimeControl?.CanPlayPause == true
                        : LastViewModel.CanPauseResume:
                    commandId = CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume;
                    break;
                default:
                    return false;
            }

            _commandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                payload));
            return true;
        }

        internal bool ProcessPlaybackSpeedShortcut(
            bool increase,
            bool textInputFocused,
            bool popupOpen)
        {
            if (textInputFocused
                || popupOpen
                || !IsWorkspaceOpen
                || _commandSink == null
                || LastViewModel?.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics
                || !LastViewModel.CanChangeSpeed)
            {
                return false;
            }

            string commandId = increase
                ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.IncreasePlaybackSpeed
                : CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DecreasePlaybackSpeed;
            _commandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                null));
            return true;
        }

        internal bool ProcessPlaybackNavigationShortcut(
            bool previousPressed,
            bool nextPressed,
            bool textInputFocused,
            bool popupOpen)
        {
            if (previousPressed == nextPressed
                || textInputFocused
                || popupOpen
                || !IsWorkspaceOpen
                || _commandSink == null
                || LastViewModel?.WorkspaceMode != CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics)
            {
                return false;
            }

            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel runtime =
                LastViewModel.RuntimeFrame;
            bool busy = runtime == null
                || runtime.ReplayInProgress
                || runtime.FutureAdvanceActive
                || runtime.SelectionPhase != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle;
            bool playbackActive = LastViewModel.PresentationContext
                    == CharacterTestRuntimeAiPlannerDevPresentationContext.Live
                || LastViewModel.PresentationContext
                    == CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview
                || runtime?.HistoryPlaybackActive == true;
            bool advanceLatestFrame = !previousPressed
                && !busy
                && runtime.Available
                && runtime.SelectedFrame >= runtime.LatestFrame
                && runtime.SelectedFrame < runtime.SessionStartFrame + runtime.FrameLimit - 1
                && (LastViewModel.CanStep
                    || (playbackActive && LastViewModel.CanPauseResume));
            bool canNavigate = !busy
                && runtime.Available
                && (previousPressed
                    ? runtime.SelectedFrame > runtime.RetainedStartFrame
                    : runtime.SelectedFrame < runtime.LatestFrame || advanceLatestFrame);
            if (!canNavigate)
                return false;

            if (playbackActive && LastViewModel.CanPauseResume)
            {
                _commandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                    null));
            }

            string commandId = previousPressed
                ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PreviousFrame
                : advanceLatestFrame
                    ? CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step
                    : CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.NextFrame;
            _commandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                null));
            return true;
        }

        internal void ConfigureShellMountLease(object lease)
        {
            _shellMountLease = lease ?? throw new ArgumentNullException(nameof(lease));
        }

        internal void ConfigurePreviewContractFaultSink(Action<string, string> sink)
        {
            _previewContractFaultSink = sink;
        }

        private bool CloseCore()
        {
            _behaviorCardPreviewHost?.SetActive(false);
            _controller?.ClearTimelineItemSelection();
            _controller?.CancelBranchRecompute();
            bool closedWorkspace = _navigator != null
                && _navigator.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
            bool closedRecovery = _navigator != null
                && _navigator.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
            ClearOpenViewState();
            return closedWorkspace || closedRecovery;
        }

        internal bool MountShellWorkspace(
            object lease,
            CharacterTestRuntimeAiLabShellState state)
        {
            if (!ReferenceEquals(_shellMountLease, lease)
                || state == null
                || _commandSink == null
                || _controller == null
                || !_configured)
            {
                WriteVisibility("mount-workspace-rejected", state, "rejected", "Host prerequisites are unavailable.");
                return false;
            }

            _navigator?.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
            CharacterTestRuntimeAiLabWorkspaceMode workspaceMode;
            CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab;
            MapWorkspace(state.ActiveWorkspace, out workspaceMode, out diagnosticsTab);
            CharacterTestRuntimeAiLabWorkspaceMode returnMode;
            CharacterTestRuntimeAiLabDiagnosticsTab ignored;
            MapWorkspace(state.PageContext.ReturnWorkspace, out returnMode, out ignored);

            int entryIndex = _commandSink.SelectedWorkbenchEntryIndex;
            int selectedStepIndex = _commandSink.SelectedCompiledStepIndex;
            CharacterTestRuntimeAiLabLogicElementKind selectedKind =
                _commandSink.SelectedLogicElementKind;
            int selectedIndex = _commandSink.SelectedLogicElementIndex;
            bool requiresBehaviorCardContext =
                state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                || (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                    && state.PageContext.TimelineMode ==
                        CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan);
            bool showsBehaviorLogic =
                state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                && state.PageContext.TimelineMode ==
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
            if (requiresBehaviorCardContext || showsBehaviorLogic)
            {
                bool resolved = _controller.TryResolveBehaviorCardShellContext(
                        state.PageContext,
                        out entryIndex,
                        out selectedKind,
                        out selectedIndex);
                if (!resolved && requiresBehaviorCardContext)
                {
                    WriteVisibility(
                        "mount-workspace-context-missing",
                        state,
                        "failed",
                        "The saved Behavior Card identity no longer resolves in the draft workspace.");
                    return false;
                }
                if (resolved)
                {
                    selectedStepIndex = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                        ? selectedIndex
                        : 0;
                }
                else
                {
                    entryIndex = -1;
                    selectedStepIndex = 0;
                    selectedKind = CharacterTestRuntimeAiLabLogicElementKind.None;
                    selectedIndex = -1;
                }
            }

            _commandSink.RestoreNavigationState(
                workspaceMode,
                diagnosticsTab,
                state.PageContext.TimelineMode,
                returnMode,
                entryIndex,
                selectedStepIndex,
                selectedKind,
                selectedIndex);
            bool refreshed = Refresh(openIfClosed: true, mountingState: state);
            bool visible = refreshed && IsActuallyVisible;
            WriteVisibility(
                "mount-workspace-result",
                state,
                visible ? "visible" : "failed",
                visible ? string.Empty : _lastStatusMessage);
            return visible;
        }

        internal bool MountShellStatus(
            object lease,
            CharacterTestRuntimeAiLabShellState state,
            string status)
        {
            if (!ReferenceEquals(_shellMountLease, lease)
                || state == null
                || _commandSink == null
                || _navigator == null
                || !_recoveryConfigured)
            {
                WriteVisibility("mount-status-rejected", state, "rejected", "Host prerequisites are unavailable.");
                return false;
            }
            _commandSink.SetShellStatus(
                status,
                state.PresentationMode
                    == CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress);
            _behaviorCardPreviewHost?.SetActive(false);
            _navigator.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
            ClearOpenViewState();
            MxUiOpenResult result = _navigator.Open(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId,
                CharacterTestRuntimeAiLabShellSurfaceViewModel.Create(state, status));
            if (result.Success)
            {
                Fgui.GComponent recoveryRoot = ResolveVisibleRoot(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
                bool recoveryVisible = recoveryRoot != null
                    && !recoveryRoot.isDisposed
                    && recoveryRoot.visible
                    && recoveryRoot.onStage
                    && recoveryRoot.parent != null;
                if (!recoveryVisible)
                {
                    WriteVisibility(
                        "mount-status-result",
                        state,
                        "failed",
                        "Recovery root is not actually visible.");
                }
                return recoveryVisible;
            }
            Warn("Runtime AI Lab fail-safe surface open failed: " + result.Message);
            WriteVisibility("mount-status-result", state, "failed", result.Message);
            return false;
        }

        internal void UnmountShell(object lease)
        {
            if (!ReferenceEquals(_shellMountLease, lease))
                return;
            CharacterTestRuntimeAiLabShellState state = _controller?.ShellCoordinator?.CurrentState;
            bool wasActuallyVisible = IsActuallyVisible;
            _behaviorCardPreviewHost?.ReleaseSuspendedResources();
            if (_navigator != null)
            {
                _navigator.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
                _navigator.Close(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
            }
            ClearOpenViewState();
            if (wasActuallyVisible)
                WriteVisibility("unmount-visible-root", state, "hidden", string.Empty);
        }

        private static void MapWorkspace(
            CharacterTestRuntimeAiLabWorkspace workspace,
            out CharacterTestRuntimeAiLabWorkspaceMode workspaceMode,
            out CharacterTestRuntimeAiLabDiagnosticsTab diagnosticsTab)
        {
            diagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
            switch (workspace)
            {
                case CharacterTestRuntimeAiLabWorkspace.TacticalLibrary:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.TacticalLibrary;
                    return;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorGroup:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
                    return;
                case CharacterTestRuntimeAiLabWorkspace.BehaviorCard:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                    return;
                case CharacterTestRuntimeAiLabWorkspace.RuntimeFrame:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    diagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                    return;
                case CharacterTestRuntimeAiLabWorkspace.Replay:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    diagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Replay;
                    return;
                case CharacterTestRuntimeAiLabWorkspace.Facts:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    return;
                default:
                    workspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
                    return;
            }
        }

        internal bool Toggle()
        {
            if (_shellMountLease != null)
                return false;
            return IsOpen ? Close() : Open();
        }

        public void DetachSlice()
        {
            _slice = null;
            _behaviorCardPreviewHost?.ReleaseSuspendedResources();
        }

        public void RebindSlice(
            GameSlice slice,
            CharacterTestRuntimeAiPlannerDevSceneController controller = null)
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
            _shortcutInput?.Dispose();
            _shortcutInput = null;
            _behaviorCardPreviewHost?.Dispose();
            _behaviorCardPreviewHost = null;
            _toastPresenter.Hide();
            _controller?.ClearTimelineItemSelection();
            _controller?.CancelBranchRecompute();
            if (_navigator != null)
            {
                _navigator.ReleaseView(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
                _navigator.ReleaseView(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryViewId);
            }
            ClearOpenViewState();
            if (_ownsLayerHost)
                _layerHost?.Dispose();
            if (_ownsPopupService)
                _popupService?.Dispose();
            _layerHost = null;
            _ownsLayerHost = false;
            _popupService = null;
            _ownsPopupService = false;
            _navigator = null;
            _slice = null;
            _controller = null;
            _commandSink = null;
            _behaviorCardWorkspaceCommands = null;
            _textProvider = MxUiNullTextProvider.Instance;
            _configured = false;
            _recoveryConfigured = false;
            _reportedOpenFailure = false;
            _lastStatusMessage = string.Empty;
            _shellMountLease = null;
            _previewContractFaultSink = null;
            _workspaceBinder = null;
            LastViewModel = null;
            _retainedBehaviorCardViewModel = null;
            RefreshAttemptCount = 0;
            SuccessfulRefreshCount = 0;
            SkippedRefreshCount = 0;
        }

        private void Update()
        {
            TryFulfillCharacterActionPanelRemount();
            _toastPresenter.Tick(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            // Follow the displayed simulation frame after Update, independently of UI throttling.
            _behaviorCardPreviewHost?.TickCameraPresentation();
            // DrawMesh submissions last one render frame, independently of throttled UI refresh.
            _behaviorCardPreviewHost?.DrawTargetHurtboxes();
        }

        internal static bool RequestCharacterActionPanelRemount()
        {
            s_characterActionPanelRemountRequested = true;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost host =
                UnityEngine.Object.FindAnyObjectByType<
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            return host != null && host.TryFulfillCharacterActionPanelRemount();
        }

        private bool TryFulfillCharacterActionPanelRemount()
        {
            if (!s_characterActionPanelRemountRequested)
                return false;
            if (!Refresh(openIfClosed: true))
                return false;
            s_characterActionPanelRemountRequested = false;
            return true;
        }

        private void OnDestroy()
        {
            _toastPresenter.Dispose();
        }

    }
}
