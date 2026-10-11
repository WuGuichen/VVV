using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink
    {
        private static bool TryParseFactsScrollPayload(
            string payload,
            out float factOffset,
            out float candidateOffset,
            out float deltaOffset)
        {
            factOffset = 0f;
            candidateOffset = 0f;
            deltaOffset = 0f;
            if (string.IsNullOrEmpty(payload))
                return false;
            int first = payload.IndexOf(',');
            int second = first >= 0 ? payload.IndexOf(',', first + 1) : -1;
            return first > 0
                && second > first + 1
                && float.TryParse(
                    payload.Substring(0, first),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out factOffset)
                && float.TryParse(
                    payload.Substring(first + 1, second - first - 1),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out candidateOffset)
                && float.TryParse(
                    payload.Substring(second + 1),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out deltaOffset);
        }

        private string SelectFactsAgent(string payload)
        {
            if (_controller == null
                || !int.TryParse(
                    payload,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int entityId))
            {
                return "FACT_AGENT_SELECTION_INVALID";
            }
            _controller.SelectFactsAgent(entityId, out string diagnostic);
            return diagnostic;
        }

        private string LocateFactsBehaviorNode()
        {
            if (_controller == null)
                return "运行时智能实验室控制器不可用。";
            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot = _controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt = snapshot.CommittedFrameReceipt;
            if (!receipt.IsValid)
                return "Committed frame evidence is unavailable.";
            _controller.CaptureFactsNavigationReturnToken();
            CharacterTestRuntimeAiPlannerDevFrameSnapshot frame = receipt.Bundle.Record.FrameSnapshot;
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame?.Agents;
            int selectedAgentIndex = FindAgentIndex(agents, receipt.ResolvedAgentEntityId);
            if (selectedAgentIndex < 0)
                return "当前没有可定位的运行智能体。";

            CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[selectedAgentIndex];
            CharacterTestRuntimeAiPlannerStateSnapshot state = frame.RuntimeAiPlannerStateSnapshot;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot logic = FindFactsLogicAgent(
                state,
                agent.EntityId,
                agent.RuntimeAiPlannerProfileSlot);
            if (logic == null)
                return "当前智能体尚未进入行为卡阶段。";

            SelectedWorkbenchEntryIndex = Math.Max(0, logic.ActiveBindingIndex);
            if (!_controller.TryResolveFactsBehaviorSelection(
                SelectedWorkbenchEntryIndex,
                out CharacterTestRuntimeAiLabLogicElementKind selectedKind,
                out int selectedIndex))
            {
                return "FACT_BEHAVIOR_TARGET_UNMAPPED";
            }
            SelectedCompiledStepIndex = selectedKind == CharacterTestRuntimeAiLabLogicElementKind.Stage
                ? Math.Max(0, selectedIndex)
                : Math.Max(0, logic.CurrentStageIndex);
            SelectedLogicElementKind = selectedKind;
            SelectedLogicElementIndex = selectedIndex;
            SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            return "已定位到当前行为阶段。";
        }

        /// <summary>
        /// Aligns the Behavior Plan presentation with the behavior card that is
        /// committed for the selected Agent in the latest frame.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc: explicit Trigger/presentation navigation only. Runtime
        /// authority and the committed frame are read but never mutated.
        /// </remarks>
        internal bool TrySelectCurrentRuntimeBehaviorForPresentation(
            out string diagnostic)
        {
            return TrySelectCurrentRuntimeBehaviorForPresentation(
                openAuthoringWorkspace: true,
                out diagnostic);
        }

        private bool TrySelectCurrentRuntimeBehaviorForPresentation(
            bool openAuthoringWorkspace,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_controller == null)
            {
                diagnostic = "运行时智能实验室控制器不可用。";
                return false;
            }

            CharacterTestRuntimeAiPlannerDevSceneSnapshot snapshot =
                _controller.CaptureSnapshot();
            CharacterTestRuntimeAiLabCommittedFrameReceipt receipt =
                snapshot.CommittedFrameReceipt;
            if (!receipt.IsValid)
            {
                diagnostic = "Committed frame evidence is unavailable.";
                return false;
            }

            CharacterTestRuntimeAiPlannerDevFrameSnapshot frame =
                receipt.Bundle.Record.FrameSnapshot;
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                frame?.Agents;
            int selectedAgentIndex = FindAgentIndex(
                agents,
                receipt.ResolvedAgentEntityId);
            if (selectedAgentIndex < 0)
            {
                diagnostic = "当前没有可定位的运行智能体。";
                return false;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent =
                agents[selectedAgentIndex];
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                    _controller,
                    frame.RuntimeAiPlannerStateSnapshot,
                    frame.Frame,
                    selectedAgent,
                    hasSelectedAgent: true,
                    requireCapturedState: true);
            if (runtime.Status
                    != CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved
                || runtime.CommittedCard == null
                || runtime.LogicAgent == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(runtime.Diagnostic)
                    ? "当前智能体尚未进入行为卡阶段。"
                    : runtime.Diagnostic;
                return false;
            }

            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                _controller.DraftWorkspace;
            if (!workspace.TryGetGroupForSlot(
                    _controller.SelectedSetupTeamId,
                    _controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                diagnostic = "当前测试槽位缺少行为组草稿。";
                return false;
            }

            int entryIndex = -1;
            for (int index = 0; index < group.Entries.Count; index++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry =
                    group.Entries[index];
                if (entry != null
                    && string.Equals(
                        entry.EntryId,
                        runtime.CommittedCard.DraftEntryId,
                        StringComparison.Ordinal))
                {
                    entryIndex = index;
                    break;
                }
            }
            if (entryIndex < 0)
            {
                diagnostic = "当前运行行为卡不在所选行为组草稿中。";
                return false;
            }

            SelectedWorkbenchEntryIndex = entryIndex;
            SelectedCompiledStepIndex = Math.Max(
                0,
                runtime.LogicAgent.CurrentStageIndex);
            SelectedLogicElementKind =
                CharacterTestRuntimeAiLabLogicElementKind.Stage;
            SelectedLogicElementIndex = SelectedCompiledStepIndex;
            if (openAuthoringWorkspace)
                SetWorkspaceMode(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard);
            diagnostic = "已定位到当前运行行为卡。";
            return true;
        }

        private static int FindAgentIndex(
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents,
            int entityId)
        {
            if (agents == null || entityId <= 0)
                return -1;
            for (int index = 0; index < agents.Count; index++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[index];
                if (agent.EntityId == entityId)
                    return index;
            }
            return -1;
        }

        private static CharacterTestRuntimeAiLabLogicAgentStateSnapshot FindFactsLogicAgent(
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            int entityId,
            int profileSlot)
        {
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] slots = state?.BehaviorLogicSlots;
            if (slots == null)
                return null;
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot = slots[slotIndex];
                if (slot == null || slot.Slot != profileSlot || slot.Agents == null)
                    continue;
                for (int agentIndex = 0; agentIndex < slot.Agents.Length; agentIndex++)
                {
                    CharacterTestRuntimeAiLabLogicAgentStateSnapshot candidate = slot.Agents[agentIndex];
                    if (candidate != null && candidate.EntityId == entityId)
                        return candidate;
                }
            }
            return null;
        }
    }
}
