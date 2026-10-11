using System;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private CharacterActionAuthoringDocumentSession _documentSession;
        private Func<bool> _documentGraphSwitchConfirmationOverride = null;
        private Action<string, string> _documentGraphSwitchErrorOverride = null;

        private void ShowDocumentGraphSwitchError(string title, string message)
        {
            if (_documentGraphSwitchErrorOverride != null)
            {
                _documentGraphSwitchErrorOverride(title ?? string.Empty, message ?? string.Empty);
                return;
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private bool ConfirmDocumentGraphSwitch()
        {
            return _documentGraphSwitchConfirmationOverride?.Invoke()
                ?? ConfirmDiscardUnsavedChanges();
        }

        private bool TryLoadDocumentSession(
            CharacterActionAuthoringSourceEntry profileSource,
            string selectedProfileStableId = null)
        {
            if (!TryCreateDocumentSession(
                    profileSource,
                    selectedProfileStableId,
                    out CharacterActionAuthoringDocumentSession loaded,
                    out string error))
            {
                Debug.LogWarning("加载完整 Character Action 文档会话失败: " + error);
                return false;
            }

            AdoptDocumentSession(loaded);
            return true;
        }

        private bool TryCreateDocumentSession(
            CharacterActionAuthoringSourceEntry profileSource,
            string selectedProfileStableId,
            out CharacterActionAuthoringDocumentSession loaded,
            out string error)
        {
            loaded = null;
            error = string.Empty;
            if (profileSource == null || !profileSource.HasProfile)
            {
                error = "Profile source is missing character_action_profile.json.";
                return false;
            }

            try
            {
                loaded = CharacterActionAuthoringDocumentSession.Load(
                    profileSource,
                    _actionSetSources,
                    _actionLibrarySources,
                    selectedProfileStableId);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                loaded = null;
                return false;
            }
        }

        private void AdoptDocumentSession(CharacterActionAuthoringDocumentSession loaded)
        {
            if (loaded == null)
                throw new ArgumentNullException(nameof(loaded));

            _documentSession = loaded;
            TouchWorkbenchPreviewRevision();
            SyncModelFromDocumentSession(preserveReferenceContext: false);
            CaptureCommonSourceBaseline();
            _referenceDiscovery = null;
            _undoHistory.Clear();
            _redoHistory.Clear();
            _validationDirty = true;
            RefreshLegacyDirtyFlagsFromDocumentSession();
        }

        private void SyncModelFromDocumentSession(bool preserveReferenceContext = true)
        {
            if (_documentSession == null)
                return;

            CharacterActionAuthoringReferenceContext referenceContext = preserveReferenceContext
                ? _model?.ReferenceContext
                : null;
            var model = new CharacterActionAuthoringModel
            {
                Profile = _documentSession.CurrentProfile ?? new CharacterActionProfileJsonDto(),
                ActionSet = _documentSession.CurrentActionSet ?? new CharacterActionSetJsonDto(),
                ReferenceContext = referenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault(),
            };

            CharacterActionAuthoringActionLibraryDocument library = _documentSession.CurrentActionLibrary;
            CharacterActionJsonDto[] actions = library?.Document.Actions ?? Array.Empty<CharacterActionJsonDto>();
            model.Actions.AddRange(actions);

            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                CharacterReactionProfileJsonDto[] profiles =
                    _documentSession.ActionSetBundles[i].ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                model.ReactionProfiles.AddRange(profiles);
                CharacterLandingProfileJsonDto[] landingProfiles =
                    _documentSession.ActionSetBundles[i].LandingProfilesDocument.LandingProfiles
                    ?? Array.Empty<CharacterLandingProfileJsonDto>();
                model.LandingProfiles.AddRange(landingProfiles);
            }

            int selectedActionId = _documentSession.Selection.ActionId;
            for (int i = 0; i < model.Actions.Count; i++)
            {
                if (model.Actions[i]?.ActionId != selectedActionId)
                    continue;
                model.SelectedActionIndex = i;
                break;
            }

            model.ReferenceContext.MergeFromModel(model);
            model.Normalize();
            _model = model;
            _profileSourceDirectory = _documentSession.ProfileDocument.Source.DirectoryPath;
            CharacterActionAuthoringActionSetBundleDocument bundle =
                _documentSession.FindActionSetBundle(_documentSession.Selection.ActionSetStableId);
            _actionSetSourceDirectory = bundle?.Source.DirectoryPath ?? string.Empty;
            _actionLibrarySourceDirectory = library?.Source.DirectoryPath ?? string.Empty;
            _pendingTimelineSelectedItemId = _documentSession.Selection.TimelineItemStableId;
        }

        private bool SelectDocumentProfile(string stableId)
        {
            if (_documentSession == null)
                return false;
            if (string.Equals(
                    _documentSession.Selection.ProfileStableId,
                    stableId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            CharacterActionAuthoringSourceEntry profileSource = _documentSession.ProfileDocument.Source;
            if (!TryCreateDocumentSession(
                    profileSource,
                    stableId,
                    out CharacterActionAuthoringDocumentSession staged,
                    out _)
                || !string.Equals(staged.Selection.ProfileStableId, stableId, StringComparison.Ordinal))
            {
                return false;
            }

            bool hadDirtyState = IsAnyDirty();
            if (hadDirtyState && !ConfirmDocumentGraphSwitch())
                return false;

            // Saving can change one or more files in the graph after the first staging pass.
            // Reload only for that branch; the discard branch intentionally adopts the
            // already staged on-disk baseline after ConfirmDiscardUnsavedChanges cleared
            // the previous in-memory drafts.
            if (hadDirtyState && _documentSession != null)
            {
                if (!TryCreateDocumentSession(
                        profileSource,
                        stableId,
                        out staged,
                        out _)
                    || !string.Equals(staged.Selection.ProfileStableId, stableId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            ExitSceneEditMode(restoreEmbeddedPreview: true);
            AdoptDocumentSession(staged);
            _timelinePlayback?.Pause();
            DisposeTimelinePreviewSession();
            RefreshUi(UiRefreshScope.All);
            return true;
        }

        private bool SelectDocumentActionSet(string stableId)
        {
            if (_documentSession == null)
                return false;
            ExitSceneEditMode(restoreEmbeddedPreview: true);
            if (!_documentSession.SelectActionSet(stableId))
                return false;
            TouchWorkbenchPreviewRevision();
            _timelinePlayback?.Pause();
            DisposeTimelinePreviewSession();
            SyncModelFromDocumentSession();
            RefreshUi(UiRefreshScope.All);
            return true;
        }

        private bool SelectDocumentAction(int actionId)
        {
            if (_documentSession == null)
                return false;
            string previousLibrary = _documentSession.Selection.ActionLibraryId;
            ExitSceneEditMode(restoreEmbeddedPreview: true);
            if (!_documentSession.SelectAction(actionId))
                return false;
            // Selection does not change the document graph validated for preview.
            // The runtime preview tracks the selected action id separately.
            if (!string.Equals(
                previousLibrary,
                _documentSession.Selection.ActionLibraryId,
                StringComparison.Ordinal))
            {
                _undoHistory.Clear();
                _redoHistory.Clear();
            }
            _timelinePlayback?.Pause();
            _documentSession.SelectTimelineItem(string.Empty);
            SyncModelFromDocumentSession();
            _pendingTimelineSelectedItemId = string.Empty;
            return true;
        }

        private bool SelectDocumentActionLibrary(string libraryId)
        {
            if (_documentSession == null)
                return false;
            ExitSceneEditMode(restoreEmbeddedPreview: true);
            if (!_documentSession.SelectActionLibrary(libraryId))
                return false;
            TouchWorkbenchPreviewRevision();
            _timelinePlayback?.Pause();
            DisposeTimelinePreviewSession();
            _documentSession.SelectTimelineItem(string.Empty);
            SyncModelFromDocumentSession();
            _pendingTimelineSelectedItemId = string.Empty;
            RefreshUi(UiRefreshScope.All);
            return true;
        }

        private void ApplyHistoryModelToDocumentSession(
            CharacterActionAuthoringModel restoredModel,
            CharacterActionAuthoringModel currentModel = null)
        {
            if (_documentSession == null || restoredModel == null)
                return;

            CharacterActionProfileJsonDto restoredProfile = restoredModel.Profile;
            CharacterActionProfileJsonDto currentProfile = currentModel?.Profile;
            CharacterActionProfileJsonDto[] profiles =
                _documentSession.ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
            int profileIndex = FindDtoIndexByIdentity(
                profiles,
                currentProfile,
                restoredProfile?.StableId,
                profile => profile?.StableId);
            if (profileIndex >= 0)
            {
                profiles[profileIndex] = restoredProfile;
                _documentSession.ProfileDocument.State.MarkChanged();
            }

            CharacterActionSetJsonDto restoredSet = restoredModel.ActionSet;
            CharacterActionSetJsonDto currentSet = currentModel?.ActionSet;
            CharacterActionAuthoringActionSetBundleDocument restoredSetBundle = null;
            int restoredSetIndex = -1;
            if (TryFindActionSetSlot(
                currentSet,
                restoredSet?.StableId,
                out restoredSetBundle,
                out restoredSetIndex))
            {
                restoredSetBundle.ActionSetsDocument.ActionSets[restoredSetIndex] = restoredSet;
                restoredSetBundle.State.MarkChanged();
            }

            CharacterActionAuthoringActionLibraryDocument library =
                FindActionLibraryByIdentity(currentModel) ?? _documentSession.CurrentActionLibrary;
            if (library != null)
            {
                library.Document.Actions = restoredModel.Actions.ToArray();
                library.State.MarkChanged();
            }

            int reactionCount = restoredModel.ReactionProfiles.Count;
            for (int i = 0; i < reactionCount; i++)
            {
                CharacterReactionProfileJsonDto currentReaction =
                    currentModel != null && i < currentModel.ReactionProfiles.Count
                        ? currentModel.ReactionProfiles[i]
                        : null;
                CharacterReactionProfileJsonDto restoredReaction = restoredModel.ReactionProfiles[i];
                if (TryFindReactionSlot(
                    currentReaction,
                    restoredReaction?.StableId,
                    out CharacterActionAuthoringActionSetBundleDocument reactionBundle,
                    out int reactionIndex))
                {
                    reactionBundle.ReactionProfilesDocument.ReactionProfiles[reactionIndex] = restoredReaction;
                    reactionBundle.State.MarkChanged();
                }
            }

            if (profileIndex >= 0)
                _documentSession.SelectProfile(restoredProfile?.StableId);
            if (restoredSetBundle != null && restoredSetIndex >= 0)
            {
                // A stable-id edit does not implicitly mutate Profile membership. Keep
                // the edited DTO selected while validation reports the stale reference;
                // SelectActionSet would reject the temporary mismatch.
                _documentSession.Selection.ActionSetStableId = restoredSet?.StableId ?? string.Empty;
            }
            int selectedAction = restoredModel.SelectedAction?.ActionId ?? 0;
            if (selectedAction > 0)
                _documentSession.SelectAction(selectedAction);
            _model = restoredModel;
            RefreshLegacyDirtyFlagsFromDocumentSession();
        }

        private CharacterActionAuthoringActionLibraryDocument FindActionLibraryByIdentity(
            CharacterActionAuthoringModel currentModel)
        {
            if (currentModel == null || currentModel.Actions.Count == 0)
                return null;

            for (int i = 0; i < _documentSession.ActionLibraries.Count; i++)
            {
                CharacterActionJsonDto[] actions =
                    _documentSession.ActionLibraries[i].Document.Actions
                    ?? Array.Empty<CharacterActionJsonDto>();
                for (int j = 0; j < actions.Length; j++)
                {
                    for (int k = 0; k < currentModel.Actions.Count; k++)
                    {
                        if (ReferenceEquals(actions[j], currentModel.Actions[k]))
                            return _documentSession.ActionLibraries[i];
                    }
                }
            }

            return null;
        }

        private bool TryFindActionSetSlot(
            CharacterActionSetJsonDto current,
            string restoredStableId,
            out CharacterActionAuthoringActionSetBundleDocument bundle,
            out int index)
        {
            bundle = null;
            index = -1;

            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                CharacterActionSetJsonDto[] values =
                    _documentSession.ActionSetBundles[i].ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int j = 0; j < values.Length; j++)
                {
                    if (!ReferenceEquals(values[j], current))
                        continue;
                    bundle = _documentSession.ActionSetBundles[i];
                    index = j;
                    return true;
                }
            }

            string fallbackId = current?.StableId ?? restoredStableId ?? string.Empty;
            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                CharacterActionSetJsonDto[] values =
                    _documentSession.ActionSetBundles[i].ActionSetsDocument.ActionSets
                    ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int j = 0; j < values.Length; j++)
                {
                    if (!string.Equals(values[j]?.StableId, fallbackId, StringComparison.Ordinal))
                        continue;
                    if (bundle != null)
                    {
                        bundle = null;
                        index = -1;
                        return false;
                    }
                    bundle = _documentSession.ActionSetBundles[i];
                    index = j;
                }
            }

            return bundle != null;
        }

        private bool TryFindReactionSlot(
            CharacterReactionProfileJsonDto current,
            string restoredStableId,
            out CharacterActionAuthoringActionSetBundleDocument bundle,
            out int index)
        {
            bundle = null;
            index = -1;

            // Reference identity disambiguates equal stable ids in independent bundles.
            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                CharacterReactionProfileJsonDto[] values =
                    _documentSession.ActionSetBundles[i].ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                for (int j = 0; j < values.Length; j++)
                {
                    if (!ReferenceEquals(values[j], current))
                        continue;
                    bundle = _documentSession.ActionSetBundles[i];
                    index = j;
                    return true;
                }
            }

            // Lifecycle draft restoration has no live identity. Only accept a globally
            // unique id fallback; never guess between two same-id bundle entries.
            string fallbackId = current?.StableId ?? restoredStableId ?? string.Empty;
            for (int i = 0; i < _documentSession.ActionSetBundles.Count; i++)
            {
                CharacterReactionProfileJsonDto[] values =
                    _documentSession.ActionSetBundles[i].ReactionProfilesDocument.ReactionProfiles
                    ?? Array.Empty<CharacterReactionProfileJsonDto>();
                for (int j = 0; j < values.Length; j++)
                {
                    if (!string.Equals(values[j]?.StableId, fallbackId, StringComparison.Ordinal))
                        continue;
                    if (bundle != null)
                    {
                        bundle = null;
                        index = -1;
                        return false;
                    }
                    bundle = _documentSession.ActionSetBundles[i];
                    index = j;
                }
            }

            return bundle != null;
        }

        private static int FindDtoIndexByIdentity<T>(
            T[] values,
            T current,
            string restoredStableId,
            Func<T, string> stableIdSelector)
            where T : class
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (ReferenceEquals(values[i], current))
                    return i;
            }

            string currentStableId = current == null ? string.Empty : stableIdSelector(current);
            string fallbackId = string.IsNullOrWhiteSpace(currentStableId)
                ? restoredStableId ?? string.Empty
                : currentStableId;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(stableIdSelector(values[i]), fallbackId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private void MarkDocumentSessionChangedForCurrentContext(bool refreshDirtyFlags = true)
        {
            if (_documentSession == null)
                return;

            switch (_workspace)
            {
                case AuthoringWorkspace.ActionComposition:
                    if (_documentSession.CurrentActionLibrary != null && _model != null)
                    {
                        _documentSession.CurrentActionLibrary.Document.Actions = _model.Actions.ToArray();
                        int selectedId = _model.SelectedAction?.ActionId ?? 0;
                        if (selectedId > 0)
                            _documentSession.SelectAction(selectedId);
                    }
                    _documentSession.MarkCurrentActionChanged();
                    break;
                case AuthoringWorkspace.Mappings:
                    if (_mappingSection == MappingWorkspaceSection.ProfileComposition)
                    {
                        _documentSession.MarkCurrentProfileChanged(_model?.Profile);
                        SyncModelFromDocumentSession();
                    }
                    else if (_mappingSection != MappingWorkspaceSection.ReferenceSources
                        && _mappingSection != MappingWorkspaceSection.Compatibility)
                        _documentSession.MarkCurrentActionSetChanged(_model?.ActionSet);
                    break;
            }
            if (refreshDirtyFlags)
                RefreshLegacyDirtyFlagsFromDocumentSession();
        }

        private void ApplyActionSetDocumentChange(Action mutate, UiRefreshScope refreshScope)
        {
            using (CharacterActionPreviewBindingDiscovery.BeginReadScope())
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
            {
            PushUndoSnapshot();
            _redoHistory.Clear();
            mutate?.Invoke();
            TouchWorkbenchPreviewRevision();
            ScheduleWorkbenchValidation();
            if (_documentSession != null)
            {
                _documentSession.MarkCurrentActionSetChanged(_model?.ActionSet);
                RefreshLegacyDirtyFlagsFromDocumentSession();
            }
            else
            {
                _actionSetDirty = true;
            }
            _validationDirty = true;
            RefreshUi(refreshScope);
            }
        }

        private void RefreshLegacyDirtyFlagsFromDocumentSession()
        {
            if (_documentSession == null)
                return;
            _profileDirty = _documentSession.ProfileDocument.State.IsDirty;
            _actionSetDirty = false;
            _actionLibraryDirty = false;
            for (int i = 0; i < _documentSession.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documentSession.DocumentStates[i];
                if (!state.IsDirty)
                    continue;
                if (state.Key.Kind == CharacterActionAuthoringDocumentKind.ActionSetBundle)
                    _actionSetDirty = true;
                else if (state.Key.Kind == CharacterActionAuthoringDocumentKind.ActionLibrary)
                    _actionLibraryDirty = true;
            }
        }

        private bool IsDocumentSessionDirty()
        {
            if (_documentSession == null)
                return false;
            for (int i = 0; i < _documentSession.DocumentStates.Count; i++)
            {
                if (_documentSession.DocumentStates[i].IsDirty)
                    return true;
            }
            return false;
        }

        private bool TryValidateDocumentSession(bool includeSnapshots)
        {
            if (_documentSession == null)
                return false;
            _validation = _documentSession.Validate(
                _model?.ReferenceContext,
                buildWorkstationSnapshots: includeSnapshots);
            _validationDirty = false;
            return true;
        }

        private bool TrySaveAllDocumentSessionSources()
        {
            if (_documentSession == null)
                return false;

            TryValidateDocumentSession(includeSnapshots: true);
            if (_validation?.HasErrors == true)
            {
                _problemsExpanded = true;
                EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
                RefreshProblemsWorkbench();
                return false;
            }

            try
            {
                _documentSession.SaveAllDirty();
                RefreshLegacyDirtyFlagsFromDocumentSession();
                return true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("保存 Character Action 文档失败", ex.Message, "OK");
                _problemsExpanded = true;
                EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
                return false;
            }
        }

        private CharacterActionProfileJsonDto[] GetDocumentProfiles()
        {
            return _documentSession?.ProfileDocument.Document.Profiles
                ?? Array.Empty<CharacterActionProfileJsonDto>();
        }

        private string[] GetCurrentProfileActionSetIds()
        {
            return _documentSession?.CurrentProfile?.ActionSetIds ?? Array.Empty<string>();
        }
    }
}
