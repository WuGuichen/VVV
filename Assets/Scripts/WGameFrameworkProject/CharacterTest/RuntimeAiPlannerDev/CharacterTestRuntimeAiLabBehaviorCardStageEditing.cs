using System;

namespace MxFramework.Game.CharacterTest
{
    [Flags]
    internal enum CharacterTestRuntimeAiLabStageSelectionModifier
    {
        None = 0,
        AddRange = 1 << 0,
        Toggle = 1 << 1
    }

    internal enum CharacterTestRuntimeAiLabStageGestureKind
    {
        None = 0,
        Move = 1,
        Resize = 2,
        Marquee = 3
    }

    internal readonly struct CharacterTestRuntimeAiLabStageEditIdentity
    {
        internal CharacterTestRuntimeAiLabStageEditIdentity(
            int authorityGeneration,
            string workspaceId,
            string groupDraftId,
            string entryId,
            string cardDraftId,
            int draftRevision)
        {
            AuthorityGeneration = authorityGeneration;
            WorkspaceId = Normalize(workspaceId);
            GroupDraftId = Normalize(groupDraftId);
            EntryId = Normalize(entryId);
            CardDraftId = Normalize(cardDraftId);
            DraftRevision = draftRevision;
        }

        internal int AuthorityGeneration { get; }
        internal string WorkspaceId { get; }
        internal string GroupDraftId { get; }
        internal string EntryId { get; }
        internal string CardDraftId { get; }
        internal int DraftRevision { get; }

        internal bool IsValid => AuthorityGeneration > 0
            && !string.IsNullOrEmpty(WorkspaceId)
            && !string.IsNullOrEmpty(GroupDraftId)
            && !string.IsNullOrEmpty(EntryId)
            && !string.IsNullOrEmpty(CardDraftId)
            && DraftRevision > 0;

        internal bool Matches(in CharacterTestRuntimeAiLabStageEditIdentity other)
        {
            return AuthorityGeneration == other.AuthorityGeneration
                && DraftRevision == other.DraftRevision
                && string.Equals(WorkspaceId, other.WorkspaceId, StringComparison.Ordinal)
                && string.Equals(GroupDraftId, other.GroupDraftId, StringComparison.Ordinal)
                && string.Equals(EntryId, other.EntryId, StringComparison.Ordinal)
                && string.Equals(CardDraftId, other.CardDraftId, StringComparison.Ordinal);
        }

        internal bool MatchesRoute(in CharacterTestRuntimeAiLabStageEditIdentity other)
        {
            return AuthorityGeneration == other.AuthorityGeneration
                && string.Equals(WorkspaceId, other.WorkspaceId, StringComparison.Ordinal)
                && string.Equals(GroupDraftId, other.GroupDraftId, StringComparison.Ordinal)
                && string.Equals(EntryId, other.EntryId, StringComparison.Ordinal);
        }

        private static string Normalize(string value) => value?.Trim() ?? string.Empty;
    }

    internal sealed class CharacterTestRuntimeAiLabStageSelectionState
    {
        private string[] _selectedIds = Array.Empty<string>();
        private string[] _selectionScratch = Array.Empty<string>();
        private int[] _orderedStageIndices = Array.Empty<int>();
        private int _selectedCount;
        private CharacterTestRuntimeAiLabStageEditIdentity _identity;

        internal int Count => _selectedCount;
        internal string PrimaryStageId { get; private set; } = string.Empty;
        internal string RangeAnchorStageId { get; private set; } = string.Empty;
        internal string ExpandedStageId { get; private set; } = string.Empty;
        internal long Revision { get; private set; }

        internal string GetSelectedId(int index)
        {
            return index >= 0 && index < _selectedCount ? _selectedIds[index] : string.Empty;
        }

        internal bool IsSelected(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
                return false;
            for (int i = 0; i < _selectedCount; i++)
            {
                if (string.Equals(_selectedIds[i], stageId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal void Clear()
        {
            bool hadState = _identity.IsValid
                || _selectedCount > 0
                || !string.IsNullOrEmpty(PrimaryStageId)
                || !string.IsNullOrEmpty(RangeAnchorStageId)
                || !string.IsNullOrEmpty(ExpandedStageId);
            if (!hadState)
                return;

            ClearIds();
            _identity = default;
            Revision++;
        }

        internal void Reconcile(
            in CharacterTestRuntimeAiLabStageEditIdentity identity,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            int previousCount = _selectedCount;
            string previousPrimary = PrimaryStageId;
            string previousAnchor = RangeAnchorStageId;
            string previousExpanded = ExpandedStageId;
            CopySelectionToScratch();
            if (!_identity.IsValid || !_identity.MatchesRoute(identity))
            {
                ClearIds();
            }
            else
            {
                BuildStableOrder(stages, count);
                int write = 0;
                for (int rank = 0; rank < count; rank++)
                {
                    int stageIndex = _orderedStageIndices[rank];
                    string stageId = stages[stageIndex]?.StageId ?? string.Empty;
                    if (ScratchContains(stageId))
                    {
                        EnsureCapacity(write + 1);
                        _selectedIds[write++] = stageId;
                    }
                }
                for (int i = write; i < _selectedCount; i++)
                    _selectedIds[i] = string.Empty;
                _selectedCount = write;
                if (!IsSelected(PrimaryStageId))
                    PrimaryStageId = write > 0 ? _selectedIds[0] : string.Empty;
                if (!ContainsStage(stages, count, RangeAnchorStageId))
                    RangeAnchorStageId = PrimaryStageId;
                if (!IsSelected(ExpandedStageId))
                    ExpandedStageId = string.Empty;
            }
            _identity = identity;
            if (previousCount != _selectedCount
                || !string.Equals(previousPrimary, PrimaryStageId, StringComparison.Ordinal)
                || !string.Equals(previousAnchor, RangeAnchorStageId, StringComparison.Ordinal)
                || !string.Equals(previousExpanded, ExpandedStageId, StringComparison.Ordinal)
                || !MatchesSelectionScratch(previousCount))
            {
                Revision++;
            }
        }

        internal bool Select(
            string stageId,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            int hitIndex = FindStage(stages, count, stageId);
            if (hitIndex < 0)
                return false;

            if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.Toggle) != 0)
            {
                Toggle(stageId, stages, count);
            }
            else if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.AddRange) != 0)
            {
                BuildStableOrder(stages, count);
                int hitRank = FindOrderedRank(stages, count, stageId);
                int anchorRank = FindOrderedRank(stages, count, RangeAnchorStageId);
                if (anchorRank < 0)
                    anchorRank = hitRank;
                int first = Math.Min(anchorRank, hitRank);
                int last = Math.Max(anchorRank, hitRank);
                EnsureCapacity(last - first + 1);
                ClearSelectedStorage();
                for (int rank = first; rank <= last; rank++)
                {
                    int stageIndex = _orderedStageIndices[rank];
                    _selectedIds[_selectedCount++] = stages[stageIndex]?.StageId ?? string.Empty;
                }
                PrimaryStageId = stageId;
                if (RangeAnchorStageId.Length == 0)
                    RangeAnchorStageId = stageId;
            }
            else
            {
                EnsureCapacity(1);
                ClearSelectedStorage();
                _selectedIds[0] = stageId;
                _selectedCount = 1;
                PrimaryStageId = stageId;
                RangeAnchorStageId = stageId;
            }

            if (!IsSelected(ExpandedStageId))
                ExpandedStageId = string.Empty;
            Revision++;
            return true;
        }

        internal bool ReplaceFromOrderedMask(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            bool[] hitMask,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier)
        {
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            if (hitMask == null || hitMask.Length < count)
                return false;
            EnsureCapacity(count);
            if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.Toggle) == 0
                && (modifier & CharacterTestRuntimeAiLabStageSelectionModifier.AddRange) == 0)
            {
                ClearSelectedStorage();
            }
            for (int i = 0; i < count; i++)
            {
                if (!hitMask[i])
                    continue;
                string stageId = stages[i]?.StageId ?? string.Empty;
                if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.Toggle) != 0)
                {
                    ToggleRaw(stageId);
                }
                else if (!IsSelected(stageId))
                {
                    _selectedIds[_selectedCount++] = stageId;
                }
            }
            NormalizeToStageOrder(stages, count);
            PrimaryStageId = _selectedCount > 0 ? _selectedIds[0] : string.Empty;
            if (!ContainsStage(stages, count, RangeAnchorStageId))
                RangeAnchorStageId = PrimaryStageId;
            if (!IsSelected(ExpandedStageId))
                ExpandedStageId = string.Empty;
            Revision++;
            return true;
        }

        internal bool ReplaceFromStableIds(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string[] stageIds,
            int selectedCount,
            CharacterTestRuntimeAiLabStageSelectionModifier modifier)
        {
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            int idsCount = Math.Max(0, Math.Min(selectedCount, stageIds?.Length ?? 0));
            if (stageIds == null || idsCount != selectedCount)
                return false;
            for (int i = 0; i < idsCount; i++)
            {
                if (FindStage(stages, count, stageIds[i]) < 0)
                    return false;
                for (int previous = 0; previous < i; previous++)
                {
                    if (string.Equals(stageIds[previous], stageIds[i], StringComparison.Ordinal))
                        return false;
                }
            }
            EnsureCapacity(Math.Max(count, _selectedCount + idsCount));
            if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.Toggle) == 0
                && (modifier & CharacterTestRuntimeAiLabStageSelectionModifier.AddRange) == 0)
            {
                ClearSelectedStorage();
            }
            for (int i = 0; i < idsCount; i++)
            {
                string stageId = stageIds[i] ?? string.Empty;
                if ((modifier & CharacterTestRuntimeAiLabStageSelectionModifier.Toggle) != 0)
                    ToggleRaw(stageId);
                else if (!IsSelected(stageId))
                    _selectedIds[_selectedCount++] = stageId;
            }
            NormalizeToStageOrder(stages, count);
            PrimaryStageId = _selectedCount > 0 ? _selectedIds[0] : string.Empty;
            if (!ContainsStage(stages, count, RangeAnchorStageId))
                RangeAnchorStageId = PrimaryStageId;
            if (!IsSelected(ExpandedStageId))
                ExpandedStageId = string.Empty;
            Revision++;
            return true;
        }

        internal bool ToggleExpanded(string stageId)
        {
            if (!IsSelected(stageId))
                return false;
            ExpandedStageId = string.Equals(ExpandedStageId, stageId, StringComparison.Ordinal)
                ? string.Empty
                : stageId;
            PrimaryStageId = stageId;
            Revision++;
            return true;
        }

        private void Toggle(
            string stageId,
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            string previousPrimary = PrimaryStageId;
            ToggleRaw(stageId);
            NormalizeToStageOrder(stages, stageCount);
            PrimaryStageId = IsSelected(stageId)
                ? stageId
                : IsSelected(previousPrimary)
                    ? previousPrimary
                : (_selectedCount > 0 ? _selectedIds[0] : string.Empty);
            RangeAnchorStageId = stageId;
        }

        private void ToggleRaw(string stageId)
        {
            for (int i = 0; i < _selectedCount; i++)
            {
                if (!string.Equals(_selectedIds[i], stageId, StringComparison.Ordinal))
                    continue;
                for (int move = i + 1; move < _selectedCount; move++)
                    _selectedIds[move - 1] = _selectedIds[move];
                _selectedIds[--_selectedCount] = string.Empty;
                return;
            }
            EnsureCapacity(_selectedCount + 1);
            _selectedIds[_selectedCount++] = stageId;
        }

        private void NormalizeToStageOrder(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            CopySelectionToScratch();
            BuildStableOrder(stages, stageCount);
            int write = 0;
            for (int rank = 0; rank < stageCount; rank++)
            {
                int stageIndex = _orderedStageIndices[rank];
                string stageId = stages[stageIndex]?.StageId ?? string.Empty;
                if (ScratchContains(stageId))
                    _selectedIds[write++] = stageId;
            }
            for (int i = write; i < _selectedCount; i++)
                _selectedIds[i] = string.Empty;
            _selectedCount = write;
        }

        private void ClearIds()
        {
            ClearSelectedStorage();
            PrimaryStageId = string.Empty;
            RangeAnchorStageId = string.Empty;
            ExpandedStageId = string.Empty;
        }

        private void ClearSelectedStorage()
        {
            for (int i = 0; i < _selectedCount; i++)
                _selectedIds[i] = string.Empty;
            _selectedCount = 0;
        }

        private void EnsureCapacity(int count)
        {
            if (_selectedIds.Length >= count)
                return;
            int capacity = Math.Max(count, Math.Max(8, _selectedIds.Length * 2));
            Array.Resize(ref _selectedIds, capacity);
            Array.Resize(ref _selectionScratch, capacity);
            Array.Resize(ref _orderedStageIndices, capacity);
        }

        private void BuildStableOrder(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount)
        {
            int count = Math.Max(0, Math.Min(stageCount, stages?.Length ?? 0));
            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                int insert = i;
                while (insert > 0
                    && CompareStageOrder(stages, i, _orderedStageIndices[insert - 1]) < 0)
                {
                    _orderedStageIndices[insert] = _orderedStageIndices[insert - 1];
                    insert--;
                }
                _orderedStageIndices[insert] = i;
            }
        }

        private int FindOrderedRank(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string stageId)
        {
            BuildStableOrder(stages, stageCount);
            for (int rank = 0; rank < stageCount; rank++)
            {
                int stageIndex = _orderedStageIndices[rank];
                if (string.Equals(stages[stageIndex]?.StageId, stageId, StringComparison.Ordinal))
                    return rank;
            }
            return -1;
        }

        private static int CompareStageOrder(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int leftIndex,
            int rightIndex)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel left = stages[leftIndex];
            CharacterTestRuntimeAiLabLogicStageViewModel right = stages[rightIndex];
            if (ReferenceEquals(left, right))
                return leftIndex.CompareTo(rightIndex);
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;
            int stableId = string.CompareOrdinal(left.StageId, right.StageId);
            return stableId != 0 ? stableId : leftIndex.CompareTo(rightIndex);
        }

        private void CopySelectionToScratch()
        {
            if (_selectionScratch.Length < _selectedIds.Length)
                Array.Resize(ref _selectionScratch, _selectedIds.Length);
            for (int i = 0; i < _selectedCount; i++)
                _selectionScratch[i] = _selectedIds[i];
        }

        private bool ScratchContains(string stageId)
        {
            for (int i = 0; i < _selectedCount; i++)
            {
                if (string.Equals(_selectionScratch[i], stageId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private bool MatchesSelectionScratch(int previousCount)
        {
            if (previousCount != _selectedCount)
                return false;
            for (int i = 0; i < previousCount; i++)
            {
                if (!string.Equals(_selectionScratch[i], _selectedIds[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static int FindStage(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string stageId)
        {
            for (int i = 0; i < stageCount; i++)
            {
                if (string.Equals(stages[i]?.StageId, stageId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static bool ContainsStage(
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages,
            int stageCount,
            string stageId)
        {
            return stageId.Length > 0 && FindStage(stages, stageCount, stageId) >= 0;
        }
    }

}
