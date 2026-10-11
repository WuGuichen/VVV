using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private const string SessionKeyPrefix = "MxFramework.CharacterActionAuthoringWindow.";
        private const string SessionModelKey = SessionKeyPrefix + "Model";
        private const string SessionProfileSourceKey = SessionKeyPrefix + "ProfileSource";
        private const string SessionActionSetSourceKey = SessionKeyPrefix + "ActionSetSource";
        private const string SessionActionLibrarySourceKey = SessionKeyPrefix + "ActionLibrarySource";
        private const string SessionEditorModeKey = SessionKeyPrefix + "EditorMode";
        private const string SessionTabKey = SessionKeyPrefix + "Tab";
        private const string SessionSelectedActionKey = SessionKeyPrefix + "SelectedAction";
        private const string SessionSelectedTimelineItemKey = SessionKeyPrefix + "SelectedTimelineItem";
        private const string SessionSelectedAbilityTimelineItemKey =
            SessionKeyPrefix + "SelectedAbilityTimelineItem";
        private const string SessionAbilityAuthoringJsonPathKey = SessionKeyPrefix + "AbilityAuthoringJsonPath";
        private const string SessionSelectedAbilityEffectTimelineKey = SessionKeyPrefix + "SelectedAbilityEffectTimeline";
        private const string SessionAbilityEffectTimelineDraftsKey = SessionKeyPrefix + "AbilityEffectTimelineDrafts";
        private const string SessionActionFilterKey = SessionKeyPrefix + "ActionFilter";
        private const string SessionActionCategoryFilterKey = SessionKeyPrefix + "ActionCategoryFilter";
        private const string SessionCommandBindingQueryKey = SessionKeyPrefix + "CommandBindingQuery";
        private const string SessionCommandBindingSortKey = SessionKeyPrefix + "CommandBindingSort";
        private const string SessionAbilityBindingQueryKey = SessionKeyPrefix + "AbilityBindingQuery";
        private const string SessionAbilityBindingSortKey = SessionKeyPrefix + "AbilityBindingSort";
        private const string SessionProjectileSearchKey = SessionKeyPrefix + "ProjectileSearch";
        private const string SessionProjectileMotionFilterKey = SessionKeyPrefix + "ProjectileMotionFilter";
        private const string SessionProjectilePolicyFilterKey = SessionKeyPrefix + "ProjectilePolicyFilter";
        private const string SessionProjectileStatusFilterKey = SessionKeyPrefix + "ProjectileStatusFilter";
        private const string SessionProjectileOnlyUnusedKey = SessionKeyPrefix + "ProjectileOnlyUnused";
        private const string SessionProblemsQueryKey = SessionKeyPrefix + "ProblemsQuery";
        private const string SessionProblemsScopeKey = SessionKeyPrefix + "ProblemsScope";
        private const string SessionProblemsSeverityKey = SessionKeyPrefix + "ProblemsSeverity";
        private const string SessionWorkspaceKey = SessionKeyPrefix + "Workspace";
        private const string SessionMappingSectionKey = SessionKeyPrefix + "MappingSection";
        private const string SessionSharedResourceSectionKey = SessionKeyPrefix + "SharedResourceSection";
        private const string SessionSelectedProfileStableIdKey = SessionKeyPrefix + "SelectedProfileStableId";
        private const string SessionSelectedActionSetStableIdKey = SessionKeyPrefix + "SelectedActionSetStableId";
        private const string SessionPendingMechanicTuningDraftsKey = SessionKeyPrefix + "PendingMechanicTuningDrafts";
        private const string SessionResourceDraftsKey = SessionKeyPrefix + "ResourceDrafts";
        private const string SessionDocumentDraftsKey = SessionKeyPrefix + "DocumentDrafts";
        private const string SessionConflictedDocumentDraftsKey = SessionKeyPrefix + "ConflictedDocumentDrafts";
        private const string SessionDocumentDraftConflictPathsKey = SessionKeyPrefix + "DocumentDraftConflictPaths";
        private const string SessionProfileDirtyKey = SessionKeyPrefix + "ProfileDirty";
        private const string SessionActionSetDirtyKey = SessionKeyPrefix + "ActionSetDirty";
        private const string SessionActionLibraryDirtyKey = SessionKeyPrefix + "ActionLibraryDirty";
        private const string SessionAbilityEffectTimelineDirtyKey = SessionKeyPrefix + "AbilityEffectTimelineDirty";
        private const string SessionValidationDirtyKey = SessionKeyPrefix + "ValidationDirty";

        private string _pendingTimelineSelectedItemId = string.Empty;
        private string _pendingAbilityEffectTimelineSelectedItemId = string.Empty;
        private string _pendingDocumentSessionDraftsJson = string.Empty;
        private string _pendingResourceSessionDraftsJson = string.Empty;
        private string _conflictedDocumentSessionDraftsJson = string.Empty;
        private string _conflictedDocumentDraftPayloadJson = string.Empty;
        private string _conflictedResourceDraftPayloadJson = string.Empty;
        private string[] _documentDraftConflictPaths = Array.Empty<string>();
        private string[] _resourceDraftConflictPaths = Array.Empty<string>();
        private string[] _documentSessionDraftConflictPaths = Array.Empty<string>();
        private readonly Dictionary<string, string> _resourceDraftBaselineHashes =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _sessionRestored;
        private bool _savingSession;

        private void RestoreSessionState()
        {
            if (_sessionRestored)
                return;

            _sessionRestored = true;
            _profileSourceDirectory = SessionState.GetString(SessionProfileSourceKey, _profileSourceDirectory);
            _actionSetSourceDirectory = SessionState.GetString(SessionActionSetSourceKey, _actionSetSourceDirectory);
            _actionLibrarySourceDirectory = SessionState.GetString(SessionActionLibrarySourceKey, _actionLibrarySourceDirectory);
            _abilityAuthoringJsonPath = SessionState.GetString(
                SessionAbilityAuthoringJsonPathKey,
                _abilityAuthoringJsonPath);
            _selectedAbilityEffectTimelineId = SessionState.GetString(
                SessionSelectedAbilityEffectTimelineKey,
                _selectedAbilityEffectTimelineId);
            _actionListFilter = SessionState.GetString(SessionActionFilterKey, _actionListFilter);
            _actionCategoryFilter = SessionState.GetString(
                SessionActionCategoryFilterKey,
                _actionCategoryFilter);
            _commandBindingTableQuery = SessionState.GetString(
                SessionCommandBindingQueryKey,
                _commandBindingTableQuery);
            string commandSortName = SessionState.GetString(SessionCommandBindingSortKey, string.Empty);
            if (Enum.TryParse(commandSortName, out CharacterActionBindingTableSort commandSort))
                _commandBindingTableSort = commandSort;
            _abilityBindingTableQuery = SessionState.GetString(
                SessionAbilityBindingQueryKey,
                _abilityBindingTableQuery);
            string abilitySortName = SessionState.GetString(SessionAbilityBindingSortKey, string.Empty);
            if (Enum.TryParse(abilitySortName, out CharacterActionBindingTableSort abilitySort))
                _abilityBindingTableSort = abilitySort;
            _problemsQuery = SessionState.GetString(SessionProblemsQueryKey, _problemsQuery);
            string problemsScopeName = SessionState.GetString(SessionProblemsScopeKey, string.Empty);
            if (Enum.TryParse(problemsScopeName, out CharacterActionAuthoringProblemScope restoredProblemsScope))
                _problemsScope = restoredProblemsScope;
            string problemsSeverityName = SessionState.GetString(SessionProblemsSeverityKey, string.Empty);
            if (Enum.TryParse(
                problemsSeverityName,
                out CharacterActionAuthoringProblemSeverityFilter restoredProblemsSeverity))
            {
                _problemsSeverity = restoredProblemsSeverity;
            }
            _pendingTimelineSelectedItemId = SessionState.GetString(SessionSelectedTimelineItemKey, string.Empty);
            _pendingAbilityEffectTimelineSelectedItemId = SessionState.GetString(
                SessionSelectedAbilityTimelineItemKey,
                string.Empty);
            _pendingDocumentSessionDraftsJson = SessionState.GetString(
                SessionDocumentDraftsKey,
                string.Empty);
            _pendingResourceSessionDraftsJson = SessionState.GetString(
                SessionResourceDraftsKey,
                string.Empty);
            _conflictedDocumentSessionDraftsJson = SessionState.GetString(
                SessionConflictedDocumentDraftsKey,
                string.Empty);
            string conflictPathsJson = SessionState.GetString(
                SessionDocumentDraftConflictPathsKey,
                string.Empty);
            if (!string.IsNullOrWhiteSpace(conflictPathsJson))
            {
                try
                {
                    _documentSessionDraftConflictPaths =
                        JsonConvert.DeserializeObject<string[]>(conflictPathsJson)
                        ?? Array.Empty<string>();
                }
                catch (JsonException)
                {
                    _documentSessionDraftConflictPaths = Array.Empty<string>();
                }
            }
            RestoreConflictRecoveryEnvelope(
                _conflictedDocumentSessionDraftsJson,
                _documentSessionDraftConflictPaths);

            string editorModeName = SessionState.GetString(SessionEditorModeKey, _editorMode.ToString());
            if (Enum.TryParse(editorModeName, out AuthoringEditorMode restoredMode))
                _editorMode = restoredMode;

            string tabName = SessionState.GetString(SessionTabKey, _tab.ToString());
            if (Enum.TryParse(tabName, out AuthoringTab restoredTab))
                _tab = restoredTab;

            string workspaceName = SessionState.GetString(SessionWorkspaceKey, string.Empty);
            if (Enum.TryParse(workspaceName, out AuthoringWorkspace restoredWorkspace))
            {
                _workspace = restoredWorkspace;
            }
            else
            {
                _workspace = WorkspaceFromLegacyTab(_tab, _editorMode);
            }
            string mappingSectionName = SessionState.GetString(SessionMappingSectionKey, string.Empty);
            if (Enum.TryParse(mappingSectionName, out MappingWorkspaceSection restoredMappingSection))
                _mappingSection = restoredMappingSection;
            string sharedSectionName = SessionState.GetString(SessionSharedResourceSectionKey, string.Empty);
            if (Enum.TryParse(sharedSectionName, out SharedResourceSection restoredSharedSection))
                _sharedResourceSection = restoredSharedSection;
            else if (_editorMode == AuthoringEditorMode.AbilityEffectTimeline)
                _sharedResourceSection = SharedResourceSection.AbilityEffectTimeline;
            NormalizeEditorModeForWorkspace();

            string modelJson = SessionState.GetString(SessionModelKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(modelJson))
            {
                try
                {
                    CharacterActionAuthoringWindowSessionModel sessionModel =
                        JsonConvert.DeserializeObject<CharacterActionAuthoringWindowSessionModel>(
                            modelJson,
                            CharacterActionAuthoringJsonService.JsonSettings);
                    _model = sessionModel?.ToModel();
                    _model?.Normalize();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning("恢复 Character Action Authoring 窗口状态失败: " + ex.Message);
                    _model = null;
                }
            }

            RestoreSelectedActionFromSession();

            _profileDirty = SessionState.GetBool(SessionProfileDirtyKey, _profileDirty);
            _actionSetDirty = SessionState.GetBool(SessionActionSetDirtyKey, _actionSetDirty);
            _actionLibraryDirty = SessionState.GetBool(SessionActionLibraryDirtyKey, _actionLibraryDirty);
            _abilityEffectTimelineDirty = SessionState.GetBool(
                SessionAbilityEffectTimelineDirtyKey,
                _abilityEffectTimelineDirty);
            _validationDirty = SessionState.GetBool(SessionValidationDirtyKey, true);

            RestorePendingAuthoringDraftsFromSession();
        }

        private void SaveSessionState()
        {
            if (_savingSession)
                return;

            _savingSession = true;
            try
            {
                if (_timelineView != null)
                    _pendingTimelineSelectedItemId = _timelineView.SelectedItemId ?? string.Empty;
                if (_abilityEffectTimelineView != null)
                {
                    _pendingAbilityEffectTimelineSelectedItemId =
                        _abilityEffectTimelineView.SelectedItemId ?? string.Empty;
                }

                SessionState.SetString(SessionProfileSourceKey, _profileSourceDirectory ?? string.Empty);
                SessionState.SetString(SessionActionSetSourceKey, _actionSetSourceDirectory ?? string.Empty);
                SessionState.SetString(SessionActionLibrarySourceKey, _actionLibrarySourceDirectory ?? string.Empty);
                SessionState.SetString(SessionEditorModeKey, _editorMode.ToString());
                SessionState.SetString(SessionTabKey, _tab.ToString());
                SessionState.SetInt(SessionSelectedActionKey, _model?.SelectedAction?.ActionId ?? 0);
                SessionState.SetString(SessionSelectedTimelineItemKey, _pendingTimelineSelectedItemId ?? string.Empty);
                SessionState.SetString(
                    SessionSelectedAbilityTimelineItemKey,
                    _pendingAbilityEffectTimelineSelectedItemId ?? string.Empty);
                SessionState.SetString(SessionAbilityAuthoringJsonPathKey, _abilityAuthoringJsonPath ?? string.Empty);
                SessionState.SetString(
                    SessionSelectedAbilityEffectTimelineKey,
                    _selectedAbilityEffectTimelineId ?? string.Empty);
                SessionState.SetString(
                    SessionAbilityEffectTimelineDraftsKey,
                    string.Empty);
                SessionState.SetString(SessionActionFilterKey, _actionListFilter ?? string.Empty);
                SessionState.SetString(
                    SessionActionCategoryFilterKey,
                    _actionCategoryFilter ?? string.Empty);
                SessionState.SetString(
                    SessionCommandBindingQueryKey,
                    _commandBindingTableQuery ?? string.Empty);
                SessionState.SetString(SessionCommandBindingSortKey, _commandBindingTableSort.ToString());
                SessionState.SetString(
                    SessionAbilityBindingQueryKey,
                    _abilityBindingTableQuery ?? string.Empty);
                SessionState.SetString(SessionAbilityBindingSortKey, _abilityBindingTableSort.ToString());
                SessionState.SetString(SessionProblemsQueryKey, _problemsQuery ?? string.Empty);
                SessionState.SetString(SessionProblemsScopeKey, _problemsScope.ToString());
                SessionState.SetString(SessionProblemsSeverityKey, _problemsSeverity.ToString());
                SessionState.SetString(SessionWorkspaceKey, _workspace.ToString());
                SessionState.SetString(SessionMappingSectionKey, _mappingSection.ToString());
                SessionState.SetString(SessionSharedResourceSectionKey, _sharedResourceSection.ToString());
                SessionState.SetString(
                    SessionDocumentDraftsKey,
                    _documentSession?.SerializeDirtyDrafts()
                    ?? _pendingDocumentSessionDraftsJson
                    ?? string.Empty);
                SessionState.SetString(
                    SessionResourceDraftsKey,
                    SerializeResourceDraftsForSession());
                SessionState.SetString(
                    SessionConflictedDocumentDraftsKey,
                    _conflictedDocumentSessionDraftsJson ?? string.Empty);
                SessionState.SetString(
                    SessionDocumentDraftConflictPathsKey,
                    JsonConvert.SerializeObject(
                        _documentSessionDraftConflictPaths ?? Array.Empty<string>(),
                        CharacterActionAuthoringJsonService.JsonSettings));
                SessionState.SetString(
                    SessionSelectedProfileStableIdKey,
                    _documentSession?.Selection.ProfileStableId ?? _model?.Profile?.StableId ?? string.Empty);
                SessionState.SetString(
                    SessionSelectedActionSetStableIdKey,
                    _documentSession?.Selection.ActionSetStableId ?? _model?.ActionSet?.StableId ?? string.Empty);
                SessionState.SetBool(SessionProfileDirtyKey, _profileDirty);
                SessionState.SetBool(SessionActionSetDirtyKey, _actionSetDirty);
                SessionState.SetBool(SessionActionLibraryDirtyKey, _actionLibraryDirty);
                SessionState.SetBool(SessionAbilityEffectTimelineDirtyKey, _abilityEffectTimelineDirty);
                SessionState.SetBool(SessionValidationDirtyKey, _validationDirty);
                SessionState.SetString(
                    SessionPendingMechanicTuningDraftsKey,
                    string.Empty);

                if (_model != null)
                {
                    string modelJson = JsonConvert.SerializeObject(
                        CharacterActionAuthoringWindowSessionModel.FromModel(_model),
                        CharacterActionAuthoringJsonService.JsonSettings);
                    SessionState.SetString(SessionModelKey, modelJson);
                }
            }
            finally
            {
                _savingSession = false;
            }
        }

        private void RestorePendingAuthoringDraftsFromSession()
        {
            if (!string.IsNullOrWhiteSpace(_pendingResourceSessionDraftsJson))
            {
                RestoreResourceDraftsFromSession(_pendingResourceSessionDraftsJson);
                return;
            }

            RestoreLegacyPendingAuthoringDraftsFromSession();
        }

        private void RestoreLegacyPendingAuthoringDraftsFromSession()
        {
            try
            {
                string abilityJson = SessionState.GetString(SessionAbilityEffectTimelineDraftsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(abilityJson))
                    RestoreAbilityEffectTimelineDraftsFromSession(abilityJson);

                string mechanicJson = SessionState.GetString(SessionPendingMechanicTuningDraftsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(mechanicJson))
                {
                    RestorePendingMechanicTuningDrafts(
                        JsonConvert.DeserializeObject<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]>(
                            mechanicJson,
                            CharacterActionAuthoringJsonService.JsonSettings));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("恢复 Character Action 待保存草稿失败: " + ex.Message);
            }
        }

        private string SerializeResourceDraftsForSession()
        {
            var entries = new List<CharacterActionAuthoringResourceDraftSessionEntry>();
            var activeBaselineKeys = new HashSet<string>(StringComparer.Ordinal);

            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[] mechanicDrafts =
                CreatePendingMechanicTuningDrafts();
            Array.Sort(mechanicDrafts, CompareMechanicDrafts);
            if (mechanicDrafts.Length > 0)
            {
                entries.Add(CreateResourceDraftSessionEntry(
                    CharacterActionAuthoringResourceDraftKind.MechanicTuning,
                    ResolveAbilityAuthoringSourcePath(),
                    JArray.FromObject(
                        mechanicDrafts,
                        JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings)),
                    activeBaselineKeys));
            }

            if (_abilityEffectTimelineDirty)
            {
                string abilityDraftsJson = SerializeAbilityEffectTimelineDraftsForSession();
                JToken abilityDrafts = string.IsNullOrWhiteSpace(abilityDraftsJson)
                    ? new JArray()
                    : JToken.Parse(abilityDraftsJson);
                entries.Add(CreateResourceDraftSessionEntry(
                    CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline,
                    ResolveAbilityAuthoringSourcePath(),
                    abilityDrafts,
                    activeBaselineKeys));
            }

            PruneResourceDraftBaselineHashes(activeBaselineKeys);
            return entries.Count == 0
                ? string.Empty
                : CharacterActionAuthoringResourceDraftSessionPayload.Serialize(entries);
        }

        private CharacterActionAuthoringResourceDraftSessionEntry CreateResourceDraftSessionEntry(
            CharacterActionAuthoringResourceDraftKind kind,
            string sourcePath,
            JToken content,
            HashSet<string> activeBaselineKeys)
        {
            sourcePath = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
            string identity = CharacterActionAuthoringResourceDraftSessionEntry.CreateIdentity(kind, sourcePath);
            activeBaselineKeys.Add(identity);
            if (!_resourceDraftBaselineHashes.TryGetValue(identity, out string baselineHash))
            {
                baselineHash = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath);
                _resourceDraftBaselineHashes[identity] = baselineHash;
            }

            return new CharacterActionAuthoringResourceDraftSessionEntry(
                kind,
                sourcePath,
                baselineHash,
                content);
        }

        private void CaptureMechanicTuningDraftBaselineForSession()
        {
            CaptureResourceDraftBaselineForSession(
                CharacterActionAuthoringResourceDraftKind.MechanicTuning,
                ResolveAbilityAuthoringSourcePath());
        }

        private void MarkAbilityEffectTimelineDraftDirty()
        {
            CaptureResourceDraftBaselineForSession(
                CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline,
                ResolveAbilityAuthoringSourcePath());
            _abilityEffectTimelineDirty = true;
            TouchWorkbenchPreviewRevision();
        }

        private void CaptureResourceDraftBaselineForSession(
            CharacterActionAuthoringResourceDraftKind kind,
            string sourcePath)
        {
            sourcePath = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
            string identity = CharacterActionAuthoringResourceDraftSessionEntry.CreateIdentity(kind, sourcePath);
            if (!_resourceDraftBaselineHashes.ContainsKey(identity))
            {
                _resourceDraftBaselineHashes[identity] =
                    CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(sourcePath);
            }
        }

        private void ClearResourceDraftBaselinesForSession(CharacterActionAuthoringResourceDraftKind kind)
        {
            string prefix = ((int)kind).ToString() + ":";
            var remove = new List<string>();
            foreach (KeyValuePair<string, string> pair in _resourceDraftBaselineHashes)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    remove.Add(pair.Key);
            }
            for (int i = 0; i < remove.Count; i++)
                _resourceDraftBaselineHashes.Remove(remove[i]);
        }

        private void RestoreResourceDraftsFromSession(string payload)
        {
            if (!CharacterActionAuthoringResourceDraftSessionPayload.TryDeserialize(
                    payload,
                    out CharacterActionAuthoringResourceDraftSessionEntry[] entries))
            {
                SetResourceDraftConflicts(payload, new[] { "$resource-drafts" });
                _pendingResourceSessionDraftsJson = string.Empty;
                _abilityEffectTimelineDirty = false;
                return;
            }

            var restorable = new List<CharacterActionAuthoringResourceDraftSessionEntry>();
            var conflicts = new List<CharacterActionAuthoringResourceDraftSessionEntry>();
            bool containsAbilityTimeline = false;
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterActionAuthoringResourceDraftSessionEntry entry = entries[i];
                // M7: projectile drafts were historically owned by CharacterAction.
                // Ignore them when reading old session payloads; Projectile Authoring is now
                // the sole writer and restoring these entries would recreate split ownership.
                if (entry.Kind == CharacterActionAuthoringResourceDraftKind.Projectile)
                    continue;
                containsAbilityTimeline |= entry.Kind == CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline;
                string currentHash = CharacterActionAuthoringResourceDraftSessionPayload.ComputeSourceBaselineHash(
                    entry.SourcePath);
                if (string.Equals(currentHash, entry.BaselineSha256, StringComparison.Ordinal))
                    restorable.Add(entry);
                else
                    conflicts.Add(entry);
            }

            var mechanicDrafts = new List<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
            string abilityDraftsJson = string.Empty;
            bool restoredAbilityTimeline = false;
            try
            {
                JsonSerializer serializer = JsonSerializer.Create(CharacterActionAuthoringJsonService.JsonSettings);
                for (int i = 0; i < restorable.Count; i++)
                {
                    CharacterActionAuthoringResourceDraftSessionEntry entry = restorable[i];
                    if (entry.Kind == CharacterActionAuthoringResourceDraftKind.MechanicTuning)
                    {
                        CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[] values =
                            entry.Content.ToObject<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft[]>(serializer)
                            ?? Array.Empty<CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft>();
                        mechanicDrafts.AddRange(values);
                    }
                    else if (entry.Kind == CharacterActionAuthoringResourceDraftKind.AbilityEffectTimeline)
                    {
                        abilityDraftsJson = entry.Content.ToString(Formatting.None);
                        restoredAbilityTimeline = true;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("Failed to restore Character Action resource drafts: " + ex.Message);
                SetResourceDraftConflicts(payload, CollectResourceDraftPaths(entries));
                _pendingResourceSessionDraftsJson = string.Empty;
                _abilityEffectTimelineDirty = false;
                return;
            }

            if (mechanicDrafts.Count > 0)
                RestorePendingMechanicTuningDrafts(mechanicDrafts);
            if (restoredAbilityTimeline)
                RestoreAbilityEffectTimelineDraftsFromSession(abilityDraftsJson);
            if (containsAbilityTimeline)
                _abilityEffectTimelineDirty = restoredAbilityTimeline;

            for (int i = 0; i < restorable.Count; i++)
            {
                CharacterActionAuthoringResourceDraftSessionEntry entry = restorable[i];
                _resourceDraftBaselineHashes[entry.Identity] = entry.BaselineSha256;
            }

            if (conflicts.Count > 0)
            {
                SetResourceDraftConflicts(
                    CharacterActionAuthoringResourceDraftSessionPayload.Serialize(conflicts),
                    CollectResourceDraftPaths(conflicts));
            }
            _pendingResourceSessionDraftsJson = string.Empty;
        }

        private void SetResourceDraftConflicts(string payload, string[] conflictPaths)
        {
            _conflictedResourceDraftPayloadJson = payload ?? string.Empty;
            _resourceDraftConflictPaths = conflictPaths ?? Array.Empty<string>();
            UpdateCombinedConflictRecoveryState();
        }

        private static string[] CollectResourceDraftPaths(
            IReadOnlyList<CharacterActionAuthoringResourceDraftSessionEntry> entries)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                string path = entries[i]?.SourcePath;
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }
            var result = new string[paths.Count];
            paths.CopyTo(result);
            return result;
        }

        private string ResolveAbilityAuthoringSourcePath()
        {
            return string.IsNullOrWhiteSpace(_abilityAuthoringJsonPath)
                ? CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath
                : _abilityAuthoringJsonPath;
        }

        private void PruneResourceDraftBaselineHashes(HashSet<string> activeKeys)
        {
            var stale = new List<string>();
            foreach (KeyValuePair<string, string> pair in _resourceDraftBaselineHashes)
            {
                if (activeKeys == null || !activeKeys.Contains(pair.Key))
                    stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++)
                _resourceDraftBaselineHashes.Remove(stale[i]);
        }

        private static int CompareMechanicDrafts(
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft left,
            CharacterActionMechanicTuningAuthoringStore.CharacterActionMechanicTuningAuthoringDraft right)
        {
            return string.CompareOrdinal(left?.StableId, right?.StableId);
        }

        private bool RestoreDocumentSessionDraftsFromSession()
        {
            if (_documentSession == null || string.IsNullOrWhiteSpace(_pendingDocumentSessionDraftsJson))
                return false;

            string payload = _pendingDocumentSessionDraftsJson;
            if (!_documentSession.RestoreDirtyDrafts(payload, out string[] conflicts))
            {
                UnityEngine.Debug.LogWarning("恢复 Character Action 多文档草稿失败；已保留磁盘内容。" );
                return false;
            }

            _pendingDocumentSessionDraftsJson = string.Empty;
            _documentDraftConflictPaths = conflicts ?? Array.Empty<string>();
            _conflictedDocumentDraftPayloadJson = _documentDraftConflictPaths.Length > 0
                ? payload
                : string.Empty;
            UpdateCombinedConflictRecoveryState();
            RefreshLegacyDirtyFlagsFromDocumentSession();
            return true;
        }

        private void RestoreConflictRecoveryEnvelope(string payload, string[] legacyConflictPaths)
        {
            if (CharacterActionAuthoringDraftRecoveryEnvelope.TryDeserialize(
                    payload,
                    out CharacterActionAuthoringDraftRecoveryEnvelope envelope))
            {
                _conflictedDocumentDraftPayloadJson = envelope.DocumentDraftsJson;
                _conflictedResourceDraftPayloadJson = envelope.ResourceDraftsJson;
                _documentDraftConflictPaths = envelope.DocumentConflictPaths;
                _resourceDraftConflictPaths = envelope.ResourceConflictPaths;
            }
            else
            {
                _conflictedDocumentDraftPayloadJson = payload ?? string.Empty;
                _conflictedResourceDraftPayloadJson = string.Empty;
                _documentDraftConflictPaths = legacyConflictPaths ?? Array.Empty<string>();
                _resourceDraftConflictPaths = Array.Empty<string>();
            }
            UpdateCombinedConflictRecoveryState();
        }

        private void UpdateCombinedConflictRecoveryState()
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _documentDraftConflictPaths.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_documentDraftConflictPaths[i]))
                    paths.Add(_documentDraftConflictPaths[i]);
            }
            for (int i = 0; i < _resourceDraftConflictPaths.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_resourceDraftConflictPaths[i]))
                    paths.Add(_resourceDraftConflictPaths[i]);
            }
            _documentSessionDraftConflictPaths = new string[paths.Count];
            paths.CopyTo(_documentSessionDraftConflictPaths);
            _conflictedDocumentSessionDraftsJson = _documentSessionDraftConflictPaths.Length == 0
                ? string.Empty
                : CharacterActionAuthoringDraftRecoveryEnvelope.Serialize(
                    _conflictedDocumentDraftPayloadJson,
                    _conflictedResourceDraftPayloadJson,
                    _documentDraftConflictPaths,
                    _resourceDraftConflictPaths);
        }

        private void ClearConflictedDocumentSessionDrafts()
        {
            _conflictedDocumentSessionDraftsJson = string.Empty;
            _conflictedDocumentDraftPayloadJson = string.Empty;
            _conflictedResourceDraftPayloadJson = string.Empty;
            _documentDraftConflictPaths = Array.Empty<string>();
            _resourceDraftConflictPaths = Array.Empty<string>();
            _documentSessionDraftConflictPaths = Array.Empty<string>();
            SessionState.EraseString(SessionConflictedDocumentDraftsKey);
            SessionState.EraseString(SessionDocumentDraftConflictPathsKey);
            SaveSessionState();
            RefreshChrome();
            RefreshProblemsWorkbench();
        }

        private static AuthoringWorkspace WorkspaceFromLegacyTab(
            AuthoringTab tab,
            AuthoringEditorMode editorMode)
        {
            if (editorMode == AuthoringEditorMode.AbilityEffectTimeline || tab == AuthoringTab.Projectiles)
                return AuthoringWorkspace.SharedResources;
            if (tab == AuthoringTab.Actions || tab == AuthoringTab.Tracks)
                return AuthoringWorkspace.ActionComposition;
            return AuthoringWorkspace.Mappings;
        }

        private void RestoreSelectedActionFromSession()
        {
            if (_model == null || _model.Actions.Count == 0)
                return;

            int actionId = SessionState.GetInt(SessionSelectedActionKey, 0);
            if (actionId <= 0)
                return;

            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (_model.Actions[i]?.ActionId != actionId)
                    continue;

                _model.SelectedActionIndex = i;
                return;
            }
        }

        private void MergeSessionModelWithCurrentSources()
        {
            if (_model == null)
                return;

            bool changed = MergeMissingActionsFromSource();
            changed |= MergeMissingActionSetBindingsFromSource();
            if (!changed)
                return;

            _model.ReferenceContext = CharacterActionAuthoringReferenceContext.CreateFromModel(_model);
            _model.Normalize();
            _validationDirty = true;
            SaveSessionState();
        }

        private bool MergeMissingActionsFromSource()
        {
            int sourceIndex = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                _actionLibrarySources,
                _actionLibrarySourceDirectory);
            if (sourceIndex < 0)
                return false;

            CharacterActionAuthoringImportResult import;
            try
            {
                import = CharacterActionAuthoringSourceCatalog.LoadActionLibrary(_actionLibrarySources[sourceIndex]);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("同步动作库源新增动作失败: " + ex.Message);
                return false;
            }

            if (import?.Success != true || import.Model == null)
                return false;

            bool changed = false;
            CharacterActionJsonDto[] sourceActions = import.Model.Actions.ToArray();
            for (int i = 0; i < sourceActions.Length; i++)
            {
                CharacterActionJsonDto sourceAction = sourceActions[i];
                if (sourceAction == null || HasAction(sourceAction.ActionId))
                    continue;

                _model.Actions.Add(CloneJsonDto(sourceAction));
                changed = true;
            }

            return changed;
        }

        private bool MergeMissingActionSetBindingsFromSource()
        {
            int sourceIndex = CharacterActionAuthoringSourceCatalog.FindIndexByDirectory(
                _actionSetSources,
                _actionSetSourceDirectory);
            if (sourceIndex < 0)
                return false;

            CharacterActionAuthoringImportResult import;
            try
            {
                import = CharacterActionAuthoringSourceCatalog.LoadActionSet(_actionSetSources[sourceIndex]);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("同步 ActionSet 源新增绑定失败: " + ex.Message);
                return false;
            }

            if (import?.Success != true || import.Model?.ActionSet == null || _model.ActionSet == null)
                return false;

            bool changed = false;
            CharacterActionBindingJsonDto[] commandBindings =
                import.Model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < commandBindings.Length; i++)
            {
                CharacterActionBindingJsonDto binding = commandBindings[i];
                if (binding == null || HasCommandBinding(binding.IntentId, binding.ActionId))
                    continue;

                _model.ActionSet.CommandBindings = Append(
                    _model.ActionSet.CommandBindings,
                    CloneJsonDto(binding));
                changed = true;
            }

            CharacterAbilityActionBindingJsonDto[] abilityBindings =
                import.Model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < abilityBindings.Length; i++)
            {
                CharacterAbilityActionBindingJsonDto binding = abilityBindings[i];
                if (binding == null || HasAbilityBinding(binding.AbilityId, binding.ActionId))
                    continue;

                _model.ActionSet.AbilityBindings = Append(
                    _model.ActionSet.AbilityBindings,
                    CloneJsonDto(binding));
                changed = true;
            }

            return changed;
        }

        private bool HasAction(int actionId)
        {
            for (int i = 0; i < _model.Actions.Count; i++)
            {
                if (_model.Actions[i]?.ActionId == actionId)
                    return true;
            }

            return false;
        }

        private bool HasCommandBinding(int intentId, int actionId)
        {
            CharacterActionBindingJsonDto[] bindings =
                _model.ActionSet?.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterActionBindingJsonDto binding = bindings[i];
                if (binding?.IntentId == intentId
                    && binding?.ActionId == actionId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAbilityBinding(int abilityId, int actionId)
        {
            CharacterAbilityActionBindingJsonDto[] bindings =
                _model.ActionSet?.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterAbilityActionBindingJsonDto binding = bindings[i];
                if (binding != null
                    && binding.AbilityId == abilityId
                    && binding.ActionId == actionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static T[] Append<T>(T[] source, T value)
        {
            source = source ?? Array.Empty<T>();
            var result = new T[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[result.Length - 1] = value;
            return result;
        }

        private static T CloneJsonDto<T>(T value)
        {
            if (value == null)
                return default;

            string json = JsonConvert.SerializeObject(value, CharacterActionAuthoringJsonService.JsonSettings);
            return JsonConvert.DeserializeObject<T>(json, CharacterActionAuthoringJsonService.JsonSettings);
        }

        private sealed class CharacterActionAuthoringWindowSessionModel
        {
            public CharacterActionProfileJsonDto Profile { get; set; }
            public CharacterActionSetJsonDto ActionSet { get; set; }
            public List<CharacterActionJsonDto> Actions { get; set; } = new List<CharacterActionJsonDto>();
            public CharacterActionAuthoringReferenceContext ReferenceContext { get; set; }
            public int SelectedActionIndex { get; set; }

            public static CharacterActionAuthoringWindowSessionModel FromModel(CharacterActionAuthoringModel model)
            {
                model = model ?? CharacterActionAuthoringModel.CreateDefault();
                return new CharacterActionAuthoringWindowSessionModel
                {
                    Profile = model.Profile,
                    ActionSet = model.ActionSet,
                    Actions = new List<CharacterActionJsonDto>(model.Actions),
                    ReferenceContext = model.ReferenceContext,
                    SelectedActionIndex = model.SelectedActionIndex,
                };
            }

            public CharacterActionAuthoringModel ToModel()
            {
                var model = new CharacterActionAuthoringModel
                {
                    Profile = Profile ?? new CharacterActionProfileJsonDto(),
                    ActionSet = ActionSet ?? new CharacterActionSetJsonDto(),
                    ReferenceContext = ReferenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault(),
                    SelectedActionIndex = SelectedActionIndex,
                };
                if (Actions != null)
                    model.Actions.AddRange(Actions);
                model.Normalize();
                return model;
            }
        }
    }

    internal enum CharacterActionAuthoringResourceDraftKind
    {
        Projectile = 0,
        MechanicTuning = 1,
        AbilityEffectTimeline = 2,
    }

    internal sealed class CharacterActionAuthoringResourceDraftSessionEntry
    {
        internal CharacterActionAuthoringResourceDraftSessionEntry(
            CharacterActionAuthoringResourceDraftKind kind,
            string sourcePath,
            string baselineSha256,
            JToken content)
        {
            Kind = kind;
            SourcePath = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
            BaselineSha256 = baselineSha256 ?? string.Empty;
            Content = content?.DeepClone() ?? JValue.CreateNull();
        }

        internal CharacterActionAuthoringResourceDraftKind Kind { get; }
        internal string SourcePath { get; }
        internal string BaselineSha256 { get; }
        internal JToken Content { get; }
        internal string Identity => CreateIdentity(Kind, SourcePath);

        internal static string CreateIdentity(
            CharacterActionAuthoringResourceDraftKind kind,
            string sourcePath)
        {
            return ((int)kind).ToString() + ":" + CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
        }
    }

    internal static class CharacterActionAuthoringResourceDraftSessionPayload
    {
        private const int PayloadVersion = 1;

        internal static string Serialize(
            IReadOnlyList<CharacterActionAuthoringResourceDraftSessionEntry> entries)
        {
            var ordered = new List<CharacterActionAuthoringResourceDraftSessionEntry>(entries?.Count ?? 0);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i] != null)
                    ordered.Add(entries[i]);
            }
            ordered.Sort(CompareEntries);

            var array = new JArray();
            for (int i = 0; i < ordered.Count; i++)
            {
                CharacterActionAuthoringResourceDraftSessionEntry entry = ordered[i];
                array.Add(new JObject
                {
                    ["kind"] = (int)entry.Kind,
                    ["sourcePath"] = entry.SourcePath,
                    ["baselineSha256"] = entry.BaselineSha256,
                    ["content"] = entry.Content.DeepClone(),
                });
            }

            return new JObject
            {
                ["version"] = PayloadVersion,
                ["entries"] = array,
            }.ToString(Formatting.None);
        }

        internal static bool TryDeserialize(
            string json,
            out CharacterActionAuthoringResourceDraftSessionEntry[] entries)
        {
            entries = Array.Empty<CharacterActionAuthoringResourceDraftSessionEntry>();
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                JObject root = JObject.Parse(json);
                if ((int?)root["version"] != PayloadVersion || !(root["entries"] is JArray array))
                    return false;

                var parsed = new List<CharacterActionAuthoringResourceDraftSessionEntry>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    if (!(array[i] is JObject item))
                        return false;
                    int? rawKind = (int?)item["kind"];
                    string sourcePath = (string)item["sourcePath"];
                    string baseline = (string)item["baselineSha256"];
                    JToken content = item["content"];
                    if (!rawKind.HasValue
                        || !Enum.IsDefined(typeof(CharacterActionAuthoringResourceDraftKind), rawKind.Value)
                        || string.IsNullOrWhiteSpace(sourcePath)
                        || string.IsNullOrWhiteSpace(baseline)
                        || content == null)
                    {
                        return false;
                    }
                    parsed.Add(new CharacterActionAuthoringResourceDraftSessionEntry(
                        (CharacterActionAuthoringResourceDraftKind)rawKind.Value,
                        sourcePath,
                        baseline,
                        content));
                }

                entries = parsed.ToArray();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        internal static string ComputeSourceBaselineHash(string sourcePath)
        {
            string normalized = CharacterActionAuthoringSourceEntry.NormalizeAssetPath(sourcePath);
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    return ComputeTextHash("missing:" + normalized);
                using (FileStream stream = File.OpenRead(sourcePath))
                using (SHA256 sha256 = SHA256.Create())
                    return ToHex(sha256.ComputeHash(stream));
            }
            catch (IOException)
            {
                return ComputeTextHash("unreadable:" + normalized);
            }
            catch (UnauthorizedAccessException)
            {
                return ComputeTextHash("unreadable:" + normalized);
            }
        }

        private static int CompareEntries(
            CharacterActionAuthoringResourceDraftSessionEntry left,
            CharacterActionAuthoringResourceDraftSessionEntry right)
        {
            int kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
                return kind;
            int path = string.CompareOrdinal(left.SourcePath, right.SourcePath);
            if (path != 0)
                return path;
            return string.CompareOrdinal(
                (string)left.Content?["stableId"],
                (string)right.Content?["stableId"]);
        }

        private static string ComputeTextHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder((bytes?.Length ?? 0) * 2);
            for (int i = 0; bytes != null && i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    internal sealed class CharacterActionAuthoringDraftRecoveryEnvelope
    {
        private const int RecoveryVersion = 1;

        private CharacterActionAuthoringDraftRecoveryEnvelope(
            string documentDraftsJson,
            string resourceDraftsJson,
            string[] documentConflictPaths,
            string[] resourceConflictPaths)
        {
            DocumentDraftsJson = documentDraftsJson ?? string.Empty;
            ResourceDraftsJson = resourceDraftsJson ?? string.Empty;
            DocumentConflictPaths = documentConflictPaths ?? Array.Empty<string>();
            ResourceConflictPaths = resourceConflictPaths ?? Array.Empty<string>();
        }

        internal string DocumentDraftsJson { get; }
        internal string ResourceDraftsJson { get; }
        internal string[] DocumentConflictPaths { get; }
        internal string[] ResourceConflictPaths { get; }

        internal static string Serialize(
            string documentDraftsJson,
            string resourceDraftsJson,
            string[] documentConflictPaths,
            string[] resourceConflictPaths)
        {
            return new JObject
            {
                ["recoveryVersion"] = RecoveryVersion,
                // Keep the original payload byte-for-byte. A malformed recovery payload is
                // still valuable diagnostic data and must remain exportable instead of making
                // the recovery envelope itself fail to serialize.
                ["documentDrafts"] = documentDraftsJson ?? string.Empty,
                ["resourceDrafts"] = resourceDraftsJson ?? string.Empty,
                ["documentConflictPaths"] = new JArray(documentConflictPaths ?? Array.Empty<string>()),
                ["resourceConflictPaths"] = new JArray(resourceConflictPaths ?? Array.Empty<string>()),
            }.ToString(Formatting.Indented);
        }

        internal static bool TryDeserialize(
            string json,
            out CharacterActionAuthoringDraftRecoveryEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                JObject root = JObject.Parse(json);
                if ((int?)root["recoveryVersion"] != RecoveryVersion)
                    return false;
                envelope = new CharacterActionAuthoringDraftRecoveryEnvelope(
                    TokenToJson(root["documentDrafts"]),
                    TokenToJson(root["resourceDrafts"]),
                    root["documentConflictPaths"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                    root["resourceConflictPaths"]?.ToObject<string[]>() ?? Array.Empty<string>());
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string TokenToJson(JToken token)
        {
            return token == null || token.Type == JTokenType.Null
                ? string.Empty
                : token.Type == JTokenType.String
                    ? token.Value<string>() ?? string.Empty
                    : token.ToString(Formatting.None);
        }
    }
}
