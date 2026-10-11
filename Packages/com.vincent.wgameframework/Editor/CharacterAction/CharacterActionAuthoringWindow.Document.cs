using System;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private Action _readOnlySourceNoticeOverride;

        private enum UiRefreshScope
        {
            Chrome = 0,
            Content = 1,
            ActionList = 2,
            ActionDetail = 3,
            Bindings = 4,
            Tracks = 5,
            All = 6,
        }

        private void ReplaceModel(CharacterActionAuthoringModel model)
        {
            PushUndoSnapshot();
            _redoHistory.Clear();
            _model = model ?? CharacterActionAuthoringModel.CreateDefault();
            MarkDirtyForCurrentTab();
            _validationDirty = true;
            RefreshUi(UiRefreshScope.All);
        }

        private void ApplyFieldChange(Action mutate, bool recordUndo = true)
        {
            if (ApplyFieldMutation(mutate, recordUndo))
                RefreshUi(ResolveFieldChangeRefreshScope());
        }

        private bool ApplyFieldMutation(Action mutate, bool recordUndo = true, bool deferStatusRefresh = false)
        {
            if (_openRequestReadOnly)
            {
                ShowReadOnlySourceNotice();
                return false;
            }
            CaptureCurrentActionIdentity(out string previousLibraryId, out int previousActionId);
            if (recordUndo)
                PushUndoSnapshot();
            mutate?.Invoke();
            RecordCurrentActionRecentActivity(previousLibraryId, previousActionId);
            MarkDirtyForCurrentTab(deferStatusRefresh);
            _validationDirty = true;
            return true;
        }

        private UiRefreshScope ResolveFieldChangeRefreshScope()
        {
            switch (_workspace)
            {
                case AuthoringWorkspace.ActionComposition:
                    // Action name/category/duration and timeline fields all have visible
                    // projections in navigation, the timeline and the context inspector.
                    return UiRefreshScope.ActionDetail;
                case AuthoringWorkspace.Mappings:
                    // Mapping inspectors project the edited DTO back into their table row;
                    // Profile composition also needs this refresh so its target-library
                    // selector follows explicit membership changes immediately.
                    return UiRefreshScope.Bindings;
                case AuthoringWorkspace.SharedResources:
                    return UiRefreshScope.Content;
                default:
                    return UiRefreshScope.Chrome;
            }
        }

        private void ApplyStructuralChange(Action mutate, UiRefreshScope scope = UiRefreshScope.Content)
        {
            if (_openRequestReadOnly)
            {
                ShowReadOnlySourceNotice();
                return;
            }
            CaptureCurrentActionIdentity(out string previousLibraryId, out int previousActionId);
            PushUndoSnapshot();
            _redoHistory.Clear();
            mutate?.Invoke();
            CharacterActionEventIdentityAuthoring.AssignMissingEventOrders(_model?.SelectedAction);
            RecordCurrentActionRecentActivity(previousLibraryId, previousActionId);
            MarkDirtyForCurrentTab();
            _validationDirty = true;
            RefreshUi(scope);
        }

        private void ShowReadOnlySourceNotice()
        {
            if (_readOnlySourceNoticeOverride != null)
            {
                _readOnlySourceNoticeOverride();
                return;
            }

            EditorUtility.DisplayDialog(
                "只读动作源",
                "当前动作来自公共 Common 库，不能在角色上下文中直接修改。请回到 CharacterStudio 使用“复制为角色专属并编辑”。",
                "OK");
        }

        private void UpdateModel(Action update)
        {
            ApplyStructuralChange(update, UiRefreshScope.Content);
        }

        private void MarkDirtyForCurrentTab(bool deferStatusRefresh = false)
        {
            TouchWorkbenchPreviewRevision();
            if (!deferStatusRefresh)
                ScheduleWorkbenchValidation();
            if (_documentSession != null)
            {
                MarkDocumentSessionChangedForCurrentContext(refreshDirtyFlags: !deferStatusRefresh);
                if (deferStatusRefresh)
                    _actionLibraryDirty = true;
                return;
            }

            switch (_tab)
            {
                case AuthoringTab.Profile:
                    _profileDirty = true;
                    break;
                case AuthoringTab.ActionSet:
                    _actionSetDirty = true;
                    break;
                case AuthoringTab.Actions:
                case AuthoringTab.Tracks:
                    _actionLibraryDirty = true;
                    break;
                case AuthoringTab.Projectiles:
                    break;
                case AuthoringTab.References:
                    _actionSetDirty = true;
                    _actionLibraryDirty = true;
                    break;
                case AuthoringTab.Validation:
                    break;
            }
        }
    }
}
