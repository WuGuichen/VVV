using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private void RegisterTriggerWorkspaceNameEditing(
            TextField field,
            string stableId,
            string committedValue)
        {
            if (field == null)
                return;

            string baseline = committedValue ?? string.Empty;
            int editRevision = 0;
            bool cancellationPending = false;
            VisualElement input =
                field.Q(TextField.textInputUssName) ?? field;

            input.RegisterCallback<KeyDownEvent>(
                evt =>
                {
                    if (evt.keyCode != KeyCode.Escape)
                        return;

                    cancellationPending = true;
                    editRevision++;
                    field.SetValueWithoutNotify(baseline);
                    field.focusController?.IgnoreEvent(evt);
                    evt.StopImmediatePropagation();
                    field.schedule.Execute(
                        () =>
                        {
                            field.SetValueWithoutNotify(baseline);
                            cancellationPending = false;
                        });
                },
                TrickleDown.TrickleDown);
            field.RegisterValueChangedCallback(
                change =>
                {
                    if (cancellationPending)
                    {
                        field.SetValueWithoutNotify(baseline);
                        return;
                    }

                    string candidate = change.newValue;
                    string previousValue = change.previousValue;
                    int commitRevision = ++editRevision;
                    field.schedule.Execute(
                        () =>
                        {
                            if (cancellationPending
                                || commitRevision != editRevision)
                            {
                                field.SetValueWithoutNotify(baseline);
                                return;
                            }

                            if (!UpdateTriggerDisplayName(
                                    stableId,
                                    candidate))
                            {
                                field.SetValueWithoutNotify(
                                    previousValue);
                                return;
                            }
                            RefreshTriggerInspectorAfterMutation();
                        });
                });
        }

        private void SetTriggerWorkspaceRuntimeInspectorVisible(
            bool visible)
        {
            if (_showTriggerWorkspaceRuntimeInspector == visible)
                return;

            _showTriggerWorkspaceRuntimeInspector = visible;
            RebuildTriggerWorkspaceEditor(preserveViewState: true);
        }

        internal void SetTriggerWorkspaceRuntimeInspectorForTests(
            bool visible)
        {
            SetTriggerWorkspaceRuntimeInspectorVisible(visible);
        }
    }
}
