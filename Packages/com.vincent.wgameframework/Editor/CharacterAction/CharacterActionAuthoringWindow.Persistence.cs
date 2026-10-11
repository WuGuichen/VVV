using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(_profileSourceDirectory))
            {
                EditorUtility.DisplayDialog(
                    "无法保存 Profile",
                    "当前 Profile 还没有绑定源目录。请先选择已有 Profile，或使用“另存 Profile”。",
                    "OK");
                return;
            }

            SaveProfileToDirectory(_profileSourceDirectory);
        }

        private void SaveActionSet()
        {
            if (string.IsNullOrWhiteSpace(_actionSetSourceDirectory))
            {
                EditorUtility.DisplayDialog(
                    "无法保存 ActionSet",
                    "当前 ActionSet 还没有绑定源目录。请先选择已有 ActionSet，或使用“另存 ActionSet”。",
                    "OK");
                return;
            }

            SaveActionSetToDirectory(_actionSetSourceDirectory);
        }

        private void SaveActionLibrary()
        {
            if (string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory))
            {
                EditorUtility.DisplayDialog(
                    "无法保存 Action Library",
                    "当前 Action Library 还没有绑定源目录。请先选择已有 Action Library，或使用“另存 Library”。",
                    "OK");
                return;
            }

            SaveActionLibraryToDirectory(_actionLibrarySourceDirectory);
        }

        private bool IsAnyCharacterActionSourceDirty()
        {
            return IsDocumentSessionDirty()
                || _profileDirty
                || _actionSetDirty
                || _actionLibraryDirty
                || HasPendingMechanicTuningAuthoringChanges;
        }

        private string BuildCharacterActionTimelineDirtyText()
        {
            var dirty = new List<string>();
            AppendDocumentSessionDirtySourceLabels(dirty);
            if (_profileDirty)
                AddDirtySourceLabel(dirty, "Profile");
            if (_actionSetDirty)
                AddDirtySourceLabel(dirty, "ActionSet");
            if (_actionLibraryDirty)
                AddDirtySourceLabel(dirty, "Library");
            if (HasPendingMechanicTuningAuthoringChanges)
                AddDirtySourceLabel(dirty, "Mechanic Tuning");
            if (_abilityEffectTimelineDirty)
                AddDirtySourceLabel(dirty, "Ability Timeline");
            return dirty.Count == 0 ? "Saved" : "Modified: " + string.Join("/", dirty);
        }

        private void AppendDocumentSessionDirtySourceLabels(List<string> labels)
        {
            if (_documentSession == null || labels == null)
                return;
            for (int i = 0; i < _documentSession.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documentSession.DocumentStates[i];
                if (state == null || !state.IsDirty)
                    continue;
                switch (state.Key.Kind)
                {
                    case CharacterActionAuthoringDocumentKind.Profile:
                        AddDirtySourceLabel(labels, "Profile");
                        break;
                    case CharacterActionAuthoringDocumentKind.ActionSetBundle:
                        AddDirtySourceLabel(labels, "ActionSet");
                        break;
                    case CharacterActionAuthoringDocumentKind.ActionLibrary:
                        AddDirtySourceLabel(labels, "Library");
                        break;
                    case CharacterActionAuthoringDocumentKind.MechanicTuning:
                        AddDirtySourceLabel(labels, "Mechanic Tuning");
                        break;
                    case CharacterActionAuthoringDocumentKind.AbilityEffectTimeline:
                        AddDirtySourceLabel(labels, "Ability Timeline");
                        break;
                }
            }
        }

        private static void AddDirtySourceLabel(List<string> labels, string label)
        {
            if (!labels.Contains(label))
                labels.Add(label);
        }

        private void SaveAllDirtyCharacterActionSources()
        {
            using (CharacterActionAuthoringDocumentState.BeginDirtyReadScope())
                SaveAllDirtyCharacterActionSourcesCore();
        }

        private void SaveAllDirtyCharacterActionSourcesCore()
        {
            if (_openRequestReadOnly)
            {
                ShowReadOnlySourceNotice();
                ReportOpenRequestStatus(
                    "saveFailed",
                    "The selected Common action is read-only in character context. Copy it to the character library before editing.");
                return;
            }
            if (!IsAnyCharacterActionSourceDirty() && !_abilityEffectTimelineDirty)
                return;

            if (!ValidateOpenRequestSourceHashBeforeSave(out string sourceConflict))
            {
                ReportUnifiedSaveFailure(_actionLibrarySourceDirectory, sourceConflict);
                return;
            }

            _lastSaveFailureSource = string.Empty;
            _lastSaveFailureMessage = string.Empty;

            if (_documentSession != null)
            {
                var missingDocumentSources = new List<string>();
                if (HasPendingMechanicTuningAuthoringChanges && string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath))
                    missingDocumentSources.Add("Mechanic Tuning");
                if (_abilityEffectTimelineDirty && string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath))
                    missingDocumentSources.Add("Ability Effect Timeline");
                if (missingDocumentSources.Count > 0)
                {
                    EditorUtility.DisplayDialog(
                        "保存被阻止",
                        "以下修改没有绑定来源:\n- " + string.Join("\n- ", missingDocumentSources),
                        "OK");
                    return;
                }

                if (!TryPrepareUnifiedSave(
                        out CharacterActionAuthoringUnifiedSavePlan savePlan,
                        out string preparationFailurePath,
                        out string preparationFailureMessage))
                {
                    if (!string.IsNullOrWhiteSpace(preparationFailureMessage))
                        ReportUnifiedSaveFailure(preparationFailurePath, preparationFailureMessage);
                    return;
                }

                if (!savePlan.TryExecute(
                        out CharacterActionAuthoringUnifiedSaveFailure executionFailure))
                {
                    RefreshLegacyDirtyFlagsFromDocumentSession();
                    ReportUnifiedSaveFailure(executionFailure.SourcePath, executionFailure.Message);
                    return;
                }

                RefreshLegacyDirtyFlagsFromDocumentSession();
                AssetDatabase.Refresh();
                RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, _actionLibrarySourceDirectory);
                _validationDirty = true;
                EnsureValidation(includeSnapshots: true, force: true);
                _timelineView?.RefreshCommandState();
                RefreshUi(UiRefreshScope.All);
                AcceptOpenRequestSave();
                return;
            }

            var missing = new List<string>();
            if (_profileDirty && string.IsNullOrWhiteSpace(_profileSourceDirectory))
                missing.Add("Profile");
            if (_actionSetDirty && string.IsNullOrWhiteSpace(_actionSetSourceDirectory))
                missing.Add("ActionSet");
            if (_actionLibraryDirty && string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory))
                missing.Add("Action Library");
            if (HasPendingMechanicTuningAuthoringChanges && string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath))
                missing.Add("Mechanic Tuning");

            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Save blocked",
                    "These dirty sources have no bound directory:\n- "
                    + string.Join("\n- ", missing)
                    + "\n\nUse File > Save As for each unbound source.",
                    "OK");
                _timelineView?.RefreshCommandState();
                return;
            }

            if (_profileDirty)
                SaveProfileToDirectory(_profileSourceDirectory);
            if (_actionSetDirty)
                SaveActionSetToDirectory(_actionSetSourceDirectory);
            if (_actionLibraryDirty)
                SaveActionLibraryToDirectory(_actionLibrarySourceDirectory);
            if (!SavePendingMechanicTuningAuthoringChanges())
                return;

            _timelineView?.RefreshCommandState();
            RefreshUi(UiRefreshScope.Tracks);
            AcceptOpenRequestSave();
        }

        private bool TryPrepareUnifiedSave(
            out CharacterActionAuthoringUnifiedSavePlan savePlan,
            out string failurePath,
            out string failureMessage)
        {
            savePlan = null;
            failurePath = string.Empty;
            failureMessage = string.Empty;

            if (_documentSession != null)
            {
                TryValidateDocumentSession(includeSnapshots: true);
                if (_validation?.HasErrors == true)
                {
                    _problemsExpanded = true;
                    EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
                    RefreshProblemsWorkbench();
                    return false;
                }
            }

            var preparedPlan = new CharacterActionAuthoringUnifiedSavePlan();
            try
            {
                PrepareDocumentSessionSaveOperations(preparedPlan);
                using (var staging = new CharacterActionAuthoringUnifiedSaveStagingArea())
                {
                    if (!TryPrepareMechanicTuningSaveOperation(staging, preparedPlan, out failurePath, out failureMessage))
                        return false;
                    if (!TryPrepareAbilityTimelineSaveOperation(staging, preparedPlan, out failurePath, out failureMessage))
                        return false;
                }
            }
            catch (Exception ex)
            {
                failureMessage = ex.Message;
                return false;
            }

            savePlan = preparedPlan;
            return true;
        }

        private void PrepareDocumentSessionSaveOperations(
            CharacterActionAuthoringUnifiedSavePlan savePlan)
        {
            if (_documentSession == null)
                return;

            List<CharacterActionAuthoringDocumentState> states = CollectDirtySaveStates(_documentSession.DocumentStates);
            for (int i = 0; i < states.Count; i++)
                PrepareDocumentSessionSaveOperation(savePlan, states[i], GetDocumentSaveStage(states[i].Key.Kind));
        }

        internal static List<CharacterActionAuthoringDocumentState> CollectDirtySaveStates(
            IReadOnlyList<CharacterActionAuthoringDocumentState> documents)
        {
            var states = new List<CharacterActionAuthoringDocumentState>();
            for (int i = 0; i < documents.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = documents[i];
                if ((state.Key.Kind == CharacterActionAuthoringDocumentKind.ActionLibrary
                    || state.Key.Kind == CharacterActionAuthoringDocumentKind.ActionSetBundle
                    || state.Key.Kind == CharacterActionAuthoringDocumentKind.Profile) && state.IsDirty)
                    states.Add(state);
            }

            states.Sort((left, right) =>
            {
                int stage = GetDocumentSaveStage(left.Key.Kind).CompareTo(GetDocumentSaveStage(right.Key.Kind));
                return stage != 0 ? stage : string.CompareOrdinal(left.Key.SourcePath, right.Key.SourcePath);
            });
            return states;
        }

        private static CharacterActionAuthoringUnifiedSaveStage GetDocumentSaveStage(CharacterActionAuthoringDocumentKind kind)
        {
            switch (kind)
            {
                case CharacterActionAuthoringDocumentKind.ActionLibrary: return CharacterActionAuthoringUnifiedSaveStage.ActionLibrary;
                case CharacterActionAuthoringDocumentKind.ActionSetBundle: return CharacterActionAuthoringUnifiedSaveStage.ActionSetBundle;
                case CharacterActionAuthoringDocumentKind.Profile: return CharacterActionAuthoringUnifiedSaveStage.Profile;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private void PrepareDocumentSessionSaveOperation(
            CharacterActionAuthoringUnifiedSavePlan savePlan,
            CharacterActionAuthoringDocumentState preparedState,
            CharacterActionAuthoringUnifiedSaveStage saveStage)
        {
            CharacterActionAuthoringPreparedDocumentWrite[] preparedWrites = preparedState.PrepareWrites();
            bool isCommon = saveStage == CharacterActionAuthoringUnifiedSaveStage.ActionLibrary
                && IsGeneratedCommonLibrary(preparedState.Key.SourcePath);
            CharacterActionAuthoringPreparedDocumentWrite[] transactionWrites = isCommon
                ? PrepareCommonSourceWrites(preparedState, preparedWrites)
                : preparedWrites;
            savePlan.AddPreparedFiles(
                saveStage,
                preparedState.Key.SourcePath,
                transactionWrites,
                () =>
                {
                    preparedState.AcceptPreparedWrites(preparedWrites);
                    if (isCommon) CaptureCommonSourceBaseline();
                });
        }

        private bool TryPrepareMechanicTuningSaveOperation(
            CharacterActionAuthoringUnifiedSaveStagingArea staging,
            CharacterActionAuthoringUnifiedSavePlan savePlan,
            out string failurePath,
            out string failureMessage)
        {
            failurePath = string.Empty;
            failureMessage = string.Empty;
            if (!HasPendingMechanicTuningAuthoringChanges)
                return true;

            string abilityPath = string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                ? CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath
                : _abilityAuthoringJsonPath;
            string generatedPath = CharacterActionMechanicTuningAuthoringStore.DefaultGeneratedMechanicTuningsPath;
            string mirrorPath = CharacterActionMechanicTuningAuthoringStore.DefaultPlayerMirrorMechanicTuningsPath;

            try
            {
                string stagedAbilityPath = staging.StageFile("mechanic", abilityPath, "ability_authoring.json");
                string stagedGeneratedPath = staging.StageFile("mechanic", generatedPath, "generated/mechanic_tunings.json");
                string stagedMirrorPath = staging.StageFile("mechanic", mirrorPath, "generated/player_mechanic_tunings.json");
                var options = new CharacterActionMechanicTuningAuthoringStoreOptions(
                    stagedAbilityPath,
                    stagedGeneratedPath,
                    stagedMirrorPath,
                    refreshAssetDatabase: false);
                CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringEditResult result =
                    CharacterActionMechanicTuningAuthoringStore.Load(options)
                        .UpdateTunings(CreatePendingMechanicTuningDrafts());
                if (!result.Success)
                {
                    failurePath = staging.ResolveDiagnosticPath(result.Message, abilityPath);
                    failureMessage = staging.RewriteDiagnosticPaths(result.Message);
                    return false;
                }

                var writes = new List<CharacterActionAuthoringPreparedDocumentWrite>
                {
                    staging.CaptureFile(stagedAbilityPath, abilityPath),
                    staging.CaptureFile(stagedGeneratedPath, generatedPath),
                    staging.CaptureFile(stagedMirrorPath, mirrorPath),
                };
                JObject preparedRoot = result.SourceRoot == null
                    ? null
                    : (JObject)result.SourceRoot.DeepClone();
                savePlan.AddPreparedFiles(
                    CharacterActionAuthoringUnifiedSaveStage.MechanicTuning,
                    abilityPath,
                    writes,
                    () => AcceptPreparedMechanicTuningSave(preparedRoot));
                return true;
            }
            catch (CharacterActionAuthoringUnifiedSavePreparationException ex)
            {
                failurePath = ex.SourcePath;
                failureMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                failurePath = abilityPath;
                failureMessage = ex.Message;
                return false;
            }
        }

        private void AcceptPreparedMechanicTuningSave(JObject preparedRoot)
        {
            _pendingMechanicTuningDrafts.Clear();
            ClearResourceDraftBaselinesForSession(CharacterActionAuthoringResourceDraftKind.MechanicTuning);
            if (preparedRoot != null)
                _abilityAuthoringRoot = (JObject)preparedRoot.DeepClone();
            _validationDirty = true;
            _timelineView?.RefreshCommandState();
        }

        private bool TryPrepareAbilityTimelineSaveOperation(
            CharacterActionAuthoringUnifiedSaveStagingArea staging,
            CharacterActionAuthoringUnifiedSavePlan savePlan,
            out string failurePath,
            out string failureMessage)
        {
            failurePath = string.Empty;
            failureMessage = string.Empty;
            if (!_abilityEffectTimelineDirty)
                return true;

            List<string> diagnostics = ValidateAbilityEffectTimelineDrafts();
            if (diagnostics.Count > 0)
            {
                failurePath = _abilityAuthoringJsonPath;
                failureMessage = string.Join("\n", diagnostics);
                return false;
            }

            try
            {
                NormalizeAbilityEffectTimelineDrafts();
                string baseJson = staging.ReadCurrentContents(_abilityAuthoringJsonPath);
                if (string.IsNullOrWhiteSpace(baseJson))
                {
                    JObject fallback = _abilityAuthoringRoot == null
                        ? new JObject { ["schemaVersion"] = "1" }
                        : (JObject)_abilityAuthoringRoot.DeepClone();
                    baseJson = JsonConvert.SerializeObject(
                        fallback,
                        CharacterActionAuthoringJsonService.JsonSettings);
                }

                string mergedJson = CharacterActionAuthoringUnifiedSaveJson.MergeAbilityEffectTimelines(
                    baseJson,
                    SerializeAbilityEffectTimelinesToJArray());
                var write = new CharacterActionAuthoringPreparedDocumentWrite(
                    _abilityAuthoringJsonPath,
                    mergedJson);
                staging.SetCurrentContents(_abilityAuthoringJsonPath, mergedJson);
                JObject preparedRoot = JObject.Parse(mergedJson);
                savePlan.AddPreparedFiles(
                    CharacterActionAuthoringUnifiedSaveStage.AbilityEffectTimeline,
                    _abilityAuthoringJsonPath,
                    new[] { write },
                    () =>
                    {
                        _abilityAuthoringRoot = (JObject)preparedRoot.DeepClone();
                        _abilityEffectTimelineDirty = false;
                        ClearResourceDraftBaselinesForSession(
                            CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline);
                    });
                return true;
            }
            catch (CharacterActionAuthoringUnifiedSavePreparationException ex)
            {
                failurePath = ex.SourcePath;
                failureMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                failurePath = _abilityAuthoringJsonPath;
                failureMessage = ex.Message;
                return false;
            }
        }

        private bool TrySaveDocumentSessionKinds(
            params CharacterActionAuthoringDocumentKind[] kinds)
        {
            if (_documentSession == null || kinds == null || kinds.Length == 0)
                return true;

            var states = new List<CharacterActionAuthoringDocumentState>();
            for (int i = 0; i < _documentSession.DocumentStates.Count; i++)
            {
                CharacterActionAuthoringDocumentState state = _documentSession.DocumentStates[i];
                if (!state.IsDirty || System.Array.IndexOf(kinds, state.Key.Kind) < 0)
                    continue;
                states.Add(state);
            }
            states.Sort((left, right) => string.CompareOrdinal(
                left.Key.SourcePath,
                right.Key.SourcePath));
            for (int i = 0; i < states.Count; i++)
            {
                try
                {
                    states[i].Save();
                }
                catch (System.Exception ex)
                {
                    ReportUnifiedSaveFailure(states[i].Key.SourcePath, ex.Message);
                    RefreshLegacyDirtyFlagsFromDocumentSession();
                    return false;
                }
            }

            RefreshLegacyDirtyFlagsFromDocumentSession();
            return true;
        }

        private void ReportUnifiedSaveFailure(string sourcePath, string message)
        {
            _lastSaveFailureSource = sourcePath ?? string.Empty;
            _lastSaveFailureMessage = message ?? string.Empty;
            string location = string.IsNullOrWhiteSpace(sourcePath)
                ? string.Empty
                : "\n来源: " + sourcePath;
            EditorUtility.DisplayDialog("保存失败", (message ?? string.Empty) + location, "OK");
            _problemsExpanded = true;
            EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
            RefreshProblemsWorkbench();
            string status = !string.IsNullOrWhiteSpace(message)
                && message.IndexOf("changed on disk", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "conflict"
                    : "saveFailed";
            ReportOpenRequestStatus(status, (message ?? string.Empty) + location);
        }

        private void SaveProfileAs()
        {
            string defaultDirectory = string.IsNullOrWhiteSpace(_profileSourceDirectory)
                ? "Assets/MxFrameworkGenerated/CharacterActions/Profiles"
                : _profileSourceDirectory;
            string directory;
            if (!TrySelectSaveDirectory("保存 Profile", defaultDirectory, SuggestedProfileFolderName(), out directory))
                return;

            SaveProfileToDirectory(directory);
        }

        private void SaveActionSetAs()
        {
            string defaultDirectory = string.IsNullOrWhiteSpace(_actionSetSourceDirectory)
                ? "Assets/MxFrameworkGenerated/CharacterActions/ActionSets"
                : _actionSetSourceDirectory;
            string directory;
            if (!TrySelectSaveDirectory("保存 ActionSet", defaultDirectory, SuggestedActionSetFolderName(), out directory))
                return;

            SaveActionSetToDirectory(directory);
        }

        private void SaveActionLibraryAs()
        {
            string defaultDirectory = string.IsNullOrWhiteSpace(_actionLibrarySourceDirectory)
                ? "Assets/MxFrameworkGenerated/CharacterActions/Libraries"
                : _actionLibrarySourceDirectory;
            string directory;
            if (!TrySelectSaveDirectory("保存 Action Library", defaultDirectory, SuggestedActionLibraryFolderName(), out directory))
                return;

            SaveActionLibraryToDirectory(directory);
        }

        private bool TrySelectSaveDirectory(
            string title,
            string defaultDirectory,
            string defaultName,
            out string directory)
        {
            string absolute = EditorUtility.SaveFolderPanel(title, defaultDirectory, defaultName);
            if (string.IsNullOrWhiteSpace(absolute))
            {
                RestoreWindowAfterNativePanelCancel();
                directory = string.Empty;
                return false;
            }

            directory = CharacterActionAuthoringSourceCatalog.ProjectRelativeDirectoryFromAbsolute(absolute);
            if (!string.IsNullOrWhiteSpace(directory))
                return true;

            RestoreWindowAfterNativePanelCancel();
            EditorUtility.DisplayDialog("保存目录无效", "请选择当前 Unity 项目目录内的文件夹。", "OK");
            return false;
        }

        private void RestoreWindowAfterNativePanelCancel()
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    OpenWindow();
                    return;
                }

                Focus();
                if (rootVisualElement != null)
                    Rebuild();
                Repaint();
            };
        }

        private void SaveProfileToDirectory(string directory)
        {
            if (_documentSession != null)
            {
                CharacterActionAuthoringProfileDocument profileDocument = _documentSession.ProfileDocument;
                if (IsSameSaveDirectory(directory, profileDocument.Source.DirectoryPath))
                {
                    profileDocument.State.Save();
                    RefreshLegacyDirtyFlagsFromDocumentSession();
                    AssetDatabase.Refresh();
                    RefreshUi(UiRefreshScope.All);
                    return;
                }

                SaveDocumentSessionStateAs(
                    profileDocument.State,
                    CharacterActionAuthoringDocumentKind.Profile,
                    directory);
                return;
            }
            CharacterActionAuthoringSourceCatalog.SaveProfile(directory, _model);
            AssetDatabase.Refresh();
            _profileSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            _profileDirty = false;
            RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, _actionLibrarySourceDirectory);
            Rebuild();
        }

        private void SaveActionSetToDirectory(string directory)
        {
            CharacterActionAuthoringActionSetBundleDocument activeBundle = _documentSession?.FindActionSetBundle(
                _documentSession.Selection.ActionSetStableId);
            if (activeBundle != null)
            {
                CharacterActionAuthoringExportResult sessionValidation = _documentSession.Validate(
                    _model?.ReferenceContext,
                    buildWorkstationSnapshots: false);
                ApplySaveResult(sessionValidation);
                if (!sessionValidation.Success)
                    return;
                if (IsSameSaveDirectory(directory, activeBundle.Source.DirectoryPath))
                {
                    activeBundle.State.Save();
                    RefreshLegacyDirtyFlagsFromDocumentSession();
                    AssetDatabase.Refresh();
                    RefreshUi(UiRefreshScope.All);
                    return;
                }

                SaveDocumentSessionStateAs(
                    activeBundle.State,
                    CharacterActionAuthoringDocumentKind.ActionSetBundle,
                    directory);
                return;
            }
            CharacterActionAuthoringExportResult export = CharacterActionAuthoringSourceCatalog.SaveActionSet(
                directory,
                _model,
                _model?.ReferenceContext);
            ApplySaveResult(export);
            if (!export.Success)
                return;

            AssetDatabase.Refresh();
            _actionSetSourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            _actionSetDirty = false;
            RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, _actionLibrarySourceDirectory);
            Rebuild();
        }

        private void SaveActionLibraryToDirectory(string directory)
        {
            CharacterActionAuthoringActionLibraryDocument activeLibrary = _documentSession?.CurrentActionLibrary;
            if (activeLibrary != null)
            {
                CharacterActionAuthoringExportResult sessionValidation = _documentSession.Validate(
                    _model?.ReferenceContext,
                    buildWorkstationSnapshots: false);
                ApplySaveResult(sessionValidation);
                if (!sessionValidation.Success)
                    return;
                if (IsSameSaveDirectory(directory, activeLibrary.Source.DirectoryPath))
                {
                    if (IsGeneratedCommonLibrary(activeLibrary.Source.ActionsPath))
                    {
                        SaveAllDirtyCharacterActionSources();
                        return;
                    }
                    activeLibrary.State.Save();
                    RefreshLegacyDirtyFlagsFromDocumentSession();
                    AssetDatabase.Refresh();
                    RefreshUi(UiRefreshScope.All);
                    return;
                }

                SaveDocumentSessionStateAs(
                    activeLibrary.State,
                    CharacterActionAuthoringDocumentKind.ActionLibrary,
                    directory);
                return;
            }
            if (IsGeneratedCommonLibrary(
                    CharacterActionAuthoringSourceEntry.CombineAssetPath(directory, "character_actions.json")))
            {
                ReportUnifiedSaveFailure(directory,
                    "Common edits require a loaded Profile document session so source and projection can be saved together. Draft kept.");
                return;
            }
            CharacterActionAuthoringExportResult export = CharacterActionAuthoringSourceCatalog.SaveActionLibrary(
                directory,
                _model,
                _model?.ReferenceContext);
            ApplySaveResult(export);
            if (!export.Success)
                return;

            AssetDatabase.Refresh();
            _actionLibrarySourceDirectory = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(directory);
            _actionLibraryDirty = false;
            RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, _actionLibrarySourceDirectory);
            Rebuild();
        }

        private static bool IsSameSaveDirectory(string left, string right)
        {
            return string.Equals(
                CharacterActionAuthoringSourceEntry.NormalizeAssetPath(left),
                CharacterActionAuthoringSourceEntry.NormalizeAssetPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private void SaveDocumentSessionStateAs(
            CharacterActionAuthoringDocumentState state,
            CharacterActionAuthoringDocumentKind kind,
            string targetDirectory)
        {
            if (state == null)
                return;

            string dirtyDrafts = _documentSession.SerializeDirtyDrafts();
            string originalSourcePath = state.Key.SourcePath;
            string[] targetPaths;
            try
            {
                targetPaths = CharacterActionAuthoringDocumentSaveAs.CopyToDirectory(
                    state.PrepareWrites(),
                    targetDirectory);
            }
            catch (CharacterActionAuthoringUnifiedSavePreparationException ex)
            {
                ReportUnifiedSaveFailure(ex.SourcePath, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                ReportUnifiedSaveFailure(targetDirectory, ex.Message);
                return;
            }

            AssetDatabase.Refresh();
            RefreshSourceCatalogs();
            if (!TryRebindDocumentSessionAfterSaveAs(
                    kind,
                    targetDirectory,
                    originalSourcePath,
                    dirtyDrafts,
                    out string rebindError))
            {
                EditorUtility.DisplayDialog(
                    "Save As completed",
                    "The copy was written to:\n- "
                    + string.Join("\n- ", targetPaths)
                    + "\n\nThe current session remains bound to its original source because the copy could not be reloaded:\n"
                    + rebindError,
                    "OK");
                RefreshUi(UiRefreshScope.All);
                return;
            }

            RefreshLegacyDirtyFlagsFromDocumentSession();
            _validationDirty = true;
            RefreshUi(UiRefreshScope.All);
        }

        private bool TryRebindDocumentSessionAfterSaveAs(
            CharacterActionAuthoringDocumentKind kind,
            string targetDirectory,
            string originalSourcePath,
            string dirtyDrafts,
            out string error)
        {
            error = string.Empty;
            CharacterActionAuthoringDocumentSession current = _documentSession;
            if (current == null)
            {
                error = "The document session is no longer available.";
                return false;
            }

            var targetSource = new CharacterActionAuthoringSourceEntry(
                targetDirectory,
                CharacterActionAuthoringSourceEntry.GetLeafDirectoryName(targetDirectory));
            CharacterActionAuthoringSourceEntry profileSource = kind == CharacterActionAuthoringDocumentKind.Profile
                ? targetSource
                : current.ProfileDocument.Source;
            var actionSetSources = new CharacterActionAuthoringSourceEntry[current.ActionSetBundles.Count];
            for (int i = 0; i < actionSetSources.Length; i++)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle = current.ActionSetBundles[i];
                actionSetSources[i] = kind == CharacterActionAuthoringDocumentKind.ActionSetBundle
                    && string.Equals(bundle.State.Key.SourcePath, originalSourcePath, StringComparison.OrdinalIgnoreCase)
                        ? targetSource
                        : bundle.Source;
            }

            var librarySources = new CharacterActionAuthoringSourceEntry[current.ActionLibraries.Count];
            for (int i = 0; i < librarySources.Length; i++)
            {
                CharacterActionAuthoringActionLibraryDocument library = current.ActionLibraries[i];
                librarySources[i] = kind == CharacterActionAuthoringDocumentKind.ActionLibrary
                    && string.Equals(library.State.Key.SourcePath, originalSourcePath, StringComparison.OrdinalIgnoreCase)
                        ? targetSource
                        : library.Source;
            }

            CharacterActionAuthoringDocumentSession rebound;
            try
            {
                rebound = CharacterActionAuthoringDocumentSession.Load(
                    profileSource,
                    actionSetSources,
                    librarySources,
                    current.Selection.ProfileStableId);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            if (!rebound.RestoreDirtyDrafts(dirtyDrafts, out string[] conflictPaths))
            {
                error = "The unsaved drafts could not be restored into the copied session.";
                return false;
            }

            for (int i = 0; i < conflictPaths.Length; i++)
            {
                if (!string.Equals(conflictPaths[i], originalSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    error = "A sibling draft changed on disk: " + conflictPaths[i];
                    return false;
                }
            }

            AdoptDocumentSession(rebound);
            if (kind == CharacterActionAuthoringDocumentKind.Profile)
                _profileSourceDirectory = targetSource.DirectoryPath;
            else if (kind == CharacterActionAuthoringDocumentKind.ActionSetBundle)
                _actionSetSourceDirectory = targetSource.DirectoryPath;
            else if (kind == CharacterActionAuthoringDocumentKind.ActionLibrary)
                _actionLibrarySourceDirectory = targetSource.DirectoryPath;
            return true;
        }

        private void ApplySaveResult(CharacterActionAuthoringExportResult export)
        {
            _validation = export;
            _validationDirty = false;
            if (export.Success)
                return;

            EditorUtility.DisplayDialog("保存被阻止", export.DiagnosticsText, "OK");
            _problemsExpanded = true;
            EditorPrefs.SetBool(WorkbenchProblemsExpandedPreferenceKey, true);
            RefreshProblemsWorkbench();
        }

        private void DeleteActionSetSource()
        {
            int index = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(_actionSetSources, _actionSetSourceDirectory);
            if (index < 0)
                return;

            CharacterActionAuthoringSourceEntry source = _actionSetSources[index];
            if (!EditorUtility.DisplayDialog(
                "删除 ActionSet",
                "将删除以下文件:\n" + source.ActionSetsPath,
                "删除",
                "取消"))
            {
                return;
            }

            CharacterActionAuthoringSourceCatalog.DeleteActionSet(source);
            RefreshSources(_profileSourceDirectory, string.Empty, _actionLibrarySourceDirectory);
            _actionSetSourceDirectory = string.Empty;
            _actionSetDirty = true;
            Rebuild();
        }

        private void DeleteActionLibrarySource()
        {
            int index = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(_actionLibrarySources, _actionLibrarySourceDirectory);
            if (index < 0)
                return;

            CharacterActionAuthoringSourceEntry source = _actionLibrarySources[index];
            if (!EditorUtility.DisplayDialog(
                "删除 Action Library",
                "将删除以下文件:\n" + source.ActionsPath,
                "删除",
                "取消"))
            {
                return;
            }

            CharacterActionAuthoringSourceCatalog.DeleteActionLibrary(source);
            RefreshSources(_profileSourceDirectory, _actionSetSourceDirectory, string.Empty);
            _actionLibrarySourceDirectory = string.Empty;
            _actionLibraryDirty = true;
            Rebuild();
        }

        private bool ConfirmDiscardUnsavedChanges()
        {
            if (!IsAnyDirty())
                return true;

            int choice = EditorUtility.DisplayDialogComplex(
                "存在未保存修改",
                "当前工作会话有未保存内容：\n" + CreateDirtyStatusText(),
                "保存并继续",
                "取消",
                "放弃修改");
            if (choice == 1)
                return false;
            if (choice == 0)
            {
                SaveAllDirtyCharacterActionSources();
                return !IsAnyDirty();
            }

            _pendingMechanicTuningDrafts.Clear();
            if (_abilityEffectTimelineDirty)
            {
                string abilityPath = AbsoluteProjectPath(_abilityAuthoringJsonPath);
                if (File.Exists(abilityPath))
                {
                    if (!LoadAbilityEffectTimelinesFromCurrentPath(
                            confirmDirty: false,
                            refreshUi: false))
                    {
                        return false;
                    }
                }
                else
                {
                    _abilityAuthoringRoot = null;
                    _abilityEffectTimelines = null;
                    _selectedAbilityEffectTimelineId = string.Empty;
                    _pendingAbilityEffectTimelineSelectedItemId = string.Empty;
                    _abilityEffectTimelineDirty = false;
                }
            }
            ClearResourceDraftBaselinesForSession(CharacterActionAuthoringResourceDraftKind.MechanicTuning);
            ClearResourceDraftBaselinesForSession(
                CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline);
            _profileDirty = false;
            _actionSetDirty = false;
            _actionLibraryDirty = false;
            _documentSession = null;
            _undoHistory.Clear();
            _redoHistory.Clear();
            return true;
        }

        private string SuggestedProfileFolderName()
        {
            string stableId = _model?.Profile?.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = "character_action_profile";
            return stableId.Trim().Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }

        private string SuggestedActionSetFolderName()
        {
            string stableId = _model?.ActionSet?.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = "character_action_set";
            return stableId.Trim().Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }

        private string SuggestedActionLibraryFolderName()
        {
            string stableId = _model?.ActionSet?.StableId;
            if (string.IsNullOrWhiteSpace(stableId))
                stableId = "Common";
            int dotIndex = stableId.IndexOf('.');
            if (dotIndex > 0)
                stableId = stableId.Substring(0, dotIndex);
            return stableId.Trim().Replace('.', '_').Replace('/', '_').Replace('\\', '_');
        }
    }

    internal enum CharacterActionAuthoringUnifiedSaveStage
    {
        ActionLibrary = 0,
        ActionSetBundle = 1,
        MechanicTuning = 2,
        Profile = 3,
        AbilityEffectTimeline = 4,
    }

    internal readonly struct CharacterActionAuthoringUnifiedSaveFailure
    {
        internal CharacterActionAuthoringUnifiedSaveFailure(string sourcePath, string message)
        {
            SourcePath = sourcePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal string SourcePath { get; }
        internal string Message { get; }
    }

    internal sealed class CharacterActionAuthoringUnifiedSavePlan
    {
        private readonly List<Operation> _operations = new List<Operation>();
        private readonly Action<string, string> _writeFile;
        private int _sequence;

        internal CharacterActionAuthoringUnifiedSavePlan(Action<string, string> writeFile = null)
        {
            _writeFile = writeFile ?? WriteFile;
        }

        internal void AddOperation(
            CharacterActionAuthoringUnifiedSaveStage stage,
            string sortPath,
            Action execute)
        {
            _operations.Add(new Operation(
                stage,
                sortPath,
                _sequence++,
                execute,
                null,
                null));
        }

        internal void AddPreparedFiles(
            CharacterActionAuthoringUnifiedSaveStage stage,
            string sortPath,
            IReadOnlyList<CharacterActionAuthoringPreparedDocumentWrite> writes,
            Action accept)
        {
            var prepared = new List<CharacterActionAuthoringPreparedDocumentWrite>(writes?.Count ?? 0);
            for (int i = 0; writes != null && i < writes.Count; i++)
                prepared.Add(writes[i]);
            prepared.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
            _operations.Add(new Operation(
                stage,
                sortPath,
                _sequence++,
                null,
                prepared,
                accept));
        }

        internal bool TryExecute(out CharacterActionAuthoringUnifiedSaveFailure failure)
        {
            _operations.Sort(CompareOperations);
            var originalFiles = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var accepts = new List<Action>();
            var attemptedPaths = new List<string>();
            foreach (Operation operation in _operations)
            {
                if (operation.Writes == null) continue;
                foreach (CharacterActionAuthoringPreparedDocumentWrite write in operation.Writes)
                {
                    try
                    {
                        if (!originalFiles.ContainsKey(write.Path))
                            originalFiles.Add(write.Path, File.Exists(write.Path) ? File.ReadAllBytes(write.Path) : null);
                    }
                    catch (Exception ex)
                    {
                        failure = new CharacterActionAuthoringUnifiedSaveFailure(write.Path, ex.Message);
                        return false;
                    }
                    if (write.ExpectedFileHash == null) continue;
                    byte[] original = originalFiles[write.Path];
                    string actualHash = original == null ? string.Empty : ComputeFileHash(original);
                    if (!string.Equals(actualHash, write.ExpectedFileHash, StringComparison.Ordinal))
                    {
                        failure = new CharacterActionAuthoringUnifiedSaveFailure(
                            write.Path, "Source changed externally; reload before saving. Draft kept.");
                        return false;
                    }
                }
            }
            for (int i = 0; i < _operations.Count; i++)
            {
                Operation operation = _operations[i];
                if (operation.Execute != null)
                {
                    try
                    {
                        operation.Execute();
                    }
                    catch (Exception ex)
                    {
                        string rollbackFailure = RestoreFiles(originalFiles, attemptedPaths);
                        failure = new CharacterActionAuthoringUnifiedSaveFailure(
                            operation.SortPath,
                            ex.Message + rollbackFailure);
                        return false;
                    }
                }
                else
                {
                    for (int writeIndex = 0; writeIndex < operation.Writes.Count; writeIndex++)
                    {
                        CharacterActionAuthoringPreparedDocumentWrite write = operation.Writes[writeIndex];
                        try
                        {
                            attemptedPaths.Add(write.Path);
                            _writeFile(write.Path, write.Contents);
                        }
                        catch (Exception ex)
                        {
                            string rollbackFailure = RestoreFiles(originalFiles, attemptedPaths);
                            failure = new CharacterActionAuthoringUnifiedSaveFailure(
                                write.Path,
                                ex.Message + rollbackFailure);
                            return false;
                        }
                    }
                }

                if (operation.Accept != null)
                    accepts.Add(operation.Accept);
            }

            foreach (Action accept in accepts)
                accept();

            failure = default;
            return true;
        }

        private static string ComputeFileHash(byte[] contents)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(contents));
        }

        private static string RestoreFiles(Dictionary<string, byte[]> originals, List<string> attemptedPaths)
        {
            string failure = string.Empty;
            var restored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = attemptedPaths.Count - 1; i >= 0; i--)
            {
                string path = attemptedPaths[i];
                if (!restored.Add(path) || !originals.TryGetValue(path, out byte[] contents)) continue;
                try
                {
                    if (contents == null)
                    {
                        if (File.Exists(path)) File.Delete(path);
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                        File.WriteAllBytes(path, contents);
                    }
                }
                catch (Exception ex)
                {
                    failure += " Rollback failed for " + path + ": " + ex.Message;
                }
            }
            return failure;
        }

        private static int CompareOperations(Operation left, Operation right)
        {
            int stage = left.Stage.CompareTo(right.Stage);
            if (stage != 0)
                return stage;
            int path = string.CompareOrdinal(left.SortPath, right.SortPath);
            return path != 0 ? path : left.Sequence.CompareTo(right.Sequence);
        }

        private static void WriteFile(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, contents ?? string.Empty);
        }

        private sealed class Operation
        {
            internal Operation(
                CharacterActionAuthoringUnifiedSaveStage stage,
                string sortPath,
                int sequence,
                Action execute,
                List<CharacterActionAuthoringPreparedDocumentWrite> writes,
                Action accept)
            {
                Stage = stage;
                SortPath = sortPath ?? string.Empty;
                Sequence = sequence;
                Execute = execute;
                Writes = writes;
                Accept = accept;
            }

            internal CharacterActionAuthoringUnifiedSaveStage Stage { get; }
            internal string SortPath { get; }
            internal int Sequence { get; }
            internal Action Execute { get; }
            internal List<CharacterActionAuthoringPreparedDocumentWrite> Writes { get; }
            internal Action Accept { get; }
        }
    }

    internal sealed class CharacterActionAuthoringUnifiedSaveStagingArea : IDisposable
    {
        private readonly Dictionary<string, string> _currentContents =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _actualPathsByTempPath =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal CharacterActionAuthoringUnifiedSaveStagingArea()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "MxFramework-CharacterAction-Save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        internal string RootPath { get; }

        internal string StageFile(string group, string actualPath, string relativeTempPath)
        {
            string tempPath = Path.Combine(
                RootPath,
                group ?? string.Empty,
                (relativeTempPath ?? Path.GetFileName(actualPath) ?? "source.json")
                    .Replace('/', Path.DirectorySeparatorChar));
            string tempDirectory = Path.GetDirectoryName(tempPath);
            if (!string.IsNullOrWhiteSpace(tempDirectory))
                Directory.CreateDirectory(tempDirectory);

            if (TryReadCurrentContents(actualPath, out string contents))
                File.WriteAllText(tempPath, contents);
            _actualPathsByTempPath[Path.GetFullPath(tempPath)] = actualPath ?? string.Empty;
            return tempPath;
        }

        internal IReadOnlyList<CharacterActionAuthoringStagedDirectoryFile> StageDirectory(
            string group,
            string actualDirectory,
            string relativeTempDirectory)
        {
            string tempDirectory = Path.Combine(
                RootPath,
                group ?? string.Empty,
                (relativeTempDirectory ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(tempDirectory);
            var staged = new List<CharacterActionAuthoringStagedDirectoryFile>();
            if (string.IsNullOrWhiteSpace(actualDirectory) || !Directory.Exists(actualDirectory))
                return staged;

            string[] files = Directory.GetFiles(actualDirectory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                string actualPath = files[i];
                string contents = ReadCurrentContents(actualPath);
                string tempPath = Path.Combine(tempDirectory, Path.GetFileName(actualPath));
                File.WriteAllText(tempPath, contents);
                _actualPathsByTempPath[Path.GetFullPath(tempPath)] = actualPath;
                staged.Add(new CharacterActionAuthoringStagedDirectoryFile(
                    actualPath,
                    tempPath,
                    contents));
            }

            return staged;
        }

        internal CharacterActionAuthoringPreparedDocumentWrite CaptureFile(
            string tempPath,
            string actualPath)
        {
            if (!File.Exists(tempPath))
            {
                throw new CharacterActionAuthoringUnifiedSavePreparationException(
                    actualPath,
                    "Prepared JSON was not generated: " + actualPath);
            }

            string contents = File.ReadAllText(tempPath);
            SetCurrentContents(actualPath, contents);
            return new CharacterActionAuthoringPreparedDocumentWrite(actualPath, contents);
        }

        internal string ReadCurrentContents(string actualPath)
        {
            if (TryReadCurrentContents(actualPath, out string contents))
                return contents;
            return string.Empty;
        }

        internal void SetCurrentContents(string actualPath, string contents)
        {
            _currentContents[NormalizeKey(actualPath)] = contents ?? string.Empty;
        }

        internal string ResolveDiagnosticPath(string message, string fallbackPath)
        {
            string diagnostic = message ?? string.Empty;
            foreach (KeyValuePair<string, string> pair in _actualPathsByTempPath)
            {
                if (diagnostic.IndexOf(pair.Key, StringComparison.OrdinalIgnoreCase) >= 0
                    || diagnostic.IndexOf(pair.Key.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }

            return fallbackPath ?? string.Empty;
        }

        internal string RewriteDiagnosticPaths(string message)
        {
            string rewritten = message ?? string.Empty;
            foreach (KeyValuePair<string, string> pair in _actualPathsByTempPath)
            {
                rewritten = rewritten.Replace(pair.Key, pair.Value ?? string.Empty);
                rewritten = rewritten.Replace(pair.Key.Replace('\\', '/'), (pair.Value ?? string.Empty).Replace('\\', '/'));
            }

            return rewritten;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                    Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryReadCurrentContents(string actualPath, out string contents)
        {
            string key = NormalizeKey(actualPath);
            if (_currentContents.TryGetValue(key, out contents))
                return true;
            if (string.IsNullOrWhiteSpace(actualPath) || !File.Exists(actualPath))
            {
                contents = string.Empty;
                return false;
            }

            try
            {
                contents = File.ReadAllText(actualPath);
                return true;
            }
            catch (Exception ex)
            {
                throw new CharacterActionAuthoringUnifiedSavePreparationException(actualPath, ex.Message);
            }
        }

        private static string NormalizeKey(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Path.GetFullPath(path);
        }
    }

    internal readonly struct CharacterActionAuthoringStagedDirectoryFile
    {
        internal CharacterActionAuthoringStagedDirectoryFile(
            string actualPath,
            string tempPath,
            string baselineContents)
        {
            ActualPath = actualPath ?? string.Empty;
            TempPath = tempPath ?? string.Empty;
            BaselineContents = baselineContents ?? string.Empty;
        }

        internal string ActualPath { get; }
        internal string TempPath { get; }
        internal string BaselineContents { get; }
    }

    internal sealed class CharacterActionAuthoringUnifiedSavePreparationException : Exception
    {
        internal CharacterActionAuthoringUnifiedSavePreparationException(
            string sourcePath,
            string message)
            : base(message)
        {
            SourcePath = sourcePath ?? string.Empty;
        }

        internal string SourcePath { get; }
    }

    internal static class CharacterActionAuthoringUnifiedSaveJson
    {
        internal static string MergeAbilityEffectTimelines(string baseJson, JArray effectTimelines)
        {
            JObject root = string.IsNullOrWhiteSpace(baseJson)
                ? new JObject { ["schemaVersion"] = "1" }
                : JObject.Parse(baseJson);
            if (root["schemaVersion"] == null)
                root["schemaVersion"] = "1";
            root["effectTimelines"] = effectTimelines == null
                ? new JArray()
                : (JArray)effectTimelines.DeepClone();
            return JsonConvert.SerializeObject(
                root,
                CharacterActionAuthoringJsonService.JsonSettings);
        }
    }

    internal static class CharacterActionAuthoringDocumentSaveAs
    {
        internal static string[] CopyToDirectory(
            IReadOnlyList<CharacterActionAuthoringPreparedDocumentWrite> writes,
            string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new CharacterActionAuthoringUnifiedSavePreparationException(
                    targetDirectory,
                    "The Save As target directory is empty.");
            }

            var targetPaths = new string[writes?.Count ?? 0];
            for (int i = 0; writes != null && i < writes.Count; i++)
            {
                string fileName = Path.GetFileName(writes[i].Path);
                string targetPath = CharacterActionAuthoringSourceEntry.CombineAssetPath(
                    targetDirectory,
                    fileName);
                try
                {
                    Directory.CreateDirectory(targetDirectory);
                    File.WriteAllText(targetPath, writes[i].Contents ?? string.Empty);
                    targetPaths[i] = targetPath;
                }
                catch (Exception ex)
                {
                    throw new CharacterActionAuthoringUnifiedSavePreparationException(
                        targetPath,
                        ex.Message);
                }
            }

            return targetPaths;
        }
    }
}
