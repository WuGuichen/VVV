using System;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private static bool ActionMatchesFilter(CharacterActionJsonDto action, string filter)
        {
            if (action == null)
                return false;

            return (action.DisplayName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || ("#" + action.ActionId).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AddAction(CharacterActionJsonDto action)
        {
            if (ShouldChooseTargetLibraryForNewAction())
            {
                ShowNewActionTargetLibraryMenu(action);
                return;
            }

            AddActionToCurrentLibrary(action);
        }

        private bool ShouldChooseTargetLibraryForNewAction()
        {
            if (_model?.SelectedAction != null || _documentSession == null)
                return false;

            int resolvedLibraryCount = 0;
            string[] libraryIds = _documentSession.CurrentProfile?.ActionLibraryIds ?? Array.Empty<string>();
            for (int i = 0; i < _documentSession.ActionLibraries.Count; i++)
            {
                if (ContainsId(libraryIds, _documentSession.ActionLibraries[i].LibraryId))
                    resolvedLibraryCount++;
            }

            return resolvedLibraryCount > 1;
        }

        private void ShowNewActionTargetLibraryMenu(CharacterActionJsonDto action)
        {
            var menu = new GenericMenu();
            string[] libraryIds = _documentSession?.CurrentProfile?.ActionLibraryIds ?? Array.Empty<string>();
            for (int i = 0; i < _documentSession.ActionLibraries.Count; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = _documentSession.ActionLibraries[i];
                if (library == null || !ContainsId(libraryIds, library.LibraryId))
                    continue;

                string capturedLibraryId = library.LibraryId;
                string label = string.IsNullOrWhiteSpace(capturedLibraryId)
                    ? "未命名动作库"
                    : capturedLibraryId.Replace('/', '／');
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    if (SelectDocumentActionLibrary(capturedLibraryId))
                        AddActionToCurrentLibrary(action);
                });
            }

            menu.ShowAsContext();
        }

        private void AddActionToCurrentLibrary(CharacterActionJsonDto action)
        {
            PrepareForActionSelectionChange();
            ApplyActionCollectionChange(() =>
            {
                action = PrepareNewActionIdentity(action);
                _model.AddAction(action);
            });
        }

        private CharacterActionJsonDto PrepareNewActionIdentity(CharacterActionJsonDto action)
        {
            action = action ?? CharacterActionAuthoringModel.CreateBlankAction(
                0,
                "New Action");
            action.ActionId = AllocateFormalActionId();
            return action;
        }

        private void AddWorkbenchActionCommands(VisualElement parent)
        {
            CharacterActionJsonDto selected = _model?.SelectedAction;
            string libraryId = _documentSession?.Selection.ActionLibraryId;
            if (string.IsNullOrWhiteSpace(libraryId))
                libraryId = CharacterActionAuthoringSourceEntry.GetLeafDirectoryName(_actionLibrarySourceDirectory);
            parent.Add(new Label("所属动作库 · " + (string.IsNullOrWhiteSpace(libraryId) ? "未选择" : libraryId))
            {
                style = { fontSize = 10f, opacity = 0.72f },
            });

            var row = new VisualElement();
            row.AddToClassList("ca-workbench-action-command-row");
            var duplicate = new Button(DuplicateSelectedActionFromWorkbench)
            {
                name = "character-action-duplicate-action",
                text = "复制动作",
            };
            duplicate.SetEnabled(selected != null);
            row.Add(duplicate);
            var delete = new Button(() => DeleteSelectedActionFromWorkbench(requireConfirmation: true))
            {
                name = "character-action-delete-action",
                text = "删除动作",
            };
            delete.SetEnabled(selected != null);
            row.Add(delete);
            parent.Add(row);
        }

        private void DuplicateSelectedActionFromWorkbench()
        {
            if (_model?.SelectedAction == null)
                return;
            PrepareForActionSelectionChange();
            ApplyActionCollectionChange(() =>
                _model.DuplicateSelectedAction(AllocateFormalActionId()));
        }

        private void DeleteSelectedActionFromWorkbench(bool requireConfirmation)
        {
            CharacterActionJsonDto selected = _model?.SelectedAction;
            if (selected == null)
                return;
            if (requireConfirmation && !EditorUtility.DisplayDialog(
                    "删除动作",
                    "确定删除“" + (selected.DisplayName ?? ("Action #" + selected.ActionId)) + "”吗？该操作可以撤销。",
                    "删除",
                    "取消"))
                return;

            int retiredActionId = selected.ActionId;
            PrepareForActionSelectionChange();
            ApplyActionCollectionChange(() =>
            {
                if (_model.DeleteSelectedAction())
                    CharacterActionIdCatalog.Retire(retiredActionId);
            });
        }

        private void PrepareForActionSelectionChange()
        {
            if (_sceneEditMode)
                ExitSceneEditMode(restoreEmbeddedPreview: true);
            _timelinePlayback?.Pause();
            _pendingTimelineSelectedItemId = string.Empty;
            _documentSession?.SelectTimelineItem(string.Empty);
        }

        private void ApplyActionCollectionChange(Action mutate)
        {
            CaptureCurrentActionIdentity(out string previousLibraryId, out int previousActionId);
            int previousCount = _model?.Actions.Count ?? 0;
            PushUndoSnapshot();
            _redoHistory.Clear();
            mutate?.Invoke();
            int currentCount = _model?.Actions.Count ?? 0;
            if (currentCount < previousCount)
                RemoveRecentAction(previousLibraryId, previousActionId);
            else if (currentCount > previousCount)
                RecordCurrentActionRecentActivity();
            MarkDirtyForCurrentTab();
            if (_documentSession != null && _model?.SelectedAction == null)
                _documentSession.SelectActionLibrary(_documentSession.Selection.ActionLibraryId);
            _validationDirty = true;
            RefreshUi(UiRefreshScope.ActionList);
        }

        private int AllocateFormalActionId()
        {
            CharacterActionAuthoringActionLibraryDocument library =
                _documentSession?.CurrentActionLibrary;
            if (library == null)
                throw new InvalidOperationException("当前没有可写的 CharacterAction Library。");
            return CharacterActionIdCatalog.Allocate(
                library.LibraryId,
                library.Source.ActionsPath);
        }
    }
}
