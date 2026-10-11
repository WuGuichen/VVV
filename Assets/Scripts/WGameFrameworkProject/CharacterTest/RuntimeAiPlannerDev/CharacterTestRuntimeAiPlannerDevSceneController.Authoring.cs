using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabAuthoringMode _authoringMode =
            CharacterTestRuntimeAiLabAuthoringMode.Developer;
        private CharacterTestRuntimeAiLabCandidateState _candidateState;
        private CharacterTestRuntimeAiLabCandidateRevision _candidateRevision;
        private CharacterTestRuntimeAiLabCandidateRevision _activeRevision;
        private string _candidateDiagnostic = "尚未生成候选版本。";
        private bool _candidateApplyQueued;
        private int _candidateApplyFrame;
        private readonly List<CharacterTestRuntimeAiLabAppliedRevision> _appliedRevisionEvents =
            new List<CharacterTestRuntimeAiLabAppliedRevision>(8);
        private CharacterTestRuntimeAiLabAppliedRevision _baselineRevision;
        private readonly List<CharacterTestRuntimeAiLabAppliedRevision> _baselineRevisionPeers =
            new List<CharacterTestRuntimeAiLabAppliedRevision>(5);
        private bool _publishedCardAuthoringSlotOverrideActive;
        private int _publishedCardAuthoringTeamId;
        private int _publishedCardAuthoringSlotIndex = -1;
        private string _publishedCardAuthoringPreviousGroupDraftId = string.Empty;
        private string _publishedCardAuthoringGroupDraftId = string.Empty;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView _projectBehaviorCardLibraryView;
        private ProjectBehaviorCardLibraryViewCache _projectBehaviorCardLibraryViewCache;

        internal CharacterTestRuntimeAiLabAuthoringMode AuthoringMode => _authoringMode;
        internal CharacterTestRuntimeAiLabCandidateState CandidateState => _candidateState;
        internal CharacterTestRuntimeAiLabCandidateRevision CandidateRevision => _candidateRevision;
        internal CharacterTestRuntimeAiLabCandidateRevision ActiveRevision => _activeRevision;
        internal int AppliedRevisionEventCount => _appliedRevisionEvents.Count;
        internal int LastAppliedRevisionFrame => _appliedRevisionEvents.Count > 0
            ? _appliedRevisionEvents[_appliedRevisionEvents.Count - 1].ApplicationFrame
            : -1;
        internal string CandidateDiagnostic => _candidateDiagnostic;
        internal bool CanEditBehaviorCardDraft => !IsHistoricalFrameSelected
            && (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Setup
            || (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && _context != null
                && !_seekInProgress
                && !_loopPlaybackActive
                && !_candidateApplyQueued));
        internal bool CanApplyCandidate => _candidateRevision != null
            && _candidateState == CharacterTestRuntimeAiLabCandidateState.PreviewComplete
            && !_candidateApplyQueued;

        internal bool IsProjectBehaviorCardLibraryActive
        {
            get
            {
                return TryGetAvailableDraftWorkspace(out CharacterTestRuntimeAiLabDraftWorkspace workspace, out _)
                    && workspace.TryGetGroupForSlot(
                        _selectedSetupTeamId,
                        _selectedSetupSlotIndex,
                        out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                    && group.HasProjectSource;
            }
        }

        internal bool HasProjectCatalogWriteConflict =>
            _projectCatalogAuthoring?.LastWriteConflict == true;

        /// <remarks>NoAlloc on a stable projection; allocates only when its semantic dependencies change.</remarks>
        internal CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView CreateProjectBehaviorCardLibraryView()
        {
            if (!IsProjectBehaviorCardLibraryActive
                || ProjectCatalogState?.Success != true
                || !DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                ClearProjectBehaviorCardLibraryViewCache();
                return null;
            }

            CharacterTestRuntimeAiPlannerProjectCatalogState projectCatalog = ProjectCatalogState;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (_projectBehaviorCardLibraryViewCache != null
                && _projectBehaviorCardLibraryViewCache.Matches(
                projectCatalog,
                workspace,
                group,
                _selectedSetupTeamId,
                _selectedSetupSlotIndex))
            {
                return _projectBehaviorCardLibraryView;
            }

            var stableIds = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < projectCatalog.CardCatalog.Cards.Count; i++)
                stableIds.Add(projectCatalog.CardCatalog.Cards[i].StableCardId);
            var cards = new CharacterTestRuntimeAiPlannerPlayerDeckCardView[stableIds.Count];
            int index = 0;
            int totalCost = 0;
            foreach (string stableCardId in stableIds)
            {
                bool available = projectCatalog.CardCatalog.TryResolveLatestCompatible(
                    stableCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card);
                bool installed = false;
                int pinnedRevision = 0;
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    if (workspace.Cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
                        && string.Equals(draft.PublishedStableId, stableCardId, StringComparison.Ordinal))
                    {
                        installed = true;
                        pinnedRevision = draft.SourcePublishedRevision;
                        if (!entry.Disabled
                            && projectCatalog.CardCatalog.TryGetExact(
                                stableCardId,
                                pinnedRevision,
                                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard pinnedCard)
                            && pinnedCard.Metadata != null)
                        {
                            totalCost += pinnedCard.Metadata.Cost;
                        }
                        break;
                    }
                }
                CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability availability = available
                    ? CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                    : CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible;
                cards[index++] = new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                    stableCardId,
                    card,
                    availability,
                    isUnlocked: true,
                    isInstalled: installed,
                    canInstall: available && !installed,
                    canRemove: installed,
                    canReplace: installed,
                    canUseAsReplacement: available,
                    blockReason: available
                        ? CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.None
                        : CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible,
                    pinnedRevision: pinnedRevision);
            }
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view =
                new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                "project-json",
                group.Entries.Count,
                totalCost,
                isKnownCostComplete: true,
                projectCatalog.CardCatalog.CatalogHash,
                isResolved: true,
                cards,
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                isSessionLocked: !projectCatalog.CanWriteProjectCatalogs);
            _projectBehaviorCardLibraryView = view;
            _projectBehaviorCardLibraryViewCache =
                ProjectBehaviorCardLibraryViewCache.Capture(
                    projectCatalog,
                    workspace,
                    group,
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex);
            return view;
        }

        private void ClearProjectBehaviorCardLibraryViewCache()
        {
            _projectBehaviorCardLibraryView = null;
            _projectBehaviorCardLibraryViewCache = null;
        }

        private sealed class ProjectBehaviorCardLibraryViewCache
        {
            private readonly CharacterTestRuntimeAiPlannerProjectCatalogState _projectCatalog;
            private readonly CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog _cardCatalog;
            private readonly CharacterTestRuntimeAiPlannerBehaviorGroupCatalog _groupCatalog;
            private readonly CharacterTestRuntimeAiLabDraftWorkspace _workspace;
            private readonly CharacterTestRuntimeAiLabBehaviorGroupDraft _group;
            private readonly int _teamId;
            private readonly int _slotIndex;
            private readonly int _workspaceRevision;
            private readonly int _workspaceSavedRevision;
            private readonly bool _workspaceReadOnly;
            private readonly bool _canWrite;
            private readonly int _groupRevision;
            private readonly int _groupSourceRevision;
            private readonly long _groupSourceContentHash;
            private readonly string _groupSourcePath;
            private readonly string _groupSourceCatalogFileHash;
            private readonly CharacterTestRuntimeAiLabBehaviorEntryDraft[] _entries;
            private readonly bool[] _disabled;
            private readonly string[] _cardDraftIds;
            private readonly CharacterTestRuntimeAiLabBehaviorCardDraft[] _cards;
            private readonly bool[] _cardPresent;
            private readonly int[] _publishedRevisions;
            private readonly string[] _publishedStableIds;

            private ProjectBehaviorCardLibraryViewCache(
                CharacterTestRuntimeAiPlannerProjectCatalogState projectCatalog,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                int teamId,
                int slotIndex)
            {
                _projectCatalog = projectCatalog;
                _cardCatalog = projectCatalog.CardCatalog;
                _groupCatalog = projectCatalog.GroupCatalog;
                _workspace = workspace;
                _group = group;
                _teamId = teamId;
                _slotIndex = slotIndex;
                _workspaceRevision = workspace.CurrentRevision;
                _workspaceSavedRevision = workspace.LastSavedRevision;
                _workspaceReadOnly = workspace.IsReadOnly;
                _canWrite = projectCatalog.CanWriteProjectCatalogs;
                _groupRevision = group.CurrentRevision;
                _groupSourceRevision = group.SourceProjectRevision;
                _groupSourceContentHash = group.SourceProjectContentHash;
                _groupSourcePath = group.SourceProjectPath;
                _groupSourceCatalogFileHash = group.SourceCatalogFileHash;
                int count = group.Entries.Count;
                _entries = new CharacterTestRuntimeAiLabBehaviorEntryDraft[count];
                _disabled = new bool[count];
                _cardDraftIds = new string[count];
                _cards = new CharacterTestRuntimeAiLabBehaviorCardDraft[count];
                _cardPresent = new bool[count];
                _publishedRevisions = new int[count];
                _publishedStableIds = new string[count];
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                    _entries[i] = entry;
                    if (entry == null)
                        continue;
                    _disabled[i] = entry.Disabled;
                    _cardDraftIds[i] = entry.CardDraftId;
                    if (workspace.Cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                    {
                        _cardPresent[i] = true;
                        _cards[i] = card;
                        _publishedRevisions[i] = card.SourcePublishedRevision;
                        _publishedStableIds[i] = card.PublishedStableId;
                    }
                }
            }

            internal static ProjectBehaviorCardLibraryViewCache Capture(
                CharacterTestRuntimeAiPlannerProjectCatalogState projectCatalog,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                int teamId,
                int slotIndex)
            {
                return new ProjectBehaviorCardLibraryViewCache(
                    projectCatalog,
                    workspace,
                    group,
                    teamId,
                    slotIndex);
            }

            internal bool Matches(
                CharacterTestRuntimeAiPlannerProjectCatalogState projectCatalog,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                int teamId,
                int slotIndex)
            {
                if (!ReferenceEquals(_projectCatalog, projectCatalog)
                    || !ReferenceEquals(_cardCatalog, projectCatalog.CardCatalog)
                    || !ReferenceEquals(_groupCatalog, projectCatalog.GroupCatalog)
                    || !ReferenceEquals(_workspace, workspace)
                    || !ReferenceEquals(_group, group)
                    || _teamId != teamId
                    || _slotIndex != slotIndex
                    || _workspaceRevision != workspace.CurrentRevision
                    || _workspaceSavedRevision != workspace.LastSavedRevision
                    || _workspaceReadOnly != workspace.IsReadOnly
                    || _canWrite != projectCatalog.CanWriteProjectCatalogs
                    || _groupRevision != group.CurrentRevision
                    || _groupSourceRevision != group.SourceProjectRevision
                    || _groupSourceContentHash != group.SourceProjectContentHash
                    || !string.Equals(_groupSourcePath, group.SourceProjectPath, StringComparison.Ordinal)
                    || !string.Equals(
                        _groupSourceCatalogFileHash,
                        group.SourceCatalogFileHash,
                        StringComparison.Ordinal)
                    || _entries.Length != group.Entries.Count)
                {
                    return false;
                }
                for (int i = 0; i < _entries.Length; i++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                    if (!ReferenceEquals(_entries[i], entry)
                        || entry == null
                        || _disabled[i] != entry.Disabled
                        || !string.Equals(_cardDraftIds[i], entry.CardDraftId, StringComparison.Ordinal))
                    {
                        if (entry == null && _entries[i] == null)
                            continue;
                        return false;
                    }
                    bool cardPresent = workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card);
                    if (cardPresent != _cardPresent[i]
                        || (cardPresent
                            && (!ReferenceEquals(_cards[i], card)
                                || _publishedRevisions[i] != card.SourcePublishedRevision
                                || !string.Equals(
                                    _publishedStableIds[i],
                                    card.PublishedStableId,
                                    StringComparison.Ordinal))))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>Adds the next available shared project-card reference as a companion test behavior.</summary>
        /// <remarks>LowFreqAlloc: explicit test-composition authoring command.</remarks>
        internal bool TryAddNextProjectCardToTestComposition(
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (ProjectCatalogState?.Success != true
                || !DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.IsBehaviorTestComposition)
            {
                message = "当前没有可编辑的行为测试组合。";
                return false;
            }

            var stableIds = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ProjectCatalogState.CardCatalog.Cards.Count; i++)
                stableIds.Add(ProjectCatalogState.CardCatalog.Cards[i].StableCardId);
            foreach (string stableCardId in stableIds)
            {
                if (!ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                        stableCardId,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
                            .CurrentRuntimeContractVersion,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
                {
                    continue;
                }
                if (DraftWorkspace.TryAddPublishedCardToTestComposition(
                        _selectedSetupTeamId,
                        _selectedSetupSlotIndex,
                        card,
                        out entryIndex,
                        out message))
                {
                    _draftPersistence.MarkDirty();
                    BuildCandidateRevision();
                    _statusMessage = message;
                    return true;
                }
            }

            message = "项目行为卡库中没有可继续添加的陪测行为。";
            return false;
        }

        /// <summary>Adds the selected project-card revision to the current project behavior group.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab behavior-group authoring command.</remarks>
        internal bool TryAddProjectCardToCurrentBehaviorGroup(
            string stableCardId,
            out int entryIndex,
            out string message)
        {
            if (stableCardId?.StartsWith("draft:", StringComparison.Ordinal) == true)
                return TryAddTacticalDraftToCurrentBehaviorGroup(stableCardId.Substring(6), null, out entryIndex, out message);
            entryIndex = -1;
            if (string.IsNullOrWhiteSpace(stableCardId)
                || ProjectCatalogState?.Success != true
                || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                    stableCardId.Trim(),
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
            {
                message = "项目行为卡的最新兼容版本不可用。";
                return false;
            }

            bool changed = DraftWorkspace.TryAddPublishedCardToBehaviorGroup(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                card,
                out entryIndex,
                out message);
            if (!changed)
                return false;

            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        internal bool TryAddProjectCardToCurrentBehaviorGroup(
            string stableCardId,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            out int entryIndex,
            out string message) =>
            TryAddProjectCardToCurrentBehaviorGroup(
                stableCardId,
                new[] { responsibilityKind },
                out entryIndex,
                out message);

        internal bool TryAddProjectCardToCurrentBehaviorGroup(
            string stableCardId,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out int entryIndex,
            out string message)
        {
            if (stableCardId?.StartsWith("draft:", StringComparison.Ordinal) == true)
                return TryAddTacticalDraftToCurrentBehaviorGroup(stableCardId.Substring(6), responsibilities, out entryIndex, out message);
            entryIndex = -1;
            if (string.IsNullOrWhiteSpace(stableCardId)
                || ProjectCatalogState?.Success != true
                || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                    stableCardId.Trim(),
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card))
            {
                message = "项目行为卡的最新兼容版本不可用。";
                return false;
            }

            bool changed = DraftWorkspace.TryAddPublishedCardToBehaviorGroup(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                card,
                responsibilities,
                out entryIndex,
                out message);
            if (!changed)
                return false;

            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        internal bool TryCreateProjectCardInCurrentBehaviorGroup(
            out int entryIndex,
            out string stableCardId,
            out string message)
        {
            entryIndex = -1;
            stableCardId = string.Empty;
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                message = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。";
                return false;
            }
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !group.HasProjectSource
                || group.IsBehaviorTestComposition)
            {
                message = "当前没有可编辑的项目行为组。";
                return false;
            }
            if (group.Entries.Count >= CharacterTestRuntimeAiLabDraftWorkspace.MaximumBehaviorGroupEntries)
            {
                message = "行为组已达到 16 张卡的上限。";
                return false;
            }

            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiLabPublishResult creation =
                _projectCatalogAuthoring.TryCreateCard(_behaviorCardReferences);
            if (!creation.Success)
            {
                message = creation.Diagnostics.Count > 0
                    ? creation.Diagnostics[0].Message
                    : "新建行为卡失败。";
                return false;
            }

            _matchConfig.UpdateProjectCatalogState(_projectCatalogAuthoring.State);
            stableCardId = creation.PublishedCard.StableCardId;
            if (!DraftWorkspace.TryAddPublishedCardToBehaviorGroup(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    creation.PublishedCard,
                    out entryIndex,
                    out message))
            {
                return false;
            }

            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            message = "已创建“" + creation.PublishedCard.Metadata.DisplayName
                + "”并加入当前行为组。";
            _statusMessage = message;
            return true;
        }

        internal bool TryDeleteProjectCardFromCurrentBehaviorGroup(
            string stableCardId,
            out int deletedEntryIndex,
            out string message)
        {
            deletedEntryIndex = -1;
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                message = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。";
                return false;
            }
            if (DraftWorkspace.IsReadOnly
                || string.IsNullOrWhiteSpace(stableCardId))
            {
                message = "当前没有可删除的项目行为卡。";
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            if (DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                && group != null)
            {
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    if (!DraftWorkspace.Cards.TryGetValue(
                            group.Entries[entryIndex].CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft candidate)
                        || !string.Equals(
                            candidate.PublishedStableId,
                            stableCardId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    card = candidate;
                    deletedEntryIndex = entryIndex;
                    break;
                }
            }

            if (card == null)
            {
                foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorCardDraft> pair in DraftWorkspace.Cards)
                {
                    if (string.Equals(
                            pair.Value?.PublishedStableId,
                            stableCardId,
                            StringComparison.Ordinal))
                    {
                        card = pair.Value;
                        break;
                    }
                }
            }

            string displayName = card != null && !string.IsNullOrWhiteSpace(card.DisplayName)
                ? card.DisplayName
                : stableCardId;

            if (string.Equals(displayName, stableCardId, StringComparison.Ordinal)
                && _matchConfig?.ProjectCatalogState?.CardCatalog?.Cards != null)
            {
                for (int i = 0; i < _matchConfig.ProjectCatalogState.CardCatalog.Cards.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCard catalogCard =
                        _matchConfig.ProjectCatalogState.CardCatalog.Cards[i];
                    if (string.Equals(catalogCard.StableCardId, stableCardId, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(catalogCard.Metadata?.DisplayName))
                    {
                        displayName = catalogCard.Metadata.DisplayName;
                        break;
                    }
                }
            }

            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiPlannerProjectCardDeleteResult deletion =
                _projectCatalogAuthoring.TryDeleteCard(
                    stableCardId,
                    _behaviorCardReferences);
            if (!deletion.Success)
            {
                message = deletion.Diagnostic.Length > 0
                    ? deletion.Diagnostic
                    : "删除项目行为卡失败。";
                return false;
            }

            _matchConfig.UpdateProjectCatalogState(deletion.State);
            if (!DraftWorkspace.TryRemovePublishedCardReferences(
                    stableCardId,
                    out int removedReferences,
                    out string cleanupMessage))
            {
                message = "行为卡已从项目目录删除，但当前实验引用清理失败：" + cleanupMessage;
                return false;
            }
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            message = "已删除项目行为卡“"
                + displayName
                + "”"
                + (removedReferences > 0
                    ? "并清理当前实验中的 " + removedReferences.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 处引用。"
                    : "。");
            _statusMessage = message;
            return true;
        }

        internal bool CanUndoProjectCardDeletion =>
            _projectCatalogAuthoring?.CanUndoLastCardDelete == true
            && string.Equals(
                DraftWorkspace.NextUndoLabel,
                "删除项目行为卡引用",
                StringComparison.Ordinal);

        internal bool TryUndoProjectCardDeletion(
            out int restoredEntryIndex,
            out string message)
        {
            restoredEntryIndex = -1;
            if (!CanUndoProjectCardDeletion)
            {
                message = "最近一次操作不是可恢复的项目行为卡删除。";
                return false;
            }

            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiPlannerProjectCardDeleteResult restored =
                _projectCatalogAuthoring.TryUndoLastCardDelete(_behaviorCardReferences);
            if (!restored.Success)
            {
                message = restored.Diagnostic.Length > 0
                    ? restored.Diagnostic
                    : "恢复项目行为卡失败。";
                return false;
            }
            _matchConfig.UpdateProjectCatalogState(restored.State);
            if (!DraftWorkspace.TryUndo(out string workspaceMessage))
            {
                message = "行为卡文件已经恢复，但实验草稿引用恢复失败：" + workspaceMessage;
                return false;
            }

            if (DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    if (DraftWorkspace.Cards.TryGetValue(
                            group.Entries[entryIndex].CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                        && string.Equals(
                            card.PublishedStableId,
                            restored.StableCardId,
                            StringComparison.Ordinal))
                    {
                        restoredEntryIndex = entryIndex;
                        break;
                    }
                }
            }
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            message = "已恢复行为卡及其项目文件。";
            _statusMessage = message;
            return true;
        }

        internal bool TryUpgradeCurrentGroupCardReference(string stableCardId, out string message)
        {
            if (ProjectCatalogState?.Success != true
                || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                    stableCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard latest))
            {
                message = "项目行为卡的最新兼容版本不可用。";
                return false;
            }
            bool changed = DraftWorkspace.TryUpgradeProjectCardReference(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                stableCardId,
                latest,
                out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        internal bool TrySetCurrentProjectCardMetadata(
            int entryIndex,
            CharacterTestRuntimeAiLabProjectCardMetadataField field,
            string value,
            out string message)
        {
            if (DraftWorkspace == null)
            {
                message = "Runtime AI Lab 草稿工作区不可用。";
                return false;
            }
            bool changed = DraftWorkspace.TrySetProjectCardMetadata(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    entryIndex,
                    field,
                    value,
                    out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            _statusMessage = message;
            return true;
        }

        internal bool TryGetStandaloneProjectCardDraft(
            string stableCardId,
            out CharacterTestRuntimeAiLabBehaviorCardDraft draft)
        {
            draft = null;
            if (string.IsNullOrWhiteSpace(stableCardId))
                return false;
            stableCardId = stableCardId.Trim();
            if (_standaloneProjectCardDrafts.TryGetValue(stableCardId, out draft))
                return true;
            return DraftWorkspace?.TryGetProjectCardDraft(stableCardId, out draft) == true
                && draft.CurrentRevision > 1;
        }

        internal bool TrySetStandaloneProjectCardDisplayName(
            string stableCardId,
            string value,
            out string message)
        {
            stableCardId = stableCardId?.Trim() ?? string.Empty;
            string normalized = value?.Trim() ?? string.Empty;
            if (stableCardId.Length == 0)
            {
                message = "未选择项目行为卡。";
                return false;
            }
            if (normalized.Length == 0)
            {
                message = "行为卡名称不能为空。";
                return false;
            }
            if (normalized.Length > 64)
            {
                message = "行为卡名称最长 64 个字符。";
                return false;
            }
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                message = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。";
                return false;
            }
            if (!_standaloneProjectCardDrafts.TryGetValue(stableCardId, out CharacterTestRuntimeAiLabBehaviorCardDraft draft))
            {
                if (DraftWorkspace?.TryGetProjectCardDraft(stableCardId, out draft) != true)
                {
                    if (ProjectCatalogState?.CardCatalog == null
                        || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                            stableCardId,
                            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published))
                    {
                        message = "找不到行为卡的最新项目版本。";
                        return false;
                    }
                    draft = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published);
                    DraftWorkspace.TrackStandaloneProjectCardDraft(draft);
                }
                _standaloneProjectCardDrafts.Add(stableCardId, draft);
            }
            if (string.Equals(draft.DisplayName, normalized, StringComparison.Ordinal))
            {
                message = "行为卡名称没有变化。";
                return true;
            }
            draft.DisplayName = normalized;
            draft.CurrentRevision++;
            DraftWorkspace.TrackStandaloneProjectCardDraft(draft);
            _draftPersistence.MarkDirty();
            _standaloneProjectCardDraftRevision++;
            message = "名称已写入共享行为卡草稿；点击保存后发布项目行为卡新版本。";
            _statusMessage = message;
            return true;
        }

        internal bool HasStandaloneProjectCardChanges(string stableCardId) =>
            TryGetStandaloneProjectCardDraft(stableCardId, out _);

        internal void DiscardStandaloneProjectCardChanges(string stableCardId)
        {
            if (string.IsNullOrWhiteSpace(stableCardId)
                || !_standaloneProjectCardDrafts.Remove(stableCardId.Trim()))
            {
                return;
            }
            _standaloneProjectCardDraftRevision++;
        }

        internal void DiscardAllStandaloneProjectCardChanges()
        {
            if (_standaloneProjectCardDrafts.Count == 0)
                return;
            _standaloneProjectCardDrafts.Clear();
            _standaloneProjectCardDraftRevision++;
        }

        internal bool TryPublishStandaloneProjectCard(string stableCardId, out string message)
        {
            stableCardId = stableCardId?.Trim() ?? string.Empty;
            if (!TryGetStandaloneProjectCardDraft(
                    stableCardId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft draft))
            {
                message = "当前行为卡没有需要保存的修改。";
                return false;
            }
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                message = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。";
                return false;
            }
            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiLabPublishResult result =
                _projectCatalogAuthoring.TryPublishCard(draft, _behaviorCardReferences);
            if (!result.Success)
            {
                message = result.Diagnostics.Count > 0
                    ? result.Diagnostics[0].Message
                    : "保存行为卡失败。";
                return false;
            }
            _matchConfig.UpdateProjectCatalogState(_projectCatalogAuthoring.State);
            DraftWorkspace.TryAdoptStandaloneProjectCardRevision(
                stableCardId,
                result.PublishedCard);
            _draftPersistence.MarkDirty();
            _standaloneProjectCardDrafts.Remove(stableCardId);
            _standaloneProjectCardDraftRevision++;
            message = "已保存“" + result.PublishedCard.Metadata.DisplayName + "”为 r"
                + result.PublishedCard.Revision.ToString(CultureInfo.InvariantCulture) + "。";
            _statusMessage = message;
            return true;
        }

        internal bool TryReloadCurrentProjectGroup(out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft current)
                || !current.HasProjectSource)
            {
                message = "当前没有可重新加载的项目行为组工作副本。";
                return false;
            }
            CharacterTestRuntimeAiPlannerProjectCatalogState latest =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!latest.Success
                || !latest.GroupCatalog.TryGetLatest(
                    current.PublishedStableId,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision latestGroup))
            {
                message = latest.Diagnostic.Length > 0
                    ? latest.Diagnostic
                    : "项目行为组已不存在。";
                return false;
            }
            _matchConfig.BehaviorGroups.TryAdoptProjectRevision(latestGroup);
            _matchConfig.UpdateProjectCatalogState(latest);
            _projectCatalogAuthoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(latest);
            ReplaceDraftWorkspaceFromMatchConfig();
            _draftWorkspace.RestoreSetupToArena(_matchConfig);
            BuildCandidateRevision();
            message = "已丢弃本地修改并重新加载项目行为组 r"
                + latestGroup.Revision.ToString(CultureInfo.InvariantCulture)
                + "。";
            _statusMessage = message;
            return true;
        }

        internal bool TryReapplyCurrentProjectGroupOnLatest(out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft current)
                || !current.HasProjectSource)
            {
                message = "当前没有可重新应用的项目行为组工作副本。";
                return false;
            }
            CharacterTestRuntimeAiPlannerProjectCatalogState latest =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!latest.Success
                || !latest.GroupCatalog.TryGetLatest(
                    current.PublishedStableId,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision latestGroup))
            {
                message = latest.Diagnostic.Length > 0
                    ? latest.Diagnostic
                    : "项目行为组已不存在。";
                return false;
            }
            if (!DraftWorkspace.TryRebaseProjectSources(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    latestGroup,
                    latest.CardCatalog,
                    latest.GroupCatalogPath,
                    latest.GroupFileHash,
                    out message))
            {
                return false;
            }
            _matchConfig.UpdateProjectCatalogState(latest);
            _projectCatalogAuthoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(latest);
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        /// <summary>
        /// Opens one immutable Published Behavior Card as an isolated Lab draft on the matching
        /// player-side setup slot. The Published card, Installed Deck and active session remain unchanged.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit authoring navigation only.</remarks>
        internal bool TryBeginPublishedCardAuthoring(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard,
            int characterId,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (!CanEditBehaviorCardDraft)
            {
                message = "请先返回配置阶段，再编辑 Published 行为卡。";
                return false;
            }
            if (publishedCard == null)
            {
                message = "Published 行为卡不可用。";
                return false;
            }
            RestorePublishedCardAuthoringSlotBinding();
            if (!TrySelectPlayerAuthoringSlot(characterId, out message))
                return false;

            CharacterTestRuntimeAiLabDraftSlot slot = DraftWorkspace.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            string previousGroupDraftId = slot?.GroupDraftId ?? string.Empty;

            bool opened = DraftWorkspace.TryOpenPublishedCardForSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                publishedCard,
                out entryIndex,
                out CharacterTestRuntimeAiLabBehaviorCardDraft openedCard,
                out bool workspaceChanged,
                out message);
            if (!opened)
                return false;
            entryIndex = FindBehaviorTestEntryIndex(openedCard, entryIndex);
            if (EnsureBehaviorTestPrimaryEnabled(entryIndex))
                workspaceChanged = true;
            slot = DraftWorkspace.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex);
            if (slot != null
                && previousGroupDraftId.Length > 0
                && !string.Equals(slot.GroupDraftId, previousGroupDraftId, StringComparison.Ordinal))
            {
                _publishedCardAuthoringSlotOverrideActive = true;
                _publishedCardAuthoringTeamId = _selectedSetupTeamId;
                _publishedCardAuthoringSlotIndex = _selectedSetupSlotIndex;
                _publishedCardAuthoringPreviousGroupDraftId = previousGroupDraftId;
                _publishedCardAuthoringGroupDraftId = slot.GroupDraftId;
            }
            if (workspaceChanged)
                _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        /// <summary>Opens a project catalog card as a project-backed local working copy.</summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab navigation only.</remarks>
        internal bool TryBeginProjectCardAuthoring(
            string stableCardId,
            out int entryIndex,
            out string message)
        {
            entryIndex = -1;
            if (!CanEditBehaviorCardDraft)
            {
                message = "请先返回实验设置阶段，再编辑项目行为卡。";
                return false;
            }
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft selectedGroup)
                || !selectedGroup.HasProjectSource
                || ProjectCatalogState?.Success != true
                || !ProjectCatalogState.GroupCatalog.TryGetExact(
                    selectedGroup.PublishedStableId,
                    selectedGroup.SourceProjectRevision,
                    out CharacterTestRuntimeAiPlannerBehaviorGroupRevision sourceGroup)
                || !ProjectCatalogState.CardCatalog.TryResolveLatestCompatible(
                    stableCardId,
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard))
            {
                message = "找不到当前项目行为组或项目行为卡的明确版本。";
                return false;
            }

            RestorePublishedCardAuthoringSlotBinding();
            CharacterTestRuntimeAiLabDraftSlot slot = DraftWorkspace.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            string previousGroupDraftId = slot?.GroupDraftId ?? string.Empty;
            bool opened = DraftWorkspace.TryOpenPublishedCardForSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                publishedCard,
                out entryIndex,
                out CharacterTestRuntimeAiLabBehaviorCardDraft openedCard,
                out bool workspaceChanged,
                out message,
                sourceGroup,
                ProjectCatalogState.GroupCatalogPath,
                ProjectCatalogState.GroupFileHash);
            if (!opened)
                return false;

            entryIndex = FindBehaviorTestEntryIndex(openedCard, entryIndex);
            if (EnsureBehaviorTestPrimaryEnabled(entryIndex))
                workspaceChanged = true;

            slot = DraftWorkspace.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex);
            if (slot != null
                && previousGroupDraftId.Length > 0
                && !string.Equals(slot.GroupDraftId, previousGroupDraftId, StringComparison.Ordinal))
            {
                _publishedCardAuthoringSlotOverrideActive = true;
                _publishedCardAuthoringTeamId = _selectedSetupTeamId;
                _publishedCardAuthoringSlotIndex = _selectedSetupSlotIndex;
                _publishedCardAuthoringPreviousGroupDraftId = previousGroupDraftId;
                _publishedCardAuthoringGroupDraftId = slot.GroupDraftId;
            }
            if (workspaceChanged)
                _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            _statusMessage = message;
            return true;
        }

        private int FindBehaviorTestEntryIndex(
            CharacterTestRuntimeAiLabBehaviorCardDraft openedCard,
            int fallbackIndex)
        {
            if (openedCard == null
                || !DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                return fallbackIndex;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (string.Equals(
                        group.Entries[i].CardDraftId,
                        openedCard.CardDraftId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return fallbackIndex;
        }

        private bool EnsureBehaviorTestPrimaryEnabled(int entryIndex)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (!entry.Disabled && !entry.RuntimeEntry.Disabled)
                return false;

            entry.Disabled = false;
            entry.RuntimeEntry.Disabled = false;
            group.CurrentRevision++;
            return true;
        }

        /// <summary>
        /// Restores the complete experiment behavior group after leaving isolated Published-card
        /// authoring. The isolated card draft remains in the workspace for later editing.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit workspace navigation only.</remarks>
        internal bool RestorePublishedCardAuthoringSlotBinding()
        {
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out _))
            {
                return false;
            }

            if (!_publishedCardAuthoringSlotOverrideActive)
            {
                bool repaired = workspace.TryRestoreOrphanedPublishedCardAuthoringGroup(
                    _matchConfig,
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex);
                if (!repaired)
                    return false;
                _draftPersistence.MarkDirty();
                BuildCandidateRevision();
                return true;
            }

            bool restored = workspace.TryRestoreGroupForSlot(
                _publishedCardAuthoringTeamId,
                _publishedCardAuthoringSlotIndex,
                _publishedCardAuthoringGroupDraftId,
                _publishedCardAuthoringPreviousGroupDraftId);
            _publishedCardAuthoringSlotOverrideActive = false;
            _publishedCardAuthoringTeamId = 0;
            _publishedCardAuthoringSlotIndex = -1;
            _publishedCardAuthoringPreviousGroupDraftId = string.Empty;
            _publishedCardAuthoringGroupDraftId = string.Empty;
            if (!restored)
                return false;

            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            return true;
        }

        /// <remarks>LowFreqAlloc: authoring presentation refresh only; never writes catalogs.</remarks>
        internal bool HasUnpublishedBehaviorCardContent(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            int entryIndex,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            if (group == null
                || entryIndex < 0
                || entryIndex >= group.Entries.Count
                || group.Entries[entryIndex]?.RuntimeEntry == null
                || string.IsNullOrWhiteSpace(group.Entries[entryIndex].CardDraftId)
                || workspace?.Cards.TryGetValue(
                    group.Entries[entryIndex].CardDraftId,
                    out card) != true
                || card?.HasPublishedSource != true)
            {
                diagnostic = "行为卡 Published 来源或草稿身份不可用。";
                return true;
            }
            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession authoring =
                ResolveProjectCatalogAuthoringForComparison();
            if (authoring != null && authoring.ResolveTacticalSource == null)
                authoring.ResolveTacticalSource = CaptureTacticalSourceForComparison;
            if (authoring == null
                || !authoring.TryHasUnpublishedCardChanges(
                    card,
                    _behaviorCardReferences,
                    out bool hasUnpublishedChanges,
                    out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                    diagnostic = "项目行为卡 canonical compare 不可用。";
                return true;
            }
            return hasUnpublishedChanges;
        }

        /// <remarks>LowFreqAlloc: authoring presentation refresh only; never writes catalogs.</remarks>
        internal bool HasUnpublishedBehaviorGroupContent(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (group?.HasProjectSource != true || workspace == null)
            {
                diagnostic = "行为组 Published 来源或草稿工作区不可用。";
                return true;
            }
            CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession authoring =
                ResolveProjectCatalogAuthoringForComparison();
            if (authoring == null
                || !authoring.TryHasUnpublishedGroupChanges(
                    group,
                    workspace.Cards,
                    out bool hasUnpublishedChanges,
                    out diagnostic))
            {
                if (string.IsNullOrWhiteSpace(diagnostic))
                    diagnostic = "项目行为组 canonical compare 不可用。";
                return true;
            }
            return hasUnpublishedChanges;
        }

        private CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession
            ResolveProjectCatalogAuthoringForComparison()
        {
            if (_projectCatalogAuthoring != null)
                return _projectCatalogAuthoring;
            return _matchConfig?.ProjectCatalogState != null
                ? new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
                    _matchConfig.ProjectCatalogState)
                : null;
        }
        private CharacterTestTacticalPublishedSource CaptureTacticalSourceForComparison(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            => CharacterTestTacticalPublishedSource.CaptureDraft(DraftWorkspace, card);

        internal bool CanPublishBehaviorCardDraft(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            int entryIndex,
            out string reason)
        {
            bool projectFlow = group?.HasProjectSource == true;
            if (projectFlow && (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite))
            {
                reason = ProjectCatalogState?.Diagnostic ?? "项目行为卡目录当前不可写。";
                return false;
            }
            if (!projectFlow && _publishSource == null)
            {
                reason = "本地玩家卡库的 Published catalog 尚未接入。";
                return false;
            }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                reason = "Publish 只能在战前配置阶段执行。";
                return false;
            }
            if (group == null || entryIndex < 0 || entryIndex >= group.Entries.Count
                || (!projectFlow && (group.Entries.Count != 1 || entryIndex != 0)))
            {
                reason = "Publish 需要单张行为卡编辑上下文。";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            if (CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.IsNoOpPlaceholder(
                    entry.BehaviorId))
            {
                reason = "寻敌、兜底与无动作占位是免费基础策略，不能作为项目行为卡发布。";
                return false;
            }
            if (!DraftWorkspace.Cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                || !card.HasPublishedSource)
            {
                reason = "当前草稿没有可追溯的 Published 来源。";
                return false;
            }
            EnsureBehaviorCardReferences();
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            long referenceHash = _behaviorCardReferences != null
                ? _behaviorCardReferences.StableHash
                : 0L;
            long legalityIdentity = _playerLegalitySource != null
                ? _playerLegalitySource.ProjectionIdentity
                : 0L;
            if (_hasPublishPreflightCache
                && ReferenceEquals(_cachedPublishGroup, group)
                && ReferenceEquals(_cachedPublishCard, card)
                && ReferenceEquals(_cachedPublishSource, _publishSource)
                && _cachedPublishWorkspaceRevision == workspace.CurrentRevision
                && _cachedPublishGroupRevision == group.CurrentRevision
                && _cachedPublishCardRevision == card.CurrentRevision
                && _cachedPublishEntryIndex == entryIndex
                && _cachedPublishPhase == (int)_phase
                && _cachedPublishReferenceHash == referenceHash
                && _cachedPublishLegalityIdentity == legalityIdentity)
            {
                reason = _cachedPublishReason;
                return _cachedCanPublish;
            }

            PublishPreflightEvaluationCount++;
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(
                    card.LogicPayload,
                    _behaviorCardReferences);
            if (!compile.Success || compile.Program == null)
            {
                reason = compile.Diagnostics.Count > 0
                    ? compile.Diagnostics[0].Message
                    : "Logic v3 编译未通过。";
                return CachePublishPreflight(
                    false,
                    reason,
                    workspace,
                    group,
                    card,
                    entryIndex,
                    referenceHash,
                    legalityIdentity);
            }
            if (compile.Program.SemanticHash == card.SourcePublishedSemanticHash)
            {
                reason = "当前逻辑与 Published 来源一致，没有需要发布的语义变化。";
                return CachePublishPreflight(
                    false,
                    reason,
                    workspace,
                    group,
                    card,
                    entryIndex,
                    referenceHash,
                    legalityIdentity);
            }
            CharacterTestRuntimeAiLabPlayerLegalityState legality = projectFlow
                ? null
                : EvaluateSelectedPlayerLegality(group);
            for (int i = 0; legality != null && i < legality.Reasons.Count; i++)
            {
                CharacterTestRuntimeAiLabPlayerLegalityReason legalityReason = legality.Reasons[i];
                if (legalityReason.Severity == CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error
                    && legalityReason.Kind != CharacterTestRuntimeAiLabPlayerLegalityReasonKind.UnpublishedSemanticChanges)
                {
                    reason = legalityReason.Message;
                    return CachePublishPreflight(
                        false,
                        reason,
                        workspace,
                        group,
                        card,
                        entryIndex,
                        referenceHash,
                        legalityIdentity);
                }
            }
            reason = "将生成下一条连续 Published revision；不会修改当前会话。";
            return CachePublishPreflight(
                true,
                reason,
                workspace,
                group,
                card,
                entryIndex,
                referenceHash,
                legalityIdentity);
        }

        private bool CachePublishPreflight(
            bool canPublish,
            string reason,
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            int entryIndex,
            long referenceHash,
            long legalityIdentity)
        {
            _cachedPublishGroup = group;
            _cachedPublishCard = card;
            _cachedPublishSource = _publishSource;
            _cachedPublishWorkspaceRevision = workspace.CurrentRevision;
            _cachedPublishGroupRevision = group.CurrentRevision;
            _cachedPublishCardRevision = card.CurrentRevision;
            _cachedPublishEntryIndex = entryIndex;
            _cachedPublishPhase = (int)_phase;
            _cachedPublishReferenceHash = referenceHash;
            _cachedPublishLegalityIdentity = legalityIdentity;
            _cachedCanPublish = canPublish;
            _cachedPublishReason = reason ?? string.Empty;
            _hasPublishPreflightCache = true;
            return canPublish;
        }

        internal bool TryPublishBehaviorCardDraft(int entryIndex, out string message)
        {
            _hasLastPublishAttempt = true;
            _lastPublishSucceeded = false;
            _lastPublishStableCardId = string.Empty;
            message = "当前行为卡草稿不可用。";
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                _lastPublishStatus = "Publish 失败：" + message;
                _statusMessage = _lastPublishStatus;
                return false;
            }
            if (entryIndex >= 0 && entryIndex < group.Entries.Count)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft attemptedEntry = group.Entries[entryIndex];
                if (DraftWorkspace.Cards.TryGetValue(
                        attemptedEntry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft attemptedCard))
                {
                    _lastPublishStableCardId = attemptedCard.PublishedStableId;
                }
            }
            if (!CanPublishBehaviorCardDraft(group, entryIndex, out message))
            {
                _lastPublishStatus = "Publish 失败：" + message;
                _statusMessage = _lastPublishStatus;
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            CharacterTestArenaSlotConfig slot = _matchConfig.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (slot == null)
            {
                message = "当前行为卡没有对应的角色槽位。";
                _lastPublishStatus = message;
                _statusMessage = message;
                return false;
            }
            bool projectFlow = group.HasProjectSource;
            CharacterTestRuntimeAiLabPublishResult result;
            if (projectFlow)
            {
                EnsureBehaviorCardReferences();
                result = _projectCatalogAuthoring.TryPublishCard(
                    DraftWorkspace.Cards[entry.CardDraftId],
                    _behaviorCardReferences);
            }
            else
            {
                result = _publishSource.TryPublish(
                    slot.CharacterId,
                    group,
                    DraftWorkspace.Cards,
                    entry.CardDraftId);
            }
            if (!result.Success)
            {
                message = result.Diagnostics.Count > 0
                    ? result.Diagnostics[0].Message
                    : "Publish 门禁未通过。";
                _lastPublishStatus = "Publish 失败：" + message;
                _statusMessage = _lastPublishStatus;
                return false;
            }
            if (!DraftWorkspace.TryAdoptPublishedRevision(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    entryIndex,
                    result.PublishedCard,
                    out string adoptionMessage))
            {
                message = "Published revision 已生成，但草稿来源收养失败：" + adoptionMessage;
                _lastPublishStatus = message;
                _statusMessage = message;
                return false;
            }

            if (projectFlow)
                _matchConfig.UpdateProjectCatalogState(_projectCatalogAuthoring.State);

            _draftPersistence.MarkDirty();
            bool saved = _draftPersistence.Flush();
            if (saved)
                SetBehaviorRecipeSaveCheckpoint(DraftWorkspace, entry.CardDraftId);
            BuildCandidateRevision();
            message = "已发布 " + result.PublishedCard.StableCardId
                + " r" + result.PublishedCard.Revision.ToString(CultureInfo.InvariantCulture)
                + "；当前会话 evaluator 与 Replay 历史未改变。";
            if (!saved)
                message += " 草稿来源仅在内存中收养，持久化需要恢复：" + _draftPersistence.LastStatus;
            _lastPublishStatus = message;
            _statusMessage = message;
            _lastPublishSucceeded = true;
            return true;
        }

        internal bool CanPublishBehaviorGroupDraft(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out string reason)
        {
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                reason = ProjectCatalogState?.Diagnostic;
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "Player 构建只读；项目行为目录只能在 Unity Editor 中发布。";
                return false;
            }
            if (group == null || !group.HasProjectSource)
            {
                reason = "当前行为组不是从具体项目 revision 创建的工作副本。";
                return false;
            }
            if (!HasBehaviorGroupChangesToSave(group))
            {
                reason = "当前行为组没有需要保存的修改。";
                return false;
            }
            reason = "校验通过后保存行为组新版本；不会覆盖已有 revision。";
            return true;
        }

        internal bool TryPublishBehaviorGroupDraft(out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                message = "当前实验槽位没有可保存的行为组工作副本。";
                _statusMessage = message;
                return false;
            }
            if (!CanPublishBehaviorGroupDraft(group, out message))
            {
                _statusMessage = message;
                return false;
            }
            CharacterTestRuntimeAiPlannerProjectGroupPublishResult result =
                _projectCatalogAuthoring.TryPublishGroup(group, DraftWorkspace.Cards);
            if (!result.Success)
            {
                message = result.Diagnostic;
                _statusMessage = message;
                return false;
            }
            group.SourceProjectRevision = result.Group.Revision;
            group.SourceProjectContentHash = result.Group.ContentHash;
            group.SourceCatalogFileHash = result.State.GroupFileHash;
            group.CurrentRevision = 1;
            SetBehaviorGroupSaveCheckpoint(DraftWorkspace, group.GroupDraftId);
            _matchConfig.AdoptPublishedProjectGroup(result.Group, result.State);
            message = "已保存项目行为组“" + result.Group.DisplayName
                + "” r" + result.Group.Revision.ToString(CultureInfo.InvariantCulture)
                + "；新实验使用这个组，并解析所引用行为卡的最新兼容版本。";
            _statusMessage = message;
            return true;
        }

        internal bool TryPublishSetupBehaviorGroupBasicInfo(
            string groupDraftId,
            string stableGroupId,
            int sourceRevision,
            long sourceContentHash,
            string displayName,
            string description,
            string tagsText,
            out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || !string.Equals(group.GroupDraftId, groupDraftId, StringComparison.Ordinal)
                || !string.Equals(group.PublishedStableId, stableGroupId, StringComparison.Ordinal)
                || group.SourceProjectRevision != sourceRevision
                || group.SourceProjectContentHash != sourceContentHash)
            {
                message = "行为组来源已变化，请重新选择后再保存。";
                _statusMessage = message;
                return false;
            }
            if (_projectCatalogAuthoring == null || !_projectCatalogAuthoring.CanWrite)
            {
                message = ProjectCatalogState?.Diagnostic;
                if (string.IsNullOrWhiteSpace(message))
                    message = "项目行为组目录不可写。";
                _statusMessage = message;
                return false;
            }

            CharacterTestRuntimeAiPlannerProjectGroupPublishResult result =
                _projectCatalogAuthoring.TryPublishGroupBasicInfo(
                    stableGroupId,
                    sourceRevision,
                    sourceContentHash,
                    displayName,
                    description,
                    tagsText);
            if (!result.Success)
            {
                message = result.Diagnostic;
                _statusMessage = message;
                return false;
            }

            if (!_matchConfig.BehaviorGroups.TryAdoptProjectBasicInfoRevision(result.Group))
            {
                message = "基本信息 revision 已生成，但当前实验草稿无法收养该版本。";
                _statusMessage = message;
                return false;
            }

            group.DisplayName = result.Group.DisplayName;
            group.Profile.DisplayName = result.Group.DisplayName;
            group.SourceProjectRevision = result.Group.Revision;
            group.SourceProjectContentHash = result.Group.ContentHash;
            group.SourceCatalogFileHash = result.State.GroupFileHash;
            _matchConfig.UpdateProjectCatalogState(result.State);
            _draftPersistence.MarkDirty();
            bool saved = _draftPersistence.Flush();
            message = "已保存行为组基本信息“" + result.Group.DisplayName
                + "” r" + result.Group.Revision.ToString(CultureInfo.InvariantCulture) + "。";
            if (!saved)
                message += " 新 revision 已写入，但本地草稿来源持久化需要恢复：" + _draftPersistence.LastStatus;
            _statusMessage = message;
            return true;
        }

        internal bool TryConfirmSetupBehaviorGroupDraft(
            string groupDraftId,
            string displayName,
            string description,
            string tagsText,
            out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !string.Equals(group.GroupDraftId, groupDraftId, StringComparison.Ordinal))
            {
                message = "当前选择的本地行为组草稿已变化，请重新选择。";
                return false;
            }
            CharacterTestRuntimeAiPlannerProjectGroupPublishResult result =
                _projectCatalogAuthoring.TryConfirmLocalGroupDraft(
                    group,
                    DraftWorkspace.Cards,
                    displayName,
                    description,
                    tagsText);
            if (!result.Success)
            {
                message = result.Diagnostic;
                return false;
            }
            if (!_matchConfig.BehaviorGroups.TryAddProjectGroup(result.Group, out int arenaDraftId))
            {
                message = "正式行为组已写入项目目录，但当前实验未能采用它。";
                return false;
            }
            CharacterTestArenaSlotConfig slot = _matchConfig.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            slot.BehaviorGroupDraftId = arenaDraftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(arenaDraftId);
            slot.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
            _matchConfig.UpdateProjectCatalogState(result.State);
            group.DisplayName = result.Group.DisplayName;
            group.Profile.DisplayName = result.Group.DisplayName;
            group.Profile.GroupId = result.Group.StableGroupId;
            group.PublishedStableId = result.Group.StableGroupId;
            group.SourceProjectRevision = result.Group.Revision;
            group.SourceProjectContentHash = result.Group.ContentHash;
            group.SourceProjectPath = result.State.GroupCatalogPath;
            group.SourceCatalogFileHash = result.State.GroupFileHash;
            DraftWorkspace.TrySelectGroupForSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                group.GroupDraftId,
                arenaDraftId,
                out _);
            _draftPersistence.MarkDirty();
            _draftPersistence.Flush();
            message = "已将本地草稿确认为正式行为组“" + result.Group.DisplayName
                + "” r1。";
            _statusMessage = message;
            return true;
        }

        internal bool TryCreateBehaviorGroupFromSelected(out string message)
        {
            CharacterTestArenaSlotConfig slot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (slot == null || DraftWorkspace == null)
            {
                message = "当前没有可装配行为组的测试角色槽位。";
                return false;
            }
            if (!DraftWorkspace.TryCreateLocalGroupCopyForSlot(
                    slot.TeamId,
                    slot.SlotIndex,
                    out _,
                    out message))
                return false;
            _draftPersistence.MarkDirty();
            _draftPersistence.Flush();
            _statusMessage = message;
            return true;
        }

        private bool TryCreateBlankTrainingDummyGroup(
            CharacterTestArenaBehaviorGroupDraft trainingDummy,
            out CharacterTestRuntimeAiPlannerBehaviorGroupRevision created,
            out string message)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile blankProfile =
                trainingDummy.Profile.Clone();
            blankProfile.Entries.Clear();
            return _projectCatalogAuthoring.TryCreateInitialGroup(
                CharacterTestArenaBehaviorGroupDraftStore.TrainingDummyStableId,
                "木桩",
                "空白训练木桩；不自动装配行为或生成兼容行为卡。",
                new[] { "木桩", "训练" },
                blankProfile,
                out created,
                out message);
        }

        internal bool TryDeleteSelectedBehaviorGroup(out string message)
        {
            CharacterTestArenaSlotConfig selectedSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (selectedSlot == null
                || !_matchConfig.BehaviorGroups.TryGetDraft(
                    selectedSlot.BehaviorGroupDraftId,
                    out CharacterTestArenaBehaviorGroupDraft selected))
            {
                message = "当前槽位没有可删除的项目行为组。";
                return false;
            }
            CharacterTestArenaBehaviorGroupDraft fallback = null;
            for (int i = 0; i < _matchConfig.BehaviorGroups.Drafts.Count; i++)
            {
                CharacterTestArenaBehaviorGroupDraft candidate =
                    _matchConfig.BehaviorGroups.Drafts[i];
                if (!string.Equals(candidate.StableId, selected.StableId, StringComparison.Ordinal))
                {
                    fallback = candidate;
                    break;
                }
            }
            if (fallback == null)
            {
                message = "项目中必须至少保留一个行为组。";
                return false;
            }
            if (!_projectCatalogAuthoring.TryDeleteGroup(selected.StableId, out message)
                || !_matchConfig.BehaviorGroups.TryRemoveProjectGroup(
                    selected.StableId,
                    out int removedDraftId))
            {
                return false;
            }

            ReassignDeletedBehaviorGroup(_matchConfig.Team1Slots, removedDraftId, fallback.DraftId);
            ReassignDeletedBehaviorGroup(_matchConfig.Team2Slots, removedDraftId, fallback.DraftId);
            _matchConfig.UpdateProjectCatalogState(_projectCatalogAuthoring.State);
            ReplaceDraftWorkspaceFromMatchConfig();
            message = "已删除行为组“" + selected.DisplayName + "”；使用它的角色已改为“"
                + fallback.DisplayName + "”。";
            _statusMessage = message;
            return true;
        }

        private void ReplaceDraftWorkspaceFromMatchConfig()
        {
            string workspaceId = _draftWorkspace?.WorkspaceId;
            _draftWorkspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(
                _matchConfig,
                workspaceId);
            _draftWorkspace.TrySetFrameLimit(_frameLimit);
            _draftPersistence.AdoptWorkspace(_draftWorkspace);
            ResetBehaviorRecipeSaveCheckpoint(_draftWorkspace);
        }

        private static void ReassignDeletedBehaviorGroup(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            int removedDraftId,
            int fallbackDraftId)
        {
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterTestArenaSlotConfig slot = slots[i];
                if (slot == null || slot.BehaviorGroupDraftId != removedDraftId)
                    continue;
                slot.BehaviorGroupDraftId = fallbackDraftId;
                slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    fallbackDraftId);
                slot.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
            }
        }

        private bool TrySelectPlayerAuthoringSlot(int characterId, out string message)
        {
            IReadOnlyList<CharacterTestArenaSlotConfig> slots = _matchConfig?.Team1Slots;
            int fallbackIndex = -1;
            for (int i = 0; slots != null && i < slots.Count; i++)
            {
                CharacterTestArenaSlotConfig slot = slots[i];
                if (slot == null || slot.CharacterId != characterId)
                    continue;
                if (fallbackIndex < 0)
                    fallbackIndex = i;
                if (!slot.Enabled)
                    continue;
                _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
                _selectedSetupSlotIndex = slot.SlotIndex;
                message = string.Empty;
                return true;
            }
            if (fallbackIndex >= 0)
            {
                CharacterTestArenaSlotConfig slot = slots[fallbackIndex];
                _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
                _selectedSetupSlotIndex = slot.SlotIndex;
                message = string.Empty;
                return true;
            }
            message = "玩家行为卡对应的 Runtime AI Lab 角色槽位不可用。";
            return false;
        }

        internal bool TryResolveBehaviorGroupForLoadout(
            int loadoutId,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            group = null;
            if (_matchConfig?.BehaviorGroups == null || loadoutId <= 0)
                return false;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (workspace == null)
                return false;

            int draftId = loadoutId - CharacterTestArenaBehaviorGroupDraftStore.SessionLoadoutIdBase;
            if (!_matchConfig.BehaviorGroups.TryGetDraft(
                    draftId,
                    out CharacterTestArenaBehaviorGroupDraft arenaDraft))
            {
                return false;
            }

            foreach (KeyValuePair<string, CharacterTestRuntimeAiLabBehaviorGroupDraft> pair
                in workspace.Groups)
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft candidate = pair.Value;
                if (candidate != null
                    && string.Equals(candidate.PublishedStableId, arenaDraft.StableId, StringComparison.Ordinal))
                {
                    group = candidate;
                    return true;
                }
            }

            return false;
        }

        internal bool TryResolveBehaviorGroupForActiveRevision(
            long programSemanticHash,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            group = null;
            CharacterTestRuntimeAiLabCandidateRevision revision = _activeRevision;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (revision == null
                || workspace == null
                || string.IsNullOrEmpty(revision.GroupDraftId)
                || programSemanticHash == 0L)
            {
                return false;
            }

            bool programBelongsToRevision = false;
            for (int i = 0; i < revision.LogicPrograms.Count; i++)
            {
                if (revision.LogicPrograms[i]?.SemanticHash == programSemanticHash)
                {
                    programBelongsToRevision = true;
                    break;
                }
            }
            return programBelongsToRevision
                && TryGetAuthoringGroup(revision.GroupDraftId, out group)
                && group != null;
        }

        /// <summary>Resolves the immutable Runtime AI Lab revision that owned one captured frame.</summary>
        /// <remarks>NoAlloc. Presentation-only lookup over low-frequency Apply history.</remarks>
        internal bool TryResolveAppliedRevisionAtFrame(
            int frame,
            int profileSlot,
            long authorityHash,
            out CharacterTestRuntimeAiLabCandidateRevision revision)
        {
            revision = null;
            if (frame < 0 || profileSlot < 0)
                return false;

            for (int i = _appliedRevisionEvents.Count - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[i];
                if (applied == null
                    || applied.ApplicationFrame > frame
                    || applied.ProfileSlot != profileSlot
                    || applied.AuthorityHash != authorityHash)
                {
                    continue;
                }
                revision = applied.Revision;
                return revision != null;
            }

            for (int i = 0; i < _baselineRevisionPeers.Count; i++)
            {
                CharacterTestRuntimeAiLabAppliedRevision baseline = _baselineRevisionPeers[i];
                if (baseline == null
                    || baseline.ApplicationFrame > frame
                    || baseline.ProfileSlot != profileSlot
                    || baseline.AuthorityHash != authorityHash)
                {
                    continue;
                }
                revision = baseline.Revision;
                return revision != null;
            }

            if (_baselineRevision != null
                && _baselineRevision.ApplicationFrame <= frame
                && _baselineRevision.ProfileSlot == profileSlot
                && _baselineRevision.AuthorityHash == authorityHash)
            {
                revision = _baselineRevision.Revision;
            }
            return revision != null;
        }

        internal bool TryResolveBehaviorEntry(
            int loadoutId,
            string runtimeEntryId,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out int entryIndex)
        {
            entryIndex = -1;
            if (!TryResolveBehaviorGroupForLoadout(loadoutId, out group)
                || string.IsNullOrEmpty(runtimeEntryId))
            {
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (!string.Equals(entry?.RuntimeEntry?.EntryId, runtimeEntryId, StringComparison.Ordinal))
                    continue;

                entryIndex = i;
                return true;
            }

            return false;
        }

        internal bool TryResolveTimelineBehaviorEntry(
            CharacterTestRuntimeAiLabTimelineItem item,
            out int entryIndex)
        {
            entryIndex = -1;
            return item != null
                && TryResolveBehaviorEntry(
                    item.BehaviorLoadoutId,
                    item.BehaviorEntryId,
                    out _,
                    out entryIndex);
        }

        internal bool ToggleAuthoringMode()
        {
            _authoringMode = CharacterTestRuntimeAiLabAuthoringMode.Developer;
            _statusMessage = "当前 Runtime AI Lab 统一使用开发者模式。";
            return true;
        }

        internal bool TryCreatePursuitDashCard(int entryIndex, out string message)
        {
            return TryReplaceSelectedCard(
                entryIndex,
                CharacterTestRuntimeAiLabGameplayAuthoring.CreateApproachPreset(),
                out message);
        }

        internal bool TryResetGameplayCard(int entryIndex, out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out _,
                    out message))
            {
                return false;
            }

            CharacterTestRuntimeAiLabBehaviorCardPayload payload;
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                int minimum = entry.RuntimeEntry.GetInt(
                    _matchConfig.BehaviorGroups.DefinitionCatalog.TryGetDefinition(
                        entry.BehaviorId,
                        out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                            ? definition
                            : null,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey);
                int maximum = entry.RuntimeEntry.GetInt(
                    definition,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey);
                payload = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic(minimum, maximum);
            }
            else if (string.Equals(
                entry.BehaviorId,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                StringComparison.Ordinal))
            {
                payload = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(group.Profile.AttackRange);
            }
            else
            {
                payload = CharacterTestRuntimeAiLabGameplayAuthoring.CreateApproachPreset();
            }
            return TryReplaceSelectedCard(entryIndex, payload, out message);
        }

        internal bool TryCreateReactiveChaseDodgeGroup(int entryIndex, out string message)
        {
            if (!_usesTransientDraftPersistence)
            {
                message = "旧版追击/闪避验收模板只能在隔离的临时测试草稿中使用，不能写入真实用户草稿。";
                return false;
            }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                message = "旧版追击/闪避验收模板只能在配置阶段创建。";
                return false;
            }
            bool changed = DraftWorkspace.TryCreateReactiveChaseDodgePreset(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            _behaviorCardCompileCache.Clear();
            BuildCandidateRevision();
            return true;
        }

        internal bool TryCycleCardTriggerDistance(int entryIndex, out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out _,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
                return false;
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                    CharacterTestRuntimeAiLabGameplayAuthoring.CycleThreatWindow(card.Payload);
                CharacterTestRuntimeAiLabGameplayAuthoring.TryGetThreatWindow(
                    payload,
                    out int minimum,
                    out int maximum);
                return TryReplaceSelectedCard(
                    entryIndex,
                    payload,
                    (group, selected) =>
                    {
                        selected.RuntimeEntry.SetInt(
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey,
                            minimum);
                        selected.RuntimeEntry.SetInt(
                            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey,
                            maximum);
                    },
                    out message);
            }

            CharacterTestRuntimeAiLabBehaviorCardPayload distancePayload =
                CharacterTestRuntimeAiLabGameplayAuthoring.CycleTriggerDistance(card.Payload);
            CharacterTestRuntimeAiLabGameplayAuthoring.TryGetStopDistance(distancePayload, out float stopDistance);
            return TryReplaceSelectedCard(
                entryIndex,
                distancePayload,
                (group, selected) => group.Profile.AttackRange = stopDistance,
                out message);
        }

        internal bool TryCycleCardFailureOutcome(int entryIndex, int stepIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            return TryReplaceSelectedCard(
                entryIndex,
                CharacterTestRuntimeAiLabGameplayAuthoring.CycleFailureOutcome(card.Payload, stepIndex),
                out message);
        }

        internal bool TrySetGameplayStopDistance(int entryIndex, float stopDistance, out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out _,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
            {
                return false;
            }
            if (!string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                message = "当前行为卡不支持停止距离。";
                return false;
            }
            if (!CharacterTestRuntimeAiLabGameplayAuthoring.TrySetStopDistance(
                    card.Payload,
                    stopDistance,
                    out CharacterTestRuntimeAiLabBehaviorCardPayload payload))
            {
                message = "停止距离必须在 0.1–10.0m 之间。";
                return false;
            }

            float committed = 0f;
            CharacterTestRuntimeAiLabGameplayAuthoring.TryGetStopDistance(payload, out committed);
            return TryReplaceSelectedCard(
                entryIndex,
                payload,
                (group, selected) => group.Profile.AttackRange = committed,
                out message);
        }

        internal bool TrySetGameplayThreatWindow(
            int entryIndex,
            int minimumImpactFrames,
            int maximumImpactFrames,
            out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out _,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
            {
                return false;
            }
            if (!string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                message = "当前行为卡不支持威胁窗口。";
                return false;
            }
            if (!CharacterTestRuntimeAiLabGameplayAuthoring.TrySetThreatWindow(
                    card.Payload,
                    minimumImpactFrames,
                    maximumImpactFrames,
                    out CharacterTestRuntimeAiLabBehaviorCardPayload payload))
            {
                message = "威胁窗口必须在 0–240 帧之间，且起始帧不能大于结束帧。";
                return false;
            }

            return TryReplaceSelectedCard(
                entryIndex,
                payload,
                (group, selected) =>
                {
                    selected.RuntimeEntry.SetInt(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey,
                        minimumImpactFrames);
                    selected.RuntimeEntry.SetInt(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey,
                        maximumImpactFrames);
                },
                out message);
        }

        internal bool TrySetGameplayThreatParameters(
            int entryIndex,
            int minimumImpactFrames,
            int maximumImpactFrames,
            int cooldownFrames,
            out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out _,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
            {
                return false;
            }
            if (!string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                message = "当前行为卡不支持威胁参数。";
                return false;
            }
            if (cooldownFrames < CharacterTestRuntimeAiLabGameplayAuthoring.MinimumDodgeCooldownFrames
                || cooldownFrames > CharacterTestRuntimeAiLabGameplayAuthoring.MaximumDodgeCooldownFrames)
            {
                message = "闪避冷却必须在 0–600 帧之间。";
                return false;
            }
            if (!CharacterTestRuntimeAiLabGameplayAuthoring.TrySetThreatWindow(
                    card.Payload,
                    minimumImpactFrames,
                    maximumImpactFrames,
                    out CharacterTestRuntimeAiLabBehaviorCardPayload payload))
            {
                message = "威胁窗口必须在 0–240 帧之间，且起始帧不能大于结束帧。";
                return false;
            }

            return TryReplaceSelectedCard(
                entryIndex,
                payload,
                (group, selected) =>
                {
                    selected.RuntimeEntry.SetInt(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey,
                        minimumImpactFrames);
                    selected.RuntimeEntry.SetInt(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey,
                        maximumImpactFrames);
                    selected.RuntimeEntry.SetInt(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionCooldownFramesKey,
                        cooldownFrames);
                },
                out message);
        }

        internal bool TrySetGameplayDodgeCooldown(int entryIndex, int cooldownFrames, out string message)
        {
            if (!TryGetCardContext(
                    entryIndex,
                    out _,
                    out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
            {
                return false;
            }
            if (!string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                message = "当前行为卡不支持闪避冷却。";
                return false;
            }
            if (cooldownFrames < CharacterTestRuntimeAiLabGameplayAuthoring.MinimumDodgeCooldownFrames
                || cooldownFrames > CharacterTestRuntimeAiLabGameplayAuthoring.MaximumDodgeCooldownFrames)
            {
                message = "闪避冷却必须在 0–600 帧之间。";
                return false;
            }

            return TryReplaceSelectedCard(
                entryIndex,
                card.Payload.Clone(),
                (group, selected) => selected.RuntimeEntry.SetInt(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionCooldownFramesKey,
                    cooldownFrames),
                out message);
        }

        internal bool QueueCandidateApply()
        {
            if (DraftWorkspace != null
                && DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft candidateGroup))
            {
                for (int entryIndex = 0; entryIndex < candidateGroup.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = candidateGroup.Entries[entryIndex];
                    if (entry == null
                        || !DraftWorkspace.Cards.TryGetValue(
                            entry.CardDraftId,
                            out CharacterTestRuntimeAiLabBehaviorCardDraft candidateCard))
                    {
                        continue;
                    }
                    if (!CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(
                            candidateCard.LogicPayload))
                        continue;
                    _statusMessage = "DETECTOR_WINDOW_CANONICALIZATION_REQUIRED: confirm the one-time Detector window migration before Apply F+1.";
                    return false;
                }
            }
            if (!CanApplyCandidate)
            {
                _statusMessage = "候选版本尚未完成预编译与确定性预检。";
                return false;
            }
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                _statusMessage = "请先启动实验，再把候选版本应用到下一帧。";
                return false;
            }
            int currentFrame = _context?.Slice != null
                ? (int)Math.Min(int.MaxValue, _context.Slice.CurrentFrame.Value)
                : 0;
            int applicationFrame = currentFrame == int.MaxValue ? int.MaxValue : currentFrame + 1;
            if (!TryQueueCandidateApply(_candidateRevision, applicationFrame, out string diagnostic))
            {
                _candidateDiagnostic = diagnostic;
                _statusMessage = diagnostic;
                return false;
            }
            _statusMessage = "候选版本已排队，将在下一 RuntimeFrame 前应用。";
            return true;
        }

        private bool TryReplaceSelectedCard(
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            out string message)
        {
            return TryReplaceSelectedCard(entryIndex, payload, null, out message);
        }

        internal bool TryReplaceSelectedCardLogic(
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            out string message)
        {
            if (CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(payload))
            {
                message = "DETECTOR_WINDOW_CANONICALIZATION_REQUIRED: confirm the one-time Detector window migration before editing.";
                return false;
            }
            if (!CanEditBehaviorCardDraft)
            {
                message = "行为卡只能在配置阶段或暂停的最新帧编辑。";
                return false;
            }
            bool changed = DraftWorkspace.TryReplaceCardLogicPayload(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                payload,
                out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            return true;
        }

        internal bool TryAddLogicDetector(
            int entryIndex,
            int stageIndex,
            out int detectorIndex,
            out string message)
        {
            detectorIndex = -1;
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }

            int suffix = 1;
            string detectorId;
            do detectorId = "detector.custom." + suffix++;
            while (ContainsDetector(payload, detectorId));
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[stageIndex];
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = detectorId,
                Title = "目标可见",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = stage.StageId,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge,
                Priority = (int)Math.Min(int.MaxValue, HighestDetectorPriority(payload) + 10L),
                ActivationStartFrame = 0,
                ActivationDurationFrames = stage.PlannedDurationFrames,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.StageEnd
            };
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = detectorId + ".condition",
                Fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            detectorIndex = payload.Detectors.Count;
            payload.Detectors.Add(detector);
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (!changed)
                detectorIndex = -1;
            return changed;
        }

        internal bool TryAddLogicStage(int entryIndex, int afterStageIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null)
            {
                message = "行为卡尚未生成 logic v2 草稿。";
                return false;
            }

            if (CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.HasV6Authority(payload))
            {
                int sourceTimelineIndex = payload.Timelines.Count == 0
                    ? -1
                    : Math.Max(0, Math.Min(afterStageIndex, payload.Timelines.Count - 1));
                int v6InsertionIndex = sourceTimelineIndex + 1;
                int v6Suffix = 1;
                string timelineId;
                do timelineId = "timeline.custom." + v6Suffix++;
                while (payload.Timelines.Exists(t => string.Equals(t.TimelineId, timelineId, StringComparison.Ordinal)));

                var newTimeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline
                {
                    TimelineId = timelineId,
                    Order = v6InsertionIndex,
                    Title = "时间轴 " + (payload.Timelines.Count + 1),
                    Note = string.Empty,
                    EntryConditionRoot = null
                };
                payload.Timelines.Insert(v6InsertionIndex, newTimeline);
                for (int i = 0; i < payload.Timelines.Count; i++)
                    payload.Timelines[i].Order = i;
                if (string.IsNullOrEmpty(payload.EntryTimelineId) || (sourceTimelineIndex < 0 && payload.Timelines.Count == 1))
                {
                    payload.EntryTimelineId = timelineId;
                }
                else if (sourceTimelineIndex >= 0 && sourceTimelineIndex < payload.Timelines.Count)
                {
                    var sourceTimeline = payload.Timelines[sourceTimelineIndex];
                    if (sourceTimeline != null)
                    {
                        int branchFrame = 0;
                        for (int evIdx = 0; evIdx < sourceTimeline.Events.Count; evIdx++)
                        {
                            var e = sourceTimeline.Events[evIdx];
                            if (e != null)
                                branchFrame = Math.Max(branchFrame, e.StartFrame + Math.Max(1, e.DurationFrames));
                        }
                        string branchEventId = "event.enter." + v6Suffix;
                        sourceTimeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent
                        {
                            EventId = branchEventId,
                            TimelineId = sourceTimeline.TimelineId,
                            Title = "进入 " + newTimeline.Title,
                            StartFrame = branchFrame,
                            TimingKind = CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant,
                            DurationFrames = 1,
                            Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation
                            {
                                Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline,
                                TargetTimelineId = timelineId
                            }
                        });
                    }
                }
                CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(payload);
                bool changedV6 = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
                if (changedV6)
                {
                    message = "已新增时间轴。";
                }
                return changedV6;
            }

            int sourceStageIndex = payload.Stages.Count == 0
                ? -1
                : Math.Max(0, Math.Min(afterStageIndex, payload.Stages.Count - 1));
            string sourceStageId = sourceStageIndex >= 0
                ? payload.Stages[sourceStageIndex].StageId
                : string.Empty;
            int insertionIndex = sourceStageIndex + 1;
            int suffix = 1;
            string stageId;
            do stageId = "stage.custom." + suffix++;
            while (ContainsStage(payload, stageId));
            int plannedStartFrame = ResolveTimelineInsertionFrame(payload, insertionIndex);
            payload.Stages.Insert(insertionIndex, new CharacterTestRuntimeAiLabLogicStage
            {
                StageId = stageId,
                Order = insertionIndex,
                Kind = CharacterTestRuntimeAiLabCardStepKind.Logic,
                PlannedStartFrame = plannedStartFrame,
                PlannedDurationFrames = 60,
                DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed,
                Title = "逻辑阶段 " + (suffix - 1),
                Note = "纯逻辑阶段；不会取得主动作执行权。"
            });
            for (int i = 0; i < payload.Stages.Count; i++)
                payload.Stages[i].Order = i;
            for (int i = 0; i < payload.Layout.Stages.Count; i++)
            {
                if (payload.Layout.Stages[i].Column >= insertionIndex)
                    payload.Layout.Stages[i].Column++;
            }
            payload.Layout.Stages.Add(new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = stageId,
                Column = insertionIndex,
                Width = 1f
            });
            if (sourceStageIndex < 0)
            {
                payload.EntryStageId = stageId;
            }
            else if (!RetargetBestTerminalTransition(payload, sourceStageId, stageId))
            {
                AddDefaultNextStageTransition(payload, sourceStageId, stageId);
            }
            if (!CharacterTestRuntimeAiLabBehaviorRecipeTimelineCanonicalizer.TryCanonicalize(
                    payload,
                    out message))
                return false;
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
            {
                message = HasIncomingTransition(payload, stageId)
                    ? "已新增时间轴，并在同一撤销事务中建立明确的进入事件。"
                    : "已新增时间轴；前一时间轴没有可解析的有限结束帧，请添加具体进入条件。";
            }
            return changed;
        }

        internal bool TryAddLogicTransition(int entryIndex, int detectorIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || detectorIndex < 0 || detectorIndex >= payload.Detectors.Count)
            {
                message = "请先选择一个有效持续检测器。";
                return false;
            }

            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
            int sourceIndex = 0;
            if (detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage)
            {
                sourceIndex = FindStageIndex(payload, detector.StageId);
                if (sourceIndex < 0)
                {
                    message = "检测器引用的源阶段不存在。";
                    return false;
                }
            }
            string transitionId;
            int suffix = 1;
            do transitionId = detector.DetectorId + ".transition." + suffix++;
            while (ContainsTransition(payload, transitionId));
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = transitionId,
                DetectorId = detector.DetectorId,
                SourceStageId = detector.Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    ? detector.StageId
                    : string.Empty,
                TargetStageId = sourceIndex + 1 < payload.Stages.Count
                    ? payload.Stages[sourceIndex + 1].StageId
                    : CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                Priority = detector.Priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                Label = detector.Title
            });
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "已从所选 Detector 创建 Transition；可在 Inspector 中调整目标与策略。";
            return changed;
        }

        internal bool TryCycleLogicStageKind(int entryIndex, int stageIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[stageIndex];
            if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic)
            {
                stage.Kind = CharacterTestRuntimeAiLabCardStepKind.Wait;
                stage.WaitFrames = Math.Max(1, stage.WaitFrames);
            }
            else if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
            {
                stage.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                stage.MoveMinDistance = 0f;
                stage.MoveMaxDistance = Math.Max(1.5f, stage.MoveMaxDistance);
            }
            else
            {
                stage.Kind = CharacterTestRuntimeAiLabCardStepKind.Logic;
                stage.AbilityId = 0;
                stage.CharacterActionId = default;
                stage.WaitFrames = 0;
            }
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "阶段类型已更新为 " + stage.Kind + "。";
            return changed;
        }

        internal bool TryAdjustLogicStageWidth(
            int entryIndex,
            int stageIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[stageIndex];
            stage.DurationMode = CharacterTestRuntimeAiLabLogicDurationMode.Fixed;
            stage.PlannedDurationFrames = Math.Max(
                1,
                Math.Min(36000, stage.PlannedDurationFrames + (direction < 0 ? -15 : 15)));
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "Stage duration is now " + stage.PlannedDurationFrames + " RuntimeFrame(s).";
            return changed;
        }

        internal bool TryAdjustLogicStageOffset(
            int entryIndex,
            int stageIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicStageLayout layout = FindOrCreateLayout(
                payload,
                payload.Stages[stageIndex].StageId,
                stageIndex);
            layout.Offset = Math.Max(-1f, Math.Min(1f, layout.Offset + (direction < 0 ? -0.1f : 0.1f)));
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        internal bool TryRemoveLogicStage(int entryIndex, int stageIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }
            if (payload.Stages.Count <= 1)
            {
                message = "行为卡必须至少保留一个阶段。";
                return false;
            }
            string stageId = payload.Stages[stageIndex].StageId;
            if (string.Equals(payload.EntryStageId, stageId, StringComparison.Ordinal))
            {
                message = "入口阶段不能直接删除；请先将另一阶段设为入口。";
                return false;
            }

            for (int i = payload.Detectors.Count - 1; i >= 0; i--)
            {
                if (payload.Detectors[i].Scope == CharacterTestRuntimeAiLabLogicDetectorScope.Stage
                    && string.Equals(payload.Detectors[i].StageId, stageId, StringComparison.Ordinal))
                {
                    string detectorId = payload.Detectors[i].DetectorId;
                    payload.Detectors.RemoveAt(i);
                    for (int transitionIndex = payload.Transitions.Count - 1; transitionIndex >= 0; transitionIndex--)
                    {
                        if (string.Equals(payload.Transitions[transitionIndex].DetectorId, detectorId, StringComparison.Ordinal))
                            payload.Transitions.RemoveAt(transitionIndex);
                    }
                }
            }
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i].SourceStageId, stageId, StringComparison.Ordinal))
                {
                    payload.Transitions.RemoveAt(i--);
                    continue;
                }
                if (string.Equals(payload.Transitions[i].TargetStageId, stageId, StringComparison.Ordinal))
                    payload.Transitions[i].TargetStageId = CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            }
            for (int i = payload.Layout.Stages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(payload.Layout.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    payload.Layout.Stages.RemoveAt(i);
            }
            payload.Stages.RemoveAt(stageIndex);
            for (int i = 0; i < payload.Stages.Count; i++)
                payload.Stages[i].Order = i;
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "阶段及其阶段级检测/跳转已删除；指向该阶段的跳转已改为 End。";
            return changed;
        }

        internal bool TryMoveLogicStageColumn(
            int entryIndex,
            int stageIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || stageIndex < 0 || stageIndex >= payload.Stages.Count)
            {
                message = "请选择一个有效逻辑阶段。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicStage stage = payload.Stages[stageIndex];
            stage.PlannedStartFrame = Math.Max(
                0,
                Math.Min(36000, stage.PlannedStartFrame + (direction < 0 ? -15 : 15)));
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "Stage start is now RuntimeFrame " + stage.PlannedStartFrame + ".";
            return changed;
        }

        private static int ResolveTimelineInsertionFrame(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            int insertionIndex)
        {
            if (payload == null || payload.Stages.Count == 0 || insertionIndex <= 0)
                return 0;
            CharacterTestRuntimeAiLabLogicStage previous =
                payload.Stages[Math.Min(insertionIndex - 1, payload.Stages.Count - 1)];
            int duration = previous.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                ? Math.Max(1, previous.PlannedDurationFrames)
                : Math.Max(60, previous.PreviewDurationFrames);
            long start = (long)previous.PlannedStartFrame + duration;
            return start >= 36000 ? 36000 : (int)start;
        }

        internal bool TryAdjustLogicDetectorPriority(
            int entryIndex,
            int detectorIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || detectorIndex < 0 || detectorIndex >= payload.Detectors.Count)
            {
                message = "请选择一个有效持续检测器。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
            int delta = direction < 0 ? 10 : -10;
            long adjustedPriority = (long)detector.Priority + delta;
            detector.Priority = adjustedPriority > int.MaxValue
                ? int.MaxValue
                : adjustedPriority < int.MinValue
                    ? int.MinValue
                    : (int)adjustedPriority;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i].DetectorId, detector.DetectorId, StringComparison.Ordinal))
                    payload.Transitions[i].Priority = detector.Priority;
            }
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "检测轨道已重排；优先级属于行为语义，候选 hash 已重新计算。";
            return changed;
        }

        internal bool TryAdjustLogicDetectorWindowStart(
            int entryIndex,
            int detectorIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || detectorIndex < 0 || detectorIndex >= payload.Detectors.Count)
            {
                message = "Select a valid detector first.";
                return false;
            }
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
            detector.ActivationStartFrame = Math.Max(
                0,
                Math.Min(36000, detector.ActivationStartFrame + (direction < 0 ? -15 : 15)));
            bool changed = TryReplaceSelectedCardLogic(entryIndex, payload, out message);
            if (changed)
                message = "Detector window starts at +" + detector.ActivationStartFrame + " RuntimeFrame(s).";
            return changed;
        }

        internal bool TryCycleLogicDetectorFact(int entryIndex, int detectorIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || detectorIndex < 0 || detectorIndex >= payload.Detectors.Count)
            {
                message = "请选择一个有效持续检测器。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicDetector detector = payload.Detectors[detectorIndex];
            detector.Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
            CharacterTestRuntimeAiLabCardConditionFact fact;
            string title;
            CharacterTestRuntimeAiLabCardConditionFact currentFact = detector.Conditions.Count > 0
                ? detector.Conditions[0].Fact
                : CharacterTestRuntimeAiLabCardConditionFact.TargetVisible;
            detector.Conditions.Clear();
            if (currentFact == CharacterTestRuntimeAiLabCardConditionFact.TargetVisible)
            {
                fact = CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf;
                title = "危险命中预测";
            }
            else if (currentFact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf)
            {
                fact = CharacterTestRuntimeAiLabCardConditionFact.MovePathAvailable;
                title = "移动路径可用";
            }
            else
            {
                fact = CharacterTestRuntimeAiLabCardConditionFact.TargetVisible;
                title = "目标可见";
            }
            detector.Title = title;
            detector.Conditions.Add(new CharacterTestRuntimeAiLabCardCondition
            {
                ConditionId = detector.DetectorId + ".condition",
                Fact = fact,
                Operator = CharacterTestRuntimeAiLabCardConditionOperator.Equals,
                BoolValue = true
            });
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        internal bool TryCycleLogicTransitionPolicy(int entryIndex, int transitionIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || transitionIndex < 0 || transitionIndex >= payload.Transitions.Count)
            {
                message = "请选择一条有效事件跳转。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[transitionIndex];
            CharacterTestRuntimeAiLabLogicTransitionPolicy next =
                (CharacterTestRuntimeAiLabLogicTransitionPolicy)(((int)transition.Policy + 1) % 3);
            if (next == CharacterTestRuntimeAiLabLogicTransitionPolicy.PreserveAction
                && TargetOwnsPrimaryAction(payload, transition.TargetStageId))
            {
                next = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt;
            }
            transition.Policy = next;
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        internal bool TryCycleLogicTransitionTarget(
            int entryIndex,
            int transitionIndex,
            int direction,
            out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || transitionIndex < 0 || transitionIndex >= payload.Transitions.Count)
            {
                message = "请选择一条有效事件跳转。";
                return false;
            }
            CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[transitionIndex];
            int current = payload.Stages.Count;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, transition.TargetStageId, StringComparison.Ordinal))
                {
                    current = i;
                    break;
                }
            }
            int optionCount = payload.Stages.Count + 1;
            int next = (current + (direction < 0 ? optionCount - 1 : 1)) % optionCount;
            transition.TargetStageId = next < payload.Stages.Count
                ? payload.Stages[next].StageId
                : CharacterTestRuntimeAiLabLogicTransition.EndTarget;
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        internal bool TryRemoveLogicDetector(int entryIndex, int detectorIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || detectorIndex < 0 || detectorIndex >= payload.Detectors.Count)
            {
                message = "请选择一个有效持续检测器。";
                return false;
            }
            string detectorId = payload.Detectors[detectorIndex].DetectorId;
            payload.Detectors.RemoveAt(detectorIndex);
            for (int i = payload.Transitions.Count - 1; i >= 0; i--)
            {
                if (string.Equals(payload.Transitions[i].DetectorId, detectorId, StringComparison.Ordinal))
                    payload.Transitions.RemoveAt(i);
            }
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        internal bool TryRemoveLogicTransition(int entryIndex, int transitionIndex, out string message)
        {
            if (!TryGetCard(entryIndex, out CharacterTestRuntimeAiLabBehaviorCardDraft card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (payload == null || transitionIndex < 0 || transitionIndex >= payload.Transitions.Count)
            {
                message = "请选择一条有效事件跳转。";
                return false;
            }
            payload.Transitions.RemoveAt(transitionIndex);
            return TryReplaceSelectedCardLogic(entryIndex, payload, out message);
        }

        private static bool ContainsDetector(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string detectorId)
        {
            for (int i = 0; i < payload.Detectors.Count; i++)
            {
                if (string.Equals(payload.Detectors[i].DetectorId, detectorId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool ContainsStage(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            return FindStageIndex(payload, stageId) >= 0;
        }

        private static int FindStageIndex(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool ContainsTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string transitionId)
        {
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                if (string.Equals(payload.Transitions[i].TransitionId, transitionId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool RetargetBestTerminalTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string sourceStageId,
            string targetStageId)
        {
            CharacterTestRuntimeAiLabLogicTransition best = null;
            for (int i = 0; i < payload.Transitions.Count; i++)
            {
                CharacterTestRuntimeAiLabLogicTransition transition = payload.Transitions[i];
                if (!string.Equals(transition.SourceStageId, sourceStageId, StringComparison.Ordinal)
                    || !string.Equals(
                        transition.TargetStageId,
                        CharacterTestRuntimeAiLabLogicTransition.EndTarget,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (best == null
                    || transition.Priority > best.Priority
                    || (transition.Priority == best.Priority
                        && string.CompareOrdinal(transition.TransitionId, best.TransitionId) < 0))
                {
                    best = transition;
                }
            }
            if (best == null)
                return false;
            best.TargetStageId = targetStageId;
            best.EndReason = string.Empty;
            return true;
        }

        private static bool AddDefaultNextStageTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string sourceStageId,
            string targetStageId)
        {
            CharacterTestRuntimeAiLabLogicStage source = null;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i]?.StageId, sourceStageId, StringComparison.Ordinal))
                {
                    source = payload.Stages[i];
                    break;
                }
            }
            if (!CanBindPrimaryActionEnd(source))
                return false;

            int suffix = 1;
            string detectorId;
            do detectorId = "detector." + sourceStageId + ".next." + suffix++;
            while (ContainsDetector(payload, detectorId));
            int priority = HighestDetectorPriority(payload) + 1;
            var detector = new CharacterTestRuntimeAiLabLogicDetector
            {
                DetectorId = detectorId,
                Title = "主行为结束帧",
                Scope = CharacterTestRuntimeAiLabLogicDetectorScope.Stage,
                StageId = sourceStageId,
                Trigger = CharacterTestRuntimeAiLabLogicDetectorTrigger.LocalFrame,
                LocalFrameAnchor =
                    CharacterTestRuntimeAiLabBehaviorRecipeLocalFrameAnchor.PrimaryActionEnd,
                Priority = priority,
                ActivationStartFrame = 0,
                ActivationDurationFrames = 0,
                WindowEndMode = CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Infinite
            };
            payload.Detectors.Add(detector);
            string transitionId;
            suffix = 1;
            do transitionId = sourceStageId + ".next." + suffix++;
            while (ContainsTransition(payload, transitionId));
            payload.Transitions.Add(new CharacterTestRuntimeAiLabLogicTransition
            {
                TransitionId = transitionId,
                DetectorId = detectorId,
                SourceStageId = sourceStageId,
                TargetStageId = targetStageId,
                Priority = priority,
                Policy = CharacterTestRuntimeAiLabLogicTransitionPolicy.Interrupt,
                Label = "主行为结束帧"
            });
            return true;
        }

        private static bool CanBindPrimaryActionEnd(CharacterTestRuntimeAiLabLogicStage stage)
        {
            if (stage == null)
                return false;
            if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)
                return stage.WaitFrames > 0;
            return (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility)
                && Math.Max(stage.PlannedDurationFrames, stage.PreviewDurationFrames) > 0;
        }

        private static bool HasIncomingTransition(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string targetStageId)
        {
            for (int i = 0; payload != null && i < payload.Transitions.Count; i++)
            {
                if (string.Equals(
                        payload.Transitions[i]?.TargetStageId,
                        targetStageId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static int HighestDetectorPriority(CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload)
        {
            int priority = 0;
            for (int i = 0; i < payload.Detectors.Count; i++)
                priority = Math.Max(priority, payload.Detectors[i].Priority);
            return priority;
        }

        private static CharacterTestRuntimeAiLabLogicStageLayout FindOrCreateLayout(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId,
            int fallbackColumn)
        {
            for (int i = 0; i < payload.Layout.Stages.Count; i++)
            {
                if (string.Equals(payload.Layout.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return payload.Layout.Stages[i];
            }
            var created = new CharacterTestRuntimeAiLabLogicStageLayout
            {
                StageId = stageId,
                Column = Math.Max(0, fallbackColumn),
                Width = 1f
            };
            payload.Layout.Stages.Add(created);
            return created;
        }

        private static bool TargetOwnsPrimaryAction(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stageId)
        {
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i].StageId, stageId, StringComparison.Ordinal))
                    return payload.Stages[i].OwnsPrimaryAction;
            }
            return false;
        }

        private bool TryReplaceSelectedCard(
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardPayload payload,
            Action<CharacterTestRuntimeAiLabBehaviorGroupDraft, CharacterTestRuntimeAiLabBehaviorEntryDraft> configureRuntime,
            out string message)
        {
            if (!CanEditBehaviorCardDraft)
            {
                message = "行为卡只能在配置阶段或暂停的最新帧编辑。";
                return false;
            }
            bool changed = DraftWorkspace.TryReplaceCardPayloadAndConfigureRuntime(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                payload,
                configureRuntime,
                out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            return true;
        }

        private bool TryGetCard(
            int entryIndex,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string message)
        {
            card = null;
            message = string.Empty;
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                message = "Behavior entry is unavailable.";
                return false;
            }
            if (!DraftWorkspace.Cards.TryGetValue(group.Entries[entryIndex].CardDraftId, out card))
            {
                message = "Behavior card draft is missing.";
                return false;
            }
            return true;
        }

        private bool TryGetCardContext(
            int entryIndex,
            out CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            out CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string message)
        {
            group = null;
            entry = null;
            card = null;
            message = string.Empty;
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out group)
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                message = "Behavior entry is unavailable.";
                return false;
            }
            entry = group.Entries[entryIndex];
            if (!DraftWorkspace.Cards.TryGetValue(entry.CardDraftId, out card))
            {
                message = "Behavior card draft is missing.";
                return false;
            }
            return true;
        }

        /// <remarks>LowFreqAlloc: explicit authoring preflight, never called from authority Tick.</remarks>
        private void BuildCandidateRevision()
        {
            using var startupProfile = CharacterTestStartupProfiler.BuildCandidate.Auto();
            _candidateState = CharacterTestRuntimeAiLabCandidateState.Compiling;
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out string workspaceDiagnostic))
            {
                FailCandidate(workspaceDiagnostic);
                return;
            }
            if (!TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                FailCandidate("候选行为组不可用。");
                return;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile sourceProfile =
                CharacterTestArenaBehaviorGroupDraftStore.CreateRuntimeProfile(
                    group.PublishedStableId,
                    group.Profile);
            if (group.Entries.Count != sourceProfile.Entries.Count)
            {
                FailCandidate("Candidate revision binding counts are inconsistent.");
                return;
            }
            int stepCount = 0;
            int conditionCount = 0;
            string firstDifference = string.Empty;
            bool runtimeApplicable = true;
            int skippedInvalidCardCount = 0;
            var runtimeEntries = new List<CharacterTestRuntimeAiLabBehaviorEntryDraft>(sourceProfile.Entries.Count);
            var compiledPrograms = new List<CharacterTestRuntimeAiLabCompiledLogicProgram>(sourceProfile.Entries.Count);
            var candidateProfile = sourceProfile.Clone();
            candidateProfile.Entries.Clear();
            for (int profileIndex = 0; profileIndex < sourceProfile.Entries.Count; profileIndex++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry profileEntry = sourceProfile.Entries[profileIndex];
                CharacterTestRuntimeAiLabBehaviorEntryDraft matchedEntry = null;
                for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry runtimeEntry = entry?.RuntimeEntry;
                    if (runtimeEntry != null
                        && string.Equals(runtimeEntry.EntryId, profileEntry.EntryId, StringComparison.Ordinal))
                    {
                        matchedEntry = entry;
                        break;
                    }
                }

                if (matchedEntry == null)
                {
                    FailCandidate("候选行为组缺少运行时条目：" + profileEntry.BehaviorId);
                    return;
                }
                if (_singleCardTestRequest.HasValue && matchedEntry.CardDraftId != _singleCardTestRequest.Value.CardDraftId)
                    continue;
                if (!workspace.Cards.TryGetValue(
                        matchedEntry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    FailCandidate("候选版本缺少行为卡：" + matchedEntry.BehaviorId);
                    return;
                }
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult cardCompile = CompileBehaviorCardForStart(card);
                if (cardCompile == null || !cardCompile.Success || cardCompile.Program == null)
                {
                    if (!IsRecoverableLegacyBehaviorCardCompileFailure(cardCompile))
                    {
                        FailCandidate(FormatBehaviorCardCompileFailure(card, cardCompile));
                        return;
                    }
                    skippedInvalidCardCount++;
                    continue;
                }

                runtimeEntries.Add(matchedEntry);
                compiledPrograms.Add(cardCompile.Program);
                var candidateEntry = profileEntry.Clone();
                if (_singleCardTestRequest.HasValue) candidateEntry.Enabled = true;
                candidateProfile.Entries.Add(candidateEntry);
            }

            CharacterTestRuntimeAiPlannerBehaviorCompileResult compositionCompile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    candidateProfile,
                    _matchConfig.BehaviorGroups.DefinitionCatalog);
            if (compositionCompile == null || !compositionCompile.Success)
            {
                FailCandidate("基础 behavior composition 编译失败；活动 evaluator 保持不变。");
                return;
            }

            long hash = ComputeCandidateCompositionHash(
                candidateProfile,
                _matchConfig.BehaviorGroups.DefinitionCatalog);
            if (_singleCardTestRequest.HasValue)
            {
                hash = MixCandidateHash(hash, "single-card-test");
                hash = MixCandidateHash(hash, _singleCardTestRequest.Value.SetupTeamId);
                hash = MixCandidateHash(hash, _singleCardTestRequest.Value.SetupSlotIndex);
            }
            var logicPrograms = compiledPrograms.ToArray();
            var cardIdentities = new CharacterTestRuntimeAiLabRevisionCardIdentity[runtimeEntries.Count];
            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = runtimeEntries[i];
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    workspace.Cards[entry.CardDraftId];
                CharacterTestRuntimeAiLabCompiledLogicProgram program = logicPrograms[i];
                hash = MixCandidateHash(hash, entry.EntryId);
                hash = MixCandidateHash(hash, program.SemanticHash);
                if (program.SelectionDefinition != null) hash = MixCandidateHash(hash, program.SelectionDefinition.StableHash);
                if (!CharacterTestRuntimeAiLabRevisionCardIdentity.TryCreate(
                        i,
                        entry,
                        card,
                        program,
                        out cardIdentities[i],
                        out string identityDiagnostic))
                {
                    FailCandidate(identityDiagnostic);
                    return;
                }
                if (!TryCompileExpectedRuntimeProjection(
                        group,
                        entry,
                        out CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult expectedCompile)
                    || expectedCompile?.Program == null
                    || expectedCompile.Program.SemanticHash != program.SemanticHash)
                {
                    runtimeApplicable = false;
                    if (string.IsNullOrEmpty(firstDifference))
                        firstDifference = card.DisplayName + " does not match its runtime behavior entry";
                }
                stepCount += program.Stages.Count;
                conditionCount += program.ActivationConditions.Count;
                for (int detectorIndex = 0; detectorIndex < program.Detectors.Count; detectorIndex++)
                    conditionCount += program.Detectors[detectorIndex].Conditions.Count;
                if (string.IsNullOrEmpty(firstDifference))
                    firstDifference = card.DisplayName;
            }

            var comparison = new CharacterTestRuntimeAiLabCandidateComparisonReport(
                runtimeEntries.Count,
                stepCount,
                conditionCount,
                firstDifference,
                hash,
                repeatable: true);
            if (!CharacterTestRuntimeAiLabCandidateRevision.TryCreate(
                    workspace.CurrentRevision,
                    hash,
                    candidateProfile,
                    comparison,
                    logicPrograms,
                    cardIdentities,
                    group.GroupDraftId,
                    out _candidateRevision,
                    out string candidateDiagnostic))
            {
                FailCandidate(candidateDiagnostic);
                return;
            }
            if (_singleCardTestRequest.HasValue)
            {
                if (_candidateRevision.LogicPrograms.Count != 1)
                { FailCandidate("单卡测试未能生成唯一的当前战术卡。"); return; }
                _candidateRevision = _candidateRevision.WithSingleCardTestTarget(
                    _singleCardTestRequest.Value.SetupTeamId, _singleCardTestRequest.Value.SetupSlotIndex);
            }
            _candidateState = CharacterTestRuntimeAiLabCandidateState.PreviewComplete;
            _candidateDiagnostic = comparison.ToSummary()
                + (skippedInvalidCardCount > 0
                    ? "\n已跳过 "
                        + skippedInvalidCardCount.ToString(CultureInfo.InvariantCulture)
                        + " 张配置错误的行为卡；草稿未被自动修改。"
                    : string.Empty)
                + (runtimeEntries.Count == 0
                    ? "\n空行为组直接进入测试；运行时不会产生 AI 动作。"
                    : runtimeApplicable
                    ? "\n行为卡已完成运行时预检，可应用现有 composition evaluator。"
                    : "\n复杂行为卡已通过 v2 逻辑 evaluator 运行时预检，可在 Apply F+1 边界应用。由实际阶段、Detector 和 Transition 驱动。");
        }

        private void FailCandidate(string diagnostic)
        {
            _candidateRevision = null;
            _candidateState = CharacterTestRuntimeAiLabCandidateState.CompileFailed;
            _candidateDiagnostic = diagnostic ?? "候选版本失败。";
        }

        private bool TryCompileExpectedRuntimeProjection(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            out CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile)
        {
            compile = null;
            if (group?.Profile == null || entry?.RuntimeEntry == null)
                return false;

            CharacterTestRuntimeAiLabBehaviorCardPayload expected;
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog catalog =
                _matchConfig?.BehaviorGroups?.DefinitionCatalog;
            if (string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal))
            {
                if (catalog == null || !catalog.TryGetDefinition(entry.BehaviorId, out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                    return false;
                expected = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateReactiveDodgeAtomic(
                    entry.RuntimeEntry.GetInt(
                        definition,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMinImpactFramesKey),
                    entry.RuntimeEntry.GetInt(
                        definition,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ReactionMaxImpactFramesKey));
            }
            else if (string.Equals(
                entry.BehaviorId,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                StringComparison.Ordinal))
            {
                expected = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateContinuousApproachAtomic(
                    group.Profile.AttackRange);
            }
            else
            {
                if (catalog == null || !catalog.TryGetDefinition(entry.BehaviorId, out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
                    return false;
                expected = CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                    entry.BehaviorId,
                    ToLegacyCardActionKind(definition.ActionPrimitive));
            }

            compile = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(
                CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(expected),
                _behaviorCardReferences);
            return compile != null && compile.Success;
        }

        /// <remarks>LowFreqAlloc: candidate preflight only.</remarks>
        private static long ComputeCandidateCompositionHash(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            var variant = new CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
                1,
                "candidate",
                "Candidate",
                profile);
            var loadout = new CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition(
                1,
                "runtime-ai-lab.candidate",
                "Candidate",
                string.Empty,
                new[] { variant });
            var catalog = new CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog(
                "runtime-ai-lab.candidate",
                1,
                new[] { loadout });
            return MixCandidateHash(catalog.CatalogHash, definitionCatalog?.CatalogHash ?? 0L);
        }

        private static long MixCandidateHash(long hash, long value)
        {
            return unchecked((hash ^ value) * 1099511628211L);
        }

        private static long MixCandidateHash(long hash, string value)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddLong("candidate.seed", hash);
            accumulator.AddStringStable("candidate.value", value);
            return accumulator.ToHash();
        }

        private static CharacterTestRuntimeAiPlannerActionKind ToLegacyCardActionKind(
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

        private void ProcessPendingCandidateApply()
        {
            if (!_candidateApplyQueued || _queuedCandidateRevision?.Profile == null)
                return;

            CharacterTestRuntimeAiLabCandidateRevision queuedCandidate = _queuedCandidateRevision;
            CharacterTestRuntimeAiLabQueuedApplyTargetIdentity queuedTarget = _queuedApplyTarget;
            if (!TryCaptureQueuedApplyTarget(queuedCandidate, out var currentTarget, out string targetDiagnostic))
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.TargetUnavailable,
                    "Apply F+1 已取消：" + targetDiagnostic);
                return;
            }
            if (!queuedTarget.Matches(currentTarget))
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.TargetIdentityChanged,
                    "Apply F+1 已取消：运行时目标或会话 authority 已变化。 Active revision 保持不变。");
                return;
            }

            int applicationFrame = _candidateApplyFrame;
            CharacterTestRuntimeAiPlannerCommandModule module =
                _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands;
            if (!TryCollectBehaviorLoadoutRuntimeProfileSlots(
                    queuedTarget.RuntimeLoadoutId,
                    queuedTarget.RuntimeProfileSlot,
                    out List<int> profileSlots,
                    out string profileSlotDiagnostic, queuedCandidate))
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.TargetUnavailable,
                    "Apply F+1 已取消：" + profileSlotDiagnostic);
                return;
            }

            ClearQueuedApplyState();
            if (!TryPrepareRevisionInstalls(profileSlots, queuedCandidate, out var requests, out var installedRevisions, out string bindingDiagnostic))
            {
                _candidateState = CharacterTestRuntimeAiLabCandidateState.ApplyFailed;
                _candidateDiagnostic = bindingDiagnostic; _statusMessage = "候选装配校验失败，当前运行未改变。"; return;
            }
            CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result = default;
            if (module == null
                || !module.TryApplyBehaviorCompositionOverlayBatch(requests, out result))
            {
                _candidateState = CharacterTestRuntimeAiLabCandidateState.ApplyFailed;
                _candidateDiagnostic = result.ReportText ?? "候选 evaluator 应用失败。";
                _statusMessage = "候选版本应用失败；活动 evaluator 与历史时间线保持不变。";
                return;
            }

            _activeRevision = installedRevisions[0];
            DraftWorkspace.LastAppliedRevision = _activeRevision.Revision;
            int sessionGeneration = _timeline?.SessionIdentity.Generation ?? 0;
            if (_timelineSessionStarted)
            {
                for (int i = 0; i < profileSlots.Count; i++)
                {
                    _appliedRevisionEvents.Add(new CharacterTestRuntimeAiLabAppliedRevision(
                        applicationFrame,
                        profileSlots[i],
                        installedRevisions[i],
                        sessionGeneration));
                }
            }
            else
            {
                SetBaselineRevisions(profileSlots, installedRevisions);
            }
            _candidateState = CharacterTestRuntimeAiLabCandidateState.Applied;
            _candidateDiagnostic = "Revision " + _activeRevision.Revision
                + " 已同步应用到同组的 " + profileSlots.Count + " 个 Runtime profile slot。";
            _statusMessage = "候选版本已在 RuntimeFrame 前应用。";
            InvalidateTimelineProjection();
        }

        private bool TryApplyCandidateAsSessionBaseline(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_candidateRevision?.Profile == null)
            {
                diagnostic = "当前行为组尚未生成可运行的行为卡版本。";
                return false;
            }

            CharacterTestRuntimeAiPlannerCommandModule module =
                _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands;
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts =
                _context?.Slice?.Services.Modules.RuntimeAiBehaviorLoadouts;
            CharacterTestArenaSlotConfig selectedSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            int profileSlot = 0;
            string selectionFailure = string.Empty;
            if (selectedSlot != null
                && loadouts != null
                && loadouts.TryResolveSelection(
                    CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                        selectedSlot.BehaviorGroupDraftId),
                    CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId,
                    out CharacterTestRuntimeAiBehaviorSelection _,
                    out selectionFailure))
            {
                if (_sessionBuilder == null
                    || !_sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(
                        _selectedSetupTeamId,
                        _selectedSetupSlotIndex,
                        out profileSlot))
                {
                    diagnostic = "无法解析所选角色重映射后的 Runtime AI Planner profile slot。";
                    return false;
                }
            }
            else if (!string.IsNullOrEmpty(selectionFailure))
            {
                diagnostic = IsBehaviorLoadoutFailure(selectionFailure)
                    ? FormatBehaviorLoadoutSessionDiagnostic(selectionFailure, loadouts)
                    : selectionFailure;
                return false;
            }

            if (!TryCollectBehaviorLoadoutRuntimeProfileSlots(
                    selectedSlot != null
                        ? CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                            selectedSlot.BehaviorGroupDraftId)
                        : 0,
                    profileSlot,
                    out List<int> profileSlots,
                    out diagnostic, _candidateRevision))
            {
                return false;
            }

            if (!TryPrepareRevisionInstalls(profileSlots, _candidateRevision, out var requests, out var installedRevisions, out diagnostic)) return false;
            var baselineSlots = new List<int>(profileSlots);
            var baselineRequests = new List<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>(requests);
            var baselineRevisions = new List<CharacterTestRuntimeAiLabCandidateRevision>(installedRevisions);
            if (_candidateRevision.IsSingleCardTest)
            {
                if (!TryPrepareSingleCardPeerInstalls(profileSlots, out var peerSlots, out var peerRequests,
                        out var peerRevisions, out diagnostic)) return false;
                baselineSlots.AddRange(peerSlots);
                baselineRequests.AddRange(peerRequests);
                baselineRevisions.AddRange(peerRevisions);
            }
            CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result = default;
            if (module == null
                || !module.TryApplyBehaviorCompositionOverlayBatch(baselineRequests, out result))
            {
                diagnostic = result.ReportText ?? "行为卡会话基线安装失败。";
                return false;
            }

            _candidateRevision = installedRevisions[0];
            SetBaselineRevisions(baselineSlots, baselineRevisions);
            if (!_deferBaselineActiveCommit)
            {
                _activeRevision = _candidateRevision;
                DraftWorkspace.LastAppliedRevision = _activeRevision.Revision;
            }
            ClearQueuedApplyState();
            _candidateState = CharacterTestRuntimeAiLabCandidateState.Applied;
            _candidateDiagnostic = "Revision " + _candidateRevision.Revision
                + " 已作为 " + baselineSlots.Count + " 个 Runtime profile slot 的首帧前行为卡基线。";
            return true;
        }

        /// <remarks>LowFreqAlloc: session bootstrap and explicit Apply F+1 only.</remarks>
        private bool TryCollectBehaviorLoadoutRuntimeProfileSlots(
            int runtimeLoadoutId,
            int preferredProfileSlot,
            out List<int> profileSlots,
            out string diagnostic, CharacterTestRuntimeAiLabCandidateRevision revision = null)
        {
            profileSlots = new List<int>(CharacterTestArenaMatchConfig.SlotsPerTeam * 2);
            diagnostic = string.Empty;
            if (revision?.IsSingleCardTest == true)
            {
                if (_sessionBuilder == null || !_sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(
                    revision.TestTeamId, revision.TestSlotIndex, out int targetProfile) || targetProfile != preferredProfileSlot)
                { diagnostic = "单卡测试的原角色槽位已变化，拒绝改写其他角色。"; return false; }
                profileSlots.Add(targetProfile);
                return true;
            }
            if (runtimeLoadoutId <= 0)
            {
                diagnostic = "无法识别当前候选版本所属的行为组。";
                return false;
            }
            if (_matchConfig == null || _sessionBuilder == null)
            {
                diagnostic = "Runtime AI Lab 会话配置尚未就绪。";
                return false;
            }

            if (!TryCollectBehaviorLoadoutRuntimeProfileSlots(
                    CharacterTestArenaMatchConfig.Team1Id,
                    _matchConfig.Team1Slots,
                    runtimeLoadoutId,
                    profileSlots,
                    out diagnostic)
                || !TryCollectBehaviorLoadoutRuntimeProfileSlots(
                    CharacterTestArenaMatchConfig.Team2Id,
                    _matchConfig.Team2Slots,
                    runtimeLoadoutId,
                    profileSlots,
                    out diagnostic))
            {
                return false;
            }

            int preferredIndex = profileSlots.IndexOf(preferredProfileSlot);
            if (preferredIndex < 0)
            {
                diagnostic = "所选角色的 Runtime AI Planner profile slot 不再属于当前行为组。";
                return false;
            }
            if (preferredIndex > 0)
            {
                profileSlots.RemoveAt(preferredIndex);
                profileSlots.Insert(0, preferredProfileSlot);
            }
            return true;
        }

        private bool TryCollectBehaviorLoadoutRuntimeProfileSlots(
            int teamId,
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            int runtimeLoadoutId,
            List<int> profileSlots,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                CharacterTestArenaSlotConfig slot = slots[slotIndex];
                if (slot == null
                    || !slot.Enabled
                    || CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                        slot.BehaviorGroupDraftId) != runtimeLoadoutId)
                {
                    continue;
                }
                if (!_sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(
                        teamId,
                        slotIndex,
                        out int profileSlot))
                {
                    diagnostic = "无法解析同组角色重映射后的 Runtime AI Planner profile slot。";
                    return false;
                }
                if (!profileSlots.Contains(profileSlot))
                    profileSlots.Add(profileSlot);
            }
            return true;
        }

        private void SetBaselineRevisions(
            IReadOnlyList<int> profileSlots,
            IReadOnlyList<CharacterTestRuntimeAiLabCandidateRevision> revisions)
        {
            _baselineRevision = null;
            _baselineRevisionPeers.Clear();
            for (int i = 0; i < profileSlots.Count; i++)
            {
                var applied = new CharacterTestRuntimeAiLabAppliedRevision(
                    (int)(_context?.Slice?.CurrentFrame.Value ?? 0),
                    profileSlots[i],
                    revisions[i],
                    0);
                if (i == 0)
                    _baselineRevision = applied;
                else
                    _baselineRevisionPeers.Add(applied);
            }
        }

        private int ResolveSelectedRuntimeProfileSlot()
        {
            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                _context?.Slice?.RuntimeAiPlannerCommands?.CreateSnapshot();
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = snapshot?.Agents;
            if (agents == null || agents.Count == 0)
                return 0;
            CharacterTestArenaSlotConfig selectedSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            int selectedLoadoutId = selectedSlot != null
                ? CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    selectedSlot.BehaviorGroupDraftId)
                : 0;
            if (selectedLoadoutId > 0)
            {
                if (_selectedAgentIndex >= 0 && _selectedAgentIndex < agents.Count)
                {
                    CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent =
                        agents[_selectedAgentIndex];
                    if (selectedAgent.RuntimeAiBehaviorLoadoutId == selectedLoadoutId)
                    {
                        return Math.Max(0, selectedAgent.RuntimeAiPlannerProfileSlot);
                    }
                }

                for (int i = 0; i < agents.Count; i++)
                {
                    if (agents[i].RuntimeAiBehaviorLoadoutId == selectedLoadoutId)
                        return Math.Max(0, agents[i].RuntimeAiPlannerProfileSlot);
                }
            }
            int index = Math.Max(0, Math.Min(_selectedAgentIndex, agents.Count - 1));
            return Math.Max(0, agents[index].RuntimeAiPlannerProfileSlot);
        }

        private void ResetAuthoringRuntimeState()
        {
            _authoringMode = CharacterTestRuntimeAiLabAuthoringMode.Developer;
            _candidateState = CharacterTestRuntimeAiLabCandidateState.None;
            _candidateRevision = null;
            _activeRevision = null;
            _candidateDiagnostic = "尚未生成候选版本。";
            ClearQueuedApplyState();
            _appliedRevisionEvents.Clear();
            _baselineRevision = null;
            _baselineRevisionPeers.Clear();
        }

        private bool RestoreBaselineRevision(out string diagnostic)
        {
            var revisions = new List<CharacterTestRuntimeAiLabAppliedRevision>(
                1 + _baselineRevisionPeers.Count);
            if (_baselineRevision != null)
                revisions.Add(_baselineRevision);
            revisions.AddRange(_baselineRevisionPeers);
            var installed = _context?.Slice?.RuntimeAiPlannerCommands?.CaptureReplayOverlayInstallRequests();
            if (installed != null && installed.Length == revisions.Count && revisions.Count > 0)
            {
                bool same = true;
                foreach (var baseline in revisions)
                {
                    bool found = false;
                    var expected = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(baseline.ProfileSlot,
                        baseline.Profile, baseline.Revision.LogicPrograms, baseline.AuthorityHash);
                    foreach (var actual in installed) found |= IsSameReplayBinding(actual, expected);
                    same &= found;
                }
                if (same)
                {
                    // Warm restore will restore the authoritative state next. Reinstalling these exact
                    // immutable programs would unnecessarily replace an active host before its checkpoint.
                    _activeRevision = revisions[0].Revision; diagnostic = string.Empty; return true;
                }
            }
            bool tactical = false;
            foreach (var revision in revisions)
                foreach (var program in revision.Revision.LogicPrograms) tactical |= program?.TacticalSource != null;
            if (!tactical) return TryApplyRevisionBatch(revisions, out diagnostic);
            var module = _context?.Slice?.RuntimeAiPlannerCommands;
            module?.SetBootstrapDecisionsSuppressed(true);
            try { return BootstrapTacticalActors(out diagnostic) && TryApplyRevisionBatch(revisions, out diagnostic); }
            finally { module?.SetBootstrapDecisionsSuppressed(false); }
        }

        internal static bool IsSameReplayBinding(CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest actual,
            CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest expected)
        {
            if (actual.ProfileSlot != expected.ProfileSlot || actual.AuthorityHash == 0 || actual.AuthorityHash != expected.AuthorityHash
                || actual.Profile == null || expected.Profile == null || actual.LogicPrograms == null || expected.LogicPrograms == null
                || actual.LogicPrograms.Count != expected.LogicPrograms.Count
                || Newtonsoft.Json.JsonConvert.SerializeObject(actual.Profile)
                    != Newtonsoft.Json.JsonConvert.SerializeObject(expected.Profile)) return false;
            for (int i = 0; i < actual.LogicPrograms.Count; i++)
                if (!ReferenceEquals(actual.LogicPrograms[i], expected.LogicPrograms[i])) return false;
            return true;
        }

        private bool ApplyRevisionEventsForFrame(int frame, out string diagnostic)
        {
            var revisions = new List<CharacterTestRuntimeAiLabAppliedRevision>();
            for (int i = 0; i < _appliedRevisionEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[i];
                if (applied != null && applied.ApplicationFrame == frame)
                    revisions.Add(applied);
            }
            return TryApplyRevisionBatch(revisions, out diagnostic);
        }

        private bool TryApplyRevisionBatch(
            IReadOnlyList<CharacterTestRuntimeAiLabAppliedRevision> revisions,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (revisions == null || revisions.Count == 0)
                return true;

            CharacterTestRuntimeAiPlannerCommandModule module =
                _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands;
            var requests = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest[revisions.Count];
            for (int i = 0; i < revisions.Count; i++)
            {
                CharacterTestRuntimeAiLabAppliedRevision applied = revisions[i];
                if (applied?.Profile == null || applied.Revision == null)
                {
                    diagnostic = "Runtime AI Lab revision replay contains an invalid overlay request.";
                    return false;
                }
                requests[i] = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(
                    applied.ProfileSlot,
                    applied.Profile,
                    applied.Revision.LogicPrograms,
                    applied.AuthorityHash);
            }

            CharacterTestRuntimeAiPlannerBehaviorHotUpdateResult result = default;
            if (module == null
                || !module.TryApplyBehaviorCompositionOverlayBatch(requests, out result))
            {
                diagnostic = result.ReportText ?? "Runtime AI Lab revision replay failed.";
                return false;
            }

            _activeRevision = revisions[0].Revision;
            return true;
        }

        private void TruncateAppliedRevisionEventsAfter(int frame)
        {
            for (int i = _appliedRevisionEvents.Count - 1; i >= 0; i--)
            {
                if (_appliedRevisionEvents[i].ApplicationFrame > frame)
                    _appliedRevisionEvents.RemoveAt(i);
            }
        }
    }

    /// <summary>Immutable RuntimeFrame input used to replay a Runtime AI Lab evaluator revision.</summary>
    /// <remarks>LowFreqAlloc: created only by explicit authoring Apply.</remarks>
    internal sealed class CharacterTestRuntimeAiLabAppliedRevision
    {
        public CharacterTestRuntimeAiLabAppliedRevision(
            int applicationFrame,
            int profileSlot,
            CharacterTestRuntimeAiLabCandidateRevision revision,
            int sessionGeneration = 0)
        {
            ApplicationFrame = Math.Max(0, applicationFrame);
            ProfileSlot = Math.Max(0, profileSlot);
            Revision = revision;
            Profile = revision?.Profile;
            SessionGeneration = Math.Max(0, sessionGeneration);
        }

        public int ApplicationFrame { get; }
        public int ProfileSlot { get; }
        public CharacterTestRuntimeAiLabCandidateRevision Revision { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile { get; }
        public long AuthorityHash => Revision?.AuthorityHash ?? 0L;
        public int SessionGeneration { get; private set; }
        public long BoundaryRecordGeneration { get; private set; }
        public long BoundaryAuthorityHash { get; private set; }
        public long AppliedRevisionHash { get; private set; }
        public bool HasBoundRuntimeFrame => SessionGeneration > 0 && BoundaryRecordGeneration > 0L;

        internal bool TryBindRuntimeFrame(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            long appliedRevisionHash)
        {
            if (!sessionIdentity.IsValid || record == null || record.Frame != ApplicationFrame)
                return false;
            if (SessionGeneration != 0 && SessionGeneration != sessionIdentity.Generation)
                return false;
            if (HasBoundRuntimeFrame)
            {
                return SessionGeneration == sessionIdentity.Generation
                    && BoundaryRecordGeneration == record.RecordGeneration
                    && BoundaryAuthorityHash == record.StateHash
                    && AppliedRevisionHash == appliedRevisionHash;
            }

            SessionGeneration = sessionIdentity.Generation;
            BoundaryRecordGeneration = record.RecordGeneration;
            BoundaryAuthorityHash = record.StateHash;
            AppliedRevisionHash = appliedRevisionHash;
            return true;
        }

        internal bool MatchesRuntimeFrame(
            CharacterTestRuntimeAiLabTimelineSessionIdentity sessionIdentity,
            CharacterTestRuntimeAiPlannerDevTimelineRecord record,
            long appliedRevisionHash)
        {
            return HasBoundRuntimeFrame
                && sessionIdentity.IsValid
                && record != null
                && record.Frame == ApplicationFrame
                && SessionGeneration == sessionIdentity.Generation
                && BoundaryRecordGeneration == record.RecordGeneration
                && BoundaryAuthorityHash == record.StateHash
                && AppliedRevisionHash == appliedRevisionHash;
        }
    }
}
