using System;
using MxFramework.CharacterAction;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private bool IsRecentActionFilterActive()
        {
            return string.Equals(
                _actionCategoryFilter,
                RecentActionFilterLabel,
                StringComparison.Ordinal);
        }

        private void EnsureRecentActionHistory()
        {
            if (_recentActionHistory == null)
                _recentActionHistory = CharacterActionRecentEditHistory.ForProject(Application.dataPath);
        }

        private void CaptureCurrentActionIdentity(
            out string libraryId,
            out int actionId)
        {
            libraryId = ResolveCurrentActionLibraryId();
            actionId = _model?.SelectedAction?.ActionId ?? 0;
        }

        private void RecordCurrentActionRecentActivity(
            string previousLibraryId = null,
            int previousActionId = 0)
        {
            if (_workspace != AuthoringWorkspace.ActionComposition
                || _uiRefreshDepth > 0)
                return;

            CaptureCurrentActionIdentity(out string libraryId, out int actionId);
            if (string.IsNullOrWhiteSpace(libraryId)
                || actionId <= 0)
            {
                return;
            }

            EnsureRecentActionHistory();
            _recentActionHistory.Touch(
                libraryId,
                actionId,
                previousLibraryId,
                previousActionId);
        }

        private void RemoveRecentAction(string libraryId, int actionId)
        {
            EnsureRecentActionHistory();
            _recentActionHistory.Remove(libraryId, actionId);
        }

        private int GetRecentActionRank(string libraryId, int actionId)
        {
            EnsureRecentActionHistory();
            return _recentActionHistory.IndexOf(libraryId, actionId);
        }

        private string ResolveCurrentActionLibraryId()
        {
            string libraryId = _documentSession?.Selection.ActionLibraryId;
            if (!string.IsNullOrWhiteSpace(libraryId))
                return libraryId;

            libraryId = CharacterActionAuthoringSourceEntry.GetLeafDirectoryName(
                _actionLibrarySourceDirectory);
            if (!string.IsNullOrWhiteSpace(libraryId))
                return libraryId;

            string[] profileLibraries = _model?.Profile?.ActionLibraryIds
                ?? Array.Empty<string>();
            return profileLibraries.Length == 1
                ? profileLibraries[0] ?? string.Empty
                : string.Empty;
        }

        private void PruneUnavailableRecentActions()
        {
            if (_recentActionHistory == null || _model == null)
                return;

            _recentActionHistory.Prune(IsRecentActionAvailable);
        }

        private bool IsRecentActionAvailable(CharacterActionRecentEditEntry entry)
        {
            if (entry == null)
                return false;

            if (_documentSession != null)
            {
                for (int i = 0; i < _documentSession.ActionLibraries.Count; i++)
                {
                    CharacterActionAuthoringActionLibraryDocument library =
                        _documentSession.ActionLibraries[i];
                    if (library == null
                        || !string.Equals(
                            library.LibraryId,
                            entry.LibraryId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    CharacterActionJsonDto[] actions =
                        library.Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
                    return ContainsAction(actions, entry.ActionId);
                }

                return false;
            }

            if (!string.Equals(
                    ResolveCurrentActionLibraryId(),
                    entry.LibraryId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (_model.Actions[i]?.ActionId == entry.ActionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAction(
            CharacterActionJsonDto[] actions,
            int actionId)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i]?.ActionId == actionId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
