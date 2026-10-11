using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        /// <summary>Keep every other enabled actor's configured cards in an isolated single-card test.</summary>
        private bool TryPrepareSingleCardPeerInstalls(IReadOnlyList<int> ownerSlots,
            out List<int> peerSlots,
            out List<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest> peerRequests,
            out List<CharacterTestRuntimeAiLabCandidateRevision> peerRevisions,
            out string diagnostic)
        {
            peerSlots = new List<int>();
            peerRequests = new List<CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest>();
            peerRevisions = new List<CharacterTestRuntimeAiLabCandidateRevision>();
            diagnostic = string.Empty;
            if (_candidateRevision?.IsSingleCardTest != true || _matchConfig == null || _sessionBuilder == null)
                return true;

            for (int team = CharacterTestArenaMatchConfig.Team1Id; team <= CharacterTestArenaMatchConfig.Team2Id; team++)
            {
                IReadOnlyList<CharacterTestArenaSlotConfig> slots = team == CharacterTestArenaMatchConfig.Team1Id
                    ? _matchConfig.Team1Slots : _matchConfig.Team2Slots;
                for (int index = 0; index < slots.Count; index++)
                {
                    CharacterTestArenaSlotConfig slot = slots[index];
                    if (slot == null || !slot.Enabled
                        || team == _candidateRevision.TestTeamId && index == _candidateRevision.TestSlotIndex)
                        continue;
                    if (!DraftWorkspace.TryGetGroupForSlot(team, index, out var group))
                    { diagnostic = "陪测角色缺少已配置的行为组。"; return false; }
                    if (group.Entries.Count == 0) continue;
                    if (!_sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(team, index, out int profileSlot)
                        || ownerSlots.Contains(profileSlot) || peerSlots.Contains(profileSlot))
                    { diagnostic = "陪测角色的 Runtime AI Planner profile slot 无效或重复。"; return false; }
                    if (!TryBuildConfiguredPeerRevision(group, team, index, out var revision, out diagnostic)
                        || !TryPrepareRevisionInstalls(new[] { profileSlot }, revision,
                            out var requests, out var installed, out diagnostic)) return false;
                    peerSlots.Add(profileSlot);
                    peerRequests.Add(requests[0]);
                    peerRevisions.Add(installed[0]);
                }
            }
            return true;
        }

        private bool TryBuildConfiguredPeerRevision(CharacterTestRuntimeAiLabBehaviorGroupDraft group,
            int team, int slot, out CharacterTestRuntimeAiLabCandidateRevision revision, out string diagnostic)
        {
            revision = null;
            diagnostic = string.Empty;
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            var sourceProfile = CharacterTestArenaBehaviorGroupDraftStore.CreateRuntimeProfile(
                group.PublishedStableId, group.Profile);
            if (sourceProfile.Entries.Count != group.Entries.Count)
            { diagnostic = "陪测行为组的运行条目数量不一致。"; return false; }
            var profile = sourceProfile.Clone();
            profile.Entries.Clear();
            var entries = new List<CharacterTestRuntimeAiLabBehaviorEntryDraft>(group.Entries.Count);
            var programs = new List<CharacterTestRuntimeAiLabCompiledLogicProgram>(group.Entries.Count);
            int originalTeam = _selectedSetupTeamId, originalSlot = _selectedSetupSlotIndex;
            try
            {
                _selectedSetupTeamId = team;
                _selectedSetupSlotIndex = slot;
                foreach (CharacterTestRuntimeAiPlannerBehaviorModuleEntry profileEntry in sourceProfile.Entries)
                {
                    CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries.FirstOrDefault(value =>
                        value?.RuntimeEntry != null && string.Equals(value.RuntimeEntry.EntryId,
                            profileEntry.EntryId, StringComparison.Ordinal));
                    if (entry == null || !workspace.Cards.TryGetValue(entry.CardDraftId, out var card))
                    { diagnostic = "陪测行为组缺少运行条目或行为卡：" + profileEntry.EntryId; return false; }
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compiled = CompileBehaviorCardForStart(card);
                    if (compiled?.Success != true || compiled.Program == null)
                    { diagnostic = "陪测行为卡“" + card.DisplayName + "”不可运行："
                        + FormatBehaviorCardCompileFailure(card, compiled); return false; }
                    profile.Entries.Add(profileEntry.Clone());
                    entries.Add(entry);
                    programs.Add(compiled.Program);
                }
            }
            finally
            {
                _selectedSetupTeamId = originalTeam;
                _selectedSetupSlotIndex = originalSlot;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompileResult composition =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    profile, _matchConfig.BehaviorGroups.DefinitionCatalog);
            if (composition?.Success != true)
            { diagnostic = "陪测行为组的基础组合无法编译。"; return false; }
            long hash = ComputeCandidateCompositionHash(profile, _matchConfig.BehaviorGroups.DefinitionCatalog);
            var identities = new CharacterTestRuntimeAiLabRevisionCardIdentity[programs.Count];
            int stepCount = 0, conditionCount = 0;
            for (int i = 0; i < programs.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledLogicProgram program = programs[i];
                hash = MixCandidateHash(hash, entries[i].EntryId);
                hash = MixCandidateHash(hash, program.SemanticHash);
                if (program.SelectionDefinition != null)
                    hash = MixCandidateHash(hash, program.SelectionDefinition.StableHash);
                if (!CharacterTestRuntimeAiLabRevisionCardIdentity.TryCreate(i, entries[i],
                        workspace.Cards[entries[i].CardDraftId], program, out identities[i], out diagnostic)) return false;
                stepCount += program.Execution != null ? program.Execution.Events.Length : program.Stages.Count;
                conditionCount += program.Execution != null ? program.Execution.Conditions.Length : program.ActivationConditions.Count;
            }
            var comparison = new CharacterTestRuntimeAiLabCandidateComparisonReport(
                programs.Count, stepCount, conditionCount, group.DisplayName, hash, repeatable: true);
            return CharacterTestRuntimeAiLabCandidateRevision.TryCreate(workspace.CurrentRevision, hash,
                profile, comparison, programs.ToArray(), identities, group.GroupDraftId,
                out revision, out diagnostic);
        }
    }
}
