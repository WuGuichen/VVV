using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabIdentityResolutionStatus
    {
        NotApplicable = 0,
        Resolved = 1,
        Unresolved = 2
    }

    internal enum CharacterTestRuntimeAiLabIdentityResolutionReason
    {
        None = 0,
        NoRuntimeState = 1,
        LogicSlotMissing = 2,
        LogicSlotDuplicate = 3,
        AgentMissing = 4,
        AgentDuplicate = 5,
        AppliedRevisionMissing = 6,
        RevisionIdentityInvalid = 7,
        BindingIndexOutOfRange = 8,
        ProgramSemanticMismatch = 9,
        StageIndexOutOfRange = 10,
        PrimaryActionStageIndexOutOfRange = 11,
        PrimaryActionStageInvalid = 12,
        PrimaryActionStateInvalid = 13,
        PublishedStableIdNonCanonical = 14,
        EditEntryMissing = 15,
        EditCardMissing = 16,
        RuntimeStateInvalid = 17
    }

    internal enum CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship
    {
        DesignOnly = 0,
        SameCardSameSemantic = 1,
        SameCardDifferentSemantic = 2,
        DifferentCard = 3,
        DifferentRuntimeBindingSamePublishedCard = 4,
        TransitioningToDifferentCard = 5,
        TransitioningBetweenBindingsSamePublishedCard = 6
    }

    /// <summary>Immutable presentation identity captured beside one compiled behavior-card binding.</summary>
    /// <remarks>LowFreqAlloc: created only during explicit Runtime AI Lab candidate compilation.</remarks>
    internal sealed class CharacterTestRuntimeAiLabRevisionCardIdentity
    {
        private CharacterTestRuntimeAiLabRevisionCardIdentity(
            int bindingIndex,
            string runtimeEntryId,
            string draftEntryId,
            string cardDraftId,
            bool hasPublishedSource,
            string publishedStableId,
            string legacySourceKey,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind coreSlotKind,
            int sourcePublishedRevision,
            long sourcePublishedSemanticHash,
            long sourcePublishedContentHash,
            long candidateSemanticHash)
        {
            BindingIndex = bindingIndex;
            RuntimeEntryId = runtimeEntryId ?? string.Empty;
            DraftEntryId = draftEntryId ?? string.Empty;
            CardDraftId = cardDraftId ?? string.Empty;
            HasPublishedSource = hasPublishedSource;
            PublishedStableId = publishedStableId ?? string.Empty;
            LegacySourceKey = legacySourceKey ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            CoreSlotKind = coreSlotKind;
            SourcePublishedRevision = sourcePublishedRevision;
            SourcePublishedSemanticHash = sourcePublishedSemanticHash;
            SourcePublishedContentHash = sourcePublishedContentHash;
            CandidateSemanticHash = candidateSemanticHash;
        }

        public int BindingIndex { get; }
        public string RuntimeEntryId { get; }
        public string DraftEntryId { get; }
        public string CardDraftId { get; }
        public bool HasPublishedSource { get; }
        public string PublishedStableId { get; }
        public string LegacySourceKey { get; }
        public string DisplayName { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind CoreSlotKind { get; }
        public int SourcePublishedRevision { get; }
        public long SourcePublishedSemanticHash { get; }
        public long SourcePublishedContentHash { get; }
        public long CandidateSemanticHash { get; }
        internal CharacterTestRuntimeAiLabRevisionCardIdentity WithProgramHash(long hash)
            => new CharacterTestRuntimeAiLabRevisionCardIdentity(BindingIndex, RuntimeEntryId, DraftEntryId, CardDraftId,
                HasPublishedSource, PublishedStableId, LegacySourceKey, DisplayName, CoreSlotKind,
                SourcePublishedRevision, SourcePublishedSemanticHash, SourcePublishedContentHash, hash);

        /// <remarks>LowFreqAlloc: candidate compilation boundary only.</remarks>
        public static bool TryCreate(
            int bindingIndex,
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            out CharacterTestRuntimeAiLabRevisionCardIdentity identity,
            out string diagnostic)
        {
            identity = null;
            diagnostic = string.Empty;
            if (bindingIndex < 0 || entry == null || card == null || program == null)
            {
                diagnostic = "Runtime AI Lab candidate identity input is incomplete.";
                return false;
            }

            string runtimeEntryId = entry.RuntimeEntry?.EntryId ?? string.Empty;
            if (runtimeEntryId.Length == 0
                || !string.Equals(runtimeEntryId, entry.EntryId, StringComparison.Ordinal)
                || !string.Equals(entry.CardDraftId, card.CardDraftId, StringComparison.Ordinal))
            {
                diagnostic = "Runtime AI Lab candidate identity does not align with its draft entry.";
                return false;
            }

            bool hasPublishedSource = card.HasPublishedSource;
            string publishedStableId = string.Empty;
            string legacySourceKey = string.Empty;
            if (hasPublishedSource)
            {
                publishedStableId = card.PublishedStableId ?? string.Empty;
                if (!string.Equals(publishedStableId, publishedStableId.Trim(), StringComparison.Ordinal)
                    || !CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(
                        publishedStableId))
                {
                    diagnostic = "Published behavior-card source identity is not canonical.";
                    return false;
                }
            }
            else
            {
                legacySourceKey = entry.BehaviorId ?? string.Empty;
            }

            identity = new CharacterTestRuntimeAiLabRevisionCardIdentity(
                bindingIndex,
                runtimeEntryId,
                entry.EntryId,
                card.CardDraftId,
                hasPublishedSource,
                publishedStableId,
                legacySourceKey,
                string.IsNullOrWhiteSpace(card.DisplayName) ? "未命名行为卡" : card.DisplayName,
                entry.CoreSlotKind,
                hasPublishedSource ? card.SourcePublishedRevision : 0,
                hasPublishedSource ? card.SourcePublishedSemanticHash : 0L,
                hasPublishedSource ? card.SourcePublishedContentHash : 0L,
                program.SemanticHash);
            return true;
        }
    }

    internal sealed class CharacterTestRuntimeAiLabEditCardIdentity
    {
        public string DraftEntryId { get; internal set; } = string.Empty;
        public string CardDraftId { get; internal set; } = string.Empty;
        public bool HasPublishedSource { get; internal set; }
        public string PublishedStableId { get; internal set; } = string.Empty;
        public string LegacySourceKey { get; internal set; } = string.Empty;
        public string DisplayName { get; internal set; } = string.Empty;
        public int DraftRevision { get; internal set; }
        public int SourcePublishedRevision { get; internal set; }
        public long SourcePublishedSemanticHash { get; internal set; }
        public long SourcePublishedContentHash { get; internal set; }
        public long CurrentSemanticHash { get; internal set; }
    }

    internal sealed class CharacterTestRuntimeAiLabPrimaryActionIdentity
    {
        public bool Available { get; internal set; }
        public int StageIndex { get; internal set; } = -1;
        public string StageId { get; internal set; } = string.Empty;
        public string StageTitle { get; internal set; } = string.Empty;
        public CharacterTestRuntimeAiLabCardStepKind StageKind { get; internal set; }
        public int AbilityId { get; internal set; }
        public MxFramework.CharacterAction.CharacterActionId CharacterActionId { get; internal set; }
        public CharacterTestRuntimeAiPlannerActionKind LegacyActionKind { get; internal set; }
        public CharacterTestRuntimeAiLabMovementStrategy MovementStrategy { get; internal set; }
        public CharacterTestRuntimeAiLabLogicActionState State { get; internal set; }
        public bool CommandPending { get; internal set; }
        public bool CommandRejected { get; internal set; }
        public bool ActionObserved { get; internal set; }
        public bool CancelRequested { get; internal set; }
        public bool ClearControlPending { get; internal set; }
        public string DisplayName { get; internal set; } = "无主动作";
    }

    internal sealed class CharacterTestRuntimeAiLabCompositionActionDiagnostic
    {
        public CharacterTestRuntimeAiPlannerActionKind Kind { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
    {
        public CharacterTestRuntimeAiLabIdentityResolutionStatus Status { get; internal set; }
        public CharacterTestRuntimeAiLabIdentityResolutionReason Reason { get; internal set; }
        public string Diagnostic { get; internal set; } = string.Empty;
        public int SelectedEntityId { get; internal set; }
        public int Frame { get; internal set; }
        public int ProfileSlot { get; internal set; }
        public long AuthorityHash { get; internal set; }
        public CharacterTestRuntimeAiLabCandidateRevision Revision { get; internal set; }
        public CharacterTestRuntimeAiLabLogicAgentStateSnapshot LogicAgent { get; internal set; }
        public CharacterTestRuntimeAiLabRevisionCardIdentity CommittedCard { get; internal set; }
        public CharacterTestRuntimeAiLabRevisionCardIdentity PendingCard { get; internal set; }
        public CharacterTestRuntimeAiLabCompiledLogicProgram CommittedProgram { get; internal set; }
        public CharacterTestRuntimeAiLabLogicRuntimeState RuntimeState { get; internal set; }
        public bool BindingReleasePending { get; internal set; }
        public string CurrentStageId { get; internal set; } = string.Empty;
        public string CurrentStageTitle { get; internal set; } = string.Empty;
        public CharacterTestRuntimeAiLabPrimaryActionIdentity PrimaryAction { get; internal set; } =
            new CharacterTestRuntimeAiLabPrimaryActionIdentity();
        public bool HasCommittedCard => CommittedCard != null;
        public bool HasPendingCard => PendingCard != null;
    }

    internal sealed class CharacterTestRuntimeAiLabBehaviorCardIdentityProjection
    {
        public CharacterTestRuntimeAiLabIdentityResolutionStatus EditResolutionStatus { get; internal set; }
        public CharacterTestRuntimeAiLabIdentityResolutionReason EditResolutionReason { get; internal set; }
        public string EditDiagnostic { get; internal set; } = string.Empty;
        public CharacterTestRuntimeAiLabEditCardIdentity EditCard { get; internal set; } =
            new CharacterTestRuntimeAiLabEditCardIdentity();
        public CharacterTestRuntimeAiLabRevisionCardIdentity CandidateCard { get; internal set; }
        public int CandidateRevision { get; internal set; }
        public CharacterTestRuntimeAiLabCandidateState CandidateState { get; internal set; }
        public long CandidateAuthorityHash { get; internal set; }
        public CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution Runtime { get; internal set; } =
            new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution();
        public CharacterTestRuntimeAiLabCompositionActionDiagnostic CompositionWrapperDiagnostic { get; internal set; } =
            new CharacterTestRuntimeAiLabCompositionActionDiagnostic();
        public CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship Relationship { get; internal set; }
    }

    /// <summary>Builds the low-frequency Runtime AI Lab presentation identity without parsing UI text.</summary>
    internal static class CharacterTestRuntimeAiLabBehaviorCardIdentityProjector
    {
        /// <remarks>LowFreqAlloc: Runtime AI Lab presentation refresh only.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution ResolveRuntime(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            int frame,
            in CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent,
            bool hasSelectedAgent,
            bool requireCapturedState = false)
        {
            return ResolveRuntimeInto(
                new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution(),
                controller,
                state,
                frame,
                selectedAgent,
                hasSelectedAgent,
                requireCapturedState);
        }

        /// <remarks>NoAlloc after the supplied projection has initialized its nested action object.</remarks>
        internal static CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution ResolveRuntimeInto(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            int frame,
            in CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent,
            bool hasSelectedAgent,
            bool requireCapturedState = false)
        {
            result = result ?? new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution();
            ResetRuntimeIdentity(result);
            result.SelectedEntityId = hasSelectedAgent ? selectedAgent.EntityId : 0;
            result.Frame = Math.Max(0, frame);
            result.ProfileSlot = hasSelectedAgent ? selectedAgent.RuntimeAiPlannerProfileSlot : 0;
            if (!hasSelectedAgent)
            {
                result.Diagnostic = "当前帧没有行为卡运行状态。";
                return result;
            }
            if (state == null)
            {
                if (requireCapturedState)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.NoRuntimeState,
                        "选中的历史 RuntimeFrame 缺少 Runtime AI Planner state snapshot。");
                }
                result.Diagnostic = "当前尚未捕获行为卡运行状态。";
                return result;
            }
            if (state.BehaviorLogicSlots == null || state.BehaviorLogicSlots.Length == 0)
            {
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.LogicSlotMissing,
                    "当前帧缺少选中 Agent 的 Runtime AI Planner logic slot。");
            }

            CharacterTestRuntimeAiLabLogicSlotStateSnapshot matchedSlot = null;
            for (int i = 0; i < state.BehaviorLogicSlots.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot candidate = state.BehaviorLogicSlots[i];
                if (candidate == null || candidate.Slot != result.ProfileSlot)
                    continue;
                if (matchedSlot != null)
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.LogicSlotDuplicate,
                        "当前帧包含重复的 Runtime AI Planner logic profile slot。");
                matchedSlot = candidate;
            }
            if (matchedSlot == null)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.LogicSlotMissing,
                    "当前帧缺少选中 Agent 的 Runtime AI Planner logic slot。");

            result.AuthorityHash = matchedSlot.AuthorityHash;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot matchedAgent = null;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] logicAgents = matchedSlot.Agents;
            for (int i = 0; logicAgents != null && i < logicAgents.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot candidate = logicAgents[i];
                if (candidate == null || candidate.EntityId != result.SelectedEntityId)
                    continue;
                if (matchedAgent != null)
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.AgentDuplicate,
                        "当前帧包含重复的 Runtime AI Planner logic Agent identity。");
                matchedAgent = candidate;
            }
            if (matchedAgent == null)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.AgentMissing,
                    "当前帧缺少选中 Agent 的行为卡状态。");
            result.LogicAgent = matchedAgent;
            if (matchedAgent.RuntimeState < (int)CharacterTestRuntimeAiLabLogicRuntimeState.Inactive
                || matchedAgent.RuntimeState > (int)CharacterTestRuntimeAiLabLogicRuntimeState.Failed)
            {
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.RuntimeStateInvalid,
                    "当前帧包含无效的行为卡 runtime lifecycle state。");
            }
            result.RuntimeState = (CharacterTestRuntimeAiLabLogicRuntimeState)matchedAgent.RuntimeState;
            result.BindingReleasePending = matchedAgent.BindingReleasePending;

            if (controller == null
                || !controller.TryResolveAppliedRevisionAtFrame(
                    result.Frame,
                    result.ProfileSlot,
                    result.AuthorityHash,
                    out CharacterTestRuntimeAiLabCandidateRevision revision))
            {
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.AppliedRevisionMissing,
                    "历史运行身份不可解析：找不到该帧 authority hash 对应的已应用 revision。");
            }
            result.Revision = revision;
            if (revision.CardIdentities.Count != revision.LogicPrograms.Count)
            {
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.RevisionIdentityInvalid,
                    "已应用 revision 的卡身份、program 与 composition entry 数量不一致。");
            }

            int activeIndex = matchedAgent.ActiveBindingIndex;
            int pendingIndex = matchedAgent.PendingBindingIndex;
            if (activeIndex < -1 || activeIndex >= revision.CardIdentities.Count
                || pendingIndex < -1 || pendingIndex >= revision.CardIdentities.Count)
            {
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.BindingIndexOutOfRange,
                    "当前帧引用了越界的行为卡 binding。");
            }

            if (pendingIndex >= 0)
            {
                result.PendingCard = revision.CardIdentities[pendingIndex];
                if (result.PendingCard == null)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.RevisionIdentityInvalid,
                        "已应用 revision 缺少待进入 binding 的冻结卡身份。");
                }
            }
            if (activeIndex >= 0)
            {
                result.CommittedCard = revision.CardIdentities[activeIndex];
                result.CommittedProgram = revision.LogicPrograms[activeIndex];
                if (result.CommittedCard == null)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.RevisionIdentityInvalid,
                        "已应用 revision 缺少当前 binding 的冻结卡身份。");
                }
                if (result.CommittedProgram == null
                    || result.CommittedCard.CandidateSemanticHash != result.CommittedProgram.SemanticHash
                    || matchedAgent.ProgramSemanticHash != result.CommittedProgram.SemanticHash)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.ProgramSemanticMismatch,
                        "当前帧 behavior-card semantic hash 与已应用 revision 不一致。");
                }

                if (result.CommittedProgram.Execution != null)
                    return ResolveExecutionIdentity(result, matchedAgent);

                int stageIndex = matchedAgent.CurrentStageIndex;
                if (stageIndex < -1 || stageIndex >= result.CommittedProgram.Stages.Count)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.StageIndexOutOfRange,
                        "当前帧引用了越界的 behavior-card Stage。");
                }
                if (stageIndex >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledLogicStage stage = result.CommittedProgram.Stages[stageIndex];
                    result.CurrentStageId = stage.StageId;
                    result.CurrentStageTitle = string.IsNullOrWhiteSpace(stage.Title) ? stage.StageId : stage.Title;
                }

                int actionStageIndex = matchedAgent.PrimaryActionStageIndex;
                if (actionStageIndex < -1 || actionStageIndex >= result.CommittedProgram.Stages.Count)
                {
                    return Fail(result,
                        CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStageIndexOutOfRange,
                        "当前帧引用了越界的主动作 Stage。");
                }
                if (matchedAgent.PrimaryActionState < (int)CharacterTestRuntimeAiLabLogicActionState.None
                    || matchedAgent.PrimaryActionState > (int)CharacterTestRuntimeAiLabLogicActionState.Failed)
                {
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStateInvalid,
                        "当前帧包含无效的行为卡主动作状态。");
                }
                if (actionStageIndex >= 0)
                {
                    CharacterTestRuntimeAiLabCompiledLogicStage actionStage =
                        result.CommittedProgram.Stages[actionStageIndex];
                    if (!actionStage.OwnsPrimaryAction)
                    {
                        return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStageInvalid,
                            "当前帧的主动作来源 Stage 不拥有主动作执行权。");
                    }
                    FillPrimaryAction(result.PrimaryAction, actionStageIndex, actionStage, matchedAgent);
                }
            }

            result.Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved;
            result.Reason = CharacterTestRuntimeAiLabIdentityResolutionReason.None;
            result.Diagnostic = string.Empty;
            return result;
        }

        /// <remarks>LowFreqAlloc: Runtime AI Lab presentation refresh only.</remarks>
        public static CharacterTestRuntimeAiLabBehaviorCardIdentityProjection BuildProjection(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabBehaviorGroupDraft editGroup,
            int selectedEntryIndex,
            long currentEditSemanticHash,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime,
            in CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent,
            bool hasSelectedAgent)
        {
            return BuildProjectionInto(
                new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection(),
                controller,
                editGroup,
                selectedEntryIndex,
                currentEditSemanticHash,
                runtime,
                selectedAgent,
                hasSelectedAgent);
        }

        /// <remarks>NoAlloc after the supplied projection has initialized its nested objects.</remarks>
        internal static CharacterTestRuntimeAiLabBehaviorCardIdentityProjection BuildProjectionInto(
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection projection,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabBehaviorGroupDraft editGroup,
            int selectedEntryIndex,
            long currentEditSemanticHash,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime,
            in CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent,
            bool hasSelectedAgent)
        {
            projection = projection ?? new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection();
            projection.Runtime = runtime ?? projection.Runtime
                ?? new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution();
            projection.CandidateCard = null;
            projection.CandidateRevision = 0;
            projection.CandidateState = controller?.CandidateState ?? CharacterTestRuntimeAiLabCandidateState.None;
            projection.CandidateAuthorityHash = 0L;
            projection.EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.NotApplicable;
            projection.EditResolutionReason = CharacterTestRuntimeAiLabIdentityResolutionReason.None;
            projection.EditDiagnostic = string.Empty;
            projection.Relationship = CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DesignOnly;
            CharacterTestRuntimeAiLabCompositionActionDiagnostic wrapper =
                projection.CompositionWrapperDiagnostic
                ?? new CharacterTestRuntimeAiLabCompositionActionDiagnostic();
            projection.CompositionWrapperDiagnostic = wrapper;
            wrapper.Kind = hasSelectedAgent ? selectedAgent.SelectedActionKind : default;
            wrapper.Name = hasSelectedAgent ? selectedAgent.SelectedActionName ?? string.Empty : string.Empty;

            CharacterTestRuntimeAiLabBehaviorEntryDraft editEntry = null;
            CharacterTestRuntimeAiLabBehaviorCardDraft editCard = null;
            if (editGroup != null && selectedEntryIndex >= 0 && selectedEntryIndex < editGroup.Entries.Count)
            {
                editEntry = editGroup.Entries[selectedEntryIndex];
                controller?.DraftWorkspace?.Cards.TryGetValue(editEntry.CardDraftId, out editCard);
                if (editCard == null)
                {
                    projection.EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved;
                    projection.EditResolutionReason = CharacterTestRuntimeAiLabIdentityResolutionReason.EditCardMissing;
                    projection.EditDiagnostic = "选中的行为卡 Draft 不存在。";
                }
                else if (editCard.HasPublishedSource
                    && (!string.Equals(
                            editCard.PublishedStableId,
                            editCard.PublishedStableId.Trim(),
                            StringComparison.Ordinal)
                        || !CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(
                            editCard.PublishedStableId)))
                {
                    projection.EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved;
                    projection.EditResolutionReason =
                        CharacterTestRuntimeAiLabIdentityResolutionReason.PublishedStableIdNonCanonical;
                    projection.EditDiagnostic = "选中的 Published 行为卡来源 ID 不是 canonical stable ID。";
                }
                else
                {
                    projection.EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved;
                }
            }
            else if (editGroup != null)
            {
                projection.EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved;
                projection.EditResolutionReason = CharacterTestRuntimeAiLabIdentityResolutionReason.EditEntryMissing;
                projection.EditDiagnostic = "选中的行为卡条目不存在。";
            }
            projection.EditCard = projection.EditCard ?? new CharacterTestRuntimeAiLabEditCardIdentity();
            FillEditIdentity(projection.EditCard, editEntry, editCard, currentEditSemanticHash);

            CharacterTestRuntimeAiLabCandidateRevision candidateRevision = controller?.CandidateRevision;
            if (candidateRevision != null && editEntry != null)
            {
                projection.CandidateRevision = candidateRevision.Revision;
                projection.CandidateAuthorityHash = candidateRevision.AuthorityHash;
                projection.CandidateCard = FindCandidateIdentity(candidateRevision, editEntry);
            }

            projection.Relationship = ResolveRelationship(projection.EditCard, projection.Runtime);
            return projection;
        }

        /// <remarks>NoAlloc. Presentation-only match against one immutable Runtime AI Lab revision.</remarks>
        public static int FindMatchingEditEntryIndex(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            CharacterTestRuntimeAiLabRevisionCardIdentity runtimeCard)
        {
            if (group == null || runtimeCard == null)
                return -1;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (string.Equals(entry.CardDraftId, runtimeCard.CardDraftId, StringComparison.Ordinal)
                    || string.Equals(entry.EntryId, runtimeCard.DraftEntryId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution Fail(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result,
            CharacterTestRuntimeAiLabIdentityResolutionReason reason,
            string diagnostic)
        {
            result.Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved;
            result.Reason = reason;
            result.Diagnostic = diagnostic ?? string.Empty;
            return result;
        }

        private static void ResetRuntimeIdentity(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result)
        {
            result.Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.NotApplicable;
            result.Reason = CharacterTestRuntimeAiLabIdentityResolutionReason.NoRuntimeState;
            result.Diagnostic = string.Empty;
            result.SelectedEntityId = 0;
            result.Frame = 0;
            result.ProfileSlot = 0;
            result.AuthorityHash = 0L;
            result.Revision = null;
            result.LogicAgent = null;
            result.CommittedCard = null;
            result.PendingCard = null;
            result.CommittedProgram = null;
            result.RuntimeState = CharacterTestRuntimeAiLabLogicRuntimeState.Inactive;
            result.BindingReleasePending = false;
            result.CurrentStageId = string.Empty;
            result.CurrentStageTitle = string.Empty;
            CharacterTestRuntimeAiLabPrimaryActionIdentity action = result.PrimaryAction
                ?? new CharacterTestRuntimeAiLabPrimaryActionIdentity();
            result.PrimaryAction = action;
            action.Available = false;
            action.StageIndex = -1;
            action.StageId = string.Empty;
            action.StageTitle = string.Empty;
            action.StageKind = default;
            action.AbilityId = 0;
            action.CharacterActionId = default;
            action.LegacyActionKind = default;
            action.MovementStrategy = default;
            action.State = CharacterTestRuntimeAiLabLogicActionState.None;
            action.CommandPending = false;
            action.CommandRejected = false;
            action.ActionObserved = false;
            action.CancelRequested = false;
            action.ClearControlPending = false;
            action.DisplayName = "无主动作";
        }

        internal static CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution ResolveExecutionIdentity(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot agent)
        {
            var program = result.CommittedProgram;
            var execution = program.Execution;
            var state = agent.ExecutionState;
            if (state == null || state.ProgramIdentity != execution.Identity || state.EntityId != result.SelectedEntityId)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.ProgramSemanticMismatch,
                    "当前帧执行表身份与冻结程序或角色不一致。");
            int timeline = state.Timeline;
            if (timeline < -1 || timeline >= execution.Timelines.Length)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.StageIndexOutOfRange,
                    "当前帧引用了越界的执行时间轴。");
            if (timeline >= 0)
            {
                result.CurrentStageId = program.ExecutionDebugMap?.Timeline(timeline) ?? string.Empty;
                result.CurrentStageTitle = result.CurrentStageId;
            }
            if (agent.PrimaryActionState < (int)CharacterTestRuntimeAiLabLogicActionState.None
                || agent.PrimaryActionState > (int)CharacterTestRuntimeAiLabLogicActionState.Failed)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStateInvalid,
                    "当前帧包含无效的动作状态。");
            int owner = state.ActionOwner;
            if (owner < -1 || owner >= execution.Events.Length)
                return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStageIndexOutOfRange,
                    "当前帧引用了越界的动作事件。");
            if (owner >= 0)
            {
                var evt = execution.Events[owner];
                if (evt.TimelineIndex != timeline || evt.Kind != CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction
                    || evt.Parameter < 0 || evt.Parameter >= execution.Actions.Length)
                    return Fail(result, CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStageInvalid,
                        "当前动作事件不属于该时间轴或不是角色动作。");
                var action = result.PrimaryAction;
                action.Available = true;
                action.StageIndex = timeline;
                action.StageId = program.ExecutionDebugMap?.Event(owner) ?? string.Empty;
                action.StageTitle = action.StageId;
                action.StageKind = CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
                action.CharacterActionId = new MxFramework.CharacterAction.CharacterActionId(execution.Actions[evt.Parameter].ActionId);
                action.DisplayName = action.CharacterActionId.ToString();
                action.State = (CharacterTestRuntimeAiLabLogicActionState)agent.PrimaryActionState;
                action.CommandPending = agent.ExecutionRuntimeState?.CommandPending ?? agent.CommandPending;
                action.CommandRejected = action.State == CharacterTestRuntimeAiLabLogicActionState.Failed;
                action.ActionObserved = agent.PrimaryActionInstanceId > 0;
                action.CancelRequested = agent.CancelRequested;
                action.ClearControlPending = agent.ClearControlPending;
            }
            result.Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved;
            result.Reason = CharacterTestRuntimeAiLabIdentityResolutionReason.None;
            result.Diagnostic = string.Empty;
            return result;
        }

        private static void FillPrimaryAction(
            CharacterTestRuntimeAiLabPrimaryActionIdentity result,
            int stageIndex,
            CharacterTestRuntimeAiLabCompiledLogicStage stage,
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot agent)
        {
            result.Available = true;
            result.StageIndex = stageIndex;
            result.StageId = stage.StageId;
            result.StageTitle = string.IsNullOrWhiteSpace(stage.Title) ? stage.StageId : stage.Title;
            result.StageKind = stage.Kind;
            result.AbilityId = stage.AbilityId;
            result.CharacterActionId = stage.CharacterActionId;
            result.LegacyActionKind = stage.LegacyActionKind;
            result.MovementStrategy = stage.MovementStrategy;
            result.State = (CharacterTestRuntimeAiLabLogicActionState)agent.PrimaryActionState;
            result.CommandPending = agent.CommandPending;
            result.CommandRejected = agent.CommandRejected;
            result.ActionObserved = agent.ActionObserved;
            result.CancelRequested = agent.CancelRequested;
            result.ClearControlPending = agent.ClearControlPending;
            result.DisplayName = FormatPrimaryAction(stage);
        }

        private static string FormatPrimaryAction(CharacterTestRuntimeAiLabCompiledLogicStage stage)
        {
            switch (stage.Kind)
            {
                case CharacterTestRuntimeAiLabCardStepKind.UseAbility:
                    return "使用技能 " + stage.AbilityId.ToString(CultureInfo.InvariantCulture);
                case CharacterTestRuntimeAiLabCardStepKind.CharacterAction:
                    return !stage.CharacterActionId.IsValid
                        ? "执行 CharacterAction"
                        : stage.CharacterActionId.ToString();
                case CharacterTestRuntimeAiLabCardStepKind.MoveToTarget:
                    return "移动到目标";
                case CharacterTestRuntimeAiLabCardStepKind.LegacyMigrationOnly:
                    return stage.LegacyActionKind.ToString();
                default:
                    return string.IsNullOrWhiteSpace(stage.Title) ? stage.StageId : stage.Title;
            }
        }

        private static void FillEditIdentity(
            CharacterTestRuntimeAiLabEditCardIdentity result,
            CharacterTestRuntimeAiLabBehaviorEntryDraft entry,
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            long currentSemanticHash)
        {
            result = result ?? throw new ArgumentNullException(nameof(result));
            if (entry == null || card == null)
            {
                result.DraftEntryId = string.Empty;
                result.CardDraftId = string.Empty;
                result.HasPublishedSource = false;
                result.PublishedStableId = string.Empty;
                result.LegacySourceKey = string.Empty;
                result.DisplayName = "未命名行为卡";
                result.DraftRevision = 0;
                result.SourcePublishedRevision = 0;
                result.SourcePublishedSemanticHash = 0L;
                result.SourcePublishedContentHash = 0L;
                result.CurrentSemanticHash = currentSemanticHash;
                return;
            }
            bool hasPublishedSource = card.HasPublishedSource;
            result.DraftEntryId = entry.EntryId;
            result.CardDraftId = card.CardDraftId;
            result.HasPublishedSource = hasPublishedSource;
            result.PublishedStableId = hasPublishedSource ? card.PublishedStableId : string.Empty;
            result.LegacySourceKey = hasPublishedSource ? string.Empty : entry.BehaviorId;
            result.DisplayName = string.IsNullOrWhiteSpace(card.DisplayName) ? "未命名行为卡" : card.DisplayName;
            result.DraftRevision = card.CurrentRevision;
            result.SourcePublishedRevision = hasPublishedSource ? card.SourcePublishedRevision : 0;
            result.SourcePublishedSemanticHash = hasPublishedSource ? card.SourcePublishedSemanticHash : 0L;
            result.SourcePublishedContentHash = hasPublishedSource ? card.SourcePublishedContentHash : 0L;
            result.CurrentSemanticHash = currentSemanticHash;
        }

        private static CharacterTestRuntimeAiLabRevisionCardIdentity FindCandidateIdentity(
            CharacterTestRuntimeAiLabCandidateRevision revision,
            CharacterTestRuntimeAiLabBehaviorEntryDraft editEntry)
        {
            for (int i = 0; i < revision.CardIdentities.Count; i++)
            {
                CharacterTestRuntimeAiLabRevisionCardIdentity identity = revision.CardIdentities[i];
                if (identity != null
                    && (string.Equals(identity.DraftEntryId, editEntry.EntryId, StringComparison.Ordinal)
                        || string.Equals(identity.CardDraftId, editEntry.CardDraftId, StringComparison.Ordinal)))
                {
                    return identity;
                }
            }
            return null;
        }

        /// <remarks>NoAlloc. Compares already validated presentation identities.</remarks>
        internal static CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship ResolveRelationship(
            CharacterTestRuntimeAiLabEditCardIdentity edit,
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime)
        {
            if (edit == null || runtime == null
                || runtime.Status != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved)
            {
                return CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DesignOnly;
            }

            CharacterTestRuntimeAiLabRevisionCardIdentity comparison = runtime.CommittedCard ?? runtime.PendingCard;
            if (comparison == null)
                return CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DesignOnly;

            bool exact = string.Equals(edit.CardDraftId, comparison.CardDraftId, StringComparison.Ordinal)
                || string.Equals(edit.DraftEntryId, comparison.DraftEntryId, StringComparison.Ordinal);
            bool samePublished = edit.HasPublishedSource && comparison.HasPublishedSource
                && string.Equals(edit.PublishedStableId, comparison.PublishedStableId, StringComparison.Ordinal);

            if (runtime.CommittedCard != null && runtime.PendingCard != null
                && !string.Equals(
                    runtime.CommittedCard.RuntimeEntryId,
                    runtime.PendingCard.RuntimeEntryId,
                    StringComparison.Ordinal))
            {
                bool transitionSamePublished = runtime.CommittedCard.HasPublishedSource
                    && runtime.PendingCard.HasPublishedSource
                    && string.Equals(
                        runtime.CommittedCard.PublishedStableId,
                        runtime.PendingCard.PublishedStableId,
                        StringComparison.Ordinal);
                return transitionSamePublished
                    ? CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship
                        .TransitioningBetweenBindingsSamePublishedCard
                    : CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.TransitioningToDifferentCard;
            }

            if (!exact && samePublished)
                return CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship
                    .DifferentRuntimeBindingSamePublishedCard;
            if (!exact)
                return CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DifferentCard;
            return edit.CurrentSemanticHash != 0L
                && edit.CurrentSemanticHash == comparison.CandidateSemanticHash
                    ? CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardSameSemantic
                    : CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardDifferentSemantic;
        }
    }
}
