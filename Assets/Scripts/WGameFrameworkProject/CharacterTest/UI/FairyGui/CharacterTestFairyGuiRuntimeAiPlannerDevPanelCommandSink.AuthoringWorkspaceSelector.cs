#if UNITY_EDITOR
using System;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation
            _authoringWorkspacePickerConfirmation;
        private string _authoringWorkspacePickerStatus = string.Empty;

        internal event Action AuthoringWorkspacePickerCloseRequested;

        // Toolbar binding reads only the live role identity. Full disk validation belongs to opening/operating the picker.
        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel CaptureAuthoringWorkspacePickerHeader()
            => new CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel {
                Available = _controller?.AuthoringWorkspacePickerAvailable == true,
                ActiveRole = _controller?.ActiveAuthoringWorkspaceRole ?? CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                Diagnostic = _controller?.InitialOpenResult.Message ?? "工作区尚未就绪。"
            };

        void IMxUiCommandSink.Enqueue(MxUiCommand command)
        {
            if (TryHandleAuthoringWorkspacePickerCommand(command))
                return;

            Enqueue(command);
        }

        internal CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel
            CaptureAuthoringWorkspacePickerViewModel()
        {
            return CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModelBuilder.Build(
                _controller,
                _authoringWorkspacePickerConfirmation,
                _authoringWorkspacePickerStatus);
        }

        internal static bool TryParseAuthoringWorkspaceRolePayload(
            object payload,
            out CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            if (payload is CharacterTestRuntimeAiLabAuthoringWorkspaceRole value
                && CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsValid(value))
            {
                role = value;
                return true;
            }

            role = CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main;
            return false;
        }

        private bool TryHandleAuthoringWorkspacePickerCommand(MxUiCommand command)
        {
            if (string.Equals(
                    command.CommandId,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.SelectRole,
                    StringComparison.Ordinal))
            {
                HandleAuthoringWorkspaceRoleSelection(command);
                return true;
            }
            if (string.Equals(
                    command.CommandId,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.CopyMainToExperiment,
                    StringComparison.Ordinal))
            {
                HandleAuthoringWorkspaceExperimentOperation(
                    command,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment);
                return true;
            }
            if (string.Equals(
                    command.CommandId,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ResetExperiment,
                    StringComparison.Ordinal))
            {
                HandleAuthoringWorkspaceExperimentOperation(
                    command,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment);
                return true;
            }
            if (string.Equals(
                    command.CommandId,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ApplyExperimentToMain,
                    StringComparison.Ordinal))
            {
                HandleAuthoringWorkspaceExperimentOperation(
                    command,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain);
                return true;
            }
            if (string.Equals(
                    command.CommandId,
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommandIds.ClosePicker,
                    StringComparison.Ordinal))
            {
                CommandCount++;
                LastCommand = command;
                _authoringWorkspacePickerConfirmation = default;
                _authoringWorkspacePickerStatus = string.Empty;
                return true;
            }
            return false;
        }

        private void HandleAuthoringWorkspaceRoleSelection(MxUiCommand command)
        {
            SynchronizeHistoricalPresentationState();
            CommandCount++;
            LastCommand = command;
            PendingSetupBehaviorGroupDeleteDraftId = -1;

            if (!TryParseAuthoringWorkspaceRolePayload(command.Payload, out var role))
            {
                CompleteAuthoringWorkspaceRoleCommand(
                    "工作区选择无效；请选择主工作区、实验 A 或实验 B。",
                    warning: true,
                    refresh: true);
                return;
            }
            if (_controller == null)
            {
                CompleteAuthoringWorkspaceRoleCommand(
                    "工作区切换失败：Runtime AI Lab 控制器不可用。",
                    warning: true,
                    refresh: true);
                return;
            }

            bool unchanged = _controller.HasActiveAuthoringWorkspaceRole
                && _controller.ActiveAuthoringWorkspaceRole == role
                && _controller.WorkspaceAvailable;
            AuthoringWorkspaceSelectionToken selection =
                CaptureAuthoringWorkspaceSelection();
            _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);

            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                _controller.TrySelectAuthoringWorkspaceRole(role);
            if (!result.Success)
            {
                CompleteAuthoringWorkspaceRoleCommand(
                    FailureMessage(result),
                    warning: true,
                    refresh: true);
                return;
            }
            if (unchanged)
            {
                _authoringWorkspacePickerConfirmation = default;
                CompleteAuthoringWorkspaceRoleCommand(
                    DisplayName(role) + "已经是当前工作区。",
                    warning: false,
                    refresh: false);
                AuthoringWorkspacePickerCloseRequested?.Invoke();
                return;
            }

            bool stableContextRestored = RestoreAuthoringWorkspaceSelection(
                selection,
                out string restoreDiagnostic);
            string message = "已切换到" + DisplayName(role) + "。";
            if (!stableContextRestored && restoreDiagnostic.Length > 0)
                message += " " + restoreDiagnostic;

            LastStatusMessage = message;
            _panelStatusSink?.Invoke(message);
            bool refreshed = _refreshPanel?.Invoke() ?? false;
            if (refreshed
                && stableContextRestored
                && selection.RecipeSelection.RequiresStableRestore)
            {
                bool recipeRestored = _behaviorCardWorkspaceCommands
                    ?.TryRestoreAuthoringWorkspaceRecipeSelection(
                        selection.RecipeSelection) == true;
                if (recipeRestored)
                {
                    _refreshPanel?.Invoke();
                }
                else
                {
                    LastStatusMessage += " 原 Timeline/Event 在目标工作区中不存在，已回退到当前行为卡。";
                    _panelStatusSink?.Invoke(LastStatusMessage);
                    ShowToast(LastStatusMessage, isWarning: true);
                    _refreshPanel?.Invoke();
                }
            }
            _authoringWorkspacePickerConfirmation = default;
            AuthoringWorkspacePickerCloseRequested?.Invoke();
        }

        private void HandleAuthoringWorkspaceExperimentOperation(
            MxUiCommand command,
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation)
        {
            SynchronizeHistoricalPresentationState();
            CommandCount++;
            LastCommand = command;
            PendingSetupBehaviorGroupDeleteDraftId = -1;
            if (!TryParseAuthoringWorkspaceRolePayload(command.Payload, out var role)
                || !CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(role))
            {
                SetAuthoringWorkspacePickerStatus(
                    "实验操作无效；请选择实验 A 或实验 B。",
                    warning: true,
                    refresh: true);
                return;
            }
            if (_controller == null)
            {
                SetAuthoringWorkspacePickerStatus(
                    "实验操作失败：Runtime AI Lab 控制器不可用。",
                    warning: true,
                    refresh: true);
                return;
            }

            CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview =
                _controller.CaptureAuthoringWorkspaceExperimentPreview(role);
            if (!ValidateAuthoringWorkspaceExperimentOperation(operation, role, preview))
                return;
            if (!_authoringWorkspacePickerConfirmation.Matches(operation, role))
            {
                _authoringWorkspacePickerConfirmation =
                    new CharacterTestRuntimeAiLabAuthoringWorkspacePickerConfirmation(
                        operation,
                        role);
                SetAuthoringWorkspacePickerStatus(
                    ConfirmationPrompt(operation, role, preview),
                    warning: operation !=
                        CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment
                        || preview.Initialized,
                    refresh: true);
                return;
            }

            AuthoringWorkspaceSelectionToken selection = CaptureAuthoringWorkspaceSelection();
            _behaviorCardWorkspaceCommands?.CancelStageEditSessions(clearSelection: false);
            bool activeWorkspaceWillChange =
                operation == CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain
                || (_controller.HasActiveAuthoringWorkspaceRole
                    && _controller.ActiveAuthoringWorkspaceRole == role);
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result =
                ExecuteAuthoringWorkspaceExperimentOperation(operation, role);
            if (!result.Success)
            {
                SetAuthoringWorkspacePickerStatus(
                    ExperimentFailureMessage(operation, result),
                    warning: true,
                    refresh: true);
                return;
            }

            _authoringWorkspacePickerConfirmation = default;
            bool stableContextRestored = true;
            string restoreDiagnostic = string.Empty;
            if (activeWorkspaceWillChange)
            {
                stableContextRestored = RestoreAuthoringWorkspaceSelection(
                    selection,
                    out restoreDiagnostic);
            }
            string success = ExperimentSuccessMessage(operation, role);
            if (!stableContextRestored && restoreDiagnostic.Length > 0)
                success += " " + restoreDiagnostic;
            _authoringWorkspacePickerStatus = success;
            LastStatusMessage = success;
            _panelStatusSink?.Invoke(success);
            ShowToast(success, isWarning: !stableContextRestored);
            bool refreshed = _refreshPanel?.Invoke() ?? false;
            if (refreshed
                && activeWorkspaceWillChange
                && stableContextRestored
                && selection.RecipeSelection.RequiresStableRestore)
            {
                if (_behaviorCardWorkspaceCommands?.TryRestoreAuthoringWorkspaceRecipeSelection(
                        selection.RecipeSelection) == true)
                {
                    _refreshPanel?.Invoke();
                }
                else
                {
                    _authoringWorkspacePickerStatus +=
                        " 原 Timeline/Event 不再存在，已回退到当前行为卡。";
                    LastStatusMessage = _authoringWorkspacePickerStatus;
                    _panelStatusSink?.Invoke(LastStatusMessage);
                    ShowToast(LastStatusMessage, isWarning: true);
                    _refreshPanel?.Invoke();
                }
            }

            if (operation ==
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain)
            {
                AuthoringWorkspacePickerCloseRequested?.Invoke();
            }
        }

        private bool ValidateAuthoringWorkspaceExperimentOperation(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            in CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview)
        {
            if (!preview.Available)
            {
                SetAuthoringWorkspacePickerStatus(
                    "实验操作不可用：" + preview.Diagnostic,
                    warning: true,
                    refresh: true);
                return false;
            }
            if (operation == CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment
                && (!_controller.HasActiveAuthoringWorkspaceRole
                    || _controller.ActiveAuthoringWorkspaceRole != role
                    || !preview.Initialized))
            {
                SetAuthoringWorkspacePickerStatus(
                    "只能重置当前已创建的实验工作区。",
                    warning: true,
                    refresh: true);
                return false;
            }
            if (operation ==
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain
                && (!_controller.HasActiveAuthoringWorkspaceRole
                    || _controller.ActiveAuthoringWorkspaceRole != role
                    || !preview.Initialized))
            {
                SetAuthoringWorkspacePickerStatus(
                    "只能把当前实验工作区应用到主工作区。",
                    warning: true,
                    refresh: true);
                return false;
            }
            if (operation ==
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain
                && !preview.MainMatchesSource)
            {
                _authoringWorkspacePickerConfirmation = default;
                SetAuthoringWorkspacePickerStatus(
                    "主工作区在实验创建后已经变化，不能直接覆盖。请重新从主工作区复制到实验，或返回主工作区继续编辑。",
                    warning: true,
                    refresh: true);
                return false;
            }
            return true;
        }

        private CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult
            ExecuteAuthoringWorkspaceExperimentOperation(
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
                CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            switch (operation)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment:
                    return _controller.TryCopyMainToAuthoringWorkspaceExperiment(role);
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment:
                    return _controller.TryResetAuthoringWorkspaceExperiment(role);
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain:
                    return _controller.TryApplyAuthoringWorkspaceExperimentToMain(role);
                default:
                    return new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.InvalidRole,
                        role,
                        null,
                        "Unsupported authoring workspace experiment operation.");
            }
        }

        private void SetAuthoringWorkspacePickerStatus(
            string message,
            bool warning,
            bool refresh)
        {
            _authoringWorkspacePickerStatus = message ?? string.Empty;
            CompleteAuthoringWorkspaceRoleCommand(
                _authoringWorkspacePickerStatus,
                warning,
                refresh);
        }

        private static string ConfirmationPrompt(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role,
            in CharacterTestRuntimeAiLabAuthoringWorkspaceExperimentPreview preview)
        {
            string name = DisplayName(role);
            switch (operation)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment:
                    return preview.Initialized
                        ? "请确认覆盖" + name + "；覆盖后仍可从恢复副本找回。"
                        : "请确认从主工作区复制并创建" + name + "。";
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment:
                    return "请确认重置" + name + "；当前实验修改将被主工作区副本替换。";
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain:
                    return "请确认把" + name + "应用到主工作区。";
                default:
                    return "请确认实验操作。";
            }
        }

        private static string ExperimentSuccessMessage(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            string name = DisplayName(role);
            switch (operation)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment:
                    return "已从主工作区复制到" + name + "。";
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment:
                    return "已从主工作区重置" + name + "。";
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain:
                    return "已把" + name + "应用到主工作区，并切回主工作区。";
                default:
                    return "实验操作已完成。";
            }
        }

        private static string ExperimentFailureMessage(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation operation,
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result)
        {
            if (result.Status == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed)
                return "实验操作失败：当前修改保存失败。" + result.Message;
            if (result.Status ==
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainRevisionConflict)
            {
                return "主工作区在实验创建后已经变化，不能直接覆盖。";
            }
            return "实验操作失败（" + operation + "）：" + result.Message;
        }

        private AuthoringWorkspaceSelectionToken CaptureAuthoringWorkspaceSelection()
        {
            CharacterTestRuntimeAiLabShellState shellState =
                _controller?.ShellCoordinator?.CurrentState;
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport =
                shellState?.PageContext?.BehaviorCardViewport
                ?? CharacterTestRuntimeAiLabBehaviorCardViewport.Default;
            CharacterTestRuntimeAiLabPageContext page = null;
            _controller?.TryCaptureBehaviorCardShellContext(
                SelectedWorkbenchEntryIndex,
                SelectedLogicElementKind,
                SelectedLogicElementIndex,
                viewport,
                out page);
            if (page == null
                && !string.IsNullOrEmpty(shellState?.PageContext?.CardDraftId))
            {
                page = shellState.PageContext;
            }

            return new AuthoringWorkspaceSelectionToken(
                page,
                _controller?.SelectedAgentIndex ?? -1,
                _controller?.SelectedSetupTeamId ?? 0,
                _controller?.SelectedSetupSlotIndex ?? -1,
                _behaviorCardWorkspaceCommands
                    ?.CaptureAuthoringWorkspaceRecipeSelection() ?? default);
        }

        private bool RestoreAuthoringWorkspaceSelection(
            in AuthoringWorkspaceSelectionToken selection,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (selection.PageContext == null)
            {
                bool setupSelectionRetained =
                    _controller.SelectedSetupTeamId == selection.SelectedSetupTeamId
                    && _controller.SelectedSetupSlotIndex == selection.SelectedSetupSlotIndex
                    && _controller.SelectedAgentIndex == selection.SelectedAgentIndex;
                if (!setupSelectionRetained)
                {
                    diagnostic = "原角色选择在目标工作区中不可用，已使用目标工作区默认选择。";
                }
                return setupSelectionRetained;
            }

            if (selection.PageContext != null
                && _controller.TryResolveBehaviorCardShellContext(
                    selection.PageContext,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabLogicElementKind elementKind,
                    out int elementIndex))
            {
                SelectedWorkbenchEntryIndex = Math.Max(0, entryIndex);
                SelectedCompiledStepIndex = elementKind ==
                        CharacterTestRuntimeAiLabLogicElementKind.Stage
                    ? Math.Max(0, elementIndex)
                    : 0;
                SelectedLogicElementKind = elementIndex >= 0
                    ? elementKind
                    : CharacterTestRuntimeAiLabLogicElementKind.Stage;
                SelectedLogicElementIndex = Math.Max(0, elementIndex);
                if (!TryCommitRestoredShellContext(selection.PageContext, elementIndex >= 0))
                {
                    diagnostic = "行为卡已恢复，但 Shell 上下文更新失败。";
                    return false;
                }

                if (_controller.SelectedSetupTeamId != selection.SelectedSetupTeamId
                    || _controller.SelectedSetupSlotIndex != selection.SelectedSetupSlotIndex
                    || _controller.SelectedAgentIndex != selection.SelectedAgentIndex)
                {
                    diagnostic = "行为卡已恢复；原角色选择在目标工作区中不可用。";
                    return false;
                }
                return true;
            }

            SelectedWorkbenchEntryIndex = 0;
            SelectedCompiledStepIndex = 0;
            SelectedLogicElementKind = CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = 0;
            CharacterTestRuntimeAiLabBehaviorCardViewport viewport =
                selection.PageContext?.BehaviorCardViewport
                ?? CharacterTestRuntimeAiLabBehaviorCardViewport.Default;
            if (_controller.TryCaptureBehaviorCardShellContext(
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Stage,
                    0,
                    viewport,
                    out CharacterTestRuntimeAiLabPageContext fallback))
            {
                TryCommitRestoredShellContext(fallback, hasElementSelection: true);
                diagnostic = "原行为卡或 Timeline/Event 在目标工作区中不存在，已选择默认行为卡。";
                return false;
            }

            if (_controller.IsShellSetupPhase)
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.Setup);
            else if (TimelineMode != CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace)
                SetTimelineMode(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace);
            diagnostic = "原行为卡在目标工作区中不存在，已返回安全的默认页面。";
            return false;
        }

        private bool TryCommitRestoredShellContext(
            CharacterTestRuntimeAiLabPageContext context,
            bool hasElementSelection)
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                _controller?.ShellCoordinator;
            CharacterTestRuntimeAiLabShellState state = coordinator?.CurrentState;
            if (!CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
                    .HostsBehaviorCardContext(state))
            {
                return true;
            }

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.TryUpdateBehaviorCardContext(
                    state.Generation,
                    state.Revision,
                    context.GroupDraftId,
                    context.EntryId,
                    context.CardDraftId,
                    hasElementSelection
                        ? context.SelectedElementKind
                        : CharacterTestRuntimeAiLabSelectedElementKind.None,
                    hasElementSelection
                        ? context.SelectedElementStableId
                        : string.Empty,
                    context.BehaviorCardViewport);
            return result.Success;
        }

        private void CompleteAuthoringWorkspaceRoleCommand(
            string message,
            bool warning,
            bool refresh)
        {
            LastStatusMessage = message;
            _panelStatusSink?.Invoke(message);
            ShowToast(message, warning);
            if (refresh)
                _refreshPanel?.Invoke();
        }

        private static string FailureMessage(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult result)
        {
            switch (result.Status)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ExperimentNotInitialized:
                    return DisplayName(result.Role) + "尚未创建，请先从主工作区复制。";
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.FlushFailed:
                    return "工作区切换失败：当前修改保存失败。" + result.Message;
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable:
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.WorkspaceLoadFailed:
                case CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.ReadOnly:
                    return "工作区无法打开：" + result.Message;
                default:
                    return "工作区切换失败：" + result.Message;
            }
        }

        private static string DisplayName(
            CharacterTestRuntimeAiLabAuthoringWorkspaceRole role)
        {
            return role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                ? "主工作区"
                : role == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                    ? "实验 A"
                    : "实验 B";
        }

        private readonly struct AuthoringWorkspaceSelectionToken
        {
            internal AuthoringWorkspaceSelectionToken(
                CharacterTestRuntimeAiLabPageContext pageContext,
                int selectedAgentIndex,
                int selectedSetupTeamId,
                int selectedSetupSlotIndex,
                CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection recipeSelection)
            {
                PageContext = pageContext;
                SelectedAgentIndex = selectedAgentIndex;
                SelectedSetupTeamId = selectedSetupTeamId;
                SelectedSetupSlotIndex = selectedSetupSlotIndex;
                RecipeSelection = recipeSelection;
            }

            internal CharacterTestRuntimeAiLabPageContext PageContext { get; }
            internal int SelectedAgentIndex { get; }
            internal int SelectedSetupTeamId { get; }
            internal int SelectedSetupSlotIndex { get; }
            internal CharacterTestRuntimeAiLabAuthoringWorkspaceRecipeSelection
                RecipeSelection { get; }
        }
    }
}
#endif
