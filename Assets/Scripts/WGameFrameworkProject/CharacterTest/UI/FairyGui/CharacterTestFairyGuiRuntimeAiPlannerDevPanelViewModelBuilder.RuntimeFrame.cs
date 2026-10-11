using System.Collections.Generic;
using System.Globalization;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
    {
        private static void FillRuntimeFrameWorkspace(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel root,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            in CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiLabTimelineProjection projection,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame,
            int selectedAgentIndex)
        {
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel view = root.RuntimeFrame
                ?? new CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel();
            root.RuntimeFrame = view;
            view.Projection = projection;
            view.SelectedItem = root.TimelineSelectedItem;
            view.ClearTimelineDetails();
            view.AgentOptions = BuildRuntimeAgentOptions(
                controller,
                selectedFrame,
                selectedAgentIndex);
            int clampedAgentIndex = ClampIndex(selectedAgentIndex, view.AgentOptions.Length);
            CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel selectedAgent =
                view.AgentOptions.Length > 0
                    ? view.AgentOptions[clampedAgentIndex]
                    : null;
            view.AgentEntityId = selectedAgent?.EntityId ?? 0;
            view.AgentSlotLabel = selectedAgent?.SlotLabel ?? string.Empty;
            view.AgentDisplayName = selectedAgent?.DisplayName ?? string.Empty;
            view.SelectionPhase = snapshot.FrameSelectionPhase;
            view.ReplayInProgress = snapshot.ReplayInProgress;
            view.HistoryPlaybackActive = snapshot.HistoryPlaybackActive;
            view.FutureAdvanceActive = controller.FutureAdvanceActive;
            view.FutureAdvanceTargetFrame = controller.FutureAdvanceTargetFrame;
            view.BranchRecomputeArmed = snapshot.BranchRecomputeArmed;
            view.BranchConfirmationPending = snapshot.BranchConfirmationPending;
            view.FrameLimit = controller.FrameLimit;
            view.CanSelectFrame = root.CanSelectFrame;
            view.CanStepBackward = root.CanStepBack;
            view.CanStepForward = root.CanStep10;
            view.CanPauseResume = root.CanPauseResume;
            view.CanJumpFrame = root.CanJumpFrame;
            view.CanPan = root.CanPanTimeline;
            view.CanZoom = root.CanZoomTimeline;
            view.CanSetLoop = root.CanSetLoop;
            view.CanSetLoopRange = root.CanSetLoopRange;
            view.CanToggleBranchRecompute = root.CanToggleBranchRecompute;
            view.PauseResumeLabel = root.PauseResumeLabel;
            view.BranchRecomputeLabel = root.BranchRecomputeLabel;
            view.BranchRecomputeRiskText = root.BranchRecomputeRiskText;
            view.JumpFrameInputText = root.JumpFrameInputText;

            if (timeline == null || timeline.Count <= 0 || projection == null)
            {
                view.Available = false;
                view.ContextText = "尚未捕获 RuntimeFrame";
                view.RangeText = "开始测试后显示 retained history";
                view.StatusText = projection?.StatusMessage ?? string.Empty;
                return;
            }

            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = snapshot.CommittedFrameReceipt;
            if (!receipt.IsValid
                || !timeline.SessionIdentity.Equals(receipt.Bundle.SessionIdentity)
                || projection.Domain.SelectedFrame != receipt.Frame
                || !timeline.TryGetFrame(receipt.Frame, out CharacterTestRuntimeAiPlannerDevTimelineRecord selectedRecord)
                || selectedRecord.RecordGeneration != receipt.Bundle.RecordGeneration
                || selectedRecord.StateHash != receipt.AuthorityHash)
            {
                view.Available = false;
                view.ContextText = "当前帧记录暂不可用";
                view.RangeText = string.Empty;
                view.StatusText = "当前帧记录与时间轴不一致，详情已暂时隐藏。";
                return;
            }

            view.Available = true;
            view.SessionGeneration = receipt.Bundle.SessionIdentity.Generation;
            view.CommitGeneration = receipt.CommitGeneration;
            view.RecordGeneration = receipt.Bundle.RecordGeneration;
            view.AuthorityHash = receipt.AuthorityHash;
            view.SessionStartFrame = projection.Domain.SessionStartFrame;
            view.RetainedStartFrame = projection.Domain.RetainedStartFrame;
            view.SelectedFrame = receipt.Frame;
            view.LatestFrame = projection.Domain.LatestFrame;
            view.Viewport = projection.Viewport;
            view.LoopEnabled = timeline.LoopEnabled;
            view.LoopStartFrame = timeline.LoopStartFrame;
            view.LoopEndFrame = timeline.LoopEndFrame;
            view.ContextText = "智能体 "
                + view.AgentEntityId.ToString(CultureInfo.InvariantCulture)
                + " · 当前第 "
                + view.SelectedFrame.ToString(CultureInfo.InvariantCulture)
                + " 帧";
            view.RangeText = "已保留 "
                + view.RetainedStartFrame.ToString(CultureInfo.InvariantCulture)
                + "…"
                + view.LatestFrame.ToString(CultureInfo.InvariantCulture)
                + " 帧 · 上限 "
                + view.FrameLimit.ToString(CultureInfo.InvariantCulture);
            view.StatusText = projection.StatusMessage;
            FillTimelineDetailViewModels(view, controller, projection);
        }

        private static CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[]
            BuildRuntimeAgentOptions(
                CharacterTestRuntimeAiPlannerDevSceneController controller,
                CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame,
                int selectedAgentIndex)
        {
            CharacterTestArenaMatchConfig matchConfig = controller?.MatchConfig;
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                selectedFrame?.Agents;
            int count = agents?.Count ?? 0;
            if (count <= 0)
                return System.Array.Empty<CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel>();

            var options = new CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel[count];
            var claimedSlots = new bool[CharacterTestArenaMatchConfig.SlotsPerTeam * 2];
            int clampedSelectedAgentIndex = ClampIndex(selectedAgentIndex, count);
            for (int agentIndex = 0; agentIndex < count; agentIndex++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[agentIndex];
                if (TryResolveRuntimeAgentSlot(
                        controller,
                        matchConfig,
                        agent,
                        claimedSlots,
                        requireIdentityMatch: true,
                        out int teamId,
                        out int slotIndex,
                        out CharacterTestArenaSlotConfig slot)
                    || TryResolveRuntimeAgentSlot(
                        controller,
                        matchConfig,
                        agent,
                        claimedSlots,
                        requireIdentityMatch: false,
                        out teamId,
                        out slotIndex,
                        out slot))
                {
                    options[agentIndex] = CreateRuntimeAgentOption(
                        agent,
                        agentIndex,
                        teamId,
                        slotIndex,
                        ResolveCharacterDisplay(slot),
                        clampedSelectedAgentIndex);
                    continue;
                }

                options[agentIndex] = CreateRuntimeAgentOption(
                    agent,
                    agentIndex,
                    0,
                    agentIndex,
                    "智能体 " + agent.EntityId.ToString(CultureInfo.InvariantCulture),
                    clampedSelectedAgentIndex);
            }
            return options;
        }

        private static bool TryResolveRuntimeAgentSlot(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestArenaMatchConfig matchConfig,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent,
            bool[] claimedSlots,
            bool requireIdentityMatch,
            out int teamId,
            out int slotIndex,
            out CharacterTestArenaSlotConfig slot)
        {
            for (int candidateTeamId = CharacterTestArenaMatchConfig.Team1Id;
                 candidateTeamId <= CharacterTestArenaMatchConfig.Team2Id;
                 candidateTeamId++)
            {
                for (int candidateSlotIndex = 0;
                     candidateSlotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam;
                     candidateSlotIndex++)
                {
                    int flatIndex = candidateTeamId == CharacterTestArenaMatchConfig.Team1Id
                        ? candidateSlotIndex
                        : CharacterTestArenaMatchConfig.SlotsPerTeam + candidateSlotIndex;
                    CharacterTestArenaSlotConfig candidate =
                        matchConfig?.TryGetSlot(candidateTeamId, candidateSlotIndex);
                    if (candidate == null || !candidate.Enabled || claimedSlots[flatIndex])
                        continue;
                    if (requireIdentityMatch)
                    {
                        int expectedLoadoutId =
                            CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                                candidate.BehaviorGroupDraftId);
                        if (!controller.TryResolveSetupRuntimeAiPlannerProfileSlot(
                                candidateTeamId,
                                candidateSlotIndex,
                                out int expectedProfileSlot)
                            || agent.RuntimeAiPlannerProfileSlot != expectedProfileSlot
                            || (agent.RuntimeAiBehaviorLoadoutId > 0
                                && agent.RuntimeAiBehaviorLoadoutId != expectedLoadoutId))
                        {
                            continue;
                        }
                    }

                    claimedSlots[flatIndex] = true;
                    teamId = candidateTeamId;
                    slotIndex = candidateSlotIndex;
                    slot = candidate;
                    return true;
                }
            }

            teamId = 0;
            slotIndex = 0;
            slot = null;
            return false;
        }

        private static CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel
            CreateRuntimeAgentOption(
                CharacterTestRuntimeAiPlannerAgentSnapshot agent,
                int agentIndex,
                int teamId,
                int slotIndex,
                string displayName,
                int selectedAgentIndex)
        {
            string slotLabel = teamId > 0
                ? "T"
                    + teamId.ToString(CultureInfo.InvariantCulture)
                    + "-"
                    + (slotIndex + 1).ToString(CultureInfo.InvariantCulture)
                : "A" + (agentIndex + 1).ToString(CultureInfo.InvariantCulture);
            return new CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel
            {
                AgentIndex = agentIndex,
                EntityId = agent.EntityId,
                TeamId = teamId,
                SlotIndex = slotIndex,
                SlotLabel = slotLabel,
                DisplayName = displayName ?? string.Empty,
                Selected = agentIndex == selectedAgentIndex
            };
        }

        private static void FillTimelineDetailViewModels(
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel view,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabTimelineProjection projection)
        {
            if (view == null || projection == null)
                return;
            for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
            {
                IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                    projection.Tracks[trackIndex].Items;
                for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    AddTimelineDetailTree(view, controller, items[itemIndex]);
            }
            if (view.TryGetTimelineDetail(view.SelectedItem, out CharacterTestRuntimeAiLabTimelineDetailViewModel selected))
                view.SelectedDetail = selected;
        }

        private static void AddTimelineDetailTree(
            CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel view,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (item == null)
                return;
            view.AddTimelineDetail(
                CharacterTestRuntimeAiLabTimelineDetailProjector.Project(item, controller));
            for (int memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                AddTimelineDetailTree(view, controller, item.Members[memberIndex]);
        }
    }
}
