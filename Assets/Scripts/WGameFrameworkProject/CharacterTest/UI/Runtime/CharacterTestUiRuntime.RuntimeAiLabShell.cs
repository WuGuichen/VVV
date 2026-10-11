using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiLabDeferredPreviewFault
    {
        private bool _pending;
        private string _code = string.Empty;
        private string _detail = string.Empty;

        internal void Enqueue(string code, string detail)
        {
            if (_pending)
                return;
            _pending = true;
            _code = code ?? string.Empty;
            _detail = detail ?? string.Empty;
        }

        internal bool TryTake(out string code, out string detail)
        {
            code = _code;
            detail = _detail;
            if (!_pending)
                return false;
            Clear();
            return true;
        }

        internal void Clear()
        {
            _pending = false;
            _code = string.Empty;
            _detail = string.Empty;
        }
    }

    public sealed partial class CharacterTestUiRuntime
    {
        private readonly CharacterTestRuntimeAiLabDeferredPreviewFault
            _runtimeAiLabDeferredPreviewFault = new CharacterTestRuntimeAiLabDeferredPreviewFault();

        private void ConfigureRuntimeAiLabShell()
        {
            if (_runtimeAiPlannerDevSceneController == null)
                return;

            bool playerDeckAvailable = _fairyGuiBehaviorDeckPanelHost != null
                && _fairyGuiBehaviorDeckPanelHost.IsConfigured;
            bool labAvailable = _fairyGuiRuntimeAiPlannerDevPanelHost != null
                && _fairyGuiRuntimeAiPlannerDevPanelHost.IsConfigured;
            _runtimeAiLabShellCoordinator =
                _runtimeAiPlannerDevSceneController.EnsureShellCoordinator(
                    playerDeckAvailable,
                    labAvailable,
                    CharacterTestRuntimeAiLabWorkspace.Setup);
            object mountLease = _runtimeAiLabShellCoordinator.MountLease;
            _fairyGuiBehaviorDeckPanelHost?.ConfigureShellMountLease(mountLease);
            _fairyGuiRuntimeAiPlannerDevPanelHost?.ConfigureShellMountLease(mountLease);
            _fairyGuiRuntimeAiPlannerDevPanelHost?.ConfigurePreviewContractFaultSink(
                HandleRuntimeAiLabPreviewContractFault);
            _runtimeAiLabShellMountAdapter = new RuntimeAiLabShellMountAdapter(
                _fairyGuiBehaviorDeckPanelHost,
                _fairyGuiRuntimeAiPlannerDevPanelHost,
                _runtimeAiPlannerDevSceneController,
                mountLease);
            CharacterTestRuntimeAiLabShellState state =
                _runtimeAiLabShellCoordinator.CurrentState;
            if (!labAvailable
                && state.PresentationMode == CharacterTestRuntimeAiLabPresentationMode.Workspace)
            {
                _runtimeAiLabShellCoordinator.EnterUnavailableRecovery(
                    _fairyGuiRuntimeAiPlannerDevPanelHost?.LastStatusMessage
                        ?? "Runtime AI Lab package or contract preflight failed.",
                    state.ActiveWorkspace);
            }
            _runtimeAiLabShellCoordinator.AttachMountAdapter(
                _runtimeAiLabShellCoordinator.CurrentState.Generation,
                _runtimeAiLabShellMountAdapter);
        }

        private void ReleaseRuntimeAiLabShellMount()
        {
            _runtimeAiLabDeferredPreviewFault.Clear();
            if (_runtimeAiLabShellCoordinator != null && _runtimeAiLabShellMountAdapter != null)
                _runtimeAiLabShellCoordinator.DetachMountAdapter(_runtimeAiLabShellMountAdapter);
            _runtimeAiLabShellMountAdapter = null;
            _runtimeAiLabShellCoordinator = null;
        }

        private bool NavigateRuntimeAiLab(CharacterTestRuntimeAiLabWorkspace target)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _runtimeAiLabShellCoordinator;
            if (coordinator == null)
                return false;
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            return coordinator.Navigate(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                target).Success;
        }

        private bool BackRuntimeAiLab()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _runtimeAiLabShellCoordinator;
            if (coordinator == null)
                return false;
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            return coordinator.Back(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace).Success;
        }

        private bool HasRetainedRuntimeAiLabSession() =>
            _runtimeAiPlannerDevSceneController != null
            && !_runtimeAiPlannerDevSceneController.IsShellSetupPhase
            && _runtimeAiPlannerDevSceneController.HasShellRuntimeHistory;

        private bool ContinueRuntimeAiLabToSetup()
        {
            CharacterTestRuntimeAiLabWorkspace target = HasRetainedRuntimeAiLabSession()
                ? CharacterTestRuntimeAiLabWorkspace.RuntimeFrame
                : CharacterTestRuntimeAiLabWorkspace.Setup;
            return NavigateRuntimeAiLab(target);
        }

        private bool UpdateRuntimeAiLabPlayerDeckContext(
            int pageIndex,
            string replacementSourceStableCardId)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator = _runtimeAiLabShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            return state != null
                && state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                && coordinator.TryUpdatePlayerDeckContext(
                    state.Generation,
                    state.Revision,
                    pageIndex,
                    replacementSourceStableCardId).Success;
        }

        private bool ToggleRuntimeAiLabVisibility()
        {
            return _runtimeAiLabShellCoordinator != null
                && _runtimeAiLabShellCoordinator.ToggleVisibility().Success;
        }

        private void HandleRuntimeAiLabPreviewContractFault(string code, string detail)
        {
            _runtimeAiLabDeferredPreviewFault.Enqueue(code, detail);
        }

        private void FlushRuntimeAiLabPreviewContractFault()
        {
            if (!_runtimeAiLabDeferredPreviewFault.TryTake(out string code, out string detail))
                return;

            CharacterTestRuntimeAiLabShellCoordinator coordinator = _runtimeAiLabShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (state == null
                || state.Visibility != CharacterTestRuntimeAiLabShellVisibility.Visible
                || state.PresentationMode != CharacterTestRuntimeAiLabPresentationMode.Workspace
                || state.ActiveWorkspace != CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                return;
            }

            coordinator.EnterRecovery(
                string.IsNullOrEmpty(code)
                    ? CharacterTestRuntimeAiLabBehaviorCardPreviewHost.RootUnavailableCode
                    : code,
                string.IsNullOrEmpty(detail)
                    ? "Behavior Card preview surface is unavailable."
                    : detail,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                true,
                false);
        }

        private sealed class RuntimeAiLabShellMountAdapter : ICharacterTestRuntimeAiLabShellMountAdapter
        {
            private readonly CharacterTestFairyGuiBehaviorDeckPanelHost _playerDeckHost;
            private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost _labHost;
            private readonly CharacterTestRuntimeAiPlannerDevSceneController _controller;
            private readonly object _mountLease;

            internal RuntimeAiLabShellMountAdapter(
                CharacterTestFairyGuiBehaviorDeckPanelHost playerDeckHost,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost labHost,
                CharacterTestRuntimeAiPlannerDevSceneController controller,
                object mountLease)
            {
                _playerDeckHost = playerDeckHost;
                _labHost = labHost;
                _controller = controller;
                _mountLease = mountLease;
            }

            public bool IsSetupPhase => _controller != null && _controller.IsShellSetupPhase;

            public bool HasRuntimeHistory =>
                _controller != null && _controller.HasShellRuntimeHistory;

            public bool TryPrepareForReopen(out string diagnostic)
            {
                if (_labHost == null)
                {
                    diagnostic = "Runtime AI Lab host is unavailable.";
                    return false;
                }
                return _labHost.TryRevalidateConfiguration(out diagnostic);
            }

            public bool IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace workspace)
            {
                if (_labHost == null || !_labHost.IsConfigured)
                    return false;
                return workspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                    ? _playerDeckHost != null && _playerDeckHost.IsConfigured
                    : true;
            }

            public bool TryNormalizeWorkspaceContext(
                CharacterTestRuntimeAiLabWorkspace workspace,
                CharacterTestRuntimeAiLabPageContext context,
                out CharacterTestRuntimeAiLabPageContext normalizedContext,
                out string diagnostic)
            {
                normalizedContext = context;
                diagnostic = string.Empty;
                if (_labHost == null || !_labHost.IsConfigured)
                {
                    diagnostic = "Runtime AI Lab package or contract preflight failed.";
                    return false;
                }
                if (workspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                {
                    bool available = _playerDeckHost != null && _playerDeckHost.IsConfigured;
                    if (!available)
                        diagnostic = "Player Deck workspace is unavailable.";
                    return available;
                }
                if (_controller == null)
                {
                    diagnostic = "Runtime AI Lab controller is unavailable.";
                    return false;
                }
                return _controller.TryNormalizeShellWorkspaceContext(
                    workspace,
                    context,
                    out normalizedContext,
                    out diagnostic);
            }

            public bool TryMountWorkspace(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                if (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck
                    || state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.Setup)
                {
                    _controller?.RestorePublishedCardAuthoringSlotBinding();
                }
                UnmountAll();
                if (_labHost == null || !_labHost.IsConfigured)
                {
                    diagnostic = "Runtime AI Lab package or contract preflight failed.";
                    return false;
                }
                if (state.ActiveWorkspace == CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                {
                    if (_playerDeckHost == null || !_playerDeckHost.IsConfigured)
                    {
                        diagnostic = "Player Deck workspace is unavailable.";
                        return false;
                    }
                    _playerDeckHost.CommandSink?.RestoreShellContext(
                        state.PageContext.PlayerDeckPageIndex,
                        state.PageContext.ReplacementSourceStableCardId);
                    if (_playerDeckHost.OpenFromShell(_mountLease))
                    {
                        diagnostic = string.Empty;
                        return true;
                    }
                    diagnostic = _playerDeckHost.LastStatusMessage;
                    return false;
                }

                if (_labHost == null || !_labHost.IsConfigured)
                {
                    diagnostic = "Runtime AI Lab workspace is unavailable.";
                    return false;
                }
                if (_labHost.MountShellWorkspace(_mountLease, state))
                {
                    diagnostic = string.Empty;
                    return true;
                }
                diagnostic = _labHost.LastStatusMessage;
                return false;
            }

            public bool TryMountTransition(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                UnmountAll();
                if (_labHost != null
                    && _labHost.IsConfigured
                    && _labHost.MountShellStatus(
                        _mountLease,
                        state,
                        "正在建立 Runtime AI Lab 会话…"))
                {
                    diagnostic = string.Empty;
                    return true;
                }
                diagnostic = "Runtime AI Lab transition surface is unavailable.";
                return false;
            }

            public bool TryMountRecovery(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                UnmountAll();
                string status = state.Fault != null
                    ? state.Fault.Title + "\n" + state.Fault.Detail
                    : "Runtime AI Lab needs recovery.";
                if (_labHost != null
                    && _labHost.IsConfigured
                    && _labHost.MountShellStatus(_mountLease, state, status))
                {
                    diagnostic = string.Empty;
                    return true;
                }
                diagnostic = "Runtime AI Lab recovery surface is unavailable.";
                return false;
            }

            public bool TryDismissTopModalForVisibilityToggle(
                out bool dismissed,
                out string diagnostic)
            {
                dismissed = false;
                diagnostic = string.Empty;
                if (_playerDeckHost == null || !_playerDeckHost.IsOpen)
                    return true;
                return _playerDeckHost.TryDismissTopModalForVisibilityToggle(
                    out dismissed,
                    out diagnostic);
            }

            public void UnmountAll()
            {
                _playerDeckHost?.CloseFromShell(_mountLease);
                _labHost?.UnmountShell(_mountLease);
            }
        }
    }
}
