using System;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private int _stageEditAuthorityGeneration = 1;

        internal int StageEditAuthorityGeneration => _stageEditAuthorityGeneration;

        internal void InvalidateStageEditAuthority()
        {
            CancelQueuedApply(
                CharacterTestRuntimeAiLabQueuedApplyCancelReason.WorkspaceAuthorityChanged,
                "Apply F+1 已取消：行为卡工作区 authority 已替换。");
            _stageEditAuthorityGeneration = _stageEditAuthorityGeneration == int.MaxValue
                ? 1
                : _stageEditAuthorityGeneration + 1;
        }

        internal bool TryResolveStageEditIdentity(
            int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity,
            out string message)
        {
            identity = default;
            if (!TryGetCard(
                    entryIndex,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card,
                    out message))
            {
                return false;
            }
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                message = "Behavior card route is unavailable.";
                return false;
            }
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[entryIndex];
            identity = new CharacterTestRuntimeAiLabStageEditIdentity(
                _stageEditAuthorityGeneration,
                DraftWorkspace.WorkspaceId,
                group.GroupDraftId,
                entry.EntryId,
                card.CardDraftId,
                DraftWorkspace.CurrentRevision);
            if (!identity.IsValid)
            {
                message = "Behavior card edit identity is incomplete.";
                return false;
            }
            message = string.Empty;
            return true;
        }

        internal bool TryResolveStageEditIdentityByEntryId(
            string entryId,
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity,
            out string message)
        {
            entryIndex = -1;
            identity = default;
            if (string.IsNullOrWhiteSpace(entryId)
                || !DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                message = "Behavior entry route is unavailable.";
                return false;
            }
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry != null && string.Equals(entry.EntryId, entryId, StringComparison.Ordinal))
                {
                    entryIndex = i;
                    return TryResolveStageEditIdentity(entryIndex, out identity, out message);
                }
            }
            message = "Behavior entry route no longer exists.";
            return false;
        }

        internal bool TryMoveLogicStages(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string[] selectedStageIds,
            int selectedCount,
            int requestedDelta,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryMoveStages(
                    payload,
                    selectedStageIds,
                    selectedCount,
                    requestedDelta,
                    out _,
                    out message))
            {
                return false;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "移动 Stage",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryResizeLogicStage(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string stageId,
            int requestedDelta,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryResizeStage(
                    payload,
                    stageId,
                    requestedDelta,
                    out _,
                    out message))
            {
                return false;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "调整 Stage 持续时间",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryEditLogicStageField(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string stageId,
            CharacterTestRuntimeAiLabStageFieldKind field,
            in CharacterTestRuntimeAiLabStageFieldValue value,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                    payload,
                    stageId,
                    field,
                    value,
                    out bool changed,
                    out message))
            {
                return false;
            }
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "编辑 Stage " + field,
                out resultingCardDraftId,
                out message);
        }

        internal bool TryEditLogicStageKind(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string stageId,
            CharacterTestRuntimeAiLabCardStepKind kind,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryApplyField(
                    payload,
                    stageId,
                    CharacterTestRuntimeAiLabStageFieldKind.Kind,
                    new CharacterTestRuntimeAiLabStageFieldValue(stageKind: kind),
                    out bool changed,
                    out message))
            {
                return false;
            }
            if (!changed)
            {
                resultingCardDraftId = card.CardDraftId;
                return true;
            }
            CharacterTestRuntimeAiLabLogicStage stage = null;
            for (int i = 0; i < payload.Stages.Count; i++)
            {
                if (string.Equals(payload.Stages[i]?.StageId, stageId, StringComparison.Ordinal))
                {
                    stage = payload.Stages[i];
                    break;
                }
            }
            if (stage == null)
            {
                message = "Stage stable ID is unavailable.";
                return false;
            }
            if (kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility && stage.AbilityId <= 0)
                stage.AbilityId = CharacterTestAbilityIds.DashStrikeAbilityId;
            else if (kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction
                && !stage.CharacterActionId.IsValid)
            {
                stage.CharacterActionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
            }
            return TryCommitStageMutation(
                entryIndex,
                payload,
                "编辑 Stage Kind",
                out resultingCardDraftId,
                out message);
        }

        internal bool TryDuplicateLogicStages(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string[] selectedStageIds,
            int selectedCount,
            out string[] createdStageIds,
            out string resultingCardDraftId,
            out string message)
        {
            createdStageIds = Array.Empty<string>();
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDuplicate(
                    payload,
                    selectedStageIds,
                    selectedCount,
                    out createdStageIds,
                    out message))
            {
                return false;
            }
            if (TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "复制 Stage",
                    out resultingCardDraftId,
                    out message))
            {
                return true;
            }
            createdStageIds = Array.Empty<string>();
            return false;
        }

        internal bool TryDeleteLogicStages(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            string[] selectedStageIds,
            int selectedCount,
            out string focusStageId,
            out string resultingCardDraftId,
            out string message)
        {
            focusStageId = string.Empty;
            resultingCardDraftId = string.Empty;
            if (!TryBeginStageMutation(entryIndex, expectedIdentity, out var card, out message))
                return false;
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload = card.LogicPayload?.Clone();
            if (!CharacterTestRuntimeAiLabBehaviorCardStageMutation.TryDelete(
                    payload,
                    selectedStageIds,
                    selectedCount,
                    out focusStageId,
                    out message))
            {
                return false;
            }
            if (TryCommitStageMutation(
                    entryIndex,
                    payload,
                    "删除 Stage",
                    out resultingCardDraftId,
                    out message))
            {
                return true;
            }
            focusStageId = string.Empty;
            return false;
        }

        internal bool TryUndoBehaviorCardEdit(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out CharacterTestRuntimeAiLabStageEditIdentity resultingIdentity,
            out string message)
        {
            return TryUndoBehaviorCardEdit(
                entryIndex,
                expectedIdentity,
                out _,
                out resultingIdentity,
                out message);
        }

        internal bool TryUndoBehaviorCardEdit(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out int resultingEntryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity resultingIdentity,
            out string message)
        {
            resultingEntryIndex = -1;
            resultingIdentity = default;
            if (!TryBeginHistoryMutationByRoute(expectedIdentity, out _, out message))
                return false;
            if (!DraftWorkspace.TryUndo(out message))
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            if (!TryResolveStageEditIdentityByRouteCore(
                    expectedIdentity.GroupDraftId,
                    expectedIdentity.EntryId,
                    out resultingEntryIndex,
                    out resultingIdentity,
                    out _))
            {
                message = "历史已恢复；原行为卡条目已不存在。";
            }
            return true;
        }

        internal bool TryRedoBehaviorCardEdit(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out CharacterTestRuntimeAiLabStageEditIdentity resultingIdentity,
            out string message)
        {
            return TryRedoBehaviorCardEdit(
                entryIndex,
                expectedIdentity,
                out _,
                out resultingIdentity,
                out message);
        }

        internal bool TryRedoBehaviorCardEdit(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out int resultingEntryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity resultingIdentity,
            out string message)
        {
            resultingEntryIndex = -1;
            resultingIdentity = default;
            if (!TryBeginHistoryMutationByRoute(expectedIdentity, out _, out message))
                return false;
            if (!DraftWorkspace.TryRedo(out message))
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            if (!TryResolveStageEditIdentityByRouteCore(
                    expectedIdentity.GroupDraftId,
                    expectedIdentity.EntryId,
                    out resultingEntryIndex,
                    out resultingIdentity,
                    out _))
            {
                message = "历史已重做；原行为卡条目已不存在。";
            }
            return true;
        }

        internal bool TryResolveStageEditIdentityByRoute(
            in CharacterTestRuntimeAiLabStageEditIdentity expectedRoute,
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity,
            out string message)
        {
            entryIndex = -1;
            identity = default;
            message = string.Empty;
            if (!expectedRoute.IsValid
                || !TryResolveStageEditIdentityByRouteCore(
                    expectedRoute.GroupDraftId,
                    expectedRoute.EntryId,
                    out entryIndex,
                    out identity,
                    out message)
                || !identity.MatchesRoute(expectedRoute)
                || !string.Equals(
                    identity.CardDraftId,
                    expectedRoute.CardDraftId,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(message))
                    message = "行为卡历史路由已经失效。";
                entryIndex = -1;
                identity = default;
                return false;
            }
            return true;
        }

        private bool TryResolveStageEditIdentityByRouteCore(
            string groupDraftId,
            string entryId,
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity,
            out string message)
        {
            entryIndex = -1;
            identity = default;
            if (!DraftWorkspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || !string.Equals(group.GroupDraftId, groupDraftId, StringComparison.Ordinal))
            {
                message = "Behavior group route no longer exists after history restore.";
                return false;
            }
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry != null && string.Equals(entry.EntryId, entryId, StringComparison.Ordinal))
                {
                    entryIndex = i;
                    break;
                }
            }
            if (entryIndex < 0)
            {
                message = "Behavior entry route no longer exists after history restore.";
                return false;
            }
            return TryResolveStageEditIdentity(entryIndex, out identity, out message);
        }

        private bool TryBeginHistoryMutationByRoute(
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out int currentEntryIndex,
            out string message)
        {
            currentEntryIndex = -1;
            message = string.Empty;
            if (!TryResolveStageEditIdentityByRoute(
                    expectedIdentity,
                    out currentEntryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity currentIdentity,
                    out message)
                || !currentIdentity.Matches(expectedIdentity))
            {
                if (string.IsNullOrEmpty(message))
                    message = "行为卡历史身份已经失效。";
                return false;
            }
            return TryBeginHistoryMutation(currentEntryIndex, currentIdentity, out message);
        }

        private bool TryBeginStageMutation(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out CharacterTestRuntimeAiLabBehaviorCardDraft card,
            out string message,
            bool allowWindowCanonicalizationRequired = false)
        {
            card = null;
            if (_candidateApplyQueued)
            {
                message = "Apply F+1 已排队；处理完成前不能修改草稿。";
                return false;
            }
            if (!CanEditBehaviorCardDraft)
            {
                message = "行为卡只能在配置阶段或暂停的最新帧编辑。";
                return false;
            }
            if (!TryResolveStageEditIdentity(entryIndex, out var current, out message)
                || !current.Matches(expectedIdentity))
            {
                if (string.IsNullOrEmpty(message))
                    message = "行为卡编辑身份已失效。";
                return false;
            }
            if (!TryGetCard(entryIndex, out card, out message))
                return false;
            if (!allowWindowCanonicalizationRequired
                && CharacterTestRuntimeAiLabBehaviorCardGraphMutation.RequiresWindowCanonicalization(
                    card.LogicPayload))
            {
                message = "DETECTOR_WINDOW_CANONICALIZATION_REQUIRED: confirm the one-time Detector window migration before editing or applying.";
                card = null;
                return false;
            }
            return true;
        }

        private bool TryBeginHistoryMutation(
            int entryIndex,
            in CharacterTestRuntimeAiLabStageEditIdentity expectedIdentity,
            out string message)
        {
            if (!TryBeginStageMutation(
                    entryIndex,
                    expectedIdentity,
                    out _,
                    out message,
                    allowWindowCanonicalizationRequired: true))
                return false;
            return true;
        }

        private bool TryCommitStageMutation(
            int entryIndex,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string operationLabel,
            out string resultingCardDraftId,
            out string message)
        {
            resultingCardDraftId = string.Empty;
            bool changed = DraftWorkspace.TryReplaceCardLogicPayload(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                payload,
                operationLabel,
                out message);
            if (!changed)
                return false;
            _draftPersistence.MarkDirty();
            BuildCandidateRevision();
            if (TryGetCard(entryIndex, out var resultingCard, out _))
                resultingCardDraftId = resultingCard.CardDraftId;
            return true;
        }
    }
}
