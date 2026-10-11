#if UNITY_EDITOR
using System;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
    {
        internal const string PopupComponentName = "RuntimeAiLabAuthoringWorkspacePicker";
        internal const string RoleRowComponentName = "RuntimeAiLabAuthoringWorkspaceRoleRow";
        internal const string Anchor = "btnAuthoringWorkspaceRole";
        internal const string Prototype = "authoringWorkspacePickerPrototype";
        internal const string Title = "title";
        internal const string Status = "statusText";
        internal const string MainRow = "roleMain";
        internal const string ExperimentARow = "roleExperimentA";
        internal const string ExperimentBRow = "roleExperimentB";
        internal const string CopyExperimentA = "btnCopyExperimentA";
        internal const string CopyExperimentB = "btnCopyExperimentB";
        internal const string ResetExperiment = "btnResetExperiment";
        internal const string ApplyExperimentToMain = "btnApplyExperimentToMain";
        internal const string ConfirmationPanel = "confirmationPanel";
        internal const string ConfirmationTitle = "confirmationTitle";
        internal const string ConfirmationSummary = "confirmationSummary";
        internal const string Confirm = "btnConfirm";
        internal const string Cancel = "btnCancel";
        internal const string Close = "btnClose";

        internal const string RowCurrentMark = "currentMark";
        internal const string RowRoleName = "roleName";
        internal const string RowRoleKind = "roleKind";
        internal const string RowState = "stateText";
        internal const string RowRevision = "revisionText";
        internal const string RowUpdated = "updatedText";
        internal const string RowSource = "sourceText";
        internal const string RowDiagnostic = "diagnosticText";

        internal static readonly string[] RequiredPopupChildren =
        {
            Title,
            Status,
            MainRow,
            ExperimentARow,
            ExperimentBRow,
            CopyExperimentA,
            CopyExperimentB,
            ResetExperiment,
            ApplyExperimentToMain,
            ConfirmationPanel,
            ConfirmationTitle,
            ConfirmationSummary,
            Confirm,
            Cancel,
            Close
        };

        internal static readonly string[] RequiredRoleRowChildren =
        {
            RowCurrentMark,
            RowRoleName,
            RowRoleKind,
            RowState,
            RowRevision,
            RowUpdated,
            RowSource,
            RowDiagnostic
        };
    }

    /// <summary>
    /// Binds the fixed authoring Role picker to a Composer-owned packaged component.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc: popup creation and explicit refresh only. The binder deliberately has no
    /// runtime-built visual fallback; a missing Composer component is reported on the anchor.
    /// </remarks>
    internal sealed class CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder :
        IDisposable
    {
        private readonly IMxUiCommandSink _commandSink;
        private readonly CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink _typedCommandSink;
        private readonly MxUiViewId _viewId;
        private readonly IMxFairyGuiPopupService _popupService;
        private Fgui.GComponent _popup;
        private Fgui.GComponent _boundRoot;
        private Fgui.GObject _anchor;
        private bool _disposed;
        private string _componentDiagnostic = string.Empty;

        internal CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder(
            IMxUiCommandSink commandSink,
            MxUiViewId viewId,
            IMxFairyGuiPopupService popupService)
        {
            _commandSink = commandSink;
            _typedCommandSink = commandSink as
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink;
            _viewId = viewId;
            _popupService = popupService;
            if (_typedCommandSink != null)
                _typedCommandSink.AuthoringWorkspacePickerCloseRequested += CloseFromCommand;
        }

        internal bool IsOpen => _popupService?.IsOpen(_popup) == true;
        internal string ComponentDiagnostic => _componentDiagnostic;

        internal void Bind(Fgui.GComponent root, bool bindPrimaryExperimentAction = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerBinder));
            if (!ReferenceEquals(_boundRoot, root))
            {
                if (IsOpen)
                    _popupService.Hide(_popup, MxFairyGuiPopupCloseReason.OwnerClosed);
                _boundRoot = root;
            }
            _anchor = root?.GetChild(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Anchor);
            if (_anchor == null)
                return;

            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel =
                _typedCommandSink?.CaptureAuthoringWorkspacePickerHeader();
            bool enabled = viewModel?.Available == true
                && _popupService != null
                && _typedCommandSink != null;
            Fgui.GButton button = _anchor.asButton;
            if (button != null)
            {
                button.enabled = enabled;
                button.grayed = !enabled;
                button.text = ActiveRoleText(viewModel) + (enabled ? " ▾" : string.Empty);
            }
            _anchor.touchable = enabled;
            _anchor.alpha = enabled ? 1f : 0.55f;
            _anchor.tooltips = enabled
                ? "选择主工作区、实验 A 或实验 B"
                : DisabledReason(viewModel);
            _anchor.onClick.Set(() =>
            {
                if (!enabled)
                    return;
                if (IsOpen)
                {
                    _popupService.Hide(_popup, MxFairyGuiPopupCloseReason.Toggle);
                    return;
                }

                if (!EnsurePopup()) { _anchor.tooltips = _componentDiagnostic; return; }
                var openedModel = _typedCommandSink.CaptureAuthoringWorkspacePickerViewModel();
                Render(openedModel);
                if (_popupService.Show(
                        _viewId,
                        _popup,
                        _anchor,
                        new MxFairyGuiPopupOptions(
                            MxFairyGuiPopupPlacement.Below,
                            safeMargin: 16f,
                            anchorGap: 8f),
                        OnPopupClosed))
                {
                    // FairyGUI applies packaged component defaults while attaching a popup.
                    // Rebind after attachment so nested Role rows cannot revert to prototype text.
                    Render(openedModel);
                    _popup.RequestFocus();
                }
            });

            if (bindPrimaryExperimentAction
                && viewModel?.Available == true
                && CharacterTestRuntimeAiLabAuthoringWorkspaceRoleContract.IsExperiment(
                    viewModel.ActiveRole))
            {
                Fgui.GButton primary = root.GetChild(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                        .PublishCurrentChanges)?.asButton;
                if (primary != null)
                {
                    // This entry opens the check/confirmation flow; it does not authorize a write.
                    bool canApply = enabled;
                    primary.text = "应用到主工作区";
                    primary.enabled = canApply;
                    primary.grayed = !canApply;
                    primary.touchable = canApply;
                    primary.alpha = canApply ? 1f : 0.55f;
                    primary.tooltips = canApply
                        ? "点击检查修改并确认是否应用到主工作区。"
                        : DisabledReason(viewModel);
                    primary.onClick.Set(() =>
                    {
                        if (!canApply)
                            return;
                        if (!IsOpen)
                            _anchor.onClick.Call();
                        EnqueueAndRefresh(
                            CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands
                                .ApplyExperimentToMain(
                                    _viewId,
                                    viewModel.ActiveRole));
                    });
                }
            }

            // An open popup keeps its explicitly captured snapshot. Commands refresh it after validation;
            // ordinary battle-frame/UI refreshes must never reload workspaces or calculate experiment diffs.
        }

        internal bool EnsurePopup()
        {
            if (_popup != null
                && _popup.displayObject != null
                && !_popup.displayObject.isDisposed)
            {
                return true;
            }

            // The screen owns a hidden prototype only as a published-package contract slot.
            // Never promote that child to a live popup: Bind() runs before the user opens the
            // picker, so making the prototype visible leaks a second UI layer over every
            // workspace (including CharacterAction authoring) and leaves its packaged sample
            // row text on screen. A live popup must always be a detached component instance.
            _popup = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                        .PopupComponentName)?.asCom;
            if (_popup == null)
            {
                _componentDiagnostic = "缺少 Composer 组件 "
                    + CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                        .PopupComponentName
                    + "；Workspace Picker 尚未发布。";
                return false;
            }

            if (!ValidateComponentContract(_popup, out _componentDiagnostic))
            {
                _popup.Dispose();
                _popup = null;
                return false;
            }

            _componentDiagnostic = string.Empty;
            _popup.name = "authoringWorkspacePicker";
            _popup.visible = true;
            _popup.opaque = true;
            CharacterTestFairyGuiPopupInputBoundary.Install(
                _popup,
                () => _popupService?.Hide(_popup, MxFairyGuiPopupCloseReason.Cancel));
            return true;
        }

        internal static bool ValidateComponentContract(
            Fgui.GComponent popup,
            out string diagnostic)
        {
            if (popup == null)
            {
                diagnostic = "Workspace Picker popup component is null.";
                return false;
            }
            string[] popupChildren =
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                    .RequiredPopupChildren;
            for (int i = 0; i < popupChildren.Length; i++)
            {
                if (popup.GetChild(popupChildren[i]) == null)
                {
                    diagnostic = "Workspace Picker component is missing child '"
                        + popupChildren[i] + "'.";
                    return false;
                }
            }

            string[] textChildren =
            {
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Title,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Status,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationTitle,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationSummary
            };
            for (int i = 0; i < textChildren.Length; i++)
            {
                if (popup.GetChild(textChildren[i])?.asTextField == null)
                {
                    diagnostic = "Workspace Picker child '" + textChildren[i]
                        + "' must be a Text node.";
                    return false;
                }
            }
            string[] buttonChildren =
            {
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentA,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentB,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ResetExperiment,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ApplyExperimentToMain,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Confirm,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Cancel,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Close
            };
            for (int i = 0; i < buttonChildren.Length; i++)
            {
                if (popup.GetChild(buttonChildren[i])?.asButton == null)
                {
                    diagnostic = "Workspace Picker child '" + buttonChildren[i]
                        + "' must be a Button component instance.";
                    return false;
                }
            }

            string[] roleRows =
            {
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.MainRow,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentARow,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentBRow
            };
            for (int rowIndex = 0; rowIndex < roleRows.Length; rowIndex++)
            {
                Fgui.GComponent row = popup.GetChild(roleRows[rowIndex])?.asCom;
                if (row == null)
                {
                    diagnostic = "Workspace Picker child '" + roleRows[rowIndex]
                        + "' must be a "
                        + CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                            .RoleRowComponentName
                        + " component.";
                    return false;
                }
                string[] rowChildren =
                    CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract
                        .RequiredRoleRowChildren;
                for (int childIndex = 0; childIndex < rowChildren.Length; childIndex++)
                {
                    if (row.GetChild(rowChildren[childIndex]) == null)
                    {
                        diagnostic = "Workspace Role row '" + roleRows[rowIndex]
                            + "' is missing child '" + rowChildren[childIndex] + "'.";
                        return false;
                    }
                }
                for (int childIndex = 1; childIndex < rowChildren.Length; childIndex++)
                {
                    if (row.GetChild(rowChildren[childIndex])?.asTextField == null)
                    {
                        diagnostic = "Workspace Role row '" + roleRows[rowIndex]
                            + "' child '" + rowChildren[childIndex]
                            + "' must be a Text node.";
                        return false;
                    }
                }
            }
            diagnostic = string.Empty;
            return true;
        }

        private void Render(CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel)
        {
            if (_popup == null || viewModel == null)
                return;
            SetText(_popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Title,
                "工作区");
            SetText(_popup, CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Status,
                string.IsNullOrEmpty(viewModel.StatusText)
                    ? viewModel.Diagnostic
                    : viewModel.StatusText);
            for (int i = 0; i < viewModel.Rows.Length && i < 3; i++)
                BindRoleRow(RoleRowAt(i), viewModel.Rows[i]);

            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentA,
                "复制主工作区到实验 A",
                viewModel.CanCopyExperimentA,
                () => EnqueueAndRefresh(
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                        _viewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA)));
            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.CopyExperimentB,
                "复制主工作区到实验 B",
                viewModel.CanCopyExperimentB,
                () => EnqueueAndRefresh(
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                        _viewId,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentB)));
            SetButtonVisibility(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ResetExperiment,
                viewModel.ShowResetExperiment);
            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ResetExperiment,
                "重置当前实验",
                viewModel.CanResetExperiment,
                () => EnqueueAndRefresh(
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ResetExperiment(
                        _viewId,
                        viewModel.ActiveRole)));
            SetButtonVisibility(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ApplyExperimentToMain,
                viewModel.ShowApplyExperimentToMain);
            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ApplyExperimentToMain,
                "应用到主工作区",
                viewModel.CanApplyExperimentToMain,
                () => EnqueueAndRefresh(
                    CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ApplyExperimentToMain(
                        _viewId,
                        viewModel.ActiveRole)),
                viewModel.ApplyDisabledReason);
            BindConfirmation(viewModel);

            Fgui.GButton close = _popup.GetChild(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Close)?.asButton;
            if (close != null)
            {
                close.text = "关闭";
                close.onClick.Set(() =>
                    _popupService?.Hide(_popup, MxFairyGuiPopupCloseReason.Toggle));
            }
        }

        private void BindRoleRow(
            Fgui.GComponent row,
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerRowViewModel viewModel)
        {
            if (row == null || viewModel == null)
                return;
            row.opaque = true;
            SetVisible(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowCurrentMark,
                viewModel.IsCurrent);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRoleName,
                viewModel.DisplayName);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRoleKind,
                viewModel.RoleKindText);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowState,
                viewModel.StateText);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowRevision,
                viewModel.RevisionText);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowUpdated,
                viewModel.UpdatedText);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowSource,
                viewModel.SourceText);
            SetText(row,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.RowDiagnostic,
                viewModel.Diagnostic);
            bool canCreate = viewModel.CanCopyFromMain;
            bool enabled = viewModel.CanSelect || canCreate;
            row.touchable = enabled;
            row.alpha = enabled || viewModel.IsCurrent ? 1f : 0.55f;
            row.tooltips = viewModel.Diagnostic;
            row.onClick.Set(() =>
            {
                if (viewModel.CanSelect)
                {
                    EnqueueAndRefresh(
                        CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Select(
                            _viewId,
                            viewModel.Role));
                }
                else if (canCreate)
                {
                    EnqueueAndRefresh(
                        CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                            _viewId,
                            viewModel.Role));
                }
            });
        }

        private void BindConfirmation(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel)
        {
            Fgui.GObject panel = _popup.GetChild(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationPanel);
            if (panel != null)
                panel.visible = viewModel.ShowConfirmation;
            SetText(
                _popup,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationTitle,
                viewModel.ConfirmationTitle);
            SetText(
                _popup,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationSummary,
                viewModel.ConfirmationSummary);
            SetVisible(
                _popup,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationTitle,
                viewModel.ShowConfirmation);
            SetVisible(
                _popup,
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ConfirmationSummary,
                viewModel.ShowConfirmation);
            SetButtonVisibility(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Confirm,
                viewModel.ShowConfirmation);
            SetButtonVisibility(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Cancel,
                viewModel.ShowConfirmation);
            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Confirm,
                viewModel.ConfirmationButtonText,
                viewModel.ShowConfirmation,
                () => EnqueueAndRefresh(ConfirmationCommand(viewModel)));
            BindActionButton(
                CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.Cancel,
                "取消",
                viewModel.ShowConfirmation,
                () =>
                {
                    _commandSink?.Enqueue(
                        CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Close(_viewId));
                    if (IsOpen)
                        Render(_typedCommandSink?.CaptureAuthoringWorkspacePickerViewModel());
                });
        }

        private MxUiCommand ConfirmationCommand(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel)
        {
            switch (viewModel.ConfirmationOperation)
            {
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.CopyMainToExperiment:
                    return CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.CopyMainToExperiment(
                        _viewId,
                        viewModel.ConfirmationRole);
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ResetExperiment:
                    return CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ResetExperiment(
                        _viewId,
                        viewModel.ConfirmationRole);
                case CharacterTestRuntimeAiLabAuthoringWorkspacePickerOperation.ApplyExperimentToMain:
                    return CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.ApplyExperimentToMain(
                        _viewId,
                        viewModel.ConfirmationRole);
                default:
                    return CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Close(_viewId);
            }
        }

        private void BindActionButton(
            string childName,
            string text,
            bool enabled,
            Action action,
            string disabledReason = "")
        {
            Fgui.GButton button = _popup?.GetChild(childName)?.asButton;
            if (button == null)
                return;
            button.text = text ?? string.Empty;
            button.enabled = enabled;
            button.grayed = !enabled;
            button.touchable = enabled;
            button.tooltips = enabled ? string.Empty : disabledReason ?? string.Empty;
            button.onClick.Set(() =>
            {
                if (enabled)
                    action?.Invoke();
            });
        }

        private void EnqueueAndRefresh(MxUiCommand command)
        {
            _commandSink?.Enqueue(command);
            if (IsOpen)
                Render(_typedCommandSink?.CaptureAuthoringWorkspacePickerViewModel());
        }

        internal static void InstallInputBoundary(Fgui.GComponent popup, Action close) =>
            CharacterTestFairyGuiPopupInputBoundary.Install(popup, close);

        private void OnPopupClosed(MxFairyGuiPopupCloseReason reason)
        {
            _commandSink?.Enqueue(
                CharacterTestRuntimeAiLabAuthoringWorkspacePickerCommands.Close(_viewId));
        }

        private void CloseFromCommand()
        {
            if (IsOpen)
                _popupService.Hide(_popup, MxFairyGuiPopupCloseReason.Toggle);
        }

        private Fgui.GComponent RoleRowAt(int index)
        {
            string childName = index == 0
                ? CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.MainRow
                : index == 1
                    ? CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentARow
                    : CharacterTestFairyGuiRuntimeAiLabAuthoringWorkspacePickerComponentContract.ExperimentBRow;
            return _popup?.GetChild(childName)?.asCom;
        }

        private void SetButtonVisibility(string childName, bool visible)
        {
            Fgui.GObject child = _popup?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetVisible(Fgui.GComponent component, string childName, bool visible)
        {
            Fgui.GObject child = component?.GetChild(childName);
            if (child != null)
                child.visible = visible;
        }

        private static void SetText(Fgui.GComponent component, string childName, string value)
        {
            Fgui.GObject child = component?.GetChild(childName);
            if (child?.asTextField != null)
                child.asTextField.text = value ?? string.Empty;
        }

        private static string ActiveRoleText(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel)
        {
            if (viewModel == null)
                return "工作区";
            return viewModel.ActiveRole == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main
                ? "主工作区"
                : viewModel.ActiveRole == CharacterTestRuntimeAiLabAuthoringWorkspaceRole.ExperimentA
                    ? "实验 A"
                    : "实验 B";
        }

        private string DisabledReason(
            CharacterTestRuntimeAiLabAuthoringWorkspacePickerViewModel viewModel)
        {
            if (_popupService == null)
                return "Workspace Popup Service 不可用。";
            if (_typedCommandSink == null)
                return "Workspace Picker 需要 Runtime AI Lab typed command sink。";
            if (_componentDiagnostic.Length > 0)
                return _componentDiagnostic;
            return viewModel?.Diagnostic ?? "Workspace Picker 不可用。";
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_typedCommandSink != null)
                _typedCommandSink.AuthoringWorkspacePickerCloseRequested -= CloseFromCommand;
            if (IsOpen)
                _popupService.Hide(_popup, MxFairyGuiPopupCloseReason.OwnerClosed);
            _popup?.Dispose();
            _popup = null;
            _boundRoot = null;
            _anchor = null;
        }
    }
}
#endif
