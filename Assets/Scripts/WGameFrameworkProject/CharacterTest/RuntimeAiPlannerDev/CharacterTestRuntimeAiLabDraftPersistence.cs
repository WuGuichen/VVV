using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftPersistence
    {
        private const double AutosaveDelaySeconds = 0.5d;
        internal const int MaximumRetainedWorkspaces = 5;
        private readonly string _draftsRoot;
        private readonly string _recoveryRoot;
        private readonly bool _pruneInactiveWorkspaces;
        // Diagnostics for separating UI projection from explicit persistence work.
        internal static long DirectoryLoadCount { get; private set; }

        private bool _updateLatestWorkspacePointerOnFlush = true;
        private CharacterTestRuntimeAiLabDraftWorkspace _workspace;
        private double _saveDueAt = double.MaxValue;
        private string _lastAutomaticSaveWorkspace;
        private int _lastAutomaticSaveRevision = -1;

        public CharacterTestRuntimeAiLabDraftPersistence(
            string draftsRoot = null,
            string recoveryRoot = null)
        {
            bool usesSharedAuthoringRoot = string.IsNullOrWhiteSpace(draftsRoot);
            _draftsRoot = usesSharedAuthoringRoot
                ? Path.Combine("Authoring", "RuntimeAiLab", "Drafts")
                : draftsRoot;
            _recoveryRoot = !string.IsNullOrWhiteSpace(recoveryRoot)
                ? recoveryRoot
                : usesSharedAuthoringRoot
                    ? Path.Combine("UserSettings", "RuntimeAiLab", "DraftRecovery")
                    : _draftsRoot;
            _pruneInactiveWorkspaces = !usesSharedAuthoringRoot;
        }

        public CharacterTestRuntimeAiLabDraftWorkspace Workspace => _workspace;
        public string LastStatus { get; private set; } = string.Empty;
        internal bool LastSaveWasAutomatic => _workspace != null
            && _lastAutomaticSaveWorkspace == _workspace.WorkspaceId
            && _lastAutomaticSaveRevision == _workspace.LastSavedRevision;
        internal string DraftsRoot => Path.GetFullPath(_draftsRoot);
        internal string RecoveryRoot => Path.GetFullPath(_recoveryRoot);
        internal bool UsesSharedAuthoringRoot => !_pruneInactiveWorkspaces;

        /// <summary>Creates and activates a new Lab draft from the supplied setup without restoring local history.</summary>
        /// <remarks>LowFreqAlloc: Lab setup initialization only.</remarks>
        public CharacterTestRuntimeAiLabDraftWorkspace CreateFromCurrent(
            CharacterTestArenaMatchConfig source)
        {
            _workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(source);
            LastStatus = "Created Runtime AI Lab workspace draft from the current setup.";
            MarkDirty();
            return _workspace;
        }

        internal CharacterTestRuntimeAiLabDraftWorkspace CreateFromWorkspace(
            CharacterTestRuntimeAiLabDraftWorkspace source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            _workspace = source.ForkForIsolatedTest();
            LastStatus = "Created isolated Runtime AI Lab test workspace from the current setup.";
            MarkDirty();
            return _workspace;
        }

        /// <summary>Loads the latest persisted workspace as a recovery source without activating it.</summary>
        /// <remarks>LowFreqAlloc: Lab setup initialization only.</remarks>
        public bool TryLoadLatest(
            out CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string message)
        {
            workspace = null;
            string latestPath = Path.Combine(_draftsRoot, "latest-workspace.txt");
            if (!File.Exists(latestPath))
            {
                message = "No Runtime AI Lab recovery draft is available.";
                return false;
            }

            string workspaceId = File.ReadAllText(latestPath).Trim();
            if (!Guid.TryParse(workspaceId, out _))
            {
                message = "The latest Runtime AI Lab workspace pointer is invalid.";
                return false;
            }

            return TryLoad(workspaceId, out workspace, out message);
        }

        public CharacterTestRuntimeAiLabDraftWorkspace OpenOrCreate(CharacterTestArenaMatchConfig source)
        {
            string latestPath = Path.Combine(_draftsRoot, "latest-workspace.txt");
            if (File.Exists(latestPath))
            {
                string workspaceId = File.ReadAllText(latestPath).Trim();
                if (Guid.TryParse(workspaceId, out _)
                    && TryLoad(workspaceId, out CharacterTestRuntimeAiLabDraftWorkspace loaded, out string loadMessage))
                {
                    _workspace = loaded;
                    LastStatus = loadMessage;
                    if (_workspace.Dirty)
                        MarkDirty();
                    return _workspace;
                }
            }

            _workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(source);
            LastStatus = "Created Runtime AI Lab workspace draft.";
            MarkDirty();
            return _workspace;
        }

        /// <summary>
        /// Replaces the active workspace after a project-catalog operation rebuilt the Lab draft.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit authoring catalog mutation only.</remarks>
        internal void AdoptWorkspace(CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));

            _workspace = workspace;
            LastStatus = "Runtime AI Lab workspace replaced from the current project catalog.";
            MarkDirty();
        }

        public void MarkDirty()
        {
            _saveDueAt = Time.realtimeSinceStartupAsDouble + AutosaveDelaySeconds;
        }

        public void TickAutosave()
        {
            if (_workspace == null || !_workspace.Dirty || Time.realtimeSinceStartupAsDouble < _saveDueAt)
                return;
            FlushCore(automatic: true);
        }

        public bool Flush() => FlushCore(automatic: false);

        private bool FlushCore(bool automatic)
        {
            if (_workspace == null)
            {
                LastStatus = "Runtime AI Lab workspace is unavailable.";
                return false;
            }
            if (_workspace.IsReadOnly)
            {
                LastStatus = "Runtime AI Lab workspace is read-only and was not saved.";
                return false;
            }
            if (!TryValidateWorkspaceForPersistence(_workspace, out string validationMessage))
            {
                // Keep the in-memory edit available for correction, but do not repeatedly retry the
                // same invalid recovery write every Update. The next real mutation calls MarkDirty.
                _saveDueAt = double.MaxValue;
                LastStatus = "Runtime AI Lab draft was not saved: " + validationMessage;
                return false;
            }

            try
            {
                string workspaceDirectory = GetWorkspaceDirectory(_workspace.WorkspaceId);
                if (!automatic)
                    CharacterTestRuntimeAiLabDraftNumericIdentities.PrepareForSave(_workspace);
                string stagingDirectory = GetStagingDirectory(_workspace.WorkspaceId);
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
                Directory.CreateDirectory(Path.Combine(stagingDirectory, "groups"));
                Directory.CreateDirectory(Path.Combine(stagingDirectory, "cards"));

                var groupRefs = new JArray();
                foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in SortedGroups(_workspace.Groups))
                {
                    string json = WriteGroup(group);
                    string relative = "groups/" + group.GroupDraftId + ".json";
                    File.WriteAllText(Path.Combine(stagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), json, Encoding.UTF8);
                    groupRefs.Add(WriteReference(group.GroupDraftId, group.CurrentRevision, relative, Hash(json)));
                }

                var cardRefs = new JArray();
                foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in SortedCards(_workspace.Cards))
                {
                    string json = WriteCard(card);
                    string relative = "cards/" + card.CardDraftId + ".json";
                    File.WriteAllText(Path.Combine(stagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar)), json, Encoding.UTF8);
                    cardRefs.Add(WriteReference(card.CardDraftId, card.CurrentRevision, relative, Hash(json)));
                }

                var tacticalRefs = WriteTacticalDefinitions(stagingDirectory, _workspace);
                string workspaceJson = WriteWorkspace(_workspace, groupRefs, cardRefs, tacticalRefs);
                File.WriteAllText(Path.Combine(stagingDirectory, "workspace.json"), workspaceJson, Encoding.UTF8);
                if (!TryLoadDirectory(stagingDirectory, out _, out string stagingValidationMessage, markRecovered: false))
                    throw new InvalidDataException("Staged Runtime AI Lab workspace failed validation: " + stagingValidationMessage);

                PreservePrevious(
                    workspaceDirectory,
                    GetPreviousDirectory(_workspace.WorkspaceId));
                CommitStaging(stagingDirectory, workspaceDirectory);
                if (_workspace.HasCompositionData && !TryLoadDirectory(workspaceDirectory, out _, out string committedIssue, markRecovered: false))
                    throw new InvalidDataException("Committed tactical workspace failed reread: " + committedIssue);
                Directory.CreateDirectory(_draftsRoot);
                if (_updateLatestWorkspacePointerOnFlush)
                {
                    File.WriteAllText(
                        Path.Combine(_draftsRoot, "latest-workspace.txt"),
                        _workspace.WorkspaceId,
                        Encoding.UTF8);
                }
                _workspace.LastSavedRevision = _workspace.CurrentRevision;
                _lastAutomaticSaveWorkspace = automatic ? _workspace.WorkspaceId : null;
                _lastAutomaticSaveRevision = automatic ? _workspace.LastSavedRevision : -1;
                _workspace.UpdatedUtc = DateTime.UtcNow;
                _saveDueAt = double.MaxValue;
                string retentionDiagnostic = _pruneInactiveWorkspaces
                    ? PruneInactiveWorkspaces(_workspace.WorkspaceId)
                    : string.Empty;
                LastStatus = retentionDiagnostic.Length == 0
                    ? "Runtime AI Lab draft saved."
                    : "Runtime AI Lab draft saved. " + retentionDiagnostic;
                return true;
            }
            catch (Exception exception)
            {
                LastStatus = "Runtime AI Lab draft save failed: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Bounds local recovery storage after a successful save. Only direct child directories
        /// named as workspace GUIDs are eligible; the active workspace and directory links are
        /// never removed.
        /// </summary>
        /// <remarks>LowFreqAlloc: post-save local retention maintenance only.</remarks>
        private string PruneInactiveWorkspaces(string activeWorkspaceId)
        {
            try
            {
                string root = Path.GetFullPath(_draftsRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(root))
                    return string.Empty;

                var inactive = new List<DirectoryInfo>();
                string[] directories = Directory.GetDirectories(root, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < directories.Length; i++)
                {
                    string fullPath = Path.GetFullPath(directories[i]);
                    var directory = new DirectoryInfo(fullPath);
                    if (directory.Parent == null
                        || !string.Equals(directory.Parent.FullName, root, StringComparison.OrdinalIgnoreCase)
                        || !Guid.TryParse(directory.Name, out Guid workspaceId)
                        || string.Equals(
                            workspaceId.ToString("D", CultureInfo.InvariantCulture),
                            activeWorkspaceId,
                            StringComparison.OrdinalIgnoreCase)
                        || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    inactive.Add(directory);
                }

                inactive.Sort((left, right) =>
                {
                    int modified = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
                    return modified != 0
                        ? modified
                        : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
                });

                int inactiveRetention = Math.Max(0, MaximumRetainedWorkspaces - 1);
                int removed = 0;
                for (int i = inactiveRetention; i < inactive.Count; i++)
                {
                    inactive[i].Delete(recursive: true);
                    removed++;
                }

                return removed > 0
                    ? "Removed " + removed.ToString(CultureInfo.InvariantCulture)
                        + " expired workspace draft(s)."
                    : string.Empty;
            }
            catch (Exception exception)
            {
                return "Workspace retention cleanup failed: " + exception.Message;
            }
        }

        public bool TryLoad(
            string workspaceId,
            out CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string message)
        {
            workspace = null;
            string current = GetWorkspaceDirectory(workspaceId);
            if (TryLoadDirectory(current, out workspace, out message, markRecovered: false))
                return true;

            string currentFailure = message;
            string previous = GetPreviousDirectory(workspaceId);
            if (TryLoadDirectory(previous, out workspace, out message, markRecovered: true))
            {
                workspace.Diagnostic = "Recovered from previous. Current draft remains preserved. " + currentFailure;
                message = workspace.Diagnostic;
                return true;
            }

            message = currentFailure + " Previous recovery also failed: " + message;
            return false;
        }

        private bool TryLoadDirectory(
            string directory,
            out CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string message,
            bool markRecovered)
        {
            using var startupProfile = CharacterTestStartupProfiler.LoadDraftDirectory.Auto();
            DirectoryLoadCount++;
            workspace = null;
            try
            {
                string workspacePath = Path.Combine(directory, "workspace.json");
                if (!File.Exists(workspacePath))
                {
                    message = "workspace.json is missing.";
                    return false;
                }

                JObject root = JObject.Parse(File.ReadAllText(workspacePath));
                int schemaVersion = ReadSchemaVersion((string)root["schema"]);
                bool readOnly = schemaVersion > 4;
                if (schemaVersion <= 0)
                {
                    message = "Workspace schema is unsupported.";
                    return false;
                }

                workspace = new CharacterTestRuntimeAiLabDraftWorkspace(
                    RequiredGuid(root, "workspaceId"),
                    (string)root["displayName"]);
                workspace.CreatedUtc = ReadUtc(root, "createdUtc", DateTime.UtcNow);
                workspace.UpdatedUtc = ReadUtc(root, "updatedUtc", workspace.CreatedUtc);
                workspace.CurrentRevision = Math.Max(1, (int?)root["currentRevision"] ?? 1);
                workspace.LastSavedRevision = workspace.CurrentRevision;
                workspace.LastExplicitSaveRevision = Math.Max(
                    0,
                    (int?)root["lastExplicitSaveRevision"] ?? workspace.CurrentRevision);
                workspace.LastCompiledRevision = Math.Max(0, (int?)root["lastCompiledRevision"] ?? 0);
                workspace.LastAppliedRevision = Math.Max(0, (int?)root["lastAppliedRevision"] ?? 0);
                workspace.FrameLimit = Math.Max(1, Math.Min(
                    CharacterTestRuntimeAiPlannerDevTimeline.DefaultCapacity,
                    (int?)root["frameLimit"]
                        ?? CharacterTestRuntimeAiPlannerDevSceneController.DefaultRuntimeAiLabFrameLimit));
                workspace.IsReadOnly = readOnly;
                workspace.RecoveredFromPrevious = markRecovered;

                ReadReferencedGroups(directory, root["groups"] as JArray, workspace);
                ReadTacticalDefinitions(directory, root["tacticalDefinitions"], schemaVersion, workspace);
                ReadReferencedCards(directory, root["cards"] as JArray, workspace);
                if (!TryValidateWorkspaceForPersistence(workspace, out string validationMessage))
                {
                    message = "Persisted Runtime AI Lab draft is not runnable: " + validationMessage;
                    workspace = null;
                    return false;
                }
                ReadSlots(root["slots"] as JArray, workspace);
                ValidateReferences(workspace);
                bool sharedCardsMigrated = workspace.CanonicalizeSharedBehaviorCards();
                bool activationContractMigrated = workspace.RepairLegacyBehaviorActivationConditions();
                bool generatedIntervalsMigrated = workspace.RepairGeneratedCompatibilityV7Intervals();
                bool generatedCoreArtifactsRemoved = workspace.RepairGeneratedCoreContractArtifacts();
                message = markRecovered
                    ? "Recovered from previous Runtime AI Lab draft."
                    : readOnly
                        ? "Opened newer Runtime AI Lab draft read-only."
                        : sharedCardsMigrated
                            || generatedCoreArtifactsRemoved
                            || activationContractMigrated
                            || generatedIntervalsMigrated
                            ? workspace.Diagnostic
                            : "Loaded Runtime AI Lab draft.";
                return true;
            }
            catch (Exception exception)
            {
                workspace = null;
                message = exception.Message;
                return false;
            }
        }

        private static bool TryValidateWorkspaceForPersistence(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string message)
        {
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in workspace.Cards.Values)
            {
                if (card == null || card.Cost < 0 || card.Cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost
                    || float.IsNaN(card.CooldownSeconds) || float.IsInfinity(card.CooldownSeconds)
                    || card.CooldownSeconds < 0f || card.CooldownSeconds * 60d >= int.MaxValue)
                { message = "Invalid author cost or cooldown."; return false; }
                if (card?.HasCompositionSource == true)
                {
                    try { CharacterTestTacticalCompositionJson.Read(card.CompositionSourceJson); }
                    catch (Exception error) { message = error.Message; return false; }
                    continue; // Missing logical references remain repairable; manifest/file integrity is checked on reread.
                }
                if (card?.HasTacticalSource == true)
                {
                    if (!CharacterTestTacticalAuthoringJson.TryRead(card.TacticalSourceJson, out _, out message)) return false;
                    continue; // Readable authoring errors are saved unchanged; compile/start remain blocked.
                }
                if (card?.LogicPayload == null)
                {
                    message = "behavior card payload is missing.";
                    return false;
                }

                CharacterTestRuntimeAiLabCardDiagnostic? firstError = null;
                for (int timelineIndex = 0;
                     timelineIndex < card.LogicPayload.Timelines.Count && !firstError.HasValue;
                     timelineIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                        card.LogicPayload.Timelines[timelineIndex];
                    for (int eventIndex = 0;
                         timeline != null && eventIndex < timeline.Events.Count;
                         eventIndex++)
                    {
                        CharacterTestRuntimeAiLabBehaviorRecipeEvent evt = timeline.Events[eventIndex];
                        string path = "$.timelines[" + timelineIndex + "].events[" + eventIndex + "]";
                        if (evt?.Operation == null
                            || evt.Operation.Kind ==
                                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured)
                        {
                            firstError = new CharacterTestRuntimeAiLabCardDiagnostic(
                                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                                "CARD_V6_EVENT_OPERATION_MISSING",
                                path + ".operation",
                                "Event operation is not configured.");
                            break;
                        }
                        if (evt.Operation.Kind ==
                                CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                            && !evt.Operation.CharacterActionId.IsValid)
                        {
                            firstError = new CharacterTestRuntimeAiLabCardDiagnostic(
                                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                                "CARD_V6_EVENT_ACTION_REQUIRED",
                                path + ".operation.characterActionId",
                                "CharacterAction reference is required.");
                            break;
                        }
                        if (TryFindMissingConditionCharacterAction(
                                evt.ConditionRoot,
                                path + ".conditionRoot",
                                out CharacterTestRuntimeAiLabCardDiagnostic conditionError))
                        {
                            firstError = conditionError;
                            break;
                        }
                        if (evt.DurationReviewRequired)
                        {
                            firstError = new CharacterTestRuntimeAiLabCardDiagnostic(
                                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                                "CARD_V7_EVENT_DURATION_REVIEW_REQUIRED",
                                path + ".durationReviewRequired",
                                "Event duration requires confirmation.");
                            break;
                        }
                    }
                }
                if (!firstError.HasValue)
                    continue;

                string cardName = string.IsNullOrWhiteSpace(card.DisplayName)
                    ? "unnamed behavior card"
                    : card.DisplayName;
                message = "behavior card '" + cardName + "' has " + firstError.Value.Code
                    + " at " + firstError.Value.Path + ".";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryFindMissingConditionCharacterAction(
            CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node,
            string path,
            out CharacterTestRuntimeAiLabCardDiagnostic diagnostic)
        {
            if (node?.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.FactPredicate
                && node.Predicate?.Fact == CharacterTestRuntimeAiLabCardConditionFact.SelfCanTransitionToAction
                && !node.Predicate.CharacterActionId.IsValid)
            {
                diagnostic = new CharacterTestRuntimeAiLabCardDiagnostic(
                    CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                    "CARD_V6_CONDITION_ACTION_REQUIRED",
                    path + ".predicate.characterActionId",
                    "Condition CharacterAction reference is required.");
                return true;
            }
            for (int index = 0; node != null && index < node.Children.Count; index++)
            {
                if (TryFindMissingConditionCharacterAction(
                        node.Children[index],
                        path + ".children[" + index + "]",
                        out diagnostic))
                {
                    return true;
                }
            }
            diagnostic = default;
            return false;
        }

        private static string WriteWorkspace(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            JArray groupRefs,
            JArray cardRefs,
            JArray tacticalRefs)
        {
            var slots = new JArray();
            for (int i = 0; i < workspace.Slots.Count; i++)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = workspace.Slots[i];
                if (slot == null)
                    continue;
                slots.Add(new JObject
                {
                    ["teamId"] = slot.TeamId,
                    ["slotIndex"] = slot.SlotIndex,
                    ["enabled"] = slot.Enabled,
                    ["characterId"] = slot.CharacterId,
                    ["groupDraftId"] = slot.GroupDraftId,
                    ["arenaBehaviorGroupDraftId"] = slot.ArenaBehaviorGroupDraftId,
                    ["spawnX"] = slot.SpawnX,
                    ["spawnZ"] = slot.SpawnZ
                });
            }

            var root = new JObject
            {
                ["schema"] = workspace.HasCompositionData ? "mx.character-test.runtime-ai-lab.workspace.v4" : CharacterTestRuntimeAiLabDraftWorkspace.Schema,
                ["workspaceId"] = workspace.WorkspaceId,
                ["displayName"] = workspace.DisplayName,
                ["createdUtc"] = workspace.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
                ["updatedUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["currentRevision"] = workspace.CurrentRevision,
                ["lastExplicitSaveRevision"] = workspace.LastExplicitSaveRevision,
                ["lastCompiledRevision"] = workspace.LastCompiledRevision,
                ["lastAppliedRevision"] = workspace.LastAppliedRevision,
                ["frameLimit"] = workspace.FrameLimit,
                ["slots"] = slots,
                ["groups"] = groupRefs,
                ["cards"] = cardRefs
            };
            if (workspace.HasCompositionData) root["tacticalDefinitions"] = tacticalRefs;
            return root.ToString(Formatting.Indented);
        }

        private static string WriteGroup(CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            var entries = new JArray();
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                var parameters = new JArray();
                foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in entry.RuntimeEntry.Parameters)
                {
                    parameters.Add(new JObject
                    {
                        ["key"] = pair.Key,
                        ["kind"] = pair.Value.Kind.ToString(),
                        ["boolValue"] = pair.Value.BoolValue,
                        ["intValue"] = pair.Value.IntValue,
                        ["floatValue"] = pair.Value.FloatValue
                    });
                }
                entries.Add(new JObject
                {
                    ["entryId"] = entry.EntryId,
                    ["cardDraftId"] = entry.CardDraftId,
                    ["numericId"] = entry.RuntimeEntry.NumericId,
                    ["definitionNumericId"] = entry.RuntimeEntry.DefinitionNumericId,
                    ["behaviorId"] = entry.BehaviorId,
                    ["order"] = entry.Order,
                    ["disabled"] = entry.Disabled,
                    ["responsibilityKinds"] = WriteResponsibilities(entry.ResponsibilityKinds),
                    ["parameterOverrides"] = parameters
                });
            }

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = group.Profile;
            var root = new JObject
            {
                ["schema"] = CharacterTestRuntimeAiLabBehaviorGroupDraft.Schema,
                ["groupDraftId"] = group.GroupDraftId,
                ["publishedStableId"] = group.PublishedStableId,
                ["displayName"] = group.DisplayName,
                ["currentRevision"] = group.CurrentRevision,
                ["sourceProjectRevision"] = group.SourceProjectRevision,
                ["sourceProjectContentHash"] = group.SourceProjectContentHash,
                ["sourceProjectPath"] = group.SourceProjectPath,
                ["sourceCatalogFileHash"] = group.SourceCatalogFileHash,
                ["targetSelectionMode"] = profile.TargetSelectionMode.ToString(),
                ["attackRange"] = profile.AttackRange,
                ["perceptionSettings"] = profile.PerceptionSettings != null ? JObject.FromObject(profile.PerceptionSettings) : new JObject(),
                ["movementSettings"] = profile.MovementSettings != null ? JObject.FromObject(profile.MovementSettings) : new JObject(),
                ["entries"] = entries
            };
            return root.ToString(Formatting.Indented);
        }

        private static string WriteCard(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            return new JObject
            {
                ["schema"] = CharacterTestRuntimeAiLabBehaviorCardDraft.Schema,
                ["cardDraftId"] = card.CardDraftId,
                ["publishedStableId"] = card.PublishedStableId,
                ["sourcePublishedRevision"] = card.SourcePublishedRevision,
                ["sourcePublishedSemanticHash"] = card.SourcePublishedSemanticHash,
                ["sourcePublishedContentHash"] = card.SourcePublishedContentHash,
                ["displayName"] = card.DisplayName,
                ["category"] = card.Category,
                ["iconKey"] = card.IconKey,
                ["cost"] = card.Cost,
                ["cooldownSeconds"] = card.CooldownSeconds,
                ["shortDescription"] = card.ShortDescription,
                ["useSummary"] = card.UseSummary,
                ["riskSummary"] = card.RiskSummary,
                ["tagsText"] = card.TagsText,
                ["requiredCapabilities"] = JArray.FromObject(card.RequiredCapabilities),
                ["responsibilityKinds"] = WriteResponsibilities(card.ResponsibilityKinds),
                ["currentRevision"] = card.CurrentRevision,
                ["payloadSchema"] = card.HasCompositionSource ? CharacterTestTacticalCompositionSource.CurrentSchema : card.HasTacticalSource ? card.CloneTacticalSource().Schema : CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema,
                ["payloadVersion"] = card.HasCompositionSource ? 1 : card.HasTacticalSource ? card.CloneTacticalSource().Version : 5,
                ["payload"] = JToken.Parse(card.HasCompositionSource ? card.CompositionSourceJson : card.HasTacticalSource ? card.TacticalSourceJson : CharacterTestRuntimeAiLabBehaviorCardLogicJson.Serialize(card.LogicPayload)),
                ["sourcePayloadSchema"] = CharacterTestRuntimeAiLabBehaviorCardPayload.Schema,
                ["sourcePayloadV1"] = card.HasAuthoredSource ? null : CharacterTestRuntimeAiLabBehaviorCardJson.WritePayload(card.Payload),
                ["lastCompiledRevision"] = card.LastCompiledRevision,
                ["lastCompileStatus"] = card.LastCompileStatus
            }.ToString(Formatting.Indented);
        }

        private static JArray WriteResponsibilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities)
        {
            var result = new JArray();
            for (int i = 0; responsibilities != null && i < responsibilities.Count; i++)
                result.Add(responsibilities[i].ToString());
            return result;
        }

        private static int ReadDraftNumericId(JObject entry, string field)
        {
            JToken value = entry[field];
            if (value == null) return 0;
            if (value.Type != JTokenType.Integer
                || !int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)
                || id < 0)
                throw new InvalidDataException("Draft " + field + " must be a non-negative Int32 identity.");
            return id;
        }

        private static CharacterTestRuntimeAiLabBehaviorGroupDraft ReadGroup(string json)
        {
            JObject root = JObject.Parse(json);
            if (ReadSchemaVersion((string)root["schema"]) <= 0)
                throw new InvalidDataException("Behavior group schema is unsupported.");
            if (!Enum.TryParse((string)root["targetSelectionMode"], out CharacterTestAiTargetSelectionMode targetMode))
                targetMode = CharacterTestAiTargetSelectionMode.NearestOpposingTeam;
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                (string)root["publishedStableId"],
                targetMode,
                (float?)root["attackRange"] ?? CharacterTestAiBehaviorProfileSource.DefaultAttackRange,
                root["perceptionSettings"]?.ToObject<CharacterTestRuntimeAiPlannerPerceptionSettings>(),
                root["movementSettings"]?.ToObject<CharacterTestRuntimeAiPlannerMovementSettings>());
            var group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                RequiredGuid(root, "groupDraftId"),
                (string)root["publishedStableId"],
                (string)root["displayName"],
                profile,
                (int?)root["currentRevision"] ?? 1,
                (int?)root["sourceProjectRevision"] ?? 0,
                (long?)root["sourceProjectContentHash"] ?? 0L,
                (string)root["sourceProjectPath"],
                (string)root["sourceCatalogFileHash"]);
            JArray entries = root["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject item = (JObject)entries[i];
                string behaviorId = (string)item["behaviorId"] ?? string.Empty;
                var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    behaviorId,
                    !((bool?)item["disabled"] ?? false),
                    (int?)item["order"] ?? 0)
                {
                    NumericId = ReadDraftNumericId(item, "numericId"),
                    DefinitionNumericId = ReadDraftNumericId(item, "definitionNumericId")
                };
                var responsibilities = new List<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
                JArray responsibilityKinds = item["responsibilityKinds"] as JArray;
                for (int responsibilityIndex = 0;
                     responsibilityKinds != null && responsibilityIndex < responsibilityKinds.Count;
                     responsibilityIndex++)
                {
                    if (Enum.TryParse(
                            (string)responsibilityKinds[responsibilityIndex],
                            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind parsed))
                    {
                        responsibilities.Add(parsed);
                    }
                }
                if (responsibilities.Count == 0)
                {
                    if (Enum.TryParse(
                            (string)item["responsibilityKind"],
                            out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind))
                    {
                        responsibilities.Add(responsibilityKind);
                    }
                    else if (Enum.TryParse(
                                 (string)item["slotKind"],
                                 out CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind legacySlotKind))
                    {
                        responsibilities.Add(
                            CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(
                                legacySlotKind));
                    }
                }
                runtimeEntry.SetResponsibilities(responsibilities);
                JArray parameters = item["parameterOverrides"] as JArray;
                for (int p = 0; parameters != null && p < parameters.Count; p++)
                {
                    JObject parameter = (JObject)parameters[p];
                    string key = (string)parameter["key"] ?? string.Empty;
                    if (!Enum.TryParse((string)parameter["kind"], out CharacterTestRuntimeAiPlannerBehaviorParameterKind kind))
                        throw new InvalidDataException("Unknown behavior parameter kind.");
                    if (kind == CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool)
                        runtimeEntry.SetBool(key, (bool?)parameter["boolValue"] ?? false);
                    else if (kind == CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int)
                        runtimeEntry.SetInt(key, (int?)parameter["intValue"] ?? 0);
                    else
                        runtimeEntry.SetFloat(key, (float?)parameter["floatValue"] ?? 0f);
                }
                group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                    RequiredGuid(item, "entryId"),
                    RequiredGuid(item, "cardDraftId"),
                    behaviorId,
                    runtimeEntry.Order,
                    runtimeEntry.Disabled,
                    runtimeEntry));
            }
            group.NormalizeOrders();
            return group;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardDraft ReadCard(string json)
        {
            JObject root = JObject.Parse(json);
            if (root["cost"] != null && root["cost"].Type != JTokenType.Integer
                || root["cooldownSeconds"] != null && root["cooldownSeconds"].Type != JTokenType.Integer && root["cooldownSeconds"].Type != JTokenType.Float)
                throw new JsonSerializationException("Author cost/cooldown must be JSON numbers; cost must be an integer.");
            if (ReadSchemaVersion((string)root["schema"]) <= 0)
                throw new InvalidDataException("Behavior card schema is unsupported.");
            string payloadSchema = (string)root["payloadSchema"] ?? string.Empty;
            CharacterTestRuntimeAiLabBehaviorCardPayload payload;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logicPayload = null;
            bool migratedFromV1 = true;
            CharacterTestTacticalAuthoringSource tactical = null;
            CharacterTestTacticalCompositionSource composition = null;
            if (string.Equals(payloadSchema, CharacterTestTacticalCompositionSource.CurrentSchema, StringComparison.Ordinal))
            {
                if ((int?)root["payloadVersion"] != 1) throw new InvalidDataException("Composition payload version is unsupported.");
                composition = CharacterTestTacticalCompositionJson.Read(root["payload"]?.ToString(Formatting.None));
                payload = new CharacterTestRuntimeAiLabBehaviorCardPayload(); migratedFromV1 = false;
            }
            else if (string.Equals(payloadSchema, CharacterTestTacticalAuthoringSource.CurrentSchema, StringComparison.Ordinal)
                || string.Equals(payloadSchema, CharacterTestTacticalAuthoringSource.BlockSchema, StringComparison.Ordinal)
                || string.Equals(payloadSchema, CharacterTestTacticalAuthoringSource.TimingSchema, StringComparison.Ordinal)
                || string.Equals(payloadSchema, CharacterTestTacticalAuthoringSource.PreparationSchema, StringComparison.Ordinal))
            {
                int version = payloadSchema == CharacterTestTacticalAuthoringSource.PreparationSchema ? 5 : payloadSchema == CharacterTestTacticalAuthoringSource.TimingSchema ? 4 : payloadSchema == CharacterTestTacticalAuthoringSource.BlockSchema ? 3 : 2;
                if ((int?)root["payloadVersion"] != version) throw new InvalidDataException("Tactical payload version is unsupported.");
                tactical = CharacterTestTacticalAuthoringJson.Read(root["payload"]?.ToString(Formatting.None));
                if (tactical.Schema != payloadSchema || tactical.Version != version) throw new InvalidDataException("Tactical payload identity mismatch.");
                payload = new CharacterTestRuntimeAiLabBehaviorCardPayload();
                migratedFromV1 = false;
            }
            else if (string.Equals(payloadSchema, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema, StringComparison.Ordinal)
                || string.Equals(payloadSchema, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV10Schema, StringComparison.Ordinal)
                || string.Equals(payloadSchema, CharacterTestRuntimeAiLabBehaviorCardLogicPayload.LegacyV9Schema, StringComparison.Ordinal))
            {
                JToken sourceV1 = root["sourcePayloadV1"];
                payload = sourceV1 != null
                    ? CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(sourceV1.ToString(Formatting.None))
                    : CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic((string)root["publishedStableId"]);
                logicPayload = CharacterTestRuntimeAiLabBehaviorCardLogicJson.Deserialize(
                    root["payload"]?.ToString(Formatting.None));
                migratedFromV1 = false;
            }
            else if (string.Equals(payloadSchema, CharacterTestRuntimeAiLabBehaviorCardDraft.LegacyAtomicPayloadSchema, StringComparison.Ordinal))
            {
                payload = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    (string)root["payload"]?["behaviorId"] ?? (string)root["publishedStableId"]);
            }
            else
            {
                payload = CharacterTestRuntimeAiLabBehaviorCardJson.Deserialize(
                    root["payload"]?.ToString(Formatting.None));
            }
            if (logicPayload == null && tactical == null && composition == null)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                    CharacterTestRuntimeAiLabBehaviorCardValidator.Validate(payload);
                if (CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics))
                    throw new InvalidDataException("Behavior card payload validation failed: " + diagnostics[0].Code + " " + diagnostics[0].Path);
            }
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                RequiredGuid(root, "cardDraftId"),
                (string)root["publishedStableId"],
                (string)root["displayName"],
                (string)root["category"],
                payload,
                (int?)root["currentRevision"] ?? 1)
            {
                LastCompiledRevision = Math.Max(0, (int?)root["lastCompiledRevision"] ?? 0),
                LastCompileStatus = (string)root["lastCompileStatus"] ?? "NotCompiled",
                SourcePublishedRevision = Math.Max(0, (int?)root["sourcePublishedRevision"] ?? 0),
                SourcePublishedSemanticHash = (long?)root["sourcePublishedSemanticHash"] ?? 0L,
                SourcePublishedContentHash = (long?)root["sourcePublishedContentHash"] ?? 0L,
                IconKey = (string)root["iconKey"] ?? string.Empty,
                Cost = (int?)root["cost"] ?? 0,
                CooldownSeconds = (float?)root["cooldownSeconds"] ?? 0f,
                ShortDescription = (string)root["shortDescription"] ?? string.Empty,
                UseSummary = (string)root["useSummary"] ?? string.Empty,
                RiskSummary = (string)root["riskSummary"] ?? string.Empty,
                TagsText = (string)root["tagsText"] ?? string.Empty,
                RequiredCapabilities = root["requiredCapabilities"]
                    ?.ToObject<CharacterTestRuntimeAiPlannerBehaviorCapability[]>()
                    ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>(),
                ResponsibilityKinds = ReadResponsibilities(root["responsibilityKinds"] as JArray)
            };
            if (logicPayload != null)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> logicDiagnostics =
                    CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(logicPayload);
                card.LogicPayload = logicPayload;
                if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(logicDiagnostics))
                    card.LastCompileStatus = "DraftInvalid";
            }
            if (composition != null) card.SetCompositionSource(composition);
            else if (tactical != null) card.SetTacticalSource(tactical);
            if (card.Cost < 0 || card.Cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost
                || float.IsNaN(card.CooldownSeconds) || float.IsInfinity(card.CooldownSeconds)
                || card.CooldownSeconds < 0f || card.CooldownSeconds * 60d >= int.MaxValue)
                throw new JsonSerializationException("Invalid author cost or cooldown.");
            card.LogicPayloadMigratedFromV1 = migratedFromV1;
            return card;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ReadResponsibilities(
            JArray source)
        {
            var values = new List<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (Enum.TryParse(
                        (string)source[i],
                        out CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind value))
                    values.Add(value);
            }
            return CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(values);
        }

        private static void ReadReferencedGroups(
            string directory,
            JArray references,
            CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            for (int i = 0; references != null && i < references.Count; i++)
            {
                JObject reference = (JObject)references[i];
                string json = ReadAndValidateReference(directory, reference);
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = ReadGroup(json);
                if (!string.Equals(group.GroupDraftId, (string)reference["id"], StringComparison.Ordinal))
                    throw new InvalidDataException("Behavior group reference id mismatch.");
                workspace.AddLoadedGroup(group);
            }
        }

        private static void ReadReferencedCards(
            string directory,
            JArray references,
            CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            for (int i = 0; references != null && i < references.Count; i++)
            {
                JObject reference = (JObject)references[i];
                string json = ReadAndValidateReference(directory, reference);
                CharacterTestRuntimeAiLabBehaviorCardDraft card = ReadCard(json);
                if (!string.Equals(card.CardDraftId, (string)reference["id"], StringComparison.Ordinal))
                    throw new InvalidDataException("Behavior card reference id mismatch.");
                workspace.AddLoadedCard(card);
            }
        }

        private static void ReadSlots(JArray slots, CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                JObject item = (JObject)slots[i];
                var slot = new CharacterTestRuntimeAiLabDraftSlot(
                    (int?)item["teamId"] ?? 0,
                    (int?)item["slotIndex"] ?? -1,
                    (bool?)item["enabled"] ?? false,
                    (int?)item["characterId"] ?? 0,
                    RequiredGuid(item, "groupDraftId"),
                    (int?)item["arenaBehaviorGroupDraftId"] ?? 0)
                {
                    SpawnX = (float?)item["spawnX"] ?? 0f,
                    SpawnZ = (float?)item["spawnZ"] ?? 0f
                };
                workspace.SetLoadedSlot(slot);
            }
        }

        private static void ValidateReferences(CharacterTestRuntimeAiLabDraftWorkspace workspace)
        {
            for (int i = 0; i < workspace.Slots.Count; i++)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = workspace.Slots[i];
                if (slot != null && !workspace.Groups.ContainsKey(slot.GroupDraftId))
                    throw new InvalidDataException("Workspace slot references a missing behavior group.");
            }
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in workspace.Groups.Values)
            {
                for (int i = 0; i < group.Entries.Count; i++)
                {
                    if (!workspace.Cards.ContainsKey(group.Entries[i].CardDraftId))
                        throw new InvalidDataException("Behavior group references a missing behavior card.");
                }
            }
        }

        private static string ReadAndValidateReference(string directory, JObject reference)
        {
            string relative = (string)reference["path"] ?? string.Empty;
            if (relative.Contains(".."))
                throw new InvalidDataException("Draft reference path escapes the workspace.");
            string path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            string json = File.ReadAllText(path);
            string expectedHash = (string)reference["hash"] ?? string.Empty;
            if (!string.Equals(Hash(json), expectedHash, StringComparison.Ordinal)
                && !string.Equals(HashLegacyWindowsText(json), expectedHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Draft content hash mismatch: " + relative);
            }
            return json;
        }

        private static JObject WriteReference(string id, int revision, string path, string hash)
        {
            return new JObject
            {
                ["id"] = id,
                ["revision"] = revision,
                ["path"] = path,
                ["hash"] = hash
            };
        }

        private static string RequiredGuid(JObject root, string property)
        {
            string value = (string)root[property];
            if (!Guid.TryParse(value, out Guid parsed))
                throw new InvalidDataException(property + " must be a GUID.");
            return parsed.ToString("D", CultureInfo.InvariantCulture);
        }

        private static int ReadSchemaVersion(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
                return 0;
            int marker = schema.LastIndexOf(".v", StringComparison.Ordinal);
            return marker >= 0 && int.TryParse(schema.Substring(marker + 2), out int version) ? version : 0;
        }

        private static DateTime ReadUtc(JObject root, string property, DateTime fallback)
        {
            return DateTime.TryParse(
                (string)root[property],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime value) ? value : fallback;
        }

        private static string Hash(string content)
        {
            return HashExact(NormalizeHashText(content));
        }

        private static string HashLegacyWindowsText(string content)
        {
            return HashExact(NormalizeHashText(content).Replace("\n", "\r\n"));
        }

        private static string NormalizeHashText(string content)
        {
            return (content ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
        }

        private static string HashExact(string content)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static IEnumerable<CharacterTestRuntimeAiLabBehaviorGroupDraft> SortedGroups(
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> groups)
        {
            var list = new List<CharacterTestRuntimeAiLabBehaviorGroupDraft>(groups.Values);
            list.Sort((left, right) => string.CompareOrdinal(left.GroupDraftId, right.GroupDraftId));
            return list;
        }

        private static IEnumerable<CharacterTestRuntimeAiLabBehaviorCardDraft> SortedCards(
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            var list = new List<CharacterTestRuntimeAiLabBehaviorCardDraft>(cards.Values);
            list.Sort((left, right) => string.CompareOrdinal(left.CardDraftId, right.CardDraftId));
            return list;
        }

        private static void PreservePrevious(
            string workspaceDirectory,
            string previous)
        {
            if (!File.Exists(Path.Combine(workspaceDirectory, "workspace.json")))
                return;
            if (Directory.Exists(previous))
                Directory.Delete(previous, recursive: true);
            Directory.CreateDirectory(previous);
            CopySnapshot(workspaceDirectory, previous);
        }

        private static void CommitStaging(string stagingDirectory, string workspaceDirectory)
        {
            Directory.CreateDirectory(workspaceDirectory);
            string groups = Path.Combine(workspaceDirectory, "groups");
            string cards = Path.Combine(workspaceDirectory, "cards");
            string tactical = Path.Combine(workspaceDirectory, "tactical-shared");
            if (Directory.Exists(groups)) Directory.Delete(groups, recursive: true);
            if (Directory.Exists(cards)) Directory.Delete(cards, recursive: true);
            if (Directory.Exists(tactical)) Directory.Delete(tactical, recursive: true);
            Directory.Move(Path.Combine(stagingDirectory, "groups"), groups);
            Directory.Move(Path.Combine(stagingDirectory, "cards"), cards);
            if (Directory.Exists(Path.Combine(stagingDirectory, "tactical-shared")))
                Directory.Move(Path.Combine(stagingDirectory, "tactical-shared"), tactical);
            string target = Path.Combine(workspaceDirectory, "workspace.json");
            string staged = Path.Combine(stagingDirectory, "workspace.json");
            if (File.Exists(target)) File.Delete(target);
            File.Move(staged, target);
            Directory.Delete(stagingDirectory, recursive: true);
        }

        private static void CopySnapshot(string source, string destination)
        {
            File.Copy(Path.Combine(source, "workspace.json"), Path.Combine(destination, "workspace.json"), overwrite: true);
            CopyDirectory(Path.Combine(source, "groups"), Path.Combine(destination, "groups"));
            CopyDirectory(Path.Combine(source, "cards"), Path.Combine(destination, "cards"));
            if (Directory.Exists(Path.Combine(source, "tactical-shared")))
                CopyDirectory(Path.Combine(source, "tactical-shared"), Path.Combine(destination, "tactical-shared"));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            if (!Directory.Exists(source)) return;
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], Path.Combine(destination, Path.GetFileName(files[i])), overwrite: true);
        }

        private string GetWorkspaceDirectory(string workspaceId)
        {
            return Path.Combine(_draftsRoot, CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(workspaceId));
        }

        private string GetPreviousDirectory(string workspaceId)
        {
            string normalized = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(workspaceId);
            return string.Equals(_recoveryRoot, _draftsRoot, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(_draftsRoot, normalized, ".previous")
                : Path.Combine(_recoveryRoot, normalized, ".previous");
        }

        private string GetStagingDirectory(string workspaceId)
        {
            string normalized = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(workspaceId);
            return string.Equals(_recoveryRoot, _draftsRoot, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(_draftsRoot, normalized + ".staging")
                : Path.Combine(_recoveryRoot, normalized, ".staging");
        }
    }
}
