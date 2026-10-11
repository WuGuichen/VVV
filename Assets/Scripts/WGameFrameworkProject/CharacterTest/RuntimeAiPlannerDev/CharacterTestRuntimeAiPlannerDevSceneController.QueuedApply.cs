using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabQueuedApplyCancelReason
    {
        None = 0,
        Explicit = 1,
        AgentChanged = 2,
        SetupTargetChanged = 3,
        WorkspaceAuthorityChanged = 4,
        SessionChanged = 5,
        TargetIdentityChanged = 6,
        TargetUnavailable = 7
    }

    internal readonly struct CharacterTestRuntimeAiLabQueuedApplyTargetIdentity
    {
        internal CharacterTestRuntimeAiLabQueuedApplyTargetIdentity(
            int timelineSessionGeneration,
            long sessionAuthorityHash,
            int selectedAgentEntityId,
            int selectedCharacterId,
            int runtimeProfileSlot,
            int setupTeamId,
            int setupSlotIndex,
            int setupCharacterId,
            int runtimeLoadoutId,
            int runtimeVariantId,
            long runtimeCatalogHash,
            int groupDraftId,
            long candidateAuthorityHash)
        {
            TimelineSessionGeneration = timelineSessionGeneration;
            SessionAuthorityHash = sessionAuthorityHash;
            SelectedAgentEntityId = selectedAgentEntityId;
            SelectedCharacterId = selectedCharacterId;
            RuntimeProfileSlot = runtimeProfileSlot;
            SetupTeamId = setupTeamId;
            SetupSlotIndex = setupSlotIndex;
            SetupCharacterId = setupCharacterId;
            RuntimeLoadoutId = runtimeLoadoutId;
            RuntimeVariantId = runtimeVariantId;
            RuntimeCatalogHash = runtimeCatalogHash;
            GroupDraftId = groupDraftId;
            CandidateAuthorityHash = candidateAuthorityHash;
        }

        internal int TimelineSessionGeneration { get; }
        internal long SessionAuthorityHash { get; }
        internal int SelectedAgentEntityId { get; }
        internal int SelectedCharacterId { get; }
        internal int RuntimeProfileSlot { get; }
        internal int SetupTeamId { get; }
        internal int SetupSlotIndex { get; }
        internal int SetupCharacterId { get; }
        internal int RuntimeLoadoutId { get; }
        internal int RuntimeVariantId { get; }
        internal long RuntimeCatalogHash { get; }
        internal int GroupDraftId { get; }
        internal long CandidateAuthorityHash { get; }

        internal bool IsValid => TimelineSessionGeneration > 0
            && SessionAuthorityHash != 0L
            && SelectedAgentEntityId > 0
            && SelectedCharacterId > 0
            && RuntimeProfileSlot >= 0
            && SetupTeamId > 0
            && SetupSlotIndex >= 0
            && SetupCharacterId > 0
            && RuntimeLoadoutId > 0
            && GroupDraftId > 0
            && CandidateAuthorityHash != 0L;

        internal bool Matches(in CharacterTestRuntimeAiLabQueuedApplyTargetIdentity other)
        {
            return TimelineSessionGeneration == other.TimelineSessionGeneration
                && SessionAuthorityHash == other.SessionAuthorityHash
                && SelectedAgentEntityId == other.SelectedAgentEntityId
                && SelectedCharacterId == other.SelectedCharacterId
                && RuntimeProfileSlot == other.RuntimeProfileSlot
                && SetupTeamId == other.SetupTeamId
                && SetupSlotIndex == other.SetupSlotIndex
                && SetupCharacterId == other.SetupCharacterId
                && RuntimeLoadoutId == other.RuntimeLoadoutId
                && RuntimeVariantId == other.RuntimeVariantId
                && RuntimeCatalogHash == other.RuntimeCatalogHash
                && GroupDraftId == other.GroupDraftId
                && CandidateAuthorityHash == other.CandidateAuthorityHash;
        }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private CharacterTestRuntimeAiLabCandidateRevision _queuedCandidateRevision;
        private CharacterTestRuntimeAiLabQueuedApplyTargetIdentity _queuedApplyTarget;

        internal CharacterTestRuntimeAiLabCandidateRevision QueuedCandidateRevision =>
            _queuedCandidateRevision;
        internal CharacterTestRuntimeAiLabQueuedApplyTargetIdentity QueuedApplyTarget =>
            _queuedApplyTarget;

        private int ResolveQueuedApplySessionGeneration()
        {
            if (_timelineSessionStarted)
                return _timelineSessionGeneration;
            return _timelineSessionGeneration == int.MaxValue
                ? 1
                : _timelineSessionGeneration + 1;
        }

        private bool TryCaptureQueuedApplyTarget(
            CharacterTestRuntimeAiLabCandidateRevision candidate,
            out CharacterTestRuntimeAiLabQueuedApplyTargetIdentity identity,
            out string diagnostic)
        {
            identity = default;
            diagnostic = string.Empty;
            if (candidate?.Profile == null || candidate.AuthorityHash == 0L)
            {
                diagnostic = "候选行为卡版本缺少可冻结的 authority 身份。";
                return false;
            }

            int sessionGeneration = ResolveQueuedApplySessionGeneration();
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || sessionGeneration <= 0
                || _context?.Slice == null)
            {
                diagnostic = "当前 Runtime AI Lab 会话身份不可用。";
                return false;
            }

            CharacterTestArenaSlotConfig setupSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (setupSlot == null
                || !setupSlot.Enabled
                || setupSlot.CharacterId <= 0
                || setupSlot.BehaviorGroupDraftId <= 0)
            {
                diagnostic = "当前测试槽位无法作为 Apply F+1 目标。";
                return false;
            }

            CharacterTestRuntimeAiPlannerSnapshot runtimeSnapshot =
                _context.Slice.RuntimeAiPlannerCommands?.CreateSnapshot();
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = runtimeSnapshot?.Agents;
            if (agents == null || agents.Count == 0
                || _selectedAgentIndex < 0
                || _selectedAgentIndex >= agents.Count)
            {
                diagnostic = "当前没有可冻结的 Runtime AI Planner Agent。";
                return false;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent = agents[_selectedAgentIndex];
            int profileSlot = ResolveSelectedRuntimeProfileSlot();
            int expectedLoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                setupSlot.BehaviorGroupDraftId);
            int runtimeLoadoutId = selectedAgent.RuntimeAiBehaviorLoadoutId > 0
                ? selectedAgent.RuntimeAiBehaviorLoadoutId
                : expectedLoadoutId;
            if (selectedAgent.EntityId <= 0
                || selectedAgent.RuntimeAiPlannerProfileSlot != profileSlot
                || runtimeLoadoutId != expectedLoadoutId)
            {
                diagnostic = "选中 Agent 与当前行为卡测试槽位不一致；请先选择对应 Agent。";
                return false;
            }

            int selectedCharacterId = ResolveRuntimeCharacterId(selectedAgent.EntityId);
            if (selectedCharacterId <= 0 || selectedCharacterId != setupSlot.CharacterId)
            {
                diagnostic = "选中 Agent 的角色身份与当前测试槽位不一致。";
                return false;
            }

            CharacterTestRuntimeAiPlannerStateSnapshot state =
                _context.Slice.Services.Modules.RuntimeAiPlannerCommands?.CreateStateSnapshot();
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot matchedLogicSlot = null;
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] logicSlots = state?.BehaviorLogicSlots;
            for (int i = 0; logicSlots != null && i < logicSlots.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot logicSlot = logicSlots[i];
                if (logicSlot == null || logicSlot.Slot != profileSlot)
                    continue;
                if (matchedLogicSlot != null)
                {
                    diagnostic = "运行时包含重复的行为逻辑 profile slot。";
                    return false;
                }
                matchedLogicSlot = logicSlot;
            }
            if (matchedLogicSlot == null
                || matchedLogicSlot.AuthorityHash == 0L
                || !ContainsLogicAgent(matchedLogicSlot.Agents, selectedAgent.EntityId))
            {
                diagnostic = "选中 Agent 的运行时行为逻辑 authority 尚未建立。";
                return false;
            }

            identity = new CharacterTestRuntimeAiLabQueuedApplyTargetIdentity(
                sessionGeneration,
                matchedLogicSlot.AuthorityHash,
                selectedAgent.EntityId,
                selectedCharacterId,
                profileSlot,
                setupSlot.TeamId,
                setupSlot.SlotIndex,
                setupSlot.CharacterId,
                runtimeLoadoutId,
                selectedAgent.RuntimeAiBehaviorVariantId,
                selectedAgent.RuntimeAiBehaviorCatalogHash,
                setupSlot.BehaviorGroupDraftId,
                candidate.AuthorityHash);
            if (!identity.IsValid)
            {
                diagnostic = "Apply F+1 目标身份不完整。";
                identity = default;
                return false;
            }
            return true;
        }

        private int ResolveRuntimeCharacterId(int entityId)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _context?.Slice?.Services.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null && entity.Id.Value == entityId)
                    return entity.Identity.CharacterId;
            }
            return 0;
        }

        private static bool ContainsLogicAgent(
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents,
            int entityId)
        {
            for (int i = 0; agents != null && i < agents.Length; i++)
            {
                if (agents[i] != null && agents[i].EntityId == entityId)
                    return true;
            }
            return false;
        }

        private bool TryQueueCandidateApply(
            CharacterTestRuntimeAiLabCandidateRevision candidate,
            int applicationFrame,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (!TryCaptureQueuedApplyTarget(candidate, out var target, out diagnostic))
                return false;
            _queuedCandidateRevision = candidate;
            _queuedApplyTarget = target;
            _candidateApplyQueued = true;
            _candidateApplyFrame = Math.Max(0, applicationFrame);
            _candidateState = CharacterTestRuntimeAiLabCandidateState.ApplyPending;
            return true;
        }

        internal bool CancelQueuedApply(
            CharacterTestRuntimeAiLabQueuedApplyCancelReason reason,
            string diagnostic)
        {
            if (!_candidateApplyQueued
                && _queuedCandidateRevision == null
                && !_queuedApplyTarget.IsValid)
            {
                return false;
            }

            ClearQueuedApplyState();
            if (_candidateRevision != null)
                _candidateState = CharacterTestRuntimeAiLabCandidateState.PreviewComplete;
            string reasonText = string.IsNullOrWhiteSpace(diagnostic)
                ? "Apply F+1 已取消（" + reason + "）。"
                : diagnostic;
            _candidateDiagnostic = reasonText;
            _statusMessage = reasonText;
            return true;
        }

        private void ClearQueuedApplyState()
        {
            _candidateApplyQueued = false;
            _candidateApplyFrame = 0;
            _queuedCandidateRevision = null;
            _queuedApplyTarget = default;
        }
    }
}
