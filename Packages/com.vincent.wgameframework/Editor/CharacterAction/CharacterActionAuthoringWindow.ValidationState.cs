using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private IVisualElementScheduledItem _validationDebounceItem;

        private void ScheduleWorkbenchValidation()
        {
            if (rootVisualElement?.panel == null)
                return;

            _validationDebounceItem?.Pause();
            _validationDebounceItem = rootVisualElement.schedule.Execute(RunDebouncedWorkbenchValidation);
            _validationDebounceItem.ExecuteLater(150);
        }

        private void RunDebouncedWorkbenchValidation()
        {
            _validationDebounceItem = null;
            if (!_validationDirty || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureValidation(includeSnapshots: false, force: true);
            RefreshProblemsWorkbench();
            RefreshChrome();
        }

        private void CancelWorkbenchValidationSchedule()
        {
            _validationDebounceItem?.Pause();
            _validationDebounceItem = null;
        }

        private void EnsureValidationForCurrentTab()
        {
            if (_tab == AuthoringTab.Validation)
                EnsureValidation(includeSnapshots: true, force: _validationDirty || _validation == null);
            else if (_validation == null)
                EnsureValidation(includeSnapshots: false, force: true);
        }

        private void EnsureValidation(bool includeSnapshots, bool force)
        {
            if (!force && _validation != null)
                return;

            if (TryValidateDocumentSession(includeSnapshots))
                return;

            _validation = CharacterActionAuthoringJsonService.Validate(
                _model,
                _model?.ReferenceContext,
                includeSnapshots);
            _validationDirty = false;
        }

        private void CopyJsonToClipboard()
        {
            CharacterActionAuthoringExportResult export = _documentSession != null
                ? _documentSession.Validate(_model?.ReferenceContext, buildWorkstationSnapshots: false)
                : CharacterActionAuthoringJsonService.Validate(
                    _model,
                    _model?.ReferenceContext,
                    buildWorkstationSnapshots: false);
            _validation = export;
            _validationDirty = false;
            EditorGUIUtility.systemCopyBuffer =
                export.ActionSetsJson
                + Environment.NewLine
                + Environment.NewLine
                + export.ActionsJson;
            RefreshUi(UiRefreshScope.Chrome);
        }
    }
}
