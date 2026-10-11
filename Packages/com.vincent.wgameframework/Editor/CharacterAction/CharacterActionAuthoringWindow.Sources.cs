using System;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private const string DefaultCharacterActionProfileSourceDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Profiles/codex_flow_test";
        private const string DefaultCharacterActionSetSourceDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/codex_flow_test";
        private const string DefaultCharacterActionLibrarySourceDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common";
        private const string RetiredCharacterTestProfileSourceDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/Profiles/character_test";
        private const string RetiredCharacterTestActionSetSourceDirectory =
            "Assets/MxFrameworkGenerated/CharacterActions/ActionSets/character_test";

        private void InitializeModelFromSources()
        {
            _model = CharacterActionAuthoringModel.CreateDefault();
            _referenceDiscovery = null;
            _undoHistory.Clear();
            _redoHistory.Clear();

            int profileIndex = FindPreferredSourceIndex(
                _profileSources,
                _profileSourceDirectory,
                DefaultCharacterActionProfileSourceDirectory);
            bool loadedProfile = profileIndex >= 0 && LoadProfileSource(profileIndex, confirmDiscard: false, rebuild: false);
            if (loadedProfile)
            {
                _profileDirty = false;
                _actionSetDirty = string.IsNullOrWhiteSpace(_actionSetSourceDirectory);
                _actionLibraryDirty = string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory);
                _validationDirty = true;
                return;
            }

            int actionSetIndex = FindPreferredSourceIndex(
                _actionSetSources,
                _actionSetSourceDirectory,
                DefaultCharacterActionSetSourceDirectory);
            int actionLibraryIndex = FindPreferredSourceIndex(
                _actionLibrarySources,
                _actionLibrarySourceDirectory,
                DefaultCharacterActionLibrarySourceDirectory);
            bool loadedActionSet = actionSetIndex >= 0 && LoadActionSetSource(actionSetIndex, confirmDiscard: false, rebuild: false);
            bool loadedActionLibrary = actionLibraryIndex >= 0 && LoadActionLibrarySource(actionLibraryIndex, confirmDiscard: false, rebuild: false);
            if (loadedActionSet || loadedActionLibrary)
            {
                _profileDirty = true;
                _actionSetDirty = !loadedActionSet;
                _actionLibraryDirty = !loadedActionLibrary;
                _validationDirty = true;
                return;
            }

            _profileSourceDirectory = string.Empty;
            _actionSetSourceDirectory = string.Empty;
            _actionLibrarySourceDirectory = string.Empty;
            _profileDirty = true;
            _actionSetDirty = true;
            _actionLibraryDirty = true;
            _validationDirty = true;
        }

        private void RefreshSources(
            string preferredProfileDirectory,
            string preferredActionSetDirectory,
            string preferredActionLibraryDirectory)
        {
            RefreshSourceCatalogs();
            preferredProfileDirectory = NormalizePreferredSourceDirectory(
                preferredProfileDirectory,
                DefaultCharacterActionProfileSourceDirectory);
            preferredActionSetDirectory = NormalizePreferredSourceDirectory(
                preferredActionSetDirectory,
                DefaultCharacterActionSetSourceDirectory);
            if (!string.IsNullOrWhiteSpace(preferredProfileDirectory))
                _profileSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(preferredProfileDirectory);
            if (!string.IsNullOrWhiteSpace(preferredActionSetDirectory))
                _actionSetSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(preferredActionSetDirectory);
            if (!string.IsNullOrWhiteSpace(preferredActionLibraryDirectory))
                _actionLibrarySourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(preferredActionLibraryDirectory);
        }

        private void RefreshSourceCatalogs()
        {
            _profileSources = CharacterActionAuthoringSourceCatalog.DiscoverProfileSources();
            _actionSetSources = CharacterActionAuthoringSourceCatalog.DiscoverActionSetSources();
            _actionLibrarySources = CharacterActionAuthoringSourceCatalog.DiscoverActionLibrarySources();
        }

        private static int FindPreferredSourceIndex(
            CharacterActionAuthoringSourceEntry[] sources,
            string preferredDirectory,
            string defaultDirectory)
        {
            int index = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(sources, preferredDirectory);
            if (index >= 0)
                return index;

            index = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(sources, defaultDirectory);
            if (index >= 0)
                return index;

            return sources != null && sources.Length > 0 ? 0 : -1;
        }

        private static string NormalizePreferredSourceDirectory(string directory, string defaultDirectory)
        {
            directory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            if (string.IsNullOrWhiteSpace(directory))
                return string.Empty;
            if (IsRetiredCharacterTestSourceDirectory(directory))
                return defaultDirectory;
            return directory;
        }

        private static string NormalizeRequestedSourceDirectory(string directory, string defaultDirectory)
        {
            string normalized = NormalizePreferredSourceDirectory(directory, defaultDirectory);
            return string.IsNullOrWhiteSpace(normalized) ? defaultDirectory : normalized;
        }

        private static bool IsRetiredCharacterTestSourceDirectory(string directory)
        {
            return string.Equals(directory, RetiredCharacterTestProfileSourceDirectory, StringComparison.Ordinal)
                || string.Equals(directory, RetiredCharacterTestActionSetSourceDirectory, StringComparison.Ordinal);
        }

        private void NewConfig()
        {
            if (!ConfirmDiscardUnsavedChanges())
                return;

            _documentSession = null;
            _model = CharacterActionAuthoringModel.CreateDefault();
            _profileSourceDirectory = string.Empty;
            _actionSetSourceDirectory = string.Empty;
            _actionLibrarySourceDirectory = string.Empty;
            _profileDirty = true;
            _actionSetDirty = true;
            _actionLibraryDirty = true;
            _validationDirty = true;
            _referenceDiscovery = null;
            _undoHistory.Clear();
            _redoHistory.Clear();
            Rebuild();
        }

        private bool LoadProfileSource(
            int index,
            bool confirmDiscard,
            bool rebuild = true,
            bool showErrorDialog = true)
        {
            if (index < 0 || index >= _profileSources.Length)
                return false;

            CharacterActionAuthoringSourceEntry source = _profileSources[index];
            if (!TryStageProfileSource(source, out CharacterActionAuthoringDocumentSession staged, out string error))
            {
                if (showErrorDialog)
                    EditorUtility.DisplayDialog("加载 Profile 失败", error, "OK");
                return false;
            }

            bool hadDirtyState = confirmDiscard && IsAnyDirty();
            if (hadDirtyState && !ConfirmDocumentGraphSwitch())
            {
                if (rebuild)
                    Rebuild();
                return false;
            }

            // The save branch can update any document in the source graph after the
            // initial staging pass. Re-stage only when the old session still exists;
            // the discard branch intentionally adopts the already staged disk baseline.
            if (hadDirtyState && _documentSession != null
                && !TryStageProfileSource(source, out staged, out error))
            {
                if (showErrorDialog)
                    EditorUtility.DisplayDialog("加载 Profile 失败", error, "OK");
                return false;
            }

            AdoptDocumentSession(staged);
            _profileSourceDirectory = source.DirectoryPath;
            _profileDirty = false;
            _validationDirty = true;
            _referenceDiscovery = null;
            if (rebuild)
                Rebuild();
            return true;
        }

        private bool TryStageProfileSource(
            CharacterActionAuthoringSourceEntry source,
            out CharacterActionAuthoringDocumentSession staged,
            out string error)
        {
            staged = null;
            error = string.Empty;
            try
            {
                CharacterActionAuthoringImportResult import =
                    CharacterActionAuthoringSourceCatalog.LoadProfile(source);
                if (!import.Success)
                {
                    error = import.Validation?.DiagnosticsText ?? "Profile validation failed.";
                    return false;
                }

                return TryCreateDocumentSession(
                    source,
                    import.Model?.Profile?.StableId,
                    out staged,
                    out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void LoadProfileReferencedSources(bool rebuild)
        {
            string actionSetId = FirstOrEmpty(_model?.Profile?.ActionSetIds);
            if (!string.IsNullOrWhiteSpace(actionSetId))
            {
                int index = CharacterActionAuthoringSourceCatalog.FindActionSetIndexByStableId(_actionSetSources, actionSetId);
                if (index >= 0)
                    LoadActionSetSource(index, confirmDiscard: false, rebuild: false);
            }

            string libraryId = FirstOrEmpty(_model?.Profile?.ActionLibraryIds);
            if (!string.IsNullOrWhiteSpace(libraryId))
            {
                int index = CharacterActionAuthoringSourceCatalog.FindActionLibraryIndexById(_actionLibrarySources, libraryId);
                if (index >= 0)
                    LoadActionLibrarySource(index, confirmDiscard: false, rebuild: false);
            }

            if (rebuild)
                Rebuild();
        }

        private bool LoadActionSetSource(int index, bool confirmDiscard, bool rebuild = true)
        {
            if (index < 0 || index >= _actionSetSources.Length)
                return false;
            if (confirmDiscard && IsAnyDirty() && !ConfirmDiscardUnsavedChanges())
            {
                if (rebuild)
                    Rebuild();
                return false;
            }

            CharacterActionAuthoringSourceEntry source = _actionSetSources[index];
            try
            {
                CharacterActionAuthoringImportResult import = CharacterActionAuthoringSourceCatalog.LoadActionSet(source);
                if (!import.Success)
                {
                    EditorUtility.DisplayDialog("加载 ActionSet 失败", import.Validation.DiagnosticsText, "OK");
                    return false;
                }

                _model = _model ?? CharacterActionAuthoringModel.CreateDefault();
                _model.ActionSet = import.Model.ActionSet;
                _model.ReactionProfiles.Clear();
                _model.ReactionProfiles.AddRange(import.Model.ReactionProfiles);
                _model.Normalize();
                _actionSetSourceDirectory = source.DirectoryPath;
                _actionSetDirty = false;
                _validationDirty = true;
                _referenceDiscovery = null;
                if (rebuild)
                    Rebuild();
                return true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("加载 ActionSet 失败", ex.Message, "OK");
                return false;
            }
        }

        private bool LoadActionLibrarySource(int index, bool confirmDiscard, bool rebuild = true)
        {
            if (index < 0 || index >= _actionLibrarySources.Length)
                return false;
            if (confirmDiscard && IsAnyDirty() && !ConfirmDiscardUnsavedChanges())
            {
                if (rebuild)
                    Rebuild();
                return false;
            }

            CharacterActionAuthoringSourceEntry source = _actionLibrarySources[index];
            try
            {
                CharacterActionAuthoringImportResult import = CharacterActionAuthoringSourceCatalog.LoadActionLibrary(source);
                if (!import.Success)
                {
                    EditorUtility.DisplayDialog("加载 Action Library 失败", import.Validation.DiagnosticsText, "OK");
                    return false;
                }

                _model = _model ?? CharacterActionAuthoringModel.CreateDefault();
                _model.Actions.Clear();
                _model.Actions.AddRange(import.Model.Actions);
                _model.SelectedActionIndex = 0;
                _model.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateFromModel(_model);
                _model.Normalize();
                _actionLibrarySourceDirectory = source.DirectoryPath;
                _actionLibraryDirty = false;
                _validationDirty = true;
                _referenceDiscovery = null;
                if (rebuild)
                    Rebuild();
                return true;
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("加载 Action Library 失败", ex.Message, "OK");
                return false;
            }
        }

        private static bool ContainsId(string[] ids, string id)
        {
            ids = ids ?? Array.Empty<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string[] AppendId(string[] ids, string id)
        {
            ids = ids ?? Array.Empty<string>();
            var result = new string[ids.Length + 1];
            Array.Copy(ids, result, ids.Length);
            result[result.Length - 1] = id;
            return result;
        }

        private static string[] RemoveId(string[] ids, string id)
        {
            ids = ids ?? Array.Empty<string>();
            var result = new System.Collections.Generic.List<string>(ids.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.Equals(ids[i], id, StringComparison.Ordinal))
                    result.Add(ids[i]);
            }

            return result.ToArray();
        }

        private static string FirstOrEmpty(string[] values)
        {
            return values == null || values.Length == 0 ? string.Empty : values[0] ?? string.Empty;
        }
    }
}
