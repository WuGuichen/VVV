using System;
using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabProjectCardMetadataField
    {
        DisplayName = 0,
        Cost = 1,
        ShortDescription = 2,
        UseSummary = 3,
        RiskSummary = 4,
        Tags = 5,
        CooldownSeconds = 6
    }

    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        public const string Schema = "mx.character-test.runtime-ai-lab.workspace.v3";
        public const int MaximumBehaviorGroupEntries = 16;
        private readonly Dictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> _groups =
            new Dictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft>(StringComparer.Ordinal);
        private readonly Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> _cards =
            new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(StringComparer.Ordinal);
        private readonly CharacterTestRuntimeAiLabDraftSlot[] _slots =
            new CharacterTestRuntimeAiLabDraftSlot[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        private readonly List<HistoryEntry> _undo = new List<HistoryEntry>(100);
        private readonly List<HistoryEntry> _redo = new List<HistoryEntry>(100);

        public CharacterTestRuntimeAiLabDraftWorkspace(string workspaceId, string displayName)
        {
            WorkspaceId = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(workspaceId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Runtime AI Lab Workspace" : displayName;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
            CurrentRevision = 1;
        }

        public string WorkspaceId { get; }
        public string DisplayName { get; set; }
        public DateTime CreatedUtc { get; internal set; }
        public DateTime UpdatedUtc { get; internal set; }
        public int CurrentRevision { get; internal set; }
        public int LastSavedRevision { get; internal set; }
        public int LastExplicitSaveRevision { get; internal set; }
        public int LastCompiledRevision { get; internal set; }
        public int LastAppliedRevision { get; internal set; }
        internal int FrameLimit { get; set; } = CharacterTestRuntimeAiPlannerDevSceneController.DefaultRuntimeAiLabFrameLimit;
        public bool IsReadOnly { get; internal set; }
        public bool RecoveredFromPrevious { get; internal set; }
        public string Diagnostic { get; internal set; } = string.Empty;
        public bool Dirty => CurrentRevision != LastSavedRevision;
        public bool CanUndo => !IsReadOnly && _undo.Count > 0;
        public bool CanRedo => !IsReadOnly && _redo.Count > 0;
        internal string NextUndoLabel => _undo.Count > 0
            ? _undo[_undo.Count - 1].Label
            : string.Empty;
        public string UndoDescription => _undo.Count > 0 ? _undo[_undo.Count - 1].Label : string.Empty;
        public string RedoDescription => _redo.Count > 0 ? _redo[_redo.Count - 1].Label : string.Empty;
        public IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> Groups => _groups;
        public IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> Cards => _cards;
        public IReadOnlyList<CharacterTestRuntimeAiLabDraftSlot> Slots => _slots;

        public static CharacterTestRuntimeAiLabDraftWorkspace Import(
            CharacterTestArenaMatchConfig config,
            string workspaceId = null)
        {
            var workspace = new CharacterTestRuntimeAiLabDraftWorkspace(workspaceId, "Runtime AI Lab Experiment");
            CharacterTestArenaBehaviorGroupDraftStore source = config?.BehaviorGroups;
            CharacterTestRuntimeAiPlannerProjectCatalogState project = config?.ProjectCatalogState;
            var groupByArenaId = new Dictionary<int, string>();
            if (source != null)
            {
                for (int i = 0; i < source.Drafts.Count; i++)
                {
                    CharacterTestArenaBehaviorGroupDraft arena = source.Drafts[i];
                    CharacterTestRuntimeAiLabBehaviorGroupDraft group =
                        CharacterTestRuntimeAiLabBehaviorGroupDraft.Import(
                            arena,
                            workspace._cards,
                            project?.GroupCatalog,
                            project?.CardCatalog,
                            project?.GroupCatalogPath,
                            project?.GroupFileHash);
                    workspace._groups.Add(group.GroupDraftId, group);
                    groupByArenaId[arena.DraftId] = group.GroupDraftId;
                }
            }

            if (project?.CardCatalog != null)
            {
                for (int i = 0; i < project.CardCatalog.Cards.Count; i++)
                {
                    var published = project.CardCatalog.Cards[i];
                    if (published.TacticalSource != null)
                    {
                        CharacterTestRuntimeAiLabBehaviorCardDraft selected = null;
                        foreach (var candidate in workspace._cards.Values)
                            if (candidate.PublishedStableId == published.StableCardId) { selected = candidate; break; }
                        if (selected != null && selected.SourcePublishedRevision != published.Revision) continue;
                        bool newer = false;
                        if (selected == null)
                            foreach (var candidate in project.CardCatalog.Cards)
                                if (candidate.StableCardId == published.StableCardId && candidate.Revision > published.Revision) { newer = true; break; }
                        if (newer) continue;
                    }
                    workspace.GetOrCreateSharedPublishedCard(published);
                }
            }

            workspace.CanonicalizeSharedBehaviorCards();

            workspace.ImportSlots(config, CharacterTestArenaMatchConfig.Team1Id, groupByArenaId);
            workspace.ImportSlots(config, CharacterTestArenaMatchConfig.Team2Id, groupByArenaId);
            workspace.LastSavedRevision = 0;
            workspace.LastExplicitSaveRevision = workspace.CurrentRevision;
            return workspace;
        }

        internal CharacterTestRuntimeAiLabDraftWorkspace ForkForIsolatedTest()
        {
            var fork = new CharacterTestRuntimeAiLabDraftWorkspace(null, DisplayName);
            fork.RestoreState(CaptureState());
            fork.FrameLimit = FrameLimit;
            fork.CreatedUtc = DateTime.UtcNow;
            fork.UpdatedUtc = fork.CreatedUtc;
            fork.LastSavedRevision = 0;
            fork.LastExplicitSaveRevision = fork.CurrentRevision;
            fork.LastCompiledRevision = 0;
            fork.LastAppliedRevision = 0;
            return fork;
        }

        /// <summary>
        /// Compatibility hook retained for older callers. v3 responsibility groups never
        /// synthesize, replace, enable, disable, reorder, or reclassify installed behaviors.
        /// </summary>
        public bool EnsureCoreSlotContracts(bool markMigrated)
        {
            return false;
        }

        /// <summary>
        /// Removes only the disabled compatibility artifacts produced by the retired one-card
        /// core-slot repair. Authored and published duplicate installations are preserved.
        /// </summary>
        /// <remarks>LowFreqAlloc: persisted-workspace load migration only.</remarks>
        internal bool RepairGeneratedCoreContractArtifacts()
        {
            if (IsReadOnly)
                return false;

            int removedCount = 0;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair in _groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = pair.Value;
                if (group == null)
                    continue;

                bool groupChanged = false;
                for (int entryIndex = group.Entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (!IsRetiredCoreContractArtifact(group, entry))
                        continue;

                    group.Entries.RemoveAt(entryIndex);
                    removedCount++;
                    groupChanged = true;
                }

                if (!groupChanged)
                    continue;
                group.NormalizeOrders();
                group.CurrentRevision++;
            }

            if (removedCount == 0)
                return false;

            CurrentRevision++;
            UpdatedUtc = DateTime.UtcNow;
            string diagnostic = "已清理旧槽位逻辑生成的 "
                + removedCount.ToString(CultureInfo.InvariantCulture)
                + " 条重复行为；手动添加和已发布行为未被修改，请保存草稿。";
            Diagnostic = string.IsNullOrWhiteSpace(Diagnostic)
                ? diagnostic
                : Diagnostic + " " + diagnostic;
            return true;
        }

        private bool IsRetiredCoreContractArtifact(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry)
        {
            if (group == null
                || entry == null
                || !entry.Disabled
                || entry.ResponsibilityKinds[0]
                    != CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                || !_cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || card.HasPublishedSource
                || !IsGeneratedCompatibilityWrapper(card)
                || !string.Equals(card.PublishedStableId, entry.BehaviorId, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(entry.BehaviorId, "Engage.NoOp", StringComparison.Ordinal)
                || string.Equals(entry.BehaviorId, "Damage.NoOp", StringComparison.Ordinal))
            {
                return true;
            }

            if (!IsRetiredCoreDefaultBehavior(entry.BehaviorId))
                return false;

            for (int candidateIndex = 0; candidateIndex < group.Entries.Count; candidateIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft candidate = group.Entries[candidateIndex];
                if (candidate == null
                    || candidate.Disabled
                    || candidate.ResponsibilityKinds[0]
                        == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
                    || !string.Equals(candidate.BehaviorId, entry.BehaviorId, StringComparison.Ordinal))
                {
                    continue;
                }
                return true;
            }
            return false;
        }

        private static bool IsRetiredCoreDefaultBehavior(string behaviorId)
        {
            return string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                || string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                || string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                || string.Equals(
                    behaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// Repairs old compatibility-wrapper behavior cards whose module-level activation
        /// conditions were lost while their authored stage graph was retained.
        /// </summary>
        /// <remarks>LowFreqAlloc: persisted-workspace load migration only.</remarks>
        internal bool RepairLegacyBehaviorActivationConditions()
        {
            if (IsReadOnly)
                return false;

            bool changed = false;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair in _groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = pair.Value;
                if (group == null)
                    continue;

                bool groupChanged = false;
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (entry == null
                        || !_cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                        || card?.Payload == null
                        || card.LogicPayload == null
                        || !string.Equals(
                            card.Payload.Intent,
                            "Compatibility wrapper for published behavior definition "
                                + entry.BehaviorId
                                + ".",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    bool sourceChanged = CharacterTestRuntimeAiLabBehaviorCardFactory.ApplyLegacyActivationMigration(
                        card.Payload,
                        entry.BehaviorId,
                        group.Profile.AttackRange);
                    bool logicChanged = CharacterTestRuntimeAiLabBehaviorCardFactory.ApplyLegacyActivationMigration(
                        card.LogicPayload,
                        entry.BehaviorId,
                        group.Profile.AttackRange);
                    if (!sourceChanged && !logicChanged)
                        continue;

                    card.CurrentRevision++;
                    card.LastCompileStatus = "ActivationContractMigrated";
                    groupChanged = true;
                    changed = true;
                }

                if (groupChanged)
                    group.CurrentRevision++;
            }

            if (changed)
            {
                CurrentRevision++;
                UpdatedUtc = DateTime.UtcNow;
                const string activationDiagnostic =
                    "已修复旧版行为卡缺失的模块激活条件，请保存草稿。";
                Diagnostic = string.IsNullOrWhiteSpace(Diagnostic)
                    ? activationDiagnostic
                    : Diagnostic + " " + activationDiagnostic;
            }
            return changed;
        }

        /// <summary>
        /// Repairs v7 interval-review flags that were applied to generated compatibility wrappers.
        /// Authored cards remain review-gated; only the deterministic wrappers created from the
        /// Runtime AI Planner module catalog are normalized automatically.
        /// </summary>
        /// <remarks>LowFreqAlloc: persisted-workspace load migration only.</remarks>
        internal bool RepairGeneratedCompatibilityV7Intervals()
        {
            if (IsReadOnly)
                return false;

            var repairedCardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorCardDraft> pair in _cards)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft card = pair.Value;
                if (!IsGeneratedCompatibilityWrapper(card))
                    continue;

                bool changed = CharacterTestRuntimeAiLabBehaviorRecipeV5Templates
                    .ConfigureGeneratedV7FixedIntervals(
                        card.LogicPayload,
                        card.PublishedStableId);
                if (!changed)
                {
                    continue;
                }

                card.CurrentRevision++;
                card.LastCompileStatus = "GeneratedV7IntervalsMigrated";
                repairedCardIds.Add(pair.Key);
            }

            if (repairedCardIds.Count == 0)
                return false;

            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair in _groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = pair.Value;
                bool referencesRepairedCard = false;
                for (int i = 0; group != null && i < group.Entries.Count; i++)
                {
                    if (repairedCardIds.Contains(group.Entries[i].CardDraftId))
                    {
                        referencesRepairedCard = true;
                        break;
                    }
                }
                if (referencesRepairedCard)
                    group.CurrentRevision++;
            }

            CurrentRevision++;
            UpdatedUtc = DateTime.UtcNow;
            const string intervalDiagnostic =
                "已修复系统生成行为卡的 v7 固定区间；自定义事件未被修改，请保存草稿。";
            Diagnostic = string.IsNullOrWhiteSpace(Diagnostic)
                ? intervalDiagnostic
                : Diagnostic + " " + intervalDiagnostic;
            return true;
        }

        private static bool IsGeneratedCompatibilityWrapper(
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            return card?.Payload != null
                && !string.IsNullOrWhiteSpace(card.PublishedStableId)
                && string.Equals(
                    card.Payload.Intent,
                    "Compatibility wrapper for published behavior definition "
                        + card.PublishedStableId
                        + ".",
                    StringComparison.Ordinal);
        }


        public CharacterTestRuntimeAiLabDraftSlot TryGetSlot(int teamId, int slotIndex)
        {
            int index = ToSlotIndex(teamId, slotIndex);
            return index >= 0 ? _slots[index] : null;
        }

        public bool TryGetGroupForSlot(
            int teamId,
            int slotIndex,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            group = null;
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            return slot != null
                && !string.IsNullOrWhiteSpace(slot.GroupDraftId)
                && _groups.TryGetValue(slot.GroupDraftId, out group);
        }

        public bool TrySelectGroupForSlot(
            int teamId,
            int slotIndex,
            string groupDraftId,
            int arenaBehaviorGroupDraftId,
            out string message)
        {
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            if (IsReadOnly || slot == null || string.IsNullOrWhiteSpace(groupDraftId)
                || !_groups.TryGetValue(groupDraftId, out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                message = IsReadOnly ? "Runtime AI Lab workspace is read-only." : "所选行为组草稿已不存在。";
                return false;
            }
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    group.Profile,
                    CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            if (!compile.Success)
            {
                message = "该行为组草稿当前不可用于测试。";
                return false;
            }
            if (string.Equals(slot.GroupDraftId, groupDraftId, StringComparison.Ordinal)
                && slot.ArenaBehaviorGroupDraftId == arenaBehaviorGroupDraftId)
            {
                message = "已选择行为组草稿。";
                return true;
            }
            WorkspaceState before = CaptureState();
            slot.GroupDraftId = groupDraftId;
            slot.ArenaBehaviorGroupDraftId = Math.Max(0, arenaBehaviorGroupDraftId);
            TouchWorkspace();
            PushUndo(before);
            message = "已选择行为组草稿。";
            return true;
        }

        public bool TryCreateLocalGroupCopyForSlot(
            int teamId,
            int slotIndex,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft copy,
            out string message)
        {
            copy = null;
            if (IsReadOnly || !TryGetGroupForSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft source))
            {
                message = IsReadOnly ? "Runtime AI Lab workspace is read-only." : "当前没有可复制的行为组。";
                return false;
            }
            WorkspaceState before = CaptureState();
            copy = source.Clone(newIdentity: true);
            copy.DisplayName = source.DisplayName + " 副本";
            copy.Profile.DisplayName = copy.DisplayName;
            copy.Profile.GroupId = "runtime-ai-lab.draft." + copy.GroupDraftId;
            copy.PublishedStableId = string.Empty;
            copy.SourceProjectRevision = 0;
            copy.SourceProjectContentHash = 0;
            copy.SourceProjectPath = string.Empty;
            copy.SourceCatalogFileHash = string.Empty;
            _groups.Add(copy.GroupDraftId, copy);
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            slot.GroupDraftId = copy.GroupDraftId;
            slot.ArenaBehaviorGroupDraftId = 0;
            Touch(copy);
            PushUndo(before);
            message = "已创建本地行为组草稿“" + copy.DisplayName + "”；确认前仅用于当前实验。";
            return true;
        }

        /// <summary>
        /// Restores a slot after a transient single-card authoring group has finished serving as the
        /// active editing context.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit authoring navigation only.</remarks>
        public bool TryRestoreGroupForSlot(
            int teamId,
            int slotIndex,
            string expectedCurrentGroupDraftId,
            string restoredGroupDraftId)
        {
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            if (slot == null
                || string.IsNullOrWhiteSpace(restoredGroupDraftId)
                || !_groups.ContainsKey(restoredGroupDraftId)
                || !string.Equals(
                    slot.GroupDraftId,
                    expectedCurrentGroupDraftId,
                    StringComparison.Ordinal)
                || string.Equals(slot.GroupDraftId, restoredGroupDraftId, StringComparison.Ordinal))
            {
                return false;
            }

            slot.GroupDraftId = restoredGroupDraftId;
            TouchWorkspace();
            return true;
        }

        /// <summary>
        /// Repairs a slot left on an isolated Published-card authoring group by an interrupted or
        /// older navigation flow.
        /// </summary>
        /// <remarks>LowFreqAlloc: setup navigation and compatibility recovery only.</remarks>
        public bool TryRestoreOrphanedPublishedCardAuthoringGroup(
            CharacterTestArenaMatchConfig config,
            int teamId,
            int slotIndex)
        {
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            if (slot == null
                || !_groups.TryGetValue(
                    slot.GroupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft current)
                || current?.Profile == null
                || !current.Profile.CompositionId.StartsWith(
                    "runtime-ai-lab.published-card.",
                    StringComparison.Ordinal))
            {
                return false;
            }

            int arenaDraftId = slot.ArenaBehaviorGroupDraftId > 0
                ? slot.ArenaBehaviorGroupDraftId
                : config?.TryGetSlot(teamId, slotIndex)?.BehaviorGroupDraftId ?? 0;
            if (config?.BehaviorGroups == null
                || !config.BehaviorGroups.TryGetDraft(
                    arenaDraftId,
                    out CharacterTestArenaBehaviorGroupDraft arenaGroup))
            {
                return false;
            }

            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in _groups.Values)
            {
                if (group == null
                    || string.Equals(group.GroupDraftId, current.GroupDraftId, StringComparison.Ordinal)
                    || !string.Equals(
                        group.PublishedStableId,
                        arenaGroup.StableId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                slot.GroupDraftId = group.GroupDraftId;
                TouchWorkspace();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Opens the workspace's shared draft for one Published Behavior Card and assigns a temporary
        /// single-card authoring group to the requested Lab slot.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit authoring navigation only.</remarks>
        public bool TryOpenPublishedCardForSlot(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out int entryIndex,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out bool workspaceChanged,
            out string message,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision projectSourceGroup = null,
            string projectSourcePath = null,
            string projectCatalogFileHash = null)
        {
            entryIndex = -1;
            card = null;
            workspaceChanged = false;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
            if (slot == null)
            {
                message = "Runtime AI Lab authoring slot is unavailable.";
                return false;
            }
            if (publishedCard == null || publishedCard.Metadata == null)
            {
                message = "Published behavior card is unavailable.";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorGroupDraft group = FindPublishedCardAuthoringGroup(
                publishedCard,
                projectSourceGroup != null,
                out card,
                out entryIndex);
            if (group != null)
            {
                if (!string.Equals(slot.GroupDraftId, group.GroupDraftId, StringComparison.Ordinal))
                {
                    WorkspaceState before = CaptureState();
                    slot.GroupDraftId = group.GroupDraftId;
                    TouchWorkspace();
                    PushUndo(before);
                    workspaceChanged = true;
                }
                message = "已打开 Published 行为卡的隔离草稿。";
                return true;
            }

            WorkspaceState creationBefore = CaptureState();
            card = GetOrCreateSharedPublishedCard(publishedCard);

            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "runtime-ai-lab.published-card." + publishedCard.StableCardId);
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 10)
            {
                EntryId = publishedCard.StableCardId
            };
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                runtimeEntry,
                publishedCard.Metadata);
            group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null,
                string.Empty,
                publishedCard.Metadata.DisplayName + " · Lab Draft",
                profile);
            if (projectSourceGroup != null)
            {
                group.PublishedStableId = projectSourceGroup.StableGroupId;
                group.SourceProjectRevision = projectSourceGroup.Revision;
                group.SourceProjectContentHash = projectSourceGroup.ContentHash;
                group.SourceProjectPath = projectSourcePath ?? string.Empty;
                group.SourceCatalogFileHash = projectCatalogFileHash ?? string.Empty;
            }
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
            group.NormalizeOrders();
            _groups.Add(group.GroupDraftId, group);
            slot.GroupDraftId = group.GroupDraftId;
            TouchWorkspace();
            PushUndo(creationBefore);
            workspaceChanged = true;
            entryIndex = 0;
            message = "已从 Published revision 创建 copy-on-write 行为卡草稿。";
            return true;
        }

        public bool SyncSlotFromArena(
            CharacterTestArenaMatchConfig config,
            int teamId,
            int slotIndex,
            bool selectExactProjectSource = false)
        {
            CharacterTestArenaSlotConfig source = config?.TryGetSlot(teamId, slotIndex);
            CharacterTestRuntimeAiLabDraftSlot target = TryGetSlot(teamId, slotIndex);
            if (source == null || target == null || IsReadOnly)
                return false;

            string groupDraftId = target.GroupDraftId;
            int arenaBehaviorGroupDraftId = target.ArenaBehaviorGroupDraftId;
            bool currentGroupExists = _groups.ContainsKey(groupDraftId);
            bool arenaSelectionChanged =
                target.ArenaBehaviorGroupDraftId > 0
                && target.ArenaBehaviorGroupDraftId != source.BehaviorGroupDraftId;
            if (currentGroupExists && target.ArenaBehaviorGroupDraftId <= 0)
                arenaBehaviorGroupDraftId = source.BehaviorGroupDraftId;
            bool hasArenaGroup = config.BehaviorGroups.TryGetDraft(
                source.BehaviorGroupDraftId,
                out CharacterTestArenaBehaviorGroupDraft arenaGroup);
            bool exactProjectSelectionRequired = selectExactProjectSource
                && hasArenaGroup
                && (!currentGroupExists
                    || !MatchesUnmodifiedArenaGroup(_groups[groupDraftId], arenaGroup));
            if ((!currentGroupExists || arenaSelectionChanged || exactProjectSelectionRequired)
                && hasArenaGroup)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft matchedGroup = null;
                foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in _groups.Values)
                {
                    if (MatchesUnmodifiedArenaGroup(group, arenaGroup))
                    {
                        matchedGroup = group;
                        break;
                    }
                }
                if (matchedGroup == null)
                    matchedGroup = ImportProjectGroup(config, arenaGroup);
                if (matchedGroup != null)
                {
                    groupDraftId = matchedGroup.GroupDraftId;
                    arenaBehaviorGroupDraftId = source.BehaviorGroupDraftId;
                }
            }

            config.TryGetSpawnPosition(teamId, slotIndex, out UnityEngine.Vector2 spawn);
            if (target.Enabled == source.Enabled
                && target.CharacterId == source.CharacterId
                && string.Equals(target.GroupDraftId, groupDraftId, StringComparison.Ordinal)
                && target.ArenaBehaviorGroupDraftId == arenaBehaviorGroupDraftId
                && Math.Abs(target.SpawnX - spawn.x) < 0.0001f
                && Math.Abs(target.SpawnZ - spawn.y) < 0.0001f)
            {
                return false;
            }

            WorkspaceState before = CaptureState();
            target.Enabled = source.Enabled;
            target.CharacterId = source.CharacterId;
            target.GroupDraftId = groupDraftId;
            target.ArenaBehaviorGroupDraftId = arenaBehaviorGroupDraftId;
            target.SpawnX = spawn.x;
            target.SpawnZ = spawn.y;
            TouchWorkspace();
            PushUndo(before);
            return true;
        }

        internal static bool MatchesUnmodifiedArenaGroup(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestArenaBehaviorGroupDraft arenaGroup)
        {
            if (group == null
                || arenaGroup == null
                || group.IsBehaviorTestComposition
                || !string.Equals(
                    group.PublishedStableId,
                    arenaGroup.StableId,
                    StringComparison.Ordinal)
                || group.SourceProjectRevision != arenaGroup.SourceProjectRevision
                || group.SourceProjectContentHash != arenaGroup.SourceProjectContentHash
                || group.CurrentRevision != arenaGroup.Revision)
            {
                return false;
            }

            return string.Equals(
                group.DisplayName,
                arenaGroup.DisplayName,
                StringComparison.Ordinal);
        }

        /// <remarks>LowFreqAlloc: setup recovery after the project catalog gained a group.</remarks>
        private CharacterTestRuntimeAiLabBehaviorGroupDraft ImportProjectGroup(
            CharacterTestArenaMatchConfig config,
            CharacterTestArenaBehaviorGroupDraft arenaGroup)
        {
            if (arenaGroup == null)
                return null;
            CharacterTestRuntimeAiPlannerProjectCatalogState project = config?.ProjectCatalogState;
            CharacterTestRuntimeAiLabBehaviorGroupDraft imported =
                CharacterTestRuntimeAiLabBehaviorGroupDraft.Import(
                    arenaGroup,
                    _cards,
                    project?.GroupCatalog,
                    project?.CardCatalog,
                    project?.GroupCatalogPath,
                    project?.GroupFileHash);
            _groups.Add(imported.GroupDraftId, imported);
            return imported;
        }

        public void RestoreSetupToArena(CharacterTestArenaMatchConfig config)
        {
            if (config == null)
                return;
            for (int i = 0; i < _slots.Length; i++)
            {
                CharacterTestRuntimeAiLabDraftSlot source = _slots[i];
                CharacterTestArenaSlotConfig target = source != null
                    ? config.TryGetSlot(source.TeamId, source.SlotIndex)
                    : null;
                if (target == null)
                    continue;
                target.Enabled = source.Enabled;
                target.CharacterId = source.CharacterId;
                config.SetSpawnPosition(source.TeamId, source.SlotIndex, new UnityEngine.Vector2(source.SpawnX, source.SpawnZ));
                if (!_groups.TryGetValue(source.GroupDraftId, out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
                    continue;
                for (int draftIndex = 0; draftIndex < config.BehaviorGroups.Drafts.Count; draftIndex++)
                {
                    CharacterTestArenaBehaviorGroupDraft arena = config.BehaviorGroups.Drafts[draftIndex];
                    if (!string.Equals(arena.StableId, group.PublishedStableId, StringComparison.Ordinal))
                        continue;
                    target.BehaviorGroupDraftId = arena.DraftId;
                    target.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(arena.DraftId);
                    target.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
                    break;
                }
            }
        }

        /// <summary>Restores persisted Team/Slot enablement and spawn positions into the supplied setup.</summary>
        /// <remarks>NoAlloc. Lab initialization helper; does not restore characters, behavior groups, or loadouts.</remarks>
        public void RestoreSetupPreferencesToArena(CharacterTestArenaMatchConfig config)
        {
            if (config == null)
                return;
            for (int i = 0; i < _slots.Length; i++)
            {
                CharacterTestRuntimeAiLabDraftSlot source = _slots[i];
                CharacterTestArenaSlotConfig target = source != null
                    ? config.TryGetSlot(source.TeamId, source.SlotIndex)
                    : null;
                if (target == null)
                    continue;
                target.Enabled = source.Enabled;
                config.SetSpawnPosition(
                    source.TeamId,
                    source.SlotIndex,
                    new UnityEngine.Vector2(source.SpawnX, source.SpawnZ));
            }
        }

        public bool TryDuplicateEntry(int teamId, int slotIndex, int entryIndex, out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            if (IsLockedFreeStrategy(group.Entries[entryIndex].CoreSlotKind))
            {
                message = "锁定的免费基础策略不能复制。";
                RestoreState(before);
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft source = group.Entries[entryIndex];
            var copy = source.Clone(newEntryIdentity: true);
            copy.Order = source.Order + 1;
            group.Entries.Insert(entryIndex + 1, copy);
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            message = "已在当前行为卡后创建副本。";
            return true;
        }

        /// <summary>Adds one existing workspace behavior card to the selected behavior group.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab authoring command.</remarks>
        public bool TryAddExistingCardEntry(
            int teamId,
            int slotIndex,
            string cardDraftId,
            out int entryIndex,
            out string message)
        {
            return TryAddExistingCardEntry(
                teamId,
                slotIndex,
                cardDraftId,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other,
                out entryIndex,
                out message);
        }

        public bool TryAddExistingCardEntry(
            int teamId,
            int slotIndex,
            string cardDraftId,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            out int entryIndex,
            out string message) =>
            TryAddExistingCardEntry(
                teamId,
                slotIndex,
                cardDraftId,
                new[] { responsibilityKind },
                out entryIndex,
                out message);

        public bool TryAddExistingCardEntry(
            int teamId,
            int slotIndex,
            string cardDraftId,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            WorkspaceState before = CaptureState();
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(cardDraftId)
                || !_cards.ContainsKey(cardDraftId))
            {
                message = "Behavior card draft is unavailable.";
                return false;
            }
            if (!TryGetGroupForSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                message = "Behavior group draft is missing.";
                return false;
            }
            if (group.Entries.Count >= MaximumBehaviorGroupEntries)
            {
                message = "行为组已达到 16 张卡的上限。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft source = FindEntryForCard(cardDraftId);
            if (source == null && _cards[cardDraftId].HasAuthoredSource)
            {
                var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, 0);
                source = new CharacterTestRuntimeAiLabBehaviorEntryDraft(null, cardDraftId,
                    runtimeEntry.BehaviorId, 0, false, runtimeEntry);
            }
            if (source == null)
            {
                message = "Behavior card runtime entry is unavailable.";
                return false;
            }

            if (CountGroupReferences(group.GroupDraftId) > 1)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
                group = group.Clone(newIdentity: true);
                _groups.Add(group.GroupDraftId, group);
                slot.GroupDraftId = group.GroupDraftId;
                TouchWorkspace();
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft copy = source.Clone(newEntryIdentity: true);
            copy.Order = (group.Entries.Count + 1) * 10;
            copy.Disabled = false;
            copy.RuntimeEntry.Disabled = false;
            copy.SetResponsibilities(responsibilities);
            group.Entries.Add(copy);
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            entryIndex = group.Entries.Count - 1;
            message = "行为卡已添加到当前行为组。";
            return true;
        }

        /// <summary>Adds one immutable project-card revision to a project behavior-group draft.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab behavior-group authoring command.</remarks>
        public bool TryAddPublishedCardToBehaviorGroup(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (publishedCard?.Metadata == null)
            {
                message = "项目行为卡不可用。";
                return false;
            }
            if (!TryGetGroupForSlot(
                    teamId,
                    slotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.HasProjectSource
                || group.IsBehaviorTestComposition)
            {
                message = "当前没有可编辑的项目行为组。";
                return false;
            }
            if (group.Entries.Count >= MaximumBehaviorGroupEntries)
            {
                message = "行为组已达到 16 张卡的上限。";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] normalized =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(responsibilities);

            WorkspaceState before = CaptureState();
            if (CountGroupReferences(group.GroupDraftId) > 1)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
                group = group.Clone(newIdentity: true);
                _groups.Add(group.GroupDraftId, group);
                slot.GroupDraftId = group.GroupDraftId;
                TouchWorkspace();
            }

            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                GetOrCreateSharedPublishedCard(publishedCard);
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: (group.Entries.Count + 1) * 10)
            {
                EntryId = publishedCard.StableCardId
            };
            runtimeEntry.SetResponsibilities(normalized);
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                runtimeEntry,
                publishedCard.Metadata);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before, "添加行为卡");
            entryIndex = group.Entries.Count - 1;
            message = "行为卡已添加到当前行为组。";
            return true;
        }

        public bool TryAddPublishedCardToBehaviorGroup(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out int entryIndex,
            out string message)
        {
            return TryAddPublishedCardToBehaviorGroup(
                teamId,
                slotIndex,
                publishedCard,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.ResolveCompatibleResponsibilities(
                    publishedCard?.Metadata?.RequiredCapabilities),
                out entryIndex,
                out message);
        }

        public bool TryAddPublishedCardToBehaviorGroup(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (publishedCard?.Metadata == null)
            {
                message = "项目行为卡不可用。";
                return false;
            }
            if (!TryGetGroupForSlot(
                    teamId,
                    slotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.HasProjectSource
                || group.IsBehaviorTestComposition)
            {
                message = "当前没有可编辑的项目行为组。";
                return false;
            }
            if (group.Entries.Count >= MaximumBehaviorGroupEntries)
            {
                message = "行为组已达到 16 张卡的上限。";
                return false;
            }
            WorkspaceState before = CaptureState();
            if (CountGroupReferences(group.GroupDraftId) > 1)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
                group = group.Clone(newIdentity: true);
                _groups.Add(group.GroupDraftId, group);
                slot.GroupDraftId = group.GroupDraftId;
                TouchWorkspace();
            }

            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                GetOrCreateSharedPublishedCard(publishedCard);
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: (group.Entries.Count + 1) * 10)
            {
                EntryId = publishedCard.StableCardId,
            };
            runtimeEntry.SetResponsibilities(new[] { responsibilityKind });
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                runtimeEntry,
                publishedCard.Metadata);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            entryIndex = group.Entries.Count - 1;
            message = "行为卡已添加到当前行为组。";
            return true;
        }

        /// <summary>
        /// Adds an immutable project-card revision as a companion in the current behavior-test
        /// composition. The current authored card remains the first-class draft; this operation
        /// only extends its local test context and never creates a project behavior-group identity.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab test-composition editing only.</remarks>
        public bool TryAddPublishedCardToTestComposition(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (publishedCard?.Metadata == null)
            {
                message = "项目行为卡不可用。";
                return false;
            }
            if (!TryGetGroupForSlot(
                    teamId,
                    slotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.IsBehaviorTestComposition)
            {
                message = "当前没有可编辑的行为测试组合。";
                return false;
            }
            if (group.Entries.Count >= MaximumBehaviorGroupEntries)
            {
                message = "测试组合已达到 16 张行为卡上限。";
                return false;
            }
            WorkspaceState before = CaptureState();
            CharacterTestRuntimeAiLabBehaviorCardDraft card =
                GetOrCreateSharedPublishedCard(publishedCard);
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: (group.Entries.Count + 1) * 10)
            {
                EntryId = publishedCard.StableCardId,
            };
            runtimeEntry.SetResponsibilities(new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other
            });
            CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.ConfigureCanonicalArbitration(
                runtimeEntry,
                publishedCard.Metadata);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                runtimeEntry.BehaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            entryIndex = group.Entries.Count - 1;
            message = "已添加陪测行为；只影响当前行为草稿的测试组合。";
            return true;
        }

        /// <summary>Atomically replaces one physical core slot with an existing compatible card.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab authoring command.</remarks>
        public bool TryReplaceCoreSlot(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            string cardDraftId,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (!CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(slotKind))
            {
                message = "请选择一个基础槽。";
                return false;
            }
            if (IsLockedFreeStrategy(slotKind))
            {
                message = "寻敌与兜底是锁定的免费基础策略，不能替换为行为卡。";
                return false;
            }
            if (!TryGetGroupForSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                message = "Behavior group draft is missing.";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft source = FindEntryForCard(cardDraftId);
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            if (source == null
                || !catalog.TryGetDefinition(
                    source.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                || !CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCompatible(slotKind, definition))
            {
                message = "所选行为与“"
                    + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                    + "”基础槽不兼容。";
                return false;
            }
            string roleMessage = string.Empty;
            if (!_cards.TryGetValue(cardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft sourceCard)
                || !CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy.Fulfills(
                    slotKind,
                    sourceCard,
                    out roleMessage))
            {
                message = string.IsNullOrEmpty(roleMessage)
                    ? "所选行为卡草稿不可用。"
                    : roleMessage;
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (group.Entries[i].CoreSlotKind == slotKind)
                {
                    entryIndex = i;
                    break;
                }
            }
            if (entryIndex < 0)
            {
                message = "目标基础槽缺失，草稿需要迁移修复。";
                return false;
            }

            WorkspaceState before = CaptureState();
            CharacterTestRuntimeAiLabBehaviorEntryDraft target = group.Entries[entryIndex];
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry replacement = source.RuntimeEntry.Clone();
            replacement.EntryId = target.EntryId;
            replacement.Order = target.Order;
            replacement.Disabled = false;
            replacement.CoreSlotKind = slotKind;
            var replacementDraft = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                target.EntryId,
                cardDraftId,
                replacement.BehaviorId,
                target.Order,
                disabled: false,
                replacement)
            {
                CoreSlotKind = slotKind
            };
            group.Entries[entryIndex] = replacementDraft;
            group.NormalizeOrders();
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport report =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                    group.Profile.Entries,
                    catalog);
            if (!report.Success)
            {
                RestoreState(before);
                entryIndex = -1;
                message = report.Message;
                return false;
            }

            Touch(group);
            PushUndo(before);
            message = "已替换“"
                + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                + "”基础行为。";
            return true;
        }

        public bool TryCreateReactiveChaseDodgePreset(
            int teamId,
            int slotIndex,
            int entryIndex,
            out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }

            group.DisplayName = "追击与闪避";
            group.Profile.DisplayName = "追击与闪避";
            group.Profile.AttackRange = 1.5f;
            group.Entries.Clear();

            AddPresetEntry(
                group,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                "目标策略：最近敌人",
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Targeting.ToString(),
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack),
                runtimeEntry => { });
            AddPresetEntry(
                group,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                "攻击威胁闪避",
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Survival.ToString(),
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic(6, 12),
                runtimeEntry =>
                {
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey, 200f);
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey, 0.2f);
                    runtimeEntry.SetInt(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey, 6);
                    runtimeEntry.SetInt(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey, 12);
                    runtimeEntry.SetInt(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionCooldownFramesKey, 30);
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey, 1f);
                });
            AddPresetEntry(
                group,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                "持续靠近目标",
                CharacterTestRuntimeAiPlannerBehaviorModuleCategory.Movement.ToString(),
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(1.5f),
                runtimeEntry =>
                {
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey, 40f);
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CostKey, 1f);
                    runtimeEntry.SetFloat(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.SpeedScaleKey, 1f);
                });

            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            message = "已创建玩法模板：持续靠近目标，并在预计命中前 6–12 帧侧向闪避。";
            return true;
        }

        public bool TryMoveEntry(int teamId, int slotIndex, int entryIndex, int direction, out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            int target = entryIndex + (direction < 0 ? -1 : 1);
            if (target < 0 || target >= group.Entries.Count)
            {
                message = direction < 0
                    ? "当前行为卡已经位于最前，无法继续上移。"
                    : "当前行为卡已经位于最后，无法继续下移。";
                RestoreState(before);
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (IsLockedFreeStrategy(entry.CoreSlotKind))
            {
                message = "锁定的免费基础策略不能移动。";
                RestoreState(before);
                return false;
            }
            if (IsLockedFreeStrategy(
                group.Entries[target].CoreSlotKind))
            {
                message = "行为卡不能越过锁定的免费基础策略。";
                RestoreState(before);
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft targetEntry = group.Entries[target];
            int sourceOrder = entry.Order;
            entry.Order = targetEntry.Order;
            entry.RuntimeEntry.Order = entry.Order;
            targetEntry.Order = sourceOrder;
            targetEntry.RuntimeEntry.Order = targetEntry.Order;
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            message = direction < 0 ? "行为卡已上移。" : "行为卡已下移。";
            return true;
        }

        public bool TrySetEntryResponsibility(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(
                    teamId,
                    slotIndex,
                    entryIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out message))
            {
                RestoreState(before);
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (_cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft projectCard)
                && projectCard.HasPublishedSource)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] authored =
                    { responsibilityKind };
                return TrySetProjectCardResponsibilities(
                    before,
                    projectCard,
                    authored,
                    out message);
            }
            if (entry.ResponsibilityKinds[0] == responsibilityKind)
            {
                message = "行为已经属于该职责。";
                return true;
            }
            entry.SetResponsibilities(new[] { responsibilityKind });
            entry.RuntimeEntry.SetResponsibilities(new[] { responsibilityKind });
            Touch(group);
            PushUndo(before, "调整行为职责");
            message = "行为已移动到“"
                + CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.GetDisplayName(responsibilityKind)
                + "”。";
            return true;
        }

        public bool TrySetEntryResponsibilities(
            int teamId,
            int slotIndex,
            int entryIndex,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(
                    teamId,
                    slotIndex,
                    entryIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out message))
            {
                RestoreState(before);
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] normalized =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(responsibilities);
            if (_cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft projectCard)
                && projectCard.HasPublishedSource)
            {
                return TrySetProjectCardResponsibilities(
                    before,
                    projectCard,
                    normalized,
                    out message);
            }
            if (CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.SetEquals(
                    entry.ResponsibilityKinds,
                    normalized))
            {
                message = "行为职责没有变化。";
                return true;
            }

            entry.SetResponsibilities(normalized);
            Touch(group);
            PushUndo(before, "调整行为职责");
            message = "行为职责已更新。";
            return true;
        }

        private bool TrySetProjectCardResponsibilities(
            WorkspaceState before,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out string message)
        {
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] normalized =
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(responsibilities);
            if (CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.SetEquals(
                    card.ResponsibilityKinds,
                    normalized))
            {
                message = "行为卡职责没有变化。";
                return true;
            }

            card.ResponsibilityKinds = normalized;
            SynchronizeCardResponsibilities(card);
            card.CurrentRevision++;
            TouchWorkspace();
            PushUndo(before, "编辑行为卡职责");
            message = "已修改行为卡职责草稿；点击保存后才会写入项目行为卡文件。";
            return true;
        }

        private void SynchronizeCardResponsibilities(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft candidate in _groups.Values)
            {
                for (int i = 0; candidate != null && i < candidate.Entries.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft candidateEntry = candidate.Entries[i];
                    if (string.Equals(
                            candidateEntry.CardDraftId,
                            card.CardDraftId,
                            StringComparison.Ordinal))
                    {
                        candidateEntry.SetResponsibilities(card.ResponsibilityKinds);
                        for (int profileIndex = 0;
                            candidate.Profile != null
                            && profileIndex < candidate.Profile.Entries.Count;
                            profileIndex++)
                        {
                            CharacterTestRuntimeAiPlannerBehaviorModuleEntry profileEntry =
                                candidate.Profile.Entries[profileIndex];
                            if (string.Equals(
                                    profileEntry.EntryId,
                                    candidateEntry.EntryId,
                                    StringComparison.Ordinal))
                            {
                                profileEntry.SetResponsibilities(card.ResponsibilityKinds);
                                break;
                            }
                        }
                    }
                }
            }
        }

        public bool TryToggleEntry(int teamId, int slotIndex, int entryIndex, out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (IsLockedFreeStrategy(entry.CoreSlotKind))
            {
                message = "锁定的免费基础策略必须始终启用。";
                RestoreState(before);
                return false;
            }
            entry.Disabled = !entry.Disabled;
            entry.RuntimeEntry.Disabled = entry.Disabled;
            Touch(group);
            PushUndo(before);
            message = group.Entries[entryIndex].Disabled
                ? "行为卡已禁用，不再参与运行时裁决。"
                : "行为卡已启用，将参与运行时裁决。";
            return true;
        }

        public bool TryRemoveEntry(int teamId, int slotIndex, int entryIndex, out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            if (IsLockedFreeStrategy(group.Entries[entryIndex].CoreSlotKind))
            {
                message = "锁定的免费基础策略不能删除。";
                RestoreState(before);
                return false;
            }
            group.Entries.RemoveAt(entryIndex);
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before);
            message = "行为卡已从当前行为组移除，可通过撤销恢复。";
            return true;
        }

        public bool TryRemovePublishedCardReferences(
            string stableCardId,
            out int removedReferences,
            out string message)
        {
            removedReferences = 0;
            string normalized = stableCardId?.Trim() ?? string.Empty;
            if (IsReadOnly || normalized.Length == 0)
            {
                message = IsReadOnly
                    ? "Runtime AI Lab workspace is read-only."
                    : "行为卡 ID 不可用。";
                return false;
            }

            WorkspaceState before = CaptureState();
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in _groups.Values)
            {
                bool changed = false;
                for (int entryIndex = group.Entries.Count - 1; entryIndex >= 0; entryIndex--)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (_cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                        && string.Equals(
                            card.PublishedStableId,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        group.Entries.RemoveAt(entryIndex);
                        removedReferences++;
                        changed = true;
                    }
                }
                if (changed)
                {
                    group.NormalizeOrders();
                    Touch(group);
                }
            }

            var removedCardDraftIds = new List<string>();
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorCardDraft> pair in _cards)
            {
                if (string.Equals(
                    pair.Value?.PublishedStableId,
                    normalized,
                    StringComparison.Ordinal))
                {
                    removedCardDraftIds.Add(pair.Key);
                }
            }
            for (int i = 0; i < removedCardDraftIds.Count; i++)
                _cards.Remove(removedCardDraftIds[i]);

            if (removedReferences == 0 && removedCardDraftIds.Count == 0)
            {
                message = "工作区没有这张行为卡的草稿或引用。";
                return true;
            }
            TouchWorkspace();
            PushUndo(before, "删除项目行为卡引用");
            message = "已清理当前实验中的 "
                + removedReferences.ToString(CultureInfo.InvariantCulture)
                + " 处行为卡引用。";
            return true;
        }

        public bool TryBeginCardEdit(
            int teamId,
            int slotIndex,
            int entryIndex,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string message)
        {
            card = null;
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (IsLockedFreeStrategy(entry.CoreSlotKind))
            {
                message = "寻敌与兜底是锁定的免费基础策略，不是可编辑行为卡。";
                return false;
            }
            if (!_cards.TryGetValue(entry.CardDraftId, out card))
            {
                message = "Behavior card draft is missing.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        public bool TryReplaceCardPayload(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            out string message)
        {
            return TryReplaceCardPayloadAndConfigureRuntime(
                teamId,
                slotIndex,
                entryIndex,
                payload,
                null,
                out message);
        }

        public bool TryReplaceCardPayloadAndConfigureRuntime(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            Action<CharacterTestRuntimeAiLabBehaviorGroupDraft, CharacterTestRuntimeAiLabBehaviorEntryDraft> configureRuntime,
            out string message)
        {
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            if (IsLockedFreeStrategy(group.Entries[entryIndex].CoreSlotKind))
            {
                message = "锁定的免费基础策略不能写入行为卡草稿。";
                RestoreState(before);
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (!_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card))
            {
                RestoreState(before);
                message = "Behavior card draft is missing.";
                return false;
            }
            if (card.HasAuthoredSource)
            {
                RestoreState(before);
                message = "Tactical source cannot be replaced through the legacy payload editor.";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload nextLogic =
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(
                    payload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardPayload());
            if (CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(entry.CoreSlotKind)
                && !CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy.Fulfills(
                    entry.CoreSlotKind,
                    nextLogic,
                    out string roleMessage))
            {
                RestoreState(before);
                message = roleMessage;
                return false;
            }
            card.Payload = payload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardPayload();
            card.LogicPayload = nextLogic;
            card.LogicPayloadMigratedFromV1 = true;
            configureRuntime?.Invoke(group, entry);
            card.CurrentRevision++;
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardValidator.Validate(card.Payload);
            bool invalid = CharacterTestRuntimeAiLabBehaviorCardValidator.HasErrors(diagnostics);
            card.LastCompileStatus = invalid ? "DraftInvalid" : "SchemaValid";
            TouchWorkspace();
            PushUndo(before);
            message = invalid
                ? "Behavior card payload saved with diagnostics: " + diagnostics[0].Code
                : "Behavior card payload updated.";
            return true;
        }

        public bool TryReplaceCardLogicPayload(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string message)
        {
            return TryReplaceCardLogicPayload(
                teamId,
                slotIndex,
                entryIndex,
                payload,
                "行为卡编辑",
                out message);
        }

        internal bool TryReplaceCardLogicPayload(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string operationLabel,
            out string message)
        {
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload normalized =
                payload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardLogicPayload();
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(normalized);
            WorkspaceState before = CaptureState();
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group, out message))
            {
                RestoreState(before);
                return false;
            }
            if (IsLockedFreeStrategy(group.Entries[entryIndex].CoreSlotKind))
            {
                message = "锁定的免费基础策略不能写入行为卡草稿。";
                RestoreState(before);
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (!_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card))
            {
                RestoreState(before);
                message = "Behavior card draft is missing.";
                return false;
            }
            if (CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(entry.CoreSlotKind)
                && !CharacterTestRuntimeAiLabBehaviorCoreSlotCardPolicy.HasRequiredActionShape(
                    entry.CoreSlotKind,
                    normalized,
                    out string roleMessage))
            {
                RestoreState(before);
                message = roleMessage;
                return false;
            }

            if (card.HasAuthoredSource)
            {
                RestoreState(before);
                message = "Tactical source is read-only in the legacy Recipe editor.";
                return false;
            }
            card.LogicPayload = normalized;
            card.LogicPayloadMigratedFromV1 = false;
            card.CurrentRevision++;
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                CharacterTestRuntimeAiLabBehaviorCardLogicValidator.Validate(card.LogicPayload);
            bool invalid = CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics);
            card.LastCompileStatus = invalid ? "DraftInvalid" : "SchemaValidV2";
            TouchWorkspace();
            PushUndo(before, operationLabel);
            message = invalid
                ? "Behavior card logic saved with diagnostics: " + diagnostics[0].Code
                : "Behavior card logic updated.";
            return true;
        }

        /// <summary>
        /// Re-anchors the current isolated draft to an immutable Published revision. Publishing is an
        /// irreversible authority boundary, so pre-publish undo history is discarded instead of being
        /// allowed to restore an obsolete source identity.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit successful Publish boundary only.</remarks>
        public bool TryAdoptPublishedRevision(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out string message)
        {
            if (publishedCard == null)
            {
                message = "Published behavior card is unavailable.";
                return false;
            }
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out var group, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (IsLockedFreeStrategy(entry.CoreSlotKind))
            {
                message = "锁定的免费基础策略不能采用行为卡 revision。";
                return false;
            }
            if (!_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || !string.Equals(card.PublishedStableId, publishedCard.StableCardId, StringComparison.Ordinal))
            {
                message = "Published behavior card does not match the current draft source.";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
            if (!compile.Success
                || compile.Program == null
                || compile.Program.SemanticHash != publishedCard.SemanticHash)
            {
                message = "Published behavior card semantics do not match the current draft.";
                return false;
            }

            card.SourcePublishedRevision = publishedCard.Revision;
            card.SourcePublishedSemanticHash = publishedCard.SemanticHash;
            card.SourcePublishedContentHash = publishedCard.ContentHash;
            card.LastCompileStatus = "Published";
            card.LastCompiledRevision = card.CurrentRevision;
            card.CurrentRevision++;
            TouchWorkspace();
            _undo.Clear();
            _redo.Clear();
            message = "Draft source adopted Published revision "
                + publishedCard.Revision.ToString(CultureInfo.InvariantCulture)
                + "; pre-publish undo history was closed.";
            return true;
        }

        /// <summary>Edits project-card metadata on the local working copy only.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab form edit only.</remarks>
        public bool TrySetProjectCardMetadata(
            int teamId,
            int slotIndex,
            int entryIndex,
            CharacterTestRuntimeAiLabProjectCardMetadataField field,
            string value,
            out string message)
        {
            if (!TryGetEditableGroup(teamId, slotIndex, entryIndex, out var group, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (IsLockedFreeStrategy(entry.CoreSlotKind)
                || !_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || !card.HasPublishedSource)
            {
                message = "免费基础策略或非项目行为卡不能编辑项目元数据。";
                return false;
            }

            string normalized = value?.Trim() ?? string.Empty;
            if (field == CharacterTestRuntimeAiLabProjectCardMetadataField.DisplayName
                && normalized.Length == 0)
            {
                message = "行为卡显示名称不能为空。";
                return false;
            }
            if (normalized.Length > 512)
            {
                message = "行为卡元数据字段过长。";
                return false;
            }

            WorkspaceState before = CaptureState();
            switch (field)
            {
                case CharacterTestRuntimeAiLabProjectCardMetadataField.DisplayName:
                    card.DisplayName = normalized;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.Cost:
                    if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost)
                        || cost < 0
                        || cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost)
                    {
                        message = "行为卡费用必须是 0 到 10 的整数。";
                        return false;
                    }
                    card.Cost = cost;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.CooldownSeconds:
                    if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out float cooldown)
                        || float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f || cooldown * 60d >= int.MaxValue)
                    { message = "Cooldown must be finite, nonnegative and fit the authority frame range."; return false; }
                    card.CooldownSeconds = cooldown;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.ShortDescription:
                    card.ShortDescription = normalized;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.UseSummary:
                    card.UseSummary = normalized;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.RiskSummary:
                    card.RiskSummary = normalized;
                    break;
                case CharacterTestRuntimeAiLabProjectCardMetadataField.Tags:
                    card.TagsText = normalized;
                    break;
                default:
                    message = "未知的行为卡元数据字段。";
                    return false;
            }
            card.CurrentRevision++;
            TouchWorkspace();
            PushUndo(before, "编辑项目行为卡元数据");
            message = "已修改本地行为卡草稿；项目 JSON 与当前实验尚未变化。";
            return true;
        }

        /// <summary>Refreshes the shared behavior-card definition from a newer Published revision.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab group-draft command only.</remarks>
        public bool TryUpgradeProjectCardReference(
            int teamId,
            int slotIndex,
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            out string message)
        {
            if (publishedCard == null
                || !string.Equals(publishedCard.StableCardId, stableCardId, StringComparison.Ordinal))
            {
                message = "项目行为卡版本不可用。";
                return false;
            }
            if (!TryGetGroupForSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.HasProjectSource)
            {
                message = "当前行为组不是项目 JSON 工作副本。";
                return false;
            }
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (!_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !string.Equals(card.PublishedStableId, stableCardId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (publishedCard.Revision <= card.SourcePublishedRevision)
                {
                    message = "共享行为卡已经是最新版本。";
                    return false;
                }

                WorkspaceState before = CaptureState();
                ImportPublishedTacticalDependencies(publishedCard);
                card.CopyDefinitionFrom(
                    CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publishedCard));
                SynchronizeCardResponsibilities(card);
                card.LastCompileStatus = "ProjectRevisionUpgraded";
                TouchWorkspace();
                PushUndo(before, "刷新共享行为卡版本");
                message = "已将共享行为卡“" + stableCardId + "”刷新到 r"
                    + publishedCard.Revision.ToString(CultureInfo.InvariantCulture)
                    + "；所有引用它的行为组将使用同一份定义。";
                return true;
            }
            message = "当前行为组没有引用这张项目行为卡。";
            return false;
        }

        /// <summary>
        /// Explicitly re-anchors a retained local group/card working copy to the latest disk
        /// revisions after an optimistic-concurrency conflict. Authored content is preserved.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit conflict-resolution command only.</remarks>
        public bool TryRebaseProjectSources(
            int teamId,
            int slotIndex,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision latestGroup,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog latestCards,
            string groupCatalogPath,
            string groupFileHash,
            out string message)
        {
            if (latestGroup == null || latestCards == null)
            {
                message = "最新项目行为目录不可用。";
                return false;
            }
            if (!TryGetGroupForSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.HasProjectSource
                || !string.Equals(group.PublishedStableId, latestGroup.StableGroupId, StringComparison.Ordinal))
            {
                message = "当前本地工作副本与项目行为组不匹配。";
                return false;
            }

            WorkspaceState before = CaptureState();
            group.SourceProjectRevision = latestGroup.Revision;
            group.SourceProjectContentHash = latestGroup.ContentHash;
            group.SourceProjectPath = groupCatalogPath ?? string.Empty;
            group.SourceCatalogFileHash = groupFileHash ?? string.Empty;
            // Card definitions are shared by stable identity. Re-anchor their Published source
            // identity without replacing the retained authored body or creating group-local copies.
            // This is required when a catalog migration changes revision hashes while a Main
            // workspace still carries unpublished edits based on the same stable cards.
            var rebasedCardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                if (entry == null
                    || string.IsNullOrWhiteSpace(entry.CardDraftId)
                    || !_cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || !card.HasPublishedSource
                    || !rebasedCardIds.Add(card.CardDraftId)
                    || !latestCards.TryResolveLatestCompatible(
                        card.PublishedStableId,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                            .CurrentRuntimeContractVersion,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard latestCard))
                {
                    continue;
                }

                card.SourcePublishedRevision = latestCard.Revision;
                card.SourcePublishedSemanticHash = latestCard.SemanticHash;
                card.SourcePublishedContentHash = latestCard.ContentHash;
            }
            Touch(group);
            PushUndo(before, "基于新版重新应用修改");
            message = "已把本地修改重新锚定到项目行为组 r"
                + latestGroup.Revision.ToString(CultureInfo.InvariantCulture)
                + "；尚未写入项目 JSON。";
            return true;
        }

        public bool TryUndo(out string message)
        {
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (_undo.Count == 0)
            {
                message = "No Runtime AI Lab draft mutation to undo.";
                return false;
            }

            HistoryEntry entry = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            WorkspaceState current = CaptureState();
            int nextRevision = CurrentRevision + 1;
            RestoreState(entry.State);
            CurrentRevision = nextRevision;
            UpdatedUtc = DateTime.UtcNow;
            AddHistory(_redo, current, entry.Label);
            message = "已撤销：" + entry.Label;
            return true;
        }

        public bool TryRedo(out string message)
        {
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (_redo.Count == 0)
            {
                message = "No Runtime AI Lab draft mutation to redo.";
                return false;
            }

            HistoryEntry entry = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            WorkspaceState current = CaptureState();
            int nextRevision = CurrentRevision + 1;
            RestoreState(entry.State);
            CurrentRevision = nextRevision;
            UpdatedUtc = DateTime.UtcNow;
            AddHistory(_undo, current, entry.Label);
            message = "已重做：" + entry.Label;
            return true;
        }

        internal void AddLoadedGroup(CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            _groups[group.GroupDraftId] = group;
        }

        internal void AddLoadedCard(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            _cards[card.CardDraftId] = card;
        }

        /// <summary>
        /// Keeps standalone project-card working copies in the versioned workspace even when no
        /// behavior-group entry currently references them.
        /// </summary>
        /// <remarks>LowFreqAlloc: production Lab workspace open/recovery only.</remarks>
        internal bool EnsurePublishedProjectCardDrafts(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog)
        {
            if (catalog == null)
                return false;

            int initialCount = _cards.Count;
            for (int cardIndex = 0; cardIndex < catalog.Cards.Count; cardIndex++)
            {
                var published = catalog.Cards[cardIndex];
                if (published.TacticalSource != null && catalog.TryResolveLatestCompatible(published.StableCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var latest)
                    && latest.Revision != published.Revision) continue;
                GetOrCreateSharedPublishedCard(published);
            }
            if (_cards.Count == initialCount)
                return false;

            TouchWorkspace();
            Diagnostic = "已将项目行为卡工作副本补入共享草稿。";
            return true;
        }

        internal bool TryGetProjectCardDraft(
            string stableCardId,
            out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            draft = null;
            if (string.IsNullOrWhiteSpace(stableCardId))
                return false;
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in _cards.Values)
            {
                if (candidate == null
                    || !string.Equals(
                        candidate.PublishedStableId,
                        stableCardId,
                        StringComparison.Ordinal)
                    || (draft != null && candidate.CurrentRevision <= draft.CurrentRevision))
                {
                    continue;
                }
                draft = candidate;
            }
            return draft != null;
        }

        internal void TrackStandaloneProjectCardDraft(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            if (draft == null)
                throw new ArgumentNullException(nameof(draft));
            _cards[draft.CardDraftId] = draft;
            TouchWorkspace();
        }

        internal bool TryAdoptStandaloneProjectCardRevision(
            string stableCardId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            if (publishedCard == null
                || !TryGetProjectCardDraft(stableCardId, out CharacterTestRuntimeAiLabBehaviorCardDraft draft))
            {
                return false;
            }
            ImportPublishedTacticalDependencies(publishedCard);
            draft.CopyDefinitionFrom(
                CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publishedCard));
            TouchWorkspace();
            return true;
        }

        internal void SetLoadedSlot(CharacterTestRuntimeAiLabDraftSlot slot)
        {
            int index = ToSlotIndex(slot.TeamId, slot.SlotIndex);
            if (index >= 0)
                _slots[index] = slot;
        }

        private bool TryGetEditableGroup(
            int teamId,
            int slotIndex,
            int entryIndex,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string message)
        {
            group = null;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (!TryGetGroupForSlot(teamId, slotIndex, out group))
            {
                message = "Behavior group draft is missing.";
                return false;
            }

            if (CountGroupReferences(group.GroupDraftId) > 1)
            {
                CharacterTestRuntimeAiLabDraftSlot slot = TryGetSlot(teamId, slotIndex);
                group = group.Clone(newIdentity: true);
                _groups.Add(group.GroupDraftId, group);
                slot.GroupDraftId = group.GroupDraftId;
                TouchWorkspace();
            }

            if (entryIndex < 0 || entryIndex >= group.Entries.Count)
            {
                message = "Behavior entry is not available.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        private static bool IsLockedFreeStrategy(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            return false;
        }

        internal bool TryValidateResponsibilityAssignments(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string message)
        {
            if (group == null)
            {
                message = "行为组不可用。";
                return false;
            }
            message = string.Empty;
            return true;
        }

        private bool TryValidateAllResponsibilityAssignments(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string message)
        {
            if (group == null)
            {
                message = "行为组不可用。";
                return false;
            }
            message = string.Empty;
            return true;
        }

        private void ImportSlots(
            CharacterTestArenaMatchConfig config,
            int teamId,
            IReadOnlyDictionary<int, string> groupByArenaId)
        {
            for (int slotIndex = 0; slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam; slotIndex++)
            {
                CharacterTestArenaSlotConfig source = config?.TryGetSlot(teamId, slotIndex);
                string groupId = string.Empty;
                if (source != null)
                    groupByArenaId.TryGetValue(source.BehaviorGroupDraftId, out groupId);
                if (string.IsNullOrEmpty(groupId) && _groups.Count > 0)
                {
                    foreach (string candidate in _groups.Keys)
                    {
                        groupId = candidate;
                        break;
                    }
                }

                var slot = new CharacterTestRuntimeAiLabDraftSlot(
                    teamId,
                    slotIndex,
                    source != null && source.Enabled,
                    source != null ? source.CharacterId : CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    groupId,
                    source?.BehaviorGroupDraftId ?? 0);
                if (config != null && config.TryGetSpawnPosition(teamId, slotIndex, out UnityEngine.Vector2 spawn))
                {
                    slot.SpawnX = spawn.x;
                    slot.SpawnZ = spawn.y;
                }
                _slots[ToSlotIndex(teamId, slotIndex)] = slot;
            }
        }

        private int CountGroupReferences(string groupDraftId)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && string.Equals(_slots[i].GroupDraftId, groupDraftId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private CharacterTestRuntimeAiLabBehaviorGroupDraft FindPublishedCardAuthoringGroup(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            bool requireProjectSource,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out int entryIndex)
        {
            CharacterTestRuntimeAiLabBehaviorGroupDraft matchedGroup = null;
            CharacterTestRuntimeAiLabBehaviorCardDraft matchedCard = null;
            int matchedEntryIndex = -1;
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft candidateGroup in _groups.Values)
            {
                if (candidateGroup == null
                    || candidateGroup.Entries.Count != 1
                    || candidateGroup.HasProjectSource != requireProjectSource)
                    continue;
                for (int i = 0; i < candidateGroup.Entries.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = candidateGroup.Entries[i];
                    if (!_cards.TryGetValue(entry.CardDraftId, out CharacterTestRuntimeAiLabBehaviorCardDraft candidateCard)
                        || !candidateCard.MatchesPublishedSource(publishedCard))
                    {
                        continue;
                    }

                    bool isEarlierGroup = matchedGroup == null
                        || string.CompareOrdinal(candidateGroup.GroupDraftId, matchedGroup.GroupDraftId) < 0;
                    bool isSameGroupEarlierEntry = matchedGroup != null
                        && string.Equals(candidateGroup.GroupDraftId, matchedGroup.GroupDraftId, StringComparison.Ordinal)
                        && i < matchedEntryIndex;
                    if (!isEarlierGroup && !isSameGroupEarlierEntry)
                        continue;

                    matchedGroup = candidateGroup;
                    matchedCard = candidateCard;
                    matchedEntryIndex = i;
                }
            }
            card = matchedCard;
            entryIndex = matchedEntryIndex;
            return matchedGroup;
        }

        /// <summary>
        /// Restores the v3 workspace invariant that one stable behavior-card identity owns one
        /// mutable draft definition. Behavior groups retain only installation records that point at
        /// that shared definition.
        /// </summary>
        /// <remarks>LowFreqAlloc: workspace import/recovery migration only.</remarks>
        internal bool CanonicalizeSharedBehaviorCards()
        {
            if (IsReadOnly || _cards.Count < 2)
                return false;

            var canonicalByStableId = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(
                StringComparer.Ordinal);
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in _cards.Values)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.PublishedStableId))
                    continue;
                if (!canonicalByStableId.TryGetValue(candidate.PublishedStableId, out CharacterTestRuntimeAiLabBehaviorCardDraft current)
                    || IsPreferredSharedCard(candidate, current))
                {
                    canonicalByStableId[candidate.PublishedStableId] = candidate;
                }
            }

            var remap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in _cards.Values)
            {
                if (candidate == null
                    || string.IsNullOrWhiteSpace(candidate.PublishedStableId)
                    || !canonicalByStableId.TryGetValue(candidate.PublishedStableId, out CharacterTestRuntimeAiLabBehaviorCardDraft canonical)
                    || string.Equals(candidate.CardDraftId, canonical.CardDraftId, StringComparison.Ordinal))
                {
                    continue;
                }
                remap[candidate.CardDraftId] = canonical.CardDraftId;
            }
            if (remap.Count == 0)
                return false;

            int referenceCount = 0;
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in _groups.Values)
            {
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (!remap.TryGetValue(entry.CardDraftId, out string canonicalId))
                        continue;
                    entry.CardDraftId = canonicalId;
                    referenceCount++;
                }
            }
            foreach (string duplicateId in remap.Keys)
                _cards.Remove(duplicateId);

            TouchWorkspace();
            Diagnostic = "已将 " + remap.Count.ToString(CultureInfo.InvariantCulture)
                + " 份组内行为卡副本合并为共享行为卡，并更新 "
                + referenceCount.ToString(CultureInfo.InvariantCulture) + " 条行为组引用。";
            return true;
        }

        private CharacterTestRuntimeAiLabBehaviorCardDraft GetOrCreateSharedPublishedCard(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            if (publishedCard.TacticalSource != null && TryGetProjectCardDraft(publishedCard.StableCardId, out var existing))
            {
                if (existing.SourcePublishedRevision != publishedCard.Revision || existing.CurrentRevision > 1) return existing;
                bool complete = true;
                foreach (var definition in publishedCard.TacticalSource.CloneDefinitions())
                    if (!_tacticalDefinitions.ContainsKey(definition.DefinitionId)) complete = false;
                if (complete) return existing;
            }
            ImportPublishedTacticalDependencies(publishedCard);
            return GetOrCreateSharedPublishedCard(_cards, publishedCard);
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardDraft GetOrCreateSharedPublishedCard(
            IDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            if (cards == null)
                throw new ArgumentNullException(nameof(cards));
            if (publishedCard == null)
                throw new ArgumentNullException(nameof(publishedCard));

            CharacterTestRuntimeAiLabBehaviorCardDraft shared = null;
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft candidate in cards.Values)
            {
                if (candidate != null
                    && string.Equals(candidate.PublishedStableId, publishedCard.StableCardId, StringComparison.Ordinal)
                    && (shared == null || IsPreferredSharedCard(candidate, shared)))
                {
                    shared = candidate;
                }
            }
            if (shared == null)
            {
                shared = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publishedCard);
                cards.Add(shared.CardDraftId, shared);
                return shared;
            }

            if (publishedCard.TacticalSource != null && shared.SourcePublishedRevision != publishedCard.Revision)
                throw new InvalidOperationException("同一战术卡的不同固定版本不能隐式合并到一个工作副本，请显式选择版本：" + publishedCard.StableCardId);

            // A pristine imported definition follows the newest Published revision. A locally edited
            // definition remains authoritative until the author explicitly publishes or discards it.
            if (shared.CurrentRevision <= 1
                && publishedCard.Revision > shared.SourcePublishedRevision)
            {
                CharacterTestRuntimeAiLabBehaviorCardDraft latest =
                    CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(publishedCard);
                shared.CopyDefinitionFrom(latest);
            }
            return shared;
        }

        private static bool IsPreferredSharedCard(
            CharacterTestRuntimeAiLabBehaviorCardDraft candidate,
            CharacterTestRuntimeAiLabBehaviorCardDraft current)
        {
            if (candidate.CurrentRevision != current.CurrentRevision)
                return candidate.CurrentRevision > current.CurrentRevision;
            if (candidate.SourcePublishedRevision != current.SourcePublishedRevision)
                return candidate.SourcePublishedRevision > current.SourcePublishedRevision;
            return string.CompareOrdinal(candidate.CardDraftId, current.CardDraftId) < 0;
        }

        private CharacterTestRuntimeAiLabBehaviorEntryDraft FindEntryForCard(string cardDraftId)
        {
            CharacterTestRuntimeAiLabBehaviorEntryDraft result = null;
            string resultGroupId = null;
            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair in _groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = pair.Value;
                for (int i = 0; i < group.Entries.Count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft candidate = group.Entries[i];
                    if (!string.Equals(candidate.CardDraftId, cardDraftId, StringComparison.Ordinal))
                        continue;
                    if (result == null
                        || string.CompareOrdinal(pair.Key, resultGroupId) < 0
                        || (string.Equals(pair.Key, resultGroupId, StringComparison.Ordinal)
                            && candidate.Order < result.Order))
                    {
                        result = candidate;
                        resultGroupId = pair.Key;
                    }
                }
            }
            return result;
        }

        private void AddPresetEntry(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            string behaviorId,
            string displayName,
            string category,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            Action<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> configure)
        {
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                behaviorId,
                enabled: true,
                order: (group.Entries.Count + 1) * 10);
            configure?.Invoke(runtimeEntry);
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                behaviorId,
                displayName,
                category,
                payload);
            _cards.Add(card.CardDraftId, card);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                null,
                card.CardDraftId,
                behaviorId,
                runtimeEntry.Order,
                disabled: false,
                runtimeEntry));
        }

        private void Touch(CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            group.CurrentRevision++;
            TouchWorkspace();
        }

        private void TouchWorkspace()
        {
            CurrentRevision++;
            UpdatedUtc = DateTime.UtcNow;
        }

        internal bool TrySetFrameLimit(int frameLimit)
        {
            if (IsReadOnly)
                return false;
            if (FrameLimit == frameLimit)
                return true;
            FrameLimit = frameLimit;
            TouchWorkspace();
            return true;
        }

        private static int ToSlotIndex(int teamId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= CharacterTestArenaMatchConfig.SlotsPerTeam)
                return -1;
            if (teamId == CharacterTestArenaMatchConfig.Team1Id)
                return slotIndex;
            if (teamId == CharacterTestArenaMatchConfig.Team2Id)
                return CharacterTestArenaMatchConfig.SlotsPerTeam + slotIndex;
            return -1;
        }

        private WorkspaceState CaptureState()
        {
            var state = new WorkspaceState
            {
                DisplayName = DisplayName,
                UpdatedUtc = UpdatedUtc,
                CurrentRevision = CurrentRevision,
                LastSavedRevision = LastSavedRevision,
                LastCompiledRevision = LastCompiledRevision,
                LastAppliedRevision = LastAppliedRevision
            };
            foreach (CharacterTestRuntimeAiLabBehaviorGroupDraft group in _groups.Values)
                state.Groups.Add(group.Clone(newIdentity: false));
            foreach (CharacterTestRuntimeAiLabBehaviorCardDraft card in _cards.Values)
                state.Cards.Add(card.Clone(newIdentity: false));
            state.TacticalDefinitions.AddRange(CloneTacticalDefinitions());
            for (int i = 0; i < _slots.Length; i++)
                state.Slots[i] = _slots[i]?.Clone();
            return state;
        }

        private void RestoreState(WorkspaceState state)
        {
            if (state == null)
                return;
            DisplayName = state.DisplayName;
            UpdatedUtc = state.UpdatedUtc;
            CurrentRevision = state.CurrentRevision;
            LastSavedRevision = state.LastSavedRevision;
            LastCompiledRevision = state.LastCompiledRevision;
            LastAppliedRevision = state.LastAppliedRevision;
            _groups.Clear();
            _cards.Clear();
            _tacticalDefinitions.Clear();
            foreach (var definition in state.TacticalDefinitions) AddLoadedTacticalDefinition(definition);
            for (int i = 0; i < state.Groups.Count; i++)
                _groups.Add(state.Groups[i].GroupDraftId, state.Groups[i].Clone(newIdentity: false));
            for (int i = 0; i < state.Cards.Count; i++)
                _cards.Add(state.Cards[i].CardDraftId, state.Cards[i].Clone(newIdentity: false));
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = state.Slots[i]?.Clone();
        }

        private void PushUndo(WorkspaceState state, string operationLabel = null)
        {
            AddHistory(_undo, state, operationLabel);
            _redo.Clear();
        }

        private static void AddHistory(
            List<HistoryEntry> target,
            WorkspaceState state,
            string operationLabel)
        {
            if (target.Count == 100)
                target.RemoveAt(0);
            target.Add(new HistoryEntry(
                state,
                string.IsNullOrWhiteSpace(operationLabel) ? "草稿编辑" : operationLabel.Trim()));
        }

        private sealed class HistoryEntry
        {
            internal HistoryEntry(WorkspaceState state, string label)
            {
                State = state;
                Label = label ?? string.Empty;
            }

            internal WorkspaceState State { get; }
            internal string Label { get; }
        }

        private sealed class WorkspaceState
        {
            public string DisplayName;
            public DateTime UpdatedUtc;
            public int CurrentRevision;
            public int LastSavedRevision;
            public int LastCompiledRevision;
            public int LastAppliedRevision;
            public readonly List<CharacterTestRuntimeAiLabBehaviorGroupDraft> Groups =
                new List<CharacterTestRuntimeAiLabBehaviorGroupDraft>();
            public readonly List<CharacterTestRuntimeAiLabBehaviorCardDraft> Cards =
                new List<CharacterTestRuntimeAiLabBehaviorCardDraft>();
            public readonly List<CharacterTestTacticalSharedDefinition> TacticalDefinitions = new List<CharacterTestTacticalSharedDefinition>();
            public readonly CharacterTestRuntimeAiLabDraftSlot[] Slots =
                new CharacterTestRuntimeAiLabDraftSlot[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorGroupDraft
    {
        public const string Schema = "mx.character-test.runtime-ai-lab.behavior-group.v4";

        public CharacterTestRuntimeAiLabBehaviorGroupDraft(
            string groupDraftId,
            string publishedStableId,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            int currentRevision = 1,
            int sourceProjectRevision = 0,
            long sourceProjectContentHash = 0,
            string sourceProjectPath = null,
            string sourceCatalogFileHash = null)
        {
            GroupDraftId = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(groupDraftId);
            PublishedStableId = publishedStableId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Behavior Group" : displayName;
            Profile = profile?.Clone() ?? CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CurrentRevision = Math.Max(1, currentRevision);
            SourceProjectRevision = sourceProjectRevision;
            SourceProjectContentHash = sourceProjectContentHash;
            SourceProjectPath = sourceProjectPath ?? string.Empty;
            SourceCatalogFileHash = sourceCatalogFileHash ?? string.Empty;
        }

        public string GroupDraftId { get; }
        public string PublishedStableId { get; set; }
        public string DisplayName { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile { get; }
        public List<CharacterTestRuntimeAiLabBehaviorEntryDraft> Entries { get; } =
            new List<CharacterTestRuntimeAiLabBehaviorEntryDraft>();
        public int CurrentRevision { get; internal set; }
        public int SourceProjectRevision { get; internal set; }
        public long SourceProjectContentHash { get; internal set; }
        public string SourceProjectPath { get; internal set; }
        public string SourceCatalogFileHash { get; internal set; }
        public bool HasProjectSource => SourceProjectRevision > 0 && SourceProjectContentHash != 0;
        public bool IsBehaviorTestComposition =>
            Profile != null
            && Profile.CompositionId != null
            && Profile.CompositionId.StartsWith(
                "runtime-ai-lab.published-card.",
                StringComparison.Ordinal);

        public string PrimaryBehaviorTestCardDraftId(
            IReadOnlyDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards)
        {
            if (!IsBehaviorTestComposition || cards == null)
                return string.Empty;
            string stableCardId = Profile.CompositionId.Substring(
                "runtime-ai-lab.published-card.".Length);
            for (int i = 0; i < Entries.Count; i++)
            {
                if (cards.TryGetValue(
                        Entries[i].CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    && string.Equals(
                        card.PublishedStableId,
                        stableCardId,
                        StringComparison.Ordinal))
                {
                    return Entries[i].CardDraftId;
                }
            }
            return string.Empty;
        }

        public static CharacterTestRuntimeAiLabBehaviorGroupDraft Import(
            CharacterTestArenaBehaviorGroupDraft source,
            IDictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft> cards,
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog groupCatalog = null,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog = null,
            string sourceProjectPath = null,
            string sourceCatalogFileHash = null)
        {
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision publishedGroup = null;
            if (source != null && groupCatalog != null)
            {
                groupCatalog.TryGetExact(
                    source.StableId,
                    source.SourceProjectRevision,
                    out publishedGroup);
            }
            var group = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                null,
                source?.StableId,
                source?.DisplayName,
                source?.Profile,
                source?.Revision ?? 1,
                source?.SourceProjectRevision ?? 0,
                source?.SourceProjectContentHash ?? 0,
                sourceProjectPath,
                sourceCatalogFileHash);
            group.Profile.Entries.Clear();
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries = source?.Profile?.Entries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry = entries[i].Clone();
                CharacterTestRuntimeAiPlannerBehaviorGroupCardReference fixedReference =
                    FindFixedReference(publishedGroup, runtimeEntry.EntryId);
                if (fixedReference == null
                    || cardCatalog == null
                    || (!cardCatalog.TryResolveLatestCompatible(
                            fixedReference.StableCardId,
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
                        && !cardCatalog.TryGetExact(
                            fixedReference.StableCardId,
                            fixedReference.CardRevision,
                            out publishedCard)))
                {
                    // Unpinned legacy/core entries are runtime implementation details, not authoring cards.
                    // Starting a fresh project must never synthesize compatibility card drafts for them.
                    continue;
                }
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    CharacterTestRuntimeAiLabDraftWorkspace.GetOrCreateSharedPublishedCard(
                        cards,
                        publishedCard);
                if (!cards.ContainsKey(card.CardDraftId))
                    cards.Add(card.CardDraftId, card);
                runtimeEntry.SetResponsibilities(card.ResponsibilityKinds);
                group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                    runtimeEntry.EntryId,
                    card.CardDraftId,
                    runtimeEntry.BehaviorId,
                    runtimeEntry.Order,
                    runtimeEntry.Disabled,
                    runtimeEntry));
            }
            group.NormalizeOrders();
            return group;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCardReference FindFixedReference(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            string entryId)
        {
            for (int i = 0; group != null && i < group.Cards.Count; i++)
            {
                if (string.Equals(group.Cards[i].EntryId, entryId, StringComparison.Ordinal))
                    return group.Cards[i];
            }
            return null;
        }

        public CharacterTestRuntimeAiLabBehaviorGroupDraft Clone(bool newIdentity)
        {
            var clone = new CharacterTestRuntimeAiLabBehaviorGroupDraft(
                newIdentity ? null : GroupDraftId,
                PublishedStableId,
                DisplayName + (newIdentity ? " Copy" : string.Empty),
                Profile,
                CurrentRevision,
                SourceProjectRevision,
                SourceProjectContentHash,
                SourceProjectPath,
                SourceCatalogFileHash);
            clone.Profile.Entries.Clear();
            for (int i = 0; i < Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry =
                    Entries[i].Clone(newEntryIdentity: newIdentity);
                clone.Entries.Add(entry);
                clone.Profile.Entries.Add(entry.RuntimeEntry);
            }
            return clone;
        }

        public void NormalizeOrders()
        {
            Entries.Sort((left, right) => (left?.Order ?? 0).CompareTo(right?.Order ?? 0));
            Profile.Entries.Clear();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].Order = (i + 1) * 10;
                Entries[i].RuntimeEntry.Order = Entries[i].Order;
                Entries[i].RuntimeEntry.Disabled = Entries[i].Disabled;
                Profile.Entries.Add(Entries[i].RuntimeEntry);
            }
        }

        private static int CompareEntries(
            CharacterTestRuntimeAiLabBehaviorEntryDraft left,
            CharacterTestRuntimeAiLabBehaviorEntryDraft right)
        {
            int leftSlot = CoreSlotSortIndex(
                left?.CoreSlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible);
            int rightSlot = CoreSlotSortIndex(
                right?.CoreSlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible);
            int slotComparison = leftSlot.CompareTo(rightSlot);
            return slotComparison != 0
                ? slotComparison
                : (left?.Order ?? 0).CompareTo(right?.Order ?? 0);
        }

        private static int CoreSlotSortIndex(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting:
                    return 0;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    return 1;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    return 2;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback:
                    return 3;
                default:
                    return 4;
            }
        }
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorEntryDraft
    {
        public CharacterTestRuntimeAiLabBehaviorEntryDraft(
            string entryId,
            string cardDraftId,
            string behaviorId,
            int order,
            bool disabled,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry)
        {
            EntryId = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(entryId);
            CardDraftId = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(cardDraftId);
            BehaviorId = behaviorId ?? string.Empty;
            Order = order;
            Disabled = disabled;
            RuntimeEntry = runtimeEntry?.Clone()
                ?? new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(BehaviorId, !disabled, order);
            RuntimeEntry.EntryId = EntryId;
        }

        public string EntryId { get; }
        public string CardDraftId { get; set; }
        public string BehaviorId { get; set; }
        public int Order { get; set; }
        public bool Disabled { get; set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> ResponsibilityKinds =>
            RuntimeEntry.ResponsibilityKinds;

        [Obsolete("Use ResponsibilityKinds and SetResponsibilities for multi-responsibility authoring.")]
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind ResponsibilityKind
        {
            get => RuntimeEntry.ResponsibilityKinds[0];
            set => RuntimeEntry.SetResponsibilities(new[] { value });
        }

        public bool HasResponsibility(
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility) =>
            RuntimeEntry.HasResponsibility(responsibility);

        public void SetResponsibilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities) =>
            RuntimeEntry.SetResponsibilities(responsibilities);

        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind CoreSlotKind
        {
            get => CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToLegacySlot(ResponsibilityKinds[0]);
            set => SetResponsibilities(new[] { CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(value) });
        }
        public CharacterTestRuntimeAiPlannerBehaviorModuleEntry RuntimeEntry { get; }

        public CharacterTestRuntimeAiLabBehaviorEntryDraft Clone(bool newEntryIdentity)
        {
            var runtime = RuntimeEntry.Clone();
            if (newEntryIdentity) runtime.NumericId = 0;
            return new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                newEntryIdentity ? null : EntryId,
                CardDraftId,
                BehaviorId,
                Order,
                Disabled,
                runtime);
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardDraft
    {
        public const string Schema = "mx.character-test.runtime-ai-lab.behavior-card.v3";
        public const string LegacyAtomicPayloadSchema = "mx.character-test.runtime-ai-lab.behavior-card.atomic.v1";

        public CharacterTestRuntimeAiLabBehaviorCardDraft(
            string cardDraftId,
            string publishedStableId,
            string displayName,
            string category,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            int currentRevision = 1)
        {
            CardDraftId = CharacterTestRuntimeAiLabDraftIds.NormalizeOrCreate(cardDraftId);
            PublishedStableId = publishedStableId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? PublishedStableId : displayName;
            Category = category ?? string.Empty;
            Payload = payload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardPayload();
            LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(Payload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Migration.SynchronizeForRead(LogicPayload);
            LogicPayloadMigratedFromV1 = true;
            CurrentRevision = Math.Max(1, currentRevision);
        }

        public string CardDraftId { get; }
        public string PublishedStableId { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string IconKey { get; internal set; } = string.Empty;
        public int Cost { get; internal set; }
        public float CooldownSeconds { get; internal set; }
        public string ShortDescription { get; internal set; } = string.Empty;
        public string UseSummary { get; internal set; } = string.Empty;
        public string RiskSummary { get; internal set; } = string.Empty;
        public string TagsText { get; internal set; } = string.Empty;
        public CharacterTestRuntimeAiPlannerBehaviorCapability[] RequiredCapabilities { get; internal set; } =
            Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
        public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; internal set; } =
            { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
        private CharacterTestRuntimeAiLabBehaviorCardPayload _payload;
        public CharacterTestRuntimeAiLabBehaviorCardPayload Payload
        {
            get => _payload;
            internal set
            {
                if (HasAuthoredSource) throw new InvalidOperationException("Tactical source cannot be overwritten by a legacy payload.");
                _payload = value;
            }
        }
        private CharacterTestRuntimeAiLabBehaviorCardLogicPayload _logicPayload;
        public CharacterTestRuntimeAiLabBehaviorCardLogicPayload LogicPayload
        {
            get => _logicPayload;
            internal set
            {
                if (HasAuthoredSource) throw new InvalidOperationException("Tactical source cannot be overwritten by a Recipe projection.");
                _logicPayload = value;
            }
        }
        public bool LogicPayloadMigratedFromV1 { get; internal set; }
        public int CurrentRevision { get; internal set; }
        public int LastCompiledRevision { get; internal set; }
        public string LastCompileStatus { get; internal set; } = "NotCompiled";
        public int SourcePublishedRevision { get; internal set; }
        public long SourcePublishedSemanticHash { get; internal set; }
        public long SourcePublishedContentHash { get; internal set; }
        public bool HasPublishedSource => PublishedStableId.Length > 0 && SourcePublishedRevision > 0;

        /// <remarks>LowFreqAlloc: explicit Published-to-Draft copy-on-write boundary.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardDraft CreatePublishedCopy(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            if (publishedCard == null)
                throw new ArgumentNullException(nameof(publishedCard));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata = publishedCard.Metadata;
            string category = metadata != null && metadata.Tags.Count > 0
                ? metadata.Tags[0]
                : "Published";
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                publishedCard.StableCardId,
                metadata?.DisplayName,
                category,
                new CharacterTestRuntimeAiLabBehaviorCardPayload())
            {
                LogicPayload = publishedCard.CloneLogic()
                    ?? new CharacterTestRuntimeAiLabBehaviorCardLogicPayload(),
                LogicPayloadMigratedFromV1 = false,
                LastCompileStatus = "PublishedCopy",
                SourcePublishedRevision = publishedCard.Revision,
                SourcePublishedSemanticHash = publishedCard.SemanticHash,
                SourcePublishedContentHash = publishedCard.ContentHash,
                IconKey = metadata?.IconKey ?? string.Empty,
                Cost = metadata?.Cost ?? 0, CooldownSeconds = metadata?.CooldownSeconds ?? 0f,
                ShortDescription = metadata?.ShortDescription ?? string.Empty,
                UseSummary = metadata?.UseSummary ?? string.Empty,
                RiskSummary = metadata?.RiskSummary ?? string.Empty,
                TagsText = metadata != null ? string.Join(", ", metadata.Tags) : string.Empty,
                RequiredCapabilities = metadata != null
                    ? CopyCapabilities(metadata.RequiredCapabilities)
                    : Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>(),
                ResponsibilityKinds = metadata != null
                    ? CopyResponsibilities(metadata.Responsibilities)
                    : new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other }
            };
            if (publishedCard.TacticalSource != null)
            {
                if (publishedCard.TacticalSource.IsLegacy) draft.SetTacticalSource(publishedCard.TacticalSource.CloneLegacy());
                else draft.SetCompositionSource(publishedCard.TacticalSource.CloneComposition());
            }
            return draft;
        }

        public bool MatchesPublishedSource(CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard)
        {
            return publishedCard != null
                && SourcePublishedRevision == publishedCard.Revision
                && SourcePublishedSemanticHash == publishedCard.SemanticHash
                && SourcePublishedContentHash == publishedCard.ContentHash
                && string.Equals(PublishedStableId, publishedCard.StableCardId, StringComparison.Ordinal);
        }

        public static CharacterTestRuntimeAiLabBehaviorCardDraft Import(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            string behaviorId = entry?.BehaviorId ?? string.Empty;
            string displayName = behaviorId;
            string category = string.Empty;
            CharacterTestRuntimeAiPlannerActionKind actionKind = CharacterTestRuntimeAiPlannerActionKind.Wait;
            if (CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    behaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                displayName = definition.DisplayName;
                category = definition.Category.ToString();
                actionKind = ToLegacyActionKind(definition.ActionPrimitive);
            }
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                null,
                behaviorId,
                displayName,
                category,
                CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(behaviorId, actionKind));
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForRead(draft.LogicPayload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Templates.ConfigureGeneratedV7FixedIntervals(
                draft.LogicPayload,
                behaviorId);
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForRead(draft.LogicPayload);
            return draft;
        }

        public CharacterTestRuntimeAiLabBehaviorCardDraft Clone(bool newIdentity)
        {
            CharacterTestRuntimeAiLabBehaviorCardDraft clone = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                newIdentity ? null : CardDraftId,
                PublishedStableId,
                DisplayName + (newIdentity ? " Copy" : string.Empty),
                Category,
                Payload,
                CurrentRevision)
            {
                LastCompiledRevision = LastCompiledRevision,
                LastCompileStatus = LastCompileStatus,
                IconKey = IconKey,
                Cost = Cost, CooldownSeconds = CooldownSeconds,
                ShortDescription = ShortDescription,
                UseSummary = UseSummary,
                RiskSummary = RiskSummary,
                TagsText = TagsText,
                RequiredCapabilities = CopyCapabilities(RequiredCapabilities),
                ResponsibilityKinds = CopyResponsibilities(ResponsibilityKinds)
            };
            if (HasCompositionSource) clone.SetCompositionSource(CloneCompositionSource());
            else if (HasTacticalSource) clone.SetTacticalSource(CloneTacticalSource());
            else clone.LogicPayload = LogicPayload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardLogicPayload();
            clone.LogicPayloadMigratedFromV1 = LogicPayloadMigratedFromV1;
            clone.SourcePublishedRevision = SourcePublishedRevision;
            clone.SourcePublishedSemanticHash = SourcePublishedSemanticHash;
            clone.SourcePublishedContentHash = SourcePublishedContentHash;
            return clone;
        }

        internal void CopyDefinitionFrom(CharacterTestRuntimeAiLabBehaviorCardDraft source)
        {
            if (ReferenceEquals(this, source)) return;
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!string.Equals(PublishedStableId, source.PublishedStableId, StringComparison.Ordinal))
                throw new InvalidOperationException("Shared behavior-card identities do not match.");

            DisplayName = source.DisplayName;
            Category = source.Category;
            IconKey = source.IconKey;
            Cost = source.Cost; CooldownSeconds = source.CooldownSeconds;
            ShortDescription = source.ShortDescription;
            UseSummary = source.UseSummary;
            RiskSummary = source.RiskSummary;
            TagsText = source.TagsText;
            RequiredCapabilities = CopyCapabilities(source.RequiredCapabilities);
            ResponsibilityKinds = CopyResponsibilities(source.ResponsibilityKinds);
            TacticalSourceJson = null;
            CompositionSourceJson = null;
            Payload = source.Payload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardPayload();
            if (source.HasCompositionSource) SetCompositionSource(source.CloneCompositionSource());
            else if (source.HasTacticalSource) SetTacticalSource(source.CloneTacticalSource());
            else LogicPayload = source.LogicPayload?.Clone() ?? new CharacterTestRuntimeAiLabBehaviorCardLogicPayload();
            LogicPayloadMigratedFromV1 = source.LogicPayloadMigratedFromV1;
            CurrentRevision = source.CurrentRevision;
            LastCompiledRevision = source.LastCompiledRevision;
            LastCompileStatus = source.LastCompileStatus;
            SourcePublishedRevision = source.SourcePublishedRevision;
            SourcePublishedSemanticHash = source.SourcePublishedSemanticHash;
            SourcePublishedContentHash = source.SourcePublishedContentHash;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCapability[] CopyCapabilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            var copy = new CharacterTestRuntimeAiPlannerBehaviorCapability[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return copy;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
        {
            if (source == null || source.Count == 0)
                return new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other };
            var copy = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[source.Count];
            for (int i = 0; i < source.Count; i++)
                copy[i] = source[i];
            return CharacterTestRuntimeAiPlannerBehaviorResponsibilityPolicy.Normalize(copy);
        }

        private static CharacterTestRuntimeAiPlannerActionKind ToLegacyActionKind(
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive primitive)
        {
            switch (primitive)
            {
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.MoveToTarget:
                    return CharacterTestRuntimeAiPlannerActionKind.MoveToTarget;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Strafe:
                    return CharacterTestRuntimeAiPlannerActionKind.Strafe;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Retreat:
                    return CharacterTestRuntimeAiPlannerActionKind.Retreat;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.PrimaryAttack:
                    return CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.CastAbilitySlot:
                    return CharacterTestRuntimeAiPlannerActionKind.CastAbilitySlot;
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Dodge:
                    return CharacterTestRuntimeAiPlannerActionKind.Dodge;
                default:
                    return CharacterTestRuntimeAiPlannerActionKind.Wait;
            }
        }
    }

    internal sealed class CharacterTestRuntimeAiLabDraftSlot
    {
        public CharacterTestRuntimeAiLabDraftSlot(
            int teamId,
            int slotIndex,
            bool enabled,
            int characterId,
            string groupDraftId,
            int arenaBehaviorGroupDraftId = 0)
        {
            TeamId = teamId;
            SlotIndex = slotIndex;
            Enabled = enabled;
            CharacterId = characterId;
            GroupDraftId = groupDraftId ?? string.Empty;
            ArenaBehaviorGroupDraftId = arenaBehaviorGroupDraftId;
        }

        public int TeamId { get; }
        public int SlotIndex { get; }
        public bool Enabled { get; set; }
        public int CharacterId { get; set; }
        public string GroupDraftId { get; set; }
        public int ArenaBehaviorGroupDraftId { get; set; }
        public float SpawnX { get; set; }
        public float SpawnZ { get; set; }

        public CharacterTestRuntimeAiLabDraftSlot Clone()
        {
            return new CharacterTestRuntimeAiLabDraftSlot(
                TeamId,
                SlotIndex,
                Enabled,
                CharacterId,
                GroupDraftId,
                ArenaBehaviorGroupDraftId)
            {
                SpawnX = SpawnX,
                SpawnZ = SpawnZ
            };
        }
    }

    internal static class CharacterTestRuntimeAiLabDraftIds
    {
        public static string NormalizeOrCreate(string value)
        {
            return Guid.TryParse(value, out Guid parsed)
                ? parsed.ToString("D", CultureInfo.InvariantCulture)
                : Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        }
    }
}
